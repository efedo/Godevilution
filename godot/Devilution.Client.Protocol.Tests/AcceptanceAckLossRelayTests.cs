using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Acceptance;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Negative checks execute the same scene-only relay used by actual Godot runs. */
public sealed class AcceptanceAckLossRelayTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(AuthoritativeProtocolLimits.MaximumEnvelopeBytes + 1)]
    public async Task RefusesInvalidFrameLengthsAndJoinsOwnedWorker(int length)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var relay = new AcceptanceAckLossRelay(((IPEndPoint)upstream.LocalEndpoint).Port, 1);
        Exception? primary = null;
        try {
            using var downstream = new TcpClient();
            await downstream.ConnectAsync(IPAddress.Loopback, relay.Port, timeout.Token);
            using var accepted = await upstream.AcceptTcpClientAsync(timeout.Token);
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, length);
            await downstream.GetStream().WriteAsync(header, timeout.Token);
            await RequireRelayFailureAsync(relay, timeout.Token);
        } catch (Exception exception) {
            primary = exception;
            throw;
        } finally {
            upstream.Stop();
            await DisposeRelayPreservingAsync(relay, primary);
        }
    }

    [Fact]
    public async Task RefusesChangedPendingCommandOnReplacementConnection()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var relay = new AcceptanceAckLossRelay(((IPEndPoint)upstream.LocalEndpoint).Port, 1);
        Exception? primary = null;
        try {
            using (var downstream = new TcpClient()) {
                await downstream.ConnectAsync(IPAddress.Loopback, relay.Port, timeout.Token);
                using var accepted = await upstream.AcceptTcpClientAsync(timeout.Token);
                var command = new Command { ClientSequence = 1, RequestedTick = 5,
                    SetFriendlyModeRequested = new SetFriendlyModeRequested { FriendlyMode = false } };
                await WriteAsync(downstream.GetStream(), new Envelope {
                    CommandBatch = new CommandBatch { Commands = { command } },
                }, timeout.Token);
                var forwarded = await ReadAsync(accepted.GetStream(), timeout.Token);
                Assert.Equal(command, Assert.Single(forwarded!.CommandBatch.Commands));
                await WriteAsync(accepted.GetStream(), new Envelope {
                    CommandAck = new CommandAck { Results = { new CommandResult {
                        ClientSequence = 1, ServerReceiptSequence = 1, AppliedTick = 5,
                        Status = CommandStatus.Accepted, RejectReason = CommandRejectReason.Unspecified,
                    } } },
                }, timeout.Token);
                while (relay.Withheld is null) {
                    relay.VerifyHealthy();
                    await Task.Delay(10, timeout.Token);
                }
                Assert.Equal(0, downstream.Available);
                accepted.Close();
                Assert.Null(await ReadAsync(downstream.GetStream(), timeout.Token));
            }
            using var replacement = new TcpClient();
            await replacement.ConnectAsync(IPAddress.Loopback, relay.Port, timeout.Token);
            using var next = await upstream.AcceptTcpClientAsync(timeout.Token);
            await WriteAsync(replacement.GetStream(), new Envelope {
                CommandBatch = new CommandBatch { Commands = { new Command {
                    ClientSequence = 1, RequestedTick = 6,
                    SetFriendlyModeRequested = new SetFriendlyModeRequested { FriendlyMode = false },
                } } },
            }, timeout.Token);
            await RequireRelayFailureAsync(relay, timeout.Token);
        } catch (Exception exception) {
            primary = exception;
            throw;
        } finally {
            upstream.Stop();
            await DisposeRelayPreservingAsync(relay, primary);
        }
    }

    private static async Task DisposeRelayPreservingAsync(AcceptanceAckLossRelay relay, Exception? primary)
    {
        if (primary is null) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => relay.DisposeAsync().AsTask());
            return;
        }
        try { await relay.DisposeAsync(); } catch (Exception cleanup) {
            primary.Data["AcceptanceRelayCleanup"] = cleanup.Message;
        }
    }

    private static async Task RequireRelayFailureAsync(AcceptanceAckLossRelay relay, CancellationToken token)
    {
        while (true) {
            try { relay.VerifyHealthy(); } catch (InvalidOperationException exception) {
                Assert.IsType<InvalidDataException>(exception.InnerException);
                return;
            }
            await Task.Delay(10, token);
        }
    }

    private static async Task WriteAsync(NetworkStream stream, Envelope envelope, CancellationToken token)
    {
        var payload = envelope.ToByteArray();
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(payload, token);
    }

    private static async Task<Envelope?> ReadAsync(NetworkStream stream, CancellationToken token)
    {
        var header = new byte[4];
        var first = await stream.ReadAsync(header.AsMemory(0, 1), token);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), token);
        var payload = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
        await stream.ReadExactlyAsync(payload, token);
        return Envelope.Parser.ParseFrom(payload);
    }
}
