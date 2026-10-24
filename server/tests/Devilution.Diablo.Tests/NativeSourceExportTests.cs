using System.Text;
using Devilution.Data;
using Devilution.Diablo;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Archive-free checks of the C# native-source exporter. The fixtures under
 * test/fixtures/native-open-metadata were verified byte for byte against the
 * native engine's export (NativeSourceExportReferenceTests, local only).
 */
public sealed class NativeSourceExportTests
{
    private static string Assets() => Path.GetDirectoryName(NativeSourceExportReferenceTests.TextDataRoot())!;

    private static string Fixtures() => Path.Combine(Path.GetDirectoryName(Assets())!, "test", "fixtures", "native-open-metadata");

    [Fact]
    public void OpenMetadataTablesMatchTheVerifiedNativeExport()
    {
        // Geometry is retail-derived; an all-zero stand-in keeps this archive-free.
        var geometry = new NativeSourceGeometry(new AuthoritativeNativeDunGeometryCatalog(
                AuthoritativeDunPieceCatalog.LoadTil("l1.til", new byte[206 * 8]),
                AuthoritativeDunTilePropertyCatalog.LoadSol("l1.sol", new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount])),
            new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount]);
        var tables = NativeSourceExport.CreatePopulationTables(geometry, Assets())
            .Where(table => !NativeSourceExport.RetailDerivedTableNames.Contains(table.SourcePath))
            .Append(NativeSourceMonsterExport.Runtime(Path.Combine(Assets(), "txtdata")).CreateTable())
            .ToArray();
        Assert.Equal(Directory.GetFiles(Fixtures(), "*.tsv").Select(Path.GetFileName).Order(StringComparer.Ordinal),
            tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal));
        foreach (var table in tables)
            Assert.True(File.ReadAllText(Path.Combine(Fixtures(), table.SourcePath), Encoding.UTF8)
                == table.ToCanonicalTsv().Replace("\n", "\r\n", StringComparison.Ordinal), table.SourcePath);
    }

    [Fact]
    public void ShippedFontPagesMatchTheNativeAssetList()
    {
        var assetsCmake = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Assets())!, "CMake", "Assets.cmake"));
        var listed = System.Text.RegularExpressions.Regex.Matches(assetsCmake, @"fonts/12-([0-9a-f]+)\.clx")
            .Select(match => Convert.ToInt32(match.Groups[1].Value, 16));
        Assert.Equal(listed.Order(), NativeSourceItemExport.ShippedFontPages.Order());
        Assert.Equal(NativeSourceItemExport.ShippedFontPages.Count * 256,
            NativeSourceItemExport.GlyphWidths(Path.Combine(Assets(), "fonts")).Count);
    }

    [Fact]
    public void CathedralSolFixesPatchTheNativeTileList()
    {
        var properties = NativeSourceGeometryExport.ApplyCathedralSolFixes([0x01, 0x40]);
        Assert.Equal(AuthoritativeDunTilePropertyCatalog.NativeTileCount, properties.Length);
        Assert.Equal(new byte[] { 0x01, 0x40 }, properties[..2]);
        const byte light = (byte)AuthoritativeDunTileFlags.BlockLight;
        const byte missile = (byte)AuthoritativeDunTileFlags.BlockMissile;
        Assert.Equal(light | missile, properties[9]);
        Assert.Equal(missile, properties[27]);
        Assert.Equal(light, properties[24]);
        Assert.Equal(light, properties[403]);
        Assert.Equal(light | missile, properties[450]);
        Assert.Equal(0, properties[10]);
        Assert.Throws<InvalidDataException>(() => NativeSourceGeometryExport.ApplyCathedralSolFixes([]));
        Assert.Throws<InvalidDataException>(() => NativeSourceGeometryExport.ApplyCathedralSolFixes(
            new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount + 1]));
    }

    [Fact]
    public void CelFrameSizesCountRowsLikeNativeCelToClx()
    {
        // Frame 0: width 3, no header, rows "-2 transparent, 1 pixel" and "3 pixels".
        // Frame 1: width 2, 10-byte header, one row of a 2-pixel transparent run.
        byte[] frame0 = [0xFE, 0x01, 0xAA, 0x03, 0x01, 0x02, 0x03];
        byte[] frame1 = [0x0A, 0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0xFE];
        var cel = Cel(frame0, frame1);
        Assert.Equal(new[] { (3, 2), (2, 1) }, NativeSourceFootprintExport.CelFrameSizes(cel, [3, 2]));
        Assert.Throws<InvalidDataException>(() => NativeSourceFootprintExport.CelFrameSizes(cel, [3]));
        Assert.Throws<InvalidDataException>(() => NativeSourceFootprintExport.CelFrameSizes(Cel([0x05, 0x01]), [6]));
        Assert.Equal(new ushort[] { 33, 28 }, NativeSourceFootprintExport.ReadWidths("33\r\n28\r\n"));
    }

    [Fact]
    public void ClxWidthsReadEachSpriteHeader()
    {
        byte[] clx = [2, 0, 0, 0, 16, 0, 0, 0, 22, 0, 0, 0, 28, 0, 0, 0,
            6, 0, 7, 0, 1, 0, 6, 0, 9, 0, 2, 0];
        Assert.Equal(new[] { 7, 9 }, NativeSourceItemExport.ClxWidths(clx));
        Assert.Throws<InvalidDataException>(() => NativeSourceItemExport.ClxWidths(clx[..^1]));
    }

    [Fact]
    public void RetailAvailabilityExcludesHellfireAndSpecialItems()
    {
        Assert.True(NativeSourceItemExport.IsAvailable(0));
        Assert.All(new[] { 5, 22, 32, 35, 47, 83, 86, 92, 161, 165 },
            index => Assert.False(NativeSourceItemExport.IsAvailable(index)));
        Assert.All(new[] { 34, 48, 82, 87, 91, 93, 160, 166 },
            index => Assert.True(NativeSourceItemExport.IsAvailable(index)));
    }

    private static byte[] Cel(params byte[][] frames)
    {
        var header = 4 + 4 * (frames.Length + 1);
        var bytes = new List<byte>(BitConverter.GetBytes((uint)frames.Length));
        var offset = header;
        foreach (var frame in frames) {
            bytes.AddRange(BitConverter.GetBytes((uint)offset));
            offset += frame.Length;
        }
        bytes.AddRange(BitConverter.GetBytes((uint)offset));
        foreach (var frame in frames)
            bytes.AddRange(frame);
        return [.. bytes];
    }
}
