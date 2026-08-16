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
}
