using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

/** The compiled all-class profile descends L1-L8 by stairs, restores cold, and climbs back. */
public sealed class AuthoritativeCatacombsNativeProgressionTests
{
    private const string ProfileVariable = "GODEVILUTION_NATIVE_ALL_CLASS_PROFILE";

    [Theory(Explicit = true)]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(2U)]
    public void AllClassProfileDescendsToL8AndRestoresCold(uint classId)
    {
        var (content, seed) = LoadAllClassProfile();
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, classId, seed);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.NotNull(activation.NativeCatacombsMetadata);
        // The single-player Town warp stays closed until L5's warp-up is taken.
        Assert.False(world.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Catacombs));
        Assert.DoesNotContain(world.PublishedWarpTargets!, row => row.SourceLevelId == 0 && row.TargetLevelId == 5);
        var start = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate("native-catacombs-descent", classId,
            null, null, null, startingLevelId: 1, startingPositionX: start.TriggerX, startingPositionY: start.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-catacombs-descent", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-catacombs-descent", value => value);
        var commands = WorldCommands(simulation);
        var codec = new DiabloCohortCodec();

        for (uint level = 1; level < 8; level++) {
            UseStair(world, commands, player, level, level + 1);
            Assert.True(world.OwnsNativePopulationLevel(level + 1));
        }
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeCatacombsProgressionFormatVersion, captured.FormatVersion);
        Assert.Equal(Enumerable.Range(2, 7).Select(value => (uint)value),
            captured.NativeProgression!.Entries.Select(entry => entry.LevelId));
        Assert.Equal(Enumerable.Range(1, 8).Select(value => (uint)value),
            captured.CathedralItems!.Levels.Select(level => level.LevelId));
        var bytes = codec.Serialize(captured);
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));

        var restoredWorld = candidate.Simulation.WorldState;
        var restoredPlayer = restoredRuntime.Read("native-catacombs-descent", value => value);
        var restoredCommands = WorldCommands(candidate.Simulation);
        for (var level = 8U; level > 1; level--)
            UseStair(restoredWorld, restoredCommands, restoredPlayer, level, level - 1);
        Assert.Equal(1U, restoredPlayer.LevelId);
    }

    /** Warming runs off the authority lock, so it must not reach the live run or change what it generates. */
    [Fact(Explicit = true)]
    public void WarmingFloorEntriesLeavesTheLiveRunAndItsLaterFloorsUnchanged()
    {
        var (content, seed) = LoadAllClassProfile();
        var codec = new DiabloCohortCodec();
        var warmed = StartDescent(content, seed);
        var cold = StartDescent(content, seed);
        var before = codec.Serialize(warmed.Simulation.CaptureCohortState(warmed.Runtime));

        warmed.Simulation.WarmNativeFloorEntries();

        Assert.Equal(before, codec.Serialize(warmed.Simulation.CaptureCohortState(warmed.Runtime)));
        for (uint level = 1; level < 8; level++) {
            UseStair(warmed.Simulation.WorldState, WorldCommands(warmed.Simulation), warmed.Player, level, level + 1);
            UseStair(cold.Simulation.WorldState, WorldCommands(cold.Simulation), cold.Player, level, level + 1);
        }
        Assert.Equal(codec.Serialize(cold.Simulation.CaptureCohortState(cold.Runtime)),
            codec.Serialize(warmed.Simulation.CaptureCohortState(warmed.Runtime)));
    }

    [Theory(Explicit = true)]
    [InlineData(2U)]
    [InlineData(5U)]
    public void FloorEntryPublishesTheSameStateWithoutCommitVerification(uint levelId)
    {
        var (content, seed) = LoadAllClassProfile();
        var codec = new DiabloCohortCodec();
        string Enter(bool verifyAtCommit)
        {
            var run = StartDescent(content, seed);
            var world = run.Simulation.WorldState;
            for (uint level = 1; level + 1 < levelId; level++)
                UseStair(world, WorldCommands(run.Simulation), run.Player, level, level + 1);
            var stair = world.World!.WarpTargets.Single(row => row.SourceLevelId == levelId - 1 && row.TargetLevelId == levelId);
            run.Player.PositionX = stair.TriggerX;
            run.Player.PositionY = stair.TriggerY;
            var prepared = world.PrepareNativeProgressionEntry(run.Player, levelId, [run.Player],
                run.Simulation.Items!, run.Simulation.Spells!, verifyAtCommit: verifyAtCommit)!.Value;
            world.CommitNativeProgressionEntry(prepared.Admission, prepared.Entry);
            return codec.Serialize(run.Simulation.CaptureCohortState(run.Runtime));
        }

        Assert.Equal(Enter(verifyAtCommit: true), Enter(verifyAtCommit: false));
    }

    /** Callers that do not commit in the same call keep the full state comparison by default. */
    [Theory(Explicit = true)]
    [InlineData(2U)]
    [InlineData(5U)]
    public void VerifiedFloorEntryRefusesAChangedFloor(uint levelId)
    {
        var (content, seed) = LoadAllClassProfile();
        var codec = new DiabloCohortCodec();
        var run = StartDescent(content, seed);
        var world = run.Simulation.WorldState;
        for (uint level = 1; level + 1 < levelId; level++)
            UseStair(world, WorldCommands(run.Simulation), run.Player, level, level + 1);
        var prepared = world.PrepareNativeProgressionEntry(run.Player, levelId, [run.Player],
            run.Simulation.Items!, run.Simulation.Spells!)!.Value;
        prepared.Admission.Floor.CombatTargets.Values.First(target => target.HitPointsQ6 > 128).ApplyDamageQ6(64);
        var before = codec.Serialize(run.Simulation.CaptureCohortState(run.Runtime));

        Assert.Throws<InvalidOperationException>(() => world.CommitNativeProgressionEntry(prepared.Admission, prepared.Entry));

        Assert.Equal(before, codec.Serialize(run.Simulation.CaptureCohortState(run.Runtime)));
        Assert.False(world.OwnsNativePopulationLevel(levelId));
    }

    /** Skipping the full state comparison keeps the identity checks on what the floor would adopt. */
    [Fact(Explicit = true)]
    public void UnverifiedFloorEntryStillRefusesAForeignEntity()
    {
        var (content, seed) = LoadAllClassProfile();
        var run = StartDescent(content, seed);
        var world = run.Simulation.WorldState;
        var prepared = world.PrepareNativeProgressionEntry(run.Player, 2, [run.Player],
            run.Simulation.Items!, run.Simulation.Spells!, verifyAtCommit: false)!.Value;
        var foreign = world.CombatTargets.Values.First();
        prepared.Admission.Floor.CombatTargets.Add(foreign.EntityId, foreign);

        Assert.Throws<InvalidOperationException>(() => world.CommitNativeProgressionEntry(prepared.Admission, prepared.Entry));
    }

    private static (DiabloSimulation Simulation, AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> Runtime,
        AuthoritativePlayerState Player) StartDescent(ComposedContent content, uint seed)
    {
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, 0, seed);
        var simulation = activation.Simulation;
        var start = simulation.WorldState.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate("native-floor-warmup", 0,
            null, null, null, startingLevelId: 1, startingPositionX: start.TriggerX, startingPositionY: start.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-floor-warmup", player => player.EntityId = 0x70000001);
        return (simulation, runtime, runtime.Read("native-floor-warmup", value => value));
    }

    /** Native CheckTriggers ignores a walking player: the L5 down path crosses the up stair. */
    [Fact(Explicit = true)]
    public void PathThroughTheUpStairReachesTheNextCatacombsDepth()
    {
        var (content, seed) = LoadAllClassProfile();
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, 0, seed);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var start = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate("native-catacombs-walk", 0,
            null, null, null, startingLevelId: 1, startingPositionX: start.TriggerX, startingPositionY: start.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-catacombs-walk", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-catacombs-walk", value => value);
        var commands = WorldCommands(simulation);
        for (uint level = 1; level < 5; level++)
            UseStair(world, commands, player, level, level + 1);
        var up = world.World!.WarpTargets.Single(row => row.SourceLevelId == 5 && row.TargetLevelId == 4);
        var down = world.World.WarpTargets.Single(row => row.SourceLevelId == 5 && row.TargetLevelId == 6);
        while (player.CurrentAction is { } arrival)
            runtime.AdvanceTo(Math.Max(arrival.EndTick, checked(runtime.LastAdvancedTick + 1)));
        var tick = checked(runtime.LastAdvancedTick + 1);
        var walk = simulation.Execute(player, new DiabloMoveToIntent(down.TriggerX, down.TriggerY, FollowPath: true), tick, [player]);
        Assert.True(walk.Succeeded, $"{walk.Failure} {player.CombatMode} {player.CurrentAction}");
        var crossed = false;
        for (var step = 0; step < 400 && player.LevelId == 5; step++) {
            runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
            crossed |= player.LevelId == 5 && (player.PositionX, player.PositionY) == (up.TriggerX, up.TriggerY);
        }
        Assert.True(crossed, $"The planned path no longer crosses the up stair: L{player.LevelId} ({player.PositionX},{player.PositionY}).");
        Assert.Equal(6U, player.LevelId);
    }

    /**
     * Native single-player WM_DIABTWARPUP: L5's warp-up returns to Town and sets pTownWarps,
     * so CreateTown opens the Catacombs warp, which then leads back to L5 at any clvl.
     */
    [Fact(Explicit = true)]
    public void SinglePlayerL5WarpUpOpensTheTownWarpAndRestoresCold()
    {
        var (activation, runtime, player) = DescendToL5("native-town-warp-sp");
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var commands = WorldCommands(simulation);
        var lockedTown = world.CaptureNativeTown()!;
        var lockedPieces = world.World!.Levels.Single(level => level.LevelId == 0).NativePieceLayer!.ToArray();
        Assert.Equal(new AuthoritativeTownWarpOpenings(false, false, false, false, false), lockedTown.WarpOpenings);

        UseStair(world, commands, player, 5, 0);
        Assert.Equal((49, 22), (player.PositionX, player.PositionY));
        Assert.True(world.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Catacombs));
        Assert.False(world.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Caves));
        var down = Assert.Single(world.PublishedWarpTargets!, row => row.SourceLevelId == 0 && row.TargetLevelId == 5);
        Assert.Equal(0U, down.RequiredCharacterLevel);
        var town = world.CaptureNativeTown()!;
        Assert.Equal(AuthoritativeTownWarpUnlocks.Catacombs, town.Settings.WarpUnlocks);
        Assert.Equal(new AuthoritativeTownWarpOpenings(true, false, false, false, false), town.WarpOpenings);
        Assert.Equal(lockedTown.ClosedHellTileChoices, town.ClosedHellTileChoices);
        Assert.Equal(lockedTown.CatalogFingerprint, town.CatalogFingerprint);
        Assert.NotEqual(lockedPieces, world.World.Levels.Single(level => level.LevelId == 0).NativePieceLayer!);

        // No clvl gate in single-player; L5 is already generated, so no new entry.
        Assert.Equal(1U, player.CharacterLevel);
        UseStair(world, commands, player, 0, 5);
        Assert.Equal((down.TargetX, down.TargetY), (player.PositionX, player.PositionY));
        UseStair(world, commands, player, 5, 0);
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(Enumerable.Range(2, 4).Select(value => (uint)value),
            captured.NativeProgression!.Entries.Select(entry => entry.LevelId));

        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(captured);
        // A fresh host activates the locked Town and adopts the unlock from the save.
        var (content, seed) = LoadAllClassProfile();
        var fresh = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(content, 0, seed);
        Assert.False(fresh.Simulation.WorldState.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Catacombs));
        var candidate = fresh.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        var restoredWorld = candidate.Simulation.WorldState;
        Assert.True(restoredWorld.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Catacombs));
        Assert.Equal(AuthoritativeTownWarpUnlocks.Catacombs, restoredWorld.World!.WarpTargets
            .Single(row => row.SourceLevelId == 0 && row.TargetLevelId == 5).RequiredTownWarp);
        var restoredPlayer = restoredRuntime.Read("native-town-warp-sp", value => value);
        UseStair(restoredWorld, WorldCommands(candidate.Simulation), restoredPlayer, 0, 5);
    }

    /**
     * L5's warp-up room lies behind closed Catacombs doors (native 42/43): opened from adjacent
     * tiles they connect it, and a walk that stops on the warp-up returns to Town (49,22).
     */
    [Fact(Explicit = true)]
    public void OpenedDoorsReachTheL5WarpUpAndWalkingOntoItReturnsToTown()
    {
        var (activation, runtime, player) = DescendToL5("native-town-warp-walk");
        var simulation = activation.Simulation;
        var warpUp = simulation.WorldState.World!.WarpTargets.Single(row => row.SourceLevelId == 5 && row.TargetLevelId == 0);
        var goal = (warpUp.TriggerX, warpUp.TriggerY);
        while (player.CurrentAction is { } arrival)
            runtime.AdvanceTo(Math.Max(arrival.EndTick, checked(runtime.LastAdvancedTick + 1)));
        Assert.False(Reach(simulation, player).ContainsKey(goal));
        var opened = new List<(int, int)>();
        for (var attempt = 0; attempt < 16 && !Reach(simulation, player).ContainsKey(goal); attempt++) {
            var reach = Reach(simulation, player);
            var door = simulation.WorldState.Objects.Values
                .Where(o => o.LevelId == 5 && o.NativeObjectId is 42 or 43 && !o.MissilePassable)
                .Select(o => (Door: o, From: reach.Keys.Where(cell => NativeDistance(cell, (o.PositionX, o.PositionY)) == 1)
                    .Cast<(int, int)?>().FirstOrDefault()))
                .Where(pair => pair.From is not null)
                .OrderBy(pair => NativeDistance((pair.Door.PositionX, pair.Door.PositionY), goal))
                .First();
            (player.PositionX, player.PositionY) = door.From!.Value;
            var result = simulation.Execute(player, new DiabloOperateObjectIntent(door.Door.EntityId),
                checked(runtime.LastAdvancedTick + 1), [player]);
            Assert.True(result.Succeeded, $"Door {door.Door.EntityId}: {result.Failure}");
            runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
            Assert.True(door.Door.MissilePassable, $"Door {door.Door.EntityId} did not open.");
            opened.Add((door.Door.PositionX, door.Door.PositionY));
        }
        Assert.True(Reach(simulation, player).ContainsKey(goal), $"Opened {opened.Count} doors without reaching the warp-up.");

        // Monster AI on L5 is batch 2.4; walk the last tile onto the trigger as a stair test does.
        var from = Reach(simulation, player).Keys.First(cell => NativeDistance(cell, goal) == 1);
        (player.PositionX, player.PositionY) = from;
        var walk = simulation.Execute(player, new DiabloMoveToIntent(warpUp.TriggerX, warpUp.TriggerY, FollowPath: true),
            checked(runtime.LastAdvancedTick + 1), [player]);
        Assert.True(walk.Succeeded, $"{walk.Failure}");
        for (var step = 0; step < 100 && player.LevelId == 5; step++)
            runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
        Assert.Equal((0U, 49, 22), (player.LevelId, player.PositionX, player.PositionY));
        Assert.True(simulation.WorldState.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Catacombs));
    }

    private static int NativeDistance((int X, int Y) first, (int X, int Y) second)
        => Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));

    private static Dictionary<(int, int), int> Reach(DiabloSimulation simulation, AuthoritativePlayerState player)
    {
        var world = simulation.WorldState;
        var seen = new Dictionary<(int, int), int> { [(player.PositionX, player.PositionY)] = 0 };
        var queue = new Queue<(int X, int Y)>([(player.PositionX, player.PositionY)]);
        while (queue.Count > 0) {
            var (x, y) = queue.Dequeue();
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++) {
                    var next = (x + dx, y + dy);
                    if (seen.ContainsKey(next) || !world.IsWalkable(5, next.Item1, next.Item2))
                        continue;
                    seen[next] = seen[(x, y)] + 1;
                    queue.Enqueue(next);
                }
        }
        return seen;
    }

    /** The admitted L5 roster keeps a killed archer; its corpse lets a fresh host restore the floor. */
    [Fact(Explicit = true)]
    public void KilledCatacombsArcherRestoresOnAFreshHost()
    {
        var (activation, runtime, player) = DescendToL5("native-killed-archer");
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var archer = world.CombatTargets.Values
            .Where(target => target.LevelId == 5 && target.NativeAi is "SkeletonRanged" or "GoatRanged")
            .OrderBy(target => target.EntityId).First();
        DiabloCombatIntentResult? last = null;
        for (var swing = 0; swing < 40 && world.CombatTargets.ContainsKey(archer.EntityId); swing++) {
            (player.PositionX, player.PositionY) = AdjacentWalkable(simulation, archer.PositionX, archer.PositionY);
            player.Life = player.LifeMaximum;
            archer.HitPoints = 1;
            last = simulation.Execute(player, new DiabloAttackIntent(archer.EntityId), checked(runtime.LastAdvancedTick + 1), [player]);
            runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 20));
        }
        Assert.False(world.CombatTargets.ContainsKey(archer.EntityId),
            $"{last?.Succeeded}/{last?.Failure}; hp={archer.HitPointsQ6}; at ({archer.PositionX},{archer.PositionY}) player ({player.PositionX},{player.PositionY})");
        Assert.Contains(archer.EntityId, world.Corpses.Keys);

        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var (content, seed) = LoadAllClassProfile();
        var fresh = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(content, 0, seed);
        var candidate = fresh.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Contains(archer.EntityId, candidate.Simulation.WorldState.Corpses.Keys);
    }

    /** Special attacks and Sneak fades are bounded by the Special graphic, not the normal attack, on restore. */
    [Fact(Explicit = true)]
    public void SpecialGraphicCursorsRestoreOnAFreshHost()
    {
        var (activation, runtime, _) = DescendToL5("native-special-cursor");
        var simulation = activation.Simulation;
        var actor = simulation.WorldState.CombatTargets.Values
            .Where(target => target.LevelId == 5 && !target.IsMoving && target.NativeMeleeCursor is null
                && target.NativeInitialization is { } native && native.NativeSpecialAttackFrameCount > native.NativeAttackFrameCount)
            .OrderBy(target => target.EntityId).First();
        var codec = new DiabloCohortCodec();
        foreach (var cursor in new[] {
                     new AuthoritativeNativeMeleeCursor(actor.NativeInitialization!.NativeSpecialAttackFrameCount - 1, 0) {
                         Fade = AuthoritativeNativeFade.Out,
                     },
                     new AuthoritativeNativeMeleeCursor(actor.NativeInitialization.NativeSpecialAttackFrameCount - 1, 0) {
                         UsesSpecialAttack = true,
                     },
                 }) {
            actor.RestoreNativeMeleeCursor(cursor);
            actor.RestoreNativeSneakState(new AuthoritativeNativeSneakState(9, 3, true, 2));
            var bytes = codec.Serialize(simulation.CaptureCohortState(runtime));
            var candidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
            var restoredRuntime = candidate.CreateRuntime();
            Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
            var restored = candidate.Simulation.WorldState.CombatTargets[actor.EntityId];
            Assert.Equal(cursor, restored.NativeMeleeCursor);
            Assert.Equal(new AuthoritativeNativeSneakState(9, 3, true, 2), restored.NativeSneak);
        }
    }

    /** Native ranged AI vars, a ranged cursor and a projectile-owned Acid puddle restore on a fresh host. */
    [Fact(Explicit = true)]
    public void NativeRangedStateRestoresOnAFreshHost()
    {
        var (activation, runtime, _) = DescendToL5("native-ranged-state");
        var simulation = activation.Simulation;
        var actor = simulation.WorldState.CombatTargets.Values
            .Where(target => target.LevelId == 5 && !target.IsMoving && target.NativeMeleeCursor is null
                && target.NativeInitialization is { NativeSpecialAttackFrameCount: > 1 })
            .OrderBy(target => target.EntityId).First();
        var vars = new AuthoritativeNativeAiVars(10, 3, true, 4, 7, 1);
        var cursor = new AuthoritativeNativeMeleeCursor(0, 0) { RangedMissile = "Acid", UsesSpecialAttack = true };
        actor.RestoreNativeAiVars(vars);
        actor.RestoreNativeMeleeCursor(cursor);
        var puddle = new AuthoritativeProjectile(0x8000F001, actor.EntityId, 0, 0, 5, actor.PositionX, actor.PositionY,
            actor.PositionX, actor.PositionY, 2, AuthoritativeDamageType.Acid, 0, 40, allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.AcidPuddle,
            NativeMissileId = "AcidPuddle",
            ImpactLifetime = 0,
            NativeMonsterMissile = AuthoritativeNativeMonsterMissileState.Create(
                AuthoritativeNativeMonsterMissileKind.AcidPuddle, actor) with { Ending = true, LastCollisionTargetHash = -2 },
        };
        simulation.WorldState.Projectiles.Add(puddle.EntityId, puddle);
        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        var restored = candidate.Simulation.WorldState.CombatTargets[actor.EntityId];
        Assert.Equal(vars, restored.NativeAiVars);
        Assert.Equal(cursor, restored.NativeMeleeCursor);
        Assert.Equal(puddle.NativeMonsterMissile, candidate.Simulation.WorldState.Projectiles[puddle.EntityId].NativeMonsterMissile);
    }

    private static (int X, int Y) AdjacentWalkable(DiabloSimulation simulation, int x, int y)
    {
        return new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) }
            .Select(offset => (X: x + offset.Item1, Y: y + offset.Item2))
            .First(cell => simulation.WorldState.World!.IsWalkable(5, cell.X, cell.Y)
                && !simulation.WorldState.CombatTargets.Values.Any(target => target.LevelId == 5
                    && (target.PositionX, target.PositionY) == cell));
    }

    /** Saves from before batch 2.2c lack the Town warp pair; they restore with it, still closed. */
    [Fact(Explicit = true)]
    public void PreTownWarpSinglePlayerSaveRestoresWithClosedTownWarp()
    {
        var (activation, runtime, _) = DescendToL5("native-town-warp-legacy");
        var captured = activation.Simulation.CaptureCohortState(runtime);
        static bool TownWarp(DiabloWorldGeometryWarpState warp)
            => warp.WarpId is 1201 or 1202;
        Assert.Equal(2, captured.Geometry!.WarpTargets!.Count(TownWarp));
        var legacy = captured with {
            Geometry = captured.Geometry with { WarpTargets = captured.Geometry.WarpTargets!.Where(warp => !TownWarp(warp)).ToArray() },
            CathedralDoors = captured.CathedralDoors! with {
                BackingGeometry = captured.CathedralDoors.BackingGeometry with {
                    WarpTargets = captured.CathedralDoors.BackingGeometry.WarpTargets!.Where(warp => !TownWarp(warp)).ToArray(),
                },
            },
        };
        var codec = new DiabloCohortCodec();
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(codec.Serialize(legacy)));
        var restoredRuntime = candidate.CreateRuntime();
        var restoredWorld = candidate.Simulation.WorldState;
        Assert.Equal(2, restoredWorld.World!.WarpTargets.Count(warp => TownWarp(AuthoritativeWorld.CaptureWarpState(warp))));
        Assert.False(restoredWorld.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Catacombs));
        var restoredPlayer = restoredRuntime.Read("native-town-warp-legacy", value => value);
        UseStair(restoredWorld, WorldCommands(candidate.Simulation), restoredPlayer, 5, 0);
        Assert.True(restoredWorld.IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks.Catacombs));
    }

    private static (DiabloContentActivation Activation, Devilution.Engine.AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> Runtime, AuthoritativePlayerState Player) DescendToL5(string name)
    {
        var (content, seed) = LoadAllClassProfile();
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, 0, seed);
        var simulation = activation.Simulation;
        var start = simulation.WorldState.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate(name, 0,
            null, null, null, startingLevelId: 1, startingPositionX: start.TriggerX, startingPositionY: start.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write(name, player => player.EntityId = 0x70000001);
        var player = runtime.Read(name, value => value);
        var commands = WorldCommands(simulation);
        for (uint level = 1; level < 5; level++)
            UseStair(simulation.WorldState, commands, player, level, level + 1);
        return (activation, runtime, player);
    }

    /** Native multiplayer WM_DIABTOWNWARP: clvl 8 enters L5 first by ENTRY_TWARPDN; L5's warp-up returns. */
    [Fact(Explicit = true)]
    public void MultiplayerTownWarpGatesCharacterLevelEntersL5AndRestoresCold()
    {
        var (content, seed) = LoadAllClassProfile();
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownClass(content, 0, seed, DiabloGameMode.Multiplayer);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var down = world.World!.WarpTargets.Single(row => row.SourceLevelId == 0 && row.TargetLevelId == 5);
        Assert.Equal((49, 21, 8U, AuthoritativeCatacombsEntry.TownWarpDown),
            (down.TriggerX, down.TriggerY, down.RequiredCharacterLevel, down.FirstEntry));
        var template = activation.PlayerLoadouts.CreateTemplate("native-town-warp", 0,
            null, null, null, startingLevelId: 0, startingPositionX: down.TriggerX, startingPositionY: down.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-town-warp", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-town-warp", value => value);
        var commands = WorldCommands(simulation);

        // Below clvl 8 the warp is refused and the player walks one tile south.
        Assert.Equal(1U, player.CharacterLevel);
        var refused = commands.ExecuteLevelTrigger(player, 0, [player]);
        Assert.Equal(DiabloRuleFailure.NotAllowed, refused.Failure);
        Assert.Equal(0U, player.LevelId);
        var turnBack = Assert.Single(player.NativeWalkPath!.Directions);
        Assert.Equal((0, 1), AuthoritativeNativePlayerPathfinder.GetDisplacement(turnBack));

        player.NativeWalkPath = null;
        player.CharacterLevel = 8;
        UseStair(world, commands, player, 0, 5);
        Assert.Equal((down.TargetX, down.TargetY), (player.PositionX, player.PositionY));
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(new AuthoritativeTownWarpOpenings(true, true, true, false, true), captured.NativeTown!.WarpOpenings);
        var entry = Assert.Single(captured.NativeProgression!.Entries);
        Assert.Equal((5U, AuthoritativeCatacombsEntry.TownWarpDown), (entry.LevelId, entry.CatacombsEntry));

        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(captured);
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        var restoredDown = candidate.Simulation.WorldState.World!.WarpTargets.Single(row => row.SourceLevelId == 0 && row.TargetLevelId == 5);
        Assert.Equal((8U, AuthoritativeCatacombsEntry.TownWarpDown), (restoredDown.RequiredCharacterLevel, restoredDown.FirstEntry));
        var restoredPlayer = restoredRuntime.Read("native-town-warp", value => value);
        UseStair(candidate.Simulation.WorldState, WorldCommands(candidate.Simulation), restoredPlayer, 5, 0);
        Assert.Equal((49, 22), (restoredPlayer.PositionX, restoredPlayer.PositionY));
    }

    [Fact]
    public void CatacombsDepthsOwnOnlyLevelsFiveThroughEight()
    {
        Assert.Equal(new uint[] { 5, 6, 7, 8 }, Enumerable.Range(0, 12).Select(value => (uint)value)
            .Where(AuthoritativeCatacombsNativeDepths.Owns));
        Assert.Equal("cathedral_depth_4_to_catacombs_depth_5",
            $"{AuthoritativeCatacombsNativeDepths.Symbol(4)}_to_{AuthoritativeCatacombsNativeDepths.Symbol(5)}");
        Assert.Equal("cathedral", AuthoritativeCatacombsNativeDepths.Symbol(1));
    }

    [Fact]
    public void CatacombsCheckpointRejectsOwnerStateOutsideEnteredFloors()
    {
        var empty = new DiabloNativeCatacombsCheckpoint([], [], [], []);
        empty.ValidateShape(new HashSet<uint> { 5 });
        Assert.Throws<InvalidDataException>(() => (empty with { Doors = null! }).ValidateShape(new HashSet<uint> { 5 }));
        var door = new AuthoritativeCatacombsDoorState(0x0A000001, 6, 42, AuthoritativeCatacombsDoorKind.Left,
            3, 3, AuthoritativeCatacombsDoorStatus.Closed, 1, 3, false, false);
        (empty with { Doors = [door] }).ValidateShape(new HashSet<uint> { 5, 6 });
        Assert.Throws<InvalidDataException>(() => (empty with { Doors = [door] }).ValidateShape(new HashSet<uint> { 5 }));
        Assert.Throws<InvalidDataException>(() => (empty with { Doors = [null!] }).ValidateShape(new HashSet<uint> { 5 }));
    }

    private static void UseStair(DiabloWorldState world, DiabloWorldCommandProcessor commands,
        AuthoritativePlayerState player, uint source, uint target)
    {
        var stair = world.World!.WarpTargets.Single(row => row.SourceLevelId == source && row.TargetLevelId == target);
        player.CurrentAction = null;
        player.PositionX = stair.TriggerX;
        player.PositionY = stair.TriggerY;
        var result = commands.ExecuteLevelTrigger(player, 0, [player]);
        Assert.True(result.Succeeded, $"Stair {source}->{target}: {result.Failure}");
        Assert.Equal(target, player.LevelId);
    }

    private static DiabloWorldCommandProcessor WorldCommands(DiabloSimulation simulation)
        => (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulation)!;

    private static (ComposedContent Content, uint Seed) LoadAllClassProfile()
    {
        var directory = Environment.GetEnvironmentVariable(ProfileVariable);
        Assert.False(string.IsNullOrWhiteSpace(directory), $"{ProfileVariable} must name a compiled all-class profile.");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!,
            "cathedral-native-all-class-town-playtest.json")));
        var profile = document.RootElement;
        var nested = profile.GetProperty("classProfile");
        var directories = new[] { "contentDirectory", "populationDirectory", "runtimeDirectory" }
            .Select(key => nested.GetProperty(key).GetString()!)
            .Append(profile.GetProperty("townDirectory").GetString()!)
            .Append(AuthoritativeCatacombsPopulationMetadata.ProfileSidecarDirectory);
        var packs = directories.Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(directory!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        Assert.Equal(profile.GetProperty("contentManifestHash").GetString(), manifest.Sha256);
        return (ComposedContent.Compose(manifest), profile.GetProperty("townInitialSeed").GetUInt32());
    }
}
