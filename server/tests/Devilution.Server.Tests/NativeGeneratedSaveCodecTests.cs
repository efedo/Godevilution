using System.Text.Json.Nodes;
using Devilution.Server.Gameplay;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeGeneratedSaveCodecTests
{
    [Fact]
    public void GeneratedIdentitySurvivesTheGameOwnedVersionTwoEnvelope()
    {
        var identity = CreateIdentity();
        var snapshot = CreateSnapshot() with { NativeGeneratedWorldIdentity = identity };
        var codec = new AuthoritativeSaveCodec();
        var serialized = codec.Serialize(snapshot);
        var document = JsonNode.Parse(serialized)!;

        Assert.Equal(2, document["FormatVersion"]!.GetValue<int>());
        Assert.Equal(1, AuthoritativeSaveCodec.CurrentFormatVersion);
        var restored = codec.Deserialize(serialized);
        Assert.Equal(identity.RouteFingerprint, restored.NativeGeneratedWorldIdentity!.RouteFingerprint);
        Assert.Equal(identity.Levels, restored.NativeGeneratedWorldIdentity.Levels);
        Assert.Equal(snapshot.RandomState, restored.RandomState);
        Assert.Equal(snapshot.Player.Gold, restored.Player.Gold);
        Assert.Equal(snapshot.Player.Life, restored.Player.Life);
        var legacyDocument = JsonNode.Parse(codec.Serialize(CreateSnapshot()))!;
        Assert.Equal(legacyDocument["SnapshotBase64"]!.GetValue<string>(), document["SnapshotBase64"]!.GetValue<string>());
    }

    [Fact]
    public void AuthoredProfilesKeepTheOriginalEnvelopeAndNoGeneratedMetadata()
    {
        var codec = new AuthoritativeSaveCodec();
        var serialized = codec.Serialize(CreateSnapshot());
        var document = JsonNode.Parse(serialized)!;
        Assert.Equal(1, document["FormatVersion"]!.GetValue<int>());
        Assert.Null(document["NativeGeneratedWorldIdentity"]);
        Assert.Null(codec.Deserialize(serialized).NativeGeneratedWorldIdentity);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty-levels")]
    [InlineData("null-levels")]
    [InlineData("missing-levels")]
    [InlineData("null-level")]
    [InlineData("wrong-depth")]
    [InlineData("missing-entry")]
    [InlineData("missing-seed")]
    [InlineData("missing-attempt-seed")]
    [InlineData("bad-hash")]
    [InlineData("bad-generator")]
    [InlineData("bad-entry")]
    [InlineData("duplicate-level")]
    public void GeneratedSaveRejectsIncompleteOrMalformedIdentity(string mutation)
    {
        var codec = new AuthoritativeSaveCodec();
        var document = JsonNode.Parse(codec.Serialize(CreateSnapshot() with {
            NativeGeneratedWorldIdentity = CreateIdentity(),
        }))!;
        var identity = document["NativeGeneratedWorldIdentity"]!;
        var levels = identity["Levels"]!.AsArray();
        switch (mutation) {
            case "missing": document.AsObject().Remove("NativeGeneratedWorldIdentity"); break;
            case "null": document["NativeGeneratedWorldIdentity"] = null; break;
            case "empty-levels": levels.Clear(); break;
            case "null-levels": identity["Levels"] = null; break;
            case "missing-levels": identity.AsObject().Remove("Levels"); break;
            case "null-level": levels[0] = null; break;
            case "wrong-depth": levels[0]!["LevelId"] = 2; break;
            case "missing-entry": levels[0]!.AsObject().Remove("Entry"); break;
            case "missing-seed": levels[0]!.AsObject().Remove("DungeonSeed"); break;
            case "missing-attempt-seed": levels[0]!.AsObject().Remove("AttemptSeed"); break;
            case "bad-hash": identity["RouteFingerprint"] = "not-a-hash"; break;
            case "bad-generator": levels[0]!["GeneratorRevision"] = 2; break;
            case "bad-entry": levels[0]!["Entry"] = 99; break;
            case "duplicate-level": levels.Add(levels[0]!.DeepClone()); break;
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void BuiltInAdapterSelectionDoesNotAddAnAmbiguousDefault()
    {
        Assert.Equal("devilution.diablo", Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories()).Id);
        Assert.Equal("devilution.diablo", Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories("external.adapter")).Id);
        Assert.Equal(DiabloAuthoritativeGameHostAdapterFactory.NativeGeneratedId,
            Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories(DiabloAuthoritativeGameHostAdapterFactory.NativeGeneratedId)).Id);
    }

    private static DiabloNativeGeneratedWorldIdentity CreateIdentity()
        => new([new AuthoritativeCathedralLevel1GenerationIdentity(
            AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId,
            AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId, 1,
            1, "cathedral", 2588, AuthoritativeCathedralEntry.Main, 2588, 123,
            new string('A', 64), new string('B', 64), new string('C', 64), new string('D', 64))], new string('E', 64));

    private static DiabloSimulationSnapshot CreateSnapshot()
        => new(5, new StorePlayerSnapshot(7, null, []), [], [], [], [], [], null) { RandomState = 42 };
}
