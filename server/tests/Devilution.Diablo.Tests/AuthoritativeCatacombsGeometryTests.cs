using System.Globalization;
using System.Text.Json;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCatacombsGeometryTests
{
    [Fact]
    public void ExpansionUsesL2ExteriorAndPreservesOpaqueSolWithoutCathedralCorrections()
    {
        var result = Synthetic();
        var definitions = Enumerable.Range(0, 160).Select(id => new AuthoritativeDunPieceDefinition(
            (uint)id, id == 11 ? 9U : 100U, 101, 102, 103)).ToArray();
        var properties = new byte[1379];
        properties[9] = 0x40;
        properties[100] = 5;
        var geometry = AuthoritativeCatacombsGeometryMaterializer.Materialize(5, result,
            new(definitions), properties, "catacombs-5");
        Assert.Equal((ushort)9, geometry.Level.NativePieceLayer[0]);
        Assert.Equal((ushort)100, geometry.Level.NativePieceLayer[16 * 112 + 16]);
        Assert.Equal(AuthoritativeDunTileFlags.None, geometry.Level.NativePieceProperties[9]);
        Assert.Equal((byte)0x40, geometry.EffectivePieceProperties[9]);
        Assert.DoesNotContain(0, geometry.Level.NativePieceCollisionCells);
        Assert.DoesNotContain(0, geometry.Level.NativePieceMissileBlockedCells);
        Assert.Contains(16 * 112 + 16, geometry.Level.NativePieceCollisionCells);
        Assert.Contains(16 * 112 + 16, geometry.Level.NativePieceMissileBlockedCells);
        properties[9] = 0;
        ((ushort[])result.MegaTiles)[0] = 12;
        ((byte[])result.NativeTransparency)[0] = 255;
        Assert.Equal((byte)0x40, geometry.EffectivePieceProperties[9]);
        Assert.Equal((ushort)3, geometry.GenerationResult.MegaTiles[0]);
        Assert.Equal((byte)0, geometry.GenerationResult.NativeTransparency[0]);
    }

    [Fact]
    public void MalformedFamilyLayersAndMetadataFailBeforePublication()
    {
        var pieces = new AuthoritativeDunPieceCatalog(Enumerable.Range(0, 160)
            .Select(id => new AuthoritativeDunPieceDefinition((uint)id, 0, 0, 0, 0)));
        var result = Synthetic();
        void Refuses(AuthoritativeCatacombsGenerationResult value) => Assert.Throws<InvalidDataException>(
            () => AuthoritativeCatacombsGeometryMaterializer.Materialize(5, value, pieces, new byte[1379], "catacombs-5"));
        Refuses(result with { Depth = 4 });
        Refuses(result with { Depth = 6 });
        Refuses(result with { MegaTiles = new ushort[1600] });
        Refuses(result with { ReplacementMegaTiles = new ushort[1599] });
        Refuses(result with { ProtectedMegaTiles = [] });
        Refuses(result with { NativeTransparency = new byte[6400] });
        Refuses(result with { NativeTileFlags = [] });
        Refuses(result with { MinimumBounds = new(0, 0) });
        Refuses(result with { EntryPosition = new(0, 0) });
        Refuses(result with { NextTransparencyValue = 128 });
        Refuses(result with { Themes = [new(1, 1, int.MaxValue, 4, 1)] });
        Refuses(result with { Themes = [new(1, 1, 4, int.MaxValue, 1)] });
        Refuses(result with { Themes = [new(40, 1, 1, 1, 1)] });
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsGeometryMaterializer.Materialize(5,
            result, pieces, new byte[559], "catacombs-5"));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsGeometryMaterializer.Materialize(5,
            result, new(pieces.Definitions.Take(159)), new byte[1379], "catacombs-5"));
    }

    [Fact(Explicit = true)]
    public void EverySupportedNativeCaseMatchesFineGeometry()
    {
        var reference = NativeCatacombsGenerationReference.Load();
        var compared = 0;
        foreach (var item in reference.Cases) {
            if (item.Result is not { } result) continue;
            // Native coarse output is an input to this materializer test only.
            // Production generation never reads a native captured map.
            var geometry = AuthoritativeCatacombsGeometryMaterializer.Materialize((uint)result.Depth,
                result, reference.Pieces, reference.Sol, "catacombs-" + result.Depth);
            Assert.Equal(item.FinePieces, geometry.Level.NativePieceLayer);
            Assert.Equal(item.FineSpecials.Select(v => (ushort)v), geometry.Level.NativeSpecialLayer);
            Assert.Equal(result.NativeTransparency.Select(v => (ushort)v), geometry.Level.NativeTransparency);
            Assert.Equal(item.EffectiveSol, geometry.EffectivePieceProperties);
            Assert.Equal(item.Triggers, geometry.Triggers);
            Assert.Equal(item.FinePieces.Select((piece, cell) => (piece, cell))
                .Where(v => (item.EffectiveSol[v.piece] & 1) != 0).Select(v => v.cell), geometry.Level.NativePieceCollisionCells);
            Assert.Equal(item.FinePieces.Select((piece, cell) => (piece, cell))
                .Where(v => (item.EffectiveSol[v.piece] & 4) != 0).Select(v => v.cell), geometry.Level.NativePieceMissileBlockedCells);
            compared++;
        }
        Assert.Equal(66, compared);
    }

    private static AuthoritativeCatacombsGenerationResult Synthetic()
        => new(5, AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs,
            AuthoritativeCatacombsEntry.Main, 0, null, 0, 0,
            Enumerable.Repeat((ushort)3, 1600).ToArray(), Enumerable.Repeat((ushort)3, 1600).ToArray(),
            new bool[1600], new byte[12544], new byte[12544], [], new(3, 4), new(9, 10), new(15, 16),
            new(27, 28), new(16, 16), new(96, 96), default, default, 1);
}

/** External comparison reader; never part of production generation or metadata. */
internal sealed record NativeCatacombsGenerationReference(
    AuthoritativeDunPieceCatalog Pieces, byte[] Sol, IReadOnlyList<NativeCatacombsGenerationCase> Cases)
{
    internal static NativeCatacombsGenerationReference Load()
    {
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CATACOMBS_REFERENCE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(root), "An explicit completed native Catacombs corpus is required.");
        var til = File.ReadAllBytes(Path.Combine(root!, "source", "levels", "l2data", "l2.til"));
        var sourceSol = File.ReadAllBytes(Path.Combine(root!, "source", "levels", "l2data", "l2.sol"));
        Assert.Equal(160 * 8, til.Length);
        Assert.InRange(sourceSol.Length, 1, 1379);
        var sol = new byte[1379];
        sourceSol.CopyTo(sol, 0);
        var index = File.ReadAllLines(Path.Combine(root!, "cases", "cases.tsv"));
        Assert.Equal("case_id\tlevel_id\tentry\tinput_seed\tinitial_level_seed\tattempt_seed\tfinal_random_state\tview_x\tview_y\tfile", index[0]);
        Assert.Equal(76, index.Length);
        var cases = new List<NativeCatacombsGenerationCase>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in index.Skip(1)) {
            var row = line.Split('\t');
            Assert.Equal(10, row.Length);
            Assert.True(ids.Add(row[0]));
            Assert.Equal(row[0] + ".json", row[9]);
            Assert.Equal(row[9], Path.GetFileName(row[9]));
            var bytes = File.ReadAllBytes(Path.Combine(root!, "cases", row[9]));
            Assert.InRange(bytes.Length, 1, 1024 * 1024);
            using var document = JsonDocument.Parse(bytes);
            var c = document.RootElement;
            int Int(string name) => c.GetProperty(name).GetInt32();
            uint UInt(string name) => c.GetProperty(name).GetUInt32();
            ushort[] Words(string name) => c.GetProperty(name).EnumerateArray().Select(v => v.GetUInt16()).ToArray();
            byte[] Bytes(string name) => c.GetProperty(name).EnumerateArray().Select(v => v.GetByte()).ToArray();
            AuthoritativeCathedralPoint Point(string name) {
                var values = c.GetProperty(name).EnumerateArray().Select(v => v.GetInt32()).ToArray();
                Assert.Equal(2, values.Length); return new(values[0], values[1]);
            }
            AuthoritativeCatacombsRectangle Rect(string name) {
                var values = c.GetProperty(name).EnumerateArray().Select(v => v.GetInt32()).ToArray();
                Assert.Equal(4, values.Length); return new(values[0], values[1], values[2], values[3]);
            }
            Assert.Equal(1, Int("schemaVersion"));
            Assert.Equal("quest-disabled-original-diablo-single-player-normal-catacombs", c.GetProperty("policy").GetString());
            Assert.Equal("all-NOTAVAIL", c.GetProperty("questPolicy").GetString());
            Assert.Equal("post-CreateDungeon-pre-CreateThemeRooms", c.GetProperty("captureBoundary").GetString());
            Assert.Equal(sourceSol.Length, Int("sourceSolByteCount"));
            Assert.Equal(row[0], c.GetProperty("caseId").GetString());
            Assert.Equal(row[2], c.GetProperty("entry").GetString());
            Assert.Equal(int.Parse(row[1], CultureInfo.InvariantCulture), Int("levelId"));
            Assert.Equal(uint.Parse(row[3], CultureInfo.InvariantCulture), UInt("inputSeed"));
            uint? retained = c.GetProperty("initialLevelSeed").ValueKind == JsonValueKind.Null ? null : UInt("initialLevelSeed");
            Assert.Equal(row[4], retained?.ToString(CultureInfo.InvariantCulture) ?? "none");
            Assert.Equal(uint.Parse(row[5], CultureInfo.InvariantCulture), UInt("attemptSeed"));
            Assert.Equal(uint.Parse(row[6], CultureInfo.InvariantCulture), UInt("finalRandomState"));
            Assert.Equal(new AuthoritativeCathedralPoint(int.Parse(row[7]), int.Parse(row[8])), Point("view"));
            var mega = Words("megaTiles"); var replacement = Words("replacementMegaTiles"); var protectedCells = Bytes("protectedTiles");
            Assert.Equal(1600, mega.Length); Assert.Equal(1600, replacement.Length); Assert.Equal(1600, protectedCells.Length);
            Assert.All(protectedCells, v => Assert.InRange(v, (byte)0, (byte)1));
            var fine = Words("finePieces"); var special = Bytes("fineSpecials"); var trans = Bytes("fineTransparency"); var flags = Bytes("fineTileFlags");
            Assert.Equal(12544, fine.Length); Assert.Equal(12544, special.Length); Assert.Equal(12544, trans.Length); Assert.Equal(12544, flags.Length);
            var effective = Bytes("effectiveSol"); Assert.Equal(sol, effective);
            var triggers = c.GetProperty("triggers").EnumerateArray().Select(t => new AuthoritativeCatacombsTrigger(
                t.GetProperty("x").GetInt32(), t.GetProperty("y").GetInt32(),
                (AuthoritativeCatacombsTriggerKind)t.GetProperty("message").GetInt32(), t.GetProperty("level").GetInt32())).ToArray();
            Assert.False(c.GetProperty("triggerFlag").GetBoolean());
            var entry = row[2] switch { "main" => AuthoritativeCatacombsEntry.Main, "previous" => AuthoritativeCatacombsEntry.Previous,
                "town-warp-down" when Int("levelId") == 5 => AuthoritativeCatacombsEntry.TownWarpDown, _ => (AuthoritativeCatacombsEntry?)null };
            AuthoritativeCathedralPoint Anchor(ushort marker, int dx, int dy) {
                var cell = Assert.Single(mega.Select((tile, cell) => (tile, cell)), v => v.tile == marker).cell;
                return new(cell % 40 - dx, cell / 40 - dy);
            }
            AuthoritativeCatacombsGenerationResult? result = entry.HasValue ? new(Int("levelId"),
                AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs, entry.Value, UInt("inputSeed"), retained,
                UInt("attemptSeed"), UInt("finalRandomState"), mega, replacement, protectedCells.Select(v => v != 0).ToArray(), trans, flags,
                c.GetProperty("themes").EnumerateArray().Select(t => new AuthoritativeCatacombsThemeRoom(
                    t.GetProperty("x").GetInt32(), t.GetProperty("y").GetInt32(), t.GetProperty("width").GetInt32(),
                    t.GetProperty("height").GetInt32(), t.GetProperty("transparency").GetInt32())).ToArray(),
                Anchor(77, 2, 1), Anchor(78, 2, 2), Int("levelId") == 5 ? Anchor(160, 2, 1) : null,
                Point("view"), Point("minimumBounds"), Point("maximumBounds"), Rect("setPieceRoom"), Rect("setPiece"), Int("nextTransparency")) : null;
            cases.Add(new(row[0], result, fine, special, effective, triggers));
        }
        return new(AuthoritativeDunPieceCatalog.LoadTil("l2.til", til), sol, cases.AsReadOnly());
    }
}

internal sealed record NativeCatacombsGenerationCase(string Id, AuthoritativeCatacombsGenerationResult? Result,
    ushort[] FinePieces, byte[] FineSpecials, byte[] EffectiveSol, AuthoritativeCatacombsTrigger[] Triggers);
