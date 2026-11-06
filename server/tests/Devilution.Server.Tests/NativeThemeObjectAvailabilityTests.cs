using System.Text.Json;
using Devilution.Client.Acceptance;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeThemeObjectAvailabilityTests
{
    [Fact(Explicit = true)]
    public void MeasureShallowThemeTargetsWithTheJourneysActualPathRules()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_THEME_AVAILABILITY_METADATA");
        if (string.IsNullOrWhiteSpace(directory)) Assert.Skip("Select private native Catacombs profile metadata.");
        var pack = ContentPackLoader.LoadManifestDirectory(directory);
        var metadata = AuthoritativeCatacombsPopulationMetadata.LoadProfileSidecar(new ContentManifest("theme-availability", "1.0.0", [pack]))!;
        Assert.NotNull(metadata);
        var rows = new List<object>();
        for (uint seed = 0; seed < 100; seed++) {
            var fresh = AuthoritativeCatacombsFreshLevelGenerator.Generate(metadata, 5, seed, 0,
                AuthoritativeCatacombsEntry.Main, new(11776, 640, 10, 8, new(75, 68), true), stableLevelSymbol: "catacombs5");
            var (snapshot, geometry) = NativeShrineAvailabilityTests.CreateApproachSnapshot(fresh);
            var player = snapshot.Players.Single();
            var targets = snapshot.Objects.Where(value => NativeThemeObjectJourneyEvidence.CanDemonstrate(value, player))
                .Select(value => new {Type = value.NativeObjectId, X = value.PositionX, Y = value.PositionY,
                    Steps = NativeThemeObjectJourneyEvidence.FindApproach(snapshot, geometry, value)?.Count})
                .Where(value => value.Steps is <= 24)
                .OrderByDescending(value => NativeThemeObjectJourneyEvidence.IsLoot(value.Type))
                .ThenBy(value => value.Steps).ToArray();
            rows.Add(new {Seed = seed, Depth = 5, EntryX = player.PositionX, EntryY = player.PositionY,
                InitialItemCount = fresh.Population.State.ActiveItemCount, Targets = targets});
        }
        var output = Environment.GetEnvironmentVariable("GODEVILUTION_THEME_AVAILABILITY_REPORT")
            ?? throw new InvalidOperationException("Select a private theme availability report path.");
        File.WriteAllText(output, JsonSerializer.Serialize(rows));
        Assert.Equal(100, rows.Count);
    }
}
