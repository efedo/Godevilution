using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Real framing and receive/poll/admission paths; only the peer's execution result is scripted. */
internal sealed class TooLateRetryPeer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly Channel<Command> commands = Channel.CreateUnbounded<Command>();
    private TcpClient? peer;
    private NetworkStream stream = null!;
    private Task receiver = Task.CompletedTask;
    private ulong receiptSequence;
    private bool admitToClient = true;
    public ManualTimeProvider Time { get; } = new();
    public AuthoritativeClient Client { get; private set; } = null!;
    public AuthoritativeClientModel Model { get; } = new();
    public Snapshot World { get; private set; } = new() { Tick = 100, ContentManifestHash = "content", RulesetIdentityHash = "rules",
        Players = { new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64 } },
        LevelGeometry = { new LevelGeometrySnapshot { Width = 4, Height = 4 } },
    };
    public List<AuthoritativeClientMessage> Messages { get; } = [];
    public CancellationToken Token => timeout.Token;

    public static async Task<TooLateRetryPeer> StartAsync(bool admitToClient = true, ulong lastClientSequence = 0)
    {
        var fixture = new TooLateRetryPeer { admitToClient = admitToClient };
        fixture.timeout.CancelAfter(TimeSpan.FromSeconds(15)); fixture.listener.Start();
        fixture.Client = new(new("127.0.0.1", ((IPEndPoint)fixture.listener.LocalEndpoint).Port, "retry-test", "0.1.0", "content", "rules") {
            TimeProvider = fixture.Time, SnapshotPollInterval = TimeSpan.FromMinutes(1),
        });
        var connect = fixture.Client.ConnectAsync(fixture.Token);
        fixture.peer = await fixture.listener.AcceptTcpClientAsync(fixture.Token);
        fixture.stream = fixture.peer.GetStream();
        Assert.NotNull((await ReadAsync(fixture.stream, fixture.Token)).ClientHello);
        await fixture.SendAsync(new() { ServerHello = new() { ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content",
            RulesetIdentityHash = "rules", SessionToken = "retry", LastClientSequence = lastClientSequence, TickRateHz = 200, GameMode = GameMode.SinglePlayer,
            CommandSchedulingMode = CommandSchedulingMode.ClockGated, SupportsClockOnlyProbes = true } });
        await fixture.SnapshotAsync(); await connect;
        fixture.Drain(); fixture.receiver = fixture.ReceiveAsync();
        return fixture;
    }

    private async Task ReceiveAsync()
    {
        try {
            while (!Token.IsCancellationRequested) {
                var envelope = await ReadAsync(stream, Token);
                if (envelope.CommandBatch is { } batch) {
                    foreach (var command in batch.Commands) await commands.Writer.WriteAsync(command, Token);
                } else if (envelope.ClockProbeRequest is { } probe) {
                    await SendAsync(new() { TimingReply = new() { RequestId = probe.RequestId, ServerSchedulingClockTick = World.Tick } });
                } else if (envelope.SnapshotRequest is { } request) {
                    if (request.TimingRequestId != 0)
                        await SendAsync(new() { TimingReply = new() { RequestId = request.TimingRequestId } });
                    else await SnapshotAsync();
                }
            }
        } catch (Exception exception) when (timeout.IsCancellationRequested && exception is OperationCanceledException or IOException or ObjectDisposedException) { }
    }

    public void Drain()
    {
        foreach (var message in Client.DrainMessages()) {
            Assert.Null(message.Error);
            Messages.Add(message);
            if (admitToClient) Client.ApplyMessageToModel(Model, message); else Model.Apply(message);
        }
    }

    public async Task UntilAsync(Func<bool> ready, bool poll = true)
    {
        while (true) {
            Token.ThrowIfCancellationRequested(); Drain();
            if (ready()) return;
            if (poll) await Client.PollAsync(Token);
            await Task.Delay(1, Token); // Cooperative I/O polling, never a gameplay/timing witness.
        }
    }

    public async Task<Command> NextAsync()
    {
        var next = commands.Reader.ReadAsync(Token).AsTask();
        await UntilAsync(() => next.IsCompleted);
        return await next;
    }

    public async Task<CommandResult> ResultAsync(Command command, CommandRejectReason reason = CommandRejectReason.TooLate,
        CommandStatus status = CommandStatus.Rejected, bool snapshot = true, ulong? receipt = null)
    {
        World.Tick = Math.Max(World.Tick, command.RequestedTick + 6);
        var result = new CommandResult { ClientSequence = command.ClientSequence, ServerReceiptSequence = receipt ?? ++receiptSequence,
            AppliedTick = World.Tick, Status = status, RejectReason = reason };
        await SendAsync(new() { CommandAck = new() { Results = { result } } });
        if (snapshot) await SnapshotAsync();
        return result;
    }

    public Task SnapshotAsync() => SendAsync(new() { Snapshot = World.Clone() });
    public async Task SendAsync(Envelope envelope)
    {
        await writes.WaitAsync(Token);
        try { await WriteAsync(stream, envelope, Token); } finally { writes.Release(); }
    }

    internal static async Task WriteAsync(Stream stream, Envelope envelope, CancellationToken token)
    {
        var bytes = envelope.ToByteArray(); var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
    }
    internal static async Task<Envelope> ReadAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, token);
        var bytes = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
        await stream.ReadExactlyAsync(bytes, token); return Envelope.Parser.ParseFrom(bytes);
    }
    public async ValueTask DisposeAsync()
    {
        timeout.Cancel(); await Client.DisposeAsync(); peer?.Dispose(); listener.Stop();
        await receiver; timeout.Dispose(); writes.Dispose();
    }
}
