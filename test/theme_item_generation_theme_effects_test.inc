// Archive-free actual native dispatch for 2.5c part B; no game archives are opened.
TEST_F(ThemeRoomGenerationTest, CaptureNativeThemeEffectsPartB)
{
    const char *directory = std::getenv("GODEVILUTION_THEME_EFFECTS_PART_B_ORACLE_DIRECTORY");
    if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the part B oracle directory.";
    const std::filesystem::path root = std::filesystem::absolute(directory);
    ASSERT_TRUE(std::filesystem::is_directory(root));
    ASSERT_TRUE(std::filesystem::is_empty(root));
    struct ExecutionGuard {
        bool headless = HeadlessMode;
        bool network = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
        std::list<Missile> missiles = std::move(Missiles);
        ScopedMissileDataForTest data;
        ~ExecutionGuard() {
            Missiles = std::move(missiles); HeadlessMode = headless;
            ClrDiabloMsg(); if (network) SNetDestroy();
        }
    } guard;
    ASSERT_TRUE(guard.network);
    HeadlessMode = true;
    LoadPlayerDataFiles(); LoadMissileData(); RegisterObjectBehaviors();
    Players.resize(2);
    auto writePlayer = [](std::ostream &out, const Player &player) {
        JsonObject json(out);
        json.Number("life", player.life.current); json.Number("lifeBase", player.life.base);
        json.Number("lifeMaximum", player.life.maximum); json.Number("lifeBaseMaximum", player.life.maximumBase);
        json.Number("mana", player.mana.current); json.Number("manaBase", player.mana.base);
        json.Number("manaMaximum", player.mana.maximum); json.Number("manaBaseMaximum", player.mana.maximumBase);
        json.Number("strength", player.attributes.strength.base); json.Number("magic", player.attributes.magic.base);
        json.Number("dexterity", player.attributes.dexterity.base); json.Number("vitality", player.attributes.vitality.base);
        json.Boolean("manaShield", player.pManaShield); json.Number("gold", player._pGold);
        json.Number("spells", player._pMemSpells);
        json.Key("spellLevels"); WriteArray(out, player._pSplLvl, [&](auto value) { out << static_cast<int>(value); });
        json.Key("goldPiles"); out << '[';
        for (int i = 0; i < player._pNumInv; ++i) { if (i) out << ','; out << player.InvList[i]._ivalue; }
        out << ']';
    };
    std::ofstream stream(root / "native-theme-effects-part-b.json", std::ios::binary | std::ios::trunc);
    ASSERT_TRUE(stream.good());
    {
        JsonObject document(stream);
        document.String("format", "godevilution-native-theme-effects-part-b"); document.Number("version", 1);
        document.Key("catalog"); WriteCatalog(stream);
        document.Key("cases"); stream << '[';
        int cases = 0;
        for (int depth : { 1, 4, 5, 8 }) {
            for (bool multiplayer : { false, true }) {
                for (int type : { 66, 76, 79, 80, 81, 82, 26, 91 }) {
                    std::vector<uint32_t> seeds { 0, 2588, 0xFFFFFFFFU };
                    if (type == 82) { seeds.clear(); for (uint32_t pair = 0; pair < 12; ++pair) seeds.push_back(pair << 16); }
                    if (type == 79 || type == 80) {
                        // Hit every eligible effect and retain seeds whose first draw is rejected.
                        std::array<bool, 26> found {};
                        for (uint32_t seed = 0; seed < 10000; ++seed) {
                            SetRndSeed(seed); const int selected = GenerateRnd(26);
                            if (!found[selected]) { found[selected] = true; seeds.push_back(seed); }
                            if (std::all_of(found.begin(), found.end(), [](bool value) { return value; })) break;
                        }
                    }
                    if (type == 66 || type == 76 || type == 81 || type == 26 || type == 91) seeds = { 0, 1, 2, 3 };
                    for (uint32_t seed : seeds) {
                        for (int peer = 0; peer < (multiplayer ? 2 : 1); ++peer) {
                            MyPlayer = &Players[peer]; MyPlayerId = static_cast<uint8_t>(peer);
                            PrepareRoom(depth, 0, 1, 0, false);
                            currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth),
                                .type = depth <= 4 ? DTYPE_CATHEDRAL : DTYPE_CATACOMBS });
                            gbIsMultiplayer = multiplayer; Missiles.clear(); ClrDiabloMsg();
                            for (Tile &tile : tiles()) { tile.setPiece(0); tile.setPlayer(0); tile.setMonster(0); tile.setObject(0); }
                            for (int index = 0; index < 2; ++index) {
                                Player &player = Players[index]; player = {};
                                player._pClass = HeroClass::Warrior; player.setAnimations(); player.setCharacterLevel(10);
                                player.plractive = true; player.plrlevel = static_cast<uint8_t>(depth);
                                player.position.tile = { static_cast<uint8_t>(50 + index), static_cast<uint8_t>(type == 26 || type == 91 ? 49 : 51) };
                                player.position.future = player.position.tile; player.direction = Direction::South;
                                player.attributes.strength.base = player.attributes.strength.current = 50;
                                player.attributes.magic.base = player.attributes.magic.current = 40;
                                player.attributes.dexterity.base = player.attributes.dexterity.current = 45;
                                player.attributes.vitality.base = player.attributes.vitality.current = 60;
                                player.life.maximumBase = player.life.maximum = 6400; player.life.base = player.life.current = 3200;
                                player.mana.maximumBase = player.mana.maximum = 6400; player.mana.base = player.mana.current = 0;
                                player._pMemSpells = UINT64_C(1); player._pSplLvl[1] = 1;
                                tileAt(player.position.tile).setPlayer(index + 1);
                            }
                            if (type == 66) Players[0].life.current = Players[0].life.base = seed == 1 ? 6400 : seed == 2 ? 6399 : 3200;
                            if (type == 76) Players[0].mana.current = Players[0].mana.base = seed == 1 ? 6400 : seed == 2 ? 6399 : 0;
                            if (type == 66 && seed == 3) { Players[0].life.current = 6336; Players[0].life.base = 6000; }
                            if (type == 76 && seed == 3) { Players[0].mana.current = 6336; Players[0].mana.base = 6000; }
                            if (type == 26 || type == 91) {
                                Players[0]._pFireResist = seed == 1 ? 75 : 0;
                                Players[0].pManaShield = seed == 2;
                                if (seed == 2) Players[0].mana.current = Players[0].mana.base = 70;
                                if (seed == 3) { Players[0]._pmode = PM_DEATH; Players[0].life.current = Players[0].life.base = 0; }
                            }
                            Object &object = Objects[0]; object = {};
                            ActiveObjects[0] = 0; ActiveObjectCount = 1;
                            object._otype = static_cast<_object_id>(type); object.position = { 50, 50 };
                            object._oRndSeed = seed; object._oAnimFrame = 1; object._oAnimLen = type == 80 ? 0U : 11U;
                            object.selectionRegion = type == 26 || type == 91 ? SelectionRegion::None : SelectionRegion::Bottom | SelectionRegion::Middle;
                            object._olid = NO_LIGHT; tileAt(object.position).setObject(1);
                            SetRndSeed(1234);
                            if (cases++) stream << ',';
                            JsonObject row(stream);
                            row.Number("depth", depth); row.Number("object", type); row.Number("seed", seed);
                            row.Boolean("multiplayer", multiplayer); row.Number("peer", peer);
                            row.Key("input"); writePlayer(stream, Players[0]);
                            if (type == 26 || type == 91) {
                                ProcessObjects(); ProcessMissiles();
                                ProcessObjects(); ProcessMissiles();
                            } else OperateObject(Players[0], object);
                            row.Key("expected"); writePlayer(stream, *MyPlayer);
                            row.Number("shrineType", object._oVar1); row.Number("rng", GetLCGEngineState());
                            row.Number("selection", object.selectionRegion); row.Number("frame", object._oAnimFrame);
                            row.String("message", CurrentDiabloMsgForTesting());
                            const auto liveMissile = std::find_if(Missiles.begin(), Missiles.end(), [](const Missile &missile) { return !missile._miDelFlag; });
                            row.Number("missiles", std::count_if(Missiles.begin(), Missiles.end(), [](const Missile &missile) { return !missile._miDelFlag; }));
                            row.Number("duration", liveMissile == Missiles.end() ? 0 : liveMissile->duration);
                            row.Number("groundCount", ActiveItemCount);
                            if (type == 66 || type == 76 || type == 81 || type == 82) {
                                OperateObject(Players[0], object);
                                row.Key("repeat"); writePlayer(stream, *MyPlayer);
                                row.Number("repeatSelection", object.selectionRegion);
                            }
                        }
                    }
                }
            }
        }
        stream << ']'; document.Number("caseCount", cases);
    }
    stream.flush(); ASSERT_TRUE(stream.good());
}
