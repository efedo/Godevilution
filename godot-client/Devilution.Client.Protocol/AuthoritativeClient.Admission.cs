namespace Devilution.Client.Protocol;

public sealed partial class AuthoritativeClient
{
    private int snapshotAdmissionRejected;
    private long rejectedSnapshotAttempt;

    /** A transport handshake alone cannot release a failed model-admission boundary. */
    public bool RequiresCompleteSnapshotAdmission => Volatile.Read(ref snapshotAdmissionRejected) != 0;

    /** Fences gameplay immediately on admission failure, until a new connection's complete baseline is admitted. */
    public void ApplyMessageToModel(AuthoritativeClientModel model, AuthoritativeClientMessage message)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(message);
        var recovering = RequiresCompleteSnapshotAdmission;
        var admittedRevision = model.AcceptedSnapshotRevision;
        try {
            if (recovering && message.Snapshot is { } snapshot
                && (snapshot.GeometryUnchanged || snapshot.StateUnchanged
                    || Interlocked.Read(ref initialConnectionAttemptEpoch) == Interlocked.Read(ref rejectedSnapshotAttempt)))
                throw new InvalidDataException("A new connection's complete authoritative snapshot is required after admission failure.");
            model.Apply(message);
            if (recovering && message.Snapshot is not null && model.AcceptedSnapshotRevision != admittedRevision)
                Volatile.Write(ref snapshotAdmissionRejected, 0);
        } catch {
            Interlocked.Exchange(ref rejectedSnapshotAttempt, Interlocked.Read(ref initialConnectionAttemptEpoch));
            Volatile.Write(ref snapshotAdmissionRejected, 1);
            model.ResetTransientPresentation();
            throw;
        }
    }
}
