using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class AuthoritativeClientTransportTests
{
    [Theory]
    [InlineData(0, AuthoritativeClientReceiveTerminalKind.EndOfStream, null)]
    [InlineData(1, AuthoritativeClientReceiveTerminalKind.ProtocolFailure, "System.IO.InvalidDataException")]
    [InlineData(2, AuthoritativeClientReceiveTerminalKind.ProtocolFailure, "Google.Protobuf.InvalidProtocolBufferException")]
    [InlineData(3, AuthoritativeClientReceiveTerminalKind.TransportFailure, "System.IO.EndOfStreamException")]
    [InlineData(4, AuthoritativeClientReceiveTerminalKind.Cancellation, null)]
    public async Task ReceiveTerminationRetainsItsKindWithoutChangingTransportOutcome(
        int mode, AuthoritativeClientReceiveTerminalKind expectedKind, string? expectedException)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recorder = new AuthoritativeClientCommandTimingRecorder();
        var server = Task.Run(async () => {
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = peer.GetStream();
            _ = await ReadEnvelopeAsync(stream);
            await WriteEnvelopeAsync(stream, new Envelope { ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0", ContentManifestHash = "content-hash",
                TickRateHz = 20, GameMode = GameMode.SinglePlayer,
            } });
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } });
            await connected.Task.WaitAsync(timeout.Token);
            if (mode == 4) {
                var read = await stream.ReadAsync(new byte[1], timeout.Token);
                Assert.Equal(0, read);
                return;
            }
            if (mode == 0) return;
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, mode == 1 ? 0 : mode == 2 ? 1 : 8);
            await stream.WriteAsync(header, timeout.Token);
            if (mode == 2) await stream.WriteAsync(new byte[] { 0x0f }, timeout.Token);
            if (mode == 3) await stream.WriteAsync(new byte[] { 0x08, 0x01 }, timeout.Token);
        }, timeout.Token);
        var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "receive-terminal-test", "0.1.0", "content-hash")) {
            CommandTimingRecorder = recorder,
        };
        var disposed = false;
        try {
            await client.ConnectAsync(timeout.Token);
            connected.SetResult();
            if (mode == 4) { await client.DisposeAsync(); disposed = true; }
            while (recorder.CaptureReport().ReceiveTerminals!.Count == 0)
                await Task.Delay(10, timeout.Token);
            await server.WaitAsync(timeout.Token);
            var terminal = Assert.Single(recorder.CaptureReport().ReceiveTerminals!);
            Assert.Equal(expectedKind, terminal.Kind);
            if (mode != 4) Assert.Equal(expectedException, terminal.ExceptionType);
            Assert.True(terminal.TransportGeneration > 0);
            Assert.True(terminal.StopwatchTimestamp > 0);
            Assert.False(client.IsConnected);
            Assert.Empty(recorder.CaptureReport().Commands);
        } finally {
            if (!disposed) await client.DisposeAsync();
        }
    }
}
