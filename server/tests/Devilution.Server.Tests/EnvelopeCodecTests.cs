using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class EnvelopeCodecTests
{
    [Fact]
    public void EnvelopeLimitUsesTheSharedProtocolMaximum()
    {
        Assert.Equal(
            AuthoritativeProtocolLimits.MaximumEnvelopeBytes,
            EnvelopeCodec.MaxEnvelopeBytes);
    }

    [Fact]
    public async Task RoundTripPreservesTheEnvelopePayload()
    {
        await using var stream = new MemoryStream();
        var cancellationToken = TestContext.Current.CancellationToken;
        var envelope = new Envelope {
            ServerHello = new ServerHello {
                ServerBuildId = "server-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                TickRateHz = 20,
            },
        };

        await EnvelopeCodec.WriteAsync(stream, envelope, cancellationToken);
        stream.Position = 0;
        var result = await EnvelopeCodec.ReadAsync(stream, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, result!.PayloadCase);
        Assert.Equal(envelope.ServerHello, result.ServerHello);
    }

    [Fact]
    public async Task CleanEndOfStreamReturnsNull()
    {
        await using var stream = new MemoryStream();

        var result = await EnvelopeCodec.ReadAsync(stream, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task TruncatedEnvelopeIsRejected()
    {
        await using var stream = new MemoryStream([1, 0, 0]);

        await Assert.ThrowsAsync<EndOfStreamException>(async () => await EnvelopeCodec.ReadAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OversizedEnvelopeIsRejectedBeforeWriting()
    {
        await using var stream = new MemoryStream();
        var envelope = new Envelope {
            Error = new ProtocolError { Detail = new string('x', EnvelopeCodec.MaxEnvelopeBytes) },
        };

        await Assert.ThrowsAsync<InvalidDataException>(async () => await EnvelopeCodec.WriteAsync(stream, envelope, TestContext.Current.CancellationToken));
    }
}
