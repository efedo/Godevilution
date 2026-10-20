namespace Devilution.Diablo;

/**
 * Post-InitPlayer/post-theme context immediately before InitMissiles.
 * Last and Temp are supplied at this boundary, not before InitPlayer's reset.
 * Missile objects are intentionally not modelled
 * by this detached boundary: an ordinary fresh level must enter it with an
 * empty missile list and the three player spell-status bits are the only
 * scalar inputs whose native reset is observable here.
 */
public sealed record AuthoritativeCathedralLevelReadyPlayerContext(
	int SpellFlags,
	bool Infravision,
	bool AutoMapShowItems,
	bool MissilePreFlag,
	int LifeCurrentQ6,
	AuthoritativeCathedralPopulationPoint Last,
	AuthoritativeCathedralPopulationPoint Temp,
	bool PriorMissilesEmpty);

/** The supported handoff is deliberately before the player-join message. */
public enum AuthoritativeCathedralLevelReadyStage
{
	PreparedBeforeJoin = 0,
}

/** Request for the bounded post-theme, pre-join native level-load boundary. */
public sealed class AuthoritativeCathedralLevelReadyRequest
{
	public AuthoritativeCathedralLevelReadyRequest(
		AuthoritativeCathedralFreshLevelResult freshLevel,
		AuthoritativeCathedralLevelReadyPlayerContext player,
		AuthoritativeCathedralAutomapMetadata automap,
		bool portalsAbsent)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(automap);
		if (player.SpellFlags is < 0 or > byte.MaxValue)
			throw new InvalidDataException("Native player spell flags must fit uint8.");
		if (player.LifeCurrentQ6 <= 0)
			throw new InvalidDataException("The cold ready boundary requires a living local player.");
		if (!ContainsScalar(player.Last) || !ContainsScalar(player.Temp))
			throw new InvalidDataException("Unused native player Last and Temp coordinates must fit uint8.");
		if (!player.PriorMissilesEmpty)
			throw new InvalidDataException("The cold level-ready boundary requires an empty missile list.");
		ArgumentNullException.ThrowIfNull(freshLevel);
		if (!freshLevel.Request.Hero.LevelChanging)
			throw new InvalidDataException("The supported pre-join cold handoff requires level-changing state.");
		if (!portalsAbsent)
			throw new InvalidDataException("Retained portal state is outside the cold ordinary Cathedral boundary.");
		if (freshLevel.VisionRecords.Count != 4 || freshLevel.VisionActive.Count != 4)
			throw new InvalidDataException("Native fresh level vision requires all four player records.");
		if (freshLevel.Population.State.ActiveLightCount <= 0
			|| !freshLevel.Population.State.ActiveLightIds.Take(freshLevel.Population.State.ActiveLightCount).Contains(0))
			throw new InvalidDataException("The cold level-ready boundary requires the local player light.");

		FreshLevel = freshLevel;
		Player = player;
		Automap = automap;
		PortalsAbsent = portalsAbsent;
	}

	public AuthoritativeCathedralFreshLevelResult FreshLevel { get; }
	public AuthoritativeCathedralLevelReadyPlayerContext Player { get; }
	public AuthoritativeCathedralAutomapMetadata Automap { get; }
	public bool PortalsAbsent { get; }

	private static bool ContainsScalar(AuthoritativeCathedralPopulationPoint point)
		=> point.X is >= 0 and <= 255 && point.Y is >= 0 and <= 255;
}

/** Native player scalar state after syncInitialPosition, retaining InitPlayer's Old tile. */
public sealed record AuthoritativeCathedralLevelReadyPlayerState(
	int Id,
	int Level,
	bool Active,
	bool LevelChanging,
	AuthoritativeCathedralPopulationPoint Tile,
	AuthoritativeCathedralPopulationPoint Future,
	AuthoritativeCathedralPopulationPoint Old,
	AuthoritativeCathedralPopulationPoint Last,
	AuthoritativeCathedralPopulationPoint Temp,
	int LifeCurrentQ6,
	bool MissilePreFlag,
	int LightId,
	int LightRadius);

/** Scalar corpse state; sprites are deliberately outside this headless contract. */
public sealed record AuthoritativeCathedralLevelReadyCorpseState(
	int Slot,
	int Frame,
	int Width,
	int TranslationPaletteIndex);

/**
 * Detached output after SavePreLighting, InitMissiles, InitCorpses,
 * ProcessLightList and ProcessVisionList. It is not a joined
 * multiplayer/session state and performs no store, portal, or network work.
 */
public sealed class AuthoritativeCathedralLevelReadyResult
{
	internal AuthoritativeCathedralLevelReadyResult(
		AuthoritativeCathedralLevelReadyRequest request,
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralLevelReadyPlayerState player,
		IReadOnlyList<AuthoritativeCathedralLevelReadyCorpseState> corpses,
		IReadOnlyList<int> bestiaryCorpseIds,
		int bloodSpatterSlot,
		int stoneCorpseId,
		IReadOnlyList<AuthoritativeCathedralEncounterLightState> visionRecords,
		IReadOnlyList<bool> visionActive,
		bool updateVision,
		IReadOnlyList<bool> visibleTransparencyRegions,
		IReadOnlyList<byte> automapExploration,
		AuthoritativeCathedralLevelReadyPlayerContext playerContext,
		uint randomState,
		uint monsterClearRandomState)
	{
		Request = request ?? throw new ArgumentNullException(nameof(request));
		State = state ?? throw new ArgumentNullException(nameof(state));
		Player = player ?? throw new ArgumentNullException(nameof(player));
		Corpses = Array.AsReadOnly(corpses.ToArray());
		BestiaryCorpseIds = Array.AsReadOnly(bestiaryCorpseIds.ToArray());
		BloodSpatterSlot = bloodSpatterSlot;
		StoneCorpseId = stoneCorpseId;
		VisionRecords = Array.AsReadOnly(visionRecords.Select(value => value with { }).ToArray());
		VisionActive = Array.AsReadOnly(visionActive.ToArray());
		UpdateVision = updateVision;
		VisibleTransparencyRegions = Array.AsReadOnly(visibleTransparencyRegions.ToArray());
		AutomapExploration = Array.AsReadOnly(automapExploration.ToArray());
		SpellFlags = playerContext.SpellFlags & ~7;
		Infravision = playerContext.Infravision;
		MissilePreFlag = playerContext.MissilePreFlag;
		AutoMapShowItems = false;
		MissilesCleared = true;
		FinalRandomState = randomState;
		MonsterClearRandomState = monsterClearRandomState;
		Stage = AuthoritativeCathedralLevelReadyStage.PreparedBeforeJoin;
	}

	/** Retains metadata, animation, item initialization and pre-load provenance for runtime integration. */
	public AuthoritativeCathedralLevelReadyRequest Request { get; }
	public AuthoritativeCathedralThemeRoomState State { get; }
	public AuthoritativeCathedralLevelReadyPlayerState Player { get; }
	public IReadOnlyList<AuthoritativeCathedralLevelReadyCorpseState> Corpses { get; }
	public IReadOnlyList<int> BestiaryCorpseIds { get; }
	public int BloodSpatterSlot { get; }
	/** Native stonendx is one past the stone corpse's zero-based slot. */
	public int StoneCorpseId { get; }
	public IReadOnlyList<AuthoritativeCathedralEncounterLightState> VisionRecords { get; }
	public IReadOnlyList<bool> VisionActive { get; }
	public bool UpdateVision { get; }
	public IReadOnlyList<bool> VisibleTransparencyRegions { get; }
	public IReadOnlyList<byte> AutomapExploration { get; }
	public int SpellFlags { get; }
	public bool Infravision { get; }
	public bool MissilePreFlag { get; }
	public bool AutoMapShowItems { get; }
	public bool MissilesCleared { get; }
	public uint FinalRandomState { get; }
	public uint MonsterClearRandomState { get; }
	public AuthoritativeCathedralLevelReadyStage Stage { get; }
}

/** Explicit C2 input for the same bounded pre-join ready boundary. */
public sealed class AuthoritativeCatacombsLevelReadyRequest
{
	public AuthoritativeCatacombsLevelReadyRequest(
		AuthoritativeCatacombsFreshLevelResult freshLevel,
		AuthoritativeCathedralLevelReadyPlayerContext player,
		AuthoritativeCatacombsAutomapMetadata automap,
		bool portalsAbsent)
	{
		ArgumentNullException.ThrowIfNull(freshLevel);
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(automap);
		if (player.SpellFlags is < 0 or > byte.MaxValue)
			throw new InvalidDataException("Native player spell flags must fit uint8.");
		if (player.LifeCurrentQ6 <= 0)
			throw new InvalidDataException("The cold ready boundary requires a living local player.");
		if (player.Last.X is < 0 or > 255 || player.Last.Y is < 0 or > 255
			|| player.Temp.X is < 0 or > 255 || player.Temp.Y is < 0 or > 255)
			throw new InvalidDataException("Unused native player Last and Temp coordinates must fit uint8.");
		if (!player.PriorMissilesEmpty)
			throw new InvalidDataException("The cold level-ready boundary requires an empty missile list.");
		if (!freshLevel.Request.Hero.LevelChanging)
			throw new InvalidDataException("The supported pre-join cold handoff requires level-changing state.");
		if (!portalsAbsent)
			throw new InvalidDataException("Retained portal state is outside the cold ordinary Catacombs boundary.");
		if (freshLevel.VisionRecords.Count != 4 || freshLevel.VisionActive.Count != 4)
			throw new InvalidDataException("Native fresh level vision requires all four player records.");
		var state = freshLevel.Population.State;
		if (state.ActiveLightCount <= 0 || !state.ActiveLightIds.Take(state.ActiveLightCount).Contains(0))
			throw new InvalidDataException("The cold level-ready boundary requires the local player light.");
		_ = AuthoritativeNativeFreshLevelView.FromCatacombs(freshLevel);

		FreshLevel = freshLevel;
		Player = player;
		Automap = automap;
		PortalsAbsent = portalsAbsent;
	}

	public AuthoritativeCatacombsFreshLevelResult FreshLevel { get; }
	public AuthoritativeCathedralLevelReadyPlayerContext Player { get; }
	public AuthoritativeCatacombsAutomapMetadata Automap { get; }
	public bool PortalsAbsent { get; }
}

/** Detached C2 ready output; it never acquires C1 request or profile identity. */
public sealed class AuthoritativeCatacombsLevelReadyResult
{
	internal AuthoritativeCatacombsLevelReadyResult(
		AuthoritativeCatacombsLevelReadyRequest request,
		AuthoritativeNativeLevelReadyOutput output)
	{
		Request = request ?? throw new ArgumentNullException(nameof(request));
		State = output.State;
		Player = output.Player;
		Corpses = Array.AsReadOnly(output.Corpses.ToArray());
		BestiaryCorpseIds = Array.AsReadOnly(output.BestiaryCorpseIds.ToArray());
		BloodSpatterSlot = output.BloodSpatterSlot;
		StoneCorpseId = output.StoneCorpseId;
		VisionRecords = Array.AsReadOnly(output.VisionRecords.Select(value => value with { }).ToArray());
		VisionActive = Array.AsReadOnly(output.VisionActive.ToArray());
		UpdateVision = output.UpdateVision;
		VisibleTransparencyRegions = Array.AsReadOnly(output.VisibleTransparencyRegions.ToArray());
		AutomapExploration = Array.AsReadOnly(output.AutomapExploration.ToArray());
		SpellFlags = output.PlayerContext.SpellFlags & ~7;
		Infravision = output.PlayerContext.Infravision;
		MissilePreFlag = output.PlayerContext.MissilePreFlag;
		AutoMapShowItems = false;
		MissilesCleared = true;
		FinalRandomState = output.FinalRandomState;
		MonsterClearRandomState = output.MonsterClearRandomState;
		Stage = AuthoritativeCathedralLevelReadyStage.PreparedBeforeJoin;
	}

	public AuthoritativeCatacombsLevelReadyRequest Request { get; }
	public AuthoritativeCathedralThemeRoomState State { get; }
	public AuthoritativeCathedralLevelReadyPlayerState Player { get; }
	public IReadOnlyList<AuthoritativeCathedralLevelReadyCorpseState> Corpses { get; }
	public IReadOnlyList<int> BestiaryCorpseIds { get; }
	public int BloodSpatterSlot { get; }
	public int StoneCorpseId { get; }
	public IReadOnlyList<AuthoritativeCathedralEncounterLightState> VisionRecords { get; }
	public IReadOnlyList<bool> VisionActive { get; }
	public bool UpdateVision { get; }
	public IReadOnlyList<bool> VisibleTransparencyRegions { get; }
	public IReadOnlyList<byte> AutomapExploration { get; }
	public int SpellFlags { get; }
	public bool Infravision { get; }
	public bool MissilePreFlag { get; }
	public bool AutoMapShowItems { get; }
	public bool MissilesCleared { get; }
	public uint FinalRandomState { get; }
	public uint MonsterClearRandomState { get; }
	public AuthoritativeCathedralLevelReadyStage Stage { get; }
}

internal sealed record AuthoritativeNativeLevelReadyOutput(
	AuthoritativeCathedralThemeRoomState State,
	AuthoritativeCathedralLevelReadyPlayerState Player,
	IReadOnlyList<AuthoritativeCathedralLevelReadyCorpseState> Corpses,
	IReadOnlyList<int> BestiaryCorpseIds,
	int BloodSpatterSlot,
	int StoneCorpseId,
	IReadOnlyList<AuthoritativeCathedralEncounterLightState> VisionRecords,
	IReadOnlyList<bool> VisionActive,
	bool UpdateVision,
	IReadOnlyList<bool> VisibleTransparencyRegions,
	IReadOnlyList<byte> AutomapExploration,
	AuthoritativeCathedralLevelReadyPlayerContext PlayerContext,
	uint FinalRandomState,
	uint MonsterClearRandomState);

/** Bounded ordinary Cathedral loader continuation after the fresh compositor. */
public static class AuthoritativeCathedralLevelReadyGenerator
{
	private const int Width = AuthoritativeCathedralThemeRoomState.Width;
	private const int CellCount = AuthoritativeCathedralThemeRoomState.CellCount;
	private const int MonsterCapacity = AuthoritativeCathedralThemeRoomState.MonsterCapacity;
	private const int LightCapacity = AuthoritativeCathedralThemeRoomState.LightCapacity;
	private const int VisionCapacity = 4;
	private const int StandMode = 0;
	private const int ChargeMode = 14;
	private const uint BerserkFlag = 1U << 11;
	private const int NoLight = -1;
	private const byte Missile = 1 << 0;
	private const byte Visible = 1 << 1;
	private const byte MissileFireWall = 1 << 4;
	private const byte MissileLightningWall = 1 << 5;
	private const byte Lit = 1 << 6;
	private const byte Explored = 1 << 7;
	private const byte Solid = 1 << 0;
	private const byte BlockLight = 1 << 1;

	public static AuthoritativeCathedralLevelReadyResult Generate(
		AuthoritativeCathedralLevelReadyRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		var fresh = AuthoritativeNativeFreshLevelView.FromCathedral(request.FreshLevel);
		var output = GenerateCore(fresh, request.Player, request.PortalsAbsent,
			request.Automap.ProjectExploration);
		return new(request, output.State, output.Player, output.Corpses, output.BestiaryCorpseIds,
			output.BloodSpatterSlot, output.StoneCorpseId, output.VisionRecords, output.VisionActive,
			output.UpdateVision, output.VisibleTransparencyRegions, output.AutomapExploration,
			output.PlayerContext, output.FinalRandomState, output.MonsterClearRandomState);
	}

	internal static AuthoritativeCatacombsLevelReadyResult GenerateCatacombs(
		AuthoritativeCatacombsLevelReadyRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		var fresh = AuthoritativeNativeFreshLevelView.FromCatacombs(request.FreshLevel);
		var output = GenerateCore(fresh, request.Player, request.PortalsAbsent,
			request.Automap.ProjectExploration);
		return new(request, output);
	}

	private static AuthoritativeNativeLevelReadyOutput GenerateCore(
		AuthoritativeNativeFreshLevelView fresh,
		AuthoritativeCathedralLevelReadyPlayerContext playerContext,
		bool portalsAbsent,
		Action<IReadOnlyList<ushort>, int, int, byte, byte[]> projectExploration)
	{
		var source = fresh.State;
		var monsterSlots = source.MonsterSlots.Select(value => value with { }).ToArray();
		var monsterRefs = source.MonsterRefs.ToArray();
		var playerRefs = source.PlayerRefs.ToArray();
		var tileFlags = source.TileFlags.ToArray();
		var light = source.Light.ToArray();
		var preLight = source.PreLight.ToArray();
		var activeLightIds = source.ActiveLightIds.ToArray();
		var lightRecords = source.LightRecords.Select(value => value with { }).ToArray();
		var activeLightCount = source.ActiveLightCount;

		ValidateColdState(source, fresh);
		var sourcePlayer = fresh.Player;
		var readyPlayer = new AuthoritativeCathedralLevelReadyPlayerState(
			sourcePlayer.Id, sourcePlayer.Level, sourcePlayer.Active,
			sourcePlayer.LevelChanging, sourcePlayer.Tile, sourcePlayer.Future,
			sourcePlayer.Tile, playerContext.Last, playerContext.Temp,
			playerContext.LifeCurrentQ6, playerContext.MissilePreFlag, 0,
			fresh.LightRadius);

		// InitMissiles runs before SavePreLighting.  It has no RNG effects in
		// this cold contract and only clears the represented tile/status fields.
		for (var index = 0; index < tileFlags.Length; index++)
			tileFlags[index] = (byte)(tileFlags[index] & ~(Missile | MissileFireWall | MissileLightningWall));
		var corpseOutput = InitializeCorpses(fresh, monsterSlots);

		// Native SavePreLighting snapshots the live static-object/theme light
		// channel before any player/monster light is moved or added.
		preLight = light.ToArray();
		var synced = SyncInitialPosition(
			fresh, source, playerRefs, readyPlayer, portalsAbsent);
		readyPlayer = synced.Player;

		UpdateMonsterLights(monsterSlots, activeLightIds, ref activeLightCount,
			readyPlayer.LightId, source, lightRecords, !source.DisableLighting);
		for (var index = 0; index < source.ActiveMonsterCount; index++) {
			var slot = source.ActiveMonsterIds[index];
			if (slot < 0 || slot >= MonsterCapacity)
				throw new InvalidDataException("Native active monster identity is outside the monster pool.");
			if (monsterSlots[slot].Mode == ChargeMode)
				monsterSlots[slot] = monsterSlots[slot] with { Mode = StandMode };
		}

		// LoadGameLevelLightVision restores the pre-light channel, forces a
		// player-light refresh, then processes the indexed light list.
		light = preLight.ToArray();
		if (!source.DisableLighting) {
			ChangeLightXY(lightRecords, readyPlayer.LightId, readyPlayer.Tile);
			ProcessLightList(light, preLight, activeLightIds, ref activeLightCount,
				lightRecords, source);
		}

		var vision = ProcessVision(
			fresh, source, tileFlags, projectExploration, portalsAbsent);
		var state = new AuthoritativeCathedralThemeRoomState(
			source.ObjectSlots, source.ActiveObjectIds, source.AvailableObjectIds,
			source.ActiveObjectCount, monsterSlots, source.ActiveMonsterIds,
			source.ActiveMonsterCount, source.MonsterTargetCount, monsterRefs,
			playerRefs, source.ItemSlots, source.ActiveItemIds, source.ActiveItemCount,
			source.ItemRefs, source.UniqueItemFlags, source.Pieces, source.Transparency,
			source.Special, source.PieceProperties, tileFlags, source.ObjectRefs,
			light, preLight, activeLightIds, activeLightCount, lightRecords,
			updateLighting: source.DisableLighting ? source.UpdateLighting : false,
			source.DisableLighting, source.Flags, source.Corpses,
			source.MonsterPetrified);

		return new(state, readyPlayer, corpseOutput.Corpses, corpseOutput.BestiaryCorpseIds,
			corpseOutput.BloodSpatterSlot, corpseOutput.StoneCorpseId,
			vision.VisionRecords, fresh.VisionActive, UpdateVision: false,
			vision.VisibleTransparencyRegions, vision.AutomapExploration, playerContext,
			fresh.FinalRandomState, fresh.MonsterClearRandomState);
	}

	private static void ValidateColdState(
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeNativeFreshLevelView fresh)
	{
		if (state.Pieces.Count != CellCount || state.TileFlags.Count != CellCount
			|| state.Transparency.Count != CellCount || state.PieceProperties.Count != 1379)
			throw new InvalidDataException("The ready Cathedral stage requires complete native tile channels.");
		if (state.MonsterSlots.Count != MonsterCapacity || state.ActiveMonsterIds.Count != MonsterCapacity)
			throw new InvalidDataException("The ready Cathedral stage requires the complete monster pool.");
		if (state.LightRecords.Count != LightCapacity || state.ActiveLightIds.Count != LightCapacity)
			throw new InvalidDataException("The ready Cathedral stage requires all indexed light records.");
		if (fresh.VisionRecords.Count != VisionCapacity || fresh.VisionActive.Count != VisionCapacity)
			throw new InvalidDataException("The ready Cathedral stage requires all vision records.");
		foreach (var id in state.ActiveLightIds.Take(state.ActiveLightCount)) {
			if (id < 0 || id >= LightCapacity)
				throw new InvalidDataException("Active indexed light identity is outside the native light pool.");
			var light = state.LightRecords[id];
			if (!state.DisableLighting && (light.OffsetX != 0 || light.OffsetY != 0))
				throw new InvalidDataException("Nonzero retained light offsets are outside the supported cold boundary.");
			if (!state.DisableLighting && !light.IsInvalid && (light.Tile.X < 0 || light.Tile.X >= Width
				|| light.Tile.Y < 0 || light.Tile.Y >= Width || light.Radius > 15))
				throw new InvalidDataException("An active native light would reach an unsafe managed lighting call.");
		}
		if (state.PlayerRefs.Any(value => value != 0))
			throw new InvalidDataException("Fresh ready generation cannot borrow pre-existing player occupancy.");
	}

	private static (AuthoritativeCathedralLevelReadyPlayerState Player, int[] PlayerRefs) SyncInitialPosition(
		AuthoritativeNativeFreshLevelView fresh,
		AuthoritativeCathedralThemeRoomState state,
		int[] playerRefs,
		AuthoritativeCathedralLevelReadyPlayerState player,
		bool portalsAbsent)
	{
		var offsets = new[] { (0, 0), (1, 0), (0, 1), (1, 1), (2, 0), (0, 2), (1, 2), (2, 1) };
		AuthoritativeCathedralPopulationPoint? selected = null;
		foreach (var (x, y) in offsets) {
			var candidate = new AuthoritativeCathedralPopulationPoint(player.Tile.X + x, player.Tile.Y + y);
			if (PositionIsAvailable(candidate, state, player.Id)) {
				selected = candidate;
				break;
			}
		}
		if (selected is null) {
			if (!portalsAbsent)
				throw new InvalidDataException("Portal-aware player fallback is outside the supported ready boundary.");
			selected = FindClosestValidPosition(fresh, state, player.Tile, player.Id);
		}
		var position = selected ?? new AuthoritativeCathedralPopulationPoint(0, 0);
		if (!Contains(position))
			throw new InvalidDataException("Native player sync selected an unsafe dungeon position.");
		playerRefs[Index(position)] = player.Id + 1;
		return (player with { Tile = position, Future = position }, playerRefs);
	}

	private static AuthoritativeCathedralPopulationPoint? FindClosestValidPosition(
		AuthoritativeNativeFreshLevelView fresh,
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralPopulationPoint origin,
		int playerId)
	{
		for (var radius = 1; radius <= 50; radius++) {
			foreach (var displacement in Crawl(radius)) {
				var candidate = new AuthoritativeCathedralPopulationPoint(origin.X + displacement.X, origin.Y + displacement.Y);
				if (fresh.TriggerPositions.Contains(candidate))
					continue;
				if (PositionIsAvailable(candidate, state, playerId))
					return candidate;
			}
		}
		return null;
	}

	private static IEnumerable<(int X, int Y)> Crawl(int radius)
	{
		yield return (0, radius);
		if (radius == 0)
			yield break;
		yield return (0, -radius);
		for (var x = 1; x < radius; x++) {
			yield return (-x, radius);
			yield return (x, radius);
			yield return (-x, -radius);
			yield return (x, -radius);
		}
		if (radius > 1) {
			var d = radius - 1;
			yield return (-d, d);
			yield return (d, d);
			yield return (-d, -d);
			yield return (d, -d);
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

	private static bool PositionIsAvailable(
		AuthoritativeCathedralPopulationPoint position,
		AuthoritativeCathedralThemeRoomState state,
		int playerId)
	{
		if (!Contains(position))
			return false;
		var index = Index(position);
		if ((state.PieceProperties[state.Pieces[index]] & Solid) != 0)
			return false;
		var objectReference = state.ObjectRefs[index];
		if (objectReference != 0) {
			var objectSlot = Math.Abs(objectReference) - 1;
			if (objectSlot < 0 || objectSlot >= state.ObjectSlots.Count)
				return false;
			if (state.ObjectSlots[objectSlot].SolidFlag)
				return false;
		}
		var playerReference = state.PlayerRefs[index];
		if (playerReference != 0 && playerReference != playerId + 1)
			return false;
		var monsterReference = state.MonsterRefs[index];
		if (monsterReference <= 0)
			return monsterReference == 0;
		var monsterSlot = monsterReference - 1;
		if (monsterSlot < 0 || monsterSlot >= state.MonsterSlots.Count)
			return false;
		return (state.MonsterSlots[monsterSlot].HitPoints >> 6) <= 0;
	}

	private static void UpdateMonsterLights(
		AuthoritativeCathedralPopulationMonsterState[] monsters,
		int[] activeLightIds,
		ref int activeLightCount,
		int playerLightId,
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralEncounterLightState[] records,
		bool lightingEnabled)
	{
		if (records.Length != LightCapacity || activeLightIds.Length != LightCapacity)
			throw new InvalidDataException("Native indexed light state is incomplete.");
		for (var activeIndex = 0; activeIndex < state.ActiveMonsterCount; activeIndex++) {
			var slot = state.ActiveMonsterIds[activeIndex];
			if (slot < 0 || slot >= monsters.Length)
				throw new InvalidDataException("Active monster identity is outside the native pool.");
			var monster = monsters[slot];
			if ((monster.Flags & BerserkFlag) != 0) {
				if (lightingEnabled && activeLightCount < LightCapacity) {
					var lightId = activeLightIds[activeLightCount++];
					var old = records[lightId];
					records[lightId] = old with {
						Tile = monster.Tile, Radius = 3, OffsetX = 0, OffsetY = 0,
						IsInvalid = false, HasChanged = false,
					};
					monsters[slot] = monster with { LightId = lightId };
					monster = monsters[slot];
				}
				else {
					monsters[slot] = monster with { LightId = NoLight };
					monster = monsters[slot];
				}
			}
			if (monster.LightId == NoLight)
				continue;
			if (monster.LightId < 0 || monster.LightId >= LightCapacity)
				throw new InvalidDataException("Monster light identity is outside the native light pool.");
			if (monster.LightId == playerLightId) {
				monsters[slot] = monster with { LightId = NoLight };
				continue;
			}
			var light = records[monster.LightId];
			if (lightingEnabled && monster.Tile != light.Tile)
				records[monster.LightId] = light with {
					HasChanged = true, OldTile = light.Tile, OldRadius = light.Radius, Tile = monster.Tile,
				};
		}
	}

	private static void ChangeLightXY(
		AuthoritativeCathedralEncounterLightState[] records,
		int lightId,
		AuthoritativeCathedralPopulationPoint position)
	{
		if (lightId < 0 || lightId >= records.Length)
			throw new InvalidDataException("The local player light identity is outside the native light pool.");
		var light = records[lightId];
		records[lightId] = light with {
			HasChanged = true, OldTile = light.Tile, OldRadius = light.Radius, Tile = position,
		};
	}

	private static void ProcessLightList(
		byte[] light,
		byte[] preLight,
		int[] activeLightIds,
		ref int activeLightCount,
		AuthoritativeCathedralEncounterLightState[] records,
		AuthoritativeCathedralThemeRoomState state)
	{
		if (state.DisableLighting)
			return;
		if (activeLightCount == 0)
			return;
		for (var index = 0; index < activeLightCount; index++) {
			var id = activeLightIds[index];
			var record = records[id];
			if (record.IsInvalid)
				UnLight(light, preLight, record.Tile, record.Radius);
			if (record.HasChanged) {
				UnLight(light, preLight, record.OldTile, record.OldRadius);
				records[id] = record with { HasChanged = false };
			}
		}
		for (var index = 0; index < activeLightCount; index++) {
			var id = activeLightIds[index];
			var record = records[id];
			if (record.IsInvalid) {
				activeLightCount--;
				(activeLightIds[activeLightCount], activeLightIds[index]) = (activeLightIds[index], activeLightIds[activeLightCount]);
				index--;
				continue;
			}
			if (record.OffsetX != 0 || record.OffsetY != 0)
				throw new InvalidDataException("Nonzero indexed light offsets are outside the zero-offset cold implementation.");
			if (!Contains(record.Tile) || record.Radius > 15)
				throw new InvalidDataException("An active indexed light would reach an unsafe native lighting call.");
			if ((state.PieceProperties[state.Pieces[Index(record.Tile)]] & Solid) != 0)
				continue;
			AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, record.Tile, record.Radius, false);
		}
	}

	private static void UnLight(
		byte[] light,
		byte[] preLight,
		AuthoritativeCathedralPopulationPoint position,
		int radius)
	{
		var extent = radius + 2;
		for (var y = Math.Max(0, position.Y - extent); y <= Math.Min(Width - 1, position.Y + extent); y++)
			for (var x = Math.Max(0, position.X - extent); x <= Math.Min(Width - 1, position.X + extent); x++)
				light[Index(new(x, y))] = preLight[Index(new(x, y))];
	}

	private static (IReadOnlyList<AuthoritativeCathedralEncounterLightState> VisionRecords,
		IReadOnlyList<bool> VisibleTransparencyRegions,
		IReadOnlyList<byte> AutomapExploration) ProcessVision(
		AuthoritativeNativeFreshLevelView fresh,
		AuthoritativeCathedralThemeRoomState state,
		byte[] tileFlags,
		Action<IReadOnlyList<ushort>, int, int, byte, byte[]> projectExploration,
		bool portalsAbsent)
	{
		if (!portalsAbsent)
			throw new InvalidDataException("Portal-aware vision is outside the supported cold boundary.");
		var transparency = new bool[256];
		// InitAutomap precedes CreateLevel and clears the complete coarse view.
		var automapExploration = new byte[AuthoritativeCathedralAutomapMetadata.ExplorationCellCount];
		var records = fresh.VisionRecords.Select(value => value with { }).ToArray();
		foreach (var (record, active, recordIndex) in fresh.VisionRecords
			.Zip(fresh.VisionActive)
			.Select((value, index) => (value.First, value.Second, index))) {
			if (!active)
				continue;
			if (!Contains(record.Tile) || record.Radius is < 0 or > 15)
				throw new InvalidDataException("Active vision reaches an unsafe native tile or radius.");
			if (record.HasChanged) {
				RemoveVisionFlags(tileFlags, record.OldTile, record.OldRadius);
				records[recordIndex] = record with { HasChanged = false };
			}
			AuthoritativeCathedralNativeVision.Cast(
				record.Tile, record.Radius,
				point => {
					var index = Index(point);
					if (tileFlags[index] != 0)
						projectExploration(fresh.MegaTiles, point.X, point.Y, 4, automapExploration);
					tileFlags[index] |= Visible | Lit | Explored;
				},
				point => {
					var value = state.Transparency[Index(point)];
					if (value < 0)
						throw new InvalidDataException("Native vision reached a negative transparency index.");
					if (value != 0)
						transparency[value] = true;
				},
				point => {
					var index = Index(point);
					return (state.PieceProperties[state.Pieces[index]] & BlockLight) == 0;
				});
		}
		return (records, transparency, automapExploration);
	}

	private static void RemoveVisionFlags(
		byte[] tileFlags,
		AuthoritativeCathedralPopulationPoint position,
		int radius)
	{
		var extent = radius + 2;
		for (var y = Math.Max(0, position.Y - extent); y <= Math.Min(Width - 1, position.Y + extent); y++)
			for (var x = Math.Max(0, position.X - extent); x <= Math.Min(Width - 1, position.X + extent); x++)
				tileFlags[Index(new(x, y))] &= unchecked((byte)~(Visible | Lit));
	}

	private static (IReadOnlyList<AuthoritativeCathedralLevelReadyCorpseState> Corpses,
		IReadOnlyList<int> BestiaryCorpseIds, int BloodSpatterSlot, int StoneCorpseId) InitializeCorpses(
		AuthoritativeNativeFreshLevelView fresh,
		AuthoritativeCathedralPopulationMonsterState[] monsters)
	{
		var corpses = Enumerable.Range(0, 31)
			.Select(slot => new AuthoritativeCathedralLevelReadyCorpseState(slot, 0, 0, 0))
			.ToArray();
		var ids = new int[fresh.Bestiary.Entries.Count];
		var seenTypes = new HashSet<int>();
		var next = 0;
		for (var index = 0; index < fresh.Bestiary.Entries.Count; index++) {
			var entry = fresh.Bestiary.Entries[index];
			if (!seenTypes.Add(entry.NativeType))
				continue;
			if (next >= corpses.Length - 2)
				throw new InvalidDataException("Native corpse storage cannot represent the selected bestiary.");
			// CMonster::getAnimData(Death) supplies these two independent
			// scalars; the bestiary retains native per-animation effective widths.
			corpses[next] = new(next, entry.Frames[4] - 1, entry.Widths[4], 0);
			ids[index] = next + 1;
			next++;
		}
		var bloodSlot = next;
		next++;
		if (next >= corpses.Length)
			throw new InvalidDataException("Native stone corpse slot is outside the 31-slot corpse pool.");
		corpses[next] = new(next, 11, 128, 0);
		var stoneCorpseId = next + 1;
		next++;
		for (var activeIndex = 0; activeIndex < fresh.State.ActiveMonsterCount; activeIndex++) {
			var slot = fresh.State.ActiveMonsterIds[activeIndex];
			var monster = monsters[slot];
			if (monster.UniqueType == byte.MaxValue)
				continue;
			var entry = fresh.Bestiary.Entries
				.SingleOrDefault(value => value.NativeType == monster.Type);
			if (entry is null || next >= corpses.Length)
				throw new InvalidDataException("A unique monster corpse cannot be resolved from the selected bestiary.");
			corpses[next] = new(next, entry.Frames[4] - 1, entry.Widths[4], slot + 1);
			monsters[slot] = monster with { CorpseId = next + 1 };
			next++;
		}
		return (corpses, ids, bloodSlot, stoneCorpseId);
	}

	private static int Index(AuthoritativeCathedralPopulationPoint point)
		=> checked(point.X + point.Y * Width);

	private static bool Contains(AuthoritativeCathedralPopulationPoint point)
		=> point.X is >= 0 and < Width && point.Y is >= 0 and < Width;
}
