using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;
using ProtocolObjectPresentationKind = Devilution.Protocol.V1.ObjectPresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed class ObjectPresentationStyleTests
{
    [Theory]
    [InlineData(ProtocolObjectPresentationKind.Unspecified, false, false, false, 0, ObjectPresentationKind.Generic)]
    [InlineData(ProtocolObjectPresentationKind.Generic, false, false, false, 0, ObjectPresentationKind.Generic)]
    [InlineData(ProtocolObjectPresentationKind.Shrine, false, false, false, 0, ObjectPresentationKind.Shrine)]
    [InlineData(ProtocolObjectPresentationKind.Quest, false, false, false, 0, ObjectPresentationKind.Quest)]
    [InlineData(ProtocolObjectPresentationKind.Breakable, false, false, false, 0, ObjectPresentationKind.Breakable)]
    [InlineData(ProtocolObjectPresentationKind.Trap, false, false, false, 0, ObjectPresentationKind.Trap)]
    [InlineData(ProtocolObjectPresentationKind.NativeArmorStand, false, false, false, 0, ObjectPresentationKind.NativeArmorStand)]
    [InlineData(ProtocolObjectPresentationKind.NativeWeaponRack, false, false, false, 0, ObjectPresentationKind.NativeWeaponRack)]
    [InlineData(ProtocolObjectPresentationKind.Unspecified, true, false, false, 0, ObjectPresentationKind.Shrine)]
    [InlineData(ProtocolObjectPresentationKind.Unspecified, false, true, false, 0, ObjectPresentationKind.Trap)]
    [InlineData(ProtocolObjectPresentationKind.Unspecified, false, false, true, 0, ObjectPresentationKind.Breakable)]
    [InlineData(ProtocolObjectPresentationKind.Unspecified, false, false, false, 12, ObjectPresentationKind.Quest)]
    public void ResolvesServerOwnedObjectPresentationFamilies(
        ProtocolObjectPresentationKind wireKind,
        bool shrine,
        bool trap,
        bool breakable,
        uint questId,
        ObjectPresentationKind expected)
    {
        var state = new ObjectSnapshot {
            PresentationKind = wireKind,
            IsShrine = shrine,
            TrapArmed = trap,
            Breakable = breakable,
            QuestId = questId,
        };

        Assert.Equal(expected, ObjectPresentationStyle.Resolve(state));
    }

    [Theory]
    [InlineData(89u, ObjectPresentationKind.NativeArmorStand)]
    [InlineData(90u, ObjectPresentationKind.NativeWeaponRack)]
    public void ResolvesAuthoredNativeObjectPresentationIdentities(
        uint nativeObjectId,
        ObjectPresentationKind expected)
    {
        var state = new ObjectSnapshot {
            NativeObjectId = nativeObjectId,
        };

        Assert.Equal(expected, ObjectPresentationStyle.Resolve(state));
    }

    [Fact]
    public void UnknownNativeObjectIdentityRetainsGenericFallback()
    {
        var state = new ObjectSnapshot {
            NativeObjectId = 109,
        };

        Assert.Equal(ObjectPresentationKind.Generic, ObjectPresentationStyle.Resolve(state));
    }

    [Theory]
    [InlineData(false, true, 1u, 1u, ObjectPresentationFrame.Default)]
    [InlineData(true, false, 1u, 1u, ObjectPresentationFrame.Default)]
    [InlineData(true, true, 0u, 0u, ObjectPresentationFrame.Default)]
    [InlineData(true, true, 0u, 1u, ObjectPresentationFrame.Default)]
    [InlineData(true, true, 1u, 1u, ObjectPresentationFrame.Activated)]
    public void ResolvesAuthoredObjectLifecycleFrames(
        bool activated,
        bool keepVisible,
        uint animationFrame,
        uint activatedAnimationFrame,
        ObjectPresentationFrame expected)
    {
        var state = new ObjectSnapshot {
            Activated = activated,
            KeepVisibleAfterActivation = keepVisible,
            AnimationFrame = animationFrame,
            ActivatedAnimationFrame = activatedAnimationFrame,
        };

        Assert.Equal(expected, ObjectPresentationStyle.ResolveFrame(state));
    }
}
