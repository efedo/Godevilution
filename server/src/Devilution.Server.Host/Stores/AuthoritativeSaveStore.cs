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
        var temporaryPath = path + ".tmp";
        lock (synchronization) {
            File.WriteAllText(temporaryPath, serializedSave, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
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
