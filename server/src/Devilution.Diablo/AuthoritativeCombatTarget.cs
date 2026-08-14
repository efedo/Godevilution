
namespace Devilution.Diablo;

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
        int fallenFearDirection = 0)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (hitPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(hitPoints));
        if (attackDamage < 0 || aggroRange < 0)
            throw new ArgumentOutOfRangeException(nameof(attackDamage));
        if (minDamage is < 0 || maxDamage is < 0 || minDamageSpecial is < 0 || maxDamageSpecial is < 0)
            throw new ArgumentOutOfRangeException(nameof(minDamage));
        if (monsterLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterLevel));
        if (deathAnimationTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(deathAnimationTicks));
        if (experienceReward > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(experienceReward));
        if (toHit is < 0 or > 255 || toHitSpecial is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(toHit));
        if (intelligence is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(intelligence));
        if (fallenFearTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(fallenFearTicks));
        if (fallenFearDirection is < 0 or >= 8)
            throw new ArgumentOutOfRangeException(nameof(fallenFearDirection));
        if (!string.IsNullOrEmpty(nativeAi)
            && !AuthoritativeMonsterTypeCatalog.IsValidNativeAi(nativeAi))
            throw new ArgumentException("The monster native AI identity is invalid.", nameof(nativeAi));
        if (gargoyleSpecialActive && nativeAi != "Gargoyle")
            throw new ArgumentException(
                "The Gargoyle special lock requires the Gargoyle native AI identity.",
                nameof(gargoyleSpecialActive));
        if (fallenFearTicks != 0 && nativeAi != "Fallen")
            throw new ArgumentException(
                "Fallen fear requires the Fallen native AI identity.",
                nameof(fallenFearTicks));
        if (fireResistance is < -100 or > 100
            || lightningResistance is < -100 or > 100
            || magicResistance is < -100 or > 100
            || acidResistance is < -100 or > 100)
            throw new ArgumentOutOfRangeException(nameof(fireResistance));
        var resolvedFuturePositionX = futurePositionX ?? positionX;
        var resolvedFuturePositionY = futurePositionY ?? positionY;
        if (moving) {
            if (Math.Max(
                    Math.Abs(resolvedFuturePositionX - positionX),
                    Math.Abs(resolvedFuturePositionY - positionY)) != 1) {
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
        hitPointsQ6 = DiabloFixedPointQ6.FromWhole(hitPoints);
        ArmorClass = armorClass;
        maxHitPointsQ6 = DiabloFixedPointQ6.FromWhole(
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
                levelId,
                positionX,
                positionY,
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

    /** Remaining server ticks in the terminal death animation, when active. */
    public int DeathTicksRemaining { get; private set; }
    /** Whole-point compatibility projection; exact authoritative state is HitPointsQ6. */
    public int HitPoints
    {
        get => DiabloFixedPointQ6.ToWholeCeiling(hitPointsQ6);
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            hitPointsQ6 = DiabloFixedPointQ6.FromWhole(value);
        }
    }

    public int HitPointsQ6 => hitPointsQ6;
    public int ArmorClass { get; set; }
    /** Whole-point compatibility projection; exact authoritative state is MaxHitPointsQ6. */
    public int MaxHitPoints
    {
        get => DiabloFixedPointQ6.ToWholeCeiling(maxHitPointsQ6);
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            maxHitPointsQ6 = DiabloFixedPointQ6.FromWhole(value);
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
        if (NativeAi != "Fallen")
            throw new InvalidOperationException("Only Fallen monsters can enter fear.");
        if (ticks < 0 || direction is < 0 or >= 8)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        FallenFearTicks = ticks;
        FallenFearDirection = direction;
    }

    internal void RestoreFallenFear(int ticks, int direction)
    {
        if (ticks < 0 || direction is < 0 or >= 8)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        if (ticks != 0 && NativeAi != "Fallen")
            throw new InvalidDataException("Only Fallen monsters can restore fear.");
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

    internal void BeginMovement(int futurePositionX, int futurePositionY)
    {
        if (Math.Max(
                Math.Abs(futurePositionX - PositionX),
                Math.Abs(futurePositionY - PositionY)) != 1) {
            throw new ArgumentException(
                "A moving monster must have an adjacent future position.");
        }
        FuturePositionX = futurePositionX;
        FuturePositionY = futurePositionY;
        IsMoving = true;
    }

    internal void CompleteMovement()
    {
        if (!IsMoving)
            return;
        PositionX = FuturePositionX;
        PositionY = FuturePositionY;
        IsMoving = false;
    }

    internal void CancelMovement()
    {
        FuturePositionX = PositionX;
        FuturePositionY = PositionY;
        IsMoving = false;
    }

    internal void ApplyKnockback(int positionX, int positionY)
    {
        PositionX = positionX;
        PositionY = positionY;
        FuturePositionX = positionX;
        FuturePositionY = positionY;
        IsMoving = false;
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
