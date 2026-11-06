using System.Text.Json;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Real native object/missile functions, regenerated without an archive by the parity script. */
public sealed class NativeObjectTrapNativeOracleTests
{
    private static JsonDocument Corpus()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_OBJECT_TRAP_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
            Assert.Skip("Run tools/run-native-oracle-parity.sh to generate native object trap comparisons.");
        var path = Path.Combine(directory!, "native-object-traps.json");
        Assert.InRange(new FileInfo(path).Length, 1, 1024 * 1024);
        var document = JsonDocument.Parse(File.ReadAllBytes(path));
        Assert.Equal("godevilution-native-object-traps", document.RootElement.GetProperty("format").GetString());
        Assert.Equal(1, N(document.RootElement, "version"));
        return document;
    }

    [Fact]
    public void DoorsSarcophagiAndOrdinaryChestsFireOnceWhileConvertedChestsRetainTheirWallTrap()
    {
        using var corpus = Corpus();
        var cases = corpus.RootElement.GetProperty("wallCases").EnumerateArray().ToArray();
        Assert.Equal(4 * (6 + 8) * 3 * 2, cases.Length);
        foreach (var row in cases) {
            var opened = row.GetProperty("opened").GetBoolean();
            var ready = DiabloWorldState.UpdateTrapState(0, N(row, "triggerType"), opened ? 1 : 0, !opened, true);
            Assert.Equal(row.GetProperty("ready").GetBoolean(), ready);
            Assert.Equal(ready ? 1 : 0, N(row, "var4"));
            Assert.Equal(!ready, row.GetProperty("trapFlag").GetBoolean());
            Assert.False(DiabloWorldState.UpdateTrapState(1, N(row, "triggerType"), 1, false, false));
            var missiles = row.GetProperty("missiles").EnumerateArray().ToArray();
            if (!ready) {
                Assert.Empty(missiles);
                Assert.Equal(2588U, U(row, "rng"));
                continue;
            }
            var random = new DeterministicLegacyRandom(2588);
            var facts = new AuthoritativeCathedralNativeTrapFacts(1, 701, 53, 42, 1, 48, 50,
                0, 50, 50, N(row, "missileType"), 0, 0, 700, N(row, "triggerType"), 50, 50, true);
            var player = Player(50, 51);
            var target = DiabloWorldState.FindNativeTrapTarget(facts, [player]);
            Assert.Equal((50, 51), target);
            var projectile = DiabloCombatCommandProcessor.AddObjectTrapMissile(800, 701, 700, 42, 1,
                48, 50, target.X, target.Y, (int)DeterministicGridDirections.Toward(48, 50, target.X, target.Y),
                N(row, "missileType"), false, random, N(row, "depth"));
            EqualMissile(Assert.Single(missiles), projectile);
            Assert.Equal(U(row, "rng"), random.State);
        }
    }

    [Fact]
    public void FlightImpactExpiryAndLightningChildrenMatchNativeEveryTick()
    {
        using var corpus = Corpus();
        var cases = corpus.RootElement.GetProperty("flightCases").EnumerateArray().ToArray();
        Assert.Equal(16, cases.Length);
        foreach (var row in cases) {
            var depth = N(row, "depth");
            var type = N(row, "type");
            var random = new DeterministicLegacyRandom(2588);
            var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112,
                fallbackMissileBlockedCells: [50 * 112 + 48, 50 * 112 + 52]);
            var player = Player(60, 60);
            var processor = new DiabloCombatCommandProcessor(world, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
                new AuthoritativeCombatRules(10, 1, 100), random);
            var initial = DiabloCombatCommandProcessor.AddObjectTrapMissile(800, 701, 700, 42, 1,
                48, 50, 56, 50, (int)DeterministicGridDirections.Toward(48, 50, 56, 50),
                type, type == 27, random, depth);
            world.Projectiles.Add(initial.EntityId, initial);
            EqualMissile(Assert.Single(row.GetProperty("initialMissiles").EnumerateArray()), initial);
            Assert.Equal(U(row, "initialRng"), random.State);
            foreach (var tick in row.GetProperty("ticks").EnumerateArray()) {
                var ids = world.Projectiles.Keys.Order().ToList();
                var seen = ids.ToHashSet();
                for (var index = 0; index < ids.Count; index++) {
                    if (world.Projectiles.TryGetValue(ids[index], out var projectile))
                        processor.ProcessObjectTrapMissile((ulong)N(tick, "tick"), projectile, depth, [player], []);
                    foreach (var id in world.Projectiles.Keys.Order())
                        if (seen.Add(id)) ids.Add(id);
                }
                var actual = world.Projectiles.Values.OrderBy(value => value.EntityId).ToArray();
                var expected = tick.GetProperty("missiles").EnumerateArray().ToArray();
                Assert.True(expected.Length == actual.Length,
                    $"depth {depth} type {type} tick {N(tick, "tick")}: expected {expected.Length} missiles, actual {actual.Length}");
                for (var index = 0; index < expected.Length; index++)
                    EqualMissile(expected[index], actual[index]);
                Assert.True(U(tick, "rng") == random.State,
                    $"depth {depth} type {type} tick {N(tick, "tick")}: native RNG {U(tick, "rng")}, actual {random.State}");
            }
        }
    }

    [Fact]
    public void NativeTrapDamageAndResistanceUseTheSameRandomStreamOnEveryDepth()
    {
        using var corpus = Corpus();
        var cases = corpus.RootElement.GetProperty("playerHits").EnumerateArray().ToArray();
        Assert.Equal(8 * 4 * 2, cases.Length);
        foreach (var row in cases) {
            var resistance = N(row, "resistance");
            var equipment = new EquippedStoreItem(0, 1, AuthoritativeItemState.Empty with {
                StatFlag = true, Identified = true,
                Flags = AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag,
                PlusGetHit = 1, PlusFireResistance = resistance, PlusLightningResistance = resistance,
            });
            var player = Player(50, 50, [equipment], N(row, "armor"));
            var random = new DeterministicLegacyRandom(0);
            var processor = new DiabloCombatCommandProcessor(new DiabloWorldState(), new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
                new AuthoritativeCombatRules(10, 1, 100), random);
            var type = N(row, "type");
            var hit = processor.PlayerMHit(701, player, N(row, "minimum"), N(row, "maximum"), 1,
                type == 0 ? AuthoritativeDamageType.Physical : type == 7 ? AuthoritativeDamageType.Lightning : AuthoritativeDamageType.Fire,
                type is 0 or 27, 1, [player], []);
            Assert.Equal(row.GetProperty("hit").GetBoolean(), hit);
            Assert.Equal(N(row, "life"), player.Life);
            Assert.Equal(U(row, "rng"), random.State);
        }
    }

    [Fact]
    public void ActualTrapMissileCollisionsMatchNativeDamageUnitsBlockingAndRandomStream()
    {
        using var corpus = Corpus();
        var cases = corpus.RootElement.GetProperty("impactCases").EnumerateArray().ToArray();
        Assert.Equal(48, cases.Length);
        foreach (var row in cases) {
            var depth = N(row, "depth"); var type = N(row, "type"); var targetKind = N(row, "target");
            var resistance = N(row, "resistance");
            var equipment = targetKind == 2 ? new[] { new EquippedStoreItem(4, 1,
                AuthoritativeItemState.Empty with { StatFlag = true, ItemType = 5 }) } : [];
            // Match the native scalar resistance without adding unrelated equipment bonuses.
            var elemental = new EquippedStoreItem(0, 2, AuthoritativeItemState.Empty with {
                StatFlag = true, Identified = true, PlusFireResistance = resistance, PlusLightningResistance = resistance,
            });
            var player = Player(targetKind == 0 ? 60 : 50, targetKind == 0 ? 60 : 50,
                equipment.Concat([elemental]).ToArray(), N(row, "armor"));
            player.Attributes = player.Attributes with { Dexterity = new(100, 100) };
            var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);
            var monster = new AuthoritativeCombatTarget(901, 50, 50, 100, levelId: 42, monsterId: 1,
                fireResistance: resistance, lightningResistance: resistance);
            monster.RestoreNativeInitialization(new(new string('A', 64),
                    new AuthoritativeCathedralPopulationMonsterState { Slot = N(row, "slot"), Type = 0, UniqueType = 255 },
                    0, 0, 0, 0, new(N(row, "slot"), 0, 255, 901, 1, 42, string.Empty), false), 0, 0);
            if (targetKind == 0) world.CombatTargets.Add(monster.EntityId, monster);
            var random = new DeterministicLegacyRandom(U(row, "seed"));
            var processor = new DiabloCombatCommandProcessor(world, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
                new AuthoritativeCombatRules(10, 1, 100), random);
            var initial = DiabloCombatCommandProcessor.AddObjectTrapMissile(800, 701, 700, 42, 1,
                48, 50, 56, 50, (int)DeterministicGridDirections.Toward(48, 50, 56, 50),
                type, type == 27, random, depth);
            world.Projectiles.Add(initial.EntityId, initial);
            foreach (var tick in row.GetProperty("ticks").EnumerateArray()) {
                var ids = world.Projectiles.Keys.Order().ToList(); var seen = ids.ToHashSet();
                for (var index = 0; index < ids.Count; index++) {
                    if (world.Projectiles.TryGetValue(ids[index], out var projectile))
                        processor.ProcessObjectTrapMissile((ulong)N(tick, "tick"), projectile, depth, [player], []);
                    foreach (var id in world.Projectiles.Keys.Order()) if (seen.Add(id)) ids.Add(id);
                }
                var context = $"depth {depth}, type {type}, target {targetKind}, resistance {resistance}, tick {N(tick, "tick")}";
                Assert.True(N(tick, "life") == player.Life, context + $": player native {N(tick, "life")}, managed {player.Life}");
                Assert.True(N(tick, "monsterHp") == monster.HitPointsQ6, context + $": monster native {N(tick, "monsterHp")}, managed {monster.HitPointsQ6}");
                Assert.True(U(tick, "rng") == random.State, context + $": RNG native {U(tick, "rng")}, managed {random.State}");
                Assert.Equal(tick.GetProperty("blocked").GetBoolean(), player.CombatMode == DiabloPlayerCombatMode.Block);
            }
        }
        // L8 Lightning carries 16..23 Q6, not 16..23 whole HP, and never blocks.
        var lightningMonster = cases.Single(row => N(row, "depth") == 8 && N(row, "type") == 7
            && N(row, "target") == 0 && N(row, "resistance") == 0);
        Assert.InRange(6400 - N(lightningMonster.GetProperty("ticks").EnumerateArray()
            .First(tick => N(tick, "monsterHp") < 6400), "monsterHp"), 16, 23);
        var lightningShield = cases.Single(row => N(row, "depth") == 8 && N(row, "type") == 7
            && N(row, "target") == 2 && N(row, "resistance") == 0);
        var shiftedHit = lightningShield.GetProperty("ticks").EnumerateArray().First(tick => N(tick, "life") < 6400);
        Assert.Equal(64, 6400 - N(shiftedHit, "life"));
        Assert.False(shiftedHit.GetProperty("blocked").GetBoolean());
    }

    internal static void EqualMissile(JsonElement expected, AuthoritativeProjectile actual)
    {
        var state = actual.NativeTrapArrow!;
        var retained = JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(actual))!;
        state.ValidateShape(retained);
        var type = state.Stage == NativeObjectMissileStage.LightningSegment ? 8
            : state.Stage == NativeObjectMissileStage.Explosion ? 9 : state.MissileTypeId;
        Assert.Equal(N(expected, "type"), type);
        Assert.Equal(-1, N(expected, "source"));
        Assert.Equal(1, N(expected, "caster"));
        Assert.Equal(N(expected, "damage"), actual.Damage);
        Assert.Equal((uint)N(expected, "duration"), actual.RemainingTicks);
        Assert.Equal(Point(expected, "tile"), (actual.PositionX, actual.PositionY));
        Assert.Equal(Point(expected, "start"), (actual.StartX, actual.StartY));
        Assert.Equal(Point(expected, "velocity"), (actual.VelocityX, actual.VelocityY));
        Assert.Equal(Point(expected, "traveled"), (actual.TraveledX, actual.TraveledY));
    }

    private static AuthoritativePlayerState Player(int x, int y, IReadOnlyList<EquippedStoreItem>? equipment = null, int armor = 0)
        => new(0, 0, 6400, 0, 0, PlayerAttributesState.Zero, equipment ?? [], [], [],
            x, y, 6400, 1, 42, armor) { EntityId = 900 };

    private static (int X, int Y) Point(JsonElement row, string name)
        => (N(row.GetProperty(name), "x"), N(row.GetProperty(name), "y"));
    private static int N(JsonElement row, string name) => row.GetProperty(name).GetInt32();
    private static uint U(JsonElement row, string name) => row.GetProperty(name).GetUInt32();
}
