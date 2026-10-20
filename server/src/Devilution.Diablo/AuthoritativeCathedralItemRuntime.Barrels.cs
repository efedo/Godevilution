namespace Devilution.Diablo;

/** Prepared native barrel mutation; it never changes the live item pool. */
internal sealed class AuthoritativeCathedralItemBarrelTransaction
{
    internal AuthoritativeCathedralItemBarrelTransaction(
        uint levelId,
        int expectedVersion,
        int expectedUniqueVersion,
        Guid runtimeIdentity,
        AuthoritativeCathedralBarrelOperationResult operation,
        AuthoritativeCathedralNativeItemProjection? projection)
    {
        LevelId = levelId;
        ExpectedVersion = expectedVersion;
        ExpectedUniqueVersion = expectedUniqueVersion;
        RuntimeIdentity = runtimeIdentity;
        Operation = operation;
        Projection = projection;
    }

    public uint LevelId { get; }
    public AuthoritativeCathedralBarrelOperationResult Operation { get; }
    internal int ExpectedVersion { get; }
    internal int ExpectedUniqueVersion { get; }
    internal Guid RuntimeIdentity { get; }
    internal AuthoritativeCathedralNativeItemProjection? Projection { get; }
}

/** Visible item results from one committed native barrel operation. */
internal sealed record AuthoritativeCathedralItemBarrelMutationResult(
    uint LevelId,
    uint FinalWorldRandomState,
    AuthoritativeCathedralBarrelOperationResult Operation,
    IReadOnlyList<AuthoritativeCathedralNativeItemProjection> Projections);

public partial class AuthoritativeCathedralItemRuntime
{
    /** Prepare one native barrel operation against a versioned pool snapshot. */
    internal AuthoritativeCathedralItemBarrelTransaction PrepareBarrelOperation(
        uint levelId,
        AuthoritativeCathedralBarrelOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var level = GetLevel(levelId);
        var levelProfile = GetLevelProfile(levelId);
        var family = NativeItemFamily(levelId);
        var facts = request.Barrel;
        if (request.Policy.Depth != levelProfile.Depth
            || request.Policy.GameMode != DiabloGameMode.SinglePlayer
            || facts.LevelId != levelId)
            throw new InvalidDataException("Native barrel item transactions require the owned single-player Cathedral pool.");
        if (!levelProfile.ObjectBindings.Any(binding =>
                binding.NativeObjectSlot == facts.ObjectSlot
                && binding.EntityId == facts.EntityId
                && binding.ObjectType == facts.NativeTypeId
                && binding.MutationPolicy == AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel
                && binding.PositionX == facts.PositionX
                && binding.PositionY == facts.PositionY))
            throw new InvalidDataException("Native barrel facts do not match an explicitly bound object.");
        if (request.WorldState.ActiveItemCount != level.ActiveItemCount
            || !request.WorldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds)
            || !request.WorldState.Items.SequenceEqual(level.Items)
            || !request.WorldState.ItemRefs.SequenceEqual(level.SignedItemRefs)
            || !request.WorldState.UniqueItemFlags.SetEquals(uniqueItemFlags))
            throw new InvalidOperationException("Native barrel operation was prepared from stale or altered item-pool state.");

        var operation = family.IsCatacombs
            ? AuthoritativeCathedralBarrelOperationGenerator.Generate(request, family)
            : AuthoritativeCathedralBarrelOperationGenerator.Generate(request);
        AuthoritativeCathedralNativeItemProjection? projection = null;
        if (operation.Loot is { Allocated: true, Visible: true, Item: { } item } loot) {
            projection = ProjectNativeItem(
                loot.Slot,
                0,
                loot.ItemSeed,
                PriceFor(item),
                item,
                GetBinding(item.ItemIndex),
                profile.ContentInventoryDimensions);
        }
        return new(levelId, level.Version, uniqueHistoryVersion, runtimeIdentity, operation, projection);
    }

    /**
     * Commit only after full item-checkpoint preflight and owner revalidation.
     * The owner callback atomically commits the broken-object state and any
     * linked actor activation; combat owns explosive damage and chain effects.
     */
    internal AuthoritativeCathedralItemBarrelMutationResult? CommitBarrelOperation(
        AuthoritativeCathedralItemBarrelTransaction transaction,
        Func<uint> allocateEntityId,
        Func<uint, bool> isEntityIdOccupied,
        Func<bool> canCommitOwner,
        Action commitOwner)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(allocateEntityId);
        ArgumentNullException.ThrowIfNull(isEntityIdOccupied);
        ArgumentNullException.ThrowIfNull(canCommitOwner);
        ArgumentNullException.ThrowIfNull(commitOwner);

        var level = GetLevel(transaction.LevelId);
        if (transaction.RuntimeIdentity != runtimeIdentity
            || level.Version != transaction.ExpectedVersion
            || transaction.ExpectedUniqueVersion != uniqueHistoryVersion)
            throw new InvalidOperationException("The native barrel transaction was prepared from stale raw item state.");

        var operation = transaction.Operation;
        var worldState = operation.WorldState;
        if (worldState.Items.Count != PoolCapacity
            || worldState.ActiveItemIds.Count != ActiveCapacity
            || worldState.ActiveItemCount < level.ActiveItemCount
            || worldState.ActiveItemCount > Math.Min(ActiveCapacity, level.ActiveItemCount + 1)
            || !worldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds))
            throw new InvalidDataException("Native barrel result does not preserve the configured item-pool shape.");

        var loot = operation.Loot;
        var allocated = loot?.Allocated ?? false;
        if (worldState.ActiveItemCount != level.ActiveItemCount + (allocated ? 1 : 0))
            throw new InvalidDataException("Native barrel active-item count disagrees with its single allocation attempt.");

        var bindings = level.Bindings.ToArray();
        var projections = new List<AuthoritativeCathedralNativeItemProjection>(1);
        if (allocated) {
            var entry = loot!;
            var expectedSlotIndex = level.ActiveItemCount;
            if (expectedSlotIndex >= ActiveCapacity
                || entry.Slot != level.ActiveItemIds[expectedSlotIndex]
                || bindings[entry.Slot] != 0)
                throw new InvalidDataException("Native barrel allocation does not use the next inactive slot.");

            if (entry.Visible) {
                var projection = transaction.Projection;
                if (projection is null || entry.Item is null
                    || projection.Slot != entry.Slot
                    || projection.Raw != entry.Item
                    || projection.EntityId != 0)
                    throw new InvalidDataException("Visible native barrel loot has no matching prepared projection.");
                ValidatePreparedProjection(projection);
                var validationEntityId = FindValidationEntityId(bindings);
                projection = projection with { EntityId = validationEntityId };
                ValidateProjection(projection, allowSlotMismatch: false);
                bindings[entry.Slot] = validationEntityId;
            } else if (transaction.Projection is not null) {
                throw new InvalidDataException("Invisible native barrel loot unexpectedly has a live projection.");
            }
        } else if (transaction.Projection is not null) {
            throw new InvalidDataException("Native barrel operation has a projection without an allocation.");
        }

        var candidateCheckpoint = BuildCandidateCheckpoint(bindings);
        ValidateCheckpoint(candidateCheckpoint, profile, nativeFamilyContexts);
        if (!canCommitOwner())
            return null;

        if (allocated && loot!.Visible) {
            var entityId = allocateEntityId();
            if (entityId == 0 || isEntityIdOccupied(entityId) || IsBound(entityId))
                throw new InvalidDataException("Native barrel item allocation collided with an existing entity identity.");
            var projection = transaction.Projection! with { EntityId = entityId };
            ValidateProjection(projection, allowSlotMismatch: false);
            bindings[loot.Slot] = entityId;
            projections.Add(projection);
            candidateCheckpoint = BuildCandidateCheckpoint(bindings);
            ValidateCheckpoint(candidateCheckpoint, profile, nativeFamilyContexts);
        }

        commitOwner();
        ApplyBarrelLootState(level, worldState, bindings);
        return new(transaction.LevelId, operation.FinalWorldRandomState, operation, projections);

        DiabloCathedralItemLevelCheckpoint BuildCandidateLevel(IReadOnlyList<uint> candidateBindings)
            => new(
                level.LevelId,
                level.Depth,
                worldState.Items.Select(CloneItem).ToArray(),
                worldState.ActiveItemIds.ToArray(),
                worldState.ActiveItemCount,
                worldState.ItemRefs.ToArray(),
                candidateBindings.Select((entityId, slot) => new DiabloCathedralItemSlotEntityBinding(slot, entityId)).ToArray(),
                level.RandomState,
                level.GenerationIdentity);

        DiabloCathedralItemCheckpoint BuildCandidateCheckpoint(IReadOnlyList<uint> candidateBindings)
        {
            var candidateLevel = BuildCandidateLevel(candidateBindings);
            ValidateLevelShape(candidateLevel, nativeFamilyContexts);
            return new(
                profile.MetadataFingerprint,
                profile.GenerationFingerprint,
                worldState.UniqueItemFlags.OrderBy(flag => flag).ToArray(),
                levels.Values.Select(value => value.LevelId == level.LevelId ? candidateLevel : value.Checkpoint)
                    .OrderBy(value => value.LevelId)
                    .ToArray());
        }

        uint FindValidationEntityId(IReadOnlyList<uint> candidateBindings)
        {
            for (var candidate = uint.MaxValue; candidate > 0; candidate--)
                if (!IsBound(candidate) && !candidateBindings.Contains(candidate))
                    return candidate;
            throw new InvalidOperationException("No temporary native item identity is available for checkpoint preflight.");
        }

        void ValidatePreparedProjection(AuthoritativeCathedralNativeItemProjection projection)
        {
            var expected = ProjectNativeItem(
                projection.Slot,
                0,
                projection.Raw.Seed,
                PriceFor(projection.Raw),
                projection.Raw,
                GetBinding(projection.Raw.ItemIndex),
                profile.ContentInventoryDimensions);
            if (projection != expected)
                throw new InvalidDataException("Prepared native barrel projection changed canonical item state.");
        }
    }

    private void ApplyBarrelLootState(
        MutableLevel level,
        AuthoritativeCathedralThemeItemWorldState worldState,
        IReadOnlyList<uint> bindings)
    {
        if (worldState.Items.Count != PoolCapacity
            || worldState.ActiveItemIds.Count != ActiveCapacity
            || bindings.Count != PoolCapacity)
            throw new InvalidDataException("Native barrel result does not contain a complete item-pool projection.");
        for (var i = 0; i < PoolCapacity; i++) {
            level.Items[i] = CloneItem(worldState.Items[i]);
            level.Bindings[i] = bindings[i];
        }
        for (var i = 0; i < ActiveCapacity; i++)
            level.ActiveItemIds[i] = worldState.ActiveItemIds[i];
        for (var i = 0; i < CellCount; i++)
            level.SignedItemRefs[i] = worldState.ItemRefs[i];
        level.ActiveItemCount = worldState.ActiveItemCount;
        if (!uniqueItemFlags.SetEquals(worldState.UniqueItemFlags)) {
            uniqueItemFlags = worldState.UniqueItemFlags.ToHashSet();
            uniqueHistoryVersion++;
        }
        level.Version++;
    }
}
