using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    /** Detached admission: all source, pool, identity and projection checks precede publication. */
    internal sealed record NativeFloorAdmission(
        DiabloWorldState Floor,
        AuthoritativeCathedralNativePopulationBuild Build,
        AuthoritativeCathedralDoorCatalog Doors,
        AuthoritativeCathedralItemRuntime Items,
        string? ExpectedItemCheckpoint,
        string? ExpectedFloorState,
        string? ExpectedCombinedItems,
        IReadOnlyList<AuthoritativePlayerState> Participants);

    internal NativeFloorAdmission PrepareNativeFloorAdmission(
        AuthoritativeCathedralNativePopulationBuild build,
        AuthoritativeCathedralDoorDefinition door,
        AuthoritativeItemCatalog items,
        AuthoritativeSpellCatalog spells,
        IReadOnlyList<AuthoritativePlayerState> participants,
        bool verifyAtCommit = true)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (World is null || cathedralItems is null || cathedralDoorCatalog is null
            || NativePopulationForLevel(build.Policy.LevelId) is not null
            || door.LevelId != build.Policy.LevelId
            || !World.ContainsLevel(build.Policy.LevelId))
            throw new InvalidOperationException("A new native floor requires an existing run and an unvisited configured depth.");
        var levelId = build.Policy.LevelId;
        if (CombatTargets.Values.Any(value => value.LevelId == levelId)
            || Objects.Values.Any(value => value.LevelId == levelId)
            || WorldItems.Values.Any(value => value.LevelId == levelId)
            || Corpses.Values.Any(value => value.LevelId == levelId)
            || Projectiles.Values.Any(value => value.LevelId == levelId))
            throw new InvalidDataException("A first native entry cannot replace existing floor entities.");
        var backing = new AuthoritativeWorld();
        foreach (var level in World.Levels.Where(value => value.LevelId == 0))
            backing.AddLevel(level);
        backing.AddLevel(build.Policy.CreateBackingLevel());
        var floor = new DiabloWorldState(nativeTownCatalog, CaptureNativeTown(), backing,
            combatTargets: build.Monsters.Records.Where(row => row.Target is not null)
                .Select(row => row.Target!).ToArray(), objects: build.Objects,
            deferCohortInitialization: true,
            cathedralDoors: new AuthoritativeCathedralDoorCatalog([door])) {
            nativePopulationPolicy = build.Policy.Bindings,
            nativePopulationParticipantClassId = nativePopulationParticipantClassId,
            nativePopulationMultiplayer = nativePopulationMultiplayer,
        };
        floor.deferCohortInitialization = false;
        floor.RebuildProjectedGeometry();
        floor.InitializeNativePopulationDoors();
        floor.ConfigureNativePopulationPolicy(build.Policy, build.Items, build.MonsterMetadataFingerprint);
        floor.ConfigureCathedralItems(build.Items, null, build.MonsterMetadataFingerprint, items, spells);
        var ids = build.Policy.BoundEntityIds.Concat(floor.WorldItems.Keys).ToArray();
        if (ids.Distinct().Count() != ids.Length
            || ids.Any(id => IsEntityIdOccupied(id) || participants.Any(player => player.EntityId == id)))
            throw new InvalidDataException("A new native floor reuses an actor, object, reservation, item or participant identity.");
        var current = CaptureCathedralItems()!;
        if (!current.UniqueItemFlags.All(build.Generated.Population.State.UniqueItemFlags.Contains))
            throw new InvalidDataException("A new floor discarded the run's unique item history.");
        var oldProfile = cathedralItems.Profile;
        if (oldProfile.MetadataFingerprint != build.Items.MetadataFingerprint
            || oldProfile.MonsterCatalog.Fingerprint != build.Items.MonsterCatalog.Fingerprint)
            throw new InvalidDataException("All native floors must use the same validated item and actor catalogs.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            NativePopulationOwners.Append(build.Policy.Bindings).OrderBy(policy => policy.LevelId)
                .Select(policy => new { policy.LevelId, policy.Fingerprint }).ToArray())));
        var (profile, checkpoint) = CombineFloorItems(oldProfile, current, build.Items, fingerprint);
        var combined = new AuthoritativeCathedralItemRuntime(profile, checkpoint, items.NativeGoldStackMetadata);
        var doors = new AuthoritativeCathedralDoorCatalog(cathedralDoorCatalog.Definitions.Append(door));
        if (!verifyAtCommit) {
            floor.ValidateNativePopulationState([]);
            return new(floor, build, doors, combined, null, null, null, participants.ToArray());
        }
        return new(floor, build, doors, combined,
            JsonSerializer.Serialize(current), CaptureNativeAdmissionState(floor),
            JsonSerializer.Serialize(combined.CaptureCheckpoint()), participants.ToArray());
    }

    /** Adds a new floor's item bindings, unique history and level rows to the run's item registry. */
    private static (AuthoritativeCathedralItemRuntimeProfile Profile, DiabloCathedralItemCheckpoint Checkpoint) CombineFloorItems(
        AuthoritativeCathedralItemRuntimeProfile old, DiabloCathedralItemCheckpoint current,
        AuthoritativeCathedralItemRuntimeProfile floor, string fingerprint)
    {
        var bindings = old.ItemBindings.ToDictionary();
        foreach (var entry in floor.ItemBindings) {
            if (bindings.TryGetValue(entry.Key, out var previous) && previous != entry.Value)
                throw new InvalidDataException("A new floor changed an existing stable item binding.");
            bindings[entry.Key] = entry.Value;
        }
        var added = floor.InitialCheckpoint;
        var initial = old.InitialCheckpoint with {
            GenerationFingerprint = fingerprint,
            UniqueItemFlags = old.InitialCheckpoint.UniqueItemFlags.Union(added.UniqueItemFlags).Order().ToArray(),
            Levels = old.InitialCheckpoint.Levels.Concat(added.Levels).OrderBy(level => level.LevelId).ToArray(),
        };
        return (old with {
            GenerationFingerprint = fingerprint, InitialCheckpoint = initial, ItemBindings = bindings,
            LevelProfiles = old.LevelProfiles.Concat(floor.LevelProfiles).OrderBy(level => level.LevelId).ToArray(),
        }, current with {
            GenerationFingerprint = fingerprint,
            UniqueItemFlags = current.UniqueItemFlags.Union(added.UniqueItemFlags).Order().ToArray(),
            Levels = current.Levels.Concat(added.Levels).OrderBy(level => level.LevelId).ToArray(),
        });
    }

    private static string CaptureNativeAdmissionState(DiabloWorldState floor)
    {
        floor.ValidateNativePopulationState([]);
        return AuthoritativeCathedralNativeProgression.ComputeFingerprint(new {
            Geometry = floor.CaptureCohortInfrastructure(),
            Doors = floor.CaptureCathedralDoors(),
            Actors = floor.CombatTargets.OrderBy(entry => entry.Key).Select(entry => new {
                entry.Key, State = entry.Value.CaptureCohortState(), entry.Value.NativeInitialization,
            }).ToArray(),
            Objects = floor.Objects.OrderBy(entry => entry.Key).Select(entry => new {
                entry.Key, State = DiabloSimulation.CaptureObject(entry.Value),
            }).ToArray(),
            Items = floor.CaptureCathedralItems(),
            Sarcophagi = floor.CaptureNativeSarcophagi(), Barrels = floor.CaptureNativeBarrels(),
            Traps = floor.CaptureNativeTraps(), Emergence = floor.CaptureNativeInitialSpecialStand(),
            floor.Projectiles.Count, CorpseCount = floor.Corpses.Count, PortalCount = floor.Portals.Count,
        });
    }

    internal void CommitNativeFloorAdmission(NativeFloorAdmission admission)
    {
        var floor = admission.Floor;
        var policy = admission.Build.Policy;
        // Recheck the publication boundary before any write, including item/history changes.
        var actualIds = floor.CombatTargets.Keys.Concat(floor.Objects.Keys).Concat(floor.WorldItems.Keys)
            .Concat(floor.Projectiles.Keys).Concat(floor.Corpses.Keys).Concat(floor.Portals.Keys).ToArray();
        if (NativePopulationForLevel(policy.LevelId) is not null
            || actualIds.Distinct().Count() != actualIds.Length
            || actualIds.Concat(policy.BoundEntityIds).Any(id => IsEntityIdOccupied(id)
                || admission.Participants.Any(player => player.EntityId == id))
            || CombatTargets.Values.Any(value => value.LevelId == policy.LevelId)
            || Objects.Values.Any(value => value.LevelId == policy.LevelId)
            || WorldItems.Values.Any(value => value.LevelId == policy.LevelId)
            || Corpses.Values.Any(value => value.LevelId == policy.LevelId)
            || Projectiles.Values.Any(value => value.LevelId == policy.LevelId)
            || admission.ExpectedFloorState is not null
                && (CaptureNativeAdmissionState(floor) != admission.ExpectedFloorState
                    || JsonSerializer.Serialize(admission.Items.CaptureCheckpoint()) != admission.ExpectedCombinedItems
                    || JsonSerializer.Serialize(CaptureCathedralItems()) != admission.ExpectedItemCheckpoint))
            throw new InvalidOperationException("A prepared native floor is stale.");
        RequireAdoptableLevelStates(floor);
        AdoptFloorEntities(floor, apply: false);
        World!.ReplaceLevel(policy.CreateBackingLevel());
        projectedLevels[policy.LevelId] = floor.projectedLevels[policy.LevelId];
        AdoptFloorEntities(floor, apply: true);
        AdoptLevelStates(floor);
        cathedralItems = admission.Items;
        cathedralDoorCatalog = admission.Doors;
        trustedCathedralDoorBackingGeometry = World.CaptureGeometryState();
        PreserveCathedralDoorProjection();
    }
}
