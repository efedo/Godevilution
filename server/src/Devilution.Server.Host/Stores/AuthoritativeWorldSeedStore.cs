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

    public AuthoritativeWorldSeedStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    /** Loads the existing seed or creates it once for a new save root. */
    public uint LoadOrCreate(uint? requestedSeed = null)
    {
        var path = Path.Combine(rootDirectory, FileName);
        var pathLock = PathLocks.GetOrAdd(path, static _ => new object());
        lock (pathLock) {
            if (File.Exists(path)) {
                return ReadExisting(path, requestedSeed);
            }

            var seed = requestedSeed
                ?? BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                var bytes = StrictUtf8Text.Encode(
                    seed.ToString(CultureInfo.InvariantCulture),
                    "world seed");
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
                File.Move(temporaryPath, path, overwrite: false);
            } catch (IOException) when (File.Exists(path)) {
                // Another process won first initialization between our existence
                // check and the atomic move. Its seed is now authoritative.
                return ReadExisting(path, requestedSeed);
            } finally {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
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
