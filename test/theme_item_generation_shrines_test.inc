// Archive-free actual OperateObject -> OperateShrine, including peer-local multiplayer effects.
TEST_F(ThemeRoomGenerationTest, CaptureNativeVanillaShrines)
{
    const char *directory = std::getenv("GODEVILUTION_SHRINE_ORACLE_DIRECTORY");
    if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the shrine oracle directory.";
    const std::filesystem::path root = std::filesystem::absolute(directory);
    ASSERT_TRUE(std::filesystem::is_directory(root));
    ASSERT_TRUE(std::filesystem::is_empty(root));
    struct ExecutionGuard {
        bool headless = HeadlessMode;
        bool network = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
        std::list<Missile> missiles = std::move(Missiles);
        ScopedMissileDataForTest data;
        ~ExecutionGuard() {
            Missiles = std::move(missiles);
            HeadlessMode = headless;
            ClrDiabloMsg();
            if (network) SNetDestroy();
        }
    } guard;
    ASSERT_TRUE(guard.network);
    HeadlessMode = true;
    LoadPlayerDataFiles();
    LoadMissileData();
    RegisterObjectBehaviors();
    Players.resize(3);
    MyPlayer = &Players[0];
    auto writeItem = [](std::ostream &out, const Item &item) {
        JsonObject json(out);
        json.Number("type", item._itype);
        json.Number("index", item.IDidx);
        json.Number("seed", item._iSeed);
        json.Number("createInfo", item._iCreateInfo);
        json.Number("misc", item._iMiscId);
        json.Number("spell", item._iSpell);
        json.Number("minDamage", item._iMinDam);
        json.Number("maxDamage", item._iMaxDam);
        json.Number("armor", item._iAC);
        json.Number("durability", item._iDurability);
        json.Number("maxDurability", item._iMaxDur);
        json.Number("charges", item._iCharges);
        json.Number("maxCharges", item._iMaxCharges);
        json.Number("value", item._ivalue);
        json.Number("minimumMagic", item._iMinMag);
        json.Boolean("identified", item._iIdentified);
        json.Boolean("statFlag", item._iStatFlag);
        json.Number("quality", item._iMagical);
        json.Number("plusMana", item._iPLMana);
    };
    auto writePlayer = [&](std::ostream &out, const Player &player) {
        JsonObject json(out);
        json.Boolean("dead", player._pmode == PM_DEATH);
        json.Number("strength", player.attributes.strength.base);
        json.Number("magic", player.attributes.magic.base);
        json.Number("dexterity", player.attributes.dexterity.base);
        json.Number("vitality", player.attributes.vitality.base);
        json.Number("currentStrength", player.attributes.strength.current);
        json.Number("currentMagic", player.attributes.magic.current);
        json.Number("currentDexterity", player.attributes.dexterity.current);
        json.Number("currentVitality", player.attributes.vitality.current);
        json.Number("life", player.life.current);
        json.Number("lifeMaximum", player.life.maximum);
        json.Number("lifeBaseMaximum", player.life.maximumBase);
        json.Number("mana", player.mana.current);
        json.Number("manaMaximum", player.mana.maximum);
        json.Number("manaBaseMaximum", player.mana.maximumBase);
        json.Number("lifeBase", player.life.base);
        json.Number("manaBase", player.mana.base);
        json.Boolean("manaShield", player.pManaShield);
        json.Number("gold", player._pGold);
        json.Number("spells", player._pMemSpells);
        json.Key("spellLevels");
        WriteArray(out, player._pSplLvl, [&](auto level) { out << static_cast<int>(level); });
        json.Key("equipment");
        WriteArray(out, player.InvBody, [&](const Item &item) { writeItem(out, item); });
        json.Key("inventory"); out << '[';
        for (int i = 0; i < player._pNumInv; ++i) { if (i) out << ','; writeItem(out, player.InvList[i]); }
        out << ']';
        json.Key("belt");
        WriteArray(out, player.SpdList, [&](const Item &item) { writeItem(out, item); });
        json.Key("grid");
        WriteArray(out, player.InvGrid, [&](auto cell) { out << static_cast<int>(cell); });
    };
    auto initializeItem = [](Item &item, ItemType type, uint32_t seed) {
        const auto row = std::find_if(AllItemsList.begin(), AllItemsList.end(), [&](const auto &value) {
            return value.itype == type;
        });
        ASSERT_NE(row, AllItemsList.end());
        InitializeItem(item, static_cast<_item_indexes>(std::distance(AllItemsList.begin(), row)));
        item._iSeed = seed;
        item._iMinStr = item._iMinMag = item._iMinDex = 0;
        item._iStatFlag = true;
        item._iMinDam = 2; item._iMaxDam = 9; item._iAC = 5;
        item._iDurability = 15; item._iMaxDur = 40;
        item._iCharges = 2; item._iMaxCharges = 10;
        item._iMagical = ITEM_QUALITY_MAGIC; item._iIdentified = false;
    };
    std::ofstream stream(root / "native-vanilla-shrines.json", std::ios::binary | std::ios::trunc);
    ASSERT_TRUE(stream.good());
    {
        JsonObject document(stream);
        document.String("format", "godevilution-native-vanilla-shrines");
        document.Number("version", 1);
        document.Key("catalog"); WriteCatalog(stream);
        document.Key("cases"); stream << '[';
        int cases = 0;
        for (int depth : { 1, 4, 5, 8 }) {
            for (bool multiplayer : { false, true }) {
                for (int type = 0; type < 26; ++type) {
                    const auto availability = multiplayer ? MultiplayerShrineAvailabilityForTesting() : SinglePlayerShrineAvailabilityForTesting();
                    if (!availability[type]) continue;
                    const bool deadPeerVariant = multiplayer && (type == 18 || type == 25);
                    const int variants = depth == 1 && type == 7 && !multiplayer ? 4
                        : deadPeerVariant || (depth == 1 && (type == 13 || type == 17) && !multiplayer) ? 2 : 1;
                    for (int variant = 0; variant < variants; ++variant) {
                    for (int peer = 0; peer < (multiplayer ? 3 : 1); ++peer) {
                        MyPlayer = &Players[peer];
                        MyPlayerId = static_cast<uint8_t>(peer);
                        PrepareRoom(depth, 0, 3, 0, false);
                        currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth),
                            .type = depth <= 4 ? DTYPE_CATHEDRAL : DTYPE_CATACOMBS });
                        gbIsMultiplayer = multiplayer;
                        Missiles.clear();
                        ClrDiabloMsg();
                        for (Tile &tile : tiles()) { tile.setPiece(0); tile.setPlayer(0); tile.setMonster(0); tile.setObject(0); }
                        for (int playerIndex = 0; playerIndex < 3; ++playerIndex) {
                            Player &player = Players[playerIndex];
                            player = {};
                            player._pClass = HeroClass::Warrior;
                            player.setAnimations();
                            player.setCharacterLevel(10);
                            player.plractive = true; player.plrlevel = static_cast<uint8_t>(depth);
                            player.position.tile = { static_cast<uint8_t>(50 + playerIndex), 51 };
                            player.position.future = player.position.tile;
                            player.direction = Direction::South;
                            player.attributes.strength.base = player.attributes.strength.current = 50;
                            player.attributes.magic.base = player.attributes.magic.current = 40;
                            player.attributes.dexterity.base = player.attributes.dexterity.current = 45;
                            player.attributes.vitality.base = player.attributes.vitality.current = 60;
                            player.life.maximumBase = player.life.maximum = 6400;
                            player.life.base = player.life.current = 3200;
                            player.mana.maximumBase = player.mana.maximum = 6400;
                            player.mana.base = player.mana.current = 0;
                            player._pMemSpells = (UINT64_C(1) << 0) | (UINT64_C(1) << 29) | (UINT64_C(1) << 30);
                            player._pSplLvl[1] = 1; player._pSplLvl[30] = 5; player._pSplLvl[31] = 15;
                            const ItemType equipment[] = { ItemType::Helm, ItemType::Ring, ItemType::Ring,
                                ItemType::Amulet, ItemType::Sword, ItemType::Shield, ItemType::LightArmor };
                            for (int slot = 0; slot < NUM_INVLOC; ++slot)
                                initializeItem(player.InvBody[slot], equipment[slot], 100 + slot);
                            player.InvBody[1]._iDurability = player.InvBody[1]._iMaxDur = DUR_INDESTRUCTIBLE;
                            player.InvBody[2]._iDurability = player.InvBody[2]._iMaxDur = 0;
                            player._pNumInv = 5;
                            initializeItem(player.InvList[0], ItemType::Staff, 200);
                            for (int slot = 1; slot < 5; ++slot) {
                                const item_misc_id misc[] = { IMISC_HEAL, IMISC_MANA, IMISC_FULLHEAL, IMISC_FULLMANA };
                                InitializeItem(player.InvList[slot], ItemMiscIdIdx(misc[slot - 1]));
                                player.InvList[slot]._iSeed = 200 + slot;
                                player.InvList[slot]._iStatFlag = true;
                            }
                            for (int slot = 0; slot < 5; ++slot) player.InvGrid[slot] = static_cast<int8_t>(slot + 1);
                            InitializeItem(player.SpdList[0], ItemMiscIdIdx(IMISC_HEAL));
                            player.SpdList[0]._iSeed = 300;
                            player.SpdList[0]._iStatFlag = true;
                            tileAt(player.position.tile).setPlayer(playerIndex + 1);
                        }
                        if (variant == 1 && type == 13) {
                            Player &player = Players[0];
                            player.attributes.magic.base = player.attributes.magic.current = 10;
                            player.InvBody[1]._iPLMana = -640;
                            player.InvBody[1]._iIdentified = true;
                            player.mana.maximum = player.mana.maximumBase = player.mana.base = 0;
                            player.mana.current = -640;
                            player.InvList[0]._iMinMag = 12;
                            player.InvList[0]._iStatFlag = false;
                        }
                        if (variant == 1 && type == 7) {
                            Player &player = Players[0];
                            player._pMemSpells |= UINT64_C(1) << 35;
                            player._pSplLvl[36] = 3;
                            player._pNumInv = 6;
                            InitializeItem(player.InvList[5], ItemMiscIdIdx(IMISC_BOOK));
                            player.InvList[5]._iSeed = 205;
                            player.InvList[5]._iSpell = SpellID::BoneSpirit;
                            player.InvList[5].updateRequiredStatsCacheForPlayer(player);
                            player.InvGrid[5] = 6;
                        }
                        if (type == 7 && variant >= 2) {
                            Player &player = Players[0];
                            player._pMemSpells = (UINT64_C(1) << 37) | (UINT64_C(1) << 40);
                            std::fill(std::begin(player._pSplLvl), std::end(player._pSplLvl), 0);
                            player._pSplLvl[38] = 5; player._pSplLvl[41] = 6;
                            if (variant == 3) player._pMemSpells |= UINT64_C(1);
                        }
                        if (deadPeerVariant && variant == 1) {
                            Players[1]._pmode = PM_DEATH;
                            Players[1].life.current = Players[1].life.base = 0;
                            Players[1].mana.current = Players[1].mana.base = 1280;
                        }
                        // Active order deliberately differs from numeric slot order.
                        ActiveObjects[0] = 2; ActiveObjects[1] = 0; ActiveObjects[2] = 1;
                        Object &shrine = Objects[2]; shrine = {};
                        shrine._otype = depth % 2 == 0 ? OBJ_SHRINER : OBJ_SHRINEL;
                        shrine.position = { 50, 50 }; shrine._oRndSeed = type % 2 == 0 ? 2588 : 743271966;
                        shrine._oVar1 = type; shrine.selectionRegion = SelectionRegion::Bottom;
                        shrine._oAnimFrame = 1; shrine._oAnimLen = 11;
                        tileAt(shrine.position).setObject(3);
                        for (int slot = 0; slot < 2; ++slot) {
                            Object &chest = Objects[slot]; chest = {};
                            chest._otype = static_cast<_object_id>(OBJ_CHEST1 + slot);
                            chest.position = { 55 + slot, 50 }; chest._oRndSeed = 800 + slot;
                            chest._oAnimFrame = 3; chest.selectionRegion = SelectionRegion::None;
                            tileAt(chest.position).setObject(slot + 1);
                        }
                        for (int x = 0; x < DMAXX; ++x) for (int y = 0; y < DMAXY; ++y)
                            CurrentAutomapManager.GetAutomapView()[x][y] = MAP_EXP_NONE;
                        const uint32_t globalSeed = depth == 1 && type == 17 && variant == 1 && !multiplayer ? 3135529114U : 1234U;
                        SetRndSeed(globalSeed);
                        if (cases++) stream << ',';
                        JsonObject row(stream);
                        row.Number("depth", depth); row.Number("type", type);
                        row.Boolean("multiplayer", multiplayer); row.Number("peer", peer);
                        row.Number("seed", shrine._oRndSeed);
                        row.Number("globalSeed", globalSeed);
                        row.Key("input"); writePlayer(stream, Players[0]);
                        if (deadPeerVariant && variant == 1) { row.Key("deadPeerInput"); writePlayer(stream, Players[1]); }
                        OperateObject(Players[0], shrine);
                        row.Key("expected"); writePlayer(stream, *MyPlayer);
                        row.Boolean("message", IsDiabloMsgAvailable());
                        row.String("messageText", CurrentDiabloMsgForTesting());
                        row.Number("rng", GetLCGEngineState());
                        row.Number("selection", shrine.selectionRegion);
                        EXPECT_EQ(shrine.selectionRegion, SelectionRegion::None) << "shrine=" << type;
                        row.Key("chestSeeds"); stream << '[' << Objects[0]._oRndSeed << ',' << Objects[1]._oRndSeed << ']';
                        row.Key("chestFrames"); stream << '[' << Objects[0]._oAnimFrame << ',' << Objects[1]._oAnimFrame << ']';
                        row.Number("groundCount", ActiveItemCount);
                        row.Key("ground"); stream << '[';
                        for (int index = 0; index < ActiveItemCount; ++index) {
                            if (index) stream << ','; writeItem(stream, Items[ActiveItems[index]]);
                        }
                        stream << ']';
                        int explored = 0;
                        for (int x = 0; x < DMAXX; ++x) for (int y = 0; y < DMAXY; ++y)
                            if (CurrentAutomapManager.GetAutomapView()[x][y] == MAP_EXP_SHRINE) ++explored;
                        row.Number("explored", explored);
                        row.Number("missiles", Missiles.size());
                        row.Key("missileStates"); stream << '[';
                        bool firstMissile = true;
                        for (const Missile &missile : Missiles) {
                            if (missile._miDelFlag) continue;
                            if (!firstMissile) stream << ',';
                            firstMissile = false;
                            WriteRangedMissile(stream, missile);
                        }
                        stream << ']';
                        if (type == 7 && variant == 3) {
                            Player &target = Players[0];
                            EXPECT_EQ(CheckSpell(target, SpellID::Firebolt, SpellType::Spell, true), SpellCheckResult::Fail_Level0);
                            const auto mana = target.mana;
                            target.mana.current = 6400; target._pISplLvlAdd = 1;
                            EXPECT_EQ(CheckSpell(target, SpellID::Firebolt, SpellType::Spell, true), SpellCheckResult::Success);
                            target.mana = mana; target._pISplLvlAdd = 0;
                        }
                        if (deadPeerVariant && variant == 1 && peer == 1) {
                            Player &target = Players[1];
                            const auto life = target.life;
                            const auto mana = target.mana;
                            // The remote-player path runs the same resource assignments without local death-menu UI.
                            MyPlayer = &Players[0]; MyPlayerId = 0;
                            ApplyResurrect(target);
                            EXPECT_EQ(target.life.current, std::min(10 << 6, target.life.maximumBase));
                            EXPECT_EQ(target.life.base, target.life.current + target.life.maximumBase - target.life.maximum);
                            EXPECT_EQ(target.mana.current, 0);
                            EXPECT_EQ(target.mana.base, target.mana.maximumBase - target.mana.maximum);
                            row.Key("afterResurrect"); writePlayer(stream, target);
                            target.life = life; target.mana = mana; target._pmode = PM_DEATH;
                            RestartTownLvl(target);
                            EXPECT_EQ(target.life.current, 64);
                            EXPECT_EQ(target.life.base, 64 + target.life.maximumBase - target.life.maximum);
                            EXPECT_EQ(target.mana.current, 0);
                            EXPECT_EQ(target.mana.base, target.mana.maximumBase - target.mana.maximum);
                            row.Key("afterRestart"); writePlayer(stream, target);
                        }
                    }
                    }
                }
            }
        }
        stream << ']'; document.Number("caseCount", cases);
    }
    stream.flush();
    ASSERT_TRUE(stream.good());
    EXPECT_LT(std::filesystem::file_size(root / "native-vanilla-shrines.json"), 8U * 1024U * 1024U);
}
