using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeMissileActivationBoundaryTests
{
    [Fact]
    public void RejectsMonsterMappingsWithoutNativeMissileNamespace()
    {
        var pack = LoadBasePackWithout("native_missiles.tsv");

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("missing-native-missiles", "1", [pack]))));

        Assert.Contains("monster_missiles.tsv requires native_missiles.tsv", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNativeMissileNamespaceWithoutSpriteCatalog()
    {
        var pack = LoadBasePackWithout("native_missile_sprites.tsv");

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("missing-native-sprites", "1", [pack]))));

        Assert.Contains("native_missiles.tsv requires native_missile_sprites.tsv", exception.Message, StringComparison.Ordinal);
    }

    private static ContentPack LoadBasePackWithout(string sourcePath)
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables.Where(table => !string.Equals(
                table.SourcePath,
                sourcePath,
                StringComparison.OrdinalIgnoreCase)).ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
