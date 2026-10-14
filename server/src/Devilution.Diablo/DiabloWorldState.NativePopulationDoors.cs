namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    /** Keeps the public legacy constructors and their scanner path unchanged. */
    internal DiabloWorldState(DiabloSimulationOptions options)
        : this(options.World, options.FallbackWidth, options.FallbackHeight,
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
            || options.NativeCathedralItems.GenerationFingerprint != nativePopulationPolicy.Fingerprint
            || options.NativeMonsterMetadataFingerprint != nativePopulationPolicy.MonsterMetadataFingerprint
            || cathedralDoorCatalog?.Definitions.Count != 1
            || cathedralDoorCatalog.Definitions[0].LevelId != nativePopulationPolicy.LevelId)
            throw new InvalidDataException("Direct native population requires the exact single-player L1 item, actor and door profile.");
        nativePopulationPolicy.ValidateItemProfile(options.NativeCathedralItems);
        nativePopulationParticipantClassId = options.PlayerClasses?.Definitions.SingleOrDefault(
            definition => string.Equals(definition.Symbol, "rogue", StringComparison.Ordinal))?.ClassId
            ?? throw new InvalidDataException("Native population requires the canonical Rogue participant class.");
        if (World is null)
            throw new InvalidDataException("Native population requires its complete backing world.");
        var nativeBacking = World.Levels.Single(level => level.LevelId == nativePopulationPolicy.LevelId);
        nativePopulationPolicy.ValidateBackingLevel(nativeBacking,
            allowOmittedSpecial: options.DeferCohortInitialization);
        if (options.DeferCohortInitialization)
            World.ReplaceLevel(AuthoritativeCathedralDoors.CopyLevel(nativeBacking,
                special: nativePopulationPolicy.InitialState.Special.Select(number => (ushort)number)));
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
        var policy = nativePopulationPolicy!;
        if (World is null)
            throw new InvalidDataException("Native population doors require backing geometry.");
        DiabloCathedralDoorCheckpoint.RequireSameGeometry(trustedCathedralDoorBackingGeometry, checkpoint.BackingGeometry);
        var backing = new AuthoritativeWorld();
        backing.RestoreGeometryState(checkpoint.BackingGeometry);
        var baseline = backing.Levels.Single(value => value.LevelId == policy.LevelId);
        policy.ValidateBackingLevel(baseline, allowOmittedSpecial: true);
        baseline = AuthoritativeCathedralDoors.CopyLevel(baseline,
            special: policy.InitialState.Special.Select(number => (ushort)number));
        backing.ReplaceLevel(baseline);
        var saved = checkpoint.DoorLevels.Single();
        if (saved.LevelId != policy.LevelId || saved.Doors.Count != policy.InitialDoorCount
            || !saved.BackingSpecial.SequenceEqual(baseline.NativeSpecialLayer)
            || saved.EffectiveSpecial.Count != baseline.Width * baseline.Height)
            throw new InvalidDataException("Native door checkpoint coverage or raw backing special bytes changed.");
        var effective = ValidateCohortEffectiveLevels(checkpoint.EffectiveLevels).ToDictionary();
        var legitimate = BuildProjectedGeometry();
        if (!effective.Keys.Order().SequenceEqual(legitimate.Keys.Order()))
            throw new InvalidDataException("Native door checkpoint changed complete level coverage.");
        foreach (var entry in effective)
            if (entry.Key != policy.LevelId)
                DiabloCathedralDoorCheckpoint.RequireSameLevels(
                    [CaptureCohortLevel(legitimate[entry.Key])], [CaptureCohortLevel(entry.Value)]);
        var projected = AuthoritativeCathedralDoors.CopyLevel(effective[policy.LevelId], special: saved.EffectiveSpecial);
        effective[policy.LevelId] = projected;
        var restored = new Dictionary<uint, (uint LevelId, AuthoritativeCathedralDoorState Door)>();
        var expectedOrder = saved.Doors.OrderBy(value => value.PositionY).ThenBy(value => value.PositionX).ToArray();
        if (!saved.Doors.SequenceEqual(expectedOrder))
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
        // Publish only after all validation succeeds. Shared-frame operation history stays exact.
        World.RestoreGeometryState(backing.CaptureGeometryState());
        // The frozen geometry DTO omits Special; restore only the immutable owner's raw layer.
        World.ReplaceLevel(AuthoritativeCathedralDoors.CopyLevel(
            World.Levels.Single(level => level.LevelId == policy.LevelId),
            special: policy.InitialState.Special.Select(number => (ushort)number)));
        projectedLevels.Clear();
        foreach (var entry in effective)
            projectedLevels.Add(entry.Key, entry.Value);
        cathedralDoorLevels.Clear();
        cathedralDoorLevels.Add(policy.LevelId, projected);
        cathedralDoors.Clear();
        foreach (var entry in restored)
            cathedralDoors.Add(entry.Key, entry.Value);
        PreserveCathedralDoorProjection();
    }
}
