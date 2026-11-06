using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeTrapHostPublicationTests
{
    [Fact(Explicit = true)]
    [Trait("Category", "Slow")]
    public async Task SavedThemeResumePublishesPastRecordedStall()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_THEME_RESUME_SESSION");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select the private theme-object resume session and profile.");
        var run = Directory.GetDirectories(Path.Combine(session!, "save-command-logs")).Order().Last();
        var records = CommandLogReader.ReadAll(Path.Combine(run, HostCommandLog.LogFileName));
        var checkpoint = records.Select((record, index) => (record, index)).Last(entry =>
            entry.record.Checkpoint is { } marker && File.Exists(Path.Combine(run, marker.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(run, checkpoint.record.Checkpoint.FileName)));
        var timer = Stopwatch.StartNew();
        var executor = Restore(document, profile!);
        output.WriteLine($"Restored tick {document.Tick} in {timer.ElapsedMilliseconds} ms.");
        var participant = Assert.Single(document.Participants);
        var model = new AuthoritativeClientModel();
        var initialItems = new DiabloCohortCodec().Deserialize(document.SerializedModuleState)
            .WorldItems.Select(item => item.EntityId).ToHashSet();
        ulong tick = document.Tick;
        void CheckEvents(ulong requestedTick)
        {
            if (executor.DrainEvents(participant.SessionId, participant.EntityId, requestedTick) is not { } events) return;
            AuthoritativeEventBatchValidator.Validate(events, requestedTick);
            var envelope = Envelope.Parser.ParseFrom(new Envelope { EventBatch = events }.ToByteArray());
            model.Apply(new AuthoritativeClientMessage(Events: envelope.EventBatch));
        }
        void Check(ulong requestedTick)
        {
            CheckEvents(requestedTick);
            timer.Restart();
            var snapshot = executor.CreateSnapshot(participant.SessionId, participant.EntityId, requestedTick);
            var capture = timer.ElapsedMilliseconds;
            timer.Restart();
            AuthoritativeSnapshotValidator.Validate(snapshot);
            var envelope = Envelope.Parser.ParseFrom(new Envelope { Snapshot = snapshot }.ToByteArray());
            model.Apply(new AuthoritativeClientMessage(Snapshot: envelope.Snapshot));
            output.WriteLine($"Snapshot {snapshot.Tick}: capture={capture} ms, wire/admit={timer.ElapsedMilliseconds} ms, bytes={envelope.CalculateSize()}, objects={snapshot.Objects.Count}, items={snapshot.WorldItems.Count}.");
            Assert.Equal(requestedTick, model.Snapshot!.Tick);
        }
        Check(tick);
        foreach (var record in records.Skip(checkpoint.index + 1)) {
            if (record.ServerCommand is not { } logged) continue;
            output.WriteLine($"Command {logged.Command.ClientSequence} {logged.Command.IntentCase}: current={logged.CurrentTick}, applied={logged.Result.AppliedTick}, status={logged.Result.Status}.");
            timer.Restart();
            executor.AdvanceTo(Math.Max(tick, logged.CurrentTick));
            var result = executor.Execute(logged.SessionId, logged.Command.Clone(), logged.Result.AppliedTick);
            tick = Math.Max(tick, logged.Result.AppliedTick);
            output.WriteLine($"Applied: {result.Succeeded}/{result.RejectReason}, elapsed={timer.ElapsedMilliseconds} ms.");
            Assert.Equal(logged.Result.Status is CommandStatus.Accepted, result.Succeeded);
            Check(tick);
        }
        tick = Math.Max(tick, 18228);
        ulong finalTick;
        await using (var host = new SnapshotHost(tick,
            value => executor.CreateSnapshot(participant.SessionId, participant.EntityId, value), executor.AdvanceTo)) {
            await host.ConnectAsync();
            for (ulong next = tick + 1; next <= tick + 200; next += 20) {
                output.WriteLine($"Polling tick {next}.");
                await host.PollAsync(next);
                CheckEvents(next);
            }
            finalTick = host.Model.Snapshot!.Tick;
            Assert.True(finalTick > 18228);
            Assert.Null(host.Server.LastClientFailure);
        }
        var captured = executor.CaptureCohortState();
        var state = new DiabloCohortCodec().Deserialize(captured.SerializedState);
        var identify = Assert.Single(state.WorldItems, item => !initialItems.Contains(item.EntityId)
            && item.State.ItemActionKind == DiabloItemActionKind.Identify);
        Assert.Equal(0, identify.State.SpellId);
        var level = Assert.Single(state.CathedralItems!.Levels, value => value.LevelId == identify.LevelId);
        var slot = Assert.Single(level.SlotEntityBindings, value => value.EntityId == identify.EntityId).Slot;
        Assert.Equal(5, level.Items[slot].SpellId);
        var cold = Restore(document with { Tick = finalTick, SerializedModuleState = captured.SerializedState }, profile!);
        Assert.Equal(captured.SerializedState, cold.CaptureCohortState().SerializedState);
        await using var freshHost = new SnapshotHost(finalTick,
            value => cold.CreateSnapshot(participant.SessionId, participant.EntityId, value), cold.AdvanceTo);
        await freshHost.ConnectAsync();
        await freshHost.PollAsync(finalTick + 1);
        Assert.Null(freshHost.Server.LastClientFailure);
        output.WriteLine($"Published through {finalTick}; Identify scroll survived cohort capture and fresh-host restore.");
    }
}
