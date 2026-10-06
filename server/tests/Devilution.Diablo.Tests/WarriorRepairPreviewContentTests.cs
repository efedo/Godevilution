using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Validates the isolated authored Warrior Repair profile through module activation. */
public sealed class WarriorRepairPreviewContentTests
{
    private const uint WarriorClassId = 0;
    private const uint SwordItemId = 100;
    private const uint SwordEquipmentSlot = 4;
    private const uint RepairSpellId = 26;
    private const uint TownLevelId = 0;

    [Fact]
    public void PreviewChangesOnlyStartingSwordDurabilityAndAuthoritativelyRepairsLoadedWarriorTemplate()
    {
        var (basePack, previewPack) = LoadPacks();
        var baseManifest = new ContentManifest("devilution.base", "0.1.0", [basePack]);
        var previewManifest = new ContentManifest("devilution.base", "0.1.0", [basePack, previewPack]);
        Assert.NotEqual(baseManifest.Sha256, previewManifest.Sha256);

        var baseContent = ComposedContent.Compose(baseManifest);
        var previewContent = ComposedContent.Compose(previewManifest);
        Assert.Equal(
            baseContent.GetRequiredTable("content_ids.tsv").ToCanonicalTsv(),
            previewContent.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        Assert.Equal(
            new[] { "items.tsv" },
            baseContent.Tables
                .Where(table => table.ToCanonicalTsv()
                    != previewContent.GetRequiredTable(table.SourcePath).ToCanonicalTsv())
                .Select(table => table.SourcePath)
                .Order());
        foreach (var baseTable in baseContent.Tables.Where(table => table.SourcePath != "items.tsv"))
            Assert.Equal(baseTable.ToCanonicalTsv(),
                previewContent.GetRequiredTable(baseTable.SourcePath).ToCanonicalTsv());
        Assert.Equal(baseContent.Assets.Select(asset => asset.SourcePath).Order(),
            previewContent.Assets.Select(asset => asset.SourcePath).Order());
        foreach (var baseAsset in baseContent.Assets)
            Assert.Equal(baseAsset.Contents.ToArray(),
                previewContent.GetRequiredAsset(baseAsset.SourcePath).Contents.ToArray());

        var baseItems = baseContent.GetRequiredTable("items.tsv");
        var previewItems = previewContent.GetRequiredTable("items.tsv");
        Assert.Equal(baseItems.Rows.Count, previewItems.Rows.Count);
        Assert.Equal(baseItems.Rows.Select(row => row.Required("item_id")).Order(),
            previewItems.Rows.Select(row => row.Required("item_id")).Order());
        foreach (var baseRow in baseItems.Rows.Where(row => row.Required("item_id") != SwordItemId.ToString())) {
            var previewRow = Assert.Single(previewItems.Rows,
                row => row.Required("item_id") == baseRow.Required("item_id"));
            foreach (var column in baseItems.Columns)
                Assert.Equal(baseRow[column], previewRow[column]);
        }
        var baseSwordRow = Assert.Single(baseItems.Rows,
            row => row.Required("item_id") == SwordItemId.ToString());
        var previewSwordRow = Assert.Single(previewItems.Rows,
            row => row.Required("item_id") == SwordItemId.ToString());
        foreach (var column in baseContent.GetRequiredTable("items.tsv").Columns
                     .Except(["durability", "max_durability"]))
            Assert.Equal(baseSwordRow[column], previewSwordRow[column]);
        Assert.Equal("24", baseSwordRow.Required("durability"));
        Assert.Equal("24", baseSwordRow.Required("max_durability"));
        Assert.Equal("12", previewSwordRow.Required("durability"));
        Assert.Equal("24", previewSwordRow.Required("max_durability"));

        var baseActivation = DiabloModule.Instance.ContentBootstrap.Activate(baseContent, randomSeed: 0);
        var previewActivation = DiabloModule.Instance.ContentBootstrap.Activate(previewContent, randomSeed: 0);
        var baseLoadout = baseActivation.PlayerLoadouts.GetRequired(WarriorClassId);
        var previewLoadout = previewActivation.PlayerLoadouts.GetRequired(WarriorClassId);
        Assert.Equal(baseLoadout.ClassId, previewLoadout.ClassId);
        Assert.Equal(baseLoadout.AbilitySpellId, previewLoadout.AbilitySpellId);
        Assert.Equal(baseLoadout.AbilityKind, previewLoadout.AbilityKind);
        Assert.Equal(baseLoadout.StartingGold, previewLoadout.StartingGold);
        Assert.Equal(baseLoadout.StartingSpellId, previewLoadout.StartingSpellId);
        Assert.Equal(baseLoadout.StartingSpellLevel, previewLoadout.StartingSpellLevel);
        Assert.Equal(baseLoadout.Items.ToArray(), previewLoadout.Items.ToArray());
        Assert.Equal(RepairSpellId, previewLoadout.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Repair, previewLoadout.AbilityKind);
        Assert.Equal(9, previewActivation.PlayerClasses.GetRequired(WarriorClassId)
            .RepairAbilityDurabilityLevelOffset);

        var basePlayer = baseActivation.PlayerLoadouts.CreateTemplate(
            "warrior-repair-preview-player", WarriorClassId, null, null, null, TownLevelId)
            .CreatePlayer();
        var previewTemplate = previewActivation.PlayerLoadouts.CreateTemplate(
            "warrior-repair-preview-player", WarriorClassId, null, null, null, TownLevelId);
        var previewPlayer = previewTemplate.CreatePlayer();
        var baseSword = Assert.Single(basePlayer.Equipment, item => item.Slot == SwordEquipmentSlot);
        var damagedSword = Assert.Single(previewPlayer.Equipment, item => item.Slot == SwordEquipmentSlot);
        Assert.Equal(SwordItemId, damagedSword.State.StableItemId);
        Assert.NotEqual(0U, damagedSword.ItemSeed);
        Assert.Equal(24, baseSword.State.Durability);
        Assert.Equal(24, baseSword.State.MaxDurability);
        Assert.Equal(12, damagedSword.State.Durability);
        Assert.Equal(24, damagedSword.State.MaxDurability);
        Assert.Equal((TownLevelId, 1U), (previewPlayer.LevelId, previewPlayer.CharacterLevel));
        Assert.Equal((previewLoadout.AbilityKind, RepairSpellId),
            (previewPlayer.AbilityKind, previewPlayer.AbilitySpellId));

        Assert.Equal(basePlayer.Gold, previewPlayer.Gold);
        Assert.Equal(basePlayer.Experience, previewPlayer.Experience);
        Assert.Equal(basePlayer.Life, previewPlayer.Life);
        Assert.Equal(basePlayer.Mana, previewPlayer.Mana);
        Assert.Equal(basePlayer.Attributes, previewPlayer.Attributes);
        Assert.Equal(basePlayer.Equipment.ToArray(), previewPlayer.Equipment.ToArray()
            .Select(item => item.Slot == 4 ? item with { State = baseSword.State } : item).ToArray());
        Assert.Equal(basePlayer.Belt.ToArray(), previewPlayer.Belt.ToArray());
        Assert.Equal(basePlayer.InventoryGrid.ToArray(), previewPlayer.InventoryGrid.ToArray());
        Assert.Equal(basePlayer.Inventory.ToArray(), previewPlayer.Inventory.ToArray());

        var playerBeforeRejectedTarget = previewTemplate.CreatePlayer();
        var wrongTargetSword = Assert.Single(playerBeforeRejectedTarget.Equipment,
            item => item.Slot == SwordEquipmentSlot);
        var equipmentBeforeRejectedTarget = playerBeforeRejectedTarget.Equipment.ToArray();
        var inventoryBeforeRejectedTarget = playerBeforeRejectedTarget.Inventory.ToArray();
        var beltBeforeRejectedTarget = playerBeforeRejectedTarget.Belt.ToArray();
        var inventoryGridBeforeRejectedTarget = playerBeforeRejectedTarget.InventoryGrid.ToArray();
        var resourcesBeforeRejectedTarget = (
            playerBeforeRejectedTarget.Life,
            playerBeforeRejectedTarget.Mana,
            playerBeforeRejectedTarget.Gold,
            playerBeforeRejectedTarget.Experience);
        var rejectedRandom = new DiabloRandom(0);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(), random: rejectedRandom, repairAbilitySpellId: RepairSpellId);
        var rejected = processor.Execute(
            playerBeforeRejectedTarget,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Equipment,
                    SwordEquipmentSlot,
                    checked(wrongTargetSword.State.StableItemId + 1)),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);
        Assert.False(rejected.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, rejected.Failure);
        Assert.Equal(equipmentBeforeRejectedTarget, playerBeforeRejectedTarget.Equipment);
        Assert.Equal(inventoryBeforeRejectedTarget, playerBeforeRejectedTarget.Inventory);
        Assert.Equal(beltBeforeRejectedTarget, playerBeforeRejectedTarget.Belt);
        Assert.Equal(inventoryGridBeforeRejectedTarget, playerBeforeRejectedTarget.InventoryGrid);
        Assert.Equal(resourcesBeforeRejectedTarget,
            (playerBeforeRejectedTarget.Life, playerBeforeRejectedTarget.Mana,
                playerBeforeRejectedTarget.Gold, playerBeforeRejectedTarget.Experience));
        var rejectedSword = Assert.Single(playerBeforeRejectedTarget.Equipment,
            item => item.Slot == SwordEquipmentSlot);
        Assert.Equal(12, rejectedSword.State.Durability);
        Assert.Equal(24, rejectedSword.State.MaxDurability);
        Assert.Equal(0U, rejectedRandom.State);

        var validRandom = new DiabloRandom(0);
        var validProcessor = new DiabloStoreCommandProcessor(
            new StoreCatalog(), random: validRandom, repairAbilitySpellId: RepairSpellId);
        var resourcesBeforeRepair = (
            previewPlayer.Life, previewPlayer.Mana, previewPlayer.Gold, previewPlayer.Experience);
        var equipmentBeforeRepair = previewPlayer.Equipment.ToArray();
        var inventoryBeforeRepair = previewPlayer.Inventory.ToArray();
        var beltBeforeRepair = previewPlayer.Belt.ToArray();
        var inventoryGridBeforeRepair = previewPlayer.InventoryGrid.ToArray();
        var repair = validProcessor.Execute(
            previewPlayer,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Equipment,
                    SwordEquipmentSlot,
                    damagedSword.State.StableItemId),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);
        Assert.True(repair.Succeeded, repair.Failure?.ToString());
        var repairedSword = Assert.Single(previewPlayer.Equipment, item => item.Slot == SwordEquipmentSlot);
        Assert.Equal(16, repairedSword.State.Durability);
        Assert.Equal(16, repairedSword.State.MaxDurability);
        Assert.Equal(damagedSword with {
            State = damagedSword.State with { Durability = 16, MaxDurability = 16 },
        }, repairedSword);
        Assert.Equal(resourcesBeforeRepair,
            (previewPlayer.Life, previewPlayer.Mana, previewPlayer.Gold, previewPlayer.Experience));
        Assert.Equal(inventoryBeforeRepair, previewPlayer.Inventory);
        Assert.Equal(beltBeforeRepair, previewPlayer.Belt);
        Assert.Equal(inventoryGridBeforeRepair, previewPlayer.InventoryGrid);
        Assert.Equal(equipmentBeforeRepair.Where(item => item.Slot != 4),
            previewPlayer.Equipment.Where(item => item.Slot != 4));
        Assert.Equal(71_484_141U, validRandom.State);
    }

    [Fact]
    public void PreviewRequiresItsExactBaseVersionAndCannotStandAlone()
    {
        var (basePack, previewPack) = LoadPacks();
        Assert.Equal("godevilution.warrior-repair-preview", previewPack.Id);
        Assert.Equal("0.1.0", previewPack.Version);
        Assert.Equal(basePack.Schema, previewPack.Schema);
        Assert.Equal(new[] { "devilution.base" }, previewPack.Dependencies);
        var constraint = Assert.Single(previewPack.DependencyConstraints);
        Assert.Equal("devilution.base", constraint.DependencyId);
        Assert.Equal("0.1.0", constraint.Minimum.ToString());
        Assert.Equal("0.1.0", constraint.Maximum.ToString());
        Assert.Throws<ArgumentException>(() => new ContentManifest("preview", "1", [previewPack]));

        var differentBase = new ContentPack(basePack.Id, "0.1.1", basePack.Tables,
            basePack.Dependencies, basePack.Overlays, basePack.Schema,
            basePack.DependencyConstraints, basePack.Assets);
        Assert.Throws<ArgumentException>(() => new ContentManifest(
            "preview", "1", [differentBase, previewPack]));
    }

    private static (ContentPack Base, ContentPack Preview) LoadPacks()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) {
                var root = Path.Combine(directory.FullName, "server", "content");
                return (
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "base")),
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "warrior-repair-preview")));
            }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository.");
    }
}
