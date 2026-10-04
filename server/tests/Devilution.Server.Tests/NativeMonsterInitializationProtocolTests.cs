using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeMonsterInitializationProtocolTests
{
    [Fact]
    public void NativeProfileWithNoActorsCannotLoseMetadataThroughLegacySave()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeMonsterMetadataFingerprint = new string('A', 64),
        });
        var player = DiabloPlayerTemplate.CreateDefault(100, 6400, 3200, 0).CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 0);
        Assert.Empty(snapshot.Monsters);
        Assert.Equal(new string('A', 64), snapshot.NativeMonsterMetadataFingerprint);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSaveCodec().Serialize(snapshot));
    }

    [Theory]
    [InlineData(0U, 1U)]
    [InlineData(1U, 0U)]
    [InlineData(0U, 0U)]
    [InlineData(0x80000000U, 0xFFFFFFFFU)]
    public void ExplicitNativePolicyRoundTripsExactZeroSeedsWithoutChangingLegacyHashes(uint itemSeed, uint aiSeed)
    {
        var simulation = DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            CombatTargets = [new AuthoritativeCombatTarget(9, 3, 3, 10)],
        });
        var player = DiabloPlayerTemplate.CreateDefault(100, 6400, 3200, 0).CreatePlayer();
        player.EntityId = 7;
        var baseline = simulation.CaptureSnapshot(player, 0);
        var native = baseline with { Monsters = [Assert.Single(baseline.Monsters) with {
            NativeInitializationPresent = true, MonsterItemSeed = itemSeed, MonsterAiSeed = aiSeed,
        }] };
        var projected = DiabloProtocolMapper.ToProtocolSnapshot(native);
        AuthoritativeSnapshotValidator.Validate(projected);
        Assert.True(projected.Monsters[0].HasNativeInitializationPresent);
        var restored = Assert.Single(DiabloProtocolMapper.FromProtocolSnapshot(projected).Monsters);
        Assert.True(restored.NativeInitializationPresent);
        Assert.Equal((itemSeed, aiSeed), (restored.MonsterItemSeed, restored.MonsterAiSeed));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSaveCodec().Serialize(native));
        var unmarked = projected.Clone();
        unmarked.Monsters[0].ClearNativeInitializationPresent();
        Assert.NotEqual(SnapshotStateHasher.Compute(unmarked), SnapshotStateHasher.Compute(projected));
        var explicitFalse = unmarked.Clone();
        explicitFalse.Monsters[0].NativeInitializationPresent = false;
        Assert.Equal(SnapshotStateHasher.Compute(unmarked), SnapshotStateHasher.Compute(explicitFalse));
        Assert.False(DiabloProtocolMapper.ToProtocolSnapshot(baseline).Monsters[0].HasNativeInitializationPresent);
    }
}
