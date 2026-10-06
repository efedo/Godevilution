#include <climits>
#include <random>

#include <gtest/gtest.h>

#include "engine/assets.hpp"
#include "engine/random.hpp"
#include "application/game_mode.hpp"
#include "game/items/items.hpp"
#include "game/players/players.hpp"
#include "game/spells/spells.hpp"
#include "tables/playerdat.hpp"
#include "utils/string/str_cat.hpp"

namespace devilution {
namespace {

class ItemsTest : public ::testing::Test {
public:
	void SetUp() override
	{
		Players.resize(1);
		MyPlayer = &Players[0];
		for (auto &flag : UniqueItemFlags)
			flag = false;
	}

	static void SetUpTestSuite()
	{
		LoadItemData();
		LoadSpellData();
		LoadPlayerDataFiles();
		gbIsSpawn = false;
		gbIsMultiplayer = false; // to also test UniqueItemFlags
	}
};

void GenerateAllUniques(bool hellfire, const size_t expectedUniques)
{
	gbIsHellfire = hellfire;
	UnloadModArchives();
	if (hellfire) {
		LoadModArchives({ { "Hellfire" } });
	}
	LoadItemData();

	std::mt19937 betterRng;
	const std::uniform_int_distribution<uint32_t> dist(0, std::numeric_limits<uint32_t>::max());

	// Get seed for test run from time. If a test run fails, remember the seed and hardcode it here.
	const uint32_t testRunSeed = static_cast<uint32_t>(time(nullptr));
	betterRng.seed(testRunSeed);

	std::set<int> foundUniques;

	constexpr int max_iterations = 1000000;
	int iteration = 0;
	int uniqueIndex = -1;

	for (auto &uniqueItem : UniqueItems) {
		++uniqueIndex;

		if (foundUniques.contains(uniqueIndex))
			continue;

		_item_indexes uniqueBaseIndex = IDI_GOLD;
		for (size_t j = 0; j < AllItemsList.size(); j++) {
			if (!IsItemAvailable(static_cast<int>(j)))
				continue;
			if (AllItemsList[j].iItemId != uniqueItem.UIItemId)
				continue;
			if (AllItemsList[j].dropRate > 0)
				uniqueBaseIndex = static_cast<_item_indexes>(j);
			break;
		}

		if (uniqueBaseIndex == IDI_GOLD)
			continue; // Unique not dropable (Quest-Unique)

		auto &baseItemData = AllItemsList[static_cast<size_t>(uniqueBaseIndex)];

		while (true) {
			iteration += 1;

			if (iteration > max_iterations)
				FAIL() << StrCat("Item ", uniqueItem.UIName, " not found in ", max_iterations, " tries with test run seed ", testRunSeed);

			Item testItem = {};
			testItem._iMiscId = baseItemData.iMiscId;
			std::uniform_int_distribution<int32_t> dist(0, INT_MAX);
			SetRndSeed(dist(betterRng));

			int targetLvl = uniqueItem.UIMinLvl;
			int uper = 1;
			if (uniqueItem.UIMinLvl - 4 > 0) {
				uper = 15;
				targetLvl = uniqueItem.UIMinLvl - 4;
			}

			SetupAllItems(*MyPlayer, testItem, uniqueBaseIndex, AdvanceRndSeed(), targetLvl, uper, true, false);
			TryRandomUniqueItem(testItem, uniqueBaseIndex, targetLvl, uper, true, false);
			SetupItem(testItem);

			if (testItem._iMagical != ITEM_QUALITY_UNIQUE)
				continue;

			foundUniques.insert(testItem._iUid);

			if (testItem._iUid == uniqueIndex)
				break;
		}
	}

	EXPECT_EQ(foundUniques.size(), expectedUniques) << StrCat("test run seed ", testRunSeed);
}

TEST_F(ItemsTest, AllDiabloUniquesCanDrop)
{
	GenerateAllUniques(false, 79);
}

TEST_F(ItemsTest, AllHellfireUniquesCanDrop)
{
	GenerateAllUniques(true, 99);
}

TEST_F(ItemsTest, ShippedBaseDefinitionsMatchAuthoritativeCatalog)
{
	Item warrior = {};
	SetRndSeed(0x12345678);
	GetItemAttrs(warrior, IDI_WARRIOR, 1);
	EXPECT_EQ(warrior._itype, ItemType::Sword);
	EXPECT_EQ(warrior._iMinDam, 2);
	EXPECT_EQ(warrior._iMaxDam, 6);
	EXPECT_EQ(warrior._ivalue, 120);
	EXPECT_EQ(warrior._iDurability, 24);
	EXPECT_EQ(warrior._iMinStr, 18);

	Item cleaver = {};
	SetRndSeed(0x12345678);
	GetItemAttrs(cleaver, IDI_CLEAVER, 1);
	EXPECT_EQ(cleaver._itype, ItemType::Axe);
	EXPECT_EQ(cleaver._iMinDam, 4);
	EXPECT_EQ(cleaver._iMaxDam, 24);
	EXPECT_EQ(cleaver._ivalue, 2000);
	EXPECT_EQ(cleaver._iLoc, ILOC_TWOHAND);
}

TEST_F(ItemsTest, WarriorRepairUsesNativeDurabilityLossAndRandomSequence)
{
	Item item = {};
	item._iDurability = 1;
	item._iMaxDur = 20;
	SetRndSeed(0);

	RepairItem(item, 10);

	EXPECT_EQ(item._iDurability, 18);
	EXPECT_EQ(item._iMaxDur, 18);
	EXPECT_EQ(GetLCGEngineState(), 22695478U);
}

TEST_F(ItemsTest, WarriorLevelOneRepairUsesFiveNativeDrawsForDamagedStartingSword)
{
	Item item = {};
	item._iDurability = 12;
	item._iMaxDur = 24;
	SetRndSeed(0);

	RepairItem(item, 1);

	EXPECT_EQ(item._iDurability, 16);
	EXPECT_EQ(item._iMaxDur, 16);
	EXPECT_EQ(GetLCGEngineState(), 71484141U);
}

TEST_F(ItemsTest, SorcererStaffRechargeUsesNativeChargeLossAndRandomSequence)
{
	Item item = {};
	InitializeItem(item, IDI_SHORTSTAFF);
	item._iSpell = SpellID::ChargedBolt;
	item._iCharges = 1;
	item._iMaxCharges = 10;
	MyPlayer->setCharacterLevel(10);
	SetRndSeed(1);

	RechargeItem(item, *MyPlayer);

	EXPECT_EQ(item._iCharges, 8);
	EXPECT_EQ(item._iMaxCharges, 8);
	EXPECT_EQ(GetLCGEngineState(), 22695478U);
}

} // namespace
} // namespace devilution
