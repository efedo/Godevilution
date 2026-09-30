using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class PerformanceWorkloadObservationsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void StagedPlayersAreObservedIndependentlyOfLocalFirstOrder(int participants)
    {
        var snapshot = StagedSnapshot(participants);
        Assert.True(PerformanceWorkloadObservations.AllParticipantsStaged(snapshot, participants));
        var reverse = snapshot.Players.Reverse().ToArray();
        snapshot.Players.Clear();
        snapshot.Players.AddRange(reverse);
        Assert.True(PerformanceWorkloadObservations.AllParticipantsStaged(snapshot, participants));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CompleteParticipantCountDoesNotProveFinalStagingWasObserved(int staleSlot)
    {
        var snapshot = StagedSnapshot(4);
        snapshot.Players[staleSlot - 1].PositionX = 0;
        snapshot.Players[staleSlot - 1].PositionY = 0;
        Assert.Equal(4, snapshot.Players.Count); // The previous scene guard passed here.
        Assert.False(PerformanceWorkloadObservations.AllParticipantsStaged(snapshot, 4));
        Assert.False(PerformanceWorkloadObservations.StagedSlots(snapshot, 4)[staleSlot - 1]);
        Assert.True(PerformanceWorkloadObservations.AllParticipantsStaged(StagedSnapshot(4), 4));
    }

    [Fact]
    public void MovingMissingDuplicateOrWrongLevelPlayersCannotCloseStaging()
    {
        var snapshot = StagedSnapshot(4);
        snapshot.Players[3].CombatMode = PlayerCombatMode.WalkSouthwards;
        Assert.False(PerformanceWorkloadObservations.AllParticipantsStaged(snapshot, 4));
        snapshot = StagedSnapshot(4);
        snapshot.Players[3].LevelId = 1;
        Assert.False(PerformanceWorkloadObservations.AllParticipantsStaged(snapshot, 4));
        snapshot = StagedSnapshot(4);
        snapshot.Players[3].CharacterName = snapshot.Players[2].CharacterName;
        Assert.False(PerformanceWorkloadObservations.AllParticipantsStaged(snapshot, 4));
        Assert.False(PerformanceWorkloadObservations.AllParticipantsStaged(StagedSnapshot(1), 4));
        Assert.False(PerformanceWorkloadObservations.AllParticipantsStaged(null, 1));
        Assert.False(PerformanceWorkloadObservations.AllParticipantsStaged(StagedSnapshot(1), 2));
    }

    private static Snapshot StagedSnapshot(int participants)
    {
        var snapshot = new Snapshot();
        foreach (var slot in Enumerable.Range(1, participants)) {
            var position = slot switch { 1 => (0, 3), 2 => (3, 0), 3 => (0, 1), _ => (1, 1) };
            snapshot.Players.Add(new PlayerSnapshot {
                EntityId = (uint)slot, CharacterName = $"M0-Performance-{slot}", CharacterClassId = 1,
                LevelId = 0, PositionX = position.Item1, PositionY = position.Item2, CombatMode = PlayerCombatMode.Stand,
            });
        }
        return snapshot;
    }
}
