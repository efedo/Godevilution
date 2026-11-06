using System.Text.Json;
using Devilution.Tests;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Real OperateShrine outcomes, regenerated archive-free by tools/run-native-oracle-parity.sh. */
public sealed class NativeShrineNativeOracleTests
{
    public static IEnumerable<object[]> ShrineTypes => Enumerable.Range(0, 26).Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(ShrineTypes))]
    public void VanillaShrineMatchesNativeForBothFamiliesAndMultiplayerPeers(int type)
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_SHRINE_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) Assert.Skip("Run tools/run-native-oracle-parity.sh for the native shrine comparison.");
        var path = Path.Combine(directory!, "native-vanilla-shrines.json");
        Assert.InRange(new FileInfo(path).Length, 1, 8 * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var corpus = document.RootElement;
        Assert.Equal("godevilution-native-vanilla-shrines", corpus.GetProperty("format").GetString());
        Assert.Equal(401, corpus.GetProperty("caseCount").GetInt32());
        var catalog = ReadCatalog(corpus.GetProperty("catalog"));
        var cases = corpus.GetProperty("cases").EnumerateArray().Where(row => N(row, "type") == type).ToArray();
        Assert.NotEmpty(cases);
        foreach (var row in cases) {
            var depth = N(row, "depth");
            var multiplayer = row.GetProperty("multiplayer").GetBoolean();
            var peer = N(row, "peer");
            var fixture = new NativeShrineTestFixture(depth, type, row.GetProperty("seed").GetUInt32(), catalog,
                multiplayer ? DiabloGameMode.Multiplayer : DiabloGameMode.SinglePlayer);
            fixture.Random.Restore(row.GetProperty("globalSeed").GetUInt32());
            var input = row.GetProperty("input");
            var players = Enumerable.Range(0, multiplayer ? 3 : 1).Select(index => ReadPlayer(fixture,
                index == 1 && row.TryGetProperty("deadPeerInput", out var deadInput) ? deadInput : input, depth, index)).ToArray();
            var result = fixture.Rules.Execute(players[0], new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, players);
            Assert.True(result.Succeeded, $"Shrine {type} L{depth} peer {peer}: {result.Failure}");
            var dead = players[peer].CombatMode == DiabloPlayerCombatMode.Death;
            EqualPlayer(fixture, dead && type == 18 ? row.GetProperty("deadPeerInput") : row.GetProperty("expected"),
                players[peer], keepDeadLifeZero: dead);
            Assert.True(fixture.Shrine.Activated);
            Assert.False(fixture.World.CanOperateNativeObject(fixture.Shrine));
            Assert.Equal(row.GetProperty("explored").GetInt32() > 0, players[peer].FullyExploredLevelIds.Contains((uint)depth));
            var messages = peer == 0 ? result.Events : fixture.Rules.DrainDirectedEvents()
                .Where(delivery => delivery.Recipient == players[peer]).Select(delivery => delivery.Event).ToArray();
            Assert.Equal(row.GetProperty("message").GetBoolean(), messages.Count != 0);
            foreach (var message in messages) Assert.Equal(row.GetProperty("messageText").GetString(), message.Text);
            // Each peer's native world is separate; only the activating peer produces inventory/world loot.
            if (peer == 0) {
                Assert.Equal(row.GetProperty("rng").GetUInt32(), fixture.Random.State);
                var missiles = row.GetProperty("missileStates").EnumerateArray().ToArray();
                Assert.Equal(missiles.Length, fixture.World.Projectiles.Count);
                foreach (var pair in missiles.Zip(fixture.World.Projectiles.Values.OrderBy(value => value.EntityId))) {
                    // Native Nova keeps whole HP until collision; the shared Nova controller/balls retain Q6 damage.
                    var nativeDamage = pair.First.GetProperty("damage").GetInt32();
                    Assert.Equal(type == 10 ? nativeDamage << 6 : nativeDamage, pair.Second.Damage);
                    Assert.Equal(pair.First.GetProperty("duration").GetUInt32(), pair.Second.RemainingTicks);
                    var position = pair.First.GetProperty("tile");
                    Assert.Equal((position.GetProperty("x").GetInt32(), position.GetProperty("y").GetInt32()), (pair.Second.PositionX, pair.Second.PositionY));
                    Assert.Equal(1U, pair.Second.SourceEntityId);
                    Assert.Equal(type == 10 ? AuthoritativeProjectileBehavior.NovaController : AuthoritativeProjectileBehavior.Phasing, pair.Second.Behavior);
                    if (type == 10) Assert.Equal(pair.First.GetProperty("spellLevel").GetInt32(), pair.Second.EffectiveSpellLevel);
                }
                var chests = fixture.World.Objects.Values.Where(value => value.NativeObjectId is 5 or 6).OrderBy(value => value.NativeObjectId).ToArray();
                Assert.Equal(row.GetProperty("chestSeeds").EnumerateArray().Select(value => value.GetUInt32()),
                    chests.Select(chest => chest.NativeRandomSeed ?? (uint)(800 + chest.NativeObjectId - 5)));
                Assert.Equal(row.GetProperty("chestFrames").EnumerateArray().Select(value => value.GetInt32()), chests.Select(chest => chest.AnimationFrame));
                var ground = row.GetProperty("ground").EnumerateArray().ToArray();
                Assert.Equal(ground.Length, fixture.World.WorldItems.Count);
                foreach (var pair in ground.Zip(fixture.World.WorldItems.Values.OrderBy(item => item.EntityId)))
                    EqualItem(fixture, pair.First, pair.Second.ItemSeed, pair.Second.State);
            }
            if (row.TryGetProperty("afterResurrect", out var resurrected)) {
                var target = players[peer];
                var saved = fixture.Simulation.CaptureSnapshot(target, 0);
                var spell = fixture.Spells.Definitions.Single(value => value.ProjectileBehavior == AuthoritativeProjectileBehavior.Resurrection);
                players[0].Mana = players[0].ManaMaximum;
                players[0].MemorizedSpells.Add(new(spell.SpellId, 1));
                Assert.True(fixture.Simulation.Execute(players[0], new DiabloCastIntent(spell.SpellId, target.EntityId, 0, 0,
                    DiabloCastSourceKind.MemorizedSpell), 1, players).Succeeded);
                EqualPlayer(fixture, resurrected, target);
                fixture.Simulation.RestoreSnapshot(target, saved);
                target.CurrentAction = null; target.CombatModeEndTick = 0;
                var animations = AuthoritativePlayerAnimationCatalog.LoadTsv("class_animations.tsv", File.ReadAllText(
                    Path.Combine(NativeShrineTestFixture.RepositoryRoot(), "server/content/base/class_animations.tsv")), fixture.Classes);
                var town = new DiabloWorldCommandProcessor(new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112),
                    animations, useNativePlayerActionTiming: true);
                Assert.True(town.Execute(target, new DiabloRestartTownIntent(), 2, [target]).Succeeded);
                EqualResources(row.GetProperty("afterRestart"), target);
            }
        }
    }

    private static AuthoritativePlayerState ReadPlayer(NativeShrineTestFixture fixture, JsonElement row, int depth, int index)
    {
        PlayerAttributeState Attribute(string key) => new(N(row, key), N(row, "current" + char.ToUpperInvariant(key[0]) + key[1..]));
        var equipment = row.GetProperty("equipment").EnumerateArray().Select((item, slot) => (item, slot))
            .Where(value => N(value.item, "type") != -1).Select(value => new EquippedStoreItem((uint)value.slot,
                value.item.GetProperty("seed").GetUInt32(), ReadItem(fixture, value.item))).ToArray();
        var inventory = row.GetProperty("inventory").EnumerateArray().Select(item => new OwnedStoreItem(0, 0,
            item.GetProperty("seed").GetUInt32(), 0, 0, ReadItem(fixture, item))).ToArray();
        var belt = row.GetProperty("belt").EnumerateArray().Select((item, slot) => (item, slot))
            .Where(value => N(value.item, "type") != -1).Select(value => new BeltStoreItem((uint)value.slot,
                value.item.GetProperty("seed").GetUInt32(), ReadItem(fixture, value.item))).ToArray();
        var bits = row.GetProperty("spells").GetUInt64();
        var levels = row.GetProperty("spellLevels").EnumerateArray().Select(value => value.GetUInt32()).ToArray();
        var known = Enumerable.Range(1, 51).Where(spell => (bits & (1UL << (spell - 1))) != 0)
            .Select(spell => new MemorizedSpellState(fixture.Spells.TryGetByNativeId((uint)spell, out var definition) ? definition.SpellId : throw new InvalidDataException(), levels[spell])).ToArray();
        var player = new AuthoritativePlayerState(0, 0, N(row, "life"), N(row, "mana"), N(row, "manaMaximum"),
            new(Attribute("strength"), Attribute("magic"), Attribute("dexterity"), Attribute("vitality")),
            equipment, row.GetProperty("grid").EnumerateArray().Select(value => value.GetInt32() == 0 ? -1 : Math.Abs(value.GetInt32()) - 1).ToArray(), belt,
            50 + index, 51, N(row, "lifeMaximum"), 10, (uint)depth, 0, inventory: inventory,
            memorizedSpells: known, baseLifeMaximum: N(row, "lifeBaseMaximum"), baseManaMaximum: N(row, "manaBaseMaximum"),
            playerClass: fixture.Classes.GetRequired(0), items: fixture.Items) { EntityId = (uint)index + 1 };
        player.RestoreNativeResourceBases(N(row, "lifeBase"), N(row, "manaBase"));
        player.AdoptEquipmentAttributeSnapshot();
        if (row.GetProperty("dead").GetBoolean()) {
            player.CombatMode = DiabloPlayerCombatMode.Death; player.CombatModeEndTick = 10;
            player.CurrentAction = new(DiabloPlayerActionKind.Death, 0, 0, 10, 10) { EffectApplied = true, TargetEntityId = 1 };
        }
        return player;
    }

    private static AuthoritativeItemState ReadItem(NativeShrineTestFixture fixture, JsonElement row)
    {
        var state = fixture.Items.Generate((uint)N(row, "index") + 1, row.GetProperty("seed").GetUInt32());
        return state with { ItemType = N(row, "type"), CreateInfo = row.GetProperty("createInfo").GetUInt32(),
            MiscId = N(row, "misc"), SpellId = N(row, "spell") == 0 ? 0 : (int)fixture.Spells.Definitions.Single(spell => spell.NativeSpellId == (uint)N(row, "spell")).SpellId, ItemIndex = N(row, "index"),
            MinDamage = N(row, "minDamage"), MaxDamage = N(row, "maxDamage"), ArmorClass = N(row, "armor"),
            Durability = N(row, "durability"), MaxDurability = N(row, "maxDurability"),
            Charges = N(row, "charges"), MaxCharges = N(row, "maxCharges"), Value = N(row, "value"),
            MinimumMagic = N(row, "minimumMagic"), Identified = row.GetProperty("identified").GetBoolean(),
            StatFlag = row.GetProperty("statFlag").GetBoolean(), Magical = N(row, "quality"),
            MinimumStrength = 0, MinimumDexterity = 0, PlusMana = N(row, "plusMana"),
        };
    }

    private static void EqualPlayer(NativeShrineTestFixture fixture, JsonElement expected, AuthoritativePlayerState player, bool keepDeadLifeZero = false)
    {
        Assert.Equal(new[] { N(expected, "strength"), N(expected, "magic"), N(expected, "dexterity"), N(expected, "vitality") },
            new[] { player.Attributes.Strength.Base, player.Attributes.Magic.Base, player.Attributes.Dexterity.Base, player.Attributes.Vitality.Base });
        Assert.Equal(new[] { N(expected, "currentStrength"), N(expected, "currentMagic"), N(expected, "currentDexterity"), N(expected, "currentVitality") },
            new[] { player.Attributes.Strength.Current, player.Attributes.Magic.Current, player.Attributes.Dexterity.Current, player.Attributes.Vitality.Current });
        EqualResources(expected, player, keepDeadLifeZero);
        Assert.Equal(expected.GetProperty("gold").GetUInt32(), player.Gold);
        Assert.Equal(expected.GetProperty("manaShield").GetBoolean(), player.ManaShieldActive);
        var bits = expected.GetProperty("spells").GetUInt64();
        var levels = expected.GetProperty("spellLevels").EnumerateArray().Select(value => value.GetUInt32()).ToArray();
        Assert.Equal(Enumerable.Range(1, 51).Where(spell => (bits & (1UL << (spell - 1))) != 0)
            .Select(spell => new MemorizedSpellState(fixture.Spells.TryGetByNativeId((uint)spell, out var definition) ? definition.SpellId : throw new InvalidDataException(), levels[spell])).OrderBy(spell => spell.SpellId), player.MemorizedSpells.OrderBy(spell => spell.SpellId));
        var equipment = expected.GetProperty("equipment").EnumerateArray().Where(item => N(item, "type") != -1).ToArray();
        Assert.Equal(equipment.Length, player.Equipment.Count);
        foreach (var pair in equipment.Zip(player.Equipment.OrderBy(item => item.Slot))) EqualItem(fixture, pair.First, pair.Second.ItemSeed, pair.Second.State);
        var inventory = expected.GetProperty("inventory").EnumerateArray().ToArray();
        Assert.Equal(inventory.Length, player.Inventory.Count);
        foreach (var pair in inventory.Zip(player.Inventory)) {
            EqualItem(fixture, pair.First, pair.Second.ItemSeed, pair.Second.State);
            if (N(pair.First, "type") == 11) {
                Assert.Equal((uint)N(pair.First, "value"), pair.Second.Price);
                Assert.Equal(N(pair.First, "value"), pair.Second.State.IdentifiedValue);
            }
        }
        var belt = expected.GetProperty("belt").EnumerateArray().Where(item => N(item, "type") != -1).ToArray();
        Assert.Equal(belt.Length, player.Belt.Count);
        foreach (var pair in belt.Zip(player.Belt.OrderBy(item => item.Slot))) EqualItem(fixture, pair.First, pair.Second.ItemSeed, pair.Second.State);
        Assert.Equal(expected.GetProperty("grid").EnumerateArray().Select(value => value.GetInt32() == 0 ? -1 : Math.Abs(value.GetInt32()) - 1), player.InventoryGrid);
    }

    private static void EqualResources(JsonElement expected, AuthoritativePlayerState player, bool keepDeadLifeZero = false)
    {
        Assert.Equal((keepDeadLifeZero ? 0 : N(expected, "life"), N(expected, "lifeMaximum"), N(expected, "lifeBaseMaximum")),
            (player.Life, player.LifeMaximum, player.BaseLifeMaximum));
        Assert.Equal((N(expected, "mana"), N(expected, "manaMaximum"), N(expected, "manaBaseMaximum")),
            (player.Mana, player.ManaMaximum, player.BaseManaMaximum));
        Assert.Equal((N(expected, "lifeBase"), N(expected, "manaBase")), (player.NativeLifeBase!.Value, player.NativeManaBase!.Value));
    }

    private static void EqualItem(NativeShrineTestFixture fixture, JsonElement expected, uint seed, AuthoritativeItemState state)
    {
        var nativeSeed = expected.GetProperty("seed").GetUInt32();
        Assert.Equal(N(expected, "type") == 11 && nativeSeed == 0 ? uint.MaxValue : nativeSeed, seed);
        var keys = new[] { "type", "index", "createInfo", "misc", "spell", "minDamage", "maxDamage", "armor", "durability",
            "maxDurability", "charges", "maxCharges", "value", "minimumMagic", "quality" };
        var actual = new[] { state.ItemType, state.ItemIndex, (int)state.CreateInfo, state.MiscId, fixture.Spells.TryGet((uint)state.SpellId, out var spell) ? (int)spell.NativeSpellId : state.SpellId, state.MinDamage,
            state.MaxDamage, state.ArmorClass, state.Durability, state.MaxDurability, state.Charges, state.MaxCharges,
            state.Value, state.MinimumMagic, state.Magical };
        foreach (var pair in keys.Zip(actual)) Assert.True(N(expected, pair.First) == pair.Second,
            $"Item {seed} {pair.First}: expected {N(expected, pair.First)}, actual {pair.Second}");
        Assert.Equal(expected.GetProperty("identified").GetBoolean(), state.Identified);
        Assert.Equal(expected.GetProperty("statFlag").GetBoolean(), state.StatFlag);
    }

    private static int N(JsonElement value, string name) => value.GetProperty(name).GetInt32();
}
