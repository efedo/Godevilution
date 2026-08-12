using System.Collections.Concurrent;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Protocol;

/**
 * TCP client for the frozen authoritative protocol. Gameplay commands are
 * retained until acknowledged and resent using an adaptive latency timeout.
 */
public sealed class AuthoritativeClient : IAsyncDisposable
{
    private const int MaximumEnvelopeBytes = 1024 * 1024;
    private readonly ClientConnectionOptions options;
    private readonly ConcurrentQueue<AuthoritativeClientMessage> incoming = new();
    private readonly Dictionary<ulong, PendingCommand> pending = [];
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly object pendingLock = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private Task? receiveTask;
    private CancellationTokenSource? transportLifetime;
    private long transportGeneration;
    private int transportConnected;
    private ulong nextClientSequence;
    private long lastSnapshotRequestMs;
    private double roundTripMilliseconds = 100;

    public string SessionToken { get; private set; } = string.Empty;

    public uint ServerTickRateHz { get; private set; } = 20;

    public GameMode ServerGameMode { get; private set; } = GameMode.Unspecified;

    public ulong SuggestedCommandTick(ulong observedTick)
    {
        var leadTicks = Math.Max(1, (int)Math.Ceiling(RetryTimeout.TotalSeconds * ServerTickRateHz) + 1);
        return checked(observedTick + (ulong)leadTicks);
    }

    public AuthoritativeClient(ClientConnectionOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsConnected => Volatile.Read(ref transportConnected) == 1;

    public int PendingCommandCount
    {
        get
        {
            lock (pendingLock)
                return pending.Count;
        }
    }

    public TimeSpan RetryTimeout => TimeSpan.FromMilliseconds(Math.Clamp(
        roundTripMilliseconds * 4,
        options.MinimumRetryTimeout.TotalMilliseconds,
        options.MaximumRetryTimeout.TotalMilliseconds));

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            throw new InvalidOperationException("The authoritative client is already connected.");
        if (string.IsNullOrWhiteSpace(options.ContentManifestHash))
            throw new InvalidOperationException("DEVILUTION_CONTENT_HASH must match the server manifest before connecting.");

        tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(options.Host, options.Port, cancellationToken);
        stream = tcpClient.GetStream();
        var clientHello = new ClientHello {
            ClientBuildId = options.ClientBuildId,
            ProtocolSchemaVersion = options.ProtocolSchemaVersion,
            ContentManifestHash = options.ContentManifestHash,
            RulesetIdentityHash = options.RulesetIdentityHash,
            ResumeToken = string.IsNullOrWhiteSpace(SessionToken) ? options.ResumeToken : SessionToken,
        };
        if (options.RequestedCharacterClassId.HasValue) {
            clientHello.RequestedCharacterClassId
                = options.RequestedCharacterClassId.Value;
        }
        if (!string.IsNullOrEmpty(options.RequestedCharacterName))
            clientHello.RequestedCharacterName = options.RequestedCharacterName;
        await WriteEnvelopeAsync(new Envelope { ClientHello = clientHello }, cancellationToken);

        var hello = await ReadEnvelopeAsync(stream, cancellationToken)
            ?? throw new EndOfStreamException("The server closed the connection during handshake.");
        if (hello.PayloadCase == Envelope.PayloadOneofCase.Error)
            throw new InvalidDataException(hello.Error.Detail);
        if (hello.PayloadCase != Envelope.PayloadOneofCase.ServerHello)
            throw new InvalidDataException("The first server response must be ServerHello.");
        if (hello.ServerHello.ProtocolSchemaVersion != options.ProtocolSchemaVersion
            || hello.ServerHello.ContentManifestHash != options.ContentManifestHash)
            throw new InvalidDataException("The server returned an incompatible protocol or content identity.");

        SessionToken = hello.ServerHello.SessionToken;
        ServerTickRateHz = hello.ServerHello.TickRateHz == 0 ? 20 : hello.ServerHello.TickRateHz;
        ServerGameMode = hello.ServerHello.GameMode;

        var initialSnapshot = await ReadEnvelopeAsync(stream, cancellationToken)
            ?? throw new EndOfStreamException("The server closed the connection before the initial snapshot.");
        Enqueue(initialSnapshot);
        var connectedStream = stream;
        var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var connectionGeneration = Interlocked.Increment(ref transportGeneration);
        transportLifetime = connectionLifetime;
        Volatile.Write(ref transportConnected, 1);
        receiveTask = Task.Run(
            () => ReceiveLoopAsync(connectedStream, connectionLifetime.Token, connectionGeneration),
            CancellationToken.None);
    }

    /**
     * Reconnects this client instance using its existing resume token and
     * client-sequence ledger. Pending commands are made immediately eligible
     * for retry after the new authoritative snapshot arrives.
     */
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (lifetime.IsCancellationRequested)
            throw new ObjectDisposedException(nameof(AuthoritativeClient));

        await CloseTransportAsync();
        lock (pendingLock) {
            foreach (var pendingCommand in pending.Values)
                pendingCommand.LastSentMs = null;
        }
        lastSnapshotRequestMs = 0;
        await ConnectAsync(cancellationToken);
    }

    public ulong Queue(Command command, ulong requestedTick)
    {
        ArgumentNullException.ThrowIfNull(command);
        var sequence = checked(++nextClientSequence);
        command.ClientSequence = sequence;
        command.RequestedTick = requestedTick;
        lock (pendingLock)
            pending.Add(sequence, new PendingCommand(command));
        return sequence;
    }

    /** Sends new commands and resubmits commands whose adaptive timeout elapsed. */
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || stream is null)
            return;

        var now = clock.ElapsedMilliseconds;
        PendingCommand[] dueCommands;
        lock (pendingLock)
            dueCommands = pending.Values.Where(command => command.LastSentMs is null || now - command.LastSentMs >= RetryTimeout.TotalMilliseconds).ToArray();
        foreach (var entry in dueCommands) {
            await WriteEnvelopeAsync(new Envelope {
                CommandBatch = new CommandBatch { Commands = { entry.Command } },
            }, cancellationToken);
            entry.LastSentMs = now;
        }

        var hasPendingCommands = false;
        lock (pendingLock)
            hasPendingCommands = pending.Count > 0;
        if (!hasPendingCommands && now - lastSnapshotRequestMs >= options.SnapshotPollInterval.TotalMilliseconds) {
            await WriteEnvelopeAsync(new Envelope { SnapshotRequest = new Envelope.Types.SnapshotRequest() }, cancellationToken);
            lastSnapshotRequestMs = now;
        }
    }

    public IReadOnlyList<AuthoritativeClientMessage> DrainMessages()
    {
        var messages = new List<AuthoritativeClientMessage>();
        while (incoming.TryDequeue(out var message))
            messages.Add(message);
        return messages;
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        await CloseTransportAsync();
        lifetime.Dispose();
        writeLock.Dispose();
    }

    private async Task CloseTransportAsync()
    {
        Interlocked.Increment(ref transportGeneration);
        Volatile.Write(ref transportConnected, 0);
        transportLifetime?.Cancel();
        stream?.Dispose();
        tcpClient?.Dispose();
        stream = null;
        tcpClient = null;
        if (receiveTask is not null) {
            try {
                await receiveTask;
            } catch (Exception) {
            }
            receiveTask = null;
        }
        transportLifetime?.Dispose();
        transportLifetime = null;
    }

    private async Task ReceiveLoopAsync(
        NetworkStream networkStream,
        CancellationToken cancellationToken,
        long connectionGeneration)
    {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                var envelope = await ReadEnvelopeAsync(networkStream, cancellationToken);
                if (envelope is null)
                    break;
                Enqueue(envelope);
            }
        } catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && (exception is IOException or SocketException or EndOfStreamException or ObjectDisposedException)) {
            EnqueueConnectionClosed(exception.Message);
        } finally {
            if (Volatile.Read(ref transportGeneration) == connectionGeneration)
                Volatile.Write(ref transportConnected, 0);
        }
    }

    private void EnqueueConnectionClosed(string detail = "The authoritative connection closed.")
    {
        incoming.Enqueue(new AuthoritativeClientMessage(Error: new ProtocolError {
            Code = ProtocolErrorCode.NotAuthenticated,
            Detail = $"Authoritative connection closed: {detail}",
        }));
    }

    private void Enqueue(Envelope envelope)
    {
        if (envelope.PayloadCase == Envelope.PayloadOneofCase.CommandAck)
            Acknowledge(envelope.CommandAck);
        incoming.Enqueue(envelope.PayloadCase switch {
            Envelope.PayloadOneofCase.Snapshot => new AuthoritativeClientMessage(Snapshot: envelope.Snapshot),
            Envelope.PayloadOneofCase.EventBatch => new AuthoritativeClientMessage(Events: envelope.EventBatch),
            Envelope.PayloadOneofCase.CommandAck => new AuthoritativeClientMessage(Acknowledgement: envelope.CommandAck),
            Envelope.PayloadOneofCase.Error => new AuthoritativeClientMessage(Error: envelope.Error),
            _ => new AuthoritativeClientMessage(Error: new ProtocolError {
                Code = ProtocolErrorCode.InvalidMessage,
                Detail = $"Unexpected server payload: {envelope.PayloadCase}",
            }),
        });
    }

    private void Acknowledge(CommandAck acknowledgement)
    {
        var now = clock.ElapsedMilliseconds;
        foreach (var result in acknowledgement.Results) {
            lock (pendingLock) {
                if (!pending.Remove(result.ClientSequence, out var command) || command.LastSentMs is null)
                    continue;
                roundTripMilliseconds = (roundTripMilliseconds * 0.75) + ((now - command.LastSentMs.Value) * 0.25);
            }
        }
    }

    private async Task WriteEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.ToByteArray();
        if (payload.Length == 0 || payload.Length > MaximumEnvelopeBytes)
            throw new InvalidDataException("The authoritative envelope is outside the allowed size range.");
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

        await writeLock.WaitAsync(cancellationToken);
        try {
            await stream!.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        } finally {
            writeLock.Release();
        }
    }

    private static async Task<Envelope?> ReadEnvelopeAsync(NetworkStream networkStream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        if (!await ReadExactlyAsync(networkStream, header, cancellationToken))
            return null;
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaximumEnvelopeBytes)
            throw new InvalidDataException("The authoritative envelope length is outside the allowed range.");
        var payload = new byte[length];
        if (!await ReadExactlyAsync(networkStream, payload, cancellationToken))
            throw new EndOfStreamException("The authoritative envelope payload was truncated.");
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                return false;
            offset += read;
        }
        return true;
    }

    private sealed class PendingCommand(Command command)
    {
        public Command Command { get; } = command;
        public long? LastSentMs { get; set; }
    }
}
