using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Server.Tests;

/** Synthetic catalogs test the production depth adapter, not native geometry or continuous GUI traversal. */
public sealed class CathedralDepthContentActivationTests
{
    [Theory]
    [InlineData(AuthoritativeGameMode.SinglePlayer)]
    [InlineData(AuthoritativeGameMode.Multiplayer)]
    public void EveryNativeStairBoundaryAndMiddleFloorColdRestoreUseTheDepthIdentity(AuthoritativeGameMode mode)
    {
        using var fixture = new Fixture();
        var activation = Activate(fixture.Content);
        Assert.Null(activation.Simulation.NativeGeneratedWorldIdentity);
        Assert.Equal(4, activation.Simulation.CathedralDepthWorldIdentity!.Levels.Count);
        var world = activation.Simulation.WorldState.World!;
        Assert.Equal(8, world.WarpTargets.Count);
        var host = CreateHost(fixture.Content, mode);
        host.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        host.CreateSnapshot("player", 1, 0);
        ulong tick = 0;
        ulong sequence = 0;
        foreach (var destination in new uint[] { 1, 2, 3, 4, 3, 2, 1, 0 }) {
            var source = host.GetPlayerState("player").LevelId;
            var warp = world.WarpTargets.Single(warp => warp.SourceLevelId == source && warp.TargetLevelId == destination);
            MoveOntoWarp(host, world, warp, ref tick, ref sequence);
            var arrived = host.GetPlayerState("player");
            Assert.Equal((destination, warp.TargetX, warp.TargetY), (arrived.LevelId, arrived.PositionX, arrived.PositionY));
            if (destination != 2 || source != 1)
                continue;
            var saved = host.ExportPlayerSave("player");
            var decoded = new AuthoritativeSaveCodec().Deserialize(saved);
            Assert.Equal(2U, decoded.Player.LevelId);
            Assert.Equal(4, decoded.CathedralDepthWorldIdentity!.Levels.Count);
            var replacement = CreateHost(fixture.Content, mode);
            replacement.ImportPlayerSave("player", saved);
            Assert.Equal(saved, replacement.ExportPlayerSave("player"));
            Assert.Equal(tick, replacement.GetSessionStateTick("player"));
            var geometry = replacement.CreateSnapshot("player", 1, tick).LevelGeometry;
            foreach (var depth in new uint[] { 1, 2, 3, 4 }) {
                var restored = Assert.Single(geometry, level => level.LevelId == depth);
                var original = world.Levels.Single(level => level.LevelId == depth);
                Assert.Equal(original.NativePieceLayer.Select(value => (uint)value), restored.NativePieceLayer);
                Assert.Equal(original.NativeTransparency.Select(value => (uint)value), restored.NativeTransparency);
                Assert.Equal(original.BlockedCells, restored.BlockedCells);
            }
            host = replacement;
            sequence = 0; // This bridge restores simulation state; the generic host owns connection ledgers.
        }
        Assert.Equal((0U, 25, 31), (host.GetPlayerState("player").LevelId,
            host.GetPlayerState("player").PositionX, host.GetPlayerState("player").PositionY));
        Assert.DoesNotContain(world.WarpTargets, warp => warp.SourceLevelId == 4 && warp.TargetLevelId == 5);
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("attempt")]
    [InlineData("final-rng")]
    [InlineData("coarse")]
    [InlineData("fine")]
    [InlineData("level-routes")]
    [InlineData("catalog")]
    [InlineData("world-routes")]
    [InlineData("missing")]
    [InlineData("legacy-l1")]
    public void RejectsDifferentDepthIdentityBeforeMutatingTheLiveHost(string change)
    {
        using var fixture = new Fixture();
        var host = CreateHost(fixture.Content);
        host.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        host.CreateSnapshot("player", 1, 0);
        var before = host.ExportPlayerSave("player");
        var codec = new AuthoritativeSaveCodec();
        var snapshot = codec.Deserialize(before);
        var identity = snapshot.CathedralDepthWorldIdentity!;
        var levels = identity.Levels.ToArray();
        var changed = levels[2];
        levels[2] = change switch {
            "seed" => changed with { DungeonSeed = changed.DungeonSeed + 1 },
            "attempt" => changed with { AttemptSeed = changed.AttemptSeed + 1 },
            "final-rng" => changed with { FinalRandomState = changed.FinalRandomState + 1 },
            "coarse" => changed with { CoarseFingerprint = DifferentFingerprint(changed.CoarseFingerprint) },
            "fine" => changed with { FineFingerprint = DifferentFingerprint(changed.FineFingerprint) },
            "level-routes" => changed with { RouteFingerprint = DifferentFingerprint(changed.RouteFingerprint) },
            "catalog" => changed with { CatalogFingerprint = DifferentFingerprint(changed.CatalogFingerprint) },
            _ => changed,
        };
        var replacement = new DiabloCathedralDepthWorldIdentity(levels,
            change == "world-routes" ? DifferentFingerprint(identity.RouteFingerprint) : identity.RouteFingerprint);
        snapshot = snapshot with {
            CathedralDepthWorldIdentity = change is "missing" or "legacy-l1" ? null : replacement,
            NativeGeneratedWorldIdentity = change == "legacy-l1" ? LegacyIdentity() : null,
            Player = snapshot.Player with { Gold = snapshot.Player.Gold + 50 },
        };
        Assert.Throws<InvalidDataException>(() => host.ImportPlayerSave("player", codec.Serialize(snapshot)));
        Assert.Equal(before, host.ExportPlayerSave("player"));
        Assert.Equal(0UL, host.GetSessionStateTick("player"));
    }

    [Fact]
    public void AuthoredHostCannotRestoreADepthProfileAndLegacyActivationRejectsItsCatalog()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidDataException>(() => DiabloModule.Instance.ContentBootstrap.Activate(fixture.Content));
        Assert.Throws<InvalidDataException>(() => DiabloModule.Instance.NativeGeneratedContentBootstrap.ActivateGenerated(fixture.Content));
        var generated = CreateHost(fixture.Content);
        generated.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var baseContent = ComposedContent.Compose(new ContentManifest("base-test", "1", [LoadBasePack()]));
        var catalog = BuiltInAuthoritativeGameHostAdapters.Create();
        var registry = new GameplayModuleRegistry();
        registry.Load(catalog.CreateBuiltInModules(), new GameplayModuleContext(baseContent.Manifest, new DelegateAuthoritativeClock(() => 0)));
        var authored = Assert.IsType<StoreSimulationExecutor>(catalog.Activate(registry, baseContent,
            new AuthoritativePlayerStartConfiguration(null, null, null)));
        authored.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var before = authored.ExportPlayerSave("player");
        Assert.Throws<InvalidDataException>(() => authored.ImportPlayerSave("player", generated.ExportPlayerSave("player")));
        Assert.Equal(before, authored.ExportPlayerSave("player"));
    }

    [Fact]
    public void DepthSeedsDoNotConsumeOrDependOnGameplayRandomness()
    {
        using var fixture = new Fixture();
        var first = Activate(fixture.Content, 42);
        var second = Activate(fixture.Content, 99);
        Assert.Equal(first.Simulation.CathedralDepthWorldIdentity!.Levels, second.Simulation.CathedralDepthWorldIdentity!.Levels);
        var start = first.Simulation.WorldState.World!.GetStartPosition(0);
        var player = first.PlayerLoadouts.CreateTemplate("player", 0, null, null, null, 0, null, start.X, start.Y).CreatePlayer();
        Assert.NotEqual(first.Simulation.CaptureSnapshot(player, 0).RandomState, second.Simulation.CaptureSnapshot(player, 0).RandomState);
    }

    [Theory]
    [InlineData("native_cathedral_depths.tsv")]
    [InlineData("native_generated_route_bindings.tsv")]
    [InlineData("native_dun_piece_definitions.tsv")]
    [InlineData("native_dun_tile_properties.tsv")]
    public void MissingDepthInputsCannotFallBackToAuthoredGeometry(string path)
    {
        using var fixture = new Fixture();
        var pack = new ContentPack(fixture.Pack.Id, fixture.Pack.Version,
            fixture.Pack.Tables.Where(table => table.SourcePath != path).ToArray(), schema: fixture.Pack.Schema,
            assets: fixture.Pack.Assets);
        Assert.Throws<InvalidDataException>(() => Activate(ComposedContent.Compose(new ContentManifest(pack.Id, pack.Version, [pack]))));
    }

    private static void MoveOntoWarp(StoreSimulationExecutor host, AuthoritativeWorld world, AuthoritativeWarpTarget warp,
        ref ulong tick, ref ulong sequence)
    {
        var geometry = world.Levels.Single(level => level.LevelId == warp.SourceLevelId);
        var offsets = new[] { (X: 0, Y: 1), (X: 1, Y: 0), (X: -1, Y: 0), (X: 0, Y: -1) };
        var offset = offsets.First(offset => geometry.IsWalkable(warp.TriggerX - offset.X, warp.TriggerY - offset.Y)
            && !world.WarpTargets.Any(other => other.SourceLevelId == warp.SourceLevelId
                && other.TriggerX == warp.TriggerX - offset.X && other.TriggerY == warp.TriggerY - offset.Y));
        var codec = new AuthoritativeSaveCodec();
        var snapshot = codec.Deserialize(host.ExportPlayerSave("player"));
        // Synthetic boundary verification deliberately positions a valid server checkpoint;
        // the separate Godot acceptance proves continuous ordinary-control traversal.
        host.ImportPlayerSave("player", codec.Serialize(snapshot with {
            Player = snapshot.Player with { LevelId = warp.SourceLevelId,
                PositionX = warp.TriggerX - offset.X, PositionY = warp.TriggerY - offset.Y },
        }));
        var result = new AuthoritativeCommandServer(host).Process("player", new Command {
            ClientSequence = ++sequence, RequestedTick = tick + 1,
            MoveRequested = new MoveRequested { DirectionX = offset.X, DirectionY = offset.Y },
        }, tick + 1);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        tick += 100;
        host.AdvanceTo(tick);
    }

    private static DiabloContentActivation Activate(ComposedContent content, uint randomSeed = 42)
        => DiabloModule.Instance.CathedralDepthContentBootstrap.ActivateDepths(content, randomSeed);

    private static StoreSimulationExecutor CreateHost(ComposedContent content, AuthoritativeGameMode mode = AuthoritativeGameMode.SinglePlayer)
    {
        var catalog = BuiltInAuthoritativeGameHostAdapters.Create(DiabloAuthoritativeGameHostAdapterFactory.CathedralDepthId);
        var registry = new GameplayModuleRegistry();
        registry.Load(catalog.CreateBuiltInModules(), new GameplayModuleContext(content.Manifest, new DelegateAuthoritativeClock(() => 0)));
        return Assert.IsType<StoreSimulationExecutor>(catalog.Activate(registry, content,
            new AuthoritativePlayerStartConfiguration(null, null, null) { StartingLevelId = 0, RandomSeed = 42, GameMode = mode }));
    }

    private static string DifferentFingerprint(string fingerprint)
        => fingerprint == new string('A', 64) ? new string('B', 64) : new string('A', 64);

    private static DiabloNativeGeneratedWorldIdentity LegacyIdentity()
        => new([new(AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId,
            AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId, 1, 1, "cathedral", 17,
            AuthoritativeCathedralEntry.Main, 17, 0, new string('A', 64), new string('B', 64),
            new string('C', 64), new string('D', 64))], new string('E', 64));

    private static ContentPack LoadBasePack()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return ContentPackLoader.LoadManifestDirectory(Path.Combine(directory.FullName, "server", "content", "base"));
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string output = Path.Combine(Path.GetTempPath(), "godevilution-depth-host-" + Guid.NewGuid().ToString("N"));
        public ContentPack Pack { get; }
        public ComposedContent Content { get; }

        public Fixture()
        {
            var pieces = "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + string.Join('\n', Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
                    .Select(id => $"{id}\t{(id == 63 ? 128 : id == 58 ? 114 : id == 21 ? 1 : 2)}\t2\t2\t2")) + "\n";
            var properties = "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n"
                + string.Join('\n', Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
                    .Select(id => $"{id}\t{id == 1}\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse")) + "\n";
            CathedralDepthPlaytestCompiler.Compile(new CathedralDepthPlaytestCompilerOptions(
                LoadBasePack(), output, pieces, properties, [17, 17, 17, 17]) {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
            });
            Pack = ContentPackLoader.LoadManifestDirectory(Path.Combine(output, "content"));
            Content = ComposedContent.Compose(new ContentManifest(Pack.Id, Pack.Version, [Pack]));
        }

        public void Dispose()
        {
            var target = Path.GetFullPath(output);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(target).StartsWith("godevilution-depth-host-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe depth host fixture cleanup target.");
            if (Directory.Exists(target))
                Directory.Delete(target, true);
        }
    }
}
