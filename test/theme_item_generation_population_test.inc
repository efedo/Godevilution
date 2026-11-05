struct NativeComposedPopulationGuard {
	MonsterPopulationGlobalsForTesting oldMonsterGlobals = CaptureMonsterPopulationGlobalsForTesting();
	ItemInitializationGlobalsForTesting oldItemGlobals = CaptureItemInitializationGlobalsForTesting();
	std::unique_ptr<Level> oldLevel = std::make_unique<Level>(currentLevel().id());
	decltype(sgGameInitInfo) oldGameInfo = sgGameInitInfo;
	bool oldHellfireSave = gbIsHellfireSaveGame;
	bool oldPlayerIsDead = MyPlayerIsDead;
	std::vector<LevelGenerationRow> oldLevelData = LevelGenerationData;
	std::array<std::optional<uint32_t>, NUMLEVELS> oldSeeds;
	std::array<uint32_t, NUMLEVELS> oldDungeonSeeds;
	std::array<TriggerStruct, MAXTRIGGERS> oldTriggers;
	NativeComposedPopulationGuard()
	{
		CopyCoarseState(*oldLevel, currentLevel());
		oldLevel->pMegaTiles_ = std::move(currentLevel().pMegaTiles_);
		oldLevel->pDungeonCels_ = std::move(currentLevel().pDungeonCels_);
		oldLevel->pSpecialCels_ = std::move(currentLevel().pSpecialCels_);
		std::copy_n(LevelSeeds, NUMLEVELS, oldSeeds.begin());
		std::copy_n(DungeonSeeds, NUMLEVELS, oldDungeonSeeds.begin());
		std::copy_n(trigs, MAXTRIGGERS, oldTriggers.begin());
	}
	~NativeComposedPopulationGuard()
	{
		CopyCoarseState(currentLevel(), *oldLevel);
		currentLevel().pMegaTiles_ = std::move(oldLevel->pMegaTiles_);
		currentLevel().pDungeonCels_ = std::move(oldLevel->pDungeonCels_);
		currentLevel().pSpecialCels_ = std::move(oldLevel->pSpecialCels_);
		sgGameInitInfo = oldGameInfo;
		gbIsHellfireSaveGame = oldHellfireSave;
		MyPlayerIsDead = oldPlayerIsDead;
		LevelGenerationData = std::move(oldLevelData);
		std::copy(oldSeeds.begin(), oldSeeds.end(), LevelSeeds);
		std::copy(oldDungeonSeeds.begin(), oldDungeonSeeds.end(), DungeonSeeds);
		std::copy(oldTriggers.begin(), oldTriggers.end(), trigs);
		RestoreMonsterPopulationGlobalsForTesting(oldMonsterGlobals);
		RestoreItemInitializationGlobalsForTesting(oldItemGlobals);
	}
	static void CopyCoarseState(Level &target, const Level &source)
	{
		target.setId(source.id());
		target.DungeonMask_ = source.DungeonMask_;
		target.Protected_ = source.Protected_;
		target.SetPieceRoom_ = source.SetPieceRoom_;
		target.SetPiece_ = source.SetPiece_;
		for (int x = 0; x < DMAXX; ++x)
			std::copy_n(source.megaTiles_[x], DMAXY, target.megaTiles_[x]);
		std::copy_n(source.DPieceMicros_, MAXTILES, target.DPieceMicros_);
		target.dminPosition_ = source.dminPosition_;
		target.dmaxPosition_ = source.dmaxPosition_;
		target.ViewPosition_ = source.ViewPosition_;
		target.MicroTileLen_ = source.MicroTileLen_;
		target.TransVal_ = source.TransVal_;
		target.TransList_ = source.TransList_;
		target.themeCount_ = source.themeCount_;
		std::copy_n(source.themeLoc_, MAXTHEMES, target.themeLoc_);
		target.numThemes_ = source.numThemes_;
		target.armorFlag_ = source.armorFlag_;
		target.weaponFlag_ = source.weaponFlag_;
		target.zharlib_ = source.zharlib_;
		target.trigFlag_ = source.trigFlag_;
		target.numTriggers_ = source.numTriggers_;
		target.tWarpFrom_ = source.tWarpFrom_;
		target.uberRow_ = source.uberRow_;
		target.uberCol_ = source.uberCol_;
		target.isUberRoomOpened_ = source.isUberRoomOpened_;
		target.isUberLeverActivated_ = source.isUberLeverActivated_;
		target.uberDiabloMonsterIndex_ = source.uberDiabloMonsterIndex_;
	}
};

// Bind semantic L8 speech inputs to actual native object initialization; no MPQ is opened.
TEST_F(ThemeRoomGenerationTest, CatacombsStoryBooksUseNativeSecondMessageColumn)
{
	NativeComposedPopulationGuard guard;
	constexpr std::array<_speech_id, 3> messages { TEXT_BOOK12, TEXT_BOOK22, TEXT_BOOK32 };
	constexpr std::array<int, 3> nativeIds { 250, 253, 256 };
	ASSERT_FALSE(AllObjects[OBJ_STORYBOOK].isAnimated());
	for (size_t row = 0; row < messages.size(); ++row) {
		SCOPED_TRACE(row);
		PrepareRoom(8, 0, 0, 0, false);
		currentLevel().setId(LevelId { .levelNum = 8, .type = DTYPE_CATACOMBS });
		currentLevelNumber() = 8;
		levelType() = DTYPE_CATACOMBS;
		DungeonSeeds[16] = static_cast<uint32_t>(row) << 16;
		SetRndSeed(UINT32_C(0x13579BDF));
		const uint32_t before = GetLCGEngineState();
		Object *book = AddObject(OBJ_STORYBOOK, { 50, 50 });
		ASSERT_NE(book, nullptr);
		EXPECT_EQ(static_cast<int>(messages[row]), nativeIds[row]);
		EXPECT_EQ(book->_oVar1, static_cast<int>(row));
		EXPECT_EQ(book->_oVar2, messages[row]);
		EXPECT_EQ(book->_oVar3, 1 + 3 * static_cast<int>(row));
		EXPECT_EQ(book->_oAnimFrame, 5U - 2U * static_cast<unsigned>(row));
		EXPECT_EQ(book->_oVar4, 6 - 2 * static_cast<int>(row));
		EXPECT_EQ(GetLCGEngineState(), before);
	}
}

// Extends the existing controlled upstream oracle, not the complete campaign
// LoadGameLevel lifecycle. Geometry comes from original local assets; this
// fixture alone opens MPQs and only when explicitly selected.
// Own the extra global channels used only by the cold loader continuation.
struct NativeLevelReadyGuard {
	AutomapManager oldAutomap = CurrentAutomapManager;
	std::array<uint16_t, 256> oldDefinitions = CaptureAutomapDefinitionsForTesting();
	std::list<Missile> oldMissiles = std::move(Missiles);
	bool oldMissilePreFlag = MissilePreFlag;
	std::array<Corpse, MaxCorpses> oldCorpses;
	int8_t oldStone = stonendx;
	std::array<Portal, MAXPORTAL> oldPortals;
	NativeLevelReadyGuard()
	{
		for (size_t index = 0; index < MaxCorpses; ++index)
			oldCorpses[index] = std::move(Corpses[index]);
		std::copy_n(Portals, MAXPORTAL, oldPortals.begin());
	}
	~NativeLevelReadyGuard()
	{
		CurrentAutomapManager = oldAutomap;
		RestoreAutomapDefinitionsForTesting(oldDefinitions);
		Missiles = std::move(oldMissiles);
		MissilePreFlag = oldMissilePreFlag;
		for (size_t index = 0; index < MaxCorpses; ++index)
			Corpses[index] = std::move(oldCorpses[index]);
		stonendx = oldStone;
		std::copy(oldPortals.begin(), oldPortals.end(), Portals);
	}
};

void WriteLevelReadyState(std::ostream &out)
{
	JsonObject ready(out);
	ready.Key("world");
	WriteRoomWorld(out);
	ready.Key("corpses");
	WriteArray(out, Corpses, [&](const Corpse &corpse) {
		out << '[' << corpse.frame << ',' << corpse.width << ',' << static_cast<int>(corpse.translationPaletteIndex) << ']';
	});
	ready.Number("stoneCorpseId", stonendx);
	ready.Key("bestiaryCorpseIds");
	WriteArray(out, LevelBestiary, [&](const CMonster &type) { out << static_cast<int>(type.corpseId); });
	ready.Key("deathAnimations");
	WriteArray(out, LevelBestiary, [&](const CMonster &type) {
		const AnimStruct &death = type.getAnimData(MonsterGraphic::Death);
		out << '[' << static_cast<int>(death.frames) << ',' << death.width << ']';
	});
	ready.Key("player");
	{
		const Player &player = *MyPlayer;
		JsonObject value(out);
		value.Key("tile");
		WriteRoomPoint(out, { player.position.tile.x, player.position.tile.y });
		value.Key("future");
		WriteRoomPoint(out, { player.position.future.x, player.position.future.y });
		value.Key("old");
		WriteRoomPoint(out, { player.position.old.x, player.position.old.y });
		value.Key("last");
		WriteRoomPoint(out, { player.position.last.x, player.position.last.y });
		value.Key("temp");
		WriteRoomPoint(out, { player.position.temp.x, player.position.temp.y });
		value.Key("view");
		WriteRoomPoint(out, { viewPosition().x, viewPosition().y });
		value.Number("id", MyPlayerId);
		value.Number("level", player.plrlevel);
		value.Number("currentLife", player.life.current);
		value.Number("lightId", player.lightId);
		value.Number("lightRadius", player._pLightRad);
		value.Boolean("active", player.plractive);
		value.Boolean("levelChanging", player._pLvlChanging);
	}
	ready.Number("spellFlags", MyPlayer->_pSpellFlags);
	ready.Boolean("infravision", MyPlayer->_pInfraFlag);
	ready.Boolean("autoMapShowItems", CurrentAutomapManager.GetAutoMapShowItems());
	ready.Number("missileCount", Missiles.size());
	ready.Boolean("missilePreFlag", MissilePreFlag);
	ready.Key("automapExploration");
	out << '[';
	for (int y = 0; y < DMAXY; ++y)
		for (int x = 0; x < DMAXX; ++x) {
			if (x != 0 || y != 0) out << ',';
			out << static_cast<int>(CurrentAutomapManager.GetAutomapView()[x][y]);
		}
	out << ']';
	ready.Key("visibleTransparencyRegions");
	WriteArray(out, visibleTransparencyRegions(), [&](bool value) { out << (value ? "true" : "false"); });
	ready.Boolean("updateVision", CurrentLightManager.VisionUpdatePendingForTesting());
	ready.Key("visionActive");
	WriteArray(out, CurrentLightManager.visionActive_, [&](bool value) { out << (value ? "true" : "false"); });
	ready.Key("visionRecords");
	int id = 0;
	WriteArray(out, CurrentLightManager.visionList_, [&](const Light &vision) {
		JsonObject value(out);
		value.Number("lightId", id++);
		value.Key("tile");
		WriteRoomPoint(out, { vision.position.tile.x, vision.position.tile.y });
		value.Key("oldTile");
		WriteRoomPoint(out, { vision.position.old.x, vision.position.old.y });
		value.Number("offsetX", vision.position.offset.deltaX);
		value.Number("offsetY", vision.position.offset.deltaY);
		value.Number("radius", vision.radius);
		value.Number("oldRadius", vision.oldRadius);
		value.Boolean("isInvalid", vision.isInvalid);
		value.Boolean("hasChanged", vision.hasChanged);
	});
}

#ifndef UNPACKED_MPQS
std::optional<std::vector<uint16_t>> ReadCursorWidthsForNativeOracle(std::string_view path, std::string &error)
{
	auto asset = LoadAsset(path);
	if (!asset.has_value()) {
		error = asset.error();
		return std::nullopt;
	}
	std::string_view text = static_cast<std::string_view>(asset.value());
	std::vector<uint16_t> widths;
	while (!text.empty()) {
		const char *end;
		const ParseIntResult<uint16_t> parsed = ParseInt<uint16_t>(text, std::numeric_limits<uint16_t>::min(),
		    std::numeric_limits<uint16_t>::max(), &end);
		if (!parsed.has_value() || end == text.data()) {
			error = "Cursor width asset contains an invalid width.";
			return std::nullopt;
		}
		widths.push_back(parsed.value());
		text.remove_prefix(static_cast<size_t>(end - text.data()));
		while (!text.empty() && (text.front() == '\r' || text.front() == '\n'))
			text.remove_prefix(1);
	}
	if (widths.empty()) {
		error = "Cursor width asset is empty.";
		return std::nullopt;
	}
	return widths;
}
#endif

std::optional<std::string> WriteNativeRogueInventoryFootprints(const std::filesystem::path &root)
{
	std::optional<OwnedClxSpriteList> primarySpritesStorage;
	std::optional<OwnedClxSpriteList> secondarySprites;
#ifdef UNPACKED_MPQS
	auto primaryResult = LoadClxWithStatus("data\\inv\\objcurs.clx");
	if (!primaryResult.has_value())
		return primaryResult.error();
	primarySpritesStorage.emplace(std::move(primaryResult.value()));
	if (FindAsset("data\\inv\\objcurs2.clx").ok()) {
		auto secondaryResult = LoadClxWithStatus("data\\inv\\objcurs2.clx");
		if (!secondaryResult.has_value())
			return secondaryResult.error();
		secondarySprites.emplace(std::move(secondaryResult.value()));
	}
#else
	std::string loadError;
	auto primaryWidths = ReadCursorWidthsForNativeOracle("data\\inv\\objcurs-widths.txt", loadError);
	if (!primaryWidths.has_value())
		return loadError;
	auto primaryResult = LoadCelWithStatus("data\\inv\\objcurs", primaryWidths->data());
	if (!primaryResult.has_value())
		return primaryResult.error();
	primarySpritesStorage.emplace(std::move(primaryResult.value()));
	if (FindAsset("data\\inv\\objcurs2-widths.txt").ok()) {
		auto secondaryWidths = ReadCursorWidthsForNativeOracle("data\\inv\\objcurs2-widths.txt", loadError);
		if (!secondaryWidths.has_value())
			return loadError;
		auto secondaryResult = LoadCelWithStatus("data\\inv\\objcurs2", secondaryWidths->data());
		if (!secondaryResult.has_value())
			return secondaryResult.error();
		secondarySprites.emplace(std::move(secondaryResult.value()));
	}
#endif
	if (!primarySpritesStorage.has_value())
		return "The primary native item cursor sprite list is unavailable.";
	const OwnedClxSpriteList &primarySprites = *primarySpritesStorage;
	const uint32_t primarySpriteCount = primarySprites.numSprites();
	std::vector<bool> emitted(AllItemsList.size(), false);
	const std::filesystem::path path = root / "native-rogue-l1-inventory.tsv";
	std::ofstream table(path, std::ios::binary | std::ios::trunc);
	if (!table.good())
		return "Unable to create the native Rogue inventory footprint sidecar.";
	table << "native_item_index\tnative_cursor_id\tpixel_width\tpixel_height\tinventory_width\tinventory_height\r\n";
	const int firstItemCursor = static_cast<int>(CURSOR_FIRSTITEM);
	for (size_t index = 0; index < AllItemsList.size(); ++index) {
		const ItemData &item = AllItemsList[index];
		if (!IsItemAvailable(static_cast<int>(index)) || item.itype == ItemType::None || item.iCurs == ICURS_DEFAULT)
			continue; // Explicit unavailable rows do not have a selectable cursor footprint.
		const int cursorId = firstItemCursor + static_cast<int>(item.iCurs);
		uint16_t pixelWidth;
		uint16_t pixelHeight;
		if (cursorId <= 0)
			return "A selectable native item has a non-positive cursor id.";
		if (static_cast<uint32_t>(cursorId) <= primarySpriteCount) {
			const ClxSprite sprite = primarySprites[static_cast<size_t>(cursorId - 1)];
			pixelWidth = sprite.width();
			pixelHeight = sprite.height();
		} else if (secondarySprites.has_value()
		    && static_cast<uint32_t>(cursorId) - primarySpriteCount <= secondarySprites->numSprites()) {
			const ClxSprite sprite = (*secondarySprites)[static_cast<size_t>(static_cast<uint32_t>(cursorId) - primarySpriteCount - 1)];
			pixelWidth = sprite.width();
			pixelHeight = sprite.height();
		} else {
			return "A selectable native item cursor has no loaded sprite.";
		}
		if (pixelWidth == 0 || pixelHeight == 0)
			return "A selectable native item cursor has an empty sprite.";
		const int inventoryWidth = pixelWidth / InventorySlotSizeInPixels.width;
		const int inventoryHeight = pixelHeight / InventorySlotSizeInPixels.height;
		if (inventoryWidth <= 0 || inventoryHeight <= 0)
			return "A selectable native item cursor is smaller than one inventory cell.";
		table << index << '\t' << cursorId << '\t' << pixelWidth << '\t' << pixelHeight << '\t'
		      << inventoryWidth << '\t' << inventoryHeight << "\r\n";
		emitted[index] = true;
	}
	if (std::none_of(emitted.begin(), emitted.end(), [](bool value) { return value; }))
		return "The native Rogue inventory footprint sidecar contains no selectable items.";
	const auto requireItem = [&](const Item &item, std::string_view source) -> std::optional<std::string> {
		if (item.isEmpty())
			return std::nullopt;
		const int index = static_cast<int>(item.IDidx);
		if (index < 0 || static_cast<size_t>(index) >= emitted.size() || !emitted[static_cast<size_t>(index)])
			return std::string("A native Rogue ") + std::string(source) + " item has no selectable cursor footprint.";
		return std::nullopt;
	};
	for (uint8_t itemId : ItemPoolAdapter::ActiveItemIds()) {
		if (const auto error = requireItem(Items[itemId], "L1 ground"))
			return error;
	}
	if (MyPlayer != nullptr) {
		for (const Item &item : MyPlayer->InvBody)
			if (const auto error = requireItem(item, "owned equipment")) return error;
		if (MyPlayer->_pNumInv < 0 || MyPlayer->_pNumInv > InventoryGridCells)
			return "The native Rogue inventory count is outside its fixed capacity.";
		for (int index = 0; index < MyPlayer->_pNumInv; ++index)
			if (const auto error = requireItem(MyPlayer->InvList[index], "owned inventory")) return error;
		for (const Item &item : MyPlayer->SpdList)
			if (const auto error = requireItem(item, "owned belt")) return error;
		if (const auto error = requireItem(MyPlayer->HoldItem, "owned cursor"))
			return error;
	}
	table.flush();
	if (!table.good())
		return "Writing the native Rogue inventory footprint sidecar failed.";
	table.close();
	return std::nullopt;
}

void ThemeRoomGenerationTest::CapturePopulationOracle(const char *directory, bool freshLoadOrder, bool levelReady, bool nativeRogueRoute, std::optional<HeroClass> nativeClassRoute, bool multiplayer)
{
	if (directory == nullptr || !*directory) GTEST_SKIP() << "Select private upstream Cathedral population explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	const auto repository = std::filesystem::weakly_canonical(std::filesystem::absolute(__FILE__).parent_path().parent_path());
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	ASSERT_FALSE(std::mismatch(repository.begin(), repository.end(), root.begin(), root.end()).first == repository.end());
	// Use the same original-asset discovery and controlled hero context as the
	// earlier population fixture. No server or client runtime loads archives.
	NativeComposedPopulationGuard composedGuard;
	std::unique_ptr<NativeLevelReadyGuard> readyGuard;
	if (levelReady) readyGuard = std::make_unique<NativeLevelReadyGuard>();
	// The inherited fixture already owns and initializes the player and tables.
	// Avoid TestInitGame/InitQuests: quests are explicitly disabled below, and
	// their extra quest-dialog/UI state is not part of this population seam.
	MyPlayerId = 0;
	sgGameInitInfo.fullQuests = 1;
	gbIsHellfire = false;
	gbIsHellfireSaveGame = false;
	gbSndInited = false;
	UnloadModArchives();
	MpqArchives.erase(MainMpqPriority);
	gbIsSpawn = false;
	LoadGameArchives();
	if (!HaveMainData() || gbIsSpawn) GTEST_SKIP() << "Original DIABDAT.MPQ was not discovered.";
	currentLevel().setId(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL });
#ifndef UNPACKED_MPQS
	const auto &mainArchive = MpqArchives.at(MainMpqPriority);
	for (const std::string_view path : { "levels\\l1data\\l1.til", "levels\\l1data\\l1.sol" }) {
		const AssetRef asset = FindAsset(path);
		ASSERT_TRUE(asset.ok());
		ASSERT_EQ(asset.archive, &mainArchive);
	}
	if (levelReady) {
		const AssetRef automapAsset = FindAsset("levels\\l1data\\l1.amp");
		ASSERT_TRUE(automapAsset.ok());
		ASSERT_EQ(automapAsset.archive, &mainArchive);
	}
#endif
	size_t tileCount = 0;
	auto definitions = LoadFileInMemWithStatus<MegaTile>("levels\\l1data\\l1.til", &tileCount);
	ASSERT_TRUE(definitions.has_value());
	ASSERT_NE(definitions->get(), nullptr);
	ASSERT_EQ(tileCount, 206U);
	megaTiles() = std::move(*definitions);
	for (int piece = 0; piece < MAXTILES; ++piece)
		tileProperties()[piece] = TileProperties::None;
	ASSERT_TRUE(LoadLevelSOLData().has_value());
	ASSERT_TRUE(CurrentLightManager.LoadTrns().has_value());
	CurrentLightManager.MakeLightTable();
	LoadLevelGenerationData();
	if (freshLoadOrder) LoadPlayerDataFiles();
	// New class tuples use actual native character creation, independently of the
	// managed profile. The historical prepared Rogue tuple remains unchanged.
	Player canonicalHero;
	if (nativeClassRoute.has_value()) {
		ASSERT_TRUE(*nativeClassRoute == HeroClass::Warrior || *nativeClassRoute == HeroClass::Sorcerer);
		canonicalHero.create(*nativeClassRoute);
	}
	const bool playableRoute = nativeRogueRoute || nativeClassRoute.has_value();
	const int maximumLife = nativeClassRoute.has_value() ? canonicalHero.life.maximumBase : nativeRogueRoute ? 2880
	    : freshLoadOrder                                                                                     ? 6400
	                                                                                                         : 64;
	const int maximumMana = nativeClassRoute.has_value() ? canonicalHero.mana.maximumBase : nativeRogueRoute ? 1440
	    : freshLoadOrder                                                                                     ? 3200
	                                                                                                         : 0;
	const int lightRadius = nativeClassRoute.has_value() ? canonicalHero._pLightRad : 8;
	const char *fileName = nativeClassRoute.has_value() ? "native-class-l1-population.json" : nativeRogueRoute ? "native-rogue-l1-population.json"
	    : levelReady                                                                                           ? "native-level-ready.json"
	    : multiplayer                                                                                          ? "native-fresh-multiplayer-population.json"
	                                                                                                           : (freshLoadOrder ? "native-fresh-population.json" : "native-full-population.json");
	std::ofstream stream(root / fileName, std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int cases = 0, multiRoomCases = 0;
	{
		JsonObject document(stream);
		document.String("format", nativeClassRoute.has_value() ? "godevilution-cathedral-native-class-l1" : nativeRogueRoute ? "godevilution-cathedral-native-rogue-l1"
		        : levelReady                                                                                                 ? "godevilution-cathedral-level-ready"
		        : multiplayer                                                                                                ? "godevilution-cathedral-fresh-multiplayer-population"
		                                                                                                                     : (freshLoadOrder ? "godevilution-cathedral-fresh-population" : "godevilution-cathedral-full-population"));
		document.Number("version", 1);
		document.String("policy", nativeClassRoute.has_value() ? "fresh-cold-original-diablo-class-l1-main-seed2588" : nativeRogueRoute ? "fresh-cold-original-diablo-rogue-l1-main-seed2588"
		        : levelReady                                                                                                            ? "fresh-cold-original-diablo-single-player-normal-post-theme-through-light-vision-before-join"
		        : multiplayer                                                                                                           ? "fresh-cold-original-diablo-multiplayer-normal-clear-before-createlevel-through-createthemerooms"
		                                                                                                                                : (freshLoadOrder ? "fresh-cold-original-diablo-single-player-normal-clear-before-createlevel-through-createthemerooms" : "controlled-original-diablo-single-player-normal-post-createlevel-through-createthemerooms"));
		document.Number("playerMaximumLife", maximumLife);
		document.Number("playerMaximumMana", maximumMana);
		if (nativeClassRoute.has_value()) document.Number("playerClassId", static_cast<int>(*nativeClassRoute));
		if (freshLoadOrder) {
			document.Number("playerLightRadius", lightRadius);
			document.Number("storySeed", UINT32_C(0x13579BDF));
			document.Key("playerFuture");
			WriteRoomPoint(stream, playableRoute ? Point { 57, 40 } : Point { 75, 68 });
			document.Boolean("playerLevelChanging", true);
		}
		if (levelReady) {
			document.Key("coldPlayer");
			JsonObject context(stream);
			context.Number("spellFlags", 255);
			context.Boolean("infravision", true);
			context.Boolean("autoMapShowItems", true);
			context.Boolean("missilePreFlag", true);
			context.Number("currentLife", 6400);
			context.Boolean("priorMissilesEmpty", true);
			context.Boolean("portalsAbsent", true);
			context.Key("last");
			WriteRoomPoint(stream, { 13, 17 });
			// InitPlayer's ClearStateVariables clears the pre-load Temp (19,23).
			context.Key("temp");
			WriteRoomPoint(stream, { 0, 0 });
		}
		document.Key("itemCatalog");
		WriteCatalog(stream);
		document.Key("cases");
		stream << '[';
		for (int depth = 1; depth <= 4; ++depth)
			for (const uint32_t seed : { UINT32_C(2588), UINT32_C(743271966) })
				for (const lvl_entry entry : { ENTRY_MAIN, ENTRY_PREV }) {
					if (playableRoute && (depth != 1 || seed != UINT32_C(2588) || entry != ENTRY_MAIN)) continue;
					FreeMonsters();
					InitializeObjectPool();
					ObjectPoolAdapter::ResetLegacyObjectPools();
					for (int slot = 0; slot < MAXOBJECTS; ++slot)
						Objects[slot] = {};
					for (auto &type : LevelBestiary.types)
						type = {};
					InitializeMonsterPool();
					for (Monster &monster : MonsterPoolAdapter::AllMonsters())
						monster = {};
					// InitializeMonsterPool leaves inactive IDs untouched. This controlled
					// baseline explicitly prepares identity IDs with count zero and no
					// RNG draws; InitLevelMonsters independently resets them again below.
					for (unsigned slot = 0; slot < MaxMonsters; ++slot)
						ActiveMonsters[slot] = slot;
					InitializeItemPool();
					ItemPoolAdapter::ResetLegacyItemPools();
					for (int slot = 0; slot <= MAXITEMS; ++slot)
						Items[slot] = {};
					ClearUniqueItemFlags();
					CurrentLightManager.Init();
					// This new oracle explicitly models cold storage, not retained level
					// pools. Init() alone intentionally does not clear inactive light records.
					if (freshLoadOrder) {
						std::fill(std::begin(CurrentLightManager.lights_), std::end(CurrentLightManager.lights_), Light {});
						std::fill(std::begin(CurrentLightManager.visionList_), std::end(CurrentLightManager.visionList_), Light {});
					}
					for (Tile &tile : tiles()) {
						tile.setObject(0);
						tile.setMonster(0);
						tile.setPlayer(0);
						tile.setItem(0);
					}
					currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATHEDRAL });
					LevelSeeds[depth] = std::nullopt;
					DungeonSeeds[depth] = seed;
					DungeonSeeds[16] = UINT32_C(0x13579BDF);
					if (nativeClassRoute.has_value()) *MyPlayer = std::move(canonicalHero);
					MyPlayer->pOriginalCathedral = true;
					MyPlayer->plractive = true;
					MyPlayer->_pClass = nativeClassRoute.value_or(nativeRogueRoute ? HeroClass::Rogue : HeroClass::Warrior);
					MyPlayer->plrlevel = static_cast<uint8_t>(depth);
					MyPlayer->_pLvlChanging = freshLoadOrder;
					MyPlayer->plrIsOnSetLevel = false;
					MyPlayer->life = { 64, 64, 64, 64, 100 };
					if (freshLoadOrder) MyPlayer->life = { maximumLife, maximumLife, maximumLife, maximumLife, 100 };
					MyPlayer->mana = {};
					if (freshLoadOrder) MyPlayer->mana.maximumBase = maximumMana;
					MyPlayer->pLvlLoad = 1;
					MyPlayer->_pLightRad = lightRadius;
					if (freshLoadOrder) MyPlayer->position.future = { static_cast<uint8_t>(playableRoute ? 57 : 75), static_cast<uint8_t>(playableRoute ? 40 : 68) };
					gbIsMultiplayer = multiplayer;
					gbIsSpawn = false;
					gbIsHellfire = false;
					gbIsHellfireSaveGame = false;
					LoadingMapObjects = false;
					sgGameInitInfo.nDifficulty = DIFF_NORMAL;
					for (Quest &quest : Quests)
						quest._qactive = QUEST_NOTAVAIL;
					RestoreThemeRoomGlobalsForTesting({});
					armorFlag() = true;
					weaponFlag() = true;
					uint32_t preClearState = 0, postClearState = 0;
					if (freshLoadOrder) {
						SetRndSeed(seed);
						preClearState = GetLCGEngineState();
						InitLevelMonsters();
						postClearState = GetLCGEngineState();
					}
					if (levelReady) {
						Missiles.clear();
						MissilePreFlag = true;
						std::fill(std::begin(Corpses), std::end(Corpses), Corpse {});
						stonendx = 0;
						std::fill(std::begin(Portals), std::end(Portals), Portal {});
						RestoreAutomapDefinitionsForTesting({});
						CurrentAutomapManager.InitAutomap();
						CurrentAutomapManager.SetAutoMapShowItems(true);
						MyPlayer->_pSpellFlags = static_cast<SpellFlag>(255);
						MyPlayer->_pInfraFlag = true;
						if (cases == 0) {
							std::ofstream metadata(root / "cathedral_automap.tsv", std::ios::binary | std::ios::trunc);
							ASSERT_TRUE(metadata.good());
							metadata << "id\ttype\tflags\r\n";
							const auto mapping = CaptureAutomapDefinitionsForTesting();
							for (size_t index = 0; index < mapping.size(); ++index)
								metadata << index << '\t' << (mapping[index] & 0xFF) << '\t' << (mapping[index] >> 8) << "\r\n";
							metadata.flush();
							ASSERT_TRUE(metadata.good());
						}
					}
					CreateLevelForTesting(entry);
					MyPlayer->position.tile = viewPosition();
					if (!freshLoadOrder) MyPlayer->position.future = viewPosition();
					MyPlayer->position.old = viewPosition();
					MyPlayer->position.last = viewPosition();
					if (levelReady) {
						MyPlayer->position.last = { 13, 17 };
						MyPlayer->position.temp = { 19, 23 };
					}
					if (cases++ != 0) stream << ',';
					JsonObject row(stream);
					row.Number("depth", depth);
					row.Number("seed", seed);
					row.String("entry", entry == ENTRY_MAIN ? "main" : "previous");
					row.String("snapshot", "cathedral-population-depth-" + std::to_string(depth) + "-" + std::to_string(seed) + (entry == ENTRY_MAIN ? "-main.json" : "-previous.json"));
					if (!freshLoadOrder) {
						preClearState = GetLCGEngineState();
						row.Number("preClearRandomState", preClearState);
						row.Key("baseline");
						WriteRoomWorld(stream);
						InitLevelMonsters();
						postClearState = GetLCGEngineState();
					} else {
						row.Number("preClearRandomState", preClearState);
						row.Number("nextTransparencyValue", nextTransparencyValue());
						row.Key("triggerPositions");
						stream << '[';
						for (int trigger = 0; trigger < numTriggers(); ++trigger) {
							if (trigger) stream << ',';
							WriteRoomPoint(stream, trigs[trigger].position);
						}
						stream << ']';
					}
					row.Number("postClearRandomState", postClearState);
					SetRndSeed(seed);
					ASSERT_TRUE(GetLevelMTypes().has_value());
					InitThemes();
					if (freshLoadOrder) {
						row.Number("prePlayerRandomState", GetLCGEngineState());
						// Actual player initialization allocates slot 0 before unique lights.
						// false avoids first-load hotkey/UI storage, without changing dungeon
						// placement, animation draws, light allocation or vision activation.
						InitPlayer(*MyPlayer, false);
						ASSERT_EQ(MyPlayer->lightId, 0);
						row.Number("playerIdleFrames", MyPlayer->_pNFrames);
						row.Number("postPlayerRandomState", GetLCGEngineState());
						row.Key("playerAnimation");
						stream << '[' << static_cast<int>(MyPlayer->animInfo.currentFrame)
						       << ',' << static_cast<int>(MyPlayer->animInfo.tickCounterOfCurrentFrame)
						       << ',' << static_cast<int>(MyPlayer->animInfo.numberOfFrames)
						       << ',' << static_cast<int>(MyPlayer->animInfo.ticksPerFrame)
						       << ',' << (MyPlayer->animInfo.isPetrified ? "true" : "false") << ']';
						row.Key("visionActive");
						WriteArray(stream, CurrentLightManager.visionActive_, [&](bool active) { stream << (active ? "true" : "false"); });
						row.Boolean("updateVision", CurrentLightManager.VisionUpdatePendingForTesting());
						row.Key("visionRecords");
						int visionId = 0;
						WriteArray(stream, CurrentLightManager.visionList_, [&](const Light &vision) {
							JsonObject record(stream);
							record.Number("lightId", visionId++);
							record.Key("tile");
							WriteRoomPoint(stream, { vision.position.tile.x, vision.position.tile.y });
							record.Key("oldTile");
							WriteRoomPoint(stream, { vision.position.old.x, vision.position.old.y });
							record.Number("offsetX", vision.position.offset.deltaX);
							record.Number("offsetY", vision.position.offset.deltaY);
							record.Number("radius", vision.radius);
							record.Number("oldRadius", vision.oldRadius);
							record.Boolean("isInvalid", vision.isInvalid);
							record.Boolean("hasChanged", vision.hasChanged);
						});
						row.Key("baseline");
						WriteRoomWorld(stream);
					}
					row.Key("selectedRoomCatalog");
					WriteRoomCatalog(stream);
					row.Key("selectedThemes");
					stream << '[';
					for (int i = 0; i < numThemes(); ++i) {
						if (i) stream << ',';
						stream << '[' << static_cast<int>(themes[i].ttype) << ',' << static_cast<int>(themes[i].ttval) << ']';
					}
					stream << ']';
					if (numThemes() > 1) ++multiRoomCases;
					SetRndSeed(seed);
					HoldThemeRooms();
					InitGolems();
					InitObjects();
					ASSERT_TRUE(InitMonsters().has_value());
					InitItems();
					row.Key("afterItems");
					WriteRoomWorld(stream);
					row.Key("itemInitialization");
					{
						const auto state = CaptureItemInitializationGlobalsForTesting();
						JsonObject init(stream);
						init.Boolean("showUniqueInfo", state.showUniqueInfo);
						init.Number("recordCount", state.count);
						init.Key("records");
						WriteArray(stream, state.records, [&](const ItemGetRecordStruct &record) {
							stream << '[' << record.nSeed << ',' << record.wCI << ',' << record.nIndex << ',' << record.dwTimestamp << ']';
						});
					}
					row.Key("visibleTransparencyRegions");
					WriteArray(stream, visibleTransparencyRegions(), [&](bool visible) { stream << (visible ? "true" : "false"); });
					CreateThemeRooms();
					row.Key("expected");
					WriteRoomWorld(stream);
					if (levelReady) {
						const uint32_t before = GetLCGEngineState();
						CompleteFreshLevelForTesting(entry);
						ASSERT_EQ(GetLCGEngineState(), before);
						row.Key("ready");
						WriteLevelReadyState(stream);
					}
				}
		stream << ']';
		document.Number("caseCount", cases);
		document.Number("multiRoomCases", multiRoomCases);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(cases, playableRoute ? 1 : 16);
	if (!playableRoute) EXPECT_GT(multiRoomCases, 0);
	EXPECT_LE(std::filesystem::file_size(root / fileName), 128U * 1024U * 1024U);
	if (nativeRogueRoute) {
		const auto footprintError = WriteNativeRogueInventoryFootprints(root);
		ASSERT_FALSE(footprintError.has_value()) << footprintError.value_or("unknown cursor footprint failure");
		EXPECT_LE(std::filesystem::file_size(root / "native-rogue-l1-inventory.tsv"), 4U * 1024U * 1024U);
	}
}

#ifndef UNPACKED_MPQS
struct NativeCatacombsPopulationAssetGuard {
	std::string assetsPath = paths::AssetsPath();
	std::vector<std::string> overrides = std::move(OverridePaths);
	decltype(MpqArchives) archives = std::move(MpqArchives);
	~NativeCatacombsPopulationAssetGuard()
	{
		MpqArchives.clear();
		MpqArchives = std::move(archives);
		OverridePaths = std::move(overrides);
		paths::SetAssetsPath(assetsPath);
	}
};

struct NativeCatacombsPopulationOutputGuard {
	std::filesystem::path root;
	bool ownsRoot = false;
	bool complete = false;
	bool WriteStatus(std::string_view value) const
	{
		std::ofstream status(root / "capture-status.json", std::ios::binary | std::ios::trunc);
		if (!status.good())
			return false;
		{
			JsonObject document(status);
			document.String("status", value);
		}
		status << "\r\n";
		status.flush();
		if (!status.good())
			return false;
		status.close();
		return status.good();
	}
	~NativeCatacombsPopulationOutputGuard()
	{
		if (ownsRoot && !complete)
			WriteStatus("incomplete");
	}
};

struct NativeCatacombsPopulationCase {
	HeroClass heroClass;
	int depth;
	lvl_entry entry;
	uint32_t seed;
	bool retainedUniqueHistory;
	bool aiEligibilityCoverage = false;
};

constexpr char NativeCatacombsPreparedInventorySeedPolicy[] = "catacombs-class-storage-slot-v1";

// Pin only starter-item identity seeds; native population uses independent world RNG state.
void ApplyNativeCatacombsPreparedInventorySeedPolicy(Player &player)
{
	const uint32_t classComponent = (static_cast<uint32_t>(player._pClass) & 0xFFU) << 16;
	auto assignContainer = [&](auto &items, uint32_t container) {
		uint32_t slot = 0;
		for (Item &item : items) {
			if (!item.isEmpty())
				item._iSeed = 0xC2000000U | classComponent | (container << 8) | slot;
			++slot;
		}
	};
	assignContainer(player.InvBody, 1);
	assignContainer(player.InvList, 2);
	assignContainer(player.SpdList, 3);
	if (!player.HoldItem.isEmpty())
		player.HoldItem._iSeed = 0xC2000000U | classComponent | (4U << 8);
}

std::vector<uint32_t> CaptureNativeCatacombsPlayerItemSeeds(const Player &player)
{
	std::vector<uint32_t> seeds;
	for (const Item &item : player.InvBody)
		seeds.push_back(item._iSeed);
	for (const Item &item : player.InvList)
		seeds.push_back(item._iSeed);
	for (const Item &item : player.SpdList)
		seeds.push_back(item._iSeed);
	seeds.push_back(player.HoldItem._iSeed);
	return seeds;
}

const char *CatacombsClassSymbol(HeroClass heroClass)
{
	return heroClass == HeroClass::Warrior ? "warrior" : "sorcerer";
}

const char *CatacombsEntrySymbol(lvl_entry entry)
{
	return entry == ENTRY_MAIN ? "main" : entry == ENTRY_PREV ? "previous"
	                                                          : "town-warp-down";
}

void WriteCatacombsPlayer(std::ostream &out, const Player &player)
{
	JsonObject json(out);
	json.Number("classId", player._pClass);
	json.Number("characterLevel", player.getCharacterLevel());
	json.Number("dungeonLevel", player.plrlevel);
	json.Number("experience", player._pExperience);
	json.Number("lifeBase", player.life.base);
	json.Number("lifeCurrent", player.life.current);
	json.Number("lifeMaximum", player.life.maximum);
	json.Number("lifeMaximumBase", player.life.maximumBase);
	json.Number("manaBase", player.mana.base);
	json.Number("manaCurrent", player.mana.current);
	json.Number("manaMaximum", player.mana.maximum);
	json.Number("manaMaximumBase", player.mana.maximumBase);
	json.Number("strength", player.attributes.strength.base);
	json.Number("magic", player.attributes.magic.base);
	json.Number("dexterity", player.attributes.dexterity.base);
	json.Number("vitality", player.attributes.vitality.base);
	json.Number("statPoints", player._pStatPts);
	json.Number("gold", player._pGold);
	json.Number("lightRadius", player._pLightRad);
	json.Number("lightId", player.lightId);
	json.Number("mode", player._pmode);
	json.Number("direction", player.direction);
	json.Number("pLvlLoad", player.pLvlLoad);
	json.Number("spellFlags", player._pSpellFlags);
	json.Number("townWarps", player.pTownWarps);
	json.Boolean("active", player.plractive);
	json.Boolean("levelChanging", player._pLvlChanging);
	json.Boolean("originalCathedral", player.pOriginalCathedral);
	json.Key("positionTile");
	WriteRoomPoint(out, { player.position.tile.x, player.position.tile.y });
	json.Key("positionFuture");
	WriteRoomPoint(out, { player.position.future.x, player.position.future.y });
	json.Key("positionOld");
	WriteRoomPoint(out, { player.position.old.x, player.position.old.y });
	json.Key("positionLast");
	WriteRoomPoint(out, { player.position.last.x, player.position.last.y });
	json.Key("positionTemp");
	WriteRoomPoint(out, { player.position.temp.x, player.position.temp.y });
	json.Number("inventoryCount", player._pNumInv);
	json.Key("bodyItems");
	WriteArray(out, player.InvBody, [&](const Item &item) { WriteItem(out, item); });
	json.Key("inventoryItems");
	WriteArray(out, player.InvList, [&](const Item &item) { WriteItem(out, item); });
	json.Key("beltItems");
	WriteArray(out, player.SpdList, [&](const Item &item) { WriteItem(out, item); });
	json.Key("cursorItem");
	WriteItem(out, player.HoldItem);
	json.Key("inventoryGrid");
	WriteArray(out, player.InvGrid, [&](int8_t value) { out << static_cast<int>(value); });
	json.Key("spellLevels");
	WriteArray(out, player._pSplLvl, [&](uint8_t value) { out << static_cast<int>(value); });
	json.Key("animation");
	{
		JsonObject animation(out);
		animation.Number("currentFrame", player.animInfo.currentFrame);
		animation.Number("tickCounterOfCurrentFrame", player.animInfo.tickCounterOfCurrentFrame);
		animation.Number("frameCount", player.animInfo.numberOfFrames);
		animation.Number("ticksPerFrame", player.animInfo.ticksPerFrame);
		animation.Boolean("isPetrified", player.animInfo.isPetrified);
	}
}

void WriteCatacombsBestiary(std::ostream &out)
{
	out << '[';
	for (size_t index = 0; index < LevelBestiary.size(); ++index) {
		if (index != 0)
			out << ',';
		const CMonster &entry = LevelBestiary[index];
		const MonsterData &source = entry.data();
		JsonObject json(out);
		json.Number("index", index);
		json.Number("monsterType", entry.type);
		json.Number("placeFlags", entry.placeFlags);
		json.Number("corpseId", entry.corpseId);
		json.String("name", source.name);
		json.Number("spriteId", source.spriteId);
		json.Number("nativeAi", source.ai);
		json.Number("availability", source.availability);
		json.Number("imageCost", source.image);
		json.Number("minimumDungeonLevel", source.minDunLvl);
		json.Number("maximumDungeonLevel", source.maxDunLvl);
		json.Number("treasure", source.treasure);
		const std::optional<MissileID> missile = AiMissileType(source.ai);
		if (missile.has_value())
			json.Number("aiMissileId", missile.value());
		else {
			json.Key("aiMissileId");
			out << "null";
		}
		json.Key("sourceFrames");
		WriteArray(out, source.frames, [&](int8_t value) { out << static_cast<int>(value); });
		json.Key("sourceRates");
		WriteArray(out, source.rate, [&](int8_t value) { out << static_cast<int>(value); });
		json.Key("animations");
		out << '[';
		for (size_t animation = 0; animation < std::size(entry.anims); ++animation) {
			if (animation != 0)
				out << ',';
			const AnimStruct &data = entry.anims[animation];
			out << '[' << data.width << ',' << static_cast<int>(data.frames) << ',' << static_cast<int>(data.rate) << ']';
		}
		out << ']';
	}
	out << ']';
}

void WriteCatacombsThemeState(std::ostream &out)
{
	JsonObject json(out);
	json.Key("selected");
	out << '[';
	for (int index = 0; index < numThemes(); ++index) {
		if (index != 0)
			out << ',';
		out << '[' << static_cast<int>(themes[index].ttype) << ',' << static_cast<int>(themes[index].ttval) << ']';
	}
	out << ']';
	json.Number("roomCount", themeCount());
	json.Key("rooms");
	out << '[';
	for (int index = 0; index < themeCount(); ++index) {
		if (index != 0)
			out << ',';
		const THEME_LOC &location = themeLocations()[index];
		JsonObject room(out);
		room.Key("position");
		WriteRoomPoint(out, { location.room.position.x, location.room.position.y });
		room.Key("size");
		out << '[' << static_cast<int>(location.room.size.width) << ',' << static_cast<int>(location.room.size.height) << ']';
		room.Number("transparency", location.ttval);
	}
	out << ']';
}

void WriteCatacombsResetRecords(std::ostream &out)
{
	const MonsterPopulationGlobalsForTesting monsters = CaptureMonsterPopulationGlobalsForTesting();
	const ItemInitializationGlobalsForTesting items = CaptureItemInitializationGlobalsForTesting();
	JsonObject json(out);
	json.Key("monsterPopulation");
	{
		JsonObject state(out);
		state.Number("totalMonsters", monsters.totalMonsters);
		state.Number("imageTotal", monsters.imageTotal);
		state.Number("uniqueTransparency", monsters.uniqueTransparency);
	}
	json.Key("itemInitialization");
	{
		JsonObject state(out);
		state.Boolean("showUniqueInfo", items.showUniqueInfo);
		state.Number("recordCount", items.count);
		state.Key("records");
		WriteArray(out, items.records, [&](const ItemGetRecordStruct &record) {
			out << '[' << record.nSeed << ',' << record.wCI << ',' << record.nIndex << ',' << record.dwTimestamp << ']';
		});
	}
}

void WriteCatacombsRngBoundary(std::ostream &out, bool &first, std::string_view name, uint32_t value)
{
	if (!first)
		out << ',';
	first = false;
	JsonString(out, name);
	out << ':' << value;
}
#endif

void ThemeRoomGenerationTest::CaptureNativeCatacombsPopulationOracle(const char *archivePath, const char *directory, bool captureStages, bool multiplayer)
{
	if (archivePath == nullptr || archivePath[0] == '\0' || directory == nullptr || directory[0] == '\0')
		GTEST_SKIP() << "Set both explicit Catacombs population archive and new reference directory variables.";
#ifdef UNPACKED_MPQS
	GTEST_SKIP() << "The Catacombs population oracle requires the explicit MPQ archive backend.";
#else
	const std::filesystem::path explicitArchive = std::filesystem::absolute(archivePath).lexically_normal();
	ASSERT_TRUE(std::filesystem::is_regular_file(explicitArchive)) << explicitArchive;
	NativeCatacombsPopulationOutputGuard outputGuard;
	outputGuard.root = std::filesystem::absolute(directory).lexically_normal();
	ASSERT_TRUE(outputGuard.root.is_absolute());
	ASSERT_FALSE(std::filesystem::exists(outputGuard.root)) << "Refusing to overwrite an existing Catacombs population reference directory.";
	ASSERT_TRUE(std::filesystem::is_directory(outputGuard.root.parent_path()))
	    << "Create the reference directory parent before opting into native capture.";

	NativeComposedPopulationGuard composedGuard;
	NativeLevelReadyGuard readyGuard;
	NativeItemWorldGuard itemWorldGuard;
	NativeCatacombsPopulationAssetGuard assetGuard;
	std::error_code createError;
	outputGuard.ownsRoot = std::filesystem::create_directory(outputGuard.root, createError);
	ASSERT_TRUE(outputGuard.ownsRoot) << "Could not create a fresh native population reference directory: " << createError.message();
	ASSERT_TRUE(outputGuard.WriteStatus("capturing")) << "Could not write the capture status marker.";
	const std::filesystem::path repository = std::filesystem::weakly_canonical(std::filesystem::absolute(__FILE__).parent_path().parent_path());
	const std::string repositoryAssets = (repository / "assets").string() + "/";
	paths::SetAssetsPath(repositoryAssets);
	OverridePaths = { repositoryAssets };
	MpqArchives.clear();
	std::int32_t archiveOpenError = 0;
	auto archive = MpqArchive::Open(explicitArchive.string().c_str(), archiveOpenError);
	ASSERT_TRUE(archive.has_value()) << "Could not open the explicitly selected original archive: " << MpqArchive::ErrorMessage()
	                                 << " (" << archiveOpenError << ')';
	const auto [archiveIt, archiveInserted] = MpqArchives.emplace(MainMpqPriority, std::move(archive.value()));
	ASSERT_TRUE(archiveInserted);
	MpqArchive &mainArchive = archiveIt->second;

	currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
	ASSERT_TRUE(FindAsset("levels\\l2data\\l2.til").ok());
	ASSERT_EQ(FindAsset("levels\\l2data\\l2.til").archive, &mainArchive);
	ASSERT_TRUE(FindAsset("levels\\l2data\\l2.sol").ok());
	ASSERT_EQ(FindAsset("levels\\l2data\\l2.sol").archive, &mainArchive);
	for (const char *translationTable : { "plrgfx\\infra.trn", "plrgfx\\stone.trn" }) {
		const AssetRef translationAsset = FindAsset(translationTable);
		ASSERT_TRUE(translationAsset.ok()) << translationTable;
		ASSERT_EQ(translationAsset.archive, &mainArchive) << translationTable;
	}
	// Pause is a bundled engine translation table, not an original MPQ asset.
	// Pin the production lookup to the canonical loose resource bytes.
	std::array<uint8_t, 256> expectedPause {};
	std::array<uint8_t, 256> loadedPause {};
	const auto pausePath = repository / "assets/gendata/pause.trn";
	ASSERT_EQ(std::filesystem::file_size(pausePath), expectedPause.size());
	std::ifstream pauseSource(pausePath, std::ios::binary);
	pauseSource.read(reinterpret_cast<char *>(expectedPause.data()), expectedPause.size());
	ASSERT_TRUE(pauseSource.good());
	pauseSource.close();
	ASSERT_TRUE(pauseSource.good());
	const auto pauseAsset = FindAsset("gendata\\pause.trn");
	ASSERT_TRUE(pauseAsset.ok());
	ASSERT_EQ(pauseAsset.archive, nullptr);
	ASSERT_EQ(pauseAsset.size(), loadedPause.size());
	auto pauseHandle = OpenAsset(FindAsset("gendata\\pause.trn"));
	ASSERT_TRUE(pauseHandle.ok());
	ASSERT_TRUE(pauseHandle.read(loadedPause.data(), loadedPause.size()));
	ASSERT_EQ(loadedPause, expectedPause);
	ASSERT_TRUE(FindAsset("levels\\l2data\\l2.amp").ok());
	ASSERT_EQ(FindAsset("levels\\l2data\\l2.amp").archive, &mainArchive);
	const AssetRef automapAsset = FindAsset("levels\\l2data\\l2.amp");
	ASSERT_EQ(automapAsset.archive, &mainArchive);
	const size_t sourceAutomapByteCount = automapAsset.size();
	ASSERT_GT(sourceAutomapByteCount, 0U);
	ASSERT_LE(sourceAutomapByteCount, 4U * 1024U * 1024U);
	ASSERT_EQ(sourceAutomapByteCount % sizeof(uint16_t), 0U);
	AssetHandle automapHandle = OpenAsset(FindAsset("levels\\l2data\\l2.amp"));
	ASSERT_TRUE(automapHandle.ok());
	std::vector<uint8_t> sourceAutomap(sourceAutomapByteCount);
	ASSERT_TRUE(automapHandle.read(sourceAutomap.data(), sourceAutomap.size()));
	std::vector<uint16_t> sourceAutomapWords(sourceAutomapByteCount / sizeof(uint16_t));
	for (size_t word = 0; word < sourceAutomapWords.size(); ++word) {
		sourceAutomapWords[word] = static_cast<uint16_t>(sourceAutomap[word * 2])
		    | static_cast<uint16_t>(static_cast<uint16_t>(sourceAutomap[word * 2 + 1]) << 8);
	}
	const AssetRef solAsset = FindAsset("levels\\l2data\\l2.sol");
	ASSERT_EQ(solAsset.archive, &mainArchive);
	const size_t sourceSolByteCount = solAsset.size();
	ASSERT_EQ(sourceSolByteCount, 559U);
	AssetHandle solHandle = OpenAsset(FindAsset("levels\\l2data\\l2.sol"));
	ASSERT_TRUE(solHandle.ok());
	std::vector<uint8_t> sourceSol(sourceSolByteCount);
	ASSERT_TRUE(solHandle.read(sourceSol.data(), sourceSol.size()));
	const size_t reservedSolFlagCount = static_cast<size_t>(std::count_if(sourceSol.begin(), sourceSol.end(), [](uint8_t value) { return (value & 0x40) != 0; }));
	ASSERT_GT(reservedSolFlagCount, 0U) << "The raw Catacombs SOL should preserve native reserved 0x40 bits.";

	const AssetRef tileAsset = FindAsset("levels\\l2data\\l2.til");
	ASSERT_EQ(tileAsset.archive, &mainArchive);
	ASSERT_EQ(tileAsset.size(), 160U * sizeof(MegaTile));
	size_t megaTileCount = 0;
	auto tileDefinitions = LoadFileInMemWithStatus<MegaTile>("levels\\l2data\\l2.til", &megaTileCount);
	ASSERT_TRUE(tileDefinitions.has_value());
	ASSERT_NE(tileDefinitions->get(), nullptr);
	ASSERT_EQ(megaTileCount, 160U);
	std::vector<MegaTile> sourceMegaTiles(megaTileCount);
	std::copy_n(tileDefinitions->get(), megaTileCount, sourceMegaTiles.begin());
	megaTiles() = std::move(tileDefinitions.value());
	std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
	ASSERT_TRUE(LoadLevelSOLData().has_value());
	for (size_t piece = 0; piece < MAXTILES; ++piece) {
		const uint8_t expected = piece < sourceSol.size() ? sourceSol[piece] : 0;
		ASSERT_EQ(static_cast<uint8_t>(tileProperties()[piece]), expected) << "SOL piece " << piece;
	}
	ASSERT_TRUE(CurrentLightManager.LoadTrns().has_value());
	CurrentLightManager.MakeLightTable();
	LoadLevelGenerationData();
	LoadObjectData();
	LoadMonsterData();
	LoadItemData();
	LoadSpellData();
	LoadPlayerDataFiles();
	ASSERT_EQ(AllObjects.size(), 109U);
	ASSERT_FALSE(MonstersData.empty());
	ASSERT_FALSE(AllItemsList.empty());

	std::filesystem::create_directories(outputGuard.root / "cases");
	{
		std::ofstream source(outputGuard.root / "native-catacombs-sources.json", std::ios::binary | std::ios::trunc);
		ASSERT_TRUE(source.good());
		{
			JsonObject document(source);
			document.String("format", "godevilution-native-catacombs-population-sources");
			document.Number("version", 1);
			document.Number("tileDefinitionCount", megaTileCount);
			document.Key("tileDefinitions");
			WriteArray(source, sourceMegaTiles, [&](const MegaTile &tile) {
				source << '[' << tile.micro1 << ',' << tile.micro2 << ',' << tile.micro3 << ',' << tile.micro4 << ']';
			});
			document.Number("automapSourceByteCount", sourceAutomap.size());
			document.Number("automapWordCount", sourceAutomapWords.size());
			document.Key("automapRawWordsLittleEndian");
			WriteArray(source, sourceAutomapWords, [&](uint16_t value) { source << value; });
			document.Number("solSourceByteCount", sourceSol.size());
			document.Number("solEffectiveSlotCount", MAXTILES);
			document.Number("solReserved0x40Count", reservedSolFlagCount);
			document.Key("solRawBytes");
			WriteArray(source, sourceSol, [&](uint8_t value) { source << static_cast<unsigned int>(value); });
			document.Key("solEffectiveProperties");
			WriteArray(source, tileProperties(), [&](TileProperties property) { source << static_cast<unsigned int>(property); });
		}
		source << "\r\n";
		source.flush();
		ASSERT_TRUE(source.good());
		source.close();
		ASSERT_TRUE(source.good());
	}

	std::vector<NativeCatacombsPopulationCase> cases;
	for (const uint32_t seed : { UINT32_C(2588), UINT32_C(743271966) })
		for (int depth = 5; depth <= 8; ++depth)
			for (const lvl_entry entry : { ENTRY_MAIN, ENTRY_PREV })
				cases.push_back({ HeroClass::Warrior, depth, entry, seed, false });
	for (int depth = 5; depth <= 8; ++depth)
		cases.push_back({ HeroClass::Sorcerer, depth, ENTRY_MAIN, UINT32_C(2588), false });
	for (const HeroClass heroClass : { HeroClass::Warrior, HeroClass::Sorcerer })
		cases.push_back({ heroClass, 5, ENTRY_TWARPDN, UINT32_C(2588), false });
	for (const HeroClass heroClass : { HeroClass::Warrior, HeroClass::Sorcerer })
		for (int depth = 5; depth <= 8; ++depth)
			cases.push_back({ heroClass, depth, ENTRY_MAIN, UINT32_C(2588), true });
	cases.push_back({ HeroClass::Warrior, 8, ENTRY_MAIN, UINT32_C(0), false, true });
	cases.push_back({ HeroClass::Warrior, 8, ENTRY_MAIN, UINT32_C(1), false, true });
	ASSERT_EQ(cases.size(), 32U);
	std::array<bool, 128> carriedHistory {};
	std::vector<NativeCatacombsPopulationCase> indexRows;
	indexRows.reserve(cases.size());
	for (const NativeCatacombsPopulationCase &spec : cases) {
		if (spec.retainedUniqueHistory && spec.depth != 5)
			std::copy(carriedHistory.begin(), carriedHistory.end(), std::begin(UniqueItemFlags));
		else {
			ClearUniqueItemFlags();
			if (spec.retainedUniqueHistory)
				UniqueItemFlags[PreparedCatacombsUniqueHistoryId] = true;
		}
		const std::array<bool, 128> historyAtCaseStart = CaptureFlags();
		if (spec.retainedUniqueHistory)
			ASSERT_TRUE(historyAtCaseStart[PreparedCatacombsUniqueHistoryId]);
		else
			ASSERT_EQ(std::count(historyAtCaseStart.begin(), historyAtCaseStart.end(), true), 0);
		FreeMonsters();
		InitializeObjectPool();
		ObjectPoolAdapter::ResetLegacyObjectPools();
		for (int slot = 0; slot < MAXOBJECTS; ++slot)
			Objects[slot] = {};
		for (CMonster &type : LevelBestiary.types)
			type = {};
		InitializeMonsterPool();
		for (Monster &monster : MonsterPoolAdapter::AllMonsters())
			monster = {};
		for (unsigned slot = 0; slot < MaxMonsters; ++slot)
			ActiveMonsters[slot] = slot;
		InitializeItemPool();
		ItemPoolAdapter::ResetLegacyItemPools();
		for (int slot = 0; slot <= MAXITEMS; ++slot)
			Items[slot] = {};
		CurrentLightManager.Init();
		std::fill(std::begin(CurrentLightManager.lights_), std::end(CurrentLightManager.lights_), Light {});
		std::fill(std::begin(CurrentLightManager.visionList_), std::end(CurrentLightManager.visionList_), Light {});
		for (Tile &tile : tiles())
			tile = {};
		for (Quest &quest : Quests)
			quest._qactive = QUEST_NOTAVAIL;
		RestoreThemeRoomGlobalsForTesting({});
		for (int index = 0; index < MAXTHEMES; ++index)
			themes[index] = {};
		numThemes() = 0;
		RestoreAutomapDefinitionsForTesting({});
		CurrentAutomapManager.InitAutomap();
		CurrentAutomapManager.SetAutoMapShowItems(true);
		Missiles.clear();
		MissilePreFlag = true;
		std::fill(std::begin(Corpses), std::end(Corpses), Corpse {});
		stonendx = 0;
		std::fill(std::begin(Portals), std::end(Portals), Portal {});
		MyPlayerId = 0;
		Players.resize(1);
		MyPlayer = &Players[0];
		Player canonicalHero;
		canonicalHero.create(spec.heroClass);
		const uint32_t beforePreparedInventorySeedPolicy = GetLCGEngineState();
		ApplyNativeCatacombsPreparedInventorySeedPolicy(canonicalHero);
		ASSERT_EQ(GetLCGEngineState(), beforePreparedInventorySeedPolicy);
		*MyPlayer = std::move(canonicalHero);
		MyPlayer->pOriginalCathedral = true;
		MyPlayer->plractive = true;
		MyPlayer->_pLvlChanging = true;
		MyPlayer->plrIsOnSetLevel = false;
		MyPlayer->plrlevel = static_cast<uint8_t>(spec.depth);
		MyPlayer->pLvlLoad = 1;
		MyPlayer->position.future = { 75, 68 };
		MyPlayer->position.last = { 13, 17 };
		MyPlayer->position.temp = { 19, 23 };
		const int maximumLife = MyPlayer->life.maximumBase;
		const int maximumMana = MyPlayer->mana.maximumBase;
		MyPlayer->life = { maximumLife, maximumLife, maximumLife, maximumLife, 100 };
		MyPlayer->mana = { maximumMana, maximumMana, maximumMana, maximumMana, 100 };
		currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(spec.depth), .type = DTYPE_CATACOMBS });
		currentLevelNumber() = spec.depth;
		levelType() = DTYPE_CATACOMBS;
		LevelSeeds[spec.depth] = std::nullopt;
		DungeonSeeds[spec.depth] = spec.seed;
		DungeonSeeds[16] = UINT32_C(0x13579BDF);
		gbIsMultiplayer = multiplayer;
		gbIsSpawn = false;
		gbIsHellfire = false;
		gbIsHellfireSaveGame = false;
		gbSndInited = false;
		LoadingMapObjects = false;
		HeadlessMode = true;
		sgGameInitInfo.fullQuests = 1;
		sgGameInitInfo.nDifficulty = DIFF_NORMAL;
		armorFlag() = true;
		weaponFlag() = true;
		const std::vector<uint32_t> playerGenerationItemSeeds = CaptureNativeCatacombsPlayerItemSeeds(*MyPlayer);
		std::ostringstream playerGenerationInput;
		WriteCatacombsPlayer(playerGenerationInput, *MyPlayer);
		ASSERT_TRUE(playerGenerationInput.good());
		SetRndSeed(spec.seed);
		const uint32_t beforePopulationReset = GetLCGEngineState();
		InitLevelMonsters();
		const uint32_t afterPopulationReset = GetLCGEngineState();
		const uint32_t beforeCreateLevel = GetLCGEngineState();
		CreateLevelForTesting(spec.entry);
		ASSERT_TRUE(LevelSeeds[spec.depth].has_value());
		const uint32_t attemptSeed = *LevelSeeds[spec.depth];
		const uint32_t afterCreateLevel = GetLCGEngineState();
		SetRndSeed(spec.seed);
		ASSERT_TRUE(GetLevelMTypes().has_value());
		const uint32_t afterGetLevelMTypes = GetLCGEngineState();
		InitThemes();
		const uint32_t afterInitThemes = GetLCGEngineState();
		const uint32_t beforeInitPlayer = GetLCGEngineState();
		const Point preEntryTile = MyPlayer->position.tile;
		const Point preEntryFuture = MyPlayer->position.future;
		const Point preEntryOld = MyPlayer->position.old;
		const Point preEntryLast = MyPlayer->position.last;
		const Point preEntryTemp = MyPlayer->position.temp;
		InitPlayer(*MyPlayer, false);
		ASSERT_EQ(MyPlayer->lightId, 0);
		ASSERT_EQ(CaptureNativeCatacombsPlayerItemSeeds(*MyPlayer), playerGenerationItemSeeds)
		    << "Native InitPlayer should receive and preserve the prepared starter-item identities.";
		const uint32_t afterInitPlayer = GetLCGEngineState();
		const std::array<bool, 128> inputUniqueFlags = CaptureFlags();
		ASSERT_EQ(inputUniqueFlags, historyAtCaseStart) << "Level setup unexpectedly changed retained unique-item history.";
		if (spec.retainedUniqueHistory)
			ASSERT_TRUE(inputUniqueFlags[PreparedCatacombsUniqueHistoryId]);
		SetRndSeed(spec.seed);
		const uint32_t afterPopulationSeedReset = GetLCGEngineState();
		const std::string file = std::string(CatacombsClassSymbol(spec.heroClass))
		    + (spec.aiEligibilityCoverage ? "-ai-eligibility-coverage" : spec.retainedUniqueHistory ? "-retained-history"
		            : spec.entry == ENTRY_TWARPDN                                                   ? "-town-warp-down"
		                                                                                            : "-cold")
		    + "-l" + std::to_string(spec.depth) + "-" + CatacombsEntrySymbol(spec.entry) + "-" + std::to_string(spec.seed) + ".json";
		const std::filesystem::path casePath = outputGuard.root / "cases" / file;
		std::ofstream caseOutput(casePath, std::ios::binary | std::ios::trunc);
		ASSERT_TRUE(caseOutput.good());
		uint32_t afterHoldThemeRooms = 0;
		uint32_t afterInitGolems = 0;
		uint32_t afterInitObjects = 0;
		uint32_t afterInitMonsters = 0;
		uint32_t afterInitItems = 0;
		uint32_t afterCreateThemeRooms = 0;
		uint32_t beforeReady = 0;
		uint32_t afterReady = 0;
		std::array<bool, 128> outputUniqueFlags {};
		{
			JsonObject document(caseOutput);
			document.String("format", "godevilution-catacombs-population-case");
			document.Number("version", captureStages ? 2 : 1);
			document.String("policy", multiplayer ? "quest-disabled-original-diablo-multiplayer-normal" : "quest-disabled-original-diablo-single-player-normal");
			document.String("characterClass", CatacombsClassSymbol(spec.heroClass));
			document.Number("characterClassId", spec.heroClass);
			document.Number("characterLevel", MyPlayer->getCharacterLevel());
			document.Number("levelId", spec.depth);
			document.String("entry", CatacombsEntrySymbol(spec.entry));
			document.Number("inputSeed", spec.seed);
			if (captureStages) document.Number("storySeed", DungeonSeeds[16]);
			if (spec.aiEligibilityCoverage)
				document.String("casePurpose", "native-ai-eligibility-coverage");
			document.Number("attemptSeed", attemptSeed);
			document.Number("solSourceByteCount", sourceSolByteCount);
			document.Number("solEffectiveSlotCount", MAXTILES);
			document.Boolean("retainedUniqueHistory", spec.retainedUniqueHistory);
			document.Key("rngBoundaries");
			caseOutput << '{';
			bool firstBoundary = true;
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "beforePopulationReset", beforePopulationReset);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterPopulationReset", afterPopulationReset);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "beforeCreateLevel", beforeCreateLevel);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterCreateLevel", afterCreateLevel);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterGetLevelMTypes", afterGetLevelMTypes);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterInitThemes", afterInitThemes);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "beforeInitPlayer", beforeInitPlayer);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterInitPlayer", afterInitPlayer);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterPopulationSeedReset", afterPopulationSeedReset);
			caseOutput << '}';
			document.Key("uniqueItemHistory");
			{
				JsonObject history(caseOutput);
				history.String("kind", spec.retainedUniqueHistory ? "retained-chain" : "cold");
				if (spec.retainedUniqueHistory && spec.depth > 5)
					history.Number("previousDepth", spec.depth - 1);
				if (spec.retainedUniqueHistory) {
					history.Number("preparedUniqueItemFlagId", PreparedCatacombsUniqueHistoryId);
					history.Boolean("preparedFlagSeededThisCase", spec.depth == 5);
					history.Boolean("preparedFlagPresentAtInput", historyAtCaseStart[PreparedCatacombsUniqueHistoryId]);
				}
				history.Key("inputFlags");
				WriteFlags(caseOutput, inputUniqueFlags);
			}
			document.Key("playerPreEntryContext");
			{
				JsonObject context(caseOutput);
				context.String("kind", "prepared-catacombs-entry-context");
				context.Boolean("precedingFloorGameplayInput", false);
				context.String("preparedInventorySeedPolicy", NativeCatacombsPreparedInventorySeedPolicy);
				context.Number("level", MyPlayer->plrlevel);
				context.Key("tile");
				WriteRoomPoint(caseOutput, preEntryTile);
				context.Key("future");
				WriteRoomPoint(caseOutput, preEntryFuture);
				context.Key("old");
				WriteRoomPoint(caseOutput, preEntryOld);
				context.Key("last");
				WriteRoomPoint(caseOutput, preEntryLast);
				context.Key("temp");
				WriteRoomPoint(caseOutput, preEntryTemp);
			}
			document.Key("playerGenerationInput");
			caseOutput << playerGenerationInput.str();
			document.Key("playerBeforePopulation");
			WriteCatacombsPlayer(caseOutput, *MyPlayer);
			document.Key("bestiary");
			WriteCatacombsBestiary(caseOutput);
			document.Key("worldBeforePopulation");
			WriteRoomWorld(caseOutput);
			document.Key("themesBeforePopulation");
			WriteCatacombsThemeState(caseOutput);
			SetRndSeed(spec.seed);
			HoldThemeRooms();
			afterHoldThemeRooms = GetLCGEngineState();
			InitGolems();
			afterInitGolems = GetLCGEngineState();
			InitObjects();
			afterInitObjects = GetLCGEngineState();
			if (captureStages) {
				document.Key("worldAfterInitialObjects");
				WriteRoomWorld(caseOutput);
			}
			ASSERT_TRUE(InitMonsters().has_value());
			afterInitMonsters = GetLCGEngineState();
			if (captureStages) {
				document.Key("worldAfterInitMonsters");
				WriteRoomWorld(caseOutput);
			}
			InitItems();
			afterInitItems = GetLCGEngineState();
			if (captureStages) {
				document.Key("worldAfterInitItems");
				WriteRoomWorld(caseOutput);
			}
			const std::array<bool, 128> outputUniqueFlagsAfterItems = CaptureFlags();
			if (spec.retainedUniqueHistory)
				ASSERT_TRUE(outputUniqueFlagsAfterItems[PreparedCatacombsUniqueHistoryId]);
			CreateThemeRooms();
			afterCreateThemeRooms = GetLCGEngineState();
			outputUniqueFlags = CaptureFlags();
			if (spec.retainedUniqueHistory)
				ASSERT_TRUE(outputUniqueFlags[PreparedCatacombsUniqueHistoryId]);
			document.Key("rngAfterPopulationStages");
			{
				JsonObject boundaries(caseOutput);
				boundaries.Number("afterHoldThemeRooms", afterHoldThemeRooms);
				boundaries.Number("afterInitGolems", afterInitGolems);
				boundaries.Number("afterInitObjects", afterInitObjects);
				boundaries.Number("afterInitMonsters", afterInitMonsters);
				boundaries.Number("afterInitItems", afterInitItems);
				boundaries.Number("afterCreateThemeRooms", afterCreateThemeRooms);
			}
			document.Key("uniqueItemHistoryAfterItems");
			WriteFlags(caseOutput, outputUniqueFlagsAfterItems);
			document.Key("uniqueItemHistoryOutput");
			WriteFlags(caseOutput, outputUniqueFlags);
			document.Key("populationResetRecords");
			WriteCatacombsResetRecords(caseOutput);
			document.Key("worldAfterThemeCreation");
			WriteRoomWorld(caseOutput);
			document.Key("themesAfterCreation");
			WriteCatacombsThemeState(caseOutput);
			beforeReady = GetLCGEngineState();
			CompleteFreshLevelForTesting(spec.entry);
			afterReady = GetLCGEngineState();
			document.Key("rngAfterLevelReady");
			{
				JsonObject boundaries(caseOutput);
				boundaries.Number("beforeReadySequence", beforeReady);
				boundaries.Number("afterReadySequence", afterReady);
				boundaries.String("sequence", "InitMissiles,InitCorpses,SavePreLighting,SyncPortals,SyncPlayerEntry,UpdateMonsterLights,UnstuckChargers,LoadGameLevelLightVision");
			}
			document.Key("ready");
			WriteLevelReadyState(caseOutput);
			document.Key("playerReady");
			WriteCatacombsPlayer(caseOutput, *MyPlayer);
		}
		caseOutput << "\r\n";
		caseOutput.flush();
		caseOutput.close();
		ASSERT_TRUE(caseOutput.good());
		ASSERT_LE(std::filesystem::file_size(casePath), 128U * 1024U * 1024U) << casePath;
		EXPECT_EQ(beforeReady, afterReady) << "The native fresh level-ready tail should not consume gameplay RNG.";
		if (spec.retainedUniqueHistory)
			carriedHistory = outputUniqueFlags;
		indexRows.push_back(spec);
	}
	{
		std::ofstream manifest(outputGuard.root / "manifest.json", std::ios::binary | std::ios::trunc);
		ASSERT_TRUE(manifest.good());
		{
			JsonObject document(manifest);
			document.String("format", "godevilution-native-catacombs-population");
			document.Number("version", captureStages ? 2 : 1);
			document.String("policy", multiplayer ? "quest-disabled-original-diablo-multiplayer-normal" : "quest-disabled-original-diablo-single-player-normal");
			document.Number("caseCount", indexRows.size());
			if (captureStages)
				document.String("capturedStages", "post-InitObjects,post-InitMonsters,post-InitItems");
			document.Number("catacombsFirstLevel", 5);
			document.Number("catacombsLastLevel", 8);
			document.Number("nativeSolSourceBytes", sourceSolByteCount);
			document.Number("nativeSolEffectiveSlots", MAXTILES);
			document.Number("nativeSolReserved0x40Bytes", reservedSolFlagCount);
			document.Number("nativeMegaTileCount", megaTileCount);
			document.Number("populationMatrixCaseCount", 30);
			document.Number("aiEligibilityCoverageCaseCount", 2);
			document.String("aiEligibilityCoverageCases", "appended after the 30-case matrix: Warrior L8 Main seed 0 for Rhino eligibility and seed 1 for Magma/Fat eligibility");
			document.String("preparedInventorySeedPolicy", NativeCatacombsPreparedInventorySeedPolicy);
			document.String("coldWarriorCases", "seeds 2588 and 743271966; levels 5 through 8; Main and Previous");
			document.String("sorcererCases", "seed 2588; levels 5 through 8; Main");
			document.String("townWarpDownCases", "seed 2588; Warrior and Sorcerer; level 5");
			document.String("retainedUniqueHistoryCases", "seed 2588; Warrior and Sorcerer; level 5 seeds unique-item flag 7, then final flags carry through levels 6 to 8");
			document.Number("preparedUniqueItemFlagId", PreparedCatacombsUniqueHistoryId);
			document.String("playerPreEntryContext", "prepared context without preceding-floor gameplay; future 75,68; last 13,17; temp 19,23; InitPlayer supplies tile/old and ready sync supplies future");
			document.Key("readyTail");
			JsonString(manifest, "InitMissiles,InitCorpses,SavePreLighting,SyncPortals,SyncPlayerEntry,UpdateMonsterLights,UnstuckChargers,LoadGameLevelLightVision");
			document.Key("cases");
			manifest << '[';
			for (size_t index = 0; index < indexRows.size(); ++index) {
				if (index != 0)
					manifest << ',';
				const NativeCatacombsPopulationCase &spec = indexRows[index];
				JsonObject row(manifest);
				row.String("file", "cases/" + std::string(CatacombsClassSymbol(spec.heroClass)) + (spec.aiEligibilityCoverage ? "-ai-eligibility-coverage" : spec.retainedUniqueHistory ? "-retained-history"
				                           : spec.entry == ENTRY_TWARPDN                                                                                                                ? "-town-warp-down"
				                                                                                                                                                                        : "-cold")
				        + "-l" + std::to_string(spec.depth) + "-" + CatacombsEntrySymbol(spec.entry) + "-" + std::to_string(spec.seed) + ".json");
				row.String("characterClass", CatacombsClassSymbol(spec.heroClass));
				row.Number("characterClassId", spec.heroClass);
				row.Number("levelId", spec.depth);
				row.String("entry", CatacombsEntrySymbol(spec.entry));
				row.Number("inputSeed", spec.seed);
				if (spec.aiEligibilityCoverage)
					row.String("casePurpose", "native-ai-eligibility-coverage");
				row.Boolean("retainedUniqueHistory", spec.retainedUniqueHistory);
				if (spec.retainedUniqueHistory) {
					row.Number("preparedUniqueItemFlagId", PreparedCatacombsUniqueHistoryId);
					row.Boolean("preparedUniqueItemFlagSeeded", spec.depth == 5);
				}
			}
			manifest << ']';
		}
		manifest << "\r\n";
		manifest.flush();
		ASSERT_TRUE(manifest.good());
		manifest.close();
		ASSERT_TRUE(manifest.good());
	}
	ASSERT_LE(std::filesystem::file_size(outputGuard.root / "manifest.json"), 4U * 1024U * 1024U);
	ASSERT_LE(std::filesystem::file_size(outputGuard.root / "native-catacombs-sources.json"), 4U * 1024U * 1024U);
	ASSERT_TRUE(outputGuard.WriteStatus("complete")) << "Could not finalize the capture status marker.";
	outputGuard.complete = true;
#endif
}

// Additive staged reference; the earlier fixture retains its exact version-1 bytes.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsFreshPopulationStages)
{
	CaptureNativeCatacombsPopulationOracle(
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_ARCHIVE"),
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_REFERENCE_DIRECTORY"), true);
}

// The staged matrix with gbIsMultiplayer: 150% scatter, full monster life, multiplayer shrines and item pools.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsFreshMultiplayerPopulationStages)
{
	CaptureNativeCatacombsPopulationOracle(
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_ARCHIVE"),
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_MULTIPLAYER_POPULATION_REFERENCE_DIRECTORY"), true, true);
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsFreshPopulation)
{
	CaptureNativeCatacombsPopulationOracle(
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_ARCHIVE"),
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_REFERENCE_DIRECTORY"));
}

TEST_F(ThemeRoomGenerationTest, CaptureComposedOrdinaryPopulation)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_FULL_POPULATION_ORACLE_DIRECTORY"), false);
}

TEST_F(ThemeRoomGenerationTest, CaptureFreshOrdinaryPopulation)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_FRESH_POPULATION_ORACLE_DIRECTORY"), true);
}

// The same fresh matrix with gbIsMultiplayer: full monster life, 150% scatter, multiplayer shrines and spell pools.
TEST_F(ThemeRoomGenerationTest, CaptureFreshOrdinaryMultiplayerPopulation)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_FRESH_MULTIPLAYER_POPULATION_ORACLE_DIRECTORY"), true, false, false, std::nullopt, true);
}

// Separately selected exact playable-profile tuple; established corpus bytes stay frozen.
TEST_F(ThemeRoomGenerationTest, CaptureNativeRogueL1Population)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_NATIVE_ROGUE_L1_ORACLE_DIRECTORY"), true, false, true);
}

// Separate opt-in birth tuples; no widening or recapture of earlier corpora.
TEST_F(ThemeRoomGenerationTest, CaptureNativeWarriorL1Population)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_NATIVE_WARRIOR_L1_ORACLE_DIRECTORY"), true, false, false, HeroClass::Warrior);
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeSorcererL1Population)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_NATIVE_SORCERER_L1_ORACLE_DIRECTORY"), true, false, false, HeroClass::Sorcerer);
}

TEST_F(ThemeRoomGenerationTest, CaptureFreshLevelReady)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_LEVEL_READY_ORACLE_DIRECTORY"), true, true);
}

// Actual L2 object operations on controlled SOL/occupancy inputs. No MPQ is required.
