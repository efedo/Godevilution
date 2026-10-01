using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralLevel1RuntimeCatalogTests
{
	[Fact]
	public void LoadRequiresExactSchemaStableIdentityAndSupportedGenerator()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralLevel1RuntimeCatalog.Load(fixture.Table, fixture.StableIds);

		Assert.Equal(AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId, catalog.CatalogId);
		Assert.Equal(AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId, catalog.GeneratorId);
        Assert.Single(catalog.Definitions);
		Assert.Equal((uint)1, catalog.Definitions[0].LevelId);
		Assert.Equal("cathedral", catalog.Definitions[0].LevelSymbol);
		Assert.Equal(fixture.Seed, catalog.Definitions[0].DungeonSeed);
		Assert.Equal(AuthoritativeCathedralEntry.Main, catalog.Definitions[0].Entry);

		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table.Replace("level_symbol", "wrong", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table.Replace("\tcathedral\t", "\tother\t", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table.Replace("\tmain\n", "\tbogus\n", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table.Replace("\t1\t", "\t2\t", StringComparison.Ordinal),
				fixture.StableIds));
	}

	[Fact]
	public void LoadRejectsDuplicateMalformedAndEmptyTables()
	{
		var fixture = CreateFixture();
		var duplicate = fixture.Table + fixture.Table[(fixture.Table.IndexOf('\n') + 1)..];
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(duplicate, fixture.StableIds));

		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table.Replace("\t1\t", "\t-1\t", StringComparison.Ordinal),
				fixture.StableIds));
		var nonLevelOne = fixture.Table[(fixture.Table.IndexOf('\n') + 1)..]
			.Replace("1\tcathedral", "2\tcathedral", StringComparison.Ordinal);
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table[..(fixture.Table.IndexOf('\n') + 1)] + nonLevelOne,
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table + nonLevelOne,
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				fixture.Table.Replace("\tcathedral\t", "\t\t", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralLevel1RuntimeCatalog.Load(
				"level_symbol\tlevel_id\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\n",
				fixture.StableIds));
	}

	[Fact]
	public void PrepareEagerlyStagesFineGeometryRoutesAndIdentityWithoutWorldMutation()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralLevel1RuntimeCatalog.Load(fixture.Table, fixture.StableIds);
		var original = fixture.World.Levels.Single();

		var plan = catalog.Prepare(fixture.World, fixture.Geometry);
		var candidate = Assert.Single(plan.Candidates);

		Assert.Same(original, fixture.World.Levels.Single());
		Assert.Equal(112, candidate.Level.Width);
		Assert.Equal(112, candidate.Level.Height);
		Assert.Empty(candidate.Level.NativeMegaTileLayer);
		Assert.Equal(candidate.GenerationResult.EntryPosition.X, candidate.Level.StartPositionX);
		Assert.Equal(candidate.GenerationResult.EntryPosition.Y, candidate.Level.StartPositionY);
		Assert.True(candidate.Level.IsDefaultStart);
		Assert.True(candidate.Level.SuppressMultiplayerDeathDrops);
		Assert.True(candidate.Level.ContainsPosition(candidate.Routes.UpTrigger.X, candidate.Routes.UpTrigger.Y));
		Assert.True(candidate.Level.ContainsPosition(candidate.Routes.DownTrigger.X, candidate.Routes.DownTrigger.Y));
		Assert.True(candidate.Level.ContainsPosition(candidate.Routes.MainEntry.X, candidate.Routes.MainEntry.Y));
		Assert.True(candidate.Level.ContainsPosition(candidate.Routes.PreviousEntry.X, candidate.Routes.PreviousEntry.Y));
		Assert.False(string.IsNullOrWhiteSpace(candidate.Identity.CoarseFingerprint));
		Assert.False(string.IsNullOrWhiteSpace(candidate.Identity.FineFingerprint));
		Assert.False(string.IsNullOrWhiteSpace(candidate.Identity.RouteFingerprint));
		Assert.False(string.IsNullOrWhiteSpace(candidate.Identity.CatalogFingerprint));

		plan.Apply(fixture.World);
		Assert.Same(candidate.Level, fixture.World.Levels.Single());
	}

	[Fact]
	public void PrepareSupportsPreviousEntryAndUnsignedBoundarySeed()
	{
		var fixture = CreateFixture(uint.MaxValue, false, AuthoritativeCathedralEntry.Previous);
		var plan = AuthoritativeCathedralLevel1RuntimeCatalog
			.Load(fixture.Table, fixture.StableIds)
			.Prepare(fixture.World, fixture.Geometry);
		var candidate = Assert.Single(plan.Candidates);

		Assert.Equal(uint.MaxValue, candidate.Identity.DungeonSeed);
		Assert.Equal(AuthoritativeCathedralEntry.Previous, candidate.Identity.Entry);
		Assert.Equal(candidate.GenerationResult.EntryPosition, candidate.Routes.PreviousEntry);
	}

	[Fact]
	public void SeedAndExternalGeometryChangesProduceDifferentGenerationIdentity()
	{
		var first = CreateFixture(17, false);
		var firstPlan = AuthoritativeCathedralLevel1RuntimeCatalog
			.Load(first.Table, first.StableIds)
			.Prepare(first.World, first.Geometry);

		var second = CreateFixture(18, false);
		var secondPlan = AuthoritativeCathedralLevel1RuntimeCatalog
			.Load(second.Table, second.StableIds)
			.Prepare(second.World, second.Geometry);
		Assert.NotEqual(
			firstPlan.Candidates[0].Identity.CoarseFingerprint,
			secondPlan.Candidates[0].Identity.CoarseFingerprint);

		var changedGeometry = CreateFixture(17, true);
		var changedPlan = AuthoritativeCathedralLevel1RuntimeCatalog
			.Load(changedGeometry.Table, changedGeometry.StableIds)
			.Prepare(changedGeometry.World, changedGeometry.Geometry);
		Assert.NotEqual(
			firstPlan.Candidates[0].Identity.CatalogFingerprint,
			changedPlan.Candidates[0].Identity.CatalogFingerprint);
		Assert.NotEqual(
			firstPlan.Candidates[0].Identity.FineFingerprint,
			changedPlan.Candidates[0].Identity.FineFingerprint);
	}

	[Fact]
	public void FineFingerprintIncludesNativeMutationProvenanceAndExplicitVisionState()
	{
		var first = new AuthoritativeLevel(
			1,
			4,
			4,
			blockedCells: [1],
			missileBlockedCells: [2],
			nativePieceCollisionCells: [1],
			nativePieceMissileBlockedCells: [2],
			nativeVisionBlockedCells: [1]);
		var second = new AuthoritativeLevel(
			1,
			4,
			4,
			blockedCells: [1],
			missileBlockedCells: [2],
			nativePieceCollisionCells: [3],
			nativePieceMissileBlockedCells: [4]);

		Assert.NotEqual(
			AuthoritativeCathedralLevel1RuntimeCatalog.ComputeFineFingerprint(first),
			AuthoritativeCathedralLevel1RuntimeCatalog.ComputeFineFingerprint(second));
	}

	[Fact]
	public void PrepareRejectsMissingGeometryOrMalformedNativeTriggerPieces()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralLevel1RuntimeCatalog.Load(fixture.Table, fixture.StableIds);
		Assert.Throws<ArgumentNullException>(() => catalog.Prepare(fixture.World, null!));

		var noTriggers = CreateGeometry(fixture.Seed, TriggerShape.None, changedProperties: false);
		Assert.Throws<InvalidDataException>(() => catalog.Prepare(CreateWorld(), noTriggers));

		var missingDown = CreateGeometry(fixture.Seed, TriggerShape.MissingDown, changedProperties: false);
		Assert.Throws<InvalidDataException>(() => catalog.Prepare(CreateWorld(), missingDown));

		var duplicateUp = CreateGeometry(fixture.Seed, TriggerShape.DuplicateUp, changedProperties: false);
		Assert.Throws<InvalidDataException>(() => catalog.Prepare(CreateWorld(), duplicateUp));

		var duplicateDown = CreateGeometry(fixture.Seed, TriggerShape.DuplicateDown, changedProperties: false);
		Assert.Throws<InvalidDataException>(() => catalog.Prepare(CreateWorld(), duplicateDown));
	}

	[Fact]
	public void PrepareRejectsBlockedNativeTriggerOrEntry()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralLevel1RuntimeCatalog.Load(fixture.Table, fixture.StableIds);

		var blockedTrigger = CreateGeometry(fixture.Seed, TriggerShape.BlockedTrigger, changedProperties: false);
		Assert.Throws<InvalidDataException>(() => catalog.Prepare(CreateWorld(isDefaultStart: false), blockedTrigger));

		var blockedEntry = CreateGeometry(fixture.Seed, TriggerShape.BlockedEntry, changedProperties: false);
		Assert.Throws<InvalidDataException>(() => catalog.Prepare(CreateWorld(isDefaultStart: false), blockedEntry));
	}

	[Fact]
	public void ApplyRejectsStaleWorldWithoutMutatingIt()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralLevel1RuntimeCatalog.Load(fixture.Table, fixture.StableIds);
		var plan = catalog.Prepare(fixture.World, fixture.Geometry);
		var replacement = new AuthoritativeLevel(1, 4, 4) { StableSymbol = "cathedral" };
		fixture.World.ReplaceLevel(replacement);

		Assert.Throws<InvalidOperationException>(() => plan.Apply(fixture.World));
		Assert.Same(replacement, fixture.World.Levels.Single());
	}

	private static Fixture CreateFixture(
		uint seed = 17,
		bool changedProperties = false,
		AuthoritativeCathedralEntry entry = AuthoritativeCathedralEntry.Main)
	{
		var stableIds = new StableContentIdCatalog([
			new StableContentId("level", "cathedral", 1),
		]);
		var geometry = CreateGeometry(seed, TriggerShape.Valid, changedProperties);
		return new Fixture(
			seed,
			$"level_id\tlevel_symbol\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\n"
			+ $"1\tcathedral\t{AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId}\t1\t{seed}\t{(entry == AuthoritativeCathedralEntry.Main ? "main" : "previous")}\n",
			stableIds,
			CreateWorld(),
			geometry);
	}

	private static AuthoritativeWorld CreateWorld(bool isDefaultStart = true)
	{
		var world = new AuthoritativeWorld();
		world.AddLevel(new AuthoritativeLevel(
			1,
			4,
			4,
			isDefaultStart: isDefaultStart,
			suppressMultiplayerDeathDrops: true) {
			StableSymbol = "cathedral",
		});
		return world;
	}

	private static AuthoritativeNativeDunGeometryCatalog CreateGeometry(
		uint seed,
		TriggerShape shape,
		bool changedProperties)
	{
		var generated = AuthoritativeCathedralLevel1Generator.Generate(seed, AuthoritativeCathedralEntry.Main);
		var singleUseMegaTiles = generated.MegaTiles
			.GroupBy(tile => tile)
			.Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId && group.Count() == 1)
			.Select(group => group.Key)
			.Take(2)
			.ToArray();
		if (shape is not TriggerShape.None && singleUseMegaTiles.Length != 2)
			throw new InvalidOperationException("The synthetic test seed did not expose two single-use mega-tile IDs.");
		var repeatedMegaTile = generated.MegaTiles
			.GroupBy(tile => tile)
			.Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId && group.Count() > 1)
			.Select(group => group.Key)
			.FirstOrDefault();
		if (shape is TriggerShape.DuplicateUp or TriggerShape.DuplicateDown && repeatedMegaTile == 0)
			throw new InvalidOperationException("The synthetic test seed did not expose a repeated mega-tile ID.");

		var pieces = Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
			.Select(id => new AuthoritativeDunPieceDefinition((uint)id, 1, 1, 1, 1))
			.ToArray();
		if (shape is not TriggerShape.None)
		{
			var firstCatalogId = checked((uint)singleUseMegaTiles[0] - 1);
			var secondCatalogId = checked((uint)singleUseMegaTiles[1] - 1);
			pieces[checked((int)firstCatalogId)] = new AuthoritativeDunPieceDefinition(firstCatalogId, 128, 1, 1, 1);
			pieces[checked((int)secondCatalogId)] = new AuthoritativeDunPieceDefinition(secondCatalogId, 114, 1, 1, 1);
			if (shape == TriggerShape.MissingDown)
				pieces[checked((int)secondCatalogId)] = new AuthoritativeDunPieceDefinition(secondCatalogId, 1, 1, 1, 1);
		}
		if (shape is TriggerShape.DuplicateUp or TriggerShape.DuplicateDown)
		{
			var repeatedCatalogId = checked((uint)repeatedMegaTile - 1);
			var repeatedPiece = shape == TriggerShape.DuplicateUp ? (uint)128 : 114;
			pieces[checked((int)repeatedCatalogId)] = new AuthoritativeDunPieceDefinition(repeatedCatalogId, repeatedPiece, 1, 1, 1);
		}

		var triggerFlags = shape == TriggerShape.BlockedTrigger
			? AuthoritativeDunTileFlags.Solid
			: AuthoritativeDunTileFlags.None;
		var entryFlags = shape == TriggerShape.BlockedEntry
			? AuthoritativeDunTileFlags.Solid
			: AuthoritativeDunTileFlags.None;
		var properties = new List<AuthoritativeDunTileProperty>
		{
			new(1, entryFlags),
			new(114, AuthoritativeDunTileFlags.None),
			new(128, triggerFlags),
			new(1360, changedProperties ? AuthoritativeDunTileFlags.Transparent : AuthoritativeDunTileFlags.None),
		};
		return new AuthoritativeNativeDunGeometryCatalog(
			new AuthoritativeDunPieceCatalog(pieces),
			new AuthoritativeDunTilePropertyCatalog(properties));
	}

	private enum TriggerShape
	{
		None,
		Valid,
		MissingDown,
		DuplicateUp,
		DuplicateDown,
		BlockedTrigger,
		BlockedEntry,
	}

	private sealed record Fixture(
		uint Seed,
		string Table,
		StableContentIdCatalog StableIds,
		AuthoritativeWorld World,
		AuthoritativeNativeDunGeometryCatalog Geometry);
}
