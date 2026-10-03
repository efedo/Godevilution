using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

/** Acceptance-only loopback relay: withhold one committed reply, never gameplay. */
internal sealed class AcceptanceAckLossRelay : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    private readonly int upstreamPort;
    private readonly ulong sequence;
    private readonly object sync = new();
    private readonly Task worker;
    private byte[]? originalCommand;
    private CommandResult? withheld;
    private CommandResult? recovered;
    private Exception? failure;
    private int connections;
    private int recoveredTransmissions;

    public AcceptanceAckLossRelay(int upstreamPort, ulong sequence)
    {
        this.upstreamPort = upstreamPort;
        this.sequence = sequence;
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        worker = RunAsync();
    }

    public int Port { get; }
    public CommandResult? Withheld { get { lock (sync) return withheld?.Clone(); } }
    public CommandResult? Recovered { get { lock (sync) return recovered?.Clone(); } }
    public int RecoveredTransmissions { get { lock (sync) return recoveredTransmissions; } }

    public void VerifyHealthy()
    {
        lock (sync)
            if (failure is not null)
                throw new InvalidOperationException("The acceptance ACK-loss relay failed.", failure);
    }

    private async Task RunAsync()
    {
        try {
            while (!lifetime.IsCancellationRequested) {
                using var downstream = await listener.AcceptTcpClientAsync(lifetime.Token);
                using var upstream = new TcpClient { NoDelay = true };
                downstream.NoDelay = true;
                try {
                    await upstream.ConnectAsync(IPAddress.Loopback, upstreamPort, lifetime.Token)
                        .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                } catch (SocketException) when (Volatile.Read(ref connections) > 0) {
                    // An automatic reconnect may arrive while the owned host is offline.
                    continue;
                }
                var first = Interlocked.Increment(ref connections) == 1;
                using var connection = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                var send = ForwardAsync(downstream.GetStream(), upstream.GetStream(), false, first, connection.Token);
                var receive = ForwardAsync(upstream.GetStream(), downstream.GetStream(), true, first, connection.Token);
                await Task.WhenAny(send, receive);
                connection.Cancel();
                downstream.Close();
                upstream.Close();
                await Task.WhenAll(send, receive);
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (ObjectDisposedException) when (lifetime.IsCancellationRequested) {
        } catch (SocketException) when (lifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            lock (sync) failure = exception;
        }
    }

    private async Task ForwardAsync(NetworkStream from, NetworkStream to, bool responses, bool first,
        CancellationToken cancellationToken)
    {
        var suppress = false;
        try {
            while (!cancellationToken.IsCancellationRequested) {
                var header = new byte[4];
                if (!await ReadExactlyAsync(from, header, cancellationToken))
                    return;
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length <= 0 || length > AuthoritativeProtocolLimits.MaximumEnvelopeBytes)
                    throw new InvalidDataException("The acceptance relay received an invalid frame size.");
                var payload = new byte[length];
                if (!await ReadExactlyAsync(from, payload, cancellationToken))
                    throw new EndOfStreamException("The acceptance relay received a partial frame.");
                var envelope = Envelope.Parser.ParseFrom(payload);
                if (!responses && envelope.PayloadCase == Envelope.PayloadOneofCase.CommandBatch) {
                    foreach (var command in envelope.CommandBatch.Commands.Where(command => command.ClientSequence == sequence)) {
                        var bytes = command.ToByteArray();
                        lock (sync) {
                            if (originalCommand is null) {
                                if (!first)
                                    throw new InvalidDataException("The pending command was not sent before host loss.");
                                originalCommand = bytes;
                            } else if (!originalCommand.AsSpan().SequenceEqual(bytes)) {
                                throw new InvalidDataException("Reconnect changed the original pending command bytes.");
                            }
                            if (!first)
                                recoveredTransmissions++;
                        }
                    }
                }
                if (responses && envelope.PayloadCase == Envelope.PayloadOneofCase.CommandAck) {
                    foreach (var result in envelope.CommandAck.Results.Where(result => result.ClientSequence == sequence)) {
                        lock (sync) {
                            if (first) {
                                withheld ??= result.Clone();
                                suppress = true;
                            } else {
                                recovered ??= result.Clone();
                            }
                        }
                    }
                }
                if (suppress)
                    continue;
                await to.WriteAsync(header, cancellationToken);
                await to.WriteAsync(payload, cancellationToken);
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        } catch (IOException) when (cancellationToken.IsCancellationRequested) {
        } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
        } catch (EndOfStreamException) when (first && Withheld is not null) {
            // Killing a host can truncate a later frame after the committed ACK.
        } catch (IOException exception) when (exception.InnerException is SocketException {
            SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted }) {
            // Abrupt owned-host termination is the deliberate transport fault.
        }
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < buffer.Length;) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                return offset == 0 ? false : throw new EndOfStreamException("The acceptance relay received truncated data.");
            offset += read;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        listener.Stop();
        try {
            await worker.WaitAsync(TimeSpan.FromSeconds(5));
            VerifyHealthy();
        } finally {
            lifetime.Dispose();
        }
    }
}
