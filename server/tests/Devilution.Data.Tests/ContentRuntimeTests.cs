using Xunit;

namespace Devilution.Data.Tests;

public sealed class ContentRuntimeTests
{
    [Theory]
    [InlineData("../outside.dun")]
    [InlineData("levels/../../outside.dun")]
    [InlineData("/absolute/level.dun")]
    [InlineData("C:/absolute/level.dun")]
    public void ContentAssetsRejectPathsOutsideThePack(string sourcePath)
    {
        Assert.Throws<ArgumentException>(() => new ContentAsset(sourcePath, ReadOnlyMemory<byte>.Empty));
    }

    [Theory]
    [InlineData("../outside.tsv")]
    [InlineData("levels/../../outside.tsv")]
    [InlineData("/absolute/table.tsv")]
    [InlineData("C:/absolute/table.tsv")]
    [InlineData("levels//table.tsv")]
    public void TablesAndOverlaysRejectNonCanonicalPaths(string sourcePath)
    {
        Assert.Throws<ArgumentException>(() => TsvTable.Parse(sourcePath, "id\nvalue\n"));
        Assert.Throws<ArgumentException>(() => new ContentTableOverlay(sourcePath, ["id"]));
    }

    [Fact]
    public void ContentPackRejectsDirectTablesWithNonCanonicalPaths()
    {
        var table = new TsvTable("items\\nested.tsv", ["id"], []);

        Assert.Throws<ArgumentException>(() => new ContentPack("base", "1", [table]));
    }

    [Fact]
    public void TsvLoaderPreservesRowsAndRejectsMalformedValues()
    {
        var table = TsvTable.Parse("items.tsv", "\uFEFFid\tvalue\n# comment\na\t42\r\nb\t7\n");

        Assert.Equal(["id", "value"], table.Columns);
        Assert.Equal(["a", "b"], table.Rows.Select(row => row.Required("id")));
        Assert.Equal(42, table.Rows[0].RequiredInt32("value"));
        Assert.Throws<InvalidDataException>(() => TsvTable.Parse("bad.tsv", "id\tvalue\na\n"));
        Assert.Throws<InvalidDataException>(() => TsvTable
            .Parse("bad.tsv", "id\tvalue\na\tnot-a-number\n")
            .Rows[0]
            .RequiredInt32("value"));
    }

    [Fact]
    public void ParsesStrictBooleanTsvValues()
    {
        var table = TsvTable.Parse(
            "flags.tsv",
            "id\tenabled\tvisible\n"
                + "one\ttrue\t0\n"
                + "two\t1\tfalse\n");

        Assert.True(table.Rows[0].RequiredBoolean("enabled"));
        Assert.False(table.Rows[0].RequiredBoolean("visible"));
        Assert.True(table.Rows[1].OptionalBoolean("enabled"));
        Assert.False(table.Rows[1].OptionalBoolean("visible"));
        Assert.False(table.Rows[0].OptionalBoolean("missing"));
        Assert.Null(table.Rows[0].OptionalNullableBoolean("missing"));
        Assert.True(table.Rows[0].OptionalNullableBoolean("enabled"));
        Assert.Throws<InvalidDataException>(() =>
            TsvTable.Parse("bad-flags.tsv", "id\tenabled\nrow\t2\n")
                .Rows[0]
                .RequiredBoolean("enabled"));
    }

    [Fact]
    public void ContentManifestHashMatchesNativeGoldenVectors()
    {
        var items = TsvTable.Parse("items.tsv", "id\tname\nshort-sword\tShort Sword\nbuckler\tBuckler\n");
        var monsters = TsvTable.Parse("monsters.tsv", "id\tname\ngoat\tGoatman\n");
        var baseline = new ContentManifest(
            "baseline",
            "1",
            [new ContentPack("core", "1", [items]), new ContentPack("expansion", "2", [monsters])]);
        var reordered = new ContentManifest(
            "baseline",
            "1",
            [new ContentPack("expansion", "2", [monsters]), new ContentPack("core", "1", [items])]);
        var changedItems = TsvTable.Parse(
            "items.tsv",
            "id\tname\nshort-sword\tShort Sword\nbuckler\tTower Shield\n");
        var changed = new ContentManifest(
            "baseline",
            "1",
            [new ContentPack("core", "1", [changedItems]), new ContentPack("expansion", "2", [monsters])]);

        Assert.Equal("40d13766d2726d36216120e9cd36e064c72fee0395a46d23abcf6d316c9ff34c", baseline.Sha256);
        Assert.Equal("5c536ffd249de81cefd8d74bcf35b4738f129d950a89e339f521c0f1994ee35f", reordered.Sha256);
        Assert.Equal("5d12a34dddd0fdaa81382946736e6637afc5e319e5228a89571144da2a24cf59", changed.Sha256);
    }

    [Fact]
    public void ContentManifestRejectsDuplicatePackIds()
    {
        var table = TsvTable.Parse("items.tsv", "id\n1\n");

        Assert.Throws<ArgumentException>(() => new ContentManifest(
            "base",
            "1",
            [new ContentPack("core", "1", [table]), new ContentPack("core", "2", [table])]));
    }

    [Fact]
    public void ContentPackRejectsCaseInsensitiveTableAndAssetDuplicates()
    {
        var tables = new[] {
            TsvTable.Parse("items.tsv", "id\n1\n"),
            TsvTable.Parse("ITEMS.TSV", "id\n2\n"),
        };
        Assert.Throws<ArgumentException>(() => new ContentPack("base", "1", tables));

        var assets = new[] {
            new ContentAsset("levels/sample.dun", new byte[] { 1 }),
            new ContentAsset("LEVELS/SAMPLE.DUN", new byte[] { 1 }),
        };
        Assert.Throws<ArgumentException>(() => new ContentPack(
            "base",
            "1",
            [],
            assets: assets));

        Assert.Throws<ArgumentException>(() => new ContentPack(
            "base",
            "1",
            [TsvTable.Parse("items.tsv", "id\n1\n")],
            assets: [new ContentAsset("ITEMS.TSV", new byte[] { 1 })]));
    }

    [Fact]
    public void ContentPackDirectoryLoaderUsesStableRelativeTableOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"godevilution-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try {
            File.WriteAllText(Path.Combine(root, "z.tsv"), "id\nlast\n");
            File.WriteAllText(Path.Combine(root, "nested", "a.tsv"), "id\nfirst\n");

            var pack = ContentPackLoader.LoadDirectory("base", "1", root);

            Assert.Equal(["nested/a.tsv", "z.tsv"], pack.Tables.Select(table => table.SourcePath));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ContentPackDirectoryLoaderCarriesNativeBinaryAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), $"godevilution-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "levels", "l1data"));
        try {
            File.WriteAllText(Path.Combine(root, "rules.tsv"), "id\nbase\n");
            File.WriteAllBytes(
                Path.Combine(root, "levels", "l1data", "sample.dun"),
                [1, 2, 3, 4]);

            var pack = ContentPackLoader.LoadDirectory("base", "1", root);

            Assert.Equal("levels/l1data/sample.dun", Assert.Single(pack.Assets).SourcePath);
            Assert.Equal([1, 2, 3, 4], Assert.Single(pack.Assets).Contents.ToArray());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ContentPackDirectoryLoaderRejectsReparsePointEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"godevilution-content-{Guid.NewGuid():N}");
        var outside = Path.Combine(Path.GetTempPath(), $"godevilution-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        try {
            File.WriteAllText(Path.Combine(outside, "rules.tsv"), "id\nexternal\n");
            try {
                Directory.CreateSymbolicLink(Path.Combine(root, "linked"), outside);
            } catch (Exception exception) when (exception is UnauthorizedAccessException
                or PlatformNotSupportedException
                or IOException) {
                return;
            }

            var error = Assert.Throws<InvalidDataException>(() =>
                ContentPackLoader.LoadDirectory("base", "1", root));
            Assert.Contains("reparse-point", error.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void ContentPackDirectoryLoaderRejectsAReparsePointRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"godevilution-content-{Guid.NewGuid():N}");
        var outside = Path.Combine(Path.GetTempPath(), $"godevilution-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try {
            File.WriteAllText(Path.Combine(outside, "rules.tsv"), "id\nexternal\n");
            try {
                Directory.CreateSymbolicLink(root, outside);
            } catch (Exception exception) when (exception is UnauthorizedAccessException
                or PlatformNotSupportedException
                or IOException) {
                return;
            }

            var error = Assert.Throws<InvalidDataException>(() =>
                ContentPackLoader.LoadDirectory("base", "1", root));
            Assert.Contains("reparse-point", error.Message, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void ContentPackDirectoryLoaderRejectsAnEmptyPack()
    {
        var root = Path.Combine(Path.GetTempPath(), $"godevilution-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try {
            Assert.Throws<InvalidDataException>(() => ContentPackLoader.LoadDirectory("base", "1", root));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CompositionAppliesCompositeKeyUpsertsInStableOrder()
    {
        var baseStores = TsvTable.Parse(
            "stores.tsv",
            "store_id\tstore_slot\tprice\n1\t0\t75\n1\t1\t100\n");
        var overlayStores = TsvTable.Parse(
            "stores.tsv",
            "store_id\tstore_slot\tprice\n1\t0\t90\n2\t0\t40\n");
        var manifest = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [baseStores]),
            new ContentPack(
                "balance",
                "1",
                [overlayStores],
                ["base"],
                [new ContentTableOverlay("stores.tsv", ["store_id", "store_slot"])]),
        ]);

        var content = ComposedContent.Compose(manifest);
        var stores = content.GetRequiredTable("stores.tsv");

        Assert.Equal(["90", "100", "40"], stores.Rows.Select(row => row.Required("price")));
        Assert.Equal(["1:0", "1:1", "2:0"], stores.Rows.Select(row =>
            $"{row.Required("store_id")}:{row.Required("store_slot")}"));
        Assert.Equal(
            stores.ToCanonicalTsv(),
            TsvTable.Parse("stores.tsv", stores.ToCanonicalTsv()).ToCanonicalTsv());
    }

    [Fact]
    public void CompositionAllowsAValidatedOverlayToIntroduceAnOptionalTable()
    {
        var optional = TsvTable.Parse(
            "optional.tsv",
            "id\tvalue\n1\tfirst\n2\tsecond\n");
        var manifest = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [TsvTable.Parse("items.tsv", "id\tvalue\n1\tbase\n")]),
            new ContentPack(
                "extension",
                "1",
                [optional],
                ["base"],
                [new ContentTableOverlay("optional.tsv", ["id"])]),
        ]);

        var content = ComposedContent.Compose(manifest);

        Assert.Equal(
            ["first", "second"],
            content.GetRequiredTable("optional.tsv").Rows.Select(row => row.Required("value")));
    }

    [Fact]
    public void CompositionExposesAssetsAndRejectsConflictingDuplicates()
    {
        var first = new ContentAsset("levels/l1data/l1.til", new byte[] { 1, 2 });
        var same = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [first]),
            new ContentPack("variant", "1", [], ["base"], [], assets: [
                new ContentAsset("LEVELS/L1DATA/L1.TIL", new byte[] { 1, 2 })]),
        ]);

        var composed = ComposedContent.Compose(same);
        Assert.Equal([1, 2], composed.GetRequiredAsset("levels\\l1data\\l1.til").Contents.ToArray());

        var conflicting = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [first]),
            new ContentPack("variant", "1", [], ["base"], [], assets: [
                new ContentAsset("levels/l1data/l1.til", new byte[] { 9, 9 })]),
        ]);
        var exception = Assert.Throws<InvalidDataException>(() => ComposedContent.Compose(conflicting));
        Assert.Contains("l1.til", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeAssetBytesContributeToManifestIdentity()
    {
        var baseline = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/sample.dun", new byte[] { 1, 2, 3 })]),
        ]);
        var changed = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/sample.dun", new byte[] { 1, 2, 4 })]),
        ]);

        Assert.NotEqual(baseline.Sha256, changed.Sha256);
    }

    [Fact]
    public void CompositionRejectsImplicitConflictsSchemaChangesAndDuplicateKeys()
    {
        var baseline = TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n");
        var implicitConflict = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [baseline]),
            new ContentPack(
                "implicit",
                "1",
                [TsvTable.Parse("items.tsv", "id\tvalue\na\t2\n")],
                ["base"],
                []),
        ]);
        var changedSchema = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [baseline]),
            new ContentPack(
                "schema",
                "1",
                [TsvTable.Parse("items.tsv", "id\tname\na\tchanged\n")],
                ["base"],
                [new ContentTableOverlay("items.tsv", ["id"])]),
        ]);
        var duplicateKeys = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [baseline]),
            new ContentPack(
                "duplicate",
                "1",
                [TsvTable.Parse("items.tsv", "id\tvalue\na\t2\na\t3\n")],
                ["base"],
                [new ContentTableOverlay("items.tsv", ["id"])]),
        ]);

        Assert.Contains(
            "no overlay declaration",
            Assert.Throws<InvalidDataException>(() =>
                ComposedContent.Compose(implicitConflict)).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "same ordered columns",
            Assert.Throws<InvalidDataException>(() =>
                ComposedContent.Compose(changedSchema)).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "duplicate overlay key",
            Assert.Throws<InvalidDataException>(() =>
                ComposedContent.Compose(duplicateKeys)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FailedReloadLeavesPreviouslyComposedContentActive()
    {
        var table = TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n");
        var initialManifest = new ContentManifest(
            "rules",
            "1",
            [new ContentPack("base", "1", [table])]);
        var runtime = new ContentRuntime(initialManifest);
        var original = runtime.Active;
        var invalidManifest = new ContentManifest("rules", "2", [
            new ContentPack("base", "1", [table]),
            new ContentPack(
                "broken",
                "1",
                [TsvTable.Parse("items.tsv", "id\tvalue\na\t2\n")],
                ["base"],
                []),
        ]);

        var failed = runtime.TryReload(invalidManifest);

        Assert.False(failed.Succeeded);
        Assert.Same(original, failed.Active);
        Assert.Same(original, runtime.Active);
        Assert.Equal("1", runtime.Active.GetRequiredTable("items.tsv").Rows[0].Required("value"));
        Assert.Contains("broken", Assert.Single(failed.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulReloadPublishesTheFullyComposedCandidate()
    {
        var baseTable = TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n");
        var runtime = new ContentRuntime(new ContentManifest(
            "rules",
            "1",
            [new ContentPack("base", "1", [baseTable])]));
        var candidateManifest = new ContentManifest("rules", "2", [
            new ContentPack("base", "1", [baseTable]),
            new ContentPack(
                "balance",
                "1",
                [TsvTable.Parse("items.tsv", "id\tvalue\na\t2\n")],
                ["base"],
                [new ContentTableOverlay("items.tsv", ["id"])]),
        ]);

        var result = runtime.TryReload(candidateManifest);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Same(result.Active, runtime.Active);
        Assert.Same(candidateManifest, result.Active.Manifest);
        Assert.Equal("2", result.Active.GetRequiredTable("items.tsv").Rows[0].Required("value"));
    }

    [Fact]
    public void CompositionReportsPackAndTableForAnEmptyOverlayKey()
    {
        var manifest = new ContentManifest("rules", "1", [
            new ContentPack(
                "base",
                "1",
                [TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n")]),
            new ContentPack(
                "broken",
                "1",
                [TsvTable.Parse("items.tsv", "id\tvalue\n\t2\n")],
                ["base"],
                [new ContentTableOverlay("items.tsv", ["id"])]),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            ComposedContent.Compose(manifest));

        Assert.Contains("broken", exception.Message, StringComparison.Ordinal);
        Assert.Contains("items.tsv", exception.Message, StringComparison.Ordinal);
        Assert.Contains("empty key column 'id'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestDirectoryLoaderReadsDependenciesAndOverlayKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"godevilution-overlay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try {
            File.WriteAllText(
                Path.Combine(root, ContentPackLoader.ManifestFileName),
                """
                {
                  "formatVersion": 1,
                  "id": "balance",
                  "version": "1.2.0",
                  "dependencies": ["base"],
                  "schema": { "id": "devilution.diablo", "version": "0.1.0" },
                  "dependencyConstraints": [
                    {
                      "id": "base",
                      "minimumVersion": "1.0.0",
                      "maximumVersion": "1.9.9"
                    }
                  ],
                  "overlays": [
                    { "table": "items.tsv", "keys": ["id"], "mode": "upsert" }
                  ]
                }
                """);
            File.WriteAllText(Path.Combine(root, "items.tsv"), "id\tvalue\na\t2\n");

            var pack = ContentPackLoader.LoadManifestDirectory(root);

            Assert.Equal("balance", pack.Id);
            Assert.Equal("1.2.0", pack.Version);
            Assert.Equal(["base"], pack.Dependencies);
            Assert.Equal(
                new ContentSchemaIdentity("devilution.diablo", "0.1.0"),
                pack.Schema);
            var constraint = Assert.Single(pack.DependencyConstraints);
            Assert.Equal("base", constraint.DependencyId);
            Assert.Equal("1.0.0", constraint.Minimum.ToString());
            Assert.Equal("1.9.9", constraint.Maximum.ToString());
            var overlay = Assert.Single(pack.Overlays);
            Assert.Equal("items.tsv", overlay.TablePath);
            Assert.Equal(["id"], overlay.KeyColumns);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OverlayMetadataChangesIdentityWithoutChangingLegacyGoldenVectors()
    {
        var table = TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n");
        var legacy = new ContentManifest(
            "rules",
            "1",
            [new ContentPack("base", "1", [table])]);
        var withMetadata = new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [table]),
            new ContentPack(
                "overlay",
                "1",
                [TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n")],
                ["base"],
                [new ContentTableOverlay("items.tsv", ["id"])]),
        ]);

        Assert.NotEqual(legacy.Sha256, withMetadata.Sha256);
        Assert.Throws<ArgumentException>(() => new ContentManifest(
            "rules",
            "1",
            [new ContentPack(
                "overlay",
                "1",
                [table],
                ["base"],
                [new ContentTableOverlay("items.tsv", ["id"])])]));
    }

    [Fact]
    public void ManifestEnforcesDependencyVersionsAndOneSchemaFamily()
    {
        var table = TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n");
        var schema = new ContentSchemaIdentity("devilution.diablo", "0.1.0");
        var compatible = new ContentManifest("rules", "1", [
            new ContentPack("base", "1.2.0", [table], [], [], schema),
            new ContentPack(
                "balance",
                "2.0.0",
                [TsvTable.Parse("extra.tsv", "id\nextra\n")],
                ["base"],
                [],
                schema,
                [new ContentPackDependencyConstraint("base", "1.0.0", "1.9.9")]),
        ]);

        Assert.Equal(schema, compatible.Schema);
        var previousAdditiveSchema = new ContentSchemaIdentity("devilution.diablo", "1.433.0");
        var currentSchema = new ContentSchemaIdentity("devilution.diablo", "1.434.0");
        Assert.Throws<ArgumentException>(() => new ContentManifest("rules", "1", [
            new ContentPack("base", "1.2.0", [table], [], [], currentSchema),
            new ContentPack(
                "previous-overlay",
                "1.0.0",
                [TsvTable.Parse("extra.tsv", "id\nextra\n")],
                ["base"],
                [],
                previousAdditiveSchema),
        ]));
        Assert.Throws<ArgumentException>(() => new ContentManifest("rules", "1", [
            new ContentPack("base", "2.0.0", [table], [], [], schema),
            new ContentPack(
                "balance",
                "2.0.0",
                [TsvTable.Parse("extra.tsv", "id\nextra\n")],
                ["base"],
                [],
                schema,
                [new ContentPackDependencyConstraint("base", "1.0.0", "1.9.9")]),
        ]));
        Assert.Throws<ArgumentException>(() => new ContentManifest("rules", "1", [
            new ContentPack("base", "1.2.0", [table], [], [], schema),
            new ContentPack(
                "other-game",
                "1.0.0",
                [TsvTable.Parse("extra.tsv", "id\nextra\n")],
                ["base"],
                [],
                new ContentSchemaIdentity("other.schema", "1.0.0")),
        ]));
    }

    [Fact]
    public void CompatibilityMetadataChangesIdentityButLegacyIdentityRemainsStable()
    {
        var table = TsvTable.Parse("items.tsv", "id\tvalue\na\t1\n");
        var legacy = new ContentManifest(
            "rules",
            "1",
            [new ContentPack("base", "1.2.0", [table])]);
        var versioned = new ContentManifest(
            "rules",
            "1",
            [new ContentPack(
                "base",
                "1.2.0",
                [table],
                [],
                [],
                new ContentSchemaIdentity("devilution.diablo", "0.1.0"))]);

        Assert.NotEqual(legacy.Sha256, versioned.Sha256);
        Assert.Equal("1.2.3", ContentSemanticVersion.Parse("1.2.3").ToString());
        Assert.False(ContentSemanticVersion.TryParse("1.2", out _));
        Assert.Throws<ArgumentException>(() =>
            new ContentPackDependencyConstraint("base", "2.0.0", "1.0.0"));
    }
}
