// Archive-free actual OperateTrap, AddMissile and ProcessMissiles player/monster collision captures.
TEST_F(ThemeRoomGenerationTest, CaptureNativeObjectTrapOperations)
{
	const char *directory = std::getenv("GODEVILUTION_OBJECT_TRAP_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the object trap oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool network = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		std::list<Missile> missiles = std::move(Missiles);
		bool preFlag = MissilePreFlag;
		ScopedMissileDataForTest data;
		~ExecutionGuard() {
			Missiles = std::move(missiles);
			MissilePreFlag = preFlag;
			if (network) SNetDestroy();
		}
	} guard;
	ASSERT_TRUE(guard.network);
	LoadPlayerDataFiles();
	LoadMissileData();
	std::ofstream stream(root / "native-object-traps.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	{
		JsonObject document(stream);
		document.String("format", "godevilution-native-object-traps");
		document.Number("version", 1);
		document.Key("wallCases");
		stream << '[';
		bool first = true;
		for (int depth = 1; depth <= 8; ++depth) {
			for (int triggerType : { 1, 2, 5, 6, 7, 42, 43, 48, 68, 69, 70 }) {
				if (depth <= 4 ? triggerType >= 42 && triggerType != 48 : triggerType <= 2 || triggerType == 48) continue;
				for (int type : { 0, 1, 7 }) {
					for (bool opened : { false, true }) {
						PrepareRoom(depth, 0, 2, 0, false);
						currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth),
							.type = depth <= 4 ? DTYPE_CATHEDRAL : DTYPE_CATACOMBS });
						Object &trigger = Objects[0];
						trigger = {};
						trigger._otype = static_cast<_object_id>(triggerType);
						trigger.position = { 50, 50 };
						trigger._oDoorFlag = triggerType == 1 || triggerType == 2 || triggerType == 42 || triggerType == 43;
						trigger.selectionRegion = static_cast<SelectionRegion>(opened ? 0 : 3);
						trigger._oTrapFlag = true;
						trigger._oVar4 = opened ? 1 : 0;
						tileAt(trigger.position).setObject(1);
						Object &trap = Objects[1];
						trap = {};
						trap._otype = OBJ_TRAPL;
						trap.position = { 48, 50 };
						trap._oVar1 = 50;
						trap._oVar2 = 50;
						trap._oVar3 = type;
						tileAt(trap.position).setObject(2);
						MyPlayer->position.tile = { 50, 51 };
						MyPlayer->life.current = MyPlayer->life.base = 6400;
						for (Tile &tile : tiles()) tile.setPlayer(0);
						tileAt(MyPlayer->position.tile).setPlayer(1);
						Missiles.clear();
						SetRndSeed(2588);
						if (!first) stream << ',';
						first = false;
						JsonObject row(stream);
						row.Number("depth", depth);
						row.Number("triggerType", trigger._otype);
						row.Number("missileType", type);
						row.Boolean("opened", opened);
						row.Boolean("ready", UpdateTrapState(trap));
						trap._oVar4 = 0;
						trigger._oTrapFlag = true;
						OperateTrap(trap);
						row.Number("var4", trap._oVar4);
						row.Boolean("trapFlag", trigger._oTrapFlag);
						row.Number("rng", GetLCGEngineState());
						row.Key("missiles");
						WriteArray(stream, Missiles, [&](const Missile &missile) { WriteRangedMissile(stream, missile); });
						const auto random = GetLCGEngineState();
						const auto count = Missiles.size();
						OperateTrap(trap);
						EXPECT_EQ(GetLCGEngineState(), random);
						EXPECT_EQ(Missiles.size(), count);
					}
				}
			}
		}
		stream << ']';
		document.Key("flightCases");
		stream << '[';
		first = true;
		for (int depth : { 1, 4, 5, 8 }) {
			for (int type : { 0, 1, 7, 27 }) {
				PrepareRoom(depth, 0, 0, 0, false);
				currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth),
					.type = depth <= 4 ? DTYPE_CATHEDRAL : DTYPE_CATACOMBS });
				MyPlayer->position.tile = { 60, 60 };
				MyPlayer->_pInvincible = true;
				for (Tile &tile : tiles()) tile.setPlayer(0);
				std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
				tileProperties()[2] = TileProperties::BlockMissile;
				tileAt(48, 50).setPiece(2);
				tileAt(52, 50).setPiece(2);
				Missiles.clear();
				SetRndSeed(2588);
				AddMissile({ 48, 50 }, { 56, 50 }, GetDirection({ 48, 50 }, { 56, 50 }),
					static_cast<MissileID>(type), TARGET_PLAYERS, -1, 0, 0);
				if (!first) stream << ',';
				first = false;
				JsonObject row(stream);
				row.Number("depth", depth);
				row.Number("type", type);
				row.Number("initialRng", GetLCGEngineState());
				row.Key("initialMissiles");
				WriteArray(stream, Missiles, [&](const Missile &missile) { WriteRangedMissile(stream, missile); });
				row.Key("ticks");
				stream << '[';
				for (int tick = 1; tick <= 24; ++tick) {
					ProcessMissiles();
					if (tick != 1) stream << ',';
					JsonObject step(stream);
					step.Number("tick", tick);
					step.Number("rng", GetLCGEngineState());
					step.Key("missiles");
					// Native erases _miDelFlag at the next pass; the managed pool drops it immediately.
					std::vector<const Missile *> live;
					for (const Missile &missile : Missiles) if (!missile._miDelFlag) live.push_back(&missile);
					WriteArray(stream, live, [&](const Missile *missile) { WriteRangedMissile(stream, *missile); });
				}
				stream << ']';
			}
		}
		stream << ']';
		// Real ProcessMissiles collisions, including Lightning's shifted Q6 path.
		document.Key("impactCases");
		stream << '[';
		first = true;
		for (int depth : { 1, 8 }) {
			for (int type : { 0, 1, 7, 27 }) {
				for (int target : { 0, 1, 2 }) { // monster, player, shield player
					for (int resistance : { 0, 50 }) {
						Monster *actor = PrepareCatacombsActor(depth, MT_NZOMBIE, nullptr);
						ASSERT_NE(actor, nullptr);
						currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth),
							.type = depth <= 4 ? DTYPE_CATHEDRAL : DTYPE_CATACOMBS });
						actor->hitPoints = actor->maxHitPoints = 6400;
						actor->armorClass = 0;
						actor->resistance = resistance == 0 ? 0 : RESIST_FIRE | RESIST_LIGHTNING;
						MyPlayer->position.tile = MyPlayer->position.future = MyPlayer->position.old = target == 0 ? Point { 60, 60 } : Point { 50, 50 };
						MyPlayer->life.current = MyPlayer->life.base = MyPlayer->life.maximum = MyPlayer->life.maximumBase = 6400;
						MyPlayer->attributes.dexterity.base = MyPlayer->attributes.dexterity.current = 100;
						MyPlayer->_pFireResist = MyPlayer->_pLghtResist = resistance;
						MyPlayer->pLvlLoad = 0; // Normal gameplay: native hit sounds still draw RNG in headless mode.
						MyPlayer->_pBlockFlag = target == 2;
						MyPlayer->_pmode = PM_STAND;
						for (Tile &tile : tiles()) { tile.setPlayer(0); tile.setMonster(0); }
						tileAt(MyPlayer->position.tile).setPlayer(1);
						if (target == 0) tileAt(actor->position.tile).setMonster(actor->getId() + 1);
						Missiles.clear();
						const uint32_t seed = target == 2 && type == 7 ? 2588 : 0;
						SetRndSeed(seed);
						AddMissile({ 48, 50 }, { 56, 50 }, GetDirection({ 48, 50 }, { 56, 50 }),
							static_cast<MissileID>(type), TARGET_PLAYERS, -1, 0, 0);
						if (!first) stream << ',';
						first = false;
						JsonObject row(stream);
						row.Number("depth", depth); row.Number("type", type);
						row.Number("target", target); row.Number("resistance", resistance); row.Number("seed", seed);
						row.Number("slot", actor->getId()); row.Number("armor", MyPlayer->GetArmor());
						row.Key("ticks"); stream << '[';
						for (int tick = 1; tick <= 12; ++tick) {
							ProcessMissiles();
							if (tick != 1) stream << ',';
							JsonObject step(stream);
							step.Number("tick", tick); step.Number("rng", GetLCGEngineState());
							step.Number("life", MyPlayer->life.current); step.Number("monsterHp", actor->hitPoints);
							step.Boolean("blocked", MyPlayer->_pmode == PM_BLOCK);
						}
						stream << ']';
					}
				}
			}
		}
		stream << ']';
		document.Key("playerHits");
		stream << '[';
		first = true;
		for (int depth = 1; depth <= 8; ++depth) {
			for (int type : { 0, 1, 7, 27 }) {
				for (int resistance : { 0, 50 }) {
					PrepareRoom(depth, 0, 0, 0, false);
					currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth),
						.type = depth <= 4 ? DTYPE_CATHEDRAL : DTYPE_CATACOMBS });
					MyPlayer->_pmode = PM_STAND;
					MyPlayer->_pInvincible = false;
					MyPlayer->_pSpellFlags = SpellFlag::None;
					MyPlayer->_pIFlags = ItemSpecialEffect::HalfTrapDamage;
					MyPlayer->_pIGetHit = 1;
					MyPlayer->_pBlockFlag = false;
					MyPlayer->_pFireResist = resistance;
					MyPlayer->_pLghtResist = resistance;
					MyPlayer->life.current = MyPlayer->life.base = MyPlayer->life.maximum = MyPlayer->life.maximumBase = 6400;
					MyPlayer->mana.current = MyPlayer->mana.base = MyPlayer->mana.maximum = MyPlayer->mana.maximumBase = 0;
					const DamageType damageType = type == 0 ? DamageType::Physical : type == 7 ? DamageType::Lightning : DamageType::Fire;
					if (!first) stream << ',';
					first = false;
					JsonObject row(stream);
					row.Number("depth", depth);
					row.Number("type", type);
					row.Number("resistance", resistance);
					row.Number("armor", MyPlayer->GetArmor());
					row.Number("minimum", depth);
					row.Number("maximum", 2 * depth);
					SetRndSeed(0);
					bool blocked = false;
					row.Boolean("hit", PlayerMHit(*MyPlayer, nullptr, 1, depth, 2 * depth,
						static_cast<MissileID>(type), damageType, false, DeathReason::MonsterOrTrap, &blocked));
					row.Number("life", MyPlayer->life.current);
					row.Number("rng", GetLCGEngineState());
				}
			}
		}
		stream << ']';
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
}
