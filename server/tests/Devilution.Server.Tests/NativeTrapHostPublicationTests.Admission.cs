using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeTrapHostPublicationTests
{
    [Theory]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 7)]
    [InlineData(2, 0)] [InlineData(2, 1)] [InlineData(2, 7)]
    [InlineData(42, 0)] [InlineData(42, 1)] [InlineData(42, 7)]
    [InlineData(43, 0)] [InlineData(43, 1)] [InlineData(43, 7)]
    public void DoorLinkedMissileCheckpointRehydratesItsDoorAndRestores(int trigger, int type)
    {
        var depth = trigger <= 2 ? 4 : 8;
        var missile = new BoundaryMissile(new(type, false, 48, 50, 56, 50, true, 2588, trigger), depth, 0);
        var fixture = new NativeTrapRestoreFixture(depth, missile.Objects, missile.Owners, type);
        var cohort = NativeMissileCohortFixture.Create(depth) with {
            Objects = missile.Objects, NativeTraps = missile.Owners,
            Projectiles = missile.World.Projectiles.Values.Select(value =>
                JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(value))!).ToArray(),
        };
        var codec = new DiabloCohortCodec();
        fixture.Restore(codec.Deserialize(codec.Serialize(fixture.PrepareDoorCheckpoint(cohort))));
    }

    [Theory]
    [InlineData(0, false, 1)] [InlineData(0, false, 2)] [InlineData(0, false, 3)] [InlineData(0, false, 4)]
    [InlineData(0, false, 5)] [InlineData(0, false, 6)] [InlineData(0, false, 7)] [InlineData(0, false, 8)]
    [InlineData(1, false, 3)] [InlineData(1, false, 8)] [InlineData(7, false, 6)] [InlineData(7, false, 8)]
    [InlineData(0, true, 5)] [InlineData(0, true, 8)] [InlineData(27, true, 5)] [InlineData(27, true, 8)]
    public void CapturedObjectMissileRehydratesItsNativeOwnerAndActuallyRestores(int type, bool intrinsic, int depth)
    {
        var missile = new BoundaryMissile(new(type, intrinsic, 56, 56, 60, 60, false, 2588), depth, 0);
        var fixture = new NativeTrapRestoreFixture(depth, missile.Objects, missile.Owners, type);
        var cohort = NativeMissileCohortFixture.Create(depth) with {
            Objects = missile.Objects, NativeTraps = missile.Owners,
            Projectiles = missile.World.Projectiles.Values.Select(value =>
                JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(value))!).ToArray(),
        };
        var codec = new DiabloCohortCodec();
        fixture.Restore(codec.Deserialize(codec.Serialize(cohort)));
    }

    [Theory]
    [InlineData(5, false)] [InlineData(6, false)] [InlineData(7, false)]
    [InlineData(68, true)] [InlineData(69, true)] [InlineData(70, true)]
    public void IntrinsicFireArrowRequiresNativeTrappedChestAtEveryAdmissionLayer(int nativeType, bool legal)
        => AssertIntrinsicFireArrowOwnerAdmission(nativeType, legal);

    private static void AssertIntrinsicFireArrowOwnerAdmission(int nativeType, bool legal)
    {
        var missile = new BoundaryMissile(new(27, true, 56, 56, 60, 60, false, 2588), 7, nativeType % 3);
        var projectiles = missile.World.Projectiles.Values.Select(value =>
            JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(value))!).ToArray();
        var legalCohort = NativeMissileCohortFixture.Create(7) with {
            Objects = missile.Objects, Projectiles = projectiles,
        };
        var objects = legalCohort.Objects.Select(value => value with { NativeObjectId = (uint)nativeType }).ToArray();
        var cohort = legalCohort with { Objects = objects };
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(new DiabloSimulationSnapshot(0,
            cohort.Participants[0].Player, [], [], objects, [], projectiles, null));
        var codec = new DiabloCohortCodec();
        // Also exercise the reader with actual wire bytes, without asking the writer to accept a forgery.
        var json = JsonNode.Parse(codec.Serialize(legalCohort))!;
        json["Objects"]![0]!["NativeObjectId"] = nativeType;
        var bytes = json.ToJsonString();
        if (legal) {
            AuthoritativeSnapshotValidator.Validate(snapshot);
            new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
            codec.Serialize(cohort);
            codec.Deserialize(bytes);
        } else {
            Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(snapshot));
            Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel()
                .Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
            Assert.Throws<InvalidDataException>(() => codec.Serialize(cohort));
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(bytes));
        }
    }
}
