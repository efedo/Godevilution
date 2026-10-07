#include <algorithm>
#include <array>
#include <climits>
#include <random>
#include <tuple>
#include <vector>

#include <gtest/gtest.h>

#include "application/game_mode.hpp"
#include "engine/assets.hpp"
#include "engine/gfx/backbuffer_state.hpp"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/items/items.hpp"
#include "game/players/players.hpp"
#include "game/spells/spells.hpp"
#include "persistence/options.h"
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

class ScopedBardIdentifyState {
public:
	ScopedBardIdentifyState()
	    : fixturePlayers_(2)
	    , previousMyPlayer_(MyPlayer)
	    , previousMyPlayerId_(MyPlayerId)
	    , previousLightManager_(CurrentLightManager)
	    , previousRandomSeed_(GetLCGEngineState())
	    , previousTestBard_(*GetOptions().Gameplay.testBard)
	    , previousHealthRedraw_(IsRedrawComponent(PanelDrawComponent::Health))
	    , previousManaRedraw_(IsRedrawComponent(PanelDrawComponent::Mana))
	{
		Players.swap(previousPlayers_);
		Players.swap(fixturePlayers_);
		MyPlayer = &Players[0];
		MyPlayerId = 0;
		CurrentLightManager = LightManager {};
		GetOptions().Gameplay.testBard.SetValue(true);
	}

	ScopedBardIdentifyState(const ScopedBardIdentifyState &) = delete;
	ScopedBardIdentifyState &operator=(const ScopedBardIdentifyState &) = delete;

	~ScopedBardIdentifyState()
	{
		GetOptions().Gameplay.testBard.SetValue(previousTestBard_);
		CurrentLightManager = previousLightManager_;
		SetRndSeed(previousRandomSeed_);
		Players.swap(fixturePlayers_);
		Players.swap(previousPlayers_);
		MyPlayer = previousMyPlayer_;
		MyPlayerId = previousMyPlayerId_;
		if (!previousHealthRedraw_)
			RedrawComponentComplete(PanelDrawComponent::Health);
		if (!previousManaRedraw_)
			RedrawComponentComplete(PanelDrawComponent::Mana);
	}

private:
	std::vector<Player> previousPlayers_;
	std::vector<Player> fixturePlayers_;
	Player *previousMyPlayer_;
	uint8_t previousMyPlayerId_;
	LightManager previousLightManager_;
	uint32_t previousRandomSeed_;
	bool previousTestBard_;
	bool previousHealthRedraw_;
	bool previousManaRedraw_;
};

auto ComparableItemFields(const Item &item)
{
	return std::tie(item._iSeed, item._iCreateInfo, item._itype, item._iAnimFlag, item.position,
	    item._iDelFlag, item.selectionRegion, item._iPostDraw, item._iMagical, item._iLoc,
	    item._iClass, item._iCurs, item._ivalue, item._iIvalue, item._iMinDam, item._iMaxDam,
	    item._iAC, item._iFlags, item._iMiscId, item._iSpell, item.IDidx, item._iCharges,
	    item._iMaxCharges, item._iDurability, item._iMaxDur, item._iPLDam, item._iPLToHit,
	    item._iPLAC, item._iPLStr, item._iPLMag, item._iPLDex, item._iPLVit, item._iPLFR,
	    item._iPLLR, item._iPLMR, item._iPLMana, item._iPLHP, item._iPLDamMod,
	    item._iPLGetHit, item._iPLLight, item._iSplLvlAdd, item._iRequest, item._iUid,
	    item._iFMinDam, item._iFMaxDam, item._iLMinDam, item._iLMaxDam, item._iPLEnAc,
	    item._iPrePower, item._iSufPower, item._iVAdd1, item._iVMult1, item._iVAdd2,
	    item._iVMult2, item._iMinStr, item._iMinMag, item._iMinDex, item._iStatFlag,
	    item._iDamAcFlags, item.dwBuff);
}

void ExpectSameItemExceptIdentified(const Item &expected, const Item &actual)
{
	EXPECT_EQ(ComparableItemFields(expected), ComparableItemFields(actual));
	EXPECT_STREQ(expected._iName, actual._iName);
	EXPECT_STREQ(expected._iIName, actual._iIName);
	EXPECT_EQ(expected.animInfo.sprites.has_value(), actual.animInfo.sprites.has_value());
	if (expected.animInfo.sprites.has_value())
		EXPECT_EQ(expected.animInfo.sprites->data(), actual.animInfo.sprites->data());

	const AnimationTimingState expectedTiming = expected.animInfo.captureTimingState();
	const AnimationTimingState actualTiming = actual.animInfo.captureTimingState();
	EXPECT_EQ(std::tie(expectedTiming.numberOfFrames, expectedTiming.ticksPerFrame,
	              expectedTiming.tickCounterOfCurrentFrame, expectedTiming.currentFrame,
	              expectedTiming.isPetrified, expectedTiming.relevantFramesForDistributing,
	              expectedTiming.skippedFramesFromPreviousAnimation, expectedTiming.tickModifier,
	              expectedTiming.ticksSinceSequenceStarted),
	    std::tie(actualTiming.numberOfFrames, actualTiming.ticksPerFrame,
	        actualTiming.tickCounterOfCurrentFrame, actualTiming.currentFrame,
	        actualTiming.isPetrified, actualTiming.relevantFramesForDistributing,
	        actualTiming.skippedFramesFromPreviousAnimation, actualTiming.tickModifier,
	        actualTiming.ticksSinceSequenceStarted));
}

void ExpectSameItem(const Item &expected, const Item &actual)
{
	EXPECT_EQ(expected._iIdentified, actual._iIdentified);
	ExpectSameItemExceptIdentified(expected, actual);
}

class ScopedRechargeRandomState {
public:
	explicit ScopedRechargeRandomState(uint32_t seed)
	    : previousSeed_(GetLCGEngineState())
	{
		SetRndSeed(seed);
	}

	ScopedRechargeRandomState(const ScopedRechargeRandomState &) = delete;
	ScopedRechargeRandomState &operator=(const ScopedRechargeRandomState &) = delete;

	~ScopedRechargeRandomState()
	{
		SetRndSeed(previousSeed_);
	}

private:
	uint32_t previousSeed_;
};

Item MakeRechargeTestItem(ItemType type, SpellID spell, int charges, int maximumCharges)
{
	Item item = {};
	item._itype = type;
	item._iSpell = spell;
	item._iCharges = charges;
	item._iMaxCharges = maximumCharges;
	item._iSeed = 0x12345678;
	item._iCreateInfo = 0x3456;
	item._ivalue = 314;
	item._iDurability = 12;
	item._iMaxDur = 20;
	item._iIdentified = true;
	return item;
}

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

TEST_F(ItemsTest, SorcererStaffRechargeFullyChargedStaffIsNoOpWithoutRandomDraw)
{
	ScopedRechargeRandomState randomState(0x12345678);
	Player player = {};
	player.setCharacterLevel(10);
	ASSERT_NE(&player, MyPlayer);

	Item staff = MakeRechargeTestItem(ItemType::Staff, SpellID::ChargedBolt, 10, 10);
	const Item expected = staff;
	ASSERT_TRUE(IsValidSpell(staff._iSpell));
	const uint32_t initialRandomState = GetLCGEngineState();

	RechargeItem(staff, player);

	ExpectSameItem(expected, staff);
	EXPECT_EQ(GetLCGEngineState(), initialRandomState);
}

TEST_F(ItemsTest, SorcererStaffRechargeCanExhaustItsLastMaximumChargeWithOneRandomDraw)
{
	ScopedRechargeRandomState randomState(0);
	Player player = {};
	player.setCharacterLevel(10);
	ASSERT_NE(&player, MyPlayer);

	Item staff = MakeRechargeTestItem(ItemType::Staff, SpellID::ChargedBolt, 0, 1);
	Item expected = staff;
	expected._iMaxCharges = 0;
	ASSERT_TRUE(IsValidSpell(staff._iSpell));

	RechargeItem(staff, player);

	EXPECT_EQ(staff._iCharges, 0);
	EXPECT_EQ(staff._iMaxCharges, 0);
	ExpectSameItem(expected, staff);
	EXPECT_EQ(GetLCGEngineState(), 1U);
}

TEST_F(ItemsTest, RechargeItemRejectsNonStaffAndNullSpellWithoutChangingItemOrRandom)
{
	ScopedRechargeRandomState randomState(0x87654321);
	Player player = {};
	player.setCharacterLevel(10);
	ASSERT_NE(&player, MyPlayer);

	Item nonStaff = MakeRechargeTestItem(ItemType::Sword, SpellID::ChargedBolt, 3, 5);
	const Item expectedNonStaff = nonStaff;
	ASSERT_TRUE(IsValidSpell(nonStaff._iSpell));

	RechargeItem(nonStaff, player);

	ExpectSameItem(expectedNonStaff, nonStaff);
	EXPECT_EQ(GetLCGEngineState(), 0x87654321U);

	Item nullSpell = MakeRechargeTestItem(ItemType::Staff, SpellID::Null, 2, 5);
	const Item expectedNullSpell = nullSpell;
	ASSERT_FALSE(IsValidSpell(nullSpell._iSpell));

	RechargeItem(nullSpell, player);

	ExpectSameItem(expectedNullSpell, nullSpell);
	EXPECT_EQ(GetLCGEngineState(), 0x87654321U);
}

TEST_F(ItemsTest, BardIdentifyChangesOnlyTheEquippedItemsIdentificationState)
{
	ScopedBardIdentifyState scopedState;
	Player &localPlayer = Players[0];
	localPlayer = {};
	localPlayer.plrlevel = currentLevelNumber();

	Player &bard = Players[1];
	bard = {};
	bard._pClass = HeroClass::Bard;
	bard.setCharacterLevel(1);
	bard.plrlevel = localPlayer.plrlevel == 0 ? 1 : 0;
	bard.plractive = false;
	bard._pExperience = 1234;
	bard._pRSplType = SpellType::Invalid;
	bard._pRSpell = SpellID::Null;
	bard._pLightRad = 10;

	Item &sword = bard.InvBody[INVLOC_HAND_LEFT];
	InitializeItem(sword, IDI_BARDSWORD);
	ASSERT_EQ(sword.IDidx, IDI_BARDSWORD);
	ASSERT_EQ(sword._itype, ItemType::Sword);
	ASSERT_EQ(sword._iClass, ICLASS_WEAPON);
	ASSERT_EQ(sword._iMagical, ITEM_QUALITY_NORMAL);
	ASSERT_FALSE(sword._iIdentified);
	ASSERT_TRUE(IsItemAvailable(static_cast<int>(IDI_BARDSWORD)));
	const ClassAttributes &bardAttributes = GetClassAttributes(HeroClass::Bard);
	bard.attributes.strength.base = std::max<int>(bardAttributes.baseStr, sword._iMinStr);
	bard.attributes.strength.current = bard.attributes.strength.base;
	bard.attributes.magic.base = std::max<int>(bardAttributes.baseMag, sword._iMinMag);
	bard.attributes.magic.current = bard.attributes.magic.base;
	bard.attributes.dexterity.base = std::max<int>(bardAttributes.baseDex, sword._iMinDex);
	bard.attributes.dexterity.current = bard.attributes.dexterity.base;
	bard.attributes.vitality.base = bardAttributes.baseVit;
	bard.attributes.vitality.current = bardAttributes.baseVit;
	const int baseLife = bard.calculateBaseLife();
	bard.life = { baseLife, baseLife, baseLife, baseLife, 0 };
	const int baseMana = bard.calculateBaseMana();
	bard.mana = { baseMana, baseMana, baseMana, baseMana, 0 };

	InitializeItem(bard.InvList[0], IDI_BARDDAGGER);
	bard.InvList[0]._iSeed = 0x12345678;
	bard._pNumInv = 1;
	bard.InvGrid[0] = 1;
	InitializeItem(bard.SpdList[0], IDI_HEAL);
	bard.SpdList[0]._iSeed = 0x87654321;

	ASSERT_NE(&bard, MyPlayer);
	ASSERT_EQ(&bard, &Players[1]);
	ASSERT_EQ(bard.getId(), 1);
	ASSERT_FALSE(bard.isOnActiveLevel());

	SetRndSeed(0x2468ACE0);
	CalcPlrInv(bard, false);
	ASSERT_FALSE(sword._iIdentified);
	ASSERT_TRUE(sword._iStatFlag);
	ASSERT_TRUE(bard.CanUseItem(sword));
	const uint32_t randomStateBeforeIdentify = GetLCGEngineState();

	std::array<Item, NUM_INVLOC> equippedBefore {};
	std::copy_n(bard.InvBody, equippedBefore.size(), equippedBefore.begin());
	std::array<Item, InventoryGridCells> inventoryBefore {};
	std::copy_n(bard.InvList, inventoryBefore.size(), inventoryBefore.begin());
	std::array<Item, MaxBeltItems> beltBefore {};
	std::copy_n(bard.SpdList, beltBefore.size(), beltBefore.begin());
	const Item heldItemBefore = bard.HoldItem;
	std::array<int8_t, InventoryGridCells> inventoryGridBefore {};
	std::copy_n(bard.InvGrid, inventoryGridBefore.size(), inventoryGridBefore.begin());
	const int inventoryCountBefore = bard._pNumInv;
	const auto attributesBefore = std::array<int, 8> {
	    bard.attributes.strength.base, bard.attributes.strength.current,
	    bard.attributes.magic.base, bard.attributes.magic.current,
	    bard.attributes.dexterity.base, bard.attributes.dexterity.current,
	    bard.attributes.vitality.base, bard.attributes.vitality.current,
	};
	const auto lifeBefore = std::array<int, 5> {
	    bard.life.base, bard.life.current, bard.life.maximum, bard.life.maximumBase, bard.life.percentage,
	};
	const auto manaBefore = std::array<int, 5> {
	    bard.mana.base, bard.mana.current, bard.mana.maximum, bard.mana.maximumBase, bard.mana.percentage,
	};
	const auto derivedStateBefore = std::tuple {
	    bard._pDamageMod, bard._pIAC, bard._pIBonusToHit, bard._pIBonusAC, bard._pIGetHit,
	    bard._pIFlags, bard.pDamAcFlags,
	    bard.damageBonuses.physical.minimum, bard.damageBonuses.physical.maximum,
	    bard.damageBonuses.fire.minimum, bard.damageBonuses.fire.maximum,
	    bard.damageBonuses.lightning.minimum, bard.damageBonuses.lightning.maximum,
	    bard.damageBonuses.percent, bard.damageBonuses.flat, bard.damageBonuses.armorPiercing,
	    bard._pISpells, bard._pISplLvlAdd, bard._pLightRad, bard._pgfxnum, bard._pBlockFlag,
	    bard._pFireResist, bard._pMagResist, bard._pLghtResist,
	    bard._pRSpell, bard._pRSplType, bard._pMemSpells, bard._pAblSpells, bard._pScrlSpells,
	};
	const uint32_t experienceBefore = bard._pExperience;
	const int goldBefore = bard._pGold;

	CheckIdentify(bard, INVLOC_HAND_LEFT);

	EXPECT_TRUE(sword._iIdentified);
	ExpectSameItemExceptIdentified(equippedBefore[INVLOC_HAND_LEFT], sword);
	for (size_t slot = 0; slot < equippedBefore.size(); ++slot) {
		if (slot == INVLOC_HAND_LEFT)
			continue;
		ExpectSameItem(equippedBefore[slot], bard.InvBody[slot]);
	}
	for (size_t slot = 0; slot < inventoryBefore.size(); ++slot)
		ExpectSameItem(inventoryBefore[slot], bard.InvList[slot]);
	for (size_t slot = 0; slot < beltBefore.size(); ++slot)
		ExpectSameItem(beltBefore[slot], bard.SpdList[slot]);
	ExpectSameItem(heldItemBefore, bard.HoldItem);
	EXPECT_TRUE(std::equal(inventoryGridBefore.begin(), inventoryGridBefore.end(), bard.InvGrid));
	EXPECT_EQ(bard._pNumInv, inventoryCountBefore);
	EXPECT_EQ((std::array<int, 8> {
	              bard.attributes.strength.base, bard.attributes.strength.current,
	              bard.attributes.magic.base, bard.attributes.magic.current,
	              bard.attributes.dexterity.base, bard.attributes.dexterity.current,
	              bard.attributes.vitality.base, bard.attributes.vitality.current,
	          }),
	    attributesBefore);
	EXPECT_EQ((std::array<int, 5> {
	              bard.life.base, bard.life.current, bard.life.maximum, bard.life.maximumBase,
	              bard.life.percentage,
	          }),
	    lifeBefore);
	EXPECT_EQ((std::array<int, 5> {
	              bard.mana.base, bard.mana.current, bard.mana.maximum, bard.mana.maximumBase,
	              bard.mana.percentage,
	          }),
	    manaBefore);
	EXPECT_EQ((std::tuple {
	              bard._pDamageMod, bard._pIAC, bard._pIBonusToHit, bard._pIBonusAC,
	              bard._pIGetHit, bard._pIFlags, bard.pDamAcFlags,
	              bard.damageBonuses.physical.minimum, bard.damageBonuses.physical.maximum,
	              bard.damageBonuses.fire.minimum, bard.damageBonuses.fire.maximum,
	              bard.damageBonuses.lightning.minimum, bard.damageBonuses.lightning.maximum,
	              bard.damageBonuses.percent, bard.damageBonuses.flat,
	              bard.damageBonuses.armorPiercing, bard._pISpells, bard._pISplLvlAdd,
	              bard._pLightRad, bard._pgfxnum, bard._pBlockFlag, bard._pFireResist,
	              bard._pMagResist, bard._pLghtResist, bard._pRSpell, bard._pRSplType,
	              bard._pMemSpells, bard._pAblSpells, bard._pScrlSpells,
	          }),
	    derivedStateBefore);
	EXPECT_EQ(bard._pExperience, experienceBefore);
	EXPECT_EQ(bard._pGold, goldBefore);
	EXPECT_EQ(GetLCGEngineState(), randomStateBeforeIdentify);
}

} // namespace
} // namespace devilution
