using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeIdentifyItemProjectionTests
{
    [Fact]
    public void IdentifyScrollUsesItsBoundItemActionAndKeepsItsRawSpellOrdinal()
    {
        var profile = Profile();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var projection = Project(runtime, profile);
        Assert.Equal(5, projection.Raw.SpellId);
        Assert.Equal(0, projection.State.SpellId);
        Assert.Equal(DiabloItemActionKind.Identify, projection.State.ItemActionKind);
        Assert.Equal(DiabloItemSpellSourceKind.None, projection.State.SpellSourceKind);
        Assert.False(profile.NativeSpellBindings!.ContainsKey(5));
    }

    [Fact]
    public void LegacyIdentifyBindingWithoutSourceKindRemainsLoadable()
    {
        var profile = Profile(source: null);
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        Assert.True(runtime.TryAddWorldItem(1, Project(runtime, profile), out _));
        var fresh = new AuthoritativeCathedralItemRuntime(profile, runtime.CaptureCheckpoint());
        var restored = fresh.GetProjection(1, 77)!;
        Assert.Equal(5, restored.Raw.SpellId);
        Assert.Equal(0, restored.State.SpellId);
        Assert.Equal(DiabloItemActionKind.Identify, restored.State.ItemActionKind);
        Assert.Null(restored.State.SpellSourceKind);
    }

    [Fact]
    public void IdentifyScrollPoolRestoresWithoutAddingACastableSpellBinding()
    {
        var profile = Profile();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        Assert.True(runtime.TryAddWorldItem(1, Project(runtime, profile), out _));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(runtime.CaptureCheckpoint());
        var decoded = JsonSerializer.Deserialize<DiabloCathedralItemCheckpoint>(bytes)!;
        var fresh = new AuthoritativeCathedralItemRuntime(profile, decoded);
        var restored = fresh.GetProjection(1, 77)!;
        Assert.Equal(5, restored.Raw.SpellId);
        Assert.Equal(0, restored.State.SpellId);
        Assert.Throws<InvalidDataException>(() => fresh.TryReplaceWorldItem(1, 77,
            restored with { State = restored.State with { SpellId = 5 } }));
    }

    [Fact]
    public void DroppingAnOwnedIdentifyScrollRehydratesNativeIdentifyRatherThanNone()
    {
        var profile = Profile();
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var projection = Project(runtime, profile);
        var worldItem = new AuthoritativeWorldItem(77, 1, 1, 1,
            projection.ItemSeed, projection.Price, projection.State);
        Assert.True(runtime.Add(worldItem, runtime.ProjectWorldItem(worldItem), out _));
        var dropped = runtime.GetProjection(1, 77)!;
        Assert.Equal(5, dropped.Raw.SpellId);
        Assert.Equal(0, dropped.State.SpellId);
        Assert.Equal(DiabloItemActionKind.Identify, dropped.State.ItemActionKind);
    }

    [Theory]
    [InlineData(21, DiabloItemActionKind.None, DiabloItemSpellSourceKind.None)]
    [InlineData(24, DiabloItemActionKind.Identify, DiabloItemSpellSourceKind.None)]
    [InlineData(21, DiabloItemActionKind.Identify, DiabloItemSpellSourceKind.ConsumableItem)]
    public void OtherUnmappedSpellFiveItemsStillFail(int misc, DiabloItemActionKind action, DiabloItemSpellSourceKind source)
    {
        var profile = Profile(action, source);
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        Assert.Throws<InvalidDataException>(() => Project(runtime, profile, misc));
    }

    [Fact]
    public void CastableScrollsContinueToRequireAndUseTheirExplicitMapping()
    {
        var profile = Profile(DiabloItemActionKind.None, DiabloItemSpellSourceKind.ConsumableItem);
        var runtime = new AuthoritativeCathedralItemRuntime(profile);
        var raw = Raw() with { SpellId = 1 };
        var projection = runtime.ProjectNativeItem(0, 77, raw.Seed, 100, raw, profile.ItemBindings[0], profile.ContentInventoryDimensions);
        Assert.Equal(101, projection.State.SpellId);
        Assert.Equal(DiabloItemActionKind.None, projection.State.ItemActionKind);
    }

    private static AuthoritativeCathedralItemRuntimeProfile Profile(DiabloItemActionKind action = DiabloItemActionKind.Identify,
        DiabloItemSpellSourceKind? source = DiabloItemSpellSourceKind.None)
    {
        var profile = AuthoritativeCathedralItemRuntimeTestData.Profile();
        var bindings = profile.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[0] = bindings[0] with { StableSymbol = "identify_scroll", ItemActionKind = action, SpellSourceKind = source };
        return profile with { ItemBindings = bindings, NativeSpellBindings = new Dictionary<int, int> { [0] = 0, [1] = 101 } };
    }

    private static AuthoritativeCathedralInitialItemState Raw()
        => AuthoritativeCathedralItemRuntimeTestData.Item(0, 1, 1) with {
            ItemType = 0, MiscId = 21, SpellId = 5, BaseName = "Identify scroll", IdentifiedName = "Identify scroll",
        };

    private static AuthoritativeCathedralNativeItemProjection Project(AuthoritativeCathedralItemRuntime runtime,
        AuthoritativeCathedralItemRuntimeProfile profile, int misc = 21)
    {
        var raw = Raw() with { MiscId = misc };
        return runtime.ProjectNativeItem(0, 77, raw.Seed, 100, raw, profile.ItemBindings[0], profile.ContentInventoryDimensions);
    }
}
