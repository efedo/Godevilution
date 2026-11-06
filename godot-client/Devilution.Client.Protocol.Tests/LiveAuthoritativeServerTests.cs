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
    private static readonly ConditionalWeakTable<AuthoritativeClient, MessageBacklog> messageBacklogs = new();

    // These overlays edit the checked-in base tables, so their schema must come
    // from that same pack, not a version copied into every live test.
    private static string BaseContentSchemaJson
    {
        get {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                FindRepositoryRoot(), "server", "content", "base", "content-pack.json")));
            return manifest.RootElement.GetProperty("schema").GetRawText();
        }
    }

    [Fact]
    public async Task GodotTransportProjectsTheServerSelectedDifficulty()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            difficultyOverride: "hell");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-difficulty", requestedClassId: 2));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        model.Apply(await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null));

        Assert.Equal(GameDifficulty.Hell, model.Snapshot!.Difficulty);
        Assert.Equal(
            "Hell",
            AuthoritativeDifficultyPresentation.Format(model.Snapshot.Difficulty));
    }

    [Fact]
    public async Task GodotTransportRunsThroughPackagedDiabloAdapterAndReconnects()
    {
        using var package = new PackagedDiabloAdapterPackage();
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            gameAdapterPackageRoot: package.Root,
            gameAdapterId: "godevilution.diablo.package",
            gameMode: "single-player");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-packaged-diablo", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var initialPlayer = Assert.Single(initial.Snapshot!.Players);
        var sessionToken = client.SessionToken;
        Assert.NotEmpty(sessionToken);

        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        var resumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null
                && message.Snapshot.Players.Single().EntityId == initialPlayer.EntityId);

        Assert.Equal(sessionToken, client.SessionToken);
        Assert.Equal(initialPlayer.EntityId, resumed.Snapshot!.Players.Single().EntityId);
        Assert.NotEmpty(resumed.Snapshot.StateSha256);
    }

    [Fact]
    public async Task PackagedDiabloAdapterRestoresAuthoritativeStateAfterProcessRestart()
    {
        using var package = new PackagedDiabloAdapterPackage("0.1.0");
        using var upgradedPackage = new PackagedDiabloAdapterPackage("0.2.0");
        Assert.NotEqual(package.Version, upgradedPackage.Version);
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Packaged-Diablo-Restart-Test-"
                + Guid.NewGuid().ToString("N"));
        var saveRootLease = LiveTestSaveRoot.Register(saveRoot);
        AuthoritativeClient? client = null;
        Exception? primaryFailure = null;
        try {
            uint entityId;
            int movedPositionY;
            int serverPort;
            using (var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false,
                gameAdapterPackageRoot: package.Root,
                gameAdapterId: "godevilution.diablo.package",
                gameMode: "single-player")) {
                serverPort = server.Port;
                client = new AuthoritativeClient(
                    CreateOptions(server, "godot-live-packaged-diablo-restart", requestedClassId: 2));
                await client.ConnectAsync(TestContext.Current.CancellationToken);
                var initial = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is not null);
                var player = Assert.Single(initial.Snapshot!.Players);
                entityId = player.EntityId;
                Assert.Throws<InvalidOperationException>(() => ReadStoppedSessionCheckpoint(
                    server, TestContext.Current.CancellationToken));
                var model = new AuthoritativeClientModel();
                model.Apply(initial);
                var moveSequence = client.Queue(new Command {
                    MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
                }, client.SuggestedCommandTick(model.CurrentTick));
                await client.PollAsync(TestContext.Current.CancellationToken);
                var acceptedMove = await WaitForAcknowledgementAsync(client, moveSequence);
                Assert.Equal(CommandStatus.Accepted, acceptedMove.Status);
                var moved = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot?.Players.Single().PositionY
                        == player.PositionY + 1);
                movedPositionY = moved.Snapshot!.Players.Single().PositionY;

                // Accepted receipts are published only after SaveSession. Kill,
                // do not gracefully flush, then inspect the quiescent checkpoint.
                // Live readers can interfere with Windows File.Move publication.
                server.Dispose();
                Assert.True(server.HasExited);
                Assert.Empty(cleanupFailures);
                using (var canceledRead = new CancellationTokenSource()) {
                    canceledRead.Cancel();
                    Assert.Throws<OperationCanceledException>(() => ReadStoppedSessionCheckpoint(
                        server, canceledRead.Token));
                }
                var checkpoint = ReadStoppedSessionCheckpoint(server, TestContext.Current.CancellationToken);
                var savedMove = Assert.Single(checkpoint.CommandReceipts, receipt => receipt.ClientSequence == moveSequence);
                Assert.Equal(acceptedMove, savedMove);
                var checkpointPlayer = Assert.Single(ParseCheckpointSnapshot(checkpoint).Players);
                Assert.Equal(entityId, checkpointPlayer.EntityId);
                Assert.Equal(2U, checkpointPlayer.CharacterClassId);
                Assert.Equal(player.PositionX, checkpointPlayer.PositionX);
                Assert.Equal(movedPositionY, checkpointPlayer.PositionY);
            }

            using var restartedServer = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                portOverride: serverPort,
                saveRootOverride: saveRoot,
                gameAdapterPackageRoot: upgradedPackage.Root,
                gameAdapterId: "godevilution.diablo.package",
                gameMode: "single-player");
            Assert.NotEqual(package.Version, upgradedPackage.Version);
            Assert.Equal(client.ServerIdentity!.ContentManifestHash, restartedServer.ContentHash);
            Assert.Equal(client.ServerIdentity.RulesetIdentityHash, restartedServer.RulesetHash);
            await client.ReconnectAsync(TestContext.Current.CancellationToken);
            var restored = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null
                    && message.Snapshot.Players.Single().EntityId == entityId
                    && message.Snapshot.Players.Single().PositionY == movedPositionY);
            var restoredPlayer = Assert.Single(restored.Snapshot!.Players);
            Assert.Equal(entityId, restoredPlayer.EntityId);
            Assert.Equal(2U, restoredPlayer.CharacterClassId);
            Assert.Equal(movedPositionY, restoredPlayer.PositionY);
            Assert.NotEmpty(restored.Snapshot.StateSha256);
            Assert.Equal(restartedServer.ContentHash, client.ServerIdentity!.ContentManifestHash);
            Assert.Equal(restartedServer.RulesetHash, client.ServerIdentity.RulesetIdentityHash);
        } catch (Exception failure) {
            primaryFailure = failure;
            throw;
        } finally {
            if (client is not null) {
                try {
                    await client.DisposeAsync();
                } catch (Exception cleanupFailure) {
                    cleanupFailures.Add(cleanupFailure);
                }
            }
            if (primaryFailure is null && cleanupFailures.Count == 0)
                CleanupLocalRoot(saveRootLease);
        }
    }

    [Fact]
    public async Task GodotTransportReceivesDifficultyScaledAuthoritativeMonsterState()
    {
        var hellfireRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "hellfire");
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            hellfireRoot,
            difficultyOverride: "hell");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-difficulty-monster", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var snapshot = initial.Snapshot!;
        Assert.Equal(GameDifficulty.Hell, snapshot.Difficulty);
        var monster = Assert.Single(snapshot.Monsters, candidate => candidate.EntityId == 10);

        Assert.Equal(2U, monster.MonsterId);
        Assert.Equal(244, monster.MaxHitPoints);
        Assert.Equal(monster.MaxHitPoints, monster.HitPoints);
    }

    [Fact]
    public async Task GodotTransportAllocatesASeededAuthoritativeAttributePoint()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingStatPointsOverride: 1,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-stat-point", requestedClassId: 0));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        model.Apply(await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null));
        var initial = Assert.Single(model.Snapshot!.Players);
        var initialVitality = initial.Attributes.Vitality.Current;
        var initialLife = initial.Life;
        Assert.Equal(1U, initial.StatPoints);

        var sequence = client.Queue(new Command {
            AllocateAttributeRequested = new AllocateAttributeRequested {
                Attribute = PlayerAttribute.Vitality,
                Amount = 1,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, sequence)).Status);

        var allocated = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single() is { } player
                && player.StatPoints == 0
                && player.Attributes.Vitality.Current == initialVitality + 1
                && player.Life > initialLife);
        model.Apply(allocated);
    }

    [Fact]
    public async Task GodotTransportReconcilesAuthoritativeStashSessionGoldAndMoves()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-stash-session", requestedClassId: 0));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        await MoveToProjectedTownerAsync(client, model, 7);
        var initialPlayer = Assert.Single(model.Snapshot!.Players);
        Assert.True(initialPlayer.Gold >= 10);
        var initialGold = initialPlayer.Gold;
        var initialStashGold = initialPlayer.StashGold;
        var equipment = Assert.Single(initialPlayer.Equipment, item => item.Slot == 4);
        var stableItemId = equipment.State.StableItemId;
        Assert.NotEqual(0U, stableItemId);

        var openSequence = client.Queue(new Command {
            OpenStashRequested = new OpenStashRequested(),
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, openSequence)).Status);
        var opened = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().StashOpen == true);
        model.Apply(opened);

        var depositSequence = client.Queue(new Command {
            DepositStashGoldRequested = new DepositStashGoldRequested { Amount = 10 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, depositSequence)).Status);
        var deposited = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single() is { StashOpen: true }
                player
                && player.Gold == initialGold - 10
                && player.StashGold == initialStashGold + 10);
        model.Apply(deposited);

        var withdrawSequence = client.Queue(new Command {
            WithdrawStashGoldRequested = new WithdrawStashGoldRequested { Amount = 4 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, withdrawSequence)).Status);
        var withdrawn = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single() is { StashOpen: true }
                player
                && player.Gold == initialGold - 6
                && player.StashGold == initialStashGold + 6);
        model.Apply(withdrawn);

        var moveSequence = client.Queue(new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                    StableItemId = stableItemId,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, moveSequence)).Status);
        var moved = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Stash.Any(item =>
                item.Page == 0
                && item.Cell == 0
                && item.Item.State.StableItemId == stableItemId) == true);
        model.Apply(moved);

        var closeSequence = client.Queue(new Command {
            CloseStashRequested = new CloseStashRequested(),
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, closeSequence)).Status);
        var closed = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().StashOpen == false);
        model.Apply(closed);

        var rejectedMoveSequence = client.Queue(new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var rejectedMove = await WaitForAcknowledgementAsync(client, rejectedMoveSequence);
        Assert.Equal(CommandStatus.Rejected, rejectedMove.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, rejectedMove.RejectReason);
    }

    private sealed class StartedServer : IDisposable
    {
        private readonly LiveServerProcess process;
        private readonly string saveRoot;
        private readonly bool deleteSaveRootOnDispose;
        private readonly bool ownsSaveRoot;
        private readonly LiveAuthoritativeServerTests owner;
        private readonly LiveTestSaveRoot? cleanupRoot;
        private LiveServerProcess.Identity identity = new(0, string.Empty, string.Empty);
        private bool startupComplete;
        private bool disposed;
        private bool retainRootAfterCleanupFailure;

        private StartedServer(
            LiveServerProcess process,
            string saveRoot,
            bool deleteSaveRootOnDispose,
            bool ownsSaveRoot,
            LiveAuthoritativeServerTests owner,
            LiveTestSaveRoot? cleanupRoot)
        {
            this.process = process;
            this.saveRoot = saveRoot;
            this.deleteSaveRootOnDispose = deleteSaveRootOnDispose;
            this.ownsSaveRoot = ownsSaveRoot;
            this.owner = owner;
            this.cleanupRoot = cleanupRoot;
        }

        public int Port => identity.Port;

        public string ContentHash => identity.ContentHash;

        public string RulesetHash => identity.RulesetHash;

        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;
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
            uint? tickRateHz = null)
        {
            if (commandSchedulingMode.HasValue
                && commandSchedulingMode.Value is not (CommandSchedulingMode.Immediate or CommandSchedulingMode.ClockGated)) {
                throw new ArgumentOutOfRangeException(nameof(commandSchedulingMode),
                    "Live host fixtures support only Immediate and ClockGated command scheduling.");
            }
            if (persistenceMode is not null && persistenceMode is not ("legacy" or "cohort")) {
                throw new ArgumentOutOfRangeException(nameof(persistenceMode),
                    "Live host fixtures support only legacy and cohort persistence.");
            }
            var repositoryRoot = LiveAuthoritativeServerTests.FindRepositoryRoot();
            var owner = TestContext.Current.TestClassInstance as LiveAuthoritativeServerTests
                ?? throw new InvalidOperationException("Live hosts require a per-test cleanup owner.");
            var assembly = await LiveServerProcess.ResolveAssemblyAsync(
                repositoryRoot, ResolveBuildConfiguration(),
                Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH"),
                Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY"),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var ownsSaveRoot = string.IsNullOrWhiteSpace(saveRootOverride);
            var saveRoot = Path.GetFullPath(saveRootOverride ?? Path.Combine(
                Path.GetTempPath(),
                "Godevilution-Godot-Test-" + Guid.NewGuid().ToString("N")));
            var cleanupRoot = ownsSaveRoot || deleteSaveRootOnDispose
                ? LiveTestSaveRoot.Register(saveRoot) : null;
            Directory.CreateDirectory(saveRoot);
            var startInfo = LiveServerProcess.CreateStartInfo(repositoryRoot, assembly);
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add((portOverride ?? 0).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--protocol-version");
            startInfo.ArgumentList.Add(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity);
            startInfo.ArgumentList.Add("--save-root");
            startInfo.ArgumentList.Add(saveRoot);
            if (performanceOutput is not null) {
                startInfo.ArgumentList.Add("--performance-output");
                startInfo.ArgumentList.Add(performanceOutput);
            }
            if (commandSchedulingMode.HasValue) {
                startInfo.ArgumentList.Add("--command-scheduling");
                startInfo.ArgumentList.Add(commandSchedulingMode.Value == CommandSchedulingMode.ClockGated
                    ? "clock-gated" : "immediate");
            }
            if (persistenceMode is not null) {
                startInfo.ArgumentList.Add("--persistence-mode");
                startInfo.ArgumentList.Add(persistenceMode);
            }
            if (tickRateHz.HasValue) {
                startInfo.ArgumentList.Add("--tick-rate");
                startInfo.ArgumentList.Add(tickRateHz.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingGoldOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-gold");
                startInfo.ArgumentList.Add(startingGoldOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingLifeOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-life");
                startInfo.ArgumentList.Add(startingLifeOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingManaOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-mana");
                startInfo.ArgumentList.Add(startingManaOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingStatPointsOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-stat-points");
                startInfo.ArgumentList.Add(startingStatPointsOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingLevelOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-level");
                startInfo.ArgumentList.Add(startingLevelOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (randomSeed.HasValue) {
                startInfo.ArgumentList.Add("--random-seed");
                startInfo.ArgumentList.Add(randomSeed.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(gameMode)) {
                startInfo.ArgumentList.Add("--game-mode");
                startInfo.ArgumentList.Add(gameMode);
            }
            if (!string.IsNullOrWhiteSpace(difficultyOverride)) {
                startInfo.ArgumentList.Add("--difficulty");
                startInfo.ArgumentList.Add(difficultyOverride);
            }
            if (!string.IsNullOrWhiteSpace(contentOverlayRoot)) {
                startInfo.ArgumentList.Add("--content-overlay");
                startInfo.ArgumentList.Add(Path.GetFullPath(contentOverlayRoot));
            }
            if (!string.IsNullOrWhiteSpace(additionalContentOverlayRoot)) {
                startInfo.ArgumentList.Add("--content-overlay");
                startInfo.ArgumentList.Add(Path.GetFullPath(additionalContentOverlayRoot));
            }
            if (!string.IsNullOrWhiteSpace(gameAdapterPackageRoot)) {
                startInfo.ArgumentList.Add("--game-adapter-package");
                startInfo.ArgumentList.Add(Path.GetFullPath(gameAdapterPackageRoot));
            }
            if (!string.IsNullOrWhiteSpace(gameAdapterId)) {
                startInfo.ArgumentList.Add("--game-adapter");
                startInfo.ArgumentList.Add(gameAdapterId);
            }
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

        public async Task StartPerformanceRecordingAsync(CancellationToken cancellationToken)
            => await process.StartPerformanceRecordingAsync(cancellationToken);

        public async Task QuitAsync(CancellationToken cancellationToken)
        {
            await process.QuitAsync(cancellationToken);
            Assert.Equal(0, process.ExitCode);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            try {
                process.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
}
