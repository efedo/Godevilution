namespace Devilution.Server.Stores;

/** Stable JSON envelope for versioned server-owned save payloads. */
public sealed record AuthoritativeSaveDocument(int FormatVersion, string SnapshotBase64);

/** Supplies serialized authoritative session state to the server host. */
public interface IAuthoritativeSaveProvider
{
    /** Legacy name retained for existing adapters and save files. */
    string ExportPlayerSave(string sessionId);

    /** Legacy name retained for existing adapters and save files. */
    void ImportPlayerSave(string sessionId, string serializedSave);

    /**
     * Exports the complete server-owned state needed to migrate a session.
     * Existing adapters remain compatible through the legacy save methods.
     */
    string ExportSessionState(string sessionId) => ExportPlayerSave(sessionId);

    /** Imports a complete server-owned session state into the named session. */
    void ImportSessionState(string sessionId, string serializedState)
        => ImportPlayerSave(sessionId, serializedState);

    /**
     * Returns the deterministic tick represented by the imported session
     * state. Legacy providers may return zero; the host retains its clock
     * boundary in that case.
     */
    ulong GetSessionStateTick(string sessionId) => 0;
}
