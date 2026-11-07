using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ResponsiveMovementOccupancyTests
{
    // AuthoritativeNativeTownRoster / native InitCows: humans occupy one cell, cows a 2x2 NW footprint.
    private static readonly (int Type, int X, int Y)[] Residents = [
        (0, 62, 63), (1, 55, 79), (3, 55, 62), (4, 62, 71), (5, 71, 84),
        (6, 80, 20), (7, 43, 66), (8, 11, 53), (9, 58, 16), (9, 56, 14), (9, 59, 20),
    ];

    public static IEnumerable<object[]> ResidentCells()
    {
        foreach (var (type, x, y) in Residents)
            for (var dx = type == 9 ? -1 : 0; dx <= 0; dx++)
                for (var dy = type == 9 ? -1 : 0; dy <= 0; dy++)
                    foreach (var click in new[] { false, true })
                        yield return [type, x, y, dx, dy, click];
    }

    [Theory]
    [MemberData(nameof(ResidentCells))]
    public void EveryResidentAndCowFootprintCellStopsClickAndHeldPrediction(
        int type, int x, int y, int dx, int dy, bool click)
    {
        var target = new ClientPosition(x + dx, y + dy);
        var source = new ClientPosition(target.X + (dx == -1 ? -1 : 1), target.Y);
        var snapshot = SnapshotAt(source.X, source.Y);
        snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = type, PositionX = x, PositionY = y });
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        Predict(model, target, click);

        Assert.Null(model.LocalMovementTarget);
        Assert.Null(model.PredictedPlayerPosition);
        var visual = new AuthoritativeLocalPlayerPresentation();
        Assert.Equal(new ClientPresentationPosition(source.X, source.Y),
            visual.Advance(model.Snapshot!.Players[0], snapshot.Tick, model.LocalMovementTarget, 1d / 60, 20));
    }

    [Theory]
    [InlineData(58, 16)] [InlineData(56, 14)] [InlineData(59, 20)]
    public void ClickPastACowFollowsTheNativePathAroundItsWholeFootprint(int x, int y)
    {
        var snapshot = SnapshotAt(x - 2, y);
        snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = 9, PositionX = x, PositionY = y });
        var occupied = new HashSet<(int, int)> { (x, y), (x - 1, y), (x, y - 1), (x - 1, y - 1) };
        var path = AuthoritativeNativePlayerPathfinder.FindPath(x - 2, y, x + 2, y, 100,
            (px, py) => px >= 0 && py >= 0 && px < 112 && py < 112 && !occupied.Contains((px, py)),
            (_, _, _, _) => true);
        Assert.NotEmpty(path);
        var step = AuthoritativeNativePlayerPathfinder.GetDisplacement(path[0]);
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        model.TrackPredictedMoveTo(1, x + 2, y);

        var target = Assert.IsType<ClientPosition>(model.LocalMovementTarget);
        Assert.Equal(new ClientPosition(x - 2 + step.X, y + step.Y), target);
        Assert.DoesNotContain((target.X, target.Y), occupied);
    }

    [Theory]
    [InlineData(false, false, true)] [InlineData(true, false, true)] [InlineData(true, true, true)]
    [InlineData(false, false, false)] [InlineData(true, false, false)] [InlineData(true, true, false)]
    public void RemotePlayersOccupyBothTheirOriginAndReservedDestination(bool moving, bool destination, bool click)
    {
        var snapshot = SnapshotAt(50, 50);
        var other = Player(destination ? 52 : 51, 50);
        other.EntityId = 2;
        if (moving) {
            other.CombatMode = PlayerCombatMode.WalkSouthwards;
            other.CombatModeEndTick = 18;
            other.CurrentAction = new() { Kind = PlayerActionKind.Move, StartedAtTick = 10,
                EffectTick = 18, ReadyTick = 18, EndTick = 18, DirectionX = destination ? -1 : 1 };
        }
        snapshot.Players.Add(other);
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        Predict(model, new(51, 50), click);

        Assert.Null(model.LocalMovementTarget);
        Assert.Null(model.PredictedPlayerPosition);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeadPeerOccupancyMatchesTheServersDoorException(bool onOpenDoor)
    {
        var snapshot = SnapshotAt(50, 50);
        var peer = Player(51, 50);
        peer.EntityId = 2; peer.Life = 0;
        snapshot.Players.Add(peer);
        if (onOpenDoor) snapshot.Objects.Add(OpenDoor(51, 50));
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        model.TrackPredictedMoveTo(1, 51, 50);

        Assert.Equal(onOpenDoor ? new ClientPosition(51, 50) : null, model.LocalMovementTarget);
    }

    [Theory]
    [InlineData(false, false, 0U, true)] [InlineData(true, false, 0U, true)]
    [InlineData(true, true, 0U, true)] [InlineData(false, false, 1U, false)]
    [InlineData(false, false, 0U, false)]
    public void MonstersReserveBothCellsButDeadMonstersAndOtherLevelsDoNotBlock(
        bool moving, bool destination, uint level, bool alive)
    {
        var snapshot = SnapshotAt(50, 50);
        snapshot.Monsters.Add(new MonsterSnapshot { EntityId = 2, LevelId = level, PositionX = destination ? 52 : 51,
            PositionY = 50, FuturePositionX = moving && !destination ? 52 : 51, FuturePositionY = 50, Moving = moving,
            MovementStartedTick = moving ? 10UL : 0, MovementEffectTick = moving ? 18UL : 0,
            HitPoints = alive ? 1 : 0, MaxHitPoints = 1, Alive = alive });
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        model.TrackPredictedMoveTo(1, 51, 50);

        Assert.Equal(alive && level == 0 ? null : new ClientPosition(51, 50), model.LocalMovementTarget);
    }

    [Fact]
    public void LegacyLevelZeroMonsterOccupancyMatchesTheServersWildcard()
    {
        var snapshot = SnapshotAt(50, 50);
        snapshot.Players[0].LevelId = 2;
        snapshot.Monsters.Add(new MonsterSnapshot { EntityId = 2, PositionX = 51, PositionY = 50,
            HitPoints = 1, MaxHitPoints = 1, Alive = true });
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        model.TrackPredictedMoveTo(1, 51, 50);

        Assert.Null(model.LocalMovementTarget);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProjectedSolidObjectFootprintsRemainBlockedAfterActivation(bool activated)
    {
        var snapshot = SnapshotAt(50, 50);
        snapshot.LevelGeometry.Add(new LevelGeometrySnapshot { Width = 112, Height = 112,
            StaticObjectBlockedCells = { 50 * 112 + 51, 50 * 112 + 52 } });
        snapshot.Objects.Add(new ObjectSnapshot { EntityId = 2, NativeObjectId = 77, ObjectId = 77,
            PositionX = 52, PositionY = 50, Activated = activated });
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        Predict(model, new(51, 50), click: true);

        Assert.Null(model.LocalMovementTarget);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingNativeObjectFootprintGeometryDisablesPrediction(bool click)
    {
        var snapshot = SnapshotAt(50, 50);
        snapshot.Objects.Add(new ObjectSnapshot { EntityId = 2, NativeObjectId = 77, ObjectId = 77,
            PositionX = 55, PositionY = 50 });
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        Predict(model, new(51, 50), click);

        Assert.Null(model.LocalMovementTarget);
        Assert.Null(model.PredictedPlayerPosition);
    }

    [Fact]
    public void OccupantsOfOtherLevelsDoNotStopPrediction()
    {
        var snapshot = SnapshotAt(50, 50);
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 2, LevelId = 1, PositionX = 51, PositionY = 50, Life = 64, LifeMaximum = 64 });
        snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = 9, LevelId = 1, PositionX = 51, PositionY = 50 });
        snapshot.Objects.Add(new ObjectSnapshot { EntityId = 3, NativeObjectId = 77, ObjectId = 77,
            LevelId = 1, PositionX = 51, PositionY = 50 });
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: snapshot));

        model.TrackPredictedMoveTo(1, 51, 50);

        Assert.Equal(new ClientPosition(51, 50), model.LocalMovementTarget);
    }

    private static void Predict(AuthoritativeClientModel model, ClientPosition target, bool click)
    {
        if (click) model.TrackPredictedMoveTo(1, target.X, target.Y);
        else {
            var player = model.Snapshot!.Players[0];
            model.TrackPredictedMove(1, target.X - player.PositionX, target.Y - player.PositionY);
        }
    }

    private static Snapshot SnapshotAt(int x, int y) => new() { Tick = 10, Players = { Player(x, y) } };
    private static PlayerSnapshot Player(int x, int y) => new() { EntityId = 1, PositionX = x, PositionY = y,
        Life = 64, LifeMaximum = 64, CombatMode = PlayerCombatMode.Stand };

    private static ObjectSnapshot OpenDoor(int x, int y) => new() { EntityId = 3, ObjectId = 1, NativeObjectId = 1,
        LevelId = 0, PositionX = x, PositionY = y, KeepVisibleAfterActivation = true,
        AnimationFrame = 3, MissilePassable = true, CathedralDoor = new() {
            EntityId = 3, PositionX = x, PositionY = y, NativeObjectId = 1, DoorState = 1,
            CachedPiece = 43, CachedNeighborPiece = 49, AnimationFrame = 3, SelectionRegion = 2,
            PreDraw = true, MissilePassable = true,
        } };
}
