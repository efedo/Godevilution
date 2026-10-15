using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class DiabloCohortCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeBarrelV7RetainsRawOwnerMutationAndHitCredit(bool broken)
    {
        var source = NativeBarrelState(broken, playerHitRecorded: true);
        var encoded = codec.Serialize(source);
        var restored = codec.Deserialize(encoded);

        Assert.Equal(encoded, codec.Serialize(restored));
        Assert.Equal(source.NativeBarrels, restored.NativeBarrels);
        Assert.Equal(source.NativeSarcophagi, restored.NativeSarcophagi);
        Assert.Equal(source.Objects, restored.Objects);
        Assert.True(Assert.Single(restored.CombatTargets).NativePlayerHitRecorded);
        var barrel = Assert.Single(restored.NativeBarrels!);
        Assert.Equal(broken, barrel.Broken);
        Assert.Equal(broken, restored.Objects.Single(value => value.NativeObjectId == 57).Activated);
    }

    [Theory]
    [InlineData("missing-owner")]
    [InlineData("wrong-native-id")]
    [InlineData("object-frame")]
    [InlineData("object-activation")]
    [InlineData("object-missile")]
    [InlineData("bad-animation")]
    public void NativeBarrelV7RejectsIncompleteOrConflictingOwnerState(string defect)
    {
        var document = JsonNode.Parse(codec.Serialize(NativeBarrelState(broken: true)))!.AsObject();
        switch (defect) {
        case "missing-owner":
            document.Remove("NativeBarrels");
            break;
        case "wrong-native-id":
            document["Objects"]![1]!["NativeObjectId"] = 58;
            break;
        case "object-frame":
            document["Objects"]![1]!["AnimationFrame"] = 2;
            break;
        case "object-activation":
            document["Objects"]![1]!["Activated"] = false;
            break;
        case "object-missile":
            document["Objects"]![1]!["MissilePassable"] = false;
            break;
        case "bad-animation":
            document["NativeBarrels"]![0]!["AnimationDelay"] = 2;
            break;
        }

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void NativeBarrelExtensionAndPlayerHitCreditCannotBeDownConvertedButV6BytesStayStable()
    {
        var v6 = NativeSarcophagusState(opened: false);
        var v6Bytes = codec.Serialize(v6);
        Assert.DoesNotContain("NativeBarrels", v6Bytes);
        Assert.DoesNotContain("NativePlayerHitRecorded", v6Bytes);
        Assert.Equal(v6Bytes, codec.Serialize(codec.Deserialize(v6Bytes)));

        var withBarrel = NativeBarrelState(broken: false);
        Assert.Throws<InvalidDataException>(() => codec.Serialize(withBarrel with {
            FormatVersion = DiabloCohortState.NativeSarcophagusFormatVersion,
        }));

        var withHitCredit = NativeBarrelState(broken: false, playerHitRecorded: true) with {
            FormatVersion = DiabloCohortState.NativeSarcophagusFormatVersion,
            NativeBarrels = null,
        };
        Assert.Throws<InvalidDataException>(() => codec.Serialize(withHitCredit));
    }

    private static DiabloCohortState NativeBarrelState(bool broken, bool playerHitRecorded = false)
    {
        var source = NativeSarcophagusState(opened: false);
        var barrelObject = new DiabloObjectState(
            0x09002031, 0x09000032, 1, 20, 20, broken, 0,
            AuthoritativeObjectEffectKind.None, 0) {
            NativeObjectId = 57,
            AnimationFrame = 1,
            MissilePassable = true,
        };
        var barrel = new DiabloNativeBarrelState(
            ObjectSlot: 4,
            EntityId: barrelObject.EntityId,
            NativeTypeId: 57,
            InitialPositionX: 20,
            InitialPositionY: 20,
            RandomSeed: 0x87654321,
            Var1: 0,
            Var2: 0,
            Var3: 0,
            Var4: -1,
            InitialSelectionRegion: 1,
            InitialAnimationFrame: 1,
            InitialAnimationFlag: 0,
            InitialAnimationDelay: 1000,
            InitialAnimationCount: 0,
            InitialAnimationLength: 8,
            InitialSolidFlag: true,
            InitialMissilePassable: true,
            InitialBreakFlag: 0,
            InitialPreFlag: false,
            TrapArmed: false,
            HasLinkedTrap: false,
            Broken: broken,
            AnimationFlag: broken ? 1U : 0U,
            AnimationFrame: 1,
            AnimationDelay: broken ? 1 : 1000,
            AnimationCount: 0,
            SolidFlag: !broken,
            MissilePassable: true,
            BreakFlag: broken ? -1 : 0,
            SelectionRegion: broken ? 0 : 1,
            PreFlag: broken,
            LinkedMonsterSlot: -1,
            LinkedMonsterEntityId: 0,
            LinkedMonsterTypeId: -1,
            ActorState: DiabloNativeBarrelActorState.None,
            SpawnPositionX: 0,
            SpawnPositionY: 0,
            SpawnDirection: 0,
            SpecialFrameIndex: 0,
            SpecialTickCounter: 0);
        var target = Assert.Single(source.CombatTargets);
        return source with {
            FormatVersion = DiabloCohortState.NativeBarrelFormatVersion,
            Objects = source.Objects.Append(barrelObject).OrderBy(value => value.EntityId).ToArray(),
            NativeBarrels = [barrel],
            CombatTargets = [target with { NativePlayerHitRecorded = playerHitRecorded }],
        };
    }
}
