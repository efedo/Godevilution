using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Independent actual DoLighting/DoUnLight output; expected channels are comparison-only. */
public sealed class AuthoritativeCathedralThemeStaticLightNativeOracleTests
{
    private const int Cells = 112 * 112;
    private const string FileName = "native-theme-static-light.json";

    [Fact(Explicit = true)]
    public void CompleteLightChannelsMatchNative()
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_THEME_STATIC_LIGHT_ORACLE_DIRECTORY");
        Assert.Equal(new[] { FileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var cases = ReadCorpus(AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(
            Path.Combine(directory, FileName), 8 * 1024 * 1024));
        foreach (var row in cases) {
            var light = row.Input.Light.ToArray();
            var preLight = row.Input.PreLight.ToArray();
            AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, new(row.X, row.Y), row.Radius, row.Loading);
            Assert.Equal(row.Expected.Light, light);
            Assert.Equal(row.Expected.PreLight, preLight);
            Assert.NotSame(row.Input.Light, light);
            Assert.NotSame(row.Input.PreLight, preLight);
        }
    }

    [Theory]
    [InlineData("float")]
    [InlineData("negative")]
    [InlineData("shade")]
    [InlineData("size")]
    [InlineData("duplicateCase")]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("wrongPolicy")]
    public void InvalidCorpusIsRejected(string mutation)
    {
        var corpus = PortableCorpus();
        var first = corpus["cases"]![0]!;
        switch (mutation) {
        case "float": first["radius"] = 0.5; break;
        case "negative": first["x"] = -1; break;
        case "shade": first["input"]!["light"]![0] = 16; break;
        case "size": first["input"]!["light"]!.AsArray().RemoveAt(0); break;
        case "duplicateCase": corpus["cases"]![1] = first.DeepClone(); break;
        case "extra": first["unexpected"] = true; break;
        case "missing": first.AsObject().Remove("loadingMapObjects"); break;
        case "wrongPolicy": corpus["policy"] = "hellfire"; break;
        }
        using var document = JsonDocument.Parse(corpus.ToJsonString());
        Assert.ThrowsAny<Exception>(() => ReadCorpus(document.RootElement));
    }

    [Fact]
    public void DuplicateJsonKeysAreRejected()
    {
        var json = PortableCorpus().ToJsonString();
        json = json.Insert(1, "\"version\":1,");
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => ReadCorpus(document.RootElement));
    }

    [Fact]
    public void PortableCorpusRetainsAllRadiiCornersAndLoadingModes()
    {
        using var document = JsonDocument.Parse(PortableCorpus().ToJsonString());
        var cases = ReadCorpus(document.RootElement);
        Assert.Equal(40, cases.Length);
        Assert.All(cases, row => {
            Assert.Equal(Cells, row.Input.Light.Length);
            Assert.Equal(Cells, row.Expected.PreLight.Length);
        });
    }

    private sealed record Channels(byte[] Light, byte[] PreLight);
    private sealed record Case(int X, int Y, int Radius, bool Loading, Channels Input, Channels Expected);

    private static Case[] ReadCorpus(JsonElement root)
    {
        Properties(root, "format", "version", "policy", "cases", "caseCount");
        if (S(root, "format") != "godevilution-cathedral-theme-static-light" || N(root, "version") != 1
            || S(root, "policy") != "original-diablo-cathedral-zero-offset-static-light" || N(root, "caseCount") != 40)
            throw new InvalidDataException("The static-light corpus identity is not supported.");
        var cases = A(root, "cases").EnumerateArray().Select(row => {
            Properties(row, "x", "y", "radius", "loadingMapObjects", "input", "expected");
            var x = N(row, "x"); var y = N(row, "y"); var radius = N(row, "radius");
            if (x is < 0 or > 111 || y is < 0 or > 111 || radius is < 0 or > 15)
                throw new InvalidDataException("Static-light case bounds are unsafe.");
            return new Case(x, y, radius, B(row, "loadingMapObjects"),
                ReadChannels(row.GetProperty("input")), ReadChannels(row.GetProperty("expected")));
        }).ToArray();
        var keys = cases.Select(row => (row.X, row.Y, row.Radius, row.Loading)).ToArray();
        if (cases.Length != 40 || !keys.SequenceEqual(Matrix()))
            throw new InvalidDataException("The complete ordered static-light matrix is required.");
        return cases;
    }

    private static Channels ReadChannels(JsonElement value)
    {
        Properties(value, "light", "preLight");
        byte[] Channel(string name)
        {
            var values = A(value, name).EnumerateArray().Select(Integer).ToArray();
            if (values.Length != Cells || values.Any(shade => shade is < 0 or > 15))
                throw new InvalidDataException("Each native light channel requires 112x112 bounded shades.");
            return values.Select(shade => (byte)shade).ToArray();
        }
        return new(Channel("light"), Channel("preLight"));
    }

    private static IEnumerable<(int X, int Y, int Radius, bool Loading)> Matrix()
    {
        for (var radius = 0; radius < 16; radius++)
            foreach (var loading in new[] { false, true }) yield return (50, 50, radius, loading);
        foreach (var (x, y) in new[] { (0, 0), (111, 0), (0, 111), (111, 111) })
            foreach (var loading in new[] { false, true }) yield return (x, y, 5, loading);
    }

    private static JsonObject PortableCorpus()
    {
        JsonObject Channels()
            => new() { ["light"] = JsonSerializer.SerializeToNode(new int[Cells]),
                ["preLight"] = JsonSerializer.SerializeToNode(new int[Cells]) };
        var cases = new JsonArray();
        foreach (var (x, y, radius, loading) in Matrix())
            cases.Add(new JsonObject { ["x"] = x, ["y"] = y, ["radius"] = radius,
                ["loadingMapObjects"] = loading, ["input"] = Channels(), ["expected"] = Channels() });
        return new() { ["format"] = "godevilution-cathedral-theme-static-light", ["version"] = 1,
            ["policy"] = "original-diablo-cathedral-zero-offset-static-light", ["caseCount"] = 40, ["cases"] = cases };
    }
}
