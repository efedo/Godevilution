using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeGeneratedRouteBindingsTests
{
	[Fact]
	public void LoadRequiresExactSchemaStableIdentitiesAndUniqueEndpointBindings()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeNativeGeneratedRouteBindings.Load(fixture.Table, fixture.StableIds);

		Assert.Equal(8, catalog.Bindings.Count);
		Assert.False(string.IsNullOrWhiteSpace(catalog.CatalogFingerprint));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Load(
				fixture.Table.Replace("route_kind\troute_id", "route_id\troute_kind", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Load(
				fixture.Table.Replace("warp\t100", "bogus\t100", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Load(
				fixture.Table.Replace("town_cathedral_return", "unknown_anchor", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Load(
				fixture.Table.Replace("warp\t100\tto_cathedral", "warp\t101\tto_cathedral", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Load(
				fixture.Table + "warp\t100\tto_cathedral\tsource\t0\ttown\ttown_cathedral_trigger\n",
				fixture.StableIds));
	}

	[Fact]
	public void ResolveAcceptsSeparatorBearingStableSymbolsAndFingerprintsThemDeterministically()
	{
		var fixture = CreateFixture();
		const string separatorBearingSymbol = "to|cathedral";
		var stableIds = new StableContentIdCatalog(fixture.StableIds.Entries.Select(entry =>
			entry is { Kind: "warp", NumericId: 100 }
				? entry with { Symbol = separatorBearingSymbol }
				: entry));
		var separatorWarp = new AuthoritativeWarpTarget(100, 0, 75, 68, 1, 75, 68) {
			StableSymbol = separatorBearingSymbol,
		};
		var warps = fixture.Warps
			.Select(warp => warp.WarpId == 100 ? separatorWarp : warp)
			.ToArray();
		var table = fixture.Table.Replace("to_cathedral", separatorBearingSymbol, StringComparison.Ordinal);
		var resolution = AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			fixture.Plan,
			warps,
			fixture.Portals,
			AuthoritativeNativeGeneratedRouteBindings.Load(table, stableIds));
		var repeated = AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			fixture.Plan,
			warps,
			fixture.Portals,
			AuthoritativeNativeGeneratedRouteBindings.Load(table, stableIds));

		Assert.Equal(separatorBearingSymbol, resolution.Warps.Single(warp => warp.WarpId == 100).StableSymbol);
		Assert.Equal(resolution.RouteFingerprint, repeated.RouteFingerprint);
	}

	[Fact]
	public void ResolveStagesNativeAnchorsPreservesMetadataAndSupportsContinuationRoutes()
	{
		var fixture = CreateFixture();
		var originalWarp = fixture.Warps.Single(warp => warp.WarpId == 100);
		var originalReturn = fixture.Portals.Single(portal => portal.PortalId == 200);
		var resolution = AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			fixture.Plan,
			fixture.Warps,
			fixture.Portals,
			AuthoritativeNativeGeneratedRouteBindings.Load(fixture.Table, fixture.StableIds));

		var candidate = fixture.Plan.Candidates.Single();
		Assert.Equal(3, resolution.Warps.Count);
		var warp = resolution.Warps.Single(warp => warp.WarpId == 100);
		Assert.Equal((25, 29), (warp.TriggerX, warp.TriggerY));
		Assert.Equal((candidate.Routes.MainEntry.X, candidate.Routes.MainEntry.Y), (warp.TargetX, warp.TargetY));
		Assert.Equal(originalWarp.RequiredQuestId, warp.RequiredQuestId);
		Assert.Equal(originalWarp.RequiredQuestCompleted, warp.RequiredQuestCompleted);
		Assert.Equal(originalWarp.TargetSetLevelId, warp.TargetSetLevelId);
		Assert.Equal(originalWarp.TargetSetLevelActiveMap, warp.TargetSetLevelActiveMap);
		Assert.Equal(originalWarp.TargetSetLevelSymbol, warp.TargetSetLevelSymbol);
		Assert.Equal(originalWarp.StableSymbol, warp.StableSymbol);
		var optionalWarp = resolution.Warps.Single(warp => warp.WarpId == 101);
		Assert.Equal((60, 60), (optionalWarp.TriggerX, optionalWarp.TriggerY));
		Assert.Equal((60, 60), (optionalWarp.TargetX, optionalWarp.TargetY));
		Assert.Equal((uint)9, optionalWarp.RequiredQuestId);
		Assert.True(optionalWarp.RequiredQuestCompleted);
		Assert.Equal(2, optionalWarp.TargetSetLevelId);
		Assert.False(optionalWarp.TargetSetLevelActiveMap);
		Assert.Equal("set_two", optionalWarp.TargetSetLevelSymbol);

        var returnPortal = Assert.Single(resolution.Portals, portal => portal.PortalId == 200);
		Assert.Equal((candidate.Routes.UpTrigger.X, candidate.Routes.UpTrigger.Y), (returnPortal.SourcePositionX, returnPortal.SourcePositionY));
		Assert.Equal((25, 31), (returnPortal.DestinationPositionX, returnPortal.DestinationPositionY));
		Assert.Equal(originalReturn.RemainingTicks, returnPortal.RemainingTicks);
		Assert.Equal(originalReturn.OwnerEntityId, returnPortal.OwnerEntityId);
		Assert.Equal(originalReturn.Bidirectional, returnPortal.Bidirectional);
		Assert.Equal(originalReturn.DestinationSetLevelId, returnPortal.DestinationSetLevelId);
		Assert.Equal(originalReturn.DestinationSetLevelSymbol, returnPortal.DestinationSetLevelSymbol);

        var continuation = Assert.Single(resolution.Portals, portal => portal.PortalId == 201);
		Assert.Equal((candidate.Routes.DownTrigger.X, candidate.Routes.DownTrigger.Y), (continuation.SourcePositionX, continuation.SourcePositionY));
		Assert.Equal((candidate.Routes.PreviousEntry.X, candidate.Routes.PreviousEntry.Y), (continuation.DestinationPositionX, continuation.DestinationPositionY));
		Assert.NotSame(originalWarp, warp);
		Assert.NotSame(originalReturn, returnPortal);
		Assert.Equal((75, 68), (originalWarp.TriggerX, originalWarp.TriggerY));
		Assert.Equal((75, 68), (originalReturn.DestinationPositionX, originalReturn.DestinationPositionY));
		Assert.Same(fixture.OriginalCathedral, fixture.World.Levels.Single(level => level.LevelId == 1));
		Assert.False(string.IsNullOrWhiteSpace(resolution.RouteFingerprint));

		var repeated = AuthoritativeNativeGeneratedRouteBindings.Resolve(
			fixture.World,
			fixture.Town,
			fixture.Plan,
			fixture.Warps,
			fixture.Portals,
			AuthoritativeNativeGeneratedRouteBindings.Load(fixture.Table, fixture.StableIds));
		Assert.Equal(resolution.RouteFingerprint, repeated.RouteFingerprint);
	}

	[Fact]
	public void ResolveRejectsMissingGeneratedBindingsAndDanglingRowsWithoutPartialMutation()
	{
		var fixture = CreateFixture();
		var originalWarp = fixture.Warps.Single(warp => warp.WarpId == 100);
		var missingMain = fixture.Table.Replace(
			"warp\t100\tto_cathedral\tdestination\t1\tcathedral\tmain_entry\n",
			string.Empty,
			StringComparison.Ordinal);
		var missingCatalog = AuthoritativeNativeGeneratedRouteBindings.Load(missingMain, fixture.StableIds);
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Resolve(
				fixture.World,
				fixture.Town,
				fixture.Plan,
				fixture.Warps,
				fixture.Portals,
				missingCatalog));
		Assert.Equal((75, 68), (originalWarp.TriggerX, originalWarp.TriggerY));
		Assert.Same(fixture.OriginalCathedral, fixture.World.Levels.Single(level => level.LevelId == 1));

		var staleEndpoint = fixture.Table.Replace(
			"destination\t1\tcathedral\tmain_entry",
			"destination\t2\tother\tmain_entry",
			StringComparison.Ordinal);
		var staleCatalog = AuthoritativeNativeGeneratedRouteBindings.Load(staleEndpoint, fixture.StableIds);
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Resolve(
				fixture.World,
				fixture.Town,
				fixture.Plan,
				fixture.Warps,
				fixture.Portals,
				staleCatalog));

		var dangling = fixture.Table
			+ "portal\t202\torphan\tsource\t0\ttown\ttown_cathedral_trigger\n";
		var danglingCatalog = AuthoritativeNativeGeneratedRouteBindings.Load(dangling, fixture.StableIds);
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Resolve(
				fixture.World,
				fixture.Town,
				fixture.Plan,
				fixture.Warps,
				fixture.Portals,
				danglingCatalog));
	}

	[Fact]
	public void ResolveRejectsTownMisuseBlockedAnchorsAndNonGeneratedBindings()
	{
		var blocked = CreateFixture(blockTownAnchors: true);
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Resolve(
				blocked.World,
				blocked.Town,
				blocked.Plan,
				blocked.Warps,
				blocked.Portals,
				AuthoritativeNativeGeneratedRouteBindings.Load(blocked.Table, blocked.StableIds)));

		var fixture = CreateFixture();
		var unregisteredTown = new AuthoritativeLevel(0, 112, 112) { StableSymbol = "town" };
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Resolve(
				fixture.World,
				unregisteredTown,
				fixture.Plan,
				fixture.Warps,
				fixture.Portals,
				AuthoritativeNativeGeneratedRouteBindings.Load(fixture.Table, fixture.StableIds)));

		var nonGeneratedTable = fixture.Table
			+ "warp\t101\tto_other\tdestination\t2\tother\ttown_cathedral_return\n";
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Resolve(
				fixture.World,
				fixture.Town,
				fixture.Plan,
				fixture.Warps,
				fixture.Portals,
				AuthoritativeNativeGeneratedRouteBindings.Load(nonGeneratedTable, fixture.StableIds)));
	}

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveRejectsPortalOrGatedRoutesAsMandatoryNativeStairs(bool gateMain)
	{
		var fixture = CreateFixture();
		var gatedMain = new AuthoritativeWarpTarget(
			100,
			0,
			75,
			68,
			1,
			75,
			68,
			requiredQuestId: 9,
			requiredQuestCompleted: true) {
			StableSymbol = "to_cathedral",
		};
		var tableWithoutReturnWarp = fixture.Table
			.Replace(
				"warp\t102\treturn_cathedral\tsource\t1\tcathedral\tup_trigger\n",
				string.Empty,
				StringComparison.Ordinal)
			.Replace(
				"warp\t102\treturn_cathedral\tdestination\t0\ttown\ttown_cathedral_return\n",
				string.Empty,
				StringComparison.Ordinal);

		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeNativeGeneratedRouteBindings.Resolve(
				fixture.World,
				fixture.Town,
				fixture.Plan,
                gateMain
                    ? fixture.Warps.Select(warp => warp.WarpId == 100 ? gatedMain : warp)
                    : fixture.Warps.Where(warp => warp.WarpId != 102),
                fixture.Portals,
                AuthoritativeNativeGeneratedRouteBindings.Load(gateMain ? fixture.Table : tableWithoutReturnWarp, fixture.StableIds)));
    }

    [Fact]
    public void RouteFingerprintSeparatesPreviouslyAmbiguousUnboundSymbols()
    {
        var fixture = CreateFixture();
        var bindings = AuthoritativeNativeGeneratedRouteBindings.Load(fixture.Table, fixture.StableIds);
        var first = new AuthoritativeWarpTarget(103, 0, 1, 1, 0, 2, 2) { StableSymbol = "a|none|2|x" };
        var firstFingerprint = AuthoritativeNativeGeneratedRouteBindings.Resolve(fixture.World, fixture.Town,
            fixture.Plan, fixture.Warps.Append(first), fixture.Portals, bindings).RouteFingerprint;
        fixture.World.ReplaceLevel(new AuthoritativeLevel(2, 112, 112) { StableSymbol = "x|none|0|town" });
        var second = new AuthoritativeWarpTarget(103, 2, 1, 1, 0, 2, 2) { StableSymbol = "a" };
        var secondFingerprint = AuthoritativeNativeGeneratedRouteBindings.Resolve(fixture.World, fixture.Town,
            fixture.Plan, fixture.Warps.Append(second), fixture.Portals, bindings).RouteFingerprint;
        Assert.NotEqual(firstFingerprint, secondFingerprint);
    }

	private static Fixture CreateFixture(bool blockTownAnchors = false)
	{
		const uint seed = 17;
		var stableIds = new StableContentIdCatalog([
			new StableContentId("level", "town", 0),
			new StableContentId("level", "cathedral", 1),
			new StableContentId("level", "other", 2),
			new StableContentId("warp", "to_cathedral", 100),
			new StableContentId("warp", "to_other", 101),
			new StableContentId("warp", "return_cathedral", 102),
			new StableContentId("portal", "return_town", 200),
			new StableContentId("portal", "continuation", 201),
			new StableContentId("portal", "orphan", 202),
		]);
		var townBlocked = blockTownAnchors
			? new[] { AuthoritativeNativeGeneratedRouteBindings.TownCathedralTriggerY * 112 + AuthoritativeNativeGeneratedRouteBindings.TownCathedralTriggerX,
				AuthoritativeNativeGeneratedRouteBindings.TownCathedralReturnY * 112 + AuthoritativeNativeGeneratedRouteBindings.TownCathedralReturnX }
			: Array.Empty<int>();
		var world = new AuthoritativeWorld();
		var town = new AuthoritativeLevel(0, 112, 112, townBlocked, townBlocked) { StableSymbol = "town" };
		var originalCathedral = new AuthoritativeLevel(1, 4, 4, isDefaultStart: true) { StableSymbol = "cathedral" };
		world.AddLevel(town);
		world.AddLevel(originalCathedral);
		world.AddLevel(new AuthoritativeLevel(2, 112, 112) { StableSymbol = "other" });
		var runtime = AuthoritativeCathedralLevel1RuntimeCatalog.Load(
			$"level_id\tlevel_symbol\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\n"
			+ $"1\tcathedral\t{AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId}\t1\t{seed}\tmain\n",
			stableIds);
		var plan = runtime.Prepare(world, CreateGeometry(seed));
		var candidate = plan.Candidates.Single();
		var warps = new[] {
			new AuthoritativeWarpTarget(
				100,
				0,
				75,
				68,
				1,
				75,
				68) {
				TargetSetLevelSymbol = string.Empty,
				StableSymbol = "to_cathedral",
			},
			new AuthoritativeWarpTarget(
				102,
				1,
				candidate.Routes.UpTrigger.X,
				candidate.Routes.UpTrigger.Y,
				0,
				75,
				68) {
				StableSymbol = "return_cathedral",
			},
			new AuthoritativeWarpTarget(
				101,
				0,
				60,
				60,
				2,
				60,
				60,
				requiredQuestId: 9,
				requiredQuestCompleted: true,
				targetSetLevelId: 2,
				targetSetLevelActiveMap: false) {
				TargetSetLevelSymbol = "set_two",
				StableSymbol = "to_other",
			},
		};
		var portals = new[] {
			new AuthoritativePortal(
				200,
				1,
				candidate.Routes.UpTrigger.X,
				candidate.Routes.UpTrigger.Y,
				0,
				75,
				68) {
				RemainingTicks = 12,
				OwnerEntityId = 7,
				Bidirectional = true,
				DestinationSetLevelId = 3,
				DestinationSetLevelActiveMap = false,
				DestinationSetLevelSymbol = "set_three",
				StableSymbol = "return_town",
			},
			new AuthoritativePortal(
				201,
				1,
				candidate.Routes.DownTrigger.X,
				candidate.Routes.DownTrigger.Y,
				1,
				candidate.Routes.PreviousEntry.X,
				candidate.Routes.PreviousEntry.Y) {
				StableSymbol = "continuation",
			},
		};
		return new Fixture(
			stableIds,
			world,
			town,
			originalCathedral,
			plan,
			warps,
			portals,
			$"route_kind\troute_id\troute_symbol\tendpoint\tlevel_id\tlevel_symbol\tanchor\n"
			+ "warp\t100\tto_cathedral\tsource\t0\ttown\ttown_cathedral_trigger\n"
			+ "warp\t100\tto_cathedral\tdestination\t1\tcathedral\tmain_entry\n"
			+ "warp\t102\treturn_cathedral\tsource\t1\tcathedral\tup_trigger\n"
			+ "warp\t102\treturn_cathedral\tdestination\t0\ttown\ttown_cathedral_return\n"
			+ "portal\t200\treturn_town\tsource\t1\tcathedral\tup_trigger\n"
			+ "portal\t200\treturn_town\tdestination\t0\ttown\ttown_cathedral_return\n"
			+ "portal\t201\tcontinuation\tsource\t1\tcathedral\tdown_trigger\n"
			+ "portal\t201\tcontinuation\tdestination\t1\tcathedral\tprevious_entry\n");
	}

	private static AuthoritativeNativeDunGeometryCatalog CreateGeometry(uint seed)
	{
		var generated = AuthoritativeCathedralLevel1Generator.Generate(seed, AuthoritativeCathedralEntry.Main);
		var singleUseMegaTiles = generated.MegaTiles
			.GroupBy(tile => tile)
			.Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId && group.Count() == 1)
			.Select(group => group.Key)
			.Take(2)
			.ToArray();
		if (singleUseMegaTiles.Length != 2)
			throw new InvalidOperationException("The synthetic seed did not expose two trigger mega-tile IDs.");

		var pieces = Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
			.Select(id => new AuthoritativeDunPieceDefinition((uint)id, 1, 1, 1, 1))
			.ToArray();
        pieces[singleUseMegaTiles[0] - 1] = new AuthoritativeDunPieceDefinition(checked((uint)singleUseMegaTiles[0] - 1), 128, 1, 1, 1);
        pieces[singleUseMegaTiles[1] - 1] = new AuthoritativeDunPieceDefinition(checked((uint)singleUseMegaTiles[1] - 1), 114, 1, 1, 1);
		return new AuthoritativeNativeDunGeometryCatalog(
			new AuthoritativeDunPieceCatalog(pieces),
			new AuthoritativeDunTilePropertyCatalog([
				new AuthoritativeDunTileProperty(1, AuthoritativeDunTileFlags.None),
				new AuthoritativeDunTileProperty(114, AuthoritativeDunTileFlags.None),
				new AuthoritativeDunTileProperty(128, AuthoritativeDunTileFlags.None),
			]));
	}

	private sealed record Fixture(
		StableContentIdCatalog StableIds,
		AuthoritativeWorld World,
		AuthoritativeLevel Town,
		AuthoritativeLevel OriginalCathedral,
		AuthoritativeCathedralLevel1GenerationPlan Plan,
		IReadOnlyList<AuthoritativeWarpTarget> Warps,
		IReadOnlyList<AuthoritativePortal> Portals,
		string Table);
}
