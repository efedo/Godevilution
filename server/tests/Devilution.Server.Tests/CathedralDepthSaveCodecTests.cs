using System.Text.Json.Nodes;
using Devilution.Server.Gameplay;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class CathedralDepthSaveCodecTests
{
    [Fact]
    public void DepthIdentityUsesItsOwnVersionThreeEnvelopeAndUnchangedWirePayload()
    {
        var codec = new AuthoritativeSaveCodec();
        var original = CreateSnapshot();
        var identity = CreateIdentity();
        var serialized = codec.Serialize(original with { CathedralDepthWorldIdentity = identity });
        var document = JsonNode.Parse(serialized)!;
        Assert.Equal(3, document["FormatVersion"]!.GetValue<int>());
        Assert.Equal(1, AuthoritativeSaveCodec.CurrentFormatVersion);
        Assert.Equal(2, AuthoritativeSaveCodec.NativeGeneratedFormatVersion);
        Assert.Null(document["NativeGeneratedWorldIdentity"]);
        Assert.Equal(JsonNode.Parse(codec.Serialize(original))!["SnapshotBase64"]!.GetValue<string>(),
            document["SnapshotBase64"]!.GetValue<string>());
        var restored = codec.Deserialize(serialized);
        Assert.Null(restored.NativeGeneratedWorldIdentity);
        Assert.Equal(identity.Levels, restored.CathedralDepthWorldIdentity!.Levels);
        Assert.Equal(identity.RouteFingerprint, restored.CathedralDepthWorldIdentity.RouteFingerprint);
        Assert.Equal(original.RandomState, restored.RandomState);
        Assert.Equal(original.Player.Gold, restored.Player.Gold);
        Assert.Equal(original.Player.Life, restored.Player.Life);
        Assert.Equal(serialized, codec.Serialize(restored));
    }

    [Theory]
    [InlineData("missing-identity")]
    [InlineData("null-identity")]
    [InlineData("empty-levels")]
    [InlineData("null-levels")]
    [InlineData("missing-levels")]
    [InlineData("null-level")]
    [InlineData("wrong-depth")]
    [InlineData("wrong-id")]
    [InlineData("reordered")]
    [InlineData("duplicate-level")]
    [InlineData("missing-entry")]
    [InlineData("missing-seed")]
    [InlineData("missing-depth")]
    [InlineData("missing-policy")]
    [InlineData("missing-attempt")]
    [InlineData("missing-final-rng")]
    [InlineData("bad-hash")]
    [InlineData("lowercase-hash")]
    [InlineData("bad-generator")]
    [InlineData("bad-revision")]
    [InlineData("bad-catalog")]
    [InlineData("bad-policy")]
    [InlineData("bad-entry")]
    [InlineData("duplicate-symbol")]
    [InlineData("unknown-envelope-field")]
    [InlineData("unknown-identity-field")]
    [InlineData("unknown-level-field")]
    public void RejectsMalformedOrAmbiguousDepthMetadata(string mutation)
    {
        var codec = new AuthoritativeSaveCodec();
        var document = JsonNode.Parse(codec.Serialize(CreateSnapshot() with {
            CathedralDepthWorldIdentity = CreateIdentity(),
        }))!;
        var identity = document["CathedralDepthWorldIdentity"]!;
        var levels = identity["Levels"]!.AsArray();
        switch (mutation) {
            case "missing-identity": document.AsObject().Remove("CathedralDepthWorldIdentity"); break;
            case "null-identity": document["CathedralDepthWorldIdentity"] = null; break;
            case "empty-levels": levels.Clear(); break;
            case "null-levels": identity["Levels"] = null; break;
            case "missing-levels": identity.AsObject().Remove("Levels"); break;
            case "null-level": levels[2] = null; break;
            case "wrong-depth": levels[2]!["Depth"] = 4; break;
            case "wrong-id": levels[2]!["LevelId"] = 4; break;
            case "reordered":
                var first = levels[0]!.DeepClone();
                levels[0] = levels[1]!.DeepClone();
                levels[1] = first;
                break;
            case "duplicate-level": levels.Add(levels[0]!.DeepClone()); break;
            case "missing-entry": levels[2]!.AsObject().Remove("Entry"); break;
            case "missing-seed": levels[2]!.AsObject().Remove("DungeonSeed"); break;
            case "missing-depth": levels[2]!.AsObject().Remove("Depth"); break;
            case "missing-policy": levels[2]!.AsObject().Remove("Policy"); break;
            case "missing-attempt": levels[2]!.AsObject().Remove("AttemptSeed"); break;
            case "missing-final-rng": levels[2]!.AsObject().Remove("FinalRandomState"); break;
            case "bad-hash": identity["RouteFingerprint"] = "not-a-hash"; break;
            case "lowercase-hash": levels[2]!["FineFingerprint"] = new string('b', 64); break;
            case "bad-generator": levels[2]!["GeneratorId"] = AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId; break;
            case "bad-revision": levels[2]!["GeneratorRevision"] = 2; break;
            case "bad-catalog": levels[2]!["CatalogId"] = AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId; break;
            case "bad-policy": levels[2]!["Policy"] = 99; break;
            case "bad-entry": levels[2]!["Entry"] = 99; break;
            case "duplicate-symbol": levels[2]!["LevelSymbol"] = levels[1]!["LevelSymbol"]!.DeepClone(); break;
            case "unknown-envelope-field": document["NativeGeneratedWorldIdentity"] = new JsonObject(); break;
            case "unknown-identity-field": identity["Other"] = 1; break;
            case "unknown-level-field": levels[2]!["Other"] = 1; break;
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Theory]
    [InlineData("FormatVersion")]
    [InlineData("RouteFingerprint")]
    [InlineData("Depth")]
    public void RejectsDuplicateMetadataProperties(string name)
    {
        var codec = new AuthoritativeSaveCodec();
        var serialized = codec.Serialize(CreateSnapshot() with { CathedralDepthWorldIdentity = CreateIdentity() });
        var value = name switch { "FormatVersion" => "3", "Depth" => "1", _ => "\"" + new string('E', 64) + "\"" };
        serialized = serialized.Replace($"\"{name}\":{value}", $"\"{name}\":{value},\"{name}\":{value}", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(serialized));
    }

    [Fact]
    public void BuiltInSelectionAndLegacyConstructorsRemainUnambiguous()
    {
        Assert.Equal("devilution.diablo", Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories()).Id);
        Assert.Equal(DiabloAuthoritativeGameHostAdapterFactory.NativeGeneratedId,
            new DiabloAuthoritativeGameHostAdapterFactory(true).Id);
        Assert.Equal("devilution.diablo", new DiabloAuthoritativeGameHostAdapterFactory(false).Id);
        Assert.Equal(DiabloAuthoritativeGameHostAdapterFactory.CathedralDepthId,
            Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories(DiabloAuthoritativeGameHostAdapterFactory.CathedralDepthId)).Id);
        Assert.Throws<ArgumentException>(() => new DiabloAuthoritativeGameHostAdapterFactory("unrecognized"));
    }

    [Fact]
    public void CannotSerializeMixedGenerationProfiles()
    {
        var legacy = new DiabloNativeGeneratedWorldIdentity([new(
            AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId,
            AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId, 1,
            1, "cathedral", 2588, AuthoritativeCathedralEntry.Main, 2588, 123,
            new string('A', 64), new string('B', 64), new string('C', 64), new string('D', 64))], new string('E', 64));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSaveCodec().Serialize(CreateSnapshot() with {
            NativeGeneratedWorldIdentity = legacy, CathedralDepthWorldIdentity = CreateIdentity(),
        }));
    }

    private static DiabloCathedralDepthWorldIdentity CreateIdentity()
        => new(Enumerable.Range(1, 4).Select(depth => new AuthoritativeCathedralDepthGenerationIdentity(
            AuthoritativeCathedralDepthRuntimeCatalog.CatalogId,
            AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId, 1,
            (uint)depth, depth == 1 ? "cathedral" : $"cathedral_depth_{depth}", depth,
            AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
            depth == 4 ? uint.MaxValue : (uint)depth, AuthoritativeCathedralEntry.Main, (uint)depth, 123,
            new string('A', 64), new string('B', 64), new string('C', 64), new string('D', 64))).ToArray(), new string('E', 64));

    private static DiabloSimulationSnapshot CreateSnapshot()
        => new(5, new StorePlayerSnapshot(7, null, []), [], [], [], [], [], null) { RandomState = 42 };
}
