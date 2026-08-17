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
    public void ChargedBoltPvpFixtureDamagesTheSecondAuthoritativeParticipant()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-charged-bolt-pvp.json");
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
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 26,
                ProjectileLifetime = 256,
                ProjectileCount = 1,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Firebolt,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 1,
                RandomDamageRange = 10,
                MagicDamageDivisor = 8,
                SpellLevelDamage = 1,
                DamageBonus = 1,
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
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(12, 16, 0, 0, 0, 0) {
                Range = 112,
                AreaRadius = 1,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Fireball,
                ManaAdjustment = 1,
                MinimumManaCost = 10,
                ManaCostScale = 64,
                RandomDamageRange = 10,
                DamageBonus = 4,
                DamageScale = 2,
                RandomDamageIterationsBonus = 2,
                CharacterLevelDamageDivisor = 1,
                SpellEffectScaleDivisor = 8,
                ProjectileSpeedSpellLevelStep = 2,
                ProjectileSpeedMaximumBonus = 34,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 14,
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
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(38, 24, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.BoneSpirit,
                ManaAdjustment = 1,
                ManaAdjustmentDivisor = 1,
                MinimumManaCost = 12,
                ManaCostScale = 64,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 7,
                SelfDamage = 6,
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
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(38, 24, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.BoneSpirit,
                ManaAdjustment = 1,
                ManaAdjustmentDivisor = 1,
                MinimumManaCost = 12,
                ManaCostScale = 64,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 7,
                SelfDamage = 6,
            },
        ]);
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
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(12, 16, 0, 0, 0, 0) {
                Range = 112,
                AreaRadius = 1,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Fireball,
                ManaAdjustment = 1,
                MinimumManaCost = 10,
                ManaCostScale = 64,
                RandomDamageRange = 10,
                DamageBonus = 4,
                DamageScale = 2,
                RandomDamageIterationsBonus = 2,
                CharacterLevelDamageDivisor = 1,
                SpellEffectScaleDivisor = 8,
                ProjectileSpeedSpellLevelStep = 2,
                ProjectileSpeedMaximumBonus = 34,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 14,
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
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 26,
                ProjectileLifetime = 256,
                ProjectileCount = 1,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Firebolt,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 1,
                RandomDamageRange = 10,
                MagicDamageDivisor = 8,
                SpellLevelDamage = 1,
                DamageBonus = 1,
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
            "Secondary replay participant currently supports Move and Attack commands only.",
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

    private static ReplayFixture LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic-buy.json");
        return ReplayFixtureLoader.Load(File.ReadAllText(path));
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
}
