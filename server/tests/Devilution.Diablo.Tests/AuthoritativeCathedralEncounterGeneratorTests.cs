using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Portable source-contract tests for ordinary Cathedral InitMonsters. */
public sealed class AuthoritativeCathedralEncounterGeneratorTests
{
	[Fact]
	public void PreservesInitialGolemsAndStartsRngAfterInitialPopulation()
	{
		var (population, initial) = CreateInitialPopulation();
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.Equal(initial.Monsters[0], result.Monsters[0]);
		Assert.Equal(initial.Monsters.Count, result.Monsters.Count);
		Assert.Equal(initial.FinalRandomState, result.FinalRandomState);
		Assert.Equal(initial.Monsters.Count, result.TotalMonsters);
	}

	[Fact]
	public void PreparesOrderedUniqueAndAllocatesNativeLightSlot()
	{
		var (population, initial) = CreateInitialPopulation();
		var unique = CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None, customToHit: 42, talkMessage: 17);
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000, uniques: [unique]);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var prepared = Assert.Single(result.Monsters.Skip(initial.Monsters.Count));

		Assert.Equal(1, prepared.UniqueType);
		Assert.Equal(17, prepared.TalkMessage);
		Assert.Equal(6, prepared.Goal);
		Assert.Equal(42, result.UniqueToHit[prepared.Slot]);
		Assert.Equal(0, prepared.LightId);
		Assert.Equal(0, prepared.UniqueTranslation);
		Assert.Equal(1, result.ActiveLightCount);
		Assert.Equal(0, result.ActiveLightSlots[0]);
	}

	[Fact]
	public void HorkDemonUsesNoLightAndUniqueRowsRemainSourceOrdered()
	{
		var (population, initial) = CreateInitialPopulation();
		var definitions = new[] {
			CreateUnique(10, 109, AuthoritativeCathedralUniquePack.None),
			CreateUnique(11, 109, AuthoritativeCathedralUniquePack.None),
		};
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000, uniques: definitions);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var uniques = result.Monsters.Skip(initial.Monsters.Count).ToArray();

		Assert.Equal(new[] { 10, 11 }, uniques.Select(value => value.UniqueType));
		Assert.Equal(-1, uniques[0].LightId);
		Assert.Equal(0, uniques[1].LightId);
		Assert.Equal(new[] { 0, 1 }, uniques.Select(value => value.UniqueTranslation));
	}

	[Fact]
	public void NativeQuestSuppressionUsesOnlyQuestUniqueIdsAndAllowsOrdinaryIdsAboveTwelve()
	{
		var (population, initial) = CreateInitialPopulation();
		var definitions = new[] {
			CreateUnique(0, 109, AuthoritativeCathedralUniquePack.None),
			CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None),
			CreateUnique(2, 109, AuthoritativeCathedralUniquePack.None),
			CreateUnique(3, 109, AuthoritativeCathedralUniquePack.None),
			CreateUnique(7, 109, AuthoritativeCathedralUniquePack.None),
			CreateUnique(8, 109, AuthoritativeCathedralUniquePack.None),
			CreateUnique(13, 109, AuthoritativeCathedralUniquePack.None),
		};
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000, uniques: definitions);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.Equal(new[] { 1, 13 }, result.Monsters.Skip(initial.Monsters.Count).Select(monster => monster.UniqueType));
	}

	[Fact]
	public void RejectsSelectedSetPieceAndNakrulRowsButAllowsSkippedDepthRows()
	{
		var (population, initial) = CreateInitialPopulation();
		foreach (var nativeUniqueId in new[] { 4, 5, 6, 9, 12 }) {
			var definition = CreateUnique(nativeUniqueId, 109, AuthoritativeCathedralUniquePack.None);
			Assert.Throws<InvalidDataException>(() => CreateEncounter(
				population, initial, densityDivisor: 100_000, uniques: [definition]));
		}

		var unselectedNakrulRow = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			uniques: [CreateUnique(1, 137, AuthoritativeCathedralUniquePack.None)]);
		var unselectedNakrulResult = AuthoritativeCathedralEncounterGenerator.Generate(unselectedNakrulRow);
		Assert.Equal(initial.Monsters.Count, unselectedNakrulResult.Monsters.Count);

		var unselectedSetPieceRow = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			uniques: [CreateUnique(4, 137, AuthoritativeCathedralUniquePack.None)]);
		var unselectedSetPieceResult = AuthoritativeCathedralEncounterGenerator.Generate(unselectedSetPieceRow);
		Assert.Equal(initial.Monsters.Count, unselectedSetPieceResult.Monsters.Count);

		var (selectedNakrulPopulation, selectedNakrulInitial) = CreateInitialPopulation(includeNakrulType: true);
		Assert.Throws<InvalidDataException>(() => CreateEncounter(
			selectedNakrulPopulation,
			selectedNakrulInitial,
			densityDivisor: 100_000,
			uniques: []));

		var depthZero = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			uniques: [CreateUnique(4, 109, AuthoritativeCathedralUniquePack.None, dungeonLevel: 0)]);
		var result = AuthoritativeCathedralEncounterGenerator.Generate(depthZero);

		Assert.Equal(initial.Monsters.Count, result.Monsters.Count);
	}

	[Fact]
	public void LeashedPackDoublesMinionHpCopiesLeaderAiAndSetsPackSize()
	{
		var (population, initial) = CreateInitialPopulation(includeMinionType: true);
		var unique = CreateUnique(1, 8, AuthoritativeCathedralUniquePack.Leashed);
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000, uniques: [unique]);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var leader = result.Monsters[initial.Monsters.Count];
		var minions = result.Monsters.Skip(initial.Monsters.Count + 1).ToArray();

		Assert.Equal(8, minions.Length);
		Assert.Equal(8, leader.PackSize);
		Assert.All(minions, minion => {
			Assert.Equal(leader.Slot, minion.Leader);
			Assert.Equal(1, minion.LeaderRelation);
			Assert.Equal(leader.Ai, minion.Ai);
			Assert.InRange(minion.MaxHitPoints, 256, 576);
			Assert.Equal(minion.MaxHitPoints, minion.HitPoints);
		});
	}

	[Fact]
	public void UniqueAiOverrideRestoresStandAnimationAfterGargoyleBaseAnimation()
	{
		var (population, initial) = CreateInitialPopulation(golemAi: 12);
		var unique = CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None);
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000, uniques: [unique]);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var prepared = result.Monsters[initial.Monsters.Count];

		Assert.Equal(1, prepared.Ai);
		Assert.Equal(8, prepared.AnimNumberOfFrames);
		Assert.Equal(4, prepared.AnimTicksPerFrame);
		Assert.InRange(prepared.AnimCurrentFrame, 0, 7);
		Assert.Equal(0U, prepared.Flags & 4U);
		Assert.Equal(0, prepared.Mode);
	}

	[Fact]
	public void IndependentPackPreservesNoLeaderRelation()
	{
		var (population, initial) = CreateInitialPopulation(includeMinionType: true);
		var unique = CreateUnique(1, 8, AuthoritativeCathedralUniquePack.Independent);
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000, uniques: [unique]);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var minion = result.Monsters[initial.Monsters.Count + 1];

		Assert.Equal(0, minion.LeaderRelation);
		Assert.Equal(255, minion.Leader);
	}

	[Fact]
	public void DensityUsesNonSolid80By80AreaAndScatterTypeOrder()
	{
		var (population, initial) = CreateInitialPopulation(includeMinionType: true);
		var parameters = CreateEncounter(population, initial, densityDivisor: 6400, scatterLevelTypes: [1]);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.Equal(initial.Monsters.Count + 1, result.Monsters.Count);
		Assert.Equal(1, result.Monsters[^1].LevelType);
		Assert.Equal(initial.Monsters.Count + 1, result.TotalMonsters);
		Assert.Contains(result.Monsters[^1].Slot + 1, result.MonsterTileRefs);
	}

	[Fact]
	public void ZeroAnimationDimensionsDoNotConsumeNativeRngDraws()
	{
		var (population, initial) = CreateInitialPopulation(includeMinionType: true, zeroAnimation: true);
		var parameters = CreateEncounter(population, initial, densityDivisor: 6400, scatterLevelTypes: [1]);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var expected = new DeterministicLegacyRandom(initial.FinalRandomState);
		expected.GenerateRnd(1); // scatter type
		expected.GenerateRnd(80); // candidate X
		expected.GenerateRnd(80); // candidate Y
		expected.GenerateRnd(8); // monster direction
		expected.GenerateRnd(6); // inclusive raw HP range 4..9
		expected.AdvanceSeed();
		expected.AdvanceSeed();
		expected.GenerateRnd(8); // first post-success walk roll
		expected.GenerateRnd(8); // second post-success walk roll

		Assert.Equal(expected.State, result.FinalRandomState);
		Assert.Equal(0, result.Monsters[^1].AnimTicksPerFrame);
		Assert.Equal(0, result.Monsters[^1].AnimNumberOfFrames);
		Assert.Equal(0, result.Monsters[^1].AnimTickCounter);
		Assert.Equal(0, result.Monsters[^1].AnimCurrentFrame);
	}

	[Fact]
	public void TriggerVisionUsesBlockLightAndIsRemovedAfterPlacement()
	{
		var (population, initial) = CreateInitialPopulation();
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			triggerPositions: [new AuthoritativeCathedralPopulationPoint(56, 56)],
			initialVisibility: new bool[AuthoritativeCathedralPopulationGeometry.CellCount],
			initialLit: Enumerable.Repeat(true, AuthoritativeCathedralPopulationGeometry.CellCount).ToArray());

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.DoesNotContain(true, result.Visibility);
		var centerIndex = 56 + 56 * AuthoritativeCathedralPopulationGeometry.Width;
		Assert.False(result.Lit[centerIndex]);
		Assert.Equal(0, result.TileFlags[centerIndex] & (byte)(0x02 | 0x40));
		Assert.True(result.Lit[0]);
	}

	[Fact]
	public void FullLightAllocatorReturnsNoLightWhenUniqueNeedsUnavailableSlot()
	{
		var (population, initial) = CreateInitialPopulation();
		var unique = CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None);
		var allocator = new AuthoritativeCathedralEncounterLightAllocator(
			Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights).ToArray(),
			AuthoritativeCathedralEncounterLightAllocator.MaximumLights);
		var parameters = CreateEncounter(population, initial, densityDivisor: 100_000, uniques: [unique], lightAllocator: allocator);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.Equal(-1, result.Monsters[initial.Monsters.Count].LightId);
		Assert.Equal(AuthoritativeCathedralEncounterLightAllocator.MaximumLights, result.ActiveLightCount);
	}

	[Fact]
	public void DisabledLightManagerReturnsNoLightWithoutAllocatingOrChangingHistory()
	{
		var (population, initial) = CreateInitialPopulation();
		var originalRecords = Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights)
			.Select(index => new AuthoritativeCathedralEncounterLightState(
				index, new(255, 254), new(253, 252), -2, 3, 7, 9, true, true))
			.ToArray();
		var allocator = new AuthoritativeCathedralEncounterLightAllocator(
			Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights).ToArray(),
			0, originalRecords, updateLighting: true, disableLighting: true);
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			uniques: [CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None)],
			lightAllocator: allocator);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.Equal(-1, result.Monsters[initial.Monsters.Count].LightId);
		Assert.Equal(0, result.ActiveLightCount);
		Assert.True(result.DisableLighting);
		Assert.True(result.UpdateLighting);
		Assert.Equal(allocator.ActiveLightSlots, result.ActiveLightSlots);
		Assert.Equal(originalRecords, result.LightRecords);
	}

	[Fact]
	public void PreservesFullMonsterPoolTileFlagsAndLightHistory()
	{
		var (population, initial) = CreateInitialPopulation();
		var slots = Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity)
			.Select(index => index < initial.Monsters.Count
				? initial.Monsters[index]
				: new AuthoritativeCathedralPopulationMonsterState {
					Slot = index,
					Temp = new AuthoritativeCathedralPopulationPoint(9, 9),
					GolemToHit = index == 199 ? 73 : 0,
				})
			.ToArray();
		var tileFlags = new byte[AuthoritativeCathedralPopulationGeometry.CellCount];
		tileFlags[0] = 0xF7;
		var initialVisibility = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		initialVisibility[0] = true;
		var initialLit = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		initialLit[0] = true;
		var lights = Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights)
			.Select(index => index == 31
				? new AuthoritativeCathedralEncounterLightState(
					index, new(255, 254), new(0, 255), sbyte.MinValue, sbyte.MaxValue, byte.MaxValue, 0, true, true)
				: new AuthoritativeCathedralEncounterLightState(
					index, new(2, 2), new(3, 3), 1, -1, 4, 5, true, true))
			.ToArray();
		var allocator = new AuthoritativeCathedralEncounterLightAllocator(
			Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights).ToArray(),
			0, lights);
		var unique = CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None);
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			uniques: [unique],
			initialMonsterSlots: slots,
			initialVisibility: initialVisibility,
			initialLit: initialLit,
			initialTileFlags: tileFlags,
			lightAllocator: allocator);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.Equal(AuthoritativeCathedralPopulationParameters.MonsterCapacity, result.MonsterSlots.Count);
		Assert.Equal(AuthoritativeCathedralPopulationParameters.MonsterCapacity, result.ActiveMonsterIds.Count);
		Assert.Equal(Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity), result.ActiveMonsterIds);
		Assert.Equal(initial.Monsters.Count + 1, result.ActiveMonsterCount);
		Assert.Equal(73, result.MonsterSlots[199].GolemToHit);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(9, 9), result.MonsterSlots[199].Temp);
		Assert.Equal(199, result.MonsterSlots[199].Slot);
		Assert.Equal(0xF7, result.TileFlags[0]);
		Assert.InRange(result.LightRecords[0].Tile.X, 16, 95);
		Assert.InRange(result.LightRecords[0].Tile.Y, 16, 95);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(3, 3), result.LightRecords[0].OldTile);
		Assert.Equal(5, result.LightRecords[0].OldRadius);
		Assert.Equal(3, result.LightRecords[0].Radius);
		Assert.False(result.LightRecords[0].IsInvalid);
		Assert.False(result.LightRecords[0].HasChanged);
		Assert.Equal(Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights), result.ActiveLightSlots);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(255, 254), result.LightRecords[31].Tile);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 255), result.LightRecords[31].OldTile);
		Assert.Equal(sbyte.MinValue, result.LightRecords[31].OffsetX);
		Assert.Equal(sbyte.MaxValue, result.LightRecords[31].OffsetY);
		Assert.Equal(byte.MaxValue, result.LightRecords[31].Radius);
		Assert.True(result.UpdateLighting);
	}

	[Fact]
	public void NativeVisionStopsAtBlockLightAndRetainsTransparencyRegionAfterUnvision()
	{
		var wall = new AuthoritativeCathedralPopulationPoint(11, 10);
		var (blockedPopulation, blockedInitial) = CreateInitialPopulation(
			blockLightCell: wall,
			transparencyCell: wall,
			transparencyValue: 5);
		var blockedParameters = CreateEncounter(
			blockedPopulation,
			blockedInitial,
			densityDivisor: 100_000,
			triggerPositions: [new AuthoritativeCathedralPopulationPoint(10, 10)]);

		var blockedResult = AuthoritativeCathedralEncounterGenerator.Generate(blockedParameters);

		Assert.DoesNotContain(true, blockedResult.Visibility);
		Assert.False(blockedResult.VisibleTransparencyRegions[5]);

		var (transparentPopulation, transparentInitial) = CreateInitialPopulation(
			transparencyCell: wall,
			transparencyValue: 5);
		var transparentParameters = CreateEncounter(
			transparentPopulation,
			transparentInitial,
			densityDivisor: 100_000,
			triggerPositions: [new AuthoritativeCathedralPopulationPoint(10, 10)]);

		var transparentResult = AuthoritativeCathedralEncounterGenerator.Generate(transparentParameters);

		Assert.DoesNotContain(true, transparentResult.Visibility);
		Assert.True(transparentResult.VisibleTransparencyRegions[5]);
	}

	[Theory]
	[InlineData(2, false)]
	[InlineData(2, true)]
	[InlineData(3, false)]
	[InlineData(3, true)]
	[InlineData(4, false)]
	[InlineData(4, true)]
	public void DepthTwoThroughFourUseNativeGroupRollRanges(int depth, bool grouped)
	{
		var (population, initial) = CreateInitialPopulation(depth: depth, includeMinionType: true);
		if (grouped) {
			var found = false;
			// Select a real preceding InitObjects result, not a fabricated RNG
			// boundary. Search is bounded and independent of encounter generation.
			for (uint seed = 0; seed < 32; seed++) {
				(population, initial) = CreateInitialPopulation(
					depth: depth, includeMinionType: true, populationSeed: seed);
				var probe = new DeterministicLegacyRandom(initial.FinalRandomState);
				probe.GenerateRnd(1);
				if (probe.GenerateRnd(2) == 0)
					continue;
				found = true;
				break;
			}
			Assert.True(found, "A bounded fixture seed must exercise the native group branch.");
		}

		var expected = new DeterministicLegacyRandom(initial.FinalRandomState);
		expected.GenerateRnd(1); // The one-entry scatter list still uses native GenerateRnd.
		var firstRoll = expected.GenerateRnd(2);
		Assert.Equal(grouped, firstRoll != 0);
		var expectedGroupSize = firstRoll == 0
			? 1
			: depth == 2 ? expected.GenerateRnd(2) + 2 : expected.GenerateRnd(3) + 3;
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 6400 / expectedGroupSize,
			scatterLevelTypes: [1]);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var transcript = SimulateScatterTranscript(population, initial, 6400 / expectedGroupSize);

		Assert.InRange(expectedGroupSize, grouped ? (depth == 2 ? 2 : 3) : 1, grouped ? (depth == 2 ? 3 : 5) : 1);
		Assert.Equal(initial.Monsters.Count + expectedGroupSize, result.Monsters.Count);
		Assert.Equal(expectedGroupSize, result.TotalMonsters - initial.Monsters.Count);
		Assert.Equal(transcript.FinalRandomState, result.FinalRandomState);
		Assert.Equal(transcript.AddedTiles, result.Monsters
			.Skip(initial.Monsters.Count)
			.Select(monster => monster.Tile));
	}

	[Fact]
	public void SparseUniqueUsesPlaceableCenterBeforeDrawingAnotherCandidate()
	{
		var (population, initial) = CreateInitialPopulation();
		var expected = new DeterministicLegacyRandom(initial.FinalRandomState);
		var candidate = new AuthoritativeCathedralPopulationPoint(
			expected.GenerateRnd(80) + 16,
			expected.GenerateRnd(80) + 16);
		var candidateIndex = candidate.X + candidate.Y * AuthoritativeCathedralPopulationGeometry.Width;
		Assert.Equal(0, initial.TileObjectRefs[candidateIndex]);

		var visibility = Enumerable.Repeat(true, AuthoritativeCathedralPopulationGeometry.CellCount).ToArray();
		visibility[candidateIndex] = false;
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			uniques: [CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None)],
			initialVisibility: visibility);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var prepared = Assert.Single(result.Monsters.Skip(initial.Monsters.Count));

		Assert.Equal(candidate, prepared.Tile);
		expected.GenerateRnd(8); // InitMonster direction.
		expected.GenerateRnd(3); // Stand tick counter.
		expected.GenerateRnd(7); // Stand frame.
		expected.GenerateRnd(6); // Inclusive 4..9 hit-point roll.
		expected.AdvanceSeed();
		expected.AdvanceSeed();
		expected.GenerateRnd(7); // PrepareUniqueMonst changes back to Stand.
		Assert.Equal(expected.State, result.FinalRandomState);
	}

	[Fact]
	public void ReusedNativeSlotResetsTouchedFieldsAndPreservesUntouchedFields()
	{
		var (population, initial) = CreateInitialPopulation(includeMinionType: true);
		var reusedSlot = initial.Monsters.Count;
		var sentinel = new AuthoritativeCathedralPopulationMonsterState {
			Slot = reusedSlot,
			Type = 999,
			LevelType = 998,
			Mode = 997,
			Goal = 996,
			GoalVar1 = 995,
			GoalVar2 = 994,
			GoalVar3 = 993,
			Var1 = 101,
			Var2 = 102,
			Var3 = 103,
			PathCount = 992,
			Tile = new(40, 41),
			Future = new(42, 43),
			Old = new(44, 45),
			Last = new(7, 8),
			Temp = new(9, 10),
			Direction = 991,
			Enemy = 990,
			EnemyPosition = new(11, 12),
			Invalid = true,
			Ai = 989,
			Intelligence = 988,
			ActiveForTicks = 987,
			UniqueTranslation = 77,
			CorpseId = 66,
			WhoHit = 986,
			MinDamage = 985,
			MaxDamage = 984,
			MinDamageSpecial = 983,
			MaxDamageSpecial = 982,
			ArmorClass = 981,
			Resistance = 980,
			TalkMessage = 979,
			Leader = 978,
			LeaderRelation = 977,
			PackSize = 55,
			Flags = uint.MaxValue,
			RndItemSeed = uint.MaxValue,
			AiSeed = uint.MaxValue,
			GolemToHit = 44,
			UniqueType = 999,
			LightId = 999,
			HitPoints = 976,
			MaxHitPoints = 975,
			AnimTicksPerFrame = 974,
			AnimTickCounter = 973,
			AnimNumberOfFrames = 972,
			AnimCurrentFrame = 971,
			ReducePlayerStrength = 970,
			ReducePlayerMagic = 969,
			ReducePlayerDexterity = 968,
			ReducePlayerVitality = 967,
			ReducePlayerMaxHP = 966,
			ReducePlayerMaxMana = 965,
		};
		var slots = Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity)
			.Select(index => index < initial.Monsters.Count
				? initial.Monsters[index]
				: index == reusedSlot
					? sentinel
					: new AuthoritativeCathedralPopulationMonsterState { Slot = index })
			.ToArray();
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 6400,
			scatterLevelTypes: [1],
			initialMonsterSlots: slots);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var reused = Assert.Single(result.Monsters.Skip(initial.Monsters.Count));

		Assert.Equal(reusedSlot, reused.Slot);
		Assert.Equal(8, reused.Type);
		Assert.Equal(1, reused.LevelType);
		Assert.Equal(0, reused.Mode);
		Assert.Equal(1, reused.Goal);
		Assert.Equal(0, reused.GoalVar1);
		Assert.Equal(0, reused.GoalVar2);
		Assert.Equal(0, reused.GoalVar3);
		Assert.Equal(0, reused.PathCount);
		Assert.Equal(0, reused.Enemy);
		Assert.False(reused.Invalid);
		Assert.InRange(reused.Direction, 0, 7);
		Assert.Equal(1, reused.Ai);
		Assert.Equal(1, reused.Intelligence);
		Assert.Equal(0, reused.ActiveForTicks);
		Assert.Equal(255, reused.UniqueType);
		Assert.Equal(255, reused.Leader);
		Assert.Equal(0, reused.LeaderRelation);
		Assert.Equal(-1, reused.TalkMessage);
		Assert.Equal(-1, reused.LightId);
		Assert.Equal(2, reused.MinDamage);
		Assert.Equal(4, reused.MaxDamage);
		Assert.Equal(2, reused.MinDamageSpecial);
		Assert.Equal(6, reused.MaxDamageSpecial);
		Assert.Equal(2, reused.ArmorClass);
		Assert.Equal(1, reused.Resistance);
		Assert.Equal(0U, reused.Flags);
		Assert.NotEqual(uint.MaxValue, reused.RndItemSeed);
		Assert.NotEqual(uint.MaxValue, reused.AiSeed);
		Assert.InRange(reused.HitPoints, 64, 288);
		Assert.Equal(reused.HitPoints, reused.MaxHitPoints);
		Assert.Equal(4, reused.AnimTicksPerFrame);
		Assert.InRange(reused.AnimTickCounter, 0, 3);
		Assert.Equal(8, reused.AnimNumberOfFrames);
		Assert.InRange(reused.AnimCurrentFrame, 0, 7);
		Assert.Equal(1, reused.ReducePlayerStrength);
		Assert.Equal(2, reused.ReducePlayerMagic);
		Assert.Equal(3, reused.ReducePlayerDexterity);
		Assert.Equal(4, reused.ReducePlayerVitality);
		Assert.Equal(5, reused.ReducePlayerMaxHP);
		Assert.Equal(6, reused.ReducePlayerMaxMana);
		Assert.Equal(101, reused.Var1);
		Assert.Equal(102, reused.Var2);
		Assert.Equal(103, reused.Var3);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(7, 8), reused.Last);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(9, 10), reused.Temp);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(11, 12), reused.EnemyPosition);
		Assert.Equal(77, reused.UniqueTranslation);
		Assert.Equal(66, reused.CorpseId);
		Assert.Equal(55, reused.PackSize);
		Assert.Equal(44, reused.GolemToHit);
		Assert.Equal(reused.Slot + 1, result.MonsterTileRefs[PointIndex(reused.Tile)]);
		Assert.Equal(reused.Tile, reused.Future);
		Assert.Equal(reused.Tile, reused.Old);
	}

	[Fact]
	public void FailedLeashedPackRetainsTheTenthPartialAttemptAndRollsBackRefs()
	{
		var (population, initial) = CreateInitialPopulation();
		var expected = new DeterministicLegacyRandom(initial.FinalRandomState);
		var leaderPosition = new AuthoritativeCathedralPopulationPoint(
			expected.GenerateRnd(80) + 16,
			expected.GenerateRnd(80) + 16);
		var leaderIndex = PointIndex(leaderPosition);
		Assert.Equal(0, initial.TileObjectRefs[leaderIndex]);

		var deltaX = new[] { 1, 0, -1, -1, -1, 0, 1, 1 };
		var deltaY = new[] { 1, 1, 1, 0, -1, -1, -1, 0 };
		var openNeighbors = Enumerable.Range(0, deltaX.Length)
			.Select(direction => new AuthoritativeCathedralPopulationPoint(
				leaderPosition.X + deltaX[direction], leaderPosition.Y + deltaY[direction]))
			.Where(position => position.X is >= 0 and < AuthoritativeCathedralPopulationGeometry.Width
				&& position.Y is >= 0 and < AuthoritativeCathedralPopulationGeometry.Width
				&& initial.TileObjectRefs[PointIndex(position)] == 0)
			.Take(7)
			.ToArray();
		Assert.NotEmpty(openNeighbors);

		var visibility = Enumerable.Repeat(true, AuthoritativeCathedralPopulationGeometry.CellCount).ToArray();
		visibility[leaderIndex] = false;
		foreach (var neighbor in openNeighbors)
			visibility[PointIndex(neighbor)] = false;
		// The eighth minion slot is never reachable: the leader plus these seven
		// placeable cells leave one requested slot unused even while retries roll
		// back.  Keeping a sentinel there proves rollback does not rewrite an
		// untouched native slot.
		var reusedSlot = initial.Monsters.Count + 8;
		var sentinel = new AuthoritativeCathedralPopulationMonsterState {
			Slot = reusedSlot,
			Var1 = 201,
			Var2 = 202,
			Var3 = 203,
			Last = new AuthoritativeCathedralPopulationPoint(17, 18),
			Temp = new AuthoritativeCathedralPopulationPoint(19, 20),
			EnemyPosition = new AuthoritativeCathedralPopulationPoint(21, 22),
			UniqueTranslation = 87,
			CorpseId = 86,
			PackSize = 85,
			GolemToHit = 84,
		};
		var slots = Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity)
			.Select(index => index < initial.Monsters.Count
				? initial.Monsters[index]
				: index == reusedSlot
					? sentinel
					: new AuthoritativeCathedralPopulationMonsterState { Slot = index })
			.ToArray();
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			uniques: [CreateUnique(1, 109, AuthoritativeCathedralUniquePack.Leashed)],
			initialVisibility: visibility,
			initialMonsterSlots: slots);

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);
		var leader = result.Monsters[initial.Monsters.Count];
		var transcript = SimulateLeashedPackTranscript(population, initial, visibility);

		Assert.Equal(10, transcript.RetryCount);
		Assert.Equal(transcript.FinalRandomState, result.FinalRandomState);
		Assert.Equal(transcript.ActiveMonsterCount, result.ActiveMonsterCount);
		Assert.Equal(transcript.ActiveTiles, result.Monsters
			.Skip(initial.Monsters.Count)
			.Select(monster => monster.Tile));
		Assert.Equal(transcript.MonsterTileRefs, result.MonsterTileRefs);
		Assert.InRange(leader.PackSize, 1, 7);
		Assert.Equal(initial.Monsters.Count + 1 + leader.PackSize, result.ActiveMonsterCount);
		Assert.True(result.ActiveMonsterCount < initial.Monsters.Count + 9);
		Assert.Equal(initial.Monsters.Count + 1, result.MonsterTileRefs[leaderIndex]);
		Assert.Equal(sentinel, result.MonsterSlots[reusedSlot]);
		var retained = result.MonsterSlots[reusedSlot];
		Assert.Equal(201, retained.Var1);
		Assert.Equal(202, retained.Var2);
		Assert.Equal(203, retained.Var3);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(17, 18), retained.Last);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(19, 20), retained.Temp);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(21, 22), retained.EnemyPosition);
		Assert.Equal(87, retained.UniqueTranslation);
		Assert.Equal(86, retained.CorpseId);
		Assert.Equal(85, retained.PackSize);
		Assert.Equal(84, retained.GolemToHit);
		var newlyPlaced = result.Monsters.Skip(initial.Monsters.Count).ToArray();
		Assert.Equal(newlyPlaced.Length, result.MonsterTileRefs.Count(value => value != 0));
		Assert.DoesNotContain(result.MonsterTileRefs, value => value < 0 || value > result.ActiveMonsterCount);
		foreach (var monster in newlyPlaced)
			Assert.Equal(monster.Slot + 1, result.MonsterTileRefs[PointIndex(monster.Tile)]);
	}

	[Fact]
	public void RejectsMalformedEncounterPrerequisites()
	{
		var (population, initial) = CreateInitialPopulation();
		var refs = new int[AuthoritativeCathedralPopulationGeometry.CellCount];
		refs[0] = 201;
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralEncounterParameters(
			population, initial, refs, new bool[refs.Length], [], [], [], 1, CreateLightAllocator()));
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralEncounterParameters(
			population, initial, new int[refs.Length], new bool[refs.Length - 1], [], [], [], 1, CreateLightAllocator()));
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralEncounterParameters(
			population, initial, new int[refs.Length], new bool[refs.Length],
			[new AuthoritativeCathedralPopulationPoint(0, 0)], [], [], 1, CreateLightAllocator()));
	}

	private sealed record ScatterTranscript(
		uint FinalRandomState,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> AddedTiles);

	private static ScatterTranscript SimulateScatterTranscript(
		AuthoritativeCathedralPopulationParameters population,
		AuthoritativeCathedralPopulationResult initial,
		int densityDivisor)
	{
		const byte tilePopulated = 1 << 3;
		var random = new DeterministicLegacyRandom(initial.FinalRandomState);
		var tileRefs = new int[AuthoritativeCathedralPopulationGeometry.CellCount];
		var objectRefs = initial.TileObjectRefs.ToArray();
		var visibility = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		var tileFlags = population.Geometry.Populated
			.Select(value => value ? tilePopulated : (byte)0)
			.ToArray();
		var slots = initial.Monsters.Select(monster => monster.Tile).ToList();
		var activeCount = initial.Monsters.Count;
		var walkable = Enumerable.Range(16, 80)
			.SelectMany(x => Enumerable.Range(16, 80).Select(y => x + y * AuthoritativeCathedralPopulationGeometry.Width))
			.Count(index => (population.Geometry.PieceProperties[initial.Pieces[index]] & 1) == 0);
		var targetCount = Math.Min(
			activeCount + walkable / densityDivisor,
			AuthoritativeCathedralPopulationParameters.MonsterCapacity - 10);
		while (activeCount < targetCount) {
			random.GenerateRnd(1); // One ordered scatter type.
			var requested = population.Depth == 1 || random.GenerateRnd(2) == 0
				? 1
				: population.Depth == 2 ? random.GenerateRnd(2) + 2 : random.GenerateRnd(3) + 3;
			var groupCount = Math.Min(requested, targetCount - activeCount);
			var placed = 0;
			for (var retry = 0; retry < 10; retry++) {
				while (placed != 0) {
					activeCount--;
					placed--;
					tileRefs[PointIndex(slots[activeCount])] = 0;
				}
				var origin = RequireOpenPosition(random, tileRefs, objectRefs, visibility, tileFlags, population);
				var x = origin.X;
				var y = origin.Y;
				var placedThisTry = 0;
				for (var tries = 0; placedThisTry < groupCount && tries < 100; AdvanceTestGroupPosition(random, ref x, ref y)) {
					var position = new AuthoritativeCathedralPopulationPoint(x, y);
					if (!CanPlaceTestMonster(position, tileRefs, objectRefs, visibility, tileFlags, population)) {
						tries++;
						continue;
					}
					ConsumeTestMonsterRng(random, population.OrderedBestiary[1]);
					if (slots.Count <= activeCount)
						slots.Add(position);
					else
						slots[activeCount] = position;
					tileRefs[PointIndex(position)] = activeCount + 1;
					activeCount++;
					placed++;
					placedThisTry++;
				}
				if (placed >= groupCount)
					break;
			}
		}
		return new(
			random.State,
			slots.Skip(initial.Monsters.Count).Take(activeCount - initial.Monsters.Count).ToArray());
	}

	private static AuthoritativeCathedralPopulationPoint RequireOpenPosition(
		DeterministicLegacyRandom random,
		IReadOnlyList<int> tileRefs,
		IReadOnlyList<int> objectRefs,
		IReadOnlyList<bool> visibility,
		IReadOnlyList<byte> tileFlags,
		AuthoritativeCathedralPopulationParameters population)
	{
		for (var attempt = 0; attempt < 20_000; attempt++) {
			var position = new AuthoritativeCathedralPopulationPoint(
				random.GenerateRnd(80) + 16,
				random.GenerateRnd(80) + 16);
			if (CanPlaceTestMonster(position, tileRefs, objectRefs, visibility, tileFlags, population))
				return position;
		}
		throw new InvalidDataException("Portable scatter transcript exhausted its placement bound.");
	}

	private static bool CanPlaceTestMonster(
		AuthoritativeCathedralPopulationPoint position,
		IReadOnlyList<int> tileRefs,
		IReadOnlyList<int> objectRefs,
		IReadOnlyList<bool> visibility,
		IReadOnlyList<byte> tileFlags,
		AuthoritativeCathedralPopulationParameters population)
	{
		if (position.X is < 0 or >= AuthoritativeCathedralPopulationGeometry.Width
			|| position.Y is < 0 or >= AuthoritativeCathedralPopulationGeometry.Width)
			return false;
		var index = PointIndex(position);
		return tileRefs[index] == 0
			&& objectRefs[index] == 0
			&& !visibility[index]
			&& (tileFlags[index] & (1 << 3)) == 0
			&& (population.Geometry.PieceProperties[population.Geometry.Pieces[index]] & 1) == 0;
	}

	private static void ConsumeTestMonsterRng(
		DeterministicLegacyRandom random,
		AuthoritativeCathedralPopulationMonsterDefinition data)
	{
		random.GenerateRnd(8);
		random.GenerateRnd(data.Rates[0] - 1);
		random.GenerateRnd(data.Frames[0] - 1);
		random.RandomIntLessThan(data.MaximumHitPoints - data.MinimumHitPoints + 1);
		random.AdvanceSeed();
		random.AdvanceSeed();
	}

	private static void AdvanceTestGroupPosition(DeterministicLegacyRandom random, ref int x, ref int y)
	{
		var horizontal = random.GenerateRnd(8);
		var vertical = random.GenerateRnd(8);
		var deltaX = new[] { 1, 0, -1, -1, -1, 0, 1, 1 };
		x += deltaX[horizontal];
		y += deltaX[vertical];
	}

	private sealed record LeashedPackTranscript(
		uint FinalRandomState,
		int RetryCount,
		int ActiveMonsterCount,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> ActiveTiles,
		IReadOnlyList<int> MonsterTileRefs);

	/**
	 * A deliberately narrow source transcript for the constrained pack fixture.
	 * It models only the native candidate, retry, and RNG order; all fields and
	 * admission rules remain exercised by the production generator itself.
	 */
	private static LeashedPackTranscript SimulateLeashedPackTranscript(
		AuthoritativeCathedralPopulationParameters population,
		AuthoritativeCathedralPopulationResult initial,
		IReadOnlyList<bool> visibility)
	{
		var random = new DeterministicLegacyRandom(initial.FinalRandomState);
		var refs = new int[AuthoritativeCathedralPopulationGeometry.CellCount];
		var objectRefs = initial.TileObjectRefs;
		var slots = initial.Monsters.Select(monster => monster.Tile).ToList();
		var activeCount = initial.Monsters.Count;
		var leaderPosition = SimulateUniquePosition(population, random, refs, objectRefs, visibility);
		ConsumeTestMonsterRng(random, population.OrderedBestiary[0]);
		SetTranscriptMonster(slots, refs, ref activeCount, leaderPosition);

		const int requested = 8;
		var placed = 0;
		var retries = 0;
		for (; retries < 10; retries++) {
			while (placed != 0) {
				activeCount--;
				placed--;
				refs[PointIndex(slots[activeCount])] = 0;
			}

			var direction = random.GenerateRnd(8);
			var origin = AddTestDirection(leaderPosition, direction);
			var x = origin.X;
			var y = origin.Y;
			var placedThisTry = 0;
			for (var tries = 0; placedThisTry < requested && tries < 100; AdvanceTestGroupPosition(random, ref x, ref y)) {
				var position = new AuthoritativeCathedralPopulationPoint(x, y);
				if (!CanPlaceTranscriptMonster(position, population, refs, objectRefs, visibility)
					|| !ContainsTest(origin)
					|| population.Geometry.Transparency[PointIndex(position)]
						!= population.Geometry.Transparency[PointIndex(origin)]
					|| Math.Abs(x - origin.X) >= 4
					|| Math.Abs(y - origin.Y) >= 4) {
					tries++;
					continue;
				}

				ConsumeTestMonsterRng(random, population.OrderedBestiary[0]);
				// PlaceGroup restores ordinary minions directly to Stand animation.
				random.GenerateRnd(population.OrderedBestiary[0].Frames[0] - 1);
				SetTranscriptMonster(slots, refs, ref activeCount, position);
				placed++;
				placedThisTry++;
			}
			if (placed >= requested)
				break;
		}

		// PlaceUniqueMonsters restores the leader directly to Stand animation.
		random.GenerateRnd(population.OrderedBestiary[0].Frames[0] - 1);
		return new(
			random.State,
			retries,
			activeCount,
			slots.Skip(initial.Monsters.Count).Take(activeCount - initial.Monsters.Count).ToArray(),
			refs);
	}

	private static AuthoritativeCathedralPopulationPoint SimulateUniquePosition(
		AuthoritativeCathedralPopulationParameters population,
		DeterministicLegacyRandom random,
		IReadOnlyList<int> refs,
		IReadOnlyList<int> objectRefs,
		IReadOnlyList<bool> visibility)
	{
		var sparseAttempts = 0;
		for (var attempt = 0; attempt < population.MaximumPlacementAttempts; attempt++) {
			var position = new AuthoritativeCathedralPopulationPoint(
				random.GenerateRnd(80) + 16,
				random.GenerateRnd(80) + 16);
			var availableAround = 0;
			for (var x = position.X - 3; x < position.X + 3; x++)
				for (var y = position.Y - 3; y < position.Y + 3; y++)
					if (ContainsTest(new(x, y))
						&& CanPlaceTranscriptMonster(new(x, y), population, refs, objectRefs, visibility))
						availableAround++;
			var canPlace = CanPlaceTranscriptMonster(position, population, refs, objectRefs, visibility);
			if (availableAround < 9 && ++sparseAttempts < 1000 && !canPlace)
				continue;
			if (canPlace)
				return position;
		}
		throw new InvalidDataException("Portable unique transcript exhausted its placement bound.");
	}

	private static bool CanPlaceTranscriptMonster(
		AuthoritativeCathedralPopulationPoint position,
		AuthoritativeCathedralPopulationParameters population,
		IReadOnlyList<int> refs,
		IReadOnlyList<int> objectRefs,
		IReadOnlyList<bool> visibility)
	{
		if (!ContainsTest(position))
			return false;
		var index = PointIndex(position);
		return refs[index] == 0
			&& objectRefs[index] == 0
			&& !population.Geometry.PlayerOccupied[index]
			&& !visibility[index]
			&& !population.Geometry.Populated[index]
			&& (population.Geometry.PieceProperties[population.Geometry.Pieces[index]] & 1) == 0;
	}

	private static void SetTranscriptMonster(
		IList<AuthoritativeCathedralPopulationPoint> slots,
		IList<int> refs,
		ref int activeCount,
		AuthoritativeCathedralPopulationPoint position)
	{
		if (slots.Count <= activeCount)
			slots.Add(position);
		else
			slots[activeCount] = position;
		refs[PointIndex(position)] = activeCount + 1;
		activeCount++;
	}

	private static AuthoritativeCathedralPopulationPoint AddTestDirection(
		AuthoritativeCathedralPopulationPoint origin,
		int direction)
	{
		var deltaX = new[] { 1, 0, -1, -1, -1, 0, 1, 1 };
		var deltaY = new[] { 1, 1, 1, 0, -1, -1, -1, 0 };
		return new(origin.X + deltaX[direction], origin.Y + deltaY[direction]);
	}

	private static bool ContainsTest(AuthoritativeCathedralPopulationPoint position)
		=> position.X is >= 0 and < AuthoritativeCathedralPopulationGeometry.Width
			&& position.Y is >= 0 and < AuthoritativeCathedralPopulationGeometry.Width;

	private static int PointIndex(AuthoritativeCathedralPopulationPoint position)
		=> position.X + position.Y * AuthoritativeCathedralPopulationGeometry.Width;

	[Fact]
	public void DetachesEncounterInputsAndOutputs()
	{
		var (population, initial) = CreateInitialPopulation();
		var refs = new int[AuthoritativeCathedralPopulationGeometry.CellCount];
		var visible = new bool[refs.Length];
		var visibleTransparencyRegions = new bool[256];
		visibleTransparencyRegions[7] = true;
		var parameters = CreateEncounter(
			population,
			initial,
			densityDivisor: 100_000,
			initialMonsterRefs: refs,
			initialVisibility: visible,
			initialVisibleTransparencyRegions: visibleTransparencyRegions);
		refs[0] = 1;
		visible[0] = true;
		visibleTransparencyRegions[7] = false;

		var result = AuthoritativeCathedralEncounterGenerator.Generate(parameters);

		Assert.Equal(0, parameters.InitialMonsterTileRefs[0]);
		Assert.False(parameters.InitialVisibility[0]);
		Assert.True(parameters.InitialVisibleTransparencyRegions[7]);
		Assert.True(result.VisibleTransparencyRegions[7]);
		Assert.Throws<NotSupportedException>(() => ((IList<int>)result.MonsterTileRefs)[0] = 1);
		Assert.Throws<NotSupportedException>(() => ((IList<bool>)result.Visibility)[0] = true);
		Assert.Throws<NotSupportedException>(() => ((IList<bool>)result.VisibleTransparencyRegions)[0] = true);
	}

	[Theory]
	[InlineData(1457187811U, 37, 66, 3651446062U)]
	[InlineData(988045466U, 63, 37, 2567911961U)]
	public void SignedMinimumUniqueCandidateConsumesBothCoordinatesAndRetries(uint seed, int x, int y, uint expectedState)
	{
		var (population, initial) = CreateInitialPopulation();
		// Inverse LCG seeds put signed INT_MIN in candidate X or Y. Native
		// consumes the complete invalid pair, then retries both coordinates.
		initial = WithEncounterBoundarySeed(initial, seed);
		Assert.Equal(0, initial.TileObjectRefs[x + y * 112]);
		var result = AuthoritativeCathedralEncounterGenerator.Generate(CreateEncounter(
			population, initial, 100_000,
			uniques: [CreateUnique(1, 109, AuthoritativeCathedralUniquePack.None)]));
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(x, y), result.Monsters[^1].Tile);
		Assert.Equal(expectedState, result.FinalRandomState);
	}

	[Theory]
	[InlineData(1, 988045466U)]
	[InlineData(2, 4203210069U)]
	[InlineData(3, 4203210069U)]
	[InlineData(4, 4203210069U)]
	public void SignedMinimumScatterCandidateRetriesAfterTypeAndDepthDraws(int depth, uint seed)
	{
		var (population, initial) = CreateInitialPopulation(depth: depth, includeMinionType: true);
		initial = WithEncounterBoundarySeed(initial, seed);
		Assert.Equal(0, initial.TileObjectRefs[37 + 66 * 112]);
		var result = AuthoritativeCathedralEncounterGenerator.Generate(CreateEncounter(
			population, initial, 6400, scatterLevelTypes: [1]));
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(37, 66), result.Monsters[^1].Tile);
		Assert.Equal(2039887495U, result.FinalRandomState);
		Assert.Equal(initial.Monsters.Count + 1, result.ActiveMonsterCount);
	}

	private static AuthoritativeCathedralPopulationResult WithEncounterBoundarySeed(
		AuthoritativeCathedralPopulationResult initial, uint seed)
		=> new(initial.Objects.ToArray(), initial.ActiveObjectCount,
			initial.ActiveObjectIds.ToArray(), initial.AvailableObjectIds.ToArray(),
			initial.TileObjectRefs.ToArray(), initial.Monsters.ToArray(), initial.Pieces.ToArray(),
			initial.Special.ToArray(), initial.Transparency.ToArray(), initial.RandomAfterGolems, seed);

	private static AuthoritativeCathedralEncounterParameters CreateEncounter(
		AuthoritativeCathedralPopulationParameters population,
		AuthoritativeCathedralPopulationResult initial,
		int densityDivisor,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? uniques = null,
		IReadOnlyList<int>? scatterLevelTypes = null,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint>? triggerPositions = null,
		IReadOnlyList<bool>? initialVisibility = null,
		IReadOnlyList<int>? initialMonsterRefs = null,
		AuthoritativeCathedralEncounterLightAllocator? lightAllocator = null,
		IReadOnlyList<bool>? initialVisibleTransparencyRegions = null,
		IReadOnlyList<AuthoritativeCathedralPopulationMonsterState>? initialMonsterSlots = null,
		IReadOnlyList<int>? initialActiveMonsterIds = null,
		int? initialActiveMonsterCount = null,
		IReadOnlyList<bool>? initialLit = null,
		IReadOnlyList<byte>? initialTileFlags = null)
	{
		var slots = initialMonsterSlots ?? Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity)
			.Select(index => index < initial.Monsters.Count
				? initial.Monsters[index]
				: new AuthoritativeCathedralPopulationMonsterState { Slot = index })
			.ToArray();
		var activeIds = initialActiveMonsterIds
			?? Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity).ToArray();
		return new(
			population,
			initial,
			initialMonsterRefs ?? new int[AuthoritativeCathedralPopulationGeometry.CellCount],
			initialVisibility ?? new bool[AuthoritativeCathedralPopulationGeometry.CellCount],
			triggerPositions ?? [],
			scatterLevelTypes ?? [],
			uniques ?? [],
			densityDivisor,
			lightAllocator ?? CreateLightAllocator(),
			maximumPlacementAttempts: 20_000,
			initialVisibleTransparencyRegions: initialVisibleTransparencyRegions,
			initialMonsterSlots: slots,
			initialActiveMonsterIds: activeIds,
			initialActiveMonsterCount: initialActiveMonsterCount ?? initial.Monsters.Count,
			initialLit: initialLit,
			initialTileFlags: initialTileFlags);
	}

	private static AuthoritativeCathedralEncounterLightAllocator CreateLightAllocator()
		=> new(Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights).ToArray(), 0);

	private static AuthoritativeCathedralUniqueDefinition CreateUnique(
		int nativeUniqueId,
		int monsterType,
		AuthoritativeCathedralUniquePack pack,
		int customToHit = 0,
		int talkMessage = -1,
		int dungeonLevel = 1)
		=> new(
			nativeUniqueId, monsterType, dungeonLevel, 20, 1, 5, 2, 4, 1, 1, 2, 3, 4, 5, 6,
			pack, customToHit, 0, talkMessage, false);

	private static (AuthoritativeCathedralPopulationParameters Parameters, AuthoritativeCathedralPopulationResult Result) CreateInitialPopulation(
		bool includeMinionType = false,
		bool includeNakrulType = false,
		bool zeroAnimation = false,
		int golemAi = 1,
		int depth = 1,
		uint populationSeed = 2588,
		byte piecePropertyFlags = 0,
		byte transparencyValue = 0,
		AuthoritativeCathedralPopulationPoint? blockLightCell = null,
		AuthoritativeCathedralPopulationPoint? transparencyCell = null)
	{
		var pieces = new ushort[AuthoritativeCathedralPopulationGeometry.CellCount];
		if (blockLightCell is { } blocked) {
			pieces[blocked.X + blocked.Y * AuthoritativeCathedralPopulationGeometry.Width] = 1;
		}
		var transparency = new byte[AuthoritativeCathedralPopulationGeometry.CellCount];
		if (transparencyCell is { } transparent)
			transparency[transparent.X + transparent.Y * AuthoritativeCathedralPopulationGeometry.Width] = transparencyValue;
		else
			Array.Fill(transparency, transparencyValue);
		var pieceProperties = CreatePieceProperties(piecePropertyFlags);
		if (blockLightCell is not null)
			pieceProperties[1] = 0x02;
		var geometry = new AuthoritativeCathedralPopulationGeometry(
			pieces,
			new byte[AuthoritativeCathedralPopulationGeometry.CellCount],
			transparency,
			pieceProperties,
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount],
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount],
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount]);
		var objects = Enumerable.Range(0, 109)
			.Select(index => new AuthoritativeCathedralPopulationObjectDefinition(index, 0, 0, 0, 0, 0))
			.ToArray();
		var bestiary = new List<AuthoritativeCathedralPopulationMonsterDefinition> {
			CreateMonster(109, false, zeroAnimation, golemAi),
		};
		if (includeMinionType)
			bestiary.Add(CreateMonster(8, true, zeroAnimation));
		if (includeNakrulType)
			bestiary.Add(CreateMonster(137, false, zeroAnimation));
		var parameters = new AuthoritativeCathedralPopulationParameters(
			depth, populationSeed, 743271966, geometry, objects, bestiary,
			new AuthoritativeCathedralPopulationPlayer(0, 1, false, false, new(1, 1), new(1, 1)),
			0, [0, 1, 2], maximumPlacementAttempts: 20_000);
		return (parameters, AuthoritativeCathedralPopulationGenerator.Generate(parameters));
	}

	private static byte[] CreatePieceProperties(byte piecePropertyFlags)
	{
		var properties = new byte[AuthoritativeCathedralPopulationGeometry.PieceCount];
		properties[0] = piecePropertyFlags;
		return properties;
	}

	private static AuthoritativeCathedralPopulationMonsterDefinition CreateMonster(
		int nativeType,
		bool skeleton,
		bool zeroAnimation = false,
		int ai = 1)
		=> new(
			nativeType, skeleton, 4, 9, ai, 1, 0, 2, 4, 2, 6, 2, 1, 1, 2, 3, 4, 5, 6,
			zeroAnimation ? new int[6] : new[] { 8, 7, 6, 5, 4, 0 },
			zeroAnimation ? new int[6] : new[] { 4, 3, 2, 1, 5, 0 });
}
