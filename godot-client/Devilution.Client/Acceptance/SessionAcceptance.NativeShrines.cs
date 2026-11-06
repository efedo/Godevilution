using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private sealed record NativeShrineCheckpoint(uint Level, uint Entity, uint Type, int MessageId,
        string Message, string PlayerAfterEffect);
    private NativeShrineCheckpoint? nativeShrine;
    private static bool NativeWarriorRequiresShrine => System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_REQUIRE_SHRINE") == "1";
    private static bool RequiresSeededMysterious => NativeWarriorRequiresShrine
        && System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_SEED") == "98";
    private readonly AcceptanceStageBudget nativeShrineBudget = new("Native shrine and lasting effect", 80);
    private readonly List<string> nativeShrineApproaches = [];

    /** Real world clicks and cardinal movement, with one shared deadline and at most two nearby approaches per floor. */
    private async Task TryNativeShrineAsync(Main main)
    {
        if (nativeShrine is not null) return;
        if (RequiresSeededMysterious) {
            var level = main.ObservedSnapshot!.Players.Single().LevelId;
            if (level < 5) return;
            Require(level == 5, "The seed-98 run passed L5 without operating its Mysterious shrine at (81,65).");
        }
        using var timing = NativeWarriorStep("objects", "native shrine");
        using var stage = NativeObjectStage(nativeShrineBudget);
        var tried = new HashSet<uint>();
        for (var attempt = 0; attempt < 2; attempt++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var geometry = DepthGeometryFromSnapshot(snapshot, player.LevelId);
            var candidate = snapshot.Objects.Where(value => value.LevelId == player.LevelId
                    && !tried.Contains(value.EntityId) && NativeShrineJourneyEvidence.CanDemonstrate(value, player)
                    && (!RequiresSeededMysterious || NativeShrineJourneyEvidence.IsSeededMysterious(value)))
                .Select(value => (Object: value, Path: NativeObjectApproachPath.FindForShrine(snapshot, geometry, value, NativeWarriorRequiresShrine)))
                .Where(value => value.Path is { Count: <= 24 })
                .OrderBy(value => value.Path!.Count).ThenBy(value => value.Object.EntityId).FirstOrDefault();
            if (candidate.Object is not { } shrine) {
                Require(!RequiresSeededMysterious, "The seed-98 Mysterious shrine on L5 at (81,65) could not be reached within the shrine budget. "
                    + string.Join("; ", nativeShrineApproaches));
                return;
            }
            tried.Add(shrine.EntityId);
            await UseNativeHealingPotionIfNeeded(main);
            try {
                await MoveAdjacentToNativePosition(main, current => current.Objects.Single(value => value.EntityId == shrine.EntityId),
                    NativeWarriorRequiresShrine ? NativeObjectObstruction(main, shrine) : null);
            } catch (InvalidOperationException exception) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                && alive.LevelId == shrine.LevelId) {
                nativeShrineApproaches.Add($"L{shrine.LevelId} shrine {shrine.EntityId}: {exception.Message}");
                await SettleInterruptedNativeDoorApproachAsync(main);
                continue;
            }
            var before = main.ObservedSnapshot!.Players.Single();
            var sequence = main.ObservedLastQueuedSequence;
            GetViewport().GuiReleaseFocus();
            await ClickWorld(main, shrine.PositionX, shrine.PositionY);
            await AcceptedAndSettled(main, sequence, "Operate a native vanilla shrine");
            await Until(() => main.ObservedRecentEvents.Any(value => value.Dialogue is { } message
                    && message.TargetEntityId == shrine.EntityId && !string.IsNullOrEmpty(message.Text)),
                "The native shrine message did not reach the client.", 10);
            var afterSnapshot = main.ObservedSnapshot!;
            var after = afterSnapshot.Players.Single();
            var used = afterSnapshot.Objects.Single(value => value.EntityId == shrine.EntityId);
            Require(used.Activated && !used.NativeShrineInteractable,
                "The native shrine did not retain its one-use transition.");
            await ObserveNativeWarriorOperatedObjectDrawAsync(main, used);
            Require(NativeShrineJourneyEvidence.Changed(shrine.NativeShrineType, shrine.LevelId, before, after),
                "The selected native shrine did not apply its lasting effect.");
            var dialogue = main.ObservedRecentEvents.Last(value => value.Dialogue?.TargetEntityId == shrine.EntityId).Dialogue;
            nativeShrine = new(shrine.LevelId, shrine.EntityId, shrine.NativeShrineType, dialogue.MessageId,
                dialogue.Text, Convert.ToBase64String(after.ToByteArray()));
            VerifyNativeShrineRetention(afterSnapshot, nativeShrine);
            GD.Print($"Native shrine {shrine.NativeShrineType} on L{shrine.LevelId} used; message={dialogue.Text}");
            return;
        }
    }

    private static void VerifyNativeShrineRetention(Snapshot snapshot, NativeShrineCheckpoint expected)
    {
        var shrine = snapshot.Objects.SingleOrDefault(value => value.EntityId == expected.Entity);
        Require(shrine is not null && shrine.LevelId == expected.Level && shrine.Activated
                && !shrine.NativeShrineInteractable && shrine.NativeShrineType == expected.Type,
            "The used native shrine lost its retained identity or became usable again.");
        if (RequiresSeededMysterious)
            Require(NativeShrineJourneyEvidence.IsSeededMysterious(shrine!), "The seed-98 Mysterious shrine lost its known identity on fresh-host resume.");
        var after = PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(expected.PlayerAfterEffect));
        Require(NativeShrineJourneyEvidence.Retained(expected.Type, expected.Level, after, snapshot.Players.Single()),
            "The native shrine's lasting effect did not survive retention or fresh-host resume.");
    }
}
