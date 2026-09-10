using System.Buffers.Binary;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeDunLevelContentActivationBoundaryTests
{
    [Fact]
    public void CurrentSchemaAcceptsCompleteNativeDunLevelSchemaWithOptionalGeometry()
    {
        var basePack = LoadBasePack();
        var pack = AddNativeDunLevelTable(basePack, CreateNativeDunLevelTable());

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "current-native-dun-level-contract",
                "1",
                [pack])));

        Assert.Contains(
            activation.Simulation.WorldState.World!.Levels,
            level => level.LevelId == 2 && level.NativeObjectLayer.Count == 4);
    }

    [Fact]
    public void CurrentSchemaRejectsLegacyOrderedNativeDunLevelSchema()
    {
        var basePack = LoadBasePack();
        var table = ReverseColumns(CreateNativeDunLevelTable());
        var pack = AddNativeDunLevelTable(basePack, table);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-native-dun-level-schema",
                    "1",
                    [pack]))));

        Assert.Contains("Production native-DUN level table", exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyOrderedNativeDunLevelCompatibility()
    {
        var basePack = LoadBasePack();
        var previous = AddNativeDunLevelTable(
            basePack,
            ReverseColumns(CreateNativeDunLevelTable()),
            new ContentSchemaIdentity("devilution.diablo", "1.500.0"));

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-native-dun-level-contract",
                "1",
                [previous])));

        Assert.NotNull(activation);
        Assert.Contains(
            activation.Simulation.WorldState.World!.Levels,
            level => level.LevelId == 2 && level.NativeObjectLayer.Count == 4);
    }

    private static ContentPack LoadBasePack()
    {
        var root = FindRepositoryRoot();
        return ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
    }

    private static TsvTable CreateNativeDunLevelTable()
        => TsvTable.Parse(
            "native_dun_levels.tsv",
            "level_id\tlevel_symbol\tasset_path\ttile_asset_path\tsol_asset_path\n"
                + "2\tcatacombs\ttest/native-level.dun\t\t\n");

    private static ContentPack AddNativeDunLevelTable(
        ContentPack basePack,
        TsvTable table,
        ContentSchemaIdentity? schema = null)
    {
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables.Concat([table]).ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            schema ?? basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets.Concat([
                new ContentAsset(
                    "test/native-level.dun",
                    CreateDun([0, 17, 0, 19])),
            ]).ToArray());
    }

    private static TsvTable ReverseColumns(TsvTable table)
    {
        var columns = table.Columns.Reverse().ToArray();
        var lines = new List<string> { string.Join('\t', columns) };
        lines.AddRange(table.Rows.Select(row =>
            string.Join('\t', columns.Select(column => row[column]))));
        return TsvTable.Parse(table.SourcePath, string.Join('\n', lines) + "\n");
    }

    private static byte[] CreateDun(ushort[] objectLayer)
    {
        var megaWidth = 1;
        var megaHeight = 1;
        var megaCount = megaWidth * megaHeight;
        var dPieceCount = megaCount * 4;
        var words = new ushort[2 + megaCount + dPieceCount * 4];
        words[0] = (ushort)megaWidth;
        words[1] = (ushort)megaHeight;
        Array.Copy(objectLayer, 0, words, 2 + megaCount + dPieceCount * 2, objectLayer.Length);
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        return bytes;
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
