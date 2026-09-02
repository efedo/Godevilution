using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterSpawnPolicyCatalogTests
{
    [Fact]
    public void DefaultsUnlistedIdentitiesToOrdinary()
    {
        var catalog = new AuthoritativeMonsterSpawnPolicyCatalog([
            new AuthoritativeMonsterSpawnPolicyDefinition(
                7,
                AuthoritativeMonsterSpawnRole.Boss),
        ]);

        Assert.True(catalog.IsOrdinary(2));
        Assert.False(catalog.IsOrdinary(7));
        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(7U, definition.MonsterId);
        Assert.Equal(AuthoritativeMonsterSpawnRole.Boss, definition.SpawnRole);
    }

    [Fact]
    public void AppliesLegacySpecialIdentityFallbackWhenPolicyIsOmitted()
    {
        var catalog = new AuthoritativeMonsterSpawnPolicyCatalog([]);
        var legacyGolem = CreateType(2) with { NativeAi = "Golem" };
        var legacyBoss = CreateType(3) with { Diablo = true };

        Assert.False(catalog.IsOrdinary(legacyGolem));
        Assert.False(catalog.IsOrdinary(legacyBoss));
        Assert.True(catalog.IsOrdinary(CreateType(4)));
    }

    [Fact]
    public void LoadsSymbolicRolesAndRejectsUnknownRoles()
    {
        var catalog = AuthoritativeMonsterSpawnPolicyCatalog.LoadTsv(
            "monster_spawn_policies.tsv",
            "monster_id\tmonster_symbol\tspawn_role\n"
                + "111\tgolem_native_109\tSummon\n");

        Assert.False(catalog.IsOrdinary(111));
        Assert.Equal(AuthoritativeMonsterSpawnRole.Summon, Assert.Single(catalog.Definitions).SpawnRole);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterSpawnPolicyCatalog.LoadTsv(
                "monster_spawn_policies.tsv",
                "monster_id\tspawn_role\n2\tencounter\n"));
    }

    [Fact]
    public void ValidatesReferencesAgainstMonsterTypes()
    {
        var catalog = new AuthoritativeMonsterSpawnPolicyCatalog([
            new AuthoritativeMonsterSpawnPolicyDefinition(
                9,
                AuthoritativeMonsterSpawnRole.Quest),
        ]);
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.ValidateMonsterTypes(types));
    }

    private static AuthoritativeMonsterTypeDefinition CreateType(uint monsterId)
        => new(
            monsterId,
            monsterId,
            $"Monster {monsterId}",
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
            };
}
