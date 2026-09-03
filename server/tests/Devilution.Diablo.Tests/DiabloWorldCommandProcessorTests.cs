using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloWorldCommandProcessorTests
{
    [Fact]
    public void FriendlyModeIntentSetsExplicitRetrySafeState()
    {
        var processor = new DiabloWorldCommandProcessor(new DiabloWorldState());
        var player = CreatePlayer();

        Assert.True(processor.Execute(
            player,
            new DiabloSetFriendlyModeIntent(false),
            1,
            [player]).Succeeded);
        Assert.False(player.FriendlyMode);

        Assert.True(processor.Execute(
            player,
            new DiabloSetFriendlyModeIntent(false),
            2,
            [player]).Succeeded);
        Assert.False(player.FriendlyMode);
    }

    [Fact]
    public void ActivatedObjectSelectsItsAuthoredPresentationFrame()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n1\t8\t8\tnone\n");
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            2,
            2,
            effectKind: AuthoritativeObjectEffectKind.Heal,
            effectAmount: 1,
            hideWhenActivated: false,
            animationFrame: 3,
            activatedAnimationFrame: 7);
        var player = CreatePlayer(levelId: 1, positionX: 2, positionY: 1, entityId: 1);
        var result = new DiabloWorldCommandProcessor(
            new DiabloWorldState(geometry, objects: [@object]))
            .Execute(player, new DiabloOperateObjectIntent(@object.EntityId), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.False(@object.HideWhenActivated);
        Assert.Equal(7, @object.AnimationFrame);
    }

    [Theory]
    [InlineData(71U, 3009U, 8U, 255)]
    [InlineData(72U, 3010U, 5U, 254)]
    [InlineData(88U, 3012U, 13U, 256)]
    public void NativeQuestBookTransitionTemplateActivatesItsLinkedQuest(
        uint nativeObjectId,
        uint questId,
        uint levelId,
        int dialogueMessageId)
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            $"level_id\twidth\theight\tblocked_cells\n{levelId}\t20\t20\tnone\n");
        var quest = new AuthoritativeQuestState(
            questId,
            levelId,
            requiredProgress: 1,
            active: false,
            variable1: 0);
        var @object = new AuthoritativeWorldObject(
            20,
            nativeObjectId,
            levelId,
            5,
            5,
            questId: questId,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1,
            questTransitionAmount: 1,
            questTransitionRequiredVariable1: 0,
            nativeObjectId: nativeObjectId,
            dialogueMessageId: dialogueMessageId);
        var world = new DiabloWorldState(geometry, objects: [@object], quests: [quest]);
        var player = CreatePlayer(levelId: levelId, positionX: 5, positionY: 4, entityId: 1);

        var result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(@object.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(1, quest.Variable1);
        Assert.False(quest.Completed);
        var dialogue = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Dialogue, dialogue.Kind);
        Assert.Equal(dialogueMessageId, dialogue.MessageId);
        Assert.Equal(questId, dialogue.QuestId);
        Assert.Equal(dialogueMessageId, quest.MessageId);
    }

    [Fact]
    public void QuestTransitionAppliesItsAuthoredFollowUpAfterTheTransition()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n24\t8\t8\tnone\n");
        var quest = new AuthoritativeQuestState(
            3022,
            24,
            1,
            active: true,
            variable1: 2,
            completionVariable1: 3);
        var lever = new AuthoritativeWorldObject(
            2409,
            6007,
            24,
            3,
            3,
            questId: 3022,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.CompleteQuest,
            questTransitionRequiredVariable1: 2,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            questSequenceFollowUpAmount: 7);
        var world = new DiabloWorldState(
            geometry,
            objects: [lever],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 24, blockedCellsAdd: [27]),
            ]));
        var player = CreatePlayer(levelId: 24, positionX: 3, positionY: 2, entityId: 1);

        var result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(lever.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Completed);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        Assert.Contains(27, world.LevelGeometry.Single().BlockedCells);
    }

    [Fact]
    public void MovementRejectsMalformedBlockedAndOccupiedDestinationsWithoutMutation()
    {
        var world = new DiabloWorldState(
            fallbackWidth: 3,
            fallbackHeight: 3,
            fallbackBlockedCells: [1]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);
        var otherPlayer = CreatePlayer(positionY: 1, entityId: 2);

        AssertRejected(
            processor.Execute(player, new DiabloMoveIntent(2, 0), 1, [player, otherPlayer]),
            DiabloRuleFailure.Malformed);
        AssertRejected(
            processor.Execute(player, new DiabloMoveIntent(1, 0), 1, [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);
        AssertRejected(
            processor.Execute(player, new DiabloMoveIntent(0, 1), 1, [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);

        Assert.Equal(0, player.PositionX);
        Assert.Equal(0, player.PositionY);
        Assert.Equal(DiabloPlayerDirection.South, player.FacingDirection);
    }

    [Fact]
    public void MovementRejectsCoordinateOverflowWithoutWrappingOrMutating()
    {
        var processor = new DiabloWorldCommandProcessor(new DiabloWorldState());
        var player = CreatePlayer(positionX: int.MaxValue, entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloMoveIntent(1, 0),
            1,
            [player]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal((int.MaxValue, 0), (player.PositionX, player.PositionY));
        Assert.Equal(DiabloPlayerDirection.South, player.FacingDirection);
    }

    [Fact]
    public void QuestSequenceFollowUpMaterializesDeterministicUniqueMonster()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            24,
            40,
            40,
            nativePieceLayer: Enumerable.Repeat<ushort>(0, 40 * 40)));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                139,
                137,
                "Na-Krul",
                24,
                24,
                31,
                1332,
                1332,
                125,
                40,
                50,
                40,
                50,
                false,
                false,
                false) {
                NativeAi = "SkeletonMelee",
            },
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            monsterTypes,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(24, 24, 100, 100, 0),
            ]),
            uniqueMonsters: new AuthoritativeUniqueMonsterCatalog([
                new AuthoritativeUniqueMonsterDefinition(0, 139, "Na-Krul", "SkeletonMelee") {
                    SpawnHealthPercent = 50,
                    SpawnArmorDelta = -50,
                    SpawnFireResistance = 0,
                    SpawnLightningResistance = 0,
                    SpawnMagicResistance = 0,
                    SpawnAcidResistance = 0,
                },
            ]));
        var quest = new AuthoritativeQuestState(
            3022,
            24,
            1,
            variable1: 2,
            completionVariable1: 3);
        var @object = new AuthoritativeWorldObject(
            2403,
            6003,
            24,
            10,
            10,
            questId: 3022,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 3,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster,
            questSequenceFollowUpAmount: 139,
            questSequenceFollowUpOffsetX: 1,
            questSequenceFollowUpOffsetY: 0,
            questSequenceFollowUpWorldMutationId: 7);
        var worldState = new DiabloWorldState(
            world,
            objects: [@object],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(
                    7,
                    24,
                    anchorEntityId: @object.EntityId,
                    pieceLayerRectangles: [
                        new AuthoritativeWorldMutationPieceRectangle(0, 0, 1, 1, 297),
                    ]),
            ]));
        var processor = new DiabloWorldCommandProcessor(
            worldState,
            random: new DiabloRandom(7),
            gameMode: DiabloGameMode.SinglePlayer,
            monsterSpawnPlanner: planner,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(positionX: 9, positionY: 10, levelId: 24, entityId: 1);
        var otherPlayer = CreatePlayer(positionX: 11, positionY: 10, levelId: 24, entityId: 2);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloOperateObjectIntent(@object.EntityId),
                1,
                [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2, quest.Variable1);
        Assert.Empty(worldState.CombatTargets);

        otherPlayer.PositionX = 0;
        otherPlayer.PositionY = 0;

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(@object.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var target = Assert.Single(worldState.CombatTargets.Values);
        Assert.Equal(
            AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId,
            target.EntityId);
        Assert.Equal(("Na-Krul", 666, 75), (target.UniqueIdentity, target.MaxHitPoints, target.ArmorClass));
        Assert.True(quest.Completed);
        Assert.Equal((ushort)297, worldState.LevelGeometry.Single().NativePieceLayer[10 * 40 + 10]);
    }

    [Fact]
    public void QuestSequenceRejectsAnUnknownStableMonsterReferenceBeforeMutation()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 7, "Native Seven A", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, false, false, false),
            new AuthoritativeMonsterTypeDefinition(
                7, 7, "Native Seven B", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, false, false, false),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            geometry,
            monsterTypes,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(1, 1, 100, 100, 0),
            ]));
        var quest = new AuthoritativeQuestState(42, 1, 1);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            1,
            1,
            1,
            questId: 42,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster,
            questSequenceFollowUpAmount: 99);
        var world = new DiabloWorldState(geometry, objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            monsterSpawnPlanner: planner,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(levelId: 1, positionX: 1, positionY: 0, entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(@object.EntityId),
            1,
            [player]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0, quest.Variable1);
        Assert.False(@object.Activated);
        Assert.Empty(world.CombatTargets);
    }

    [Fact]
    public void NaKrulLeverCompletesQuestAfterTheAuthoredTomeSequence()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n24\t40\t40\tnone\n");
        var quest = new AuthoritativeQuestState(
            3022,
            24,
            1,
            active: true,
            variable1: 3);
        var lever = new AuthoritativeWorldObject(
            2409,
            6007,
            24,
            27,
            19,
            questId: 3022,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.CompleteQuest,
            questTransitionRequiredVariable1: 3);
        var world = new DiabloWorldState(
            geometry,
            objects: [lever],
            quests: [quest]);
        var player = CreatePlayer(
            positionX: 26,
            positionY: 19,
            levelId: 24,
            entityId: 7);

        var result = new DiabloWorldCommandProcessor(world).Execute(
            player,
            new DiabloOperateObjectIntent(lever.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(lever.Activated);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
    }

    [Fact]
    public void MovementRejectsLiveMonsterOccupancy()
    {
        var monster = new AuthoritativeCombatTarget(10, 1, 0, 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var player = CreatePlayer(entityId: 1);
        var processor = new DiabloWorldCommandProcessor(world);

        var result = processor.Execute(player, new DiabloMoveIntent(1, 0), 1, [player]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0, player.PositionX);
    }

    [Fact]
    public void OccupancyTriggerAppliesAfterAuthoredLinkedObjectsActivate()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n5\t20\t20\tnone\n");
        var quest = new AuthoritativeQuestState(3016, 5, 1, active: true);
        var book1 = new AuthoritativeWorldObject(
            2010,
            1,
            5,
            0,
            0,
            questId: 3016,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 9302,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.ActivateQuest);
        var book2 = new AuthoritativeWorldObject(
            2011,
            2,
            5,
            4,
            4,
            questId: 3016,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 9303,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.ActivateQuest);
        var circle = new AuthoritativeWorldObject(
            2014,
            3,
            5,
            2,
            2,
            questId: 3016,
            effectKind: AuthoritativeObjectEffectKind.WorldMutation,
            effectAmount: 9304,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1,
            questTransitionAmount: 4,
            questSequenceFollowUpKind: AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            questSequenceFollowUpAmount: 10,
            questSequenceFollowUpOffsetX: 0,
            questSequenceFollowUpOffsetY: 10,
            projectileSourceKind: AuthoritativeObjectProjectileSourceKind.ActivatingPlayer,
            activateOnOccupancy: true,
            requiredActivatedLinkedObjects: 2);
        var world = new DiabloWorldState(
            geometry,
            objects: [book1, book2, circle],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(9302, 5),
                new AuthoritativeWorldMutation(9303, 5),
                new AuthoritativeWorldMutation(9304, 5),
            ]));
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(10, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
                ProjectileLifetime = 2,
                PhasingMinimumAxisOffset = 1,
                PhasingMaximumAxisOffset = 10,
            },
        ]);
        var processor = new DiabloWorldCommandProcessor(world, spells: spells);
        var player = CreatePlayer(levelId: 5, positionX: 0, positionY: 1, entityId: 7);

        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(book1.EntityId),
            1,
            [player]).Succeeded);
        player.PositionX = 4;
        player.PositionY = 3;
        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(book2.EntityId),
            2,
            [player]).Succeeded);

        player.PositionX = 1;
        player.PositionY = 2;
        var result = processor.Execute(
            player,
            new DiabloMoveIntent(1, 0),
            3,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(circle.Activated);
        Assert.Equal(4, quest.Variable1);
        Assert.Equal([9302U, 9303U, 9304U], world.ActiveWorldMutationIds);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal((7U, 5U, 2, 12), (
            projectile.SourceEntityId,
            projectile.LevelId,
            projectile.PositionX,
            projectile.TargetY));
    }

    [Fact]
    public void PortalRequiresExactSourceAndUnoccupiedDestination()
    {
        var portal = new AuthoritativePortal(5, 0, 0, 0, 2, 3, 4);
        var world = new DiabloWorldState(portals: [portal]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionY: 1, entityId: 1);
        var otherPlayer = CreatePlayer(positionX: 3, positionY: 4, levelId: 2, entityId: 2);

        AssertRejected(
            processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);

        player.PositionY = 0;
        AssertRejected(
            processor.Execute(player, new DiabloUsePortalIntent(5), 2, [player, otherPlayer]),
            DiabloRuleFailure.NotAllowed);

        otherPlayer.PositionX = 2;
        player.StatusEffects.Add(new AuthoritativeStatusEffect(7, 30, 2));
        player.StatusEffects.Add(new AuthoritativeStatusEffect(9, 40, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Infravision,
        });
        player.StatusEffects.Add(new AuthoritativeStatusEffect(46, 50, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Search,
        });
        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 3, [player, otherPlayer]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.Contains(2U, player.VisitedLevelIds);
        Assert.Equal(3, player.PositionX);
        Assert.Equal(4, player.PositionY);
        var retainedStatus = Assert.Single(player.StatusEffects);
        Assert.Equal(7U, retainedStatus.EffectId);
    }

    [Fact]
    public void TownPortalUsesTheValidatedPlacementRadiusAndLifetime()
    {
        var world = new DiabloWorldState(fallbackWidth: 9, fallbackHeight: 9);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 2, positionY: 2, levelId: 1, entityId: 1);
        var blockedPositions = new[] {
            (4, 4), (3, 3), (4, 3), (5, 3),
            (3, 4),           (5, 4),
            (3, 5), (4, 5), (5, 5),
        };
        var blockers = blockedPositions
            .Select((position, index) => CreatePlayer(
                positionX: position.Item1,
                positionY: position.Item2,
                levelId: 1,
                entityId: (uint)(10 + index)))
            .ToArray();
        var participants = new[] { player }.Concat(blockers).ToArray();

        var tooSmall = processor.ExecuteTownPortal(
            player,
            4,
            4,
            1,
            participants,
            searchRadius: 1,
            lifetime: 7);

        Assert.True(tooSmall.Succeeded);
        Assert.Empty(world.Portals);

        var sufficient = processor.ExecuteTownPortal(
            player,
            4,
            4,
            1,
            participants,
            searchRadius: 2,
            lifetime: 7);

        Assert.True(sufficient.Succeeded);
        var portal = Assert.Single(world.Portals.Values);
        Assert.Equal(7U, portal.RemainingTicks);
        Assert.Equal(2, Math.Max(
            Math.Abs(portal.SourcePositionX - 4),
            Math.Abs(portal.SourcePositionY - 4)));

        var malformed = processor.ExecuteTownPortal(
            player,
            4,
            4,
            1,
            participants,
            destinationPositionX: -1);

        Assert.False(malformed.Succeeded);
        Assert.Equal(DiabloRuleFailure.Malformed, malformed.Failure);
    }

    [Fact]
    public void TownPortalRejectsWhenNativePortalPoolIsFull()
    {
        var portals = Enumerable.Range(1, AuthoritativePortalRules.NativeMaximumPortalCount)
            .Select(id => new AuthoritativePortal((uint)id, 0, id, 0, 1, id, 1))
            .ToArray();
        var world = new DiabloWorldState(portals: portals);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 2, positionY: 2, levelId: 1, entityId: 5);

        var result = processor.ExecuteTownPortal(
            player,
            4,
            4,
            1,
            [player],
            searchRadius: 2,
            lifetime: 7);

        AssertRejected(result, DiabloRuleFailure.NotAllowed);
        Assert.Equal(
            AuthoritativePortalRules.NativeMaximumPortalCount,
            world.Portals.Count);
    }

    [Fact]
    public void TownPortalUsesTheExternalTownLevelIdentity()
    {
        var world = new DiabloWorldState(fallbackWidth: 80, fallbackHeight: 80);
        var combatRules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            townLevelId: 5);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: combatRules);
        var player = CreatePlayer(positionX: 10, positionY: 10, levelId: 1, entityId: 1);

        var created = processor.ExecuteTownPortal(
            player,
            12,
            10,
            1,
            [player],
            searchRadius: 2,
            lifetime: 7);

        Assert.True(created.Succeeded);
        var portal = Assert.Single(world.Portals.Values);
        Assert.Equal(5U, portal.DestinationLevelId);

        player.LevelId = 5;
        var rejected = processor.ExecuteTownPortal(
            player,
            12,
            10,
            2,
            [player]);

        AssertRejected(rejected, DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void EnteringQuestRouteLevelActivatesInitialQuestFromExternalMetadata()
    {
        var portal = new AuthoritativePortal(5, 1, 0, 0, 2, 3, 4);
        var quest = new AuthoritativeQuestState(3001, 2, 1, active: false);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3001, 0, "The Magic Rock", 2, -1, string.Empty, 0, true),
        ]);
        var world = new DiabloWorldState(portals: [portal], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);

        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.True(quest.Active);
    }

    [Fact]
    public void EnteringSkeletonKingRouteActivatesItsAuthoredQuest()
    {
        var portal = new AuthoritativePortal(5, 1, 0, 0, 3, 3, 4);
        var quest = new AuthoritativeQuestState(
            3013,
            3,
            1,
            active: false,
            completionMonsterId: 52,
            routeActivationVariable1: 1);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3013, 3, "The Curse of King Leoric", 3, -1, "DTYPE_CATHEDRAL", 1, false),
        ]);
        var world = new DiabloWorldState(portals: [portal], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);

        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(3U, player.LevelId);
        Assert.True(quest.Active);
        Assert.Equal(1, quest.Variable1);
        Assert.Equal(52U, quest.CompletionMonsterId);
    }

    [Fact]
    public void EnteringMaterializedSkeletonKingSetLevelActivatesItsAuthoredQuest()
    {
        var setLevel = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
            setLevelId: 1,
            activeMap: true);
        var portal = new AuthoritativePortal(5, 1, 0, 0, setLevel, 3, 4);
        var quest = new AuthoritativeQuestState(
            3013,
            3,
            1,
            active: false,
            completionMonsterId: 52,
            routeActivationVariable1: 1);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3013, 3, "The Curse of King Leoric", 3, -1, "DTYPE_CATHEDRAL", 1, false),
        ]);
        var world = new DiabloWorldState(portals: [portal], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);

        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(setLevel, player.LevelId);
        Assert.Equal((3, 4), (player.PositionX, player.PositionY));
        Assert.True(quest.Active);
        Assert.Equal(1, quest.Variable1);
    }

    [Fact]
    public void EnteringMaterializedSetLevelPreMapDoesNotActivateItsAuthoredQuest()
    {
        var preMap = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
            setLevelId: 1,
            activeMap: false);
        var portal = new AuthoritativePortal(5, 1, 0, 0, preMap, 3, 4);
        var quest = new AuthoritativeQuestState(3013, 3, 1, active: false);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3013, 3, "The Curse of King Leoric", 3, -1, "DTYPE_CATHEDRAL", 1, false),
        ]);
        var world = new DiabloWorldState(portals: [portal], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);

        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(preMap, player.LevelId);
        Assert.False(quest.Active);
        Assert.Equal(0, quest.Variable1);
    }

    [Fact]
    public void MultiplayerRouteDoesNotActivateSinglePlayerOnlyQuest()
    {
        var portal = new AuthoritativePortal(5, 1, 0, 0, 2, 3, 4);
        var quest = new AuthoritativeQuestState(3001, 2, 1, active: false);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3001, 0, "The Magic Rock", 2, 2, string.Empty, 0, true),
        ]);
        var world = new DiabloWorldState(portals: [portal], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            gameMode: DiabloGameMode.Multiplayer);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);

        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.False(quest.Active);
    }

    [Fact]
    public void WarpSelectsNearestTriggerAndCrawlsAroundOccupiedDestination()
    {
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(new AuthoritativeLevel(1, 5, 5));
        authoritativeWorld.AddLevel(new AuthoritativeLevel(2, 5, 5));
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(7, 1, 1, 1, 2, 2, 2));
        var blocker = CreatePlayer(positionX: 2, positionY: 2, levelId: 2, entityId: 2);
        var world = new DiabloWorldState(authoritativeWorld);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);
        player.StatusEffects.Add(new AuthoritativeStatusEffect(9, 20, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Infravision,
        });
        player.StatusEffects.Add(new AuthoritativeStatusEffect(46, 30, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Search,
        });

        var result = processor.ExecuteWarp(player, 12, [player, blocker]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.NotEqual((2, 2), (player.PositionX, player.PositionY));
        Assert.True(authoritativeWorld.IsWalkable(player.LevelId, player.PositionX, player.PositionY));
        Assert.Empty(player.StatusEffects);
    }

    [Fact]
    public void WarpUsesTheValidatedDestinationSearchRadius()
    {
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(new AuthoritativeLevel(1, 7, 7));
        authoritativeWorld.AddLevel(new AuthoritativeLevel(2, 7, 7));
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(7, 1, 1, 1, 2, 3, 3));
        var world = new DiabloWorldState(authoritativeWorld);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);
        var blockedPositions = new[] {
            (3, 3), (2, 2), (3, 2), (4, 2),
            (2, 3),           (4, 3),
            (2, 4), (3, 4), (4, 4),
        };
        var blockers = blockedPositions
            .Select((position, index) => CreatePlayer(
                positionX: position.Item1,
                positionY: position.Item2,
                levelId: 2,
                entityId: (uint)(10 + index)))
            .ToArray();
        var participants = new[] { player }.Concat(blockers).ToArray();

        var tooSmall = processor.ExecuteWarp(
            player,
            12,
            participants,
            searchRadius: 1);

        AssertRejected(tooSmall, DiabloRuleFailure.NotAllowed);
        Assert.Equal(1U, player.LevelId);

        var sufficient = processor.ExecuteWarp(
            player,
            12,
            participants,
            searchRadius: 2);

        Assert.True(sufficient.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(2, Math.Max(
            Math.Abs(player.PositionX - 3),
            Math.Abs(player.PositionY - 3)));
    }

    [Fact]
    public void QuestGatedWarpRequiresCompletionBeforeLeavingTown()
    {
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(new AuthoritativeLevel(0, 40, 40));
        authoritativeWorld.AddLevel(new AuthoritativeLevel(17, 40, 40));
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(
            2,
            0,
            36,
            22,
            17,
            36,
            24,
            requiredQuestId: 3017,
            requiredQuestCompleted: true));
        var quest = new AuthoritativeQuestState(3017, 17, 1, active: true);
        var world = new DiabloWorldState(authoritativeWorld, quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 36, positionY: 22, levelId: 0, entityId: 1);

        var spellWarp = processor.ExecuteWarp(player, 12, [player]);
        AssertRejected(spellWarp, DiabloRuleFailure.NotAllowed);

        var locked = processor.ExecuteLevelTrigger(player, 12, [player]);

        AssertRejected(locked, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0U, player.LevelId);

        quest.Completed = true;
        quest.Active = false;
        player.PositionX = 35;
        var opened = processor.Execute(
            player,
            new DiabloMoveIntent(1, 0),
            12,
            [player]);

        Assert.True(opened.Succeeded);
        Assert.Equal(17U, player.LevelId);
        Assert.Equal((36, 24), (player.PositionX, player.PositionY));
    }

    [Fact]
    public void PickupPlacesMultiCellItemAndRemovesItFromWorld()
    {
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            InventoryWidth = 2,
            InventoryHeight = 1,
        };
        var item = new AuthoritativeWorldItem(20, 0, 1, 0, 42, 75, itemState);
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        var ownedItem = Assert.Single(player.Inventory);
        Assert.Equal(42U, ownedItem.ItemSeed);
        Assert.Equal(27UL, ownedItem.PurchasedAtTick);
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Equal(0, player.InventoryGrid[1]);
        Assert.DoesNotContain(20U, world.WorldItems.Keys);
    }

    [Fact]
    public void PickupResolvesFootprintByMiscIdWhenWorldStateUsesDefaultDimensions()
    {
        var definition = new AuthoritativeItemDefinition(
            7,
            1,
            100,
            150,
            3,
            7,
            0,
            20,
            40,
            2,
            1,
            true) {
            NativeItemIndex = 101,
            MiscId = 121,
        };
        var itemState = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            MiscId = 121,
        };
        var item = new AuthoritativeWorldItem(20, 0, 1, 0, 42, 75, itemState);
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: new AuthoritativeItemCatalog([definition]));
        var player = CreatePlayer(entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        var ownedItem = Assert.Single(player.Inventory);
        Assert.Equal(2, ownedItem.State.InventoryWidth);
        Assert.Equal(1, ownedItem.State.InventoryHeight);
        Assert.Equal(100, ownedItem.State.Value);
        Assert.Equal(150, ownedItem.State.IdentifiedValue);
        Assert.Equal(3, ownedItem.State.MinDamage);
        Assert.Equal(7, ownedItem.State.MaxDamage);
        Assert.Equal(20, ownedItem.State.Durability);
        Assert.Equal(40, ownedItem.State.MaxDurability);
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Equal(0, player.InventoryGrid[1]);
    }

    [Fact]
    public void PickupGoldBypassesInventorySpaceAndRestoresServerOwnedBalance()
    {
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = 11,
                ItemIndex = 0,
                Value = 75,
                IdentifiedValue = 75,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(
            entityId: 1,
            inventoryGrid: Enumerable.Repeat(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(175U, player.Gold);
        Assert.Empty(player.Inventory);
        Assert.DoesNotContain(20U, world.WorldItems.Keys);
    }

    [Fact]
    public void PickupGoldHydratesOmittedValueFieldsFromWorldPrice()
    {
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = 11,
                ItemIndex = 0,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(175U, player.Gold);
        Assert.Empty(player.Inventory);
        Assert.DoesNotContain(20U, world.WorldItems.Keys);
    }

    [Fact]
    public void PickupGoldUsesNativeTopologyAndPlacementOrderWhenStacksArePresent()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var worldGold = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = rules.DeathGoldItemType,
                ItemIndex = rules.DeathGoldItemIndex,
                Value = 75,
                IdentifiedValue = 75,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [worldGold]);
        var random = new DiabloRandom(0);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            random: random);
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var fullStack = new OwnedStoreItem(
            0,
            0,
            123,
            5000,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = rules.DeathGoldItemType,
                ItemIndex = rules.DeathGoldItemIndex,
                Value = 5000,
                IdentifiedValue = 5000,
                StatFlag = true,
            });
        var player = CreatePlayer(
            entityId: 1,
            gold: 5000,
            inventory: [fullStack],
            inventoryGrid: inventoryGrid);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(5075U, player.Gold);
        Assert.Equal([5000, 75], player.Inventory.Select(item => item.State.Value).ToArray());
        Assert.Equal(1U, player.Inventory[1].ItemSeed);
        Assert.Equal(1, player.InventoryGrid[39]);
        Assert.Equal(1U, random.State);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void TopologyAwareGoldPickupMovesFullOverflowToHeldItem()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var worldGold = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = rules.DeathGoldItemType,
                ItemIndex = rules.DeathGoldItemIndex,
                Value = 75,
                IdentifiedValue = 75,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [worldGold]);
        var random = new DiabloRandom(0);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            random: random);
        var inventory = Enumerable.Range(0, 40)
            .Select(index => index == 0
                ? new OwnedStoreItem(
                    0,
                    0,
                    123,
                    5000,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = rules.DeathGoldItemType,
                        ItemIndex = rules.DeathGoldItemIndex,
                        Value = 5000,
                        IdentifiedValue = 5000,
                        StatFlag = true,
                    })
                : new OwnedStoreItem(
                    0,
                    0,
                    checked((uint)(123 + index)),
                    1,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = 1,
                        StatFlag = true,
                    }))
            .ToArray();
        var player = CreatePlayer(
            entityId: 1,
            gold: 5000,
            inventory: inventory,
            inventoryGrid: Enumerable.Range(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(5000U, player.Gold);
        Assert.Equal(5000, player.Inventory[0].State.Value);
        Assert.Equal(0U, random.State);
        Assert.Empty(world.WorldItems);
        Assert.Equal(42U, player.HeldItem?.ItemSeed);
        Assert.Equal(75U, player.HeldItem?.Price);
        Assert.Equal(75, player.HeldItem?.State.Value);
    }

    [Fact]
    public void TopologyAwareGoldPickupPartiallyFillsInventoryAndHoldsRemainder()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var worldGold = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            CreateGoldState(75, rules));
        var fillerState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            StatFlag = true,
        };
        var inventory = new List<OwnedStoreItem> {
            CreateGoldStack(123, 4990, rules),
        };
        for (var index = 1; index < 40; index++) {
            inventory.Add(new OwnedStoreItem(
                0,
                0,
                checked((uint)(200 + index)),
                1,
                0,
                fillerState));
        }
        var world = new DiabloWorldState(worldItems: [worldGold]);
        var random = new DiabloRandom(0);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            random: random);
        var player = CreatePlayer(
            entityId: 1,
            gold: 4990,
            inventory: inventory,
            inventoryGrid: Enumerable.Range(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(5000U, player.Gold);
        Assert.Equal(5000U, player.Inventory[0].Price);
        Assert.Equal(42U, player.HeldItem?.ItemSeed);
        Assert.Equal(65U, player.HeldItem?.Price);
        Assert.Equal(27UL, player.HeldItem?.PurchasedAtTick);
        Assert.Equal(0U, random.State);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void HeldItemDropValidatesTargetAndReturnsItemToWorld()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var heldItem = CreateGoldStack(42, 65, rules) with { PurchasedAtTick = 27 };
        var world = new DiabloWorldState();
        var processor = new DiabloWorldCommandProcessor(world, combatRules: rules);
        var player = CreatePlayer(entityId: 1, gold: 0, heldItem: heldItem);

        var rejected = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(2, 0),
            28,
            [player]);

        AssertRejected(rejected, DiabloRuleFailure.NotAllowed);
        Assert.Same(heldItem, player.HeldItem);
        Assert.Empty(world.WorldItems);

        var accepted = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(1, 0),
            29,
            [player]);

        Assert.True(accepted.Succeeded);
        Assert.Null(player.HeldItem);
        var dropped = Assert.Single(world.WorldItems).Value;
        Assert.Equal(0x40000000U, dropped.EntityId);
        Assert.Equal(42U, dropped.ItemSeed);
        Assert.Equal(65U, dropped.Price);
        Assert.Equal((1, 0), (dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void HeldItemDropRejectsOutOfBoundsCoordinatesBeforeDistanceArithmetic()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var heldItem = CreateGoldStack(42, 65, rules);
        var world = new DiabloWorldState(fallbackWidth: 8, fallbackHeight: 8);
        var processor = new DiabloWorldCommandProcessor(world, combatRules: rules);
        var player = CreatePlayer(entityId: 1, gold: 0, heldItem: heldItem);

        var result = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(int.MinValue, 0),
            1,
            [player]);

        AssertRejected(result, DiabloRuleFailure.NotAllowed);
        Assert.Same(heldItem, player.HeldItem);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void HeldItemDropUsesTheReservedMovementDestination()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var heldItem = CreateGoldStack(42, 65, rules);
        var world = new DiabloWorldState();
        var processor = new DiabloWorldCommandProcessor(world, combatRules: rules);
        var player = CreatePlayer(entityId: 1, gold: 0, heldItem: heldItem);
        player.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Move,
            1,
            2,
            2,
            2) {
            DirectionX = 1,
        };

        var result = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(2, 0),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Null(player.HeldItem);
        var dropped = Assert.Single(world.WorldItems).Value;
        Assert.Equal((2, 0), (dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void HeldItemDropCompletesAContentOwnedWorldItemAnchorQuest()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var quest = new AuthoritativeQuestState(3023, 21, 1, active: true);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 14,
                WorldItemAnchorPositionY = 14,
            },
        ]);
        var world = new DiabloWorldState(
            fallbackWidth: 40,
            fallbackHeight: 40,
            quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            positionX: 13,
            positionY: 14,
            levelId: 21,
            entityId: 1,
            gold: 0,
            heldItem: CreateGoldStack(42, 65, rules));

        var result = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(14, 14),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, quest.Progress);
        var dropped = Assert.Single(world.WorldItems.Values);
        Assert.Equal((21U, 14, 14), (dropped.LevelId, dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void StableInventoryItemDropCompletesAContentOwnedWorldItemAnchorQuest()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var quest = new AuthoritativeQuestState(3023, 21, 1, active: true);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 14,
                WorldItemAnchorPositionY = 14,
            },
        ]);
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            75,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemIndex = 1,
                Value = 75,
                IdentifiedValue = 75,
                StableItemId = 7001,
            });
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var world = new DiabloWorldState(
            fallbackWidth: 40,
            fallbackHeight: 40,
            quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            positionX: 13,
            positionY: 14,
            levelId: 21,
            entityId: 1,
            gold: 0,
            inventory: [item],
            inventoryGrid: inventoryGrid);

        var result = processor.Execute(
            player,
            new DiabloDropItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    StableItemId: 7001),
                14,
                14),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        var dropped = Assert.Single(world.WorldItems).Value;
        Assert.Equal((21U, 14, 14, 7001U), (
            dropped.LevelId,
            dropped.PositionX,
            dropped.PositionY,
            dropped.State.StableItemId));
    }

    [Fact]
    public void StableInventoryItemDropRejectsAStaleReferenceWithoutMutation()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            75,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemIndex = 1,
                Value = 75,
                IdentifiedValue = 75,
                StableItemId = 7001,
            });
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var world = new DiabloWorldState(fallbackWidth: 40, fallbackHeight: 40);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            entityId: 1,
            inventory: [item],
            inventoryGrid: inventoryGrid);

        var result = processor.Execute(
            player,
            new DiabloDropItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    StableItemId: 7002),
                1,
                0),
            1,
            [player]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Single(player.Inventory);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void HeldItemDropDoesNotCompleteAnInactiveWorldItemAnchorQuest()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var quest = new AuthoritativeQuestState(
            3023,
            21,
            1,
            active: false,
            activateOnRoute: false);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 14,
                WorldItemAnchorPositionY = 14,
            },
        ]);
        var world = new DiabloWorldState(
            fallbackWidth: 40,
            fallbackHeight: 40,
            quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            positionX: 13,
            positionY: 14,
            levelId: 21,
            entityId: 1,
            gold: 0,
            heldItem: CreateGoldStack(42, 65, rules));

        var result = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(14, 14),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(0U, quest.Progress);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void AnchoredWorldItemAndQuestCompletionRoundTripThroughSnapshot()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var quest = new AuthoritativeQuestState(3023, 21, 1, active: true);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 14,
                WorldItemAnchorPositionY = 14,
            },
        ]);
        var world = new DiabloWorldState(fallbackWidth: 40, fallbackHeight: 40, quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            positionX: 13,
            positionY: 14,
            levelId: 21,
            entityId: 1,
            gold: 0,
            heldItem: CreateGoldStack(42, 65, rules));

        Assert.True(processor.Execute(
            player,
            new DiabloDropHeldItemIntent(14, 14),
            1,
            [player]).Succeeded);

        var simulation = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            FallbackWidth = 40,
            FallbackHeight = 40,
            WorldItems = world.WorldItems.Values.ToArray(),
            Quests = world.Quests.Values.ToArray(),
        });
        var snapshot = simulation.CaptureSnapshot(player, 1);
        var restored = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules));
        restored.RestoreSnapshot(CreatePlayer(entityId: 1), snapshot);

        var restoredQuest = Assert.Single(restored.WorldState.Quests).Value;
        Assert.True(restoredQuest.Completed);
        Assert.False(restoredQuest.Active);
        var restoredItem = Assert.Single(restored.WorldState.WorldItems).Value;
        Assert.Equal((21U, 14, 14, 42U, 65U), (
            restoredItem.LevelId,
            restoredItem.PositionX,
            restoredItem.PositionY,
            restoredItem.ItemSeed,
            restoredItem.Price));
    }

    [Fact]
    public void MultiplayerDropDoesNotCompleteSinglePlayerWorldItemAnchorQuest()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var quest = new AuthoritativeQuestState(3023, 21, 1, active: true);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 14,
                WorldItemAnchorPositionY = 14,
            },
        ]);
        var world = new DiabloWorldState(fallbackWidth: 40, fallbackHeight: 40, quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            questTypes: questTypes,
            gameMode: DiabloGameMode.Multiplayer);
        var player = CreatePlayer(
            positionX: 13,
            positionY: 14,
            levelId: 21,
            entityId: 1,
            gold: 0,
            heldItem: CreateGoldStack(42, 65, rules));

        var result = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(14, 14),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(quest.Completed);
        Assert.True(quest.Active);
    }

    [Fact]
    public void MalformedGoldPickupDoesNotMutateBalanceOrWorld()
    {
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = 11,
                ItemIndex = 0,
                Value = 75,
                IdentifiedValue = 74,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        AssertRejected(result, DiabloRuleFailure.Malformed);
        Assert.Equal(100U, player.Gold);
        Assert.Empty(player.Inventory);
        Assert.Same(item, world.WorldItems[20]);
    }

    [Fact]
    public void PickupUsesHeldCursorWhenInventoryHasNoPlacement()
    {
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with { ItemType = 1 });
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1, inventoryGrid: Enumerable.Repeat(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(item.ItemSeed, player.HeldItem?.ItemSeed);
        Assert.Equal(item.Price, player.HeldItem?.Price);
        Assert.Equal(item.State, player.HeldItem?.State);
        Assert.DoesNotContain(20U, world.WorldItems.Keys);
    }

    [Fact]
    public void ObjectAppliesLinkedQuestAndEmitsTransportNeutralEvent()
    {
        var quest = new AuthoritativeQuestState(30, 0, 1);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.Heal,
            effectAmount: 25);
        var world = new DiabloWorldState(objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(life: 30, lifeMaximum: 50, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.True(quest.Completed);
        Assert.Equal(50, player.Life);
        var gameplayEvent = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Healing, gameplayEvent.Kind);
        Assert.Equal(20U, gameplayEvent.SourceEntityId);
        Assert.Equal(7U, gameplayEvent.TargetEntityId);
        Assert.Equal(20, gameplayEvent.Amount);
    }

    [Fact]
    public void ObjectInteractionRejectsWrappedExtremeDistanceWithoutMutation()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            int.MinValue,
            0);
        var world = new DiabloWorldState(objects: [@object]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: int.MaxValue, entityId: 7);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloOperateObjectIntent(@object.EntityId),
                5,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(@object.Activated);
    }

    [Fact]
    public void ArmedTrapUsesNativeHalfTrapDamageEquipmentAndPreservesQ6EventAmount()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.Damage,
            effectAmount: 64,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(
            life: 100,
            lifeMaximum: 100,
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag,
                })]);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(68, player.Life);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 20, 7, 0) {
                AmountQ6 = 32,
            },
            Assert.Single(result.Events));
    }

    [Fact]
    public void LinkedObjectAppliesConfiguredQuestProgressAndClampsAtRequiredAmount()
    {
        var quest = new AuthoritativeQuestState(30, 0, 5, progress: 4);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            questId: 30,
            questProgressAmount: 3);
        var world = new DiabloWorldState(objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]).Succeeded);

        Assert.Equal(5U, quest.Progress);
        Assert.True(quest.Completed);
    }

    [Fact]
    public void QuestSequenceObjectsRequireNativeOrderAndPersistCompletion()
    {
        var quest = new AuthoritativeQuestState(
            3022,
            0,
            1,
            completionVariable1: 3,
            completionVariable2: 4,
            completionActivateQuestId: 3023);
        var nextQuest = new AuthoritativeQuestState(3023, 0, 1, active: false);
        var objects = new[] {
            new AuthoritativeWorldObject(
                2401,
                6001,
                0,
                1,
                0,
                questId: 3022,
                effectKind: AuthoritativeObjectEffectKind.QuestSequence,
                effectAmount: 1),
            new AuthoritativeWorldObject(
                2402,
                6002,
                0,
                1,
                0,
                questId: 3022,
                effectKind: AuthoritativeObjectEffectKind.QuestSequence,
                effectAmount: 2),
            new AuthoritativeWorldObject(
                2403,
                6003,
                0,
                1,
                0,
                questId: 3022,
                effectKind: AuthoritativeObjectEffectKind.QuestSequence,
                effectAmount: 3),
        };
        var world = new DiabloWorldState(objects: objects, quests: [quest, nextQuest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer();

        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2402),
            1,
            [player]).Succeeded);
        Assert.Equal(0, quest.Variable1);
        Assert.False(quest.Completed);

        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2401),
            2,
            [player]).Succeeded);
        Assert.Equal(1, quest.Variable1);
        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2402),
            3,
            [player]).Succeeded);
        Assert.Equal(2, quest.Variable1);
        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2403),
            4,
            [player]).Succeeded);

        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, quest.Progress);
        Assert.Equal(3, quest.Variable1);
        Assert.Equal(4, quest.Variable2);
        Assert.True(nextQuest.Active);
        Assert.All(objects, @object => Assert.False(@object.Activated));
    }

    [Fact]
    public void QuestSequenceObjectProjectsItsAuthoredQuestMessage()
    {
        var quest = new AuthoritativeQuestState(
            3012,
            13,
            1,
            messageId: 110,
            active: false);
        var @object = new AuthoritativeWorldObject(
            2404,
            6004,
            13,
            20,
            20,
            questId: 3012,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 1);
        var world = new DiabloWorldState(objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            levelId: 13,
            positionX: 19,
            positionY: 20);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(2404),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var dialogue = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Dialogue, dialogue.Kind);
        Assert.Equal(110, dialogue.MessageId);
        Assert.Equal(3012U, dialogue.QuestId);
        Assert.Equal(2404U, dialogue.TargetEntityId);
        Assert.Equal(1, quest.Variable1);
        Assert.True(quest.Active);
        Assert.False(@object.Activated);
    }

    [Fact]
    public void QuestDialogueValidatesUniqueIdentityActivatesQuestAndEmitsMessage()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            active: false,
            messageId: 77,
            completionUniqueIdentity: "Gharbad",
            dialogueVariable1: 1,
            dialogueVariable2: 2);
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Gharbad");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3003, 20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal((1, 2), (quest.Variable1, quest.Variable2));
        var dialogue = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Dialogue, dialogue.Kind);
        Assert.Equal(7U, dialogue.SourceEntityId);
        Assert.Equal(20U, dialogue.TargetEntityId);
        Assert.Equal(3003U, dialogue.QuestId);
        Assert.Equal(77, dialogue.MessageId);
        Assert.Equal(0, dialogue.Amount);
    }

    [Fact]
    public void QuestDialogueDoesNotRegressNativeDialogueVariablesOnRetry()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            variable1: 3,
            dialogueVariable1: 1,
            completionUniqueIdentity: "Gharbad");
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Gharbad");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        Assert.True(processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3003, 20),
            5,
            [player]).Succeeded);

        Assert.Equal(3, quest.Variable1);
    }

    [Fact]
    public void QuestDialogueAdvancesAuthoredSubsequentNativeVariableOnRetry()
    {
        var quest = new AuthoritativeQuestState(
            3012,
            13,
            1,
            active: false,
            messageId: 110,
            completionUniqueIdentity: "Warlord",
            dialogueVariable1: 2,
            dialogueCompletionVariable1: 3,
            completionVariable1: 3);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 13,
            unique: true,
            uniqueIdentity: "Warlord");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 13,
            entityId: 7);

        Assert.True(processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3012, 20),
            5,
            [player]).Succeeded);
        Assert.Equal(2, quest.Variable1);

        var secondConversation = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3012, 20),
            6,
            [player]);

        Assert.True(secondConversation.Succeeded);
        Assert.Equal(3, quest.Variable1);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(110, Assert.Single(secondConversation.Events).MessageId);
    }

    [Fact]
    public void StagedWarlordDialogueUsesAuthoredTransitionsAndKeepsCombatGateOpen()
    {
        var quest = new AuthoritativeQuestState(
            3012,
            13,
            1,
            active: false,
            completionUniqueIdentity: "Warlord",
            dialogueCompletionVariable1: 3);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 13,
            unique: true,
            uniqueIdentity: "Warlord");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(3012, 0, -1, 110, 2, 0),
            new(3012, 2, -1, 110, 3, 0),
        ]);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questDialogueStages: stages);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 13,
            entityId: 7);

        var first = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3012, 20),
            5,
            [player]);
        var firstVariable1 = quest.Variable1;
        var second = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3012, 20),
            6,
            [player]);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, firstVariable1);
        Assert.Equal(110, Assert.Single(first.Events).MessageId);
        Assert.Equal(
            AuthoritativeMonsterDialogueState.Inquiring,
            target.DialogueState);
        Assert.Equal(3, quest.Variable1);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(110, Assert.Single(second.Events).MessageId);
        Assert.Equal(
            AuthoritativeMonsterDialogueState.Combat,
            target.DialogueState);
    }

    [Fact]
    public void StagedQuestDialogueAppliesAuthoredMessagesAndVariableTransitions()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            active: false,
            completionUniqueIdentity: "Gharbad",
            dialogueCompletionVariable1: 5);
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Gharbad");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(3003, 0, -1, 144, 1, 0, NextMessageId: 222),
            new(3003, 1, -1, 145, 3, 0),
            new(3003, 3, -1, 146, 4, 0),
            new(3003, 4, -1, 147, 5, 0),
        ]);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questDialogueStages: stages);
        var player = CreatePlayer(entityId: 7);

        foreach (var expected in new[] {
            (MessageId: 144, Variable1: 1, NextMessageId: 222),
            (MessageId: 145, Variable1: 3, NextMessageId: 146),
            (MessageId: 146, Variable1: 4, NextMessageId: 147),
            (MessageId: 147, Variable1: 5, NextMessageId: 0),
        }) {
            var result = processor.Execute(
                player,
                new DiabloTalkToQuestIntent(3003, 20),
                (ulong)expected.Variable1,
                [player]);

            Assert.True(result.Succeeded, result.Failure?.ToString());
            Assert.Equal(expected.Variable1, quest.Variable1);
            Assert.Equal(expected.MessageId, Assert.Single(result.Events).MessageId);
            Assert.Equal(expected.NextMessageId, target.DialogueMessageId);
            Assert.True(quest.Active);
            Assert.False(quest.Completed);
        }
    }

    [Fact]
    public void StagedQuestDialogueAppliesValidatedWorldMutationAfterAcceptedStep()
    {
        var geometry = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n0\t4\t4\tnone\n");
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            active: false,
            completionUniqueIdentity: "Gharbad",
            dialogueCompletionVariable1: 2);
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Gharbad");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(
                3003,
                0,
                -1,
                144,
                1,
                0,
                FollowUpKind: AuthoritativeQuestDialogueFollowUpKind.WorldMutation,
                FollowUpAmount: 7),
        ]);
        var world = new DiabloWorldState(
            geometry,
            combatTargets: [target],
            quests: [quest],
            worldMutations: new AuthoritativeWorldMutationCatalog([
                new AuthoritativeWorldMutation(7, 0, blockedCellsAdd: [5]),
            ]));
        var processor = new DiabloWorldCommandProcessor(
            world,
            questDialogueStages: stages);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3003, 20),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(1, quest.Variable1);
        Assert.Contains(7U, world.ActiveWorldMutationIds);
        Assert.False(world.IsWalkable(0, 1, 1));
        Assert.Equal(144, Assert.Single(result.Events).MessageId);
    }

    [Fact]
    public void StagedDialogueRewardCreatesOneDeterministicWorldItem()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
                DropRate = 1,
            },
        ]);
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            active: false,
            completionUniqueIdentity: "Gharbad",
            dialogueCompletionVariable1: 5);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 0,
            monsterId: 1,
            monsterLevel: 1,
            unique: true,
            uniqueIdentity: "Gharbad");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(
                3003,
                1,
                -1,
                145,
                3,
                0,
                0,
                false,
                false,
                AuthoritativeQuestDialogueRewardKind.RandomUniqueMonsterDrop,
                0,
                1,
                1),
        ]);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questDialogueStages: stages);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            entityId: 7);
        var otherPlayer = CreatePlayer(
            positionX: 21,
            positionY: 21,
            entityId: 8);
        quest.Variable1 = 1;

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTalkToQuestIntent(3003, 20),
                1,
                [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Equal(1, quest.Variable1);
        Assert.Empty(world.WorldItems);

        otherPlayer.PositionX = 0;
        otherPlayer.PositionY = 0;
        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3003, 20),
            1,
            [player, otherPlayer]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal((21, 21), (reward.PositionX, reward.PositionY));
        Assert.Equal(7U, reward.State.StableItemId);
        Assert.Equal(3, quest.Variable1);
        Assert.Equal(145, Assert.Single(result.Events).MessageId);
    }

    [Fact]
    public void StagedNormalItemDialogueRewardWaitsForPlacementBeforeAdvancingZhar()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2203, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3004,
            8,
            1,
            active: false,
            messageId: 148,
            completionUniqueIdentity: "Zhar",
            completionVariable1: 3);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 8,
            monsterId: 3,
            monsterLevel: 8,
            unique: true,
            uniqueIdentity: "Zhar");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(
                3004,
                0,
                -1,
                148,
                2,
                0,
                RewardKind: AuthoritativeQuestDialogueRewardKind.NormalItem,
                RewardItemLevel: 8,
                RewardOffsetX: 1,
                RewardOffsetY: 1,
                RewardItemId: 2203),
        ]);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questDialogueStages: stages);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 8,
            entityId: 7);
        var blocker = CreatePlayer(positionX: 21, positionY: 21, levelId: 8, entityId: 8);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTalkToQuestIntent(3004, 20),
                1,
                [player, blocker]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Active);
        Assert.Equal(0, quest.Variable1);
        Assert.Empty(world.WorldItems);

        blocker.PositionX = 0;
        blocker.PositionY = 0;
        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3004, 20),
            2,
            [player, blocker]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(2, quest.Variable1);
        Assert.Equal(148, Assert.Single(result.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal((2203U, 21, 21), (
            reward.State.StableItemId,
            reward.PositionX,
            reward.PositionY));
    }

    [Fact]
    public void ItemGatedQuestDialogueConsumesTheItemAndCompletesTheQuest()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2136, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3008,
            4,
            1,
            variable1: 2,
            completionUniqueIdentity: "Snotspill");
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 4,
            unique: true,
            uniqueIdentity: "Snotspill");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(3008, 2, -1, 23, 3, 0, 2136, true, true),
        ]);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questDialogueStages: stages);
        var player = CreatePlayer(levelId: 4, entityId: 7);
        var sign = items.Generate(2136, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, sign.CreateInfo, 0, 0, sign));
        player.InventoryGrid[0] = 0;

        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3008, 20),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal((1U, 3), (quest.Progress, quest.Variable1));
        Assert.Empty(player.Inventory);
        Assert.Equal(23, Assert.Single(result.Events).MessageId);
    }

    [Fact]
    public void AuthoredInitialSnotspillDialogueUsesTheStagedRoute()
    {
        var quest = new AuthoritativeQuestState(
            3008,
            4,
            1,
            variable1: 0,
            completionUniqueIdentity: "Snotspill",
            messageId: 12,
            dialogueVariable1: 1);
        var target = new AuthoritativeCombatTarget(
            20,
            25,
            25,
            100,
            levelId: 4,
            unique: true,
            uniqueIdentity: "Snotspill");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(3008, 0, -1, 12, 1, 0),
            new(3008, 1, -1, 21, 2, 0, NextMessageId: 22),
        ]);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questDialogueStages: stages);
        var player = CreatePlayer(
            positionX: 24,
            positionY: 25,
            levelId: 4,
            entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3008, 20),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal((1, 0), (quest.Variable1, quest.Variable2));
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(21, target.DialogueMessageId);
        Assert.Equal(12, Assert.Single(result.Events).MessageId);
    }

    [Fact]
    public void SnotspillOpeningDialogueAppliesItsAuthoritativeTransparencyFollowUp()
    {
        var quest = new AuthoritativeQuestState(
            3008,
            4,
            1,
            variable1: 1,
            completionUniqueIdentity: "Snotspill");
        var target = new AuthoritativeCombatTarget(
            20,
            25,
            25,
            100,
            levelId: 4,
            unique: true,
            uniqueIdentity: "Snotspill");
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            4,
            40,
            40,
            nativeTransparency: []));
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                9301,
                4,
                anchorUniqueIdentity: "Snotspill",
                transparencyRectangles: [
                    new AuthoritativeWorldMutationTransparencyRectangle(-11, -9, 16, 8, 9),
                ],
                createTransparencyLayer: true),
        ]);
        var world = new DiabloWorldState(
            geometry,
            combatTargets: [target],
            quests: [quest],
            worldMutations: mutations);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questDialogueStages: new AuthoritativeQuestDialogueStageCatalog([
                new(
                    3008,
                    1,
                    -1,
                    21,
                    2,
                    0,
                    FollowUpKind: AuthoritativeQuestDialogueFollowUpKind.WorldMutation,
                    FollowUpAmount: 9301,
                    NextMessageId: 22),
            ]));
        var player = CreatePlayer(positionX: 24, positionY: 25, levelId: 4, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3008, 20),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(2, quest.Variable1);
        Assert.True(quest.Active);
        Assert.Contains(9301U, world.ActiveWorldMutationIds);
        var projected = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)9, projected.NativeTransparency[16 * 40 + 14]);
        Assert.Equal(21, Assert.Single(result.Events).MessageId);
    }

    [Fact]
    public void SnotspillSignChestUsesItsQuestGateAndGeneratesTheBanner()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2136, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3008,
            4,
            1,
            variable1: 1,
            completionUniqueIdentity: "Snotspill");
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(4, 40, 40));
        var signChest = new AuthoritativeWorldObject(
            2002,
            97,
            4,
            10,
            10,
            questId: 3008,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.SetVariable1,
            questTransitionAmount: 2,
            questTransitionRequiredVariable1: 1,
            rewardKind: AuthoritativeObjectRewardKind.NormalItem,
            rewardId: 2136);
        var world = new DiabloWorldState(
            geometry,
            quests: [quest],
            objects: [signChest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 10, positionY: 9, levelId: 4, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(2002),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(signChest.Activated);
        Assert.Equal(2, quest.Variable1);
        var banner = Assert.Single(world.WorldItems.Values);
        Assert.Equal(2136U, banner.State.StableItemId);
    }

    [Fact]
    public void ObjectRewardRejectsAWorldItemTileOccupiedByAnotherParticipant()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2136, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var signChest = new AuthoritativeWorldObject(
            2002,
            97,
            4,
            10,
            10,
            rewardKind: AuthoritativeObjectRewardKind.NormalItem,
            rewardId: 2136);
        var world = new DiabloWorldState(objects: [signChest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 10, positionY: 9, levelId: 4, entityId: 7);
        var otherPlayer = CreatePlayer(positionX: 10, positionY: 10, levelId: 4, entityId: 8);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloOperateObjectIntent(signChest.EntityId),
                1,
                [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(signChest.Activated);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void ItemGatedQuestDialogueAppliesItsWorldMutationFollowUp()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2136, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3008,
            4,
            1,
            variable1: 2,
            completionUniqueIdentity: "Snotspill");
        var target = new AuthoritativeCombatTarget(
            20,
            25,
            25,
            100,
            levelId: 4,
            unique: true,
            uniqueIdentity: "Snotspill");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                9300,
                4,
                anchorUniqueIdentity: "Snotspill",
                megaTileAssignments: [
                    new AuthoritativeWorldMutationMegaTileAssignment(0, 0, 136),
                ],
                anchorMegaOffsetX: -1,
                anchorMegaOffsetY: -1,
                transparencyRectangles: [
                    new AuthoritativeWorldMutationTransparencyRectangle(-1, -1, 1, 1, 9),
                ],
                createTransparencyLayer: true),
        ]);
        var worldGeometry = new AuthoritativeWorld();
        worldGeometry.AddLevel(new AuthoritativeLevel(
            4,
            40,
            40,
            nativeMegaTileLayer: Enumerable.Repeat<ushort>(0, 400)));
        var world = new DiabloWorldState(
            worldGeometry,
            combatTargets: [target],
            quests: [quest],
            worldMutations: mutations);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questDialogueStages: new AuthoritativeQuestDialogueStageCatalog([
                new(
                    3008,
                    2,
                    -1,
                    23,
                    3,
                    0,
                    2136,
                    true,
                    true,
                    FollowUpKind: AuthoritativeQuestDialogueFollowUpKind.WorldMutation,
                    FollowUpAmount: 9300),
            ]));
        var player = CreatePlayer(positionX: 24, positionY: 25, levelId: 4, entityId: 7);
        var sign = items.Generate(2136, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, sign.CreateInfo, 0, 0, sign));
        player.InventoryGrid[0] = 0;

        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3008, 20),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Contains(9300U, world.ActiveWorldMutationIds);
        var projected = Assert.Single(world.LevelGeometry);
        Assert.Equal((ushort)136, projected.NativeMegaTileLayer[12 * 20 + 12]);
        Assert.Equal((ushort)9, projected.NativeTransparency[24 * 40 + 24]);
    }

    [Fact]
    public void ContentOwnedQuestTownerStagesShareOneQuestAndConsumeBeltItems()
    {
        var items = new AuthoritativeItemCatalog([
            CreateQuestItem(2139),
            CreateQuestItem(2140),
            CreateQuestItem(2141),
            CreateQuestItem(2142),
        ]);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(300201, 3002, 6, 0, 80, 20, 2141,
                AuthoritativeQuestTownerInteractionKind.Activate,
                Variable1: 2,
                MessageId: 129),
            new(300202, 3002, 6, 0, 80, 20, 2139,
                AuthoritativeQuestTownerInteractionKind.Advance,
                RequiredVariable1: 2,
                Variable1: 5,
                MessageId: 131),
            new(300203, 3002, 6, 0, 80, 20, 2140,
                AuthoritativeQuestTownerInteractionKind.Dialogue,
                RequiredVariable1: 5,
                MessageId: 132,
                ConsumeItem: false),
            new(300204, 3002, 6, 0, 80, 20, 2142,
                AuthoritativeQuestTownerInteractionKind.Complete,
                RequiredVariable1: 7,
                MessageId: 133),
        ]);
        var quest = new AuthoritativeQuestState(
            3002,
            9,
            1,
            active: false);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 79, positionY: 20);

        player.Inventory.Add(CreateQuestOwnedItem(items, 2141, 1));
        Assert.False(quest.Active);
        Assert.True(items.IsStateForDefinition(
            player.Inventory[0].State,
            items.GetRequired(2141)));
        var activation = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3002, 6),
            1,
            [player]);
        Assert.True(activation.Succeeded, activation.Failure?.ToString());
        Assert.Equal((true, 2, 129), (quest.Active, quest.Variable1, Assert.Single(activation.Events).MessageId));
        Assert.Empty(player.Inventory);

        player.Inventory.Add(CreateQuestOwnedItem(items, 2139, 2));
        var advancement = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3002, 6),
            2,
            [player]);
        Assert.True(advancement.Succeeded);
        Assert.Equal((true, 5, 131), (quest.Active, quest.Variable1, Assert.Single(advancement.Events).MessageId));

        player.Inventory.Add(CreateQuestOwnedItem(items, 2140, 3));
        var dialogue = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3002, 6),
            3,
            [player]);
        Assert.True(dialogue.Succeeded);
        Assert.Equal((true, 5, 132), (quest.Active, quest.Variable1, Assert.Single(dialogue.Events).MessageId));
        Assert.Single(player.Inventory);

        quest.Variable1 = 7;
        player.Inventory.Clear();
        player.Belt.Add(new BeltStoreItem(
            0,
            4,
            items.GetRequired(2142).Generate(4)));
        var completion = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3002, 6),
            4,
            [player]);
        Assert.True(completion.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, quest.Progress);
        Assert.Equal(133, Assert.Single(completion.Events).MessageId);
        Assert.Empty(player.Belt);
    }

    [Fact]
    public void ContentOwnedCompletionStageConsumesItemAndSpawnsQuestReward()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1004, "Empyrean Band", 113, 1, 8000, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(113, 1, 8000, 8000, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(2135, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var quest = new AuthoritativeQuestState(
            3001,
            1,
            3,
            active: true,
            variable2: 1,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 2135,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1004,
            turnInRewardItemLevel: 5,
            turnInRewardPositionX: 61,
            turnInRewardPositionY: 64,
            turnInMessageId: 117);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(
                300101,
                3001,
                0,
                0,
                62,
                63,
                2135,
                AuthoritativeQuestTownerInteractionKind.Complete,
                RequiredVariable2: 1,
                MessageId: 117,
                UseQuestTurnInReward: true),
        ]);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);
        var requiredItem = items.Generate(2135, 42);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 63,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, requiredItem.CreateInfo, 0, 0, requiredItem)],
            entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(117, Assert.Single(result.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1004, reward.State.UniqueId);
        Assert.Equal((61, 64), (reward.PositionX, reward.PositionY));
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3001, 0),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void ContentOwnedAdvanceStageSpawnsRewardOnlyAfterPlacementValidation()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2140, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(2142, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3002,
            9,
            1,
            active: true,
            variable1: 5,
            turnInTownerId: 1,
            turnInLevelId: 0,
            turnInPositionX: 55,
            turnInPositionY: 79,
            turnInItemId: 2140,
            turnInRewardItemLevel: 0,
            turnInRewardPositionX: 55,
            turnInRewardPositionY: 80,
            turnInRewardItemId: 2142,
            turnInMessageId: 124);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(
                300211,
                3002,
                1,
                0,
                55,
                79,
                2140,
                AuthoritativeQuestTownerInteractionKind.Advance,
                RequiredVariable1: 5,
                Variable1: 6,
                MessageId: 124,
                UseQuestTurnInReward: true),
        ]);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);
        var brain = items.Generate(2140, 42);
        var player = CreatePlayer(
            positionX: 54,
            positionY: 79,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, brain.CreateInfo, 0, 0, brain)],
            entityId: 7);
        var blocker = CreatePlayer(positionX: 55, positionY: 80, entityId: 8);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3002, 1),
                1,
                [player, blocker]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Single(player.Inventory);
        Assert.Equal(5, quest.Variable1);

        blocker.PositionX = 0;
        blocker.PositionY = 0;
        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3002, 1),
            2,
            [player, blocker]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal((6, 124), (quest.Variable1, Assert.Single(result.Events).MessageId));
        Assert.Empty(player.Inventory);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(2142U, reward.State.StableItemId);
        Assert.Equal((55, 80), (reward.PositionX, reward.PositionY));
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3002, 1),
                3,
                [player, blocker]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void ContentOwnedQuestTownerDialogueCanApplyAfterCompletionWithoutAnItem()
    {
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(300205, 3013, 3, 0, 55, 62, 0,
                AuthoritativeQuestTownerInteractionKind.Dialogue,
                RequiredVariable2: 1,
                Variable1: 2,
                Variable2: 2,
                MessageId: 3,
                ConsumeItem: false,
                AllowCompleted: true),
        ]);
        var quest = new AuthoritativeQuestState(
            3013,
            3,
            1,
            progress: 1,
            completed: true,
            active: false,
            variable2: 1);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 55, positionY: 62);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3013, 3),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal((2, 2), (quest.Variable1, quest.Variable2));
        Assert.Equal(3, Assert.Single(result.Events).MessageId);
    }

    [Fact]
    public void ContentOwnedQuestTownerActivationUsesServerOwnedVisitedLevelGate()
    {
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(300206, 3013, 3, 0, 55, 62, 0,
                AuthoritativeQuestTownerInteractionKind.Activate,
                Variable1: 1,
                Variable2: 1,
                MessageId: 2,
                ConsumeItem: false,
                RequiredVisitedLevel1: 2,
                RequiredVisitedLevel2: 4),
        ]);
        var quest = new AuthoritativeQuestState(
            3013,
            3,
            1,
            active: false);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 55, positionY: 62);

        var beforeVisit = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3013, 3),
            1,
            [player]);
        Assert.False(beforeVisit.Succeeded);
        Assert.False(quest.Active);

        player.MarkLevelVisited(4);
        var afterVisit = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3013, 3),
            2,
            [player]);
        Assert.True(afterVisit.Succeeded, afterVisit.Failure?.ToString());
        Assert.True(quest.Active);
        Assert.Equal((1, 1), (quest.Variable1, quest.Variable2));
        Assert.Equal(2, Assert.Single(afterVisit.Events).MessageId);
    }

    [Fact]
    public void HellfireLittleGirlUsesItemAbsenceForConversationAndDirectTheodoreHandIn()
    {
        var items = new AuthoritativeItemCatalog([
            CreateQuestItem(2102),
            CreateQuestItem(171),
        ]);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(301901, 3019, 11, 0, 77, 43, 0,
                AuthoritativeQuestTownerInteractionKind.Activate,
                Variable1: 1,
                MessageId: 283,
                ConsumeItem: false,
                RequiredVisitedLevel1: 17,
                RequireUnlogged: true,
                RequiredItemId: 2102,
                RequireItemPresent: false),
            new(301902, 3019, 11, 0, 77, 43, 0,
                AuthoritativeQuestTownerInteractionKind.Dialogue,
                RequiredVariable1: 1,
                MessageId: 284,
                ConsumeItem: false,
                RequiredItemId: 2102,
                RequireItemPresent: false),
        ]);
        var quest = new AuthoritativeQuestState(
            3019,
            17,
            1,
            active: false,
            logged: false,
            variable1: 0,
            turnInTownerId: 11,
            turnInLevelId: 0,
            turnInPositionX: 77,
            turnInPositionY: 43,
            turnInItemId: 2102,
            turnInMessageId: 285,
            turnInRewardItemId: 171,
            turnInRewardItemLevel: 13,
            turnInRewardPositionX: 77,
            turnInRewardPositionY: 43);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 77, positionY: 43);
        player.MarkLevelVisited(17);

        var activation = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3019, 11),
            1,
            [player]);
        Assert.True(activation.Succeeded, activation.Failure?.ToString());
        Assert.Equal((true, true, 1, 283),
            (quest.Active, quest.Logged, quest.Variable1, Assert.Single(activation.Events).MessageId));

        var activeDialogue = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3019, 11),
            2,
            [player]);
        Assert.True(activeDialogue.Succeeded, activeDialogue.Failure?.ToString());
        Assert.Equal(284, Assert.Single(activeDialogue.Events).MessageId);
        Assert.True(quest.Active);

        player.Inventory.Add(CreateQuestOwnedItem(items, 2102, 42));
        var completion = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3019, 11),
            3,
            [player]);
        Assert.True(completion.Succeeded, completion.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.Empty(player.Inventory);
        Assert.Equal(285, Assert.Single(completion.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(171U, reward.State.StableItemId);
        Assert.Equal((77, 43), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void HellfireLittleGirlAcceptsTheodoreBeforeOpeningDialogue()
    {
        var items = new AuthoritativeItemCatalog([
            CreateQuestItem(2102),
            CreateQuestItem(171),
        ]);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(301903, 3019, 11, 0, 77, 43, 2102,
                AuthoritativeQuestTownerInteractionKind.Complete,
                MessageId: 285,
                AllowInitial: true,
                UseQuestTurnInReward: true),
        ]);
        var quest = new AuthoritativeQuestState(
            3019,
            17,
            1,
            active: false,
            turnInTownerId: 11,
            turnInLevelId: 0,
            turnInPositionX: 77,
            turnInPositionY: 43,
            turnInItemId: 2102,
            turnInRewardItemId: 171,
            turnInRewardItemLevel: 13,
            turnInRewardPositionX: 77,
            turnInRewardPositionY: 43);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);
        var player = CreatePlayer(
            positionX: 77,
            positionY: 43,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [CreateQuestOwnedItem(items, 2102, 42)]);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3019, 11),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(285, Assert.Single(result.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(171U, reward.State.StableItemId);
        Assert.Equal((77, 43), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void ContentOwnedActivationCanAdvanceAnActiveQuestWhenItsVariableGateMatches()
    {
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(300207, 3001, 0, 0, 62, 63, 0,
                AuthoritativeQuestTownerInteractionKind.Activate,
                RequiredVariable2: 0,
                Variable1: 0,
                Variable2: 1,
                MessageId: 115,
                ConsumeItem: false,
                RequiredVisitedLevel1: 4,
                RequiredVisitedLevel2: 5),
        ]);
        var quest = new AuthoritativeQuestState(
            3001,
            1,
            3,
            active: true,
            variable2: 0);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 61, positionY: 63);

        var beforeVisit = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            1,
            [player]);
        Assert.False(beforeVisit.Succeeded);
        Assert.Equal(0, quest.Variable2);

        player.MarkLevelVisited(4);
        var afterVisit = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            2,
            [player]);
        Assert.True(afterVisit.Succeeded, afterVisit.Failure?.ToString());
        Assert.True(quest.Active);
        Assert.Equal((0, 1), (quest.Variable1, quest.Variable2));
        Assert.Equal(115, Assert.Single(afterVisit.Events).MessageId);
    }

    [Fact]
    public void ContentOwnedUnloggedTownerStagesMatchNativePoisonedWaterBranches()
    {
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(300209, 3014, 1, 0, 55, 79, 0,
                AuthoritativeQuestTownerInteractionKind.Activate,
                Variable1: 0,
                MessageId: 39,
                ConsumeItem: false,
                RequiredVisitedLevel1: 1,
                RequiredVisitedLevel2: 5,
                RequireUnlogged: true),
            new(300210, 3014, 1, 0, 55, 79, 0,
                AuthoritativeQuestTownerInteractionKind.Dialogue,
                MessageId: 39,
                ConsumeItem: false,
                RequireUnlogged: true),
        ]);
        var quest = new AuthoritativeQuestState(
            3014,
            4,
            1,
            active: false,
            logged: false);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 55, positionY: 79);

        var blocked = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3014, 1),
            1,
            [player]);
        AssertRejected(blocked, DiabloRuleFailure.InvalidTarget);

        player.MarkLevelVisited(5);
        var activation = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3014, 1),
            2,
            [player]);
        Assert.True(activation.Succeeded, activation.Failure?.ToString());
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(39, quest.MessageId);

        var unloggedActiveQuest = new AuthoritativeQuestState(
            3014,
            4,
            1,
            active: true,
            logged: false);
        var unloggedWorld = new DiabloWorldState(quests: [unloggedActiveQuest]);
        var unloggedProcessor = new DiabloWorldCommandProcessor(
            unloggedWorld,
            questTownerInteractions: interactions);
        var fallback = unloggedProcessor.Execute(
            player,
            new DiabloTurnInQuestIntent(3014, 1),
            3,
            [player]);

        Assert.True(fallback.Succeeded, fallback.Failure?.ToString());
        Assert.True(unloggedActiveQuest.Logged);
        Assert.Equal(39, unloggedActiveQuest.MessageId);
    }

    [Fact]
    public void DialogueOnlyTurnInIsSuppressedAfterBlockedLevelVisit()
    {
        var items = new AuthoritativeItemCatalog([CreateQuestItem(43)]);
        var quest = new AuthoritativeQuestState(
            3017,
            17,
            1,
            active: true,
            turnInTownerId: 7,
            turnInLevelId: 0,
            turnInPositionX: 43,
            turnInPositionY: 66,
            turnInItemId: 43,
            turnInMessageId: 274,
            turnInDialogueOnly: true,
            turnInBlockedVisitedLevel: 21);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 43, positionY: 66);
        player.Inventory.Add(CreateQuestOwnedItem(items, 43, 1));
        player.MarkLevelVisited(21);

        var blocked = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3017, 7),
            1,
            [player]);

        AssertRejected(blocked, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0, quest.MessageId);

        player.VisitedLevelIds.Clear();
        var available = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3017, 7),
            2,
            [player]);

        Assert.True(available.Succeeded, available.Failure?.ToString());
        Assert.Equal(274, quest.MessageId);
        Assert.Single(player.Inventory);
    }

    [Fact]
    public void QuestDialogueRejectsMismatchedUniqueIdentity()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            messageId: 77,
            completionUniqueIdentity: "Gharbad");
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Zhar");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTalkToQuestIntent(3003, 20),
                5,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Logged);
    }

    [Fact]
    public void NativeUniqueDialogueHandInConsumesElixirAndSpawnsVeilOfSteel()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1008, "Veil of Steel", 117, 1, 63800, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(117, 1, 63800, 63800, 0, 0, 18, 60, 60, 1, 1, true),
            new AuthoritativeItemDefinition(2137, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 14,
            unique: true,
            uniqueIdentity: "Lachdanan");
        var quest = new AuthoritativeQuestState(
            3005,
            14,
            1,
            active: false,
            messageId: 81,
            completionUniqueIdentity: "Lachdanan",
            dialogueItemId: 2137,
            dialogueRewardUniqueId: 1008,
            dialogueRewardItemLevel: 1,
            dialogueRewardOffsetY: 1,
            dialogueCompletionMessageId: 83,
            dialogueCompletionDefeatsTarget: true);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 14,
            entityId: 7,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var otherPlayer = CreatePlayer(
            positionX: 20,
            positionY: 21,
            levelId: 14,
            entityId: 8);

        var initial = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            1,
            [player]);

        Assert.True(initial.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(81, Assert.Single(initial.Events).MessageId);
        Assert.Equal(81, target.DialogueMessageId);

        var elixir = items.Generate(2137, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, elixir.CreateInfo, 0, 0, elixir));
        player.InventoryGrid[0] = 0;

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTalkToQuestIntent(3005, 20),
                2,
                [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Completed);
        Assert.Single(player.Inventory);
        Assert.Empty(world.WorldItems);

        otherPlayer.PositionX = 0;
        otherPlayer.PositionY = 0;
        var completed = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            2,
            [player, otherPlayer]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(83, Assert.Single(completed.Events).MessageId);
        Assert.Equal(83, target.DialogueMessageId);
        Assert.Equal(0, target.HitPoints);
        Assert.Equal(target.DeathAnimationTicks, target.DeathTicksRemaining);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1008, reward.State.UniqueId);
        Assert.Equal((20, 21), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void StagedLachdananDialogueSelectsItemBranchAndSpawnsAuthoredUniqueReward()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1008, "Veil of Steel", 117, 1, 63800, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(117, 1, 63800, 63800, 0, 0, 18, 60, 60, 1, 1, true),
            new AuthoritativeItemDefinition(2137, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var quest = new AuthoritativeQuestState(
            3005,
            14,
            1,
            active: false,
            messageId: 81,
            completionUniqueIdentity: "Lachdanan");
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 14,
            unique: true,
            uniqueIdentity: "Lachdanan");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(
                3005,
                0,
                0,
                81,
                0,
                0,
                RequiredItemId: 2137,
                NextMessageId: 81,
                RequireItemPresent: false),
            new(
                3005,
                0,
                0,
                83,
                0,
                0,
                RequiredItemId: 2137,
                ConsumeItem: true,
                CompleteQuest: true,
                RewardKind: AuthoritativeQuestDialogueRewardKind.UniqueItem,
                RewardItemLevel: 1,
                RewardOffsetY: 1,
                NextMessageId: 83,
                DefeatTarget: true,
                RewardUniqueId: 1008),
        ]);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questDialogueStages: stages);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 14,
            entityId: 7,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var blocker = CreatePlayer(positionX: 20, positionY: 21, levelId: 14, entityId: 8);

        var initial = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            1,
            [player, blocker]);

        Assert.True(initial.Succeeded, initial.Failure?.ToString());
        Assert.Equal(81, Assert.Single(initial.Events).MessageId);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Empty(world.WorldItems);

        var elixir = items.Generate(2137, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, elixir.CreateInfo, 0, 0, elixir));
        player.InventoryGrid[0] = 0;

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTalkToQuestIntent(3005, 20),
                2,
                [player, blocker]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Completed);
        Assert.Single(player.Inventory);
        Assert.Empty(world.WorldItems);

        blocker.PositionX = 0;
        blocker.PositionY = 0;
        var completed = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            3,
            [player, blocker]);

        Assert.True(completed.Succeeded, completed.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(83, Assert.Single(completed.Events).MessageId);
        Assert.Equal(83, target.DialogueMessageId);
        Assert.Equal(0, target.HitPoints);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1008, reward.State.UniqueId);
        Assert.Equal((20, 21), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void AuthoredDialogueNormalRewardUsesValidatedSpellBookGeneration()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
            new AuthoritativeItemDefinition(2137, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var spellBooks = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 14,
            unique: true,
            uniqueIdentity: "Lachdanan");
        var quest = new AuthoritativeQuestState(
            3005,
            14,
            1,
            active: false,
            messageId: 81,
            completionUniqueIdentity: "Lachdanan",
            dialogueItemId: 2137,
            dialogueRewardItemId: 114,
            dialogueRewardItemLevel: 1,
            dialogueRewardOffsetY: 1,
            dialogueCompletionMessageId: 83);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            gameMode: DiabloGameMode.SinglePlayer,
            items: items,
            spellBooks: spellBooks);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 14,
            entityId: 7,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray());

        var initial = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            1,
            [player]);
        Assert.True(initial.Succeeded);

        var elixir = items.Generate(2137, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, elixir.CreateInfo, 0, 0, elixir));
        player.InventoryGrid[0] = 0;

        var completed = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(114, reward.State.ItemIndex);
        Assert.Equal(7, reward.State.SpellId);
        Assert.Equal(DiabloItemActionKind.ReadBook, reward.State.ItemActionKind);
        Assert.Equal("Book of Firebolt", reward.State.BaseName);
        Assert.Equal((20, 21), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void AuthoredDialogueActivationRewardSpawnsOnlyOnFirstConversation()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var spellBooks = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 8,
            unique: true,
            uniqueIdentity: "Zhar");
        var quest = new AuthoritativeQuestState(
            3004,
            8,
            1,
            active: false,
            messageId: 148,
            completionUniqueIdentity: "Zhar",
            dialogueVariable1: 2,
            dialogueRewardItemId: 114,
            dialogueRewardItemLevel: 8,
            dialogueRewardOffsetX: 1,
            dialogueRewardOffsetY: 1,
            dialogueRewardOnActivation: true);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            gameMode: DiabloGameMode.SinglePlayer,
            items: items,
            spellBooks: spellBooks);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 8,
            entityId: 7);

        var first = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3004, 20),
            1,
            [player]);

        Assert.True(first.Succeeded);
        Assert.True(quest.Active);
        Assert.Equal(2, quest.Variable1);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(7, reward.State.SpellId);
        Assert.Equal("Book of Firebolt", reward.State.BaseName);
        Assert.Equal((21, 21), (reward.PositionX, reward.PositionY));

        var second = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3004, 20),
            2,
            [player]);

        Assert.True(second.Succeeded);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void NativeTownerTurnInStartsQuestConversationThenConsumesItemAndSpawnsUniqueReward()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Reward Blade", 7, 1, 100, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 1, 2, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);
        var requiredItem = items.Generate(7, 42);
        var inventory = new OwnedStoreItem(0, 0, requiredItem.CreateInfo, 0, 0, requiredItem);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 63,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [inventory],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3011,
            10,
            1,
            messageId: 88,
            variable2: 0,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 7,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1001,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 61,
            turnInRewardPositionY: 64);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var otherPlayer = CreatePlayer(
            positionX: 61,
            positionY: 64,
            entityId: 8);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            1,
            [player, otherPlayer]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Single(player.Inventory);
        var startDialogue = Assert.Single(started.Events);
        Assert.Equal(DiabloGameplayEventKind.Dialogue, startDialogue.Kind);
        Assert.Equal(88, startDialogue.MessageId);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3011, 0),
                2,
                [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Completed);
        Assert.Single(player.Inventory);
        Assert.Empty(world.WorldItems);

        otherPlayer.PositionX = 0;
        otherPlayer.PositionY = 0;
        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            2,
            [player, otherPlayer]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1001, reward.State.UniqueId);
        Assert.Equal((61, 64), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void NativeMagicRockTurnInUsesTheAuthoredConversationGateAndEmpyreanBand()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1004, "Empyrean Band", 113, 1, 8000, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(113, 1, 8000, 8000, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(2135, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var requiredItem = items.Generate(2135, 42);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 63,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, requiredItem.CreateInfo, 0, 0, requiredItem)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3001,
            1,
            3,
            messageId: 115,
            variable2: 0,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 2135,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1004,
            turnInRewardItemLevel: 5,
            turnInRewardPositionX: 61,
            turnInRewardPositionY: 64,
            turnInMessageId: 117);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Single(player.Inventory);
        Assert.Equal(115, Assert.Single(started.Events).MessageId);

        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.Empty(player.Inventory);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1004, reward.State.UniqueId);
        Assert.Equal((61, 64), (reward.PositionX, reward.PositionY));
        Assert.Equal(117, Assert.Single(completed.Events).MessageId);
    }

    [Fact]
    public void NativeAnvilTurnInUsesTheAuthoredConversationGateAndGriswoldsEdge()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1010, "Griswold's Edge", 119, 1, 42000, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(119, 1, 750, 750, 4, 12, 0, 50, 50, 1, 1, true),
            new AuthoritativeItemDefinition(2138, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var quest = new AuthoritativeQuestState(
            3011,
            10,
            1,
            active: true,
            messageId: 88,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 2138,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1010,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 61,
            turnInRewardPositionY: 64,
            turnInMessageId: 90);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 63, entityId: 7);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Equal(88, Assert.Single(started.Events).MessageId);

        var anvil = items.Generate(2138, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, anvil.CreateInfo, 0, 0, anvil));
        player.InventoryGrid[0] = 0;

        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.Empty(player.Inventory);
        Assert.Equal(90, Assert.Single(completed.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1010, reward.State.UniqueId);
        Assert.Equal((61, 64), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void NativeTownerConversationGateRunsBeforeRequiredItemValidation()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2135, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3001,
            1,
            3,
            messageId: 115,
            variable2: 0,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 2135,
            turnInRequiredVariable2: 1,
            turnInMessageId: 117);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 63, entityId: 7);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Empty(player.Inventory);
        Assert.Equal(115, Assert.Single(started.Events).MessageId);
    }

    [Fact]
    public void NativeOgdenSignTurnInUsesAuthoredStagesAndHarlequinReward()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1007, "Harlequin Crest", 116, 1, 4000, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(116, 7, 4000, 4000, 0, 0, 0, 15, 15, 1, 1, true),
            new AuthoritativeItemDefinition(2136, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var quest = new AuthoritativeQuestState(
            3008,
            4,
            1,
            active: true,
            variable1: 1,
            messageId: 12,
            completionVariable1: 3,
            turnInTownerId: 3,
            turnInLevelId: 0,
            turnInPositionX: 55,
            turnInPositionY: 62,
            turnInItemId: 2136,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1007,
            turnInRewardItemLevel: 4,
            turnInRewardPositionX: 54,
            turnInRewardPositionY: 63,
            turnInMessageId: 13);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 54, positionY: 62, entityId: 7);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3008, 3),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Equal(12, Assert.Single(started.Events).MessageId);

        var sign = items.Generate(2136, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, sign.CreateInfo, 0, 0, sign));
        player.InventoryGrid[0] = 0;

        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3008, 3),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.Equal(3, quest.Variable1);
        Assert.Empty(player.Inventory);
        Assert.Equal(13, Assert.Single(completed.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1007, reward.State.UniqueId);
        Assert.Equal((54, 63), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void NativeTownerTurnInRejectsWrongTownerOrMissingItemWithoutMutation()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 1, 2, 0, 20, 20, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3011,
            10,
            1,
            variable2: 1,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 7,
            turnInRequiredVariable2: 1);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 63, entityId: 7);

        AssertRejected(
            processor.Execute(player, new DiabloTurnInQuestIntent(3011, 1), 1, [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Completed);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void NativeTownerTurnInSpawnsConfiguredNormalItemReward()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 1, 2, 0, 20, 20, 1, 1, true),
            new AuthoritativeItemDefinition(171, 13, 1200, 1200, 0, 0, 0, 0, 0, 1, 1, false),
        ]);
        var requiredItem = items.Generate(7, 42);
        var player = CreatePlayer(
            positionX: 77,
            positionY: 43,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, requiredItem.CreateInfo, 0, 0, requiredItem)]);
        var quest = new AuthoritativeQuestState(
            3019,
            17,
            1,
            turnInTownerId: 11,
            turnInLevelId: 0,
            turnInPositionX: 77,
            turnInPositionY: 43,
            turnInItemId: 7,
            turnInRewardItemId: 171,
            turnInRewardItemLevel: 13,
            turnInRewardPositionX: 77,
            turnInRewardPositionY: 43);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3019, 11),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(171U, reward.State.StableItemId);
        Assert.Equal((77, 43), (reward.PositionX, reward.PositionY));
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void AuthoredStagedTurnInConsumesBrainKeepsQuestActiveAndSpawnsSpectralElixir()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2140, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(2142, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var brain = items.Generate(2140, 42);
        var player = CreatePlayer(
            positionX: 55,
            positionY: 79,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, brain.CreateInfo, 0, 0, brain)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3002,
            9,
            1,
            variable1: 5,
            turnInTownerId: 1,
            turnInLevelId: 0,
            turnInPositionX: 55,
            turnInPositionY: 79,
            turnInItemId: 2140,
            turnInMessageId: 124,
            turnInRewardItemId: 2142,
            turnInRewardPositionX: 55,
            turnInRewardPositionY: 80,
            turnInRequiredVariable1: 5,
            turnInVariable1: 7,
            turnInProgressOnly: true);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3002, 1),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(0U, quest.Progress);
        Assert.Equal(7, quest.Variable1);
        Assert.Empty(player.Inventory);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(2142U, reward.State.StableItemId);
        Assert.Equal((55, 80), (reward.PositionX, reward.PositionY));
        Assert.Equal(124, Assert.Single(result.Events).MessageId);

        var retry = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3002, 1),
            2,
            [player]);

        Assert.False(retry.Succeeded);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void AuthoredJerseyTurnInConsumesBrownSuitAndSpawnsBovinePlate()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1011, "Bovine Plate", 120, 1, 400, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(120, 9, 0, 0, 0, 0, 40, 40, 40, 2, 2, true),
            new AuthoritativeItemDefinition(2104, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var suit = items.Generate(2104, 42);
        var player = CreatePlayer(
            positionX: 60,
            positionY: 22,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, suit.CreateInfo, 0, 0, suit)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3024,
            9,
            1,
            active: true,
            messageId: 300,
            variable2: 1,
            turnInTownerId: 12,
            turnInLevelId: 0,
            turnInPositionX: 61,
            turnInPositionY: 22,
            turnInItemId: 2104,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1011,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 62,
            turnInRewardPositionY: 23,
            turnInMessageId: 304);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3024, 12),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(304, Assert.Single(result.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1011, reward.State.UniqueId);
        Assert.Equal((62, 23), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void AuthoredTownerAlternateItemConsumesGreySuitWithoutCompletingQuest()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2104, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(2105, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var greySuit = items.Generate(2105, 42);
        var player = CreatePlayer(
            positionX: 60,
            positionY: 22,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, greySuit.CreateInfo, 0, 0, greySuit)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3024,
            9,
            1,
            active: true,
            turnInTownerId: 12,
            turnInLevelId: 0,
            turnInPositionX: 61,
            turnInPositionY: 22,
            turnInItemId: 2104,
            turnInAlternateItemId: 2105,
            turnInAlternateMessageId: 303);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3024, 12),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(quest.Completed);
        Assert.True(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(303, Assert.Single(result.Events).MessageId);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void AuthoredTownerActivationSpawnsInitialRuneBombOnce()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2101, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3018,
            9,
            1,
            active: false,
            activateOnRoute: false,
            messageId: 277,
            completionVariable1: 1,
            turnInTownerId: 10,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 16,
            turnInRewardItemId: 2101,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 63,
            turnInRewardPositionY: 16,
            turnInActivationOnly: true);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 16, entityId: 7);

        var first = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3018, 10),
            1,
            [player]);

        Assert.True(first.Succeeded);
        Assert.True(quest.Active);
        Assert.Equal(1, quest.Variable1);
        Assert.Equal(277, Assert.Single(first.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(2101U, reward.State.StableItemId);
        Assert.Equal((63, 16), (reward.PositionX, reward.PositionY));

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3018, 10),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void AuthoredInteractionActivationCanReuseQuestTurnInReward()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2101, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3018,
            9,
            1,
            active: false,
            completionVariable1: 1,
            turnInTownerId: 10,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 16,
            turnInRewardItemId: 2101,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 63,
            turnInRewardPositionY: 16,
            turnInActivationOnly: true);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new AuthoritativeQuestTownerInteractionDefinition(
                301801,
                3018,
                10,
                0,
                62,
                16,
                0,
                AuthoritativeQuestTownerInteractionKind.Activate,
                Variable1: 1,
                MessageId: 277,
                ConsumeItem: false,
                UseQuestTurnInReward: true),
        ]);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 61, positionY: 16, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3018, 10),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.Equal(1, quest.Variable1);
        Assert.Equal(277, Assert.Single(result.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal((2101U, 63, 16), (
            reward.State.StableItemId,
            reward.PositionX,
            reward.PositionY));
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3018, 10),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void NativeButcherActivationUsesAuthoredDeadGuyInteraction()
    {
        var quest = new AuthoritativeQuestState(
            3007,
            2,
            1,
            active: false,
            messageId: 65,
            completionMonsterId: 53,
            turnInTownerId: 2,
            turnInLevelId: 0,
            turnInPositionX: 24,
            turnInPositionY: 32,
            turnInActivationOnly: true,
            turnInActivationVariable1: 1);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: new AuthoritativeItemCatalog([
                new AuthoritativeItemDefinition(1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            ]));
        var player = CreatePlayer(positionX: 23, positionY: 32, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3007, 2),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(1, quest.Variable1);
        Assert.Equal(65, Assert.Single(result.Events).MessageId);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3007, 2),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void AuthoredTownerActivationConsumesItemAndUsesExplicitNativeMarker()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(34, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var staff = items.Generate(34, 42);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 71,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, staff.CreateInfo, 0, 0, staff)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3016,
            15,
            1,
            active: false,
            completionVariable1: 7,
            turnInTownerId: 4,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 71,
            turnInItemId: 34,
            turnInMessageId: 35,
            turnInActivationOnly: true,
            turnInActivationVariable1: 2);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        Assert.Equal(35, quest.TurnInMessageId);
        Assert.True(quest.TurnInActivationOnly);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3016, 4),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(2, quest.Variable1);
        Assert.Empty(player.Inventory);
        Assert.Equal(35, Assert.Single(result.Events).MessageId);
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3016, 4),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void ContentOwnedLazarusActivationUsesNativeDialogueAndConsumesStaff()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(34, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var staff = items.Generate(34, 42);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 71,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, staff.CreateInfo, 0, 0, staff)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3016,
            15,
            1,
            active: false,
            turnInTownerId: 4);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(
                301602,
                3016,
                4,
                0,
                62,
                71,
                34,
                AuthoritativeQuestTownerInteractionKind.Activate,
                Variable1: 2,
                MessageId: 23),
        ]);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3016, 4),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(2, quest.Variable1);
        Assert.Empty(player.Inventory);
        Assert.Equal(23, Assert.Single(result.Events).MessageId);
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3016, 4),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void ContentOwnedLazarusMultiplayerOpeningDialogueLogsUnloggedQuest()
    {
        var quest = new AuthoritativeQuestState(
            3016,
            15,
            1,
            active: true,
            logged: false,
            turnInTownerId: 4);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(
                301603,
                3016,
                4,
                0,
                62,
                71,
                0,
                AuthoritativeQuestTownerInteractionKind.Dialogue,
                MessageId: 23,
                ConsumeItem: false,
                RequireUnlogged: true),
        ]);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 61, positionY: 71, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3016, 4),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Logged);
        Assert.Equal(23, Assert.Single(result.Events).MessageId);
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3016, 4),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void AuthoredTownerCompletionStageSpawnsAuricOnce()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2103, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3018,
            9,
            1,
            completed: true,
            active: false,
            variable2: 0,
            turnInTownerId: 10,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 16,
            turnInOnCompletion: true,
            turnInCompletionVariable2: 1,
            turnInCompletionMessageId: 280,
            turnInCompletionRewardItemId: 2103,
            turnInCompletionRewardPositionX: 63,
            turnInCompletionRewardPositionY: 16);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 16, entityId: 7);

        var first = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3018, 10),
            1,
            [player]);

        Assert.True(first.Succeeded);
        Assert.True(quest.Completed);
        Assert.Equal(1, quest.Variable2);
        Assert.Equal(280, Assert.Single(first.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(2103U, reward.State.StableItemId);
        Assert.Equal((63, 16), (reward.PositionX, reward.PositionY));

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3018, 10),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void ContentOwnedCompletionAfterQuestStageUsesCompletionRewardAndFollowOnQuest()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2103, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3018,
            9,
            1,
            completed: true,
            active: false,
            variable2: 0,
            completionActivateQuestId: 3019,
            turnInTownerId: 10,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 16,
            turnInOnCompletion: true,
            turnInCompletionVariable2: 1,
            turnInCompletionMessageId: 280,
            turnInCompletionRewardItemId: 2103,
            turnInCompletionRewardPositionX: 63,
            turnInCompletionRewardPositionY: 16);
        var followOn = new AuthoritativeQuestState(3019, 17, 1, active: false);
        var interactions = new AuthoritativeQuestTownerInteractionCatalog([
            new(
                301802,
                3018,
                10,
                0,
                62,
                16,
                0,
                AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest,
                RequiredVariable2: 0,
                Variable2: 1,
                MessageId: 280,
                ConsumeItem: false,
                AllowCompleted: true,
                UseQuestCompletionReward: true),
        ]);
        var world = new DiabloWorldState(quests: [quest, followOn]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            questTownerInteractions: interactions);
        var player = CreatePlayer(positionX: 61, positionY: 16, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3018, 10),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.Equal(1, quest.Variable2);
        Assert.True(followOn.Active);
        Assert.Equal(280, Assert.Single(result.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal((2103U, 63, 16), (
            reward.State.StableItemId,
            reward.PositionX,
            reward.PositionY));
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3018, 10),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void AuthoredLazarusCompletionStageWritesNativeMarkerAndLogsDiablo()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(34, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3016,
            15,
            1,
            completed: true,
            active: false,
            variable1: 7,
            variable2: 0,
            completionActivateQuestId: 3006,
            turnInTownerId: 4,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 71,
            turnInOnCompletion: true,
            turnInCompletionVariable2: 1,
            turnInCompletionRequiredVariable1: 7,
            turnInCompletionVariable1: 8,
            turnInCompletionMessageId: 25);
        var diabloQuest = new AuthoritativeQuestState(3006, 15, 1, active: false);
        var world = new DiabloWorldState(quests: [quest, diabloQuest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 71, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3016, 4),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal((8, 1), (quest.Variable1, quest.Variable2));
        Assert.True(diabloQuest.Active);
        Assert.True(diabloQuest.Logged);
        Assert.Equal(25, Assert.Single(result.Events).MessageId);
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3016, 4),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void AuthoredVariableOneOnlyCompletionStagePreservesVariableTwo()
    {
        var quest = new AuthoritativeQuestState(
            3014,
            0,
            1,
            completed: true,
            active: false,
            variable1: 0,
            variable2: 0,
            turnInTownerId: 1,
            turnInLevelId: 0,
            turnInPositionX: 55,
            turnInPositionY: 79,
            turnInOnCompletion: true,
            turnInCompletionRequiredVariable1: 0,
            turnInCompletionVariable1: 2,
            turnInCompletionMessageId: 41);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: new AuthoritativeItemCatalog([
                new AuthoritativeItemDefinition(43, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            ]));
        var player = CreatePlayer(positionX: 54, positionY: 79, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3014, 1),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal((2, 0), (quest.Variable1, quest.Variable2));
        Assert.Equal(41, Assert.Single(result.Events).MessageId);
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3014, 1),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void AuthoredTownerDialogueRequiresItemWithoutConsumingOrCompletingQuest()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(43, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var map = items.Generate(43, 42);
        var player = CreatePlayer(
            positionX: 42,
            positionY: 66,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, map.CreateInfo, 0, 0, map)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3017,
            17,
            1,
            active: true,
            turnInTownerId: 7,
            turnInLevelId: 0,
            turnInPositionX: 43,
            turnInPositionY: 66,
            turnInItemId: 43,
            turnInMessageId: 274,
            turnInDialogueOnly: true);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3017, 7),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.True(quest.Logged);
        Assert.Equal(274, quest.MessageId);
        Assert.Single(player.Inventory);
        Assert.Equal(274, Assert.Single(result.Events).MessageId);
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3017, 7),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void TrapDisarmRequiresRogueAbilityAndClearsArmedState()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var player = new AuthoritativePlayerState(
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
            0,
            0,
            abilitySpellId: 28) {
            EntityId = 7,
        };
        var processor = new DiabloWorldCommandProcessor(
            world,
            trapDisarmAbilitySpellId: 28);

        var result = processor.Execute(
            player,
            new DiabloDisarmTrapIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(@object.TrapArmed);
    }

    [Fact]
    public void SemanticTrapDisarmRoleAuthorizesAConfiguredCustomSpellIdentity()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var player = new AuthoritativePlayerState(
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
            0,
            0,
            abilitySpellId: 900,
            abilityKind: DiabloPlayerAbilityKind.TrapDisarm) {
            EntityId = 7,
        };
        var processor = new DiabloWorldCommandProcessor(
            world,
            trapDisarmAbilitySpellId: 28);

        var result = processor.Execute(
            player,
            new DiabloDisarmTrapIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(@object.TrapArmed);
    }

    [Fact]
    public void TrapDisarmRejectsWrongAbilityAndLeavesTrapArmed()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var player = CreatePlayer(entityId: 7);
        var processor = new DiabloWorldCommandProcessor(
            world,
            trapDisarmAbilitySpellId: 28);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloDisarmTrapIntent(20),
                5,
                [player]),
            DiabloRuleFailure.NotAllowed);
        Assert.True(@object.TrapArmed);
    }

    [Fact]
    public void TrapDisarmRejectsWrappedExtremeDistanceWithoutMutation()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            int.MinValue,
            0,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var player = CreatePlayer(positionX: int.MaxValue, entityId: 7);
        player.AbilitySpellId = 28;
        var processor = new DiabloWorldCommandProcessor(
            world,
            trapDisarmAbilitySpellId: 28);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloDisarmTrapIntent(@object.EntityId),
                5,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.True(@object.TrapArmed);
    }

    [Fact]
    public void ExperienceObjectLevelsPlayerAndEmitsEvent()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.Experience,
            effectAmount: 2100);
        var world = new DiabloWorldState(objects: [@object]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2100U, player.Experience);
        Assert.Equal(3U, player.CharacterLevel);
        Assert.Equal(
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Experience,
                7,
                7,
                2100),
            Assert.Single(result.Events));
    }

    [Fact]
    public void QuestAdvancementCompletesThenRejectsFurtherProgress()
    {
        var quest = new AuthoritativeQuestState(30, 0, 2);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        Assert.True(processor.Execute(
            player,
            new DiabloAdvanceQuestIntent(30),
            1,
            [player]).Succeeded);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.True(processor.Execute(
            player,
            new DiabloAdvanceQuestIntent(30),
            2,
            [player]).Succeeded);
        Assert.Equal(2U, quest.Progress);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);

        AssertRejected(
            processor.Execute(player, new DiabloAdvanceQuestIntent(30), 3, [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2U, quest.Progress);
    }

    [Fact]
    public void UnavailableQuestRejectsAdvancementAndLinkedObjectProgress()
    {
        var quest = new AuthoritativeQuestState(30, 0, 2, unavailable: true);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            questId: 30);
        var world = new DiabloWorldState(objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        AssertRejected(
            processor.Execute(player, new DiabloAdvanceQuestIntent(30), 1, [player]),
            DiabloRuleFailure.InvalidTarget);
        AssertRejected(
            processor.Execute(player, new DiabloOperateObjectIntent(20), 2, [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0U, quest.Progress);
        Assert.False(@object.Activated);
    }

    [Fact]
    public void InitialWorldValidationRejectsOverlappingLiveEntities()
    {
        var monster = new AuthoritativeCombatTarget(10, 1, 0, 5);
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty);
        var world = new DiabloWorldState(combatTargets: [monster], worldItems: [item]);

        Assert.Throws<InvalidDataException>(() => world.ValidateInitialPlayerPosition(0, 0, 0));
    }

    private static AuthoritativePlayerState CreatePlayer(
        int positionX = 0,
        int positionY = 0,
        uint levelId = 0,
        int life = 50,
        int lifeMaximum = 50,
        uint entityId = 0,
        IReadOnlyList<int>? inventoryGrid = null,
        uint gold = 100,
        IReadOnlyList<OwnedStoreItem>? inventory = null,
        OwnedStoreItem? heldItem = null,
        IReadOnlyList<EquippedStoreItem>? equipment = null)
    {
        return new AuthoritativePlayerState(
            gold,
            0,
            life,
            0,
            0,
            PlayerAttributesState.Zero,
            equipment ?? [],
            inventoryGrid ?? Enumerable.Repeat(-1, 40).ToArray(),
            [],
            positionX,
            positionY,
            lifeMaximum,
            1,
            levelId,
            0,
            inventory: inventory,
            heldItem: heldItem) {
            EntityId = entityId,
        };
    }

    private static OwnedStoreItem CreateGoldStack(
        uint itemSeed,
        uint value,
        AuthoritativeCombatRules rules)
    {
        return new OwnedStoreItem(
            0,
            0,
            itemSeed,
            value,
            0,
            CreateGoldState(value, rules));
    }

    private static AuthoritativeItemDefinition CreateQuestItem(uint itemId)
        => new(itemId, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true);

    private static OwnedStoreItem CreateQuestOwnedItem(
        AuthoritativeItemCatalog items,
        uint itemId,
        uint seed)
    {
        return new OwnedStoreItem(
            0,
            0,
            seed,
            0,
            0,
            items.GetRequired(itemId).Generate(seed));
    }

    private static AuthoritativeItemState CreateGoldState(
        uint value,
        AuthoritativeCombatRules rules)
    {
        return AuthoritativeItemState.Empty with {
            ItemType = rules.DeathGoldItemType,
            ItemIndex = rules.DeathGoldItemIndex,
            Value = checked((int)value),
            IdentifiedValue = checked((int)value),
            StatFlag = true,
        };
    }

    private static void AssertRejected(
        DiabloWorldIntentResult result,
        DiabloRuleFailure expectedFailure)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expectedFailure, result.Failure);
        Assert.Empty(result.Events);
    }
}
