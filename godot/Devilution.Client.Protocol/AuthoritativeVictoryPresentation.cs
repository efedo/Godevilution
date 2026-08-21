using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Formats the server-owned Diablo victory identity for presentation clients. */
public static class AuthoritativeVictoryPresentation
{
    public static string Format(VictoryEvent victory)
    {
        ArgumentNullException.ThrowIfNull(victory);
        return victory.DiabloKillLevel == 0
            ? "Victory — Diablo defeated"
            : $"Victory — Diablo defeated on level {victory.DiabloKillLevel}";
    }
}
