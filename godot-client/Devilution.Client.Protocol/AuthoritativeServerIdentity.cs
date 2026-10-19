using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Immutable identity and transport metadata accepted from a ServerHello. */
public sealed record AuthoritativeServerIdentity(
    string BuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash,
    uint TickRateHz,
    GameMode GameMode);
