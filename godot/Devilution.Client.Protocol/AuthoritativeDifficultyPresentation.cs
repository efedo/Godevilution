using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Formats the server-selected difficulty for presentation without owning rules. */
public static class AuthoritativeDifficultyPresentation
{
    public static string Format(GameDifficulty difficulty)
    {
        return difficulty switch {
            GameDifficulty.Nightmare => "Nightmare",
            GameDifficulty.Hell => "Hell",
            GameDifficulty.Unspecified or GameDifficulty.Normal => "Normal",
            _ => "Normal",
        };
    }
}
