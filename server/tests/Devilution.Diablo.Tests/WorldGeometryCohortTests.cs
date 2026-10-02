using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class WorldGeometryCohortTests
{
	[Fact]
	public void CaptureRestoreRoundTripsAllBackingLayersAndWarpsInIdentityOrder()
	{
		var source = CreateWorld();
		var state = source.CaptureGeometryState();

		Assert.Equal(new uint[] { 1, 2 }, state.Levels!.Select(level => level.LevelId));
		Assert.Equal(new uint[] { 20, 21 }, state.WarpTargets!.Select(warp => warp.WarpId));

		var restored = new AuthoritativeWorld();
		restored.RestoreGeometryState(state);

		AssertGeometryEqual(source, restored);
	}

	[Fact]
	public void CaptureDetachesLayerArraysAndPieceProperties()
	{
		var source = CreateWorld();
		var state = source.CaptureGeometryState();
		var level = Assert.Single(state.Levels!, candidate => candidate.LevelId == 1);

		Assert.IsType<int[]>(level.BlockedCells)[0] = 777;
		Assert.IsType<ushort[]>(level.NativePieceLayer)[4] = 0;
		Assert.IsType<Dictionary<ushort, AuthoritativeDunTileFlags>>(
			level.NativePieceProperties)[2] = AuthoritativeDunTileFlags.Solid;

		var original = source.Levels.Single(level => level.LevelId == 1);
		Assert.Equal(3, original.BlockedCells[0]);
		Assert.Equal(2, original.NativePieceLayer[4]);
		Assert.Equal(
			AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
			original.NativePieceProperties[2]);
	}

	[Fact]
	public void RestorePreservesExplicitAndDerivedVisionDistinction()
	{
		var source = CreateWorld();
		var state = source.CaptureGeometryState();
		var explicitLevel = state.Levels!.Single(level => level.LevelId == 1);
		var derivedLevel = state.Levels!.Single(level => level.LevelId == 2);

		Assert.True(explicitLevel.HasExplicitNativeVisionBlockedCells);
		Assert.False(derivedLevel.HasExplicitNativeVisionBlockedCells);
		Assert.Equal(new[] { 0, 4 }, derivedLevel.NativeVisionBlockedCells);

		var restored = new AuthoritativeWorld();
		restored.RestoreGeometryState(state);

		Assert.True(restored.Levels.Single(level => level.LevelId == 1).HasExplicitNativeVisionBlockedCells);
		Assert.False(restored.Levels.Single(level => level.LevelId == 2).HasExplicitNativeVisionBlockedCells);
		AssertGeometryEqual(source, restored);
	}

	[Fact]
	public void LateAddedReplacedLevelsAndWarpsAreCapturedAndRestored()
	{
		var source = CreateWorld();
		source.ReplaceLevel(CreateLevel(
			2,
			3,
			3,
			blockedCells: [0, 8],
			missileBlockedCells: [0, 8],
			nativeVisionBlockedCells: [0, 4, 8],
			isDefaultStart: false,
			stableSymbol: "cathedral-replaced"));
		source.AddLevel(CreateLevel(
			3,
			2,
			2,
			blockedCells: [],
			missileBlockedCells: [],
			nativeVisionBlockedCells: [],
			isDefaultStart: false,
			stableSymbol: "cathedral-late"));
		source.AddWarpTarget(new AuthoritativeWarpTarget(
			30,
			3,
			0,
			0,
			1,
			2,
			2) {
			StableSymbol = "late-return",
		});

		var restored = new AuthoritativeWorld();
		restored.RestoreGeometryState(source.CaptureGeometryState());

		AssertGeometryEqual(source, restored);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RestorePreservesRegisteredWarpAfterReplaceBlocksItsEndpoint(bool targetEndpoint)
	{
		var source = CreateWorld();
		source.ReplaceLevel(CreateLevel(
			1,
			4,
			4,
			blockedCells: targetEndpoint ? [3, 10, 12] : [3, 5, 12],
			missileBlockedCells: targetEndpoint ? [3, 7, 10, 12] : [3, 5, 7, 12],
			nativeVisionBlockedCells: targetEndpoint ? [3, 10, 12] : [3, 5, 12],
			isDefaultStart: true,
			stableSymbol: "cathedral-main-blocked"));

		var state = source.CaptureGeometryState();
		var restored = new AuthoritativeWorld();
		restored.RestoreGeometryState(state);

		AssertGeometryEqual(source, restored);
		var blockedRegistration = Assert.Throws<InvalidDataException>(() => source.AddWarpTarget(
			new AuthoritativeWarpTarget(30, 1, targetEndpoint ? 2 : 1, targetEndpoint ? 2 : 1, 2, 1, 1)));
		Assert.Contains("blocked endpoint", blockedRegistration.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void InvalidGeometryNeverPartiallyMutatesTheExistingWorld()
	{
		var world = CreateWorld();
		var valid = world.CaptureGeometryState();
		Assert.NotNull(valid.Levels);
		Assert.NotNull(valid.WarpTargets);
		var level = valid.Levels!.Single(candidate => candidate.LevelId == 1);
		var secondLevel = valid.Levels!.Single(candidate => candidate.LevelId == 2);
		var warp = valid.WarpTargets!.Single(candidate => candidate.WarpId == 20);

		AssertRejectsWithoutMutation(world, valid with { Levels = null });
		AssertRejectsWithoutMutation(world, valid with { WarpTargets = null });
		AssertRejectsWithoutMutation(
			world,
			valid with {
				Levels = Replace(valid.Levels!, level with { BlockedCells = null }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				Levels = Replace(valid.Levels, level with { NativePieceProperties = null }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				Levels = Replace(valid.Levels, level with { Width = 0 }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				Levels = Replace(valid.Levels, level with {
					NativePieceLayer = new ushort[] { 1 },
				}),
			});
		var nullLevel = valid.Levels.ToArray();
		nullLevel[0] = null!;
		AssertRejectsWithoutMutation(world, valid with { Levels = nullLevel });
		AssertRejectsWithoutMutation(
			world,
			valid with {
				Levels = ReplaceAt(valid.Levels, 1, secondLevel with { LevelId = 1 }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				Levels = ReplaceAt(valid.Levels, 1, secondLevel with { IsDefaultStart = true }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				Levels = Replace(valid.Levels, level with {
					HasExplicitNativeVisionBlockedCells = false,
					NativeVisionBlockedCells = [],
				}),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				WarpTargets = Replace(
					valid.WarpTargets!,
					warp with { TargetLevelId = 99 }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				WarpTargets = Replace(
					valid.WarpTargets!,
					warp with { TriggerX = 4, TriggerY = 0 }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				WarpTargets = Replace(
					valid.WarpTargets!,
					warp with { TargetX = 3 }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				WarpTargets = ReplaceAt(
					valid.WarpTargets!,
					1,
					valid.WarpTargets!.Last() with { SourceLevelId = 1, TriggerX = 1, TriggerY = 1 }),
			});
		AssertRejectsWithoutMutation(
			world,
			valid with {
				WarpTargets = ReplaceAt(
					valid.WarpTargets!,
					1,
					valid.WarpTargets!.Last() with { WarpId = 20 }),
			});
		var nullWarp = valid.WarpTargets!.ToArray();
		nullWarp[0] = null!;
		AssertRejectsWithoutMutation(world, valid with { WarpTargets = nullWarp });
	}

	[Fact]
	public void LegacyTsvWorldCanBeCapturedAndRestoredWithoutChangingEntryBehavior()
	{
		var source = AuthoritativeWorld.LoadTsv(
			"levels.tsv",
			"level_id\tlevel_symbol\twidth\theight\tblocked_cells\tdefault_start\tstart_x\tstart_y\n"
				+ "0\ttown\t4\t4\tnone\t1\t1\t2\n"
				+ "1\tcathedral\t4\t4\t3\t0\t0\t0\n");
		source.AddWarpTarget(new AuthoritativeWarpTarget(7, 0, 1, 1, 1, 2, 2));

		var restored = new AuthoritativeWorld();
		restored.RestoreGeometryState(source.CaptureGeometryState());

		Assert.Equal(source.GetStartPosition(0), restored.GetStartPosition(0));
		Assert.Equal(source.WarpTargets.Single().TargetLevelId, restored.WarpTargets.Single().TargetLevelId);
		AssertGeometryEqual(source, restored);
	}

	[Fact]
	public void EmptyGeometryCanReplacePopulatedWorldAndPopulateEmptyWorld()
	{
		var populated = CreateWorld();
		var empty = new DiabloWorldGeometryState([], []);

		populated.RestoreGeometryState(empty);
		Assert.Empty(populated.Levels);
		Assert.Empty(populated.WarpTargets);

		var restored = new AuthoritativeWorld();
		restored.RestoreGeometryState(empty);
		Assert.Empty(restored.Levels);
		Assert.Empty(restored.WarpTargets);
	}

	private static AuthoritativeWorld CreateWorld()
	{
		var world = new AuthoritativeWorld();
		world.AddLevel(CreateLevel(
			1,
			4,
			4,
			blockedCells: [3, 12],
			missileBlockedCells: [3, 7, 12],
			nativeVisionBlockedCells: [3, 5, 12],
			isDefaultStart: true,
			stableSymbol: "cathedral-main"));
		world.AddLevel(CreateLevel(
			2,
			3,
			3,
			blockedCells: [0],
			missileBlockedCells: [0],
			nativeVisionBlockedCells: null,
			isDefaultStart: false,
			stableSymbol: "cathedral-next"));
		world.AddWarpTarget(new AuthoritativeWarpTarget(
			20,
			1,
			1,
			1,
			2,
			1,
			1,
			requiredQuestId: 77,
			requiredQuestCompleted: true,
			targetSetLevelId: 2,
			targetSetLevelActiveMap: false) {
			TargetSetLevelSymbol = "set-two",
			StableSymbol = "down",
		});
		world.AddWarpTarget(new AuthoritativeWarpTarget(
			21,
			2,
			1,
			1,
			1,
			2,
			2) {
			StableSymbol = "up",
		});
		return world;
	}

	private static AuthoritativeLevel CreateLevel(
		uint levelId,
		int width,
		int height,
		IReadOnlyList<int> blockedCells,
		IReadOnlyList<int> missileBlockedCells,
		IReadOnlyList<int>? nativeVisionBlockedCells,
		bool isDefaultStart,
		string stableSymbol)
	{
		var cellCount = checked(width * height);
		var pieceLayer = Enumerable.Repeat<ushort>(1, cellCount).ToArray();
		if (cellCount > 4)
			pieceLayer[4] = 2;
		return new AuthoritativeLevel(
			levelId,
			width,
			height,
			blockedCells,
			missileBlockedCells,
			isDefaultStart,
			suppressMultiplayerDeathDrops: true,
			nativeTransparency: Enumerable.Range(0, cellCount)
				.Select(value => (ushort)(value % 4))
				.ToArray(),
			nativeObjectLayer: Enumerable.Repeat<ushort>(0, cellCount)
				.Select((value, index) => index == 5 ? (ushort)7 : value)
				.ToArray(),
			nativeMegaTileLayer: Enumerable.Range(10, checked((width + 1) / 2 * ((height + 1) / 2)))
				.Select(value => (ushort)value)
				.ToArray(),
			nativePieceLayer: pieceLayer,
			nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
				[2] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
			},
			nativePieceCollisionCells: cellCount > 4 ? [4] : [],
			nativePieceMissileBlockedCells: cellCount > 4 ? [4] : [],
			nativeMonsterLayer: Enumerable.Repeat<ushort>(0, cellCount)
				.Select((value, index) => index == 6 ? (ushort)8 : value)
				.ToArray(),
			startPositionX: width > 2 ? 1 : 0,
			startPositionY: width > 2 ? 1 : 0,
			nativeVisionBlockedCells: nativeVisionBlockedCells) {
			StableSymbol = stableSymbol,
		};
	}

	private static IReadOnlyList<T> Replace<T>(
		IReadOnlyList<T> values,
		T replacement)
	{
		var result = values.ToArray();
		result[0] = replacement;
		return result;
	}

	private static IReadOnlyList<T> ReplaceAt<T>(
		IReadOnlyList<T> values,
		int index,
		T replacement)
	{
		var result = values.ToArray();
		result[index] = replacement;
		return result;
	}

	private static void AssertRejectsWithoutMutation(
		AuthoritativeWorld world,
		DiabloWorldGeometryState invalid)
	{
		var before = world.CaptureGeometryState();
		Assert.ThrowsAny<Exception>(() => world.RestoreGeometryState(invalid));
		var after = world.CaptureGeometryState();
		AssertGeometryStateEqual(before, after);
	}

	private static void AssertGeometryEqual(
		AuthoritativeWorld expected,
		AuthoritativeWorld actual)
	{
		AssertGeometryStateEqual(
			expected.CaptureGeometryState(),
			actual.CaptureGeometryState());
	}

	private static void AssertGeometryStateEqual(
		DiabloWorldGeometryState expected,
		DiabloWorldGeometryState actual)
	{
		Assert.Equal(expected.Levels!.Count, actual.Levels!.Count);
		for (var index = 0; index < expected.Levels.Count; index++) {
			var left = expected.Levels[index];
			var right = actual.Levels[index];
			Assert.Equal(left.LevelId, right.LevelId);
			Assert.Equal(left.StableSymbol, right.StableSymbol);
			Assert.Equal(left.Width, right.Width);
			Assert.Equal(left.Height, right.Height);
			Assert.Equal(left.BlockedCells, right.BlockedCells);
			Assert.Equal(left.MissileBlockedCells, right.MissileBlockedCells);
			Assert.Equal(left.NativeTransparency, right.NativeTransparency);
			Assert.Equal(left.NativeObjectLayer, right.NativeObjectLayer);
			Assert.Equal(left.NativeMegaTileLayer, right.NativeMegaTileLayer);
			Assert.Equal(left.NativePieceLayer, right.NativePieceLayer);
			Assert.Equal(
				left.NativePieceProperties!.OrderBy(pair => pair.Key),
				right.NativePieceProperties!.OrderBy(pair => pair.Key));
			Assert.Equal(left.NativePieceCollisionCells, right.NativePieceCollisionCells);
			Assert.Equal(left.NativeVisionBlockedCells, right.NativeVisionBlockedCells);
			Assert.Equal(left.HasExplicitNativeVisionBlockedCells, right.HasExplicitNativeVisionBlockedCells);
			Assert.Equal(left.NativePieceMissileBlockedCells, right.NativePieceMissileBlockedCells);
			Assert.Equal(left.NativeMonsterLayer, right.NativeMonsterLayer);
			Assert.Equal(left.IsDefaultStart, right.IsDefaultStart);
			Assert.Equal(left.SuppressMultiplayerDeathDrops, right.SuppressMultiplayerDeathDrops);
			Assert.Equal(left.StartPositionX, right.StartPositionX);
			Assert.Equal(left.StartPositionY, right.StartPositionY);
		}

		Assert.Equal(expected.WarpTargets!.Count, actual.WarpTargets!.Count);
		for (var index = 0; index < expected.WarpTargets.Count; index++) {
			var left = expected.WarpTargets[index];
			var right = actual.WarpTargets[index];
			Assert.Equal(left, right);
		}
	}
}
