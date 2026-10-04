using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Portable invariants for the bounded ordinary-Diablo Cathedral population phase. */
public sealed class AuthoritativeCathedralPopulationTests
{
	[Fact]
	public void GeneratesExactlyFourGolemsBeforeObjectsAndPreservesNativeFixedPointShape()
	{
		var parameters = CreateParameters(includeSkeleton: false);
		var result = AuthoritativeCathedralPopulationGenerator.Generate(parameters);

		Assert.Equal(4, result.Monsters.Count);
		Assert.All(result.Monsters, monster => {
			Assert.Equal(AuthoritativeCathedralPopulationParameters.GolemNativeType, monster.Type);
			Assert.Equal(0, monster.LevelType);
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(1, 0), monster.Tile);
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(1, 0), monster.Future);
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(1, 0), monster.Old);
			Assert.Equal(monster.MaxHitPoints, monster.HitPoints);
			Assert.InRange(monster.MaxHitPoints, 128, 288);
			Assert.InRange(monster.MinDamageSpecial, 0, 255);
			Assert.InRange(monster.MaxDamageSpecial, 0, 255);
			Assert.Equal(1, monster.Ai);
			Assert.Equal(1, monster.Intelligence);
			Assert.Equal(2, monster.MinDamage);
			Assert.Equal(4, monster.MaxDamage);
			Assert.Equal(1, monster.ReducePlayerStrength);
			Assert.Equal(2, monster.ReducePlayerMagic);
			Assert.Equal(3, monster.ReducePlayerDexterity);
			Assert.Equal(4, monster.ReducePlayerVitality);
			Assert.Equal(5, monster.ReducePlayerMaxHP);
			Assert.Equal(6, monster.ReducePlayerMaxMana);
			Assert.Equal(255, monster.UniqueType);
			Assert.Equal(255, monster.Leader);
			Assert.Equal(0, monster.LeaderRelation);
			Assert.Equal(-1, monster.LightId);
			Assert.Equal(-1, monster.TalkMessage);
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 0), monster.Last);
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 0), monster.Temp);
			Assert.Equal(0, monster.GolemToHit);
		});
		Assert.NotEqual(parameters.PopulationSeed, result.RandomAfterGolems);
		Assert.All(result.Monsters, monster => {
			Assert.Equal(8, monster.AnimNumberOfFrames);
			Assert.Equal(4, monster.AnimTicksPerFrame);
		});
	}

	[Fact]
	public void GolemRngConsumesStandAnimationBeforeQ6HitPointsThenItemAndAiSeeds()
	{
		var parameters = CreateParameters(includeSkeleton: false, populationSeed: 0x0BAD_C0DE);
		var result = AuthoritativeCathedralPopulationGenerator.Generate(parameters);
		var expected = new DeterministicLegacyRandom(parameters.PopulationSeed);
		var expectedRawHitPoints = 0;
		for (var index = 0; index < 4; index++) {
			expected.GenerateRnd(4 - 1);
			expected.GenerateRnd(8 - 1);
			var rawHitPoints = expected.GenerateRnd(9 - 4 + 1) + 4;
			if (index == 0) expectedRawHitPoints = rawHitPoints;
			expected.AdvanceRndSeed();
			expected.AdvanceRndSeed();
		}

		Assert.Equal(expected.State, result.RandomAfterGolems);
		Assert.Equal(Math.Max((expectedRawHitPoints << 6) / 2, 64), result.Monsters[0].MaxHitPoints);
		Assert.Equal(result.Monsters[0].MaxHitPoints, result.Monsters[0].HitPoints);
		Assert.Equal(4, result.Monsters[0].AnimTicksPerFrame);
		Assert.Equal(8, result.Monsters[0].AnimNumberOfFrames);
		Assert.InRange(result.Monsters[0].AnimTickCounter, 0, 3);
		Assert.InRange(result.Monsters[0].AnimCurrentFrame, 0, 7);
	}

	[Fact]
	public void ZeroStandRateAndFrameDoNotAdvanceGolemRng()
	{
		var parameters = CreateParameters(includeSkeleton: false, standFrames: 0, standRates: 0);
		var result = AuthoritativeCathedralPopulationGenerator.Generate(parameters);
		var expected = new DeterministicLegacyRandom(parameters.PopulationSeed);
		for (var index = 0; index < 4; index++) {
			expected.GenerateRnd(9 - 4 + 1);
			expected.AdvanceRndSeed();
			expected.AdvanceRndSeed();
		}
		Assert.Equal(expected.State, result.RandomAfterGolems);
		Assert.Equal(0, result.Monsters[0].AnimTicksPerFrame);
		Assert.Equal(0, result.Monsters[0].AnimNumberOfFrames);
	}

	[Fact]
	public void DoesNotInventASkeletonWhenOrderedBestiaryHasNoSkeleton()
	{
		var result = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters(includeSkeleton: false));

		Assert.Equal(4, result.Monsters.Count);
		Assert.DoesNotContain(result.Monsters, monster => monster.Type is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27);
	}

	[Fact]
	public void SelectsSkeletonsOnlyFromTheOrderedBestiaryAndUsesStartStandState()
	{
		var result = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters(includeSecondSkeleton: true));

		var skeletons = result.Monsters.Where(monster => monster.Type is 8 or 9).ToArray();
		Assert.All(skeletons, skeleton => {
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 0), skeleton.Tile);
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 0), skeleton.Future);
			Assert.Equal(0, skeleton.AnimCurrentFrame);
			Assert.Equal(0, skeleton.AnimTickCounter);
		});
		Assert.All(result.Monsters, monster => Assert.Contains(monster.Type, new[] { 8, 9, 109 }));
	}

	[Fact]
	public void OrderedSkeletonCandidatesChangeTheSelectedNativeTypeWithoutChangingTheRngPath()
	{
		var normal = AuthoritativeCathedralPopulationGenerator.Generate(
			CreateParameters(includeSecondSkeleton: true));
		var reversed = AuthoritativeCathedralPopulationGenerator.Generate(
			CreateParameters(includeSecondSkeleton: true, reverseSkeletonOrder: true));
		var normalSkeletons = normal.Monsters.Where(monster => monster.Type is 8 or 9).ToArray();
		var reversedSkeletons = reversed.Monsters.Where(monster => monster.Type is 8 or 9).ToArray();

		Assert.NotEmpty(normalSkeletons);
		Assert.Equal(normalSkeletons.Length, reversedSkeletons.Length);
		Assert.NotEqual(normalSkeletons[0].Type, reversedSkeletons[0].Type);
		Assert.Equal(normal.FinalRandomState, reversed.FinalRandomState);
	}

	[Fact]
	public void RetainsNativeObjectPoolTailsAndOneSignedTileReferenceArray()
	{
		var result = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters());

		Assert.Equal(AuthoritativeCathedralPopulationParameters.ObjectCapacity, result.ActiveObjectIds.Count);
		Assert.Equal(AuthoritativeCathedralPopulationParameters.ObjectCapacity, result.AvailableObjectIds.Count);
		Assert.Equal(AuthoritativeCathedralPopulationGeometry.CellCount, result.TileObjectRefs.Count);
		Assert.Equal(result.ActiveObjectCount, result.ActiveObjectIds.Take(result.ActiveObjectCount).Count(id => id >= 0));
		Assert.All(result.ActiveObjectIds.Take(result.ActiveObjectCount), id => Assert.InRange(id, 0, 126));
		Assert.All(result.ActiveObjectIds.Skip(result.ActiveObjectCount), id => Assert.Equal(0, id));
		Assert.Equal(0, result.ActiveObjectIds[0]);
		var expectedActive = new[] { 0 }.Concat(Enumerable.Range(128 - result.ActiveObjectCount, result.ActiveObjectCount - 1).Reverse());
		Assert.Equal(expectedActive, result.ActiveObjectIds.Take(result.ActiveObjectCount));
		Assert.Equal(127 - result.ActiveObjectCount, result.AvailableObjectIds[0]);
		Assert.Equal(Enumerable.Range(1, 126), result.AvailableObjectIds.Skip(1));
		Assert.Contains(result.TileObjectRefs, value => value < 0);
		Assert.Contains(result.TileObjectRefs, value => value > 0);
	}

	[Fact]
	public void FullObjectPoolKeepsItsFinalAvailableHeadAndSuppressesFurtherAllocations()
	{
		var pieces = Enumerable.Repeat((ushort)269, AuthoritativeCathedralPopulationGeometry.CellCount).ToArray();
		var result = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters(
			geometry: CreateGeometry(pieces: pieces), includeSkeleton: false));

		Assert.Equal(127, result.ActiveObjectCount);
		Assert.Equal(new[] { 0 }.Concat(Enumerable.Range(1, 126).Reverse()), result.ActiveObjectIds);
		Assert.Equal(1, result.AvailableObjectIds[0]);
		Assert.Equal(Enumerable.Range(1, 126), result.AvailableObjectIds.Skip(1));
		Assert.Equal(127, result.Objects.Count(item => item.Type >= 0));
		Assert.Equal(4, result.Monsters.Count);
		Assert.DoesNotContain(result.Objects, item => item.Type is 5 or 6 or 7 or 57 or 58);
	}

	[Fact]
	public void NonTrapDefinitionsIgnoreTrapPercentageWithoutChangingRng()
	{
		var zero = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters(trapPercent: 0));
		var one = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters(trapPercent: 1));

		Assert.Equal(zero.FinalRandomState, one.FinalRandomState);
		Assert.Equal(zero.ActiveObjectCount, one.ActiveObjectCount);
	}

	[Fact]
	public void BigSarcophagusUsesSignedThreeByFourCandidateFootprint()
	{
		var result = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters());
		var sarcophagi = result.Objects.Where(item => item.Type == 48).ToArray();

		Assert.NotEmpty(sarcophagi);
		Assert.All(sarcophagi, sarcophagus => {
			Assert.InRange(sarcophagus.Position.X, 1, 110);
			Assert.InRange(sarcophagus.Position.Y, 2, 110);
			var primary = sarcophagus.Position.Y * AuthoritativeCathedralPopulationGeometry.Width + sarcophagus.Position.X;
			var north = (sarcophagus.Position.Y - 1) * AuthoritativeCathedralPopulationGeometry.Width + sarcophagus.Position.X;
			Assert.Equal(sarcophagus.Slot + 1, result.TileObjectRefs[primary]);
			Assert.Equal(-(sarcophagus.Slot + 1), result.TileObjectRefs[north]);
		});
	}

	[Fact]
	public void PreservesAllNativeObjectFieldsAndStoryMessageSelection()
	{
		var result = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters(depth: 4, storySeed: 0x0002_0000));
		var storyBook = Assert.Single(result.Objects, item => item.Type == 86);

		Assert.Equal(-1, storyBook.BookMessage);
		Assert.Equal(2, storyBook.Var1);
		Assert.Equal(103, storyBook.Var2);
		Assert.Equal(6, storyBook.Var3);
		Assert.Equal(1U, storyBook.AnimFrame);
		Assert.Equal(2, storyBook.Var4);
		Assert.Equal(86, storyBook.Type);
		Assert.False(storyBook.DeleteFlag);
		Assert.Equal(0, storyBook.BreakFlag);
		Assert.False(storyBook.PreFlag);
		Assert.False(storyBook.TrapFlag);
		Assert.False(storyBook.DoorFlag);
		Assert.All(result.Objects.Where(item => item.Type >= 0), item => {
			Assert.InRange(item.AnimLength, 0U, 255U);
			Assert.InRange(item.AnimFrame, 0U, 255U);
			Assert.InRange(item.AnimWidth, (ushort)0, (ushort)255);
		});
	}

	[Fact]
	public void UsesNativeSolBitsAndDynamicDoorNeighborMapping()
	{
		var pieces = new ushort[AuthoritativeCathedralPopulationGeometry.CellCount];
		Array.Fill(pieces, (ushort)1);
		pieces[10 + 10 * 112] = 43;
		pieces[10 + 9 * 112] = 49;
		var properties = new byte[AuthoritativeCathedralPopulationGeometry.PieceCount];
		properties[1] = 0x80; // Native Trap bit.
		properties[43] = 0x01; // Native Solid bit.
		var result = AuthoritativeCathedralPopulationGenerator.Generate(CreateParameters(
			geometry: CreateGeometry(pieces: pieces, pieceProperties: properties)));

		Assert.Equal((ushort)43, result.Pieces[10 + 10 * 112]);
		Assert.Equal((byte)0x80, properties[1]);
		Assert.Equal((byte)0x01, properties[43]);

		var left = AuthoritativeCathedralDoors.Discover(CreateDoorLevel(
			AuthoritativeCathedralDoorKind.Left, (ushort)43, (ushort)49, solidOpenPieces: true));
		var leftOpen = AuthoritativeCathedralDoors.DeltaOpen(left.Level, Assert.Single(left.Doors));
		var leftOpenedAgain = AuthoritativeCathedralDoors.DeltaOpen(leftOpen.Level, leftOpen.Door);
		Assert.Equal((ushort)410, leftOpen.Level.NativePieceLayer[2 * 8 + 3]);
		Assert.Equal((ushort)395, leftOpenedAgain.Level.NativePieceLayer[2 * 8 + 3]);
		Assert.Contains(2 * 8 + 3, leftOpen.Level.NativePieceCollisionCells);

		var right = AuthoritativeCathedralDoors.Discover(CreateDoorLevel(
			AuthoritativeCathedralDoorKind.Right, (ushort)45, (ushort)49, solidOpenPieces: true));
		var rightOpen = AuthoritativeCathedralDoors.DeltaOpen(right.Level, Assert.Single(right.Doors));
		var rightOpenedAgain = AuthoritativeCathedralDoors.DeltaOpen(rightOpen.Level, rightOpen.Door);
		Assert.Equal((ushort)411, rightOpen.Level.NativePieceLayer[3 * 8 + 3]);
		Assert.Equal((ushort)395, rightOpenedAgain.Level.NativePieceLayer[3 * 8 + 3]);
		Assert.Contains(3 * 8 + 3, rightOpen.Level.NativePieceCollisionCells);
	}

	[Fact]
	public void DetachesInputsAndDoesNotExposeMutableResultArrays()
	{
		var pieces = new ushort[AuthoritativeCathedralPopulationGeometry.CellCount];
		Array.Fill(pieces, (ushort)1);
		var parameters = CreateParameters(geometry: CreateGeometry(pieces: pieces));
		var result = AuthoritativeCathedralPopulationGenerator.Generate(parameters);
		pieces[0] = 1378;

		Assert.Equal((ushort)1, parameters.Geometry.Pieces[0]);
		Assert.Equal((ushort)1, result.Pieces[0]);
		Assert.Throws<NotSupportedException>(() => ((IList<ushort>)result.Pieces)[0] = 2);
	}

	[Fact]
	public void DetachesFramesRatesObjectCatalogSolAndOccupancyInputs()
	{
		var pieces = new ushort[AuthoritativeCathedralPopulationGeometry.CellCount];
		Array.Fill(pieces, (ushort)1);
		var special = new byte[AuthoritativeCathedralPopulationGeometry.CellCount];
		var transparency = new byte[AuthoritativeCathedralPopulationGeometry.CellCount];
		var properties = new byte[AuthoritativeCathedralPopulationGeometry.PieceCount];
		var populated = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		var player = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		var monster = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		var frames = Enumerable.Repeat(8, 6).ToArray();
		var rates = Enumerable.Repeat(4, 6).ToArray();
		var objects = Enumerable.Range(0, 109)
			.Select(type => new AuthoritativeCathedralPopulationObjectDefinition(type, 0, 5, 8, 1, 0))
			.ToArray();
		var parameters = new AuthoritativeCathedralPopulationParameters(
			1, 1, 2,
			new AuthoritativeCathedralPopulationGeometry(pieces, special, transparency, properties, populated, player, monster),
			objects,
			[CreateMonster(109, false, 8, 4) with { Frames = frames, Rates = rates }],
			new AuthoritativeCathedralPopulationPlayer(0, 1, true, false, new(40, 40), new(40, 40)),
			0, [1, 2, 3]);

		pieces[0] = 1378;
		special[0] = 9;
		transparency[0] = 9;
		properties[1] = 0xFF;
		populated[0] = true;
		player[0] = true;
		monster[0] = true;
		frames[0] = 0;
		rates[0] = 0;
		objects[0] = new AuthoritativeCathedralPopulationObjectDefinition(108, 0, 5, 8, 1, 0);

		Assert.Equal((ushort)1, parameters.Geometry.Pieces[0]);
		Assert.Equal((byte)0, parameters.Geometry.Special[0]);
		Assert.Equal((byte)0, parameters.Geometry.Transparency[0]);
		Assert.Equal((byte)0, parameters.Geometry.PieceProperties[1]);
		Assert.False(parameters.Geometry.Populated[0]);
		Assert.False(parameters.Geometry.PlayerOccupied[0]);
		Assert.False(parameters.Geometry.MonsterOccupied[0]);
		Assert.Equal(8, parameters.OrderedBestiary[0].Frames[0]);
		Assert.Equal(4, parameters.OrderedBestiary[0].Rates[0]);
		Assert.Equal(0, parameters.Objects[0].NativeType);
	}

	[Fact]
	public void RejectsMalformedPrerequisitesAndBoundedUnableToPlaceGeometry()
	{
		Assert.Throws<InvalidDataException>(() => CreateParameters(objectCount: 108));
		Assert.Throws<InvalidDataException>(() => CreateParameters(geometry: CreateGeometry(piece: 1379)));
		Assert.Throws<InvalidDataException>(() => CreateParameters(trapPercent: 101));

		var solidProperties = new byte[AuthoritativeCathedralPopulationGeometry.PieceCount];
		Array.Fill(solidProperties, (byte)0x01);
		var blocked = CreateParameters(
			geometry: CreateGeometry(pieceProperties: solidProperties), maximumPlacementAttempts: 3);
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationGenerator.Generate(blocked));
	}

	private static AuthoritativeLevel CreateDoorLevel(
		AuthoritativeCathedralDoorKind kind, ushort main, ushort neighbor, bool solidOpenPieces)
	{
		const int width = 8;
		const int height = 8;
		var pieces = new ushort[width * height];
		var x = kind == AuthoritativeCathedralDoorKind.Left ? 3 : 4;
		var y = 3;
		pieces[y * width + x] = main;
		var neighborX = kind == AuthoritativeCathedralDoorKind.Left ? x : x - 1;
		var neighborY = kind == AuthoritativeCathedralDoorKind.Left ? y - 1 : y;
		pieces[neighborY * width + neighborX] = neighbor;
		var required = new ushort[] { 0, 43, 45, 49, 392, 394, 395, 410, 411 };
		var properties = required.ToDictionary(
			piece => piece,
			piece => solidOpenPieces && piece is 410 or 411
				? AuthoritativeDunTileFlags.Solid
				: AuthoritativeDunTileFlags.None);
		return new AuthoritativeLevel(
			1, width, height, nativePieceLayer: pieces, nativePieceProperties: properties);
	}

	private static AuthoritativeCathedralPopulationParameters CreateParameters(
		int depth = 1,
		uint populationSeed = 0x1234_5678,
		uint storySeed = 0x0001_0000,
		bool includeSkeleton = true,
		bool includeSecondSkeleton = false,
		bool reverseSkeletonOrder = false,
		int standFrames = 8,
		int standRates = 4,
		AuthoritativeCathedralPopulationGeometry? geometry = null,
		int trapPercent = 0,
		int maximumPlacementAttempts = 1_000_000,
		int? objectCount = null)
	{
		var objects = Enumerable.Range(0, objectCount ?? 109)
			.Select(type => new AuthoritativeCathedralPopulationObjectDefinition(type, type == 1 ? (byte)1 : (byte)0, 5, 8, 1, 0))
			.ToArray();
		var monsters = new List<AuthoritativeCathedralPopulationMonsterDefinition> { CreateMonster(109, false, standFrames, standRates) };
		if (includeSkeleton && reverseSkeletonOrder) monsters.Add(CreateMonster(9, true, standFrames, standRates));
		if (includeSkeleton) monsters.Add(CreateMonster(8, true, standFrames, standRates));
		if (includeSecondSkeleton && !reverseSkeletonOrder) monsters.Add(CreateMonster(9, true, standFrames, standRates));
		return new AuthoritativeCathedralPopulationParameters(
			depth, populationSeed, storySeed, geometry ?? CreateGeometry(), objects, monsters,
			new AuthoritativeCathedralPopulationPlayer(0, 1, true, false, new(40, 40), new(40, 40)),
			trapPercent, [101, 102, 103], maximumPlacementAttempts);
	}

	private static AuthoritativeCathedralPopulationMonsterDefinition CreateMonster(int type, bool skeleton, int standFrames = 8, int standRates = 4)
		=> new(type, skeleton, 4, 9, 1, 1, 0, 2, 4, 2, 6, 2, 1, 1, 2, 3, 4, 5, 6,
			[standFrames, 8, 8, 8, 8, 8], [standRates, 4, 4, 4, 4, 4]);

	private static AuthoritativeCathedralPopulationGeometry CreateGeometry(
		ushort[]? pieces = null,
		byte[]? pieceProperties = null,
		ushort? piece = null)
	{
		pieces ??= Enumerable.Repeat(piece ?? (ushort)1, AuthoritativeCathedralPopulationGeometry.CellCount).ToArray();
		if (piece.HasValue) Array.Fill(pieces, piece.Value);
		return new AuthoritativeCathedralPopulationGeometry(
			pieces,
			new byte[AuthoritativeCathedralPopulationGeometry.CellCount],
			new byte[AuthoritativeCathedralPopulationGeometry.CellCount],
			pieceProperties ?? new byte[AuthoritativeCathedralPopulationGeometry.PieceCount],
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount],
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount],
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount]);
	}
}
