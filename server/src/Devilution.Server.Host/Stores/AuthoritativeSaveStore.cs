using System.Text;

namespace Devilution.Server.Stores;

/** Stores versioned authoritative save envelopes on the server filesystem. */
public sealed class AuthoritativeSaveStore
{
    private readonly string rootDirectory;
    private readonly object synchronization = new();

    public AuthoritativeSaveStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    public void Save(string sessionId, string serializedSave)
    {
        var path = GetPath(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedSave);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        lock (synchronization) {
            try {
                var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                    .GetBytes(serializedSave);
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
        lock (synchronization)
            return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public bool Delete(string sessionId)
    {
        var path = GetPath(sessionId);
        lock (synchronization) {
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
