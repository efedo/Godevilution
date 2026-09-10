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
    public void RejectsLegacyNativeMissileMetadataSchemaInCurrentSchema()
    {
        var basePack = LoadBasePack();
        var nativeMissiles = basePack.Tables.Single(
            table => table.SourcePath == "native_missiles.tsv");
        var pack = ReplaceBaseTable(
            basePack,
            nativeMissiles.SourcePath,
            CreateLegacyNativeMissileTable(nativeMissiles));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-native-missile-schema",
                    "1",
                    [pack]))));

        Assert.Contains("Production native-missile table", exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsLegacyNativeMissileAliasSchemaInCurrentSchema()
    {
        var basePack = LoadBasePack();
        var aliases = basePack.Tables.Single(
            table => table.SourcePath == "native_missile_aliases.tsv");
        var pack = ReplaceBaseTable(
            basePack,
            aliases.SourcePath,
            CreateLegacyNativeMissileAliasTable(aliases));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-native-missile-alias-schema",
                    "1",
                    [pack]))));

        Assert.Contains("Production native-missile-alias table", exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyNativeMissileMetadataCompatibility()
    {
        var basePack = LoadBasePack();
        var nativeMissiles = basePack.Tables.Single(
            table => table.SourcePath == "native_missiles.tsv");
        var current = ReplaceBaseTable(
            basePack,
            nativeMissiles.SourcePath,
            CreateLegacyNativeMissileTable(nativeMissiles));
        var previous = new ContentPack(
            current.Id,
            current.Version,
            current.Tables,
            current.Dependencies,
            current.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.499.0"),
            current.DependencyConstraints,
            current.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-native-missile-contract",
                "1",
                [previous])));

        Assert.NotNull(activation);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyNativeMissileAliasCompatibility()
    {
        var basePack = LoadBasePack();
        var aliases = basePack.Tables.Single(
            table => table.SourcePath == "native_missile_aliases.tsv");
        var current = ReplaceBaseTable(
            basePack,
            aliases.SourcePath,
            CreateLegacyNativeMissileAliasTable(aliases));
        var previous = new ContentPack(
            current.Id,
            current.Version,
            current.Tables,
            current.Dependencies,
            current.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.499.0"),
            current.DependencyConstraints,
            current.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-native-missile-alias-contract",
                "1",
                [previous])));

        Assert.NotNull(activation);
    }

    [Fact]
    public void RejectsLegacyNativeMissileSpriteMetadataSchemaInCurrentSchema()
    {
        var basePack = LoadBasePack();
        var nativeSprites = basePack.Tables.Single(
            table => table.SourcePath == "native_missile_sprites.tsv");
        var pack = ReplaceBaseTable(
            basePack,
            nativeSprites.SourcePath,
            CreateLegacyNativeMissileSpriteTable(nativeSprites));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-native-missile-sprite-schema",
                    "1",
                    [pack]))));

        Assert.Contains("Production native-missile sprite table", exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyNativeMissileSpriteMetadataCompatibility()
    {
        var basePack = LoadBasePack();
        var nativeSprites = basePack.Tables.Single(
            table => table.SourcePath == "native_missile_sprites.tsv");
        var current = ReplaceBaseTable(
            basePack,
            nativeSprites.SourcePath,
            CreateLegacyNativeMissileSpriteTable(nativeSprites));
        var previous = new ContentPack(
            current.Id,
            current.Version,
            current.Tables,
            current.Dependencies,
            current.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.499.0"),
            current.DependencyConstraints,
            current.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-native-missile-sprite-contract",
                "1",
                [previous])));

        Assert.NotNull(activation);
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

    private static TsvTable CreateLegacyNativeMissileTable(TsvTable nativeMissiles)
    {
        var lines = nativeMissiles.ToCanonicalTsv()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => {
                var fields = line.Split('\t');
                return string.Join('\t', fields.Where((_, index) =>
                    !string.Equals(nativeMissiles.Columns[index], "movement_distribution", StringComparison.Ordinal)));
            });
        return TsvTable.Parse(nativeMissiles.SourcePath, string.Join('\n', lines) + "\n");
    }

    private static TsvTable CreateLegacyNativeMissileAliasTable(TsvTable aliases)
    {
        var lines = aliases.ToCanonicalTsv()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line + "\tlegacy_name");
        return TsvTable.Parse(
            aliases.SourcePath,
            string.Join('\n', lines) + "\n");
    }

    private static TsvTable CreateLegacyNativeMissileSpriteTable(TsvTable nativeSprites)
    {
        var lines = nativeSprites.ToCanonicalTsv()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => {
                var fields = line.Split('\t');
                return string.Join('\t', fields.Where((_, index) =>
                    !string.Equals(nativeSprites.Columns[index], "alternate_width", StringComparison.Ordinal)));
            });
        return TsvTable.Parse(nativeSprites.SourcePath, string.Join('\n', lines) + "\n");
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
