using Devilution.Engine;

namespace Devilution.Diablo;

/**
 * Native L5-L8 ranged families (monsters.cpp SkeletonBowAi, AiRanged, AiRangedAvoidance, and the
 * Familiar's BatAi)
 * with the Stand, Delay, attack and ranged-attack mode handlers they rely on. State lives on
 * the actor: NativeAiVars (var1/var2/goal) and the shared animation cursor.
 */
public sealed partial class DiabloCombatCommandProcessor
{
    private const int NativeStandMode = 0;
    private const int NativeMeleeAttackMode = 4;
    private const int NativeSpecialMeleeAttackMode = 7;
    private const int NativeRangedAttackMode = 10;
    private const int NativeSpecialRangedAttackMode = 12;
    private const int NativeDelayMode = 13;
    private const int NativeFullActiveWindow = byte.MaxValue;
    private const int NativeNormalGoal = 1;
    private const int NativeRetreatGoal = 2;
    private const int NativeMoveGoal = 4;
    private const int NativeFamiliarTypeId = 41;

    private static readonly AuthoritativeNativeAiVars InitialNativeAiVars = new(NativeStandMode, 0, false, NativeNormalGoal, 0, 0);

    private static bool IsNativeCatacombsRangedFamily(AuthoritativeCombatTarget target)
        => IsNativeCatacombsActor(target)
            && (target.NativeAi is "SkeletonRanged" or "GoatRanged" or "Acid" or "Magma" || IsNativeFamiliar(target));

    private static bool IsNativeFamiliar(AuthoritativeCombatTarget target)
        => target.NativeAi == "Bat" && target.NativeInitialization?.Binding.NativeTypeId == NativeFamiliarTypeId;

    /**
     * One ProcessMonsters update: the AI runs while standing, then UpdateModeStance; a mode
     * that completes returns to Stand, applies GroupUnity and repeats both.
     */
    private void AdvanceNativeCatacombsRangedFamily(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var enemy = SelectNativeEnemy(target, players);
        for (var pass = 0; pass < 8; pass++) {
            if (enemy is not null && IsNativeStanding(target))
                RunNativeRangedAi(target, enemy, players);
            if (!UpdateNativeRangedModeStance(target, enemy, players, tick, events))
                return;
            ApplyNativeCatacombsGroupUnityAfterModeCompletion(target);
        }
        throw new InvalidOperationException("A native ranged actor completed too many modes in one update.");
    }

    private static AuthoritativePlayerState? SelectNativeEnemy(
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players)
        => players
            .Where(player => player.Life > 0 && player.LevelId == target.LevelId
                && player.CombatMode is not (DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit))
            .OrderBy(player => DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX, player.PositionY, target.PositionX, target.PositionY))
            .ThenBy(player => player.EntityId)
            .FirstOrDefault();

    private static bool IsNativeStanding(AuthoritativeCombatTarget target)
        => target.NativeMeleeCursor is null && !target.IsMoving && target.NativeAiVars is not { Delaying: true };

    /** Native startStand: var1 records the completed mode and var2 restarts the stand count. */
    private static void StartNativeStand(AuthoritativeCombatTarget target, int completedMode)
        => target.RestoreNativeAiVars((target.NativeAiVars ?? InitialNativeAiVars) with {
            Var1 = completedMode,
            Var2 = 0,
            Delaying = false,
        });

    /** UpdateModeStance; true when the current mode completed and the actor is standing again. */
    private bool UpdateNativeRangedModeStance(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState? enemy,
        IReadOnlyList<AuthoritativePlayerState> players,
        ulong tick,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        // A walk admitted this update receives its first MonsterWalk with the other new walks.
        if (target.IsMoving)
            return false;
        var vars = target.NativeAiVars ?? InitialNativeAiVars;
        if (target.NativeMeleeCursor is { } cursor) {
            var step = AdvanceNativeMelee(target);
            if (step.EffectFrame) {
                if (cursor.RangedMissile is { } missile) {
                    ReleaseNativeRangedMissile(target, enemy, cursor, missile);
                } else {
                    if (enemy is not null
                        && DeterministicGridMetrics.ChebyshevDistance(target.PositionX, target.PositionY,
                            enemy.PositionX, enemy.PositionY) <= 1
                        && !IsPlayerProtectedByFlash(enemy) && !IsPlayerEtherealized(enemy))
                        ResolveMonsterMeleeEffect(target, enemy, players, tick, events, enemy.ArmorClass,
                            unchecked((uint)tick ^ target.EntityId ^ enemy.EntityId), step.UsesSpecialAttack);
                    // MonsterAttack's playEffect picks a sound variant.
                    random.AdvanceRndSeed();
                }
            }
            if (!step.Completed || target.HitPointsQ6 <= 0)
                return false;
            StartNativeStand(target, cursor switch {
                { HitRecovery: true } => NativeHitRecoveryMode,
                { RangedMissile: not null, UsesSpecialAttack: true } => NativeSpecialRangedAttackMode,
                { RangedMissile: not null } => NativeRangedAttackMode,
                { UsesSpecialAttack: true } => NativeSpecialMeleeAttackMode,
                _ => NativeMeleeAttackMode,
            });
            return true;
        }
        if (vars.Delaying) {
            // MonsterDelay: `if (var2-- == 0) startStand`.
            if (vars.Var2 == 0) {
                StartNativeStand(target, NativeDelayMode);
                return true;
            }
            target.RestoreNativeAiVars(vars with { Var2 = vars.Var2 - 1 });
            return false;
        }
        // MonsterIdle counts stand updates.
        target.RestoreNativeAiVars(vars with { Var2 = Math.Min(short.MaxValue, vars.Var2 + 1) });
        return false;
    }

    private void RunNativeRangedAi(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState enemy,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var sameRegion = world.World?.Levels.FirstOrDefault(level => level.LevelId == target.LevelId)
            ?.IsSameTransparencyRegion(target.PositionX, target.PositionY, enemy.FuturePositionX, enemy.FuturePositionY)
            ?? true;
        var context = new NativeRangedAiContext(target, enemy, players,
            target.NativeAiVars ?? InitialNativeAiVars) { SameTransparencyRegion = sameRegion };
        switch (target.NativeAi) {
        case "SkeletonRanged":
            NativeSkeletonBowAi(context);
            break;
        case "GoatRanged":
            NativeAiRanged(context);
            break;
        case "Bat":
            NativeFamiliarBatAi(context);
            break;
        default:
            NativeAiRangedAvoidance(context);
            break;
        }
        target.RestoreNativeAiVars(context.Vars);
    }

    /** SkeletonBowAi: back away from a close enemy, otherwise sometimes shoot. */
    private void NativeSkeletonBowAi(NativeRangedAiContext context)
    {
        var target = context.Target;
        if (target.ActiveForTicks == 0)
            return;
        var md = context.EnemyDirection;
        target.RestoreDirection(md);
        var v = random.GenerateRnd(100);
        var walking = false;
        var vars = context.Vars;
        if (context.Distance < 4
            && (vars.Var2 > 20 && v < 2 * target.Intelligence + 13
                || IsNativeMoveMode(vars.Var1) && vars.Var2 == 0 && v < 2 * target.Intelligence + 63))
            walking = NativeWalk(context, Opposite(md));
        if (!walking && random.GenerateRnd(100) < 2 * target.Intelligence + 3 && NativeLineClearMovingMissile(context))
            StartNativeRangedAttack(context, "Arrow", 4, special: false);
    }

    /** AiRanged (GoatRanged): pause after each shot, back away from a close enemy, then shoot. */
    private void NativeAiRanged(NativeRangedAiContext context)
    {
        var target = context.Target;
        if (target.ActiveForTicks == NativeFullActiveWindow) {
            var md = context.EnemyDirection;
            target.RestoreDirection(md);
            if (context.Vars.Var1 == NativeRangedAttackMode) {
                NativeAiDelay(context, random.GenerateRnd(20));
            } else if (context.Distance < 4) {
                if (random.GenerateRnd(100) < 10 * (target.Intelligence + 7))
                    NativeRandomWalk(context, Opposite(md));
            }
            if (IsNativeStanding(context) && NativeLineClearMovingMissile(context))
                StartNativeRangedAttack(context, "Arrow", 0, special: false);
            return;
        }
        if (target.ActiveForTicks != 0)
            NativeRandomWalk(context, context.LastDirection);
    }

    /** BatAi for the Familiar: wander toward a distant enemy, strike with Lightning, then retreat two steps. */
    private void NativeFamiliarBatAi(NativeRangedAiContext context)
    {
        var target = context.Target;
        if (target.ActiveForTicks == 0)
            return;
        var md = context.LastDirection;
        target.RestoreDirection(md);
        var v = random.GenerateRnd(100);
        var vars = context.Vars;
        if (vars.Goal == NativeRetreatGoal) {
            if (vars.GoalVar1 == 0) {
                NativeRandomWalk(context, Opposite(md));
                context.Vars = vars with { GoalVar1 = vars.GoalVar1 + 1 };
            } else {
                // PickRandomlyAmong({ Right(md), Left(md) }).
                var side = random.GenerateRnd(2) == 0 ? NativeRight(md) : NativeLeft(md);
                NativeRandomWalk(context, side);
                context.Vars = vars with { Goal = NativeNormalGoal };
            }
            return;
        }
        if (context.Distance >= 2) {
            if (vars.Var2 > 20 && v < target.Intelligence + 13
                || IsNativeMoveMode(vars.Var1) && vars.Var2 == 0 && v < target.Intelligence + 63)
                NativeRandomWalk(context, md);
        } else if (v < 4 * target.Intelligence + 8) {
            BeginNativeMelee(target);
            target.RestoreDirection(context.EnemyDirection);
            context.Vars = vars with { Goal = NativeRetreatGoal, GoalVar1 = 0 };
            CreateNativeFamiliarLightning(target, context.Enemy);
        }
    }

    /** AiRangedAvoidance (Acid, Magma): circle a distant enemy, cast specials, melee when adjacent. */
    private void NativeAiRangedAvoidance(NativeRangedAiContext context)
    {
        var target = context.Target;
        if (target.ActiveForTicks == 0)
            return;
        var md = context.LastDirection;
        var lessMissiles = target.NativeAi == "Acid" ? 1 : 0;
        var missile = target.NativeAi == "Acid" ? "Acid" : "MagmaBall";
        var intelligence = target.Intelligence;
        var v = random.GenerateRnd(10000);
        var distance = context.Distance;
        if (distance >= 2 && target.ActiveForTicks == NativeFullActiveWindow && context.SameTransparencyRegion) {
            if (context.Vars.Goal == NativeMoveGoal || distance >= 3 && random.FlipCoin(4u << lessMissiles)) {
                if (context.Vars.Goal != NativeMoveGoal)
                    context.Vars = context.Vars with { GoalVar1 = 0, GoalVar2 = random.GenerateRnd(2) };
                context.Vars = context.Vars with { Goal = NativeMoveGoal };
                var goalVar1 = context.Vars.GoalVar1;
                context.Vars = context.Vars with { GoalVar1 = Math.Min(short.MaxValue, goalVar1 + 1) };
                if (goalVar1 >= 2 * distance && NativeIsDirOk(context, md)) {
                    context.Vars = context.Vars with { Goal = NativeNormalGoal };
                } else if (v < (500 * (intelligence + 1) >> lessMissiles) && NativeLineClearMovingMissile(context)) {
                    StartNativeRangedAttack(context, missile, 0, special: true);
                } else {
                    NativeRoundWalk(context, md);
                }
            }
        } else {
            context.Vars = context.Vars with { Goal = NativeNormalGoal };
        }
        if (context.Vars.Goal != NativeMoveGoal) {
            if ((distance >= 3 && v < (500 * (intelligence + 2) >> lessMissiles)
                    || v < (500 * (intelligence + 1) >> lessMissiles))
                && NativeLineClearMovingMissile(context)) {
                StartNativeRangedAttack(context, missile, 0, special: true);
            } else if (distance >= 2) {
                v = random.GenerateRnd(100);
                if (v < 1000 * (intelligence + 5)
                    || IsNativeMoveMode(context.Vars.Var1) && context.Vars.Var2 == 0 && v < 1000 * (intelligence + 8))
                    NativeRandomWalk(context, md);
            } else if (v < 1000 * (intelligence + 6)) {
                BeginNativeMelee(target);
                target.RestoreDirection(context.EnemyDirection);
            }
        }
        if (IsNativeStanding(context))
            NativeAiDelay(context, random.GenerateRnd(10) + 5);
    }

    /** StartRangedAttack / StartRangedSpecialAttack face the enemy and take the cursor. */
    private static void StartNativeRangedAttack(NativeRangedAiContext context, string missile, int damage, bool special)
    {
        BeginNativeRangedAttack(context.Target, missile, damage, special);
        context.Target.RestoreDirection(context.EnemyDirection);
    }

    /** AiDelay: a positive length enters Delay with var2 as its countdown. */
    private static void NativeAiDelay(NativeRangedAiContext context, int length)
    {
        if (length > 0)
            context.Vars = context.Vars with { Var2 = length, Delaying = true };
    }

    private static bool IsNativeStanding(NativeRangedAiContext context)
        => context.Target.NativeMeleeCursor is null && !context.Target.IsMoving && !context.Vars.Delaying;

    private static bool IsNativeMoveMode(int mode) => mode is >= 1 and <= 3;

    private static int Opposite(int direction) => (direction + 4) % 8;

    private static int NativeLeft(int direction) => (direction + 7) % 8;

    private static int NativeRight(int direction) => (direction + 1) % 8;

    private bool NativeIsDirOk(NativeRangedAiContext context, int direction)
        => FindStepInDirection(context.Target, direction, context.Players).HasValue;

    /** Monster::walk: one step when isDirOK. */
    private bool NativeWalk(NativeRangedAiContext context, int direction)
    {
        if (FindStepInDirection(context.Target, direction, context.Players) is not { } step)
            return false;
        context.Target.BeginMovement(step.X, step.Y);
        return true;
    }

    private bool NativeRandomWalk(NativeRangedAiContext context, int direction)
    {
        if (FindNativeRandomWalkStepFromRandom(context.Target, direction, context.Players) is not { } step)
            return false;
        context.Target.BeginMovement(step.X, step.Y);
        return true;
    }

    /** RoundWalk: turn 90, then 45, then straight; otherwise flip the turn side and RandomWalk away. */
    private void NativeRoundWalk(NativeRangedAiContext context, int direction)
    {
        var turnLeft = context.Vars.GoalVar2 != 0;
        var turn45 = turnLeft ? NativeLeft(direction) : NativeRight(direction);
        var turn90 = turnLeft ? NativeLeft(turn45) : NativeRight(turn45);
        if (NativeWalk(context, turn90) || NativeWalk(context, turn45) || NativeWalk(context, direction))
            return;
        context.Vars = context.Vars with { GoalVar2 = turnLeft ? 0 : 1 };
        NativeRandomWalk(context, Opposite(turn90));
    }

    private bool NativeLineClearMovingMissile(NativeRangedAiContext context)
        => world.HasGuardianTargetLineOfSight(context.Target.LevelId, context.Target.PositionX,
            context.Target.PositionY, context.EnemyX, context.EnemyY);

    /** MonsterRangedAttack / MonsterRangedSpecialAttack on the effect frame: AddMissile, then playEffect. */
    private void ReleaseNativeRangedMissile(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState? enemy,
        AuthoritativeNativeMeleeCursor cursor,
        string missile)
    {
        var created = enemy is not null && AddNativeMonsterMissile(target, enemy, missile, cursor.RangedDamage);
        if (created || !cursor.UsesSpecialAttack)
            random.AdvanceRndSeed();
    }

    /** AddMissile(tile, enemyPosition, direction, missile, TARGET_PLAYERS, monster, damage, 0). */
    private bool AddNativeMonsterMissile(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState enemy,
        string missile,
        int damage)
    {
        var enemyX = enemy.FuturePositionX;
        var enemyY = enemy.FuturePositionY;
        switch (missile) {
        case "Arrow":
            CreateNativeMonsterArrow(target, enemy.EntityId, enemyX, enemyY);
            return true;
        case "Acid":
        case "MagmaBall":
            if (!TryGetMonsterMissile(target, out var mapping) || mapping.MissileId != missile)
                throw new InvalidDataException($"The {target.NativeAi} actor has no {missile} missile mapping.");
            CreateNativeMonsterProjectile(target, enemy.EntityId, enemyX, enemyY,
                Enum.Parse<AuthoritativeNativeMonsterMissileKind>(missile), mapping);
            return true;
        default:
            throw new NotSupportedException($"Native missile {missile} has no L5-L8 monster implementation.");
        }
    }

    /** Mutable AI inputs for one native ranged-AI call; Vars is written back when the AI returns. */
    private sealed class NativeRangedAiContext(
        AuthoritativeCombatTarget target,
        AuthoritativePlayerState enemy,
        IReadOnlyList<AuthoritativePlayerState> players,
        AuthoritativeNativeAiVars vars)
    {
        public AuthoritativeCombatTarget Target { get; } = target;
        public IReadOnlyList<AuthoritativePlayerState> Players { get; } = players;
        public AuthoritativePlayerState Enemy { get; } = enemy;
        public AuthoritativeNativeAiVars Vars { get; set; } = vars;

        /** Native enemyPosition: the player's future tile. */
        public int EnemyX { get; } = enemy.FuturePositionX;
        public int EnemyY { get; } = enemy.FuturePositionY;

        public int Distance => Math.Max(Math.Abs(Target.PositionX - EnemyX), Math.Abs(Target.PositionY - EnemyY));

        /** GetMonsterDirection. */
        public int EnemyDirection => GetDirection(Target.PositionX, Target.PositionY, EnemyX, EnemyY);

        /** GetDirection(tile, position.last); a visible enemy's last position is its future tile. */
        public int LastDirection => EnemyDirection;

        public bool SameTransparencyRegion { get; init; } = true;
    }
}
