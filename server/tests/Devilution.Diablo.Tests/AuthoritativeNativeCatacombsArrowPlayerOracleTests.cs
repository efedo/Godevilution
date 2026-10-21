using System.Reflection;
using System.Text.Json;
using Devilution.Engine;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralNativePopulationIntegrationTests;

namespace Devilution.Diablo.Tests;

/** Prepared PlayerMHit inputs compare the actual C2 impact leaf, independently of owner/flight admission. */
public sealed class AuthoritativeNativeCatacombsArrowPlayerOracleTests
{
    [Theory(Explicit = true)]
    [InlineData("warrior-low-damage-hit-no-recovery")]
    [InlineData("sorcerer-low-damage-hit-no-recovery")]
    [InlineData("player-arrow-miss")]
    [InlineData("player-arrow-block")]
    [InlineData("player-arrow-invincible")]
    [InlineData("player-arrow-etherealized")]
    [InlineData("player-arrow-already-dead")]
    [InlineData("moving-player-negative-then-settled")]
    [InlineData("new-player-interceptor-at-current-cell")]
    [InlineData("player-hit-before-same-cell-tile-blocker")]
    [InlineData("player-hit-before-same-cell-breakable-crux")]
    [InlineData("source-live-damage-changed-before-impact")]
    [InlineData("source-removed-before-impact")]
    [InlineData("source-dead-before-impact")]
    [InlineData("final-lifetime-tick-collision")]
    [InlineData("ordered-two-arrow-block-then-hit")]
    public void PreparedPlayerImpactMatchesNativeLifeModeFacingAndSharedRng(string caseName)
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_ARROW_FLIGHT_ORACLE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory));
        var path = Path.Combine(directory!, "native-catacombs-arrow-flight.json");
        Assert.InRange(new FileInfo(path).Length, 1, 32 * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var row = document.RootElement.GetProperty("cases").EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == caseName);
        using var fixture = CreateFixture(row.GetProperty("heroClassId").GetUInt32());
        var method = typeof(DiabloCombatCommandProcessor).GetMethod("ResolveNativeCatacombsArrowPlayerHit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var comparisons = 0;
        foreach (var tick in row.GetProperty("ticks").EnumerateArray()) {
            var input = Assert.Single(tick.GetProperty("playersBefore").EnumerateArray());
            var expected = Assert.Single(tick.GetProperty("playersAfter").EnumerateArray());
            var sourceInput = tick.GetProperty("actorsBefore").EnumerateArray()
                .Single(value => N(value, "slot") == N(row, "sourceSlot"));
            var geometry = new AuthoritativeWorld();
            geometry.AddLevel(new AuthoritativeLevel(5, 112, 112));
            var world = new DiabloWorldState(geometry);
            var player = Player(fixture, input);
            var source = new AuthoritativeCombatTarget(0x40000001,
                X(sourceInput, "tile"), Y(sourceInput, "tile"), N(sourceInput, "hitPointsQ6") / 64,
                levelId: 5, monsterId: 1, monsterLevel: N(sourceInput, "level"),
                toHit: N(sourceInput, "toHit"), minDamage: N(sourceInput, "minimumDamage"),
                maxDamage: N(sourceInput, "maximumDamage"), playerMinion: B(sourceInput, "playerMinion"));
            if (B(sourceInput, "active"))
                world.CombatTargets.Add(source.EntityId, source);
            PrepareProtection(fixture, world, player, input);
            var random = new DeterministicLegacyRandom(tick.GetProperty("rngBefore").GetUInt32());
            var processor = new DiabloCombatCommandProcessor(world, fixture.Spells,
                new AuthoritativeCombatRules(10, 1, 100), random, fixture.Activation.PlayerClasses,
                fixture.Activation.PlayerAnimations, useNativePlayerActionTiming: true, items: fixture.Items);
            // Native missile-list order is retained: the first impact may change block eligibility.
            foreach (var missile in tick.GetProperty("missilesBefore").EnumerateArray()) {
                var nextTraveledX = checked(X(missile, "traveled16_16") + X(missile, "velocity16_16"));
                var nextTraveledY = checked(Y(missile, "traveled16_16") + Y(missile, "velocity16_16"));
                var tile = DiabloCombatCommandProcessor.NativeTrapArrowTile(
                    X(missile, "start"), Y(missile, "start"), nextTraveledX, nextTraveledY);
                var moved = tile != (X(missile, "tile"), Y(missile, "tile"));
                Assert.InRange(Math.Abs(tile.X - X(missile, "tile"))
                    + Math.Abs(tile.Y - Y(missile, "tile")), 0, 1);
                var cell = tick.GetProperty("collisionCellsBefore").EnumerateArray()
                    .DistinctBy(value => value.GetRawText(), StringComparer.Ordinal)
                    .SingleOrDefault(value => X(value, "position") == tile.X && Y(value, "position") == tile.Y);
                if (cell.ValueKind == JsonValueKind.Undefined || N(cell, "signedPlayer") <= 0
                    || moved && tile == (X(missile, "start"), Y(missile, "start"))
                    || !moved && N(missile, "lastCollisionTargetHash") == N(cell, "signedOccupancyXor"))
                    continue;
                var launch = row.GetProperty("launches").EnumerateArray()
                    .Single(value => N(value, "ordinal") == N(missile, "ordinal"));
                var state = State(row, sourceInput, missile, launch, player);
                var events = new List<DiabloDirectedGameplayEvent>();
                _ = method.Invoke(processor, [1UL, state, player, new[] { player }, events]);
                comparisons++;
            }
            Assert.Equal(N(expected, "lifeQ6"), player.Life);
            Assert.Equal(N(expected, "mode"), (int)player.CombatMode);
            Assert.Equal(N(expected, "direction"), (int)player.FacingDirection);
            Assert.Equal(tick.GetProperty("rngAfter").GetUInt32(), random.State);
        }
        Assert.True(comparisons > 0, "The selected native case must exercise the real player-impact leaf.");
    }

    private static AuthoritativePlayerState Player(QualifiedCatacombsBuilderFixture fixture, JsonElement row)
    {
        var classId = row.GetProperty("heroClassId").GetUInt32();
        var definition = fixture.Activation.PlayerClasses.Definitions.Single(value => value.ClassId == classId);
        var dexterity = N(row, "blockChanceWithoutLevel") - definition.BaseBlockChance;
        IReadOnlyList<EquippedStoreItem> equipment = [];
        if (B(row, "blockFlag")) {
            var shield = fixture.Items.GetRequired(101);
            equipment = [new(5, 101, AuthoritativeItemState.Empty with {
                StableItemId = shield.ItemId, ItemType = shield.ItemType, EquipLocation = shield.EquipLocation,
                ItemClass = shield.ItemClass, ItemIndex = shield.NativeItemIndex,
                Durability = shield.MaxDurability, MaxDurability = shield.MaxDurability,
                Identified = true, StatFlag = true,
            })];
        }
        return new AuthoritativePlayerState(0, 0, N(row, "lifeQ6"), 6400, 6400,
            new(new(10, 10), new(10, 10), new(dexterity, dexterity), new(0, 0)), equipment, [], [],
            X(row, "tile"), Y(row, "tile"), N(row, "maximumLifeQ6"),
            row.GetProperty("characterLevel").GetUInt32(), 5, N(row, "armorValue"), classId,
            combatMode: (DiabloPlayerCombatMode)N(row, "mode"),
            facingDirection: (DiabloPlayerDirection)N(row, "direction"), items: fixture.Items) {
            EntityId = 0x70000001,
        };
    }

    private static void PrepareProtection(QualifiedCatacombsBuilderFixture fixture,
        DiabloWorldState world, AuthoritativePlayerState player, JsonElement row)
    {
        if (B(row, "etherealized"))
            player.StatusEffects = [new(1, 10, 0) { Behavior = AuthoritativeStatusEffectBehavior.Etherealize }];
        if (!B(row, "invincible"))
            return;
        // The native invincibility condition is represented by the server's active Flash owner.
        var flash = fixture.Spells.Definitions.Single(value => value.ProjectileBehavior == AuthoritativeProjectileBehavior.Flash);
        Assert.True(flash.ProjectileLifetime > 1);
        world.Projectiles.Add(0x60000001, new AuthoritativeProjectile(0x60000001, player.EntityId, 0,
            flash.SpellId, 5, player.PositionX, player.PositionY, player.PositionX, player.PositionY,
            0, AuthoritativeDamageType.Physical, 0, checked((uint)flash.ProjectileLifetime - 1)) {
            Behavior = AuthoritativeProjectileBehavior.FlashBottom,
        });
    }

    private static AuthoritativeNativeCatacombsArrowState State(JsonElement row, JsonElement source,
        JsonElement missile, JsonElement launch, AuthoritativePlayerState player)
    {
        var startX = X(missile, "start");
        var startY = Y(missile, "start");
        var targetX = N(launch, "requestedTargetX");
        var targetY = N(launch, "requestedTargetY");
        var direction = N(launch, "launchFacingDirection");
        var destination = AuthoritativeNativeCatacombsArrowState.ResolveEffectiveDestination(
            startX, startY, targetX, targetY, direction);
        return new(checked((uint)N(missile, "ordinal")), 0x40000001, N(row, "sourceSlot"), 1,
            N(row, "sourceType"), 5, 5, player.EntityId, N(row, "heroClassId"), "SkeletonRanged",
            new string('A', 64), new string('B', 64), new string('C', 64), startX, startY,
            targetX, targetY, direction, destination.X, destination.Y, N(launch, "direction16"),
            checked((uint)N(missile, "distance")), checked((short)N(missile, "lastCollisionTargetHash")),
            X(source, "tile"), Y(source, "tile"), N(source, "level"), N(source, "toHit"),
            N(source, "minimumDamage"), N(source, "maximumDamage"), B(source, "playerMinion"), B(source, "berserk"));
    }

    private static int N(JsonElement value, string name) => value.GetProperty(name).GetInt32();
    private static bool B(JsonElement value, string name) => value.GetProperty(name).GetBoolean();
    private static int X(JsonElement value, string name) => N(value.GetProperty(name), "x");
    private static int Y(JsonElement value, string name) => N(value.GetProperty(name), "y");
}
