using System.Buffers.Binary;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeTownGeometryTests
{
    private static readonly AuthoritativeDunPieceCatalog PieceCatalog = CreatePieceCatalog();
    private static readonly AuthoritativeDunTilePropertyCatalog TileProperties = CreateTileProperties();

    [Fact]
    public void TownDunReadsOnlyItsConsumedTileChannelAndRejectsMalformedInput()
    {
        var bytes = new byte[] { 2, 0, 1, 0, 1, 0, 9, 0 };
        var map = AuthoritativeTownTileMap.Load("sector.dun", bytes);
        Assert.Equal((2, 1), (map.MegaWidth, map.MegaHeight));
        Assert.Equal(new ushort[] { 1, 9 }, map.TileLayer);
        bytes[4] = 7;
        Assert.Equal((ushort)1, map.TileLayer[0]);
        Assert.Equal(map.TileLayer, AuthoritativeTownTileMap.Load("sector.dun", new byte[] {
            2, 0, 1, 0, 1, 0, 9, 0, 22, 0,
        }).TileLayer);
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownTileMap.Load("sector.dun", bytes[..6]));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownTileMap.Load("sector.dun", bytes[..3]));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownTileMap.Load("sector.dun", new byte[] { 0, 0, 1, 0 }));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownTileMap.Load("sector.dun", new byte[] { 57, 0, 1, 0 }));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownTileMap.Load("sector.dun", new byte[AuthoritativeTownTileMap.MaximumSourceBytes + 2]));
    }

    [Fact]
    public void SectorMayEndExactlyAtTheFineGridBoundary()
    {
        var request = CreateRequest() with {
            Sector1South = CreateDun(33, 33, Enumerable.Repeat((ushort)1, 33 * 33).ToArray()),
        };
        var geometry = AuthoritativeTownGeometryBuilder.Build(request);
        Assert.Equal(new ushort[] { 359, 100, 101, 102 }, ReadMega(geometry.NativePieceLayer, 110, 110));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownGeometryBuilder.Build(request with {
            Sector1South = CreateDun(34, 33, Enumerable.Repeat((ushort)1, 34 * 33).ToArray()),
        }));
    }

    [Fact]
    public void BuildExpandsSectorsAndOverlaysAutomapIntoExplicitTownChannels()
    {
        var initialMega = new ushort[AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight];
        initialMega[1] = 19;
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            sectorIds: [1, 0, 2, 1],
            automapTiles: [(0, 0, (ushort)7)],
            initialMegaTiles: initialMega,
            initialProtectedMegaCells: [1]));

        Assert.Equal(112, AuthoritativeTownGeometry.FineWidth);
        Assert.Equal(112, AuthoritativeTownGeometry.FineHeight);
        Assert.Equal(40, AuthoritativeTownGeometry.MegaWidth);
        Assert.Equal(40, AuthoritativeTownGeometry.MegaHeight);
        Assert.Equal(112 * 112, geometry.NativePieceLayer.Count);
        Assert.Equal(40 * 40, geometry.NativeMegaTileLayer.Count);
        Assert.Equal(112 * 112, geometry.NativeSpecialLayer.Count);
        Assert.Equal(112 * 112, geometry.NativeSolPropertyLayer.Count);
        Assert.Equal(112 * 112, geometry.NativeTransparencyLayer.Count);

        Assert.Equal(new ushort[] { 359, 100, 101, 102 },
            ReadMega(geometry.NativePieceLayer, 0, 0));
        Assert.Equal(new ushort[] { 301, 302, 303, 304 },
            ReadMega(geometry.NativePieceLayer, 0, 46));
        Assert.Equal(new ushort[] { 218, 218, 218, 218 },
            ReadMega(geometry.NativePieceLayer, 46, 0));
        Assert.Equal(new ushort[] { 359, 100, 101, 102 },
            ReadMega(geometry.NativePieceLayer, 46, 46));
        Assert.Equal((ushort)426, Fine(geometry.NativePieceLayer, 10, 100));

        Assert.Equal((ushort)7, Mega(geometry.NativeMegaTileLayer, 0, 0));
        Assert.Equal((ushort)19, Mega(geometry.NativeMegaTileLayer, 1, 0));
        Assert.Equal(new[] { 0, 1 }, geometry.NativeProtectedMegaCells);
        Assert.Equal((ushort)1, Fine(geometry.NativeSpecialLayer, 0, 0));
        Assert.Equal(10, geometry.MinimumDungeonPositionX);
        Assert.Equal(10, geometry.MinimumDungeonPositionY);
        Assert.Equal(84, geometry.MaximumDungeonPositionX);
        Assert.Equal(84, geometry.MaximumDungeonPositionY);
        Assert.Equal(AuthoritativeTownWarpPolicy.Version, geometry.WarpPolicyVersion);
    }

    [Fact]
    public void WarpPolicyMatchesNativeSpawnMultiplayerProgressionAndQuestRules()
    {
        var unlockedSpawn = Policy(
            spawn: true,
            multiplayer: true,
            hellfire: true,
            characterLevel: 30,
            unlocks: AllNormalUnlocks,
            farmer: AuthoritativeTownFarmerQuestState.Done,
            grave: AuthoritativeTownGraveQuestState.Done);
        Assert.Equal(new AuthoritativeTownWarpOpenings(false, false, false, false, false),
            unlockedSpawn.ResolveOpenings());

        var multiplayer = Policy(multiplayer: true);
        Assert.Equal(new AuthoritativeTownWarpOpenings(true, true, true, false, true),
            multiplayer.ResolveOpenings());

        var legacyProgression = Policy(characterLevel: 30);
        Assert.Equal(new AuthoritativeTownWarpOpenings(false, false, false, false, false),
            legacyProgression.ResolveOpenings());

        var hellfireProgression = Policy(hellfire: true, characterLevel: 14);
        Assert.Equal(new AuthoritativeTownWarpOpenings(true, false, false, false, false),
            hellfireProgression.ResolveOpenings());
        Assert.True(Policy(hellfire: true, characterLevel: 15)
            .IsWarpOpen(AuthoritativeTownWarp.Caves));
        Assert.True(Policy(hellfire: true, characterLevel: 20)
            .IsWarpOpen(AuthoritativeTownWarp.Hell));
        Assert.True(Policy(hellfire: true, farmer: AuthoritativeTownFarmerQuestState.Done)
            .IsWarpOpen(AuthoritativeTownWarp.Nest));
        Assert.True(Policy(hellfire: true, farmer: AuthoritativeTownFarmerQuestState.HiveDone)
            .IsWarpOpen(AuthoritativeTownWarp.Nest));
        Assert.True(Policy(hellfire: true, grave: AuthoritativeTownGraveQuestState.Done)
            .IsWarpOpen(AuthoritativeTownWarp.Crypt));
        Assert.False(Policy(farmer: AuthoritativeTownFarmerQuestState.Done)
            .IsWarpOpen(AuthoritativeTownWarp.Nest));
        Assert.False(Policy(grave: AuthoritativeTownGraveQuestState.Done)
            .IsWarpOpen(AuthoritativeTownWarp.Crypt));
    }

    [Fact]
    public void ClosedHellConsumesTenOrderedDrawsAndAllClosedGatesApplyNativeGeometry()
    {
        const uint seed = 0x91A2B3C4;
        var request = CreateRequest(
            policy: Policy(
                spawn: true,
                hellfire: true,
                unlocks: AllNormalUnlocks,
                farmer: AuthoritativeTownFarmerQuestState.Other,
                grave: AuthoritativeTownGraveQuestState.Other),
            automapTiles: [(20, 7, (ushort)3)],
            seed: seed);
        var geometry = AuthoritativeTownGeometryBuilder.Build(request);
        var expectedRandom = new AuthoritativeLegacyRandom(seed);
        var expectedChoices = Enumerable.Range(0, 10)
            .Select(_ => Math.Max(expectedRandom.GenerateRnd(4), 0) + 1)
            .ToArray();

        Assert.Equal(expectedChoices, geometry.ClosedHellTileChoices);
        Assert.Equal(expectedRandom.State, geometry.FinalRandomState);
        Assert.Equal(seed, geometry.InitialRandomState);
        Assert.Equal(new AuthoritativeTownWarpOpenings(false, false, false, false, false),
            geometry.WarpOpenings);
        Assert.Equal((ushort)10, Mega(geometry.NativeMegaTileLayer, 20, 7));
        Assert.Equal((ushort)8, Mega(geometry.NativeMegaTileLayer, 20, 6));
        Assert.Equal((ushort)8, Mega(geometry.NativeMegaTileLayer, 4, 30));
        Assert.Equal((ushort)7, Mega(geometry.NativeMegaTileLayer, 15, 35));
        Assert.Equal((ushort)7, Mega(geometry.NativeMegaTileLayer, 16, 35));
        Assert.Equal((ushort)7, Mega(geometry.NativeMegaTileLayer, 17, 35));
        Assert.Contains(7 * AuthoritativeTownGeometry.MegaWidth + 20,
            geometry.NativeProtectedMegaCells);
        Assert.Equal((ushort)0x489, Fine(geometry.NativePieceLayer, 78, 60));
        Assert.Equal((ushort)0x52A, Fine(geometry.NativePieceLayer, 36, 21));
        Assert.Equal((ushort)18, Mega(geometry.NativeMegaTileLayer, 35, 27));
        Assert.Equal((ushort)63, Mega(geometry.NativeMegaTileLayer, 36, 27));
        Assert.Equal((ushort)0, Mega(geometry.NativeMegaTileLayer, 14, 8));
        Assert.Equal((ushort)0, Mega(geometry.NativeMegaTileLayer, 14, 7));

        for (var index = 0; index < expectedChoices.Length; index++) {
            var expectedNorthWest = PieceCatalog.TryGet((uint)(expectedChoices[index] - 1), out var mega)
                ? checked((ushort)mega.NorthWestPieceId)
                : throw new InvalidOperationException("Expected fixture mega-tile is missing.");
            Assert.Equal(expectedNorthWest, Fine(geometry.NativePieceLayer, 36 + index, 78));
        }
        Assert.Equal((ushort)1364, Fine(geometry.NativePieceLayer, 60, 70));
        Assert.Empty(geometry.NativeTransparencyLayer.Where(value => value != 0));
    }

    [Fact]
    public void OpenGatesSkipClosedHellDrawsAndWaterAvailabilitySelectsFountainTile()
    {
        const uint seed = 0x10203040;
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            policy: Policy(unlocks: AllNormalUnlocks),
            waterQuestState: AuthoritativeTownWaterQuestState.NotAvailable,
            seed: seed));

        Assert.Empty(geometry.ClosedHellTileChoices);
        Assert.Equal(seed, geometry.FinalRandomState);
        Assert.Equal(new ushort[] { 280, 281, 282, 283 },
            ReadMega(geometry.NativePieceLayer, 60, 70));
        Assert.Equal((ushort)426, Fine(geometry.NativePieceLayer, 78, 60));
        Assert.Equal((ushort)0, Mega(geometry.NativeMegaTileLayer, 35, 27));

        var done = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            policy: Policy(unlocks: AllNormalUnlocks),
            waterQuestState: AuthoritativeTownWaterQuestState.Done));
        Assert.Equal(new ushort[] { 280, 281, 282, 283 }, ReadMega(done.NativePieceLayer, 60, 70));

        var initializedQuest = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            policy: Policy(unlocks: AllNormalUnlocks),
            waterQuestState: AuthoritativeTownWaterQuestState.Init));
        var activeQuest = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            policy: Policy(unlocks: AllNormalUnlocks),
            waterQuestState: AuthoritativeTownWaterQuestState.Active));
        Assert.Equal(new ushort[] { 1364, 1365, 1366, 1367 },
            ReadMega(initializedQuest.NativePieceLayer, 60, 70));
        Assert.Equal(initializedQuest.NativePieceLayer, activeQuest.NativePieceLayer);
    }

    [Fact]
    public void HellfireQuestOpeningsApplyOpenHiveAndGraveGeometry()
    {
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            policy: Policy(
                hellfire: true,
                unlocks: AllNormalUnlocks,
                farmer: AuthoritativeTownFarmerQuestState.HiveDone,
                grave: AuthoritativeTownGraveQuestState.Done)));

        Assert.Equal(new AuthoritativeTownWarpOpenings(true, true, true, true, true),
            geometry.WarpOpenings);
        Assert.Equal((ushort)47, Mega(geometry.NativeMegaTileLayer, 36, 27));
        Assert.Equal((ushort)0x50D, Fine(geometry.NativePieceLayer, 79, 61));
        Assert.Equal((ushort)47, Mega(geometry.NativeMegaTileLayer, 14, 8));
        Assert.Equal((ushort)47, Mega(geometry.NativeMegaTileLayer, 14, 7));
        Assert.Equal((ushort)0x532, Fine(geometry.NativePieceLayer, 36, 21));
        Assert.Empty(geometry.ClosedHellTileChoices);
    }

    [Fact]
    public void LoadRetainsZeroAutomapMegaCellsProtectedBitsAndUnmappedSpecials()
    {
        var initialMega = Enumerable.Repeat((ushort)19,
            AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight).ToArray();
        var initialSpecial = Enumerable.Repeat((ushort)31,
            AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight).ToArray();
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            entry: AuthoritativeTownEntry.Load,
            existingViewX: 41,
            existingViewY: 42,
            automapTiles: [(0, 0, (ushort)7)],
            initialMegaTiles: initialMega,
            initialProtectedMegaCells: [2, 12],
            initialSpecialLayer: initialSpecial));

        Assert.Equal((ushort)7, Mega(geometry.NativeMegaTileLayer, 0, 0));
        Assert.Equal((ushort)19, Mega(geometry.NativeMegaTileLayer, 1, 0));
        Assert.Equal(new[] { 0, 2, 12 }, geometry.NativeProtectedMegaCells);
        Assert.Equal((ushort)1, Fine(geometry.NativeSpecialLayer, 0, 0));
        Assert.Equal((ushort)31, Fine(geometry.NativeSpecialLayer, 10, 10));
        Assert.Equal(41, geometry.ViewPositionX);
        Assert.Equal(42, geometry.ViewPositionY);
        Assert.All(geometry.NativeTransparencyLayer, value => Assert.Equal((ushort)0, value));
    }

    [Fact]
    public void FreshInitializationClearsFineSpecialsButRetainsMegaAndProtectedState()
    {
        var initialMega = Enumerable.Repeat((ushort)19,
            AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight).ToArray();
        var initialSpecial = Enumerable.Repeat((ushort)31,
            AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight).ToArray();
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            entry: AuthoritativeTownEntry.Main,
            initialMegaTiles: initialMega,
            initialProtectedMegaCells: [12],
            initialSpecialLayer: initialSpecial));

        Assert.Equal((ushort)19, Mega(geometry.NativeMegaTileLayer, 1, 0));
        Assert.Equal(new[] { 12 }, geometry.NativeProtectedMegaCells);
        Assert.Equal((ushort)0, Fine(geometry.NativeSpecialLayer, 10, 10));
        Assert.Equal((ushort)1, Fine(geometry.NativeSpecialLayer, 0, 0));
        Assert.Equal((75, 68), (geometry.ViewPositionX, geometry.ViewPositionY));
    }

    [Theory]
    [InlineData(AuthoritativeTownEntry.Main, 0, 75, 68)]
    [InlineData(AuthoritativeTownEntry.Previous, 0, 25, 31)]
    [InlineData(AuthoritativeTownEntry.TownWarpUp, 5, 49, 22)]
    [InlineData(AuthoritativeTownEntry.TownWarpUp, 9, 18, 69)]
    [InlineData(AuthoritativeTownEntry.TownWarpUp, 13, 41, 81)]
    [InlineData(AuthoritativeTownEntry.TownWarpUp, 21, 36, 25)]
    [InlineData(AuthoritativeTownEntry.TownWarpUp, 17, 79, 62)]
    [InlineData(AuthoritativeTownEntry.TownWarpUp, 99, 41, 42)]
    [InlineData(AuthoritativeTownEntry.ReturnLevel, 0, 41, 42)]
    public void EntrySelectsNativeTownViewPosition(
        AuthoritativeTownEntry entry,
        int townWarpFrom,
        int expectedX,
        int expectedY)
    {
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            entry: entry,
            townWarpFrom: townWarpFrom,
            existingViewX: 41,
            existingViewY: 42));

        Assert.Equal(expectedX, geometry.ViewPositionX);
        Assert.Equal(expectedY, geometry.ViewPositionY);
    }

    [Fact]
    public void LaterHiveGraveAndFountainOperationsRetainNativeAsymmetriesAndOtherChannels()
    {
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            policy: Policy(spawn: true, hellfire: true),
            entry: AuthoritativeTownEntry.Load,
            initialSpecialLayer: Enumerable.Repeat((ushort)31,
                AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight).ToArray()));
        var openHive = AuthoritativeTownGeometryBuilder.OpenHive(geometry);
        Assert.Equal((ushort)47, Mega(openHive.NativeMegaTileLayer, 36, 27));
        Assert.Equal((ushort)18, Mega(openHive.NativeMegaTileLayer, 35, 27));
        Assert.Equal((ushort)0x48A, Fine(openHive.NativePieceLayer, 79, 60));
        Assert.Equal((ushort)31, Fine(openHive.NativeSpecialLayer, 79, 60));

        var closedHive = AuthoritativeTownGeometryBuilder.CloseHive(openHive);
        Assert.Equal((ushort)18, Mega(closedHive.NativeMegaTileLayer, 35, 27));
        Assert.Equal((ushort)63, Mega(closedHive.NativeMegaTileLayer, 36, 27));
        Assert.Equal((ushort)0x4EA, Fine(closedHive.NativePieceLayer, 79, 60));

        var openGrave = AuthoritativeTownGeometryBuilder.OpenGrave(closedHive);
        Assert.Equal((ushort)47, Mega(openGrave.NativeMegaTileLayer, 14, 8));
        Assert.Equal((ushort)47, Mega(openGrave.NativeMegaTileLayer, 14, 7));
        Assert.Equal((ushort)0x532, Fine(openGrave.NativePieceLayer, 36, 21));
        var closedGrave = AuthoritativeTownGeometryBuilder.CloseGrave(openGrave);
        Assert.Equal((ushort)47, Mega(closedGrave.NativeMegaTileLayer, 14, 8));
        Assert.Equal((ushort)0x52A, Fine(closedGrave.NativePieceLayer, 36, 21));

        var clean = AuthoritativeTownGeometryBuilder.CleanFountain(closedGrave);
        Assert.Equal(new ushort[] { 280, 281, 282, 283 }, ReadMega(clean.NativePieceLayer, 60, 70));
        Assert.Equal((ushort)426, Fine(clean.NativePieceLayer, 10, 10));
        Assert.Equal(closedGrave.NativeSpecialLayer, clean.NativeSpecialLayer);
        Assert.Equal(closedGrave.NativeTransparencyLayer, clean.NativeTransparencyLayer);
        Assert.Contains(Cell(78, 60), openHive.NativeSolidCells);
        Assert.True(openHive.NativeSolPropertyLayer[Cell(78, 60)]
            .HasFlag(AuthoritativeDunTileFlags.Solid));
    }

    [Fact]
    public void SolFlagsRemainSeparateForSolidMissileAndBlockLightChannels()
    {
        var geometry = AuthoritativeTownGeometryBuilder.Build(CreateRequest(
            sectorIds: [0, 0, 0, 1],
            policy: Policy(unlocks: AllNormalUnlocks)));
        Assert.Equal(AuthoritativeDunTileFlags.Solid,
            geometry.NativeSolPropertyLayer[Cell(0, 0)]);
        Assert.Equal(AuthoritativeDunTileFlags.BlockMissile,
            geometry.NativeSolPropertyLayer[Cell(1, 0)]);
        Assert.Equal(AuthoritativeDunTileFlags.BlockLight,
            geometry.NativeSolPropertyLayer[Cell(0, 1)]);
        Assert.Contains(Cell(0, 0), geometry.NativeSolidCells);
        Assert.DoesNotContain(Cell(0, 0), geometry.NativeMissileBlockedCells);
        Assert.Contains(Cell(1, 0), geometry.NativeMissileBlockedCells);
        Assert.DoesNotContain(Cell(1, 0), geometry.NativeBlockLightCells);
        Assert.Contains(Cell(0, 1), geometry.NativeBlockLightCells);
        Assert.DoesNotContain(Cell(0, 1), geometry.NativeSolidCells);
    }

    [Fact]
    public void RejectsUnsafeAssetAndRetainedStateShapes()
    {
        var normal = CreateRequest();
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownGeometryBuilder.Build(
            normal with { Automap = CreateDun(1, 1, [0]) }));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownGeometryBuilder.Build(
            normal with { Sector1South = CreateDun(40, 40, new ushort[1600]) }));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownGeometryBuilder.Build(
            normal with { InitialMegaTileLayer = new ushort[4] }));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownGeometryBuilder.Build(
            normal with { InitialProtectedMegaCells = [1600] }));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownGeometryBuilder.Build(
            normal with { InitialSpecialLayer = new ushort[1] }));
        Assert.Throws<InvalidDataException>(() => AuthoritativeTownGeometryBuilder.Build(
            normal with { Entry = AuthoritativeTownEntry.Load, InitialSpecialLayer = null }));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeTownGeometryBuilder.Build(
            normal with { WarpPolicy = Policy(characterLevel: 51) }));
    }

    [Fact(Explicit = true)]
    public void MatchesNativeTownGeometryOracleWhenReferenceDirectoryIsConfigured()
    {
        var referenceDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_NATIVE_SCENE_REFERENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(referenceDirectory))
            throw new InvalidOperationException("Explicit native Town reference directory is required.");
        if (!Directory.Exists(referenceDirectory))
            throw new DirectoryNotFoundException(referenceDirectory);

        var townDirectory = Path.Combine(referenceDirectory, "Town");
        var inputDirectory = Path.Combine(referenceDirectory, "levels", "towndata");
        var table = TsvTable.Parse(
            "native-town-geometry.tsv",
            File.ReadAllText(Path.Combine(townDirectory, "geometry.tsv")));
        Assert.Equal(191, table.Rows.Count);
        Assert.Equal(191, table.Rows.Select(row => row.Required("file")).Distinct(StringComparer.Ordinal).Count());
        var matrix = table.Rows.Take(160).ToArray();
        Assert.All(matrix, row => {
            Assert.False(row.RequiredBoolean("dirty"));
            Assert.Equal(0, row.RequiredInt32("entry"));
            Assert.Equal(0, row.RequiredInt32("warp_from"));
            Assert.Equal("none", row.Required("mutation"));
        });
        var matrixKeys = matrix.Select(row => string.Join(",", new[] {
            "seed", "hellfire", "warps", "water_state", "hive", "grave",
        }.Select(row.Required))).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(160, matrixKeys.Count);
        foreach (var seed in new uint[] { 2588, 0x80000000 })
        foreach (var hf in new[] { 0, 1 })
        foreach (var warps in Enumerable.Range(0, 8))
        foreach (var water in new[] { 2, 3 })
        foreach (var quest in Enumerable.Range(0, hf == 0 ? 1 : 4))
            Assert.Contains($"{seed},{hf},{warps},{water},{quest & 1},{(quest >> 1) & 1}", matrixKeys);
        var sectors = new[] {
            LoadDun(inputDirectory, "sector1s.dun"),
            LoadDun(inputDirectory, "sector2s.dun"),
            LoadDun(inputDirectory, "sector3s.dun"),
            LoadDun(inputDirectory, "sector4s.dun"),
        };
        var automap = LoadDun(inputDirectory, "automap.dun");
        var pieces = AuthoritativeDunPieceCatalog.LoadTil(
            "levels/towndata/town.til",
            File.ReadAllBytes(Path.Combine(inputDirectory, "town.til")));
        var sourceSol = File.ReadAllBytes(Path.Combine(inputDirectory, "town.sol"));
        Assert.InRange(sourceSol.Length, 1, AuthoritativeDunTilePropertyCatalog.NativeTileCount);
        var zeroInitializedSol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        sourceSol.CopyTo(zeroInitializedSol, 0);
        var properties = AuthoritativeDunTilePropertyCatalog.LoadSol(
            "levels/towndata/town.sol", zeroInitializedSol);

        foreach (var row in table.Rows) {
            var dirty = row.RequiredBoolean("dirty");
            var hellfire = row.RequiredBoolean("hellfire");
            var farmerDone = row.RequiredBoolean("hive");
            var graveDone = row.RequiredBoolean("grave");
            var waterState = (AuthoritativeTownWaterQuestState)row.RequiredInt32("water_state");
            if (!Enum.IsDefined(waterState))
                throw new InvalidDataException("Native Town fixture has an unsupported water quest state.");
            Assert.Equal(
                waterState is AuthoritativeTownWaterQuestState.Init
                    or AuthoritativeTownWaterQuestState.Active,
                row.RequiredBoolean("water"));

            var unlocks = (AuthoritativeTownWarpUnlocks)row.RequiredInt32("warps");
            var entry = (AuthoritativeTownEntry)row.RequiredInt32("entry");
            if (!Enum.IsDefined(entry))
                throw new InvalidDataException("Native Town fixture has an unsupported entry value.");

            ushort[]? initialMega = null;
            int[]? protectedCells = null;
            ushort[]? initialSpecial = null;
            if (dirty) {
                initialMega = Enumerable.Repeat((ushort)19,
                    AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight).ToArray();
                protectedCells = Enumerable.Range(0,
                        AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight)
                    .Where(cell => (cell % AuthoritativeTownGeometry.MegaWidth
                        + cell / AuthoritativeTownGeometry.MegaWidth) % 3 == 0)
                    .ToArray();
                initialSpecial = Enumerable.Repeat((ushort)31,
                    AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight).ToArray();
            }
            else if (entry == AuthoritativeTownEntry.Load) {
                // ENTRY_LOAD preserves the incoming fine special layer even when it is all zero.
                initialSpecial = new ushort[
                    AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight];
            }

            var request = new AuthoritativeTownGeometryRequest(
                sectors[0],
                sectors[1],
                sectors[2],
                sectors[3],
                automap,
                pieces,
                properties,
                Policy(
                    hellfire: hellfire,
                    characterLevel: 1,
                    unlocks: unlocks,
                    farmer: farmerDone
                        ? AuthoritativeTownFarmerQuestState.Done
                        : AuthoritativeTownFarmerQuestState.Other,
                    grave: graveDone
                        ? AuthoritativeTownGraveQuestState.Done
                        : AuthoritativeTownGraveQuestState.Other),
                entry,
                row.RequiredInt32("warp_from"),
                waterState,
                row.RequiredUInt32("seed"),
                41,
                42,
                initialMega,
                protectedCells,
                initialSpecial);
            var geometry = AuthoritativeTownGeometryBuilder.Build(request);
            geometry = ApplyFixtureMutation(geometry, row.Required("mutation"));
            var outputPath = Path.Combine(townDirectory, row.Required("file"));
            using var stream = File.OpenRead(outputPath);
            using var reader = new BinaryReader(stream);

            var label = row.Required("file");
            AssertOracleUInt32(reader, geometry.FinalRandomState, label, "final random state");
            AssertOracleUInt32(reader, checked((uint)geometry.ViewPositionX), label, "view x");
            AssertOracleUInt32(reader, checked((uint)geometry.ViewPositionY), label, "view y");
            AssertOracleUInt32(reader, checked((uint)geometry.MinimumDungeonPositionX), label, "minimum x");
            AssertOracleUInt32(reader, checked((uint)geometry.MinimumDungeonPositionY), label, "minimum y");
            AssertOracleUInt32(reader, checked((uint)geometry.MaximumDungeonPositionX), label, "maximum x");
            AssertOracleUInt32(reader, checked((uint)geometry.MaximumDungeonPositionY), label, "maximum y");

            for (var cell = 0; cell < geometry.NativePieceLayer.Count; cell++) {
                AssertOracleUInt32(reader, geometry.NativePieceLayer[cell], label, $"piece[{cell}]");
                AssertOracleUInt32(reader, geometry.NativeSpecialLayer[cell], label, $"special[{cell}]");
                AssertOracleUInt32(reader, (byte)geometry.NativeSolPropertyLayer[cell], label,
                    $"properties[{cell}]");
                AssertOracleUInt32(reader, geometry.NativeTransparencyLayer[cell], label,
                    $"transparency[{cell}]");
            }
            var protectedMega = geometry.NativeProtectedMegaCells.ToHashSet();
            for (var cell = 0; cell < geometry.NativeMegaTileLayer.Count; cell++) {
                AssertOracleUInt32(reader, geometry.NativeMegaTileLayer[cell], label, $"mega[{cell}]");
                AssertOracleUInt32(reader, protectedMega.Contains(cell) ? 1U : 0U, label,
                    $"protected[{cell}]");
            }
            Assert.Equal(stream.Length, stream.Position);
        }
    }

    private static AuthoritativeTownGeometryRequest CreateRequest(
        IReadOnlyList<ushort>? sectorIds = null,
        IReadOnlyList<(int X, int Y, ushort Value)>? automapTiles = null,
        AuthoritativeTownWarpPolicy? policy = null,
        AuthoritativeTownEntry entry = AuthoritativeTownEntry.Main,
        int townWarpFrom = 0,
        AuthoritativeTownWaterQuestState waterQuestState = AuthoritativeTownWaterQuestState.Active,
        uint seed = 0x12345678,
        int existingViewX = 0,
        int existingViewY = 0,
        IReadOnlyList<ushort>? initialMegaTiles = null,
        IReadOnlyList<int>? initialProtectedMegaCells = null,
        IReadOnlyList<ushort>? initialSpecialLayer = null)
    {
        var ids = sectorIds ?? new ushort[] { 1, 0, 2, 1 };
        if (ids.Count != 4)
            throw new ArgumentException("Four sector IDs are required.", nameof(sectorIds));
        return new AuthoritativeTownGeometryRequest(
            CreateDun(1, 1, [ids[0]]),
            CreateDun(1, 1, [ids[1]]),
            CreateDun(1, 1, [ids[2]]),
            CreateDun(1, 1, [ids[3]]),
            CreateDun(40, 40, CreateAutomapLayer(automapTiles)),
            PieceCatalog,
            TileProperties,
            policy ?? Policy(unlocks: AllNormalUnlocks),
            entry,
            townWarpFrom,
            waterQuestState,
            seed,
            existingViewX,
            existingViewY,
            initialMegaTiles,
            initialProtectedMegaCells,
            initialSpecialLayer);
    }

    private static AuthoritativeTownWarpPolicy Policy(
        bool spawn = false,
        bool multiplayer = false,
        bool hellfire = false,
        int characterLevel = 1,
        AuthoritativeTownWarpUnlocks unlocks = AuthoritativeTownWarpUnlocks.None,
        AuthoritativeTownFarmerQuestState farmer = AuthoritativeTownFarmerQuestState.Other,
        AuthoritativeTownGraveQuestState grave = AuthoritativeTownGraveQuestState.Other)
        => new(spawn, multiplayer, hellfire, characterLevel, unlocks, farmer, grave);

    private static AuthoritativeTownTileMap LoadDun(string directory, string file)
        => AuthoritativeTownTileMap.Load(
            $"levels/towndata/{file}",
            File.ReadAllBytes(Path.Combine(directory, file)));

    private static AuthoritativeTownGeometry ApplyFixtureMutation(
        AuthoritativeTownGeometry geometry,
        string mutation)
        => mutation switch {
            "none" => geometry,
            "hive" => AuthoritativeTownGeometryBuilder.OpenHive(geometry),
            "grave" => AuthoritativeTownGeometryBuilder.OpenGrave(geometry),
            "fountain" => AuthoritativeTownGeometryBuilder.CleanFountain(geometry),
            "all" => AuthoritativeTownGeometryBuilder.CleanFountain(
                AuthoritativeTownGeometryBuilder.OpenGrave(
                    AuthoritativeTownGeometryBuilder.OpenHive(geometry))),
            _ => throw new InvalidDataException($"Unknown native Town mutation '{mutation}'."),
        };

    private static void AssertOracleUInt32(
        BinaryReader reader,
        uint expected,
        string fixture,
        string channel)
    {
        var actual = reader.ReadUInt32();
        Assert.True(expected == actual, $"{fixture}: {channel}: expected {expected}, actual {actual}.");
    }

    private const AuthoritativeTownWarpUnlocks AllNormalUnlocks =
        AuthoritativeTownWarpUnlocks.Catacombs
        | AuthoritativeTownWarpUnlocks.Caves
        | AuthoritativeTownWarpUnlocks.Hell;

    private static ushort[] CreateAutomapLayer(
        IReadOnlyList<(int X, int Y, ushort Value)>? values)
    {
        var layer = new ushort[40 * 40];
        if (values is null)
            return layer;
        foreach (var (x, y, value) in values)
            layer[y * 40 + x] = value;
        return layer;
    }

    private static AuthoritativeDunMapAsset CreateDun(
        int width,
        int height,
        IReadOnlyList<ushort> tiles)
    {
        var count = checked(width * height);
        if (tiles.Count != count)
            throw new ArgumentException("DUN tile layer size does not match dimensions.", nameof(tiles));
        var words = new ushort[2 + count * 17];
        words[0] = checked((ushort)width);
        words[1] = checked((ushort)height);
        for (var index = 0; index < count; index++)
            words[index + 2] = tiles[index];
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        return AuthoritativeDunMapAsset.Load("synthetic-town.dun", bytes);
    }

    private static AuthoritativeDunPieceCatalog CreatePieceCatalog()
    {
        var definitions = new AuthoritativeDunPieceDefinition[342];
        for (var id = 0; id < definitions.Length; id++) {
            var first = checked((uint)((id * 4) % AuthoritativeDunTilePropertyCatalog.NativeTileCount));
            definitions[id] = id switch {
                0 => new AuthoritativeDunPieceDefinition(0, 359, 100, 101, 102),
                1 => new AuthoritativeDunPieceDefinition(1, 301, 302, 303, 304),
                2 => new AuthoritativeDunPieceDefinition(2, 401, 402, 403, 404),
                3 => new AuthoritativeDunPieceDefinition(3, 501, 502, 503, 504),
                70 => new AuthoritativeDunPieceDefinition(70, 280, 281, 282, 283),
                319 => new AuthoritativeDunPieceDefinition(319, 1000, 1001, 1002, 1003),
                _ => new AuthoritativeDunPieceDefinition(
                    checked((uint)id), first, first + 1, first + 2, first + 3),
            };
        }
        return new AuthoritativeDunPieceCatalog(definitions);
    }

    private static AuthoritativeDunTilePropertyCatalog CreateTileProperties()
    {
        var properties = new AuthoritativeDunTileProperty[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        for (var id = 0; id < properties.Length; id++) {
            var flags = id switch {
                359 => AuthoritativeDunTileFlags.Solid,
                0x489 => AuthoritativeDunTileFlags.Solid,
                100 => AuthoritativeDunTileFlags.BlockMissile,
                101 => AuthoritativeDunTileFlags.BlockLight,
                218 => AuthoritativeDunTileFlags.BlockMissile,
                1000 => AuthoritativeDunTileFlags.BlockLight,
                _ => AuthoritativeDunTileFlags.None,
            };
            properties[id] = new AuthoritativeDunTileProperty(checked((uint)id), flags);
        }
        return new AuthoritativeDunTilePropertyCatalog(properties);
    }

    private static ushort[] ReadMega(IReadOnlyList<ushort> fine, int x, int y)
        => [
            Fine(fine, x, y),
            Fine(fine, x + 1, y),
            Fine(fine, x, y + 1),
            Fine(fine, x + 1, y + 1),
        ];

    private static ushort Fine(IReadOnlyList<ushort> layer, int x, int y)
        => layer[Cell(x, y)];

    private static ushort Mega(IReadOnlyList<ushort> layer, int x, int y)
        => layer[y * AuthoritativeTownGeometry.MegaWidth + x];

    private static int Cell(int x, int y)
        => y * AuthoritativeTownGeometry.FineWidth + x;
}
