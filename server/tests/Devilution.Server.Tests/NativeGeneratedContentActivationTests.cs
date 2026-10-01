using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Synthetic catalogs exercise production activation; they are not native geometry oracles. */
public sealed class NativeGeneratedContentActivationTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("previous")]
    public void ExplicitHostAdapterPublishesFineGeometryAndNativeWalkingRoutes(string entry)
    {
        var content = CreateContent(entry: entry);
        var activation = Activate(content);
        var level = activation.Simulation.WorldState.World!.Levels.Single(level => level.LevelId == 1);
        var expected = AuthoritativeCathedralLevel1Generator.Generate(17,
            entry == "main" ? AuthoritativeCathedralEntry.Main : AuthoritativeCathedralEntry.Previous);
        Assert.Equal((112, 112), (level.Width, level.Height));
        Assert.Empty(level.NativeMegaTileLayer);
        Assert.Equal(112 * 112, level.NativePieceLayer.Count);
        Assert.Equal((expected.EntryPosition.X, expected.EntryPosition.Y), (level.StartPositionX, level.StartPositionY));
        Assert.NotNull(activation.Simulation.NativeGeneratedWorldIdentity);

        var executor = CreateHost(content);
        executor.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var snapshot = executor.CreateSnapshot("player", 1, 0);
        var player = Assert.Single(snapshot.Players);
        Assert.Equal(1U, player.LevelId);
        Assert.Equal((level.StartPositionX, level.StartPositionY), (player.PositionX, player.PositionY));
        var projection = Assert.Single(snapshot.LevelGeometry, candidate => candidate.LevelId == 1);
        Assert.Equal((112, 112), (projection.Width, projection.Height));
        var save = new AuthoritativeSaveCodec().Deserialize(executor.ExportPlayerSave("player"));
        Assert.NotNull(save.NativeGeneratedWorldIdentity);
        var forward = activation.Simulation.WorldState.World.WarpTargets.Single(warp => warp.WarpId == 1001);
        Assert.Equal((25, 29), (forward.TriggerX, forward.TriggerY));
        var backward = activation.Simulation.WorldState.World.WarpTargets.Single(warp => warp.WarpId == 1099);
        Assert.Equal((25, 31), (backward.TargetX, backward.TargetY));
        Assert.Equal((ushort)128, level.NativePieceLayer[backward.TriggerY * 112 + backward.TriggerX]);
    }

    [Theory]
    [InlineData(AuthoritativeGameMode.SinglePlayer)]
    [InlineData(AuthoritativeGameMode.Multiplayer)]
    public void AutomaticTownStairsAndFreshHostRestoreUseTheSameGeneratedIdentity(AuthoritativeGameMode mode)
    {
        var content = CreateContent();
        var executor = CreateHost(content, startingLevel: 0, mode: mode);
        executor.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        executor.CreateSnapshot("player", 1, 0);
        var codec = new AuthoritativeSaveCodec();
        var adjacent = codec.Deserialize(executor.ExportPlayerSave("player"));
        // A valid server checkpoint places the participant one step from the native Town stairs.
        executor.ImportPlayerSave("player", codec.Serialize(adjacent with {
            Player = adjacent.Player with { LevelId = 0, PositionX = 25, PositionY = 28 },
        }));
        var result = new AuthoritativeCommandServer(executor).Process("player", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
        }, 1);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        executor.AdvanceTo(100);
        var entered = executor.GetPlayerState("player");
        Assert.Equal(1U, entered.LevelId);
        var level = Activate(content).Simulation.WorldState.World!.Levels.Single(level => level.LevelId == 1);
        Assert.Equal((level.StartPositionX, level.StartPositionY), (entered.PositionX, entered.PositionY));

        var saved = executor.ExportPlayerSave("player");
        var replacement = CreateHost(content, mode: mode);
        replacement.ImportPlayerSave("resumed", saved);
        var resumed = replacement.GetPlayerState("resumed");
        Assert.Equal((entered.LevelId, entered.PositionX, entered.PositionY), (resumed.LevelId, resumed.PositionX, resumed.PositionY));
        Assert.Equal(100UL, replacement.GetSessionStateTick("resumed"));
        Assert.Equal(codec.Deserialize(saved).NativeGeneratedWorldIdentity!.Levels,
            codec.Deserialize(replacement.ExportPlayerSave("resumed")).NativeGeneratedWorldIdentity!.Levels);

        var returnWarp = level.NativePieceLayer.Select((piece, index) => (piece, index)).Single(cell => cell.piece == 128).index;
        var atReturn = codec.Deserialize(replacement.ExportPlayerSave("resumed"));
        replacement.ImportPlayerSave("resumed", codec.Serialize(atReturn with {
            Player = atReturn.Player with { PositionX = returnWarp % 112, PositionY = returnWarp / 112 - 1 },
        }));
        var returned = new AuthoritativeCommandServer(replacement).Process("resumed", new Command {
            ClientSequence = 1,
            RequestedTick = 101,
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
        }, 101);
        Assert.Equal(CommandStatus.Accepted, returned.Status);
        replacement.AdvanceTo(200);
        var backInTown = replacement.GetPlayerState("resumed");
        Assert.Equal((0U, 25, 31), (backInTown.LevelId, backInTown.PositionX, backInTown.PositionY));
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("geometry")]
    [InlineData("routes")]
    [InlineData("entry")]
    [InlineData("missing-identity")]
    public void RestoreRejectsChangedGenerationBeforeMutatingTheLiveHost(string change)
    {
        var executor = CreateHost(CreateContent());
        executor.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        executor.CreateSnapshot("player", 1, 0);
        var saved = executor.ExportPlayerSave("player");
        var changedContent = CreateContent(
            seed: change == "seed" ? 18U : 17U,
            entry: change == "entry" ? "previous" : "main",
            changedGeometry: change == "geometry",
            changedRoutes: change == "routes");
        var replacement = CreateHost(changedContent);
        replacement.InitializeParticipant("player", new AuthoritativeParticipantSelection(1));
        replacement.CreateSnapshot("player", 1, 0);
        var before = replacement.ExportPlayerSave("player");
        if (change == "missing-identity") {
            var codec = new AuthoritativeSaveCodec();
            saved = codec.Serialize(codec.Deserialize(saved) with { NativeGeneratedWorldIdentity = null });
        }
        Assert.Throws<InvalidDataException>(() => replacement.ImportPlayerSave("player", saved));
        Assert.Equal(before, replacement.ExportPlayerSave("player"));
        Assert.Equal(0UL, replacement.GetSessionStateTick("player"));
    }

    [Fact]
    public void DungeonGenerationDoesNotConsumeOrDependOnTheGameplayRandomStream()
    {
        var content = CreateContent();
        var first = Activate(content, 42);
        var second = Activate(content, 99);
        var original = DiabloModule.Instance.ContentBootstrap.Activate(LoadBaseContent(), 42);
        var player = first.PlayerLoadouts.CreateTemplate("player", 0, null, null, null, 1, null, 1, 1).CreatePlayer();
        Assert.Equal(first.Simulation.NativeGeneratedWorldIdentity!.Levels,
            second.Simulation.NativeGeneratedWorldIdentity!.Levels);
        Assert.Equal(original.Simulation.CaptureSnapshot(player, 0).RandomState,
            first.Simulation.CaptureSnapshot(player, 0).RandomState);
        Assert.NotEqual(first.Simulation.CaptureSnapshot(player, 0).RandomState,
            second.Simulation.CaptureSnapshot(player, 0).RandomState);
    }

    [Theory]
    [InlineData("native_cathedral_level1.tsv")]
    [InlineData("native_generated_route_bindings.tsv")]
    [InlineData("native_dun_piece_definitions.tsv")]
    [InlineData("native_dun_tile_properties.tsv")]
    public void GeneratedActivationRequiresTheCompleteExplicitContract(string missingTable)
    {
        var content = CreateContent(omittedTable: missingTable);
        Assert.Throws<InvalidDataException>(() => Activate(content));
    }

    [Fact]
    public void DefaultActivationAndBaseContentRemainUnchanged()
    {
        Assert.Throws<InvalidDataException>(() => DiabloModule.Instance.ContentBootstrap.Activate(CreateContent()));
        Assert.Throws<InvalidDataException>(() => Activate(LoadBaseContent()));
        var original = DiabloModule.Instance.ContentBootstrap.Activate(LoadBaseContent());
        Assert.Null(original.Simulation.NativeGeneratedWorldIdentity);
        Assert.Equal(40, original.Simulation.WorldState.World!.Levels.Single(level => level.LevelId == 1).Width);
    }

    [Fact]
    public void GeneratedSaveCannotBeImportedIntoTheLegacyHost()
    {
        var generated = CreateHost(CreateContent());
        generated.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var content = LoadBaseContent();
        var catalog = BuiltInAuthoritativeGameHostAdapters.Create();
        var registry = new GameplayModuleRegistry();
        registry.Load(catalog.CreateBuiltInModules(), new GameplayModuleContext(content.Manifest,
            new DelegateAuthoritativeClock(() => 0)));
        var legacy = Assert.IsType<StoreSimulationExecutor>(catalog.Activate(registry, content,
            new AuthoritativePlayerStartConfiguration(null, null, null)));
        legacy.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var before = legacy.ExportPlayerSave("player");
        Assert.Throws<InvalidDataException>(() => legacy.ImportPlayerSave("player", generated.ExportPlayerSave("player")));
        Assert.Equal(before, legacy.ExportPlayerSave("player"));
    }

    [Fact]
    public void FixedGeneratedEntryOverrideIsRejectedInsteadOfSilentlyChangingNativeEntry()
    {
        var content = CreateContent(extraTable: TsvTable.Parse("level_start_positions.tsv",
            "level_id\tlevel_symbol\tstart_x\tstart_y\n1\tcathedral\t1\t1\n"));
        var exception = Assert.Throws<InvalidDataException>(() => Activate(content));
        Assert.Contains("fixed level-start", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoredQuestFragmentCannotBeSilentlyAppliedToGeneratedFineGeometry()
    {
        var originalFragments = LoadBasePack().Tables.Single(table => table.SourcePath == "native_dun_fragments.tsv");
        var exception = Assert.Throws<InvalidDataException>(() => Activate(CreateContent(extraTable: originalFragments)));
        Assert.Contains("does not yet support native quest fragments", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGeometryMutationSurvivesColdRestoreAndRejectedRestoreRollback()
    {
        var mutations = LoadBasePack().Tables.Single(table => table.SourcePath == "world_mutations.tsv");
        var row = mutations.Columns.ToDictionary(column => column, column => mutations.Rows[0][column]);
        row["mutation_id"] = "9399";
        row["level_id"] = "1";
        row["anchor_unique_identity"] = "";
        row["blocked_cells_add"] = "10170";
        row["transparency_rects"] = "90,90,1,1,7";
        var table = TsvTable.Parse(mutations.SourcePath, mutations.ToCanonicalTsv()
            + string.Join('\t', mutations.Columns.Select(column => row[column])) + "\n");
        var content = CreateContent(extraTable: table);
        var activation = Activate(content);
        Assert.True(activation.Simulation.WorldState.ApplyWorldMutation(9399));
        var start = activation.Simulation.WorldState.World!.GetStartPosition(1);
        var executor = new StoreSimulationExecutor(activation,
            activation.PlayerLoadouts.CreateTemplate("player", 0, null, null, null, 1, null, start.X, start.Y));
        executor.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var codec = new AuthoritativeSaveCodec();
        var saved = executor.ExportPlayerSave("player");
        var replacement = CreateHost(content);
        replacement.ImportPlayerSave("player", saved);
        var geometry = Assert.Single(replacement.CreateSnapshot("player", 1, 0).LevelGeometry, level => level.LevelId == 1);
        Assert.Contains(10170, geometry.BlockedCells);
        Assert.Equal(7U, geometry.NativeTransparency[10170]);
        var before = replacement.ExportPlayerSave("player");
        var invalid = codec.Deserialize(before);
        // Same identity passes the precheck, then an invalid player must roll back the earlier mutation restore.
        Assert.Throws<InvalidDataException>(() => replacement.ImportPlayerSave("player", codec.Serialize(invalid with {
            ActiveWorldMutationIds = [],
            Player = invalid.Player with { PositionX = 1000 },
        })));
        Assert.Equal(before, replacement.ExportPlayerSave("player"));
        Assert.Contains(10170, Assert.Single(replacement.CreateSnapshot("player", 1, 0).LevelGeometry, level => level.LevelId == 1).BlockedCells);
    }

    [Fact(Explicit = true)]
    public void LocalAssetSemanticCatalogActivatesTraversesAndRestoresThroughProductionHost()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory));
        var pieces = AuthoritativeDunPieceCatalog.LoadTil("private-oracle:l1.til", File.ReadAllBytes(Path.Combine(directory!, "l1.til")));
        var rawSol = File.ReadAllBytes(Path.Combine(directory!, "l1.sol"));
        Assert.InRange(rawSol.Length, 1, AuthoritativeDunTilePropertyCatalog.NativeTileCount);
        // Test-only semantic export: preserve the native oracle's zero tail and strip its unnamed 0x40 bit.
        // Production receives validated TSV definitions, never archives or this normalization policy.
        var semanticSol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        for (var index = 0; index < rawSol.Length; index++)
            semanticSol[index] = (byte)(rawSol[index] & 0xBF);
        var properties = AuthoritativeDunTilePropertyCatalog.LoadSol("private-oracle:semantic-l1.sol", semanticSol);
        var content = CreateContent(seed: 2588, externalGeometry: new(pieces, properties));
        var activation = Activate(content);
        var level = activation.Simulation.WorldState.World!.Levels.Single(level => level.LevelId == 1);
        var executor = CreateHost(content, startingLevel: 0);
        executor.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var codec = new AuthoritativeSaveCodec();
        var snapshot = codec.Deserialize(executor.ExportPlayerSave("player"));
        executor.ImportPlayerSave("player", codec.Serialize(snapshot with {
            Player = snapshot.Player with { LevelId = 0, PositionX = 25, PositionY = 28 },
        }));
        var result = new AuthoritativeCommandServer(executor).Process("player", new Command {
            ClientSequence = 1, RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
        }, 1);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        executor.AdvanceTo(100);
        Assert.Equal((1U, level.StartPositionX, level.StartPositionY),
            (executor.GetPlayerState("player").LevelId, executor.GetPlayerState("player").PositionX, executor.GetPlayerState("player").PositionY));
        var saved = executor.ExportPlayerSave("player");
        var replacement = CreateHost(content);
        replacement.ImportPlayerSave("player", saved);
        var restoredGeometry = Assert.Single(replacement.CreateSnapshot("player", 1, 100).LevelGeometry, candidate => candidate.LevelId == 1);
        Assert.Equal(level.NativeTransparency.Select(value => (uint)value), restoredGeometry.NativeTransparency);
        Assert.Equal(level.BlockedCells, restoredGeometry.BlockedCells);
        var returnWarp = activation.Simulation.WorldState.World.WarpTargets.Single(warp => warp.WarpId == 1099);
        var neighbors = new[] { (0, 1), (1, 0), (-1, 0), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) };
        var step = neighbors.First(direction => level.IsWalkable(returnWarp.TriggerX - direction.Item1, returnWarp.TriggerY - direction.Item2));
        var atReturn = codec.Deserialize(replacement.ExportPlayerSave("player"));
        replacement.ImportPlayerSave("player", codec.Serialize(atReturn with {
            Player = atReturn.Player with { PositionX = returnWarp.TriggerX - step.Item1, PositionY = returnWarp.TriggerY - step.Item2 },
        }));
        var returned = new AuthoritativeCommandServer(replacement).Process("player", new Command {
            ClientSequence = 1, RequestedTick = 101,
            MoveRequested = new MoveRequested { DirectionX = step.Item1, DirectionY = step.Item2 },
        }, 101);
        Assert.Equal(CommandStatus.Accepted, returned.Status);
        replacement.AdvanceTo(200);
        Assert.Equal((0U, 25, 31), (replacement.GetPlayerState("player").LevelId,
            replacement.GetPlayerState("player").PositionX, replacement.GetPlayerState("player").PositionY));
    }

    private static DiabloContentActivation Activate(ComposedContent content, uint randomSeed = 42)
        => DiabloModule.Instance.NativeGeneratedContentBootstrap.ActivateGenerated(content, randomSeed);

    private static StoreSimulationExecutor CreateHost(ComposedContent content, uint? startingLevel = null,
        AuthoritativeGameMode mode = AuthoritativeGameMode.SinglePlayer)
    {
        var catalog = BuiltInAuthoritativeGameHostAdapters.Create(DiabloAuthoritativeGameHostAdapterFactory.NativeGeneratedId);
        var registry = new GameplayModuleRegistry();
        registry.Load(catalog.CreateBuiltInModules(), new GameplayModuleContext(content.Manifest,
            new DelegateAuthoritativeClock(() => 0)));
        return Assert.IsType<StoreSimulationExecutor>(catalog.Activate(registry, content,
            new AuthoritativePlayerStartConfiguration(null, null, null) {
                StartingLevelId = startingLevel, RandomSeed = 42, GameMode = mode,
            }));
    }

    private static ComposedContent CreateContent(uint seed = 17, string entry = "main",
        bool changedGeometry = false, bool changedRoutes = false, string? omittedTable = null,
        TsvTable? extraTable = null, AuthoritativeNativeDunGeometryCatalog? externalGeometry = null)
    {
        var basePack = LoadBasePack();
        var generated = AuthoritativeCathedralLevel1Generator.Generate(seed, AuthoritativeCathedralEntry.Main);
        var triggerMega = generated.MegaTiles.GroupBy(tile => tile)
            .Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId && group.Count() == 1)
            .Select(group => (uint)group.Key - 1).Take(2).ToArray();
        if (triggerMega.Length != 2)
            throw new InvalidOperationException("Synthetic fixture requires two unique mega tiles.");
        var pieces = "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
            + string.Join('\n', Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount).Select(id =>
                $"{id}\t{(id == triggerMega[0] ? 128 : id == triggerMega[1] ? 114 : 1)}\t1\t1\t1")) + "\n";
        var properties = "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n"
            + "1\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\n"
            + "114\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\n"
            + "128\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\n"
            + $"1360\tfalse\tfalse\tfalse\t{(changedGeometry ? "true" : "false")}\tfalse\tfalse\tfalse\n";
        if (externalGeometry is not null) {
            pieces = pieces[..(pieces.IndexOf('\n') + 1)] + string.Join('\n', externalGeometry.Pieces.Definitions.Select(piece =>
                $"{piece.MegaTileId}\t{piece.NorthWestPieceId}\t{piece.NorthEastPieceId}\t{piece.SouthWestPieceId}\t{piece.SouthEastPieceId}")) + "\n";
            var flags = new[] { AuthoritativeDunTileFlags.Solid, AuthoritativeDunTileFlags.BlockLight,
                AuthoritativeDunTileFlags.BlockMissile, AuthoritativeDunTileFlags.Transparent,
                AuthoritativeDunTileFlags.TransparentLeft, AuthoritativeDunTileFlags.TransparentRight, AuthoritativeDunTileFlags.Trap };
            properties = properties[..(properties.IndexOf('\n') + 1)] + string.Join('\n', externalGeometry.TileProperties.Definitions.Select(property =>
                property.PieceId + "\t" + string.Join('\t', flags.Select(flag => property.Flags.HasFlag(flag) ? "true" : "false")))) + "\n";
        }
        var additions = new[] {
            // The base prototype places a Catacombs quest on logical level 1.
            // This explicit test profile omits that incompatible fragment and its placements.
            WithoutRows(basePack, "native_dun_fragments.tsv", row => row["level_id"] == "1"),
            WithoutRows(basePack, "native_dun_fragment_objects.tsv", row => row["fragment_id"] == "3"),
            TsvTable.Parse("content_ids.tsv", basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv()
                + "warp\tcathedral_to_town\t1099\n"),
            TsvTable.Parse("warps.tsv", basePack.Tables.Single(table => table.SourcePath == "warps.tsv").ToCanonicalTsv()
                + "1099\tcathedral_to_town\t1\tcathedral\t1\t1\t0\ttown\t25\t31\t0\t\t0\tnone\t\tfalse\n"),
            TsvTable.Parse("native_dun_piece_definitions.tsv", pieces),
            TsvTable.Parse("native_dun_tile_properties.tsv", properties),
            TsvTable.Parse("native_cathedral_level1.tsv", "level_id\tlevel_symbol\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\n"
                + $"1\tcathedral\t{AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId}\t1\t{seed}\t{entry}\n"),
            TsvTable.Parse("native_generated_route_bindings.tsv", "route_kind\troute_id\troute_symbol\tendpoint\tlevel_id\tlevel_symbol\tanchor\n"
                + "warp\t1001\ttown_to_cathedral\tsource\t0\ttown\ttown_cathedral_trigger\n"
                + "warp\t1001\ttown_to_cathedral\tdestination\t1\tcathedral\tmain_entry\n"
                + "warp\t1002\tcathedral_to_catacombs\tsource\t1\tcathedral\tdown_trigger\n"
                + "warp\t1099\tcathedral_to_town\tsource\t1\tcathedral\tup_trigger\n"
                + "warp\t1099\tcathedral_to_town\tdestination\t0\ttown\ttown_cathedral_return\n"
                + $"portal\t1\tcathedral_entrance\tsource\t1\tcathedral\t{(changedRoutes ? "previous_entry" : "main_entry")}\n"),
        };
        var replacements = additions.Select(table => table.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tables = basePack.Tables.Where(table => !replacements.Contains(table.SourcePath)).Concat(additions);
        if (externalGeometry is not null) {
            // Explicit oracle profile keeps the synthetic door only as a set-level template,
            // with no exterior encounter. This does not certify native population.
            var objects = basePack.Tables.Single(table => table.SourcePath == "objects.tsv");
            var templateOnly = TsvTable.Parse(objects.SourcePath, string.Join('\t', objects.Columns) + "\n"
                + string.Join('\n', objects.Rows.Select(row => string.Join('\t', objects.Columns.Select(column =>
                    row["level_id"] == "1" && column == "template_only" ? "1" : row[column])))) + "\n");
            tables = tables.Where(table => table.SourcePath is not ("objects.tsv" or "monsters.tsv"
                    or "native_dun_fragments.tsv" or "native_dun_fragment_objects.tsv"))
                .Append(templateOnly)
                .Append(WithoutRows(basePack, "monsters.tsv", row => row["level_id"] == "1"))
                // A single L1 semantic catalog cannot provide collision for the prototype's other-family fragments.
                .Append(WithoutRows(basePack, "native_dun_fragments.tsv", _ => true))
                .Append(WithoutRows(basePack, "native_dun_fragment_objects.tsv", _ => true));
        }
        if (extraTable is not null)
            tables = tables.Where(table => table.SourcePath != extraTable.SourcePath).Append(extraTable);
        var pack = new ContentPack(basePack.Id, basePack.Version, tables.Where(table => table.SourcePath != omittedTable).ToArray(),
            basePack.Dependencies, basePack.Overlays, basePack.Schema, basePack.DependencyConstraints, basePack.Assets);
        return ComposedContent.Compose(new ContentManifest("generated-runtime-test", "1", [pack]));
    }

    private static ComposedContent LoadBaseContent()
        => ComposedContent.Compose(new ContentManifest("unchanged-base", "1", [LoadBasePack()]));

    private static TsvTable WithoutRows(ContentPack pack, string path, Func<TsvRow, bool> omit)
    {
        var original = pack.Tables.Single(table => table.SourcePath == path);
        return TsvTable.Parse(path, string.Join('\t', original.Columns) + "\n"
            + string.Join('\n', original.Rows.Where(row => !omit(row)).Select(row =>
                string.Join('\t', original.Columns.Select(column => row[column])))) + "\n");
    }

    private static ContentPack LoadBasePack()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return ContentPackLoader.LoadManifestDirectory(Path.Combine(directory.FullName, "server", "content", "base"));
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
