#include "player_test.h"

#include <tuple>
#include <vector>

#include <gtest/gtest.h>

#include "engine/cursor.h"
#include "engine/assets.hpp"
#include "engine/lighting.h"
#include "application/init.hpp"
#include "game/levels/dungeon_common.h"
#include "tables/playerdat.hpp"
#include "tables/spelldat.h"

using namespace devilution;

namespace devilution {
extern void TestPlayerCheckNewPath(Player &player);
extern bool TestPlayerDoGotHit(Player &player);
}

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
