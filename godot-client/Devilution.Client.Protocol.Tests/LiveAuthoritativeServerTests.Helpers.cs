using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{

    private static string SetDefaultClass(string row, bool isDefault)
    {
        var cells = row.Split('\t');
        cells[4] = isDefault ? "true" : "false";
        return string.Join('\t', cells);
    }

    private static ClientConnectionOptions CreateOptions(
        StartedServer server,
        string clientBuildId,
        uint requestedClassId)
    {
        return new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            clientBuildId,
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            server.ContentHash,
            server.RulesetHash) {
            RequestedCharacterClassId = requestedClassId,
        };
    }

    /** Never open a legacy canonical checkpoint while its File.Move publisher is live. */
    private static AuthoritativeSessionDocument ReadStoppedSessionCheckpoint(
        StartedServer server, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!server.HasExited)
            throw new InvalidOperationException("Checkpoint inspection requires confirmed owned-host exit.");
        var path = Assert.Single(Directory.EnumerateFiles(server.SaveRoot, "*.session.json"));
        var sessionId = Path.GetFileName(path)[..^".session.json".Length];
        return new AuthoritativeSessionStore(server.SaveRoot).Load(sessionId, server.RulesetHash)
            ?? throw new InvalidDataException("The stopped host has no durable session checkpoint.");
    }

    private static Snapshot ParseCheckpointSnapshot(AuthoritativeSessionDocument checkpoint)
    {
        using var save = JsonDocument.Parse(checkpoint.SerializedState);
        var snapshotBase64 = save.RootElement
            .GetProperty("SnapshotBase64")
            .GetString()
            ?? throw new InvalidDataException("The checkpoint SnapshotBase64 property is empty.");
        return Snapshot.Parser.ParseFrom(Convert.FromBase64String(snapshotBase64));
    }

    private static async Task<AuthoritativeClientMessage> WaitForMessageAsync(
        AuthoritativeClient client,
        Func<AuthoritativeClientMessage, bool> predicate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backlog = messageBacklogs.GetOrCreateValue(client);
        for (var attempt = 0; attempt < 200; attempt++) {
            lock (backlog.Synchronization) {
                foreach (var message in client.DrainMessages())
                    backlog.Pending.Enqueue(message);

                var pendingCount = backlog.Pending.Count;
                for (var index = 0; index < pendingCount; index++) {
                    var message = backlog.Pending.Dequeue();
                    if (message.Error is not null)
                        throw new InvalidOperationException(
                            $"Authoritative live-client error: {message.Error.Code}/{message.Error.Detail}");
                    if (predicate(message))
                        return message;
                    backlog.Pending.Enqueue(message);
                }
            }
            await client.PollAsync(cancellationToken);
            await Task.Delay(25, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for a live authoritative client message.");
    }

    /** Keeps this live melee assertion independent of wall-clock scheduling. */
    private static ulong FindGuaranteedHitTick(
        ulong suggestedTick,
        uint attackerEntityId,
        uint targetEntityId)
    {
        for (var offset = 0UL; offset < 100; offset++) {
            var tick = checked(suggestedTick + offset);
            var seed = unchecked((uint)tick ^ attackerEntityId ^ targetEntityId);
            var roll = unchecked(seed * 1664525U + 1013904223U) % 100U;
            if (roll < 50)
                return tick;
        }
        throw new InvalidOperationException("Could not find a deterministic player-hit tick.");
    }

    private sealed class MessageBacklog
    {
        public object Synchronization { get; } = new();

        public Queue<AuthoritativeClientMessage> Pending { get; } = [];
    }

    private static async Task<CommandResult> WaitForAcknowledgementAsync(AuthoritativeClient client, ulong sequence)
    {
        var message = await WaitForMessageAsync(client, message =>
            message.Acknowledgement?.Results.Any(result => result.ClientSequence == sequence) == true);
        return message.Acknowledgement!.Results.Single(result => result.ClientSequence == sequence);
    }

    /** Walks through acknowledged native target-cell steps to an authored level trigger. */
    private static async Task MoveToProjectedWarpAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        string warpSymbol,
        ulong minimumFreshTick = 0)
    {
        var snapshot = model.Snapshot
            ?? throw new InvalidOperationException("A live snapshot is required before native warp travel.");
        var player = snapshot.Players.FirstOrDefault()
            ?? throw new InvalidOperationException("The live snapshot has no local player.");
        var warp = model.VisibleWarpTriggers.SingleOrDefault(candidate =>
            candidate.StableSymbol == warpSymbol
            && candidate.Available);
        Assert.NotNull(warp);
        var sourceLevel = player.LevelId;
        var targetLevel = warp!.TargetLevelId;
        Assert.NotEqual(sourceLevel, targetLevel);
        var sourceEntityId = player.EntityId;
        var sourceObservationTick = Math.Max(model.CurrentTick, minimumFreshTick);
        var settledSource = await WaitForMessageAsync(
            client,
            message => message.Snapshot is { } settledSnapshot
                && settledSnapshot.Tick > sourceObservationTick
                && settledSnapshot.Players.SingleOrDefault(
                    candidate => candidate.EntityId == sourceEntityId) is { } settledPlayer
                && settledPlayer.LevelId == sourceLevel
                && settledPlayer.CombatMode == PlayerCombatMode.Stand
                && settledPlayer.CurrentAction is null);
        model.Apply(settledSource);

        for (var attempt = 0; attempt < 32; attempt++) {
            player = model.Snapshot!.Players.Single(candidate => candidate.EntityId == sourceEntityId);
            if (player.LevelId == targetLevel) {
                if (player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null)
                    return;

                var targetObservationTick = model.CurrentTick;
                var settledTarget = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is { } settledSnapshot
                        && settledSnapshot.Tick > targetObservationTick
                        && settledSnapshot.Players.SingleOrDefault(
                            candidate => candidate.EntityId == sourceEntityId) is { } settledPlayer
                        && settledPlayer.LevelId == targetLevel
                        && settledPlayer.CombatMode == PlayerCombatMode.Stand
                        && settledPlayer.CurrentAction is null);
                model.Apply(settledTarget);
                return;
            }
            Assert.Equal(sourceLevel, player.LevelId);

            var beforePosition = (player.PositionX, player.PositionY);
            var priorObservedTick = model.CurrentTick;
            var sequence = client.Queue(new Command {
                MoveRequested = new MoveRequested {
                    TargetCell = true,
                    TargetX = warp.TriggerX,
                    TargetY = warp.TriggerY,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Authored warp step to '{warpSymbol}' was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}.");
            var moved = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } movedSnapshot
                    && movedSnapshot.Tick >= acknowledgement.AppliedTick
                    && movedSnapshot.Tick > priorObservedTick
                    && movedSnapshot.Players.SingleOrDefault(
                        candidate => candidate.EntityId == sourceEntityId) is { } candidate
                    && ((candidate.LevelId == targetLevel
                            && candidate.CombatMode == PlayerCombatMode.Stand
                            && candidate.CurrentAction is null)
                        || (candidate.LevelId == sourceLevel
                            && (candidate.PositionX, candidate.PositionY) != beforePosition
                            && candidate.CombatMode == PlayerCombatMode.Stand
                            && candidate.CurrentAction is null)));
            model.Apply(moved);
        }

        throw new TimeoutException(
            $"Could not reach authored warp trigger '{warpSymbol}' from the authoritative player position.");
    }

    /** Walks through acknowledged native target-cell steps to a projected towner. */
    private static async Task MoveToProjectedTownerAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        int townerId,
        uint? localEntityId = null)
    {
        var snapshot = model.Snapshot
            ?? throw new InvalidOperationException("A live snapshot is required before native town travel.");
        var player = (localEntityId is uint entityId
                ? snapshot.Players.SingleOrDefault(candidate => candidate.EntityId == entityId)
                : snapshot.Players.FirstOrDefault())
            ?? throw new InvalidOperationException("The live snapshot has no local player.");
        var greeting = snapshot.TownerGreetings.SingleOrDefault(candidate =>
            candidate.TownerId == townerId && candidate.LevelId == player.LevelId);
        Assert.NotNull(greeting);

        for (var attempt = 0; attempt < 200; attempt++) {
            player = model.Snapshot!.Players.Single(candidate => candidate.EntityId == player.EntityId);
            var distance = Math.Max(
                Math.Abs(player.PositionX - greeting!.PositionX),
                Math.Abs(player.PositionY - greeting.PositionY));
            if (distance <= 1
                && player.LevelId == greeting.LevelId
                && player.CombatMode == PlayerCombatMode.Stand
                && player.CurrentAction is null)
                return;

            var beforePosition = (player.PositionX, player.PositionY);
            var priorObservedTick = model.CurrentTick;
            var requestedTick = client.SuggestedCommandTick(model.CurrentTick);
            var sequence = client.Queue(new Command {
                MoveRequested = new MoveRequested {
                    TargetCell = true,
                    TargetX = greeting.PositionX,
                    TargetY = greeting.PositionY,
                },
            }, requestedTick);
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            if (acknowledgement.Status == CommandStatus.Rejected
                && acknowledgement.RejectReason == CommandRejectReason.TooLate) {
                // A loaded host can outrun the model tick that chose the requested
                // tick. Catch up to the host's reported tick and request again.
                model.Apply(await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is { } current
                        && current.Tick >= acknowledgement.AppliedTick));
                continue;
            }
            var actionEndTick = player.CurrentAction is { } action
                ? action.EndTick.ToString()
                : "none";
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Projected towner {townerId} attempt {attempt} sequence {sequence} requestedTick {requestedTick} "
                    + $"ack={acknowledgement.Status}/{acknowledgement.RejectReason} appliedTick={acknowledgement.AppliedTick} "
                    + $"modelTick={model.CurrentTick} player=({player.PositionX},{player.PositionY}) level={player.LevelId} "
                    + $"mode={player.CombatMode} actionEndTick={actionEndTick} target=({greeting.PositionX},{greeting.PositionY}).");
            var moved = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } movedSnapshot
                    && movedSnapshot.Tick >= acknowledgement.AppliedTick
                    && movedSnapshot.Tick > priorObservedTick
                    && movedSnapshot.Players.SingleOrDefault(
                        candidate => candidate.EntityId == player.EntityId) is { } candidate
                    && candidate.LevelId == greeting.LevelId
                    && (candidate.PositionX, candidate.PositionY) != beforePosition
                    && candidate.CombatMode == PlayerCombatMode.Stand
                    && candidate.CurrentAction is null);
            model.Apply(moved);
        }

        throw new TimeoutException(
            $"Could not reach projected towner {townerId} from the authoritative player position.");
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "protocol/devilution.proto")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private static string ResolveBuildConfiguration()
        => LiveServerProcess.BuildConfiguration;
}
