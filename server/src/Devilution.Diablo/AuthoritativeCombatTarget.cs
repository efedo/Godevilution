
using Devilution.Domain.Protocol;
using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Mutable native attack or hit-recovery cursor; the current enemy is resolved separately each tick. */
public sealed record AuthoritativeNativeMeleeCursor(int FrameIndex, int TickCounter)
{
    public bool HitRecovery { get; init; }
}

/** Minimal server-owned combat target used by the initial combat boundary. */
public sealed class AuthoritativeCombatTarget
{
    private int hitPointsQ6;
    private int maxHitPointsQ6;

    public AuthoritativeCombatTarget(
        uint entityId,
        int positionX,
        int positionY,
        int hitPoints,
        int armorClass = 0,
        int? maxHitPoints = null,
        uint levelId = 0,
        uint monsterId = 0,
        uint dropItemEntityId = 0,
        uint dropItemSeed = 0,
        uint dropItemPrice = 0,
        AuthoritativeItemState? dropItemState = null,
        int attackDamage = 0,
        int aggroRange = 0,
        int fireResistance = 0,
        int lightningResistance = 0,
        int magicResistance = 0,
        int acidResistance = 0,
        int monsterLevel = 0,
        bool moving = false,
        int? futurePositionX = null,
        int? futurePositionY = null,
        bool petrified = false,
        bool stoneCurseImmune = false,
        bool undead = false,
        bool playerMinion = false,
        uint ownerPlayerId = 0,
        int? minDamage = null,
        int? maxDamage = null,
        int? minDamageSpecial = null,
        int? maxDamageSpecial = null,
        bool unique = false,
        bool diablo = false,
        bool berserkImmune = false,
        int deathAnimationTicks = 0,
        bool demon = false,
        bool animal = false,
        bool knockback = false,
        uint experienceReward = 0,
        int toHit = 0,
        int toHitSpecial = 0,
        string nativeAi = "",
        int intelligence = 0,
        bool gargoyleSpecialActive = false,
        int fallenFearTicks = 0,
        int fallenFearDirection = 0,
        bool sneakHidden = false,
        int sneakRetreatTicks = 0,
        int scavengerHealingTicks = 0,
        uint scavengerCorpseEntityId = 0,
        int batRetreatTicks = 0,
        int batRetreatDirection = 0,
        string uniqueIdentity = "",
        int counselorRetreatTicks = 0,
        int batMovementIdleTicks = -1,
        int batMovementMode = 0,
        int rangedAvoidanceMoveTicks = -1,
        int rangedAvoidanceTurn = 0,
        int rangedAvoidanceMovementIdleTicks = -1,
        int rangedAvoidanceMovementMode = 0,
        int rhinoMovementTicks = -1,
        int rhinoMovementTurn = 0,
        int avoidanceMovementTicks = -1,
        int avoidanceMovementTurn = 0,
        int avoidanceMovementIdleTicks = -1,
        int avoidanceMovementMode = 0,
        int skeletonMeleeDelayTicks = 0,
        int skeletonRangedMovementIdleTicks = -1,
        int skeletonRangedMovementMode = 0,
        int fatMovementIdleTicks = -1,
        int fatMovementMode = 0,
        int rhinoMovementIdleTicks = -1,
        int rhinoMovementMode = 0,
        int rhinoDelayTicks = 0,
        int skeletonKingDelayTicks = 0,
        int horkDemonDelayTicks = 0,
        int rangedAvoidanceDelayTicks = 0,
        int snakeDelayTicks = 0,
        int counselorDelayTicks = 0,
        int avoidanceDelayTicks = 0,
        int rangedDelayTicks = 0,
        int megaDelayTicks = 0,
        int direction = 0,
        int dialogueMessageId = 0,
        uint monsterItemSeed = 0,
        uint monsterAiSeed = 0,
        int initialStandAnimationFrame = 0,
        int initialStandAnimationTick = 0,
        int fallenRallyTicks = 0,
        uint leaderEntityId = 0,
        AuthoritativeMonsterLeaderRelation leaderRelation = AuthoritativeMonsterLeaderRelation.None,
        int packSize = 0,
        int activeForTicks = 0,
        AuthoritativeMonsterDialogueState dialogueState = AuthoritativeMonsterDialogueState.Unspecified,
        uint? dropLevelId = null,
        int? dropPositionX = null,
        int? dropPositionY = null,
        int horkDemonMoveTicks = -1,
        int horkDemonMoveTurn = 0)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if ((monsterItemSeed == 0) != (monsterAiSeed == 0))
            throw new ArgumentException(
                "Monster InitMonster seeds must be supplied together.");
        if (hitPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(hitPoints));
        if (attackDamage < 0 || aggroRange < 0)
            throw new ArgumentOutOfRangeException(nameof(attackDamage));
        if (minDamage is < 0 || maxDamage is < 0 || minDamageSpecial is < 0 || maxDamageSpecial is < 0)
            throw new ArgumentOutOfRangeException(nameof(minDamage));
        if (monsterLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterLevel));
        if (deathAnimationTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDeathAnimationTicks)
            throw new ArgumentOutOfRangeException(nameof(deathAnimationTicks));
        if (experienceReward > NativeCompatibilityLimits.MaximumNativeMonsterExperienceReward)
            throw new ArgumentOutOfRangeException(nameof(experienceReward));
        if (toHit is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterToHit
            || toHitSpecial is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterToHit)
            throw new ArgumentOutOfRangeException(nameof(toHit));
        if (intelligence is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterIntelligence)
            throw new ArgumentOutOfRangeException(nameof(intelligence));
        if (fallenFearTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(fallenFearTicks));
        if (fallenFearDirection is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(fallenFearDirection));
        if (initialStandAnimationFrame is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationFrame)
            throw new ArgumentOutOfRangeException(nameof(initialStandAnimationFrame));
        if (initialStandAnimationTick is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationTick)
            throw new ArgumentOutOfRangeException(nameof(initialStandAnimationTick));
        if (fallenRallyTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenRallyTicks)
            throw new ArgumentOutOfRangeException(nameof(fallenRallyTicks));
        if (!Enum.IsDefined(leaderRelation))
            throw new ArgumentOutOfRangeException(nameof(leaderRelation));
        if (packSize is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterPackSize)
            throw new ArgumentOutOfRangeException(nameof(packSize));
        if (activeForTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterActiveTicks)
            throw new ArgumentOutOfRangeException(nameof(activeForTicks));
        if (leaderRelation == AuthoritativeMonsterLeaderRelation.None
            ? leaderEntityId != 0
            : leaderEntityId == 0)
            throw new ArgumentException(
                "Monster leader identity and relation must be supplied together.",
                nameof(leaderEntityId));
        if (leaderRelation != AuthoritativeMonsterLeaderRelation.None && unique)
            throw new ArgumentException(
                "Unique monsters cannot be members of another monster pack.",
                nameof(leaderRelation));
        if (sneakRetreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(sneakRetreatTicks));
        if (scavengerHealingTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(scavengerHealingTicks));
        if (batRetreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks)
            throw new ArgumentOutOfRangeException(nameof(batRetreatTicks));
        if (batRetreatDirection is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(batRetreatDirection));
        if (batMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(batMovementIdleTicks));
        if (batMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(batMovementMode));
        if (rangedAvoidanceMoveTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(rangedAvoidanceMoveTicks));
        if (rangedAvoidanceTurn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(rangedAvoidanceTurn));
        if (rangedAvoidanceMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(rangedAvoidanceMovementIdleTicks));
        if (rangedAvoidanceMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(rangedAvoidanceMovementMode));
        if (rhinoMovementTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(rhinoMovementTicks));
        if (rhinoMovementTurn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(rhinoMovementTurn));
        if (avoidanceMovementTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(avoidanceMovementTicks));
        if (avoidanceMovementTurn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(avoidanceMovementTurn));
        if (avoidanceMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(avoidanceMovementIdleTicks));
        if (avoidanceMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(avoidanceMovementMode));
        if (skeletonMeleeDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(skeletonMeleeDelayTicks));
        if (skeletonRangedMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(skeletonRangedMovementIdleTicks));
        if (skeletonRangedMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(skeletonRangedMovementMode));
        if (fatMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(fatMovementIdleTicks));
        if (fatMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(fatMovementMode));
        if (rhinoMovementIdleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(rhinoMovementIdleTicks));
        if (rhinoMovementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(rhinoMovementMode));
        if (rhinoDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(rhinoDelayTicks));
        if (skeletonKingDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(skeletonKingDelayTicks));
        if (horkDemonDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(horkDemonDelayTicks));
        if (horkDemonMoveTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(horkDemonMoveTicks));
        if (horkDemonMoveTurn is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(horkDemonMoveTurn));
        if (rangedAvoidanceDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(rangedAvoidanceDelayTicks));
        if (snakeDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(snakeDelayTicks));
        if (counselorDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(counselorDelayTicks));
        if (avoidanceDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(avoidanceDelayTicks));
        if (rangedDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(rangedDelayTicks));
        if (megaDelayTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(megaDelayTicks));
        if (direction is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (dialogueMessageId is < 0 or > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dialogueMessageId));
        if (!Enum.IsDefined(dialogueState))
            throw new ArgumentOutOfRangeException(nameof(dialogueState));
        if (dialogueState != AuthoritativeMonsterDialogueState.Unspecified && !unique)
            throw new ArgumentException(
                "An authored dialogue state requires a unique encounter.",
                nameof(dialogueState));
        if (dialogueMessageId != 0 && !unique)
            throw new ArgumentException(
                "A dialogue identity requires a unique encounter.",
                nameof(dialogueMessageId));
        if (counselorRetreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks)
            throw new ArgumentOutOfRangeException(nameof(counselorRetreatTicks));
        if (!NativeTextLimits.IsIdentityWithinLimit(uniqueIdentity))
            throw new ArgumentOutOfRangeException(nameof(uniqueIdentity));
        if (!string.IsNullOrWhiteSpace(uniqueIdentity) && !unique)
            throw new ArgumentException(
                "A unique monster identity requires the unique monster flag.",
                nameof(uniqueIdentity));
        if (!string.IsNullOrEmpty(nativeAi)
            && !AuthoritativeMonsterTypeCatalog.IsValidNativeAi(nativeAi))
            throw new ArgumentException("The monster native AI identity is invalid.", nameof(nativeAi));
        if (fireResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance
            || lightningResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance
            || magicResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance
            || acidResistance is < NativeCompatibilityLimits.MinimumNativeMonsterResistance or > NativeCompatibilityLimits.MaximumNativeMonsterResistance)
            throw new ArgumentOutOfRangeException(nameof(fireResistance));
        var resolvedFuturePositionX = futurePositionX ?? positionX;
        var resolvedFuturePositionY = futurePositionY ?? positionY;
        if (moving) {
            if (DeterministicGridMetrics.ChebyshevDistance(
                    positionX,
                    positionY,
                    resolvedFuturePositionX,
                    resolvedFuturePositionY) != 1) {
                throw new ArgumentException(
                    "A moving monster must have an adjacent future position.");
            }
        } else if (resolvedFuturePositionX != positionX
            || resolvedFuturePositionY != positionY) {
            throw new ArgumentException(
                "A stationary monster's future position must match its current position.");
        }

        EntityId = entityId;
        PositionX = positionX;
        PositionY = positionY;
        FuturePositionX = resolvedFuturePositionX;
        FuturePositionY = resolvedFuturePositionY;
        Direction = direction;
        DialogueMessageId = dialogueMessageId;
        DialogueState = dialogueState;
        IsMoving = moving;
        IsPetrified = petrified;
        StoneCurseImmune = stoneCurseImmune;
        IsUndead = undead;
        IsDemon = demon;
        IsAnimal = animal;
        HasKnockback = knockback;
        IsPlayerMinion = playerMinion;
        OwnerPlayerId = ownerPlayerId;
        IsUnique = unique;
        UniqueIdentity = uniqueIdentity;
        IsDiablo = diablo;
        BerserkImmune = berserkImmune;
        DeathAnimationTicks = deathAnimationTicks;
        ExperienceReward = experienceReward;
        ToHit = toHit;
        ToHitSpecial = toHitSpecial;
        NativeAi = nativeAi;
        Intelligence = intelligence;
        GargoyleSpecialActive = gargoyleSpecialActive;
        FallenFearTicks = fallenFearTicks;
        FallenFearDirection = fallenFearDirection;
        FallenStandAnimationFrame = initialStandAnimationFrame;
        FallenStandAnimationTick = initialStandAnimationTick;
        FallenRallyTicks = fallenRallyTicks;
        LeaderEntityId = leaderEntityId;
        LeaderRelation = leaderRelation;
        PackSize = packSize;
        ActiveForTicks = activeForTicks;
        SneakHidden = sneakHidden;
        SneakRetreatTicks = sneakRetreatTicks;
        ScavengerHealingTicks = scavengerHealingTicks;
        ScavengerCorpseEntityId = scavengerCorpseEntityId;
        BatRetreatTicks = batRetreatTicks;
        BatRetreatDirection = batRetreatDirection;
        CounselorRetreatTicks = counselorRetreatTicks;
        BatMovementIdleTicks = batMovementIdleTicks;
        BatMovementMode = batMovementMode;
        RangedAvoidanceMoveTicks = rangedAvoidanceMoveTicks;
        RangedAvoidanceTurn = rangedAvoidanceTurn;
        RangedAvoidanceMovementIdleTicks = rangedAvoidanceMovementIdleTicks;
        RangedAvoidanceMovementMode = rangedAvoidanceMovementMode;
        RhinoMovementTicks = rhinoMovementTicks;
        RhinoMovementTurn = rhinoMovementTurn;
        AvoidanceMovementTicks = avoidanceMovementTicks;
        AvoidanceMovementTurn = avoidanceMovementTurn;
        AvoidanceMovementIdleTicks = avoidanceMovementIdleTicks;
        AvoidanceMovementMode = avoidanceMovementMode;
        SkeletonMeleeDelayTicks = skeletonMeleeDelayTicks;
        SkeletonRangedMovementIdleTicks = skeletonRangedMovementIdleTicks;
        SkeletonRangedMovementMode = skeletonRangedMovementMode;
        FatMovementIdleTicks = fatMovementIdleTicks;
        FatMovementMode = fatMovementMode;
        RhinoMovementIdleTicks = rhinoMovementIdleTicks;
        RhinoMovementMode = rhinoMovementMode;
        RhinoDelayTicks = rhinoDelayTicks;
        SkeletonKingDelayTicks = skeletonKingDelayTicks;
        HorkDemonDelayTicks = horkDemonDelayTicks;
        HorkDemonMoveTicks = horkDemonMoveTicks;
        HorkDemonMoveTurn = horkDemonMoveTurn;
        RangedAvoidanceDelayTicks = rangedAvoidanceDelayTicks;
        SnakeDelayTicks = snakeDelayTicks;
        CounselorDelayTicks = counselorDelayTicks;
        AvoidanceDelayTicks = avoidanceDelayTicks;
        RangedDelayTicks = rangedDelayTicks;
        MegaDelayTicks = megaDelayTicks;
        MonsterItemSeed = monsterItemSeed;
        MonsterAiSeed = monsterAiSeed;
        hitPointsQ6 = DeterministicFixedPointQ6.FromWhole(hitPoints);
        ArmorClass = armorClass;
        maxHitPointsQ6 = DeterministicFixedPointQ6.FromWhole(
            Math.Max(hitPoints, maxHitPoints ?? hitPoints));
        LevelId = levelId;
        MonsterId = monsterId;
        AttackDamage = attackDamage;
        MinDamage = minDamage ?? attackDamage;
        MaxDamage = maxDamage ?? attackDamage;
        MinDamageSpecial = minDamageSpecial ?? MinDamage;
        MaxDamageSpecial = maxDamageSpecial ?? MaxDamage;
        AggroRange = aggroRange;
        FireResistance = fireResistance;
        LightningResistance = lightningResistance;
        MagicResistance = magicResistance;
        AcidResistance = acidResistance;
        MonsterLevel = monsterLevel;
        if (dropItemEntityId != 0 || dropItemSeed != 0) {
            if (dropItemEntityId == 0 || dropItemSeed == 0)
                throw new ArgumentException("Monster drops require both entity and item IDs.");
            Drop = new AuthoritativeWorldItem(
                dropItemEntityId,
                dropLevelId ?? levelId,
                dropPositionX ?? positionX,
                dropPositionY ?? positionY,
                dropItemSeed,
                dropItemPrice,
                dropItemState ?? AuthoritativeItemState.Empty with { ItemType = 1, Value = (int)dropItemPrice, IdentifiedValue = (int)dropItemPrice });
        }
    }

    public uint EntityId { get; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int FuturePositionX { get; private set; }
    public int FuturePositionY { get; private set; }

    /** Server tick at which the current one-tick walk was admitted. */
    public ulong MovementStartedTick { get; private set; }

    /** First server tick at which the current walk is complete. */
    public ulong MovementEffectTick { get; private set; }

    /** Native monster direction used by movement, attacks, and Hork Spawn. */
    public int Direction { get; private set; }

    /** Native InitMonster seed used for monster-owned item drops. */
    public uint MonsterItemSeed { get; set; }

    /** Native InitMonster seed used for multiplayer AI synchronization. */
    public uint MonsterAiSeed { get; set; }

    /** Explicit native initialization survives a legitimate zero-valued RNG seed. */
    public AuthoritativeNativeMonsterInitialization? NativeInitialization { get; private set; }

    public bool NativeInitializationPresent => NativeInitialization is not null;

    /** Current native attack/recovery animation cursor; null means no owned animation is active. */
    public AuthoritativeNativeMeleeCursor? NativeMeleeCursor { get; private set; }

    /** Native walk cursor retains both current and future occupancy until completion. */
    public AuthoritativeNativeWalkCursor? NativeWalkCursor { get; private set; }

    /** Older callers retain their existing nonzero AI-seed compatibility marker. */
    public bool UsesNativeRandomSeeds => NativeInitializationPresent || MonsterAiSeed != 0;

    internal void RestoreNativeInitialization(
        AuthoritativeNativeMonsterInitialization initialization,
        uint itemSeed,
        uint aiSeed)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        initialization.ValidateTarget(EntityId, MonsterId, LevelId, IsUnique, UniqueIdentity);
        NativeInitialization = initialization;
        MonsterItemSeed = itemSeed;
        MonsterAiSeed = aiSeed;
    }

    /** Restores or clears the separately versioned normal-melee cursor. */
    internal void RestoreNativeMeleeCursor(AuthoritativeNativeMeleeCursor? cursor)
    {
        if (cursor is null) {
            NativeMeleeCursor = null;
            return;
        }

        var initialization = NativeInitialization
            ?? throw new InvalidDataException("A native melee cursor requires native monster initialization.");
        var frameCount = cursor.HitRecovery
            ? initialization.NativeHitRecoveryFrameCount
            : initialization.NativeAttackFrameCount;
        var ticksPerFrame = cursor.HitRecovery
            ? initialization.NativeHitRecoveryTicksPerFrame
            : initialization.NativeAttackTicksPerFrame;
        var effectiveTicksPerFrame = Math.Max(1, ticksPerFrame);
        if (HitPointsQ6 <= 0
            || frameCount <= 0
            || cursor.FrameIndex < 0
            || cursor.FrameIndex >= frameCount
            || cursor.TickCounter < 0
            || cursor.TickCounter >= effectiveTicksPerFrame)
            throw new InvalidDataException("Native melee cursor is outside the initialized attack animation.");

        NativeMeleeCursor = cursor with { };
    }

    internal void RestoreNativeWalkCursor(AuthoritativeNativeWalkCursor? cursor)
    {
        if (cursor is null) {
            NativeWalkCursor = null;
            return;
        }
        var native = NativeInitialization
            ?? throw new InvalidDataException("A native walk cursor requires native initialization.");
        if (!IsMoving || HitPointsQ6 <= 0 || NativeMeleeCursor is not null
            || native.NativeWalkFrameCount <= 0 || cursor.FrameIndex < -1
            || cursor.FrameIndex >= native.NativeWalkFrameCount
            || cursor.TickCounter < 0 || cursor.TickCounter >= Math.Max(1, native.NativeWalkTicksPerFrame))
            throw new InvalidDataException("A native walk cursor is outside its reserved movement animation.");
        NativeWalkCursor = cursor with { };
    }

    /** Server-owned dialogue identity currently displayed by a unique encounter. */
    public int DialogueMessageId { get; set; }

    /** Server-owned authored dialogue state used by native compatibility presentation. */
    public AuthoritativeMonsterDialogueState DialogueState { get; set; }
    public bool IsMoving { get; private set; }
    public bool IsPetrified { get; set; }
    public bool StoneCurseImmune { get; set; }

    /** External monster capability used by Holy Bolt targeting. */
    public bool IsUndead { get; set; }

    /** External monster class capability used by demon-specific equipment. */
    public bool IsDemon { get; set; }

    /** External monster class capability used by sword/mace damage modifiers. */
    public bool IsAnimal { get; set; }

    /** Native monster ability that displaces a player after a successful hit. */
    public bool HasKnockback { get; set; }

    /** Native monster faction marker used by Apocalypse's minion exclusion. */
    public bool IsPlayerMinion { get; set; }

    /** Authoritative owner entity for a summoned player minion, when present. */
    public uint OwnerPlayerId { get; set; }

    /** Immutable native eligibility markers used by Berserk targeting. */
    public bool IsUnique { get; set; }

    /** Validated native unique-monster identity, distinct from the base monster type. */
    public string UniqueIdentity { get; set; }

    /** Authoritative leader identity for a non-unique monster pack member. */
    public uint LeaderEntityId { get; private set; }

    /** Native leashed/separated relationship to LeaderEntityId. */
    public AuthoritativeMonsterLeaderRelation LeaderRelation { get; private set; }

    /** Native count of currently leashed members carried by a pack leader. */
    public int PackSize { get; private set; }

    /** Native activation countdown propagated through leashed pack members. */
    public int ActiveForTicks { get; private set; }

    public bool IsDiablo { get; set; }

    public bool BerserkImmune { get; set; }

    /** Native physical damage channels; AttackDamage remains the legacy projection. */
    public int MinDamage { get; set; }

    public int MaxDamage { get; set; }

    public int MinDamageSpecial { get; set; }

    public int MaxDamageSpecial { get; set; }

    /** Native MFLAG_BERSERK state. */
    public bool IsBerserked { get; set; }

    /** Native death-animation duration imported from the active monster data. */
    public int DeathAnimationTicks { get; }

    /** Native experience reward imported from the active monster data. */
    public uint ExperienceReward { get; }

    /** Native normal-attack chance to hit imported from active monster data. */
    public int ToHit { get; }

    /** Native special-attack chance to hit imported from active monster data. */
    public int ToHitSpecial { get; }

    /** Native MonsterAIID dispatch identity imported from validated content. */
    public string NativeAi { get; }

    /** Native intelligence value imported from validated content. */
    public int Intelligence { get; }

    /** Native Gargoyle special lock held while it remains safely distant after healing. */
    public bool GargoyleSpecialActive { get; set; }

    /** Native Fallen fear countdown after a nearby Fallen dies. */
    public int FallenFearTicks { get; private set; }

    /** Direction of the native Fallen fear retreat. */
    public int FallenFearDirection { get; private set; }

    /** Current native Fallen standing-animation frame. */
    public int FallenStandAnimationFrame { get; private set; }

    /** Current native Fallen standing-animation tick. */
    public int FallenStandAnimationTick { get; private set; }

    /** Remaining native Fallen rally attack ticks. */
    public int FallenRallyTicks { get; private set; }

    /** Transient server marker for the current forced rally attack. */
    public bool FallenRallyAttackPending { get; private set; }

    /** Native Sneak visibility state; hidden monsters are not targetable. */
    public bool SneakHidden { get; private set; }

    /** Remaining native Sneak retreat ticks after a successful hit. */
    public int SneakRetreatTicks { get; private set; }

    /** Remaining native Scavenger corpse-eating ticks. */
    public int ScavengerHealingTicks { get; private set; }

    /** Selected corpse identity for the native Scavenger healing goal. */
    public uint ScavengerCorpseEntityId { get; private set; }

    /** Remaining native Bat retreat steps after an adjacent attack. */
    public int BatRetreatTicks { get; private set; }

    /** Direction toward the victim when the native Bat retreat started. */
    public int BatRetreatDirection { get; private set; }

    /** Native Bat var2-compatible movement counter used by movement admissions. */
    public int BatMovementIdleTicks { get; private set; }

    /** Native Bat var1-compatible movement value retained across movement. */
    public int BatMovementMode { get; private set; }

    /** Native AiRangedAvoidance goalVar1; -1 means the Normal goal is active. */
    public int RangedAvoidanceMoveTicks { get; private set; }

    /** Native AiRangedAvoidance goalVar2 turn state (0 or 1). */
    public int RangedAvoidanceTurn { get; private set; }

    /** Native AiRangedAvoidance var2-compatible movement counter. */
    public int RangedAvoidanceMovementIdleTicks { get; private set; }

    /** Native AiRangedAvoidance var1-compatible movement value. */
    public int RangedAvoidanceMovementMode { get; private set; }

    /** Native Rhino goalVar1 movement counter; -1 means the Normal goal. */
    public int RhinoMovementTicks { get; private set; }

    /** Native Rhino goalVar2 turn state (0 or 1). */
    public int RhinoMovementTurn { get; private set; }

    /** Native AiAvoidance goalVar1 movement counter; -1 means Normal. */
    public int AvoidanceMovementTicks { get; private set; }

    /** Native AiAvoidance goalVar2 turn state (0 or 1). */
    public int AvoidanceMovementTurn { get; private set; }

    /** Native AiAvoidance var2-compatible movement counter used by admissions. */
    public int AvoidanceMovementIdleTicks { get; private set; }

    /** Native AiAvoidance var1-compatible movement value retained across movement. */
    public int AvoidanceMovementMode { get; private set; }

    /** Remaining native SkeletonAi delay ticks before the next decision. */
    public int SkeletonMeleeDelayTicks { get; private set; }

    /** Native SkeletonBow var2-compatible movement counter. */
    public int SkeletonRangedMovementIdleTicks { get; private set; }

    /** Native SkeletonBow var1-compatible movement value. */
    public int SkeletonRangedMovementMode { get; private set; }

    /** Native OverlordAi var2-compatible movement counter. */
    public int FatMovementIdleTicks { get; private set; }

    /** Native OverlordAi var1-compatible movement value. */
    public int FatMovementMode { get; private set; }

    /** Native Rhino var2-compatible movement counter. */
    public int RhinoMovementIdleTicks { get; private set; }

    /** Native Rhino var1-compatible movement value. */
    public int RhinoMovementMode { get; private set; }

    /** Remaining native Rhino AiDelay ticks before the next decision. */
    public int RhinoDelayTicks { get; private set; }

    /** Remaining native LeoricAi delay ticks before the next decision. */
    public int SkeletonKingDelayTicks { get; private set; }

    /** Remaining native HorkDemonAi delay ticks before the next decision. */
    public int HorkDemonDelayTicks { get; private set; }

    /** Native HorkDemon goalVar1; -1 means the Normal goal is active. */
    public int HorkDemonMoveTicks { get; private set; }

    /** Native HorkDemon goalVar2 turn state (0 or 1). */
    public int HorkDemonMoveTurn { get; private set; }

    /** Remaining native AiRangedAvoidance delay ticks before the next decision. */
    public int RangedAvoidanceDelayTicks { get; private set; }

    /** Remaining native SnakeAi delay ticks before the next decision. */
    public int SnakeDelayTicks { get; private set; }

    /** Remaining native CounselorAi delay ticks before the next decision. */
    public int CounselorDelayTicks { get; private set; }

    /** Remaining native AiAvoidance delay ticks before the next decision. */
    public int AvoidanceDelayTicks { get; private set; }

    /** Remaining native AiRanged delay ticks after a ranged attack. */
    public int RangedDelayTicks { get; private set; }

    /** Remaining native MegaAi post-action delay ticks. */
    public int MegaDelayTicks { get; private set; }

    /** Remaining native Counselor retreat attempts after a wounded adjacent turn. */
    public int CounselorRetreatTicks { get; private set; }

    /** Remaining server ticks in the terminal death animation, when active. */
    public int DeathTicksRemaining { get; private set; }
    /** Whole-point compatibility projection; exact authoritative state is HitPointsQ6. */
    public int HitPoints
    {
        get => DeterministicFixedPointQ6.ToWholeCeiling(hitPointsQ6);
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            hitPointsQ6 = DeterministicFixedPointQ6.FromWhole(value);
        }
    }

    public int HitPointsQ6 => hitPointsQ6;
    public int ArmorClass { get; set; }
    /** Whole-point compatibility projection; exact authoritative state is MaxHitPointsQ6. */
    public int MaxHitPoints
    {
        get => DeterministicFixedPointQ6.ToWholeCeiling(maxHitPointsQ6);
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            maxHitPointsQ6 = DeterministicFixedPointQ6.FromWhole(value);
        }
    }

    public int MaxHitPointsQ6 => maxHitPointsQ6;
    public uint LevelId { get; set; }
    public uint MonsterId { get; set; }
    public int AttackDamage { get; set; }
    public int AggroRange { get; set; }
    public int FireResistance { get; set; }
    public int LightningResistance { get; set; }
    public int MagicResistance { get; set; }
    public int AcidResistance { get; set; }
    public int MonsterLevel { get; set; }
    public AuthoritativeWorldItem? Drop { get; }

    /** Captures the complete mutable monster projection without advancing timing. */
    internal DiabloMonsterState CaptureCohortState()
    {
        return new DiabloMonsterState(
            EntityId,
            MonsterId,
            LevelId,
            PositionX,
            PositionY,
            HitPoints,
            MaxHitPoints,
            ArmorClass,
            AttackDamage,
            AggroRange,
            FireResistance,
            LightningResistance,
            MagicResistance,
            AcidResistance) {
            MovementStartedTick = IsMoving ? MovementStartedTick : 0,
            MovementEffectTick = IsMoving ? MovementEffectTick : 0,
            MonsterLevel = MonsterLevel,
            Moving = IsMoving,
            FuturePositionX = FuturePositionX,
            FuturePositionY = FuturePositionY,
            Direction = Direction,
            MonsterItemSeed = MonsterItemSeed,
            MonsterAiSeed = MonsterAiSeed,
            NativeInitializationPresent = NativeInitializationPresent,
            NativeMeleeCursor = NativeMeleeCursor,
            NativeWalkCursor = NativeWalkCursor,
            DialogueMessageId = DialogueMessageId,
            DialogueState = DialogueState,
            Petrified = IsPetrified,
            StoneCurseImmune = StoneCurseImmune,
            IsUndead = IsUndead,
            IsDemon = IsDemon,
            IsAnimal = IsAnimal,
            HasKnockback = HasKnockback,
            IsPlayerMinion = IsPlayerMinion,
            OwnerPlayerId = OwnerPlayerId,
            IsUnique = IsUnique,
            UniqueIdentity = UniqueIdentity,
            LeaderEntityId = LeaderEntityId,
            LeaderRelation = LeaderRelation,
            PackSize = PackSize,
            ActiveForTicks = ActiveForTicks,
            IsDiablo = IsDiablo,
            BerserkImmune = BerserkImmune,
            MinDamage = MinDamage,
            MaxDamage = MaxDamage,
            MinDamageSpecial = MinDamageSpecial,
            MaxDamageSpecial = MaxDamageSpecial,
            IsBerserked = IsBerserked,
            DeathAnimationTicks = DeathAnimationTicks,
            ExperienceReward = ExperienceReward,
            ToHit = ToHit,
            ToHitSpecial = ToHitSpecial,
            NativeAi = NativeAi,
            Intelligence = Intelligence,
            GargoyleSpecialActive = GargoyleSpecialActive,
            DeathTicksRemaining = DeathTicksRemaining,
            FallenFearTicks = FallenFearTicks,
            FallenFearDirection = FallenFearDirection,
            FallenStandAnimationFrame = FallenStandAnimationFrame,
            FallenStandAnimationTick = FallenStandAnimationTick,
            FallenRallyTicks = FallenRallyTicks,
            SneakHidden = SneakHidden,
            SneakRetreatTicks = SneakRetreatTicks,
            ScavengerHealingTicks = ScavengerHealingTicks,
            ScavengerCorpseEntityId = ScavengerCorpseEntityId,
            BatRetreatTicks = BatRetreatTicks,
            BatRetreatDirection = BatRetreatDirection,
            BatMovementIdleTicks = BatMovementIdleTicks,
            BatMovementMode = BatMovementMode,
            RangedAvoidanceMoveTicks = RangedAvoidanceMoveTicks,
            RangedAvoidanceTurn = RangedAvoidanceTurn,
            RangedAvoidanceMovementIdleTicks = RangedAvoidanceMovementIdleTicks,
            RangedAvoidanceMovementMode = RangedAvoidanceMovementMode,
            RhinoMovementTicks = RhinoMovementTicks,
            RhinoMovementTurn = RhinoMovementTurn,
            AvoidanceMovementTicks = AvoidanceMovementTicks,
            AvoidanceMovementTurn = AvoidanceMovementTurn,
            AvoidanceMovementIdleTicks = AvoidanceMovementIdleTicks,
            AvoidanceMovementMode = AvoidanceMovementMode,
            SkeletonMeleeDelayTicks = SkeletonMeleeDelayTicks,
            SkeletonRangedMovementIdleTicks = SkeletonRangedMovementIdleTicks,
            SkeletonRangedMovementMode = SkeletonRangedMovementMode,
            FatMovementIdleTicks = FatMovementIdleTicks,
            FatMovementMode = FatMovementMode,
            RhinoMovementIdleTicks = RhinoMovementIdleTicks,
            RhinoMovementMode = RhinoMovementMode,
            RhinoDelayTicks = RhinoDelayTicks,
            SkeletonKingDelayTicks = SkeletonKingDelayTicks,
            HorkDemonDelayTicks = HorkDemonDelayTicks,
            HorkDemonMoveTicks = HorkDemonMoveTicks,
            HorkDemonMoveTurn = HorkDemonMoveTurn,
            RangedAvoidanceDelayTicks = RangedAvoidanceDelayTicks,
            SnakeDelayTicks = SnakeDelayTicks,
            CounselorDelayTicks = CounselorDelayTicks,
            AvoidanceDelayTicks = AvoidanceDelayTicks,
            RangedDelayTicks = RangedDelayTicks,
            MegaDelayTicks = MegaDelayTicks,
            CounselorRetreatTicks = CounselorRetreatTicks,
            HitPointsQ6 = HitPointsQ6,
            MaxHitPointsQ6 = MaxHitPointsQ6,
        };
    }

    /** Restores the one same-tick marker omitted by the legacy snapshot. */
    internal void RestoreCohortFallenRallyAttackPending(bool pending)
    {
        FallenRallyAttackPending = pending;
    }

    internal int ApplyDamageQ6(int damageQ6)
    {
        if (damageQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(damageQ6));
        var appliedDamageQ6 = Math.Min(hitPointsQ6, damageQ6);
        hitPointsQ6 -= appliedDamageQ6;
        return appliedDamageQ6;
    }

    internal int ApplyHealingQ6(int healingQ6)
    {
        if (healingQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(healingQ6));
        var appliedHealingQ6 = Math.Min(maxHitPointsQ6 - hitPointsQ6, healingQ6);
        hitPointsQ6 += appliedHealingQ6;
        return appliedHealingQ6;
    }

    internal void BeginDeathAnimation()
    {
        if (hitPointsQ6 != 0)
            throw new InvalidOperationException("A living monster cannot begin its death animation.");
        DeathTicksRemaining = DeathAnimationTicks;
    }

    /** Applies a content-authored encounter defeat without combat rewards. */
    internal void DefeatForDialogue()
    {
        if (hitPointsQ6 == 0)
            return;
        hitPointsQ6 = 0;
        CancelMovement();
        BeginDeathAnimation();
    }

    internal bool AdvanceDeathAnimation()
    {
        if (DeathTicksRemaining == 0)
            return true;
        DeathTicksRemaining--;
        return DeathTicksRemaining == 0;
    }

    internal void RestoreDeathAnimation(int deathTicksRemaining)
    {
        if (deathTicksRemaining < 0 || deathTicksRemaining > DeathAnimationTicks)
            throw new ArgumentOutOfRangeException(nameof(deathTicksRemaining));
        DeathTicksRemaining = deathTicksRemaining;
    }

    internal void StartFallenFear(int ticks, int direction)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
            || direction is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        FallenFearTicks = ticks;
        FallenFearDirection = direction;
    }

    internal void RestoreFallenFear(int ticks, int direction)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
            || direction is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        FallenFearTicks = ticks;
        FallenFearDirection = direction;
    }

    internal bool AdvanceFallenFear()
    {
        if (FallenFearTicks == 0)
            return false;
        FallenFearTicks--;
        return true;
    }

    internal bool AdvanceFallenStandAnimation(int frameCount, int ticksPerFrame)
    {
        if (frameCount <= 0 || ticksPerFrame <= 0)
            return false;
        if (FallenStandAnimationTick + 1 < ticksPerFrame) {
            FallenStandAnimationTick++;
            return false;
        }
        FallenStandAnimationTick = 0;
        if (FallenStandAnimationFrame + 1 < frameCount) {
            FallenStandAnimationFrame++;
            return false;
        }
        return true;
    }

    internal void ResetFallenStandAnimation()
    {
        FallenStandAnimationFrame = 0;
        FallenStandAnimationTick = 0;
    }

    internal void StartFallenRally(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenRallyTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        FallenRallyTicks = ticks;
    }

    internal bool AdvanceFallenRally(bool forceAttack)
    {
        FallenRallyAttackPending = forceAttack && FallenRallyTicks > 0;
        if (FallenRallyTicks == 0)
            return false;
        FallenRallyTicks--;
        return true;
    }

    internal void RestoreFallenRally(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenRallyTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        FallenRallyTicks = ticks;
        FallenRallyAttackPending = false;
    }

    internal void RestoreFallenAnimation(int frame, int tick)
    {
        if (frame is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationFrame
            || tick is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterFallenStandAnimationTick)
            throw new ArgumentOutOfRangeException(nameof(frame));
        FallenStandAnimationFrame = frame;
        FallenStandAnimationTick = tick;
    }

    internal void ClearFallenRallyAttack()
    {
        FallenRallyAttackPending = false;
    }

    internal void SetSneakHidden(bool hidden)
    {
        SneakHidden = hidden;
    }

    internal void StartSneakRetreat(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        SneakHidden = false;
        SneakRetreatTicks = ticks;
    }

    internal void RestoreSneakState(bool hidden, int retreatTicks)
    {
        if (retreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(retreatTicks));
        SneakHidden = hidden;
        SneakRetreatTicks = retreatTicks;
    }

    internal bool AdvanceSneakRetreat()
    {
        if (SneakRetreatTicks == 0)
            return false;
        SneakRetreatTicks--;
        return true;
    }

    internal void StartScavengerHealing(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        ScavengerHealingTicks = ticks;
        ScavengerCorpseEntityId = 0;
    }

    internal void SelectScavengerCorpse(uint corpseEntityId)
    {
        ScavengerCorpseEntityId = corpseEntityId;
    }

    internal bool AdvanceScavengerHealing()
    {
        if (ScavengerHealingTicks == 0)
            return false;
        ScavengerHealingTicks--;
        return true;
    }

    internal void ClearScavengerHealing()
    {
        ScavengerHealingTicks = 0;
        ScavengerCorpseEntityId = 0;
    }

    internal void RestoreScavengerHealing(int ticks, uint corpseEntityId)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        ScavengerHealingTicks = ticks;
        ScavengerCorpseEntityId = corpseEntityId;
    }

    internal void StartBatRetreat(int direction, int retreatTicks)
    {
        if (direction is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (retreatTicks is < 1 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks)
            throw new ArgumentOutOfRangeException(nameof(retreatTicks));
        BatRetreatTicks = retreatTicks;
        BatRetreatDirection = direction;
    }

    internal void RestoreBatRetreat(int ticks, int direction)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks
            || direction is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        BatRetreatTicks = ticks;
        BatRetreatDirection = direction;
    }

    internal bool AdvanceBatRetreat()
    {
        if (BatRetreatTicks == 0)
            return false;
        BatRetreatTicks--;
        if (BatRetreatTicks == 0)
            BatRetreatDirection = 0;
        return true;
    }

    internal void RecordBatMovement(int displacementX, int displacementY)
    {
        if (displacementX is < -1 or > 1
            || displacementY is < -1 or > 1
            || (displacementX == 0 && displacementY == 0)) {
            throw new ArgumentOutOfRangeException(nameof(displacementX));
        }
        BatMovementMode = displacementX;
        BatMovementIdleTicks = displacementY;
    }

    internal void AdvanceBatMovementIdle()
    {
        if (BatMovementIdleTicks < NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            BatMovementIdleTicks++;
    }

    internal void RestoreBatMovementState(int idleTicks, int movementMode)
    {
        if (idleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(idleTicks));
        if (movementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(movementMode));
        BatMovementIdleTicks = idleTicks;
        BatMovementMode = movementMode;
    }

    internal void StartRangedAvoidanceMovement(int turn)
    {
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        RangedAvoidanceMoveTicks = 0;
        RangedAvoidanceTurn = turn;
    }

    internal bool AdvanceRangedAvoidanceMovement(int distance, bool directionAvailable)
    {
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (RangedAvoidanceMoveTicks < 0)
            return false;

        var completes = RangedAvoidanceMoveTicks >= checked(2 * distance)
            && directionAvailable;
        if (RangedAvoidanceMoveTicks < NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            RangedAvoidanceMoveTicks++;
        if (completes) {
            RangedAvoidanceMoveTicks = -1;
            RangedAvoidanceTurn = 0;
        }
        return !completes;
    }

    internal void RestoreRangedAvoidanceMovement(int moveTicks, int turn)
    {
        if (moveTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(moveTicks));
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        RangedAvoidanceMoveTicks = moveTicks;
        RangedAvoidanceTurn = turn;
    }

    internal void ClearRangedAvoidanceMovement()
    {
        RangedAvoidanceMoveTicks = -1;
        RangedAvoidanceTurn = 0;
    }

    internal void UpdateRangedAvoidanceTurn(int turn)
    {
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        RangedAvoidanceTurn = turn;
    }

    internal void RecordRangedAvoidanceMovement(int displacementX, int displacementY)
    {
        if (displacementX is < -1 or > 1
            || displacementY is < -1 or > 1
            || (displacementX == 0 && displacementY == 0)) {
            throw new ArgumentOutOfRangeException(nameof(displacementX));
        }
        RangedAvoidanceMovementMode = displacementX;
        RangedAvoidanceMovementIdleTicks = displacementY;
    }

    internal void AdvanceRangedAvoidanceMovementIdle()
    {
        if (RangedAvoidanceMovementIdleTicks < NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            RangedAvoidanceMovementIdleTicks++;
    }

    internal void RestoreRangedAvoidanceMovementState(int idleTicks, int movementMode)
    {
        if (idleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(idleTicks));
        if (movementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(movementMode));
        RangedAvoidanceMovementIdleTicks = idleTicks;
        RangedAvoidanceMovementMode = movementMode;
    }

    internal void StartRhinoMovement(int turn)
    {
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        RhinoMovementTicks = 0;
        RhinoMovementTurn = turn;
    }

    internal bool AdvanceRhinoMovement(int distance, bool sameTransparencyRegion)
    {
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (RhinoMovementTicks < 0)
            return false;

        var completes = RhinoMovementTicks >= checked(2 * distance)
            || !sameTransparencyRegion;
        if (RhinoMovementTicks < NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            RhinoMovementTicks++;
        if (completes) {
            RhinoMovementTicks = -1;
            RhinoMovementTurn = 0;
        }
        return !completes;
    }

    internal void RestoreRhinoMovement(int movementTicks, int turn)
    {
        if (movementTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(movementTicks));
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        RhinoMovementTicks = movementTicks;
        RhinoMovementTurn = turn;
    }

    internal void ClearRhinoMovement()
    {
        RhinoMovementTicks = -1;
        RhinoMovementTurn = 0;
    }

    internal void UpdateRhinoMovementTurn(int turn)
    {
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        RhinoMovementTurn = turn;
    }

    internal void StartAvoidanceMovement(int turn)
    {
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        AvoidanceMovementTicks = 0;
        AvoidanceMovementTurn = turn;
    }

    internal bool AdvanceAvoidanceMovement(
        int distance,
        bool sameTransparencyRegion,
        bool directionAvailable)
    {
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (AvoidanceMovementTicks < 0)
            return false;

        var completes = (AvoidanceMovementTicks >= checked(2 * distance)
                && directionAvailable)
            || !sameTransparencyRegion;
        if (AvoidanceMovementTicks < NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            AvoidanceMovementTicks++;
        if (completes) {
            AvoidanceMovementTicks = -1;
            AvoidanceMovementTurn = 0;
        }
        return !completes;
    }

    internal void RestoreAvoidanceMovement(int movementTicks, int turn)
    {
        if (movementTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(movementTicks));
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        AvoidanceMovementTicks = movementTicks;
        AvoidanceMovementTurn = turn;
    }

    internal void ClearAvoidanceMovement()
    {
        AvoidanceMovementTicks = -1;
        AvoidanceMovementTurn = 0;
    }

    internal void UpdateAvoidanceMovementTurn(int turn)
    {
        if (turn is < NativeCompatibilityLimits.MinimumNativeMonsterMovementTurn or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTurn)
            throw new ArgumentOutOfRangeException(nameof(turn));
        AvoidanceMovementTurn = turn;
    }

    internal void RecordAvoidanceMovement(int displacementX, int displacementY)
    {
        if (displacementX is < -1 or > 1
            || displacementY is < -1 or > 1
            || (displacementX == 0 && displacementY == 0)) {
            throw new ArgumentOutOfRangeException(nameof(displacementX));
        }
        AvoidanceMovementMode = displacementX;
        AvoidanceMovementIdleTicks = displacementY;
    }

    internal void AdvanceAvoidanceMovementIdle()
    {
        if (AvoidanceMovementIdleTicks < NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            AvoidanceMovementIdleTicks++;
    }

    internal void RestoreAvoidanceMovementState(int idleTicks, int movementMode)
    {
        if (idleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(idleTicks));
        if (movementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(movementMode));
        AvoidanceMovementIdleTicks = idleTicks;
        AvoidanceMovementMode = movementMode;
    }

    internal void StartSkeletonMeleeDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        SkeletonMeleeDelayTicks = ticks;
    }

    internal bool AdvanceSkeletonMeleeDelay()
    {
        if (SkeletonMeleeDelayTicks == 0)
            return false;
        SkeletonMeleeDelayTicks--;
        return true;
    }

    internal void RestoreSkeletonMeleeDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        SkeletonMeleeDelayTicks = ticks;
    }

    internal void RecordSkeletonRangedMovement(int displacementX, int displacementY)
    {
        if (displacementX is < -1 or > 1
            || displacementY is < -1 or > 1
            || (displacementX == 0 && displacementY == 0)) {
            throw new ArgumentOutOfRangeException(nameof(displacementX));
        }
        SkeletonRangedMovementMode = displacementX;
        SkeletonRangedMovementIdleTicks = displacementY;
    }

    internal void AdvanceSkeletonRangedMovementIdle()
    {
        if (SkeletonRangedMovementIdleTicks < NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            SkeletonRangedMovementIdleTicks++;
    }

    internal void RestoreSkeletonRangedMovementState(int idleTicks, int movementMode)
    {
        if (idleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(idleTicks));
        if (movementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(movementMode));
        SkeletonRangedMovementIdleTicks = idleTicks;
        SkeletonRangedMovementMode = movementMode;
    }

    internal void RecordFatMovement(int displacementX, int displacementY)
    {
        if (displacementX is < -1 or > 1
            || displacementY is < -1 or > 1
            || (displacementX == 0 && displacementY == 0)) {
            throw new ArgumentOutOfRangeException(nameof(displacementX));
        }
        FatMovementMode = displacementX;
        FatMovementIdleTicks = displacementY;
    }

    internal void AdvanceFatMovementIdle()
    {
        if (FatMovementIdleTicks < NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            FatMovementIdleTicks++;
    }

    internal void RestoreFatMovementState(int idleTicks, int movementMode)
    {
        if (idleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(idleTicks));
        if (movementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(movementMode));
        FatMovementIdleTicks = idleTicks;
        FatMovementMode = movementMode;
    }

    internal void RecordRhinoMovement(int displacementX, int displacementY)
    {
        if (displacementX is < -1 or > 1
            || displacementY is < -1 or > 1
            || (displacementX == 0 && displacementY == 0)) {
            throw new ArgumentOutOfRangeException(nameof(displacementX));
        }
        RhinoMovementMode = displacementX;
        RhinoMovementIdleTicks = displacementY;
    }

    internal void AdvanceRhinoMovementIdle()
    {
        if (RhinoMovementIdleTicks < NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            RhinoMovementIdleTicks++;
    }

    internal void RestoreRhinoMovementState(int idleTicks, int movementMode)
    {
        if (idleTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(idleTicks));
        if (movementMode is < NativeCompatibilityLimits.MinimumNativeMonsterMovementMode or > NativeCompatibilityLimits.MaximumNativeMonsterMovementMode)
            throw new ArgumentOutOfRangeException(nameof(movementMode));
        RhinoMovementIdleTicks = idleTicks;
        RhinoMovementMode = movementMode;
    }

    internal void StartRhinoDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        RhinoDelayTicks = ticks;
    }

    internal bool AdvanceRhinoDelay()
    {
        if (RhinoDelayTicks == 0)
            return false;
        RhinoDelayTicks--;
        return true;
    }

    internal void RestoreRhinoDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        RhinoDelayTicks = ticks;
    }

    internal void StartSkeletonKingDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        SkeletonKingDelayTicks = ticks;
    }

    internal bool AdvanceSkeletonKingDelay()
    {
        if (SkeletonKingDelayTicks == 0)
            return false;
        SkeletonKingDelayTicks--;
        return true;
    }

    internal void RestoreSkeletonKingDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        SkeletonKingDelayTicks = ticks;
    }

    internal void StartHorkDemonDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        HorkDemonDelayTicks = ticks;
    }

    internal bool AdvanceHorkDemonDelay()
    {
        if (HorkDemonDelayTicks == 0)
            return false;
        HorkDemonDelayTicks--;
        return true;
    }

    internal void RestoreHorkDemonDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        HorkDemonDelayTicks = ticks;
    }

    internal void StartHorkDemonMovement(int turn)
    {
        if (turn is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(turn));
        HorkDemonMoveTicks = 0;
        HorkDemonMoveTurn = turn;
    }

    /** Advances native HorkDemon goalVar1 with its post-increment boundary. */
    internal bool AdvanceHorkDemonMovement(
        int distance,
        bool sameTransparencyRegion = true)
    {
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (HorkDemonMoveTicks < 0)
            return false;

        var continues = HorkDemonMoveTicks < checked(2 * distance)
            && sameTransparencyRegion;
        if (HorkDemonMoveTicks < NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            HorkDemonMoveTicks++;
        if (!continues)
            ClearHorkDemonMovement();
        return continues;
    }

    internal void RestoreHorkDemonMovement(int moveTicks, int turn)
    {
        if (moveTicks is < -1 or > NativeCompatibilityLimits.MaximumNativeMonsterMovementTicks)
            throw new ArgumentOutOfRangeException(nameof(moveTicks));
        if (turn is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(turn));
        HorkDemonMoveTicks = moveTicks;
        HorkDemonMoveTurn = turn;
    }

    internal void UpdateHorkDemonMovementTurn(int turn)
    {
        if (turn is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(turn));
        HorkDemonMoveTurn = turn;
    }

    internal void ClearHorkDemonMovement()
    {
        HorkDemonMoveTicks = -1;
        HorkDemonMoveTurn = 0;
    }

    internal void StartRangedAvoidanceDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        RangedAvoidanceDelayTicks = ticks;
    }

    internal bool AdvanceRangedAvoidanceDelay()
    {
        if (RangedAvoidanceDelayTicks == 0)
            return false;
        RangedAvoidanceDelayTicks--;
        return true;
    }

    internal void RestoreRangedAvoidanceDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        RangedAvoidanceDelayTicks = ticks;
    }

    internal void StartSnakeDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        SnakeDelayTicks = ticks;
    }

    internal bool AdvanceSnakeDelay()
    {
        if (SnakeDelayTicks == 0)
            return false;
        SnakeDelayTicks--;
        return true;
    }

    internal void RestoreSnakeDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        SnakeDelayTicks = ticks;
    }

    internal void StartCounselorDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        CounselorDelayTicks = ticks;
    }

    internal bool AdvanceCounselorDelay()
    {
        if (CounselorDelayTicks == 0)
            return false;
        CounselorDelayTicks--;
        return true;
    }

    internal void RestoreCounselorDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        CounselorDelayTicks = ticks;
    }

    internal void StartAvoidanceDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        AvoidanceDelayTicks = ticks;
    }

    internal bool AdvanceAvoidanceDelay()
    {
        if (AvoidanceDelayTicks == 0)
            return false;
        AvoidanceDelayTicks--;
        return true;
    }

    internal void RestoreAvoidanceDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        AvoidanceDelayTicks = ticks;
    }

    internal void StartRangedDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        RangedDelayTicks = ticks;
    }

    internal bool AdvanceRangedDelay()
    {
        if (RangedDelayTicks == 0)
            return false;
        RangedDelayTicks--;
        return true;
    }

    internal void RestoreRangedDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        RangedDelayTicks = ticks;
    }

    internal void StartMegaDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        MegaDelayTicks = ticks;
    }

    internal bool AdvanceMegaDelay()
    {
        if (MegaDelayTicks == 0)
            return false;
        MegaDelayTicks--;
        return true;
    }

    internal void RestoreMegaDelay(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        MegaDelayTicks = ticks;
    }

    internal void StartCounselorRetreat(int ticks)
    {
        if (ticks is < 1 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        CounselorRetreatTicks = ticks;
    }

    internal void RestoreCounselorRetreat(int ticks)
    {
        if (ticks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        CounselorRetreatTicks = ticks;
    }

    internal bool AdvanceCounselorRetreat()
    {
        if (CounselorRetreatTicks == 0)
            return false;
        CounselorRetreatTicks--;
        return true;
    }

    internal void BeginMovement(int futurePositionX, int futurePositionY)
    {
        if (DeterministicGridMetrics.ChebyshevDistance(
                PositionX,
                PositionY,
                futurePositionX,
                futurePositionY) != 1) {
            throw new ArgumentException(
                "A moving monster must have an adjacent future position.");
        }
        FuturePositionX = futurePositionX;
        FuturePositionY = futurePositionY;
        Direction = (int)DeterministicGridDirections.FromAdjacentStep(
            futurePositionX - PositionX,
            futurePositionY - PositionY);
        IsMoving = true;
    }

    /** Stamps legacy or newly admitted movement when it enters a snapshot. */
    internal void EnsureMovementTiming(ulong snapshotTick)
    {
        if (!IsMoving || MovementStartedTick != 0 || MovementEffectTick != 0)
            return;
        MovementStartedTick = snapshotTick == ulong.MaxValue
            ? ulong.MaxValue - 1
            : snapshotTick;
        MovementEffectTick = snapshotTick == ulong.MaxValue
            ? ulong.MaxValue
            : snapshotTick + 1;
    }

    internal void RestoreMovementTiming(ulong startedTick, ulong effectTick)
    {
        if (!IsMoving) {
            if (startedTick != 0 || effectTick != 0)
                throw new ArgumentException(
                    "Stationary monsters cannot carry movement timing.");
            MovementStartedTick = 0;
            MovementEffectTick = 0;
            return;
        }
        if (startedTick != 0 && effectTick <= startedTick) {
            throw new ArgumentException(
                "Moving monsters must carry a valid movement timing window.");
        }
        MovementStartedTick = startedTick;
        MovementEffectTick = effectTick;
    }

    internal void SetLeaderRelation(AuthoritativeMonsterLeaderRelation relation)
    {
        if (relation == AuthoritativeMonsterLeaderRelation.None)
            throw new ArgumentOutOfRangeException(nameof(relation));
        LeaderRelation = relation;
    }

    internal void BreakLeaderRelation()
    {
        LeaderEntityId = 0;
        LeaderRelation = AuthoritativeMonsterLeaderRelation.None;
    }

    internal void IncrementPackSize()
    {
        PackSize = Math.Min(NativeCompatibilityLimits.MaximumNativeMonsterPackSize, PackSize + 1);
    }

    internal void DecrementPackSize()
    {
        PackSize = Math.Max(0, PackSize - 1);
    }

    internal void RestoreGroupUnity(
        uint leaderEntityId,
        AuthoritativeMonsterLeaderRelation leaderRelation,
        int packSize)
    {
        if (!Enum.IsDefined(leaderRelation)
            || packSize is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterPackSize
            || (leaderRelation == AuthoritativeMonsterLeaderRelation.None
                ? leaderEntityId != 0
                : leaderEntityId == 0)
            || (leaderRelation != AuthoritativeMonsterLeaderRelation.None && IsUnique)) {
            throw new ArgumentException("Invalid monster pack relationship state.");
        }
        LeaderEntityId = leaderEntityId;
        LeaderRelation = leaderRelation;
        PackSize = packSize;
    }

    internal void RestoreActiveForTicks(int activeForTicks)
    {
        if (activeForTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterActiveTicks)
            throw new ArgumentOutOfRangeException(nameof(activeForTicks));
        ActiveForTicks = activeForTicks;
    }

    internal void RestoreDialogueState(AuthoritativeMonsterDialogueState dialogueState)
    {
        if (!Enum.IsDefined(dialogueState))
            throw new ArgumentOutOfRangeException(nameof(dialogueState));
        if (dialogueState != AuthoritativeMonsterDialogueState.Unspecified && !IsUnique)
            throw new ArgumentException(
                "An authored dialogue state requires a unique encounter.",
                nameof(dialogueState));
        DialogueState = dialogueState;
    }

    internal void RestoreDirection(int direction)
    {
        if (direction is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDirection)
            throw new ArgumentOutOfRangeException(nameof(direction));
        Direction = direction;
    }

    internal void CompleteMovement()
    {
        if (!IsMoving)
            return;
        PositionX = FuturePositionX;
        PositionY = FuturePositionY;
        IsMoving = false;
        NativeWalkCursor = null;
        MovementStartedTick = 0;
        MovementEffectTick = 0;
    }

    internal void CancelMovement()
    {
        FuturePositionX = PositionX;
        FuturePositionY = PositionY;
        IsMoving = false;
        NativeWalkCursor = null;
        MovementStartedTick = 0;
        MovementEffectTick = 0;
    }

    internal void ApplyKnockback(int positionX, int positionY)
    {
        PositionX = positionX;
        PositionY = positionY;
        FuturePositionX = positionX;
        FuturePositionY = positionY;
        IsMoving = false;
        NativeWalkCursor = null;
        MovementStartedTick = 0;
        MovementEffectTick = 0;
    }

    internal void RestoreOccupancy(
        bool moving,
        int futurePositionX,
        int futurePositionY,
        bool petrified)
    {
        if (moving)
            BeginMovement(futurePositionX, futurePositionY);
        else
            CancelMovement();
        IsPetrified = petrified;
    }

    internal void RestoreHitPointsQ6(int restoredHitPointsQ6, int restoredMaxHitPointsQ6)
    {
        if (restoredHitPointsQ6 < 0
            || restoredMaxHitPointsQ6 < restoredHitPointsQ6) {
            throw new ArgumentOutOfRangeException(nameof(restoredHitPointsQ6));
        }
        hitPointsQ6 = restoredHitPointsQ6;
        maxHitPointsQ6 = restoredMaxHitPointsQ6;
    }
}
