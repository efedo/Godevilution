using System.Security.Cryptography;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Focused guards for the family-specific fresh/full/ready Catacombs contracts. */
public sealed class AuthoritativeCatacombsCompositionContractTests
{
	private const uint StorySeed = 0x1357_9BDF;

	[Fact(Explicit = true)]
	public void FreshEntryRequestCopiesUniqueHistoryAndRejectsUnsupportedEntry()
	{
		var metadata = AuthoritativeCatacombsInitialPopulationNativeOracleTests.QualifiedInputs().Metadata;
		var flags = new HashSet<int> { 7 };
		var hero = WarriorHero();
		var request = AuthoritativeCatacombsFreshLevelGenerator.CreateEntryRequest(
			metadata, 5, 2588, StorySeed, AuthoritativeCatacombsEntry.Main, hero,
			initialUniqueItemFlags: flags);

		flags.Add(8);
		Assert.Equal(new[] { 7 }, request.InitialUniqueItemFlags.Order());
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			AuthoritativeCatacombsFreshLevelGenerator.CreateEntryRequest(
				metadata, 6, 2588, StorySeed, AuthoritativeCatacombsEntry.TownWarpDown, hero));
	}

	[Fact(Explicit = true)]
	public void FullAndReadyContractsDetachInputsAndRejectUnsupportedColdState()
	{
		var metadata = AuthoritativeCatacombsInitialPopulationNativeOracleTests.QualifiedInputs().Metadata;
		var fresh = AuthoritativeCatacombsFreshLevelGenerator.Generate(
			metadata, 5, 2588, StorySeed, AuthoritativeCatacombsEntry.Main, WarriorHero(),
			initialUniqueItemFlags: new HashSet<int> { 7 });
		var source = fresh.FullPopulationRequest;
		var mutableTileFlags = source.InitialTileFlags.ToArray();
		var mutableUniqueFlags = new HashSet<int>(source.InitialUniqueItemFlags);
		var detached = CopyFullRequest(source, source.TriggerPositions, mutableTileFlags, mutableUniqueFlags);
		var copiedTileFlags = detached.InitialTileFlags.ToArray();
		var newlyAddedFlag = Enumerable.Range(0, 128).First(value => !mutableUniqueFlags.Contains(value));

		mutableTileFlags[0] ^= 0x80;
		mutableUniqueFlags.Add(newlyAddedFlag);
		Assert.Equal(copiedTileFlags, detached.InitialTileFlags);
		Assert.DoesNotContain(newlyAddedFlag, detached.InitialUniqueItemFlags);
		Assert.Throws<InvalidDataException>(() => CopyFullRequest(
			source, Array.Empty<AuthoritativeCathedralPopulationPoint>(),
			source.InitialTileFlags.ToArray(), new HashSet<int>(source.InitialUniqueItemFlags)));

		var automap = CreateAutomap();
		var player = new AuthoritativeCathedralLevelReadyPlayerContext(
			0, false, false, true, 4480, new(13, 17), new(0, 0), true);
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCatacombsLevelReadyRequest(
			fresh, player, automap, portalsAbsent: false));
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCatacombsLevelReadyRequest(
			fresh, player with { PriorMissilesEmpty = false }, automap, portalsAbsent: true));
		var accepted = new AuthoritativeCatacombsLevelReadyRequest(fresh, player, automap, portalsAbsent: true);
		Assert.Same(fresh, accepted.FreshLevel);
	}

	private static AuthoritativeCatacombsFreshLevelHeroContext WarriorHero()
		=> new(4480, 640, 10, 10, new(75, 68), LevelChanging: true);

	private static AuthoritativeCatacombsAutomapMetadata CreateAutomap()
	{
		var tables = AuthoritativeCatacombsAutomapMetadata.CreateTables(
			Enumerable.Repeat((ushort)0, AuthoritativeCatacombsAutomapMetadata.SourceWordCount).ToArray(),
			Convert.ToHexString(SHA256.HashData(new byte[AuthoritativeCatacombsAutomapMetadata.SourceWordCount * sizeof(ushort)]))
				.ToLowerInvariant());
		return AuthoritativeCatacombsAutomapMetadata.Load(new ContentPack(
			AuthoritativeCatacombsAutomapMetadata.CatalogId,
			AuthoritativeCatacombsAutomapMetadata.SchemaVersion,
			tables,
			schema: new ContentSchemaIdentity(AuthoritativeCatacombsAutomapMetadata.SchemaId,
				AuthoritativeCatacombsAutomapMetadata.SchemaVersion)));
	}

	private static AuthoritativeCatacombsFullPopulationRequest CopyFullRequest(
		AuthoritativeCatacombsFullPopulationRequest source,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
		IReadOnlyList<byte> tileFlags,
		IReadOnlySet<int> uniqueFlags)
		=> new(source.InitialPopulation, source.MonsterClearRandomState,
			source.InitialMonsterTileRefs, source.InitialPlayerRefs, tileFlags,
			source.InitialCorpses, source.InitialLight, source.InitialPreLight,
			source.LightAllocator, triggerPositions, source.OrderedUniqueDefinitions,
			source.MonsterDensityDivisor, source.ShrineAvailable, source.ItemCatalog,
			source.ItemPolicy, source.ItemPlayer, source.InitialItemSlots,
			source.InitialActiveItemIds, source.InitialActiveItemCount, uniqueFlags,
			source.InitialItemTiles, source.PotionDefinitions,
			source.MaximumPlacementAttempts, source.MaximumItemPlacementAttempts,
			source.InitialVisibleTransparencyRegions, playerInitialized: true);
}
