using Devilution.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public abstract class LiveAuthoritativeTestBase : IAsyncLifetime
{
    private readonly ConditionalWeakTable<AuthoritativeClient, MessageBacklog> messageBacklogs = new();

    // These overlays edit the checked-in base tables, so their schema must come
    // from that same pack, not a version copied into every live test.
    private protected static string BaseContentSchemaJson
    {
        get {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                FindRepositoryRoot(), "server", "content", "base", "content-pack.json")));
            return manifest.RootElement.GetProperty("schema").GetRawText();
        }
    }

    private protected sealed class StartedServer : IDisposable
    {
        private readonly LiveServerProcess? process;
        private readonly InProcessLiveServer? inProcess;
        private readonly string saveRoot;
        private readonly bool deleteSaveRootOnDispose;
        private readonly bool ownsSaveRoot;
        private readonly LiveAuthoritativeTestBase owner;
        private readonly LiveTestSaveRoot? cleanupRoot;
        private LiveServerProcess.Identity identity = new(0, string.Empty, string.Empty);
        private bool startupComplete;
        private bool disposed;
        private bool retainRootAfterCleanupFailure;

        private StartedServer(
            LiveServerProcess? process,
            string saveRoot,
            bool deleteSaveRootOnDispose,
            bool ownsSaveRoot,
            LiveAuthoritativeTestBase owner,
            LiveTestSaveRoot? cleanupRoot,
            InProcessLiveServer? inProcess = null)
        {
            this.process = process;
            this.inProcess = inProcess;
            this.saveRoot = saveRoot;
            this.deleteSaveRootOnDispose = deleteSaveRootOnDispose;
            this.ownsSaveRoot = ownsSaveRoot;
            this.owner = owner;
            this.cleanupRoot = cleanupRoot;
        }

        public int Port => identity.Port;

        public string ContentHash => identity.ContentHash;

        public string RulesetHash => identity.RulesetHash;

        public bool IsProcess => process is not null;
        public string ProcessDiagnostics => RequireProcess().Diagnostics;
        public bool IsClockHeld => inProcess?.IsClockHeld == true;
        public void AdvanceObservedClock(ulong tick) => inProcess!.AdvanceObservedClock(tick);
        public IDisposable HoldClockForObservation(ulong? tick = null)
            => inProcess?.HoldClockForObservation(tick) ?? throw new InvalidOperationException("Clock observation requires an in-process host.");
        public ulong CurrentTick => inProcess?.CurrentTick ?? throw new InvalidOperationException("Clock observation requires an in-process host.");
        public int ProcessId => RequireProcess().Id;
        public bool HasExited => process?.HasExited ?? inProcess!.HasStopped;
        public string SaveRoot => saveRoot;

        public static async Task<StartedServer> StartAsync(
            CancellationToken cancellationToken,
            string? contentOverlayRoot = null,
            string? additionalContentOverlayRoot = null,
            uint? startingGoldOverride = null,
            int? startingLifeOverride = null,
            int? startingManaOverride = null,
            uint? startingStatPointsOverride = null,
            uint? startingLevelOverride = null,
            uint? randomSeed = null,
            string? gameMode = null,
            string? difficultyOverride = null,
            int? portOverride = null,
            string? saveRootOverride = null,
            bool deleteSaveRootOnDispose = true,
            string? gameAdapterPackageRoot = null,
            string? gameAdapterId = null,
            string? performanceOutput = null,
            CommandSchedulingMode? commandSchedulingMode = null,
            string? persistenceMode = null,
            uint? tickRateHz = null,
            bool useProcess = false,
            bool passiveMonsters = false,
            bool productionStartup = false,
            bool controlledClock = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (useProcess && controlledClock)
                throw new ArgumentException("Controlled clocks require an in-process fixture.", nameof(controlledClock));
            if (!useProcess && performanceOutput is not null)
                throw new ArgumentException("Operator/performance tests require a real process.", nameof(useProcess));
            if (commandSchedulingMode.HasValue
                && commandSchedulingMode.Value is not (CommandSchedulingMode.Immediate or CommandSchedulingMode.ClockGated)) {
                throw new ArgumentOutOfRangeException(nameof(commandSchedulingMode),
                    "Live host fixtures support only Immediate and ClockGated command scheduling.");
            }
            if (persistenceMode is not null && persistenceMode is not ("legacy" or "cohort")) {
                throw new ArgumentOutOfRangeException(nameof(persistenceMode),
                    "Live host fixtures support only legacy and cohort persistence.");
            }
            var repositoryRoot = FindRepositoryRoot();
            var owner = TestContext.Current.TestClassInstance as LiveAuthoritativeTestBase
                ?? throw new InvalidOperationException("Live hosts require a per-test cleanup owner.");
            var assembly = useProcess ? await LiveServerProcess.ResolveAssemblyAsync(
                repositoryRoot, ResolveBuildConfiguration(),
                Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH"),
                Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY"),
                cancellationToken) : null;
            cancellationToken.ThrowIfCancellationRequested();
            var ownsSaveRoot = string.IsNullOrWhiteSpace(saveRootOverride);
            var saveRoot = Path.GetFullPath(saveRootOverride ?? Path.Combine(
                Path.GetTempPath(),
                "Godevilution-Godot-Test-" + Guid.NewGuid().ToString("N")));
            var cleanupRoot = ownsSaveRoot || deleteSaveRootOnDispose
                ? LiveTestSaveRoot.Register(saveRoot) : null;
            Directory.CreateDirectory(saveRoot);
            var arguments = new List<string>();
            arguments.Add("--port");
            arguments.Add((portOverride ?? 0).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            arguments.Add("--protocol-version");
            arguments.Add(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity);
            arguments.Add("--save-root");
            arguments.Add(saveRoot);
            if (performanceOutput is not null) {
                arguments.Add("--performance-output");
                arguments.Add(performanceOutput);
            }
            if (commandSchedulingMode.HasValue) {
                arguments.Add("--command-scheduling");
                arguments.Add(commandSchedulingMode.Value == CommandSchedulingMode.ClockGated
                    ? "clock-gated" : "immediate");
            }
            if (persistenceMode is not null) {
                arguments.Add("--persistence-mode");
                arguments.Add(persistenceMode);
            }
            arguments.Add("--tick-rate");
            arguments.Add((tickRateHz ?? LiveServerProcess.DefaultTickRateHz).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            if (startingGoldOverride.HasValue) {
                arguments.Add("--starting-gold");
                arguments.Add(startingGoldOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingLifeOverride.HasValue) {
                arguments.Add("--starting-life");
                arguments.Add(startingLifeOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingManaOverride.HasValue) {
                arguments.Add("--starting-mana");
                arguments.Add(startingManaOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingStatPointsOverride.HasValue) {
                arguments.Add("--starting-stat-points");
                arguments.Add(startingStatPointsOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingLevelOverride.HasValue) {
                arguments.Add("--starting-level");
                arguments.Add(startingLevelOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (randomSeed.HasValue) {
                arguments.Add("--random-seed");
                arguments.Add(randomSeed.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(gameMode)) {
                arguments.Add("--game-mode");
                arguments.Add(gameMode);
            }
            if (!string.IsNullOrWhiteSpace(difficultyOverride)) {
                arguments.Add("--difficulty");
                arguments.Add(difficultyOverride);
            }
            if (!string.IsNullOrWhiteSpace(contentOverlayRoot)) {
                arguments.Add("--content-overlay");
                arguments.Add(Path.GetFullPath(contentOverlayRoot));
            }
            if (!string.IsNullOrWhiteSpace(additionalContentOverlayRoot)) {
                arguments.Add("--content-overlay");
                arguments.Add(Path.GetFullPath(additionalContentOverlayRoot));
            }
            if (!string.IsNullOrWhiteSpace(gameAdapterPackageRoot)) {
                arguments.Add("--game-adapter-package");
                arguments.Add(Path.GetFullPath(gameAdapterPackageRoot));
            }
            if (!string.IsNullOrWhiteSpace(gameAdapterId)) {
                arguments.Add("--game-adapter");
                arguments.Add(gameAdapterId);
            }
            if (passiveMonsters) {
                var quiet = CreatePassiveMonsterOverlay(repositoryRoot, saveRoot,
                    new[] { contentOverlayRoot, additionalContentOverlayRoot }.OfType<string>());
                arguments.Add("--content-overlay");
                arguments.Add(quiet);
            }
            if (!useProcess) {
                try {
                    var host = InProcessLiveServer.Start(repositoryRoot, arguments, cancellationToken, controlledClock);
                    var hosted = new StartedServer(null, saveRoot, deleteSaveRootOnDispose, ownsSaveRoot,
                        owner, cleanupRoot, host) { identity = host.Identity, startupComplete = true };
                    owner.ownedServers.Add(hosted);
                    return hosted;
                } catch (Exception failure) {
                    if (failure.Data["InProcessHostCleanupFailure"] is Exception cleanupFailure) {
                        owner.cleanupFailures.Add(cleanupFailure);
                    } else if (ownsSaveRoot) {
                        try { cleanupRoot!.Delete(); } catch (Exception cleanup) {
                            failure.Data["LiveServerStartupCleanupFailure"] = cleanup;
                            owner.cleanupFailures.Add(cleanup);
                        }
                    }
                    throw;
                }
            }
            var startInfo = LiveServerProcess.CreateStartInfo(repositoryRoot, assembly!, tickRateHz: null, fastStartup: !productionStartup);
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);
            LiveServerProcess process;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                process = LiveServerProcess.Start(startInfo);
            } catch (Exception failure) {
                if (failure.Data["LiveServerConstructionExitConfirmed"] is false) {
                    owner.cleanupFailures.Add(new InvalidOperationException(
                        "The started host did not confirm exit; retain its owned save root.", failure));
                } else if (ownsSaveRoot) {
                    try { cleanupRoot!.Delete(); } catch (Exception cleanup) {
                        failure.Data["LiveServerStartupCleanupFailure"] = cleanup;
                        owner.cleanupFailures.Add(cleanup);
                    }
                }
                throw;
            }
            var server = new StartedServer(process, saveRoot, deleteSaveRootOnDispose, ownsSaveRoot, owner, cleanupRoot);
            owner.ownedServers.Add(server);
            try {
                server.identity = await process.WaitForIdentityAsync(cancellationToken);
                server.startupComplete = true;
                return server;
            } catch (Exception failure) {
                server.Dispose();
                failure.Data["LiveServerDiagnostics"] = process.Diagnostics;
                throw;
            }
        }

        private LiveServerProcess RequireProcess()
            => process ?? throw new InvalidOperationException("This fixture owns an in-process host, not a PID.");

        public async Task StartPerformanceRecordingAsync(CancellationToken cancellationToken)
            => await RequireProcess().StartPerformanceRecordingAsync(cancellationToken);

        public async Task QuitAsync(CancellationToken cancellationToken)
        {
            await RequireProcess().QuitAsync(cancellationToken);
            Assert.Equal(0, RequireProcess().ExitCode);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            try {
                if (process is not null)
                    process.DisposeAsync().AsTask().GetAwaiter().GetResult();
                else
                    inProcess!.Dispose();
                disposed = true;
                if (!retainRootAfterCleanupFailure && (startupComplete ? deleteSaveRootOnDispose : ownsSaveRoot))
                    cleanupRoot!.Delete();
            } catch (Exception failure) {
                retainRootAfterCleanupFailure = true;
                // Report at class cleanup so a using scope cannot mask its body failure.
                owner.cleanupFailures.Add(failure);
            }
        }

    }

    // A stationary target remains present, hittable and projected. Resource/status tests
    // must not race unrelated NPC attacks while a second connection joins or a host restarts.
    private protected static string CreatePassiveMonsterOverlay(string repositoryRoot, string saveRoot, IEnumerable<string> overlays)
    {
        var basePack = ContentPackLoader.LoadManifestDirectory(Path.Combine(repositoryRoot, "server", "content", "base"));
        var packs = new[] { basePack }.Concat(overlays.Select(ContentPackLoader.LoadManifestDirectory)).ToArray();
        var table = ComposedContent.Compose(new ContentManifest("test", "1", packs)).GetRequiredTable("monsters.tsv");
        var path = Path.Combine(saveRoot, "passive-monster-fixture");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "content-pack.json"), $$"""
            {
              "formatVersion": 1, "id": "test.passive-monsters", "version": "1",
              "dependencies": [{{string.Join(",", packs.Select(pack => JsonSerializer.Serialize(pack.Id)))}}],
              "schema": {{BaseContentSchemaJson}},
              "overlays": [{ "table": "monsters.tsv", "keys": ["entity_id"], "mode": "upsert" }]
            }
            """);
        File.WriteAllLines(Path.Combine(path, "monsters.tsv"),
            new[] { string.Join('\t', table.Columns) }.Concat(table.Rows.Select(row =>
                string.Join('\t', table.Columns.Select(column => column == "aggro_range" ? "0" : row[column])))));
        return path;
    }

    private protected static string SetDefaultClass(string row, bool isDefault)
    {
        var cells = row.Split('\t');
        cells[4] = isDefault ? "true" : "false";
        return string.Join('\t', cells);
    }

    private protected static ClientConnectionOptions CreateOptions(
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
            // The harness polls while waiting for game conditions, without a rendering-frame cadence.
            SnapshotPollInterval = TimeSpan.Zero,
        };
    }

    /** Never open a legacy canonical checkpoint while its File.Move publisher is live. */
    private protected static AuthoritativeSessionDocument ReadStoppedSessionCheckpoint(
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

    private protected static Snapshot ParseCheckpointSnapshot(AuthoritativeSessionDocument checkpoint)
    {
        using var save = JsonDocument.Parse(checkpoint.SerializedState);
        var snapshotBase64 = save.RootElement
            .GetProperty("SnapshotBase64")
            .GetString()
            ?? throw new InvalidDataException("The checkpoint SnapshotBase64 property is empty.");
        return Snapshot.Parser.ParseFrom(Convert.FromBase64String(snapshotBase64));
    }

    private protected async Task<AuthoritativeClientMessage> WaitForMessageAsync(
        AuthoritativeClient client,
        Func<AuthoritativeClientMessage, bool> predicate,
        StartedServer? observation = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backlog = messageBacklogs.GetOrCreateValue(client);
        var lastPoll = Stopwatch.GetTimestamp();
        for (var attempt = 0; attempt < 200; attempt++) {
            var receivedSnapshot = false;
            lock (backlog.Synchronization) {
                foreach (var message in client.DrainMessages()) {
                    receivedSnapshot |= message.Snapshot is not null;
                    backlog.Pending.Enqueue(message);
                }

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
            if (observation?.IsClockHeld == true) {
                lock (backlog.Synchronization) {
                    var published = backlog.Pending.Select(message => message.Snapshot?.Tick).Max();
                    if (published is { } boundary && boundary >= observation.CurrentTick)
                        observation.AdvanceObservedClock(checked(boundary + 1));
                }
            }
            // Immediate hosts answer every snapshot request. Pace requests by responses
            // instead of filling the socket with polls while a loaded host is publishing.
            if ((client.PendingCommandCount == 0 && (attempt == 0 || receivedSnapshot))
                || Stopwatch.GetElapsedTime(lastPoll) >= client.RetryTimeout) {
                await client.PollAsync(cancellationToken);
                lastPoll = Stopwatch.GetTimestamp();
            }
            await Task.Delay(25, cancellationToken);
        }
        Snapshot? latest;
        lock (backlog.Synchronization)
            latest = backlog.Pending.LastOrDefault(message => message.Snapshot is not null)?.Snapshot;
        throw new TimeoutException("Timed out waiting for a live authoritative client message; "
            + $"connected={client.IsConnected}; tick={latest?.Tick}; players="
            + string.Join(";", latest?.Players.Select(player =>
                $"{player.EntityId}:L{player.LevelId}({player.PositionX},{player.PositionY}) life={player.Life} mode={player.CombatMode} action={player.CurrentAction?.Kind}") ?? []));
    }

    private protected Task<AuthoritativeClientMessage> WaitForGameConditionAsync(
        StartedServer server, AuthoritativeClient client, Func<AuthoritativeClientMessage, bool> predicate)
        => WaitForMessageAsync(client, predicate, server);

    // UI admission has only two ticks of lateness tolerance. Catch up to the server's
    // rejection boundary before retrying; never retry a gameplay-rule refusal.
    private protected async Task<CommandResult> SubmitFreshUiCommandAsync(
        AuthoritativeClient client, AuthoritativeClientModel model, Command command)
    {
        for (var attempt = 0; ; attempt++) {
            var sequence = client.Queue(command, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var result = await WaitForAcknowledgementAsync(client, sequence);
            if (result.Status != CommandStatus.Rejected || result.RejectReason != CommandRejectReason.TooLate || attempt == 7)
                return result;
            model.Apply(await WaitForMessageAsync(client, message => message.Snapshot is { } snapshot
                && snapshot.Tick >= result.AppliedTick
                && snapshot.Players.First().CombatMode == PlayerCombatMode.Stand
                && snapshot.Players.First().CurrentAction is null));
        }
    }

    /** Keeps this live melee assertion independent of wall-clock scheduling. */
    private protected static ulong FindGuaranteedHitTick(
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

    private protected sealed class MessageBacklog
    {
        public object Synchronization { get; } = new();

        public Queue<AuthoritativeClientMessage> Pending { get; } = [];
    }

    private protected async Task<CommandResult> WaitForAcknowledgementAsync(AuthoritativeClient client, ulong sequence)
    {
        try {
            var message = await WaitForMessageAsync(client, message =>
                message.Acknowledgement?.Results.Any(result => result.ClientSequence == sequence) == true);
            return message.Acknowledgement!.Results.Single(result => result.ClientSequence == sequence);
        } catch (TimeoutException failure) when (client.CommandTimingRecorder is { } recorder) {
            var report = recorder.CaptureReport();
            throw new TimeoutException($"Receipt {sequence} pending={client.PendingCommandCount}; "
                + $"dispatch={JsonSerializer.Serialize(report.Commands.Where(command => command.ClientSequence == sequence).TakeLast(2))}; "
                + $"receipts={JsonSerializer.Serialize(report.Acknowledgements.TakeLast(4))}; {failure.Message}", failure);
        }
    }

    /** Walks through acknowledged native target-cell steps to an authored level trigger. */
    private protected async Task MoveToProjectedWarpAsync(
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

    /** Uses the negotiated native path, or acknowledged single steps, to reach a projected towner. */
    private protected async Task MoveToProjectedTownerAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        int townerId,
        uint? localEntityId = null,
        StartedServer? observation = null)
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
                    FollowPath = client.SupportsTargetCellPathing,
                    TargetX = greeting.PositionX,
                    TargetY = greeting.PositionY,
                },
            }, requestedTick);
            await client.PollAsync(TestContext.Current.CancellationToken);
            CommandResult acknowledgement;
            try { acknowledgement = await WaitForAcknowledgementAsync(client, sequence); }
            catch (TimeoutException failure) {
                throw new TimeoutException($"Towner step seq={sequence} requested={requestedTick} observed={model.CurrentTick} "
                    + $"suggestedNow={client.SuggestedCommandTick(model.CurrentTick)} rtt={client.RoundTripMilliseconds:F1} retry={client.RetryTimeout.TotalMilliseconds:F1}; {failure.Message}", failure);
            }
            if (acknowledgement.Status == CommandStatus.Rejected
                && acknowledgement.RejectReason == CommandRejectReason.TooLate) {
                // A loaded host can outrun the model tick that chose the requested
                // tick. Catch up to the host's reported tick and request again.
                model.Apply(await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is { } current
                        && current.Tick >= acknowledgement.AppliedTick, observation));
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
                    && candidate.CurrentAction is null
                    && !candidate.NativeWalkPathPending, observation);
            model.Apply(moved);
        }

        throw new TimeoutException(
            $"Could not reach projected towner {townerId} from the authoritative player position.");
    }

    private protected static string FindRepositoryRoot()
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

    private protected static string ResolveBuildConfiguration()
        => LiveServerProcess.BuildConfiguration;
    private protected readonly List<StartedServer> ownedServers = [];
    private protected readonly List<Exception> cleanupFailures = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        foreach (var server in ownedServers)
            server.Dispose();
        if (cleanupFailures.Count != 0)
            throw new AggregateException("Live authoritative test cleanup failed.", cleanupFailures);
        return ValueTask.CompletedTask;
    }

    private protected void CleanupLocalRoot(LiveTestSaveRoot lease)
    {
        try { lease.Delete(); }
        catch (Exception failure) { cleanupFailures.Add(failure); }
    }

    private protected static void AssertTownPortalAcceptedReceipt(CommandResult acknowledgement, ulong sequence)
    {
        Assert.Equal(sequence, acknowledgement.ClientSequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
        Assert.Equal(CommandRejectReason.Unspecified, acknowledgement.RejectReason);
        Assert.True(acknowledgement.ServerReceiptSequence > 0);
        Assert.True(acknowledgement.AppliedTick > 0);
    }

    private protected static PortalSnapshot AssertTownPortalAtTick(
        Snapshot snapshot,
        PortalSnapshot portalIdentity,
        ulong baselineTick,
        uint baselineRemaining)
    {
        Assert.True(baselineRemaining > 0);
        Assert.True(snapshot.Tick >= baselineTick);
        var elapsedTicks = snapshot.Tick - baselineTick;
        var expectedRemaining = elapsedTicks >= (ulong)(baselineRemaining - 1)
            ? 1U
            : baselineRemaining - checked((uint)elapsedTicks);
        var expected = portalIdentity.Clone();
        expected.RemainingTicks = expectedRemaining;
        var actual = Assert.Single(snapshot.Portals, portal => portal.PortalId == portalIdentity.PortalId);
        Assert.Equal(expected, actual);
        Assert.True(actual.RemainingTicks > 0);
        return actual;
    }
}
