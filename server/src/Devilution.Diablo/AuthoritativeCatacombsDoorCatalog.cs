using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Devilution.Diablo;

/** One native L2 door type pair admitted by the Catacombs population profile. */
public sealed record AuthoritativeCatacombsDoorDefinition(
    uint Depth, uint LeftNativeObjectId, uint RightNativeObjectId);

/** Family-scoped binding for native Catacombs door object types 42 and 43. */
public sealed class AuthoritativeCatacombsDoorCatalog
{
    public const string ProfileId = "original-diablo.catacombs-l2-doors";
    public const int Revision = 1;
    public const uint LeftNativeObjectId = 42;
    public const uint RightNativeObjectId = 43;

    private readonly IReadOnlyList<AuthoritativeCatacombsDoorDefinition> definitions;

    private AuthoritativeCatacombsDoorCatalog(
        AuthoritativeCatacombsPopulationMetadata metadata,
        IReadOnlyList<AuthoritativeCatacombsDoorDefinition> values)
    {
        PopulationManifestHash = metadata.ManifestHash;
        PopulationFingerprint = metadata.Fingerprint;
        definitions = Array.AsReadOnly(values.ToArray());
        var canonical = string.Join("\n", new[] {
            ProfileId,
            Revision.ToString(CultureInfo.InvariantCulture),
            PopulationManifestHash,
            PopulationFingerprint,
        }.Concat(definitions.Select(value => string.Create(CultureInfo.InvariantCulture,
            $"{value.Depth}:{value.LeftNativeObjectId}:{value.RightNativeObjectId}"))));
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public IReadOnlyList<AuthoritativeCatacombsDoorDefinition> Definitions => definitions;
    public string PopulationManifestHash { get; }
    public string PopulationFingerprint { get; }
    public string Fingerprint { get; }

    public AuthoritativeCatacombsDoorDefinition ForDepth(uint depth)
        => definitions.FirstOrDefault(value => value.Depth == depth)
            ?? throw new InvalidDataException($"Catacombs door depth {depth} is not admitted.");

    public bool Supports(AuthoritativeCatacombsPopulationMetadata metadata)
        => metadata is not null
            && string.Equals(PopulationManifestHash, metadata.ManifestHash, StringComparison.Ordinal)
            && string.Equals(PopulationFingerprint, metadata.Fingerprint, StringComparison.Ordinal);

    /** Build this catalog only from the separately validated C2 population metadata. */
    public static AuthoritativeCatacombsDoorCatalog Load(
        AuthoritativeCatacombsPopulationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (!metadata.DepthRules.Select(rule => rule.Depth).SequenceEqual([5, 6, 7, 8]))
            throw new InvalidDataException("Catacombs doors require the ordered native depths five through eight.");

        ValidateNativeDefinition(metadata.Population.Objects, LeftNativeObjectId);
        ValidateNativeDefinition(metadata.Population.Objects, RightNativeObjectId);
        var definitions = Enumerable.Range(5, 4)
            .Select(depth => new AuthoritativeCatacombsDoorDefinition(
                (uint)depth, LeftNativeObjectId, RightNativeObjectId))
            .ToArray();
        return new AuthoritativeCatacombsDoorCatalog(metadata, definitions);
    }

    private static void ValidateNativeDefinition(
        IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objects,
        uint nativeObjectId)
    {
        if (nativeObjectId >= objects.Count)
            throw new InvalidDataException("Catacombs door type is outside the bound native object table.");
        var value = objects[(int)nativeObjectId];
        if (value.NativeType != (int)nativeObjectId
            || value.Flags != 24
            || value.AnimDelay != (nativeObjectId == LeftNativeObjectId ? 1 : 2)
            || value.AnimLength != 0
            || value.AnimWidth != 64
            || value.SelectionRegion != 3)
            throw new InvalidDataException($"Native Catacombs door object {nativeObjectId} does not match its canonical record.");
    }
}
