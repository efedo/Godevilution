using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Devilution.Shared;
using Godot;

namespace Devilution.Client.Acceptance;

/** One acceptance-only health policy; observations never issue commands while a caller validates an earlier receipt. */
public partial class SessionAcceptance
{
    private readonly JourneyCombatSafety journeySafety = new();
    private Main? journeySafetyMain;
    private uint journeySafetyPlayer;
    private ulong journeySafetyRevision = ulong.MaxValue;
    private long journeySafetyGeneration = -1;
    private Snapshot? journeySafetySnapshot;
    private int journeyDefenseDepth;
    private bool journeyDrinking;
    private JourneySafetyActivity journeySafetyActivity = JourneySafetyActivity.Combat;
    private static bool JourneySafetyControlsReleased
        => !Input.IsMouseButtonPressed(MouseButton.Left) && !Input.IsMouseButtonPressed(MouseButton.Right)
            && !Input.IsKeyPressed(Key.W) && !Input.IsKeyPressed(Key.A) && !Input.IsKeyPressed(Key.S) && !Input.IsKeyPressed(Key.D)
            && !Input.IsKeyPressed(Key.Up) && !Input.IsKeyPressed(Key.Down) && !Input.IsKeyPressed(Key.Left) && !Input.IsKeyPressed(Key.Right);

    private void RegisterJourneySafety(Main main, uint playerId)
    {
        if (journeySafetyMain == main && journeySafetyPlayer == playerId) return;
        journeySafetyMain = main; journeySafetyPlayer = playerId;
        journeySafetyRevision = ulong.MaxValue; journeySafetySnapshot = null;
        journeySafety.Reset();
    }

    private Snapshot JourneySafetySnapshot(Main main)
    {
        if (journeySafetyMain != main || journeySafetySnapshot is null
            || journeySafetyRevision != main.ObservedSnapshotRevision || journeySafetyGeneration != main.ObservedModelGeneration) {
            var snapshot = main.ObservedSnapshot ?? throw new InvalidOperationException("Journey safety lost its authoritative snapshot.");
            if (journeySafetyMain != main || journeySafetyPlayer == 0) {
                Require(snapshot.Players.Count == 1, "A multiplayer journey must register its local safety participant.");
                RegisterJourneySafety(main, snapshot.Players[0].EntityId);
            }
            journeySafetySnapshot = snapshot;
            journeySafetyRevision = main.ObservedSnapshotRevision;
            journeySafetyGeneration = main.ObservedModelGeneration;
        }
        return journeySafetySnapshot!;
    }

    private PlayerSnapshot JourneySafetyPlayer(Main main, Snapshot snapshot)
        => snapshot.Players.Single(player => player.EntityId == journeySafetyPlayer
            || journeySafetyPlayer == 0 && snapshot.Players.Count == 1);

    private JourneyCombatSafety.Observation ObserveJourneySafety(Main main)
    {
        var snapshot = JourneySafetySnapshot(main);
        var player = JourneySafetyPlayer(main, snapshot);
        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var nearby = 0; var adjacent = 0;
        if (player.LevelId != 0) foreach (var monster in snapshot.Monsters) {
            if (!monster.Alive || monster.IsPlayerMinion || !LevelMembership.IsOnLevel(monster.LevelId, player.LevelId)) continue;
            var distance = NativeMonsterDistance(player, monster);
            if (distance > 6 || distance > 1 && !AuthoritativeNativeVisionVisibility.IsVisible(player.LevelId,
                    monster.PositionX, monster.PositionY, player.LevelId, geometry, player.PositionX, player.PositionY,
                    player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10)) continue;
            nearby++;
            if (distance <= 1) adjacent++;
        }
        var result = main.ObservedCommandResult;
        var admitted = main.ObservedPendingCommandCount == 0 && (main.ObservedLastQueuedSequence == 0
            || AcceptanceCommandBoundary.HasLatestAppliedReceipt(snapshot.Tick, result, main.ObservedLastQueuedSequence));
        return new(player.EntityId, player.LevelId, snapshot.Tick, player.Life, player.LifeMaximum, nearby, adjacent,
            NativeHealingSupply(player), admitted, AuthoritativePlayerSettlement.IsSettled(player, main.ObservedPendingCommandCount),
            main.ObservedIdentity?.TickRateHz ?? 20, main.ObservedRoundTripMilliseconds ?? 0,
            player.CombatMode == PlayerCombatMode.GotHit, player.CombatModeEndTick);
    }

    private JourneyCombatSafety.Decision JourneySafetyDecision(Main main)
        => journeySafety.Observe(ObserveJourneySafety(main), journeySafetyActivity);

    private void ObserveJourneySafetyFrame()
    {
        if (journeySafetyMain is { TransportConnected: true } main)
            _ = JourneySafetyDecision(main); // Death fails here even inside action, receipt, or stair waits.
    }

    private async Task JourneySafetyCheckpointAsync(Main main, JourneySafetyActivity activity)
    {
        var previous = journeySafetyActivity;
        journeySafetyActivity = activity;
        try { await journeySafety.CheckpointAsync(activity, () => ObserveJourneySafety(main), async () => {
            if (journeyDrinking) return;
            journeyDrinking = true;
            try { await UseNativeHealingPotionIfNeeded(main); }
            finally { journeyDrinking = false; }
        }, journeyDefenseDepth == 0 ? () => DefendJourneyMovementAsync(main) : null); }
        finally { journeySafetyActivity = previous; }
    }

    /** Quick-use is legal in hit recovery and an attack: do not wait for Stand before using the real control. */
    private async Task WaitForJourneySettlementAsync(Main main, string context, double seconds = 20, Action<Snapshot>? observe = null)
    {
        var clock = Stopwatch.StartNew();
        var tick = main.ObservedSnapshot!.Tick;
        await journeySafety.WaitForSettlementAsync(() => {
            var state = ObserveJourneySafety(main);
            observe?.Invoke(JourneySafetySnapshot(main));
            return state;
        }, Frame, async () => {
            if (!journeyDrinking && JourneySafetyControlsReleased)
                await JourneySafetyCheckpointAsync(main, JourneySafetyActivity.Combat);
        }, state => AcceptanceTickBudget.StageExpired(seconds, clock.Elapsed.TotalSeconds,
            state.TickRateHz, tick, state.Tick), context);
    }

    /** Retain the attack receipt before a potion can become the client's latest admitted command. */
    private async Task<CommandResult> WaitForJourneyCombatCommandAsync(Main main, ulong sequence, string context, double seconds = 20)
    {
        return await JourneyCombatSafety.PreserveReceiptAsync(async () => {
            await Until(() => {
                var state = ObserveJourneySafety(main); JourneyCombatSafety.RequireAlive(state);
                return state.CommandsAdmitted && main.ObservedCommandResult is { } result && result.ClientSequence > sequence;
            }, $"{context} did not reach its authoritative receipt.", seconds);
            return main.ObservedCommandResult!;
        }, () => WaitForJourneySettlementAsync(main, context, seconds));
    }

    /** Pause the actual route repeater, heal/fight, then resume the same selected route on the same floor. */
    private async Task GuardJourneyTravelAsync(Main main, uint sourceLevel, bool fleeing = false, string resumeControl = "Travel",
        JourneySafetyActivity activity = JourneySafetyActivity.Travel)
    {
        var state = ObserveJourneySafety(main);
        var decision = journeySafety.Observe(state, fleeing ? JourneySafetyActivity.Retreat : activity);
        if (state.Level != sourceLevel || sourceLevel == 0
            || !decision.Drink && (fleeing || !decision.StopWalking)) return;
        var cancel = (Button)main.FindChild("CancelTravel", true, false);
        await JourneyCombatSafety.ProtectRouteAsync(sourceLevel, () => ObserveJourneySafety(main).Level,
            () => !cancel.Disabled, () => StopNativeTravelAsync(main),
            () => WaitForNativeCommandBoundaryAsync(main, "Journey route safety pause", settlePlayer: false),
            () => JourneySafetyCheckpointAsync(main, fleeing ? JourneySafetyActivity.Retreat : activity),
            () => WaitForJourneySettlementAsync(main, "Journey route safety pause"), async () => {
            var sections = (TabContainer)main.FindChild("HudSections", true, false);
            await ClickControl(main, sections, 4, resumeControl);
            await Until(() => !cancel.Disabled || ObserveJourneySafety(main).Level != sourceLevel,
                "The route did not resume after proactive healing.", 5);
        });
    }

    private async Task DefendJourneyMovementAsync(Main main)
    {
        if (journeyDefenseDepth > 0) return;
        journeyDefenseDepth++;
        try {
            var snapshot = JourneySafetySnapshot(main);
            var player = JourneySafetyPlayer(main, snapshot);
            if (player.LevelId == 0) return;
            if (snapshot.Players.Count == 1 && player.CharacterClassId == 0) {
                await ClearNativeWarriorThreatsAsync(main, player.LevelId);
                return;
            }
            // Ranged and multiplayer journeys use their equipped/readied real attack controls.
            for (var work = 0; work < 16 && JourneySafetyDecision(main).StopWalking; work++) {
                await JourneySafetyCheckpointAsync(main, JourneySafetyActivity.Combat);
                snapshot = JourneySafetySnapshot(main); player = JourneySafetyPlayer(main, snapshot);
                var ranged = player.CharacterClassId != 0;
                var target = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion
                        && LevelMembership.IsOnLevel(monster.LevelId, player.LevelId)
                        && NativeMonsterDistance(player, monster) <= (ranged ? 6 : 1)
                        && IsRenderedWorldTileVisible(main, monster.PositionX, monster.PositionY)
                        && AuthoritativeTargetSelector.SelectCombatTarget(snapshot, player.EntityId,
                            monster.PositionX, monster.PositionY) == monster.EntityId)
                    .OrderBy(monster => NativeMonsterDistance(player, monster)).ThenBy(monster => monster.HitPointsQ6).FirstOrDefault();
                if (target is null) {
                    if (!await TryJourneyRetreatStepAsync(main)) await WaitTicksWhile(main, () => true, .5);
                    continue;
                }
                var sequence = main.ObservedLastQueuedSequence;
                await ClickWorld(main, target.PositionX, target.PositionY,
                    player.CharacterClassId == 2 && main.ObservedSpellSourceArmed ? MouseButton.Left : MouseButton.Right);
                if (main.ObservedLastQueuedSequence > sequence)
                    _ = await WaitForJourneyCombatCommandAsync(main, sequence, "Defend before walking through a group");
            }
        } finally { journeyDefenseDepth--; }
    }

    /** One real, open adjacent step that improves clearance; stairs are never an accidental retreat tile. */
    private async Task<bool> TryJourneyRetreatStepAsync(Main main)
    {
        var state = ObserveJourneySafety(main);
        if (!state.CommandsAdmitted || !state.Settled || state.Level == 0) return false;
        var snapshot = JourneySafetySnapshot(main); var player = JourneySafetyPlayer(main, snapshot);
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == player.LevelId);
        var current = NativeThreatClearance(snapshot, player.LevelId, player.PositionX, player.PositionY);
        (int X, int Y)? best = null;
        var clearance = current;
        foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (1, -1), (-1, -1), (-1, 1) }) {
            var x = player.PositionX + dx; var y = player.PositionY + dy;
            if (!IsNativeApproachCellOpen(main, snapshot, player, geometry, x, y)
                || geometry.WarpTriggers.Any(warp => warp.TriggerX == x && warp.TriggerY == y)
                || dx != 0 && dy != 0 && (!IsNativeApproachCellOpen(main, snapshot, player, geometry, x, player.PositionY)
                    || !IsNativeApproachCellOpen(main, snapshot, player, geometry, player.PositionX, y))) continue;
            var candidate = NativeThreatClearance(snapshot, player.LevelId, x, y);
            if (candidate > clearance && IsRenderedWorldTileVisible(main, x, y)) { best = (x, y); clearance = candidate; }
        }
        if (best is not { } step) return false;
        var before = main.ObservedLastQueuedSequence;
        await Click(main.ObservedTileScreenPosition(step.X, step.Y), walk: true);
        if (main.ObservedLastQueuedSequence == before) return false;
        var result = await WaitForJourneyCombatCommandAsync(main, before, "Take a safer retreat step");
        return AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(result);
    }
}
