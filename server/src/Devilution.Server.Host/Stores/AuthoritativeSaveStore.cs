using System.Collections.Concurrent;

namespace Devilution.Server.Stores;

/** Stores versioned authoritative save envelopes on the server filesystem. */
public sealed class AuthoritativeSaveStore
{
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly string rootDirectory;

    public AuthoritativeSaveStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    /** Root directory shared by related server-owned persistence stores. */
    public string RootDirectory => rootDirectory;

    public void Save(string sessionId, string serializedSave)
    {
        var path = GetPath(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedSave);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var pathLock = PathLocks.GetOrAdd(path, static _ => new object());
        lock (pathLock) {
            try {
                var bytes = StrictUtf8Text.Encode(serializedSave, "authoritative save");
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.WriteThrough)) {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, path, overwrite: true);
            } finally {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }

    public string? Load(string sessionId)
    {
        var path = GetPath(sessionId);
        var pathLock = PathLocks.GetOrAdd(path, static _ => new object());
        lock (pathLock)
            return File.Exists(path)
                ? StrictUtf8Text.ReadAllText(path, "authoritative save")
                : null;
    }

    public bool Delete(string sessionId)
    {
        var path = GetPath(sessionId);
        var pathLock = PathLocks.GetOrAdd(path, static _ => new object());
        lock (pathLock) {
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
    }

    private string GetPath(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (sessionId.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
            throw new ArgumentException("Save session IDs may contain only letters, digits, '-' and '_'.", nameof(sessionId));
        return Path.Combine(rootDirectory, sessionId + ".json");
    }
}
