using System.Buffers.Binary;
using Google.Protobuf;
using Devilution.Protocol.V1;

namespace Devilution.Server.Protocol;

/** Reads and writes bounded four-byte little-endian length-prefixed envelopes. */
public static class EnvelopeCodec
{
    public const int HeaderSize = sizeof(uint);
    public const int MaxEnvelopeBytes = 1024 * 1024;

    public static async ValueTask WriteAsync(Stream stream, Envelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(envelope);

        var payload = envelope.ToByteArray();
        ValidateLength(payload.Length);

        var header = new byte[HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async ValueTask<Envelope?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[HeaderSize];
        var hasData = await ReadFullyAsync(stream, header, allowCleanEndOfStream: true, cancellationToken);
        if (!hasData)
            return null;

        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        ValidateLength(length);
        var payload = new byte[(int)length];
        await ReadFullyAsync(stream, payload, allowCleanEndOfStream: false, cancellationToken);
        try {
            return Envelope.Parser.ParseFrom(payload);
        } catch (InvalidProtocolBufferException exception) {
            throw new InvalidDataException("The envelope payload is not valid Protobuf.", exception);
        }
    }

    private static void ValidateLength(int length)
    {
        if (length <= 0 || length > MaxEnvelopeBytes)
            throw new InvalidDataException($"Envelope length {length} is outside the allowed range.");
    }

    private static void ValidateLength(uint length)
    {
        if (length == 0 || length > MaxEnvelopeBytes)
            throw new InvalidDataException($"Envelope length {length} is outside the allowed range.");
    }

    private static async ValueTask<bool> ReadFullyAsync(Stream stream, Memory<byte> buffer, bool allowCleanEndOfStream, CancellationToken cancellationToken)
    {
        var bytesRead = 0;
        while (bytesRead < buffer.Length) {
            var read = await stream.ReadAsync(buffer[bytesRead..], cancellationToken);
            if (read == 0) {
                if (allowCleanEndOfStream && bytesRead == 0)
                    return false;
                throw new EndOfStreamException("The envelope was truncated.");
            }

            bytesRead += read;
        }

        return true;
    }
}
