namespace Devilution.Diablo;

/** Applies combat intents and autonomous combat ticks without host dependencies. */
public sealed class DiabloCombatCommandProcessor
{
    private enum PlayerDeathReason
    {
        MonsterOrTrap,
        Player,
    }

    private readonly DiabloWorldState world;
    private readonly AuthoritativeSpellCatalog spells;
    private readonly AuthoritativeCombatRules rules;
    private readonly DiabloRandom random;
    private readonly AuthoritativePlayerClassCatalog? playerClasses;
    private readonly AuthoritativePlayerAnimationCatalog? playerAnimations;
    private readonly bool useNativePlayerActionTiming;
    private readonly DiabloGameMode gameMode;
    private readonly AuthoritativeItemCatalog? items;
    private readonly AuthoritativePlayerDamageResolver playerDamage;
    private readonly DiabloWorldCommandProcessor? worldCommands;
    private uint nextWorldItemEntityId = 0x40000000;
    private uint nextProjectileEntityId = 0x80000000;

    private const uint FastHitRecoveryFlag = 1U << 21;
    private const uint FasterHitRecoveryFlag = 1U << 22;
    private const uint FastestHitRecoveryFlag = 1U << 23;
    private const uint FastBlockFlag = 1U << 24;
    private const uint FastAttackFlag = 1U << 18;
    private const uint FasterAttackFlag = 1U << 19;
    private const uint FastestAttackFlag = 1U << 20;

    private static readonly int[] ChargedBoltPath = [
        -1, 0, 1, -1, 0, 1, -1, -1, 0, 0, 1, 1, 0, 1, -1, 0,
    ];

    private static readonly (int X, int Y)[] DirectionDisplacements = [
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
        (0, -1),
        (1, -1),
        (1, 0),
    ];

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
        DiabloRandom? random = null,
        AuthoritativePlayerClassCatalog? playerClasses = null,
        AuthoritativePlayerAnimationCatalog? playerAnimations = null,
        bool useNativePlayerActionTiming = false,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        AuthoritativeItemCatalog? items = null,
        AuthoritativePlayerDamageResolver? playerDamage = null,
        DiabloWorldCommandProcessor? worldCommands = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.spells = spells ?? throw new ArgumentNullException(nameof(spells));
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        this.random = random ?? new DiabloRandom(0);
        this.playerClasses = playerClasses;
        this.playerAnimations = playerAnimations;
        this.useNativePlayerActionTiming = useNativePlayerActionTiming;
        this.gameMode = Enum.IsDefined(gameMode)
            ? gameMode
            : throw new ArgumentOutOfRangeException(nameof(gameMode));
        this.items = items;
        this.playerDamage = playerDamage ?? new AuthoritativePlayerDamageResolver(spells);
        this.worldCommands = worldCommands;
        if (useNativePlayerActionTiming && playerAnimations is null) {
            throw new ArgumentException(
                "Native player action timing requires a player animation catalog.",
                nameof(playerAnimations));
        }
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

        return intent switch {
            DiabloAttackIntent attack => Attack(player, attack.TargetEntityId, appliedTick),
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

    /** Applies one deterministic autonomous world tick in caller-provided player order. */
    public IReadOnlyList<DiabloDirectedGameplayEvent> AdvanceTick(
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Any(player => player is null))
            throw new ArgumentException("Combat tick players cannot contain null state.", nameof(players));

        world.AdvancePortals(tick);
        var events = new List<DiabloDirectedGameplayEvent>();
        foreach (var player in players)
            AdvancePlayerAction(player, tick, players, events);
        foreach (var player in players)
            AdvanceCombatMode(player, tick);
        AdvanceMonsters(tick, players, events);
        AdvanceProjectiles(tick, players, events);
        return events;
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
                player.PositionX = checked(player.PositionX + action.DirectionX);
                player.PositionY = checked(player.PositionY + action.DirectionY);
                break;
            case DiabloPlayerActionKind.Attack:
                AddDirectedEvents(
                    player,
                    ResolveAttack(
                        player,
                        action.TargetEntityId,
                        action.StartedAtTick,
                        player.CombatMode == DiabloPlayerCombatMode.RangedAttack),
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

    private DiabloCombatIntentResult Attack(
        AuthoritativePlayerState player,
        uint targetEntityId,
        ulong appliedTick)
    {
        if (!useNativePlayerActionTiming)
            return ResolveAttack(player, targetEntityId, appliedTick);
        if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var animation = playerAnimations!.GetRequired(player.CharacterClassId);
        var profile = GetAttackAnimationProfile(player);
        var timing = animation.GetAttack(profile);
        var ranged = profile == DiabloPlayerAttackAnimationProfile.Bow;
        if (!TryGetAttackTarget(player, targetEntityId, ranged, out var target))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        UpdateFacingToward(player, target.PositionX, target.PositionY);
        var skippedFrames = GetAttackSkippedFrames(player, ranged);
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

    private DiabloCombatIntentResult ResolveAttack(
        AuthoritativePlayerState player,
        uint targetEntityId,
        ulong appliedTick,
        bool ranged = false)
    {
        if (!TryGetAttackTarget(player, targetEntityId, ranged, out var target))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        UpdateFacingToward(player, target.PositionX, target.PositionY);
        var damage = ResolvePlayerAttackDamage(
            player,
            target,
            unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId));
        var events = new List<DiabloGameplayEvent>();
        ApplyResolvedDamage(player, target, damage, events);
        if (damage > 0)
            ApplyEquipmentAttackEffects(player, damage, events);
        return events.Count == 0
            ? DiabloCombatIntentResult.Accepted
            : new DiabloCombatIntentResult(true, null, events);
    }

    /** Applies native usable equipment resource gains after a successful hit. */
    private void ApplyEquipmentAttackEffects(
        AuthoritativePlayerState player,
        int damage,
        ICollection<DiabloGameplayEvent> events)
    {
        var gain = AuthoritativeEquipmentAttackEffectRules.Resolve(
            DiabloFixedPointQ6.FromWhole(damage),
            GetUsableEquipmentFlags(player),
            random.GenerateRnd);
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
                DiabloFixedPointQ6.ToWholeTruncated(lifeGain)) {
                AmountQ6 = lifeGain,
            });
        }
    }

    /** Applies native usable equipment damage, hit, and target-armor bonuses. */
    private int ResolvePlayerAttackDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        uint seed)
    {
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

        var damage = checked(
            rules.BaseAttackDamage
            + rules.BaseAttackDamage * damagePercent / 100
            + damageFlat);
        return rules.ResolveAttackDamage(
            damage,
            target.ArmorClass,
            Math.Clamp(checked(rules.HitChancePercent + hitBonus + armorPiercingBonus), 0, 100),
            seed);
    }

    private bool TryGetAttackTarget(
        AuthoritativePlayerState player,
        uint targetEntityId,
        bool ranged,
        out AuthoritativeCombatTarget target)
    {
        if (!world.CombatTargets.TryGetValue(targetEntityId, out target!)
            || target.HitPoints <= 0
            || (target.LevelId != 0 && target.LevelId != player.LevelId)) {
            return false;
        }
        var distance = Math.Max(
            Math.Abs(target.PositionX - player.PositionX),
            Math.Abs(target.PositionY - player.PositionY));
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
        if (!spells.TryGet(intent.SpellId, out var spell))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        ResolvedCastSource source = default!;
        if (!skipSourceValidation
            && !TryResolveCastSource(player, intent, out source, out var sourceFailure))
            return DiabloCombatIntentResult.Rejected(sourceFailure);
        var isRuneFire = source.Kind == DiabloCastSourceKind.ConsumableItem
            && source.Item?.State.MiscId == 47;
        var hasDamage = spell.DamageAmount > 0
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
                || Math.Max(
                    Math.Abs(resurrectionTarget.PositionX - player.PositionX),
                    Math.Abs(resurrectionTarget.PositionY - player.PositionY)) > spell.Range) {
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
                || Math.Max(
                    Math.Abs(healOtherTarget.PositionX - player.PositionX),
                    Math.Abs(healOtherTarget.PositionY - player.PositionY)) > spell.Range) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            centerX = healOtherTarget.PositionX;
            centerY = healOtherTarget.PositionY;
        }
        if ((hasDamage || isBerserk) && !isSelfCenteredFlash && !isApocalypse && !isRingOfFire) {
            if (isBerserk) {
                target = FindBerserkTarget(
                    player.LevelId,
                    intent.TargetEntityId,
                    intent.TargetX,
                    intent.TargetY,
                    spell.BerserkSearchRadius,
                    respectMagicResistance: false);
            } else if (intent.TargetEntityId != 0) {
                world.CombatTargets.TryGetValue(intent.TargetEntityId, out target);
            } else {
                target = world.CombatTargets.Values
                    .Where(candidate => candidate.HitPoints > 0
                        && (candidate.LevelId == 0 || candidate.LevelId == player.LevelId)
                        && candidate.PositionX == intent.TargetX
                        && candidate.PositionY == intent.TargetY)
                    .OrderBy(candidate => candidate.EntityId)
                    .FirstOrDefault();
            }
            centerX = target?.PositionX ?? intent.TargetX;
            centerY = target?.PositionY ?? intent.TargetY;
            if (isRuneFire) {
                runeFirePosition = FindRuneFirePosition(player, centerX, centerY);
                if (!runeFirePosition.HasValue)
                    return DiabloCombatIntentResult.Accepted;
                centerX = runeFirePosition.Value.X;
                centerY = runeFirePosition.Value.Y;
            }
            if ((intent.TargetEntityId != 0 && target is null)
                || (isBerserk && target is null)
                || (target is not null
                    && (target.HitPoints <= 0
                        || (target.LevelId != 0 && target.LevelId != player.LevelId)))
                || Math.Max(
                    Math.Abs(centerX - player.PositionX),
                    Math.Abs(centerY - player.PositionY)) > spell.Range
                || (!(isFireWall || isLightningWall || isRingOfFire || isFlameWave || isInferno || isChainLightning || isNova || isJester) && spell.AreaRadius == 0
                    && spell.ProjectileSpeed == 0
                    && target is null)) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            var hasLineOfSight = isFireWall || isLightningWall || isRingOfFire || isFlameWave || isInferno || isChainLightning || isNova || spell.ProjectileSpeed > 0
                ? world.HasMissileLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    centerX,
                    centerY)
                : world.HasLineOfSight(
                    player.LevelId,
                    player.PositionX,
                    player.PositionY,
                    centerX,
                    centerY);
            if (!hasLineOfSight) {
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }

            damageTargets = isBerserk || isFireWall || isLightningWall || isRingOfFire || isFlameWave || isInferno || isChainLightning || isNova
                ? []
                : world.CombatTargets.Values
                    .Where(candidate => candidate.HitPoints > 0
                        && (candidate.LevelId == 0 || candidate.LevelId == player.LevelId)
                        && Math.Max(
                            Math.Abs(candidate.PositionX - centerX),
                            Math.Abs(candidate.PositionY - centerY)) <= spell.AreaRadius)
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

        var projectileTravelTicks = 0;
        if (spell.ProjectileSpeed > 0
            && spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Targeted) {
            var distance = Math.Max(
                Math.Abs(centerX - player.PositionX),
                Math.Abs(centerY - player.PositionY));
            projectileTravelTicks = Math.Max(
                1,
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
                || player.LevelId == AuthoritativePortalRules.TownLevelId
                || intent.TargetEntityId != 0
                || Math.Max(
                    Math.Abs(intent.TargetX - player.PositionX),
                    Math.Abs(intent.TargetY - player.PositionY)) > spell.Range
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
                applyEffects: false);
            if (!townPortalValidation.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    townPortalValidation.Failure ?? DiabloRuleFailure.InvalidTarget);
        }
        if (isGolem) {
            if (intent.TargetEntityId != 0
                || Math.Max(
                    Math.Abs(intent.TargetX - player.PositionX),
                    Math.Abs(intent.TargetY - player.PositionY)) > spell.Range
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
                applyEffects: false);
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
            && (player.Life <= checked((int)player.CharacterLevel * 64)
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
                players);
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
                players);
            if (!townPortalResult.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    townPortalResult.Failure ?? DiabloRuleFailure.Malformed);
        }
        if (isWarp) {
            var warpResult = worldCommands!.ExecuteWarp(
                player,
                appliedTick,
                players);
            if (!warpResult.Succeeded)
                return DiabloCombatIntentResult.Rejected(
                    warpResult.Failure ?? DiabloRuleFailure.Malformed);
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
                    DiabloFixedPointQ6.ToWholeTruncated(appliedHealingQ6)) {
                    AmountQ6 = appliedHealingQ6,
                });
            }
        } else if (isManaShield) {
            player.ManaShieldActive = true;
        } else if (isReflect) {
            // Native AddReflect uses spell level 2 when the cast does not carry
            // an explicit level, then adds level * character level to uint16
            // charges. Overflow intentionally fizzles the charge increment.
            var reflectSpellLevel = effectiveSpellLevel == 0 ? 2 : effectiveSpellLevel;
            var chargeIncrement = checked((uint)reflectSpellLevel * player.CharacterLevel);
            if (chargeIncrement <= ushort.MaxValue - player.ReflectCharges)
                player.ReflectCharges += chargeIncrement;
        } else if (isMagi) {
            player.Mana = player.ManaMaximum;
        } else if (isMana) {
            player.Mana = Math.Min(
                player.ManaMaximum,
                checked(player.Mana + ResolveManaRestorationQ6(player, effectiveSpellLevel)));
        } else if (isGolem) {
            SpawnGolem(
                player,
                golemSpawnPosition!.Value,
                effectiveSpellLevel,
                spell.GolemDeathAnimationTicks);
        } else if (isBerserk && target is not null) {
            ApplyBerserk(target, spell, effectiveSpellLevel);
        } else if (isJester) {
            var jesterSpellId = SelectJesterSpellId();
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
                    DiabloFixedPointQ6.ToWholeTruncated(appliedHealingQ6)) {
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
                        (uint)projectileTravelTicks),
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
            DiabloFixedPointQ6.FromWhole(spell.SelfDamage));
        if (damageResult.IncomingDamageQ6 > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                player.EntityId,
                player.EntityId,
                DiabloFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
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
    private uint SelectJesterSpellId()
    {
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

        source = default;
        return false;
    }

    private static bool TryResolveChargedItem(
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
        if (reference.Location is not (
                DiabloPlayerItemLocation.Inventory or DiabloPlayerItemLocation.Belt)
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
        int effectiveSpellLevel)
    {
        var manaQ6 = checked(random.RandomIntBetween(1, 10) * 64);
        for (var level = 0; level < player.CharacterLevel; level++)
            manaQ6 = checked(manaQ6 + random.RandomIntBetween(1, 4) * 64);
        for (var level = 0; level < effectiveSpellLevel; level++)
            manaQ6 = checked(manaQ6 + random.RandomIntBetween(1, 6) * 64);

        var classSymbol = playerClasses?.TryGet(player.CharacterClassId, out var playerClass) == true
            ? playerClass.Symbol
            : string.Empty;
        if (classSymbol.Equals("sorcerer", StringComparison.OrdinalIgnoreCase)
            || player.CharacterClassId == 2) {
            return checked(manaQ6 * 2);
        }
        if (classSymbol.Equals("rogue", StringComparison.OrdinalIgnoreCase)
            || classSymbol.Equals("bard", StringComparison.OrdinalIgnoreCase)
            || player.CharacterClassId is 1 or 4) {
            return checked(manaQ6 + manaQ6 / 2);
        }
        return manaQ6;
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
        var pathIndex = random.GenerateRnd(15) + 1;
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

    private static bool IsMatchingChargedItem(AuthoritativeItemState state, uint spellId)
    {
        return DiabloCastSourceRules.IsMatchingChargedItem(state, spellId);
    }

    private bool IsMatchingConsumableItem(AuthoritativeItemState state, uint spellId)
    {
        return DiabloCastSourceRules.IsMatchingConsumableItem(state, spellId, spells);
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
        foreach (var offset in ResurrectionInitialOffsets) {
            var candidateX = checked(target.PositionX + offset.X);
            var candidateY = checked(target.PositionY + offset.Y);
            if (IsResurrectionPositionAvailable(target, candidateX, candidateY, players))
                return (candidateX, candidateY);
        }

        return DiabloCrawl.FindClosestValidPosition(
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
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return DiabloCrawl.FindClosestValidPosition(
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
                    players.Where(candidate => candidate.Life > 0),
                    player.EntityId));
    }

    private (int X, int Y)? FindWallSpreadPosition(
        AuthoritativePlayerState player,
        int targetX,
        int targetY)
    {
        return DiabloCrawl.FindClosestValidPosition(
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
        return DiabloCrawl.FindClosestValidPosition(
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
        return DiabloCrawl.FindClosestValidPosition(
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
        return DiabloCrawl.FindClosestValidPosition(
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
            2 * (320 * effectiveSpellLevel
                + DiabloFixedPointQ6.FromWhole(player.ManaMaximum) / 3));
        var hitPoints = DiabloFixedPointQ6.ToWholeCeiling(hitPointsQ6);
        var minimumDamage = checked(2 * (effectiveSpellLevel + 4));
        var maximumDamage = checked(2 * (effectiveSpellLevel + 8));
        var golem = new AuthoritativeCombatTarget(
            entityId,
            position.X,
            position.Y,
            hitPoints,
            armorClass: 25,
            maxHitPoints: hitPoints,
            levelId: player.LevelId,
            attackDamage: maximumDamage,
            monsterLevel: checked((int)player.CharacterLevel),
            playerMinion: true,
            ownerPlayerId: player.EntityId,
            minDamage: minimumDamage,
            maxDamage: maximumDamage,
            minDamageSpecial: minimumDamage,
            maxDamageSpecial: maximumDamage,
            deathAnimationTicks: deathAnimationTicks);
        golem.RestoreHitPointsQ6(hitPointsQ6, hitPointsQ6);
        world.CombatTargets.Add(entityId, golem);
    }

    private static int RotateDirection(int direction, int offset)
    {
        var rotated = (direction + offset) % DirectionDisplacements.Length;
        return rotated < 0 ? rotated + DirectionDisplacements.Length : rotated;
    }

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
        var position = DiabloCrawl.FindClosestValidPosition(
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
        bool respectMagicResistance)
    {
        if (targetEntityId != 0) {
            if (!world.CombatTargets.TryGetValue(targetEntityId, out var explicitTarget)
                || !IsBerserkTargetEligible(explicitTarget, levelId)
                || (respectMagicResistance && !PassesBerserkResistance(explicitTarget))) {
                return null;
            }
            return explicitTarget;
        }

        AuthoritativeCombatTarget? selectedTarget = null;
        var position = DiabloCrawl.FindClosestValidPosition(
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
                .FirstOrDefault(candidate => !respectMagicResistance || PassesBerserkResistance(candidate));
                return selectedTarget is not null;
            });
        return position.HasValue ? selectedTarget : null;
    }

    private static bool IsBerserkTargetEligible(
        AuthoritativeCombatTarget target,
        uint levelId)
    {
        return target.HitPointsQ6 > 0
            && (target.LevelId == 0 || target.LevelId == levelId)
            && !target.IsPlayerMinion
            && !target.IsBerserked
            && !target.IsUnique
            && !target.IsDiablo
            && !target.BerserkImmune
            && !target.IsPetrified;
    }

    private bool PassesBerserkResistance(AuthoritativeCombatTarget target)
    {
        if (target.MagicResistance >= 100)
            return false;
        return target.MagicResistance <= 0 || random.FlipCoin(2);
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
            if (target.IsMoving && !target.IsPetrified)
                target.CompleteMovement();
            if (target.IsPlayerMinion) {
                AdvancePlayerMinion(target, tick, players, events);
                continue;
            }
            if (target.HitPoints <= 0) {
                if (target.DeathAnimationTicks != 0
                    && target.AdvanceDeathAnimation()) {
                    world.CombatTargets.Remove(target.EntityId);
                }
                continue;
            }
            if (target.AggroRange <= 0)
                continue;
            if (target.IsPetrified)
                continue;

            var victim = players
                .Where(player => player.Life > 0
                    && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel
                        or DiabloPlayerCombatMode.Quit)
                    && player.LevelId == target.LevelId
                    && Math.Max(
                        Math.Abs(player.PositionX - target.PositionX),
                        Math.Abs(player.PositionY - target.PositionY)) <= target.AggroRange)
                .OrderBy(player => Math.Max(
                    Math.Abs(player.PositionX - target.PositionX),
                    Math.Abs(player.PositionY - target.PositionY)))
                .ThenBy(player => player.EntityId)
                .FirstOrDefault();
            if (victim is null)
                continue;

            var distance = Math.Max(
                Math.Abs(victim.PositionX - target.PositionX),
                Math.Abs(victim.PositionY - target.PositionY));
            if (distance <= 1) {
                if (IsPlayerProtectedByFlash(victim)
                    || IsPlayerEtherealized(victim))
                    continue;
                var damage = rules.ResolveAttackDamage(
                    target.AttackDamage,
                    victim.ArmorClass,
                    unchecked((uint)tick ^ target.EntityId ^ victim.EntityId));
                damage = AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                    damage,
                    rules.MinimumDamage,
                    victim.Equipment);
                var reflectedDamage = 0;
                if (damage > 0 && victim.ReflectCharges > 0) {
                    victim.ReflectCharges--;
                    reflectedDamage = checked(
                        damage * random.RandomIntBetween(20, 30) / 100);
                    if (reflectedDamage > 0) {
                        var reflectedEvents = new List<DiabloGameplayEvent>();
                        ApplyResolvedDamageQ6(
                            victim,
                            target,
                            DiabloFixedPointQ6.FromWhole(reflectedDamage),
                            reflectedEvents);
                        foreach (var reflectedEvent in reflectedEvents)
                            events.Add(new DiabloDirectedGameplayEvent(victim, reflectedEvent));
                    }
                }
                var damageResult = playerDamage.Apply(
                    victim,
                    Math.Max(0, damage - reflectedDamage));
                if (damageResult.IncomingDamageQ6 > 0) {
                    events.Add(new DiabloDirectedGameplayEvent(
                        victim,
                        new DiabloGameplayEvent(
                            DiabloGameplayEventKind.Damage,
                            target.EntityId,
                            victim.EntityId,
                            damageResult.IncomingDamageQ6)));
                }
                if (victim.Life == 0)
                    StartPlayerDeath(
                        victim,
                        target.EntityId,
                        tick,
                        players,
                        PlayerDeathReason.MonsterOrTrap);
                continue;
            }

            var deltaX = Math.Sign(victim.PositionX - target.PositionX);
            var deltaY = Math.Sign(victim.PositionY - target.PositionY);
            var candidates = new[] {
                (X: target.PositionX + deltaX, Y: target.PositionY + deltaY),
                (X: target.PositionX + deltaX, Y: target.PositionY),
                (X: target.PositionX, Y: target.PositionY + deltaY),
            };
            foreach (var candidate in candidates.Where(
                         candidate => candidate.X != target.PositionX
                             || candidate.Y != target.PositionY)) {
                if (!world.IsWalkable(target.LevelId, candidate.X, candidate.Y)
                    || world.IsOccupied(
                        target.LevelId,
                        candidate.X,
                        candidate.Y,
                        players,
                        target.EntityId)) {
                    continue;
                }
                target.BeginMovement(candidate.X, candidate.Y);
                break;
            }
        }
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
            .OrderBy(candidate => Math.Max(
                Math.Abs(candidate.PositionX - minion.PositionX),
                Math.Abs(candidate.PositionY - minion.PositionY)))
            .ThenBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        if (enemy is null) {
            TryBeginPlayerMinionIdleWalk(minion, owner, players);
            return;
        }

        var distance = Math.Max(
            Math.Abs(enemy.PositionX - minion.PositionX),
            Math.Abs(enemy.PositionY - minion.PositionY));
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
                DiabloFixedPointQ6.FromWhole(damage),
                minionEvents);
            foreach (var minionEvent in minionEvents)
                events.Add(new DiabloDirectedGameplayEvent(owner, minionEvent));
            return;
        }

        var nextStep = FindPlayerMinionStep(minion, enemy, players);
        if (nextStep.HasValue)
            minion.BeginMovement(nextStep.Value.X, nextStep.Value.Y);
    }

    private void TryBeginPlayerMinionIdleWalk(
        AuthoritativeCombatTarget minion,
        AuthoritativePlayerState owner,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var ownerDirection = (int)owner.FacingDirection;
        var directions = new List<int> { ownerDirection };
        if (random.FlipCoin(2)) {
            directions.Add(RotateDirection(ownerDirection, 1));
            directions.Add(RotateDirection(ownerDirection, -1));
        } else {
            directions.Add(RotateDirection(ownerDirection, -1));
            directions.Add(RotateDirection(ownerDirection, 1));
        }
        if (random.FlipCoin(2)) {
            directions.Add(RotateDirection(ownerDirection, -2));
            directions.Add(RotateDirection(ownerDirection, 2));
        } else {
            directions.Add(RotateDirection(ownerDirection, 2));
            directions.Add(RotateDirection(ownerDirection, -2));
        }

        foreach (var direction in directions) {
            if (TryBeginPlayerMinionWalk(minion, direction, players))
                return;
        }

        var fallbackDirection = RotateDirection(ownerDirection, -1);
        for (var index = 0; index < DirectionDisplacements.Length; index++) {
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

        var queue = new Queue<(int X, int Y)>();
        var previous = new Dictionary<(int X, int Y), (int X, int Y)>();
        queue.Enqueue(start);
        previous[start] = start;
        while (queue.Count > 0) {
            var current = queue.Dequeue();
            if (goals.Contains(current)) {
                var step = current;
                while (previous[step] != start)
                    step = previous[step];
                return step;
            }

            foreach (var displacement in MinionPathDisplacements) {
                var candidate = (
                    X: current.X + displacement.X,
                    Y: current.Y + displacement.Y);
                if (previous.ContainsKey(candidate)
                    || !world.IsWalkable(minion.LevelId, candidate.X, candidate.Y)
                    || (!goals.Contains(candidate)
                        && world.IsOccupied(
                            minion.LevelId,
                            candidate.X,
                            candidate.Y,
                            players,
                            minion.EntityId))) {
                    continue;
                }
                previous[candidate] = current;
                queue.Enqueue(candidate);
            }
        }
        return null;
    }

    private static readonly (int X, int Y)[] MinionPathDisplacements = [
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
        (0, -1),
        (1, -1),
    ];

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
                    AdvanceApocalypseBoom(projectile, players, events);
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
                if (projectile.RemainingTicks > 1) {
                    var nextX = projectile.PositionX
                        + Math.Sign(projectile.TargetX - projectile.PositionX);
                    var nextY = projectile.PositionY
                        + Math.Sign(projectile.TargetY - projectile.PositionY);
                    if (world.IsMissileBlocked(projectile.LevelId, nextX, nextY)
                        || !world.HasMissileLineOfSight(
                            projectile.LevelId,
                            projectile.PositionX,
                            projectile.PositionY,
                            nextX,
                            nextY)) {
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
                                     && Math.Max(
                                         Math.Abs(target.PositionX - projectile.TargetX),
                                         Math.Abs(target.PositionY - projectile.TargetY))
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
            players);
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
            if (projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar
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
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar) {
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
                if (projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar) {
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
            foreach (var displacement in DiabloCrawl.EnumerateDisplacements(
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
                && Math.Max(
                    Math.Abs(candidate.PositionX - projectile.PositionX),
                    Math.Abs(candidate.PositionY - projectile.PositionY)) <= 19)
            .OrderBy(candidate => Math.Max(
                Math.Abs(candidate.PositionX - projectile.PositionX),
                Math.Abs(candidate.PositionY - projectile.PositionY)))
            .ThenBy(candidate => candidate.EntityId)
            .FirstOrDefault();
        var owner = players.FirstOrDefault(candidate => IsProjectileOwner(projectile, candidate));
        var targetX = target?.PositionX ?? projectile.PositionX;
        var targetY = target?.PositionY ?? projectile.PositionY;
        if (target is null && owner is not null) {
            var displacement = DiabloPlayerDirections.GetDisplacement(owner.FacingDirection);
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
        }

        if (expiresAfterMovement)
            EnterFireballImpact(tick, projectile, players, events);
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
        foreach (var offset in FireballExplosionOffsets) {
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
            DiabloCrawl.MaximumSupportedRadius);
        var targetIds = new HashSet<uint>();
        foreach (var displacement in DiabloCrawl.EnumerateDisplacements(1, radius)) {
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

        var quarterRadius = new List<(int X, int Y)>();
        for (var y = 0; y <= spell.NovaSearchRadius; y++)
            quarterRadius.Add((spell.NovaSearchRadius, y));
        for (var x = spell.NovaSearchRadius - 1; x >= 0; x--)
            quarterRadius.Add((x, spell.NovaSearchRadius));
        foreach (var quarter in quarterRadius) {
            var offsets = new[] {
                quarter,
                (-quarter.X, -quarter.Y),
                (-quarter.X, quarter.Y),
                (quarter.X, -quarter.Y),
            };
            foreach (var offset in offsets) {
                var entityId = AllocateProjectileEntityId();
                world.Projectiles.Add(
                    entityId,
                    CreateNovaBall(
                        entityId,
                        controller,
                        spell,
                        checked(controller.PositionX + offset.Item1),
                        checked(controller.PositionY + offset.Item2)));
            }
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

        var quarterRadius = new List<(int X, int Y)>();
        for (var y = 0; y <= spell.ImmolationSearchRadius; y++)
            quarterRadius.Add((spell.ImmolationSearchRadius, y));
        for (var x = spell.ImmolationSearchRadius - 1; x >= 0; x--)
            quarterRadius.Add((x, spell.ImmolationSearchRadius));
        foreach (var quarter in quarterRadius) {
            var offsets = new[] {
                quarter,
                (-quarter.X, -quarter.Y),
                (-quarter.X, quarter.Y),
                (quarter.X, -quarter.Y),
            };
            foreach (var offset in offsets) {
                var entityId = AllocateProjectileEntityId();
                world.Projectiles.Add(
                    entityId,
                    CreateImmolationBall(
                        entityId,
                        controller,
                        spell,
                        checked(controller.PositionX + offset.Item1),
                        checked(controller.PositionY + offset.Item2)));
            }
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

    private void AdvanceApocalypseController(
        AuthoritativeProjectile controller,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!spells.TryGet(controller.SpellId, out var spell)
            || spell.ProjectileBehavior != AuthoritativeProjectileBehavior.ApocalypseController
            || !players.Any(player => IsProjectileOwner(controller, player))) {
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
        AuthoritativeProjectile projectile,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        projectile.RemainingTicks--;
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
                    DiabloFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
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
            ApplyElementalBurst(tick, projectile, players, events);
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
        foreach (var offset in GuardianArc) {
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
        if (Math.Max(
                Math.Abs(targetX - controller.PositionX),
                Math.Abs(targetY - controller.PositionY)) > spell.GuardianTargetSearchRadius)
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
                && Math.Max(
                    Math.Abs(target.PositionX - projectile.PositionX),
                    Math.Abs(target.PositionY - projectile.PositionY)) <= 19)
            .OrderBy(target => Math.Max(
                Math.Abs(target.PositionX - projectile.PositionX),
                Math.Abs(target.PositionY - projectile.PositionY)))
            .ThenBy(target => target.EntityId)
            .FirstOrDefault();
        var targetX = nextMonster?.PositionX
            ?? checked(projectile.PositionX
                + DiabloPlayerDirections.GetDisplacement(owner.FacingDirection).X);
        var targetY = nextMonster?.PositionY
            ?? checked(projectile.PositionY
                + DiabloPlayerDirections.GetDisplacement(owner.FacingDirection).Y);
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
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var startX = projectile.PathIndex == 1
            ? projectile.ElementalOriginX
            : projectile.StartX;
        var startY = projectile.PathIndex == 1
            ? projectile.ElementalOriginY
            : projectile.StartY;
        foreach (var offset in ElementalBurstOffsets) {
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
                _ = TryResolvePersistentShiftedProjectilePlayerHit(
                    tick,
                    projectile,
                    playerTarget,
                    players,
                    events);
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
            ? FlashBottomOffsets
            : FlashTopOffsets;
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

        if (projectile.RemainingTicks == 0)
            world.Projectiles.Remove(projectile.EntityId);
    }

    private static void EnterProjectileImpact(AuthoritativeProjectile projectile)
    {
        projectile.Phase = AuthoritativeProjectilePhase.Impact;
        projectile.RemainingTicks = (uint)projectile.ImpactLifetime;
        projectile.VelocityX = 0;
        projectile.VelocityY = 0;
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

        if (projectile.PathTicks == 0) {
            var direction = projectile.OriginalDirection;
            direction = ChargedBoltPath[projectile.PathIndex] switch {
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
            ? Math.Max(
                Math.Abs(previousX - finalTile.X),
                Math.Abs(previousY - finalTile.Y))
            : Math.Abs(previousX - finalTile.X)
                + Math.Abs(previousY - finalTile.Y);
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
        var playerStopsProjectile = false;
        if (!hitTarget && playerTarget is not null) {
            _ = TryResolveFireboltPlayerHit(
                tick,
                projectile,
                playerTarget,
                players,
                events,
                out playerStopsProjectile);
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
        return blocked || hitTarget || playerStopsProjectile;
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

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - target.MonsterLevel * 2,
            5,
            95);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DiabloFixedPointQ6.FromWhole(projectile.Damage);
        if (resistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
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

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - target.MonsterLevel * 2,
            5,
            95);
        if (!target.IsPetrified && random.RandomIntLessThan(100) >= hitChance)
            return false;

        var damageQ6 = target.HitPointsQ6 / 3 / DiabloFixedPointQ6.Scale
            * DiabloFixedPointQ6.Scale;
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

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + (ownerClass?.BaseMagicToHit ?? 50)
                - checked((int)target.CharacterLevel * 2),
            5,
            95);
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
                + checked((int)target.CharacterLevel * 2)
                - checked((int)owner.CharacterLevel * 2),
            0,
            100);
        var damageQ6 = target.Life / 3;
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
                    DiabloFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
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

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + (ownerClass?.BaseMagicToHit ?? 50)
            - target.MonsterLevel * 2,
            5,
            95);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DiabloFixedPointQ6.FromWhole(
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
            - checked((int)target.CharacterLevel * 2),
            5,
            95);
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
            + checked((int)target.CharacterLevel * 2)
            - checked((int)owner.CharacterLevel * 2),
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
                    DiabloFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
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

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + (ownerClass?.BaseMagicToHit ?? 50)
            - target.MonsterLevel * 2,
            5,
            95);
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

        var ownerClass = GetPlayerClass(owner);
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + (ownerClass?.BaseMagicToHit ?? 50)
            - checked((int)target.CharacterLevel * 2),
            5,
            95);
        if (random.RandomIntLessThan(100) >= hitChance)
            return false;

        var damageQ6 = random.RandomIntBetween(projectile.Damage, projectile.Damage) / 2;
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
                    DiabloFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
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
        var hitTarget = target is not null
            && TryResolveChargedBoltHit(projectile, target, players, events);
        if (!hitTarget && playerTarget is not null) {
            hitTarget = TryResolveChargedBoltPlayerHit(
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
            - checked((int)target.CharacterLevel * 2),
            5,
            95);
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
            + checked((int)target.CharacterLevel * 2)
            - checked((int)owner.CharacterLevel * 2),
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
                    DiabloFixedPointQ6.ToWholeTruncated(damageResult.IncomingDamageQ6)) {
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
            else
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
                out _)) {
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
                DiabloGoldInventory.CreateState(stackValue, rules));
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
            var preferredDirection = DiabloPlayerDirections.RotateLeft(
                player.FacingDirection,
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
                && item.State.ItemIndex == definition.NativeItemIndex
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
            if (!DiabloGoldInventory.IsValidStack(item, rules)
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
                    DiabloGoldInventory.CreateState(amount, rules));
                player.Inventory[index] = DiabloGoldInventory.WithValue(
                    item,
                    item.Price - amount,
                    rules);
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
            var displacement = DiabloPlayerDirections.GetDisplacement(
                preferredDirection.Value);
            var preferredX = checked(player.PositionX + displacement.X);
            var preferredY = checked(player.PositionY + displacement.Y);
            if (IsDeathDropPositionAvailable(player, players, preferredX, preferredY))
                return (preferredX, preferredY);
        }
        return DiabloCrawl.FindClosestValidPosition(
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
        player.FacingDirection = DiabloPlayerDirections.Toward(
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
        while (nextWorldItemEntityId == 0
            || players.Any(player => player.EntityId == nextWorldItemEntityId)
            || world.CombatTargets.ContainsKey(nextWorldItemEntityId)
            || world.WorldItems.ContainsKey(nextWorldItemEntityId)
            || world.Objects.ContainsKey(nextWorldItemEntityId)
            || world.Projectiles.ContainsKey(nextWorldItemEntityId)) {
            nextWorldItemEntityId = checked(nextWorldItemEntityId + 1);
        }
        return nextWorldItemEntityId++;
    }

    private void StartBlock(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        ulong tick)
    {
        DiabloPlayerActionLifecycle.Cancel(player);
        var blockingFrames = GetPlayerAnimation(player)?.BlockingFrames ?? 2;
        var skippedFrames = HasUsableEquipmentFlag(player, FastBlockFlag)
                || IsNativeFastBlockingMonk(player, playerClass)
            ? blockingFrames - 2
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
        var threshold = checked((int)player.CharacterLevel);
        if (string.Equals(playerClass?.Symbol, "barbarian", StringComparison.Ordinal))
            threshold += threshold / 4;
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
            ? 3
            : (flags & FasterHitRecoveryFlag) != 0
                ? 2
                : (flags & FastHitRecoveryFlag) != 0
                    ? 1
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

    private static DiabloPlayerAttackAnimationProfile GetAttackAnimationProfile(
        AuthoritativePlayerState player)
    {
        var hands = player.Equipment
            .Where(item => item.Slot is 4 or 5 && IsUsable(item.State))
            .OrderBy(item => item.Slot)
            .ToArray();
        var hasShield = hands.Any(item => item.State.ItemType == 5);
        var weapon = hands.FirstOrDefault(item => item.State.ItemClass == 1
            && item.State.ItemType != 5);
        return weapon?.State.ItemType switch {
            1 => hasShield
                ? DiabloPlayerAttackAnimationProfile.SwordShield
                : DiabloPlayerAttackAnimationProfile.Sword,
            2 => DiabloPlayerAttackAnimationProfile.Axe,
            3 => DiabloPlayerAttackAnimationProfile.Bow,
            4 => hasShield
                ? DiabloPlayerAttackAnimationProfile.MaceShield
                : DiabloPlayerAttackAnimationProfile.Mace,
            10 => DiabloPlayerAttackAnimationProfile.Staff,
            _ => hasShield
                ? DiabloPlayerAttackAnimationProfile.UnarmedShield
                : DiabloPlayerAttackAnimationProfile.Unarmed,
        };
    }

    private int GetAttackSkippedFrames(AuthoritativePlayerState player, bool ranged)
    {
        var flags = GetUsableEquipmentFlags(player);
        if (ranged) {
            return rules.GameVariant != DiabloGameVariant.Hellfire
                    && (flags & FastAttackFlag) != 0
                ? 1
                : 0;
        }
        if ((flags & (FastestAttackFlag | FasterAttackFlag)) != 0)
            return 2;
        if ((flags & FastAttackFlag) != 0)
            return 1;
        // Native first actions do not skip a logic frame for Quick Attack.
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

    private static int GetPlayerResistance(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        AuthoritativeDamageType damageType)
    {
        var naturalResistanceBonus = playerClass?.Flags.HasFlag(
            DiabloPlayerClassFlags.NaturalResistance) == true
            ? checked((int)player.CharacterLevel)
            : 0;
        return AuthoritativeEquipmentResistanceRules.Calculate(
            player.Equipment,
            damageType,
            naturalResistanceBonus);
    }

    private bool CanBlock(AuthoritativePlayerState player)
    {
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        if (IsUsableType(left, 5) || IsUsableType(right, 5))
            return true;

        var playerClass = GetPlayerClass(player);
        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.FlexibleBlock) != true)
            return false;
        if (IsUsableType(left, 10) || IsUsableType(right, 10))
            return true;
        if (left is null && right is null)
            return true;
        return left is not null
            && right is null
            && left.State.ItemClass == 1
            && left.State.EquipLocation != 2
            || right is not null
            && left is null
            && right.State.ItemClass == 1
            && right.State.EquipLocation != 2;
    }

    private static bool HasUsableEquipmentFlag(AuthoritativePlayerState player, uint flag)
    {
        return (GetUsableEquipmentFlags(player) & flag) != 0;
    }

    private static uint GetUsableEquipmentFlags(AuthoritativePlayerState player)
    {
        return player.Equipment
            .Where(item => IsUsable(item.State)
                && (item.State.Magical == 0 || item.State.Identified))
            .Aggregate(0U, (flags, item) => flags | item.State.Flags);
    }

    private static bool IsNativeFastBlockingMonk(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass)
    {
        if (!string.Equals(playerClass?.Symbol, "monk", StringComparison.Ordinal))
            return false;
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        return IsUsableType(left, 10) || IsUsableType(right, 10);
    }

    private static bool IsUsableType(EquippedStoreItem? item, int itemType)
    {
        return item is not null
            && item.State.ItemType == itemType
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

        var baseMagicToHit = 50;
        if (playerClasses is not null
            && playerClasses.TryGet(owner.CharacterClassId, out var playerClass)) {
            baseMagicToHit = playerClass.BaseMagicToHit;
        }
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
            + baseMagicToHit
            - target.MonsterLevel * 2,
            5,
            95);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DiabloFixedPointQ6.FromWhole(
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
    {
        var deltaX = destinationX - startX;
        var deltaY = destinationY - startY;
        int direction;
        if (deltaX >= 0) {
            if (deltaY >= 0) {
                if (5 * deltaX <= deltaY * 2)
                    return 1;
                direction = 0;
            } else {
                deltaY = -deltaY;
                if (5 * deltaX <= deltaY * 2)
                    return 5;
                direction = 6;
            }
            if (5 * deltaY <= deltaX * 2)
                direction = 7;
        } else {
            deltaX = -deltaX;
            if (deltaY >= 0) {
                if (5 * deltaX <= deltaY * 2)
                    return 1;
                direction = 2;
            } else {
                deltaY = -deltaY;
                if (5 * deltaX <= deltaY * 2)
                    return 5;
                direction = 4;
            }
            if (5 * deltaY <= deltaX * 2)
                direction = 3;
        }
        return direction;
    }

    private static (int X, int Y) CalculateVelocity(
        int startX,
        int startY,
        int destinationX,
        int destinationY,
        int speed)
    {
        if (startX == destinationX && startY == destinationY)
            return (0, 0);
        var deltaX = startX - destinationX;
        var deltaY = startY - destinationY;
        var rotatedX = deltaY - deltaX;
        var rotatedY = -(deltaY + deltaX);
        var magnitude = MathF.Sqrt((float)checked(
            rotatedX * rotatedX + rotatedY * rotatedY));
        var normalX = (int)(unchecked(rotatedX << 16) / magnitude);
        var normalY = (int)(unchecked(rotatedY << 16) / magnitude);
        normalY /= 2;
        return (unchecked(normalX * speed), unchecked(normalY * speed));
    }

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
        var damage = rules.ResolveDamage(baseDamage, target.ArmorClass, damageType, target);
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
            DiabloFixedPointQ6.FromWhole(damage),
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
                DiabloFixedPointQ6.ToWholeTruncated(damageQ6)) {
                AmountQ6 = damageQ6,
            });
        }
        if (!wasAlive || target.HitPointsQ6 != 0)
            return;

        target.CancelMovement();
        target.BeginDeathAnimation();

        AdvanceQuestForMonsterDefeat(target);

        DiabloPlayerProgression.GrantExperience(player, rules.DefeatExperience);
        events.Add(new DiabloGameplayEvent(
            DiabloGameplayEventKind.Experience,
            player.EntityId,
            player.EntityId,
            checked((int)rules.DefeatExperience)));
        if (target.Drop is not null)
            world.WorldItems[target.Drop.EntityId] = target.Drop;
    }

    /** Applies an optional external quest kill trigger at the authoritative death boundary. */
    private void AdvanceQuestForMonsterDefeat(AuthoritativeCombatTarget target)
    {
        if (target.MonsterId == 0)
            return;
        foreach (var quest in world.Quests.Values
                     .Where(quest => quest.Active
                         && !quest.Unavailable
                         && !quest.Completed
                         && quest.CompletionMonsterId == target.MonsterId
                         && (quest.LevelId == 0 || quest.LevelId == target.LevelId))) {
            quest.Progress = Math.Min(
                quest.RequiredProgress,
                checked(quest.Progress + 1));
            quest.Logged = true;
            if (quest.Progress == quest.RequiredProgress) {
                quest.Completed = true;
                quest.Active = false;
            }
        }
    }

    private uint AllocateProjectileEntityId()
    {
        while (nextProjectileEntityId == 0
            || world.CombatTargets.ContainsKey(nextProjectileEntityId)
            || world.WorldItems.ContainsKey(nextProjectileEntityId)
            || world.Objects.ContainsKey(nextProjectileEntityId)
            || world.Projectiles.ContainsKey(nextProjectileEntityId)) {
            nextProjectileEntityId = checked(nextProjectileEntityId + 1);
        }
        return nextProjectileEntityId++;
    }

}
