using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Google.Protobuf;
using System.Text.Json;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

/**
 * Opt-in Sorcerer lifecycle acceptance. Every stage uses the same controls and
 * server-owned snapshots as a player; the lifecycle preview only makes the
 * authored combat timing deterministic.
 */
public partial class SessionAcceptance
{
    private const uint LifecycleCharacterClassId = 2;
    private const string LifecycleCharacterName = "M1-Sorcerer";
    private const uint LifecycleQuestId = 3007;
    private const int LifecycleQuestTownerId = 2;
    private const int LifecycleQuestX = 24;
    private const int LifecycleQuestY = 32;
    private const uint LifecycleSpellTargetEntityId = 9;
    private const uint LifecycleDeathTargetEntityId = 10;
    private const int LifecycleStaffSpellId = 30;
    private const uint LifecycleMemorizedSpellId = 1;

    private async Task VerifySorcererLifecycleAsync(
        Main main,
        string directory,
        string phase,
        bool visible,
        Evidence? previous)
    {
        if (phase == "Lifecycle") {
            await VerifyLifecycleCreationAsync(main, directory, visible);
            return;
        }

        await VerifyLifecycleResumeAsync(main, directory, visible, previous
            ?? throw new InvalidDataException("Lifecycle resume evidence is missing."));
    }

    private async Task VerifyLifecycleCreationAsync(
        Main main,
        string directory,
        bool visible)
    {
        await Until(() => Settled(main), "Sorcerer creation did not settle.");
        var initial = RequireLifecycleSorcerer(main);
        var initialIdentity = main.ObservedIdentity
            ?? throw new InvalidDataException("Lifecycle creation has no server identity.");
        var staff = RequireLifecycleStaff(initial);
        var staffStableId = staff.State!.StableItemId;
        var initialStaffCharges = staff.State.Charges;
        var initialStaffMaximum = staff.State.MaxCharges;
        Require(initialStaffCharges > 1 && initialStaffMaximum == initialStaffCharges,
            "The Sorcerer starter staff did not arrive with authoritative charges.");
        Require(initial.MemorizedSpells.Any(spell => spell.SpellId == LifecycleMemorizedSpellId
            && spell.Level == 2),
            "The Sorcerer did not receive the authored level-two Firebolt spell.");
        Require(initial.Belt.Count(item => item.State?.BaseName == "Potion of Mana") == 2,
            "The Sorcerer starter belt did not contain exactly two authored mana potions.");

        await SelectLifecycleStaffSourceAsync(main, directory, verifyKeyboard: true);
        var quest = await ActivateLifecycleQuestAsync(main, directory);
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "lifecycle-quest.png"));
        await VerifyRouteJourney(main, directory, visible);

        var combat = await VerifyLifecycleSpellsAsync(
            main,
            initialStaffCharges,
            initialStaffMaximum,
            directory,
            visible);

        var deathTarget = RequireLifecycleMonster(main, LifecycleDeathTargetEntityId);
        LifecycleDeathEvidence? deathEvidence = null;
        for (var step = 0; step < 32; step++) {
            var player = main.ObservedSnapshot!.Players.Single();
            if (Math.Max(Math.Abs(player.PositionX - deathTarget.PositionX),
                    Math.Abs(player.PositionY - deathTarget.PositionY)) <= 3)
                break;
            deathEvidence = await MoveLifecycleTowardAsync(main, deathTarget.PositionX, deathTarget.PositionY,
                directory, allowLethalDeath: true);
            if (deathEvidence is not null)
                break;
        }
        var deathApproach = main.ObservedSnapshot!.Players.Single();
        Require(Math.Max(Math.Abs(deathApproach.PositionX - deathTarget.PositionX),
                Math.Abs(deathApproach.PositionY - deathTarget.PositionY)) <= 3,
            "The real movement controls could not reach the authored lethal encounter.");
        await WaitForLifecycleDeathAsync(main, directory, deathEvidence);
        var dead = main.ObservedSnapshot!.Players.Single();
        var droppedStaff = RequireLifecycleDroppedStaff(main, staffStableId);
        Require(droppedStaff.ItemSeed == staff.ItemSeed
            && droppedStaff.LevelId == dead.LevelId
            && droppedStaff.State.Charges == combat.ChargesAfterRecharge
            && droppedStaff.State.MaxCharges == combat.MaximumAfterRecharge,
            "Native death changed the charged staff's seed, level or recharged state.");

        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await RevealControl(sections, 0, (Button)main.FindChild("RestartTown", true, false));
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "lifecycle-death.png"));
        var restartSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 0, "RestartTown");
        await Until(() => {
            var player = main.ObservedSnapshot!.Players.Single();
            var result = main.ObservedCommandResult;
            return result is not null
                && result.ClientSequence > restartSequence
                && IsAcceptedOrSuccessfulDuplicate(result)
                && player.LevelId == 0
                && player.Life > 0
                && player.Mana == 0
                && player.CombatMode == PlayerCombatMode.Stand
                && player.CurrentAction is null
                && player.ActiveStoreId == 0
                && !player.StashOpen;
        }, "The real RestartTown control did not restore a settled Town player.", 30);
        var restarted = main.ObservedSnapshot!.Players.Single();
        Require(!OwnsLifecycleStaff(restarted, staffStableId),
            "RestartTown restored a death-dropped staff without an authoritative pickup.");
        Require(RequireLifecycleDroppedStaff(main, staffStableId).Equals(droppedStaff),
            "RestartTown changed the exact off-level death drop in the complete world snapshot.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "lifecycle-restart.png"));

        var identity = main.ObservedIdentity!;
        var restartResult = main.ObservedCommandResult!;
        var state = new LifecycleStateEvidence(
            Passed: true,
            ContentHash: initialIdentity.ContentManifestHash,
            RulesetHash: initialIdentity.RulesetIdentityHash,
            EntityId: restarted.EntityId,
            CharacterClassId: restarted.CharacterClassId,
            CharacterName: restarted.CharacterName,
            QuestInitialBefore: quest.InitialBefore,
            QuestInitialAfter: quest.InitialAfter,
            QuestProgressAfter: quest.ProgressAfter,
            StaffStableId: staffStableId,
            DroppedStaffSnapshotJson: JsonFormatter.Default.Format(droppedStaff),
            StaffInitialCharges: initialStaffCharges,
            StaffInitialMaximum: initialStaffMaximum,
            StaffChargesAfterCast: combat.ChargesAfterCast,
            StaffMaximumAfterCast: combat.MaximumAfterCast,
            StaffChargesAfterRecharge: combat.ChargesAfterRecharge,
            StaffMaximumAfterRecharge: combat.MaximumAfterRecharge,
            ManaBeforeCast: combat.ManaBeforeCast,
            ManaAfterCast: combat.ManaAfterCast,
            DeathLevelId: dead.LevelId,
            DeathEntityId: LifecycleDeathTargetEntityId,
            DeathDroppedStaff: true,
            RestartLevelId: restarted.LevelId,
            RestartLife: restarted.Life,
            RestartMana: restarted.Mana,
            RestartSequence: restartResult.ClientSequence);
        WriteEvidence(directory, "lifecycle-state.json", state);
        WriteEvidence(directory, "lifecycle.json", new Evidence(true,
            identity.GameMode.ToString(), identity.ContentManifestHash,
            identity.RulesetIdentityHash, restarted.EntityId,
            restarted.CharacterClassId, restarted.LevelId, restarted.PositionX,
            restarted.PositionY, restartResult.ClientSequence, visible));
        GD.Print($"Godot application acceptance passed: Lifecycle; visible={visible}; sequence={restartResult.ClientSequence}");
    }

    private async Task<LifecycleQuestEvidence> ActivateLifecycleQuestAsync(
        Main main,
        string directory)
    {
        var interaction = main.ObservedSnapshot!.QuestTownerInteractions
            .SingleOrDefault(candidate => candidate.QuestId == LifecycleQuestId
                && candidate.TownerId == LifecycleQuestTownerId
                && candidate.LevelId == 0
                && candidate.PositionX == LifecycleQuestX
                && candidate.PositionY == LifecycleQuestY)
            ?? throw new InvalidDataException(
                "The authored itemless Butcher quest/towner interaction was not projected by the server.");
        var before = main.ObservedSnapshot.Quests.Single(quest => quest.QuestId == LifecycleQuestId);
        Require(before.Initial && !before.Completed && !before.Unavailable,
            "The authored Butcher quest was not in its initial active state.");

        for (var step = 0; step < 256; step++) {
            var player = main.ObservedSnapshot.Players.Single();
            if (AuthoritativeInteractionRange.IsAdjacent(player, LifecycleQuestX, LifecycleQuestY))
                break;
            await MoveLifecycleTowardAsync(main, LifecycleQuestX, LifecycleQuestY, directory);
        }
        var arrived = main.ObservedSnapshot.Players.Single();
        Require(AuthoritativeInteractionRange.IsAdjacent(arrived, LifecycleQuestX, LifecycleQuestY),
            "The real movement controls could not reach the authored Butcher quest/towner cell.");
        var available = AuthoritativeQuestTownerInteractionSelector.Select(main.ObservedSnapshot);
        Require(available?.InteractionId == interaction.InteractionId,
            "The itemless Butcher quest/towner interaction was not enabled at its authored position.");

        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickControl(main, sections, 4, "QuestTowner");
        await Until(() => {
            var result = main.ObservedCommandResult;
            var quest = main.ObservedSnapshot!.Quests.Single(candidate => candidate.QuestId == LifecycleQuestId);
            return result is not null
                && result.ClientSequence > sequence
                && IsAcceptedOrSuccessfulDuplicate(result)
                && Settled(main)
                && !quest.Initial;
        }, "The real QuestTowner control did not activate the itemless Butcher quest.", 15);
        var after = main.ObservedSnapshot!.Quests.Single(quest => quest.QuestId == LifecycleQuestId);
        WriteEvidence(directory, "lifecycle-quest.json", new {
            Passed = true,
            InteractionId = interaction.InteractionId,
            QuestId = interaction.QuestId,
            TownerId = interaction.TownerId,
            PositionX = interaction.PositionX,
            PositionY = interaction.PositionY,
            InitialBefore = before.Initial,
            InitialAfter = after.Initial,
            ProgressAfter = after.Progress,
            Sequence = main.ObservedCommandResult!.ClientSequence,
        });
        return new LifecycleQuestEvidence(before.Initial, after.Initial, after.Progress);
    }

    private async Task<(TabContainer Sections, ItemList Equipment, int EquipmentIndex, ItemList Sources)>
        SelectLifecycleStaffSourceAsync(Main main, string directory, bool verifyKeyboard)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var equipment = (ItemList)main.FindChild("EquipmentItems", true, false);
        await RevealControl(sections, 1, equipment);
        var equipmentIndex = FindListItem(equipment, "Short Staff of Charged Bolt");
        Require(equipmentIndex >= 0, "The charged staff did not reach the actual equipment control.");
        await ClickListItem(sections, 1, equipment, equipmentIndex);
        Require(main.ObservedSelectedItemReference is {
                Location: PlayerItemLocation.Equipment, Slot: 4,
            }, "The actual equipment control did not select the charged staff.");

        var sources = (ItemList)main.FindChild("SpellSources", true, false);
        await RevealControl(sections, 0, sources);
        var chargedIndex = FindListItem(sources, "Charged Bolt", "staff");
        Require(chargedIndex >= 0, "The charged staff source did not reach the actual spell-source control.");
        await ClickListItem(sections, 0, sources, chargedIndex);
        Require(main.ObservedSelectedItemReference is null,
            "Choosing a spell source retained a pending carried-item drop selection.");
        if (verifyKeyboard) {
            var sequence = main.ObservedLastQueuedSequence;
            foreach (var key in new[] { Key.E, Key.Q }) {
                await ClickListItem(sections, 1, equipment, equipmentIndex);
                Require(main.ObservedSelectedItemReference is not null,
                    "Keyboard source selection requires an actual pending item selection.");
                await PressAcceptanceKey(key);
                Require(main.ObservedSelectedItemReference is null
                    && main.ObservedLastQueuedSequence == sequence,
                    $"Cycling spell sources with {key} retained item selection or queued gameplay.");
            }
            await ClickListItem(sections, 1, equipment, equipmentIndex);
            Require(main.ObservedSelectedItemReference is not null,
                "Repeated source selection requires an actual pending item selection.");
            await ClickListItem(sections, 0, sources, chargedIndex);
            Require(main.ObservedSelectedItemReference is null
                && main.ObservedLastQueuedSequence == sequence,
                "Explicitly reselecting the same spell source retained item selection or queued gameplay.");
            WriteEvidence(directory, "lifecycle-selection.json", new {
                Passed = true, Mouse = true, Keyboard = true, SameSourceReselection = true,
                QueuedSequenceBefore = sequence, QueuedSequenceAfter = main.ObservedLastQueuedSequence,
            });
        }
        return (sections, equipment, equipmentIndex, sources);
    }

    private async Task<LifecycleCombatEvidence> VerifyLifecycleSpellsAsync(
        Main main,
        int initialCharges,
        int initialMaximum,
        string directory,
        bool visible)
    {
        var (sections, equipment, equipmentIndex, sources) = await SelectLifecycleStaffSourceAsync(main, directory, verifyKeyboard: false);
        var target = RequireLifecycleMonster(main, LifecycleSpellTargetEntityId);
        var castSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickWorld(main, target.PositionX, target.PositionY);
        await AcceptedAndSettled(main, castSequence, "Cast charged staff spell");
        await Until(() => RequireLifecycleStaff(main.ObservedSnapshot!.Players.Single()).State!.Charges == initialCharges - 1,
            "The charged staff cast did not consume exactly one authoritative charge.", 15);
        var afterCastStaff = RequireLifecycleStaff(main.ObservedSnapshot!.Players.Single()).State!;
        Require(afterCastStaff.Charges >= 0 && afterCastStaff.MaxCharges == initialMaximum,
            "The charged staff cast changed maximum charges before Recharge.");

        var rechargeSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickListItem(sections, 1, equipment, equipmentIndex);
        await ClickControl(main, sections, 2, "RechargeSelected");
        await Until(() => {
            var result = main.ObservedCommandResult;
            var state = FindLifecycleStaff(main.ObservedSnapshot!.Players.Single())?.State;
            return result is not null
                && result.ClientSequence > rechargeSequence
                && IsAcceptedOrSuccessfulDuplicate(result)
                && Settled(main)
                && state is not null
                && afterCastStaff.MaxCharges > 0
                && state.MaxCharges == afterCastStaff.MaxCharges - 1
                && state.Charges == state.MaxCharges;
        }, "The real RechargeSelected ability did not restore authoritative staff charges.", 15);
        var afterRechargeStaff = RequireLifecycleStaff(main.ObservedSnapshot!.Players.Single()).State!;
        Require(afterCastStaff.MaxCharges > 0
            && afterRechargeStaff.MaxCharges == afterCastStaff.MaxCharges - 1
            && afterRechargeStaff.Charges == afterRechargeStaff.MaxCharges,
            "Recharge did not produce the native charge/max transition.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "lifecycle-recharge.png"));

        var manaBefore = main.ObservedSnapshot.Players.Single().Mana;
        var fireboltIndex = FindListItem(sources, "Firebolt", "level 2");
        Require(fireboltIndex >= 0, "The learned Firebolt source did not reach the actual spell-source control.");
        await ClickListItem(sections, 0, sources, fireboltIndex);
        Require(main.ObservedSelectedItemReference is null,
            "Choosing Firebolt after Recharge retained a pending carried-item drop selection.");
        target = RequireLifecycleMonster(main, LifecycleSpellTargetEntityId);
        var fireboltSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await ClickWorld(main, target.PositionX, target.PositionY);
        await AcceptedAndSettled(main, fireboltSequence, "Cast memorized Firebolt");
        await Until(() => main.ObservedSnapshot!.Players.Single().Mana < manaBefore,
            "The memorized Firebolt did not consume authoritative mana.", 15);
        var manaAfter = main.ObservedSnapshot!.Players.Single().Mana;
        Require(manaAfter < manaBefore,
            "The memorized Firebolt cast did not reduce mana.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "lifecycle-firebolt.png"));
        return new LifecycleCombatEvidence(
            afterCastStaff.Charges,
            afterCastStaff.MaxCharges,
            afterRechargeStaff.Charges,
            afterRechargeStaff.MaxCharges,
            manaBefore,
            manaAfter);
    }

    private async Task WaitForLifecycleDeathAsync(Main main, string directory, LifecycleDeathEvidence? evidence)
    {
        await Until(() => {
            evidence ??= ObserveLifecycleDeath(main.ObservedSnapshot!);
            return evidence is not null;
        }, "The native death action did not identify the authored lethal encounter.", 180);
        await Until(() => {
            var player = main.ObservedSnapshot!.Players.Single();
            return player.Life == 0
                && player.CombatMode == PlayerCombatMode.Death
                && player.CurrentAction is null;
        }, "The authored lifecycle encounter did not produce a settled native player death.", 180);
        var dead = main.ObservedSnapshot!.Players.Single();
        Require(dead.Life == 0 && dead.CombatMode == PlayerCombatMode.Death
            && dead.CurrentAction is null,
            "The lifecycle death did not settle at zero life in native Death mode.");
        Require(main.ObservedSnapshot.Monsters.Any(monster =>
            monster.EntityId == LifecycleDeathTargetEntityId),
            "The lifecycle death evidence lost the authored lethal encounter identity.");
        WriteEvidence(directory, "lifecycle-death-action.json", evidence!);
    }

    private static LifecycleDeathEvidence? ObserveLifecycleDeath(Snapshot snapshot)
    {
        var player = snapshot.Players.Single();
        return player is { Life: 0, CombatMode: PlayerCombatMode.Death, CurrentAction: { } action }
            && action.TargetEntityId == LifecycleDeathTargetEntityId
            ? new LifecycleDeathEvidence(true, snapshot.Tick, player.EntityId, player.LevelId,
                player.PositionX, player.PositionY, action.TargetEntityId, JsonFormatter.Default.Format(action))
            : null;
    }

    private async Task VerifyLifecycleResumeAsync(
        Main main,
        string directory,
        bool visible,
        Evidence previous)
    {
        var state = JsonSerializer.Deserialize<LifecycleStateEvidence>(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "lifecycle-state.json")))
            ?? throw new InvalidDataException("Lifecycle state evidence is empty.");
        Require(state.Passed
            && state.CharacterClassId == LifecycleCharacterClassId
            && state.CharacterName == LifecycleCharacterName
            && state.EntityId == previous.EntityId
            && state.ContentHash == previous.ContentHash
            && state.RulesetHash == previous.RulesetHash
            && state.QuestInitialBefore
            && !state.QuestInitialAfter
            && state.DeathLevelId == 1
            && state.DeathEntityId == LifecycleDeathTargetEntityId
            && state.DeathDroppedStaff
            && !string.IsNullOrEmpty(state.DroppedStaffSnapshotJson)
            && state.StaffChargesAfterCast == state.StaffInitialCharges - 1
            && state.StaffMaximumAfterCast > 0
            && state.StaffMaximumAfterRecharge == state.StaffMaximumAfterCast - 1
            && state.StaffChargesAfterRecharge == state.StaffMaximumAfterRecharge
            && state.ManaAfterCast < state.ManaBeforeCast,
            "Lifecycle state evidence does not match the creation identity.");
        var player = RequireLifecycleSorcerer(main);
        Require(player.EntityId == state.EntityId
            && player.LevelId == state.RestartLevelId
            && player.Life == state.RestartLife
            && player.Mana == state.RestartMana
            && player.LevelId == 0
            && player.Life > 0
            && player.Mana == 0
            && !OwnsLifecycleStaff(player, state.StaffStableId),
            "Fresh-host lifecycle restore did not preserve the authoritative death/restart outcome.");
        var quest = main.ObservedSnapshot!.Quests.Single(candidate => candidate.QuestId == LifecycleQuestId);
        Require(quest.Initial == state.QuestInitialAfter
            && quest.Progress == state.QuestProgressAfter,
            "Fresh-host lifecycle restore changed the activated quest state.");
        var expectedDrop = WorldItemSnapshot.Parser.ParseJson(state.DroppedStaffSnapshotJson);
        Require(RequireLifecycleDroppedStaff(main, state.StaffStableId).Equals(expectedDrop),
            "Fresh-host restore changed or lost the exact off-level death-dropped staff.");

        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        await PressDown();
        await Until(() => {
            var result = main.ObservedCommandResult;
            var current = main.ObservedSnapshot!.Players.Single();
            return result is not null
                && result.ClientSequence > sequence
                && result.ClientSequence > previous.Sequence
                && IsAcceptedOrSuccessfulDuplicate(result)
                && current.LevelId == 0
                && current.Life > 0
                && current.PositionX == player.PositionX
                && current.PositionY == player.PositionY + 1
                && current.CombatMode == PlayerCombatMode.Stand
                && current.CurrentAction is null;
        }, "Fresh-host lifecycle resume did not complete a new accepted movement action.", 15);
        var movementSequence = main.ObservedCommandResult!.ClientSequence;
        var restoredDrop = RequireLifecycleDroppedStaff(main, state.StaffStableId);
        Require(restoredDrop.Equals(expectedDrop),
            "Resumed movement changed the exact persisted off-level death drop.");
        WriteEvidence(directory, "lifecycle-restored-drop.json", new {
            Passed = true,
            MovementSequence = movementSequence,
            FinalSequence = main.ObservedCommandResult!.ClientSequence,
            DroppedStaffSnapshotJson = JsonFormatter.Default.Format(restoredDrop),
        });
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "lifecycle-resume.png"));
        var moved = main.ObservedSnapshot!.Players.Single();
        var result = main.ObservedCommandResult!;
        WriteEvidence(directory, "lifecycleresume.json", new Evidence(true,
            main.ObservedIdentity!.GameMode.ToString(),
            main.ObservedIdentity.ContentManifestHash,
            main.ObservedIdentity.RulesetIdentityHash, moved.EntityId,
            moved.CharacterClassId, moved.LevelId, moved.PositionX,
            moved.PositionY, result.ClientSequence, visible));
        GD.Print($"Godot application acceptance passed: LifecycleResume; visible={visible}; sequence={result.ClientSequence}");
    }

    private static PlayerSnapshot RequireLifecycleSorcerer(Main main)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        Require(player.CharacterClassId == LifecycleCharacterClassId
            && player.CharacterName == LifecycleCharacterName
            && player.AbilityKind == PlayerAbilityKind.Recharge,
            "The lifecycle character is not the authored Sorcerer with Recharge.");
        return player;
    }

    private static bool OwnsLifecycleStaff(PlayerSnapshot player, uint stableItemId)
        => player.Equipment.Any(item => item.State?.StableItemId == stableItemId)
            || player.Inventory.Any(item => item.State?.StableItemId == stableItemId)
            || player.Belt.Any(item => item.State?.StableItemId == stableItemId)
            || player.Stash.Any(item => item.Item?.State?.StableItemId == stableItemId)
            || player.HeldItem?.State?.StableItemId == stableItemId;

    private static WorldItemSnapshot RequireLifecycleDroppedStaff(Main main, uint stableItemId)
    {
        var snapshot = main.ObservedSnapshot!;
        var copies = snapshot.WorldItems.Where(item => item.State?.StableItemId == stableItemId).ToArray();
        Require(copies.Length == 1 && !OwnsLifecycleStaff(snapshot.Players.Single(), stableItemId),
            "The native death drop must exist exactly once in the world and nowhere in owned items.");
        return copies.Single().Clone();
    }

    private static EquippedItemSnapshot RequireLifecycleStaff(PlayerSnapshot player)
        => FindLifecycleStaff(player)
            ?? throw new InvalidOperationException("The authoritative charged staff is missing.");

    private static EquippedItemSnapshot? FindLifecycleStaff(PlayerSnapshot player)
        => player.Equipment.SingleOrDefault(item => item.Slot == 4
            && item.State is {
                StableItemId: not 0,
                SpellId: LifecycleStaffSpellId,
                SpellSourceKind: ItemSpellSourceKind.ChargedItem,
            });

    private static MonsterSnapshot RequireLifecycleMonster(Main main, uint entityId)
        => main.ObservedSnapshot!.Monsters.SingleOrDefault(monster =>
            monster.EntityId == entityId && monster.Alive)
            ?? throw new InvalidOperationException(
                $"The authored lifecycle monster {entityId} is not alive in the current snapshot.");

    private async Task<LifecycleDeathEvidence?> MoveLifecycleTowardAsync(Main main, int targetX, int targetY,
        string directory, bool allowLethalDeath = false)
    {
        var player = main.ObservedSnapshot!.Players.Single();
        var dx = targetX - player.PositionX;
        var dy = targetY - player.PositionY;
        Require(dx != 0 || dy != 0, "Lifecycle movement was asked to move from the target cell.");
        var key = Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Key.Right : Key.Left
            : dy > 0 ? Key.Down : Key.Up;
        var sequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        var expectedX = player.PositionX + (key == Key.Right ? 1 : key == Key.Left ? -1 : 0);
        var expectedY = player.PositionY + (key == Key.Down ? 1 : key == Key.Up ? -1 : 0);
        LifecycleDeathEvidence? deathEvidence = null;
        await KeyCommand(main, key, sequence, directory, "lifecycle",
            snapshot => {
                var moved = snapshot.Players.Single();
                if (moved.EntityId != player.EntityId || moved.LevelId != player.LevelId
                    || moved.PositionX != expectedX || moved.PositionY != expectedY)
                    return false;
                // The deliberate lethal approach may die before returning to
                // Stand. Preserve the actual pending action/source immediately.
                if (allowLethalDeath && ObserveLifecycleDeath(snapshot) is { } observed) {
                    deathEvidence = observed;
                    return true;
                }
                return moved is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null };
            },
            $"Same player at level {player.LevelId}, ({expectedX},{expectedY}), settled or observed lethal death.");
        return deathEvidence;
    }

    private static int FindListItem(ItemList list, params string[] requiredText)
    {
        for (var index = 0; index < list.ItemCount; index++) {
            var text = list.GetItemText(index);
            if (requiredText.All(value => text.Contains(value, StringComparison.OrdinalIgnoreCase)))
                return index;
        }
        return -1;
    }

    private sealed record LifecycleQuestEvidence(
        bool InitialBefore,
        bool InitialAfter,
        uint ProgressAfter);

    private sealed record LifecycleDeathEvidence(bool Passed, ulong Tick, uint PlayerEntityId,
        uint LevelId, int X, int Y, uint SourceEntityId, string PendingActionJson);

    private sealed record LifecycleCombatEvidence(
        int ChargesAfterCast,
        int MaximumAfterCast,
        int ChargesAfterRecharge,
        int MaximumAfterRecharge,
        int ManaBeforeCast,
        int ManaAfterCast);

    private sealed record LifecycleStateEvidence(
        bool Passed,
        string ContentHash,
        string RulesetHash,
        uint EntityId,
        uint CharacterClassId,
        string CharacterName,
        bool QuestInitialBefore,
        bool QuestInitialAfter,
        uint QuestProgressAfter,
        uint StaffStableId,
        string DroppedStaffSnapshotJson,
        int StaffInitialCharges,
        int StaffInitialMaximum,
        int StaffChargesAfterCast,
        int StaffMaximumAfterCast,
        int StaffChargesAfterRecharge,
        int StaffMaximumAfterRecharge,
        int ManaBeforeCast,
        int ManaAfterCast,
        uint DeathLevelId,
        uint DeathEntityId,
        bool DeathDroppedStaff,
        uint RestartLevelId,
        int RestartLife,
        int RestartMana,
        ulong RestartSequence);
}
