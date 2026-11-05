using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Exercises the live clock-gated cohort host, including validation, publication and client admission. */
public sealed partial class NativeTrapHostPublicationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0, 53, 42)]
    [InlineData(1, 53, 42)]
    [InlineData(7, 53, 42)]
    [InlineData(0, 54, 43)]
    [InlineData(1, 54, 43)]
    [InlineData(7, 54, 43)]
    [InlineData(0, 68, 68)]
    [InlineData(27, 68, 68)]
    [InlineData(27, 69, 69)]
    [InlineData(27, 70, 70)]
    public async Task ObjectMissileLifecyclePublishesThroughTheLiveHost(int type, int sourceType, int triggerType)
    {
        var missiles = new NativeMissileSnapshots(type, sourceType, triggerType);
        await using var host = new SnapshotHost(0, missiles.Build, missiles.AdvanceTo);
        await host.ConnectAsync();
        var stages = new HashSet<NativeObjectMissileStage>();
        for (ulong tick = 1; tick <= 256; tick++) {
            var snapshot = await host.PollAsync(tick);
            foreach (var projectile in missiles.World.Projectiles.Values)
                stages.Add(projectile.NativeTrapArrow!.Stage);
            if (type == 1 && tick == 1) {
                var firebolt = Assert.Single(snapshot.Projectiles);
                Assert.Equal((151396408U, 151396470U), (firebolt.SourceEntityId, firebolt.TargetEntityId));
                Assert.Equal((54, 74, 67, 74), (firebolt.StartX, firebolt.StartY, firebolt.TargetX, firebolt.TargetY));
                Assert.Equal("Firebolt", firebolt.NativeMissileId);
                Assert.Equal(0U, firebolt.SpellId);
            }
            if (snapshot.Projectiles.Count == 0) break;
        }
        Assert.Empty(missiles.World.Projectiles);
        Assert.Contains(NativeObjectMissileStage.Flight, stages);
        if (type == 1) Assert.Contains(NativeObjectMissileStage.Explosion, stages);
        if (type == 7) Assert.Contains(NativeObjectMissileStage.LightningSegment, stages);
        if (type == 27) Assert.Contains(NativeObjectMissileStage.ElementalImpact, stages);
        Assert.Null(host.Server.LastClientFailure);
        Assert.False(host.Server.Completion.IsCompleted);
    }

    [Fact]
    public async Task ObjectMissilePublishesAtRecordedHostBoundary()
    {
        var missiles = new NativeMissileSnapshots(1, 53, 42);
        await using var host = new SnapshotHost(0, missiles.Build, missiles.AdvanceTo);
        await host.ConnectAsync();
        // The failing host advanced from the operation at 5640 to publication at 5646.
        var snapshot = await host.PollAsync(6);
        var missile = Assert.Single(snapshot.Projectiles);
        Assert.Equal("Firebolt", missile.NativeMissileId);
        Assert.Equal((151396408U, 151396470U), (missile.SourceEntityId, missile.TargetEntityId));
        Assert.Equal(0U, missile.SpellId);
        Assert.Null(host.Server.LastClientFailure);
        Assert.False(host.Server.Completion.IsCompleted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void InvalidObjectMissilesStillFailBothHostAndClientAdmission(int corruption)
    {
        var missiles = new NativeMissileSnapshots(1, 53, 42);
        missiles.AdvanceTo(1);
        var snapshot = missiles.Build(1);
        switch (corruption) {
        case 0: snapshot.Objects[0].NativeObjectId = 57; break;
        case 1: snapshot.Objects[1].NativeObjectId = 54; break;
        case 2: snapshot.Projectiles[0].TargetEntityId = 999; break;
        case 3: snapshot.Objects[0].LevelId = 6; break;
        case 4: snapshot.Projectiles[0].NativeMissileId = "UnownedMissile"; break;
        case 5: snapshot.Projectiles[0].Phase = 1; break;
        case 6: snapshot.Projectiles[0].RemainingTicks = 257; break;
        case 7: snapshot.Projectiles[0].SourceEntityId = 1; break;
        case 8: snapshot.Projectiles[0].Damage = -1; break;
        case 9: snapshot.Projectiles[0].StartX++; break;
        case 10: snapshot.Projectiles[0].OriginalDirection = 8; break;
        case 11: snapshot.Projectiles[0].RemainingTicks = 0; break;
        }
        Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(snapshot));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel()
            .Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public async Task SavedRequiredDoorStatePublishesAtRecordedHostBoundaryAndAdvancesTheClient()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_TRAP_HOST_SESSION");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select a private required-door session and profile to publish its captured state through the host.");
        var log = Directory.GetFiles(Path.Combine(session!, "save-command-logs"), HostCommandLog.LogFileName,
            SearchOption.AllDirectories).Single();
        var directory = Path.GetDirectoryName(log)!;
        var records = CommandLogReader.ReadAll(log);
        var operation = records.Select((record, index) => (record, index)).Last(entry =>
            entry.record.ServerCommand?.Command.OperateObjectRequested?.ObjectEntityId == 151396470);
        var operatedTick = operation.record.ServerCommand.Result.AppliedTick;
        var checkpoint = records.Select((record, index) => (record, index)).Last(entry => entry.index < operation.index
            && entry.record.Checkpoint is { } marker && marker.Tick < operatedTick
            && File.Exists(Path.Combine(directory, marker.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(
            Path.Combine(directory, checkpoint.record.Checkpoint.FileName)));
        var executor = Restore(document, profile!);
        var participant = Assert.Single(document.Participants);
        // Fencing stops advancement; the leave record retains the failed live publication boundary.
        var publicationTick = records.Last(value => value.ParticipantLeft?.SessionId == participant.SessionId)
            .ParticipantLeft.Tick;
        Assert.InRange(publicationTick, operatedTick + 1, operatedTick + 128);
        foreach (var record in records.Skip(checkpoint.index + 1).Take(operation.index - checkpoint.index)) {
            if (record.ServerCommand is not { } logged) continue;
            executor.AdvanceTo(logged.CurrentTick);
            var actual = executor.Execute(logged.SessionId, logged.Command.Clone(), logged.Result.AppliedTick);
            Assert.Equal(logged.Result.Status == CommandStatus.Accepted, actual.Succeeded);
        }
        output.WriteLine($"Replayed accepted door operation at {operatedTick}; module assembly {typeof(StoreSimulationExecutor).Assembly.ManifestModule.ModuleVersionId}.");
        // Only the host's small test roster is persisted; private module data stays in memory.
        await using var host = new SnapshotHost(operatedTick,
            tick => executor.CreateSnapshot(participant.SessionId, participant.EntityId, tick), executor.AdvanceTo);
        await host.ConnectAsync();
        ulong? explosionTick = null;
        ulong? expiryTick = null;
        var firebolts = new HashSet<uint>();
        for (var tick = publicationTick; tick <= operatedTick + 128; tick++) {
            var snapshot = await host.PollAsync(tick);
            var missiles = snapshot.Projectiles.Where(value => value.SourceEntityId == 151396408).ToArray();
            foreach (var missile in missiles.Where(value => value.NativeMissileId == "Firebolt")) firebolts.Add(missile.EntityId);
            if (tick == publicationTick) {
                var missile = Assert.Single(missiles);
                Assert.Equal("Firebolt", missile.NativeMissileId);
                Assert.Equal(0U, missile.SpellId);
                Assert.Equal((54, 74, 67, 74), (missile.StartX, missile.StartY, missile.TargetX, missile.TargetY));
                output.WriteLine($"Published recorded host boundary {tick}: {missile.NativeMissileId} at {missile.PositionX},{missile.PositionY}, damage {missile.Damage}, source {missile.SourceEntityId}, trigger {missile.TargetEntityId}.");
            }
            if (missiles.FirstOrDefault(value => value.NativeMissileId == "MagmaBallExplosion") is { } boom && explosionTick is null) {
                explosionTick = tick;
                output.WriteLine($"Published explosion tick {tick} at {boom.PositionX},{boom.PositionY}.");
            }
            if (missiles.Length == 0) expiryTick ??= tick;
            if (expiryTick is not null && tick >= expiryTick + 8) break;
        }
        Assert.NotNull(explosionTick);
        Assert.NotNull(expiryTick);
        Assert.Single(firebolts);
        var final = new DiabloCohortCodec().Deserialize(executor.CaptureCohortState().SerializedState);
        var fired = final.NativeTraps!.Single(value => value.Facts.TriggerEntityId == 151396470);
        Assert.Equal(DiabloNativeTrapDisposition.Fired, fired.Disposition);
        Assert.Equal(1, fired.TrapVar4);
        Assert.False(fired.TriggerTrapArmed);
        Assert.Null(host.Server.LastClientFailure);
        Assert.False(host.Server.Completion.IsCompleted);
        output.WriteLine($"Published expiry tick {expiryTick}; client reached {host.Model.Snapshot!.Tick}; host remained healthy.");
    }

    private static StoreSimulationExecutor Restore(AuthoritativeCohortDocument document, string profile)
    {
        var saved = new DiabloCohortCodec().Deserialize(document.SerializedModuleState);
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime",
            "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile, name))).ToArray();
        var content = ComposedContent.Compose(new ContentManifest(packs[0].Id, packs[0].Version, packs));
        var classId = saved.Participants[0].Player.CharacterClassId;
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(
            content, classId, saved.Configuration.InitialRandomSeed, saved.Configuration.GameMode);
        var template = activation.PlayerLoadouts.CreateTemplate("default", classId, null, null, null, 0, null, 75, 68);
        var executor = new StoreSimulationExecutor(activation, (_, _) => template, null,
            AuthoritativeProtocolFeatures.TargetCellPathing | AuthoritativeProtocolFeatures.QueuedRangedAttacks
                | AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots);
        using var prepared = executor.PrepareCohortRestore(new AuthoritativeModuleCohortState(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(),
            document.SerializedModuleState));
        executor.CommitCohortRestore(prepared);
        return executor;
    }

    private sealed class NativeMissileSnapshots(int type, int sourceType, int triggerType)
    {
        private readonly DeterministicLegacyRandom random = new(2588);
        private readonly AuthoritativePlayerState player = new(0, 0, 6400, 0, 0, PlayerAttributesState.Zero,
            [], [], [], 60, 60, 6400, 1, 7, 0) { EntityId = 1 };
        private DiabloCombatCommandProcessor? processor;
        private ulong currentTick;
        public DiabloWorldState World { get; } = new(fallbackWidth: 112, fallbackHeight: 112,
            fallbackMissileBlockedCells: [74 * 112 + 76]);

        public void AdvanceTo(ulong tick)
        {
            processor ??= new DiabloCombatCommandProcessor(World, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
                new AuthoritativeCombatRules(10, 1, 100), random);
            while (currentTick < tick) {
                currentTick++;
                if (currentTick == 1) {
                    var initial = DiabloCombatCommandProcessor.AddObjectTrapMissile(800, 151396408,
                        sourceType == triggerType ? 151396408U : 151396470U, 7, 1, 54, 74, 67, 74,
                        (int)DeterministicGridDirections.Toward(54, 74, 67, 74), type, sourceType == triggerType, random, 7);
                    World.Projectiles.Add(initial.EntityId, initial);
                    continue;
                }
                var ids = World.Projectiles.Keys.Order().ToList(); var seen = ids.ToHashSet();
                for (var index = 0; index < ids.Count; index++) {
                    if (World.Projectiles.TryGetValue(ids[index], out var missile))
                        processor.ProcessObjectTrapMissile(currentTick, missile, 7, [player], []);
                    foreach (var id in World.Projectiles.Keys.Order()) if (seen.Add(id)) ids.Add(id);
                }
            }
        }

        public Snapshot Build(ulong tick)
        {
            var intrinsic = sourceType == triggerType;
            var objects = new List<DiabloObjectState> { NativeObject(151396408, (uint)sourceType, intrinsic, 54, 74) };
            if (!intrinsic) objects.Add(NativeObject(151396470, (uint)triggerType, false, 68, 74));
            var projectiles = World.Projectiles.Values.OrderBy(value => value.EntityId)
                .Select(value => JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(value))!).ToArray();
            return DiabloProtocolMapper.ToProtocolSnapshot(new DiabloSimulationSnapshot(tick,
                new StorePlayerSnapshot(0, null, [], 0, 6400, 0, 0, PlayerAttributesState.Zero, [], [], []) {
                    EntityId = 1, LevelId = 7, PositionX = 60, PositionY = 60,
                }, [], [], objects.ToArray(), [], projectiles, null));
        }

        private static DiabloObjectState NativeObject(uint entity, uint type, bool activated, int x, int y)
            => new(entity, type, 7, x, y, activated, 0, AuthoritativeObjectEffectKind.None, 0) {
                NativeObjectId = type, MissilePassable = true, HideWhenActivated = false,
            };
    }

    /** The production host builds and validates each snapshot before its real protobuf/TCP publication. */
    private sealed class SnapshotHost : IAsyncDisposable
    {
        private static readonly string ContentHash = new('a', 64);
        private static readonly string RulesetHash = new('b', 64);
        private readonly string root = Path.Combine(Path.GetTempPath(), "Godev-Native-Trap-Host-" + Guid.NewGuid().ToString("N"));
        private readonly CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        private readonly TcpClient client = new() { NoDelay = true };
        private readonly MutableClock clock;
        private NetworkStream stream = null!;
        public AuthoritativeTcpServer Server { get; }
        public AuthoritativeClientModel Model { get; } = new();

        public SnapshotHost(ulong initialTick, Func<ulong, Snapshot> build, Action<ulong> advance)
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            clock = new MutableClock(initialTick);
            var adapter = new SnapshotAdapter(build, advance);
            Server = new AuthoritativeTcpServer(new AuthoritativeCommandServer(adapter),
                new ProtocolHandshake(new ProtocolServerIdentity("native-trap-host", "0.1.0", ContentHash, 20) {
                    RulesetIdentityHash = RulesetHash,
                }), clock, adapter, port: 0, address: IPAddress.Loopback) {
                CohortStore = new AuthoritativeCohortStore(root),
                UseClockGatedCommandScheduling = true,
            };
        }

        public async Task ConnectAsync()
        {
            Server.Start();
            await client.ConnectAsync(IPAddress.Loopback, Server.Port, timeout.Token);
            stream = client.GetStream();
            await EnvelopeCodec.WriteAsync(stream, new Envelope { ClientHello = new ClientHello {
                ClientBuildId = "native-trap-client", ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = ContentHash, RulesetIdentityHash = RulesetHash,
                ResumeToken = "native-trap-host-participant", SupportsClockGatedCommands = true,
            } }, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ServerHello, (await ReadAsync()).PayloadCase);
            Accept(await ReadAsync(), clock.CurrentTick);
        }

        public async Task<Snapshot> PollAsync(ulong tick)
        {
            clock.Set(tick);
            await EnvelopeCodec.WriteAsync(stream, new Envelope { SnapshotRequest = new Envelope.Types.SnapshotRequest {
                TimingRequestId = tick,
            } }, timeout.Token);
            var response = await ReadAsync();
            var snapshot = Accept(response, tick);
            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, (await ReadAsync()).PayloadCase);
            return snapshot;
        }

        private Snapshot Accept(Envelope envelope, ulong tick)
        {
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, envelope.PayloadCase);
            Model.Apply(new AuthoritativeClientMessage(Snapshot: envelope.Snapshot));
            Assert.Equal(tick, Model.Snapshot!.Tick);
            return envelope.Snapshot;
        }

        private async Task<Envelope> ReadAsync()
        {
            var envelope = await EnvelopeCodec.ReadAsync(stream, timeout.Token);
            Assert.True(envelope is not null, $"The live host closed before publication: {Server.LastClientFailure}");
            return envelope!;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            try { await Server.DisposeAsync(); }
            finally {
                timeout.Dispose();
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class MutableClock(ulong tick) : IAuthoritativeClock
    {
        private long currentTick = checked((long)tick);
        public ulong CurrentTick => checked((ulong)Interlocked.Read(ref currentTick));
        public void Set(ulong tick) => Interlocked.Exchange(ref currentTick, checked((long)tick));
    }

    private sealed class SnapshotAdapter(Func<ulong, Snapshot> build, Action<ulong> advance)
        : IAuthoritativeGameHostAdapter, IAuthoritativeGameCohortAdapter
    {
        private readonly List<AuthoritativeModuleCohortParticipant> participants = [];
        private ulong tick;
        public AuthoritativeProtocolFeatures SupportedProtocolFeatures => AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots;
        public bool SupportsCohortPersistence => true;
        public string CohortModuleId => "native-trap-publication-test";
        public int CohortFormatVersion => 1;
        public bool IsEntityIdOccupied(uint entityId) => false;
        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick) => CommandExecutionResult.Accepted;
        public void AdvanceTo(ulong value) { advance(value); tick = value; }
        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong requestedTick) => build(requestedTick);
        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong requestedTick) => null;
        public string ExportPlayerSave(string sessionId) => "{}";
        public void ImportPlayerSave(string sessionId, string serializedSave) => throw new NotSupportedException();
        public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
            => participants.Add(new(sessionId, selection.EntityId!.Value));
        public AuthoritativeModuleCohortState CaptureCohortState() => new(CohortModuleId, CohortFormatVersion, tick, participants.ToArray(), "{}");
        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(AuthoritativeModuleCohortState state) => throw new NotSupportedException();
        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared) => throw new NotSupportedException();
    }
}
