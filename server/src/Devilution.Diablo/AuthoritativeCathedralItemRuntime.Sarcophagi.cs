namespace Devilution.Diablo;

/** Prepared native sarcophagus transaction; prepare never mutates the runtime. */
internal sealed class AuthoritativeCathedralItemSarcophagusTransaction
{
    internal AuthoritativeCathedralItemSarcophagusTransaction(
        uint levelId,
        int expectedVersion,
        int expectedUniqueVersion,
        Guid runtimeIdentity,
        AuthoritativeCathedralSarcophagusOperationResult operation,
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
    public AuthoritativeCathedralSarcophagusOperationResult Operation { get; }
    internal int ExpectedVersion { get; }
    internal int ExpectedUniqueVersion { get; }
    internal Guid RuntimeIdentity { get; }
    internal AuthoritativeCathedralNativeItemProjection? Projection { get; }
}

/** Live item projections published by one committed sarcophagus operation. */
internal sealed record AuthoritativeCathedralItemSarcophagusMutationResult(
    uint LevelId,
    uint FinalRandomState,
    AuthoritativeCathedralSarcophagusOperationResult Operation,
    IReadOnlyList<AuthoritativeCathedralNativeItemProjection> Projections);

public partial class AuthoritativeCathedralItemRuntime
{
    /** Prepare one native sarcophagus result against a versioned clone of its item pool. */
    internal AuthoritativeCathedralItemSarcophagusTransaction PrepareSarcophagusOperation(
        uint levelId,
        AuthoritativeCathedralSarcophagusOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var level = GetLevel(levelId);
        var levelProfile = GetLevelProfile(levelId);
        var facts = request.Sarcophagus;
        if (levelProfile.Depth is < 1 or > 2 || request.Policy.Depth != levelProfile.Depth
            || request.Policy.GameMode != DiabloGameMode.SinglePlayer
            || facts.LevelId != levelId)
            throw new InvalidDataException("Native sarcophagus item transactions require the owned single-player Cathedral pool.");
        if (!levelProfile.ObjectBindings.Any(binding =>
                binding.NativeObjectSlot == facts.ObjectSlot
                && binding.EntityId == facts.EntityId
                && binding.ObjectType == facts.NativeTypeId
                && binding.MutationPolicy == AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus
                && binding.PositionX == facts.PositionX
                && binding.PositionY == facts.PositionY))
            throw new InvalidDataException("Native sarcophagus facts do not match an explicitly bound object.");
        if (request.WorldState.ActiveItemCount != level.ActiveItemCount
            || !request.WorldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds)
            || !request.WorldState.Items.SequenceEqual(level.Items)
            || !request.WorldState.ItemRefs.SequenceEqual(level.SignedItemRefs)
            || !request.WorldState.UniqueItemFlags.SetEquals(uniqueItemFlags))
            throw new InvalidOperationException("Native sarcophagus operation was prepared from stale or altered item-pool state.");

        var operation = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(request);
        AuthoritativeCathedralNativeItemProjection? projection = null;
        if (operation.Loot is { Allocated: true, Visible: true, Item: { } item } entry) {
            var binding = GetBinding(item.ItemIndex);
            projection = ProjectNativeItem(
                entry.Slot,
                0,
                entry.ItemSeed,
                PriceFor(item),
                item,
                binding,
                profile.ContentInventoryDimensions);
        }
        return new(levelId, level.Version, uniqueHistoryVersion, runtimeIdentity, operation, projection);
    }

    /**
     * Commit after full checkpoint/projection validation and owner revalidation.
     * The owner callback commits the object and optional linked actor together;
     * the item pool is copied only after that synchronous callback succeeds.
     */
    internal AuthoritativeCathedralItemSarcophagusMutationResult? CommitSarcophagusOperation(
        AuthoritativeCathedralItemSarcophagusTransaction transaction,
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
            throw new InvalidOperationException("The native sarcophagus transaction was prepared from stale raw item state.");

        var operation = transaction.Operation;
        var loot = operation.Loot;
        var worldState = operation.WorldState;
        if (worldState.Items.Count != PoolCapacity
            || worldState.ActiveItemIds.Count != ActiveCapacity
            || worldState.ActiveItemCount < level.ActiveItemCount
            || worldState.ActiveItemCount > Math.Min(ActiveCapacity, level.ActiveItemCount + 1)
            || !worldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds))
            throw new InvalidDataException("Native sarcophagus result does not preserve the configured item-pool shape.");

        var allocated = loot?.Allocated ?? false;
        if (worldState.ActiveItemCount != level.ActiveItemCount + (allocated ? 1 : 0))
            throw new InvalidDataException("Native sarcophagus allocation count disagrees with its item-pool result.");
        var bindings = level.Bindings.ToArray();
        var projections = new List<AuthoritativeCathedralNativeItemProjection>(1);
        if (allocated) {
            var entry = loot!;
            var expectedSlotIndex = level.ActiveItemCount;
            if (expectedSlotIndex >= ActiveCapacity
                || entry.Slot != level.ActiveItemIds[expectedSlotIndex]
                || bindings[entry.Slot] != 0)
                throw new InvalidDataException("Native sarcophagus allocation does not use the next inactive slot.");

            if (entry.Visible) {
                var projection = transaction.Projection;
                if (projection is null || entry.Item is null
                    || projection.Slot != entry.Slot
                    || projection.Raw != entry.Item
                    || projection.EntityId != 0)
                    throw new InvalidDataException("Visible native sarcophagus loot has no matching prepared projection.");
                ValidatePreparedProjection(projection);
                var validationEntityId = FindValidationEntityId(bindings);
                projection = projection with { EntityId = validationEntityId };
                ValidateProjection(projection, allowSlotMismatch: false);
                bindings[entry.Slot] = validationEntityId;
            } else if (transaction.Projection is not null) {
                throw new InvalidDataException("Invisible native sarcophagus loot unexpectedly has a live projection.");
            }
        } else if (transaction.Projection is not null) {
            throw new InvalidDataException("A full native item pool produced a sarcophagus projection without an allocation.");
        }

        var candidateCheckpoint = BuildCandidateCheckpoint(bindings);
        ValidateCheckpoint(candidateCheckpoint, profile);

        // Refuse before consuming an entity identity. This validation is paired
        // with the owner’s same-thread commit callback, which must not fail after
        // returning true for this exact prepared operation.
        if (!canCommitOwner())
            return null;

        if (allocated && loot!.Visible) {
            var entityId = allocateEntityId();
            if (entityId == 0 || isEntityIdOccupied(entityId) || IsBound(entityId))
                throw new InvalidDataException("Native sarcophagus item allocation collided with an existing entity identity.");
            var projection = transaction.Projection! with { EntityId = entityId };
            ValidateProjection(projection, allowSlotMismatch: false);
            bindings[loot.Slot] = entityId;
            projections.Add(projection);
            candidateCheckpoint = BuildCandidateCheckpoint(bindings);
            ValidateCheckpoint(candidateCheckpoint, profile);
        }

        commitOwner();
        ApplySarcophagusLootState(level, worldState, bindings);
        return new(transaction.LevelId, operation.FinalRandomState, operation, projections);

        DiabloCathedralItemCheckpoint BuildCandidateCheckpoint(IReadOnlyList<uint> candidateBindings)
        {
            var candidateLevel = new DiabloCathedralItemLevelCheckpoint(
                level.LevelId,
                level.Depth,
                worldState.Items.Select(CloneItem).ToArray(),
                worldState.ActiveItemIds.ToArray(),
                worldState.ActiveItemCount,
                worldState.ItemRefs.ToArray(),
                candidateBindings.Select((entityId, slot) => new DiabloCathedralItemSlotEntityBinding(slot, entityId)).ToArray(),
                level.RandomState,
                level.GenerationIdentity);
            ValidateLevelShape(candidateLevel);
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
                throw new InvalidDataException("Prepared native sarcophagus projection changed canonical item state.");
        }
    }

    private void ApplySarcophagusLootState(
        MutableLevel level,
        AuthoritativeCathedralThemeItemWorldState worldState,
        IReadOnlyList<uint> bindings)
    {
        if (worldState.Items.Count != PoolCapacity
            || worldState.ActiveItemIds.Count != ActiveCapacity
            || bindings.Count != PoolCapacity)
            throw new InvalidDataException("Native sarcophagus result does not contain a complete item-pool projection.");
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
