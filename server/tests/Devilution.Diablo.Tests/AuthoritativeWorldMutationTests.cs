using Devilution.Data;
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
    public void LoadsAndExpandsGeometryRectanglesDeterministically()
    {
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tlevel_id\tgeometry_rect_add\tgeometry_rect_remove\n"
                + "7\t1\t1,1,2,2\t0,0,1,1;2,3,1,1\n");

        var mutation = Assert.Single(catalog.Definitions);
        var rectangle = Assert.Single(mutation.GeometryRectanglesAdd);
        Assert.Equal((1, 1, 2, 2), (rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));
        Assert.Equal([5, 6, 9, 10], mutation.GetBlockedCells(4, 4));
        Assert.Equal([0, 14], mutation.GetBlockedCellsToRemove(4, 4));
        Assert.Equal(mutation.GetBlockedCells(4, 4), mutation.GetMissileBlockedCells(4, 4));
    }

    [Fact]
    public void AppliesGeometryRectanglesToWalkAndMissileProjection()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(1, 1, 2, 2)]),
        ]);
        var world = new DiabloWorldState(geometry, worldMutations: mutations);

        Assert.True(world.ApplyWorldMutation(7));
        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(1, 2, 2));
        Assert.True(world.IsMissileGeometryBlocked(1, 1, 1));
        Assert.True(world.IsWalkable(1, 0, 0));
    }

    [Fact]
    public void RejectsOutOfBoundsOrOverlappingGeometryRectangles()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(
                    7,
                    1,
                    geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(3, 3, 2, 2)]),
            ]).ValidateWorld(geometry));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(
                    8,
                    1,
                    blockedCellsRemove: [5],
                    geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(1, 1, 2, 2)]),
            ]).ValidateWorld(geometry));
    }

    [Fact]
    public void ResolvesSetLevelMutationTargetsToRuntimeLevels()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("set_level", "SL_BONECHAMB", 2),
        ]);
        var catalog = AuthoritativeWorldMutationCatalog.LoadTsv(
            "world_mutations.tsv",
            "mutation_id\tset_level_id\tset_level_symbol\tactive_map\tblocked_cells_add\n"
                + "7\t2\tSL_BONECHAMB\t1\t4\n",
            stableIds);

        var mutation = Assert.Single(catalog.Definitions);
        Assert.Equal(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(2, activeMap: true),
            mutation.LevelId);
        Assert.Equal([4], mutation.BlockedCellsAdd);
    }

    [Fact]
    public void RejectsAmbiguousWorldMutationLevelTargets()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tset_level_id\tblocked_cells_add\n"
                    + "7\t1\t2\t4\n"));
    }

    [Fact]
    public void RejectsActiveMapOnOrdinaryWorldMutationTarget()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                "mutation_id\tlevel_id\tactive_map\tblocked_cells_add\n"
                    + "7\t1\t1\t4\n"));
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
    public void ObjectTableLoadsSpellLevelGrant()
    {
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\n"
                + "20\t4\t1\t1\t0\tGrantSpellLevel\t7\n"));

        Assert.Equal(AuthoritativeObjectEffectKind.GrantSpellLevel, @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
    }

    [Fact]
    public void ObjectTableLoadsSpellProjectileEffect()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\tprojectile_target_offset_x\tprojectile_target_offset_y\n"
                + "20\t4\t1\t1\t0\tSpawnSpellProjectile\t7\tphasing\t1\t-2\n",
            stableIds));

        Assert.Equal(AuthoritativeObjectEffectKind.SpawnSpellProjectile, @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
        Assert.Equal((1, -2), (@object.ProjectileTargetOffsetX, @object.ProjectileTargetOffsetY));
    }

    [Fact]
    public void ObjectTableLoadsActivatingPlayerProjectileSource()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\tprojectile_source\tprojectile_target_offset_x\tprojectile_target_offset_y\n"
                + "20\t4\t1\t1\t0\tSpawnSpellProjectile\t0\tphasing\tActivatingPlayer\t1\t-2\n",
            stableIds));

        Assert.Equal(
            AuthoritativeObjectProjectileSourceKind.ActivatingPlayer,
            @object.ProjectileSourceKind);
    }

    [Fact]
    public void ObjectTableLoadsCompositeSpellGrantAndProjectileEffect()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "guardian", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\tprojectile_target_offset_x\tprojectile_target_offset_y\n"
                + "20\t4\t1\t1\t0\tGrantSpellLevelAndSpawnProjectile\t0\tguardian\t1\t0\n",
            stableIds));

        Assert.Equal(
            AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile,
            @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
        Assert.Equal((1, 0), (@object.ProjectileTargetOffsetX, @object.ProjectileTargetOffsetY));
    }

    [Fact]
    public void ObjectTableLoadsQuestSequenceFollowUp()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tquest_id\teffect_kind\teffect_amount\tquest_sequence_follow_up_kind\tquest_sequence_follow_up_amount\tquest_sequence_follow_up_spell_symbol\tquest_sequence_follow_up_offset_x\tquest_sequence_follow_up_offset_y\n"
                + "20\t4\t1\t1\t0\t30\tQuestSequence\t1\tSpawnSpellProjectile\t0\tphasing\t1\t-2\n",
            stableIds));

        Assert.Equal(AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile, @object.QuestSequenceFollowUpKind);
        Assert.Equal(7U, @object.QuestSequenceFollowUpAmount);
        Assert.Equal((1, -2), (@object.QuestSequenceFollowUpOffsetX, @object.QuestSequenceFollowUpOffsetY));
    }

    [Fact]
    public void ObjectTableLoadsWorldMutationProjectileFollowUp()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "phasing", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\tquest_sequence_follow_up_kind\tquest_sequence_follow_up_amount\tquest_sequence_follow_up_spell_symbol\tquest_sequence_follow_up_offset_x\tquest_sequence_follow_up_offset_y\n"
                + "20\t4\t1\t1\t0\tWorldMutation\t7\tSpawnSpellProjectile\t0\tphasing\t1\t0\n",
            stableIds));

        Assert.Equal(AuthoritativeObjectEffectKind.WorldMutation, @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
        Assert.Equal(
            AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            @object.QuestSequenceFollowUpKind);
        Assert.Equal(7U, @object.QuestSequenceFollowUpAmount);
        Assert.Equal((1, 0), (@object.QuestSequenceFollowUpOffsetX, @object.QuestSequenceFollowUpOffsetY));
    }

    [Fact]
    public void WorldMutationRejectsNonProjectileFollowUp()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            questSequenceFollowUpAmount: 8));
    }

    [Fact]
    public void ObjectTableResolvesStableSpellSymbolForSpellLevelGrant()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "guardian", 7),
        ]);
        var @object = Assert.Single(AuthoritativeWorldObjectCatalog.LoadTsv(
            "objects.tsv",
            "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\teffect_kind\teffect_amount\teffect_spell_symbol\n"
                + "20\t4\t1\t1\t0\tGrantSpellLevel\t0\tguardian\n",
            stableIds));

        Assert.Equal(7, @object.EffectAmount);
    }

    [Fact]
    public void ObjectActivationGrantsOneCatalogSpellLevel()
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
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevel,
            effectAmount: 7);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0),
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var learned = Assert.Single(player.MemorizedSpells);
        Assert.Equal(new MemorizedSpellState(7, 1), learned);
        Assert.True(@object.Activated);
    }

    [Fact]
    public void ObjectActivationSpawnsValidatedPhasingProjectile()
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
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1,
            projectileTargetOffsetY: 0);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Phasing, projectile.Behavior);
        Assert.Equal(7U, projectile.SpellId);
        Assert.Equal(2, projectile.PositionX);
        Assert.Equal(0, projectile.PositionY);
        Assert.Equal(2U, projectile.RemainingTicks);
        Assert.True(@object.Activated);
    }

    [Fact]
    public void ObjectActivationCanSpawnPhasingProjectileFromActivatingPlayer()
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
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1,
            projectileSourceKind: AuthoritativeObjectProjectileSourceKind.ActivatingPlayer);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal((0, 0), (projectile.PositionX, projectile.PositionY));
        Assert.Equal((2, 0), (projectile.TargetX, projectile.TargetY));
    }

    [Fact]
    public void CompositeObjectEffectGrantsSpellAndSpawnsProjectileAtomically()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            41,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1,
            projectileTargetOffsetY: 0);
        var world = new DiabloWorldState(geometry, objects: [@object]);
        var player = CreatePlayer();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(new MemorizedSpellState(7, 1), Assert.Single(player.MemorizedSpells));
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Phasing, projectile.Behavior);
        Assert.Equal(7U, projectile.SpellId);
        Assert.Equal((2, 0), (projectile.PositionX, projectile.PositionY));
        Assert.True(@object.Activated);
    }

    [Fact]
    public void WorldMutationObjectAppliesMutationAndFollowUpProjectile()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            41,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 7,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            questSequenceFollowUpAmount: 9,
            questSequenceFollowUpOffsetX: 1,
            questSequenceFollowUpOffsetY: 0);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 1, [6]),
            ]));
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(9, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(9U, projectile.SpellId);
        Assert.Equal((2, 0), (projectile.PositionX, projectile.PositionY));
        Assert.True(@object.Activated);
        Assert.False(world.IsWalkable(1, 2, 1));
    }

    [Fact]
    public void QuestSequenceAppliesFollowUpMutationOnlyOnExpectedStep()
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
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            questSequenceFollowUpAmount: 7);
        var world = new DiabloWorldState(
            geometry,
            objects: [@object],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 1, [6], [5]),
            ]));
        var player = CreatePlayer();

        var result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(1, quest.Variable1);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        Assert.False(@object.Activated);

        quest.Variable1 = 1;
        world.RestoreWorldMutations([]);
        result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            2,
            [player]);
        Assert.True(result.Succeeded);
        Assert.Equal(0, quest.Variable1);
        Assert.Empty(world.ActiveWorldMutationIds);

        result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(20),
            3,
            [player]);
        Assert.True(result.Succeeded);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
    }

    [Fact]
    public void QuestSequenceSpawnsFollowUpProjectileAtAuthoredOffset()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t4\t4\tnone\n");
        var quest = new AuthoritativeQuestState(30, 1, 5, active: false);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            questSequenceFollowUpAmount: 7,
            questSequenceFollowUpOffsetX: 1,
            questSequenceFollowUpOffsetY: 0);
        var world = new DiabloWorldState(geometry, objects: [@object], quests: [quest]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 1,
            },
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(7U, projectile.SpellId);
        Assert.Equal((2, 0), (projectile.PositionX, projectile.PositionY));
        Assert.False(@object.Activated);
    }

    [Fact]
    public void ObjectActivationRejectsUnsupportedSpellProjectileBehavior()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7);
        var world = new DiabloWorldState(objects: [@object]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0),
        ]);

        var result = new DiabloWorldCommandProcessor(world, spells: spells).Execute(
            CreatePlayer(),
            new DiabloOperateObjectIntent(20),
            1,
            []);

        Assert.False(result.Succeeded);
        Assert.False(@object.Activated);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void ObjectCatalogRejectsUnsupportedSpellProjectileBehavior()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.ValidateSpellRequirements([@object], spells));

        Assert.Contains("unsupported authored projectile behavior", exception.Message);
    }

    [Fact]
    public void ObjectCatalogRejectsActivatingPlayerSourceForGuardian()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileSourceKind: AuthoritativeObjectProjectileSourceKind.ActivatingPlayer);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.GuardianController,
                RandomDamageRange = 10,
                MagicDamageDivisor = 8,
                SpellLevelDamage = 1,
                DamageBonus = 1,
                ResistantDamageDivisor = 4,
                ProjectileImpactLifetime = 1,
                ManaAdjustment = 2,
                ManaAdjustmentDivisor = 1,
                MinimumManaCost = 30,
                ManaCostScale = 64,
                GuardianDurationTickScale = 16,
                GuardianMinimumDuration = 30,
                GuardianMaximumDuration = 30,
                GuardianCharacterLevelDurationDivisor = 2,
                GuardianSpawnSearchRadius = 5,
                GuardianTargetSearchRadius = 6,
                GuardianFireInterval = 16,
                GuardianProjectileSpeedLevelStep = 2,
                GuardianProjectileSpeedMaximumBonus = 47,
            },
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.ValidateSpellRequirements([@object], spells));

        Assert.Contains("only for Phasing", exception.Message);
    }

    [Fact]
    public void ObjectCatalogRejectsProjectileTargetOutsideWorld()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            1,
            effectKind: AuthoritativeObjectEffectKind.SpawnSpellProjectile,
            effectAmount: 7,
            projectileTargetOffsetX: 1);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t2\t2\tnone\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectCatalog.ValidateWorld([@object], world));

        Assert.Contains("projectile target outside its level", exception.Message);
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
