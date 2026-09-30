using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class PostEffectCastSourceProjectionTests
{
    [Theory]
    [InlineData(PlayerItemLocation.Inventory, 39U, 0U)]
    [InlineData(PlayerItemLocation.Belt, 7U, 0U)]
    [InlineData(PlayerItemLocation.Equipment, 6U, 0U)]
    [InlineData(PlayerItemLocation.Stash, 99U, 0U)]
    public void ConsumedSourceSurvivesProjectionAndSaveImport(
        PlayerItemLocation location, uint slot, uint page)
    {
        var snapshot = CreateSnapshot(location, slot, page);
        AuthoritativeSnapshotValidator.Validate(snapshot);
        var imported = DiabloProtocolMapper.FromProtocolSnapshot(snapshot);
        var projected = DiabloProtocolMapper.ToProtocolSnapshot(imported);
        AuthoritativeSnapshotValidator.Validate(projected);

        Assert.Empty(projected.Players[0].Inventory);
        Assert.Equal(snapshot.Players[0].CurrentAction, projected.Players[0].CurrentAction);
        Assert.True(projected.Players[0].CurrentAction.EffectApplied);
    }

    [Theory]
    [InlineData(PlayerItemLocation.Inventory, 40U, 0U)]
    [InlineData(PlayerItemLocation.Belt, 8U, 0U)]
    [InlineData(PlayerItemLocation.Equipment, 7U, 0U)]
    [InlineData(PlayerItemLocation.Stash, 100U, 0U)]
    [InlineData(PlayerItemLocation.Stash, 0U, uint.MaxValue)]
    [InlineData((PlayerItemLocation)99, 0U, 0U)]
    public void ConsumedSourceStillRequiresBoundedHistoricalReference(
        PlayerItemLocation location, uint slot, uint page)
    {
        AssertRejected(CreateSnapshot(location, slot, page));
    }

    [Fact]
    public void MissingSourceIsRejectedBeforeEffect()
    {
        var snapshot = CreateSnapshot(PlayerItemLocation.Inventory, 0, 0);
        snapshot.Tick = 4;
        snapshot.Players[0].CurrentAction.EffectApplied = false;
        AssertRejected(snapshot);
    }

    [Fact]
    public void ConsumedSourceStillRequiresAnItemReference()
    {
        var snapshot = CreateSnapshot(PlayerItemLocation.Inventory, 0, 0);
        snapshot.Players[0].CurrentAction.Cast.SourceItem = null;
        AssertRejected(snapshot);
    }

    [Fact]
    public void ChargedSourcesStillRequireCurrentOwnershipAfterEffect()
    {
        var snapshot = CreateSnapshot(PlayerItemLocation.Equipment, 4, 0);
        snapshot.Players[0].CurrentAction.Cast.Source = CastSourceKind.ChargedItem;
        AssertRejected(snapshot);
    }

    [Fact]
    public void NonItemSourceCannotCarryAConsumedReference()
    {
        var snapshot = CreateSnapshot(PlayerItemLocation.Inventory, 0, 0);
        snapshot.Players[0].CurrentAction.Cast.Source = CastSourceKind.Ability;
        AssertRejected(snapshot);
    }

    [Fact]
    public void ConsumedSourceDoesNotBypassEffectTickConsistency()
    {
        var snapshot = CreateSnapshot(PlayerItemLocation.Inventory, 0, 0);
        snapshot.Tick = 4;
        AssertRejected(snapshot);
    }

    private static void AssertRejected(Snapshot snapshot)
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(snapshot));
        Assert.Throws<InvalidDataException>(() => DiabloProtocolMapper.FromProtocolSnapshot(snapshot));
    }

    private static Snapshot CreateSnapshot(PlayerItemLocation location, uint slot, uint page)
        => new() {
            Tick = 5,
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Life = 1,
                    LifeMaximum = 1,
                    BaseLifeMaximum = 1,
                    CharacterLevel = 1,
                    CombatMode = PlayerCombatMode.Spell,
                    CombatModeEndTick = 10,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Cast,
                        StartedAtTick = 1,
                        EffectTick = 5,
                        ReadyTick = 5,
                        EndTick = 10,
                        EffectApplied = true,
                        Cast = new CastRequested {
                            SpellId = 1,
                            Source = CastSourceKind.ConsumableItem,
                            SourceItem = new PlayerItemReference {
                                Location = location,
                                Slot = slot,
                                Page = page,
                                StableItemId = 42,
                            },
                        },
                    },
                },
            },
        };
}
