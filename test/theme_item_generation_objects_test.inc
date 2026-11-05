TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsChestOperations)
{
	CaptureCatacombsChestOperations(std::getenv("GODEVILUTION_CATACOMBS_CHEST_ORACLE_DIRECTORY"), false);
}

// The same lanes with gbIsMultiplayer: chest loot draws from the multiplayer item and spell pools.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsMultiplayerChestOperations)
{
	CaptureCatacombsChestOperations(std::getenv("GODEVILUTION_CATACOMBS_MULTIPLAYER_CHEST_ORACLE_DIRECTORY"), true);
}

void ThemeRoomGenerationTest::CaptureCatacombsChestOperations(const char *directory, bool multiplayer)
{
	if (directory == nullptr || *directory == '\0')
		GTEST_SKIP() << "Set the explicit Catacombs chest oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native evidence.";
	struct ExecutionGuard {
		bool sound = gbSndInited;
		bool headless = HeadlessMode;
		bool missilePreFlag = MissilePreFlag;
		std::list<Missile> savedMissiles = std::move(Missiles);
		ScopedMissileDataForTest missileData;
		~ExecutionGuard()
		{
			Missiles = std::move(savedMissiles);
			MissilePreFlag = missilePreFlag;
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} guard;
	HeadlessMode = true;
	gbSndInited = false;
	LoadMissileData();
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Arrow));
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::FireArrow));
	Player remotePlayer;
	remotePlayer.position.tile = { 46, 50 };
	const char *fileName = multiplayer ? "native-catacombs-multiplayer-chest-operations.json" : "native-catacombs-chest-operations.json";
	gbIsMultiplayer = multiplayer;
	// Native UpdateEnemy skips a dead multiplayer player; a real level entry is alive.
	if (multiplayer) MyPlayer->life.current = MyPlayer->life.base = MyPlayer->life.maximum = MyPlayer->life.maximumBase;
	std::ofstream stream(root / fileName, std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-chest-operations");
		document.Number("version", 1);
		if (multiplayer) document.Boolean("multiplayer", true);
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		struct Lane {
			std::string name;
			int depth;
			int rawType;
			int itemCount;
			int branch;
			bool trapFlag;
			int trapKind;
			uint32_t seed;
			int activeItems;
			bool blockPlacement;
			bool linkedWallTrap;
		};
		std::vector<Lane> lanes;
		constexpr std::array<int, 6> rawTypes { 5, 6, 7, 68, 69, 70 };
		constexpr std::array<uint32_t, 6> seeds { 0, 1, 2588, UINT32_C(0x80000000), UINT32_MAX, 743271966 };
		for (int depth = 5; depth <= 8; ++depth) {
			for (size_t index = 0; index < rawTypes.size(); ++index) {
				const int rawType = rawTypes[index];
				const bool intrinsic = rawType >= 68;
				const bool linkedWallTrap = depth == 8 && rawType == 6;
				std::ostringstream name;
				name << "depth-" << depth << "-type-" << rawType;
				lanes.push_back({ name.str(), depth, rawType, 1, static_cast<int>(index % 2),
				    intrinsic || linkedWallTrap,
				    static_cast<int>((depth + static_cast<int>(index)) % 2),
				    seeds[(static_cast<size_t>(depth) + index) % seeds.size()],
				    2, false, linkedWallTrap });
			}
		}
		for (int rawType = 68; rawType <= 70; ++rawType) {
			std::ostringstream name;
			name << "disarmed-type-" << rawType;
			lanes.push_back({ name.str(), 5, rawType, 1, rawType % 2, false, 1, 2588, 2, false, false });
		}
		// AddChestTraps can convert a chest already linked to a wall trap. The
		// intrinsic chest still fires; UpdateTrapState does not dispatch TCHEST links.
		lanes.push_back({ "intrinsic-with-retained-wall-link", 8, 68, 1, 1, true, 1, 2588, 2, false, true });
		lanes.push_back({ "zero-count-zero-seed", 5, 5, 0, 0, false, 0, 0, 2, false, false });
		lanes.push_back({ "full-item-pool", 8, 7, 1, 1, false, 0, UINT32_C(0x80000000), MAXITEMS, false, false });
		lanes.push_back({ "blocked-item-placement", 6, 5, 1, 0, false, 0, 2588, 2, true, false });
		for (const Lane &lane : lanes) {
			PrepareRoom(lane.depth, lane.activeItems, lane.linkedWallTrap ? 2 : 1, 0, false);
			currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(lane.depth), .type = DTYPE_CATACOMBS });
			MyPlayer->plrlevel = static_cast<uint8_t>(lane.depth);
			MyPlayer->plrIsOnSetLevel = false;
			MyPlayer->_pLvlChanging = false;
			MyPlayer->pLvlLoad = 1;
			MyPlayer->position.tile = { 60, 60 };
			tileAt(50, 50).setPlayer(0);
			tileAt(50, 50).setMonster(0);
			if (lane.blockPlacement)
				std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::Solid);
			Object &chest = Objects[0];
			chest._otype = static_cast<_object_id>(lane.rawType);
			chest.position = { 50, 50 };
			chest._oSolidFlag = true;
			chest._oMissFlag = true;
			chest.selectionRegion = SelectionRegion::Bottom;
			chest._oAnimFrame = 1;
			chest._oAnimLen = 3;
			chest._oAnimDelay = 1;
			chest._oAnimWidth = 96;
			chest._oRndSeed = lane.seed;
			chest._oVar1 = lane.itemCount;
			chest._oVar2 = lane.branch;
			chest._oVar4 = lane.trapKind;
			chest._oTrapFlag = lane.trapFlag;
			tileAt(chest.position).setObject(1);
			if (lane.linkedWallTrap) {
				Object &wallTrap = Objects[1];
				wallTrap._otype = static_cast<_object_id>(53);
				wallTrap.position = { 50, 49 };
				wallTrap._oVar1 = 50;
				wallTrap._oVar2 = 50;
				wallTrap._oVar3 = static_cast<int>(MissileID::Arrow);
				wallTrap._oVar4 = 0;
				tileAt(wallTrap.position).setObject(2);
			}
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(UINT32_C(0x12345678));
			const NativeItemWorldState before = CaptureItemWorld();
			if (caseCount++ != 0) stream << ',';
			JsonObject row(stream);
			row.String("name", lane.name);
			row.Number("depth", lane.depth);
			row.Number("levelType", levelType());
			row.Number("rawType", lane.rawType);
			row.Number("itemCount", lane.itemCount);
			row.Number("branch", lane.branch);
			row.Boolean("trapFlag", lane.trapFlag);
			row.Number("trapKind", lane.trapKind);
			row.Number("seed", lane.seed);
			row.Number("remotePlayerX", remotePlayer.position.tile.x);
			row.Number("remotePlayerY", remotePlayer.position.tile.y);
			row.Number("trapDirectionArgument", GetDirection(chest.position, remotePlayer.position.tile));
			row.Boolean("blockPlacement", lane.blockPlacement);
			row.Boolean("linkedWallTrap", lane.linkedWallTrap);
			auto writeLinkedWallTrap = [&]() {
				if (lane.linkedWallTrap)
					WriteRoomObject(stream, 1, Objects[1]);
				else
					stream << "null";
			};
			row.Key("inputChest");
			WriteRoomObject(stream, 0, chest);
			row.Key("inputLinkedWallTrap");
			writeLinkedWallTrap();
			row.Key("inputActiveObjectIds");
			WriteArray(stream, ObjectPoolAdapter::ActiveObjectIds(), [&](int id) { stream << id; });
			row.Key("input");
			WriteItemWorld(stream, before, true);
			row.Key("inputMissiles");
			WriteChestMissiles(stream, remotePlayer.position.tile);
			row.Boolean("inputMissilePreFlag", MissilePreFlag);
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, chest);
			const NativeItemWorldState after = CaptureItemWorld();
			row.Key("expectedChest");
			WriteRoomObject(stream, 0, chest);
			row.Key("expectedLinkedWallTrap");
			writeLinkedWallTrap();
			row.Key("expected");
			WriteItemWorld(stream, after, false);
			row.Key("expectedMissiles");
			WriteChestMissiles(stream, remotePlayer.position.tile);
			row.Boolean("expectedMissilePreFlag", MissilePreFlag);
			EXPECT_EQ(chest.selectionRegion, SelectionRegion::None);
			EXPECT_EQ(chest._oAnimFrame, 3U);
			EXPECT_EQ(chest._oRndSeed, lane.seed);
			if (lane.rawType >= 68 && lane.trapFlag) {
				EXPECT_FALSE(chest._oTrapFlag);
				EXPECT_EQ(Missiles.size(), 1U);
				ASSERT_FALSE(Missiles.empty());
				const Missile &missile = Missiles.front();
				EXPECT_EQ(missile._mitype, lane.trapKind == 0 ? MissileID::Arrow : MissileID::FireArrow);

				EXPECT_EQ(missile.position.start.x, chest.position.x);
				EXPECT_EQ(missile.position.start.y, chest.position.y);
				EXPECT_EQ(missile.position.tile.x, chest.position.x);
				EXPECT_EQ(missile.position.tile.y, chest.position.y);
				EXPECT_TRUE(missile.position.velocity.deltaX != 0 || missile.position.velocity.deltaY != 0);
				EXPECT_EQ(missile._misource, -1);
				EXPECT_EQ(missile._micaster, TARGET_PLAYERS);
				EXPECT_EQ(missile._midam, 0);
			} else {
				EXPECT_EQ(chest._oTrapFlag, lane.trapFlag);
				EXPECT_TRUE(Missiles.empty());
			}
			if (lane.itemCount == 0)
				EXPECT_EQ(after.activeCount, before.activeCount);
			if (lane.activeItems == MAXITEMS)
				EXPECT_EQ(after.activeCount, before.activeCount);
			if (lane.blockPlacement)
				EXPECT_EQ(after.activeCount, before.activeCount + 1);
			std::ostringstream firstState, firstChest, firstMissiles;
			WriteItemWorld(firstState, after, false);
			WriteRoomObject(firstChest, 0, chest);
			WriteChestMissiles(firstMissiles, remotePlayer.position.tile);
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, chest);
			const NativeItemWorldState repeated = CaptureItemWorld();
			row.Key("repeatedChest");
			WriteRoomObject(stream, 0, chest);
			row.Key("repeatedLinkedWallTrap");
			writeLinkedWallTrap();
			row.Key("repeatedActiveObjectIds");
			WriteArray(stream, ObjectPoolAdapter::ActiveObjectIds(), [&](int id) { stream << id; });
			row.Key("repeated");
			WriteItemWorld(stream, repeated, false);
			row.Key("repeatedMissiles");
			WriteChestMissiles(stream, remotePlayer.position.tile);
			row.Boolean("repeatedMissilePreFlag", MissilePreFlag);
			std::ostringstream repeatedState, repeatedChest, repeatedMissiles;
			WriteItemWorld(repeatedState, repeated, false);
			WriteRoomObject(repeatedChest, 0, chest);
			WriteChestMissiles(repeatedMissiles, remotePlayer.position.tile);
			EXPECT_EQ(firstState.str(), repeatedState.str());
			EXPECT_EQ(firstChest.str(), repeatedChest.str());
			EXPECT_EQ(firstMissiles.str(), repeatedMissiles.str());
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 31);
	EXPECT_LE(std::filesystem::file_size(root / fileName), 64U * 1024U * 1024U);
}

void WriteChestMissiles(std::ostream &out, WorldTilePosition operationTarget)
{
	// AddArrow stores velocity derived from AddMissile's destination, but does not persist that destination in the projectile union.
	out << '[';
	bool first = true;
	for (const Missile &missile : Missiles) {
		if (!first) out << ',';
		first = false;
		JsonObject row(out);
		row.Number("type", missile._mitype);
		row.Number("direction", missile.getDirection());
		row.Number("frameGroup", missile.getFrameGroupRaw());
		row.Number("startX", missile.position.start.x);
		row.Number("startY", missile.position.start.y);
		row.Number("tileX", missile.position.tile.x);
		row.Number("tileY", missile.position.tile.y);
		row.Number("velocityX", missile.position.velocity.deltaX);
		row.Number("velocityY", missile.position.velocity.deltaY);
		row.Number("destinationArgumentX", operationTarget.x);
		row.Number("destinationArgumentY", operationTarget.y);
		row.Number("source", missile._misource);
		row.Number("ownerKind", missile._micaster);
		row.Number("damage", missile._midam);
		row.Number("spellLevel", missile._mispllvl);
	}
	out << ']';
}

TEST_F(ThemeRoomGenerationTest, NativeThemeRoomGuardRestoresBestiarySizeAndEntries)
{
	const size_t originalSize = LevelBestiary.size();
	ASSERT_GT(originalSize, 0U);
	const _monster_id originalFirstType = LevelBestiary[0].type;
	{
		NativeThemeRoomGuard nestedGuard;
		LevelBestiary.init();
		EXPECT_TRUE(LevelBestiary.empty());
	}
	EXPECT_EQ(LevelBestiary.size(), originalSize);
	EXPECT_EQ(LevelBestiary[0].type, originalFirstType);
}

// Capture complete ordinary L1 sarcophagus operation state without asking the
// managed implementation to supply any expected native values.
TEST_F(ThemeRoomGenerationTest, CaptureNativeSarcophagusOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_SARCOPHAGUS_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the native sarcophagus capture directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode;
		~ExecutionGuard()
		{
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} executionGuard;
	gbSndInited = false;
	HeadlessMode = true;
	Player remotePlayer;
	std::ofstream stream(root / "native-sarcophagus-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-sarcophagus-operations");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-untrapped-cathedral-level1");
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		struct Lane {
			const char *name;
			int var1;
			uint32_t sarcophagusSeed;
			int itemCount;
			bool levelLoading;
			int spawnPattern; // 0 native default availability, 1 clear neighbors/occupied center, 2 three clear neighbors, 3 blocked.
		};
		constexpr Lane lanes[] = {
			{ "loot-var1-0", 0, 1, 2, false, 0 },
			{ "loot-var1-2-partial-pool", 2, 2588, MAXITEMS - 1, false, 0 },
			{ "loot-var1-2-full-pool", 2, UINT32_C(0x80000000), MAXITEMS, false, 0 },
			{ "empty-var1-3", 3, UINT32_MAX, 2, false, 0 },
			{ "empty-var1-7-loading", 7, 743271966, 2, true, 0 },
			{ "spawn-var1-8-default-neighbors", 8, 1, 2, false, 0 },
			{ "spawn-var1-9-default-neighbors", 9, 2588, 2, false, 0 },
			{ "spawn-var1-8-clear-neighbors-center-occupied", 8, 1, 2, false, 1 },
			{ "spawn-var1-9-negative-rng", 9, UINT32_C(1457187811), 2, false, 2 },
			{ "spawn-var1-8-no-space", 8, UINT32_C(0x80000000), 2, false, 3 },
		};
		constexpr std::array<Direction, 8> spawnDirections {
			Direction::North,
			Direction::NorthEast,
			Direction::East,
			Direction::NorthWest,
			Direction::SouthEast,
			Direction::West,
			Direction::SouthWest,
			Direction::South,
		};
		auto isSpawnTileAvailable = [](Point position) {
			return InDungeonBounds(position) && tileAt(position).player() == 0
			    && tileAt(position).monster() == 0 && IsTileWalkable(position);
		};
		auto writeAvailability = [&](Point center) {
			stream << '[' << (isSpawnTileAvailable(center) ? "true" : "false");
			for (Direction direction : spawnDirections)
				stream << ',' << (isSpawnTileAvailable(center + direction) ? "true" : "false");
			stream << ']';
		};
		auto writeNearbyTiles = [&](Point center) {
			stream << '[';
			std::array<Point, 9> positions { center };
			for (size_t i = 0; i < spawnDirections.size(); ++i)
				positions[i + 1] = center + spawnDirections[i];
			for (size_t i = 0; i < positions.size(); ++i) {
				if (i != 0) stream << ',';
				const Point position = positions[i];
				const Tile &tile = tileAt(position);
				JsonObject row(stream);
				row.Key("position");
				WriteRoomPoint(stream, position);
				row.Number("piece", tile.piece());
				row.Number("special", static_cast<int>(tile.special()));
				row.Number("transparency", static_cast<int>(tile.transVal()));
				row.Number("flags", static_cast<int>(tile.flags()));
				row.Number("corpse", static_cast<int>(tile.corpse()));
				row.Number("player", static_cast<int>(tile.player()));
				row.Number("monster", tile.monster());
				row.Number("object", static_cast<int>(tile.object()));
				row.Number("item", static_cast<int>(tile.item()));
				row.Number("light", static_cast<int>(tile.light()));
				row.Number("preLight", static_cast<int>(tile.preLight()));
				row.Number("tileProperties", static_cast<int>(tileProperties()[position.y * MAXDUNX + position.x]));
			}
			stream << ']';
		};
		auto writeActiveMonsters = [&]() {
			WriteArray(stream, MonsterPoolAdapter::ActiveMonsterIds(), [&](unsigned id) { stream << id; });
		};
		auto writeActiveObjects = [&]() {
			WriteArray(stream, ObjectPoolAdapter::ActiveObjectIds(), [&](int id) { stream << id; });
		};
		for (const Lane &lane : lanes) {
			PrepareRoom(1, lane.itemCount, 0, 0, false);
			MyPlayer->pLvlLoad = lane.levelLoading ? 1 : 0;
			MyPlayer->position.tile = { 40, 40 };
			MyPlayer->position.future = { 40, 40 };
			tileAt(50, 50).setPlayer(0);
			tileAt(51, 50).setMonster(0);
			tileAt(40, 40).setPlayer(-1);
			Object *sarcophagus = AddObjectPresentation(OBJ_SARC, { 50, 50 });
			ASSERT_NE(sarcophagus, nullptr);
			const int objectSlot = static_cast<int>(sarcophagus->GetId());
			ASSERT_GE(objectSlot, 0);
			ASSERT_LT(objectSlot, MAXOBJECTS);
			ASSERT_NE(sarcophagus->selectionRegion, SelectionRegion::None);
			tileAt({ 50, 49 }).setObject(-(objectSlot + 1));
			sarcophagus->_oRndSeed = lane.sarcophagusSeed;
			sarcophagus->_oVar1 = lane.var1;
			sarcophagus->_oVar2 = -1;
			Monster *linked = nullptr;
			if (lane.var1 >= 8) {
				linked = PreSpawnSkeleton();
				ASSERT_NE(linked, nullptr);
				sarcophagus->_oVar2 = static_cast<int>(linked->getId());
			}
			if (lane.spawnPattern != 0) {
				for (int y = 49; y <= 51; ++y)
					for (int x = 49; x <= 51; ++x)
						tileAt(x, y).setPlayer(1);
				if (lane.spawnPattern == 1) {
					for (int y = 49; y <= 51; ++y)
						for (int x = 49; x <= 51; ++x)
							if (x != 50 || y != 50) tileAt(x, y).setPlayer(0);
				} else if (lane.spawnPattern == 2) {
					tileAt(49, 49).setPlayer(0); // North
					tileAt(51, 49).setPlayer(0); // East
					tileAt(49, 50).setPlayer(0); // NorthWest
				} else if (lane.spawnPattern != 3) {
					FAIL() << "Unknown sarcophagus spawn pattern.";
				}
			}
			EXPECT_FALSE(isSpawnTileAvailable(sarcophagus->position));
			if (lane.spawnPattern == 2) {
				std::array<bool, 9> available {};
				available[0] = isSpawnTileAvailable(sarcophagus->position);
				for (size_t i = 0; i < spawnDirections.size(); ++i)
					available[i + 1] = isSpawnTileAvailable(sarcophagus->position + spawnDirections[i]);
				EXPECT_EQ(std::count(available.begin(), available.end(), true), 3);
				EXPECT_TRUE(available[1]); // North
				EXPECT_TRUE(available[3]); // East
				EXPECT_TRUE(available[4]); // NorthWest
			}
			SetRndSeed(UINT32_C(0x12345678));
			const NativeItemWorldState beforeItems = CaptureItemWorld();
			const size_t activeMonsterCountBefore = ActiveMonsterCount;
			const int linkedSlot = linked == nullptr ? -1 : static_cast<int>(linked->getId());
			const int specialGraphic = static_cast<int>(MonsterGraphic::Special);
			const int specialFrameCount = linked == nullptr ? 0 : linked->type().anims[specialGraphic].frames;
			const int specialTicksPerFrame = linked == nullptr ? 0 : linked->type().anims[specialGraphic].rate;
			if (caseCount++ != 0) stream << ',';
			JsonObject row(stream);
			row.String("name", lane.name);
			row.Number("depth", 1);
			row.Number("slot", objectSlot);
			row.Number("var1", lane.var1);
			row.Number("var2", sarcophagus->_oVar2);
			row.Number("sarcophagusSeed", sarcophagus->_oRndSeed);
			row.Boolean("levelLoading", lane.levelLoading);
			row.Number("linkedMonsterSlot", linkedSlot);
			row.Number("specialFrameCount", specialFrameCount);
			row.Number("specialTicksPerFrame", specialTicksPerFrame);
			row.Key("spawnTilesAvailable");
			writeAvailability(sarcophagus->position);
			row.Key("inputSarcophagus");
			WriteRoomObject(stream, objectSlot, *sarcophagus);
			row.Key("input");
			WriteItemWorld(stream, beforeItems, true);
			row.Number("inputActiveObjectCount", ActiveObjectCount);
			row.Key("inputActiveObjectIds");
			writeActiveObjects();
			row.Key("inputActiveMonsterIds");
			writeActiveMonsters();
			row.Key("inputLinkedMonster");
			if (linked != nullptr)
				WriteRoomMonster(stream, linkedSlot, *linked);
			else
				stream << "null";
			row.Boolean("inputLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, *sarcophagus);
			const NativeItemWorldState afterItems = CaptureItemWorld();
			EXPECT_EQ(sarcophagus->selectionRegion, SelectionRegion::None);
			EXPECT_TRUE(sarcophagus->_oAnimFlag);
			EXPECT_EQ(sarcophagus->_oAnimDelay, 3);
			EXPECT_EQ(sarcophagus->_oRndSeed, lane.sarcophagusSeed);
			EXPECT_EQ(ActiveMonsterCount, activeMonsterCountBefore);
			row.Number("expectedRandomState", GetLCGEngineState());
			row.Key("expectedSarcophagus");
			WriteRoomObject(stream, objectSlot, *sarcophagus);
			row.Key("expected");
			WriteItemWorld(stream, afterItems, false);
			row.Number("expectedActiveObjectCount", ActiveObjectCount);
			row.Key("expectedActiveObjectIds");
			writeActiveObjects();
			row.Key("expectedActiveMonsterIds");
			writeActiveMonsters();
			row.Key("expectedSpawnTiles");
			writeNearbyTiles(sarcophagus->position);
			row.Key("expectedLinkedMonster");
			if (linked != nullptr)
				WriteRoomMonster(stream, linkedSlot, *linked);
			else
				stream << "null";
			row.Boolean("expectedLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			std::ostringstream expectedState;
			WriteItemWorld(expectedState, afterItems, false);
			std::ostringstream expectedObject;
			WriteRoomObject(expectedObject, objectSlot, *sarcophagus);
			std::ostringstream expectedActor;
			if (linked != nullptr) WriteRoomMonster(expectedActor, linkedSlot, *linked);
			const auto expectedActiveMonsterIds = std::vector<unsigned>(MonsterPoolAdapter::ActiveMonsterIds().begin(),
			    MonsterPoolAdapter::ActiveMonsterIds().end());
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, *sarcophagus);
			const NativeItemWorldState repeatedItems = CaptureItemWorld();
			std::ostringstream repeatedState;
			WriteItemWorld(repeatedState, repeatedItems, false);
			std::ostringstream repeatedObject;
			WriteRoomObject(repeatedObject, objectSlot, *sarcophagus);
			std::ostringstream repeatedActor;
			if (linked != nullptr) WriteRoomMonster(repeatedActor, linkedSlot, *linked);
			const auto repeatedActiveMonsterIds = std::vector<unsigned>(MonsterPoolAdapter::ActiveMonsterIds().begin(),
			    MonsterPoolAdapter::ActiveMonsterIds().end());
			row.Key("repeatedSarcophagus");
			WriteRoomObject(stream, objectSlot, *sarcophagus);
			row.Key("repeated");
			WriteItemWorld(stream, repeatedItems, false);
			row.Number("repeatedActiveObjectCount", ActiveObjectCount);
			row.Key("repeatedActiveObjectIds");
			writeActiveObjects();
			row.Key("repeatedActiveMonsterIds");
			writeActiveMonsters();
			row.Key("repeatedLinkedMonster");
			if (linked != nullptr)
				WriteRoomMonster(stream, linkedSlot, *linked);
			else
				stream << "null";
			row.Boolean("repeatedLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			EXPECT_EQ(expectedState.str(), repeatedState.str());
			EXPECT_EQ(expectedObject.str(), repeatedObject.str());
			EXPECT_EQ(expectedActor.str(), repeatedActor.str());
			EXPECT_EQ(expectedActiveMonsterIds, repeatedActiveMonsterIds);
			EXPECT_EQ(ActiveMonsterCount, activeMonsterCountBefore);
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 10);
	EXPECT_LE(std::filesystem::file_size(root / "native-sarcophagus-operations.json"), 64U * 1024U * 1024U);
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeTrapArrowBoundaries)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_TRAP_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the native trap capture directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode;
		bool networkInitialized = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		std::list<Missile> savedMissiles;
		bool preFlag = MissilePreFlag;
		ExecutionGuard() { Missiles.swap(savedMissiles); }
		~ExecutionGuard()
		{
			Missiles.swap(savedMissiles);
			MissilePreFlag = preFlag;
			if (networkInitialized) SNetDestroy();
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} guard;
	ASSERT_TRUE(guard.networkInitialized);
	HeadlessMode = true;
	gbSndInited = false;
	LoadPlayerDataFiles();
	LoadMissileData();
	std::ofstream stream(root / "native-trap-arrow.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-trap-arrow");
		document.Number("version", 1);
		document.Key("players");
		stream << '[';
		for (int lane = 0; lane < 5; ++lane) {
			PrepareRoom(1, 2, 0, 0, false);
			MyPlayer->_pmode = PM_STAND;
			MyPlayer->_pInvincible = false;
			MyPlayer->_pSpellFlags = SpellFlag::None;
			MyPlayer->_pIFlags = lane == 4 ? ItemSpecialEffect::HalfTrapDamage : ItemSpecialEffect::None;
			MyPlayer->_pIGetHit = 0;
			MyPlayer->_pBlockFlag = lane == 2;
			MyPlayer->attributes.dexterity.base = lane == 2 ? 100 : 0;
			MyPlayer->attributes.dexterity.current = lane == 2 ? 100 : 0;
			MyPlayer->life.current = MyPlayer->life.maximumBase;
			MyPlayer->life.base = MyPlayer->life.current;
			MyPlayer->life.maximum = MyPlayer->life.maximumBase;
			MyPlayer->mana.current = MyPlayer->mana.maximumBase;
			if (lane == 3) MyPlayer->_pSpellFlags |= SpellFlag::Etherealize;
			const int distance = lane == 1 ? 100 : 1;
			const uint32_t seed = lane == 2 ? 0 : UINT32_C(0x12345678);
			SetRndSeed(seed);
			if (lane != 0) stream << ',';
			JsonObject row(stream);
			row.Number("lane", lane);
			row.Number("seed", seed);
			row.Number("distance", distance);
			row.Number("inputLife", MyPlayer->life.current);
			row.Number("inputMana", MyPlayer->mana.current);
			row.Number("maximumLife", MyPlayer->life.maximumBase);
			row.Number("maximumMana", MyPlayer->mana.maximumBase);
			row.Number("armor", MyPlayer->GetArmor());
			row.Number("dexterity", MyPlayer->attributes.dexterity.current);
			bool blocked = false;
			const bool hit = PlayerMHit(*MyPlayer, nullptr, distance, 1, 2, MissileID::Arrow,
			    DamageType::Physical, false, DeathReason::MonsterOrTrap, &blocked);
			row.Boolean("hit", hit);
			row.Boolean("blocked", blocked);
			row.Number("life", MyPlayer->life.current);
			row.Number("mana", MyPlayer->mana.current);
			row.Number("mode", static_cast<int>(MyPlayer->_pmode));
			row.Number("rng", GetLCGEngineState());
		}
		stream << ']';
		PrepareRoom(1, 2, 0, 0, false);
		MyPlayer->_pmode = PM_STAND;
		MyPlayer->_pInvincible = false;
		MyPlayer->_pSpellFlags = SpellFlag::None;
		tileAt(50, 50).setPlayer(0);
		tileAt(51, 50).setMonster(0);
		Missiles.clear();
		Object *trigger = AddObjectPresentation(OBJ_CHEST1, { 52, 50 });
		Object *trap = AddObjectPresentation(OBJ_TRAPL, { 48, 50 });
		ASSERT_NE(trigger, nullptr);
		ASSERT_NE(trap, nullptr);
		trigger->_oTrapFlag = true;
		trigger->selectionRegion = SelectionRegion::None;
		trap->_oVar1 = 52;
		trap->_oVar2 = 50;
		trap->_oVar3 = static_cast<int>(MissileID::Arrow);
		trap->_oVar4 = 0;
		SetRndSeed(UINT32_C(0x12345678));
		ProcessObjects();
		ASSERT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(trap->_oVar4, 1);
		EXPECT_FALSE(trigger->_oTrapFlag);
		document.Key("flight");
		stream << '[';
		for (int step = 0; step < 3; ++step) {
			ProcessObjects();
			ProcessMissiles();
			ASSERT_EQ(Missiles.size(), 1U);
			const Missile &arrow = Missiles.front();
			if (step != 0) stream << ',';
			JsonObject row(stream);
			row.Number("distance", arrow._midist);
			row.Number("remaining", arrow.duration);
			row.Number("x", arrow.position.tile.x);
			row.Number("y", arrow.position.tile.y);
			row.Number("vx", arrow.position.velocity.deltaX);
			row.Number("vy", arrow.position.velocity.deltaY);
			row.Number("tx", arrow.position.traveled.deltaX);
			row.Number("ty", arrow.position.traveled.deltaY);
			row.Number("hash", arrow.lastCollisionTargetHash);
			row.Number("rng", GetLCGEngineState());
		}
		stream << ']';
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeBarrelOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the native barrel capture directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode;
		bool networkInitialized = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		~ExecutionGuard()
		{
			if (networkInitialized) SNetDestroy();
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} executionGuard;
	ASSERT_TRUE(executionGuard.networkInitialized);
	gbSndInited = false;
	HeadlessMode = true;
	// Class/experience tables are canonical process-lifetime data in native table tests.
	LoadPlayerDataFiles();
	LoadMissileData();
	ASSERT_GT(GetMaximumCharacterLevel(), 0);
	std::ofstream stream(root / "native-barrel-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-barrel-operations");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-untrapped-cathedral-level1");
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		auto writePlayer = [&](const Player &player) {
			JsonObject json(stream);
			json.Number("id", player.getId());
			json.Number("level", player.plrlevel);
			json.Boolean("active", player.plractive);
			json.Number("mode", static_cast<int>(player._pmode));
			json.Number("characterLevel", player.getCharacterLevel());
			json.Number("experience", player._pExperience);
			json.Number("strength", player.attributes.strength.current);
			json.Number("dexterity", player.attributes.dexterity.current);
			json.Number("magic", player.attributes.magic.current);
			json.Key("tile");
			WriteRoomPoint(stream, player.position.tile);
			json.Key("future");
			WriteRoomPoint(stream, player.position.future);
			json.Number("life", player.life.current);
			json.Number("lifeMaximumBase", player.life.maximumBase);
			json.Number("mana", player.mana.current);
			json.Number("manaMaximumBase", player.mana.maximumBase);
			json.Number("armor", player.GetArmor());
			json.Number("blockChance", player.GetBlockChance(false));
			json.Boolean("blockFlag", player._pBlockFlag);
			json.Boolean("invincible", player._pInvincible);
			json.Number("fireResistance", player._pFireResist);
			json.Number("lightningResistance", player._pLghtResist);
			json.Number("magicResistance", player._pMagResist);
			json.Number("getHit", player._pIGetHit);
			json.Number("itemFlags", static_cast<uint32_t>(player._pIFlags));
		};
		auto addBarrel = [&](int type, Point position, uint32_t seed, int var2, int var3, int var4) -> Object * {
			Object *barrel = AddObjectPresentation(static_cast<_object_id>(type), position);
			if (barrel == nullptr) return nullptr;
			barrel->_oVar1 = 0;
			barrel->_oVar2 = var2;
			barrel->_oVar3 = var3;
			barrel->_oVar4 = var4;
			barrel->_oRndSeed = seed;
			return barrel;
		};
		auto prepare = [&](int itemCount = 2, int monsterCount = 0) {
			PrepareRoom(1, itemCount, 0, monsterCount, false);
			MyPlayer->pLvlLoad = 0;
			MyPlayer->setCharacterLevel(1);
			MyPlayer->life.base = MyPlayer->life.maximumBase;
			MyPlayer->life.maximum = MyPlayer->life.maximumBase;
			MyPlayer->life.current = MyPlayer->life.maximumBase;
			MyPlayer->mana.base = MyPlayer->mana.maximumBase;
			MyPlayer->mana.maximum = MyPlayer->mana.maximumBase;
			MyPlayer->mana.current = MyPlayer->mana.maximumBase;
			MyPlayer->_pExperience = 0;
			MyPlayer->_pmode = PM_STAND;
			MyPlayer->_pInvincible = false;
			MyPlayer->_pBlockFlag = false;
			MyPlayer->_pFireResist = 0;
			MyPlayer->_pLghtResist = 0;
			MyPlayer->_pMagResist = 0;
			MyPlayer->_pIGetHit = 0;
			MyPlayer->attributes.dexterity.base = 0;
			MyPlayer->attributes.dexterity.current = 0;
			tileAt(50, 50).setPlayer(0);
			tileAt(51, 50).setPlayer(0);
			tileAt(51, 50).setMonster(0);
		};
		auto setPlayerPosition = [&](Point position) {
			tileAt(MyPlayer->position.tile).setPlayer(0);
			MyPlayer->position.tile = position;
			MyPlayer->position.future = position;
			MyPlayer->position.old = position;
			tileAt(position).setPlayer(1); // Positive current occupancy is required by PlayerAtPosition(..., true).
		};
		auto capture = [&](const char *name, Object &barrel, uint32_t initialRandomState, const Monster *lootActor = nullptr) {
			SetRndSeed(initialRandomState);
			const NativeItemWorldState beforeItems = CaptureItemWorld();
			const bool originalSolid = barrel._oSolidFlag;
			barrel._oSolidFlag = false; // BreakBarrel removes solidity before ActivateSkeleton.
			constexpr std::array<Direction, 8> spawnDirections {
				Direction::North,
				Direction::NorthEast,
				Direction::East,
				Direction::NorthWest,
				Direction::SouthEast,
				Direction::West,
				Direction::SouthWest,
				Direction::South,
			};
			std::array<bool, 9> spawnTilesAvailable {};
			auto isSpawnTileAvailable = [](Point position) {
				return InDungeonBounds(position) && tileAt(position).player() == 0
				    && tileAt(position).monster() == 0 && IsTileWalkable(position);
			};
			spawnTilesAvailable[0] = isSpawnTileAvailable(barrel.position);
			for (size_t index = 0; index < spawnDirections.size(); ++index)
				spawnTilesAvailable[index + 1] = isSpawnTileAvailable(barrel.position + spawnDirections[index]);
			barrel._oSolidFlag = originalSolid;
			if (caseCount++ != 0) stream << ',';
			JsonObject row(stream);
			row.String("name", name);
			row.Number("depth", 1);
			row.Number("slot", barrel.GetId());
			row.Number("initialRandomState", initialRandomState);
			row.Number("barrelSeed", barrel._oRndSeed);
			row.Number("nativeType", barrel._otype);
			row.Number("var1", barrel._oVar1);
			row.Number("var2", barrel._oVar2);
			row.Number("var3", barrel._oVar3);
			row.Number("var4", barrel._oVar4);
			row.Boolean("levelLoading", MyPlayer->pLvlLoad != 0);
			row.Key("spawnTilesAvailable");
			stream << '[';
			for (size_t index = 0; index < spawnTilesAvailable.size(); ++index) {
				if (index != 0) stream << ',';
				stream << (spawnTilesAvailable[index] ? "true" : "false");
			}
			stream << ']';
			const Monster *linked = barrel._oVar4 >= 0 && barrel._oVar4 < MaxMonsters
			    ? &Monsters[barrel._oVar4]
			    : nullptr;
			row.Number("linkedMonsterSlot", linked == nullptr ? -1 : barrel._oVar4);
			row.Key("inputLinkedMonster");
			if (linked == nullptr)
				stream << "null";
			else
				WriteRoomMonster(stream, barrel._oVar4, *linked);
			row.Key("lootActorContext");
			if (lootActor == nullptr) {
				stream << "null";
			} else {
				JsonObject context(stream);
				context.Number("slot", lootActor->getId());
				context.Number("type", lootActor->type().type);
				context.Number("level", lootActor->level(DIFF_NORMAL));
				context.Number("experience", lootActor->exp(DIFF_NORMAL));
				context.Number("treasure", lootActor->data().treasure);
				context.Number("whoHit", lootActor->whoHit);
				context.Number("rndItemSeed", lootActor->rndItemSeed);
			}
			row.Boolean("inputLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			const int specialGraphic = static_cast<int>(MonsterGraphic::Special);
			row.Number("specialFrameCount", linked == nullptr ? 0 : linked->type().anims[specialGraphic].frames);
			row.Number("specialTicksPerFrame", linked == nullptr ? 0 : linked->type().anims[specialGraphic].rate);
			row.Key("inputBarrel");
			WriteRoomObject(stream, barrel.GetId(), barrel);
			row.Key("inputItems");
			WriteItemWorld(stream, beforeItems, true);
			row.Key("inputPlayer");
			writePlayer(*MyPlayer);
			row.Key("inputWorld");
			WriteRoomWorld(stream);
			BreakObject(*MyPlayer, barrel);
			const NativeItemWorldState afterItems = CaptureItemWorld();
			row.Number("expectedRandomState", GetLCGEngineState());
			row.Key("expectedBarrel");
			WriteRoomObject(stream, barrel.GetId(), barrel);
			row.Key("expectedItems");
			WriteItemWorld(stream, afterItems, false);
			row.Key("expectedPlayer");
			writePlayer(*MyPlayer);
			row.Key("expectedWorld");
			WriteRoomWorld(stream);
			EXPECT_FALSE(barrel.canInteractWith());
			EXPECT_FALSE(barrel._oSolidFlag);
			EXPECT_TRUE(barrel._oMissFlag);
			EXPECT_EQ(barrel._oAnimFrame, 1U);
			const int lifeAfterFirstBreak = MyPlayer->life.current;
			const int manaAfterFirstBreak = MyPlayer->mana.current;
			const Point positionAfterFirstBreak = MyPlayer->position.tile;
			const auto modeAfterFirstBreak = MyPlayer->_pmode;
			std::ostringstream firstWorld, firstItems;
			WriteRoomWorld(firstWorld);
			WriteItemWorld(firstItems, afterItems, false);
			BreakObject(*MyPlayer, barrel); // Broken barrels are a native no-op; no second drop or blast.
			const NativeItemWorldState repeatedItems = CaptureItemWorld();
			row.Key("repeatedBarrel");
			WriteRoomObject(stream, barrel.GetId(), barrel);
			row.Key("repeatedItems");
			WriteItemWorld(stream, repeatedItems, false);
			row.Key("repeatedPlayer");
			writePlayer(*MyPlayer);
			row.Key("repeatedWorld");
			WriteRoomWorld(stream);
			std::ostringstream repeatedWorld, repeatedItemsJson;
			WriteRoomWorld(repeatedWorld);
			WriteItemWorld(repeatedItemsJson, repeatedItems, false);
			EXPECT_EQ(firstWorld.str(), repeatedWorld.str());
			EXPECT_EQ(firstItems.str(), repeatedItemsJson.str());
			EXPECT_EQ(lifeAfterFirstBreak, MyPlayer->life.current);
			EXPECT_EQ(manaAfterFirstBreak, MyPlayer->mana.current);
			EXPECT_EQ(positionAfterFirstBreak, MyPlayer->position.tile);
			EXPECT_EQ(modeAfterFirstBreak, MyPlayer->_pmode);
		};

		prepare();
		Object *useful = addBarrel(OBJ_BARREL, { 50, 50 }, 2588, 0, 0, -1);
		ASSERT_NE(useful, nullptr);
		capture("ordinary-useful", *useful, UINT32_C(0x12345678));

		prepare();
		Object *random = addBarrel(OBJ_BARREL, { 50, 50 }, 1, 1, 1, -1);
		ASSERT_NE(random, nullptr);
		capture("ordinary-random", *random, UINT32_C(0x80000000));

		prepare();
		Object *empty = addBarrel(OBJ_BARREL, { 50, 50 }, UINT32_MAX, 2, 2, -1);
		ASSERT_NE(empty, nullptr);
		capture("ordinary-empty", *empty, UINT32_C(0x12345678));

		prepare();
		Object *linkedBarrel = addBarrel(OBJ_BARREL, { 50, 50 }, 743271966, 8, 1, -1);
		ASSERT_NE(linkedBarrel, nullptr);
		Monster *linked = PreSpawnSkeleton();
		ASSERT_NE(linked, nullptr);
		linkedBarrel->_oVar4 = static_cast<int>(linked->getId());
		capture("ordinary-linked-skeleton", *linkedBarrel, UINT32_C(0x87654321));

		prepare();
		for (int y = 49; y <= 51; ++y) {
			for (int x = 49; x <= 51; ++x) {
				const bool available = (x == 50 && y == 49) || (x == 51 && y == 49) || (x == 51 && y == 50);
				if (!available)
					tileAt(x, y).setPiece(2);
			}
		}
		SetRndSeed(UINT32_C(0xA11CE));
		Object *neighborBarrel = addBarrel(OBJ_BARREL, { 50, 50 }, 743271966, 8, 1, -1);
		ASSERT_NE(neighborBarrel, nullptr);
		Monster *neighborLinked = PreSpawnSkeleton();
		ASSERT_NE(neighborLinked, nullptr);
		neighborBarrel->_oVar4 = static_cast<int>(neighborLinked->getId());
		capture("ordinary-linked-skeleton-signed-neighbor", *neighborBarrel, UINT32_C(1457187811));

		auto findPlayerSeed = [](bool shouldHit) {
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				const int roll = GenerateRnd(100);
				if ((roll < 40) == shouldHit) return candidate;
			}
			return UINT32_MAX;
		};
		auto findPlayerBlockSeed = [] {
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				const int hit = GenerateRnd(100);
				const int block = GenerateRnd(100);
				if (hit < 40 && block < std::clamp(MyPlayer->GetBlockChance(false), 0, 100))
					return candidate;
			}
			return UINT32_MAX;
		};
		auto addBlastActor = [&](int resistance, int hitPoints, int whoHit) {
			SetRndSeed(UINT32_C(0xA11CE));
			Monster *actor = PreSpawnSkeleton();
			if (actor == nullptr)
				return actor;
			ActivateSkeleton(*actor, { 51, 50 });
			actor->resistance = static_cast<uint16_t>(resistance);
			actor->hitPoints = hitPoints;
			actor->maxHitPoints = std::max(hitPoints, 6400);
			actor->whoHit = static_cast<int8_t>(whoHit);
			return actor;
		};
		auto findMonsterHitSeed = [](const Monster &actor) {
			const int hitChance = std::clamp(90 - static_cast<int>(actor.armorClass), 5, 95);
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				if (GenerateRnd(100) < hitChance)
					return candidate;
			}
			return UINT32_MAX;
		};
		auto findMonsterLootSeed = [](const Monster &actor) {
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				if (RndItemForMonsterLevel(static_cast<int8_t>(actor.level(DIFF_NORMAL))) != IDI_NONE)
					return candidate;
			}
			return UINT32_MAX;
		};
		prepare();
		Object *hit = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(hit, nullptr);
		setPlayerPosition({ 50, 50 });
		const uint32_t hitSeed = findPlayerSeed(true);
		ASSERT_NE(hitSeed, UINT32_MAX);
		capture("explosive-player-hit", *hit, hitSeed);

		prepare();
		Object *miss = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(miss, nullptr);
		setPlayerPosition({ 50, 50 });
		const uint32_t missSeed = findPlayerSeed(false);
		ASSERT_NE(missSeed, UINT32_MAX);
		capture("explosive-player-miss", *miss, missSeed);

		prepare();
		MyPlayer->attributes.dexterity.base = 100;
		MyPlayer->attributes.dexterity.current = 100;
		MyPlayer->_pBlockFlag = true;
		Object *blocked = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(blocked, nullptr);
		setPlayerPosition({ 50, 50 });
		const uint32_t blockSeed = findPlayerBlockSeed();
		ASSERT_NE(blockSeed, UINT32_MAX);
		capture("explosive-player-blocked", *blocked, blockSeed);

		prepare();
		MyPlayer->_pFireResist = 50;
		Object *resistedPlayer = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(resistedPlayer, nullptr);
		setPlayerPosition({ 50, 50 });
		capture("explosive-player-fire-resisted", *resistedPlayer, findPlayerSeed(true));

		prepare(2, 0);
		Monster *resistedActor = addBlastActor(RESIST_FIRE, 6400, 0);
		ASSERT_NE(resistedActor, nullptr);
		Object *resistedMonster = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(resistedMonster, nullptr);
		capture("explosive-monster-fire-resisted", *resistedMonster, findMonsterHitSeed(*resistedActor), resistedActor);

		prepare(2, 0);
		Monster *immuneActor = addBlastActor(IMMUNE_FIRE, 6400, 0);
		ASSERT_NE(immuneActor, nullptr);
		Object *immuneMonster = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(immuneMonster, nullptr);
		capture("explosive-monster-fire-immune", *immuneMonster, UINT32_C(0xC001D00D), immuneActor);

		for (const auto &[name, whoHit] : std::array<std::pair<const char *, int>, 2> {
		         std::pair { "explosive-monster-lethal-uncredited", 0 },
		         std::pair { "explosive-monster-lethal-credited", 1 } }) {
			prepare(2, 0);
			Monster *lethalActor = addBlastActor(0, 64, whoHit);
			ASSERT_NE(lethalActor, nullptr);
			const uint32_t lootSeed = findMonsterLootSeed(*lethalActor);
			ASSERT_NE(lootSeed, UINT32_MAX);
			lethalActor->rndItemSeed = lootSeed;
			Object *lethalMonster = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
			ASSERT_NE(lethalMonster, nullptr);
			const uint32_t hitSeed = findMonsterHitSeed(*lethalActor);
			ASSERT_NE(hitSeed, UINT32_MAX);
			capture(name, *lethalMonster, hitSeed, lethalActor);
			EXPECT_EQ(lethalActor->hitPoints, 0);
			EXPECT_EQ(MyPlayer->_pExperience > 0, whoHit != 0);
		}

		prepare(2, 0);
		MyPlayer->setCharacterLevel(3);
		MyPlayer->_pExperience = GetNextExperienceThresholdForLevel(2);
		Monster *scaledActor = addBlastActor(0, 64, 1);
		ASSERT_NE(scaledActor, nullptr);
		scaledActor->rndItemSeed = findMonsterLootSeed(*scaledActor);
		Object *scaledBarrel = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(scaledBarrel, nullptr);
		const uint32_t beforeScaledExperience = MyPlayer->_pExperience;
		capture("explosive-monster-lethal-credited-level-delta", *scaledBarrel,
		    findMonsterHitSeed(*scaledActor), scaledActor);
		EXPECT_EQ(MyPlayer->_pExperience - beforeScaledExperience, 51U);

		prepare(2, 0);
		Monster *occupant = PreSpawnSkeleton();
		ASSERT_NE(occupant, nullptr);
		ActivateSkeleton(*occupant, { 51, 50 });
		ASSERT_EQ(occupant->position.tile, (WorldTilePosition { 51, 50 }));
		Object *chainRoot = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		Object *chainChild = addBarrel(OBJ_BARRELEX, { 51, 50 }, 2, 0, 0, -1);
		ASSERT_NE(chainRoot, nullptr);
		ASSERT_NE(chainChild, nullptr);
		setPlayerPosition({ 51, 50 });
		capture("explosive-chain-monster-player-order", *chainRoot, UINT32_C(0x5EED1234));
		EXPECT_FALSE(chainChild->canInteractWith());
		EXPECT_FALSE(chainChild->_oSolidFlag);
		prepare(2, 0);
		Monster *northwestActor = addBlastActor(0, 256, 0);
		ASSERT_NE(northwestActor, nullptr);
		tileAt(northwestActor->position.tile).setMonster(0);
		northwestActor->position.tile = { 48, 48 };
		northwestActor->position.future = { 48, 48 };
		northwestActor->position.old = { 48, 48 };
		tileAt(48, 48).setMonster(northwestActor->getId() + 1);
		Object *threeRoot = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		Object *threeNorthwest = addBarrel(OBJ_BARRELEX, { 49, 49 }, 2, 0, 0, -1);
		Object *threeSouth = addBarrel(OBJ_BARRELEX, { 50, 51 }, 3, 0, 0, -1);
		ASSERT_NE(threeRoot, nullptr);
		ASSERT_NE(threeNorthwest, nullptr);
		ASSERT_NE(threeSouth, nullptr);
		setPlayerPosition({ 51, 50 });
		capture("explosive-three-barrel-nw-monster-player-order", *threeRoot, 2588, northwestActor);
		EXPECT_FALSE(threeNorthwest->canInteractWith());
		EXPECT_FALSE(threeSouth->canInteractWith());
		EXPECT_EQ(northwestActor->hitPoints, 128);
		EXPECT_EQ(MyPlayer->life.current, 5422);
		EXPECT_EQ(GetLCGEngineState(), UINT32_C(1630774887));
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 16);
	EXPECT_LE(std::filesystem::file_size(root / "native-barrel-operations.json"), 64U * 1024U * 1024U);
}

TEST_F(ThemeRoomGenerationTest, FullObjectPoolRetainsSlotsAndFailedShrineCarry)
{
	PrepareRoom(1, 2, MAXOBJECTS, MaxMonsters, false);
	numThemes() = 1;
	themes[0] = { THEME_LIBRARY, 1 };
	SetRndSeed(2588);
	const auto before = CaptureThemeRoomGlobalsForTesting();
	CreateThemeRooms();
	EXPECT_EQ(ActiveObjectCount, MAXOBJECTS);
	EXPECT_EQ(ActiveMonsterCount, MaxMonsters);
	EXPECT_EQ(CaptureThemeRoomGlobalsForTesting().variant, before.variant);
	EXPECT_EQ(CaptureThemeRoomGlobalsForTesting().x, 0);
	EXPECT_EQ(CaptureThemeRoomGlobalsForTesting().y, 0);
	for (int slot = 0; slot < MAXOBJECTS; ++slot) {
		EXPECT_EQ(Objects[slot]._oVar1, 91);
		EXPECT_EQ(Objects[slot]._oVar2, -17);
		EXPECT_EQ(Objects[slot]._oRndSeed, 1000U + slot);
	}
}

TEST_F(ThemeRoomGenerationTest, NoSkeletonBarrelRetainsMinusOneWithoutSelectionDraw)
{
	LevelBestiary.init();
	ASSERT_TRUE(LevelBestiary.addType(MT_GOLEM, PLACE_SPECIAL).has_value());
	ASSERT_TRUE(LevelBestiary.addType(MT_NZOMBIE, PLACE_SCATTER).has_value());
	uint32_t selectedSeed = 0, expectedState = 0;
	int expectedVar2 = 0, expectedVar3 = 0;
	bool found = false;
	// The actual bundled barrel is not animated. Only seed/var2/var3 draw;
	// AddSkeleton's empty candidate list consumes no random value.
	ASSERT_EQ(static_cast<uint8_t>(AllObjects[OBJ_BARREL].flags) & 1U, 0U);
	for (uint32_t seed = 0; seed < 4096 && !found; ++seed) {
		SetRndSeed(seed);
		AdvanceRndSeed();
		expectedVar2 = GenerateRnd(10);
		expectedVar3 = GenerateRnd(3);
		if (expectedVar2 >= 8) {
			selectedSeed = seed;
			expectedState = GetLCGEngineState();
			found = true;
		}
	}
	ASSERT_TRUE(found);
	PrepareRoom(1);
	SetRndSeed(selectedSeed);
	Object *barrel = AddObject(OBJ_BARREL, { 50, 50 });
	ASSERT_NE(barrel, nullptr);
	EXPECT_EQ(barrel->_oVar2, expectedVar2);
	EXPECT_EQ(barrel->_oVar3, expectedVar3);
	EXPECT_EQ(barrel->_oVar4, -1);
	EXPECT_EQ(ActiveMonsterCount, 4U);
	EXPECT_EQ(GetLCGEngineState(), expectedState);
	RecordProperty("barrelSeed", std::to_string(selectedSeed));
}

TEST_F(ThemeRoomGenerationTest, BlockedSkeletonWithNoNeighborRetainsReservationWithoutDraw)
{
	PrepareRoom(1);
	SetRndSeed(2588);
	Monster *skeleton = PreSpawnSkeleton();
	ASSERT_NE(skeleton, nullptr);
	const auto slot = skeleton->getId();
	const auto count = ActiveMonsterCount;
	const uint32_t randomState = GetLCGEngineState();
	for (int y = 49; y <= 51; ++y)
		for (int x = 49; x <= 51; ++x)
			tileAt(x, y).setPlayer(1);
	ActivateSkeleton(*skeleton, { 50, 50 });
	EXPECT_EQ(skeleton->position.tile, (WorldTilePosition { 0, 0 }));
	EXPECT_EQ(skeleton->position.future, (WorldTilePosition { 0, 0 }));
	EXPECT_EQ(skeleton->mode, MonsterMode::Stand);
	EXPECT_EQ(ActiveMonsterCount, count);
	EXPECT_EQ(ActiveMonsters[count - 1], slot);
	EXPECT_EQ(GetLCGEngineState(), randomState);
	EXPECT_FALSE(skeleton->animInfo.isPetrified);
}

TEST_F(ThemeRoomGenerationTest, SignedSkeletonFallbackUsesNativeUnsignedSizeRemainder)
{
	static_assert(sizeof(size_t) == 8, "This signed corner is the frozen x64 oracle contract.");
	PrepareRoom(1);
	SetRndSeed(2588);
	Monster *skeleton = PreSpawnSkeleton();
	ASSERT_NE(skeleton, nullptr);
	for (int y = 49; y <= 51; ++y)
		for (int x = 49; x <= 51; ++x)
			tileAt(x, y).setPlayer(1);
	// Native direction iteration N,NE,E: three choices, not a power of two.
	for (int x = 49; x <= 51; ++x)
		tileAt(x, 49).setPlayer(0);
	SetRndSeed(UINT32_C(1457187811));
	ASSERT_EQ(GenerateRnd(15), -8);
	const uint32_t expectedState = GetLCGEngineState();
	ASSERT_EQ(static_cast<size_t>(-8) % 3U, 2U);
	SetRndSeed(UINT32_C(1457187811));
	ActivateSkeleton(*skeleton, { 50, 50 });
	EXPECT_EQ(GetLCGEngineState(), expectedState);
	EXPECT_EQ(skeleton->position.tile, (WorldTilePosition { 51, 49 }));
	EXPECT_EQ(skeleton->direction, Direction::West);
	EXPECT_EQ(skeleton->mode, MonsterMode::SpecialStand);
	EXPECT_EQ(tileAt(51, 49).monster(), static_cast<int>(skeleton->getId()) + 1);
	EXPECT_FALSE(skeleton->animInfo.isPetrified);
}

TEST_F(ThemeRoomGenerationTest, BothMultiplayerOnlyShrineSelectionsRetryBeforeAnimationCoin)
{
	const auto availability = SinglePlayerShrineAvailabilityForTesting();
	ASSERT_EQ(static_cast<uint8_t>(AllObjects[OBJ_SHRINER].flags) & 1U, 0U);
	for (const int rejected : { 18, 25 }) {
		ASSERT_FALSE(availability[rejected]);
		uint32_t selectedSeed = 0, expectedState = 0, expectedObjectSeed = 0;
		int expectedIndex = 0;
		bool expectedAlternateAnimation = false, found = false;
		for (uint32_t seed = 0; seed < 65536 && !found; ++seed) {
			SetRndSeed(seed);
			expectedObjectSeed = AdvanceRndSeed();
			if (GenerateRnd(26) != rejected) continue;
			do {
				expectedIndex = GenerateRnd(26);
				ASSERT_GE(expectedIndex, 0);
			} while (!availability[expectedIndex]);
			expectedAlternateAnimation = !FlipCoin();
			expectedState = GetLCGEngineState();
			selectedSeed = seed;
			found = true;
		}
		ASSERT_TRUE(found);
		PrepareRoom(1);
		SetRndSeed(selectedSeed);
		Object *shrine = AddObject(OBJ_SHRINER, { 50, 50 });
		ASSERT_NE(shrine, nullptr);
		EXPECT_EQ(shrine->_oRndSeed, expectedObjectSeed);
		EXPECT_EQ(shrine->_oVar1, expectedIndex);
		EXPECT_EQ(GetLCGEngineState(), expectedState);
		EXPECT_TRUE(shrine->_oPreFlag);
		EXPECT_EQ(shrine->_oAnimFrame, expectedAlternateAnimation ? 12U : AllObjects[OBJ_SHRINER].animDelay);
		EXPECT_EQ(shrine->_oAnimLen, expectedAlternateAnimation ? 22U : AllObjects[OBJ_SHRINER].animLen);
		RecordProperty("rejected" + std::to_string(rejected) + "Seed", std::to_string(selectedSeed));
	}
}

TEST_F(ThemeRoomGenerationTest, CaptureCompleteSelectedThemePass)
{
	const char *directory = std::getenv("GODEVILUTION_THEME_ROOM_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory) GTEST_SKIP() << "Select the separate private theme-room corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	const auto repository = std::filesystem::weakly_canonical(std::filesystem::absolute(__FILE__).parent_path().parent_path());
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	ASSERT_FALSE(std::mismatch(repository.begin(), repository.end(), root.begin(), root.end()).first == repository.end());
	std::ofstream stream(root / "native-theme-rooms.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	constexpr std::array<int, 14> types { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 13, 14, 15 };
	int cases = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-theme-rooms");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-complete-selected-cathedral-themes");
		document.Number("playerMaximumLife", 6400);
		document.Number("playerMaximumMana", 3200);
		document.Key("itemCatalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		for (int depth = 1; depth <= 4; ++depth) {
			for (int lane = 0; lane < 22; ++lane) {
				const int itemCount = lane == 17 ? MAXITEMS : 2;
				const int objectCount = lane == 15 || lane == 16 ? MAXOBJECTS : 0;
				const int monsterCount = lane == 18 || lane == 19 ? MaxMonsters : 4;
				PrepareRoom(depth, itemCount, objectCount, monsterCount, lane != 16);
				const uint32_t seed = lane == 17 ? UINT32_C(1457187811) : UINT32_C(2588) + depth * 17;
				const std::string name = lane < 14 ? "type-" + std::to_string(types[lane]) : std::array<std::string, 8> { "ordered", "object-full-library", "object-full-failed-fit", "item-full-signed-pit", "monster-full-skeleton", "monster-full-goat", "loading-shrine", "empty" }[lane - 14];
				numThemes() = lane == 21 ? 0 : lane == 14 ? 14
				                                          : 1;
				if (lane == 14)
					for (int i = 0; i < 14; ++i)
						themes[i] = { static_cast<theme_id>(types[i]), 1 };
				else if (lane != 21)
					themes[0] = { static_cast<theme_id>(lane < 14 ? types[lane] : lane == 15 || lane == 16 ? 5
						                  : lane == 17                                                     ? 2
						                  : lane == 18                                                     ? 3
						                  : lane == 19                                                     ? 11
						                                                                                   : 1),
						1 };
				LoadingMapObjects = lane == 20;
				SetRndSeed(seed);
				if (cases++ != 0) stream << ',';
				JsonObject row(stream);
				row.String("name", name);
				row.Number("depth", depth);
				row.Number("seed", seed);
				row.Boolean("loadingMapObjects", LoadingMapObjects);
				row.Key("themes");
				stream << '[';
				for (int i = 0; i < numThemes(); ++i) {
					if (i) stream << ',';
					stream << '[' << static_cast<int>(themes[i].ttype) << ',' << static_cast<int>(themes[i].ttval) << ']';
				}
				stream << ']';
				row.Key("player");
				{
					JsonObject player(stream);
					player.Number("id", 0);
					player.Number("level", depth);
					player.Boolean("active", true);
					player.Boolean("levelChanging", false);
					player.Key("tile");
					WriteRoomPoint(stream, MyPlayer->position.tile);
					player.Key("future");
					WriteRoomPoint(stream, MyPlayer->position.future);
				}
				row.Key("input");
				WriteRoomWorld(stream);
				CreateThemeRooms();
				row.Key("expected");
				WriteRoomWorld(stream);
				if (lane == 17) {
					EXPECT_EQ(ActiveItemCount, MAXITEMS);
					const Item &last = Items[ActiveItems[MAXITEMS - 1]];
					EXPECT_FALSE(last._iAnimFlag);
					EXPECT_EQ(last.selectionRegion, SelectionRegion::Bottom);
					EXPECT_EQ(last.animInfo.currentFrame, last.animInfo.numberOfFrames - 1);
				}
			}
		}
		stream << ']';
		document.Number("caseCount", cases);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(cases, 88);
	EXPECT_LE(std::filesystem::file_size(root / "native-theme-rooms.json"), 128U * 1024U * 1024U);
}

// Controlled L2 theme seams complement the separately captured generated maps.
TEST_F(ThemeRoomGenerationTest, CaptureCompleteCatacombsThemePass)
{
	CaptureCatacombsThemePass(std::getenv("GODEVILUTION_CATACOMBS_THEME_ROOM_ORACLE_DIRECTORY"), false);
}

// The same controlled branches with gbIsMultiplayer: full theme-monster life, multiplayer shrines and item pools.
TEST_F(ThemeRoomGenerationTest, CaptureCompleteCatacombsMultiplayerThemePass)
{
	CaptureCatacombsThemePass(std::getenv("GODEVILUTION_CATACOMBS_MULTIPLAYER_THEME_ROOM_ORACLE_DIRECTORY"), true);
}

void ThemeRoomGenerationTest::CaptureCatacombsThemePass(const char *directory, bool multiplayer)
{
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select the separate private Catacombs theme-room corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	const char *fileName = multiplayer ? "native-catacombs-multiplayer-theme-rooms.json" : "native-catacombs-theme-rooms.json";
	gbIsMultiplayer = multiplayer;
	// Native UpdateEnemy skips a dead multiplayer player; a real level entry is alive.
	if (multiplayer) MyPlayer->life.current = MyPlayer->life.base = MyPlayer->life.maximum = MyPlayer->life.maximumBase;
	std::ofstream stream(root / fileName, std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	constexpr std::array<int, 16> types { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16 };
	constexpr std::array<const char *, 6> edges {
		"ordered-racks", "object-full-armor", "object-full-weapon", "item-full-gold", "item-full-minimum-gold", "item-full-nongold"
	};
	int cases = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-theme-rooms");
		document.Number("version", 1);
		document.String("policy", multiplayer ? "controlled-original-diablo-multiplayer-normal-catacombs-theme-branches"
		                                      : "controlled-original-diablo-single-player-normal-catacombs-theme-branches");
		document.Number("playerMaximumLife", 6400);
		document.Number("playerMaximumMana", 3200);
		document.Key("itemCatalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		for (const int depth : { 5, 8 }) {
			for (int lane = 0; lane < 22; ++lane) {
				const bool fullItems = lane >= 19;
				PrepareRoom(depth, fullItems ? MAXITEMS : 2, lane == 17 || lane == 18 ? MAXOBJECTS : 0);
				currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATACOMBS });
				CurrentLightManager.MakeLightTable();
				const uint32_t seed = UINT32_C(2588) + depth * 17;
				const std::string name = lane < 16 ? "type-" + std::to_string(types[lane]) : edges[lane - 16];
				numThemes() = lane == 16 ? 4 : 1;
				if (lane == 16) {
					for (int i = 0; i < 4; ++i)
						themes[i] = { static_cast<theme_id>(i % 2 == 0 ? 10 : 16), 1 };
				} else {
					themes[0] = { static_cast<theme_id>(lane < 16 ? types[lane] : lane == 17 ? 10
						                  : lane == 18                                       ? 16
						                                                                     : 4),
						1 };
				}
				if (fullItems) {
					Item &last = Items[ActiveItems[MAXITEMS - 1]];
					last._itype = lane == 21 ? ItemType::Misc : ItemType::Gold;
					last.IDidx = lane == 21 ? IDI_HEAL : IDI_GOLD;
					last._ivalue = lane == 20 ? 1 : 99;
				}
				SetRndSeed(seed);
				if (cases++ != 0) stream << ',';
				JsonObject row(stream);
				row.String("name", name);
				row.Number("depth", depth);
				row.Number("seed", seed);
				row.Key("themes");
				stream << '[';
				for (int i = 0; i < numThemes(); ++i) {
					if (i != 0) stream << ',';
					stream << '[' << static_cast<int>(themes[i].ttype) << ',' << static_cast<int>(themes[i].ttval) << ']';
				}
				stream << ']';
				row.Key("player");
				{
					JsonObject player(stream);
					player.Number("id", 0);
					player.Number("level", depth);
					player.Boolean("active", true);
					player.Boolean("levelChanging", false);
					player.Key("tile");
					WriteRoomPoint(stream, MyPlayer->position.tile);
					player.Key("future");
					WriteRoomPoint(stream, MyPlayer->position.future);
				}
				row.Key("input");
				WriteRoomWorld(stream);
				CreateThemeRooms();
				row.Key("expected");
				WriteRoomWorld(stream);
				if (fullItems) {
					const Item &last = Items[ActiveItems[MAXITEMS - 1]];
					EXPECT_EQ(last._ivalue, lane == 21 ? 99 : 1);
					EXPECT_EQ(last._iSeed, 1000U);
					EXPECT_EQ(last._iPLStr, 17);
					EXPECT_EQ(last._iCharges, 7);
					EXPECT_EQ(last._iMaxCharges, 9);
					EXPECT_EQ(last.dwBuff, 128U);
				}
			}
		}
		stream << ']';
		document.Number("caseCount", cases);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(cases, 44);
	EXPECT_LE(std::filesystem::file_size(root / fileName), 128U * 1024U * 1024U);
}

// Extra public state borrowed only by the real-geometry opt-in. The underlying
// guards already retain tables, pools, tiles, RNG, player and lighting state.
