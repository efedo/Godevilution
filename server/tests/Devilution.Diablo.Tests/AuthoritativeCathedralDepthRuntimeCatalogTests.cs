using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralDepthRuntimeCatalogTests
{
	[Fact]
	public void LoadRequiresExactOrderedFourDepthSchemaAndStableIdentities()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralDepthRuntimeCatalog.Load(fixture.Table, fixture.StableIds);

		Assert.Equal(AuthoritativeCathedralDepthRuntimeCatalog.CatalogId, catalog.CatalogId);
		Assert.Equal(AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId, catalog.GeneratorId);
		Assert.Equal(4, catalog.Definitions.Count);
		Assert.Equal(new[] { 1, 2, 3, 4 }, catalog.Definitions.Select(definition => definition.Depth));
		Assert.Equal(new uint[] { 1, 2, 3, 4 }, catalog.Definitions.Select(definition => definition.LevelId));
		Assert.Equal(AuthoritativeCathedralEntry.Main, catalog.Definitions[0].Entry);
		Assert.Equal(AuthoritativeCathedralEntry.Previous, catalog.Definitions[1].Entry);
		Assert.All(catalog.Definitions, definition =>
			Assert.Equal(
				AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
				definition.Policy));

		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralDepthRuntimeCatalog.Load(
				fixture.Table.Replace("depth\tgenerator_id", "generator_id\tdepth", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralDepthRuntimeCatalog.Load(
				fixture.Table.Replace("2\tcathedral2\t2", "2\tcathedral2\t3", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralDepthRuntimeCatalog.Load(
				fixture.Table.Replace("quest-disabled-original-diablo", "quest-enabled", StringComparison.Ordinal),
				fixture.StableIds));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralDepthRuntimeCatalog.Load(
				fixture.Table[..fixture.Table.LastIndexOf("4\tcathedral4", StringComparison.Ordinal)],
				fixture.StableIds));
	}

	[Fact]
	public void IdentityRequiresCanonicalOrderedNativeDepthsAndRequireSameIsSeparate()
	{
		var identities = Enumerable.Range(1, 4)
			.Select(depth => CreateIdentity(depth, depth))
			.ToArray();
		var identity = new DiabloCathedralDepthWorldIdentity(identities, Fingerprint);
		Assert.Equal(new[] { 1, 2, 3, 4 }, identity.Levels.Select(level => level.Depth));

		Assert.Throws<InvalidDataException>(() =>
			new DiabloCathedralDepthWorldIdentity(identities.Reverse().ToArray(), Fingerprint));
		Assert.Throws<InvalidDataException>(() =>
			new DiabloCathedralDepthWorldIdentity(identities, new string('a', 64)));

		var equivalent = new DiabloCathedralDepthWorldIdentity(
			identities.Select(level => level with { }).ToArray(),
			Fingerprint);
		DiabloCathedralDepthWorldIdentity.RequireSame(identity, equivalent);
		var changed = new DiabloCathedralDepthWorldIdentity(
			identities.Select(level => level with { DungeonSeed = level.DungeonSeed + 1U }).ToArray(),
			Fingerprint);
		Assert.Throws<InvalidDataException>(() =>
			DiabloCathedralDepthWorldIdentity.RequireSame(
				identity,
				changed));
	}

	private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

	private static AuthoritativeCathedralDepthGenerationIdentity CreateIdentity(int depth, int levelId)
		=> new(
			AuthoritativeCathedralDepthRuntimeCatalog.CatalogId,
			AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId,
			AuthoritativeCathedralDepthRuntimeCatalog.GeneratorRevision,
			(uint)levelId,
			$"cathedral{depth}",
			depth,
			AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
			(uint)(depth * 17),
			AuthoritativeCathedralEntry.Main,
			1,
			2,
			Fingerprint,
			Fingerprint,
			Fingerprint,
			Fingerprint);

	private static Fixture CreateFixture()
	{
		var stableIds = new StableContentIdCatalog(Enumerable.Range(1, 4)
			.Select(depth => new StableContentId("level", $"cathedral{depth}", (uint)depth)));
		var rows = Enumerable.Range(1, 4).Select(depth =>
			$"{depth}\tcathedral{depth}\t{depth}\t{AuthoritativeCathedralDepthRuntimeCatalog.GeneratorId}\t1\t{depth * 17}\t{(depth == 2 ? "previous" : "main")}\tquest-disabled-original-diablo\n");
		return new Fixture(
			"level_id\tlevel_symbol\tdepth\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\tpolicy\n" + string.Concat(rows),
			stableIds);
	}

	private sealed record Fixture(string Table, StableContentIdCatalog StableIds);
}
