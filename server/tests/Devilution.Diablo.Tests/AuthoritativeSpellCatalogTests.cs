using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeSpellCatalogTests
{
    [Fact]
    public void ProjectileBehaviorValuesRemainProtocolCompatible()
    {
        Assert.Equal(16, (int)AuthoritativeProjectileBehavior.Fireball);
        Assert.Equal(18, (int)AuthoritativeProjectileBehavior.Search);
        Assert.Equal(19, (int)AuthoritativeProjectileBehavior.HolyBolt);
        Assert.Equal(20, (int)AuthoritativeProjectileBehavior.BloodStar);
        Assert.Equal(21, (int)AuthoritativeProjectileBehavior.BoneSpirit);
        Assert.Equal(30, (int)AuthoritativeProjectileBehavior.NovaController);
        Assert.Equal(35, (int)AuthoritativeProjectileBehavior.Elemental);
        Assert.Equal(36, (int)AuthoritativeProjectileBehavior.GuardianController);
        Assert.Equal(37, (int)AuthoritativeProjectileBehavior.Telekinesis);
        Assert.Equal(38, (int)AuthoritativeProjectileBehavior.TownPortal);
        Assert.Equal(44, (int)AuthoritativeProjectileBehavior.Jester);
        Assert.Equal(48, (int)AuthoritativeProjectileBehavior.Golem);
        Assert.Equal(49, (int)AuthoritativeProjectileBehavior.Etherealize);
    }

    [Fact]
    public void ShippedChargedBoltCarriesNativeStaffLevel()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(30, out var chargedBolt));
        Assert.Equal(1, chargedBolt.StaffLevel);
    }

    [Fact]
    public void ShippedTelekinesisCarriesNativeTargetInteractionTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(33, out var telekinesis));
        Assert.Equal(33U, telekinesis.NativeSpellId);
        Assert.Equal(15, telekinesis.ManaCost);
        Assert.Equal(2, telekinesis.ManaAdjustment);
        Assert.Equal(8, telekinesis.MinimumManaCost);
        Assert.Equal(2, telekinesis.StaffLevel);
        Assert.Equal(AuthoritativeProjectileBehavior.Telekinesis, telekinesis.ProjectileBehavior);
    }

    [Fact]
    public void ShippedTownPortalCarriesNativePortalTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(39, out var townPortal));
        Assert.Equal(7U, townPortal.NativeSpellId);
        Assert.Equal(35, townPortal.ManaCost);
        Assert.Equal(3, townPortal.ManaAdjustment);
        Assert.Equal(18, townPortal.MinimumManaCost);
        Assert.Equal(3, townPortal.StaffLevel);
        Assert.Equal(AuthoritativeProjectileBehavior.TownPortal, townPortal.ProjectileBehavior);
    }

    [Fact]
    public void ShippedJesterCarriesNativeDispatchIdentity()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(47, out var jester));
        Assert.Equal(39U, jester.NativeSpellId);
        Assert.Equal(AuthoritativeProjectileBehavior.Jester, jester.ProjectileBehavior);
        Assert.Equal((20, 3, 1, 15, 64, 10), (
            jester.ManaCost,
            jester.ManaAdjustment,
            jester.ManaAdjustmentDivisor,
            jester.MinimumManaCost,
            jester.ManaCostScale,
            jester.StaffLevel));
    }

    [Fact]
    public void ShippedNovaCarriesNativeRadialLightningTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(18, out var nova));
        Assert.Equal(18U, nova.NativeSpellId);
        Assert.Equal(AuthoritativeProjectileBehavior.NovaController, nova.ProjectileBehavior);
        Assert.Equal((60, 16, 255, 36, 4, 1, 64), (
            nova.ManaCost,
            nova.ProjectileSpeed,
            nova.ProjectileLifetime,
            nova.NovaProjectileCount,
            nova.NovaSearchRadius,
            nova.NovaControllerLifetime,
            nova.DamageScale));
    }

    [Fact]
    public void ShippedGolemCarriesNativeSummonIdentity()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(52, out var golem));
        Assert.Equal(21U, golem.NativeSpellId);
        Assert.Equal(AuthoritativeProjectileBehavior.Golem, golem.ProjectileBehavior);
        Assert.Equal((100, 6, 60, 9), (
            golem.ManaCost,
            golem.ManaAdjustment,
            golem.MinimumManaCost,
            golem.StaffLevel));
        Assert.Equal(12, golem.GolemDeathAnimationTicks);
    }

    [Fact]
    public void ShippedEtherealizeCarriesNativeLevelScopedStatusIdentity()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(53, out var etherealize));
        Assert.Equal(26U, etherealize.NativeSpellId);
        Assert.Equal(AuthoritativeProjectileBehavior.Etherealize, etherealize.ProjectileBehavior);
        Assert.Equal(53U, etherealize.StatusEffectId);
        Assert.Equal(uint.MaxValue, etherealize.StatusDuration);
        Assert.Equal(-1, etherealize.StaffLevel);
    }

    [Fact]
    public void StableSpellIdsResolveNativeSourceIdsExplicitly()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGetByNativeId(13, out var guardian));
        Assert.Equal(7U, guardian.SpellId);
        Assert.True(catalog.TryGetByNativeId(31, out var holyBolt));
        Assert.Equal(5U, holyBolt.SpellId);
        Assert.True(catalog.TryGetByNativeId(36, out var boneSpirit));
        Assert.Equal(38U, boneSpirit.SpellId);
    }

    [Fact]
    public void ShippedRuneMiscIdentityResolvesToStableStoneCurse()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGetByConsumableMiscId(51, out var stoneCurse));
        Assert.Equal(8U, stoneCurse.SpellId);
        Assert.Equal(AuthoritativeProjectileBehavior.StoneCurse, stoneCurse.ProjectileBehavior);
    }

    [Fact]
    public void DuplicateConsumableMiscIdsAreRejected()
    {
        var definitions = new[] {
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0) { ConsumableMiscId = 51 },
            new AuthoritativeSpellDefinition(2, 1, 0, 0, 0, 0) { ConsumableMiscId = 51 },
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => new AuthoritativeSpellCatalog(definitions));

        Assert.Contains("Consumable miscellaneous ID 51", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidConsumableMiscIdsAreRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0) { ConsumableMiscId = 46 },
        ]));

        Assert.Contains("invalid consumable miscellaneous ID 46", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateNativeSpellIdsAreRejected()
    {
        var definitions = new[] {
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0) { NativeSpellId = 13 },
            new AuthoritativeSpellDefinition(2, 1, 0, 0, 0, 0) { NativeSpellId = 13 },
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => new AuthoritativeSpellCatalog(definitions));

        Assert.Contains("Native spell ID 13", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedFireboltCarriesNativeManaDamageAndProjectileTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(1, out var firebolt));
        Assert.Equal(AuthoritativeProjectileBehavior.Firebolt, firebolt.ProjectileBehavior);
        Assert.Equal((6, 1, 2, 3, 64), (
            firebolt.ManaCost,
            firebolt.ManaAdjustment,
            firebolt.ManaAdjustmentDivisor,
            firebolt.MinimumManaCost,
            firebolt.ManaCostScale));
        Assert.Equal((10, 8, 1, 1), (
            firebolt.RandomDamageRange,
            firebolt.MagicDamageDivisor,
            firebolt.SpellLevelDamage,
            firebolt.DamageBonus));
        Assert.Equal((26, 256, 4), (
            firebolt.ProjectileSpeed,
            firebolt.ProjectileLifetime,
            firebolt.ResistantDamageDivisor));
    }

    [Fact]
    public void ShippedHolyBoltCarriesNativeUndeadProjectileTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(5, out var holyBolt));
        Assert.Equal(AuthoritativeProjectileBehavior.HolyBolt, holyBolt.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Physical, holyBolt.DamageType);
        Assert.Equal((7, 10, 9, 1), (
            holyBolt.ManaCost,
            holyBolt.RandomDamageRange,
            holyBolt.DamageBonus,
            holyBolt.CharacterLevelDamageDivisor));
        Assert.Equal((16, 2, 47, 256), (
            holyBolt.ProjectileSpeed,
            holyBolt.ProjectileSpeedSpellLevelStep,
            holyBolt.ProjectileSpeedMaximumBonus,
            holyBolt.ProjectileLifetime));
    }

    [Fact]
    public void ShippedBloodStarCarriesNativeMagicProjectileAndSelfDamageTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(36, out var bloodStar));
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, bloodStar.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Magic, bloodStar.DamageType);
        Assert.Equal((25, 2, 1, 14, 64), (
            bloodStar.ManaCost,
            bloodStar.ManaAdjustment,
            bloodStar.ManaAdjustmentDivisor,
            bloodStar.MinimumManaCost,
            bloodStar.ManaCostScale));
        Assert.Equal((16, 256, 112, 4), (
            bloodStar.ProjectileSpeed,
            bloodStar.ProjectileLifetime,
            bloodStar.Range,
            bloodStar.ResistantDamageDivisor));
        Assert.Equal((2, 8, 3, 5), (
            bloodStar.MagicDamageDivisor,
            bloodStar.MagicDamageReductionDivisor,
            bloodStar.SpellLevelDamage,
            bloodStar.SelfDamage));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            bloodStar with { SelfDamage = 0 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            bloodStar with { MagicDamageReductionDivisor = 4 },
        ]));
    }

    [Fact]
    public void ShippedBoneSpiritCarriesNativeHomingProjectileAndSelfDamageTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));
        Assert.True(catalog.TryGet(38, out var boneSpirit));

        Assert.Equal(AuthoritativeProjectileBehavior.BoneSpirit, boneSpirit.ProjectileBehavior);
        Assert.Equal(16, boneSpirit.ProjectileSpeed);
        Assert.Equal(256, boneSpirit.ProjectileLifetime);
        Assert.Equal(7, boneSpirit.ProjectileImpactLifetime);
        Assert.Equal(AuthoritativeDamageType.Magic, boneSpirit.DamageType);
        Assert.Equal(4, boneSpirit.ResistantDamageDivisor);
        Assert.Equal(6, boneSpirit.SelfDamage);
    }

    [Fact]
    public void ShippedElementalCarriesNativeRetargetAndBurstTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));
        Assert.True(catalog.TryGet(31, out var elemental));

        Assert.Equal(AuthoritativeProjectileBehavior.Elemental, elemental.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Fire, elemental.DamageType);
        Assert.Equal((35, 2, 20, 64), (
            elemental.ManaCost,
            elemental.ManaAdjustment,
            elemental.MinimumManaCost,
            elemental.ManaCostScale));
        Assert.Equal((16, 256, 14, 1), (
            elemental.ProjectileSpeed,
            elemental.ProjectileLifetime,
            elemental.ProjectileImpactLifetime,
            elemental.AreaRadius));
        Assert.Equal((10, 2, 4, 2, 8, 255, 2), (
            elemental.RandomDamageRange,
            elemental.RandomDamageIterationsBonus,
            elemental.DamageAmount,
            elemental.MagicDamageDivisor,
            elemental.SpellEffectScaleDivisor,
            elemental.ElementalRetargetLifetime,
            elemental.ElementalCharacterLevelDamageMultiplier));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            elemental with { ElementalRetargetLifetime = 254 },
        ]));
    }

    [Fact]
    public void ShippedGuardianCarriesNativeTurretAndFireboltTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));
        Assert.True(catalog.TryGet(7, out var guardian));

        Assert.Equal(AuthoritativeProjectileBehavior.GuardianController, guardian.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Fire, guardian.DamageType);
        Assert.Equal((50, 30, 2, 30, 64), (
            guardian.ManaCost,
            guardian.MinimumManaCost,
            guardian.ManaAdjustment,
            guardian.GuardianMinimumDuration,
            guardian.ManaCostScale));
        Assert.Equal((16, 16, 30, 2, 5, 6, 16), (
            guardian.ProjectileSpeed,
            guardian.GuardianDurationTickScale,
            guardian.GuardianMaximumDuration,
            guardian.GuardianCharacterLevelDurationDivisor,
            guardian.GuardianSpawnSearchRadius,
            guardian.GuardianTargetSearchRadius,
            guardian.GuardianFireInterval));
        Assert.Equal((2, 47, 10, 8, 1, 1, 4), (
            guardian.GuardianProjectileSpeedLevelStep,
            guardian.GuardianProjectileSpeedMaximumBonus,
            guardian.RandomDamageRange,
            guardian.MagicDamageDivisor,
            guardian.SpellLevelDamage,
            guardian.DamageBonus,
            guardian.ResistantDamageDivisor));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            guardian with { GuardianFireInterval = 8 },
        ]));
    }

    [Fact]
    public void ShippedFireballCarriesNativeDamageSpeedAndExplosionTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(12, out var fireball));
        Assert.Equal(AuthoritativeProjectileBehavior.Fireball, fireball.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Fire, fireball.DamageType);
        Assert.Equal((16, 1, 10, 64), (
            fireball.ManaCost,
            fireball.ManaAdjustment,
            fireball.MinimumManaCost,
            fireball.ManaCostScale));
        Assert.Equal((10, 2, 1, 2, 4, 8), (
            fireball.RandomDamageRange,
            fireball.RandomDamageIterationsBonus,
            fireball.CharacterLevelDamageDivisor,
            fireball.DamageScale,
            fireball.DamageBonus,
            fireball.SpellEffectScaleDivisor));
        Assert.Equal((16, 2, 34, 256, 1, 14, 4), (
            fireball.ProjectileSpeed,
            fireball.ProjectileSpeedSpellLevelStep,
            fireball.ProjectileSpeedMaximumBonus,
            fireball.ProjectileLifetime,
            fireball.AreaRadius,
            fireball.ProjectileImpactLifetime,
            fireball.ResistantDamageDivisor));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                fireball with { ProjectileSpeedMaximumBonus = 1 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                fireball with { AreaRadius = 0 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                fireball with { RandomDamageIterationsBonus = 65 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                fireball with { ProjectileSpeed = 500 },
            ]));
    }

    [Fact]
    public void ShippedLightningCarriesNativeControllerAndSegmentTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(3, out var lightning));
        Assert.Equal(AuthoritativeProjectileBehavior.Lightning, lightning.ProjectileBehavior);
        Assert.Equal((10, 1, 6, 64), (
            lightning.ManaCost,
            lightning.ManaAdjustment,
            lightning.MinimumManaCost,
            lightning.ManaCostScale));
        Assert.Equal((2, 1, 2, 64), (
            lightning.RandomDamageRange,
            lightning.CharacterLevelRandomDamageDivisor,
            lightning.DamageBonus,
            lightning.DamageScale));
        Assert.Equal((32, 256, 6, 2, 4), (
            lightning.ProjectileSpeed,
            lightning.ProjectileLifetime,
            lightning.SecondaryProjectileLifetime,
            lightning.SecondaryProjectileLifetimeSpellLevelDivisor,
            lightning.ResistantDamageDivisor));
    }

    [Fact]
    public void ShippedFlashCarriesNativeLayerDamageAndManaTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(4, out var flash));
        Assert.Equal(AuthoritativeProjectileBehavior.Flash, flash.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Magic, flash.DamageType);
        Assert.Equal((30, 2, 16, 64), (
            flash.ManaCost,
            flash.ManaAdjustment,
            flash.MinimumManaCost,
            flash.ManaCostScale));
        Assert.Equal((0, 19, 2, 1, 4), (
            flash.ProjectileSpeed,
            flash.ProjectileLifetime,
            flash.ProjectileCount,
            flash.AreaRadius,
            flash.ResistantDamageDivisor));
        Assert.Equal((20, 1, 1, 1, 1, 8, 50), (
            flash.RandomDamageRange,
            flash.DamageBonus,
            flash.RandomDamageIterationsCharacterLevelDivisor,
            flash.RandomDamageIterationsBonus,
            flash.CharacterLevelDamageDivisor,
            flash.SpellEffectScaleDivisor,
            flash.PostScaleDamagePercent));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([flash with { SpellEffectScaleDivisor = 0 }]));
    }

    [Fact]
    public void ShippedHealingCarriesNativeRandomHealingAndManaTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(2, out var healing));
        Assert.Equal(AuthoritativeProjectileBehavior.Healing, healing.ProjectileBehavior);
        Assert.Equal((5, 3, 1, 2, 1, 64), (
            healing.ManaCost,
            healing.ManaAdjustment,
            healing.ManaAdjustmentDivisor,
            healing.CharacterLevelManaCost,
            healing.MinimumManaCost,
            healing.ManaCostScale));
        Assert.Equal((1, 10, 4, 1, 6, 1, 64), (
            healing.HealingAmount,
            healing.RandomHealingRange,
            healing.CharacterLevelRandomHealingRange,
            healing.CharacterLevelHealing,
            healing.SpellLevelRandomHealingRange,
            healing.SpellLevelHealing,
            healing.HealingScale));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([healing with { RandomHealingRange = 0 }]));
    }

    [Fact]
    public void ShippedHealOtherCarriesNativePlayerTargetHealingAndManaTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(34, out var healOther));
        Assert.Equal(AuthoritativeProjectileBehavior.HealOther, healOther.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Magic, healOther.DamageType);
        Assert.Equal((5, 3, 1, 2, 1, 64), (
            healOther.ManaCost,
            healOther.ManaAdjustment,
            healOther.ManaAdjustmentDivisor,
            healOther.CharacterLevelManaCost,
            healOther.MinimumManaCost,
            healOther.ManaCostScale));
        Assert.Equal((112, 0, 1, 1), (
            healOther.Range,
            healOther.ProjectileSpeed,
            healOther.ProjectileLifetime,
            healOther.ProjectileCount));
        Assert.Equal((1, 10, 4, 1, 6, 1, 64), (
            healOther.HealingAmount,
            healOther.RandomHealingRange,
            healOther.CharacterLevelRandomHealingRange,
            healOther.CharacterLevelHealing,
            healOther.SpellLevelRandomHealingRange,
            healOther.SpellLevelHealing,
            healOther.HealingScale));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([healOther with { Range = 0 }]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([healOther with { RandomHealingRange = 0 }]));
    }

    [Fact]
    public void ShippedTeleportCarriesNativeMovementAndManaTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(23, out var teleport));
        Assert.Equal(AuthoritativeProjectileBehavior.Teleport, teleport.ProjectileBehavior);
        Assert.Equal((35, 3, 15, 64), (
            teleport.ManaCost,
            teleport.ManaAdjustment,
            teleport.MinimumManaCost,
            teleport.ManaCostScale));
        Assert.Equal((0, 2, 1, 0), (
            teleport.ProjectileSpeed,
            teleport.ProjectileLifetime,
            teleport.ProjectileCount,
            teleport.AreaRadius));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([teleport with { ProjectileLifetime = 3 }]));
    }

    [Fact]
    public void ShippedResurrectionCarriesNativePlayerRevivalAndManaTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            path,
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(32, out var resurrection));
        Assert.Equal(
            AuthoritativeProjectileBehavior.Resurrection,
            resurrection.ProjectileBehavior);
        Assert.Equal((20, 2, 20, 64), (
            resurrection.ManaCost,
            resurrection.ManaAdjustment,
            resurrection.MinimumManaCost,
            resurrection.ManaCostScale));
        Assert.Equal((112, 0, 16, 1), (
            resurrection.Range,
            resurrection.ProjectileSpeed,
            resurrection.ProjectileLifetime,
            resurrection.ProjectileCount));
        Assert.Equal((10, 64, 50), (
            resurrection.ResurrectionLife,
            resurrection.ResurrectionLifeScale,
            resurrection.ResurrectionSearchRadius));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                resurrection with { ResurrectionSearchRadius = 0 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                resurrection with {
                    ProjectileBehavior = AuthoritativeProjectileBehavior.ResurrectionBeam,
                },
            ]));
    }

    [Fact]
    public void ShippedManaShieldCarriesNativePersistentDefenseTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(11, out var manaShield));
        Assert.Equal(
            AuthoritativeProjectileBehavior.ManaShield,
            manaShield.ProjectileBehavior);
        Assert.Equal((33, 33, 64), (
            manaShield.ManaCost,
            manaShield.MinimumManaCost,
            manaShield.ManaCostScale));
        Assert.Equal((24, 3, 7), (
            manaShield.ManaShieldBaseDamageReductionDivisor,
            manaShield.ManaShieldDamageReductionLevelStep,
            manaShield.ManaShieldDamageReductionMaximumLevel));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                manaShield with { ManaShieldBaseDamageReductionDivisor = 22 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                manaShield with {
                    ManaShieldBaseDamageReductionDivisor = int.MaxValue,
                    ManaShieldDamageReductionLevelStep = int.MaxValue,
                    ManaShieldDamageReductionMaximumLevel = int.MaxValue,
                },
            ]));
    }

    [Fact]
    public void ShippedStoneCurseCarriesNativeDurationAndSearchTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(8, out var stoneCurse));
        Assert.Equal(
            AuthoritativeProjectileBehavior.StoneCurse,
            stoneCurse.ProjectileBehavior);
        Assert.Equal((60, 3, 40, 64), (
            stoneCurse.ManaCost,
            stoneCurse.ManaAdjustment,
            stoneCurse.MinimumManaCost,
            stoneCurse.ManaCostScale));
        Assert.Equal((6, 15, 16, 5, 11), (
            stoneCurse.StoneCurseBaseDuration,
            stoneCurse.StoneCurseMaximumDuration,
            stoneCurse.StoneCurseDurationScale,
            stoneCurse.StoneCurseSearchRadius,
            stoneCurse.ProjectileImpactLifetime));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                stoneCurse with { StoneCurseSearchRadius = 0 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                stoneCurse with {
                    StoneCurseMaximumDuration = int.MaxValue,
                    StoneCurseDurationScale = int.MaxValue,
                },
            ]));
    }

    [Fact]
    public void ShippedInfravisionCarriesNativeStatusDurationAndManaTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(9, out var infravision));
        Assert.Equal(
            AuthoritativeProjectileBehavior.Infravision,
            infravision.ProjectileBehavior);
        Assert.Equal((40, 5, 20, 64), (
            infravision.ManaCost,
            infravision.ManaAdjustment,
            infravision.MinimumManaCost,
            infravision.ManaCostScale));
        Assert.Equal((9U, 1584U, 0, 8), (
            infravision.StatusEffectId,
            infravision.StatusDuration,
            infravision.StatusMagnitude,
            infravision.InfravisionDurationGrowthDivisor));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                infravision with { InfravisionDurationGrowthDivisor = 1 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                infravision with {
                    ProjectileBehavior = AuthoritativeProjectileBehavior.Targeted,
                },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                infravision,
                infravision with { SpellId = 10, StatusEffectId = 10 },
            ]));
    }

    [Fact]
    public void ShippedSearchCarriesNativeAdditiveStatusDurationTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(46, out var search));
        Assert.Equal(AuthoritativeProjectileBehavior.Search, search.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Magic, search.DamageType);
        Assert.Equal((15, 1, 1, 64), (
            search.ManaCost,
            search.ManaAdjustment,
            search.MinimumManaCost,
            search.ManaCostScale));
        Assert.Equal((46U, 245U, 0, 2, 10), (
            search.StatusEffectId,
            search.StatusDuration,
            search.StatusMagnitude,
            search.SearchCharacterLevelDurationMultiplier,
            search.SearchSpellLevelDurationMultiplier));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                search with { SearchCharacterLevelDurationMultiplier = 0 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                search with {
                    ProjectileBehavior = AuthoritativeProjectileBehavior.Targeted,
                },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                search,
                search with { SpellId = 47, StatusEffectId = 47 },
            ]));
    }

    [Fact]
    public void ShippedPhasingCarriesNativeRandomMovementAndManaTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(path, File.ReadAllText(path));

        Assert.True(catalog.TryGet(10, out var phasing));
        Assert.Equal(AuthoritativeProjectileBehavior.Phasing, phasing.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Magic, phasing.DamageType);
        Assert.Equal((12, 2, 4, 64), (
            phasing.ManaCost,
            phasing.ManaAdjustment,
            phasing.MinimumManaCost,
            phasing.ManaCostScale));
        Assert.Equal((1, 0, 2, 1, 0, 4, 6), (
            phasing.Range,
            phasing.ProjectileSpeed,
            phasing.ProjectileLifetime,
            phasing.ProjectileCount,
            phasing.ProjectileCountLevelDivisor,
            phasing.PhasingMinimumAxisOffset,
            phasing.PhasingMaximumAxisOffset));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                phasing with { PhasingMinimumAxisOffset = 0 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                phasing with { PhasingMaximumAxisOffset = 3 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                phasing with { PhasingMaximumAxisOffset = 33 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                phasing with { StatusDuration = 1 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                phasing with {
                    ProjectileBehavior = AuthoritativeProjectileBehavior.Targeted,
                },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                phasing,
                phasing with { SpellId = 12 },
            ]));
    }

    [Fact]
    public void LoadsDeclarativeHealingAndStatusTuning()
    {
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\thealing_amount\tstatus_effect_id\tstatus_duration\tstatus_magnitude\n"
            + "7\t4\t12\t3\t8\t2\n");

        Assert.True(catalog.TryGet(7, out var spell));
        Assert.Equal(4, spell.ManaCost);
        Assert.Equal(12, spell.HealingAmount);
        Assert.Equal(3U, spell.StatusEffectId);
        Assert.Equal(8U, spell.StatusDuration);

        var damageCatalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\thealing_amount\tdamage_amount\trange\tstatus_effect_id\tstatus_duration\tstatus_magnitude\n"
                + "8\t4\t0\t12\t2\t0\t0\t0\n");
        Assert.True(damageCatalog.TryGet(8, out var damageSpell));
        Assert.Equal(12, damageSpell.DamageAmount);
        Assert.Equal(2, damageSpell.Range);

        var areaCatalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\tdamage_amount\trange\tarea_radius\tdamage_type\n"
                + "9\t6\t10\t4\t1\tFire\n");
        Assert.True(areaCatalog.TryGet(9, out var areaSpell));
        Assert.Equal(1, areaSpell.AreaRadius);
        Assert.Equal(AuthoritativeDamageType.Fire, areaSpell.DamageType);
    }

    [Fact]
    public void RejectsDuplicateOrInvalidSpellDefinitions()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0),
            new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 0, 0, 0, 0, 0),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0) { DamageAmount = 10, Range = 0 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Firebolt,
                ProjectileSpeed = 26,
                RandomDamageRange = 10,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(3, 10, 0, 0, 0, 0) {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Lightning,
                ProjectileSpeed = 32,
                RandomDamageRange = 2,
                DamageBonus = 2,
                CharacterLevelRandomDamageDivisor = 1,
                DamageScale = 64,
            },
        ]));
    }

    [Fact]
    public void LoadsProjectileTuningAndRejectsProjectileHealing()
    {
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "projectiles.tsv",
            "spell_id\tmana_cost\tdamage_amount\tprojectile_speed\tprojectile_lifetime\tprojectile_count\n"
                + "7\t4\t12\t2\t8\t4");
        var spell = Assert.Single(catalog.Definitions);
        Assert.Equal(2, spell.ProjectileSpeed);
        Assert.Equal(8, spell.ProjectileLifetime);
        Assert.Equal(4, spell.ProjectileCount);

        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(8, 3, 10, 0, 0, 0) { ProjectileSpeed = 1 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(9, 3, 0, 0, 0, 0) {
                DamageAmount = 4,
                ProjectileCount = 2,
            },
        ]));
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
