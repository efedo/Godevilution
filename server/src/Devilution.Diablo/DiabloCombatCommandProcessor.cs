using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Applies combat intents and autonomous combat ticks without host dependencies. */
public sealed partial class DiabloCombatCommandProcessor
{
    private enum PlayerDeathReason
    {
        MonsterOrTrap,
        Player,
        Environment,
    }

    private readonly DiabloWorldState world;
    private readonly AuthoritativeSpellCatalog spells;
    private readonly AuthoritativeSpellDefinition? reflectSpell;
    private readonly AuthoritativeCombatRules rules;
    private readonly DeterministicLegacyRandom random;
    private readonly AuthoritativePlayerClassCatalog? playerClasses;
    private readonly AuthoritativePlayerAnimationCatalog? playerAnimations;
    private readonly bool useNativePlayerActionTiming;
    private readonly DiabloGameMode gameMode;
    private readonly DiabloDifficulty difficulty;
    private readonly AuthoritativeItemCatalog? items;
    private readonly AuthoritativeSpellBookCatalog? spellBooks;
    private readonly AuthoritativeMonsterMissileCatalog? monsterMissiles;
    private readonly AuthoritativeMonsterTypeCatalog? monsterTypes;
    private readonly uint monsterFlashSpellId;
    private readonly AuthoritativePlayerDamageResolver playerDamage;
    private readonly DiabloWorldCommandProcessor? worldCommands;
    private readonly AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner;
    private readonly AuthoritativeWorldRules? worldRules;
    private readonly AuthoritativeExperienceCatalog? experience;
    private readonly AuthoritativeQuestDialogueStageCatalog? questDialogueStages;
    private IReadOnlyList<AuthoritativePlayerState> activePlayers = [];
    private readonly DeterministicEntityIdAllocator worldItemEntityIds = new(0x40000000);
    private readonly DeterministicEntityIdAllocator monsterEntityIds = new(0x10000000);
    private readonly DeterministicEntityIdAllocator projectileEntityIds = new(0x80000000);
    private DiabloCommandCallbackPhase currentCommandCallbackPhase;
    private AuthoritativePlayerState? commandCallbackPlayer;

    private const uint FastHitRecoveryFlag = 1U << 21;
    private const uint FasterHitRecoveryFlag = 1U << 22;
    private const uint FastestHitRecoveryFlag = 1U << 23;
    private const uint FastBlockFlag = 1U << 24;
    private const uint FastAttackFlag = 1U << 18;
    private const uint FasterAttackFlag = 1U << 19;
    private const uint FastestAttackFlag = 1U << 20;
    private const uint QuickAttackFlag = 1U << 17;
    private const int NativeReflectDamagePercentMinimum = 20;
    private const int NativeReflectDamagePercentMaximum = 30;

    private static readonly int[] ChargedBoltPath = [
        -1, 0, 1, -1, 0, 1, -1, -1, 0, 0, 1, 1, 0, 1, -1, 0,
    ];

    private static IReadOnlyList<(int X, int Y)> DirectionDisplacements
        => DeterministicGridDirections.AdjacentDisplacements;

    private static readonly int[] SnakeDriftPattern = [1, 1, 0, -1, -1, 0];

    private static readonly (int X, int Y)[] FlashBottomOffsets = [
        (-1, 0),
        (0, 0),
        (1, 0),
        (-1, 1),
        (0, 1),
        (1, 1),
    ];

    private static readonly (int X, int Y)[] FlashTopOffsets = [
        (-1, -1),
        (0, -1),
        (1, -1),
    ];

    // Native ProcessFireball checks these cells in this exact direction order.
    private static readonly (int X, int Y)[] FireballExplosionOffsets = [
        (0, 0),
        (0, 1),
        (0, -1),
        (1, 0),
        (1, -1),
        (1, 1),
        (-1, 0),
        (-1, 1),
        (-1, -1),
    ];

    // Native Elemental checks the center and all eight neighboring cells in this order.
    private static readonly (int X, int Y)[] ElementalBurstOffsets = [
        (0, 0),
        (-1, 1),
        (1, -1),
        (1, 1),
        (1, 0),
        (0, 1),
        (-1, -1),
        (-1, 0),
        (0, -1),
    ];

    private static readonly (int X, int Y)[] GuardianArc = [
        (6, 0), (5, 0), (4, 0), (3, 0), (2, 0), (1, 0),
        (6, 1), (5, 1), (4, 1), (3, 1), (6, 2), (2, 1),
        (5, 2), (6, 3), (4, 2), (5, 3), (3, 2), (1, 1),
        (6, 4), (6, 5), (5, 4), (4, 3), (2, 2), (5, 5),
        (4, 4), (3, 3), (6, 6), (5, 6), (4, 5), (3, 4),
        (2, 3), (4, 6), (3, 5), (2, 4), (1, 2), (3, 6),
        (2, 5), (1, 3), (0, 1), (2, 6), (1, 4), (1, 5),
        (1, 6), (0, 2), (0, 3), (0, 6), (0, 5), (0, 4),
    ];

    private static readonly (int X, int Y)[] ResurrectionInitialOffsets = [
        (0, 0),
        (1, 0),
        (0, 1),
        (1, 1),
        (2, 0),
        (0, 2),
        (1, 2),
        (2, 1),
    ];

    public DiabloCombatCommandProcessor(
        DiabloWorldState world,
        AuthoritativeSpellCatalog spells,
        AuthoritativeCombatRules rules,
        DeterministicLegacyRandom? random = null,
        AuthoritativePlayerClassCatalog? playerClasses = null,
        AuthoritativePlayerAnimationCatalog? playerAnimations = null,
        bool useNativePlayerActionTiming = false,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        AuthoritativeItemCatalog? items = null,
        AuthoritativePlayerDamageResolver? playerDamage = null,
        DiabloWorldCommandProcessor? worldCommands = null,
        AuthoritativeMonsterMissileCatalog? monsterMissiles = null,
        AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner = null,
        AuthoritativeMonsterTypeCatalog? monsterTypes = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal,
        AuthoritativeWorldRules? worldRules = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        uint monsterFlashSpellId = DiabloSimulationOptions.LegacyMonsterFlashSpellId,
        AuthoritativeExperienceCatalog? experience = null,
        AuthoritativeQuestDialogueStageCatalog? questDialogueStages = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        foreach (var projectileEntityId in this.world.Projectiles.Keys)
            projectileEntityIds.Reserve(projectileEntityId);
        this.spells = spells ?? throw new ArgumentNullException(nameof(spells));
        this.reflectSpell = spells.Definitions.FirstOrDefault(definition =>
            definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Reflect);
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        this.random = random ?? new DeterministicLegacyRandom(0);
        this.playerClasses = playerClasses;
        this.playerAnimations = playerAnimations;
        this.useNativePlayerActionTiming = useNativePlayerActionTiming;
        this.gameMode = Enum.IsDefined(gameMode)
            ? gameMode
            : throw new ArgumentOutOfRangeException(nameof(gameMode));
        this.difficulty = Enum.IsDefined(difficulty)
            ? difficulty
            : throw new ArgumentOutOfRangeException(nameof(difficulty));
        this.items = items;
        this.spellBooks = spellBooks;
        this.monsterMissiles = monsterMissiles;
        this.monsterSpawnPlanner = monsterSpawnPlanner;
        this.monsterTypes = monsterTypes;
        this.monsterFlashSpellId = monsterFlashSpellId == 0
            ? throw new ArgumentOutOfRangeException(nameof(monsterFlashSpellId))
            : monsterFlashSpellId;
        this.worldRules = worldRules?.Validate();
        this.experience = experience;
        this.questDialogueStages = questDialogueStages;
        this.playerDamage = playerDamage ?? new AuthoritativePlayerDamageResolver(spells);
        this.worldCommands = worldCommands;
        if (useNativePlayerActionTiming && playerAnimations is null) {
            throw new ArgumentException(
                "Native player action timing requires a player animation catalog.",
                nameof(playerAnimations));
        }
    }

    /** Captures allocator cursors without inferring them from surviving entities. */
    internal DiabloCombatCommandAllocatorState CaptureCohortAllocators()
    {
        return new DiabloCombatCommandAllocatorState(
            worldItemEntityIds.NextId,
            monsterEntityIds.NextId,
            projectileEntityIds.NextId);
    }

    /** Restores exact allocator cursors, including zero exhaustion. */
    internal void RestoreCohortAllocators(DiabloCombatCommandAllocatorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        worldItemEntityIds.RestoreNextId(state.NextWorldItemEntityId);
        monsterEntityIds.RestoreNextId(state.NextMonsterEntityId);
        projectileEntityIds.RestoreNextId(state.NextProjectileEntityId);
    }

    private int GetDifficultyLevelOffset()
        => rules.DifficultyRules.GetLevelOffset(difficulty);

    private int GetTownPortalDestinationPositionX(AuthoritativeSpellDefinition spell)
        => worldRules?.HasTownPortalDestinationGeometry == true
            ? worldRules.TownPortalDestinationPositionX
            : spell.TownPortalDestinationPositionX;

    private int GetTownPortalDestinationPositionY(AuthoritativeSpellDefinition spell)
        => worldRules?.HasTownPortalDestinationGeometry == true
            ? worldRules.TownPortalDestinationPositionY
            : spell.TownPortalDestinationPositionY;

    private int GetTownPortalDestinationSlotCount(AuthoritativeSpellDefinition spell)
        => worldRules?.HasTownPortalDestinationGeometry == true
            ? worldRules.TownPortalDestinationSlotCount
            : spell.TownPortalDestinationSlotCount;

    private int GetTownPortalDestinationSlotSpacing(AuthoritativeSpellDefinition spell)
        => worldRules?.HasTownPortalDestinationGeometry == true
            ? worldRules.TownPortalDestinationSlotSpacing
            : spell.TownPortalDestinationSlotSpacing;

    private int GetDifficultyToHitBonus()
        => rules.DifficultyRules.GetToHitBonus(difficulty);

    private int GetDifficultyArmorClassBonus()
        => rules.DifficultyRules.GetArmorBonus(difficulty);

    /** Resolves native Monster::level without changing persisted base content. */
    private int GetEffectiveMonsterLevel(AuthoritativeCombatTarget monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return checked(monster.MonsterLevel + GetDifficultyLevelOffset());
    }

    /** Resolves native Monster::toHit/toHitSpecial for the active difficulty. */
    private int GetEffectiveMonsterToHit(
        AuthoritativeCombatTarget monster,
        bool special)
    {
        ArgumentNullException.ThrowIfNull(monster);
        var baseToHit = special ? monster.ToHitSpecial : monster.ToHit;
        // Native player minions use the server-owned golem accuracy for their
        // normal attack and do not receive the difficulty accuracy bonus.
        if (!special && monster.IsPlayerMinion)
            return baseToHit;
        return checked(baseToHit + GetDifficultyToHitBonus());
    }

    /** Resolves native monster armor without changing persisted base content. */
    private int GetEffectiveMonsterArmorClass(AuthoritativeCombatTarget monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return checked(monster.ArmorClass + GetDifficultyArmorClassBonus());
    }

    /** Resolves native difficulty damage scaling for one monster channel. */
    private (int Minimum, int Maximum) GetEffectiveMonsterDamageRange(
        AuthoritativeCombatTarget monster,
        bool special)
    {
        ArgumentNullException.ThrowIfNull(monster);
        var minimum = special ? monster.MinDamageSpecial : monster.MinDamage;
        var maximum = special ? monster.MaxDamageSpecial : monster.MaxDamage;
        return (
            rules.DifficultyRules.ApplyDamageScaling(minimum, difficulty),
            rules.DifficultyRules.ApplyDamageScaling(maximum, difficulty));
    }

    /** Selects the native random monster damage after difficulty scaling. */
    private int SelectEffectiveMonsterDamage(
        AuthoritativeCombatTarget monster,
        bool special,
        uint seed)
    {
        var range = GetEffectiveMonsterDamageRange(monster, special);
        return SelectNativeDamage(range.Minimum, range.Maximum, seed);
    }

    /** Native MonsterAttack rolls the effective min/max range at impact. */
    private int RollEffectiveMonsterDamage(
        AuthoritativeCombatTarget monster,
        bool special)
    {
        var range = GetEffectiveMonsterDamageRange(monster, special);
        return random.RandomIntBetween(range.Minimum, range.Maximum);
    }

    /** Rolls native MonsterAttackPlayer damage in its signed 26.6 unit. */
    private int RollEffectiveMonsterDamageQ6(
        AuthoritativeCombatTarget monster,
        bool special)
    {
        var range = GetEffectiveMonsterDamageRange(monster, special);
        return random.RandomIntBetween(
            DeterministicFixedPointQ6.FromWhole(range.Minimum),
            DeterministicFixedPointQ6.FromWhole(range.Maximum));
    }

    /** Resolves native Monster::exp for the active difficulty and uniqueness. */
    private uint GetEffectiveMonsterExperienceReward(
        AuthoritativeCombatTarget monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        var baseReward = monster.ExperienceReward == 0 && !monster.NativeInitializationPresent
            ? rules.DefeatExperience
            : monster.ExperienceReward;
        var multiplier = rules.DifficultyRules.GetExperienceMultiplier(difficulty);
        var difficultyReward = difficulty == DiabloDifficulty.Normal
            ? baseReward
            : checked(
                (baseReward + rules.DifficultyRules.ExperienceBonus) * (uint)multiplier);
        return monster.IsUnique
            ? checked(difficultyReward * (uint)rules.UniqueExperienceMultiplier)
            : difficultyReward;
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick)
    {
        return Execute(player, intent, appliedTick, [player]);
    }

    public DiabloCombatIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(players);
        if (players.Any(candidate => candidate is null)) {
            throw new ArgumentException(
                "Combat participants cannot contain null state.",
                nameof(players));
        }

        activePlayers = players;

        var queueDuringMove = intent is DiabloAttackIntent { QueueDuringMove: true }
            or DiabloAttackAtIntent { QueueDuringMove: true };
        if (queueDuringMove) {
            if (!useNativePlayerActionTiming || !world.SupportsNativeTargetCellPathing)
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            if (player.CurrentAction?.Kind == DiabloPlayerActionKind.Move
                || player.CurrentAction is { Kind: DiabloPlayerActionKind.Attack } rangedAction
                    && player.CombatMode == DiabloPlayerCombatMode.RangedAttack
                    && appliedTick >= rangedAction.StartedAtTick
                    && appliedTick < rangedAction.ReadyTick)
                return QueueNativeRangedAttack(player, intent, players);
        }

        var result = intent switch {
            DiabloAttackIntent attack => Attack(
                player,
                attack.TargetEntityId,
                appliedTick,
                players),
            DiabloAttackAtIntent attackAt => AttackAt(
                player,
                attackAt.TargetX,
                attackAt.TargetY,
                appliedTick,
                players),
            DiabloCastIntent cast => Cast(player, cast, appliedTick, players),
            _ => DiabloCombatIntentResult.Rejected(DiabloRuleFailure.Malformed),
        };
        if (result.Succeeded) {
            player.NativeWalkPath = null;
            player.NativeQueuedRangedAttack = null;
        }
        return result;
    }

    /** Advances one player's status durations to an applied server tick. */
    public static void AdvanceStatusEffects(AuthoritativePlayerState player, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.LastAppliedTick is not ulong previousTick || tick <= previousTick) {
            player.LastAppliedTick = Math.Max(player.LastAppliedTick ?? 0, tick);
            return;
        }

        var elapsed = tick - previousTick;
        player.StatusEffects = player.StatusEffects
            .Select(effect => effect.Behavior == AuthoritativeStatusEffectBehavior.Etherealize
                ? effect
                : effect with {
                    RemainingTicks = effect.RemainingTicks > elapsed
                        ? effect.RemainingTicks - (uint)elapsed
                        : 0,
                })
            .Where(effect => effect.RemainingTicks > 0)
            .ToList();
        player.LastAppliedTick = tick;
    }

    /** Expires one server-owned native combat mode at its first inactive tick. */
    public static void AdvanceCombatMode(AuthoritativePlayerState player, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.CurrentAction is not null
            || player.CombatModeEndTick == 0
            || tick < player.CombatModeEndTick) {
            return;
        }
        player.CombatMode = DiabloPlayerCombatMode.Stand;
        player.CombatModeEndTick = 0;
    }

    /** Applies native block/hit recovery durability rolls before mode expiry. */
    private bool AdvanceCombatModeWithEquipmentDamage(
        AuthoritativePlayerState player,
        ulong tick)
    {
        if (player.CurrentAction is not null
            || player.CombatModeEndTick == 0
            || tick < player.CombatModeEndTick) {
            return false;
        }

        var completed = player.CombatMode is DiabloPlayerCombatMode.Block
            or DiabloPlayerCombatMode.GotHit;

        if (player.CombatMode == DiabloPlayerCombatMode.Block) {
            if (HasNativeParryDurabilityTarget(player)
                && random.FlipCoin(10))
                ApplyNativeParryDurabilityLoss(player);
        } else if (player.CombatMode == DiabloPlayerCombatMode.GotHit
            && HasNativeArmorDurabilityTarget(player)
            && !random.FlipCoin(4)) {
            ApplyNativeArmorDurabilityLoss(player);
        }

        AdvanceCombatMode(player, tick);
        return completed;
    }

    /** Applies one deterministic autonomous world tick in caller-provided player order. */
    public IReadOnlyList<DiabloDirectedGameplayEvent> AdvanceTick(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Any(player => player is null))
            throw new ArgumentException("Combat tick players cannot contain null state.", nameof(players));

        activePlayers = players;

        world.AdvancePortals(tick);
        var events = new List<DiabloDirectedGameplayEvent>();
        var completedPriorModes = new bool[players.Count];
        for (var index = 0; index < players.Count; index++) {
            var player = players[index];
            AdvancePlayerEquipmentDamage(player, tick, players, events);
            completedPriorModes[index] = AdvancePlayerAction(player, tick, players, events);
        }
        for (var index = 0; index < players.Count; index++)
            completedPriorModes[index] |= AdvanceCombatModeWithEquipmentDamage(players[index], tick);
        for (var index = 0; index < players.Count; index++) {
            var player = players[index];
            AdvanceNativeQueuedRangedAttack(player, tick, players, completedPriorModes[index], events);
            if (worldCommands is not null) {
                AddDirectedEvents(player,
                    worldCommands.AdvanceNativeWalkContinuation(
                        player,
                        tick,
                        players,
                        completedPriorModes[index] ? 2 : 1),
                    events);
            }
        }
        AdvanceMonsters(tick, players, events);
        AdvanceNativeTraps(players);
        AdvanceProjectiles(tick, players, events);
        return events;
    }

    /**
     * Advances one tick while exposing an ordered command callback at each
     * player's primary-action scheduling point. Legacy AdvanceTick remains
     * unchanged; this seam is used only by the Engine ExecuteTick path.
     */
    public IReadOnlyList<DiabloDirectedGameplayEvent> AdvanceTickWithCommands(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        Func<AuthoritativePlayerState, IReadOnlyList<DiabloDirectedGameplayEvent>> executeCommands)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(executeCommands);
        if (players.Any(player => player is null))
            throw new ArgumentException("Combat tick players cannot contain null state.", nameof(players));

        activePlayers = players;

        world.AdvancePortals(tick);
        var events = new List<DiabloDirectedGameplayEvent>();
        foreach (var player in players) {
            AdvancePlayerEquipmentDamage(player, tick, players, events);
            var completedAction = AdvancePlayerAction(player, tick, players, events);
            var completedPriorMode = completedAction
                || AdvanceCombatModeWithEquipmentDamage(player, tick);
            currentCommandCallbackPhase = completedPriorMode
                ? DiabloCommandCallbackPhase.CompletionLoopStart
                : IsReadyRepeat(player, tick)
                    ? DiabloCommandCallbackPhase.AttackOrRangedReadyRepeat
                    : IsSpellReadmission(player, tick)
                        ? DiabloCommandCallbackPhase.SpellReadmission
                        : DiabloCommandCallbackPhase.OrdinaryCommandSlot;
            commandCallbackPlayer = player;
            try {
                var callbackEvents = executeCommands(player)
                    ?? throw new InvalidOperationException(
                        "A command callback returned a null event collection.");
                events.AddRange(callbackEvents);
            } finally {
                commandCallbackPlayer = null;
                currentCommandCallbackPhase = DiabloCommandCallbackPhase.OrdinaryCommandSlot;
            }

            AdvanceNativeQueuedRangedAttack(player, tick, players, completedPriorMode, events);

            // Native completes the old mode before the callback. If that
            // callback started a replacement action with an effect due now,
            // resolve that immediate work before the next actor is visited.
            if (completedPriorMode)
                AdvancePlayerAction(player, tick, players, events);
            if (worldCommands is not null)
                AddDirectedEvents(player,
                    worldCommands.AdvanceNativeWalkContinuation(
                        player, tick, players, completedPriorMode ? 2 : 1), events);
        }
        AdvanceMonsters(tick, players, events);
        AdvanceNativeTraps(players);
        AdvanceProjectiles(tick, players, events);
        return events;
    }

    private static bool IsReadyRepeat(AuthoritativePlayerState player, ulong tick)
    {
        var action = player.CurrentAction;
        return action is not null
            && tick >= action.ReadyTick
            && (player.CombatMode is DiabloPlayerCombatMode.Attack
                or DiabloPlayerCombatMode.RangedAttack);
    }

    internal DiabloCommandCallbackPhase? GetCurrentCommandCallbackPhase(
        AuthoritativePlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return useNativePlayerActionTiming
            && ReferenceEquals(commandCallbackPlayer, player)
                ? currentCommandCallbackPhase
                : null;
    }

    private static bool IsSpellReadmission(AuthoritativePlayerState player, ulong tick)
    {
        var action = player.CurrentAction;
        return action is not null
            && action.Kind == DiabloPlayerActionKind.Cast
            && player.CombatMode == DiabloPlayerCombatMode.Spell
            && action.EffectApplied
            && tick >= action.ReadyTick;
    }

    private bool GetIncludesFirstFrame(AuthoritativePlayerState player)
    {
        if (ReferenceEquals(commandCallbackPlayer, player)) {
            return currentCommandCallbackPhase switch {
                DiabloCommandCallbackPhase.AttackOrRangedReadyRepeat => false,
                DiabloCommandCallbackPhase.CompletionLoopStart => true,
                _ => false,
            };
        }

        return player.CurrentAction is not null
            && player.CurrentAction.EffectApplied
            && player.CombatMode is DiabloPlayerCombatMode.Attack
                or DiabloPlayerCombatMode.RangedAttack;
    }

    /** Applies native item damage that occurs once per active non-town tick. */
    private void AdvancePlayerEquipmentDamage(
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (player.Life <= 0
            || player.LevelId == 0
            || !HasUsableEquipmentFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.DrainLifeFlag)) {
            return;
        }

        var damageResult = playerDamage.Apply(player, 4);
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                player,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    player.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (player.Life == 0)
            StartPlayerDeath(
                player,
                player.EntityId,
                tick,
                players,
                PlayerDeathReason.Environment);
    }
}
