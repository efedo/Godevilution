using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeUniqueMonsterCatalogTests
{
    [Fact]
    public void AppliesValidatedIdentityToMatchingUniqueEncounter()
    {
        var target = new AuthoritativeCombatTarget(
            16,
            1,
            1,
            20,
            monsterId: 126,
            unique: true);
        var catalog = AuthoritativeUniqueMonsterCatalog.LoadTsv(
            "unique_monsters.tsv",
            "entity_id\tmonster_id\tidentity\n16\t126\tDefiler\n");

        catalog.ApplyTo([target]);

        Assert.Equal("Defiler", target.UniqueIdentity);
    }

    [Fact]
    public void UniqueMonsterRowsResolveStableMonsterSymbols()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("monster", "defiler", 126),
        ]);
        var catalog = AuthoritativeUniqueMonsterCatalog.LoadTsv(
            "unique_monsters.tsv",
            "entity_id\tmonster_id\tmonster_symbol\tidentity\n16\t0\tdefiler\tDefiler\n",
            stableIds);

        Assert.Equal(126U, Assert.Single(catalog.Definitions).MonsterId);
    }

    [Fact]
    public void ValidatesExplicitMonsterSelectorsWithoutResolvingDeferredAiSelectors()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            CreateType(126) with { NativeAi = "SkeletonMelee" },
        ]);
        var catalog = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 126, "Defiler", "SkeletonMelee"),
            new AuthoritativeUniqueMonsterDefinition(0, 0, "Gharbad", "Gharbad"),
        ]);

        catalog.ValidateMonsterTypes(monsterTypes);
    }

    [Fact]
    public void RejectsUnknownOrMismatchedExplicitMonsterSelectors()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            CreateType(126) with { NativeAi = "SkeletonMelee" },
        ]);
        var unknown = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 999, "Unknown", "SkeletonMelee"),
        ]);
        Assert.Throws<InvalidDataException>(() => unknown.ValidateMonsterTypes(monsterTypes));

        var mismatched = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 126, "Wrong AI", "Gharbad"),
        ]);
        Assert.Throws<InvalidDataException>(() => mismatched.ValidateMonsterTypes(monsterTypes));
    }

    [Fact]
    public void RejectsIdentitiesThatExceedTheNativeUtf8ByteLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthoritativeCombatTarget(
            16,
            1,
            1,
            20,
            unique: true,
            uniqueIdentity: new string('é', 65)));

        Assert.Throws<InvalidDataException>(() => new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(16, 126, new string('é', 65)),
        ]));
    }

    [Fact]
    public void AppliesIdentityThroughValidatedMonsterAndNativeAiSelector()
    {
        var target = new AuthoritativeCombatTarget(
            0x10000010,
            20,
            20,
            220,
            monsterId: 126,
            unique: true,
            nativeAi: "SkeletonMelee");
        var catalog = AuthoritativeUniqueMonsterCatalog.LoadTsv(
            "unique_monsters.tsv",
            "entity_id\tmonster_id\tidentity\tnative_ai\n"
                + "0\t126\tDefiler\tSkeletonMelee\n");

        catalog.ApplyTo([target]);

        Assert.Equal("Defiler", target.UniqueIdentity);
    }

    [Fact]
    public void RejectsSelectorThatMatchesMultipleUniqueEncounters()
    {
        var targets = new[] {
            new AuthoritativeCombatTarget(
                1,
                1,
                1,
                20,
                monsterId: 126,
                unique: true,
                nativeAi: "SkeletonMelee"),
            new AuthoritativeCombatTarget(
                2,
                2,
                2,
                20,
                monsterId: 126,
                unique: true,
                nativeAi: "SkeletonMelee"),
        };
        var catalog = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(
                0,
                126,
                "Defiler",
                "SkeletonMelee"),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.ApplyTo(targets));
    }

    [Fact]
    public void DefersUnmatchedSelectorUntilGeneratedEncounterMaterializes()
    {
        var target = new AuthoritativeCombatTarget(
            16,
            1,
            1,
            20,
            monsterId: 126,
            unique: false,
            nativeAi: "SkeletonMelee");
        var catalog = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(
                0,
                126,
                "Defiler",
                "SkeletonMelee"),
        ]);

        catalog.ApplyTo([target]);

        Assert.Empty(target.UniqueIdentity);
    }

    [Fact]
    public void RejectsIdentityWhenEncounterTypeOrUniqueFlagDoesNotMatch()
    {
        var target = new AuthoritativeCombatTarget(
            16,
            1,
            1,
            20,
            monsterId: 126);
        var catalog = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(16, 126, "Defiler"),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.ApplyTo([target]));
    }

    [Fact]
    public void AppliesAuthoredSpawnWeakeningAlongsideUniqueIdentity()
    {
        var target = new AuthoritativeCombatTarget(
            16,
            1,
            1,
            1332,
            armorClass: 125,
            maxHitPoints: 1332,
            monsterId: 139,
            unique: true,
            fireResistance: 100,
            lightningResistance: 100,
            magicResistance: 100,
            acidResistance: 100,
            nativeAi: "SkeletonMelee");
        var catalog = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 139, "Na-Krul", "SkeletonMelee") {
                SpawnHealthPercent = 50,
                SpawnArmorDelta = -50,
                SpawnFireResistance = 0,
                SpawnLightningResistance = 0,
                SpawnMagicResistance = 0,
                SpawnAcidResistance = 0,
            },
        ]);

        catalog.ApplyToGenerated([target]);

        Assert.Equal("Na-Krul", target.UniqueIdentity);
        Assert.Equal((666, 666), (target.MaxHitPoints, target.HitPoints));
        Assert.Equal(75, target.ArmorClass);
        Assert.Equal((0, 0, 0, 0), (
            target.FireResistance,
            target.LightningResistance,
            target.MagicResistance,
            target.AcidResistance));
    }

    private static AuthoritativeMonsterTypeDefinition CreateType(uint monsterId)
        => new(
            monsterId,
            monsterId,
            $"Monster {monsterId}",
            1,
            16,
            1,
            10,
            10,
            0,
            1,
            2,
            1,
            2,
            false,
            false,
            false) {
                NativeAi = "Zombie",
            };
}
