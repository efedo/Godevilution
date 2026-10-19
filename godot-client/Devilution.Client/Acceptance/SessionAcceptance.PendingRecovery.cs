using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Real controls with committed-but-unobserved outcomes across host replacement. */
    private async Task VerifyMultiplayerPendingRecoveryAsync(Main main, string directory, string prefix, int slot,
        MultiplayerEvidence previous, AuthoritativeServerIdentity identity, AcceptanceAckLossRelay relay)
    {
        var root = System.IO.Directory.GetParent(directory)!.FullName;
        var peerSlot = 3 - slot;
        var peerDirectory = System.IO.Path.Combine(root, $"player-{peerSlot}");
        var baseline = main.ObservedSnapshot!;
        Require(main.ObservedClockGatedScheduling && baseline.Players.Count == 2,
            "Pending recovery requires a complete clock-gated cohort roster.");
        var localBefore = baseline.Players.Single(player => player.EntityId == previous.EntityId);
        var peerBefore = baseline.Players.Single(player => player.EntityId == previous.PeerEntityId);
        Require(ObservePlayer(localBefore) == previous.Local && ObservePlayer(peerBefore) == previous.Remote,
            "Pending recovery did not start from the exact saved roster.");
        WriteEvidence(directory, prefix + "-prepared.json", new { Prepared = true, Slot = slot });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-prepared.json")),
            "Both clients must observe the restored roster before either pending command.");

        var pendingSequence = previous.Sequence + 1;
        await PressAcceptanceKey(Key.F);
        await Until(() => {
            relay.VerifyHealthy();
            return relay.Withheld is not null;
        }, "The actual friendly-mode control did not receive a committed reply at the relay.");
        var original = relay.Withheld!;
        Require(original.ClientSequence == pendingSequence && original.Status == CommandStatus.Accepted
            && original.RejectReason == CommandRejectReason.Unspecified
            && original.ServerReceiptSequence > previous.ReceiptSequence && original.AppliedTick >= baseline.Tick,
            "The withheld command was not a new accepted original receipt.");
        Require(main.ObservedPendingCommandCount == 1 && main.ObservedLastQueuedSequence == pendingSequence
            && (main.ObservedCommandResult?.ClientSequence ?? 0) < pendingSequence,
            "The withheld receipt reached the client or the command did not remain pending.");
        WriteEvidence(directory, prefix + "-ready.json", new {
            Ready = true, Slot = slot, EntityId = previous.EntityId, PeerEntityId = previous.PeerEntityId,
            SnapshotFingerprint = SnapshotFingerprint(main.ObservedSnapshot!), ClockGated = true,
            PendingSequence = pendingSequence, PendingReceiptSequence = original.ServerReceiptSequence,
            PendingAppliedTick = original.AppliedTick, PendingStatus = original.Status.ToString(),
            PendingRejectReason = original.RejectReason.ToString(), PendingCount = main.ObservedPendingCommandCount,
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(root, prefix + "-server-stopped.json"))
            && !main.TransportConnected, "The owned host did not terminate while the command remained pending.");
        var status = (Label)main.FindChild("ConnectionStatus", true, false);
        await Until(() => status.Text.Contains("Connection lost") && status.Text.Contains("reconnecting"),
            "Pending recovery did not show disconnect/reconnect feedback.");

        var offlineSnapshot = main.ObservedSnapshot!;
        var fingerprint = SnapshotFingerprint(offlineSnapshot);
        var revision = main.ObservedSnapshotRevision;
        var resultFingerprint = CommandFingerprint(main.ObservedCommandResult);
        var localScreen = main.ObservedPlayerScreenPosition!.Value;
        var peerScreen = main.ObservedEntityScreenPosition(previous.PeerEntityId)!.Value;
        var offline = System.Diagnostics.Stopwatch.StartNew();
        while (offline.Elapsed < TimeSpan.FromSeconds(2)) {
            await PressAcceptanceKey(slot == 1 ? Key.Down : Key.Up);
            relay.VerifyHealthy();
            Require(!main.TransportConnected && main.ObservedPendingCommandCount == 1
                && main.ObservedLastQueuedSequence == pendingSequence,
                "Offline input changed the retained pending command or reconnected prematurely.");
            Require(main.ObservedSnapshotRevision == revision && SnapshotFingerprint(main.ObservedSnapshot!) == fingerprint
                && CommandFingerprint(main.ObservedCommandResult) == resultFingerprint,
                "Offline input changed authoritative snapshot or receipt state.");
            Require(main.ObservedPlayerScreenPosition!.Value.DistanceTo(localScreen) < 0.01f
                && main.ObservedEntityScreenPosition(previous.PeerEntityId)!.Value.DistanceTo(peerScreen) < 0.01f,
                "Offline input moved a rendered actor while its command was unresolved.");
            Require(status.Text.Contains("Connection lost"), "Offline disconnect feedback disappeared.");
        }
        WriteEvidence(directory, prefix + "-offline-ready.json", new {
            Passed = true, Slot = slot, OfflineSeconds = offline.Elapsed.TotalSeconds,
            SnapshotFingerprint = fingerprint, SnapshotRevision = revision, PendingCount = 1,
            PendingSequence = pendingSequence,
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(root, prefix + "-server-restarted.json")),
            "The launcher did not replace the host after both pending/offline barriers.");
        await Until(() => {
            relay.VerifyHealthy();
            return main.TransportConnected && main.ObservedPendingCommandCount == 0 && relay.Recovered is not null
                && main.ObservedCommandResult?.ClientSequence == pendingSequence;
        }, "The original pending command did not settle after automatic reconnect.");
        var duplicate = relay.Recovered!;
        var clientResult = main.ObservedCommandResult!;
        Require(duplicate.Status == CommandStatus.Duplicate && IsAcceptedOrSuccessfulDuplicate(clientResult)
            && duplicate.ClientSequence == original.ClientSequence
            && duplicate.ServerReceiptSequence == original.ServerReceiptSequence
            && duplicate.AppliedTick == original.AppliedTick && duplicate.RejectReason == original.RejectReason
            && clientResult.Equals(duplicate) && relay.RecoveredTransmissions >= 1,
            "Reconnect did not return the exact original successful duplicate receipt.");
        Require(main.ObservedIdentity == identity && main.ObservedClockGatedScheduling,
            "Pending reconnect changed content, mode or scheduler identity.");
        var expectedLocal = previous.Local with { Hostile = !previous.Local.Hostile };
        var expectedPeer = previous.Remote with { Hostile = !previous.Remote.Hostile };
        await Until(() => main.ObservedSnapshot is { } snapshot && snapshot.Players.Count == 2
            && snapshot.Players.Select(player => player.EntityId).Distinct().Count() == 2
            && ObservePlayer(snapshot.Players.Single(player => player.EntityId == previous.EntityId)) == expectedLocal
            && ObservePlayer(snapshot.Players.Single(player => player.EntityId == previous.PeerEntityId)) == expectedPeer,
            "Each committed friendly-mode assignment must restore the expected mode for the original roster.");
        var reconnectTick = main.ObservedSnapshot!.Tick;
        Require(reconnectTick >= original.AppliedTick, "Reconnect snapshot predates the committed command.");
        WriteEvidence(directory, prefix + "-rejoined.json", new {
            Rejoined = true, Slot = slot, Tick = reconnectTick,
            EntityId = previous.EntityId, PeerEntityId = previous.PeerEntityId,
            PendingSequence = pendingSequence, PendingReceiptSequence = original.ServerReceiptSequence,
            PendingAppliedTick = original.AppliedTick, RetryTransmissions = relay.RecoveredTransmissions,
            FrozenCommand = true, DuplicateStatus = duplicate.Status.ToString(), PendingCount = 0,
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-rejoined.json")),
            "The peer did not settle its original pending command before new input.");

        await KeyCommand(main, Key.Right, pendingSequence, directory, prefix,
            snapshot => snapshot.Players.Single(player => player.EntityId == previous.EntityId) is {
                    CurrentAction: null, CombatMode: PlayerCombatMode.Stand } moved
                && ObservePlayer(moved) == (expectedLocal with { X = expectedLocal.X + 1 }),
            "The next genuinely new control settles beyond the restored receipt high-water.");
        var result = main.ObservedCommandResult!;
        Require(result.ClientSequence == pendingSequence + 1 && IsAcceptedOrSuccessfulDuplicate(result)
            && result.ServerReceiptSequence > original.ServerReceiptSequence && result.AppliedTick >= reconnectTick,
            "The next new control reused or regressed the restored receipt identity.");
        var movedLocal = ObservePlayer(main.ObservedSnapshot!.Players.Single(player => player.EntityId == previous.EntityId));
        WriteEvidence(directory, prefix + "-moved.json", new MultiplayerMoveEvidence(movedLocal,
            previous.EntityId, result.ServerReceiptSequence, result.AppliedTick));
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-moved.json")),
            "The peer did not settle its next new control.");
        var peerMove = expectedPeer with { X = expectedPeer.X + 1 };
        await Until(() => ObservePlayer(main.ObservedSnapshot!.Players.Single(player => player.EntityId == previous.PeerEntityId)) == peerMove
            && main.ObservedEntityScreenPosition(previous.PeerEntityId) is { } displayed
            && displayed.DistanceTo(main.ObservedTileScreenPosition(peerMove.X, peerMove.Y)) < 1,
            "The peer's committed mode and next settled movement were not rendered here.");
        Require(ObservePlayer(main.ObservedSnapshot!.Players.Single(player => player.EntityId == previous.EntityId)) == movedLocal,
            "Peer recovery changed this owner's settled state.");
        var world = (Control)main.FindChild("WorldArea", true, false);
        Require(world.GetGlobalRect().HasPoint(main.ObservedPlayerScreenPosition!.Value)
            && world.GetGlobalRect().HasPoint(main.ObservedEntityScreenPosition(previous.PeerEntityId)!.Value),
            "Recovered actors were clipped outside the world area.");
        var visible = DisplayServer.GetName() != "headless";
        if (visible)
            await Capture(System.IO.Path.Combine(directory, prefix + "-players.png"));
        WriteEvidence(directory, prefix + ".json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, Slot = slot, EntityId = previous.EntityId,
            PeerEntityId = previous.PeerEntityId, Local = movedLocal, Remote = peerMove,
            Sequence = result.ClientSequence, ReceiptSequence = result.ServerReceiptSequence, AppliedTick = result.AppliedTick,
            PendingSequence = pendingSequence, PendingReceiptSequence = original.ServerReceiptSequence,
            PendingAppliedTick = original.AppliedTick, RetryTransmissions = relay.RecoveredTransmissions,
            FrozenCommand = true, DuplicateStatus = duplicate.Status.ToString(), ReconnectTick = reconnectTick,
            DisconnectedSnapshotFingerprint = fingerprint,
            ClockGated = true, Visible = visible,
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + ".json")),
            "The peer did not complete pending-command recovery visibility assertions.");
        relay.VerifyHealthy();
    }
}
