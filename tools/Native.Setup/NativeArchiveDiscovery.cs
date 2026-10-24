using Godevilution.Tools.Archive;

namespace Godevilution.Tools.Setup;

/** Where a candidate archive path came from, in search order. */
public enum NativeArchiveOrigin
{
    Explicit,
    Configured,
    Environment,
    PreferencePath,
    GogRegistry,
    KnownInstall,
}

public sealed record NativeArchiveCandidate(string Path, NativeArchiveOrigin Origin);

/**
 * Finds the player's DIABDAT.MPQ. After the explicit, remembered and environment
 * paths it follows engine/assets.cpp GetMPQSearchPaths on Windows (the SDL
 * preference path and the GOG registry entry for game 1412601690), then the
 * usual install folders.
 */
public static class NativeArchiveDiscovery
{
    public const string EnvironmentVariable = "GODEVILUTION_DIABDAT";
    public const string ConfiguredPathFileName = "archive-path.txt";
    public const string GogGameId = "1412601690";
    private static readonly string[] ArchiveNames = ["DIABDAT.MPQ", "diabdat.mpq"];

    public sealed record Environment(
        string? ExplicitPath,
        string? ConfiguredPath,
        string? EnvironmentPath,
        string? ApplicationData,
        IReadOnlyList<string> GogRegistryPaths,
        IReadOnlyList<string> KnownInstallDirectories);

    public static Environment CurrentEnvironment(string? explicitPath, string setupRoot)
    {
        var configured = Path.Combine(setupRoot, ConfiguredPathFileName);
        var programFilesX86 = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
        var known = new List<string>();
        if (!string.IsNullOrEmpty(programFilesX86)) {
            known.Add(Path.Combine(programFilesX86, "GOG Galaxy", "Games", "Diablo"));
            known.Add(Path.Combine(programFilesX86, "Diablo"));
        }
        known.Add(Path.Combine(Path.GetPathRoot(System.Environment.SystemDirectory) ?? "C:\\", "GOG Games", "Diablo"));
        return new(explicitPath,
            File.Exists(configured) ? File.ReadAllText(configured).Trim() : null,
            System.Environment.GetEnvironmentVariable(EnvironmentVariable),
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            ReadGogRegistryPaths(), known);
    }

    /** Every candidate file in search order; directories expand to their DIABDAT.MPQ. */
    public static IReadOnlyList<NativeArchiveCandidate> Candidates(Environment environment)
    {
        var result = new List<NativeArchiveCandidate>();
        void AddFile(string? path, NativeArchiveOrigin origin)
        {
            if (!string.IsNullOrWhiteSpace(path))
                result.Add(new(path.Trim(), origin));
        }
        void AddDirectory(string? directory, NativeArchiveOrigin origin)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return;
            foreach (var name in ArchiveNames)
                result.Add(new(Path.Combine(directory.Trim(), name), origin));
        }
        AddFile(environment.ExplicitPath, NativeArchiveOrigin.Explicit);
        AddFile(environment.ConfiguredPath, NativeArchiveOrigin.Configured);
        AddFile(environment.EnvironmentPath, NativeArchiveOrigin.Environment);
        // paths::PrefPath(): SDL_GetPrefPath("diasurgical", "devilution").
        if (!string.IsNullOrEmpty(environment.ApplicationData))
            AddDirectory(Path.Combine(environment.ApplicationData, "diasurgical", "devilution"), NativeArchiveOrigin.PreferencePath);
        foreach (var gog in environment.GogRegistryPaths)
            AddDirectory(gog, NativeArchiveOrigin.GogRegistry);
        foreach (var known in environment.KnownInstallDirectories)
            AddDirectory(known, NativeArchiveOrigin.KnownInstall);
        return result;
    }

    /** The first candidate that opens as an MPQ containing Cathedral level data. */
    public static NativeArchiveCandidate? Find(Environment environment, Func<string, bool>? isUsable = null)
    {
        isUsable ??= IsUsableArchive;
        foreach (var candidate in Candidates(environment)) {
            if (File.Exists(candidate.Path) && isUsable(candidate.Path))
                return candidate with { Path = Path.GetFullPath(candidate.Path) };
            if (candidate.Origin is NativeArchiveOrigin.Explicit)
                throw new FileNotFoundException($"The supplied archive is missing or is not a Diablo DIABDAT.MPQ: {candidate.Path}");
        }
        return null;
    }

    public static bool IsUsableArchive(string path)
    {
        try {
            using var archive = MpqArchive.Open(path);
            return archive.Contains(NativeSourceGeometryExport.TilPath) && archive.Contains(NativeSourceFootprintExport.CursorCelPath);
        } catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) {
            return false;
        }
    }

    // fsg_get_gog_game_path: HKLM\SOFTWARE\(WOW6432Node\)GOG.com\Games\<id>, value "path".
    private static IReadOnlyList<string> ReadGogRegistryPaths()
    {
        if (!OperatingSystem.IsWindows())
            return [];
        var result = new List<string>();
        foreach (var key in new[] { $@"SOFTWARE\WOW6432Node\GOG.com\Games\{GogGameId}", $@"SOFTWARE\GOG.com\Games\{GogGameId}" }) {
            try {
                using var registry = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
                if (registry?.GetValue("path") is string path && !string.IsNullOrWhiteSpace(path))
                    result.Add(path);
            } catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException) {
            }
        }
        return result;
    }
}
