using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Validates the isolated authored Monk Search start through module activation. */
public sealed class MonkSearchPreviewContentTests
{
    private const uint MonkClassId = 3;
    private const uint SearchSpellId = 46;
    private const uint CathedralLevelId = 1;

    [Fact]
    public void PreviewAddsOnlyACathedralStartAndPreservesTheHellfireMonkProfile()
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
            new[] { "level_start_positions.tsv" },
            previewContent.Tables.Select(table => table.SourcePath)
                .Except(baseContent.Tables.Select(table => table.SourcePath), StringComparer.OrdinalIgnoreCase)
                .Order());
        Assert.False(baseContent.TryGetTable("level_start_positions.tsv", out _));
        foreach (var baseTable in baseContent.Tables)
            Assert.Equal(baseTable.ToCanonicalTsv(),
                previewContent.GetRequiredTable(baseTable.SourcePath).ToCanonicalTsv());
        Assert.Equal(
            "level_id\tlevel_symbol\tstart_x\tstart_y\n1\tcathedral\t20\t20\n",
            previewContent.GetRequiredTable("level_start_positions.tsv").ToCanonicalTsv());
        Assert.Equal(baseContent.Assets.Select(asset => asset.SourcePath).Order(),
            previewContent.Assets.Select(asset => asset.SourcePath).Order());
        foreach (var baseAsset in baseContent.Assets)
            Assert.Equal(baseAsset.Contents.ToArray(),
                previewContent.GetRequiredAsset(baseAsset.SourcePath).Contents.ToArray());

        var baseActivation = DiabloModule.Instance.ContentBootstrap.Activate(baseContent, randomSeed: 0);
        var previewActivation = DiabloModule.Instance.ContentBootstrap.Activate(previewContent, randomSeed: 0);
        var baseWorld = Assert.IsType<AuthoritativeWorld>(baseActivation.Simulation.WorldState.World);
        var previewWorld = Assert.IsType<AuthoritativeWorld>(previewActivation.Simulation.WorldState.World);
        var baseStart = baseWorld.GetStartPosition(CathedralLevelId);
        var previewStart = previewWorld.GetStartPosition(CathedralLevelId);
        Assert.Equal((0, 0), baseStart);
        Assert.Equal((20, 20), previewStart);
        Assert.True(previewWorld.IsWalkable(CathedralLevelId, previewStart.X, previewStart.Y));

        foreach (var levelRow in baseContent.GetRequiredTable("levels.tsv").Rows)
        {
            var levelId = uint.Parse(levelRow.Required("level_id"),
                System.Globalization.CultureInfo.InvariantCulture);
            if (levelId != CathedralLevelId)
                Assert.Equal(baseWorld.GetStartPosition(levelId), previewWorld.GetStartPosition(levelId));
        }

        var previewWorldState = previewActivation.Simulation.WorldState;
        bool IsSafeWalkableCell(int x, int y)
            => previewWorld.ContainsPosition(CathedralLevelId, x, y)
                && previewWorld.IsWalkable(CathedralLevelId, x, y)
                && !previewWorldState.CombatTargets.Values.Any(target =>
                    target.LevelId == CathedralLevelId
                    && (target.PositionX == x && target.PositionY == y
                        || target.IsMoving && target.FuturePositionX == x && target.FuturePositionY == y))
                && !previewWorldState.WorldItems.Values.Any(item =>
                    item.LevelId == CathedralLevelId && item.PositionX == x && item.PositionY == y)
                && !previewWorldState.Objects.Values.Any(@object =>
                    @object.LevelId == CathedralLevelId && @object.PositionX == x && @object.PositionY == y)
                && !previewWorld.WarpTargets.Any(warp =>
                    warp.SourceLevelId == CathedralLevelId && warp.TriggerX == x && warp.TriggerY == y);
        Assert.True(IsSafeWalkableCell(previewStart.X, previewStart.Y));

        var baseMonsterRow = Assert.Single(baseContent.GetRequiredTable("monsters.tsv").Rows,
            row => row.Required("entity_id") == "9");
        var previewMonsterRow = Assert.Single(previewContent.GetRequiredTable("monsters.tsv").Rows,
            row => row.Required("entity_id") == "9");
        foreach (var column in baseContent.GetRequiredTable("monsters.tsv").Columns)
            Assert.Equal(baseMonsterRow[column], previewMonsterRow[column]);
        var authoredMonster = Assert.Single(previewWorldState.CombatTargets.Values,
            target => target.EntityId == 9);
        Assert.Equal((1U, CathedralLevelId, 1, 0, 6),
            (authoredMonster.MonsterId, authoredMonster.LevelId,
                authoredMonster.PositionX, authoredMonster.PositionY, authoredMonster.AggroRange));
        Assert.True(ChebyshevDistance(previewStart.X, previewStart.Y,
            authoredMonster.PositionX, authoredMonster.PositionY) > authoredMonster.AggroRange);
        const int nextX = 21;
        const int nextY = 20;
        Assert.Equal(1, ChebyshevDistance(previewStart.X, previewStart.Y, nextX, nextY));
        Assert.True(IsSafeWalkableCell(nextX, nextY));
        Assert.True(ChebyshevDistance(nextX, nextY,
            authoredMonster.PositionX, authoredMonster.PositionY) > authoredMonster.AggroRange);

        var baseLoadout = baseActivation.PlayerLoadouts.GetRequired(MonkClassId);
        var previewLoadout = previewActivation.PlayerLoadouts.GetRequired(MonkClassId);
        Assert.Equal(baseLoadout.ClassId, previewLoadout.ClassId);
        Assert.Equal(baseLoadout.AbilitySpellId, previewLoadout.AbilitySpellId);
        Assert.Equal(baseLoadout.AbilityKind, previewLoadout.AbilityKind);
        Assert.Equal(baseLoadout.StartingGold, previewLoadout.StartingGold);
        Assert.Equal(baseLoadout.StartingSpellId, previewLoadout.StartingSpellId);
        Assert.Equal(baseLoadout.StartingSpellLevel, previewLoadout.StartingSpellLevel);
        Assert.Equal(baseLoadout.Items.ToArray(), previewLoadout.Items.ToArray());
        Assert.Equal(SearchSpellId, previewLoadout.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Search, previewLoadout.AbilityKind);
        Assert.Equal(0U, previewLoadout.StartingSpellId);
        Assert.Equal(0U, previewLoadout.StartingSpellLevel);

        var monkRow = Assert.Single(previewContent.GetRequiredTable("classes.tsv").Rows,
            row => row.Required("class_id") == MonkClassId.ToString());
        Assert.Equal("Hellfire", monkRow.Required("availability"));

        var basePlayer = baseActivation.PlayerLoadouts.CreateTemplate(
            "monk-search-preview-player", MonkClassId, null, null, null, CathedralLevelId,
            startingPositionX: baseStart.X, startingPositionY: baseStart.Y).CreatePlayer();
        var previewPlayer = previewActivation.PlayerLoadouts.CreateTemplate(
            "monk-search-preview-player", MonkClassId, null, null, null, CathedralLevelId,
            startingPositionX: previewStart.X, startingPositionY: previewStart.Y).CreatePlayer();
        Assert.Equal(basePlayer.CharacterClassId, previewPlayer.CharacterClassId);
        Assert.Equal(basePlayer.LevelId, previewPlayer.LevelId);
        Assert.Equal(basePlayer.CharacterLevel, previewPlayer.CharacterLevel);
        Assert.Equal((baseStart.X, baseStart.Y), (basePlayer.PositionX, basePlayer.PositionY));
        Assert.Equal((previewStart.X, previewStart.Y), (previewPlayer.PositionX, previewPlayer.PositionY));
        Assert.Equal((CathedralLevelId, 1U), (previewPlayer.LevelId, previewPlayer.CharacterLevel));
        Assert.Equal((DiabloPlayerAbilityKind.Search, SearchSpellId),
            (previewPlayer.AbilityKind, previewPlayer.AbilitySpellId));
        Assert.Equal(basePlayer.Gold, previewPlayer.Gold);
        Assert.Equal(basePlayer.Experience, previewPlayer.Experience);
        Assert.Equal(basePlayer.Life, previewPlayer.Life);
        Assert.Equal(basePlayer.Mana, previewPlayer.Mana);
        Assert.Equal(basePlayer.Attributes, previewPlayer.Attributes);
        Assert.Equal(basePlayer.Equipment.ToArray(), previewPlayer.Equipment.ToArray());
        foreach (var player in new[] { basePlayer, previewPlayer })
        {
            Assert.DoesNotContain(player.MemorizedSpells, spell => spell.SpellId == SearchSpellId);
            Assert.Equal(0, player.Equipment
                .Where(item => item.State.StatFlag
                    && (item.State.Magical == 0 || item.State.Identified))
                .Sum(item => item.State.SpellLevelAdd));
        }
        Assert.Equal(basePlayer.Belt.ToArray(), previewPlayer.Belt.ToArray());
        Assert.Equal(basePlayer.InventoryGrid.ToArray(), previewPlayer.InventoryGrid.ToArray());
        Assert.Equal(basePlayer.Inventory.ToArray(), previewPlayer.Inventory.ToArray());
    }

    [Fact]
    public void PreviewRequiresItsExactBaseVersionAndCannotStandAlone()
    {
        var (basePack, previewPack) = LoadPacks();
        Assert.Equal("devilution.base", basePack.Id);
        Assert.Equal("0.1.0", basePack.Version);
        Assert.Equal("godevilution.monk-search-preview", previewPack.Id);
        Assert.Equal("0.1.0", previewPack.Version);
        Assert.Equal(basePack.Schema, previewPack.Schema);
        Assert.Equal("devilution.diablo", previewPack.Schema?.Id);
        Assert.Equal("1.502.0", previewPack.Schema?.Version.ToString());
        var overlay = Assert.Single(previewPack.Overlays);
        Assert.Equal("level_start_positions.tsv", overlay.TablePath);
        Assert.Equal(new[] { "level_id" }, overlay.KeyColumns);
        Assert.Equal(ContentTableOverlayMode.Upsert, overlay.Mode);
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
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                var root = Path.Combine(directory.FullName, "server", "content");
                return (
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "base")),
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "monk-search-preview")));
            }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository.");
    }

    private static int ChebyshevDistance(int firstX, int firstY, int secondX, int secondY)
        => Math.Max(Math.Abs(firstX - secondX), Math.Abs(firstY - secondY));
}