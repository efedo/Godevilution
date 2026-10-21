using System.Text.Json;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    /** First-entry preparation never mutates an already-running native world. */
    internal sealed record NativeCatacombsFloorAdmission(
        DiabloWorldState Floor,
        AuthoritativeCatacombsNativePopulationBuild Build,
        AuthoritativeCathedralItemRuntime Items,
        string ExpectedWorld,
        string ExpectedFloor,
        string ExpectedItems,
        IReadOnlyList<AuthoritativePlayerState> Participants,
        string ExpectedParticipants);

    internal static DiabloWorldState CreateNativeCatacombsFloor(
        AuthoritativeCatacombsNativePopulationBuild build,
        AuthoritativeItemCatalog items,
        AuthoritativeSpellCatalog spells,
        AuthoritativeMonsterMissileCatalog missiles,
        IReadOnlyList<AuthoritativeNativeCatacombsRangedAction>? rangedActions = null)
        => CreateNativeCatacombsFloorCore(build, items, spells, missiles, null, null, [], rangedActions);

    private static DiabloWorldState CreateNativeCatacombsFloorCore(
        AuthoritativeCatacombsNativePopulationBuild build,
        AuthoritativeItemCatalog items,
        AuthoritativeSpellCatalog spells,
        AuthoritativeMonsterMissileCatalog missiles,
        AuthoritativeNativeTownCatalog? town,
        DiabloNativeTownCheckpoint? townState,
        IEnumerable<AuthoritativeLevel> townLevels,
        IReadOnlyList<AuthoritativeNativeCatacombsRangedAction>? rangedActions = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(missiles);
        missiles.ValidateSpellReferences(spells);
        var owner = build.Policy.Bindings;
        var backing = new AuthoritativeWorld();
        foreach (var level in townLevels)
            backing.AddLevel(level);
        backing.AddLevel(owner.CreateBackingLevel());
        var floor = new DiabloWorldState(town, townState, backing,
            combatTargets: build.Monsters.SharedRuntime.Records.Where(row => row.Target is not null)
                .Select(row => row.Target!).ToArray(), objects: build.Objects,
            deferCohortInitialization: true) {
            nativePopulationPolicy = owner,
            nativePopulationParticipantClassId = build.Policy.CharacterClassId,
        };
        floor.deferCohortInitialization = false;
        floor.RebuildProjectedGeometry();
        floor.ConfigureNativeCatacombsDoors(owner, build.DoorCatalog);
        floor.ConfigureNativeCatacombsMonsterPhases(build.Monsters);
        floor.ConfigureNativeCatacombsRangedActions(owner, missiles, rangedActions);
        floor.ConfigureNativePopulationBindings(owner, build.Items, build.MonsterMetadataFingerprint);
        floor.ConfigureNativeFamilyItems(build.Items, null, [owner], items, spells);
        floor.ValidateNativePopulationState([]);
        return floor;
    }

    internal NativeCatacombsFloorAdmission PrepareNativeCatacombsFloorAdmission(
        AuthoritativeCatacombsNativePopulationBuild build,
        AuthoritativeItemCatalog items,
        AuthoritativeSpellCatalog spells,
        AuthoritativeMonsterMissileCatalog missiles,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(participants);
        var owner = build.Policy.Bindings;
        var levelId = owner.LevelId;
        if (World is null || cathedralItems is null || !HasNativePopulation
            || nativePopulationParticipantClassId != build.Policy.CharacterClassId
            || NativePopulationForLevel(levelId) is not null || !World.ContainsLevel(levelId)
            || participants.Count != 1 || participants[0].CharacterClassId != build.Policy.CharacterClassId)
            throw new InvalidOperationException("A new Catacombs floor requires an existing class-bound run and one participant.");
        var player = participants[0];
        var hero = build.Ready.Request.FreshLevel.Request.Hero;
        if (player.Life <= 0 || player.Life != build.Ready.Player.LifeCurrentQ6
            || player.BaseLifeMaximum != hero.MaximumLifeBase
            || player.BaseManaMaximum != hero.MaximumManaBase || player.LightRadius != hero.LightRadius
            || player.FuturePositionX != hero.PreEntryFuture.X || player.FuturePositionY != hero.PreEntryFuture.Y)
            throw new InvalidDataException("The prepared Catacombs hero no longer matches the entering participant.");
        foreach (var item in player.Inventory.Select(value => value.State)
                .Concat(player.Equipment.Select(value => value.State))
                .Concat(player.Belt.Select(value => value.State))
                .Concat(player.HeldItem is { } held ? [held.State] : [])) {
            if (!build.Items.ItemBindings.TryGetValue(item.ItemIndex, out var binding)
                || binding.StableItemId != item.StableItemId)
                throw new InvalidDataException("A Catacombs entry cannot admit an unbound carried item.");
        }
        ValidateNativePopulationState(participants);
        if (HasEntitiesOnNativeAdmissionLevel(levelId))
            throw new InvalidDataException("A first Catacombs entry cannot replace existing floor entities.");
        if (nativeCatacombsDoorCatalogFingerprint is not null
            && nativeCatacombsDoorCatalogFingerprint != build.DoorCatalog.Fingerprint)
            throw new InvalidDataException("A Catacombs run cannot change its door source catalog.");
        ArgumentNullException.ThrowIfNull(missiles);
        var missileBindings = missiles.Definitions
            .Select(AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint).Order().ToArray();
        if (nativeCatacombsRangedMissileCatalogs.Values.Any(catalog => !catalog.Definitions
                .Select(AuthoritativeNativeCatacombsRangedAction.ComputeMissileMappingFingerprint).Order()
                .SequenceEqual(missileBindings)))
            throw new InvalidDataException("A Catacombs run cannot change its trusted ranged mappings.");
        var floor = CreateNativeCatacombsFloorCore(build, items, spells, missiles, nativeTownCatalog,
            CaptureNativeTown(), World.Levels.Where(level => level.LevelId == 0));
        var identities = owner.BoundEntityIds.Concat(floor.WorldItems.Keys).ToArray();
        if (identities.Distinct().Count() != identities.Length
            || identities.Any(id => IsEntityIdOccupied(id) || participants.Any(value => value.EntityId == id)))
            throw new InvalidDataException("A Catacombs floor reused an entity or dormant reservation.");
        var current = CaptureCathedralItems()!;
        if (!current.UniqueItemFlags.ToHashSet().SetEquals(build.Ready.Request.FreshLevel.Request.InitialUniqueItemFlags))
            throw new InvalidDataException("A Catacombs floor was generated without the run's complete prior unique history.");
        var old = cathedralItems.Profile;
        if (old.MetadataFingerprint != build.Items.MetadataFingerprint
            || old.MonsterCatalog.Fingerprint != build.Items.MonsterCatalog.Fingerprint
            || !old.UniqueDefinitions.SequenceEqual(build.Items.UniqueDefinitions)
            || !old.NativeSpellBindings!.OrderBy(pair => pair.Key)
                .SequenceEqual(build.Items.NativeSpellBindings!.OrderBy(pair => pair.Key)))
            throw new InvalidDataException("Native floors require the same validated item, spell and actor catalogs.");
        var bindings = old.ItemBindings.ToDictionary();
        foreach (var entry in build.Items.ItemBindings) {
            if (bindings.TryGetValue(entry.Key, out var previous) && previous != entry.Value)
                throw new InvalidDataException("A Catacombs floor changed an existing stable item binding.");
            bindings[entry.Key] = entry.Value;
        }
        var owners = NativePopulationOwners.Append(owner).OrderBy(value => value.LevelId).ToArray();
        var fingerprint = AuthoritativeNativePopulationBindings.ComputeFamilyItemGenerationFingerprint(owners);
        var initial = old.InitialCheckpoint with {
            GenerationFingerprint = fingerprint,
            UniqueItemFlags = old.InitialCheckpoint.UniqueItemFlags
                .Union(build.Items.InitialCheckpoint.UniqueItemFlags).Order().ToArray(),
            Levels = old.InitialCheckpoint.Levels.Concat(build.Items.InitialCheckpoint.Levels)
                .OrderBy(value => value.LevelId).ToArray(),
        };
        var profile = old with {
            GenerationFingerprint = fingerprint, InitialCheckpoint = initial, ItemBindings = bindings,
            LevelProfiles = old.LevelProfiles.Concat(build.Items.LevelProfiles).OrderBy(value => value.LevelId).ToArray(),
        };
        var checkpoint = current with {
            GenerationFingerprint = fingerprint,
            UniqueItemFlags = current.UniqueItemFlags.Union(build.Items.InitialCheckpoint.UniqueItemFlags).Order().ToArray(),
            Levels = current.Levels.Concat(build.Items.InitialCheckpoint.Levels).OrderBy(value => value.LevelId).ToArray(),
        };
        var combined = new AuthoritativeCathedralItemRuntime(profile, checkpoint, items.NativeGoldStackMetadata,
            owners.ToDictionary(value => value.LevelId, value => value.ItemFamilyContext));
        return new(floor, build, combined, CaptureNativeFamilyAdmissionState(this),
            CaptureNativeFamilyAdmissionState(floor), CaptureNativeAdmissionItems(combined),
            participants.ToArray(), CaptureNativeAdmissionParticipants(participants));
    }

    /** Rechecks every mutable owner and destination map before the first live write. */
    internal void CommitNativeCatacombsFloorAdmission(NativeCatacombsFloorAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        var floor = admission.Floor;
        var owner = admission.Build.Policy.Bindings;
        var levelId = owner.LevelId;
        var liveIds = floor.CombatTargets.Keys.Concat(floor.Objects.Keys).Concat(floor.WorldItems.Keys)
            .Concat(floor.Corpses.Keys).Concat(floor.Projectiles.Keys).Concat(floor.Portals.Keys).ToArray();
        if (World is null || !World.ContainsLevel(levelId) || NativePopulationForLevel(levelId) is not null
            || liveIds.Distinct().Count() != liveIds.Length || HasEntitiesOnNativeAdmissionLevel(levelId)
            || liveIds.Concat(owner.BoundEntityIds).Any(id => IsEntityIdOccupied(id)
                || admission.Participants.Any(player => player.EntityId == id))
            || CaptureNativeAdmissionParticipants(admission.Participants) != admission.ExpectedParticipants
            || CaptureNativeFamilyAdmissionState(this) != admission.ExpectedWorld
            || CaptureNativeFamilyAdmissionState(floor) != admission.ExpectedFloor
            || CaptureNativeAdmissionItems(admission.Items) != admission.ExpectedItems
            || nativeCatacombsDoorCatalogFingerprint is not null
                && nativeCatacombsDoorCatalogFingerprint != floor.nativeCatacombsDoorCatalogFingerprint)
            throw new InvalidOperationException("A prepared Catacombs floor is stale.");
        RequireNativeAdmissionKeys(nativePopulationPolicies, floor.nativePopulationPolicies);
        RequireNativeAdmissionKeys(nativeSarcophagiByEntityId, floor.nativeSarcophagiByEntityId);
        RequireNativeAdmissionKeys(nativeBarrelsByEntityId, floor.nativeBarrelsByEntityId);
        RequireNativeAdmissionKeys(nativeTrapsByEntityId, floor.nativeTrapsByEntityId);
        RequireNativeAdmissionKeys(nativeInitialSpecialStand, floor.nativeInitialSpecialStand);
        RequireNativeAdmissionKeys(nativeCatacombsDoorOwners, floor.nativeCatacombsDoorOwners);
        RequireNativeAdmissionKeys(nativeCatacombsDoorLevels, floor.nativeCatacombsDoorLevels);
        RequireNativeAdmissionKeys(nativeCatacombsDoorsByEntityId, floor.nativeCatacombsDoorsByEntityId);
        RequireNativeAdmissionKeys(nativeCatacombsMonsterPhases, floor.nativeCatacombsMonsterPhases);
        RequireNativeAdmissionKeys(nativeCatacombsMonsterPhaseOrders, floor.nativeCatacombsMonsterPhaseOrders);
        RequireNativeAdmissionKeys(nativeCatacombsMonsterPhaseBindings, floor.nativeCatacombsMonsterPhaseBindings);
        RequireNativeAdmissionKeys(nativeCatacombsRangedActions, floor.nativeCatacombsRangedActions);
        RequireNativeAdmissionKeys(nativeCatacombsArrows, floor.nativeCatacombsArrows);
        RequireNativeAdmissionKeys(nativeCatacombsRangedMissileCatalogs, floor.nativeCatacombsRangedMissileCatalogs);
        if (nativeCatacombsRangedConfiguredLevels.Overlaps(floor.nativeCatacombsRangedConfiguredLevels))
            throw new InvalidOperationException("A prepared Catacombs ranged floor is already registered.");
        if (nativeCatacombsMonsterPhaseLevels.Overlaps(floor.nativeCatacombsMonsterPhaseLevels))
            throw new InvalidOperationException("A prepared Catacombs phase floor is already registered.");
        var replacement = owner.CreateBackingLevel();
        var geometry = new AuthoritativeWorld();
        foreach (var level in World.Levels)
            geometry.AddLevel(level);
        geometry.ReplaceLevel(replacement);
        // All throwing validation is complete. Only validated dictionary copies follow.
        World.ReplaceLevel(replacement);
        projectedLevels[levelId] = floor.projectedLevels[levelId];
        CopyNativeAdmissionMap(CombatTargets, floor.CombatTargets);
        CopyNativeAdmissionMap(Objects, floor.Objects);
        CopyNativeAdmissionMap(WorldItems, floor.WorldItems);
        CopyNativeAdmissionMap(nativePopulationPolicies, floor.nativePopulationPolicies);
        CopyNativeAdmissionMap(nativeSarcophagiByEntityId, floor.nativeSarcophagiByEntityId);
        CopyNativeAdmissionMap(nativeBarrelsByEntityId, floor.nativeBarrelsByEntityId);
        CopyNativeAdmissionMap(nativeTrapsByEntityId, floor.nativeTrapsByEntityId);
        CopyNativeAdmissionMap(nativeInitialSpecialStand, floor.nativeInitialSpecialStand);
        CopyNativeAdmissionMap(nativeCatacombsDoorOwners, floor.nativeCatacombsDoorOwners);
        CopyNativeAdmissionMap(nativeCatacombsDoorLevels, floor.nativeCatacombsDoorLevels);
        CopyNativeAdmissionMap(nativeCatacombsDoorsByEntityId, floor.nativeCatacombsDoorsByEntityId);
        CopyNativeAdmissionMap(nativeCatacombsMonsterPhases, floor.nativeCatacombsMonsterPhases);
        CopyNativeAdmissionMap(nativeCatacombsMonsterPhaseOrders, floor.nativeCatacombsMonsterPhaseOrders);
        CopyNativeAdmissionMap(nativeCatacombsMonsterPhaseBindings, floor.nativeCatacombsMonsterPhaseBindings);
        nativeCatacombsMonsterPhaseLevels.UnionWith(floor.nativeCatacombsMonsterPhaseLevels);
        CopyNativeAdmissionMap(nativeCatacombsRangedActions, floor.nativeCatacombsRangedActions);
        CopyNativeAdmissionMap(nativeCatacombsArrows, floor.nativeCatacombsArrows);
        CopyNativeAdmissionMap(nativeCatacombsRangedMissileCatalogs, floor.nativeCatacombsRangedMissileCatalogs);
        nativeCatacombsRangedConfiguredLevels.UnionWith(floor.nativeCatacombsRangedConfiguredLevels);
        nativeCatacombsDoorCatalog ??= floor.nativeCatacombsDoorCatalog;
        nativeCatacombsDoorCatalogFingerprint ??= floor.nativeCatacombsDoorCatalogFingerprint;
        cathedralItems = admission.Items;
    }

    private bool HasEntitiesOnNativeAdmissionLevel(uint levelId)
        => CombatTargets.Values.Any(value => value.LevelId == levelId)
            || Objects.Values.Any(value => value.LevelId == levelId)
            || WorldItems.Values.Any(value => value.LevelId == levelId)
            || Corpses.Values.Any(value => value.LevelId == levelId)
            || Projectiles.Values.Any(value => value.LevelId == levelId)
            || Portals.Values.Any(value => value.SourceLevelId == levelId || value.DestinationLevelId == levelId);

    private static void RequireNativeAdmissionKeys<T>(Dictionary<uint, T> current, Dictionary<uint, T> incoming)
    {
        if (incoming.Keys.Any(current.ContainsKey))
            throw new InvalidOperationException("A prepared Catacombs owner map collides with live state.");
    }

    private static void CopyNativeAdmissionMap<T>(Dictionary<uint, T> destination, Dictionary<uint, T> source)
    {
        foreach (var entry in source)
            destination.Add(entry.Key, entry.Value);
    }

    private static string CaptureNativeAdmissionItems(AuthoritativeCathedralItemRuntime items)
        => JsonSerializer.Serialize(new { items.Profile, Checkpoint = items.CaptureCheckpoint() });

    private static string CaptureNativeAdmissionParticipants(IReadOnlyList<AuthoritativePlayerState> participants)
        => JsonSerializer.Serialize(participants);

    private static string CaptureNativeFamilyAdmissionState(DiabloWorldState floor)
    {
        floor.ValidateNativePopulationState([]);
        return AuthoritativeCathedralNativeProgression.ComputeFingerprint(new {
            Backing = floor.World!.Levels.OrderBy(level => level.LevelId).Select(level => new {
                State = CaptureCohortLevel(level), Special = level.NativeSpecialLayer,
            }).ToArray(),
            Effective = floor.projectedLevels.OrderBy(entry => entry.Key).Select(entry => new {
                State = CaptureCohortLevel(entry.Value), Special = entry.Value.NativeSpecialLayer,
            }).ToArray(),
            Owners = floor.NativePopulationOwners.OrderBy(owner => owner.LevelId)
                .Select(owner => new { owner.Family, owner.LevelId, owner.Fingerprint }).ToArray(),
            Town = floor.CaptureNativeTown(), C1Doors = floor.CaptureCathedralDoorObjects(),
            C2Doors = floor.CaptureNativeCatacombsDoorStates(),
            Phases = floor.CaptureNativeCatacombsMonsterPhases(),
            Ranged = floor.CaptureNativeCatacombsRangedActions(),
            Arrows = floor.CaptureNativeCatacombsArrows(),
            RangedLevels = floor.nativeCatacombsRangedConfiguredLevels.Order().ToArray(),
            RangedMappings = floor.nativeCatacombsRangedMissileCatalogs.OrderBy(row => row.Key)
                .Select(row => new { LevelId = row.Key, Definitions = row.Value.Definitions
                    .OrderBy(value => value.NativeAi, StringComparer.Ordinal).ThenBy(value => value.MonsterId).ToArray() }).ToArray(),
            Actors = floor.CombatTargets.OrderBy(entry => entry.Key).Select(entry => new {
                entry.Key, State = entry.Value.CaptureCohortState(), entry.Value.NativeInitialization,
            }).ToArray(),
            Objects = floor.Objects.OrderBy(entry => entry.Key).Select(entry => DiabloSimulation.CaptureObject(entry.Value)).ToArray(),
            Items = floor.CaptureCathedralItems(), ItemProfile = floor.cathedralItems?.Profile,
            Sarcophagi = floor.CaptureNativeSarcophagi(), Barrels = floor.CaptureNativeBarrels(),
            Traps = floor.CaptureNativeTraps(), Emergence = floor.CaptureNativeInitialSpecialStand(),
            Projectiles = floor.Projectiles.OrderBy(entry => entry.Key).ToArray(),
            Corpses = floor.Corpses.OrderBy(entry => entry.Key).ToArray(),
            Portals = floor.Portals.OrderBy(entry => entry.Key).ToArray(),
            Quests = floor.Quests.OrderBy(entry => entry.Key).ToArray(),
        });
    }
}
