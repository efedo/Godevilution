using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Devilution.Server.Stores;

/** Persists the server-owned world seed used for deterministic generation. */
public sealed class AuthoritativeWorldSeedStore
{
    public const string FileName = "world-seed.txt";

    private readonly string rootDirectory;
    private readonly object synchronization = new();

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
        lock (synchronization) {
            if (File.Exists(path)) {
                var persistedSeed = Parse(File.ReadAllText(path));
                if (requestedSeed is uint explicitSeed && explicitSeed != persistedSeed) {
                    throw new InvalidDataException(
                        $"The configured world seed {explicitSeed} does not match "
                        + $"the persisted world seed {persistedSeed}.");
                }
                return persistedSeed;
            }

            var seed = requestedSeed
                ?? BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
            var temporaryPath = path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                seed.ToString(CultureInfo.InvariantCulture),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
            return seed;
        }
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
