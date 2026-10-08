using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Public ServerHello fields useful for acceptance, excluding all session credentials. */
public sealed record AuthoritativeServerHelloObservation(
    string ServerBuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash,
    uint TickRateHz,
    GameMode GameMode,
    CommandSchedulingMode CommandSchedulingMode,
    ulong LastClientSequence,
    bool ResumedSessionMatched);

/**
 * The first decoded handshake snapshot for one transport attempt. It retains
 * at most one first-frame snapshot for the latest attempt. The 1 MiB
 * wire-envelope limit does not cap the snapshot's heap representation.
 */
public sealed class AuthoritativeInitialConnectionObservation
{
    private readonly Snapshot? firstWireSnapshot;

    internal AuthoritativeInitialConnectionObservation(long attemptEpoch)
        : this(attemptEpoch, null, null, admissionCompleted: false, takeSnapshotOwnership: true)
    {
    }

    private AuthoritativeInitialConnectionObservation(
        long attemptEpoch,
        AuthoritativeServerHelloObservation? serverHello,
        Snapshot? firstWireSnapshot,
        bool admissionCompleted,
        bool takeSnapshotOwnership)
    {
        if (attemptEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptEpoch));

        AttemptEpoch = attemptEpoch;
        ServerHello = serverHello;
        this.firstWireSnapshot = takeSnapshotOwnership
            ? firstWireSnapshot
            : firstWireSnapshot?.Clone();
        AdmissionCompleted = admissionCompleted;
    }

    public long AttemptEpoch { get; }

    public AuthoritativeServerHelloObservation? ServerHello { get; }

    /** Returns an independent protobuf clone; callers cannot mutate retained evidence. */
    public Snapshot? FirstWireSnapshot => firstWireSnapshot?.Clone();

    /**
     * Records successful handshake admission for this attempt and remains true
     * after disconnect or disposal. Consumers must also bind the current attempt
     * and current connection state.
     */
    public bool AdmissionCompleted { get; }

    internal bool HasFirstWireSnapshot => firstWireSnapshot is not null;

    internal AuthoritativeInitialConnectionObservation WithServerHello(
        AuthoritativeServerHelloObservation serverHello)
        => new(
            AttemptEpoch,
            serverHello,
            firstWireSnapshot,
            AdmissionCompleted,
            takeSnapshotOwnership: true);

    internal AuthoritativeInitialConnectionObservation WithFirstWireSnapshot(Snapshot snapshot)
        => new(
            AttemptEpoch,
            ServerHello,
            snapshot,
            AdmissionCompleted,
            takeSnapshotOwnership: false);

    internal AuthoritativeInitialConnectionObservation WithAdmissionCompleted(bool admissionCompleted)
        => new(
            AttemptEpoch,
            ServerHello,
            firstWireSnapshot,
            admissionCompleted,
            takeSnapshotOwnership: true);
}