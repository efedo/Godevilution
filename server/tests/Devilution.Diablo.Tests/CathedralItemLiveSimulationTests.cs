using System.Text.Json;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Actual command/death ownership and detached restore; not a full native level lifecycle. */
public sealed class CathedralItemLiveSimulationTests
{
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void ConsecutiveRealKillsConsumeCurrentPoolAndRestoreAmbientLootStream()
    {
        var profile = Profile();
        var first = Target(9, 7, 40, 40);
        var second = Target(10, 8, 41, 40);
        var simulation = DiabloSimulationFactory.Instance.Create(Options(profile, [first, second]));
        var hero = Hero(40, 39);
        var firstExpected = simulation.WorldState.PrepareCathedralDeathLoot(first, hero, [hero])!;
        Assert.True(simulation.Execute(hero, new DiabloAttackIntent(9), 1, [hero]).Succeeded);
        Assert.Equal(firstExpected.Loot.FinalRandomState, simulation.RandomState);
        var afterFirst = simulation.WorldState.CaptureCathedralItems()!;
        Assert.Equal(1, afterFirst.Levels[0].ActiveItemCount);

        var secondExpected = simulation.WorldState.PrepareCathedralDeathLoot(second, hero, [hero])!;
        Assert.True(simulation.Execute(hero, new DiabloAttackIntent(10), 2, [hero]).Succeeded);
        var current = simulation.WorldState.CaptureCathedralItems()!;
        Assert.Equal(2, current.Levels[0].ActiveItemCount);
        Assert.Equal(secondExpected.Loot.FinalRandomState, simulation.RandomState);
        Assert.Equal(secondExpected.Loot.Items, current.Levels[0].Items);
        Assert.Equal(secondExpected.Loot.SignedItemGrid, current.Levels[0].SignedItemRefs);
        Assert.Equal(2, simulation.WorldState.WorldItems.Count);
        Assert.Equal(2, simulation.WorldState.WorldItems.Values.Select(item => (item.PositionX, item.PositionY)).Distinct().Count());
        Assert.Empty(simulation.WorldState.CombatTargets);
    }

    [Fact]
    public void PickupAndModifiedRedropReuseNativeSlotWithoutAuthoredZeroFallback()
    {
        var checkpoint = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var source = checkpoint.Levels[0];
        var items = source.Items.ToArray();
        items[1] = items[1] with { CreateInfo = 0x8000, Flags = 0, Durability = 0, MaxDurability = 0, ArmorClass = 0 };
        checkpoint = checkpoint with { Levels = [source with { Items = items }] };
        var simulation = DiabloSimulationFactory.Instance.Create(Options(Profile(checkpoint)));
        var hero = Hero(2, 1);
        Assert.True(simulation.Execute(hero, new DiabloPickupWorldItemIntent(102), 1, [hero]).Succeeded);
        var owned = Assert.Single(hero.Inventory);
        Assert.Equal(0U, owned.State.CreateInfo);
        Assert.Equal((0U, 0, 0), (owned.State.Flags, owned.State.Durability, owned.State.ArmorClass));
        hero.Inventory[0] = owned with { State = owned.State with { Durability = 3, MaxDurability = 9, ArmorClass = 5 } };
        var drop = new DiabloDropItemIntent(new(DiabloPlayerItemLocation.Inventory, 0, StableItemId: 1002), 3, 1);
        Assert.True(simulation.Execute(hero, drop, 2, [hero]).Succeeded);
        Assert.Empty(hero.Inventory);
        var level = simulation.WorldState.CaptureCathedralItems()!.Levels[0];
        Assert.Equal(2, level.ActiveItemCount);
        Assert.Equal(2, level.SignedItemRefs[1 * 112 + 3]);
        Assert.Equal((3, 9, 5), (level.Items[1].Durability, level.Items[1].MaxDurability, level.Items[1].ArmorClass));
        Assert.Equal(0U, level.Items[1].Seed);
        Assert.Equal(0U, level.Items[1].CreateInfo);
        Assert.Equal(0, level.SignedItemRefs[2 * 112 + 2]);
        level.ValidateShape();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativePickupUpdatesPoolForGoldAndFullInventoryCursor(bool gold)
    {
        var simulation = DiabloSimulationFactory.Instance.Create(Options(Profile(
            AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint())));
        var hero = Hero(1, 2);
        if (!gold)
            for (var index = 0; index < hero.InventoryGrid.Count; index++) hero.InventoryGrid[index] = 0;
        var result = simulation.Execute(hero, new DiabloPickupWorldItemIntent(gold ? 101U : 102U), 1, [hero]);
        Assert.True(result.Succeeded, result.Failure?.ToString());
        var level = simulation.WorldState.CaptureCathedralItems()!.Levels[0];
        Assert.Equal(1, level.ActiveItemCount);
        Assert.Equal(gold ? 1 : 0, level.ActiveItemIds[0]);
        Assert.Equal(gold ? 0 : 1, level.ActiveItemIds[1]);
        if (gold) {
            Assert.Equal(0, level.Items[0].Value);
            Assert.Equal(4, level.Items[0].CursorGraphic);
            Assert.Equal(100, level.Items[0].IdentifiedValue);
            Assert.True(level.Items[0].StatFlag);
        } else {
            Assert.NotNull(hero.HeldItem);
        }
        level.ValidateShape();
    }

    [Fact]
    public void ColdCohortRestoreRetainsItemCapabilityAfterEveryMonsterHasDied()
    {
        var profile = Profile();
        var options = Options(profile, [Target(9, 7, 40, 40)]);
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(1000, 1000, 0, 0));
        runtime.Write("hero", hero => {
            hero.EntityId = 1; hero.LevelId = 1; hero.PositionX = 40; hero.PositionY = 39;
            Assert.True(simulation.Execute(hero, new DiabloAttackIntent(9), 0, [hero]).Succeeded);
        });
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(4, captured.FormatVersion);
        Assert.Empty(captured.CombatTargets);
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(options, captured);
        var restored = candidate.CreateRuntime();
        Assert.Equal(JsonSerializer.Serialize(captured), JsonSerializer.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        runtime.AdvanceTo(1); restored.AdvanceTo(1);
        Assert.Equal(JsonSerializer.Serialize(simulation.CaptureCohortState(runtime)),
            JsonSerializer.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        Assert.NotSame(simulation.WorldState.WorldItems, candidate.Simulation.WorldState.WorldItems);
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.CreateCohortCandidate(
            Options(profile with { GenerationFingerprint = new string('C', 64) }), captured));
    }

    [Fact]
    public void RawProjectionTamperingRejectsCaptureAndDeathBeforeHitPointMutation()
    {
        var target = Target(9, 7, 40, 40);
        var simulation = DiabloSimulationFactory.Instance.Create(Options(Profile(), [target]));
        var ghostState = Options(Profile()).Items!.Definitions.Single(item => item.ItemId == 1002).Generate(1);
        simulation.WorldState.WorldItems.Add(999, new(999, 1, 1, 1, 1, 999, ghostState));
        Assert.Throws<InvalidDataException>(() => simulation.WorldState.CaptureCathedralItems());
        var hero = Hero(40, 39);
        Assert.Throws<InvalidDataException>(() => simulation.Execute(hero, new DiabloAttackIntent(9), 1, [hero]));
        Assert.Equal(1, target.HitPoints);
    }

    [Fact]
    public void MissingProfileAndUnsupportedLiveModeCannotAdoptItemCheckpoint()
    {
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeCathedralItemRestore = AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint(),
        }));
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeCathedralItems = Profile(), NativeMonsterMetadataFingerprint = Fingerprint,
            GameMode = DiabloGameMode.Multiplayer,
        }));
    }

    internal static AuthoritativeCathedralItemRuntimeProfile Profile(DiabloCathedralItemCheckpoint? checkpoint = null)
        => AuthoritativeCathedralItemRuntimeTestData.Profile() with {
            InitialCheckpoint = checkpoint ?? AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint(),
            ItemCatalog = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog(),
            MonsterCatalog = AuthoritativeCathedralMonsterLootGeneratorTests.RuntimeCatalog(),
            ItemBindings = new Dictionary<int, AuthoritativeCathedralItemContentBinding> {
                [0] = new(0, 1001, "gold"), [1] = new(1, 1002, "sword"),
                [2] = new(2, 1003, "book", ItemActionKind: DiabloItemActionKind.ReadBook),
            },
            NativeSpellBindings = new Dictionary<int, int> { [0] = 0, [1] = 101 },
        };

    internal static DiabloSimulationOptions Options(AuthoritativeCathedralItemRuntimeProfile profile,
        IReadOnlyList<AuthoritativeCombatTarget>? targets = null) => new(new StoreCatalog(), DiabloModule.Instance.Rules) {
        FallbackWidth = 112, FallbackHeight = 112, RandomSeed = 71, CombatTargets = targets,
        CombatRules = new(100, 1, 10), NativeMonsterMetadataFingerprint = Fingerprint, NativeCathedralItems = profile,
        SuppressInitialWorldScaling = true,
        Items = new(profile.ItemBindings.Values.Select(binding => new AuthoritativeItemDefinition(
            binding.StableItemId, binding.NativeItemIndex == 0 ? 11 : 1, 999, 999, 7, 12, 9, 50, 50,
            binding.InventoryWidth, binding.InventoryHeight, true) {
                NativeItemIndex = binding.NativeItemIndex, StableSymbol = binding.StableSymbol,
                ItemActionKind = binding.ItemActionKind ?? DiabloItemActionKind.None,
            }).Append(new AuthoritativeItemDefinition(24, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 23, MiscId = 44,
            })),
        Spells = new([new AuthoritativeSpellDefinition(101, 1, 0, 0, 0, 0) { NativeSpellId = 1 }]),
    };

    private static AuthoritativePlayerState Hero(int x, int y)
    {
        var player = DiabloPlayerTemplate.CreateDefault(1000, 1000, 0, 0).CreatePlayer();
        player.EntityId = 1; player.LevelId = 1; player.PositionX = x; player.PositionY = y;
        return player;
    }

    internal static AuthoritativeCombatTarget Target(uint entity, int slot, int x, int y)
    {
        var target = new AuthoritativeCombatTarget(entity, x, y, 1, maxHitPoints: 1, monsterId: 90, levelId: 1);
        var raw = new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot, Type = 0, LevelType = 1, UniqueType = 255, Leader = 255,
            Tile = new(x, y), Future = new(x, y), Old = new(x, y), LightId = -1,
            HitPoints = 64, MaxHitPoints = 64, RndItemSeed = 2588, AiSeed = 0,
        };
        target.RestoreNativeInitialization(new(Fingerprint, raw, 0, 0, 1, 1,
            new(slot, 0, 255, entity, 90, 1, string.Empty), false), 2588, 0);
        return target;
    }
}
