using System.Collections;
using System.Diagnostics;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class SnapshotReferenceTests(ITestOutputHelper output)
{
    [Fact]
    public void ProjectionCopiesPreserveEverySchemaFieldAndUnknownFields()
    {
        var source = new Snapshot();
        foreach (var field in Snapshot.Descriptor.Fields.InFieldNumberOrder()) {
            var value = field.FieldType switch {
                FieldType.Message => Activator.CreateInstance(field.MessageType.ClrType)!,
                FieldType.Enum => Enum.ToObject(field.EnumType.ClrType, 1),
                FieldType.String => "field",
                FieldType.Bool => (object)true,
                FieldType.UInt64 => 7UL,
                FieldType.UInt32 => 7U,
                _ => throw new InvalidOperationException($"Cover new snapshot field {field.Name}."),
            };
            if (field.IsRepeated) ((IList)field.Accessor.GetValue(source)).Add(value);
            else field.Accessor.SetValue(source, value);
        }
        source.MergeFrom(new byte[] { 0xF8, 0x07, 0x25 });
        var owned = source.CloneWithoutGeometry();
        owned.LevelGeometry.Add(source.LevelGeometry.Clone());
        Assert.Equal(source, owned);
        Assert.Equal(source, source.WithGeometryForTransport(source.LevelGeometry));
        owned.Players[0].Life = 123;
        Assert.NotEqual(owned.Players[0].Life, source.Players[0].Life);
    }

    [Fact]
    public void IdleReferencesPublishCompleteOwnedStateAndPreserveAdmissionCounters()
    {
        var source = TownSnapshot();
        var cache = new AuthoritativeSnapshotGeometryCache();
        var first = RoundTrip(cache.Project(source));
        Assert.False(first.GeometryUnchanged);
        Assert.Equal(1UL, first.GeometryRevision);
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: first));
        var before = model.Snapshot!;
        source.Tick = 2;
        source.StateSha256 = new('b', 64);
        var wire = RoundTrip(cache.Project(source));
        Assert.True(wire.StateUnchanged);
        Assert.Empty(wire.Players);
        Assert.Empty(wire.LevelGeometry);
        model.Apply(new(Snapshot: wire));
        Assert.Equal(source, model.Snapshot);
        Assert.Equal(1UL, before.Tick);
        Assert.Equal(2UL, model.AcceptedSnapshotRevision);
        Assert.Equal(1UL, model.AcceptedSnapshotContentRevision);
        var observed = model.CompleteSnapshotObservation(wire);
        Assert.Equal(source, observed);
        observed.LevelGeometry[0].Width = 2;
        Assert.Equal(4, model.Snapshot!.LevelGeometry[0].Width);
        Assert.Equal(0UL, source.GeometryRevision);
    }

    [Fact]
    public void ChangedBodyUsesCachedGeometryAndRetainsDynamicValidationAndOwnership()
    {
        var source = TownSnapshot();
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        var first = RoundTrip(cache.Project(source));
        model.Apply(new(Snapshot: first));
        first.LevelGeometry[0].Width = 99;
        source.Tick++;
        source.Players[0].Life = 63;
        var wire = RoundTrip(cache.Project(source));
        Assert.True(wire.GeometryUnchanged);
        Assert.False(wire.StateUnchanged);
        Assert.NotEmpty(wire.Players);
        Assert.Empty(wire.LevelGeometry);
        model.Apply(new(Snapshot: wire));
        Assert.Equal(source, model.Snapshot);
        Assert.Equal(2UL, model.AcceptedSnapshotContentRevision);
        wire.Players[0].Life = 65;
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: wire)));
        Assert.Equal(63, model.Snapshot!.Players[0].Life);
        Assert.Equal(4, model.Snapshot.LevelGeometry[0].Width);
    }

    [Theory]
    [InlineData("missing-baseline")] [InlineData("geometry-zero")] [InlineData("geometry-unknown")]
    [InlineData("identity")] [InlineData("inline-geometry")] [InlineData("state-zero")]
    [InlineData("state-unknown")] [InlineData("inline-body")] [InlineData("geometry-flag")]
    [InlineData("epoch")] [InlineData("invalid-hash")]
    public void ConflictingReferencesAreRejectedBeforeChangingTheModel(string corruption)
    {
        var source = TownSnapshot();
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var wire = RoundTrip(cache.Project(source));
        switch (corruption) {
        case "missing-baseline": model = new(); break;
        case "geometry-zero": wire.GeometryRevision = 0; break;
        case "geometry-unknown": wire.GeometryRevision++; break;
        case "identity": wire.ContentManifestHash = new('e', 64); break;
        case "inline-geometry": wire.LevelGeometry.Add(new LevelGeometrySnapshot()); break;
        case "state-zero": wire.StateRevision = 0; break;
        case "state-unknown": wire.StateRevision++; break;
        case "inline-body": wire.Players.Add(new PlayerSnapshot { EntityId = 1 }); break;
        case "geometry-flag": wire.GeometryUnchanged = false; break;
        case "epoch": model.ResetTransientPresentation(); break;
        case "invalid-hash": wire.StateSha256 = "bad"; break;
        }
        var revision = model.AcceptedSnapshotRevision;
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: wire)));
        Assert.Equal(revision, model.AcceptedSnapshotRevision);
    }

    [Fact]
    public void ReferencesToActiveActionsAreRejectedAndMovingBodiesAreSentInFull()
    {
        var source = TownSnapshot();
        source.Players[0].NativeWalkPathPending = true;
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var next = RoundTrip(cache.Project(source));
        Assert.True(next.GeometryUnchanged);
        Assert.False(next.StateUnchanged);
        next.StateUnchanged = true;
        next.Players.Clear();
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: next)));
    }

    [Theory]
    [InlineData("piece")] [InlineData("door")] [InlineData("visibility")] [InlineData("identity")]
    public void GeometryChangesPublishAndValidateANewCompleteBaseline(string change)
    {
        var source = TownSnapshot();
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        source.Tick++;
        switch (change) {
        case "piece": source.LevelGeometry[0].NativePieceLayer[0] = 1; break;
        case "door": source.LevelGeometry[0].BlockedCells.Add(1); source.LevelGeometry[0].NativeVisionBlockedCells.Add(1); break;
        case "visibility": source.LevelGeometry[0].NativeVisionBlockedCells.Add(1); break;
        case "identity": source.RulesetIdentityHash = new('e', 64); break;
        }
        var next = RoundTrip(cache.Project(source));
        Assert.False(next.GeometryUnchanged);
        Assert.False(next.StateUnchanged);
        Assert.Equal(2UL, next.GeometryRevision);
        model.Apply(new(Snapshot: next));
        Assert.Equal(source, model.Snapshot);
        // A malformed full baseline must never be cached as trusted geometry.
        next.GeometryRevision++;
        next.LevelGeometry[0].BlockedCells.Add(0);
        next.LevelGeometry[0].BlockedCells.Add(0);
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: next)));
    }

    [Fact]
    public void LegacySnapshotsAndFreshEpochsReplaceReferenceState()
    {
        var source = TownSnapshot();
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var oldReference = RoundTrip(cache.Project(source));
        model.Apply(new(Snapshot: source));
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: oldReference)));
        model.ResetTransientPresentation();
        var fresh = new AuthoritativeSnapshotGeometryCache();
        source.Tick = 0;
        model.Apply(new(Snapshot: RoundTrip(fresh.Project(source))));
        model.Apply(new(Snapshot: RoundTrip(fresh.Project(source))));
        Assert.Equal(0UL, model.CurrentTick);
        Assert.Equal(source, model.Snapshot);
    }

    [Fact]
    public void StaleReferencesAreValidatedButCannotRewindTheAdmittedClock()
    {
        var source = TownSnapshot();
        source.Tick = 10;
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var stale = RoundTrip(cache.Project(source));
        stale.Tick = 9;
        model.Apply(new(Snapshot: stale));
        Assert.Equal(10UL, model.CurrentTick);
        Assert.Equal(1UL, model.AcceptedSnapshotRevision);
        stale.RandomState = 99;
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: stale)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DiscardedSnapshotsCannotInstallGeometryOrBodyReferences(bool geometryChanged)
    {
        var source = TownSnapshot();
        source.Tick = 10;
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var admitted = model.Snapshot!;
        var knownReference = RoundTrip(cache.Project(source));
        knownReference.Tick = 12;

        source.Tick = 9;
        source.Players[0].Life = 63;
        if (geometryChanged) source.LevelGeometry[0].NativePieceLayer[0] = 1;
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        Assert.Same(admitted, model.Snapshot);
        Assert.Equal(1UL, model.AcceptedSnapshotRevision);
        Assert.Equal(1UL, model.AcceptedSnapshotContentRevision);

        source.Tick = 11;
        var unadmittedReference = RoundTrip(cache.Project(source));
        Assert.True(unadmittedReference.StateUnchanged);
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: unadmittedReference)));
        Assert.Same(admitted, model.Snapshot);
        model.Apply(new(Snapshot: knownReference));
        Assert.Equal(12UL, model.CurrentTick);
        Assert.Equal(64, model.Snapshot!.Players[0].Life);
        Assert.Equal(0U, model.Snapshot.LevelGeometry[0].NativePieceLayer[0]);
        Assert.Equal(2UL, model.AcceptedSnapshotRevision);
        Assert.Equal(1UL, model.AcceptedSnapshotContentRevision);
    }

    [Fact]
    public void DiscardedLegacySnapshotsCannotClearAnAdmittedReferenceBaseline()
    {
        var source = TownSnapshot();
        source.Tick = 10;
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var reference = RoundTrip(cache.Project(source));
        source.Tick = 9;
        source.Players[0].Life = 63;
        model.Apply(new(Snapshot: source));
        reference.Tick = 11;
        model.Apply(new(Snapshot: reference));
        Assert.Equal(11UL, model.CurrentTick);
        Assert.Equal(64, model.Snapshot!.Players[0].Life);
        Assert.Equal(1UL, model.AcceptedSnapshotContentRevision);
    }

    [Fact]
    public void NativeSizedIdleDecodeAndAdmissionStayBelowEightKiBPerPoll()
    {
        var source = TownSnapshot(large: true);
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var payload = new Envelope { Snapshot = cache.Project(source) }.ToByteArray();
        void Poll() => model.Apply(AuthoritativeClientMessage.FromEnvelope(Envelope.Parser.ParseFrom(payload)));
        for (var i = 0; i < 32; i++) Poll();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 512; i++) Poll();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"idle reference: wire={payload.Length}, allocation={bytes / 512.0:F0} B/poll, elapsed={watch.Elapsed.TotalMilliseconds / 512:F4} ms/poll");
        Assert.InRange(bytes, 0, 512L * 8192);
        Assert.Equal(source, model.Snapshot);
    }

    [Fact]
    public void NativeSizedMovingAdmissionDoesNotCloneOrRevalidateGeometryCells()
    {
        var source = TownSnapshot(large: true);
        source.Players[0].NativeWalkPathPending = true;
        var cache = new AuthoritativeSnapshotGeometryCache();
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: RoundTrip(cache.Project(source))));
        var payload = new Envelope { Snapshot = cache.Project(source) }.ToByteArray();
        void Poll() => model.Apply(AuthoritativeClientMessage.FromEnvelope(Envelope.Parser.ParseFrom(payload)));
        for (var i = 0; i < 16; i++) Poll();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++) Poll();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"moving reference: wire={payload.Length}, allocation={bytes / 128.0:F0} B/poll");
        Assert.InRange(bytes, 0, 128L * 32768);
    }

    [Fact]
    public void IdlePresentationAndHeldWalkCadenceDoNotAllocatePerFrame()
    {
        var source = TownSnapshot();
        var clock = new AuthoritativePresentationClock();
        var cadence = new HeldWalkCadence();
        void Frame() { clock.Advance(source, 1d / 60, 20); cadence.Advance(1d / 60); cadence.Due(new(1, 1)); }
        for (var i = 0; i < 32; i++) Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; i++) Frame();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"idle presentation/held cadence: {bytes} B / 4096 frames");
        Assert.InRange(bytes, 0, 256);
    }

    private static Snapshot RoundTrip(Snapshot snapshot)
        => Envelope.Parser.ParseFrom(new Envelope { Snapshot = snapshot }.ToByteArray()).Snapshot;

    private static Snapshot TownSnapshot(bool large = false)
    {
        var snapshot = new Snapshot { Tick = 1, StateSha256 = new('a', 64),
            ContentManifestHash = new('c', 64), RulesetIdentityHash = new('d', 64),
            Players = { new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64, PositionX = 1, PositionY = 1 } } };
        var width = large ? 112 : 4;
        for (uint id = 0; id < (large ? 9 : 1); id++) {
            var geometry = new LevelGeometrySnapshot { LevelId = id, Width = width, Height = width };
            for (var i = 0; i < width * width; i++) {
                geometry.NativePieceLayer.Add(0);
                if (i % 2 == 0) { geometry.BlockedCells.Add(i); geometry.NativeVisionBlockedCells.Add(i); geometry.MissileBlockedCells.Add(i); }
            }
            snapshot.LevelGeometry.Add(geometry);
        }
        return snapshot;
    }
}
