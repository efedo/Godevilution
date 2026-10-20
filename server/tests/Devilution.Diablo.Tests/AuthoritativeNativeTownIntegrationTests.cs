using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Data;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Exercises the distinct Town owner through real four-pack activation and cold cohort restoration. */
public sealed class AuthoritativeNativeTownIntegrationTests
{
    [Theory(Explicit = true)]
    [InlineData("warrior", 0U)]
    [InlineData("sorcerer", 2U)]
    public void NativeTownRetainsRosterGeometryMutationsAndClassAcrossColdRestore(string symbol, uint classId)
    {
        var content = LoadProfile(symbol);
        Assert.Throws<InvalidDataException>(() => DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(content, 2588));
        var catalog = AuthoritativeNativeTownCatalog.Load(content);
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownProgression(content, 2588);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var template = activation.PlayerLoadouts.CreateTemplate("native-town", classId, null, null, null,
            startingLevelId: 0, startingPositionX: 75, startingPositionY: 68);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-town", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-town", value => value);
        Assert.True(world.HasNativeTown);
        var town = world.LevelGeometry.Single(level => level.LevelId == 0);
        Assert.Equal((ushort)923, town.NativePieceLayer[63 * 112 + 62]);
        Assert.False(world.IsWalkable(0, 62, 63));
        Assert.True(world.IsWalkable(0, 62, 64));
        Assert.True(world.IsOccupied(0, 55, 79, [player], player.EntityId));
        Assert.True(world.IsOccupied(0, 57, 15, [player], player.EntityId));
        Assert.False(world.IsOccupied(0, 62, 64, [player], player.EntityId));
        Assert.Empty(town.StaticObjectBlockedCells);
        var presentation = world.CapturePresentationLevelGeometry().Single(level => level.LevelId == 0);
        Assert.Equal(catalog.Roster.OccupiedCells, presentation.StaticObjectBlockedCells.Order().ToArray());
        Assert.Empty(world.LevelGeometry.Single(level => level.LevelId == 0).StaticObjectBlockedCells);
        Assert.Same(presentation, world.CapturePresentationLevelGeometry().Single(level => level.LevelId == 0));

        var codec = new DiabloCohortCodec();
        var initial = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeTownProgressionFormatVersion, initial.FormatVersion);
        var initialTown = Assert.IsType<DiabloNativeTownCheckpoint>(initial.NativeTown);
        var randomBefore = initial.RandomState;

        // Keep a genuine native destination walk live across the v11 cold-candidate boundary.
        runtime.AdvanceTo(1);
        var walkTarget = FindWalkTarget(player.PositionX, player.PositionY, minimumLength: 4);
        var walkPath = FindWalkPath(player.PositionX, player.PositionY, walkTarget.X, walkTarget.Y);
        Assert.True(walkPath.Length >= 4);
        var walkTick = checked(runtime.LastAdvancedTick + 1);
        Assert.True(simulation.Execute(player,
            new DiabloMoveToIntent(walkTarget.X, walkTarget.Y, FollowPath: true), walkTick, [player]).Succeeded);
        runtime.AdvanceTo(walkTick);
        Assert.NotNull(player.NativeWalkPath);

        var pendingWalkBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var pendingWalkState = codec.Deserialize(pendingWalkBytes);
        Assert.NotNull(pendingWalkState.Participants.Single().Player.NativeWalkPath);
        var walkCandidate = activation.CohortCandidateFactory!(pendingWalkState);
        var walkRestored = walkCandidate.CreateRuntime();
        Assert.Equal(pendingWalkBytes, codec.Serialize(walkCandidate.Simulation.CaptureCohortState(walkRestored)));
        var walkRestoredPlayer = walkRestored.Read("native-town", value => value);
        for (var step = 0; step < 512 && (player.NativeWalkPath is not null || player.CurrentAction is not null); step++) {
            var nextWalkTick = checked(runtime.LastAdvancedTick + 1);
            runtime.AdvanceTo(nextWalkTick);
            walkRestored.AdvanceTo(nextWalkTick);
        }
        Assert.Null(player.NativeWalkPath);
        Assert.Null(player.CurrentAction);
        Assert.Null(walkRestoredPlayer.NativeWalkPath);
        Assert.Null(walkRestoredPlayer.CurrentAction);
        Assert.Equal((walkTarget.X, walkTarget.Y), (player.PositionX, player.PositionY));
        Assert.Equal(runtime.LastAdvancedTick, walkRestored.LastAdvancedTick);
        Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
            codec.Serialize(walkCandidate.Simulation.CaptureCohortState(walkRestored)));
        Assert.Equal((player.PositionX, player.PositionY),
            (walkRestoredPlayer.PositionX, walkRestoredPlayer.PositionY));

        world.ApplyNativeTownMutation(AuthoritativeNativeTownMutation.OpenHive);
        world.ApplyNativeTownMutation(AuthoritativeNativeTownMutation.OpenGrave);
        world.ApplyNativeTownMutation(AuthoritativeNativeTownMutation.CleanFountain);
        var mutated = simulation.CaptureCohortState(runtime);
        Assert.Equal(randomBefore, mutated.RandomState);
        Assert.Equal(initialTown.FinalRandomState, mutated.NativeTown!.FinalRandomState);
        Assert.False(initialTown.NativePieceLayer.SequenceEqual(mutated.NativeTown.NativePieceLayer));
        Assert.Equal(3, mutated.NativeTown.Mutations.Count);
        Assert.Equal(1600, mutated.NativeTown.NativeMegaTileLayer.Count);
        Assert.Empty(mutated.Geometry!.Levels!.Single(level => level.LevelId == 0).NativeMegaTileLayer!);
        Assert.NotSame(presentation, world.CapturePresentationLevelGeometry().Single(level => level.LevelId == 0));

        // Enter a fresh Cathedral floor after Town changes; temporary admission must retain its Town owner.
        var commands = (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulation)!;
        for (uint sourceLevel = 0; sourceLevel <= 1; sourceLevel++) {
            var stair = world.World!.WarpTargets.Single(row => row.SourceLevelId == sourceLevel
                && row.TargetLevelId == sourceLevel + 1);
            player.PositionX = stair.TriggerX;
            player.PositionY = stair.TriggerY;
            Assert.True(commands.ExecuteLevelTrigger(player, 0, [player]).Succeeded);
            Assert.Equal(sourceLevel + 1, player.LevelId);
        }
        var state = simulation.CaptureCohortState(runtime);
        var encoded = codec.Serialize(state);
        Assert.Equal(encoded, codec.Serialize(codec.Deserialize(encoded)));
        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(encoded));
        var restored = candidate.CreateRuntime();
        Assert.Equal(encoded, codec.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        Assert.Equal(classId, candidate.Simulation.CaptureCohortState(restored).Participants.Single().Player.CharacterClassId);
        Assert.Equal(state.NativeTown!.NativeSpecialLayer,
            candidate.Simulation.WorldState.LevelGeometry.Single(level => level.LevelId == 0).NativeSpecialLayer);

        foreach (var forged in new[] {
            state with { NativeTown = state.NativeTown! with { FinalRandomState = state.NativeTown!.FinalRandomState ^ 1U } },
            state with { NativeTown = state.NativeTown! with { Mutations = [] } },
            state with { NativeTown = state.NativeTown! with { CatalogFingerprint = new string('0', 64) } },
        }) {
            Assert.Throws<InvalidDataException>(() => activation.CohortCandidateFactory!(forged));
            Assert.Equal(encoded, codec.Serialize(simulation.CaptureCohortState(runtime)));
        }
        Assert.Throws<InvalidDataException>(() => codec.Serialize(state with { FormatVersion = 10 }));
        foreach (var json in new[] {
            JsonNode.Parse(encoded)!.AsObject(), JsonNode.Parse(encoded)!.AsObject(), JsonNode.Parse(encoded)!.AsObject(),
        }.Select((document, index) => {
            if (index == 0) document.Remove("NativeTown");
            else if (index == 1) document["NativeTown"] = null;
            else document["NativeTown"]!["Unexpected"] = 1;
            return document.ToJsonString();
        }))
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(json));

        var next = checked(runtime.LastAdvancedTick + 1);
        runtime.AdvanceTo(next);
        restored.AdvanceTo(next);
        Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
            codec.Serialize(candidate.Simulation.CaptureCohortState(restored)));

        (int X, int Y) FindWalkTarget(int sourceX, int sourceY, int minimumLength)
        {
            for (var radius = minimumLength; radius <= 8; radius++) {
                for (var y = sourceY - radius; y <= sourceY + radius; y++) {
                    for (var x = sourceX - radius; x <= sourceX + radius; x++) {
                        if (Math.Max(Math.Abs(x - sourceX), Math.Abs(y - sourceY)) != radius
                            || !world.ContainsPosition(0, x, y) || !world.IsWalkable(0, x, y)
                            || world.IsOccupied(0, x, y, [player], player.EntityId))
                            continue;
                        var path = FindWalkPath(sourceX, sourceY, x, y);
                        if (path.Length >= minimumLength && !CrossesTownWarp(sourceX, sourceY, path))
                            return (x, y);
                    }
                }
            }
            throw new InvalidOperationException("The pinned Town geometry has no reachable destination-walk target.");
        }

        int[] FindWalkPath(int startX, int startY, int targetX, int targetY)
            => AuthoritativeNativePlayerPathfinder.FindPath(
                startX, startY, targetX, targetY, AuthoritativeNativePlayerPathfinder.MaximumPathLength,
                (x, y) => world.ContainsPosition(0, x, y) && world.IsWalkable(0, x, y)
                    && !world.IsOccupied(0, x, y, [player], player.EntityId),
                (fromX, fromY, toX, toY) => {
                    var dx = toX - fromX;
                    var dy = toY - fromY;
                    return dx == 0 || dy == 0
                        || world.IsWalkable(0, fromX + dx, fromY)
                            && world.IsWalkable(0, fromX, fromY + dy);
                });

        bool CrossesTownWarp(int sourceX, int sourceY, IReadOnlyList<int> directions)
        {
            foreach (var direction in directions) {
                var displacement = AuthoritativeNativePlayerPathfinder.GetDisplacement(direction);
                sourceX = unchecked((byte)(sourceX + displacement.X));
                sourceY = unchecked((byte)(sourceY + displacement.Y));
                if (world.World!.WarpTargets.Any(warp => warp.SourceLevelId == 0
                        && warp.TriggerX == sourceX && warp.TriggerY == sourceY))
                    return true;
            }
            return false;
        }
    }

    [Fact(Explicit = true)]
    public void CompiledTownProfileMatchesQualifiedNativeBackingCapture()
    {
        var path = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_TOWN_REFERENCE_PATH");
        Assert.False(string.IsNullOrWhiteSpace(path), "The qualified original-SP NotAvailable Town capture is required.");
        var catalog = AuthoritativeNativeTownCatalog.Load(LoadProfile("warrior"));
        var town = catalog.Capture(catalog.CreateInitialGeometry(), []);
        using var stream = File.OpenRead(path!);
        using var reader = new BinaryReader(stream);
        foreach (var value in new uint[] { town.FinalRandomState, (uint)town.ViewPositionX, (uint)town.ViewPositionY,
                     (uint)town.MinimumDungeonPositionX, (uint)town.MinimumDungeonPositionY,
                     (uint)town.MaximumDungeonPositionX, (uint)town.MaximumDungeonPositionY })
            Assert.Equal(value, reader.ReadUInt32());
        for (var cell = 0; cell < 112 * 112; cell++) {
            Assert.Equal((uint)town.NativePieceLayer[cell], reader.ReadUInt32());
            Assert.Equal((uint)town.NativeSpecialLayer[cell], reader.ReadUInt32());
            Assert.Equal((uint)town.NativeSolPropertyLayer[cell], reader.ReadUInt32());
            Assert.Equal((uint)town.NativeTransparencyLayer[cell], reader.ReadUInt32());
        }
        var protectedCells = town.NativeProtectedMegaCells.ToHashSet();
        for (var cell = 0; cell < 40 * 40; cell++) {
            Assert.Equal((uint)town.NativeMegaTileLayer[cell], reader.ReadUInt32());
            Assert.Equal(protectedCells.Contains(cell) ? 1U : 0U, reader.ReadUInt32());
        }
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact(Explicit = true)]
    public void LegacyVersionTenRetainsIndependentlyFrozenQualifiedJourneyBytes()
    {
        var path = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_TOWN_LEGACY_COHORT_PATH");
        Assert.False(string.IsNullOrWhiteSpace(path), "The qualified pre-Town v10 cohort path is required.");
        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(
            document.RootElement.GetProperty("SerializedModuleState").GetString()!));
        var bytes = Encoding.UTF8.GetBytes(payload);
        Assert.Equal(5803388, bytes.Length);
        Assert.Equal("18a4fab6082b80839d4e324efeb2bd4b205d9d5487fc3b2a1f4d2508bc55c576",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        var codec = new DiabloCohortCodec();
        var state = codec.Deserialize(payload);
        Assert.Equal(10, state.FormatVersion);
        Assert.Null(state.NativeTown);
        Assert.Equal(payload, codec.Serialize(state));
        foreach (var value in new JsonNode?[] { null, new JsonObject() }) {
            var forged = JsonNode.Parse(payload)!.AsObject();
            forged["NativeTown"] = value;
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(forged.ToJsonString()));
        }
    }

    private static ComposedContent LoadProfile(string symbol)
    {
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_TOWN_PROFILE_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(root), "The compiled format-5 class profile root is required.");
        var directory = Path.Combine(root!, symbol);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,
            "cathedral-native-town-progression-playtest.json")));
        var profile = document.RootElement;
        Assert.Equal(5, profile.GetProperty("formatVersion").GetInt32());
        var nested = profile.GetProperty("classProfile");
        var packs = new[] { "contentDirectory", "populationDirectory", "runtimeDirectory" }
            .Select(key => ContentPackLoader.LoadManifestDirectory(Path.Combine(directory, nested.GetProperty(key).GetString()!)))
            .Append(ContentPackLoader.LoadManifestDirectory(Path.Combine(directory, profile.GetProperty("townDirectory").GetString()!)))
            .ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        Assert.Equal(profile.GetProperty("contentManifestHash").GetString(), manifest.Sha256);
        return ComposedContent.Compose(manifest);
    }
}
