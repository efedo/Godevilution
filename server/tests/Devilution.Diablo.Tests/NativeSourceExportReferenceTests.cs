using Devilution.Data;
using Devilution.Diablo;
using Godevilution.Tools;
using Godevilution.Tools.Archive;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Local-only: compares the C# native-source exporter with a profile compiled
 * from the native engine's export. Needs the player's DIABDAT.MPQ and such a
 * profile; neither may be committed (retail-derived).
 */
public sealed class NativeSourceExportReferenceTests
{
    private static (MpqArchive Archive, AuthoritativeCathedralPopulationMetadata Reference) Open()
    {
        var archivePath = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SOURCE_ARCHIVE");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SOURCE_REFERENCE_PROFILE");
        Assert.False(string.IsNullOrWhiteSpace(archivePath), "Set GODEVILUTION_NATIVE_SOURCE_ARCHIVE to DIABDAT.MPQ.");
        Assert.False(string.IsNullOrWhiteSpace(profile), "Set GODEVILUTION_NATIVE_SOURCE_REFERENCE_PROFILE to a compiled class profile.");
        var tables = ReferenceTables(profile!);
        var reference = AuthoritativeCathedralPopulationMetadata.Load(new ContentPack(
            CathedralPopulationMetadataCompiler.PackId, CathedralPopulationMetadataCompiler.PackVersion, tables,
            schema: new ContentSchemaIdentity(AuthoritativeCathedralPopulationMetadata.SchemaId,
                AuthoritativeCathedralPopulationMetadata.SchemaVersion)));
        return (MpqArchive.Open(archivePath!), reference);
    }

    /** The profile nests its copied metadata tables under the population pack. */
    internal static TsvTable[] ReferenceTables(string profile)
        => Directory.GetFiles(Path.Combine(profile, "native_cathedral_population", "native_cathedral_population"), "*.tsv")
            .Order(StringComparer.Ordinal)
            .Select(path => TsvTable.Parse(Path.GetFileName(path), File.ReadAllText(path)))
            .ToArray();

    internal static string TextDataRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory);
             directory is not null; directory = directory.Parent) {
            var candidate = Path.Combine(directory.FullName, "assets", "txtdata");
            if (File.Exists(Path.Combine(candidate, "text", "textdat.tsv")))
                return candidate;
        }
        throw new DirectoryNotFoundException("Could not locate assets/txtdata.");
    }

    [Fact(Explicit = true)]
    public void CathedralGeometryMatchesTheNativeExport()
    {
        var (archive, reference) = Open();
        using (archive) {
            var geometry = NativeSourceGeometryExport.Read(archive);
            Assert.Equal(reference.EffectivePieceProperties, geometry.EffectivePieceProperties);
            Assert.Equal(reference.Geometry.Pieces.Definitions, geometry.Catalog.Pieces.Definitions);
            Assert.Equal(reference.Geometry.TileProperties.Definitions, geometry.Catalog.TileProperties.Definitions);
        }
    }

    [Fact(Explicit = true)]
    public void PopulationRulesMatchTheNativeExport()
    {
        var (archive, reference) = Open();
        archive.Dispose();
        var textData = TextDataRoot();
        Assert.Equal(reference.DepthRules, NativeSourceRuleExport.DepthRules(textData));
        Assert.Equal(reference.StoryMessages, NativeSourceRuleExport.StoryMessages(textData));
        Assert.Equal(reference.ShrineAvailable, NativeSourceRuleExport.SinglePlayerShrineAvailability());
    }

    [Fact(Explicit = true)]
    public void ItemTablesMatchTheNativeExport()
    {
        var expected = AuthoritativeCathedralThemeItemMetadata.CreateTables(Open().Reference.Items.Catalog);
        var actual = AuthoritativeCathedralThemeItemMetadata.CreateTables(
            NativeSourceItemExport.Read(Path.GetDirectoryName(TextDataRoot())!));
        AssertTablesEqual(expected, actual);
    }

    [Fact(Explicit = true)]
    public void PopulationMetadataTablesMatchTheNativeExportByteForByte()
    {
        var (archive, _) = Open();
        using (archive) {
            var profile = Path.Combine(Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SOURCE_REFERENCE_PROFILE")!,
                "native_cathedral_population", "native_cathedral_population");
            var output = Directory.CreateTempSubdirectory("native-source-export-").FullName;
            try {
                var tables = NativeSourceExport.CreatePopulationTables(archive, Path.GetDirectoryName(TextDataRoot())!);
                AssertTablesEqual(ReferenceTables(Path.GetDirectoryName(Path.GetDirectoryName(profile))!), tables);
                NativeSourceExport.WriteTables(tables, output);
                Assert.Equal(AuthoritativeCathedralPopulationMetadata.TableNames,
                    Directory.GetFiles(output).Select(Path.GetFileName).Order(StringComparer.Ordinal));
                foreach (var name in AuthoritativeCathedralPopulationMetadata.TableNames)
                    Assert.True(File.ReadAllBytes(Path.Combine(profile, name)).AsSpan()
                        .SequenceEqual(File.ReadAllBytes(Path.Combine(output, name))), name);
            } finally {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact(Explicit = true)]
    public void RuntimeSidecarTablesMatchTheNativeExportByteForByte()
    {
        var (archive, reference) = Open();
        using (archive) {
            var runtime = Path.Combine(Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SOURCE_REFERENCE_PROFILE")!,
                "native_cathedral_runtime", "native_cathedral_runtime");
            var textData = TextDataRoot();
            var tables = new[] {
                NativeSourceMonsterExport.Runtime(textData).CreateTable(),
                NativeSourceFootprintExport.CreateTable(archive, Path.GetDirectoryName(textData)!, reference.Items.Catalog),
            };
            foreach (var table in tables)
                Assert.Equal(File.ReadAllText(Path.Combine(runtime, table.SourcePath)).Replace("\r\n", "\n", StringComparison.Ordinal),
                    table.ToCanonicalTsv());
        }
    }

    internal static void AssertTablesEqual(IReadOnlyList<TsvTable> expected, IReadOnlyList<TsvTable> actual)
    {
        Assert.Equal(expected.Select(table => table.SourcePath), actual.Select(table => table.SourcePath));
        foreach (var (left, right) in expected.Zip(actual)) {
            var leftLines = left.ToCanonicalTsv().Split('\n');
            var rightLines = right.ToCanonicalTsv().Split('\n');
            var mismatch = leftLines.Zip(rightLines).Select((pair, line) => (pair, line))
                .FirstOrDefault(row => row.pair.First != row.pair.Second);
            Assert.True(mismatch.pair.First is null && leftLines.Length == rightLines.Length,
                $"{left.SourcePath} line {mismatch.line + 1}:\n  native {mismatch.pair.First}\n  export {mismatch.pair.Second}");
        }
    }
}
