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
        bool trapArmed = false)
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
        MissilePassable = missilePassable;
        Breakable = breakable;
        Destroyed = destroyed;
        TrapArmed = trapArmed;
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
    public bool MissilePassable { get; set; }
    public bool Breakable { get; set; }
    public bool Destroyed { get; set; }

    /** Native-compatible armed trap state for cursor-driven Trap Disarm. */
    public bool TrapArmed { get; set; }
}

/** External object placement and identity definitions. */
public static class AuthoritativeWorldObjectCatalog
{
    public static IReadOnlyList<AuthoritativeWorldObject> LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var objects = table.Rows.Select(row => new AuthoritativeWorldObject(
            row.RequiredUInt32("entity_id"),
            row.RequiredUInt32("object_id"),
            row.RequiredUInt32("level_id"),
            row.RequiredInt32("position_x"),
            row.RequiredInt32("position_y"),
            row.OptionalInt32("activated") != 0,
            row.OptionalUInt32("quest_id"),
            ParseEffectKind(row),
            row.OptionalInt32("effect_amount"),
            row.OptionalUInt32("quest_progress", 1),
            row.OptionalInt32("missile_passable") != 0,
            row.OptionalInt32("breakable") != 0,
            row.OptionalInt32("destroyed") != 0,
            row.OptionalInt32("trap_armed") != 0)).ToArray();
        if (objects.Any(@object => @object.EffectKind == AuthoritativeObjectEffectKind.None && @object.EffectAmount != 0))
            throw new InvalidDataException($"Object table '{sourcePath}' assigns an amount to an object without an effect.");
        if (objects.Select(@object => @object.EntityId).Distinct().Count() != objects.Length)
            throw new InvalidDataException($"Object table '{sourcePath}' contains duplicate entity IDs.");
        return objects;
    }

    private static AuthoritativeObjectEffectKind ParseEffectKind(TsvRow row)
    {
        if (!row.TryGet("effect_kind", out var value) || string.IsNullOrWhiteSpace(value))
            return AuthoritativeObjectEffectKind.None;
        if (Enum.TryParse<AuthoritativeObjectEffectKind>(value, true, out var effectKind))
            return effectKind;
        throw new InvalidDataException($"Object row {row.LineNumber} contains an unknown effect kind '{value}'.");
    }
}
