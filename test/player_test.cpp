#include "player_test.h"

#include <algorithm>
#include <array>
#include <filesystem>
#include <optional>
#include <string>
#include <string_view>
#include <tuple>
#include <vector>

#include <gtest/gtest.h>

#include "application/headless_mode.hpp"
#include "application/init.hpp"
#include "engine/assets.hpp"
#include "engine/cursor.h"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "network/protocol/msg.h"
#include "network/protocol/multi.h"
#include "utils/endian/endian_swap.hpp"
#include "utils/paths.h"
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif
#include "game/levels/dungeon_common.h"
#include "tables/playerdat.hpp"
#include "tables/spelldat.h"

using namespace devilution;

namespace {

// The fixture reads only loose TSV metadata, so the valid join path is deterministic
// even when this test runs before any archive-loading test.
std::optional<std::filesystem::path> FindLoosePlayerMetadataAssetsPath()
{
	const auto hasRequiredTables = [](const std::filesystem::path &assets) {
		const std::array<std::string_view, 2> sharedTables {
			"txtdata/Experience.tsv",
			"txtdata/classes/classdat.tsv",
		};
		const std::array<std::string_view, 6> classFolders {
			"warrior",
			"rogue",
			"sorcerer",
			"monk",
			"bard",
			"barbarian",
		};
		const std::array<std::string_view, 5> classTables {
			"attributes.tsv",
			"starting_loadout.tsv",
			"sprites.tsv",
			"animations.tsv",
			"sounds.tsv",
		};
		for (const std::string_view table : sharedTables) {
			if (!std::filesystem::is_regular_file(assets / table))
				return false;
		}
		for (const std::string_view folder : classFolders) {
			for (const std::string_view table : classTables) {
				if (!std::filesystem::is_regular_file(assets / "txtdata/classes" / folder / table))
					return false;
			}
		}
		return true;
	};
	const std::array<std::filesystem::path, 3> starts {
		std::filesystem::path(paths::BasePath()),
		std::filesystem::current_path(),
		std::filesystem::path(paths::AssetsPath()),
	};
	for (std::filesystem::path current : starts) {
		if (current.empty())
			continue;
		current = current.lexically_normal();
		for (;;) {
			if (current.filename() == "assets" && hasRequiredTables(current))
				return current;
			const std::filesystem::path assets = current / "assets";
			if (hasRequiredTables(assets))
				return assets;
			const std::filesystem::path parent = current.parent_path();
			if (parent == current || parent.empty())
				break;
			current = parent;
		}
	}
	return std::nullopt;
}

// Sends the real legacy join packet through ParseCmd on a blank, headless level.
class OfflineJoinLevelTest : public testing::Test {
protected:
	static constexpr Point LocalPosition { 50, 50 };
	static constexpr Point ExpectedRemotePosition { 51, 50 };
	static constexpr Point RemoteStartingPosition { 10, 10 };

	void SetUp() override
	{
		previousAssetsPath_ = paths::AssetsPath();
		previousOverridePaths_.swap(OverridePaths);
		previousLevelId_ = currentLevel().id();
		previousViewPosition_ = viewPosition();
		previousMyPlayer_ = MyPlayer;
		previousMyPlayerId_ = MyPlayerId;
		previousLightManager_ = CurrentLightManager;
		previousHeadlessMode_ = HeadlessMode;
		previousBufferMessages_ = gbBufferMsgs;
		previousMultiplayer_ = gbIsMultiplayer;
		previousActivePlayers_ = gbActivePlayers;
		previousLastCommand_ = TestGetLastParsedCommand();
		previousRandomState_ = GetLCGEngineState();
		std::copy_n(&tileProperties()[0], MAXTILES, previousTileProperties_.begin());
		std::copy_n(sgwPackPlrOffsetTbl, MAX_PLRS, previousPackOffsets_.begin());
		for (int y = 0; y < MAXDUNY; ++y) {
			for (int x = 0; x < MAXDUNX; ++x) {
				previousTiles_[y * MAXDUNX + x] = tileAt({ x, y });
				tileAt({ x, y }) = Tile {};
			}
		}
		previousPlayers_.swap(Players);

		const std::optional<std::filesystem::path> metadataAssets = FindLoosePlayerMetadataAssetsPath();
		ASSERT_TRUE(metadataAssets.has_value()) << "Loose class metadata TSVs are required for the headless join fixture.";
		paths::SetAssetsPath(metadataAssets->string());
		OverridePaths.emplace_back(paths::AssetsPath());
		HeadlessMode = true;
		// The hidden class-table caches are process-owned and have no unload API;
		// reload them from the canonical loose TSVs on every fixture setup.
		LoadPlayerDataFiles();
		ASSERT_EQ(GetNumPlayerClasses(), 6U);
		ASSERT_GT(GetPlayerAnimDataForClass(HeroClass::Warrior).unarmedFrames, 0);
		currentLevel().setId({ 1, DTYPE_CATHEDRAL, false, SL_NONE });
		viewPosition() = { 40, 40 };
		std::fill_n(&tileProperties()[0], MAXTILES, TileProperties::None);
		CurrentLightManager = LightManager {};
		HeadlessMode = true;
		gbBufferMsgs = 0;
		gbIsMultiplayer = false;
		gbActivePlayers = 2;
		TestSetLastParsedCommand(CMD_STAND);
		SetRndSeed(0x12345678);
		std::fill_n(sgwPackPlrOffsetTbl, MAX_PLRS, 0);

		Players.resize(2);
		MyPlayerId = 0;
		MyPlayer = &Players[0];
		InitializePlayer(Players[0], 1, LocalPosition, false);
		InitializePlayer(Players[1], 0, RemoteStartingPosition, true);
		tileAt(LocalPosition).setPlayer(1);
	}

	void TearDown() override
	{
		for (int y = 0; y < MAXDUNY; ++y) {
			for (int x = 0; x < MAXDUNX; ++x)
				tileAt({ x, y }) = previousTiles_[y * MAXDUNX + x];
		}
		std::copy(previousTileProperties_.begin(), previousTileProperties_.end(), &tileProperties()[0]);
		currentLevel().setId(previousLevelId_);
		viewPosition() = previousViewPosition_;
		Players.swap(previousPlayers_);
		MyPlayer = previousMyPlayer_;
		MyPlayerId = previousMyPlayerId_;
		CurrentLightManager = previousLightManager_;
		HeadlessMode = previousHeadlessMode_;
		gbBufferMsgs = previousBufferMessages_;
		gbIsMultiplayer = previousMultiplayer_;
		gbActivePlayers = previousActivePlayers_;
		TestSetLastParsedCommand(previousLastCommand_);
		SetRndSeed(previousRandomState_);
		std::copy(previousPackOffsets_.begin(), previousPackOffsets_.end(), sgwPackPlrOffsetTbl);
		paths::SetAssetsPath(previousAssetsPath_);
		OverridePaths.swap(previousOverridePaths_);
	}

	bool IsOfflineAndDisconnected() const
	{
		if (gbIsMultiplayer)
			return false;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected())
			return false;
#endif
		return true;
	}

	static TCmdLocParam2 MakeJoinCommand(uint8_t x, uint8_t y, uint16_t level)
	{
		TCmdLocParam2 command {};
		command.bCmd = CMD_PLAYER_JOINLEVEL;
		command.x = x;
		command.y = y;
		command.wParam1 = Swap16LE(level);
		command.wParam2 = 0;
		return command;
	}

	static size_t ParseJoin(const TCmdLocParam2 &command)
	{
		return ParseCmd(1, reinterpret_cast<const TCmd *>(&command), sizeof(command));
	}

	static void InitializePlayer(Player &player, uint8_t level, Point position, bool changingLevel)
	{
		player._pName[0] = '\0';
		player._pClass = HeroClass::Warrior;
		player._pgfxnum = 0;
		player._pLightRad = 5;
		player._pInvincible = false;
		player.plractive = true;
		player.plrIsOnSetLevel = false;
		player.setLevel(level);
		player._pLvlChanging = changingLevel;
		player.position.tile = position;
		player.position.future = position;
		player.position.old = position;
		player.direction = Direction::West;
		player._pmode = PM_STAND;
		player.life.current = 100;
		player.life.maximum = 100;
		player.life.maximumBase = 100;
		player.life.base = 100;
	}

	std::string previousAssetsPath_;
	std::vector<std::string> previousOverridePaths_;
	std::vector<Player> previousPlayers_;
	std::array<Tile, MAXDUNX * MAXDUNY> previousTiles_ {};
	std::array<TileProperties, MAXTILES> previousTileProperties_ {};
	std::array<uint16_t, MAX_PLRS> previousPackOffsets_ {};
	LevelId previousLevelId_ {};
	Point previousViewPosition_ {};
	Player *previousMyPlayer_ = nullptr;
	uint8_t previousMyPlayerId_ = 0;
	LightManager previousLightManager_ {};
	bool previousHeadlessMode_ = false;
	uint8_t previousBufferMessages_ = 0;
	bool previousMultiplayer_ = false;
	uint8_t previousActivePlayers_ = 0;
	uint8_t previousLastCommand_ = CMD_STAND;
	uint32_t previousRandomState_ = 0;
};

TEST_F(OfflineJoinLevelTest, PlacesRemotePlayerAtFirstFreeCellAndActivatesVision)
{
	ASSERT_TRUE(IsOfflineAndDisconnected());
	const uint32_t randomState = GetLCGEngineState();
	const TCmdLocParam2 command = MakeJoinCommand(LocalPosition.x, LocalPosition.y, 1);

	EXPECT_EQ(ParseJoin(command), sizeof(command));

	Player &remote = Players[1];
	EXPECT_EQ(remote.plrlevel, 1);
	EXPECT_FALSE(remote.plrIsOnSetLevel);
	EXPECT_FALSE(remote._pLvlChanging);
	EXPECT_EQ(remote.position.tile, ExpectedRemotePosition);
	EXPECT_EQ(remote.position.future, ExpectedRemotePosition);
	EXPECT_EQ(remote.direction, Direction::South);
	EXPECT_EQ(remote._pmode, PM_STAND);
	EXPECT_EQ(tileAt(LocalPosition).player(), 1);
	EXPECT_EQ(tileAt(ExpectedRemotePosition).player(), 2);
	EXPECT_EQ(PlayerAtPosition(LocalPosition), &Players[0]);
	EXPECT_EQ(PlayerAtPosition(ExpectedRemotePosition), &remote);

	int localMarkers = 0;
	int remoteMarkers = 0;
	int otherMarkers = 0;
	for (int y = 0; y < MAXDUNY; ++y) {
		for (int x = 0; x < MAXDUNX; ++x) {
			const int marker = tileAt({ x, y }).player();
			if (marker == 1)
				++localMarkers;
			else if (marker == 2)
				++remoteMarkers;
			else if (marker != 0)
				++otherMarkers;
		}
	}
	EXPECT_EQ(localMarkers, 1);
	EXPECT_EQ(remoteMarkers, 1);
	EXPECT_EQ(otherMarkers, 0);

	ASSERT_TRUE(CurrentLightManager.visionActive_[remote.getId()]);
	EXPECT_EQ(CurrentLightManager.visionList_[remote.getId()].position.tile, ExpectedRemotePosition);
	EXPECT_EQ(CurrentLightManager.visionList_[remote.getId()].radius, remote._pLightRad);
	EXPECT_TRUE(CurrentLightManager.VisionUpdatePendingForTesting());
	EXPECT_EQ(GetLCGEngineState(), randomState);
}

TEST_F(OfflineJoinLevelTest, RejectsInvalidLevelAndOutOfBoundsBeforeMutatingPlayerPresentation)
{
	ASSERT_TRUE(IsOfflineAndDisconnected());
	Player &remote = Players[1];
	const Point initialPosition = remote.position.tile;
	const Point initialFuture = remote.position.future;
	const Point initialOld = remote.position.old;
	const auto initialDirection = remote.direction;
	const auto initialMode = remote._pmode;
	const uint32_t randomState = GetLCGEngineState();

	for (const TCmdLocParam2 command : {
	         MakeJoinCommand(ExpectedRemotePosition.x, ExpectedRemotePosition.y, 0xFFFF),
	         MakeJoinCommand(static_cast<uint8_t>(MAXDUNX), LocalPosition.y, 1),
	     }) {
		EXPECT_EQ(ParseJoin(command), sizeof(command));
		EXPECT_EQ(remote.plrlevel, 0);
		EXPECT_TRUE(remote._pLvlChanging);
		EXPECT_EQ(remote.position.tile, initialPosition);
		EXPECT_EQ(remote.position.future, initialFuture);
		EXPECT_EQ(remote.position.old, initialOld);
		EXPECT_EQ(remote.direction, initialDirection);
		EXPECT_EQ(remote._pmode, initialMode);
		EXPECT_EQ(tileAt(LocalPosition).player(), 1);
		EXPECT_EQ(tileAt(ExpectedRemotePosition).player(), 0);
		EXPECT_EQ(PlayerAtPosition(LocalPosition), &Players[0]);
		EXPECT_EQ(PlayerAtPosition(ExpectedRemotePosition), nullptr);
		EXPECT_FALSE(CurrentLightManager.visionActive_[remote.getId()]);
		EXPECT_FALSE(CurrentLightManager.VisionUpdatePendingForTesting());
		EXPECT_EQ(GetLCGEngineState(), randomState);
	}
}

} // namespace

namespace devilution {
extern void TestPlayerCheckNewPath(Player &player);
extern bool TestPlayerDoGotHit(Player &player);
} // namespace devilution

class FlashCastFacingTest : public testing::TestWithParam<size_t> {
protected:
	void SetUp() override
	{
		LoadSpellData();
		Players.swap(previousPlayers_);
		previousMyPlayer_ = MyPlayer;
		previousMyPlayerId_ = MyPlayerId;
		previousViewPosition_ = viewPosition();
		previousLightManager_ = CurrentLightManager;
		Players.resize(2);
		MyPlayerId = 0;
		MyPlayer = &Players[0];
	}

	void TearDown() override
	{
		Players.swap(previousPlayers_);
		MyPlayer = previousMyPlayer_;
		MyPlayerId = previousMyPlayerId_;
		viewPosition() = previousViewPosition_;
		CurrentLightManager = previousLightManager_;
	}

private:
	std::vector<Player> previousPlayers_;
	Player *previousMyPlayer_ = nullptr;
	uint8_t previousMyPlayerId_ = 0;
	Point previousViewPosition_;
	LightManager previousLightManager_;
};

TEST_P(FlashCastFacingTest, LegacyActionTurnsTowardCursorBeforeSelfCenteredEffect)
{
	Player &player = Players[GetParam()];
	player._pClass = HeroClass::Sorcerer;
	player.life.current = 2560;
	player.mana.current = 3000;
	player._pSplLvl[static_cast<size_t>(SpellID::Flash)] = 3;
	player._pSFrames = 14;
	player._pSFNum = 6;
	player.lightId = NO_LIGHT;
	player.clearPath();
	player.queuedSpell.spellId = SpellID::Flash;
	player.queuedSpell.spellType = SpellType::Spell;

	// Exercise CheckNewPath -> StartSpell, not compact replay's calculation.
	// The final vector characterizes the still-open same-tile parity gap.
	// Headless animation needs no MPQs.
	for (const auto &[origin, cursor, expected] : {
	         std::tuple { Point { 5, 5 }, Point { 0, 0 }, Direction::North },
	         std::tuple { Point { 0, 0 }, Point { 1, 0 }, Direction::SouthEast },
	         std::tuple { Point { 0, 0 }, Point { 0, 0 }, Direction::SouthWest } }) {
		player.position.tile = origin;
		player.position.future = origin;
		player.position.old = origin;
		player.direction = Direction::South;
		player._pmode = PM_STAND;
		player.destAction = ACTION_SPELL;
		player.destParam1 = cursor.x;
		player.destParam2 = cursor.y;

		TestPlayerCheckNewPath(player);

		EXPECT_EQ(player._pmode, PM_SPELL);
		EXPECT_EQ(player.executedSpell.spellId, SpellID::Flash);
		EXPECT_EQ(player.position.tile, origin);
		EXPECT_EQ(player.position.temp, cursor);
		EXPECT_EQ(player.direction, expected);
	}
}

INSTANTIATE_TEST_SUITE_P(LocalAndRemote, FlashCastFacingTest, testing::Values(size_t { 0 }, size_t { 1 }));

int RunBlockTest(int frames, ItemSpecialEffect flags)
{
	devilution::Player &player = Players[0];

	player._pHFrames = frames;
	player._pIFlags = flags;
	// StartPlrHit compares damage (a 6 bit fixed point value) to character level to determine if the player shrugs off the hit.
	// We don't initialise player so this comparison can't be relied on, instead we use forcehit to ensure the player enters hit mode
	player.startHit(0, true);

	int i = 1;
	for (; i < 100; i++) {
		TestPlayerDoGotHit(player);
		if (player._pmode != PM_GOTHIT)
			break;
		player.animInfo.currentFrame++;
	}

	return i;
}

constexpr ItemSpecialEffect Normal = ItemSpecialEffect::None;
constexpr ItemSpecialEffect Balance = ItemSpecialEffect::FastHitRecovery;
constexpr ItemSpecialEffect Stability = ItemSpecialEffect::FasterHitRecovery;
constexpr ItemSpecialEffect Harmony = ItemSpecialEffect::FastestHitRecovery;
constexpr ItemSpecialEffect BalanceStability = Balance | Stability;
constexpr ItemSpecialEffect BalanceHarmony = Balance | Harmony;
constexpr ItemSpecialEffect StabilityHarmony = Stability | Harmony;

constexpr int Warrior = 6;
constexpr int Rogue = 7;
constexpr int Sorcerer = 8;

struct BlockTestCase {
	int expectedRecoveryFrame;
	int maxRecoveryFrame;
	ItemSpecialEffect itemFlags;
};

BlockTestCase BlockData[] = {
	{ 6, Warrior, Normal },
	{ 7, Rogue, Normal },
	{ 8, Sorcerer, Normal },

	{ 5, Warrior, Balance },
	{ 6, Rogue, Balance },
	{ 7, Sorcerer, Balance },

	{ 4, Warrior, Stability },
	{ 5, Rogue, Stability },
	{ 6, Sorcerer, Stability },

	{ 3, Warrior, Harmony },
	{ 4, Rogue, Harmony },
	{ 5, Sorcerer, Harmony },

	{ 4, Warrior, BalanceStability },
	{ 5, Rogue, BalanceStability },
	{ 6, Sorcerer, BalanceStability },

	{ 3, Warrior, BalanceHarmony },
	{ 4, Rogue, BalanceHarmony },
	{ 5, Sorcerer, BalanceHarmony },

	{ 3, Warrior, StabilityHarmony },
	{ 4, Rogue, StabilityHarmony },
	{ 5, Sorcerer, StabilityHarmony },
};

TEST(Player, PM_DoGotHit)
{
	LoadCoreArchives();
	LoadGameArchives();
	if (!HaveMainData()) {
		GTEST_SKIP() << "MPQ assets (spawn.mpq or DIABDAT.MPQ) not found - skipping test";
	}
	LoadPlayerDataFiles();

	Players.resize(1);
	MyPlayer = &Players[0];
	for (size_t i = 0; i < sizeof(BlockData) / sizeof(*BlockData); i++) {
		EXPECT_EQ(BlockData[i].expectedRecoveryFrame, RunBlockTest(BlockData[i].maxRecoveryFrame, BlockData[i].itemFlags));
	}
}

static void AssertPlayer(devilution::Player &player)
{
	ASSERT_EQ(CountU8(player._pSplLvl, 64), 0);
	ASSERT_EQ(Count8(player.InvGrid, InventoryGridCells), 1);
	ASSERT_EQ(CountItems(player.InvBody, NUM_INVLOC), 1);
	ASSERT_EQ(CountItems(player.InvList, InventoryGridCells), 1);
	ASSERT_EQ(CountItems(player.SpdList, MaxBeltItems), 2);
	ASSERT_EQ(CountItems(&player.HoldItem, 1), 0);

	ASSERT_EQ(player.position.tile.x, 0);
	ASSERT_EQ(player.position.tile.y, 0);
	ASSERT_EQ(player.position.future.x, 0);
	ASSERT_EQ(player.position.future.y, 0);
	ASSERT_EQ(player.plrlevel, 0);
	ASSERT_EQ(player.destAction, 0);
	ASSERT_STREQ(player._pName, "");
	ASSERT_EQ(player._pClass, HeroClass::Rogue);
	ASSERT_EQ(player.attributes.strength.base, 20);
	ASSERT_EQ(player.attributes.strength.current, 20);
	ASSERT_EQ(player.attributes.magic.base, 15);
	ASSERT_EQ(player.attributes.magic.current, 15);
	ASSERT_EQ(player.attributes.dexterity.base, 30);
	ASSERT_EQ(player.attributes.dexterity.current, 30);
	ASSERT_EQ(player.attributes.vitality.base, 20);
	ASSERT_EQ(player.attributes.vitality.current, 20);
	ASSERT_EQ(player.getCharacterLevel(), 1);
	ASSERT_EQ(player._pStatPts, 0);
	ASSERT_EQ(player._pExperience, 0);
	ASSERT_EQ(player._pGold, 100);
	ASSERT_EQ(player.life.maximumBase, 2880);
	ASSERT_EQ(player.life.base, 2880);
	ASSERT_EQ(player.getBaseToBlock(), 20);
	ASSERT_EQ(player.mana.maximumBase, 1440);
	ASSERT_EQ(player.mana.base, 1440);
	ASSERT_EQ(player._pMemSpells, 0);
	ASSERT_EQ(player._pNumInv, 1);
	ASSERT_EQ(player.wReflections, 0);
	ASSERT_EQ(player.pTownWarps, 0);
	ASSERT_EQ(player.pDungMsgs, 0);
	ASSERT_EQ(player.pDungMsgs2, 0);
	ASSERT_EQ(player.pLvlLoad, 0);
	ASSERT_EQ(player.pDiabloKillLevel, 0);
	ASSERT_EQ(player.pManaShield, 0);
	ASSERT_EQ(player.pDamAcFlags, ItemSpecialEffectHf::None);

	ASSERT_EQ(player._pmode, 0);
	ASSERT_EQ(Count8(player.walkpath, MaxPathLengthPlayer), 0);
	ASSERT_EQ(player.queuedSpell.spellId, SpellID::Null);
	ASSERT_EQ(player.queuedSpell.spellType, SpellType::Skill);
	ASSERT_EQ(player.queuedSpell.spellFrom, 0);
	ASSERT_EQ(player.inventorySpell, SpellID::Null);
	ASSERT_EQ(player._pRSpell, SpellID::TrapDisarm);
	ASSERT_EQ(player._pRSplType, SpellType::Skill);
	ASSERT_EQ(player._pSBkSpell, SpellID::Null);
	ASSERT_EQ(player._pAblSpells, 134217728);
	ASSERT_EQ(player._pScrlSpells, 0);
	ASSERT_EQ(player._pSpellFlags, SpellFlag::None);
	ASSERT_EQ(player._pBlockFlag, 0);
	ASSERT_EQ(player._pLightRad, 10);
	ASSERT_EQ(player._pDamageMod, 0);
	ASSERT_EQ(player.life.current, 2880);
	ASSERT_EQ(player.life.maximum, 2880);
	ASSERT_EQ(player.mana.current, 1440);
	ASSERT_EQ(player.mana.maximum, 1440);
	ASSERT_EQ(player.getNextExperienceThreshold(), 2000);
	ASSERT_EQ(player._pMagResist, 0);
	ASSERT_EQ(player._pFireResist, 0);
	ASSERT_EQ(player._pLghtResist, 0);
	ASSERT_EQ(CountBool(player._pLvlVisited, NUMLEVELS), 0);
	ASSERT_EQ(CountBool(player._pSLvlVisited, NUMLEVELS), 0);
	// This test case uses a Rogue, starting loadout is a short bow with damage 1-4
	ASSERT_EQ(player.damageBonuses.physical.minimum, 1);
	ASSERT_EQ(player.damageBonuses.physical.maximum, 4);
	ASSERT_EQ(player._pIAC, 0);
	ASSERT_EQ(player.damageBonuses.percent, 0);
	ASSERT_EQ(player._pIBonusToHit, 0);
	ASSERT_EQ(player._pIBonusAC, 0);
	ASSERT_EQ(player.damageBonuses.flat, 0);
	ASSERT_EQ(player._pISpells, 0);
	ASSERT_EQ(player._pIFlags, ItemSpecialEffect::None);
	ASSERT_EQ(player._pIGetHit, 0);
	ASSERT_EQ(player._pISplLvlAdd, 0);
	ASSERT_EQ(player.damageBonuses.armorPiercing, 0);
	ASSERT_EQ(player.damageBonuses.fire.minimum, 0);
	ASSERT_EQ(player.damageBonuses.fire.maximum, 0);
	ASSERT_EQ(player.damageBonuses.lightning.minimum, 0);
	ASSERT_EQ(player.damageBonuses.lightning.maximum, 0);
}

TEST(Player, CreatePlayer)
{
	LoadCoreArchives();
	LoadGameArchives();

	// The tests need spawn.mpq or diabdat.mpq
	if (!HaveMainData()) {
		GTEST_SKIP() << "MPQ assets (spawn.mpq or DIABDAT.MPQ) not found - skipping test";
	}

	LoadPlayerDataFiles();
	LoadMonsterData();
	LoadItemData();
	Players.resize(1);
	Players[0].create(HeroClass::Rogue);
	AssertPlayer(Players[0]);
}

TEST(Player, IsUnarmedChecksCurrentWeaponGraphic)
{
	Player player;
	player._pgfxnum = static_cast<uint8_t>(PlayerWeaponGraphic::Unarmed);
	EXPECT_TRUE(player.isUnarmed());

	player._pgfxnum = static_cast<uint8_t>(PlayerWeaponGraphic::Sword);
	EXPECT_FALSE(player.isUnarmed());
}

TEST(Player, InitDungeonMessagesClearsMessageFlags)
{
	Player player;
	player.pDungMsgs = 0xFF;
	player.pDungMsgs2 = 0xFF;

	player.initDungeonMessages();

	EXPECT_EQ(player.pDungMsgs, 0);
	EXPECT_EQ(player.pDungMsgs2, 0);
}
