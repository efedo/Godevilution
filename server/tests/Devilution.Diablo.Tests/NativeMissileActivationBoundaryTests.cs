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

        Assert.Contains(
            "requires external 'native_missiles.tsv' content",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNativeMissileNamespaceWithoutSpriteCatalog()
    {
        var pack = LoadBasePackWithout("native_missile_sprites.tsv");

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("missing-native-sprites", "1", [pack]))));

        Assert.Contains(
            "requires external 'native_missile_sprites.tsv' content",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPartialNativeMissileNamespaceDuringProductionActivation()
    {
        var basePack = LoadBasePack();
        var nativeMissiles = basePack.Tables.Single(
            table => table.SourcePath == "native_missiles.tsv");
        var partialContent = string.Join(
            '\n',
            nativeMissiles.ToCanonicalTsv()
                .Split('\n')
                .Where(line => !line.StartsWith("Firebolt\t", StringComparison.Ordinal)));
        var pack = ReplaceBaseTable(
            basePack,
            nativeMissiles.SourcePath,
            TsvTable.Parse(nativeMissiles.SourcePath, partialContent));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("partial-native-missiles", "1", [pack]))));

        Assert.Contains("complete native misdat.tsv contract", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Firebolt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPartialNativeMissileSpriteNamespaceDuringProductionActivation()
    {
        var basePack = LoadBasePack();
        var nativeSprites = basePack.Tables.Single(
            table => table.SourcePath == "native_missile_sprites.tsv");
        var partialContent = string.Join(
            '\n',
            nativeSprites.ToCanonicalTsv()
                .Split('\n')
                .Where(line => !line.StartsWith("Fireball\t", StringComparison.Ordinal)));
        var pack = ReplaceBaseTable(
            basePack,
            nativeSprites.SourcePath,
            TsvTable.Parse(nativeSprites.SourcePath, partialContent));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("partial-native-sprites", "1", [pack]))));

        Assert.Contains("required base missile_sprites.tsv identities", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Fireball", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectCompatibilityCatalogsMayRemainPartialUntilExplicitValidation()
    {
        var catalog = new AuthoritativeNativeMissileCatalog([
            new("Firebolt", "", "", "", "", "", "", "", null),
        ]);

        Assert.Single(catalog.Definitions);

        var exception = Assert.Throws<InvalidDataException>(
            catalog.ValidateCompleteNativeNamespace);

        Assert.Contains("Acid", exception.Message, StringComparison.Ordinal);
    }

    private static ContentPack LoadBasePackWithout(string sourcePath)
        => ReplaceBaseTable(
            LoadBasePack(),
            sourcePath,
            replacement: null);

    private static ContentPack LoadBasePack()
    {
        var root = FindRepositoryRoot();
        return ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
    }

    private static ContentPack ReplaceBaseTable(
        ContentPack basePack,
        string sourcePath,
        TsvTable? replacement)
    {
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables.Where(table => !string.Equals(
                table.SourcePath,
                sourcePath,
                StringComparison.OrdinalIgnoreCase))
                .Concat(replacement is null ? [] : [replacement])
                .ToArray(),
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
