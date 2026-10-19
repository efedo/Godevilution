using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Immutable transport message drained by the Godot main thread. */
public sealed record AuthoritativeClientMessage(
    Snapshot? Snapshot = null,
    EventBatch? Events = null,
    CommandAck? Acknowledgement = null,
    ProtocolError? Error = null)
{
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
