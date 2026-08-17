namespace Devilution.Server.Host;

/** Commands accepted by the standalone server operator console. */
public enum ServerOperatorCommandKind
{
    Empty,
    Help,
    Status,
    Reload,
    Quit,
    Unknown,
}

/** Parses the deliberately small, local-only operator command surface. */
public static class ServerOperatorCommand
{
    public static ServerOperatorCommandKind Parse(string? input)
    {
        return input?.Trim().ToLowerInvariant() switch {
            null or "" => ServerOperatorCommandKind.Empty,
            "help" or "?" => ServerOperatorCommandKind.Help,
            "status" => ServerOperatorCommandKind.Status,
            "reload" => ServerOperatorCommandKind.Reload,
            "quit" or "exit" or "stop" => ServerOperatorCommandKind.Quit,
            _ => ServerOperatorCommandKind.Unknown,
        };
    }
}
