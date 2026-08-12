using System.Text.Json;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;

namespace Devilution.Server.Stores;

/**
 * Compatibility codec for server-owned saves. Version 1 retains the original
 * JSON envelope containing a base64 Protobuf snapshot; game state stays neutral.
 */
public sealed class AuthoritativeSaveCodec
{
    public const int CurrentFormatVersion = 1;

    public string Serialize(DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var protocolSnapshot = DiabloProtocolMapper.ToProtocolSnapshot(snapshot);
        return JsonSerializer.Serialize(new AuthoritativeSaveDocument(
            CurrentFormatVersion,
            Convert.ToBase64String(protocolSnapshot.ToByteArray())));
    }

    public DiabloSimulationSnapshot Deserialize(string serializedSave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedSave);
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
        Snapshot snapshot;
        try {
            snapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(snapshotBase64));
        } catch (Exception exception)
            when (exception is FormatException or InvalidProtocolBufferException) {
            throw new InvalidDataException(
                "The authoritative save snapshot is not valid.",
                exception);
        }
        return DiabloProtocolMapper.FromProtocolSnapshot(snapshot);
    }
}
