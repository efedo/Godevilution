using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Host;

namespace Devilution.Server.Protocol;

public sealed record ProtocolServerIdentity(
    string BuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    uint TickRateHz)
{
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
        return new ProtocolServerIdentity(buildId, protocolSchemaVersion, ruleset.CombinedSha256, tickRateHz) {
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
    private readonly ProtocolServerIdentity identity;

    public ProtocolHandshake(ProtocolServerIdentity identity)
    {
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        if (string.IsNullOrWhiteSpace(identity.BuildId)
            || string.IsNullOrWhiteSpace(identity.ProtocolSchemaVersion)
            || string.IsNullOrWhiteSpace(identity.ContentManifestHash)
            || identity.TickRateHz == 0
            || !Enum.IsDefined(identity.GameMode))
            throw new ArgumentException("The server identity must contain all handshake fields.", nameof(identity));
    }

    public HandshakeResult Validate(ClientHello? clientHello)
    {
        if (clientHello is null
            || string.IsNullOrWhiteSpace(clientHello.ProtocolSchemaVersion)
            || string.IsNullOrWhiteSpace(clientHello.ContentManifestHash))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.InvalidMessage, "Client hello is missing required identity fields."));

        if (!string.Equals(clientHello.ProtocolSchemaVersion, identity.ProtocolSchemaVersion, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.UnsupportedSchema, "The client protocol schema version is not supported."));

        if (!string.Equals(clientHello.ContentManifestHash, identity.ContentManifestHash, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.ContentMismatch, "The client content manifest does not match the server."));

        if (!string.IsNullOrWhiteSpace(clientHello.RulesetIdentityHash)
            && !string.Equals(clientHello.RulesetIdentityHash, identity.RulesetIdentityHash, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.ContentMismatch, "The client gameplay ruleset does not match the server."));

        return HandshakeResult.Success(new ServerHello {
            ServerBuildId = identity.BuildId,
            ProtocolSchemaVersion = identity.ProtocolSchemaVersion,
            ContentManifestHash = identity.ContentManifestHash,
            TickRateHz = identity.TickRateHz,
            RulesetIdentityHash = identity.RulesetIdentityHash,
            GameMode = identity.GameMode switch {
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
}
