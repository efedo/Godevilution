using System.Globalization;
using System.Net;
using Devilution.Protocol;

namespace Devilution.Server.Host;

/** Command-line configuration for the standalone authoritative server host. */
public sealed record ServerHostOptions(
    IPAddress BindAddress,
    int Port,
    string ContentRoot,
    string ContentId,
    string ContentVersion,
    string BuildId,
    string ProtocolSchemaVersion,
    uint TickRateHz,
    uint? StartingGold)
{
    public int? StartingLife { get; init; }

    public int? StartingMana { get; init; }

    /** Optional server-owned primary-attribute points for development starts. */
    public uint? StartingStatPoints { get; init; }

    /** Acceptance-only participant definition file read by the selected game adapter. */
    public string? TestParticipantPath { get; init; }

    /** Explicit whole-host opt-in; native compatibility retains immediate timing. */
    public bool UseClockGatedCommandScheduling { get; init; }

    /** Dedicated-root opt-in; existing per-session saves are never migrated implicitly. */
    public bool UseCohortPersistence { get; init; }

    /** Optional content level override for newly initialized participants. */
    public uint? StartingLevelId { get; init; }

    /** Null loads the save-root seed or creates it once for a new server. */
    public uint? RandomSeed { get; init; }

    public string SaveRoot { get; init; } = Path.Combine("server", "saves");

    /** Optional new file for an explicitly started local diagnostic recording. */
    public string? PerformanceOutput { get; init; }

    /** Opt-in bounded stdout evidence for host command/snapshot timing. */
    public bool CommandTimingTrace { get; init; }

    public IReadOnlyList<string> ContentOverlayRoots { get; init; } = [];

    public IReadOnlyList<string> GameplayModulePackageRoots { get; init; } = [];

    /** Optional hash-verified packages that provide additional host adapters. */
    public IReadOnlyList<string> GameHostAdapterPackageRoots { get; init; } = [];

    /** Optional adapter factory ID; null preserves single-compatible-factory selection. */
    public string? GameAdapterId { get; init; }

    /** Standalone servers are multiplayer unless a local single-player launch opts out. */
    public AuthoritativeGameMode GameMode { get; init; } = AuthoritativeGameMode.Multiplayer;

    /** Native difficulty selected for the authoritative session. */
    public AuthoritativeGameDifficulty Difficulty { get; init; } = AuthoritativeGameDifficulty.Normal;

    /** Optional bounded server-owned encounter replacement for the start level. */
    public int? ProceduralMonsterLimit { get; init; }

    /** Optional level override for the procedural encounter replacement. */
    public uint? ProceduralMonsterLevelId { get; init; }

    public static ServerHostOptions Defaults { get; } = new(
        IPAddress.Loopback,
        6113,
        Path.Combine("server", "content", "base"),
        "devilution.base",
        "0.1.0",
        "devilution-server",
        ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
        20,
        null);

    public static bool TryParse(IReadOnlyList<string> args, out ServerHostOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(args);
        var current = Defaults;
        for (var index = 0; index < args.Count; index++) {
            var argument = args[index];
            if (argument is "--help" or "-h") {
                options = current;
                error = string.Empty;
                return false;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count) {
                options = current;
                error = $"Unknown or incomplete option '{argument}'.";
                return false;
            }

            var value = args[++index];
            switch (argument) {
            case "--persistence-mode":
                if (value is not ("legacy" or "cohort")) {
                    options = current;
                    error = $"'{value}' is not a valid persistence mode.";
                    return false;
                }
                current = current with { UseCohortPersistence = value == "cohort" };
                break;
            case "--command-scheduling":
                if (value is not ("immediate" or "clock-gated")) {
                    options = current;
                    error = $"'{value}' is not a valid command scheduling mode.";
                    return false;
                }
                current = current with { UseClockGatedCommandScheduling = value == "clock-gated" };
                break;
            case "--performance-output":
                if (string.IsNullOrWhiteSpace(value)) {
                    options = current;
                    error = "The performance output path cannot be empty.";
                    return false;
                }
                current = current with { PerformanceOutput = value };
                break;
            case "--command-timing-trace":
                if (!bool.TryParse(value, out var commandTimingTrace)) {
                    options = current;
                    error = $"'{value}' is not a valid command timing trace setting.";
                    return false;
                }
                current = current with { CommandTimingTrace = commandTimingTrace };
                break;
            case "--bind":
                if (!IPAddress.TryParse(value, out var bindAddress)) {
                    options = current;
                    error = $"'{value}' is not a valid bind address.";
                    return false;
                }
                current = current with { BindAddress = bindAddress };
                break;
            case "--port":
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 0 or > 65535) {
                    options = current;
                    error = $"'{value}' is not a valid TCP port.";
                    return false;
                }
                current = current with { Port = port };
                break;
            case "--content-root":
                current = current with { ContentRoot = value };
                break;
            case "--content-id":
                current = current with { ContentId = value };
                break;
            case "--content-version":
                current = current with { ContentVersion = value };
                break;
            case "--content-overlay":
                current = current with {
                    ContentOverlayRoots = current.ContentOverlayRoots.Append(value).ToArray(),
                };
                break;
            case "--module-package":
                current = current with {
                    GameplayModulePackageRoots = current.GameplayModulePackageRoots
                        .Append(value)
                        .ToArray(),
                };
                break;
            case "--game-adapter-package":
                current = current with {
                    GameHostAdapterPackageRoots = current.GameHostAdapterPackageRoots
                        .Append(value)
                        .ToArray(),
                };
                break;
            case "--game-adapter":
                current = current with { GameAdapterId = value };
                break;
            case "--build-id":
                current = current with { BuildId = value };
                break;
            case "--protocol-version":
                current = current with { ProtocolSchemaVersion = value };
                break;
            case "--tick-rate":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var tickRate) || tickRate == 0) {
                    options = current;
                    error = $"'{value}' is not a valid tick rate.";
                    return false;
                }
                current = current with { TickRateHz = tickRate };
                break;
            case "--starting-gold":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var startingGold)) {
                    options = current;
                    error = $"'{value}' is not a valid starting gold amount.";
                    return false;
                }
                current = current with { StartingGold = startingGold };
                break;
            case "--starting-life":
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var startingLife) || startingLife < 0) {
                    options = current;
                    error = $"'{value}' is not a valid starting life amount.";
                    return false;
                }
                current = current with { StartingLife = startingLife };
                break;
            case "--starting-mana":
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var startingMana) || startingMana < 0) {
                    options = current;
                    error = $"'{value}' is not a valid starting mana amount.";
                    return false;
                }
                current = current with { StartingMana = startingMana };
                break;
            case "--starting-stat-points":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var startingStatPoints)) {
                    options = current;
                    error = $"'{value}' is not a valid starting stat-point amount.";
                    return false;
                }
                current = current with { StartingStatPoints = startingStatPoints };
                break;
            case "--test-participant":
                if (string.IsNullOrWhiteSpace(value) || !File.Exists(value)) {
                    options = current;
                    error = $"Test participant file '{value}' does not exist.";
                    return false;
                }
                current = current with { TestParticipantPath = Path.GetFullPath(value) };
                break;
            case "--starting-level":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var startingLevelId)) {
                    options = current;
                    error = $"'{value}' is not a valid starting level ID.";
                    return false;
                }
                current = current with { StartingLevelId = startingLevelId };
                break;
            case "--random-seed":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var randomSeed)) {
                    options = current;
                    error = $"'{value}' is not a valid 32-bit random seed.";
                    return false;
                }
                current = current with { RandomSeed = randomSeed };
                break;
            case "--game-mode":
                if (!TryParseGameMode(value, out var gameMode)) {
                    options = current;
                    error = $"'{value}' is not a valid game mode.";
                    return false;
                }
                current = current with { GameMode = gameMode };
                break;
            case "--difficulty":
                if (!TryParseDifficulty(value, out var difficulty)) {
                    options = current;
                    error = $"'{value}' is not a valid game difficulty.";
                    return false;
                }
                current = current with { Difficulty = difficulty };
                break;
            case "--procedural-monsters":
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var monsterLimit)
                    || monsterLimit < 0) {
                    options = current;
                    error = $"'{value}' is not a valid procedural monster limit.";
                    return false;
                }
                current = current with { ProceduralMonsterLimit = monsterLimit };
                break;
            case "--procedural-monster-level":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var monsterLevelId)) {
                    options = current;
                    error = $"'{value}' is not a valid procedural monster level ID.";
                    return false;
                }
                current = current with { ProceduralMonsterLevelId = monsterLevelId };
                break;
            case "--save-root":
                current = current with { SaveRoot = value };
                break;
            default:
                options = current;
                error = $"Unknown option '{argument}'.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(current.ContentRoot)
            || string.IsNullOrWhiteSpace(current.ContentId)
            || string.IsNullOrWhiteSpace(current.ContentVersion)
            || string.IsNullOrWhiteSpace(current.BuildId)
            || string.IsNullOrWhiteSpace(current.ProtocolSchemaVersion)) {
            options = current;
            error = "Content, build, and protocol identity values cannot be empty.";
            return false;
        }
        if (current.GameAdapterId is not null
            && string.IsNullOrWhiteSpace(current.GameAdapterId)) {
            options = current;
            error = "The game adapter ID cannot be empty.";
            return false;
        }
        if (current.ProceduralMonsterLevelId.HasValue
            && !current.ProceduralMonsterLimit.HasValue) {
            options = current;
            error = "A procedural monster level requires --procedural-monsters.";
            return false;
        }

        options = current;
        error = string.Empty;
        return true;
    }

    private static bool TryParseGameMode(
        string value,
        out AuthoritativeGameMode gameMode)
    {
        switch (value) {
        case "single-player":
            gameMode = AuthoritativeGameMode.SinglePlayer;
            return true;
        case "multiplayer":
            gameMode = AuthoritativeGameMode.Multiplayer;
            return true;
        default:
            gameMode = default;
            return false;
        }
    }

    private static bool TryParseDifficulty(
        string value,
        out AuthoritativeGameDifficulty difficulty)
    {
        switch (value) {
        case "normal":
            difficulty = AuthoritativeGameDifficulty.Normal;
            return true;
        case "nightmare":
            difficulty = AuthoritativeGameDifficulty.Nightmare;
            return true;
        case "hell":
            difficulty = AuthoritativeGameDifficulty.Hell;
            return true;
        default:
            difficulty = default;
            return false;
        }
    }

    public static string Usage => """
        Usage: Devilution.Server [options]

          --bind <address>             Bind address (default: 127.0.0.1)
          --port <port>                TCP port, 0 chooses an available port
          --content-root <directory>   External TSV content root
          --content-id <id>            Content pack identity
          --content-version <version>  Content pack version
          --content-overlay <directory> Ordered pack with content-pack.json; repeatable
          --module-package <directory> Hash-verified gameplay-module package; repeatable
          --game-adapter-package <directory> Hash-verified host-adapter package; repeatable
          --game-adapter <id>         Select a compatible host adapter factory
          --build-id <id>              Server build identity
          --protocol-version <version> Protocol schema version
          --tick-rate <hz>             Authoritative simulation rate
          --command-scheduling <mode>  immediate (default) or clock-gated; requires capable clients
          --starting-gold <amount>     Override content-defined initial wallet
          --starting-life <amount>     Override content-derived initial life (Q6)
          --starting-mana <amount>     Override content-derived initial mana (Q6)
          --starting-stat-points <n>   Seed server-owned attribute points for development starts
          --test-participant <file>    Acceptance only: start new participants from this definition
          --starting-level <id>        Override the content-defined initial level for new participants
          --random-seed <uint32>       Initial server/world seed (persisted in save root)
          --game-mode <mode>           single-player or multiplayer (default: multiplayer)
          --difficulty <level>         normal, nightmare, or hell (default: normal)
          --procedural-monsters <n>    Replace static encounters with at most n generated monsters
          --procedural-monster-level <id> Level for generated encounters (default: start level)
          --save-root <directory>      Server-owned authoritative save directory
          --persistence-mode <mode>    legacy (default) or cohort; cohort requires a dedicated root
          --performance-output <file> New diagnostic JSON file; use profile-start after warm-up, then quit
          --command-timing-trace <bool> Bounded command timing JSON lines on stdout (default: false)
        """;
}
