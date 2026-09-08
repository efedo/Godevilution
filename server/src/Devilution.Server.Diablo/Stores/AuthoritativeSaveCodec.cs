using System.Text;
using System.Text.Json;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;

namespace Devilution.Server.Stores;

/**
 * Compatibility codec for server-owned session state. Version 1 retains the
 * original JSON envelope containing a base64 Protobuf snapshot; player and
 * world state stay transport-neutral.
 */
public sealed class AuthoritativeSaveCodec
{
    public const int CurrentFormatVersion = 1;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly AuthoritativeItemCatalog? items;

    public AuthoritativeSaveCodec(AuthoritativeItemCatalog? items = null)
    {
        this.items = items;
    }

    public string Serialize(DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var protocolSnapshot = DiabloProtocolMapper.ToProtocolSnapshot(
            snapshot,
            items: items,
            allowZeroPlayerEntityId: true);
        return JsonSerializer.Serialize(new AuthoritativeSaveDocument(
            CurrentFormatVersion,
            Convert.ToBase64String(protocolSnapshot.ToByteArray())));
    }

    public DiabloSimulationSnapshot Deserialize(string serializedSave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedSave);
        try {
            StrictUtf8.GetBytes(serializedSave);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException(
                "The authoritative save contains invalid UTF-16 text.",
                exception);
        }
        AuthoritativeSaveDocument? document;
        try {
            document = JsonSerializer.Deserialize<AuthoritativeSaveDocument>(serializedSave);
        } catch (JsonException exception) {
            throw new InvalidDataException(
                "The authoritative save is not valid JSON.",
                exception);
        }
        if (document is null || string.IsNullOrWhiteSpace(document.SnapshotBase64)) {
            throw new InvalidDataException(
                "The authoritative save format is unsupported or incomplete.");
        }

        return document.FormatVersion switch {
            1 => DeserializeVersionOne(document.SnapshotBase64),
            _ => throw new InvalidDataException(
                $"Authoritative save format {document.FormatVersion} is unsupported."),
        };
    }

    private static DiabloSimulationSnapshot DeserializeVersionOne(string snapshotBase64)
    {
        byte[] snapshotBytes;
        try {
            snapshotBytes = Convert.FromBase64String(snapshotBase64);
        } catch (FormatException exception) {
            throw new InvalidDataException(
                "The authoritative save snapshot is not valid Base64.",
                exception);
        }
        if (!string.Equals(
                Convert.ToBase64String(snapshotBytes),
                snapshotBase64,
                StringComparison.Ordinal)) {
            throw new InvalidDataException(
                "The authoritative save snapshot must use canonical Base64 encoding.");
        }

        try {
            return DiabloProtocolMapper.FromProtocolSnapshot(
                Snapshot.Parser.ParseFrom(snapshotBytes));
        } catch (InvalidProtocolBufferException exception) {
            throw new InvalidDataException(
                "The authoritative save snapshot is not valid Protobuf.",
                exception);
        }
    }
}
