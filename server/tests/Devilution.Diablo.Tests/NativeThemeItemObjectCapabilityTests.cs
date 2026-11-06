using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeThemeItemObjectCapabilityTests
{
    [Theory]
    [InlineData(62)] [InlineData(63)]
    public void ZharBookcaseIsRefusedAtTheNativeMonsterSlotButUnrelatedSlotsDoNotDisableBooks(int type)
    {
        var raw = new AuthoritativeCathedralPopulationObjectState {Type = type, SelectionRegion = 3, AnimFrame = 3};
        var monsters = Enumerable.Range(0, 200).Select(slot => new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot, UniqueType = 255,
        }).ToArray();
        monsters[5] = monsters[5] with {UniqueType = 2};
        Assert.True(AuthoritativeNativePopulationBindings.IsSupportedNativeThemeItem(raw, monsters));
        monsters[4] = monsters[4] with {UniqueType = 2};
        Assert.False(AuthoritativeNativePopulationBindings.IsSupportedNativeThemeItem(raw, monsters));
        Assert.True(AuthoritativeNativePopulationBindings.IsSupportedNativeThemeItem(raw with {Type = 61, AnimFrame = 4}, monsters));
    }

    [Fact]
    public void QuestBooksEmptyDisplaysAndMalformedOrPreconsumedItemObjectsStayRefused()
    {
        var monsters = Enumerable.Range(0, 200).Select(slot => new AuthoritativeCathedralPopulationMonsterState {Slot = slot, UniqueType = 255}).ToArray();
        var raw = new AuthoritativeCathedralPopulationObjectState {Type = 61, SelectionRegion = 3, AnimFrame = 4};
        Assert.True(AuthoritativeNativePopulationBindings.IsSupportedNativeThemeItem(raw, monsters));
        foreach (var invalid in new[] {
            raw with {SelectionRegion = 0}, raw with {TrapFlag = true}, raw with {AnimFrame = 0},
            raw with {AnimFrame = 254}, raw with {Type = 62, AnimFrame = 2},
            raw with {Type = 25}, raw with {Type = 41}, raw with {Type = 71}, raw with {Type = 72},
            raw with {Type = 78}, raw with {Type = 86}, raw with {Type = 88}, raw with {Type = 93}, raw with {Type = 95},
        }) Assert.False(AuthoritativeNativePopulationBindings.IsSupportedNativeThemeItem(invalid, monsters));
    }
}
