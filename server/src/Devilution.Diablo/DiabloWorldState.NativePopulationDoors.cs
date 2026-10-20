namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    /** Keeps the public legacy constructors and their scanner path unchanged. */
    internal DiabloWorldState(DiabloSimulationOptions options)
        : this(options.NativeTown, options.NativeTownRestore, options.World, options.FallbackWidth, options.FallbackHeight,
            options.FallbackBlockedCells, options.CombatTargets, options.Portals,
            options.WorldItems, options.Objects, options.Quests, options.Projectiles,
            options.FallbackMissileBlockedCells, options.Corpses, options.WorldMutations,
            nativeDunObjects: options.NativeDunObjects, nativeObjectTemplates: options.NativeObjectTemplates,
            allowMissingNativeDunLayers: options.NativeDunFragments is not null,
            deferCohortInitialization: true, cathedralDoors: options.CathedralDoors,
            cathedralDoorBackingGeometry: options.CathedralDoorBackingGeometry)
    {
        nativePopulationPolicy = options.NativePopulationPolicy
            ?? throw new InvalidDataException("Direct native population construction requires its immutable owner.");
        if (options.GameMode != DiabloGameMode.SinglePlayer || options.Difficulty != DiabloDifficulty.Normal
            || options.CombatRules?.GameVariant != DiabloGameVariant.Diablo
            || options.NativeCathedralItems is null
            || options.NativeProgressionRestore is null
                && options.NativeCathedralItems.GenerationFingerprint != nativePopulationPolicy.Fingerprint
            || options.NativeMonsterMetadataFingerprint != nativePopulationPolicy.MonsterMetadataFingerprint
            || cathedralDoorCatalog is null
            || options.NativeProgressionRestore is null && (cathedralDoorCatalog.Definitions.Count != 1
                || cathedralDoorCatalog.Definitions[0].LevelId != nativePopulationPolicy.LevelId))
            throw new InvalidDataException("Direct native population requires the exact single-player L1 item, actor and door profile.");
        if (options.NativeProgressionRestore is { } restored) {
            if (options.NativeProgression is null
                || !restored.Owners.Select(owner => owner.LevelId).SequenceEqual(
                    cathedralDoorCatalog.Definitions.Select(door => door.LevelId))
                || options.NativeCathedralItems.GenerationFingerprint != restored.Items.GenerationFingerprint
                || restored.Owners.Any(owner => owner.MonsterMetadataFingerprint != options.NativeMonsterMetadataFingerprint))
                throw new InvalidDataException("Native restored floor owners do not match the explicit run and item registry.");
            foreach (var owner in restored.Owners)
                nativePopulationPolicies.Add(owner.LevelId, owner);
        } else {
            nativePopulationPolicy.ValidateItemProfile(options.NativeCathedralItems);
        }
        var participantClassId = options.NativePopulationClassId
            ?? options.PlayerClasses?.Definitions.SingleOrDefault(
                definition => string.Equals(definition.Symbol, "rogue", StringComparison.Ordinal))?.ClassId
            ?? throw new InvalidDataException("Native population requires its canonical participant class.");
        var participantClass = options.PlayerClasses?.GetRequired(participantClassId)
            ?? throw new InvalidDataException("Native population requires its active participant class catalog.");
        if (participantClass.Availability != DiabloPlayerClassAvailability.BaseGame
            || participantClass.ClassId is not (0U or 1U or 2U))
            throw new InvalidDataException("Native population supports only the canonical base-game classes.");
        nativePopulationParticipantClassId = participantClass.ClassId;
        if (World is null)
            throw new InvalidDataException("Native population requires its complete backing world.");
        foreach (var owner in NativePopulationOwners) {
            var nativeBacking = World.Levels.Single(level => level.LevelId == owner.LevelId);
            owner.ValidateBackingLevel(nativeBacking, allowOmittedSpecial: options.DeferCohortInitialization);
            if (options.DeferCohortInitialization)
                World.ReplaceLevel(AuthoritativeCathedralDoors.CopyLevel(nativeBacking,
                    special: owner.InitialState.Special.Select(number => (ushort)number)));
        }
        if (!options.DeferCohortInitialization) {
            deferCohortInitialization = false;
            RebuildProjectedGeometry();
            InitializeNativePopulationDoors();
        }
    }

    private void InitializeNativePopulationDoors()
    {
        var policy = nativePopulationPolicy!;
        if (World is null || !projectedLevels.TryGetValue(policy.LevelId, out var level))
            throw new InvalidDataException("Native doors require their fully generated backing level.");
        var prepared = new Dictionary<uint, (uint LevelId, AuthoritativeCathedralDoorState Door)>();
        var positions = new HashSet<(int X, int Y)>();
        var definition = cathedralDoorCatalog!.Definitions.Single();
        foreach (var value in Objects.Values.Where(value => value.LevelId == policy.LevelId && value.CathedralDoor is not null)) {
            var expected = policy.GetInitialDoor(value.EntityId);
            var objectId = expected.Kind == AuthoritativeCathedralDoorKind.Left
                ? definition.LeftObjectId : definition.RightObjectId;
            if (value.CathedralDoor != expected || value.ObjectId != objectId
                || !positions.Add((expected.PositionX, expected.PositionY)))
                throw new InvalidDataException("Native doors must use their exact raw caches, flags and identity bindings.");
            AuthoritativeCathedralDoors.Validate(level, expected);
            policy.ValidateObject(value);
            prepared.Add(value.EntityId, (policy.LevelId, expected));
        }
        if (prepared.Count != policy.InitialDoorCount)
            throw new InvalidDataException("The populated level omitted a generated native door.");
        foreach (var entry in prepared)
            cathedralDoors.Add(entry.Key, entry.Value);
        cathedralDoorLevels.Add(policy.LevelId, level);
        PreserveCathedralDoorProjection();
    }

    /** Restores exact saved geometry against raw door provenance, without a scanner or replay. */
    private void RestoreNativePopulationDoors(DiabloCathedralDoorCheckpoint checkpoint,
        IReadOnlyList<DiabloObjectState> objects)
    {
        if (World is null)
            throw new InvalidDataException("Native population doors require backing geometry.");
        DiabloCathedralDoorCheckpoint.RequireSameGeometry(trustedCathedralDoorBackingGeometry, checkpoint.BackingGeometry);
        var backing = new AuthoritativeWorld();
        backing.RestoreGeometryState(checkpoint.BackingGeometry);
        var owners = NativePopulationOwners.OrderBy(policy => policy.LevelId).ToArray();
        if (!checkpoint.DoorLevels.Select(level => level.LevelId).SequenceEqual(owners.Select(owner => owner.LevelId)))
            throw new InvalidDataException("Native doors require every retained floor owner exactly once.");
        var effective = ValidateCohortEffectiveLevels(checkpoint.EffectiveLevels).ToDictionary();
        var legitimate = BuildProjectedGeometry();
        if (!effective.Keys.Order().SequenceEqual(legitimate.Keys.Order()))
            throw new InvalidDataException("Native door checkpoint changed complete level coverage.");
        foreach (var entry in effective)
            if (!owners.Any(owner => owner.LevelId == entry.Key))
                DiabloCathedralDoorCheckpoint.RequireSameLevels(
                    [CaptureCohortLevel(legitimate[entry.Key])], [CaptureCohortLevel(entry.Value)]);
        var restored = new Dictionary<uint, (uint LevelId, AuthoritativeCathedralDoorState Door)>();
        var doorLevels = new Dictionary<uint, AuthoritativeLevel>();
        foreach (var policy in owners) {
            var baseline = backing.Levels.Single(value => value.LevelId == policy.LevelId);
            policy.ValidateBackingLevel(baseline, allowOmittedSpecial: true);
            baseline = AuthoritativeCathedralDoors.CopyLevel(baseline,
                special: policy.InitialState.Special.Select(number => (ushort)number));
            backing.ReplaceLevel(baseline);
            var saved = checkpoint.DoorLevels.Single(value => value.LevelId == policy.LevelId);
            if (saved.Doors.Count != policy.InitialDoorCount
                || !saved.BackingSpecial.SequenceEqual(baseline.NativeSpecialLayer)
                || saved.EffectiveSpecial.Count != baseline.Width * baseline.Height)
                throw new InvalidDataException("Native door checkpoint coverage or raw backing special bytes changed.");
            var projected = AuthoritativeCathedralDoors.CopyLevel(effective[policy.LevelId], special: saved.EffectiveSpecial);
            effective[policy.LevelId] = projected;
            if (!saved.Doors.SequenceEqual(saved.Doors.OrderBy(value => value.PositionY).ThenBy(value => value.PositionX)))
                throw new InvalidDataException("Native door checkpoint ordering changed.");
            foreach (var state in saved.Doors) {
                var initial = policy.GetInitialDoor(state.EntityId);
                var door = new AuthoritativeCathedralDoorState((AuthoritativeCathedralDoorKind)state.NativeObjectId,
                    state.PositionX, state.PositionY, (AuthoritativeCathedralDoorStatus)state.DoorState,
                    state.CachedPiece, state.CachedNeighborPiece, state.AnimationFrame,
                    state.SelectionRegion, state.PreDraw, state.MissilePassable, state.Solid);
                if (door.Kind != initial.Kind || door.PositionX != initial.PositionX || door.PositionY != initial.PositionY
                    || door.Var1 != initial.Var1 || door.Var2 != initial.Var2 || door.SolidFlag != initial.SolidFlag
                    || !Objects.TryGetValue(state.EntityId, out var value)
                    || objects.Count(candidate => candidate.EntityId == state.EntityId) != 1
                    || value.AnimationFrame != door.AnimationFrame || value.MissilePassable != door.MissFlag
                    || !restored.TryAdd(state.EntityId, (policy.LevelId, door)))
                    throw new InvalidDataException("Saved native doors conflict with their raw binding or object projection.");
                AuthoritativeCathedralDoors.Validate(projected, door);
                policy.ValidateObject(value);
            }
            ValidateCathedralDoorEffectiveGeometry(baseline, projected, saved.Doors);
            doorLevels.Add(policy.LevelId, projected);
        }
        // Publish only after all floors pass. No scanner or operation replay is involved.
        World.RestoreGeometryState(backing.CaptureGeometryState());
        foreach (var owner in owners)
            World.ReplaceLevel(AuthoritativeCathedralDoors.CopyLevel(
                World.Levels.Single(level => level.LevelId == owner.LevelId),
                special: owner.InitialState.Special.Select(number => (ushort)number)));
        projectedLevels.Clear();
        foreach (var entry in effective) projectedLevels.Add(entry.Key, entry.Value);
        cathedralDoorLevels.Clear();
        foreach (var entry in doorLevels) cathedralDoorLevels.Add(entry.Key, entry.Value);
        cathedralDoors.Clear();
        foreach (var entry in restored) cathedralDoors.Add(entry.Key, entry.Value);
        PreserveCathedralDoorProjection();
        PreserveNativeTownProjection();
    }
}
