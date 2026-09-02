using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunMonsterCatalogTests
{
    [Fact]
    public void TsvLoaderMapsRawLayerIdentityToNativeMonsterIdentity()
    {
        var catalog = AuthoritativeNativeDunMonsterCatalog.LoadTsv(
            "native_dun_monsters.tsv",
            "native_dun_monster_id\tnative_monster_id\tmonster_id\n"
                + "1\t0\t1\n155\t51\t53\n");

        Assert.Equal(2, catalog.Definitions.Count);
        Assert.True(catalog.TryGetNativeMonsterId(1, out var zombie));
        Assert.Equal(0U, zombie);
        Assert.True(catalog.TryGetNativeMonsterId(155, out var butcher));
        Assert.Equal(51U, butcher);
        Assert.True(catalog.TryGetMonsterId(155, out var butcherStableId));
        Assert.Equal(53U, butcherStableId);
        Assert.False(catalog.TryGetNativeMonsterId(2, out _));
    }

    [Fact]
    public void RejectsRawLayerIdentityOutsideNativeConversionTable()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunMonsterCatalog.LoadTsv(
                "native_dun_monsters.tsv",
                "native_dun_monster_id\tnative_monster_id\tmonster_id\n158\t0\t1\n"));
    }

    [Fact]
    public void RejectsNativeMonsterTypeIdOutsideCompatibilityRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeDunMonsterCatalog([
                new AuthoritativeNativeDunMonsterDefinition(1, 200, 1),
            ]));
    }

    [Fact]
    public void RejectsDuplicateRawLayerIdentity()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeDunMonsterCatalog([
                new AuthoritativeNativeDunMonsterDefinition(1, 0, 1),
                new AuthoritativeNativeDunMonsterDefinition(1, 1, 2),
            ]));
    }

    [Fact]
    public void ValidatesNativeMonsterReferencesAgainstActiveCatalog()
    {
        var catalog = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 0, 1),
        ]);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                0,
                "Zombie",
                1,
                2,
                1,
                1,
                1,
                0,
                1,
                1,
                0,
                0,
                false,
                false,
                false),
        ]);

        catalog.ValidateMonsterTypes(monsterTypes);
    }

    [Fact]
    public void RejectsNativeMonsterReferencesAbsentFromActiveCatalog()
    {
        var catalog = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 51, 1),
        ]);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                0,
                "Zombie",
                1,
                2,
                1,
                1,
                1,
                0,
                1,
                1,
                0,
                0,
                false,
                false,
                false),
        ]);

        Assert.Throws<InvalidDataException>(
            () => catalog.ValidateMonsterTypes(monsterTypes));
    }

    [Fact]
    public void MaterializesMappedCellsInNativeRowMajorOrder()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            1,
            3,
            2,
            nativeMonsterLayer: [1, 0, 1, 0, 1, 2]));
        var types = new AuthoritativeMonsterTypeCatalog([CreateType(1, 0)]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            types,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
            ]));
        var catalog = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 0, 1),
        ]);

        var targets = catalog.Materialize(world, planner, seed: 17);

        Assert.Equal(3, targets.Count);
        Assert.Equal(
            [(0, 0), (2, 0), (1, 1)],
            targets.Select(target => (target.PositionX, target.PositionY)));
        Assert.Equal(
            [AuthoritativeNativeDunMonsterCatalog.RuntimeMonsterEntityIdBase,
                AuthoritativeNativeDunMonsterCatalog.RuntimeMonsterEntityIdBase + 1,
                AuthoritativeNativeDunMonsterCatalog.RuntimeMonsterEntityIdBase + 2],
            targets.Select(target => target.EntityId));
        Assert.All(targets, target => Assert.Equal(1U, target.MonsterId));

        Assert.Empty(catalog.Materialize(world, planner, targets));
    }

    [Fact]
    public void RejectsMappedCellThatIsBlockedOrOccupied()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            1,
            2,
            1,
            blockedCells: [0],
            nativeMonsterLayer: [1, 0]));
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            new AuthoritativeMonsterTypeCatalog([CreateType(1, 0)]),
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
            ]));
        var catalog = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 0, 1),
        ]);

        Assert.Throws<InvalidDataException>(
            () => catalog.Materialize(world, planner));
    }

    [Fact]
    public void SimulationMaterializesMappedNativeDunCells()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            1,
            2,
            1,
            nativeMonsterLayer: [1, 0]));
        var types = new AuthoritativeMonsterTypeCatalog([CreateType(1, 0)]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            types,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
            ]));
        var catalog = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 0, 1),
        ]);

        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                MonsterTypes = types,
                MonsterSpawnPlanner = planner,
                NativeDunMonsters = catalog,
            });

        var target = Assert.Single(simulation.WorldState.CombatTargets.Values);
        Assert.Equal((AuthoritativeNativeDunMonsterCatalog.RuntimeMonsterEntityIdBase, 1U, 0, 1U), (
            target.EntityId,
            target.LevelId,
            target.PositionX,
            target.MonsterId));
    }

    private static AuthoritativeMonsterTypeDefinition CreateType(
        uint monsterId,
        uint nativeMonsterId)
        => new(
            monsterId,
            nativeMonsterId,
            "Monster",
            1,
            16,
            1,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            false,
            false,
            false);
}
