namespace Devilution.Client.Protocol;

/** Stores only a local resume credential; authoritative saves remain on the host. */
public static class LocalSessionBookmark
{
    public static string? Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            return null;
        var token = File.ReadAllText(path).Trim();
        Validate(token);
        return token;
    }

    public static void Write(string path, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Validate(token);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
                stream.Write(System.Text.Encoding.UTF8.GetBytes(token));
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        } finally {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512
            || token.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
            throw new InvalidDataException("The local resume bookmark is invalid; it has not been overwritten.");
    }
}
