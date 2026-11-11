using System.Text.Json;
using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeLevelRetentionEvidenceTests
{
    [Fact]
    public void TownCheckpointRetainsActualFloorBaselinesThroughJsonRoundTrip()
    {
        var l1 = Floor(1); var l2 = Floor(2);
        var captured = new[] { NativeLevelRetentionEvidence.Capture(l1, 1), NativeLevelRetentionEvidence.Capture(l2, 2) };
        var recorded = JsonSerializer.Deserialize<NativeLevelRetentionBaseline[]>(JsonSerializer.Serialize(captured));
        var town = Floor(0); town.Monsters.Clear(); town.Corpses.Clear();
        var restored = NativeLevelRetentionEvidence.RequireBaselines(recorded, town, 1, 2);
        NativeLevelRetentionEvidence.Verify(l1, restored.Single(value => value.LevelId == 1));
        NativeLevelRetentionEvidence.Verify(l2, restored.Single(value => value.LevelId == 2));
        Assert.Empty(town.Corpses); // The evidence comes from the actual floor visits, not the final Town view.
        Assert.Equal(1U, Snapshot.Parser.ParseFrom(Convert.FromBase64String(restored[0].Payload)).Players[0].LevelId);
    }

    [Fact]
    public void TownOnlyCheckpointCannotSilentlyEstablishL1OrL2Evidence()
    {
        var town = Floor(0); town.Monsters.Clear(); town.Corpses.Clear();
        var missing = Assert.Throws<InvalidOperationException>(() =>
            NativeLevelRetentionEvidence.RequireBaselines(null, town, 1, 2));
        Assert.Contains("L1", missing.Message);
        Assert.Contains("saved Town view", missing.Message);
        Assert.Throws<InvalidOperationException>(() => NativeLevelRetentionEvidence.Capture(town, 1));
        town.Players[0].LevelId = 1;
        var empty = Assert.Throws<InvalidOperationException>(() => NativeLevelRetentionEvidence.Capture(town, 1));
        Assert.Contains("L1 retention baseline is empty", empty.Message);
    }

    [Theory]
    [InlineData("missing corpse")] [InlineData("moved corpse")] [InlineData("revived corpse")]
    [InlineData("missing dead actor")] [InlineData("revived dead actor")]
    public void RevisitRejectsLostMovedAndResurrectedFloorState(string mutation)
    {
        var before = Floor(1);
        var baseline = NativeLevelRetentionEvidence.Capture(before, 1);
        var after = before.Clone();
        switch (mutation) {
        case "missing corpse": after.Corpses.Clear(); break;
        case "moved corpse": after.Corpses[0].PositionX++; break;
        case "revived corpse":
            after.Monsters.Add(new MonsterSnapshot { EntityId = after.Corpses[0].EntityId, LevelId = 1, Alive = true }); break;
        case "missing dead actor": after.Monsters.Clear(); break;
        case "revived dead actor": after.Monsters[0].Alive = true; break;
        }
        var error = Assert.Throws<InvalidOperationException>(() => NativeLevelRetentionEvidence.Verify(after, baseline));
        Assert.Contains("L1", error.Message);
    }

    [Fact]
    public void NativeDeathConversionKeepsTheReservedDeadActorAsACorpse()
    {
        var before = Floor(2);
        var baseline = NativeLevelRetentionEvidence.Capture(before, 2);
        var after = before.Clone(); var dead = after.Monsters[0];
        after.Corpses.Add(new CorpseSnapshot { EntityId = dead.EntityId, LevelId = dead.LevelId,
            PositionX = dead.PositionX, PositionY = dead.PositionY });
        after.Monsters.Clear();
        NativeLevelRetentionEvidence.Verify(after, baseline);
    }

    [Fact]
    public void MissingDuplicateEmptyOrHiddenBaselinesFailInsteadOfSkippingChecks()
    {
        var l1 = NativeLevelRetentionEvidence.Capture(Floor(1), 1);
        var l2 = NativeLevelRetentionEvidence.Capture(Floor(2), 2);
        var town = Floor(0);
        Assert.Throws<InvalidOperationException>(() => NativeLevelRetentionEvidence.RequireBaselines([l1], town, 1, 2));
        Assert.Throws<InvalidOperationException>(() => NativeLevelRetentionEvidence.RequireBaselines([l1, l1], town, 1, 2));
        var empty = new Snapshot { Players = { new PlayerSnapshot { EntityId = 1, LevelId = 2 } } };
        var invalid = l2 with { Payload = Convert.ToBase64String(empty.ToByteArray()) };
        var error = Assert.Throws<InvalidOperationException>(() =>
            NativeLevelRetentionEvidence.RequireBaselines([l1, invalid], town, 1, 2));
        Assert.Contains("L2 retention baseline is empty", error.Message);
        Assert.Throws<InvalidOperationException>(() => NativeLevelRetentionEvidence.Verify(town, l1));
    }

    [Fact]
    public void OlderCompleteCheckpointCanSupplyNonemptyLegacyFloorEvidence()
    {
        var complete = Floor(0); complete.PlayerRoster.Clear(); complete.Corpses.Clear(); complete.Monsters.Clear();
        foreach (var level in new uint[] { 1, 2 }) {
            complete.Corpses.AddRange(Floor(level).Corpses);
            complete.Monsters.AddRange(Floor(level).Monsters);
        }
        var baselines = NativeLevelRetentionEvidence.RequireBaselines(null, complete, 1, 2);
        Assert.Equal(2, baselines.Length);
        foreach (var baseline in baselines) NativeLevelRetentionEvidence.Verify(Floor(baseline.LevelId), baseline);
    }

    private static Snapshot Floor(uint level) => new() { Tick = 10,
        Players = { new PlayerSnapshot { EntityId = 1, LevelId = level } },
        PlayerRoster = { new PlayerRosterEntry { EntityId = 1, LevelId = level } },
        Corpses = { new CorpseSnapshot { EntityId = 100 + level, LevelId = level, PositionX = 2, PositionY = 3 } },
        Monsters = { new MonsterSnapshot { EntityId = 200 + level, LevelId = level, PositionX = 4, PositionY = 5,
            Alive = false, HitPoints = 0, MaxHitPoints = 64, DeathTicksRemaining = 2 } },
    };
}
