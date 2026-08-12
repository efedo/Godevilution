namespace Devilution.Server.Stores;

/** Stable JSON envelope for versioned server-owned save payloads. */
public sealed record AuthoritativeSaveDocument(int FormatVersion, string SnapshotBase64);

/** Supplies serialized authoritative player saves to the server host. */
public interface IAuthoritativeSaveProvider
{
    string ExportPlayerSave(string sessionId);

    void ImportPlayerSave(string sessionId, string serializedSave);
}
