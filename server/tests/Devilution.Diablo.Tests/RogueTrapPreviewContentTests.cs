using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Validates the isolated authored Rogue trap profile through module activation. */
public sealed class RogueTrapPreviewContentTests
{
    private const uint RogueClassId = 1;
    private const uint TrapEntityId = 2001;
    private const uint CathedralLevelId = 1;
    private const int TrapX = 20;
    private const int TrapY = 22;

    [Fact]
    public void PreviewAddsReachableTrapAndProfileStartWhilePreservingBaseLoadoutResourcesAndOtherWorldContent()
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
            new[] { "objects.tsv" },
            baseContent.Tables
                .Where(table => table.ToCanonicalTsv()
                    != previewContent.GetRequiredTable(table.SourcePath).ToCanonicalTsv())
                .Select(table => table.SourcePath)
                .Order());
        Assert.Equal(
            new[] { "level_start_positions.tsv" },
            previewContent.Tables.Select(table => table.SourcePath)
                .Except(baseContent.Tables.Select(table => table.SourcePath), StringComparer.OrdinalIgnoreCase)
                .Order());
        Assert.False(baseContent.TryGetTable("level_start_positions.tsv", out _));
        foreach (var baseTable in baseContent.Tables.Where(table => table.SourcePath != "objects.tsv"))
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

        var baseObjects = baseContent.GetRequiredTable("objects.tsv");
        var previewObjects = previewContent.GetRequiredTable("objects.tsv");
        Assert.Equal(baseObjects.Rows.Count, previewObjects.Rows.Count);
        foreach (var baseRow in baseObjects.Rows.Where(row => row.Required("entity_id") != TrapEntityId.ToString())) {
            var previewRow = Assert.Single(previewObjects.Rows,
                row => row.Required("entity_id") == baseRow.Required("entity_id"));
            foreach (var column in baseObjects.Columns)
                Assert.Equal(baseRow[column], previewRow[column]);
        }

        var baseTrapRow = Assert.Single(baseObjects.Rows,
            row => row.Required("entity_id") == TrapEntityId.ToString());
        var previewTrapRow = Assert.Single(previewObjects.Rows,
            row => row.Required("entity_id") == TrapEntityId.ToString());
        foreach (var column in baseObjects.Columns.Except(["position_x", "position_y", "trap_armed"]))
            Assert.Equal(baseTrapRow[column], previewTrapRow[column]);
        Assert.Equal("20", previewTrapRow.Required("position_x"));
        Assert.Equal("22", previewTrapRow.Required("position_y"));
        Assert.Equal("1", previewTrapRow.Required("trap_armed"));

        var baseActivation = DiabloModule.Instance.ContentBootstrap.Activate(baseContent, randomSeed: 0);
        var previewActivation = DiabloModule.Instance.ContentBootstrap.Activate(previewContent, randomSeed: 0);
        var baseWorld = Assert.IsType<AuthoritativeWorld>(baseActivation.Simulation.WorldState.World);
        var previewWorld = Assert.IsType<AuthoritativeWorld>(previewActivation.Simulation.WorldState.World);
        var start = previewWorld.GetStartPosition(CathedralLevelId);
        var baseStart = baseWorld.GetStartPosition(CathedralLevelId);
        Assert.Equal((20, 20), start);
        Assert.Equal((0, 0), baseStart);
        Assert.NotEqual(baseStart, start);
        foreach (var levelRow in baseContent.GetRequiredTable("levels.tsv").Rows) {
            var levelId = uint.Parse(levelRow.Required("level_id"),
                System.Globalization.CultureInfo.InvariantCulture);
            if (levelId != CathedralLevelId)
                Assert.Equal(baseWorld.GetStartPosition(levelId), previewWorld.GetStartPosition(levelId));
        }
        Assert.True(previewWorld.IsWalkable(CathedralLevelId, start.X, start.Y));
        Assert.True(previewWorld.IsWalkable(CathedralLevelId, TrapX, TrapY));
        Assert.Equal(2, ChebyshevDistance(start.X, start.Y, TrapX, TrapY));

        var worldState = previewActivation.Simulation.WorldState;
        bool IsSafeWalkableCell(int x, int y)
            => previewWorld.ContainsPosition(CathedralLevelId, x, y)
                && previewWorld.IsWalkable(CathedralLevelId, x, y)
                && !worldState.CombatTargets.Values.Any(target =>
                    target.LevelId == CathedralLevelId
                    && (target.PositionX == x && target.PositionY == y
                        || target.IsMoving && target.FuturePositionX == x && target.FuturePositionY == y))
                && !worldState.WorldItems.Values.Any(item =>
                    item.LevelId == CathedralLevelId && item.PositionX == x && item.PositionY == y)
                && !worldState.Objects.Values.Any(@object =>
                    @object.EntityId != TrapEntityId && @object.LevelId == CathedralLevelId
                    && @object.PositionX == x && @object.PositionY == y)
                && !previewWorld.WarpTargets.Any(warp =>
                    warp.SourceLevelId == CathedralLevelId && warp.TriggerX == x && warp.TriggerY == y);

        Assert.True(IsSafeWalkableCell(start.X, start.Y));
        var trapAdjacentCells = (
            from x in Enumerable.Range(TrapX - 1, 3)
            from y in Enumerable.Range(TrapY - 1, 3)
            where (x, y) != (TrapX, TrapY)
            select (X: x, Y: y)).ToArray();
        Assert.Equal(8, trapAdjacentCells.Length);
        Assert.All(trapAdjacentCells, cell =>
            Assert.True(IsSafeWalkableCell(cell.X, cell.Y),
                $"Trap-adjacent cell ({cell.X},{cell.Y}) must be walkable and unoccupied."));
        Assert.True(IsSafeWalkableCell(20, 21));
        Assert.True(IsSafeWalkableCell(21, 21));
        foreach (var approach in new[] { (X: 20, Y: 21), (X: 21, Y: 21) }) {
            Assert.Equal(1, ChebyshevDistance(start.X, start.Y, approach.X, approach.Y));
            Assert.Equal(1, ChebyshevDistance(approach.X, approach.Y, TrapX, TrapY));
            Assert.Contains(new[] { (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0) },
                step => IsSafeWalkableCell(approach.X + step.X, approach.Y + step.Y));
        }

        Assert.Equal(1, ChebyshevDistance(20, 21, start.X, start.Y));
        Assert.True(IsSafeWalkableCell(20, 21) && IsSafeWalkableCell(start.X, start.Y));

        var baseMonsterRow = Assert.Single(baseContent.GetRequiredTable("monsters.tsv").Rows,
            row => row.Required("entity_id") == "9");
        var previewMonsterRow = Assert.Single(previewContent.GetRequiredTable("monsters.tsv").Rows,
            row => row.Required("entity_id") == "9");
        foreach (var column in baseContent.GetRequiredTable("monsters.tsv").Columns)
            Assert.Equal(baseMonsterRow[column], previewMonsterRow[column]);
        var authoredMonster = Assert.Single(worldState.CombatTargets.Values,
            target => target.EntityId == 9);
        Assert.Equal((1U, CathedralLevelId, 1, 0, 6),
            (authoredMonster.MonsterId, authoredMonster.LevelId,
                authoredMonster.PositionX, authoredMonster.PositionY, authoredMonster.AggroRange));
        Assert.True(ChebyshevDistance(start.X, start.Y,
            authoredMonster.PositionX, authoredMonster.PositionY) > authoredMonster.AggroRange);

        var baseLoadout = baseActivation.PlayerLoadouts.GetRequired(RogueClassId);
        var previewLoadout = previewActivation.PlayerLoadouts.GetRequired(RogueClassId);
        Assert.Equal(baseLoadout.ClassId, previewLoadout.ClassId);
        Assert.Equal(baseLoadout.AbilitySpellId, previewLoadout.AbilitySpellId);
        Assert.Equal(baseLoadout.AbilityKind, previewLoadout.AbilityKind);
        Assert.Equal(baseLoadout.Items.ToArray(), previewLoadout.Items.ToArray());
        Assert.Equal(28U, previewLoadout.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.TrapDisarm, previewLoadout.AbilityKind);

        var basePlayer = baseActivation.PlayerLoadouts.CreateTemplate(
            "rogue-trap-preview-player", RogueClassId, null, null, null, CathedralLevelId,
            startingPositionX: baseStart.X, startingPositionY: baseStart.Y).CreatePlayer();
        var previewPlayer = previewActivation.PlayerLoadouts.CreateTemplate(
            "rogue-trap-preview-player", RogueClassId, null, null, null, CathedralLevelId,
            startingPositionX: start.X, startingPositionY: start.Y).CreatePlayer();
        Assert.Equal(basePlayer.CharacterClassId, previewPlayer.CharacterClassId);
        Assert.Equal(basePlayer.LevelId, previewPlayer.LevelId);
        Assert.Equal((baseStart.X, baseStart.Y), (basePlayer.PositionX, basePlayer.PositionY));
        Assert.Equal((start.X, start.Y), (previewPlayer.PositionX, previewPlayer.PositionY));
        Assert.Equal(basePlayer.Gold, previewPlayer.Gold);
        Assert.Equal(basePlayer.Experience, previewPlayer.Experience);
        Assert.Equal(basePlayer.Life, previewPlayer.Life);
        Assert.Equal(basePlayer.Mana, previewPlayer.Mana);
        Assert.Equal(basePlayer.Attributes, previewPlayer.Attributes);
        Assert.Equal(basePlayer.Equipment.ToArray(), previewPlayer.Equipment.ToArray());
        Assert.Equal(basePlayer.Belt.ToArray(), previewPlayer.Belt.ToArray());
        Assert.Equal(basePlayer.InventoryGrid.ToArray(), previewPlayer.InventoryGrid.ToArray());
        Assert.Equal(basePlayer.Inventory.ToArray(), previewPlayer.Inventory.ToArray());

        Assert.Equal(DiabloPlayerAbilityKind.TrapDisarm, previewPlayer.AbilityKind);
        Assert.Equal(28U, previewPlayer.AbilitySpellId);
        var projectedTrap = Assert.Single(
            previewActivation.Simulation.CapturePresentationSnapshot(previewPlayer, 0).Objects,
            @object => @object.EntityId == TrapEntityId);
        Assert.Equal((TrapEntityId, 1U, CathedralLevelId, TrapX, TrapY),
            (projectedTrap.EntityId, projectedTrap.ObjectId, projectedTrap.LevelId,
                projectedTrap.PositionX, projectedTrap.PositionY));
        Assert.True(projectedTrap.TrapArmed);
        Assert.False(projectedTrap.Activated);
        Assert.Equal(AuthoritativeObjectEffectKind.None, projectedTrap.EffectKind);
    }

    [Fact]
    public void PreviewRequiresItsExactBaseVersionAndCannotStandAlone()
    {
        var (basePack, previewPack) = LoadPacks();
        Assert.Equal("godevilution.rogue-trap-preview", previewPack.Id);
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
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "rogue-trap-preview")));
            }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository.");
    }

    private static int ChebyshevDistance(int firstX, int firstY, int secondX, int secondY)
        => Math.Max(Math.Abs(firstX - secondX), Math.Abs(firstY - secondY));
}
