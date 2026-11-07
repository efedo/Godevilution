using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class MainSnapshotAdmissionTests
{
    /** Real sockets/codec/ACK ledger; faults are supplied by the peer, never by a reconnect substitute. */
    private sealed class RecoveryPeer(bool rejectRecovery, CommandStatus? batchReceipt = null) : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly List<Task> connections = [];
        private Task? accept;
        private int handshakes;
        private long ledger;
        public int Handshakes => Volatile.Read(ref handshakes);
        public ClientConnectionOptions Options => new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            "admission-recovery", "0.2.0", new('c', 64), new('d', 64)) {
                UseSnapshotReferences = true, SnapshotPollInterval = TimeSpan.Zero,
            };
        public void Start()
        {
            listener.Start();
            accept = AcceptAsync();
        }
        private async Task AcceptAsync()
        {
            try {
                while (!stop.IsCancellationRequested) {
                    var socket = await listener.AcceptTcpClientAsync(stop.Token);
                    connections.Add(ServeAsync(socket));
                }
            } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
        private async Task ServeAsync(TcpClient socket)
        {
            using (socket) {
                try {
                    var stream = socket.GetStream();
                    var hello = await EnvelopeCodec.ReadAsync(stream, stop.Token);
                    Assert.NotNull(hello?.ClientHello);
                    var ordinal = Interlocked.Increment(ref handshakes);
                    await EnvelopeCodec.WriteAsync(stream, new Envelope { ServerHello = new() {
                        ProtocolSchemaVersion = "0.2.0", ContentManifestHash = new('c', 64), RulesetIdentityHash = new('d', 64),
                        SessionToken = "retained-session", HostInstanceId = "retained-host", TickRateHz = 20,
                        GameMode = GameMode.SinglePlayer, CommandSchedulingMode = CommandSchedulingMode.Immediate,
                        SupportsSnapshotReferences = true, LastClientSequence = (ulong)Interlocked.Read(ref ledger),
                    } }, stop.Token);
                    var full = FullSnapshot((ulong)(10 + ordinal * 2));
                    if (ordinal > 1 && rejectRecovery) full.Players[0].Life = 65;
                    await EnvelopeCodec.WriteAsync(stream, new Envelope { Snapshot = full }, stop.Token);
                    while (await EnvelopeCodec.ReadAsync(stream, stop.Token) is { } request) {
                        if (request.CommandBatch is not { } commands) continue;
                        var command = Assert.Single(commands.Commands);
                        Interlocked.Exchange(ref ledger, (long)command.ClientSequence);
                        if (ordinal == 1 && batchReceipt is { } status) {
                            await EnvelopeCodec.WriteAsync(stream, new Envelope { Snapshot = BadReference(full) }, stop.Token);
                            await EnvelopeCodec.WriteAsync(stream, new Envelope { Snapshot = full.Clone() }, stop.Token);
                            await EnvelopeCodec.WriteAsync(stream, new Envelope { CommandAck = new() {
                                Results = { new CommandResult { ClientSequence = command.ClientSequence, Status = status,
                                    AppliedTick = command.RequestedTick + 1, ServerReceiptSequence = command.ClientSequence } },
                            } }, stop.Token);
                        }
                    }
                } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (IOException) { } // A rejected connection is intentionally closed by the real reconnect path.
                catch (SocketException) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            listener.Stop();
            if (accept is not null) await accept;
            await Task.WhenAll(connections);
            stop.Dispose();
        }
    }
    private static Snapshot FullSnapshot(ulong tick) => new() {
        Tick = tick, StateSha256 = new('a', 64), ContentManifestHash = new('c', 64), RulesetIdentityHash = new('d', 64),
        GeometryRevision = 1, StateRevision = 1,
        Players = { new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64, PositionX = 1, PositionY = 1 } },
        LevelGeometry = { new LevelGeometrySnapshot { LevelId = 0, Width = 4, Height = 4 } },
    };
    private static Snapshot BadReference(Snapshot full) => new() {
        Tick = full.Tick + 1, StateSha256 = full.StateSha256,
        ContentManifestHash = full.ContentManifestHash, RulesetIdentityHash = full.RulesetIdentityHash,
        GeometryUnchanged = true, GeometryRevision = 2, StateUnchanged = true, StateRevision = 1,
    };
    private static async Task UntilAsync(Func<bool> condition)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(5, deadline.Token);
    }
}
