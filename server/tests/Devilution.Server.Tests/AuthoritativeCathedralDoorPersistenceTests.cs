using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

/** Persistence boundaries for exact Cathedral door geometry and transitions. */
public sealed class AuthoritativeCathedralDoorPersistenceTests
{
    [Fact]
    public void V2CohortCodecRetainsCanonicalGeometryThroughSharedArchAndBlockedClearOperations()
    {
        var source = CreateFixture();
        var runtime = source.Simulation.CreateRuntime(source.Template);
        runtime.AddParticipant("p", source.Player);
        OpenSharedArchHistory(source.Simulation, source.Player);

        var codec = new DiabloCohortCodec();
        var encoded = codec.Serialize(source.Simulation.CaptureCohortState(runtime));
        var decoded = codec.Deserialize(encoded);
        Assert.Equal(2, decoded.FormatVersion);
        Assert.NotNull(decoded.CathedralDoors);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(source.Options, decoded);
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(encoded, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));

        var restoredPlayer = Assert.Single(candidate.Participants).State;
        OperateAndAssertEqual(source.Simulation, source.Player, candidate.Simulation, restoredPlayer,
            DoorId(3, 2));

        source.Player.PositionX = 3;
        source.Player.PositionY = 2;
        restoredPlayer.PositionX = 3;
        restoredPlayer.PositionY = 2;
        OperateAndAssertEqual(source.Simulation, source.Player, candidate.Simulation, restoredPlayer,
            DoorId(3, 2));
        OperateAndAssertEqual(source.Simulation, source.Player, candidate.Simulation, restoredPlayer,
            DoorId(3, 2));
        Assert.Equal(AuthoritativeCathedralDoorStatus.Blocked,
            source.Simulation.WorldState.Objects[DoorId(3, 2)].CathedralDoor!.Status);

        source.Player.PositionX = 1;
        source.Player.PositionY = 3;
        restoredPlayer.PositionX = 1;
        restoredPlayer.PositionY = 3;
        source.Simulation.WorldState.UpdateCathedralDoorOccupancy([source.Player]);
        candidate.Simulation.WorldState.UpdateCathedralDoorOccupancy([restoredPlayer]);
        AssertCanonicalCohortEqual(codec, source.Simulation, runtime,
            candidate.Simulation, restoredRuntime);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open,
            source.Simulation.WorldState.Objects[DoorId(3, 2)].CathedralDoor!.Status);
    }

    [Fact]
    public void SaveV4RestoresMutableDoorsAndProducesCanonicalBytesAfterTheNextOperation()
    {
        var source = CreateFixture();
        OpenSharedArchHistory(source.Simulation, source.Player);
        var codec = new AuthoritativeSaveCodec();
        var original = source.Simulation.CaptureSnapshot(source.Player, 3);

        var encoded = codec.Serialize(original);
        Assert.Equal(AuthoritativeSaveCodec.CathedralDoorFormatVersion,
            System.Text.Json.Nodes.JsonNode.Parse(encoded)!["FormatVersion"]!.GetValue<int>());
        var restored = codec.Deserialize(encoded);
        Assert.NotNull(restored.CathedralDoors);
        Assert.Equal(original.CathedralDepthWorldIdentity!.Levels,
            restored.CathedralDepthWorldIdentity!.Levels);

        var destination = CreateFixture();
        destination.Simulation.RestoreSnapshot(destination.Player, restored);
        Assert.Equal(encoded, codec.Serialize(destination.Simulation.CaptureSnapshot(destination.Player, 3)));

        Assert.True(source.Simulation.WorldState.TryOperateCathedralDoor(
            DoorId(3, 2), [source.Player]));
        Assert.True(destination.Simulation.WorldState.TryOperateCathedralDoor(
            DoorId(3, 2), [destination.Player]));
        Assert.Equal(
            codec.Serialize(source.Simulation.CaptureSnapshot(source.Player, 4)),
            codec.Serialize(destination.Simulation.CaptureSnapshot(destination.Player, 4)));
    }

    [Fact]
    public void V4WireCacheCannotDisagreeWithItsGeometryCheckpoint()
    {
        var fixture = CreateFixture();
        var codec = new AuthoritativeSaveCodec();
        var snapshot = fixture.Simulation.CaptureSnapshot(fixture.Player, 3);
        var document = System.Text.Json.Nodes.JsonNode.Parse(codec.Serialize(snapshot))!;
        var wire = Snapshot.Parser.ParseFrom(Convert.FromBase64String(document["SnapshotBase64"]!.GetValue<string>()));
        wire.StateSha256 = string.Empty;
        wire.Objects[0].CathedralDoor!.CachedNeighborPiece++;
        document["SnapshotBase64"] = Convert.ToBase64String(wire.ToByteArray());
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void DoorWireCannotBeSavedWithoutItsMutableGeometry()
    {
        var fixture = CreateFixture();
        var snapshot = fixture.Simulation.CaptureSnapshot(fixture.Player, 3);
        var downgraded = snapshot with { CathedralDoors = null };
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSaveCodec().Serialize(downgraded));
        Assert.Throws<InvalidDataException>(() => fixture.Simulation.RestoreSnapshot(fixture.Player, downgraded));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V4MetadataCannotBeDowngradedThroughLegacyOrDuplicateVersionKeys(bool duplicateVersion)
    {
        var fixture = CreateFixture();
        var codec = new AuthoritativeSaveCodec();
        var document = System.Text.Json.Nodes.JsonNode.Parse(
            codec.Serialize(fixture.Simulation.CaptureSnapshot(fixture.Player, 0)))!;
        var wire = Snapshot.Parser.ParseFrom(Convert.FromBase64String(document["SnapshotBase64"]!.GetValue<string>()));
        wire.StateSha256 = string.Empty;
        foreach (var item in wire.Objects)
            item.CathedralDoor = null;
        document["SnapshotBase64"] = Convert.ToBase64String(wire.ToByteArray());
        string malformed;
        if (duplicateVersion)
            malformed = document.ToJsonString().Replace("\"FormatVersion\":4", "\"FormatVersion\":4,\"FormatVersion\":1", StringComparison.Ordinal);
        else {
            document["FormatVersion"] = 1;
            malformed = document.ToJsonString();
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(malformed));
    }

    [Theory]
    [InlineData("\"4\"")]
    [InlineData("null")]
    [InlineData("true")]
    public void V4MalformedScalarVersionRetainsInvalidDataExceptionContract(string scalar)
    {
        var fixture = CreateFixture();
        var codec = new AuthoritativeSaveCodec();
        var document = System.Text.Json.Nodes.JsonNode.Parse(
            codec.Serialize(fixture.Simulation.CaptureSnapshot(fixture.Player, 0)))!;
        document["FormatVersion"] = System.Text.Json.Nodes.JsonNode.Parse(scalar);
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DeadPlayerDoorCellRetainsV4AndV2CanonicalStateAndNextOperation(int doorState)
    {
        var source = CreateFixture();
        var runtime = source.Simulation.CreateRuntime(source.Template);
        runtime.AddParticipant("p", source.Player);
        var door = DoorId(2, 3);
        source.Simulation.WorldState.TryOperateCathedralDoor(door, [source.Player]);
        source.Player.PositionX = 2;
        source.Player.Life = 0;
        if (doorState == 0)
            source.Simulation.WorldState.TryOperateCathedralDoor(door, [source.Player]);
        else {
            source.Simulation.WorldState.CombatTargets.Add(88, new AuthoritativeCombatTarget(88, 2, 3, 10, levelId: 1));
            source.Simulation.WorldState.WorldItems.Add(89, new AuthoritativeWorldItem(89, 1, 2, 3, 123, 0,
                AuthoritativeItemState.Empty with { ItemType = 1, ItemIndex = 1 }));
            if (doorState == 2)
                source.Simulation.WorldState.TryOperateCathedralDoor(door, [source.Player]);
        }
        Assert.Equal(doorState, (int)source.Simulation.WorldState.Objects[door].CathedralDoor!.Status);
        var saveCodec = new AuthoritativeSaveCodec();
        var encoded = saveCodec.Serialize(source.Simulation.CaptureSnapshot(source.Player, 0));
        var destination = CreateFixture();
        destination.Simulation.RestoreSnapshot(destination.Player, saveCodec.Deserialize(encoded));
        Assert.Equal(encoded, saveCodec.Serialize(destination.Simulation.CaptureSnapshot(destination.Player, 0)));

        if (doorState != 0) {
            var valid = source.Simulation.CaptureSnapshot(source.Player, 0);
            var monster = valid.Monsters[0];
            var malformed = valid with { Monsters = [.. valid.Monsters,
                monster with { EntityId = 90, PositionX = 0, PositionY = 0 },
                monster with { EntityId = 91, PositionX = 0, PositionY = 0 }] };
            Assert.Throws<InvalidDataException>(() => destination.Simulation.RestoreSnapshot(destination.Player, malformed));
            Assert.Equal(encoded, saveCodec.Serialize(destination.Simulation.CaptureSnapshot(destination.Player, 0)));
        }

        var cohortCodec = new DiabloCohortCodec();
        var cohort = cohortCodec.Serialize(source.Simulation.CaptureCohortState(runtime));
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(source.Options, cohortCodec.Deserialize(cohort));
        var candidateRuntime = candidate.CreateRuntime();
        Assert.Equal(cohort, cohortCodec.Serialize(candidate.Simulation.CaptureCohortState(candidateRuntime)));
        var candidatePlayer = Assert.Single(candidate.Participants).State;
        foreach (var simulation in new[] { source.Simulation, destination.Simulation, candidate.Simulation }) {
            simulation.WorldState.CombatTargets.Clear();
            simulation.WorldState.WorldItems.Clear();
        }
        OperateAndAssertEqual(source.Simulation, source.Player, destination.Simulation, destination.Player, door);
        Assert.True(candidate.Simulation.WorldState.TryOperateCathedralDoor(door, [candidatePlayer]));
        Assert.Equal(source.Simulation.WorldState.Objects[door].CathedralDoor,
            candidate.Simulation.WorldState.Objects[door].CathedralDoor);
        Assert.Equal(saveCodec.Serialize(source.Simulation.CaptureSnapshot(source.Player, 1)),
            saveCodec.Serialize(destination.Simulation.CaptureSnapshot(destination.Player, 1)));
        AssertCanonicalCohortEqual(cohortCodec, source.Simulation, runtime, candidate.Simulation, candidateRuntime);
    }

    private static void OperateAndAssertEqual(
        DiabloSimulation source,
        AuthoritativePlayerState sourcePlayer,
        DiabloSimulation restored,
        AuthoritativePlayerState restoredPlayer,
        uint doorId)
    {
        Assert.True(source.WorldState.TryOperateCathedralDoor(doorId, [sourcePlayer]));
        Assert.True(restored.WorldState.TryOperateCathedralDoor(doorId, [restoredPlayer]));
        Assert.Equal(
            source.WorldState.Objects[doorId].CathedralDoor,
            restored.WorldState.Objects[doorId].CathedralDoor);
        Assert.Equal(
            source.WorldState.LevelGeometry.Single().NativePieceLayer,
            restored.WorldState.LevelGeometry.Single().NativePieceLayer);
        Assert.Equal(
            source.WorldState.LevelGeometry.Single().NativeSpecialLayer,
            restored.WorldState.LevelGeometry.Single().NativeSpecialLayer);
    }

    private static void AssertCanonicalCohortEqual(
        DiabloCohortCodec codec,
        DiabloSimulation source,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> sourceRuntime,
        DiabloSimulation restored,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> restoredRuntime)
        => Assert.Equal(
            codec.Serialize(source.CaptureCohortState(sourceRuntime)),
            codec.Serialize(restored.CaptureCohortState(restoredRuntime)));

    private static void OpenSharedArchHistory(DiabloSimulation simulation, AuthoritativePlayerState player)
    {
        Assert.True(simulation.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]));
        Assert.True(simulation.WorldState.TryOperateCathedralDoor(DoorId(3, 2), [player]));
        Assert.True(simulation.WorldState.TryOperateCathedralDoor(DoorId(2, 3), [player]));
    }

    private static uint DoorId(int x, int y) => DiabloWorldState.GetCathedralDoorEntityId(1, 6, x, y);

    private static Fixture CreateFixture()
    {
        var pieces = new ushort[36];
        pieces[2 * 6 + 2] = 49;
        pieces[2 * 6 + 3] = 45;
        pieces[3 * 6 + 2] = 43;
        var flags = Enumerable.Range(0, 453)
            .ToDictionary(value => (ushort)value, _ => AuthoritativeDunTileFlags.None);
        flags[43] = flags[45] = AuthoritativeDunTileFlags.Solid
            | AuthoritativeDunTileFlags.BlockMissile | AuthoritativeDunTileFlags.BlockLight;
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 6, 6,
            blockedCells: [15, 20], missileBlockedCells: [15, 20],
            isDefaultStart: true, nativePieceLayer: pieces, nativePieceProperties: flags,
            nativePieceCollisionCells: [15, 20], nativePieceMissileBlockedCells: [15, 20],
            startPositionX: 1, startPositionY: 3));
        var identity = CreateDepthIdentity();
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = world,
            CathedralDoors = new AuthoritativeCathedralDoorCatalog([new(1, 1, 2)]),
            CathedralDepthWorldIdentity = identity,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var player = DiabloPlayerTemplate.CreateDefault(0, 64, 0, 0).CreatePlayer();
        player.EntityId = 17;
        player.LevelId = 1;
        player.PositionX = 1;
        player.PositionY = 3;
        return new Fixture(options, simulation, player,
            DiabloPlayerTemplate.CreateDefault(0, 64, 0, 1));
    }

    private static DiabloCathedralDepthWorldIdentity CreateDepthIdentity()
        => new(Enumerable.Range(1, 4).Select(depth => new AuthoritativeCathedralDepthGenerationIdentity(
            AuthoritativeCathedralDepthRuntimeCatalog.CatalogId,
            AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId, 1,
            (uint)depth, depth == 1 ? "cathedral" : $"cathedral_depth_{depth}", depth,
            AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
            depth == 4 ? uint.MaxValue : (uint)depth, AuthoritativeCathedralEntry.Main,
            (uint)depth, 123, new string('A', 64), new string('B', 64),
            new string('C', 64), new string('D', 64))).ToArray(), new string('E', 64));

    private sealed record Fixture(
        DiabloSimulationOptions Options,
        DiabloSimulation Simulation,
        AuthoritativePlayerState Player,
        DiabloPlayerTemplate Template);
}
