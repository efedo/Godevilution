using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeTrapArrowFlightAndPlayerImpactMatchNativeCapture()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_TRAP_ORACLE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory));
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory!, "native-trap-arrow.json")));
        Assert.Equal("godevilution-cathedral-trap-arrow", document.RootElement.GetProperty("format").GetString());
        var velocity = DiabloCombatCommandProcessor.NativeTrapArrowVelocity(48, 50, 52, 50);
        foreach (var step in document.RootElement.GetProperty("flight").EnumerateArray()) {
            var distance = step.GetProperty("distance").GetInt32();
            var tile = DiabloCombatCommandProcessor.NativeTrapArrowTile(48, 50,
                velocity.X * distance, velocity.Y * distance);
            Assert.Equal((velocity.X, velocity.Y), (step.GetProperty("vx").GetInt32(), step.GetProperty("vy").GetInt32()));
            Assert.Equal((tile.X, tile.Y), (step.GetProperty("x").GetInt32(), step.GetProperty("y").GetInt32()));
            Assert.Equal((velocity.X * distance, velocity.Y * distance),
                (step.GetProperty("tx").GetInt32(), step.GetProperty("ty").GetInt32()));
            Assert.Equal(256 - distance, step.GetProperty("remaining").GetInt32());
            Assert.Equal(0x12345678U, step.GetProperty("rng").GetUInt32());
            Assert.Equal(0, step.GetProperty("hash").GetInt32());
        }
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        Assert.NotNull(setup);
        var simulation = setup!.Activation.Simulation;
        var world = simulation.WorldState;
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var rows = document.RootElement.GetProperty("players").EnumerateArray().ToArray();
        Assert.Equal(5, rows.Length);
        foreach (var row in rows) {
            var lane = row.GetProperty("lane").GetInt32();
            IReadOnlyList<EquippedStoreItem> equipment = lane == 4
                ? [new(0, 1, AuthoritativeItemState.Empty with {
                    StatFlag = true, Flags = AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag,
                })] : [];
            if (lane == 2) {
                var shield = simulation.Items!.GetRequired(101);
                equipment = [new(5, 101, AuthoritativeItemState.Empty with {
                    StableItemId = shield.ItemId, ItemType = shield.ItemType, EquipLocation = shield.EquipLocation,
                    ItemClass = shield.ItemClass, ArmorClass = shield.ArmorClass, ItemIndex = shield.NativeItemIndex,
                    Durability = shield.MaxDurability, MaxDurability = shield.MaxDurability,
                    Identified = true, StatFlag = true,
                })];
            }
            var dex = row.GetProperty("dexterity").GetInt32();
            var player = new AuthoritativePlayerState(0, 0, row.GetProperty("inputLife").GetInt32(),
                row.GetProperty("inputMana").GetInt32(), row.GetProperty("maximumMana").GetInt32(),
                new(new(20, 20), new(10, 10), new(dex, dex), new(0, 0)), equipment, [], [],
                60, 60, row.GetProperty("maximumLife").GetInt32(), 1, 1,
                row.GetProperty("armor").GetInt32(), rogue.ClassId, items: simulation.Items) { EntityId = 0x7E000701 };
            if (lane == 3)
                player.StatusEffects = [new(1, 10, 0) { Behavior = AuthoritativeStatusEffectBehavior.Etherealize }];
            var random = new DeterministicLegacyRandom(row.GetProperty("seed").GetUInt32());
            var processor = new DiabloCombatCommandProcessor(world, simulation.Spells, simulation.CombatRules,
                random, simulation.PlayerClasses, simulation.PlayerAnimations, useNativePlayerActionTiming: true,
                items: simulation.Items);
            var events = new List<DiabloGameplayEvent>();
            var source = world.CaptureNativeTraps()![0].Facts.TrapEntityId;
            var hit = processor.ResolveNativeTrapArrowPlayerHit(source, player,
                row.GetProperty("distance").GetInt32(), 1, [player], events);
            Assert.Equal(row.GetProperty("hit").GetBoolean(), hit);
            Assert.Equal(row.GetProperty("life").GetInt32(), player.Life);
            Assert.Equal(row.GetProperty("mana").GetInt32(), player.Mana);
            Assert.Equal(row.GetProperty("mode").GetInt32(), (int)player.CombatMode);
            Assert.Equal(row.GetProperty("rng").GetUInt32(), random.State);
            Assert.Equal(row.GetProperty("blocked").GetBoolean(), player.CombatMode == DiabloPlayerCombatMode.Block);
        }
    }
}
