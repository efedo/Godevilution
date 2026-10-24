using System.Text.Json;
using Godevilution.Tools.Setup;
using Xunit;

namespace Devilution.Diablo.Tests;

/** First-run native setup: archive discovery order, fingerprint inputs and build reuse. */
public sealed class NativeSetupTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("native-setup-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    private static NativeArchiveDiscovery.Environment Environment(string? explicitPath = null, string? configured = null,
        string? environment = null, string? applicationData = null, string[]? gog = null, string[]? known = null)
        => new(explicitPath, configured, environment, applicationData, gog ?? [], known ?? []);

    [Fact]
    public void CandidatesFollowExplicitConfiguredEnvironmentThenNativeSearchOrder()
    {
        var candidates = NativeArchiveDiscovery.Candidates(Environment("x.mpq", "c.mpq", "e.mpq",
            Path.Combine("app"), [Path.Combine("gog")], [Path.Combine("known")]));
        Assert.Equal([
            NativeArchiveOrigin.Explicit, NativeArchiveOrigin.Configured, NativeArchiveOrigin.Environment,
            NativeArchiveOrigin.PreferencePath, NativeArchiveOrigin.PreferencePath,
            NativeArchiveOrigin.GogRegistry, NativeArchiveOrigin.GogRegistry,
            NativeArchiveOrigin.KnownInstall, NativeArchiveOrigin.KnownInstall,
        ], candidates.Select(candidate => candidate.Origin));
        Assert.Equal(Path.Combine("app", "diasurgical", "devilution", "DIABDAT.MPQ"), candidates[3].Path);
        Assert.Equal(Path.Combine("gog", "DIABDAT.MPQ"), candidates[5].Path);
        Assert.Empty(NativeArchiveDiscovery.Candidates(Environment(" ", null, "")));
    }

    [Fact]
    public void FindReturnsTheFirstUsableArchiveAndRefusesABadExplicitOne()
    {
        var gog = Directory.CreateDirectory(Path.Combine(root, "gog")).FullName;
        var known = Directory.CreateDirectory(Path.Combine(root, "known")).FullName;
        File.WriteAllText(Path.Combine(gog, "DIABDAT.MPQ"), "unusable");
        File.WriteAllText(Path.Combine(known, "DIABDAT.MPQ"), "usable");
        bool Usable(string path) => File.ReadAllText(path) == "usable";

        var found = NativeArchiveDiscovery.Find(Environment(configured: Path.Combine(root, "missing.mpq"), gog: [gog], known: [known]), Usable);
        Assert.Equal((Path.Combine(known, "DIABDAT.MPQ"), NativeArchiveOrigin.KnownInstall), (found!.Path, found.Origin));
        Assert.Null(NativeArchiveDiscovery.Find(Environment(gog: [gog]), Usable));
        Assert.Throws<FileNotFoundException>(() => NativeArchiveDiscovery.Find(Environment(Path.Combine(gog, "DIABDAT.MPQ"), known: [known]), Usable));
        Assert.False(NativeArchiveDiscovery.IsUsableArchive(Path.Combine(gog, "DIABDAT.MPQ")));
    }

    [Fact]
    public void FingerprintTracksInputsArchiveIdentityAndIgnoresBuildOutput()
    {
        var repository = FakeRepository();
        var archive = Path.Combine(root, "DIABDAT.MPQ");
        File.WriteAllText(archive, "archive");
        var baseline = NativeSetupFingerprint.Compute(repository, archive);
        Assert.Equal(baseline, NativeSetupFingerprint.Compute(repository, archive));

        Directory.CreateDirectory(Path.Combine(repository, "tools", "Native.Setup", "bin"));
        File.WriteAllText(Path.Combine(repository, "tools", "Native.Setup", "bin", "out.dll"), "build output");
        Assert.Equal(baseline, NativeSetupFingerprint.Compute(repository, archive));

        File.WriteAllText(Path.Combine(repository, "assets", "txtdata", "table.tsv"), "changed");
        var changedInput = NativeSetupFingerprint.Compute(repository, archive);
        Assert.NotEqual(baseline, changedInput);

        File.WriteAllText(Path.Combine(repository, "server", "src", "Devilution.Diablo", "Schema.cs"), "// new schema");
        Assert.NotEqual(changedInput, NativeSetupFingerprint.Compute(repository, archive));

        var beforeArchive = NativeSetupFingerprint.Compute(repository, archive);
        File.WriteAllText(archive, "archive, patched");
        Assert.NotEqual(beforeArchive, NativeSetupFingerprint.Compute(repository, archive));

        File.Delete(Path.Combine(repository, "assets", "levels", "towndata", "automap.dun"));
        Assert.Throws<FileNotFoundException>(() => NativeSetupFingerprint.Compute(repository, archive));
    }

    [Fact]
    public void ReuseRequiresTheSameFingerprintAndEveryPublishedOutput()
    {
        var repository = FakeRepository();
        var setup = Directory.CreateDirectory(Path.Combine(root, "setup")).FullName;
        var profile = Directory.CreateDirectory(Path.Combine(setup, "profile-0123456789abcdef")).FullName;
        File.WriteAllText(Path.Combine(profile, NativeSetupBuilder.TownProfileFileName), "{}");
        var packs = new[] { "native-players", "native-scenes", "native-towners", "native-objects" }.Select(folder => {
            var directory = Directory.CreateDirectory(Path.Combine(NativeSetupBuilder.ClientAssetsDirectory(repository), folder, "setup-0123456789abcdef")).FullName;
            File.WriteAllText(Path.Combine(directory, "catalog.json"), "{}");
            return NativeSetupBuilder.FileToResource(repository, directory, "catalog.json");
        }).ToArray();
        Assert.Equal("res://assets/native-players/setup-0123456789abcdef/catalog.json", packs[0]);
        var fingerprint = new string('a', 64);
        var state = new NativeSetupState(NativeSetupFingerprint.SchemaVersion, fingerprint, "DIABDAT.MPQ",
            profile, new string('b', 64), packs[0], packs[1], packs[2], packs[3]);
        var statePath = Path.Combine(setup, NativeSetupBuilder.StateFileName);

        Assert.Null(NativeSetupBuilder.TryReuse(setup, repository, fingerprint));
        File.WriteAllText(statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        Assert.Equal(profile, NativeSetupBuilder.TryReuse(setup, repository, fingerprint)!.ProfileDirectory);
        Assert.Null(NativeSetupBuilder.TryReuse(setup, repository, new string('c', 64)));
        File.Delete(NativeSetupBuilder.ResourceToFile(repository, packs[2]));
        Assert.Null(NativeSetupBuilder.TryReuse(setup, repository, fingerprint));
        File.WriteAllText(statePath, "not json");
        Assert.Null(NativeSetupBuilder.TryReuse(setup, repository, fingerprint));
    }

    private string FakeRepository()
    {
        var repository = Path.Combine(root, "repository");
        foreach (var input in NativeSetupFingerprint.InputPaths) {
            var path = Path.Combine(repository, input);
            if (Path.GetExtension(input) is ".txt" or ".dun") {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, input);
            } else {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "input.txt"), input);
            }
        }
        return repository;
    }
}
