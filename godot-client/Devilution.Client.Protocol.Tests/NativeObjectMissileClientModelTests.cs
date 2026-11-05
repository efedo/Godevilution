using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectMissileClientModelTests
{
    [Theory]
    [InlineData(AuthoritativeProjectileBehavior.Firebolt, "Firebolt", 0, false)]
    [InlineData(AuthoritativeProjectileBehavior.Firebolt, "MagmaBallExplosion", 1, false)]
    [InlineData(AuthoritativeProjectileBehavior.Lightning, "LightningControl", 0, false)]
    [InlineData(AuthoritativeProjectileBehavior.LightningSegment, "Lightning", 0, false)]
    [InlineData(AuthoritativeProjectileBehavior.WeaponArrow, "FireArrow", 0, true)]
    [InlineData(AuthoritativeProjectileBehavior.WeaponArrow, "FireArrow", 1, true)]
    public void NativeTrapFlightAndImpactSnapshotsContinueAdvancingTheClientClock(
        AuthoritativeProjectileBehavior behavior, string nativeId, int phase, bool intrinsic)
    {
        var snapshot = Trap(behavior, nativeId, phase, intrinsic);
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        var later = snapshot.Clone(); later.Tick++;
        model.Apply(new AuthoritativeClientMessage(Snapshot: later));
        Assert.Equal(snapshot.Tick + 1, model.Snapshot!.Tick);
        Assert.Equal(nativeId, Assert.Single(model.Snapshot.Projectiles).NativeMissileId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void InvalidSpelllessMissilesStillFailAdmission(int corruption)
    {
        var snapshot = Trap(AuthoritativeProjectileBehavior.Firebolt, "Firebolt", 0, false);
        switch (corruption) {
        case 0: snapshot.Objects[1].NativeObjectId = 57; break;
        case 1: snapshot.Objects[0].NativeObjectId = 54; break;
        case 2: snapshot.Projectiles[0].TargetEntityId = 999; break;
        case 3: snapshot.Objects[1].LevelId = 6; break;
        case 4: snapshot.Projectiles[0].NativeMissileId = "UnownedMissile"; break;
        case 5: snapshot.Projectiles[0].Phase = 1; break;
        case 6: snapshot.Projectiles[0].RemainingTicks = 257; break;
        case 7: snapshot.Projectiles[0].SourceEntityId = 7; break;
        case 8: snapshot.Projectiles[0].Damage = -1; break;
        }
        Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel()
            .Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Theory]
    [InlineData(AuthoritativeProjectileBehavior.Firebolt, "Firebolt", 0)]
    [InlineData(AuthoritativeProjectileBehavior.Firebolt, "MagmaBallExplosion", 1)]
    [InlineData(AuthoritativeProjectileBehavior.Lightning, "LightningControl", 0)]
    [InlineData(AuthoritativeProjectileBehavior.LightningSegment, "Lightning", 0)]
    public void FiredWallMissileStaysValidAfterTheDoorIsClosedAgain(
        AuthoritativeProjectileBehavior behavior, string nativeId, int phase)
    {
        var snapshot = Trap(behavior, nativeId, phase, false);
        snapshot.Objects[0].MissilePassable = false;
        new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        snapshot.Projectiles[0].StartX++;
        Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel()
            .Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Theory]
    [InlineData(5, false)] [InlineData(6, false)] [InlineData(7, false)]
    [InlineData(68, true)] [InlineData(69, true)] [InlineData(70, true)]
    public void OnlyNativeTrappedChestsCanOwnAnIntrinsicFireArrow(uint nativeType, bool legal)
    {
        var snapshot = Trap(AuthoritativeProjectileBehavior.WeaponArrow, "FireArrow", 0, true);
        snapshot.Objects[0].NativeObjectId = nativeType;
        var model = new AuthoritativeClientModel();
        if (legal)
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        else
            Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void IntrinsicFireArrowNeedsItsOwnOpenedChestAndLegacyWeaponArrowsStillWork()
    {
        var snapshot = Trap(AuthoritativeProjectileBehavior.WeaponArrow, "FireArrow", 0, true);
        snapshot.Objects[0].Activated = false;
        Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel()
            .Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
        snapshot.Projectiles[0].NativeMissileId = "";
        snapshot.Projectiles[0].SourceEntityId = 7;
        snapshot.Projectiles[0].TargetEntityId = 0;
        new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
    }

    private static Snapshot Trap(AuthoritativeProjectileBehavior behavior, string nativeId, int phase, bool intrinsic)
        => new() {
            Tick = 300,
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 7, VisitedLevelIds = { 0, 7 } } },
            Objects = {
                new ObjectSnapshot { EntityId = 20, ObjectId = 42, LevelId = 7, NativeObjectId = intrinsic ? 68U : 42U,
                    Activated = intrinsic, MissilePassable = true, KeepVisibleAfterActivation = true },
                new ObjectSnapshot { EntityId = 21, ObjectId = 53, LevelId = 7, NativeObjectId = 53 },
            },
            Projectiles = { new ProjectileSnapshot {
                EntityId = 30, LevelId = 7, SourceEntityId = intrinsic ? 20U : 21U, TargetEntityId = 20,
                NativeMissileId = nativeId, Behavior = (int)behavior, Phase = phase, RemainingTicks = 8,
            } },
        };
}
