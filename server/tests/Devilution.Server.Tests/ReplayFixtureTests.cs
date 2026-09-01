using Devilution.Data;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Replay;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ReplayFixtureTests
{
    [Fact]
    public void ProjectionOnlyCheckpointMarkerLoadsWithoutHashValidation()
    {
        const string json = """
            {
              "format_version": 1,
              "fixture_id": "gameplay/projection-only",
              "protocol_schema_version": "0.1.0",
              "tick_rate_hz": 20,
              "rng_seed": 1,
              "initial_state": {
                "player": "A",
                "gold": 0,
                "experience": 0,
                "life": 1,
                "mana": 1
              },
              "commands": [{
                "client_sequence": 1,
                "target_tick": 1,
                "server_receipt_sequence": 1,
                "kind": "Move",
                "payload": {
                  "direction_x": 0,
                  "direction_y": 0
                }
              }],
              "checkpoints": [{
                "tick": 1,
                "state_sha256": "timeline-only"
              }]
            }
            """;

        var fixture = ReplayFixtureLoader.Load(json);

        var checkpoint = Assert.Single(fixture.Checkpoints);
        Assert.Equal(1UL, checkpoint.Tick);
        Assert.Equal("timeline-only", checkpoint.StateSha256);
    }

    [Fact]
    public void BasicBuyFixtureLoadsAndSortsByAuthoritativeOrder()
    {
        var fixture = LoadFixture();

        Assert.Equal("stores/basic-buy", fixture.FixtureId);
        Assert.Equal(305419896UL, fixture.RngSeed);
        Assert.Equal([1UL, 2UL], fixture.OrderedCommands.Select(command => command.ClientSequence));
        Assert.Equal("OpenStore", fixture.OrderedCommands[0].Kind);
        Assert.Equal("BuyItem", fixture.OrderedCommands[1].Kind);
        Assert.Equal(0U, fixture.OrderedCommands[1].StoreSlot);
    }

    [Fact]
    public void BasicBuyFixtureExecutesCommandsAndMatchesLegacyInitialHash()
    {
        var fixture = LoadFixture();
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75)]);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingRandomSeed: checked((uint)fixture.RngSeed));
        var commandServer = new AuthoritativeCommandServer(executor);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, commandServer);

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal([1UL, 2UL], result.Results.Select(command => command.ServerReceiptSequence));
        Assert.Equal(25U, Assert.Single(result.FinalSnapshot.Players).Gold);
        var checkpoint = Assert.Single(fixture.Checkpoints);
        Assert.Equal(0UL, checkpoint.Tick);
        Assert.Equal(checkpoint.StateSha256, LegacyReplayStateHasher.Compute(fixture));
        Assert.NotEqual(checkpoint.StateSha256, result.InitialSnapshot.StateSha256);
    }

    [Fact]
    public void TransactionParityFixtureExecutesPurchaseSaleAndManaRefill()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "transaction-parity.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { ItemType = 1, Value = 75, IdentifiedValue = 75, Durability = 1, MaxDurability = 1 })]);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingRandomSeed: checked((uint)fixture.RngSeed));
        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(33U, player.Gold);
        Assert.Equal(640, player.Mana);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void GoldTopologyFixtureMatchesNativeStorePileAndGridTransitions()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gold-topology-parity.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(
            0,
            42,
            100,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                Value = 100,
                IdentifiedValue = 100,
                Durability = 1,
                MaxDurability = 1,
            })]);
        var combatRules = new AuthoritativeCombatRules(10, 1, 100);
        var goldState = AuthoritativeItemState.Empty with {
            ItemType = combatRules.DeathGoldItemType,
            Value = checked((int)fixture.InitialState.Gold),
            IdentifiedValue = checked((int)fixture.InitialState.Gold),
            ItemIndex = combatRules.DeathGoldItemIndex,
            StatFlag = true,
        };
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[checked((int)fixture.InitialState.GoldStackCell)] = 0;
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingInventoryGrid: inventoryGrid,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingCombatRules: combatRules,
            startingInventory: [new OwnedStoreItem(
                0,
                0,
                fixture.InitialState.GoldStackSeed,
                fixture.InitialState.Gold,
                0,
                goldState)],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(15U, player.Gold);
        var gold = Assert.Single(player.Inventory);
        var expectedRandom = new DiabloRandom(checked((uint)fixture.RngSeed));
        Assert.Equal(unchecked((uint)expectedRandom.AdvanceRndSeed()), gold.ItemSeed);
        Assert.Equal(15U, gold.Price);
        Assert.Equal(0, player.InventoryGrid[39]);
        Assert.Equal(640, player.Mana);
        Assert.Equal(expectedRandom.State, result.FinalSnapshot.RandomState);
    }

    [Fact]
    public void GameplayFixtureExecutesMovementAndCombatTransitions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-movement-combat.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLifeMaximum: fixture.InitialState.Life,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 2, 0, 11, 2)],
            startingRandomSeed: checked((uint)fixture.RngSeed));
        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(1, player.PositionX);
        Assert.Equal(100U, player.Experience);
        Assert.Equal(40, player.Life);
    }

    [Fact]
    public void GameplaySpellFixtureExecutesDataDrivenHealingTransition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-spell-cast.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingLifeMaximum: 40,
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingMemorizedSpells: fixture.InitialState.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(5, player.Mana);
        Assert.Equal(40, player.Life);
    }

    [Fact]
    public void ChargedBoltFixtureMatchesNativeSpawnVectorAndRngState()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-charged-bolt.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadReplaySpells(fixture, "charged-bolt");
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingLifeMaximum: fixture.InitialState.Life,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(
                    fixture.InitialState.Magic,
                    fixture.InitialState.Magic),
            },
            startingSpells: spells,
            startingMemorizedSpells: fixture.InitialState.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(14, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Equal(5, result.FinalSnapshot.Projectiles.Count);
        Assert.Equal([13, 11, 7, 11, 8], result.FinalSnapshot.Projectiles.Select(projectile => projectile.PathIndex));
        Assert.Equal([7, 8, 6, 6, 1], result.FinalSnapshot.Projectiles.Select(projectile => projectile.Damage));
        Assert.Equal(0x7162D42BU, result.FinalSnapshot.RandomState);
        Assert.Equal(
            Assert.Single(fixture.Checkpoints).StateSha256,
            result.FinalSnapshot.StateSha256);
    }

    [Fact]
    public void ChargedBoltResolutionFixtureAdvancesCheckpointOnlyTicks()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-charged-bolt-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadReplaySpells(fixture, "charged-bolt");
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            lightningResistance: fixture.InitialState.CombatTargetLightningResistance,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel,
            moving: fixture.InitialState.CombatTargetMoving,
            futurePositionX: fixture.InitialState.CombatTargetFuturePositionX,
            futurePositionY: fixture.InitialState.CombatTargetFuturePositionY,
            petrified: fixture.InitialState.CombatTargetPetrified,
            stoneCurseImmune: fixture.InitialState.CombatTargetStoneCurseImmune,
            demon: fixture.InitialState.CombatTargetDemon);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingLifeMaximum: fixture.InitialState.Life,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(
                    fixture.InitialState.Magic,
                    fixture.InitialState.Magic),
            },
            startingCombatTargets: [target],
            startingSpells: spells,
            startingMemorizedSpells: fixture.InitialState.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(5UL, result.FinalSnapshot.Tick);
        Assert.Equal(2, result.Checkpoints.Count);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal(27, monster.HitPoints);
        Assert.Equal(1696, monster.HitPointsQ6);
        Assert.Equal(0x80DD0724U, result.FinalSnapshot.RandomState);
        Assert.Equal(4, result.FinalSnapshot.Projectiles.Count);
        Assert.Equal(
            2,
            result.FinalSnapshot.Projectiles.Count(projectile =>
                projectile.Phase == (int)AuthoritativeProjectilePhase.Impact));
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void FireWallResolutionFixtureMatchesValidatedBaseContentAndNativeLifecycle()
    {
        var fixture = LoadReplayFixture("gameplay-fire-wall-resolution.json");
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingMemorizedSpells: fixture.InitialState.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal(8UL, result.FinalSnapshot.Tick);
        Assert.DoesNotContain(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.Behavior == (int)AuthoritativeProjectileBehavior.FireWallController);
        Assert.NotEmpty(result.FinalSnapshot.Projectiles);
        Assert.All(
            result.FinalSnapshot.Projectiles,
            projectile => Assert.Equal(
                (int)AuthoritativeProjectileBehavior.FireWallSegment,
                projectile.Behavior));
        Assert.Contains(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.PositionX == 14 && projectile.PositionY == 10);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void LightningWallResolutionFixtureMatchesNativeControllerAndSegmentLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-lightning-wall-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal(8UL, result.FinalSnapshot.Tick);
        Assert.DoesNotContain(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.Behavior == (int)AuthoritativeProjectileBehavior.LightningWallController);
        Assert.NotEmpty(result.FinalSnapshot.Projectiles);
        Assert.All(
            result.FinalSnapshot.Projectiles,
            projectile => {
                Assert.Equal((int)AuthoritativeProjectileBehavior.FireWallSegment, projectile.Behavior);
                Assert.Equal((int)AuthoritativeDamageType.Lightning, projectile.DamageType);
                Assert.InRange(projectile.RemainingTicks, 248U, 253U);
            });
    }

    [Fact]
    public void RingOfFireResolutionFixtureMatchesNativeCrawlAndWallSegments()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-ring-of-fire-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal(8UL, result.FinalSnapshot.Tick);
        Assert.Equal(24, result.FinalSnapshot.Projectiles.Count);
        Assert.All(
            result.FinalSnapshot.Projectiles,
            projectile => {
                Assert.Equal((int)AuthoritativeProjectileBehavior.FireWallSegment, projectile.Behavior);
                Assert.Equal((int)AuthoritativeDamageType.Fire, projectile.DamageType);
                Assert.True(projectile.RemainingTicks > 0);
            });
    }

    [Fact]
    public void NovaRadialFanFixtureMatchesNativeControllerAndBallAdmission()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-nova-radial-fan-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(36, result.FinalSnapshot.Projectiles.Count);
        Assert.All(
            result.FinalSnapshot.Projectiles,
            projectile => {
                Assert.Equal((int)AuthoritativeProjectileBehavior.NovaBall, projectile.Behavior);
                Assert.Equal((int)AuthoritativeDamageType.Lightning, projectile.DamageType);
                Assert.Equal(254U, projectile.RemainingTicks);
                Assert.Equal(16, projectile.Speed);
            });
    }

    [Fact]
    public void FlameWaveFixtureMatchesNativeBranchAdmissionAndMovement()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-flame-wave-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(9, result.FinalSnapshot.Projectiles.Count);
        Assert.All(
            result.FinalSnapshot.Projectiles,
            projectile => {
                Assert.Equal((int)AuthoritativeProjectileBehavior.FlameWaveSegment, projectile.Behavior);
                Assert.Equal((int)AuthoritativeDamageType.Fire, projectile.DamageType);
                Assert.Equal(254U, projectile.RemainingTicks);
                Assert.Equal(16, projectile.Speed);
            });
    }

    [Fact]
    public void ChainLightningFixtureMatchesNativeFanOutAndImpactResolution()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-chain-lightning-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.LevelId,
            lightningResistance: fixture.InitialState.CombatTargetLightningResistance,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel,
            petrified: fixture.InitialState.CombatTargetPetrified);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingCombatTargets: [target],
            startingSpells: spells,
            startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(2, result.FinalSnapshot.Projectiles.Count(projectile =>
            projectile.Behavior == (int)AuthoritativeProjectileBehavior.ChainLightningBolt));
        Assert.Equal(2, result.FinalSnapshot.Projectiles.Count(projectile =>
            projectile.Behavior == (int)AuthoritativeProjectileBehavior.LightningSegment));
        Assert.All(
            result.FinalSnapshot.Projectiles.Where(projectile =>
                projectile.Behavior == (int)AuthoritativeProjectileBehavior.ChainLightningBolt),
            projectile => {
                Assert.Equal(254U, projectile.RemainingTicks);
                Assert.Equal(32, projectile.Speed);
            });
    }

    [Fact]
    public void ImmolationRadialFanFixtureMatchesNativeControllerAndFireballs()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-immolation-radial-fan-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(36, result.FinalSnapshot.Projectiles.Count);
        Assert.All(
            result.FinalSnapshot.Projectiles,
            projectile => {
                Assert.Equal((int)AuthoritativeProjectileBehavior.Fireball, projectile.Behavior);
                Assert.Equal((int)AuthoritativeDamageType.Fire, projectile.DamageType);
                Assert.Equal(255U, projectile.RemainingTicks);
                Assert.Equal(16, projectile.Speed);
                Assert.Equal(1, projectile.ImpactLifetime);
            });
    }

    [Fact]
    public void ElementalFixtureMatchesNativeRetargetingProjectileLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-elemental-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        Assert.Equal((int)AuthoritativeProjectileBehavior.Elemental, projectile.Behavior);
        Assert.Equal((int)AuthoritativeDamageType.Fire, projectile.DamageType);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(16, projectile.Speed);
        Assert.Equal(14, projectile.ImpactLifetime);
    }

    [Fact]
    public void GuardianFixtureMatchesNativeTurretAndFireboltAdmission()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-guardian-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            fireResistance: initial.CombatTargetFireResistance,
            monsterLevel: initial.CombatTargetMonsterLevel,
            petrified: initial.CombatTargetPetrified);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingSpells: spells,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingCombatTargets: [target],
            startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(15UL, result.FinalSnapshot.Tick);
        var projectiles = result.FinalSnapshot.Projectiles;
        Assert.Contains(
            projectiles,
            projectile => projectile.Behavior == (int)AuthoritativeProjectileBehavior.GuardianController);
        var bolt = Assert.Single(
            projectiles,
            projectile => projectile.Behavior == (int)AuthoritativeProjectileBehavior.Firebolt);
        Assert.Equal(16, bolt.Speed);
        Assert.Equal(255U, bolt.RemainingTicks);
        Assert.Equal(1, bolt.ImpactLifetime);
    }

    [Fact]
    public void RageFixtureMatchesNativeActiveCooldownAndExpiryDamage()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-rage-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingAbilitySpellId: initial.AbilitySpellId,
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(495UL, result.FinalSnapshot.Tick);
        Assert.Equal(6394, Assert.Single(result.FinalSnapshot.Players).Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
    }

    [Fact]
    public void InfernoFixtureMatchesNativeControllerAndPersistentSegments()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-inferno-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingWorld: world,
            startingAbilitySpellId: initial.AbilitySpellId,
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6UL, result.FinalSnapshot.Tick);
        var projectiles = result.FinalSnapshot.Projectiles;
        Assert.Equal(3, projectiles.Count);
        Assert.DoesNotContain(
            projectiles,
            projectile => projectile.Behavior == (int)AuthoritativeProjectileBehavior.InfernoController);
        Assert.All(
            projectiles,
            projectile => {
                Assert.Equal((int)AuthoritativeProjectileBehavior.InfernoSegment, projectile.Behavior);
                Assert.True(projectile.Damage > 0);
            });
        Assert.Equal([15U, 18U, 20U], projectiles.Select(projectile => projectile.RemainingTicks));
    }

    [Fact]
    public void ApocalypseFixtureMatchesNativeRowMajorScanAndBoomLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-apocalypse-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterLevel: initial.CombatTargetMonsterLevel,
            petrified: initial.CombatTargetPetrified);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingSpells: spells,
            startingAbilitySpellId: initial.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(3UL, result.FinalSnapshot.Tick);
        var boom = Assert.Single(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.Behavior == (int)AuthoritativeProjectileBehavior.ApocalypseBoom);
        Assert.Equal(4, boom.PositionX);
        Assert.Equal(2, boom.PositionY);
        Assert.Equal(13U, boom.RemainingTicks);
        Assert.True(boom.ApocalypseHit);
        Assert.True(
            Assert.Single(result.FinalSnapshot.Monsters).HitPoints
                < initial.CombatTargetHitPoints);
    }

    [Fact]
    public void MagiFixtureMatchesNativeFullManaRestoration()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-magi-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingAbilitySpellId: initial.AbilitySpellId,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(initial.ManaMaximum, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void ManaFixtureMatchesNativeSeededClassScaledRestoration()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-mana-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var repositoryRoot = FindRepositoryRoot();
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(repositoryRoot, "server", "content", "base", "classes.tsv")));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingPlayerClasses: classes,
            startingAbilitySpellId: initial.AbilitySpellId,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(3208, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void ReflectFixtureMatchesNativeLevelScaledChargeCreation()
    {
        var fixture = LoadReplayFixture("gameplay-reflect-resolution.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingAbilitySpellId: initial.AbilitySpellId,
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6U, Assert.Single(result.FinalSnapshot.Players).ReflectCharges);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void ReflectRetaliationFixtureMatchesNativeChargeConsumptionAndDamage()
    {
        var fixture = LoadReplayFixture("gameplay-reflect-retaliation.json");
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            aggroRange: initial.CombatTargetAggroRange);
        var spells = LoadBaseSpells(fixture);
        var stores = new StoreCatalog();
        stores.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            stores,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingReflectCharges: initial.ReflectCharges,
            startingCombatTargets: [target],
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(12, player.Life);
        Assert.Equal(0U, player.ReflectCharges);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal(18, monster.HitPoints);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void EtherealizeFixtureMatchesNativePermanentStatusAndDamageImmunity()
    {
        var fixture = LoadReplayFixture("gameplay-etherealize-resolution.json");
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            aggroRange: initial.CombatTargetAggroRange);
        var spells = LoadBaseSpells(fixture);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingSpells: spells,
            startingAbilitySpellId: initial.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        var status = Assert.Single(player.StatusEffects);
        Assert.Equal(53U, status.EffectId);
        Assert.Equal(uint.MaxValue, status.RemainingTicks);
        Assert.Equal((int)AuthoritativeStatusEffectBehavior.Etherealize, (int)status.Behavior);
        Assert.Equal(initial.Life, player.Life);
        Assert.Equal(initial.CombatTargetHitPoints, Assert.Single(result.FinalSnapshot.Monsters).HitPoints);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void BerserkFixtureMatchesNativeDamageMutationAndRecastRejection()
    {
        var fixture = LoadReplayFixture("gameplay-berserk-resolution.json");
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            minDamageSpecial: initial.CombatTargetMinDamageSpecial,
            maxDamageSpecial: initial.CombatTargetMaxDamageSpecial,
            magicResistance: initial.CombatTargetMagicResistance);
        var spells = LoadBaseSpells(fixture);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Rejected],
            result.Results.Select(command => command.Status));
        Assert.Equal(
            [CommandRejectReason.InvalidTarget],
            result.Results.Where(command => command.Status == CommandStatus.Rejected)
                .Select(command => command.RejectReason));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.True(monster.IsBerserked);
        Assert.Equal(14, monster.MinDamage);
        Assert.Equal(26, monster.MaxDamage);
        Assert.Equal(39, monster.MinDamageSpecial);
        Assert.Equal(52, monster.MaxDamageSpecial);
        Assert.Equal(26, monster.AttackDamage);
        Assert.Equal(2440, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void ChargedBoltPvpFixtureDamagesTheSecondAuthoritativeParticipant()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-charged-bolt-pvp.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadReplaySpells(fixture, "charged-bolt");
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        Assert.True(executor.GetPlayerState(secondary.Player).Life < secondary.Life);
        Assert.Equal(
            [1U, secondary.EntityId],
            result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(
            executor.GetPlayerState(secondary.Player).Life,
            result.FinalSnapshot.Players[1].Life);
        Assert.Equal(5UL, result.FinalSnapshot.Tick);
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void FireboltPvpFixtureDamagesTheSecondAuthoritativeParticipant()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-firebolt-pvp.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var victim = executor.GetPlayerState(secondary.Player);
        Assert.True(victim.Life < secondary.Life);
        Assert.Equal(
            [1U, secondary.EntityId],
            result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(5UL, result.FinalSnapshot.Tick);
        Assert.Equal(0x3F10CD47U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void FireballPvpFixtureDamagesTheSecondAuthoritativeParticipant()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-fireball-pvp.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var victim = executor.GetPlayerState(secondary.Player);
        Assert.Equal(1123, victim.Life);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(17UL, result.FinalSnapshot.Tick);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void BoneSpiritPvpFixtureAppliesNativeCurrentLifeHalfDamage()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-bone-spirit-pvp.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadBaseSpells(fixture);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var victim = executor.GetPlayerState(secondary.Player);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(1667, victim.Life);
        Assert.Equal(896, result.FinalSnapshot.Players[0].Life);
        Assert.Equal(10UL, result.FinalSnapshot.Tick);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void BoneSpiritHomingFixtureRetargetsAndAppliesResistantMonsterDamage()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-bone-spirit-homing.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadReplaySpells(fixture, "projectile-variants");
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            magicResistance: initial.CombatTargetMagicResistance,
            monsterLevel: initial.CombatTargetMonsterLevel,
            petrified: initial.CombatTargetPetrified);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingCombatTargets: [target],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal(38, monster.HitPoints);
        Assert.Equal(2416, monster.HitPointsQ6);
        Assert.Equal(10UL, result.FinalSnapshot.Tick);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        Assert.Equal(9U, projectile.LastCollisionTargetEntityId);
        Assert.Equal(6U, projectile.RemainingTicks);
        Assert.Equal((int)AuthoritativeProjectileBehavior.BoneSpirit, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(4, projectile.ResistantDamageDivisor);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void HellfireFireballBlockFixtureRetainsImpactAndResolvesSplash()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-fireball-hellfire-block.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadReplaySpells(fixture, "projectile-variants");
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var victim = executor.GetPlayerState(secondary.Player);
        Assert.Equal(1987, victim.Life);
        Assert.Equal(DiabloGameVariant.Hellfire, fixture.GameVariant);
        Assert.Equal(17UL, result.FinalSnapshot.Tick);
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void HellfireChargedBoltBlockFixturePreservesLifeAndConsumesVariantRng()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-charged-bolt-hellfire-block.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) {
                Range = 4,
                DamageType = AuthoritativeDamageType.Lightning,
                ProjectileSpeed = 8,
                ProjectileLifetime = 256,
                ProjectileCount = 4,
                ProjectileCountLevelDivisor = 2,
                ProjectileBehavior = AuthoritativeProjectileBehavior.ChargedBolt,
                RandomMagicDamageDivisor = 4,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 8,
            },
        ]);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var victim = executor.GetPlayerState(secondary.Player);
        Assert.True(victim.Life < secondary.Life);
        Assert.Equal(DiabloPlayerCombatMode.GotHit, victim.CombatMode);
        Assert.Equal(
            [1U, secondary.EntityId],
            result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal((int)victim.CombatMode, (int)result.FinalSnapshot.Players[1].CombatMode);
        Assert.Equal(DiabloGameVariant.Hellfire, fixture.GameVariant);
        Assert.Equal(5UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void HellfireFireboltBlockFixtureRetainsImpactAndPreservesLife()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-firebolt-hellfire-block.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadReplaySpells(fixture, "projectile-variants");
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var victim = executor.GetPlayerState(secondary.Player);
        Assert.Equal(secondary.Life, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);
        Assert.Equal(5UL, result.FinalSnapshot.Tick);
        Assert.Equal(3, result.Checkpoints.Count);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void ChargedBoltMovingOccupancyFixtureHitsPetrifiedFutureCell()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-charged-bolt-moving-occupancy.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            lightningResistance: initial.CombatTargetLightningResistance,
            monsterLevel: initial.CombatTargetMonsterLevel,
            moving: initial.CombatTargetMoving,
            futurePositionX: initial.CombatTargetFuturePositionX,
            futurePositionY: initial.CombatTargetFuturePositionY,
            petrified: initial.CombatTargetPetrified,
            stoneCurseImmune: initial.CombatTargetStoneCurseImmune,
            demon: initial.CombatTargetDemon);
        var spells = LoadReplaySpells(fixture, "charged-bolt");
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingCombatTargets: [target],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.True(monster.Moving);
        Assert.True(monster.Petrified);
        Assert.Equal(1, monster.FuturePositionX);
        Assert.Equal(1, monster.FuturePositionY);
        Assert.True(monster.HitPoints < initial.CombatTargetHitPoints);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void ChargedBoltMovingOccupancyFixtureRejectsNonAdjacentFutureCell()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-charged-bolt-moving-occupancy.json");
        var json = File.ReadAllText(path).Replace(
            "\"combat_target_future_position_x\": 1",
            "\"combat_target_future_position_x\": 4",
            StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid combat-target occupancy state.",
            exception.Message);
    }

    [Fact]
    public void ChargedBoltObjectCollisionFixtureBreaksServerOwnedObject()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-charged-bolt-object-collision.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = LoadReplaySpells(fixture, "charged-bolt");
        var initial = fixture.InitialState;
        var @object = new AuthoritativeWorldObject(
            initial.ObjectEntityId,
            initial.ObjectId,
            initial.LevelId,
            initial.ObjectPositionX,
            initial.ObjectPositionY,
            missilePassable: initial.ObjectMissilePassable,
            breakable: initial.ObjectBreakable,
            destroyed: initial.ObjectDestroyed);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingLifeMaximum: initial.Life,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingObjects: [@object],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.True(@object.Destroyed);
        Assert.Equal(3, result.FinalSnapshot.Projectiles.Count);
        Assert.Equal(0x7162D42BU, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayPortalFixtureExecutesLevelTransition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-portal-transition.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 40, 40, []));
        world.AddLevel(new AuthoritativeLevel(2, 40, 40, []));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorld: world,
            startingPortals: [new AuthoritativePortal(5, 1, 1, 1, 2, 3, 4)],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(3, player.PositionX);
        Assert.Equal(4, player.PositionY);
    }

    [Fact]
    public void GameplayMultiLevelFixtureRetainsEntitiesOnTheirOriginalLevel()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-multi-level-occupancy.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 40, 40, []));
        world.AddLevel(new AuthoritativeLevel(2, 40, 40, []));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorld: world,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                2,
                0,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                AuthoritativeItemState.Empty)],
            startingObjects: [new AuthoritativeWorldObject(
                fixture.InitialState.ObjectEntityId,
                fixture.InitialState.ObjectId,
                fixture.InitialState.LevelId,
                fixture.InitialState.ObjectPositionX,
                fixture.InitialState.ObjectPositionY)],
            startingPortals: [new AuthoritativePortal(5, 1, 0, 0, 2, 3, 4)],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(2U, Assert.Single(result.FinalSnapshot.Players).LevelId);
        Assert.Equal(20U, Assert.Single(result.FinalSnapshot.WorldItems).EntityId);
        Assert.Equal(1U, Assert.Single(result.FinalSnapshot.WorldItems).LevelId);
        Assert.Equal(30U, Assert.Single(result.FinalSnapshot.Objects).EntityId);
        Assert.Equal(1U, Assert.Single(result.FinalSnapshot.Objects).LevelId);
    }

    [Fact]
    public void GameplayWorldItemFixtureExecutesPickupTransition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-world-item-pickup.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var item = new AuthoritativeWorldItem(
            fixture.InitialState.WorldItemEntityId,
            fixture.InitialState.LevelId,
            fixture.InitialState.PositionX + 1,
            fixture.InitialState.PositionY,
            fixture.InitialState.WorldItemSeed,
            fixture.InitialState.WorldItemPrice,
            AuthoritativeItemState.Empty);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorldItems: [item],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Empty(result.FinalSnapshot.WorldItems);
        Assert.Equal(fixture.InitialState.WorldItemSeed, Assert.Single(result.FinalSnapshot.Players[0].Inventory).ItemSeed);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void GameplayPrimaryHeldItemPickupOnOccupiedTileIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-primary-held-item-pickup-rejection.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var worldItemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9920,
            StatFlag = true,
        };
        var heldItem = new OwnedStoreItem(
            0,
            0,
            9010,
            25,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemIndex = 1,
                StableItemId = 9921,
                StatFlag = true,
            });
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingHeldItem: heldItem,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                fixture.InitialState.PositionX,
                fixture.InitialState.PositionY + 1,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                worldItemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Rejected, result.Results[1].Status);
        Assert.Equal(CommandRejectReason.NotAllowed, result.Results[1].RejectReason);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
        Assert.Equal(fixture.Checkpoints[1].StateSha256, result.Checkpoints[1].ActualStateSha256);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(9010U, player.HeldItem?.ItemSeed);
        Assert.Single(result.FinalSnapshot.WorldItems);
    }

    [Fact]
    public void GameplayPrimaryHeldItemPickupReplacesCursorAtPickupOrigin()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-primary-held-item-pickup-replacement.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var worldItemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9920,
            StatFlag = true,
        };
        var heldItem = new OwnedStoreItem(
            0,
            0,
            9010,
            25,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemIndex = 1,
                StableItemId = 9921,
                StatFlag = true,
            });
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingHeldItem: heldItem,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                fixture.InitialState.PositionX,
                fixture.InitialState.PositionY + 1,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                worldItemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var player = Assert.Single(result.FinalSnapshot.Players);
        var worldItem = Assert.Single(result.FinalSnapshot.WorldItems);
        var inventoryItem = Assert.Single(player.Inventory);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Null(player.HeldItem);
        Assert.Equal(fixture.InitialState.WorldItemSeed, inventoryItem.ItemSeed);
        Assert.Equal(9920U, inventoryItem.State.StableItemId);
        Assert.Equal(1U, inventoryItem.PurchasedAtTick);
        Assert.Equal(0x40000000U, worldItem.EntityId);
        Assert.Equal(9010U, worldItem.ItemSeed);
        Assert.Equal(9921U, worldItem.State.StableItemId);
        Assert.Equal(fixture.InitialState.PositionX, worldItem.PositionX);
        Assert.Equal(fixture.InitialState.PositionY, worldItem.PositionY);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void GameplayStableItemDropFixtureUsesStableInventoryIdentity()
    {
        var result = ExecuteStableInventoryDropFixture("gameplay-stable-item-drop.json");

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Empty(Assert.Single(result.FinalSnapshot.Players).Inventory);
        var dropped = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(123U, dropped.ItemSeed);
        Assert.Equal(7001U, dropped.State.StableItemId);
    }

    [Fact]
    public void GameplayStableItemDropRetryFixtureRejectsStaleStableIdentity()
    {
        var result = ExecuteStableInventoryDropFixture("gameplay-stable-item-drop-retry.json");
        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Rejected],
            result.Results.Select(command => command.Status));
        Assert.Empty(Assert.Single(result.FinalSnapshot.Players).Inventory);
        var dropped = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(124U, dropped.ItemSeed);
        Assert.Equal(7004U, dropped.State.StableItemId);
    }

    [Fact]
    public void GameplayStableBeltDropFixtureUsesStableBeltIdentity()
    {
        var result = ExecuteStableSlottedDropFixture("gameplay-stable-belt-drop.json");
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Empty(player.Belt);
        var dropped = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(321U, dropped.ItemSeed);
        Assert.Equal(7002U, dropped.State.StableItemId);
    }

    [Fact]
    public void GameplayStableBeltDropFixtureRejectsNonAdjacentDestination()
    {
        var result = ExecuteStableSlottedDropFixture("gameplay-stable-belt-drop-invalid-target.json");
        Assert.Equal(CommandStatus.Rejected, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(7005U, Assert.Single(player.Belt).State.StableItemId);
        Assert.Empty(result.FinalSnapshot.WorldItems);
    }

    [Fact]
    public void GameplayStableEquipmentDropFixtureUsesStableEquipmentIdentity()
    {
        var result = ExecuteStableSlottedDropFixture("gameplay-stable-equipment-drop.json");
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Empty(player.Equipment);
        var dropped = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(654U, dropped.ItemSeed);
        Assert.Equal(7003U, dropped.State.StableItemId);
    }

    private static ReplayExecutionResult ExecuteStableSlottedDropFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingEquipment: fixture.InitialState.Equipment
                .Select(item => item.ToEquippedStoreItem())
                .ToArray(),
            startingBelt: fixture.InitialState.Belt
                .Select(item => item.ToBeltStoreItem())
                .ToArray(),
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorld: world,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        return ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
    }

    private static ReplayExecutionResult ExecuteStableInventoryDropFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var goldState = AuthoritativeItemState.Empty with {
            ItemType = 11,
            ItemIndex = 0,
            Value = checked((int)fixture.InitialState.Gold),
            IdentifiedValue = checked((int)fixture.InitialState.Gold),
            StatFlag = true,
            StableItemId = fixture.InitialState.GoldStackStableItemId,
        };
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[checked((int)fixture.InitialState.GoldStackCell)] = 0;
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(fixture.InitialState.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingInventoryGrid: inventoryGrid,
            startingInventory: [new OwnedStoreItem(
                0,
                0,
                fixture.InitialState.GoldStackSeed,
                fixture.InitialState.Gold,
                0,
                goldState)],
            startingWorld: world,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        return ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
    }

    [Fact]
    public void GameplayGoldOverflowFixtureCarriesAndDropsNativeRemainder()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-gold-pickup-overflow.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var goldState = AuthoritativeItemState.Empty with {
            ItemType = rules.DeathGoldItemType,
            ItemIndex = rules.DeathGoldItemIndex,
            Value = checked((int)fixture.InitialState.Gold),
            IdentifiedValue = checked((int)fixture.InitialState.Gold),
            StatFlag = true,
        };
        var fillerState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            StatFlag = true,
        };
        var inventory = new List<OwnedStoreItem> {
            new(
                0,
                0,
                fixture.InitialState.GoldStackSeed,
                fixture.InitialState.Gold,
                0,
                goldState),
        };
        for (var index = 0U; index < fixture.InitialState.InventoryFillerCount; index++) {
            inventory.Add(new OwnedStoreItem(
                0,
                0,
                200U + index,
                1,
                0,
                fillerState));
        }
        var worldGoldState = goldState with {
            Value = checked((int)fixture.InitialState.WorldItemPrice),
            IdentifiedValue = checked((int)fixture.InitialState.WorldItemPrice),
        };
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingInventoryGrid: Enumerable.Range(0, 40).ToArray(),
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatRules: rules,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                fixture.InitialState.PositionX + 1,
                fixture.InitialState.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                worldGoldState)],
            startingInventory: inventory,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal(2, result.Checkpoints.Count);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(5000U, player.Gold);
        Assert.Equal(5000U, player.Inventory[0].Price);
        Assert.Null(player.HeldItem);
        var dropped = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(0x40000000U, dropped.EntityId);
        Assert.Equal(42U, dropped.ItemSeed);
        Assert.Equal(65U, dropped.Price);
        Assert.Equal(0U, result.FinalSnapshot.RandomState);
    }

    [Fact]
    public void MultiplayerDeathSuppressionFixtureKeepsGoldAndRngState()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-death-suppression.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var goldState = AuthoritativeItemState.Empty with {
            ItemType = rules.DeathGoldItemType,
            ItemIndex = rules.DeathGoldItemIndex,
            Value = checked((int)initial.Gold),
            IdentifiedValue = checked((int)initial.Gold),
            StatFlag = true,
        };
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[checked((int)initial.GoldStackCell)] = 0;
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            initial.LevelId,
            40,
            40,
            suppressMultiplayerDeathDrops:
                initial.SuppressMultiplayerDeathDrops));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingInventoryGrid: inventoryGrid,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                levelId: initial.CombatTargetLevelId,
                attackDamage: initial.CombatTargetAttackDamage,
                aggroRange: initial.CombatTargetAggroRange)],
            startingWorld: geometry,
            startingCombatRules: rules,
            startingInventory: [new OwnedStoreItem(
                0,
                0,
                initial.GoldStackSeed,
                initial.Gold,
                0,
                goldState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(DiabloGameMode.Multiplayer, fixture.GameMode);
        Assert.True(initial.SuppressMultiplayerDeathDrops);
        Assert.Equal(CommandStatus.Rejected, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(PlayerCombatMode.Death, player.CombatMode);
        Assert.Equal(101U, player.Gold);
        Assert.Equal(101U, Assert.Single(player.Inventory).Price);
        Assert.Empty(result.FinalSnapshot.WorldItems);
        Assert.Equal(1U, result.FinalSnapshot.RandomState);
        Assert.True(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void GameplayObjectQuestFixtureExecutesAuthoritativeWorldTransitions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-object-quest.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingObjects: [new AuthoritativeWorldObject(
                fixture.InitialState.ObjectEntityId,
                fixture.InitialState.ObjectId,
                fixture.InitialState.LevelId,
                fixture.InitialState.ObjectPositionX,
                fixture.InitialState.ObjectPositionY)],
            startingQuests: [new AuthoritativeQuestState(
                fixture.InitialState.QuestId,
                fixture.InitialState.LevelId,
                fixture.InitialState.QuestRequiredProgress)],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.True(Assert.Single(result.FinalSnapshot.Objects).Activated);
        var quest = Assert.Single(result.FinalSnapshot.Quests);
        Assert.Equal(1U, quest.Progress);
        Assert.True(quest.Completed);
    }

    [Fact]
    public void GameplayQuestSelectionFixtureRejectsUnavailableQuestTransitions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-quest-selection.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        Assert.NotNull(fixture.QuestSelection);
        var selection = fixture.QuestSelection!;
        var selectedQuestIds = selection.SelectUnavailableQuestIds();
        Assert.Contains(fixture.InitialState.QuestId, selectedQuestIds);
        var quest = new AuthoritativeQuestState(
            fixture.InitialState.QuestId,
            fixture.InitialState.LevelId,
            fixture.InitialState.QuestRequiredProgress);
        var poolCatalog = new AuthoritativeQuestPoolCatalog(
            selection.Pools.SelectMany(pool => pool.QuestIds.Select(questId =>
                new AuthoritativeQuestPoolEntry(pool.PoolId, questId))));
        AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection([quest], poolCatalog, selection.Seed);
        Assert.True(quest.Unavailable);

        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingLevelId: fixture.InitialState.LevelId,
            startingQuests: [quest],
            startingRandomSeed: checked((uint)fixture.RngSeed));
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Rejected, CommandStatus.Rejected], result.Results.Select(command => command.Status));
        Assert.True(Assert.Single(result.FinalSnapshot.Quests).Unavailable);
        Assert.Equal(1U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayAutonomousRegenerationSoakMatchesNativeCheckpoints()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-autonomous-regen-soak.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(1000UL, result.Checkpoints[^1].Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
        Assert.Equal(fixture.Checkpoints[^1].StateSha256, result.FinalSnapshot.StateSha256);
        Assert.Equal(305419896U, result.FinalSnapshot.RandomState);
    }

    [Fact]
    public void GameplayMultiplayerAutonomousTickMatchesNativeParticipantHashes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-multiplayer-autonomous-tick.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        Assert.Equal("B", fixture.Commands[0].Player);
        Assert.Equal(string.Empty, fixture.Commands[1].Player);
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(DiabloGameMode.Multiplayer, fixture.GameMode);
        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal([1U, 8U], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(73, result.FinalSnapshot.Players[0].Life);
        Assert.Equal(80, result.FinalSnapshot.Players[1].Life);
        Assert.Equal(6, result.FinalSnapshot.Players[1].PositionX);
        Assert.Equal(6, result.FinalSnapshot.Players[1].PositionY);
        Assert.Equal(10UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryItemDropProjectsNativeParticipantState()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-item-drop.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var secondary = Assert.Single(
            result.FinalSnapshot.Players,
            player => player.EntityId == fixture.InitialState.SecondaryPlayer!.EntityId);
        Assert.Empty(secondary.Inventory);
        var worldItem = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(9001U, worldItem.ItemSeed);
        Assert.Equal(9901U, worldItem.State.StableItemId);
        Assert.Equal(4, worldItem.PositionX);
        Assert.Equal(5, worldItem.PositionY);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void WarpFixtureMatchesNativeLevelTransitionAndVisitedState()
    {
        var fixture = LoadReplayFixture("gameplay-warp-resolution.json");
        var initial = fixture.InitialState;
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 5, 5));
        world.AddLevel(new AuthoritativeLevel(initial.WarpTargetLevelId, 5, 5));
        world.AddWarpTarget(new AuthoritativeWarpTarget(
            initial.WarpId,
            initial.WarpSourceLevelId,
            initial.WarpTriggerX,
            initial.WarpTriggerY,
            initial.WarpTargetLevelId,
            initial.WarpTargetX,
            initial.WarpTargetY));
        var spells = LoadBaseSpells(fixture);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingWorld: world,
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(initial.WarpTargetLevelId, player.LevelId);
        Assert.Equal((initial.WarpTargetX, initial.WarpTargetY), (player.PositionX, player.PositionY));
        Assert.Equal(initial.Mana - 35 * 64, player.Mana);
        Assert.Equal(
            [initial.LevelId, initial.WarpTargetLevelId],
            player.VisitedLevelIds);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GolemFixtureMatchesNativeSummonState()
    {
        var fixture = LoadReplayFixture("gameplay-golem-resolution.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var golem = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal(initial.Mana - 94 * 64, player.Mana);
        Assert.Equal(0x80000000U, golem.EntityId);
        Assert.Equal(initial.LevelId, golem.LevelId);
        Assert.Equal(12, golem.PositionX);
        Assert.Equal(10, golem.PositionY);
        Assert.Equal(13354, golem.HitPoints);
        Assert.Equal(13354, golem.MaxHitPoints);
        Assert.Equal(854612, golem.HitPointsQ6);
        Assert.Equal(854612, golem.MaxHitPointsQ6);
        Assert.Equal(25, golem.ArmorClass);
        Assert.Equal(4, golem.MonsterLevel);
        Assert.Equal(20, golem.AttackDamage);
        Assert.Equal(12, golem.MinDamage);
        Assert.Equal(20, golem.MaxDamage);
        Assert.Equal(12, golem.MinDamageSpecial);
        Assert.Equal(20, golem.MaxDamageSpecial);
        Assert.Equal(1443805422U, golem.MonsterItemSeed);
        Assert.Equal(1058065735U, golem.MonsterAiSeed);
        Assert.True(golem.IsPlayerMinion);
        Assert.Equal(1U, golem.OwnerPlayerId);
        Assert.Equal(12, golem.DeathAnimationTicks);
        Assert.Equal(0, golem.DeathTicksRemaining);
        Assert.Equal("Golem", golem.NativeAi);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void JesterFireboltFixtureMatchesNativeDispatchState()
    {
        var fixture = LoadReplayFixture("gameplay-jester-firebolt-resolution.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        Assert.Equal(912, player.Mana);
        Assert.Equal(1U, projectile.SpellId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.Firebolt, (int)projectile.Behavior);
        Assert.Equal(0, (int)projectile.PositionX);
        Assert.Equal(0, (int)projectile.PositionY);
        Assert.Equal(4, (int)projectile.TargetX);
        Assert.Equal(0, (int)projectile.TargetY);
        Assert.Equal(13, (int)projectile.Damage);
        Assert.Equal(256U, (uint)projectile.RemainingTicks);
        Assert.Equal(22695478U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void TownPortalFixtureMatchesNativeDynamicPortalState()
    {
        var fixture = LoadReplayFixture("gameplay-town-portal-resolution.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 80, 80));
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 80, 80));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingWorld: world,
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var portal = Assert.Single(result.FinalSnapshot.Portals);
        Assert.Equal(1952, player.Mana);
        Assert.Equal(0x20000000U, portal.PortalId);
        Assert.Equal(initial.LevelId, portal.SourceLevelId);
        Assert.Equal(4, portal.SourcePositionX);
        Assert.Equal(2, portal.SourcePositionY);
        Assert.Equal(0U, portal.DestinationLevelId);
        Assert.Equal(57, portal.DestinationPositionX);
        Assert.Equal(40, portal.DestinationPositionY);
        Assert.Equal(100U, portal.RemainingTicks);
        Assert.Equal(1U, portal.OwnerEntityId);
        Assert.True(portal.Bidirectional);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void TelekinesisWorldItemFixtureMatchesNativePickupAndManaCost()
    {
        var fixture = LoadReplayFixture("gameplay-telekinesis-world-item.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 40, 40));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9916,
            Value = checked((int)initial.WorldItemPrice),
            IdentifiedValue = checked((int)initial.WorldItemPrice),
            StatFlag = true,
        };
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.Life,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorld: world,
            startingWorldItems: [new AuthoritativeWorldItem(
                initial.WorldItemEntityId,
                initial.LevelId,
                initial.PositionX + 1,
                initial.PositionY,
                initial.WorldItemSeed,
                initial.WorldItemPrice,
                itemState)],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(1168, player.Mana);
        Assert.Empty(result.FinalSnapshot.WorldItems);
        Assert.Equal(initial.WorldItemSeed, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void TelekinesisObjectFixtureMatchesNativeActivationAndManaCost()
    {
        var fixture = LoadReplayFixture("gameplay-telekinesis-object.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 40, 40));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.Life,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingWorld: world,
            startingObjects: [new AuthoritativeWorldObject(
                initial.ObjectEntityId,
                initial.ObjectId,
                initial.LevelId,
                initial.ObjectPositionX,
                initial.ObjectPositionY,
                effectKind: (AuthoritativeObjectEffectKind)initial.ObjectEffectKind,
                effectAmount: initial.ObjectEffectAmount)],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.True(Assert.Single(result.FinalSnapshot.Objects).Activated);
        Assert.Equal(25U, Assert.Single(result.FinalSnapshot.Players).Experience);
        Assert.Equal(1168, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void TelekinesisMonsterFixtureMatchesNativeKnockbackAndManaCost()
    {
        var fixture = LoadReplayFixture("gameplay-telekinesis-monster-knockback.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            futurePositionX: initial.CombatTargetFuturePositionX,
            futurePositionY: initial.CombatTargetFuturePositionY);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.Life,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal((2, 0), (monster.PositionX, monster.PositionY));
        Assert.False(monster.Moving);
        Assert.Equal(1168, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void TelekinesisInvalidTargetFixturePreservesWorldItemAndMana()
    {
        var fixture = LoadReplayFixture("gameplay-telekinesis-invalid-target.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 40, 40));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9916,
            StatFlag = true,
        };
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.Life,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorld: world,
            startingWorldItems: [new AuthoritativeWorldItem(
                initial.WorldItemEntityId,
                initial.LevelId,
                initial.PositionX + 1,
                initial.PositionY,
                initial.WorldItemSeed,
                initial.WorldItemPrice,
                itemState)],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var command = Assert.Single(result.Results);
        Assert.Equal(CommandStatus.Rejected, command.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, command.RejectReason);
        Assert.Equal(initial.Mana, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void TelekinesisInsufficientManaFixturePreservesWorldItemAndMana()
    {
        var fixture = LoadReplayFixture("gameplay-telekinesis-insufficient-mana.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 40, 40));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9916,
            StatFlag = true,
        };
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.Life,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorld: world,
            startingWorldItems: [new AuthoritativeWorldItem(
                initial.WorldItemEntityId,
                initial.LevelId,
                initial.PositionX + 1,
                initial.PositionY,
                initial.WorldItemSeed,
                initial.WorldItemPrice,
                itemState)],
            startingSpells: spells,
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var command = Assert.Single(result.Results);
        Assert.Equal(CommandStatus.Rejected, command.Status);
        Assert.Equal(CommandRejectReason.InsufficientResources, command.RejectReason);
        Assert.Equal(initial.Mana, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void TelekinesisUnmemorizedFixtureRejectsWithoutChangingWorldState()
    {
        var fixture = LoadReplayFixture("gameplay-telekinesis-unmemorized.json");
        var initial = fixture.InitialState;
        var spells = LoadBaseSpells(fixture);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(initial.LevelId, 40, 40));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9916,
            StatFlag = true,
        };
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.Life,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorld: world,
            startingWorldItems: [new AuthoritativeWorldItem(
                initial.WorldItemEntityId,
                initial.LevelId,
                initial.PositionX + 1,
                initial.PositionY,
                initial.WorldItemSeed,
                initial.WorldItemPrice,
                itemState)],
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var command = Assert.Single(result.Results);
        Assert.Equal(CommandStatus.Rejected, command.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, command.RejectReason);
        Assert.Equal(initial.Mana, Assert.Single(result.FinalSnapshot.Players).Mana);
        Assert.Empty(result.FinalSnapshot.Players.Single().Inventory);
        Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void CounselorDelayFixtureMatchesNativeState()
    {
        var fixture = LoadReplayFixture("gameplay-counselor-delay.json");
        var executor = CreateCounselorExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.False(monster.Moving);
        Assert.Equal(8, monster.CounselorDelayTicks);
        Assert.Equal(0, monster.CounselorRetreatTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void CounselorRetreatFixtureMatchesNativeMovementAndCountdown()
    {
        var fixture = LoadReplayFixture("gameplay-counselor-retreat.json");
        var executor = CreateCounselorExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal((3, 0), (monster.PositionX, monster.PositionY));
        Assert.True(monster.Moving);
        Assert.Equal((4, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Equal(1, monster.CounselorRetreatTicks);
        Assert.Equal(0, monster.CounselorDelayTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryItemPickupMatchesNativeParticipantHash()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-item-pickup.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9910,
            Value = checked((int)fixture.InitialState.WorldItemPrice),
            IdentifiedValue = checked((int)fixture.InitialState.WorldItemPrice),
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX - 1,
                secondary.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(2, result.Results.Count);
        Assert.Equal(CommandStatus.Accepted, result.Results[0].Status);
        Assert.Equal(CommandStatus.Rejected, result.Results[1].Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, result.Results[1].RejectReason);
        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        var item = Assert.Single(player.Inventory);
        Assert.Equal(fixture.InitialState.WorldItemSeed, item.ItemSeed);
        Assert.Equal(9910U, item.State.StableItemId);
        Assert.Equal(1U, item.PurchasedAtTick);
        Assert.Empty(result.FinalSnapshot.WorldItems);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryItemPickupWithExistingItemMatchesNativeParticipantHash()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-item-pickup-with-existing-item.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9911,
            Value = checked((int)fixture.InitialState.WorldItemPrice),
            IdentifiedValue = checked((int)fixture.InitialState.WorldItemPrice),
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX - 1,
                secondary.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        Assert.Equal(2, player.Inventory.Count);
        Assert.Equal(9003U, player.Inventory[0].ItemSeed);
        Assert.Equal(fixture.InitialState.WorldItemSeed, player.Inventory[1].ItemSeed);
        Assert.Equal(1U, player.Inventory[1].PurchasedAtTick);
        Assert.Empty(result.FinalSnapshot.WorldItems);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryItemPickupOverflowMatchesNativeParticipantHash()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-item-pickup-overflow.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9912,
            Value = checked((int)fixture.InitialState.WorldItemPrice),
            IdentifiedValue = checked((int)fixture.InitialState.WorldItemPrice),
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX - 1,
                secondary.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        Assert.Equal(40, player.Inventory.Count);
        Assert.Null(player.HeldItem);
        var worldItem = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(0x40000000U, worldItem.EntityId);
        Assert.Equal(fixture.InitialState.WorldItemSeed, worldItem.ItemSeed);
        Assert.Equal((5, 6), (worldItem.PositionX, worldItem.PositionY));
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryHeldItemPickupReplacesCursorAtPickupOrigin()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-held-item-pickup-replacement.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9920,
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX - 1,
                secondary.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        var inventoryItem = Assert.Single(player.Inventory);
        var worldItem = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Null(player.HeldItem);
        Assert.Equal(fixture.InitialState.WorldItemSeed, inventoryItem.ItemSeed);
        Assert.Equal(9920U, inventoryItem.State.StableItemId);
        Assert.Equal(1U, inventoryItem.PurchasedAtTick);
        Assert.Equal(0x40000000U, worldItem.EntityId);
        Assert.Equal(9010U, worldItem.ItemSeed);
        Assert.Equal(9921U, worldItem.State.StableItemId);
        Assert.Equal(secondary.PositionX, worldItem.PositionX);
        Assert.Equal(secondary.PositionY, worldItem.PositionY);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void GameplayMultiplayerSecondaryHeldItemPickupOnOccupiedTileIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-held-item-pickup-rejection.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            StableItemId = 9920,
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX,
                secondary.PositionY + 1,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        var worldItem = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal(CommandStatus.Accepted, result.Results[0].Status);
        Assert.Equal(CommandStatus.Rejected, result.Results[1].Status);
        Assert.Equal(CommandRejectReason.NotAllowed, result.Results[1].RejectReason);
        Assert.Equal(9010U, player.HeldItem?.ItemSeed);
        Assert.Equal(9921U, player.HeldItem?.State.StableItemId);
        Assert.Equal(fixture.InitialState.WorldItemSeed, worldItem.ItemSeed);
        Assert.Equal(9920U, worldItem.State.StableItemId);
        Assert.Equal(secondary.PositionX, player.PositionX);
        Assert.Equal(secondary.PositionY + 1, player.PositionY);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryGoldPickupMatchesNativeParticipantHash()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-gold-pickup.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 11,
            ItemIndex = 0,
            StableItemId = 9913,
            Value = checked((int)fixture.InitialState.WorldItemPrice),
            IdentifiedValue = checked((int)fixture.InitialState.WorldItemPrice),
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX - 1,
                secondary.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        Assert.Equal(
            secondary.Gold + fixture.InitialState.WorldItemPrice,
            player.Gold);
        Assert.Empty(player.Inventory);
        Assert.Null(player.HeldItem);
        Assert.Empty(result.FinalSnapshot.WorldItems);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryGoldPickupRejectsMalformedStack()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-gold-pickup-malformed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 11,
            ItemIndex = 0,
            StableItemId = 9914,
            Value = checked((int)fixture.InitialState.WorldItemPrice),
            IdentifiedValue = checked((int)fixture.InitialState.WorldItemPrice - 1),
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX - 1,
                secondary.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        Assert.Equal(CommandStatus.Rejected, Assert.Single(result.Results).Status);
        Assert.Equal(CommandRejectReason.Malformed, result.Results[0].RejectReason);
        Assert.Equal(0U, player.Gold);
        Assert.Empty(player.Inventory);
        Assert.Null(player.HeldItem);
        Assert.Single(result.FinalSnapshot.WorldItems);
    }

    [Fact]
    public void GameplayMultiplayerSecondaryGoldPickupRejectsWalletOverflow()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-gold-pickup-overflow.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 11,
            ItemIndex = 0,
            StableItemId = 9915,
            Value = checked((int)fixture.InitialState.WorldItemPrice),
            IdentifiedValue = checked((int)fixture.InitialState.WorldItemPrice),
            StatFlag = true,
        };
        var secondary = fixture.InitialState.SecondaryPlayer!;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                secondary.PositionX - 1,
                secondary.PositionY,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                itemState)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        var player = Assert.Single(
            result.FinalSnapshot.Players,
            candidate => candidate.EntityId == secondary.EntityId);
        Assert.Equal(CommandStatus.Rejected, Assert.Single(result.Results).Status);
        Assert.Equal(CommandRejectReason.Malformed, result.Results[0].RejectReason);
        Assert.Equal(secondary.Gold, player.Gold);
        Assert.Empty(player.Inventory);
        Assert.Null(player.HeldItem);
        Assert.Single(result.FinalSnapshot.WorldItems);
    }

    [Fact]
    public void GameplayMultiplayerSecondaryAttackMatchesNativeParticipantHash()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-attack.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(10, Assert.Single(result.FinalSnapshot.Monsters).HitPoints);
        Assert.Equal(640, Assert.Single(result.FinalSnapshot.Monsters).HitPointsQ6);
        Assert.Equal([1U, 8U], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMonsterArrowResolutionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-resolution.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var definition = Assert.Single(fixture.InitialProjectiles);
        var projectile = new AuthoritativeProjectile(
            definition.EntityId,
            definition.SourceEntityId,
            definition.TargetEntityId,
            definition.SpellId,
            definition.LevelId,
            definition.PositionX,
            definition.PositionY,
            definition.TargetX,
            definition.TargetY,
            definition.Damage,
            (AuthoritativeDamageType)definition.DamageType,
            definition.AreaRadius,
            definition.RemainingTicks,
            allowZeroSpellId: true) {
            Behavior = (AuthoritativeProjectileBehavior)definition.Behavior,
            Phase = (AuthoritativeProjectilePhase)definition.Phase,
            Speed = definition.Speed,
            ImpactLifetime = definition.ImpactLifetime,
            DamageMinimum = definition.DamageMinimum,
            DamageMaximum = definition.DamageMaximum,
        };
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingProjectiles: [projectile],
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(96, player.Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(3UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySecondaryMonsterDeathDropsMatchNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-monster-death-drops.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var definition = Assert.Single(fixture.InitialProjectiles);
        var projectile = new AuthoritativeProjectile(
            definition.EntityId,
            definition.SourceEntityId,
            definition.TargetEntityId,
            definition.SpellId,
            definition.LevelId,
            definition.PositionX,
            definition.PositionY,
            definition.TargetX,
            definition.TargetY,
            definition.Damage,
            (AuthoritativeDamageType)definition.DamageType,
            definition.AreaRadius,
            definition.RemainingTicks,
            allowZeroSpellId: true) {
            Behavior = (AuthoritativeProjectileBehavior)definition.Behavior,
            Phase = (AuthoritativeProjectilePhase)definition.Phase,
            Speed = definition.Speed,
            ImpactLifetime = definition.ImpactLifetime,
            DamageMinimum = definition.DamageMinimum,
            DamageMaximum = definition.DamageMaximum,
        };
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingProjectiles: [projectile],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondary = Assert.Single(
            result.FinalSnapshot.Players,
            player => player.EntityId == 8);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(0, secondary.Life);
        Assert.Equal(PlayerCombatMode.Death, secondary.CombatMode);
        Assert.Equal(50U, secondary.Gold);
        Assert.Empty(secondary.Equipment);
        Assert.Equal(2, result.FinalSnapshot.WorldItems.Count);
        Assert.Equal(50U, result.FinalSnapshot.WorldItems[0].Price);
        Assert.Equal(101U, result.FinalSnapshot.WorldItems[1].ItemSeed);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySecondaryMonsterMeleeMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-monster-melee.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var primary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1);
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);

        Assert.Equal(100, primary.Life);
        Assert.Equal(97, secondary.Life);
        Assert.Equal(PlayerCombatMode.Stand, secondary.CombatMode);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGargoyleHealingMatchesNativeSelfHealBoundary()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-gargoyle-healing.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            gargoyleSpecialActive: initial.CombatTargetGargoyleSpecialActive);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);


        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(21, monster.HitPoints);
        Assert.Equal(1344, monster.HitPointsQ6);
        Assert.True(monster.GargoyleSpecialActive);
        Assert.Equal(45390955U, result.FinalSnapshot.RandomState);
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGargoyleAttackMatchesNativeAdjacentAdmission()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-gargoyle-attack.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            aggroRange: initial.CombatTargetAggroRange,
            toHit: initial.CombatTargetToHit,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            gargoyleSpecialActive: initial.CombatTargetGargoyleSpecialActive);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var primary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1);
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, primary.Life);
        Assert.Equal(100, secondary.Life);
        Assert.Equal(PlayerCombatMode.Block, secondary.CombatMode);
        Assert.Equal(5UL, secondary.CombatModeEndTick);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(2667323828U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGoatMeleeAttackMatchesNativeAdjacentAdmission()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-goat-melee-attack.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            aggroRange: initial.CombatTargetAggroRange,
            toHit: initial.CombatTargetToHit,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var primary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1);
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, primary.Life);
        Assert.Equal(100, secondary.Life);
        Assert.Equal(PlayerCombatMode.Block, secondary.CombatMode);
        Assert.Equal(5UL, secondary.CombatModeEndTick);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(2667323828U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGargoyleAvoidanceIdleMatchesNativeAdmission()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-gargoyle-avoidance-idle.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            gargoyleSpecialActive: initial.CombatTargetGargoyleSpecialActive);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(2, monster.PositionX);
        Assert.Equal(0, monster.PositionY);
        Assert.Equal(0, monster.AvoidanceMovementIdleTicks);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(1443805422U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySecondaryMonsterNativeAiSeedMatchesNativeBlockLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-monster-native-ai-seed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            aggroRange: initial.CombatTargetAggroRange,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(100, secondary.Life);
        Assert.Equal(PlayerCombatMode.Block, secondary.CombatMode);
        Assert.Equal(5UL, secondary.CombatModeEndTick);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGargoyleAvoidanceWalkMatchesNativeAdmission()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-gargoyle-avoidance-walk.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            gargoyleSpecialActive: initial.CombatTargetGargoyleSpecialActive);
        var @object = new AuthoritativeWorldObject(
            initial.ObjectEntityId,
            initial.ObjectId,
            initial.LevelId,
            initial.ObjectPositionX,
            initial.ObjectPositionY,
            missilePassable: initial.ObjectMissilePassable,
            breakable: initial.ObjectBreakable,
            destroyed: initial.ObjectDestroyed);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingObjects: [@object],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.True(monster.Moving);
        Assert.Equal(0, monster.PositionX);
        Assert.Equal(0, monster.PositionY);
        Assert.Equal(1, monster.FuturePositionX);
        Assert.Equal(1, monster.FuturePositionY);
        Assert.Equal(0, monster.Direction);
        Assert.Equal(1, monster.AvoidanceMovementIdleTicks);
        Assert.Equal(1, monster.AvoidanceMovementMode);
        Assert.Equal(90781909U, monster.MonsterAiSeed);
        Assert.Equal(274169216U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGargoyleAvoidanceMoveMatchesNativeAdmission()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-gargoyle-avoidance-move.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            gargoyleSpecialActive: initial.CombatTargetGargoyleSpecialActive);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.True(monster.Moving);
        Assert.Equal(0, monster.PositionX);
        Assert.Equal(1, monster.PositionY);
        Assert.Equal(0, monster.FuturePositionX);
        Assert.Equal(2, monster.FuturePositionY);
        Assert.Equal(1, monster.Direction);
        Assert.Equal(2, monster.AvoidanceMovementTicks);
        Assert.Equal(0, monster.AvoidanceMovementTurn);
        Assert.Equal(1, monster.AvoidanceMovementIdleTicks);
        Assert.Equal(0, monster.AvoidanceMovementMode);
        Assert.Equal(203440698U, monster.MonsterAiSeed);
        // The per-monster stream is restored before each multiplayer AI pass;
        // the global stream therefore remains at the fourth tick-2 draw.
        Assert.Equal(1084909516U, result.FinalSnapshot.RandomState);
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySkeletonMeleeDelayMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-skeleton-melee-delay.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            skeletonMeleeDelayTicks: initial.CombatTargetSkeletonMeleeDelayTicks);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.False(monster.Moving);
        Assert.Equal((4, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(14, monster.SkeletonMeleeDelayTicks);
        Assert.Equal(9U, result.FinalSnapshot.RandomState);
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySecondaryMonsterNativeAiSeedMatchesNativeImpactLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-monster-native-ai-seed-impact.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            aggroRange: initial.CombatTargetAggroRange,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(97, secondary.Life);
        Assert.Equal(PlayerCombatMode.Stand, secondary.CombatMode);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySkeletonMeleeAttackDelayMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-skeleton-melee-attack-delay.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            attackDamage: initial.CombatTargetAttackDamage,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            skeletonMeleeDelayTicks: initial.CombatTargetSkeletonMeleeDelayTicks);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, Assert.Single(result.FinalSnapshot.Players).Life);
        Assert.Equal(0, monster.SkeletonMeleeDelayTicks);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(9U, result.FinalSnapshot.RandomState);
        Assert.Equal(12UL, result.FinalSnapshot.Tick);
        Assert.Equal(3, result.Checkpoints.Count);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayRhinoChargeMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-rhino-charge.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            attackDamage: initial.CombatTargetAttackDamage,
            aggroRange: initial.CombatTargetAggroRange,
            monsterLevel: initial.CombatTargetMonsterLevel,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            toHit: initial.CombatTargetToHit,
            toHitSpecial: initial.CombatTargetToHitSpecial,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(97, player.Life);
        Assert.Equal((6, 0), (player.PositionX, player.PositionY));
        Assert.Equal((4, 0), (monster.PositionX, monster.PositionY));
        Assert.False(monster.Moving);
        Assert.Equal(9U, result.FinalSnapshot.RandomState);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Single(result.Checkpoints);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySnakeDistantDelayMatchesNativeLifecycle()
    {
        var fixture = LoadReplayFixture("gameplay-snake-distant-delay.json");
        var executor = CreateSnakeReplayExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.False(monster.Moving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(15, monster.SnakeDelayTicks);
        Assert.Equal(9U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySnakeAdjacentDelayMatchesNativeLifecycle()
    {
        var fixture = LoadReplayFixture("gameplay-snake-adjacent-delay.json");
        var executor = CreateSnakeReplayExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.False(monster.Moving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(10, monster.SnakeDelayTicks);
        Assert.Equal(9U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySnakeChargeLifecycleMatchesNative()
    {
        var fixture = LoadReplayFixture("gameplay-snake-charge-lifecycle.json");
        var executor = CreateSnakeChargeReplayExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        var player = Assert.Single(result.FinalSnapshot.Players);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6144, player.Life);
        Assert.Equal(PlayerCombatMode.GotHit, player.CombatMode);
        Assert.Equal(10UL, player.CombatModeEndTick);
        Assert.Equal((3, 0), (player.PositionX, player.PositionY));
        Assert.Equal((3, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(54, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(2U, projectile.RemainingTicks);
        Assert.Equal(4261773604U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySneakConcealmentMatchesNativeLifecycle()
    {
        var fixture = LoadReplayFixture("gameplay-sneak-conceal.json");
        var executor = CreateStealthReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.True(monster.SneakHidden);
        Assert.Equal((4, 10), (monster.PositionX, monster.PositionY));
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySneakRevealMatchesNativeLifecycle()
    {
        var fixture = LoadReplayFixture("gameplay-sneak-reveal.json");
        var executor = CreateStealthReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.False(monster.SneakHidden);
        Assert.Equal((4, 10), (monster.PositionX, monster.PositionY));
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayBatRetreatMatchesNativeLifecycle()
    {
        var fixture = LoadReplayFixture("gameplay-bat-retreat.json");
        var executor = CreateStealthReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal((9, 9), (monster.PositionX, monster.PositionY));
        Assert.Equal(0, monster.BatRetreatTicks);
        Assert.Equal(0, monster.BatRetreatDirection);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayScavengerCorpseLifecycleMatchesNative()
    {
        var fixture = LoadReplayFixture("gameplay-scavenger-corpse.json");
        var executor = CreateScavengerReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        var corpse = Assert.Single(result.FinalSnapshot.Corpses);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal(16, monster.HitPoints);
        Assert.Equal((5, 10), (monster.PositionX, monster.PositionY));
        Assert.False(monster.Moving);
        Assert.Equal(8, monster.ScavengerHealingTicks);
        Assert.Equal(30U, monster.ScavengerCorpseEntityId);
        Assert.Equal(30U, corpse.EntityId);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayScavengerHellfireCorpseLifecycleMatchesNative()
    {
        var fixture = LoadReplayFixture("gameplay-scavenger-hellfire-corpse.json");
        var executor = CreateScavengerReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(5, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal(64, monster.HitPoints);
        Assert.Equal(4096, monster.HitPointsQ6);
        Assert.Equal(0, monster.ScavengerHealingTicks);
        Assert.Equal(0U, monster.ScavengerCorpseEntityId);
        Assert.Empty(result.FinalSnapshot.Corpses);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayZombieActionMatchesNative()
    {
        var fixture = LoadReplayFixture("gameplay-zombie-action.json");
        var executor = CreateZombieReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal((5, 10), (monster.PositionX, monster.PositionY));
        Assert.False(monster.Moving);
        Assert.Equal(7, monster.Direction);
    }

    [Fact]
    public void GameplaySecondaryMonsterPathingMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-monster-pathing.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);

        Assert.True(monster.Moving);
        Assert.Equal(3, monster.PositionX);
        Assert.Equal(3, monster.PositionY);
        Assert.Equal(2, monster.FuturePositionX);
        Assert.Equal(2, monster.FuturePositionY);
        Assert.Equal(100, secondary.Life);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayFatMovementMatchesNative()
    {
        var fixture = LoadReplayFixture("gameplay-fat-movement.json");
        var executor = CreateFatReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal((5, 10), (monster.PositionX, monster.PositionY));
        Assert.True(monster.Moving);
        Assert.Equal((6, 10), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Equal(7, monster.Direction);
        Assert.Equal(0, monster.FatMovementIdleTicks);
        Assert.Equal(1, monster.FatMovementMode);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGoatMeleeMovementMatchesNative()
    {
        var fixture = LoadReplayFixture("gameplay-goat-melee-movement.json");
        var executor = CreateGoatMeleeReplayExecutor(fixture);
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal((8, 10), (monster.PositionX, monster.PositionY));
        Assert.True(monster.Moving);
        Assert.Equal((9, 10), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Equal(7, monster.Direction);
        Assert.Equal(0, monster.AvoidanceMovementIdleTicks);
        Assert.Equal(1, monster.AvoidanceMovementMode);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySecondaryMonsterObjectPathingMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-monster-object-pathing.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var @object = new AuthoritativeWorldObject(
            fixture.InitialState.ObjectEntityId,
            fixture.InitialState.ObjectId,
            fixture.InitialState.LevelId,
            fixture.InitialState.ObjectPositionX,
            fixture.InitialState.ObjectPositionY,
            missilePassable: fixture.InitialState.ObjectMissilePassable,
            breakable: fixture.InitialState.ObjectBreakable,
            destroyed: fixture.InitialState.ObjectDestroyed);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingObjects: [@object],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);

        Assert.True(monster.Moving);
        Assert.Equal(3, monster.PositionX);
        Assert.Equal(3, monster.PositionY);
        Assert.Equal(2, monster.FuturePositionX);
        Assert.Equal(2, monster.FuturePositionY);
        Assert.Equal(100, secondary.Life);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMonsterArrowAutonomousAdmissionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-autonomous.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        Assert.Empty(fixture.InitialProjectiles);
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterId: fixture.InitialState.CombatTargetMonsterId,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            minDamage: fixture.InitialState.CombatTargetMinDamage,
            maxDamage: fixture.InitialState.CombatTargetMaxDamage,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel,
            toHit: fixture.InitialState.CombatTargetToHit,
            nativeAi: fixture.InitialState.CombatTargetNativeAi,
            intelligence: fixture.InitialState.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                fixture.InitialState.CombatTargetNativeAi,
                "Arrow",
                AuthoritativeProjectileBehavior.MonsterArrow,
                ProjectileSpeed: 1,
                ProjectileLifetime: 2,
                ProjectileImpactLifetime: 1,
                ProjectileDamage: 4,
                RangedRetreatMinimumDistance: 1,
                RangedRetreatChanceBase: 1,
                RangedRetreatChanceIntelligenceScale: 1),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = result.FinalSnapshot.Players.Single(candidate => candidate.EntityId == 1);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, player.Life);
        Assert.Equal(76, result.FinalSnapshot.Players.Single(candidate => candidate.EntityId == 8).Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMonsterArrowSkeletonRangedAdmissionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-skeleton-ranged.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        Assert.Empty(fixture.InitialProjectiles);
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterId: fixture.InitialState.CombatTargetMonsterId,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            minDamage: fixture.InitialState.CombatTargetMinDamage,
            maxDamage: fixture.InitialState.CombatTargetMaxDamage,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel,
            toHit: fixture.InitialState.CombatTargetToHit,
            nativeAi: fixture.InitialState.CombatTargetNativeAi,
            intelligence: fixture.InitialState.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                fixture.InitialState.CombatTargetNativeAi,
                "Arrow",
                AuthoritativeProjectileBehavior.MonsterArrow,
                ProjectileSpeed: 1,
                ProjectileLifetime: 4,
                ProjectileImpactLifetime: 1,
                ProjectileDamage: 4),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(96, player.Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(13UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonHorkSpawnAdmissionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-autonomous.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        Assert.Empty(fixture.InitialProjectiles);
        var executor = CreateHorkSpawnExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, player.Life);
        Assert.Equal((int)AuthoritativeProjectileBehavior.HorkSpawn, projectile.Behavior);
        Assert.Equal(117U, projectile.SpellId);
        Assert.Equal(8U, projectile.RemainingTicks);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonMultiplayerTargetSelectionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-multiplayer-target.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, executor.GetPlayerState(secondary.Player).Life);
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerSecondaryPortalTransitionRoutesTheSecondarySession()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-secondary-portal.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 40, 40, []));
        world.AddLevel(new AuthoritativeLevel(2, 40, 40, []));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorld: world,
            startingPortals: [new AuthoritativePortal(5, 1, 1, 1, 2, 3, 4)],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(
            [1U, 8U],
            result.InitialSnapshot.Players
                .OrderBy(player => player.EntityId)
                .Select(player => player.EntityId));
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var players = result.FinalSnapshot.Players.OrderBy(player => player.EntityId).ToArray();
        Assert.Equal([1U, 8U], players.Select(player => player.EntityId));
        Assert.Equal((1U, 0, 0), (players[0].LevelId, players[0].PositionX, players[0].PositionY));
        Assert.Equal((2U, 3, 4), (players[1].LevelId, players[1].PositionX, players[1].PositionY));
        Assert.Equal([1U, 2U], players[1].VisitedLevelIds.OrderBy(levelId => levelId));
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
        Assert.Equal(fixture.Checkpoints[^1].StateSha256, result.FinalSnapshot.StateSha256);

        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var restored = new StoreSimulationExecutor(new StoreCatalog(), startingGold: 0);
        // Import in the opposite order as the replay setup to prove that the
        // persisted participant set and visited-level state do not depend on
        // which reconnecting session is restored first.
        restored.ImportPlayerSave(secondary.Player, executor.ExportPlayerSave(secondary.Player));
        restored.ImportPlayerSave(fixture.InitialState.Player, executor.ExportPlayerSave(fixture.InitialState.Player));
        var restoredSnapshot = restored.CreateSnapshot(fixture.InitialState.Player, 1, result.FinalSnapshot.Tick);

        Assert.Equal(result.FinalSnapshot.StateSha256, restoredSnapshot.StateSha256);
        var restoredSecondary = Assert.Single(
            restoredSnapshot.Players,
            player => player.EntityId == secondary.EntityId);
        Assert.Equal([1U, 2U], restoredSecondary.VisitedLevelIds.OrderBy(levelId => levelId));
    }

    [Fact]
    public void GameplayHorkDemonDelayMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-delay.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, player.Life);
        Assert.Equal(1, monster.HorkDemonDelayTicks);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonHorkSpawnPlayerImpactMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-impact.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture, enableSpawnPlanner: false);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6336, player.Life);
        Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonHorkSpawnMissPreservesTravelBeforeExpiry()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-miss.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture, enableSpawnPlanner: false);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6400, player.Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonHorkSpawnSecondaryBlockMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-secondary-block.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture, enableSpawnPlanner: false);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondary = Assert.Single(
            result.FinalSnapshot.Players,
            player => player.EntityId == 8);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6400, secondary.Life);
        Assert.Equal(PlayerCombatMode.Block, secondary.CombatMode);
        Assert.Equal(5UL, secondary.CombatModeEndTick);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonHorkSpawnSecondaryImpactMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-secondary-impact.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture, enableSpawnPlanner: false);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondary = Assert.Single(
            result.FinalSnapshot.Players,
            player => player.EntityId == 8);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6336, secondary.Life);
        Assert.Equal(PlayerCombatMode.GotHit, secondary.CombatMode);
        Assert.Equal(7UL, secondary.CombatModeEndTick);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonHorkSpawnBreaksCurrentMissileBlockingObject()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-object-break.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture, enableSpawnPlanner: false);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var @object = Assert.Single(result.FinalSnapshot.Objects);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.True(@object.Destroyed);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayHorkDemonHorkSpawnPrimaryBlockMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-primary-block.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateHorkSpawnExecutor(fixture, enableSpawnPlanner: false);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6400, player.Life);
        Assert.Equal(PlayerCombatMode.Block, player.CombatMode);
        Assert.Equal(5UL, player.CombatModeEndTick);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySuccubusBloodStarAdmissionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-blood-star-autonomous.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        Assert.Empty(fixture.InitialProjectiles);
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterId: fixture.InitialState.CombatTargetMonsterId,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            minDamage: fixture.InitialState.CombatTargetMinDamage,
            maxDamage: fixture.InitialState.CombatTargetMaxDamage,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel,
            toHit: fixture.InitialState.CombatTargetToHit,
            nativeAi: fixture.InitialState.CombatTargetNativeAi,
            intelligence: fixture.InitialState.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                fixture.InitialState.CombatTargetNativeAi,
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                SpellId: 36,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, player.Life);
        Assert.Equal((int)AuthoritativeProjectileBehavior.BloodStar, projectile.Behavior);
        Assert.Equal(36U, projectile.SpellId);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(3, projectile.OriginalDirection);
        Assert.Equal(-741440, projectile.VelocityX);
        Assert.Equal(-370720, projectile.VelocityY);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayLazarusSuccubusBloodStarAdmissionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-blood-star-lazarus-succubus.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        Assert.Empty(fixture.InitialProjectiles);
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterId: fixture.InitialState.CombatTargetMonsterId,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            minDamage: fixture.InitialState.CombatTargetMinDamage,
            maxDamage: fixture.InitialState.CombatTargetMaxDamage,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel,
            toHit: fixture.InitialState.CombatTargetToHit,
            nativeAi: fixture.InitialState.CombatTargetNativeAi,
            intelligence: fixture.InitialState.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                fixture.InitialState.CombatTargetNativeAi,
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                SpellId: 36,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, player.Life);
        Assert.Equal((int)AuthoritativeProjectileBehavior.BloodStar, projectile.Behavior);
        Assert.Equal(36U, projectile.SpellId);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(3, projectile.OriginalDirection);
        Assert.Equal(-741440, projectile.VelocityX);
        Assert.Equal(-370720, projectile.VelocityY);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySuccubusBloodStarMultiplayerTargetSelectionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-blood-star-multiplayer-target.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                SpellId: 36,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(100, Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1).Life);
        Assert.Equal(100, executor.GetPlayerState(secondary.Player).Life);
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.BloodStar, projectile.Behavior);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySuccubusBloodStarImpactMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-blood-star-impact.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                SpellId: 36,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6144, player.Life);
        Assert.Equal(27UL, result.FinalSnapshot.Tick);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        Assert.Equal((int)AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySuccubusBloodStarSecondaryBlockMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-blood-star-secondary-block.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                SpellId: 36,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondaryState = executor.GetPlayerState(secondary.Player);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(0, result.FinalSnapshot.Players[0].Life);
        Assert.Equal(secondary.Life, secondaryState.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, secondaryState.CombatMode);
        Assert.Equal(31UL, secondaryState.CombatModeEndTick);
        Assert.Equal((int)AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.Equal(27UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySuccubusBloodStarNativeAiSeedMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-blood-star-native-ai-seed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateBloodStarExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.BloodStar, projectile.Behavior);
        Assert.Equal(36U, projectile.SpellId);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(10, monster.RangedDelayTicks);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySuccubusBloodStarCloseNativeAiSeedMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-blood-star-close-native-ai-seed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateBloodStarExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.True(monster.Moving);
        Assert.Equal(4, monster.PositionX);
        Assert.Equal(0, monster.PositionY);
        Assert.Equal(5, monster.FuturePositionX);
        Assert.Equal(0, monster.FuturePositionY);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMegaInfernoMatchesNativeControllerLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-mega-inferno.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMegaInfernoExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var controller = Assert.Single(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.Behavior
                == (int)AuthoritativeProjectileBehavior.InfernoController);
        var segment = Assert.Single(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.Behavior
                == (int)AuthoritativeProjectileBehavior.InfernoSegment);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(9U, controller.SourceEntityId);
        Assert.Equal(1U, controller.TargetEntityId);
        Assert.Equal(21U, controller.SpellId);
        Assert.Equal(255U, controller.RemainingTicks);
        Assert.Equal(1, controller.PathIndex);
        Assert.Equal(4, segment.Damage);
        Assert.Equal(39U, segment.RemainingTicks);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(5, monster.MegaDelayTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMegaInfernoNativeAiSeedMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-mega-inferno-native-ai-seed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMegaInfernoExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var controller = Assert.Single(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.Behavior
                == (int)AuthoritativeProjectileBehavior.InfernoController);
        var segment = Assert.Single(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.Behavior
                == (int)AuthoritativeProjectileBehavior.InfernoSegment);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(1U, controller.TargetEntityId);
        Assert.Equal(255U, controller.RemainingTicks);
        Assert.Equal(1, controller.PathIndex);
        Assert.Equal(4, segment.Damage);
        Assert.Equal(39U, segment.RemainingTicks);
        Assert.Equal(15, monster.MegaDelayTicks);
        Assert.NotEqual(0U, monster.MonsterAiSeed);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayLichMonsterMagicMultiplayerTargetSelectionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-multiplayer-target.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterMagicExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        var secondaryState = executor.GetPlayerState(secondary.Player);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(6400, Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1).Life);
        Assert.Equal(6400, secondaryState.Life);
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayLichMonsterMagicActiveProjectilePersistsAcrossSaveRestore()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-miss.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMonsterMagicExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var expected = result.FinalSnapshot;
        var expectedProjectile = Assert.Single(expected.Projectiles);
        var save = executor.ExportPlayerSave(fixture.InitialState.Player);

        var replacement = CreateMonsterMagicExecutor(fixture);
        replacement.ImportPlayerSave(fixture.InitialState.Player, save);

        var restored = replacement.CreateSnapshot(
            fixture.InitialState.Player,
            1,
            expected.Tick);
        var restoredProjectile = Assert.Single(restored.Projectiles);
        Assert.Equal(expected.StateSha256, restored.StateSha256);
        Assert.Equal(expectedProjectile.SourceEntityId, restoredProjectile.SourceEntityId);
        Assert.Equal(expectedProjectile.TargetEntityId, restoredProjectile.TargetEntityId);
        Assert.Equal(expectedProjectile.Behavior, restoredProjectile.Behavior);
        Assert.Equal(expectedProjectile.Phase, restoredProjectile.Phase);
        Assert.Equal(expectedProjectile.RemainingTicks, restoredProjectile.RemainingTicks);

        executor.AdvanceTo(expected.Tick + 1);
        replacement.AdvanceTo(expected.Tick + 1);
        var continued = executor.CreateSnapshot(
            fixture.InitialState.Player,
            1,
            expected.Tick + 1);
        var restoredContinued = replacement.CreateSnapshot(
            fixture.InitialState.Player,
            1,
            expected.Tick + 1);
        Assert.Equal(continued.StateSha256, restoredContinued.StateSha256);
    }

    [Fact]
    public void GameplayBoneDemonMonsterMagicMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-bone-demon.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMonsterMagicExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(1U, projectile.SpellId);
        Assert.Equal((int)AuthoritativeDamageType.Magic, projectile.DamageType);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayArchLichMonsterMagicMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-arch-lich.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMonsterMagicExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(1U, projectile.SpellId);
        Assert.Equal((int)AuthoritativeDamageType.Magic, projectile.DamageType);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayPsychorbMonsterMagicMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-psychorb.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMonsterMagicExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(1U, projectile.SpellId);
        Assert.Equal((int)AuthoritativeDamageType.Magic, projectile.DamageType);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayNecromorbMonsterMagicMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-necromorb.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMonsterMagicExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(1U, projectile.SpellId);
        Assert.Equal((int)AuthoritativeDamageType.Magic, projectile.DamageType);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(255U, projectile.RemainingTicks);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySkeletonRangedMonsterArrowMultiplayerTargetSelectionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-multiplayer-target.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        var secondaryState = executor.GetPlayerState(secondary.Player);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(100, Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1).Life);
        Assert.Equal(100, secondaryState.Life);
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterArrow, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(6U, projectile.RemainingTicks);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGoatRangedMonsterArrowMultiplayerTargetSelectionMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-goat-ranged-multiplayer-target.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        var secondaryState = executor.GetPlayerState(secondary.Player);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(100, Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1).Life);
        Assert.Equal(100, secondaryState.Life);
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterArrow, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(6U, projectile.RemainingTicks);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGoatRangedCloseRetreatMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-goat-ranged-close-retreat.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        var secondaryState = executor.GetPlayerState(secondary.Player);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(100, secondaryState.Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.True(monster.Moving);
        Assert.Equal(4, monster.PositionX);
        Assert.Equal(0, monster.PositionY);
        Assert.Equal(5, monster.FuturePositionX);
        Assert.Equal(0, monster.FuturePositionY);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGoatRangedDelayMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-goat-ranged-delay.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterArrow, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(6U, projectile.RemainingTicks);
        Assert.Equal(0, monster.RangedDelayTicks);
        Assert.Equal(2UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySkeletonRangedCloseRetreatMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-skeleton-ranged-close-retreat.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);
        var secondaryState = executor.GetPlayerState(secondary.Player);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(100, secondaryState.Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.True(monster.Moving);
        Assert.Equal(3, monster.PositionX);
        Assert.Equal(0, monster.PositionY);
        Assert.Equal(4, monster.FuturePositionX);
        Assert.Equal(0, monster.FuturePositionY);
        Assert.Equal(0, monster.SkeletonRangedMovementIdleTicks);
        Assert.Equal(1, monster.SkeletonRangedMovementMode);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplaySkeletonRangedNativeAiSeedMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-skeleton-ranged-native-ai-seed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterArrow, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(6U, projectile.RemainingTicks);
        Assert.Equal(0, monster.RangedDelayTicks);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGoatRangedNativeAiSeedMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-goat-ranged-native-ai-seed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterArrow, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(6U, projectile.RemainingTicks);
        Assert.Equal(10, monster.RangedDelayTicks);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayGoatRangedCloseNativeAiSeedMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-arrow-goat-ranged-close-native-ai-seed.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = CreateMonsterArrowExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.True(monster.Moving);
        Assert.Equal(4, monster.PositionX);
        Assert.Equal(0, monster.PositionY);
        Assert.Equal(5, monster.FuturePositionX);
        Assert.Equal(0, monster.FuturePositionY);
        Assert.Equal(1788971303U, monster.MonsterAiSeed);
        Assert.Equal(1UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayLichMonsterMagicMatchesNativeLifecycle()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-autonomous.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "OrangeFlare",
                AuthoritativeProjectileBehavior.MonsterMagic,
                SpellId: 1,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6144, player.Life);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(1U, projectile.SpellId);
        Assert.Equal((int)AuthoritativeDamageType.Magic, projectile.DamageType);
        Assert.Equal((int)AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.Equal(27UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayLichMonsterMagicMissPreservesProjectileAndPlayerState()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-miss.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "OrangeFlare",
                AuthoritativeProjectileBehavior.MonsterMagic,
                SpellId: 1,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(6400, player.Life);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal((int)AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(229U, projectile.RemainingTicks);
        Assert.Equal(1U, projectile.LastCollisionTargetEntityId);
        Assert.Equal(27UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayLichMonsterMagicRoutesImpactToSecondaryParticipant()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-secondary-impact.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var initial = fixture.InitialState;
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(initial.SecondaryPlayer);
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "OrangeFlare",
                AuthoritativeProjectileBehavior.MonsterMagic,
                SpellId: 1,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: DiabloGameMode.Multiplayer);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondaryState = executor.GetPlayerState(secondary.Player);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(0, result.FinalSnapshot.Players[0].Life);
        Assert.Equal(6144, secondaryState.Life);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.Equal(27UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayLichMonsterMagicBlocksOnSecondaryParticipant()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-monster-magic-secondary-block.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var secondary = Assert.IsType<ReplaySecondaryPlayer>(fixture.InitialState.SecondaryPlayer);
        var executor = CreateMonsterMagicExecutor(fixture);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var secondaryState = executor.GetPlayerState(secondary.Player);
        var projectile = Assert.Single(result.FinalSnapshot.Projectiles);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(0, result.FinalSnapshot.Players[0].Life);
        Assert.Equal(6400, secondaryState.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, secondaryState.CombatMode);
        Assert.Equal(31UL, secondaryState.CombatModeEndTick);
        Assert.Equal((int)AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(secondary.EntityId, projectile.TargetEntityId);
        Assert.Equal((int)AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.Equal(27UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerMonsterArrowRoutesImpactToSecondaryParticipant()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-monster-arrow.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var definition = Assert.Single(fixture.InitialProjectiles);
        var projectile = new AuthoritativeProjectile(
            definition.EntityId,
            definition.SourceEntityId,
            definition.TargetEntityId,
            definition.SpellId,
            definition.LevelId,
            definition.PositionX,
            definition.PositionY,
            definition.TargetX,
            definition.TargetY,
            definition.Damage,
            (AuthoritativeDamageType)definition.DamageType,
            definition.AreaRadius,
            definition.RemainingTicks,
            allowZeroSpellId: true) {
            Behavior = (AuthoritativeProjectileBehavior)definition.Behavior,
            Phase = (AuthoritativeProjectilePhase)definition.Phase,
            Speed = definition.Speed,
            ImpactLifetime = definition.ImpactLifetime,
            DamageMinimum = definition.DamageMinimum,
            DamageMaximum = definition.DamageMaximum,
        };
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingProjectiles: [projectile],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var primary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1);
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(100, primary.Life);
        Assert.Equal(76, secondary.Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(3UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerMonsterArrowVolleyRoutesBothImpactsDeterministically()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-monster-arrow-volley.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var projectiles = fixture.InitialProjectiles.Select(definition => new AuthoritativeProjectile(
            definition.EntityId,
            definition.SourceEntityId,
            definition.TargetEntityId,
            definition.SpellId,
            definition.LevelId,
            definition.PositionX,
            definition.PositionY,
            definition.TargetX,
            definition.TargetY,
            definition.Damage,
            (AuthoritativeDamageType)definition.DamageType,
            definition.AreaRadius,
            definition.RemainingTicks,
            allowZeroSpellId: true) {
            Behavior = (AuthoritativeProjectileBehavior)definition.Behavior,
            Phase = (AuthoritativeProjectilePhase)definition.Phase,
            Speed = definition.Speed,
            ImpactLifetime = definition.ImpactLifetime,
            DamageMinimum = definition.DamageMinimum,
            DamageMaximum = definition.DamageMaximum,
        }).ToArray();
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingProjectiles: projectiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var primary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 1);
        var secondary = Assert.Single(result.FinalSnapshot.Players, player => player.EntityId == 8);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, secondary.EntityId], result.FinalSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal(96, primary.Life);
        Assert.Equal(76, secondary.Life);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(3UL, result.FinalSnapshot.Tick);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void GameplayMultiplayerMonsterArrowVolleyContinuesAfterBothParticipantSavesRestore()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-monster-arrow-volley.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel);
        var projectiles = fixture.InitialProjectiles.Select(definition => new AuthoritativeProjectile(
            definition.EntityId,
            definition.SourceEntityId,
            definition.TargetEntityId,
            definition.SpellId,
            definition.LevelId,
            definition.PositionX,
            definition.PositionY,
            definition.TargetX,
            definition.TargetY,
            definition.Damage,
            (AuthoritativeDamageType)definition.DamageType,
            definition.AreaRadius,
            definition.RemainingTicks,
            allowZeroSpellId: true) {
            Behavior = (AuthoritativeProjectileBehavior)definition.Behavior,
            Phase = (AuthoritativeProjectilePhase)definition.Phase,
            Speed = definition.Speed,
            ImpactLifetime = definition.ImpactLifetime,
            DamageMinimum = definition.DamageMinimum,
            DamageMaximum = definition.DamageMaximum,
        }).ToArray();
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var source = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingProjectiles: projectiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
        source.CreateSnapshot("player-b", 8, 0);
        source.AdvanceTo(1);
        var expectedAtSave = source.CreateSnapshot("player-a", 1, 1);
        var primarySave = source.ExportPlayerSave("player-a");
        var secondarySave = source.ExportPlayerSave("player-b");

        var replacement = new StoreSimulationExecutor(new StoreCatalog(), startingGold: 0);
        replacement.ImportPlayerSave("player-a", primarySave);
        replacement.ImportPlayerSave("player-b", secondarySave);
        var restoredAtSave = replacement.CreateSnapshot("player-a", 1, 1);

        Assert.Equal(expectedAtSave.StateSha256, restoredAtSave.StateSha256);
        Assert.Equal(
            expectedAtSave.Players.Select(player => (player.EntityId, player.Life)),
            restoredAtSave.Players.Select(player => (player.EntityId, player.Life)));
        Assert.Equal(
            expectedAtSave.Projectiles.Select(projectile => (projectile.EntityId, projectile.TargetEntityId, projectile.RemainingTicks)),
            restoredAtSave.Projectiles.Select(projectile => (projectile.EntityId, projectile.TargetEntityId, projectile.RemainingTicks)));

        source.AdvanceTo(2);
        replacement.AdvanceTo(2);
        var expectedAfterResume = source.CreateSnapshot("player-a", 1, 2);
        var restoredAfterResume = replacement.CreateSnapshot("player-a", 1, 2);
        Assert.Equal(expectedAfterResume.StateSha256, restoredAfterResume.StateSha256);
        Assert.Equal(
            expectedAfterResume.Players.Select(player => (player.EntityId, player.Life)),
            restoredAfterResume.Players.Select(player => (player.EntityId, player.Life)));
        Assert.Equal(
            expectedAfterResume.Projectiles.Select(projectile => (projectile.EntityId, projectile.TargetEntityId, projectile.Phase, projectile.RemainingTicks)),
            restoredAfterResume.Projectiles.Select(projectile => (projectile.EntityId, projectile.TargetEntityId, projectile.Phase, projectile.RemainingTicks)));

        source.AdvanceTo(4);
        replacement.AdvanceTo(4);
        var expectedAfterImpact = source.CreateSnapshot("player-a", 1, 4);
        var restoredAfterImpact = replacement.CreateSnapshot("player-a", 1, 4);
        Assert.Equal(expectedAfterImpact.StateSha256, restoredAfterImpact.StateSha256);
        Assert.Empty(restoredAfterImpact.Projectiles);
    }

    [Fact]
    public void GameplayStatusExpiryFixtureAdvancesEffectsByAuthoritativeTick()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-status-expiry.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(
                2,
                3,
                0,
                fixture.InitialState.StatusEffectId,
                fixture.InitialState.StatusDuration,
                fixture.InitialState.StatusMagnitude),
        ]);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingLifeMaximum: fixture.InitialState.Life,
            startingSpells: spells,
            startingAbilitySpellId: fixture.InitialState.AbilitySpellId,
            startingMemorizedSpells: fixture.InitialState.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Empty(player.StatusEffects);
    }

    [Fact]
    public void StructuredContentManifestLoadsAndRetainsEveryCheckpoint()
    {
        var fixture = ReplayFixtureLoader.Load(CreateStructuredFixture(
            """
            [
              { "tick": 1, "state_sha256": "legacy-open" },
              { "tick": 2, "state_sha256": "legacy-buy" }
            ]
            """));

        var contentManifestProperty = fixture.GetType().GetProperty("ContentManifest");
        Assert.NotNull(contentManifestProperty);
        var contentManifest = contentManifestProperty.GetValue(fixture);
        Assert.NotNull(contentManifest);
        Assert.Equal("test-content", contentManifest.GetType().GetProperty("Id")?.GetValue(contentManifest));
        Assert.Equal("1", contentManifest.GetType().GetProperty("Version")?.GetValue(contentManifest));
        Assert.Equal("a4f2d11cb54e18c5df07eae670a7ab9ab739f5efbd83dc84d5fcefe956315e97", contentManifest.GetType().GetProperty("Sha256")?.GetValue(contentManifest));
        Assert.Equal([1UL, 2UL], fixture.Checkpoints.Select(checkpoint => checkpoint.Tick));
        Assert.Equal(["legacy-open", "legacy-buy"], fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256));
    }

    [Fact]
    public void StableSpellIdsAboveTheNativeRangeLoadInReplayState()
    {
        var fixture = ReplayFixtureLoader.Load(
            CreateStructuredFixture(
                """
                [
                  { "tick": 1, "state_sha256": "legacy-open" },
                  { "tick": 2, "state_sha256": "legacy-buy" }
                ]
                """).Replace(
                    "\"life\": 640",
                    "\"life\": 640,\n            \"memorized_spells\": [{ \"spell_id\": 53, \"level\": 1 }]",
                    StringComparison.Ordinal));

        var spell = Assert.Single(fixture.InitialState.MemorizedSpells);
        Assert.Equal(53U, spell.SpellId);
        Assert.Equal(1U, spell.Level);
    }

    [Fact]
    public void StructuredFixtureWithoutACheckpointForEachCommandTickIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReplayFixtureLoader.Load(CreateStructuredFixture(
            """
            [
              { "tick": 1, "state_sha256": "legacy-open" }
            ]
            """)));

        Assert.Equal("Replay fixture must include a checkpoint at command tick 2.", exception.Message);
    }

    [Fact]
    public void StructuredFixtureWithDuplicateCheckpointTicksIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReplayFixtureLoader.Load(CreateStructuredFixture(
            """
            [
              { "tick": 1, "state_sha256": "first" },
              { "tick": 1, "state_sha256": "duplicate" },
              { "tick": 2, "state_sha256": "second" }
            ]
            """)));

        Assert.Equal("Replay fixture checkpoint ticks must be unique.", exception.Message);
    }

    [Fact]
    public void FinalSnapshotHashMismatchIsRejected()
    {
        var fixture = ReplayFixtureLoader.Load(CreateStructuredFixture(
            """
            [
              { "tick": 1, "state_sha256": "legacy-open" },
              { "tick": 2, "state_sha256": "legacy-buy" }
            ]
            """,
            "not-the-final-snapshot-hash"));
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75)]);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingRandomSeed: checked((uint)fixture.RngSeed));
        var commandServer = new AuthoritativeCommandServer(executor);

        var exception = Assert.Throws<InvalidDataException>(() => ReplayFixtureExecutor.Execute(fixture, executor, commandServer));

        Assert.Equal("Replay final snapshot hash mismatch.", exception.Message);
    }

    [Fact]
    public void MalformedFixtureIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ReplayFixtureLoader.Load("{\"format_version\":1}"));
    }

    [Fact]
    public void SecondaryReplayParticipantRejectsUnsupportedCommand()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-multiplayer-autonomous-tick.json");
        var json = File.ReadAllText(path)
            .Replace("\"kind\": \"Move\"", "\"kind\": \"OpenStore\"", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Secondary replay participant supports Move, Attack, UsePortal, DropItem, PickupWorldItem, and DropHeldItem commands only.",
            exception.Message);
    }

    [Fact]
    public void InitialLifeMaximumBelowCurrentLifeIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-healing-resolution.json");
        var json = File.ReadAllText(path)
            .Replace("\"life_maximum\": 5000", "\"life_maximum\": 999", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid initial player life.",
            exception.Message);
    }

    [Fact]
    public void InvalidPlayerActionTimingIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-action-lifecycle.json");
        var json = File.ReadAllText(path)
            .Replace("\"walking_frames\": 8", "\"walking_frames\": 0", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid player-action timing.",
            exception.Message);
    }

    [Fact]
    public void InvalidPlayerDeathTimingIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-death-lifecycle.json");
        var json = File.ReadAllText(path)
            .Replace("\"death_frames\": 20", "\"death_frames\": 128", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid player-action timing.",
            exception.Message);
    }

    [Fact]
    public void NegativeCombatTargetAttackDamageIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-death-lifecycle.json");
        var json = File.ReadAllText(path)
            .Replace(
                "\"combat_target_attack_damage\": 5",
                "\"combat_target_attack_damage\": -1",
                StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid combat-target occupancy state.",
            exception.Message);
    }

    [Fact]
    public void InvalidCombatTargetFireResistanceIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-firebolt-resolution.json");
        var json = File.ReadAllText(path)
            .Replace(
                "\"combat_target_fire_resistance\": 25",
                "\"combat_target_fire_resistance\": 101",
                StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid combat-target occupancy state.",
            exception.Message);
    }

    [Fact]
    public void InvalidCombatTargetLightningResistanceIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-lightning-resolution.json");
        var json = File.ReadAllText(path)
            .Replace(
                "\"combat_target_lightning_resistance\": 25",
                "\"combat_target_lightning_resistance\": 101",
                StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid combat-target occupancy state.",
            exception.Message);
    }

    [Fact]
    public void InvalidCombatTargetMagicResistanceIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-flash-resolution.json");
        var json = File.ReadAllText(path)
            .Replace(
                "\"combat_target_magic_resistance\": 25",
                "\"combat_target_magic_resistance\": 101",
                StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid combat-target occupancy state.",
            exception.Message);
    }

    [Fact]
    public void InvalidHorkDemonDelayIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-hork-spawn-delay.json");
        var json = File.ReadAllText(path)
            .Replace(
                "\"combat_target_hork_demon_delay_ticks\": 2",
                "\"combat_target_hork_demon_delay_ticks\": 1025",
                StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture contains invalid combat-target occupancy state.",
            exception.Message);
    }

    [Fact]
    public void InvalidInitialEquipmentIsRejected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-action-ranged-diablo.json");
        var json = File.ReadAllText(path)
            .Replace("\"item_seed\": 100", "\"item_seed\": 0", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReplayFixtureLoader.Load(json));

        Assert.Equal(
            "Replay fixture equipment contains invalid or duplicate item state.",
            exception.Message);
    }

    private static StoreSimulationExecutor CreateCounselorExecutor(ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            counselorDelayTicks: initial.CombatTargetCounselorDelayTicks,
            counselorRetreatTicks: initial.CombatTargetCounselorRetreatTicks);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateMonsterMagicExecutor(ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            rangedDelayTicks: initial.CombatTargetRangedDelayTicks,
            skeletonRangedMovementIdleTicks: initial.CombatTargetSkeletonRangedMovementIdleTicks,
            skeletonRangedMovementMode: initial.CombatTargetSkeletonRangedMovementMode);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "OrangeFlare",
                AuthoritativeProjectileBehavior.MonsterMagic,
                SpellId: 1,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateMonsterArrowExecutor(ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            rangedDelayTicks: initial.CombatTargetRangedDelayTicks,
            skeletonRangedMovementIdleTicks: initial.CombatTargetSkeletonRangedMovementIdleTicks,
            skeletonRangedMovementMode: initial.CombatTargetSkeletonRangedMovementMode);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "Arrow",
                AuthoritativeProjectileBehavior.MonsterArrow,
                ProjectileSpeed: 1,
                ProjectileLifetime: 7,
                ProjectileImpactLifetime: 1,
                ProjectileDamage: 4),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateBloodStarExecutor(ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            rangedDelayTicks: initial.CombatTargetRangedDelayTicks);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                SpellId: 36,
                ProjectileSpeed: 16,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Magic,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                RangedDelayRandomRange: 20),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateMegaInfernoExecutor(ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            monsterId: initial.CombatTargetMonsterId,
            aggroRange: initial.CombatTargetAggroRange,
            attackDamage: initial.CombatTargetAttackDamage,
            minDamage: initial.CombatTargetMinDamage,
            maxDamage: initial.CombatTargetMaxDamage,
            monsterLevel: initial.CombatTargetMonsterLevel,
            toHit: initial.CombatTargetToHit,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            monsterItemSeed: initial.CombatTargetMonsterItemSeed,
            monsterAiSeed: initial.CombatTargetMonsterAiSeed,
            megaDelayTicks: initial.CombatTargetMegaDelayTicks);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Mega",
                "InfernoControl",
                AuthoritativeProjectileBehavior.InfernoController,
                SpellId: 21,
                ProjectileSpeed: 32,
                ProjectileLifetime: 256,
                DamageType: AuthoritativeDamageType.Fire,
                InfernoMaximumDistance: 5,
                InfernoAdjacentChanceBase: 40,
                InfernoAdjacentChanceIntelligenceScale: 10,
                InfernoRangedFarDistance: 3,
                InfernoRangedChanceBase: 5,
                InfernoRangedChanceIntelligenceScale: 5,
                InfernoRangedFarIntelligenceOffset: 1,
                InfernoAdjacentChoiceModulo: 2,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 1,
                MegaDelayRandomRange: 20),
        ]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(21, 11, 0, 0, 0, 0) {
                NativeSpellId = 20,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 32,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.InfernoController,
                RandomDamageRange = 2,
                DamageBonus = 24,
                DamageScale = 12,
                SecondaryProjectileLifetime = 20,
                InfernoSegmentLifetimeStep = 1,
                ProjectileImpactLifetime = 1,
            },
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingSpells: spells,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateHorkSpawnExecutor(
        ReplayFixture fixture,
        bool enableSpawnPlanner = true)
    {
        var target = new AuthoritativeCombatTarget(
            fixture.InitialState.CombatTargetEntityId,
            fixture.InitialState.CombatTargetPositionX,
            fixture.InitialState.CombatTargetPositionY,
            fixture.InitialState.CombatTargetHitPoints,
            maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
            levelId: fixture.InitialState.CombatTargetLevelId,
            monsterId: fixture.InitialState.CombatTargetMonsterId,
            aggroRange: fixture.InitialState.CombatTargetAggroRange,
            attackDamage: fixture.InitialState.CombatTargetAttackDamage,
            monsterLevel: fixture.InitialState.CombatTargetMonsterLevel,
            nativeAi: fixture.InitialState.CombatTargetNativeAi,
            intelligence: fixture.InitialState.CombatTargetIntelligence,
            horkDemonDelayTicks: fixture.InitialState.CombatTargetHorkDemonDelayTicks);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                fixture.InitialState.CombatTargetNativeAi,
                "HorkSpawn",
                AuthoritativeProjectileBehavior.HorkSpawn,
                SpawnMonsterId: 117,
                ProjectileSpeed: 8,
                ProjectileLifetime: 9,
                ProjectileImpactLifetime: 1,
                DamageType: AuthoritativeDamageType.Physical),
        ]);
        var spawnWorld = new AuthoritativeWorld();
        spawnWorld.AddLevel(new AuthoritativeLevel(1, 40, 40));
        var spawnTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                117,
                117,
                "Hork Spawn",
                0,
                100,
                1,
                1,
                1,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "HorkDemon",
                AggroRange = 6,
            },
        ]);
        var spawnPlanner = new AuthoritativeMonsterSpawnPlanner(
            spawnWorld,
            spawnTypes,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(0, 100, 100, 100, 0),
            ]));
        var startingProjectiles = fixture.InitialProjectiles
            .Select(source => new AuthoritativeProjectile(
                source.EntityId,
                source.SourceEntityId,
                source.TargetEntityId,
                source.SpellId,
                source.LevelId,
                source.PositionX,
                source.PositionY,
                source.TargetX,
                source.TargetY,
                source.Damage,
                (AuthoritativeDamageType)source.DamageType,
                source.AreaRadius,
                source.RemainingTicks,
                allowZeroSpellId: source.SpellId == 0) {
                Behavior = (AuthoritativeProjectileBehavior)source.Behavior,
                Phase = (AuthoritativeProjectilePhase)source.Phase,
                Speed = source.Speed,
                ImpactLifetime = source.ImpactLifetime,
                DamageMinimum = source.DamageMinimum,
                DamageMaximum = source.DamageMaximum,
            })
            .ToArray();
        var startingEquipment = fixture.InitialState.Equipment
            .Select(source => new EquippedStoreItem(
                source.Slot,
                source.ItemSeed,
                AuthoritativeItemState.Empty with {
                    ItemType = source.ItemType,
                    ItemClass = source.ItemClass,
                    Flags = source.Flags,
                    StatFlag = source.StatFlag,
                }))
            .ToArray();
        IReadOnlyList<AuthoritativeWorldObject> startingObjects = fixture.InitialState.ObjectEntityId == 0
            ? Array.Empty<AuthoritativeWorldObject>()
            : [new AuthoritativeWorldObject(
                fixture.InitialState.ObjectEntityId,
                fixture.InitialState.ObjectId,
                fixture.InitialState.LevelId,
                fixture.InitialState.ObjectPositionX,
                fixture.InitialState.ObjectPositionY,
                missilePassable: fixture.InitialState.ObjectMissilePassable,
                breakable: fixture.InitialState.ObjectBreakable,
                destroyed: fixture.InitialState.ObjectDestroyed)];
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingEquipment: startingEquipment,
            startingObjects: startingObjects,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingCombatTargets: [target],
            startingProjectiles: startingProjectiles,
            startingMonsterMissiles: missiles,
            startingMonsterSpawnPlanner: enableSpawnPlanner ? spawnPlanner : null,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static ReplayFixture LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic-buy.json");
        return ReplayFixtureLoader.Load(File.ReadAllText(path));
    }

    private static ReplayFixture LoadReplayFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return ReplayFixtureLoader.Load(File.ReadAllText(path));
    }

    private static StoreSimulationExecutor CreateSnakeReplayExecutor(
        ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            snakeDelayTicks: initial.CombatTargetSnakeDelayTicks);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "Rhino",
                AuthoritativeProjectileBehavior.SnakeCharge),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateStealthReplayExecutor(
        ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            sneakHidden: initial.CombatTargetSneakHidden,
            sneakRetreatTicks: initial.CombatTargetSneakRetreatTicks,
            batRetreatTicks: initial.CombatTargetBatRetreatTicks,
            batRetreatDirection: initial.CombatTargetBatRetreatDirection);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateScavengerReplayExecutor(
        ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            scavengerHealingTicks: initial.CombatTargetScavengerHealingTicks,
            scavengerCorpseEntityId: initial.CombatTargetScavengerCorpseEntityId,
            fatMovementIdleTicks: initial.CombatTargetFatMovementIdleTicks,
            fatMovementMode: initial.CombatTargetFatMovementMode);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingCorpses: initial.Corpses
                .Select(corpse => new AuthoritativeCorpse(
                    corpse.EntityId,
                    corpse.LevelId,
                    corpse.PositionX,
                    corpse.PositionY))
                .ToArray(),
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateZombieReplayExecutor(
        ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateFatReplayExecutor(
        ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence,
            fatMovementIdleTicks: initial.CombatTargetFatMovementIdleTicks,
            fatMovementMode: initial.CombatTargetFatMovementMode);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateGoatMeleeReplayExecutor(
        ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static StoreSimulationExecutor CreateSnakeChargeReplayExecutor(
        ReplayFixture fixture)
    {
        var initial = fixture.InitialState;
        var target = new AuthoritativeCombatTarget(
            initial.CombatTargetEntityId,
            initial.CombatTargetPositionX,
            initial.CombatTargetPositionY,
            initial.CombatTargetHitPoints,
            maxHitPoints: initial.CombatTargetMaxHitPoints,
            levelId: initial.CombatTargetLevelId,
            aggroRange: initial.CombatTargetAggroRange,
            monsterLevel: initial.CombatTargetMonsterLevel,
            nativeAi: initial.CombatTargetNativeAi,
            intelligence: initial.CombatTargetIntelligence);
        var missiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                initial.CombatTargetNativeAi,
                "Rhino",
                AuthoritativeProjectileBehavior.SnakeCharge,
                SpellId: 37,
                ProjectileSpeed: 18,
                ProjectileLifetime: 4,
                ProjectileDamage: 4,
                ProjectileImpactLifetime: 2),
        ]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        return new StoreSimulationExecutor(
            catalog,
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingLifeMaximum: initial.LifeMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [target],
            startingMonsterMissiles: missiles,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            gameMode: fixture.GameMode);
    }

    private static string CreateStructuredFixture(string checkpoints, string? finalStateSha256 = null)
    {
        var finalState = finalStateSha256 is null
            ? string.Empty
            : $",\n  \"final_state_sha256\": \"{finalStateSha256}\"";
        return $$"""
        {
          "format_version": 1,
          "fixture_id": "stores/structured-buy",
          "protocol_schema_version": "0.1.0",
          "content_manifest": {
            "id": "test-content",
            "version": "1",
            "sha256": "a4f2d11cb54e18c5df07eae670a7ab9ab739f5efbd83dc84d5fcefe956315e97"
          },
          "tick_rate_hz": 20,
          "rng_seed": 1,
          "initial_state": {
            "player": "A",
            "gold": 100,
            "experience": 200,
            "life": 640
          },
          "commands": [
            {
              "client_sequence": 1,
              "target_tick": 1,
              "server_receipt_sequence": 1,
              "kind": "OpenStore"
            },
            {
              "client_sequence": 2,
              "target_tick": 2,
              "server_receipt_sequence": 2,
              "kind": "BuyItem",
              "payload": { "store_id": 1, "store_slot": 0 }
            }
          ],
          "checkpoints": {{checkpoints}}{{finalState}}
        }
        """;
    }

    private static AuthoritativeSpellCatalog LoadReplaySpells(
        ReplayFixture fixture,
        string packDirectory)
    {
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "test",
            "content",
            "replay",
            packDirectory);
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var identity = Assert.IsType<ReplayContentManifest>(fixture.ContentManifest);
        Assert.Equal(pack.Id, identity.Id);
        Assert.Equal(pack.Version, identity.Version);
        var manifest = new ContentManifest(identity.Id, identity.Version, [pack]);
        Assert.Equal(manifest.Sha256, identity.Sha256);
        return AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
    }
    private static AuthoritativeSpellCatalog LoadBaseSpells(ReplayFixture fixture)
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        return AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
    }

    private static void AssertUsesBaseContentManifest(
        ReplayFixture fixture,
        string contentRoot)
    {
        var identity = Assert.IsType<ReplayContentManifest>(fixture.ContentManifest);
        var manifest = new ContentManifest(
            identity.Id,
            identity.Version,
            [ContentPackLoader.LoadDirectory(
                identity.Id,
                identity.Version,
                contentRoot)]);
        Assert.Equal(manifest.Sha256, identity.Sha256);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "server", "content", "base", "classes.tsv")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}
