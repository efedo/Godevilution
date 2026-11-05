TEST_F(ThemeRoomGenerationTest, CaptureMonsterRuntimeMetadata)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory) GTEST_SKIP() << "Select a separate private runtime metadata directory.";
	const auto root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native evidence.";
	ASSERT_EQ(MonstersData.size(), 138U);
	ASSERT_EQ(UniqueMonstersData.size(), 100U);
	std::ofstream table(root / "cathedral_population_monster_runtime.tsv", std::ios::binary);
	ASSERT_TRUE(table.good());
	table << "native_monster_id\tstable_symbol\tname\tlevel\tto_hit\tanim_frame_num\tto_hit_special\tanim_frame_num_special\tmonster_class\tselection_region\ttreasure\texperience\tresistance_hell\r\n";
	for (size_t i = 0; i < MonstersData.size(); ++i) {
		const auto &data = MonstersData[i];
		ASSERT_EQ(data.name.find_first_of("\t\r\n"), std::string::npos);
		table << i << "\tNativeMonster_" << std::setfill('0') << std::setw(3) << i << '\t' << data.name
		      << '\t' << static_cast<int>(data.level) << '\t' << static_cast<int>(data.toHit)
		      << '\t' << static_cast<int>(data.animFrameNum) << '\t' << static_cast<int>(data.toHitSpecial)
		      << '\t' << static_cast<int>(data.animFrameNumSpecial) << '\t' << static_cast<int>(data.monsterClass)
		      << '\t' << static_cast<int>(data.selectionRegion) << '\t' << data.treasure
		      << '\t' << data.exp << '\t' << static_cast<int>(data.resistanceHell) << "\r\n";
	}
	table.flush();
	ASSERT_TRUE(table.good());
	table.close();
	std::ofstream stream(root / "native-monster-runtime.json", std::ios::binary);
	ASSERT_TRUE(stream.good());
	int count = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-monster-runtime");
		document.Number("version", 1);
		document.Key("cases");
		stream << '[';
		auto capture = [&](int nativeId, int uniqueId, _difficulty difficulty, bool golem, bool berserk) {
			LevelBestiary.types[0].type = static_cast<_monster_id>(nativeId);
			Monster monster {};
			monster.levelType = 0;
			monster.uniqueType = uniqueId < 0 ? UniqueMonsterType::None : static_cast<UniqueMonsterType>(uniqueId);
			monster.flags = (golem ? MFLAG_GOLEM : 0) | (berserk ? MFLAG_BERSERK : 0);
			monster.golemToHit = UINT16_MAX;
			if (count++ != 0) stream << ',';
			JsonObject row(stream);
			row.Number("nativeId", nativeId);
			row.Number("uniqueId", uniqueId);
			row.Number("difficulty", difficulty);
			row.Boolean("playerMinion", monster.isPlayerMinion());
			row.Number("golemToHit", monster.golemToHit);
			row.Number("uniqueDungeonLevel", uniqueId < 0 ? 0 : UniqueMonstersData[uniqueId].mlevel);
			row.Number("uniqueCustomToHit", uniqueId < 0 ? 0 : UniqueMonstersData[uniqueId].customToHit);
			row.Number("level", monster.level(difficulty));
			row.Number("experience", monster.exp(difficulty));
			row.Number("toHit", monster.toHit(difficulty));
			row.Number("toHitSpecial", monster.toHitSpecial(difficulty));
		};
		for (int i = 0; i < 138; ++i)
			for (auto difficulty : { DIFF_NORMAL, DIFF_NIGHTMARE, DIFF_HELL })
				capture(i, -1, difficulty, false, false);
		for (int i = 0; i < 100; ++i)
			for (auto difficulty : { DIFF_NORMAL, DIFF_NIGHTMARE, DIFF_HELL })
				capture(UniqueMonstersData[i].mtype, i, difficulty, false, false);
		for (auto difficulty : { DIFF_NORMAL, DIFF_NIGHTMARE, DIFF_HELL }) {
			capture(MT_GOLEM, -1, difficulty, true, false);
			capture(MT_GOLEM, -1, difficulty, true, true);
		}
		stream << ']';
		document.Number("caseCount", count);
		document.Key("uniqueBaseTypes");
		WriteArray(stream, UniqueMonstersData, [&](const auto &unique) { stream << static_cast<int>(unique.mtype); });
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(count, 720);
}

/** A CLX list of one-pixel sprites, enough for code that reads monster sprites without drawing them. */
std::unique_ptr<uint8_t[]> MakePlaceholderClxList(uint32_t spriteCount)
{
	constexpr uint32_t SpriteHeaderSize = 6;
	constexpr uint32_t SpriteSize = SpriteHeaderSize + 1;
	const uint32_t tableSize = 4 + 4 * (spriteCount + 1);
	const uint32_t dataSize = tableSize + SpriteSize * spriteCount;
	auto data = std::make_unique<uint8_t[]>(dataSize);
	std::fill_n(data.get(), dataSize, 0);
	WriteLE32(data.get(), spriteCount);
	for (uint32_t index = 0; index <= spriteCount; ++index)
		WriteLE32(&data[4 + index * 4], tableSize + index * SpriteSize);
	for (uint32_t index = 0; index < spriteCount; ++index) {
		const uint32_t offset = tableSize + index * SpriteSize;
		WriteLE16(&data[offset], SpriteHeaderSize);
		WriteLE16(&data[offset + 2], 1);
		WriteLE16(&data[offset + 4], 1);
		data[offset + SpriteHeaderSize] = 1;
	}
	return data;
}

/** One prepared L5-L8 actor of a native type, free-running against a passive Warrior. */
struct CatacombsAiLane {
	const char *name;
	int depth;
	_monster_id type;
	int x = 50;
};

/**
 * Free-running native AI for prepared L5-L8 actors. One actor per lane approaches a
 * stationary, durable level-1 Warrior; every tick records the actor, the Warrior's life
 * and the shared RNG after ProcessMonsters and ProcessMissiles, and with recordMissiles
 * every live missile. Every tile is visible, so the active window never lapses.
 */
template <size_t LaneCount, typename PrepareActor>
void CaptureNativeCatacombsAiLanes(PrepareActor prepareActor, const std::filesystem::path &root, const char *fileName,
    const char *format, const char *policy, const std::array<CatacombsAiLane, LaneCount> &lanes, bool recordMissiles)
{
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode, missilePreFlag = MissilePreFlag;
		uint32_t random = GetLCGEngineState();
		std::list<Missile> savedMissiles = std::move(Missiles);
		ScopedMissileDataForTest missileData;
		~ExecutionGuard()
		{
			Missiles = std::move(savedMissiles);
			MissilePreFlag = missilePreFlag;
			gbSndInited = sound;
			HeadlessMode = headless;
			SetRndSeed(random);
		}
	} guard;
	HeadlessMode = true;
	gbSndInited = false;
	LoadPlayerDataFiles();
	LoadMissileData();

	constexpr int TickCount = 300;
	constexpr int DurableLife = 2000 << 6;
	const std::unique_ptr<uint8_t[]> placeholderSprites = MakePlaceholderClxList(32);
	std::ofstream stream(root / fileName, std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	{
		JsonObject document(stream);
		document.String("format", format);
		document.Number("version", 1);
		document.String("policy", policy);
		document.Number("caseCount", lanes.size());
		document.Number("tickCount", TickCount);
		document.Key("cases");
		stream << '[';
		bool firstCase = true;
		for (const CatacombsAiLane &lane : lanes) {
			Monster *actor = prepareActor(lane);
			ASSERT_NE(actor, nullptr) << lane.name;
			for (AnimStruct &anim : const_cast<CMonster &>(actor->type()).anims) {
				if (!anim.sprites) anim.sprites = ClxSpriteListOrSheet { placeholderSprites.get(), 0 };
			}
			actor->packSize = 0; // AddMonster keeps the reused slot's pack size; this actor leads no pack.
			actor->var1 = actor->var2 = actor->var3 = 0; // ClrAllMonsters/ClearMVars state of a freshly loaded level.
			actor->position.temp = { 0, 0 };
			// An open, fully visible arena keeps the actor awake wherever it moves.
			for (Tile &tile : tiles()) tile.addFlags(DungeonFlag::Visible);
			Missiles.clear();
			MissilePreFlag = false;
			MyPlayer->life = { DurableLife, DurableLife, DurableLife, DurableLife, 100 };
			MyPlayer->pLvlLoad = 0; // Live play: monster sound effects draw their variant.
			SetRndSeed(2588);
			if (!firstCase) stream << ',';
			firstCase = false;
			JsonObject row(stream);
			row.String("name", lane.name);
			row.Number("depth", lane.depth);
			row.Number("nativeType", lane.type);
			const MonsterData &data = actor->data();
			row.Key("monsterData");
			{
				JsonObject json(stream);
				json.String("name", data.name);
				json.Number("level", data.level);
				json.Number("ai", data.ai);
				json.Number("intelligence", data.intelligence);
				json.Number("abilityFlags", data.abilityFlags);
				json.Number("toHit", data.toHit);
				json.Number("animFrameNum", data.animFrameNum);
				json.Number("toHitSpecial", data.toHitSpecial);
				json.Number("animFrameNumSpecial", data.animFrameNumSpecial);
				json.Number("minDamage", data.minDamage);
				json.Number("maxDamage", data.maxDamage);
				json.Number("minDamageSpecial", data.minDamageSpecial);
				json.Number("maxDamageSpecial", data.maxDamageSpecial);
				json.Number("armorClass", data.armorClass);
				json.Number("monsterClass", data.monsterClass);
				json.Number("resistance", data.resistance);
				json.Number("resistanceHell", data.resistanceHell);
				json.Number("selectionRegion", data.selectionRegion);
				json.Number("treasure", data.treasure);
				json.Number("exp", data.exp);
				json.Number("width", data.width);
				json.Boolean("hasSpecial", data.hasSpecial);
				json.Key("frames");
				stream << '[';
				for (size_t index = 0; index < 6; ++index) stream << (index != 0 ? "," : "") << static_cast<int>(data.frames[index]);
				stream << ']';
				json.Key("rates");
				stream << '[';
				for (size_t index = 0; index < 6; ++index) stream << (index != 0 ? "," : "") << static_cast<int>(data.rate[index]);
				stream << ']';
			}
			row.Number("toHit", actor->toHit(sgGameInitInfo.nDifficulty));
			row.Number("toHitSpecial", actor->toHitSpecial(sgGameInitInfo.nDifficulty));
			row.Number("level", actor->level(sgGameInitInfo.nDifficulty));
			row.Key("initialActor");
			WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
			row.Key("initialPlayer");
			WriteRangedPlayer(stream, *MyPlayer);
			row.Number("initialPlayerLife", MyPlayer->life.current);
			row.Number("playerArmor", MyPlayer->GetArmor());
			row.Number("playerBlockChance", MyPlayer->GetBlockChance());
			row.Number("playerGetHit", MyPlayer->_pIGetHit);
			row.Number("initialRandom", GetLCGEngineState());
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < TickCount; ++tick) {
				ProcessMonsters();
				ProcessMissiles();
				if (tick != 0) stream << ',';
				JsonObject state(stream);
				state.Number("random", GetLCGEngineState());
				state.Number("playerLife", MyPlayer->life.current);
				state.Number("playerLifeMaximum", MyPlayer->life.maximum);
				state.Number("playerMode", MyPlayer->_pmode);
				state.Number("missileCount", Missiles.size());
				if (recordMissiles) {
					state.Key("missiles");
					stream << '[';
					bool firstMissile = true;
					for (const Missile &missile : Missiles) {
						if (!firstMissile) stream << ',';
						firstMissile = false;
						WriteRangedMissile(stream, missile);
					}
					stream << ']';
				}
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
			}
			stream << ']';
		}
		stream << ']';
	}
	ASSERT_TRUE(stream.good());
}

/**
 * Batch 2.4a: the L5-L8 melee families. The Bat swoop and Horned charge move the actor
 * through a missile; the Gargoyle starts beside the Warrior so it wakes from its perch.
 */
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsMeleeAi)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_MELEE_AI_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the explicit Catacombs melee AI oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native AI evidence.";
	const std::array<CatacombsAiLane, 10> lanes { {
		{ "zombie", 5, MT_YZOMBIE },
		{ "fallen", 5, MT_BFALLSP },
		{ "skeleton", 5, MT_XSKELAX },
		{ "scavenger", 6, MT_YSCAV },
		{ "sneak", 6, MT_STALKER },
		{ "goat-melee", 6, MT_BGOATMC },
		{ "fat", 6, MT_FAT },
		{ "bat", 5, MT_GLOOM },
		{ "gargoyle", 6, MT_WINGED, 59 },
		{ "rhino", 7, MT_HORNED },
	} };
	CaptureNativeCatacombsAiLanes(
	    [this](const CatacombsAiLane &lane) { return PrepareCatacombsActor(lane.depth, lane.type, nullptr, lane.x); },
	    root, "native-catacombs-melee-ai.json", "godevilution-catacombs-melee-ai",
	    "prepared-original-diablo-catacombs-melee-ai-free-run", lanes, false);
}

/**
 * Batch 2.4b: the L5-L8 ranged families (AiRanged, AiRangedAvoidance, SkeletonBowAi and
 * the Familiar's BatAi lightning), recording each tick's missiles as well.
 */
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsRangedAi)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_RANGED_AI_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the explicit Catacombs ranged AI oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native AI evidence.";
	const std::array<CatacombsAiLane, 7> lanes { {
		{ "skeleton-archer", 5, MT_XSKELBW },
		{ "goat-archer", 6, MT_BGOATBW },
		{ "acid", 7, MT_NACID },
		{ "poison-spitter", 8, MT_RACID },
		{ "magma", 8, MT_NMAGMA },
		{ "blood-stone", 8, MT_YMAGMA },
		{ "familiar", 7, MT_FAMILIAR },
	} };
	CaptureNativeCatacombsAiLanes(
	    [this](const CatacombsAiLane &lane) { return PrepareCatacombsActor(lane.depth, lane.type, nullptr, lane.x); },
	    root, "native-catacombs-ranged-ai.json", "godevilution-catacombs-ranged-ai",
	    "prepared-original-diablo-catacombs-ranged-ai-free-run", lanes, true);
}

