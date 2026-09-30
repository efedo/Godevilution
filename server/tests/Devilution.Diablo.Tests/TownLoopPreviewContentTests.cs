using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Optional authored playthrough content must not mutate the compatible base pack. */
public sealed class TownLoopPreviewContentTests
{
    [Fact]
    public void PreviewChangesOnlyAuthoredLoopContentUnderADistinctIdentity()
    {
        var (basePack, preview, _) = LoadPacks();
        var baseManifest = new ContentManifest("devilution.base", "0.1.0", [basePack]);
        var previewManifest = new ContentManifest("devilution.base", "0.1.0", [basePack, preview]);
        Assert.Equal("8ca8a6d80722c06439ad6cf62efc75d1e486971669c0509f6a03e6438cd3fd1e", baseManifest.Sha256);
        Assert.NotEqual(baseManifest.Sha256, previewManifest.Sha256);
        var original = ComposedContent.Compose(baseManifest);
        var composed = ComposedContent.Compose(previewManifest);
        Assert.Equal("5d88aa59bf6a3759c082f0d14a1553846de0f9c04d26a23785791fc17f1f6d6f", previewManifest.Sha256);
        Assert.Equal(new[] { "content_ids.tsv", "monsters.tsv", "warps.tsv" },
            original.Tables.Where(table => table.ToCanonicalTsv() != composed.GetRequiredTable(table.SourcePath).ToCanonicalTsv())
                .Select(table => table.SourcePath).Order());
        Assert.Equal(original.Assets, composed.Assets);
        Assert.Equal(original.GetRequiredTable("warps.tsv").Rows.Count + 1, composed.GetRequiredTable("warps.tsv").Rows.Count);
        var originalMonster = original.GetRequiredTable("monsters.tsv").Rows.Single(row => row.Required("entity_id") == "9");
        var previewMonster = composed.GetRequiredTable("monsters.tsv").Rows.Single(row => row.Required("entity_id") == "9");
        foreach (var column in original.GetRequiredTable("monsters.tsv").Columns.Except(["drop_item_id", "drop_item_symbol"]))
            Assert.Equal(originalMonster.Required(column), previewMonster.Required(column));
        Assert.Equal("1", originalMonster.Required("drop_item_id"));
        Assert.Equal("2160", previewMonster.Required("drop_item_id"));
        Assert.Equal(original.GetRequiredTable("stores.tsv").ToCanonicalTsv(), composed.GetRequiredTable("stores.tsv").ToCanonicalTsv());
        Assert.Equal(baseManifest.Sha256, new ContentManifest("devilution.base", "0.1.0", [basePack]).Sha256);
    }

    [Fact]
    public void PortalPreviewAddsOnlyValidatedStockUnderASeparateIdentity()
    {
        var (basePack, preview, portal) = LoadPacks();
        var originalManifest = new ContentManifest("devilution.base", "0.1.0", [basePack, preview]);
        var portalManifest = new ContentManifest("devilution.base", "0.1.0", [basePack, preview, portal]);
        var original = ComposedContent.Compose(originalManifest);
        var composed = ComposedContent.Compose(portalManifest);
        Assert.NotEqual(originalManifest.Sha256, portalManifest.Sha256);
        Assert.Equal("godevilution.town-portal-preview", portal.Id);
        Assert.Equal(new[] { "stores.tsv" }, original.Tables
            .Where(table => table.ToCanonicalTsv() != composed.GetRequiredTable(table.SourcePath).ToCanonicalTsv())
            .Select(table => table.SourcePath));
        var originalRows = original.GetRequiredTable("stores.tsv").Rows;
        var newRows = composed.GetRequiredTable("stores.tsv").Rows;
        Assert.Equal(originalRows.Count + 1, newRows.Count);
        foreach (var originalRow in originalRows) {
            var retainedRow = newRows.Single(row => row.Required("store_id") == originalRow.Required("store_id")
                && row.Required("store_slot") == originalRow.Required("store_slot"));
            foreach (var column in original.GetRequiredTable("stores.tsv").Columns)
                Assert.Equal(originalRow.Required(column), retainedRow.Required(column));
        }
        var stock = newRows.Single(row => row.Required("store_id") == "1" && row.Required("store_slot") == "2");
        Assert.Equal("Smith", stock.Required("store_role"));
        Assert.Equal("9100", stock.Required("item_seed"));
        Assert.Equal("2119", stock.Required("item_id"));
        Assert.Equal("scroll_of_town_portal", stock.Required("item_symbol"));
        Assert.Equal("50", stock.Required("price"));
        Assert.Throws<ArgumentException>(() => new ContentManifest("portal", "1", [portal]));
        Assert.Throws<ArgumentException>(() => new ContentManifest("portal", "1", [basePack, portal]));
        var differentPreview = new ContentPack(preview.Id, "0.1.1", preview.Tables, preview.Dependencies,
            preview.Overlays, preview.Schema, preview.DependencyConstraints, preview.Assets);
        Assert.Throws<ArgumentException>(() => new ContentManifest("portal", "1", [basePack, differentPreview, portal]));
        DiabloModule.Instance.ContentBootstrap.Activate(composed);
    }

    [Fact]
    public void ComposedGeometryAllowsTheReturnWithoutAnImmediateForwardTrigger()
    {
        var activation = Activate();
        var world = activation.Simulation.WorldState.World!;
        var reverse = Assert.Single(world.WarpTargets, warp => warp.WarpId == 1017);
        Assert.Equal((1U, 4, 1, 0U, 57, 39), (reverse.SourceLevelId, reverse.TriggerX, reverse.TriggerY,
            reverse.TargetLevelId, reverse.TargetX, reverse.TargetY));
        for (var x = 1; x <= 4; x++)
            Assert.True(world.IsWalkable(1, x, 1));
        Assert.True(world.IsWalkable(0, 57, 39));
        Assert.DoesNotContain(world.WarpTargets, warp => warp.SourceLevelId == 0 && warp.TriggerX == 57 && warp.TriggerY == 39);
        var runtime = activation.Simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate("preview", 1, null, null, null, 1));
        runtime.Read("preview", player => {
            // Unit-level trigger setup; application acceptance separately walks here.
            player.PositionX = 4;
            player.PositionY = 1;
            var processor = new DiabloWorldCommandProcessor(activation.Simulation.WorldState);
            Assert.True(processor.ExecuteLevelTrigger(player, 1, [player]).Succeeded);
            Assert.Equal((0U, 57, 39), (player.LevelId, player.PositionX, player.PositionY));
            processor.ExecuteLevelTrigger(player, 2, [player]);
            Assert.Equal((0U, 57, 39), (player.LevelId, player.PositionX, player.PositionY));
            return true;
        });
    }

    [Fact]
    public void LootIsEquippableWithoutGrantingExtraStartingResources()
    {
        var activation = Activate();
        var loot = Assert.IsType<AuthoritativeWorldItem>(activation.Simulation.WorldState.CombatTargets[9].Drop);
        Assert.Equal(2160U, loot.State.StableItemId);
        Assert.Equal(119, loot.State.ItemIndex);
        Assert.Equal(6001U, loot.ItemSeed);
        Assert.Equal(25U, loot.Price);
        Assert.Equal(DiabloEquipmentSlotKind.Hand, AuthoritativeItemClassification.GetEquipmentSlotKind(loot.State, activation.Simulation.Items));
        var (basePack, _, _) = LoadPacks();
        var original = DiabloModule.Instance.ContentBootstrap.Activate(ComposedContent.Compose(new ContentManifest("base", "1", [basePack])));
        var expected = original.PlayerLoadouts.CreateTemplate("preview", 1, null, null, null, 0);
        var player = activation.PlayerLoadouts.CreateTemplate("preview", 1, null, null, null, 0);
        Assert.Equal(100U, player.Gold);
        Assert.Equal(expected.Equipment, player.Equipment);
        Assert.Equal(expected.Belt, player.Belt);
        Assert.Equal(expected.Inventory, player.Inventory);
        Assert.Equal(expected.MemorizedSpells, player.MemorizedSpells);
    }

    [Fact]
    public void PreviewRequiresItsExactBaseVersionAndCannotStandAlone()
    {
        var (basePack, preview, _) = LoadPacks();
        Assert.Equal("godevilution.town-loop-preview", preview.Id);
        Assert.Equal(basePack.Schema, preview.Schema);
        Assert.Throws<ArgumentException>(() => new ContentManifest("preview", "1", [preview]));
        var differentBase = new ContentPack(basePack.Id, "0.1.1", basePack.Tables, basePack.Dependencies,
            basePack.Overlays, basePack.Schema, basePack.DependencyConstraints, basePack.Assets);
        Assert.Throws<ArgumentException>(() => new ContentManifest("preview", "1", [differentBase, preview]));
    }

    private static DiabloContentActivation Activate()
    {
        var (basePack, preview, _) = LoadPacks();
        return DiabloModule.Instance.ContentBootstrap.Activate(ComposedContent.Compose(new ContentManifest("preview", "1", [basePack, preview])));
    }

    private static (ContentPack Base, ContentPack Preview, ContentPack Portal) LoadPacks()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) {
                var root = Path.Combine(directory.FullName, "server", "content");
                return (ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "base")),
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "town-loop-preview")),
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "town-portal-preview")));
            }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository.");
    }
}
