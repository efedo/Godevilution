using System.Globalization;
using System.Text;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeTownCatalogTests
{
    [Fact]
    public void LoadsCanonicalTownMapsPropertiesAndRosterAsSeparateFineAndCoarseChannels()
    {
        var catalog = LoadCatalog();
        var geometry = catalog.CreateInitialGeometry();

        Assert.Equal(AuthoritativeNativeTownCatalog.TownLevelId, catalog.LevelId);
        Assert.Equal("town", catalog.LevelSymbol);
        Assert.Equal("original-diablo.town", catalog.BoundGeneratorId);
        Assert.Equal(1, catalog.BoundGeneratorRevision);
        Assert.Equal("native-town-warp-policy.v1", geometry.WarpPolicyVersion);
        Assert.Equal(2588U, geometry.InitialRandomState);
        Assert.Equal(112 * 112, geometry.NativePieceLayer.Count);
        Assert.Equal(40 * 40, geometry.NativeMegaTileLayer.Count);
        Assert.Equal(112 * 112, geometry.NativeSpecialLayer.Count);
        Assert.Equal(112 * 112, geometry.NativeTransparencyLayer.Count);
        Assert.Equal((ushort)7, geometry.NativeMegaTileLayer[0]);
        Assert.Contains(0, geometry.NativeProtectedMegaCells);
        Assert.Equal((ushort)359, geometry.NativePieceLayer[62 * 112 + 62]);
        Assert.Equal((ushort)1, geometry.NativeSpecialLayer[62 * 112 + 62]);
        Assert.Equal((ushort)923, geometry.NativePieceLayer[63 * 112 + 62]);
        Assert.Contains(63 * 112 + 62, geometry.NativeSolidCells);
        Assert.Equal((ushort)909, geometry.NativePieceLayer[64 * 112 + 62]);
        Assert.DoesNotContain(64 * 112 + 62, geometry.NativeSolidCells);
        Assert.Equal(10, geometry.ClosedHellTileChoices.Count);

        Assert.Equal(1379, catalog.NativeSourceFlags.Count);
        Assert.Equal((byte)64, catalog.NativeSourceFlags[20]);
        Assert.Equal(AuthoritativeDunTileFlags.None, geometry.NativePieceProperties[20]);
        Assert.Equal(11, catalog.Roster.Residents.Count);
        Assert.Equal(8, catalog.Roster.Humans.Count);
        Assert.Equal(20, catalog.Roster.OccupiedCells.Count);
        Assert.True(catalog.Roster.IsOccupied(62, 63));
        Assert.True(catalog.Roster.IsOccupied(57, 15));
        Assert.False(catalog.Roster.IsOccupied(62, 64));
        catalog.Roster.ValidateGreetings(CreateGreetings(catalog.Roster), geometry);
    }

    [Fact]
    public void CheckpointReplaysOrderedMutationsAndDetachesEveryRetainedChannel()
    {
        var catalog = LoadCatalog();
        var mutations = new List<AuthoritativeNativeTownMutation> {
            AuthoritativeNativeTownMutation.OpenHive,
            AuthoritativeNativeTownMutation.OpenGrave,
            AuthoritativeNativeTownMutation.CleanFountain,
            AuthoritativeNativeTownMutation.CloseHive,
        };
        var geometry = Apply(catalog.CreateInitialGeometry(), mutations);
        var checkpoint = catalog.Capture(geometry, mutations);
        mutations.Clear();

        Assert.Equal(4, checkpoint.Mutations.Count);
        Assert.Equal(AuthoritativeNativeTownMutation.OpenHive, checkpoint.Mutations[0]);
        Assert.Equal(2588U, checkpoint.InitialRandomState);
        Assert.Equal(geometry.FinalRandomState, checkpoint.FinalRandomState);
        Assert.Equal(40 * 40, checkpoint.NativeMegaTileLayer.Count);
        Assert.Single(checkpoint.NativeProtectedMegaCells);
        Assert.Equal(0, checkpoint.NativeProtectedMegaCells[0]);
        Assert.Equal(112 * 112, checkpoint.NativePieceLayer.Count);
        Assert.Equal(112 * 112, checkpoint.NativeSpecialLayer.Count);
        Assert.Equal(112 * 112, checkpoint.NativeTransparencyLayer.Count);
        Assert.Equal(112 * 112, checkpoint.NativeSolPropertyLayer.Count);
        Assert.NotEqual(
            catalog.CreateInitialGeometry().NativePieceLayer[60 * 112 + 78],
            checkpoint.NativePieceLayer[60 * 112 + 78]);

        var random = new AuthoritativeLegacyRandom(0xFEED1234);
        var randomState = random.State;
        var restored = catalog.Restore(checkpoint);
        Assert.Equal(randomState, random.State);
        Assert.Equal(geometry.NativePieceLayer, restored.NativePieceLayer);
        Assert.Equal(geometry.NativeMegaTileLayer, restored.NativeMegaTileLayer);
        Assert.Equal(geometry.NativeProtectedMegaCells, restored.NativeProtectedMegaCells);
        Assert.Equal(geometry.NativeSpecialLayer, restored.NativeSpecialLayer);
        Assert.Equal(geometry.NativeTransparencyLayer, restored.NativeTransparencyLayer);
        Assert.Equal(geometry.NativeSolPropertyLayer, restored.NativeSolPropertyLayer);
        Assert.Equal(geometry.NativeSolidCells, restored.NativeSolidCells);
        Assert.Equal(geometry.NativeMissileBlockedCells, restored.NativeMissileBlockedCells);
        Assert.Equal(geometry.NativeBlockLightCells, restored.NativeBlockLightCells);

        var prototype = new AuthoritativeLevel(0, 112, 112, startPositionX: 75, startPositionY: 68) {
            StableSymbol = "town",
        };
        var backing = catalog.CreateBackingLevel(restored, prototype);
        Assert.Equal(112, backing.Width);
        Assert.Equal(112, backing.Height);
        Assert.Empty(backing.NativeMegaTileLayer);
        Assert.Equal(restored.NativePieceLayer, backing.NativePieceLayer);
        Assert.Equal(restored.NativeSpecialLayer, backing.NativeSpecialLayer);
        Assert.Empty(backing.StaticObjectBlockedCells);
        Assert.Equal(restored.NativeSolidCells, backing.NativePieceCollisionCells);
    }

    [Fact]
    public void RestoreRejectsChangedProvenanceChannelsSettingsAndMutationHistory()
    {
        var catalog = LoadCatalog();
        var history = new[] { AuthoritativeNativeTownMutation.OpenHive };
        var geometry = Apply(catalog.CreateInitialGeometry(), history);
        var checkpoint = catalog.Capture(geometry, history);

        Assert.Throws<InvalidDataException>(() => (checkpoint with {
            NativeProtectedMegaCells = [-1],
        }).ValidateShape());
        Assert.Throws<InvalidDataException>(() => (checkpoint with {
            ClosedHellTileChoices = [0],
        }).ValidateShape());
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            CatalogFingerprint = new string('0', 64),
        }));
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            FinalRandomState = checkpoint.FinalRandomState ^ 1,
        }));
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            Settings = checkpoint.Settings with { TownWarpFrom = 9 },
        }));
        var pieces = checkpoint.NativePieceLayer.ToArray();
        pieces[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            NativePieceLayer = pieces,
        }));
        var mega = checkpoint.NativeMegaTileLayer.ToArray();
        mega[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            NativeMegaTileLayer = mega,
        }));
        var special = checkpoint.NativeSpecialLayer.ToArray();
        special[0] = 1;
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            NativeSpecialLayer = special,
        }));
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            Mutations = [((AuthoritativeNativeTownMutation)255)],
        }));
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            NativeTransparencyLayer = [],
        }));
        Assert.Throws<InvalidDataException>(() => catalog.Restore(checkpoint with {
            Mutations = Enumerable.Repeat(
                AuthoritativeNativeTownMutation.OpenHive,
                AuthoritativeNativeTownCatalog.MaximumMutationCount + 1).ToArray(),
        }));
        Assert.Throws<InvalidDataException>(() => catalog.Capture(
            catalog.CreateInitialGeometry(),
            history));
    }

    [Fact]
    public void ReservedSolByteRemainsContentIdentityEvenThoughItHasNoCollisionEffect()
    {
        var withReservedBit = LoadCatalog();
        var withoutReservedBit = AuthoritativeNativeTownCatalog.Load(CreateContent(
            tilePropertiesTransform: text => text.Replace(
                "20\t64\tfalse",
                "20\t0\tfalse",
                StringComparison.Ordinal)));

        Assert.NotEqual(withReservedBit.Fingerprint, withoutReservedBit.Fingerprint);
        Assert.Equal((byte)64, withReservedBit.NativeSourceFlags[20]);
        Assert.Equal((byte)0, withoutReservedBit.NativeSourceFlags[20]);
        Assert.Equal(
            withReservedBit.CreateInitialGeometry().NativePieceProperties[20],
            withoutReservedBit.CreateInitialGeometry().NativePieceProperties[20]);
    }

    [Fact]
    public void GreetingBindingAllowsOnlyTheCanonicalSmithSceneryOverlap()
    {
        var catalog = LoadCatalog();
        var geometry = catalog.CreateInitialGeometry();
        var greetings = CreateGreetings(catalog.Roster);
        catalog.Roster.ValidateGreetings(greetings, geometry);

        var movedSmith = new AuthoritativeTownerGreetingCatalog(greetings.Definitions.Select(definition =>
            definition.TownerId == 0
                ? definition with { PositionX = 61 }
                : definition));
        Assert.Throws<InvalidDataException>(() => catalog.Roster.ValidateGreetings(movedSmith, geometry));
        var missingResident = new AuthoritativeTownerGreetingCatalog(
            greetings.Definitions.Where(definition => definition.TownerId != 8));
        Assert.Throws<InvalidDataException>(() => catalog.Roster.ValidateGreetings(missingResident, geometry));

        var blockedPepin = AuthoritativeTownGeometryBuilder.Build(new AuthoritativeTownGeometryRequest(
            CreateSmallMap("sector1_south", 23, 23, (8, 8, 1), (8, 9, 2), (4, 16, 3)),
            CreateSmallMap("sector2_south", 23, 23),
            CreateSmallMap("sector3_south", 23, 23),
            CreateSmallMap("sector4_south", 23, 23),
            CreateSmallMap("automap", 40, 40),
            CreatePieceCatalog(blockPepin: true),
            CreateTileProperties(),
            new AuthoritativeTownWarpPolicy(false, false, false, 1,
                AuthoritativeTownWarpUnlocks.None,
                AuthoritativeTownFarmerQuestState.Other,
                AuthoritativeTownGraveQuestState.Other),
            AuthoritativeTownEntry.Main,
            0,
            AuthoritativeTownWaterQuestState.NotAvailable,
            2588));
        Assert.Throws<InvalidDataException>(() => catalog.Roster.ValidateGreetings(greetings, blockedPepin));
    }

    [Fact]
    public void LoaderRejectsPartialReorderedMismatchedAndUnsupportedInputs()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeTownCatalog.Load(
            CreateContent(mapTilesTransform: text => text[..text.LastIndexOf("automap\t39\t39", StringComparison.Ordinal)])));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeTownCatalog.Load(
            CreateContent(mapsTransform: text => text.Replace(
                "sector1_south\t25\t25",
                "sector1_south\t24\t25",
                StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeTownCatalog.Load(
            CreateContent(mapTilesTransform: text => text.Replace(
                "sector1_south\t0\t0\t0",
                "sector1_south\t1\t0\t0",
                StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeTownCatalog.Load(
            CreateContent(settingsTransform: text => text.Replace(
                "not_available",
                "active",
                StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeTownCatalog.Load(
            CreateContent(tilePropertiesTransform: text => text.Replace(
                "20\t64\tfalse",
                "20\t1\tfalse",
                StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeTownCatalog.Load(
            CreateContent(rosterTransform: text => text.Replace(
                "smith\tGriswold the Blacksmith\tgriswold\t62\t63",
                "smith\tGriswold the Blacksmith\tgriswold\t61\t63",
                StringComparison.Ordinal))));
    }

    internal static AuthoritativeNativeTownCatalog LoadCatalog()
        => AuthoritativeNativeTownCatalog.Load(CreateContent());

    private static AuthoritativeTownerGreetingCatalog CreateGreetings(AuthoritativeNativeTownRoster roster)
        => new(roster.Humans.Select((resident, index) => new AuthoritativeTownerGreetingDefinition(
            resident.TownerId,
            AuthoritativeNativeTownCatalog.TownLevelId,
            resident.PositionX,
            resident.PositionY,
            100 + index)));

    private static AuthoritativeTownGeometry Apply(
        AuthoritativeTownGeometry geometry,
        IEnumerable<AuthoritativeNativeTownMutation> mutations)
    {
        foreach (var mutation in mutations) {
            geometry = mutation switch {
                AuthoritativeNativeTownMutation.OpenHive => AuthoritativeTownGeometryBuilder.OpenHive(geometry),
                AuthoritativeNativeTownMutation.CloseHive => AuthoritativeTownGeometryBuilder.CloseHive(geometry),
                AuthoritativeNativeTownMutation.OpenGrave => AuthoritativeTownGeometryBuilder.OpenGrave(geometry),
                AuthoritativeNativeTownMutation.CloseGrave => AuthoritativeTownGeometryBuilder.CloseGrave(geometry),
                AuthoritativeNativeTownMutation.CleanFountain => AuthoritativeTownGeometryBuilder.CleanFountain(geometry),
                _ => throw new InvalidOperationException(),
            };
        }
        return geometry;
    }

    private static ComposedContent CreateContent(
        Func<string, string>? mapTilesTransform = null,
        Func<string, string>? settingsTransform = null,
        Func<string, string>? tilePropertiesTransform = null,
        Func<string, string>? rosterTransform = null,
        Func<string, string>? mapsTransform = null)
    {
        var mapTiles = CreateMapTiles();
        var tables = new[] {
            TsvTable.Parse(AuthoritativeNativeTownCatalog.IdentityTableName,
                "level_id\tlevel_symbol\tgenerator_id\tgenerator_revision\n"
                + "0\ttown\toriginal-diablo.town\t1\n"),
            TsvTable.Parse(AuthoritativeNativeTownCatalog.SettingsTableName,
                (settingsTransform ?? (text => text))(
                    "is_spawn\tis_multiplayer\tis_hellfire\tcharacter_level\twarp_unlocks\tfarmer_quest_state\tgrave_quest_state\tentry\ttown_warp_from\twater_quest_state\tinitial_random_state\n"
                    + "0\t0\t0\t1\t0\tother\tother\tmain\t0\tnot_available\t2588\n")),
            TsvTable.Parse(AuthoritativeNativeTownCatalog.MapsTableName,
                (mapsTransform ?? (text => text))(
                    "map_id\tmega_width\tmega_height\n"
                + "sector1_south\t25\t25\n"
                + "sector2_south\t25\t23\n"
                + "sector3_south\t23\t25\n"
                + "sector4_south\t23\t23\n"
                + "automap\t40\t40\n")),
            TsvTable.Parse(AuthoritativeNativeTownCatalog.MapTilesTableName,
                (mapTilesTransform ?? (text => text))(mapTiles)),
            TsvTable.Parse(AuthoritativeNativeTownCatalog.PieceDefinitionsTableName, CreatePieceDefinitions()),
            TsvTable.Parse(AuthoritativeNativeTownCatalog.TilePropertiesTableName,
                (tilePropertiesTransform ?? (text => text))(CreateTilePropertiesTable())),
            TsvTable.Parse(AuthoritativeNativeTownRoster.TableName,
                (rosterTransform ?? (text => text))(CreateRosterTable())),
        };
        return ComposedContent.Compose(new ContentManifest(
            "town-catalog-test",
            "1.0.0",
            [new ContentPack("town-profile-test", "1.0.0", tables)]));
    }

    private static string CreateMapTiles()
    {
        var output = new StringBuilder("map_id\tx\ty\tmega_tile_id\n");
        foreach (var (mapId, width, height) in new[] {
            ("sector1_south", 25, 25),
            ("sector2_south", 25, 23),
            ("sector3_south", 23, 25),
            ("sector4_south", 23, 23),
            ("automap", 40, 40),
        }) {
            for (var y = 0; y < height; y++) {
                for (var x = 0; x < width; x++) {
                    var tile = mapId switch {
                        "sector1_south" when (x, y) == (8, 8) => 1,
                        "sector1_south" when (x, y) == (8, 9) => 2,
                        "automap" when (x, y) == (0, 0) => 7,
                        _ => 0,
                    };
                    output.Append(mapId).Append('\t')
                        .Append(x.ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(y.ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(tile.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
            }
        }
        return output.ToString();
    }

    private static string CreatePieceDefinitions()
    {
        var output = new StringBuilder(
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n");
        for (var id = 0; id < 400; id++) {
            var pieces = id switch {
                0 => (359, 909, 923, 909),
                1 => (909, 909, 909, 909),
                _ => (426, 426, 426, 426),
            };
            output.Append(id).Append('\t')
                .Append(pieces.Item1).Append('\t')
                .Append(pieces.Item2).Append('\t')
                .Append(pieces.Item3).Append('\t')
                .Append(pieces.Item4).Append('\n');
        }
        return output.ToString();
    }

    private static string CreateTilePropertiesTable()
    {
        var output = new StringBuilder(
            "piece_id\tsource_flags\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n");
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++) {
            var flags = id switch {
                923 => (byte)AuthoritativeDunTileFlags.Solid,
                20 => (byte)0x40,
                _ => (byte)0,
            };
            output.Append(id).Append('\t').Append(flags).Append('\t')
                .Append(Bit(flags, AuthoritativeDunTileFlags.Solid)).Append('\t')
                .Append(Bit(flags, AuthoritativeDunTileFlags.BlockLight)).Append('\t')
                .Append(Bit(flags, AuthoritativeDunTileFlags.BlockMissile)).Append('\t')
                .Append(Bit(flags, AuthoritativeDunTileFlags.Transparent)).Append('\t')
                .Append(Bit(flags, AuthoritativeDunTileFlags.TransparentLeft)).Append('\t')
                .Append(Bit(flags, AuthoritativeDunTileFlags.TransparentRight)).Append('\t')
                .Append(Bit(flags, AuthoritativeDunTileFlags.Trap)).Append('\n');
        }
        return output.ToString();
    }

    private static string Bit(byte raw, AuthoritativeDunTileFlags flag)
        => ((raw & (byte)flag) != 0) ? "true" : "false";

    private static string CreateRosterTable()
    {
        var output = new StringBuilder(
            "towner_id\ttowner_symbol\tname\tshort_name\tposition_x\tposition_y\tdirection\n");
        foreach (var resident in CreateRoster()) {
            output.Append(resident.TownerId).Append('\t')
                .Append(resident.Symbol).Append('\t')
                .Append(resident.Name).Append('\t')
                .Append(resident.ShortName).Append('\t')
                .Append(resident.PositionX).Append('\t')
                .Append(resident.PositionY).Append('\t')
                .Append(resident.Direction).Append('\n');
        }
        return output.ToString();
    }

    private static IReadOnlyList<AuthoritativeNativeTownResident> CreateRoster()
        => [
            new(0, "smith", "Griswold the Blacksmith", "griswold", 62, 63, "south_west"),
            new(1, "healer", "Pepin the Healer", "pepin", 55, 79, "south_east"),
            new(3, "tavern", "Ogden the Tavern owner", "ogden", 55, 62, "south_west"),
            new(4, "story", "Cain the Elder", "cain", 62, 71, "south"),
            new(5, "drunk", "Farnham the Drunk", "farnham", 71, 84, "south"),
            new(6, "witch", "Adria the Witch", "adria", 80, 20, "south"),
            new(7, "barmaid", "Gillian the Barmaid", "gillian", 43, 66, "south"),
            new(8, "pegboy", "Wirt the Peg-legged boy", "wirt", 11, 53, "south"),
            new(9, "cow", "Cow", "cow", 58, 16, "south_west"),
            new(9, "cow", "Cow", "cow", 56, 14, "north_west"),
            new(9, "cow", "Cow", "cow", 59, 20, "north"),
        ];

    private static AuthoritativeTownTileMap CreateSmallMap(
        string source,
        int width,
        int height,
        params (int X, int Y, ushort Value)[] nonZero)
    {
        var tiles = new ushort[width * height];
        foreach (var (x, y, value) in nonZero)
            tiles[y * width + x] = value;
        var bytes = new byte[4 + tiles.Length * 2];
        bytes[0] = (byte)width;
        bytes[2] = (byte)height;
        for (var index = 0; index < tiles.Length; index++) {
            bytes[4 + index * 2] = (byte)tiles[index];
            bytes[5 + index * 2] = (byte)(tiles[index] >> 8);
        }
        return AuthoritativeTownTileMap.Load(source, bytes);
    }

    private static AuthoritativeDunPieceCatalog CreatePieceCatalog(bool blockPepin = false)
        => new(Enumerable.Range(0, 400).Select(id => {
            var pieces = id switch {
                0 => (359U, 909U, 923U, 909U),
                1 => (909U, 909U, 909U, 909U),
                2 when blockPepin => (909U, 909U, 909U, 923U),
                _ => (426U, 426U, 426U, 426U),
            };
            return new AuthoritativeDunPieceDefinition(
                (uint)id,
                pieces.Item1,
                pieces.Item2,
                pieces.Item3,
                pieces.Item4);
        }));

    private static AuthoritativeDunTilePropertyCatalog CreateTileProperties()
        => new(Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            .Select(id => new AuthoritativeDunTileProperty(
                (uint)id,
                id == 923 ? AuthoritativeDunTileFlags.Solid : AuthoritativeDunTileFlags.None)));
}
