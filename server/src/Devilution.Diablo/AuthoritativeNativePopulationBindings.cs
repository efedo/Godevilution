using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

internal enum AuthoritativeNativePopulationFamily { Cathedral, Catacombs }

/** Stable immutable pool, identity, footprint and lifecycle bindings shared by native families. */
internal sealed partial class AuthoritativeNativePopulationBindings
{
    private const int Chest1 = 5, Chest2 = 6, Chest3 = 7, LeftTrap = 53, RightTrap = 54;
    private readonly Dictionary<uint, DiabloObjectState> objectSnapshots = [];
    private readonly Dictionary<uint, AuthoritativeCathedralPopulationObjectState> objectRows = [];
    private readonly Dictionary<int, uint> objectEntitiesBySlot = [];
    private readonly Dictionary<uint, AuthoritativeNativeMonsterInitialization> liveInitializations = [];
    private readonly Dictionary<uint, string> liveNativeAis = [];
    private readonly HashSet<uint> reservedEntityIds = [];
    private readonly AuthoritativeCathedralMonsterRuntimeAdapterResult liveMonsterAdapter;
    private readonly string? itemBindingFingerprint;
    private readonly AuthoritativeLevel backingLevel;
    private readonly DiabloWorldGeometryLevelState backingGeometry;

    internal AuthoritativeNativePopulationBindings(
        AuthoritativeNativePopulationFamily family, int depth,
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralMonsterRuntimeAdapterResult monsters,
        IReadOnlyList<AuthoritativeWorldObject> objects,
        AuthoritativeLevel preparedBacking,
        IReadOnlyList<AuthoritativeCathedralPopulationMonsterDefinition> monsterDefinitions,
        AuthoritativeCathedralItemRuntimeProfile? itemProfile,
        AuthoritativeCathedralNativePopulationPolicy? cathedralPolicy = null,
        string? catacombsDoorCatalogFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(preparedBacking);
        ArgumentNullException.ThrowIfNull(monsterDefinitions);
        if (!Enum.IsDefined(family)
            || (family == AuthoritativeNativePopulationFamily.Cathedral ? depth is < 1 or > 4 : depth is < 5 or > 8)
            || (family == AuthoritativeNativePopulationFamily.Cathedral) != (cathedralPolicy is not null))
            throw new InvalidDataException("Native population bindings require an explicit family and depth.");
        if (family == AuthoritativeNativePopulationFamily.Catacombs
            ? !AuthoritativeNativeMonsterInitialization.IsFingerprint(catacombsDoorCatalogFingerprint)
            : catacombsDoorCatalogFingerprint is not null)
            throw new InvalidDataException("Native door catalog identity must match its explicit population family.");
        CatacombsDoorCatalogFingerprint = catacombsDoorCatalogFingerprint;
        Family = family;
        CathedralPolicy = cathedralPolicy;
        Depth = depth;
        InitialSpecialStandActors = Array.AsReadOnly(monsters.InitialSpecialStandActors.ToArray());
        if (monsters.ActiveMonsterCount != state.ActiveMonsterCount
            || !monsters.ActiveMonsterIds.SequenceEqual(state.ActiveMonsterIds)
            || !monsters.MonsterSlots.SequenceEqual(state.MonsterSlots)
            || !monsters.MonsterPetrified.SequenceEqual(state.MonsterPetrified)
            || monsters.Records.Count != state.ActiveMonsterCount)
            throw new InvalidDataException("The native actor adapter must retain the entire generated monster pool and order.");
        liveMonsterAdapter = monsters;
        var levelIds = monsters.Records.Select(row => row.Binding.LevelId).Distinct().ToArray();
        if (levelIds.Length != 1 || levelIds[0] == 0)
            throw new InvalidDataException("The populated Cathedral requires one explicit non-Town level identity.");
        LevelId = levelIds[0];
        MonsterMetadataFingerprint = monsters.MetadataFingerprint;
        InitialState = state;
        populationMonsterDefinitions = monsterDefinitions.ToDictionary(definition => definition.NativeType);
        if (preparedBacking.LevelId != LevelId)
            throw new InvalidDataException("Native population backing and actor floor identities disagree.");
        backingLevel = AuthoritativeCathedralDoors.CopyLevel(preparedBacking);
        var backingWorld = new AuthoritativeWorld();
        backingWorld.AddLevel(backingLevel);
        backingGeometry = backingWorld.CaptureGeometryState().Levels!.Single();
        var identities = new HashSet<uint>();
        var reservations = new List<AuthoritativeNativeMonsterInitialization>();
        var activeObjectRowsBySlot = state.ActiveObjectIds.Take(state.ActiveObjectCount)
            .Select(slot => state.ObjectSlots[slot]).ToDictionary(row => row.Slot);
        foreach (var owner in GetNativePreSpawnOwnerLinks(activeObjectRowsBySlot.Values))
            linkedPreSpawnOwnerObjectByMonsterSlot.Add(owner.Key, owner.Value);
        for (var index = 0; index < monsters.Records.Count; index++) {
            var row = monsters.Records[index];
            row.Initialization.Validate();
            if (row.Binding.NativeSlot != state.ActiveMonsterIds[index]
                || row.InitialState != state.MonsterSlots[row.Binding.NativeSlot]
                || row.Initialization.InitialState != row.InitialState
                || row.Initialization.MetadataFingerprint != MonsterMetadataFingerprint
                || row.Initialization.Binding.EntityId != row.Binding.EntityId
                || row.Initialization.Binding.ContentMonsterId != row.Binding.ContentMonsterId
                || row.Initialization.Binding.LevelId != LevelId
                || !identities.Add(row.Binding.EntityId))
                throw new InvalidDataException("Generated native actor identity or initialization changed during binding.");
            if (row.Target is not null) {
                if (linkedPreSpawnOwnerObjectByMonsterSlot.ContainsKey(row.Binding.NativeSlot))
                    throw new InvalidDataException("A native object reservation points at an already-active monster.");
                if (row.UnsupportedReason != AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None
                    || row.Target.NativeInitialization != row.Initialization)
                    throw new InvalidDataException("A populated actor cannot normalize an unsupported native state.");
                if (row.Target.IsPlayerMinion)
                    throw new NotSupportedException("The populated L1 policy excludes player minions until their reserved slots have a live owner.");
                liveInitializations.Add(row.Binding.EntityId, row.Initialization);
                liveNativeAis.Add(row.Binding.EntityId, row.Target.NativeAi);
                continue;
            }
            if (linkedPreSpawnOwnerObjectByMonsterSlot.TryGetValue(row.Binding.NativeSlot, out var ownerObjectSlot)) {
                var owner = activeObjectRowsBySlot[ownerObjectSlot];
                if (!IsLinkedPreSpawnSkeleton(row, state)
                    || (owner.Type == Sarcophagus && (owner.Var1 < 8 || owner.Var2 != row.Binding.NativeSlot))
                    || (owner.Type == Barrel && (owner.Var2 < 8 || owner.Var4 != row.Binding.NativeSlot)))
                    throw new InvalidDataException("A native pre-spawn skeleton reservation does not match its exact object owner.");
                reservedEntityIds.Add(row.Binding.EntityId);
                if (owner.Type == Sarcophagus)
                    sarcophagusReservationsByObjectSlot.Add(owner.Slot, row.Initialization);
                else if (owner.Type == Barrel)
                    barrelReservationsByObjectSlot.Add(owner.Slot, row.Initialization);
                continue;
            }
            if (!IsDormantGolemReservation(row, state))
                throw new NotSupportedException($"Native slot {row.Binding.NativeSlot} cannot be activated: {row.UnsupportedReason}.");
            reservedEntityIds.Add(row.Binding.EntityId);
            reservations.Add(row.Initialization);
        }
        if (liveInitializations.Count == 0 || reservations.Count != 4
            || !reservations.Select(row => row.Binding.NativeSlot).Order().SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw new InvalidDataException("Native L1 must retain exactly the four dormant golem reservations.");
        Reservations = Array.AsReadOnly(reservations.ToArray());
        if (objects.Count != state.ActiveObjectCount)
            throw new InvalidDataException("Every generated active object requires an explicit projection and policy.");
        for (var index = 0; index < objects.Count; index++) {
            var projection = objects[index];
            var raw = state.ObjectSlots[state.ActiveObjectIds[index]];
            if (projection.LevelId != LevelId || projection.NativeObjectId != raw.Type
                || projection.PositionX != raw.Position.X || projection.PositionY != raw.Position.Y
                || projection.Activated || projection.Destroyed
                || projection.Breakable
                || projection.MissilePassable != raw.MissilePassable
                || projection.TrapArmed != raw.TrapFlag
                || projection.EffectKind != AuthoritativeObjectEffectKind.None
                || projection.RewardKind != AuthoritativeObjectRewardKind.None
                || projection.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
                || projection.ActivateOnOccupancy
                || !identities.Add(projection.EntityId))
                throw new InvalidDataException("A native object projection changed its raw provenance or added authored behavior.");
            if (raw.DoorFlag && ((Family == AuthoritativeNativePopulationFamily.Cathedral ? raw.Type is not (1 or 2) : raw.Type is not (42 or 43)) || projection.HideWhenActivated))
                throw new NotSupportedException("Generated Cathedral doors require a supported type and persistent visible projection.");
            objectRows.Add(projection.EntityId, raw);
            objectEntitiesBySlot.Add(raw.Slot, projection.EntityId);
            objectSnapshots.Add(projection.EntityId, DiabloSimulation.CaptureObject(projection));
        }
        if (sarcophagusReservationsByObjectSlot.Keys.Any(slot => !objectEntitiesBySlot.ContainsKey(slot))
            || barrelReservationsByObjectSlot.Keys.Any(slot => !objectEntitiesBySlot.ContainsKey(slot)))
            throw new InvalidDataException("A linked sarcophagus reservation has no retained object projection.");
        itemBindingFingerprint = itemProfile is null ? null : ComputeItemBindingFingerprint(itemProfile);
    }

    internal AuthoritativeNativePopulationFamily Family { get; }
    internal string? CatacombsDoorCatalogFingerprint { get; }
    internal AuthoritativeCathedralNativePopulationPolicy? CathedralPolicy { get; }
    internal int Depth { get; }
    internal uint LevelId { get; }
    internal string MonsterMetadataFingerprint { get; }
    internal AuthoritativeCathedralThemeRoomState InitialState { get; }
    internal IReadOnlyList<AuthoritativeNativeMonsterInitialization> Reservations { get; }
    internal IReadOnlyList<AuthoritativeNativeInitialSpecialStand> InitialSpecialStandActors { get; }
    internal IEnumerable<uint> BoundEntityIds
        => objectRows.Keys.Concat(liveInitializations.Keys).Concat(reservedEntityIds);
    internal IReadOnlyDictionary<uint, string> LiveNativeAis => liveNativeAis;
    internal string? ItemBindingFingerprint => itemBindingFingerprint;
    private string? fingerprint;
    internal string Fingerprint => fingerprint
        ?? throw new InvalidOperationException("Native population generation identity is not bound.");

    internal void BindGenerationFingerprint(string value)
    {
        if (fingerprint is not null || !AuthoritativeNativeMonsterInitialization.IsFingerprint(value))
            throw new InvalidOperationException("Native population generation identity is bound exactly once.");
        fingerprint = value;
    }

    internal bool ReservesEntityId(uint entityId) => reservedEntityIds.Contains(entityId);
    internal AuthoritativeLevel CreateBackingLevel() => AuthoritativeCathedralDoors.CopyLevel(backingLevel);

    internal void ValidateBackingLevel(AuthoritativeLevel level, bool allowOmittedSpecial = false)
    {
        var actual = new AuthoritativeWorld();
        actual.AddLevel(level);
        DiabloCathedralDoorCheckpoint.RequireSameLevels([backingGeometry], actual.CaptureGeometryState().Levels);
        if (!level.NativeSpecialLayer.SequenceEqual(InitialState.Special.Select(number => (ushort)number))
            && !(allowOmittedSpecial && level.NativeSpecialLayer.All(number => number == 0)))
            throw new InvalidDataException("Native backing special bytes differ from the retained population.");
    }


    internal uint? ObjectEntityAt(int x, int y)
    {
        if ((uint)x >= AuthoritativeCathedralThemeRoomState.Width || (uint)y >= AuthoritativeCathedralThemeRoomState.Width)
            return null;
        var reference = InitialState.ObjectRefs[y * AuthoritativeCathedralThemeRoomState.Width + x];
        if (reference == 0)
            return null;
        if (!objectEntitiesBySlot.TryGetValue(Math.Abs(reference) - 1, out var entityId))
            throw new InvalidDataException("A signed native object footprint has no active binding.");
        return entityId;
    }

    internal IEnumerable<(uint LevelId, int X, int Y)> SolidObjectPositions()
    {
        for (var index = 0; index < InitialState.ObjectRefs.Count; index++) {
            var reference = InitialState.ObjectRefs[index];
            if (reference != 0 && InitialState.ObjectSlots[Math.Abs(reference) - 1].SolidFlag)
                yield return (LevelId, index % AuthoritativeCathedralThemeRoomState.Width,
                    index / AuthoritativeCathedralThemeRoomState.Width);
        }
    }

    internal bool ObjectIsSolid(uint entityId) => objectRows[entityId].SolidFlag;

    internal int InitialDoorCount => objectRows.Values.Count(row => row.DoorFlag);
    internal AuthoritativeCathedralDoorState GetInitialDoor(uint entityId)
    {
        if (!objectRows.TryGetValue(entityId, out var raw) || !raw.DoorFlag || raw.Type is not (1 or 2))
            throw new InvalidDataException("A saved native door has no raw generated binding.");
        return new((AuthoritativeCathedralDoorKind)raw.Type, raw.Position.X, raw.Position.Y,
            (AuthoritativeCathedralDoorStatus)raw.Var4, raw.Var1, raw.Var2,
            checked((int)raw.AnimFrame), raw.SelectionRegion, raw.PreFlag, raw.MissilePassable, raw.SolidFlag);
    }

    internal void ValidateObject(AuthoritativeWorldObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateObjectProjection(DiabloSimulation.CaptureObject(value));
    }

    internal void ValidateObjectProjection(DiabloObjectState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.LevelId != LevelId || !objectRows.TryGetValue(current.EntityId, out var raw))
            throw new InvalidDataException("A populated native object has no owned generation binding.");
        var expected = objectSnapshots[current.EntityId];
        if (expected.TrapArmed && !current.TrapArmed
            && HasSupportedNativeTrapLink(InitialState, raw)) {
            // The per-world trap owner validates whether this one-way raw trap-flag
            // change came from a successful disarm or a completed wall trap.
            current = current with { TrapArmed = expected.TrapArmed };
        }
        if (raw.DoorFlag) {
            // The separate door owner validates its state, cached geometry and scalar changes.
            current = current with {
                MissilePassable = expected.MissilePassable,
                AnimationFrame = expected.AnimationFrame,
            };
        }
        if (current == expected)
            return;
        if (IsSupportedNativeChest(raw)) {
            var opened = expected with {
                Activated = true,
                AnimationFrame = GetOpenedChestAnimationFrame(raw),
            };
            if (current == opened)
                return;
        }
        if (IsSupportedNativeSarcophagus(InitialState, raw) && current.Activated
            && current with { Activated = expected.Activated, AnimationFrame = expected.AnimationFrame } == expected
            && current.AnimationFrame >= raw.AnimFrame && current.AnimationFrame <= raw.AnimLength)
            return;
        if (IsSupportedNativeBarrel(InitialState, raw) && current.Activated
            && current with {
                Activated = expected.Activated,
                AnimationFrame = expected.AnimationFrame,
                MissilePassable = expected.MissilePassable,
            } == expected
            && current.MissilePassable
            && current.AnimationFrame is >= 1 && current.AnimationFrame <= raw.AnimLength)
            return;
        if (current != expected)
            throw new InvalidDataException("An unsupported native object mutation changed retained state.");
    }
    internal bool CanOperate(AuthoritativeWorldObject value)
    {
        ValidateObject(value);
        var raw = objectRows[value.EntityId];
        var supportedLinkedTrapDoor = raw.TrapFlag && HasSupportedNativeTrapLink(InitialState, raw);
        return raw.DoorFlag && (!raw.TrapFlag || supportedLinkedTrapDoor) && value.CathedralDoor is not null
            || CanOperateNativeChest(value)
            || CanOperateNativeSarcophagus(value);
    }

    internal bool CanOperateNativeChest(AuthoritativeWorldObject value)
    {
        ValidateObject(value);
        return objectRows.TryGetValue(value.EntityId, out var raw)
            && IsSupportedNativeChest(raw)
            && !value.Activated;
    }

    internal bool TryGetNativeChestLootParameters(
        AuthoritativeWorldObject value,
        out int nativeType,
        out uint randomSeed,
        out int itemCount,
        out int itemBranch)
    {
        nativeType = 0;
        randomSeed = 0;
        itemCount = 0;
        itemBranch = 0;
        ValidateObject(value);
        if (!objectRows.TryGetValue(value.EntityId, out var raw)
            || !IsSupportedNativeChest(raw)
            || value.Activated)
            return false;

        nativeType = raw.Type;
        randomSeed = raw.RndSeed;
        itemCount = raw.Var1;
        itemBranch = raw.Var2;
        return true;
    }

    internal bool TryCommitNativeChestOpen(AuthoritativeWorldObject value)
    {
        if (!CanOperateNativeChest(value))
            return false;
        var raw = objectRows[value.EntityId];
        value.Activated = true;
        value.AnimationFrame = GetOpenedChestAnimationFrame(raw);
        ValidateObject(value);
        return true;
    }

    internal static bool IsSupportedNativeChest(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
    {
        var hasTrap = HasLinkedNativeTrap(state, raw);
        if (raw.Type is not (Chest1 or Chest2 or Chest3)
            || raw.TrapFlag != hasTrap
            || hasTrap && !HasSupportedNativeTrapLink(state, raw)
            || raw.SelectionRegion == 0 || raw.AnimFrame > byte.MaxValue - 2
            || raw.Var1 < 0 || raw.Var1 > raw.Type - Chest1 + 1
            || raw.Var2 is < 0 or >= 8)
            return false;
        return true;
    }

    private bool IsSupportedNativeChest(AuthoritativeCathedralPopulationObjectState raw)
        => Family == AuthoritativeNativePopulationFamily.Catacombs
            ? IsSupportedNativeCatacombsChest(InitialState, raw)
            : IsSupportedNativeChest(InitialState, raw);

    private static int GetOpenedChestAnimationFrame(AuthoritativeCathedralPopulationObjectState raw)
        => checked((int)raw.AnimFrame + 2);

    internal void ValidateWorld(DiabloWorldState world)
    {
        if (world.World is null)
            throw new InvalidDataException("Native population requires complete backing geometry.");
        ValidateBackingLevel(world.World.Levels.Single(level => level.LevelId == LevelId));
        if (world.CombatTargets.Values.Select(value => value.LevelId)
            .Concat(world.CombatTargets.Values.Where(value => value.Drop is not null)
                .Select(value => value.Drop!.LevelId))
            .Concat(world.WorldItems.Values.Select(value => value.LevelId))
            .Concat(world.Objects.Values.Select(value => value.LevelId))
            .Concat(world.Corpses.Values.Select(value => value.LevelId))
            .Concat(world.Projectiles.Values.Select(value => value.LevelId))
            .Any(levelId => !world.AllowsNativePopulationLevel(levelId)))
            throw new InvalidDataException("The populated route cannot retain entities outside Town and its owned Cathedral level.");
        if (world.Portals.Values.Any(portal => !world.AllowsNativePopulationLevel(portal.SourceLevelId)
            || !world.AllowsNativePopulationLevel(portal.DestinationLevelId)))
            throw new InvalidDataException("The populated route cannot retain portals outside Town and its owned Cathedral level.");
        foreach (var entityId in reservedEntityIds) {
            var record = liveMonsterAdapter.Records.Single(value => value.Binding.EntityId == entityId);
            if (IsSarcophagusReservationSpawned(record.Binding.NativeSlot, entityId, world)
                || IsNativeBarrelReservationSpawned(record.Binding.NativeSlot, entityId, world))
                continue;
            if (world.CombatTargets.ContainsKey(entityId) || world.WorldItems.ContainsKey(entityId)
                || world.Objects.ContainsKey(entityId) || world.Projectiles.ContainsKey(entityId)
                || world.Portals.ContainsKey(entityId) || world.Corpses.ContainsKey(entityId))
                throw new InvalidDataException("A dormant native monster reservation was reused or activated.");
        }
        var currentObjects = world.Objects.Values.Where(value => value.LevelId == LevelId).ToArray();
        if (currentObjects.Length != objectRows.Count)
            throw new InvalidDataException("The populated level lost a retained native object.");
        foreach (var value in currentObjects)
            ValidateObject(value);
        world.ValidateNativeSarcophagusState(this);
        world.ValidateNativeBarrelState(this);
        world.ValidateNativeTrapState(this);
        if (Family == AuthoritativeNativePopulationFamily.Catacombs) {
            world.ValidateNativeCatacombsDoors(this);
            world.ValidateNativeCatacombsMonsterPhases(this);
            world.ValidateNativeCatacombsRangedActions(this);
        }
        foreach (var entry in liveInitializations) {
            if (world.CombatTargets.TryGetValue(entry.Key, out var current)) {
                if (current.LevelId != LevelId || current.NativeInitialization != entry.Value
                    || current.IsPlayerMinion
                    || !string.Equals(current.NativeAi, liveNativeAis[entry.Key], StringComparison.Ordinal))
                    throw new InvalidDataException("A generated native actor changed its owned level, initialization or classification.");
            } else if (!world.Corpses.TryGetValue(entry.Key, out var corpse) || corpse.LevelId != LevelId
                || !world.ContainsPosition(LevelId, corpse.PositionX, corpse.PositionY)) {
                throw new InvalidDataException("A generated native actor disappeared without a retained death disposition.");
            }
        }
        foreach (var target in world.CombatTargets.Values.Where(value => value.LevelId == LevelId)) {
            if (liveInitializations.TryGetValue(target.EntityId, out var expected)) {
                if (target.NativeInitialization != expected || target.IsPlayerMinion
                    || !string.Equals(target.NativeAi, liveNativeAis[target.EntityId], StringComparison.Ordinal))
                    throw new InvalidDataException("A generated native actor changed its owned initialization or classification.");
                continue;
            }
            if (target.NativeInitialization is not { } native || target.IsPlayerMinion
                || !(OwnsSarcophagusActor(target.EntityId) || OwnsNativeBarrelActor(target.EntityId))
                || !linkedPreSpawnOwnerObjectByMonsterSlot.TryGetValue(native.Binding.NativeSlot, out var ownerSlot)
                || native != GetPreSpawnReservation(ownerSlot)
                || !(IsSarcophagusReservationSpawned(native.Binding.NativeSlot, target.EntityId, world)
                    || IsNativeBarrelReservationSpawned(native.Binding.NativeSlot, target.EntityId, world)))
                throw new InvalidDataException("The populated level acquired an unsupported or unowned actor.");
        }
    }

    internal void ValidateItemProfile(AuthoritativeCathedralItemRuntimeProfile items)
    {
        if (itemBindingFingerprint is null || ComputeItemBindingFingerprint(items) != itemBindingFingerprint)
            throw new InvalidDataException("The native population profile changed its item, spell, catalog or placement bindings.");
        var level = items.LevelProfiles.SingleOrDefault(value => value.LevelId == LevelId)
            ?? throw new InvalidDataException("Native item bindings omit their owned population level.");
        foreach (var binding in level.ObjectBindings) {
            if (!objectRows.TryGetValue(binding.EntityId, out var raw))
                throw new InvalidDataException("A native item object binding has no population row.");
            var expectedPolicy = raw.DoorFlag
                ? Family == AuthoritativeNativePopulationFamily.Catacombs
                    ? AuthoritativeCathedralItemObjectMutationPolicy.CatacombsDoor
                    : AuthoritativeCathedralItemObjectMutationPolicy.CathedralDoor
                : IsSupportedNativeChest(raw)
                    ? AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                    : IsSupportedNativeSarcophagus(InitialState, raw)
                        ? AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus
                        : IsSupportedNativeBarrel(InitialState, raw)
                            ? AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel
                        : AuthoritativeCathedralItemObjectMutationPolicy.ExplicitlyRefused;
            if (binding.NativeObjectSlot != raw.Slot || binding.ObjectType != raw.Type
                || binding.PositionX != raw.Position.X || binding.PositionY != raw.Position.Y
                || binding.InitialSolid != raw.SolidFlag || binding.InitialSelectionRegion != raw.SelectionRegion
                || binding.MutationPolicy != expectedPolicy)
                throw new InvalidDataException("Native item placement object policy disagrees with its exact generated population row.");
        }
        if (level.ObjectBindings.Count != objectRows.Count)
            throw new InvalidDataException("Native item placement must bind every generated object exactly once.");
    }

    private static string ComputeItemBindingFingerprint(AuthoritativeCathedralItemRuntimeProfile items)
    {
        var identity = new {
            items.MetadataFingerprint,
            InitialCheckpoint = items.InitialCheckpoint with {
                GenerationFingerprint = string.Empty,
                UniqueItemFlags = items.InitialCheckpoint.UniqueItemFlags.Order().ToArray(),
                Levels = items.InitialCheckpoint.Levels.Select(level => level with { GenerationIdentity = null }).ToArray(),
            },
            ItemCatalog = AuthoritativeCathedralThemeItemMetadata.CreateTables(items.ItemCatalog)
                .OrderBy(table => table.SourcePath, StringComparer.Ordinal)
                .Select(table => new { table.SourcePath, table.Columns,
                    Rows = table.Rows.Select(row => table.Columns.Select(column => row[column]).ToArray()).ToArray(),
                }).ToArray(),
            items.ItemPolicy,
            MonsterCatalogFingerprint = items.MonsterCatalog.Fingerprint,
            UniqueDefinitions = items.UniqueDefinitions.OrderBy(row => row.NativeUniqueId).ToArray(),
            ItemBindings = items.ItemBindings.OrderBy(row => row.Key).ToArray(),
            items.ContentInventoryDimensions,
            LevelProfiles = items.LevelProfiles.OrderBy(row => row.LevelId)
                .Select(level => level with {
                    // NativeChest only adds an interaction for objects that the old
                    // generation profile already retained as explicitly refused.
                    // Normalize that one capability-only distinction so closed v5
                    // checkpoints keep their established generation fingerprint.
                    ObjectBindings = level.ObjectBindings.Select(binding =>
                        binding.MutationPolicy is AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                            or AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus
                            or AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel
                            ? binding with {
                                MutationPolicy = AuthoritativeCathedralItemObjectMutationPolicy.ExplicitlyRefused,
                            }
                            : binding)
                        .OrderBy(row => row.NativeObjectSlot).ToArray(),
                }).ToArray(),
            NativeSpellBindings = items.NativeSpellBindings?.OrderBy(row => row.Key).ToArray(),
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
    }

    private static bool IsDormantGolemReservation(
        AuthoritativeCathedralMonsterRuntimeRecord row, AuthoritativeCathedralThemeRoomState state)
    {
        var raw = row.InitialState;
        var reference = raw.Slot + 1;
        return row.UnsupportedReason == AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DormantGolem
            && raw.Slot is >= 0 and <= 3
            && raw.Type == AuthoritativeCathedralPopulationParameters.GolemNativeType
            && raw.Ai == 21 && raw.Mode == 0 && raw.Goal == 1
            && raw.GoalVar1 == 0 && raw.GoalVar2 == 0 && raw.GoalVar3 == 0
            && raw.Tile == new AuthoritativeCathedralPopulationPoint(1, 0)
            && raw.Future == raw.Tile && raw.HitPoints == 64 && raw.MaxHitPoints == 64
            && raw.UniqueType == 255 && !row.Initialization.InitialAnimationPetrified
            && !state.MonsterRefs.Any(value => value == reference || value == -reference);
    }
}
