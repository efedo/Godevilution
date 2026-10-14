using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeBeltConsumableSelectorTests
{
    [Fact]
    public void SelectsTheExactTargetlessBeltItemWithoutChangingPlayerState()
    {
        var player = Player(
            BeltItem(2, 702, ItemActionKind.RestorePartialLife),
            BeltItem(5, 705, ItemActionKind.RestoreFullMana));

        var selected = AuthoritativeBeltConsumableSelector.Select(player, 5);

        Assert.NotNull(selected);
        Assert.Equal(PlayerItemLocation.Belt, selected.Location);
        Assert.Equal(5u, selected.Slot);
        Assert.Equal(705u, selected.StableItemId);
        Assert.Equal(2, player.Belt.Count);
        Assert.Equal(2u, player.Belt[0].Slot);
        Assert.Equal(5u, player.Belt[1].Slot);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void RejectsOutOfRangeBeltSlot(int slot)
    {
        var player = Player(BeltItem(0, 700, ItemActionKind.RestorePartialLife));

        Assert.Null(AuthoritativeBeltConsumableSelector.Select(player, slot));
    }

    [Fact]
    public void RejectsNullPlayer()
    {
        Assert.Null(AuthoritativeBeltConsumableSelector.Select(null, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsDeadPlayerForOtherwiseUsableItem(int life)
    {
        var player = PlayerWithLife(life, BeltItem(3, 703, ItemActionKind.RestorePartialLife));

        Assert.Null(AuthoritativeBeltConsumableSelector.Select(player, 3));
    }

    [Theory]
    [InlineData(PlayerCombatMode.NewLevel)]
    [InlineData(PlayerCombatMode.Quit)]
    public void RejectsLevelTransitionAndQuitModes(PlayerCombatMode combatMode)
    {
        var player = PlayerWithLife(
            100,
            BeltItem(3, 703, ItemActionKind.RestorePartialLife));
        player.CombatMode = combatMode;

        Assert.Null(AuthoritativeBeltConsumableSelector.Select(player, 3));
    }

    [Theory]
    [InlineData(PlayerCombatMode.Stand)]
    [InlineData(PlayerCombatMode.WalkNorthwards)]
    [InlineData(PlayerCombatMode.Attack)]
    [InlineData(PlayerCombatMode.RangedAttack)]
    public void AllowsUseDuringOrdinaryActions(PlayerCombatMode combatMode)
    {
        var player = PlayerWithLife(
            100,
            BeltItem(3, 703, ItemActionKind.RestorePartialLife));
        player.CombatMode = combatMode;

        Assert.NotNull(AuthoritativeBeltConsumableSelector.Select(player, 3));
    }

    [Fact]
    public void RejectsMissingDepletedInertOrTargetedBeltItems()
    {
        var missing = Player(BeltItem(1, 701, ItemActionKind.RestorePartialLife));
        var depleted = Player(BeltItem(2, 702, ItemActionKind.RestorePartialLife, deleted: true));
        var inert = Player(BeltItem(3, 703, ItemActionKind.Unspecified));
        var targeted = Player(BeltItem(4, 704, ItemActionKind.ApplySharpnessOil));
        var zeroIdentity = Player(BeltItem(5, 0, ItemActionKind.RestorePartialLife));
        var missingState = Player(new BeltItemSnapshot { Slot = 7 });

        Assert.Null(AuthoritativeBeltConsumableSelector.Select(missing, 0));
        Assert.Null(AuthoritativeBeltConsumableSelector.Select(depleted, 2));
        Assert.Null(AuthoritativeBeltConsumableSelector.Select(inert, 3));
        Assert.Null(AuthoritativeBeltConsumableSelector.Select(targeted, 4));
        Assert.Null(AuthoritativeBeltConsumableSelector.Select(zeroIdentity, 5));
        Assert.Null(AuthoritativeBeltConsumableSelector.Select(missingState, 7));
    }

    [Fact]
    public void RejectsAnAmbiguousBeltSlotEvenWhenOneEntryIsUsable()
    {
        var player = Player(
            BeltItem(6, 706, ItemActionKind.RestorePartialLife),
            BeltItem(6, 706, ItemActionKind.RestoreFullMana));

        Assert.Null(AuthoritativeBeltConsumableSelector.Select(player, 6));
    }

    private static PlayerSnapshot Player(params BeltItemSnapshot[] belt)
        => PlayerWithLife(100, belt);

    private static PlayerSnapshot PlayerWithLife(int life, params BeltItemSnapshot[] belt)
    {
        var player = new PlayerSnapshot { Life = life };
        player.Belt.Add(belt);
        return player;
    }

    private static BeltItemSnapshot BeltItem(
        uint slot,
        uint stableItemId,
        ItemActionKind action,
        bool deleted = false)
    {
        return new BeltItemSnapshot {
            Slot = slot,
            ItemSeed = slot + 1,
            State = new ItemStateSnapshot {
                StableItemId = stableItemId,
                ItemActionKind = action,
                Deleted = deleted,
            },
        };
    }
}
