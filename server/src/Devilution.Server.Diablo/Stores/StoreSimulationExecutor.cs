using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;

namespace Devilution.Server.Stores;

/**
 * Protocol adapter and authoritative simulation composition for the current
 * gameplay vertical slice.
 *
 * Gameplay commands are translated into module-owned Diablo intents. The host
 * supplies stable session order and tick numbers for autonomous transitions.
 * The outer command server provides command-level deduplication, so each intent
 * is applied once even when a client command is retried.
 */
public sealed class StoreSimulationExecutor : IAuthoritativeGameHostAdapter
{
    /** Reserved service-only store used by Adria's mana refill action. */
    public const uint AdriaStoreId = DiabloStoreCommandProcessor.AdriaStoreId;
    private readonly DiabloSimulation simulation;
    private readonly AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime;
    private readonly AuthoritativeSaveCodec saveCodec;
    private readonly Func<string, uint?, DiabloPlayerTemplate> playerTemplateFactory;

    /** Creates the protocol adapter over module-activated content and state. */
    public StoreSimulationExecutor(
        DiabloContentActivation content,
        DiabloPlayerTemplate playerTemplate,
        AuthoritativeSaveCodec? saveCodec = null)
        : this(content, CreateFixedPlayerTemplateFactory(playerTemplate), saveCodec)
    {
    }

    /** Creates an adapter that can select a validated template per session. */
    public StoreSimulationExecutor(
        DiabloContentActivation content,
        Func<string, uint?, DiabloPlayerTemplate> playerTemplateFactory,
        AuthoritativeSaveCodec? saveCodec = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(playerTemplateFactory);
        simulation = content.Simulation;
        this.playerTemplateFactory = playerTemplateFactory;
        var defaultTemplate = playerTemplateFactory("default", null)
            ?? throw new InvalidOperationException(
                "The player-template factory returned no default template.");
        runtime = simulation.CreateRuntime(defaultTemplate);
        this.saveCodec = saveCodec ?? new AuthoritativeSaveCodec(simulation.Items);
    }

    /** Compatibility constructor retained for focused host-adapter tests. */
    public StoreSimulationExecutor(
        StoreCatalog catalog,
        uint startingGold,
        uint startingExperience = 0,
        int startingLife = 0,
        int startingMana = 0,
        PlayerAttributesState? startingAttributes = null,
        IReadOnlyList<EquippedStoreItem>? startingEquipment = null,
        IReadOnlyList<int>? startingInventoryGrid = null,
        IStoreGameplayRules? gameplayRules = null,
        IReadOnlyList<BeltStoreItem>? startingBelt = null,
        int? startingManaMaximum = null,
        int startingPositionX = 0,
        int startingPositionY = 0,
        int? startingLifeMaximum = null,
        uint startingCharacterLevel = 1,
        IReadOnlyList<AuthoritativeCombatTarget>? startingCombatTargets = null,
        int worldWidth = 40,
        int worldHeight = 40,
        uint startingLevelId = 0,
        IReadOnlyList<int>? startingBlockedCells = null,
        IReadOnlyList<AuthoritativePortal>? startingPortals = null,
        AuthoritativeWorld? startingWorld = null,
        AuthoritativeSpellCatalog? startingSpells = null,
        AuthoritativeCombatRules? startingCombatRules = null,
        IReadOnlyList<AuthoritativeWorldItem>? startingWorldItems = null,
        IReadOnlyList<AuthoritativeProjectile>? startingProjectiles = null,
        IReadOnlyList<AuthoritativeCorpse>? startingCorpses = null,
        IReadOnlyList<AuthoritativeWorldObject>? startingObjects = null,
        IReadOnlyList<AuthoritativeQuestState>? startingQuests = null,
        int startingArmorClass = 0,
        IDiabloStoreRules? diabloRules = null,
        IDiabloSimulationFactory? simulationFactory = null,
        AuthoritativeSaveCodec? saveCodec = null,
        uint startingCharacterClassId = 0,
        IReadOnlyList<OwnedStoreItem>? startingInventory = null,
        uint startingAbilitySpellId = 0,
        IReadOnlyList<MemorizedSpellState>? startingMemorizedSpells = null,
        uint startingRandomSeed = 0,
        AuthoritativePlayerClassCatalog? startingPlayerClasses = null,
        AuthoritativePlayerAnimationCatalog? startingPlayerAnimations = null,
        IReadOnlyList<int>? startingMissileBlockedCells = null,
        bool useNativePlayerActionTiming = false,
        OwnedStoreItem? startingHeldItem = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        DiabloPlayerDirection startingFacingDirection = DiabloPlayerDirection.South,
        string startingCharacterName = "",
        AuthoritativeItemCatalog? startingItems = null,
        bool startingManaShieldActive = false,
        uint identifyAbilitySpellId = 0,
        uint repairAbilitySpellId = 0,
        uint rechargeAbilitySpellId = 0,
        uint trapDisarmAbilitySpellId = 0,
        uint startingReflectCharges = 0,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal,
        AuthoritativeMonsterMissileCatalog? startingMonsterMissiles = null,
        AuthoritativeMonsterTypeCatalog? startingMonsterTypes = null,
        AuthoritativePlayerClassDefinition? startingPlayerClass = null,
        AuthoritativeItemQuestEffectCatalog? startingItemQuestEffects = null,
        uint startingStatPoints = 0,
        IReadOnlyList<StashStoreItem>? startingStash = null,
        uint startingStashGold = 0,
        AuthoritativeMonsterSpawnPlanner? startingMonsterSpawnPlanner = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var storeRules = diabloRules ?? (gameplayRules switch {
            null => DiabloModule.Instance.Rules,
            DiabloGameplayModule module => module.Rules,
            _ => new StoreGameplayRulesBridge(gameplayRules),
        });
        simulation = (simulationFactory ?? DiabloSimulationFactory.Instance).Create(
            new DiabloSimulationOptions(catalog, storeRules) {
                World = startingWorld,
                FallbackWidth = worldWidth,
                FallbackHeight = worldHeight,
                FallbackBlockedCells = startingBlockedCells,
                FallbackMissileBlockedCells = startingMissileBlockedCells,
                CombatTargets = startingCombatTargets,
                Portals = startingPortals,
                WorldItems = startingWorldItems,
                Projectiles = startingProjectiles,
                Corpses = startingCorpses,
                Objects = startingObjects,
                Quests = startingQuests,
                ItemQuestEffects = startingItemQuestEffects,
                Spells = startingSpells,
                CombatRules = startingCombatRules,
                Items = startingItems,
                IdentifyAbilitySpellId = identifyAbilitySpellId,
                RepairAbilitySpellId = repairAbilitySpellId,
                RechargeAbilitySpellId = rechargeAbilitySpellId,
                TrapDisarmAbilitySpellId = trapDisarmAbilitySpellId,
                PlayerClasses = startingPlayerClasses,
                PlayerAnimations = startingPlayerAnimations,
                UseNativePlayerActionTiming = useNativePlayerActionTiming,
                RandomSeed = startingRandomSeed,
                GameMode = gameMode,
                Difficulty = difficulty,
                MonsterMissiles = startingMonsterMissiles,
                MonsterTypes = startingMonsterTypes,
                MonsterSpawnPlanner = startingMonsterSpawnPlanner,
            });
        var compatibilityTemplate = new DiabloPlayerTemplate(
            startingGold,
            startingExperience,
            startingLife,
            startingMana,
            startingManaMaximum ?? startingMana,
            startingAttributes,
            startingEquipment,
            startingInventoryGrid,
            startingBelt,
            startingPositionX,
            startingPositionY,
            startingLifeMaximum ?? startingLife,
            startingCharacterLevel,
            startingLevelId,
            startingArmorClass,
            startingCharacterClassId,
            startingInventory,
            startingAbilitySpellId,
            startingMemorizedSpells,
            heldItem: startingHeldItem,
            facingDirection: startingFacingDirection,
            characterName: startingCharacterName,
            manaShieldActive: startingManaShieldActive,
            reflectCharges: startingReflectCharges,
            items: startingItems,
            playerClass: startingPlayerClass,
            statPoints: startingStatPoints,
            stash: startingStash,
            stashGold: startingStashGold);
        playerTemplateFactory = (_, _) => compatibilityTemplate;
        runtime = simulation.CreateRuntime(compatibilityTemplate);
        this.saveCodec = saveCodec ?? new AuthoritativeSaveCodec(simulation.Items);
    }

    public void InitializeParticipant(
        string sessionId,
        AuthoritativeParticipantSelection selection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(selection);
        var template = playerTemplateFactory(sessionId, selection.ArchetypeId)
            ?? throw new InvalidOperationException(
                "The player-template factory returned no selected template.");
        if (!string.IsNullOrEmpty(selection.CharacterName))
            template = template.WithCharacterName(selection.CharacterName);
        runtime.AddParticipant(sessionId, template.CreatePlayer());
    }

    /** Registers an explicitly constructed module template for deterministic replay setup. */
    public void InitializeParticipant(
        string sessionId,
        DiabloPlayerTemplate template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(template);
        runtime.AddParticipant(sessionId, template.CreatePlayer());
    }

    private static Func<string, uint?, DiabloPlayerTemplate> CreateFixedPlayerTemplateFactory(
        DiabloPlayerTemplate playerTemplate)
    {
        ArgumentNullException.ThrowIfNull(playerTemplate);
        return (_, _) => playerTemplate;
    }

    public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(sessionId))
            return CommandExecutionResult.Rejected(CommandRejectReason.Malformed);

        return runtime.Execute(
            sessionId,
            appliedTick,
            (player, participants) => command.IntentCase switch {
                Command.IntentOneofCase.OpenStoreRequested => ExecuteStoreIntent(
                    player,
                    new DiabloOpenStoreIntent(command.OpenStoreRequested.StoreId),
                    appliedTick),
                Command.IntentOneofCase.CloseStoreRequested => ExecuteStoreIntent(
                    player,
                    new DiabloCloseStoreIntent(),
                    appliedTick),
                Command.IntentOneofCase.OpenStashRequested => ExecuteStoreIntent(
                    player,
                    new DiabloOpenStashIntent(),
                    appliedTick),
                Command.IntentOneofCase.CloseStashRequested => ExecuteStoreIntent(
                    player,
                    new DiabloCloseStashIntent(),
                    appliedTick),
                Command.IntentOneofCase.DepositStashGoldRequested => ExecuteStoreIntent(
                    player,
                    new DiabloDepositStashGoldIntent(
                        command.DepositStashGoldRequested.Amount),
                    appliedTick),
                Command.IntentOneofCase.WithdrawStashGoldRequested => ExecuteStoreIntent(
                    player,
                    new DiabloWithdrawStashGoldIntent(
                        command.WithdrawStashGoldRequested.Amount),
                    appliedTick),
                Command.IntentOneofCase.SplitGoldRequested => ExecuteSplitGoldIntent(
                    player,
                    command.SplitGoldRequested,
                    appliedTick),
                Command.IntentOneofCase.SortInventoryRequested => ExecuteStoreIntent(
                    player,
                    new DiabloSortInventoryIntent(),
                    appliedTick),
                Command.IntentOneofCase.PurchaseRequested => ExecuteStoreIntent(
                    player,
                    new DiabloPurchaseIntent(
                        command.PurchaseRequested.StoreId,
                        command.PurchaseRequested.StoreSlot),
                    appliedTick),
                Command.IntentOneofCase.SellItemRequested => ExecuteItemServiceIntent(
                    player,
                    command.SellItemRequested.Item,
                    command.SellItemRequested.InventoryIndex,
                    static (item, index) => new DiabloSellItemIntent(item, index),
                    appliedTick),
                Command.IntentOneofCase.RepairItemRequested => ExecuteRepairItemIntent(
                    player,
                    command.RepairItemRequested,
                    appliedTick),
                Command.IntentOneofCase.RechargeItemRequested => ExecuteRechargeItemIntent(
                    player,
                    command.RechargeItemRequested,
                    appliedTick),
                Command.IntentOneofCase.IdentifyItemRequested => ExecuteIdentifyItemIntent(
                    player,
                    command.IdentifyItemRequested,
                    appliedTick),
                Command.IntentOneofCase.ReadBookRequested => ExecuteReadBookIntent(
                    player,
                    command.ReadBookRequested,
                    appliedTick),
                Command.IntentOneofCase.UseConsumableRequested => ExecuteUseConsumableIntent(
                    player,
                    command.UseConsumableRequested,
                    appliedTick),
                Command.IntentOneofCase.MoveInventoryItemRequested => ExecuteMoveInventoryItemIntent(
                    player,
                    command.MoveInventoryItemRequested,
                    appliedTick),
                Command.IntentOneofCase.MoveItemRequested => ExecuteMoveItemIntent(
                    player,
                    command.MoveItemRequested,
                    appliedTick),
                Command.IntentOneofCase.AutoEquipItemRequested => ExecuteAutoEquipItemIntent(
                    player,
                    command.AutoEquipItemRequested,
                    appliedTick),
                Command.IntentOneofCase.RefillManaRequested => ExecuteStoreIntent(
                    player,
                    new DiabloRefillManaIntent(),
                    appliedTick),
                Command.IntentOneofCase.MoveRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    command.MoveRequested.TargetCell
                        ? new DiabloMoveToIntent(
                            command.MoveRequested.TargetX,
                            command.MoveRequested.TargetY)
                        : new DiabloMoveIntent(
                            command.MoveRequested.DirectionX,
                            command.MoveRequested.DirectionY),
                    appliedTick),
                Command.IntentOneofCase.CastRequested => ExecuteCastIntent(
                    player,
                    participants,
                    command.CastRequested,
                    appliedTick),
                Command.IntentOneofCase.AttackRequested => ExecuteCombatIntent(
                    player,
                    participants,
                    command.AttackRequested.TargetCell
                        ? new DiabloAttackAtIntent(
                            command.AttackRequested.TargetX,
                            command.AttackRequested.TargetY)
                        : new DiabloAttackIntent(command.AttackRequested.TargetEntityId),
                    appliedTick),
                Command.IntentOneofCase.UsePortalRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloUsePortalIntent(command.UsePortalRequested.PortalId),
                    appliedTick),
                Command.IntentOneofCase.PickupWorldItemRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloPickupWorldItemIntent(
                        command.PickupWorldItemRequested.ItemEntityId),
                    appliedTick),
                Command.IntentOneofCase.DropHeldItemRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloDropHeldItemIntent(
                        command.DropHeldItemRequested.TargetX,
                        command.DropHeldItemRequested.TargetY),
                    appliedTick),
                Command.IntentOneofCase.DropItemRequested => ExecuteDropItemIntent(
                    player,
                    participants,
                    command.DropItemRequested,
                    appliedTick),
                Command.IntentOneofCase.OperateObjectRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloOperateObjectIntent(
                        command.OperateObjectRequested.ObjectEntityId),
                    appliedTick),
                Command.IntentOneofCase.DisarmTrapRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloDisarmTrapIntent(
                        command.DisarmTrapRequested.ObjectEntityId),
                    appliedTick),
                Command.IntentOneofCase.AdvanceQuestRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloAdvanceQuestIntent(command.AdvanceQuestRequested.QuestId),
                    appliedTick),
                Command.IntentOneofCase.TalkToQuestRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloTalkToQuestIntent(
                        command.TalkToQuestRequested.QuestId,
                        command.TalkToQuestRequested.TargetEntityId),
                    appliedTick),
                Command.IntentOneofCase.TurnInQuestRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloTurnInQuestIntent(
                        command.TurnInQuestRequested.QuestId,
                        command.TurnInQuestRequested.TownerId),
                    appliedTick),
                Command.IntentOneofCase.TalkToTownerRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloTalkToTownerIntent(
                        checked((int)command.TalkToTownerRequested.TownerId)),
                    appliedTick),
                Command.IntentOneofCase.SetFriendlyModeRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloSetFriendlyModeIntent(
                        command.SetFriendlyModeRequested.FriendlyMode),
                    appliedTick),
                Command.IntentOneofCase.AllocateAttributeRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    CreateAllocateAttributeIntent(command.AllocateAttributeRequested),
                    appliedTick),
                Command.IntentOneofCase.RestartTownRequested => ExecuteWorldIntent(
                    player,
                    participants,
                    new DiabloRestartTownIntent(),
                    appliedTick),
                _ => new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                    CommandExecutionResult.Rejected(CommandRejectReason.Malformed)),
            });
    }

    private static DiabloAllocateAttributeIntent CreateAllocateAttributeIntent(
        AllocateAttributeRequested request)
    {
        var attribute = request.Attribute switch {
            PlayerAttribute.Strength => DiabloPlayerAttribute.Strength,
            PlayerAttribute.Magic => DiabloPlayerAttribute.Magic,
            PlayerAttribute.Dexterity => DiabloPlayerAttribute.Dexterity,
            PlayerAttribute.Vitality => DiabloPlayerAttribute.Vitality,
            _ => (DiabloPlayerAttribute)0,
        };
        return new DiabloAllocateAttributeIntent(attribute, request.Amount);
    }

    public StorePlayerSnapshot GetPlayerState(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        return runtime.Read(sessionId, simulation.CapturePlayerState);
    }

    public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        return runtime.Capture(sessionId, tick, (state, participants) => {
            state.EntityId = entityId;
            var participantSnapshots = participants
                .Where(participant => participant.EntityId != 0)
                .Select(simulation.CapturePlayerState)
                .ToArray();
            return DiabloProtocolMapper.ToProtocolSnapshot(
                simulation.CaptureSnapshot(state, tick),
                participantSnapshots,
                state.PlayerClass?.Flags.HasFlag(DiabloPlayerClassFlags.TrapSense) ?? true,
                simulation.Spells,
                simulation.SpellBooks,
                simulation.Items,
                simulation.WorldState.LevelGeometry,
                simulation.WorldState.World?.WarpTargets,
                allowZeroPlayerEntityId: entityId == 0);
        });
    }

    /**
     * Serializes complete authoritative session state for persistence or migration.
     * Capturing at the last advanced tick flushes native-timed effects before save.
     */
    public string ExportPlayerSave(string sessionId)
    {
        var tick = runtime.LastAdvancedTick;
        var snapshot = runtime.Capture(
            sessionId,
            tick,
            state => simulation.CaptureSnapshot(state, tick));
        return saveCodec.Serialize(snapshot);
    }

    /** Exports the complete player and authoritative world state for migration. */
    public string ExportSessionState(string sessionId)
    {
        return ExportPlayerSave(sessionId);
    }

    /** Restores a validated save into an existing session without changing world configuration. */
    public void ImportPlayerSave(string sessionId, string serializedSave)
    {
        var snapshot = saveCodec.Deserialize(serializedSave);
        runtime.WriteOrAddParticipant(
            sessionId,
            () => {
                var savedTemplate = playerTemplateFactory(
                    sessionId,
                    snapshot.Player.CharacterClassId)
                    ?? throw new InvalidOperationException(
                        "The player-template factory returned no saved-class template.");
                return savedTemplate.CreatePlayer();
            },
            player => simulation.RestoreSnapshot(player, snapshot));
        runtime.RestoreAtTick(snapshot.Tick);
    }

    /** Restores the complete player and authoritative world state for migration. */
    public void ImportSessionState(string sessionId, string serializedState)
    {
        ImportPlayerSave(sessionId, serializedState);
    }

    /** Advances statuses and autonomous monster behavior at a server tick boundary. */
    public void AdvanceTo(ulong tick)
    {
        runtime.AdvanceTo(tick);
    }

    public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        return DiabloProtocolMapper.ToProtocolEventBatch(
            runtime.DrainEvents(sessionId),
            entityId,
            tick,
            simulation.DialogueMessages);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteStoreIntent(
        AuthoritativePlayerState player,
        DiabloStoreIntent intent,
        ulong appliedTick)
    {
        var result = simulation.Execute(player, intent, appliedTick);
        if (result.Succeeded)
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Accepted,
                result.Events);

        var rejectReason = DiabloGameplayModule.ToCommandRejectReason(result.Failure)
            ?? throw new InvalidOperationException("A rejected Diablo intent must provide a reason.");
        return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
            CommandExecutionResult.Rejected(rejectReason),
            result.Events);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteWorldIntent(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants,
        DiabloWorldIntent intent,
        ulong appliedTick)
    {
        var result = simulation.Execute(player, intent, appliedTick, participants);
        if (result.Succeeded) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Accepted,
                result.Events);
        }

        var rejectReason = DiabloGameplayModule.ToCommandRejectReason(result.Failure)
            ?? throw new InvalidOperationException("A rejected Diablo intent must provide a reason.");
        return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
            CommandExecutionResult.Rejected(rejectReason),
            result.Events);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteCombatIntent(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants,
        DiabloCombatIntent intent,
        ulong appliedTick)
    {
        var result = simulation.Execute(player, intent, appliedTick, participants);
        if (result.Succeeded) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Accepted,
                result.Events);
        }

        var rejectReason = DiabloGameplayModule.ToCommandRejectReason(result.Failure)
            ?? throw new InvalidOperationException("A rejected Diablo intent must provide a reason.");
        return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
            CommandExecutionResult.Rejected(rejectReason),
            result.Events);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteCastIntent(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants,
        CastRequested request,
        ulong appliedTick)
    {
        var source = request.Source switch {
            CastSourceKind.Unspecified => DiabloCastSourceKind.Unspecified,
            CastSourceKind.Ability => DiabloCastSourceKind.Ability,
            CastSourceKind.MemorizedSpell => DiabloCastSourceKind.MemorizedSpell,
            CastSourceKind.ChargedItem => DiabloCastSourceKind.ChargedItem,
            CastSourceKind.ConsumableItem => DiabloCastSourceKind.ConsumableItem,
            _ => (DiabloCastSourceKind?)null,
        };
        if (!source.HasValue
            || !TryMapItemReference(request.SourceItem, out var sourceItem)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }

        return ExecuteCombatIntent(
            player,
            participants,
            new DiabloCastIntent(
                request.SpellId,
                request.TargetEntityId,
                request.TargetX,
                request.TargetY,
                source.Value,
                sourceItem),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteItemServiceIntent(
        AuthoritativePlayerState player,
        PlayerItemReference? reference,
        uint inventoryIndex,
        Func<DiabloPlayerItemReference?, uint, DiabloStoreIntent> createIntent,
        ulong appliedTick)
    {
        if (!TryMapItemReference(reference, out var item)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        return ExecuteStoreIntent(player, createIntent(item, inventoryIndex), appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteIdentifyItemIntent(
        AuthoritativePlayerState player,
        IdentifyItemRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        if (!TryMapItemServiceSource(request.Source, out var source)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        if (!TryMapItemReference(request.SourceItem, out var sourceItem)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        return ExecuteStoreIntent(
            player,
            new DiabloIdentifyItemIntent(item, request.InventoryIndex, source, sourceItem),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteReadBookIntent(
        AuthoritativePlayerState player,
        ReadBookRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        return ExecuteStoreIntent(
            player,
            new DiabloReadBookIntent(item, request.InventoryIndex),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteUseConsumableIntent(
        AuthoritativePlayerState player,
        UseConsumableRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item) || !item.HasValue) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        if (!TryMapItemReference(request.TargetItem, out var target)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        return ExecuteStoreIntent(
            player,
            new DiabloUseConsumableIntent(item.Value, target),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteMoveInventoryItemIntent(
        AuthoritativePlayerState player,
        MoveInventoryItemRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        return ExecuteStoreIntent(
            player,
            new DiabloMoveInventoryItemIntent(item, request.InventoryIndex, request.TargetCell),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteRepairItemIntent(
        AuthoritativePlayerState player,
        RepairItemRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        if (!TryMapItemServiceSource(request.Source, out var source)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        return ExecuteStoreIntent(
            player,
            new DiabloRepairItemIntent(item, request.InventoryIndex, source),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteRechargeItemIntent(
        AuthoritativePlayerState player,
        RechargeItemRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        if (!TryMapItemServiceSource(request.Source, out var source)) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }
        return ExecuteStoreIntent(
            player,
            new DiabloRechargeItemIntent(item, request.InventoryIndex, source),
            appliedTick);
    }

    private static bool TryMapItemServiceSource(
        ItemServiceSourceKind source,
        out DiabloItemServiceSourceKind mapped)
    {
        mapped = source switch {
            ItemServiceSourceKind.Unspecified => DiabloItemServiceSourceKind.Vendor,
            ItemServiceSourceKind.ClassAbility => DiabloItemServiceSourceKind.ClassAbility,
            ItemServiceSourceKind.ConsumableItem => DiabloItemServiceSourceKind.ConsumableItem,
            _ => (DiabloItemServiceSourceKind)(-1),
        };
        return Enum.IsDefined(mapped);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteMoveItemIntent(
        AuthoritativePlayerState player,
        MoveItemRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item)
            || !item.HasValue
            || !TryMapItemReference(request.Destination, out var destination)
            || !destination.HasValue) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }

        return ExecuteStoreIntent(
            player,
            new DiabloMoveItemIntent(item.Value, destination.Value),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteDropItemIntent(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants,
        DropItemRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item)
            || !item.HasValue) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }

        return ExecuteWorldIntent(
            player,
            participants,
            new DiabloDropItemIntent(
                item.Value,
                request.TargetX,
                request.TargetY),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteSplitGoldIntent(
        AuthoritativePlayerState player,
        SplitGoldRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item) || !item.HasValue) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }

        return ExecuteStoreIntent(
            player,
            new DiabloSplitGoldIntent(item.Value, request.Amount),
            appliedTick);
    }

    private SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent> ExecuteAutoEquipItemIntent(
        AuthoritativePlayerState player,
        AutoEquipItemRequested request,
        ulong appliedTick)
    {
        if (!TryMapItemReference(request.Item, out var item) || !item.HasValue) {
            return new SimulationStepResult<CommandExecutionResult, DiabloGameplayEvent>(
                CommandExecutionResult.Rejected(CommandRejectReason.Malformed));
        }

        return ExecuteStoreIntent(
            player,
            new DiabloAutoEquipItemIntent(
                item.Value,
                request.HasEquipmentSlot ? request.EquipmentSlot : null),
            appliedTick);
    }

    private static bool TryMapItemReference(
        PlayerItemReference? source,
        out DiabloPlayerItemReference? destination)
    {
        if (source is null) {
            destination = null;
            return true;
        }

        var location = source.Location switch {
            PlayerItemLocation.Inventory => DiabloPlayerItemLocation.Inventory,
            PlayerItemLocation.Belt => DiabloPlayerItemLocation.Belt,
            PlayerItemLocation.Equipment => DiabloPlayerItemLocation.Equipment,
            PlayerItemLocation.Stash => DiabloPlayerItemLocation.Stash,
            _ => (DiabloPlayerItemLocation?)null,
        };
        if (!location.HasValue) {
            destination = null;
            return false;
        }

        destination = new DiabloPlayerItemReference(
            location.Value,
            source.Slot,
            source.StableItemId,
            source.Page);
        return true;
    }

}
