namespace Devilution.Data;

/** Immutable binary asset carried by a validated content pack. */
public sealed class ContentAsset
{
    public ContentAsset(string sourcePath, ReadOnlyMemory<byte> contents)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("An asset source path is required.", nameof(sourcePath));
        var normalizedPath = sourcePath.Replace('\\', '/');
        if (Path.IsPathRooted(normalizedPath)
            || normalizedPath.Contains(':', StringComparison.Ordinal)
            || normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or "..")) {
            throw new ArgumentException(
                "Asset source paths must be relative and must not traverse directories.",
                nameof(sourcePath));
        }
        SourcePath = normalizedPath;
        Contents = contents.ToArray();
    }

    public string SourcePath { get; }

    /** Defensive immutable copy of the asset bytes. */
    public ReadOnlyMemory<byte> Contents { get; }
}
