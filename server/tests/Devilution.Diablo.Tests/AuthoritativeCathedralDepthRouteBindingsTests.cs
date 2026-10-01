using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralDepthRouteBindingsTests
{
	[Fact]
	public void PrepareStagesAllFourDepthsAndResolveRequiresTheNativeStaircase()
	{
		var fixture = CreateFixture();
		var plan = fixture.Catalog.Prepare(fixture.World, fixture.Geometry);
		Assert.Equal(new[] { 1, 2, 3, 4 }, plan.Candidates.Select(candidate => candidate.Identity.Depth));
		Assert.Same(fixture.OriginalLevels[0], fixture.World.Levels.Single(level => level.LevelId == 1));

		var resolution = AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			plan,
			fixture.Warps,
			[],
			fixture.Bindings);
		Assert.Equal(8, resolution.Warps.Count);
		var first = plan.Candidates[0];
		Assert.Equal(first.Routes.MainEntry, new AuthoritativeCathedralPoint(
			resolution.Warps.Single(warp => warp.WarpId == 100).TargetX,
			resolution.Warps.Single(warp => warp.WarpId == 100).TargetY));
		Assert.Equal(first.Routes.PreviousEntry, new AuthoritativeCathedralPoint(
			resolution.Warps.Single(warp => warp.WarpId == 103).TargetX,
			resolution.Warps.Single(warp => warp.WarpId == 103).TargetY));

		plan.Apply(fixture.World);
		Assert.Same(plan.Candidates[3].Level, fixture.World.Levels.Single(level => level.LevelId == 4));
	}

	[Fact]
	public void ResolveRejectsMissingExtraConditionalPortalAndDanglingStairBindings()
	{
		var fixture = CreateFixture();
		var plan = fixture.Catalog.Prepare(fixture.World, fixture.Geometry);

		var missing = fixture.Warps.Where(warp => warp.WarpId != 106).ToArray();
		Assert.Throws<InvalidDataException>(() => AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			plan,
			missing,
			[],
			fixture.Bindings));

		var conditional = fixture.Warps.Select(warp => warp.WarpId == 106
			? new AuthoritativeWarpTarget(
				warp.WarpId,
				warp.SourceLevelId,
				warp.TriggerX,
				warp.TriggerY,
				warp.TargetLevelId,
				warp.TargetX,
				warp.TargetY,
				requiredQuestId: 99,
				requiredQuestCompleted: true) {
				StableSymbol = warp.StableSymbol,
			}
			: warp).ToArray();
		Assert.Throws<InvalidDataException>(() => AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			plan,
			conditional,
			[],
			fixture.Bindings));

		var extraPortal = new AuthoritativePortal(
			900,
			1,
			1,
			1,
			0,
			1,
			1) { StableSymbol = "extra_portal" };
		var withPortalIds = new StableContentIdCatalog(fixture.StableIds.Entries.Append(
			new StableContentId("portal", "extra_portal", 900)));
		var withPortalTable = fixture.BindingTable +
			"portal\t900\textra_portal\tsource\t1\tcathedral1\tup_trigger\n" +
			"portal\t900\textra_portal\tdestination\t0\ttown\ttown_cathedral_return\n";
		var withPortalBindings = AuthoritativeNativeGeneratedRouteBindings.Load(withPortalTable, withPortalIds);
		Assert.Throws<InvalidDataException>(() => AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			plan,
			fixture.Warps,
			[extraPortal],
			withPortalBindings));

		var danglingTable = fixture.BindingTable +
			"warp\t999\tdangling\tsource\t0\ttown\ttown_cathedral_trigger\n";
		var danglingIds = new StableContentIdCatalog(fixture.StableIds.Entries.Append(
			new StableContentId("warp", "dangling", 999)));
		var dangling = AuthoritativeNativeGeneratedRouteBindings.Load(danglingTable, danglingIds);
		Assert.Throws<InvalidDataException>(() => AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			plan,
			fixture.Warps,
			[],
			dangling));
	}

	[Fact]
	public void ApplyRejectsStaleWorldBeforeReplacingAnyDepth()
	{
		var fixture = CreateFixture();
		var plan = fixture.Catalog.Prepare(fixture.World, fixture.Geometry);
		var original = fixture.OriginalLevels.ToArray();
		var replacement = new AuthoritativeLevel(3, 4, 4) { StableSymbol = "cathedral3" };
		fixture.World.ReplaceLevel(replacement);

		Assert.Throws<InvalidOperationException>(() => plan.Apply(fixture.World));
		Assert.Same(original[0], fixture.World.Levels.Single(level => level.LevelId == 1));
		Assert.Same(replacement, fixture.World.Levels.Single(level => level.LevelId == 3));
	}

	private static Fixture CreateFixture()
	{
		const uint seed = 17;
		var stableIds = new StableContentIdCatalog(
			new[] {
				new StableContentId("level", "town", 0),
			}
			.Concat(Enumerable.Range(1, 4).Select(depth =>
				new StableContentId("level", $"cathedral{depth}", (uint)depth)))
			.Concat(Enumerable.Range(100, 8).Select(id =>
				new StableContentId("warp", $"stair{id}", (uint)id))));
		var world = new AuthoritativeWorld();
		var town = new AuthoritativeLevel(0, 112, 112) { StableSymbol = "town" };
		world.AddLevel(town);
		var originals = Enumerable.Range(1, 4).Select(depth =>
			new AuthoritativeLevel(
				(uint)depth,
				4,
				4,
				isDefaultStart: depth == 1) { StableSymbol = $"cathedral{depth}" }).ToArray();
		foreach (var original in originals)
			world.AddLevel(original);

		var table = "level_id\tlevel_symbol\tdepth\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\tpolicy\n"
			+ string.Concat(Enumerable.Range(1, 4).Select(depth =>
				$"{depth}\tcathedral{depth}\t{depth}\t{AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId}\t1\t{seed}\tmain\tquest-disabled-original-diablo\n"));
		var catalog = AuthoritativeCathedralDepthRuntimeCatalog.Load(table, stableIds);
		var plan = catalog.Prepare(world, CreateGeometry());
		var warps = new List<AuthoritativeWarpTarget>();
		AddWarp(warps, 100, 0, 1, "stair100");
		AddWarp(warps, 101, 1, 0, "stair101");
		AddWarp(warps, 102, 1, 2, "stair102");
		AddWarp(warps, 103, 2, 1, "stair103");
		AddWarp(warps, 104, 2, 3, "stair104");
		AddWarp(warps, 105, 3, 2, "stair105");
		AddWarp(warps, 106, 3, 4, "stair106");
		AddWarp(warps, 107, 4, 3, "stair107");

		var bindingRows = new List<string> {
			"route_kind\troute_id\troute_symbol\tendpoint\tlevel_id\tlevel_symbol\tanchor\n",
		};
		AddBinding(bindingRows, 100, "stair100", "source", 0, "town", "town_cathedral_trigger");
		AddBinding(bindingRows, 100, "stair100", "destination", 1, "cathedral1", "main_entry");
		AddBinding(bindingRows, 101, "stair101", "source", 1, "cathedral1", "up_trigger");
		AddBinding(bindingRows, 101, "stair101", "destination", 0, "town", "town_cathedral_return");
		for (var depth = 1; depth < 4; depth++) {
			var downId = (uint)(100 + depth * 2);
			var upId = downId + 1;
			AddBinding(bindingRows, downId, $"stair{downId}", "source", (uint)depth, $"cathedral{depth}", "down_trigger");
			AddBinding(bindingRows, downId, $"stair{downId}", "destination", (uint)depth + 1, $"cathedral{depth + 1}", "main_entry");
			AddBinding(bindingRows, upId, $"stair{upId}", "source", (uint)depth + 1, $"cathedral{depth + 1}", "up_trigger");
			AddBinding(bindingRows, upId, $"stair{upId}", "destination", (uint)depth, $"cathedral{depth}", "previous_entry");
		}
		var bindings = AuthoritativeNativeGeneratedRouteBindings.Load(string.Concat(bindingRows), stableIds);
		return new Fixture(table, stableIds, world, town, originals, catalog, CreateGeometry(), plan, warps, bindings, string.Concat(bindingRows));
	}

	private static void AddWarp(List<AuthoritativeWarpTarget> warps, uint id, uint source, uint target, string symbol)
		=> warps.Add(new AuthoritativeWarpTarget(id, source, 1, 1, target, 1, 1) { StableSymbol = symbol });

	private static void AddBinding(List<string> rows, uint id, string symbol, string endpoint, uint levelId, string levelSymbol, string anchor)
		=> rows.Add($"warp\t{id}\t{symbol}\t{endpoint}\t{levelId}\t{levelSymbol}\t{anchor}\n");

	private static AuthoritativeNativeDunGeometryCatalog CreateGeometry()
	{
		var generated = Enumerable.Range(1, 4)
			.Select(depth => AuthoritativeCathedralDepthGenerator.Generate(depth, 17, AuthoritativeCathedralEntry.Main))
			.ToArray();
		const ushort upTriggerTile = 64;
		const ushort downTriggerTile = 59;
		if (generated.Any(result => result.MegaTiles.Count(tile => tile == upTriggerTile) != 1
			|| result.MegaTiles.Count(tile => tile == downTriggerTile) != 1))
			throw new InvalidOperationException("Synthetic Cathedral depths did not expose the native protected stair tiles.");
		var pieces = Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
			.Select(id => new AuthoritativeDunPieceDefinition((uint)id, 1, 1, 1, 1))
			.ToArray();
		pieces[upTriggerTile - 1] = new AuthoritativeDunPieceDefinition(upTriggerTile - 1, 128, 1, 1, 1);
		pieces[downTriggerTile - 1] = new AuthoritativeDunPieceDefinition(downTriggerTile - 1, 114, 1, 1, 1);
		return new AuthoritativeNativeDunGeometryCatalog(
			new AuthoritativeDunPieceCatalog(pieces),
			new AuthoritativeDunTilePropertyCatalog([
				new(1, AuthoritativeDunTileFlags.None),
				new(114, AuthoritativeDunTileFlags.None),
				new(128, AuthoritativeDunTileFlags.None),
			]));
	}

	private sealed record Fixture(
		string Table,
		StableContentIdCatalog StableIds,
		AuthoritativeWorld World,
		AuthoritativeLevel Town,
		IReadOnlyList<AuthoritativeLevel> OriginalLevels,
		AuthoritativeCathedralDepthRuntimeCatalogDefinition Catalog,
		AuthoritativeNativeDunGeometryCatalog Geometry,
		AuthoritativeCathedralDepthGenerationPlan Plan,
		IReadOnlyList<AuthoritativeWarpTarget> Warps,
		AuthoritativeNativeGeneratedRouteBindingCatalog Bindings,
		string BindingTable);
}
