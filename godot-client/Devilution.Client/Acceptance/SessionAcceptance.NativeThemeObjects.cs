using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private sealed record NativeThemeObjectCheckpoint(uint Level, uint Entity, uint Type, uint Frame, int X, int Y,
        string PlayerBeforeEffect, string PlayerAfterEffect, string[] Drops);
    private NativeThemeObjectCheckpoint? nativeThemeObject;
    private bool nativeThemeObjectReuseVerified;
    private static bool NativeWarriorRequiresThemeObject
        => System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_REQUIRE_THEME_OBJECT") == "1";
    private readonly AcceptanceStageBudget nativeThemeObjectBudget = new("Native theme object and effect", 80);
    private readonly AcceptanceStageBudget nativeThemeObjectResumeBudget = new("Resumed native fountain reuse", 80);
    private readonly List<string> nativeThemeObjectApproaches = [];

    /** Opt-in only: two nearby approaches per floor, with the same planner used by the seed measurement. */
    private async Task TryNativeThemeObjectAsync(Main main)
    {
        if (!NativeWarriorRequiresThemeObject || nativeThemeObject is not null
            || main.ObservedSnapshot!.Players.Single().LevelId < 5) return;
        using var timing = NativeWarriorStep("objects", "native theme object");
        using var stage = NativeObjectStage(nativeThemeObjectBudget);
        var tried = new HashSet<uint>();
        for (var attempt = 0; attempt < 2; attempt++) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            var geometry = DepthGeometryFromSnapshot(snapshot, player.LevelId);
            var candidate = snapshot.Objects.Where(value => value.LevelId == player.LevelId
                    && !tried.Contains(value.EntityId) && NativeThemeObjectJourneyEvidence.CanDemonstrate(value, player))
                .Select(value => (Object: value, Path: NativeThemeObjectJourneyEvidence.FindApproach(snapshot, geometry, value)))
                .Where(value => value.Path is { Count: <= 24 })
                .OrderByDescending(value => NativeThemeObjectJourneyEvidence.IsLoot(value.Object.NativeObjectId))
                .ThenBy(value => value.Path!.Count).ThenBy(value => value.Object.EntityId).FirstOrDefault();
            if (candidate.Object is not { } target) return;
            tried.Add(target.EntityId);
            await UseNativeHealingPotionIfNeeded(main);
            try {
                await MoveAdjacentToNativePosition(main, current => current.Objects.Single(value => value.EntityId == target.EntityId),
                    NativeObjectObstruction(main, target));
            } catch (NativeObjectApproachUnavailableException exception) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                && alive.LevelId == target.LevelId) {
                nativeThemeObjectApproaches.Add($"L{target.LevelId} theme object {target.EntityId}: {exception.Message}");
                await SettleInterruptedNativeDoorApproachAsync(main);
                continue;
            }
            // As for chests and shrines, reaching operation range completes recovery.
            // The shared obstruction handler fights only when the approach needs it.
            await PrepareNativeObjectAsync(main, "Before operating a native theme object");
            var beforeSnapshot = main.ObservedSnapshot!;
            target = beforeSnapshot.Objects.Single(value => value.EntityId == target.EntityId);
            var before = beforeSnapshot.Players.Single();
            // A fountain's observable effect may have changed during the approach or combat recovery.
            if (!NativeThemeObjectJourneyEvidence.CanDemonstrate(target, before)) continue;
            var oldItems = beforeSnapshot.WorldItems.Select(value => (value.ItemSeed, value.State.StableItemId)).ToHashSet();
            var sequence = main.ObservedLastQueuedSequence;
            GetViewport().GuiReleaseFocus();
            await ClickWorld(main, target.PositionX, target.PositionY);
            await AcceptedObjectOperationAndSettled(main, sequence, target.EntityId, "Operate a native theme room object");
            var afterSnapshot = main.ObservedSnapshot!;
            var after = afterSnapshot.Players.Single();
            var used = afterSnapshot.Objects.Single(value => value.EntityId == target.EntityId);
            NativeThemeObjectJourneyEvidence.VerifyRetained(used, target.LevelId, target.NativeObjectId,
                NativeThemeObjectJourneyEvidence.UsedFrame(target));
            var drops = afterSnapshot.WorldItems.Where(value => value.LevelId == target.LevelId
                    && !oldItems.Contains((value.ItemSeed, value.State.StableItemId))).ToArray();
            if (NativeThemeObjectJourneyEvidence.IsLoot(target.NativeObjectId)) {
                Require(drops.Length > 0, "The native theme room object produced no loot on the floor.");
                foreach (var drop in drops) NativeThemeObjectJourneyEvidence.VerifyDrop(drop, target.LevelId);
            } else {
                Require(NativeThemeObjectJourneyEvidence.EffectApplied(target.NativeObjectId, before, after),
                    "The native theme fountain did not apply its expected resource, status or attribute effect.");
            }
            nativeThemeObject = new(target.LevelId, target.EntityId, target.NativeObjectId, used.AnimationFrame,
                target.PositionX, target.PositionY, Convert.ToBase64String(before.ToByteArray()),
                Convert.ToBase64String(after.ToByteArray()), drops.Select(value => Convert.ToBase64String(value.ToByteArray())).ToArray());
            VerifyNativeThemeObjectRetention(afterSnapshot, nativeThemeObject);
            await ObserveNativeWarriorOperatedObjectDrawAsync(main, used);
            GD.Print($"Native theme object {target.NativeObjectId} on L{target.LevelId} at ({target.PositionX},{target.PositionY}) operated; drops={drops.Length}");
            return;
        }
    }

    private static void VerifyNativeThemeObjectRetention(Snapshot snapshot, NativeThemeObjectCheckpoint expected)
    {
        if (LevelScopedSnapshotEvidence.IncludesLevel(snapshot, expected.Level)) {
            var target = snapshot.Objects.SingleOrDefault(value => value.EntityId == expected.Entity);
            NativeThemeObjectJourneyEvidence.VerifyRetained(target, expected.Level, expected.Type, expected.Frame);
            Require(target!.PositionX == expected.X && target.PositionY == expected.Y,
                "The native theme object's retained position changed.");
        }
        // Drops may later be picked up or spent; their recorded birth identity proves the floor loot and native item level.
        if (NativeThemeObjectJourneyEvidence.IsLoot(expected.Type)) {
            Require(expected.Drops.Length > 0, "The theme object checkpoint omitted its native floor loot evidence.");
            foreach (var payload in expected.Drops)
                NativeThemeObjectJourneyEvidence.VerifyDrop(WorldItemSnapshot.Parser.ParseFrom(Convert.FromBase64String(payload)), expected.Level);
        } else {
            Require(NativeThemeObjectJourneyEvidence.EffectApplied(expected.Type,
                    PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(expected.PlayerBeforeEffect)),
                    PlayerSnapshot.Parser.ParseFrom(Convert.FromBase64String(expected.PlayerAfterEffect))),
                "The theme object checkpoint omitted its native fountain effect evidence.");
        }
    }

    private async Task VerifyNativeThemeObjectReuseAsync(Main main, NativeThemeObjectCheckpoint? expected)
    {
        if (expected is null || !NativeThemeObjectJourneyEvidence.IsReusable(expected.Type)
            || nativeThemeObjectReuseVerified || main.ObservedSnapshot!.Players.Single().LevelId != expected.Level) return;
        using var timing = NativeWarriorStep("objects", "resumed fountain reuse");
        using var stage = NativeObjectStage(nativeThemeObjectResumeBudget);
        var target = main.ObservedSnapshot.Objects.Single(value => value.EntityId == expected.Entity);
        // The first run opened any required route doors; preserve the checkpoint's door state on resume.
        await MoveAdjacentToNativePosition(main, current => current.Objects.Single(value => value.EntityId == expected.Entity),
            NativeObjectObstruction(main, target, allowClosedDoors: false));
        await PrepareNativeObjectAsync(main, "Before reusing a native fountain");
        var before = main.ObservedSnapshot!.Players.Single();
        var sequence = main.ObservedLastQueuedSequence;
        GetViewport().GuiReleaseFocus();
        await ClickWorld(main, expected.X, expected.Y);
        await AcceptedObjectOperationAndSettled(main, sequence, expected.Entity, "Reuse a native fountain on the fresh host");
        Require(NativeThemeObjectJourneyEvidence.EffectApplied(expected.Type, before, main.ObservedSnapshot!.Players.Single()),
            "The fresh host did not retain the native fountain's reusable effect.");
        VerifyNativeThemeObjectRetention(main.ObservedSnapshot, expected);
        nativeThemeObjectReuseVerified = true;
    }
}
