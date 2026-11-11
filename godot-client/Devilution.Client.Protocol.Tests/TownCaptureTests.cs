using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Acceptance;
using Devilution.Client.Diagnostics;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class TownCaptureTests
{
    [Theory]
    [InlineData("admission")]
    [InlineData("warmup")]
    [InlineData("idle")]
    [InlineData("walking")]
    [InlineData("unreachable-clicks")]
    [InlineData("resident-clicks")]
    [InlineData("finalizing")]
    public async Task EveryPhaseBoundsAnUnsignalledWaitCancelsInputAndNamesItsBoundary(string phase)
    {
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new List<string>();
        var run = new TownCaptureRun(logs.Add, (duration, _) => {
            Assert.Equal(TimeSpan.FromSeconds(5), duration);
            return expired.Task;
        });
        CancellationToken input = default;
        var pending = run.PhaseAsync(phase, 5, token => {
            input = token; run.Operation = "waiting for resident receipt";
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, () => "tick=3655, queued=140, receipt=140/3655/Accepted, pending=0");
        Assert.False(pending.IsCompleted);
        expired.SetResult();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Contains($"phase '{phase}'", error.Message);
        Assert.Contains("waiting for resident receipt", error.Message);
        Assert.Contains("queued=140", error.Message);
        Assert.Contains("pending=0", error.Message);
        Assert.IsType<TimeoutException>(error.InnerException);
        Assert.True(input.IsCancellationRequested);
        Assert.False(run.Completed);
        Assert.Empty(run.CompletedPhases);
        Assert.Contains(logs, line => line.Contains(error.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllFourCompletedPhasesPublishAndExitWithoutWaitingForAnotherRenderFrame()
    {
        var noFurtherFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = new TownCaptureRun(_ => { }, (_, _) => noFurtherFrame.Task);
        var written = false;
        foreach (var phase in new[] { "idle", "walking", "unreachable-clicks", "resident-clicks" })
            await run.PhaseAsync(phase, 5, _ => Task.CompletedTask, () => "all three resident greetings accepted");
        await run.PhaseAsync("finalizing", 5, _ => {
            run.Complete(() => written = true);
            return Task.CompletedTask;
        }, () => "complete");
        Assert.False(noFurtherFrame.Task.IsCompleted);
        Assert.True(written);
        Assert.True(run.Completed);
        Assert.Contains("finalizing", run.CompletedPhases);
    }

    [Fact]
    public async Task FailedPublicationCannotMarkPartialCaptureComplete()
    {
        var run = new TownCaptureRun(_ => { });
        Assert.Throws<InvalidOperationException>(() => run.Complete(() => Assert.Fail("Published too early.")));
        foreach (var phase in new[] { "idle", "walking", "unreachable-clicks", "resident-clicks" })
            await run.PhaseAsync(phase, 5, _ => Task.CompletedTask, () => "settled");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run.PhaseAsync("finalizing", 5, _ => {
            run.Complete(() => throw new IOException("evidence write failed"));
            return Task.CompletedTask;
        }, () => "receipt=140/3655/Accepted"));
        Assert.Contains("evidence write failed", error.Message);
        Assert.False(run.Completed);
        Assert.NotNull(run.Failure);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(19, 0)]
    [InlineData(0, 19)]
    [InlineData(19, 19)]
    public void ResidentApproachesReachAnAdjacentCellFromDistantWalkEndpoints(int startX, int startY)
    {
        var snapshot = Town();
        snapshot.Players[0].PositionX = startX; snapshot.Players[0].PositionY = startY;
        foreach (var (x, y) in new[] { (5, 5), (15, 15), (5, 15) })
            snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { PositionX = x, PositionY = y, TownerId = 1 });
        // The wall forces navigation through the open centre rather than a straight line to the NPC.
        for (var y = 0; y < 20; y++)
            if (y != 10) snapshot.LevelGeometry[0].BlockedCells.Add(y * 20 + 10);
        var route = new TownCaptureRoute(snapshot);
        var xNow = startX; var yNow = startY;
        foreach (var resident in snapshot.NativeTownResidents) {
            Assert.InRange(route.ResidentDistance(xNow, yNow, resident.PositionX, resident.PositionY), 0, 60);
            for (var attempt = 0; Math.Max(Math.Abs(xNow - resident.PositionX), Math.Abs(yNow - resident.PositionY)) > 1; attempt++) {
                Assert.True(attempt < 20, "The resident route did not make bounded progress.");
                var next = route.TowardResident(xNow, yNow, resident.PositionX, resident.PositionY, 5);
                Assert.False(route.IsBlocked(next.X, next.Y));
                Assert.NotEqual((xNow, yNow), next);
                (xNow, yNow) = next;
            }
            Assert.Equal(0, route.ResidentDistance(xNow, yNow, resident.PositionX, resident.PositionY));
        }
    }

    [Fact]
    public void ExpiringAPhasedCaptureWritesItsMeasuredSectionsAsPartialEvidence()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"TownCapture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parent);
        try {
            var recorder = Recorder(parent, 2); var start = Stopwatch.GetTimestamp();
            recorder.ObserveWorldAdmission(start);
            recorder.BeginPhase("resident-clicks", start);
            for (var frame = 0; frame < 120; frame++) {
                var time = start + frame * Stopwatch.Frequency / 60;
                recorder.RecordFrameCost(time, default); recorder.RecordFrame(time, time, time);
            }
            var end = start + Stopwatch.Frequency * 2;
            recorder.RecordFrameCost(end, default); recorder.RecordFrame(end, end, end);
            using var document = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.True(document.RootElement.GetProperty("Partial").GetBoolean());
            Assert.False(document.RootElement.GetProperty("Outcome").GetProperty("Succeeded").GetBoolean());
            Assert.True(document.RootElement.GetProperty("Quality").GetProperty("IsValid").GetBoolean());
            Assert.Equal("resident-clicks", document.RootElement.GetProperty("PhaseSummaries")[0].GetProperty("Phase").GetString());
        } finally { Directory.Delete(parent, true); }
    }

    [Theory]
    [InlineData("TownCapture", null, false)]
    [InlineData("TownCapture", "1", true)]
    [InlineData("NativeDepthJourney", null, true)]
    [InlineData(null, null, false)]
    public void CaptureUsesNormalDrawCountersWithoutJourneyHistoryAllocations(string? phase, string? explicitHistory, bool detailed)
        => Assert.Equal(detailed, ClientFrameEvidenceRecorder.RequestsDetailedPresentationHistory(phase, explicitHistory));

    [Theory]
    [InlineData(false, 10, 45, 60)]
    [InlineData(true, 2, 3, 8)]
    public void RunsTimedPhasesThenRequiresEveryBlockedAndResidentClick(bool smoke, double warmup, double idle, double walk)
    {
        var sequence = new TownCaptureSequence(smoke);
        Assert.False(sequence.Advance(warmup - .001));
        Assert.True(sequence.Advance(warmup));
        Assert.Equal(TownCapturePhase.Idle, sequence.Phase);
        Assert.False(sequence.Advance(warmup + idle - .001));
        Assert.True(sequence.Advance(warmup + idle));
        Assert.True(sequence.UsesHeldKeys(warmup + idle));
        Assert.False(sequence.UsesHeldKeys(warmup + idle + walk / 2));
        Assert.False(sequence.Advance(warmup + idle + walk - .001));
        var end = warmup + idle + walk;
        Assert.True(sequence.Advance(end));
        Assert.Equal(TownCapturePhase.UnreachableClicks, sequence.Phase);
        for (var index = 0; index < 10; index++) {
            Assert.False(sequence.Advance(end + 1));
            sequence.Clicked();
        }
        Assert.Throws<InvalidOperationException>(() => sequence.Clicked());
        Assert.True(sequence.Advance(end + 1));
        Assert.Equal(TownCapturePhase.ResidentClicks, sequence.Phase);
        for (var index = 0; index < 3; index++) {
            Assert.False(sequence.Advance(end + 2));
            sequence.Clicked();
        }
        Assert.True(sequence.Advance(end + 2));
        Assert.Equal(TownCapturePhase.Complete, sequence.Phase);
        Assert.False(sequence.Advance(end + 3));
    }

    [Fact]
    public void RejectsOutOfOrderTimeAndClicksInTimedPhases()
    {
        var sequence = new TownCaptureSequence(false);
        Assert.Throws<InvalidOperationException>(() => sequence.Clicked());
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Advance(double.NaN));
        sequence.Advance(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Advance(9));
    }

    [Fact]
    public void TourIsDeterministicReachesEveryQuadrantAndAvoidsBlockedCellsResidentsAndStairs()
    {
        var snapshot = Town();
        snapshot.LevelGeometry[0].WarpTriggers.Add(new WarpTriggerSnapshot { TriggerX = 10, TriggerY = 10 });
        snapshot.LevelGeometry[0].BlockedCells.Add(11 * 20 + 10);
        snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot { PositionX = 9, PositionY = 10, TownerId = 1 });
        var route = new TownCaptureRoute(snapshot); var other = new TownCaptureRoute(snapshot);
        var x = 5; var y = 5; var cells = new HashSet<(int X, int Y)>();
        for (var step = 0; step < 200; step++) {
            var next = route.Next(x, y, 1);
            Assert.Equal(other.Next(x, y, 1), next);
            Assert.False(route.IsBlocked(next.X, next.Y));
            Assert.InRange(Math.Abs(next.X - x) + Math.Abs(next.Y - y), 0, 1);
            cells.Add(next); (x, y) = next;
        }
        Assert.Contains(cells, cell => cell.X < 5 && cell.Y < 5);
        Assert.Contains(cells, cell => cell.X > 14 && cell.Y < 5);
        Assert.Contains(cells, cell => cell.X > 14 && cell.Y > 14);
        Assert.Contains(cells, cell => cell.X < 5 && cell.Y > 14);
        Assert.True(route.IsBlocked(10, 10));
        Assert.True(route.IsBlocked(11, 10));
        Assert.True(route.IsBlocked(10, 11));
        Assert.True(route.IsBlocked(9, 10));
    }

    [Fact]
    public void SummariesKeepWarmupAndBoundaryIntervalsOutAndMeasureMedianP99AndWorstSecond()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"TownCapture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parent);
        try {
            var recorder = Recorder(parent, 3);
            var start = Stopwatch.GetTimestamp();
            recorder.ObserveWorldAdmission(start);
            recorder.BeginPhase("idle", start);
            for (var frame = 0; frame <= 60; frame++) {
                var time = start + frame * Stopwatch.Frequency / 60;
                recorder.RecordFrameCost(time, default); recorder.RecordFrame(time, time, time);
            }
            recorder.BeginPhase("walking", start + Stopwatch.Frequency);
            // The next phase's 100 ms boundary gap is excluded; subsequent 20 ms intervals are measured.
            for (var frame = 0; frame < 90; frame++) {
                var time = start + Stopwatch.Frequency * 11 / 10 + frame * Stopwatch.Frequency / 50;
                recorder.RecordFrameCost(time, default); recorder.RecordFrame(time, time, time);
            }
            var end = start + Stopwatch.Frequency * 3;
            recorder.RecordFrameCost(end, default); recorder.RecordFrame(end, end, end);
            using var doc = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            var phases = doc.RootElement.GetProperty("PhaseSummaries");
            Assert.Equal(2, phases.GetArrayLength());
            Assert.Equal(60, phases[0].GetProperty("FrameIntervalCount").GetInt32());
            Assert.Equal(1000d / 60, phases[0].GetProperty("MedianFrameMilliseconds").GetDouble(), 3);
            Assert.Equal(1000d / 60, phases[0].GetProperty("P99FrameMilliseconds").GetDouble(), 3);
            Assert.Equal("idle", phases[0].GetProperty("WorstSecond").GetProperty("Phase").GetString());
            Assert.Equal(89, phases[1].GetProperty("FrameIntervalCount").GetInt32());
            Assert.Equal(20, phases[1].GetProperty("P99FrameMilliseconds").GetDouble(), 3);
            Assert.All(doc.RootElement.GetProperty("FrameCosts").EnumerateArray(), second =>
                Assert.Contains(second.GetProperty("Phase").GetString(), new[] { "idle", "walking" }));
        } finally { Directory.Delete(parent, true); }
    }

    [Fact]
    public void ActivePhaseCollectionAndRouteStepsDoNotAllocatePerFrame()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"TownCapture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parent);
        try {
            var recorder = Recorder(parent, 10); var start = Stopwatch.GetTimestamp();
            recorder.ObserveWorldAdmission(start); recorder.BeginPhase("idle", start);
            recorder.RecordFrame(start, start, start);
            var route = new TownCaptureRoute(Town());
            route.Next(5, 5, 1);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 1; index < 1000; index++) {
                var time = start + index * Stopwatch.Frequency / 10000;
                recorder.RecordFrame(time, time, time);
                route.Next(5, 5, 1);
            }
            Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 128);
        } finally { Directory.Delete(parent, true); }
    }

    private static Snapshot Town()
    {
        var snapshot = new Snapshot();
        snapshot.Players.Add(new PlayerSnapshot { PositionX = 5, PositionY = 5 });
        snapshot.LevelGeometry.Add(new LevelGeometrySnapshot { Width = 20, Height = 20 });
        return snapshot;
    }

    private static ClientFrameEvidenceRecorder Recorder(string parent, int seconds)
    {
        var recorder = ClientFrameEvidenceRecorder.Create(new(Path.Combine(parent, "frames"), TimeSpan.Zero,
            TimeSpan.FromSeconds(seconds), 4096, true), new("build", "schema", "content", "rules", "renderer", "device", "1280x720", true, 60, false, "4.7.2", 1));
        recorder.ObserveServerIdentity(new("server", "schema", "content", "rules", 20, GameMode.SinglePlayer));
        recorder.ObserveParticipantCount(1);
        return recorder;
    }
}
