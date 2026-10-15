using System.Collections.ObjectModel;

namespace Devilution.Diablo;

/** Stable content identity required when a native item is projected to the live model. */
public sealed record AuthoritativeCathedralItemContentBinding(
    int NativeItemIndex,
    uint StableItemId,
    string StableSymbol,
    int InventoryWidth = 1,
    int InventoryHeight = 1,
    DiabloItemSpellSourceKind? SpellSourceKind = null,
    DiabloItemActionKind? ItemActionKind = null);

/** Content-owned inventory dimensions used by the native projection boundary. */
public sealed record AuthoritativeCathedralItemInventoryDimensions(
    int InventoryWidth = 10,
    int InventoryHeight = 4,
    int BeltWidth = 8,
    int BeltHeight = 1,
    int StashWidth = 10,
    int StashHeight = 4);

/** Native object provenance retained by an opt-in populated level profile. */
public enum AuthoritativeCathedralItemObjectMutationPolicy
{
    Immutable,
    CathedralDoor,
    BarrelDestroy,
    CruxDestroy,
    ExplicitlyRefused,
    NativeChest,
    NativeSarcophagus,
    NativeBarrel,
}

/**
 * Exact native object identity used to validate item placement inputs.  The
 * item runtime never infers this information from a generic interaction flag.
 */
public sealed record AuthoritativeCathedralItemObjectBinding(
    int NativeObjectSlot,
    uint EntityId,
    int ObjectType,
    int PositionX,
    int PositionY,
    bool InitialSolid,
    int InitialSelectionRegion,
    int RawFlags = 0,
    AuthoritativeCathedralItemObjectMutationPolicy MutationPolicy = AuthoritativeCathedralItemObjectMutationPolicy.Immutable);

/** Static, immutable placement inputs for one populated Cathedral level. */
public sealed record AuthoritativeCathedralItemLevelProfile(
    uint LevelId,
    int Depth,
    IReadOnlyList<bool> SolidTiles,
    IReadOnlyList<bool> SetPieceTiles,
    IReadOnlyList<int> InitialObjectRefs,
    IReadOnlyList<AuthoritativeCathedralItemObjectBinding> ObjectBindings)
{
    /** Explicit raw-pool opt-in; the original constructor and deconstruction remain unchanged. */
    public bool PreserveSparseNativeObjectSlots { get; init; }
}

/** Dynamic actor/object tile references supplied synchronously by the live owner. */
public sealed record AuthoritativeCathedralItemOccupancyContext(
    IReadOnlyList<int> PlayerRefs,
    IReadOnlyList<int> MonsterRefs,
    IReadOnlyList<int> ObjectRefs,
    IReadOnlyList<bool> CurrentSolidTiles,
    IReadOnlyList<bool> CurrentSetPieceTiles,
    IReadOnlyList<bool> CurrentObjectSolid,
    IReadOnlyList<bool> CurrentObjectCanInteract);

/** Native CanPut inputs; actor refs are deliberately not part of this test. */
public sealed record AuthoritativeCathedralItemPlacementView(
    IReadOnlyList<bool> SolidTiles,
    IReadOnlyList<int> ObjectRefs,
    IReadOnlyList<bool> ObjectSolid,
    IReadOnlyList<bool> ObjectCanInteract,
    IReadOnlyList<int> ItemRefs);

/** A complete slot-to-live-entity binding array entry; zero means non-visible. */
public sealed record DiabloCathedralItemSlotEntityBinding(int Slot, uint EntityId);

/** Detached per-level raw item pool checkpoint. Dynamic actor views are intentionally absent. */
public sealed record DiabloCathedralItemLevelCheckpoint(
    uint LevelId,
    int Depth,
    IReadOnlyList<AuthoritativeCathedralInitialItemState> Items,
    IReadOnlyList<int> ActiveItemIds,
    int ActiveItemCount,
    IReadOnlyList<int> SignedItemRefs,
    IReadOnlyList<DiabloCathedralItemSlotEntityBinding> SlotEntityBindings,
    uint RandomState = 0,
    string? GenerationIdentity = null)
{
    public void ValidateShape()
        => AuthoritativeCathedralItemRuntime.ValidateLevelShape(this);
}

/**
 * Separately versioned populated-item extension payload. Unique history is
 * global to the run, not reset when a level is entered or restored.
 */
public sealed record DiabloCathedralItemCheckpoint(
    string MetadataFingerprint,
    string GenerationFingerprint,
    IReadOnlyList<int> UniqueItemFlags,
    IReadOnlyList<DiabloCathedralItemLevelCheckpoint> Levels)
{
    public void ValidateShape()
        => AuthoritativeCathedralItemRuntime.ValidateCheckpointShape(this);
}

/**
 * Explicit opt-in content contract for the live raw item owner. The profile
 * carries the initial checkpoint, raw catalogs, generation fences, stable item
 * bindings, and immutable level/object placement provenance.
 */
public sealed record AuthoritativeCathedralItemRuntimeProfile(
    string MetadataFingerprint,
    string GenerationFingerprint,
    DiabloCathedralItemCheckpoint InitialCheckpoint,
    AuthoritativeCathedralThemeItemCatalog ItemCatalog,
    AuthoritativeCathedralThemeItemPolicy ItemPolicy,
    AuthoritativeCathedralMonsterRuntimeCatalog MonsterCatalog,
    IReadOnlyList<AuthoritativeCathedralUniqueDefinition> UniqueDefinitions,
    IReadOnlyDictionary<int, AuthoritativeCathedralItemContentBinding> ItemBindings,
    AuthoritativeCathedralItemInventoryDimensions ContentInventoryDimensions,
    IReadOnlyList<AuthoritativeCathedralItemLevelProfile> LevelProfiles,
    IReadOnlyDictionary<int, int>? NativeSpellBindings = null);

/** External cursor metadata used when a drop resets native ground animation. */
public sealed record AuthoritativeCathedralItemGroundAnimation(
    int CursorGraphic,
    int FrameCount,
    int TicksPerFrame = 1);

/**
 * Raw item plus its direct live projection. Raw fields remain available for
 * checkpointing and presentation; State is built directly and never resolved
 * through StoreCatalog or authored fallback metadata.
 */
public sealed record AuthoritativeCathedralNativeItemProjection(
    int Slot,
    uint EntityId,
    uint ItemSeed,
    uint Price,
    AuthoritativeCathedralInitialItemState Raw,
    AuthoritativeItemState State,
    AuthoritativeCathedralItemContentBinding Binding);

/** Inputs at the live MonsterDeath -> SpawnLoot boundary. */
public sealed record AuthoritativeCathedralItemDeathRequest(
    uint LevelId,
    int MonsterId,
    uint MonsterItemSeed,
    int PositionX,
    int PositionY,
    AuthoritativeCathedralItemOccupancyContext Occupancy,
    bool IsUnique = false,
    bool IsPlayerMinion = false,
    AuthoritativeCathedralUniqueDefinition? Unique = null,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null,
    string? GenerationIdentity = null);

/** Prepared mutation; constructing it never changes the live runtime. */
public sealed class AuthoritativeCathedralItemDeathTransaction
{
    internal AuthoritativeCathedralItemDeathTransaction(
        uint levelId,
        int expectedVersion,
        AuthoritativeCathedralMonsterLootResult loot,
        AuthoritativeCathedralNativeItemProjection? projection,
        uint entityId,
        Guid runtimeIdentity,
        int expectedUniqueVersion)
    {
        LevelId = levelId;
        ExpectedVersion = expectedVersion;
        Loot = loot;
        Projection = projection;
        EntityId = entityId;
        RuntimeIdentity = runtimeIdentity;
        ExpectedUniqueVersion = expectedUniqueVersion;
    }

    internal int ExpectedVersion { get; }
    internal Guid RuntimeIdentity { get; }
    internal int ExpectedUniqueVersion { get; }
    internal int SelectedSlot => Loot.SelectedSlot;

    public uint LevelId { get; }
    public AuthoritativeCathedralMonsterLootResult Loot { get; }
    public AuthoritativeCathedralNativeItemProjection? Projection { get; }
    public uint EntityId { get; }
    public uint FinalRandomState => Loot.FinalRandomState;
    public AuthoritativeWorldItem? WorldItem
        => Projection is null || EntityId == 0
            ? null
            : new AuthoritativeWorldItem(
                EntityId, LevelId, Projection.Raw.PositionX, Projection.Raw.PositionY,
                Projection.ItemSeed, Projection.Price, Projection.State);
    public bool RequiresWorldItem => Projection is not null && Loot.PlacementSucceeded;

    internal AuthoritativeCathedralItemDeathTransaction WithEntity(
        uint entityId,
        AuthoritativeCathedralNativeItemProjection projection)
        => new(LevelId, ExpectedVersion, Loot, projection, entityId,
            RuntimeIdentity, ExpectedUniqueVersion);
}

/** Prepared native chest mutation; it never changes the live item pool. */
internal sealed class AuthoritativeCathedralItemChestTransaction
{
    internal AuthoritativeCathedralItemChestTransaction(
        uint levelId,
        int expectedVersion,
        AuthoritativeCathedralChestLootResult loot,
        IReadOnlyList<AuthoritativeCathedralNativeItemProjection?> projections,
        Guid runtimeIdentity,
        int expectedUniqueVersion)
    {
        LevelId = levelId;
        ExpectedVersion = expectedVersion;
        Loot = loot;
        Projections = projections.ToArray();
        RuntimeIdentity = runtimeIdentity;
        ExpectedUniqueVersion = expectedUniqueVersion;
    }

    public uint LevelId { get; }
    public AuthoritativeCathedralChestLootResult Loot { get; }
    internal int ExpectedVersion { get; }
    internal IReadOnlyList<AuthoritativeCathedralNativeItemProjection?> Projections { get; }
    internal Guid RuntimeIdentity { get; }
    internal int ExpectedUniqueVersion { get; }
}

/** Visible item results from one committed native chest operation. */
internal sealed record AuthoritativeCathedralItemChestMutationResult(
    uint LevelId,
    uint FinalRandomState,
    IReadOnlyList<AuthoritativeCathedralNativeItemProjection> Projections);

/** Result of a committed live item mutation. */
public sealed record AuthoritativeCathedralItemMutationResult(
    uint LevelId,
    int Slot,
    uint EntityId,
    bool HasWorldItem,
    AuthoritativeCathedralNativeItemProjection? Projection,
    AuthoritativeCathedralMonsterLootOutcome? LootOutcome = null);

/**
 * Canonical mutable owner for the native Cathedral item pools. It owns only
 * item slots, active-list order/count, signed item refs, unique history, and
 * slot/entity bindings. Player, monster, object, door, and placement views are
 * supplied as synchronous call inputs by the simulation owner.
 */
public partial class AuthoritativeCathedralItemRuntime
{
    public const int Width = AuthoritativeCathedralThemeItemWorldState.Width;
    public const int Height = AuthoritativeCathedralThemeItemWorldState.Height;
    public const int CellCount = AuthoritativeCathedralThemeItemWorldState.CellCount;
    public const int PoolCapacity = AuthoritativeCathedralThemeItemWorldState.PoolCapacity;
    public const int ActiveCapacity = AuthoritativeCathedralThemeItemWorldState.ActiveCapacity;

    private readonly AuthoritativeCathedralItemRuntimeProfile profile;
    private readonly Dictionary<uint, MutableLevel> levels;
    private readonly Guid runtimeIdentity = Guid.NewGuid();
    private readonly AuthoritativeNativeGoldStackMetadata? nativeGoldStackMetadata;
    private HashSet<int> uniqueItemFlags;
    private int uniqueHistoryVersion;

    public AuthoritativeCathedralItemRuntime(AuthoritativeCathedralItemRuntimeProfile profile)
        : this(profile, profile?.InitialCheckpoint, null)
    {
    }

    public AuthoritativeCathedralItemRuntime(
        AuthoritativeCathedralItemRuntimeProfile profile,
        DiabloCathedralItemCheckpoint? checkpoint)
        : this(profile, checkpoint, null)
    {
    }

    internal AuthoritativeCathedralItemRuntime(
        AuthoritativeCathedralItemRuntimeProfile profile,
        DiabloCathedralItemCheckpoint? checkpoint,
        AuthoritativeNativeGoldStackMetadata? nativeGoldStackMetadata)
    {
        this.profile = CloneProfile(profile ?? throw new ArgumentNullException(nameof(profile)));
        ValidateProfile(this.profile);
        if (nativeGoldStackMetadata is not null
            && (!this.profile.ItemBindings.TryGetValue(nativeGoldStackMetadata.NativeItemIndex, out var goldBinding)
                || goldBinding.StableItemId != nativeGoldStackMetadata.StableItemId
                || goldBinding.InventoryWidth != nativeGoldStackMetadata.InventoryWidth
                || goldBinding.InventoryHeight != nativeGoldStackMetadata.InventoryHeight))
            throw new InvalidDataException("Native Gold cursor metadata does not match the runtime item binding.");
        this.nativeGoldStackMetadata = nativeGoldStackMetadata;
        var source = checkpoint ?? this.profile.InitialCheckpoint;
        ValidateCheckpoint(source, this.profile);
        uniqueItemFlags = source.UniqueItemFlags.ToHashSet();
        levels = source.Levels.ToDictionary(
            level => level.LevelId,
            level => new MutableLevel(CloneLevel(level), GetLevelProfile(this.profile, level.LevelId)));
    }

    public AuthoritativeCathedralItemRuntimeProfile Profile => profile;
    public IReadOnlySet<int> UniqueItemFlags => new ReadOnlySet<int>(uniqueItemFlags);
    public bool HasLevel(uint levelId) => levels.ContainsKey(levelId);
    public bool HasFreeSlot(uint levelId) => GetLevel(levelId).ActiveItemCount < ActiveCapacity;
    public bool IsBound(uint entityId)
        => entityId != 0 && levels.Values.Any(level => level.Bindings.Contains(entityId));

    public static bool CanPutItem(
        int positionX,
        int positionY,
        AuthoritativeCathedralItemPlacementView view,
        int replacingSlot = -1)
    {
        ArgumentNullException.ThrowIfNull(view);
        ValidateCells(view.SolidTiles, nameof(view.SolidTiles));
        ValidateCells(view.ObjectRefs, nameof(view.ObjectRefs));
        ValidateCells(view.ItemRefs, nameof(view.ItemRefs));
        ArgumentNullException.ThrowIfNull(view.ObjectSolid);
        ArgumentNullException.ThrowIfNull(view.ObjectCanInteract);
        if (view.ObjectSolid.Count != view.ObjectCanInteract.Count)
            throw new InvalidDataException("Native placement object flags must have equal lengths.");
        if (view.ObjectRefs.Any(value => value is < -127 or > sbyte.MaxValue)
            || view.ObjectSolid.Count > ActiveCapacity)
            throw new InvalidDataException("Native placement object references have an unsafe shape.");
        if ((uint)positionX >= Width || (uint)positionY >= Height)
            return false;
        var index = checked(positionY * Width + positionX);
        if (view.SolidTiles[index])
            return false;
        int? ObjectAt(int x, int y, bool large) {
            if ((uint)x >= Width || (uint)y >= Height)
                return null;
            var reference = view.ObjectRefs[y * Width + x];
            return reference > 0 || large && reference != 0
                ? Math.Abs(reference) - 1
                : null;
        }
        if (ObjectAt(positionX, positionY, true) is { } direct) {
            if (direct < 0 || direct >= view.ObjectSolid.Count)
                throw new InvalidDataException("Native placement object reference is outside object metadata.");
            if (view.ObjectSolid[direct])
                return false;
        }
        if (ObjectAt(positionX + 1, positionY + 1, true) is { } south) {
            if (south < 0 || south >= view.ObjectCanInteract.Count)
                throw new InvalidDataException("Native placement object reference is outside object metadata.");
            if (view.ObjectCanInteract[south])
                return false;
        }
        var east = ObjectAt(positionX + 1, positionY, false);
        var west = ObjectAt(positionX, positionY + 1, false);
        if (east is { } eastSlot && west is { } westSlot) {
            if (eastSlot < 0 || eastSlot >= view.ObjectCanInteract.Count
                || westSlot < 0 || westSlot >= view.ObjectCanInteract.Count)
                throw new InvalidDataException("Native placement object reference is outside object metadata.");
            if (view.ObjectCanInteract[eastSlot] && view.ObjectCanInteract[westSlot])
                return false;
        }
        var itemRef = view.ItemRefs[index];
        return itemRef == 0 || itemRef == replacingSlot + 1;
    }

    public AuthoritativeCathedralNativeItemProjection? GetProjection(
        uint levelId,
        uint entityId)
    {
        if (entityId == 0)
            return null;
        var level = GetLevel(levelId);
        var slot = FindBoundSlot(level, entityId);
        if (slot < 0)
            return null;
        var raw = level.Items[slot];
        return ProjectNativeItem(
            slot,
            entityId,
            raw.Seed,
            PriceFor(raw),
            raw,
            GetBinding(raw.ItemIndex),
            profile.ContentInventoryDimensions);
    }

    public AuthoritativeCathedralNativeItemProjection ProjectWorldItem(
        AuthoritativeWorldItem worldItem,
        bool resetGroundAnimation = false)
    {
        ArgumentNullException.ThrowIfNull(worldItem);
        var projection = GetProjection(worldItem.LevelId, worldItem.EntityId);
        AuthoritativeCathedralInitialItemState raw;
        AuthoritativeCathedralItemContentBinding binding;
        if (projection is not null) {
            if (worldItem.State.StableItemId != 0
                && worldItem.State.StableItemId != projection.Binding.StableItemId)
                throw new InvalidDataException("World item stable identity does not match its native binding.");
            raw = ApplyOwnedState(projection.Raw, projection.Binding, worldItem.State,
                worldItem.PositionX, worldItem.PositionY);
            binding = projection.Binding;
        } else {
            binding = GetBinding(worldItem.State.ItemIndex);
            raw = RehydrateRaw(worldItem);
        }
        if (!resetGroundAnimation)
            return ProjectNativeItem(
                projection?.Slot ?? 0,
                worldItem.EntityId,
                worldItem.ItemSeed,
                PriceFor(raw),
                raw,
                binding,
                profile.ContentInventoryDimensions);
        if (!profile.ItemCatalog.CursorAnimations.TryGetValue(
                raw.CursorGraphic, out var animation))
            throw new InvalidDataException("Native ground animation metadata is unavailable for this item cursor.");
        return ProjectNativeItem(
            projection?.Slot ?? 0,
            worldItem.EntityId,
            worldItem.ItemSeed,
            PriceFor(raw),
            ResetGroundAnimation(raw, new AuthoritativeCathedralItemGroundAnimation(
                raw.CursorGraphic, animation.FrameCount, animation.TicksPerFrame)),
            binding,
            profile.ContentInventoryDimensions);
    }

    public DiabloCathedralItemCheckpoint CaptureCheckpoint()
    {
        var capturedLevels = levels.Values
            .OrderBy(level => level.LevelId)
            .Select(level => CloneLevel(level.Checkpoint))
            .ToArray();
        return new DiabloCathedralItemCheckpoint(
            profile.MetadataFingerprint,
            profile.GenerationFingerprint,
            uniqueItemFlags.OrderBy(value => value).ToArray(),
            capturedLevels);
    }

    public DiabloCathedralItemLevelCheckpoint CaptureLevel(uint levelId)
        => CloneLevel(GetLevel(levelId).Checkpoint);

    /** Advances the ordinary ground-item animation clock for one active level. */
    internal void AdvanceGroundAnimations(uint levelId)
    {
        var level = GetLevel(levelId);
        var changes = new List<(int Slot, AuthoritativeCathedralInitialItemState Item)>();
        for (var index = 0; index < level.ActiveItemCount; index++) {
            var slot = level.ActiveItemIds[index];
            var item = level.Items[slot];
            if (!item.AnimFlag)
                continue;
            if (!profile.ItemCatalog.CursorAnimations.TryGetValue(item.CursorGraphic, out var cursorAnimation)
                || item.Animation.NumberOfFrames != cursorAnimation.FrameCount
                || item.Animation.TicksPerFrame != cursorAnimation.TicksPerFrame)
                throw new InvalidDataException("An active native item animation disagrees with cursor metadata.");
            if (item.CursorGraphic == MagicRockCursorGraphic
                && cursorAnimation.FrameCount <= MagicRockLastMiddleFrame)
                throw new InvalidDataException("Native Magic Rock animation metadata omits its elevated selection frames.");
            var next = AdvanceGroundAnimation(item);
            if (next != item)
                changes.Add((slot, next));
        }

        foreach (var change in changes)
            level.Items[change.Slot] = change.Item;
        if (changes.Count != 0)
            level.Version++;
    }

    private static AuthoritativeCathedralInitialItemState AdvanceGroundAnimation(
        AuthoritativeCathedralInitialItemState item)
    {
        var animation = item.Animation;
        if (animation.TicksPerFrame <= 0
            || animation.NumberOfFrames <= 0
            || animation.CurrentFrame < 0
            || animation.CurrentFrame >= animation.NumberOfFrames
            || animation.TickCounterOfCurrentFrame < 0
            || animation.TickCounterOfCurrentFrame >= animation.TicksPerFrame)
            throw new InvalidDataException("An active native item animation has invalid cursor state.");

        var tickCounter = animation.TickCounterOfCurrentFrame + 1;
        var frame = animation.CurrentFrame;
        if (tickCounter >= animation.TicksPerFrame) {
            tickCounter = 0;
            frame++;
            if (frame >= animation.NumberOfFrames)
                frame = 0;
        }

        var animFlag = item.AnimFlag;
        var selectionRegion = item.SelectionRegion;
        if (item.CursorGraphic == MagicRockCursorGraphic) {
            if (selectionRegion == SelectionBottom && frame == MagicRockFloorFrameCount)
                frame = 0;
            if (selectionRegion == SelectionMiddle && frame == MagicRockLastMiddleFrame)
                frame = MagicRockFirstMiddleFrame;
        } else if (frame == animation.NumberOfFrames - 1) {
            frame = animation.NumberOfFrames - 1;
            animFlag = false;
            selectionRegion = SelectionBottom;
        }

        return item with {
            Animation = animation with {
                TickCounterOfCurrentFrame = tickCounter,
                CurrentFrame = frame,
            },
            AnimFlag = animFlag,
            SelectionRegion = selectionRegion,
        };
    }

    private const int MagicRockCursorGraphic = 76;
    private const int MagicRockFloorFrameCount = 10;
    private const int MagicRockFirstMiddleFrame = 10;
    private const int MagicRockLastMiddleFrame = 19;
    private const int SelectionBottom = 1;
    private const int SelectionMiddle = 2;

    /** Builds a detached native world view from current items and live occupancy inputs. */
    public AuthoritativeCathedralThemeItemWorldState BuildWorldState(
        uint levelId,
        AuthoritativeCathedralItemOccupancyContext occupancy)
    {
        ArgumentNullException.ThrowIfNull(occupancy);
        var level = GetLevel(levelId);
        var levelProfile = GetLevelProfile(levelId);
        return new AuthoritativeCathedralThemeItemWorldState(
            level.Items,
            level.ActiveItemIds,
            level.ActiveItemCount,
            level.SignedItemRefs,
            occupancy.PlayerRefs,
            occupancy.MonsterRefs,
            occupancy.ObjectRefs,
            occupancy.CurrentSolidTiles,
            occupancy.CurrentSetPieceTiles,
            occupancy.CurrentObjectSolid,
            occupancy.CurrentObjectCanInteract,
            uniqueItemFlags);
    }

    /** Prepare native death loot against a versioned clone; no live mutation occurs. */
    public AuthoritativeCathedralItemDeathTransaction PrepareDeath(
        AuthoritativeCathedralItemDeathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var level = GetLevel(request.LevelId);
        var levelProfile = GetLevelProfile(request.LevelId);
        if (request.GenerationIdentity is not null
            && !string.Equals(request.GenerationIdentity, level.Checkpoint.GenerationIdentity,
                StringComparison.Ordinal))
            throw new InvalidDataException("Native Cathedral item death generation identity does not match the level checkpoint.");
        if (request.PositionX is < 0 or >= Width || request.PositionY is < 0 or >= Height)
            throw new ArgumentOutOfRangeException(nameof(request.PositionX));

        var worldState = BuildWorldState(request.LevelId, request.Occupancy);
        var loot = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            new AuthoritativeCathedralMonsterLootRequest(
                worldState,
                profile.ItemCatalog,
                profile.ItemPolicy with {
                    Depth = levelProfile.Depth,
                    CurrentDungeonLevel = levelProfile.Depth,
                },
                profile.MonsterCatalog,
                request.MonsterId,
                request.MonsterItemSeed,
                request.PositionX,
                request.PositionY,
                request.IsUnique,
                request.IsPlayerMinion,
                request.Unique,
                request.Player));

        AuthoritativeCathedralNativeItemProjection? projection = null;
        if (loot.Dropped && loot.Item is not null && loot.SelectedSlot >= 0 && loot.PlacementSucceeded) {
            var binding = GetBinding(loot.Item.ItemIndex);
            projection = ProjectNativeItem(
                loot.SelectedSlot,
                0,
                loot.Item.Seed,
                PriceFor(loot.Item),
                loot.Item,
                binding,
                profile.ContentInventoryDimensions);
        }
        return new AuthoritativeCathedralItemDeathTransaction(
            request.LevelId, level.Version, loot, projection, 0,
            runtimeIdentity, uniqueHistoryVersion);
    }

    public AuthoritativeCathedralItemDeathTransaction PrepareLoot(
        AuthoritativeCathedralItemDeathRequest request,
        uint entityId = 0)
    {
        var prepared = PrepareDeath(request);
        return prepared.Projection is null || entityId == 0
            ? prepared
            : prepared.WithEntity(entityId, prepared.Projection with { EntityId = entityId });
    }

    /** Prepare the native chest's ordered item attempts against a versioned pool clone. */
    internal AuthoritativeCathedralItemChestTransaction PrepareChestLoot(
        uint levelId,
        AuthoritativeCathedralChestLootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var level = GetLevel(levelId);
        var levelProfile = GetLevelProfile(levelId);
        if (levelProfile.Depth != 1 || request.Policy.Depth != 1
            || request.Policy.GameMode != DiabloGameMode.SinglePlayer)
            throw new InvalidDataException("Native chest item transactions are limited to the owned single-player L1 pool.");
        if (!levelProfile.ObjectBindings.Any(binding =>
                binding.NativeObjectSlot == request.ChestObjectSlot
                && binding.MutationPolicy == AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                && binding.PositionX == request.PositionX
                && binding.PositionY == request.PositionY))
            throw new InvalidDataException("Native chest request does not match its explicit object binding.");
        if (request.WorldState.ActiveItemCount != level.ActiveItemCount
            || !request.WorldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds)
            || !request.WorldState.Items.SequenceEqual(level.Items)
            || !request.WorldState.ItemRefs.SequenceEqual(level.SignedItemRefs)
            || !request.WorldState.UniqueItemFlags.SetEquals(uniqueItemFlags))
            throw new InvalidOperationException("Native chest loot was prepared from stale or altered item-pool state.");

        var loot = AuthoritativeCathedralChestLootGenerator.Generate(request);
        var projections = new AuthoritativeCathedralNativeItemProjection?[loot.Entries.Count];
        for (var index = 0; index < loot.Entries.Count; index++) {
            var entry = loot.Entries[index];
            if (!entry.Allocated || !entry.Visible || entry.Item is null)
                continue;
            var binding = GetBinding(entry.Item.ItemIndex);
            projections[index] = ProjectNativeItem(
                entry.Slot,
                0,
                entry.Item.Seed,
                PriceFor(entry.Item),
                entry.Item,
                binding,
                profile.ContentInventoryDimensions);
        }
        return new(levelId, level.Version, loot, projections, runtimeIdentity, uniqueHistoryVersion);
    }

    /**
     * Commit a prepared chest operation only after every projection and entity
     * identity is validated, then perform the single object-open transition.
     */
    internal AuthoritativeCathedralItemChestMutationResult? CommitChestLoot(
        AuthoritativeCathedralItemChestTransaction transaction,
        Func<uint> allocateEntityId,
        Func<uint, bool> isEntityIdOccupied,
        Func<bool> canCommitChestOpen,
        Action commitChestOpen)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(allocateEntityId);
        ArgumentNullException.ThrowIfNull(isEntityIdOccupied);
        ArgumentNullException.ThrowIfNull(canCommitChestOpen);
        ArgumentNullException.ThrowIfNull(commitChestOpen);
        var level = GetLevel(transaction.LevelId);
        if (transaction.RuntimeIdentity != runtimeIdentity
            || level.Version != transaction.ExpectedVersion
            || transaction.ExpectedUniqueVersion != uniqueHistoryVersion)
            throw new InvalidOperationException("The Cathedral chest transaction was prepared from stale raw item state.");

        var loot = transaction.Loot;
        if (loot.Entries.Count != transaction.Projections.Count
            || loot.WorldState.Items.Count != PoolCapacity
            || loot.WorldState.ActiveItemIds.Count != ActiveCapacity
            || loot.WorldState.ActiveItemCount < level.ActiveItemCount
            || loot.WorldState.ActiveItemCount > ActiveCapacity
            || !loot.WorldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds))
            throw new InvalidDataException("Native chest result does not preserve the configured item-pool shape.");

        // Refuse before consuming allocator identities. The commit callback is
        // an action because this synchronous command path has already validated
        // the same chest and cannot legitimately fail between these steps.
        if (!canCommitChestOpen())
            return null;

        var bindings = level.Bindings.ToArray();
        var projections = new List<AuthoritativeCathedralNativeItemProjection>();
        var allocatedCount = 0;
        var entityIds = new HashSet<uint>();
        for (var index = 0; index < loot.Entries.Count; index++) {
            var entry = loot.Entries[index];
            var projection = transaction.Projections[index];
            if (!entry.Allocated) {
                if (projection is not null)
                    throw new InvalidDataException("A full native chest pool produced an item projection without an allocation.");
                continue;
            }
            var expectedSlotIndex = level.ActiveItemCount + allocatedCount;
            if (expectedSlotIndex >= ActiveCapacity
                || entry.Slot != level.ActiveItemIds[expectedSlotIndex]
                || bindings[entry.Slot] != 0)
                throw new InvalidDataException("Native chest allocation does not use the next inactive slot.");
            allocatedCount++;

            if (!entry.Visible) {
                if (projection is not null)
                    throw new InvalidDataException("An invisible native chest item unexpectedly has a live projection.");
                continue;
            }
            if (projection is null || entry.Item is null
                || projection.Slot != entry.Slot
                || projection.Raw != entry.Item
                || projection.EntityId != 0)
                throw new InvalidDataException("A visible native chest item has no matching prepared projection.");

            var entityId = allocateEntityId();
            if (entityId == 0 || !entityIds.Add(entityId)
                || isEntityIdOccupied(entityId) || IsBound(entityId))
                throw new InvalidDataException("Native chest item allocation collided with an existing entity identity.");
            projection = projection with { EntityId = entityId };
            ValidateProjection(projection, allowSlotMismatch: false);
            bindings[entry.Slot] = entityId;
            projections.Add(projection);
        }
        if (loot.WorldState.ActiveItemCount != level.ActiveItemCount + allocatedCount)
            throw new InvalidDataException("Native chest active-item count disagrees with its allocation attempts.");

        var candidateLevel = new DiabloCathedralItemLevelCheckpoint(
            level.LevelId,
            level.Depth,
            loot.WorldState.Items.Select(CloneItem).ToArray(),
            loot.WorldState.ActiveItemIds.ToArray(),
            loot.WorldState.ActiveItemCount,
            loot.WorldState.ItemRefs.ToArray(),
            bindings.Select((entityId, slot) => new DiabloCathedralItemSlotEntityBinding(slot, entityId)).ToArray(),
            level.RandomState,
            level.GenerationIdentity);
        ValidateLevelShape(candidateLevel);
        var candidateCheckpoint = new DiabloCathedralItemCheckpoint(
            profile.MetadataFingerprint,
            profile.GenerationFingerprint,
            loot.WorldState.UniqueItemFlags.OrderBy(flag => flag).ToArray(),
            levels.Values.Select(value => value.LevelId == level.LevelId ? candidateLevel : value.Checkpoint)
                .OrderBy(value => value.LevelId)
                .ToArray());
        ValidateCheckpoint(candidateCheckpoint, profile);

        commitChestOpen();

        ApplyChestLootState(level, loot, bindings);
        return new(transaction.LevelId, loot.FinalRandomState, projections);
    }

    /**
     * Commits a prepared death atomically. Entity allocation is delayed until
     * commit so a discarded/stale transaction cannot expose a world item.
     */
    public AuthoritativeCathedralItemMutationResult CommitDeath(
        AuthoritativeCathedralItemDeathTransaction transaction,
        Func<uint>? allocateEntityId = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var level = GetLevel(transaction.LevelId);
        if (transaction.RuntimeIdentity != runtimeIdentity
            || level.Version != transaction.ExpectedVersion
            || transaction.ExpectedUniqueVersion != uniqueHistoryVersion)
            throw new InvalidOperationException("The Cathedral item death transaction was prepared from stale raw item state.");

        var loot = transaction.Loot;
        if (!loot.Dropped || loot.Item is null || loot.SelectedSlot < 0)
            return new(transaction.LevelId, -1, 0, false, null, loot.Outcome);

        var entityId = 0u;
        AuthoritativeCathedralNativeItemProjection? projection = transaction.Projection;
        if (loot.PlacementSucceeded) {
            if (projection is null)
                throw new InvalidDataException("A visible native item has no representable projection.");
            if (allocateEntityId is null && transaction.EntityId == 0)
                throw new InvalidOperationException("A visible native item requires an authoritative entity allocator.");
            entityId = transaction.EntityId != 0 ? transaction.EntityId : allocateEntityId!();
            if (entityId == 0)
                throw new InvalidDataException("The authoritative item entity allocator returned zero.");
            if (IsBound(entityId))
                throw new InvalidDataException("The authoritative item entity allocator returned an existing native item entity.");
            projection = projection with { EntityId = entityId };
        }

        ApplyLootState(level, loot, projection?.EntityId ?? 0);
        return new(transaction.LevelId, loot.SelectedSlot, entityId,
            projection is not null && loot.PlacementSucceeded, projection, loot.Outcome);
    }

    public AuthoritativeCathedralItemMutationResult Commit(
        AuthoritativeCathedralItemDeathTransaction transaction)
        => CommitDeath(transaction);

    /** Native DeleteItem semantics: clear the matching tile, swap active tail, preserve raw slots. */
    public bool RemoveWorldItem(uint levelId, uint entityId, out int slot)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        var level = GetLevel(levelId);
        slot = FindBoundSlot(level, entityId);
        if (slot < 0)
            return false;
        DeleteItem(level, slot);
        level.Version++;
        return true;
    }

    public bool Remove(AuthoritativeWorldItem worldItem)
    {
        ArgumentNullException.ThrowIfNull(worldItem);
        return RemoveWorldItem(worldItem.LevelId, worldItem.EntityId, out _);
    }

    public bool Remove(AuthoritativeWorldItem worldItem, AuthoritativeItemState? updatedOwnedState)
        => RemoveWorldItem(worldItem, updatedOwnedState);

    /**
     * Pickup overload preserving native inactive-slot bookkeeping.  CF_PREGEN
     * is cleared before DeleteItem; the caller supplies the already-authoritative
     * stat-cache result instead of asking this pool to infer player attributes.
     */
    public bool RemoveWorldItem(
        AuthoritativeWorldItem worldItem,
        AuthoritativeItemState? updatedOwnedState)
    {
        ArgumentNullException.ThrowIfNull(worldItem);
        var level = GetLevel(worldItem.LevelId);
        var slot = FindBoundSlot(level, worldItem.EntityId);
        if (slot < 0)
            return false;
        if (updatedOwnedState is not null) {
            var raw = level.Items[slot];
            if (raw.ItemType == 11 && (!updatedOwnedState.NativeCursorGraphic.HasValue
                || updatedOwnedState.Value < 0 || updatedOwnedState.Value > raw.Value
                || updatedOwnedState.NativeCursorGraphic is < 0 or > byte.MaxValue))
                throw new InvalidDataException("Native Gold pickup requires its remaining value and cursor.");
            level.Items[slot] = raw with {
                CreateInfo = (ushort)(raw.CreateInfo & 0x7FFF),
                MinimumMagic = updatedOwnedState.MinimumMagic,
                StatFlag = updatedOwnedState.StatFlag,
                Value = raw.ItemType == 11 ? updatedOwnedState.Value : raw.Value,
                CursorGraphic = raw.ItemType == 11 ? updatedOwnedState.NativeCursorGraphic!.Value : raw.CursorGraphic,
            };
        }
        DeleteItem(level, slot);
        level.Version++;
        return true;
    }

    /** Adds a raw item through the one live world-item mutation boundary. */
    public bool TryAddWorldItem(
        uint levelId,
        AuthoritativeCathedralNativeItemProjection projection,
        out int slot)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var level = GetLevel(levelId);
        if (level.ActiveItemCount >= ActiveCapacity || projection.EntityId == 0 || IsBound(projection.EntityId))
            return Fail(out slot);
        ValidateProjection(projection, allowSlotMismatch: true);
        slot = level.ActiveItemIds[level.ActiveItemCount];
        if (!TrySetItemReference(level, projection.Raw.PositionX, projection.Raw.PositionY, slot))
            return Fail(out slot);
        level.Items[slot] = CloneItem(projection.Raw);
        level.ActiveItemCount++;
        level.Bindings[slot] = projection.EntityId;
        level.Version++;
        return true;
    }

    public bool Add(
        AuthoritativeWorldItem worldItem,
        AuthoritativeCathedralNativeItemProjection projection,
        out int slot)
    {
        ArgumentNullException.ThrowIfNull(worldItem);
        ArgumentNullException.ThrowIfNull(projection);
        if (worldItem.EntityId != projection.EntityId
            || worldItem.PositionX != projection.Raw.PositionX
            || worldItem.PositionY != projection.Raw.PositionY
            || worldItem.ItemSeed != projection.ItemSeed
            || worldItem.Price != projection.Price)
            throw new InvalidDataException("World item and native projection do not describe the same item.");
        return TryAddWorldItem(worldItem.LevelId, projection, out slot);
    }

    /** Replaces the raw payload of an already-bound item without metadata fallback. */
    public bool TryReplaceWorldItem(
        uint levelId,
        uint entityId,
        AuthoritativeCathedralNativeItemProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        var level = GetLevel(levelId);
        var slot = FindBoundSlot(level, entityId);
        if (slot < 0)
            return false;
        if (projection.EntityId != entityId)
            throw new InvalidDataException("Native replacement projection is bound to a different entity.");
        ValidateProjection(projection, allowSlotMismatch: true);
        var old = level.Items[slot];
        if (old.PositionX != projection.Raw.PositionX || old.PositionY != projection.Raw.PositionY) {
            if (!CanUseDestination(level, projection.Raw.PositionX, projection.Raw.PositionY, slot))
                return false;
            ClearItemReference(level, old.PositionX, old.PositionY, slot);
            _ = TrySetItemReference(level, projection.Raw.PositionX, projection.Raw.PositionY, slot);
        }
        level.Items[slot] = CloneItem(projection.Raw);
        level.Bindings[slot] = entityId;
        level.Version++;
        return true;
    }

    /** Applies external cursor animation metadata without inventing raw values. */
    public AuthoritativeCathedralInitialItemState ResetGroundAnimation(
        AuthoritativeCathedralInitialItemState item,
        AuthoritativeCathedralItemGroundAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(animation);
        if (animation.CursorGraphic is < 0 or > byte.MaxValue
            || animation.FrameCount is < 1 or > sbyte.MaxValue
            || animation.TicksPerFrame is < 1 or > sbyte.MaxValue)
            throw new InvalidDataException("Native ground animation metadata is outside the item field widths.");
        return item with {
            CursorGraphic = animation.CursorGraphic,
            AnimFlag = true,
            Animation = new AuthoritativeCathedralInitialItemAnimation {
                TicksPerFrame = animation.TicksPerFrame,
                TickCounterOfCurrentFrame = 0,
                NumberOfFrames = animation.FrameCount,
                CurrentFrame = 0,
                IsPetrified = false,
            },
        };
    }

    /** Direct raw-to-live projection; every gameplay scalar is copied explicitly. */
    public AuthoritativeCathedralNativeItemProjection ProjectNativeItem(
        int slot,
        uint entityId,
        uint itemSeed,
        uint price,
        AuthoritativeCathedralInitialItemState raw,
        AuthoritativeCathedralItemContentBinding binding,
        AuthoritativeCathedralItemInventoryDimensions dimensions,
        AuthoritativeCathedralItemGroundAnimation? groundAnimation = null)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(dimensions);
        if (groundAnimation is not null)
            raw = ResetGroundAnimation(raw, groundAnimation);
        ValidateBinding(binding);
        ValidateDimensions(dimensions);
        if (slot is < 0 or >= PoolCapacity || raw.ItemIndex != binding.NativeItemIndex)
            throw new InvalidDataException("Native item projection has an unsafe slot or content identity.");
        AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(raw);
        var stableSpellId = raw.SpellId;
        if (raw.SpellId >= 0) {
            if (profile.NativeSpellBindings is null
                || !profile.NativeSpellBindings.TryGetValue(raw.SpellId, out stableSpellId))
                throw new InvalidDataException(
                    $"Native spell ordinal {raw.SpellId} has no explicit stable content binding.");
        }
        var state = new AuthoritativeItemState(
            raw.CreateInfo,
            raw.ItemType,
            raw.PositionX,
            raw.PositionY,
            raw.Deleted,
            raw.Identified,
            raw.Magical,
            raw.EquipLocation,
            raw.ItemClass,
            raw.Value,
            raw.IdentifiedValue,
            raw.MinDamage,
            raw.MaxDamage,
            raw.ArmorClass,
            raw.Flags,
            raw.MiscId,
            stableSpellId,
            raw.ItemIndex,
            raw.Charges,
            raw.MaxCharges,
            raw.Durability,
            raw.MaxDurability) {
            StableItemId = binding.StableItemId,
            SpellSourceKind = binding.SpellSourceKind,
            ItemActionKind = binding.ItemActionKind ?? DiabloItemActionKind.None,
            PlusDamage = raw.PlusDamage,
            PlusToHit = raw.PlusToHit,
            PlusArmorClass = raw.PlusArmorClass,
            PlusStrength = raw.PlusStrength,
            PlusMagic = raw.PlusMagic,
            PlusDexterity = raw.PlusDexterity,
            PlusVitality = raw.PlusVitality,
            PlusFireResistance = raw.PlusFireResistance,
            PlusLightningResistance = raw.PlusLightningResistance,
            PlusMagicResistance = raw.PlusMagicResistance,
            PlusMana = raw.PlusMana,
            PlusHitPoints = raw.PlusHitPoints,
            PlusDamageModifier = raw.PlusDamageModifier,
            PlusGetHit = raw.PlusGetHit,
            PlusLight = raw.PlusLight,
            SpellLevelAdd = raw.SpellLevelAdd,
            UniqueId = raw.UniqueId,
            FireMinDamage = raw.FireMinDamage,
            FireMaxDamage = raw.FireMaxDamage,
            LightningMinDamage = raw.LightningMinDamage,
            LightningMaxDamage = raw.LightningMaxDamage,
            PlusEnemyArmorClass = raw.PlusEnemyArmorClass,
            PrefixPower = raw.PrefixPower,
            SuffixPower = raw.SuffixPower,
            ValueAdd1 = raw.ValueAdd1,
            ValueMultiply1 = raw.ValueMultiply1,
            ValueAdd2 = raw.ValueAdd2,
            ValueMultiply2 = raw.ValueMultiply2,
            MinimumStrength = raw.MinimumStrength,
            MinimumMagic = raw.MinimumMagic,
            MinimumDexterity = raw.MinimumDexterity,
            StatFlag = raw.StatFlag,
            HellfireDamageArmorFlags = raw.HellfireDamageArmorFlags,
            Buff = raw.Buff,
            InventoryWidth = binding.InventoryWidth,
            InventoryHeight = binding.InventoryHeight,
            BaseName = raw.BaseName,
            IdentifiedName = raw.IdentifiedName,
            NativeCursorGraphic = raw.CursorGraphic,
        };
        return new(slot, entityId, itemSeed, price, CloneItem(raw), state, binding);
    }

    private AuthoritativeCathedralInitialItemState RehydrateRaw(AuthoritativeWorldItem worldItem)
    {
        var state = worldItem.State;
        if (state.CreateInfo > ushort.MaxValue || state.NativeCursorGraphic is null
            || string.IsNullOrEmpty(state.BaseName) || state.ItemIndex < 0)
            throw new InvalidDataException("World item state cannot be represented as a native Cathedral item.");
        var nativeSpellId = ToNativeSpellId(state.SpellId);
        var raw = new AuthoritativeCathedralInitialItemState {
            Seed = worldItem.ItemSeed,
            CreateInfo = (ushort)state.CreateInfo,
            ItemType = state.ItemType,
            PositionX = worldItem.PositionX,
            PositionY = worldItem.PositionY,
            Deleted = state.Deleted,
            Identified = state.Identified,
            Magical = state.Magical,
            BaseName = state.BaseName,
            IdentifiedName = state.IdentifiedName,
            EquipLocation = state.EquipLocation,
            ItemClass = state.ItemClass,
            CursorGraphic = state.NativeCursorGraphic.Value,
            Value = state.Value,
            IdentifiedValue = state.IdentifiedValue,
            MinDamage = state.MinDamage,
            MaxDamage = state.MaxDamage,
            ArmorClass = state.ArmorClass,
            Flags = state.Flags,
            MiscId = state.MiscId,
            SpellId = nativeSpellId,
            ItemIndex = state.ItemIndex,
            Charges = state.Charges,
            MaxCharges = state.MaxCharges,
            Durability = state.Durability,
            MaxDurability = state.MaxDurability,
            PlusDamage = state.PlusDamage,
            PlusToHit = state.PlusToHit,
            PlusArmorClass = state.PlusArmorClass,
            PlusStrength = state.PlusStrength,
            PlusMagic = state.PlusMagic,
            PlusDexterity = state.PlusDexterity,
            PlusVitality = state.PlusVitality,
            PlusFireResistance = state.PlusFireResistance,
            PlusLightningResistance = state.PlusLightningResistance,
            PlusMagicResistance = state.PlusMagicResistance,
            PlusMana = state.PlusMana,
            PlusHitPoints = state.PlusHitPoints,
            PlusDamageModifier = state.PlusDamageModifier,
            PlusGetHit = state.PlusGetHit,
            PlusLight = state.PlusLight,
            SpellLevelAdd = state.SpellLevelAdd,
            UniqueId = state.UniqueId,
            FireMinDamage = state.FireMinDamage,
            FireMaxDamage = state.FireMaxDamage,
            LightningMinDamage = state.LightningMinDamage,
            LightningMaxDamage = state.LightningMaxDamage,
            PlusEnemyArmorClass = state.PlusEnemyArmorClass,
            PrefixPower = state.PrefixPower,
            SuffixPower = state.SuffixPower,
            ValueAdd1 = state.ValueAdd1,
            ValueMultiply1 = state.ValueMultiply1,
            ValueAdd2 = state.ValueAdd2,
            ValueMultiply2 = state.ValueMultiply2,
            MinimumStrength = state.MinimumStrength,
            MinimumMagic = state.MinimumMagic,
            MinimumDexterity = state.MinimumDexterity,
            StatFlag = state.StatFlag,
            HellfireDamageArmorFlags = state.HellfireDamageArmorFlags,
            Buff = state.Buff,
        };
        return raw;
    }

    private AuthoritativeCathedralInitialItemState ApplyOwnedState(
        AuthoritativeCathedralInitialItemState raw,
        AuthoritativeCathedralItemContentBinding binding,
        AuthoritativeItemState state,
        int positionX,
        int positionY)
    {
        var updated = RehydrateRaw(new AuthoritativeWorldItem(
            1,
            1,
            positionX,
            positionY,
            raw.Seed,
            PriceFor(raw),
            state));
        var cursorGraphic = raw.CursorGraphic;
        if (nativeGoldStackMetadata is { } gold
            && raw.ItemIndex == gold.NativeItemIndex
            && binding.StableItemId == gold.StableItemId)
            cursorGraphic = gold.CursorFor(PriceFor(updated));
        return updated with {
            CursorGraphic = cursorGraphic,
            Animation = raw.Animation with { },
            AnimFlag = raw.AnimFlag,
            SelectionRegion = raw.SelectionRegion,
            PostDraw = raw.PostDraw,
            Request = raw.Request,
        };
    }

    private int ToNativeSpellId(int stableSpellId)
    {
        if (stableSpellId < 0)
            return -1;
        if (profile.NativeSpellBindings is null)
            throw new InvalidDataException("Stable spell identity cannot be inverted without native spell bindings.");
        foreach (var pair in profile.NativeSpellBindings)
            if (pair.Value == stableSpellId)
                return pair.Key;
        throw new InvalidDataException($"Stable spell identity {stableSpellId} has no native ordinal binding.");
    }

    private void ApplyLootState(
        MutableLevel level,
        AuthoritativeCathedralMonsterLootResult loot,
        uint entityId)
    {
        if (loot.WorldState.Items.Count != PoolCapacity
            || loot.WorldState.ActiveItemIds.Count != ActiveCapacity)
            throw new InvalidDataException("Native loot result does not contain a complete item pool.");
        for (var i = 0; i < PoolCapacity; i++)
            level.Items[i] = CloneItem(loot.WorldState.Items[i]);
        for (var i = 0; i < ActiveCapacity; i++)
            level.ActiveItemIds[i] = loot.WorldState.ActiveItemIds[i];
        for (var i = 0; i < CellCount; i++)
            level.SignedItemRefs[i] = loot.WorldState.ItemRefs[i];
        level.ActiveItemCount = loot.WorldState.ActiveItemCount;
        if (!uniqueItemFlags.SetEquals(loot.UniqueItemFlags)) {
            uniqueItemFlags = loot.UniqueItemFlags.ToHashSet();
            uniqueHistoryVersion++;
        }
        if (loot.SelectedSlot >= 0 && loot.SelectedSlot < PoolCapacity)
            level.Bindings[loot.SelectedSlot] = entityId;
        level.Version++;
    }

    private void ApplyChestLootState(
        MutableLevel level,
        AuthoritativeCathedralChestLootResult loot,
        IReadOnlyList<uint> bindings)
    {
        if (loot.WorldState.Items.Count != PoolCapacity
            || loot.WorldState.ActiveItemIds.Count != ActiveCapacity
            || bindings.Count != PoolCapacity)
            throw new InvalidDataException("Native chest result does not contain a complete item-pool projection.");
        for (var i = 0; i < PoolCapacity; i++) {
            level.Items[i] = CloneItem(loot.WorldState.Items[i]);
            level.Bindings[i] = bindings[i];
        }
        for (var i = 0; i < ActiveCapacity; i++)
            level.ActiveItemIds[i] = loot.WorldState.ActiveItemIds[i];
        for (var i = 0; i < CellCount; i++)
            level.SignedItemRefs[i] = loot.WorldState.ItemRefs[i];
        level.ActiveItemCount = loot.WorldState.ActiveItemCount;
        if (!uniqueItemFlags.SetEquals(loot.WorldState.UniqueItemFlags)) {
            uniqueItemFlags = loot.WorldState.UniqueItemFlags.ToHashSet();
            uniqueHistoryVersion++;
        }
        // The chest transition is a mutation even when it creates no items.
        level.Version++;
    }

    private MutableLevel GetLevel(uint levelId)
        => levels.TryGetValue(levelId, out var level)
            ? level
            : throw new KeyNotFoundException($"Native Cathedral item level {levelId} is not configured.");

    private AuthoritativeCathedralItemLevelProfile GetLevelProfile(uint levelId)
        => GetLevelProfile(profile, levelId);

    private static AuthoritativeCathedralItemLevelProfile GetLevelProfile(
        AuthoritativeCathedralItemRuntimeProfile profile,
        uint levelId)
        => profile.LevelProfiles.FirstOrDefault(level => level.LevelId == levelId)
            ?? throw new KeyNotFoundException($"Native Cathedral item level profile {levelId} is not configured.");

    private AuthoritativeCathedralItemContentBinding GetBinding(int nativeItemIndex)
        => profile.ItemBindings.TryGetValue(nativeItemIndex, out var binding)
            ? binding
            : throw new InvalidDataException($"Native item {nativeItemIndex} has no explicit stable content binding.");

    private static void DeleteItem(MutableLevel level, int slot)
    {
        if (slot < 0 || slot >= PoolCapacity)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var activeIndex = Array.IndexOf(level.ActiveItemIds, slot, 0, level.ActiveItemCount);
        if (activeIndex < 0)
            throw new InvalidDataException("Bound native item slot is not active.");
        ClearItemReference(level, level.Items[slot].PositionX, level.Items[slot].PositionY, slot);
        level.ActiveItemCount--;
        if (activeIndex < level.ActiveItemCount)
            (level.ActiveItemIds[activeIndex], level.ActiveItemIds[level.ActiveItemCount]) =
                (level.ActiveItemIds[level.ActiveItemCount], level.ActiveItemIds[activeIndex]);
        level.Bindings[slot] = 0;
    }

    private static int FindBoundSlot(MutableLevel level, uint entityId)
    {
        for (var slot = 0; slot < PoolCapacity; slot++)
            if (level.Bindings[slot] == entityId)
                return slot;
        return -1;
    }

    private static bool TrySetItemReference(MutableLevel level, int x, int y, int slot)
    {
        if (!CanUseDestination(level, x, y, slot))
            return false;
        var index = checked(y * Width + x);
        level.SignedItemRefs[index] = checked(slot + 1);
        return true;
    }

    private static bool CanUseDestination(MutableLevel level, int x, int y, int slot)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return false;
        var index = checked(y * Width + x);
        var existing = level.SignedItemRefs[index];
        return existing == 0 || existing == slot + 1;
    }

    private static void ClearItemReference(MutableLevel level, int x, int y, int slot)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return;
        var index = checked(y * Width + x);
        if (level.SignedItemRefs[index] == slot + 1)
            level.SignedItemRefs[index] = 0;
    }

    private static bool Fail(out int slot)
    {
        slot = -1;
        return false;
    }

    private static uint PriceFor(AuthoritativeCathedralInitialItemState item)
        => checked((uint)Math.Max(0, item.Identified ? item.IdentifiedValue : item.Value));

    private static AuthoritativeCathedralInitialItemState CloneItem(
        AuthoritativeCathedralInitialItemState item)
        => item with { Animation = item.Animation with { } };

    private static DiabloCathedralItemLevelCheckpoint CloneLevel(
        DiabloCathedralItemLevelCheckpoint source)
        => new(
            source.LevelId,
            source.Depth,
            source.Items.Select(CloneItem).ToArray(),
            source.ActiveItemIds.ToArray(),
            source.ActiveItemCount,
            source.SignedItemRefs.ToArray(),
            source.SlotEntityBindings.Select(binding => binding with { }).ToArray(),
            source.RandomState,
            source.GenerationIdentity);

    private static AuthoritativeCathedralItemRuntimeProfile CloneProfile(
        AuthoritativeCathedralItemRuntimeProfile source)
        => new(
            source.MetadataFingerprint,
            source.GenerationFingerprint,
            CloneCheckpoint(source.InitialCheckpoint),
            source.ItemCatalog,
            source.ItemPolicy with { },
            source.MonsterCatalog,
            source.UniqueDefinitions.ToArray(),
            source.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value with { }),
            source.ContentInventoryDimensions with { },
            source.LevelProfiles.Select(level => new AuthoritativeCathedralItemLevelProfile(
                level.LevelId,
                level.Depth,
                level.SolidTiles.ToArray(),
                level.SetPieceTiles.ToArray(),
                level.InitialObjectRefs.ToArray(),
                level.ObjectBindings.Select(binding => binding with { }).ToArray()) {
                    PreserveSparseNativeObjectSlots = level.PreserveSparseNativeObjectSlots,
                }).ToArray(),
            source.NativeSpellBindings?.ToDictionary(pair => pair.Key, pair => pair.Value));

    private static DiabloCathedralItemCheckpoint CloneCheckpoint(
        DiabloCathedralItemCheckpoint source)
        => new(
            source.MetadataFingerprint,
            source.GenerationFingerprint,
            source.UniqueItemFlags.ToArray(),
            source.Levels.Select(CloneLevel).ToArray());

    private static void ValidateProfile(AuthoritativeCathedralItemRuntimeProfile profile)
    {
        RequireFingerprint(profile.MetadataFingerprint, nameof(profile.MetadataFingerprint));
        RequireFingerprint(profile.GenerationFingerprint, nameof(profile.GenerationFingerprint));
        ArgumentNullException.ThrowIfNull(profile.InitialCheckpoint);
        ArgumentNullException.ThrowIfNull(profile.ItemCatalog);
        ArgumentNullException.ThrowIfNull(profile.ItemPolicy);
        ArgumentNullException.ThrowIfNull(profile.MonsterCatalog);
        ArgumentNullException.ThrowIfNull(profile.UniqueDefinitions);
        ArgumentNullException.ThrowIfNull(profile.ItemBindings);
        ArgumentNullException.ThrowIfNull(profile.ContentInventoryDimensions);
        ArgumentNullException.ThrowIfNull(profile.LevelProfiles);
        ArgumentNullException.ThrowIfNull(profile.NativeSpellBindings);
        ValidateDimensions(profile.ContentInventoryDimensions);
        foreach (var binding in profile.ItemBindings.Values)
            ValidateBinding(binding);
        if (profile.ItemBindings.Any(pair => pair.Key != pair.Value.NativeItemIndex))
            throw new InvalidDataException("Native item stable binding dictionary keys must match native indexes.");
        if (profile.ItemBindings.Values.Select(binding => binding.NativeItemIndex).Distinct().Count()
            != profile.ItemBindings.Count)
            throw new InvalidDataException("Native item stable bindings contain duplicate indexes.");
        if (profile.NativeSpellBindings.Keys.Any(nativeId => nativeId is < 0 or > sbyte.MaxValue))
            throw new InvalidDataException("Native spell bindings contain an unsafe native ordinal.");
        foreach (var level in profile.LevelProfiles)
            ValidateLevelProfile(level);
    }

    private static void ValidateCheckpoint(
        DiabloCathedralItemCheckpoint checkpoint,
        AuthoritativeCathedralItemRuntimeProfile profile)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        checkpoint.ValidateShape();
        if (!string.Equals(checkpoint.MetadataFingerprint, profile.MetadataFingerprint, StringComparison.Ordinal)
            || !string.Equals(checkpoint.GenerationFingerprint, profile.GenerationFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Native Cathedral item checkpoint identity does not match the active profile.");
        ArgumentNullException.ThrowIfNull(checkpoint.UniqueItemFlags);
        if (checkpoint.UniqueItemFlags.Any(flag => flag is < 0 or > 127)
            || checkpoint.UniqueItemFlags.Distinct().Count() != checkpoint.UniqueItemFlags.Count
            || !checkpoint.UniqueItemFlags.SequenceEqual(checkpoint.UniqueItemFlags.OrderBy(flag => flag)))
            throw new InvalidDataException("Native unique item history must contain distinct non-negative IDs.");
        ArgumentNullException.ThrowIfNull(checkpoint.Levels);
        if (checkpoint.Levels.Count == 0
            || checkpoint.Levels.Count != profile.LevelProfiles.Count
            || checkpoint.Levels.Any(level => !profile.LevelProfiles.Any(
                candidate => candidate.LevelId == level.LevelId)))
            throw new InvalidDataException("Native Cathedral item checkpoint must contain a level.");
        if (checkpoint.Levels.Select(level => level.LevelId).Distinct().Count() != checkpoint.Levels.Count)
            throw new InvalidDataException("Native Cathedral item checkpoint contains duplicate levels.");
        foreach (var level in checkpoint.Levels)
            ValidateLevelShape(level);
        foreach (var level in checkpoint.Levels) {
            var profileLevel = profile.LevelProfiles.FirstOrDefault(candidate => candidate.LevelId == level.LevelId);
            if (profileLevel is null || profileLevel.Depth != level.Depth)
                throw new InvalidDataException("Native item checkpoint level identity does not match the profile.");
        }
    }

    internal static void ValidateCheckpointShape(DiabloCathedralItemCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (!IsCanonicalFingerprint(checkpoint.MetadataFingerprint)
            || !IsCanonicalFingerprint(checkpoint.GenerationFingerprint))
            throw new InvalidDataException("Native Cathedral item checkpoint identities must be canonical SHA-256 fingerprints.");
        if (checkpoint.UniqueItemFlags is null || checkpoint.UniqueItemFlags.Any(flag => flag is < 0 or > 127)
            || checkpoint.UniqueItemFlags.Distinct().Count() != checkpoint.UniqueItemFlags.Count
            || !checkpoint.UniqueItemFlags.SequenceEqual(checkpoint.UniqueItemFlags.OrderBy(flag => flag)))
            throw new InvalidDataException("Native unique item history must be sorted-unique IDs from 0 through 127.");
        if (checkpoint.Levels is null || checkpoint.Levels.Count == 0
            || checkpoint.Levels.Select(level => level.LevelId).Distinct().Count() != checkpoint.Levels.Count
            || !checkpoint.Levels.Select(level => level.LevelId)
                .SequenceEqual(checkpoint.Levels.Select(level => level.LevelId).OrderBy(levelId => levelId)))
            throw new InvalidDataException("Native Cathedral item checkpoint levels are not unique.");
        foreach (var level in checkpoint.Levels)
            ValidateLevelShape(level);
    }

    private static void ValidateLevelProfile(AuthoritativeCathedralItemLevelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Depth is < 1 or > 4)
            throw new InvalidDataException("Native Cathedral item level depth is outside the supported range.");
        ValidateCells(profile.SolidTiles, nameof(profile.SolidTiles));
        ValidateCells(profile.SetPieceTiles, nameof(profile.SetPieceTiles));
        ValidateCells(profile.InitialObjectRefs, nameof(profile.InitialObjectRefs));
        ArgumentNullException.ThrowIfNull(profile.ObjectBindings);
        var bindings = profile.ObjectBindings.ToArray();
        if (bindings.Length > ActiveCapacity
            || bindings.Any(binding => binding is null || binding.NativeObjectSlot < 0
                || binding.NativeObjectSlot >= ActiveCapacity
                || binding.PositionX is < 0 or >= Width
                || binding.PositionY is < 0 or >= Height
                || binding.InitialSelectionRegion is < 0 or > 7
                || binding.EntityId == 0
                || !Enum.IsDefined(binding.MutationPolicy)))
            throw new InvalidDataException("Native object provenance contains an invalid binding.");
        if (bindings.Select(binding => binding.NativeObjectSlot).Distinct().Count() != bindings.Length)
            throw new InvalidDataException("Native object provenance contains duplicate slots.");
        if (!profile.PreserveSparseNativeObjectSlots
            && bindings.Select(binding => binding.NativeObjectSlot).OrderBy(slot => slot)
                .Select((slot, index) => slot == index).Any(valid => !valid))
            throw new InvalidDataException("Native object provenance must retain dense native slot order.");
        if (profile.InitialObjectRefs.Any(value => value is < -127 or > sbyte.MaxValue))
            throw new InvalidDataException("Native signed object references exceed their field width.");
        if (bindings.Where(binding => binding.EntityId != 0)
                .GroupBy(binding => binding.EntityId).Any(group => group.Count() != 1))
            throw new InvalidDataException("Native object provenance contains duplicate live entities.");
        var bySlot = bindings.ToDictionary(binding => binding.NativeObjectSlot);
        foreach (var entry in profile.InitialObjectRefs.Select((value, index) => (value, index))) {
            if (entry.value <= 0)
                if (entry.value != 0 && !bySlot.ContainsKey(Math.Abs(entry.value) - 1))
                    throw new InvalidDataException("Native signed object footprint references an unbound object.");
            if (entry.value > 0) {
                var slot = entry.value - 1;
                if (!bySlot.TryGetValue(slot, out var binding)
                    || entry.index != binding.PositionY * Width + binding.PositionX)
                    throw new InvalidDataException("Native positive object footprint disagrees with object provenance.");
            }
        }
        foreach (var binding in bindings) {
            var mainIndex = binding.PositionY * Width + binding.PositionX;
            if (profile.InitialObjectRefs[mainIndex] != binding.NativeObjectSlot + 1)
                throw new InvalidDataException("Native positive object footprint disagrees with object provenance.");
        }
    }

    internal static void ValidateLevelShape(DiabloCathedralItemLevelCheckpoint level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (level.Depth is < 1 or > 4)
            throw new InvalidDataException("Native Cathedral item level depth is outside the supported range.");
        ArgumentNullException.ThrowIfNull(level.Items);
        ArgumentNullException.ThrowIfNull(level.ActiveItemIds);
        ArgumentNullException.ThrowIfNull(level.SignedItemRefs);
        ArgumentNullException.ThrowIfNull(level.SlotEntityBindings);
        if (level.Items.Count != PoolCapacity)
            throw new InvalidDataException($"Native item storage requires exactly {PoolCapacity} entries.");
        if (level.ActiveItemIds.Count != ActiveCapacity)
            throw new InvalidDataException($"Native ActiveItems requires exactly {ActiveCapacity} entries.");
        if (level.ActiveItemCount is < 0 or > ActiveCapacity)
            throw new InvalidDataException("Native ActiveItemCount is outside the native pool range.");
        if (level.ActiveItemIds.Any(id => id is < 0 or >= ActiveCapacity)
            || level.ActiveItemIds.Distinct().Count() != ActiveCapacity)
            throw new InvalidDataException("Native ActiveItems must be a permutation of slots 0 through 126.");
        ValidateCells(level.SignedItemRefs, nameof(level.SignedItemRefs));
        if (level.SignedItemRefs.Any(value => value is < 0 or > sbyte.MaxValue))
            throw new InvalidDataException("Native signed item references exceed their field width.");
        if (level.SlotEntityBindings.Count != PoolCapacity
            || level.SlotEntityBindings.Select((binding, index) => binding.Slot == index).Any(valid => !valid)
            || level.SlotEntityBindings.Select(binding => binding.Slot).Distinct().Count() != PoolCapacity
            || level.SlotEntityBindings.Any(binding => binding.Slot is < 0 or >= PoolCapacity))
            throw new InvalidDataException("Native slot/entity bindings must cover all 128 slots exactly once.");
        if (level.SlotEntityBindings.Where(binding => binding.EntityId != 0)
                .GroupBy(binding => binding.EntityId).Any(group => group.Count() != 1))
            throw new InvalidDataException("Native slot/entity bindings contain duplicate live entities.");
        foreach (var item in level.Items)
            AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
        var activeSlots = level.ActiveItemIds.Take(level.ActiveItemCount).ToHashSet();
        var refsBySlot = level.SignedItemRefs
            .Select((value, index) => (value, index))
            .Where(entry => entry.value != 0)
            .GroupBy(entry => entry.value - 1)
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (refsBySlot.Keys.Any(slot => !activeSlots.Contains(slot)))
            throw new InvalidDataException("Native signed item reference points at an inactive slot.");
        var bindingsBySlot = level.SlotEntityBindings.ToDictionary(binding => binding.Slot);
        if (refsBySlot.Keys.Any(slot => bindingsBySlot[slot].EntityId == 0))
            throw new InvalidDataException("Native signed item reference has no visible entity binding.");
        for (var slot = 0; slot < PoolCapacity; slot++) {
            var binding = bindingsBySlot[slot];
            if (binding.EntityId == 0)
                continue;
            if (!activeSlots.Contains(slot))
                throw new InvalidDataException("A visible native item binding points at an inactive slot.");
            var item = level.Items[slot];
            if ((uint)item.PositionX >= Width || (uint)item.PositionY >= Height
                || !refsBySlot.TryGetValue(slot, out var refs)
                || refs.Length != 1
                || refs[0].index != item.PositionY * Width + item.PositionX)
                throw new InvalidDataException("A visible native item binding disagrees with its signed tile reference.");
        }
    }

    private void ValidateProjection(
        AuthoritativeCathedralNativeItemProjection projection,
        bool allowSlotMismatch)
    {
        ArgumentNullException.ThrowIfNull(projection.Raw);
        ArgumentNullException.ThrowIfNull(projection.State);
        ValidateBinding(projection.Binding);
        if (projection.Slot is < 0 or >= PoolCapacity
            || !allowSlotMismatch && projection.Slot < 0
            || projection.Raw.ItemIndex != projection.Binding.NativeItemIndex)
            throw new InvalidDataException("Native item projection has an unsafe slot or identity.");
        AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(projection.Raw);
        if (projection.EntityId == 0 || projection.ItemSeed != projection.Raw.Seed
            || projection.Price != PriceFor(projection.Raw)
            || !profile.ItemBindings.TryGetValue(projection.Raw.ItemIndex, out var expectedBinding)
            || !Equals(expectedBinding, projection.Binding))
            throw new InvalidDataException("A live world-item projection requires a non-zero entity ID.");
        var expectedSpellId = projection.Raw.SpellId;
        if (expectedSpellId >= 0
            && (profile.NativeSpellBindings is null
                || !profile.NativeSpellBindings.TryGetValue(expectedSpellId, out expectedSpellId)))
            throw new InvalidDataException("Native item projection has no stable spell binding.");
        var state = projection.State;
        if (state.CreateInfo != projection.Raw.CreateInfo
            || state.ItemType != projection.Raw.ItemType
            || state.PositionX != projection.Raw.PositionX
            || state.PositionY != projection.Raw.PositionY
            || state.Deleted != projection.Raw.Deleted
            || state.Identified != projection.Raw.Identified
            || state.Magical != projection.Raw.Magical
            || state.EquipLocation != projection.Raw.EquipLocation
            || state.ItemClass != projection.Raw.ItemClass
            || state.Value != projection.Raw.Value
            || state.IdentifiedValue != projection.Raw.IdentifiedValue
            || state.MinDamage != projection.Raw.MinDamage
            || state.MaxDamage != projection.Raw.MaxDamage
            || state.ArmorClass != projection.Raw.ArmorClass
            || state.Flags != projection.Raw.Flags
            || state.MiscId != projection.Raw.MiscId
            || state.SpellId != expectedSpellId
            || state.ItemIndex != projection.Raw.ItemIndex
            || state.Charges != projection.Raw.Charges
            || state.MaxCharges != projection.Raw.MaxCharges
            || state.Durability != projection.Raw.Durability
            || state.MaxDurability != projection.Raw.MaxDurability
            || state.StableItemId != projection.Binding.StableItemId
            || state.BaseName != projection.Raw.BaseName
            || state.IdentifiedName != projection.Raw.IdentifiedName
            || state.NativeCursorGraphic != projection.Raw.CursorGraphic
            || state.InventoryWidth != projection.Binding.InventoryWidth
            || state.InventoryHeight != projection.Binding.InventoryHeight)
            throw new InvalidDataException("Native item projection does not preserve the canonical raw scalar projection.");
        var expected = ProjectNativeItem(
            projection.Slot,
            projection.EntityId,
            projection.Raw.Seed,
            PriceFor(projection.Raw),
            projection.Raw,
            projection.Binding,
            profile.ContentInventoryDimensions);
        if (projection.Price != expected.Price || projection.State != expected.State)
            throw new InvalidDataException("Native item projection contains a non-canonical gameplay field.");
    }

    private static void ValidateBinding(AuthoritativeCathedralItemContentBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.NativeItemIndex < 0 || binding.StableItemId == 0
            || string.IsNullOrWhiteSpace(binding.StableSymbol)
            || binding.InventoryWidth < 1 || binding.InventoryHeight < 1
            || binding.SpellSourceKind is { } sourceKind && !Enum.IsDefined(sourceKind)
            || binding.ItemActionKind is { } actionKind && !Enum.IsDefined(actionKind))
            throw new InvalidDataException("Native item stable binding is incomplete.");
    }

    private static void ValidateDimensions(AuthoritativeCathedralItemInventoryDimensions dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        if (dimensions.InventoryWidth < 1 || dimensions.InventoryHeight < 1
            || dimensions.BeltWidth < 1 || dimensions.BeltHeight < 1
            || dimensions.StashWidth < 1 || dimensions.StashHeight < 1)
            throw new InvalidDataException("Native item inventory dimensions must be positive.");
    }

    private static void ValidateCells<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != CellCount)
            throw new InvalidDataException($"Native {name} requires exactly {CellCount} entries.");
    }

    private static void RequireFingerprint(string value, string name)
    {
        if (!IsCanonicalFingerprint(value))
            throw new InvalidDataException($"Native Cathedral item {name} is required.");
    }

    private static bool IsCanonicalFingerprint(string? value)
        => value is { Length: 64 }
            && value.All(character => character is >= '0' and <= '9'
                or >= 'A' and <= 'F'
                or >= 'a' and <= 'f');

    private sealed class MutableLevel
    {
        public MutableLevel(
            DiabloCathedralItemLevelCheckpoint source,
            AuthoritativeCathedralItemLevelProfile levelProfile)
        {
            LevelId = source.LevelId;
            Depth = source.Depth;
            Items = source.Items.Select(CloneItem).ToArray();
            ActiveItemIds = source.ActiveItemIds.ToArray();
            ActiveItemCount = source.ActiveItemCount;
            SignedItemRefs = source.SignedItemRefs.ToArray();
            Bindings = Enumerable.Repeat(0u, PoolCapacity).ToArray();
            foreach (var binding in source.SlotEntityBindings)
                Bindings[binding.Slot] = binding.EntityId;
            RandomState = source.RandomState;
            GenerationIdentity = source.GenerationIdentity;
            // Sparse native pools retain source slot numbers; legacy dense profiles keep their array lengths.
            var objectCapacity = levelProfile.ObjectBindings.Count == 0 ? 0
                : levelProfile.ObjectBindings.Max(binding => binding.NativeObjectSlot) + 1;
            ObjectSolid = new bool[objectCapacity];
            ObjectCanInteract = new bool[objectCapacity];
            foreach (var binding in levelProfile.ObjectBindings) {
                ObjectSolid[binding.NativeObjectSlot] = binding.InitialSolid;
                ObjectCanInteract[binding.NativeObjectSlot] = binding.InitialSelectionRegion != 0;
            }
        }

        public uint LevelId { get; }
        public int Depth { get; }
        public AuthoritativeCathedralInitialItemState[] Items { get; }
        public int[] ActiveItemIds { get; }
        public int ActiveItemCount { get; set; }
        public int[] SignedItemRefs { get; }
        public uint[] Bindings { get; }
        public uint RandomState { get; }
        public string? GenerationIdentity { get; }
        public int Version { get; set; }

        public DiabloCathedralItemLevelCheckpoint Checkpoint => new(
            LevelId,
            Depth,
            Items.Select(CloneItem).ToArray(),
            ActiveItemIds.ToArray(),
            ActiveItemCount,
            SignedItemRefs.ToArray(),
            Bindings.Select((entityId, slot) => new DiabloCathedralItemSlotEntityBinding(slot, entityId)).ToArray(),
            RandomState,
            GenerationIdentity);

        public bool[] ObjectSolid { get; }
        public bool[] ObjectCanInteract { get; }
    }

    private sealed class ReadOnlySet<T>(IEnumerable<T> values) : IReadOnlySet<T>
    {
        private readonly HashSet<T> set = values.ToHashSet();
        public int Count => set.Count;
        public bool Contains(T item) => set.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => set.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => set.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => set.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => set.SetEquals(other);
        public IEnumerator<T> GetEnumerator() => set.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
