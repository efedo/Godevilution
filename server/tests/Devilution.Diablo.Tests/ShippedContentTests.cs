using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Smoke-tests the checked-in base content through the module-owned bootstrap. */
public sealed class ShippedContentTests
{
    [Fact]
    public void BaseContentPackUsesTheExplicitVersionedManifest()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);

        Assert.Equal("devilution.base", pack.Id);
        Assert.Equal("0.1.0", pack.Version);
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "1.168.0"),
            pack.Schema);
        Assert.Empty(pack.Overlays);
    }

    [Fact]
    public void BaseProceduralActivationCarriesTheShippedMonsterDrop()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("base", "1", [pack])),
            randomSeed: 0x1234,
            monsterSpawns: new DiabloMonsterSpawnConfiguration(1));

        var target = Assert.Single(activation.Simulation.WorldState.CombatTargets.Values);
        Assert.NotNull(target.Drop);
        Assert.Equal(
            AuthoritativeMonsterSpawnPlanner.FirstGeneratedDropEntityId,
            target.Drop!.EntityId);
        Assert.NotEqual(0U, target.Drop.ItemSeed);
        Assert.Equal(1, target.Drop.State.ItemIndex);
        Assert.NotNull(activation.MonsterMissiles);
        Assert.True(activation.MonsterMissiles!.TryGet("GoatRanged", out var goatRanged));
        Assert.Equal("Arrow", goatRanged.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, goatRanged.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles!.TryGet("Succubus", out var succubus));
        Assert.Equal("BloodStar", succubus.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, succubus.ProjectileBehavior);
        Assert.Equal(36U, succubus.SpellId);
        Assert.True(activation.MonsterMissiles.TryGet("Lich", out var lich));
        Assert.Equal("OrangeFlare", lich.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, lich.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("ArchLich", out var archLich));
        Assert.Equal("YellowFlare", archLich.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, archLich.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Psychorb", out var psychorb));
        Assert.Equal("BlueFlare", psychorb.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, psychorb.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Necromorb", out var necromorb));
        Assert.Equal("RedFlare", necromorb.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, necromorb.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("BoneDemon", out var boneDemon));
        Assert.Equal("BlueFlare2", boneDemon.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, boneDemon.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Storm", out var storm));
        Assert.Equal("ThinLightningControl", storm.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.Lightning, storm.ProjectileBehavior);
        Assert.Equal(3U, storm.SpellId);
        Assert.Equal(32, storm.ProjectileSpeed);
        Assert.Equal(256, storm.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Diablo", out var diablo));
        Assert.Equal("DiabloApocalypse", diablo.MissileId);
        Assert.Equal(40, diablo.ProjectileDamage);
        Assert.Equal(AuthoritativeProjectileBehavior.ApocalypseController, diablo.ProjectileBehavior);
        Assert.Equal(25U, diablo.SpellId);
        Assert.Equal(0, diablo.ProjectileSpeed);
        Assert.Equal(15, diablo.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Magma", out var magma));
        Assert.Equal("MagmaBall", magma.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, magma.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Mega", out var mega));
        Assert.Equal("InfernoControl", mega.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.InfernoController, mega.ProjectileBehavior);
        Assert.Equal(21U, mega.SpellId);
        Assert.Equal(32, mega.ProjectileSpeed);
        Assert.Equal(256, mega.ProjectileLifetime);
        Assert.Equal((5, 40, 10, 3, 5, 5, 1, 2), (
            mega.InfernoMaximumDistance,
            mega.InfernoAdjacentChanceBase,
            mega.InfernoAdjacentChanceIntelligenceScale,
            mega.InfernoRangedFarDistance,
            mega.InfernoRangedChanceBase,
            mega.InfernoRangedChanceIntelligenceScale,
            mega.InfernoRangedFarIntelligenceOffset,
            mega.InfernoAdjacentChoiceModulo));
        Assert.True(activation.MonsterMissiles.TryGet("Acid", out var acid));
        Assert.Equal("Acid", acid.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.Acid, acid.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Acid, acid.DamageType);
        Assert.Equal(20, acid.ProjectileLifetimeBase);
        Assert.Equal(5, acid.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(15, acid.PuddleDurationRandomRange);
        Assert.Equal(40, acid.PuddleDurationBase);
        Assert.Equal(40, acid.PuddleDurationIntelligenceScale);
        Assert.Equal(2, acid.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(1, acid.PuddleDamageLow);
        Assert.Equal(2, acid.PuddleDamageHigh);
        Assert.Equal((2, 3, 500, 500, 1), (
            acid.RangedAvoidanceMinimumDistance,
            acid.RangedAvoidanceFarDistance,
            acid.RangedAvoidanceChanceBase,
            acid.RangedAvoidanceChanceIntelligenceScale,
            acid.RangedAvoidanceFarIntelligenceOffset));
        Assert.True(activation.MonsterMissiles.TryGet("AcidUnique", out var acidUnique));
        Assert.Equal("Acid", acidUnique.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.Acid, acidUnique.ProjectileBehavior);
        Assert.Equal(20, acidUnique.ProjectileLifetimeBase);
        Assert.Equal(5, acidUnique.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(15, acidUnique.PuddleDurationRandomRange);
        Assert.Equal(40, acidUnique.PuddleDurationBase);
        Assert.Equal(40, acidUnique.PuddleDurationIntelligenceScale);
        Assert.Equal(2, acidUnique.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(1, acidUnique.PuddleDamageLow);
        Assert.Equal(2, acidUnique.PuddleDamageHigh);
        Assert.Equal((4, 70, 10), (
            acidUnique.RangedRetreatMinimumDistance,
            acidUnique.RangedRetreatChanceBase,
            acidUnique.RangedRetreatChanceIntelligenceScale));
        Assert.True(activation.MonsterMissiles.TryGet("SkeletonRanged", out var skeletonRanged));
        Assert.Equal("Arrow", skeletonRanged.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, skeletonRanged.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Snake", out var snake));
        Assert.Equal("Rhino", snake.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.SnakeCharge, snake.ProjectileBehavior);
        Assert.Null(snake.DamageType);
        Assert.Equal(2, snake.ChargeMinimumDistance);
        Assert.Equal(18, snake.ProjectileSpeed);
        Assert.Equal(256, snake.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Bat", 42, out var gloom));
        Assert.Equal("Rhino", gloom.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.BatCharge, gloom.ProjectileBehavior);
        Assert.Equal(42U, gloom.MonsterId);
        Assert.Equal((5, 33, 4), (
            gloom.ChargeMinimumDistance,
            gloom.ChargeChanceBase,
            gloom.ChargeChanceIntelligenceScale));
        Assert.Equal(18, gloom.ProjectileSpeed);
        Assert.Equal(256, gloom.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 107, out var counselorFirebolt));
        Assert.Equal(("Firebolt", AuthoritativeProjectileBehavior.Firebolt), (
            counselorFirebolt.MissileId,
            counselorFirebolt.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 108, out var counselorChargedBolt));
        Assert.Equal(("ChargedBolt", AuthoritativeProjectileBehavior.MonsterChargedBolt), (
            counselorChargedBolt.MissileId,
            counselorChargedBolt.ProjectileBehavior));
        Assert.Equal(30U, counselorChargedBolt.SpellId);
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 109, out var counselorLightning));
        Assert.Equal(("LightningControl", AuthoritativeProjectileBehavior.Lightning), (
            counselorLightning.MissileId,
            counselorLightning.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 110, out var counselorFireball));
        Assert.Equal(("Fireball", AuthoritativeProjectileBehavior.Fireball), (
            counselorFireball.MissileId,
            counselorFireball.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 113, out var counselorArchbishop));
        Assert.Equal(("Fireball", AuthoritativeProjectileBehavior.Fireball), (
            counselorArchbishop.MissileId,
            counselorArchbishop.ProjectileBehavior));
    }

    [Fact]
    public void HellfireOverlaySelectsTheExternalNativeGameVariant()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var manifest = new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]);

        var composed = ComposedContent.Compose(manifest);
        var combat = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            composed.GetRequiredTable("combat.tsv").ToCanonicalTsv());

        Assert.Equal("devilution.hellfire", hellfirePack.Id);
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "1.168.0"),
            hellfirePack.Schema);
        Assert.Equal(DiabloGameVariant.Hellfire, combat.GameVariant);
        Assert.Equal((15, 10, 20, 25, 30, 2, 30, 2, 10, 2, 2, 40, 2, 500, 10, 40, 75, 8, 3, 5, 3, 5, 40, 20), (
            combat.MonsterMeleeMinimumHitChance,
            combat.MonsterRangedMinimumHitChance,
            combat.MonsterMinimumHitChanceLevel14,
            combat.MonsterMinimumHitChanceLevel15,
            combat.MonsterMinimumHitChanceLevel16,
            combat.MonsterToHitLevelScale,
            combat.MonsterToHitBaseBonus,
            combat.MonsterRangedDistancePenalty,
            combat.MonsterMagicMinimumHitChance,
            combat.MonsterMagicToHitMonsterLevelScale,
            combat.MonsterMagicToHitPlayerLevelScale,
            combat.MonsterMagicToHitBaseBonus,
            combat.MonsterMagicDistancePenalty,
            combat.MonsterChargeBaseToHit,
            combat.MonsterAcidPuddleMinimumHitChance,
            combat.MonsterAcidPuddleBaseHitChance,
            combat.MaximumResistance,
            combat.RandomStealLifeDivisor,
            combat.StealLife3Percent,
            combat.StealLife5Percent,
            combat.StealMana3Percent,
            combat.StealMana5Percent,
            combat.HellfireArmorBonusAgainstDemons,
            combat.HellfireArmorBonusAgainstUndead));
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(composed);
        Assert.Equal(
            [0U, 1U, 2U, 3U, 4U, 9U, 17U, 18U, 19U, 20U, 21U, 22U, 23U, 24U],
            activation.Simulation.WorldState.World!.Levels.Select(level => level.LevelId));
        Assert.True(activation.Simulation.WorldState.World!.IsWalkable(24, 39, 39));
        Assert.Equal(5, activation.LevelGeneration!.Definitions.Count);
        Assert.Equal(25, activation.LevelGeneration.GetRequired(24).TrapPercent);
        Assert.NotNull(activation.MonsterSpawnPlanner);
        var plannedSpawns = activation.MonsterSpawnPlanner.Plan(
            24,
            seed: 0x1234,
            multiplayer: false,
            maximumMonsters: 3);
        Assert.Equal(3, plannedSpawns.Count);
        Assert.All(plannedSpawns, spawn => {
            Assert.Equal(24U, spawn.LevelId);
            Assert.True(spawn.Definition.MinimumDungeonLevel <= 24);
            Assert.True(spawn.Definition.MaximumDungeonLevel >= 24);
        });
        var proceduralActivation = DiabloModule.Instance.ContentBootstrap.Activate(
            composed,
            randomSeed: 0x1234,
            gameMode: DiabloGameMode.Multiplayer,
            monsterSpawns: new DiabloMonsterSpawnConfiguration(3, 24));
        Assert.Equal(3, proceduralActivation.Simulation.WorldState.CombatTargets.Count);
        Assert.All(
            proceduralActivation.Simulation.WorldState.CombatTargets.Values,
            target => {
                Assert.InRange(
                    target.EntityId,
                    AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId,
                    AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId + 2);
                Assert.Equal(24U, target.LevelId);
                Assert.True(target.HitPoints > 0);
                Assert.True(target.MonsterId > 0);
            });
        Assert.Equal(24, activation.QuestTypes!.Definitions.Count);
        Assert.Equal(13, activation.QuestPools!.Definitions.Count);
        Assert.Equal(9, activation.Simulation.WorldState.Quests.Count);
        var baseQuest = activation.Simulation.WorldState.Quests[3001];
        Assert.Equal(
            activation.QuestPools.SelectUnavailableQuestIds(0).Contains(baseQuest.QuestId),
            baseQuest.Unavailable);
        Assert.False(activation.Simulation.WorldState.Quests[3021].Active);
        Assert.Equal("Defiler", activation.Simulation.WorldState.Quests[3021].CompletionUniqueIdentity);
        Assert.Equal(1, activation.QuestTypes.Definitions[12].SetLevel);
        Assert.Equal("Archbishop Lazarus' Lair", activation.SetLevelNames!.GetDisplayName(5));
        var hellfireMonster = activation.Simulation.WorldState.CombatTargets[10];
        Assert.Equal(2U, hellfireMonster.MonsterId);
        Assert.True(hellfireMonster.IsUndead);
        Assert.True(activation.Simulation.WorldState.CombatTargets[14].IsAnimal);
        Assert.False(hellfireMonster.IsDemon);
        Assert.Equal(100, hellfireMonster.MagicResistance);
        Assert.Equal(16, hellfireMonster.DeathAnimationTicks);
        Assert.Equal(10, hellfireMonster.ToHit);
        Assert.Equal(0, hellfireMonster.ToHitSpecial);
        Assert.Equal("Zombie", hellfireMonster.NativeAi);
        Assert.Equal(0, hellfireMonster.Intelligence);
        Assert.Equal(11, hellfireMonster.MaxHitPoints);
        Assert.Equal(8, activation.Simulation.WorldState.CombatTargets.Count);
        Assert.Equal(25, activation.Simulation.WorldState.CombatTargets[11].MaxHitPoints);
        Assert.True(activation.Simulation.WorldState.CombatTargets[12].IsUndead);
        Assert.Equal(5, activation.Simulation.WorldState.CombatTargets[15].MaxDamageSpecial);
        var defiler = activation.Simulation.WorldState.CombatTargets[16];
        Assert.Equal(126U, defiler.MonsterId);
        Assert.True(defiler.IsUnique);
        Assert.Equal("Defiler", defiler.UniqueIdentity);
        Assert.Equal("SkeletonMelee", defiler.NativeAi);
        Assert.NotNull(defiler.Drop);
        Assert.Equal(43, defiler.Drop!.State.MiscId);
        Assert.Equal(42, defiler.Drop.State.ItemIndex);
        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());
        Assert.Equal(139, monsterTypes.Definitions.Count);
        Assert.Equal(6, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).AggroRange);
        Assert.Equal(16, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).DeathAnimationTicks);
        Assert.Equal(54U, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).ExperienceReward);
        Assert.Equal(10, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).ToHit);
        Assert.Equal("Zombie", monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).NativeAi);
        Assert.Equal(0, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).Intelligence);
        Assert.Equal(
            (4, 8, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Fallen")
                .Select(definition => (
                    definition.FallenFearRadius,
                    definition.FallenFearLevelOffset,
                    definition.FallenFearMinimumTicks))
                .Distinct()
                .Single());
        Assert.Equal(
            (5, 1, 3, 9),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Sneak")
                .Select(definition => (
                    definition.SneakDistanceBase,
                    definition.SneakDistanceIntelligenceScale,
                    definition.SneakRetreatReleaseDistance,
                    definition.SneakRetreatTicks))
                .Distinct()
                .Single());
        Assert.Equal(
            (8, 4, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Bat")
                .Select(definition => (
                    definition.BatAttackChanceBase,
                    definition.BatAttackChanceIntelligenceScale,
                    definition.BatRetreatDurationTicks))
                .Distinct()
                .Single());
        Assert.Equal(
            (35, 4, 20, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "SkeletonMelee")
                .Select(definition => (
                    definition.SkeletonMeleeDelayChanceBase,
                    definition.SkeletonMeleeDelayChanceIntelligenceScale,
                    definition.SkeletonMeleeAttackChanceBase,
                    definition.SkeletonMeleeAttackChanceIntelligenceScale))
                .Distinct()
                .Single());
        Assert.Equal(
            (4, 13, 2, 3, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "SkeletonRanged")
                .Select(definition => (
                    definition.SkeletonRangedRetreatMinimumDistance,
                    definition.SkeletonRangedRetreatChanceBase,
                    definition.SkeletonRangedRetreatChanceIntelligenceScale,
                    definition.SkeletonRangedAttackChanceBase,
                    definition.SkeletonRangedAttackChanceIntelligenceScale))
                .Distinct()
                .Single());
        Assert.Equal(
            (28, 2, 23, 2, 50, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "GoatMelee")
                .Select(definition => (
                    definition.GoatMeleeMovementChanceBase,
                    definition.GoatMeleeMovementChanceIntelligenceScale,
                    definition.GoatMeleeAttackChanceBase,
                    definition.GoatMeleeAttackChanceIntelligenceScale,
                    definition.GoatMeleeSpecialHealthThresholdPercent,
                    definition.GoatMeleeSpecialChanceGroupPeriod))
                .Distinct()
                .Single());
        Assert.Equal(
            (20, 4, 20, 4, 15, 4),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Fat")
                .Select(definition => (
                    definition.FatMovementChanceBase,
                    definition.FatMovementChanceIntelligenceScale,
                    definition.FatAttackChanceBase,
                    definition.FatAttackChanceIntelligenceScale,
                    definition.FatSpecialChanceBase,
                    definition.FatSpecialChanceIntelligenceScale))
                .Distinct()
                .Single());
        Assert.Equal(
            (10, 2, 20, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Zombie")
                .Select(definition => (
                    definition.ZombieActionChanceBase,
                    definition.ZombieActionChanceIntelligenceScale,
                    definition.ZombieRandomDirectionChanceBase,
                    definition.ZombieRandomDirectionChanceIntelligenceScale))
                .Distinct()
                .Single());
        Assert.Equal(
            (2, 4, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Zombie")
                .Select(definition => (
                    definition.ZombieMovementDistanceMinimum,
                    definition.ZombieMovementDistanceBase,
                    definition.ZombieMovementDistanceIntelligenceScale))
                .Distinct()
                .Single());
        var toadDemon = monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 54);
        Assert.Equal(("Fat", 40, 8, 20), (
            toadDemon.NativeAi,
            toadDemon.ToHitSpecial,
            toadDemon.MinimumSpecialDamage,
            toadDemon.MaximumSpecialDamage));
        var hornedDemon = monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 64);
        Assert.Equal(("Rhino", 0, 100, 5, 32), (
            hornedDemon.NativeAi,
            hornedDemon.Intelligence,
            hornedDemon.ToHitSpecial,
            hornedDemon.MinimumSpecialDamage,
            hornedDemon.MaximumSpecialDamage));
        Assert.Equal(
            [0, 1, 2, 3],
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Rhino")
                .OrderBy(definition => definition.NativeMonsterId)
                .Select(definition => definition.Intelligence));
        Assert.Equal((5, 43, 2), monsterTypes.Definitions
            .Where(definition => definition.NativeAi == "Rhino")
            .Select(definition => (
                definition.RhinoChargeMinimumDistance,
                definition.RhinoChargeChanceBase,
                definition.RhinoChargeChanceIntelligenceScale))
            .Distinct()
            .Single());
        Assert.Equal(
            "HorkDemon",
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).NativeAi);
        Assert.Equal((28, 2, 3, 43, 2, 33, 2), (
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonAttackChanceBase,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonAttackChanceIntelligenceScale,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonSpawnMinimumDistance,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonSpawnChanceBase,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonSpawnChanceIntelligenceScale,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonMovementChanceBase,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonMovementChanceIntelligenceScale));
        Assert.Equal((2, 1, 50, 16, 5, 4), monsterTypes.Definitions
            .Where(definition => definition.NativeAi == "Gargoyle")
            .Select(definition => (
                definition.GargoyleSafeDistanceBase,
                definition.GargoyleSafeDistanceIntelligenceScale,
                definition.GargoyleHealingHealthThresholdPercent,
                definition.GargoyleHealDivisorMultiplier,
                definition.GargoyleHealDivisorRandomRange,
                definition.GargoyleHealDivisorRandomOffset))
            .Distinct()
            .Single());
        Assert.Equal((10, 4, 8, 1, 75, 50), monsterTypes.Definitions
            .Where(definition => definition.NativeAi == "Scavenger")
            .Select(definition => (
                definition.ScavengerHealingDurationTicks,
                definition.ScavengerCorpseSearchRadius,
                definition.ScavengerHellfireHealingDivisor,
                definition.ScavengerBaseHealingAmount,
                definition.ScavengerBaseHealingTargetPercent,
                definition.ScavengerHealingHealthThresholdPercent))
            .Distinct()
            .Single());
        Assert.True(activation.MonsterMissiles!.TryGet("HorkDemon", out var horkDemonMissile));
        Assert.Equal(("HorkSpawn", AuthoritativeProjectileBehavior.HorkSpawn), (
            horkDemonMissile!.MissileId,
            horkDemonMissile!.ProjectileBehavior));
        Assert.Equal(117U, horkDemonMissile.SpawnMonsterId);
        Assert.Equal(8, horkDemonMissile.ProjectileSpeed);
        Assert.Equal(9, horkDemonMissile.ProjectileLifetime);
        Assert.Equal(48, monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 85).DeathAnimationTicks);
        Assert.Equal(42, monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 129).DeathAnimationTicks);
        Assert.Equal(12, monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 109).DeathAnimationTicks);
        Assert.Equal(7, monsterTypes.Definitions.Single(definition => definition.MonsterId == 5).AggroRange);
        Assert.Equal(73, monsterTypes.Definitions.Count(definition => definition.Demon));
        Assert.Equal(4, monsterTypes.Definitions.Count(definition => definition.Knockback));
        Assert.True(monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 110).Knockback);
        Assert.Equal("Zombie", monsterTypes.Definitions[1].Name);
        Assert.Equal(0U, monsterTypes.Definitions[1].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[1].MagicResistance);
        Assert.Equal("Carver", monsterTypes.Definitions[6].Name);
        Assert.Equal("Horror", monsterTypes.Definitions[12].Name);
        Assert.True(monsterTypes.Definitions[12].Undead);
        Assert.Equal(11U, monsterTypes.Definitions[12].NativeMonsterId);
        Assert.Equal("Bone Gasher", monsterTypes.Definitions[20].Name);
        Assert.Equal(19U, monsterTypes.Definitions[20].NativeMonsterId);
        Assert.Equal(75, monsterTypes.Definitions[20].LightningResistance);
        Assert.Equal("Horror", monsterTypes.Definitions[24].Name);
        Assert.Equal(23U, monsterTypes.Definitions[24].NativeMonsterId);
        Assert.Equal("Invisible Lord", monsterTypes.Definitions[29].Name);
        Assert.Equal(28U, monsterTypes.Definitions[29].NativeMonsterId);
        Assert.True(monsterTypes.Definitions[29].Demon);
        Assert.Equal((75, 75, 75), (
            monsterTypes.Definitions[29].FireResistance,
            monsterTypes.Definitions[29].LightningResistance,
            monsterTypes.Definitions[29].MagicResistance));
        Assert.Equal("Stone Clan", monsterTypes.Definitions[36].Name);
        Assert.Equal(35U, monsterTypes.Definitions[36].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[36].MagicResistance);
        Assert.Equal("Night Clan", monsterTypes.Definitions[38].Name);
        Assert.Equal((30, 30), (
            monsterTypes.Definitions[38].MinimumSpecialDamage,
            monsterTypes.Definitions[38].MaximumSpecialDamage));
        Assert.Equal(100, monsterTypes.Definitions[38].MagicResistance);
        Assert.Equal((100, 75), (
            monsterTypes.Definitions[42].LightningResistance,
            monsterTypes.Definitions[42].MagicResistance));
        Assert.Equal("Poison Spitter", monsterTypes.Definitions[48].Name);
        Assert.Equal(47U, monsterTypes.Definitions[48].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[48].MagicResistance);
        Assert.Equal(100, monsterTypes.Definitions[48].AcidResistance);
        Assert.Equal("Pit Beast", monsterTypes.Definitions[49].Name);
        Assert.Equal((75, 100), (
            monsterTypes.Definitions[49].LightningResistance,
            monsterTypes.Definitions[49].MagicResistance));
        Assert.Equal("Skeleton King", monsterTypes.Definitions[51].Name);
        Assert.True(monsterTypes.Definitions[51].Undead);
        Assert.Equal((100, 100, 100), (
            monsterTypes.Definitions[51].FireResistance,
            monsterTypes.Definitions[51].LightningResistance,
            monsterTypes.Definitions[51].MagicResistance));
        Assert.Equal((3, 35, 4), (
            monsterTypes.Definitions[51].SkeletonKingSummonMinimumDistance,
            monsterTypes.Definitions[51].SkeletonKingSummonChanceBase,
            monsterTypes.Definitions[51].SkeletonKingSummonChanceIntelligenceScale));
        Assert.Equal((8, 20), (
            monsterTypes.Definitions[55].MinimumSpecialDamage,
            monsterTypes.Definitions[55].MaximumSpecialDamage));
        Assert.Equal("Devourer", monsterTypes.Definitions[60].Name);
        Assert.Equal(59U, monsterTypes.Definitions[60].NativeMonsterId);
        Assert.Equal((100, 100), (
            monsterTypes.Definitions[61].FireResistance,
            monsterTypes.Definitions[61].MagicResistance));
        Assert.Equal((5, 32), (
            monsterTypes.Definitions[65].MinimumSpecialDamage,
            monsterTypes.Definitions[65].MaximumSpecialDamage));
        Assert.Equal("Undead Balrog", monsterTypes.Definitions[72].Name);
        Assert.Equal(71U, monsterTypes.Definitions[72].NativeMonsterId);
        Assert.False(monsterTypes.Definitions[72].Undead);
        Assert.Equal((75, 75, 100), (
            monsterTypes.Definitions[72].FireResistance,
            monsterTypes.Definitions[72].LightningResistance,
            monsterTypes.Definitions[72].MagicResistance));
        Assert.Equal((4, 16), (
            monsterTypes.Definitions[77].MinimumSpecialDamage,
            monsterTypes.Definitions[77].MaximumSpecialDamage));
        Assert.Equal((75, 75, 75), (
            monsterTypes.Definitions[81].FireResistance,
            monsterTypes.Definitions[81].LightningResistance,
            monsterTypes.Definitions[81].MagicResistance));
        Assert.Equal("Blood Claw", monsterTypes.Definitions[84].Name);
        Assert.Equal(83U, monsterTypes.Definitions[84].NativeMonsterId);
        Assert.Equal((100, 75, 100), (
            monsterTypes.Definitions[84].FireResistance,
            monsterTypes.Definitions[84].LightningResistance,
            monsterTypes.Definitions[84].MagicResistance));
        Assert.Equal("Death Wing", monsterTypes.Definitions[85].Name);
        Assert.Equal((75, 100, 100), (
            monsterTypes.Definitions[85].FireResistance,
            monsterTypes.Definitions[85].LightningResistance,
            monsterTypes.Definitions[85].MagicResistance));
        Assert.Equal("Balrog", monsterTypes.Definitions[89].Name);
        Assert.Equal(88U, monsterTypes.Definitions[89].NativeMonsterId);
        Assert.Equal("Steel Lord", monsterTypes.Definitions[96].Name);
        Assert.Equal(95U, monsterTypes.Definitions[96].NativeMonsterId);
        Assert.Equal("Blood Knight", monsterTypes.Definitions[97].Name);
        Assert.Equal((75, 100, 100), (
            monsterTypes.Definitions[97].FireResistance,
            monsterTypes.Definitions[97].LightningResistance,
            monsterTypes.Definitions[97].MagicResistance));
        Assert.True(monsterTypes.Definitions[98].Undead);
        Assert.Equal("Reality Weaver", monsterTypes.Definitions[101].Name);
        Assert.True(monsterTypes.Definitions[101].Undead);
        Assert.Equal((100, 100, 75), (
            monsterTypes.Definitions[101].FireResistance,
            monsterTypes.Definitions[101].LightningResistance,
            monsterTypes.Definitions[101].MagicResistance));
        Assert.Equal("Cabalist", monsterTypes.Definitions[108].Name);
        Assert.Equal(107U, monsterTypes.Definitions[108].NativeMonsterId);
        Assert.Equal("Advocate", monsterTypes.Definitions[109].Name);
        Assert.Equal((100, 100, 100), (
            monsterTypes.Definitions[109].FireResistance,
            monsterTypes.Definitions[109].LightningResistance,
            monsterTypes.Definitions[109].MagicResistance));
        Assert.Equal("The Dark Lord", monsterTypes.Definitions[111].Name);
        Assert.Equal(110U, monsterTypes.Definitions[111].NativeMonsterId);
        Assert.Equal(1666, monsterTypes.Definitions[111].MaximumHitPoints);
        Assert.Equal("Necromorb", monsterTypes.Definitions[120].Name);
        Assert.Equal(119U, monsterTypes.Definitions[120].NativeMonsterId);
        Assert.Equal((100, 75), (
            monsterTypes.Definitions[120].FireResistance,
            monsterTypes.Definitions[120].LightningResistance));
        Assert.Equal((10, 10), (
            monsterTypes.Definitions[121].MinimumSpecialDamage,
            monsterTypes.Definitions[121].MaximumSpecialDamage));
        Assert.Equal((50, 60), (
            monsterTypes.Definitions[125].MinimumSpecialDamage,
            monsterTypes.Definitions[125].MaximumSpecialDamage));
        Assert.True(monsterTypes.Definitions[129].Undead);
        Assert.Equal("Hellbat", monsterTypes.Definitions[132].Name);
        Assert.Equal(131U, monsterTypes.Definitions[132].NativeMonsterId);
        Assert.Equal("Bone Demon", monsterTypes.Definitions[133].Name);
        Assert.True(monsterTypes.Definitions[133].Undead);
        Assert.Equal((50, 50), (
            monsterTypes.Definitions[133].MinimumSpecialDamage,
            monsterTypes.Definitions[133].MaximumSpecialDamage));
        Assert.Equal("Na-Krul", monsterTypes.Definitions[^1].Name);
        Assert.Equal(137U, monsterTypes.Definitions[^1].NativeMonsterId);
        Assert.Equal(125, monsterTypes.Definitions[^1].ArmorClass);
        Assert.Equal((40, 50), (
            monsterTypes.Definitions[^1].MinimumSpecialDamage,
            monsterTypes.Definitions[^1].MaximumSpecialDamage));
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            composed.GetRequiredTable("quest_types.tsv").ToCanonicalTsv());
        Assert.Equal(24, questTypes.Definitions.Count);
        Assert.Equal("The Magic Rock", questTypes.Definitions[0].Name);
        Assert.Equal("The Jersey's Jersey", questTypes.Definitions[^1].Name);
        Assert.Equal(23U, questTypes.Definitions[^1].NativeQuestId);
        var hellfireCursors = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            composed.GetRequiredTable("item_cursor_graphics.tsv").ToCanonicalTsv());
        var hellfireItems = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            composed.GetRequiredTable("items.tsv").ToCanonicalTsv(),
            cursorCatalog: hellfireCursors);
        var hellfireUniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            composed.GetRequiredTable("unique_items.tsv").ToCanonicalTsv(),
            hellfireCursors);
        hellfireItems.AttachUniqueCatalog(hellfireUniques);
        Assert.Equal(2, hellfireItems.GetRequired(1).MinimumMonsterLevel);
        Assert.Equal(0, hellfireItems.GetRequired(1).DropRate);
        Assert.False(hellfireItems.GetRequired(1).Usable);
        var crown = hellfireItems.GenerateUnique(1003, 0x12345678, 1);
        Assert.Equal(7, crown.ItemIndex);
        Assert.Equal("The Undead Crown", crown.BaseName);
        Assert.Equal("The Undead Crown", crown.IdentifiedName);
        Assert.Equal(77, crown.NativeCursorGraphic);
        Assert.Equal(16650, crown.Value);
        Assert.Equal(15, crown.ArmorClass);
        Assert.Equal(0x00000002U, crown.Flags);
        var band = hellfireItems.GenerateUnique(1004, 0x12345678, 1);
        Assert.Equal(1004, band.UniqueId);
        Assert.Equal(8000, band.Value);
        Assert.Equal(2, band.PlusStrength);
        Assert.Equal(2, band.PlusMagic);
        Assert.Equal(2, band.PlusDexterity);
        Assert.Equal(2, band.PlusVitality);
        Assert.Equal(2, band.PlusLight);
        Assert.Equal(0x00200000U | 0x10000000U, band.Flags);
        var opticAmulet = hellfireItems.GenerateUnique(1005, 0x12345678, 1);
        Assert.Equal(1005, opticAmulet.UniqueId);
        Assert.Equal(9750, opticAmulet.Value);
        Assert.Equal(2, opticAmulet.PlusLight);
        Assert.Equal(20, opticAmulet.PlusLightningResistance);
        Assert.Equal(-1, opticAmulet.PlusGetHit);
        Assert.Equal(5, opticAmulet.PlusMagic);
        var ringOfTruth = hellfireItems.GenerateUnique(1006, 0x12345678, 1);
        Assert.Equal(1006, ringOfTruth.UniqueId);
        Assert.Equal(9100, ringOfTruth.Value);
        Assert.Equal(10 << 6, ringOfTruth.PlusHitPoints);
        Assert.Equal(-1, ringOfTruth.PlusGetHit);
        Assert.Equal(10, ringOfTruth.PlusMagicResistance);
        var harlequinCrest = hellfireItems.GenerateUnique(1007, 0x12345678, 1);
        Assert.Equal(1007, harlequinCrest.UniqueId);
        Assert.Equal(4000, harlequinCrest.Value);
        Assert.Equal(-3, harlequinCrest.PlusArmorClass);
        Assert.Equal(-1, harlequinCrest.PlusGetHit);
        Assert.Equal(2, harlequinCrest.PlusStrength);
        Assert.Equal(448, harlequinCrest.PlusHitPoints);
        Assert.Equal(448, harlequinCrest.PlusMana);
        var veilOfSteel = hellfireItems.GenerateUnique(1008, 0x12345678, 1);
        Assert.Equal(1008, veilOfSteel.UniqueId);
        Assert.Equal(63800, veilOfSteel.Value);
        Assert.Equal(50, veilOfSteel.PlusFireResistance);
        Assert.Equal(50, veilOfSteel.PlusLightningResistance);
        Assert.Equal(50, veilOfSteel.PlusMagicResistance);
        Assert.Equal(60, veilOfSteel.PlusArmorClass);
        Assert.Equal(-(30 << 6), veilOfSteel.PlusMana);
        Assert.Equal(15, veilOfSteel.PlusStrength);
        Assert.Equal(15, veilOfSteel.PlusVitality);
        var arkainesValor = hellfireItems.GenerateUnique(1009, 0x12345678, 1);
        Assert.Equal(1009, arkainesValor.UniqueId);
        Assert.Equal(42000, arkainesValor.Value);
        Assert.Equal(25, arkainesValor.ArmorClass);
        Assert.Equal(10, arkainesValor.PlusVitality);
        Assert.Equal(-3, arkainesValor.PlusGetHit);
        Assert.NotEqual(0U, arkainesValor.Flags & 0x00800000U);
        var griswoldsEdge = hellfireItems.GenerateUnique(1010, 0x12345678, 1);
        Assert.Equal(1010, griswoldsEdge.UniqueId);
        Assert.Equal(42000, griswoldsEdge.Value);
        Assert.Equal(4, griswoldsEdge.MinDamage);
        Assert.Equal(12, griswoldsEdge.MaxDamage);
        Assert.Equal(1, griswoldsEdge.FireMinDamage);
        Assert.Equal(10, griswoldsEdge.FireMaxDamage);
        Assert.Equal(25, griswoldsEdge.PlusToHit);
        Assert.NotEqual(0U, griswoldsEdge.Flags & 0x00040000U);
        Assert.NotEqual(0U, griswoldsEdge.Flags & 0x00000800U);
        Assert.Equal(20 << 6, griswoldsEdge.PlusMana);
        Assert.Equal(-(20 << 6), griswoldsEdge.PlusHitPoints);
        var bovinePlate = hellfireItems.GenerateUnique(1011, 0x12345678, 1);
        Assert.Equal(1011, bovinePlate.UniqueId);
        Assert.Equal(400, bovinePlate.Value);
        Assert.Equal(150, bovinePlate.ArmorClass);
        Assert.Equal(255, bovinePlate.Durability);
        Assert.Equal(255, bovinePlate.MaxDurability);
        Assert.Equal(50, bovinePlate.MinimumStrength);
        Assert.Equal(30, bovinePlate.PlusFireResistance);
        Assert.Equal(30, bovinePlate.PlusLightningResistance);
        Assert.Equal(30, bovinePlate.PlusMagicResistance);
        Assert.Equal(5, bovinePlate.PlusLight);
        Assert.Equal(-(50 << 6), bovinePlate.PlusMana);
        Assert.Equal(-2, bovinePlate.SpellLevelAdd);
        var riftBow = hellfireItems.GenerateUnique(1012, 0x12345678, 1);
        Assert.Equal(1012, riftBow.UniqueId);
        Assert.Equal(1800, riftBow.Value);
        Assert.Equal(4, riftBow.ItemIndex);
        Assert.Equal(2, riftBow.PlusDamageModifier);
        Assert.Equal(-3, riftBow.PlusDexterity);
        Assert.NotEqual(0U, riftBow.Flags & 0x00000004U);
        var butchersCleaver = hellfireItems.GenerateUnique(1013, 0x12345678, 1);
        Assert.Equal(1013, butchersCleaver.UniqueId);
        Assert.Equal(3650, butchersCleaver.Value);
        Assert.Equal(6, butchersCleaver.ItemIndex);
        Assert.Equal(4, butchersCleaver.MinDamage);
        Assert.Equal(24, butchersCleaver.MaxDamage);
        Assert.Equal(10, butchersCleaver.PlusStrength);
        Assert.Equal(10, butchersCleaver.Durability);
        Assert.Equal(10, butchersCleaver.MaxDurability);
        var needler = hellfireItems.GenerateUnique(1014, 0x12345678, 2);
        Assert.Equal(1014, needler.UniqueId);
        Assert.Equal(8900, needler.Value);
        Assert.Equal(4, needler.ItemIndex);
        Assert.Equal(1, needler.MinDamage);
        Assert.Equal(3, needler.MaxDamage);
        Assert.Equal(50, needler.PlusToHit);
        Assert.NotEqual(0U, needler.Flags & 0x00040000U);
        var celestialBow = hellfireItems.GenerateUnique(1015, 0x12345678, 2);
        Assert.Equal(1015, celestialBow.UniqueId);
        Assert.Equal(1200, celestialBow.Value);
        Assert.Equal(146, celestialBow.ItemIndex);
        Assert.Equal(1, celestialBow.MinDamage);
        Assert.Equal(6, celestialBow.MaxDamage);
        Assert.Equal(0, celestialBow.MinimumStrength);
        Assert.Equal(2, celestialBow.PlusDamageModifier);
        Assert.Equal(5, celestialBow.ArmorClass);
        var deadlyHunter = hellfireItems.GenerateUnique(1016, 0x12345678, 3);
        Assert.Equal(1016, deadlyHunter.UniqueId);
        Assert.Equal(8750, deadlyHunter.Value);
        Assert.Equal(147, deadlyHunter.ItemIndex);
        Assert.Equal(3, deadlyHunter.MinDamage);
        Assert.Equal(6, deadlyHunter.MaxDamage);
        Assert.Equal(20, deadlyHunter.PlusToHit);
        Assert.Equal(-5, deadlyHunter.PlusMagic);
        Assert.NotEqual(0U, deadlyHunter.Flags & 0x40000000U);
        var bowOfTheDead = hellfireItems.GenerateUnique(1017, 0x12345678, 5);
        Assert.Equal(1017, bowOfTheDead.UniqueId);
        Assert.Equal(2500, bowOfTheDead.Value);
        Assert.Equal(147, bowOfTheDead.ItemIndex);
        Assert.Equal(10, bowOfTheDead.PlusToHit);
        Assert.Equal(4, bowOfTheDead.PlusDexterity);
        Assert.Equal(-3, bowOfTheDead.PlusVitality);
        Assert.Equal(-2, bowOfTheDead.PlusLight);
        Assert.Equal(30, bowOfTheDead.Durability);
        Assert.Equal(30, bowOfTheDead.MaxDurability);
        var blackoakBow = hellfireItems.GenerateUnique(1018, 0x12345678, 5);
        Assert.Equal(1018, blackoakBow.UniqueId);
        Assert.Equal(2500, blackoakBow.Value);
        Assert.Equal(146, blackoakBow.ItemIndex);
        Assert.Equal(10, blackoakBow.PlusDexterity);
        Assert.Equal(-10, blackoakBow.PlusVitality);
        Assert.Equal(50, blackoakBow.PlusDamage);
        Assert.Equal(-1, blackoakBow.PlusLight);
        var flamedart = hellfireItems.GenerateUnique(1019, 0x12345678, 10);
        Assert.Equal(1019, flamedart.UniqueId);
        Assert.Equal(14250, flamedart.Value);
        Assert.Equal(145, flamedart.ItemIndex);
        Assert.Equal(2, flamedart.MinDamage);
        Assert.Equal(5, flamedart.MaxDamage);
        Assert.Equal(20, flamedart.PlusToHit);
        Assert.Equal(40, flamedart.PlusFireResistance);
        Assert.Equal(1, flamedart.FireMinDamage);
        Assert.Equal(6, flamedart.FireMaxDamage);
        Assert.NotEqual(0U, flamedart.Flags & 0x00000010U);
        var fleshstinger = hellfireItems.GenerateUnique(1020, 0x12345678, 13);
        Assert.Equal(1020, fleshstinger.UniqueId);
        Assert.Equal(16500, fleshstinger.Value);
        Assert.Equal(146, fleshstinger.ItemIndex);
        Assert.Equal(15, fleshstinger.PlusDexterity);
        Assert.Equal(40, fleshstinger.PlusToHit);
        Assert.Equal(80, fleshstinger.PlusDamage);
        Assert.Equal(37, fleshstinger.Durability);
        Assert.Equal(37, fleshstinger.MaxDurability);
        var windforce = hellfireItems.GenerateUnique(1021, 0x12345678, 17);
        Assert.Equal(1021, windforce.UniqueId);
        Assert.Equal(37750, windforce.Value);
        Assert.Equal(151, windforce.ItemIndex);
        Assert.Equal(1, windforce.MinDamage);
        Assert.Equal(14, windforce.MaxDamage);
        Assert.Equal(5, windforce.PlusStrength);
        Assert.Equal(200, windforce.PlusDamage);
        Assert.NotEqual(0U, windforce.Flags & 0x00000800U);
        var eaglehorn = hellfireItems.GenerateUnique(1022, 0x12345678, 26);
        Assert.Equal(1022, eaglehorn.UniqueId);
        Assert.Equal(42500, eaglehorn.Value);
        Assert.Equal(149, eaglehorn.ItemIndex);
        Assert.Equal(1, eaglehorn.MinDamage);
        Assert.Equal(10, eaglehorn.MaxDamage);
        Assert.Equal(20, eaglehorn.PlusDexterity);
        Assert.Equal(50, eaglehorn.PlusToHit);
        Assert.Equal(100, eaglehorn.PlusDamage);
        Assert.Equal(255, eaglehorn.Durability);
        Assert.Equal(255, eaglehorn.MaxDurability);
        var gonnagalsDirk = hellfireItems.GenerateUnique(1023, 0x12345678, 1);
        Assert.Equal(1023, gonnagalsDirk.UniqueId);
        Assert.Equal(7040, gonnagalsDirk.Value);
        Assert.Equal(119, gonnagalsDirk.ItemIndex);
        Assert.Equal(1, gonnagalsDirk.MinDamage);
        Assert.Equal(4, gonnagalsDirk.MaxDamage);
        Assert.Equal(-5, gonnagalsDirk.PlusDexterity);
        Assert.Equal(4, gonnagalsDirk.PlusDamageModifier);
        Assert.Equal(25, gonnagalsDirk.PlusFireResistance);
        Assert.NotEqual(0U, gonnagalsDirk.Flags & 0x00040000U);
        var defender = hellfireItems.GenerateUnique(1024, 0x12345678, 1);
        Assert.Equal(1024, defender.UniqueId);
        Assert.Equal(2000, defender.Value);
        Assert.Equal(125, defender.ItemIndex);
        Assert.Equal(1, defender.MinDamage);
        Assert.Equal(8, defender.MaxDamage);
        Assert.Equal(5, defender.ArmorClass);
        Assert.Equal(17, defender.MinimumStrength);
        Assert.Equal(5, defender.PlusVitality);
        Assert.Equal(-5, defender.PlusToHit);
        var gryphonsClaw = hellfireItems.GenerateUnique(1025, 0x12345678, 1);
        Assert.Equal(1025, gryphonsClaw.UniqueId);
        Assert.Equal(1000, gryphonsClaw.Value);
        Assert.Equal(121, gryphonsClaw.ItemIndex);
        Assert.Equal(4, gryphonsClaw.MinDamage);
        Assert.Equal(8, gryphonsClaw.MaxDamage);
        Assert.Equal(30, gryphonsClaw.MinimumStrength);
        Assert.Equal(100, gryphonsClaw.PlusDamage);
        Assert.Equal(-2, gryphonsClaw.PlusMagic);
        Assert.Equal(-5, gryphonsClaw.PlusDexterity);
        var blackRazor = hellfireItems.GenerateUnique(1026, 0x12345678, 1);
        Assert.Equal(1026, blackRazor.UniqueId);
        Assert.Equal(2000, blackRazor.Value);
        Assert.Equal(119, blackRazor.ItemIndex);
        Assert.Equal(1, blackRazor.MinDamage);
        Assert.Equal(4, blackRazor.MaxDamage);
        Assert.Equal(150, blackRazor.PlusDamage);
        Assert.Equal(2, blackRazor.PlusVitality);
        Assert.Equal(5, blackRazor.Durability);
        Assert.Equal(5, blackRazor.MaxDurability);
        var gibbousMoon = hellfireItems.GenerateUnique(1027, 0x12345678, 2);
        Assert.Equal(1027, gibbousMoon.UniqueId);
        Assert.Equal(6660, gibbousMoon.Value);
        Assert.Equal(127, gibbousMoon.ItemIndex);
        Assert.Equal(4, gibbousMoon.MinDamage);
        Assert.Equal(12, gibbousMoon.MaxDamage);
        Assert.Equal(40, gibbousMoon.MinimumStrength);
        Assert.Equal(2, gibbousMoon.PlusStrength);
        Assert.Equal(2, gibbousMoon.PlusMagic);
        Assert.Equal(2, gibbousMoon.PlusDexterity);
        Assert.Equal(2, gibbousMoon.PlusVitality);
        Assert.Equal(25, gibbousMoon.PlusDamage);
        Assert.Equal(15 << 6, gibbousMoon.PlusMana);
        Assert.Equal(-3, gibbousMoon.PlusLight);
        var iceShank = hellfireItems.GenerateUnique(1028, 0x12345678, 3);
        Assert.Equal(1028, iceShank.UniqueId);
        Assert.Equal(5250, iceShank.Value);
        Assert.Equal(126, iceShank.ItemIndex);
        Assert.Equal(2, iceShank.MinDamage);
        Assert.Equal(10, iceShank.MaxDamage);
        Assert.Equal(30, iceShank.MinimumStrength);
        Assert.Equal(30, iceShank.MinimumDexterity);
        Assert.Equal(40, iceShank.PlusFireResistance);
        Assert.Equal(15, iceShank.Durability);
        Assert.Equal(15, iceShank.MaxDurability);
        Assert.InRange(iceShank.PlusStrength, 5, 10);
        var executionersBlade = hellfireItems.GenerateUnique(1029, 0x12345678, 3);
        Assert.Equal(1029, executionersBlade.UniqueId);
        Assert.Equal(7080, executionersBlade.Value);
        Assert.Equal(121, executionersBlade.ItemIndex);
        Assert.Equal(4, executionersBlade.MinDamage);
        Assert.Equal(8, executionersBlade.MaxDamage);
        Assert.Equal(30, executionersBlade.MinimumStrength);
        Assert.Equal(150, executionersBlade.PlusDamage);
        Assert.Equal(-(10 << 6), executionersBlade.PlusHitPoints);
        Assert.Equal(-1, executionersBlade.PlusLight);
        Assert.Equal(60, executionersBlade.Durability);
        Assert.Equal(60, executionersBlade.MaxDurability);
        var bonesaw = hellfireItems.GenerateUnique(1030, 0x12345678, 6);
        Assert.Equal(1030, bonesaw.UniqueId);
        Assert.Equal(4400, bonesaw.Value);
        Assert.Equal(123, bonesaw.ItemIndex);
        Assert.Equal(1, bonesaw.MinDamage);
        Assert.Equal(12, bonesaw.MaxDamage);
        Assert.Equal(35, bonesaw.MinimumStrength);
        Assert.Equal(10, bonesaw.PlusDamageModifier);
        Assert.Equal(10, bonesaw.PlusStrength);
        Assert.Equal(-5, bonesaw.PlusMagic);
        Assert.Equal(-5, bonesaw.PlusDexterity);
        Assert.Equal(10 << 6, bonesaw.PlusHitPoints);
        Assert.Equal(-(10 << 6), bonesaw.PlusMana);
        var shadowhawk = hellfireItems.GenerateUnique(1031, 0x12345678, 8);
        Assert.Equal(1031, shadowhawk.UniqueId);
        Assert.Equal(13750, shadowhawk.Value);
        Assert.Equal(127, shadowhawk.ItemIndex);
        Assert.Equal(4, shadowhawk.MinDamage);
        Assert.Equal(12, shadowhawk.MaxDamage);
        Assert.Equal(40, shadowhawk.MinimumStrength);
        Assert.Equal(-2, shadowhawk.PlusLight);
        Assert.Equal(15, shadowhawk.PlusToHit);
        Assert.Equal(5, shadowhawk.PlusFireResistance);
        Assert.Equal(5, shadowhawk.PlusLightningResistance);
        Assert.Equal(5, shadowhawk.PlusMagicResistance);
        Assert.NotEqual(0U, shadowhawk.Flags & 0x00010000U);
        var wizardspike = hellfireItems.GenerateUnique(1032, 0x12345678, 11);
        Assert.Equal(1032, wizardspike.UniqueId);
        Assert.Equal(12920, wizardspike.Value);
        Assert.Equal(119, wizardspike.ItemIndex);
        Assert.Equal(1, wizardspike.MinDamage);
        Assert.Equal(4, wizardspike.MaxDamage);
        Assert.Equal(15, wizardspike.PlusMagic);
        Assert.Equal(35 << 6, wizardspike.PlusMana);
        Assert.Equal(25, wizardspike.PlusToHit);
        Assert.Equal(15, wizardspike.PlusFireResistance);
        Assert.Equal(15, wizardspike.PlusLightningResistance);
        Assert.Equal(15, wizardspike.PlusMagicResistance);
        var lightsabre = hellfireItems.GenerateUnique(1033, 0x12345678, 13);
        Assert.Equal(1033, lightsabre.UniqueId);
        Assert.Equal(19150, lightsabre.Value);
        Assert.Equal(125, lightsabre.ItemIndex);
        Assert.Equal(1, lightsabre.MinDamage);
        Assert.Equal(8, lightsabre.MaxDamage);
        Assert.Equal(17, lightsabre.MinimumStrength);
        Assert.Equal(2, lightsabre.PlusLight);
        Assert.Equal(1, lightsabre.LightningMinDamage);
        Assert.Equal(10, lightsabre.LightningMaxDamage);
        Assert.Equal(20, lightsabre.PlusToHit);
        Assert.Equal(50, lightsabre.PlusLightningResistance);
        var falconsTalon = hellfireItems.GenerateUnique(1034, 0x12345678, 15);
        Assert.Equal(1034, falconsTalon.UniqueId);
        Assert.Equal(7867, falconsTalon.Value);
        Assert.Equal(122, falconsTalon.ItemIndex);
        Assert.Equal(3, falconsTalon.MinDamage);
        Assert.Equal(7, falconsTalon.MaxDamage);
        Assert.Equal(23, falconsTalon.MinimumStrength);
        Assert.Equal(23, falconsTalon.MinimumDexterity);
        Assert.Equal(20, falconsTalon.PlusToHit);
        Assert.Equal(-33, falconsTalon.PlusDamage);
        Assert.Equal(10, falconsTalon.PlusDexterity);
        Assert.NotEqual(0U, falconsTalon.Flags & 0x00100000U);
        var inferno = hellfireItems.GenerateUnique(1035, 0x12345678, 17);
        Assert.Equal(1035, inferno.UniqueId);
        Assert.Equal(34600, inferno.Value);
        Assert.Equal(126, inferno.ItemIndex);
        Assert.Equal(2, inferno.MinDamage);
        Assert.Equal(10, inferno.MaxDamage);
        Assert.Equal(30, inferno.MinimumStrength);
        Assert.Equal(30, inferno.MinimumDexterity);
        Assert.Equal(2, inferno.FireMinDamage);
        Assert.Equal(12, inferno.FireMaxDamage);
        Assert.Equal(3, inferno.PlusLight);
        Assert.Equal(20 << 6, inferno.PlusMana);
        Assert.Equal(80, inferno.PlusFireResistance);
        var doombringer = hellfireItems.GenerateUnique(1036, 0x12345678, 19);
        Assert.Equal(1036, doombringer.UniqueId);
        Assert.Equal(18250, doombringer.Value);
        Assert.Equal(129, doombringer.ItemIndex);
        Assert.Equal(8, doombringer.MinDamage);
        Assert.Equal(16, doombringer.MaxDamage);
        Assert.Equal(65, doombringer.MinimumStrength);
        Assert.Equal(25, doombringer.PlusToHit);
        Assert.Equal(250, doombringer.PlusDamage);
        Assert.Equal(-5, doombringer.PlusStrength);
        Assert.Equal(-5, doombringer.PlusMagic);
        Assert.Equal(-5, doombringer.PlusDexterity);
        Assert.Equal(-5, doombringer.PlusVitality);
        Assert.Equal(-(25 << 6), doombringer.PlusHitPoints);
        Assert.Equal(-2, doombringer.PlusLight);
        var grandfather = hellfireItems.GenerateUnique(1037, 0x12345678, 27);
        Assert.Equal(1037, grandfather.UniqueId);
        Assert.Equal(119800, grandfather.Value);
        Assert.Equal(130, grandfather.ItemIndex);
        Assert.Equal(10, grandfather.MinDamage);
        Assert.Equal(20, grandfather.MaxDamage);
        Assert.Equal(75, grandfather.MinimumStrength);
        Assert.Equal(1, grandfather.EquipLocation);
        Assert.Equal(5, grandfather.PlusStrength);
        Assert.Equal(5, grandfather.PlusMagic);
        Assert.Equal(5, grandfather.PlusDexterity);
        Assert.Equal(5, grandfather.PlusVitality);
        Assert.Equal(20, grandfather.PlusToHit);
        Assert.Equal(70, grandfather.PlusDamage);
        Assert.Equal(20 << 6, grandfather.PlusHitPoints);
        var mangler = hellfireItems.GenerateUnique(1038, 0x12345678, 2);
        Assert.Equal(1038, mangler.UniqueId);
        Assert.Equal(2850, mangler.Value);
        Assert.Equal(133, mangler.ItemIndex);
        Assert.Equal(6, mangler.MinDamage);
        Assert.Equal(16, mangler.MaxDamage);
        Assert.Equal(30, mangler.MinimumStrength);
        Assert.Equal(2, mangler.EquipLocation);
        Assert.Equal(200, mangler.PlusDamage);
        Assert.Equal(-5, mangler.PlusDexterity);
        Assert.Equal(-5, mangler.PlusMagic);
        Assert.Equal(-(10 << 6), mangler.PlusMana);
        var sharpBeak = hellfireItems.GenerateUnique(1039, 0x12345678, 2);
        Assert.Equal(1039, sharpBeak.UniqueId);
        Assert.Equal(2850, sharpBeak.Value);
        Assert.Equal(133, sharpBeak.ItemIndex);
        Assert.Equal(6, sharpBeak.MinDamage);
        Assert.Equal(16, sharpBeak.MaxDamage);
        Assert.Equal(30, sharpBeak.MinimumStrength);
        Assert.Equal(2, sharpBeak.EquipLocation);
        Assert.Equal(20 << 6, sharpBeak.PlusHitPoints);
        Assert.Equal(-10, sharpBeak.PlusMagic);
        Assert.Equal(-(10 << 6), sharpBeak.PlusMana);
        var bloodSlayer = hellfireItems.GenerateUnique(1040, 0x12345678, 3);
        Assert.Equal(1040, bloodSlayer.UniqueId);
        Assert.Equal(2500, bloodSlayer.Value);
        Assert.Equal(134, bloodSlayer.ItemIndex);
        Assert.Equal(8, bloodSlayer.MinDamage);
        Assert.Equal(20, bloodSlayer.MaxDamage);
        Assert.Equal(50, bloodSlayer.MinimumStrength);
        Assert.Equal(2, bloodSlayer.EquipLocation);
        Assert.Equal(100, bloodSlayer.PlusDamage);
        Assert.NotEqual(0U, bloodSlayer.Flags & 0x40000000U);
        Assert.Equal(-5, bloodSlayer.PlusStrength);
        Assert.Equal(-5, bloodSlayer.PlusMagic);
        Assert.Equal(-5, bloodSlayer.PlusDexterity);
        Assert.Equal(-5, bloodSlayer.PlusVitality);
        Assert.Equal(-1, bloodSlayer.SpellLevelAdd);
        var celestialAxe = hellfireItems.GenerateUnique(1041, 0x12345678, 4);
        Assert.Equal(1041, celestialAxe.UniqueId);
        Assert.Equal(14100, celestialAxe.Value);
        Assert.Equal(135, celestialAxe.ItemIndex);
        Assert.Equal(10, celestialAxe.MinDamage);
        Assert.Equal(25, celestialAxe.MaxDamage);
        Assert.Equal(0, celestialAxe.MinimumStrength);
        Assert.Equal(2, celestialAxe.EquipLocation);
        Assert.Equal(15, celestialAxe.PlusToHit);
        Assert.Equal(15 << 6, celestialAxe.PlusHitPoints);
        Assert.Equal(-15, celestialAxe.PlusStrength);
        var wickedAxe = hellfireItems.GenerateUnique(1042, 0x12345678, 5);
        Assert.Equal(1042, wickedAxe.UniqueId);
        Assert.Equal(31150, wickedAxe.Value);
        Assert.Equal(133, wickedAxe.ItemIndex);
        Assert.Equal(6, wickedAxe.MinDamage);
        Assert.Equal(16, wickedAxe.MaxDamage);
        Assert.Equal(30, wickedAxe.MinimumStrength);
        Assert.Equal(2, wickedAxe.EquipLocation);
        Assert.Equal(30, wickedAxe.PlusToHit);
        Assert.Equal(10, wickedAxe.PlusDexterity);
        Assert.Equal(-10, wickedAxe.PlusVitality);
        Assert.Equal(-2, wickedAxe.PlusGetHit);
        Assert.Equal(255, wickedAxe.Durability);
        Assert.Equal(255, wickedAxe.MaxDurability);
        var stonecleaver = hellfireItems.GenerateUnique(1043, 0x12345678, 7);
        Assert.Equal(1043, stonecleaver.UniqueId);
        Assert.Equal(23900, stonecleaver.Value);
        Assert.Equal(134, stonecleaver.ItemIndex);
        Assert.Equal(8, stonecleaver.MinDamage);
        Assert.Equal(20, stonecleaver.MaxDamage);
        Assert.Equal(50, stonecleaver.MinimumStrength);
        Assert.Equal(2, stonecleaver.EquipLocation);
        Assert.Equal(30 << 6, stonecleaver.PlusHitPoints);
        Assert.Equal(20, stonecleaver.PlusToHit);
        Assert.Equal(50, stonecleaver.PlusDamage);
        Assert.Equal(40, stonecleaver.PlusLightningResistance);
        var aguinarasHatchet = hellfireItems.GenerateUnique(1044, 0x12345678, 12);
        Assert.Equal(1044, aguinarasHatchet.UniqueId);
        Assert.Equal(24800, aguinarasHatchet.Value);
        Assert.Equal(131, aguinarasHatchet.ItemIndex);
        Assert.Equal(2, aguinarasHatchet.MinDamage);
        Assert.Equal(10, aguinarasHatchet.MaxDamage);
        Assert.Equal(0, aguinarasHatchet.MinimumStrength);
        Assert.Equal(2, aguinarasHatchet.EquipLocation);
        Assert.Equal(1, aguinarasHatchet.SpellLevelAdd);
        Assert.Equal(10, aguinarasHatchet.PlusMagic);
        Assert.Equal(80, aguinarasHatchet.PlusMagicResistance);
        var hellslayer = hellfireItems.GenerateUnique(1045, 0x12345678, 15);
        Assert.Equal(1045, hellslayer.UniqueId);
        Assert.Equal(26200, hellslayer.Value);
        Assert.Equal(135, hellslayer.ItemIndex);
        Assert.Equal(10, hellslayer.MinDamage);
        Assert.Equal(25, hellslayer.MaxDamage);
        Assert.Equal(65, hellslayer.MinimumStrength);
        Assert.Equal(2, hellslayer.EquipLocation);
        Assert.Equal(8, hellslayer.PlusStrength);
        Assert.Equal(8, hellslayer.PlusVitality);
        Assert.Equal(100, hellslayer.PlusDamage);
        Assert.Equal(25 << 6, hellslayer.PlusHitPoints);
        Assert.Equal(-(25 << 6), hellslayer.PlusMana);
        var messerschmidtsReaver = hellfireItems.GenerateUnique(1046, 0x12345678, 25);
        Assert.Equal(1046, messerschmidtsReaver.UniqueId);
        Assert.Equal(58000, messerschmidtsReaver.Value);
        Assert.Equal(136, messerschmidtsReaver.ItemIndex);
        Assert.Equal(12, messerschmidtsReaver.MinDamage);
        Assert.Equal(30, messerschmidtsReaver.MaxDamage);
        Assert.Equal(80, messerschmidtsReaver.MinimumStrength);
        Assert.Equal(2, messerschmidtsReaver.EquipLocation);
        Assert.Equal(200, messerschmidtsReaver.PlusDamage);
        Assert.Equal(15, messerschmidtsReaver.PlusDamageModifier);
        Assert.Equal(5, messerschmidtsReaver.PlusStrength);
        Assert.Equal(5, messerschmidtsReaver.PlusMagic);
        Assert.Equal(5, messerschmidtsReaver.PlusDexterity);
        Assert.Equal(5, messerschmidtsReaver.PlusVitality);
        Assert.Equal(-(50 << 6), messerschmidtsReaver.PlusHitPoints);
        Assert.Equal(2, messerschmidtsReaver.FireMinDamage);
        Assert.Equal(12, messerschmidtsReaver.FireMaxDamage);
        var crackrust = hellfireItems.GenerateUnique(1047, 0x12345678, 1);
        Assert.Equal(1047, crackrust.UniqueId);
        Assert.Equal(11375, crackrust.Value);
        Assert.Equal(137, crackrust.ItemIndex);
        Assert.Equal(1, crackrust.MinDamage);
        Assert.Equal(8, crackrust.MaxDamage);
        Assert.Equal(16, crackrust.MinimumStrength);
        Assert.Equal(1, crackrust.EquipLocation);
        Assert.Equal(2, crackrust.PlusStrength);
        Assert.Equal(2, crackrust.PlusMagic);
        Assert.Equal(2, crackrust.PlusDexterity);
        Assert.Equal(2, crackrust.PlusVitality);
        Assert.Equal(15, crackrust.PlusFireResistance);
        Assert.Equal(15, crackrust.PlusLightningResistance);
        Assert.Equal(15, crackrust.PlusMagicResistance);
        Assert.Equal(50, crackrust.PlusDamage);
        Assert.Equal(-1, crackrust.SpellLevelAdd);
        Assert.Equal(255, crackrust.Durability);
        Assert.Equal(255, crackrust.MaxDurability);
        var hammerOfJholm = hellfireItems.GenerateUnique(1048, 0x12345678, 1);
        Assert.Equal(1048, hammerOfJholm.UniqueId);
        Assert.Equal(8700, hammerOfJholm.Value);
        Assert.Equal(142, hammerOfJholm.ItemIndex);
        Assert.Equal(4, hammerOfJholm.MinDamage);
        Assert.Equal(10, hammerOfJholm.MaxDamage);
        Assert.Equal(55, hammerOfJholm.MinimumStrength);
        Assert.Equal(2, hammerOfJholm.EquipLocation);
        Assert.Equal(3, hammerOfJholm.PlusStrength);
        Assert.Equal(15, hammerOfJholm.PlusToHit);
        Assert.Equal(255, hammerOfJholm.Durability);
        Assert.Equal(255, hammerOfJholm.MaxDurability);
        var civerbsCudgel = hellfireItems.GenerateUnique(1049, 0x12345678, 1);
        Assert.Equal(1049, civerbsCudgel.UniqueId);
        Assert.Equal(2000, civerbsCudgel.Value);
        Assert.Equal(137, civerbsCudgel.ItemIndex);
        Assert.Equal(1, civerbsCudgel.MinDamage);
        Assert.Equal(8, civerbsCudgel.MaxDamage);
        Assert.Equal(16, civerbsCudgel.MinimumStrength);
        Assert.Equal(1, civerbsCudgel.EquipLocation);
        Assert.NotEqual(0U, civerbsCudgel.Flags & 0x40000000U);
        Assert.Equal(-5, civerbsCudgel.PlusDexterity);
        Assert.Equal(-2, civerbsCudgel.PlusMagic);
        var celestialStar = hellfireItems.GenerateUnique(1050, 0x12345678, 2);
        Assert.Equal(1050, celestialStar.UniqueId);
        Assert.Equal(7810, celestialStar.Value);
        Assert.Equal(142, celestialStar.ItemIndex);
        Assert.Equal(2, celestialStar.MinDamage);
        Assert.Equal(12, celestialStar.MaxDamage);
        Assert.Equal(0, celestialStar.MinimumStrength);
        Assert.Equal(1, celestialStar.EquipLocation);
        Assert.Equal(2, celestialStar.PlusLight);
        Assert.Equal(10, celestialStar.PlusDamageModifier);
        Assert.Equal(-8, celestialStar.PlusArmorClass);
        var baranarsStar = hellfireItems.GenerateUnique(1051, 0x12345678, 5);
        Assert.Equal(1051, baranarsStar.UniqueId);
        Assert.Equal(6850, baranarsStar.Value);
        Assert.Equal(138, baranarsStar.ItemIndex);
        Assert.Equal(1, baranarsStar.MinDamage);
        Assert.Equal(10, baranarsStar.MaxDamage);
        Assert.Equal(26, baranarsStar.MinimumStrength);
        Assert.Equal(1, baranarsStar.EquipLocation);
        Assert.Equal(12, baranarsStar.PlusToHit);
        Assert.Equal(80, baranarsStar.PlusDamage);
        Assert.NotEqual(0U, baranarsStar.Flags & 0x00020000U);
        Assert.Equal(4, baranarsStar.PlusVitality);
        Assert.Equal(-4, baranarsStar.PlusDexterity);
        Assert.Equal(60, baranarsStar.Durability);
        Assert.Equal(60, baranarsStar.MaxDurability);
        var gnarledRoot = hellfireItems.GenerateUnique(1052, 0x12345678, 9);
        Assert.Equal(1052, gnarledRoot.UniqueId);
        Assert.Equal(9820, gnarledRoot.Value);
        Assert.Equal(139, gnarledRoot.ItemIndex);
        Assert.Equal(3, gnarledRoot.MinDamage);
        Assert.Equal(6, gnarledRoot.MaxDamage);
        Assert.Equal(18, gnarledRoot.MinimumStrength);
        Assert.Equal(1, gnarledRoot.EquipLocation);
        Assert.Equal(20, gnarledRoot.PlusToHit);
        Assert.Equal(300, gnarledRoot.PlusDamage);
        Assert.Equal(10, gnarledRoot.PlusDexterity);
        Assert.Equal(5, gnarledRoot.PlusMagic);
        Assert.Equal(10, gnarledRoot.PlusFireResistance);
        Assert.Equal(10, gnarledRoot.PlusLightningResistance);
        Assert.Equal(10, gnarledRoot.PlusMagicResistance);
        Assert.Equal(-10, gnarledRoot.PlusArmorClass);
        var craniumBasher = hellfireItems.GenerateUnique(1053, 0x12345678, 12);
        Assert.Equal(1053, craniumBasher.UniqueId);
        Assert.Equal(36500, craniumBasher.Value);
        Assert.Equal(142, craniumBasher.ItemIndex);
        Assert.Equal(6, craniumBasher.MinDamage);
        Assert.Equal(20, craniumBasher.MaxDamage);
        Assert.Equal(55, craniumBasher.MinimumStrength);
        Assert.Equal(2, craniumBasher.EquipLocation);
        Assert.Equal(20, craniumBasher.PlusDamageModifier);
        Assert.Equal(15, craniumBasher.PlusStrength);
        Assert.Equal(255, craniumBasher.Durability);
        Assert.Equal(255, craniumBasher.MaxDurability);
        Assert.Equal(-(150 << 6), craniumBasher.PlusMana);
        Assert.Equal(5, craniumBasher.PlusFireResistance);
        Assert.Equal(5, craniumBasher.PlusLightningResistance);
        Assert.Equal(5, craniumBasher.PlusMagicResistance);
        var schaefersHammer = hellfireItems.GenerateUnique(1054, 0x12345678, 16);
        Assert.Equal(1054, schaefersHammer.UniqueId);
        Assert.Equal(56125, schaefersHammer.Value);
        Assert.Equal(138, schaefersHammer.ItemIndex);
        Assert.Equal(5, schaefersHammer.MinDamage);
        Assert.Equal(9, schaefersHammer.MaxDamage);
        Assert.Equal(40, schaefersHammer.MinimumStrength);
        Assert.Equal(1, schaefersHammer.EquipLocation);
        Assert.Equal(-100, schaefersHammer.PlusDamage);
        Assert.Equal(1, schaefersHammer.LightningMinDamage);
        Assert.Equal(50, schaefersHammer.LightningMaxDamage);
        Assert.Equal(50 << 6, schaefersHammer.PlusHitPoints);
        Assert.Equal(30, schaefersHammer.PlusToHit);
        Assert.Equal(80, schaefersHammer.PlusLightningResistance);
        Assert.Equal(1, schaefersHammer.PlusLight);
        var dreamflange = hellfireItems.GenerateUnique(1055, 0x12345678, 26);
        Assert.Equal(1055, dreamflange.UniqueId);
        Assert.Equal(26450, dreamflange.Value);
        Assert.Equal(137, dreamflange.ItemIndex);
        Assert.Equal(1, dreamflange.MinDamage);
        Assert.Equal(8, dreamflange.MaxDamage);
        Assert.Equal(16, dreamflange.MinimumStrength);
        Assert.Equal(1, dreamflange.EquipLocation);
        Assert.Equal(30, dreamflange.PlusMagic);
        Assert.Equal(50 << 6, dreamflange.PlusMana);
        Assert.Equal(50, dreamflange.PlusMagicResistance);
        Assert.Equal(2, dreamflange.PlusLight);
        Assert.Equal(1, dreamflange.SpellLevelAdd);
        var staffOfShadows = hellfireItems.GenerateUnique(1056, 0x12345678, 2);
        Assert.Equal(1056, staffOfShadows.UniqueId);
        Assert.Equal(1250, staffOfShadows.Value);
        Assert.Equal(153, staffOfShadows.ItemIndex);
        Assert.Equal(4, staffOfShadows.MinDamage);
        Assert.Equal(8, staffOfShadows.MaxDamage);
        Assert.Equal(0, staffOfShadows.MinimumStrength);
        Assert.Equal(2, staffOfShadows.EquipLocation);
        Assert.Equal(-10, staffOfShadows.PlusMagic);
        Assert.Equal(10, staffOfShadows.PlusToHit);
        Assert.Equal(60, staffOfShadows.PlusDamage);
        Assert.Equal(-2, staffOfShadows.PlusLight);
        Assert.NotEqual(0U, staffOfShadows.Flags & 0x00020000U);
        var immolator = hellfireItems.GenerateUnique(1057, 0x12345678, 4);
        Assert.Equal(1057, immolator.UniqueId);
        Assert.Equal(3900, immolator.Value);
        Assert.Equal(153, immolator.ItemIndex);
        Assert.Equal(4, immolator.MinDamage);
        Assert.Equal(8, immolator.MaxDamage);
        Assert.Equal(0, immolator.MinimumStrength);
        Assert.Equal(2, immolator.EquipLocation);
        Assert.Equal(20, immolator.PlusFireResistance);
        Assert.Equal(4, immolator.FireMinDamage);
        Assert.Equal(4, immolator.FireMaxDamage);
        Assert.NotEqual(0U, immolator.Flags & 0x00000010U);
        Assert.Equal(10 << 6, immolator.PlusMana);
        Assert.Equal(-5, immolator.PlusVitality);
        var stormSpire = hellfireItems.GenerateUnique(1058, 0x12345678, 8);
        Assert.Equal(1058, stormSpire.UniqueId);
        Assert.Equal(22500, stormSpire.Value);
        Assert.Equal(156, stormSpire.ItemIndex);
        Assert.Equal(8, stormSpire.MinDamage);
        Assert.Equal(16, stormSpire.MaxDamage);
        Assert.Equal(30, stormSpire.MinimumStrength);
        Assert.Equal(2, stormSpire.EquipLocation);
        Assert.Equal(50, stormSpire.PlusLightningResistance);
        Assert.Equal(2, stormSpire.LightningMinDamage);
        Assert.Equal(8, stormSpire.LightningMaxDamage);
        Assert.NotEqual(0U, stormSpire.Flags & 0x00000020U);
        Assert.Equal(10, stormSpire.PlusStrength);
        Assert.Equal(-10, stormSpire.PlusMagic);
        var gleamsong = hellfireItems.GenerateUnique(1059, 0x12345678, 8);
        Assert.Equal(1059, gleamsong.UniqueId);
        Assert.Equal(6520, gleamsong.Value);
        Assert.Equal(152, gleamsong.ItemIndex);
        Assert.Equal(1, gleamsong.MinDamage);
        Assert.Equal(4, gleamsong.MaxDamage);
        Assert.Equal(0, gleamsong.MinimumStrength);
        Assert.Equal(2, gleamsong.EquipLocation);
        Assert.Equal(25 << 6, gleamsong.PlusMana);
        Assert.Equal(-3, gleamsong.PlusStrength);
        Assert.Equal(-3, gleamsong.PlusVitality);
        Assert.Equal(10, gleamsong.SpellId);
        Assert.Equal(76, gleamsong.Charges);
        Assert.Equal(76, gleamsong.MaxCharges);
        var thundercall = hellfireItems.GenerateUnique(1060, 0x12345678, 14);
        Assert.Equal(1060, thundercall.UniqueId);
        Assert.Equal(22250, thundercall.Value);
        Assert.Equal(154, thundercall.ItemIndex);
        Assert.Equal(5, thundercall.MinDamage);
        Assert.Equal(10, thundercall.MaxDamage);
        Assert.Equal(0, thundercall.MinimumStrength);
        Assert.Equal(2, thundercall.EquipLocation);
        Assert.Equal(35, thundercall.PlusToHit);
        Assert.Equal(1, thundercall.LightningMinDamage);
        Assert.Equal(10, thundercall.LightningMaxDamage);
        Assert.NotEqual(0U, thundercall.Flags & 0x00000020U);
        Assert.Equal(3, thundercall.SpellId);
        Assert.Equal(76, thundercall.Charges);
        Assert.Equal(76, thundercall.MaxCharges);
        Assert.Equal(30, thundercall.PlusLightningResistance);
        Assert.Equal(2, thundercall.PlusLight);
        var theProtector = hellfireItems.GenerateUnique(1061, 0x12345678, 16);
        Assert.Equal(1061, theProtector.UniqueId);
        Assert.Equal(17240, theProtector.Value);
        Assert.Equal(152, theProtector.ItemIndex);
        Assert.Equal(1, theProtector.MinDamage);
        Assert.Equal(4, theProtector.MaxDamage);
        Assert.Equal(0, theProtector.MinimumStrength);
        Assert.Equal(2, theProtector.EquipLocation);
        Assert.Equal(5, theProtector.PlusVitality);
        Assert.Equal(-5, theProtector.PlusGetHit);
        Assert.Equal(40, theProtector.ArmorClass);
        Assert.Equal(2, theProtector.SpellId);
        Assert.Equal(86, theProtector.Charges);
        Assert.Equal(86, theProtector.MaxCharges);
        Assert.NotEqual(0U, theProtector.Flags & 0x04000000U);
        var najsPuzzler = hellfireItems.GenerateUnique(1062, 0x12345678, 18);
        Assert.Equal(1062, najsPuzzler.UniqueId);
        Assert.Equal(34000, najsPuzzler.Value);
        Assert.Equal(153, najsPuzzler.ItemIndex);
        Assert.Equal(4, najsPuzzler.MinDamage);
        Assert.Equal(8, najsPuzzler.MaxDamage);
        Assert.Equal(0, najsPuzzler.MinimumStrength);
        Assert.Equal(2, najsPuzzler.EquipLocation);
        Assert.Equal(20, najsPuzzler.PlusMagic);
        Assert.Equal(10, najsPuzzler.PlusDexterity);
        Assert.Equal(20, najsPuzzler.PlusFireResistance);
        Assert.Equal(20, najsPuzzler.PlusLightningResistance);
        Assert.Equal(20, najsPuzzler.PlusMagicResistance);
        Assert.Equal(23, najsPuzzler.SpellId);
        Assert.Equal(57, najsPuzzler.Charges);
        Assert.Equal(57, najsPuzzler.MaxCharges);
        Assert.Equal(-(25 << 6), najsPuzzler.PlusHitPoints);
        var mindcry = hellfireItems.GenerateUnique(1063, 0x12345678, 20);
        Assert.Equal(1063, mindcry.UniqueId);
        Assert.Equal(41500, mindcry.Value);
        Assert.Equal(155, mindcry.ItemIndex);
        Assert.Equal(6, mindcry.MinDamage);
        Assert.Equal(12, mindcry.MaxDamage);
        Assert.Equal(20, mindcry.MinimumStrength);
        Assert.Equal(2, mindcry.EquipLocation);
        Assert.Equal(15, mindcry.PlusMagic);
        Assert.Equal(13, mindcry.SpellId);
        Assert.Equal(69, mindcry.Charges);
        Assert.Equal(69, mindcry.MaxCharges);
        Assert.Equal(15, mindcry.PlusFireResistance);
        Assert.Equal(15, mindcry.PlusLightningResistance);
        Assert.Equal(15, mindcry.PlusMagicResistance);
        Assert.Equal(1, mindcry.SpellLevelAdd);
        var rodOfOnan = hellfireItems.GenerateUnique(1064, 0x12345678, 22);
        Assert.Equal(1064, rodOfOnan.UniqueId);
        Assert.Equal(44167, rodOfOnan.Value);
        Assert.Equal(156, rodOfOnan.ItemIndex);
        Assert.Equal(8, rodOfOnan.MinDamage);
        Assert.Equal(16, rodOfOnan.MaxDamage);
        Assert.Equal(30, rodOfOnan.MinimumStrength);
        Assert.Equal(2, rodOfOnan.EquipLocation);
        Assert.Equal(21, rodOfOnan.SpellId);
        Assert.Equal(50, rodOfOnan.Charges);
        Assert.Equal(50, rodOfOnan.MaxCharges);
        Assert.Equal(100, rodOfOnan.PlusDamage);
        Assert.Equal(5, rodOfOnan.PlusStrength);
        var helmOfSpirits = hellfireItems.GenerateUnique(1065, 0x12345678, 1);
        Assert.Equal(1065, helmOfSpirits.UniqueId);
        Assert.Equal(7525, helmOfSpirits.Value);
        Assert.Equal(51, helmOfSpirits.ItemIndex);
        Assert.Equal(4, helmOfSpirits.ArmorClass);
        Assert.Equal(30, helmOfSpirits.Durability);
        Assert.Equal(30, helmOfSpirits.MaxDurability);
        Assert.Equal(25, helmOfSpirits.MinimumStrength);
        Assert.Equal(4, helmOfSpirits.EquipLocation);
        Assert.NotEqual(0U, helmOfSpirits.Flags & 0x00010000U);
        var thinkingCap = hellfireItems.GenerateUnique(1066, 0x12345678, 6);
        Assert.Equal(1066, thinkingCap.UniqueId);
        Assert.Equal(2020, thinkingCap.Value);
        Assert.Equal(50, thinkingCap.ItemIndex);
        Assert.Equal(4, thinkingCap.ArmorClass);
        Assert.Equal(1, thinkingCap.Durability);
        Assert.Equal(1, thinkingCap.MaxDurability);
        Assert.Equal(0, thinkingCap.MinimumStrength);
        Assert.Equal(4, thinkingCap.EquipLocation);
        Assert.Equal(30 << 6, thinkingCap.PlusMana);
        Assert.Equal(2, thinkingCap.SpellLevelAdd);
        Assert.Equal(20, thinkingCap.PlusFireResistance);
        Assert.Equal(20, thinkingCap.PlusLightningResistance);
        Assert.Equal(20, thinkingCap.PlusMagicResistance);
        var overlordsHelm = hellfireItems.GenerateUnique(1067, 0x12345678, 7);
        Assert.Equal(1067, overlordsHelm.UniqueId);
        Assert.Equal(12500, overlordsHelm.Value);
        Assert.Equal(51, overlordsHelm.ItemIndex);
        Assert.Equal(4, overlordsHelm.ArmorClass);
        Assert.Equal(15, overlordsHelm.Durability);
        Assert.Equal(15, overlordsHelm.MaxDurability);
        Assert.Equal(25, overlordsHelm.MinimumStrength);
        Assert.Equal(4, overlordsHelm.EquipLocation);
        Assert.Equal(20, overlordsHelm.PlusStrength);
        Assert.Equal(15, overlordsHelm.PlusDexterity);
        Assert.Equal(5, overlordsHelm.PlusVitality);
        Assert.Equal(-20, overlordsHelm.PlusMagic);
        var foolsCrest = hellfireItems.GenerateUnique(1068, 0x12345678, 12);
        Assert.Equal(1068, foolsCrest.UniqueId);
        Assert.Equal(10150, foolsCrest.Value);
        Assert.Equal(51, foolsCrest.ItemIndex);
        Assert.Equal(4, foolsCrest.ArmorClass);
        Assert.Equal(30, foolsCrest.Durability);
        Assert.Equal(30, foolsCrest.MaxDurability);
        Assert.Equal(25, foolsCrest.MinimumStrength);
        Assert.Equal(4, foolsCrest.EquipLocation);
        Assert.Equal(-4, foolsCrest.PlusStrength);
        Assert.Equal(-4, foolsCrest.PlusMagic);
        Assert.Equal(-4, foolsCrest.PlusDexterity);
        Assert.Equal(-4, foolsCrest.PlusVitality);
        Assert.Equal(100 << 6, foolsCrest.PlusHitPoints);
        Assert.InRange(foolsCrest.PlusGetHit, 1, 6);
        Assert.NotEqual(0U, foolsCrest.Flags & 0x04000000U);
        var gotterdamerung = hellfireItems.GenerateUnique(1069, 0x12345678, 21);
        Assert.Equal(1069, gotterdamerung.UniqueId);
        Assert.Equal(54900, gotterdamerung.Value);
        Assert.Equal(54, gotterdamerung.ItemIndex);
        Assert.Equal(60, gotterdamerung.ArmorClass);
        Assert.Equal(60, gotterdamerung.Durability);
        Assert.Equal(60, gotterdamerung.MaxDurability);
        Assert.Equal(50, gotterdamerung.MinimumStrength);
        Assert.Equal(4, gotterdamerung.EquipLocation);
        Assert.Equal(20, gotterdamerung.PlusStrength);
        Assert.Equal(20, gotterdamerung.PlusMagic);
        Assert.Equal(20, gotterdamerung.PlusDexterity);
        Assert.Equal(20, gotterdamerung.PlusVitality);
        Assert.Equal(-4, gotterdamerung.PlusGetHit);
        Assert.Equal(-4, gotterdamerung.PlusLight);
        Assert.NotEqual(0U, gotterdamerung.Flags & 0x80000000U);
        var royalCirclet = hellfireItems.GenerateUnique(1070, 0x12345678, 27);
        Assert.Equal(1070, royalCirclet.UniqueId);
        Assert.Equal(24875, royalCirclet.Value);
        Assert.Equal(53, royalCirclet.ItemIndex);
        Assert.Equal(40, royalCirclet.ArmorClass);
        Assert.Equal(40, royalCirclet.Durability);
        Assert.Equal(40, royalCirclet.MaxDurability);
        Assert.Equal(0, royalCirclet.MinimumStrength);
        Assert.Equal(4, royalCirclet.EquipLocation);
        Assert.Equal(10, royalCirclet.PlusStrength);
        Assert.Equal(10, royalCirclet.PlusMagic);
        Assert.Equal(10, royalCirclet.PlusDexterity);
        Assert.Equal(10, royalCirclet.PlusVitality);
        Assert.Equal(40 << 6, royalCirclet.PlusMana);
        Assert.Equal(1, royalCirclet.PlusLight);
        var tornFlesh = hellfireItems.GenerateUnique(1071, 0x12345678, 2);
        Assert.Equal(1071, tornFlesh.UniqueId);
        Assert.Equal(4825, tornFlesh.Value);
        Assert.Equal(56, tornFlesh.ItemIndex);
        Assert.Equal(8, tornFlesh.ArmorClass);
        Assert.Equal(255, tornFlesh.Durability);
        Assert.Equal(255, tornFlesh.MaxDurability);
        Assert.Equal(0, tornFlesh.MinimumStrength);
        Assert.Equal(4, tornFlesh.EquipLocation);
        Assert.Equal(10, tornFlesh.PlusVitality);
        Assert.Equal(-1, tornFlesh.PlusGetHit);
        var gladiatorsBane = hellfireItems.GenerateUnique(1072, 0x12345678, 6);
        Assert.Equal(1072, gladiatorsBane.UniqueId);
        Assert.Equal(3450, gladiatorsBane.Value);
        Assert.Equal(62, gladiatorsBane.ItemIndex);
        Assert.Equal(25, gladiatorsBane.ArmorClass);
        Assert.Equal(135, gladiatorsBane.Durability);
        Assert.Equal(135, gladiatorsBane.MaxDurability);
        Assert.Equal(20, gladiatorsBane.MinimumStrength);
        Assert.Equal(4, gladiatorsBane.EquipLocation);
        Assert.Equal(-3, gladiatorsBane.PlusStrength);
        Assert.Equal(-3, gladiatorsBane.PlusMagic);
        Assert.Equal(-3, gladiatorsBane.PlusDexterity);
        Assert.Equal(-3, gladiatorsBane.PlusVitality);
        Assert.Equal(-2, gladiatorsBane.PlusGetHit);
        var rainbowCloak = hellfireItems.GenerateUnique(1073, 0x12345678, 2);
        Assert.Equal(1073, rainbowCloak.UniqueId);
        Assert.Equal(4900, rainbowCloak.Value);
        Assert.Equal(57, rainbowCloak.ItemIndex);
        Assert.Equal(10, rainbowCloak.ArmorClass);
        Assert.Equal(27, rainbowCloak.Durability);
        Assert.Equal(27, rainbowCloak.MaxDurability);
        Assert.Equal(0, rainbowCloak.MinimumStrength);
        Assert.Equal(4, rainbowCloak.EquipLocation);
        Assert.Equal(1, rainbowCloak.PlusStrength);
        Assert.Equal(1, rainbowCloak.PlusMagic);
        Assert.Equal(1, rainbowCloak.PlusDexterity);
        Assert.Equal(1, rainbowCloak.PlusVitality);
        Assert.Equal(10, rainbowCloak.PlusFireResistance);
        Assert.Equal(10, rainbowCloak.PlusLightningResistance);
        Assert.Equal(10, rainbowCloak.PlusMagicResistance);
        Assert.Equal(5 << 6, rainbowCloak.PlusHitPoints);
        var leatherOfAut = hellfireItems.GenerateUnique(1074, 0x12345678, 4);
        Assert.Equal(1074, leatherOfAut.UniqueId);
        Assert.Equal(10550, leatherOfAut.Value);
        Assert.Equal(60, leatherOfAut.ItemIndex);
        Assert.Equal(15, leatherOfAut.ArmorClass);
        Assert.Equal(255, leatherOfAut.Durability);
        Assert.Equal(255, leatherOfAut.MaxDurability);
        Assert.Equal(0, leatherOfAut.MinimumStrength);
        Assert.Equal(4, leatherOfAut.EquipLocation);
        Assert.Equal(5, leatherOfAut.PlusStrength);
        Assert.Equal(-5, leatherOfAut.PlusMagic);
        Assert.Equal(5, leatherOfAut.PlusDexterity);
        var wisdomsWrap = hellfireItems.GenerateUnique(1075, 0x12345678, 5);
        Assert.Equal(1075, wisdomsWrap.UniqueId);
        Assert.Equal(6200, wisdomsWrap.Value);
        Assert.Equal(58, wisdomsWrap.ItemIndex);
        Assert.Equal(15, wisdomsWrap.ArmorClass);
        Assert.Equal(24, wisdomsWrap.Durability);
        Assert.Equal(24, wisdomsWrap.MaxDurability);
        Assert.Equal(0, wisdomsWrap.MinimumStrength);
        Assert.Equal(4, wisdomsWrap.EquipLocation);
        Assert.Equal(5, wisdomsWrap.PlusMagic);
        Assert.Equal(10 << 6, wisdomsWrap.PlusMana);
        Assert.Equal(25, wisdomsWrap.PlusLightningResistance);
        Assert.Equal(-1, wisdomsWrap.PlusGetHit);
        var sparkingMail = hellfireItems.GenerateUnique(1076, 0x12345678, 9);
        Assert.Equal(1076, sparkingMail.UniqueId);
        Assert.Equal(15750, sparkingMail.Value);
        Assert.Equal(63, sparkingMail.ItemIndex);
        Assert.Equal(30, sparkingMail.ArmorClass);
        Assert.Equal(55, sparkingMail.Durability);
        Assert.Equal(55, sparkingMail.MaxDurability);
        Assert.Equal(30, sparkingMail.MinimumStrength);
        Assert.Equal(4, sparkingMail.EquipLocation);
        Assert.Equal(1, sparkingMail.LightningMinDamage);
        Assert.Equal(10, sparkingMail.LightningMaxDamage);
        Assert.NotEqual(0U, sparkingMail.Flags & 0x00000020U);
        var scavengerCarapace = hellfireItems.GenerateUnique(1077, 0x12345678, 13);
        Assert.Equal(1077, scavengerCarapace.UniqueId);
        Assert.Equal(14000, scavengerCarapace.Value);
        Assert.Equal(66, scavengerCarapace.ItemIndex);
        Assert.Equal(20, scavengerCarapace.ArmorClass);
        Assert.Equal(80, scavengerCarapace.Durability);
        Assert.Equal(80, scavengerCarapace.MaxDurability);
        Assert.Equal(40, scavengerCarapace.MinimumStrength);
        Assert.Equal(4, scavengerCarapace.EquipLocation);
        Assert.Equal(-30, scavengerCarapace.PlusArmorClass);
        Assert.Equal(-15, scavengerCarapace.PlusGetHit);
        Assert.Equal(5, scavengerCarapace.PlusDexterity);
        Assert.Equal(40, scavengerCarapace.PlusLightningResistance);
        var nightscape = hellfireItems.GenerateUnique(1078, 0x12345678, 16);
        Assert.Equal(1078, nightscape.UniqueId);
        Assert.Equal(11600, nightscape.Value);
        Assert.Equal(55, nightscape.ItemIndex);
        Assert.Equal(15, nightscape.ArmorClass);
        Assert.Equal(12, nightscape.Durability);
        Assert.Equal(12, nightscape.MaxDurability);
        Assert.Equal(0, nightscape.MinimumStrength);
        Assert.Equal(4, nightscape.EquipLocation);
        Assert.Equal(3, nightscape.PlusDexterity);
        Assert.Equal(20, nightscape.PlusFireResistance);
        Assert.Equal(20, nightscape.PlusLightningResistance);
        Assert.Equal(20, nightscape.PlusMagicResistance);
        Assert.Equal(-4, nightscape.PlusLight);
        Assert.NotEqual(0U, nightscape.Flags & 0x00400000U);
        var najsLightPlate = hellfireItems.GenerateUnique(1079, 0x12345678, 19);
        Assert.Equal(1079, najsLightPlate.UniqueId);
        Assert.Equal(78700, najsLightPlate.Value);
        Assert.Equal(68, najsLightPlate.ItemIndex);
        Assert.Equal(42, najsLightPlate.ArmorClass);
        Assert.Equal(75, najsLightPlate.Durability);
        Assert.Equal(75, najsLightPlate.MaxDurability);
        Assert.Equal(0, najsLightPlate.MinimumStrength);
        Assert.Equal(4, najsLightPlate.EquipLocation);
        Assert.Equal(5, najsLightPlate.PlusMagic);
        Assert.Equal(20 << 6, najsLightPlate.PlusMana);
        Assert.Equal(20, najsLightPlate.PlusFireResistance);
        Assert.Equal(20, najsLightPlate.PlusLightningResistance);
        Assert.Equal(20, najsLightPlate.PlusMagicResistance);
        Assert.Equal(1, najsLightPlate.SpellLevelAdd);
        var demonspikeCoat = hellfireItems.GenerateUnique(1080, 0x12345678, 25);
        Assert.Equal(1080, demonspikeCoat.UniqueId);
        Assert.Equal(251175, demonspikeCoat.Value);
        Assert.Equal(71, demonspikeCoat.ItemIndex);
        Assert.Equal(100, demonspikeCoat.ArmorClass);
        Assert.Equal(255, demonspikeCoat.Durability);
        Assert.Equal(255, demonspikeCoat.MaxDurability);
        Assert.Equal(90, demonspikeCoat.MinimumStrength);
        Assert.Equal(4, demonspikeCoat.EquipLocation);
        Assert.Equal(-6, demonspikeCoat.PlusGetHit);
        Assert.Equal(10, demonspikeCoat.PlusStrength);
        Assert.Equal(50, demonspikeCoat.PlusFireResistance);
        var theDeflector = hellfireItems.GenerateUnique(1081, 0x12345678, 1);
        Assert.Equal(1081, theDeflector.UniqueId);
        Assert.Equal(1500, theDeflector.Value);
        Assert.Equal(72, theDeflector.ItemIndex);
        Assert.Equal(7, theDeflector.ArmorClass);
        Assert.Equal(16, theDeflector.Durability);
        Assert.Equal(16, theDeflector.MaxDurability);
        Assert.Equal(0, theDeflector.MinimumStrength);
        Assert.Equal(4, theDeflector.EquipLocation);
        Assert.Equal(-20, theDeflector.PlusDamage);
        Assert.Equal(-5, theDeflector.PlusToHit);
        Assert.Equal(10, theDeflector.PlusFireResistance);
        Assert.Equal(10, theDeflector.PlusLightningResistance);
        Assert.Equal(10, theDeflector.PlusMagicResistance);
        var splitSkullShield = hellfireItems.GenerateUnique(1082, 0x12345678, 1);
        Assert.Equal(1082, splitSkullShield.UniqueId);
        Assert.Equal(2025, splitSkullShield.Value);
        Assert.Equal(72, splitSkullShield.ItemIndex);
        Assert.Equal(10, splitSkullShield.ArmorClass);
        Assert.Equal(15, splitSkullShield.Durability);
        Assert.Equal(15, splitSkullShield.MaxDurability);
        Assert.Equal(0, splitSkullShield.MinimumStrength);
        Assert.Equal(4, splitSkullShield.EquipLocation);
        Assert.Equal(10 << 6, splitSkullShield.PlusHitPoints);
        Assert.Equal(2, splitSkullShield.PlusStrength);
        Assert.Equal(-1, splitSkullShield.PlusLight);
        var dragonsBreach = hellfireItems.GenerateUnique(1083, 0x12345678, 2);
        Assert.Equal(1083, dragonsBreach.UniqueId);
        Assert.Equal(19200, dragonsBreach.Value);
        Assert.Equal(75, dragonsBreach.ItemIndex);
        Assert.Equal(20, dragonsBreach.ArmorClass);
        Assert.Equal(255, dragonsBreach.Durability);
        Assert.Equal(255, dragonsBreach.MaxDurability);
        Assert.Equal(50, dragonsBreach.MinimumStrength);
        Assert.Equal(4, dragonsBreach.EquipLocation);
        Assert.Equal(25, dragonsBreach.PlusFireResistance);
        Assert.Equal(5, dragonsBreach.PlusStrength);
        Assert.Equal(-5, dragonsBreach.PlusMagic);
        var blackoakShield = hellfireItems.GenerateUnique(1084, 0x12345678, 4);
        Assert.Equal(1084, blackoakShield.UniqueId);
        Assert.Equal(5725, blackoakShield.Value);
        Assert.Equal(73, blackoakShield.ItemIndex);
        Assert.Equal(18, blackoakShield.ArmorClass);
        Assert.Equal(60, blackoakShield.Durability);
        Assert.Equal(60, blackoakShield.MaxDurability);
        Assert.Equal(25, blackoakShield.MinimumStrength);
        Assert.Equal(4, blackoakShield.EquipLocation);
        Assert.Equal(10, blackoakShield.PlusDexterity);
        Assert.Equal(-10, blackoakShield.PlusVitality);
        Assert.Equal(-1, blackoakShield.PlusLight);
        var holyDefender = hellfireItems.GenerateUnique(1085, 0x12345678, 10);
        Assert.Equal(1085, holyDefender.UniqueId);
        Assert.Equal(13800, holyDefender.Value);
        Assert.Equal(74, holyDefender.ItemIndex);
        Assert.Equal(15, holyDefender.ArmorClass);
        Assert.Equal(96, holyDefender.Durability);
        Assert.Equal(96, holyDefender.MaxDurability);
        Assert.Equal(40, holyDefender.MinimumStrength);
        Assert.Equal(4, holyDefender.EquipLocation);
        Assert.Equal(-2, holyDefender.PlusGetHit);
        Assert.Equal(20, holyDefender.PlusFireResistance);
        Assert.NotEqual(0U, holyDefender.Flags & 0x01000000U);
        var stormshield = hellfireItems.GenerateUnique(1086, 0x12345678, 24);
        Assert.Equal(1086, stormshield.UniqueId);
        Assert.Equal(49000, stormshield.Value);
        Assert.Equal(77, stormshield.ItemIndex);
        Assert.Equal(40, stormshield.ArmorClass);
        Assert.Equal(255, stormshield.Durability);
        Assert.Equal(255, stormshield.MaxDurability);
        Assert.Equal(80, stormshield.MinimumStrength);
        Assert.Equal(4, stormshield.EquipLocation);
        Assert.Equal(4, stormshield.PlusGetHit);
        Assert.Equal(10, stormshield.PlusStrength);
        Assert.Equal(50, stormshield.PlusLightningResistance);
        Assert.NotEqual(0U, stormshield.Flags & 0x01000000U);
        var bramble = hellfireItems.GenerateUnique(1087, 0x12345678, 1);
        Assert.Equal(1087, bramble.UniqueId);
        Assert.Equal(1000, bramble.Value);
        Assert.Equal(157, bramble.ItemIndex);
        Assert.Equal(0, bramble.ArmorClass);
        Assert.Equal(5, bramble.EquipLocation);
        Assert.Equal(-2, bramble.PlusStrength);
        Assert.Equal(-2, bramble.PlusMagic);
        Assert.Equal(-2, bramble.PlusDexterity);
        Assert.Equal(-2, bramble.PlusVitality);
        Assert.Equal(3, bramble.PlusDamageModifier);
        Assert.Equal(10 << 6, bramble.PlusMana);
        var ringOfRegha = hellfireItems.GenerateUnique(1088, 0x12345678, 1);
        Assert.Equal(1088, ringOfRegha.UniqueId);
        Assert.Equal(4175, ringOfRegha.Value);
        Assert.Equal(157, ringOfRegha.ItemIndex);
        Assert.Equal(5, ringOfRegha.EquipLocation);
        Assert.Equal(10, ringOfRegha.PlusMagic);
        Assert.Equal(10, ringOfRegha.PlusMagicResistance);
        Assert.Equal(1, ringOfRegha.PlusLight);
        Assert.Equal(-3, ringOfRegha.PlusStrength);
        Assert.Equal(-3, ringOfRegha.PlusDexterity);
        var theBleeder = hellfireItems.GenerateUnique(1089, 0x12345678, 2);
        Assert.Equal(1089, theBleeder.UniqueId);
        Assert.Equal(8500, theBleeder.Value);
        Assert.Equal(157, theBleeder.ItemIndex);
        Assert.Equal(5, theBleeder.EquipLocation);
        Assert.Equal(20, theBleeder.PlusMagicResistance);
        Assert.Equal(30 << 6, theBleeder.PlusMana);
        Assert.Equal(-(10 << 6), theBleeder.PlusHitPoints);
        var constrictingRing = hellfireItems.GenerateUnique(1090, 0x12345678, 5);
        Assert.Equal(1090, constrictingRing.UniqueId);
        Assert.Equal(62000, constrictingRing.Value);
        Assert.Equal(157, constrictingRing.ItemIndex);
        Assert.Equal(5, constrictingRing.EquipLocation);
        Assert.Equal(75, constrictingRing.PlusFireResistance);
        Assert.Equal(75, constrictingRing.PlusLightningResistance);
        Assert.Equal(75, constrictingRing.PlusMagicResistance);
        Assert.NotEqual(0U, constrictingRing.Flags & 0x00000040U);
        var ringOfEngagement = hellfireItems.GenerateUnique(1091, 0x12345678, 11);
        Assert.Equal(1091, ringOfEngagement.UniqueId);
        Assert.Equal(12476, ringOfEngagement.Value);
        Assert.Equal(157, ringOfEngagement.ItemIndex);
        Assert.Equal(5, ringOfEngagement.EquipLocation);
        Assert.Equal(5, ringOfEngagement.ArmorClass);
        Assert.InRange(ringOfEngagement.PlusGetHit, -2, -1);
        Assert.Equal(2, ringOfEngagement.PlusEnemyArmorClass);
        Assert.NotEqual(0U, ringOfEngagement.Flags & 0x04000000U);
        var giantsKnuckle = hellfireItems.GenerateUnique(1092, 0x12345678, 8);
        Assert.Equal(1092, giantsKnuckle.UniqueId);
        Assert.Equal(8000, giantsKnuckle.Value);
        Assert.Equal(157, giantsKnuckle.ItemIndex);
        Assert.Equal(5, giantsKnuckle.EquipLocation);
        Assert.Equal(60, giantsKnuckle.PlusStrength);
        Assert.Equal(-30, giantsKnuckle.PlusDexterity);
        var mercurialRing = hellfireItems.GenerateUnique(1093, 0x12345678, 8);
        Assert.Equal(1093, mercurialRing.UniqueId);
        Assert.Equal(8000, mercurialRing.Value);
        Assert.Equal(157, mercurialRing.ItemIndex);
        Assert.Equal(5, mercurialRing.EquipLocation);
        Assert.Equal(60, mercurialRing.PlusDexterity);
        Assert.Equal(-30, mercurialRing.PlusStrength);
        var xorinesRing = hellfireItems.GenerateUnique(1094, 0x12345678, 8);
        Assert.Equal(1094, xorinesRing.UniqueId);
        Assert.Equal(8000, xorinesRing.Value);
        Assert.Equal(157, xorinesRing.ItemIndex);
        Assert.Equal(5, xorinesRing.EquipLocation);
        Assert.Equal(60, xorinesRing.PlusMagic);
        Assert.Equal(-30, xorinesRing.PlusStrength);
        var kariksRing = hellfireItems.GenerateUnique(1095, 0x12345678, 8);
        Assert.Equal(1095, kariksRing.UniqueId);
        Assert.Equal(8000, kariksRing.Value);
        Assert.Equal(157, kariksRing.ItemIndex);
        Assert.Equal(5, kariksRing.EquipLocation);
        Assert.Equal(60, kariksRing.PlusVitality);
        Assert.Equal(-30, kariksRing.PlusMagic);
        var ringOfMagma = hellfireItems.GenerateUnique(1096, 0x12345678, 8);
        Assert.Equal(1096, ringOfMagma.UniqueId);
        Assert.Equal(8000, ringOfMagma.Value);
        Assert.Equal(157, ringOfMagma.ItemIndex);
        Assert.Equal(5, ringOfMagma.EquipLocation);
        Assert.Equal(60, ringOfMagma.PlusFireResistance);
        Assert.Equal(-30, ringOfMagma.PlusLightningResistance);
        Assert.Equal(-30, ringOfMagma.PlusMagicResistance);
        var ringOfTheMystics = hellfireItems.GenerateUnique(1097, 0x12345678, 8);
        Assert.Equal(1097, ringOfTheMystics.UniqueId);
        Assert.Equal(8000, ringOfTheMystics.Value);
        Assert.Equal(157, ringOfTheMystics.ItemIndex);
        Assert.Equal(5, ringOfTheMystics.EquipLocation);
        Assert.Equal(60, ringOfTheMystics.PlusMagicResistance);
        Assert.Equal(-30, ringOfTheMystics.PlusFireResistance);
        Assert.Equal(-30, ringOfTheMystics.PlusLightningResistance);
        var ringOfThunder = hellfireItems.GenerateUnique(1098, 0x12345678, 8);
        Assert.Equal(1098, ringOfThunder.UniqueId);
        Assert.Equal(8000, ringOfThunder.Value);
        Assert.Equal(157, ringOfThunder.ItemIndex);
        Assert.Equal(5, ringOfThunder.EquipLocation);
        Assert.Equal(60, ringOfThunder.PlusLightningResistance);
        Assert.Equal(-30, ringOfThunder.PlusFireResistance);
        Assert.Equal(-30, ringOfThunder.PlusMagicResistance);
        var amuletOfWarding = hellfireItems.GenerateUnique(1099, 0x12345678, 12);
        Assert.Equal(1099, amuletOfWarding.UniqueId);
        Assert.Equal(30000, amuletOfWarding.Value);
        Assert.Equal(160, amuletOfWarding.ItemIndex);
        Assert.Equal(6, amuletOfWarding.EquipLocation);
        Assert.Equal(27, amuletOfWarding.MiscId);
        Assert.Equal(40, amuletOfWarding.PlusFireResistance);
        Assert.Equal(40, amuletOfWarding.PlusLightningResistance);
        Assert.Equal(40, amuletOfWarding.PlusMagicResistance);
        Assert.Equal(-6400, amuletOfWarding.PlusHitPoints);
        var gnatSting = hellfireItems.GenerateUnique(1100, 0x12345678, 15);
        Assert.Equal(1100, gnatSting.UniqueId);
        Assert.Equal(30000, gnatSting.Value);
        Assert.Equal(145, gnatSting.ItemIndex);
        Assert.Equal(2, gnatSting.EquipLocation);
        Assert.Equal(1, gnatSting.MinDamage);
        Assert.Equal(2, gnatSting.MaxDamage);
        Assert.Equal(255, gnatSting.Durability);
        Assert.Equal(255, gnatSting.MaxDurability);
        Assert.NotEqual(0U, gnatSting.Flags & 0x00000200U);
        Assert.NotEqual(0U, gnatSting.Flags & 0x00020000U);
        var flambeau = hellfireItems.GenerateUnique(1101, 0x12345678, 11);
        Assert.Equal(1101, flambeau.UniqueId);
        Assert.Equal(30000, flambeau.Value);
        Assert.Equal(147, flambeau.ItemIndex);
        Assert.Equal(2, flambeau.EquipLocation);
        Assert.Equal(0, flambeau.MinDamage);
        Assert.Equal(0, flambeau.MaxDamage);
        Assert.Equal(15, flambeau.FireMinDamage);
        Assert.Equal(20, flambeau.FireMaxDamage);
        Assert.Equal(255, flambeau.Durability);
        Assert.Equal(255, flambeau.MaxDurability);
        Assert.NotEqual(0U, flambeau.Flags & 0x00000008U);
        Assert.NotEqual(0U, flambeau.Flags & 0x02000000U);
        var armorOfGloom = hellfireItems.GenerateUnique(1102, 0x12345678, 25);
        Assert.Equal(1102, armorOfGloom.UniqueId);
        Assert.Equal(200000, armorOfGloom.Value);
        Assert.Equal(71, armorOfGloom.ItemIndex);
        Assert.Equal(4, armorOfGloom.EquipLocation);
        Assert.Equal(225, armorOfGloom.ArmorClass);
        Assert.Equal(0, armorOfGloom.MinimumStrength);
        Assert.Equal(-2, armorOfGloom.PlusLight);
        Assert.Equal(90, armorOfGloom.Durability);
        Assert.Equal(90, armorOfGloom.MaxDurability);
        Assert.NotEqual(0U, armorOfGloom.Flags & 0x80000000U);
        var blitzen = hellfireItems.GenerateUnique(1103, 0x12345678, 13);
        Assert.Equal(1103, blitzen.UniqueId);
        Assert.Equal(30000, blitzen.Value);
        Assert.Equal(147, blitzen.ItemIndex);
        Assert.Equal(2, blitzen.EquipLocation);
        Assert.Equal(0, blitzen.MinDamage);
        Assert.Equal(0, blitzen.MaxDamage);
        Assert.Equal(10, blitzen.FireMinDamage);
        Assert.Equal(15, blitzen.FireMaxDamage);
        Assert.Equal(1, blitzen.LightningMinDamage);
        Assert.Equal(0, blitzen.LightningMaxDamage);
        Assert.Equal(255, blitzen.Durability);
        Assert.Equal(255, blitzen.MaxDurability);
        Assert.NotEqual(0U, blitzen.Flags & 0x02000000U);
        Assert.NotEqual(0U, blitzen.Flags & 0x00000008U);
        var thunderclap = hellfireItems.GenerateUnique(1104, 0x12345678, 13);
        Assert.Equal(1104, thunderclap.UniqueId);
        Assert.Equal(30000, thunderclap.Value);
        Assert.Equal(138, thunderclap.ItemIndex);
        Assert.Equal(1, thunderclap.EquipLocation);
        Assert.Equal(20, thunderclap.PlusStrength);
        Assert.Equal(30, thunderclap.PlusLightningResistance);
        Assert.Equal(2, thunderclap.PlusLight);
        Assert.Equal(3, thunderclap.FireMinDamage);
        Assert.Equal(6, thunderclap.FireMaxDamage);
        Assert.Equal(2, thunderclap.LightningMinDamage);
        Assert.Equal(0, thunderclap.LightningMaxDamage);
        Assert.Equal(255, thunderclap.Durability);
        Assert.Equal(255, thunderclap.MaxDurability);
        Assert.NotEqual(0U, thunderclap.Flags & 0x00000020U);
        Assert.NotEqual(0U, thunderclap.Flags & 0x00000010U);
        var shirotachi = hellfireItems.GenerateUnique(1105, 0x12345678, 21);
        Assert.Equal(1105, shirotachi.UniqueId);
        Assert.Equal(36000, shirotachi.Value);
        Assert.Equal(130, shirotachi.ItemIndex);
        Assert.Equal(1, shirotachi.EquipLocation);
        Assert.Equal(10, shirotachi.MinDamage);
        Assert.Equal(20, shirotachi.MaxDamage);
        Assert.Equal(6, shirotachi.LightningMinDamage);
        Assert.Equal(6, shirotachi.LightningMaxDamage);
        Assert.Equal(2, shirotachi.PlusEnemyArmorClass);
        Assert.Equal(100, shirotachi.Durability);
        Assert.Equal(100, shirotachi.MaxDurability);
        Assert.NotEqual(0U, shirotachi.Flags & 0x00100000U);
        Assert.NotEqual(0U, shirotachi.Flags & 0x00000020U);
        var eaterOfSouls = hellfireItems.GenerateUnique(1106, 0x12345678, 23);
        Assert.Equal(1106, eaterOfSouls.UniqueId);
        Assert.Equal(42000, eaterOfSouls.Value);
        Assert.Equal(129, eaterOfSouls.ItemIndex);
        Assert.Equal(1, eaterOfSouls.EquipLocation);
        Assert.Equal(8, eaterOfSouls.MinDamage);
        Assert.Equal(16, eaterOfSouls.MaxDamage);
        Assert.Equal(3200, eaterOfSouls.PlusHitPoints);
        Assert.Equal(255, eaterOfSouls.Durability);
        Assert.Equal(255, eaterOfSouls.MaxDurability);
        Assert.NotEqual(0U, eaterOfSouls.Flags & 0x00010000U);
        Assert.NotEqual(0U, eaterOfSouls.Flags & 0x00004000U);
        Assert.NotEqual(0U, eaterOfSouls.Flags & 0x00000040U);
        var diamondedge = hellfireItems.GenerateUnique(1107, 0x12345678, 17);
        Assert.Equal(1107, diamondedge.UniqueId);
        Assert.Equal(42000, diamondedge.Value);
        Assert.Equal(126, diamondedge.ItemIndex);
        Assert.Equal(1, diamondedge.EquipLocation);
        Assert.Equal(2, diamondedge.MinDamage);
        Assert.Equal(10, diamondedge.MaxDamage);
        Assert.Equal(10, diamondedge.ArmorClass);
        Assert.Equal(50, diamondedge.PlusToHit);
        Assert.Equal(100, diamondedge.PlusDamage);
        Assert.Equal(50, diamondedge.PlusLightningResistance);
        Assert.Equal(10, diamondedge.Durability);
        Assert.Equal(10, diamondedge.MaxDurability);
        var boneChainArmor = hellfireItems.GenerateUnique(1108, 0x12345678, 13);
        Assert.Equal(1108, boneChainArmor.UniqueId);
        Assert.Equal(36000, boneChainArmor.Value);
        Assert.Equal(62, boneChainArmor.ItemIndex);
        Assert.Equal(4, boneChainArmor.EquipLocation);
        Assert.Equal(40, boneChainArmor.ArmorClass);
        Assert.Equal(45, boneChainArmor.Durability);
        Assert.Equal(45, boneChainArmor.MaxDurability);
        Assert.Equal(64, boneChainArmor.HellfireDamageArmorFlags);
        var demonPlateArmor = hellfireItems.GenerateUnique(1109, 0x12345678, 25);
        Assert.Equal(1109, demonPlateArmor.UniqueId);
        Assert.Equal(80000, demonPlateArmor.Value);
        Assert.Equal(71, demonPlateArmor.ItemIndex);
        Assert.Equal(4, demonPlateArmor.EquipLocation);
        Assert.Equal(80, demonPlateArmor.ArmorClass);
        Assert.Equal(90, demonPlateArmor.Durability);
        Assert.Equal(90, demonPlateArmor.MaxDurability);
        Assert.Equal(32, demonPlateArmor.HellfireDamageArmorFlags);
        var acolytesAmulet = hellfireItems.GenerateUnique(1110, 0x12345678, 10);
        Assert.Equal(1110, acolytesAmulet.UniqueId);
        Assert.Equal(10000, acolytesAmulet.Value);
        Assert.Equal(160, acolytesAmulet.ItemIndex);
        Assert.Equal(6, acolytesAmulet.EquipLocation);
        Assert.Equal(3, acolytesAmulet.ItemClass);
        Assert.Equal(50, acolytesAmulet.ManaToLifePercent);
        Assert.Equal(0, acolytesAmulet.LifeToManaPercent);
        Assert.Equal(0, acolytesAmulet.PlusMana);
        Assert.Equal(0, acolytesAmulet.PlusHitPoints);
        var gladiatorsRing = hellfireItems.GenerateUnique(1111, 0x12345678, 10);
        Assert.Equal(1111, gladiatorsRing.UniqueId);
        Assert.Equal(10000, gladiatorsRing.Value);
        Assert.Equal(157, gladiatorsRing.ItemIndex);
        Assert.Equal(5, gladiatorsRing.EquipLocation);
        Assert.Equal(3, gladiatorsRing.ItemClass);
        Assert.Equal(0, gladiatorsRing.ManaToLifePercent);
        Assert.Equal(40, gladiatorsRing.LifeToManaPercent);
        Assert.Equal(0, gladiatorsRing.PlusMana);
        Assert.Equal(0, gladiatorsRing.PlusHitPoints);
        var hellfireSorcerer = activation.PlayerLoadouts.CreateTemplate(
            "hellfire-sorcerer",
            2,
            null,
            null,
            null,
            activation.DefaultStartingLevelId);
        var startingStaff = Assert.Single(
            hellfireSorcerer.Equipment,
            item => item.Slot == 4);
        Assert.Equal(1, startingStaff.State.SpellId);
        Assert.Equal(23, startingStaff.State.MiscId);
        Assert.Equal(166, startingStaff.State.ItemIndex);
        Assert.True(startingStaff.State.StatFlag);
        Assert.Equal(2, composed.Manifest.Packs.Count);
    }

    [Fact]
    public void BaseContentManifestAndAuthoritativeCatalogsLoadDeterministically()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var firstManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);
        var secondManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);

        Assert.Equal(firstManifest.Sha256, secondManifest.Sha256);
        Assert.Equal(64, firstManifest.Sha256.Length);
        Assert.True(firstManifest.Packs.Single().Tables.Count >= 16);
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "content_ids.tsv")));
        Assert.Equal(5U, stableIds.Resolve("spell", "identify"));
        Assert.Equal(26U, stableIds.Resolve("spell", "repair"));
        Assert.Equal(27U, stableIds.Resolve("spell", "recharge"));
        Assert.Equal(28U, stableIds.Resolve("spell", "trap_disarm"));
        Assert.Equal(30U, stableIds.Resolve("spell", "charged_bolt"));
        Assert.Equal(46U, stableIds.Resolve("spell", "search"));

        var module = new DiabloModule();
        var registry = new GameplayModuleRegistry();
        registry.Load(
            [module],
            new GameplayModuleContext(
                firstManifest,
                new DelegateAuthoritativeClock(() => 0)));
        var activated = registry.GetRequiredCapability<IDiabloContentBootstrap>()
            .Activate(ComposedContent.Compose(firstManifest));
        var runtime = activated.Simulation.CreateRuntime(
            activated.PlayerLoadouts.CreateTemplate(
                "player",
                activated.PlayerClasses.Default.ClassId,
                null,
                null,
                null,
                activated.DefaultStartingLevelId));
        var initial = runtime.Capture(
            "player",
            0,
            player => activated.Simulation.CaptureSnapshot(player, 0));
        var opened = runtime.Execute(
            "player",
            1,
            (player, _) => new SimulationStepResult<DiabloIntentResult, DiabloGameplayEvent>(
                activated.Simulation.Execute(player, new DiabloOpenStoreIntent(1), 1)));
        var snapshot = runtime.Capture(
            "player",
            1,
            player => activated.Simulation.CaptureSnapshot(player, 1));
        var moved = runtime.Execute(
            "player",
            2,
            (player, participants) =>
                new SimulationStepResult<DiabloWorldIntentResult, DiabloGameplayEvent>(
                    activated.Simulation.Execute(
                        player,
                        new DiabloMoveIntent(0, 1),
                        2,
                        participants)));
        var moving = runtime.Read(
            "player",
            player => activated.Simulation.CaptureSnapshot(player, 2));

        Assert.True(opened.Succeeded);
        Assert.True(moved.Succeeded);
        Assert.Equal((0, 0), (moving.Player.PositionX, moving.Player.PositionY));
        Assert.Equal(DiabloPlayerCombatMode.WalkSouthwards, moving.Player.CombatMode);
        Assert.Equal(10UL, moving.Player.CombatModeEndTick);
        var moveAction = Assert.IsType<AuthoritativePlayerActionState>(
            moving.Player.CurrentAction);
        Assert.Equal(DiabloPlayerActionKind.Move, moveAction.Kind);
        Assert.Equal((0, 1), (moveAction.DirectionX, moveAction.DirectionY));
        Assert.Equal(6, activated.PlayerClasses.Definitions.Count);
        Assert.Equal(6, activated.PlayerAnimations.Definitions.Count);
        Assert.Same(activated.PlayerAnimations, activated.Simulation.PlayerAnimations);
        Assert.Equal("warrior", activated.PlayerClasses.Default.Symbol);
        Assert.Equal(0U, snapshot.Player.CharacterClassId);
        Assert.Equal(30, snapshot.Player.Attributes.Strength.Base);
        Assert.Equal(10, snapshot.Player.Attributes.Magic.Base);
        Assert.Equal(20, snapshot.Player.Attributes.Dexterity.Base);
        Assert.Equal(25, snapshot.Player.Attributes.Vitality.Base);
        Assert.Equal(4480, initial.Player.Life);
        Assert.Equal(4480, initial.Player.LifeMaximum);
        Assert.Equal(640, initial.Player.Mana);
        Assert.Equal(640, initial.Player.ManaMaximum);
        Assert.Equal(100U, initial.Player.Gold);
        Assert.Equal(26U, initial.Player.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Repair, initial.Player.AbilityKind);
        Assert.Empty(initial.Player.MemorizedSpells);
        Assert.Equal(2, initial.Player.Equipment.Count);
        Assert.Equal(2, initial.Player.Belt.Count);
        Assert.Equal(2, initial.Player.Inventory.Count);
        var startingGold = Assert.Single(
            initial.Player.Inventory,
            item => item.State.ItemType == activated.Simulation.CombatRules.DeathGoldItemType);
        Assert.Equal(100U, startingGold.Price);
        Assert.Equal(1, initial.Player.InventoryGrid[30]);
        Assert.Equal(4U, activated.StoreRules.GetSalePrice(new OwnedStoreItem(
            0,
            0,
            1,
            0,
            0,
            AuthoritativeItemState.Empty with { Value = 16 })));
        Assert.NotEmpty(snapshot.Monsters);
        var store = Assert.IsType<DiabloStoreState>(snapshot.ActiveStore);
        Assert.Equal(2, store.Items.Count);
        var firstStoreItem = store.Items[0];
        Assert.Equal(42U, firstStoreItem.ItemSeed);
        Assert.Equal(1, firstStoreItem.State.ItemIndex);
        Assert.Equal(4, firstStoreItem.State.MinDamage);
        Assert.Equal(8, firstStoreItem.State.MaxDamage);
        Assert.True(firstStoreItem.State.Identified);
        var secondStoreItem = store.Items[1];
        Assert.Equal(43U, secondStoreItem.ItemSeed);
        Assert.Equal(2, secondStoreItem.State.ItemIndex);
        Assert.Equal(5, secondStoreItem.State.ArmorClass);
        Assert.Equal(5, activated.Simulation.Items!.GetRequired(2).MinArmorClass);
        Assert.Equal(5, activated.Simulation.Items.GetRequired(2).MaxArmorClass);
        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        Assert.NotEqual(0U, legacyItems.ResolveLegacySymbol("IDI_WARRIOR"));
        Assert.Equal(6, legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678).MaxDamage);

        var invalidClass = snapshot with {
            Player = snapshot.Player with { CharacterClassId = 99 },
        };
        var livePlayer = runtime.Read("player", player => player);
        Assert.Throws<InvalidDataException>(() =>
            activated.Simulation.RestoreSnapshot(livePlayer, invalidClass));
        Assert.Equal(0U, livePlayer.CharacterClassId);
    }

    [Fact]
    public void BaseContentActivationRejectsAMissingDirectStartingSpell()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadDirectory("base", "1", contentRoot);
        var spells = basePack.Tables.Single(table => table.SourcePath == "spells.tsv");
        var withoutChargedBolt = TsvTable.Parse(
            spells.SourcePath,
            string.Join(
                '\n',
                spells.ToCanonicalTsv()
                    .Split('\n')
                    .Where(line => !line.StartsWith("30\t", StringComparison.Ordinal))));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == spells.SourcePath
                    ? withoutChargedBolt
                    : table)
                .ToArray());
        var incompleteManifest = new ContentManifest("base", "1", [incompletePack]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(incompleteManifest)));

        Assert.Contains("charged item 104 spell 30", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseContentGenerationAndServicePricingCoverTheShippedSources()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var rules = AuthoritativeItemGenerationRules.LoadTsv(
            "item_generation.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_generation.tsv")));
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_prefixes.tsv")),
            "item_suffixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_suffixes.tsv")),
            rules);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "items.tsv")),
            affixes);
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "unique_items.tsv")));
        items.AttachUniqueCatalog(uniques);

        var weapon = items.Generate(1, 42, 10);
        Assert.Equal(weapon, items.Generate(1, 42, 10));
        Assert.Equal(1, weapon.ItemIndex);
        Assert.Equal(4, weapon.MinDamage);
        Assert.Equal(8, weapon.MaxDamage);
        var unique = items.GenerateUnique(1001, 42, 1);
        Assert.Equal(1001, unique.UniqueId);
        Assert.Equal(3650, unique.Value);
        Assert.Equal(10, unique.PlusToHit);
        Assert.Equal(1, unique.FireMinDamage);
        Assert.Equal(10, unique.FireMaxDamage);

        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        var warrior = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678);
        var cleaver = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_CLEAVER"), 0x12345678);
        Assert.Equal(6, warrior.MaxDamage);
        Assert.Equal(24, cleaver.MaxDamage);
        Assert.Equal(2, cleaver.ItemType);

        var pricing = StoreServicePricing.LoadTsv(
            "store_services.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "store_services.tsv")));
        Assert.Equal(4, pricing.SaleDivisor);
        Assert.Equal(2, pricing.NormalRepairDivisor);
        Assert.Equal(30, pricing.MagicalRepairPercent);
        Assert.Equal(100U, pricing.IdentificationPrice);
        Assert.Equal(64, pricing.ManaChunk);
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
