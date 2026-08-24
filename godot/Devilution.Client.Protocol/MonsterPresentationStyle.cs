using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation family selected from server-owned monster dialogue metadata. */
public enum MonsterPresentationKind
{
    Generic,
    Dialogue,
}

public static class MonsterPresentationStyle
{
    public static MonsterPresentationKind Resolve(MonsterSnapshot monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.Alive
            && monster.IsUnique
            && monster.DialogueMessageId > 0
            ? MonsterPresentationKind.Dialogue
            : MonsterPresentationKind.Generic;
    }
}
