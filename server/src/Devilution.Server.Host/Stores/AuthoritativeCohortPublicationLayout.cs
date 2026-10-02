namespace Devilution.Server.Stores;

/**
 * Owns the explicit host-only first-publication layout. The marker is an
 * authorization for this managed layout, not an authority over checkpoint
 * contents; the canonical checkpoint remains the only recovery source.
 */
internal sealed class AuthoritativeCohortPublicationLayout
{
    internal const string InitializingMarkerFileName = ".authoritative-cohort.initializing";
    internal const string CommittedMarkerFileName = ".authoritative-cohort.committed";
    internal const string StagingDirectoryName = ".authoritative-staging";

    private static readonly string[] CanonicalNames = [
        AuthoritativeCohortStore.CheckpointFileName,
        AuthoritativeWorldSeedStore.FileName,
    ];

    private readonly string initializingMarkerPath;
    private readonly string committedMarkerPath;
    private bool firstPublicationPending;

    private AuthoritativeCohortPublicationLayout(
        string rootDirectory,
        bool firstPublicationPending)
    {
        RootDirectory = rootDirectory;
        StagingDirectory = Path.Combine(rootDirectory, StagingDirectoryName);
        initializingMarkerPath = Path.Combine(rootDirectory, InitializingMarkerFileName);
        committedMarkerPath = Path.Combine(rootDirectory, CommittedMarkerFileName);
        this.firstPublicationPending = firstPublicationPending;
    }

    internal string RootDirectory { get; }

    internal string StagingDirectory { get; }

    internal bool FirstPublicationPending => firstPublicationPending;

    internal bool IsCommitted => File.Exists(committedMarkerPath);

    internal bool StagingDirectoryExists
    {
        get {
            _ = TryGetAttributes(StagingDirectory, out var exists);
            return exists;
        }
    }

    /** Refuses a prepared committed layout whose canonical history disappeared. */
    internal void ValidateCanonicalForSave()
    {
        if (firstPublicationPending)
            return;
        ValidateMarkerPath(committedMarkerPath, expectedLength: 0);
        ValidateCanonicalForSave(RootDirectory, committedMarkerPath);
    }

    /** Seed initialization belongs only to a root that has no canonical history. */
    internal void ValidateSeedAccess()
    {
        ValidateCanonicalSibling(RootDirectory, AuthoritativeWorldSeedStore.FileName);
        ValidateCanonicalSibling(RootDirectory, AuthoritativeCohortStore.CheckpointFileName);
        _ = TryGetAttributes(Path.Combine(RootDirectory, AuthoritativeWorldSeedStore.FileName), out var seedExists);
        _ = TryGetAttributes(Path.Combine(RootDirectory, AuthoritativeCohortStore.CheckpointFileName), out var canonicalExists);
        if (!seedExists && (canonicalExists || IsCommitted))
            throw new InvalidDataException("The cohort root is missing its original world seed; no seed is regenerated.");
    }

    /**
     * Validates an existing root and prepares the host-owned first-publication
     * namespace. This is called only after the root lease has been acquired.
     */
    internal static AuthoritativeCohortPublicationLayout Prepare(
        string rootDirectory,
        bool canonicalExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        ValidateRootDirectory(root);

        var initializing = ValidateMarker(root, InitializingMarkerFileName);
        var committed = ValidateMarker(root, CommittedMarkerFileName);
        if (initializing && committed)
            throw new InvalidDataException(
                "The cohort publication layout contains both marker states.");

        var layout = new AuthoritativeCohortPublicationLayout(
            root,
            firstPublicationPending: !committed);

        if (canonicalExists) {
            // A valid canonical checkpoint remains authoritative even when
            // older or unrelated sibling files are present. Reserved marker
            // collisions were validated above before any lazy adoption.
            ValidateCanonicalSibling(root, AuthoritativeCohortStore.CheckpointFileName);
            ValidateCanonicalSibling(root, AuthoritativeWorldSeedStore.FileName);
            AuthoritativeWorldSeedStore.ValidateExisting(root);
            if (committed) {
                layout.ValidateStagingDirectory(requireExisting: false);
                layout.firstPublicationPending = false;
                return layout;
            }

            if (initializing)
                layout.ValidateStagingDirectory(requireExisting: false);
            else {
                // Validate an existing managed child before publishing the
                // adoption marker. A missing child is created only after the
                // committed marker records that a canonical has existed.
                if (layout.StagingDirectoryExists)
                    layout.ValidateStagingDirectory(requireExisting: true);
                layout.CreateCommittedMarkerForExistingCanonical();
                layout.ValidateStagingDirectory(requireExisting: false);
            }
            if (!initializing)
                layout.firstPublicationPending = false;
            return layout;
        }

        if (committed)
            throw new InvalidDataException(
                "The committed cohort publication marker has no canonical checkpoint.");

        ValidateNewRootEntries(root, initializing);
        if (!initializing)
            layout.CreateInitializingMarker();
        layout.ValidateStagingDirectory(requireExisting: false);
        return layout;
    }

    internal string CreateStagePath(string canonicalName)
    {
        if (!CanonicalNames.Contains(canonicalName, StringComparer.Ordinal))
            throw new ArgumentException(
                "The canonical name is not owned by the cohort publication layout.",
                nameof(canonicalName));
        ValidateStagingDirectory(requireExisting: true);
        return Path.Combine(
            StagingDirectory,
            canonicalName + "." + Guid.NewGuid().ToString("N") + ".tmp");
    }

    /** Atomically marks the first canonical publication as committed. */
    internal void Commit()
    {
        if (!firstPublicationPending)
            return;
        ValidateMarkerPath(initializingMarkerPath, expectedLength: 0);
        ValidateMarkerPath(committedMarkerPath, expectedLength: null, allowMissing: true);
        if (File.Exists(committedMarkerPath))
            throw new InvalidDataException(
                "The cohort publication layout changed while committing its marker.");
        File.Move(initializingMarkerPath, committedMarkerPath, overwrite: false);
        firstPublicationPending = false;
    }

    private void CreateInitializingMarker()
    {
        using var marker = new FileStream(
            initializingMarkerPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1,
            options: FileOptions.WriteThrough);
        marker.Flush(flushToDisk: true);
    }

    private void CreateCommittedMarkerForExistingCanonical()
    {
        using var marker = new FileStream(
            committedMarkerPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1,
            options: FileOptions.WriteThrough);
        marker.Flush(flushToDisk: true);
        ValidateStagingDirectory(requireExisting: false);
    }

    private void ValidateStagingDirectory(bool requireExisting)
    {
        var attributes = TryGetAttributes(StagingDirectory, out var exists);
        if (exists) {
            if (OperatingSystem.IsWindows())
                WindowsCheckpointStage.EnsureStageDirectory(RootDirectory, StagingDirectory);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint))
                != FileAttributes.Directory)
                throw new IOException("The cohort staging path is not a regular directory.");
        } else if (requireExisting) {
            throw new InvalidDataException(
                "The cohort publication marker has no managed staging directory.");
        } else {
            if (OperatingSystem.IsWindows())
                WindowsCheckpointStage.EnsureStageDirectory(RootDirectory, StagingDirectory);
            else
                Directory.CreateDirectory(StagingDirectory);
            attributes = TryGetAttributes(StagingDirectory, out exists);
            if (!exists || (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint))
                != FileAttributes.Directory)
                throw new IOException("The cohort staging directory could not be prepared.");
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(StagingDirectory)) {
            var name = Path.GetFileName(entry);
            if (!IsOwnedStageName(name))
                throw new InvalidDataException(
                    $"The cohort staging directory contains an unknown entry '{name}'.");
            var entryAttributes = TryGetAttributes(entry, out _);
            if ((entryAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint))
                != 0)
                throw new IOException(
                    $"The cohort staging entry '{name}' is not a regular file.");
        }
    }

    private static bool IsOwnedStageName(string name)
    {
        foreach (var canonicalName in CanonicalNames) {
            var prefix = canonicalName + ".";
            if (!name.StartsWith(prefix, StringComparison.Ordinal)
                || !name.EndsWith(".tmp", StringComparison.Ordinal))
                continue;
            var guidText = name[prefix.Length..^4];
            if (Guid.TryParseExact(guidText, "N", out var guid)
                && string.Equals(guidText, guid.ToString("N"), StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void ValidateNewRootEntries(string root, bool initializing)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root)) {
            var name = Path.GetFileName(entry);
            if ((name is "authoritative-cohort.lease"
                    or AuthoritativeWorldSeedStore.FileName)
                || initializing && (name is InitializingMarkerFileName
                    or CommittedMarkerFileName
                    or StagingDirectoryName))
                continue;
            throw new InvalidDataException(
                "The cohort root contains legacy or unknown files. Existing saves are preserved; an explicit migration policy or a fresh dedicated root is required.");
        }

        ValidateCanonicalSibling(root, AuthoritativeWorldSeedStore.FileName);
        AuthoritativeWorldSeedStore.ValidateExisting(root);
    }

    private static void ValidateCanonicalSibling(string root, string name)
    {
        var path = Path.Combine(root, name);
        var attributes = TryGetAttributes(path, out var exists);
        if (!exists)
            return;
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException($"The cohort canonical path '{name}' is not a regular file.");
    }

    private static void ValidateCanonicalForSave(string root, string markerPath)
    {
        var canonicalPath = Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName);
        var attributes = TryGetAttributes(canonicalPath, out var exists);
        if (!exists)
            throw new InvalidDataException(
                $"The committed cohort marker '{Path.GetFileName(markerPath)}' has no canonical checkpoint.");
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException("The committed cohort canonical path is not a regular file.");
    }

    private static bool ValidateMarker(string root, string name)
    {
        var path = Path.Combine(root, name);
        var attributes = TryGetAttributes(path, out var exists);
        if (!exists)
            return false;
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException($"The cohort marker '{name}' is not a regular file.");
        ValidateMarkerPath(path, expectedLength: 0);
        return true;
    }

    private static void ValidateMarkerPath(
        string path,
        long? expectedLength,
        bool allowMissing = false)
    {
        var attributes = TryGetAttributes(path, out var exists);
        if (!exists) {
            if (allowMissing)
                return;
            throw new InvalidDataException("The cohort publication marker is missing.");
        }
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException("The cohort publication marker is not a regular file.");
        if (expectedLength is long length && new FileInfo(path).Length != length)
            throw new InvalidDataException("The cohort publication marker is malformed.");
    }

    private static void ValidateRootDirectory(string root)
    {
        var attributes = TryGetAttributes(root, out var exists);
        if (!exists || (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint))
            != FileAttributes.Directory)
            throw new IOException("The cohort root must be a regular directory.");
    }

    private static FileAttributes TryGetAttributes(string path, out bool exists)
    {
        try {
            exists = true;
            return File.GetAttributes(path);
        } catch (FileNotFoundException) {
            exists = false;
            return 0;
        } catch (DirectoryNotFoundException) {
            exists = false;
            return 0;
        }
    }
}
