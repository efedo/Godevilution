using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeStartContentTests
{
    [Fact]
    public void NativeStartPackIsPinnedAndLeavesBaseWorldContentUnchanged()
    {
        var (basePack, nativeStartPack) = LoadPacks();
        var baseManifest = new ContentManifest("devilution.base", "0.1.0", [basePack]);
        var nativeManifest = new ContentManifest(
            "native-start",
            "1",
            [basePack, nativeStartPack]);

        Assert.Equal("godevilution.native-start", nativeStartPack.Id);
        Assert.Equal("0.1.0", nativeStartPack.Version);
        Assert.Equal("1.502.0", nativeStartPack.Schema!.Version.ToString());
        Assert.Equal(["devilution.base"], nativeStartPack.Dependencies);
        var constraint = Assert.Single(nativeStartPack.DependencyConstraints);
        Assert.Equal("devilution.base", constraint.DependencyId);
        Assert.Equal("0.1.0", constraint.Minimum.ToString());
        Assert.Equal("0.1.0", constraint.Maximum.ToString());
        Assert.Empty(nativeStartPack.Overlays);
        Assert.Empty(nativeStartPack.Assets);
        Assert.Equal("level_start_positions.tsv", Assert.Single(nativeStartPack.Tables).SourcePath);
        Assert.Equal("8ca8a6d80722c06439ad6cf62efc75d1e486971669c0509f6a03e6438cd3fd1e", baseManifest.Sha256);
        Assert.NotEqual(baseManifest.Sha256, nativeManifest.Sha256);

        var baseContent = ComposedContent.Compose(baseManifest);
        var nativeContent = ComposedContent.Compose(nativeManifest);
        foreach (var table in basePack.Tables)
            Assert.Equal(baseContent.GetRequiredTable(table.SourcePath).ToCanonicalTsv(),
                nativeContent.GetRequiredTable(table.SourcePath).ToCanonicalTsv());
        foreach (var asset in basePack.Assets)
            Assert.Equal(baseContent.GetRequiredAsset(asset.SourcePath).Contents.ToArray(),
                nativeContent.GetRequiredAsset(asset.SourcePath).Contents.ToArray());
        Assert.Equal(
            "level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t75\t68\n",
            nativeContent.GetRequiredTable("level_start_positions.tsv").ToCanonicalTsv());
    }

    [Fact]
    public void BaseActivationKeepsLegacyEntryPointsAndNativeActivationChangesOnlyTown()
    {
        var (basePack, nativeStartPack) = LoadPacks();
        var baseActivation = Activate([basePack]);
        var nativeActivation = Activate([basePack, nativeStartPack]);

        var baseWorld = baseActivation.Simulation.WorldState.World!;
        var nativeWorld = nativeActivation.Simulation.WorldState.World!;
        Assert.Equal((0, 0), baseWorld.GetStartPosition(0));
        Assert.Equal((0, 0), baseWorld.GetStartPosition(1));
        Assert.Equal((0, 0), baseWorld.GetStartPosition(2));
        Assert.Equal((75, 68), nativeWorld.GetStartPosition(0));
        Assert.Equal((0, 0), nativeWorld.GetStartPosition(1));
        Assert.Equal((0, 0), nativeWorld.GetStartPosition(2));
        Assert.Equal(baseWorld.DefaultStartingLevelId, nativeWorld.DefaultStartingLevelId);
        foreach (var level in nativeActivation.Simulation.WorldState.LevelGeometry)
            Assert.Equal(nativeWorld.GetStartPosition(level.LevelId), (level.StartPositionX, level.StartPositionY));
    }

    [Fact]
    public void PreviousSchemaWithoutEntryTableKeepsLegacyStarts()
    {
        var (basePack, _) = LoadPacks();
        var previous = new ContentPack(basePack.Id, basePack.Version, basePack.Tables,
            basePack.Dependencies, basePack.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.501.0"),
            basePack.DependencyConstraints, basePack.Assets);
        var activation = Activate([previous]);
        Assert.Equal(1U, activation.DefaultStartingLevelId);
        foreach (var level in activation.Simulation.WorldState.LevelGeometry)
            Assert.Equal((0, 0), (level.StartPositionX, level.StartPositionY));
    }

    [Fact]
    public void EffectiveMutationCollisionRejectsEntryBeforeAnyStartChanges()
    {
        var stableIds = new StableContentIdCatalog([new StableContentId("level", "town", 0)]);
        var world = AuthoritativeWorld.LoadTsv("levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tblocked_cells\n0\ttown\t4\t4\tnone\n", stableIds);
        var state = new DiabloWorldState(world, worldMutations: new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(1, 0, blockedCellsAdd: [5]),
        ]));
        Assert.True(state.ApplyWorldMutation(1));
        Assert.True(world.IsWalkable(0, 1, 1));
        Assert.False(state.IsWalkable(0, 1, 1));
        var catalog = AuthoritativeLevelStartCatalog.LoadTsv("level_start_positions.tsv",
            "level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t1\t1\n", stableIds);

        var exception = Assert.Throws<InvalidDataException>(() => catalog.ValidateEffectiveGeometry(state));
        Assert.Contains("effective geometry", exception.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0), world.GetStartPosition(0));
    }

    [Fact]
    public void BootstrapRejectsEntryBlockedDuringSimulationConstruction()
    {
        var (basePack, nativePack) = LoadPacks();
        var factory = new BlockingSimulationFactory();
        var bootstrap = new DiabloContentBootstrap(factory);
        var content = ComposedContent.Compose(new ContentManifest("blocked-start", "1", [basePack, nativePack]));

        var exception = Assert.Throws<InvalidDataException>(() => bootstrap.Activate(content));
        Assert.Contains("effective geometry", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(factory.Simulation);
        Assert.Equal((0, 0), factory.Simulation.WorldState.World!.GetStartPosition(0));
    }

    [Fact]
    public void NativeStartCatalogStagesAllReplacementsBeforeMutatingTheWorld()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
            new StableContentId("level", "cathedral", 1),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tblocked_cells\tdefault_start\n"
                + "0\ttown\t112\t112\tnone\t0\n"
                + "1\tcathedral\t4\t4\t5\t1\n",
            stableIds);
        var catalog = AuthoritativeLevelStartCatalog.LoadTsv(
            "level_start_positions.tsv",
            "level_id\tlevel_symbol\tstart_x\tstart_y\n"
                + "0\ttown\t75\t68\n"
                + "1\tcathedral\t1\t1\n",
            stableIds);

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(world));

        Assert.Contains("invalid start position", exception.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0), world.GetStartPosition(0));
        Assert.Equal((0, 0), world.GetStartPosition(1));
    }

    [Fact]
    public void WithStartPositionPreservesGeometryFlagsAndIdentityWithoutMutatingOriginal()
    {
        var nativeTransparency = Enumerable.Range(0, 16).Select(value => (ushort)value).ToArray();
        var nativeObjectLayer = Enumerable.Repeat((ushort)1, 16).ToArray();
        var nativeMegaTileLayer = Enumerable.Repeat((ushort)2, 4).ToArray();
        var nativePieceLayer = Enumerable.Repeat((ushort)3, 16).ToArray();
        var nativeMonsterLayer = Enumerable.Repeat((ushort)4, 16).ToArray();
        var pieceProperties = new Dictionary<ushort, AuthoritativeDunTileFlags> {
            [3] = AuthoritativeDunTileFlags.BlockLight,
        };
        var source = new AuthoritativeLevel(
            1,
            4,
            4,
            blockedCells: [5],
            missileBlockedCells: [6],
            isDefaultStart: true,
            suppressMultiplayerDeathDrops: true,
            nativeTransparency: nativeTransparency,
            nativeObjectLayer: nativeObjectLayer,
            nativeMegaTileLayer: nativeMegaTileLayer,
            nativePieceLayer: nativePieceLayer,
            nativePieceProperties: pieceProperties,
            nativePieceCollisionCells: [7],
            nativePieceMissileBlockedCells: [8],
            nativeMonsterLayer: nativeMonsterLayer,
            startPositionX: 0,
            startPositionY: 0,
            nativeVisionBlockedCells: [0, 1]) {
            StableSymbol = "cathedral",
        };

        var updated = source.WithStartPosition(2, 3);

        Assert.Equal((0, 0), (source.StartPositionX, source.StartPositionY));
        Assert.Equal((2, 3), (updated.StartPositionX, updated.StartPositionY));
        Assert.Equal(source.LevelId, updated.LevelId);
        Assert.Equal(source.StableSymbol, updated.StableSymbol);
        Assert.Equal(source.Width, updated.Width);
        Assert.Equal(source.Height, updated.Height);
        Assert.Equal(source.IsDefaultStart, updated.IsDefaultStart);
        Assert.Equal(source.SuppressMultiplayerDeathDrops, updated.SuppressMultiplayerDeathDrops);
        Assert.Equal(source.BlockedCells, updated.BlockedCells);
        Assert.Equal(source.MissileBlockedCells, updated.MissileBlockedCells);
        Assert.Equal(source.NativeTransparency, updated.NativeTransparency);
        Assert.Equal(source.NativeObjectLayer, updated.NativeObjectLayer);
        Assert.Equal(source.NativeMegaTileLayer, updated.NativeMegaTileLayer);
        Assert.Equal(source.NativePieceLayer, updated.NativePieceLayer);
        Assert.Equal(
            source.NativePieceProperties.OrderBy(pair => pair.Key),
            updated.NativePieceProperties.OrderBy(pair => pair.Key));
        Assert.Equal(source.NativePieceCollisionCells, updated.NativePieceCollisionCells);
        Assert.Equal(source.NativePieceMissileBlockedCells, updated.NativePieceMissileBlockedCells);
        Assert.Equal(source.NativeMonsterLayer, updated.NativeMonsterLayer);
        Assert.Equal(source.NativeVisionBlockedCells, updated.NativeVisionBlockedCells);
        Assert.Equal(source.HasExplicitNativeVisionBlockedCells, updated.HasExplicitNativeVisionBlockedCells);
    }

    [Fact]
    public void WithStartPositionPreservesImplicitVisionGeometry()
    {
        var source = new AuthoritativeLevel(
            0,
            4,
            4,
            blockedCells: [5],
            nativePieceLayer: Enumerable.Repeat((ushort)3, 16),
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [3] = AuthoritativeDunTileFlags.BlockLight,
            });

        var updated = source.WithStartPosition(2, 2);

        Assert.False(source.HasExplicitNativeVisionBlockedCells);
        Assert.False(updated.HasExplicitNativeVisionBlockedCells);
        Assert.Equal(source.NativeVisionBlockedCells, updated.NativeVisionBlockedCells);
    }

    [Theory]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\tbad\t68\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n0\tunknown\t75\t68\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n1\ttown\t75\t68\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t75\t68\n0\ttown\t75\t68\n")]
    [InlineData("level_id\tstart_x\tstart_y\n0\t75\t68\n")]
    [InlineData("level_symbol\tlevel_id\tstart_x\tstart_y\ntown\t0\t75\t68\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\textra\n0\ttown\t75\t68\tvalue\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n0\t\t75\t68\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n\ttown\t75\t68\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t\t68\n")]
    [InlineData("level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t2147483648\t68\n")]
    public void NativeStartCatalogRejectsMalformedOrMismatchedRows(string contents)
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeLevelStartCatalog.LoadTsv(
            "level_start_positions.tsv",
            contents,
            stableIds));
    }

    [Fact]
    public void NativeStartCatalogRejectsAnUnregisteredLevelAtApplication()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
            new StableContentId("level", "cathedral", 1),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tblocked_cells\tdefault_start\n"
                + "0\ttown\t112\t112\tnone\t0\n",
            stableIds);
        var catalog = AuthoritativeLevelStartCatalog.LoadTsv(
            "level_start_positions.tsv",
            "level_id\tlevel_symbol\tstart_x\tstart_y\n"
                + "1\tcathedral\t1\t1\n",
            stableIds);

        Assert.Throws<InvalidDataException>(() => catalog.Apply(world));
        Assert.Equal((0, 0), world.GetStartPosition(0));
    }

    [Theory]
    [InlineData(112, 68)]
    [InlineData(75, 112)]
    [InlineData(-1, 68)]
    [InlineData(75, -1)]
    public void NativeStartCatalogRejectsAnOutOfBoundsEntry(int x, int y)
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tblocked_cells\tdefault_start\n"
                + "0\ttown\t112\t112\tnone\t0\n",
            stableIds);
        var catalog = AuthoritativeLevelStartCatalog.LoadTsv(
            "level_start_positions.tsv",
            "level_id\tlevel_symbol\tstart_x\tstart_y\n"
                + $"0\ttown\t{x}\t{y}\n",
            stableIds);

        Assert.Throws<InvalidDataException>(() => catalog.Apply(world));
        Assert.Equal((0, 0), world.GetStartPosition(0));
    }

    [Fact]
    public void NativeStartCatalogRejectsARegisteredWorldSymbolMismatch()
    {
        var stableIds = new StableContentIdCatalog([new StableContentId("level", "town", 0)]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 4, 4) { StableSymbol = "another-level" });
        var catalog = AuthoritativeLevelStartCatalog.LoadTsv("level_start_positions.tsv",
            "level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t1\t1\n", stableIds);

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(world));
        Assert.Contains("does not match the registered level symbol", exception.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0), world.GetStartPosition(0));
    }

    private sealed class BlockingSimulationFactory : IDiabloSimulationFactory
    {
        public DiabloSimulation? Simulation { get; private set; }

        public DiabloSimulation Create(DiabloSimulationOptions options)
        {
            Simulation = DiabloSimulationFactory.Instance.Create(options);
            var town = Simulation.WorldState.World!.Levels.Single(level => level.LevelId == 0);
            Simulation.WorldState.ReplaceLevel(new AuthoritativeLevel(0, town.Width, town.Height,
                blockedCells: town.BlockedCells.Append(68 * town.Width + 75)) { StableSymbol = town.StableSymbol });
            return Simulation;
        }
    }

    private static DiabloContentActivation Activate(IReadOnlyList<ContentPack> packs)
        => DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("native-start-tests", "1", packs)));

    private static (ContentPack Base, ContentPack NativeStart) LoadPacks()
    {
        var root = FindRepositoryRoot();
        var contentRoot = Path.Combine(root, "server", "content");
        return (
            ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "base")),
            ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "native-start")));
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
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository.");
    }
}
