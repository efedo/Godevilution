using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class PredictionCostTests(ITestOutputHelper output)
{
    [Fact]
    public void IndexedCollisionMatchesOriginalRulesOverEveryTownCell()
    {
        var snapshot = Town();
        var random = new Random(193);
        for (var cell = 0; cell < 112 * 112; cell++)
            if (random.Next(3) == 0) snapshot.LevelGeometry[0].BlockedCells.Add(cell);
        snapshot.LevelGeometry[0].StaticObjectBlockedCells.Add(44 * 112 + 57);
        snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = 9, PositionX = 58, PositionY = 16 });
        snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = 1, PositionX = 55, PositionY = 79 });
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 2, PositionX = 52, PositionY = 50, Life = 0 });
        snapshot.Monsters.Add(new MonsterSnapshot { Alive = true, PositionX = 47, PositionY = 50,
            Moving = true, FuturePositionX = 48, FuturePositionY = 50 });
        snapshot.Monsters.Add(new MonsterSnapshot { Alive = true, LevelId = 1, PositionX = 49, PositionY = 50 });
        snapshot.Objects.Add(new ObjectSnapshot { EntityId = 3, PositionX = 51, PositionY = 50 });
        var index = new PredictionCollisionIndex();
        index.Prepare(snapshot, snapshot.Players[0]);
        for (var y = 0; y < 112; y++)
            for (var x = 0; x < 112; x++)
                Assert.Equal(LegacyPredictionCollision.IsProjectedMoveAvailable(snapshot, snapshot.Players[0], x, y),
                    index.IsAvailable(x, y));
    }

    [Fact]
    public void MissingGeometryAndExtremeCoordinatesKeepLegacyRejections()
    {
        var snapshot = Town(); snapshot.LevelGeometry.Clear();
        snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = 9,
            PositionX = int.MinValue, PositionY = int.MinValue });
        var index = new PredictionCollisionIndex();
        foreach (var geometry in new[] { false, true }) {
            if (geometry) snapshot.Objects.Add(new ObjectSnapshot { NativeObjectId = 77 });
            index.Prepare(snapshot.Clone(), snapshot.Players[0]);
            foreach (var coordinate in new[] { int.MinValue, -1, 0, 255, int.MaxValue })
                Assert.Equal(LegacyPredictionCollision.IsProjectedMoveAvailable(snapshot, snapshot.Players[0], coordinate, coordinate),
                    index.IsAvailable(coordinate, coordinate));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeCornerAndBlockedEndpointRulesKeepTheSameFirstStep(bool blockedEndpoint)
    {
        var snapshot = Town();
        snapshot.LevelGeometry[0].BlockedCells.Add(50 * 112 + 51);
        snapshot.LevelGeometry[0].BlockedCells.Add(51 * 112 + 50);
        if (blockedEndpoint) snapshot.Objects.Add(new ObjectSnapshot { EntityId = 3, ObjectId = 4, PositionX = 52, PositionY = 52 });
        var player = snapshot.Players[0];
        var geometry = snapshot.LevelGeometry[0];
        bool Corner(int x, int y) => x >= 0 && y >= 0 && x < 112 && y < 112
            && !geometry.BlockedCells.Contains(y * 112 + x);
        var expected = AuthoritativeNativePlayerPathfinder.FindPath(50, 50, 52, 52, 100,
            (x, y) => LegacyPredictionCollision.IsProjectedMoveAvailable(snapshot, player, x, y),
            (x, y, nx, ny) => x == nx || y == ny || Corner(nx, y) && Corner(x, ny));
        Assert.NotEmpty(expected);
        var step = AuthoritativeNativePlayerPathfinder.GetDisplacement(expected[0]);
        var model = new AuthoritativeClientModel(); model.Apply(new(Snapshot: snapshot));
        model.TrackPredictedMoveTo(1, 52, 52);
        Assert.Equal(new ClientPosition(50 + step.X, 50 + step.Y), model.LocalMovementTarget);
    }

    [Fact]
    public void FailedSearchIsRetainedUntilCollisionOriginOrTargetChanges()
    {
        var snapshot = DisconnectedTown();
        var index = new PredictionCollisionIndex(); index.Prepare(snapshot, snapshot.Players[0]);
        var search = new NativePredictionSearch(index);
        Assert.Null(search.FirstStep(new(50, 50), new(70, 50)));
        var searches = search.SearchCount;
        var revision = index.Revision;
        var unchanged = snapshot.Clone(); unchanged.Tick++;
        index.Prepare(unchanged, unchanged.Players[0]);
        Assert.Equal(revision, index.Revision);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var repeat = 0; repeat < 1000; repeat++) search.FirstStep(new(50, 50), new(70, 50));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        Assert.Equal(searches, search.SearchCount);
        search.FirstStep(new(49, 50), new(70, 50)); Assert.Equal(++searches, search.SearchCount);
        search.FirstStep(new(49, 50), new(71, 50)); Assert.Equal(++searches, search.SearchCount);
        unchanged.LevelGeometry[0].BlockedCells.Clear();
        index.Prepare(unchanged.Clone(), unchanged.Players[0]);
        Assert.True(index.Revision > revision);
        Assert.NotNull(search.FirstStep(new(49, 50), new(71, 50)));
        Assert.Equal(++searches, search.SearchCount);
    }

    [Fact]
    public void OccupancyAndGeometryChangesInvalidateFailedSearches()
    {
        var original = Town();
        Action<Snapshot>[] changes = [
            value => value.LevelGeometry[0].BlockedCells.Add(4),
            value => value.LevelGeometry[0].StaticObjectBlockedCells.Add(4),
            value => value.Objects.Add(new ObjectSnapshot { PositionX = 4, PositionY = 5 }),
            value => value.Monsters.Add(new MonsterSnapshot { Alive = true, Moving = true, PositionX = 4, PositionY = 5,
                FuturePositionX = 6, FuturePositionY = 7 }),
            value => value.Players.Add(new PlayerSnapshot { EntityId = 2, PositionX = 4, PositionY = 5 }),
            value => value.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = 9, PositionX = 4, PositionY = 5 }),
        ];
        foreach (var change in changes) {
            var index = new PredictionCollisionIndex(); index.Prepare(original, original.Players[0]);
            var revision = index.Revision;
            var next = original.Clone(); change(next); index.Prepare(next, next.Players[0]);
            Assert.True(index.Revision > revision);
        }
    }

    [Fact]
    public void UnreachableClickAndRepeatedAdmissionHaveBoundedCost()
    {
        // No retail geometry: a solid divider makes the opposite half of Town unreachable.
        var snapshot = DisconnectedTown();
        var model = new AuthoritativeClientModel(); model.Apply(new(Snapshot: snapshot));
        model.TrackPredictedMoveTo(1, 70, 50); // JIT and index warmup.
        var allocation = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        model.TrackPredictedMoveTo(2, 71, 50);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        Assert.Null(model.LocalMovementTarget);
        Assert.InRange(bytes, 0, 1_000_000);
        // The deterministic probe bound protects loaded CI; report the actual wall cost separately.
        Assert.InRange(elapsed, 0, 50);
        allocation = GC.GetAllocatedBytesForCurrentThread(); started = Stopwatch.GetTimestamp();
        for (var repeat = 0; repeat < 1000; repeat++) model.TrackPredictedMoveTo((ulong)repeat + 3, 71, 50);
        var cachedBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        Assert.InRange(cachedBytes, 0, 100_000);
        var index = new PredictionCollisionIndex(); index.Prepare(snapshot, snapshot.Players[0]);
        var search = new NativePredictionSearch(index); search.FirstStep(new(50, 50), new(70, 50));
        Assert.InRange(search.CollisionQueries, 1, NativePredictionSearch.MaximumCollisionQueries + 1);
        output.WriteLine($"Unreachable click: {elapsed:F3} ms, {bytes} B; 1000 cached clicks: "
            + $"{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} ms, {cachedBytes} B; probes={search.CollisionQueries}.");
    }

    private static Snapshot Town() => new() { Tick = 10, Players = { new PlayerSnapshot {
        EntityId = 1, Life = 64, LifeMaximum = 64, PositionX = 50, PositionY = 50 } },
        LevelGeometry = { new LevelGeometrySnapshot { Width = 112, Height = 112 } } };

    private static Snapshot DisconnectedTown()
    {
        var snapshot = Town();
        for (var y = 0; y < 112; y++) snapshot.LevelGeometry[0].BlockedCells.Add(y * 112 + 60);
        return snapshot;
    }
}
