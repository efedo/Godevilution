namespace Devilution.Client.Protocol.Tests;

/** Exact registered cleanup target within a GUID-owned test fixture, never a broad user directory. */
internal sealed class LiveTestSaveRoot
{
    public string Path { get; }

    private LiveTestSaveRoot(string path) => Path = path;

    public static LiveTestSaveRoot Register(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (File.Exists(fullPath))
            throw new InvalidOperationException("A live-test save-root cleanup target must be a directory.");
        var current = new DirectoryInfo(fullPath);
        var fixtureFound = false;
        for (var directory = current; directory is not null; directory = directory.Parent) {
            var name = directory.Name;
            if (name.StartsWith("Godevilution-", StringComparison.Ordinal)
                && name.Length > 32 && Guid.TryParseExact(name[^32..], "N", out _))
                fixtureFound = true;
        }
        if (!fixtureFound)
            throw new InvalidOperationException("Live-test cleanup requires an explicitly registered GUID-owned Godevilution fixture subtree.");
        AssertRegularAncestry(current);
        return new LiveTestSaveRoot(fullPath);
    }

    public void Delete()
    {
        if (File.Exists(Path))
            throw new InvalidOperationException("The registered live-test save root was replaced by a file.");
        var root = new DirectoryInfo(Path);
        AssertRegularAncestry(root);
        if (!root.Exists)
            return;
        // Inspect one level at a time; never recurse through a reparse entry.
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);
        while (pending.TryPop(out var directory)) {
            foreach (var entry in directory.EnumerateFileSystemInfos()) {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Live-test cleanup refuses a reparse entry: " + entry.FullName);
                if (entry is DirectoryInfo child)
                    pending.Push(child);
            }
        }
        Directory.Delete(Path, recursive: true);
    }

    private static void AssertRegularAncestry(DirectoryInfo directory)
    {
        for (var current = directory; current is not null; current = current.Parent) {
            if ((current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                || current.LinkTarget is not null)
                throw new InvalidOperationException("Live-test cleanup refuses a reparse ancestor: " + current.FullName);
        }
    }
}
