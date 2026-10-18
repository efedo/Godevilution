using Google.Protobuf;
using Devilution.Protocol.V1;

namespace Devilution.Server.Host;

public sealed partial class AuthoritativeTcpServer
{
    /** Keeps only an opaque identity for the latest sent snapshot, not its payload. */
    private sealed class ClockedConnectionSnapshotWriteState
    {
        private object? lastSuccessfullyWrittenToken;

        public object RecordSuccessfulWrite()
        {
            var identityToken = new object();
            lastSuccessfullyWrittenToken = identityToken;
            return identityToken;
        }

        public void RecordSuccessfulReuse(object identityToken)
            => lastSuccessfullyWrittenToken = identityToken;

        public bool IsLastSuccessfullyWritten(object identityToken)
            => ReferenceEquals(lastSuccessfullyWrittenToken, identityToken);
    }

    private sealed record ClockedReusableSnapshot(Snapshot Snapshot, object IdentityToken);

    private async ValueTask<ClockedReusableSnapshot?> TryGetReusableDuplicateSnapshotAsync(
        ClockedCommandSnapshotReuseRegistry registry,
        ClockedCommandSnapshotReuseCandidate candidate,
        CommandAck acknowledgement,
        string sessionId,
        uint entityId,
        ulong currentTick,
        ClockedConnectionSnapshotWriteState writtenSnapshots,
        CancellationToken cancellationToken)
    {
        IDisposable? authorityTurn = await EnterCohortAuthorityTurnAsync(
            cancellationToken,
            classifyClockedTransportFailure: true);
        try {
            return commandServer.WithSynchronizedSession(sessionId, () => {
                RequireClockedTransportActive(true, cancellationToken);
                RequireCohortHealthy();
                if (!SupportsCommitBoundaryCommandSnapshots
                    || !StringComparer.Ordinal.Equals(
                        handshake.Identity.ContentManifestHash,
                        candidate.ContentManifestHash)
                    || !StringComparer.Ordinal.Equals(
                        handshake.Identity.RulesetIdentityHash,
                        candidate.RulesetIdentityHash)
                    || !ReferenceEquals(GetRulesetGenerationToken(), candidate.GenerationToken)) {
                    return null;
                }

                return registry.TryGetReusableSnapshot(
                    candidate,
                    acknowledgement,
                    sessionId,
                    entityId,
                    handshake.Identity.ContentManifestHash,
                    handshake.Identity.RulesetIdentityHash,
                    GetRulesetGenerationToken(),
                    currentTick,
                    writtenSnapshots);
            });
        } finally {
            authorityTurn?.Dispose();
        }
    }

    /** Retains a detached command snapshot only while its original/linked work is in flight. */
    private sealed class ClockedCommandSnapshotReuseRegistry
    {
        private readonly object synchronization = new();
        private readonly Dictionary<ulong, ClockedCommandSnapshotReuseCandidate> inFlight = [];

        public ClockedCommandSnapshotReuseRegistration RegisterOrLink(
            string sessionId,
            uint entityId,
            string contentManifestHash,
            string rulesetIdentityHash,
            object? generationToken,
            CommandBatch batch)
        {
            if (batch.Commands.Count != 1)
                return default;

            var command = batch.Commands[0];
            var batchBytes = batch.ToByteArray();
            lock (synchronization) {
                if (inFlight.TryGetValue(command.ClientSequence, out var existing)) {
                    if (existing.TryAddLinkedReference(
                        sessionId,
                        entityId,
                        contentManifestHash,
                        rulesetIdentityHash,
                        generationToken,
                        batchBytes)) {
                        return new ClockedCommandSnapshotReuseRegistration(null, existing);
                    }
                    return default;
                }

                var candidate = new ClockedCommandSnapshotReuseCandidate(
                    this,
                    sessionId,
                    entityId,
                    contentManifestHash,
                    command.ClientSequence,
                    rulesetIdentityHash,
                    generationToken,
                    batchBytes);
                inFlight.Add(command.ClientSequence, candidate);
                return new ClockedCommandSnapshotReuseRegistration(candidate, null);
            }
        }

        public ClockedReusableSnapshot? TryGetReusableSnapshot(
            ClockedCommandSnapshotReuseCandidate candidate,
            CommandAck duplicateAcknowledgement,
            string sessionId,
            uint entityId,
            string contentManifestHash,
            string rulesetIdentityHash,
            object? generationToken,
            ulong currentTick,
            ClockedConnectionSnapshotWriteState writtenSnapshots)
        {
            lock (synchronization) {
                if (!StringComparer.Ordinal.Equals(candidate.SessionId, sessionId)
                    || candidate.EntityId != entityId
                    || !StringComparer.Ordinal.Equals(candidate.ContentManifestHash, contentManifestHash)
                    || !StringComparer.Ordinal.Equals(candidate.RulesetIdentityHash, rulesetIdentityHash)
                    || !ReferenceEquals(candidate.GenerationToken, generationToken)
                    || candidate.ReusableSnapshot is not { } snapshot
                    || candidate.SnapshotIdentityToken is not { } identityToken
                    || !writtenSnapshots.IsLastSuccessfullyWritten(identityToken)
                    || snapshot.Tick < currentTick
                    || !IsMatchingAcceptedDuplicate(candidate.OriginalAcknowledgement, duplicateAcknowledgement,
                        candidate.ClientSequence)) {
                    return null;
                }
                return new ClockedReusableSnapshot(snapshot, identityToken);
            }
        }

        public void PublishReusableSnapshot(
            ClockedCommandSnapshotReuseCandidate candidate,
            CommandAck acknowledgement,
            Snapshot snapshot,
            object identityToken)
        {
            lock (synchronization) {
                if (candidate.OriginalReleased
                    || !inFlight.TryGetValue(candidate.ClientSequence, out var current)
                    || !ReferenceEquals(current, candidate)
                    || !IsSingleAcceptedAcknowledgement(acknowledgement, candidate.ClientSequence)) {
                    return;
                }

                candidate.OriginalAcknowledgement = acknowledgement.Clone();
                candidate.ReusableSnapshot = snapshot;
                candidate.SnapshotIdentityToken = identityToken;
            }
        }

        public void ReleaseOriginal(ClockedCommandSnapshotReuseCandidate candidate)
        {
            lock (synchronization) {
                if (!candidate.OriginalReleased) {
                    candidate.OriginalReleased = true;
                    if (inFlight.TryGetValue(candidate.ClientSequence, out var current)
                        && ReferenceEquals(current, candidate)) {
                        inFlight.Remove(candidate.ClientSequence);
                    }
                    ReleaseReference(candidate);
                }
            }
        }

        public void ReleaseLinked(ClockedCommandSnapshotReuseCandidate candidate)
        {
            lock (synchronization) {
                if (candidate.LinkedReferences > 0) {
                    candidate.LinkedReferences--;
                    ReleaseReference(candidate);
                }
            }
        }

        private void ReleaseReference(ClockedCommandSnapshotReuseCandidate candidate)
        {
            candidate.ReferenceCount--;
            if (candidate.ReferenceCount == 0) {
                candidate.OriginalAcknowledgement = null;
                candidate.ReusableSnapshot = null;
                candidate.SnapshotIdentityToken = null;
            }
        }

        private bool IsMatchingAcceptedDuplicate(
            CommandAck? original,
            CommandAck duplicate,
            ulong clientSequence)
        {
            if (!IsSingleAcceptedAcknowledgement(original, clientSequence)
                || duplicate.Results.Count != 1) {
                return false;
            }

            var originalResult = original!.Results[0];
            var duplicateResult = duplicate.Results[0];
            return duplicateResult.ClientSequence == clientSequence
                && duplicateResult.Status == CommandStatus.Duplicate
                && duplicateResult.ServerReceiptSequence == originalResult.ServerReceiptSequence
                && duplicateResult.AppliedTick == originalResult.AppliedTick;
        }

        private static bool IsSingleAcceptedAcknowledgement(CommandAck? acknowledgement, ulong clientSequence)
            => acknowledgement is { Results.Count: 1 }
                && acknowledgement.Results[0].ClientSequence == clientSequence
                && acknowledgement.Results[0].Status == CommandStatus.Accepted;
    }

    private sealed class ClockedCommandSnapshotReuseCandidate(
        ClockedCommandSnapshotReuseRegistry registry,
        string sessionId,
        uint entityId,
        string contentManifestHash,
        ulong clientSequence,
        string rulesetIdentityHash,
        object? generationToken,
        byte[] batchBytes)
    {
        public string SessionId { get; } = sessionId;
        public uint EntityId { get; } = entityId;
        public string ContentManifestHash { get; } = contentManifestHash;
        public ulong ClientSequence { get; } = clientSequence;
        public string RulesetIdentityHash { get; } = rulesetIdentityHash;
        public object? GenerationToken { get; } = generationToken;
        private byte[] BatchBytes { get; } = batchBytes;
        public int ReferenceCount { get; set; } = 1;
        public int LinkedReferences { get; set; }
        public bool OriginalReleased { get; set; }
        public CommandAck? OriginalAcknowledgement { get; set; }
        public Snapshot? ReusableSnapshot { get; set; }
        public object? SnapshotIdentityToken { get; set; }

        public bool TryAddLinkedReference(
            string requestedSessionId,
            uint requestedEntityId,
            string requestedContentManifestHash,
            string requestedRulesetIdentityHash,
            object? requestedGenerationToken,
            byte[] requestedBatchBytes)
        {
            if (OriginalReleased
                || !StringComparer.Ordinal.Equals(SessionId, requestedSessionId)
                || EntityId != requestedEntityId
                || !StringComparer.Ordinal.Equals(ContentManifestHash, requestedContentManifestHash)
                || !StringComparer.Ordinal.Equals(RulesetIdentityHash, requestedRulesetIdentityHash)
                || !ReferenceEquals(GenerationToken, requestedGenerationToken)
                || !BatchBytes.AsSpan().SequenceEqual(requestedBatchBytes)) {
                return false;
            }

            ReferenceCount = checked(ReferenceCount + 1);
            LinkedReferences++;
            return true;
        }

        public void ReleaseOriginal()
            => registry.ReleaseOriginal(this);

        public void ReleaseLinked()
            => registry.ReleaseLinked(this);

        public void PublishReusableSnapshot(CommandAck acknowledgement, Snapshot snapshot, object identityToken)
            => registry.PublishReusableSnapshot(this, acknowledgement, snapshot, identityToken);
    }

    private readonly record struct ClockedCommandSnapshotReuseRegistration(
        ClockedCommandSnapshotReuseCandidate? OriginalCandidate,
        ClockedCommandSnapshotReuseCandidate? LinkedCandidate)
    {
        public void Release()
        {
            OriginalCandidate?.ReleaseOriginal();
            LinkedCandidate?.ReleaseLinked();
        }
    }
}
