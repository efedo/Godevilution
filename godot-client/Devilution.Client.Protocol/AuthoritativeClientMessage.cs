using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Immutable transport message drained by the Godot main thread. */
public sealed record AuthoritativeClientMessage(
    Snapshot? Snapshot = null,
    EventBatch? Events = null,
    CommandAck? Acknowledgement = null,
    ProtocolError? Error = null)
{
    /** Local receiving attempt; zero for offline messages. Never serialized on the product wire. */
    public long ConnectionAttemptEpoch { get; init; }

    /** Connection-negotiated body-reference policy, supplied by the receiving transport. */
    public bool LevelScopedIdleReferences { get; init; }

    /** Frozen local send identities for rescheduled receipts; never added to the wire. */
    public IReadOnlyList<AuthoritativeCommandReceiptContext>? AcknowledgedCommands { get; init; }

    public static AuthoritativeClientMessage FromEnvelope(Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return envelope.PayloadCase switch {
            Envelope.PayloadOneofCase.Snapshot => new(Snapshot: envelope.Snapshot),
            Envelope.PayloadOneofCase.EventBatch => new(Events: envelope.EventBatch),
            Envelope.PayloadOneofCase.CommandAck => new(Acknowledgement: envelope.CommandAck),
            Envelope.PayloadOneofCase.Error => new(Error: envelope.Error),
            _ => throw new InvalidDataException($"Unsupported captured payload: {envelope.PayloadCase}"),
        };
    }
}

/** Transport-owned identity, without retaining command payloads or a receipt registry. */
public readonly record struct AuthoritativeCommandReceiptContext(
    ulong ClientSequence, ulong RequestedTick, Command.IntentOneofCase Intent);
