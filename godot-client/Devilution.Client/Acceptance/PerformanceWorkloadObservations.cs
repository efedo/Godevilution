using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Read-only assertions for the opt-in town workload, never gameplay logic. */
public static class PerformanceWorkloadObservations
{
    public static bool AllParticipantsStaged(Snapshot? snapshot, int participants)
        => participants is 1 or 4 && snapshot?.Players.Count == participants
            && StagedSlots(snapshot, participants).All(ready => ready);

    public static bool[] StagedSlots(Snapshot? snapshot, int participants)
        => Enumerable.Range(1, participants).Select(slot => {
            var player = FindSlot(snapshot, slot);
            var destination = slot switch {
                1 => (X: 0, Y: 3), 2 => (X: 3, Y: 0),
                3 => (X: 0, Y: 1), 4 => (X: 1, Y: 1), _ => (X: -1, Y: -1),
            };
            return player is not null && player.CharacterClassId == 1 && player.LevelId == 0
                && player.PositionX == destination.X && player.PositionY == destination.Y && IsSettled(player);
        }).ToArray();

    public static bool[] SettledSlots(Snapshot? snapshot, int participants)
        => Enumerable.Range(1, participants)
            .Select(slot => FindSlot(snapshot, slot) is { } player && IsSettled(player)).ToArray();

    private static PlayerSnapshot? FindSlot(Snapshot? snapshot, int slot)
    {
        var expectedName = $"M0-Performance-{slot}";
        var matching = snapshot?.Players.Where(player => player.CharacterName == expectedName).ToArray();
        return matching?.Length == 1 ? matching[0] : null;
    }

    private static bool IsSettled(PlayerSnapshot player)
        => player.CombatMode == PlayerCombatMode.Stand && player.CurrentAction is null;
}
