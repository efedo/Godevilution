using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Positive module-only continuation coverage for retained cohort edges. */
public sealed class DiabloSimulationCohortContinuationTests
{
    [Fact]
    public void DeadTargetRetainsItsDropWhenTheSameTemplateIsPublishedLive()
    {
        var item = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 2,
            Value = 37,
            IdentifiedValue = 37,
        };
        var target = new AuthoritativeCombatTarget(
            11,
            3,
            3,
            0,
            maxHitPoints: 40,
            levelId: 0,
            dropItemEntityId: 300,
            dropItemSeed: 401,
            dropItemPrice: 37,
            dropItemState: item,
            deathAnimationTicks: 4);
        var liveDrop = new AuthoritativeWorldItem(300, 0, 3, 3, 401, 37, item);
        var fixture = CreateFixture(combatTargets: [target]);
        fixture.Runtime.Write("hero", _ => {
            fixture.Simulation.WorldState.CombatTargets[11].BeginDeathAnimation();
            fixture.Simulation.WorldState.WorldItems.Add(liveDrop.EntityId, liveDrop);
        });

        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();

        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));
        var restoredTarget = Assert.Single(
            candidate.Simulation.CaptureCohortState(restored).CombatTargets);
        Assert.Equal(4, restoredTarget.Monster.DeathTicksRemaining);
        Assert.Equal(restoredTarget.Drop, Assert.Single(
            candidate.Simulation.CaptureCohortState(restored).WorldItems));

        fixture.Runtime.AdvanceTo(1);
        restored.AdvanceTo(1);
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
    }

    [Fact]
    public void MovedTargetRetainsTheOriginalDropTemplateAndContinuesDeathParity()
    {
        var item = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 3,
            Value = 19,
            IdentifiedValue = 19,
        };
        var target = new AuthoritativeCombatTarget(
            21,
            1,
            1,
            20,
            levelId: 0,
            dropItemEntityId: 301,
            dropItemSeed: 402,
            dropItemPrice: 19,
            dropItemState: item,
            deathAnimationTicks: 1);
        var fixture = CreateFixture(combatTargets: [target]);
        var sourceTarget = fixture.Simulation.WorldState.CombatTargets[21];
        sourceTarget.BeginMovement(2, 1);
        sourceTarget.CompleteMovement();

        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        var capturedTarget = Assert.Single(checkpoint.CombatTargets);
        Assert.Equal((2, 1), (capturedTarget.Monster.PositionX, capturedTarget.Monster.PositionY));
        Assert.Equal((1, 1), (capturedTarget.Drop!.PositionX, capturedTarget.Drop.PositionY));

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));

        var restoredTarget = candidate.Simulation.WorldState.CombatTargets[21];
        sourceTarget.ApplyDamageQ6(sourceTarget.HitPointsQ6);
        restoredTarget.ApplyDamageQ6(restoredTarget.HitPointsQ6);
        sourceTarget.BeginDeathAnimation();
        restoredTarget.BeginDeathAnimation();
        fixture.Runtime.RestoreAtTick(1);
        restored.RestoreAtTick(1);
        fixture.Runtime.AdvanceTo(2);
        restored.AdvanceTo(2);
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
    }

    [Fact]
    public void ProjectileMayOutliveMissingSourceAndHistoricalTarget()
    {
        var projectile = new AuthoritativeProjectile(
            700,
            sourceEntityId: 701,
            targetEntityId: 702,
            spellId: 1,
            levelId: 0,
            positionX: 1,
            positionY: 1,
            targetX: 6,
            targetY: 6,
            damage: 9,
            damageType: AuthoritativeDamageType.Fire,
            areaRadius: 0,
            remainingTicks: 3) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
            DamageQ6 = 641,
            Phase = AuthoritativeProjectilePhase.Travel,
            VelocityX = 17,
            VelocityY = -11,
            ActivationDelayTicks = 1,
        };
        var fixture = CreateFixture(projectiles: [projectile]);

        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        var capturedProjectile = Assert.Single(checkpoint.Projectiles);
        Assert.Equal((701U, 702U, 641), (
            capturedProjectile.SourceEntityId,
            capturedProjectile.TargetEntityId,
            capturedProjectile.DamageQ6));

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));

        fixture.Runtime.AdvanceTo(1);
        restored.AdvanceTo(1);
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
    }

    [Fact]
    public void ScavengerMayRetainAReferenceToACorpseRemovedBeforeCapture()
    {
        var scavenger = new AuthoritativeCombatTarget(
            801,
            4,
            4,
            25,
            levelId: 0,
            nativeAi: "Scavenger",
            scavengerHealingTicks: 7,
            scavengerCorpseEntityId: 899);
        var fixture = CreateFixture(combatTargets: [scavenger]);

        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        var capturedScavenger = Assert.Single(checkpoint.CombatTargets).Monster;
        Assert.Equal((7, 899U), (
            capturedScavenger.ScavengerHealingTicks,
            capturedScavenger.ScavengerCorpseEntityId));
        Assert.Empty(checkpoint.Corpses);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));

        fixture.Runtime.AdvanceTo(1);
        restored.AdvanceTo(1);
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
    }

    [Fact]
    public void VictoryFreezesAnOlderActionAndPortalClockWhileRuntimeContinues()
    {
        var portal = new AuthoritativePortal(900, 0, 0, 0, 0, 6, 6) {
            RemainingTicks = 8,
            OwnerEntityId = 1,
            Bidirectional = true,
            StableSymbol = "retained-portal",
        };
        var fixture = CreateFixture(portals: [portal]);
        fixture.Simulation.WorldState.RestorePortalClock(2);
        fixture.Simulation.WorldState.VictoryTriggered = true;
        fixture.Runtime.Write("hero", player => {
            player.CurrentAction = new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Move,
                4,
                5,
                6,
                7) {
                DirectionX = 1,
                EffectApplied = false,
            };
            player.CombatMode = DiabloPlayerCombatMode.WalkSideways;
            player.CombatModeEndTick = 7;
        });

        fixture.Runtime.AdvanceTo(12);
        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        Assert.Equal(12UL, checkpoint.Tick);
        Assert.Equal(2UL, checkpoint.LastPortalTick);
        Assert.True(checkpoint.VictoryTriggered);
        Assert.NotNull(checkpoint.Participants[0].Player.CurrentAction);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));

        var sourcePlayer = fixture.Runtime.Read("hero", player => player);
        var restoredPlayer = restored.Read("hero", player => player);
        var sourceResult = fixture.Simulation.Execute(
            sourcePlayer,
            new DiabloMoveIntent(1, 0),
            13,
            [sourcePlayer]);
        var restoredResult = candidate.Simulation.Execute(
            restoredPlayer,
            new DiabloMoveIntent(1, 0),
            13,
            [restoredPlayer]);
        Assert.Equal(sourceResult.Succeeded, restoredResult.Succeeded);
        Assert.Equal(sourceResult.Failure, restoredResult.Failure);
        Assert.Equal(
            sourceResult.Events.ToArray(),
            restoredResult.Events.ToArray());
        Assert.Equal(DiabloRuleFailure.NotAllowed, sourceResult.Failure);
    }

    [Fact]
    public void PendingMoveDoesNotEnterADestinationBlockedAfterAdmission()
    {
        var fixture = CreateFixture(useNativePlayerActionTiming: true);
        var player = fixture.Runtime.Read("hero", state => state);
        var admitted = fixture.Simulation.Execute(
            player,
            new DiabloMoveIntent(1, 0),
            4,
            [player]);
        Assert.True(admitted.Succeeded, admitted.Failure?.ToString());
        Assert.Equal((2, 1), (player.FuturePositionX, player.FuturePositionY));

        fixture.Simulation.WorldState.Objects.Add(
            950,
            new AuthoritativeWorldObject(950, 1, 0, 2, 1));
        fixture.Runtime.RestoreAtTick(4);
        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        Assert.Equal(
            DiabloPlayerActionKind.Move,
            Assert.Single(checkpoint.Participants).Player.CurrentAction!.Kind);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));

        fixture.Runtime.AdvanceTo(12);
        restored.AdvanceTo(12);
        var sourcePlayer = fixture.Runtime.Read("hero", state => state);
        var restoredPlayer = restored.Read("hero", state => state);
        Assert.Equal((1, 1), (sourcePlayer.PositionX, sourcePlayer.PositionY));
        Assert.Equal((1, 1), (restoredPlayer.PositionX, restoredPlayer.PositionY));
        Assert.Null(sourcePlayer.CurrentAction);
        Assert.Null(restoredPlayer.CurrentAction);

        var sourceResult = fixture.Simulation.Execute(
            sourcePlayer,
            new DiabloMoveIntent(1, 0),
            13,
            [sourcePlayer]);
        var restoredResult = candidate.Simulation.Execute(
            restoredPlayer,
            new DiabloMoveIntent(1, 0),
            13,
            [restoredPlayer]);
        Assert.Equal(sourceResult, restoredResult);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, sourceResult.Failure);
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
    }

    [Fact]
    public void RichQuestDialogueRetainsEveryPolicyFieldAndContinuesAfterRestore()
    {
        var quest = new AuthoritativeQuestState(
            990,
            0,
            3,
            progress: 1,
            active: false,
            logged: true,
            variable1: 4,
            variable2: 5,
            messageId: 77,
            completionMonsterId: 12,
            completionUniqueIdentity: "cohort-rich-quest",
            dialogueVariable1: 6,
            dialogueVariable2: 7,
            completionVariable1: 8,
            completionVariable2: 9,
            completionActivateQuestId: 991,
            activateOnRoute: true,
            completionRewardItemId: 400,
            completionRewardItemLevel: 12,
            turnInTownerId: 20,
            turnInLevelId: 0,
            turnInPositionX: 3,
            turnInPositionY: 3,
            turnInItemId: 401,
            turnInRequiredVariable2: 10,
            turnInRewardUniqueId: 402,
            turnInRewardItemLevel: 5,
            turnInRewardPositionX: 4,
            turnInRewardPositionY: 4,
            turnInMessageId: 88,
            turnInRewardItemId: 403,
            dialogueItemId: 404,
            dialogueRewardUniqueId: 405,
            dialogueRewardItemLevel: 6,
            dialogueRewardOffsetX: 1,
            dialogueRewardOffsetY: -1,
            dialogueCompletionMessageId: 89,
            dialogueRewardItemId: 406,
            dialogueRewardOnActivation: true,
            turnInOnCompletion: true,
            turnInCompletionVariable2: 11,
            turnInCompletionMessageId: 90,
            turnInCompletionRewardUniqueId: 407,
            turnInCompletionRewardItemLevel: 7,
            turnInCompletionRewardPositionX: 5,
            turnInCompletionRewardPositionY: 5,
            turnInCompletionRewardItemId: 408,
            turnInAlternateItemId: 409,
            turnInAlternateMessageId: 91,
            turnInActivationVariable1: 12,
            routeActivationVariable1: 13,
            turnInCompletionRequiredVariable1: 14,
            turnInCompletionVariable1: 15,
            turnInRequiredVariable1: 16,
            turnInVariable1: 17,
            turnInProgressOnly: true,
            dialogueCompletionVariable1: 18,
            turnInBlockedVisitedLevel: 2,
            dialogueCompletionDefeatsTarget: true) {
            StableSymbol = "cohort-rich-quest",
        };
        var fixture = CreateFixture(
            quests: [quest],
            questTypes: new AuthoritativeQuestTypeCatalog([
                new AuthoritativeQuestTypeDefinition(
                    990,
                    20,
                    "Cohort Rich Quest",
                    0,
                    0,
                    string.Empty,
                    0,
                    false),
            ]),
            questRouteMessages: new AuthoritativeQuestRouteMessageCatalog([
                new(990, 0, 286),
            ]));

        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        var checkpointQuest = Assert.Single(checkpoint.Quests);
        AssertQuestProjection(quest, checkpointQuest);
        Assert.Equal(20U, checkpointQuest.NativeQuestId);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));
        AssertQuestProjection(
            candidate.Simulation.WorldState.Quests[990],
            candidate.Simulation.CaptureCohortState(restored).Quests.Single());

        var sourcePlayer = fixture.Runtime.Read("hero", state => state);
        var restoredPlayer = restored.Read("hero", state => state);
        var sourceResult = fixture.Simulation.Execute(
            sourcePlayer,
            new DiabloMoveIntent(1, 0),
            1,
            [sourcePlayer]);
        var restoredResult = candidate.Simulation.Execute(
            restoredPlayer,
            new DiabloMoveIntent(1, 0),
            1,
            [restoredPlayer]);
        Assert.Equal(sourceResult.Succeeded, restoredResult.Succeeded);
        Assert.Equal(sourceResult.Failure, restoredResult.Failure);
        Assert.Equal(
            sourceResult.Events.ToArray(),
            restoredResult.Events.ToArray());
        Assert.True(sourceResult.Succeeded, sourceResult.Failure?.ToString());
        Assert.Equal(DiabloGameplayEventKind.Dialogue, Assert.Single(sourceResult.Events).Kind);

        var sourceQuest = fixture.Simulation.WorldState.Quests[990];
        var restoredQuest = candidate.Simulation.WorldState.Quests[990];
        AssertQuestProjection(sourceQuest, fixture.Simulation.CaptureCohortState(fixture.Runtime).Quests.Single());
        AssertQuestProjection(sourceQuest, candidate.Simulation.CaptureCohortState(restored).Quests.Single());
        AssertQuestProjection(sourceQuest, restoredQuest);
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
    }

    [Fact]
    public void MatchingPlayerPoliciesContinueEquipmentAndPolicyLossIsRejected()
    {
        var classes = LoadShippedPlayerMetadata().Classes;
        var items = CreatePolicyItems();
        var fixture = CreateFixture(
            playerClasses: classes,
            items: items,
            playerTemplate: CreatePolicyTemplate(classes, items));
        fixture.Runtime.Write("hero", player => {
            var state = AuthoritativeItemState.Empty with {
                StableItemId = 500,
                ItemType = 1,
                ItemIndex = 500,
                EquipLocation = 1,
                ItemClass = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
                StatFlag = true,
                Identified = true,
            };
            player.Inventory.Add(new OwnedStoreItem(0, 0, 901, 0, 0, state));
            player.InventoryGrid[0] = 0;
        });

        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        var participant = Assert.Single(checkpoint.Participants);
        Assert.Equal(true, participant.PlayerClassBound);
        Assert.Equal(true, participant.ItemCatalogBound);
        Assert.Equal(classes.Default, participant.PlayerClassDefinition);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        AssertExactRecapture(
            checkpoint,
            candidate.Simulation.CaptureCohortState(restored));
        Assert.Same(classes.Default, fixture.Runtime.Read("hero", state => state.PlayerClass));
        Assert.Same(items, fixture.Runtime.Read("hero", state => state.Items));
        Assert.Same(classes.Default, restored.Read("hero", state => state.PlayerClass));
        Assert.Same(items, restored.Read("hero", state => state.Items));

        var sourcePlayer = fixture.Runtime.Read("hero", state => state);
        var restoredPlayer = restored.Read("hero", state => state);
        var sourceResult = fixture.Simulation.Execute(
            sourcePlayer,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    500)),
            1);
        var restoredResult = candidate.Simulation.Execute(
            restoredPlayer,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    500)),
            1);
        Assert.Equal(sourceResult, restoredResult);
        Assert.True(sourceResult.Succeeded, sourceResult.Failure?.ToString());
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
        DiabloPlayerProgression.GrantExperience(sourcePlayer, 1000);
        DiabloPlayerProgression.GrantExperience(restoredPlayer, 1000);
        Assert.Equal(sourcePlayer.CharacterLevel, restoredPlayer.CharacterLevel);
        Assert.Equal(sourcePlayer.Attributes, restoredPlayer.Attributes);
        Assert.Equal(sourcePlayer.LifeMaximum, restoredPlayer.LifeMaximum);
        Assert.Equal(sourcePlayer.ManaMaximum, restoredPlayer.ManaMaximum);
        AssertExactRecapture(
            fixture.Simulation.CaptureCohortState(fixture.Runtime),
            candidate.Simulation.CaptureCohortState(restored));
        var postContinuation = fixture.Simulation.CaptureCohortState(fixture.Runtime);

        var missingItems = ClonePolicyOptions(fixture.Options, classes, null);
        Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.CreateCohortCandidate(missingItems, checkpoint));
        var missingClasses = ClonePolicyOptions(fixture.Options, null, items);
        Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.CreateCohortCandidate(missingClasses, checkpoint));
        var mismatchedClasses = new AuthoritativePlayerClassCatalog(
            classes.Definitions.Select(definition => definition with {
                DisplayName = definition.DisplayName + " (mismatch)",
            }));
        var mismatched = ClonePolicyOptions(fixture.Options, mismatchedClasses, items);
        Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.CreateCohortCandidate(mismatched, checkpoint));

        AssertExactRecapture(
            postContinuation,
            fixture.Simulation.CaptureCohortState(fixture.Runtime));
    }

    private static (
        DiabloSimulationOptions Options,
        DiabloSimulation Simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> Runtime)
        CreateFixture(
            IReadOnlyList<AuthoritativeCombatTarget>? combatTargets = null,
            IReadOnlyList<AuthoritativeProjectile>? projectiles = null,
            IReadOnlyList<AuthoritativePortal>? portals = null,
            IReadOnlyList<AuthoritativeWorldItem>? worldItems = null,
            IReadOnlyList<AuthoritativeCorpse>? corpses = null,
            bool useNativePlayerActionTiming = false,
            IReadOnlyList<AuthoritativeQuestState>? quests = null,
            AuthoritativeQuestTypeCatalog? questTypes = null,
            AuthoritativeQuestRouteMessageCatalog? questRouteMessages = null,
            AuthoritativePlayerClassCatalog? playerClasses = null,
            AuthoritativeItemCatalog? items = null,
            DiabloPlayerTemplate? playerTemplate = null)
    {
        AuthoritativePlayerClassCatalog? classes = playerClasses;
        AuthoritativePlayerAnimationCatalog? animations = null;
        if (useNativePlayerActionTiming) {
            var metadata = LoadShippedPlayerMetadata();
            classes ??= metadata.Classes;
            animations = metadata.Animations;
        }

        var options = new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            FallbackWidth = 8,
            FallbackHeight = 8,
            CombatTargets = combatTargets,
            Projectiles = projectiles,
            Portals = portals,
            WorldItems = worldItems,
            Corpses = corpses,
            Quests = quests,
            QuestTypes = questTypes,
            QuestRouteMessages = questRouteMessages,
            Items = items,
            PlayerClasses = classes,
            PlayerAnimations = animations,
            UseNativePlayerActionTiming = useNativePlayerActionTiming,
            RandomSeed = 71,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var template = playerTemplate ?? DiabloPlayerTemplate.CreateDefault(
                100,
                50,
                0,
                0,
                classes?.Default);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("hero", player => {
            player.EntityId = 1;
            player.CharacterName = "Hero";
            player.PositionX = 1;
            player.PositionY = 1;
            player.Equipment.Add(new EquippedStoreItem(
                0,
                77,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemIndex = 1,
                    ArmorClass = 2,
                    PlusStrength = 3,
                    StatFlag = true,
                }));
            player.RecalculateEquipmentAttributes();
            player.RecalculateEquipmentArmor();
        });
        return (options, simulation, runtime);
    }

    private static AuthoritativeItemCatalog CreatePolicyItems()
    {
        return new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 23,
                MiscId = 44,
                StableSymbol = "pvp_ear",
            },
            new AuthoritativeItemDefinition(
                500,
                1,
                10,
                10,
                1,
                4,
                0,
                10,
                10,
                1,
                1,
                true) {
                EquipLocation = 1,
                ItemClass = 1,
                StableSymbol = "cohort_policy_sword",
            },
        ]);
    }

    private static DiabloPlayerTemplate CreatePolicyTemplate(
        AuthoritativePlayerClassCatalog classes,
        AuthoritativeItemCatalog items)
    {
        var playerClass = classes.Default;
        return new DiabloPlayerTemplate(
            gold: 100,
            experience: 0,
            life: 50,
            mana: 0,
            manaMaximum: 0,
            attributes: playerClass.CreateStartingAttributes(),
            equipment: null,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            belt: null,
            positionX: 0,
            positionY: 0,
            lifeMaximum: 50,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            characterClassId: playerClass.ClassId,
            playerClass: playerClass,
            items: items);
    }

    private static DiabloSimulationOptions ClonePolicyOptions(
        DiabloSimulationOptions source,
        AuthoritativePlayerClassCatalog? playerClasses,
        AuthoritativeItemCatalog? items)
    {
        return new DiabloSimulationOptions(source.StoreCatalog, source.StoreRules) {
            FallbackWidth = source.FallbackWidth,
            FallbackHeight = source.FallbackHeight,
            CombatRules = source.CombatRules,
            PlayerClasses = playerClasses,
            Items = items,
            UseNativePlayerActionTiming = source.UseNativePlayerActionTiming,
            RandomSeed = source.RandomSeed,
            GameMode = source.GameMode,
            Difficulty = source.Difficulty,
        };
    }

    private static void AssertExactRecapture(
        DiabloCohortState expected,
        DiabloCohortState actual)
    {
        Assert.Equal(expected.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.Configuration, actual.Configuration);
        Assert.Equal(expected.RandomState, actual.RandomState);
        Assert.Equal(expected.VictoryTriggered, actual.VictoryTriggered);
        Assert.Equal(expected.LastPortalTick, actual.LastPortalTick);
        Assert.Equal(expected.Allocators, actual.Allocators);
        Assert.Equal(expected.Participants.Count, actual.Participants.Count);
        for (var index = 0; index < expected.Participants.Count; index++) {
            var expectedParticipant = expected.Participants[index];
            var actualParticipant = actual.Participants[index];
            Assert.Equal(expectedParticipant.SessionId, actualParticipant.SessionId);
            Assert.Equal(expectedParticipant.EntityId, actualParticipant.EntityId);
            Assert.Equal(expectedParticipant.LastAppliedTick, actualParticipant.LastAppliedTick);
            Assert.Equal(expectedParticipant.BaseArmorClass, actualParticipant.BaseArmorClass);
            Assert.Equal(expectedParticipant.MaximumAttribute, actualParticipant.MaximumAttribute);
            Assert.Equal(expectedParticipant.GoldTopologyEnabled, actualParticipant.GoldTopologyEnabled);
            Assert.Equal(expectedParticipant.EffectiveArmorClass, actualParticipant.EffectiveArmorClass);
            Assert.Equal(
                expectedParticipant.AppliedEquipmentAttributeBonuses,
                actualParticipant.AppliedEquipmentAttributeBonuses);
            Assert.Equal(
                expectedParticipant.PlayerClassBound,
                actualParticipant.PlayerClassBound);
            Assert.Equal(
                expectedParticipant.ItemCatalogBound,
                actualParticipant.ItemCatalogBound);
            Assert.Equal(
                expectedParticipant.PlayerClassDefinition,
                actualParticipant.PlayerClassDefinition);
            var expectedPlayer = expectedParticipant.Player;
            var actualPlayer = actualParticipant.Player;
            Assert.Equal(expectedPlayer.Gold, actualPlayer.Gold);
            Assert.Equal(expectedPlayer.ActiveStoreId, actualPlayer.ActiveStoreId);
            Assert.Equal(expectedPlayer.Inventory, actualPlayer.Inventory);
            Assert.Equal(expectedPlayer.Experience, actualPlayer.Experience);
            Assert.Equal(expectedPlayer.Life, actualPlayer.Life);
            Assert.Equal(expectedPlayer.Mana, actualPlayer.Mana);
            Assert.Equal(expectedPlayer.ManaMaximum, actualPlayer.ManaMaximum);
            Assert.Equal(expectedPlayer.Attributes, actualPlayer.Attributes);
            Assert.Equal(expectedPlayer.Equipment, actualPlayer.Equipment);
            Assert.Equal(expectedPlayer.InventoryGrid, actualPlayer.InventoryGrid);
            Assert.Equal(expectedPlayer.Belt, actualPlayer.Belt);
            Assert.Equal((expectedPlayer.PositionX, expectedPlayer.PositionY),
                (actualPlayer.PositionX, actualPlayer.PositionY));
            Assert.Equal(expectedPlayer.LifeMaximum, actualPlayer.LifeMaximum);
            Assert.Equal(expectedPlayer.BaseLifeMaximum, actualPlayer.BaseLifeMaximum);
            Assert.Equal(expectedPlayer.BaseManaMaximum, actualPlayer.BaseManaMaximum);
            Assert.Equal(expectedPlayer.CharacterLevel, actualPlayer.CharacterLevel);
            Assert.Equal(expectedPlayer.LevelId, actualPlayer.LevelId);
            Assert.Equal(expectedPlayer.EntityId, actualPlayer.EntityId);
            Assert.Equal(expectedPlayer.CharacterClassId, actualPlayer.CharacterClassId);
            Assert.Equal(expectedPlayer.CharacterName, actualPlayer.CharacterName);
            Assert.Equal(expectedPlayer.ManaShieldActive, actualPlayer.ManaShieldActive);
            Assert.Equal(expectedPlayer.ReflectCharges, actualPlayer.ReflectCharges);
            Assert.Equal(expectedPlayer.DiabloKillLevel, actualPlayer.DiabloKillLevel);
            Assert.Equal(expectedPlayer.StatPoints, actualPlayer.StatPoints);
            Assert.Equal(expectedPlayer.VisitedLevelIds, actualPlayer.VisitedLevelIds);
            Assert.Equal(expectedPlayer.AbilitySpellId, actualPlayer.AbilitySpellId);
            Assert.Equal(expectedPlayer.AbilityKind, actualPlayer.AbilityKind);
            Assert.Equal(expectedPlayer.MemorizedSpells, actualPlayer.MemorizedSpells);
            Assert.Equal(expectedPlayer.StatusEffects, actualPlayer.StatusEffects);
            Assert.Equal(expectedPlayer.FriendlyMode, actualPlayer.FriendlyMode);
            Assert.Equal(expectedPlayer.FacingDirection, actualPlayer.FacingDirection);
            Assert.Equal(expectedPlayer.CombatMode, actualPlayer.CombatMode);
            Assert.Equal(expectedPlayer.CombatModeEndTick, actualPlayer.CombatModeEndTick);
            Assert.Equal(expectedPlayer.CurrentAction, actualPlayer.CurrentAction);
            Assert.Equal(expectedPlayer.HeldItem, actualPlayer.HeldItem);
            Assert.Equal(expectedPlayer.Stash, actualPlayer.Stash);
            Assert.Equal(expectedPlayer.StashGold, actualPlayer.StashGold);
            Assert.Equal(expectedPlayer.StashOpen, actualPlayer.StashOpen);
            Assert.Equal(expectedPlayer.LightRadius, actualPlayer.LightRadius);
        }
        Assert.Equal(expected.CombatTargets, actual.CombatTargets);
        Assert.Equal(expected.WorldItems, actual.WorldItems);
        Assert.Equal(expected.Objects, actual.Objects);
        Assert.Equal(expected.Quests, actual.Quests);
        Assert.Equal(expected.Projectiles, actual.Projectiles);
        Assert.Equal(expected.Portals, actual.Portals);
        Assert.Equal(expected.Corpses, actual.Corpses);
        Assert.Equal(expected.SetLevelObjectOrigins, actual.SetLevelObjectOrigins);
        Assert.Equal(expected.ProceduralQuestPieceOrigins, actual.ProceduralQuestPieceOrigins);
        Assert.Equal(expected.MaterializedProceduralLevelIds, actual.MaterializedProceduralLevelIds);
        Assert.Equal(expected.AppliedNativeDunFragmentIds, actual.AppliedNativeDunFragmentIds);
        Assert.Equal(expected.Stores, actual.Stores);
        Assert.Equal(expected.Geometry, actual.Geometry);
        Assert.Equal(
            expected.WorldInfrastructure.FallbackWidth,
            actual.WorldInfrastructure.FallbackWidth);
        Assert.Equal(
            expected.WorldInfrastructure.FallbackHeight,
            actual.WorldInfrastructure.FallbackHeight);
        Assert.Equal(
            expected.WorldInfrastructure.BlockedCells,
            actual.WorldInfrastructure.BlockedCells);
        Assert.Equal(
            expected.WorldInfrastructure.MissileBlockedCells,
            actual.WorldInfrastructure.MissileBlockedCells);
        Assert.Equal(
            expected.WorldInfrastructure.ActiveWorldMutationIds,
            actual.WorldInfrastructure.ActiveWorldMutationIds);
        Assert.Equal(
            expected.WorldInfrastructure.EffectiveLevels,
            actual.WorldInfrastructure.EffectiveLevels);
    }

    private static void AssertQuestProjection(
        AuthoritativeQuestState expected,
        object actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var projectedType = actual.GetType();
        var publicProperties = typeof(AuthoritativeQuestState)
            .GetProperties(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public)
            .Where(property => property.GetMethod is not null);
        foreach (var property in publicProperties) {
            var projected = projectedType.GetProperty(property.Name);
            Assert.NotNull(projected);
            Assert.Equal(property.GetValue(expected), projected!.GetValue(actual));
        }
    }

    private static (
        AuthoritativePlayerClassCatalog Classes,
        AuthoritativePlayerAnimationCatalog Animations)
        LoadShippedPlayerMetadata()
    {
        var root = FindRepositoryRoot();
        var contentRoot = Path.Combine(root, "server", "content", "base");
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        return (classes, animations);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }
}
