using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Explicit family routing for operation-time item generation in Catacombs. */
public sealed class CatacombsNativeLootOperationContextTests
{
    [Fact]
    public void MonsterLootUsesExplicitCatacombsContextAndKeepsMinionEarlyReturn()
    {
        var catalog = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog(goldPercent: 100);
        var world = EmptyWorld();
        var request = new AuthoritativeCathedralMonsterLootRequest(
            world,
            catalog,
            new AuthoritativeCathedralThemeItemPolicy(5, CurrentDungeonLevel: 5),
            AuthoritativeCathedralMonsterLootGeneratorTests.RuntimeCatalog(
                noDropMonsterId: 1, specialTreasureMonsterId: 2),
            MonsterId: 0,
            MonsterItemSeed: 2588,
            PositionX: 40,
            PositionY: 40);

        var result = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            request, AuthoritativeNativeDungeonFamilyContext.Catacombs(5));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.DroppedItem, result.Outcome);
        Assert.Equal(0, result.SelectedNativeItemIndex);
        Assert.Equal(5, request.Policy.Depth);
        Assert.Equal(1, result.Item!.CreateInfo & 0x3F);

        var minionResult = AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
            request with { IsPlayerMinion = true },
            AuthoritativeNativeDungeonFamilyContext.Catacombs(5));
        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.MinionSkipped, minionResult.Outcome);
        Assert.Equal(minionResult.InitialRandomState, minionResult.FinalRandomState);
        Assert.Equal(world.ItemRefs, minionResult.SignedItemGrid);
    }

    [Fact]
    public void BarrelAndSarcophagusRouteGeneratedItemsThroughCatacombsContext()
    {
        var context = AuthoritativeNativeDungeonFamilyContext.Catacombs(5);
        var catalog = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog();

        var barrelRequest = new AuthoritativeCathedralBarrelOperationRequest(
            WorldWithObject(1),
            catalog,
            new AuthoritativeCathedralThemeItemPolicy(5, CurrentDungeonLevel: 0),
            BarrelFacts(),
            new bool[9],
            InitialWorldRandomState: 0x12345678);
        var barrel = AuthoritativeCathedralBarrelOperationGenerator.Generate(barrelRequest, context);

        Assert.NotNull(barrel.Loot);
        Assert.False(barrel.WorldState.ObjectSolid[0]);
        Assert.False(barrel.WorldState.ObjectCanInteract[0]);
        Assert.Equal(AuthoritativeCathedralBarrelOpenedMutation.Native, barrel.OpenedMutation);

        var sarcophagusRequest = new AuthoritativeCathedralSarcophagusOperationRequest(
            WorldWithObject(1, sarcophagusFootprint: true),
            catalog,
            new AuthoritativeCathedralThemeItemPolicy(5, CurrentDungeonLevel: 5),
            SarcophagusFacts(),
            new bool[9]);
        var sarcophagus = AuthoritativeCathedralSarcophagusOperationGenerator.Generate(
            sarcophagusRequest, context);

        Assert.NotNull(sarcophagus.Loot);
        Assert.Null(sarcophagus.Spawn);
        Assert.False(sarcophagus.WorldState.ObjectCanInteract[0]);
        Assert.Equal(2588u, sarcophagus.InitialRandomState);
    }

    [Fact]
    public void ContextDepthMustMatchAndPublicCathedralEntryPointsStayClosedToDepthFive()
    {
        var context = AuthoritativeNativeDungeonFamilyContext.Catacombs(6);
        var catalog = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog();
        var monsterRequest = new AuthoritativeCathedralMonsterLootRequest(
            EmptyWorld(), catalog, new AuthoritativeCathedralThemeItemPolicy(5),
            AuthoritativeCathedralMonsterLootGeneratorTests.RuntimeCatalog(),
            0, 2588, 40, 40);
        var barrelRequest = BarrelRequest(catalog, depth: 5);
        var sarcophagusRequest = SarcophagusRequest(catalog, depth: 5);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(monsterRequest, context));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralBarrelOperationGenerator.Generate(barrelRequest, context));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralSarcophagusOperationGenerator.Generate(sarcophagusRequest, context));

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(monsterRequest));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralBarrelOperationGenerator.Generate(barrelRequest));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralSarcophagusOperationGenerator.Generate(sarcophagusRequest));
    }

    [Fact]
    public void MonsterCurrentDungeonLevelIsZeroOrTheBoundDepth()
    {
        var request = new AuthoritativeCathedralMonsterLootRequest(
            EmptyWorld(),
            AuthoritativeCathedralMonsterLootGeneratorTests.Catalog(),
            new AuthoritativeCathedralThemeItemPolicy(5, CurrentDungeonLevel: 6),
            AuthoritativeCathedralMonsterLootGeneratorTests.RuntimeCatalog(),
            0, 2588, 40, 40);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralMonsterLootGenerator.SpawnLoot(
                request, AuthoritativeNativeDungeonFamilyContext.Catacombs(5)));
    }

    private static AuthoritativeCathedralBarrelOperationRequest BarrelRequest(
        AuthoritativeCathedralThemeItemCatalog catalog,
        int depth)
        => new(
            WorldWithObject(1),
            catalog,
            new AuthoritativeCathedralThemeItemPolicy(depth),
            BarrelFacts(),
            new bool[9],
            InitialWorldRandomState: 0x12345678);

    private static AuthoritativeCathedralSarcophagusOperationRequest SarcophagusRequest(
        AuthoritativeCathedralThemeItemCatalog catalog,
        int depth)
        => new(
            WorldWithObject(1, sarcophagusFootprint: true),
            catalog,
            new AuthoritativeCathedralThemeItemPolicy(depth),
            SarcophagusFacts(),
            new bool[9]);

    private static AuthoritativeCathedralNativeBarrelFacts BarrelFacts()
        => new(
            0, 900, 57, 700, new(50, 50), 2588,
            0, 0, 1, -1, 1, 1, 0, 1000, 0, 5,
            true, true, 1, false, false, false, null, 0, 0);

    private static AuthoritativeCathedralNativeSarcophagusFacts SarcophagusFacts()
        => new(
            0, 901, 48, 700, new(50, 50), 2588,
            2, -1, 1, 1, 0, 1, 0, 8,
            false, false, null, 8, 0);

    private static AuthoritativeCathedralThemeItemWorldState WorldWithObject(
        int objectCount,
        bool sarcophagusFootprint = false)
    {
        var objectRefs = new int[AuthoritativeCathedralThemeItemWorldState.CellCount];
        var origin = Index(50, 50);
        objectRefs[origin] = 1;
        if (sarcophagusFootprint)
            objectRefs[Index(50, 49)] = -1;
        return new(
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, AuthoritativeCathedralThemeItemWorldState.ActiveCapacity).ToArray(),
            0,
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            objectRefs,
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            Enumerable.Repeat(true, objectCount).ToArray(),
            Enumerable.Repeat(true, objectCount).ToArray());
    }

    private static AuthoritativeCathedralThemeItemWorldState EmptyWorld()
        => new(
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, AuthoritativeCathedralThemeItemWorldState.ActiveCapacity).ToArray(),
            0,
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new int[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            new bool[AuthoritativeCathedralThemeItemWorldState.CellCount],
            Array.Empty<bool>(),
            Array.Empty<bool>());

    private static int Index(int x, int y)
        => checked(y * AuthoritativeCathedralThemeItemWorldState.Width + x);
}
