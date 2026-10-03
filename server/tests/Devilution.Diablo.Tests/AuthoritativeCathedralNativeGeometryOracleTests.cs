using System.Text.Json;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Optional cross-runtime comparison against a native local-asset export.
 *
 * The test is explicit because the native export reads licensed local GOG
 * assets. Normal managed tests remain archive-free and deterministic.
 */
public sealed class AuthoritativeCathedralNativeGeometryOracleTests
{
    private const string OracleDirectoryVariable = "GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY";
    private const string DepthOracleDirectoryVariable = "GODEVILUTION_CATHEDRAL_DEPTH_ORACLE_DIRECTORY";
    private const string OracleFormat = "godevilution-cathedral-fine-oracle";
    private const string DepthOracleFormat = "godevilution-cathedral-depth-fine-oracle";
    private const string DepthOraclePolicy = "quest-disabled-original-diablo";
    private const int OracleVersion = 1;
    private const int CoarseWidth = 40;
    private const int CoarseHeight = 40;
    private const int NativeWidth = 112;
    private const int NativeHeight = 112;
    private const int NativeTileCount = AuthoritativeDunTilePropertyCatalog.NativeTileCount;
    private const byte BlockLight = (byte)AuthoritativeDunTileFlags.BlockLight;
    private const byte NativeOpaqueSolBit = 0x40;
    private const byte SupportedSolMask = 0xBF;

    [Fact]
    public void SolOracleBoundaryNormalizationPreservesRawBytesAndZeroTail()
    {
        var raw = new byte[] { 0x41, 0x40, 0xBF };
        var original = raw.ToArray();
        var padded = PreparePaddedSol(raw);
        var comparison = StripNativeOpaqueSolBit(padded);

        Assert.Equal(original, raw);
        Assert.Equal(NativeTileCount, padded.Length);
        Assert.Equal((byte)0x41, padded[0]);
        Assert.Equal((byte)0x40, padded[1]);
        Assert.Equal((byte)0xBF, padded[2]);
        Assert.Equal((byte)0x01, comparison[0]);
        Assert.Equal((byte)0x00, comparison[1]);
        Assert.Equal((byte)0xBF, comparison[2]);
        Assert.All(comparison.Skip(3), value => Assert.Equal((byte)0, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(NativeTileCount + 1)]
    public void SolOracleBoundaryNormalizationRejectsInvalidRawLengths(int length)
    {
        Assert.Throws<InvalidDataException>(() => PreparePaddedSol(new byte[length]));
    }

    [Theory(Explicit = true)]
    [InlineData("cathedral-2588-main.json", 2588U, "main")]
    [InlineData("cathedral-2588-previous.json", 2588U, "previous")]
    [InlineData("cathedral-743271966-main.json", 743271966U, "main")]
    [InlineData("cathedral-743271966-previous.json", 743271966U, "previous")]
    [InlineData("cathedral-0-main.json", 0U, "main")]
    [InlineData("cathedral-0-previous.json", 0U, "previous")]
    [InlineData("cathedral-1-main.json", 1U, "main")]
    [InlineData("cathedral-1-previous.json", 1U, "previous")]
    [InlineData("cathedral-2147483648-main.json", 2147483648U, "main")]
    [InlineData("cathedral-2147483648-previous.json", 2147483648U, "previous")]
    [InlineData("cathedral-4294967295-main.json", 4294967295U, "main")]
    [InlineData("cathedral-4294967295-previous.json", 4294967295U, "previous")]
    public void NativeLocalAssetFineGeometryMatchesManagedMaterializer(
        string snapshotFileName,
        uint seed,
        string entryName)
        => CompareNativeFineGeometry(snapshotFileName, seed, entryName);

    public static TheoryData<int, uint, string> CathedralDepthCases
    {
        get {
            var cases = new TheoryData<int, uint, string>();
            foreach (var depth in Enumerable.Range(1, 4)) {
                foreach (var seed in new uint[] { 2588, 743271966, 0, 1, 0x80000000, uint.MaxValue }) {
                    foreach (var entry in new[] { "main", "previous" })
                        cases.Add(depth, seed, entry);
                }
            }
            return cases;
        }
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(CathedralDepthCases))]
    public void NativeLocalAssetDepthFineGeometryMatchesManagedMaterializer(int depth, uint seed, string entryName)
        => CompareNativeFineGeometry($"cathedral-depth-{depth}-{seed}-{entryName}.json", seed, entryName, depth);

    private static void CompareNativeFineGeometry(
        string snapshotFileName,
        uint seed,
        string entryName,
        int? depth = null)
    {
        var directoryVariable = depth.HasValue ? DepthOracleDirectoryVariable : OracleDirectoryVariable;
        var root = Environment.GetEnvironmentVariable(directoryVariable);
        Assert.False(string.IsNullOrWhiteSpace(root),
            $"{directoryVariable} must point to the native oracle directory.");
        Assert.True(Directory.Exists(root), $"Native oracle directory does not exist: {root}");

        var tilBytes = ReadRequiredFile(root!, "l1.til");
        Assert.Equal(206 * sizeof(ushort) * 4, tilBytes.Length);
        var solBytes = ReadRequiredFile(root!, "l1.sol");
        Assert.InRange(solBytes.Length, 1, NativeTileCount);

        var paddedSol = PreparePaddedSol(solBytes);
        // Native l1.sol contains an opaque 0x40 bit at d-piece 170 which the
        // strict managed LoadSol contract intentionally rejects. Preserve the
        // raw asset and strip only this comparison-boundary bit. The native
        // opaque-bit channel is checked separately below; this is not a claim
        // of full opaque-byte parity.
        var comparisonSol = StripNativeOpaqueSolBit(paddedSol);
        var pieces = AuthoritativeDunPieceCatalog.LoadTil("oracle:l1.til", tilBytes);
        var properties = AuthoritativeDunTilePropertyCatalog.LoadSol(
            "oracle:l1.sol (zero-extended and opaque-bit stripped for comparison)",
            comparisonSol);

        var snapshot = ReadSnapshot(root!, snapshotFileName);
        Assert.Equal(depth.HasValue ? DepthOracleFormat : OracleFormat, snapshot.Format);
        Assert.Equal(OracleVersion, snapshot.Version);
        Assert.Equal(depth ?? 1, snapshot.Level);
        if (depth.HasValue) {
            Assert.Equal(DepthOraclePolicy, snapshot.Policy);
            Assert.Equal(80, snapshot.CoarseTransparencyWidth);
            Assert.Equal(80, snapshot.CoarseTransparencyHeight);
            Assert.Equal(80 * 80, snapshot.CoarseTransparency.Length);
        }
        Assert.Equal(seed, snapshot.InputSeed);
        Assert.Equal(entryName, snapshot.Entry);
        Assert.Equal(CoarseWidth, snapshot.CoarseWidth);
        Assert.Equal(CoarseHeight, snapshot.CoarseHeight);
        Assert.Equal(NativeWidth, snapshot.NativeWidth);
        Assert.Equal(NativeHeight, snapshot.NativeHeight);
        Assert.Equal(NativeTileCount, snapshot.SolCount);
        Assert.Equal(CoarseWidth * CoarseHeight, snapshot.MegaTiles.Length);
        Assert.Equal(NativeWidth * NativeHeight, snapshot.Pieces.Length);
        Assert.Equal(NativeWidth * NativeHeight, snapshot.Transparency.Length);
        Assert.Equal(NativeTileCount, snapshot.SolProperties.Length);
        Assert.Equal(2, snapshot.EntryPosition.Length);
        Assert.Equal(2, snapshot.StairsUp.Length);
        Assert.Equal(2, snapshot.StairsDown.Length);

        var entry = entryName switch {
            "main" => AuthoritativeCathedralEntry.Main,
            "previous" => AuthoritativeCathedralEntry.Previous,
            _ => throw new InvalidDataException($"Unknown oracle entry '{entryName}'."),
        };
        var depthResult = depth.HasValue
            ? AuthoritativeCathedralDepthGenerator.Generate(depth.Value, seed, entry)
            : null;
        // Normalize only the comparison view; deeper production generation and
        // materialization use their depth-aware APIs, not the level-1 facade.
        var result = depthResult is null
            ? AuthoritativeCathedralLevel1Generator.Generate(seed, entry)
            : new AuthoritativeCathedralLevel1Result(
                depthResult.InputSeed, depthResult.AttemptSeed, depthResult.FinalRandomState,
                depthResult.MegaTiles, depthResult.NativeTransparency,
                depthResult.StairsUp, depthResult.StairsDown, depthResult.EntryPosition);
        if (depthResult is not null) {
            Assert.Equal(depth, depthResult.Depth);
            Assert.Equal(AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs, depthResult.Policy);
            Assert.Equal(depthResult.NativeTransparency, snapshot.CoarseTransparency);
            Assert.Equal(HashBytes(snapshot.CoarseTransparency), snapshot.Hashes.CoarseTransparency);
        }
        Assert.Equal(result.AttemptSeed, snapshot.AttemptSeed);
        Assert.Equal(result.FinalRandomState, snapshot.FinalRandomState);
        Assert.Equal(result.EntryPosition.X, snapshot.EntryPosition[0]);
        Assert.Equal(result.EntryPosition.Y, snapshot.EntryPosition[1]);
        Assert.Equal(result.StairsUp.X, snapshot.StairsUp[0]);
        Assert.Equal(result.StairsUp.Y, snapshot.StairsUp[1]);
        Assert.Equal(result.StairsDown.X, snapshot.StairsDown[0]);
        Assert.Equal(result.StairsDown.Y, snapshot.StairsDown[1]);
        Assert.Equal(result.MegaTiles, snapshot.MegaTiles);

        var level = depthResult is null ? AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            result,
            pieces,
            properties,
            stableSymbol: "cathedral",
            isDefaultStart: false).Level : AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
                checked((uint)depthResult.Depth),
                depthResult,
                pieces,
                properties,
                stableSymbol: $"cathedral_depth_{depthResult.Depth}",
                isDefaultStart: false).Level;
        Assert.Equal(snapshot.Pieces, level.NativePieceLayer);
        Assert.Equal(snapshot.Transparency, level.NativeTransparency);

        var effectiveProperties = new ushort[NativeTileCount];
        for (var pieceId = 0; pieceId < NativeTileCount; pieceId++) {
            Assert.True(level.NativePieceProperties.TryGetValue((ushort)pieceId, out var flags));
            effectiveProperties[pieceId] = (ushort)flags;

            Assert.Equal(
                (byte)(paddedSol[pieceId] & NativeOpaqueSolBit),
                (byte)(snapshot.SolProperties[pieceId] & NativeOpaqueSolBit));
            Assert.Equal(
                0,
                snapshot.SolProperties[pieceId] & ~(SupportedSolMask | NativeOpaqueSolBit));
        }
        var expectedSemanticProperties = snapshot.SolProperties
            .Select(value => checked((ushort)(value & SupportedSolMask)))
            .ToArray();
        Assert.Equal(expectedSemanticProperties, effectiveProperties);
        Assert.Equal(snapshot.SolidCells, level.BlockedCells);
        Assert.Equal(snapshot.MissileBlockedCells, level.MissileBlockedCells);
        Assert.Equal(snapshot.SolidCells, level.NativePieceCollisionCells);
        Assert.Equal(snapshot.MissileBlockedCells, level.NativePieceMissileBlockedCells);

        var explicitLightCells = snapshot.Pieces
            .Select((piece, cell) => (piece, cell))
            .Where(value => (effectiveProperties[value.piece] & BlockLight) != 0)
            .Select(value => value.cell)
            .ToArray();
        Assert.Equal(snapshot.BlockLightCells, explicitLightCells);

        // AuthoritativeLevel deliberately unions solid movement blockers into
        // vision. Compare that conservative projection separately from the
        // native explicit BlockLight list above; this does not claim exact
        // native vision parity for the shared protocol projection.
        var conservativeVision = snapshot.SolidCells
            .Concat(snapshot.BlockLightCells)
            .Distinct()
            .OrderBy(cell => cell)
            .ToArray();
        Assert.Equal(conservativeVision, level.NativeVisionBlockedCells);
        Assert.True(level.HasExplicitNativeVisionBlockedCells);
        Assert.Equal(snapshot.EntryPosition[0], level.StartPositionX);
        Assert.Equal(snapshot.EntryPosition[1], level.StartPositionY);

        Assert.Equal(Hash(snapshot.MegaTiles), snapshot.Hashes.MegaTiles);
        Assert.Equal(Hash(snapshot.Pieces), snapshot.Hashes.Pieces);
        Assert.Equal(HashBytes(snapshot.Transparency), snapshot.Hashes.Transparency);
        Assert.Equal(HashBytes(snapshot.SolProperties), snapshot.Hashes.SolProperties);
        Assert.Equal(Hash(snapshot.SolidCells), snapshot.Hashes.SolidCells);
        Assert.Equal(Hash(snapshot.MissileBlockedCells), snapshot.Hashes.MissileBlockedCells);
        Assert.Equal(Hash(snapshot.BlockLightCells), snapshot.Hashes.BlockLightCells);
    }

    internal static byte[] PreparePaddedSol(byte[] raw)
    {
        if (raw.Length is < 1 or > NativeTileCount)
            throw new InvalidDataException(
                $"The raw l1.sol length must be in the range 1..{NativeTileCount}, got {raw.Length}.");

        var padded = new byte[NativeTileCount];
        Buffer.BlockCopy(raw, 0, padded, 0, raw.Length);
        return padded;
    }

    internal static byte[] StripNativeOpaqueSolBit(byte[] padded)
    {
        if (padded.Length != NativeTileCount)
            throw new InvalidDataException(
                $"The padded l1.sol comparison catalog must contain {NativeTileCount} entries.");

        var comparison = padded.ToArray();
        for (var pieceId = 0; pieceId < comparison.Length; pieceId++)
            comparison[pieceId] &= SupportedSolMask;
        return comparison;
    }

    private static byte[] ReadRequiredFile(string root, string fileName)
    {
        var path = Path.Combine(root, fileName);
        Assert.True(File.Exists(path), $"Native oracle file is missing: {path}");
        var bytes = File.ReadAllBytes(path);
        Assert.NotEmpty(bytes);
        return bytes;
    }

    private static OracleSnapshot ReadSnapshot(string root, string fileName)
    {
        var path = Path.Combine(root, fileName);
        Assert.True(File.Exists(path), $"Native oracle snapshot is missing: {path}");
        var snapshot = JsonSerializer.Deserialize<OracleSnapshot>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(snapshot!.Hashes.MegaTiles));
        return snapshot;
    }

    private static string Hash(ushort[] values)
    {
        var hash = FnvOffset;
        foreach (var value in values) {
            hash = Add(hash, (byte)value);
            hash = Add(hash, (byte)(value >> 8));
        }
        return FormatHash(hash);
    }

    private static string HashBytes(IReadOnlyList<ushort> values)
    {
        var hash = FnvOffset;
        foreach (var value in values)
            hash = Add(hash, checked((byte)value));
        return FormatHash(hash);
    }

    private static string Hash(int[] values)
    {
        var hash = FnvOffset;
        foreach (var value in values) {
            var unsigned = unchecked((uint)value);
            hash = Add(hash, (byte)unsigned);
            hash = Add(hash, (byte)(unsigned >> 8));
            hash = Add(hash, (byte)(unsigned >> 16));
            hash = Add(hash, (byte)(unsigned >> 24));
        }
        return FormatHash(hash);
    }

    private static ulong Add(ulong hash, byte value)
        => unchecked((hash ^ value) * FnvPrime);

    private static string FormatHash(ulong hash)
        => $"0x{hash:x16}";

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private sealed class OracleSnapshot
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public int Level { get; set; }
        public string Policy { get; set; } = string.Empty;
        public string Entry { get; set; } = string.Empty;
        public uint InputSeed { get; set; }
        public uint AttemptSeed { get; set; }
        public uint FinalRandomState { get; set; }
        public int CoarseWidth { get; set; }
        public int CoarseHeight { get; set; }
        public int CoarseTransparencyWidth { get; set; }
        public int CoarseTransparencyHeight { get; set; }
        public int NativeWidth { get; set; }
        public int NativeHeight { get; set; }
        public int SolCount { get; set; }
        public int[] EntryPosition { get; set; } = [];
        public int[] StairsUp { get; set; } = [];
        public int[] StairsDown { get; set; } = [];
        public ushort[] MegaTiles { get; set; } = [];
        public ushort[] CoarseTransparency { get; set; } = [];
        public ushort[] Pieces { get; set; } = [];
        public ushort[] Transparency { get; set; } = [];
        public ushort[] SolProperties { get; set; } = [];
        public int[] SolidCells { get; set; } = [];
        public int[] MissileBlockedCells { get; set; } = [];
        public int[] BlockLightCells { get; set; } = [];
        public OracleHashes Hashes { get; set; } = new();
    }

    private sealed class OracleHashes
    {
        public string MegaTiles { get; set; } = string.Empty;
        public string CoarseTransparency { get; set; } = string.Empty;
        public string Pieces { get; set; } = string.Empty;
        public string Transparency { get; set; } = string.Empty;
        public string SolProperties { get; set; } = string.Empty;
        public string SolidCells { get; set; } = string.Empty;
        public string MissileBlockedCells { get; set; } = string.Empty;
        public string BlockLightCells { get; set; } = string.Empty;
    }
}
