namespace Godevilution.Tools.Archive;

/**
 * Original game files read straight from the player's MPQ, addressed by the
 * relative '/' paths importers use for an extracted tree (MPQ names use '\').
 * An optional DevilutionX assets directory is layered in as native's asset
 * search does (for example its plrgfx .trn fixes); no asset shadows a DIABDAT file.
 */
public sealed class NativeArchiveSource(MpqArchive archive, string? assetsDirectory = null)
{
    public MpqArchive Archive { get; } = archive ?? throw new ArgumentNullException(nameof(archive));

    public string? AssetsDirectory { get; } = assetsDirectory is null ? null : Path.GetFullPath(assetsDirectory);

    /** Validates an importer-relative path and returns its archive name. */
    public static string ToArchiveName(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var segments = relativePath.Replace('\\', '/').Split('/');
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException($"Archive source path '{relativePath}' is unsafe.");
        return string.Join('\\', segments);
    }

    public bool Exists(string relativePath) => AssetPath(relativePath) is not null || Archive.Contains(ToArchiveName(relativePath));

    public long Length(string relativePath)
        => AssetPath(relativePath) is { } asset ? new FileInfo(asset).Length : Archive.GetFileSize(ToArchiveName(relativePath));

    public byte[] Read(string relativePath)
        => AssetPath(relativePath) is { } asset ? File.ReadAllBytes(asset) : Archive.ReadFile(ToArchiveName(relativePath));

    /** Reads a file whose size is between one byte and the importer's bound. */
    public byte[] ReadBounded(string relativePath, long maximumBytes, string description)
    {
        var name = ToArchiveName(relativePath);
        var length = Exists(relativePath) ? Length(relativePath) : 0;
        if (length < 1 || length > maximumBytes)
            throw new InvalidDataException($"{description} '{name}' is missing from the archive, empty, or exceeds {maximumBytes} bytes.");
        return Read(relativePath);
    }

    private string? AssetPath(string relativePath)
    {
        if (AssetsDirectory is null)
            return null;
        var path = Path.Combine(AssetsDirectory, Path.Combine(ToArchiveName(relativePath).Split('\\')));
        return File.Exists(path) ? path : null;
    }
}
