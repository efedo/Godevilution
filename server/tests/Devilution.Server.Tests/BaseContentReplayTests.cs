using Devilution.Server.Commands;
using Devilution.Server.Replay;
using Devilution.Server.Stores;
using Devilution.Data;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Server.Tests;

/** Executes a replay against the checked-in base store catalog. */
public sealed class BaseContentReplayTests
{
    [Theory]
    [InlineData("gameplay-class-physical-damage.json", 1, 94)]
    [InlineData("gameplay-class-physical-damage-monk.json", 3, 93)]
    [InlineData("gameplay-class-physical-damage-bard.json", 4, 93)]
    [InlineData("gameplay-class-physical-damage-barbarian.json", 5, 89)]
    [InlineData("gameplay-class-physical-damage-warrior.json", 0, 93)]
    [InlineData("gameplay-class-physical-damage-sorcerer.json", 2, 95)]
    [InlineData("gameplay-class-physical-damage-armor.json", 0, 99)]
    [InlineData("gameplay-class-physical-damage-miss.json", 2, 100)]
    [InlineData("gameplay-class-physical-damage-armor-piercing.json", 0, 99)]
    [InlineData("gameplay-class-physical-damage-hell.json", 0, 600)]
    public void ClassPhysicalDamageUsesExplicitAttributesAtSharedCheckpoint(
        string fixtureName,
        int expectedClassId,
        int expectedHitPoints)
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            fixtureName)));
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var initial = fixture.InitialState;
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var classDefinition = classes.GetRequired(checked((uint)initial.CharacterClass));
        var attributes = classDefinition.CreateStartingAttributes() with {
            Strength = new PlayerAttributeState(initial.Strength, initial.Strength),
            Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            Dexterity = new PlayerAttributeState(initial.Dexterity, initial.Dexterity),
            Vitality = new PlayerAttributeState(initial.Vitality, initial.Vitality),
        };
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingEquipment: initial.Equipment
                .Select(item => item.ToEquippedStoreItem())
                .ToArray(),
            startingAttributes: attributes,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                armorClass: initial.CombatTargetArmorClass,
                maxHitPoints: initial.CombatTargetMaxHitPoints)],
            startingPlayerClasses: classes,
            startingPlayerClass: classDefinition,
            difficulty: fixture.Difficulty,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var beforeAttack = Assert.Single(
            executor.CreateSnapshot(initial.Player, 1, 0).Players);
        Assert.Equal((uint)expectedClassId, beforeAttack.CharacterClassId);
        Assert.Equal(initial.Dexterity, beforeAttack.Attributes.Dexterity.Current);
        Assert.Equal(4, Assert.Single(beforeAttack.Equipment).State.MinDamage);
        Assert.Equal(4, Assert.Single(beforeAttack.Equipment).State.MaxDamage);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(expectedHitPoints, Assert.Single(result.FinalSnapshot.Monsters).HitPoints);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void ElementalEquipmentAttackMatchesNativeReplayCheckpoint()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-elemental-equipment.json")));
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingEquipment: initial.Equipment
                .Select(item => item.ToEquippedStoreItem())
                .ToArray(),
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                armorClass: initial.CombatTargetArmorClass,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                fireResistance: initial.CombatTargetFireResistance,
                lightningResistance: initial.CombatTargetLightningResistance)],
            startingCombatRules: new AuthoritativeCombatRules(10, 1, 100),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(82, Assert.Single(result.FinalSnapshot.Monsters).HitPoints);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void RangedElementalAttackSelectsArrowChannelsAtSharedCheckpoint()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-elemental-arrows.json")));
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingEquipment: initial.Equipment
                .Select(item => item.ToEquippedStoreItem())
                .ToArray(),
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints)],
            startingCombatRules: new AuthoritativeCombatRules(1, 1, 100),
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var arrowTarget = Assert.Single(result.FinalSnapshot.Monsters);
        Assert.Equal(100, arrowTarget.HitPoints);
        var arrow = Assert.Single(result.FinalSnapshot.Projectiles);
        Assert.Equal(58, arrow.Behavior);
        Assert.Equal(64, arrow.DamageQ6);
        Assert.Equal(4, arrow.ElementalFireDamage);
        Assert.Equal(22695478U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void BaseStorePurchaseAndSaleUseTheCheckedInCatalog()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "base-content-purchase.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(fixturePath));
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var items = AuthoritativeItemCatalog.LoadTsv("items.tsv", File.ReadAllText(Path.Combine(contentRoot, "items.tsv")));
        var catalog = StoreCatalog.LoadTsv("stores.tsv", File.ReadAllText(Path.Combine(contentRoot, "stores.tsv")), items);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingRandomSeed: checked((uint)fixture.RngSeed));

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal(43U, Assert.Single(result.FinalSnapshot.Players).Gold);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void PlayerActionLifecycleUsesBaseAnimationContentAtSharedCheckpoints()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-action-lifecycle.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(fixturePath));
        var timing = Assert.IsType<ReplayPlayerActionTiming>(fixture.PlayerActionTiming);
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var animation = animations.GetRequired(
            checked((uint)fixture.InitialState.CharacterClass));
        var attack = animation.GetAttack(
            DiabloPlayerAttackAnimationProfile.Unarmed);
        Assert.Equal(animation.WalkingFrames, timing.WalkingFrames);
        Assert.Equal(attack.Frames, timing.AttackFrames);
        Assert.Equal(attack.ActionFrame, timing.AttackActionFrame);
        Assert.Equal(animation.CastingFrames, timing.CastingFrames);
        Assert.Equal(animation.CastingActionFrame, timing.CastingActionFrame);
        Assert.Equal(animation.DeathFrames, timing.DeathFrames);

        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLifeMaximum: 40,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                fixture.InitialState.CombatTargetEntityId,
                fixture.InitialState.CombatTargetPositionX,
                fixture.InitialState.CombatTargetPositionY,
                fixture.InitialState.CombatTargetHitPoints,
                maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints)],
            startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
            startingMemorizedSpells: fixture.InitialState.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingPlayerAnimations: animations,
            useNativePlayerActionTiming: true);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal(1, player.PositionX);
        Assert.Equal(100U, player.Experience);
        Assert.Equal(40, player.Life);
        Assert.Equal(5, player.Mana);
        Assert.Equal(PlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Theory]
    [InlineData(
        "gameplay-player-action-ranged-diablo.json",
        DiabloGameVariant.Diablo,
        15UL,
        16UL,
        20UL,
        16UL)]
    [InlineData(
        "gameplay-player-action-ranged-hellfire.json",
        DiabloGameVariant.Hellfire,
        16UL,
        17UL,
        21UL,
        17UL)]
    public void RangedPlayerActionUsesSharedEquipmentAndVariantTiming(
        string fixtureName,
        DiabloGameVariant expectedVariant,
        ulong expectedEffectTick,
        ulong expectedReadyTick,
        ulong expectedEndTick,
        ulong chainedMoveTick)
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            fixtureName);
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(fixturePath));
        Assert.Equal(expectedVariant, fixture.GameVariant);
        var timing = Assert.IsType<ReplayPlayerActionTiming>(
            fixture.PlayerActionTiming);
        var repositoryRoot = FindRepositoryRoot();
        var basePack = fixture.GameVariant == DiabloGameVariant.Hellfire
            ? ContentPackLoader.LoadManifestDirectory(
                Path.Combine(repositoryRoot, "server", "content", "base"))
            : ContentPackLoader.LoadDirectory(
                "devilution.base",
                "0.1.0",
                Path.Combine(repositoryRoot, "server", "content", "base"));
        var packs = new List<ContentPack> { basePack };
        if (fixture.GameVariant == DiabloGameVariant.Hellfire) {
            packs.Add(ContentPackLoader.LoadManifestDirectory(
                Path.Combine(repositoryRoot, "server", "content", "hellfire")));
        }
        var contentManifest = new ContentManifest(
            fixture.GameVariant == DiabloGameVariant.Hellfire ? "hellfire" : "devilution.base",
            fixture.GameVariant == DiabloGameVariant.Hellfire ? "1" : "0.1.0",
            packs);
        AssertUsesContentManifest(fixture, contentManifest);
        var composed = ComposedContent.Compose(contentManifest);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            composed.GetRequiredTable("classes.tsv").ToCanonicalTsv());
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            composed.GetRequiredTable("class_animations.tsv").ToCanonicalTsv(),
            classes);
        var animation = animations.GetRequired(
            checked((uint)fixture.InitialState.CharacterClass));
        var bow = animation.GetAttack(DiabloPlayerAttackAnimationProfile.Bow);
        Assert.Equal(animation.WalkingFrames, timing.WalkingFrames);
        Assert.Equal(bow.Frames, timing.AttackFrames);
        Assert.Equal(bow.ActionFrame, timing.AttackActionFrame);
        Assert.Equal(animation.CastingFrames, timing.CastingFrames);
        Assert.Equal(animation.CastingActionFrame, timing.CastingActionFrame);
        Assert.Equal(animation.DeathFrames, timing.DeathFrames);
        var equippedBow = Assert.Single(fixture.InitialState.Equipment);
        Assert.Equal(4U, equippedBow.Slot);
        Assert.Equal(3, equippedBow.ItemType);
        Assert.Equal(1, equippedBow.ItemClass);
        Assert.Equal(1U << 18, equippedBow.Flags);
        Assert.True(equippedBow.StatFlag);
        Assert.Equal(
            chainedMoveTick,
            fixture.Commands.Single(command => command.Kind == "Move").TargetTick);

        StoreSimulationExecutor CreateExecutor()
        {
            return new StoreSimulationExecutor(
                new StoreCatalog(),
                fixture.InitialState.Gold,
                fixture.InitialState.Experience,
                fixture.InitialState.Life,
                fixture.InitialState.Mana,
                startingEquipment: fixture.InitialState.Equipment
                    .Select(item => item.ToEquippedStoreItem())
                    .ToArray(),
                startingAttributes: classes
                    .GetRequired(checked((uint)fixture.InitialState.CharacterClass))
                    .CreateStartingAttributes(),
                startingManaMaximum: fixture.InitialState.ManaMaximum,
                startingPositionX: fixture.InitialState.PositionX,
                startingPositionY: fixture.InitialState.PositionY,
                startingLifeMaximum: fixture.InitialState.Life,
                startingCharacterLevel: fixture.InitialState.CharacterLevel,
                startingCombatTargets: [new AuthoritativeCombatTarget(
                    fixture.InitialState.CombatTargetEntityId,
                    fixture.InitialState.CombatTargetPositionX,
                    fixture.InitialState.CombatTargetPositionY,
                    fixture.InitialState.CombatTargetHitPoints,
                    maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints)],
                startingCombatRules: AuthoritativeCombatRules.LoadTsv(
                    "combat.tsv",
                    composed.GetRequiredTable("combat.tsv").ToCanonicalTsv()),
                startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
                startingRandomSeed: checked((uint)fixture.RngSeed),
                startingPlayerClasses: classes,
                startingPlayerAnimations: animations,
                useNativePlayerActionTiming: true);
        }

        var probeExecutor = CreateExecutor();
        var probeServer = new AuthoritativeCommandServer(probeExecutor);
        var attackResult = probeServer.Process(
            fixture.InitialState.Player,
            new Command {
                ClientSequence = 1,
                RequestedTick = 10,
                AttackRequested = new AttackRequested {
                    TargetEntityId = fixture.InitialState.CombatTargetEntityId,
                },
            },
            10);
        Assert.Equal(CommandStatus.Accepted, attackResult.Status);
        var attackPlayer = Assert.Single(
            probeExecutor.CreateSnapshot(fixture.InitialState.Player, 1, 10).Players);
        Assert.Equal(PlayerCombatMode.RangedAttack, attackPlayer.CombatMode);
        Assert.Equal((expectedEffectTick, expectedReadyTick, expectedEndTick), (
            attackPlayer.CurrentAction!.EffectTick,
            attackPlayer.CurrentAction.ReadyTick,
            attackPlayer.CurrentAction.EndTick));

        var executor = CreateExecutor();
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.All(
            result.Results,
            command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal(1, player.PositionX);
        Assert.Equal(100U, player.Experience);
        Assert.Equal(PlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void PlayerDeathCancelsMovementDropsGoldAndUsesBaseContentRules()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-death-lifecycle.json")));
        var timing = Assert.IsType<ReplayPlayerActionTiming>(
            fixture.PlayerActionTiming);
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "combat.tsv")));
        var animation = animations.GetRequired(
            checked((uint)fixture.InitialState.CharacterClass));
        Assert.Equal(animation.DeathFrames, timing.DeathFrames);
        Assert.Equal(2, rules.DeathGoldDivisor);
        Assert.Equal(5000, rules.DeathGoldMaximumStack);
        Assert.Equal(11, rules.DeathGoldItemType);
        Assert.Equal(0, rules.DeathGoldItemIndex);
        Assert.Equal(50, rules.DeathDropSearchRadius);

        StoreSimulationExecutor CreateExecutor()
        {
            var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
            IReadOnlyList<OwnedStoreItem>? inventory = null;
            if (fixture.InitialState.GoldStackSeed != 0) {
                inventoryGrid[checked((int)fixture.InitialState.GoldStackCell)] = 0;
                inventory = [new OwnedStoreItem(
                    0,
                    0,
                    fixture.InitialState.GoldStackSeed,
                    fixture.InitialState.Gold,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = rules.DeathGoldItemType,
                        Value = checked((int)fixture.InitialState.Gold),
                        IdentifiedValue = checked((int)fixture.InitialState.Gold),
                        ItemIndex = rules.DeathGoldItemIndex,
                        StatFlag = true,
                    })];
            }
            return new StoreSimulationExecutor(
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
                startingCombatTargets: [new AuthoritativeCombatTarget(
                    fixture.InitialState.CombatTargetEntityId,
                    fixture.InitialState.CombatTargetPositionX,
                    fixture.InitialState.CombatTargetPositionY,
                    fixture.InitialState.CombatTargetHitPoints,
                    armorClass: fixture.InitialState.CombatTargetArmorClass,
                    maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
                    attackDamage: fixture.InitialState.CombatTargetAttackDamage,
                    aggroRange: fixture.InitialState.CombatTargetAggroRange)],
                startingCombatRules: rules,
                startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
                startingInventory: inventory,
                startingInventoryGrid: inventoryGrid,
                startingRandomSeed: checked((uint)fixture.RngSeed),
                startingPlayerClasses: classes,
                startingPlayerAnimations: animations,
                useNativePlayerActionTiming: true);
        }

        var probeExecutor = CreateExecutor();
        var probeServer = new AuthoritativeCommandServer(probeExecutor);
        _ = probeExecutor.CreateSnapshot(
            fixture.InitialState.Player,
            entityId: 1,
            tick: 0);
        var move = Assert.Single(fixture.Commands);
        var moveResult = probeServer.Process(
            fixture.InitialState.Player,
            new Command {
                ClientSequence = move.ClientSequence,
                RequestedTick = move.TargetTick,
                MoveRequested = new MoveRequested {
                    DirectionX = move.DirectionX,
                    DirectionY = move.DirectionY,
                },
            },
            move.TargetTick);
        Assert.Equal(CommandStatus.Accepted, moveResult.Status);
        probeServer.AdvanceTo(2);
        var deathSnapshot = probeExecutor.CreateSnapshot(
            fixture.InitialState.Player,
            1,
            2);
        var death = Assert.Single(deathSnapshot.Players);
        Assert.Equal(0, death.Life);
        Assert.Equal(PlayerCombatMode.Death, death.CombatMode);
        Assert.Equal(41UL, death.CombatModeEndTick);
        Assert.Equal(PlayerActionKind.Death, death.CurrentAction!.Kind);
        Assert.True(death.CurrentAction.EffectApplied);
        Assert.Equal(9U, death.CurrentAction.TargetEntityId);
        Assert.Equal((2UL, 2UL, 41UL, 41UL), (
            death.CurrentAction.StartedAtTick,
            death.CurrentAction.EffectTick,
            death.CurrentAction.ReadyTick,
            death.CurrentAction.EndTick));
        Assert.Equal(50U, death.Gold);
        Assert.Equal(51, Assert.Single(death.Inventory).State.Value);
        var probeGold = Assert.Single(deathSnapshot.WorldItems);
        Assert.Equal((0, 1), (probeGold.PositionX, probeGold.PositionY));
        Assert.Equal(50U, probeGold.Price);
        Assert.Equal(11, probeGold.State.ItemType);
        Assert.Equal(0, probeGold.State.ItemIndex);
        Assert.Equal(50, probeGold.State.Value);
        Assert.True(probeGold.State.StatFlag);
        Assert.Equal(probeGold.ItemSeed, deathSnapshot.RandomState);

        var executor = CreateExecutor();
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.All(
            result.Results,
            command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal(0, player.Life);
        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
        Assert.Equal(PlayerCombatMode.Death, player.CombatMode);
        Assert.Equal(0UL, player.CombatModeEndTick);
        Assert.Null(player.CurrentAction);
        Assert.Equal(50U, player.Gold);
        Assert.Equal(51, Assert.Single(player.Inventory).State.Value);
        Assert.Equal(50U, Assert.Single(result.FinalSnapshot.WorldItems).Price);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void PlayerDeathDropsEquipmentWithNativeFacingAndSlotPlacement()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-equipped-death-drops.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "combat.tsv")));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingEquipment: fixture.InitialState.Equipment
                .Select(item => item.ToEquippedStoreItem())
                .ToArray(),
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLifeMaximum: fixture.InitialState.LifeMaximum,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                fixture.InitialState.CombatTargetEntityId,
                fixture.InitialState.CombatTargetPositionX,
                fixture.InitialState.CombatTargetPositionY,
                fixture.InitialState.CombatTargetHitPoints,
                maxHitPoints: fixture.InitialState.CombatTargetMaxHitPoints,
                attackDamage: fixture.InitialState.CombatTargetAttackDamage,
                aggroRange: fixture.InitialState.CombatTargetAggroRange)],
            startingCombatRules: rules,
            startingQuests: [new AuthoritativeQuestState(
                fixture.InitialState.QuestId,
                fixture.InitialState.LevelId,
                fixture.InitialState.QuestRequiredProgress)],
            startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingPlayerAnimations: animations,
            useNativePlayerActionTiming: true,
            startingFacingDirection: fixture.InitialState.FacingDirection);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(0, player.Life);
        Assert.Equal(PlayerDirection.East, player.FacingDirection);
        Assert.Empty(player.Equipment);
        var drops = result.FinalSnapshot.WorldItems.OrderBy(item => item.EntityId).ToArray();
        Assert.Equal([101U, 102U, 103U], drops.Select(item => item.ItemSeed).ToArray());
        Assert.Equal([(10, 9), (9, 11), (10, 11)], drops
            .Select(item => (item.PositionX, item.PositionY))
            .ToArray());
        Assert.All(drops, item => Assert.Equal(34U, item.Price));
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void RestartTownUsesBaseContentAndMatchesNativeTransitionTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-restart-town.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "levels.tsv")));
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "combat.tsv")));
        rules.ValidateWorld(world);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                levelId: initial.CombatTargetLevelId,
                attackDamage: initial.CombatTargetAttackDamage,
                aggroRange: initial.CombatTargetAggroRange)],
            startingLevelId: initial.LevelId,
            startingWorld: world,
            startingCombatRules: rules,
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingPlayerAnimations: animations,
            useNativePlayerActionTiming: true);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal((0U, 75, 68), (
            player.LevelId,
            player.PositionX,
            player.PositionY));
        Assert.Equal((64, 0), (player.Life, player.Mana));
        Assert.Equal(PlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void HolyBoltUsesBaseContentAndMatchesNativeResolutionTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-holy-bolt-resolution.json")));
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                monsterLevel: initial.CombatTargetMonsterLevel,
                undead: initial.CombatTargetUndead)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(9U, monster.EntityId);
        Assert.Equal(4, monster.HitPoints);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void HolyBoltPassesThroughLivingTargetAndMatchesNativeResolutionTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-holy-bolt-living-target.json")));
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                monsterLevel: initial.CombatTargetMonsterLevel,
                undead: initial.CombatTargetUndead)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(9U, monster.EntityId);
        Assert.Equal(20, monster.HitPoints);
        Assert.NotEmpty(result.FinalSnapshot.Projectiles);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }
    [Fact]
    public void FireboltUsesBaseContentAndMatchesNativeResolutionTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-firebolt-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                fireResistance: initial.CombatTargetFireResistance,
                monsterLevel: initial.CombatTargetMonsterLevel)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(680, player.Mana);
        Assert.Equal(8, monster.HitPoints);
        Assert.Equal(464, monster.HitPointsQ6);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(0x3F10CD47U, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void FireballUsesBaseContentAndMatchesNativeAreaImpactTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-fireball-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                fireResistance: initial.CombatTargetFireResistance,
                monsterLevel: initial.CombatTargetMonsterLevel,
                petrified: initial.CombatTargetPetrified)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(1104, player.Mana);
        Assert.Equal(156, monster.HitPoints);
        Assert.Equal(9926, monster.HitPointsQ6);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(0xE8098E4AU, result.FinalSnapshot.RandomState);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void PvpDeathCreatesTheNativeEarAndMatchesTheSharedReplayTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-pvp-ear-death.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "items.tsv")));
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "combat.tsv")));
        rules.ValidateItems(items);
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingSpells: spells,
            startingCombatRules: rules,
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            gameMode: fixture.GameMode,
            startingCharacterName: initial.CharacterName,
            startingItems: items);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(["Caster", "Dead-RogueDM"], result.FinalSnapshot.Players
            .Select(player => player.CharacterName)
            .ToArray());
        var victim = result.FinalSnapshot.Players[1];
        Assert.Equal(0, victim.Life);
        Assert.Equal(PlayerCombatMode.Death, victim.CombatMode);
        var ear = Assert.Single(result.FinalSnapshot.WorldItems);
        Assert.Equal((1633955154U, 0U), (ear.ItemSeed, ear.Price));
        Assert.Equal((17509U, 23, 44, 3), (
            ear.State.CreateInfo,
            ear.State.ItemIndex,
            ear.State.MiscId,
            ear.State.Value));
        Assert.Equal(("Ear of Dead-RogueDM", "Dead-RogueDM", 21), (
            ear.State.BaseName,
            ear.State.IdentifiedName,
            ear.State.NativeCursorGraphic));
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    [Fact]
    public void LightningUsesBaseContentAndMatchesNativeSegmentTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-lightning-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                lightningResistance: initial.CombatTargetLightningResistance,
                monsterLevel: initial.CombatTargetMonsterLevel,
                petrified: initial.CombatTargetPetrified,
                stoneCurseImmune: initial.CombatTargetStoneCurseImmune)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
        Assert.Equal(488, player.Mana);
        Assert.Equal(6, monster.HitPoints);
        Assert.Equal(384, monster.HitPointsQ6);
        Assert.Equal(6, result.FinalSnapshot.Projectiles.Count);
        Assert.Equal(0xCBBBD6A5U, result.FinalSnapshot.RandomState);
        Assert.DoesNotContain(
            result.FinalSnapshot.Projectiles,
            projectile => projectile.PositionX == 1
                && projectile.PositionY == 0);
    }

    [Fact]
    public void FlashUsesBaseContentAndMatchesNativeLayerTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-flash-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(initial.Magic, initial.Magic),
            },
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.Life,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                magicResistance: initial.CombatTargetMagicResistance,
                monsterLevel: initial.CombatTargetMonsterLevel,
                petrified: initial.CombatTargetPetrified,
                stoneCurseImmune: initial.CombatTargetStoneCurseImmune)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var monster = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(1336, player.Mana);
        Assert.Equal(97, monster.HitPoints);
        Assert.Equal(6165, monster.HitPointsQ6);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(0xE4BDD038U, result.FinalSnapshot.RandomState);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void HealingUsesBaseContentAndMatchesNativeRandomizedTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-healing-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
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
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal(5000, player.Life);
        Assert.Equal(1656, player.Mana);
        Assert.Equal(0x8C3C2F4FU, result.FinalSnapshot.RandomState);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void ManaShieldUsesBaseContentAndMatchesNativeDamageConversionTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-mana-shield-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                attackDamage: initial.CombatTargetAttackDamage,
                aggroRange: initial.CombatTargetAggroRange,
                petrified: initial.CombatTargetPetrified,
                stoneCurseImmune: initial.CombatTargetStoneCurseImmune)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingManaShieldActive: initial.ManaShieldActive,
            startingReflectCharges: initial.ReflectCharges);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal((1080, 0, false), (
            player.Life,
            player.Mana,
            player.ManaShieldActive));
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void StoneCurseUsesBaseContentAndMatchesNativeStatusTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-stone-curse-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingLevelId: initial.LevelId,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints,
                levelId: initial.CombatTargetLevelId,
                moving: initial.CombatTargetMoving,
                futurePositionX: initial.CombatTargetFuturePositionX,
                futurePositionY: initial.CombatTargetFuturePositionY,
                petrified: initial.CombatTargetPetrified,
                stoneCurseImmune: initial.CombatTargetStoneCurseImmune)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        var target = Assert.Single(result.FinalSnapshot.Monsters);

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal(3088, player.Mana);
        Assert.Equal((10, 9, false, false), (
            target.PositionX,
            target.PositionY,
            target.Moving,
            target.Petrified));
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void InfravisionConsumablesUseBaseContentAndMatchNativeStatusTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-infravision-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingBelt: initial.Belt.Select(item => item.ToBeltStoreItem()).ToArray(),
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingLevelId: initial.LevelId,
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Empty(player.Belt);
        Assert.Empty(player.StatusEffects);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void MonkSearchAbilityUsesBaseContentAndMatchesNativeAdditiveTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-search-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingLevelId: initial.LevelId,
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingAbilitySpellId: initial.AbilitySpellId,
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(
            [CommandStatus.Accepted, CommandStatus.Accepted],
            result.Results.Select(command => command.Status));
        Assert.Equal(960, player.Mana);
        Assert.Empty(player.StatusEffects);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void TeleportUsesBaseContentAndMatchesNativeCrawlTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-teleport-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                initial.CombatTargetEntityId,
                initial.CombatTargetPositionX,
                initial.CombatTargetPositionY,
                initial.CombatTargetHitPoints,
                maxHitPoints: initial.CombatTargetMaxHitPoints)],
            startingObjects: [new AuthoritativeWorldObject(
                initial.ObjectEntityId,
                initial.ObjectId,
                initial.LevelId,
                initial.ObjectPositionX,
                initial.ObjectPositionY,
                questId: initial.ObjectQuestId)],
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingPlayerAnimations: animations,
            useNativePlayerActionTiming: true);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        Assert.Equal(
            [1U, fixture.InitialState.SecondaryPlayer!.EntityId],
            result.FinalSnapshot.Players.Select(player => player.EntityId));
        var player = result.FinalSnapshot.Players[0];

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal((9, 10), (player.PositionX, player.PositionY));
        Assert.Equal(1592, player.Mana);
        Assert.Equal(PlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void PhasingUsesBaseContentAndMatchesNativeRandomMovementTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-phasing-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingPlayerAnimations: animations,
            useNativePlayerActionTiming: true,
            startingFacingDirection: initial.FacingDirection);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal((24, 15), (player.PositionX, player.PositionY));
        Assert.Equal(9488, player.Mana);
        Assert.Equal(PlayerDirection.East, player.FacingDirection);
        Assert.Equal(PlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(0x955E76D9U, result.FinalSnapshot.RandomState);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void ResurrectionUsesBaseContentAndMatchesNativeMultiplayerTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-resurrection-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingPlayerAnimations: animations,
            useNativePlayerActionTiming: true);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, 8U], result.FinalSnapshot.Players.Select(player => player.EntityId));
        var caster = result.FinalSnapshot.Players[0];
        var target = result.FinalSnapshot.Players[1];
        Assert.Equal(1720, caster.Mana);
        Assert.Equal(PlayerCombatMode.Stand, caster.CombatMode);
        Assert.Equal((640, 0), (target.Life, target.Mana));
        Assert.Equal((10, 10), (target.PositionX, target.PositionY));
        Assert.Equal(PlayerCombatMode.Stand, target.CombatMode);
        Assert.Null(target.CurrentAction);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void HealOtherUsesBaseContentAndMatchesNativeMultiplayerTimeline()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-heal-other-resolution.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));
        var initial = fixture.InitialState;
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            initial.Gold,
            initial.Experience,
            initial.Life,
            initial.Mana,
            startingManaMaximum: initial.ManaMaximum,
            startingPositionX: initial.PositionX,
            startingPositionY: initial.PositionY,
            startingLifeMaximum: initial.LifeMaximum,
            startingCharacterLevel: initial.CharacterLevel,
            startingSpells: spells,
            startingCombatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: fixture.GameVariant),
            startingCharacterClassId: checked((uint)initial.CharacterClass),
            startingMemorizedSpells: initial.MemorizedSpells
                .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                .ToArray(),
            startingRandomSeed: checked((uint)fixture.RngSeed),
            startingPlayerClasses: classes,
            startingPlayerAnimations: animations,
            useNativePlayerActionTiming: true,
            gameMode: fixture.GameMode);

        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal([1U, 8U], result.FinalSnapshot.Players.Select(player => player.EntityId));
        var caster = result.FinalSnapshot.Players[0];
        var target = result.FinalSnapshot.Players[1];
        Assert.Equal((1000, 2552), (caster.Life, caster.Mana));
        Assert.Equal(8488, target.Life);
        Assert.Equal(PlayerCombatMode.Stand, caster.CombatMode);
        Assert.Null(caster.CurrentAction);
        Assert.Empty(result.FinalSnapshot.Projectiles);
        Assert.Equal(0xCB5679F1U, result.FinalSnapshot.RandomState);
        Assert.True(
            fixture.Checkpoints
                .Select(checkpoint => checkpoint.StateSha256)
                .SequenceEqual(result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256)),
            string.Join(
                Environment.NewLine,
                result.Checkpoints.Select(checkpoint =>
                    $"tick {checkpoint.Tick}: {checkpoint.ActualStateSha256}")));
    }

    [Fact]
    public void PortalUseCreatesOneTickNewLevelBarrier()
    {
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "gameplay-player-level-transition.json")));
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        AssertUsesBaseContentManifest(fixture, contentRoot);
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);

        StoreSimulationExecutor CreateExecutor()
        {
            return new StoreSimulationExecutor(
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
                startingLevelId: fixture.InitialState.LevelId,
                startingPortals: [new AuthoritativePortal(5, 1, 0, 0, 2, 3, 4)],
                startingCharacterClassId: checked((uint)fixture.InitialState.CharacterClass),
                startingRandomSeed: checked((uint)fixture.RngSeed),
                startingPlayerClasses: classes,
                startingPlayerAnimations: animations,
                useNativePlayerActionTiming: true);
        }

        var probeExecutor = CreateExecutor();
        var probeServer = new AuthoritativeCommandServer(probeExecutor);
        var portalResult = probeServer.Process(
            fixture.InitialState.Player,
            new Command {
                ClientSequence = 1,
                RequestedTick = 1,
                UsePortalRequested = new UsePortalRequested { PortalId = 5 },
            },
            1);
        Assert.Equal(CommandStatus.Accepted, portalResult.Status);
        probeServer.AdvanceTo(1);
        var transition = Assert.Single(
            probeExecutor.CreateSnapshot(fixture.InitialState.Player, 1, 1).Players);
        Assert.Equal((2U, 3, 4), (
            transition.LevelId,
            transition.PositionX,
            transition.PositionY));
        Assert.Equal(PlayerCombatMode.NewLevel, transition.CombatMode);
        Assert.Equal(2UL, transition.CombatModeEndTick);
        Assert.Equal(
            PlayerActionKind.LevelTransition,
            transition.CurrentAction!.Kind);
        Assert.True(transition.CurrentAction.EffectApplied);
        Assert.Equal(5U, transition.CurrentAction.TargetEntityId);

        var executor = CreateExecutor();
        var result = ReplayFixtureExecutor.Execute(
            fixture,
            executor,
            new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal((2U, 3, 4), (
            player.LevelId,
            player.PositionX,
            player.PositionY));
        Assert.Equal(PlayerCombatMode.Stand, player.CombatMode);
        Assert.Equal(0UL, player.CombatModeEndTick);
        Assert.Null(player.CurrentAction);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
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
        Assert.True(
            string.Equals(manifest.Sha256, identity.Sha256, StringComparison.Ordinal),
            $"Expected base content manifest {manifest.Sha256}, fixture contains {identity.Sha256}.");
    }

    private static void AssertUsesContentManifest(
        ReplayFixture fixture,
        ContentManifest manifest)
    {
        var identity = Assert.IsType<ReplayContentManifest>(fixture.ContentManifest);
        Assert.Equal(manifest.Id, identity.Id);
        Assert.Equal(manifest.Version, identity.Version);
        Assert.True(
            string.Equals(manifest.Sha256, identity.Sha256, StringComparison.Ordinal),
            $"Expected content manifest {manifest.Sha256}, fixture contains {identity.Sha256}.");
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
