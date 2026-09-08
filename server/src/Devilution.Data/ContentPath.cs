namespace Devilution.Data;

/** Canonical relative paths used by tables, overlays, and binary pack assets. */
internal static class ContentPath
{
    public static string NormalizeRelative(string sourcePath, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException($"A {resourceName} source path is required.", nameof(sourcePath));

        var normalizedPath = sourcePath.Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.None);
        if (Path.IsPathRooted(normalizedPath)
            || normalizedPath.Contains(':', StringComparison.Ordinal)
            || normalizedPath.StartsWith("/", StringComparison.Ordinal)
            || normalizedPath.EndsWith("/", StringComparison.Ordinal)
            || normalizedPath.Contains("//", StringComparison.Ordinal)
            || segments.Any(segment => segment is "" or "." or "..")) {
            throw new ArgumentException(
                $"{resourceName} source paths must be canonical relative paths without traversal segments.",
                nameof(sourcePath));
        }

        return normalizedPath;
    }
}
