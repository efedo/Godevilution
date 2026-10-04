using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Focused tests for the detached pre-join Cathedral level-load boundary. */
public sealed class AuthoritativeCathedralLevelReadyGeneratorTests
{
	[Fact]
	public void ContextRefusesRetainedMissilesPortalsAndUnsafeScalars()
	{
		var fresh = Fresh();
		foreach (var player in new[] { Player() with { PriorMissilesEmpty = false },
			Player() with { LifeCurrentQ6 = 0 }, Player() with { Last = new(-1, 1) },
			Player() with { Temp = new(1, 256) }, Player() with { SpellFlags = -1 },
			Player() with { SpellFlags = 256 } })
			Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralLevelReadyRequest(fresh, player, Automap(), true));
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralLevelReadyRequest(fresh, Player(), Automap(), false));
		Assert.Throws<ArgumentNullException>(() => new AuthoritativeCathedralLevelReadyRequest(fresh, Player(), null!, true));
		var unchanged = AuthoritativeCathedralFreshLevelGenerator.Generate(SyntheticMetadata(), 1, 2588,
			0x12345678, AuthoritativeCathedralEntry.Main, Hero() with { LevelChanging = false });
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralLevelReadyRequest(unchanged, Player(), Automap(), true));
	}

	[Theory]
	[InlineData(1, 2588U, "main")]
	[InlineData(4, 0U, "previous")]
	public void ReadyGenerationClearsMissilesPreservesRngAndRetainsPlayerOld(
		int depth, uint seed, string entry)
	{
		var fresh = AuthoritativeCathedralFreshLevelGenerator.Generate(
			SyntheticMetadata(), depth, seed, 0x12345678, ParseEntry(entry), Hero());
		var sourcePlayer = fresh.InitialPopulation.ObjectTemplate.Player;
		var request = new AuthoritativeCathedralLevelReadyRequest(
			fresh,
			new AuthoritativeCathedralLevelReadyPlayerContext(
				255, true, true, true, 6400, new(13, 17), new(19, 23), true),
			Automap(), portalsAbsent: true);

		var result = AuthoritativeCathedralLevelReadyGenerator.Generate(request);

		Assert.Equal(248, result.SpellFlags);
		Assert.True(result.Infravision);
		Assert.True(result.MissilePreFlag);
		Assert.False(result.AutoMapShowItems);
		Assert.False(result.UpdateVision);
		Assert.True(result.MissilesCleared);
		Assert.Equal(fresh.Population.FinalRandomState, result.FinalRandomState);
		Assert.Equal(fresh.MonsterClearFinalRandomState, result.MonsterClearRandomState);
		Assert.Equal(sourcePlayer.Tile, result.Player.Old);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(13, 17), result.Player.Last);
		Assert.Equal(new AuthoritativeCathedralPopulationPoint(19, 23), result.Player.Temp);
		Assert.Equal(sourcePlayer.LevelChanging, result.Player.LevelChanging);
		Assert.Equal(31, result.Corpses.Count);
		Assert.Equal(1600, result.AutomapExploration.Count);
		Assert.Equal(256, result.VisibleTransparencyRegions.Count);
		Assert.Equal(4, result.VisionRecords.Count);
	}

	[Fact]
	public void PlayerLastAndTempRetainNativeByteBoundary()
	{
		var fresh = Fresh();
		var player = Player() with { Last = new(255, 255), Temp = new(255, 255) };
		var result = AuthoritativeCathedralLevelReadyGenerator.Generate(
			new(fresh, player, Automap(), portalsAbsent: true));

		Assert.Equal(player.Last, result.Player.Last);
		Assert.Equal(player.Temp, result.Player.Temp);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(128)]
	[InlineData(255)]
	public void SpellResetPreservesOtherBitsAndFractionalLife(int flags)
	{
		var result = AuthoritativeCathedralLevelReadyGenerator.Generate(new(Fresh(),
			Player() with { SpellFlags = flags, LifeCurrentQ6 = 1, MissilePreFlag = false, Infravision = false }, Automap(), true));
		Assert.Equal(flags & ~7, result.SpellFlags);
		Assert.Equal(1, result.Player.LifeCurrentQ6);
		Assert.False(result.MissilePreFlag);
		Assert.False(result.Infravision);
	}

	[Fact]
	public void SyncInitialPositionUsesOnlyTheFirstEightOffsets()
	{
		var fresh = Fresh();
		var origin = fresh.InitialPopulation.ObjectTemplate.Player.Tile;
		var omitted = new AuthoritativeCathedralPopulationPoint(origin.X + 2, origin.Y + 2);
		var fallback = new AuthoritativeCathedralPopulationPoint(origin.X, origin.Y - 1);
		Assert.DoesNotContain(fallback, fresh.TriggerPositions);
		var state = MakeAvailable(MakeAvailable(AllCellsSolid(fresh.Population.State), omitted), fallback);

		var result = Ready(WithState(fresh, state));

		Assert.Equal(fallback, result.Player.Tile);
		Assert.NotEqual(omitted, result.Player.Tile);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	public void DirectOffsetOrderChoosesFirstAvailableBeforeLaterCandidate(int first)
	{
		var fresh = Fresh();
		var origin = fresh.InitialPopulation.ObjectTemplate.Player.Tile;
		var offsets = new[] { (0, 0), (1, 0), (0, 1), (1, 1), (2, 0), (0, 2), (1, 2), (2, 1) };
		var expected = new AuthoritativeCathedralPopulationPoint(origin.X + offsets[first].Item1, origin.Y + offsets[first].Item2);
		var later = new AuthoritativeCathedralPopulationPoint(origin.X + offsets[first + 1].Item1, origin.Y + offsets[first + 1].Item2);
		var state = MakeAvailable(MakeAvailable(AllCellsSolid(fresh.Population.State), expected), later);
		Assert.Equal(expected, Ready(WithState(fresh, state)).Player.Tile);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	public void SignedSolidObjectReferencesBlockPlayerPlacement(int objectReference)
	{
		var fresh = Fresh();
		var origin = fresh.InitialPopulation.ObjectTemplate.Player.Tile;
		var state = BlockWithSolidObject(fresh.Population.State, origin, new[] { (0, 0) }, objectReference < 0);

		var result = Ready(WithState(fresh, state));

		Assert.NotEqual(origin, result.Player.Tile);
	}

	[Fact]
	public void FirstNativeCrawlCandidateWinsAfterDirectOffsetsAreBlocked()
	{
		var fresh = Fresh();
		var origin = fresh.InitialPopulation.ObjectTemplate.Player.Tile;
		var directOffsets = new[] { (0, 0), (1, 0), (0, 1), (1, 1), (2, 0), (0, 2), (1, 2), (2, 1) };
		var target = Crawl(1)
			.Select(offset => new AuthoritativeCathedralPopulationPoint(origin.X + offset.X, origin.Y + offset.Y))
			.First(point => Contains(point) && !fresh.TriggerPositions.Contains(point) && !directOffsets.Any(offset =>
				point == new AuthoritativeCathedralPopulationPoint(origin.X + offset.Item1, origin.Y + offset.Item2)));
		var state = BlockWithSolidObject(fresh.Population.State, origin, directOffsets, negativeReference: true);
		state = MakeAvailable(state, target);

		var result = Ready(WithState(fresh, state));

		Assert.Equal(target, result.Player.Tile);
	}

	[Fact]
	public void NativeCrawlReachesRadiusFiftyBeforeReturningFallback()
	{
		var fresh = Fresh();
		var origin = fresh.InitialPopulation.ObjectTemplate.Player.Tile;
		var target = Crawl(50)
			.Select(offset => new AuthoritativeCathedralPopulationPoint(origin.X + offset.X, origin.Y + offset.Y))
			.First(point => Contains(point) && !fresh.TriggerPositions.Contains(point));
		var state = AllCellsSolid(fresh.Population.State);
		state = MakeAvailable(state, target);

		var result = Ready(WithState(fresh, state));

		Assert.Equal(target, result.Player.Tile);
	}

	[Fact]
	public void NativeCrawlExhaustionUsesUncheckedZeroZeroFallback()
	{
		var fresh = Fresh();
		var state = AllCellsSolid(fresh.Population.State);

		var result = Ready(WithState(fresh, state));

		Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 0), result.Player.Tile);
	}

	[Theory]
	[InlineData(-1, 1, false)]
	[InlineData(1, 1, true)]
	[InlineData(1, 63, true)]
	[InlineData(1, 64, false)]
	public void MonsterOccupancyUsesNativeSignedReferenceAndQ6LifeRules(
		int monsterReference, int hitPoints, bool originIsAvailable)
	{
		var fresh = Fresh();
		var origin = fresh.InitialPopulation.ObjectTemplate.Player.Tile;
		var state = MakeAvailable(fresh.Population.State, origin);
		var monsterRefs = state.MonsterRefs.ToArray();
		monsterRefs[Index(origin)] = monsterReference;
		var monsters = state.MonsterSlots.ToArray();
		monsters[0] = monsters[0] with { HitPoints = hitPoints };
		state = RebuildState(state, monsterSlots: monsters, monsterRefs: monsterRefs);

		var result = Ready(WithState(fresh, state));

		Assert.Equal(originIsAvailable, result.Player.Tile == origin);
	}

	[Fact]
	public void PlayerLightMovesWhileVisionRecordAndOldTileRemainAtInitPlayerEntry()
	{
		var fresh = Fresh();
		var origin = fresh.InitialPopulation.ObjectTemplate.Player.Tile;
		var offsets = new[] { (0, 0), (1, 0), (0, 1), (1, 1), (2, 0), (0, 2), (1, 2), (2, 1) };
		var state = BlockWithSolidObject(fresh.Population.State, origin, offsets, negativeReference: true);
		var target = Crawl(1)
			.Select(offset => new AuthoritativeCathedralPopulationPoint(origin.X + offset.X, origin.Y + offset.Y))
			.First(point => Contains(point) && !fresh.TriggerPositions.Contains(point) && !offsets.Any(offset =>
				point == new AuthoritativeCathedralPopulationPoint(origin.X + offset.Item1, origin.Y + offset.Item2)));
		state = MakeAvailable(state, target);
		var result = Ready(WithState(fresh, state));

		Assert.NotEqual(origin, result.Player.Tile);
		Assert.Equal(fresh.VisionRecords[0].Tile, result.VisionRecords[0].Tile);
		Assert.Equal(fresh.VisionRecords[0].OldTile, result.VisionRecords[0].OldTile);
		Assert.Equal(result.Player.Tile, result.State.LightRecords[0].Tile);
		Assert.Equal(fresh.Population.State.LightRecords[0].Tile, result.State.LightRecords[0].OldTile);
		Assert.False(result.State.LightRecords[0].HasChanged);
	}

	[Fact]
	public void InitCorpsesKeepsThirtyOneSlotsBloodGapStoneAndUniquePaletteSlot()
	{
		var fresh = Fresh();
		var source = fresh.Population.State;
		var uniqueSlot = source.ActiveMonsterIds[0];
		var selected = fresh.Population.InitialPopulation.Bestiary.Entries[0];
		var monsters = source.MonsterSlots.ToArray();
		monsters[uniqueSlot] = monsters[uniqueSlot] with {
			Type = selected.NativeType, LevelType = 0, UniqueType = 0, UniqueTranslation = 255,
		};
		var result = Ready(WithState(fresh, RebuildState(source, monsterSlots: monsters)));

		Assert.Equal(31, result.Corpses.Count);
		Assert.Equal(new AuthoritativeCathedralLevelReadyCorpseState(
			result.BloodSpatterSlot, 0, 0, 0), result.Corpses[result.BloodSpatterSlot]);
		var stone = result.Corpses[result.StoneCorpseId - 1];
		Assert.Equal(11, stone.Frame);
		Assert.Equal(128, stone.Width);
		Assert.Equal(0, stone.TranslationPaletteIndex);
		Assert.All(result.BestiaryCorpseIds, id => Assert.InRange(id, 1, 31));
		var unique = result.State.MonsterSlots[uniqueSlot];
		Assert.Equal(0, unique.UniqueType);
		Assert.Equal(255, unique.UniqueTranslation);
		Assert.Equal(uniqueSlot + 1, result.Corpses[unique.CorpseId - 1].TranslationPaletteIndex);
		foreach (var slot in result.State.ActiveMonsterIds.Take(result.State.ActiveMonsterCount)) {
			var monster = result.State.MonsterSlots[slot];
			if (monster.UniqueType == byte.MaxValue)
				continue;
			Assert.InRange(monster.CorpseId, 1, 31);
			Assert.Equal(slot + 1, result.Corpses[monster.CorpseId - 1].TranslationPaletteIndex);
		}
		var last = result.State.ActiveMonsterIds.Take(result.State.ActiveMonsterCount)
			.Select(slot => result.State.MonsterSlots[slot])
			.Where(monster => monster.CorpseId > 0)
			.Select(monster => monster.CorpseId)
			.DefaultIfEmpty(result.StoneCorpseId)
			.Max();
		Assert.All(result.Corpses.Skip(last), corpse =>
			Assert.Equal(new AuthoritativeCathedralLevelReadyCorpseState(corpse.Slot, 0, 0, 0), corpse));
	}

	[Fact]
	public void ChargeCleanupOnlyChangesModeAndPreservesAnimationScratchAndRng()
	{
		var fresh = Fresh();
		var source = fresh.Population.State;
		var slot = source.ActiveMonsterIds[0];
		var original = source.MonsterSlots[slot];
		var monsters = source.MonsterSlots.ToArray();
		monsters[slot] = original with {
			Mode = 14, AnimTicksPerFrame = 7, AnimTickCounter = -3, AnimNumberOfFrames = 12,
			AnimCurrentFrame = 5, Var1 = 37, Var2 = -12, Var3 = 9, GoalVar1 = 23,
		};
		var result = Ready(WithState(fresh, RebuildState(source, monsterSlots: monsters)));

		var actual = result.State.MonsterSlots[slot];
		Assert.Equal(0, actual.Mode);
		Assert.Equal(7, actual.AnimTicksPerFrame);
		Assert.Equal(-3, actual.AnimTickCounter);
		Assert.Equal(12, actual.AnimNumberOfFrames);
		Assert.Equal(5, actual.AnimCurrentFrame);
		Assert.Equal(37, actual.Var1);
		Assert.Equal(-12, actual.Var2);
		Assert.Equal(9, actual.Var3);
		Assert.Equal(23, actual.GoalVar1);
		Assert.Equal(source.MonsterPetrified[slot], result.State.MonsterPetrified[slot]);
		Assert.Equal(fresh.Population.FinalRandomState, result.FinalRandomState);
	}

	[Fact]
	public void InvalidLightIsRemovedByNativeSwapAndChangedLightClearsChangeBit()
	{
		var fresh = Fresh();
		var source = fresh.Population.State;
		var ids = Enumerable.Range(0, 32).ToArray();
		var records = source.LightRecords.ToArray();
		records[1] = records[1] with { IsInvalid = true, HasChanged = true,
			Tile = new(22, 23), OldTile = new(21, 23), Radius = 5, OldRadius = 4 };
		records[2] = records[2] with { IsInvalid = false, HasChanged = false,
			Tile = new(24, 25), Radius = 3, OffsetX = 0, OffsetY = 0 };
		var state = RebuildState(source, activeMonsterCount: 0, activeLightIds: ids,
			activeLightCount: 3, lightRecords: records);
		var result = Ready(WithState(fresh, state));

		Assert.Equal(2, result.State.ActiveLightCount);
		Assert.Equal(new[] { 0, 2, 1 }.Concat(Enumerable.Range(3, 29)), result.State.ActiveLightIds);
		Assert.Equal(records[1] with { HasChanged = false }, result.State.LightRecords[1]);
		Assert.Equal(records[2], result.State.LightRecords[2]);
		Assert.False(result.State.LightRecords[0].HasChanged);
		Assert.Equal(source.LightRecords[0].Tile, result.State.LightRecords[0].OldTile);
	}

	private static AuthoritativeCathedralLevelReadyPlayerContext Player()
		=> new(255, true, true, true, 6400, new(13, 17), new(19, 23), true);

	private static AuthoritativeCathedralAutomapMetadata Automap()
		=> AuthoritativeCathedralAutomapMetadata.Load(AuthoritativeCathedralAutomapMetadata.CreateTable(
			Enumerable.Range(0, 256).Select(index => new AuthoritativeCathedralAutomapTileDefinition((byte)index,
				AuthoritativeCathedralAutomapTileType.None, AuthoritativeCathedralAutomapTileFlags.None))).ToCanonicalTsv());

	private static AuthoritativeCathedralFreshLevelResult Fresh()
		=> AuthoritativeCathedralFreshLevelGenerator.Generate(SyntheticMetadata(), 1, 2588, 0x12345678,
			AuthoritativeCathedralEntry.Main, Hero());

	private static AuthoritativeCathedralFreshLevelHeroContext Hero()
		=> new(6400, 3200, 8, 10, new(75, 68), true);

	private static AuthoritativeCathedralPopulationMetadata SyntheticMetadata()
		=> AuthoritativeCathedralPopulationMetadata.Load(new ContentPack(
			"test.cathedral-ready", "1.0.0",
			CreateReadyTables(),
			schema: new ContentSchemaIdentity(
				AuthoritativeCathedralPopulationMetadata.SchemaId,
				AuthoritativeCathedralPopulationMetadata.SchemaVersion)));

	private static IReadOnlyList<TsvTable> CreateReadyTables()
	{
		var tables = CathedralPopulationMetadataCompilerTests.CreateCompleteTables().ToArray();
		var path = AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath;
		var source = tables.Single(table => table.SourcePath == path);
		var lines = source.ToCanonicalTsv().TrimEnd('\n').Split('\n');
		var columns = source.Columns.ToArray();
		var northWestColumn = Array.IndexOf(columns, "piece_north_west");
		// PlaceStairs emits exactly these native coarse marker IDs.  Give their
		// northwest fine cell the corresponding runtime trigger piece while the
		// remaining synthetic geometry stays walkable and non-solid.
		foreach (var (megaTileId, triggerPieceId) in new[] { (64, 128), (59, 114) }) {
			var cells = lines[megaTileId + 1].Split('\t');
			cells[northWestColumn] = triggerPieceId.ToString(System.Globalization.CultureInfo.InvariantCulture);
			lines[megaTileId + 1] = string.Join('\t', cells);
		}
		tables[Array.IndexOf(tables, source)] = TsvTable.Parse(path, string.Join('\n', lines) + "\n");

		var itemPath = AuthoritativeCathedralThemeItemMetadata.ItemsTablePath;
		var itemSource = tables.Single(table => table.SourcePath == itemPath);
		var itemLines = itemSource.ToCanonicalTsv().TrimEnd('\n').Split('\n');
		var itemColumns = itemSource.Columns.ToArray();
		var itemTypeColumn = Array.IndexOf(itemColumns, "item_type");
		var miscColumn = Array.IndexOf(itemColumns, "misc_id");
		foreach (var (nativeIndex, miscId) in new[] { (24, 3), (25, 6) }) {
			var cells = itemLines[nativeIndex + 1].Split('\t');
			cells[itemTypeColumn] = "0";
			cells[miscColumn] = miscId.ToString(System.Globalization.CultureInfo.InvariantCulture);
			itemLines[nativeIndex + 1] = string.Join('\t', cells);
		}
		tables[Array.IndexOf(tables, itemSource)] = TsvTable.Parse(
			itemPath, string.Join('\n', itemLines) + "\n");

		var uniquePath = AuthoritativeCathedralUniqueMonsterCatalog.TableName;
		var uniqueSource = tables.Single(table => table.SourcePath == uniquePath);
		var uniqueLines = uniqueSource.ToCanonicalTsv().TrimEnd('\n').Split('\n');
		var uniqueColumns = uniqueSource.Columns.ToArray();
		var dungeonLevelColumn = Array.IndexOf(uniqueColumns, "dungeon_level");
		// This synthetic pack intentionally has no depth-matching unique rows;
		// the ready tests inject a selected unique directly when testing corpse
		// palette state.  It therefore never asks for set-piece/uber prerequisites.
		for (var row = 0; row < 100; row++) {
			var cells = uniqueLines[row + 1].Split('\t');
			cells[dungeonLevelColumn] = "255";
			uniqueLines[row + 1] = string.Join('\t', cells);
		}
		tables[Array.IndexOf(tables, uniqueSource)] = TsvTable.Parse(
			uniquePath, string.Join('\n', uniqueLines) + "\n");
		return tables;
	}

	private static AuthoritativeCathedralEntry ParseEntry(string value)
		=> value == "main" ? AuthoritativeCathedralEntry.Main : AuthoritativeCathedralEntry.Previous;

	private static AuthoritativeCathedralLevelReadyResult Ready(AuthoritativeCathedralFreshLevelResult fresh)
		=> AuthoritativeCathedralLevelReadyGenerator.Generate(new(fresh, Player(), Automap(), true));

	private static AuthoritativeCathedralFreshLevelResult WithState(
		AuthoritativeCathedralFreshLevelResult fresh,
		AuthoritativeCathedralThemeRoomState state)
	{
		var theme = fresh.Population.ThemeRooms;
		var themeResult = new AuthoritativeCathedralThemeRoomResult(
			state, theme.FinalRandomState, theme.Themes, theme.ThemeOrigins, theme.ThemeVars,
			theme.ItemNoFlippySlots, theme.FinalThemeVar1, theme.FinalThemeOrigin);
		var population = new AuthoritativeCathedralFullPopulationResult(
			fresh.Population.InitialPopulation, fresh.Population.ClearedMonsterPool,
			fresh.Population.Encounters, fresh.Population.InitialItems,
			fresh.Population.BeforeThemeRooms, themeResult);
		return new AuthoritativeCathedralFreshLevelResult(
			fresh.Request, fresh.Geometry, fresh.InitialPopulation, fresh.FullPopulationRequest,
			population, fresh.TriggerPositions, fresh.PreThemePopulated, fresh.InitialTileFlags,
			fresh.InitialLight, fresh.InitialPreLight, fresh.LightAllocator,
			fresh.MonsterClearFinalRandomState, fresh.PlayerPreparation, fresh.VisionRecords,
			fresh.VisionActive, fresh.UpdateVision);
	}

	private static AuthoritativeCathedralThemeRoomState BlockWithSolidObject(
		AuthoritativeCathedralThemeRoomState source,
		AuthoritativeCathedralPopulationPoint origin,
		IReadOnlyList<(int X, int Y)> offsets,
		bool negativeReference)
	{
		var refs = source.ObjectRefs.ToArray();
		foreach (var offset in offsets) {
			var point = new AuthoritativeCathedralPopulationPoint(origin.X + offset.X, origin.Y + offset.Y);
			if (Contains(point))
				refs[Index(point)] = negativeReference ? -1 : 1;
		}
		var objects = source.ObjectSlots.ToArray();
		objects[0] = objects[0] with { SolidFlag = true };
		return RebuildState(source, objectSlots: objects, objectRefs: refs);
	}

	private static AuthoritativeCathedralThemeRoomState AllCellsSolid(
		AuthoritativeCathedralThemeRoomState source)
	{
		var refs = Enumerable.Repeat(-1, AuthoritativeCathedralThemeRoomState.CellCount).ToArray();
		var objects = source.ObjectSlots.ToArray();
		objects[0] = objects[0] with { SolidFlag = true };
		return RebuildState(source, objectSlots: objects, objectRefs: refs);
	}

	private static AuthoritativeCathedralThemeRoomState MakeAvailable(
		AuthoritativeCathedralThemeRoomState source,
		AuthoritativeCathedralPopulationPoint point)
	{
		if (!Contains(point))
			throw new InvalidDataException("Test candidate is outside the native dungeon.");
		var refs = source.ObjectRefs.ToArray();
		var monsterRefs = source.MonsterRefs.ToArray();
		var playerRefs = source.PlayerRefs.ToArray();
		refs[Index(point)] = 0;
		monsterRefs[Index(point)] = 0;
		playerRefs[Index(point)] = 0;
		var properties = source.PieceProperties.ToArray();
		properties[source.Pieces[Index(point)]] &= 0xFE;
		return RebuildState(source, objectRefs: refs, monsterRefs: monsterRefs,
			playerRefs: playerRefs, pieceProperties: properties);
	}

	private static AuthoritativeCathedralThemeRoomState RebuildState(
		AuthoritativeCathedralThemeRoomState source,
		IReadOnlyList<AuthoritativeCathedralPopulationObjectState>? objectSlots = null,
		IReadOnlyList<AuthoritativeCathedralPopulationMonsterState>? monsterSlots = null,
		IReadOnlyList<int>? monsterRefs = null,
		IReadOnlyList<int>? playerRefs = null,
		IReadOnlyList<int>? objectRefs = null,
		IReadOnlyList<ushort>? pieces = null,
		IReadOnlyList<byte>? pieceProperties = null,
		int? activeMonsterCount = null,
		IReadOnlyList<int>? activeLightIds = null,
		int? activeLightCount = null,
		IReadOnlyList<AuthoritativeCathedralEncounterLightState>? lightRecords = null)
		=> new(
			objectSlots ?? source.ObjectSlots, source.ActiveObjectIds, source.AvailableObjectIds,
			source.ActiveObjectCount, monsterSlots ?? source.MonsterSlots, source.ActiveMonsterIds,
			activeMonsterCount ?? source.ActiveMonsterCount, source.MonsterTargetCount, monsterRefs ?? source.MonsterRefs,
			playerRefs ?? source.PlayerRefs, source.ItemSlots, source.ActiveItemIds,
			source.ActiveItemCount, source.ItemRefs, source.UniqueItemFlags, pieces ?? source.Pieces,
			source.Transparency, source.Special, pieceProperties ?? source.PieceProperties, source.TileFlags,
			objectRefs ?? source.ObjectRefs, source.Light, source.PreLight,
			activeLightIds ?? source.ActiveLightIds, activeLightCount ?? source.ActiveLightCount,
			lightRecords ?? source.LightRecords, source.UpdateLighting, source.DisableLighting,
			source.Flags, source.Corpses, source.MonsterPetrified);

	private static IEnumerable<(int X, int Y)> Crawl(int radius)
	{
		yield return (0, radius);
		yield return (0, -radius);
		for (var x = 1; x < radius; x++) {
			yield return (-x, radius);
			yield return (x, radius);
			yield return (-x, -radius);
			yield return (x, -radius);
		}
		if (radius > 1) {
			var diagonal = radius - 1;
			yield return (-diagonal, diagonal);
			yield return (diagonal, diagonal);
			yield return (-diagonal, -diagonal);
			yield return (diagonal, -diagonal);
		}
		yield return (-radius, 0);
		yield return (radius, 0);
		for (var y = 1; y < radius; y++) {
			yield return (-radius, y);
			yield return (radius, y);
			yield return (-radius, -y);
			yield return (radius, -y);
		}
	}

	private static int Index(AuthoritativeCathedralPopulationPoint point)
		=> point.X + point.Y * AuthoritativeCathedralThemeRoomState.Width;

	private static bool Contains(AuthoritativeCathedralPopulationPoint point)
		=> point.X is >= 0 and < AuthoritativeCathedralThemeRoomState.Width
			&& point.Y is >= 0 and < AuthoritativeCathedralThemeRoomState.Width;
}
