using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeSpellCatalogTests
{
    [Fact]
    public void ResolvesSymbolicSpellIdsThroughStableContentIds()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "fireball", 12),
        ]);
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tspell_symbol\tmana_cost\n"
                + "12\tfireball\t16\n",
            stableIds);

        Assert.True(catalog.TryGet(12, out _));
    }

    [Fact]
    public void ResolvesSymbolicGolemMonsterIdentityThroughStableContentIds()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "golem", 52),
            new StableContentId("monster", "golem_native_109", 111),
        ]);
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tspell_symbol\tmana_cost\trange\tdamage_type\tprojectile_behavior\tprojectile_lifetime\tprojectile_count\tstaff_level\tgolem_death_animation_ticks\tgolem_hit_points_level_base_q6\tgolem_hit_points_level_multiplier\tgolem_hit_points_mana_divisor\tgolem_damage_level_multiplier\tgolem_minimum_damage_level_offset\tgolem_maximum_damage_level_offset\tgolem_armor_class\tgolem_monster_id\tgolem_monster_symbol\n"
                + "52\tgolem\t100\t112\tFire\tGolem\t1\t1\t9\t12\t320\t2\t3\t2\t4\t8\t25\t0\tgolem_native_109\n",
            stableIds);

        Assert.Equal(111U, Assert.Single(catalog.Definitions).GolemMonsterId);
    }

    [Fact]
    public void RejectsMismatchedNumericAndSymbolicGolemMonsterIdentity()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "golem", 52),
            new StableContentId("monster", "golem_native_109", 111),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv(
                "spells.tsv",
                "spell_id\tspell_symbol\tmana_cost\trange\tdamage_type\tprojectile_behavior\tprojectile_lifetime\tprojectile_count\tstaff_level\tgolem_death_animation_ticks\tgolem_hit_points_level_base_q6\tgolem_hit_points_level_multiplier\tgolem_hit_points_mana_divisor\tgolem_damage_level_multiplier\tgolem_minimum_damage_level_offset\tgolem_maximum_damage_level_offset\tgolem_armor_class\tgolem_monster_id\tgolem_monster_symbol\n"
                    + "52\tgolem\t100\t112\tFire\tGolem\t1\t1\t9\t12\t320\t2\t3\t2\t4\t8\t25\t109\tgolem_native_109\n",
                stableIds));

        Assert.Contains("maps monster 109 to symbol 'golem_native_109'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsOptionalSpellDisplayNameMetadata()
    {
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\tdisplay_name\n"
                + "1\t6\tFirebolt\n");

        Assert.Equal("Firebolt", Assert.Single(catalog.Definitions).DisplayName);
    }

    [Fact]
    public void ShippedDirectSpellsCarryValidatedDisplayNames()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(path));

        Assert.NotEmpty(catalog.Definitions);
        Assert.All(catalog.Definitions, spell =>
            Assert.False(string.IsNullOrWhiteSpace(spell.DisplayName)));
    }

    [Fact]
    public void ShippedDirectSpellsCarryExternalizedNativePatterns()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(path));

        catalog.ValidateExternalizedNativePatterns();
    }

    [Fact]
    public void ProductionValidationRejectsMissingNativePatterns()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var shipped = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(path));
        var incomplete = new AuthoritativeSpellCatalog(shipped.Definitions.Select(
            spell => spell.SpellId == 12
                ? spell with { FireballExplosionOffsets = [] }
                : spell));

        Assert.Throws<InvalidDataException>(incomplete.ValidateExternalizedNativePatterns);
    }

    [Fact]
    public void RejectsSymbolicSpellIdNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "fireball", 12),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tspell_symbol\tmana_cost\n"
                + "3\tfireball\t16\n",
            stableIds));
    }

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
        Assert.Equal(58, (int)AuthoritativeProjectileBehavior.WeaponArrow);
        Assert.Equal(59, (int)AuthoritativeProjectileBehavior.MonsterArrow);
    }

    [Fact]
    public void ValidatesStableSpellReferences()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0),
        ]);
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "firebolt", 1),
        ]);

        spells.ValidateStableReferences(stableIds);
    }

    [Fact]
    public void AcceptsStableReferenceForAnActiveClassAbility()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0),
        ]);
        var stableIds = new StableContentIdCatalog([
            new StableContentId("ability", "repair", 26),
        ]);

        spells.ValidateStableReferences(stableIds, [26]);
    }

    [Fact]
    public void LegacySpellNamespaceStillValidatesAnActiveClassAbility()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0),
        ]);
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "repair", 26),
        ]);

        spells.ValidateStableReferences(stableIds, [26]);
    }

    [Fact]
    public void RejectsUnknownStableSpellReferences()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0),
        ]);
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "missing", 9999),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            spells.ValidateStableReferences(stableIds));
        Assert.Contains("symbol 'missing' references unknown spell 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsExplicitConsumableBehavior()
    {
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\thealing_amount\tdamage_amount\tconsumable_behavior\n"
            + "12\t16\t0\t0\tRuneFire\n");

        var spell = Assert.Single(catalog.Definitions);
        Assert.Equal(AuthoritativeConsumableBehavior.RuneFire, spell.ConsumableBehavior);
    }

    [Fact]
    public void TranslatesLegacyRuneFireMiscIdentityAtCatalogBoundary()
    {
        var catalog = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(12, 16, 0, 0, 0, 0) {
                ConsumableMiscId = AuthoritativeSpellCatalog.LegacyRuneFireConsumableMiscId,
            },
        ]);

        Assert.Equal(
            AuthoritativeConsumableBehavior.RuneFire,
            Assert.Single(catalog.Definitions).ConsumableBehavior);
    }

    [Fact]
    public void RejectsUnknownConsumableBehavior()
    {
        var exception = Assert.Throws<InvalidDataException>(() => AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\thealing_amount\tdamage_amount\tconsumable_behavior\n"
            + "12\t16\t0\t0\tUnknown\n"));

        Assert.Contains("unknown consumable behavior", exception.Message, StringComparison.Ordinal);
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(39, out var townPortal));
        Assert.Equal(7U, townPortal.NativeSpellId);
        Assert.Equal(35, townPortal.ManaCost);
        Assert.Equal(3, townPortal.ManaAdjustment);
        Assert.Equal(18, townPortal.MinimumManaCost);
        Assert.Equal(3, townPortal.StaffLevel);
        Assert.Equal(AuthoritativeProjectileBehavior.TownPortal, townPortal.ProjectileBehavior);
        Assert.Equal(5, townPortal.TownPortalSearchRadius);
        Assert.Equal(100, townPortal.TownPortalLifetime);
        Assert.Equal(57, townPortal.TownPortalDestinationPositionX);
        Assert.Equal(40, townPortal.TownPortalDestinationPositionY);
        Assert.Equal(4, townPortal.TownPortalDestinationSlotCount);
        Assert.Equal(2, townPortal.TownPortalDestinationSlotSpacing);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([townPortal with { TownPortalSearchRadius = 0 }]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([townPortal with { TownPortalLifetime = 0 }]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([townPortal with { TownPortalDestinationSlotCount = 0 }]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([townPortal with { TownPortalDestinationSlotSpacing = -1 }]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(1, 5, 20, 0, 0, 0) {
                    TownPortalDestinationPositionX = 58,
                },
            ]));
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
        var stableIdsPath = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "content_ids.tsv");
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            File.ReadAllText(stableIdsPath));
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(path),
            stableIds);

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
        Assert.Equal(
            [1U, 1U, 12U, 6U, 7U, 16U, 39U, 23U, 25U, 8U],
            jester.JesterSpellIds);
    }

    [Fact]
    public void JesterDispatchRejectsNumericAndSymbolicMismatches()
    {
        var repositoryRoot = FindRepositoryRoot();
        var spellPath = Path.Combine(repositoryRoot, "server", "content", "base", "spells.tsv");
        var stableIdsPath = Path.Combine(repositoryRoot, "server", "content", "base", "content_ids.tsv");
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            File.ReadAllText(stableIdsPath));
        var contents = File.ReadAllText(spellPath).Replace(
            "firebolt,firebolt,fireball,fire_wall,guardian,chain_lightning,town_portal,teleport,apocalypse,stone_curse",
            "firebolt,firebolt,fireball,fire_wall,guardian,chain_lightning,town_portal,teleport,apocalypse,fireball",
            StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv("spells.tsv", contents, stableIds));
        Assert.Contains("mismatched Jester spell IDs and symbols", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JesterDispatchRejectsCyclesBeforeActivation()
    {
        var first = new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0) {
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Jester,
            ProjectileLifetime = 1,
            JesterSpellIds = Enumerable.Repeat(2U, 10).ToArray(),
        };
        var second = first with {
            SpellId = 2,
            JesterSpellIds = Enumerable.Repeat(1U, 10).ToArray(),
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([first, second]));

        Assert.Contains("Jester dispatch graph contains a cycle", exception.Message, StringComparison.Ordinal);
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(52, out var golem));
        Assert.Equal(21U, golem.NativeSpellId);
        Assert.Equal(AuthoritativeProjectileBehavior.Golem, golem.ProjectileBehavior);
        Assert.Equal((100, 6, 60, 9), (
            golem.ManaCost,
            golem.ManaAdjustment,
            golem.MinimumManaCost,
            golem.StaffLevel));
        Assert.Equal(12, golem.GolemDeathAnimationTicks);
        Assert.Equal((320, 2, 3, 2, 4, 8, 25), (
            golem.GolemHitPointsLevelBaseQ6,
            golem.GolemHitPointsLevelMultiplier,
            golem.GolemHitPointsManaDivisor,
            golem.GolemDamageLevelMultiplier,
            golem.GolemMinimumDamageLevelOffset,
            golem.GolemMaximumDamageLevelOffset,
            golem.GolemArmorClass));
        Assert.Equal(111U, golem.GolemMonsterId);
    }

    [Fact]
    public void GolemSummonIdentityMustResolveAgainstMonsterCatalog()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(52, 100, 0, 0, 0, 0) {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Golem,
                Range = 112,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileLifetime = 1,
                ProjectileCount = 1,
                StaffLevel = 9,
                GolemDeathAnimationTicks = 12,
                GolemMonsterId = 9,
            },
        ]);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2,
                2,
                "Configured Monster",
                1,
                16,
                1,
                1,
                2,
                1,
                1,
                2,
                0,
                0,
                false,
                false,
                false) {
                    NativeAi = "Zombie",
                },
        ]);

        Assert.Throws<InvalidDataException>(
            () => spells.ValidateMonsterReferences(monsterTypes));
    }

    [Fact]
    public void ShippedEtherealizeCarriesNativeSpellIdentity()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(53, out var etherealize));
        Assert.Equal(25U, etherealize.NativeSpellId);
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGetByNativeId(13, out var guardian));
        Assert.Equal(7U, guardian.SpellId);
        Assert.True(catalog.TryGetByNativeId(31, out var holyBolt));
        Assert.Equal(5U, holyBolt.SpellId);
        Assert.True(catalog.TryGetByNativeId(36, out var boneSpirit));
        Assert.Equal(38U, boneSpirit.SpellId);
    }

    [Fact]
    public void ShippedRuneMiscIdentitiesResolveToStableSpellRows()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        var expected = new Dictionary<int, (uint SpellId, AuthoritativeProjectileBehavior Behavior)> {
            [47] = (12, AuthoritativeProjectileBehavior.Fireball),
            [48] = (50, AuthoritativeProjectileBehavior.LightningWallController),
            [49] = (18, AuthoritativeProjectileBehavior.NovaController),
            [50] = (45, AuthoritativeProjectileBehavior.ImmolationController),
            [51] = (8, AuthoritativeProjectileBehavior.StoneCurse),
        };
        foreach (var (miscId, expectedSpell) in expected) {
            Assert.True(catalog.TryGetByConsumableMiscId(miscId, out var definition));
            Assert.Equal(expectedSpell.SpellId, definition.SpellId);
            Assert.Equal(expectedSpell.Behavior, definition.ProjectileBehavior);
        }
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
            "spells.tsv",
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(1, out var firebolt));
        Assert.Equal("Firebolt", firebolt.DisplayName);
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
        Assert.Equal((2, 5, 95), (
            firebolt.FireboltHitMonsterLevelScale,
            firebolt.FireboltMinimumHitChance,
            firebolt.FireboltMaximumHitChance));
        Assert.Equal((2, 5, 95), (
            firebolt.PersistentProjectileHitMonsterLevelScale,
            firebolt.PersistentProjectileMinimumHitChance,
            firebolt.PersistentProjectileMaximumHitChance));
        Assert.Equal((2, 5, 95), (
            firebolt.PersistentProjectileHitCharacterLevelScale,
            firebolt.PersistentProjectilePlayerMinimumHitChance,
            firebolt.PersistentProjectilePlayerMaximumHitChance));
        Assert.Equal(2, firebolt.PersistentProjectilePlayerDamageDivisor);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            firebolt with { FireboltMinimumHitChance = 96 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            firebolt with {
                FireboltMinimumHitChance = 80,
                FireboltMaximumHitChance = 79,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            firebolt with {
                PersistentProjectileMinimumHitChance = 80,
                PersistentProjectileMaximumHitChance = 79,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            firebolt with {
                PersistentProjectilePlayerMinimumHitChance = 80,
                PersistentProjectilePlayerMaximumHitChance = 79,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            firebolt with { PersistentProjectilePlayerDamageDivisor = 0 },
        ]));
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
        var stableIdsPath = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "content_ids.tsv");
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            File.ReadAllText(stableIdsPath));
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(path),
            stableIds);

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
        Assert.Equal((2, 5, 95), (
            holyBolt.HolyBoltHitMonsterLevelScale,
            holyBolt.HolyBoltMinimumHitChance,
            holyBolt.HolyBoltMaximumHitChance));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            holyBolt with { HolyBoltMinimumHitChance = 96 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            holyBolt with {
                HolyBoltMinimumHitChance = 80,
                HolyBoltMaximumHitChance = 79,
            },
        ]));
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));
        Assert.True(catalog.TryGet(38, out var boneSpirit));

        Assert.Equal(AuthoritativeProjectileBehavior.BoneSpirit, boneSpirit.ProjectileBehavior);
        Assert.Equal(16, boneSpirit.ProjectileSpeed);
        Assert.Equal(256, boneSpirit.ProjectileLifetime);
        Assert.Equal(7, boneSpirit.ProjectileImpactLifetime);
        Assert.Equal(AuthoritativeDamageType.Magic, boneSpirit.DamageType);
        Assert.Equal(4, boneSpirit.ResistantDamageDivisor);
        Assert.Equal(6, boneSpirit.SelfDamage);
        Assert.Equal((2, 5, 95, 3, 3), (
            boneSpirit.BoneSpiritHitMonsterLevelScale,
            boneSpirit.BoneSpiritMinimumHitChance,
            boneSpirit.BoneSpiritMaximumHitChance,
            boneSpirit.BoneSpiritDamageHitPointsDivisor,
            boneSpirit.BoneSpiritPlayerDamageHitPointsDivisor));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            boneSpirit with { BoneSpiritMinimumHitChance = 96 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            boneSpirit with { BoneSpiritDamageHitPointsDivisor = 0 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            boneSpirit with { BoneSpiritPlayerDamageHitPointsDivisor = 0 },
        ]));
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));
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
        Assert.Equal(
            [(0, 0), (-1, 1), (1, -1), (1, 1), (1, 0), (0, 1), (-1, -1), (-1, 0), (0, -1)],
            elemental.ElementalBurstOffsets);
        var malformed = File.ReadAllText(path).Replace(
            "0,0;-1,1;1,-1;1,1;1,0;0,1;-1,-1;-1,0;0,-1",
            "0,bad",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv("spells.tsv", malformed));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            elemental with { ElementalRetargetLifetime = 254 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            elemental with { ElementalBurstOffsets = [(0, 0)] },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            elemental with {
                ElementalBurstOffsets = [
                    (0, 0), (-1, 1), (1, -1), (1, 1), (1, 0),
                    (0, 1), (-1, -1), (-1, 0), (2, 0),
                ],
            },
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));
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
        Assert.Equal(
            [
                (6, 0), (5, 0), (4, 0), (3, 0), (2, 0), (1, 0),
                (6, 1), (5, 1), (4, 1), (3, 1), (6, 2), (2, 1),
                (5, 2), (6, 3), (4, 2), (5, 3), (3, 2), (1, 1),
                (6, 4), (6, 5), (5, 4), (4, 3), (2, 2), (5, 5),
                (4, 4), (3, 3), (6, 6), (5, 6), (4, 5), (3, 4),
                (2, 3), (4, 6), (3, 5), (2, 4), (1, 2), (3, 6),
                (2, 5), (1, 3), (0, 1), (2, 6), (1, 4), (1, 5),
                (1, 6), (0, 2), (0, 3), (0, 6), (0, 5), (0, 4),
            ],
            guardian.GuardianArcOffsets);
        var malformed = File.ReadAllText(path).Replace(
            "6,0;5,0;4,0;3,0;2,0;1,0;6,1;5,1;4,1;3,1;6,2;2,1;5,2;6,3;4,2;5,3;3,2;1,1;6,4;6,5;5,4;4,3;2,2;5,5;4,4;3,3;6,6;5,6;4,5;3,4;2,3;4,6;3,5;2,4;1,2;3,6;2,5;1,3;0,1;2,6;1,4;1,5;1,6;0,2;0,3;0,6;0,5;0,4",
            "0,bad",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv("spells.tsv", malformed));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            guardian with { GuardianFireInterval = 8 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            guardian with { GuardianArcOffsets = [(0, 0)] },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            guardian with {
                GuardianArcOffsets = [
                    (6, 0), (5, 0), (4, 0), (3, 0), (2, 0), (1, 0),
                    (6, 1), (5, 1), (4, 1), (3, 1), (6, 2), (2, 1),
                    (5, 2), (6, 3), (4, 2), (5, 3), (3, 2), (1, 1),
                    (6, 4), (6, 5), (5, 4), (4, 3), (2, 2), (5, 5),
                    (4, 4), (3, 3), (6, 6), (5, 6), (4, 5), (3, 4),
                    (2, 3), (4, 6), (3, 5), (2, 4), (1, 2), (3, 6),
                    (2, 5), (1, 3), (0, 1), (2, 6), (1, 4), (1, 5),
                    (1, 6), (0, 2), (0, 3), (0, 6), (0, 5), (7, 0),
                ],
            },
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
            "spells.tsv",
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
        Assert.Equal(
            [(0, 0), (0, 1), (0, -1), (1, 0), (1, -1), (1, 1), (-1, 0), (-1, 1), (-1, -1)],
            fireball.FireballExplosionOffsets);
        var malformed = File.ReadAllText(path).Replace(
            "0,0;0,1;0,-1;1,0;1,-1;1,1;-1,0;-1,1;-1,-1",
            "0,bad",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv("spells.tsv", malformed));
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
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                fireball with { FireballExplosionOffsets = [(0, 0)] },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                fireball with {
                    FireballExplosionOffsets = [
                        (0, 0), (0, 1), (0, -1), (1, 0), (1, -1),
                        (1, 1), (-1, 0), (-1, 1), (2, 0),
                    ],
                },
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
            "spells.tsv",
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
            "spells.tsv",
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
        Assert.Equal(2, flash.FlashMonsterDamageLevelScale);
        Assert.Equal((20, 1, 1, 1, 1, 8, 50), (
            flash.RandomDamageRange,
            flash.DamageBonus,
            flash.RandomDamageIterationsCharacterLevelDivisor,
            flash.RandomDamageIterationsBonus,
            flash.CharacterLevelDamageDivisor,
            flash.SpellEffectScaleDivisor,
            flash.PostScaleDamagePercent));
        Assert.Equal(
            [(-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1)],
            flash.FlashBottomOffsets);
        Assert.Equal([(-1, -1), (0, -1), (1, -1)], flash.FlashTopOffsets);
        var malformed = File.ReadAllText(path).Replace(
            "-1,0;0,0;1,0;-1,1;0,1;1,1",
            "0,bad",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv("spells.tsv", malformed));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([flash with { SpellEffectScaleDivisor = 0 }]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                flash with { FlashBottomOffsets = [(0, 0)] },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                flash with {
                    FlashTopOffsets = [(-1, -1), (0, -1), (2, -1)],
                },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                flash with { FlashMonsterDamageLevelScale = 0 },
            ]));
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
            "spells.tsv",
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
            "spells.tsv",
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
            "spells.tsv",
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
        Assert.Equal(5, teleport.TeleportSearchRadius);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([teleport with { ProjectileLifetime = 3 }]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([teleport with { TeleportSearchRadius = 0 }]));
    }

    [Fact]
    public void ShippedWarpCarriesNativeDestinationSearchTuning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            File.ReadAllText(path));

        Assert.True(catalog.TryGet(49, out var warp));
        Assert.Equal(AuthoritativeProjectileBehavior.Warp, warp.ProjectileBehavior);
        Assert.Equal(5, warp.WarpSearchRadius);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([warp with { WarpSearchRadius = 0 }]));
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
            "spells.tsv",
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
        Assert.Equal(
            [(0, 0), (1, 0), (0, 1), (1, 1), (2, 0), (0, 2), (1, 2), (2, 1)],
            resurrection.ResurrectionInitialOffsets);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                resurrection with { ResurrectionSearchRadius = 0 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                resurrection with { ResurrectionInitialOffsets = [(0, 0)] },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv(
                "spells.tsv",
                File.ReadAllText(path).Replace(
                    "0,0;1,0;0,1;1,1;2,0;0,2;1,2;2,1",
                    "0,0;invalid")));
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
    public void ShippedManaCarriesNativeRestorationRanges()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(41, out var mana));
        Assert.Equal(AuthoritativeProjectileBehavior.Mana, mana.ProjectileBehavior);
        Assert.Equal((10, 4, 6), (
            mana.ManaRandomRange,
            mana.ManaCharacterLevelRandomRange,
            mana.ManaSpellLevelRandomRange));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                mana with { ManaCharacterLevelRandomRange = 0 },
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSpellCatalog([
                mana with { ProjectileBehavior = AuthoritativeProjectileBehavior.Magi },
            ]));
    }

    [Fact]
    public void ShippedRageCarriesNativeAdmissionLifeThreshold()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(22, out var rage));
        Assert.Equal(AuthoritativeProjectileBehavior.RageController, rage.ProjectileBehavior);
        Assert.Equal(64, rage.RageMinimumLifeQ6PerCharacterLevel);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            rage with {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Magi,
            },
        ]));
    }

    [Fact]
    public void ShippedReflectCarriesNativeDefaultSpellLevel()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(43, out var reflect));
        Assert.Equal(AuthoritativeProjectileBehavior.Reflect, reflect.ProjectileBehavior);
        Assert.Equal(2, reflect.ReflectDefaultSpellLevel);
        Assert.Equal(20, reflect.ReflectDamagePercentMinimum);
        Assert.Equal(30, reflect.ReflectDamagePercentMaximum);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            reflect with {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Magi,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            reflect with {
                ReflectDamagePercentMinimum = 40,
                ReflectDamagePercentMaximum = 30,
            },
        ]));
    }

    [Fact]
    public void ShippedBerserkCarriesNativeResistanceFrequency()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(48, out var berserk));
        Assert.Equal(AuthoritativeProjectileBehavior.Berserk, berserk.ProjectileBehavior);
        Assert.Equal(2, berserk.BerserkResistanceFrequency);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            berserk with {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Magi,
            },
        ]));
    }

    [Fact]
    public void ShippedChargedBoltCarriesNativePathRandomRange()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "spells.tsv");
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

        Assert.True(catalog.TryGet(30, out var chargedBolt));
        Assert.Equal(AuthoritativeProjectileBehavior.ChargedBolt, chargedBolt.ProjectileBehavior);
        Assert.Equal(15, chargedBolt.ChargedBoltPathRandomRange);
        Assert.Equal(
            [-1, 0, 1, -1, 0, 1, -1, -1, 0, 0, 1, 1, 0, 1, -1, 0],
            chargedBolt.ChargedBoltPathPattern);
        Assert.Equal((2, 5, 95), (
            chargedBolt.ChargedBoltHitMonsterLevelScale,
            chargedBolt.ChargedBoltMinimumHitChance,
            chargedBolt.ChargedBoltMaximumHitChance));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            chargedBolt with {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Firebolt,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            chargedBolt with { ChargedBoltMinimumHitChance = 96 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            chargedBolt with { ChargedBoltPathPattern = [0] },
        ]));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSpellCatalog.LoadTsv(
                "spells.tsv",
                File.ReadAllText(path).Replace(
                    "-1,0,1,-1,0,1,-1,-1,0,0,1,1,0,1,-1,0",
                    "0,bad")));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            chargedBolt with {
                ChargedBoltMinimumHitChance = 80,
                ChargedBoltMaximumHitChance = 79,
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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        var catalog = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(path));

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
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(52, 100, 0, 0, 0, 0) {
                ProjectileBehavior = AuthoritativeProjectileBehavior.Golem,
                ProjectileLifetime = 1,
                ProjectileCount = 1,
                Range = 1,
                DamageType = AuthoritativeDamageType.Fire,
                StaffLevel = 9,
                GolemDeathAnimationTicks = 12,
                GolemHitPointsLevelBaseQ6 = 0,
                GolemHitPointsManaDivisor = 3,
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
