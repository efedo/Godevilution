using Devilution.Protocol.V1;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Client-model coverage for historical consumable references after their effect. */
public sealed class PostEffectCastSourceTests
{
    [Fact]
    public void AcceptsConsumedConsumableWithReindexedInventory()
    {
        var snapshot = CastSnapshot(
            CastSourceKind.ConsumableItem,
            effectApplied: true,
            new PlayerItemReference {
                Location = PlayerItemLocation.Inventory,
                Slot = 3,
                StableItemId = 42,
            },
            player => player.Inventory.Add(new ItemSnapshot {
                ItemSeed = 9,
                State = new ItemStateSnapshot { StableItemId = 99 },
            }));

        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        Assert.Equal(PlayerActionKind.Cast, model.Snapshot!.Players[0].CurrentAction.Kind);
    }

    [Fact]
    public void PreservesStableZeroAllowanceBeforeConsumableEffect()
    {
        var snapshot = CastSnapshot(
            CastSourceKind.ConsumableItem,
            effectApplied: false,
            new PlayerItemReference {
                Location = PlayerItemLocation.Inventory,
                Slot = 0,
            },
            player => player.Inventory.Add(new ItemSnapshot {
                ItemSeed = 9,
                State = new ItemStateSnapshot { StableItemId = 42 },
            }));

        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
    }

    [Fact]
    public void PreservesNonStashPageCompatibilityAfterConsumableEffect()
    {
        var snapshot = CastSnapshot(
            CastSourceKind.ConsumableItem,
            effectApplied: true,
            new PlayerItemReference {
                Location = PlayerItemLocation.Inventory,
                Slot = 3,
                Page = 7,
            });

        new AuthoritativeClientModel().Apply(
            new AuthoritativeClientMessage(Snapshot: snapshot));
    }

    [Fact]
    public void RejectsMissingConsumableBeforeEffect()
    {
        var snapshot = CastSnapshot(
            CastSourceKind.ConsumableItem,
            effectApplied: false,
            new PlayerItemReference {
                Location = PlayerItemLocation.Inventory,
                Slot = 0,
                StableItemId = 42,
            });

        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeClientModel().Apply(
                new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void KeepsChargedSourceMembershipStrictAfterEffect()
    {
        var snapshot = CastSnapshot(
            CastSourceKind.ChargedItem,
            effectApplied: true,
            new PlayerItemReference {
                Location = PlayerItemLocation.Equipment,
                Slot = 4,
                StableItemId = 99,
            },
            player => player.Equipment.Add(new EquippedItemSnapshot {
                Slot = 4,
                ItemSeed = 9,
                State = new ItemStateSnapshot { StableItemId = 42 },
            }));

        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeClientModel().Apply(
                new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsMalformedHistoricalConsumableLocation()
    {
        var source = new PlayerItemReference {
            Location = (PlayerItemLocation)99,
            Slot = 0,
        };
        AssertMalformedHistoricalSource(source);
    }

    [Fact]
    public void RejectsOutOfRangeHistoricalConsumableSlot()
    {
        var source = new PlayerItemReference {
            Location = PlayerItemLocation.Inventory,
            Slot = 40,
        };
        AssertMalformedHistoricalSource(source);
    }

    [Fact]
    public void RejectsOutOfRangeHistoricalConsumablePage()
    {
        var source = new PlayerItemReference {
            Location = PlayerItemLocation.Stash,
            Slot = 0,
            Page = 100,
        };
        AssertMalformedHistoricalSource(source);
    }

    private static void AssertMalformedHistoricalSource(PlayerItemReference source)
    {
        var snapshot = CastSnapshot(
            CastSourceKind.ConsumableItem,
            effectApplied: true,
            source);

        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeClientModel().Apply(
                new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    private static Snapshot CastSnapshot(
        CastSourceKind source,
        bool effectApplied,
        PlayerItemReference sourceItem,
        Action<PlayerSnapshot>? populate = null)
    {
        var player = new PlayerSnapshot {
            EntityId = 7,
            CombatMode = PlayerCombatMode.Spell,
            CombatModeEndTick = 3,
        };
        populate?.Invoke(player);
        player.CurrentAction = new PlayerActionSnapshot {
            Kind = PlayerActionKind.Cast,
            StartedAtTick = 1,
            EffectTick = 2,
            ReadyTick = 2,
            EndTick = 3,
            EffectApplied = effectApplied,
            Cast = new CastRequested {
                SpellId = 1,
                Source = source,
                SourceItem = sourceItem,
            },
        };
        return new Snapshot {
            Tick = effectApplied ? 2UL : 1UL,
            Players = { player },
        };
    }
}
