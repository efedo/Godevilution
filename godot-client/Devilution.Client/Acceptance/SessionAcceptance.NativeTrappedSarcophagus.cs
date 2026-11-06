using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private NativeTrappedSarcophagusCheckpoint? nativeTrappedSarcophagus;
    private readonly AcceptanceStageBudget nativeTrappedSarcophagusBudget = new("Required trapped sarcophagus", 180);
    private static bool NativeWarriorRequiresTrappedSarcophagus
        => System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_REQUIRE_TRAPPED_SARCOPHAGUS") == "1";

    private async Task TryNativeTrappedSarcophagusAsync(Main main)
    {
        if (!NativeWarriorRequiresTrappedSarcophagus || nativeTrappedSarcophagus is not null
            || main.ObservedSnapshot!.Players.Single().LevelId != NativeDepthResumeEvidence.TrappedSarcophagusLevel) return;
        using var timed = NativeWarriorStep("objects", "Required trapped sarcophagus L4@33,62");
        using var stage = NativeObjectStage(nativeTrappedSarcophagusBudget);
        var target = main.ObservedSnapshot.Objects.SingleOrDefault(value => value.LevelId == NativeDepthResumeEvidence.TrappedSarcophagusLevel
            && value.NativeObjectId == 48 && value.PositionX == NativeDepthResumeEvidence.TrappedSarcophagusX
            && value.PositionY == NativeDepthResumeEvidence.TrappedSarcophagusY)
            ?? throw new InvalidOperationException("The default L4 seed has no required sarcophagus at (33,62).");
        Require(NativeTrappedSarcophagusJourneyEvidence.CanOpen(main.ObservedSnapshot!, target.EntityId),
            "The required sarcophagus must start unopened and interactable with an unspent linked wall trap.");
        await UseNativeHealingPotionIfNeeded(main);
        await MoveAdjacentToNativePosition(main, current => current.Objects.Single(value => value.EntityId == target.EntityId),
            NativeObjectObstruction(main, target, allowClosedDoors: false));
        await Until(() => Settled(main), "The trapped sarcophagus approach did not settle.", 10);
        var before = main.ObservedSnapshot!.Clone();
        target = before.Objects.Single(value => value.EntityId == target.EntityId);
        Require(NativeTrappedSarcophagusJourneyEvidence.CanOpen(before, target.EntityId)
            && AuthoritativeObjectSelector.SelectAt(before, target.PositionX, target.PositionY)?.EntityId == target.EntityId,
            "The trapped sarcophagus lost its unopened baseline, unspent wall source or ordinary selection before clicking.");
        var opening = new NativeTrappedSarcophagusJourneyEvidence.Opening(before, target.EntityId);
        var sequence = main.ObservedLastQueuedSequence;
        GetViewport().GuiReleaseFocus();
        await ClickWorld(main, target.PositionX, target.PositionY);
        await AcceptedAndSettled(main, sequence, "Open the required native trapped sarcophagus");
        var receipt = main.ObservedCommandResult!.Clone();
        var deadline = checked(receipt.AppliedTick + 10UL * (main.ObservedIdentity?.TickRateHz ?? 20));
        await Until(() => opening.Find(main.ObservedSnapshot!, receipt.AppliedTick, deadline) is not null,
            "The opened sarcophagus did not fire its linked wall trap on a newer native object tick.", 10);
        var after = main.ObservedSnapshot!.Clone();
        var frame = await ObserveNativeSarcophagusAnimation(main, target.EntityId, target.AnimationFrame);
        nativeTrappedSarcophagus = NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, target.EntityId, receipt, deadline, frame);
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(main.ObservedSnapshot!, nativeTrappedSarcophagus);
        await ObserveNativeWarriorOperatedObjectDrawAsync(main, main.ObservedSnapshot!.Objects.Single(value => value.EntityId == target.EntityId));
        GD.Print($"L4 sarcophagus ({target.PositionX},{target.PositionY}) opened at {receipt.AppliedTick}; trap {nativeTrappedSarcophagus.TrapSource} fired at {nativeTrappedSarcophagus.FiringTick}; loot={nativeTrappedSarcophagus.Drops.Length}, skeletons={nativeTrappedSarcophagus.Skeletons.Length}.");
    }
}
