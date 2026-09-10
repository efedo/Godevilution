using Devilution.Engine;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Host;

namespace Devilution.Server.Protocol;

public sealed record ProtocolServerIdentity(
    string BuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    uint TickRateHz)
{
    /** Protocol v1 aliases content identity to the combined ruleset identity. */
    public const string LegacyProtocolSchemaVersion = ProtocolSchemaVersions.Legacy;

    /** Protocol v2 gives content and gameplay modules independent identities. */
    public const string IndependentIdentityProtocolSchemaVersion =
        ProtocolSchemaVersions.IndependentContentAndRulesetIdentity;

    /** Additive ruleset identity; legacy callers use the combined hash field. */
    public string RulesetIdentityHash { get; init; } = ContentManifestHash;

    public AuthoritativeGameMode GameMode { get; init; } = AuthoritativeGameMode.SinglePlayer;

    public static ProtocolServerIdentity FromRuleset(
        string buildId,
        string protocolSchemaVersion,
        uint tickRateHz,
        GameplayRulesetIdentity ruleset,
        AuthoritativeGameMode gameMode = AuthoritativeGameMode.SinglePlayer)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var contentManifestHash = string.Equals(
            protocolSchemaVersion,
            IndependentIdentityProtocolSchemaVersion,
            StringComparison.Ordinal)
            ? ruleset.Content.Sha256
            : ruleset.CombinedSha256;
        return new ProtocolServerIdentity(buildId, protocolSchemaVersion, contentManifestHash, tickRateHz) {
            RulesetIdentityHash = ruleset.CombinedSha256,
            GameMode = gameMode,
        };
    }
}

public readonly record struct HandshakeResult(bool Accepted, ServerHello? ServerHello, ProtocolError? Error)
{
    public static HandshakeResult Success(ServerHello serverHello)
    {
        return new(true, serverHello, null);
    }

    public static HandshakeResult Failure(ProtocolError error)
    {
        return new(false, null, error);
    }
}

/** Validates the identity contract before a session can submit commands. */
public sealed class ProtocolHandshake
{
    private readonly object synchronization = new();
    private ProtocolServerIdentity identity;

    public ProtocolHandshake(ProtocolServerIdentity identity)
    {
        ValidateIdentity(identity);
        this.identity = identity;
    }

    /** Returns the identity used for newly accepted handshakes. */
    public ProtocolServerIdentity Identity
    {
        get {
            lock (synchronization)
                return identity;
        }
    }

    /** Publishes a new ruleset identity after a host migration succeeds. */
    public void UpdateIdentity(ProtocolServerIdentity replacement)
    {
        ValidateIdentity(replacement);
        lock (synchronization)
            identity = replacement;
    }

    public HandshakeResult Validate(
        ClientHello? clientHello,
        bool allowResumedIdentityRefresh = false)
    {
        var currentIdentity = Identity;
        if (clientHello is null
            || string.IsNullOrWhiteSpace(clientHello.ProtocolSchemaVersion)
            || string.IsNullOrWhiteSpace(clientHello.ContentManifestHash))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.InvalidMessage, "Client hello is missing required identity fields."));

        if (!AuthoritativePlayerNameRules.IsValidShape(clientHello.RequestedCharacterName))
            return HandshakeResult.Failure(Error(
                ProtocolErrorCode.InvalidMessage,
                "Client hello contains an invalid requested character name."));

        if (!string.Equals(clientHello.ProtocolSchemaVersion, currentIdentity.ProtocolSchemaVersion, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.UnsupportedSchema, "The client protocol schema version is not supported."));

        if (!allowResumedIdentityRefresh
            && !string.Equals(clientHello.ContentManifestHash, currentIdentity.ContentManifestHash, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.ContentMismatch, "The client content manifest does not match the server."));

        if (string.Equals(
                currentIdentity.ProtocolSchemaVersion,
                ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
                StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(clientHello.RulesetIdentityHash))
            return HandshakeResult.Failure(Error(
                ProtocolErrorCode.ContentMismatch,
                "The client gameplay ruleset identity is required by this protocol version."));

        if (!allowResumedIdentityRefresh
            && !string.IsNullOrWhiteSpace(clientHello.RulesetIdentityHash)
            && !string.Equals(clientHello.RulesetIdentityHash, currentIdentity.RulesetIdentityHash, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.ContentMismatch, "The client gameplay ruleset does not match the server."));

        return HandshakeResult.Success(new ServerHello {
            ServerBuildId = currentIdentity.BuildId,
            ProtocolSchemaVersion = currentIdentity.ProtocolSchemaVersion,
            ContentManifestHash = currentIdentity.ContentManifestHash,
            TickRateHz = currentIdentity.TickRateHz,
            RulesetIdentityHash = currentIdentity.RulesetIdentityHash,
            GameMode = currentIdentity.GameMode switch {
                AuthoritativeGameMode.SinglePlayer => GameMode.SinglePlayer,
                AuthoritativeGameMode.Multiplayer => GameMode.Multiplayer,
                _ => throw new InvalidOperationException("The server game mode is invalid."),
            },
        });
    }

    private static ProtocolError Error(ProtocolErrorCode code, string detail)
    {
        return new ProtocolError { Code = code, Detail = detail };
    }

    private static void ValidateIdentity(ProtocolServerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.BuildId)
            || string.IsNullOrWhiteSpace(identity.ProtocolSchemaVersion)
            || string.IsNullOrWhiteSpace(identity.ContentManifestHash)
            || string.IsNullOrWhiteSpace(identity.RulesetIdentityHash)
            || identity.TickRateHz == 0
            || !Enum.IsDefined(identity.GameMode))
            throw new ArgumentException(
                "The server identity must contain all handshake fields.",
                nameof(identity));
    }
}
