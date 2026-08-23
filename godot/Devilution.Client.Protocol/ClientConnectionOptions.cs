using Devilution.Protocol;

namespace Devilution.Client.Protocol;

/** Connection identity and retry settings shared by Godot presentation code. */
public sealed record ClientConnectionOptions(
    string Host,
    int Port,
    string ClientBuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash = "")
{
    public string ResumeToken { get; init; } = string.Empty;

    /** Stable active-content class ID requested only when creating a session. */
    public uint? RequestedCharacterClassId { get; init; }

    /** Native-compatible character identity requested only when creating a session. */
    public string? RequestedCharacterName { get; init; }

    public TimeSpan MinimumRetryTimeout { get; init; } = TimeSpan.FromMilliseconds(150);

    public TimeSpan MaximumRetryTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan SnapshotPollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public static ClientConnectionOptions FromEnvironment()
    {
        return new ClientConnectionOptions(
            Environment.GetEnvironmentVariable("DEVILUTION_SERVER_HOST") ?? "127.0.0.1",
            ParsePort(Environment.GetEnvironmentVariable("DEVILUTION_SERVER_PORT")),
            Environment.GetEnvironmentVariable("DEVILUTION_CLIENT_BUILD") ?? "godot-client-dev",
            Environment.GetEnvironmentVariable("DEVILUTION_PROTOCOL_VERSION")
                ?? ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            Environment.GetEnvironmentVariable("DEVILUTION_CONTENT_HASH") ?? string.Empty,
            Environment.GetEnvironmentVariable("DEVILUTION_RULESET_HASH") ?? string.Empty) {
            ResumeToken = Environment.GetEnvironmentVariable("DEVILUTION_RESUME_TOKEN") ?? string.Empty,
            RequestedCharacterClassId = ParseOptionalUInt32(
                Environment.GetEnvironmentVariable("DEVILUTION_CHARACTER_CLASS_ID")),
            RequestedCharacterName = Environment.GetEnvironmentVariable(
                "DEVILUTION_CHARACTER_NAME"),
        };
    }

    /** Validates the endpoint values used by both the Godot form and TCP transport. */
    public static bool TryValidateEndpoint(string? host, int port, out string error)
    {
        if (string.IsNullOrWhiteSpace(host)) {
            error = "Enter a server host.";
            return false;
        }
        if (host.Trim().Length > 253) {
            error = "The server host is too long.";
            return false;
        }
        if (port is < 1 or > 65535) {
            error = "Enter a server port between 1 and 65535.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int ParsePort(string? value)
    {
        return int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : 6113;
    }

    private static uint? ParseOptionalUInt32(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return uint.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidDataException(
                "DEVILUTION_CHARACTER_CLASS_ID must be an unsigned integer.");
    }
}
