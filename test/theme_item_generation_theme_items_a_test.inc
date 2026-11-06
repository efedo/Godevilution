// Part A: archive-free calls to the real native object dispatcher and item generator.
TEST_F(ThemeRoomGenerationTest, CaptureNativeThemeItemObjectsPartA)
{
    const char *directory = std::getenv("GODEVILUTION_THEME_ITEM_OBJECTS_A_ORACLE_DIRECTORY");
    if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the part A item-object oracle directory.";
    const std::filesystem::path root = std::filesystem::absolute(directory);
    ASSERT_TRUE(std::filesystem::is_directory(root));
    ASSERT_TRUE(std::filesystem::is_empty(root));
    struct NetworkGuard {
        bool started = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
        ~NetworkGuard() { if (started) SNetDestroy(); }
    } guard;
    ASSERT_TRUE(guard.started);
    RegisterObjectBehaviors();
    std::ofstream stream(root / "native-theme-item-objects-a.json", std::ios::binary | std::ios::trunc);
    ASSERT_TRUE(stream.good());
    int cases = 0;
    {
        JsonObject document(stream);
        document.String("format", "godevilution-theme-item-objects-a");
        document.Number("version", 1);
        document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
        document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
        document.Key("catalog"); WriteCatalog(stream);
        document.Key("emptyItem"); WriteItem(stream, Item {});
        document.Key("cases"); stream << '[';
        for (bool multiplayer : { false, true }) {
            gbIsMultiplayer = multiplayer;
            for (int depth = 1; depth <= 8; ++depth) {
                for (int type : { 61, 62, 63, 64, 67, 77, 89, 90, 92 }) {
                    for (uint32_t seed : { 0U, 1U, 2588U, UINT32_C(0x80000000), UINT32_MAX, UINT32_C(0x12345678) }) {
                        // Capacity, placement exhaustion and pre-consumed objects each get a lane.
                        for (int variant = 0; variant < 4; ++variant) {
                            PrepareRoom(depth, 0, 1, 0, false);
                            currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth),
                                .type = depth <= 4 ? DTYPE_CATHEDRAL : DTYPE_CATACOMBS });
                            MyPlayer->plrlevel = static_cast<uint8_t>(depth);
                            MyPlayer->plrIsOnSetLevel = false;
                            MyPlayer->position.tile = { 60, 60 };
                            for (int i = 0; i <= MAXITEMS; ++i) Items[i] = {};
                            for (int i = 0; i < MAXITEMS; ++i) ActiveItems[i] = static_cast<uint8_t>(i);
                            ActiveItemCount = variant == 1 ? MAXITEMS : 0;
                            if (variant == 2)
                                std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::Solid);
                            Object &object = Objects[0];
                            object._otype = static_cast<_object_id>(type);
                            object.position = { 50, 50 };
                            object._oSolidFlag = true;
                            object._oMissFlag = type != 62 && type != 63;
                            object.selectionRegion = variant == 3 ? SelectionRegion::None : SelectionRegion::Bottom;
                            object._oAnimFrame = type == 61 || type == 63 ? 4 : type == 62 ? 3 : type == 67 ? 8 : 1;
                            object._oRndSeed = seed;
                            tileAt(50, 50).setObject(1);
                            const auto initialFrame = object._oAnimFrame;
                            SetRndSeed(UINT32_C(0x12345678));
                            if (cases++ != 0) stream << ',';
                            JsonObject row(stream);
                            row.Number("depth", depth); row.Boolean("multiplayer", multiplayer);
                            row.Number("type", type); row.Number("seed", seed); row.Number("variant", variant);
                            row.Number("initialFrame", initialFrame);
                            OperateObject(*MyPlayer, object);
                            row.Number("frame", object._oAnimFrame);
                            row.Number("selection", object.selectionRegion);
                            row.Number("randomState", GetLCGEngineState());
                            row.Number("activeCount", ActiveItemCount);
                            row.Key("item"); WriteItem(stream, Items[0]);
                            row.Key("uniqueFlags"); WriteFlags(stream, CaptureFlags());
                            const auto after = GetLCGEngineState();
                            OperateObject(*MyPlayer, object);
                            row.Number("repeatedRandomState", GetLCGEngineState());
                            EXPECT_EQ(after, GetLCGEngineState());
                        }
                    }
                }
            }
        }
        stream << ']'; document.Number("caseCount", cases);
    }
    EXPECT_TRUE(stream.good());
    EXPECT_EQ(cases, 3456);
}
