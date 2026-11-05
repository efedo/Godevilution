using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

/** Private command evidence reproduces occupancy at admission without running the journey. */
public sealed class NativeJourneyDoorApproachReplayTests(ITestOutputHelper output)
{
    [Fact]
    public void SeededDoorApproachRefusalsAreMovingActorsRatherThanDoorTiles()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_DOOR_REPLAY_LOG");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select the private seeded journey command log and profile to replay its two approach refusals.");
        var records = CommandLogReader.ReadAll(Path.Combine(directory!, HostCommandLog.LogFileName));
        var header = records.First(value => value.Header is not null).Header;
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime",
            "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory(DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeAllClassTownId);
        var registry = new GameplayModuleRegistry();
        registry.Load(factory.CreateBuiltInModules(), new GameplayModuleContext(manifest, new DelegateAuthoritativeClock(() => 0)));
        var testPath = header.ServerArguments[header.ServerArguments.IndexOf("--test-participant") + 1];
        var adapter = factory.Activate(registry, ComposedContent.Compose(manifest),
            new AuthoritativePlayerStartConfiguration(null, null, null) { RandomSeed = header.RandomSeed,
                StartingLevelId = 0, TestParticipantDefinition = File.ReadAllText(testPath) });
        var commands = new AuthoritativeCommandServer(adapter, CommandAdmissionPolicy.ForLiveHost(20));
        var found = 0;
        foreach (var record in records) {
            if (record.ParticipantJoined is { Restored: false } joined) {
                commands.AdvanceTo(joined.Tick);
                adapter.InitializeParticipant(joined.SessionId, new AuthoritativeParticipantSelection(joined.RequestedClassId) {
                    EntityId = joined.EntityId, CharacterName = joined.CharacterName,
                });
            }
            if (record.ServerCommand is not { } logged) continue;
            if (logged.Command.ClientSequence > 444) break;
            commands.AdvanceTo(logged.CurrentTick);
            if (logged.Command.ClientSequence is 439 or 444) {
                var snapshot = adapter.CreateSnapshot(logged.SessionId, 1, logged.CurrentTick);
                var (x, y) = logged.Command.ClientSequence == 439 ? (53, 55) : (54, 57);
                var geometry = snapshot.LevelGeometry.Single(value => value.LevelId == 6);
                Assert.DoesNotContain(y * geometry.Width + x, geometry.BlockedCells);
                Assert.DoesNotContain(y * geometry.Width + x, geometry.StaticObjectBlockedCells);
                Assert.DoesNotContain(snapshot.Objects, value => value.LevelId == 6 && value.NativeObjectId is 42 or 43
                    && value.PositionX == x && value.PositionY == y);
                var blockers = snapshot.Monsters.Where(value => value.LevelId == 6 && value.Alive
                    && (value.PositionX == x && value.PositionY == y
                        || value.Moving && value.FuturePositionX == x && value.FuturePositionY == y)).ToArray();
                Assert.True(blockers.Length > 0, $"No actor at {x},{y}, tick {snapshot.Tick}.");
                output.WriteLine($"Step {x},{y} at tick {snapshot.Tick} blocked by "
                    + string.Join("; ", blockers.Select(value => $"{value.EntityId} AI {value.NativeAi} at {value.PositionX},{value.PositionY} future {value.FuturePositionX},{value.FuturePositionY}")));
                Assert.Equal(CommandRejectReason.InvalidTarget, logged.Result.RejectReason);
                found++;
            }
            var actual = commands.Process(logged.SessionId, logged.Command.Clone(), logged.CurrentTick);
            Assert.True(actual.Equals(logged.Result), $"Replay sequence {logged.Command.ClientSequence}: expected {logged.Result}, actual {actual}.");
        }
        Assert.Equal(2, found);
    }
}
