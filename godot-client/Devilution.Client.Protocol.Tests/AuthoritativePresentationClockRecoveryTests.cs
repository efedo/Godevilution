using Devilution.Client.PresentationAssets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class AuthoritativePresentationClockRecoveryTests
{
    [Theory]
    [InlineData(20U, 160)]
    [InlineData(200U, 1600)]
    [InlineData(20U, 60)]
    [InlineData(200U, 60)]
    public void LostServerTicksConvergeWithoutReversingAnimationAndLaterWalksUseEveryFrame(uint rate, int frameRate)
    {
        var time = new ManualTime();
        var server = new RealtimeAuthoritativeClock(rate, time);
        var presentation = new AuthoritativePresentationClock();
        var snapshot = new Snapshot();
        var player = new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64,
            CombatMode = PlayerCombatMode.WalkSouthwards };
        snapshot.Players.Add(player);
        var catalog = TownWalkCatalog();
        var laterFrames = new HashSet<int>();
        double previous = 0;
        ulong previousAction = ulong.MaxValue;
        int previousFrame = 0;
        var frameSeconds = 1d / frameRate;
        var frameCount = frameRate * 12;
        presentation.Advance(snapshot, 0, rate);
        for (var frame = 0; frame < frameCount; frame++) {
            time.Advance(frameSeconds);
            // The UI keeps rendering while the authority lock is unavailable. Its next
            // read drops excess game time, exactly as the standalone paced host does.
            var stalled = time.ElapsedSeconds is >= 3 and < 3.75;
            if (!stalled) {
                snapshot.Tick = server.CurrentTick;
                var start = snapshot.Tick / 8 * 8;
                player.CurrentAction = new PlayerActionSnapshot {
                    Kind = PlayerActionKind.Move, StartedAtTick = start, EndTick = start + 8,
                };
            }
            var tick = presentation.Advance(snapshot, frameSeconds, rate);
            Assert.True(tick >= previous, $"Presentation reversed from {previous} to {tick}.");
            Assert.InRange(tick - previous, 0, rate * frameSeconds + 1.001);
            Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Town, tick, out var selection));
            var selectedFrame = selection.FrameIndex!.Value;
            if (previousAction == player.CurrentAction!.StartedAtTick)
                Assert.True(selectedFrame >= previousFrame, "An admitted correction reversed the current walk animation.");
            if (time.ElapsedSeconds > 8) laterFrames.Add(selectedFrame);
            previous = tick;
            previousAction = player.CurrentAction.StartedAtTick;
            previousFrame = selectedFrame;
        }
        Assert.True(server.CurrentTick < (ulong)(time.ElapsedSeconds * rate) - rate / 2);
        Assert.Equal(Enumerable.Range(0, 8), laterFrames.Order());
        Assert.InRange(presentation.CurrentTick!.Value - snapshot.Tick, 0, 1.001);
    }

    [Fact]
    public void ClockAdvancementAndCorrectionAllocateNoManagedMemory()
    {
        var clock = new AuthoritativePresentationClock();
        var snapshot = new Snapshot();
        Action<int> frame = _ => { snapshot.Tick++; clock.Advance(snapshot, 0.1, 20); };
        HotPathAllocation.Warm(frame);
        var measured = HotPathAllocation.Measure(frame, 10000);
        Assert.Equal(0, measured.Bytes);
    }

    internal static NativePlayerCatalog TownWalkCatalog()
    {
        const string json = """
            {"schemaVersion":1,"packId":"clock-recovery","contentManifestHashes":["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"],
            "levelBindings":[{"contentManifestHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","levelId":0,"location":"town"}],
            "clips":[{"classId":0,"location":"town","armor":"light","weapon":"unarmed","graphic":"town-walk","path":"sprites/test.png",
            "frameWidth":96,"frameHeight":128,"framesPerDirection":8,"animationFrames":8,"ticksPerFrame":1,"actionFrame":4}],"spells":[]}
            """;
        Assert.True(NativePlayerCatalog.TryParse(json, out var catalog, out var diagnostics), string.Join(" ", diagnostics));
        return catalog!;
    }

    private sealed class ManualTime : TimeProvider
    {
        private double seconds;
        public double ElapsedSeconds => seconds;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => (long)Math.Round(seconds * TimestampFrequency);
        public void Advance(double value) => seconds += value;
    }
}
