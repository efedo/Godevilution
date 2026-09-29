using Devilution.Protocol.V1;
using Godot;
using System.Text.Json;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private static void VerifyCombatLootRestored(Main main, string directory)
    {
        using var evidence = JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(directory, "journey-combat-loot.json")));
        var fields = evidence.RootElement;
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single();
        Require(fields.GetProperty("Passed").GetBoolean(), "Previous gameplay acceptance did not pass.");
        Require(player.Experience == fields.GetProperty("Experience").GetUInt32(),
            "Fresh-host resume lost combat experience.");
        var expectedLootCount = fields.GetProperty("LootInventoryCount").GetInt32();
        Require(expectedLootCount == 1
            && player.Inventory.Count(item => item.ItemSeed == fields.GetProperty("ItemSeed").GetUInt32()) == expectedLootCount,
            "Fresh-host resume lost or duplicated the looted inventory item.");
        Require(!snapshot.Monsters.Any(monster => monster.EntityId == fields.GetProperty("MonsterId").GetUInt32() && monster.Alive),
            "Fresh-host resume revived the defeated encounter.");
        Require(!snapshot.WorldItems.Any(item => item.EntityId == fields.GetProperty("DropEntityId").GetUInt32()),
            "Fresh-host resume duplicated the picked-up loot.");
    }

    private async Task VerifyCombatAndLoot(Main main, string directory, bool visible)
    {
        await Until(() => Settled(main), "Dungeon entry did not settle.");
        var initialPlayer = main.ObservedSnapshot!.Players.Single();
        var monsterId = main.ObservedSnapshot.Monsters.Single(monster => monster.Alive
            && monster.LevelId == initialPlayer.LevelId).EntityId;
        var attacks = 0;
        var trace = new List<object>();
        ulong? lastTick = null;
        try {
            while (main.ObservedSnapshot!.Monsters.Any(monster => monster.EntityId == monsterId && monster.Alive)) {
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
                        Mode = player.CombatMode.ToString(), Action = player.CurrentAction?.ToString(),
                        Target = target?.ToString(), Projectiles = snapshot.Projectiles.Select(item => item.ToString()).ToArray() });
                });
            }
        } finally {
            WriteEvidence(directory, "journey-combat-trace.json", trace);
        }
        Require(main.ObservedSnapshot!.Players.Single().Experience > initialPlayer.Experience,
            "The defeated monster did not grant authoritative experience.");
        await Until(() => main.ObservedSnapshot!.WorldItems.Any(item => item.EntityId == 1000009),
            "The defeated monster did not drop its authored loot.");
        var drop = main.ObservedSnapshot!.WorldItems.Single(item => item.EntityId == 1000009).Clone();
        Require(drop.ItemSeed == 6001, "The encounter did not produce its authored drop.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "journey-combat.png"));

        // Move away before clicking loot to verify the real multi-step approach.
        for (var step = 0; step < 3; step++) {
            var previous = main.ObservedSnapshot!.Players.Single();
            var previousSequence = main.ObservedCommandResult!.ClientSequence;
            await PressDown();
            await AcceptedAndSettled(main, previousSequence, "Walk away from loot");
            Require(main.ObservedSnapshot!.Players.Single().PositionY == previous.PositionY + 1,
                "Walking away from the loot did not advance one tile.");
        }
        var pickupSequence = main.ObservedCommandResult!.ClientSequence;
        await ClickWorld(main, drop.PositionX, drop.PositionY);
        await Until(() => main.ObservedSnapshot!.WorldItems.All(item => item.EntityId != drop.EntityId)
            && main.ObservedSnapshot.Players.Single().Inventory.Any(item => item.ItemSeed == drop.ItemSeed),
            "Clicking distant loot did not complete the server-owned approach and pickup.", stageSeconds: 10);
        Require(main.ObservedCommandResult is { Status: CommandStatus.Accepted } result
            && result.ClientSequence >= pickupSequence + 4,
            "Distant pickup did not acknowledge the approach steps and interaction.");
        var lootInventoryCount = main.ObservedSnapshot!.Players.Single().Inventory.Count(item => item.ItemSeed == drop.ItemSeed);
        Require(lootInventoryCount == 1, "Pickup lost or duplicated the authored inventory item.");
        await SelectTab((TabContainer)main.FindChild("HudSections", true, false), 1);
        var inventory = (GridContainer)main.FindChild("InventoryGrid", true, false);
        Require(inventory.IsVisibleInTree() && inventory.GetChildren().OfType<Button>()
            .Any(cell => cell.Text != "·" && cell.TooltipText.Contains("Short Sword")),
            "The looted sword did not appear in the actual inventory controls.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "journey-loot.png"));
        WriteEvidence(directory, "journey-combat-loot.json", new {
            Passed = true, MonsterId = monsterId, Attacks = attacks, DropEntityId = drop.EntityId,
            drop.ItemSeed, Experience = main.ObservedSnapshot!.Players.Single().Experience,
            LootInventoryCount = lootInventoryCount,
            ApproachAndPickupCommands = main.ObservedCommandResult!.ClientSequence - pickupSequence,
            Sequence = main.ObservedCommandResult!.ClientSequence,
        });
    }

    private async Task ClickWorld(Main main, int x, int y, MouseButton button = MouseButton.Left)
    {
        var point = main.ObservedTileScreenPosition(x, y);
        var area = (Control)main.FindChild("WorldArea", true, false);
        Require(area.GetGlobalRect().HasPoint(point), "The observed target is outside the rendered world area.");
        await Click(point, button: button);
    }

    private static bool Settled(Main main)
        => main.ObservedPendingCommandCount == 0
            && main.ObservedSnapshot?.Players.Single() is { CurrentAction: null, CombatMode: PlayerCombatMode.Stand };

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
            Require(result.Status == CommandStatus.Accepted, $"{action}: {result.Status}/{result.RejectReason}.");
            return main.ObservedSnapshot!.Tick >= result.AppliedTick && Settled(main);
        }, $"{action} did not finish.", stageSeconds: 10);
    }
}
