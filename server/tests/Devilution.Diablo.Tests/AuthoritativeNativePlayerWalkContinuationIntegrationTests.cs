using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeDepthDestinationWalkReplacesFromFutureAndContinuesAfterColdRestore()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.True(activation.TownerGreetings!.TryGet(1, out var pepin));
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: pepin.PositionX + offset.X, Y: pepin.PositionY + offset.Y))
            .First(cell => world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, []));
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        const string session = "native-depth-walk-cold";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, null, null, null, startingLevelId: 0,
            statPointsOverride: 1, startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(session, player => player.EntityId = 0x70000001);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);
        var participants = new[] { player };

        var firstGoal = FindWalkGoal(player.PositionX, player.PositionY, excluded: null, minimumLength: 4);
        var firstPath = FindPath(player.PositionX, player.PositionY, firstGoal.X, firstGoal.Y);
        var codec = new DiabloCohortCodec();
        var noPathBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        Assert.DoesNotContain("\"NativeWalkPath\"", noPathBytes);
        Assert.True(simulation.Execute(player,
            new DiabloMoveToIntent(firstGoal.X, firstGoal.Y, FollowPath: true), 2, participants).Succeeded);
        Assert.Null(player.CurrentAction);
        Assert.Equal(firstPath, player.NativeWalkPath!.Directions.ToArray());
        var initialPosition = (X: player.PositionX, Y: player.PositionY);
        player.StatPoints = 1; // Controlled unit fixture for an accepted non-movement command.
        Assert.True(simulation.Execute(player,
            new DiabloAllocateAttributeIntent(DiabloPlayerAttribute.Dexterity, 1), 2, participants).Succeeded);
        Assert.Equal(firstPath, player.NativeWalkPath!.Directions.ToArray());
        runtime.AdvanceTo(2);
        var initialMove = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(DiabloPlayerActionKind.Move, initialMove.Kind);
        var expectedEndTick = 2UL + (ulong)activation.PlayerAnimations.GetRequired(rogue.ClassId).WalkingFrames + 1UL;
        Assert.Equal((2UL, expectedEndTick, expectedEndTick, expectedEndTick),
            (initialMove.StartedAtTick, initialMove.EffectTick, initialMove.ReadyTick, initialMove.EndTick));
        Assert.Equal(firstPath.Skip(1).ToArray(), player.NativeWalkPath!.Directions.ToArray());
        var initialFuture = (X: player.FuturePositionX, Y: player.FuturePositionY);
        var firstDisplacement = AuthoritativeNativePlayerPathfinder.GetDisplacement(firstPath[0]);
        Assert.Equal((firstDisplacement.X, firstDisplacement.Y),
            (initialMove.DirectionX, initialMove.DirectionY));
        Assert.Equal((initialPosition.X + firstDisplacement.X, initialPosition.Y + firstDisplacement.Y),
            (initialFuture.X, initialFuture.Y));
        Assert.True(world.IsOccupied(0, initialFuture.X, initialFuture.Y, participants, uint.MaxValue));
        Assert.False(initialMove.EffectApplied);
        runtime.AdvanceTo(initialMove.EffectTick - 1);
        Assert.Equal(initialPosition, (player.PositionX, player.PositionY));
        Assert.Equal(initialFuture, (player.FuturePositionX, player.FuturePositionY));
        runtime.AdvanceTo(initialMove.EndTick);
        Assert.Equal(initialFuture, (player.PositionX, player.PositionY));
        var continuedMove = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(initialMove.EndTick, continuedMove.StartedAtTick);
        Assert.False(continuedMove.EffectApplied);
        Assert.Equal(firstPath.Skip(2).ToArray(), player.NativeWalkPath!.Directions.ToArray());

        var futureX = player.FuturePositionX;
        var futureY = player.FuturePositionY;
        var replacementGoal = FindWalkGoal(futureX, futureY, firstGoal, minimumLength: 4);
        var replacementPath = FindPath(futureX, futureY, replacementGoal.X, replacementGoal.Y);
        var activeMove = player.CurrentAction;
        var replacementTick = checked(runtime.LastAdvancedTick + 1);
        Assert.True(simulation.Execute(player,
            new DiabloMoveToIntent(replacementGoal.X, replacementGoal.Y, FollowPath: true), replacementTick, participants).Succeeded);
        Assert.Equal(activeMove, player.CurrentAction);
        Assert.Equal(replacementPath, player.NativeWalkPath!.Directions.ToArray());

        var rejectedAttack = simulation.Execute(player, new DiabloAttackIntent(uint.MaxValue), replacementTick, participants);
        Assert.False(rejectedAttack.Succeeded);
        Assert.Equal(replacementPath, player.NativeWalkPath!.Directions.ToArray());
        var healingItem = player.Belt.Where(item => IsHealingAction(item.State.ItemActionKind))
            .Select(item => new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt,
                item.Slot, item.State.StableItemId))
            .Concat(player.Inventory.Select((item, index) => (item, index))
                .Where(entry => IsHealingAction(entry.item.State.ItemActionKind))
                .Select(entry => new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory,
                    (uint)entry.index, entry.item.State.StableItemId)))
            .First();
        player.Life = Math.Max(1, player.LifeMaximum / 2);
        var beforeHealing = player.Life;
        var healed = simulation.Execute(player, new DiabloUseConsumableIntent(healingItem),
            replacementTick);
        Assert.True(healed.Succeeded, healed.Failure?.ToString());
        Assert.True(player.Life > beforeHealing);
        Assert.Equal(activeMove, player.CurrentAction);
        Assert.Equal(replacementPath, player.NativeWalkPath!.Directions.ToArray());
        runtime.AdvanceTo(replacementTick);

        var checkpoint = codec.Serialize(simulation.CaptureCohortState(runtime));
        var decoded = codec.Deserialize(checkpoint);
        Assert.Equal(replacementPath,
            decoded.Participants.Single().Player.NativeWalkPath!.Directions.ToArray());
        Assert.Throws<InvalidDataException>(() => codec.Serialize(decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with {
                    NativeWalkPath = new DiabloNativeWalkPathState(0, [0]),
                },
            }],
        }));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with {
                    NativeWalkPath = new DiabloNativeWalkPathState(1, [1]),
                },
            }],
        }));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(decoded with {
            FormatVersion = DiabloCohortState.NativeProgressionFormatVersion,
        }));
        var absentPath = decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with { NativeWalkPath = null },
            }],
        };
        var absentPathBytes = codec.Serialize(absentPath);
        Assert.DoesNotContain("\"NativeWalkPath\"", absentPathBytes);
        Assert.Equal(absentPathBytes, codec.Serialize(codec.Deserialize(absentPathBytes)));
        var candidate = activation.CohortCandidateFactory!(decoded);
        var restored = candidate.CreateRuntime();
        Assert.Equal(checkpoint, codec.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        var restoredPlayer = restored.Read(session, value => value);

        var finalTick = runtime.LastAdvancedTick;
        for (var attempt = 0; attempt < 160; attempt++) {
            finalTick = checked(finalTick + 1);
            runtime.AdvanceTo(finalTick);
            restored.AdvanceTo(finalTick);
            if (player.LevelId == 0 && player.PositionX == replacementGoal.X
                && player.PositionY == replacementGoal.Y
                && player.CurrentAction is null && player.NativeWalkPath is null)
                break;
        }
        Assert.Equal((0U, replacementGoal.X, replacementGoal.Y),
            (player.LevelId, player.PositionX, player.PositionY));
        Assert.Null(player.CurrentAction);
        Assert.Null(player.NativeWalkPath);
        Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
            codec.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        Assert.Equal((player.LevelId, player.PositionX, player.PositionY),
            (restoredPlayer.LevelId, restoredPlayer.PositionX, restoredPlayer.PositionY));

        (int X, int Y) FindWalkGoal(int sourceX, int sourceY, (int X, int Y)? excluded, int minimumLength)
        {
            for (var radius = minimumLength; radius <= 10; radius++) {
                for (var y = sourceY - radius; y <= sourceY + radius; y++) {
                    for (var x = sourceX - radius; x <= sourceX + radius; x++) {
                        if (Math.Max(Math.Abs(x - sourceX), Math.Abs(y - sourceY)) != radius
                            || (excluded is { } skip && skip == (x, y))
                            || !world.ContainsPosition(0, x, y)
                            || !world.IsWalkable(0, x, y)
                            || world.IsOccupied(0, x, y, participants, player.EntityId))
                            continue;
                        var path = FindPath(sourceX, sourceY, x, y);
                        if (path.Length < minimumLength || CrossesTownWarp(sourceX, sourceY, path))
                            continue;
                        return (x, y);
                    }
                }
            }
            throw new InvalidOperationException("The pinned Town geometry has no bounded open native path target.");
        }

        static bool IsHealingAction(DiabloItemActionKind action)
            => action is DiabloItemActionKind.RestorePartialLife or DiabloItemActionKind.RestoreFullLife
                or DiabloItemActionKind.RestorePartialLifeAndMana or DiabloItemActionKind.RestoreFullLifeAndMana;

        int[] FindPath(int sourceX, int sourceY, int targetX, int targetY)
            => AuthoritativeNativePlayerPathfinder.FindPath(
                sourceX, sourceY, targetX, targetY,
                AuthoritativeNativePlayerPathfinder.MaximumPathLength,
                (x, y) => world.ContainsPosition(0, x, y)
                    && world.IsWalkable(0, x, y)
                    && !world.IsOccupied(0, x, y, participants, player.EntityId),
                (fromX, fromY, toX, toY) => {
                    var dx = toX - fromX;
                    var dy = toY - fromY;
                    return dx == 0 || dy == 0
                        || world.IsWalkable(0, fromX + dx, fromY)
                            && world.IsWalkable(0, fromX, fromY + dy);
                });

        bool CrossesTownWarp(int sourceX, int sourceY, IReadOnlyList<int> directions)
        {
            foreach (var direction in directions) {
                var displacement = AuthoritativeNativePlayerPathfinder.GetDisplacement(direction);
                sourceX = unchecked((byte)(sourceX + displacement.X));
                sourceY = unchecked((byte)(sourceY + displacement.Y));
                if (world.World!.WarpTargets.Any(warp => warp.SourceLevelId == 0
                        && warp.TriggerX == sourceX && warp.TriggerY == sourceY))
                    return true;
            }
            return false;
        }
    }

    [Fact(Explicit = true)]
    public void NativeDepthWalkConsumesOneBlockedDirectionAndRetriesOnceOnCompletion()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x2468ACE0);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.True(activation.TownerGreetings!.TryGet(1, out var pepin));
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: pepin.PositionX + offset.X, Y: pepin.PositionY + offset.Y))
            .First(cell => world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, []));
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        const string session = "native-depth-walk-blocked";
        const string blockerSession = "native-depth-walk-blocker";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, null, null, null, startingLevelId: 0,
            startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(session, player => player.EntityId = 0x70000011);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);

        var blockerCell = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: pepin.PositionX + offset.X, Y: pepin.PositionY + offset.Y))
            .First(cell => cell != start && world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, [player], player.EntityId));
        var blocker = new AuthoritativePlayerState(
            0, 0, player.Life, player.Mana, player.ManaMaximum, player.Attributes,
            player.Equipment.ToArray(), Enumerable.Repeat(-1, 40).ToArray(), [],
            blockerCell.X, blockerCell.Y, player.LifeMaximum, 1, 0, player.ArmorClass,
            characterClassId: player.CharacterClassId,
            playerClass: player.PlayerClass,
            items: player.Items);
        blocker.EntityId = 0x70000012;
        runtime.AddParticipant(blockerSession, blocker);
        var participants = new[] { player, blocker };

        var candidate = FindBlockedCompletionPath(player.PositionX, player.PositionY);
        var path = candidate.Path;
        var firstBlockedStep = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[0]);
        var initiallyBlockedCell = (X: start.X + firstBlockedStep.X,
            Y: start.Y + firstBlockedStep.Y);
        simulation.AdvanceTickWithCommands(2, participants, state => {
            if (!ReferenceEquals(state, player))
                return [];
            Assert.True(simulation.Execute(state,
                new DiabloMoveToIntent(candidate.Goal.X, candidate.Goal.Y, FollowPath: true), 2, participants).Succeeded);
            runtime.Write(blockerSession, value => {
                value.PositionX = initiallyBlockedCell.X;
                value.PositionY = initiallyBlockedCell.Y;
            });
            return [];
        });
        Assert.Null(player.CurrentAction);
        Assert.Equal(path.Skip(1).ToArray(), player.NativeWalkPath!.Directions.ToArray());
        runtime.RestoreAtTick(2);
        runtime.Write(blockerSession, value => {
            value.PositionX = blockerCell.X;
            value.PositionY = blockerCell.Y;
        });

        runtime.AdvanceTo(3);
        var firstMove = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        var firstStep = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[1]);
        var blockedStep = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[2]);
        var completionStep = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[3]);
        var firstCell = (X: start.X + firstStep.X, Y: start.Y + firstStep.Y);
        var blockedCell = (X: firstCell.X + blockedStep.X, Y: firstCell.Y + blockedStep.Y);
        var completionCell = (X: firstCell.X + completionStep.X, Y: firstCell.Y + completionStep.Y);
        Assert.Equal((firstStep.X, firstStep.Y), (firstMove.DirectionX, firstMove.DirectionY));
        Assert.Equal(path.Skip(2).ToArray(), player.NativeWalkPath!.Directions.ToArray());
        Assert.True(world.IsWalkable(0, blockedCell.X, blockedCell.Y));
        Assert.True(world.IsWalkable(0, completionCell.X, completionCell.Y));
        runtime.Write(blockerSession, value => {
            value.PositionX = blockedCell.X;
            value.PositionY = blockedCell.Y;
        });

        runtime.AdvanceTo(firstMove.EndTick);
        var continued = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(firstMove.EndTick, continued.StartedAtTick);
        Assert.Equal((completionStep.X, completionStep.Y), (continued.DirectionX, continued.DirectionY));
        Assert.Equal(firstMove.EndTick
                + (ulong)activation.PlayerAnimations.GetRequired(rogue.ClassId).WalkingFrames + 1UL,
            continued.EndTick);
        Assert.Equal(firstCell, (player.PositionX, player.PositionY));
        Assert.Equal((completionCell.X, completionCell.Y),
            (player.FuturePositionX, player.FuturePositionY));
        Assert.Equal(path.Skip(4).ToArray(), player.NativeWalkPath?.Directions.ToArray() ?? []);

        ((int X, int Y) Goal, int[] Path) FindBlockedCompletionPath(int sourceX, int sourceY)
        {
            for (var radius = 4; radius <= 10; radius++) {
                for (var y = sourceY - radius; y <= sourceY + radius; y++) {
                    for (var x = sourceX - radius; x <= sourceX + radius; x++) {
                        if (Math.Max(Math.Abs(x - sourceX), Math.Abs(y - sourceY)) != radius
                            || !world.ContainsPosition(0, x, y)
                            || !world.IsWalkable(0, x, y)
                            || world.IsOccupied(0, x, y, participants, player.EntityId))
                            continue;
                        var path = FindPath(sourceX, sourceY, x, y);
                        if (path.Length < 5 || CrossesTownWarp(sourceX, sourceY, path))
                            continue;
                        var initiallyBlocked = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[0]);
                        var first = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[1]);
                        var second = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[2]);
                        var third = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[3]);
                        var firstBlockedCell = (X: sourceX + initiallyBlocked.X,
                            Y: sourceY + initiallyBlocked.Y);
                        var firstCell = (X: sourceX + first.X, Y: sourceY + first.Y);
                        var blocked = (X: firstCell.X + second.X, Y: firstCell.Y + second.Y);
                        var continued = (X: firstCell.X + third.X, Y: firstCell.Y + third.Y);
                        if (firstBlockedCell == (sourceX, sourceY) || firstCell == (sourceX, sourceY)
                            || blocked == firstCell || continued == blocked
                            || world.IsOccupied(0, firstBlockedCell.X, firstBlockedCell.Y, participants, player.EntityId)
                            || !world.IsWalkable(0, firstBlockedCell.X, firstBlockedCell.Y)
                            || world.IsOccupied(0, firstCell.X, firstCell.Y, participants, player.EntityId)
                            || !world.IsWalkable(0, firstCell.X, firstCell.Y)
                            || world.IsOccupied(0, blocked.X, blocked.Y, participants, player.EntityId)
                            || !world.IsWalkable(0, blocked.X, blocked.Y)
                            || world.IsOccupied(0, continued.X, continued.Y, participants, player.EntityId)
                            || !world.IsWalkable(0, continued.X, continued.Y)
                            || !NativeCornerAllows(sourceX, sourceY, path[0])
                            || !NativeCornerAllows(sourceX, sourceY, path[1])
                            || !NativeCornerAllows(firstCell.X, firstCell.Y, path[2])
                            || !NativeCornerAllows(firstCell.X, firstCell.Y, path[3]))
                            continue;
                        return ((x, y), path);
                    }
                }
            }
            throw new InvalidOperationException("Pinned Town geometry has no bounded path for the blocked-step case.");
        }

        int[] FindPath(int sourceX, int sourceY, int targetX, int targetY)
            => AuthoritativeNativePlayerPathfinder.FindPath(
                sourceX, sourceY, targetX, targetY,
                AuthoritativeNativePlayerPathfinder.MaximumPathLength,
                (x, y) => world.ContainsPosition(0, x, y)
                    && world.IsWalkable(0, x, y)
                    && !world.IsOccupied(0, x, y, participants, player.EntityId),
                (fromX, fromY, toX, toY) => {
                    var dx = toX - fromX;
                    var dy = toY - fromY;
                    return dx == 0 || dy == 0
                        || world.IsWalkable(0, fromX + dx, fromY)
                            && world.IsWalkable(0, fromX, fromY + dy);
                });

        bool NativeCornerAllows(int sourceX, int sourceY, int direction)
            => direction switch {
                6 => world.IsWalkable(0, sourceX + 1, sourceY),
                8 => world.IsWalkable(0, sourceX, sourceY + 1),
                _ => true,
            };

        bool CrossesTownWarp(int sourceX, int sourceY, IReadOnlyList<int> directions)
        {
            foreach (var direction in directions) {
                var displacement = AuthoritativeNativePlayerPathfinder.GetDisplacement(direction);
                sourceX = unchecked((byte)(sourceX + displacement.X));
                sourceY = unchecked((byte)(sourceY + displacement.Y));
                if (world.World!.WarpTargets.Any(warp => warp.SourceLevelId == 0
                        && warp.TriggerX == sourceX && warp.TriggerY == sourceY))
                    return true;
            }
            return false;
        }
    }
    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDepthQueuedWalkPausesThroughHitRecoveryAndRestartsAtCompletion(bool commandTick)
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.True(activation.TownerGreetings!.TryGet(1, out var pepin));
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: pepin.PositionX + offset.X, Y: pepin.PositionY + offset.Y))
            .First(cell => world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, []));
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var player = activation.PlayerLoadouts.CreateTemplate("native-walk-hit-recovery", rogue.ClassId,
            null, null, null, startingLevelId: 0,
            startingPositionX: start.X, startingPositionY: start.Y).CreatePlayer();
        player.EntityId = 0x70000021;
        var participants = new[] { player };
        var goal = new[] { (X: 4, Y: 0), (X: 0, Y: 4), (X: -4, Y: 0), (X: 0, Y: -4) }
            .Select(offset => (X: start.X + offset.X, Y: start.Y + offset.Y))
            .First(cell => world.ContainsPosition(0, cell.X, cell.Y)
                && world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, participants));
        Assert.True(simulation.Execute(player,
            new DiabloMoveToIntent(goal.X, goal.Y, FollowPath: true), 2, participants).Succeeded);
        var capturedDirections = player.NativeWalkPath!.Directions.ToArray();
        Assert.True(capturedDirections.Length >= 4);
        // A controlled existing recovery mode isolates scheduling from damage RNG.
        player.CombatMode = DiabloPlayerCombatMode.GotHit;
        player.CombatModeEndTick = 5;
        for (ulong tick = 2; tick < 5; tick++) {
            Advance(tick);
            Assert.Equal(DiabloPlayerCombatMode.GotHit, player.CombatMode);
            Assert.Null(player.CurrentAction);
            Assert.Equal(capturedDirections, player.NativeWalkPath!.Directions.ToArray());
            Assert.Equal(start, (player.PositionX, player.PositionY));
        }
        Advance(5);
        var move = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        Assert.Equal(5UL, move.StartedAtTick);
        Assert.Equal(5UL + (ulong)activation.PlayerAnimations.GetRequired(rogue.ClassId).WalkingFrames,
            move.EndTick);
        Assert.Equal(capturedDirections.Skip(1), player.NativeWalkPath!.Directions);
        for (ulong tick = 6; tick <= 160 && (player.CurrentAction is not null || player.NativeWalkPath is not null); tick++)
            Advance(tick);
        Assert.Null(player.CurrentAction);
        Assert.Null(player.NativeWalkPath);
        Assert.Equal(goal, (player.PositionX, player.PositionY));

        void Advance(ulong tick)
        {
            if (commandTick)
                simulation.AdvanceTickWithCommands(tick, participants, _ => []);
            else
                simulation.AdvanceTick(tick, participants);
        }
    }
    [Fact(Explicit = true)]
    public void NativeDepthPathFlagIsRejectedByV9WithoutChangingLegacyMoveTo()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeProgressionContentBootstrap
            .ActivateNativeProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.True(activation.TownerGreetings!.TryGet(1, out var pepin));
        var start = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: pepin.PositionX + offset.X, Y: pepin.PositionY + offset.Y))
            .First(cell => world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, []));
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        const string session = "native-depth-walk-v9";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, null, null, null, startingLevelId: 0,
            startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(session, player => player.EntityId = 0x70000002);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);
        var codec = new DiabloCohortCodec();
        var before = codec.Serialize(simulation.CaptureCohortState(runtime));
        var rejected = simulation.Execute(player,
            new DiabloMoveToIntent(start.X + 1, start.Y, FollowPath: true), 2, [player]);
        Assert.False(rejected.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, rejected.Failure);
        Assert.Equal(before, codec.Serialize(simulation.CaptureCohortState(runtime)));

        var adjacent = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: player.PositionX + offset.X, Y: player.PositionY + offset.Y))
            .First(cell => world.IsWalkable(0, cell.X, cell.Y)
                && !world.IsOccupied(0, cell.X, cell.Y, [player], player.EntityId));
        var legacy = simulation.Execute(player,
            new DiabloMoveToIntent(adjacent.X, adjacent.Y), 2, [player]);
        Assert.True(legacy.Succeeded);
        Assert.Null(player.NativeWalkPath);
        Assert.Equal(DiabloPlayerActionKind.Move, player.CurrentAction?.Kind);
        Assert.Equal((adjacent.X, adjacent.Y), (player.FuturePositionX, player.FuturePositionY));
    }
}
