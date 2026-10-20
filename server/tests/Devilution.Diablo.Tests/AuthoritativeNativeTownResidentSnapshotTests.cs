using System.Text.Json;
using Devilution.Data;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Verifies the active-profile resident projection is re-derived after a detached cold restore. */
public sealed class AuthoritativeNativeTownResidentSnapshotTests
{
    [Fact(Explicit = true)]
    public void NativeTownResidentProjectionSurvivesCohortColdRestore()
    {
        var content = LoadProfile("warrior");
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
            .ActivateNativeTownProgression(content, 2588);
        var catalog = AuthoritativeNativeTownCatalog.Load(content);
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-town-residents",
            0,
            null,
            null,
            null,
            startingLevelId: 0,
            startingPositionX: 75,
            startingPositionY: 68);
        var runtime = activation.Simulation.CreateRuntime(template);
        runtime.Write("native-town-residents", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-town-residents", value => value);

        var initial = activation.Simulation.CaptureSnapshot(player, runtime.LastAdvancedTick);
        Assert.Equal(catalog.Fingerprint, initial.NativeTownCatalogFingerprint);
        AssertCanonicalRoster(initial.NativeTownResidents);
        Assert.False(initial.NativeTownResidents is DiabloNativeTownResidentState[]);

        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(activation.Simulation.CaptureCohortState(runtime));
        var detachedState = codec.Deserialize(bytes);
        var candidate = activation.CohortCandidateFactory!(detachedState);
        var restoredRuntime = candidate.CreateRuntime();
        var restoredPlayer = restoredRuntime.Read("native-town-residents", value => value);
        var restored = candidate.Simulation.CaptureSnapshot(
            restoredPlayer,
            restoredRuntime.LastAdvancedTick);

        Assert.Equal(initial.NativeTownCatalogFingerprint, restored.NativeTownCatalogFingerprint);
        Assert.Equal(initial.NativeTownResidents, restored.NativeTownResidents);
        AssertCanonicalRoster(restored.NativeTownResidents);
    }

    private static void AssertCanonicalRoster(IReadOnlyList<DiabloNativeTownResidentState> residents)
    {
        Assert.Equal(11, residents.Count);
        Assert.Equal(Enumerable.Range(0, 11), residents.Select(resident => resident.ResidentSlot));
        Assert.Equal(new[] { 0, 1, 3, 4, 5, 6, 7, 8, 9, 9, 9 },
            residents.Select(resident => resident.TownerId));
        Assert.All(residents, resident => Assert.Equal(0U, resident.LevelId));
        Assert.Equal(new[] { (62, 63, 1), (55, 79, 7), (55, 62, 1), (62, 71, 0), (71, 84, 0),
                (80, 20, 0), (43, 66, 0), (11, 53, 0), (58, 16, 1), (56, 14, 3), (59, 20, 4) },
            residents.Select(resident => (resident.PositionX, resident.PositionY, resident.NativeDirection)));
    }

    private static ComposedContent LoadProfile(string symbol)
    {
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_TOWN_PROFILE_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(root), "The compiled format-5 Town profile root is required.");
        var directory = Path.Combine(root!, symbol);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            directory,
            "cathedral-native-town-progression-playtest.json")));
        var profile = document.RootElement;
        Assert.Equal(5, profile.GetProperty("formatVersion").GetInt32());
        var classProfile = profile.GetProperty("classProfile");
        var packs = new[] { "contentDirectory", "populationDirectory", "runtimeDirectory" }
            .Select(key => ContentPackLoader.LoadManifestDirectory(Path.Combine(
                directory,
                classProfile.GetProperty(key).GetString()!)))
            .Append(ContentPackLoader.LoadManifestDirectory(Path.Combine(
                directory,
                profile.GetProperty("townDirectory").GetString()!)))
            .ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        Assert.Equal(profile.GetProperty("contentManifestHash").GetString(), manifest.Sha256);
        return ComposedContent.Compose(manifest);
    }
}
