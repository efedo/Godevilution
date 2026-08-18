using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldMutationTests
{
    [Fact]
    public void LoadsAndNormalizesCellOverlayDefinitions()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tblocked_cells_add\tblocked_cells_remove\tmissile_blocked_cells_add\tmissile_blocked_cells_remove\n"
                + "7\t1\t6, 6\t5\tnone\t5\n");

        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal(7U, mutation.MutationId);
        Assert.Equal([6], mutation.BlockedCellsAdd);
        Assert.Equal([5], mutation.BlockedCellsRemove);
        Assert.Equal([], mutation.MissileBlockedCellsAdd);
        Assert.Equal([5], mutation.MissileBlockedCellsRemove);
    }

    [Fact]
    public void AppliesMutationToWalkAndMissileGeometry()
    {
        var worldGeometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\tmissile_blocked_cells\n"
                + "1\t4\t4\t5\t5\n");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(7, 1, [6], [5], [6], [5]),
        ]);
        var world = new DiabloWorldState(worldGeometry, worldMutations: mutations);

        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.True(world.IsWalkable(1, 2, 1));
        Assert.True(world.IsMissileGeometryBlocked(1, 1, 1));

        Assert.True(world.ApplyWorldMutation(7));
        Assert.True(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(1, 2, 1));
        Assert.True(world.IsMissileGeometryBlocked(1, 2, 1));
        Assert.Contains(7U, world.ActiveWorldMutationIds);

        world.RestoreWorldMutations([7]);
        Assert.False(world.IsWalkable(1, 2, 1));
    }

    [Fact]
    public void ObjectActivationAppliesValidatedMutation()
    {
        var worldGeometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\t5\n");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(7, 1, [6], [5]),
        ]);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7);
        var world = new DiabloWorldState(
            worldGeometry,
            objects: [@object],
            worldMutations: mutations);
        var player = CreatePlayer();

        var result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        Assert.True(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(1, 2, 1));
    }

    private static AuthoritativePlayerState CreatePlayer()
        => new(
            0,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            50,
            1,
            1,
            0);
}
