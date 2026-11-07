using Devilution.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativePresentationEpochGateTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReturningToMenuResetsTheGateEvenWhenTheNextSessionReusesRevisionOne(bool connectedAtMenu)
    {
        var gate = new AuthoritativePresentationEpochGate();
        Assert.False(gate.Observe(hasSnapshot: true, connected: true, admittedRevision: 1));
        gate.WaitForAdmission(1);
        Assert.True(gate.IsPending);
        Assert.False(gate.Observe(hasSnapshot: true, connected: false, admittedRevision: 1));
        Assert.False(gate.Observe(hasSnapshot: false, connected: connectedAtMenu, admittedRevision: null));
        Assert.False(gate.IsPending);
        Assert.False(gate.Observe(hasSnapshot: true, connected: true, admittedRevision: 1));
        Assert.False(gate.IsPending);

        var clock = new AuthoritativePresentationClock();
        var idleTown = new Snapshot { Tick = 10 };
        clock.Advance(idleTown, 0, 20);
        Assert.True(clock.Advance(idleTown, gate.IsPending ? 0 : 0.025, 20) > 10);
    }

    [Fact]
    public void AdmissionAdvancesTheGateWhenIdleReferenceReuseKeepsContentRevisionUnchanged()
    {
        var source = new Snapshot { Tick = 10, StateSha256 = new('a', 64),
            Players = { new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64 } } };
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: cache.Project(source)));
        var gate = new AuthoritativePresentationEpochGate();
        gate.Observe(true, true, model.AcceptedSnapshotRevision);
        gate.WaitForAdmission(model.AcceptedSnapshotRevision);
        var contentRevision = model.AcceptedSnapshotContentRevision;
        source.Tick++;
        model.Apply(new(Snapshot: cache.Project(source)));
        Assert.Equal(contentRevision, model.AcceptedSnapshotContentRevision);
        Assert.True(gate.Observe(true, true, model.AcceptedSnapshotRevision));
        Assert.False(gate.IsPending);
        Assert.False(gate.Observe(true, true, model.AcceptedSnapshotRevision));
    }

    [Fact]
    public void AConnectionWaitsForAdmissionBeyondTheLatestModelRevisionRatherThanTheLastRenderedFrame()
    {
        var gate = new AuthoritativePresentationEpochGate();
        gate.Observe(true, true, 1);
        // A second snapshot was admitted after the previous render, before connection loss.
        gate.WaitForAdmission(2);
        Assert.False(gate.Observe(true, true, 2));
        Assert.True(gate.IsPending);
        Assert.False(gate.Observe(true, false, 3));
        Assert.True(gate.IsPending);
        Assert.True(gate.Observe(true, true, 3));
        Assert.False(gate.IsPending);
    }
}
