using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;
using System.Text.Json;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private static void VerifyCombatLootRestored(
        Main main,
        string directory,
        string prefix = "journey",
        bool allowEquippedLoot = false)
    {
        using var evidence = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, prefix + "-combat-loot.json")));
        var fields = evidence.RootElement;
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        Require(fields.GetProperty("Passed").GetBoolean(), "Previous gameplay acceptance did not pass.");
        VerifyRemainingBelt(player, fields);
        Require(player.Experience == fields.GetProperty("Experience").GetUInt32(),
            "Fresh-host resume lost combat experience.");
        var expectedLootCount = fields.GetProperty("LootInventoryCount").GetInt32();
        var itemSeed = fields.GetProperty("ItemSeed").GetUInt32();
        var inventoryLootCount = player.Inventory.Count(item => item.ItemSeed == itemSeed);
        var equipmentLootCount = player.Equipment.Count(item => item.ItemSeed == itemSeed);
        Require(expectedLootCount == 1
            && (allowEquippedLoot
                ? inventoryLootCount + equipmentLootCount == expectedLootCount
                : inventoryLootCount == expectedLootCount),
            "Fresh-host resume lost or duplicated the looted inventory/equipment item.");
        Require(!snapshot.Monsters.Any(monster => monster.EntityId == fields.GetProperty("MonsterId").GetUInt32() && monster.Alive),
            "Fresh-host resume revived the defeated encounter.");
        Require(!snapshot.WorldItems.Any(item => item.EntityId == fields.GetProperty("DropEntityId").GetUInt32()),
            "Fresh-host resume duplicated the picked-up loot.");
    }

    private async Task VerifyCombatAndLoot(Main main, string directory, bool visible, bool generated = false,
        bool distanceBoundedPickupDeadline = false)
    {
        var prefix = generated ? "cathedral" : "journey";
        await Until(() => Settled(main), "Dungeon entry did not settle.");
        var initialPlayer = main.ObservedSnapshot!.Players.Single();
        Require(initialPlayer.Belt.Count == 2 && initialPlayer.Belt.All(item => item.State.BaseName == "Potion of Healing"),
            "Combat acceptance requires the unmodified two-potion Rogue loadout.");
        var monsterId = main.ObservedSnapshot.Monsters.Single(monster => monster.Alive
            && monster.LevelId == initialPlayer.LevelId).EntityId;
        var attacks = 0;
        var healingPotionsUsed = 0;
        var trace = new List<object>();
        ulong? lastTick = null;
        try {
            while (main.ObservedSnapshot!.Monsters.Any(monster => monster.EntityId == monsterId && monster.Alive)) {
                var current = main.ObservedSnapshot.Players.Single();
                if (current.Life <= current.LifeMaximum / 2 && current.Belt.Any(item => item.State.BaseName == "Potion of Healing")) {
                    var healingSequence = main.ObservedCommandResult!.ClientSequence;
                    var healingSlot = current.Belt.OrderBy(item => item.Slot).ToList()
                        .FindIndex(item => item.State.BaseName == "Potion of Healing");
                    var sections = (TabContainer)main.FindChild("HudSections", true, false);
                    await ClickListItem(sections, 1, (ItemList)main.FindChild("BeltItems", true, false), healingSlot);
                    await ClickControl(main, sections, 1, "UseConsumable");
                    await AcceptedAndSettled(main, healingSequence, "Use starting healing potion");
                    await Until(() => main.ObservedSnapshot!.Players.Single().Belt.Count == current.Belt.Count - 1,
                        "The starting healing potion was not consumed.", 5);
                    healingPotionsUsed++;
                }
                Require(++attacks <= 30 && main.ObservedSnapshot.Players.Single().Life > 0,
                    "The authored encounter was not defeated through normal combat.");
                var monster = main.ObservedSnapshot.Monsters.Single(candidate => candidate.EntityId == monsterId);
                var previousSequence = main.ObservedCommandResult!.ClientSequence;
                await ClickWorld(main, monster.PositionX, monster.PositionY, MouseButton.Right);
                await AcceptedAndSettled(main, previousSequence, "Attack", snapshot => {
                    if (lastTick == snapshot.Tick)
                        return;
                    lastTick = snapshot.Tick;
                    var player = snapshot.Players.Single();
                    var target = snapshot.Monsters.SingleOrDefault(candidate => candidate.EntityId == monsterId);
                    trace.Add(new { snapshot.Tick, Attacks = attacks, player.Life,
                        player.PositionX, player.PositionY, Sequence = main.ObservedCommandResult?.ClientSequence,
                        Mode = player.CombatMode.ToString(), Action = player.CurrentAction?.ToString(),
                        Target = target?.ToString(), Projectiles = snapshot.Projectiles.Select(item => item.ToString()).ToArray() });
                });
                // A ranged action can finish one tick before its arrow lands.
                // Observe that outcome before submitting another attack against
                // a monster which the in-flight arrow is about to defeat.
                await Until(() => !main.ObservedSnapshot!.Monsters.Any(target => target.EntityId == monsterId && target.Alive)
                    || !main.ObservedSnapshot.Projectiles.Any(projectile => projectile.SourceEntityId == current.EntityId
                        && projectile.TargetEntityId == monsterId), "The acknowledged attack projectiles did not settle.", 5);
            }
        } finally {
            WriteEvidence(directory, prefix + "-combat-trace.json", trace);
        }
        Require(main.ObservedSnapshot!.Players.Single().Experience > initialPlayer.Experience,
            "The defeated monster did not grant authoritative experience.");
        await Until(() => main.ObservedSnapshot!.WorldItems.Any(item => item.EntityId == 1000009),
            "The defeated monster did not drop its authored loot.");
        var drop = main.ObservedSnapshot!.WorldItems.Single(item => item.EntityId == 1000009).Clone();
        Require(drop.ItemSeed == 6001, "The encounter did not produce its authored drop.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, prefix + "-combat.png"));

        // Move away before clicking loot to verify the real multi-step approach.
        var visited = new HashSet<(int X, int Y)> { (drop.PositionX, drop.PositionY) };
        for (var step = 0; step < 3 || (generated && Math.Max(
                 Math.Abs(main.ObservedSnapshot!.Players.Single().PositionX - drop.PositionX),
                 Math.Abs(main.ObservedSnapshot.Players.Single().PositionY - drop.PositionY)) < 4); step++) {
            Require(step < 8, "The generated loot approach did not establish a visible four-cell distance.");
            var previous = main.ObservedSnapshot!.Players.Single();
            var previousSequence = main.ObservedCommandResult!.ClientSequence;
            var offset = generated ? SelectCathedralLootStep(main, drop, visited, keepLootVisible: true) : (X: 0, Y: 1);
            if (generated)
                await Click(main.ObservedTileScreenPosition(previous.PositionX + offset.X, previous.PositionY + offset.Y), walk: true);
            else
                await PressDown();
            await AcceptedAndSettled(main, previousSequence, "Walk away from loot");
            Require(main.ObservedSnapshot!.Players.Single().PositionX == previous.PositionX + offset.X
                && main.ObservedSnapshot.Players.Single().PositionY == previous.PositionY + offset.Y,
                "Walking away from the loot did not advance one tile.");
            visited.Add((previous.PositionX + offset.X, previous.PositionY + offset.Y));
        }
        var pickupSequence = main.ObservedCommandResult!.ClientSequence;
        Require(!distanceBoundedPickupDeadline || generated,
            "A distance-bounded pickup deadline is reserved for generated Cathedral routes.");
        var pickupPlayer = main.ObservedSnapshot!.Players.Single();
        var pickupDistance = Math.Max(Math.Abs((long)pickupPlayer.PositionX - drop.PositionX),
            Math.Abs((long)pickupPlayer.PositionY - drop.PositionY));
        // A clock-gated ordinary tile can take about 49 ticks (~2.45s); allow five seconds per
        // distance cell plus one interaction window, with a finite two-minute ceiling.
        var pickupStageSeconds = distanceBoundedPickupDeadline
            ? (int)Math.Clamp((pickupDistance + 1) * 5, 10L, 120L)
            : 10;
        var pickupTrace = new List<object>();
        ulong? pickupTick = null;
        try {
            await ClickWorld(main, drop.PositionX, drop.PositionY);
            await Until(() => {
                var snapshot = main.ObservedSnapshot!;
                var player = snapshot.Players.Single();
                if (pickupTick != snapshot.Tick && pickupTrace.Count < 256) {
                    pickupTick = snapshot.Tick;
                    pickupTrace.Add(new { snapshot.Tick, player.PositionX, player.PositionY,
                        Mode = player.CombatMode.ToString(), Pending = main.ObservedPendingCommandCount,
                        Sequence = main.ObservedCommandResult?.ClientSequence,
                        Status = main.ObservedCommandResult?.Status.ToString(), Rejection = main.ObservedCommandResult?.RejectReason.ToString(),
                        DropPresent = snapshot.WorldItems.Any(item => item.EntityId == drop.EntityId),
                        LootVisible = AuthoritativeWorldItemPresentationVisibility.IsVisible(drop, player.LevelId,
                            snapshot.LevelGeometry.Single(level => level.LevelId == player.LevelId), player.PositionX, player.PositionY,
                            player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10) });
                }
                return snapshot.WorldItems.All(item => item.EntityId != drop.EntityId)
                    && player.Inventory.Any(item => item.ItemSeed == drop.ItemSeed);
            }, "Clicking distant loot did not complete the server-owned approach and pickup.", stageSeconds: pickupStageSeconds);
        } finally { WriteEvidence(directory, prefix + "-pickup-trace.json", pickupTrace); }
        Require(main.ObservedCommandResult is { } result && IsAcceptedOrSuccessfulDuplicate(result)
            && result.ClientSequence >= pickupSequence + 4,
            "Distant pickup did not acknowledge the approach steps and interaction.");
        var lootInventoryCount = main.ObservedSnapshot!.Players.Single().Inventory.Count(item => item.ItemSeed == drop.ItemSeed);
        Require(lootInventoryCount == 1, "Pickup lost or duplicated the authored inventory item.");
        var remainingBelt = BeltIdentities(main.ObservedSnapshot.Players.Single());
        Require(remainingBelt.Length == 2 - healingPotionsUsed, "Combat granted or lost an unexpected belt item.");
        await SelectTab((TabContainer)main.FindChild("HudSections", true, false), 1);
        var inventory = (GridContainer)main.FindChild("InventoryGrid", true, false);
        var carrying = main.ObservedSnapshot!.Players.Single();
        var lootIndex = carrying.Inventory.ToList().FindIndex(item => item.ItemSeed == drop.ItemSeed);
        var lootName = ItemPresentationStyle.ResolveName(carrying.Inventory[lootIndex].State);
        var anchor = InventoryLayout.Build(carrying.Inventory, carrying.InventoryGrid).Anchors[lootIndex];
        var lootCell = inventory.GetChild<Button>(anchor);
        Require(inventory.IsVisibleInTree() && !string.IsNullOrEmpty(lootName)
            && lootCell.Text != "·" && lootCell.TooltipText.Contains(lootName),
            $"The looted sword '{lootName}' did not appear in its actual inventory control: {lootCell.TooltipText}.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, prefix + "-loot.png"));
        WriteEvidence(directory, prefix + "-combat-loot.json", new {
            Passed = true, MonsterId = monsterId, Attacks = attacks, HealingPotionsUsed = healingPotionsUsed, DropEntityId = drop.EntityId,
            RemainingBelt = remainingBelt,
            drop.ItemSeed, Experience = main.ObservedSnapshot!.Players.Single().Experience,
            LootInventoryCount = lootInventoryCount,
            ApproachAndPickupCommands = main.ObservedCommandResult!.ClientSequence - pickupSequence,
            Sequence = main.ObservedCommandResult!.ClientSequence,
        });
    }

    private static BeltIdentity[] BeltIdentities(PlayerSnapshot player)
        => player.Belt.OrderBy(item => item.Slot)
            .Select(item => new BeltIdentity(item.Slot, item.ItemSeed, item.State.StableItemId)).ToArray();

    private static void VerifyRemainingBelt(PlayerSnapshot player, JsonElement evidence)
    {
        var consumed = evidence.GetProperty("HealingPotionsUsed").GetInt32();
        var expected = evidence.GetProperty("RemainingBelt").Deserialize<BeltIdentity[]>()
            ?? throw new InvalidDataException("Post-combat belt evidence is empty.");
        Require(consumed is >= 0 and <= 2 && expected.Length == 2 - consumed,
            "The original two-potion loadout was not conserved.");
        Require(BeltIdentities(player).SequenceEqual(expected),
            "Fresh-host resume lost or restored a consumed belt item.");
    }

    private sealed record BeltIdentity(uint Slot, uint Seed, uint StableId);

    private async Task ClickWorld(Main main, int x, int y, MouseButton button = MouseButton.Left)
    {
        var point = main.ObservedTileScreenPosition(x, y);
        var area = (Control)main.FindChild("WorldArea", true, false);
        Require(area.GetGlobalRect().HasPoint(point), "The observed target is outside the rendered world area.");
        // Aim and press in one normal input dispatch. Waiting a scene frame
        // between them can move the monster or camera off the observed tile.
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = button, Position = point, GlobalPosition = point, Pressed = true,
        });
        Input.FlushBufferedEvents();
        await Frame();
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = button, Position = point, GlobalPosition = point, Pressed = false,
        });
        await Frame();
    }

    private static bool Settled(Main main)
        => main.ObservedSnapshot?.Players.Single() is { } player
            && AuthoritativePlayerSettlement.IsSettled(player, main.ObservedPendingCommandCount);

    private async Task AcceptedAndSettled(Main main, ulong previousSequence, string action,
        Action<Snapshot>? observe = null)
    {
        await Until(() => {
            var snapshot = main.ObservedSnapshot!;
            observe?.Invoke(snapshot);
            Require(snapshot.Players.Single().Life > 0, $"{action}: the character died at tick {snapshot.Tick}.");
            var result = main.ObservedCommandResult;
            if (result is null || result.ClientSequence <= previousSequence)
                return false;
            Require(IsAcceptedOrSuccessfulDuplicate(result), $"{action}: {result.Status}/{result.RejectReason}.");
            return main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main);
        }, $"{action} did not finish.", stageSeconds: 10);
    }
}
