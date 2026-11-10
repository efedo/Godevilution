using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

internal sealed record NativeLevelRetentionBaseline(uint LevelId, string Payload);

/** Sparse, observed floor evidence; a Town publication cannot establish a hidden floor's baseline. */
internal static class NativeLevelRetentionEvidence
{
    internal static NativeLevelRetentionBaseline Capture(Snapshot snapshot, uint level)
    {
        if (snapshot.Players.Count != 1 || snapshot.Players[0].LevelId != level)
            throw new InvalidOperationException($"L{level} retention baseline requires the player on that level.");
        return CaptureRows(snapshot, level);
    }

    internal static NativeLevelRetentionBaseline[] RequireBaselines(
        NativeLevelRetentionBaseline[]? recorded, Snapshot legacyFullSnapshot, params uint[] levels)
    {
        if (recorded is null) {
            if (legacyFullSnapshot.PlayerRoster.Count != 0)
                throw new InvalidOperationException($"Missing L{levels[0]} retention baseline: the saved Town view contains no floor evidence; rerun the continuation.");
            // Older complete publications did expose these rows while the player was in Town.
            recorded = levels.Select(level => CaptureRows(legacyFullSnapshot, level)).ToArray();
        }
        if (recorded.Length != levels.Length || recorded.Select(value => value.LevelId).Distinct().Count() != levels.Length
            || levels.Any(level => !recorded.Any(value => value.LevelId == level)))
            throw new InvalidOperationException("The continuation checkpoint omitted or duplicated a retained floor baseline.");
        foreach (var baseline in recorded) Decode(baseline);
        return recorded;
    }

    internal static void Verify(Snapshot snapshot, NativeLevelRetentionBaseline baseline)
    {
        var expected = Decode(baseline);
        if (!LevelScopedSnapshotEvidence.IncludesLevel(snapshot, baseline.LevelId))
            throw new InvalidOperationException($"L{baseline.LevelId} retention cannot be verified from a hidden floor.");
        foreach (var corpse in expected.Corpses) {
            if (snapshot.Corpses.Count(value => Same(value, corpse)) != 1
                || snapshot.Monsters.Any(value => value.EntityId == corpse.EntityId && value.Alive))
                throw new InvalidOperationException($"Retained L{baseline.LevelId} changed, lost or revived corpse {corpse.EntityId}.");
        }
        foreach (var monster in expected.Monsters) {
            var dead = snapshot.Monsters.Any(value => value.EntityId == monster.EntityId
                && value.LevelId == monster.LevelId && !value.Alive);
            // Native death conversion legitimately replaces the actor with its corpse before publication.
            var converted = snapshot.Corpses.Any(value => value.EntityId == monster.EntityId
                && value.LevelId == monster.LevelId && value.PositionX == monster.PositionX && value.PositionY == monster.PositionY);
            if ((!dead && !converted) || snapshot.Monsters.Any(value => value.EntityId == monster.EntityId && value.Alive))
                throw new InvalidOperationException($"Retained L{baseline.LevelId} lost or revived dead actor {monster.EntityId}.");
        }
    }

    private static NativeLevelRetentionBaseline CaptureRows(Snapshot snapshot, uint level)
    {
        var proof = new Snapshot { Tick = snapshot.Tick, Players = { new PlayerSnapshot {
            EntityId = snapshot.Players[0].EntityId, LevelId = level } } };
        var observingLevel = snapshot.Players[0].LevelId == level;
        bool Belongs(uint rowLevel) => observingLevel ? AuthoritativeLevelVisibility.IsVisible(rowLevel, level) : rowLevel == level;
        proof.Corpses.AddRange(snapshot.Corpses.Where(value => Belongs(value.LevelId)).Select(value => value.Clone()));
        proof.Monsters.AddRange(snapshot.Monsters.Where(value => Belongs(value.LevelId) && !value.Alive).Select(value => value.Clone()));
        var baseline = new NativeLevelRetentionBaseline(level, Convert.ToBase64String(proof.ToByteArray()));
        Decode(baseline);
        return baseline;
    }

    private static Snapshot Decode(NativeLevelRetentionBaseline baseline)
    {
        var proof = Snapshot.Parser.ParseFrom(Convert.FromBase64String(baseline.Payload));
        if (proof.Players.Count != 1 || proof.Players[0].EntityId == 0 || proof.Players[0].LevelId != baseline.LevelId
            || proof.Corpses.Count + proof.Monsters.Count == 0
            || proof.Corpses.Select(value => value.EntityId).Concat(proof.Monsters.Select(value => value.EntityId))
                .Distinct().Count() != proof.Corpses.Count + proof.Monsters.Count
            || proof.Corpses.Any(value => value.EntityId == 0 || !AuthoritativeLevelVisibility.IsVisible(value.LevelId, baseline.LevelId))
            || proof.Monsters.Any(value => value.EntityId == 0 || !AuthoritativeLevelVisibility.IsVisible(value.LevelId, baseline.LevelId) || value.Alive))
            throw new InvalidOperationException($"L{baseline.LevelId} retention baseline is empty or invalid.");
        return proof;
    }

    private static bool Same(CorpseSnapshot actual, CorpseSnapshot expected)
        => actual.EntityId == expected.EntityId && actual.LevelId == expected.LevelId
            && actual.PositionX == expected.PositionX && actual.PositionY == expected.PositionY;
}
