using Devilution.Protocol.V1;
using Devilution.Server.Host;

namespace Devilution.Server.Stores;

/** The immutable host identity fenced by a complete cohort checkpoint. */
public sealed record AuthoritativeCohortIdentity(
    string ModuleId,
    int ModuleFormatVersion,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash,
    uint TickRateHz,
    AuthoritativeGameMode GameMode);

/** Stable owner binding for one participant in the restored module roster. */
public sealed record AuthoritativeCohortParticipantCheckpoint(
    string SessionId,
    uint EntityId,
    IReadOnlyList<CommandResult> CommandReceipts);

/** One complete, versioned host recovery document. */
public sealed record AuthoritativeCohortDocument(
    int FormatVersion,
    ulong Generation,
    AuthoritativeCohortIdentity Identity,
    ulong Tick,
    uint NextEntityId,
    ulong NextServerReceiptSequence,
    IReadOnlyList<AuthoritativeCohortParticipantCheckpoint> Participants,
    string SerializedModuleState);
