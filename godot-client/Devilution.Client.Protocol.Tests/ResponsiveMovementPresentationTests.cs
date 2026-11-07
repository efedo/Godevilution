using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ResponsiveMovementPresentationTests
{
    private static PlayerSnapshot Player() => new() { EntityId = 1, LevelId = 0, PositionX = 50, PositionY = 50,
        Life = 64, LifeMaximum = 64, CombatMode = PlayerCombatMode.Stand };

    private static PlayerSnapshot Walking(ulong started = 10)
    {
        var p = Player();
        p.CombatMode = PlayerCombatMode.WalkSouthwards;
        p.CombatModeEndTick = started + 8;
        p.CurrentAction = new() { Kind = PlayerActionKind.Move, StartedAtTick = started,
            EffectTick = started + 8, ReadyTick = started + 8, EndTick = started + 8, DirectionX = 1 };
        return p;
    }

    [Theory]
    [InlineData(10U)] [InlineData(20U)] [InlineData(200U)]
    public void InputTraversesAFractionOnTheNextFrameAndStopsAtOneTile(uint rate)
    {
        var visual = new AuthoritativeLocalPlayerPresentation();
        var player = Player();
        var first = visual.Advance(player, 10, new(51, 50), 1d / 60, rate);
        Assert.InRange(first.X, 50.000001, 50.999999);
        Assert.Equal(50 + rate / 8d / 60, first.X, 8);
        for (var i = 0; i < 120; i++) visual.Advance(player, 10, new(51, 50), 1d / 60, rate);
        Assert.Equal(new ClientPresentationPosition(51, 50), visual.Position);
        Assert.Equal(50, player.PositionX);
    }

    [Fact]
    public void ASlowRenderedFrameUsesElapsedTimeInsteadOfSlowingTheWalk()
    {
        var visual = new AuthoritativeLocalPlayerPresentation();
        Assert.Equal(50.5, visual.Advance(Player(), 10, new(51, 50), 0.2, 20).X, 8);
    }

    [Fact]
    public void ANewLevelOrDeathAlsoClearsTheModelsInputTarget()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot { Tick = 1, Players = { Player() } };
        model.Apply(new(Snapshot: snapshot));
        model.TrackPredictedMoveTo(1, 55, 50);
        Assert.NotNull(model.LocalMovementTarget);
        snapshot.Tick = 2; snapshot.Players[0].Life = 0;
        model.Apply(new(Snapshot: snapshot));
        Assert.Null(model.LocalMovementTarget);
        snapshot.Tick = 3; snapshot.Players[0].Life = 64;
        model.Apply(new(Snapshot: snapshot));
        model.TrackPredictedMoveTo(2, 55, 50);
        snapshot.Tick = 4; snapshot.Players[0].LevelId = 1;
        model.Apply(new(Snapshot: snapshot));
        Assert.Null(model.LocalMovementTarget);
    }

    [Fact]
    public void AnAuthoritativeReservedTileIsNotPendingInputPrediction()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: new Snapshot { Tick = 10, Players = { Walking() } }));
        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
        Assert.Null(model.LocalMovementTarget);
    }

    [Fact]
    public void AckAndItsLaterSnapshotNeverRewindAnAcceptedStep()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: new Snapshot { Tick = 9, Players = { Player() } }));
        model.TrackPredictedMoveTo(1, 55, 50);
        var visual = new AuthoritativeLocalPlayerPresentation();
        var first = visual.Advance(model.Snapshot!.Players[0], 9, model.LocalMovementTarget, 1d / 60, 20);
        model.Apply(new(Acknowledgement: new CommandAck { Results = { new CommandResult {
            ClientSequence = 1, Status = CommandStatus.Accepted, AppliedTick = 10 } } }));
        Assert.NotNull(model.LocalMovementTarget);
        var afterAck = visual.Advance(model.Snapshot.Players[0], 9, model.LocalMovementTarget, 1d / 60, 20);
        Assert.True(afterAck.X > first.X);
        var walking = Walking();
        model.Apply(new(Snapshot: new Snapshot { Tick = 10, Players = { walking } }));
        var afterSnapshot = visual.Advance(walking, 10, model.LocalMovementTarget, 1d / 60, 20);
        Assert.True(afterSnapshot.X > afterAck.X);
        Assert.Null(model.PredictedPlayerPosition); // A reserved tile is not pending input prediction.
    }

    [Fact]
    public void RefusedPredictionReturnsSmoothlyWithoutChangingAuthoritativePosition()
    {
        var model = new AuthoritativeClientModel();
        var player = Player();
        model.Apply(new(Snapshot: new Snapshot { Tick = 9, Players = { player } }));
        model.TrackPredictedMoveTo(1, 55, 50);
        var visual = new AuthoritativeLocalPlayerPresentation();
        var before = visual.Advance(player, 9, model.LocalMovementTarget, 0.1, 20);
        model.Apply(new(Acknowledgement: new CommandAck { Results = { new CommandResult {
            ClientSequence = 1, Status = CommandStatus.Rejected, RejectReason = CommandRejectReason.NotAllowed, AppliedTick = 9 } } }));
        Assert.Null(model.LocalMovementTarget);
        var corrected = visual.Advance(player, 9, null, 1d / 60, 20);
        Assert.InRange(corrected.X, 50.000001, before.X - 0.000001);
        for (var i = 0; i < 60; i++) visual.Advance(player, 9, null, 1d / 60, 20);
        Assert.Equal(new ClientPresentationPosition(50, 50), visual.Position);
    }

    [Fact]
    public void BlockedAndCornerBlockedStepsAreNotPredicted()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot { Players = { Player() }, LevelGeometry = { new LevelGeometrySnapshot {
            LevelId = 0, Width = 112, Height = 112, BlockedCells = { 50 * 112 + 51 } } } };
        model.Apply(new(Snapshot: snapshot));
        model.TrackPredictedMoveTo(1, 51, 50);
        Assert.Null(model.LocalMovementTarget);
        snapshot.LevelGeometry[0].BlockedCells.Add(51 * 112 + 50);
        model.Apply(new(Snapshot: snapshot));
        model.TrackPredictedMoveTo(2, 51, 51);
        Assert.NotEqual(new ClientPosition(51, 51), model.LocalMovementTarget);
    }

    [Theory]
    [InlineData(55, 50)] [InlineData(55, 49)] [InlineData(55, 52)] [InlineData(49, 55)]
    public void ClickPredictionSharesTheServersNativeOrderingAroundObstacles(int x, int y)
    {
        var model = new AuthoritativeClientModel();
        var geometry = new LevelGeometrySnapshot { LevelId = 0, Width = 112, Height = 112 };
        for (var row = 48; row <= 52; row++) geometry.BlockedCells.Add(row * 112 + 52);
        var snapshot = new Snapshot { Players = { Player() }, LevelGeometry = { geometry } };
        model.Apply(new(Snapshot: snapshot));
        model.TrackPredictedMoveTo(1, x, y);
        var path = Devilution.Diablo.AuthoritativeNativePlayerPathfinder.FindPath(50, 50, x, y, 100,
            (nx, ny) => nx >= 0 && ny >= 0 && nx < 112 && ny < 112 && !geometry.BlockedCells.Contains(ny * 112 + nx),
            (sx, sy, nx, ny) => sx == nx || sy == ny || (!geometry.BlockedCells.Contains(sy * 112 + nx)
                && !geometry.BlockedCells.Contains(ny * 112 + sx)));
        Assert.NotEmpty(path);
        var first = Devilution.Diablo.AuthoritativeNativePlayerPathfinder.GetDisplacement(path[0]);
        Assert.Equal(new ClientPosition(50 + first.X, 50 + first.Y), model.LocalMovementTarget);
    }

    [Fact]
    public void OlderServersWithoutPathContinuationPredictOnlyTheirSingleStep()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: new Snapshot { Tick = 9, Players = { Player() } }));
        model.TrackPredictedMoveTo(1, 55, 50, followPath: false);
        Assert.Equal(new ClientPosition(51, 50), model.LocalMovementTarget);
        model.Apply(new(Snapshot: new Snapshot { Tick = 10, Players = { Walking() } }));
        Assert.Equal(new ClientPosition(51, 50), model.LocalMovementTarget);
    }

    [Fact]
    public void ReversalFinishesTheCurrentServerStepBeforeTurning()
    {
        var visual = new AuthoritativeLocalPlayerPresentation();
        var player = Walking();
        visual.Advance(player, 12, new(52, 50), 0, 20);
        var before = visual.Position;
        var reversed = visual.Advance(player, 12, new(49, 50), 1d / 60, 20);
        Assert.True(reversed.X > before.X);
        for (var i = 0; i < 30; i++) visual.Advance(player, 12, new(49, 50), 1d / 60, 20);
        Assert.InRange(visual.Position.X, 50, 51);
    }

    [Fact]
    public void DeathAndLevelChangeDiscardOldVisualPrediction()
    {
        var visual = new AuthoritativeLocalPlayerPresentation();
        var player = Player();
        visual.Advance(player, 1, new(51, 50), 0.1, 20);
        player.Life = 0;
        Assert.Equal(new ClientPresentationPosition(50, 50), visual.Advance(player, 2, new(51, 50), 0.1, 20));
        player.Life = 64; player.LevelId = 1; player.PositionX = 30;
        Assert.Equal(new ClientPresentationPosition(30, 50), visual.Advance(player, 3, new(51, 50), 0.1, 20));
    }

    [Fact]
    public void AnAuthoritativeDiscontinuityResetsEvenWhenTheVisualWasNearby()
    {
        var visual = new AuthoritativeLocalPlayerPresentation();
        var player = Player();
        visual.Advance(player, 1, new(51, 50), 0.4, 20);
        player.PositionX = 52;
        Assert.Equal(new ClientPresentationPosition(52, 50), visual.Advance(player, 2, null, 1d / 60, 20));
    }

    [Fact]
    public void ADisagreeingServerStepReconcilesWithoutJumpingToTheNewPrediction()
    {
        var visual = new AuthoritativeLocalPlayerPresentation();
        var player = Player();
        var before = visual.Advance(player, 9, new(51, 50), 0.1, 20);
        var server = Walking(); server.CurrentAction.DirectionX = 0; server.CurrentAction.DirectionY = -1;
        var next = visual.Advance(server, 11, new(50, 48), 1d / 60, 20);
        Assert.InRange(next.X, 50.000001, before.X - 0.000001);
        Assert.InRange(next.Y, 49.9, 49.999999);
    }

    [Fact]
    public void NativeAndFallbackCamerasKeepTheSameFractionalPlayerAnchor()
    {
        var visual = new AuthoritativeLocalPlayerPresentation();
        var player = Player();
        (double X, double Y)? nativeAnchor = null;
        (double X, double Y)? fallbackAnchor = null;
        double? previousOrigin = null;
        for (var frame = 0; frame < 20; frame++) {
            var p = visual.Advance(player, 1, new(51, 50), 1d / 60, 20);
            var native = visual.NativeViewport(800, 480, 112, 112);
            var fallback = visual.FallbackViewport(800, 480, 112, 112);
            var a = native.WorldToScreen(p.X, p.Y);
            var b = fallback.WorldToScreen(p.X, p.Y);
            if (nativeAnchor is { } prior) { Assert.Equal(prior.X, a.X, 8); Assert.Equal(prior.Y, a.Y, 8); }
            if (fallbackAnchor is { } priorFallback) { Assert.Equal(priorFallback.X, b.X, 8); Assert.Equal(priorFallback.Y, b.Y, 8); }
            if (previousOrigin is { } origin) Assert.InRange(Math.Abs(native.OriginX - origin), 0.01, 2);
            nativeAnchor = a; fallbackAnchor = b; previousOrigin = native.OriginX;
        }
    }

    [Fact]
    public void KnownWalkAndAnimationProgressDoesNotFreezeAtTheFirstTick()
    {
        var snapshot = new Snapshot { Tick = 10, Players = { Walking() }, Monsters = { new MonsterSnapshot {
            EntityId = 2, PositionX = 40, PositionY = 40, FuturePositionX = 41, FuturePositionY = 40,
            Moving = true, MovementStartedTick = 10, MovementEffectTick = 18 } } };
        var clock = new AuthoritativePresentationClock();
        clock.Advance(snapshot, 0, 20);
        var previous = 50d;
        for (var i = 1; i <= 24; i++) {
            var tick = clock.Advance(snapshot, 1d / 60, 20);
            var p = AuthoritativePlayerPositionProjection.ResolvePresentation(snapshot.Players[0], tick);
            Assert.True(p.X > previous);
            previous = p.X;
        }
        Assert.Equal(51, previous, 8);
        var later = clock.Advance(snapshot, 5, 20);
        Assert.Equal(51, AuthoritativePlayerPositionProjection.ResolvePresentation(snapshot.Players[0], later).X);
        Assert.Equal(41, AuthoritativeMonsterPositionProjection.ResolvePresentation(snapshot.Monsters[0], later).X);
        Assert.True(later <= 30);
    }

    [Fact]
    public void HeldContinuationDoesNotWaitOneHundredMillisecondsOnAFasterHost()
    {
        var cadence = new HeldWalkCadence();
        cadence.Submitted(new(50, 50));
        cadence.Advance(0.005);
        Assert.False(cadence.Due(new(50, 50)));
        Assert.True(cadence.Due(new(51, 50)));
        cadence.Submitted(new(51, 50));
        cadence.Advance(0.04);
        Assert.True(cadence.Due(new(52, 50)));
    }

    [Fact]
    public void AnUnchangedHeldTargetRetainsItsBoundedRetargetCadence()
    {
        var cadence = new HeldWalkCadence();
        cadence.Submitted(new(50, 50));
        cadence.Advance(0.099);
        Assert.False(cadence.Due(new(50, 50)));
        cadence.Advance(0.001);
        Assert.True(cadence.Due(new(50, 50)));
    }

    [Fact]
    public void RealtimeClockProjectionDoesNotAddATickForAFractionOfElapsedTime()
    {
        var time = new ManualTimeProvider();
        var clock = new AuthoritativeCommandClock(time);
        clock.ObserveSchedulingClock(100);
        time.AdvanceMilliseconds(5);
        Assert.Equal(100UL, clock.Estimate(0, 20, roundUp: false));
        Assert.Equal(101UL, clock.Estimate(0, 20)); // Compatibility projection still rounds up.
        time.AdvanceMilliseconds(45);
        Assert.Equal(101UL, clock.Estimate(0, 20, roundUp: false));
    }

    [Theory]
    [InlineData(20U, 0d, 1)] [InlineData(20U, 2d, 1)] [InlineData(20U, 100d, 3)]
    [InlineData(20U, 250d, 6)] [InlineData(10U, 2d, 1)] [InlineData(200U, 2d, 10)]
    [InlineData(200U, 100d, 21)]
    public void RealtimeLeadCoversFullMeasuredRttIndependentlyOfTheRetryFloor(uint rate, double rtt, int ticks)
    {
        var lead = new AdaptiveCommandLead();
        lead.ObserveRoundTrip(0, rtt);
        Assert.Equal(ticks, lead.Ticks(0, rate));
    }
}
