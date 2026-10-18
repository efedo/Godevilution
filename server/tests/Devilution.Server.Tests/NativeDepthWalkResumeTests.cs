using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/** Explicit real-TCP proof that a committed native destination walk resumes in a fresh host. */
public sealed class NativeDepthWalkResumeTests
{
    private const string CharacterName = "NativeDepthWalk";
    private const string AdapterId = "devilution.diablo.cathedral-native-depth-progression.v1";

    [Fact]
    public void FalseNativeWalkPathPreservesLegacyPlayerSnapshotBytes()
    {
        var legacy = new PlayerSnapshot { EntityId = 17, LevelId = 3 };
        var withFalsePendingPath = legacy.Clone();
        withFalsePendingPath.NativeWalkPathPending = false;

        Assert.Equal(legacy.ToByteArray(), withFalsePendingPath.ToByteArray());
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeTownWalkContinuesAcrossFreshHostFromCommittedMidWalkCheckpoint(bool queueRanged)
    {
        var profile = LoadProfile();
        if (profile is null) {
            Assert.Skip("The compiled native-depth progression profile and its three content packs are required.");
            return;
        }

        var ownedRoot = Path.Combine(profile.RepositoryRoot, ".local", "native-depth-walk-resume-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ownedRoot);
        HostRun? source = null;
        HostRun? resumed = null;
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            var cancellationToken = timeout.Token;

            var sourceClock = new ControlledClock(10);
            source = CreateHost(profile, sourceClock, Path.Combine(ownedRoot, "source"));
            source.Server.Start();
            using var sourceClient = new TcpClient();
            var sourceConnection = await ConnectAsync(source, sourceClient, string.Empty, cancellationToken);
            Assert.True(sourceConnection.Hello.SupportsTargetCellPathing,
                "The actual v10 depth adapter must negotiate native target-cell pathing.");
            Assert.Equal(0UL, sourceConnection.Hello.LastClientSequence);

            var sessionId = SessionIdForToken(sourceConnection.Hello.SessionToken);
            var sourcePlayer = source.Executor.GetPlayerState(sessionId);
            var sourceEntityId = sourcePlayer.EntityId;
            Assert.False(Assert.Single(sourceConnection.Snapshot.Players,
                player => player.EntityId == sourceEntityId).NativeWalkPathPending);
            await WaitForSettledTownAsync(source, sessionId, cancellationToken);
            var settledPlayer = source.Executor.GetPlayerState(sessionId);
            Assert.Equal(0U, settledPlayer.LevelId);
            Assert.Equal(DiabloPlayerCombatMode.Stand, settledPlayer.CombatMode);
            Assert.Null(settledPlayer.CurrentAction);
            Assert.Null(settledPlayer.NativeWalkPath);
            var initialRandom = new DiabloCohortCodec().Deserialize(
                source.Store.Load()!.SerializedModuleState).RandomState;

            var townSnapshot = source.Executor.CreateSnapshot(
                sessionId, sourceEntityId, sourceClock.CurrentTick);
            var target = FindClearTownDestination(townSnapshot, sourceEntityId,
                settledPlayer.PositionX, settledPlayer.PositionY);
            Assert.InRange(target.Directions.Length, 12, 40);

            var requestedTick = checked(sourceClock.CurrentTick + 1);
            await EnvelopeCodec.WriteAsync(sourceConnection.Stream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = {
                        new Command {
                            ClientSequence = 1,
                            RequestedTick = requestedTick,
                            MoveRequested = new MoveRequested {
                                TargetCell = true,
                                TargetX = target.X,
                                TargetY = target.Y,
                                FollowPath = true,
                            },
                        },
                    },
                },
            }, cancellationToken);
            await WaitUntilAsync(() => source.Server.PendingCommandCount == 1, cancellationToken);
            sourceClock.Set(requestedTick);
            var commandReply = await ReadCommandReplyAsync(sourceConnection.Stream, cancellationToken);
            var accepted = Assert.Single(commandReply.Acknowledgement.Results);
            Assert.Equal(1UL, accepted.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, accepted.Status);
            Assert.Equal(requestedTick, accepted.AppliedTick);
            Assert.True(commandReply.Snapshot.Tick >= accepted.AppliedTick);
            Assert.True(Assert.Single(commandReply.Snapshot.Players,
                player => player.EntityId == sourceEntityId).NativeWalkPathPending,
                "The accepted destination command must expose its queued native path before the first step.");

            var firstCommandPlayer = source.Executor.GetPlayerState(sessionId);
            Assert.Equal((settledPlayer.PositionX, settledPlayer.PositionY),
                (firstCommandPlayer.PositionX, firstCommandPlayer.PositionY));
            // This host path captures after the already processed applied tick;
            // the next native player boundary starts the first reserved step.
            var acceptedDirections = firstCommandPlayer.NativeWalkPath!.Directions.ToArray();
            Assert.InRange(acceptedDirections.Length, 2,
                AuthoritativeNativePlayerPathfinder.MaximumPathLength - 1);
            Assert.Equal(1UL, Assert.Single(SessionReceipts(source.Store.Load()!, sessionId)));

            var movedPositions = new HashSet<(int X, int Y)> {
                (settledPlayer.PositionX, settledPlayer.PositionY),
            };
            var lastPosition = (firstCommandPlayer.PositionX, firstCommandPlayer.PositionY);
            var midTick = sourceClock.CurrentTick;
            for (var guard = 0; movedPositions.Count < 3 && guard < 1_200; guard++) {
                midTick = checked(midTick + 1);
                sourceClock.Set(midTick);
                await WaitForRuntimeTickAsync(source, sessionId, midTick, cancellationToken);
                var player = source.Executor.GetPlayerState(sessionId);
                Assert.Equal(0U, player.LevelId);
                var position = (player.PositionX, player.PositionY);
                if (position != lastPosition) {
                    movedPositions.Add(position);
                    lastPosition = position;
                }
                Assert.NotNull(player.NativeWalkPath);
                Assert.NotEqual((target.X, target.Y), position);
            }

            Assert.True(movedPositions.Count >= 3,
                "The one destination command must produce at least two committed native steps.");
            var midWalkPlayer = source.Executor.GetPlayerState(sessionId);
            Assert.NotNull(midWalkPlayer.NativeWalkPath);
            Assert.NotEmpty(midWalkPlayer.NativeWalkPath!.Directions);
            var consumedDirections = acceptedDirections.Length - midWalkPlayer.NativeWalkPath.Directions.Count;
            Assert.InRange(consumedDirections, 2, acceptedDirections.Length - 1);
            Assert.Equal(acceptedDirections.Skip(consumedDirections), midWalkPlayer.NativeWalkPath.Directions);
            Assert.NotEqual((target.X, target.Y), (midWalkPlayer.PositionX, midWalkPlayer.PositionY));

            var finalX = target.X;
            var finalY = target.Y;
            if (queueRanged) {
                Assert.True(sourceConnection.Hello.SupportsQueuedRangedAttacks);
                var activeMove = Assert.IsType<AuthoritativePlayerActionState>(midWalkPlayer.CurrentAction);
                Assert.Equal(DiabloPlayerActionKind.Move, activeMove.Kind);
                finalX = midWalkPlayer.PositionX + activeMove.DirectionX;
                finalY = midWalkPlayer.PositionY + activeMove.DirectionY;
                var attackTick = checked(midTick + 1);
                Assert.True(attackTick < activeMove.EndTick);
                await EnvelopeCodec.WriteAsync(sourceConnection.Stream, new Envelope {
                    CommandBatch = new CommandBatch { Commands = {
                        new Command { ClientSequence = 2, RequestedTick = attackTick,
                            AttackRequested = new AttackRequested { TargetCell = true,
                                TargetX = target.X, TargetY = target.Y, QueueDuringMove = true } },
                    } },
                }, cancellationToken);
                await WaitUntilAsync(() => source.Server.PendingCommandCount == 1, cancellationToken);
                sourceClock.Set(attackTick);
                var attackReply = await ReadCommandReplyAsync(sourceConnection.Stream, cancellationToken);
                var acceptedAttack = Assert.Single(attackReply.Acknowledgement.Results);
                Assert.Equal((2UL, CommandStatus.Accepted, attackTick),
                    (acceptedAttack.ClientSequence, acceptedAttack.Status, acceptedAttack.AppliedTick));
                midTick = attackTick;
                var queuedPlayer = source.Executor.GetPlayerState(sessionId);
                Assert.Equal(activeMove, queuedPlayer.CurrentAction);
                Assert.Null(queuedPlayer.NativeWalkPath);
                Assert.NotNull(queuedPlayer.NativeQueuedRangedAttack);
            }
            var expectedNextReceipt = queueRanged ? 3UL : 2UL;
            var sourceStore = source.Store;
            var midCheckpoint = await WaitForCheckpointAsync(sourceStore, midTick, cancellationToken);
            Assert.Equal(midTick, midCheckpoint.Tick);
            Assert.Equal(expectedNextReceipt, midCheckpoint.NextServerReceiptSequence);
            var midModule = new DiabloCohortCodec().Deserialize(midCheckpoint.SerializedModuleState);
            Assert.Equal(midTick, midModule.Tick);
            Assert.Equal(initialRandom, midModule.RandomState);
            var midParticipant = Assert.Single(midModule.Participants,
                participant => participant.SessionId == sessionId);
            Assert.Equal(sourceEntityId, midParticipant.EntityId);
            if (queueRanged) {
                Assert.Null(midParticipant.Player.NativeWalkPath);
                Assert.NotNull(midParticipant.Player.NativeQueuedRangedAttack);
            } else {
                Assert.NotNull(midParticipant.Player.NativeWalkPath);
                Assert.NotEmpty(midParticipant.Player.NativeWalkPath!.Directions);
            }

            var freshRoot = Path.Combine(ownedRoot, "fresh-resume");
            var freshStore = new AuthoritativeCohortStore(freshRoot);
            freshStore.Save(midCheckpoint with { Generation = 1 });
            var copiedCheckpoint = freshStore.Load()
                ?? throw new InvalidDataException("The copied mid-walk cohort was not committed.");
            Assert.Equal(midCheckpoint.Identity, copiedCheckpoint.Identity);
            Assert.Equal(midCheckpoint.Tick, copiedCheckpoint.Tick);
            Assert.Equal(midCheckpoint.NextEntityId, copiedCheckpoint.NextEntityId);
            Assert.Equal(midCheckpoint.NextServerReceiptSequence, copiedCheckpoint.NextServerReceiptSequence);
            Assert.Equal(midCheckpoint.SerializedModuleState, copiedCheckpoint.SerializedModuleState);
            Assert.Equal(midCheckpoint.Participants.Count, copiedCheckpoint.Participants.Count);
            Assert.Equal(
                Assert.Single(midCheckpoint.Participants, participant => participant.SessionId == sessionId)
                    .CommandReceipts.Select(receipt => receipt.ToByteArray()),
                Assert.Single(copiedCheckpoint.Participants, participant => participant.SessionId == sessionId)
                    .CommandReceipts.Select(receipt => receipt.ToByteArray()));

            var resumedClock = new ControlledClock(0);
            resumed = CreateHost(profile, resumedClock, freshRoot, freshStore);
            resumed.Server.Start();
            var restoredBeforeInput = freshStore.Load()
                ?? throw new InvalidDataException("The fresh host did not retain the copied cohort.");
            Assert.Equal(midCheckpoint.SerializedModuleState, restoredBeforeInput.SerializedModuleState);
            Assert.Equal(midCheckpoint.Tick, restoredBeforeInput.Tick);

            using var resumedClient = new TcpClient();
            var resumedConnection = await ConnectAsync(
                resumed, resumedClient, sourceConnection.Hello.SessionToken, cancellationToken);
            Assert.True(resumedConnection.Hello.SupportsTargetCellPathing);
            Assert.Equal(expectedNextReceipt - 1, resumedConnection.Hello.LastClientSequence);
            Assert.Equal(midCheckpoint.Tick, resumedConnection.Snapshot.Tick);

            var resumedDocument = freshStore.Load()
                ?? throw new InvalidDataException("The resumed host did not publish its pre-input checkpoint.");
            Assert.Equal(midCheckpoint.SerializedModuleState, resumedDocument.SerializedModuleState);
            Assert.Equal(midCheckpoint.Tick, resumedDocument.Tick);
            var resumedModule = new DiabloCohortCodec().Deserialize(resumedDocument.SerializedModuleState);
            var resumedParticipant = Assert.Single(resumedModule.Participants,
                participant => participant.SessionId == sessionId);
            Assert.Equal(midModule.RandomState, resumedModule.RandomState);
            Assert.Equal(midParticipant.Player.NativeWalkPath?.LevelId,
                resumedParticipant.Player.NativeWalkPath?.LevelId);
            Assert.Equal(midParticipant.Player.NativeWalkPath?.Directions,
                resumedParticipant.Player.NativeWalkPath?.Directions);
            Assert.Equal(midParticipant.Player.NativeQueuedRangedAttack,
                resumedParticipant.Player.NativeQueuedRangedAttack);
            Assert.Equal(midParticipant.Player.PositionX, resumedParticipant.Player.PositionX);
            Assert.Equal(midParticipant.Player.PositionY, resumedParticipant.Player.PositionY);
            Assert.Equal(midParticipant.Player.LevelId, resumedParticipant.Player.LevelId);
            Assert.Equal(midParticipant.Player.CurrentAction, resumedParticipant.Player.CurrentAction);

            Assert.Equal(sourceConnection.Hello.ContentManifestHash, resumedConnection.Hello.ContentManifestHash);
            Assert.Equal(sourceConnection.Hello.RulesetIdentityHash, resumedConnection.Hello.RulesetIdentityHash);
            Assert.Equal(sourceConnection.Hello.ContentManifestHash, resumedConnection.Snapshot.ContentManifestHash);
            Assert.Equal(sourceConnection.Hello.RulesetIdentityHash, resumedConnection.Snapshot.RulesetIdentityHash);
            var sourceMidSnapshot = source.Executor.CreateSnapshot(sessionId, sourceEntityId, midTick);
            // Direct module projection omits the host's validated wire identity binding.
            sourceMidSnapshot.ContentManifestHash = sourceConnection.Hello.ContentManifestHash;
            sourceMidSnapshot.RulesetIdentityHash = sourceConnection.Hello.RulesetIdentityHash;
            Assert.Equal(!queueRanged, Assert.Single(sourceMidSnapshot.Players,
                player => player.EntityId == sourceEntityId).NativeWalkPathPending);
            Assert.Equal(!queueRanged, Assert.Single(resumedConnection.Snapshot.Players,
                player => player.EntityId == sourceEntityId).NativeWalkPathPending);
            Assert.True(sourceMidSnapshot.ToByteArray().AsSpan()
                .SequenceEqual(resumedConnection.Snapshot.ToByteArray()),
                "Fresh-host initial projection must be the exact saved pre-input player/world snapshot.");

            var sourceFinal = await ContinueBothHostsToDestinationAsync(
                source, resumed, sourceClock, resumedClock, sessionId, finalX, finalY, cancellationToken);
            var resumedFinal = await WaitForCheckpointAsync(freshStore,
                resumed.Executor.GetSessionStateTick(sessionId), cancellationToken);
            Assert.Equal(sourceFinal.Tick, resumedFinal.Tick);
            Assert.True(StringComparer.Ordinal.Equals(sourceFinal.SerializedModuleState, resumedFinal.SerializedModuleState),
                "Fresh-host continuation must equal uninterrupted execution from the same committed checkpoint.");
            Assert.Equal(sourceFinal.NextServerReceiptSequence, resumedFinal.NextServerReceiptSequence);
            Assert.Equal(expectedNextReceipt, resumedFinal.NextServerReceiptSequence);

            var finalModule = new DiabloCohortCodec().Deserialize(resumedFinal.SerializedModuleState);
            Assert.Equal(midModule.RandomState, finalModule.RandomState);
            var finalParticipant = Assert.Single(finalModule.Participants,
                participant => participant.SessionId == sessionId);
            Assert.Equal(finalX, finalParticipant.Player.PositionX);
            Assert.Equal(finalY, finalParticipant.Player.PositionY);
            Assert.Equal(0U, finalParticipant.Player.LevelId);
            Assert.Null(finalParticipant.Player.NativeWalkPath);
            Assert.Null(finalParticipant.Player.NativeQueuedRangedAttack);
            Assert.Null(finalParticipant.Player.CurrentAction);
            Assert.Equal(DiabloPlayerCombatMode.Stand, finalParticipant.Player.CombatMode);
            var sourceFinalSnapshot = source.Executor.CreateSnapshot(sessionId, sourceEntityId, sourceFinal.Tick);
            var resumedFinalSnapshot = resumed.Executor.CreateSnapshot(sessionId, sourceEntityId, resumedFinal.Tick);
            Assert.False(Assert.Single(sourceFinalSnapshot.Players,
                player => player.EntityId == sourceEntityId).NativeWalkPathPending);
            Assert.False(Assert.Single(resumedFinalSnapshot.Players,
                player => player.EntityId == sourceEntityId).NativeWalkPathPending);
        } finally {
            if (resumed is not null)
                await resumed.Server.DisposeAsync();
            if (source is not null)
                await source.Server.DisposeAsync();
            DeleteOwnedTestRoot(ownedRoot, profile.RepositoryRoot);
        }
    }

    private static LoadedProfile? LoadProfile()
    {
        var repositoryRoot = FindRepositoryRoot();
        var profileRoot = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_WALK_PROFILE_ROOT");
        if (string.IsNullOrWhiteSpace(profileRoot))
            return null;
        profileRoot = Path.GetFullPath(profileRoot);
        var profilePath = Path.Combine(profileRoot, "cathedral-native-depth-progression-playtest.json");
        if (!File.Exists(profilePath))
            return null;

        using var profileDocument = JsonDocument.Parse(File.ReadAllText(profilePath));
        var profile = profileDocument.RootElement;
        if (profile.GetProperty("adapterId").GetString() != AdapterId
            || profile.GetProperty("nativeDepth").GetInt32() != 4)
            throw new InvalidDataException("The explicit profile is not the compiled native L1-L4 adapter profile.");

        var contentPack = ContentPackLoader.LoadManifestDirectory(Path.Combine(profileRoot,
            profile.GetProperty("contentDirectory").GetString()!));
        var populationPack = ContentPackLoader.LoadManifestDirectory(Path.Combine(profileRoot,
            profile.GetProperty("populationDirectory").GetString()!));
        var runtimePack = ContentPackLoader.LoadManifestDirectory(Path.Combine(profileRoot,
            profile.GetProperty("runtimeDirectory").GetString()!));
        var manifest = new ContentManifest(contentPack.Id, contentPack.Version,
            [contentPack, populationPack, runtimePack]);
        var composed = ComposedContent.Compose(manifest);
        var expectedHash = profile.GetProperty("contentManifestHash").GetString();
        if (!StringComparer.Ordinal.Equals(expectedHash, composed.Manifest.Sha256))
            throw new InvalidDataException("The external native-depth content manifest no longer matches its profile.");
        return new LoadedProfile(repositoryRoot, profileRoot, composed, AdapterId,
            profile.GetProperty("storySeed").GetUInt32());
    }

    private static HostRun CreateHost(
        LoadedProfile profile,
        ControlledClock clock,
        string storeRoot,
        AuthoritativeCohortStore? existingStore = null)
    {
        var store = existingStore ?? new AuthoritativeCohortStore(storeRoot);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory(profile.AdapterId);
        var playerStart = new AuthoritativePlayerStartConfiguration(null, null, null) {
            RandomSeed = profile.StorySeed,
            GameMode = AuthoritativeGameMode.SinglePlayer,
            Difficulty = AuthoritativeGameDifficulty.Normal,
            StartingLevelId = 0,
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            profile.Content.Manifest,
            factory.CreateBuiltInModules(),
            clock,
            (content, registry) => factory.Activate(registry, content, playerStart));
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var identity = ProtocolServerIdentity.FromRuleset(
            "native-depth-walk-resume-test",
            ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            20,
            runtime.Active.Identity,
            AuthoritativeGameMode.SinglePlayer);
        var server = new AuthoritativeTcpServer(
            new AuthoritativeCommandServer(adapter),
            new ProtocolHandshake(identity),
            clock,
            adapter,
            port: 0,
            address: IPAddress.Loopback) {
            CohortStore = store,
            UseClockGatedCommandScheduling = true,
        };
        return new HostRun(clock, store, runtime, server,
            Assert.IsType<StoreSimulationExecutor>(runtime.Active.Target));
    }

    private static async Task<ConnectedClient> ConnectAsync(
        HostRun host,
        TcpClient client,
        string resumeToken,
        CancellationToken cancellationToken)
    {
        await client.ConnectAsync(IPAddress.Loopback, host.Server.Port, cancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        var stream = client.GetStream();
        var identity = host.Server.ActiveContentManifestHash;
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "native-depth-walk-resume-test-client",
                ProtocolSchemaVersion = ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
                ContentManifestHash = identity,
                RulesetIdentityHash = host.Server.ActiveRulesetIdentityHash,
                ResumeToken = resumeToken,
                RequestedCharacterName = CharacterName,
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);

        var helloEnvelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, helloEnvelope.PayloadCase);
        Assert.NotEqual(string.Empty, helloEnvelope.ServerHello.SessionToken);
        Snapshot? snapshot = null;
        while (snapshot is null) {
            var envelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            if (envelope.PayloadCase == Envelope.PayloadOneofCase.Error)
                throw new InvalidDataException(envelope.Error.Detail);
            if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot)
                snapshot = envelope.Snapshot;
        }
        return new ConnectedClient(stream, helloEnvelope.ServerHello, snapshot);
    }

    private static async Task<CommandReply> ReadCommandReplyAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        CommandAck? acknowledgement = null;
        Snapshot? snapshot = null;
        while (acknowledgement is null || snapshot is null) {
            var envelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            if (envelope.PayloadCase == Envelope.PayloadOneofCase.CommandAck)
                acknowledgement = envelope.CommandAck;
            else if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot)
                snapshot = envelope.Snapshot;
            else if (envelope.PayloadCase == Envelope.PayloadOneofCase.Error)
                throw new InvalidDataException(envelope.Error.Detail);
        }
        return new CommandReply(acknowledgement, snapshot);
    }

    private static async Task<Envelope> ReadRequiredEnvelopeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        return envelope ?? throw new IOException("The native-depth walk host closed the TCP connection.");
    }

    private static (int X, int Y, int[] Directions) FindClearTownDestination(
        Snapshot snapshot,
        uint sourceEntityId,
        int startX,
        int startY)
    {
        var geometry = Assert.Single(snapshot.LevelGeometry, level => level.LevelId == 0);
        var terrainBlocked = geometry.BlockedCells.ToHashSet();
        var blocked = terrainBlocked.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var player in snapshot.Players.Where(player => player.LevelId == 0
                && player.EntityId != sourceEntityId))
            occupied.Add((player.PositionX, player.PositionY));
        foreach (var monster in snapshot.Monsters.Where(monster => monster.LevelId == 0 && monster.Alive)) {
            occupied.Add((monster.PositionX, monster.PositionY));
            if (monster.Moving)
                occupied.Add((monster.FuturePositionX, monster.FuturePositionY));
        }
        foreach (var value in snapshot.Objects.Where(value => value.LevelId == 0
                && !value.Activated && !value.Destroyed && !value.ActivateOnOccupancy))
            occupied.Add((value.PositionX, value.PositionY));

        bool InBounds(int x, int y)
            => x > 0 && y > 0 && x < geometry.Width - 1 && y < geometry.Height - 1
                && x <= byte.MaxValue && y <= byte.MaxValue;
        bool Walkable(int x, int y)
            => InBounds(x, y) && !blocked.Contains(checked(y * geometry.Width + x));
        bool Valid(int x, int y)
            => Walkable(x, y) && !occupied.Contains((x, y));
        bool CanStep(int fromX, int fromY, int toX, int toY)
        {
            var dx = toX - fromX;
            var dy = toY - fromY;
            return dx == 0 || dy == 0
                || !terrainBlocked.Contains(fromY * geometry.Width + fromX + dx)
                    && !terrainBlocked.Contains((fromY + dy) * geometry.Width + fromX);
        }

        for (var distance = 12; distance <= 36; distance++) {
            foreach (var offset in EnumerateRing(distance)) {
                var targetX = startX + offset.X;
                var targetY = startY + offset.Y;
                if (!InBounds(targetX, targetY) || !Valid(targetX, targetY))
                    continue;
                var directions = AuthoritativeNativePlayerPathfinder.FindPath(
                    startX, startY, targetX, targetY,
                    AuthoritativeNativePlayerPathfinder.MaximumPathLength,
                    Valid, CanStep);
                if (directions.Length is >= 12 and <= 40) {
                    var x = startX;
                    var y = startY;
                    var crossesWarp = false;
                    foreach (var direction in directions) {
                        var offsetStep = AuthoritativeNativePlayerPathfinder.GetDisplacement(direction);
                        x += offsetStep.X;
                        y += offsetStep.Y;
                        if (geometry.WarpTriggers.Any(warp => warp.TriggerX == x && warp.TriggerY == y)) {
                            crossesWarp = true;
                            break;
                        }
                    }
                    if (!crossesWarp)
                        return (targetX, targetY, directions);
                }
            }
        }
        throw new InvalidDataException("The external Town geometry has no clear 12-step native path candidate.");

        static IEnumerable<(int X, int Y)> EnumerateRing(int radius)
            => from y in Enumerable.Range(-radius, 2 * radius + 1)
                from x in Enumerable.Range(-radius, 2 * radius + 1)
                where Math.Max(Math.Abs(x), Math.Abs(y)) == radius
                orderby Math.Abs(x) + Math.Abs(y), x, y
                select (x, y);
    }

    private static async Task WaitForSettledTownAsync(
        HostRun host,
        string sessionId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 600; attempt++) {
            var player = host.Executor.GetPlayerState(sessionId);
            if (player.LevelId == 0 && player.Life > 0
                && player.CombatMode == DiabloPlayerCombatMode.Stand
                && player.CurrentAction is null && player.NativeWalkPath is null)
                return;
            var nextTick = checked(host.Clock.CurrentTick + 4);
            host.Clock.Set(nextTick);
            await WaitForRuntimeTickAsync(host, sessionId, nextTick, cancellationToken);
        }
        throw new TimeoutException("The actual native depth host did not settle the new Rogue in Town.");
    }

    private static async Task<AuthoritativeCohortDocument> ContinueBothHostsToDestinationAsync(
        HostRun uninterrupted,
        HostRun resumed,
        ControlledClock uninterruptedClock,
        ControlledClock resumedClock,
        string sessionId,
        int targetX,
        int targetY,
        CancellationToken cancellationToken)
    {
        for (var guard = 0; guard < 4_000; guard++) {
            var uninterruptedPlayer = uninterrupted.Executor.GetPlayerState(sessionId);
            var resumedPlayer = resumed.Executor.GetPlayerState(sessionId);
            Assert.Equal((uninterruptedPlayer.PositionX, uninterruptedPlayer.PositionY),
                (resumedPlayer.PositionX, resumedPlayer.PositionY));
            Assert.Equal(uninterruptedPlayer.NativeWalkPath?.Directions,
                resumedPlayer.NativeWalkPath?.Directions);
            var finished = IsAtDestination(uninterruptedPlayer, targetX, targetY)
                && IsAtDestination(resumedPlayer, targetX, targetY);
            if (finished) {
                var tick = uninterrupted.Executor.GetSessionStateTick(sessionId);
                return await WaitForCheckpointAsync(uninterrupted.Store, tick, cancellationToken);
            }

            var targetTick = checked(Math.Max(uninterruptedClock.CurrentTick, resumedClock.CurrentTick) + 4);
            uninterruptedClock.Set(targetTick);
            resumedClock.Set(targetTick);
            await Task.WhenAll(
                WaitForRuntimeTickAsync(uninterrupted, sessionId, targetTick, cancellationToken),
                WaitForRuntimeTickAsync(resumed, sessionId, targetTick, cancellationToken));
        }
        throw new TimeoutException("The saved native walk did not reach its captured destination within the bounded continuation.");

        static bool IsAtDestination(StorePlayerSnapshot player, int x, int y)
            => player.PositionX == x && player.PositionY == y
                && player.NativeWalkPath is null && player.CurrentAction is null
                && player.CombatMode == DiabloPlayerCombatMode.Stand;
    }

    private static async Task WaitForRuntimeTickAsync(
        HostRun host,
        string sessionId,
        ulong tick,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(() => host.Executor.GetSessionStateTick(sessionId) >= tick,
            cancellationToken);

    private static async Task<AuthoritativeCohortDocument> WaitForCheckpointAsync(
        AuthoritativeCohortStore store,
        ulong tick,
        CancellationToken cancellationToken)
    {
        AuthoritativeCohortDocument? document = null;
        await WaitUntilAsync(() => (document = store.Load()) is not null && document.Tick >= tick,
            cancellationToken);
        return document!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private static ulong[] SessionReceipts(AuthoritativeCohortDocument document, string sessionId)
        => Assert.Single(document.Participants, participant => participant.SessionId == sessionId)
            .CommandReceipts.Select(receipt => receipt.ClientSequence).ToArray();

    private static string SessionIdForToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                return current.FullName;
        throw new DirectoryNotFoundException("The repository root could not be located from the test output directory.");
    }

    private static void DeleteOwnedTestRoot(string root, string repositoryRoot)
    {
        var fullRoot = Path.GetFullPath(root);
        var expectedParent = Path.GetFullPath(Path.Combine(repositoryRoot, ".local", "native-depth-walk-resume-tests"));
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(fullRoot), expectedParent)
            || !Guid.TryParseExact(Path.GetFileName(fullRoot), "N", out _))
            throw new InvalidOperationException("Refusing to clean a path outside this native-walk test's owned root.");
        if (!Directory.Exists(fullRoot))
            return;
        if ((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Refusing to clean the native-walk test root through a reparse point.");
        Directory.Delete(fullRoot, recursive: true);
    }

    private sealed record LoadedProfile(
        string RepositoryRoot,
        string ProfileRoot,
        ComposedContent Content,
        string AdapterId,
        uint StorySeed);

    private sealed record HostRun(
        ControlledClock Clock,
        AuthoritativeCohortStore Store,
        GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> Runtime,
        AuthoritativeTcpServer Server,
        StoreSimulationExecutor Executor);

    private sealed record ConnectedClient(NetworkStream Stream, ServerHello Hello, Snapshot Snapshot);

    private sealed record CommandReply(CommandAck Acknowledgement, Snapshot Snapshot);

    private sealed class ControlledClock(ulong initialTick)
        : Devilution.Engine.IAuthoritativeClock, Devilution.Engine.IResumableAuthoritativeClock
    {
        private long currentTick = checked((long)initialTick);

        public ulong CurrentTick => checked((ulong)Interlocked.Read(ref currentTick));

        public void Set(ulong tick)
        {
            var value = checked((long)tick);
            var previous = Interlocked.Read(ref currentTick);
            if (value < previous)
                throw new InvalidOperationException("The controlled host clock cannot move backwards.");
            Interlocked.Exchange(ref currentTick, value);
        }

        public void RestoreAtTick(ulong tick) => Set(tick);
    }
}
