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

    [Fact]
    public void ObjectTableLoadsQuestTransitionRequirements()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tquest_id\teffect_kind\teffect_amount\tquest_transition_kind\tquest_transition_amount\tquest_transition_required_variable1\n"
                + "20\t4\t1\t1\t0\t30\tWorldMutation\t7\tActivateAndSetVariable1\t1\t0\n"));

        Assert.Equal(
            AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1,
            @object.QuestTransitionKind);
        Assert.Equal(1, @object.QuestTransitionAmount);
        Assert.Equal(0, @object.QuestTransitionRequiredVariable1);
    }

    [Fact]
    public void ObjectTableLoadsValidatedRewardMetadata()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\treward_kind\treward_id\treward_item_level\treward_offset_x\treward_offset_y\n"
                + "20\t4\t1\t1\t0\tNormalItem\t100\t3\t0\t1\n"));

        Assert.Equal(AuthoritativeObjectRewardKind.NormalItem, @object.RewardKind);
        Assert.Equal(100U, @object.RewardId);
        Assert.Equal(3, @object.RewardItemLevel);
        Assert.Equal(1, @object.RewardOffsetY);
    }

    [Fact]
    public void ObjectActivationGeneratesDeterministicNormalItemReward()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            rewardKind: AuthoritativeObjectRewardKind.NormalItem,
            rewardId: 100,
            rewardItemLevel: 3,
            rewardOffsetY: 1);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(100, 3, 10, 10, 0, 0, 0, 1, 1, 1, 1, true),
        ]);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();

        var result = new DiabloWorldCommandProcessor(world, items: items).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(100U, reward.State.StableItemId);
        Assert.Equal(1, reward.PositionX);
        Assert.Equal(1, reward.PositionY);
        Assert.True(@object.Activated);
    }

    [Fact]
    public void ObjectActivationAppliesQuestTransitionAndMutationAtomically()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\t5\n");
        var quest = new AuthoritativeQuestState(30, 1, 5, active: false);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1,
            questTransitionAmount: 1,
            questTransitionRequiredVariable1: 0);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 1, [6], [5]),
            ]));

        var result = new DiabloWorldCommandProcessor(world).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(1, quest.Variable1);
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
