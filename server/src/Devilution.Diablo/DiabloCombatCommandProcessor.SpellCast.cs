using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Spell casting, cast sources and spell values (native spells.cpp CastSpell/UseMana). */
public sealed partial class DiabloCombatCommandProcessor
{
    private DiabloCombatIntentResult Cast(
        AuthoritativePlayerState player,
        DiabloCastIntent intent,
        ulong appliedTick,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        intent = NormalizeVisiblePlayerTarget(player, intent, players);
        if (useNativePlayerActionTiming
            && !DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
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
        if (!useNativePlayerActionTiming)
            return ResolveCast(player, intent, appliedTick, players: players);

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
        if (spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Golem
            && !world.AllowsNativeMonsterActivation(player.LevelId))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
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
        return player.MemorizedSpells.Any(spell => spell.SpellId == spellId)
            && GetEffectiveSpellLevel(player, spellId) > 0;
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
}
