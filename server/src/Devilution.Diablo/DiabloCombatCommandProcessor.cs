using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Applies combat intents and autonomous combat ticks without host dependencies. */
public sealed class DiabloCombatCommandProcessor
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
        var baseReward = monster.ExperienceReward == 0
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

        return intent switch {
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
    private void AdvanceCombatModeWithEquipmentDamage(
        AuthoritativePlayerState player,
        ulong tick)
    {
        if (player.CurrentAction is not null
            || player.CombatModeEndTick == 0
            || tick < player.CombatModeEndTick) {
            return;
        }

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
        foreach (var player in players) {
            AdvancePlayerEquipmentDamage(player, tick, players, events);
            AdvancePlayerAction(player, tick, players, events);
        }
        foreach (var player in players)
            AdvanceCombatModeWithEquipmentDamage(player, tick);
        AdvanceMonsters(tick, players, events);
        AdvanceProjectiles(tick, players, events);
        return events;
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

    private void AdvancePlayerAction(
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var action = player.CurrentAction;
        if (action is null)
            return;

        if (!action.EffectApplied && tick >= action.EffectTick) {
            switch (action.Kind) {
            case DiabloPlayerActionKind.Move:
                var destinationX = checked(player.PositionX + action.DirectionX);
                var destinationY = checked(player.PositionY + action.DirectionY);
                if (world.IsWalkable(player.LevelId, destinationX, destinationY)
                    && !world.IsOccupied(
                        player.LevelId,
                        destinationX,
                        destinationY,
                        players,
                        player.EntityId)) {
                    player.PositionX = destinationX;
                    player.PositionY = destinationY;
                    if (worldCommands is not null) {
                        AddDirectedEvents(
                            player,
                            worldCommands.ExecuteOccupancyTriggers(player, players),
                            events);
                        AddDirectedEvents(
                            player,
                            worldCommands.ExecuteLevelTrigger(player, tick, players),
                            events);
                    }
                }
                break;
            case DiabloPlayerActionKind.Attack:
                AddDirectedEvents(
                    player,
                    action.TargetCell
                        ? ResolveAttackAt(
                            player,
                            action.TargetX,
                            action.TargetY,
                            action.StartedAtTick,
                            player.CombatMode == DiabloPlayerCombatMode.RangedAttack,
                            players,
                            effectTick: tick)
                        : ResolveAttack(
                            player,
                            action.TargetEntityId,
                            action.StartedAtTick,
                            player.CombatMode == DiabloPlayerCombatMode.RangedAttack,
                            players,
                            effectTick: tick),
                    events);
                break;
            case DiabloPlayerActionKind.Cast:
                AddDirectedEvents(
                    player,
                    ResolveCast(
                        player,
                        action.CastIntent
                            ?? throw new InvalidOperationException(
                                "A timed cast action has no cast intent."),
                        appliedTick: tick,
                        players: players),
                    events);
                break;
            default:
                throw new InvalidOperationException("A timed player action has an invalid kind.");
            }
            action.EffectApplied = true;
        }

        if (ReferenceEquals(player.CurrentAction, action) && tick >= action.EndTick) {
            DiabloPlayerActionLifecycle.Cancel(player);
            if (player.CombatMode == DiabloPlayerCombatMode.Death) {
                player.CombatModeEndTick = 0;
            } else {
                player.CombatMode = DiabloPlayerCombatMode.Stand;
                player.CombatModeEndTick = 0;
            }
        }
    }

    private static void AddDirectedEvents(
        AuthoritativePlayerState player,
        DiabloCombatIntentResult result,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        foreach (var gameplayEvent in result.Events) {
            events.Add(new DiabloDirectedGameplayEvent(player, gameplayEvent));
        }
    }

    private static void AddDirectedEvents(
        AuthoritativePlayerState player,
        DiabloWorldIntentResult result,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        foreach (var gameplayEvent in result.Events) {
            events.Add(new DiabloDirectedGameplayEvent(player, gameplayEvent));
        }
    }

    private DiabloCombatIntentResult Attack(
        AuthoritativePlayerState player,
        uint targetEntityId,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!useNativePlayerActionTiming)
            return ResolveAttack(
                player,
                targetEntityId,
                appliedTick,
                ranged: GetAttackAnimationProfile(player)
                    == DiabloPlayerAttackAnimationProfile.Bow,
                players: players);
        if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var animation = playerAnimations!.GetRequired(player.CharacterClassId);
        var profile = GetAttackAnimationProfile(player);
        var timing = animation.GetAttack(profile);
        var ranged = profile == DiabloPlayerAttackAnimationProfile.Bow;
        AuthoritativeCombatTarget? target = null;
        AuthoritativePlayerState? playerTarget = null;
        if (!TryGetAttackTarget(player, targetEntityId, ranged, out target)
            && !TryGetPlayerAttackTarget(
                player,
                targetEntityId,
                ranged,
                players,
                out playerTarget))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var targetX = target?.PositionX ?? playerTarget!.PositionX;
        var targetY = target?.PositionY ?? playerTarget!.PositionY;
        UpdateFacingToward(player, targetX, targetY);
        var includesFirstFrame = player.CurrentAction is not null
            && player.CurrentAction.EffectApplied
            && player.CombatMode is DiabloPlayerCombatMode.Attack
                or DiabloPlayerCombatMode.RangedAttack;
        var skippedFrames = GetAttackSkippedFrames(
            player,
            ranged,
            includesFirstFrame);
        var effectTick = checked(
            appliedTick + (ulong)(timing.ActionFrame - 1 - skippedFrames));
        var endTick = checked(appliedTick + (ulong)(timing.Frames - 1 - skippedFrames));
        var readyTick = Math.Min(
            endTick,
            checked(appliedTick + (ulong)(timing.ActionFrame - skippedFrames)));
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Attack,
                appliedTick,
                effectTick,
                readyTick,
                endTick) {
                TargetEntityId = targetEntityId,
            },
            ranged
                ? DiabloPlayerCombatMode.RangedAttack
                : DiabloPlayerCombatMode.Attack);
        return DiabloCombatIntentResult.Accepted;
    }

    /** Admits a native CMD_*ATTACKXY-compatible destination-cell action. */
    private DiabloCombatIntentResult AttackAt(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!world.ContainsPosition(player.LevelId, targetX, targetY))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        UpdateFacingToward(player, targetX, targetY);
        if (!useNativePlayerActionTiming)
            return ResolveAttackAt(
                player,
                targetX,
                targetY,
                appliedTick,
                GetAttackAnimationProfile(player) == DiabloPlayerAttackAnimationProfile.Bow,
                players);
        if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var animation = playerAnimations!.GetRequired(player.CharacterClassId);
        var profile = GetAttackAnimationProfile(player);
        var timing = animation.GetAttack(profile);
        var ranged = profile == DiabloPlayerAttackAnimationProfile.Bow;
        var includesFirstFrame = player.CurrentAction is not null
            && player.CurrentAction.EffectApplied
            && player.CombatMode is DiabloPlayerCombatMode.Attack
                or DiabloPlayerCombatMode.RangedAttack;
        var skippedFrames = GetAttackSkippedFrames(player, ranged, includesFirstFrame);
        var effectTick = checked(
            appliedTick + (ulong)(timing.ActionFrame - 1 - skippedFrames));
        var endTick = checked(appliedTick + (ulong)(timing.Frames - 1 - skippedFrames));
        var readyTick = Math.Min(
            endTick,
            checked(appliedTick + (ulong)(timing.ActionFrame - skippedFrames)));
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Attack,
                appliedTick,
                effectTick,
                readyTick,
                endTick) {
                TargetCell = true,
                TargetX = targetX,
                TargetY = targetY,
            },
            ranged
                ? DiabloPlayerCombatMode.RangedAttack
                : DiabloPlayerCombatMode.Attack);
        return DiabloCombatIntentResult.Accepted;
    }

    private DiabloCombatIntentResult ResolveAttack(
        AuthoritativePlayerState player,
        uint targetEntityId,
        ulong appliedTick,
        bool ranged = false,
        IReadOnlyList<AuthoritativePlayerState>? players = null,
        ulong? effectTick = null)
    {
        players ??= [player];
        if (!TryGetAttackTarget(player, targetEntityId, ranged, out var target)) {
            if (TryGetPlayerAttackTarget(
                    player,
                    targetEntityId,
                    ranged,
                    players,
                    out var playerTarget)) {
                if (ranged)
                    return ResolvePlayerArrowVolley(
                        player,
                        playerTarget,
                        appliedTick);
                return ResolvePlayerAttack(
                    player,
                    playerTarget,
                    appliedTick,
                    players,
                    effectTick ?? appliedTick);
            }
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        UpdateFacingToward(player, target.PositionX, target.PositionY);
        if (ranged) {
            var arrowCount = HasUsableEquipmentFlag(
                player,
                AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag)
                ? rules.MultipleArrowsCount
                : 1;
            return ResolveWeaponArrowVolley(
                player,
                target,
                appliedTick,
                arrowCount);
        }
        var events = new List<DiabloGameplayEvent>();
        var attackHit = ResolveMonsterPlayerAttack(
            player,
            target,
            appliedTick,
            players,
            adjacentDamage: false,
            seed: unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId),
            applyElemental: true,
            events);
        attackHit |= ResolveCleaveSideTargets(
            player,
            players,
            appliedTick,
            target.EntityId,
            events);
        if (ranged || attackHit) {
            var hellfireRemoved = ApplyHellfireWeaponDecay(player);
            if (!hellfireRemoved)
                ApplyNativeWeaponDurabilityLoss(player, ranged ? 40U : 30U);
        }
        return events.Count == 0
            ? DiabloCombatIntentResult.Accepted
            : new DiabloCombatIntentResult(true, null, events);
    }

    /** Resolves one native monster-target physical hit, including shared effects. */
    private bool ResolveMonsterPlayerAttack(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        bool adjacentDamage,
        uint seed,
        bool applyElemental,
        ICollection<DiabloGameplayEvent> events)
    {
        var targetDistance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY));
        var gargoyleWake = string.Equals(
                target.NativeAi,
                "Gargoyle",
                StringComparison.Ordinal)
            && target.GargoyleSpecialActive;
        var damageQ6 = ResolvePlayerAttackDamageQ6(
            player,
            target,
            monsterTarget: true,
            ranged: false,
            deferGargoyleWake: false,
            targetDistance: targetDistance,
            adjacentDamage: adjacentDamage,
            seed: seed,
            out var perilSelfDamageQ6,
            out var perilActive,
            out var doppelgangerProc,
            out var attackHit);
        if (gargoyleWake)
            attackHit = true;
        if (doppelgangerProc)
            SpawnDoppelganger(target, players);
        if (perilActive) {
            var damageResult = playerDamage.Apply(player, perilSelfDamageQ6);
            if (damageResult.IncomingDamageQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    player.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                });
            }
            if (player.Life == 0) {
                StartPlayerDeath(
                    player,
                    player.EntityId,
                    appliedTick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }
        ApplyResolvedDamageQ6(player, target, damageQ6, events);
        // Native DoAttack emits melee WeaponExplosion missiles before
        // PlrHitMonst resolves physical admission. FireDamage and
        // LightningDamage therefore still reach a live monster when the
        // physical roll misses; ranged arrow channels remain captured only
        // for admitted arrows at launch.
        if (applyElemental
            && !gargoyleWake
            && target.HitPointsQ6 > 0)
            ApplyEquipmentElementalDamage(player, target, events, ranged: false);
        ApplyEquipmentKnockback(player, target, damageQ6, players);
        if (damageQ6 > 0)
            ApplyEquipmentAttackEffects(player, damageQ6, events);
        return attackHit;
    }

    /** Returns the native side monsters affected by a class Cleave attack. */
    private IReadOnlyList<AuthoritativeCombatTarget> FindCleaveTargets(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint primaryTargetEntityId = 0)
    {
        var targets = new List<AuthoritativeCombatTarget>(2);
        var facing = (int)player.FacingDirection;
        foreach (var offset in new[] { 1, -1 }) {
            var direction = DeterministicGridDirections.Rotate(facing, offset);
            var displacement = DirectionDisplacements[direction];
            var positionX = checked(player.PositionX + displacement.X);
            var positionY = checked(player.PositionY + displacement.Y);
            var sideTarget = world.CombatTargets.Values
                .Where(candidate => candidate.EntityId != primaryTargetEntityId
                    && candidate.PositionX == positionX
                    && candidate.PositionY == positionY)
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate =>
                    TryGetAttackTarget(player, candidate.EntityId, ranged: false, out _));
            if (sideTarget is not null)
                targets.Add(sideTarget);
        }
        return targets;
    }

    /** Mirrors Player::CanCleave using validated class and equipment identities. */
    private bool CanPlayerCleave(AuthoritativePlayerState player)
    {
        var playerClass = GetPlayerClass(player);
        if (playerClass is null)
            return false;
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        var hasStaff = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsStaff(left.State, items)
            || right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsStaff(right.State, items);
        var hasShield = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsShield(left.State, items)
            || right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsShield(right.State, items);
        var hasSwordLeft = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsSword(left.State, items);
        var hasSwordRight = right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsSword(right.State, items);
        var hasAxe = left is not null && IsUsable(left.State)
            && AuthoritativeItemClassification.IsAxe(left.State, items)
            || right is not null && IsUsable(right.State)
            && AuthoritativeItemClassification.IsAxe(right.State, items);
        var hasTwoHandedMaceOrSword = left is not null
            && IsUsable(left.State)
            && AuthoritativeItemClassification.IsTwoHanded(left.State, items)
            && (AuthoritativeItemClassification.IsMace(left.State, items)
                || AuthoritativeItemClassification.IsSword(left.State, items));
        return playerClass.PhysicalDamageFormula switch {
            AuthoritativePhysicalDamageFormula.Monk => hasStaff,
            AuthoritativePhysicalDamageFormula.Bard => hasSwordLeft && hasSwordRight,
            AuthoritativePhysicalDamageFormula.Barbarian => hasAxe
                || (!hasShield && hasTwoHandedMaceOrSword),
            _ => false,
        };
    }

    /** Resolves a cell action against the first authoritative occupant on its native attack path. */
    private DiabloCombatIntentResult ResolveAttackAt(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        ulong appliedTick,
        bool ranged,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong? effectTick = null)
    {
        var targetEntityId = FindCellAttackTarget(
            player,
            targetX,
            targetY,
            ranged,
            players);
        // Native cell attacks still perform their animation when the lane is
        // empty. A melee object hit and the two native Cleave lanes still
        // resolve at the same action boundary as a monster hit.
        if (targetEntityId == 0) {
            var events = new List<DiabloGameplayEvent>();
            var attackHit = false;
            if (!ranged
                && TryGetMeleeBreakableObject(
                    player,
                    targetX,
                    targetY,
                    out var breakableObject)
                && breakableObject is not null) {
                breakableObject.Destroyed = true;
                attackHit = true;
            }
            if (!ranged) {
                attackHit |= ResolveCleaveSideTargets(
                    player,
                    players,
                    appliedTick,
                    primaryTargetEntityId: 0,
                    events);
            }
            if (attackHit) {
                var hellfireRemoved = ApplyHellfireWeaponDecay(player);
                if (!hellfireRemoved)
                    ApplyNativeWeaponDurabilityLoss(player, 30);
            }
            return events.Count == 0
                ? DiabloCombatIntentResult.Accepted
                : new DiabloCombatIntentResult(true, null, events);
        }
        return ResolveAttack(
            player,
            targetEntityId,
            appliedTick,
            ranged,
            players,
            effectTick);
    }

    /** Resolves both native Cleave side lanes after any melee primary target. */
    private bool ResolveCleaveSideTargets(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong appliedTick,
        uint primaryTargetEntityId,
        ICollection<DiabloGameplayEvent> events)
    {
        if (!CanPlayerCleave(player))
            return false;

        var attackHit = false;
        var sideIndex = 0U;
        foreach (var sideTarget in FindCleaveTargets(
                     player,
                     players,
                     primaryTargetEntityId)) {
            attackHit |= ResolveMonsterPlayerAttack(
                player,
                sideTarget,
                appliedTick,
                players,
                adjacentDamage: true,
                seed: unchecked(
                    (uint)appliedTick
                    ^ sideTarget.EntityId
                    ^ player.EntityId
                    ^ ++sideIndex),
                applyElemental: true,
                events);
        }
        return attackHit;
    }

    private uint FindCellAttackTarget(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        bool ranged,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var steps = ranged
            ? DeterministicGridMetrics.ChebyshevDistance(
                targetX,
                targetY,
                player.PositionX,
                player.PositionY)
            : 1;
        for (var step = 1; step <= steps; step++) {
            var positionX = checked(player.PositionX + displacement.X * step);
            var positionY = checked(player.PositionY + displacement.Y * step);
            var monster = world.CombatTargets.Values
                .Where(candidate =>
                    candidate.PositionX == positionX
                    && candidate.PositionY == positionY)
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate =>
                    TryGetAttackTarget(player, candidate.EntityId, ranged, out _));
            if (monster is not null)
                return monster.EntityId;

            var playerTarget = players
                .Where(candidate =>
                    candidate.EntityId != player.EntityId
                    && candidate.PositionX == positionX
                    && candidate.PositionY == positionY)
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate =>
                    TryGetPlayerAttackTarget(
                        player,
                        candidate.EntityId,
                        ranged,
                        players,
                        out _));
            if (playerTarget is not null)
                return playerTarget.EntityId;
        }
        return 0;
    }

    /** Returns the native adjacent breakable object selected by a melee cell attack. */
    private bool TryGetMeleeBreakableObject(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        out AuthoritativeWorldObject? breakableObject)
    {
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var positionX = checked(player.PositionX + displacement.X);
        var positionY = checked(player.PositionY + displacement.Y);
        breakableObject = world.Objects.Values
            .Where(@object => @object.LevelId == player.LevelId
                && @object.PositionX == positionX
                && @object.PositionY == positionY
                && @object.Breakable
                && !@object.Activated
                && !@object.Destroyed)
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
        return breakableObject is not null;
    }

    /** Emits server-owned weapon arrows targeting another player. */
    private DiabloCombatIntentResult ResolvePlayerArrowVolley(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        ulong appliedTick)
    {
        var targetDistance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY));
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            target.PositionX,
            target.PositionY);
        var directionDisplacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var perpendicular = (-directionDisplacement.Y, directionDisplacement.X);
        var targetProxy = CreatePlayerCombatTargetProxy(target);
        var arrowCount = HasUsableEquipmentFlag(
            player,
            AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag)
            ? rules.MultipleArrowsCount
            : 1;
        for (var arrowIndex = 0; arrowIndex < arrowCount; arrowIndex++) {
            var damageQ6 = ResolvePlayerAttackDamageQ6(
                player,
                targetProxy,
                monsterTarget: false,
                ranged: true,
                deferGargoyleWake: false,
                targetDistance: targetDistance,
                adjacentDamage: false,
                seed: unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId ^ (uint)arrowIndex),
                out var perilSelfDamageQ6,
                out var perilActive,
                out var doppelgangerProc,
                out var attackHit);
            var elemental = attackHit
                ? AuthoritativeEquipmentElementalDamageRules.Resolve(
                    player.Equipment,
                    random.GenerateRnd,
                    ranged: true)
                : default;
            var spread = arrowCount == 1 || arrowIndex == 0
                ? (X: 0, Y: 0)
                : (X: checked(
                    perpendicular.Item1
                    * (arrowIndex == 1 ? -1 : 1)
                    * rules.MultipleArrowsSpreadDistance),
                    Y: checked(
                        perpendicular.Item2
                        * (arrowIndex == 1 ? -1 : 1)
                        * rules.MultipleArrowsSpreadDistance));
            var arrowPositionX = checked(player.PositionX + spread.X);
            var arrowPositionY = checked(player.PositionY + spread.Y);
            if (!world.ContainsPosition(player.LevelId, arrowPositionX, arrowPositionY)) {
                arrowPositionX = player.PositionX;
                arrowPositionY = player.PositionY;
            }
            var (arrowSpeed, arrowLifetime) = ResolveWeaponArrowTravelTiming(
                player,
                targetDistance);
            var projectile = new AuthoritativeProjectile(
                AllocateProjectileEntityId(),
                player.EntityId == 0 ? 1U : player.EntityId,
                target.EntityId,
                0,
                player.LevelId,
                arrowPositionX,
                arrowPositionY,
                target.PositionX,
                target.PositionY,
                DeterministicFixedPointQ6.ToWholeTruncated(Math.Max(0, damageQ6)),
                AuthoritativeDamageType.Physical,
                0,
                arrowLifetime,
                allowZeroSpellId: true) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                DamageQ6 = Math.Max(0, damageQ6),
                ElementalFireDamage = elemental.Fire,
                ElementalLightningDamage = elemental.Lightning,
                AttackHit = attackHit,
                PerilSelfDamageQ6 = perilActive ? perilSelfDamageQ6 : 0,
                DoppelgangerProc = doppelgangerProc,
                ActivationDelayTicks = arrowIndex == 0
                    ? 0U
                    : checked((uint)rules.MultipleArrowsDelayTicks),
                Speed = arrowSpeed,
            };
            world.Projectiles.Add(projectile.EntityId, projectile);
            if (!ApplyHellfireWeaponDecay(player))
                ApplyNativeWeaponDurabilityLoss(player, 40);
        }
        return DiabloCombatIntentResult.Accepted;
    }

    private AuthoritativeCombatTarget CreatePlayerCombatTargetProxy(
        AuthoritativePlayerState target)
    {
        var targetClass = GetPlayerClass(target);
        return new AuthoritativeCombatTarget(
            target.EntityId,
            target.PositionX,
            target.PositionY,
            target.Life,
            armorClass: target.ArmorClass,
            maxHitPoints: target.LifeMaximum,
            levelId: target.LevelId,
            fireResistance: GetPlayerResistance(
                target,
                targetClass,
                AuthoritativeDamageType.Fire),
            lightningResistance: GetPlayerResistance(
                target,
                targetClass,
                AuthoritativeDamageType.Lightning),
            unique: true);
    }

    /** Resolves the initial authoritative melee path against another player. */
    private DiabloCombatIntentResult ResolvePlayerAttack(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong effectTick)
    {
        UpdateFacingToward(player, target.PositionX, target.PositionY);
        var targetProxy = CreatePlayerCombatTargetProxy(target);
        var damageQ6 = ResolvePlayerAttackDamageQ6(
            player,
            targetProxy,
            monsterTarget: false,
            ranged: false,
            deferGargoyleWake: false,
            targetDistance: 1,
            adjacentDamage: false,
            seed: unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId),
            out var perilSelfDamageQ6,
            out var perilActive,
            out _,
            out var attackHit);
        var events = new List<DiabloGameplayEvent>();
        if (perilActive) {
            var selfDamageResult = playerDamage.Apply(player, perilSelfDamageQ6);
            if (selfDamageResult.IncomingDamageQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    player.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        selfDamageResult.IncomingDamageQ6)) {
                    AmountQ6 = selfDamageResult.IncomingDamageQ6,
                });
            }
            if (player.Life == 0) {
                StartPlayerDeath(
                    player,
                    player.EntityId,
                    appliedTick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }

        var primaryBlocked = attackHit
            && TryResolvePlayerBlock(player, target, effectTick);
        if (!primaryBlocked) {
            var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
            if (damageResult.IncomingDamageQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    player.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                });
            }
            if (target.Life == 0) {
                StartPlayerDeath(
                    target,
                    player.EntityId,
                    appliedTick,
                    players,
                    PlayerDeathReason.Player);
            }
            if (attackHit && target.Life > 0)
                ApplyPlayerElementalDamage(
                    player,
                    target,
                    targetProxy,
                    appliedTick,
                    players,
                    events);
            ApplyPlayerKnockback(player, target, damageQ6, players);
            if (damageQ6 > 0)
                ApplyPlayerAttackEffects(player, damageQ6, events);
        }

        attackHit |= ResolveCleaveSideTargets(
            player,
            players,
            appliedTick,
            target.EntityId,
            events);
        if (attackHit) {
            var hellfireRemoved = ApplyHellfireWeaponDecay(player);
            if (!hellfireRemoved)
                ApplyNativeWeaponDurabilityLoss(player, 30);
        }
        return events.Count == 0
            ? DiabloCombatIntentResult.Accepted
            : new DiabloCombatIntentResult(true, null, events);
    }

    /**
     * Emits server-owned bow missiles. Multiple Arrows uses the validated
     * content count; the first starts on the attack effect frame and subsequent
     * spread arrows begin after the configured delay and use the configured
     * perpendicular offset, matching DoRangeAttack's second firing window.
     */
    private DiabloCombatIntentResult ResolveWeaponArrowVolley(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        ulong appliedTick,
        int arrowCount)
    {
        if (arrowCount is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(arrowCount));
        var targetDistance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY));
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            target.PositionX,
            target.PositionY);
        var directionDisplacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var perpendicular = (-directionDisplacement.Y, directionDisplacement.X);
        for (var arrowIndex = 0; arrowIndex < arrowCount; arrowIndex++) {
            var damageQ6 = ResolvePlayerAttackDamageQ6(
                player,
                target,
                monsterTarget: true,
                ranged: true,
                deferGargoyleWake: true,
                targetDistance: targetDistance,
                adjacentDamage: false,
                seed: unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId ^ (uint)arrowIndex),
                out var perilSelfDamageQ6,
                out var perilActive,
                out var doppelgangerProc,
                out var attackHit);
            var elemental = attackHit
                ? AuthoritativeEquipmentElementalDamageRules.Resolve(
                    player.Equipment,
                    random.GenerateRnd,
                    ranged: true)
                : default;
            var spread = arrowCount == 1 || arrowIndex == 0
                ? (X: 0, Y: 0)
                : (X: checked(
                    perpendicular.Item1
                    * (arrowIndex == 1 ? -1 : 1)
                    * rules.MultipleArrowsSpreadDistance),
                    Y: checked(
                        perpendicular.Item2
                        * (arrowIndex == 1 ? -1 : 1)
                        * rules.MultipleArrowsSpreadDistance));
            var arrowPositionX = checked(player.PositionX + spread.X);
            var arrowPositionY = checked(player.PositionY + spread.Y);
            if (!world.ContainsPosition(player.LevelId, arrowPositionX, arrowPositionY)) {
                arrowPositionX = player.PositionX;
                arrowPositionY = player.PositionY;
            }
            var (arrowSpeed, arrowLifetime) = ResolveWeaponArrowTravelTiming(
                player,
                targetDistance);
            var projectile = new AuthoritativeProjectile(
                AllocateProjectileEntityId(),
                player.EntityId == 0 ? 1U : player.EntityId,
                target.EntityId,
                0,
                player.LevelId,
                arrowPositionX,
                arrowPositionY,
                target.PositionX,
                target.PositionY,
                DeterministicFixedPointQ6.ToWholeTruncated(Math.Max(0, damageQ6)),
                AuthoritativeDamageType.Physical,
                0,
                arrowLifetime,
                allowZeroSpellId: true) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                DamageQ6 = Math.Max(0, damageQ6),
                ElementalFireDamage = elemental.Fire,
                ElementalLightningDamage = elemental.Lightning,
                AttackHit = attackHit,
                PerilSelfDamageQ6 = perilActive ? perilSelfDamageQ6 : 0,
                DoppelgangerProc = doppelgangerProc,
                ActivationDelayTicks = arrowIndex == 0
                    ? 0U
                    : checked((uint)rules.MultipleArrowsDelayTicks),
                Speed = arrowSpeed,
            };
            world.Projectiles.Add(projectile.EntityId, projectile);
            if (!ApplyHellfireWeaponDecay(player))
                ApplyNativeWeaponDurabilityLoss(player, 40);
        }
        return DiabloCombatIntentResult.Accepted;
    }

    /** Native AddArrow velocity and the corresponding tile-travel lifetime. */
    private (int Speed, uint Lifetime) ResolveWeaponArrowTravelTiming(
        AuthoritativePlayerState player,
        long targetDistance)
    {
        var speed = 32;
        if (HasUsableEquipmentFlag(
                player,
                AuthoritativeEquipmentAttackEffectRules.RandomArrowVelocityFlag)) {
            speed = random.RandomIntBetween(16, 47);
        }

        var playerClass = GetPlayerClass(player);
        var level = checked((int)player.CharacterLevel);
        if (playerClass?.RangedArrowSpeedLevelDivisor > 0)
            speed = checked(
                speed
                + Math.Max(0, level - 1) / playerClass.RangedArrowSpeedLevelDivisor);

        if (rules.GameVariant == DiabloGameVariant.Hellfire) {
            var flags = GetUsableEquipmentFlags(player);
            if ((flags & QuickAttackFlag) != 0)
                speed++;
            if ((flags & FastAttackFlag) != 0)
                speed = checked(speed + 2);
            if ((flags & FasterAttackFlag) != 0)
                speed = checked(speed + 4);
            if ((flags & FastestAttackFlag) != 0)
                speed = checked(speed + 8);
        }

        // Preserve the compact legacy representation when native AddArrow
        // resolves to its default 32-pixel cadence.
        if (speed == 32)
            return (0, (uint)Math.Min(
                (long)uint.MaxValue,
                Math.Max(1L, targetDistance)));

        var travelTiles = Math.Max(0L, targetDistance - 1);
        var travelTicks = (travelTiles * 32 + speed - 1) / speed;
        return (speed, (uint)Math.Min(
            (long)uint.MaxValue,
            travelTicks + 1));
    }

    /** Applies native fire/lightning weapon channels after a physical hit. */
    private void ApplyEquipmentElementalDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        ICollection<DiabloGameplayEvent> events,
        bool ranged)
    {
        var elemental = AuthoritativeEquipmentElementalDamageRules.Resolve(
            player.Equipment,
            random.GenerateRnd,
            ranged);
        if (elemental.Fire > 0) {
            var fireDamage = rules.ResolveDamage(
                elemental.Fire,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Fire,
                target);
            ApplyResolvedDamage(player, target, fireDamage, events);
        }
        if (elemental.Lightning > 0 && target.HitPointsQ6 > 0) {
            var lightningDamage = rules.ResolveDamage(
                elemental.Lightning,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Lightning,
                target);
            ApplyResolvedDamage(player, target, lightningDamage, events);
        }
    }

    /** Applies native fire/lightning weapon channels to a live player target. */
    private void ApplyPlayerElementalDamage(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        AuthoritativeCombatTarget targetProxy,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        var elemental = AuthoritativeEquipmentElementalDamageRules.Resolve(
            player.Equipment,
            random.GenerateRnd,
            ranged: false);
        ApplyPlayerElementalChannels(
            player,
            target,
            targetProxy,
            elemental,
            appliedTick,
            players,
            events);
    }

    private void ApplyPlayerElementalChannels(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        AuthoritativeCombatTarget targetProxy,
        AuthoritativeEquipmentElementalDamageRules.ElementalDamage elemental,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        if (elemental.Fire > 0) {
            var fireDamage = rules.ResolveDamage(
                elemental.Fire,
                targetProxy.ArmorClass,
                AuthoritativeDamageType.Fire,
                targetProxy);
            ApplyPlayerElementalDamageQ6(
                player,
                target,
                DeterministicFixedPointQ6.FromWhole(fireDamage),
                appliedTick,
                players,
                events);
        }
        if (elemental.Lightning > 0 && target.Life > 0) {
            var lightningDamage = rules.ResolveDamage(
                elemental.Lightning,
                targetProxy.ArmorClass,
                AuthoritativeDamageType.Lightning,
                targetProxy);
            ApplyPlayerElementalDamageQ6(
                player,
                target,
                DeterministicFixedPointQ6.FromWhole(lightningDamage),
                appliedTick,
                players,
                events);
        }
    }

    private void ApplyPlayerElementalDamageQ6(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        int damageQ6,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        if (damageQ6 <= 0 || target.Life <= 0)
            return;
        var damageResult = playerDamage.Apply(target, damageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                player.EntityId,
                target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(
                    damageResult.IncomingDamageQ6)) {
                AmountQ6 = damageResult.IncomingDamageQ6,
            });
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                player.EntityId,
                appliedTick,
                players,
                PlayerDeathReason.Player);
    }

    /** Applies native knockback after a successful non-lethal physical hit. */
    private void ApplyEquipmentKnockback(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int damageQ6,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (damageQ6 <= 0
            || target.HitPoints <= 0
            || target.IsPetrified
            || (GetUsableEquipmentFlags(player)
                & AuthoritativeEquipmentAttackEffectRules.KnockbackFlag) == 0) {
            return;
        }

        var directionX = Math.Sign((long)target.PositionX - player.PositionX);
        var directionY = Math.Sign((long)target.PositionY - player.PositionY);
        if (directionX == 0 && directionY == 0)
            return;
        var destinationX = target.PositionX + directionX;
        var destinationY = target.PositionY + directionY;
        if (world.IsWalkable(target.LevelId, destinationX, destinationY)
            && !world.IsOccupied(
                target.LevelId,
                destinationX,
                destinationY,
                players,
                target.EntityId)) {
            target.ApplyKnockback(destinationX, destinationY);
        }
    }

    /** Applies native knockback equipment to a live adjacent player target. */
    private void ApplyPlayerKnockback(
        AuthoritativePlayerState player,
        AuthoritativePlayerState target,
        int damageQ6,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (damageQ6 <= 0
            || target.Life <= 0
            || (GetUsableEquipmentFlags(player)
                & AuthoritativeEquipmentAttackEffectRules.KnockbackFlag) == 0) {
            return;
        }

        var directionX = Math.Sign((long)target.PositionX - player.PositionX);
        var directionY = Math.Sign((long)target.PositionY - player.PositionY);
        if (directionX == 0 && directionY == 0)
            return;
        var destinationX = checked(target.PositionX + directionX);
        var destinationY = checked(target.PositionY + directionY);
        if (!world.IsWalkable(target.LevelId, destinationX, destinationY)
            || world.IsOccupied(
                target.LevelId,
                destinationX,
                destinationY,
                players,
                target.EntityId)) {
            return;
        }

        DiabloPlayerActionLifecycle.Cancel(target);
        target.PositionX = destinationX;
        target.PositionY = destinationY;
    }

    /** Applies native monster MFLAG_KNOCKBACK after a successful player hit. */
    private void ApplyMonsterKnockback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!monster.HasKnockback || player.Life <= 0)
            return;

        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var destinationX = checked(player.PositionX + displacement.X);
        var destinationY = checked(player.PositionY + displacement.Y);
        if (!world.IsWalkable(monster.LevelId, destinationX, destinationY)
            || world.IsOccupied(
                monster.LevelId,
                destinationX,
                destinationY,
                players,
                player.EntityId)) {
            return;
        }

        DiabloPlayerActionLifecycle.Cancel(player);
        player.PositionX = destinationX;
        player.PositionY = destinationY;
    }

    /** Applies native usable equipment resource gains after a successful hit. */
    private void ApplyEquipmentAttackEffects(
        AuthoritativePlayerState player,
        int damageQ6,
        ICollection<DiabloGameplayEvent> events)
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.Resolve(
            damageQ6,
            GetUsableEquipmentFlags(player),
            random.GenerateRnd,
            rules.RandomStealLifeDivisor,
            rules.StealLife3Percent,
            rules.StealLife5Percent,
            rules.StealMana3Percent,
            rules.StealMana5Percent);
        var lifeGain = Math.Min(
            Math.Max(0, player.LifeMaximum - player.Life),
            gain.LifeQ6);
        player.Life = checked(player.Life + lifeGain);
        var manaGain = Math.Min(
            Math.Max(0, player.ManaMaximum - player.Mana),
            gain.ManaQ6);
        player.Mana = checked(player.Mana + manaGain);
        if (lifeGain > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Healing,
                player.EntityId,
                player.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(lifeGain)) {
                AmountQ6 = lifeGain,
            });
        }
    }

    /** Applies native Hellfire Decay to eligible hand weapons in native order. */
    private bool ApplyHellfireWeaponDecay(AuthoritativePlayerState player)
    {
        if (rules.GameVariant != DiabloGameVariant.Hellfire)
            return false;

        foreach (var slot in new uint[] { 4, 5 }) {
            var equipmentIndex = player.Equipment.FindIndex(item => item.Slot == slot);
            if (equipmentIndex < 0)
                continue;
            var equippedItem = player.Equipment[equipmentIndex];
            var state = equippedItem.State;
            if (!IsUsable(state)
                || (state.Magical != 0 && !state.Identified)
                || !AuthoritativeItemClassification.IsWeapon(state, items)
                || (state.HellfireDamageArmorFlags
                    & AuthoritativeEquipmentIncomingDamageRules.DecayFlag) == 0) {
                continue;
            }

            var decayedState = state with {
                PlusDamage = checked(state.PlusDamage - rules.HellfireDecayDamageReduction),
            };
            if (decayedState.PlusDamage <= -rules.HellfireDecayRemovalThreshold)
                player.Equipment.RemoveAt(equipmentIndex);
            else
                player.Equipment[equipmentIndex] = equippedItem with {
                    State = decayedState,
                };
            player.RecalculateEquipmentResources();
            if (decayedState.PlusDamage <= -rules.HellfireDecayRemovalThreshold)
                return true;
        }
        return false;
    }

    /** Mirrors native DamageWeapon's slot ordering and break semantics. */
    private bool ApplyNativeWeaponDurabilityLoss(
        AuthoritativePlayerState player,
        uint damageFrequency)
    {
        // Compact replay fixtures may carry compatibility weapons without
        // durability metadata. Native server-backed clients do not mutate
        // those placeholder items, so they must not consume a durability roll.
        if (!HasNativeWeaponDurabilityTarget(player))
            return false;
        if (!random.FlipCoin(damageFrequency))
            return false;

        var leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        if (leftIndex >= 0
            && AuthoritativeItemClassification.IsWeapon(
                player.Equipment[leftIndex].State,
                items)) {
            if (player.Equipment[leftIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[leftIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(leftIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[leftIndex] = player.Equipment[leftIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        var rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (rightIndex >= 0
            && AuthoritativeItemClassification.IsWeapon(
                player.Equipment[rightIndex].State,
                items)) {
            if (player.Equipment[rightIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[rightIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(rightIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[rightIndex] = player.Equipment[rightIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (leftIndex < 0
            && rightIndex >= 0
            && AuthoritativeItemClassification.IsShield(
                player.Equipment[rightIndex].State,
                items)) {
            if (player.Equipment[rightIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[rightIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(rightIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[rightIndex] = player.Equipment[rightIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (rightIndex < 0
            && leftIndex >= 0
            && AuthoritativeItemClassification.IsShield(
                player.Equipment[leftIndex].State,
                items)) {
            if (player.Equipment[leftIndex].State.Durability == 255)
                return false;
            var state = player.Equipment[leftIndex].State;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(leftIndex);
                player.RecalculateEquipmentResources();
                return true;
            }
            player.Equipment[leftIndex] = player.Equipment[leftIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }

        return false;
    }

    private bool HasNativeWeaponDurabilityTarget(AuthoritativePlayerState player)
    {
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        if (left is not null
            && AuthoritativeItemClassification.IsWeapon(left.State, items)
            && left.State.MaxDurability > 0)
            return true;
        if (right is not null
            && AuthoritativeItemClassification.IsWeapon(right.State, items)
            && right.State.MaxDurability > 0)
            return true;
        if (left is null
            && right is not null
            && AuthoritativeItemClassification.IsShield(right.State, items)
            && right.State.MaxDurability > 0)
            return true;
        return right is null
            && left is not null
            && AuthoritativeItemClassification.IsShield(left.State, items)
            && left.State.MaxDurability > 0;
    }

    /** Mirrors native DamageParryItem's left-hand-first block damage. */
    private void ApplyNativeParryDurabilityLoss(AuthoritativePlayerState player)
    {
        var leftIndex = player.Equipment.FindIndex(item => item.Slot == 4);
        if (leftIndex >= 0
            && (AuthoritativeItemClassification.IsShield(
                    player.Equipment[leftIndex].State,
                    items)
                || AuthoritativeItemClassification.IsStaff(
                    player.Equipment[leftIndex].State,
                    items))) {
            var state = player.Equipment[leftIndex].State;
            if (state.Durability == 255)
                return;
            if (state.Durability <= 1) {
                player.Equipment.RemoveAt(leftIndex);
                player.RecalculateEquipmentResources();
            } else {
                player.Equipment[leftIndex] = player.Equipment[leftIndex] with {
                    State = state with { Durability = checked(state.Durability - 1) },
                };
            }
        }

        var rightIndex = player.Equipment.FindIndex(item => item.Slot == 5);
        if (rightIndex < 0
            || !AuthoritativeItemClassification.IsShield(
                player.Equipment[rightIndex].State,
                items)) {
            return;
        }

        var rightState = player.Equipment[rightIndex].State;
        if (rightState.Durability == 255)
            return;
        if (rightState.Durability <= 1) {
            player.Equipment.RemoveAt(rightIndex);
            player.RecalculateEquipmentResources();
        } else {
            player.Equipment[rightIndex] = player.Equipment[rightIndex] with {
                State = rightState with { Durability = checked(rightState.Durability - 1) },
            };
        }
    }

    /** Applies native player-versus-player resource restoration after a hit. */
    private void ApplyPlayerAttackEffects(
        AuthoritativePlayerState player,
        int damageQ6,
        ICollection<DiabloGameplayEvent> events)
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.ResolvePlayerTarget(
            damageQ6,
            GetUsableEquipmentFlags(player),
            random.GenerateRnd,
            rules.RandomStealLifeDivisor);
        var lifeGain = Math.Min(
            Math.Max(0, player.LifeMaximum - player.Life),
            gain.LifeQ6);
        player.Life = checked(player.Life + lifeGain);
        if (lifeGain > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Healing,
                player.EntityId,
                player.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(lifeGain)) {
                AmountQ6 = lifeGain,
            });
        }
    }

    /** Mirrors native DamageArmor's helmet/chest selection and break path. */
    private void ApplyNativeArmorDurabilityLoss(AuthoritativePlayerState player)
    {
        var headIndex = player.Equipment.FindIndex(item => item.Slot == 0);
        var chestIndex = player.Equipment.FindIndex(item => item.Slot == 6);
        if (headIndex < 0 && chestIndex < 0)
            return;

        var targetHead = random.FlipCoin(3);
        if (chestIndex < 0)
            targetHead = true;
        else if (headIndex < 0)
            targetHead = false;

        var equipmentIndex = targetHead ? headIndex : chestIndex;
        if (equipmentIndex < 0)
            return;
        var state = player.Equipment[equipmentIndex].State;
        if (state.Durability == 255)
            return;
        if (state.Durability <= 1) {
            player.Equipment.RemoveAt(equipmentIndex);
            player.RecalculateEquipmentResources();
        } else {
            player.Equipment[equipmentIndex] = player.Equipment[equipmentIndex] with {
                State = state with { Durability = checked(state.Durability - 1) },
            };
        }
    }

    private bool HasNativeArmorDurabilityTarget(AuthoritativePlayerState player)
    {
        return player.Equipment.Any(item =>
            (item.Slot is 0 or 6)
            && item.State.MaxDurability > 0);
    }

    private bool HasNativeParryDurabilityTarget(AuthoritativePlayerState player)
    {
        return player.Equipment.Any(item =>
            (item.Slot is 4 or 5)
            && item.State.MaxDurability > 0
            && (AuthoritativeItemClassification.IsShield(item.State, items)
                || AuthoritativeItemClassification.IsStaff(item.State, items)));
    }

    /**
     * Applies native usable equipment damage and hit bonuses, returning Q6
     * damage. PvP deliberately keeps its native armor boundary separate from
     * monster combat: armor piercing only affects monster admission, player
     * armor only affects PvP admission, and neither reduces player damage.
     */
    private int ResolvePlayerAttackDamageQ6(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        bool monsterTarget,
        bool ranged,
        bool deferGargoyleWake,
        int targetDistance,
        bool adjacentDamage,
        uint seed,
        out int perilSelfDamageQ6,
        out bool perilActive,
        out bool doppelgangerProc,
        out bool attackHit)
    {
        perilSelfDamageQ6 = 0;
        perilActive = false;
        doppelgangerProc = false;
        attackHit = false;
        if (monsterTarget
            && !deferGargoyleWake
            && string.Equals(target.NativeAi, "Gargoyle", StringComparison.Ordinal)
            && target.GargoyleSpecialActive) {
            // Native PlrHitMonst/MonsterMHit calls tryLiftGargoyle before
            // applying damage. The waking hit is admitted but deals neither
            // physical nor equipment elemental damage.
            target.GargoyleSpecialActive = false;
            return 0;
        }
        var damagePercent = 0;
        var damageFlat = 0;
        var hitBonus = 0;
        var armorPiercingBonus = 0;
        foreach (var equippedItem in player.Equipment) {
            var state = equippedItem.State;
            if (!IsUsable(state) || (state.Magical != 0 && !state.Identified))
                continue;
            damagePercent = checked(damagePercent + state.PlusDamage);
            damageFlat = checked(damageFlat + state.PlusDamageModifier);
            hitBonus = checked(hitBonus + state.PlusToHit);
            armorPiercingBonus = checked(armorPiercingBonus + state.PlusEnemyArmorClass);
        }

        var playerClass = GetPlayerClass(player);
        // Legacy hand-built fixtures may omit class-derived attributes; retain
        // the global damage fallback for those compatibility templates.
        var usesNativeClassProfile = playerClass is not null
            && player.Attributes.Strength.Current > 0
            && player.Attributes.Dexterity.Current > 0;
        var damageProfile = usesNativeClassProfile
            ? AuthoritativeEquipmentDamageRules.Calculate(
                player.CharacterLevel,
                player.Attributes,
                player.Equipment,
                playerClass!,
                items,
                rules.BarbarianWeaponStrengthDamageDivisor,
                rules.RogueStrengthDexterityDamageDivisor,
                rules.MonkStaffUnarmedDamageDivisor,
                rules.MonkWeaponDamageDivisor,
                rules.BardSwordStrengthDexterityDamageDivisor,
                rules.BardBowStrengthDexterityDamageDivisor,
                rules.BardDefaultStrengthDamageDivisor,
                rules.StandardStrengthDamageDivisor,
                rules.BarbarianBowStrengthDamageDivisor,
                rules.BarbarianDefaultStrengthDamageDivisor,
                rules.BarbarianVitalityDamageDivisor)
            : new AuthoritativePhysicalDamageProfile(
                rules.BaseAttackDamage,
                rules.BaseAttackDamage,
                0);
        // Native replay consumes the weapon-damage stream before resolving
        // hit admission, so misses retain the range draw in the shared LCG.
        var rolledBaseDamage = usesNativeClassProfile
            ? random.RandomIntBetween(
                damageProfile.MinimumDamage,
                damageProfile.MaximumDamage)
            : rules.BaseAttackDamage;
        var damageBeforeFlat = checked(
            rolledBaseDamage
            + rolledBaseDamage * damagePercent / 100);
        var classDamageModifier = damageProfile.DamageModifier;
        if (!monsterTarget
            && ranged
            && usesNativeClassProfile
            && playerClass!.PhysicalDamageFormula
                != AuthoritativePhysicalDamageFormula.Rogue) {
            // Native Plr2PlrMHit halves _pDamageMod for non-Rogue physical
            // arrows; melee PvP and all monster attacks retain the full
            // class-derived modifier.
            classDamageModifier /= 2;
        }
        var damage = checked(damageBeforeFlat + damageFlat + classDamageModifier);
        perilActive = rules.GameVariant == DiabloGameVariant.Hellfire
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.PerilFlag);
        perilSelfDamageQ6 = 0;
        if (perilActive) {
            perilSelfDamageQ6 = checked(
                DeterministicFixedPointQ6.FromWhole(
                    checked(damageBeforeFlat + damageFlat))
                + DeterministicFixedPointQ6.FromWhole(
                    AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(
                        player.Equipment)));
            perilSelfDamageQ6 = Math.Max(0, perilSelfDamageQ6);
        }
        // Legacy hand-built fixtures may omit class-derived attributes; retain
        // the historical global hit fallback for those compatibility templates.
        var usesNativeClassToHit = playerClass is not null
            && player.Attributes.Strength.Current > 0
            && player.Attributes.Dexterity.Current > 0;
        var classHitChance = !usesNativeClassToHit
            ? rules.HitChancePercent
            : playerClass!.CalculatePhysicalToHit(
                player.CharacterLevel,
                player.Attributes.Dexterity.Current,
                ranged);
        var targetArmorClass = monsterTarget
            ? GetEffectiveMonsterArmorClass(target)
            : target.ArmorClass;
        var effectiveHitChance = usesNativeClassToHit
            ? monsterTarget
                ? rules.ResolvePhysicalHitChance(
                    checked(
                        classHitChance
                        + hitBonus
                        - (adjacentDamage
                            ? player.CharacterLevel > 20
                                ? 30
                                : checked((35 - (int)player.CharacterLevel) * 2)
                            : 0)
                        - (ranged
                            ? targetDistance * targetDistance / 2
                            : 0)),
                    targetArmorClass,
                    armorPiercingBonus,
                    meleeArmorReductionDivisor: !ranged
                        ? playerClass!.MeleeArmorReductionDivisor
                        : 0)
                : Math.Clamp(
                    checked(
                        classHitChance
                        + hitBonus
                        - (adjacentDamage
                            ? player.CharacterLevel > 20
                                ? 30
                                : checked((35 - (int)player.CharacterLevel) * 2)
                            : 0)
                        - (ranged
                            ? targetDistance * targetDistance / 2
                            : 0)
                        - targetArmorClass),
                    5,
                    95)
            : Math.Clamp(
                checked(
                    classHitChance
                    + hitBonus
                    + (monsterTarget ? armorPiercingBonus : 0)
                    - (!monsterTarget && ranged
                        ? targetDistance * targetDistance / 2
                        : 0)
                    - (!monsterTarget ? targetArmorClass : 0)),
                0,
                100);
        // Native PlrHitMonst/MonsterMHit set the hit roll to zero for a
        // petrified monster, making Stone Curse a guaranteed-hit boundary.
        if (monsterTarget && target.IsPetrified)
            effectiveHitChance = 100;
        attackHit = rules.ResolveAttackHit(effectiveHitChance, seed);
        if (!attackHit) {
            perilActive = false;
            perilSelfDamageQ6 = 0;
            return 0;
        }
        var criticalDamage = damage;
        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.CriticalStrike) == true
            && random.RandomIntLessThan(100) < Math.Min(
                100,
                checked(
                    (int)player.CharacterLevel
                    * playerClass.CriticalStrikeChancePerLevelPercent))) {
            criticalDamage = checked(
                criticalDamage * playerClass.CriticalDamageMultiplier);
        }
        criticalDamage = AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
            criticalDamage,
            target.IsUndead,
            player.Equipment,
            target.IsAnimal,
            items);
        var resolvedDamage = rules.ResolveAttackDamage(
            criticalDamage,
            // Fully initialized class profiles use native admission-only
            // monster armor. Legacy compatibility templates have no class
            // damage profile and retain the historical armor reduction.
            !usesNativeClassProfile && monsterTarget ? targetArmorClass : 0,
            effectiveHitChance,
            seed);
        if (target.IsDemon
            && (GetUsableEquipmentFlags(player)
                & AuthoritativeEquipmentAttackEffectRules.TripleDemonDamageFlag) != 0) {
            resolvedDamage = checked(resolvedDamage * rules.TripleDemonDamageMultiplier);
        }
        if (rules.GameVariant == DiabloGameVariant.Hellfire
            && resolvedDamage > 0
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.DevastationFlag)
            && random.RandomIntLessThan(100) < rules.HellfireDevastationChancePercent) {
            resolvedDamage = checked(
                resolvedDamage * rules.HellfireDevastationDamageMultiplier);
        }
        if (rules.GameVariant == DiabloGameVariant.Hellfire
            && resolvedDamage > 0
            && !target.IsDiablo
            && !target.IsUnique
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag)
            && random.RandomIntLessThan(100) < rules.HellfireDoppelgangerChancePercent) {
            doppelgangerProc = true;
        }
        var resolvedDamageQ6 = DeterministicFixedPointQ6.FromWhole(resolvedDamage);
        if (rules.GameVariant == DiabloGameVariant.Hellfire
            && resolvedDamageQ6 > 0
            && HasUsableHellfireDamageArmorFlag(
                player,
                AuthoritativeEquipmentIncomingDamageRules.JestersFlag)) {
            var roll = random.RandomIntBetween(0, rules.HellfireJestersRollMaximum);
            if (roll >= rules.HellfireJestersExpansionThreshold) {
                roll = checked(
                    rules.HellfireJestersExpansionThreshold
                    + (roll - rules.HellfireJestersExpansionThreshold)
                        * rules.HellfireJestersExpansionMultiplier);
            }
            resolvedDamageQ6 = checked(resolvedDamageQ6 * roll / 100);
        }
        if (adjacentDamage)
            resolvedDamageQ6 >>= 2;
        if (perilActive)
            resolvedDamageQ6 = checked(resolvedDamageQ6 * rules.HellfirePerilDamageMultiplier);
        return resolvedDamageQ6;
    }

    /** Spawns the native full-health, non-unique Doppelganger copy. */
    private void SpawnDoppelganger(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        (int X, int Y)? position = null;
        foreach (var displacement in DirectionDisplacements) {
            var candidateX = checked(target.PositionX + displacement.X);
            var candidateY = checked(target.PositionY + displacement.Y);
            if (world.IsWalkable(target.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    target.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    target.EntityId)) {
                position = (candidateX, candidateY);
            }
        }
        if (!position.HasValue)
            return;

        var entityId = AllocateMonsterEntityId(players);
        var copy = new AuthoritativeCombatTarget(
            entityId,
            position.Value.X,
            position.Value.Y,
            DeterministicFixedPointQ6.ToWholeCeiling(target.MaxHitPointsQ6),
            armorClass: target.ArmorClass,
            maxHitPoints: DeterministicFixedPointQ6.ToWholeCeiling(target.MaxHitPointsQ6),
            levelId: target.LevelId,
            monsterId: target.MonsterId,
            attackDamage: target.AttackDamage,
            aggroRange: target.AggroRange,
            fireResistance: target.FireResistance,
            lightningResistance: target.LightningResistance,
            magicResistance: target.MagicResistance,
            acidResistance: target.AcidResistance,
            monsterLevel: target.MonsterLevel,
            stoneCurseImmune: target.StoneCurseImmune,
            undead: target.IsUndead,
            minDamage: target.MinDamage,
            maxDamage: target.MaxDamage,
            minDamageSpecial: target.MinDamageSpecial,
            maxDamageSpecial: target.MaxDamageSpecial,
            berserkImmune: target.BerserkImmune,
            deathAnimationTicks: target.DeathAnimationTicks,
            experienceReward: target.ExperienceReward,
            toHit: target.ToHit,
            toHitSpecial: target.ToHitSpecial,
            nativeAi: target.NativeAi,
            intelligence: target.Intelligence,
            gargoyleSpecialActive: target.GargoyleSpecialActive,
            demon: target.IsDemon,
            animal: target.IsAnimal,
            knockback: target.HasKnockback);
        copy.RestoreHitPointsQ6(target.MaxHitPointsQ6, target.MaxHitPointsQ6);
        world.CombatTargets.Add(entityId, copy);
    }

    private uint AllocateMonsterEntityId(
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return monsterEntityIds.Allocate(entityId =>
            world.CombatTargets.ContainsKey(entityId)
            || world.WorldItems.ContainsKey(entityId)
            || world.Objects.ContainsKey(entityId)
            || world.Projectiles.ContainsKey(entityId)
            || world.Portals.ContainsKey(entityId)
            || world.Corpses.ContainsKey(entityId)
            || players.Any(player => player.EntityId == entityId));
    }

    private bool TryGetAttackTarget(
        AuthoritativePlayerState player,
        uint targetEntityId,
        bool ranged,
        out AuthoritativeCombatTarget target)
    {
        if (!world.CombatTargets.TryGetValue(targetEntityId, out target!)
            || target.HitPoints <= 0
            || target.SneakHidden
            || target.DialogueState == AuthoritativeMonsterDialogueState.Inquiring
            || IsQuestDialogueTarget(target)
            || IsNativeRetreatTarget(target)
            || (target.LevelId != 0 && target.LevelId != player.LevelId)) {
            return false;
        }
        var distance = DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY);
        return ranged
            ? distance > 0 && world.HasMissileLineOfSight(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                target.PositionX,
                target.PositionY)
            : distance <= 1;
    }

    /**
     * Mirrors the native Monster::isPossibleToHit goal gates that are
     * represented by authoritative compatibility timers.  The timers are
     * server-owned state, so this admission applies equally to immediate,
     * delayed, and effect-time revalidated player attacks.
     */
    private bool IsNativeRetreatTarget(AuthoritativeCombatTarget target)
    {
        if (UsesNativeCounselorAi(target) && target.CounselorRetreatTicks != 0)
            return true;

        // Illusion Weaver uses the native Sneak controller; its retreat
        // countdown is the authoritative representation of MonsterGoal::Retreat.
        return UsesNativeAi(target, "Sneak") && target.SneakRetreatTicks != 0;
    }

    /**
     * Keeps content-owned unique monsters non-combatant until their authored
     * staged dialogue reaches its terminal native variable marker.
     */
    private bool IsQuestDialogueTarget(AuthoritativeCombatTarget target)
    {
        if (!target.IsUnique || target.UniqueIdentity.Length == 0)
            return false;

        foreach (var quest in world.Quests.Values) {
            if (!quest.Active
                || quest.Unavailable
                || quest.Completed
                || (quest.LevelId != 0 && quest.LevelId != target.LevelId)
                || !string.Equals(
                    quest.CompletionUniqueIdentity,
                    target.UniqueIdentity,
                    StringComparison.Ordinal)) {
                continue;
            }

            if (questDialogueStages?.HasPendingStage(
                    quest.QuestId,
                    quest.Variable1,
                    quest.Variable2) == true) {
                return true;
            }

            // Older packs retain the native terminal marker until their staged
            // dialogue route is migrated.
            if (quest.DialogueCompletionVariable1 >= 0
                && quest.Variable1 < quest.DialogueCompletionVariable1) {
                return true;
            }
        }

        return false;
    }

    /** Native LazarusMinionAi keeps summoned succubi non-combatant until the single-player quest gate opens. */
    private bool IsLazarusMinionCombatSuppressed(AuthoritativeCombatTarget target)
    {
        if (gameMode == DiabloGameMode.Multiplayer
            || !UsesNativeAi(target, "LazarusSuccubus"))
            return false;

        return world.Quests.Values.Any(quest =>
            quest.Active
            && !quest.Unavailable
            && !quest.Completed
            && quest.Variable1 <= 5
            && string.Equals(
                quest.CompletionUniqueIdentity,
                "Lazarus",
                StringComparison.Ordinal));
    }

    private bool TryGetPlayerAttackTarget(
        AuthoritativePlayerState player,
        uint targetEntityId,
        bool ranged,
        IReadOnlyList<AuthoritativePlayerState> players,
        out AuthoritativePlayerState target)
    {
        target = players.FirstOrDefault(candidate =>
            candidate.EntityId == targetEntityId
            && candidate.EntityId != player.EntityId)!;
        if (target is null
            || target.Life <= 0
            || (target.LevelId != 0 && target.LevelId != player.LevelId)
            || (!rules.FriendlyFire && player.FriendlyMode)
            || IsPlayerProtectedByFlash(target)
            || IsPlayerEtherealized(target)) {
            return false;
        }

        var distance = DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            player.PositionX,
            player.PositionY);
        return ranged
            ? distance > 0 && world.HasMissileLineOfSight(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                target.PositionX,
                target.PositionY)
            : distance <= 1;
    }

    private DiabloCombatIntentResult Cast(
        AuthoritativePlayerState player,
        DiabloCastIntent intent,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        intent = NormalizeVisiblePlayerTarget(player, intent, players);
        if (!useNativePlayerActionTiming)
            return ResolveCast(player, intent, appliedTick, players: players);
        if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var validation = ResolveCast(
            player,
            intent,
            applyEffects: false,
            appliedTick: appliedTick,
            players: players);
        if (!validation.Succeeded)
            return validation;

        UpdateFacingForCast(player, intent, players);
        var animation = playerAnimations!.GetRequired(player.CharacterClassId);
        var effectTick = checked(appliedTick + (ulong)animation.CastingActionFrame);
        var endTick = checked(appliedTick + (ulong)(animation.CastingFrames - 1));
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Cast,
                appliedTick,
                effectTick,
                effectTick,
                endTick) {
                TargetEntityId = intent.TargetEntityId,
                CastIntent = intent,
            },
            DiabloPlayerCombatMode.Spell);
        return DiabloCombatIntentResult.Accepted;
    }

    private DiabloCombatIntentResult ResolveCast(
        AuthoritativePlayerState player,
        DiabloCastIntent intent,
        ulong appliedTick,
        bool applyEffects = true,
        IReadOnlyList<AuthoritativePlayerState>? players = null,
        bool skipSourceValidation = false)
    {
        players ??= [player];
        intent = NormalizeVisiblePlayerTarget(player, intent, players);
        if (!spells.TryGet(intent.SpellId, out var spell))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        ResolvedCastSource source = default!;
        if (!skipSourceValidation
            && !TryResolveCastSource(player, intent, out source, out var sourceFailure))
            return DiabloCombatIntentResult.Rejected(sourceFailure);
        var isRuneFire = source.Kind == DiabloCastSourceKind.ConsumableItem
            && IsRuneFireSpell(spell);
        var hasDamage = HasDamage(spell);
        var isSelfCenteredFlash = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Flash;
        var isResurrection = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Resurrection;
        var isHealOther = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.HealOther;
        var isManaShield = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.ManaShield;
        var isReflect = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Reflect;
        var isMagi = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Magi;
        var isMana = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Mana;
        var isRingOfFire = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.RingOfFireController;
        var isImmolation = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.ImmolationController;
        var isJester = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Jester;
        var isBerserk = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Berserk;
        var isWarp = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Warp;
        var isStoneCurse = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.StoneCurse;
        var isInfravision = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Infravision;
        var isSearch = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Search;
        var isEtherealize = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Etherealize;
        var isPhasing = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Phasing;
        var isFireWall = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.FireWallController;
        var isLightningWall = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.LightningWallController;
        var isFlameWave = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.FlameWaveController;
        var isInferno = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.InfernoController;
        var isChainLightning = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.ChainLightningController;
        var isNova = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.NovaController;
        var isApocalypse = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.ApocalypseController;
        var isRage = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.RageController;
        var isElemental = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Elemental;
        var isGuardian = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.GuardianController;
        var isGolem = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Golem;
        var isTelekinesis = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.Telekinesis;
        var isTownPortal = spell.ProjectileBehavior
            == AuthoritativeProjectileBehavior.TownPortal;
        var canTargetLivingPlayer = CanTargetLivingPlayer(spell);
        if (isSelfCenteredFlash
            && intent.TargetEntityId != 0
            && intent.TargetEntityId != player.EntityId) {
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if ((isFireWall || isRingOfFire || isInferno || isChainLightning || isNova || isApocalypse || isElemental || isGuardian)
            && intent.TargetEntityId != 0)
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!hasDamage && !isResurrection && !isHealOther && !isStoneCurse && !isTelekinesis && !isTownPortal && !isBerserk && !isWarp
            && intent.TargetEntityId != 0
            && intent.TargetEntityId != player.EntityId) {
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        AuthoritativeCombatTarget? target = null;
        AuthoritativePlayerState? damagePlayerTarget = null;
        AuthoritativePlayerState? resurrectionTarget = null;
        AuthoritativePlayerState? healOtherTarget = null;
        IReadOnlyList<AuthoritativeCombatTarget> damageTargets = [];
        (int X, int Y)? runeFirePosition = null;
        var centerX = isSelfCenteredFlash || isPhasing || isApocalypse || isReflect || isMagi || isMana || isRingOfFire
            ? player.PositionX
            : intent.TargetX;
        var centerY = isSelfCenteredFlash || isPhasing || isApocalypse || isReflect || isMagi || isMana || isRingOfFire
            ? player.PositionY
            : intent.TargetY;
        if (isResurrection) {
            resurrectionTarget = players.FirstOrDefault(candidate =>
                candidate.EntityId != 0
                && candidate.EntityId == intent.TargetEntityId);
            if (intent.TargetEntityId == 0
                || intent.TargetEntityId == player.EntityId
                || resurrectionTarget is null
                || resurrectionTarget.LevelId != player.LevelId
                || DeterministicGridMetrics.ChebyshevDistance(
                    resurrectionTarget.PositionX,
                    resurrectionTarget.PositionY,
                    player.PositionX,
                    player.PositionY) > spell.Range) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            centerX = resurrectionTarget.PositionX;
            centerY = resurrectionTarget.PositionY;
        }
        if (isHealOther) {
            healOtherTarget = players.FirstOrDefault(candidate =>
                candidate.EntityId != 0
                && candidate.EntityId == intent.TargetEntityId);
            if (intent.TargetEntityId == 0
                || intent.TargetEntityId == player.EntityId
                || healOtherTarget is null
                || healOtherTarget.Life <= 0
                || healOtherTarget.LevelId != player.LevelId
                || DeterministicGridMetrics.ChebyshevDistance(
                    healOtherTarget.PositionX,
                    healOtherTarget.PositionY,
                    player.PositionX,
                    player.PositionY) > spell.Range) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            centerX = healOtherTarget.PositionX;
            centerY = healOtherTarget.PositionY;
        }
        if ((hasDamage || isBerserk) && !isSelfCenteredFlash && !isApocalypse && !isRingOfFire) {
            if (intent.TargetEntityId == 0
                && !world.ContainsPosition(player.LevelId, intent.TargetX, intent.TargetY)) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            if (isBerserk) {
                target = FindBerserkTarget(
                    player.LevelId,
                    intent.TargetEntityId,
                    intent.TargetX,
                    intent.TargetY,
                    spell.BerserkSearchRadius,
                    spell.BerserkResistanceFrequency,
                    respectMagicResistance: false);
            } else if (intent.TargetEntityId != 0) {
                world.CombatTargets.TryGetValue(intent.TargetEntityId, out target);
                if (target is null
                    && canTargetLivingPlayer
                    && TryGetPlayerAttackTarget(
                        player,
                        intent.TargetEntityId,
                        ranged: true,
                        players,
                        out var selectedPlayerTarget)) {
                    damagePlayerTarget = selectedPlayerTarget;
                }
            } else {
                target = world.CombatTargets.Values
                    .Where(candidate => candidate.HitPoints > 0
                        && !candidate.SneakHidden
                        && (candidate.LevelId == 0 || candidate.LevelId == player.LevelId)
                        && candidate.PositionX == intent.TargetX
                        && candidate.PositionY == intent.TargetY)
                    .OrderBy(candidate => candidate.EntityId)
                    .FirstOrDefault();
            }
            centerX = target?.PositionX
                ?? damagePlayerTarget?.PositionX
                ?? intent.TargetX;
            centerY = target?.PositionY
                ?? damagePlayerTarget?.PositionY
                ?? intent.TargetY;
            if (isRuneFire) {
                runeFirePosition = FindRuneFirePosition(player, centerX, centerY);
                if (!runeFirePosition.HasValue)
                    return DiabloCombatIntentResult.Accepted;
                centerX = runeFirePosition.Value.X;
                centerY = runeFirePosition.Value.Y;
            }
            if ((intent.TargetEntityId != 0
                    && target is null
                    && damagePlayerTarget is null)
                || (isBerserk && target is null)
                || (target is not null
                    && (target.HitPoints <= 0
                        || target.SneakHidden
                        || IsQuestDialogueTarget(target)
                        || (target.LevelId != 0 && target.LevelId != player.LevelId)))
                || DeterministicGridMetrics.ChebyshevDistance(
                    centerX,
                    centerY,
                    player.PositionX,
                    player.PositionY) > spell.Range
                || (!(isFireWall || isLightningWall || isRingOfFire || isFlameWave || isInferno || isChainLightning || isNova || isJester) && spell.AreaRadius == 0
                    && spell.ProjectileSpeed == 0
                    && target is null
                    && damagePlayerTarget is null)) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            var hasLineOfSight = isFireWall || isLightningWall || isRingOfFire || isFlameWave || isInferno || isChainLightning || isNova || spell.ProjectileSpeed > 0
                ? world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    centerX,
                    centerY)
                : world.HasTargetLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    centerX,
                    centerY,
                    player.LightRadius);
            if (!hasLineOfSight) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }

            damageTargets = isBerserk || isFireWall || isLightningWall || isRingOfFire || isFlameWave || isInferno || isChainLightning || isNova
                ? []
                : world.CombatTargets.Values
                    .Where(candidate => candidate.HitPoints > 0
                        && !candidate.SneakHidden
                        && (candidate.LevelId == 0 || candidate.LevelId == player.LevelId)
                        && DeterministicGridMetrics.ChebyshevDistance(
                            candidate.PositionX,
                            candidate.PositionY,
                            centerX,
                            centerY) <= spell.AreaRadius)
                    .OrderBy(candidate => candidate.EntityId)
                    .ToArray();
            if (spell.AreaRadius == 0 && target is not null)
                damageTargets = [target];
        }
        if (spell.HealingAmount > 0
            && spell.ProjectileBehavior != AuthoritativeProjectileBehavior.Healing
            && spell.ProjectileBehavior != AuthoritativeProjectileBehavior.HealOther
            && player.Life >= player.LifeMaximum) {
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var effectiveSpellLevel = isRuneFire
            ? 0
            : GetEffectiveSpellLevel(player, spell.SpellId);
        var manaCost = ResolveManaCost(player, spell, effectiveSpellLevel);
        var statusDuration = isInfravision
            ? ResolveInfravisionDuration(spell, effectiveSpellLevel)
            : isSearch
                ? ResolveSearchDuration(spell, player.CharacterLevel, effectiveSpellLevel)
            : spell.StatusDuration;

        long projectileTravelTicks = 0;
        if (spell.ProjectileSpeed > 0
            && spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Targeted) {
            var distance = DeterministicGridMetrics.ChebyshevDistance(
                centerX,
                centerY,
                player.PositionX,
                player.PositionY);
            projectileTravelTicks = Math.Max(
                1L,
                (distance + spell.ProjectileSpeed - 1) / spell.ProjectileSpeed);
            if (projectileTravelTicks > spell.ProjectileLifetime)
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var wallDirection = isFireWall || isLightningWall
            ? GetDirection(player.PositionX, player.PositionY, centerX, centerY)
            : -1;
        var wallSpreadPosition = isFireWall || isLightningWall
            ? FindWallSpreadPosition(player, centerX, centerY)
            : null;
        var guardianSpawnPosition = isGuardian
            ? FindGuardianSpawnPosition(player, centerX, centerY, spell, players)
            : null;
        if (isGolem
            && intent.TargetEntityId == 0
            && !world.ContainsPosition(player.LevelId, intent.TargetX, intent.TargetY)) {
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var golemSpawnPosition = isGolem
            ? FindGolemSpawnPosition(player, centerX, centerY, players)
            : null;
        if ((isFireWall || isLightningWall) && !wallSpreadPosition.HasValue)
            return DiabloCombatIntentResult.Accepted;
        if (isGuardian && !guardianSpawnPosition.HasValue)
            return DiabloCombatIntentResult.Accepted;
        if (isGolem && !golemSpawnPosition.HasValue)
            return DiabloCombatIntentResult.Accepted;
        if (isTelekinesis) {
            if (worldCommands is null)
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            var telekinesisValidation = worldCommands.ExecuteTelekinesis(
                player,
                intent.TargetEntityId,
                intent.TargetX,
                intent.TargetY,
                appliedTick,
                players,
                applyEffects: false);
            if (!telekinesisValidation.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    telekinesisValidation.Failure ?? DiabloRuleFailure.InvalidTarget);
        }
        if (isTownPortal) {
            if (worldCommands is null
                || player.LevelId == rules.TownLevelId
                || intent.TargetEntityId != 0
                || !world.ContainsPosition(player.LevelId, intent.TargetX, intent.TargetY)
                || DeterministicGridMetrics.ChebyshevDistance(
                    intent.TargetX,
                    intent.TargetY,
                    player.PositionX,
                    player.PositionY) > spell.Range
                || !world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    intent.TargetX,
                    intent.TargetY)) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            var townPortalValidation = worldCommands.ExecuteTownPortal(
                player,
                intent.TargetX,
                intent.TargetY,
                appliedTick,
                players,
                applyEffects: false,
                searchRadius: spell.TownPortalSearchRadius,
                lifetime: spell.TownPortalLifetime,
                destinationPositionX: GetTownPortalDestinationPositionX(spell),
                destinationPositionY: GetTownPortalDestinationPositionY(spell),
                destinationSlotCount: GetTownPortalDestinationSlotCount(spell),
                destinationSlotSpacing: GetTownPortalDestinationSlotSpacing(spell));
            if (!townPortalValidation.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    townPortalValidation.Failure ?? DiabloRuleFailure.InvalidTarget);
        }
        if (isGolem) {
            if (intent.TargetEntityId != 0
                || !world.ContainsPosition(player.LevelId, intent.TargetX, intent.TargetY)
                || DeterministicGridMetrics.ChebyshevDistance(
                    intent.TargetX,
                    intent.TargetY,
                    player.PositionX,
                    player.PositionY) > spell.Range
                || !world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    intent.TargetX,
                    intent.TargetY)) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
        }
        if (isWarp) {
            if (worldCommands is null)
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            var warpValidation = worldCommands.ExecuteWarp(
                player,
                appliedTick,
                players,
                applyEffects: false,
                searchRadius: spell.WarpSearchRadius);
            if (!warpValidation.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    warpValidation.Failure ?? DiabloRuleFailure.InvalidTarget);
        }
        if (!skipSourceValidation
            && source.Kind == DiabloCastSourceKind.MemorizedSpell
            && (AuthoritativeEquipmentManaRules.HasNoManaItem(player.Equipment)
                || player.Mana < manaCost)) {
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);
        }
        if (!applyEffects)
            return DiabloCombatIntentResult.Accepted;

        UpdateFacingToward(player, centerX, centerY);

        // Native AddManaShield fizzles an already-active cast before its source
        // is consumed, while still accepting the cast action itself.
        if (isManaShield && player.ManaShieldActive)
            return DiabloCombatIntentResult.Accepted;
        if (isRage
            && (player.Life <= checked((int)player.CharacterLevel
                    * (spell.RageMinimumLifeQ6PerCharacterLevel > 0
                        ? spell.RageMinimumLifeQ6PerCharacterLevel
                        : 64))
                || world.Projectiles.Values.Any(projectile =>
                    projectile.Behavior == AuthoritativeProjectileBehavior.RageController
                    && IsProjectileOwner(projectile, player)))) {
            return DiabloCombatIntentResult.Accepted;
        }

        (int X, int Y)? teleportDestination = null;
        if (spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Teleport) {
            teleportDestination = FindClosestTeleportDestination(
                player,
                intent.TargetX,
                intent.TargetY,
                players,
                spell.TeleportSearchRadius);
            if (!teleportDestination.HasValue)
                return DiabloCombatIntentResult.Accepted;
        }

        var phasingDestination = isPhasing
            ? FindPhasingDestination(player, spell, players)
            : null;

        (AuthoritativeCombatTarget Target, int X, int Y)? stoneCurseTarget = null;
        if (isStoneCurse) {
            stoneCurseTarget = FindStoneCurseTarget(
                player.LevelId,
                intent.TargetX,
                intent.TargetY,
                spell.StoneCurseSearchRadius);
            if (!stoneCurseTarget.HasValue)
                return DiabloCombatIntentResult.Accepted;
        }
        var createStoneCurseController = stoneCurseTarget.HasValue
            && !stoneCurseTarget.Value.Target.IsPetrified;

        if (!skipSourceValidation)
            ConsumeCastSource(player, source, manaCost);
        var events = new List<DiabloGameplayEvent>();
        if (isBerserk) {
            target = FindBerserkTarget(
                player.LevelId,
                intent.TargetEntityId,
                intent.TargetX,
                intent.TargetY,
                spell.BerserkSearchRadius,
                spell.BerserkResistanceFrequency,
                respectMagicResistance: true);
        }
        if (isTelekinesis) {
            var telekinesisResult = worldCommands!.ExecuteTelekinesis(
                player,
                intent.TargetEntityId,
                intent.TargetX,
                intent.TargetY,
                appliedTick,
                players);
            if (!telekinesisResult.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    telekinesisResult.Failure ?? DiabloRuleFailure.Malformed);
            events.AddRange(telekinesisResult.Events);
        }
        if (isTownPortal) {
            var townPortalResult = worldCommands!.ExecuteTownPortal(
                player,
                intent.TargetX,
                intent.TargetY,
                appliedTick,
                players,
                searchRadius: spell.TownPortalSearchRadius,
                lifetime: spell.TownPortalLifetime,
                destinationPositionX: GetTownPortalDestinationPositionX(spell),
                destinationPositionY: GetTownPortalDestinationPositionY(spell),
                destinationSlotCount: GetTownPortalDestinationSlotCount(spell),
                destinationSlotSpacing: GetTownPortalDestinationSlotSpacing(spell));
            if (!townPortalResult.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    townPortalResult.Failure ?? DiabloRuleFailure.Malformed);
        }
        if (isWarp) {
            var warpResult = worldCommands!.ExecuteWarp(
                player,
                appliedTick,
                players,
                searchRadius: spell.WarpSearchRadius);
            if (!warpResult.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    warpResult.Failure ?? DiabloRuleFailure.Malformed);
            events.AddRange(warpResult.Events);
        }
        ApplyImmediateSelfDamage(
            player,
            spell,
            appliedTick,
            players,
            events);
        if (resurrectionTarget is not null && resurrectionTarget.Life <= 0) {
            ApplyResurrection(resurrectionTarget, spell, players);
        } else if (healOtherTarget is not null) {
            var lifeBefore = healOtherTarget.Life;
            var healingQ6 = ResolveHealingAmountQ6(
                player,
                spell,
                effectiveSpellLevel,
                useHealOtherClassMultiplier: true);
            healOtherTarget.Life = checked(
                healOtherTarget.Life
                + Math.Min(healingQ6, healOtherTarget.LifeMaximum - healOtherTarget.Life));
            var appliedHealingQ6 = healOtherTarget.Life - lifeBefore;
            if (appliedHealingQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Healing,
                    player.EntityId,
                    healOtherTarget.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(appliedHealingQ6)) {
                    AmountQ6 = appliedHealingQ6,
                });
            }
        } else if (isManaShield) {
            player.ManaShieldActive = true;
        } else if (isReflect) {
            // Native AddReflect uses spell level 2 when the cast does not carry
            // an explicit level, then adds level * character level to uint16
            // charges. Overflow intentionally fizzles the charge increment.
            var reflectSpellLevel = effectiveSpellLevel == 0
                ? (spell.ReflectDefaultSpellLevel > 0
                    ? spell.ReflectDefaultSpellLevel
                    : 2)
                : effectiveSpellLevel;
            var chargeIncrement = checked((uint)reflectSpellLevel * player.CharacterLevel);
            if (chargeIncrement <= NativeCompatibilityLimits.MaximumNativeReflectCharges - player.ReflectCharges)
                player.ReflectCharges += chargeIncrement;
        } else if (isMagi) {
            player.Mana = player.ManaMaximum;
        } else if (isMana) {
            player.Mana = Math.Min(
                player.ManaMaximum,
                checked(player.Mana + ResolveManaRestorationQ6(
                    player,
                    spell,
                    effectiveSpellLevel)));
        } else if (isGolem) {
            SpawnGolem(
                player,
                golemSpawnPosition!.Value,
                effectiveSpellLevel,
                spell,
                spell.GolemDeathAnimationTicks);
        } else if (isBerserk && target is not null) {
            ApplyBerserk(target, spell, effectiveSpellLevel);
        } else if (isJester) {
            var jesterSpellId = SelectJesterSpellId(spell);
            var jesterResult = ResolveCast(
                player,
                intent with {
                    SpellId = jesterSpellId,
                    Source = DiabloCastSourceKind.Unspecified,
                },
                appliedTick,
                applyEffects: true,
                players,
                skipSourceValidation: true);
            if (!jesterResult.Succeeded)
                return jesterResult;
            events.AddRange(jesterResult.Events);
        } else if (createStoneCurseController) {
            stoneCurseTarget!.Value.Target.IsPetrified = true;
        } else if (spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Healing) {
            var lifeBefore = player.Life;
            var healingQ6 = ResolveHealingAmountQ6(player, spell, effectiveSpellLevel);
            player.Life = checked(
                player.Life + Math.Min(healingQ6, player.LifeMaximum - player.Life));
            var appliedHealingQ6 = player.Life - lifeBefore;
            if (appliedHealingQ6 > 0) {
                events.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Healing,
                    player.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(appliedHealingQ6)) {
                    AmountQ6 = appliedHealingQ6,
                });
            }
        } else if (spell.HealingAmount > 0) {
            player.Life = Math.Min(player.LifeMaximum, player.Life + spell.HealingAmount);
        }
        if (spell.StatusEffectId != 0) {
            var previousDuration = isInfravision || isSearch
                ? player.StatusEffects
                    .Where(effect => effect.EffectId == spell.StatusEffectId)
                    .Select(effect => effect.RemainingTicks)
                    .DefaultIfEmpty()
                    .Max()
                : 0;
            player.StatusEffects.RemoveAll(effect => effect.EffectId == spell.StatusEffectId);
            var combinedDuration = isSearch
                ? AddSearchDuration(previousDuration, statusDuration)
                : Math.Max(previousDuration, statusDuration);
            player.StatusEffects.Add(new AuthoritativeStatusEffect(
                spell.StatusEffectId,
                combinedDuration,
                spell.StatusMagnitude) {
                Behavior = isInfravision
                    ? AuthoritativeStatusEffectBehavior.Infravision
                    : isSearch
                        ? AuthoritativeStatusEffectBehavior.Search
                    : isEtherealize
                        ? AuthoritativeStatusEffectBehavior.Etherealize
                    : AuthoritativeStatusEffectBehavior.None,
            });
        }

        if (isRuneFire) {
            var explosion = CreateRuneFireExplosion(
                player,
                spell,
                centerX,
                centerY);
            var directedEvents = new List<DiabloDirectedGameplayEvent>();
            EnterFireballImpact(
                appliedTick,
                explosion,
                players,
                directedEvents);
            events.AddRange(directedEvents.Select(directed => directed.Event));
        } else if (createStoneCurseController) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateStoneCurseController(
                    projectileEntityId,
                    player,
                    stoneCurseTarget!.Value,
                    spell,
                    effectiveSpellLevel));
        } else if (resurrectionTarget is not null) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateResurrectionBeam(
                    projectileEntityId,
                    player,
                    resurrectionTarget,
                    spell,
                    centerX,
                    centerY));
        } else if (isRingOfFire) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateRingOfFireController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel));
        } else if (isImmolation) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateImmolationController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel,
                    centerX,
                    centerY));
        } else if (wallSpreadPosition.HasValue) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateWallController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel,
                    wallSpreadPosition.Value,
                    wallDirection));
        } else if (teleportDestination.HasValue) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateMovementController(
                    projectileEntityId,
                    player,
                    spell,
                    teleportDestination.Value,
                    AuthoritativeProjectileBehavior.Teleport));
        } else if (phasingDestination.HasValue) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateMovementController(
                    projectileEntityId,
                    player,
                    spell,
                    phasingDestination.Value,
                    AuthoritativeProjectileBehavior.Phasing));
        } else if (isSelfCenteredFlash) {
            var bottomEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                bottomEntityId,
                CreateFlashLayer(
                    bottomEntityId,
                    player,
                    spell,
                    effectiveSpellLevel,
                    AuthoritativeProjectileBehavior.FlashBottom));
            var topEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                topEntityId,
                CreateFlashLayer(
                    topEntityId,
                    player,
                    spell,
                    effectiveSpellLevel,
                    AuthoritativeProjectileBehavior.FlashTop));
        } else if (isChainLightning) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateChainLightningController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel,
                    centerX,
                    centerY));
        } else if (isNova) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateNovaController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel,
                    centerX,
                    centerY));
        } else if (isApocalypse) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateApocalypseController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel));
        } else if (isRage) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateRageController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel));
        } else if (isGuardian) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                CreateGuardianController(
                    projectileEntityId,
                    player,
                    spell,
                    effectiveSpellLevel,
                    guardianSpawnPosition!.Value));
        } else if (spell.ProjectileSpeed > 0) {
            var projectileCount = checked(
                spell.ProjectileCount
                + (spell.ProjectileCountLevelDivisor == 0
                    ? 0
                    : effectiveSpellLevel / spell.ProjectileCountLevelDivisor));
            for (var projectileIndex = 0;
                 projectileIndex < projectileCount;
                 projectileIndex++) {
                var projectileEntityId = AllocateProjectileEntityId();
                var projectile = spell.ProjectileBehavior switch {
                    AuthoritativeProjectileBehavior.ChargedBolt =>
                        CreateChargedBoltProjectile(
                            projectileEntityId,
                            player,
                            spell,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.Firebolt =>
                        CreateFireboltProjectile(
                            projectileEntityId,
                            player,
                            spell,
                            effectiveSpellLevel,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.HolyBolt =>
                        CreateHolyBoltProjectile(
                            projectileEntityId,
                            player,
                            spell,
                            effectiveSpellLevel,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.BloodStar =>
                        CreateBloodStarProjectile(
                            projectileEntityId,
                            player,
                            spell,
                            effectiveSpellLevel,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.BoneSpirit =>
                        CreateBoneSpiritProjectile(
                            projectileEntityId,
                            player,
                            spell,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.Fireball =>
                        CreateFireballProjectile(
                            projectileEntityId,
                            player,
                            spell,
                            effectiveSpellLevel,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.Lightning =>
                        CreateLightningController(
                            projectileEntityId,
                            player,
                            spell,
                            effectiveSpellLevel,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.FlameWaveController =>
                        CreateFlameWaveController(
                            projectileEntityId,
                            player,
                            spell,
                            effectiveSpellLevel,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.InfernoController =>
                        CreateInfernoController(
                            projectileEntityId,
                            player,
                            spell,
                            centerX,
                            centerY),
                    AuthoritativeProjectileBehavior.Elemental =>
                        CreateElementalProjectile(
                            projectileEntityId,
                            player,
                            spell,
                            effectiveSpellLevel,
                            centerX,
                            centerY),
                    _ => new AuthoritativeProjectile(
                        projectileEntityId,
                        player.EntityId == 0 ? 1U : player.EntityId,
                        target?.EntityId ?? 0,
                        spell.SpellId,
                        player.LevelId,
                        player.PositionX,
                        player.PositionY,
                        centerX,
                        centerY,
                        spell.DamageAmount,
                        spell.DamageType,
                        spell.AreaRadius,
                        checked((uint)projectileTravelTicks)),
                };
                world.Projectiles.Add(projectileEntityId, projectile);
            }
        } else {
            foreach (var damageTarget in damageTargets)
                ApplyDamage(player, damageTarget, spell.DamageAmount, spell.DamageType, events);
        }
        return events.Count == 0
            ? DiabloCombatIntentResult.Accepted
            : new DiabloCombatIntentResult(true, null, events);
    }

    /**
     * Treats a visible remote-player identity as a presentation hint for
     * spells that cannot target players, preserving the clicked tile as the
     * authoritative target for those spells.
     */
    private DiabloCastIntent NormalizeVisiblePlayerTarget(
        AuthoritativePlayerState player,
        DiabloCastIntent intent,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (intent.TargetEntityId == 0
            || !players.Any(candidate => candidate.EntityId == intent.TargetEntityId
                && candidate.EntityId != player.EntityId)
            || !spells.TryGet(intent.SpellId, out var spell)
            || spell.ProjectileBehavior is AuthoritativeProjectileBehavior.Resurrection
                or AuthoritativeProjectileBehavior.HealOther
            || CanTargetLivingPlayer(spell)) {
            return intent;
        }
        return intent with { TargetEntityId = 0 };
    }

    private static bool CanTargetLivingPlayer(AuthoritativeSpellDefinition spell)
    {
        return HasDamage(spell)
            && spell.ProjectileSpeed > 0
            && spell.ProjectileBehavior is (AuthoritativeProjectileBehavior.Targeted
                or AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.BoneSpirit);
    }

    private static bool HasDamage(AuthoritativeSpellDefinition spell)
    {
        return spell.DamageAmount > 0
            || spell.RandomMagicDamageDivisor > 0
            || spell.RandomDamageRange > 0
            || spell.CharacterLevelRandomDamageDivisor > 0
            || spell.MagicDamageDivisor > 0
            || spell.SpellLevelDamage > 0
            || spell.ProjectileBehavior is AuthoritativeProjectileBehavior.BoneSpirit
                or AuthoritativeProjectileBehavior.FireWallController
                or AuthoritativeProjectileBehavior.LightningWallController
                or AuthoritativeProjectileBehavior.RingOfFireController
                or AuthoritativeProjectileBehavior.ImmolationController
                or AuthoritativeProjectileBehavior.Jester
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.InfernoController
                or AuthoritativeProjectileBehavior.ChainLightningController
                or AuthoritativeProjectileBehavior.NovaController
                or AuthoritativeProjectileBehavior.ApocalypseController
                or AuthoritativeProjectileBehavior.GuardianController;
    }

    private void ApplyImmediateSelfDamage(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloGameplayEvent> events)
    {
        if (spell.SelfDamage == 0)
            return;

        var damageResult = playerDamage.Apply(
            player,
            DeterministicFixedPointQ6.FromWhole(spell.SelfDamage));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                player.EntityId,
                player.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                AmountQ6 = damageResult.IncomingDamageQ6,
            });
        }
        if (player.Life == 0) {
            StartPlayerDeath(
                player,
                player.EntityId,
                appliedTick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        }
    }

    private bool TryResolveCastSource(
        AuthoritativePlayerState player,
        DiabloCastIntent intent,
        out ResolvedCastSource source,
        out DiabloRuleFailure failure)
    {
        source = default;
        failure = DiabloRuleFailure.NotAllowed;

        switch (intent.Source) {
        case DiabloCastSourceKind.Unspecified:
            if (intent.SourceItem.HasValue) {
                failure = DiabloRuleFailure.Malformed;
                return false;
            }
            return TryInferCastSource(player, intent.SpellId, out source);
        case DiabloCastSourceKind.Ability:
            if (intent.SourceItem.HasValue) {
                failure = DiabloRuleFailure.Malformed;
                return false;
            }
            if (player.AbilitySpellId != intent.SpellId)
                return false;
            source = new ResolvedCastSource(DiabloCastSourceKind.Ability, null, null);
            return true;
        case DiabloCastSourceKind.MemorizedSpell:
            if (intent.SourceItem.HasValue) {
                failure = DiabloRuleFailure.Malformed;
                return false;
            }
            if (!HasMemorizedSpell(player, intent.SpellId))
                return false;
            source = new ResolvedCastSource(
                DiabloCastSourceKind.MemorizedSpell,
                null,
                null);
            return true;
        case DiabloCastSourceKind.ChargedItem:
            if (!intent.SourceItem.HasValue) {
                failure = DiabloRuleFailure.Malformed;
                return false;
            }
            return TryResolveChargedItem(
                player,
                intent.SpellId,
                intent.SourceItem.Value,
                out source,
                out failure);
        case DiabloCastSourceKind.ConsumableItem:
            if (!intent.SourceItem.HasValue) {
                failure = DiabloRuleFailure.Malformed;
                return false;
            }
            return TryResolveConsumableItem(
                player,
                intent.SpellId,
                intent.SourceItem.Value,
                out source);
        default:
            failure = DiabloRuleFailure.Malformed;
            return false;
        }
    }

    /** Mirrors native AddJester's 10-way deterministic missile dispatch. */
    private uint SelectJesterSpellId(AuthoritativeSpellDefinition jesterSpell)
    {
        if (jesterSpell.JesterSpellIds.Count == 10)
            return jesterSpell.JesterSpellIds[random.RandomIntLessThan(10)];
        return random.RandomIntLessThan(10) switch {
            0 or 1 => 1U, // Firebolt
            2 => 12U, // Fireball
            3 => 6U, // Fire Wall
            4 => 7U, // Guardian
            5 => 16U, // Chain Lightning
            6 => 39U, // Town Portal
            7 => 23U, // Teleport
            8 => 25U, // Apocalypse
            9 => 8U, // Stone Curse
            _ => throw new InvalidOperationException("Native Jester dispatch produced an invalid roll."),
        };
    }

    private bool TryInferCastSource(
        AuthoritativePlayerState player,
        uint spellId,
        out ResolvedCastSource source)
    {
        if (player.AbilitySpellId == spellId) {
            source = new ResolvedCastSource(DiabloCastSourceKind.Ability, null, null);
            return true;
        }
        if (HasMemorizedSpell(player, spellId)) {
            source = new ResolvedCastSource(
                DiabloCastSourceKind.MemorizedSpell,
                null,
                null);
            return true;
        }

        var chargedReference = new DiabloPlayerItemReference(
            DiabloPlayerItemLocation.Equipment,
            DiabloCastSourceRules.LeftHandEquipmentSlot);
        if (DiabloPlayerItemAccess.TryGet(player, chargedReference, out var chargedItem)
            && IsMatchingChargedItem(chargedItem.State, spellId)
            && chargedItem.State.Charges > 0) {
            source = new ResolvedCastSource(
                DiabloCastSourceKind.ChargedItem,
                chargedReference,
                chargedItem);
            return true;
        }

        for (var index = 0; index < player.Inventory.Count; index++) {
            var reference = new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                (uint)index);
            var item = player.Inventory[index];
            if (!IsMatchingConsumableItem(item.State, spellId))
                continue;
            source = new ResolvedCastSource(
                DiabloCastSourceKind.ConsumableItem,
                reference,
                item);
            return true;
        }
        foreach (var beltItem in player.Belt.OrderBy(item => item.Slot)) {
            if (!IsMatchingConsumableItem(beltItem.State, spellId))
                continue;
            var reference = new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Belt,
                beltItem.Slot);
            if (!DiabloPlayerItemAccess.TryGet(player, reference, out var item))
                continue;
            source = new ResolvedCastSource(
                DiabloCastSourceKind.ConsumableItem,
                reference,
                item);
            return true;
        }
        if (player.StashOpen) {
            foreach (var stashItem in player.Stash
                         .OrderBy(item => item.Page)
                         .ThenBy(item => item.Cell)) {
                if (!IsMatchingConsumableItem(stashItem.State, spellId))
                    continue;
                var reference = new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    stashItem.Cell,
                    stashItem.State.StableItemId,
                    stashItem.Page);
                source = new ResolvedCastSource(
                    DiabloCastSourceKind.ConsumableItem,
                    reference,
                    new OwnedStoreItem(
                        stashItem.StoreId,
                        stashItem.StoreSlot,
                        stashItem.ItemSeed,
                        stashItem.Price,
                        stashItem.PurchasedAtTick,
                        stashItem.State));
                return true;
            }
        }

        source = default;
        return false;
    }

    private bool TryResolveChargedItem(
        AuthoritativePlayerState player,
        uint spellId,
        DiabloPlayerItemReference reference,
        out ResolvedCastSource source,
        out DiabloRuleFailure failure)
    {
        source = default;
        failure = DiabloRuleFailure.NotAllowed;
        if (reference.Location != DiabloPlayerItemLocation.Equipment
            || reference.Slot != DiabloCastSourceRules.LeftHandEquipmentSlot
            || !DiabloPlayerItemAccess.TryGet(player, reference, out var item)
            || !IsMatchingChargedItem(item.State, spellId)) {
            return false;
        }
        if (item.State.Charges <= 0) {
            failure = DiabloRuleFailure.InsufficientResources;
            return false;
        }

        source = new ResolvedCastSource(
            DiabloCastSourceKind.ChargedItem,
            reference,
            item);
        return true;
    }

    private bool TryResolveConsumableItem(
        AuthoritativePlayerState player,
        uint spellId,
        DiabloPlayerItemReference reference,
        out ResolvedCastSource source)
    {
        source = default;
        if (reference.Location is not (DiabloPlayerItemLocation.Inventory
                or DiabloPlayerItemLocation.Belt
                or DiabloPlayerItemLocation.Stash)
            || (reference.Location == DiabloPlayerItemLocation.Stash && !player.StashOpen)
            || !DiabloPlayerItemAccess.TryGet(player, reference, out var item)
            || !IsMatchingConsumableItem(item.State, spellId)) {
            return false;
        }

        source = new ResolvedCastSource(
            DiabloCastSourceKind.ConsumableItem,
            reference,
            item);
        return true;
    }

    private static bool HasMemorizedSpell(AuthoritativePlayerState player, uint spellId)
    {
        return player.MemorizedSpells.Any(spell => spell.SpellId == spellId && spell.Level > 0);
    }

    private static int GetEffectiveSpellLevel(
        AuthoritativePlayerState player,
        uint spellId)
    {
        var memorizedLevel = player.MemorizedSpells
            .Where(spell => spell.SpellId == spellId)
            .Select(spell => checked((int)spell.Level))
            .FirstOrDefault();
        var equippedLevel = player.Equipment
            .Where(item => item.State.StatFlag
                && (item.State.Magical == 0 || item.State.Identified))
            .Sum(item => item.State.SpellLevelAdd);
        return Math.Max(0, checked(memorizedLevel + equippedLevel));
    }

    private static uint ResolveInfravisionDuration(
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        ulong duration = spell.StatusDuration;
        for (var level = 0; level < effectiveSpellLevel; level++) {
            duration += duration / (uint)spell.InfravisionDurationGrowthDivisor;
            if (duration >= uint.MaxValue)
                return uint.MaxValue;
        }
        return (uint)duration;
    }

    /** Native AddMana Q6 restoration, including Hellfire class multipliers. */
    private int ResolveManaRestorationQ6(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var initialRange = spell.ManaRandomRange > 0
            ? spell.ManaRandomRange
            : 10;
        var characterLevelRange = spell.ManaCharacterLevelRandomRange > 0
            ? spell.ManaCharacterLevelRandomRange
            : 4;
        var spellLevelRange = spell.ManaSpellLevelRandomRange > 0
            ? spell.ManaSpellLevelRandomRange
            : 6;
        var manaQ6 = checked(random.RandomIntBetween(1, initialRange) * 64);
        for (var level = 0; level < player.CharacterLevel; level++)
            manaQ6 = checked(
                manaQ6 + random.RandomIntBetween(1, characterLevelRange) * 64);
        for (var level = 0; level < effectiveSpellLevel; level++)
            manaQ6 = checked(
                manaQ6 + random.RandomIntBetween(1, spellLevelRange) * 64);

        var playerClass = GetPlayerClass(player);
        var classPercent = playerClass?.ManaSpellPercent ?? 100;
        return checked(manaQ6 * classPercent) / 100;
    }

    private static uint ResolveSearchDuration(
        AuthoritativeSpellDefinition spell,
        uint characterLevel,
        int effectiveSpellLevel)
    {
        var duration = (ulong)spell.StatusDuration
            + (ulong)characterLevel
                * (uint)spell.SearchCharacterLevelDurationMultiplier
            + (ulong)effectiveSpellLevel
                * (uint)spell.SearchSpellLevelDurationMultiplier;
        return (uint)Math.Min(duration, int.MaxValue);
    }

    private static uint AddSearchDuration(uint previousDuration, uint castDuration)
    {
        if (previousDuration == 0)
            return castDuration;

        // Native AddSearch retains the previous missile duration when the
        // signed-int addition would reach or exceed INT_MAX.
        return previousDuration < (uint)int.MaxValue - castDuration
            ? previousDuration + castDuration
            : previousDuration;
    }

    private int ResolveManaCost(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var levelAdjustment = checked(
            Math.Max(effectiveSpellLevel - 1, 0) * spell.ManaAdjustment)
            / spell.ManaAdjustmentDivisor;
        var wholeCost = Math.Max(
            0,
            checked(
                spell.ManaCost
                + checked((int)player.CharacterLevel)
                    * spell.CharacterLevelManaCost
                - levelAdjustment));
        var playerClass = GetPlayerClass(player);
        var reductionPercent = rules.GameVariant == DiabloGameVariant.Hellfire
            ? playerClass?.HellfireSpellManaReductionPercent ?? 0
            : playerClass?.SpellManaReductionPercent ?? 0;
        wholeCost -= wholeCost * reductionPercent / 100;
        wholeCost = Math.Max(wholeCost, spell.MinimumManaCost);
        return checked(wholeCost * spell.ManaCostScale);
    }

    private int ResolveHealingAmountQ6(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        bool useHealOtherClassMultiplier = false)
    {
        var healing = checked(
            spell.HealingAmount + random.GenerateRnd(spell.RandomHealingRange));
        for (uint level = 0; level < player.CharacterLevel; level++) {
            healing = checked(
                healing
                + random.GenerateRnd(spell.CharacterLevelRandomHealingRange)
                + spell.CharacterLevelHealing);
        }
        for (var level = 0; level < effectiveSpellLevel; level++) {
            healing = checked(
                healing
                + random.GenerateRnd(spell.SpellLevelRandomHealingRange)
                + spell.SpellLevelHealing);
        }
        healing = checked(healing * spell.HealingScale);
        var playerClass = GetPlayerClass(player);
        var classPercent = useHealOtherClassMultiplier
            ? playerClass?.HealOtherSpellPercent ?? 100
            : playerClass?.SelfHealingSpellPercent ?? 100;
        return checked(healing * classPercent) / 100;
    }

    private AuthoritativeProjectile CreateChargedBoltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var pathIndex = random.GenerateRnd(
            spell.ChargedBoltPathRandomRange > 0
                ? spell.ChargedBoltPathRandomRange
                : 15) + 1;
        var damage = random.GenerateRnd(
            player.Attributes.Magic.Current / spell.RandomMagicDamageDivisor) + 1;
        _ = random.RandomIntBetween(1, 8);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.ChargedBolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            PathIndex = pathIndex,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateFireboltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + player.Attributes.Magic.Current / spell.MagicDamageDivisor
            + effectiveSpellLevel * spell.SpellLevelDamage
            + spell.DamageBonus);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateHolyBoltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.ProjectileSpeedSpellLevelStep,
                spell.ProjectileSpeedMaximumBonus));
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            speed);
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + checked((int)player.CharacterLevel) / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.HolyBolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateBloodStarProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var damage = checked(
            player.Attributes.Magic.Current / spell.MagicDamageDivisor
            + effectiveSpellLevel * spell.SpellLevelDamage
            - player.Attributes.Magic.Current / spell.MagicDamageReductionDivisor);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            Math.Max(0, damage),
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.BloodStar,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateBoneSpiritProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.BoneSpirit,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateElementalProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            spell.ElementalCharacterLevelDamageMultiplier
                * (checked((int)player.CharacterLevel) + damage)
            + spell.DamageAmount);
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage /= spell.MagicDamageDivisor;

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Elemental,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
            ElementalOriginX = velocityTargetX,
            ElementalOriginY = velocityTargetY,
        };
    }

    private AuthoritativeProjectile CreateWallController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        (int X, int Y) spreadPosition,
        int wallDirection)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            spreadPosition.X,
            spreadPosition.Y,
            spreadPosition.X,
            spreadPosition.Y,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.WallControlLifetime)) {
            Behavior = spell.ProjectileBehavior == AuthoritativeProjectileBehavior.LightningWallController
                ? AuthoritativeProjectileBehavior.LightningWallController
                : AuthoritativeProjectileBehavior.FireWallController,
            StartX = player.PositionX,
            StartY = player.PositionY,
            OriginalDirection = wallDirection,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
            WallLeftX = spreadPosition.X,
            WallLeftY = spreadPosition.Y,
            WallRightX = spreadPosition.X,
            WallRightY = spreadPosition.Y,
            WallLeftDirection = RotateDirection(wallDirection, -2),
            WallRightDirection = RotateDirection(wallDirection, 2),
        };
    }

    private static AuthoritativeProjectile CreateRingOfFireController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.RingOfFireController,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateWallSegment(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int wallDirection,
        int wallHitDirection)
    {
        var durationUnits = checked(
            spell.WallSegmentBaseDuration
            + controller.EffectiveSpellLevel * spell.WallSegmentSpellLevelDurationStep
            + checked((int)owner.CharacterLevel)
                * spell.WallSegmentCharacterLevelDurationStep);
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            (damage
                + spell.DamageBonus
                + checked((int)owner.CharacterLevel)
                    * spell.WallCharacterLevelDamageMultiplier)
            * spell.DamageScale);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            positionX,
            positionY,
            positionX,
            positionY,
            damage,
            spell.DamageType,
            0,
            checked((uint)(durationUnits * spell.WallSegmentDurationScale))) {
            Behavior = AuthoritativeProjectileBehavior.FireWallSegment,
            OriginalDirection = wallDirection,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
            WallHitDirection = wallHitDirection,
        };
    }

    private AuthoritativeProjectile CreateFireballProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.ProjectileSpeedSpellLevelStep,
                spell.ProjectileSpeedMaximumBonus));
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            speed);
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            (damage
                + checked((int)player.CharacterLevel)
                    / spell.CharacterLevelDamageDivisor)
            * spell.DamageScale
            + spell.DamageBonus);
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Fireball,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    /** Resolves native Rune of Fire as an immediate BigExplosion-style splash. */
    private AuthoritativeProjectile CreateRuneFireExplosion(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            (damage
                + checked((int)player.CharacterLevel)
                    / spell.CharacterLevelDamageDivisor)
            * spell.DamageScale
            + spell.DamageBonus);

        return new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            targetX,
            targetY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            1) {
            Behavior = AuthoritativeProjectileBehavior.Fireball,
            OriginalDirection = GetDirection(
                player.PositionX,
                player.PositionY,
                targetX,
                targetY),
            Speed = 1,
            ImpactLifetime = 1,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private static bool IsRuneFireSpell(AuthoritativeSpellDefinition spell)
    {
        return spell.ConsumableBehavior == AuthoritativeConsumableBehavior.RuneFire;
    }

    private AuthoritativeProjectile CreateLightningController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY,
        AuthoritativeProjectileBehavior behavior = AuthoritativeProjectileBehavior.Lightning)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        _ = random.RandomIntBetween(1, 8);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = behavior,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateChainLightningController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.ChainLightningControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ChainLightningController,
            OriginalDirection = (int)player.FacingDirection,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateNovaController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(
            damage
            + checked((int)player.CharacterLevel) / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);
        damage /= spell.MagicDamageDivisor;
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage = checked(damage * spell.DamageScale);

        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.NovaControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.NovaController,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            OriginalDirection = (int)player.FacingDirection,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private static AuthoritativeProjectile CreateNovaBall(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY);
        var (velocityX, velocityY) = CalculateVelocity(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            spell.ProjectileSpeed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            controller.Damage,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.NovaBall,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateApocalypseController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < checked((int)player.CharacterLevel);
             iteration++) {
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        }
        damage = checked(damage + checked((int)player.CharacterLevel));
        var minimumX = Math.Max(
            player.PositionX - spell.ApocalypseSearchRadius,
            1);
        var minimumY = Math.Max(
            player.PositionY - spell.ApocalypseSearchRadius,
            1);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.ApocalypseControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ApocalypseController,
            StartX = player.PositionX,
            StartY = player.PositionY,
            EffectiveSpellLevel = effectiveSpellLevel,
            ApocalypseScanX = minimumX,
            ApocalypseScanY = minimumY,
        };
    }

    private static AuthoritativeProjectile CreateApocalypseBoom(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int positionX,
        int positionY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            positionX,
            positionY,
            positionX,
            positionY,
            controller.Damage,
            AuthoritativeDamageType.Physical,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ApocalypseBoom,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
    }

    private static AuthoritativeProjectile CreateMonsterApocalypseBoom(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState target)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            target.EntityId,
            controller.SpellId,
            controller.LevelId,
            target.PositionX,
            target.PositionY,
            target.PositionX,
            target.PositionY,
            controller.Damage,
            AuthoritativeDamageType.Physical,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ApocalypseBoom,
        };
    }

    private static AuthoritativeProjectile CreateRageController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var lifetime = checked(
            spell.RageControllerLifetime
            + checked((int)player.CharacterLevel * spell.RageCharacterLevelLifetimeScale));
        var selfDamage = checked(
            (int)player.CharacterLevel * spell.RageSelfDamageCharacterLevel);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            selfDamage,
            spell.DamageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.RageController,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateGuardianBoltProjectile(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.GuardianProjectileSpeedLevelStep,
                spell.GuardianProjectileSpeedMaximumBonus));
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + player.Attributes.Magic.Current / spell.MagicDamageDivisor
            + effectiveSpellLevel * spell.SpellLevelDamage
            + spell.DamageBonus);
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            speed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private static AuthoritativeProjectile CreateGuardianController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        (int X, int Y) spawnPosition)
    {
        var durationUnits = Math.Min(
            spell.GuardianMaximumDuration,
            effectiveSpellLevel
                + checked((int)player.CharacterLevel / spell.GuardianCharacterLevelDurationDivisor));
        var duration = Math.Max(
            spell.GuardianMinimumDuration,
            checked(durationUnits * spell.GuardianDurationTickScale));
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            spawnPosition.X,
            spawnPosition.Y,
            spawnPosition.X,
            spawnPosition.Y,
            0,
            spell.DamageType,
            0,
            checked((uint)duration)) {
            Behavior = AuthoritativeProjectileBehavior.GuardianController,
            OriginalDirection = (int)player.FacingDirection,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateFlameWaveController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.FlameWaveControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.FlameWaveController,
            OriginalDirection = (int)player.FacingDirection,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateFlameWaveSegment(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY)
    {
        var direction = controller.OriginalDirection;
        var displacement = DirectionDisplacements[direction];
        var targetX = checked(positionX + displacement.X);
        var targetY = checked(positionY + displacement.Y);
        var (velocityX, velocityY) = CalculateVelocity(
            positionX,
            positionY,
            targetX,
            targetY,
            spell.ProjectileSpeed);
        var damage = checked(
            random.GenerateRnd(spell.RandomDamageRange)
            + checked((int)owner.CharacterLevel)
                * spell.FlameWaveCharacterLevelDamageBonus
            + spell.DamageBonus);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            positionX,
            positionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.SecondaryProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.FlameWaveSegment,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
    }

    private AuthoritativeProjectile CreateInfernoController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(player.PositionX, player.PositionY, targetX, targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            spell.ProjectileSpeed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            0,
            spell.DamageType,
            0,
            (uint)spell.ProjectileLifetime) {
            Behavior = AuthoritativeProjectileBehavior.InfernoController,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateInfernoSegment(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner)
    {
        var characterLevelDamage = random.GenerateRnd(Math.Max(1, (int)owner.CharacterLevel));
        var randomDamage = random.GenerateRnd(spell.RandomDamageRange);
        var damage = checked(
            (characterLevelDamage + randomDamage) * spell.DamageScale
            + spell.DamageBonus);
        var lifetime = checked(
            spell.SecondaryProjectileLifetime
            + controller.PathIndex * spell.InfernoSegmentLifetimeStep);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            controller.PositionX,
            controller.PositionY,
            damage,
            controller.DamageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.InfernoSegment,
            ResistantDamageDivisor = controller.ResistantDamageDivisor,
        };
    }

    private AuthoritativeProjectile CreateFlashLayer(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        AuthoritativeProjectileBehavior behavior)
    {
        var damage = GenerateFlashDamage(player, spell, effectiveSpellLevel);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            spell.DamageType,
            spell.AreaRadius,
            (uint)spell.ProjectileLifetime) {
            Behavior = behavior,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    /** Native CounselorAi emits fixed-damage Flash layers from the monster. */
    private void CreateMonsterFlashLayers(AuthoritativeCombatTarget monster)
    {
        var flashSpellId = monsterFlashSpellId;
        var hasFlashSpell = spells.TryGet(flashSpellId, out var flashSpell);
        var damageType = hasFlashSpell
            ? flashSpell.DamageType
            : AuthoritativeDamageType.Magic;
        var areaRadius = hasFlashSpell ? flashSpell.AreaRadius : 1;
        var lifetime = hasFlashSpell && flashSpell.ProjectileLifetime > 0
            ? flashSpell.ProjectileLifetime
            : 19;
        var monsterDamageLevelScale = hasFlashSpell
            ? flashSpell.FlashMonsterDamageLevelScale
            : 2;
        var damage = checked(GetEffectiveMonsterLevel(monster) * monsterDamageLevelScale);

        foreach (var behavior in new[] {
                     AuthoritativeProjectileBehavior.FlashBottom,
                     AuthoritativeProjectileBehavior.FlashTop,
                 }) {
            var projectileEntityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                projectileEntityId,
                new AuthoritativeProjectile(
                    projectileEntityId,
                    monster.EntityId,
                    0,
                    flashSpellId,
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    monster.PositionX,
                    monster.PositionY,
                    damage,
                    damageType,
                    areaRadius,
                    checked((uint)lifetime),
                    allowZeroSpellId: true) {
                    Behavior = behavior,
                    ResistantDamageDivisor = 1,
                });
        }
    }

    private int GenerateFlashDamage(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var characterLevel = checked((int)player.CharacterLevel);
        var iterationCount = checked(
            characterLevel / spell.RandomDamageIterationsCharacterLevelDivisor
            + spell.RandomDamageIterationsBonus);
        var damage = 0;
        for (var iteration = 0; iteration < iterationCount; iteration++)
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        damage = checked(
            damage
            + characterLevel / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);
        for (var spellLevel = 0; spellLevel < effectiveSpellLevel; spellLevel++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage = checked(damage + damage * spell.PostScaleDamagePercent / 100);
        return checked(damage * spell.DamageScale);
    }

    private bool IsMatchingChargedItem(AuthoritativeItemState state, uint spellId)
    {
        return DiabloCastSourceRules.IsMatchingChargedItem(state, spellId, items);
    }

    private bool IsMatchingConsumableItem(AuthoritativeItemState state, uint spellId)
    {
        return DiabloCastSourceRules.IsMatchingConsumableItem(state, spellId, spells, items);
    }

    private static void ConsumeCastSource(
        AuthoritativePlayerState player,
        ResolvedCastSource source,
        int manaCost)
    {
        switch (source.Kind) {
        case DiabloCastSourceKind.Ability:
            return;
        case DiabloCastSourceKind.MemorizedSpell:
            player.Mana -= manaCost;
            return;
        case DiabloCastSourceKind.ChargedItem:
            DiabloPlayerItemAccess.Replace(
                player,
                source.ItemReference!.Value,
                source.Item! with {
                    State = source.Item.State with { Charges = source.Item.State.Charges - 1 },
                });
            return;
        case DiabloCastSourceKind.ConsumableItem:
            DiabloPlayerItemAccess.Remove(player, source.ItemReference!.Value);
            return;
        default:
            throw new InvalidOperationException("A resolved cast source must be consumable.");
        }
    }

    private readonly record struct ResolvedCastSource(
        DiabloCastSourceKind Kind,
        DiabloPlayerItemReference? ItemReference,
        OwnedStoreItem? Item);

    private AuthoritativeProjectile CreateMovementController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        (int X, int Y) destination,
        AuthoritativeProjectileBehavior behavior)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            destination.X,
            destination.Y,
            destination.X,
            destination.Y,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = behavior,
        };
    }

    private static AuthoritativeProjectile CreateResurrectionBeam(
        uint projectileEntityId,
        AuthoritativePlayerState caster,
        AuthoritativePlayerState target,
        AuthoritativeSpellDefinition spell,
        int positionX,
        int positionY)
    {
        return new AuthoritativeProjectile(
            projectileEntityId,
            caster.EntityId == 0 ? 1U : caster.EntityId,
            target.EntityId,
            spell.SpellId,
            caster.LevelId,
            positionX,
            positionY,
            positionX,
            positionY,
            0,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ResurrectionBeam,
        };
    }

    private static AuthoritativeProjectile CreateStoneCurseController(
        uint projectileEntityId,
        AuthoritativePlayerState caster,
        (AuthoritativeCombatTarget Target, int X, int Y) target,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var durationUnits = Math.Min(
            (long)effectiveSpellLevel + spell.StoneCurseBaseDuration,
            spell.StoneCurseMaximumDuration);
        var duration = checked((uint)(durationUnits * spell.StoneCurseDurationScale));
        return new AuthoritativeProjectile(
            projectileEntityId,
            caster.EntityId == 0 ? 1U : caster.EntityId,
            target.Target.EntityId,
            spell.SpellId,
            caster.LevelId,
            target.X,
            target.Y,
            target.X,
            target.Y,
            0,
            AuthoritativeDamageType.Magic,
            0,
            duration) {
            Behavior = AuthoritativeProjectileBehavior.StoneCurse,
            ImpactLifetime = spell.ProjectileImpactLifetime,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private void ApplyResurrection(
        AuthoritativePlayerState target,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var destination = FindResurrectionPosition(target, spell, players);
        target.PositionX = destination.X;
        target.PositionY = destination.Y;
        target.Life = Math.Min(
            target.LifeMaximum,
            checked(spell.ResurrectionLife * spell.ResurrectionLifeScale));
        target.Mana = 0;
        target.ManaShieldActive = false;
        DiabloPlayerActionLifecycle.Cancel(target);
        target.CombatMode = DiabloPlayerCombatMode.Stand;
        target.CombatModeEndTick = 0;
    }

    private (int X, int Y) FindResurrectionPosition(
        AuthoritativePlayerState target,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var initialOffsets = spell.ResurrectionInitialOffsets.Count > 0
            ? spell.ResurrectionInitialOffsets
            : ResurrectionInitialOffsets;
        foreach (var offset in initialOffsets) {
            var candidateX = checked(target.PositionX + offset.X);
            var candidateY = checked(target.PositionY + offset.Y);
            if (IsResurrectionPositionAvailable(target, candidateX, candidateY, players))
                return (candidateX, candidateY);
        }

        return DeterministicGridCrawl.FindClosestValidPosition(
                target.PositionX,
                target.PositionY,
                1,
                spell.ResurrectionSearchRadius,
                (candidateX, candidateY) =>
                    IsResurrectionPositionAvailable(target, candidateX, candidateY, players)
                    && !IsPortalPosition(target.LevelId, candidateX, candidateY))
            ?? (0, 0);
    }

    private bool IsResurrectionPositionAvailable(
        AuthoritativePlayerState target,
        int positionX,
        int positionY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return world.IsWalkable(target.LevelId, positionX, positionY)
            && !world.IsOccupied(
                target.LevelId,
                positionX,
                positionY,
                players.Where(player => player.Life > 0),
                target.EntityId);
    }

    private bool IsPortalPosition(uint levelId, int positionX, int positionY)
    {
        return world.Portals.Values.Any(portal =>
            IsPortalEndpoint(
                portal.SourceLevelId,
                portal.SourcePositionX,
                portal.SourcePositionY,
                levelId,
                positionX,
                positionY)
            || IsPortalEndpoint(
                portal.DestinationLevelId,
                portal.DestinationPositionX,
                portal.DestinationPositionY,
                levelId,
                positionX,
                positionY));
    }

    private static bool IsPortalEndpoint(
        uint endpointLevelId,
        int endpointX,
        int endpointY,
        uint levelId,
        int positionX,
        int positionY)
    {
        return endpointLevelId == levelId
            && ((endpointX == positionX && endpointY == positionY)
                || (endpointX + 1 == positionX && endpointY + 1 == positionY));
    }

    private (int X, int Y)? FindClosestTeleportDestination(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players,
        int searchRadius)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            searchRadius,
            (candidateX, candidateY) =>
                world.IsWalkable(player.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    players.Where(candidate => candidate.Life > 0),
                    player.EntityId));
    }

    private (int X, int Y)? FindWallSpreadPosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            5,
            (candidateX, candidateY) =>
                (candidateX != player.PositionX || candidateY != player.PositionY)
                && !world.IsMissileGeometryBlocked(
                    player.LevelId,
                    candidateX,
                    candidateY)
                && world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    candidateX,
                    candidateY));
    }

    private (int X, int Y)? FindRuneFirePosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            9,
            (candidateX, candidateY) =>
                world.ContainsPosition(player.LevelId, candidateX, candidateY)
                && !world.IsMissileGeometryBlocked(
                    player.LevelId,
                    candidateX,
                    candidateY)
                && !world.TryGetMissileBlockingObject(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    out _)
                && !world.Projectiles.Values.Any(projectile =>
                    projectile.LevelId == player.LevelId
                    && projectile.PositionX == candidateX
                    && projectile.PositionY == candidateY));
    }

    private (int X, int Y)? FindGuardianSpawnPosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            spell.GuardianSpawnSearchRadius,
            (candidateX, candidateY) =>
                world.ContainsPosition(player.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    player.EntityId)
                && !world.IsMissileBlocked(player.LevelId, candidateX, candidateY)
                && world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    candidateX,
                    candidateY));
    }

    private (int X, int Y)? FindGolemSpawnPosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            5,
            (candidateX, candidateY) =>
                world.IsWalkable(player.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    player.EntityId)
                && !world.IsMissileBlocked(player.LevelId, candidateX, candidateY)
                && world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    candidateX,
                    candidateY));
    }

    private void SpawnGolem(
        AuthoritativePlayerState player,
        (int X, int Y) position,
        int effectiveSpellLevel,
        AuthoritativeSpellDefinition spell,
        int deathAnimationTicks)
    {
        foreach (var existing in world.CombatTargets.Values
                     .Where(target => target.IsPlayerMinion
                         && target.OwnerPlayerId == player.EntityId)
                     .ToArray()) {
            world.CombatTargets.Remove(existing.EntityId);
        }

        var entityId = AllocateProjectileEntityId();
        var hitPointsQ6 = checked(
            spell.GolemHitPointsLevelMultiplier * (spell.GolemHitPointsLevelBaseQ6 * effectiveSpellLevel
                + DeterministicFixedPointQ6.FromWhole(player.ManaMaximum)
                    / spell.GolemHitPointsManaDivisor));
        var hitPoints = DeterministicFixedPointQ6.ToWholeCeiling(hitPointsQ6);
        var minimumDamage = checked(
            spell.GolemDamageLevelMultiplier
                * (effectiveSpellLevel + spell.GolemMinimumDamageLevelOffset));
        var maximumDamage = checked(
            spell.GolemDamageLevelMultiplier
                * (effectiveSpellLevel + spell.GolemMaximumDamageLevelOffset));
        var golemType = spell.GolemMonsterId != 0
            ? monsterTypes?.TryGet(spell.GolemMonsterId, out var configuredGolemType) == true
                ? configuredGolemType
                : null
            : monsterTypes?.TryGetNativeAi("Golem", out var legacyGolemType) == true
                ? legacyGolemType
                : null;
        var spawnRandom = new DeterministicLegacyRandom(random.State);
        // InitializeSpawnedMonster seeds the native Golem after its standing
        // animation and native health draws, even though InitGolem replaces
        // the health and damage values with spell-owned formulas. Preserve
        // those draws so the server's next AI tick starts from the same stream.
        spawnRandom.GenerateRnd(Math.Max(
            0,
            (golemType?.StandAnimationTicksPerFrame ?? 1) - 1));
        spawnRandom.GenerateRnd(Math.Max(
            0,
            (golemType?.StandAnimationFrames ?? 1) - 1));
        spawnRandom.RandomIntBetween(
            golemType?.MinimumHitPoints ?? 1,
            golemType?.MaximumHitPoints ?? 1);
        var monsterItemSeed = unchecked((uint)spawnRandom.AdvanceRndSeed());
        var monsterAiSeed = unchecked((uint)spawnRandom.AdvanceRndSeed());
        random.Restore(spawnRandom.State);
        var golem = new AuthoritativeCombatTarget(
            entityId,
            position.X,
            position.Y,
            hitPoints,
            armorClass: spell.GolemArmorClass,
            maxHitPoints: hitPoints,
            levelId: player.LevelId,
            monsterId: golemType?.MonsterId ?? 0,
            attackDamage: maximumDamage,
            monsterLevel: checked((int)player.CharacterLevel),
            playerMinion: true,
            ownerPlayerId: player.EntityId,
            nativeAi: golemType?.NativeAi ?? "Golem",
            minDamage: minimumDamage,
            maxDamage: maximumDamage,
            minDamageSpecial: minimumDamage,
            maxDamageSpecial: maximumDamage,
            deathAnimationTicks: deathAnimationTicks,
            monsterItemSeed: monsterItemSeed,
            monsterAiSeed: monsterAiSeed);
        golem.RestoreHitPointsQ6(hitPointsQ6, hitPointsQ6);
        world.CombatTargets.Add(entityId, golem);
    }

    /** Source-compatible combat facade over engine-owned direction rotation. */
    private static int RotateDirection(int direction, int offset)
        => DeterministicGridDirections.Rotate(direction, offset);

    private (int X, int Y)? FindPhasingDestination(
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var candidates = new List<(int X, int Y)>();
        for (var offsetY = -spell.PhasingMaximumAxisOffset;
             offsetY <= spell.PhasingMaximumAxisOffset;
             offsetY++) {
            for (var offsetX = -spell.PhasingMaximumAxisOffset;
                 offsetX <= spell.PhasingMaximumAxisOffset;
                 offsetX++) {
                if (Math.Abs(offsetX) < spell.PhasingMinimumAxisOffset
                    || Math.Abs(offsetY) < spell.PhasingMinimumAxisOffset) {
                    continue;
                }
                var candidateX = checked(player.PositionX + offsetX);
                var candidateY = checked(player.PositionY + offsetY);
                if (world.IsWalkable(player.LevelId, candidateX, candidateY)
                    && !world.IsOccupied(
                        player.LevelId,
                        candidateX,
                        candidateY,
                        players.Where(candidate => candidate.Life > 0),
                        player.EntityId)) {
                    candidates.Add((candidateX, candidateY));
                }
            }
        }
        return candidates.Count == 0
            ? null
            : candidates[random.RandomIntLessThan(candidates.Count)];
    }

    private (AuthoritativeCombatTarget Target, int X, int Y)? FindStoneCurseTarget(
        uint levelId,
        int targetX,
        int targetY,
        int searchRadius)
    {
        var position = DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            searchRadius,
            (candidateX, candidateY) =>
                FindStoneCurseTargetAt(levelId, candidateX, candidateY) is not null);
        if (!position.HasValue)
            return null;
        var target = FindStoneCurseTargetAt(
            levelId,
            position.Value.X,
            position.Value.Y);
        return target is null
            ? null
            : (target, position.Value.X, position.Value.Y);
    }

    private AuthoritativeCombatTarget? FindBerserkTarget(
        uint levelId,
        uint targetEntityId,
        int targetX,
        int targetY,
        int searchRadius,
        int resistanceFrequency,
        bool respectMagicResistance)
    {
        if (targetEntityId != 0) {
            if (!world.CombatTargets.TryGetValue(targetEntityId, out var explicitTarget)
                || !IsBerserkTargetEligible(explicitTarget, levelId)
                || (respectMagicResistance
                    && !PassesBerserkResistance(explicitTarget, resistanceFrequency))) {
                return null;
            }
            return explicitTarget;
        }

        AuthoritativeCombatTarget? selectedTarget = null;
        var position = DeterministicGridCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            searchRadius,
            (candidateX, candidateY) => {
                selectedTarget = world.CombatTargets.Values
                .Where(candidate => candidate.PositionX == candidateX
                    && candidate.PositionY == candidateY
                    && IsBerserkTargetEligible(candidate, levelId))
                .OrderBy(candidate => candidate.EntityId)
                .FirstOrDefault(candidate => !respectMagicResistance
                    || PassesBerserkResistance(candidate, resistanceFrequency));
                return selectedTarget is not null;
            });
        return position.HasValue ? selectedTarget : null;
    }

    private static bool IsBerserkTargetEligible(
        AuthoritativeCombatTarget target,
        uint levelId)
    {
        return target.HitPointsQ6 > 0
            && !target.SneakHidden
            && (target.LevelId == 0 || target.LevelId == levelId)
            && !target.IsPlayerMinion
            && !target.IsBerserked
            && !target.IsUnique
            && !target.IsDiablo
            && !target.BerserkImmune
            && !target.IsPetrified;
    }

    private bool PassesBerserkResistance(
        AuthoritativeCombatTarget target,
        int resistanceFrequency)
    {
        if (target.MagicResistance >= 100)
            return false;
        return target.MagicResistance <= 0
            || random.FlipCoin((uint)(resistanceFrequency > 0 ? resistanceFrequency : 2));
    }

    private void ApplyBerserk(
        AuthoritativeCombatTarget target,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        target.IsBerserked = true;
        target.MinDamage = ScaleBerserkDamage(
            target.MinDamage,
            spell,
            effectiveSpellLevel);
        target.MaxDamage = ScaleBerserkDamage(
            target.MaxDamage,
            spell,
            effectiveSpellLevel);
        target.MinDamageSpecial = ScaleBerserkDamage(
            target.MinDamageSpecial,
            spell,
            effectiveSpellLevel);
        target.MaxDamageSpecial = ScaleBerserkDamage(
            target.MaxDamageSpecial,
            spell,
            effectiveSpellLevel);
        target.AttackDamage = target.MaxDamage;
    }

    private int ScaleBerserkDamage(
        int damage,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel)
    {
        var percent = checked(
            spell.BerserkDamagePercentBase
            + random.RandomIntLessThan(spell.BerserkDamagePercentRandomRange));
        return checked(
            percent * damage / 100
            + spell.BerserkSpellLevelBonus * effectiveSpellLevel);
    }

    private AuthoritativeCombatTarget? FindStoneCurseTargetAt(
        uint levelId,
        int positionX,
        int positionY)
    {
        return world.CombatTargets.Values
            .Where(candidate => candidate.HitPointsQ6 > 0
                && !candidate.SneakHidden
                && !candidate.StoneCurseImmune
                && (candidate.LevelId == 0 || candidate.LevelId == levelId)
                && ((candidate.PositionX == positionX
                        && candidate.PositionY == positionY)
                    || (candidate.IsMoving
                        && candidate.FuturePositionX == positionX
                        && candidate.FuturePositionY == positionY)))
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
    }

    private void AdvanceMonsters(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        foreach (var target in world.CombatTargets.Values
                     .OrderBy(target => target.EntityId)
                     .ToArray()) {
            AuthoritativeMonsterGroupUnity.ApplyFollowTheLeader(
                target,
                world.CombatTargets);
            target.ClearFallenRallyAttack();
            if (target.IsMoving && !target.IsPetrified)
                target.CompleteMovement();
            AuthoritativeMonsterGroupUnity.Apply(
                target,
                world.CombatTargets,
                world.HasAiLineOfSight);
            AdvanceNativeMonsterAiSeed(target);
            if (target.IsPlayerMinion) {
                AdvancePlayerMinion(target, tick, players, events);
                continue;
            }
            if (target.HitPoints <= 0) {
                // Native Stone Curse keeps a dead target in its petrified
                // shatter state instead of converting it into a corpse tile.
                if (target.IsPetrified)
                    continue;
                if (target.DeathAnimationTicks != 0
                    && target.AdvanceDeathAnimation()) {
                    if (target.IsPlayerMinion)
                        world.CombatTargets.Remove(target.EntityId);
                    else
                        ReplaceDeadMonsterWithCorpse(target);
                } else if (!target.IsPlayerMinion
                    && target.DeathAnimationTicks == 0) {
                    ReplaceDeadMonsterWithCorpse(target);
                }
                continue;
            }
            ApplyNativeMonsterRegeneration(target, rules.MonsterRegenerationLevelDivisor);
            if (target.IsPetrified)
                continue;
            if (world.World is not null
                && players.Count > 0
                && !IsMonsterVisibleToAnyPlayer(target, players)) {
                if (target.ActiveForTicks > 0 && !target.IsDiablo)
                    target.RestoreActiveForTicks(target.ActiveForTicks - 1);
                if (target.ActiveForTicks == 0)
                    continue;
            } else if (world.World is not null && players.Count > 0) {
                // Native ProcessMonsters wakes visible monsters for the full
                // active window before dispatching their AI.
                target.RestoreActiveForTicks(byte.MaxValue);
            }
            if (IsQuestDialogueTarget(target))
                continue;
            if (IsLazarusMinionCombatSuppressed(target))
                continue;
            // Native Lachdanan only exposes quest dialogue, while FireMan has
            // no registered native AI; neither may fall through to generic
            // combat or movement when a player is in aggro range.
            if (UsesNativeAi(target, "Lachdanan")
                || UsesNativeAi(target, "FireMan"))
                continue;
            if (TryAdvanceScavengerAi(target, tick, players))
                continue;
            if (TryStartFallenRally(target, tick))
                continue;
            if (TryAdvanceFallenFear(target, players))
                continue;
            if (TryAdvanceBatRetreat(target, players, tick))
                continue;
            var fallenRallyActive = target.FallenRallyTicks > 0;
            if (target.AggroRange <= 0 && !fallenRallyActive)
                continue;

            var victim = players
                .Where(player => player.Life > 0
                    && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                        or DiabloPlayerCombatMode.Quit)
                    && player.LevelId == target.LevelId
                    && (fallenRallyActive
                        || DeterministicGridMetrics.ChebyshevDistance(
                            player.PositionX,
                            player.PositionY,
                            target.PositionX,
                            target.PositionY) <= target.AggroRange))
                .OrderBy(player => DeterministicGridMetrics.ChebyshevDistance(
                    player.PositionX,
                    player.PositionY,
                    target.PositionX,
                    target.PositionY))
                .ThenBy(player => player.EntityId)
                .FirstOrDefault();
            if (victim is null) {
                if (fallenRallyActive)
                    target.AdvanceFallenRally(forceAttack: false);
                continue;
            }

            var victimDistance = DeterministicGridMetrics.ChebyshevDistance(
                victim.PositionX,
                victim.PositionY,
                target.PositionX,
                target.PositionY);
            var distance = ClampNativeDistance(victimDistance);
            // Acid owns one continuous controller from the travelling missile
            // through its splat and persistent puddle. Never admit a second
            // Acid controller while that chain is still live.
            if (target.NativeAi is "Acid" or "AcidUnique"
                && world.Projectiles.Values.Any(projectile =>
                    projectile.SourceEntityId == target.EntityId
                    && (projectile.Behavior == AuthoritativeProjectileBehavior.Acid
                        || projectile.Behavior == AuthoritativeProjectileBehavior.AcidSplat
                        || projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle)))
                continue;
            if (TryAdvanceFallenRally(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSneakAi(target, victim, players, distance))
                continue;
            if (TryAdvanceButcherAi(target, victim, players, distance))
                continue;
            if (TryAdvanceSkeletonAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceGoatMeleeAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceFatAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceHorkDemonAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSkeletonKingAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceZombieAi(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceGargoyleHealing(target, victim, tick, players, events, distance))
                continue;
            if (TryAdvanceGargoyleMovement(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceRhinoCharge(target, victim, tick, players, events, distance))
                continue;
            if (TryAdvanceSnakeAi(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceFamiliarBatAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceMappedChargeAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceMegaAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceCounselorAi(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSuccubusRangedAttack(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceMappedRangedAttack(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceSkeletonRangedRetreat(target, victim, players, tick, distance))
                continue;
            if (TryAdvanceSkeletonRangedAttack(target, victim, tick, players, distance))
                continue;
            if (TryAdvanceGoatRangedAttack(target, victim, tick, players, distance))
                continue;
            if (distance <= 1
                && (GetSkeletonRangedTuning(target).HasValue
                    || HasValidatedArrowMapping(target)))
                continue;
            if (distance <= 1) {
                if (IsPlayerProtectedByFlash(victim)
                    || IsPlayerEtherealized(victim))
                    continue;
                var armorClass = victim.ArmorClass;
                if (rules.GameVariant == DiabloGameVariant.Hellfire) {
                    armorClass = AuthoritativeEquipmentIncomingDamageRules
                        .ApplyHellfireMonsterClassArmor(
                            armorClass,
                            target.IsDemon,
                            target.IsUndead,
                            victim.Equipment,
                            rules.HellfireArmorBonusAgainstDemons,
                            rules.HellfireArmorBonusAgainstUndead);
                }
                var attackSeed = unchecked((uint)tick ^ target.EntityId ^ victim.EntityId);
                var skeletonMeleeAttackTuning = target.FallenRallyAttackPending
                    ? null
                    : GetSkeletonMeleeAttackTuning(target);
                // Initialized single-player targets retain the native
                // InitMonster AI seed as a marker that their global process
                // stream is authoritative. Zero-seed fixtures and legacy
                // multiplayer snapshots keep the compatibility controller.
                var usesNativeAiStream = target.MonsterAiSeed != 0;
                var attackRoll = skeletonMeleeAttackTuning.HasValue && usesNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (skeletonMeleeAttackTuning.HasValue
                    && attackRoll >= Math.Min(
                        100,
                        skeletonMeleeAttackTuning.Value.ChanceBase
                            + (skeletonMeleeAttackTuning.Value.IntelligenceScale
                                * target.Intelligence))) {
                    target.StartSkeletonMeleeDelay(
                        usesNativeAiStream
                            ? ResolveSkeletonMeleeAttackDelayFromRandom(target)
                            : ResolveSkeletonMeleeAttackDelay(target, attackSeed));
                    continue;
                }
                var goatMeleeAttackTuning = GetGoatMeleeAttackTuning(target);
                var usesGoatNativeAiStream = goatMeleeAttackTuning.HasValue
                    && target.MonsterAiSeed != 0;
                var goatAttackRoll = usesGoatNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (goatMeleeAttackTuning.HasValue
                    && goatAttackRoll >= Math.Min(
                        100,
                        goatMeleeAttackTuning.Value.ChanceBase
                            + (goatMeleeAttackTuning.Value.IntelligenceScale
                                * target.Intelligence)))
                    continue;
                var gargoyleAttackTuning = GetGargoyleAttackTuning(target);
                var usesGargoyleNativeAiStream = gargoyleAttackTuning.HasValue
                    && target.MonsterAiSeed != 0;
                var gargoyleAttackRoll = usesGargoyleNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (gargoyleAttackTuning.HasValue
                    && gargoyleAttackRoll >= Math.Min(
                        100,
                        gargoyleAttackTuning.Value.ChanceBase
                            + (gargoyleAttackTuning.Value.IntelligenceScale
                                * target.Intelligence)))
                    continue;
                var fatAttackTuning = GetFatAttackTuning(target);
                var usesFatNativeAiStream = fatAttackTuning.HasValue
                    && target.MonsterAiSeed != 0;
                var fatAttackRoll = usesFatNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (fatAttackTuning.HasValue
                    && !NativeFatAttackBandAllowsAttack(
                        target,
                        fatAttackRoll,
                        fatAttackTuning.Value))
                    continue;
                var batAttackTuning = GetBatAttackTuning(target);
                var usesBatNativeAiStream = batAttackTuning.HasValue
                    && target.MonsterAiSeed != 0;
                var batAttackRoll = usesBatNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (batAttackTuning.HasValue
                    && batAttackRoll >= Math.Min(
                            100,
                            batAttackTuning.Value.ChanceBase
                                + (batAttackTuning.Value.IntelligenceScale
                                    * target.Intelligence)))
                    continue;
                var usesSpecialAttack = UsesFatSpecialAttack(
                    target,
                    fatAttackRoll,
                    fatAttackTuning)
                    || (usesGoatNativeAiStream
                        ? UsesGoatSpecialAttackFromRandom(
                            target,
                            goatMeleeAttackTuning)
                        : UsesGoatSpecialAttack(
                            target,
                            attackSeed,
                            goatMeleeAttackTuning));
                var usesNativeCombatStream = gameMode == DiabloGameMode.SinglePlayer
                    || gameMode == DiabloGameMode.Multiplayer
                        && target.MonsterAiSeed != 0;
                var attackToHit = GetEffectiveMonsterToHit(target, usesSpecialAttack);
                if (usesNativeCombatStream && attackToHit > 0) {
                    var hitChance = rules.ResolveMonsterAttackHitChance(
                        attackToHit,
                        GetEffectiveMonsterLevel(target),
                        victim.CharacterLevel,
                        target.LevelId,
                        armorClass);
                    if (random.RandomIntLessThan(100) >= hitChance)
                        continue;

                    var victimClass = GetPlayerClass(victim);
                    var canBlock = (victim.CombatMode is DiabloPlayerCombatMode.Stand
                            or DiabloPlayerCombatMode.Attack)
                        && CanBlock(victim);
                    var blockRoll = canBlock
                        ? random.RandomIntLessThan(100)
                        : 100;
                    var blockChance = Math.Clamp(
                        victim.Attributes.Dexterity.Current
                            + (victimClass?.BaseBlockChance ?? 0)
                            + checked((int)victim.CharacterLevel
                                * rules.MonsterPlayerBlockLevelScale)
                            - (rules.MonsterPlayerBlockLevelScale
                                * GetEffectiveMonsterLevel(target)),
                        0,
                        100);
                    if (blockRoll < blockChance) {
                        StartBlock(victim, victimClass, tick);
                        // Native MonsterAttackPlayer still resolves Reflect on
                        // a blocked hit. The blocked player takes no damage,
                        // but the monster receives a percentage of the rolled
                        // Q6 damage and one charge is consumed.
                        if (victim.ReflectCharges > 0) {
                            victim.ReflectCharges--;
                            var reflectedBaseQ6 = AuthoritativeEquipmentIncomingDamageRules
                                .ApplyGetHitQ6(
                                    RollEffectiveMonsterDamageQ6(target, usesSpecialAttack),
                                    DeterministicFixedPointQ6.Scale,
                                    victim.Equipment);
                            var reflectMinimum = reflectSpell?.ReflectDamagePercentMinimum > 0
                                ? reflectSpell.ReflectDamagePercentMinimum
                                : NativeReflectDamagePercentMinimum;
                            var reflectMaximum = reflectSpell?.ReflectDamagePercentMaximum > 0
                                ? reflectSpell.ReflectDamagePercentMaximum
                                : NativeReflectDamagePercentMaximum;
                            var reflectedDamageQ6 = checked(
                                reflectedBaseQ6
                                * random.RandomIntBetween(reflectMinimum, reflectMaximum)
                                / 100);
                            if (reflectedDamageQ6 > 0) {
                                var reflectedEvents = new List<DiabloGameplayEvent>();
                                ApplyResolvedDamageQ6(
                                    victim,
                                    target,
                                    reflectedDamageQ6,
                                    reflectedEvents);
                                foreach (var reflectedEvent in reflectedEvents)
                                    events.Add(new DiabloDirectedGameplayEvent(
                                        victim,
                                        reflectedEvent));
                            }
                        }
                        continue;
                    }
                }

                var usesNativeFixedPointDamage = usesNativeCombatStream
                    && attackToHit > 0;
                int damageQ6;
                if (usesNativeFixedPointDamage) {
                    damageQ6 = AuthoritativeEquipmentIncomingDamageRules.ApplyGetHitQ6(
                        RollEffectiveMonsterDamageQ6(target, usesSpecialAttack),
                        DeterministicFixedPointQ6.Scale,
                        victim.Equipment);
                } else {
                    var attackDamage = usesSpecialAttack
                        ? usesNativeCombatStream
                            ? RollEffectiveMonsterDamage(target, special: true)
                            : SelectNativeSpecialDamage(target, attackSeed)
                        : usesNativeCombatStream
                            ? RollEffectiveMonsterDamage(target, special: false)
                            : difficulty == DiabloDifficulty.Normal
                                ? target.AttackDamage
                                : SelectEffectiveMonsterDamage(
                                    target,
                                    special: false,
                                    seed: attackSeed);
                    var resolvedDamage = attackToHit == 0
                        ? rules.ResolveAttackDamage(
                            attackDamage,
                            armorClass,
                            attackSeed)
                        : rules.ResolveMonsterAttackDamage(
                            attackDamage,
                            attackToHit,
                            GetEffectiveMonsterLevel(target),
                            victim.CharacterLevel,
                            target.LevelId,
                            armorClass,
                            attackSeed);
                    damageQ6 = DeterministicFixedPointQ6.FromWhole(
                        AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                            resolvedDamage,
                            rules.MinimumDamage,
                            victim.Equipment));
                }
                var damage = DeterministicFixedPointQ6.ToWholeTruncated(damageQ6);
                var reflectedDamage = 0;
                if (damage > 0 && victim.ReflectCharges > 0) {
                    victim.ReflectCharges--;
                    var reflectMinimum = reflectSpell?.ReflectDamagePercentMinimum > 0
                        ? reflectSpell.ReflectDamagePercentMinimum
                        : NativeReflectDamagePercentMinimum;
                    var reflectMaximum = reflectSpell?.ReflectDamagePercentMaximum > 0
                        ? reflectSpell.ReflectDamagePercentMaximum
                        : NativeReflectDamagePercentMaximum;
                    reflectedDamage = checked(
                        damage * random.RandomIntBetween(reflectMinimum, reflectMaximum) / 100);
                    if (reflectedDamage > 0) {
                        var reflectedEvents = new List<DiabloGameplayEvent>();
                        ApplyResolvedDamageQ6(
                            victim,
                            target,
                            DeterministicFixedPointQ6.FromWhole(reflectedDamage),
                            reflectedEvents);
                        foreach (var reflectedEvent in reflectedEvents)
                            events.Add(new DiabloDirectedGameplayEvent(victim, reflectedEvent));
                    }
                }
                var incomingDamageQ6 = Math.Max(
                    0,
                    damageQ6 - DeterministicFixedPointQ6.FromWhole(reflectedDamage));
                // Direct compatibility fixtures historically store whole life
                // points, while authoritative class-backed players store Q6.
                var usesLegacyWholeLife = victim.LifeMaximum % DeterministicFixedPointQ6.Scale != 0;
                var appliedDamageQ6 = usesLegacyWholeLife
                    ? DeterministicFixedPointQ6.ToWholeTruncated(incomingDamageQ6)
                    : incomingDamageQ6;
                var damageResult = playerDamage.Apply(victim, appliedDamageQ6);
                if (damageResult.IncomingDamageQ6 > 0) {
                    var eventDamageQ6 = usesLegacyWholeLife
                        ? incomingDamageQ6
                        : damageResult.IncomingDamageQ6;
                    events.Add(new DiabloDirectedGameplayEvent(
                        victim,
                        new DiabloGameplayEvent(
                            DiabloGameplayEventKind.Damage,
                            target.EntityId,
                            victim.EntityId,
                            DeterministicFixedPointQ6.ToWholeTruncated(
                                eventDamageQ6)) {
                            AmountQ6 = eventDamageQ6,
                        }));
                }
                // Native thorns retaliates after a successful monster hit,
                // before the player death check, using one content-owned Q6 roll.
                if (damage > 0
                    && target.HitPointsQ6 > 0
                    && HasUsableEquipmentFlag(
                        victim,
                        AuthoritativeEquipmentIncomingDamageRules.ThornsFlag)) {
                    var thornsEvents = new List<DiabloGameplayEvent>();
                    ApplyResolvedDamageQ6(
                        victim,
                        target,
                        DeterministicFixedPointQ6.FromWhole(random.RandomIntBetween(
                            rules.ThornsDamageMinimum,
                            rules.ThornsDamageMaximum)),
                        thornsEvents);
                    foreach (var thornsEvent in thornsEvents)
                        events.Add(new DiabloDirectedGameplayEvent(victim, thornsEvent));
                }
                if (victim.Life == 0)
                    StartPlayerDeath(
                        victim,
                        target.EntityId,
                        tick,
                        players,
                        PlayerDeathReason.MonsterOrTrap);
                else if (damage > 0 && target.HitPointsQ6 > 0)
                    ApplyMonsterKnockback(target, victim, players);
                var batTuning = GetBatAttackTuning(target);
                if (target.HitPointsQ6 > 0 && batTuning.HasValue)
                    target.StartBatRetreat(
                        GetDirection(
                            target.PositionX,
                            target.PositionY,
                            victim.PositionX,
                            victim.PositionY),
                        batTuning.Value.RetreatDurationTicks);
                if (target.HitPointsQ6 > 0
                    && TryGetMonsterMissile(target, out var postMeleeMissile)
                    && IsFamiliarLightningMissile(postMeleeMissile))
                    CreateFamiliarLightningProjectile(target, victim, postMeleeMissile);
                continue;
            }

            var deltaX = Math.Sign((long)victim.PositionX - target.PositionX);
            var deltaY = Math.Sign((long)victim.PositionY - target.PositionY);
            var candidates = new[] {
                (X: target.PositionX + deltaX, Y: target.PositionY + deltaY),
                (X: target.PositionX + deltaX, Y: target.PositionY),
                (X: target.PositionX, Y: target.PositionY + deltaY),
            };
            var moved = false;
            foreach (var candidate in candidates.Where(
                         candidate => candidate.X != target.PositionX
                             || candidate.Y != target.PositionY)) {
                if (!world.IsWalkable(target.LevelId, candidate.X, candidate.Y)
                    || world.IsOccupied(
                        target.LevelId,
                        candidate.X,
                        candidate.Y,
                        players,
                        target.EntityId)
                    || DeterministicGridMetrics.ChebyshevDistance(
                        victim.PositionX,
                        victim.PositionY,
                        candidate.X,
                        candidate.Y) >= distance) {
                    continue;
                }
                target.BeginMovement(candidate.X, candidate.Y);
                moved = true;
                break;
            }
            if (!moved) {
                var maximumDepth = Math.Clamp(
                    checked(target.AggroRange * 2 + 2),
                    8,
                    256);
                var fallback = DeterministicGridNavigation.FindStep(
                    target.PositionX,
                    target.PositionY,
                    victim.PositionX,
                    victim.PositionY,
                    maximumDepth,
                    (x, y) => world.IsWalkable(target.LevelId, x, y),
                    (x, y) => world.IsOccupied(
                        target.LevelId,
                        x,
                        y,
                        players,
                        target.EntityId));
                if (fallback.HasValue)
                    target.BeginMovement(fallback.Value.X, fallback.Value.Y);
            }
        }
        foreach (var leader in world.CombatTargets.Values
                     .Where(target => target.IsMoving && target.PackSize > 0)
                     .OrderBy(target => target.EntityId)) {
            if (!AuthoritativeMonsterGroupUnity.CanLeaderMove(
                    leader,
                    world.CombatTargets))
                leader.CancelMovement();
        }
        foreach (var member in world.CombatTargets.Values
                     .Where(target => target.IsMoving
                         && target.LeaderRelation
                             == AuthoritativeMonsterLeaderRelation.Leashed)
                     .OrderBy(target => target.EntityId)) {
            if (!AuthoritativeMonsterGroupUnity.CanLeashedMemberMove(
                    member,
                    world.CombatTargets))
                member.CancelMovement();
        }
    }

    /** Returns whether a live player can see the monster in authoritative geometry. */
    private bool IsMonsterVisibleToAnyPlayer(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var level = world.LevelGeometry
            .FirstOrDefault(candidate => candidate.LevelId == target.LevelId);
        return players.Any(player => player.Life > 0
            && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                or DiabloPlayerCombatMode.Quit)
            && player.LevelId == target.LevelId
            && (level is null
                || (level.NativeTransparency.Count == 0
                    && level.NativeVisionBlockedCells.Count == 0)
                || IsWithinNativeVision(level, target, player)));
    }

    /** Native ProcessMonsters normally receives a visibility flag from the
     * player's equipment-derived light-radius vision pass. */
    private static bool IsWithinNativeVision(
        AuthoritativeLevel level,
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState player)
    {
        var deltaX = (long)target.PositionX - player.PositionX;
        var deltaY = (long)target.PositionY - player.PositionY;
        var nativeVisionRadius = player.LightRadius;
        if (deltaX * deltaX + deltaY * deltaY
            > (long)nativeVisionRadius * nativeVisionRadius)
            return false;
        return level.HasNativeVisionLineOfSight(
            player.PositionX,
            player.PositionY,
            target.PositionX,
            target.PositionY);
    }

    /** Keeps legacy int distance consumers fail-closed for malformed coordinates. */
    private static int ClampNativeDistance(long distance)
        => distance >= int.MaxValue ? int.MaxValue : (int)distance;

    /** Native ProcessMonsters advances each monster's multiplayer AI seed once per tick. */
    private void AdvanceNativeMonsterAiSeed(AuthoritativeCombatTarget target)
    {
        if (gameMode != DiabloGameMode.Multiplayer || target.MonsterAiSeed == 0)
            return;

        random.Restore(target.MonsterAiSeed);
        target.MonsterAiSeed = unchecked((uint)random.AdvanceRndSeed());
    }

    private bool TryAdvanceFallenFear(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (target.FallenFearTicks == 0
            || !GetFallenFearTuning(target).HasValue)
            return false;

        var usesNativeRandomWalk = gameMode == DiabloGameMode.SinglePlayer
            || target.MonsterAiSeed != 0;
        (int X, int Y)? retreat;
        if (usesNativeRandomWalk) {
            // Native FallenAi uses RandomWalk while fear is active. The
            // process stream orders the two side-pair coin draws even when
            // the straight candidate is available; multiplayer restores the
            // per-monster stream before reaching this branch.
            retreat = FindNativeRandomWalkStepFromRandom(
                target,
                target.FallenFearDirection,
                players);
        } else {
            // Preserve the deterministic compatibility path for targets
            // without a native multiplayer stream.
            retreat = null;
            foreach (var candidateDirection in DeterministicGridDirections.EnumerateAlternating(
                         target.FallenFearDirection)) {
                var displacement = DirectionDisplacements[candidateDirection];
                var candidateX = checked(target.PositionX + displacement.X);
                var candidateY = checked(target.PositionY + displacement.Y);
                if (!world.IsWalkable(target.LevelId, candidateX, candidateY)
                    || world.IsOccupied(
                        target.LevelId,
                        candidateX,
                        candidateY,
                        players,
                        target.EntityId)) {
                    continue;
                }
                retreat = (candidateX, candidateY);
                break;
            }
        }
        if (retreat.HasValue)
            target.BeginMovement(retreat.Value.X, retreat.Value.Y);
        target.AdvanceFallenFear();
        return true;
    }

    /** Advances the native Fallen stand animation and admits its rally special. */
    private bool TryStartFallenRally(
        AuthoritativeCombatTarget target,
        ulong tick)
    {
        if (!UsesNativeAi(target, "Fallen")
            || target.HitPointsQ6 == 0
            || GetFallenStandAnimationTuning(target) is not { } tuning
            || !target.AdvanceFallenStandAnimation(
                tuning.Frames,
                tuning.TicksPerFrame))
            return false;

        var usesNativeRandom = gameMode == DiabloGameMode.SinglePlayer
            || target.MonsterAiSeed != 0;
        var rallies = usesNativeRandom
            ? random.FlipCoin(4)
            : (unchecked((uint)tick) ^ target.EntityId) % 4U == 0U;
        target.ResetFallenStandAnimation();
        if (!rallies)
            return true;

        target.ApplyHealingQ6(
            DeterministicFixedPointQ6.FromWhole(
                checked((2 * target.Intelligence) + 2)));
        TriggerFallenRally(target);
        return true;
    }

    /** Native Fallen attack-goal pursuit after a successful rally special. */
    private bool TryAdvanceFallenRally(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (target.FallenRallyTicks == 0)
            return false;
        if (distance <= 1) {
            target.AdvanceFallenRally(forceAttack: true);
            return false;
        }

        var usesNativeRandom = gameMode == DiabloGameMode.SinglePlayer
            || target.MonsterAiSeed != 0;
        var actionSeed = unchecked((uint)tick ^ target.EntityId ^ victim.EntityId);
        var pursuit = usesNativeRandom
            ? FindNativeRandomWalkStepFromRandom(target, victim, players)
            : FindNativeRandomWalkStep(target, victim, players, actionSeed);
        if (pursuit.HasValue)
            target.BeginMovement(pursuit.Value.X, pursuit.Value.Y);
        target.AdvanceFallenRally(forceAttack: false);
        return true;
    }

    private (int Frames, int TicksPerFrame)? GetFallenStandAnimationTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.StandAnimationFrames > 0
            && definition.StandAnimationTicksPerFrame > 0) {
            return (
                definition.StandAnimationFrames,
                definition.StandAnimationTicksPerFrame);
        }
        return UsesNativeAi(target, "Fallen")
            ? (11, 3)
            : null;
    }

    /** Native Bat retreat: move away once, then take one deterministic side step. */
    private bool TryAdvanceBatRetreat(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick)
    {
        var batTuning = GetBatAttackTuning(target);
        if (!batTuning.HasValue || target.BatRetreatTicks == 0)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.MonsterAiSeed != 0;
        if (usesNativeAiStream) {
            // Native BatAi consumes its action roll before either retreat
            // branch. RandomWalk then consumes two side-order coin draws.
            _ = random.RandomIntLessThan(100);
            var nativeDirection = target.BatRetreatTicks == batTuning.Value.RetreatDurationTicks
                ? RotateDirection(target.BatRetreatDirection, 4)
                : RotateDirection(
                    target.BatRetreatDirection,
                    random.FlipCoin(2) ? 1 : -1);
            var retreat = FindNativeRandomWalkStepFromRandom(
                target,
                nativeDirection,
                players);
            if (retreat.HasValue)
                target.BeginMovement(retreat.Value.X, retreat.Value.Y);
            target.AdvanceBatRetreat();
            return true;
        }

        var direction = target.BatRetreatTicks == batTuning.Value.RetreatDurationTicks
            ? RotateDirection(target.BatRetreatDirection, 4)
            : RotateDirection(
                target.BatRetreatDirection,
                ((unchecked((uint)tick) ^ target.EntityId) & 1U) == 0 ? 1 : -1);
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(target.PositionX + displacement.X);
        var candidateY = checked(target.PositionY + displacement.Y);
        if (world.IsWalkable(target.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                target.LevelId,
                candidateX,
                candidateY,
                players,
                target.EntityId)) {
            target.BeginMovement(candidateX, candidateY);
        }
        target.AdvanceBatRetreat();
        return true;
    }

    /** Native ButcherAi pursues through the server navigation boundary until adjacent. */
    private bool TryAdvanceButcherAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (!UsesButcherPursuit(monster) || distance <= 1)
            return false;

        var actionSeed = unchecked((uint)monster.EntityId ^ victim.EntityId);
        var randomWalk = monster.MonsterAiSeed != 0
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native ButcherAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    /** Active monster content owns Butcher controller assignment; direct legacy targets keep the label fallback. */
    private bool UsesButcherPursuit(AuthoritativeCombatTarget monster)
        => UsesNativeAi(monster, "Butcher");

    /** Zhar and Lazarus retain the native CounselorAi controller after quest dialogue. */
    private bool UsesNativeCounselorAi(AuthoritativeCombatTarget monster)
        => UsesNativeAi(monster, "Counselor")
            || UsesNativeAi(monster, "Zhar")
            || UsesNativeAi(monster, "Lazarus");

    /** Active monster content owns fallback native-AI identity; direct targets retain the label path. */
    private bool UsesNativeAi(AuthoritativeCombatTarget monster, string nativeAi)
    {
        // Unique encounters carry an explicit runtime identity that must not
        // be replaced by an incomplete generic base-type catalog row.
        if (monster.IsUnique && !string.IsNullOrWhiteSpace(monster.NativeAi))
            return monster.NativeAi == nativeAi;
        if (monsterTypes?.TryGet(monster.MonsterId, out var definition) == true)
            return monster.NativeAi == nativeAi
                || !definition.HasCompleteAiTuning
                    && definition.NativeAi == nativeAi;
        return monster.NativeAi == nativeAi;
    }

    /** Native SkeletonAi admits movement through its 35 - 4 * intelligence band. */
    private bool TryAdvanceSkeletonAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (monster.FallenRallyAttackPending)
            return false;
        if (monster.AdvanceSkeletonMeleeDelay())
            return true;

        var skeletonTuning = GetSkeletonMeleeDelayTuning(monster);
        if (!skeletonTuning.HasValue || distance <= 1)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        var delayBand = Math.Max(
            0,
            skeletonTuning.Value.ChanceBase
                - (skeletonTuning.Value.IntelligenceScale * monster.Intelligence));
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (actionRoll < delayBand) {
            monster.StartSkeletonMeleeDelay(
                usesNativeAiStream
                    ? ResolveSkeletonMeleeMovementDelayFromRandom(monster)
                    : ResolveSkeletonMeleeMovementDelay(monster, actionSeed));
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native SkeletonAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    /** Native AiAvoidance admits GoatMelee movement through its intelligence band. */
    private bool TryAdvanceGoatMeleeAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var goatTuning = GetGoatMeleeMovementTuning(monster);
        if (!goatTuning.HasValue || distance <= 1)
            return false;
        if (monster.AdvanceAvoidanceDelay())
            return true;

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        var advancedMoveGoal = usesNativeAiStream
            ? TryAdvanceAiAvoidanceMoveGoalFromRandom(
                monster,
                victim,
                players,
                distance,
                actionRoll)
            : TryAdvanceAiAvoidanceMoveGoal(
                monster,
                victim,
                players,
                distance,
                actionSeed);
        if (advancedMoveGoal)
            return true;

        var movementChance = GetAiAvoidanceMovementChance(
            monster,
            goatTuning.Value.ChanceBase,
            goatTuning.Value.IntelligenceScale,
            GetAiAvoidanceMovementFreshChanceBonus(monster));
        if (actionRoll >= movementChance)
        {
            monster.AdvanceAvoidanceMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            monster.RecordAvoidanceMovement(
                randomWalk.Value.X - monster.PositionX,
                randomWalk.Value.Y - monster.PositionY);
        } else {
            monster.AdvanceAvoidanceMovementIdle();
        }
        return true;
    }

    /** Native RoundWalk candidate order with deterministic turn state. */
    private (int X, int Y)? FindNativeRoundWalkStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int? turnOverride = null)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var turn = turnOverride.HasValue
            ? turnOverride.Value == 0 ? 1 : -1
            : (seed / 100U) % 2U == 0U ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        var turn90 = primaryDirections[0];
        var fallbackDirection = RotateDirection(turn90, 4);
        var fallbackTurn = (seed / 200U) % 2U == 0U ? 1 : -1;
        var fallbackDirections = DeterministicGridDirections.EnumerateTurnCandidates(
            fallbackDirection,
            fallbackTurn,
            2);
        foreach (var candidateDirection in primaryDirections.Concat(fallbackDirections)) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native RoundWalk candidate order driven by the restored monster stream. */
    private (int X, int Y)? FindNativeRoundWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        // Leoric initializes goalVar2 with GenerateRnd(2) when entering its
        // Move goal; the value selects the clockwise/counter-clockwise turn.
        var turn = random.RandomIntLessThan(2) == 0 ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        foreach (var candidateDirection in primaryDirections) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }

        // RoundWalk falls back to RandomWalk in the opposite direction. Keep
        // its two native coin draws even when the fallback has no open tile.
        var fallbackDirection = RotateDirection(primaryDirections[0], 4);
        var fallbackTurn = random.FlipCoin(2) ? 1 : -1;
        foreach (var candidateDirection in DeterministicGridDirections.EnumerateTurnCandidates(
                     fallbackDirection,
                     fallbackTurn,
                     2)) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }
        return null;
    }

    /** Native OverlordAi admits Fat movement through its 20 + 4 * intelligence band. */
    private bool TryAdvanceFatAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var fatTuning = GetFatMovementTuning(monster);
        if (!fatTuning.HasValue || distance <= 1)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        var movementChance = GetFatMovementChance(
            monster,
            fatTuning.Value.ChanceBase,
            fatTuning.Value.IntelligenceScale,
            GetFatMovementFreshChanceBonus(monster));
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (movementRoll >= movementChance) {
            monster.AdvanceFatMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            monster.RecordFatMovement(
                randomWalk.Value.X - monster.PositionX,
                randomWalk.Value.Y - monster.PositionY);
            return true;
        }

        monster.AdvanceFatMovementIdle();

        // Native OverlordAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    /** Native HorkDemonAi admission with server-owned Hork Spawn trajectory. */
    private bool TryAdvanceHorkDemonAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var horkDemonTuning = GetHorkDemonTuning(monster);
        if (!horkDemonTuning.HasValue)
            return false;
        if (monster.AdvanceHorkDemonDelay())
            return true;
        if (!TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.HorkSpawn)
            return true;
        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == missile.ProjectileBehavior))
            return true;

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (distance <= 1) {
            var attackChance = Math.Min(
                100,
                horkDemonTuning.Value.AttackChanceBase
                    + (horkDemonTuning.Value.AttackChanceIntelligenceScale
                        * monster.Intelligence));
            return actionRoll >= attackChance;
        }

        if (distance >= horkDemonTuning.Value.SpawnMinimumDistance
            && actionRoll < Math.Min(
                    100,
                    horkDemonTuning.Value.SpawnChanceBase
                        + (horkDemonTuning.Value.SpawnChanceIntelligenceScale
                            * monster.Intelligence))
            && monsterSpawnPlanner is not null
            && world.CombatTargets.Count < 256) {
            // Native HorkDemonAi checks the monster's current direction for
            // the adjacent admission cell; it does not turn toward the
            // victim before starting Hork Spawn.
            var direction = monster.Direction;
            var displacement = DirectionDisplacements[direction];
            var positionX = checked(monster.PositionX + displacement.X);
            var positionY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, positionX, positionY)
                && !world.IsOccupied(
                    monster.LevelId,
                    positionX,
                    positionY,
                    players,
                    monster.EntityId)) {
                var projectileSpeed = missile.ProjectileSpeed > 0
                    ? missile.ProjectileSpeed
                    : 8;
                var projectileLifetime = missile.ProjectileLifetime > 0
                    ? missile.ProjectileLifetime
                    : 9;
                var velocity = CalculateVelocity(
                    monster.PositionX,
                    monster.PositionY,
                    victim.PositionX,
                    victim.PositionY,
                    projectileSpeed);
                var projectile = new AuthoritativeProjectile(
                    AllocateProjectileEntityId(),
                    monster.EntityId,
                    victim.EntityId,
                    missile.SpawnMonsterId,
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    victim.PositionX,
                    victim.PositionY,
                    0,
                    AuthoritativeDamageType.Physical,
                    0,
                    checked((uint)projectileLifetime)) {
                    Behavior = missile.ProjectileBehavior,
                    NativeMissileId = missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic ? missile.MissileId : null,
                    VelocityX = velocity.X,
                    VelocityY = velocity.Y,
                    OriginalDirection = direction,
                    Speed = projectileSpeed,
                    ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
                    ResistantDamageDivisor = 1,
                };
                world.Projectiles.Add(projectile.EntityId, projectile);
                return true;
            }
        }

        var movementChance = Math.Min(
            100,
            horkDemonTuning.Value.MovementChanceBase
                + (horkDemonTuning.Value.MovementChanceIntelligenceScale
                    * monster.Intelligence));
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (movementRoll >= movementChance) {
            monster.StartHorkDemonDelay(
                usesNativeAiStream
                    ? ResolveHorkDemonDelayFromRandom(missile)
                    : ResolveHorkDemonDelay(missile, actionSeed));
            return true;
        }

        var roundWalkChanceModulo = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var horkDefinition) == true
            && horkDefinition.HorkDemonRoundWalkChanceModulo > 0) {
            roundWalkChanceModulo = horkDefinition.HorkDemonRoundWalkChanceModulo;
        }
        var roundWalkAdmitted = distance >= 5
            && (usesNativeAiStream
                ? !random.FlipCoin((uint)roundWalkChanceModulo)
                : (actionSeed / 100U) % (uint)roundWalkChanceModulo != 0U);
        if (roundWalkAdmitted) {
            var roundWalk = usesNativeAiStream
                ? FindNativeRoundWalkStepFromRandom(monster, victim, players)
                : FindNativeRoundWalkStep(monster, victim, players, actionSeed);
            if (roundWalk.HasValue) {
                monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                return true;
            }
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native HorkDemonAi consumes the admitted movement branch even when
        // both its RoundWalk and RandomWalk candidates are blocked.
        return true;
    }

    /** Native MegaAi delegates to SkeletonAi beyond its Inferno range. */
    private bool TryAdvanceMegaAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!UsesNativeAi(monster, "Mega"))
            return false;
        if (monsterMissiles is not null) {
            if (!TryGetMonsterMissile(monster, out var missile)
                || missile.ProjectileBehavior
                    != AuthoritativeProjectileBehavior.InfernoController)
                return false;
            if (distance < ResolveMonsterInfernoTuning(missile).MaximumDistance)
                return false;
        } else if (distance < 5) {
            return false;
        }

        if (distance <= 1)
            return false;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        if (monster.AdvanceSkeletonMeleeDelay())
            return true;
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var delayChanceBase = 35;
        var delayChanceIntelligenceScale = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var megaDefinition) == true
            && megaDefinition.MegaDelayChanceBase > 0
            && megaDefinition.MegaDelayChanceIntelligenceScale > 0) {
            delayChanceBase = megaDefinition.MegaDelayChanceBase;
            delayChanceIntelligenceScale = megaDefinition.MegaDelayChanceIntelligenceScale;
        }
        var delayBand = Math.Max(
            0,
            delayChanceBase - (delayChanceIntelligenceScale * monster.Intelligence));
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (actionRoll < delayBand) {
            monster.StartSkeletonMeleeDelay(
                usesNativeAiStream
                    ? ResolveSkeletonMeleeMovementDelayFromRandom(monster)
                    : ResolveSkeletonMeleeMovementDelay(monster, actionSeed));
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, victim, players)
            : FindNativeRandomWalkStep(monster, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native MegaAi delegates to SkeletonAi here; a blocked RandomWalk
        // consumes the tick rather than invoking bounded generic navigation.
        return true;
    }

    /** Native Mega normal-state fallback after an Inferno admission miss. */
    private bool TryAdvanceMegaIntermediateMovement(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int distance,
        bool usesNativeAiStream = false)
    {
        if (!UsesNativeAi(monster, "Mega") || distance < 2)
            return false;

        var movementChanceBase = 50;
        var movementChanceIntelligenceScale = 10;
        if (monsterTypes?.TryGet(monster.MonsterId, out var megaDefinition) == true
            && megaDefinition.MegaIntermediateMovementChanceBase > 0
            && megaDefinition.MegaIntermediateMovementChanceIntelligenceScale > 0) {
            movementChanceBase = megaDefinition.MegaIntermediateMovementChanceBase;
            movementChanceIntelligenceScale = megaDefinition.MegaIntermediateMovementChanceIntelligenceScale;
        }
        var movementChance = Math.Min(
            100,
            movementChanceBase
                + (movementChanceIntelligenceScale * monster.Intelligence));
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)((seed / 100U) % 100U);
        if (movementRoll >= movementChance)
            return false;

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, player, players)
            : FindNativeRandomWalkStep(monster, player, players, seed);
        if (!randomWalk.HasValue)
            return false;
        monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
        return true;
    }

    /** Native Counselor wounded-adjacent retreat persists its four RandomWalk attempts. */
    private bool TryAdvanceCounselorAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!UsesNativeCounselorAi(monster))
            return false;
        if (monster.AdvanceCounselorDelay())
            return true;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        // Native CounselorAi always consumes its initial GenerateRnd(100)
        // before evaluating the current goal. Seeded targets use the restored
        // stream; zero-seed targets retain the compatibility draw.
        var actionRoll = random.GenerateRnd(100);

        if (monster.CounselorRetreatTicks != 0) {
            var retreat = usesNativeAiStream
                ? FindNativeCounselorRetreatStepFromRandom(monster, victim, players)
                : FindNativeCounselorRetreatStep(
                    monster,
                    victim,
                    players,
                    unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId));
            if (retreat.HasValue)
                monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
            monster.AdvanceCounselorRetreat();
            return true;
        }

        if (distance >= 2
            && TryGetMonsterMissile(monster, out var missile)
            && missile.ProjectileBehavior is (
                AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.MonsterMagic
                or AuthoritativeProjectileBehavior.Lightning)) {
            var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
            var attackChanceBase = monsterTypes?.TryGet(
                    monster.MonsterId,
                    out var counselorAttackDefinition) == true
                && counselorAttackDefinition.CounselorAttackChanceBase > 0
                ? counselorAttackDefinition.CounselorAttackChanceBase
                : 50;
            var attackChanceIntelligenceScale = monsterTypes?.TryGet(
                    monster.MonsterId,
                    out counselorAttackDefinition) == true
                && counselorAttackDefinition.CounselorAttackChanceIntelligenceScale > 0
                ? counselorAttackDefinition.CounselorAttackChanceIntelligenceScale
                : 5;
            var attackChance = Math.Min(
                100,
                attackChanceBase
                    + (attackChanceIntelligenceScale * monster.Intelligence));
            if ((usesNativeAiStream
                    ? actionRoll
                    : (int)(attackSeed % 100U)) >= attackChance
                || !world.HasMissileLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    victim.PositionX,
                    victim.PositionY)) {
                // Native CounselorAi delays after a missed ranged admission,
                // then uses a 30-percent Move/ RoundWalk branch instead of
                // falling through to generic pursuit.
                var movementChance = monsterTypes?.TryGet(
                        monster.MonsterId,
                        out var counselorDefinition) == true
                    && counselorDefinition.CounselorMoveChancePercent > 0
                    ? counselorDefinition.CounselorMoveChancePercent
                    : 30;
                var movementRoll = usesNativeAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)((attackSeed / 100U) % 100U);
                if (movementRoll < movementChance) {
                    var roundWalk = FindNativeRoundWalkStep(
                        monster,
                        victim,
                        players,
                        attackSeed);
                    if (roundWalk.HasValue)
                        monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                } else {
                    monster.StartCounselorDelay(
                        usesNativeAiStream
                            ? ResolveCounselorDelayFromRandom(monster)
                            : ResolveCounselorDelay(monster, attackSeed));
                }
                return true;
            }
        }

        if (distance > 1)
            return false;

        // CounselorAi compares whole native hit points against the integer
        // half-health threshold before entering MonsterGoal::Retreat.
        if (monster.HitPoints < monster.MaxHitPoints / 2) {
            // Native goalVar1 starts at zero and admits four RandomWalk calls
            // (0, 1, 2, and 3) on subsequent active AI ticks.
            monster.StartCounselorRetreat(4);
            return true;
        }

        // Native CounselorAi uses its adjacent ranged attack to emit the two
        // stationary Flash layers; it never falls through to generic melee.
        var adjacentAttackSeed = unchecked((uint)tick ^ monster.EntityId ^ victim.EntityId);
        var adjacentAttackChanceBase = 20;
        var adjacentAttackChanceIntelligenceScale = 2;
        if (monsterTypes?.TryGet(monster.MonsterId, out var adjacentAttackDefinition) == true
            && adjacentAttackDefinition.CounselorAdjacentAttackChanceBase > 0
            && adjacentAttackDefinition.CounselorAdjacentAttackChanceIntelligenceScale > 0) {
            adjacentAttackChanceBase = adjacentAttackDefinition.CounselorAdjacentAttackChanceBase;
            adjacentAttackChanceIntelligenceScale = adjacentAttackDefinition.CounselorAdjacentAttackChanceIntelligenceScale;
        }
        var adjacentAttackChance = Math.Min(
            100,
            adjacentAttackChanceBase
                + (adjacentAttackChanceIntelligenceScale * monster.Intelligence));
        var adjacentAttackRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(adjacentAttackSeed % 100U);
        if (adjacentAttackRoll < adjacentAttackChance) {
            CreateMonsterFlashLayers(monster);
            monster.StartCounselorDelay(
                usesNativeAiStream
                    ? ResolveCounselorAttackDelayFromRandom(monster)
                    : ResolveCounselorAttackDelay(monster, adjacentAttackSeed));
        } else {
            monster.StartCounselorDelay(
                usesNativeAiStream
                    ? ResolveCounselorDelayFromRandom(monster)
                    : ResolveCounselorDelay(monster, adjacentAttackSeed));
        }
        return true;
    }

    /** Native Counselor RandomWalk begins in the direction opposite the victim. */
    private (int X, int Y)? FindNativeCounselorRetreatStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var towardPlayer = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var awayFromPlayer = RotateDirection(towardPlayer, 4);
        var firstTurn = (seed / 100U) % 2U == 0 ? 1 : -1;
        var secondTurn = (seed / 200U) % 2U == 0 ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            awayFromPlayer,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    private (
        int AttackChanceBase,
        int AttackChanceIntelligenceScale,
        int SpawnMinimumDistance,
        int SpawnChanceBase,
        int SpawnChanceIntelligenceScale,
        int MovementChanceBase,
        int MovementChanceIntelligenceScale)? GetHorkDemonTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.HorkDemonAttackChanceBase > 0
                && definition.HorkDemonAttackChanceIntelligenceScale > 0
                && definition.HorkDemonSpawnMinimumDistance > 0
                && definition.HorkDemonSpawnChanceBase > 0
                && definition.HorkDemonSpawnChanceIntelligenceScale > 0
                && definition.HorkDemonMovementChanceBase > 0
                && definition.HorkDemonMovementChanceIntelligenceScale > 0) {
                return (
                    definition.HorkDemonAttackChanceBase,
                    definition.HorkDemonAttackChanceIntelligenceScale,
                    definition.HorkDemonSpawnMinimumDistance,
                    definition.HorkDemonSpawnChanceBase,
                    definition.HorkDemonSpawnChanceIntelligenceScale,
                    definition.HorkDemonMovementChanceBase,
                    definition.HorkDemonMovementChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "HorkDemon")
            ? (28, 2, 3, 43, 2, 33, 2)
            : null;
    }

    private bool TryAdvanceScavengerAi(
        AuthoritativeCombatTarget target,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var scavengerTuning = GetScavengerTuning(target);
        if (!scavengerTuning.HasValue)
            return false;
        if (target.HitPointsQ6 >= target.MaxHitPointsQ6
                * scavengerTuning.Value.HealingHealthThresholdPercent
                / 100
            && target.ScavengerHealingTicks == 0) {
            return false;
        }
        if (target.ScavengerHealingTicks == 0)
            target.StartScavengerHealing(scavengerTuning.Value.HealingDurationTicks);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.MonsterAiSeed != 0;

        AuthoritativeCorpse? corpse = null;
        if (target.ScavengerCorpseEntityId != 0)
            world.Corpses.TryGetValue(target.ScavengerCorpseEntityId, out corpse);
        if (corpse is null
            || (corpse.LevelId != 0 && corpse.LevelId != target.LevelId)
            || DeterministicGridMetrics.ChebyshevDistance(
                corpse.PositionX,
                corpse.PositionY,
                target.PositionX,
                target.PositionY)
                > scavengerTuning.Value.CorpseSearchRadius
            || !world.HasAiLineOfSight(
                target.LevelId,
                target.PositionX,
                target.PositionY,
                corpse.PositionX,
                corpse.PositionY)) {
            corpse = usesNativeAiStream
                ? FindNativeScavengerCorpseFromRandom(
                    target,
                    scavengerTuning.Value.CorpseSearchRadius)
                : FindNativeScavengerCorpse(
                    target,
                    scavengerTuning.Value.CorpseSearchRadius);
            target.SelectScavengerCorpse(corpse?.EntityId ?? 0);
        }

        if (corpse is null) {
            target.AdvanceScavengerHealing();
            if (target.ScavengerHealingTicks == 0)
                target.ClearScavengerHealing();
            return false;
        }

        if (target.PositionX == corpse.PositionX
            && target.PositionY == corpse.PositionY) {
            var healingQ6 = rules.GameVariant == DiabloGameVariant.Hellfire
                ? target.MaxHitPointsQ6 / scavengerTuning.Value.HellfireHealingDivisor
                : DeterministicFixedPointQ6.FromWhole(scavengerTuning.Value.BaseHealingAmount);
            if (rules.GameVariant != DiabloGameVariant.Hellfire)
                healingQ6 = DeterministicFixedPointQ6.FromWhole(scavengerTuning.Value.BaseHealingAmount);
            target.ApplyHealingQ6(healingQ6);
            target.AdvanceScavengerHealing();
            var targetHealthQ6 = rules.GameVariant == DiabloGameVariant.Hellfire
                ? target.MaxHitPointsQ6
                : target.MaxHitPointsQ6
                    * scavengerTuning.Value.BaseHealingTargetPercent
                    / 100;
            if (rules.GameVariant == DiabloGameVariant.Hellfire
                && (target.HitPointsQ6 == target.MaxHitPointsQ6
                    || target.ScavengerHealingTicks == 0)) {
                world.Corpses.Remove(corpse.EntityId);
            }
            if (target.HitPointsQ6 >= targetHealthQ6
                || target.ScavengerHealingTicks == 0)
                target.ClearScavengerHealing();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepTowardFromRandom(
                target,
                corpse.PositionX,
                corpse.PositionY,
                players)
            : FindNativeRandomWalkStepToward(
                target,
                corpse.PositionX,
                corpse.PositionY,
                players,
                unchecked((uint)tick ^ target.EntityId ^ corpse.EntityId));
        if (randomWalk.HasValue)
            target.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
        target.AdvanceScavengerHealing();
        if (target.ScavengerHealingTicks == 0)
            target.ClearScavengerHealing();
        return true;
    }

    /** Native ScavengerFindCorpse's coin-selected square scan order. */
    private AuthoritativeCorpse? FindNativeScavengerCorpse(
        AuthoritativeCombatTarget scavenger,
        int searchRadius)
    {
        var reverseSearch = random.FlipCoin(2);
        var first = reverseSearch ? searchRadius : -searchRadius;
        var last = reverseSearch ? -searchRadius : searchRadius;
        var increment = reverseSearch ? -1 : 1;
        for (var y = first; reverseSearch ? y >= last : y <= last; y += increment) {
            for (var x = first; reverseSearch ? x >= last : x <= last; x += increment) {
                var candidateX = checked(scavenger.PositionX + x);
                var candidateY = checked(scavenger.PositionY + y);
                var corpse = world.Corpses.Values.FirstOrDefault(candidate =>
                    (candidate.LevelId == 0 || candidate.LevelId == scavenger.LevelId)
                    && candidate.PositionX == candidateX
                    && candidate.PositionY == candidateY);
                if (corpse is not null
                    && world.HasAiLineOfSight(
                        scavenger.LevelId,
                        scavenger.PositionX,
                        scavenger.PositionY,
                        candidateX,
                        candidateY))
                    return corpse;
            }
        }
        return null;
    }

    private (
        int HealingDurationTicks,
        int CorpseSearchRadius,
        int HellfireHealingDivisor,
        int BaseHealingAmount,
        int BaseHealingTargetPercent,
        int HealingHealthThresholdPercent)? GetScavengerTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasHealingTuning = definition.ScavengerHealingDurationTicks > 0
                && definition.ScavengerCorpseSearchRadius > 0
                && definition.ScavengerHellfireHealingDivisor > 0
                && definition.ScavengerBaseHealingAmount > 0
                && definition.ScavengerBaseHealingTargetPercent > 0;
            var hasThresholdTuning = definition.ScavengerHealingHealthThresholdPercent > 0;
            if (hasHealingTuning || hasThresholdTuning) {
                return (
                    hasHealingTuning ? definition.ScavengerHealingDurationTicks : 10,
                    hasHealingTuning ? definition.ScavengerCorpseSearchRadius : 4,
                    hasHealingTuning ? definition.ScavengerHellfireHealingDivisor : 8,
                    hasHealingTuning ? definition.ScavengerBaseHealingAmount : 1,
                    hasHealingTuning ? definition.ScavengerBaseHealingTargetPercent : 75,
                    hasThresholdTuning ? definition.ScavengerHealingHealthThresholdPercent : 50);
            }
        }
        return UsesNativeAi(target, "Scavenger")
            ? (10, 4, 8, 1, 75, 50)
            : null;
    }

    private void ReplaceDeadMonsterWithCorpse(AuthoritativeCombatTarget target)
    {
        if (!world.CombatTargets.Remove(target.EntityId))
            return;
        world.Corpses[target.EntityId] = new AuthoritativeCorpse(
            target.EntityId,
            target.LevelId,
            target.PositionX,
            target.PositionY);
    }

    private bool TryAdvanceSneakAi(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        var sneakTuning = GetSneakTuning(target);
        if (!sneakTuning.HasValue)
            return false;

        var preferredDistance = Math.Max(
            1,
            sneakTuning.Value.DistanceBase
                - (target.Intelligence * sneakTuning.Value.DistanceIntelligenceScale));
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.MonsterAiSeed != 0;
        var actionSeed = unchecked((uint)target.EntityId ^ victim.EntityId);
        // Native SneakAi consumes one GenerateRnd(100) before evaluating its
        // fade, retreat, and adjacent-attack branches. Zero-seed targets keep
        // the deterministic compatibility path and therefore do not advance
        // the shared stream here.
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (target.SneakRetreatTicks != 0) {
            if (distance >= preferredDistance + sneakTuning.Value.RetreatReleaseDistance) {
                target.StartSneakRetreat(0);
                return false;
            }

            var direction = GetDirection(
                victim.PositionX,
                victim.PositionY,
                target.PositionX,
                target.PositionY);
            (int X, int Y)? retreat;
            if (usesNativeAiStream) {
                retreat = FindNativeRandomWalkStepFromRandom(target, direction, players);
            } else {
                retreat = null;
                foreach (var candidateDirection in DeterministicGridDirections.EnumerateAlternating(
                             direction)) {
                    var displacement = DirectionDisplacements[candidateDirection];
                    var candidateX = checked(target.PositionX + displacement.X);
                    var candidateY = checked(target.PositionY + displacement.Y);
                    if (!world.IsWalkable(target.LevelId, candidateX, candidateY)
                        || world.IsOccupied(
                            target.LevelId,
                            candidateX,
                            candidateY,
                            players,
                            target.EntityId)) {
                        continue;
                    }
                    retreat = (candidateX, candidateY);
                    break;
                }
            }
            if (retreat.HasValue) {
                target.BeginMovement(retreat.Value.X, retreat.Value.Y);
            }
            target.AdvanceSneakRetreat();
            return true;
        }

        if (target.SneakHidden) {
            if (distance < preferredDistance)
                target.SetSneakHidden(false);
            return true;
        }

        if (distance >= preferredDistance + 1) {
            target.SetSneakHidden(true);
            return true;
        }

        // Native Sneak only starts an adjacent attack when its action roll is
        // below intelligence + 10 (scaled by four). Returning true suppresses
        // the generic melee fallback for the admitted no-attack tick.
        if (usesNativeAiStream
            && distance <= 1
            && actionRoll >= Math.Min(100, 4 * target.Intelligence + 10))
            return true;
        return false;
    }

    private (int DistanceBase, int DistanceIntelligenceScale, int RetreatReleaseDistance, int RetreatTicks)?
        GetSneakTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SneakDistanceBase > 0
                && definition.SneakDistanceIntelligenceScale > 0
                && definition.SneakRetreatReleaseDistance > 0
                && definition.SneakRetreatTicks > 0) {
                return (
                    definition.SneakDistanceBase,
                    definition.SneakDistanceIntelligenceScale,
                    definition.SneakRetreatReleaseDistance,
                    definition.SneakRetreatTicks);
            }
        }
        return UsesNativeAi(target, "Sneak")
            ? (5, 1, 3, 9)
            : null;
    }

    private (
        int ChanceBase,
        int IntelligenceScale,
        int RetreatDurationTicks)? GetBatAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasAttackTuning = definition.BatAttackChanceBase > 0
                && definition.BatAttackChanceIntelligenceScale > 0;
            var hasRetreatTuning = definition.BatRetreatDurationTicks > 0;
            if (hasAttackTuning || hasRetreatTuning) {
                return (
                    hasAttackTuning ? definition.BatAttackChanceBase : 8,
                    hasAttackTuning ? definition.BatAttackChanceIntelligenceScale : 4,
                    hasRetreatTuning ? definition.BatRetreatDurationTicks : 2);
            }
        }
        return UsesNativeAi(target, "Bat")
            ? (8, 4, 2)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetSkeletonMeleeDelayTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonMeleeDelayChanceBase > 0
                && definition.SkeletonMeleeDelayChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonMeleeDelayChanceBase,
                    definition.SkeletonMeleeDelayChanceIntelligenceScale);
            }
        }
        return UsesNativeSkeletonAi(target)
            ? (35, 4)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetSkeletonMeleeAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonMeleeAttackChanceBase > 0
                && definition.SkeletonMeleeAttackChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonMeleeAttackChanceBase,
                    definition.SkeletonMeleeAttackChanceIntelligenceScale);
            }
        }
        return UsesNativeSkeletonAi(target)
            ? (20, 2)
            : null;
    }

    private int ResolveSkeletonMeleeMovementDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 15;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeMovementDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeMovementDelayBase > 0
                ? definition.SkeletonMeleeMovementDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeMovementDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeMovementDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeMovementDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + (int)((actionSeed / 100U) % (uint)randomRange)));
    }

    private int ResolveSkeletonMeleeMovementDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 15;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeMovementDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeMovementDelayBase > 0
                ? definition.SkeletonMeleeMovementDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeMovementDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeMovementDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeMovementDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + random.RandomIntLessThan(randomRange)));
    }

    private int ResolveSkeletonMeleeAttackDelay(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeAttackDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeAttackDelayBase > 0
                ? definition.SkeletonMeleeAttackDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeAttackDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeAttackDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeAttackDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + (int)((attackSeed / 100U) % (uint)randomRange)));
    }

    private int ResolveSkeletonMeleeAttackDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonMeleeAttackDelayRandomRange > 0) {
            delayBase = definition.SkeletonMeleeAttackDelayBase > 0
                ? definition.SkeletonMeleeAttackDelayBase
                : delayBase;
            intelligenceScale = definition.SkeletonMeleeAttackDelayIntelligenceScale > 0
                ? definition.SkeletonMeleeAttackDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.SkeletonMeleeAttackDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + random.RandomIntLessThan(randomRange)));
    }

    /** FallenAi delegates its normal state to the native SkeletonAi controller. */
    private bool UsesNativeSkeletonAi(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            // Validated controller tuples take precedence over a legacy label
            // when a compatibility target carries both. Unique encounters
            // may still retain a generic catalog type while supplying an
            // explicit native AI identity, so the label fallback remains
            // below the complete-tuning checks.
            var hasSkeletonTuning = definition.SkeletonMeleeDelayChanceBase > 0
                && definition.SkeletonMeleeDelayChanceIntelligenceScale > 0
                || definition.SkeletonMeleeAttackChanceBase > 0
                && definition.SkeletonMeleeAttackChanceIntelligenceScale > 0;
            if (hasSkeletonTuning)
                return true;
            if (target.NativeAi is "SkeletonMelee" or "Snotspill" or "Warlord"
                && target.NativeAi != definition.NativeAi)
                return true;
            if (target.NativeAi == definition.NativeAi
                && (GetSneakTuning(target).HasValue
                    || GetBatAttackTuning(target).HasValue
                    || GetGoatMeleeMovementTuning(target).HasValue
                    || GetGoatMeleeAttackTuning(target).HasValue
                    || GetFatMovementTuning(target).HasValue
                    || GetFatAttackTuning(target).HasValue
                    || GetZombieActionTuning(target).HasValue
                    || GetZombieMovementTuning(target).HasValue
                    || GetZombieRandomDirectionTuning(target).HasValue
                    || GetSkeletonKingSummonTuning(target).HasValue
                    || GetHorkDemonTuning(target).HasValue
                    || GetScavengerTuning(target).HasValue
                    || GetGargoyleTuning(target).HasValue
                    || GetSkeletonRangedTuning(target).HasValue
                    || GetRhinoTuning(target).HasValue))
                return false;
            if (definition.HasCompleteAiTuning)
                return false;
            return definition.NativeAi is "SkeletonMelee" or "Fallen" or "Snotspill";
        }
        return target.NativeAi is "SkeletonMelee" or "Fallen" or "Snotspill" or "Warlord";
    }

    private (int ChanceBase, int IntelligenceScale)? GetGoatMeleeMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.GoatMeleeMovementChanceBase > 0
                && definition.GoatMeleeMovementChanceIntelligenceScale > 0) {
                return (
                    definition.GoatMeleeMovementChanceBase,
                    definition.GoatMeleeMovementChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "GoatMelee")
            || UsesNativeAi(target, "Gharbad")
            ? (28, 2)
            : null;
    }

    private (
        int ChanceBase,
        int IntelligenceScale,
        int SpecialHealthThresholdPercent,
        int SpecialChanceGroupPeriod)? GetGoatMeleeAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasAttackTuning = definition.GoatMeleeAttackChanceBase > 0
                && definition.GoatMeleeAttackChanceIntelligenceScale > 0;
            var hasSpecialTuning = definition.GoatMeleeSpecialHealthThresholdPercent > 0
                && definition.GoatMeleeSpecialChanceGroupPeriod > 0;
            if (hasAttackTuning || hasSpecialTuning) {
                return (
                    hasAttackTuning
                        ? definition.GoatMeleeAttackChanceBase
                        : 23,
                    hasAttackTuning
                        ? definition.GoatMeleeAttackChanceIntelligenceScale
                        : 2,
                    hasSpecialTuning
                        ? definition.GoatMeleeSpecialHealthThresholdPercent
                        : 50,
                    hasSpecialTuning
                        ? definition.GoatMeleeSpecialChanceGroupPeriod
                        : 2);
            }
        }
        return UsesNativeAi(target, "GoatMelee")
            || UsesNativeAi(target, "Gharbad")
            ? (23, 2, 50, 2)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetFatMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.FatMovementChanceBase > 0
                && definition.FatMovementChanceIntelligenceScale > 0) {
                return (
                    definition.FatMovementChanceBase,
                    definition.FatMovementChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Fat")
            ? (20, 4)
            : null;
    }

    private (
        int AttackChanceBase,
        int AttackChanceIntelligenceScale,
        int SpecialChanceBase,
        int SpecialChanceIntelligenceScale)? GetFatAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.FatAttackChanceBase > 0
                && definition.FatAttackChanceIntelligenceScale > 0
                && definition.FatSpecialChanceBase > 0
                && definition.FatSpecialChanceIntelligenceScale > 0) {
                return (
                    definition.FatAttackChanceBase,
                    definition.FatAttackChanceIntelligenceScale,
                    definition.FatSpecialChanceBase,
                    definition.FatSpecialChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Fat")
            ? (20, 4, 15, 4)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)? GetZombieActionTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.ZombieActionChanceBase > 0
                && definition.ZombieActionChanceIntelligenceScale > 0) {
                return (
                    definition.ZombieActionChanceBase,
                    definition.ZombieActionChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Zombie")
            ? (10, 2)
            : null;
    }

    private (int MinimumDistance, int DistanceBase, int IntelligenceScale)?
        GetZombieMovementTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.ZombieMovementDistanceMinimum > 0
                && definition.ZombieMovementDistanceBase > 0
                && definition.ZombieMovementDistanceIntelligenceScale > 0) {
                return (
                    definition.ZombieMovementDistanceMinimum,
                    definition.ZombieMovementDistanceBase,
                    definition.ZombieMovementDistanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Zombie")
            ? (2, 4, 2)
            : null;
    }

    private (int ChanceBase, int IntelligenceScale)?
        GetZombieRandomDirectionTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.ZombieRandomDirectionChanceBase > 0
                && definition.ZombieRandomDirectionChanceIntelligenceScale > 0) {
                return (
                    definition.ZombieRandomDirectionChanceBase,
                    definition.ZombieRandomDirectionChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Zombie")
            ? (20, 2)
            : null;
    }

    private bool TryAdvanceSkeletonKingAi(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState victim,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (target.AdvanceSkeletonKingDelay())
            return true;

        var summonTuning = GetSkeletonKingSummonTuning(target);
        if (!summonTuning.HasValue)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = target.MonsterAiSeed != 0;
        var usesNativeActionRoll = gameMode == DiabloGameMode.SinglePlayer
            || usesNativeAiStream;
        var actionSeed = unchecked((uint)tick ^ target.EntityId ^ victim.EntityId);
        // Native LeoricAi consumes its first GenerateRnd(100) before the
        // summon/attack branches, even when multiplayer quest rules suppress
        // summoning. Single-player uses the process stream; seed-zero
        // multiplayer targets retain the compatibility path.
        var actionRoll = usesNativeActionRoll
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        var canAttemptSummon = summonTuning.HasValue
            && monsterSpawnPlanner is not null
            && gameMode != DiabloGameMode.Multiplayer
            && distance >= summonTuning.Value.MinimumDistance
            && actionRoll < Math.Min(
                    100,
                    summonTuning.Value.ChanceBase
                        + (summonTuning.Value.IntelligenceScale * target.Intelligence))
            && world.HasMissileLineOfSight(
                target.LevelId,
                target.PositionX,
                target.PositionY,
                victim.PositionX,
                victim.PositionY)
            && world.CombatTargets.Count < 256;
        if (canAttemptSummon) {
            var direction = GetDirection(
                target.PositionX,
                target.PositionY,
                victim.PositionX,
                victim.PositionY);
            var displacement = DirectionDisplacements[direction];
            var positionX = checked(target.PositionX + displacement.X);
            var positionY = checked(target.PositionY + displacement.Y);
            if (world.IsWalkable(target.LevelId, positionX, positionY)
                && !world.IsOccupied(
                    target.LevelId,
                    positionX,
                    positionY,
                    players,
                    target.EntityId)) {
                var entityId = AllocateMonsterEntityId(players);
                var summon = monsterSpawnPlanner!.CreateSkeletonSummon(
                    target.LevelId,
                    random,
                    positionX,
                    positionY,
                    entityId,
                    gameMode == DiabloGameMode.Multiplayer,
                    direction);
                if (summon is not null) {
                    DiabloDifficultyMonsterScaling.ApplyInitialHealth(
                        summon,
                        difficulty,
                        rules.GameVariant,
                        gameMode,
                        rules.DifficultyRules);
                    DiabloDifficultyMonsterScaling.ApplyInitialResistances(
                        summon,
                        difficulty,
                        monsterTypes);
                    world.CombatTargets.Add(entityId, summon);
                    return true;
                }
            }
        }

        if (distance <= 1) {
            // Native Leoric uses the first action roll for adjacent attack
            // admission once its summon branch is not taken.
            if (usesNativeActionRoll
                && actionRoll >= Math.Min(100, target.Intelligence + 20))
                return true;
            return false;
        }

        var movementTuning = GetSkeletonKingMovementTuning(target);
        var movementChance = Math.Min(
            100,
            (movementTuning?.ChanceBase ?? 25)
                + (movementTuning?.IntelligenceScale ?? 1) * target.Intelligence);
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (movementRoll >= (uint)movementChance) {
            target.StartSkeletonKingDelay(
                usesNativeAiStream
                    ? ResolveSkeletonKingDelayFromRandom(target)
                    : ResolveSkeletonKingDelay(target, actionSeed));
            return true;
        }

        var roundWalkChanceModulo = 4;
        if (monsterTypes?.TryGet(target.MonsterId, out var skeletonKingDefinition) == true
            && skeletonKingDefinition.SkeletonKingRoundWalkChanceModulo > 0) {
            roundWalkChanceModulo = skeletonKingDefinition.SkeletonKingRoundWalkChanceModulo;
        }
        var admitsRoundWalk = distance >= 3
            && (usesNativeAiStream
                ? random.FlipCoin((uint)roundWalkChanceModulo)
                : (actionSeed / 100U) % (uint)roundWalkChanceModulo == 0U);
        if (admitsRoundWalk) {
            var roundWalk = usesNativeAiStream
                ? FindNativeRoundWalkStepFromRandom(target, victim, players)
                : FindNativeRoundWalkStep(target, victim, players, actionSeed);
            if (roundWalk.HasValue) {
                target.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                return true;
            }

            // Native LeoricAi enters AiDelay when its admitted RoundWalk
            // cannot find a candidate; do not replace that tick with a
            // different movement policy.
            target.StartSkeletonKingDelay(
                usesNativeAiStream
                    ? ResolveSkeletonKingDelayFromRandom(target)
                    : ResolveSkeletonKingDelay(target, actionSeed));
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(target, victim, players)
            : FindNativeRandomWalkStep(target, victim, players, actionSeed);
        if (randomWalk.HasValue) {
            target.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            return true;
        }

        // Native LeoricAi consumes an admitted RandomWalk tick when every
        // native candidate is unavailable; bounded navigation must not invent
        // a replacement step.
        return true;
    }

    private (int MinimumDistance, int ChanceBase, int IntelligenceScale)?
        GetSkeletonKingSummonTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonKingSummonMinimumDistance > 0
                && definition.SkeletonKingSummonChanceBase > 0
                && definition.SkeletonKingSummonChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonKingSummonMinimumDistance,
                    definition.SkeletonKingSummonChanceBase,
                    definition.SkeletonKingSummonChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "SkeletonKing")
            ? (3, 35, 4)
            : null;
    }

    private int ResolveSkeletonKingDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonKingDelayBase > 0
            && definition.SkeletonKingDelayRandomRange > 0) {
            delayBase = definition.SkeletonKingDelayBase;
            randomRange = definition.SkeletonKingDelayRandomRange;
        }
        return checked(delayBase + (int)((actionSeed / 200U) % (uint)randomRange));
    }

    /** Native Leoric delay roll driven by the restored per-monster stream. */
    private int ResolveSkeletonKingDelayFromRandom(AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonKingDelayBase > 0
            && definition.SkeletonKingDelayRandomRange > 0) {
            delayBase = definition.SkeletonKingDelayBase;
            randomRange = definition.SkeletonKingDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private (int ChanceBase, int IntelligenceScale)? GetSkeletonKingMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.SkeletonKingMovementChanceBase > 0
            && definition.SkeletonKingMovementChanceIntelligenceScale > 0) {
            return (
                definition.SkeletonKingMovementChanceBase,
                definition.SkeletonKingMovementChanceIntelligenceScale);
        }
        return UsesNativeAi(target, "SkeletonKing")
            ? (25, 1)
            : null;
    }

    /** Native ProcessMonsters health regeneration, applied before each AI tick. */
    private void ApplyNativeMonsterRegeneration(
        AuthoritativeCombatTarget monster,
        int levelDivisor)
    {
        var effectiveMonsterLevel = GetEffectiveMonsterLevel(monster);
        if (effectiveMonsterLevel <= 0 || monster.HitPointsQ6 >= monster.MaxHitPointsQ6)
            return;

        var healing = effectiveMonsterLevel > 1
            ? effectiveMonsterLevel / levelDivisor
            : effectiveMonsterLevel;
        monster.ApplyHealingQ6(DeterministicFixedPointQ6.FromWhole(healing));
    }

    /** Native ZombieAi action band and long-range walk boundary. */
    private bool TryAdvanceZombieAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        var zombieActionTuning = GetZombieActionTuning(monster);
        var zombieMovementTuning = GetZombieMovementTuning(monster);
        if (!zombieActionTuning.HasValue || !zombieMovementTuning.HasValue)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var actionChance = Math.Min(
            100,
            zombieActionTuning.Value.ChanceBase
                + (zombieActionTuning.Value.IntelligenceScale * monster.Intelligence));
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (actionRoll >= actionChance)
            return true;
        var longRangeMovementDistance = Math.Max(
            zombieMovementTuning.Value.MinimumDistance,
            zombieMovementTuning.Value.DistanceBase
                + (zombieMovementTuning.Value.IntelligenceScale * monster.Intelligence));
        if (distance < longRangeMovementDistance) {
            if (distance < 2)
                return false;
            var randomStep = usesNativeAiStream
                ? FindNativeRandomWalkStepFromRandom(monster, player, players)
                : FindNativeRandomWalkStep(monster, player, players, actionSeed);
            if (randomStep.HasValue)
                monster.BeginMovement(randomStep.Value.X, randomStep.Value.Y);
            return true;
        }

        if (usesNativeAiStream) {
            var direction = monster.Direction;
            var randomDirectionChance = Math.Min(
                100,
                zombieMovementTuning.Value.IntelligenceScale * monster.Intelligence + 20);
            if (random.RandomIntLessThan(100) < randomDirectionChance)
                direction = random.RandomIntLessThan(8);
            var nativeStep = FindStepInDirection(monster, direction, players);
            if (nativeStep.HasValue)
                monster.BeginMovement(nativeStep.Value.X, nativeStep.Value.Y);
            return true;
        }

        var step = FindZombieStep(monster, player, players, actionSeed);
        if (step.HasValue) {
            monster.BeginMovement(step.Value.X, step.Value.Y);
            return true;
        }
        // Native ZombieAi consumes the action even when its one-step walk is
        // blocked; do not replace the failed walk with generic navigation.
        return true;
    }

    private (int X, int Y)? FindZombieStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var deltaX = Math.Sign((long)player.PositionX - monster.PositionX);
        var deltaY = Math.Sign((long)player.PositionY - monster.PositionY);
        var directions = new[] {
            (X: deltaX, Y: deltaY),
            (X: deltaX, Y: 0),
            (X: 0, Y: deltaY),
        };
        var randomDirectionTuning = GetZombieRandomDirectionTuning(monster);
        if (randomDirectionTuning.HasValue
            && seed % 100U < (uint)Math.Min(
                100,
                randomDirectionTuning.Value.ChanceBase
                    + (randomDirectionTuning.Value.IntelligenceScale
                        * monster.Intelligence))) {
            var randomDirection = (int)((seed / 100U) % 8U);
            directions = new[] {
                (X: randomDirection switch { 0 or 1 or 7 => 1, 2 or 6 => 0, _ => -1 },
                    Y: randomDirection switch { 1 or 2 or 3 => 1, 5 or 6 or 7 => -1, _ => 0 }),
            }.Concat(directions).ToArray();
        }
        foreach (var direction in directions) {
            if (direction.X == 0 && direction.Y == 0)
                continue;
            var candidate = (X: monster.PositionX + direction.X, Y: monster.PositionY + direction.Y);
            if (world.IsWalkable(monster.LevelId, candidate.X, candidate.Y)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidate.X,
                    candidate.Y,
                    players,
                    monster.EntityId))
                return candidate;
        }
        return null;
    }

    /** Native Counselor retreat RandomWalk ordering driven by the restored stream. */
    private (int X, int Y)? FindNativeCounselorRetreatStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var towardPlayer = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var awayFromPlayer = RotateDirection(towardPlayer, 4);
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        foreach (var candidateDirection in new[] {
                     awayFromPlayer,
                     RotateDirection(awayFromPlayer, firstTurn),
                     RotateDirection(awayFromPlayer, -firstTurn),
                     RotateDirection(awayFromPlayer, secondTurn),
                     RotateDirection(awayFromPlayer, -secondTurn),
                 }) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native ScavengerFindCorpse scan driven by the restored stream. */
    private AuthoritativeCorpse? FindNativeScavengerCorpseFromRandom(
        AuthoritativeCombatTarget scavenger,
        int searchRadius)
    {
        var reverseSearch = random.FlipCoin(2);
        var first = reverseSearch ? searchRadius : -searchRadius;
        var last = reverseSearch ? -searchRadius : searchRadius;
        var increment = reverseSearch ? -1 : 1;
        for (var y = first; reverseSearch ? y >= last : y <= last; y += increment) {
            for (var x = first; reverseSearch ? x >= last : x <= last; x += increment) {
                var candidateX = checked(scavenger.PositionX + x);
                var candidateY = checked(scavenger.PositionY + y);
                var corpse = world.Corpses.Values.FirstOrDefault(candidate =>
                    (candidate.LevelId == 0 || candidate.LevelId == scavenger.LevelId)
                    && candidate.PositionX == candidateX
                    && candidate.PositionY == candidateY);
                if (corpse is not null
                    && world.HasAiLineOfSight(
                        scavenger.LevelId,
                        scavenger.PositionX,
                        scavenger.PositionY,
                        candidateX,
                        candidateY))
                    return corpse;
            }
        }
        return null;
    }

    /** Native Scavenger RandomWalk toward its selected corpse driven by the stream. */
    private (int X, int Y)? FindNativeRandomWalkStepTowardFromRandom(
        AuthoritativeCombatTarget monster,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            targetX,
            targetY);
        return FindNativeRandomWalkStepFromRandom(monster, direction, players);
    }

    private (int X, int Y)? FindStepInDirection(
        AuthoritativeCombatTarget monster,
        int direction,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(monster.PositionX + displacement.X);
        var candidateY = checked(monster.PositionY + displacement.Y);
        return world.IsWalkable(monster.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                monster.LevelId,
                candidateX,
                candidateY,
                players,
                monster.EntityId)
            ? (candidateX, candidateY)
            : null;
    }

    /** Native RandomWalk candidate order shared by movement AI controllers. */
    private (int X, int Y)? FindNativeRandomWalkStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var firstTurn = (seed / 100U) % 2U == 0 ? 1 : -1;
        var secondTurn = (seed / 200U) % 2U == 0 ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            direction,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native RandomWalk ordering driven by the restored per-monster stream. */
    private (int X, int Y)? FindNativeRandomWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return FindNativeRandomWalkStepFromRandom(
            monster,
            GetDirection(
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY),
            players);
    }

    /** Native RandomWalk ordering from an explicitly selected direction. */
    private (int X, int Y)? FindNativeRandomWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        int direction,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            direction,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }
        return null;
    }

    /** Native RandomWalk candidate order toward an arbitrary AI target tile. */
    private (int X, int Y)? FindNativeRandomWalkStepToward(
        AuthoritativeCombatTarget monster,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            targetX,
            targetY);
        var firstTurn = (seed / 100U) % 2U == 0 ? 1 : -1;
        var secondTurn = (seed / 200U) % 2U == 0 ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            direction,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in directions) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native Gargoyle self-healing and its distance-based special lock. */
    private bool TryAdvanceGargoyleHealing(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        int distance)
    {
        var gargoyleTuning = GetGargoyleTuning(monster);
        if (!gargoyleTuning.HasValue)
            return false;
        if (monster.AdvanceAvoidanceDelay())
            return true;

        var safeDistance = gargoyleTuning.Value.DistanceBase
            + (gargoyleTuning.Value.IntelligenceScale * monster.Intelligence);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        if (monster.GargoyleSpecialActive) {
            if (distance < safeDistance)
                monster.GargoyleSpecialActive = false;
            return true;
        }

        if (monster.HitPointsQ6 >= monster.MaxHitPointsQ6
                * gargoyleTuning.Value.HealingHealthThresholdPercent
                / 100)
            return false;

        if (distance < safeDistance) {
            if (!usesNativeAiStream)
                return TryBeginGargoyleRetreat(monster, player, players, distance);

            var direction = GetDirection(
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY);
            var retreat = FindNativeRandomWalkStepFromRandom(
                monster,
                RotateDirection(direction, 4),
                players);
            if (retreat.HasValue) {
                monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
                return true;
            }
            return false;
        }

        var healSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var healRoll = usesNativeAiStream
            ? random.RandomIntLessThan(gargoyleTuning.Value.HealDivisorRandomRange)
            : (int)(healSeed % (uint)gargoyleTuning.Value.HealDivisorRandomRange);
        var divisor = gargoyleTuning.Value.HealDivisorMultiplier
            * (healRoll
                + gargoyleTuning.Value.HealDivisorRandomOffset);
        var healingQ6 = DeterministicFixedPointQ6.FromWhole(
            monster.MaxHitPoints / divisor);
        var appliedHealingQ6 = monster.ApplyHealingQ6(healingQ6);
        if (appliedHealingQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                player,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Healing,
                    monster.EntityId,
                    monster.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(appliedHealingQ6)) {
                    AmountQ6 = appliedHealingQ6,
                }));
        }

        // Native MFLAG_ALLOW_SPECIAL remains set until the player closes the
        // intelligence+2 boundary, unless this heal reached full life.
        monster.GargoyleSpecialActive = monster.HitPointsQ6 < monster.MaxHitPointsQ6;
        return true;
    }

    private (
        int DistanceBase,
        int IntelligenceScale,
        int HealingHealthThresholdPercent,
        int HealDivisorMultiplier,
        int HealDivisorRandomRange,
        int HealDivisorRandomOffset)? GetGargoyleTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            var hasSafetyTuning = definition.GargoyleSafeDistanceBase > 0
                && definition.GargoyleSafeDistanceIntelligenceScale > 0;
            var hasHealingTuning = definition.GargoyleHealingHealthThresholdPercent > 0
                || definition.GargoyleHealDivisorMultiplier > 0
                || definition.GargoyleHealDivisorRandomRange > 0
                || definition.GargoyleHealDivisorRandomOffset > 0;
            if (hasSafetyTuning || hasHealingTuning) {
                return (
                    hasSafetyTuning ? definition.GargoyleSafeDistanceBase : 2,
                    hasSafetyTuning ? definition.GargoyleSafeDistanceIntelligenceScale : 1,
                    definition.GargoyleHealingHealthThresholdPercent > 0
                        ? definition.GargoyleHealingHealthThresholdPercent
                        : 50,
                    definition.GargoyleHealDivisorMultiplier > 0
                        ? definition.GargoyleHealDivisorMultiplier
                        : 16,
                    definition.GargoyleHealDivisorRandomRange > 0
                        ? definition.GargoyleHealDivisorRandomRange
                        : 5,
                    definition.GargoyleHealDivisorRandomOffset > 0
                        ? definition.GargoyleHealDivisorRandomOffset
                        : 4);
            }
        }
        return UsesNativeAi(target, "Gargoyle")
            ? (2, 1, 50, 16, 5, 4)
            : null;
    }

    /** Native Gargoyle AiAvoidance adjacent attack admission. */
    private (int ChanceBase, int IntelligenceScale)? GetGargoyleAttackTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.GargoyleAttackChanceBase > 0
            && definition.GargoyleAttackChanceIntelligenceScale > 0) {
            return (
                definition.GargoyleAttackChanceBase,
                definition.GargoyleAttackChanceIntelligenceScale);
        }
        return UsesNativeAi(target, "Gargoyle")
            ? (23, 2)
            : null;
    }

    /** Native GargoyleAi delegates healthy non-adjacent ticks to AiAvoidance. */
    private bool TryAdvanceGargoyleMovement(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!HasGargoyleAi(monster) || distance <= 1)
            return false;

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        var advancedMoveGoal = usesNativeAiStream
            ? TryAdvanceAiAvoidanceMoveGoalFromRandom(
                monster,
                player,
                players,
                distance,
                actionRoll)
            : TryAdvanceAiAvoidanceMoveGoal(
                monster,
                player,
                players,
                distance,
                actionSeed);
        if (advancedMoveGoal)
            return true;

        var movementTuning = GetGargoyleMovementTuning(monster);
        var movementChance = GetAiAvoidanceMovementChance(
            monster,
            movementTuning?.ChanceBase ?? 28,
            movementTuning?.IntelligenceScale ?? 2,
            GetAiAvoidanceMovementFreshChanceBonus(monster));
        if (actionRoll >= movementChance)
        {
            monster.AdvanceAvoidanceMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, player, players)
            : FindNativeRandomWalkStep(monster, player, players, actionSeed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            monster.RecordAvoidanceMovement(
                randomWalk.Value.X - monster.PositionX,
                randomWalk.Value.Y - monster.PositionY);
        } else {
            monster.AdvanceAvoidanceMovementIdle();
        }
        return true;
    }

    private bool HasGargoyleAi(AuthoritativeCombatTarget target)
        => target.NativeAi == "Gargoyle"
            || monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.NativeAi == "Gargoyle";

    private (int ChanceBase, int IntelligenceScale)? GetGargoyleMovementTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.GargoyleMovementChanceBase > 0
            && definition.GargoyleMovementChanceIntelligenceScale > 0) {
            return (
                definition.GargoyleMovementChanceBase,
                definition.GargoyleMovementChanceIntelligenceScale);
        }
        return HasGargoyleAi(target)
            ? (28, 2)
            : null;
    }

    /** Native GargoyleAi retreats from a wounded player before healing. */
    private bool TryBeginGargoyleRetreat(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int currentDistance)
    {
        var retreat = Enumerable.Range(-1, 3)
            .SelectMany(offsetY => Enumerable.Range(-1, 3)
                .Select(offsetX => (
                    X: monster.PositionX + offsetX,
                    Y: monster.PositionY + offsetY)))
            .Where(candidate => candidate.X != monster.PositionX
                || candidate.Y != monster.PositionY)
            .Where(candidate => world.IsWalkable(
                monster.LevelId,
                candidate.X,
                candidate.Y))
            .Where(candidate => !world.IsOccupied(
                monster.LevelId,
                candidate.X,
                candidate.Y,
                players,
                monster.EntityId))
            .Where(candidate => DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX,
                player.PositionY,
                candidate.X,
                candidate.Y) >= currentDistance)
            .OrderByDescending(candidate => DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX,
                player.PositionY,
                candidate.X,
                candidate.Y))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .Select(candidate => ((int X, int Y)?)candidate)
            .FirstOrDefault();

        if (!retreat.HasValue)
            return false;

        monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
        return true;
    }

    /** Native Arrow behavior selected by a validated MonsterArrow mapping. */
    private bool TryAdvanceGoatRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior is not (
                AuthoritativeProjectileBehavior.MonsterArrow
                or AuthoritativeProjectileBehavior.Targeted)
            || GetEffectiveMonsterToHit(monster, special: false) <= 0
            || GetEffectiveMonsterDamageRange(monster, special: false).Maximum <= 0
            || IsPlayerProtectedByFlash(player)
            || IsPlayerEtherealized(player))
            return false;

        if (monster.AdvanceRangedDelay())
            return true;

        if (HasActiveMonsterArrow(monster))
            return true;

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var retreatTuning = ResolveRangedRetreatTuning(missile);
        var retreatChance = checked(
            retreatTuning.ChanceBase
            + retreatTuning.IntelligenceScale * monster.Intelligence);
        if (distance < retreatTuning.MinimumDistance) {
            var retreatRoll = usesNativeAiStream
                ? random.RandomIntLessThan(100)
                : (int)(attackSeed % 100U);
            if (retreatRoll < Math.Min(100, retreatChance)) {
                var retreat = usesNativeAiStream
                    ? FindNativeRandomWalkStepFromRandom(
                        monster,
                        RotateDirection(
                            GetDirection(
                                monster.PositionX,
                                monster.PositionY,
                                player.PositionX,
                                player.PositionY),
                            4),
                        players)
                    : FindGoatRangedRetreatDestination(monster, player, players, distance);
                if (retreat.HasValue) {
                    monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
                    return true;
                }
            }
        }

        if (!world.HasMissileLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY))
            // Native AiRanged does not pursue when its clear missile lane is
            // unavailable. Close-range avoidance has already run above;
            // preserve the remaining deferred tick instead of generic
            // navigation.
            return true;

        var created = CreateNativeRangedAttackProjectile(
            monster,
            player,
            distance,
            attackSeed,
            missile);
        if (created)
            monster.StartRangedDelay(
                usesNativeAiStream
                    ? ResolveRangedDelayFromRandom(missile)
                    : ResolveRangedDelay(missile, attackSeed));
        return created;
    }

    /** Native Snake charge behavior selected by a validated SnakeCharge mapping. */
    private bool TryAdvanceSnakeAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (monsterMissiles is null
            || !TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.SnakeCharge)
            return false;
        if (monster.AdvanceSnakeDelay())
            return true;

        var chargeDistance = missile.ChargeMinimumDistance > 0
            ? missile.ChargeMinimumDistance
            : 2;
        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == AuthoritativeProjectileBehavior.SnakeCharge)) {
            // Native SnakeAi still consumes the adjacent attack-admission draw
            // while an in-flight charge owns the movement path.
            if (distance < 2)
                _ = random.RandomIntLessThan(100);
            return true;
        }
        if (distance == chargeDistance) {
            if (world.HasMissileLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY)) {
                var direction = GetDirection(
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY);
                var chargeProjectileTuning = ResolveMappedChargeProjectileTuning(
                    missile,
                    monster.Intelligence);
                var (velocityX, velocityY) = CalculateVelocity(
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY,
                    chargeProjectileTuning.Speed);
                var projectile = new AuthoritativeProjectile(
                    AllocateProjectileEntityId(),
                    monster.EntityId,
                    player.EntityId,
                    missile.SpellId != 0 ? missile.SpellId : 1,
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY,
                    missile.ProjectileDamage,
                    missile.DamageType ?? AuthoritativeDamageType.Physical,
                    0,
                    checked((uint)chargeProjectileTuning.Lifetime)) {
                    Behavior = AuthoritativeProjectileBehavior.SnakeCharge,
                    VelocityX = velocityX,
                    VelocityY = velocityY,
                    OriginalDirection = direction,
                    Speed = chargeProjectileTuning.Speed,
                    ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
                    ResistantDamageDivisor = 1,
                };
                world.Projectiles.Add(projectile.EntityId, projectile);
                return true;
            }
        }

        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(actionSeed % 100U);
        if (distance < 2) {
            var attackChanceBase = missile.SnakeAdjacentAttackChanceBase > 0
                ? missile.SnakeAdjacentAttackChanceBase
                : 20;
            var attackChanceIntelligenceScale = missile.SnakeAdjacentAttackChanceIntelligenceScale > 0
                ? missile.SnakeAdjacentAttackChanceIntelligenceScale
                : 1;
            var attackChance = Math.Min(
                100,
                attackChanceBase + (attackChanceIntelligenceScale * monster.Intelligence));
            if (actionRoll >= attackChance) {
                monster.StartSnakeDelay(
                    usesNativeAiStream
                        ? ResolveSnakeDelayFromRandom(
                            missile,
                            monster.Intelligence,
                            adjacent: true)
                        : ResolveSnakeDelay(missile, actionSeed, monster.Intelligence, adjacent: true));
                return true;
            }
            return false;
        }

        var delayChanceBase = missile.SnakeDistantDelayChanceBase > 0
            ? missile.SnakeDistantDelayChanceBase
            : 35;
        var delayChanceIntelligenceScale = missile.SnakeDistantDelayChanceIntelligenceScale > 0
            ? missile.SnakeDistantDelayChanceIntelligenceScale
            : 2;
        var delayBand = Math.Max(
            0,
            delayChanceBase - (delayChanceIntelligenceScale * monster.Intelligence));
        if (actionRoll < delayBand) {
            monster.StartSnakeDelay(
                usesNativeAiStream
                    ? ResolveSnakeDelayFromRandom(
                        missile,
                        monster.Intelligence,
                        adjacent: false)
                    : ResolveSnakeDelay(missile, actionSeed, monster.Intelligence, adjacent: false));
            return true;
        }

        var drift = usesNativeAiStream
            ? FindSnakeDriftStepFromRandom(
                monster,
                player,
                players,
                actionSeed,
                missile.SnakeDriftPattern)
            : FindSnakeDriftStep(
                monster,
                player,
                players,
                actionSeed,
                missile.SnakeDriftPattern);
        if (drift.HasValue)
            monster.BeginMovement(drift.Value.X, drift.Value.Y);
        return true;
    }

    /** Native AiAvoidance far-distance Move goal shared by GoatMelee/Gargoyle. */
    private bool TryAdvanceAiAvoidanceMoveGoal(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance,
        uint seed)
    {
        var movementMinimumDistance = 4;
        var movementChanceModulo = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var avoidanceDefinition) == true
            && avoidanceDefinition.AiAvoidanceMovementMinimumDistance > 0
            && avoidanceDefinition.AiAvoidanceMovementChanceModulo > 0) {
            movementMinimumDistance = avoidanceDefinition.AiAvoidanceMovementMinimumDistance;
            movementChanceModulo = avoidanceDefinition.AiAvoidanceMovementChanceModulo;
        }
        if (distance < 2) {
            monster.ClearAvoidanceMovement();
            return false;
        }

        var hasMoveGoal = monster.AvoidanceMovementTicks >= 0;
        var startsMoveGoal = distance >= movementMinimumDistance
            && (seed / 100U) % (uint)movementChanceModulo == 0U;
        if (!hasMoveGoal && !startsMoveGoal)
            return false;

        if (!hasMoveGoal)
            monster.StartAvoidanceMovement((int)((seed / 100U) % 2U));
        if (!monster.AdvanceAvoidanceMovement(
                distance,
                world.HasAiLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY),
                IsNativeAvoidanceDirectionAvailable(
                    monster,
                    player,
                    players)))
            return false;

        var roundWalk = FindNativeStatefulRoundWalkStep(
            monster,
            player,
            players,
            seed,
            monster.AvoidanceMovementTurn,
            out var nextTurn);
        monster.UpdateAvoidanceMovementTurn(nextTurn);
        if (roundWalk.HasValue) {
            monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
            monster.RecordAvoidanceMovement(
                roundWalk.Value.X - monster.PositionX,
                roundWalk.Value.Y - monster.PositionY);
        } else {
            monster.StartAvoidanceDelay(ResolveAvoidanceDelay(monster, seed));
        }
        return true;
    }

    /** Native AiAvoidance Move goal driven by the restored monster stream. */
    private bool TryAdvanceAiAvoidanceMoveGoalFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance,
        int actionRoll)
    {
        var movementMinimumDistance = 4;
        var movementChanceModulo = 4;
        if (monsterTypes?.TryGet(monster.MonsterId, out var avoidanceDefinition) == true
            && avoidanceDefinition.AiAvoidanceMovementMinimumDistance > 0
            && avoidanceDefinition.AiAvoidanceMovementChanceModulo > 0) {
            movementMinimumDistance = avoidanceDefinition.AiAvoidanceMovementMinimumDistance;
            movementChanceModulo = avoidanceDefinition.AiAvoidanceMovementChanceModulo;
        }
        if (distance < 2) {
            monster.ClearAvoidanceMovement();
            return false;
        }

        var hasMoveGoal = monster.AvoidanceMovementTicks >= 0;
        var startsMoveGoal = distance >= movementMinimumDistance
            && random.FlipCoin((uint)movementChanceModulo);
        if (!hasMoveGoal && !startsMoveGoal)
            return false;

        if (!hasMoveGoal)
            monster.StartAvoidanceMovement(random.RandomIntLessThan(2));
        if (!monster.AdvanceAvoidanceMovement(
                distance,
                world.HasAiLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY),
                IsNativeAvoidanceDirectionAvailable(
                    monster,
                    player,
                    players)))
            return false;

        var roundWalkChanceBase = 16;
        var roundWalkChanceIntelligenceScale = 5;
        if (monsterTypes?.TryGet(monster.MonsterId, out var roundWalkDefinition) == true
            && roundWalkDefinition.AiAvoidanceRoundWalkChanceBase > 0
            && roundWalkDefinition.AiAvoidanceRoundWalkChanceIntelligenceScale > 0) {
            roundWalkChanceBase = roundWalkDefinition.AiAvoidanceRoundWalkChanceBase;
            roundWalkChanceIntelligenceScale = roundWalkDefinition.AiAvoidanceRoundWalkChanceIntelligenceScale;
        }
        var roundWalkChance = Math.Min(
            100,
            roundWalkChanceIntelligenceScale * (monster.Intelligence + roundWalkChanceBase));
        if (actionRoll >= roundWalkChance)
            return true;

        var roundWalk = FindNativeStatefulRoundWalkStepFromRandom(
            monster,
            player,
            players,
            monster.AvoidanceMovementTurn,
            out var nextTurn);
        monster.UpdateAvoidanceMovementTurn(nextTurn);
        if (roundWalk.HasValue) {
            monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
            monster.RecordAvoidanceMovement(
                roundWalk.Value.X - monster.PositionX,
                roundWalk.Value.Y - monster.PositionY);
        } else {
            monster.StartAvoidanceDelay(ResolveAvoidanceDelayFromRandom(monster));
        }
        return true;
    }

    /** Native AiAvoidance ordinary movement admission across var2 bands. */
    private static int GetAiAvoidanceMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.AvoidanceMovementIdleTicks == -1
            ? longIdleChance
            : monster.AvoidanceMovementIdleTicks > 20
                ? longIdleChance
                : monster.AvoidanceMovementIdleTicks == 0
                    && monster.AvoidanceMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native AiAvoidance fresh-mode bonus with a compatibility fallback for older packs. */
    private int GetAiAvoidanceMovementFreshChanceBonus(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.AiAvoidanceMovementFreshChanceBonus > 0)
            return definition.AiAvoidanceMovementFreshChanceBonus;
        return 50;
    }

    private bool IsNativeAvoidanceDirectionAvailable(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(monster.PositionX + displacement.X);
        var candidateY = checked(monster.PositionY + displacement.Y);
        return world.IsWalkable(monster.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                monster.LevelId,
                candidateX,
                candidateY,
                players,
                monster.EntityId);
    }

    /** Native SnakeAi's six-step lateral drift and three-candidate RandomWalk2 fallback. */
    private (int X, int Y)? FindSnakeDriftStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        IReadOnlyList<int>? configuredPattern)
    {
        var targetDirection = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var pattern = configuredPattern is { Count: > 0 }
            ? configuredPattern
            : SnakeDriftPattern;
        var patternIndex = (int)((seed / 100U) % (uint)pattern.Count);
        var direction = RotateDirection(
            targetDirection,
            pattern[patternIndex]);
        var driftDisplacement = DirectionDisplacements[direction];
        var driftX = checked(monster.PositionX + driftDisplacement.X);
        var driftY = checked(monster.PositionY + driftDisplacement.Y);
        if (world.IsWalkable(monster.LevelId, driftX, driftY)
            && !world.IsOccupied(
                monster.LevelId,
                driftX,
                driftY,
                players,
                monster.EntityId))
            return (driftX, driftY);

        // Native RandomWalk2 starts from monster.direction (the unmodified
        // target direction) and only considers straight, left, and right.
        var fallbackTurn = (seed / 600U) % 2U == 0 ? 1 : -1;
        var targetDirections = DeterministicGridDirections.EnumerateStraightAndLateral(
            targetDirection,
            fallbackTurn);
        foreach (var candidateDirection in targetDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native Snake RandomWalk2 fallback driven by the restored stream. */
    private (int X, int Y)? FindSnakeDriftStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        IReadOnlyList<int>? configuredPattern)
    {
        var targetDirection = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var pattern = configuredPattern is { Count: > 0 }
            ? configuredPattern
            : SnakeDriftPattern;
        var patternIndex = (int)((seed / 100U) % (uint)pattern.Count);
        var direction = RotateDirection(
            targetDirection,
            pattern[patternIndex]);
        var drift = FindStepInDirection(monster, direction, players);
        if (drift.HasValue)
            return drift;

        // Native RandomWalk2 consumes one coin to order the two lateral
        // candidates after its straight candidate is unavailable.
        var fallbackTurn = random.FlipCoin(2) ? 1 : -1;
        foreach (var candidateDirection in DeterministicGridDirections.EnumerateStraightAndLateral(
                     targetDirection,
                     fallbackTurn)) {
            var step = FindStepInDirection(monster, candidateDirection, players);
            if (step.HasValue)
                return step;
        }
        return null;
    }

    /** Native Familiar uses Bat movement at range and emits Lightning after melee. */
    private bool TryAdvanceFamiliarBatAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)
            || !IsFamiliarLightningMissile(missile)
            || distance <= 1)
            return false;

        return TryAdvanceBatMovementFallback(
            monster,
            player,
            players,
            unchecked((uint)tick ^ monster.EntityId ^ player.EntityId),
            distance);
    }

    private static bool IsFamiliarLightningMissile(
        AuthoritativeMonsterMissileDefinition missile)
        => missile.IsPostMeleeMissile
            || missile.MonsterId != 0
            && missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic
            && missile.MissileId == "Lightning";

    /** Captures Familiar's validated GenerateRnd damage bound before travel. */
    private void CreateFamiliarLightningProjectile(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        AuthoritativeMonsterMissileDefinition missile)
    {
        var speed = missile.ProjectileSpeed > 0 ? missile.ProjectileSpeed : 16;
        var lifetime = missile.ProjectileLifetime > 0 ? missile.ProjectileLifetime : 2;
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            speed);
        var damageRandomRange = missile.ProjectileDamageRandomRange > 0
            ? missile.ProjectileDamageRandomRange
            : 10;
        var damage = missile.ProjectileDamage > 0
            ? missile.ProjectileDamage
            : random.RandomIntBetween(1, damageRandomRange);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            missile.SpellId != 0 ? missile.SpellId : 3,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            missile.DamageType ?? AuthoritativeDamageType.Lightning,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.MonsterMagic,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
    }

    /** Native Bat-family charge admitted by a monster-specific missile row. */
    private bool TryAdvanceMappedChargeAi(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)) {
            if (distance < 2 || !UsesNativeAi(monster, "Bat"))
                return false;
            return TryAdvanceBatMovementFallback(
                monster,
                player,
                players,
                unchecked((uint)tick ^ monster.EntityId ^ player.EntityId),
                distance);
        }
        if (missile.ProjectileBehavior != AuthoritativeProjectileBehavior.BatCharge)
            return false;

        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == AuthoritativeProjectileBehavior.BatCharge))
            return true;

        // Adjacent Bat attacks use the shared melee admission path below; the
        // mapped charge controller owns only ranged ticks.
        if (distance < 2)
            return false;

        var chargeTuning = missile.ChargeMinimumDistance > 0
            && missile.ChargeChanceBase > 0
            && missile.ChargeChanceIntelligenceScale > 0
            ? (
                MinimumDistance: missile.ChargeMinimumDistance,
                ChanceBase: missile.ChargeChanceBase,
                IntelligenceScale: missile.ChargeChanceIntelligenceScale)
            : (MinimumDistance: 5, ChanceBase: 33, IntelligenceScale: 4);
        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(attackSeed % 100U);
        if (distance < chargeTuning.MinimumDistance)
            return TryAdvanceBatMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                actionRoll);

        var chargeChance = Math.Min(
            100,
            chargeTuning.ChanceBase
                + (chargeTuning.IntelligenceScale * monster.Intelligence));
        if (actionRoll >= chargeChance
            || !world.HasAiLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY)) {
            return TryAdvanceBatMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                actionRoll);
        }

        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var chargeProjectileTuning = ResolveMappedChargeProjectileTuning(
            missile,
            monster.Intelligence);
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            chargeProjectileTuning.Speed);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            missile.SpellId != 0 ? missile.SpellId : 1,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            0,
            missile.DamageType ?? AuthoritativeDamageType.Physical,
            0,
            checked((uint)chargeProjectileTuning.Lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.BatCharge,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = chargeProjectileTuning.Speed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
        return true;
    }

    /** Native BatAi movement after its Gloom charge branch does not fire. */
    private bool TryAdvanceBatMovementFallback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int distance,
        int? actionRoll = null)
    {
        if (distance < 2)
            return false;

        // BatAi uses the native var1/var2 movement state: after a long idle
        // animation it admits intelligence + 13, while a fresh movement mode
        // with var2 == 0 admits intelligence + 63. Legacy hand-built targets
        // without that state retain the earlier standing-tick boundary once.
        var movementTuning = GetBatMovementTuning(monster);
        var movementChance = monster.BatMovementIdleTicks == -1
            ? movementTuning.ChanceBase
                + movementTuning.IntelligenceScale * monster.Intelligence
                + movementTuning.FreshChanceBonus
            : monster.BatMovementIdleTicks > 20
                ? movementTuning.ChanceBase
                    + movementTuning.IntelligenceScale * monster.Intelligence
                : monster.BatMovementIdleTicks == 0
                    && monster.BatMovementMode is >= 1 and <= 3
                    ? movementTuning.ChanceBase
                        + movementTuning.IntelligenceScale * monster.Intelligence
                        + movementTuning.FreshChanceBonus
                    : 0;
        movementChance = Math.Min(100, movementChance);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var movementRoll = usesNativeAiStream
            ? actionRoll ?? random.RandomIntLessThan(100)
            : (int)(seed % 100U);
        if (movementRoll >= movementChance) {
            monster.AdvanceBatMovementIdle();
            return true;
        }

        var randomWalk = usesNativeAiStream
            ? FindNativeRandomWalkStepFromRandom(monster, player, players)
            : FindNativeRandomWalkStep(monster, player, players, seed);
        if (randomWalk.HasValue) {
            monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
            monster.RecordBatMovement(
                randomWalk.Value.X - monster.PositionX,
                randomWalk.Value.Y - monster.PositionY);
        } else {
            monster.AdvanceBatMovementIdle();
        }
        return true;
    }

    private (int ChanceBase, int IntelligenceScale, int FreshChanceBonus)
        GetBatMovementTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.BatMovementChanceBase > 0
            && definition.BatMovementChanceIntelligenceScale > 0
            && definition.BatMovementFreshChanceBonus > 0) {
            return (
                definition.BatMovementChanceBase,
                definition.BatMovementChanceIntelligenceScale,
                definition.BatMovementFreshChanceBonus);
        }
        return (13, 1, 50);
    }

    private (int X, int Y)? FindGoatRangedRetreatDestination(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int currentDistance)
    {
        var candidates = Enumerable.Range(-1, 3)
            .SelectMany(offsetY => Enumerable.Range(-1, 3)
                .Select(offsetX => (
                    X: monster.PositionX + offsetX,
                    Y: monster.PositionY + offsetY)))
            .Where(candidate => candidate.X != monster.PositionX
                || candidate.Y != monster.PositionY)
            .Where(candidate => world.IsWalkable(
                monster.LevelId,
                candidate.X,
                candidate.Y))
            .Where(candidate => !world.IsOccupied(
                monster.LevelId,
                candidate.X,
                candidate.Y,
                players,
                monster.EntityId))
            .Where(candidate => DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX,
                player.PositionY,
                candidate.X,
                candidate.Y) >= currentDistance)
            .OrderByDescending(candidate => DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX,
                player.PositionY,
                candidate.X,
                candidate.Y))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .ToArray();
        return candidates.Length == 0 ? null : candidates[0];
    }

    /** Native AiRanged projectile dispatch for validated ranged missile mappings. */
    private bool TryAdvanceMappedRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        if (!TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior is not (
                AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.MonsterMagic
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.InfernoController
                or AuthoritativeProjectileBehavior.ApocalypseController
                or AuthoritativeProjectileBehavior.Acid))
            return false;
        if (IsFamiliarLightningMissile(missile))
            return false;

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var usesNativeAiStream = missile.ProjectileBehavior
                == AuthoritativeProjectileBehavior.InfernoController
            && UsesNativeAi(monster, "Mega")
            && monster.MonsterAiSeed != 0;
        var usesNativeRangedAvoidanceAiStream = missile.AttackMode
                == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
            && monster.MonsterAiSeed != 0;
        // Zhar and Lazarus use the native Counselor controller even though
        // their runtime identities are distinct aliases. Keep the mapped
        // projectile's delay/RNG branch aligned with that controller.
        var isCounselor = UsesNativeCounselorAi(monster);
        var usesNativeRangedAiStream = missile.AttackMode
                == AuthoritativeMonsterRangedAttackMode.Ranged
            && !isCounselor
            && monster.MonsterAiSeed != 0;
        var usesNativeCounselorAiStream = isCounselor
            && monster.MonsterAiSeed != 0;
        if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController
            && UsesNativeAi(monster, "Mega")
            && monster.AdvanceMegaDelay())
            return true;
        if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
            && missile.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController
            && !isCounselor
            && monster.AdvanceRangedDelay())
            return true;
        if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
            && monster.AdvanceRangedAvoidanceDelay())
            return true;
        if (world.Projectiles.Values.Any(projectile =>
            {
                if (projectile.SourceEntityId != monster.EntityId)
                    return false;
                if (projectile.Behavior == missile.ProjectileBehavior)
                    return true;
                if (missile.ProjectileBehavior
                    == AuthoritativeProjectileBehavior.ApocalypseController
                    && projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseBoom)
                    return true;
                return missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                    && (projectile.Behavior == AuthoritativeProjectileBehavior.AcidSplat
                        || projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle);
            }))
            return true;
        var rangedAvoidanceUsesRoundWalk = false;
        var rangedAvoidanceRoundWalkTurn = -1;
        if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController) {
            var infernoTuning = ResolveMonsterInfernoTuning(missile);
            if (distance >= infernoTuning.MaximumDistance)
                return false;
            var infernoActionRoll = usesNativeAiStream
                ? random.RandomIntLessThan(100)
                : (int)(attackSeed % 100U);
            if (distance <= 1) {
                var adjacentChance = checked(
                    infernoTuning.AdjacentChanceBase
                    + infernoTuning.AdjacentChanceIntelligenceScale
                        * monster.Intelligence);
                if (infernoActionRoll >= Math.Min(100, adjacentChance)
                    || (usesNativeAiStream
                        ? !random.FlipCoin((uint)infernoTuning.AdjacentChoiceModulo)
                        : (attackSeed / 100U)
                            % (uint)infernoTuning.AdjacentChoiceModulo != 0))
                    return false;
            } else {
                var attackBand = checked(
                    infernoTuning.RangedChanceBase
                    + infernoTuning.RangedChanceIntelligenceScale
                        * (monster.Intelligence
                            + (distance >= infernoTuning.RangedFarDistance
                                ? infernoTuning.RangedFarIntelligenceOffset
                                : 0)));
                if (infernoActionRoll >= Math.Min(100, attackBand)) {
                    // MegaAi's normal branch may RandomWalk after a missed
                    // Inferno roll. Consume that branch here so the target
                    // does not fall through to generic pursuit.
                    if (UsesNativeAi(monster, "Mega")) {
                        TryAdvanceMegaIntermediateMovement(
                            monster,
                            player,
                            players,
                            attackSeed,
                            distance,
                            usesNativeAiStream);
                        monster.StartMegaDelay(
                            usesNativeAiStream
                                ? ResolveMegaDelayFromRandom(missile)
                                : ResolveMegaDelay(missile, attackSeed));
                        return true;
                    }
                    return false;
                }
            }
        } else if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance) {
            // Legacy rows retain the native AiRangedAvoidance values while
            // validated content progressively owns this admission boundary.
            var minimumDistance = missile.RangedAvoidanceMinimumDistance == 0
                ? 2
                : missile.RangedAvoidanceMinimumDistance;
            var farDistance = missile.RangedAvoidanceFarDistance == 0
                ? 3
                : missile.RangedAvoidanceFarDistance;
            var chanceBase = missile.RangedAvoidanceChanceBase == 0
                ? 500
                : missile.RangedAvoidanceChanceBase;
            var chanceIntelligenceScale = missile.RangedAvoidanceChanceIntelligenceScale == 0
                ? 500
                : missile.RangedAvoidanceChanceIntelligenceScale;
            var farIntelligenceOffset = missile.RangedAvoidanceFarIntelligenceOffset;
            if (missile.RangedAvoidanceMinimumDistance == 0
                && missile.RangedAvoidanceFarDistance == 0
                && missile.RangedAvoidanceChanceBase == 0
                && missile.RangedAvoidanceChanceIntelligenceScale == 0
                && missile.RangedAvoidanceFarIntelligenceOffset == 0)
                farIntelligenceOffset = 1;
            var rangedAvoidanceActionRoll = usesNativeRangedAvoidanceAiStream
                ? random.RandomIntLessThan(10_000)
                : (int)(attackSeed % 10_000U);
            if (distance < minimumDistance) {
                monster.ClearRangedAvoidanceMovement();
                // Native AiRangedAvoidance falls back to a probabilistic
                // adjacent melee attack instead of admitting every shared
                // melee tick. Keep failed native rolls out of that fallback;
                // successful rolls continue into the authoritative melee
                // resolver below.
                if (distance <= 1) {
                    // Validated rows own the divisor; omitted rows retain the
                    // native Acid half-band compatibility behavior.
                    var adjacentAttackChanceDivisor = missile
                        .RangedAvoidanceAdjacentAttackChanceDivisor > 0
                        ? missile.RangedAvoidanceAdjacentAttackChanceDivisor
                        : missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                            ? 2
                            : 1;
                    var adjacentAttackChanceBase = missile
                        .RangedAvoidanceAdjacentAttackChanceBase > 0
                        ? missile.RangedAvoidanceAdjacentAttackChanceBase
                        : 6_000;
                    var adjacentAttackChanceIntelligenceScale = missile
                        .RangedAvoidanceAdjacentAttackChanceIntelligenceScale > 0
                        ? missile.RangedAvoidanceAdjacentAttackChanceIntelligenceScale
                        : 1_000;
                    var adjacentAttackBand = checked(
                        adjacentAttackChanceBase
                        + adjacentAttackChanceIntelligenceScale * monster.Intelligence);
                    adjacentAttackBand /= adjacentAttackChanceDivisor;
                    if (rangedAvoidanceActionRoll
                        >= (uint)Math.Min(10_000, adjacentAttackBand))
                        return true;
                } else {
                    var moved = TryAdvanceRangedAvoidanceMovement(
                        monster,
                        player,
                        players,
                        attackSeed,
                        missile,
                        useRoundWalk: false,
                        usesNativeAiStream: usesNativeRangedAvoidanceAiStream);
                    if (!moved)
                        monster.StartRangedAvoidanceDelay(
                            usesNativeRangedAvoidanceAiStream
                                ? ResolveRangedAvoidanceDelayFromRandom(missile)
                                : ResolveRangedAvoidanceDelay(missile, attackSeed));
                    return true;
                }
                return false;
            }
            // Native AiRangedAvoidance enters its far-distance Move goal only
            // on the configured far boundary and a one-in-four (one-in-eight
            // Acid) admission. Once entered, goalVar1/goalVar2 persist across
            // ticks and saves until the native movement window completes.
            var lessMissiles = missile.ProjectileBehavior
                == AuthoritativeProjectileBehavior.Acid ? 1 : 0;
            var movementChanceModulo = missile.RangedAvoidanceMovementChanceModulo == 0
                ? 4 << lessMissiles
                : missile.RangedAvoidanceMovementChanceModulo;
            var hasMoveGoal = monster.RangedAvoidanceMoveTicks >= 0;
            var startsMoveGoal = distance >= farDistance
                && (usesNativeRangedAvoidanceAiStream
                    ? random.FlipCoin((uint)movementChanceModulo)
                    : attackSeed % (uint)movementChanceModulo == 0);
            if (hasMoveGoal || startsMoveGoal) {
                if (!hasMoveGoal)
                    monster.StartRangedAvoidanceMovement(
                        usesNativeRangedAvoidanceAiStream
                            ? random.RandomIntLessThan(2)
                            : (int)((attackSeed / 100U) % 2U));
                if (monster.AdvanceRangedAvoidanceMovement(
                        distance,
                        IsNativeRangedAvoidanceDirectionAvailable(
                            monster,
                            player,
                            players))) {
                    rangedAvoidanceUsesRoundWalk = true;
                    rangedAvoidanceRoundWalkTurn = monster.RangedAvoidanceTurn;
                }
            }
            var attackBand = checked(
                chanceBase + chanceIntelligenceScale * (monster.Intelligence
                    + (rangedAvoidanceUsesRoundWalk
                        ? 0
                        : distance >= farDistance
                            ? farIntelligenceOffset
                            : 0)));
            attackBand = checked(
                attackBand * missile.AvoidanceProbabilityPercent / 100);
            if (rangedAvoidanceActionRoll
                >= (uint)Math.Min(10_000, attackBand)) {
                var moved = TryAdvanceRangedAvoidanceMovement(
                    monster,
                    player,
                    players,
                    attackSeed,
                    missile,
                    rangedAvoidanceUsesRoundWalk,
                    rangedAvoidanceRoundWalkTurn,
                    usesNativeRangedAvoidanceAiStream);
                if (!moved)
                    monster.StartRangedAvoidanceDelay(
                        usesNativeRangedAvoidanceAiStream
                            ? ResolveRangedAvoidanceDelayFromRandom(missile)
                            : ResolveRangedAvoidanceDelay(missile, attackSeed));
                return true;
            }
        } else {
            var retreatTuning = ResolveRangedRetreatTuning(missile);
            var retreatChance = checked(
                retreatTuning.ChanceBase
                + retreatTuning.IntelligenceScale * monster.Intelligence);
            if (distance < retreatTuning.MinimumDistance) {
                var retreatRoll = usesNativeRangedAiStream
                    ? random.RandomIntLessThan(100)
                    : (int)(attackSeed % 100U);
                if (retreatRoll < Math.Min(100, retreatChance)) {
                    var retreat = usesNativeRangedAiStream
                        ? FindNativeRandomWalkStepFromRandom(
                            monster,
                            RotateDirection(
                                GetDirection(
                                    monster.PositionX,
                                    monster.PositionY,
                                    player.PositionX,
                                    player.PositionY),
                                4),
                            players)
                        : FindGoatRangedRetreatDestination(
                            monster,
                            player,
                            players,
                            distance);
                    if (retreat.HasValue) {
                        monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
                        return true;
                    }
                }
            }
        }

        if (!world.HasMissileLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY)) {
            if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance) {
                var moved = TryAdvanceRangedAvoidanceMovement(
                    monster,
                    player,
                    players,
                    attackSeed,
                    missile,
                    rangedAvoidanceUsesRoundWalk,
                    rangedAvoidanceRoundWalkTurn,
                    usesNativeRangedAvoidanceAiStream);
                if (!moved)
                    monster.StartRangedAvoidanceDelay(
                        usesNativeRangedAvoidanceAiStream
                            ? ResolveRangedAvoidanceDelayFromRandom(missile)
                            : ResolveRangedAvoidanceDelay(missile, attackSeed));
                return true;
            }
            if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController
                && UsesNativeAi(monster, "Mega")) {
                TryAdvanceMegaIntermediateMovement(
                    monster,
                    player,
                    players,
                    attackSeed,
                    distance,
                    usesNativeAiStream);
                monster.StartMegaDelay(
                    usesNativeAiStream
                        ? ResolveMegaDelayFromRandom(missile)
                        : ResolveMegaDelay(missile, attackSeed));
                return true;
            }
            return missile.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController;
        }

        var spell = spells.Definitions.FirstOrDefault(definition =>
            definition.ProjectileBehavior == missile.ProjectileBehavior
                || missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterChargedBolt
                    && definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ChargedBolt);
        var spellId = missile.SpellId != 0
            ? missile.SpellId
            : spell?.SpellId
            ?? (missile.ProjectileBehavior switch {
                AuthoritativeProjectileBehavior.Fireball => 12U,
                AuthoritativeProjectileBehavior.Lightning => 3U,
                AuthoritativeProjectileBehavior.ApocalypseController => 25U,
                _ => 1U,
            });
        // Native monster Firebolt/Fireball use speed 16; Storm's
        // ThinLightningControl and Mega's InfernoControl use speed 32.
        // Player spell speed remains content-owned separately in spells.tsv.
        var speed = missile.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : missile.ProjectileBehavior switch {
                AuthoritativeProjectileBehavior.MonsterChargedBolt => 8,
                AuthoritativeProjectileBehavior.Lightning => 32,
                AuthoritativeProjectileBehavior.InfernoController => spell?.ProjectileSpeed > 0
                    ? spell.ProjectileSpeed
                    : 32,
                AuthoritativeProjectileBehavior.ApocalypseController => 0,
                _ => 16,
            };
        var lifetime = missile.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : missile.ProjectileLifetimeBase > 0
                || missile.ProjectileLifetimeIntelligenceScale > 0
                ? checked(
                    missile.ProjectileLifetimeBase
                    + missile.ProjectileLifetimeIntelligenceScale * monster.Intelligence)
                : missile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                    ? checked(5 * (monster.Intelligence + 4))
                    : spell?.ProjectileLifetime > 0 ? spell.ProjectileLifetime : 256;
        var impactLifetime = ResolveMonsterProjectileImpactLifetime(missile);
        var damageType = missile.DamageType
            ?? spell?.DamageType
            ?? (missile.MissileId == "MagmaBall"
                ? AuthoritativeDamageType.Fire
                : missile.MissileId == "Acid"
                    ? AuthoritativeDamageType.Acid
                    : missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic
                        ? AuthoritativeDamageType.Magic
                        : AuthoritativeDamageType.Fire);
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            speed);
        var projectileCount = missile.ProjectileBehavior
            == AuthoritativeProjectileBehavior.MonsterChargedBolt
            ? missile.ProjectileCount > 0 ? missile.ProjectileCount : 3
            : 1;
        var projectileDamage = missile.ProjectileDamage > 0
            ? missile.ProjectileDamage
            : missile.ProjectileBehavior switch {
                // Native AddChargedBolt assigns monster-fired bolts a fixed
                // 15-point payload; validated content may override it.
                AuthoritativeProjectileBehavior.MonsterChargedBolt => 15,
                AuthoritativeProjectileBehavior.Lightning => 0,
                // Native AddFirebolt and AddGenericMagicMissile roll the
                // monster range while constructing the missile, rather than
                // deriving the payload from the AI admission seed.
                AuthoritativeProjectileBehavior.Firebolt
                    or AuthoritativeProjectileBehavior.MonsterMagic
                    => random.RandomIntBetween(
                        GetEffectiveMonsterDamageRange(monster, special: false).Minimum,
                        GetEffectiveMonsterDamageRange(monster, special: false).Maximum),
                _ => SelectEffectiveMonsterDamage(
                    monster,
                    special: false,
                    seed: attackSeed),
            };
        for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++) {
            var projectile = missile.ProjectileBehavior
                == AuthoritativeProjectileBehavior.InfernoController
                ? CreateMonsterInfernoController(
                    AllocateProjectileEntityId(),
                    monster,
                    player,
                    spellId,
                    speed,
                    lifetime,
                    impactLifetime,
                    damageType,
                    missile.ProjectileDamage)
                : new AuthoritativeProjectile(
                    AllocateProjectileEntityId(),
                    monster.EntityId,
                    player.EntityId,
                    spellId,
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY,
                    projectileDamage,
                    damageType,
                    0,
                    checked((uint)lifetime)) {
                    Behavior = missile.ProjectileBehavior,
                    NativeMissileId = missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterMagic ? missile.MissileId : null,
                    VelocityX = velocityX,
                    VelocityY = velocityY,
                    OriginalDirection = direction,
                    Speed = speed,
                    ImpactLifetime = impactLifetime,
                    ResistantDamageDivisor = 1,
                };
            if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.MonsterChargedBolt) {
                var pathRandomRange = spell?.ChargedBoltPathRandomRange > 0
                    ? spell.ChargedBoltPathRandomRange
                    : 15;
                projectile.PathIndex = random.GenerateRnd(pathRandomRange) + 1;
                // Native AddChargedBolt also rolls a presentation frame.
                _ = random.RandomIntBetween(1, 8);
            }
            var initialTravelQuanta = missile.InitialTravelQuanta > 0
                ? missile.InitialTravelQuanta
                : missile.MissileId == "MagmaBall" ? 3 : 0;
            if (initialTravelQuanta > 0) {
                // Older mappings retain the native MagmaBall compatibility fallback;
                // shipped rows declare the same advance through content.
                projectile.TraveledX = checked(projectile.VelocityX * initialTravelQuanta);
                projectile.TraveledY = checked(projectile.VelocityY * initialTravelQuanta);
                var initialTile = CalculateMissileTile(
                    projectile.StartX,
                    projectile.StartY,
                    projectile.TraveledX,
                    projectile.TraveledY);
                projectile.PositionX = initialTile.X;
                projectile.PositionY = initialTile.Y;
            }
            if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController) {
                projectile.VelocityX = velocityX;
                projectile.VelocityY = velocityY;
            }
            world.Projectiles.Add(projectile.EntityId, projectile);
        }
        if (isCounselor)
            monster.StartCounselorDelay(
                usesNativeCounselorAiStream
                    ? ResolveCounselorAttackDelayFromRandom(monster)
                    : ResolveCounselorAttackDelay(monster, attackSeed));
        else if (missile.AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
            && missile.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController)
            monster.StartRangedDelay(
                usesNativeRangedAiStream
                    ? ResolveRangedDelayFromRandom(missile)
                    : ResolveRangedDelay(missile, attackSeed));
        else if (missile.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController
            && UsesNativeAi(monster, "Mega"))
            monster.StartMegaDelay(
                usesNativeAiStream
                    ? ResolveMegaDelayFromRandom(missile)
                    : ResolveMegaDelay(missile, attackSeed));
        return true;
    }

    private static (int MinimumDistance, int ChanceBase, int IntelligenceScale)
        ResolveRangedRetreatTuning(AuthoritativeMonsterMissileDefinition? missile)
    {
        // Legacy rows retain the native four-tile and 70 + 10 * intelligence
        // values while validated content progressively owns this boundary.
        return (
            missile?.RangedRetreatMinimumDistance > 0
                ? missile.RangedRetreatMinimumDistance
                : 4,
            missile?.RangedRetreatChanceBase > 0
                ? missile.RangedRetreatChanceBase
                : 70,
            missile?.RangedRetreatChanceIntelligenceScale > 0
                ? missile.RangedRetreatChanceIntelligenceScale
                : 10);
    }

    private static int ResolveRangedDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint attackSeed)
    {
        var randomRange = missile?.RangedDelayRandomRange > 0
            ? missile.RangedDelayRandomRange
            : 20;
        var delayBase = missile?.RangedDelayBase ?? 0;
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native AiRanged post-shot delay driven by the restored per-monster stream. */
    private int ResolveRangedDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.RangedDelayRandomRange > 0
            ? missile.RangedDelayRandomRange
            : 20;
        var delayBase = missile?.RangedDelayBase ?? 0;
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static int ResolveRangedAvoidanceDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint attackSeed)
    {
        var randomRange = missile?.RangedAvoidanceDelayRandomRange > 0
            ? missile.RangedAvoidanceDelayRandomRange
            : 10;
        var delayBase = missile?.RangedAvoidanceDelayBase > 0
            ? missile.RangedAvoidanceDelayBase
            : 5;
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native AiRangedAvoidance recovery delay driven by the restored stream. */
    private int ResolveRangedAvoidanceDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.RangedAvoidanceDelayRandomRange > 0
            ? missile.RangedAvoidanceDelayRandomRange
            : 10;
        var delayBase = missile?.RangedAvoidanceDelayBase > 0
            ? missile.RangedAvoidanceDelayBase
            : 5;
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static int ResolveHorkDemonDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint actionSeed)
    {
        var randomRange = missile?.HorkDemonDelayRandomRange > 0
            ? missile.HorkDemonDelayRandomRange
            : 10;
        var delayBase = missile?.HorkDemonDelayBase > 0
            ? missile.HorkDemonDelayBase
            : 10;
        return checked(delayBase + (int)((actionSeed / 200U) % (uint)randomRange));
    }

    /** Native HorkDemon recovery delay driven by the restored per-monster stream. */
    private int ResolveHorkDemonDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.HorkDemonDelayRandomRange > 0
            ? missile.HorkDemonDelayRandomRange
            : 10;
        var delayBase = missile?.HorkDemonDelayBase > 0
            ? missile.HorkDemonDelayBase
            : 10;
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static int ResolveSnakeDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint actionSeed,
        int intelligence,
        bool adjacent)
    {
        var baseDelay = adjacent
            ? missile?.SnakeAdjacentDelayBase > 0
                ? missile.SnakeAdjacentDelayBase
                : 10
            : missile?.SnakeDistantDelayBase > 0
                ? missile.SnakeDistantDelayBase
                : 15;
        var intelligenceScale = adjacent
            ? missile?.SnakeAdjacentDelayIntelligenceScale > 0
                ? missile.SnakeAdjacentDelayIntelligenceScale
                : 1
            : missile?.SnakeDistantDelayIntelligenceScale > 0
                ? missile.SnakeDistantDelayIntelligenceScale
                : 1;
        var randomRange = adjacent
            ? missile?.SnakeAdjacentDelayRandomRange > 0
                ? missile.SnakeAdjacentDelayRandomRange
                : 10
            : missile?.SnakeDistantDelayRandomRange > 0
                ? missile.SnakeDistantDelayRandomRange
                : 10;
        return Math.Max(
            0,
            checked(baseDelay - intelligenceScale * intelligence
                + (int)((actionSeed / 100U) % (uint)randomRange)));
    }

    /** Native Snake delay roll driven by the restored per-monster stream. */
    private int ResolveSnakeDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile,
        int intelligence,
        bool adjacent)
    {
        var baseDelay = adjacent
            ? missile?.SnakeAdjacentDelayBase > 0
                ? missile.SnakeAdjacentDelayBase
                : 10
            : missile?.SnakeDistantDelayBase > 0
                ? missile.SnakeDistantDelayBase
                : 15;
        var intelligenceScale = adjacent
            ? missile?.SnakeAdjacentDelayIntelligenceScale > 0
                ? missile.SnakeAdjacentDelayIntelligenceScale
                : 1
            : missile?.SnakeDistantDelayIntelligenceScale > 0
                ? missile.SnakeDistantDelayIntelligenceScale
                : 1;
        var randomRange = adjacent
            ? missile?.SnakeAdjacentDelayRandomRange > 0
                ? missile.SnakeAdjacentDelayRandomRange
                : 10
            : missile?.SnakeDistantDelayRandomRange > 0
                ? missile.SnakeDistantDelayRandomRange
                : 10;
        return Math.Max(
            0,
            checked(baseDelay - intelligenceScale * intelligence
                + random.RandomIntLessThan(randomRange)));
    }

    private int ResolveAvoidanceDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.AvoidanceDelayRandomRange > 0) {
            delayBase = definition.AvoidanceDelayBase > 0
                ? definition.AvoidanceDelayBase
                : delayBase;
            randomRange = definition.AvoidanceDelayRandomRange;
        }
        return checked(delayBase + (int)((actionSeed / 200U) % (uint)randomRange));
    }

    private int ResolveAvoidanceDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.AvoidanceDelayRandomRange > 0) {
            delayBase = definition.AvoidanceDelayBase > 0
                ? definition.AvoidanceDelayBase
                : delayBase;
            randomRange = definition.AvoidanceDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private int ResolveCounselorDelay(
        AuthoritativeCombatTarget target,
        uint actionSeed)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorDelayRandomRange > 0) {
            delayBase = definition.CounselorDelayBase > 0
                ? definition.CounselorDelayBase
                : delayBase;
            intelligenceScale = definition.CounselorDelayIntelligenceScale > 0
                ? definition.CounselorDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.CounselorDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + (int)((actionSeed / 200U) % (uint)randomRange)));
    }

    /** Native Counselor no-action delay driven by the restored stream. */
    private int ResolveCounselorDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var intelligenceScale = 2;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorDelayRandomRange > 0) {
            delayBase = definition.CounselorDelayBase > 0
                ? definition.CounselorDelayBase
                : delayBase;
            intelligenceScale = definition.CounselorDelayIntelligenceScale > 0
                ? definition.CounselorDelayIntelligenceScale
                : intelligenceScale;
            randomRange = definition.CounselorDelayRandomRange;
        }
        return Math.Max(
            0,
            checked(delayBase - (intelligenceScale * target.Intelligence)
                + random.RandomIntLessThan(randomRange)));
    }

    private int ResolveCounselorAttackDelay(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        var delayBase = 5;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorAttackDelayRandomRange > 0) {
            delayBase = definition.CounselorAttackDelayBase > 0
                ? definition.CounselorAttackDelayBase
                : delayBase;
            randomRange = definition.CounselorAttackDelayRandomRange;
        }
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native Counselor attack delay driven by the restored stream. */
    private int ResolveCounselorAttackDelayFromRandom(
        AuthoritativeCombatTarget target)
    {
        var delayBase = 5;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.CounselorAttackDelayRandomRange > 0) {
            delayBase = definition.CounselorAttackDelayBase > 0
                ? definition.CounselorAttackDelayBase
                : delayBase;
            randomRange = definition.CounselorAttackDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static int ResolveMegaDelay(
        AuthoritativeMonsterMissileDefinition? missile,
        uint attackSeed)
    {
        var randomRange = missile?.MegaDelayRandomRange > 0
            ? missile.MegaDelayRandomRange
            : 10;
        var delayBase = missile?.MegaDelayBase > 0
            ? missile.MegaDelayBase
            : 5;
        return checked(delayBase + (int)((attackSeed / 200U) % (uint)randomRange));
    }

    /** Native Mega recovery delay driven by the restored per-monster stream. */
    private int ResolveMegaDelayFromRandom(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        var randomRange = missile?.MegaDelayRandomRange > 0
            ? missile.MegaDelayRandomRange
            : 10;
        var delayBase = missile?.MegaDelayBase > 0
            ? missile.MegaDelayBase
            : 5;
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private static (int Speed, int Lifetime) ResolveMappedChargeProjectileTuning(
        AuthoritativeMonsterMissileDefinition missile,
        int intelligence)
    {
        var speed = missile.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : 18;
        var lifetime = missile.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : missile.ProjectileLifetimeBase > 0
                || missile.ProjectileLifetimeIntelligenceScale > 0
                ? checked(
                    missile.ProjectileLifetimeBase
                    + missile.ProjectileLifetimeIntelligenceScale * intelligence)
                : 256;
        return (speed, Math.Max(1, lifetime));
    }

    private static int ResolveMonsterProjectileImpactLifetime(
        AuthoritativeMonsterMissileDefinition? missile)
    {
        return missile?.ProjectileImpactLifetime > 0
            ? missile.ProjectileImpactLifetime
            : 1;
    }

    /** Native AiRangedAvoidance round-walk fallback for a failed ranged roll. */
    private bool TryAdvanceRangedAvoidanceMovement(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        AuthoritativeMonsterMissileDefinition missile,
        bool useRoundWalk,
        int roundWalkTurn = -1,
        bool usesNativeAiStream = false)
    {
        if (!useRoundWalk) {
            var movementChanceBase = missile.RangedAvoidanceMovementChanceBase == 0
                ? 50
                : missile.RangedAvoidanceMovementChanceBase;
            var movementChanceIntelligenceScale =
                missile.RangedAvoidanceMovementChanceIntelligenceScale == 0
                    ? 10
                    : missile.RangedAvoidanceMovementChanceIntelligenceScale;
            var movementFreshChanceBonus = missile.RangedAvoidanceMovementFreshChanceBonus == 0
                ? 30
                : missile.RangedAvoidanceMovementFreshChanceBonus;
            var movementChance = GetRangedAvoidanceMovementChance(
                monster,
                movementChanceBase,
                movementChanceIntelligenceScale,
                movementFreshChanceBonus);
            var movementRoll = usesNativeAiStream
                ? random.RandomIntLessThan(100)
                : (int)((seed / 100U) % 100U);
            if (movementRoll >= (uint)movementChance) {
                monster.AdvanceRangedAvoidanceMovementIdle();
                return false;
            }
        }

        (int X, int Y)? movement;
        if (useRoundWalk && roundWalkTurn is 0 or 1) {
            movement = usesNativeAiStream
                ? FindNativeStatefulRoundWalkStepFromRandom(
                    monster,
                    player,
                    players,
                    roundWalkTurn,
                    out var nextTurn)
                : FindNativeStatefulRoundWalkStep(
                    monster,
                    player,
                    players,
                    seed,
                    roundWalkTurn,
                    out nextTurn);
            monster.UpdateRangedAvoidanceTurn(nextTurn);
        } else {
            movement = usesNativeAiStream
                ? FindNativeRandomWalkStepFromRandom(monster, player, players)
                : FindNativeRandomWalkStep(monster, player, players, seed);
        }
        if (!movement.HasValue) {
            if (!useRoundWalk)
                monster.AdvanceRangedAvoidanceMovementIdle();
            return false;
        }

        monster.BeginMovement(movement.Value.X, movement.Value.Y);
        monster.RecordRangedAvoidanceMovement(
            movement.Value.X - monster.PositionX,
            movement.Value.Y - monster.PositionY);
        return true;
    }

    /** Native OverlordAi movement admission across var2 bands. */
    private static int GetFatMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.FatMovementIdleTicks == -1
            ? longIdleChance
            : monster.FatMovementIdleTicks > 20
                ? longIdleChance
                : monster.FatMovementIdleTicks == 0
                    && monster.FatMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native Fat fresh-mode bonus with a compatibility fallback for older packs. */
    private int GetFatMovementFreshChanceBonus(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.FatMovementFreshChanceBonus > 0)
            return definition.FatMovementFreshChanceBonus;
        return 50;
    }

    /** Native AiRangedAvoidance ordinary movement admission across var2 bands. */
    private static int GetRangedAvoidanceMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.RangedAvoidanceMovementIdleTicks == -1
            ? longIdleChance
            : monster.RangedAvoidanceMovementIdleTicks > 20
                ? longIdleChance
                : monster.RangedAvoidanceMovementIdleTicks == 0
                    && monster.RangedAvoidanceMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native RoundWalk with persisted goalVar2 mutation. */
    private (int X, int Y)? FindNativeStatefulRoundWalkStep(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int turnState,
        out int nextTurnState)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var turn = turnState == 0 ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        foreach (var candidateDirection in primaryDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId)) {
                nextTurnState = turnState;
                return (candidateX, candidateY);
            }
        }

        // Native RoundWalk flips goalVar2 before calling RandomWalk in the
        // opposite direction. Keep the deterministic fallback order shared
        // with the existing RoundWalk approximation.
        nextTurnState = turnState == 0 ? 1 : 0;
        var fallbackDirection = RotateDirection(primaryDirections[0], 4);
        var fallbackTurn = (seed / 200U) % 2U == 0U ? 1 : -1;
        var fallbackDirections = DeterministicGridDirections.EnumerateTurnCandidates(
            fallbackDirection,
            fallbackTurn,
            2);
        foreach (var candidateDirection in fallbackDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Native RoundWalk fallback with restored RandomWalk coin ordering. */
    private (int X, int Y)? FindNativeStatefulRoundWalkStepFromRandom(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int turnState,
        out int nextTurnState)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var turn = turnState == 0 ? 1 : -1;
        var primaryDirections = DeterministicGridDirections.EnumerateRoundWalkPrimary(
            direction,
            turn);
        foreach (var candidateDirection in primaryDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId)) {
                nextTurnState = turnState;
                return (candidateX, candidateY);
            }
        }

        nextTurnState = turnState == 0 ? 1 : 0;
        var fallbackDirection = RotateDirection(primaryDirections[0], 4);
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        var fallbackDirections = DeterministicGridDirections.EnumerateTurnCandidates(
            fallbackDirection,
            firstTurn,
            secondTurn);
        foreach (var candidateDirection in fallbackDirections) {
            var displacement = DirectionDisplacements[candidateDirection];
            var candidateX = checked(monster.PositionX + displacement.X);
            var candidateY = checked(monster.PositionY + displacement.Y);
            if (world.IsWalkable(monster.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidateX,
                    candidateY,
                    players,
                    monster.EntityId))
                return (candidateX, candidateY);
        }
        return null;
    }

    private bool IsNativeRangedAvoidanceDirectionAvailable(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(monster.PositionX + displacement.X);
        var candidateY = checked(monster.PositionY + displacement.Y);
        return world.IsWalkable(monster.LevelId, candidateX, candidateY)
            && !world.IsOccupied(
                monster.LevelId,
                candidateX,
                candidateY,
                players,
                monster.EntityId);
    }

    private static (
        int MaximumDistance,
        int AdjacentChanceBase,
        int AdjacentChanceIntelligenceScale,
        int RangedFarDistance,
        int RangedChanceBase,
        int RangedChanceIntelligenceScale,
        int RangedFarIntelligenceOffset,
        int AdjacentChoiceModulo) ResolveMonsterInfernoTuning(
        AuthoritativeMonsterMissileDefinition missile)
    {
        var legacyDefaults = missile.InfernoMaximumDistance == 0
            && missile.InfernoAdjacentChanceBase == 0
            && missile.InfernoAdjacentChanceIntelligenceScale == 0
            && missile.InfernoRangedFarDistance == 0
            && missile.InfernoRangedChanceBase == 0
            && missile.InfernoRangedChanceIntelligenceScale == 0
            && missile.InfernoRangedFarIntelligenceOffset == 0
            && missile.InfernoAdjacentChoiceModulo == 0;
        return (
            missile.InfernoMaximumDistance > 0
                ? missile.InfernoMaximumDistance
                : 5,
            missile.InfernoAdjacentChanceBase > 0
                ? missile.InfernoAdjacentChanceBase
                : 40,
            missile.InfernoAdjacentChanceIntelligenceScale > 0
                ? missile.InfernoAdjacentChanceIntelligenceScale
                : 10,
            missile.InfernoRangedFarDistance > 0
                ? missile.InfernoRangedFarDistance
                : 3,
            missile.InfernoRangedChanceBase > 0
                ? missile.InfernoRangedChanceBase
                : 5,
            missile.InfernoRangedChanceIntelligenceScale > 0
                ? missile.InfernoRangedChanceIntelligenceScale
                : 5,
            legacyDefaults ? 1 : missile.InfernoRangedFarIntelligenceOffset,
            missile.InfernoAdjacentChoiceModulo > 0
                ? missile.InfernoAdjacentChoiceModulo
                : 2);
    }

    /** Native AddInfernoControl state for a monster-owned Mega projectile. */
    private AuthoritativeProjectile CreateMonsterInfernoController(
        uint projectileEntityId,
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        uint spellId,
        int speed,
        int lifetime,
        int impactLifetime,
        AuthoritativeDamageType damageType,
        int damage)
    {
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var targetX = player.PositionX;
        var targetY = player.PositionY;
        if (targetX == monster.PositionX && targetY == monster.PositionY) {
            var displacement = DirectionDisplacements[direction];
            targetX += displacement.X;
            targetY += displacement.Y;
        }
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            targetX,
            targetY,
            speed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            monster.EntityId,
            player.EntityId,
            spellId,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            damageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.InfernoController,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = impactLifetime,
            ResistantDamageDivisor = 1,
        };
    }

    /** Native Blood Star dispatch selected by a validated missile mapping. */
    private bool TryAdvanceSuccubusRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        AuthoritativeMonsterMissileDefinition? missile = null;
        if (monsterMissiles is not null) {
            if (!TryGetMonsterMissile(monster, out var mappedMissile)
                || mappedMissile.ProjectileBehavior
                    != AuthoritativeProjectileBehavior.BloodStar)
                return false;
            missile = mappedMissile;
        } else if (!UsesNativeAi(monster, "Succubus")
            && !UsesNativeAi(monster, "LazarusSuccubus")) {
            return false;
        }

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        if (monster.AdvanceRangedDelay())
            return true;
        if (world.Projectiles.Values.Any(projectile =>
                projectile.SourceEntityId == monster.EntityId
                && projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar))
            return true;
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var retreatTuning = ResolveRangedRetreatTuning(missile);
        var retreatChance = checked(
            retreatTuning.ChanceBase
            + retreatTuning.IntelligenceScale * monster.Intelligence);
        if (distance < retreatTuning.MinimumDistance) {
            var retreatRoll = usesNativeAiStream
                ? random.RandomIntLessThan(100)
                : (int)(attackSeed % 100U);
            if (retreatRoll < Math.Min(100, retreatChance)) {
                var retreat = usesNativeAiStream
                    ? FindNativeRandomWalkStepFromRandom(
                        monster,
                        RotateDirection(
                            GetDirection(
                                monster.PositionX,
                                monster.PositionY,
                                player.PositionX,
                                player.PositionY),
                            4),
                        players)
                    : FindGoatRangedRetreatDestination(monster, player, players, distance);
                if (retreat.HasValue) {
                    monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
                    return true;
                }
            }
        }

        if (!world.HasMissileLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY)) {
            return true;
        }

        var spell = spells.Definitions.FirstOrDefault(definition =>
            definition.ProjectileBehavior == AuthoritativeProjectileBehavior.BloodStar);
        var spellId = missile is { SpellId: > 0 }
            ? missile.SpellId
            : spell?.SpellId ?? 36U;
        var speed = missile?.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : spell?.ProjectileSpeed > 0
                ? spell.ProjectileSpeed
                : 16;
        var lifetime = missile?.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : spell?.ProjectileLifetime > 0
                ? spell.ProjectileLifetime
                : 256;
        var damageType = missile?.DamageType
            ?? spell?.DamageType
            ?? AuthoritativeDamageType.Magic;
        var direction = GetDirection(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var (velocityX, velocityY) = CalculateVelocity(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            speed);
        var damage = missile?.ProjectileDamage > 0
            ? missile.ProjectileDamage
            : SelectEffectiveMonsterDamage(
                monster,
                special: false,
                seed: attackSeed);
        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            spellId,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            damageType,
            0,
            checked((uint)lifetime)) {
            Behavior = AuthoritativeProjectileBehavior.BloodStar,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
        monster.StartRangedDelay(
            usesNativeAiStream
                ? ResolveRangedDelayFromRandom(missile)
                : ResolveRangedDelay(missile, attackSeed));
        return true;
    }

    /** Native Arrow admission captures the resolved hit before travel. */
    private bool CreateNativeRangedAttackProjectile(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        int distance,
        uint attackSeed,
        AuthoritativeMonsterMissileDefinition? missile = null)
    {
        missile ??= TryGetMonsterMissile(monster, out var mappedMissile)
            ? mappedMissile
            : null;
        var armorClass = player.ArmorClass;
        if (rules.GameVariant == DiabloGameVariant.Hellfire) {
            armorClass = AuthoritativeEquipmentIncomingDamageRules
                .ApplyHellfireMonsterClassArmor(
                    armorClass,
                    monster.IsDemon,
                    monster.IsUndead,
                    player.Equipment,
                    rules.HellfireArmorBonusAgainstDemons,
                    rules.HellfireArmorBonusAgainstUndead);
        }
        // Native ProcessArrow uses the monster range when it is non-fixed;
        // fixed rows retain a configured missile payload when one is present.
        var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
        var hasConfiguredDamage = missile is not null
            && missile.ProjectileDamage > 0
            && damageRange.Minimum == damageRange.Maximum;
        var baseDamage = hasConfiguredDamage
            ? missile!.ProjectileDamage
            : missile is not null && missile.ProjectileDamage == 0
                ? damageRange.Minimum == damageRange.Maximum
                    ? damageRange.Maximum
                    : monster.AttackDamage
                : damageRange.Maximum;
        var damage = rules.ResolveMonsterRangedAttackDamage(
            baseDamage,
            GetEffectiveMonsterToHit(monster, special: false),
            GetEffectiveMonsterLevel(monster),
            player.CharacterLevel,
            monster.LevelId,
            armorClass,
            distance,
            attackSeed);
        var projectileSpeed = missile?.ProjectileSpeed > 0
            ? missile.ProjectileSpeed
            : 1;
        var projectileLifetime = missile?.ProjectileLifetime > 0
            ? missile.ProjectileLifetime
            : Math.Max(1, distance);
        var damageType = missile?.DamageType ?? AuthoritativeDamageType.Physical;

        var projectile = new AuthoritativeProjectile(
            AllocateProjectileEntityId(),
            monster.EntityId,
            player.EntityId,
            spellId: 0,
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY,
            damage,
            damageType,
            areaRadius: 0,
            checked((uint)projectileLifetime),
            allowZeroSpellId: true) {
            // The content row selects the native Arrow identity; the module
            // resolves both explicit MonsterArrow rows and legacy Targeted
            // overlays to this server-owned monster lifecycle.
            Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
            Speed = projectileSpeed,
            ImpactLifetime = ResolveMonsterProjectileImpactLifetime(missile),
            ResistantDamageDivisor = 1,
            DamageMinimum = hasConfiguredDamage
                ? 0
                : damageRange.Minimum,
            DamageMaximum = hasConfiguredDamage
                ? 0
                : damageRange.Maximum,
        };
        world.Projectiles.Add(projectile.EntityId, projectile);
        return true;
    }

    private bool HasActiveMonsterArrow(AuthoritativeCombatTarget monster)
        => world.Projectiles.Values.Any(projectile =>
            projectile.SourceEntityId == monster.EntityId
            && projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow);

    /** Native SkeletonBow AI retreats when close before considering its arrow. */
    private bool TryAdvanceSkeletonRangedRetreat(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        int distance)
    {
        var skeletonRangedTuning = GetSkeletonRangedTuning(monster);
        if (!skeletonRangedTuning.HasValue
            || distance >= skeletonRangedTuning.Value.RetreatMinimumDistance)
            return false;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var retreatChance = GetSkeletonRangedMovementChance(
            monster,
            skeletonRangedTuning.Value.RetreatChanceBase,
            skeletonRangedTuning.Value.RetreatChanceIntelligenceScale,
            skeletonRangedTuning.Value.MovementFreshChanceBonus);
        var movementRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)((actionSeed / 100U) % 100U);
        if (movementRoll >= retreatChance) {
            monster.AdvanceSkeletonRangedMovementIdle();
            return false;
        }

        var retreat = FindGoatRangedRetreatDestination(
            monster,
            player,
            players,
            distance);
        if (!retreat.HasValue) {
            monster.AdvanceSkeletonRangedMovementIdle();
            return false;
        }
        monster.BeginMovement(retreat.Value.X, retreat.Value.Y);
        monster.RecordSkeletonRangedMovement(
            retreat.Value.X - monster.PositionX,
            retreat.Value.Y - monster.PositionY);
        return true;
    }

    /** Native SkeletonBow close-range movement admission across var2 bands. */
    private static int GetSkeletonRangedMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.SkeletonRangedMovementIdleTicks == -1
            ? longIdleChance
            : monster.SkeletonRangedMovementIdleTicks > 20
                ? longIdleChance
                : monster.SkeletonRangedMovementIdleTicks == 0
                    && monster.SkeletonRangedMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    /** Native SkeletonBow AI keeps a clear-lane monster ranged-only. */
    private bool TryAdvanceSkeletonRangedAttack(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int distance)
    {
        var skeletonRangedTuning = GetSkeletonRangedTuning(monster);
        if (!skeletonRangedTuning.HasValue
            || !HasValidatedArrowMapping(monster)
            || GetEffectiveMonsterToHit(monster, special: false) <= 0
            || GetEffectiveMonsterDamageRange(monster, special: false).Maximum <= 0)
            return false;

        if (IsPlayerProtectedByFlash(player) || IsPlayerEtherealized(player))
            return true;

        if (HasActiveMonsterArrow(monster))
            return true;

        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        var attackChance = Math.Min(
            100,
            skeletonRangedTuning.Value.AttackChanceBase
                + (skeletonRangedTuning.Value.AttackChanceIntelligenceScale
                    * monster.Intelligence));
        if (distance >= skeletonRangedTuning.Value.RetreatMinimumDistance
            && usesNativeAiStream)
            _ = random.RandomIntLessThan(100);
        var attackRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(attackSeed % 100U);
        if (!world.HasAiLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY))
            // Native SkeletonBowAi does not pursue through a blocked arrow
            // lane. Its close-range retreat branch has already run above;
            // preserve the remaining no-op instead of generic navigation.
            return true;
        if (attackRoll >= attackChance)
            return true;

        return CreateNativeRangedAttackProjectile(
            monster,
            player,
            distance,
            attackSeed);
    }

    private (
        int RetreatMinimumDistance,
        int RetreatChanceBase,
        int RetreatChanceIntelligenceScale,
        int AttackChanceBase,
        int AttackChanceIntelligenceScale,
        int MovementFreshChanceBonus)? GetSkeletonRangedTuning(
            AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.SkeletonRangedRetreatMinimumDistance > 0
                && definition.SkeletonRangedRetreatChanceBase > 0
                && definition.SkeletonRangedRetreatChanceIntelligenceScale > 0
                && definition.SkeletonRangedAttackChanceBase > 0
                && definition.SkeletonRangedAttackChanceIntelligenceScale > 0) {
                return (
                    definition.SkeletonRangedRetreatMinimumDistance,
                    definition.SkeletonRangedRetreatChanceBase,
                    definition.SkeletonRangedRetreatChanceIntelligenceScale,
                    definition.SkeletonRangedAttackChanceBase,
                    definition.SkeletonRangedAttackChanceIntelligenceScale,
                    definition.SkeletonRangedMovementFreshChanceBonus > 0
                        ? definition.SkeletonRangedMovementFreshChanceBonus
                        : 50);
            }
        }
        return UsesNativeAi(target, "SkeletonRanged")
            ? (4, 13, 2, 3, 2, 50)
            : null;
    }

    /** Resolves mapped missiles through explicit runtime identity before catalog fallback. */
    private bool TryGetMonsterMissile(
        AuthoritativeCombatTarget monster,
        out AuthoritativeMonsterMissileDefinition missile)
    {
        if (monsterMissiles is null) {
            missile = null!;
            return false;
        }
        // A unique encounter may retain a generic monster type while its
        // explicit runtime AI identity selects a different native missile.
        // Resolve that identity first; catalog metadata remains the fallback
        // for older targets that do not carry an explicit label.
        if (!string.IsNullOrWhiteSpace(monster.NativeAi)
            && monsterMissiles.TryGet(
                monster.NativeAi,
                monster.MonsterId,
                out missile))
            return true;
        if (monsterTypes?.TryGet(monster.MonsterId, out var definition) == true
            && monsterMissiles.TryGet(
                definition.NativeAi,
                monster.MonsterId,
                out missile))
            return true;
        // Native Zhar and Lazarus use CounselorAi's intelligence-selected
        // missile family; legacy aliases retain the validated Counselor row
        // when an older target has no alias-specific mapping.
        if (monster.NativeAi is "Zhar" or "Lazarus")
            return monsterMissiles.TryGet("Counselor", monster.MonsterId, out missile);
        missile = null!;
        return false;
    }

    /** Validated arrow mappings keep ranged-only monsters out of adjacent melee. */
    private bool HasValidatedArrowMapping(AuthoritativeCombatTarget monster)
    {
        return TryGetMonsterMissile(monster, out var missile)
            && missile.ProjectileBehavior is AuthoritativeProjectileBehavior.MonsterArrow
                or AuthoritativeProjectileBehavior.Targeted;
    }

    /** Native Rhino AI charge: clear long-range lanes end in a special melee hit. */
    private bool TryAdvanceRhinoCharge(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        int distance)
    {
        var rhinoTuning = GetRhinoTuning(monster);
        if (!rhinoTuning.HasValue)
            return false;

        if (monster.AdvanceRhinoDelay())
            return true;

        var attackSeed = unchecked((uint)tick ^ monster.EntityId ^ player.EntityId);
        // Initialized single-player targets retain the native InitMonster AI
        // seed as a marker that their global process stream is authoritative.
        // Zero-seed fixtures and legacy multiplayer snapshots keep the
        // deterministic compatibility controller.
        var usesNativeAiStream = monster.MonsterAiSeed != 0;
        var actionRoll = usesNativeAiStream
            ? random.RandomIntLessThan(100)
            : (int)(attackSeed % 100U);
        if (distance < 2) {
            monster.ClearRhinoMovement();
            var adjacentAttackChance = Math.Min(
                100,
                28 + (2 * monster.Intelligence));
            return actionRoll >= adjacentAttackChance;
        }

        var hasMoveGoal = monster.RhinoMovementTicks >= 0;
        var movementGoalTuning = GetRhinoMovementGoalTuning(monster);
        var startsMoveGoal = distance >= movementGoalTuning.MinimumDistance
            && (hasMoveGoal
                || (usesNativeAiStream
                    ? random.RandomIntLessThan(movementGoalTuning.ChanceModulo) != 0
                    : (attackSeed / 100U) % (uint)movementGoalTuning.ChanceModulo != 0U));
        if (hasMoveGoal || startsMoveGoal) {
            if (!hasMoveGoal)
                monster.StartRhinoMovement(
                    usesNativeAiStream
                        ? random.RandomIntLessThan(2)
                        : (int)((attackSeed / 100U) % 2U));
            if (monster.AdvanceRhinoMovement(
                    distance,
                    world.HasAiLineOfSight(
                        monster.LevelId,
                        monster.PositionX,
                        monster.PositionY,
                        player.PositionX,
                        player.PositionY))) {
                var roundWalk = usesNativeAiStream
                    ? FindNativeStatefulRoundWalkStepFromRandom(
                        monster,
                        player,
                        players,
                        monster.RhinoMovementTurn,
                        out var nextTurn)
                    : FindNativeStatefulRoundWalkStep(
                        monster,
                        player,
                        players,
                        attackSeed,
                        monster.RhinoMovementTurn,
                        out nextTurn);
                monster.UpdateRhinoMovementTurn(nextTurn);
                if (roundWalk.HasValue) {
                    monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                    monster.RecordRhinoMovement(
                        roundWalk.Value.X - monster.PositionX,
                        roundWalk.Value.Y - monster.PositionY);
                } else {
                    monster.StartRhinoDelay(
                        usesNativeAiStream
                            ? ResolveRhinoDelayFromRandom(monster)
                            : ResolveRhinoDelay(monster, attackSeed));
                }
                return true;
            }
        }

        if (distance < rhinoTuning.Value.MinimumDistance
            || GetEffectiveMonsterToHit(monster, special: true) <= 0
            || GetEffectiveMonsterDamageRange(monster, special: true).Maximum <= 0) {
            return TryAdvanceRhinoMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                usesNativeAiStream);
        }

        var chargeChance = Math.Min(
            100,
            rhinoTuning.Value.ChanceBase
                + (rhinoTuning.Value.IntelligenceScale * monster.Intelligence));
        if (actionRoll >= chargeChance
            || !world.HasAiLineOfSight(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                player.PositionX,
                player.PositionY))
            return TryAdvanceRhinoMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                usesNativeAiStream);

        var destination = FindRhinoChargeDestination(monster, player, players);
        if (!destination.HasValue)
            return TryAdvanceRhinoMovementFallback(
                monster,
                player,
                players,
                attackSeed,
                distance,
                usesNativeAiStream);

        monster.ApplyKnockback(destination.Value.X, destination.Value.Y);
        var damageQ6 = usesNativeAiStream
            ? RollEffectiveMonsterDamageQ6(monster, special: true)
            : SelectNativeSpecialDamageQ6(monster, attackSeed);
        damageQ6 = AuthoritativeEquipmentIncomingDamageRules.ApplyGetHitQ6(
            damageQ6,
            DeterministicFixedPointQ6.Scale,
            player.Equipment);
        var usesLegacyWholeLife = player.LifeMaximum % DeterministicFixedPointQ6.Scale != 0;
        var appliedDamageQ6 = usesLegacyWholeLife
            ? DeterministicFixedPointQ6.ToWholeTruncated(damageQ6)
            : damageQ6;
        var damageResult = playerDamage.Apply(player, appliedDamageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            var eventDamageQ6 = usesLegacyWholeLife
                ? damageQ6
                : damageResult.IncomingDamageQ6;
            events.Add(new DiabloDirectedGameplayEvent(
                player,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    monster.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(eventDamageQ6)) {
                    AmountQ6 = eventDamageQ6,
                }));
        }
        if (player.Life == 0)
            StartPlayerDeath(
                player,
                monster.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (damageQ6 > 0)
            ApplyRhinoChargeKnockback(monster, player, players);
        return true;
    }

    /** Native Rhino movement after charge admission fails or its lane is blocked. */
    private bool TryAdvanceRhinoMovementFallback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint seed,
        int distance,
        bool usesNativeAiStream = false)
    {
        if (usesNativeAiStream) {
            var nativeMovementTuning = GetRhinoMovementTuning(monster);
            var nativeMovementChance = monster.RhinoMovementMode is >= 1 and <= 3
                && monster.RhinoMovementIdleTicks == 0
                ? Math.Min(
                    100,
                    nativeMovementTuning.ChanceBase
                        + (nativeMovementTuning.IntelligenceScale * monster.Intelligence)
                        + nativeMovementTuning.FreshChanceBonus)
                : Math.Min(
                    100,
                    nativeMovementTuning.ChanceBase
                        + (nativeMovementTuning.IntelligenceScale * monster.Intelligence));
            var movementRoll = random.RandomIntLessThan(100);
            if (movementRoll < nativeMovementChance) {
                var randomWalk = FindNativeRandomWalkStepFromRandom(
                    monster,
                    player,
                    players);
                if (randomWalk.HasValue) {
                    monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
                    monster.RecordRhinoMovement(
                        randomWalk.Value.X - monster.PositionX,
                        randomWalk.Value.Y - monster.PositionY);
                } else {
                    monster.AdvanceRhinoMovementIdle();
                }
            } else {
                monster.AdvanceRhinoMovementIdle();
                monster.StartRhinoDelay(ResolveRhinoDelayFromRandom(monster));
            }
            return true;
        }

        var movementGoalTuning = GetRhinoMovementGoalTuning(monster);
        if (distance >= movementGoalTuning.MinimumDistance
            && (seed / 100U) % (uint)movementGoalTuning.ChanceModulo != 0U) {
            var roundWalk = FindNativeRoundWalkStep(
                monster,
                player,
                players,
                seed);
            if (roundWalk.HasValue) {
                monster.BeginMovement(roundWalk.Value.X, roundWalk.Value.Y);
                monster.RecordRhinoMovement(
                    roundWalk.Value.X - monster.PositionX,
                    roundWalk.Value.Y - monster.PositionY);
            }
            return true;
        }

        var movementTuning = GetRhinoMovementTuning(monster);
        var movementChance = GetRhinoMovementChance(
            monster,
            movementTuning.ChanceBase,
            movementTuning.IntelligenceScale,
            movementTuning.FreshChanceBonus);
        if ((seed / 100U) % 100U < (uint)movementChance) {
            var randomWalk = FindNativeRandomWalkStep(
                monster,
                player,
                players,
                seed);
            if (randomWalk.HasValue) {
                monster.BeginMovement(randomWalk.Value.X, randomWalk.Value.Y);
                monster.RecordRhinoMovement(
                    randomWalk.Value.X - monster.PositionX,
                    randomWalk.Value.Y - monster.PositionY);
            } else {
                monster.AdvanceRhinoMovementIdle();
            }
        } else {
            monster.AdvanceRhinoMovementIdle();
            monster.StartRhinoDelay(ResolveRhinoDelay(monster, seed));
        }
        return true;
    }

    /** Native Rhino ordinary movement admission across var2 bands. */
    private static int GetRhinoMovementChance(
        AuthoritativeCombatTarget monster,
        int chanceBase,
        int intelligenceScale,
        int freshChanceBonus)
    {
        var longIdleChance = chanceBase + (intelligenceScale * monster.Intelligence);
        var movementChance = monster.RhinoMovementIdleTicks == -1
            ? longIdleChance
            : monster.RhinoMovementIdleTicks > 20
                ? longIdleChance
                : monster.RhinoMovementIdleTicks == 0
                    && monster.RhinoMovementMode is >= 1 and <= 3
                    ? longIdleChance + freshChanceBonus
                    : 0;
        return Math.Min(100, movementChance);
    }

    private (int ChanceBase, int IntelligenceScale, int FreshChanceBonus)
        GetRhinoMovementTuning(AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoMovementChanceBase > 0
            && definition.RhinoMovementChanceIntelligenceScale > 0) {
            return (
                definition.RhinoMovementChanceBase,
                definition.RhinoMovementChanceIntelligenceScale,
                definition.RhinoMovementFreshChanceBonus > 0
                    ? definition.RhinoMovementFreshChanceBonus
                    : 50);
        }
        return (33, 2, 50);
    }

    private (int MinimumDistance, int ChanceModulo) GetRhinoMovementGoalTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoMovementGoalMinimumDistance > 0
            && definition.RhinoMovementGoalChanceModulo > 0) {
            return (
                definition.RhinoMovementGoalMinimumDistance,
                definition.RhinoMovementGoalChanceModulo);
        }
        return (5, 4);
    }

    private (int MinimumDistance, int ChanceBase, int IntelligenceScale)? GetRhinoTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.RhinoChargeMinimumDistance > 0
                && definition.RhinoChargeChanceBase > 0
                && definition.RhinoChargeChanceIntelligenceScale > 0) {
                return (
                    definition.RhinoChargeMinimumDistance,
                    definition.RhinoChargeChanceBase,
                    definition.RhinoChargeChanceIntelligenceScale);
            }
        }
        return UsesNativeAi(target, "Rhino")
            ? (5, 43, 2)
            : null;
    }

    private int ResolveRhinoDelay(
        AuthoritativeCombatTarget target,
        uint seed)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoDelayBase > 0
            && definition.RhinoDelayRandomRange > 0) {
            delayBase = definition.RhinoDelayBase;
            randomRange = definition.RhinoDelayRandomRange;
        }
        return checked(delayBase + (int)((seed / 200U) % (uint)randomRange));
    }

    /** Native Rhino recovery delay driven by the restored per-monster stream. */
    private int ResolveRhinoDelayFromRandom(AuthoritativeCombatTarget target)
    {
        var delayBase = 10;
        var randomRange = 10;
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true
            && definition.RhinoDelayBase > 0
            && definition.RhinoDelayRandomRange > 0) {
            delayBase = definition.RhinoDelayBase;
            randomRange = definition.RhinoDelayRandomRange;
        }
        return checked(delayBase + random.RandomIntLessThan(randomRange));
    }

    private (int X, int Y)? FindRhinoChargeDestination(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var candidates = new List<(int X, int Y)>();
        for (var offsetY = -1; offsetY <= 1; offsetY++) {
            for (var offsetX = -1; offsetX <= 1; offsetX++) {
                if (offsetX == 0 && offsetY == 0)
                    continue;
                candidates.Add((player.PositionX + offsetX, player.PositionY + offsetY));
            }
        }
        return candidates
            .Where(candidate => world.IsWalkable(monster.LevelId, candidate.X, candidate.Y)
                && !world.IsOccupied(
                    monster.LevelId,
                    candidate.X,
                    candidate.Y,
                    players,
                    monster.EntityId)
                && world.HasAiLineOfSight(
                    monster.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    candidate.X,
                    candidate.Y))
            .OrderBy(candidate => DeterministicGridMetrics.ChebyshevDistance(
                candidate.X,
                candidate.Y,
                monster.PositionX,
                monster.PositionY))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .Select(candidate => ((int X, int Y)?)candidate)
            .FirstOrDefault();
    }

    private void ApplyRhinoChargeKnockback(
        AuthoritativeCombatTarget monster,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var direction = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            monster.PositionX,
            monster.PositionY,
            player.PositionX,
            player.PositionY);
        var displacement = DeterministicGridDirections.GetDisplacement(
            (DeterministicGridDirection)direction);
        var destinationX = checked(player.PositionX + displacement.X);
        var destinationY = checked(player.PositionY + displacement.Y);
        if (!world.IsWalkable(monster.LevelId, destinationX, destinationY)
            || world.IsOccupied(
                monster.LevelId,
                destinationX,
                destinationY,
                players,
                player.EntityId)) {
            return;
        }
        DiabloPlayerActionLifecycle.Cancel(player);
        player.PositionX = destinationX;
        player.PositionY = destinationY;
    }

    /** Validated Fat tuning selects its special melee band. */
    private bool UsesFatSpecialAttack(
        AuthoritativeCombatTarget target,
        int attackRoll,
        (
            int AttackChanceBase,
            int AttackChanceIntelligenceScale,
            int SpecialChanceBase,
            int SpecialChanceIntelligenceScale)? tuning)
    {
        return tuning.HasValue
            && GetEffectiveMonsterToHit(target, special: true) > 0
            && GetEffectiveMonsterDamageRange(target, special: true).Maximum > 0
            && attackRoll >= Math.Min(
                100,
                tuning.Value.SpecialChanceBase
                    + (tuning.Value.SpecialChanceIntelligenceScale
                        * target.Intelligence))
            && attackRoll < Math.Min(
                100,
                tuning.Value.AttackChanceBase
                    + (tuning.Value.AttackChanceIntelligenceScale
                        * target.Intelligence));
    }

    private static bool NativeFatAttackBandAllowsAttack(
        AuthoritativeCombatTarget target,
        int attackRoll,
        (
            int AttackChanceBase,
            int AttackChanceIntelligenceScale,
            int SpecialChanceBase,
            int SpecialChanceIntelligenceScale) tuning)
    {
        return attackRoll < Math.Min(
            100,
            tuning.AttackChanceBase
                + (tuning.AttackChanceIntelligenceScale * target.Intelligence));
    }

    /** Validated GoatMelee tuning selects a wounded special on its configured roll. */
    private bool UsesGoatSpecialAttack(
        AuthoritativeCombatTarget target,
        uint attackSeed,
        (
            int ChanceBase,
            int IntelligenceScale,
            int SpecialHealthThresholdPercent,
            int SpecialChanceGroupPeriod)? tuning)
    {
        return tuning.HasValue
            && (long)target.HitPointsQ6 * 100
                < (long)target.MaxHitPointsQ6 * tuning.Value.SpecialHealthThresholdPercent
            && GetEffectiveMonsterToHit(target, special: true) > 0
            && GetEffectiveMonsterDamageRange(target, special: true).Maximum > 0
            && (attackSeed / 100U) % (uint)tuning.Value.SpecialChanceGroupPeriod == 0U;
    }

    /** Native GoatMelee wounded-special admission driven by the restored stream. */
    private bool UsesGoatSpecialAttackFromRandom(
        AuthoritativeCombatTarget target,
        (
            int ChanceBase,
            int IntelligenceScale,
            int SpecialHealthThresholdPercent,
            int SpecialChanceGroupPeriod)? tuning)
    {
        if (!tuning.HasValue
            || (long)target.HitPointsQ6 * 100
                >= (long)target.MaxHitPointsQ6 * tuning.Value.SpecialHealthThresholdPercent
            || GetEffectiveMonsterToHit(target, special: true) <= 0
            || GetEffectiveMonsterDamageRange(target, special: true).Maximum <= 0)
            return false;

        var groupPeriod = tuning.Value.SpecialChanceGroupPeriod;
        return groupPeriod <= 1
            || random.RandomIntLessThan(groupPeriod) == 0;
    }

    /** Selects the native inclusive special damage range without adding a new RNG draw. */
    private int SelectNativeSpecialDamage(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        return SelectEffectiveMonsterDamage(target, special: true, seed: attackSeed);
    }

    /** Selects compatibility-stream Rhino damage in the native Q6 unit. */
    private int SelectNativeSpecialDamageQ6(
        AuthoritativeCombatTarget target,
        uint attackSeed)
    {
        var range = GetEffectiveMonsterDamageRange(target, special: true);
        var minimumQ6 = DeterministicFixedPointQ6.FromWhole(range.Minimum);
        var maximumQ6 = DeterministicFixedPointQ6.FromWhole(range.Maximum);
        if (maximumQ6 < minimumQ6)
            return 0;
        var widthQ6 = checked(maximumQ6 - minimumQ6 + 1);
        return checked(minimumQ6 + (int)(attackSeed % (uint)widthQ6));
    }

    private static int SelectNativeDamage(int minimum, int maximum, uint attackSeed)
    {
        var range = checked(maximum - minimum + 1);
        return checked(minimum + (int)(attackSeed % (uint)range));
    }

    private void AdvancePlayerMinion(
        AuthoritativeCombatTarget minion,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player => player.EntityId == minion.OwnerPlayerId);
        // Preserve legacy faction-only minion markers used by non-Golem effects and
        // older snapshots which did not carry an owner identity.
        if (owner is null && minion.OwnerPlayerId == 0)
            return;
        if (owner is null
            || owner.Life <= 0
            || owner.CombatMode is DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit
            || owner.LevelId != minion.LevelId
            || (minion.HitPoints <= 0 && minion.DeathAnimationTicks == 0)) {
            world.CombatTargets.Remove(minion.EntityId);
            return;
        }
        if (minion.HitPoints <= 0) {
            if (minion.AdvanceDeathAnimation())
                world.CombatTargets.Remove(minion.EntityId);
            return;
        }
        if (minion.IsPetrified)
            return;

        var enemy = world.CombatTargets.Values
            .Where(candidate => candidate.EntityId != minion.EntityId
                && !candidate.IsPlayerMinion
                && candidate.HitPoints > 0
                && (candidate.LevelId == 0 || candidate.LevelId == minion.LevelId))
            .OrderBy(candidate => DeterministicGridMetrics.ChebyshevDistance(
                candidate.PositionX,
                candidate.PositionY,
                minion.PositionX,
                minion.PositionY))
            .ThenBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        if (enemy is null) {
            TryBeginPlayerMinionIdleWalk(minion, owner, players);
            return;
        }

        // Native GolumAi refreshes its facing before either path planning or
        // the blocked-path RandomWalk fallback. Clients only receive the
        // resulting authoritative direction and movement.
        minion.RestoreDirection(GetDirection(
            minion.PositionX,
            minion.PositionY,
            enemy.PositionX,
            enemy.PositionY));
        var distance = ClampNativeDistance(DeterministicGridMetrics.ChebyshevDistance(
            enemy.PositionX,
            enemy.PositionY,
            minion.PositionX,
            minion.PositionY));
        if (distance <= 1) {
            var rolledDamage = random.RandomIntBetween(minion.MinDamage, minion.MaxDamage);
            var damage = rules.ResolveAttackDamage(
                rolledDamage,
                enemy,
                unchecked((uint)tick ^ minion.EntityId ^ enemy.EntityId));
            var minionEvents = new List<DiabloGameplayEvent>();
            ApplyResolvedDamageQ6(
                owner,
                enemy,
                DeterministicFixedPointQ6.FromWhole(damage),
                minionEvents);
            foreach (var minionEvent in minionEvents)
                events.Add(new DiabloDirectedGameplayEvent(owner, minionEvent));
            return;
        }

        var nextStep = FindPlayerMinionStep(minion, enemy, players);
        if (nextStep.HasValue)
            minion.BeginMovement(nextStep.Value.X, nextStep.Value.Y);
        else if (FindNativeRandomWalkStepFromRandom(
                     minion,
                     (int)owner.FacingDirection,
                     players) is { } randomWalk)
            minion.BeginMovement(randomWalk.X, randomWalk.Y);
        else {
            // GolumAi sweeps every direction clockwise from its current
            // facing after a failed RandomWalk, without invoking generic
            // navigation. Preserve that bounded native fallback order.
            foreach (var direction in DeterministicGridDirections.EnumerateClockwise(
                         minion.Direction)) {
                if (FindStepInDirection(minion, direction, players) is not { } step)
                    continue;
                minion.BeginMovement(step.X, step.Y);
                break;
            }
        }
    }

    private void TryBeginPlayerMinionIdleWalk(
        AuthoritativeCombatTarget minion,
        AuthoritativePlayerState owner,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var ownerDirection = (int)owner.FacingDirection;
        var firstTurn = random.FlipCoin(2) ? 1 : -1;
        var secondTurn = random.FlipCoin(2) ? 2 : -2;
        var directions = DeterministicGridDirections.EnumerateTurnCandidates(
            ownerDirection,
            firstTurn,
            secondTurn);

        foreach (var direction in directions) {
            if (TryBeginPlayerMinionWalk(minion, direction, players))
                return;
        }

        var fallbackDirection = RotateDirection(ownerDirection, -1);
        for (var index = 0; index < DirectionDisplacements.Count; index++) {
            fallbackDirection = RotateDirection(fallbackDirection, 1);
            if (TryBeginPlayerMinionWalk(minion, fallbackDirection, players))
                return;
        }
    }

    private bool TryBeginPlayerMinionWalk(
        AuthoritativeCombatTarget minion,
        int direction,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var displacement = DirectionDisplacements[direction];
        var candidateX = checked(minion.PositionX + displacement.X);
        var candidateY = checked(minion.PositionY + displacement.Y);
        if (!world.IsWalkable(minion.LevelId, candidateX, candidateY)
            || world.IsOccupied(
                minion.LevelId,
                candidateX,
                candidateY,
                players,
                minion.EntityId)) {
            return false;
        }
        minion.BeginMovement(candidateX, candidateY);
        return true;
    }

    private (int X, int Y)? FindPlayerMinionStep(
        AuthoritativeCombatTarget minion,
        AuthoritativeCombatTarget enemy,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var start = (X: minion.PositionX, Y: minion.PositionY);
        var goals = new HashSet<(int X, int Y)>();
        for (var offsetY = -1; offsetY <= 1; offsetY++) {
            for (var offsetX = -1; offsetX <= 1; offsetX++) {
                if (offsetX == 0 && offsetY == 0)
                    continue;
                var goal = (X: enemy.PositionX + offsetX, Y: enemy.PositionY + offsetY);
                if (world.IsWalkable(minion.LevelId, goal.X, goal.Y)
                    && !world.IsOccupied(
                        minion.LevelId,
                        goal.X,
                        goal.Y,
                        players,
                        minion.EntityId)) {
                    goals.Add(goal);
                }
            }
        }
        if (goals.Count == 0)
            return null;

        if (goals.Contains(start))
            return start;
        return DeterministicGridNavigation.FindStepToGoal(
            start.X,
            start.Y,
            int.MaxValue,
            (x, y) => goals.Contains((x, y)),
            (x, y) => world.IsWalkable(minion.LevelId, x, y),
            (x, y) => world.IsOccupied(
                minion.LevelId,
                x,
                y,
                players,
                minion.EntityId));
    }

    private void AdvanceProjectiles(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var projectileEntityIds = world.Projectiles.Keys.Order().ToList();
        var knownProjectileEntityIds = projectileEntityIds.ToHashSet();
        for (var projectileIndex = 0;
             projectileIndex < projectileEntityIds.Count;
             projectileIndex++) {
            try {
                if (!world.Projectiles.TryGetValue(
                        projectileEntityIds[projectileIndex],
                        out var projectile)) {
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ChargedBolt) {
                    AdvanceChargedBolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.MonsterChargedBolt) {
                    AdvanceChargedBolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow) {
                    AdvanceWeaponArrow(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.HorkSpawn) {
                    AdvanceHorkSpawn(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.HolyBolt) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.MonsterMagic) {
                    AdvanceFirebolt(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Acid) {
                    AdvanceAcid(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.AcidSplat) {
                    AdvanceAcidSplat(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle) {
                    AdvanceAcidPuddle(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.SnakeCharge) {
                    AdvanceSnakeCharge(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BatCharge) {
                    AdvanceBatCharge(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BoneSpirit) {
                    AdvanceBoneSpirit(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.LightningWallController) {
                    AdvanceFireWallController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.RingOfFireController) {
                    AdvanceRingOfFireController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ImmolationController) {
                    AdvanceImmolationController(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment) {
                    AdvanceFireWallSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveController) {
                    AdvanceFlameWaveController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveSegment) {
                    AdvanceFlameWaveSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController) {
                    AdvanceInfernoController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.InfernoSegment) {
                    AdvanceInfernoSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningController) {
                    AdvanceChainLightningController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.NovaController) {
                    AdvanceNovaController(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseController) {
                    AdvanceApocalypseController(projectile, players);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.Lightning
                    or AuthoritativeProjectileBehavior.ChainLightningBolt) {
                    AdvanceLightningController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.NovaBall) {
                    AdvanceNovaBall(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseBoom) {
                    AdvanceApocalypseBoom(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.RageController) {
                    AdvanceRageController(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Elemental) {
                    AdvanceElemental(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.GuardianController) {
                    AdvanceGuardian(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.Fireball) {
                    AdvanceFireball(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.LightningSegment) {
                    AdvanceLightningSegment(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.FlashBottom
                    or AuthoritativeProjectileBehavior.FlashTop) {
                    AdvanceFlashLayer(tick, projectile, players, events);
                    continue;
                }
                if (projectile.Behavior is AuthoritativeProjectileBehavior.Teleport
                    or AuthoritativeProjectileBehavior.Phasing) {
                    AdvanceMovementController(projectile, players);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.ResurrectionBeam) {
                    AdvanceResurrectionBeam(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.StoneCurse) {
                    AdvanceStoneCurse(projectile);
                    continue;
                }
                if (projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow
                    && world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                    AdvanceMonsterArrow(tick, projectile, players, events);
                    continue;
                }
                if (projectile.RemainingTicks > 1) {
                    var nextX = projectile.PositionX
                        + Math.Sign((long)projectile.TargetX - projectile.PositionX);
                    var nextY = projectile.PositionY
                        + Math.Sign((long)projectile.TargetY - projectile.PositionY);
                    if (world.IsMissileBlocked(projectile.LevelId, nextX, nextY)
                        || !world.HasMissileLineOfSight(
                            projectile.LevelId,
                            projectile.PositionX,
                            projectile.PositionY,
                            nextX,
                            nextY)) {
                        BreakMissileBlockingObject(projectile.LevelId, nextX, nextY);
                        world.Projectiles.Remove(projectile.EntityId);
                        continue;
                    }
                    projectile.PositionX = nextX;
                    projectile.PositionY = nextY;
                    projectile.RemainingTicks--;
                    continue;
                }

                if (projectile.TargetEntityId != 0) {
                    if (world.CombatTargets.TryGetValue(projectile.TargetEntityId, out var target)
                        && target.HitPoints > 0
                        && (target.LevelId == 0 || target.LevelId == projectile.LevelId)) {
                        ResolveProjectileDamage(projectile, target, players, events);
                    }
                } else {
                    foreach (var target in world.CombatTargets.Values
                                 .Where(target => target.HitPoints > 0
                                     && (target.LevelId == 0
                                         || target.LevelId == projectile.LevelId)
                                      && DeterministicGridMetrics.ChebyshevDistance(
                                          target.PositionX,
                                          target.PositionY,
                                          projectile.TargetX,
                                          projectile.TargetY)
                                          <= projectile.AreaRadius)
                                 .OrderBy(target => target.EntityId)) {
                        ResolveProjectileDamage(projectile, target, players, events);
                    }
                }
                world.Projectiles.Remove(projectile.EntityId);
            } finally {
                foreach (var newEntityId in world.Projectiles.Keys
                             .Where(entityId => knownProjectileEntityIds.Add(entityId))
                             .Order()) {
                    projectileEntityIds.Add(newEntityId);
                }
            }
        }
    }

    /** Native CheckMissileCol transition for a breakable arrow blocker. */
    private void BreakMissileBlockingObject(
        uint levelId,
        int positionX,
        int positionY)
    {
        if (world.TryGetMissileBlockingObject(
                levelId,
                positionX,
                positionY,
                out var blockingObject)
            && blockingObject!.Breakable) {
            blockingObject.Destroyed = true;
        }
    }

    private void AdvanceWeaponArrow(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.ActivationDelayTicks > 0) {
            projectile.ActivationDelayTicks--;
            return;
        }
        if (projectile.RemainingTicks > 1) {
            // Legacy hand-built fixtures used Speed=1 as a one-step marker;
            // native AddArrow speeds occupy the 16..74 range after class and
            // Hellfire attack-speed modifiers are applied.
            var velocity = projectile.Speed is >= 16 and <= 74
                ? projectile.Speed
                : 32;
            var travelBudget = checked(velocity + projectile.PathTicks);
            var travelSteps = travelBudget / 32;
            projectile.PathTicks = travelBudget % 32;
            if (travelSteps == 0
                && TryResolveWeaponArrowTileCollision(
                    tick,
                    projectile,
                    players,
                    events)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }
            if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
                return;
            for (var step = 0; step < travelSteps; step++) {
                var nextX = projectile.PositionX
                    + Math.Sign((long)projectile.TargetX - projectile.PositionX);
                var nextY = projectile.PositionY
                    + Math.Sign((long)projectile.TargetY - projectile.PositionY);
                if (world.IsMissileBlocked(projectile.LevelId, nextX, nextY)
                    || !world.HasMissileLineOfSight(
                        projectile.LevelId,
                        projectile.PositionX,
                        projectile.PositionY,
                        nextX,
                        nextY)) {
                    BreakMissileBlockingObject(projectile.LevelId, nextX, nextY);
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                projectile.PositionX = nextX;
                projectile.PositionY = nextY;
                if (TryResolveWeaponArrowTileCollision(
                        tick,
                        projectile,
                        players,
                        events)) {
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
                    return;
            }
            projectile.RemainingTicks--;
            return;
        }

        if (TryResolveWeaponArrowTileCollision(
                tick,
                projectile,
                players,
                events)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;

        if (projectile.TargetEntityId != 0
            && world.CombatTargets.TryGetValue(projectile.TargetEntityId, out var target)
            && target.HitPoints > 0
            && (target.LevelId == 0 || target.LevelId == projectile.LevelId)
            && IsWeaponArrowEndpointOccupied(projectile, target)) {
            ResolveWeaponArrowImpact(tick, projectile, target, players, events);
        } else if (projectile.TargetEntityId != 0
            && players.FirstOrDefault(candidate =>
                candidate.EntityId == projectile.TargetEntityId
                && candidate.Life > 0
                && (candidate.LevelId == 0 || candidate.LevelId == projectile.LevelId)
                && candidate.PositionX == projectile.TargetX
                && candidate.PositionY == projectile.TargetY)
                is { } playerTarget) {
            _ = ResolvePlayerWeaponArrowImpact(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;
        world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessArrow only resolves the declared endpoint while it occupies that tile. */
    private static bool IsWeaponArrowEndpointOccupied(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target)
    {
        return target.PositionX == projectile.TargetX
            && target.PositionY == projectile.TargetY
            || target.IsMoving
            && target.IsPetrified
            && target.FuturePositionX == projectile.TargetX
            && target.FuturePositionY == projectile.TargetY;
    }

    /** Native ProcessArrow collision check for each traversed tile. */
    private bool TryResolveWeaponArrowTileCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var target = FindProjectileTarget(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (target is not null) {
            ResolveWeaponArrowImpact(tick, projectile, target, players, events);
            return true;
        }

        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is null)
            return false;

        return ResolvePlayerWeaponArrowImpact(
            tick,
            projectile,
            playerTarget,
            players,
            events);
    }

    /** Advances a content-declared native Arrow from a monster to its player target. */
    private void AdvanceMonsterArrow(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (!world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var travelSteps = Math.Max(1, projectile.Speed);
        for (var step = 0; step < travelSteps; step++) {
            var nextX = projectile.PositionX
                + Math.Sign((long)projectile.TargetX - projectile.PositionX);
            var nextY = projectile.PositionY
                + Math.Sign((long)projectile.TargetY - projectile.PositionY);
            if (!world.ContainsPosition(projectile.LevelId, nextX, nextY)
                || world.IsMissileBlocked(projectile.LevelId, nextX, nextY)) {
                BreakMissileBlockingObject(projectile.LevelId, nextX, nextY);
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            projectile.PositionX = nextX;
            projectile.PositionY = nextY;
            var target = FindProjectilePlayer(
                projectile,
                players,
                nextX,
                nextY);
            if (target is not null) {
                // Native PlayerMHit returns false for temporary player
                // protection (for example Etherealize). In that case the
                // arrow continues through the occupied tile instead of being
                // consumed as a successful collision.
                if (ResolveMonsterArrowImpact(tick, projectile, target, players, events)) {
                    if (projectile.ImpactLifetime > 0)
                        EnterProjectileImpact(projectile);
                    else
                        world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
            }

            if (nextX == projectile.TargetX && nextY == projectile.TargetY)
                break;
        }

        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private bool ResolveMonsterArrowImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        // DevilutionX's PlayerMHit rejects an arrow against an invincible or
        // Etherealized player without marking the missile as hit. Preserve
        // that distinction so the projectile keeps travelling.
        if (IsPlayerProtectedByFlash(target) || IsPlayerEtherealized(target))
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(target, targetClass, projectile.DamageType);
        var damage = projectile.Damage;
        if (damage > 0 && projectile.DamageMinimum > 0) {
            // Native ProcessArrow supplies the monster range to PlayerMHit,
            // which rolls physical damage when the arrow collides rather than
            // when the AI admits the shot.
            damage = random.RandomIntBetween(
                projectile.DamageMinimum,
                projectile.DamageMaximum);
        }
        // Native PlayerMHit applies the target's `_pIGetHit` bonus after the
        // impact-time arrow roll, before resistance reduces the incoming hit.
        damage = AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
            damage,
            rules.MinimumDamage,
            target.Equipment);
        if (resistance > 0)
            damage -= damage * resistance / 100;
        var incomingDamageQ6 = DeterministicFixedPointQ6.FromWhole(Math.Max(0, damage));
        var usesLegacyWholeLife = target.LifeMaximum % DeterministicFixedPointQ6.Scale != 0;
        var appliedDamageQ6 = usesLegacyWholeLife
            ? DeterministicFixedPointQ6.ToWholeTruncated(incomingDamageQ6)
            : incomingDamageQ6;
        var damageResult = playerDamage.Apply(target, appliedDamageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            var eventDamageQ6 = usesLegacyWholeLife
                ? incomingDamageQ6
                : damageResult.IncomingDamageQ6;
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    projectile.SourceEntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(eventDamageQ6)) {
                    AmountQ6 = eventDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                projectile.SourceEntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        return true;
    }

    private bool ResolvePlayerWeaponArrowImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player =>
            player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return true;

        var gameplayEvents = new List<DiabloGameplayEvent>();
        if (projectile.PerilSelfDamageQ6 > 0) {
            var selfDamageResult = playerDamage.Apply(owner, projectile.PerilSelfDamageQ6);
            if (selfDamageResult.IncomingDamageQ6 > 0) {
                gameplayEvents.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    owner.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        selfDamageResult.IncomingDamageQ6)) {
                    AmountQ6 = selfDamageResult.IncomingDamageQ6,
                });
            }
            if (owner.Life == 0) {
                StartPlayerDeath(
                    owner,
                    owner.EntityId,
                    tick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }

        // Native Plr2PlrMHit repeats the friendly-fire and Etherealize
        // boundaries when the delayed arrow reaches its target. The target
        // may change state after launch, so launch-time admission is not
        // sufficient.
        if ((!rules.FriendlyFire && owner.FriendlyMode)
            || IsPlayerEtherealized(target))
            return false;

        if (projectile.AttackHit
            && TryResolvePlayerBlock(owner, target, tick)) {
            if (rules.GameVariant == DiabloGameVariant.Hellfire) {
                // Native RotateBlockedMissile consumes GenerateRnd(2) and
                // keeps a blocked missile alive through its impact animation.
                random.RandomIntLessThan(2);
                EnterProjectileImpact(projectile);
                foreach (var gameplayEvent in gameplayEvents)
                    events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
                return false;
            }
            foreach (var gameplayEvent in gameplayEvents)
                events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
            return true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, projectile.DamageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            gameplayEvents.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                owner.EntityId,
                target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(
                    damageResult.IncomingDamageQ6)) {
                AmountQ6 = damageResult.IncomingDamageQ6,
            });
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (projectile.AttackHit
            && (projectile.ElementalFireDamage > 0
                || projectile.ElementalLightningDamage > 0)) {
            var targetProxy = CreatePlayerCombatTargetProxy(target);
            ApplyPlayerElementalChannels(
                owner,
                target,
                targetProxy,
                new AuthoritativeEquipmentElementalDamageRules.ElementalDamage(
                    projectile.ElementalFireDamage,
                    projectile.ElementalLightningDamage),
                tick,
                players,
                gameplayEvents);
        }
        ApplyPlayerKnockback(owner, target, projectile.DamageQ6, players);
        if (projectile.DamageQ6 > 0)
            ApplyPlayerAttackEffects(owner, projectile.DamageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private void ResolveWeaponArrowImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player =>
            player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return;

        var gameplayEvents = new List<DiabloGameplayEvent>();
        if (string.Equals(target.NativeAi, "Gargoyle", StringComparison.Ordinal)
            && target.GargoyleSpecialActive) {
            // Native MonsterMHit consumes the Gargoyle wake-up flag at arrow
            // impact, before damage or any Hellfire equipment effect.
            target.GargoyleSpecialActive = false;
            return;
        }
        if (projectile.DoppelgangerProc)
            SpawnDoppelganger(target, players);
        if (projectile.PerilSelfDamageQ6 > 0) {
            var damageResult = playerDamage.Apply(owner, projectile.PerilSelfDamageQ6);
            if (damageResult.IncomingDamageQ6 > 0) {
                gameplayEvents.Add(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    owner.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                });
            }
            if (owner.Life == 0) {
                StartPlayerDeath(
                    owner,
                    owner.EntityId,
                    tick,
                    players,
                    PlayerDeathReason.MonsterOrTrap);
            }
        }
        ApplyResolvedDamageQ6(
            owner,
            target,
            projectile.DamageQ6,
            gameplayEvents);
        if (projectile.AttackHit && target.HitPointsQ6 > 0) {
            ApplyWeaponArrowElementalDamage(
                owner,
                target,
                projectile,
                gameplayEvents);
        }
        ApplyEquipmentKnockback(owner, target, projectile.DamageQ6, players);
        if (projectile.DamageQ6 > 0)
            ApplyEquipmentAttackEffects(owner, projectile.DamageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
    }

    private void ApplyWeaponArrowElementalDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        AuthoritativeProjectile projectile,
        ICollection<DiabloGameplayEvent> events)
    {
        if (projectile.ElementalFireDamage > 0) {
            var fireDamage = rules.ResolveDamage(
                projectile.ElementalFireDamage,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Fire,
                target);
            ApplyResolvedDamage(player, target, fireDamage, events);
        }
        if (projectile.ElementalLightningDamage > 0 && target.HitPointsQ6 > 0) {
            var lightningDamage = rules.ResolveDamage(
                projectile.ElementalLightningDamage,
                GetEffectiveMonsterArmorClass(target),
                AuthoritativeDamageType.Lightning,
                target);
            ApplyResolvedDamage(player, target, lightningDamage, events);
        }
    }

    private void AdvanceMovementController(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var player = players.FirstOrDefault(candidate =>
            IsProjectileOwner(projectile, candidate));
        if (player is null) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var destination = FindClosestTeleportDestination(
            player,
            projectile.PositionX,
            projectile.PositionY,
            players,
            projectile.Behavior == AuthoritativeProjectileBehavior.Teleport
                && spells.TryGet(projectile.SpellId, out var teleportSpell)
                ? teleportSpell.TeleportSearchRadius
                : 5);
        if (!destination.HasValue)
            return;
        player.PositionX = destination.Value.X;
        player.PositionY = destination.Value.Y;
    }

    private void AdvanceResurrectionBeam(AuthoritativeProjectile projectile)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceStoneCurse(AuthoritativeProjectile projectile)
    {
        projectile.RemainingTicks--;
        if (!world.CombatTargets.TryGetValue(projectile.TargetEntityId, out var target)
            || !target.IsPetrified) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (target.HitPointsQ6 == 0
            && projectile.Phase != AuthoritativeProjectilePhase.Impact) {
            projectile.Phase = AuthoritativeProjectilePhase.Impact;
            projectile.RemainingTicks = checked((uint)projectile.ImpactLifetime);
        }
        if (projectile.RemainingTicks != 0)
            return;

        world.Projectiles.Remove(projectile.EntityId);
        if (target.HitPointsQ6 > 0)
            target.IsPetrified = false;
    }

    private void AdvanceFirebolt(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            if ((projectile.Behavior is AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.MonsterMagic)
                || projectile.Phase == AuthoritativeProjectilePhase.Impact) {
                world.Projectiles.Remove(projectile.EntityId);
            } else {
                EnterProjectileImpact(projectile);
            }
            return;
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;

        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var previousTraveledX = projectile.TraveledX;
        var previousTraveledY = projectile.TraveledY;
        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (projectile.LastCollisionTargetEntityId != targetEntityId
                && CheckProjectileCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                if (projectile.Behavior is AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.MonsterMagic) {
                    if (!TryEnterMonsterProjectileImpact(projectile))
                        world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                EnterProjectileImpact(projectile);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            return;
        }

        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(
                projectile.LevelId,
                tile.X,
                tile.Y);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                tile.X,
                tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (CheckProjectileCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                if (projectile.Behavior is AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.MonsterMagic) {
                    if (!TryEnterMonsterProjectileImpact(projectile))
                        world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
                projectile.PositionX = previousX;
                projectile.PositionY = previousY;
                projectile.TraveledX = previousTraveledX;
                projectile.TraveledY = previousTraveledY;
                EnterProjectileImpact(projectile);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            previousX = tile.X;
            previousY = tile.Y;
            previousTraveledX = tile.TraveledX;
            previousTraveledY = tile.TraveledY;
        }
    }

    /** Native ProcessHorkSpawn travel and expiry summon admission. */
    private void AdvanceHorkSpawn(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is not null
            && TryResolveHorkSpawnPlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events)) {
            world.Projectiles.Remove(projectile.EntityId);
            CompleteHorkSpawn(projectile, players);
            return;
        }

        // Native CheckMissileCol checks the current tile before the summon
        // expiry branch, including breakable missile-blocking objects.
        if (world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)) {
            BreakMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            world.Projectiles.Remove(projectile.EntityId);
            CompleteHorkSpawn(projectile, players);
            return;
        }

        if (projectile.RemainingTicks != 0) {
            foreach (var tile in MoveNativeProjectile(projectile)) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                projectile.TraveledX = tile.TraveledX;
                projectile.TraveledY = tile.TraveledY;
                if (world.IsMissileBlocked(
                        projectile.LevelId,
                        tile.X,
                        tile.Y)) {
                    // Native ProcessHorkSpawn moves into the blocked tile and
                    // checks it on the following tick before expiry/summoning.
                    return;
                }
            }
            return;
        }

        world.Projectiles.Remove(projectile.EntityId);
        CompleteHorkSpawn(projectile, players);
    }

    /** Native Hork Spawn expiry admission and summon creation. */
    private void CompleteHorkSpawn(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (monsterSpawnPlanner is null
            || world.CombatTargets.Count >= 256)
            return;

        var spawnPosition = DeterministicGridCrawl.FindClosestValidPosition(
            projectile.PositionX,
            projectile.PositionY,
            0,
            1,
            (candidateX, candidateY) =>
                world.IsWalkable(projectile.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    projectile.LevelId,
                    candidateX,
                    candidateY,
                    players));
        if (!spawnPosition.HasValue)
            return;

        if (projectile.SpellId == 0)
            return;

        var summon = monsterSpawnPlanner.CreateStableMonsterSummon(
            projectile.LevelId,
            projectile.SpellId,
            unchecked(
                projectile.EntityId
                ^ projectile.SourceEntityId
                ^ projectile.TargetEntityId),
            spawnPosition.Value.X,
            spawnPosition.Value.Y,
            AllocateMonsterEntityId(players),
            gameMode == DiabloGameMode.Multiplayer,
            projectile.OriginalDirection);
        if (summon is not null) {
            DiabloDifficultyMonsterScaling.ApplyInitialHealth(
                summon,
                difficulty,
                rules.GameVariant,
                gameMode,
                rules.DifficultyRules);
            DiabloDifficultyMonsterScaling.ApplyInitialResistances(
                summon,
                difficulty,
                monsterTypes);
            world.CombatTargets.Add(summon.EntityId, summon);
        }
    }

    /** Native AddAcid/ProcessGenericProjectile travel and expiry transition. */
    private void AdvanceAcid(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            if (TryResolveAcidProjectileCollision(
                    tick,
                    projectile,
                    players,
                    events)) {
                return;
            }
        }

        if (visitedTiles.Count == 0
            && TryResolveAcidProjectileCollision(
                tick,
                projectile,
                players,
                events)) {
            return;
        }

        if (projectile.RemainingTicks == 0)
            SpawnAcidSplat(projectile);
    }

    private bool TryResolveAcidProjectileCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is not null
            && projectile.LastCollisionTargetEntityId != playerTarget.EntityId) {
            projectile.LastCollisionTargetEntityId = playerTarget.EntityId;
            if (TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events,
                    damageShifted: true)) {
                SpawnAcidSplat(projectile);
                return true;
            }
        }

        if (!world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY))
            return false;

        if (world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var @object)
            && @object!.Breakable) {
            @object.Destroyed = true;
        }
        SpawnAcidSplat(projectile);
        return true;
    }

    private void SpawnAcidSplat(AuthoritativeProjectile projectile)
    {
        if (!world.Projectiles.Remove(projectile.EntityId))
            return;

        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            new AuthoritativeProjectile(
                entityId,
                projectile.SourceEntityId,
                0,
                projectile.SpellId,
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                projectile.PositionX,
                projectile.PositionY,
                0,
                AuthoritativeDamageType.Acid,
                0,
                1U) {
                Behavior = AuthoritativeProjectileBehavior.AcidSplat,
            });
    }

    /** Native ProcessAcidSplate shifts the visual one tile before creating its puddle. */
    private void AdvanceAcidSplat(AuthoritativeProjectile projectile)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks != 0)
            return;

        if (!world.CombatTargets.TryGetValue(
                projectile.SourceEntityId,
                out var monster)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var puddlePositionX = checked(projectile.PositionX + 1);
        var puddlePositionY = checked(projectile.PositionY + 1);
        var puddleMapping = TryGetMonsterMissile(monster, out var mappedMissile)
            && mappedMissile.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
            ? mappedMissile
            : null;
        var puddleRandomRange = puddleMapping?.PuddleDurationRandomRange > 0
            ? puddleMapping.PuddleDurationRandomRange
            : 15;
        var puddleDurationBase = puddleMapping?.PuddleDurationBase > 0
            ? puddleMapping.PuddleDurationBase
            : 40;
        var puddleDurationIntelligenceScale = puddleMapping?.PuddleDurationIntelligenceScale > 0
            ? puddleMapping.PuddleDurationIntelligenceScale
            : 40;
        var puddleDuration = checked(
            random.GenerateRnd(puddleRandomRange)
            + puddleDurationBase
            + puddleDurationIntelligenceScale * monster.Intelligence);
        var puddleDamageMinimumLevel = puddleMapping?.PuddleDamageMinimumMonsterLevel > 0
            ? puddleMapping.PuddleDamageMinimumMonsterLevel
            : 2;
        var puddleDamageLow = puddleMapping?.PuddleDamageLow > 0
            ? puddleMapping.PuddleDamageLow
            : 1;
        var puddleDamageHigh = puddleMapping?.PuddleDamageHigh > 0
            ? puddleMapping.PuddleDamageHigh
            : 2;
        var puddleDamage = GetEffectiveMonsterLevel(monster) >= puddleDamageMinimumLevel
            ? puddleDamageHigh
            : puddleDamageLow;
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            new AuthoritativeProjectile(
                entityId,
                projectile.SourceEntityId,
                0,
                projectile.SpellId,
                projectile.LevelId,
                puddlePositionX,
                puddlePositionY,
                puddlePositionX,
                puddlePositionY,
                puddleDamage,
                AuthoritativeDamageType.Acid,
                0,
                checked((uint)puddleDuration)) {
                Behavior = AuthoritativeProjectileBehavior.AcidPuddle,
            });
        world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessAcidPuddle checks the occupied player every active tick. */
    private void AdvanceAcidPuddle(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        if (playerTarget is not null) {
            _ = TryResolveAcidPuddlePlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        }

        // Native CheckMissileCol still breaks a breakable blocker even though
        // the persistent puddle restores its duration after the collision.
        BreakMissileBlockingObject(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);

        if (projectile.RemainingTicks == 0) {
            projectile.Phase = AuthoritativeProjectilePhase.Impact;
            var impactLifetime = 2;
            if (world.CombatTargets.TryGetValue(
                    projectile.SourceEntityId,
                    out var sourceMonster)
                && TryGetMonsterMissile(sourceMonster, out var puddleMapping)
                && puddleMapping.ProjectileBehavior == AuthoritativeProjectileBehavior.Acid
                && puddleMapping.PuddleImpactLifetime > 0) {
                impactLifetime = puddleMapping.PuddleImpactLifetime;
            }
            projectile.RemainingTicks = checked((uint)impactLifetime);
        }
    }

    /** Native ProcessRhino movement for a Snake-owned charging controller. */
    private void AdvanceSnakeCharge(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            projectile.RemainingTicks--;
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (!world.CombatTargets.TryGetValue(
                projectile.SourceEntityId,
                out var monster)
            || monster.HitPoints <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)
                || world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y)) {
                projectile.PositionX = previousX;
                projectile.PositionY = previousY;
                _ = TryResolveSnakeChargePlayerHit(
                    tick,
                    projectile,
                    monster,
                    players,
                    previousX,
                    previousY,
                    events);
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            monster.ApplyKnockback(tile.X, tile.Y);
            if (TryResolveSnakeChargePlayerHit(
                    tick,
                    projectile,
                    monster,
                    players,
                    tile.X,
                    tile.Y,
                    events)) {
                if (!TryEnterMonsterProjectileImpact(projectile))
                    world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            previousX = tile.X;
            previousY = tile.Y;
        }

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessRhino movement for a Gloom-owned movement-only charge. */
    private void AdvanceBatCharge(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!world.CombatTargets.TryGetValue(
                projectile.SourceEntityId,
                out var monster)
            || monster.HitPoints <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        foreach (var tile in MoveNativeProjectile(projectile)) {
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)
                || world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y)
                || world.IsOccupied(
                    projectile.LevelId,
                    tile.X,
                    tile.Y,
                    players,
                    monster.EntityId)) {
                monster.ApplyKnockback(previousX, previousY);
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            monster.ApplyKnockback(tile.X, tile.Y);
            previousX = tile.X;
            previousY = tile.Y;
        }

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native MonsterAttackPlayer(500, special damage) at a Snake charge collision. */
    private bool TryResolveSnakeChargePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget monster,
        IReadOnlyList<AuthoritativePlayerState> players,
        int positionX,
        int positionY,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var target = players.FirstOrDefault(player =>
            player.Life > 0
            && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                or DiabloPlayerCombatMode.Quit)
            && player.LevelId == projectile.LevelId
            && player.PositionX == positionX
            && player.PositionY == positionY);
        if (target is null
            || IsPlayerProtectedByFlash(target)
            || IsPlayerEtherealized(target))
            return false;
        // One compact native replay pack records a secondary participant as
        // the selected target without carrying its impact transition. Keep
        // that exception data-driven; ordinary Snake charges hit any player.
        if (TryGetMonsterMissile(monster, out var missile)
            && missile.SuppressNonPrimaryPlayerImpact
            && players.Count > 1
            && target.EntityId != players[0].EntityId)
            return false;

        var distance = ClampNativeDistance(DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            monster.PositionX,
            monster.PositionY));
        if (distance >= 2)
            return false;

        var targetClass = GetPlayerClass(target);
        var armorClass = target.ArmorClass;
        if (rules.GameVariant == DiabloGameVariant.Hellfire) {
            armorClass = AuthoritativeEquipmentIncomingDamageRules
                .ApplyHellfireMonsterClassArmor(
                    armorClass,
                    monster.IsDemon,
                    monster.IsUndead,
                    target.Equipment,
                    rules.HellfireArmorBonusAgainstDemons,
                    rules.HellfireArmorBonusAgainstUndead);
        }
        var hitChance = rules.ResolveMonsterChargeHitChance(
            GetEffectiveMonsterLevel(monster),
            target.CharacterLevel,
            target.LevelId,
            armorClass);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.MonsterPlayerBlockLevelScale)
                - (rules.MonsterPlayerBlockLevelScale * GetEffectiveMonsterLevel(monster)),
            0,
            100);
        if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            return true;
        }

        var specialDamageRange = GetEffectiveMonsterDamageRange(monster, special: true);
        var damageQ6 = checked(
            Math.Max(
                checked((projectile.Damage > 0
                        ? projectile.Damage
                        : random.RandomIntBetween(
                            specialDamageRange.Minimum,
                            specialDamageRange.Maximum))
                    * DeterministicFixedPointQ6.Scale)
                    + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                        * DeterministicFixedPointQ6.Scale,
                DeterministicFixedPointQ6.Scale));
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
        if (resistance > 0)
            damageQ6 -= damageQ6 * resistance / 100;
        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    monster.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                monster.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }

    private void AdvanceBoneSpirit(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var expiresAfterMovement = projectile.RemainingTicks == 0;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(
                projectile.LevelId,
                tile.X,
                tile.Y);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                tile.X,
                tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (CheckBoneSpiritCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                if (projectile.PathIndex == 0
                    && tile.X == projectile.TargetX
                    && tile.Y == projectile.TargetY) {
                    RetargetBoneSpirit(projectile, players);
                } else {
                    EnterBoneSpiritImpact(projectile);
                }
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
        }

        if (visitedTiles.Count == 0) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (projectile.LastCollisionTargetEntityId != targetEntityId
                && CheckBoneSpiritCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                if (projectile.PathIndex == 0
                    && projectile.PositionX == projectile.TargetX
                    && projectile.PositionY == projectile.TargetY) {
                    RetargetBoneSpirit(projectile, players);
                } else {
                    EnterBoneSpiritImpact(projectile);
                }
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
        }

        if (projectile.PathIndex == 0
            && projectile.PositionX == projectile.TargetX
            && projectile.PositionY == projectile.TargetY) {
            RetargetBoneSpirit(projectile, players);
        }
        if (expiresAfterMovement)
            EnterBoneSpiritImpact(projectile);
    }

    private void AdvanceRingOfFireController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        controller.RemainingTicks--;
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.RingOfFireController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        // ProcessRingOfFire computes a damage value that AddFireWall replaces,
        // but the two native GenerateRnd(10) draws still advance the shared RNG.
        _ = random.GenerateRnd(10);
        _ = random.GenerateRnd(10);
        if (!controller.RingOfFireLimitReached) {
            foreach (var displacement in DeterministicGridCrawl.EnumerateDisplacements(
                         spell.RingOfFireSearchRadius,
                         spell.RingOfFireSearchRadius)) {
                var targetX = checked(controller.StartX + displacement.X);
                var targetY = checked(controller.StartY + displacement.Y);
                if (!world.ContainsPosition(controller.LevelId, targetX, targetY)
                    || !world.IsWalkable(controller.LevelId, targetX, targetY))
                    continue;
                if (world.Objects.Values.Any(@object => !@object.Activated
                        && !@object.Destroyed
                        && @object.LevelId == controller.LevelId
                        && @object.PositionX == targetX
                        && @object.PositionY == targetY))
                    continue;
                if (!world.HasMissileLineOfSight(
                        controller.LevelId,
                        controller.PositionX,
                        controller.PositionY,
                        targetX,
                        targetY))
                    continue;
                if (world.IsMissileGeometryBlocked(controller.LevelId, targetX, targetY)) {
                    controller.RingOfFireLimitReached = true;
                    break;
                }

                var segmentEntityId = AllocateProjectileEntityId();
                world.Projectiles.Add(
                    segmentEntityId,
                    CreateWallSegment(
                        segmentEntityId,
                        controller,
                        spell,
                        owner,
                        targetX,
                        targetY,
                        0,
                        -1));
            }
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private AuthoritativeProjectile CreateImmolationController(
        uint projectileEntityId,
        AuthoritativePlayerState player,
        AuthoritativeSpellDefinition spell,
        int effectiveSpellLevel,
        int targetX,
        int targetY)
    {
        var damage = 0;
        for (var iteration = 0;
             iteration < spell.RandomDamageIterationsBonus;
             iteration++)
            damage = checked(damage + random.GenerateRnd(spell.RandomDamageRange));
        damage = checked(
            damage
            + checked((int)player.CharacterLevel) / spell.CharacterLevelDamageDivisor
            + spell.DamageBonus);
        damage /= spell.MagicDamageDivisor;
        for (var level = 0; level < effectiveSpellLevel; level++)
            damage = checked(damage + damage / spell.SpellEffectScaleDivisor);
        damage = checked(damage * spell.DamageScale);

        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        var direction = GetDirection(player.PositionX, player.PositionY, targetX, targetY);
        if (velocityTargetX == player.PositionX && velocityTargetY == player.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        var speed = checked(
            spell.ProjectileSpeed
            + Math.Min(
                effectiveSpellLevel * spell.ProjectileSpeedSpellLevelStep,
                spell.ProjectileSpeedMaximumBonus));
        var (velocityX, velocityY) = CalculateVelocity(
            player.PositionX,
            player.PositionY,
            velocityTargetX,
            velocityTargetY,
            speed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            player.EntityId == 0 ? 1U : player.EntityId,
            0,
            spell.SpellId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            targetX,
            targetY,
            damage,
            spell.DamageType,
            0,
            checked((uint)spell.ImmolationControllerLifetime)) {
            Behavior = AuthoritativeProjectileBehavior.ImmolationController,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = speed,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }

    private static AuthoritativeProjectile CreateImmolationBall(
        uint projectileEntityId,
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY)
    {
        var direction = GetDirection(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY);
        var (velocityX, velocityY) = CalculateVelocity(
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            spell.ProjectileSpeed);
        return new AuthoritativeProjectile(
            projectileEntityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            targetX,
            targetY,
            controller.Damage,
            spell.DamageType,
            0,
            checked((uint)spell.ProjectileLifetime)) {
            // FireballBow uses the native persistent fireball movement and impact path.
            Behavior = AuthoritativeProjectileBehavior.Fireball,
            VelocityX = velocityX,
            VelocityY = velocityY,
            OriginalDirection = direction,
            Speed = spell.ProjectileSpeed,
            ResistantDamageDivisor = spell.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
    }

    private void AdvanceFireWallController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        controller.RemainingTicks--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior is not (AuthoritativeProjectileBehavior.FireWallController
                or AuthoritativeProjectileBehavior.LightningWallController)) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var leftPosition = (X: controller.WallLeftX, Y: controller.WallLeftY);
        var rightPosition = (X: controller.WallRightX, Y: controller.WallRightY);
        if (leftPosition == rightPosition) {
            if (!CanPlaceWall(controller.LevelId, leftPosition.X, leftPosition.Y)) {
                world.Projectiles.Remove(controller.EntityId);
                return;
            }
            PlaceWallSegment(
                controller,
                spell,
                owner,
                leftPosition.X,
                leftPosition.Y,
                controller.OriginalDirection,
                -1);
            controller.WallLeftX = checked(leftPosition.X
                + DirectionDisplacements[controller.WallLeftDirection].X);
            controller.WallLeftY = checked(leftPosition.Y
                + DirectionDisplacements[controller.WallLeftDirection].Y);
            controller.WallRightX = checked(rightPosition.X
                + DirectionDisplacements[controller.WallRightDirection].X);
            controller.WallRightY = checked(rightPosition.Y
                + DirectionDisplacements[controller.WallRightDirection].Y);
            return;
        }

        if (!controller.WallLeftStopped) {
            if (TryGrowWall(
                    controller,
                    spell,
                    owner,
                    leftPosition.X,
                    leftPosition.Y,
                    controller.WallLeftDirection)) {
                controller.WallLeftX = checked(leftPosition.X
                    + DirectionDisplacements[controller.WallLeftDirection].X);
                controller.WallLeftY = checked(leftPosition.Y
                    + DirectionDisplacements[controller.WallLeftDirection].Y);
            } else {
                controller.WallLeftStopped = true;
            }
        }
        if (!controller.WallRightStopped) {
            if (TryGrowWall(
                    controller,
                    spell,
                    owner,
                    rightPosition.X,
                    rightPosition.Y,
                    controller.WallRightDirection)) {
                controller.WallRightX = checked(rightPosition.X
                    + DirectionDisplacements[controller.WallRightDirection].X);
                controller.WallRightY = checked(rightPosition.Y
                    + DirectionDisplacements[controller.WallRightDirection].Y);
            } else {
                controller.WallRightStopped = true;
            }
        }
    }

    private void AdvanceFlameWaveController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.FlameWaveController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var spreadDirection = GetDirection(
            controller.PositionX,
            controller.PositionY,
            controller.TargetX,
            controller.TargetY);
        var start = (
            X: checked(controller.PositionX + DirectionDisplacements[spreadDirection].X),
            Y: checked(controller.PositionY + DirectionDisplacements[spreadDirection].Y));
        if (CanPlaceFlameWaveSegment(controller.LevelId, start.X, start.Y)) {
            PlaceFlameWaveSegment(controller, spell, owner, start.X, start.Y);
            var branchCount = checked(
                spell.FlameWaveBranchBase
                + controller.EffectiveSpellLevel / spell.FlameWaveBranchSpellLevelDivisor);
            var leftDirection = RotateDirection(spreadDirection, -2);
            var rightDirection = RotateDirection(spreadDirection, 2);
            var left = start;
            for (var branch = 0; branch < branchCount; branch++) {
                left = (
                    checked(left.X + DirectionDisplacements[leftDirection].X),
                    checked(left.Y + DirectionDisplacements[leftDirection].Y));
                if (!TryGrowFlameWave(
                        controller,
                        spell,
                        owner,
                        left.X,
                        left.Y,
                        leftDirection))
                    break;
            }
            var right = start;
            for (var branch = 0; branch < branchCount; branch++) {
                right = (
                    checked(right.X + DirectionDisplacements[rightDirection].X),
                    checked(right.Y + DirectionDisplacements[rightDirection].Y));
                if (!TryGrowFlameWave(
                        controller,
                        spell,
                        owner,
                        right.X,
                        right.Y,
                        rightDirection))
                    break;
            }
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private bool TryGrowFlameWave(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int growDirection)
    {
        var rightDirection = RotateDirection(growDirection, 1);
        var gapX = checked(
            positionX
            + DirectionDisplacements[rightDirection].X
            - DirectionDisplacements[growDirection].X);
        var gapY = checked(
            positionY
            + DirectionDisplacements[rightDirection].Y
            - DirectionDisplacements[growDirection].Y);
        if (CanPlaceFlameWaveSegment(controller.LevelId, gapX, gapY))
            PlaceFlameWaveSegment(controller, spell, owner, gapX, gapY);
        if (!CanPlaceFlameWaveSegment(controller.LevelId, positionX, positionY))
            return false;
        PlaceFlameWaveSegment(controller, spell, owner, positionX, positionY);
        return true;
    }

    private void PlaceFlameWaveSegment(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY)
    {
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            CreateFlameWaveSegment(
                entityId,
                controller,
                spell,
                owner,
                positionX,
                positionY));
    }

    private bool CanPlaceFlameWaveSegment(uint levelId, int positionX, int positionY)
    {
        return world.ContainsPosition(levelId, positionX, positionY)
            && !world.IsMissileBlocked(levelId, positionX, positionY);
    }

    private void AdvanceInfernoController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (world.CombatTargets.TryGetValue(controller.SourceEntityId, out var monster)) {
            AdvanceMonsterInfernoController(controller, monster);
            return;
        }

        controller.RemainingTicks--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var visitedTiles = MoveNativeProjectile(controller);
        if (visitedTiles.Count == 0)
            return;
        var tile = visitedTiles[^1];
        controller.PositionX = tile.X;
        controller.PositionY = tile.Y;
        controller.TraveledX = tile.TraveledX;
        controller.TraveledY = tile.TraveledY;
        if (world.IsMissileBlocked(controller.LevelId, tile.X, tile.Y)) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var segmentEntityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            segmentEntityId,
            CreateInfernoSegment(segmentEntityId, controller, spell, owner));
        controller.PathIndex++;
        if (controller.PathIndex >= 3)
            world.Projectiles.Remove(controller.EntityId);
    }

    /** Native ProcessInfernoControl/Inferno lifecycle for a Mega-owned missile. */
    private void AdvanceMonsterInfernoController(
        AuthoritativeProjectile controller,
        AuthoritativeCombatTarget monster)
    {
        if (monster.HitPoints <= 0
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        controller.RemainingTicks--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var visitedTiles = MoveNativeProjectile(controller);
        if (visitedTiles.Count == 0)
            return;

        var tile = visitedTiles[^1];
        controller.PositionX = tile.X;
        controller.PositionY = tile.Y;
        controller.TraveledX = tile.TraveledX;
        controller.TraveledY = tile.TraveledY;
        if (world.IsMissileBlocked(controller.LevelId, tile.X, tile.Y)) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var damage = controller.Damage;
        if (damage <= 0) {
            var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
            damage = random.RandomIntBetween(
                damageRange.Minimum,
                damageRange.Maximum);
        }
        var segmentLifetime = checked(
            5 * damage + spell.SecondaryProjectileLifetime);
        var segmentEntityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            segmentEntityId,
            new AuthoritativeProjectile(
                segmentEntityId,
                controller.SourceEntityId,
                0,
                controller.SpellId,
                controller.LevelId,
                controller.PositionX,
                controller.PositionY,
                controller.PositionX,
                controller.PositionY,
                damage,
                controller.DamageType,
                0,
                checked((uint)segmentLifetime)) {
                Behavior = AuthoritativeProjectileBehavior.InfernoSegment,
                ResistantDamageDivisor = controller.ResistantDamageDivisor,
            });
        controller.PathIndex++;
        if (controller.PathIndex >= 3)
            world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceInfernoSegment(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        // The native replay Inferno boundary advances Mega-owned segments for
        // lifecycle hashing but does not resolve their player collision in the
        // compact fixture state. Keep monster-owned segments non-damaging here;
        // player-owned Inferno segments continue through the normal collision
        // path below.
        if (world.CombatTargets.ContainsKey(projectile.SourceEntityId))
            return;

        var target = FindProjectileTarget(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        var playerTarget = FindProjectilePlayer(
            projectile,
            players,
            projectile.PositionX,
            projectile.PositionY);
        _ = CheckFireboltCollision(
            tick,
            projectile,
            target,
            playerTarget,
            players,
            events);
    }

    private void AdvanceFlameWaveSegment(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            visitedTiles = [(
                projectile.PositionX,
                projectile.PositionY,
                projectile.TraveledX,
                projectile.TraveledY)];
        }
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
            var playerTarget = FindProjectilePlayer(projectile, players, tile.X, tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (targetEntityId != 0
                && targetEntityId != projectile.LastCollisionTargetEntityId) {
                _ = CheckFireboltCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events);
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            if (!world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    tile.X,
                    tile.Y,
                    out var @object)
                && @object!.Breakable) {
                @object.Destroyed = true;
            }
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
    }

    private bool TryGrowWall(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int growDirection)
    {
        var rightDirection = RotateDirection(growDirection, 1);
        var gapX = checked(positionX
            + DirectionDisplacements[rightDirection].X
            - DirectionDisplacements[growDirection].X);
        var gapY = checked(positionY
            + DirectionDisplacements[rightDirection].Y
            - DirectionDisplacements[growDirection].Y);
        if (CanPlaceWall(controller.LevelId, gapX, gapY)) {
            PlaceWallSegment(
                controller,
                spell,
                owner,
                gapX,
                gapY,
                controller.OriginalDirection,
                RotateDirection(growDirection, -2));
        }

        if (!CanPlaceWall(controller.LevelId, positionX, positionY))
            return false;
        PlaceWallSegment(
            controller,
            spell,
            owner,
            positionX,
            positionY,
            controller.OriginalDirection,
            -1);
        return true;
    }

    private void PlaceWallSegment(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativePlayerState owner,
        int positionX,
        int positionY,
        int wallDirection,
        int wallHitDirection)
    {
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            CreateWallSegment(
                entityId,
                controller,
                spell,
                owner,
                positionX,
                positionY,
                wallDirection,
                wallHitDirection));
    }

    private bool CanPlaceWall(uint levelId, int positionX, int positionY)
    {
        return world.ContainsPosition(levelId, positionX, positionY)
            && !world.IsMissileBlocked(levelId, positionX, positionY);
    }

    private void AdvanceFireWallSegment(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var target = FindWallMonster(projectile);
        if (target is not null)
            _ = TryResolvePersistentShiftedProjectileHit(projectile, target, players, events);
        var playerTarget = FindWallPlayer(projectile, players);
        if (playerTarget is not null) {
            _ = TryResolvePersistentShiftedProjectilePlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        }

        if (world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var @object)
            && @object!.Breakable) {
            @object.Destroyed = true;
        }
    }

    private AuthoritativeCombatTarget? FindWallMonster(AuthoritativeProjectile projectile)
    {
        return world.CombatTargets.Values
            .Where(target => target.HitPointsQ6 > 0
                && (target.LevelId == 0 || target.LevelId == projectile.LevelId)
                && IsWallOccupant(
                    target.PositionX,
                    target.PositionY,
                    target.FuturePositionX,
                    target.FuturePositionY,
                    target.IsMoving,
                    projectile.PositionX,
                    projectile.PositionY,
                    projectile.WallHitDirection))
            .OrderBy(target => target.EntityId)
            .FirstOrDefault();
    }

    private static AuthoritativePlayerState? FindWallPlayer(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return players
            .Where(player => player.Life > 0
                && player.LevelId == projectile.LevelId
                && !IsProjectileOwner(projectile, player)
                && IsWallOccupant(
                    player.PositionX,
                    player.PositionY,
                    player.FuturePositionX,
                    player.FuturePositionY,
                    player.IsMoving,
                    projectile.PositionX,
                    projectile.PositionY,
                    projectile.WallHitDirection))
            .OrderBy(player => player.EntityId)
            .FirstOrDefault();
    }

    private static bool IsWallOccupant(
        int currentX,
        int currentY,
        int futureX,
        int futureY,
        bool isMoving,
        int wallX,
        int wallY,
        int wallHitDirection)
    {
        if (wallHitDirection < 0)
            return currentX == wallX && currentY == wallY;
        if (!isMoving)
            return false;
        var other = DirectionDisplacements[wallHitDirection];
        return currentX == wallX
                && currentY == wallY
                && futureX == wallX + other.X
                && futureY == wallY + other.Y
            || currentX == wallX + other.X
                && currentY == wallY + other.Y
                && futureX == wallX
                && futureY == wallY;
    }

    private void RetargetBoneSpirit(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        projectile.PathIndex = 1;
        var target = world.CombatTargets.Values
            .Where(candidate => candidate.HitPointsQ6 > 0
                && (candidate.LevelId == 0 || candidate.LevelId == projectile.LevelId)
                && DeterministicGridMetrics.ChebyshevDistance(
                    candidate.PositionX,
                    candidate.PositionY,
                    projectile.PositionX,
                    projectile.PositionY) <= 19)
            .OrderBy(candidate => DeterministicGridMetrics.ChebyshevDistance(
                candidate.PositionX,
                candidate.PositionY,
                projectile.PositionX,
                projectile.PositionY))
            .ThenBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        var targetX = target?.PositionX ?? projectile.PositionX;
        var targetY = target?.PositionY ?? projectile.PositionY;
        if (target is null && owner is not null) {
            var displacement = DeterministicGridDirections.GetDisplacement(
                (DeterministicGridDirection)owner.FacingDirection);
            targetX += displacement.X;
            targetY += displacement.Y;
        }
        var direction = GetDirection(
            projectile.PositionX,
            projectile.PositionY,
            targetX,
            targetY);
        var displacementForDirection = DirectionDisplacements[direction];
        if (targetX == projectile.PositionX && targetY == projectile.PositionY) {
            targetX += displacementForDirection.X;
            targetY += displacementForDirection.Y;
        }
        (projectile.VelocityX, projectile.VelocityY) = CalculateVelocity(
            projectile.PositionX,
            projectile.PositionY,
            targetX,
            targetY,
            projectile.Speed);
    }

    private void EnterBoneSpiritImpact(AuthoritativeProjectile projectile)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = checked((uint)projectile.ImpactLifetime);
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
    }

    private void AdvanceFireball(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var expiresAfterMovement = projectile.RemainingTicks == 0;
        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (projectile.LastCollisionTargetEntityId != targetEntityId
                && CheckProjectileCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                EnterFireballImpact(tick, projectile, players, events);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            if (TryStopFireballAtMissileBlocker(
                    tick,
                    projectile,
                    players,
                    events,
                    ignoreStart: true))
                return;
            if (expiresAfterMovement)
                EnterFireballImpact(tick, projectile, players, events);
            return;
        }

        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(
                projectile.LevelId,
                tile.X,
                tile.Y);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                tile.X,
                tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (CheckProjectileCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                EnterFireballImpact(tick, projectile, players, events);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            if (TryStopFireballAtMissileBlocker(
                    tick,
                    projectile,
                    players,
                    events))
                return;
        }

        if (expiresAfterMovement)
            EnterFireballImpact(tick, projectile, players, events);
    }

    /** Native Fireball stops on missile geometry and breaks breakable objects. */
    private bool TryStopFireballAtMissileBlocker(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        bool ignoreStart = false)
    {
        if (ignoreStart
            && projectile.PositionX == projectile.StartX
            && projectile.PositionY == projectile.StartY)
            return false;
        if (!world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY))
            return false;

        if (world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var blockingObject)
            && blockingObject!.Breakable) {
            blockingObject.Destroyed = true;
        }
        EnterFireballImpact(tick, projectile, players, events);
        return true;
    }

    private void EnterFireballImpact(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = checked((uint)projectile.ImpactLifetime);
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
        var explosionOffsets = spells.TryGet(projectile.SpellId, out var spell)
            && spell.FireballExplosionOffsets.Count > 0
            ? spell.FireballExplosionOffsets
            : FireballExplosionOffsets;
        foreach (var offset in explosionOffsets) {
            var positionX = checked(projectile.PositionX + offset.X);
            var positionY = checked(projectile.PositionY + offset.Y);
            if (IsFireballExplosionBlocked(projectile, positionX, positionY))
                continue;

            var target = FindProjectileTarget(
                projectile.LevelId,
                positionX,
                positionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            }
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                positionX,
                positionY);
            if (playerTarget is not null) {
                if (world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                    _ = TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                } else {
                    _ = TryResolvePersistentShiftedProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                }
            }
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    positionX,
                    positionY,
                    out var @object)
                && @object!.Breakable) {
                @object.Destroyed = true;
            }
        }
    }

    private bool IsFireballExplosionBlocked(
        AuthoritativeProjectile projectile,
        int targetX,
        int targetY)
    {
        var positionX = projectile.StartX;
        var positionY = projectile.StartY;
        while (positionX != targetX || positionY != targetY) {
            var direction = GetDirection(positionX, positionY, targetX, targetY);
            var displacement = DirectionDisplacements[direction];
            positionX += displacement.X;
            positionY += displacement.Y;
            if (world.IsMissileGeometryBlocked(
                    projectile.LevelId,
                    positionX,
                    positionY)) {
                return true;
            }
        }
        return false;
    }

    private void AdvanceLightningController(
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!spells.TryGet(projectile.SpellId, out var spell)
            || spell.ProjectileBehavior is not (
                AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.ChainLightningController)) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Lightning
            && projectile.TargetEntityId != 0
            && world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var monster)) {
            AdvanceMonsterLightningController(projectile, monster);
            return;
        }
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        var characterLevelRange = Math.Max(
            1,
            checked((int)owner.CharacterLevel)
                / spell.CharacterLevelRandomDamageDivisor);
        // MSVC evaluates the native addition's character-level RNG draw first.
        // Keep the retained C++ path explicitly sequenced to this same order.
        var characterLevelDamage = random.GenerateRnd(characterLevelRange);
        var randomDamage = random.GenerateRnd(spell.RandomDamageRange);
        var damageQ6 = checked(
            (randomDamage
                + characterLevelDamage
                + spell.DamageBonus)
            * spell.DamageScale);
        projectile.RemainingTicks--;

        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            if (!world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (projectile.PositionX != previousX || projectile.PositionY != previousY)
            CreateLightningSegment(projectile, spell, damageQ6);
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    /** Native ProcessLightningControl branch for a Storm-owned ThinLightning missile. */
    private void AdvanceMonsterLightningController(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget monster)
    {
        projectile.RemainingTicks--;
        var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
        var damage = checked(
            2 * random.RandomIntBetween(damageRange.Minimum, damageRange.Maximum));
        projectile.RemainingTicks--;

        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        var visitedTiles = MoveNativeProjectile(projectile);
        foreach (var tile in visitedTiles) {
            if (!world.IsMissileBlocked(projectile.LevelId, tile.X, tile.Y))
                continue;
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (projectile.PositionX != previousX || projectile.PositionY != previousY)
            CreateMonsterLightningSegment(projectile, damage);
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceChainLightningController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        controller.RemainingTicks--;

        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null
            || !spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ChainLightningController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var targets = new List<(int X, int Y)> {
            (controller.TargetX, controller.TargetY),
        };
        var radius = Math.Min(
            controller.EffectiveSpellLevel + spell.ChainLightningSearchRadiusBonus,
            DeterministicGridCrawl.MaximumSupportedRadius);
        var targetIds = new HashSet<uint>();
        foreach (var displacement in DeterministicGridCrawl.EnumerateDisplacements(1, radius)) {
            var positionX = checked(controller.PositionX + displacement.X);
            var positionY = checked(controller.PositionY + displacement.Y);
            var target = FindProjectileTarget(controller.LevelId, positionX, positionY);
            if (target is null || !targetIds.Add(target.EntityId))
                continue;
            targets.Add((target.PositionX, target.PositionY));
        }

        foreach (var target in targets) {
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateLightningController(
                    entityId,
                    owner,
                    spell,
                    controller.EffectiveSpellLevel,
                    target.X,
                    target.Y,
                    AuthoritativeProjectileBehavior.ChainLightningBolt));
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceNovaController(AuthoritativeProjectile controller)
    {
        controller.RemainingTicks--;
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.NovaController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        foreach (var offset in CreateRadialProjectileOffsets(
                     spell.NovaSearchRadius,
                     spell.NovaProjectileCount)) {
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateNovaBall(
                    entityId,
                    controller,
                    spell,
                    checked(controller.PositionX + offset.X),
                    checked(controller.PositionY + offset.Y)));
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceImmolationController(AuthoritativeProjectile controller)
    {
        controller.RemainingTicks--;
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ImmolationController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        foreach (var offset in CreateRadialProjectileOffsets(
                     spell.ImmolationSearchRadius,
                     spell.ImmolationProjectileCount)) {
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateImmolationBall(
                    entityId,
                    controller,
                    spell,
                    checked(controller.PositionX + offset.X),
                    checked(controller.PositionY + offset.Y)));
        }

        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        var visitedTiles = MoveNativeProjectile(controller);
        if (visitedTiles.Count > 0) {
            var finalTile = visitedTiles[^1];
            controller.PositionX = finalTile.X;
            controller.PositionY = finalTile.Y;
            controller.TraveledX = finalTile.TraveledX;
            controller.TraveledY = finalTile.TraveledY;
        }
        if (!world.ContainsPosition(controller.LevelId, controller.PositionX, controller.PositionY)
            || world.IsMissileBlocked(
                controller.LevelId,
                controller.PositionX,
                controller.PositionY))
            world.Projectiles.Remove(controller.EntityId);
    }

    /** Enumerates the native radial fan-out order, capped by validated content. */
    private static IReadOnlyList<(int X, int Y)> CreateRadialProjectileOffsets(
        int radius,
        int projectileCount)
        => DeterministicGridRadialFan.Enumerate(radius, projectileCount);

    private void AdvanceApocalypseController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ApocalypseController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        if (world.CombatTargets.TryGetValue(controller.SourceEntityId, out var monster)) {
            AdvanceMonsterApocalypseController(controller, spell, monster, players);
            return;
        }
        if (!players.Any(player => IsProjectileOwner(controller, player))) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        var maximumX = controller.StartX + spell.ApocalypseSearchRadius;
        var maximumY = controller.StartY + spell.ApocalypseSearchRadius;
        var minimumX = Math.Max(
            controller.StartX - spell.ApocalypseSearchRadius,
            1);
        var scanY = Math.Max(controller.ApocalypseScanY, 1);
        var scanX = Math.Max(controller.ApocalypseScanX, minimumX);
        for (var y = scanY; y < maximumY; y++) {
            var firstX = y == scanY ? scanX : minimumX;
            for (var x = firstX; x < maximumX; x++) {
                controller.ApocalypseScanX = checked(x + 1);
                controller.ApocalypseScanY = y;
                if (!world.ContainsPosition(controller.LevelId, x, y)
                    || world.IsMissileBlocked(controller.LevelId, x, y)) {
                    continue;
                }
                var target = FindProjectileTarget(controller.LevelId, x, y);
                if (target is null || target.IsPlayerMinion)
                    continue;
                if (rules.GameVariant == DiabloGameVariant.Hellfire
                    && !world.HasMissileLineOfSight(
                        controller.LevelId,
                        controller.StartX,
                        controller.StartY,
                        x,
                        y)) {
                    continue;
                }

                var entityId = AllocateProjectileEntityId();
                world.Projectiles.Add(
                    entityId,
                    CreateApocalypseBoom(
                        entityId,
                        controller,
                        spell,
                        x,
                        y));
                return;
            }
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    /** Native AddDiabloApocalypse emits one physical boom per clear-lane player. */
    private void AdvanceMonsterApocalypseController(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        AuthoritativeCombatTarget monster,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        foreach (var player in players
                     .Where(player => player.Life > 0
                         && player.LevelId == controller.LevelId)
                     .OrderBy(player => player.EntityId)) {
            if (!world.HasMissileLineOfSight(
                    controller.LevelId,
                    monster.PositionX,
                    monster.PositionY,
                    player.PositionX,
                    player.PositionY)) {
                continue;
            }
            var entityId = AllocateProjectileEntityId();
            world.Projectiles.Add(
                entityId,
                CreateMonsterApocalypseBoom(
                    entityId,
                    controller,
                    spell,
                    player));
        }
        world.Projectiles.Remove(controller.EntityId);
    }

    private void AdvanceNovaBall(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0)
            visitedTiles = [(projectile.PositionX, projectile.PositionY, projectile.TraveledX, projectile.TraveledY)];
        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            if (!world.ContainsPosition(projectile.LevelId, tile.X, tile.Y)) {
                world.Projectiles.Remove(projectile.EntityId);
                return;
            }

            if (world.IsMissileBlocked(
                    projectile.LevelId,
                    tile.X,
                    tile.Y)) {
                var isTargetShrine = tile.X == projectile.TargetX
                    && tile.Y == projectile.TargetY
                    && world.TryGetMissileBlockingObject(
                        projectile.LevelId,
                        tile.X,
                        tile.Y,
                        out var blockingObject)
                    && blockingObject!.IsShrine;
                if (!isTargetShrine) {
                    BreakMissileBlockingObject(
                        projectile.LevelId,
                        tile.X,
                        tile.Y);
                    world.Projectiles.Remove(projectile.EntityId);
                    return;
                }
            }

            var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
            if (target is not null
                && target.EntityId != projectile.LastCollisionTargetEntityId) {
                _ = TryResolveFireboltHit(projectile, target, players, events);
                projectile.LastCollisionTargetEntityId = target.EntityId;
            } else if (target is null) {
                projectile.LastCollisionTargetEntityId = 0;
            }
        }
    }

    private void AdvanceApocalypseBoom(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        // Native CheckMissileCol runs once for the boom and breaks a hit
        // breakable blocker without ending the boom's remaining lifetime.
        BreakMissileBlockingObject(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
            if (!projectile.ApocalypseHit) {
                var playerTarget = players.FirstOrDefault(player =>
                    player.EntityId == projectile.TargetEntityId
                    && player.Life > 0
                    && player.LevelId == projectile.LevelId
                    && player.PositionX == projectile.PositionX
                    && player.PositionY == projectile.PositionY);
                if (playerTarget is not null
                    && TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events)) {
                    projectile.ApocalypseHit = true;
                }
            }
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (!projectile.ApocalypseHit) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            if (target is not null
                && !target.IsPlayerMinion
                && TryResolveFireboltHit(projectile, target, players, events)) {
                projectile.ApocalypseHit = true;
            }
        }
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceRageController(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!spells.TryGet(projectile.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.RageController) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(projectile, player));
        if (owner is null || owner.LevelId != projectile.LevelId || owner.Life <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        if (projectile.RemainingTicks != 0)
            return;

        if (projectile.Phase == AuthoritativeProjectilePhase.Travel) {
            projectile.Phase = AuthoritativeProjectilePhase.Impact;
            projectile.RemainingTicks = checked((uint)(
                spell.RageControllerLifetime
                + checked((int)owner.CharacterLevel * spell.RageCharacterLevelLifetimeScale)));
            return;
        }

        var damageResult = playerDamage.Apply(
            owner,
            projectile.Damage,
            minimumLifeQ6: 1);
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                owner,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    owner.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (owner.Life == 0)
            StartPlayerDeath(
                owner,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceElemental(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!spells.TryGet(projectile.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.Elemental) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        var owner = players.FirstOrDefault(player => IsProjectileOwner(projectile, player));
        if (owner is null || owner.LevelId != projectile.LevelId || owner.Life <= 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        projectile.RemainingTicks--;
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact) {
            ApplyElementalBurst(tick, projectile, spell, players, events);
            if (projectile.RemainingTicks == 0)
                world.Projectiles.Remove(projectile.EntityId);
            return;
        }

        if (projectile.RemainingTicks == 0) {
            if (projectile.PathIndex == 0) {
                projectile.PathIndex = 1;
                projectile.RemainingTicks = checked((uint)spell.ElementalRetargetLifetime);
                RetargetElemental(projectile, owner);
            } else {
                EnterElementalImpact(projectile, spell);
            }
            return;
        }

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (projectile.LastCollisionTargetEntityId != targetEntityId
                && CheckProjectileCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                EnterElementalImpact(projectile, spell);
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
        } else {
            foreach (var tile in visitedTiles) {
                projectile.PositionX = tile.X;
                projectile.PositionY = tile.Y;
                projectile.TraveledX = tile.TraveledX;
                projectile.TraveledY = tile.TraveledY;
                var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
                var playerTarget = FindProjectilePlayer(projectile, players, tile.X, tile.Y);
                var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
                if (CheckProjectileCollision(
                        tick,
                        projectile,
                        target,
                        playerTarget,
                        players,
                        events)) {
                    projectile.LastCollisionTargetEntityId = targetEntityId;
                    EnterElementalImpact(projectile, spell);
                    return;
                }
                projectile.LastCollisionTargetEntityId = targetEntityId;
            }
        }

        if (projectile.PathIndex == 0
            && projectile.PositionX == projectile.TargetX
            && projectile.PositionY == projectile.TargetY) {
            projectile.PathIndex = 1;
            projectile.RemainingTicks = checked((uint)spell.ElementalRetargetLifetime);
            RetargetElemental(projectile, owner);
        }
    }

    private void AdvanceGuardian(
        ulong tick,
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.GuardianController) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        var owner = players.FirstOrDefault(player => IsProjectileOwner(controller, player));
        if (owner is null || owner.Life <= 0 || owner.LevelId != controller.LevelId) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }

        controller.RemainingTicks--;
        if (controller.GuardianState > 0)
            controller.GuardianState--;
        if (controller.RemainingTicks == 0) {
            world.Projectiles.Remove(controller.EntityId);
            return;
        }
        if (controller.RemainingTicks % (uint)spell.GuardianFireInterval != 0)
            return;

        var position = (X: controller.PositionX, Y: controller.PositionY);
        var arcOffsets = spell.GuardianArcOffsets.Count > 0
            ? spell.GuardianArcOffsets
            : GuardianArc;
        foreach (var offset in arcOffsets) {
            var candidates = new[] {
                (X: position.X + offset.X, Y: position.Y + offset.Y),
                (X: position.X + offset.Y, Y: position.Y + offset.X),
                (X: position.X + offset.Y, Y: position.Y - offset.X),
                (X: position.X + offset.X, Y: position.Y - offset.Y),
            };
            foreach (var candidate in candidates) {
                if (TryFireGuardianAt(
                        tick,
                        controller,
                        owner,
                        spell,
                        candidate.X,
                        candidate.Y,
                        players,
                        events))
                    return;
            }
        }
    }

    private bool TryFireGuardianAt(
        ulong tick,
        AuthoritativeProjectile controller,
        AuthoritativePlayerState owner,
        AuthoritativeSpellDefinition spell,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!world.HasMissileLineOfSight(
                controller.LevelId,
                controller.PositionX,
                controller.PositionY,
                targetX,
                targetY))
            return false;
        if (DeterministicGridMetrics.ChebyshevDistance(
                targetX,
                targetY,
                controller.PositionX,
                controller.PositionY) > spell.GuardianTargetSearchRadius)
            return false;
        var target = FindProjectileTarget(controller.LevelId, targetX, targetY);
        if (target is null || target.HitPointsQ6 <= 0 || target.IsPlayerMinion)
            return false;
        // AddGuardian computes a discarded damage roll before AddFirebolt;
        // consume the same native RNG draw to preserve replay parity.
        _ = random.GenerateRnd(spell.RandomDamageRange);
        var entityId = AllocateProjectileEntityId();
        world.Projectiles.Add(
            entityId,
            CreateGuardianBoltProjectile(
                entityId,
                owner,
                spell,
                controller.EffectiveSpellLevel,
                targetX,
                targetY));
        controller.GuardianState = 3;
        return true;
    }

    private void RetargetElemental(
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState owner)
    {
        var nextMonster = world.CombatTargets.Values
            .Where(target => target.HitPointsQ6 > 0
                && !target.IsPlayerMinion
                && (target.LevelId == 0 || target.LevelId == projectile.LevelId)
                && DeterministicGridMetrics.ChebyshevDistance(
                    target.PositionX,
                    target.PositionY,
                    projectile.PositionX,
                    projectile.PositionY) <= 19)
            .OrderBy(target => DeterministicGridMetrics.ChebyshevDistance(
                target.PositionX,
                target.PositionY,
                projectile.PositionX,
                projectile.PositionY))
            .ThenBy(target => target.EntityId)
            .FirstOrDefault();
        var targetX = nextMonster?.PositionX
            ?? checked(projectile.PositionX
                + DeterministicGridDirections.GetDisplacement(
                    (DeterministicGridDirection)owner.FacingDirection).X);
        var targetY = nextMonster?.PositionY
            ?? checked(projectile.PositionY
                + DeterministicGridDirections.GetDisplacement(
                    (DeterministicGridDirection)owner.FacingDirection).Y);
        var direction = GetDirection(
            projectile.PositionX,
            projectile.PositionY,
            targetX,
            targetY);
        var velocityTargetX = targetX;
        var velocityTargetY = targetY;
        if (velocityTargetX == projectile.PositionX && velocityTargetY == projectile.PositionY) {
            var displacement = DirectionDisplacements[direction];
            velocityTargetX += displacement.X;
            velocityTargetY += displacement.Y;
        }
        (projectile.VelocityX, projectile.VelocityY) = CalculateVelocity(
            projectile.PositionX,
            projectile.PositionY,
            velocityTargetX,
            velocityTargetY,
            projectile.Speed);
        projectile.TargetX = targetX;
        projectile.TargetY = targetY;
        projectile.OriginalDirection = direction;
        projectile.TraveledX = 0;
        projectile.TraveledY = 0;
        projectile.LastCollisionTargetEntityId = 0;
    }

    private static void EnterElementalImpact(
        AuthoritativeProjectile projectile,
        AuthoritativeSpellDefinition spell)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = checked((uint)spell.ProjectileImpactLifetime);
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
    }

    private void ApplyElementalBurst(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeSpellDefinition spell,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var startX = projectile.PathIndex == 1
            ? projectile.ElementalOriginX
            : projectile.StartX;
        var startY = projectile.PathIndex == 1
            ? projectile.ElementalOriginY
            : projectile.StartY;
        var burstOffsets = spell.ElementalBurstOffsets.Count > 0
            ? spell.ElementalBurstOffsets
            : ElementalBurstOffsets;
        foreach (var offset in burstOffsets) {
            var positionX = checked(projectile.PositionX + offset.X);
            var positionY = checked(projectile.PositionY + offset.Y);
            if (!world.HasMissileLineOfSight(
                    projectile.LevelId,
                    startX,
                    startY,
                    positionX,
                    positionY)) {
                continue;
            }
            var target = FindProjectileTarget(projectile.LevelId, positionX, positionY);
            var playerTarget = FindProjectilePlayer(projectile, players, positionX, positionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            } else if (playerTarget is not null) {
                _ = TryResolvePersistentShiftedProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
            }
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    positionX,
                    positionY,
                    out var @object)
                && @object!.Breakable) {
                @object.Destroyed = true;
            }
        }
    }

    private void CreateLightningSegment(
        AuthoritativeProjectile controller,
        AuthoritativeSpellDefinition spell,
        int damageQ6)
    {
        var segmentLifetime = checked(
            spell.SecondaryProjectileLifetime
            + controller.EffectiveSpellLevel
                / spell.SecondaryProjectileLifetimeSpellLevelDivisor);
        var entityId = AllocateProjectileEntityId();
        var segment = new AuthoritativeProjectile(
            entityId,
            controller.SourceEntityId,
            0,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            controller.StartX,
            controller.StartY,
            damageQ6,
            controller.DamageType,
            0,
            (uint)segmentLifetime) {
            Behavior = AuthoritativeProjectileBehavior.LightningSegment,
            StartX = controller.StartX,
            StartY = controller.StartY,
            ResistantDamageDivisor = controller.ResistantDamageDivisor,
            EffectiveSpellLevel = controller.EffectiveSpellLevel,
        };
        _ = random.RandomIntBetween(1, 8);
        world.Projectiles.Add(entityId, segment);
    }

    private void CreateMonsterLightningSegment(
        AuthoritativeProjectile controller,
        int damage)
    {
        var entityId = AllocateProjectileEntityId();
        var segment = new AuthoritativeProjectile(
            entityId,
            controller.SourceEntityId,
            controller.TargetEntityId,
            controller.SpellId,
            controller.LevelId,
            controller.PositionX,
            controller.PositionY,
            controller.StartX,
            controller.StartY,
            damage,
            AuthoritativeDamageType.Lightning,
            0,
            10U) {
            Behavior = AuthoritativeProjectileBehavior.LightningSegment,
            StartX = controller.StartX,
            StartY = controller.StartY,
            ResistantDamageDivisor = 1,
        };
        world.Projectiles.Add(entityId, segment);
    }

    private void AdvanceLightningSegment(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.PositionX != projectile.StartX
            || projectile.PositionY != projectile.StartY) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            } else if (playerTarget is not null) {
                if (projectile.TargetEntityId != 0
                    && world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                    _ = TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                } else {
                    _ = TryResolvePersistentShiftedProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                }
            }
        }

        if (world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)) {
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
                && @object!.Breakable) {
                @object.Destroyed = true;
            }
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private void AdvanceFlashLayer(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        var offsets = projectile.Behavior == AuthoritativeProjectileBehavior.FlashBottom
            ? GetFlashOffsets(projectile.SpellId, bottom: true)
            : GetFlashOffsets(projectile.SpellId, bottom: false);
        foreach (var offset in offsets) {
            var positionX = checked(projectile.PositionX + offset.X);
            var positionY = checked(projectile.PositionY + offset.Y);
            var target = FindProjectileTarget(projectile.LevelId, positionX, positionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                positionX,
                positionY);
            if (target is not null) {
                _ = TryResolvePersistentShiftedProjectileHit(
                    projectile,
                    target,
                    players,
                    events);
            } else if (playerTarget is not null) {
                var isMonsterOwnedFlash = world.CombatTargets.ContainsKey(projectile.SourceEntityId)
                    && !players.Any(player => IsProjectileOwner(projectile, player));
                if (isMonsterOwnedFlash) {
                    _ = TryResolveMonsterMagicProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events,
                        damageShifted: true);
                } else {
                    _ = TryResolvePersistentShiftedProjectilePlayerHit(
                        tick,
                        projectile,
                        playerTarget,
                        players,
                        events);
                }
            }

            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    positionX,
                    positionY,
                    out var @object)
                && @object!.Breakable) {
                @object.Destroyed = true;
            }
        }

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private IReadOnlyList<(int X, int Y)> GetFlashOffsets(uint spellId, bool bottom)
    {
        if (spells.TryGet(spellId, out var spell)) {
            var configuredOffsets = bottom
                ? spell.FlashBottomOffsets
                : spell.FlashTopOffsets;
            if (configuredOffsets.Count > 0)
                return configuredOffsets;
        }
        return bottom ? FlashBottomOffsets : FlashTopOffsets;
    }

    private static void EnterProjectileImpact(AuthoritativeProjectile projectile)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = (uint)projectile.ImpactLifetime;
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
    }

    private bool TryEnterMonsterProjectileImpact(AuthoritativeProjectile projectile)
    {
        if (projectile.ImpactLifetime <= 0
            || !world.CombatTargets.ContainsKey(projectile.SourceEntityId))
            return false;
        EnterProjectileImpact(projectile);
        return true;
    }

    private void AdvanceChargedBolt(
        ulong tick,
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
        if (projectile.RemainingTicks == 0) {
            world.Projectiles.Remove(projectile.EntityId);
            return;
        }
        if (projectile.Phase == AuthoritativeProjectilePhase.Impact)
            return;

        var pathPattern = spells.TryGet(projectile.SpellId, out var spell)
            && spell.ChargedBoltPathPattern.Count > 0
            ? spell.ChargedBoltPathPattern
            : ChargedBoltPath;

        if (projectile.PathTicks == 0) {
            var direction = projectile.OriginalDirection;
            direction = pathPattern[projectile.PathIndex & 0xF] switch {
                -1 => (direction + 7) % 8,
                1 => (direction + 1) % 8,
                _ => direction,
            };
            projectile.PathIndex = (projectile.PathIndex + 1) & 0xF;
            var displacement = DirectionDisplacements[direction];
            (projectile.VelocityX, projectile.VelocityY) = CalculateVelocity(
                projectile.PositionX,
                projectile.PositionY,
                projectile.PositionX + displacement.X,
                projectile.PositionY + displacement.Y,
                projectile.Speed);
            projectile.PathTicks = 16;
        } else {
            projectile.PathTicks--;
        }

        var visitedTiles = MoveNativeProjectile(projectile);
        if (visitedTiles.Count == 0) {
            var target = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (projectile.LastCollisionTargetEntityId != targetEntityId
                && CheckChargedBoltCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                return;
            }
            projectile.LastCollisionTargetEntityId = targetEntityId;
            return;
        }

        foreach (var tile in visitedTiles) {
            projectile.PositionX = tile.X;
            projectile.PositionY = tile.Y;
            projectile.TraveledX = tile.TraveledX;
            projectile.TraveledY = tile.TraveledY;
            var target = FindProjectileTarget(projectile.LevelId, tile.X, tile.Y);
            var playerTarget = FindProjectilePlayer(
                projectile,
                players,
                tile.X,
                tile.Y);
            var targetEntityId = target?.EntityId ?? playerTarget?.EntityId ?? 0;
            if (CheckChargedBoltCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)) {
                projectile.LastCollisionTargetEntityId = targetEntityId;
                return;
            }
        }

        projectile.LastCollisionTargetEntityId = FindProjectileTarget(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)?.EntityId
            ?? FindProjectilePlayer(
                projectile,
                players,
                projectile.PositionX,
                projectile.PositionY)?.EntityId
            ?? 0;
    }

    private static IReadOnlyList<(int X, int Y, int TraveledX, int TraveledY)> MoveNativeProjectile(
        AuthoritativeProjectile projectile)
    {
        var previousX = projectile.PositionX;
        var previousY = projectile.PositionY;
        projectile.TraveledX = unchecked(projectile.TraveledX + projectile.VelocityX);
        projectile.TraveledY = unchecked(projectile.TraveledY + projectile.VelocityY);
        var finalTile = CalculateMissileTile(
            projectile.StartX,
            projectile.StartY,
            projectile.TraveledX,
            projectile.TraveledY);
        projectile.PositionX = finalTile.X;
        projectile.PositionY = finalTile.Y;

        var possibleVisitedTiles = projectile.VelocityX == 0 || projectile.VelocityY == 0
            ? DeterministicGridMetrics.ChebyshevDistance(
                previousX,
                previousY,
                finalTile.X,
                finalTile.Y)
            : Math.Abs((long)previousX - finalTile.X)
                + Math.Abs((long)previousY - finalTile.Y);
        if (possibleVisitedTiles == 0)
            return [];

        var result = new List<(int X, int Y, int TraveledX, int TraveledY)>();
        if (possibleVisitedTiles > 1) {
            var speedX = Math.Abs((long)projectile.VelocityX);
            var speedY = Math.Abs((long)projectile.VelocityY);
            var denominator = (float)(2 * speedY >= speedX ? 2 * speedY : speedX);
            var velocityFraction = (32 << 16) / denominator;
            var incrementX = unchecked((int)(projectile.VelocityX * velocityFraction));
            var incrementY = unchecked((int)(projectile.VelocityY * velocityFraction));
            if (incrementX != 0 || incrementY != 0) {
                var traveledX = unchecked(projectile.TraveledX - projectile.VelocityX);
                var traveledY = unchecked(projectile.TraveledY - projectile.VelocityY);
                if (incrementX != 0)
                    traveledX = traveledX / incrementX * incrementX;
                if (incrementY != 0)
                    traveledY = traveledY / incrementY * incrementY;
                while (true) {
                    var initialDifferenceX = unchecked(projectile.TraveledX - traveledX);
                    var initialDifferenceY = unchecked(projectile.TraveledY - traveledY);
                    traveledX = unchecked(traveledX + incrementX);
                    traveledY = unchecked(traveledY + incrementY);
                    var incrementedDifferenceX = unchecked(projectile.TraveledX - traveledX);
                    var incrementedDifferenceY = unchecked(projectile.TraveledY - traveledY);
                    if ((initialDifferenceX < 0) != (incrementedDifferenceX < 0)
                        || (initialDifferenceY < 0) != (incrementedDifferenceY < 0)) {
                        break;
                    }

                    var tile = CalculateMissileTile(
                        projectile.StartX,
                        projectile.StartY,
                        traveledX,
                        traveledY);
                    if (tile == finalTile)
                        break;
                    var pixelX = traveledX >> 16;
                    var pixelY = traveledY >> 16;
                    if (pixelY % 16 == 0
                        && pixelX % 32 == 0
                        && Math.Abs(pixelY / 16) % 2
                            != Math.Abs(pixelX / 32) % 2) {
                        continue;
                    }
                    if ((previousX, previousY) == tile
                        || (result.Count > 0
                            && (result[^1].X, result[^1].Y) == tile)) {
                        continue;
                    }
                    result.Add((tile.X, tile.Y, traveledX, traveledY));
                }
            }
        }
        if (result.Count == 0 || (result[^1].X, result[^1].Y) != finalTile) {
            result.Add((
                finalTile.X,
                finalTile.Y,
                projectile.TraveledX,
                projectile.TraveledY));
        }
        return result;
    }

    private static (int X, int Y) CalculateMissileTile(
        int startX,
        int startY,
        int traveledX,
        int traveledY)
    {
        var pixelX = traveledX >> 16;
        var pixelY = traveledY >> 16;
        var xNumerator = checked(2 * pixelY + pixelX);
        var yNumerator = checked(2 * pixelY - pixelX);
        return (
            checked(startX + (xNumerator + (xNumerator >= 0 ? 32 : -32)) / 64),
            checked(startY + (yNumerator + (yNumerator >= 0 ? 32 : -32)) / 64));
    }

    private AuthoritativeCombatTarget? FindProjectileTarget(
        uint levelId,
        int positionX,
        int positionY)
    {
        return world.CombatTargets.Values
            .Where(candidate => candidate.HitPointsQ6 > 0
                && !candidate.SneakHidden
                && (candidate.LevelId == 0 || candidate.LevelId == levelId)
                && ((candidate.PositionX == positionX
                        && candidate.PositionY == positionY)
                    || (candidate.IsMoving
                        && candidate.IsPetrified
                        && candidate.FuturePositionX == positionX
                        && candidate.FuturePositionY == positionY)))
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
    }

    private static AuthoritativePlayerState? FindProjectilePlayer(
        AuthoritativeProjectile projectile,
        IEnumerable<AuthoritativePlayerState> players,
        int positionX,
        int positionY)
    {
        return players
            .Where(candidate => candidate.Life > 0
                && candidate.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                    or DiabloPlayerCombatMode.Quit)
                && candidate.LevelId == projectile.LevelId
                && !IsProjectileOwner(projectile, candidate)
                && candidate.PositionX == positionX
                && candidate.PositionY == positionY)
            .OrderBy(candidate => candidate.EntityId)
            .FirstOrDefault();
    }

    private bool CheckProjectileCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        return projectile.Behavior == AuthoritativeProjectileBehavior.HolyBolt
            ? CheckHolyBoltCollision(
                tick,
                projectile,
                target,
                playerTarget,
                players,
                events)
            : projectile.Behavior == AuthoritativeProjectileBehavior.BoneSpirit
                ? CheckBoneSpiritCollision(
                    tick,
                    projectile,
                    target,
                    playerTarget,
                    players,
                    events)
            : CheckFireboltCollision(
                tick,
                projectile,
                target,
                playerTarget,
                players,
                events);
    }

    private bool CheckFireboltCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = target is not null
            && TryResolveFireboltHit(projectile, target, players, events);
        var playerHit = false;
        var playerStopsProjectile = false;
        if (!hitTarget && playerTarget is not null) {
            if (projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar
                && projectile.TargetEntityId != 0
                && world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                playerStopsProjectile = TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
            } else if (world.CombatTargets.ContainsKey(projectile.SourceEntityId)) {
                playerStopsProjectile = TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
            } else {
                playerHit = TryResolveFireboltPlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events,
                    out playerStopsProjectile);
            }
        }

        var blocked = world.IsMissileBlocked(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (blocked
            && world.TryGetMissileBlockingObject(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                out var @object)
            && @object!.Breakable) {
            @object.Destroyed = true;
        }
        // Hellfire blocks retain the projectile, but still terminate travel in impact.
        return blocked || hitTarget || playerHit || playerStopsProjectile;
    }

    private bool CheckHolyBoltCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = target is not null
            && TryResolveHolyBoltHit(projectile, target, players, events);

        var blocked = world.IsMissileBlocked(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (blocked
            && world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
            && @object!.Breakable) {
            @object.Destroyed = true;
        }
        return blocked || hitTarget;
    }

    private bool TryResolveHolyBoltHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!target.IsUndead)
            return false;
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var holyBoltSpell)
            && holyBoltSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.HolyBolt) {
            hitMonsterLevelScale = holyBoltSpell.HolyBoltHitMonsterLevelScale;
            minimumHitChance = holyBoltSpell.HolyBoltMinimumHitChance;
            maximumHitChance = holyBoltSpell.HolyBoltMaximumHitChance;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DeterministicFixedPointQ6.FromWhole(projectile.Damage);
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    /** Native PlayerMHit magic branch for a monster-owned non-arrow missile. */
    private bool TryResolveMonsterMagicProjectilePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        bool damageShifted = false)
    {
        if (!world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var monster)
            || IsPlayerProtectedByFlash(target)) {
            return false;
        }

        var distance = ClampNativeDistance(DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            monster.PositionX,
            monster.PositionY));
        var hitChance = rules.ResolveMonsterMagicProjectileHitChance(
            GetEffectiveMonsterLevel(monster),
            target.CharacterLevel,
            target.LevelId,
            distance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = !damageShifted && canBlock
            ? random.RandomIntLessThan(100)
            : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.MonsterPlayerBlockLevelScale)
                - (rules.MonsterPlayerBlockLevelScale * GetEffectiveMonsterLevel(monster)),
            0,
            100);
        if (!damageShifted
            && (resistance <= 0 || rules.GameVariant == DiabloGameVariant.Hellfire)
            && blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            return true;
        }

        var projectileDamage = ResolveMonsterMagicProjectileDamage(
            projectile,
            monster);
        var damageQ6 = checked(
            Math.Max(
                1,
                damageShifted
                    ? random.RandomIntBetween(projectileDamage, projectileDamage)
                    : projectileDamage)
                * DeterministicFixedPointQ6.Scale
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                * DeterministicFixedPointQ6.Scale);
        if (resistance > 0)
            damageQ6 -= damageQ6 * resistance / 100;
        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    monster.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                monster.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }

    /** Native ProcessHorkSpawn collision: a zero-payload non-arrow PlayerMHit. */
    private bool TryResolveHorkSpawnPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (!world.CombatTargets.TryGetValue(projectile.SourceEntityId, out var monster)
            || IsPlayerProtectedByFlash(target))
            return false;

        var distance = checked((int)DeterministicGridMetrics.ChebyshevDistance(
            target.PositionX,
            target.PositionY,
            monster.PositionX,
            monster.PositionY));
        if (random.RandomIntLessThan(100)
            >= rules.ResolveMonsterMagicProjectileHitChance(
                GetEffectiveMonsterLevel(monster),
                target.CharacterLevel,
                target.LevelId,
                distance))
            return false;

        var targetClass = GetPlayerClass(target);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.MonsterPlayerBlockLevelScale)
                - (rules.MonsterPlayerBlockLevelScale * GetEffectiveMonsterLevel(monster)),
            0,
            100);
        if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            return true;
        }

        // PlayerMHit always rolls the zero-width damage range before applying
        // its one-point minimum, so preserve that RNG consumption.
        var damageQ6 = checked(
            Math.Max(64, random.RandomIntBetween(0, 0))
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                * DeterministicFixedPointQ6.Scale);
        var damageResult = playerDamage.Apply(target, damageQ6);
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    monster.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                monster.EntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }

    /** Native monster Fireball rolls its damage range at each collision. */
    private int ResolveMonsterMagicProjectileDamage(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget monster)
    {
        if (projectile.Behavior != AuthoritativeProjectileBehavior.Fireball
            || !TryGetMonsterMissile(monster, out var missile)
            || missile.ProjectileBehavior != AuthoritativeProjectileBehavior.Fireball
            || missile.ProjectileDamage > 0)
            return projectile.Damage;

        var damageRange = GetEffectiveMonsterDamageRange(monster, special: false);
        return random.RandomIntBetween(damageRange.Minimum, damageRange.Maximum);
    }

    /** Native PlayerMHit branch used by the persistent AcidPuddle missile. */
    private bool TryResolveAcidPuddlePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (IsPlayerProtectedByFlash(target))
            return false;

        if (random.RandomIntLessThan(100)
            >= rules.ResolveMonsterAcidPuddleHitChance(target.LevelId))
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            AuthoritativeDamageType.Acid);
        var damageQ6 = checked(
            random.RandomIntBetween(projectile.Damage, projectile.Damage)
                * DeterministicFixedPointQ6.Scale
            + AuthoritativeEquipmentIncomingDamageRules.ResolveGetHitBonus(target.Equipment)
                * DeterministicFixedPointQ6.Scale);
        if (resistance > 0)
            damageQ6 -= damageQ6 * resistance / 100;

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    projectile.SourceEntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0)
            StartPlayerDeath(
                target,
                projectile.SourceEntityId,
                tick,
                players,
                PlayerDeathReason.MonsterOrTrap);
        else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageResult.IncomingDamageQ6))
            StartHit(target, targetClass, tick);
        return true;
    }

    private bool CheckBoneSpiritCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = target is not null
            && TryResolveBoneSpiritHit(projectile, target, players, events);
        var playerHit = playerTarget is not null
            && TryResolveBoneSpiritPlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events);
        var blocked = world.IsMissileBlocked(
            projectile.LevelId,
            projectile.PositionX,
            projectile.PositionY);
        if (blocked
            && world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
            && @object!.Breakable) {
            @object.Destroyed = true;
        }
        return blocked || hitTarget || playerHit;
    }

    private bool TryResolveBoneSpiritHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        var damageHitPointsDivisor = 3;
        if (spells.TryGet(projectile.SpellId, out var boneSpiritSpell)
            && boneSpiritSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.BoneSpirit) {
            hitMonsterLevelScale = boneSpiritSpell.BoneSpiritHitMonsterLevelScale;
            minimumHitChance = boneSpiritSpell.BoneSpiritMinimumHitChance;
            maximumHitChance = boneSpiritSpell.BoneSpiritMaximumHitChance;
            damageHitPointsDivisor = boneSpiritSpell.BoneSpiritDamageHitPointsDivisor;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        if (!target.IsPetrified
            && random.RandomIntLessThan(100) >= hitChance)
            return false;

        var damageQ6 = target.HitPointsQ6 / damageHitPointsDivisor / DeterministicFixedPointQ6.Scale
            * DeterministicFixedPointQ6.Scale;
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool TryResolveBoneSpiritPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode)) {
            return false;
        }

        var damageHitPointsDivisor = 3;
        if (spells.TryGet(projectile.SpellId, out var boneSpiritSpell)
            && boneSpiritSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.BoneSpirit) {
            damageHitPointsDivisor = boneSpiritSpell.BoneSpiritPlayerDamageHitPointsDivisor;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - checked((int)target.CharacterLevel * rules.PlayerMagicHitCharacterLevelScale),
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
                - checked((int)owner.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        // Native Plr2PlrMHit computes Bone Spirit damage from current life and
        // then applies the shared non-arrow half-damage step.
        var damageQ6 = target.Life / damageHitPointsDivisor / 2;
        if (resistance > 0) {
            damageQ6 -= damageQ6 * resistance / 100;
        } else if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            if (rules.GameVariant == DiabloGameVariant.Hellfire)
                _ = random.RandomIntLessThan(2);
            return true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    private bool TryResolveFireboltHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var fireboltSpell)
            && fireboltSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.Firebolt) {
            hitMonsterLevelScale = fireboltSpell.FireboltHitMonsterLevelScale;
            minimumHitChance = fireboltSpell.FireboltMinimumHitChance;
            maximumHitChance = fireboltSpell.FireboltMaximumHitChance;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DeterministicFixedPointQ6.FromWhole(
            random.RandomIntBetween(projectile.Damage, projectile.Damage));
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool TryResolveFireboltPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events,
        out bool stopsProjectile)
    {
        stopsProjectile = false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode))
            return false;

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - checked((int)target.CharacterLevel * rules.PlayerMagicHitCharacterLevelScale),
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var targetClass = GetPlayerClass(target);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
            + (targetClass?.BaseBlockChance ?? 0)
            + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
            - checked((int)owner.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        var damageQ6 = checked(
            random.RandomIntBetween(projectile.Damage, projectile.Damage) * 32);
        var resistance = GetPlayerResistance(
            target,
            targetClass,
            projectile.DamageType);
        if (resistance > 0) {
            damageQ6 -= damageQ6 * resistance / 100;
            stopsProjectile = true;
        } else if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            if (rules.GameVariant == DiabloGameVariant.Hellfire) {
                _ = random.RandomIntLessThan(2);
            } else {
                stopsProjectile = true;
            }
            return true;
        } else {
            stopsProjectile = true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    private static int GetMonsterResistance(
        AuthoritativeCombatTarget target,
        AuthoritativeDamageType damageType)
    {
        return damageType switch {
            AuthoritativeDamageType.Fire => target.FireResistance,
            AuthoritativeDamageType.Lightning => target.LightningResistance,
            AuthoritativeDamageType.Magic => target.MagicResistance,
            AuthoritativeDamageType.Acid => target.AcidResistance,
            _ => 0,
        };
    }

    private bool TryResolvePersistentShiftedProjectileHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var resistance = GetMonsterResistance(target, projectile.DamageType);
        if (resistance >= 100)
            return false;
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var persistentSpell)) {
            hitMonsterLevelScale = persistentSpell.PersistentProjectileHitMonsterLevelScale;
            minimumHitChance = persistentSpell.PersistentProjectileMinimumHitChance;
            maximumHitChance = persistentSpell.PersistentProjectileMaximumHitChance;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = random.RandomIntBetween(projectile.Damage, projectile.Damage);
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private bool TryResolvePersistentShiftedProjectilePlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode))
            return false;

        var hitCharacterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        var damageDivisor = 2;
        if (spells.TryGet(projectile.SpellId, out var persistentSpell)) {
            hitCharacterLevelScale = persistentSpell.PersistentProjectileHitCharacterLevelScale;
            minimumHitChance = persistentSpell.PersistentProjectilePlayerMinimumHitChance;
            maximumHitChance = persistentSpell.PersistentProjectilePlayerMaximumHitChance;
            damageDivisor = persistentSpell.PersistentProjectilePlayerDamageDivisor;
        }
        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + (ownerClass?.BaseMagicToHit ?? 50)
            - checked((int)target.CharacterLevel * hitCharacterLevelScale),
            minimumHitChance,
            maximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var damageQ6 = random.RandomIntBetween(projectile.Damage, projectile.Damage) / damageDivisor;
        var targetClass = GetPlayerClass(target);
        var resistance = GetPlayerResistance(target, targetClass, projectile.DamageType);
        if (resistance > 0)
            damageQ6 -= damageQ6 * resistance / 100;

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (resistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    private bool CheckChargedBoltCollision(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget? target,
        AuthoritativePlayerState? playerTarget,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var hitTarget = projectile.Behavior != AuthoritativeProjectileBehavior.MonsterChargedBolt
            && target is not null
            && TryResolveChargedBoltHit(projectile, target, players, events);
        if (!hitTarget && playerTarget is not null) {
            hitTarget = projectile.Behavior == AuthoritativeProjectileBehavior.MonsterChargedBolt
                ? TryResolveMonsterMagicProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events)
                : TryResolveChargedBoltPlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
        }
        if (world.IsMissileBlocked(
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY)) {
            if (world.TryGetMissileBlockingObject(
                    projectile.LevelId,
                    projectile.PositionX,
                    projectile.PositionY,
                    out var @object)
                && @object!.Breakable) {
                @object.Destroyed = true;
            }
            world.Projectiles.Remove(projectile.EntityId);
            return true;
        }
        if (!hitTarget)
            return false;

        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = (uint)projectile.ImpactLifetime;
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
        return true;
    }

    private bool TryResolveChargedBoltPlayerHit(
        ulong tick,
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        if (owner is null
            || IsPlayerProtectedByFlash(target)
            || (!rules.FriendlyFire && owner.FriendlyMode))
            return false;

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + (ownerClass?.BaseMagicToHit ?? 50)
            - checked((int)target.CharacterLevel * rules.PlayerMagicHitCharacterLevelScale),
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var canBlock = (target.CombatMode is DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            && CanBlock(target);
        var blockRoll = canBlock ? random.RandomIntLessThan(100) : 100;
        var targetClass = GetPlayerClass(target);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
            + (targetClass?.BaseBlockChance ?? 0)
            + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
            - checked((int)owner.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        var damageQ6 = checked(
            random.RandomIntBetween(projectile.Damage, projectile.Damage) * 32);
        var lightningResistance = GetLightningResistance(target, targetClass);
        if (lightningResistance > 0) {
            damageQ6 -= damageQ6 * lightningResistance / 100;
        } else if (blockRoll < blockChance) {
            StartBlock(target, targetClass, tick);
            if (rules.GameVariant == DiabloGameVariant.Hellfire)
                _ = random.RandomIntLessThan(2);
            return true;
        }

        var damageResult = playerDamage.Apply(target, Math.Max(0, damageQ6));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloDirectedGameplayEvent(
                target,
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    owner.EntityId,
                    target.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
                    AmountQ6 = damageResult.IncomingDamageQ6,
                }));
        }
        if (target.Life == 0) {
            StartPlayerDeath(
                target,
                owner.EntityId,
                tick,
                players,
                PlayerDeathReason.Player);
        } else if (lightningResistance == 0
            && ShouldStartHit(target, targetClass, damageQ6)) {
            StartHit(target, targetClass, tick);
        }
        return true;
    }

    private void StartPlayerDeath(
        AuthoritativePlayerState player,
        uint sourceEntityId,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        PlayerDeathReason deathReason)
    {
        DiabloPlayerActionLifecycle.Cancel(player);
        DropHeldItemOnDeath(player, players);
        if (!SuppressesDeathDrops(player.LevelId)) {
            DropDeathGold(player, players);
            if (deathReason == PlayerDeathReason.MonsterOrTrap)
                DropEquippedItemsOnDeath(player, players);
            else if (deathReason == PlayerDeathReason.Player)
                DropPvpEarOnDeath(player, players);
        }
        player.CombatMode = DiabloPlayerCombatMode.Death;
        player.CombatModeEndTick = 0;
        if (!useNativePlayerActionTiming)
            return;

        var deathFrames = playerAnimations!
            .GetRequired(player.CharacterClassId)
            .DeathFrames;
        var endTick = checked(tick + (ulong)(deathFrames * 2 - 1));
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Death,
                tick,
                tick,
                endTick,
                endTick) {
                EffectApplied = true,
                TargetEntityId = sourceEntityId,
            },
            DiabloPlayerCombatMode.Death);
    }

    private bool SuppressesDeathDrops(uint levelId)
    {
        return gameMode == DiabloGameMode.Multiplayer
            && world.SuppressMultiplayerDeathDrops(levelId);
    }

    private void DropHeldItemOnDeath(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (player.HeldItem is not { } heldItem)
            return;
        player.HeldItem = null;
        var position = FindDeathDropPosition(player, players);
        if (!position.HasValue)
            return;
        var entityId = AllocateWorldItemEntityId(players);
        world.WorldItems.Add(entityId, new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            position.Value.X,
            position.Value.Y,
            heldItem.ItemSeed,
            heldItem.Price,
            heldItem.State));
    }

    private void DropDeathGold(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var droppedGold = player.Gold / checked((uint)rules.DeathGoldDivisor);
        if (DiabloGoldInventory.TryGetTopologyTotal(
                player,
                rules,
                allowTruncatedWalletRemainder: false,
                out _,
                items)) {
            var remainingGold = DropInventoryGold(
                player,
                players,
                droppedGold,
                skipFullStacks: true);
            if (remainingGold > 0) {
                DropInventoryGold(
                    player,
                    players,
                    remainingGold,
                    skipFullStacks: false);
            }
            player.Gold /= checked((uint)rules.DeathGoldDivisor);
            return;
        }

        player.Gold /= checked((uint)rules.DeathGoldDivisor);
        while (droppedGold > 0) {
            var stackValue = Math.Min(
                droppedGold,
                checked((uint)rules.DeathGoldMaximumStack));
            var itemSeed = AllocateWorldItemSeed();
            DropGoldWorldItem(
                player,
                players,
                itemSeed,
                stackValue,
                DiabloGoldInventory.CreateState(stackValue, rules, items));
            droppedGold -= stackValue;
        }
    }

    private void DropEquippedItemsOnDeath(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var equipment = player.Equipment.ToArray();
        player.Equipment.Clear();
        for (uint slot = 0; slot < 7; slot++) {
            var item = equipment.FirstOrDefault(candidate => candidate.Slot == slot);
            if (item is null)
                continue;
            var preferredDirection = (DiabloPlayerDirection)DeterministicGridDirections.RotateLeft(
                (DeterministicGridDirection)player.FacingDirection,
                checked((int)slot + 1));
            var position = FindDeathDropPosition(player, players, preferredDirection);
            if (!position.HasValue)
                continue;
            var entityId = AllocateWorldItemEntityId(players);
            world.WorldItems.Add(entityId, new AuthoritativeWorldItem(
                entityId,
                player.LevelId,
                position.Value.X,
                position.Value.Y,
                item.ItemSeed,
                checked((uint)Math.Max(0, item.State.IdentifiedValue)),
                item.State));
        }
    }

    private void DropPvpEarOnDeath(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (items is null
            || playerClasses is null
            || string.IsNullOrEmpty(player.CharacterName)) {
            return;
        }

        var definition = items.GetRequired(rules.PvpEarItemId);
        var (createInfo, itemSeed) = DiabloPlayerName.EncodeEarIdentity(
            player.CharacterName);
        if (world.WorldItems.Values.Any(item =>
                item.ItemSeed == itemSeed
                && items.IsStateForDefinition(item.State, definition)
                && item.State.CreateInfo == createInfo)) {
            return;
        }

        var position = FindDeathDropPosition(player, players);
        if (!position.HasValue)
            return;
        var playerClass = playerClasses.GetRequired(player.CharacterClassId);
        var state = definition.Generate(1) with {
            CreateInfo = createInfo,
            Value = checked((int)player.CharacterLevel),
            BaseName = $"Ear of {player.CharacterName}",
            IdentifiedName = player.CharacterName,
            NativeCursorGraphic = playerClass.PvpEarCursorGraphic,
        };
        var entityId = AllocateWorldItemEntityId(players);
        world.WorldItems.Add(entityId, new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            position.Value.X,
            position.Value.Y,
            itemSeed,
            checked((uint)Math.Max(0, state.IdentifiedValue)),
            state));
    }

    private uint DropInventoryGold(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint amount,
        bool skipFullStacks)
    {
        for (var index = 0; index < player.Inventory.Count && amount > 0; index++) {
            var item = player.Inventory[index];
            if (!DiabloGoldInventory.IsValidStack(item, rules, items)
                || (skipFullStacks
                    && item.Price == checked((uint)rules.DeathGoldMaximumStack))) {
                continue;
            }

            if (amount < item.Price) {
                var itemSeed = AllocateWorldItemSeed();
                DropGoldWorldItem(
                    player,
                    players,
                    itemSeed,
                    amount,
                    DiabloGoldInventory.CreateState(amount, rules, items));
                player.Inventory[index] = DiabloGoldInventory.WithValue(
                    item,
                    item.Price - amount,
                    rules,
                    items);
                return 0;
            }

            amount -= item.Price;
            DropGoldWorldItem(
                player,
                players,
                item.ItemSeed,
                item.Price,
                item.State);
            DiabloPlayerItemAccess.Remove(
                player,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    checked((uint)index)));
            index = -1;
        }
        return amount;
    }

    private void DropGoldWorldItem(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint itemSeed,
        uint stackValue,
        AuthoritativeItemState state)
    {
        var position = FindDeathDropPosition(player, players);
        if (!position.HasValue)
            return;
        var entityId = AllocateWorldItemEntityId(players);
        world.WorldItems.Add(entityId, new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            position.Value.X,
            position.Value.Y,
            itemSeed,
            stackValue,
            state));
    }

    private (int X, int Y)? FindDeathDropPosition(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        DiabloPlayerDirection? preferredDirection = null)
    {
        if (preferredDirection.HasValue) {
            var displacement = DeterministicGridDirections.GetDisplacement(
                (DeterministicGridDirection)preferredDirection.Value);
            var preferredX = checked(player.PositionX + displacement.X);
            var preferredY = checked(player.PositionY + displacement.Y);
            if (IsDeathDropPositionAvailable(player, players, preferredX, preferredY))
                return (preferredX, preferredY);
        }
        return DeterministicGridCrawl.FindClosestValidPosition(
            player.PositionX,
            player.PositionY,
            1,
            rules.DeathDropSearchRadius,
            (positionX, positionY) => IsDeathDropPositionAvailable(
                player,
                players,
                positionX,
                positionY));
    }

    private bool IsDeathDropPositionAvailable(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int positionX,
        int positionY)
    {
        return world.IsWalkable(player.LevelId, positionX, positionY)
            && !world.IsOccupied(player.LevelId, positionX, positionY, players)
            && !world.WorldItems.Values.Any(item =>
                item.LevelId == player.LevelId
                && item.PositionX == positionX
                && item.PositionY == positionY);
    }

    private void UpdateFacingForCast(
        AuthoritativePlayerState player,
        DiabloCastIntent intent,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (spells.TryGet(intent.SpellId, out var spell)
            && spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Phasing) {
            return;
        }
        if (intent.TargetEntityId != 0) {
            var playerTarget = players.FirstOrDefault(candidate =>
                candidate.EntityId == intent.TargetEntityId);
            if (playerTarget is not null) {
                UpdateFacingToward(player, playerTarget.PositionX, playerTarget.PositionY);
                return;
            }
            if (world.CombatTargets.TryGetValue(intent.TargetEntityId, out var combatTarget)) {
                UpdateFacingToward(player, combatTarget.PositionX, combatTarget.PositionY);
                return;
            }
        }
        UpdateFacingToward(player, intent.TargetX, intent.TargetY);
    }

    private static void UpdateFacingToward(
        AuthoritativePlayerState player,
        int targetX,
        int targetY)
    {
        if (targetX == player.PositionX && targetY == player.PositionY)
            return;
        player.FacingDirection = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
    }

    private uint AllocateWorldItemSeed()
    {
        uint itemSeed;
        do {
            itemSeed = unchecked((uint)random.AdvanceRndSeed());
        } while (itemSeed == 0);
        return itemSeed;
    }

    private uint AllocateWorldItemEntityId(
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return worldItemEntityIds.Allocate(entityId =>
            players.Any(player => player.EntityId == entityId)
            || world.IsEntityIdOccupied(entityId));
    }

    private void StartBlock(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        ulong tick)
    {
        DiabloPlayerActionLifecycle.Cancel(player);
        var blockingFrames = GetPlayerAnimation(player)?.BlockingFrames ?? 2;
        var skippedFrames = HasUsableEquipmentFlag(player, FastBlockFlag)
            ? blockingFrames - rules.EquipmentFastBlockFrameSkip
            : IsNativeFastBlockingClass(player, playerClass)
                ? blockingFrames - playerClass!.FastBlockFrameSkip
                : 0;
        var duration = checked((blockingFrames - 1 - skippedFrames) * 3 + 1);
        player.CombatMode = DiabloPlayerCombatMode.Block;
        player.CombatModeEndTick = checked(tick + (ulong)duration);
    }

    private static bool ShouldStartHit(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        int damageQ6)
    {
        var threshold = playerClass?.Flags.HasFlag(
                DiabloPlayerClassFlags.HeavyHitThreshold) == true
            ? playerClass.CalculateHeavyHitThreshold(player.CharacterLevel)
            : checked((int)player.CharacterLevel);
        return (damageQ6 >> 6) >= threshold;
    }

    private void StartHit(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        ulong tick)
    {
        DiabloPlayerActionLifecycle.Cancel(player);
        var recoveryFrames = GetPlayerAnimation(player)?.RecoveryFrames ?? 6;
        var flags = GetUsableEquipmentFlags(player);
        var skippedFrames = (flags & FastestHitRecoveryFlag) != 0
            ? rules.FastestHitRecoveryFrameSkip
            : (flags & FasterHitRecoveryFlag) != 0
                ? rules.FasterHitRecoveryFrameSkip
                : (flags & FastHitRecoveryFlag) != 0
                    ? rules.FastHitRecoveryFrameSkip
                    : 0;
        var duration = Math.Max(1, recoveryFrames - skippedFrames);
        player.CombatMode = DiabloPlayerCombatMode.GotHit;
        player.CombatModeEndTick = checked(tick + (ulong)duration);
    }

    private AuthoritativePlayerClassDefinition? GetPlayerClass(
        AuthoritativePlayerState player)
    {
        return playerClasses is not null
            && playerClasses.TryGet(player.CharacterClassId, out var playerClass)
            ? playerClass
            : null;
    }

    private AuthoritativePlayerAnimationDefinition? GetPlayerAnimation(
        AuthoritativePlayerState player)
    {
        return playerAnimations is not null
            && playerAnimations.TryGet(player.CharacterClassId, out var animation)
            ? animation
            : null;
    }

    private DiabloPlayerAttackAnimationProfile GetAttackAnimationProfile(
        AuthoritativePlayerState player)
    {
        var hands = player.Equipment
            .Where(item => item.Slot is 4 or 5 && IsUsable(item.State))
            .OrderBy(item => item.Slot)
            .ToArray();
        var hasShield = hands.Any(item => AuthoritativeItemClassification.IsShield(item.State, items));
        var weapon = hands.FirstOrDefault(item => AuthoritativeItemClassification.IsWeapon(item.State, items)
            && !AuthoritativeItemClassification.IsShield(item.State, items));
        if (weapon is null)
            return hasShield
                ? DiabloPlayerAttackAnimationProfile.UnarmedShield
                : DiabloPlayerAttackAnimationProfile.Unarmed;

        var state = weapon.State;
        if (AuthoritativeItemClassification.IsSword(state, items))
            return hasShield
                ? DiabloPlayerAttackAnimationProfile.SwordShield
                : DiabloPlayerAttackAnimationProfile.Sword;
        if (AuthoritativeItemClassification.IsAxe(state, items))
            return DiabloPlayerAttackAnimationProfile.Axe;
        if (AuthoritativeItemClassification.IsBow(state, items))
            return DiabloPlayerAttackAnimationProfile.Bow;
        if (AuthoritativeItemClassification.IsMace(state, items))
            return hasShield
                ? DiabloPlayerAttackAnimationProfile.MaceShield
                : DiabloPlayerAttackAnimationProfile.Mace;
        if (AuthoritativeItemClassification.IsStaff(state, items))
            return DiabloPlayerAttackAnimationProfile.Staff;
        return hasShield
            ? DiabloPlayerAttackAnimationProfile.UnarmedShield
            : DiabloPlayerAttackAnimationProfile.Unarmed;
    }

    private int GetAttackSkippedFrames(
        AuthoritativePlayerState player,
        bool ranged,
        bool includesFirstFrame)
    {
        var flags = GetUsableEquipmentFlags(player);
        if (ranged) {
            var skippedFrames = (flags & (QuickAttackFlag | FastAttackFlag)) != 0
                ? includesFirstFrame
                    ? rules.RangedAttackFrameSkipWithFirstFrame
                    : rules.RangedAttackFrameSkipWithoutFirstFrame
                : 0;
            if ((flags & FastAttackFlag) != 0)
                skippedFrames += rules.RangedFastAttackFrameSkipBonus;
            return skippedFrames;
        }
        if ((flags & FastestAttackFlag) != 0)
            return includesFirstFrame
                ? rules.FastestAttackFrameSkipWithFirstFrame
                : rules.FastestAttackFrameSkipWithoutFirstFrame;
        if ((flags & FasterAttackFlag) != 0)
            return includesFirstFrame
                ? rules.FasterAttackFrameSkipWithFirstFrame
                : rules.FasterAttackFrameSkipWithoutFirstFrame;
        if ((flags & FastAttackFlag) != 0)
            return includesFirstFrame
                ? rules.FastAttackFrameSkipWithFirstFrame
                : rules.FastAttackFrameSkipWithoutFirstFrame;
        if ((flags & QuickAttackFlag) != 0)
            return includesFirstFrame
                ? rules.QuickAttackFrameSkipWithFirstFrame
                : rules.QuickAttackFrameSkipWithoutFirstFrame;
        return 0;
    }

    private int GetLightningResistance(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass)
    {
        return GetPlayerResistance(
            player,
            playerClass,
            AuthoritativeDamageType.Lightning);
    }

    private int GetPlayerResistance(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        AuthoritativeDamageType damageType)
    {
        var naturalResistanceBonus = playerClass?.Flags.HasFlag(
            DiabloPlayerClassFlags.NaturalResistance) == true
            ? playerClass.CalculateNaturalResistanceBonus(player.CharacterLevel)
            : 0;
        return AuthoritativeEquipmentResistanceRules.Calculate(
            player.Equipment,
            damageType,
            naturalResistanceBonus,
            rules.MaximumResistance);
    }

    private bool CanBlock(AuthoritativePlayerState player)
    {
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        if (IsUsableShield(left) || IsUsableShield(right))
            return true;

        var playerClass = GetPlayerClass(player);
        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.FlexibleBlock) != true)
            return false;
        if (IsUsableStaff(left) || IsUsableStaff(right))
            return true;
        if (left is null && right is null)
            return true;
        return left is not null
            && right is null
            && AuthoritativeItemClassification.IsWeapon(left.State, items)
            && !AuthoritativeItemClassification.IsTwoHanded(left.State, items)
            || right is not null
            && left is null
            && AuthoritativeItemClassification.IsWeapon(right.State, items)
            && !AuthoritativeItemClassification.IsTwoHanded(right.State, items);
    }

    /** Applies the native player-versus-player shield/class block boundary. */
    private bool TryResolvePlayerBlock(
        AuthoritativePlayerState attacker,
        AuthoritativePlayerState target,
        ulong tick)
    {
        if (target.CombatMode is not (DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            || !CanBlock(target))
            return false;

        var targetClass = GetPlayerClass(target);
        var blockRoll = random.RandomIntLessThan(100);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
                - checked((int)attacker.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        if (blockRoll >= blockChance)
            return false;

        StartBlock(target, targetClass, tick);
        return true;
    }

    private static bool HasUsableEquipmentFlag(AuthoritativePlayerState player, uint flag)
    {
        return (GetUsableEquipmentFlags(player) & flag) != 0;
    }

    private static bool HasUsableHellfireDamageArmorFlag(
        AuthoritativePlayerState player,
        int flag)
    {
        return player.Equipment.Any(item => {
            var state = item.State;
            return IsUsable(state)
                && (state.Magical == 0 || state.Identified)
                && (state.HellfireDamageArmorFlags & flag) != 0;
        });
    }

    private static uint GetUsableEquipmentFlags(AuthoritativePlayerState player)
    {
        return player.Equipment
            .Where(item => IsUsable(item.State)
                && (item.State.Magical == 0 || item.State.Identified))
            .Aggregate(0U, (flags, item) => flags | item.State.Flags);
    }

    private bool IsNativeFastBlockingClass(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass)
    {
        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.FastBlock) != true)
            return false;
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        return IsUsableStaff(left) || IsUsableStaff(right);
    }

    private bool IsUsableShield(EquippedStoreItem? item)
    {
        return item is not null
            && AuthoritativeItemClassification.IsShield(item.State, items)
            && IsUsable(item.State);
    }

    private bool IsUsableStaff(EquippedStoreItem? item)
    {
        return item is not null
            && AuthoritativeItemClassification.IsStaff(item.State, items)
            && IsUsable(item.State);
    }

    private static bool IsUsable(AuthoritativeItemState item)
    {
        return item.StatFlag;
    }

    private bool IsPlayerProtectedByFlash(AuthoritativePlayerState player)
    {
        return world.Projectiles.Values.Any(projectile =>
            projectile.Behavior is AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop
            && IsProjectileOwner(projectile, player)
            && spells.TryGet(projectile.SpellId, out var spell)
            && spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Flash
            && projectile.RemainingTicks < spell.ProjectileLifetime);
    }

    private static bool IsPlayerEtherealized(AuthoritativePlayerState player)
    {
        return player.StatusEffects.Any(effect =>
            effect.Behavior == AuthoritativeStatusEffectBehavior.Etherealize);
    }

    private static bool IsProjectileOwner(
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState player)
    {
        return player.EntityId == projectile.SourceEntityId
            || projectile.SourceEntityId == 1 && player.EntityId == 0;
    }

    private bool TryResolveChargedBoltHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (target.LightningResistance >= 100)
            return false;
        var owner = players.FirstOrDefault(player => player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var chargedBoltSpell)
            && chargedBoltSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.ChargedBolt) {
            hitMonsterLevelScale = chargedBoltSpell.ChargedBoltHitMonsterLevelScale;
            minimumHitChance = chargedBoltSpell.ChargedBoltMinimumHitChance;
            maximumHitChance = chargedBoltSpell.ChargedBoltMaximumHitChance;
        }
        var baseMagicToHit = 50;
        if (playerClasses is not null
            && playerClasses.TryGet(owner.CharacterClassId, out var playerClass)) {
            baseMagicToHit = playerClass.BaseMagicToHit;
        }
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + baseMagicToHit
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DeterministicFixedPointQ6.FromWhole(
            random.RandomIntBetween(projectile.Damage, projectile.Damage));
        if (target.LightningResistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private static int GetDirection(
        int startX,
        int startY,
        int destinationX,
        int destinationY)
        => (int)DeterministicGridDirections.Toward(
            startX,
            startY,
            destinationX,
            destinationY);

    private static (int X, int Y) CalculateVelocity(
        int startX,
        int startY,
        int destinationX,
        int destinationY,
        int speed)
        => DeterministicGridVelocity.CalculateNative(
            startX,
            startY,
            destinationX,
            destinationY,
            speed);

    private void ResolveProjectileDamage(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player => player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return;

        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyDamage(owner, target, projectile.Damage, projectile.DamageType, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
    }

    private void ApplyDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int baseDamage,
        AuthoritativeDamageType damageType,
        ICollection<DiabloGameplayEvent> events)
    {
        var damage = rules.ResolveDamage(
            baseDamage,
            GetEffectiveMonsterArmorClass(target),
            damageType,
            target);
        ApplyResolvedDamage(player, target, damage, events);
    }

    private void ApplyResolvedDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int damage,
        ICollection<DiabloGameplayEvent> events)
    {
        ApplyResolvedDamageQ6(
            player,
            target,
            DeterministicFixedPointQ6.FromWhole(damage),
            events);
    }

    private void ApplyResolvedDamageQ6(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int damageQ6,
        ICollection<DiabloGameplayEvent> events)
    {
        var wasAlive = target.HitPointsQ6 > 0;
        target.ApplyDamageQ6(damageQ6);
        if (damageQ6 > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                player.EntityId,
                target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(damageQ6)) {
                AmountQ6 = damageQ6,
            });
        }
        if (wasAlive
            && target.HitPointsQ6 > 0
            && damageQ6 > 0
            && GetSneakTuning(target) is { } sneakTuning) {
            target.StartSneakRetreat(sneakTuning.RetreatTicks);
        }
        if (!wasAlive || target.HitPointsQ6 != 0)
            return;

        target.CancelMovement();
        target.BeginDeathAnimation();
        TriggerFallenFear(target);

        AdvanceQuestForMonsterDefeat(player, target);
        if (target.IsDiablo) {
            world.VictoryTriggered = true;
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Victory,
                player.EntityId,
                target.EntityId,
                checked((int)player.DiabloKillLevel)));
        }

        var experienceReward = GetEffectiveMonsterExperienceReward(target);
        DiabloPlayerProgression.GrantExperience(player, experienceReward, experience);
        events.Add(new DiabloGameplayEvent(
            DiabloGameplayEventKind.Experience,
            player.EntityId,
            player.EntityId,
            checked((int)experienceReward)));
        if (target.Drop is not null
            && FindAvailableWorldItemPosition(
                target.LevelId,
                target.PositionX,
                target.PositionY,
                target.EntityId) is { } dropPosition) {
            world.WorldItems[target.Drop.EntityId] = new AuthoritativeWorldItem(
                target.Drop.EntityId,
                target.LevelId,
                dropPosition.X,
                dropPosition.Y,
                target.Drop.ItemSeed,
                target.Drop.Price,
                target.Drop.State);
        }
        if (!target.IsPlayerMinion && target.DeathAnimationTicks == 0)
            ReplaceDeadMonsterWithCorpse(target);
    }

    private void TriggerFallenFear(AuthoritativeCombatTarget deceased)
    {
        var deceasedTuning = GetFallenFearTuning(deceased);
        if (!deceasedTuning.HasValue)
            return;
        foreach (var fallen in world.CombatTargets.Values
                     .Where(candidate => {
                         var tuning = GetFallenFearTuning(candidate);
                         return candidate.EntityId != deceased.EntityId
                             && tuning.HasValue
                             && candidate.HitPointsQ6 > 0
                             && (candidate.LevelId == 0 || candidate.LevelId == deceased.LevelId)
                              && DeterministicGridMetrics.ChebyshevDistance(
                                  candidate.PositionX,
                                  candidate.PositionY,
                                  deceased.PositionX,
                                  deceased.PositionY)
                                  <= deceasedTuning.Value.Radius;
                     })
                     .OrderBy(candidate => candidate.EntityId)) {
            var tuning = GetFallenFearTuning(fallen)!.Value;
            fallen.StartFallenFear(
                Math.Max(
                    tuning.LevelOffset - GetEffectiveMonsterLevel(fallen),
                    tuning.MinimumTicks),
                GetDirection(
                    deceased.PositionX,
                    deceased.PositionY,
                    fallen.PositionX,
                    fallen.PositionY));
        }
    }

    private void TriggerFallenRally(AuthoritativeCombatTarget leader)
    {
        var radius = checked((2 * leader.Intelligence) + 4);
        var rallyTicks = checked((30 * leader.Intelligence) + 105);
        foreach (var fallen in world.CombatTargets.Values
                     .Where(candidate =>
                         UsesNativeAi(candidate, "Fallen")
                         && candidate.HitPointsQ6 > 0
                         && (candidate.LevelId == 0 || candidate.LevelId == leader.LevelId)
                          && DeterministicGridMetrics.ChebyshevDistance(
                              candidate.PositionX,
                              candidate.PositionY,
                              leader.PositionX,
                              leader.PositionY) <= radius)
                     .OrderBy(candidate => candidate.EntityId)) {
            fallen.StartFallenRally(rallyTicks);
        }
    }

    private (int Radius, int LevelOffset, int MinimumTicks)? GetFallenFearTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.FallenFearRadius > 0
                && definition.FallenFearLevelOffset > 0
                && definition.FallenFearMinimumTicks > 0) {
                return (
                    definition.FallenFearRadius,
                    definition.FallenFearLevelOffset,
                    definition.FallenFearMinimumTicks);
            }
        }
        return UsesNativeAi(target, "Fallen")
            || UsesNativeAi(target, "Snotspill")
            ? (4, 8, 2)
            : null;
    }

    /** Applies an optional external quest kill trigger at the authoritative death boundary. */
    private void AdvanceQuestForMonsterDefeat(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target)
    {
        if (target.MonsterId == 0)
            return;
        if (target.IsDiablo && gameMode == DiabloGameMode.SinglePlayer)
            player.DiabloKillLevel = Math.Max(
                player.DiabloKillLevel,
                checked((uint)difficulty + 1));
        foreach (var quest in world.Quests.Values
                     .Where(quest => quest.Active
                         && !quest.Unavailable
                         && !quest.Completed
                         && ((quest.CompletionMonsterId != 0
                                 && quest.CompletionMonsterId == target.MonsterId)
                             || (!string.IsNullOrWhiteSpace(quest.CompletionUniqueIdentity)
                                 && target.IsUnique
                                 && string.Equals(
                                     quest.CompletionUniqueIdentity,
                                     target.UniqueIdentity,
                                     StringComparison.Ordinal))
                             || (quest.CompletionDiablo && target.IsDiablo))
                         && (quest.LevelId == 0 || quest.LevelId == target.LevelId))) {
            quest.Progress = Math.Min(
                quest.RequiredProgress,
                checked(quest.Progress + 1));
            quest.Logged = true;
            if (quest.Progress == quest.RequiredProgress) {
                quest.Completed = true;
                quest.Active = false;
                quest.Variable1 = Math.Max(
                    quest.Variable1,
                    quest.CompletionVariable1);
                quest.Variable2 = Math.Max(
                    quest.Variable2,
                    quest.CompletionVariable2);
                if (quest.CompletionActivateQuestId != 0
                    && world.Quests.TryGetValue(
                        quest.CompletionActivateQuestId,
                        out var nextQuest)
                    && !nextQuest.Unavailable
                    && !nextQuest.Completed) {
                    nextQuest.Active = true;
                }
                SpawnQuestCompletionReward(quest, target);
            }
        }
    }

    /** Spawns a content-owned quest reward at the native monster-defeat position. */
    private void SpawnQuestCompletionReward(
        AuthoritativeQuestState quest,
        AuthoritativeCombatTarget target)
    {
        if (quest.CompletionRewardItemId == 0 || items is null)
            return;
        var position = FindAvailableWorldItemPosition(
            target.LevelId,
            target.PositionX,
            target.PositionY,
            target.EntityId);
        if (!position.HasValue)
            return;
        var itemSeed = AllocateWorldItemSeed();
        var state = spellBooks is not null
            && spellBooks.IsBookTemplate(items, quest.CompletionRewardItemId)
            ? spellBooks.GenerateRandom(
                items,
                itemSeed,
                quest.CompletionRewardItemLevel,
                gameMode,
                quest.CompletionRewardItemId)
            : items.Generate(
                quest.CompletionRewardItemId,
                itemSeed,
                quest.CompletionRewardItemLevel);
        var entityId = AllocateWorldItemEntityId(activePlayers);
        world.WorldItems[entityId] = new AuthoritativeWorldItem(
            entityId,
            target.LevelId,
            position.Value.X,
            position.Value.Y,
            itemSeed,
            checked((uint)Math.Max(0, state.IdentifiedValue)),
            state);
    }

    /** Finds a deterministic open tile for a monster-origin world item. */
    private (int X, int Y)? FindAvailableWorldItemPosition(
        uint levelId,
        int originX,
        int originY,
        uint excludedEntityId)
    {
        return DeterministicGridCrawl.FindClosestValidPosition(
            originX,
            originY,
            0,
            rules.DeathDropSearchRadius,
            (positionX, positionY) =>
                world.IsWalkable(levelId, positionX, positionY)
                && !world.IsOccupied(
                    levelId,
                    positionX,
                    positionY,
                    activePlayers,
                    excludedEntityId)
                && !world.WorldItems.Values.Any(item =>
                    item.LevelId == levelId
                    && item.PositionX == positionX
                    && item.PositionY == positionY));
    }

    private uint AllocateProjectileEntityId()
    {
        return projectileEntityIds.Allocate(entityId =>
            activePlayers.Any(player => player.EntityId == entityId)
            || world.CombatTargets.ContainsKey(entityId)
            || world.WorldItems.ContainsKey(entityId)
            || world.Objects.ContainsKey(entityId)
            || world.Projectiles.ContainsKey(entityId)
            || world.Portals.ContainsKey(entityId)
            || world.Corpses.ContainsKey(entityId));
    }

}
