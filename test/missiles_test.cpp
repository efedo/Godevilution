#include <gmock/gmock.h>
#include <gtest/gtest.h>

#include <ankerl/unordered_dense.h>

#include <array>
#include <filesystem>
#include <list>
#include <limits>
#include <optional>
#include <string>
#include <system_error>
#include <utility>
#include <vector>

#include "application/game_mode.hpp"
#include "application/headless_mode.hpp"
#include "engine/audio/effects.h"
#include "engine/audio/sound.h"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "engine/world.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/triggers.h"
#include "game/levels/tile.hpp"
#include "game/missiles/missiles.hpp"
#include "game/players/players.hpp"
#include "game/spells/spells.hpp"
#include "network/storm/storm_net.hpp"
#include "tables/misdat.h"
#include "tables/playerdat.hpp"
#include "tables/spelldat.h"
#include "utils/paths.h"

namespace devilution {
void TestPlayerCheckNewPath(Player &player);
}

using namespace devilution;
using ::testing::AllOf;
using ::testing::Gt;
using ::testing::Lt;
using ::testing::Pair;
using ::testing::UnorderedElementsAre;

namespace {

constexpr Point MonkSearchPosition { 40, 40 };

class ScopedTestAssetsPath {
public:
	ScopedTestAssetsPath()
	    : saved_(paths::AssetsPath())
	{
	}

	~ScopedTestAssetsPath()
	{
		paths::SetAssetsPath(saved_);
	}

	ScopedTestAssetsPath(const ScopedTestAssetsPath &) = delete;
	ScopedTestAssetsPath &operator=(const ScopedTestAssetsPath &) = delete;

private:
	std::string saved_;
};

std::optional<std::filesystem::path> FindHellfireAssetsPath()
{
	const std::array<std::filesystem::path, 2> starts {
	    std::filesystem::current_path(),
	    std::filesystem::path(paths::BasePath()),
	};
	for (std::filesystem::path start : starts) {
		std::error_code error;
		start = std::filesystem::absolute(start, error);
		if (error)
			continue;
		if (std::filesystem::is_regular_file(start, error))
			start = start.parent_path();
		for (std::filesystem::path ancestor = start; !ancestor.empty();) {
			const std::filesystem::path candidate = ancestor / "mods" / "Hellfire";
			if (std::filesystem::is_regular_file(candidate / "txtdata" / "spells" / "spelldat.tsv", error)
			    && std::filesystem::is_regular_file(candidate / "txtdata" / "missiles" / "misdat.tsv", error)) {
				return candidate;
			}
			const std::filesystem::path parent = ancestor.parent_path();
			if (parent == ancestor)
				break;
			ancestor = parent;
			error.clear();
		}
	}
	return std::nullopt;
}

class ScopedNativeMonkSearchState {
public:
	ScopedNativeMonkSearchState()
	    : savedSpellsData_()
	    , savedAssetsPath_(paths::AssetsPath())
	    , savedPlayers_()
	    , savedMyPlayer_(MyPlayer)
	    , savedMyPlayerId_(MyPlayerId)
	    , savedMissiles_()
	    , savedMissilePreFlag_(MissilePreFlag)
	    , savedLevelId_(currentLevel().id())
	    , savedSearchTile_(tileAt(MonkSearchPosition))
	    , savedViewPosition_(viewPosition())
	    , savedLightManager_(CurrentLightManager)
	    , savedAutomapManager_(CurrentAutomapManager)
	    , savedHeadlessMode_(HeadlessMode)
	    , savedHellfireMode_(gbIsHellfire)
	    , savedSoundInitialized_(gbSndInited)
	    , savedSfxDelay_(sfxdelay)
	    , savedSfxId_(sfxdnum)
	    , savedRngState_(GetLCGEngineState())
	{
		SpellsData.swap(savedSpellsData_);
		Players.swap(savedPlayers_);
		Missiles.swap(savedMissiles_);
		SpellsData.clear();
		MyPlayer = nullptr;
		MyPlayerId = 0;
		MissilePreFlag = false;
		HeadlessMode = true;
		gbIsHellfire = true;
		gbSndInited = false;
		sfxdelay = 0;
		sfxdnum = SfxID::None;
		currentLevel().setId({ 1, DTYPE_CATHEDRAL, false, SL_NONE });
		tileAt(MonkSearchPosition) = Tile {};
		CurrentAutomapManager.SetAutoMapShowItems(false);
		SetRndSeed(0x4D534541);
	}

	~ScopedNativeMonkSearchState()
	{
		SpellsData.swap(savedSpellsData_);
		paths::SetAssetsPath(savedAssetsPath_);
		Players.swap(savedPlayers_);
		MyPlayerId = savedMyPlayerId_;
		MyPlayer = savedMyPlayer_;
		Missiles.swap(savedMissiles_);
		MissilePreFlag = savedMissilePreFlag_;
		currentLevel().setId(savedLevelId_);
		tileAt(MonkSearchPosition) = savedSearchTile_;
		viewPosition() = savedViewPosition_;
		CurrentLightManager = savedLightManager_;
		CurrentAutomapManager = savedAutomapManager_;
		HeadlessMode = savedHeadlessMode_;
		gbIsHellfire = savedHellfireMode_;
		gbSndInited = savedSoundInitialized_;
		sfxdelay = savedSfxDelay_;
		sfxdnum = savedSfxId_;
		SetRndSeed(savedRngState_);
	}

	ScopedNativeMonkSearchState(const ScopedNativeMonkSearchState &) = delete;
	ScopedNativeMonkSearchState &operator=(const ScopedNativeMonkSearchState &) = delete;

private:
	ScopedMissileDataForTest savedMissileData_;
	std::vector<SpellData> savedSpellsData_;
	std::string savedAssetsPath_;
	std::vector<Player> savedPlayers_;
	Player *savedMyPlayer_;
	uint8_t savedMyPlayerId_;
	std::list<Missile> savedMissiles_;
	bool savedMissilePreFlag_;
	LevelId savedLevelId_;
	Tile savedSearchTile_;
	Point savedViewPosition_;
	LightManager savedLightManager_;
	AutomapManager savedAutomapManager_;
	bool savedHeadlessMode_;
	bool savedHellfireMode_;
	bool savedSoundInitialized_;
	int savedSfxDelay_;
	SfxID savedSfxId_;
	uint32_t savedRngState_;
};

class NativeMonkSearchTest : public ::testing::Test {
protected:
	void SetUp() override
	{
		const std::optional<std::filesystem::path> assetsPath = FindHellfireAssetsPath();
		ASSERT_TRUE(assetsPath.has_value()) << "The checked-in loose Hellfire tables are required for this native test.";
		paths::SetAssetsPath(assetsPath->string() + "/");

		LoadSpellData();
		LoadMissileData();
		ASSERT_GT(SpellsData.size(), static_cast<size_t>(SpellID::Search));
		ASSERT_TRUE(IsMissileDataAvailable(MissileID::Search));
		ASSERT_EQ(GetSpellData(SpellID::Search).sMissiles[0], MissileID::Search);
		ASSERT_EQ(GetSpellData(SpellID::Search).sMissiles[1], MissileID::Null);
		ASSERT_NE(GetMissileData(MissileID::Search).addFn, nullptr);
		ASSERT_NE(GetMissileData(MissileID::Search).processFn, nullptr);

		Players.emplace_back(Player {});
		Player &player = Players.front();
		player._pClass = HeroClass::Monk;
		ASSERT_EQ(player.getCharacterLevel(), 1);
		player.position.tile = MonkSearchPosition;
		player.position.future = MonkSearchPosition;
		player.executedSpell = { SpellID::Search, SpellType::Skill, -1, 0 };
		player.life = { 6400, 6400, 6400, 6400, 100 };
		player.mana = { 3200, 3200, 3200, 3200, 100 };
		player._pmode = PM_STAND;
		MyPlayerId = 0;
		MyPlayer = &player;
	}

	void CastSearch()
	{
		ASSERT_NE(MyPlayer, nullptr);
		CastSpell(*MyPlayer, SpellID::Search, MonkSearchPosition, MonkSearchPosition, 0);
	}

	ScopedNativeMonkSearchState state_;
};

class ScopedTownPortalState {
public:
	explicit ScopedTownPortalState(Point portalPosition)
	    : portalPosition_(portalPosition)
	    , savedPlayers_(std::move(Players))
	    , savedMyPlayer_(MyPlayer)
	    , savedMyPlayerId_(MyPlayerId)
	    , savedLevelType_(levelType())
	    , savedPortalTile_(tileAt(portalPosition))
	    , savedLightManager_(CurrentLightManager)
	    , savedFullyLitLightTable_(CurrentLightManager.fullyLitLightTable_)
	    , savedFullyDarkLightTable_(CurrentLightManager.fullyDarkLightTable_)
	    , savedTrigFlag_(trigFlag())
	    , savedNumTriggers_(numTriggers())
	{
		Players.clear();
		MyPlayer = nullptr;
		MyPlayerId = 0;
		levelType() = DTYPE_TOWN;
		InitNoTriggers();
	}

	~ScopedTownPortalState()
	{
		tileAt(portalPosition_) = savedPortalTile_;
		levelType() = savedLevelType_;
		CurrentLightManager = savedLightManager_;
		CurrentLightManager.fullyLitLightTable_ = savedFullyLitLightTable_;
		CurrentLightManager.fullyDarkLightTable_ = savedFullyDarkLightTable_;
		Players = std::move(savedPlayers_);
		MyPlayerId = savedMyPlayerId_;
		MyPlayer = savedMyPlayer_;
		InitNoTriggers();
		trigFlag() = savedTrigFlag_;
		numTriggers() = savedNumTriggers_;
	}

	ScopedTownPortalState(const ScopedTownPortalState &) = delete;
	ScopedTownPortalState &operator=(const ScopedTownPortalState &) = delete;

private:
	Point portalPosition_;
	std::vector<Player> savedPlayers_;
	Player *savedMyPlayer_;
	uint8_t savedMyPlayerId_;
	dungeon_type savedLevelType_;
	Tile savedPortalTile_;
	LightManager savedLightManager_;
	uint8_t *savedFullyLitLightTable_;
	uint8_t *savedFullyDarkLightTable_;
	bool savedTrigFlag_;
	int savedNumTriggers_;
};

void AssertNoActiveTriggers()
{
	ASSERT_EQ(numTriggers(), 0);
	ASSERT_FALSE(trigFlag());
}

class ScopedLoopbackNetwork {
public:
	ScopedLoopbackNetwork()
	    : initialized_(SNetInitializeProvider(SELCONN_LOOPBACK, nullptr))
	{
	}

	~ScopedLoopbackNetwork()
	{
		if (initialized_)
			SNetDestroy();
	}

	ScopedLoopbackNetwork(const ScopedLoopbackNetwork &) = delete;
	ScopedLoopbackNetwork &operator=(const ScopedLoopbackNetwork &) = delete;

	bool initialized() const { return initialized_; }

private:
	bool initialized_;
};

} // namespace

void TestArrowRotatesUniformly(Missile &missile, int startingFrame, int leftFrame, int rightFrame)
{
	ankerl::unordered_dense::map<int, unsigned> observed {};
	for (auto i = 0; i < 100; i++) {
		missile._miAnimFrame = startingFrame;
		TestRotateBlockedMissile(missile);
		observed[missile._miAnimFrame]++;
	}

	EXPECT_THAT(observed, UnorderedElementsAre(Pair(leftFrame, AllOf(Gt(30U), Lt(70U))), Pair(rightFrame, AllOf(Gt(30U), Lt(70U))))) << "Arrows should rotate either direction roughly 50% of the time";
}

void TestAnimatedMissileRotatesUniformly(Missile &missile, int startingDir, int leftDir, int rightDir)
{
	ankerl::unordered_dense::map<int, unsigned> observed {};
	for (auto i = 0; i < 100; i++) {
		missile.setFrameGroupRaw(startingDir);
		TestRotateBlockedMissile(missile);
		observed[missile.getFrameGroupRaw()]++;
	}

	EXPECT_THAT(observed, UnorderedElementsAre(Pair(leftDir, AllOf(Gt(30U), Lt(70U))), Pair(rightDir, AllOf(Gt(30U), Lt(70U))))) << "Animated missiles should rotate either direction roughly 50% of the time";
}

TEST(Missiles, RotateBlockedMissileArrow)
{
	Players.resize(1);
	MyPlayerId = 0;
	MyPlayer = &Players[MyPlayerId];
	*MyPlayer = {};
	LoadMissileData();

	const devilution::Player &player = Players[0];
	// missile can be a copy or a reference, there's no nullptr check and the functions that use it don't expect the instance to be part of a global structure so it doesn't really matter for this use.
	Missile missile = *AddMissile({ 0, 0 }, { 0, 0 }, Direction::South, MissileID::Arrow, TARGET_MONSTERS, player, 0, 0);

	// Arrows have a hardcoded frame count and use 1-indexed sprites
	EXPECT_EQ(missile._miAnimFrame, 1);

	TestArrowRotatesUniformly(missile, 5, 4, 6);
	TestArrowRotatesUniformly(missile, 1, 16, 2);
	TestArrowRotatesUniformly(missile, 16, 15, 1);

	// All other missiles use the number of 0-indexed sprites defined in MissileSpriteData
	missile = *AddMissile({ 0, 0 }, { 0, 0 }, Direction::South, MissileID::Firebolt, TARGET_MONSTERS, player, 0, 0);
	EXPECT_EQ(missile.getFrameGroupRaw(), 0);
	TestAnimatedMissileRotatesUniformly(missile, 5, 4, 6);
	TestAnimatedMissileRotatesUniformly(missile, 0, 15, 1);
	TestAnimatedMissileRotatesUniformly(missile, 15, 14, 0);
}

TEST(Missiles, GetDirection8)
{
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 15, 15 }));
	EXPECT_EQ(Direction::SouthWest, GetDirection({ 0, 0 }, { 0, 15 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 8, 15 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 8, 8 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 15, 8 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 15, 7 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 11, 7 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 8, 11 }));
	EXPECT_EQ(Direction::North, GetDirection({ 15, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction::NorthEast, GetDirection({ 0, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 8, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 8, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 15, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 15, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 11, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 8, 11 }, { 0, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 15 }, { 15, 0 }));
	EXPECT_EQ(Direction::SouthEast, GetDirection({ 0, 0 }, { 15, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 8 }, { 15, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 8 }, { 8, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 15 }, { 8, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 15 }, { 7, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 11 }, { 7, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 8 }, { 11, 0 }));

	EXPECT_EQ(Direction::South, GetDirection({ 1, 1 }, { 2, 2 }));
	EXPECT_EQ(Direction::SouthWest, GetDirection({ 1, 1 }, { 1, 2 }));
	EXPECT_EQ(Direction::West, GetDirection({ 1, 1 }, { 0, 2 }));
	EXPECT_EQ(Direction::NorthWest, GetDirection({ 1, 1 }, { 0, 1 }));
	EXPECT_EQ(Direction::North, GetDirection({ 1, 1 }, { 0, 0 }));
	EXPECT_EQ(Direction::NorthEast, GetDirection({ 1, 1 }, { 1, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 1, 1 }, { 2, 0 }));
	EXPECT_EQ(Direction::SouthEast, GetDirection({ 1, 1 }, { 2, 1 }));

	EXPECT_EQ(Direction::SouthWest, GetDirection({ 0, 0 }, { 0, 0 })) << "GetDirection is expected to default to Direction::SouthWest when the points occupy the same tile";
}

TEST(Missiles, GetDirection16)
{
	EXPECT_EQ(Direction16::South, GetDirection16({ 0, 0 }, { 15, 15 }));
	EXPECT_EQ(Direction16::SouthWest, GetDirection16({ 0, 0 }, { 0, 15 }));
	EXPECT_EQ(Direction16::South_SouthWest, GetDirection16({ 0, 0 }, { 8, 15 }));
	EXPECT_EQ(Direction16::South, GetDirection16({ 0, 0 }, { 8, 8 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 0, 0 }, { 15, 8 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 0, 0 }, { 15, 7 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 0, 0 }, { 11, 7 }));
	EXPECT_EQ(Direction16::South, GetDirection16({ 0, 0 }, { 8, 11 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 15, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction16::NorthEast, GetDirection16({ 0, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthEast, GetDirection16({ 8, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 8, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 15, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 15, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 11, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 8, 11 }, { 0, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 0, 15 }, { 15, 0 }));
	EXPECT_EQ(Direction16::SouthEast, GetDirection16({ 0, 0 }, { 15, 0 }));
	EXPECT_EQ(Direction16::East_SouthEast, GetDirection16({ 0, 8 }, { 15, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 0, 8 }, { 8, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 0, 15 }, { 8, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 0, 15 }, { 7, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 0, 11 }, { 7, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 0, 8 }, { 11, 0 }));

	EXPECT_EQ(Direction16::South, GetDirection16({ 2, 2 }, { 3, 3 }));
	EXPECT_EQ(Direction16::South_SouthWest, GetDirection16({ 2, 2 }, { 3, 4 }));
	EXPECT_EQ(Direction16::SouthWest, GetDirection16({ 2, 2 }, { 2, 4 }));
	EXPECT_EQ(Direction16::West_SouthWest, GetDirection16({ 2, 2 }, { 1, 4 }));
	EXPECT_EQ(Direction16::West, GetDirection16({ 2, 2 }, { 1, 3 }));
	EXPECT_EQ(Direction16::West_NorthWest, GetDirection16({ 2, 2 }, { 0, 3 }));
	EXPECT_EQ(Direction16::NorthWest, GetDirection16({ 2, 2 }, { 0, 2 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 2, 2 }, { 0, 1 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 2, 2 }, { 1, 1 }));
	EXPECT_EQ(Direction16::North_NorthEast, GetDirection16({ 2, 2 }, { 1, 0 }));
	EXPECT_EQ(Direction16::NorthEast, GetDirection16({ 2, 2 }, { 2, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 2, 2 }, { 3, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 2, 2 }, { 3, 1 }));
	EXPECT_EQ(Direction16::East_SouthEast, GetDirection16({ 2, 2 }, { 4, 1 }));
	EXPECT_EQ(Direction16::SouthEast, GetDirection16({ 2, 2 }, { 4, 2 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 2, 2 }, { 4, 3 }));

	EXPECT_EQ(Direction16::South_SouthWest, GetDirection16({ 0, 0 }, { 0, 0 })) << "GetDirection16 is expected to default to Direction16::South_SouthWest when the points occupy the same tile";
}

TEST(Missiles, ProcessTownPortalRetainsOpeningAtDurationOneAndDeletesAtZero)
{
	constexpr Point portalPosition { 10, 10 };
	ASSERT_NO_FATAL_FAILURE(AssertNoActiveTriggers());
	ScopedTownPortalState stateGuard(portalPosition);

	// Keep the fixture in the already-open state; this characterizes lifetime semantics without loading portal sprites.
	Missile opening{};
	opening._mitype = MissileID::TownPortal;
	opening.position.tile = portalPosition;
	opening.position.start = portalPosition;
	opening.duration = 100;
	opening.var1 = -1;
	opening._miDelFlag = false;
	opening._mlid = NO_LIGHT;
	opening._miAnimType = MissileGraphicID::None;
	opening.setFrameGroup<PortalFrame>(PortalFrame::Idle);

	for (int tick = 0; tick < 99; tick++) {
		ProcessTownPortal(opening);
		EXPECT_EQ(opening.duration, 99 - tick);
		EXPECT_FALSE(opening._miDelFlag);
	}
	EXPECT_EQ(opening.duration, 1);
	ProcessTownPortal(opening);
	EXPECT_EQ(opening.duration, 1);
	EXPECT_FALSE(opening._miDelFlag);

	Missile expired{};
	expired._mitype = MissileID::TownPortal;
	expired.position.tile = portalPosition;
	expired.position.start = portalPosition;
	expired.duration = 0;
	expired.var1 = -1;
	expired._miDelFlag = false;
	expired._mlid = NO_LIGHT;

	ProcessTownPortal(expired);
	EXPECT_EQ(expired.duration, 0);
	EXPECT_TRUE(expired._miDelFlag);
}

TEST(Missiles, ProcessTownPortalPrecedesSameTileLevelTrigger)
{
	constexpr Point portalPosition { 25, 29 };
	ASSERT_NO_FATAL_FAILURE(AssertNoActiveTriggers());
	ScopedTownPortalState stateGuard(portalPosition);
	// ProcessTownPortal emits CMD_WARP for the local player; loopback keeps that command in-process.
	ScopedLoopbackNetwork networkGuard;
	ASSERT_TRUE(networkGuard.initialized());

	Players.emplace_back();
	Player &player = Players.front();
	MyPlayerId = 0;
	MyPlayer = &player;
	player.plractive = true;
	player.plrlevel = currentLevelNumber();
	player.plrIsOnSetLevel = false;
	player._pLvlChanging = false;
	player._pmode = PM_STAND;
	player.position.tile = portalPosition;
	player.position.future = portalPosition;
	player.position.last = portalPosition;
	player.position.old = portalPosition;
	player.position.temp = portalPosition;

	// InitTownTriggers places its first native Cathedral endpoint trigger at (25, 29).
	InitTownTriggers();
	ASSERT_GT(numTriggers(), 0);

	Missile portal{};
	portal._mitype = MissileID::TownPortal;
	portal.position.tile = portalPosition;
	portal.position.start = portalPosition;
	portal.duration = 2;
	portal.var1 = -1;
	portal._miDelFlag = false;
	portal._mlid = NO_LIGHT;
	portal._miAnimType = MissileGraphicID::None;
	portal.setFrameGroup<PortalFrame>(PortalFrame::Idle);

	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_NEWLVL);
	const auto levelBeforeTriggerCheck = currentLevelNumber();
	CheckTriggers();
	EXPECT_EQ(player._pmode, PM_NEWLVL);
	EXPECT_EQ(currentLevelNumber(), levelBeforeTriggerCheck);

	player._pmode = PM_WALK_NORTHWARDS;
	player._pLvlChanging = false;
	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_WALK_NORTHWARDS);

	player._pmode = PM_STAND;
	player.position.tile = portalPosition + Displacement { 1, 0 };
	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_STAND);

	player.position.tile = portalPosition;
	player._pLvlChanging = true;
	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_STAND);
}

TEST(MissileData, ScopedTestLoadRestoresOriginalStorage)
{
	const bool originalSearchAvailable = IsMissileDataAvailable(MissileID::Search);
	const MissileData *originalSearch = originalSearchAvailable ? &GetMissileData(MissileID::Search) : nullptr;
	ScopedTestAssetsPath assetsPathGuard;

	const std::optional<std::filesystem::path> assetsPath = FindHellfireAssetsPath();
	ASSERT_TRUE(assetsPath.has_value()) << "The checked-in loose Hellfire tables are required for this native test.";
	{
		ScopedMissileDataForTest originalDataGuard;
		EXPECT_FALSE(IsMissileDataAvailable(MissileID::Search));
		paths::SetAssetsPath(assetsPath->string() + "/");
		LoadMissileData();
		ASSERT_TRUE(IsMissileDataAvailable(MissileID::Search));
		EXPECT_NE(GetMissileData(MissileID::Search).addFn, nullptr);
		const MissileData *loadedSearch = &GetMissileData(MissileID::Search);
		const MissileFileData *loadedArrow = &GetMissileSpriteData(MissileGraphicID::Arrow);
		const uint8_t loadedArrowAnimDelay = loadedArrow->animDelay(0);
		const uint8_t loadedArrowAnimLength = loadedArrow->animLen(0);
		{
			ScopedMissileDataForTest replacementDataGuard;
			EXPECT_FALSE(IsMissileDataAvailable(MissileID::Search));
			paths::SetAssetsPath(assetsPath->string() + "/");
			LoadMissileData();
			ASSERT_TRUE(IsMissileDataAvailable(MissileID::Search));
			EXPECT_NE(&GetMissileData(MissileID::Search), loadedSearch);
			EXPECT_NE(&GetMissileSpriteData(MissileGraphicID::Arrow), loadedArrow);
		}
		EXPECT_EQ(&GetMissileData(MissileID::Search), loadedSearch);
		EXPECT_EQ(&GetMissileSpriteData(MissileGraphicID::Arrow), loadedArrow);
		EXPECT_EQ(loadedArrow->animDelay(0), loadedArrowAnimDelay);
		EXPECT_EQ(loadedArrow->animLen(0), loadedArrowAnimLength);
	}

	EXPECT_EQ(IsMissileDataAvailable(MissileID::Search), originalSearchAvailable);
	if (originalSearchAvailable) {
		ASSERT_NE(originalSearch, nullptr);
		EXPECT_EQ(&GetMissileData(MissileID::Search), originalSearch);
	}
}

TEST_F(NativeMonkSearchTest, UsesHellfireRegistrationAndExpiresAfterItsNativeDuration)
{
	const SpellData &searchSpell = GetSpellData(SpellID::Search);
	ASSERT_EQ(searchSpell.sMissiles[0], MissileID::Search);
	EXPECT_NE(GetMissileData(MissileID::Search).addFn, nullptr);
	EXPECT_NE(GetMissileData(MissileID::Search).processFn, nullptr);
	EXPECT_EQ(GetMissileData(MissileID::Search).graphic, MissileGraphicID::None);
	EXPECT_TRUE(HasAnyOf(GetMissileData(MissileID::Search).flags, MissileDataFlags::Invisible));
	EXPECT_EQ(GetMissileData(MissileID::Search).movementDistribution, MissileMovementDistribution::Disabled);

	const int initialLife = MyPlayer->life.current;
	const int initialMana = MyPlayer->mana.current;
	const uint32_t initialRngState = GetLCGEngineState();
	// DLL import thunks make callback address equality unreliable; the exact lifecycle below verifies registration behavior.
	CastSearch();

	ASSERT_EQ(Missiles.size(), 1U);
	EXPECT_EQ(Missiles.front()._mitype, MissileID::Search);
	EXPECT_EQ(Missiles.front().duration, 247);
	EXPECT_FALSE(Missiles.front()._miDelFlag);
	EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_EQ(MyPlayer->life.current, initialLife);
	EXPECT_EQ(MyPlayer->mana.current, initialMana);
	EXPECT_EQ(GetLCGEngineState(), initialRngState);

	ProcessMissiles();
	ASSERT_EQ(Missiles.size(), 1U);
	EXPECT_EQ(Missiles.front().duration, 246);
	EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());

	for (int tick = 0; tick < 245; tick++)
		ProcessMissiles();
	ASSERT_EQ(Missiles.size(), 1U);
	EXPECT_EQ(Missiles.front().duration, 1);
	EXPECT_FALSE(Missiles.front()._miDelFlag);
	EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());

	ProcessMissiles();
	EXPECT_TRUE(Missiles.empty());
	EXPECT_FALSE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_EQ(MyPlayer->life.current, initialLife);
	EXPECT_EQ(MyPlayer->mana.current, initialMana);
	EXPECT_EQ(GetLCGEngineState(), initialRngState);
}

TEST_F(NativeMonkSearchTest, PlayerCastCreatesSearchBeforeThePairedMissilePass)
{
	const std::optional<std::filesystem::path> hellfireAssets = FindHellfireAssetsPath();
	ASSERT_TRUE(hellfireAssets.has_value());
	const std::filesystem::path canonicalAssets = hellfireAssets->parent_path().parent_path() / "assets";
	ASSERT_TRUE(std::filesystem::is_regular_file(canonicalAssets / "txtdata" / "Experience.tsv"));
	ASSERT_TRUE(std::filesystem::is_regular_file(canonicalAssets / "txtdata" / "classes" / "classdat.tsv"));

	// Player/class tables are process-lifetime canonical data in the existing table-test convention;
	// only the spell and missile tables below are switched back to Hellfire.
	paths::SetAssetsPath(canonicalAssets.string() + "/");
	LoadPlayerDataFiles();
	paths::SetAssetsPath(hellfireAssets->string() + "/");
	const PlayerStartingLoadoutData &monkStartingLoadout = GetPlayerStartingLoadoutForClass(HeroClass::Monk);
	ASSERT_EQ(monkStartingLoadout.skill, SpellID::Search);

	Player &player = *MyPlayer;
	player._pClass = HeroClass::Monk;
	player.plractive = true;
	player.plrlevel = 1;
	player.lightId = NO_LIGHT;
	player.position.tile = MonkSearchPosition;
	player.position.future = MonkSearchPosition;
	player.position.old = MonkSearchPosition;
	player.direction = Direction::South;
	player._pgfxnum = static_cast<uint8_t>(PlayerWeaponGraphic::Unarmed);
	player.clearPath();
	player.setAnimations();
	player._pAblSpells = GetSpellBitmask(monkStartingLoadout.skill);
	player.life = { 6400, 6400, 6400, 6400, 100 };
	player.mana = { 3200, 3200, 3200, 3200, 100 };
	player._pmode = PM_STAND;
	player.queuedSpell = { SpellID::Search, SpellType::Skill, -1, 0 };
	player.destAction = ACTION_SPELL;
	player.destParam1 = MonkSearchPosition.x;
	player.destParam2 = MonkSearchPosition.y;

	ASSERT_EQ(player.getCharacterLevel(), 1);
	ASSERT_EQ(player.plrlevel, currentLevelNumber());
	ASSERT_TRUE(player.plractive);
	ASSERT_NE(player._pAblSpells & GetSpellBitmask(SpellID::Search), 0U);
	const PlayerAnimData &monkAnimationData = GetPlayerAnimDataForClass(HeroClass::Monk);
	ASSERT_EQ(player._pSFrames, monkAnimationData.castingFrames);
	ASSERT_EQ(player._pSFNum, monkAnimationData.castingActionFrame);
	ASSERT_GT(player._pSFrames, 1);
	ASSERT_GE(player._pSFNum, 0);
	ASSERT_LT(player._pSFNum, player._pSFrames - 1);

	const int initialLife = player.life.current;
	const int initialMana = player.mana.current;
	const uint32_t initialRngState = GetLCGEngineState();
	TestPlayerCheckNewPath(player);
	ASSERT_EQ(player._pmode, PM_SPELL);
	ASSERT_EQ(player.executedSpell.spellId, SpellID::Search);
	ASSERT_EQ(player.executedSpell.spellType, SpellType::Skill);
	ASSERT_EQ(player.executedSpell.spellLevel, 0);
	EXPECT_EQ(viewPosition(), MonkSearchPosition);

	bool castFrameVisited = false;
	bool createdOnCastFrame = false;
	ASSERT_GT(player.animInfo.numberOfFrames, 0);
	ASSERT_GT(player.animInfo.ticksPerFrame, 0);
	const int maximumCastPasses = static_cast<int>(player.animInfo.numberOfFrames) * player.animInfo.ticksPerFrame + 1;
	for (int pass = 0; pass < maximumCastPasses && Missiles.empty(); ++pass) {
		const bool onCastFrame = player._pmode == PM_SPELL && player.animInfo.currentFrame == player._pSFNum;
		if (onCastFrame) {
			ASSERT_FALSE(player.animInfo.isLastFrame());
			castFrameVisited = true;
		}
		ProcessPlayers();
		if (!Missiles.empty()) {
			createdOnCastFrame = onCastFrame;
			break;
		}
	}

	ASSERT_TRUE(castFrameVisited);
	ASSERT_TRUE(createdOnCastFrame);
	ASSERT_EQ(Missiles.size(), 1U);
	EXPECT_EQ(Missiles.front()._mitype, MissileID::Search);
	EXPECT_EQ(Missiles.front().duration, 247);
	EXPECT_FALSE(Missiles.front()._miDelFlag);
	EXPECT_EQ(player.life.current, initialLife);
	EXPECT_EQ(player.mana.current, initialMana);
	EXPECT_EQ(GetLCGEngineState(), initialRngState);

	// The ordinary caller order processes player actions before the paired missile update.
	ProcessMissiles();
	ASSERT_EQ(Missiles.size(), 1U);
	EXPECT_EQ(Missiles.front().duration, 246);
	EXPECT_FALSE(Missiles.front()._miDelFlag);
	EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_EQ(player.life.current, initialLife);
	EXPECT_EQ(player.mana.current, initialMana);
	EXPECT_EQ(GetLCGEngineState(), initialRngState);
	EXPECT_EQ(sfxdelay, 0);
}

TEST_F(NativeMonkSearchTest, RecastAddsDurationForTheSamePlayerSource)
{
	const uint32_t initialRngState = GetLCGEngineState();
	const int initialLife = MyPlayer->life.current;
	const int initialMana = MyPlayer->mana.current;
	CastSearch();
	ASSERT_EQ(Missiles.size(), 1U);
	Missile &firstSearch = Missiles.front();
	ASSERT_EQ(firstSearch.duration, 247);

	CastSearch();

	ASSERT_EQ(Missiles.size(), 2U);
	EXPECT_EQ(firstSearch.duration, 494);
	EXPECT_EQ(Missiles.back()._mitype, MissileID::Search);
	EXPECT_TRUE(Missiles.back()._miDelFlag);
	ProcessMissiles();
	ASSERT_EQ(Missiles.size(), 1U);
	EXPECT_EQ(&Missiles.front(), &firstSearch);
	EXPECT_EQ(firstSearch.duration, 493);
	EXPECT_FALSE(firstSearch._miDelFlag);
	EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_EQ(MyPlayer->life.current, initialLife);
	EXPECT_EQ(MyPlayer->mana.current, initialMana);
	EXPECT_EQ(GetLCGEngineState(), initialRngState);
}

TEST_F(NativeMonkSearchTest, RecastUsesTheStrictSignedIntDurationBoundary)
{
	constexpr int NewDuration = 247;
	const int belowBoundary = std::numeric_limits<int>::max() - NewDuration - 1;
	const int atBoundary = std::numeric_limits<int>::max() - NewDuration;
	const int aboveBoundary = std::numeric_limits<int>::max() - NewDuration + 1;
	struct BoundaryCase {
		int existingDuration;
		bool shouldAccumulate;
	};
	const std::array<BoundaryCase, 3> cases {{
	    { belowBoundary, true },
	    { atBoundary, false },
	    { aboveBoundary, false },
	}};
	const uint32_t initialRngState = GetLCGEngineState();
	const int initialLife = MyPlayer->life.current;
	const int initialMana = MyPlayer->mana.current;

	for (const BoundaryCase &testCase : cases) {
		SCOPED_TRACE(testCase.existingDuration);
		Missiles.clear();
		CastSearch();
		ASSERT_EQ(Missiles.size(), 1U);
		Missile &existingSearch = Missiles.front();
		existingSearch.duration = testCase.existingDuration;

		Missile *recast = AddMissile(
		    MonkSearchPosition,
		    MonkSearchPosition,
		    MyPlayer->direction,
		    MissileID::Search,
		    TARGET_MONSTERS,
		    *MyPlayer,
		    0,
		    0);

		ASSERT_NE(recast, nullptr);
		ASSERT_EQ(Missiles.size(), 2U);
		EXPECT_TRUE(recast->_miDelFlag);
		const int expectedDuration = testCase.shouldAccumulate ? testCase.existingDuration + NewDuration : testCase.existingDuration;
		EXPECT_EQ(existingSearch.duration, expectedDuration);

		ProcessMissiles();
		ASSERT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(&Missiles.front(), &existingSearch);
		EXPECT_EQ(existingSearch.duration, expectedDuration - 1);
		EXPECT_FALSE(existingSearch._miDelFlag);
		EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());
		EXPECT_EQ(MyPlayer->life.current, initialLife);
		EXPECT_EQ(MyPlayer->mana.current, initialMana);
		EXPECT_EQ(GetLCGEngineState(), initialRngState);
	}
}
