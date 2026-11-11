using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Devilution.Client.Protocol;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class SavedTownWalkReplayTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public void RecordedTownClicksKeepEveryReceiptCollisionAndVisualResult()
    {
        var log = Environment.GetEnvironmentVariable("GODEVILUTION_TOWN_WALK_REPLAY_LOG");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        var baselineClient = Environment.GetEnvironmentVariable("GODEVILUTION_PREDICTION_BASELINE_CLIENT");
        if (string.IsNullOrWhiteSpace(log) || string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(baselineClient))
            Assert.Skip("Select a private Town command log, native profile and captured pre-fix client assembly.");
        var records = CommandLogReader.ReadAll(log!).ToArray();
        var lastRejected = Array.FindLastIndex(records, value => value.ServerCommand is {
            Command.IntentCase: Command.IntentOneofCase.MoveRequested, Result.RejectReason: CommandRejectReason.InvalidTarget });
        Assert.True(lastRejected >= 0);
        var directory = Path.GetDirectoryName(log)!;
        var marker = records.Take(lastRejected).Last(value => value.Checkpoint is { } checkpoint
            && File.Exists(Path.Combine(directory, checkpoint.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(directory, marker.Checkpoint.FileName)));
        var adapter = Restore(document, profile!);
        var participant = Assert.Single(document.Participants);
        var commands = records.Take(lastRejected + 1).Where(value => value.UnixMilliseconds >= marker.UnixMilliseconds
            && value.ServerCommand is not null).ToArray();
        var context = new AssemblyLoadContext("pre-index-prediction", isCollectible: true);
        // Share only wire types. The captured visual predictor and its old collision rules stay isolated.
        context.Resolving += (_, name) => name.Name == typeof(Snapshot).Assembly.GetName().Name ? typeof(Snapshot).Assembly : null;
        try {
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(baselineClient!));
            var oldModel = assembly.GetType(typeof(AuthoritativeClientModel).FullName!, throwOnError: true)!;
            var oldCell = assembly.GetType(typeof(ClientPosition).FullName!, throwOnError: true)!;
            var oldCollision = oldModel.GetMethod("IsProjectedMoveAvailable", BindingFlags.NonPublic | BindingFlags.Static)!
                .CreateDelegate<Func<Snapshot, PlayerSnapshot, int, int, bool>>();
            var oldVisual = oldModel.GetMethod("FirstVisualStep", BindingFlags.NonPublic | BindingFlags.Static)!;
            var newCollision = typeof(AuthoritativeClientModel).GetMethod("IsProjectedMoveAvailable", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var model = new AuthoritativeClientModel();
            var samples = new List<(ulong Sequence, double Before, double After, long BeforeBytes, long AfterBytes)>();
            var cells = 0;
            foreach (var record in commands) {
                var logged = record.ServerCommand;
                adapter.AdvanceTo(logged.CurrentTick);
                var snapshot = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, logged.CurrentTick);
                snapshot.ContentManifestHash = document.Identity.ContentManifestHash;
                snapshot.RulesetIdentityHash = document.Identity.RulesetIdentityHash;
                model.Apply(new(Snapshot: snapshot));
                if (cells == 0) {
                    var available = newCollision.CreateDelegate<Func<Snapshot, PlayerSnapshot, int, int, bool>>(model);
                    var player = model.Snapshot!.Players[0];
                    for (var y = 0; y < 112; y++)
                        for (var x = 0; x < 112; x++) {
                            Assert.Equal(oldCollision(model.Snapshot, player, x, y), available(model.Snapshot, player, x, y));
                            cells++;
                        }
                }
                if (logged.Command.MoveRequested is { TargetCell: true } move) {
                    var player = model.Snapshot!.Players[0];
                    var origin = AuthoritativePlayerPositionProjection.Resolve(player);
                    var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    var expected = oldVisual.Invoke(null, [model.Snapshot, player,
                        Activator.CreateInstance(oldCell, origin.X, origin.Y), Activator.CreateInstance(oldCell, move.TargetX, move.TargetY)]);
                    var before = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    beforeBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                    var afterBytes = GC.GetAllocatedBytesForCurrentThread(); started = Stopwatch.GetTimestamp();
                    model.TrackPredictedMoveTo(logged.Command.ClientSequence, move.TargetX, move.TargetY);
                    var after = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    afterBytes = GC.GetAllocatedBytesForCurrentThread() - afterBytes;
                    Assert.Equal(Expected(expected), model.LocalMovementTarget);
                    // Repeated publication before the rejected ACK used to run the same expensive search again.
                    model.Apply(new(Snapshot: snapshot));
                    Assert.Equal(Expected(expected), model.LocalMovementTarget);
                    samples.Add((logged.Command.ClientSequence, before, after, beforeBytes, afterBytes));
                }
                var result = adapter.Execute(logged.SessionId, logged.Command.Clone(), logged.Result.AppliedTick);
                Assert.Equal(logged.Result.Status == CommandStatus.Accepted, result.Succeeded);
                Assert.Equal(logged.Result.RejectReason, result.RejectReason);
                model.Apply(new(Acknowledgement: new CommandAck { Results = { logged.Result.Clone() } }));
            }
            Assert.NotEmpty(samples);
            output.WriteLine($"Matched {commands.Length} authoritative receipts, {cells} Town collision cells and {samples.Count} visual targets (including repeated admission).");
            foreach (var sample in samples.OrderByDescending(value => value.Before).Take(8))
                output.WriteLine($"{sample.Sequence}: {sample.Before:F3} ms/{sample.BeforeBytes} B -> {sample.After:F3} ms/{sample.AfterBytes} B");

            ClientPosition? Expected(object? value) => value is null ? null : new(
                (int)oldCell.GetProperty("X")!.GetValue(value)!, (int)oldCell.GetProperty("Y")!.GetValue(value)!);
        } finally { context.Unload(); }
    }
}
