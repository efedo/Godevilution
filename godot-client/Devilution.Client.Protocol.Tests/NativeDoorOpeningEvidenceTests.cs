using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeDoorOpeningEvidenceTests
{
    [Fact]
    public void SparseObservationRequiresTheActualFiringTickOfThisOpening()
    {
        var before = Baseline();
        var opening = new NativeCatacombsDoorTrapObservation.Opened(new(before, 20), 110, 210);
        var after = Fired(111);
        after.Tick = 250; // The entire flight may have elapsed before publication.
        var shot = Assert.IsType<NativeCatacombsDoorTrapObservation.Firing>(opening.Find(after));
        Assert.Equal(111UL, shot.Tick);
        Assert.Equal((20U, 21U), (shot.DoorEntity, shot.TrapEntity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APreviouslyFiredOrRestoredLinkCannotBecomeFreshOpeningEvidence(bool restored)
    {
        var before = Fired(90); before.Tick = 100; before.Objects[0].MissilePassable = false;
        if (restored) before = Snapshot.Parser.ParseFrom(before.ToByteArray());
        var opening = new NativeCatacombsDoorTrapObservation.Opened(new(before, 20), 110, 210);
        Assert.Null(opening.Find(Fired(90)));
        // Even relabeling the same source with a later tick cannot bypass the pre-opening baseline.
        Assert.Null(opening.Find(Fired(111)));
    }

    [Fact]
    public void RestoredUntimedLegacyFiringCannotBeStampedWithTheObservationTick()
    {
        var restored = Fired(90); restored.Objects[1].ClearNativeTrapFiredTick();
        restored = Snapshot.Parser.ParseFrom(restored.ToByteArray());
        Assert.Empty(NativeCatacombsDoorTrapObservation.Find(restored));
        var opening = new NativeCatacombsDoorTrapObservation.Opened(new(Baseline(), 20), 110, 210);
        Assert.Null(opening.Find(restored));
    }

    [Fact]
    public void AReclosedDoorCannotReuseTheCurrentOpeningsProof()
    {
        var opening = new NativeCatacombsDoorTrapObservation.Opened(new(Baseline(), 20), 110, 210);
        var after = Fired(111);
        Assert.NotNull(opening.Find(after));
        after.Objects[0].MissilePassable = false;
        Assert.Empty(NativeCatacombsDoorTrapObservation.Find(after));
        Assert.Null(opening.Find(after));
        // Reopening it creates a new baseline that already contains the spent link.
        var reopened = new NativeCatacombsDoorTrapObservation.Opened(new(after, 20), 220, 320);
        after.Objects[0].MissilePassable = true; after.Tick = 230;
        Assert.Null(reopened.Find(after));
    }

    [Theory]
    [InlineData(109UL, false)]
    [InlineData(110UL, true)]
    [InlineData(209UL, true)]
    [InlineData(210UL, false)]
    [InlineData(211UL, false)]
    public void FiringMustFallInsideTheOpeningCommandAndItsDeadline(ulong firedTick, bool accepted)
    {
        var opening = new NativeCatacombsDoorTrapObservation.Opened(new(Baseline(), 20), 110, 210);
        Assert.Equal(accepted, opening.Find(Fired(firedTick)) is not null);
    }

    [Fact]
    public void AnAlreadyOpenBaselineOrPreexistingMissileCannotProveANewOpening()
    {
        var before = Baseline(); before.Objects[0].MissilePassable = true;
        Assert.Null(new NativeCatacombsDoorTrapObservation.Opened(new(before, 20), 110, 210).Find(Fired(111)));
        before.Objects[0].MissilePassable = false;
        before.Projectiles.Add(new ProjectileSnapshot { SpellId = 0, SourceEntityId = 21, TargetEntityId = 20 });
        Assert.Null(new NativeCatacombsDoorTrapObservation.Opened(new(before, 20), 110, 210).Find(Fired(111)));
    }

    private static Snapshot Baseline() => new() { Tick = 100, Objects = {
        new ObjectSnapshot { EntityId = 20, NativeObjectId = 42, LevelId = 7, PositionX = 50, PositionY = 50 },
        new ObjectSnapshot { EntityId = 21, NativeObjectId = 53, LevelId = 7, PositionX = 48, PositionY = 50 },
    } };

    private static Snapshot Fired(ulong tick)
    {
        var result = Baseline(); result.Tick = 300;
        result.Objects[0].MissilePassable = true;
        result.Objects[1].NativeTrapFiredTriggerEntityId = 20; result.Objects[1].NativeTrapFiredTick = tick;
        return result;
    }
}
