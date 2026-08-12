using Devilution.Protocol.V1;

namespace Devilution.Server.Protocol;

public sealed record CommandDeliveryVectorStep(
    string Operation,
    ulong ClientSequence,
    ulong? RequestedTick,
    ulong? ServerReceiptSequence,
    ulong? AppliedTick,
    CommandStatus? Status,
    CommandRejectReason? RejectReason,
    bool? AcknowledgementDelivered,
    ulong? AtMilliseconds);

public sealed record CommandDeliveryVector(
    int FormatVersion,
    string VectorId,
    string ProtocolSchemaVersion,
    uint InitialRttMilliseconds,
    IReadOnlyList<CommandDeliveryVectorStep> Steps,
    IReadOnlyList<string> Assertions);
