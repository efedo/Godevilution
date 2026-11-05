TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsDoorOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_DOOR_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select a private Catacombs door corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	std::ofstream stream(root / "native-catacombs-door-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int count = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-door-operations");
		document.Number("version", 1);
		document.String("policy", "controlled-original-diablo-catacombs-l2-door-sol-and-occupancy");
		document.Key("cases");
		stream << '[';
		for (int depth = 5; depth <= 8; ++depth) {
			for (const auto type : { OBJ_L2LDOOR, OBJ_L2RDOOR }) {
				for (int occupancy = 0; occupancy < 4; ++occupancy) {
					PrepareRoom(depth, 0, 0, 0, false);
					currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATACOMBS });
					for (Tile &tile : tiles()) {
						tile.setPlayer(0);
						tile.setMonster(0);
						tile.setItem(0);
						tile.setCorpse(0);
					}
					for (int y = 48; y <= 52; ++y) {
						for (int x = 48; x <= 52; ++x) {
							tileAt(x, y).setPiece(100 + (y - 48) * 5 + x - 48);
							tileAt(x, y).setSpecial(11);
							tileAt(x, y).setTransVal(23);
						}
					}
					tileProperties()[537] = tileProperties()[539] = TileProperties::Solid | TileProperties::BlockLight | TileProperties::BlockMissile;
					tileProperties()[12] = tileProperties()[16] = TileProperties::None;
					SetRndSeed(2588);
					Object *door = AddObject(type, { 50, 50 });
					ASSERT_NE(door, nullptr);
					Player remotePlayer;
					if (count++ != 0) stream << ',';
					JsonObject row(stream);
					row.Number("depth", depth);
					row.Number("nativeType", type);
					row.Number("occupancy", occupancy);
					row.Key("initialObject");
					WriteRoomObject(stream, door->GetId(), *door);
					row.Key("states");
					stream << '[';
					bool first = true;
					auto capture = [&](std::string_view operation) {
						if (!first) stream << ',';
						first = false;
						JsonObject state(stream);
						state.String("operation", operation);
						state.Number("random", GetLCGEngineState());
						state.Key("object");
						WriteRoomObject(stream, door->GetId(), *door);
						state.Key("cells");
						stream << '[';
						bool firstCell = true;
						for (int y = 48; y <= 52; ++y) {
							for (int x = 48; x <= 52; ++x) {
								if (!firstCell) stream << ',';
								firstCell = false;
								const Tile &tile = tileAt(x, y);
								stream << '[' << tile.piece() << ',' << static_cast<int>(tile.special()) << ',' << static_cast<int>(tile.transVal())
								       << ',' << static_cast<int>(tileProperties()[tile.piece()]) << ']';
							}
						}
						stream << ']';
					};
					capture("initial");
					SyncOpObject(remotePlayer, 0, *door);
					capture("open");
					auto &center = tileAt(50, 50);
					if (occupancy == 0) center.setCorpse(1);
					if (occupancy == 1) center.setPlayer(-1);
					if (occupancy == 2) center.setMonster(-1);
					if (occupancy == 3) center.setItem(1);
					SyncOpObject(remotePlayer, 0, *door);
					capture("blocked-close");
					ProcessObjects();
					capture("occupied-update");
					SyncObjectAnim(*door);
					capture("occupied-sync");
					ProcessObjects();
					capture("occupied-update-after-sync");
					center.setCorpse(0);
					center.setPlayer(0);
					center.setMonster(0);
					center.setItem(0);
					ProcessObjects();
					capture("clear-update");
					SyncOpObject(remotePlayer, 0, *door);
					capture("close");
					DeltaSyncOpObject(*door);
					capture("delta-open");
					DeltaSyncOpObject(*door);
					capture("repeat-delta-open");
					SyncObjectAnim(*door);
					capture("open-sync");
					SyncOpObject(remotePlayer, 0, *door);
					capture("close-after-delta");
					SyncObjectAnim(*door);
					capture("closed-sync");
					stream << ']';
				}
			}
		}
		stream << ']';
		document.Number("caseCount", count);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(count, 32);
}

// The follower observes its leader at its own active-list turn, before AI.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsGargoylePackOrder)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_PACK_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select a private Gargoyle pack reference directory.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	std::ofstream stream(root / "native-catacombs-gargoyle-pack-order.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-gargoyle-pack-order");
		document.Number("version", 1);
		document.String("policy", "prepared-inactive-gargoyle-pack-special-completion-order");
		document.Key("cases");
		stream << '[';
		for (int followerFirst = 0; followerFirst < 2; ++followerFirst) {
			PrepareRoom(5, 0, 0, 0, false);
			currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
			for (Tile &tile : tiles()) {
				tile.setPiece(0);
				tile.setPlayer(0);
				tile.setMonster(0);
				tile.setFlags(DungeonFlag::None);
			}
			MyPlayer->position.tile = { 90, 90 };
			MyPlayer->position.future = { 90, 90 };
			MyPlayer->position.old = { 90, 90 };
			MyPlayer->life.current = 6400;
			tileAt(90, 90).setPlayer(1);
			LevelBestiary.init();
			const auto type = LevelBestiary.addType(MT_GARGOYLE, PLACE_SCATTER);
			ASSERT_TRUE(type.has_value());
			SetRndSeed(2588);
			Monster *leader = AddMonster({ 10, 10 }, Direction::SouthEast, *type, true);
			ASSERT_NE(leader, nullptr);
			Monster *follower = AddMonster({ 11, 10 }, Direction::SouthEast, *type, true);
			ASSERT_NE(follower, nullptr);
			leader->packSize = 1;
			follower->setLeader(leader);
			follower->leaderRelation = LeaderRelation::Leashed;
			for (Monster *actor : { leader, follower }) {
				actor->activeForTicks = 0;
				actor->enemy = 0;
				actor->enemyPosition = { 90, 90 };
				actor->position.last = { 90, 90 };
			}
			leader->flags &= ~MFLAG_ALLOW_SPECIAL;
			leader->animInfo.currentFrame = leader->animInfo.numberOfFrames - 1;
			leader->animInfo.tickCounterOfCurrentFrame = 0;
			if (followerFirst != 0) std::swap(ActiveMonsters[0], ActiveMonsters[1]);
			SetRndSeed(2588);
			if (followerFirst != 0) stream << ',';
			JsonObject row(stream);
			row.Boolean("followerFirst", followerFirst != 0);
			row.Key("initialLeader");
			WriteRoomMonster(stream, leader->getId(), *leader);
			row.Key("initialFollower");
			WriteRoomMonster(stream, follower->getId(), *follower);
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < 3; ++tick) {
				ProcessMonsters();
				if (tick != 0) stream << ',';
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Number("random", GetLCGEngineState());
				state.Key("leader");
				WriteRoomMonster(stream, leader->getId(), *leader);
				state.Key("follower");
				WriteRoomMonster(stream, follower->getId(), *follower);
			}
			stream << ']';
		}
		stream << ']';
		document.Number("caseCount", 2);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
}

// Separate source metadata and actual Monster methods; older corpus bytes stay frozen.
// Actual controller/mode dispatch over a prepared, MPQ-free actor arena.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsActorPhases)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_ACTOR_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select a private Catacombs actor corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	LoadPlayerDataFiles();
	std::ofstream stream(root / "native-catacombs-actor-phases.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-actor-phases");
		document.Number("version", 2);
		document.String("policy", "prepared-original-diablo-single-player-normal-catacombs-actor-arena");
		document.Key("cases");
		stream << '[';
		for (const HeroClass heroClass : { HeroClass::Warrior, HeroClass::Sorcerer }) {
			for (const int lane : { 0, 5, 6, 7, 2, 3, 4 }) {
				PrepareRoom(5, 0, 0, 0, false);
				currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
				for (Tile &tile : tiles()) {
					tile.setPlayer(0);
					tile.setMonster(0);
					tile.setFlags(DungeonFlag::None);
				}
				LevelBestiary.init();
				const bool fat = lane == 0 || lane >= 5;
				const int nativeType = fat ? (lane == 0 ? 52 : lane + 48) : 81;
				const auto typeIndex = LevelBestiary.addType(static_cast<_monster_id>(nativeType), PLACE_SCATTER);
				ASSERT_TRUE(typeIndex.has_value());
				// Character creation requires inventory cursor art. This controller seam
				// prepares only class combat scalars from the canonical native tables.
				Player hero;
				hero._pClass = heroClass;
				hero.setCharacterLevel(1);
				const ClassAttributes &attributes = hero.getClassAttributes();
				hero.attributes.strength.base = hero.attributes.strength.current = attributes.baseStr;
				hero.attributes.magic.base = hero.attributes.magic.current = attributes.baseMag;
				hero.attributes.dexterity.base = hero.attributes.dexterity.current = attributes.baseDex;
				hero.attributes.vitality.base = hero.attributes.vitality.current = attributes.baseVit;
				const int life = hero.calculateBaseLife();
				const int mana = hero.calculateBaseMana();
				hero.life = { life, life, life, life, 100 };
				hero.mana = { mana, mana, mana, mana, 100 };
				hero.setAnimations();
				*MyPlayer = std::move(hero);
				MyPlayer->plractive = true;
				MyPlayer->plrlevel = 5;
				MyPlayer->_pLvlChanging = false;
				MyPlayer->plrIsOnSetLevel = false;
				MyPlayer->_pInvincible = false;
				MyPlayer->_pBlockFlag = false;
				MyPlayer->_pmode = PM_STAND;
				const Point playerPosition { static_cast<WorldTileCoord>(fat ? 51 : 60), 50 };
				MyPlayer->position.tile = playerPosition;
				MyPlayer->position.future = playerPosition;
				MyPlayer->position.old = playerPosition;
				tileAt(playerPosition).setPlayer(1);
				SetRndSeed(2588);
				Monster *actor = AddMonster({ 50, 50 }, Direction::SouthEast, *typeIndex, true);
				ASSERT_NE(actor, nullptr);
				actor->activeForTicks = 255;
				actor->enemy = 0;
				actor->enemyPosition = playerPosition;
				actor->position.last = playerPosition;
				uint32_t inputSeed = 2588;
				if (fat) {
					for (inputSeed = 0; inputSeed < 10000; ++inputSeed) {
						SetRndSeed(inputSeed);
						const int roll = GenerateRnd(100);
						if (roll >= 4 * actor->intelligence + 15 && roll < 4 * actor->intelligence + 20) break;
					}
					ASSERT_LT(inputSeed, 10000U);
				}
				SetRndSeed(inputSeed);
				if (caseCount++ != 0) stream << ',';
				JsonObject row(stream);
				row.Number("classId", heroClass);
				row.Number("nativeType", nativeType);
				row.Number("lane", lane);
				row.Number("inputSeed", inputSeed);
				row.Number("characterLevel", MyPlayer->getCharacterLevel());
				row.Number("initialPlayerLifeQ6", MyPlayer->life.current);
				row.Boolean("canBlock", false);
				row.Number("normalToHit", actor->toHit(DIFF_NORMAL));
				row.Number("specialToHit", actor->toHitSpecial(DIFF_NORMAL));
				row.Number("minimumDamage", actor->minDamage);
				row.Number("maximumDamage", actor->maxDamage);
				row.Number("minimumSpecialDamage", actor->minDamageSpecial);
				row.Number("maximumSpecialDamage", actor->maxDamageSpecial);
				row.Number("normalEffectFrame", actor->data().animFrameNum);
				row.Number("specialEffectFrame", actor->data().animFrameNumSpecial);
				row.Number("intelligence", actor->intelligence);
				row.Number("monsterLevel", actor->level(DIFF_NORMAL));
				row.Key("animations");
				WriteArray(stream, actor->type().anims, [&](const AnimStruct &anim) {
					stream << '[' << static_cast<int>(anim.frames) << ',' << static_cast<int>(anim.rate) << ']';
				});
				row.Key("ticks");
				stream << '[';
				for (int tick = 0; tick < 45; ++tick) {
					if (tick != 0) stream << ',';
					JsonObject state(stream);
					state.Number("tick", tick);
					state.Number("beforeMode", actor->mode);
					state.Number("beforeFlags", actor->flags);
					state.Number("beforeFrame", actor->animInfo.currentFrame);
					state.Number("beforeCounter", actor->animInfo.tickCounterOfCurrentFrame);
					state.Number("beforeRandom", GetLCGEngineState());
					state.Number("beforePlayerLifeQ6", MyPlayer->life.current);
					bool consumedWake = false;
					if (tick == 3 && lane == 3) {
						MyPlayer->position.future = { 51, 50 };
					} else if (tick == 3 && lane == 4) {
						consumedWake = actor->tryLiftGargoyle();
					}
					ProcessMonsters();
					// Freeze further AI after the admitted sequence; keep native mode/animation dispatch.
					if (fat || (actor->flags & MFLAG_ALLOW_SPECIAL) == 0)
						actor->activeForTicks = 0;
					state.Boolean("consumedWake", consumedWake);
					state.Number("mode", actor->mode);
					state.Number("flags", actor->flags);
					state.Number("frame", actor->animInfo.currentFrame);
					state.Number("counter", actor->animInfo.tickCounterOfCurrentFrame);
					state.Number("frameCount", actor->animInfo.numberOfFrames);
					state.Number("ticksPerFrame", actor->animInfo.ticksPerFrame);
					state.Number("random", GetLCGEngineState());
					state.Number("playerLifeQ6", MyPlayer->life.current);
				}
				stream << ']';
			}
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 14);
}

void WriteRangedPlayer(std::ostream &out, const Player &player)
{
	JsonObject json(out);
	json.Number("level", player.plrlevel);
	json.Number("characterLevel", player.getCharacterLevel());
	json.Number("mode", player._pmode);
	json.Boolean("active", player.plractive);
	json.Boolean("levelLoading", player.pLvlLoad != 0);
	json.Key("tile");
	WriteRoomPoint(out, { player.position.tile.x, player.position.tile.y });
	json.Key("future");
	WriteRoomPoint(out, { player.position.future.x, player.position.future.y });
	json.Key("old");
	WriteRoomPoint(out, { player.position.old.x, player.position.old.y });
}

void WriteRangedMissile(std::ostream &out, const Missile &missile)
{
	JsonObject json(out);
	json.Number("type", missile._mitype);
	json.Number("source", missile._misource);
	json.Number("caster", missile._micaster);
	json.Number("damage", missile._midam);
	json.Number("spellLevel", missile._mispllvl);
	json.Number("graphic", missile._miAnimType);
	json.Number("graphicDirection", missile.getFrameGroupRaw());
	json.Number("animationFrame", missile._miAnimFrame);
	json.Number("animationLength", missile._miAnimLen);
	json.Number("animationDelay", missile._miAnimDelay);
	json.Number("duration", missile.duration);
	json.Boolean("delete", missile._miDelFlag);
	json.Key("start");
	WriteRoomPoint(out, { missile.position.start.x, missile.position.start.y });
	json.Key("tile");
	WriteRoomPoint(out, { missile.position.tile.x, missile.position.tile.y });
	json.Key("velocity");
	WriteRoomPoint(out, { missile.position.velocity.deltaX, missile.position.velocity.deltaY });
	json.Key("traveled");
	WriteRoomPoint(out, { missile.position.traveled.deltaX, missile.position.traveled.deltaY });
	json.Key("rawProjectileUnionDestination");
	WriteRoomPoint(out, { missile.projectile.destX, missile.projectile.destY });
	json.Key("raw");
	WriteArray(out, missile.raw, [&](int value) { out << value; });
}

/** One headless Catacombs actor facing a level-1 Warrior on an open floor (shared by the ranged and AI captures). */
Monster *ThemeRoomGenerationTest::PrepareCatacombsActor(int depth, _monster_id nativeType, const UniqueMonsterData *unique, int actorX)
{
	PrepareRoom(depth, 0, 0, 0, false);
	currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATACOMBS });
	std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
	for (Tile &tile : tiles()) {
		tile.setPiece(0);
		tile.setTransVal(0);
		tile.setFlags(DungeonFlag::None);
		tile.setPlayer(0);
		tile.setMonster(0);
		tile.setObject(0);
		tile.setItem(0);
		tile.setCorpse(0);
		tile.setLight(0);
		tile.setPreLight(0);
	}
	LevelBestiary.init();
	const auto typeIndex = LevelBestiary.addType(nativeType, PLACE_SCATTER);
	if (!typeIndex) return nullptr;
	Player hero {}; // Value-initialized: item effects such as Reflect and GetHit start at zero.
	hero._pClass = HeroClass::Warrior;
	hero.setCharacterLevel(1);
	const ClassAttributes &attributes = hero.getClassAttributes();
	hero.attributes.strength.base = hero.attributes.strength.current = attributes.baseStr;
	hero.attributes.magic.base = hero.attributes.magic.current = attributes.baseMag;
	hero.attributes.dexterity.base = hero.attributes.dexterity.current = attributes.baseDex;
	hero.attributes.vitality.base = hero.attributes.vitality.current = attributes.baseVit;
	const int life = hero.calculateBaseLife();
	const int mana = hero.calculateBaseMana();
	hero.life = { life, life, life, life, 100 };
	hero.mana = { mana, mana, mana, mana, 100 };
	hero.setAnimations();
	*MyPlayer = std::move(hero);
	MyPlayer->plractive = true;
	MyPlayer->plrlevel = static_cast<uint8_t>(depth);
	MyPlayer->plrIsOnSetLevel = false;
	MyPlayer->_pLvlChanging = false;
	MyPlayer->pLvlLoad = 1;
	MyPlayer->_pInvincible = false;
	MyPlayer->_pBlockFlag = false;
	MyPlayer->_pmode = PM_STAND;
	const Point playerPosition { 60, 50 };
	MyPlayer->position.tile = playerPosition;
	MyPlayer->position.future = playerPosition;
	MyPlayer->position.old = playerPosition;
	tileAt(playerPosition).setPlayer(1);
	CurrentLightManager.Init();
	SetRndSeed(2588);
	Monster *actor = AddMonster({ actorX, 50 }, Direction::SouthEast, *typeIndex, true);
	if (actor == nullptr) return nullptr;
	actor->activeForTicks = UINT8_MAX;
	actor->enemy = 0;
	actor->enemyPosition = playerPosition;
	actor->position.last = playerPosition;
	actor->position.future = actor->position.tile;
	actor->goal = MonsterGoal::Normal;
	actor->goalVar1 = 0;
	actor->goalVar2 = 0;
	actor->goalVar3 = 0;
	actor->flags &= ~(MFLAG_NO_ENEMY | MFLAG_TARGETS_MONSTER | MFLAG_SEARCH | MFLAG_HIDDEN);
	tileAt(actor->position.tile).addFlags(DungeonFlag::Visible);
	if (unique != nullptr) {
		const size_t uniqueIndex = static_cast<size_t>(unique - UniqueMonstersData.data());
		actor->uniqueType = static_cast<UniqueMonsterType>(uniqueIndex);
		actor->ai = unique->mAi;
		actor->intelligence = unique->mint;
		actor->minDamage = actor->minDamageSpecial = unique->mMinDamage;
		actor->maxDamage = actor->maxDamageSpecial = unique->mMaxDamage;
		actor->maxHitPoints = std::max<int>(unique->mmaxhp / 2, 1) << 6;
		actor->hitPoints = actor->maxHitPoints;
		actor->uniqTrans = 0;
	}
	return actor;
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsRangedReleases)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_RANGED_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the explicit Catacombs ranged oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native ranged evidence.";
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
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Arrow));
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::MagmaBall));
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Acid));

	std::ofstream stream(root / "native-catacombs-ranged-release.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-ranged-release");
		document.Number("version", 1);
		document.String("policy", "prepared-original-diablo-catacombs-ranged-controller-release-clock");
		document.Key("cases");
		stream << '[';
		auto nextCase = [&]() { if (caseCount++ != 0) stream << ','; };
		auto writeIdentity = [&](JsonObject &row, std::string_view name, int depth, _monster_id nativeType, MonsterAIID ai, MissileID missileId, bool special, std::string_view uniqueName, Monster &actor) {
			row.String("name", name);
			row.Number("depth", depth);
			row.Number("nativeType", nativeType);
			row.Number("ai", ai);
			row.Number("missileId", missileId);
			row.Boolean("special", special);
			row.String("uniqueName", uniqueName);
			row.Number("normalEffectFrame", actor.data().animFrameNum);
			row.Number("specialEffectFrame", actor.data().animFrameNumSpecial);
			row.Number("sourceAnimationFrames", actor.animInfo.numberOfFrames);
			row.Key("initialActor");
			WriteRoomMonster(stream, static_cast<int>(actor.getId()), actor);
			row.Key("initialPlayer");
			WriteRangedPlayer(stream, *MyPlayer);
			row.Number("initialRandom", GetLCGEngineState());
		};
		auto writeMissiles = [&]() {
			stream << '[';
			bool first = true;
			for (const Missile &missile : Missiles) {
				if (!first) stream << ',';
				first = false;
				WriteRangedMissile(stream, missile);
			}
			stream << ']';
		};
		auto prepareActor = [&](int depth, _monster_id nativeType, const UniqueMonsterData *unique) -> Monster * {
			return PrepareCatacombsActor(depth, nativeType, unique);
		};
		auto specialSeed = [&](const Monster &actor) {
			const int shift = actor.ai == MonsterAIID::Acid ? 1 : 0;
			const int threshold = (500 * (actor.intelligence + 2)) >> shift;
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				if (candidate.generateRnd(10000) < threshold) return seed;
			}
			return UINT32_MAX;
		};

		struct Lane {
			const char *name;
			int depth;
			_monster_id type;
			MissileID missile;
			bool special;
			bool unique;
		};
		const std::array<Lane, 5> lanes { {
			{ "skeleton-bow", 5, MT_WSKELBW, MissileID::Arrow, false, false },
			{ "goat-ranged", 5, MT_NGOATBW, MissileID::Arrow, false, false },
			{ "magma", 8, MT_NMAGMA, MissileID::MagmaBall, true, false },
			{ "acid", 6, MT_NACID, MissileID::Acid, true, false },
			{ "deathspit", 6, MT_NACID, MissileID::Acid, true, true },
		} };
		for (const Lane &lane : lanes) {
			const UniqueMonsterData *unique = nullptr;
			if (lane.unique) {
				const auto found = std::find_if(UniqueMonstersData.begin(), UniqueMonstersData.end(), [](const UniqueMonsterData &entry) { return entry.mName == "Deathspit"; });
				ASSERT_NE(found, UniqueMonstersData.end());
				unique = &*found;
			}
			Monster *actor = prepareActor(lane.depth, lane.type, unique);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			uint32_t seed = 2588;
			if (actor->ai == MonsterAIID::Magma || actor->ai == MonsterAIID::Acid) seed = specialSeed(*actor);
			ASSERT_NE(seed, UINT32_MAX);
			SetRndSeed(seed);
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, lane.name, lane.depth, lane.type, actor->ai, lane.missile, lane.special, lane.unique ? "Deathspit" : "", *actor);
			row.Key("ticks");
			stream << '[';
			bool firstTick = true, sawRelease = false, completed = false;
			for (int tick = 0; tick < 128; ++tick) {
				const WorldTilePosition future { 60, static_cast<WorldTileCoord>(50 + ((tick + 1) % 3)) };
				MyPlayer->position.future = future;
				const MonsterMode expectedMode = lane.special ? MonsterMode::SpecialRangedAttack : MonsterMode::RangedAttack;
				const int effectFrame = lane.special ? actor->data().animFrameNumSpecial - 1 : actor->data().animFrameNum - 1;
				const bool eligible = actor->mode == expectedMode && actor->animInfo.currentFrame == effectFrame
				    && (!lane.special || actor->animInfo.tickCounterOfCurrentFrame == 0);
				const int beforeFrame = actor->animInfo.currentFrame;
				const MonsterMode beforeMode = actor->mode;
				const bool wasLast = actor->animInfo.isLastFrame();
				const size_t missileCount = Missiles.size();
				if (!firstTick) stream << ',';
				firstTick = false;
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Boolean("releaseFrameEligible", eligible);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", missileCount);
				ProcessMonsters();
				const bool created = Missiles.size() > missileCount;
				if (eligible && created) sawRelease = true;
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", created);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { MyPlayer->position.future.x, MyPlayer->position.future.y });
				state.Key("missiles");
				writeMissiles();
				if (sawRelease && beforeMode == expectedMode && wasLast
				    && (actor->mode != expectedMode || actor->animInfo.currentFrame < beforeFrame)) {
					completed = true;
					break;
				}
			}
			stream << ']';
			row.Boolean("naturalReleaseObserved", sawRelease);
			row.Boolean("completedAndRedispatched", completed);
			EXPECT_TRUE(sawRelease) << lane.name;
			EXPECT_TRUE(completed) << lane.name;
		}

		// Ordinary attack checks the effect frame every tick, so a deliberately
		// held frame emits repeatedly. This is a prepared controller boundary.
		{
			Monster *actor = prepareActor(5, MT_NGOATBW, nullptr);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(2588);
			ProcessMonsters();
			ASSERT_EQ(actor->mode, MonsterMode::RangedAttack);
			actor->animInfo.currentFrame = actor->data().animFrameNum - 1;
			actor->animInfo.ticksPerFrame = 2;
			actor->animInfo.tickCounterOfCurrentFrame = 0;
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, "ordinary-held-effect-frame-repeat", 5, MT_NGOATBW, actor->ai, MissileID::Arrow, false, "", *actor);
			row.String("boundary", "prepared-held-normal-effect-frame");
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < 2; ++tick) {
				if (tick != 0) stream << ',';
				const size_t before = Missiles.size();
				const WorldTilePosition future { 60, 51 };
				MyPlayer->position.future = future;
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Boolean("releaseFrameEligible", true);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", before);
				ProcessMonsters();
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", Missiles.size() > before);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Key("missiles");
				writeMissiles();
			}
			stream << ']';
			row.Boolean("repeatObserved", Missiles.size() == 2U);
			EXPECT_EQ(Missiles.size(), 2U);
		}

		// Special release additionally requires counter zero at the effect frame.
		{
			Monster *actor = prepareActor(8, MT_NMAGMA, nullptr);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(specialSeed(*actor));
			ProcessMonsters();
			ASSERT_EQ(actor->mode, MonsterMode::SpecialRangedAttack);
			actor->animInfo.currentFrame = actor->data().animFrameNumSpecial - 1;
			actor->animInfo.ticksPerFrame = 3;
			actor->animInfo.tickCounterOfCurrentFrame = 1;
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, "special-counter-gated-release", 8, MT_NMAGMA, actor->ai, MissileID::MagmaBall, true, "", *actor);
			row.String("boundary", "prepared-special-effect-frame-counter-gate");
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < 2; ++tick) {
				if (tick != 0) stream << ',';
				if (tick == 1) {
					actor->animInfo.currentFrame = actor->data().animFrameNumSpecial - 1;
					actor->animInfo.tickCounterOfCurrentFrame = 0;
				}
				const size_t before = Missiles.size();
				const WorldTilePosition future { 60, static_cast<WorldTileCoord>(51 + tick) };
				MyPlayer->position.future = future;
				const bool eligible = actor->mode == MonsterMode::SpecialRangedAttack && actor->animInfo.currentFrame == actor->data().animFrameNumSpecial - 1 && actor->animInfo.tickCounterOfCurrentFrame == 0;
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Boolean("releaseFrameEligible", eligible);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", before);
				ProcessMonsters();
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", Missiles.size() > before);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Key("missiles");
				writeMissiles();
			}
			stream << ']';
			row.Boolean("counterGateObserved", Missiles.size() == 1U);
			EXPECT_EQ(Missiles.size(), 1U);
		}

		auto writeInterruptedSpecial = [&](std::string_view name, bool petrified) {
			Monster *actor = prepareActor(8, MT_NMAGMA, nullptr);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(specialSeed(*actor));
			ProcessMonsters();
			ASSERT_EQ(actor->mode, MonsterMode::SpecialRangedAttack);
			actor->animInfo.currentFrame = actor->data().animFrameNumSpecial - 1;
			actor->animInfo.tickCounterOfCurrentFrame = 0;
			if (petrified)
				actor->petrify();
			else
				actor->startHit(std::numeric_limits<int>::max());
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, name, 8, MT_NMAGMA, actor->ai, MissileID::MagmaBall, true, "", *actor);
			row.String("boundary", "prepared-special-effect-frame-interruption");
			row.String("interrupt", petrified ? "petrified" : "hard-hit");
			row.Key("ticks");
			stream << '[';
			const size_t before = Missiles.size();
			const WorldTilePosition future { 60, 52 };
			MyPlayer->position.future = future;
			{
				JsonObject state(stream);
				state.Number("tick", 0);
				state.Boolean("releaseFrameEligible", false);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", before);
				ProcessMonsters();
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", Missiles.size() > before);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { MyPlayer->position.future.x, MyPlayer->position.future.y });
				state.Key("missiles");
				writeMissiles();
			}
			stream << ']';
			row.Boolean("releaseSuppressed", Missiles.size() == before);
			EXPECT_EQ(Missiles.size(), before);
		};
		writeInterruptedSpecial("special-petrified-before-release", true);
		writeInterruptedSpecial("special-hard-hit-before-release", false);
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	ASSERT_TRUE(!stream.fail());
	EXPECT_EQ(caseCount, 9);
	EXPECT_LE(std::filesystem::file_size(root / "native-catacombs-ranged-release.json"), 32U * 1024U * 1024U);
}

struct NativeArrowMissileOrdinals {
	std::vector<std::pair<const Missile *, int>> values;
	int next = 1;

	int Get(const Missile &missile)
	{
		const auto found = std::find_if(values.begin(), values.end(), [&](const auto &entry) { return entry.first == &missile; });
		if (found != values.end())
			return found->second;
		values.emplace_back(&missile, next);
		return next++;
	}

	void Prune()
	{
		values.erase(std::remove_if(values.begin(), values.end(), [&](const auto &entry) {
			return std::none_of(Missiles.begin(), Missiles.end(), [&](const Missile &missile) { return &missile == entry.first; });
		}),
		    values.end());
	}
};

void WriteArrowPlayer(std::ostream &out, size_t index, const Player &player)
{
	JsonObject json(out);
	json.Number("playerIndex", index);
	json.Number("heroClassId", player._pClass);
	json.Number("characterLevel", player.getCharacterLevel());
	json.Number("levelId", player.plrlevel);
	json.Number("experience", player._pExperience);
	json.Boolean("active", player.plractive);
	json.Number("mode", player._pmode);
	json.Number("direction", player.direction);
	json.Key("tile");
	WriteRoomPoint(out, player.position.tile);
	json.Key("future");
	WriteRoomPoint(out, player.position.future);
	json.Key("old");
	WriteRoomPoint(out, player.position.old);
	json.Number("lifeQ6", player.life.current);
	json.Number("maximumLifeQ6", player.life.maximum);
	json.Number("armorClass", player._pArmorClass);
	json.Number("armorValue", player.GetArmor());
	json.Number("blockChanceWithoutLevel", player.GetBlockChance(false));
	json.Boolean("blockFlag", player._pBlockFlag);
	json.Boolean("invincible", player._pInvincible);
	json.Boolean("etherealized", HasAnyOf(player._pSpellFlags, SpellFlag::Etherealize));
	json.Number("spellFlags", player._pSpellFlags);
	json.Number("arghClangBaseSoundId", GetHeroSound(player._pClass, HeroSpeech::ArghClang));
}

bool IsArrowMonsterActive(size_t slot)
{
	return std::find(ActiveMonsters, ActiveMonsters + ActiveMonsterCount, static_cast<unsigned>(slot)) != ActiveMonsters + ActiveMonsterCount;
}

void WriteArrowMonster(std::ostream &out, int slot, const Monster &monster)
{
	JsonObject json(out);
	json.Number("slot", slot);
	json.Number("nativeType", monster.type().type);
	json.Boolean("active", IsArrowMonsterActive(slot));
	json.Key("tile");
	WriteRoomPoint(out, monster.position.tile);
	json.Key("future");
	WriteRoomPoint(out, monster.position.future);
	json.Key("old");
	WriteRoomPoint(out, monster.position.old);
	json.Number("mode", monster.mode);
	json.Number("hitPointsQ6", monster.hitPoints);
	json.Number("maximumHitPointsQ6", monster.maxHitPoints);
	json.Boolean("hasNoLife", monster.hasNoLife());
	json.Boolean("invalid", monster.isInvalid);
	json.Number("goal", monster.goal);
	json.Number("whoHit", monster.whoHit);
	json.Number("corpseId", monster.corpseId);
	json.Number("uniqueType", monster.uniqueType);
	json.Number("killCountForType", MonsterKillCounts[monster.type().type]);
	json.Number("itemSeed", monster.rndItemSeed);
	json.Number("aiSeed", monster.aiSeed);
	json.Number("treasure", monster.data().treasure);
	json.Number("experienceValue", monster.exp(DIFF_NORMAL));
	json.Number("level", monster.level(DIFF_NORMAL));
	json.Number("toHit", monster.toHit(DIFF_NORMAL));
	json.Number("minimumDamage", monster.minDamage);
	json.Number("maximumDamage", monster.maxDamage);
	json.Number("armorClass", monster.armorClass);
	json.Number("faction", monster.isPlayerMinion() ? 1 : 0);
	json.Boolean("playerMinion", monster.isPlayerMinion());
	json.Boolean("berserk", (monster.flags & MFLAG_BERSERK) != 0);
	json.Boolean("petrified", monster.mode == MonsterMode::Petrified || monster.animInfo.isPetrified);
	json.Number("flags", monster.flags);
}

void WriteArrowMissile(std::ostream &out, const Missile &missile, int ordinal)
{
	JsonObject json(out);
	json.Number("ordinal", ordinal);
	json.Number("type", missile._mitype);
	json.Number("sourceSlot", missile._misource);
	json.Number("caster", missile._micaster);
	json.Number("damage", missile._midam);
	json.Number("frameGroupDirection16", missile.getFrameGroupRaw());
	json.Number("animationFrameRaw", missile._miAnimFrame);
	json.Number("duration", missile.duration);
	json.Number("distance", missile._midist);
	json.Boolean("hit", missile._miHitFlag);
	json.Boolean("delete", missile._miDelFlag);
	json.Number("lastCollisionTargetHash", missile.lastCollisionTargetHash);
	json.Key("start");
	WriteRoomPoint(out, missile.position.start);
	json.Key("tile");
	WriteRoomPoint(out, missile.position.tile);
	json.Key("velocity16_16");
	WriteRoomPoint(out, { missile.position.velocity.deltaX, missile.position.velocity.deltaY });
	json.Key("traveled16_16");
	WriteRoomPoint(out, { missile.position.traveled.deltaX, missile.position.traveled.deltaY });
	json.Key("rawProjectileUnionDestination");
	WriteRoomPoint(out, { missile.projectile.destX, missile.projectile.destY });
}

void WriteArrowActiveMonsterPool(std::ostream &out)
{
	JsonObject json(out);
	json.Number("activeCount", ActiveMonsterCount);
	json.Key("activeIds");
	out << '[';
	for (size_t index = 0; index < ActiveMonsterCount; ++index) {
		if (index != 0) out << ',';
		out << ActiveMonsters[index];
	}
	out << ']';
}

void WriteArrowItemPool(std::ostream &out)
{
	JsonObject json(out);
	json.Number("activeCount", ActiveItemCount);
	json.Key("poolOrder");
	out << '[';
	for (int index = 0; index < MAXITEMS; ++index) {
		if (index != 0) out << ',';
		out << static_cast<int>(ActiveItems[index]);
	}
	out << ']';
	json.Key("activeItems");
	out << '[';
	for (int index = 0; index < ActiveItemCount; ++index) {
		if (index != 0) out << ',';
		JsonObject row(out);
		const int slot = ActiveItems[index];
		row.Number("slot", slot);
		row.Key("item");
		WriteItem(out, Items[slot]);
	}
	out << ']';
	json.Key("itemRefs");
	out << '[';
	int tileIndex = 0;
	for (const Tile &tile : tiles()) {
		if (tileIndex++ != 0) out << ',';
		out << static_cast<int>(tile.item());
	}
	out << ']';
}

void AddArrowSampleCell(std::vector<Point> &cells, Point position)
{
	if (!InDungeonBounds(position))
		return;
	if (std::find(cells.begin(), cells.end(), position) == cells.end())
		cells.push_back(position);
}

void WriteArrowCollisionCells(std::ostream &out, const std::vector<Point> &focusCells, const std::vector<int> &actorSlots, const NativeArrowMissileOrdinals & /*ordinals*/)
{
	std::vector<Point> cells = focusCells;
	for (const Player &player : Players) {
		AddArrowSampleCell(cells, player.position.tile);
		AddArrowSampleCell(cells, player.position.future);
		AddArrowSampleCell(cells, player.position.old);
	}
	for (int slot : actorSlots) {
		if (slot < 0 || slot >= MaxMonsters)
			continue;
		const Monster &actor = Monsters[slot];
		AddArrowSampleCell(cells, actor.position.tile);
		AddArrowSampleCell(cells, actor.position.future);
		AddArrowSampleCell(cells, actor.position.old);
	}
	for (const Missile &missile : Missiles) {
		AddArrowSampleCell(cells, missile.position.tile);
		AddArrowSampleCell(cells, missile.position.start);
		for (int y = -1; y <= 1; ++y)
			for (int x = -1; x <= 1; ++x)
				AddArrowSampleCell(cells, missile.position.tile + Displacement { x, y });
	}
	std::sort(cells.begin(), cells.end(), [](Point left, Point right) { return left.y != right.y ? left.y < right.y : left.x < right.x; });
	out << '[';
	bool first = true;
	for (Point position : cells) {
		if (!first)
			out << ',';
		first = false;
		const Tile &tile = tileAt(position);
		const int piece = tile.piece();
		const int signedMonster = tile.monster();
		const int signedPlayer = tile.player();
		const int objectValue = tile.object();
		JsonObject json(out);
		json.Key("position");
		WriteRoomPoint(out, position);
		json.Number("piece", piece);
		json.Number("solFlags", piece >= 0 && piece < MAXTILES ? tileProperties()[piece] : TileProperties::None);
		json.Number("signedMonster", signedMonster);
		json.Number("signedPlayer", signedPlayer);
		json.Number("signedOccupancyXor", signedMonster ^ signedPlayer);
		json.Number("signedObject", objectValue);
		json.Number("item", tile.item());
		json.Number("corpse", tile.corpse());
		json.Boolean("blockedByTileOrObject", IsMissileBlockedByTile(position));
		if (objectValue == 0) {
			json.Key("object");
			out << "null";
		} else {
			const Object &object = Objects[std::abs(objectValue) - 1];
			json.Key("object");
			JsonObject objectJson(out);
			objectJson.Number("slot", std::abs(objectValue) - 1);
			objectJson.Number("type", object._otype);
			objectJson.Number("breakState", object._oBreak);
			objectJson.Boolean("missilePassable", object._oMissFlag);
		}
	}
	out << ']';
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsArrowFlightAndImpact)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_ARROW_FLIGHT_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the explicit Catacombs Arrow flight oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native Arrow evidence.";
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode, missilePreFlag = MissilePreFlag;
		uint8_t playerId = MyPlayerId;
		uint32_t random = GetLCGEngineState();
		std::array<int, NUM_MAX_MTYPES> oldMonsterKillCounts {};
		std::list<Missile> savedMissiles;
		ScopedMissileDataForTest missileData;
		bool networkInitialized = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		ExecutionGuard()
		{
			Missiles.swap(savedMissiles);
			std::copy_n(MonsterKillCounts, oldMonsterKillCounts.size(), oldMonsterKillCounts.begin());
		}
		~ExecutionGuard()
		{
			Missiles.swap(savedMissiles);
			MissilePreFlag = missilePreFlag;
			std::copy(oldMonsterKillCounts.begin(), oldMonsterKillCounts.end(), MonsterKillCounts);
			if (networkInitialized) SNetDestroy();
			MyPlayerId = playerId;
			gbSndInited = sound;
			HeadlessMode = headless;
			SetRndSeed(random);
		}
	} guard;
	ASSERT_TRUE(guard.networkInitialized);
	HeadlessMode = true;
	gbSndInited = false;
	MyPlayerId = 0;
	LoadPlayerDataFiles();
	LoadMissileData();
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Arrow));
	const MissileData &arrowData = GetMissileData(MissileID::Arrow);

	std::ofstream stream(root / "native-catacombs-arrow-flight.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-arrow-flight");
		document.Number("version", 1);
		document.String("policy", "prepared-original-diablo-catacombs-arrow-flight-impact");
		document.Key("cases");
		stream << '[';
		auto className = [](HeroClass heroClass) -> std::string_view {
			switch (heroClass) {
			case HeroClass::Warrior: return "warrior";
			case HeroClass::Sorcerer: return "sorcerer";
			default: return "unsupported";
			}
		};
		auto configureHero = [&](HeroClass heroClass, uint8_t characterLevel, Point position) {
			Player hero {};
			hero._pClass = heroClass;
			hero.setCharacterLevel(characterLevel);
			const ClassAttributes &attributes = hero.getClassAttributes();
			hero.attributes.strength.base = hero.attributes.strength.current = attributes.baseStr;
			hero.attributes.magic.base = hero.attributes.magic.current = attributes.baseMag;
			hero.attributes.dexterity.base = hero.attributes.dexterity.current = attributes.baseDex;
			hero.attributes.vitality.base = hero.attributes.vitality.current = attributes.baseVit;
			const int life = hero.calculateBaseLife();
			const int mana = hero.calculateBaseMana();
			hero.life = { life, life, life, life, 100 };
			hero.mana = { mana, mana, mana, mana, 100 };
			hero.setAnimations();
			hero.plractive = true;
			hero.plrlevel = 5;
			hero.plrIsOnSetLevel = false;
			hero._pLvlChanging = false;
			hero.pLvlLoad = 1;
			hero._pInvincible = false;
			hero._pBlockFlag = false;
			hero._pArmorClass = 0;
			hero._pIAC = 0;
			hero._pIBonusAC = 0;
			hero._pmode = PM_STAND;
			hero.position.tile = position;
			hero.position.future = position;
			hero.position.old = position;
			return hero;
		};
		auto seedForPlayerHit = [&](const Player &hero, const Monster &source, bool requireBlock, bool requireHit) {
			int hitChance = static_cast<int>(source.toHit(DIFF_NORMAL))
			    + (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2 + 30 - 2 - hero.GetArmor();
			hitChance = std::max(hitChance, 10);
			const int blockChance = std::clamp(hero.GetBlockChance(false)
			        - (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2,
			    0, 100);
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				const int hitRoll = candidate.generateRnd(100);
				const int blockRoll = requireBlock ? candidate.generateRnd(100) : 100;
				if ((hitRoll < hitChance) == requireHit && (!requireBlock || blockRoll < blockChance))
					return seed;
			}
			return UINT32_MAX;
		};
		auto seedForBlockThenHit = [&](const Player &hero, const Monster &source) {
			int hitChance = static_cast<int>(source.toHit(DIFF_NORMAL))
			    + (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2 + 30 - 2 - hero.GetArmor();
			hitChance = std::max(hitChance, 10);
			const int blockChance = std::clamp(hero.GetBlockChance(false)
			        - (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2,
			    0, 100);
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				const int firstHitRoll = candidate.generateRnd(100);
				const int firstBlockRoll = candidate.generateRnd(100);
				candidate.generateRnd(1); // PlayerMHit rolls arrow damage before testing the block result.
				const int secondHitRoll = candidate.generateRnd(100);
				if (firstHitRoll < hitChance && firstBlockRoll < blockChance && secondHitRoll < hitChance)
					return seed;
			}
			return UINT32_MAX;
		};
		auto seedForMonsterHit = [](const Player &, const Monster &) {
			constexpr int MonsterArrowHitChance = 89; // MonsterTrapHit uses 90 - armor(0) - first-step distance(1).
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				if (candidate.generateRnd(100) < MonsterArrowHitChance)
					return seed;
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

		auto runCase = [&](std::string_view name, std::string_view boundary, HeroClass heroClass, uint8_t heroLevel,
		                   Point sourcePosition, Point heroPosition, Point destination, int tickLimit,
		                   auto prepare, auto chooseSeed, auto launch, auto mutate) {
			PrepareRoom(5, 0, 0, 0, false);
			for (Tile &tile : tiles()) {
				tile.setPlayer(0);
				tile.setMonster(0);
				tile.setObject(0);
			}
			Missiles.clear();
			MissilePreFlag = false;
			Players.resize(1);
			MyPlayer = &Players[0];
			*MyPlayer = configureHero(heroClass, heroLevel, heroPosition);
			MyPlayer->occupyTile(heroPosition, false);
			LevelBestiary.init();
			std::vector<std::pair<_monster_id, size_t>> typeIndices;
			std::vector<Monster *> actors;
			std::vector<int> actorSlots;
			std::vector<Point> focusCells { sourcePosition, heroPosition, destination };
			auto spawnActor = [&](_monster_id type, Point position) -> Monster * {
				const auto existing = std::find_if(typeIndices.begin(), typeIndices.end(), [&](const auto &entry) { return entry.first == type; });
				size_t typeIndex;
				if (existing == typeIndices.end()) {
					const auto added = LevelBestiary.addType(type, PLACE_SCATTER);
					if (!added.has_value()) return nullptr;
					typeIndex = *added;
					typeIndices.emplace_back(type, typeIndex);
				} else {
					typeIndex = existing->second;
				}
				Monster *actor = AddMonster(position, Direction::SouthEast, typeIndex, true);
				if (actor != nullptr) {
					actor->enemy = 0;
					actor->enemyPosition = heroPosition;
					actor->activeForTicks = 0;
					actor->position.last = position;
					actor->position.future = position;
					actor->position.old = position;
					actor->hitPoints = actor->maxHitPoints = 10000 << 6;
					actors.push_back(actor);
					actorSlots.push_back(static_cast<int>(actor->getId()));
					AddArrowSampleCell(focusCells, position);
				}
				return actor;
			};
			Monster *source = spawnActor(MT_WSKELBW, sourcePosition);
			ASSERT_NE(source, nullptr);
			source->minDamage = 1;
			source->maxDamage = 1;
			prepare(*MyPlayer, *source, spawnActor, actors, actorSlots, focusCells);
			const uint32_t seed = chooseSeed(*MyPlayer, *source);
			ASSERT_NE(seed, UINT32_MAX) << "No deterministic seed matches the requested native hit/block boundary: " << name;
			SetRndSeed(seed);
			const bool captureItemPool = name == "monster-impact-remaining-life-64" || name == "monster-impact-lethal-fractional-63";
			bool observedMapEdgeDeletion = false;
			NativeArrowMissileOrdinals ordinals;
			if (caseCount != 0)
				stream << ',';
			JsonObject row(stream);
			row.String("name", name);
			row.String("boundary", boundary);
			row.Number("heroClassId", heroClass);
			row.String("heroClass", className(heroClass));
			row.Number("heroLevel", MyPlayer->getCharacterLevel());
			row.Number("sourceSlot", source->getId());
			row.Number("sourceType", source->type().type);
			row.Key("sourceStart");
			WriteRoomPoint(stream, sourcePosition);
			row.Key("destination");
			WriteRoomPoint(stream, destination);
			row.Number("initialSeed", seed);
			row.Key("missileData");
			{
				JsonObject data(stream);
				data.Number("missileId", MissileID::Arrow);
				data.Number("damageType", arrowData.damageType());
				data.Number("castSoundId", arrowData.castSound);
				data.Number("hitSoundId", arrowData.hitSound);
				data.Number("movementDistribution", arrowData.movementDistribution);
			}
			int launchCount = 0;
			auto emitArrow = [&](std::ostream &out, Point start, Point target, Direction fallbackDirection, bool stationary) {
				const uint32_t rngBefore = GetLCGEngineState();
				Missile *missile = AddMissile(start, target, fallbackDirection, MissileID::Arrow, TARGET_PLAYERS, *source, 0, 0, nullptr, SfxID::None);
				if (missile == nullptr) {
					ADD_FAILURE() << "Native Arrow creation failed.";
					return;
				}
				if (stationary) missile->position.velocity = { 0, 0 };
				if (launchCount++ != 0) out << ',';
				JsonObject launchRow(out);
				const int ordinal = ordinals.Get(*missile);
				launchRow.Number("ordinal", ordinal);
				launchRow.Number("rngBefore", rngBefore);
				launchRow.Number("rngAfter", GetLCGEngineState());
				launchRow.Key("start");
				WriteRoomPoint(out, start);
				launchRow.Number("requestedTargetX", target.x);
				launchRow.Number("requestedTargetY", target.y);
				launchRow.Number("launchFacingDirection", fallbackDirection);
				launchRow.Boolean("preparedStationaryVelocity", stationary);
				launchRow.Number("direction16", missile->_miAnimFrame - 1);
				launchRow.Number("frameGroupDirection16", missile->getFrameGroupRaw());
				launchRow.Number("animationFrameRaw", missile->_miAnimFrame);
				launchRow.Key("velocity16_16");
				WriteRoomPoint(out, { missile->position.velocity.deltaX, missile->position.velocity.deltaY });
			};
			row.Key("launches");
			stream << '[';
			launch(stream, *MyPlayer, *source, actors, actorSlots, focusCells, emitArrow);
			stream << ']';
			auto writePlayers = [&]() {
				stream << '[';
				for (size_t index = 0; index < Players.size(); ++index) {
					if (index != 0) stream << ',';
					WriteArrowPlayer(stream, index, Players[index]);
				}
				stream << ']';
			};
			auto writeActors = [&]() {
				stream << '[';
				for (size_t index = 0; index < actorSlots.size(); ++index) {
					if (index != 0) stream << ',';
					WriteArrowMonster(stream, actorSlots[index], Monsters[actorSlots[index]]);
				}
				stream << ']';
			};
			auto writeMissiles = [&]() {
				std::vector<int> ordinalsNow;
				stream << '[';
				bool first = true;
				for (const Missile &missile : Missiles) {
					if (!first) stream << ',';
					first = false;
					const int ordinal = ordinals.Get(missile);
					ordinalsNow.push_back(ordinal);
					WriteArrowMissile(stream, missile, ordinal);
				}
				stream << ']';
				return ordinalsNow;
			};
			row.Key("initial");
			{
				JsonObject initial(stream);
				initial.Number("rng", GetLCGEngineState());
				initial.Key("players");
				writePlayers();
				initial.Key("actors");
				writeActors();
				initial.Key("activeMonsterPool");
				WriteArrowActiveMonsterPool(stream);
				if (captureItemPool) {
					initial.Key("itemPool");
					WriteArrowItemPool(stream);
				}
				initial.Key("missiles");
				writeMissiles();
				initial.Key("collisionCells");
				WriteArrowCollisionCells(stream, focusCells, actorSlots, ordinals);
			}
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < tickLimit; ++tick) {
				if (tick != 0) stream << ',';
				JsonObject state(stream);
				state.Number("tickIndex", tick);
				state.Key("mutationInputs");
				{
					JsonObject inputs(stream);
					mutate(tick, *MyPlayer, *source, spawnActor, actors, actorSlots, focusCells, inputs);
				}
				state.Number("rngBefore", GetLCGEngineState());
				state.Boolean("missilePreFlagBefore", MissilePreFlag);
				state.Key("playersBefore");
				writePlayers();
				state.Key("actorsBefore");
				writeActors();
				state.Key("missilesBefore");
				const std::vector<int> liveBefore = writeMissiles();
				state.Key("collisionCellsBefore");
				WriteArrowCollisionCells(stream, focusCells, actorSlots, ordinals);
				const int playerLifeBeforeStep = MyPlayer->life.current;
				const int targetHitPointsBeforeStep = actorSlots.size() > 1 ? Monsters[actorSlots.back()].hitPoints : 0;
				const int targetKillCountBeforeStep = actorSlots.size() > 1 ? MonsterKillCounts[Monsters[actorSlots.back()].type().type] : 0;
				const size_t activeMonsterCountBeforeStep = ActiveMonsterCount;
				const uint8_t activeItemCountBeforeStep = ActiveItemCount;
				state.Key("activeMonsterPoolBefore");
				WriteArrowActiveMonsterPool(stream);
				if (captureItemPool) {
					state.Key("itemPoolBefore");
					WriteArrowItemPool(stream);
				}
				ProcessMissiles();
				if (tick == 0) {
					if (name == "warrior-low-damage-hit-no-recovery" || name == "sorcerer-low-damage-hit-no-recovery") {
						EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << name;
						EXPECT_EQ(MyPlayer->_pmode, PM_STAND) << name;
					} else if (name == "player-arrow-miss" || name == "player-arrow-invincible" || name == "player-arrow-etherealized" || name == "player-arrow-already-dead" || name == "tile-missile-blocker") {
						EXPECT_EQ(MyPlayer->life.current, playerLifeBeforeStep) << name;
					} else if (name == "player-arrow-block") {
						EXPECT_EQ(MyPlayer->life.current, playerLifeBeforeStep) << name;
						EXPECT_EQ(MyPlayer->_pmode, PM_BLOCK) << name;
					} else if (name == "player-hit-before-same-cell-tile-blocker" || name == "player-hit-before-same-cell-breakable-crux" || name == "final-lifetime-tick-collision") {
						EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << name;
					}
					if (name == "same-faction-monster-pass" || name == "moving-negative-nonpetrified-monster-ignored")
						ASSERT_EQ(Monsters[actorSlots.back()].hitPoints, targetHitPointsBeforeStep) << name;
					if (name == "opposing-player-minion-target" || name == "source-berserk-allows-same-faction-hit" || name == "target-berserk-allows-same-faction-hit" || name == "petrified-negative-monster-occupancy-hit")
						ASSERT_LT(Monsters[actorSlots.back()].hitPoints, targetHitPointsBeforeStep) << name;
				}
				if (tick == 0 && (name == "source-live-damage-changed-before-impact" || name == "source-removed-before-impact" || name == "source-dead-before-impact"))
					EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << name;
				if (tick == 0 && name == "moving-player-negative-then-settled")
					EXPECT_EQ(MyPlayer->life.current, playerLifeBeforeStep) << "A moving negative player reference is ignored.";
				if (tick == 1 && name == "moving-player-negative-then-settled")
					EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << "The settled positive player reference is hit.";
				if (tick == 1 && name == "new-monster-interceptor-at-current-cell")
					ASSERT_LT(Monsters[actorSlots.back()].hitPoints, targetHitPointsBeforeStep);
				if (tick == 1 && name == "new-player-interceptor-at-current-cell")
					ASSERT_LT(MyPlayer->life.current, playerLifeBeforeStep);
				if (tick == 0 && name == "ordered-two-arrow-block-then-hit") {
					ASSERT_EQ(liveBefore.size(), 2U);
					EXPECT_EQ(liveBefore[0], 1);
					EXPECT_EQ(liveBefore[1], 2);
					EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep);
					EXPECT_EQ(MyPlayer->_pmode, PM_BLOCK);
				}
				if (tick == 0 && name == "monster-impact-remaining-life-64") {
					ASSERT_EQ(targetHitPointsBeforeStep, 128);
					EXPECT_EQ(Monsters[actorSlots.back()].hitPoints, 64);
					EXPECT_FALSE(Monsters[actorSlots.back()].hasNoLife());
					EXPECT_NE(Monsters[actorSlots.back()].mode, MonsterMode::Death);
					EXPECT_EQ(MonsterKillCounts[Monsters[actorSlots.back()].type().type], targetKillCountBeforeStep);
				}
				if (tick == 0 && name == "monster-impact-lethal-fractional-63") {
					Monster &target = Monsters[actorSlots.back()];
					ASSERT_EQ(targetHitPointsBeforeStep, 127);
					ASSERT_TRUE(target.hasNoLife());
					EXPECT_EQ(target.hitPoints, 0);
					EXPECT_EQ(target.mode, MonsterMode::Death);
					EXPECT_EQ(MonsterKillCounts[target.type().type], targetKillCountBeforeStep + 1);
					EXPECT_EQ(ActiveMonsterCount, activeMonsterCountBeforeStep);
					EXPECT_EQ(ActiveItemCount, activeItemCountBeforeStep + 1);
					EXPECT_GT(MyPlayer->_pExperience, 0U);
				}
				if (tick == 0 && name == "player-hit-before-same-cell-breakable-crux") {
					EXPECT_EQ(Objects[0]._oBreak, -1);
					EXPECT_TRUE(Objects[0]._oMissFlag);
				}
				state.Number("rngAfter", GetLCGEngineState());
				state.Boolean("missilePreFlagAfter", MissilePreFlag);
				state.Key("playersAfter");
				writePlayers();
				state.Key("actorsAfter");
				writeActors();
				state.Key("missilesAfter");
				const std::vector<int> liveAfter = writeMissiles();
				state.Key("deletedMissileOrdinals");
				stream << '[';
				bool firstDeleted = true;
				for (int ordinal : liveBefore) {
					if (std::find(liveAfter.begin(), liveAfter.end(), ordinal) != liveAfter.end()) continue;
					if (!firstDeleted) stream << ',';
					firstDeleted = false;
					stream << ordinal;
				}
				stream << ']';
				if (name == "natural-map-edge-out-of-bounds-delete" && !liveBefore.empty()
				    && std::find(liveAfter.begin(), liveAfter.end(), liveBefore.front()) == liveAfter.end()) {
					observedMapEdgeDeletion = true;
					EXPECT_LT(tick, 32) << "An edge flight must expire by leaving the map, well before its 256-tick lifetime.";
				}
				state.Key("activeMonsterPoolAfter");
				WriteArrowActiveMonsterPool(stream);
				if (captureItemPool) {
					state.Key("itemPoolAfter");
					WriteArrowItemPool(stream);
				}
				state.Key("collisionCellsAfter");
				WriteArrowCollisionCells(stream, focusCells, actorSlots, ordinals);
				ordinals.Prune();
				if (name == "natural-map-edge-out-of-bounds-delete" && Missiles.empty()) break;
				if (Missiles.empty() && tick + 1 >= tickLimit) break;
			}
			if (name == "natural-map-edge-out-of-bounds-delete")
				EXPECT_TRUE(observedMapEdgeDeletion) << "The open edge corridor must terminate by native out-of-bounds deletion.";
			stream << ']';
			++caseCount;
		};
		auto noPrepare = [](Player &, Monster &, auto &, auto &, auto &, auto &) {};
		auto noMutation = [](int, Player &, Monster &, auto &, auto &, auto &, auto &, JsonObject &inputs) { inputs.String("kind", "none"); };
		auto fixedSeed = [](const Player &, const Monster &) { return UINT32_C(2588); };
		auto emitOneStationary = [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
			emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true);
		};

		for (int order = 0; order < 2; ++order) {
			runCase(order == 0 ? "direction-16-ordered-burst" : "direction-16-reversed-burst", "prepared-same-tick-native-add-arrow-order", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, noPrepare, fixedSeed, [&, order](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				std::vector<std::pair<int, Point>> destinations;
				for (int direction = 0; direction < 16; ++direction) {
					Point best { 50, 50 }; int bestScore = INT_MAX;
					for (int y = -12; y <= 12; ++y) for (int x = -12; x <= 12; ++x) {
						if (x == 0 && y == 0) continue;
						const Point candidate { 50 + x, 50 + y };
						if (GetDirection16({ 50, 50 }, candidate) != static_cast<Direction16>(direction)) continue;
						const int score = std::max(std::abs(x), std::abs(y)) * 64 + std::abs(x) + std::abs(y);
						if (score < bestScore) { best = candidate; bestScore = score; }
					}
					ASSERT_NE(best, (Point { 50, 50 }));
					destinations.emplace_back(direction, best);
				}
				if (order != 0) std::reverse(destinations.begin(), destinations.end());
				for (const auto &[direction, destination] : destinations)
					emitArrow(out, { 50, 50 }, destination, Direction::South, false); }, noMutation);
		}

		for (int direction = 0; direction < 16; ++direction) {
			std::ostringstream name;
			name << "natural-direction-" << std::setfill('0') << std::setw(2) << direction;
			runCase(name.str(), "native-add-arrow-direction16-fixed-point-flight", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 54, 54 }, 12, noPrepare, fixedSeed, [&, direction](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				Point destination { 50, 50 }; int bestScore = INT_MAX;
				for (int y = -12; y <= 12; ++y) for (int x = -12; x <= 12; ++x) {
					if (x == 0 && y == 0) continue;
					const Point candidate { 50 + x, 50 + y };
					if (GetDirection16({ 50, 50 }, candidate) != static_cast<Direction16>(direction)) continue;
					const int score = std::max(std::abs(x), std::abs(y)) * 64 + std::abs(x) + std::abs(y);
					if (score < bestScore) { destination = candidate; bestScore = score; }
				}
				ASSERT_NE(destination, (Point { 50, 50 }));
				emitArrow(out, { 50, 50 }, destination, Direction::South, false); }, noMutation);
		}

		runCase("natural-flight-beyond-requested-endpoint", "native-arrow-continues-until-collision-or-lifetime", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 52, 50 }, 16, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 52, 50 }, Direction::East, false); }, noMutation);
		runCase("natural-skipped-tile-diagonal", "native-arrow-fixed-point-skipped-tile-sampling", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 58, 57 }, 16, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 58, 57 }, Direction::SouthEast, false); }, noMutation);
		runCase("natural-map-edge-out-of-bounds-delete", "native-putmissile-deletes-after-leaving-dungeon-bounds", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 111, 50 }, 32, [](Player &, Monster &, auto &, auto &, auto &, auto &cells) {
			for (int x = 104; x <= 111; ++x) {
				tileAt({ x, 50 }).setPiece(0);
				tileAt({ x, 50 }).setTransVal(1);
				ASSERT_FALSE(IsMissileBlockedByTile({ x, 50 }));
				AddArrowSampleCell(cells, { x, 50 });
			} }, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				emitArrow(out, { 110, 50 }, { 111, 50 }, Direction::East, false);
				ASSERT_GT(Missiles.back().position.velocity.deltaX, 0);
				ASSERT_GT(Missiles.back().position.velocity.deltaY, 0); }, noMutation);
		runCase("stationary-full-256-tick-lifetime", "prepared-zero-velocity-arrow-lifetime-boundary", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 50, 50 }, 256, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 51, 50 }, Direction::East, true); }, noMutation);

		auto playerCell = [](Player &hero, Monster &, auto &, auto &, auto &, auto &cells) {
			hero.position.tile = { 50, 50 };
			hero.position.future = hero.position.tile;
			hero.position.old = hero.position.tile;
			for (Tile &tile : tiles())
				tile.setPlayer(0);
			hero.occupyTile(hero.position.tile, false);
			ASSERT_GT(tileAt(hero.position.tile).player(), 0);
			AddArrowSampleCell(cells, hero.position.tile);
		};
		auto lowDamage = [](int, Player &, Monster &source, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			source.minDamage = source.maxDamage = 1;
			inputs.String("kind", "source-damage-fixed-before-step");
			inputs.Number("minimumDamageInput", 1);
			inputs.Number("maximumDamageInput", 1);
		};
		auto hitSeed = [&](const Player &hero, const Monster &source) { return seedForPlayerHit(hero, source, false, true); };
		auto missSeed = [&](const Player &hero, const Monster &source) { return seedForPlayerHit(hero, source, false, false); };
		runCase("warrior-low-damage-hit-no-recovery", "native-player-mhit-low-damage-arghclang-before-recovery-check",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, lowDamage);
		runCase("sorcerer-low-damage-hit-no-recovery", "native-player-mhit-low-damage-arghclang-before-recovery-check",
		    HeroClass::Sorcerer, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, lowDamage);
		runCase("player-arrow-miss", "native-arrow-player-hit-roll-miss", HeroClass::Warrior, 2,
		    { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, missSeed, emitOneStationary, lowDamage);
		auto blockPrepare = [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
			playerCell(hero, source, spawn, actors, slots, cells);
			hero.attributes.dexterity.current = 90;
			hero._pBlockFlag = true;
			ASSERT_GT(hero.GetBlockChance(false), 0);
		};
		auto blockSeed = [&](const Player &hero, const Monster &source) { return seedForPlayerHit(hero, source, true, true); };
		runCase("player-arrow-block", "native-arrow-hit-then-block-roll-and-start-block", HeroClass::Warrior, 2,
		    { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, blockPrepare, blockSeed, emitOneStationary, lowDamage);
		runCase("ordered-two-arrow-block-then-hit", "native-same-tick-arrow-order-changes-player-block-eligibility", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
			playerCell(hero, source, spawn, actors, slots, cells);
			hero.attributes.dexterity.current = 90;
			hero._pBlockFlag = true;
			ASSERT_GT(hero.GetBlockChance(false), 0); }, seedForBlockThenHit, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true);
				emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true); }, [](int tick, Player &, Monster &, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				inputs.String("kind", "native-ordered-two-arrow-block-then-hit");
				inputs.Number("firstExpectedOrdinal", 1); inputs.Number("secondExpectedOrdinal", 2);
			} else inputs.String("kind", "none"); });
		runCase("player-arrow-invincible", "native-player-mhit-invincible-early-return", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) { playerCell(hero, source, spawn, actors, slots, cells); hero._pInvincible = true; ASSERT_TRUE(hero._pInvincible); }, fixedSeed, emitOneStationary, lowDamage);
		runCase("player-arrow-etherealized", "native-arrow-etherealize-early-return", HeroClass::Sorcerer, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) { playerCell(hero, source, spawn, actors, slots, cells); hero._pSpellFlags |= SpellFlag::Etherealize; ASSERT_TRUE(HasAnyOf(hero._pSpellFlags, SpellFlag::Etherealize)); }, fixedSeed, emitOneStationary, lowDamage);
		runCase("player-arrow-already-dead", "native-player-mhit-dead-early-return", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) { playerCell(hero, source, spawn, actors, slots, cells); hero.life.current = 0; ASSERT_EQ(hero.life.current, 0); }, fixedSeed, emitOneStationary, lowDamage);

		auto movingPlayerPrepare = [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
			playerCell(hero, source, spawn, actors, slots, cells);
			hero.position.future = { 51, 50 };
			hero.occupyTile({ 50, 50 }, true);
		};
		auto movingPlayerMutation = [](int tick, Player &hero, Monster &, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				ASSERT_LT(tileAt({ 50, 50 }).player(), 0);
				inputs.String("kind", "moving-player-negative-occupancy");
				inputs.Number("signedPlayerBefore", tileAt({ 50, 50 }).player());
			} else {
				hero.position.future = hero.position.tile;
				hero.occupyTile(hero.position.tile, false);
				ASSERT_GT(tileAt({ 50, 50 }).player(), 0);
				inputs.String("kind", "moving-player-settled-to-positive-occupancy");
				inputs.Number("signedPlayerBefore", tileAt({ 50, 50 }).player());
			}
		};
		runCase("moving-player-negative-then-settled", "native-stationary-target-hash-change-and-moving-player-ignore",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 2, movingPlayerPrepare, hitSeed, emitOneStationary, movingPlayerMutation);
		runCase("new-monster-interceptor-at-current-cell", "prepared-new-interceptor-before-native-arrow-step", HeroClass::Warrior, 2, { 44, 50 }, { 80, 80 }, { 50, 50 }, 3, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 56, 50 }, Direction::East, false); }, [](int tick, Player &, Monster &, auto &spawn, auto &actors, auto &, auto &cells, JsonObject &inputs) {
				if (tick != 1 || Missiles.empty()) { inputs.String("kind", "none"); return; }
				const Point position = Missiles.front().position.tile;
				Missiles.front().position.velocity = { 0, 0 };
				Monster *interceptor = spawn(MT_WSKELBW, position);
				inputs.String("kind", "spawn-native-monster-interceptor"); inputs.Number("positionX", position.x); inputs.Number("positionY", position.y);
				if (interceptor != nullptr) {
					interceptor->armorClass = 0; interceptor->flags |= MFLAG_GOLEM; interceptor->golemToHit = 80;
					ASSERT_TRUE(interceptor->isPlayerMinion());
					ASSERT_GT(tileAt(position).monster(), 0);
					cells.push_back(position); inputs.Number("slot", interceptor->getId()); inputs.Number("signedOccupancy", tileAt(position).monster());
				}
				else inputs.Number("slot", -1);
				(void)actors; });
		runCase("new-player-interceptor-at-current-cell", "prepared-player-moves-onto-stationary-arrow-cell", HeroClass::Warrior, 2, { 44, 50 }, { 80, 80 }, { 50, 50 }, 3, noPrepare, hitSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 56, 50 }, Direction::East, false); }, [](int tick, Player &hero, Monster &, auto &, auto &, auto &, auto &cells, JsonObject &inputs) {
				if (tick != 1 || Missiles.empty()) { inputs.String("kind", "none"); return; }
				const Point oldPosition = hero.position.tile;
				const Point position = Missiles.front().position.tile;
				Missiles.front().position.velocity = { 0, 0 };
				for (Tile &tile : tiles()) tile.setPlayer(0);
				hero.position.old = oldPosition; hero.position.tile = position; hero.position.future = position;
				hero.occupyTile(position, false);
				ASSERT_GT(tileAt(position).player(), 0);
				cells.push_back(position);
				inputs.String("kind", "move-player-to-current-arrow-cell-before-step");
				inputs.Number("oldPositionX", oldPosition.x); inputs.Number("oldPositionY", oldPosition.y);
				inputs.Number("newPositionX", position.x); inputs.Number("newPositionY", position.y);
				inputs.Number("signedPlayerInput", tileAt(position).player()); });

		auto stationaryMonsterTarget = [&](std::string_view caseName, bool sourceGolem, bool sourceBerserk, bool targetGolem, bool targetBerserk, bool targetPetrified, bool targetMoving) {
			auto prepare = [=](Player &, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
				source.minDamage = source.maxDamage = 1;
				if (sourceGolem) {
					source.flags |= MFLAG_GOLEM;
					source.golemToHit = 80;
					ASSERT_TRUE(source.isPlayerMinion());
				}
				if (sourceBerserk) source.flags |= MFLAG_BERSERK;
				Monster *target = spawn(MT_WSKELBW, { 50, 50 });
				ASSERT_NE(target, nullptr);
				if (targetGolem) {
					target->flags |= MFLAG_GOLEM;
					target->golemToHit = 80;
				}
				if (targetBerserk) target->flags |= MFLAG_BERSERK;
				if (targetPetrified) {
					target->petrify();
					target->occupyTile({ 50, 50 }, true);
					ASSERT_LT(tileAt({ 50, 50 }).monster(), 0);
					ASSERT_TRUE(target->mode == MonsterMode::Petrified || target->animInfo.isPetrified);
				} else if (targetMoving) {
					target->occupyTile({ 50, 50 }, true);
					ASSERT_LT(tileAt({ 50, 50 }).monster(), 0);
					ASSERT_FALSE(target->mode == MonsterMode::Petrified || target->animInfo.isPetrified);
				}
				AddArrowSampleCell(cells, { 50, 50 });
				(void)actors;
				(void)slots;
			};
			auto launch = [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true); };
			runCase(caseName, "native-checkmissilecol-monster-faction-and-signed-occupancy",
			    HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, prepare, fixedSeed, launch, noMutation);
		};
		stationaryMonsterTarget("same-faction-monster-pass", false, false, false, false, false, false);
		stationaryMonsterTarget("opposing-player-minion-target", true, false, false, false, false, false);
		stationaryMonsterTarget("source-berserk-allows-same-faction-hit", false, true, false, false, false, false);
		stationaryMonsterTarget("target-berserk-allows-same-faction-hit", false, false, false, true, false, false);
		stationaryMonsterTarget("petrified-negative-monster-occupancy-hit", true, false, false, false, true, false);
		stationaryMonsterTarget("moving-negative-nonpetrified-monster-ignored", false, false, false, false, false, true);

		auto monsterImpactBoundary = [&](std::string_view caseName, int initialHitPointsQ6) {
			auto prepare = [=](Player &, Monster &source, auto &spawn, auto &, auto &, auto &cells) {
				source.flags |= MFLAG_GOLEM;
				source.golemToHit = 80;
				source.minDamage = source.maxDamage = 1;
				ASSERT_TRUE(source.isPlayerMinion());
				Monster *target = spawn(MT_TSKELBW, { 50, 50 });
				ASSERT_NE(target, nullptr);
				ASSERT_FALSE(target->isPlayerMinion());
				target->armorClass = 0;
				target->hitPoints = target->maxHitPoints = initialHitPointsQ6;
				target->whoHit = 1; // Prepared prior player participation exercises native death credit.
				target->rndItemSeed = findMonsterLootSeed(*target);
				ASSERT_NE(target->rndItemSeed, UINT32_MAX) << "The lethal capture needs a deterministic nonempty native loot selection.";
				ASSERT_EQ(tileAt({ 50, 50 }).monster(), static_cast<int>(target->getId()) + 1);
				AddArrowSampleCell(cells, { 50, 50 });
			};
			auto recordInput = [](int tick, Player &, Monster &, auto &, auto &, auto &slots, auto &, JsonObject &inputs) {
				if (tick != 0) {
					inputs.String("kind", "none");
					return;
				}
				const Monster &target = Monsters[slots.back()];
				inputs.String("kind", "prepared-q6-monster-life-boundary");
				inputs.Number("targetSlot", target.getId());
				inputs.Number("targetHitPointsQ6", target.hitPoints);
				inputs.Number("sourceMinimumDamage", Monsters[slots.front()].minDamage);
				inputs.Number("sourceMaximumDamage", Monsters[slots.front()].maxDamage);
				inputs.Boolean("nativeArrowDamageIsAlreadyQ6", false);
				inputs.Number("expectedDamageQ6", 64);
				inputs.Number("expectedResidualHitPointsQ6", target.hitPoints - 64);
			};
			runCase(caseName, "native-monstertrap-hit-q6-death-boundary-and-loot-pool", HeroClass::Warrior, 2, { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, prepare, seedForMonsterHit, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true); }, recordInput);
		};
		monsterImpactBoundary("monster-impact-remaining-life-64", 128);
		monsterImpactBoundary("monster-impact-lethal-fractional-63", 127);

		auto wallOnlyPrepare = [](Player &, Monster &, auto &, auto &, auto &, auto &cells) {
			tileAt({ 50, 50 }).setPiece(3);
			tileProperties()[3] = TileProperties::BlockMissile;
			ASSERT_TRUE(IsMissileBlockedByTile({ 50, 50 }));
			AddArrowSampleCell(cells, { 50, 50 });
		};
		auto blockerPrepare = [](Player &hero, Monster &, auto &, auto &, auto &, auto &cells) {
			hero.position.tile = { 50, 50 };
			hero.position.future = hero.position.tile;
			hero.position.old = hero.position.tile;
			hero.occupyTile(hero.position.tile, false);
			tileAt({ 50, 50 }).setPiece(3);
			tileProperties()[3] = TileProperties::BlockMissile;
			ASSERT_TRUE(IsMissileBlockedByTile({ 50, 50 }));
			ASSERT_GT(tileAt({ 50, 50 }).player(), 0);
			AddArrowSampleCell(cells, { 50, 50 });
		};
		runCase("tile-missile-blocker", "native-arrow-tile-blocker", HeroClass::Warrior, 2,
		    { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, wallOnlyPrepare, fixedSeed, emitOneStationary, lowDamage);
		runCase("player-hit-before-same-cell-tile-blocker", "native-monster-player-object-check-order", HeroClass::Warrior, 2,
		    { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, blockerPrepare, hitSeed, emitOneStationary, lowDamage);
		runCase("player-hit-before-same-cell-breakable-crux", "native-monster-player-crux-check-order", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &, auto &, auto &, auto &, auto &cells) {
				hero.position.tile = { 50, 50 }; hero.position.future = hero.position.tile; hero.position.old = hero.position.tile; hero.occupyTile(hero.position.tile, false);
				ActiveObjectCount = 2; ActiveObjects[0] = 0; ActiveObjects[1] = 1;
				for (int slot = 0; slot < 2; ++slot) {
					Objects[slot] = {}; Objects[slot]._otype = OBJ_CRUX1; Objects[slot]._oBreak = 1; Objects[slot]._oMissFlag = false;
					Objects[slot]._oSolidFlag = true; Objects[slot].selectionRegion = SelectionRegion::Bottom; Objects[slot]._oVar8 = 77;
					Objects[slot].position = slot == 0 ? Point { 50, 50 } : Point { 70, 70 };
					tileAt(Objects[slot].position).setObject(static_cast<int8_t>(slot + 1));
				}
				AddArrowSampleCell(cells, { 50, 50 }); AddArrowSampleCell(cells, { 70, 70 }); }, hitSeed, emitOneStationary, lowDamage);

		auto sourceStatMutation = [](int tick, Player &, Monster &source, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				source.minDamage = 9;
				source.maxDamage = 13;
				inputs.String("kind", "source-stat-change-before-impact");
				inputs.Number("minimumDamageInput", 9);
				inputs.Number("maximumDamageInput", 13);
			} else
				inputs.String("kind", "none");
		};
		runCase("source-live-damage-changed-before-impact", "native-processarrow-reads-current-source-slot-damage",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, sourceStatMutation);
		auto removeSourceMutation = [](int tick, Player &, Monster &source, auto &, auto &, auto &, auto &cells, JsonObject &inputs) {
			if (tick != 0) {
				inputs.String("kind", "none");
				return;
			}
			const size_t slot = source.getId();
			const auto active = std::find(ActiveMonsters, ActiveMonsters + ActiveMonsterCount, static_cast<unsigned>(slot));
			ASSERT_NE(active, ActiveMonsters + ActiveMonsterCount);
			const size_t index = static_cast<size_t>(std::distance(ActiveMonsters, active));
			tileAt(source.position.tile).setMonster(0);
			DeleteMonsterAtActiveIndex(index);
			inputs.String("kind", "remove-source-from-active-pool-retain-slot-state");
			inputs.Number("sourceSlotInput", slot);
			AddArrowSampleCell(cells, source.position.tile);
		};
		runCase("source-removed-before-impact", "native-arrow-reads-retained-source-slot-after-active-removal",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, removeSourceMutation);
		auto sourceDeadMutation = [](int tick, Player &, Monster &source, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				source.hitPoints = 0;
				source.mode = MonsterMode::Death;
				inputs.String("kind", "source-dead-but-slot-retained");
				inputs.Number("hitPointsInputQ6", 0);
				inputs.Number("modeInput", source.mode);
			} else
				inputs.String("kind", "none");
		};
		runCase("source-dead-before-impact", "native-arrow-reads-dead-source-slot-state",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, sourceDeadMutation);
		runCase("final-lifetime-tick-collision", "native-processarrow-collides-before-duration-expiry-deletion", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				emitArrow(out, { 50, 50 }, { 51, 50 }, Direction::East, true);
				Missiles.back().duration = 1; }, lowDamage);

		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	ASSERT_TRUE(!stream.fail());
	EXPECT_EQ(caseCount, 48);
	EXPECT_LE(std::filesystem::file_size(root / "native-catacombs-arrow-flight.json"), 32U * 1024U * 1024U);
}

