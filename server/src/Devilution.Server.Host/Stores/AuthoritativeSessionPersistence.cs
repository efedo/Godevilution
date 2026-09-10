using Devilution.Protocol.V1;

namespace Devilution.Server.Stores;

/** Publishes the combined checkpoint before legacy compatibility mirrors. */
public sealed class AuthoritativeSessionPersistence
{
    private readonly AuthoritativeSaveStore? saveStore;
    private readonly AuthoritativeCommandReceiptStore? commandReceiptStore;
    private readonly AuthoritativeSessionStore? sessionStore;

    public AuthoritativeSessionPersistence(
        AuthoritativeSaveStore? saveStore = null,
        AuthoritativeCommandReceiptStore? commandReceiptStore = null,
        AuthoritativeSessionStore? sessionStore = null)
    {
        this.saveStore = saveStore;
        this.commandReceiptStore = commandReceiptStore;
        this.sessionStore = sessionStore;
    }

    /** Writes the authoritative checkpoint before compatibility-only files. */
    public void Save(
        string sessionId,
        string? serializedState,
        IReadOnlyDictionary<ulong, CommandResult> commandResults,
        string? rulesetIdentityHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(commandResults);

        if (sessionStore is not null && serializedState is not null)
            sessionStore.Save(sessionId, serializedState, commandResults, rulesetIdentityHash);
        if (saveStore is not null && serializedState is not null)
            saveStore.Save(sessionId, serializedState);
        if (commandReceiptStore is not null)
            commandReceiptStore.Save(sessionId, commandResults);
    }
}
