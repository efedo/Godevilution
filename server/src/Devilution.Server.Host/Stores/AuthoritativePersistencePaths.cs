namespace Devilution.Server.Stores;

/** Centralizes the safe session-file path contract for server-owned stores. */
internal static class AuthoritativePersistencePaths
{
    public static string Combine(
        string rootDirectory,
        string sessionId,
        string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        if (sessionId.Any(character =>
                !char.IsLetterOrDigit(character)
                && character is not '-' and not '_')) {
            throw new ArgumentException(
                "Session IDs may contain only letters, digits, '-' and '_'.",
                nameof(sessionId));
        }
        return Path.Combine(rootDirectory, sessionId + suffix);
    }
}
