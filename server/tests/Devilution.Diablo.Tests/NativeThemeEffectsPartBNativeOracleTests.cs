using System.Text.Json;
using Devilution.Tests;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Actual native OperateObject/ProcessObjects output, regenerated archive-free in CI. */
public sealed class NativeThemeEffectsPartBNativeOracleTests
{
    [Theory]
    [InlineData(66)] [InlineData(76)] [InlineData(79)] [InlineData(80)]
    [InlineData(81)] [InlineData(82)] [InlineData(26, Traits = ["ObjectFamily", "BurningCross"])]
    [InlineData(91, Traits = ["ObjectFamily", "BurningCross"])]
    [Trait("Category", "Slow")]
    public void ThemeEffectMatchesNativeIncludingRepeatRngFamilyAndPeerScope(int type)
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_THEME_EFFECTS_PART_B_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) Assert.Skip("Run tools/run-native-oracle-parity.sh for part B.");
        var path = Path.Combine(directory!, "native-theme-effects-part-b.json");
        Assert.InRange(new FileInfo(path).Length, 1, 8 * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal("godevilution-native-theme-effects-part-b", root.GetProperty("format").GetString());
        var catalog = ReadCatalog(root.GetProperty("catalog"));
        var cases = root.GetProperty("cases").EnumerateArray().Where(row => N(row, "object") == type).ToArray();
        Assert.NotEmpty(cases);
        var selected = new HashSet<(bool Multiplayer, int Shrine)>();
        foreach (var row in cases) {
            var depth = N(row, "depth"); var seed = row.GetProperty("seed").GetUInt32();
            var multiplayer = row.GetProperty("multiplayer").GetBoolean(); var peer = N(row, "peer");
            var fixture = new NativeShrineTestFixture(depth, 0, seed, catalog,
                multiplayer ? DiabloGameMode.Multiplayer : DiabloGameMode.SinglePlayer, nativeObjectId: type);
            var players = Enumerable.Range(0, multiplayer ? 2 : 1).Select(index => NativeThemeEffectTests.Player(fixture, depth, (uint)index + 1)).ToArray();
            for (var index = 0; index < players.Length; index++) {
                var player = players[index]; player.PositionX += index;
                if (type is 26 or 91) player.PositionY = 49;
            }
            var input = row.GetProperty("input"); var hero = players[0];
            hero.Life = N(input, "life"); hero.Mana = N(input, "mana");
            hero.RestoreNativeResourceBases(N(input, "lifeBase"), N(input, "manaBase"));
            hero.ManaShieldActive = input.GetProperty("manaShield").GetBoolean();
            if (type is 26 or 91 && seed == 3) hero.CombatMode = DiabloPlayerCombatMode.Death;
            if (type is 26 or 91 && seed == 1) {
                var definition = fixture.Items.Definitions.First(item => item.ItemType == 1);
                hero.Equipment.Add(new(4, 42, fixture.Items.Generate(definition.ItemId, 42) with {
                    StatFlag = true, Identified = true, PlusFireResistance = 75,
                }));
            }
            DiabloWorldIntentResult? result = null;
            if (type is 26 or 91) {
                fixture.Simulation.AdvanceTick(1, players); fixture.Simulation.AdvanceTick(2, players);
            } else {
                result = fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, players);
                Assert.True(result.Value.Succeeded, $"Object {type} L{depth} seed {seed}");
            }
            var expected = row.GetProperty("expected");
            EqualPlayer(fixture, expected, players[peer]);
            if (peer == 0) {
                Assert.Equal(row.GetProperty("rng").GetUInt32(), fixture.Random.State);
                Assert.Equal(N(row, "groundCount"), fixture.World.WorldItems.Count);
                Assert.Equal(N(row, "selection") == 0, fixture.Shrine.Activated || type is 26 or 91);
                Assert.Equal(N(row, "frame"), fixture.Shrine.AnimationFrame);
                if (type == 81) Assert.Equal(row.GetProperty("duration").GetUInt32(), Assert.Single(hero.StatusEffects).RemainingTicks);
                else Assert.True(N(row, "missiles") == fixture.World.Projectiles.Count, $"Object {type} L{depth} seed {seed} shrine {N(row, "shrineType")}: missile count");
            }
            var expectedMessage = row.GetProperty("message").GetString();
            var messages = peer == 0 ? result?.Events ?? [] : fixture.Rules.DrainDirectedEvents()
                .Where(value => ReferenceEquals(value.Recipient, players[peer])).Select(value => value.Event).ToArray();
            Assert.Equal(expectedMessage ?? "", messages.Where(value => value.Kind == DiabloGameplayEventKind.Dialogue).Select(value => value.Text).LastOrDefault() ?? "");
            if (row.TryGetProperty("repeat", out var repeat)) {
                var repeated = fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, players);
                Assert.Equal(type is 66 or 76, repeated.Succeeded);
                EqualPlayer(fixture, repeat, players[peer]);
            }
            if (type is 79 or 80) selected.Add((multiplayer, N(row, "shrineType")));
        }
        if (type is 79 or 80) {
            foreach (var multiplayer in new[] { false, true }) {
                var availability = multiplayer ? AuthoritativeNativeShrineAvailability.Multiplayer : AuthoritativeNativeShrineAvailability.SinglePlayer;
                Assert.Equal(Enumerable.Range(0, 26).Where(shrine => shrine != 8 && availability[shrine]),
                    selected.Where(value => value.Multiplayer == multiplayer).Select(value => value.Shrine).Order());
            }
        }
    }

    private static int N(JsonElement row, string name) => row.GetProperty(name).GetInt32();

    private static void EqualPlayer(NativeShrineTestFixture fixture, JsonElement expected, AuthoritativePlayerState player)
    {
        Assert.Equal(new[] { N(expected, "strength"), N(expected, "magic"), N(expected, "dexterity"), N(expected, "vitality") },
            new[] { player.Attributes.Strength.Base, player.Attributes.Magic.Base, player.Attributes.Dexterity.Base, player.Attributes.Vitality.Base });
        Assert.Equal(N(expected, "life"), player.Life); Assert.Equal(N(expected, "mana"), player.Mana);
        Assert.Equal(N(expected, "lifeMaximum"), player.LifeMaximum); Assert.Equal(N(expected, "manaMaximum"), player.ManaMaximum);
        Assert.Equal(N(expected, "lifeBaseMaximum"), player.BaseLifeMaximum); Assert.Equal(N(expected, "manaBaseMaximum"), player.BaseManaMaximum);
        Assert.Equal(N(expected, "lifeBase"), player.NativeLifeBase ?? player.Life + player.BaseLifeMaximum - player.LifeMaximum);
        Assert.Equal(N(expected, "manaBase"), player.NativeManaBase ?? player.Mana + player.BaseManaMaximum - player.ManaMaximum);
        Assert.Equal(expected.GetProperty("gold").GetUInt32(), player.Gold);
        Assert.Equal(expected.GetProperty("manaShield").GetBoolean(), player.ManaShieldActive);
        var bits = expected.GetProperty("spells").GetUInt64();
        var levels = expected.GetProperty("spellLevels").EnumerateArray().Select(value => value.GetUInt32()).ToArray();
        Assert.Equal(Enumerable.Range(1, 37).Where(id => (bits & (1UL << (id - 1))) != 0)
            .Select(id => new MemorizedSpellState(fixture.Spells.Definitions.Single(spell => spell.NativeSpellId == (uint)id).SpellId, levels[id]))
            .OrderBy(value => value.SpellId), player.MemorizedSpells.OrderBy(value => value.SpellId));
        Assert.Equal(expected.GetProperty("goldPiles").EnumerateArray().Select(value => value.GetInt32()), player.Inventory.Select(value => value.State.Value));
    }
}
