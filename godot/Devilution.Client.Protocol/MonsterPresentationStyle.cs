using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation family selected from server-owned monster metadata. */
public enum MonsterPresentationKind
{
    Generic,
    Dialogue,
    PlayerMinion,
}

public static class MonsterPresentationStyle
{
    public static MonsterPresentationKind Resolve(MonsterSnapshot monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if (monster.Alive && monster.IsPlayerMinion)
            return MonsterPresentationKind.PlayerMinion;
        return monster.Alive
            && monster.IsUnique
            && monster.DialogueMessageId > 0
            ? MonsterPresentationKind.Dialogue
            : MonsterPresentationKind.Generic;
    }
}
