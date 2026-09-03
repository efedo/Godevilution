using Devilution.Data;

namespace Devilution.Diablo;

/** Mutable server-owned object state used by interaction commands. */
public sealed class AuthoritativeWorldObject
{
    private uint nativeObjectId;

    public AuthoritativeWorldObject(
        uint entityId,
        uint objectId,
        uint levelId,
        int positionX,
        int positionY,
        bool activated = false,
        uint questId = 0,
        AuthoritativeObjectEffectKind effectKind = AuthoritativeObjectEffectKind.None,
        int effectAmount = 0,
        uint questProgressAmount = 1,
        bool missilePassable = false,
        bool breakable = false,
        bool destroyed = false,
        bool trapArmed = false,
        bool isShrine = false,
        AuthoritativeObjectQuestTransitionKind questTransitionKind = AuthoritativeObjectQuestTransitionKind.None,
        int questTransitionAmount = 0,
        int questTransitionRequiredVariable1 = -1,
        int questTransitionRequiredVariable2 = -1,
        AuthoritativeObjectRewardKind rewardKind = AuthoritativeObjectRewardKind.None,
        uint rewardId = 0,
        int rewardItemLevel = 0,
        int rewardOffsetX = 0,
        int rewardOffsetY = 0,
        int projectileTargetOffsetX = 0,
        int projectileTargetOffsetY = 0,
        AuthoritativeQuestSequenceFollowUpKind questSequenceFollowUpKind = AuthoritativeQuestSequenceFollowUpKind.None,
        uint questSequenceFollowUpAmount = 0,
        int questSequenceFollowUpOffsetX = 0,
        int questSequenceFollowUpOffsetY = 0,
        AuthoritativeObjectProjectileSourceKind projectileSourceKind
            = AuthoritativeObjectProjectileSourceKind.Object,
        uint questSequenceFollowUpWorldMutationId = 0,
        bool activateOnOccupancy = false,
        int requiredActivatedLinkedObjects = 0,
        uint nativeObjectId = 0,
        bool hideWhenActivated = true,
        int animationFrame = 0,
        int activatedAnimationFrame = 0,
        AuthoritativeObjectRewardOriginKind rewardOriginKind
            = AuthoritativeObjectRewardOriginKind.Object,
        int rewardSetLevelId = -1,
        int dialogueMessageId = 0)
    {
        if (entityId == 0 || objectId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (nativeObjectId > AuthoritativeLevel.NativeMaximumObjectId)
            throw new ArgumentOutOfRangeException(nameof(nativeObjectId));
        if (animationFrame is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(animationFrame));
        if (activatedAnimationFrame is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(activatedAnimationFrame));
        if (dialogueMessageId is < 0 or > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dialogueMessageId));
        EntityId = entityId;
        ObjectId = objectId;
        LevelId = levelId;
        PositionX = positionX;
        PositionY = positionY;
        Activated = activated;
        QuestId = questId;
        if (questProgressAmount == 0)
            throw new ArgumentOutOfRangeException(nameof(questProgressAmount));
        QuestProgressAmount = questProgressAmount;
        if (effectAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(effectAmount));
        if (breakable && missilePassable)
            throw new ArgumentException("A missile-passable object cannot be broken by a missile.");
        if (destroyed && !breakable)
            throw new ArgumentException("Only a breakable object may begin destroyed.");
        EffectKind = effectKind;
        EffectAmount = effectAmount;
        if (!Enum.IsDefined(questTransitionKind))
            throw new ArgumentOutOfRangeException(nameof(questTransitionKind));
        if (questTransitionAmount is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(questTransitionAmount));
        if (questTransitionRequiredVariable1 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(questTransitionRequiredVariable1));
        if (questTransitionRequiredVariable2 is < -1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(questTransitionRequiredVariable2));
        if (!IsValidQuestTransition(
                questTransitionKind,
                questTransitionAmount,
                questTransitionRequiredVariable1,
                questTransitionRequiredVariable2,
                questId)) {
            throw new InvalidDataException("Object quest transition values are invalid.");
        }
        QuestTransitionKind = questTransitionKind;
        QuestTransitionAmount = questTransitionAmount;
        QuestTransitionRequiredVariable1 = questTransitionRequiredVariable1;
        QuestTransitionRequiredVariable2 = questTransitionRequiredVariable2;
        if (!IsValidReward(
                rewardKind,
                rewardId,
                rewardItemLevel,
                rewardOffsetX,
                rewardOffsetY,
                rewardOriginKind,
                rewardSetLevelId)) {
            throw new InvalidDataException("Object reward values are invalid.");
        }
        RewardKind = rewardKind;
        RewardId = rewardId;
        RewardItemLevel = rewardItemLevel;
        RewardOffsetX = rewardOffsetX;
        RewardOffsetY = rewardOffsetY;
        RewardOriginKind = rewardOriginKind;
        RewardSetLevelId = rewardSetLevelId;
        if (!IsValidProjectileTargetOffset(
                effectKind,
                projectileTargetOffsetX,
                projectileTargetOffsetY)) {
            throw new InvalidDataException("Object projectile target offsets are invalid.");
        }
        ProjectileTargetOffsetX = projectileTargetOffsetX;
        ProjectileTargetOffsetY = projectileTargetOffsetY;
        if (!IsValidQuestSequenceFollowUp(
                effectKind,
                questTransitionKind,
                questSequenceFollowUpKind,
                questSequenceFollowUpAmount,
                questSequenceFollowUpOffsetX,
                questSequenceFollowUpOffsetY)) {
            throw new InvalidDataException("Object quest-sequence follow-up values are invalid.");
        }
        if (!IsValidProjectileSource(
                effectKind,
                questSequenceFollowUpKind,
                projectileSourceKind)) {
            throw new InvalidDataException("Object projectile source values are invalid.");
        }
        if (questSequenceFollowUpWorldMutationId != 0
            && (effectKind != AuthoritativeObjectEffectKind.QuestSequence
                && questTransitionKind == AuthoritativeObjectQuestTransitionKind.None
                || questSequenceFollowUpKind == AuthoritativeQuestSequenceFollowUpKind.None)) {
            throw new InvalidDataException(
                "Object quest-sequence world-mutation follow-up values are invalid.");
        }
        if (!IsValidOccupancyTrigger(
                activateOnOccupancy,
                requiredActivatedLinkedObjects,
                questId)) {
            throw new InvalidDataException(
                "Object occupancy-trigger values are invalid.");
        }
        QuestSequenceFollowUpKind = questSequenceFollowUpKind;
        QuestSequenceFollowUpAmount = questSequenceFollowUpAmount;
        QuestSequenceFollowUpOffsetX = questSequenceFollowUpOffsetX;
        QuestSequenceFollowUpOffsetY = questSequenceFollowUpOffsetY;
        QuestSequenceFollowUpWorldMutationId = questSequenceFollowUpWorldMutationId;
        ProjectileSourceKind = projectileSourceKind;
        ActivateOnOccupancy = activateOnOccupancy;
        RequiredActivatedLinkedObjects = requiredActivatedLinkedObjects;
        NativeObjectId = nativeObjectId;
        HideWhenActivated = hideWhenActivated;
        AnimationFrame = animationFrame;
        ActivatedAnimationFrame = activatedAnimationFrame;
        DialogueMessageId = dialogueMessageId;
        MissilePassable = missilePassable;
        Breakable = breakable;
        Destroyed = destroyed;
        TrapArmed = trapArmed;
        IsShrine = isShrine;
    }

    public uint EntityId { get; }
    public uint ObjectId { get; set; }
    public uint LevelId { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public bool Activated { get; set; }
    public uint QuestId { get; set; }
    /** External progress applied when this linked quest object is activated. */
    public uint QuestProgressAmount { get; set; }
    public AuthoritativeObjectEffectKind EffectKind { get; set; }
    public int EffectAmount { get; set; }
    public AuthoritativeObjectQuestTransitionKind QuestTransitionKind { get; set; }
    public int QuestTransitionAmount { get; set; }
    public int QuestTransitionRequiredVariable1 { get; set; }
    public int QuestTransitionRequiredVariable2 { get; set; }
    public AuthoritativeObjectRewardKind RewardKind { get; set; }
    public uint RewardId { get; set; }
    public int RewardItemLevel { get; set; }
    public int RewardOffsetX { get; set; }
    public int RewardOffsetY { get; set; }

    /** Coordinate origin used when an authored item reward is generated. */
    public AuthoritativeObjectRewardOriginKind RewardOriginKind { get; set; }

    /** Native set-level identity used by SetLevelOrigin rewards; -1 means none. */
    public int RewardSetLevelId { get; set; }
    /** Authored target displacement for server-owned spell projectiles. */
    public int ProjectileTargetOffsetX { get; set; }

    public int ProjectileTargetOffsetY { get; set; }
    /** Optional effect applied after a valid quest step or direct world mutation. */
    public AuthoritativeQuestSequenceFollowUpKind QuestSequenceFollowUpKind { get; set; }

    public uint QuestSequenceFollowUpAmount { get; set; }

    public int QuestSequenceFollowUpOffsetX { get; set; }

    public int QuestSequenceFollowUpOffsetY { get; set; }

    /** Optional second follow-up applied after the primary quest-sequence action. */
    public uint QuestSequenceFollowUpWorldMutationId { get; set; }

    /** Applies this object's authored operation when a player enters its tile. */
    public bool ActivateOnOccupancy { get; set; }

    /** Optional count of other linked quest objects that must already be active. */
    public int RequiredActivatedLinkedObjects { get; set; }

    /** Optional native object type used only by compatibility presentations. */
    public uint NativeObjectId
    {
        get => nativeObjectId;
        set
        {
            if (value > AuthoritativeLevel.NativeMaximumObjectId)
                throw new ArgumentOutOfRangeException(nameof(NativeObjectId));
            nativeObjectId = value;
        }
    }

    /** Whether the compatibility presentation removes the object after activation. */
    public bool HideWhenActivated { get; set; }

    /** Native-compatible animation frame before activation. */
    public int AnimationFrame { get; set; }

    /** Native-compatible animation frame selected when the object activates. */
    public int ActivatedAnimationFrame { get; set; }

    /** Optional authored dialogue emitted when this object is activated. */
    public int DialogueMessageId { get; set; }

    /** Returns whether this object declares behavior that may be submitted as an intent. */
    public bool HasAuthoritativeInteraction
        => EffectKind != AuthoritativeObjectEffectKind.None
            || QuestId != 0
            || QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
            || RewardKind != AuthoritativeObjectRewardKind.None
            || QuestSequenceFollowUpKind != AuthoritativeQuestSequenceFollowUpKind.None
            || DialogueMessageId != 0
            || TrapArmed;

    /** Authored projectile origin; object-target remains the compatibility default. */
    public AuthoritativeObjectProjectileSourceKind ProjectileSourceKind { get; set; }
    public bool MissilePassable { get; set; }
    public bool Breakable { get; set; }
    public bool Destroyed { get; set; }

    /** Native-compatible armed trap state for cursor-driven Trap Disarm. */
    public bool TrapArmed { get; set; }

    /** Native shrine capability used by Nova Ball's target-tile collision rule. */
    public bool IsShrine { get; set; }

    /** Copies authored object behavior to a server-assigned set-level placement. */
    public AuthoritativeWorldObject WithPlacement(
        uint entityId,
        uint levelId,
        int positionX,
        int positionY)
    {
        return new AuthoritativeWorldObject(
            entityId,
            ObjectId,
            levelId,
            positionX,
            positionY,
            Activated,
            QuestId,
            EffectKind,
            EffectAmount,
            QuestProgressAmount,
            MissilePassable,
            Breakable,
            Destroyed,
            TrapArmed,
            IsShrine,
            QuestTransitionKind,
            QuestTransitionAmount,
            QuestTransitionRequiredVariable1,
            QuestTransitionRequiredVariable2,
            RewardKind,
            RewardId,
            RewardItemLevel,
            RewardOffsetX,
            RewardOffsetY,
            ProjectileTargetOffsetX,
            ProjectileTargetOffsetY,
            QuestSequenceFollowUpKind,
            QuestSequenceFollowUpAmount,
            QuestSequenceFollowUpOffsetX,
            QuestSequenceFollowUpOffsetY,
            ProjectileSourceKind,
            QuestSequenceFollowUpWorldMutationId,
            ActivateOnOccupancy,
            RequiredActivatedLinkedObjects,
            NativeObjectId,
            HideWhenActivated,
            AnimationFrame,
            ActivatedAnimationFrame,
            RewardOriginKind,
            RewardSetLevelId,
            DialogueMessageId);
    }

    /** Returns whether another object has the same authoritative authored state. */
    internal bool HasSameAuthoritativeState(AuthoritativeWorldObject other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return EntityId == other.EntityId
            && ObjectId == other.ObjectId
            && LevelId == other.LevelId
            && PositionX == other.PositionX
            && PositionY == other.PositionY
            && Activated == other.Activated
            && QuestId == other.QuestId
            && QuestProgressAmount == other.QuestProgressAmount
            && EffectKind == other.EffectKind
            && EffectAmount == other.EffectAmount
            && QuestTransitionKind == other.QuestTransitionKind
            && QuestTransitionAmount == other.QuestTransitionAmount
            && QuestTransitionRequiredVariable1 == other.QuestTransitionRequiredVariable1
            && QuestTransitionRequiredVariable2 == other.QuestTransitionRequiredVariable2
            && RewardKind == other.RewardKind
            && RewardId == other.RewardId
            && RewardItemLevel == other.RewardItemLevel
            && RewardOffsetX == other.RewardOffsetX
            && RewardOffsetY == other.RewardOffsetY
            && RewardOriginKind == other.RewardOriginKind
            && RewardSetLevelId == other.RewardSetLevelId
            && ProjectileTargetOffsetX == other.ProjectileTargetOffsetX
            && ProjectileTargetOffsetY == other.ProjectileTargetOffsetY
            && QuestSequenceFollowUpKind == other.QuestSequenceFollowUpKind
            && QuestSequenceFollowUpAmount == other.QuestSequenceFollowUpAmount
            && QuestSequenceFollowUpOffsetX == other.QuestSequenceFollowUpOffsetX
            && QuestSequenceFollowUpOffsetY == other.QuestSequenceFollowUpOffsetY
            && QuestSequenceFollowUpWorldMutationId == other.QuestSequenceFollowUpWorldMutationId
            && ProjectileSourceKind == other.ProjectileSourceKind
            && ActivateOnOccupancy == other.ActivateOnOccupancy
            && RequiredActivatedLinkedObjects == other.RequiredActivatedLinkedObjects
            && NativeObjectId == other.NativeObjectId
            && HideWhenActivated == other.HideWhenActivated
            && AnimationFrame == other.AnimationFrame
            && ActivatedAnimationFrame == other.ActivatedAnimationFrame
            && DialogueMessageId == other.DialogueMessageId
            && MissilePassable == other.MissilePassable
            && Breakable == other.Breakable
            && Destroyed == other.Destroyed
            && TrapArmed == other.TrapArmed
            && IsShrine == other.IsShrine;
    }

    public static bool IsValidQuestTransition(
        AuthoritativeObjectQuestTransitionKind kind,
        int amount,
        int requiredVariable1,
        int requiredVariable2,
        uint questId)
    {
        if (!Enum.IsDefined(kind)
            || amount is < 0 or > byte.MaxValue
            || requiredVariable1 is < -1 or > byte.MaxValue
            || requiredVariable2 is < -1 or > byte.MaxValue) {
            return false;
        }
        if (kind != AuthoritativeObjectQuestTransitionKind.None && questId == 0)
            return false;
        var setsVariable = kind is AuthoritativeObjectQuestTransitionKind.SetVariable1
            or AuthoritativeObjectQuestTransitionKind.SetVariable2
            or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1
            or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable2
            or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable1
            or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable2;
        return kind != AuthoritativeObjectQuestTransitionKind.None
            ? setsVariable ? amount > 0 : amount == 0
            : amount == 0 && requiredVariable1 < 0 && requiredVariable2 < 0;
    }

    public static bool IsValidOccupancyTrigger(
        bool activateOnOccupancy,
        int requiredActivatedLinkedObjects,
        uint questId)
    {
        return requiredActivatedLinkedObjects is >= 0 and <= byte.MaxValue
            && (requiredActivatedLinkedObjects == 0 || questId != 0)
            && (requiredActivatedLinkedObjects == 0 || activateOnOccupancy)
            && (!activateOnOccupancy || questId != 0);
    }

    public static bool IsValidReward(
        AuthoritativeObjectRewardKind kind,
        uint rewardId,
        int rewardItemLevel,
        int rewardOffsetX,
        int rewardOffsetY)
    {
        return IsValidReward(
            kind,
            rewardId,
            rewardItemLevel,
            rewardOffsetX,
            rewardOffsetY,
            AuthoritativeObjectRewardOriginKind.Object,
            -1);
    }

    public static bool IsValidReward(
        AuthoritativeObjectRewardKind kind,
        uint rewardId,
        int rewardItemLevel,
        int rewardOffsetX,
        int rewardOffsetY,
        AuthoritativeObjectRewardOriginKind originKind,
        int setLevelId)
    {
        if (!Enum.IsDefined(kind)
            || !Enum.IsDefined(originKind)
            || rewardItemLevel is < 0 or > byte.MaxValue
            || rewardOffsetX is < -16 or > 16
            || rewardOffsetY is < -16 or > 16
            || originKind == AuthoritativeObjectRewardOriginKind.Object && setLevelId != -1
            || originKind == AuthoritativeObjectRewardOriginKind.SetLevelOrigin
                && setLevelId is < 0 or > 8) {
            return false;
        }
        return kind == AuthoritativeObjectRewardKind.None
            ? rewardId == 0
                && rewardItemLevel == 0
                && rewardOffsetX == 0
                && rewardOffsetY == 0
                && originKind == AuthoritativeObjectRewardOriginKind.Object
                && setLevelId == -1
            : rewardId != 0;
    }

    public static bool IsValidProjectileTargetOffset(
        AuthoritativeObjectEffectKind effectKind,
        int offsetX,
        int offsetY)
    {
        if (offsetX is < -32 or > 32 || offsetY is < -32 or > 32)
            return false;
        return effectKind is AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
            || offsetX == 0 && offsetY == 0;
    }

    public static bool IsValidQuestSequenceFollowUp(
        AuthoritativeObjectEffectKind effectKind,
        AuthoritativeObjectQuestTransitionKind questTransitionKind,
        AuthoritativeQuestSequenceFollowUpKind followUpKind,
        uint amount,
        int offsetX,
        int offsetY)
    {
        var allowsQuestSequenceFollowUp = effectKind == AuthoritativeObjectEffectKind.QuestSequence
            || questTransitionKind != AuthoritativeObjectQuestTransitionKind.None;
        var allowsWorldMutationProjectileFollowUp = effectKind
            == AuthoritativeObjectEffectKind.WorldMutation
            && followUpKind == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile;
        if (!Enum.IsDefined(followUpKind)
            || (!allowsQuestSequenceFollowUp
                && !allowsWorldMutationProjectileFollowUp
                && followUpKind != AuthoritativeQuestSequenceFollowUpKind.None)
            || offsetX is < -32 or > 32
            || offsetY is < -32 or > 32) {
            return false;
        }
        return followUpKind switch {
            AuthoritativeQuestSequenceFollowUpKind.None
                => amount == 0 && offsetX == 0 && offsetY == 0,
            AuthoritativeQuestSequenceFollowUpKind.WorldMutation
                => amount != 0 && offsetX == 0 && offsetY == 0,
            AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile
                => amount != 0,
            AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                => amount != 0,
            _ => false,
        };
    }

    /** Compatibility overload for callers that do not model quest transitions. */
    public static bool IsValidQuestSequenceFollowUp(
        AuthoritativeObjectEffectKind effectKind,
        AuthoritativeQuestSequenceFollowUpKind followUpKind,
        uint amount,
        int offsetX,
        int offsetY)
        => IsValidQuestSequenceFollowUp(
            effectKind,
            AuthoritativeObjectQuestTransitionKind.None,
            followUpKind,
            amount,
            offsetX,
            offsetY);

    public static bool IsValidProjectileSource(
        AuthoritativeObjectEffectKind effectKind,
        AuthoritativeQuestSequenceFollowUpKind followUpKind,
        AuthoritativeObjectProjectileSourceKind sourceKind)
    {
        if (!Enum.IsDefined(sourceKind))
            return false;
        var producesProjectile = effectKind is
                AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
            || followUpKind == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile;
        return producesProjectile
            ? true
            : sourceKind == AuthoritativeObjectProjectileSourceKind.Object;
    }

    public static bool IsValidPresentation(
        bool hideWhenActivated,
        int animationFrame,
        int activatedAnimationFrame)
    {
        return animationFrame is >= 0 and <= byte.MaxValue
            && activatedAnimationFrame is >= 0 and <= byte.MaxValue;
    }
}

/** External object placement and identity definitions. */
public static class AuthoritativeWorldObjectCatalog
{
    /** Validates authored object levels and positions against world bounds. */
    public static void ValidateWorld(
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(world);
        foreach (var @object in objects) {
            if (!world.ContainsLevel(@object.LevelId))
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown level {@object.LevelId}.");
            if (!world.ContainsPosition(@object.LevelId, @object.PositionX, @object.PositionY))
                throw new InvalidDataException(
                    $"Object {@object.EntityId} contains a position outside its level.");
            if (@object.EffectKind is AuthoritativeObjectEffectKind.SpawnSpellProjectile
                    or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                && !world.ContainsPosition(
                    @object.LevelId,
                    checked(@object.PositionX + @object.ProjectileTargetOffsetX),
                    checked(@object.PositionY + @object.ProjectileTargetOffsetY))) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} contains a projectile target outside its level.");
            }
            if (@object.QuestSequenceFollowUpKind
                    == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile
                && !world.ContainsPosition(
                    @object.LevelId,
                    checked(@object.PositionX + @object.QuestSequenceFollowUpOffsetX),
                    checked(@object.PositionY + @object.QuestSequenceFollowUpOffsetY))) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} contains a quest-sequence projectile target outside its level.");
            }
            if (@object.QuestSequenceFollowUpKind
                    == AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                && !world.ContainsPosition(
                    @object.LevelId,
                    checked(@object.PositionX + @object.QuestSequenceFollowUpOffsetX),
                    checked(@object.PositionY + @object.QuestSequenceFollowUpOffsetY))) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} contains a quest-sequence monster spawn outside its level.");
            }
        }
    }

    public static IReadOnlyList<AuthoritativeWorldObject> LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        return LoadTsvCore(sourcePath, contents, stableIds, includeTemplateOnly: false);
    }

    /** Loads behavior-only rows that are available to set-level/DUN mappings. */
    public static IReadOnlyList<AuthoritativeWorldObject> LoadTemplates(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        return LoadTsvCore(sourcePath, contents, stableIds, includeTemplateOnly: true);
    }

    private static IReadOnlyList<AuthoritativeWorldObject> LoadTsvCore(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds,
        bool includeTemplateOnly)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var parsed = table.Rows.Select(row => {
            var templateOnly = row.OptionalBoolean("template_only");
            var effectKind = ParseEffectKind(row);
            var followUpKind = ParseQuestSequenceFollowUpKind(row);
            var projectileSourceKind = ParseProjectileSourceKind(row);
            return (
                Object: new AuthoritativeWorldObject(
                    row.RequiredUInt32("entity_id"),
                    AuthoritativeWorldObjectIdentity.Resolve(row, stableIds),
                    AuthoritativeLevelIdentity.ResolveOptional(
                        row,
                        stableIds,
                        "level_id",
                        "level_symbol"),
                    templateOnly ? row.OptionalInt32("position_x") : row.RequiredInt32("position_x"),
                    templateOnly ? row.OptionalInt32("position_y") : row.RequiredInt32("position_y"),
                    row.OptionalBoolean("activated"),
                    AuthoritativeQuestIdentity.ResolveOptional(
                        row,
                        stableIds,
                        "quest_id",
                        "quest_symbol"),
                    effectKind,
                    ParseEffectAmount(row, effectKind, stableIds),
                    row.OptionalUInt32("quest_progress", 1),
                    row.OptionalBoolean("missile_passable"),
                    row.OptionalBoolean("breakable"),
                    row.OptionalBoolean("destroyed"),
                    row.OptionalBoolean("trap_armed"),
                    row.OptionalBoolean("is_shrine"),
                    ParseQuestTransitionKind(row),
                    row.OptionalInt32("quest_transition_amount"),
                    OptionalQuestTransitionValue(row, "quest_transition_required_variable1"),
                    OptionalQuestTransitionValue(row, "quest_transition_required_variable2"),
                    ParseRewardKind(row),
                    row.OptionalUInt32("reward_id"),
                    row.OptionalInt32("reward_item_level"),
                    row.OptionalInt32("reward_offset_x"),
                    row.OptionalInt32("reward_offset_y"),
                    row.OptionalInt32("projectile_target_offset_x"),
                    row.OptionalInt32("projectile_target_offset_y"),
                    followUpKind,
                    ParseQuestSequenceFollowUpAmount(row, followUpKind, stableIds),
                    row.OptionalInt32("quest_sequence_follow_up_offset_x"),
                    row.OptionalInt32("quest_sequence_follow_up_offset_y"),
                    projectileSourceKind,
                    row.OptionalUInt32("quest_sequence_follow_up_world_mutation_id"),
                    row.OptionalBoolean("activate_on_occupancy"),
                    row.OptionalInt32("required_activated_linked_objects"),
                    row.OptionalUInt32("native_object_id"),
                    row.OptionalBoolean("hide_when_activated", true),
                    row.OptionalInt32("animation_frame"),
                    row.OptionalInt32("activated_animation_frame"),
                    ParseRewardOriginKind(row),
                    ParseRewardSetLevelId(row, stableIds),
                    row.OptionalInt32("dialogue_message_id")),
                TemplateOnly: templateOnly);
        }).ToArray();
        var definitions = parsed.Select(item => item.Object).ToArray();
        if (definitions.Any(@object => @object.EffectKind == AuthoritativeObjectEffectKind.None && @object.EffectAmount != 0))
            throw new InvalidDataException($"Object table '{sourcePath}' assigns an amount to an object without an effect.");
        if (definitions.Any(@object => @object.EffectKind == AuthoritativeObjectEffectKind.QuestSequence
                && (@object.QuestId == 0 || @object.EffectAmount <= 0)))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains an invalid quest sequence object.");
        if (definitions.Any(@object => @object.EffectKind == AuthoritativeObjectEffectKind.QuestItemCompletion
                && (@object.QuestId == 0 || @object.EffectAmount <= 0)))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains an invalid quest-item completion object.");
        if (definitions.Any(@object => @object.EffectKind is
                (AuthoritativeObjectEffectKind.GrantSpellLevel
                or AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile)
                && @object.EffectAmount <= 0))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains an invalid spell effect object.");
        if (definitions.Any(@object => @object.QuestSequenceFollowUpKind
                == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile
                && @object.QuestSequenceFollowUpAmount == 0))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains an invalid quest-sequence projectile effect.");
        if (definitions.Any(@object => @object.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
                && @object.QuestId == 0))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains a quest transition without a quest.");
        if (definitions.Select(@object => @object.EntityId).Distinct().Count() != definitions.Length)
            throw new InvalidDataException($"Object table '{sourcePath}' contains duplicate entity IDs.");
        return parsed
            .Where(item => item.TemplateOnly == includeTemplateOnly)
            .Select(item => item.Object)
            .ToArray();
    }

    /** Validates normalized item identities encoded by quest-item objects. */
    public static void ValidateItemRequirements(
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(items);
        foreach (var @object in objects
                     .Where(@object => @object.EffectKind
                         == AuthoritativeObjectEffectKind.QuestItemCompletion)) {
            if (!items.TryGet((uint)@object.EffectAmount, out _))
                throw new InvalidDataException(
                    $"Quest-item object {@object.EntityId} references unknown item {@object.EffectAmount}.");
        }
    }

    /** Validates authored object rewards against the active item/unique catalogs. */
    public static void ValidateRewardRequirements(
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(items);
        foreach (var @object in objects.Where(@object =>
                     @object.RewardKind != AuthoritativeObjectRewardKind.None)) {
            if (@object.RewardKind == AuthoritativeObjectRewardKind.NormalItem
                && !items.TryGet(@object.RewardId, out _)) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown reward item {@object.RewardId}.");
            }
            if (@object.RewardKind == AuthoritativeObjectRewardKind.UniqueItem) {
                if (!items.TryGetUnique(@object.RewardId, out var unique))
                    throw new InvalidDataException(
                        $"Object {@object.EntityId} references unknown reward unique {@object.RewardId}.");
                if (@object.RewardItemLevel < unique.MinimumLevel)
                    throw new InvalidDataException(
                        $"Object {@object.EntityId} reward unique {@object.RewardId} requires item level {unique.MinimumLevel}.");
            }
        }
    }

    /** Validates stable monster identities used by authored encounter follow-ups. */
    public static void ValidateMonsterRequirements(
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var @object in objects.Where(@object =>
                     @object.QuestSequenceFollowUpKind
                         == AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster)) {
            var monsterId = @object.QuestSequenceFollowUpAmount;
            if (!monsterTypes.TryGet(monsterId, out _)) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown monster {monsterId}.");
            }
        }
    }

    /** Validates world-mutation references encoded by authored objects. */
    public static void ValidateMutationRequirements(
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeWorldMutationCatalog mutations)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(mutations);
        foreach (var @object in objects) {
            var mutationIds = new List<uint>();
            if (@object.EffectKind == AuthoritativeObjectEffectKind.WorldMutation)
                mutationIds.Add(@object.EffectAmount > 0 ? (uint)@object.EffectAmount : 0);
            if (@object.QuestSequenceFollowUpKind
                == AuthoritativeQuestSequenceFollowUpKind.WorldMutation) {
                mutationIds.Add(@object.QuestSequenceFollowUpAmount);
            }
            if (@object.QuestSequenceFollowUpWorldMutationId != 0)
                mutationIds.Add(@object.QuestSequenceFollowUpWorldMutationId);
            foreach (var mutationId in mutationIds.Where(id => id == 0 || !mutations.TryGet(id, out _))) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown world mutation {mutationId}.");
            }
        }
    }

    /** Validates spell identities encoded by authored spell-level objects. */
    public static void ValidateSpellRequirements(
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeSpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(spells);
        foreach (var @object in objects.Where(@object => @object.EffectKind is
                     AuthoritativeObjectEffectKind.GrantSpellLevel
                     or AuthoritativeObjectEffectKind.SpawnSpellProjectile
                     or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                     || @object.QuestSequenceFollowUpKind
                         == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile)) {
            var spellId = @object.EffectKind is
                    AuthoritativeObjectEffectKind.GrantSpellLevel
                    or AuthoritativeObjectEffectKind.SpawnSpellProjectile
                    or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                ? checked((uint)@object.EffectAmount)
                : @object.QuestSequenceFollowUpAmount;
            if (!spells.TryGet(spellId, out var spell))
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown spell {spellId}.");
            if ((@object.EffectKind is AuthoritativeObjectEffectKind.SpawnSpellProjectile
                    or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                || @object.QuestSequenceFollowUpKind
                        == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile)
                && !IsSupportedSpellProjectile(spell)) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unsupported authored projectile behavior '{spell.ProjectileBehavior}'.");
            }
            if (@object.ProjectileSourceKind
                    == AuthoritativeObjectProjectileSourceKind.ActivatingPlayer
                && spell.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Phasing
                    or AuthoritativeProjectileBehavior.GuardianController)) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} requires ActivatingPlayer projectile source only for Phasing or Guardian.");
            }
        }
    }

    /** Spell behaviors whose controller construction is fully authored and server-owned. */
    public static bool IsSupportedSpellProjectile(
        AuthoritativeSpellDefinition spell)
        => spell.ProjectileBehavior is
            AuthoritativeProjectileBehavior.Phasing
            or AuthoritativeProjectileBehavior.GuardianController;

    /** Validates quest identities referenced by content-owned world objects. */
    public static void ValidateQuestRequirements(
        IEnumerable<AuthoritativeWorldObject> objects,
        IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(quests);
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var @object in objects.Where(@object => @object.QuestId != 0)) {
            if (!questIds.Contains(@object.QuestId))
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown quest {@object.QuestId}.");
        }
    }

    private static AuthoritativeObjectEffectKind ParseEffectKind(TsvRow row)
    {
        if (!row.TryGet("effect_kind", out var value) || string.IsNullOrWhiteSpace(value))
            return AuthoritativeObjectEffectKind.None;
        if (Enum.TryParse<AuthoritativeObjectEffectKind>(value, true, out var effectKind))
            return effectKind;
        throw new InvalidDataException($"Object row {row.LineNumber} contains an unknown effect kind '{value}'.");
    }

    private static AuthoritativeQuestSequenceFollowUpKind ParseQuestSequenceFollowUpKind(TsvRow row)
    {
        if (!row.TryGet("quest_sequence_follow_up_kind", out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return AuthoritativeQuestSequenceFollowUpKind.None;
        }
        if (Enum.TryParse<AuthoritativeQuestSequenceFollowUpKind>(value, true, out var kind)
            && Enum.IsDefined(kind)) {
            return kind;
        }
        throw new InvalidDataException(
            $"Object row {row.LineNumber} contains an unknown quest-sequence follow-up kind '{value}'.");
    }

    private static AuthoritativeObjectProjectileSourceKind ParseProjectileSourceKind(TsvRow row)
    {
        if (!row.TryGet("projectile_source", out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "object", StringComparison.OrdinalIgnoreCase)) {
            return AuthoritativeObjectProjectileSourceKind.Object;
        }
        if (Enum.TryParse<AuthoritativeObjectProjectileSourceKind>(value, true, out var kind)
            && Enum.IsDefined(kind)) {
            return kind;
        }
        throw new InvalidDataException(
            $"Object row {row.LineNumber} contains an unknown projectile source '{value}'.");
    }

    private static AuthoritativeObjectQuestTransitionKind ParseQuestTransitionKind(TsvRow row)
    {
        if (!row.TryGet("quest_transition_kind", out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return AuthoritativeObjectQuestTransitionKind.None;
        }
        if (Enum.TryParse<AuthoritativeObjectQuestTransitionKind>(value, true, out var kind)
            && Enum.IsDefined(kind)) {
            return kind;
        }
        throw new InvalidDataException(
            $"Object row {row.LineNumber} contains an unknown quest transition kind '{value}'.");
    }

    private static AuthoritativeObjectRewardKind ParseRewardKind(TsvRow row)
    {
        if (!row.TryGet("reward_kind", out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return AuthoritativeObjectRewardKind.None;
        }
        if (Enum.TryParse<AuthoritativeObjectRewardKind>(value, true, out var kind)
            && Enum.IsDefined(kind)) {
            return kind;
        }
        throw new InvalidDataException(
            $"Object row {row.LineNumber} contains an unknown reward kind '{value}'.");
    }

    private static AuthoritativeObjectRewardOriginKind ParseRewardOriginKind(TsvRow row)
    {
        if (!row.TryGet("reward_origin", out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "object", StringComparison.OrdinalIgnoreCase))
            return AuthoritativeObjectRewardOriginKind.Object;
        if (string.Equals(value, "set_level_origin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "set_level_origin_offset", StringComparison.OrdinalIgnoreCase))
            return AuthoritativeObjectRewardOriginKind.SetLevelOrigin;
        throw new InvalidDataException(
            $"Object row {row.LineNumber} contains an unknown reward origin '{value}'.");
    }

    private static int ParseRewardSetLevelId(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var symbol = row.TryGet("reward_set_level_symbol", out var value)
            ? value.Trim()
            : string.Empty;
        if (string.Equals(symbol, "none", StringComparison.OrdinalIgnoreCase))
            return row.OptionalInt32("reward_set_level_id", -1);
        if (symbol.Length == 0
            && (!row.TryGet("reward_set_level_id", out var id)
                || string.IsNullOrWhiteSpace(id)))
            return -1;
        return AuthoritativeSetLevelIdentity.ResolveOptional(
            row,
            stableIds,
            "reward_set_level_id",
            "reward_set_level_symbol");
    }

    private static int OptionalQuestTransitionValue(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return -1;
        }
        return row.OptionalInt32(column, -1);
    }

    private static int ParseEffectAmount(
        TsvRow row,
        AuthoritativeObjectEffectKind effectKind,
        StableContentIdCatalog? stableIds)
    {
        var itemSymbol = AuthoritativeItemIdentity.ReadSymbol(row, "effect_item_symbol");
        var spellSymbol = AuthoritativeSpellIdentity.ReadSymbol(row, "effect_spell_symbol");
        if (itemSymbol.Length > 0 && effectKind != AuthoritativeObjectEffectKind.QuestItemCompletion)
            throw new InvalidDataException(
                $"Object row {row.LineNumber} supplies effect_item_symbol for effect kind '{effectKind}'.");
        if (spellSymbol.Length > 0 && effectKind is not (
                AuthoritativeObjectEffectKind.GrantSpellLevel
                or AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile))
            throw new InvalidDataException(
                $"Object row {row.LineNumber} supplies effect_spell_symbol for effect kind '{effectKind}'.");
        if (effectKind is AuthoritativeObjectEffectKind.GrantSpellLevel
                or AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile)
            return checked((int)AuthoritativeSpellIdentity.ResolveOptional(
                row,
                stableIds,
                "effect_amount",
                "effect_spell_symbol"));
        if (effectKind != AuthoritativeObjectEffectKind.QuestItemCompletion)
            return row.OptionalInt32("effect_amount");
        return checked((int)AuthoritativeItemIdentity.ResolveOptional(
            row,
            stableIds,
            "effect_amount",
            "effect_item_symbol"));
    }

    private static uint ParseQuestSequenceFollowUpAmount(
        TsvRow row,
        AuthoritativeQuestSequenceFollowUpKind followUpKind,
        StableContentIdCatalog? stableIds)
    {
        var spellSymbol = AuthoritativeSpellIdentity.ReadSymbol(
            row,
            "quest_sequence_follow_up_spell_symbol");
        var monsterSymbol = AuthoritativeMonsterIdentity.ReadSymbol(
            row,
            "quest_sequence_follow_up_monster_symbol");
        if (spellSymbol.Length > 0
            && followUpKind != AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile) {
            throw new InvalidDataException(
                $"Object row {row.LineNumber} supplies quest_sequence_follow_up_spell_symbol for follow-up kind '{followUpKind}'.");
        }
        if (monsterSymbol.Length > 0
            && followUpKind != AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster) {
            throw new InvalidDataException(
                $"Object row {row.LineNumber} supplies quest_sequence_follow_up_monster_symbol for follow-up kind '{followUpKind}'.");
        }
        if (followUpKind == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile) {
            return AuthoritativeSpellIdentity.ResolveOptional(
                row,
                stableIds,
                "quest_sequence_follow_up_amount",
                "quest_sequence_follow_up_spell_symbol");
        }
        if (followUpKind == AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster) {
            return AuthoritativeMonsterIdentity.ResolveOptional(
                row,
                stableIds,
                "quest_sequence_follow_up_amount",
                "quest_sequence_follow_up_monster_symbol");
        }
        return row.OptionalUInt32("quest_sequence_follow_up_amount");
    }
}
