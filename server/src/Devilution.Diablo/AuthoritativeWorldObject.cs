using Devilution.Data;

namespace Devilution.Diablo;

/** Mutable server-owned object state used by interaction commands. */
public sealed class AuthoritativeWorldObject
{
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
        int rewardOffsetY = 0)
    {
        if (entityId == 0 || objectId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
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
                rewardOffsetY)) {
            throw new InvalidDataException("Object reward values are invalid.");
        }
        RewardKind = rewardKind;
        RewardId = rewardId;
        RewardItemLevel = rewardItemLevel;
        RewardOffsetX = rewardOffsetX;
        RewardOffsetY = rewardOffsetY;
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
    public bool MissilePassable { get; set; }
    public bool Breakable { get; set; }
    public bool Destroyed { get; set; }

    /** Native-compatible armed trap state for cursor-driven Trap Disarm. */
    public bool TrapArmed { get; set; }

    /** Native shrine capability used by Nova Ball's target-tile collision rule. */
    public bool IsShrine { get; set; }

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

    public static bool IsValidReward(
        AuthoritativeObjectRewardKind kind,
        uint rewardId,
        int rewardItemLevel,
        int rewardOffsetX,
        int rewardOffsetY)
    {
        if (!Enum.IsDefined(kind)
            || rewardItemLevel is < 0 or > byte.MaxValue
            || rewardOffsetX is < -16 or > 16
            || rewardOffsetY is < -16 or > 16) {
            return false;
        }
        return kind == AuthoritativeObjectRewardKind.None
            ? rewardId == 0
                && rewardItemLevel == 0
                && rewardOffsetX == 0
                && rewardOffsetY == 0
            : rewardId != 0;
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
        }
    }

    public static IReadOnlyList<AuthoritativeWorldObject> LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var objects = table.Rows.Select(row => {
            var effectKind = ParseEffectKind(row);
            return new AuthoritativeWorldObject(
                row.RequiredUInt32("entity_id"),
                AuthoritativeWorldObjectIdentity.Resolve(row, stableIds),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
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
                row.OptionalInt32("reward_offset_y"));
        }).ToArray();
        if (objects.Any(@object => @object.EffectKind == AuthoritativeObjectEffectKind.None && @object.EffectAmount != 0))
            throw new InvalidDataException($"Object table '{sourcePath}' assigns an amount to an object without an effect.");
        if (objects.Any(@object => @object.EffectKind == AuthoritativeObjectEffectKind.QuestSequence
                && (@object.QuestId == 0 || @object.EffectAmount <= 0)))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains an invalid quest sequence object.");
        if (objects.Any(@object => @object.EffectKind == AuthoritativeObjectEffectKind.QuestItemCompletion
                && (@object.QuestId == 0 || @object.EffectAmount <= 0)))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains an invalid quest-item completion object.");
        if (objects.Any(@object => @object.EffectKind
                == AuthoritativeObjectEffectKind.GrantSpellLevel
                && @object.EffectAmount <= 0))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains an invalid spell-level grant object.");
        if (objects.Any(@object => @object.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
                && @object.QuestId == 0))
            throw new InvalidDataException(
                $"Object table '{sourcePath}' contains a quest transition without a quest.");
        if (objects.Select(@object => @object.EntityId).Distinct().Count() != objects.Length)
            throw new InvalidDataException($"Object table '{sourcePath}' contains duplicate entity IDs.");
        return objects;
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

    /** Validates world-mutation references encoded by authored objects. */
    public static void ValidateMutationRequirements(
        IEnumerable<AuthoritativeWorldObject> objects,
        AuthoritativeWorldMutationCatalog mutations)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(mutations);
        foreach (var @object in objects.Where(@object =>
                     @object.EffectKind == AuthoritativeObjectEffectKind.WorldMutation)) {
            if (@object.EffectAmount <= 0
                || !mutations.TryGet((uint)@object.EffectAmount, out _)) {
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown world mutation {@object.EffectAmount}.");
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
        foreach (var @object in objects.Where(@object =>
                     @object.EffectKind == AuthoritativeObjectEffectKind.GrantSpellLevel)) {
            if (!spells.TryGet(checked((uint)@object.EffectAmount), out _))
                throw new InvalidDataException(
                    $"Object {@object.EntityId} references unknown spell {@object.EffectAmount}.");
        }
    }

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
        if (spellSymbol.Length > 0 && effectKind != AuthoritativeObjectEffectKind.GrantSpellLevel)
            throw new InvalidDataException(
                $"Object row {row.LineNumber} supplies effect_spell_symbol for effect kind '{effectKind}'.");
        if (effectKind == AuthoritativeObjectEffectKind.GrantSpellLevel)
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
}
