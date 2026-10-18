extern alias ServerProtocol;

using Devilution.Engine;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralDoorRuntimeTests
{
    [Fact]
    public void ActualCommandOpensAndClosesWithoutOneShotActivation()
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        simulation.WorldState.ValidateInitialPlayerPosition(1, 1, 3);
        var processor = new DiabloWorldCommandProcessor(simulation.WorldState);
        var id = DoorId(2, 3);
        var initial = Assert.Single(simulation.WorldState.LevelGeometry);
        Assert.False(initial.IsWalkable(2, 3));
        Assert.True(processor.Execute(player, new DiabloOperateObjectIntent(id), 1, [player]).Succeeded);
        Assert.True(Assert.Single(simulation.WorldState.LevelGeometry).IsWalkable(2, 3));
        Assert.False(simulation.WorldState.Objects[id].Activated);
        Assert.True(simulation.WorldState.Objects[id].HasAuthoritativeInteraction);
        Assert.True(processor.Execute(player, new DiabloOperateObjectIntent(id), 2, [player]).Succeeded);
        Assert.Equal(initial.NativePieceLayer, Assert.Single(simulation.WorldState.LevelGeometry).NativePieceLayer);
    }

    [Fact]
    public void PresentationCaptureKeepsTypedDoorsWithoutCopyingPersistenceGeometry()
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        var presentation = simulation.CapturePresentationSnapshot(player, 0);
        Assert.Null(presentation.CathedralDoors);
        Assert.Equal(simulation.WorldState.CaptureCathedralDoorObjects(), presentation.CathedralDoorObjects);
        Assert.NotNull(simulation.CaptureSnapshot(player, 0).CathedralDoors);
    }

    [Fact]
    public void OccupiedCloseAndAutomaticOccupancyPreserveGeometryAndAnimation()
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        var id = DoorId(2, 3);
        Assert.True(simulation.WorldState.TryOperateCathedralDoor(id, [player]));
        player.PositionX = 2;
        var open = Assert.Single(simulation.WorldState.LevelGeometry);
        var frame = simulation.WorldState.Objects[id].AnimationFrame;
        Assert.True(simulation.WorldState.TryOperateCathedralDoor(id, [player]));
        Assert.Equal(AuthoritativeCathedralDoorStatus.Blocked, simulation.WorldState.Objects[id].CathedralDoor!.Status);
        Assert.Same(open, Assert.Single(simulation.WorldState.LevelGeometry));
        Assert.Equal(frame, simulation.WorldState.Objects[id].AnimationFrame);
        player.PositionX = 1;
        simulation.WorldState.UpdateCathedralDoorOccupancy([player]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open, simulation.WorldState.Objects[id].CathedralDoor!.Status);
        Assert.True(simulation.WorldState.TryOperateCathedralDoor(id, [player]));
        Assert.Equal(AuthoritativeCathedralDoorStatus.Closed, simulation.WorldState.Objects[id].CathedralDoor!.Status);
    }

    [Fact]
    public void FreshSnapshotRestoreRetainsSharedArchHistoryAndNextOperation()
    {
        var original = CreateSimulation(out _);
        var player = CreatePlayer();
        OpenSharedArchHistory(original, player);
        var snapshot = original.CaptureSnapshot(player, 3);
        var restored = CreateSimulation(out _);
        var destination = CreatePlayer();
        restored.RestoreSnapshot(destination, snapshot);
        AssertEquivalent(original, restored);
        original.WorldState.TryOperateCathedralDoor(DoorId(3, 2), [player]);
        restored.WorldState.TryOperateCathedralDoor(DoorId(3, 2), [destination]);
        AssertEquivalent(original, restored);
    }

    [Fact]
    public void FreshCohortCandidateRetainsBlockedDoorAndPlayerInOpenDoorway()
    {
        var original = CreateSimulation(out var options);
        var player = CreatePlayer();
        var runtime = original.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 64, 0, 1));
        runtime.AddParticipant("p", player);
        original.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]);
        player.PositionX = 2;
        original.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]);
        var state = original.CaptureCohortState(runtime);
        Assert.Equal(2, state.FormatVersion);
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(options, state);
        var restoredRuntime = candidate.CreateRuntime();
        AssertEquivalent(original, candidate.Simulation);
        var recaptured = candidate.Simulation.CaptureCohortState(restoredRuntime);
        Assert.Equal(2, recaptured.Participants[0].Player.PositionX);
        Assert.Equal(2, recaptured.CathedralDoors!.DoorLevels[0].Doors.Single(value => value.EntityId == DoorId(2, 3)).DoorState);
    }

    [Fact]
    public void MalformedDoorRestoreRollsBackWithoutChangingLiveGeometry()
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        OpenSharedArchHistory(simulation, player);
        var snapshot = simulation.CaptureSnapshot(player, 3);
        var level = snapshot.CathedralDoors!.DoorLevels[0];
        var doors = level.Doors.ToArray();
        doors[0] = doors[0] with { CachedNeighborPiece = 999 };
        var malformed = snapshot with {
            CathedralDoors = snapshot.CathedralDoors with { DoorLevels = [level with { Doors = doors }] },
        };
        var pieces = Assert.Single(simulation.WorldState.LevelGeometry).NativePieceLayer.ToArray();
        Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player, malformed));
        Assert.Equal(pieces, Assert.Single(simulation.WorldState.LevelGeometry).NativePieceLayer);
    }

    [Fact]
    public void OpenDoorwayAdmitsThePlayerAndCannotRelocateLiveCaches()
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        var id = DoorId(2, 3);
        simulation.WorldState.TryOperateCathedralDoor(id, [player]);
        simulation.WorldState.ValidateInitialPlayerPosition(1, 2, 3);
        var door = simulation.WorldState.Objects[id];
        Assert.Throws<InvalidDataException>(() => door.WithPlacement(id, 1, 1, 3));
        Assert.Equal(door.CathedralDoor, door.WithPlacement(id, 1, 2, 3).CathedralDoor);
        Assert.False(simulation.WorldState.IsCathedralDoorItemBlocked(1, 2, 3));
        Assert.True(simulation.WorldState.IsCathedralDoorItemBlocked(1, 1, 2));
        Assert.True(simulation.WorldState.IsCathedralDoorItemBlocked(1, 2, 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DyingAndDeadPetrifiedMonstersKeepOpenDoorsBlocked(bool petrified)
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        var id = DoorId(2, 3);
        simulation.WorldState.TryOperateCathedralDoor(id, [player]);
        var monster = new AuthoritativeCombatTarget(88, 2, 3, 0,
            levelId: 1, petrified: petrified, deathAnimationTicks: 2);
        if (!petrified)
            monster.RestoreDeathAnimation(1);
        simulation.WorldState.CombatTargets.Add(monster.EntityId, monster);
        simulation.WorldState.TryOperateCathedralDoor(id, [player]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Blocked,
            simulation.WorldState.Objects[id].CathedralDoor!.Status);
        simulation.WorldState.CombatTargets.Clear();
        simulation.WorldState.UpdateCathedralDoorOccupancy([player]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open,
            simulation.WorldState.Objects[id].CathedralDoor!.Status);
    }

    [Fact]
    public void DeadPlayerWithoutDropsDoesNotLeaveAnOccupiedDoorMarker()
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        var id = DoorId(2, 3);
        simulation.WorldState.TryOperateCathedralDoor(id, [player]);
        player.PositionX = 2;
        player.Life = 0;
        simulation.WorldState.UpdateCathedralDoorOccupancy([player]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open,
            simulation.WorldState.Objects[id].CathedralDoor!.Status);
        simulation.WorldState.TryOperateCathedralDoor(id, [player]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Closed,
            simulation.WorldState.Objects[id].CathedralDoor!.Status);
        simulation.WorldState.TryOperateCathedralDoor(id, [player]);
        Assert.False(simulation.WorldState.IsOccupied(1, 2, 3, [player]));
        var live = CreatePlayer();
        live.EntityId = 18;
        Assert.True(simulation.Execute(live, new DiabloMoveIntent(1, 0), 1, [player, live]).Succeeded);
        Assert.Equal(2, live.PositionX);
    }

    [Theory]
    [InlineData("backing-level")]
    [InlineData("missing-special")]
    [InlineData("special")]
    [InlineData("owned-piece")]
    [InlineData("other-piece")]
    [InlineData("sol")]
    [InlineData("blocked")]
    [InlineData("transparency")]
    [InlineData("town-blocked")]
    [InlineData("breakable")]
    [InlineData("quest")]
    [InlineData("occupied-activation")]
    [InlineData("open-frame-zero")]
    [InlineData("closed-frame-overflow")]
    [InlineData("open-status-closed-piece")]
    [InlineData("closed-status-open-piece")]
    public void MalformedCheckpointsFailWithoutChangingLiveDoorState(string defect)
    {
        var simulation = CreateSimulation(out _, includeTown: true);
        var player = CreatePlayer();
        OpenSharedArchHistory(simulation, player);
        var snapshot = simulation.CaptureSnapshot(player, 3);
        var checkpoint = snapshot.CathedralDoors!;
        var saved = checkpoint.DoorLevels[0];
        var effective = checkpoint.EffectiveLevels.ToArray();
        var levelIndex = Array.FindIndex(effective, level => level.LevelId == 1);
        var level = effective[levelIndex];
        var objects = snapshot.Objects.ToArray();
        var id = DoorId(2, 3);
        var objectIndex = Array.FindIndex(objects, value => value.EntityId == id);
        switch (defect) {
        case "backing-level":
            checkpoint = checkpoint with { BackingGeometry = checkpoint.BackingGeometry with {
                Levels = [.. checkpoint.BackingGeometry.Levels!, level with { LevelId = 9 }],
            } };
            break;
        case "missing-special":
            checkpoint = checkpoint with { DoorLevels = [saved with { EffectiveSpecial = [] }] };
            break;
        case "special":
            var special = saved.EffectiveSpecial.ToArray();
            special[0] = 9;
            checkpoint = checkpoint with { DoorLevels = [saved with { EffectiveSpecial = special }] };
            break;
        case "owned-piece":
        case "other-piece":
            var pieces = level.NativePieceLayer!.ToArray();
            pieces[defect == "owned-piece" ? 14 : 0] = 17;
            effective[levelIndex] = level with { NativePieceLayer = pieces };
            checkpoint = checkpoint with { EffectiveLevels = effective };
            break;
        case "open-status-closed-piece":
        case "closed-status-open-piece":
            var mainPieces = level.NativePieceLayer!.ToArray();
            var cell = defect == "open-status-closed-piece" ? 15 : 20;
            mainPieces[cell] = (ushort)(defect == "open-status-closed-piece" ? 45 : 392);
            var solidCells = mainPieces.Select((piece, index) => (piece, index))
                .Where(value => flagsFor(value.piece)).Select(value => value.index).ToArray();
            bool flagsFor(ushort piece) => level.NativePieceProperties![piece].HasFlag(AuthoritativeDunTileFlags.Solid);
            effective[levelIndex] = level with {
                NativePieceLayer = mainPieces, BlockedCells = solidCells,
                MissileBlockedCells = solidCells, NativePieceCollisionCells = solidCells,
                NativePieceMissileBlockedCells = solidCells, NativeVisionBlockedCells = solidCells,
            };
            checkpoint = checkpoint with { EffectiveLevels = effective };
            break;
        case "sol":
            var properties = level.NativePieceProperties!.ToDictionary();
            properties[392] = AuthoritativeDunTileFlags.Solid;
            effective[levelIndex] = level with { NativePieceProperties = properties };
            checkpoint = checkpoint with { EffectiveLevels = effective };
            break;
        case "blocked":
            effective[levelIndex] = level with { BlockedCells = [0, .. level.BlockedCells!] };
            checkpoint = checkpoint with { EffectiveLevels = effective };
            break;
        case "transparency":
            effective[levelIndex] = level with { NativeTransparency = new ushort[36] };
            checkpoint = checkpoint with { EffectiveLevels = effective };
            break;
        case "town-blocked":
            var townIndex = Array.FindIndex(effective, candidate => candidate.LevelId == 0);
            effective[townIndex] = effective[townIndex] with { BlockedCells = [] };
            checkpoint = checkpoint with { EffectiveLevels = effective };
            break;
        case "breakable": objects[objectIndex] = objects[objectIndex] with { Breakable = true }; break;
        case "quest": objects[objectIndex] = objects[objectIndex] with { QuestId = 99 }; break;
        case "occupied-activation": objects[objectIndex] = objects[objectIndex] with { ActivateOnOccupancy = true }; break;
        case "open-frame-zero":
        case "closed-frame-overflow":
            var states = saved.Doors.ToArray();
            var index = Array.FindIndex(states, value => value.EntityId == (defect == "open-frame-zero" ? DoorId(3, 2) : id));
            var frame = defect == "open-frame-zero" ? 0 : 255;
            states[index] = states[index] with { AnimationFrame = frame };
            objectIndex = Array.FindIndex(objects, value => value.EntityId == states[index].EntityId);
            objects[objectIndex] = objects[objectIndex] with { AnimationFrame = frame };
            checkpoint = checkpoint with { DoorLevels = [saved with { Doors = states }] };
            break;
        }
        var malformed = snapshot with {
            CathedralDoors = checkpoint, Objects = objects,
            CathedralDoorObjects = checkpoint.DoorLevels.SelectMany(value => value.Doors).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player, malformed));
        var restored = simulation.CaptureSnapshot(player, 3).CathedralDoors!;
        Assert.Equal(snapshot.CathedralDoors!.DoorLevels[0].Doors, restored.DoorLevels[0].Doors);
        Assert.Equal(snapshot.CathedralDoors.EffectiveLevels[levelIndex].NativePieceLayer,
            restored.EffectiveLevels[levelIndex].NativePieceLayer);
    }

    [Fact]
    public void CohortDuplicatedGeometryCannotNormalizeConflictingCopies()
    {
        var simulation = CreateSimulation(out var options);
        var player = CreatePlayer();
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 64, 0, 1));
        runtime.AddParticipant("p", player);
        var state = simulation.CaptureCohortState(runtime);
        var conflicting = state.WorldInfrastructure.EffectiveLevels!.ToArray();
        conflicting[0] = conflicting[0] with { BlockedCells = [] };
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.CreateCohortCandidate(options,
            state with { WorldInfrastructure = state.WorldInfrastructure with { EffectiveLevels = conflicting } }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsolatedDoorFramesCannotRetainTheOppositeStatusPiece(bool opened)
    {
        var simulation = CreateSimulation(out _, sharedArch: false);
        var player = CreatePlayer();
        if (opened)
            simulation.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]);
        var snapshot = simulation.CaptureSnapshot(player, 1);
        var checkpoint = snapshot.CathedralDoors!;
        var effective = Assert.Single(checkpoint.EffectiveLevels);
        var pieces = effective.NativePieceLayer!.ToArray();
        pieces[14] = (ushort)(opened ? 49 : 410);
        var malformed = snapshot with { CathedralDoors = checkpoint with {
            EffectiveLevels = [effective with { NativePieceLayer = pieces }],
        } };
        Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player, malformed));
        Assert.Equal(opened ? (ushort)410 : (ushort)49,
            Assert.Single(simulation.WorldState.LevelGeometry).NativePieceLayer[14]);
    }

    [Fact]
    public void DoorProfileRejectsUnsupportedMutationsAndUnmappedSpecialLayersBeforeInitialization()
    {
        _ = CreateSimulation(out var options);
        var mutationOptions = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = options.World, CathedralDoors = options.CathedralDoors,
            WorldMutations = new AuthoritativeWorldMutationCatalog([new(9, 1)]),
        };
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(mutationOptions));
        options.World!.AddLevel(new AuthoritativeLevel(0, 2, 2, nativeSpecialLayer: new ushort[4]));
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(options));
    }

    [Fact]
    public void ScannerLightsBlockMovementItemsAndPlayerAdmissionWithoutBecomingInteractiveDoors()
    {
        var simulation = CreateSimulation(out _, includeLight: true);
        Assert.Equal(2, simulation.WorldState.Objects.Count);
        Assert.True(simulation.WorldState.IsOccupied(1, 4, 4, []));
        Assert.True(simulation.WorldState.IsCathedralDoorItemBlocked(1, 4, 4));
        Assert.False(simulation.WorldState.IsCathedralDoorItemBlocked(1, 3, 3));
        Assert.DoesNotContain(28, Assert.Single(simulation.WorldState.LevelGeometry).BlockedCells);
        var presentation = Assert.Single(simulation.WorldState.CapturePresentationLevelGeometry());
        Assert.Contains(28, presentation.StaticObjectBlockedCells);
        Assert.DoesNotContain(28, presentation.BlockedCells);
        Assert.DoesNotContain(28, presentation.NativeVisionBlockedCells);
        Assert.DoesNotContain(28, presentation.MissileBlockedCells);
        Assert.DoesNotContain(28, Assert.Single(simulation.CaptureSnapshot(CreatePlayer(), 0)
            .CathedralDoors!.EffectiveLevels).BlockedCells!);
        Assert.Throws<InvalidDataException>(() => simulation.WorldState.ValidateInitialPlayerPosition(1, 4, 4));
    }

    [Fact]
    public void PresentationGeometryCachePreservesWireBytesAcrossDoorAndColdRestoreChanges()
    {
        var original = CreateSimulation(out _, includeLight: true);
        var player = CreatePlayer();
        var firstGeometry = original.WorldState.CapturePresentationLevelGeometry();
        var repeatedGeometry = original.WorldState.CapturePresentationLevelGeometry();
        Assert.NotSame(firstGeometry, repeatedGeometry);
        var originalLevel = Assert.Single(firstGeometry);
        Assert.Same(originalLevel, Assert.Single(repeatedGeometry));
        const int scannerLightCell = 4 * 6 + 4;
        Assert.Contains(scannerLightCell, originalLevel.StaticObjectBlockedCells);

        var originalWireBytes = CaptureProtocolBytes(original, player, 0, firstGeometry);
        Assert.Equal(originalWireBytes, CaptureProtocolBytes(original, player, 0, repeatedGeometry));

        Assert.True(original.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]));
        var openedSnapshot = original.CaptureSnapshot(player, 1);
        var openedGeometry = original.WorldState.CapturePresentationLevelGeometry();
        var openedLevel = Assert.Single(openedGeometry);
        Assert.NotSame(originalLevel, openedLevel);
        Assert.Contains(scannerLightCell, openedLevel.StaticObjectBlockedCells);
        var openedWireBytes = CaptureProtocolBytes(original, player, 1, openedGeometry);
        Assert.Equal(openedWireBytes, CaptureProtocolBytes(original, player, 1,
            original.WorldState.CapturePresentationLevelGeometry()));

        var restored = CreateSimulation(out _, includeLight: true);
        var destination = CreatePlayer();
        var staleGeometry = restored.WorldState.CapturePresentationLevelGeometry();
        Assert.Same(Assert.Single(staleGeometry),
            Assert.Single(restored.WorldState.CapturePresentationLevelGeometry()));
        restored.RestoreSnapshot(destination, openedSnapshot);
        var restoredGeometry = restored.WorldState.CapturePresentationLevelGeometry();
        var restoredLevel = Assert.Single(restoredGeometry);
        Assert.NotSame(Assert.Single(staleGeometry), restoredLevel);
        Assert.Contains(scannerLightCell, restoredLevel.StaticObjectBlockedCells);
        Assert.Equal(openedWireBytes, CaptureProtocolBytes(restored, destination, 1, restoredGeometry));
    }

    private static byte[] CaptureProtocolBytes(DiabloSimulation simulation, AuthoritativePlayerState player,
        ulong tick, IReadOnlyList<AuthoritativeLevel> geometry)
    {
        var presentation = simulation.CapturePresentationSnapshot(player, tick);
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(presentation, levelGeometry: geometry,
            spells: simulation.Spells, spellBooks: simulation.SpellBooks, items: simulation.Items);
        return snapshot.ToByteArray();
    }

    [Fact]
    public void SnapshotAndCohortRestoreRejectPlayerInsideScannerLight()
    {
        var simulation = CreateSimulation(out var options, includeLight: true);
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 0);
        var malformed = snapshot with { Player = snapshot.Player with { PositionX = 4, PositionY = 4 } };
        Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player, malformed));
        Assert.Equal(1, player.PositionX);
        Assert.Equal(3, player.PositionY);
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 64, 0, 1));
        runtime.AddParticipant("p", player);
        var cohort = simulation.CaptureCohortState(runtime);
        var participant = cohort.Participants[0];
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.CreateCohortCandidate(options,
            cohort with { Participants = [participant with {
                Player = participant.Player with { PositionX = 4, PositionY = 4 },
            }] }));
    }

    [Fact]
    public void SnapshotAndCohortRestoreRejectPendingLiveMoveIntoFixedScannerLight()
    {
        var simulation = CreateSimulation(out var options, includeLight: true);
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 0);
        var pendingPlayer = snapshot.Player with {
            PositionX = 3, PositionY = 4,
            CombatMode = DiabloPlayerCombatMode.WalkSideways,
            CombatModeEndTick = 3,
            CurrentAction = new AuthoritativePlayerActionState(DiabloPlayerActionKind.Move, 0, 1, 2, 3) {
                DirectionX = 1,
            },
        };
        var malformed = snapshot with { Player = pendingPlayer };
        Assert.Contains("light", Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(player, malformed)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, player.PositionX);
        Assert.Equal(3, player.PositionY);
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 64, 0, 1));
        runtime.AddParticipant("p", player);
        var cohort = simulation.CaptureCohortState(runtime);
        var participant = cohort.Participants[0];
        Assert.Contains("light", Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.CreateCohortCandidate(options,
                cohort with { Participants = [participant with { Player = pendingPlayer }] })).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(simulation.WorldState.IsPendingPlayerMoveIntoCathedralLight(0, 1, 3, 4, pendingPlayer.CurrentAction));
        Assert.False(simulation.WorldState.IsPendingPlayerMoveIntoCathedralLight(64, 1, 3, 4,
            pendingPlayer.CurrentAction! with { EffectApplied = true }));
    }

    [Fact]
    public void ClosedSharedDoorsCannotRestoreAnOpenOnly395Arch()
    {
        var simulation = CreateSimulation(out _);
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 0);
        var checkpoint = snapshot.CathedralDoors!;
        var effective = Assert.Single(checkpoint.EffectiveLevels);
        var pieces = effective.NativePieceLayer!.ToArray();
        pieces[14] = 395;
        var malformed = snapshot with { CathedralDoors = checkpoint with {
            EffectiveLevels = [effective with { NativePieceLayer = pieces }],
        } };
        Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player, malformed));
        Assert.Equal(49, Assert.Single(simulation.WorldState.LevelGeometry).NativePieceLayer[14]);
    }

    [Theory]
    [InlineData(124, true)]
    [InlineData(125, false)]
    public void RestoreCountsStaticLightsAlongsideSavedDoorsAndOtherObjects(int otherObjects, bool accepted)
    {
        var simulation = CreateSimulation(out _, includeLight: true, width: 16);
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 0);
        var extras = Enumerable.Range(0, 16 * 16)
            .Where(cell => cell != 3 * 16 + 1 && cell != 3 * 16 + 2
                && cell != 2 * 16 + 3 && cell != 4 * 16 + 4)
            .Take(otherObjects).Select(cell => new DiabloObjectState(
                (uint)(1000 + cell), 3, 1, cell % 16, cell / 16, false, 0, AuthoritativeObjectEffectKind.None, 0));
        var candidate = snapshot with { Objects = [.. snapshot.Objects, .. extras] };
        if (accepted) {
            simulation.RestoreSnapshot(player, candidate);
            Assert.Equal(126, simulation.WorldState.Objects.Count);
        } else {
            Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player, candidate));
            Assert.Equal(2, simulation.WorldState.Objects.Count);
        }
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(4, 4)]
    public void DoorProfileRejectsOverlappingAuthoredScannerObjects(int x, int y)
    {
        _ = CreateSimulation(out var source, includeLight: true);
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = source.World, CathedralDoors = source.CathedralDoors,
            Objects = [new AuthoritativeWorldObject(88, 3, 1, x, y)],
        };
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(options));
        var fresh = CreateSimulation(out _, includeLight: true);
        var player = CreatePlayer();
        var snapshot = fresh.CaptureSnapshot(player, 0);
        Assert.Throws<InvalidDataException>(() => fresh.RestoreSnapshot(player, snapshot with {
            Objects = [.. snapshot.Objects, new DiabloObjectState(88, 3, 1, x, y, false, 0,
                AuthoritativeObjectEffectKind.None, 0)],
        }));
        Assert.Equal(2, fresh.WorldState.Objects.Count);
    }

    private static void OpenSharedArchHistory(DiabloSimulation simulation, AuthoritativePlayerState player)
    {
        simulation.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]);
        simulation.WorldState.TryOperateCathedralDoor(DoorId(3, 2), [player]);
        simulation.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]);
        Assert.Equal(411, Assert.Single(simulation.WorldState.LevelGeometry,
            value => value.LevelId == 1).NativePieceLayer[2 * 6 + 2]);
    }

    private static void AssertEquivalent(DiabloSimulation left, DiabloSimulation right)
    {
        var first = Assert.Single(left.WorldState.LevelGeometry);
        var second = Assert.Single(right.WorldState.LevelGeometry);
        Assert.Equal(first.NativePieceLayer, second.NativePieceLayer);
        Assert.Equal(first.NativeSpecialLayer, second.NativeSpecialLayer);
        Assert.Equal(first.BlockedCells, second.BlockedCells);
        Assert.Equal(first.MissileBlockedCells, second.MissileBlockedCells);
        Assert.Equal(first.NativeVisionBlockedCells, second.NativeVisionBlockedCells);
        foreach (var (id, value) in left.WorldState.Objects)
            Assert.Equal(value.CathedralDoor, right.WorldState.Objects[id].CathedralDoor);
    }

    private static uint DoorId(int x, int y) => DiabloWorldState.GetCathedralDoorEntityId(1, 6, x, y);

    private static AuthoritativePlayerState CreatePlayer()
    {
        var player = DiabloPlayerTemplate.CreateDefault(0, 64, 0, 0).CreatePlayer();
        player.EntityId = 17;
        player.LevelId = 1;
        player.PositionX = 1;
        player.PositionY = 3;
        return player;
    }

    private static DiabloSimulation CreateSimulation(out DiabloSimulationOptions options,
        bool includeTown = false, bool sharedArch = true, bool includeLight = false, int width = 6)
    {
        var pieces = new ushort[width * width];
        pieces[2 * width + 2] = 49;
        pieces[2 * width + 3] = sharedArch ? (ushort)45 : (ushort)0;
        pieces[3 * width + 2] = 43;
        if (includeLight)
            pieces[4 * width + 4] = 269;
        var flags = Enumerable.Range(0, 453).ToDictionary(value => (ushort)value, _ => AuthoritativeDunTileFlags.None);
        flags[43] = flags[45] = AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile | AuthoritativeDunTileFlags.BlockLight;
        var world = new AuthoritativeWorld();
        if (includeTown)
            world.AddLevel(new AuthoritativeLevel(0, 2, 2, blockedCells: [1]));
        int[] blockers = sharedArch ? [2 * width + 3, 3 * width + 2] : [3 * width + 2];
        world.AddLevel(new AuthoritativeLevel(1, width, width,
            blockedCells: blockers, missileBlockedCells: blockers,
            isDefaultStart: true, nativePieceLayer: pieces, nativePieceProperties: flags,
            nativePieceCollisionCells: blockers, nativePieceMissileBlockedCells: blockers,
            startPositionX: 1, startPositionY: 3));
        options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = world,
            CathedralDoors = new AuthoritativeCathedralDoorCatalog([new(1, 1, 2)]),
        };
        return DiabloSimulationFactory.Instance.Create(options);
    }
}
