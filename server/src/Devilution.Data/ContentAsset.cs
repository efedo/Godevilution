namespace Devilution.Data;

/** Immutable binary asset carried by a validated content pack. */
public sealed class ContentAsset
{
    public ContentAsset(string sourcePath, ReadOnlyMemory<byte> contents)
    {
        var normalizedPath = ContentPath.NormalizeRelative(sourcePath, "asset");
        SourcePath = normalizedPath;
        Contents = contents.ToArray();
    }

    public string SourcePath { get; }

    /** Defensive immutable copy of the asset bytes. */
    public ReadOnlyMemory<byte> Contents { get; }
}
