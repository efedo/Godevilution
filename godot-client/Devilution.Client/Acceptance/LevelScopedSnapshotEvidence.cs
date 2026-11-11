using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** A hidden floor is verified on its real revisit; it is never treated as an empty retained floor. */
internal static class LevelScopedSnapshotEvidence
{
    internal static bool IncludesLevel(Snapshot snapshot, uint level)
        => snapshot.PlayerRoster.Count == 0 || snapshot.Players.Count != 0 && snapshot.Players[0].LevelId == level;

    /** The unchanged authority hash still proves the entire cold world, even with a scoped body. */
    internal static string? CompleteWorldHash(Snapshot snapshot)
        => snapshot.PlayerRoster.Count == 0 ? null : snapshot.StateSha256;

    internal static Snapshot ExpectedView(Snapshot expected, Snapshot observed)
        => observed.PlayerRoster.Count == 0 ? expected : expected.ForReceivingLevel();
}
