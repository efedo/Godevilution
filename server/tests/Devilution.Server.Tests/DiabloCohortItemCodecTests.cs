using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class DiabloCohortCodecTests
{
    [Fact]
    public void ItemVersionFourRoundTripsAllSlotsWithoutChangingOlderSchemas()
    {
        var source = ItemState();
        var serialized = codec.Serialize(source);
        Assert.Equal(serialized, codec.Serialize(codec.Deserialize(serialized)));
        Assert.Contains("\"CathedralItems\":", serialized);
        Assert.Equal(128, codec.Deserialize(serialized).CathedralItems!.Levels[0].Items.Count);
        foreach (var old in new[] { CreateState(), NativeState(0, 1) }) {
            var bytes = codec.Serialize(old);
            Assert.DoesNotContain("CathedralItems", bytes);
            Assert.DoesNotContain("CathedralItemMetadataFingerprint", bytes);
            Assert.DoesNotContain("CathedralItemGenerationFingerprint", bytes);
            Assert.Equal(bytes, codec.Serialize(codec.Deserialize(bytes)));
        }
        foreach (var version in new[] { 1, 2, 3 })
            Assert.Throws<InvalidDataException>(() => codec.Serialize(source with { FormatVersion = version }));
    }

    [Theory]
    [InlineData("missing-items")]
    [InlineData("null-items")]
    [InlineData("identity")]
    [InlineData("generation")]
    [InlineData("missing-slot")]
    [InlineData("duplicate-active-id")]
    [InlineData("unsafe-depth")]
    [InlineData("inactive-binding")]
    [InlineData("ghost-reference")]
    [InlineData("bad-history")]
    [InlineData("unknown-field")]
    [InlineData("count-coercion")]
    [InlineData("old-format")]
    public void ItemVersionFourRejectsMalformedAndCrossSchemaPayloads(string defect)
    {
        var json = JsonNode.Parse(codec.Serialize(ItemState()))!.AsObject();
        var items = json["CathedralItems"]!.AsObject();
        var level = items["Levels"]![0]!.AsObject();
        switch (defect) {
        case "missing-items": json.Remove("CathedralItems"); break;
        case "null-items": json["CathedralItems"] = null; break;
        case "identity": items["MetadataFingerprint"] = "short"; break;
        case "generation": json["Configuration"]!["CathedralItemGenerationFingerprint"] = new string('C', 64); break;
        case "missing-slot": level["Items"]!.AsArray().RemoveAt(127); break;
        case "duplicate-active-id": level["ActiveItemIds"]![126] = 0; break;
        case "unsafe-depth": level["Depth"] = 5; break;
        case "inactive-binding": level["SlotEntityBindings"]![127]!["EntityId"] = 99; break;
        case "ghost-reference": level["SignedItemRefs"]![0] = 1; break;
        case "bad-history": items["UniqueItemFlags"] = new JsonArray(128); break;
        case "unknown-field": level["Unrecognized"] = 1; break;
        case "count-coercion": level["ActiveItemCount"] = "0"; break;
        case "old-format": json["FormatVersion"] = 3; break;
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(json.ToJsonString()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void OlderVersionsRejectItemFieldsEvenWhenNull(int version)
    {
        var json = JsonNode.Parse(codec.Serialize(version == 1 ? CreateState() : NativeState(0, 1)))!.AsObject();
        json["CathedralItems"] = null;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(json.ToJsonString()));
    }

    private static DiabloCohortState ItemState()
    {
        var original = NativeState(0, 1);
        var metadata = new string('A', 64);
        var generation = new string('B', 64);
        return original with {
            FormatVersion = DiabloCohortState.CathedralItemFormatVersion,
            Configuration = original.Configuration with {
                CathedralItemMetadataFingerprint = metadata, CathedralItemGenerationFingerprint = generation,
            },
            WorldItems = [],
            CathedralItems = new(metadata, generation, [], [new(1, 1,
                Enumerable.Range(0, 128).Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray(),
                Enumerable.Range(0, 127).ToArray(), 0, new int[112 * 112],
                Enumerable.Range(0, 128).Select(slot => new DiabloCathedralItemSlotEntityBinding(slot, 0)).ToArray())]),
        };
    }
}
