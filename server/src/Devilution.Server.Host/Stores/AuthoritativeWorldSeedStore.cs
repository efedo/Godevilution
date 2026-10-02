using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

namespace Devilution.Server.Stores;

/** Persists the server-owned world seed used for deterministic generation. */
public sealed class AuthoritativeWorldSeedStore
{
    public const string FileName = "world-seed.txt";

    private static readonly ConcurrentDictionary<string, object> PathLocks = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly string rootDirectory;
    private readonly AuthoritativeCohortPublicationLayout? publicationLayout;
    private readonly AuthoritativeCohortStore? cohortOwner;

    public AuthoritativeWorldSeedStore(string rootDirectory)
        : this(rootDirectory, null, null)
    {
    }

    internal AuthoritativeWorldSeedStore(
        string rootDirectory,
        AuthoritativeCohortPublicationLayout? publicationLayout,
        AuthoritativeCohortStore? cohortOwner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        this.publicationLayout = publicationLayout;
        this.cohortOwner = cohortOwner;
        if ((publicationLayout is null) != (cohortOwner is null))
            throw new ArgumentException("A cohort seed store requires its originating lease owner.");
        Directory.CreateDirectory(this.rootDirectory);
    }

    /** Observer used only by the owned managed interruption fixture. */
    internal Action<AuthoritativePublicationPhase, string>? PublicationObserver { get; set; }

    /** Validates an already-present seed without creating or replacing it. */
    internal static void ValidateExisting(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var path = Path.Combine(Path.GetFullPath(rootDirectory), FileName);
        if (!File.Exists(path))
            return;
        _ = Parse(StrictUtf8Text.ReadAllText(path, "world seed"));
    }

    /** Loads the existing seed or creates it once for a new save root. */
    public uint LoadOrCreate(uint? requestedSeed = null)
        => cohortOwner is null
            ? LoadOrCreateCore(requestedSeed)
            : cohortOwner.ExecuteSeedOperation(publicationLayout!, () => LoadOrCreateCore(requestedSeed));

    private uint LoadOrCreateCore(uint? requestedSeed)
    {
        var path = Path.Combine(rootDirectory, FileName);
        var pathLock = PathLocks.GetOrAdd(path, static _ => new object());
        lock (pathLock) {
            if (File.Exists(path)) {
                return ReadExisting(path, requestedSeed);
            }

            var seed = requestedSeed
                ?? BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
            var temporaryPath = publicationLayout?.CreateStagePath(FileName)
                ?? path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var temporaryOwned = false;
            Exception? primaryFailure = null;
            var observer = PublicationObserver;
            var renameAttempted = false;
            var canonicalPublished = false;
            WindowsCheckpointStage? secureStage = null;
            try {
                var bytes = StrictUtf8Text.Encode(
                    seed.ToString(CultureInfo.InvariantCulture),
                    "world seed");
                if (publicationLayout is not null && OperatingSystem.IsWindows()) {
                    secureStage = WindowsCheckpointStage.Create(
                        temporaryPath, path, Path.GetDirectoryName(temporaryPath)!);
                    AuthoritativePublicationWriter.Write(secureStage.Stream, bytes, temporaryPath, observer);
                    secureStage.FinalizeMetadataAndClose();
                } else {
                    using var stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 4096,
                        options: FileOptions.WriteThrough);
                    temporaryOwned = true;
                    AuthoritativePublicationWriter.Write(stream, bytes, temporaryPath, observer);
                }
                observer?.Invoke(
                    AuthoritativePublicationPhase.AfterFlushBeforeRename,
                    temporaryPath);
                renameAttempted = true;
                File.Move(temporaryPath, path, overwrite: false);
                canonicalPublished = true;
                // The owned resource is the created file, not its old pathname.
                // Release cleanup ownership before any observer can recreate
                // that pathname after the canonical rename.
                if (OperatingSystem.IsWindows())
                    secureStage?.MarkPublished();
                temporaryOwned = false;
                observer?.Invoke(
                    AuthoritativePublicationPhase.AfterCanonicalRename,
                    path);
            } catch (IOException) when (
                renameAttempted && !canonicalPublished && File.Exists(path)) {
                // Another process won first initialization between our existence
                // check and the atomic move. Its seed is now authoritative.
                return ReadExisting(path, requestedSeed);
            } catch (Exception exception) {
                primaryFailure = exception;
                throw;
            } finally {
                try {
                    if (OperatingSystem.IsWindows())
                        secureStage?.Dispose();
                    if (temporaryOwned && File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                } catch (Exception cleanupFailure) when (primaryFailure is not null) {
                    // Preserve the seed publication failure that explains the
                    // failed initialization; cleanup cannot mask it.
                    primaryFailure.Data["SeedStageCleanupError"] = cleanupFailure;
                }
            }
            return seed;
        }
    }

    private static uint ReadExisting(string path, uint? requestedSeed)
    {
        var persistedSeed = Parse(StrictUtf8Text.ReadAllText(path, "world seed"));
        if (requestedSeed is uint explicitSeed && explicitSeed != persistedSeed) {
            throw new InvalidDataException(
                $"The configured world seed {explicitSeed} does not match "
                + $"the persisted world seed {persistedSeed}.");
        }
        return persistedSeed;
    }

    private static uint Parse(string contents)
    {
        if (!uint.TryParse(
                contents.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var seed)) {
            throw new InvalidDataException(
                $"The persisted world seed is not a valid UInt32: '{contents}'.");
        }
        return seed;
    }
}
