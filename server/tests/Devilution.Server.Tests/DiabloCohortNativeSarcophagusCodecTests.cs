using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class DiabloCohortCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeSarcophagusV6RetainsClosedAndOpenedObjectLifecycle(bool opened)
    {
        var source = NativeSarcophagusState(opened);
        var encoded = codec.Serialize(source);
        var restored = codec.Deserialize(encoded);

        Assert.Equal(encoded, codec.Serialize(restored));
        Assert.Equal(source.NativeSarcophagi, restored.NativeSarcophagi);
        Assert.Equal(source.Objects, restored.Objects);
        Assert.Equal(opened, Assert.Single(restored.NativeSarcophagi!).Opened);
        Assert.Equal(opened, Assert.Single(restored.Objects).Activated);
    }

    [Fact]
    public void NativeSarcophagusV6PreservesRawInitialAnimationCountAfterOpen()
    {
        var source = NativeSarcophagusState(opened: true, initialAnimationCount: 7);
        var encoded = codec.Serialize(source);
        var restored = codec.Deserialize(encoded);

        var state = Assert.Single(restored.NativeSarcophagi!);
        Assert.Equal(7, state.InitialAnimationCount);
        Assert.Equal(7, state.AnimationCount);
        Assert.Equal(3, state.AnimationDelay);
    }

    [Fact]
    public void NativeSarcophagusV6PreservesSetupObjectThousandTickDelay()
    {
        var source = NativeSarcophagusState(opened: false, initialAnimationDelay: 1000);
        var encoded = codec.Serialize(source);
        var restored = codec.Deserialize(encoded);

        var state = Assert.Single(restored.NativeSarcophagi!);
        Assert.Equal(1000, state.InitialAnimationDelay);
        Assert.Equal(1000, state.AnimationDelay);
        Assert.Equal(state.InitialAnimationCount, state.AnimationCount);
    }

    [Theory]
    [InlineData("missing-owner")]
    [InlineData("duplicate-slot")]
    [InlineData("duplicate-entity")]
    [InlineData("object-frame")]
    [InlineData("object-activation")]
    [InlineData("wrong-native-id")]
    public void NativeSarcophagusV6RejectsIncompleteOrConflictingOwnerState(string mutation)
    {
        var document = JsonNode.Parse(codec.Serialize(NativeSarcophagusState(opened: false)))!.AsObject();
        var rows = document["NativeSarcophagi"]!.AsArray();
        switch (mutation) {
        case "missing-owner":
            document.Remove("NativeSarcophagi");
            break;
        case "duplicate-slot":
            rows.Add(rows[0]!.DeepClone());
            rows[1]!["EntityId"] = 92U;
            break;
        case "duplicate-entity":
            rows.Add(rows[0]!.DeepClone());
            rows[1]!["ObjectSlot"] = 4;
            break;
        case "object-frame":
            document["Objects"]![0]!["AnimationFrame"] = 1;
            break;
        case "object-activation":
            document["Objects"]![0]!["Activated"] = true;
            break;
        case "wrong-native-id":
            document["Objects"]![0]!["NativeObjectId"] = 47;
            break;
        }

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void OlderNativeCombatFormatsRejectTheSarcophagusExtension()
    {
        var v5 = NativeCombatState();
        var serializedV5 = codec.Serialize(v5);
        Assert.DoesNotContain("NativeSarcophagi", serializedV5);

        var v6 = codec.Serialize(NativeSarcophagusState(opened: false));
        var downgradedWithExtension = JsonNode.Parse(v6)!.AsObject();
        downgradedWithExtension["FormatVersion"] = DiabloCohortState.NativeCombatFormatVersion;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(downgradedWithExtension.ToJsonString()));

        var legacyV5 = JsonNode.Parse(v6)!.AsObject();
        legacyV5["FormatVersion"] = DiabloCohortState.NativeCombatFormatVersion;
        legacyV5.Remove("NativeSarcophagi");
        var restoredV5 = codec.Deserialize(legacyV5.ToJsonString());
        var reserializedLegacyV5 = codec.Serialize(restoredV5);
        Assert.DoesNotContain("NativeSarcophagi", reserializedLegacyV5);
        Assert.Equal(reserializedLegacyV5, codec.Serialize(codec.Deserialize(reserializedLegacyV5)));

        Assert.Throws<InvalidDataException>(() => codec.Serialize(
            NativeSarcophagusState(opened: false) with {
                NativeSarcophagi = null,
            }));
    }

    private static DiabloCohortState NativeSarcophagusState(bool opened, int initialAnimationCount = 0, int initialAnimationDelay = 10)
    {
        var native = NativeCombatState();
        var items = EmptyNativeItems();
        var objectState = new DiabloObjectState(
            0x09002030, 0x09000031, 1, 10, 10, opened, 0,
            AuthoritativeObjectEffectKind.None, 0) {
            NativeObjectId = 48,
            AnimationFrame = opened ? 2 : 0,
        };
        var sarcophagus = new DiabloNativeSarcophagusState(
            ObjectSlot: 3,
            EntityId: objectState.EntityId,
            NativeTypeId: 48,
            InitialPositionX: 10,
            InitialPositionY: 10,
            RandomSeed: 0x12345678,
            Var1: 0,
            Var2: 0,
            InitialSelectionRegion: 1,
            TrapArmed: false,
            HasLinkedTrap: false,
            InitialAnimationFrame: 0,
            InitialAnimationFlag: 0,
            InitialAnimationDelay: initialAnimationDelay,
            InitialAnimationCount: initialAnimationCount,
            InitialAnimationLength: 8,
            Opened: opened,
            AnimationFlag: opened ? 1U : 0U,
            AnimationDelay: opened ? 3 : initialAnimationDelay,
            AnimationCount: initialAnimationCount,
            AnimationFrame: checked((uint)objectState.AnimationFrame),
            LinkedMonsterSlot: -1,
            LinkedMonsterEntityId: 0,
            LinkedMonsterTypeId: -1,
            ActorState: DiabloNativeSarcophagusActorState.None,
            SpawnPositionX: 0,
            SpawnPositionY: 0,
            SpawnDirection: 0,
            SpecialFrameIndex: 0,
            SpecialTickCounter: 0);
        return native with {
            FormatVersion = DiabloCohortState.NativeSarcophagusFormatVersion,
            Configuration = native.Configuration with {
                CathedralItemMetadataFingerprint = items.MetadataFingerprint,
                CathedralItemGenerationFingerprint = items.GenerationFingerprint,
            },
            CathedralItems = items,
            Objects = [objectState],
            NativeSarcophagi = [sarcophagus],
        };
    }

    private static DiabloCathedralItemCheckpoint EmptyNativeItems()
    {
        var level = new DiabloCathedralItemLevelCheckpoint(
            1,
            1,
            Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
                .Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray(),
            Enumerable.Range(0, AuthoritativeCathedralItemRuntime.ActiveCapacity).ToArray(),
            ActiveItemCount: 0,
            SignedItemRefs: new int[AuthoritativeCathedralItemRuntime.CellCount],
            SlotEntityBindings: Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
                .Select(slot => new DiabloCathedralItemSlotEntityBinding(slot, 0)).ToArray());
        return new DiabloCathedralItemCheckpoint(
            Fingerprint('B'), Fingerprint('C'), [], [level]);
    }

    private static string Fingerprint(char value) => new(value, 64);
}