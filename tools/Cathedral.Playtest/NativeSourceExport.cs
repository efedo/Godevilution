using System.Security.Cryptography;
using System.Text;
using Devilution.Data;
using Devilution.Diablo;
using Godevilution.Tools.Archive;

namespace Godevilution.Tools;

/** Where Write placed each native profile compiler input. */
public sealed record NativeSourceExportLayout(
    string GeometryDirectory,
    string MetadataDirectory,
    string MonsterRuntimeTable,
    string InventoryFootprintTable,
    string TownSourceDirectory,
    string CatacombsMetadataDirectory);

/**
 * Builds the native profile compiler inputs without the native engine: archive
 * level and cursor files, open txtdata and fonts, and native source constants.
 * The output is retail-derived and stays local.
 */
public static class NativeSourceExport
{
    public const string GeometryDirectoryName = "geometry";
    public const string MetadataDirectoryName = "population-metadata";
    public const string TownSourceDirectoryName = "town";
    public const string CatacombsMetadataDirectoryName = "catacombs-population-metadata";
    private const string OpenTownMap = "automap.dun";

    /** The 19 Cathedral population metadata source tables (--compile-population-metadata input). */
    public static IReadOnlyList<TsvTable> CreatePopulationTables(MpqArchive archive, string assetsRoot)
        => CreatePopulationTables(NativeSourceGeometryExport.Read(archive), assetsRoot);

    /** Tables built from archive data; every other exported table derives from the open assets alone. */
    public static IReadOnlyList<string> RetailDerivedTableNames { get; } = [
        AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
        AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
        AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName,
        NativeSourceFootprintExport.TableName,
    ];

    public static IReadOnlyList<TsvTable> CreatePopulationTables(NativeSourceGeometry geometry, string assetsRoot)
    {
        var textData = Path.Combine(assetsRoot, "txtdata");
        return AuthoritativeCathedralPopulationMetadata.CreateTables(geometry.Catalog, geometry.EffectivePieceProperties,
            NativeSourceMonsterExport.Monsters(textData), NativeSourceMonsterExport.Objects(textData),
            NativeSourceMonsterExport.UniqueMonsters(textData), NativeSourceItemExport.Read(assetsRoot),
            NativeSourceRuleExport.DepthRules(textData), NativeSourceRuleExport.StoryMessages(textData),
            NativeSourceRuleExport.SinglePlayerShrineAvailability());
    }

    /**
     * The Catacombs (L5-L8) metadata tables: archive L2 geometry, the Cathedral
     * monster, object, unique, item and shrine tables, levels 5-8 rules and the L8 books.
     */
    public static IReadOnlyList<TsvTable> CreateCatacombsTables(MpqArchive archive, string assetsRoot,
        AuthoritativeCathedralPopulationMetadata cathedral)
    {
        ArgumentNullException.ThrowIfNull(cathedral);
        var (geometry, til, sol) = NativeSourceGeometryExport.ReadCatacombs(archive);
        var textData = Path.Combine(assetsRoot, "txtdata");
        var story = File.ReadAllBytes(Path.Combine(textData, "text", "textdat.tsv"));
        return AuthoritativeCatacombsPopulationMetadata.CreateTables(geometry.Catalog, geometry.EffectivePieceProperties,
            Sha256(til), Sha256(sol), cathedral.Population.Monsters, cathedral.Population.Objects,
            cathedral.UniqueMonsters.Definitions, cathedral.Items.Catalog, NativeSourceRuleExport.CatacombsDepthRules(textData),
            NativeSourceRuleExport.CatacombsStoryMessages(textData), story.Length, Sha256(story), cathedral.ShrineAvailable);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static NativeSourceExportLayout Write(MpqArchive archive, string assetsRoot, string outputDirectory)
    {
        var population = CreatePopulationTables(archive, assetsRoot);
        var items = NativeSourceItemExport.Read(assetsRoot);
        var geometryNames = new[] {
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
        };
        var layout = new NativeSourceExportLayout(
            Path.Combine(outputDirectory, GeometryDirectoryName),
            Path.Combine(outputDirectory, MetadataDirectoryName),
            Path.Combine(outputDirectory, AuthoritativeCathedralMonsterRuntimeCatalog.TableName),
            Path.Combine(outputDirectory, NativeSourceFootprintExport.TableName),
            Path.Combine(outputDirectory, TownSourceDirectoryName),
            Path.Combine(outputDirectory, CatacombsMetadataDirectoryName));
        WriteTables(population.Where(table => geometryNames.Contains(table.SourcePath, StringComparer.Ordinal)), layout.GeometryDirectory);
        WriteTables(population, layout.MetadataDirectory);
        var cathedral = AuthoritativeCathedralPopulationMetadata.Load(new ContentPack(
            CathedralPopulationMetadataCompiler.PackId, CathedralPopulationMetadataCompiler.PackVersion, population,
            schema: new ContentSchemaIdentity(AuthoritativeCathedralPopulationMetadata.SchemaId,
                AuthoritativeCathedralPopulationMetadata.SchemaVersion)));
        WriteTables(CreateCatacombsTables(archive, assetsRoot, cathedral), layout.CatacombsMetadataDirectory);
        WriteTables([NativeSourceMonsterExport.Runtime(Path.Combine(assetsRoot, "txtdata")).CreateTable(),
            NativeSourceFootprintExport.CreateTable(archive, assetsRoot, items)], outputDirectory);
        // The Town compiler reads these files unchanged; automap.dun ships with the open assets (CMake/Assets.cmake).
        Directory.CreateDirectory(layout.TownSourceDirectory);
        foreach (var name in CathedralNativeTownPlaytestCompiler.SourceFileNames)
            File.WriteAllBytes(Path.Combine(layout.TownSourceDirectory, name), name == OpenTownMap
                ? File.ReadAllBytes(Path.Combine(assetsRoot, "levels", "towndata", name))
                : archive.ReadFile($"levels\\towndata\\{name}"));
        return layout;
    }

    /** Canonical CRLF TSV, the layout the profile compilers read. */
    public static void WriteTables(IEnumerable<TsvTable> tables, string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var table in tables)
            File.WriteAllText(Path.Combine(directory, table.SourcePath),
                table.ToCanonicalTsv().Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(false, true));
    }
}
