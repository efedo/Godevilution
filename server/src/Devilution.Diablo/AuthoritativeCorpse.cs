namespace Devilution.Diablo;

/** Server-owned corpse tile left by a defeated non-minion monster. */
public sealed class AuthoritativeCorpse
{
    public AuthoritativeCorpse(
        uint entityId,
        uint levelId,
        int positionX,
        int positionY)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        EntityId = entityId;
        LevelId = levelId;
        PositionX = positionX;
        PositionY = positionY;
    }

    public uint EntityId { get; }

    public uint LevelId { get; }

    public int PositionX { get; }

    public int PositionY { get; }
}
