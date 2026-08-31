#include <algorithm>
#include <cstdint>
#include <fstream>
#include <iterator>
#include <string>
#include <string_view>
#include <vector>

#include <gtest/gtest.h>

#include "game/players/players.hpp"
#include "game/replay/replay.hpp"
#include "game/replay/replay_fixture.hpp"
#include "game/stores/stores.hpp"

namespace devilution {
namespace {

TEST(ReplayStateHasher, EmptyStateUsesSha256)
{
	ReplayStateHasher hasher;

	EXPECT_EQ(hasher.HexDigest(), "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
}

TEST(ReplayStateHasher, CanonicalFieldsAreStable)
{
	ReplayStateHasher first;
	first.AppendBool(true);
	first.AppendUint8(7);
	first.AppendInt32(-42);
	first.AppendUint32(9001);
	first.AppendUint64(123456789);
	first.AppendString("griswold");

	ReplayStateHasher second;
	second.AppendBool(true);
	second.AppendUint8(7);
	second.AppendInt32(-42);
	second.AppendUint32(9001);
	second.AppendUint64(123456789);
	second.AppendString("griswold");

	EXPECT_EQ(first.Digest(), second.Digest());

	second.AppendString("different");
	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayStateHasher, StringsAreLengthPrefixed)
{
	ReplayStateHasher first;
	first.AppendString("ab");

	ReplayStateHasher second;
	second.AppendString("a");
	second.AppendString("b");

	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayFixtureStateHasher, NonzeroPlayerClassMatchesCSharpGolden)
{
	ReplayFixtureExecutionState state;
	state.characterClassId = 3;

	EXPECT_EQ(ComputeReplayFixtureStateHash(state), "76769ad0f438a4b3a3869e37c4a7f72d8b66dc1fdce6902e5fedfc42b17518ca");
}

TEST(ReplayFixtureStateHasher, PlayerSpellKnowledgeMatchesCSharpGolden)
{
	ReplayFixtureExecutionState state;
	state.abilitySpellId = 27;
	state.memorizedSpells = { { 3, 1 }, { 1, 2 } };

	EXPECT_EQ(ComputeReplayFixtureStateHash(state), "55df082b2fffc775c1768adf67ce55dabaa2fa0cd811ce270fb1ddda0fad178e");
}

TEST(ReplayFixtureStateHasher, PlayerCombatModeMatchesCSharpGolden)
{
	ReplayFixtureExecutionState state;
	state.combatMode = 6;
	state.combatModeEndTick = 9;

	EXPECT_EQ(ComputeReplayFixtureStateHash(state), "a5dee363d076a45767c93fc10fc90c0b5c3b1233772c80711b1a79fb78c0a0f1");
}

TEST(ReplayFixtureStateHasher, PlayerActionMatchesCSharpGolden)
{
	ReplayFixtureExecutionState state;
	state.combatMode = 9;
	state.combatModeEndTick = 20;
	state.currentAction = {
		.defined = true,
		.kind = 3,
		.startedAtTick = 2,
		.effectTick = 10,
		.readyTick = 10,
		.endTick = 20,
		.effectApplied = true,
		.targetEntityId = 9,
		.hasCast = true,
		.spellId = 30,
		.castTargetEntityId = 9,
		.targetX = 3,
		.targetY = 4,
		.castSource = 3,
		.hasCastSourceItem = true,
		.castSourceItemLocation = 2,
		.castSourceItemSlot = 4,
	};

	EXPECT_EQ(ComputeReplayFixtureStateHash(state), "c89460e4f3faa810171be7ff6cfe5e81400c0fe970a41f47616c218de4252388");
}

TEST(ReplayCommands, SortsByTargetTickThenServerReceiptSequence)
{
	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = 3, .order = { .targetTick = 2, .serverReceiptSequence = 1 } },
	    { .clientSequence = 1, .order = { .targetTick = 1, .serverReceiptSequence = 5 } },
	    { .clientSequence = 2, .order = { .targetTick = 1, .serverReceiptSequence = 2 } },
	});

	ASSERT_EQ(sorted.size(), 3U);
	EXPECT_EQ(sorted[0].clientSequence, 2U);
	EXPECT_EQ(sorted[1].clientSequence, 1U);
	EXPECT_EQ(sorted[2].clientSequence, 3U);
}

TEST(ReplayCommands, PreservesInputOrderForDuplicateReceiptSequences)
{
	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = 1, .order = { .targetTick = 1, .serverReceiptSequence = 4 } },
	    { .clientSequence = 2, .order = { .targetTick = 1, .serverReceiptSequence = 4 } },
	});

	ASSERT_EQ(sorted.size(), 2U);
	EXPECT_EQ(sorted[0].clientSequence, 1U);
	EXPECT_EQ(sorted[1].clientSequence, 2U);
}

TEST(ReplayStateProjection, ChangesWhenAuthoritativePlayerStateChanges)
{
	Player player { };
	player._pName[0] = 'A';
	player._pName[1] = '\0';
	player._pGold = 100;
	player._pExperience = 200;
	player.life.current = 640;
	player.life.maximum = 640;

	ReplayStateHasher first;
	AppendReplayPlayerState(first, 0, player);

	player._pGold = 101;
	ReplayStateHasher second;
	AppendReplayPlayerState(second, 0, player);

	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayStateProjection, ExcludesLocalizedItemNames)
{
	Item item;
	item._iSeed = 42;
	item._itype = ItemType::Sword;
	item._iIvalue = 100;

	ReplayStateHasher first;
	AppendReplayItemState(first, item);

	item._iName[0] = 'S';
	item._iName[1] = 'w';
	item._iName[2] = 'o';
	item._iName[3] = 'r';
	item._iName[4] = 'd';
	item._iName[5] = '\0';
	item._iIName[0] = 'E';
	item._iIName[1] = '\0';
	ReplayStateHasher second;
	AppendReplayItemState(second, item);

	EXPECT_EQ(first.Digest(), second.Digest());
}

TEST(ReplayStateProjection, IncludesStoreInventoryAndSelection)
{
	StoreManager store;
	store.activeStore() = TalkID::Smith;
	store.premiumItemLevel() = 3;
	store.premiumItems().push_back();
	store.premiumItems()[0]._iSeed = 42;

	ReplayStateHasher first;
	AppendReplayStoreState(first, store);

	store.premiumItems()[0]._iSeed = 43;
	ReplayStateHasher second;
	AppendReplayStoreState(second, store);

	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayFixture, ParsesAndHashesInitialStoreState)
{
	std::ifstream file("test/fixtures/replay/stores/basic-buy.json");
	ASSERT_TRUE(file.is_open());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());

	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.formatVersion, 1U);
	EXPECT_EQ(fixture.fixtureId, "stores/basic-buy");
	EXPECT_EQ(fixture.protocolSchemaVersion, "0.1.0");
	EXPECT_EQ(fixture.tickRateHz, 20U);
	EXPECT_EQ(fixture.rngSeed, 305419896U);
	ASSERT_EQ(fixture.contentManifest.packs.size(), 1U);
	EXPECT_EQ(fixture.contentManifest.packs[0], "baseline-store-content");
	ASSERT_EQ(fixture.commands.size(), 2U);
	EXPECT_EQ(fixture.commands[0].clientSequence, 2U);
	EXPECT_EQ(fixture.commands[0].kind, "BuyItem");
	EXPECT_EQ(fixture.commands[0].storeId, 1U);
	EXPECT_EQ(fixture.commands[0].storeSlot, 0U);
	EXPECT_EQ(fixture.commands[1].storeId, 1U);
	EXPECT_EQ(fixture.commands[1].storeSlot, 0U);
	ASSERT_EQ(fixture.checkpoints.size(), 1U);
	EXPECT_EQ(fixture.checkpoints[0].tick, 0U);
	EXPECT_EQ(fixture.checkpoints[0].stateSha256, "67c0e197eb04c359e6501c0df7419799d878903d6760c471d0eae93dd12c45be");

	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = fixture.commands[0].clientSequence, .order = fixture.commands[0].order },
	    { .clientSequence = fixture.commands[1].clientSequence, .order = fixture.commands[1].order },
	});
	ASSERT_EQ(sorted.size(), 2U);
	EXPECT_EQ(sorted[0].clientSequence, 1U);
	EXPECT_EQ(sorted[1].clientSequence, 2U);

	Player player { };
	player._pName[0] = fixture.initialState.player[0];
	player._pName[1] = '\0';
	player._pClass = static_cast<HeroClass>(fixture.initialState.characterClass);
	ASSERT_EQ(fixture.initialState.characterLevel, player.getCharacterLevel());
	player._pGold = fixture.initialState.gold;
	player._pExperience = fixture.initialState.experience;
	player.life.current = fixture.initialState.life;
	player.life.maximum = fixture.initialState.life;
	player.mana.current = fixture.initialState.mana;
	player.mana.maximum = fixture.initialState.mana;
	StoreManager store;
	store.activeStore() = static_cast<TalkID>(fixture.storeState.activeStore);
	store.premiumItemCount() = fixture.storeState.premiumItemCount;
	store.premiumItemLevel() = fixture.storeState.premiumItemLevel;
	for (const uint32_t seed : fixture.storeState.premiumItemSeeds) {
		store.premiumItems().push_back();
		store.premiumItems().back()._iSeed = seed;
	}

	ReplayStateHasher state;
	AppendReplayPlayerState(state, 0, player);
	AppendReplayStoreState(state, store);
	EXPECT_EQ(fixture.initialStateSha256, state.HexDigest());
}

TEST(ReplayFixture, ParsesStructuredManifestCheckpointAndStorePayloads)
{
	constexpr std::string_view Fixture = R"({
  "content_manifest": {
    "id": "base-plus-hellfire",
    "version": "1",
    "sha256": "content-hash"
  },
  "commands": [
    {
      "client_sequence": 1,
      "target_tick": 4,
      "server_receipt_sequence": 1,
      "kind": "OpenStore",
      "payload": { "store_id": 7 }
    },
    {
      "client_sequence": 2,
      "target_tick": 5,
      "server_receipt_sequence": 2,
      "payload": { "store_id": 7, "item_index": 3 },
      "kind": "BuyItem"
    }
  ],
  "checkpoints": [
    { "tick": 0, "state_sha256": "initial-hash" },
    { "tick": 5, "state_sha256": "purchase-hash" }
  ]
})";

	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(Fixture, fixture, error)) << error;
	EXPECT_EQ(fixture.contentManifest.id, "base-plus-hellfire");
	EXPECT_EQ(fixture.contentManifest.version, "1");
	EXPECT_EQ(fixture.contentManifest.sha256, "content-hash");
	EXPECT_TRUE(fixture.contentManifest.packs.empty());
	ASSERT_EQ(fixture.commands.size(), 2U);
	EXPECT_EQ(fixture.commands[0].storeId, 7U);
	EXPECT_EQ(fixture.commands[0].storeSlot, 0U);
	EXPECT_EQ(fixture.commands[1].storeId, 7U);
	EXPECT_EQ(fixture.commands[1].storeSlot, 3U);
	ASSERT_EQ(fixture.checkpoints.size(), 2U);
	EXPECT_EQ(fixture.checkpoints[0].tick, 0U);
	EXPECT_EQ(fixture.checkpoints[0].stateSha256, "initial-hash");
	EXPECT_EQ(fixture.checkpoints[1].tick, 5U);
	EXPECT_EQ(fixture.checkpoints[1].stateSha256, "purchase-hash");
	EXPECT_EQ(fixture.initialStateSha256, "initial-hash");
}

TEST(ReplayFixture, ParsesTransactionParityCommands)
{
	std::ifstream file("test/fixtures/replay/stores/transaction-parity.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.manaMaximum, 640);
	ASSERT_EQ(fixture.commands.size(), 5U);
	ASSERT_EQ(fixture.checkpoints.size(), 5U);
	EXPECT_EQ(fixture.checkpoints[0].tick, 10U);
	EXPECT_EQ(fixture.checkpoints[1].tick, 12U);
	EXPECT_EQ(fixture.checkpoints[2].tick, 14U);
	EXPECT_EQ(fixture.checkpoints[3].tick, 15U);
	EXPECT_EQ(fixture.checkpoints[4].tick, 16U);
	EXPECT_EQ(fixture.checkpoints[0].stateSha256, "3f16d8ac10153a38a8d55deb2ba0f0c9025c24483a85ad0274c5b430316413b6");
	EXPECT_EQ(fixture.checkpoints[4].stateSha256, "012a906403d44585b1e4a67933b8dcc38c11e1baf68e16a55b859545d77eba6b");
	EXPECT_EQ(fixture.commands[2].kind, "SellItem");
	EXPECT_EQ(fixture.commands[2].storeSlot, 0U);
	EXPECT_EQ(fixture.commands[3].kind, "OpenStore");
	EXPECT_EQ(fixture.commands[3].storeId, 10U);
	EXPECT_EQ(fixture.commands[4].kind, "RefillMana");
}

TEST(ReplayFixture, ExecutesTransactionTransitionsAndMatchesCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/stores/transaction-parity.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.lifeMaximum = fixture.initialState.life;
	state.characterLevel = fixture.initialState.characterLevel;
	state.gold = fixture.initialState.gold;
	state.experience = fixture.initialState.experience;
	ReplayFixtureItemState itemState;
	itemState.itemType = 1;
	itemState.value = 75;
	itemState.identifiedValue = 75;
	itemState.durability = 1;
	itemState.maxDurability = 1;
	state.activeStoreItems.push_back({ 0, 42, 75, itemState });

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 5U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.gold, 33U);
	EXPECT_EQ(state.mana, 640);
	EXPECT_TRUE(state.inventory.empty());
	EXPECT_EQ(result.transitions.back().stateSha256, fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoldTopologyStoreTransitionsAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/stores/gold-topology-parity.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.goldStackSeed, 123U);
	EXPECT_EQ(fixture.initialState.goldStackCell, 30U);
	ASSERT_EQ(fixture.commands.size(), 5U);
	EXPECT_EQ(fixture.commands[2].storeSlot, 0U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.lifeMaximum = fixture.initialState.life;
	state.characterLevel = fixture.initialState.characterLevel;
	state.gold = fixture.initialState.gold;
	state.experience = fixture.initialState.experience;
	ReplayFixtureItemState itemState;
	itemState.itemType = 1;
	itemState.value = 100;
	itemState.identifiedValue = 100;
	itemState.durability = 1;
	itemState.maxDurability = 1;
	state.activeStoreItems.push_back({ 0, 42, 100, itemState });

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 5U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.gold, 15U);
	EXPECT_EQ(state.mana, 640);
	ASSERT_EQ(state.inventory.size(), 1U);
	EXPECT_NE(state.inventory[0].itemSeed, 123U);
	EXPECT_EQ(state.inventory[0].price, 15U);
	ASSERT_EQ(state.inventoryGrid.size(), 40U);
	EXPECT_EQ(state.inventoryGrid[39], 0);
	EXPECT_NE(state.randomState, fixture.rngSeed);
	EXPECT_EQ(result.transitions.back().stateSha256, fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesElementalEquipmentAttackAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-elemental-equipment.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialState.equipment.size(), 2U);
	EXPECT_EQ(fixture.initialState.equipment[0].fireMinDamage, 4);
	EXPECT_EQ(fixture.initialState.equipment[1].lightningMaxDamage, 8);

	ReplayFixtureExecutionState state;
	state.gold = fixture.initialState.gold;
	state.experience = fixture.initialState.experience;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.combatTargetHitPoints, 82);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesRangedElementalArrowAttackAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-elemental-arrows.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialState.equipment.size(), 2U);
	EXPECT_EQ(fixture.initialState.equipment[0].flags, 1U << 3);
	EXPECT_EQ(fixture.initialState.equipment[1].flags, 1U << 4);

	ReplayFixtureExecutionState state;
	state.gold = fixture.initialState.gold;
	state.experience = fixture.initialState.experience;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.combatTargetHitPoints, 100);
	EXPECT_EQ(state.randomState, 22695478U);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].damageQ6, 64);
	EXPECT_EQ(state.projectiles[0].elementalFireDamage, 4);
	EXPECT_TRUE(state.projectiles[0].attackHit);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesBaseContentPurchaseAndSaleAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/stores/base-content-purchase.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.lifeMaximum = fixture.initialState.life;
	state.characterLevel = fixture.initialState.characterLevel;
	state.gold = fixture.initialState.gold;
	state.experience = fixture.initialState.experience;
	ReplayFixtureItemState itemState;
	itemState.createInfo = 42;
	itemState.itemType = 1;
	itemState.value = 75;
	itemState.identifiedValue = 75;
	itemState.identified = true;
	itemState.itemIndex = 1;
	itemState.minDamage = 4;
	itemState.maxDamage = 8;
	itemState.durability = 20;
	itemState.maxDurability = 20;
	itemState.baseName = "Short Sword";
	itemState.identifiedName = "Short Sword";
	state.activeStoreItems.push_back({ 0, 42, 75, itemState });
	ReplayFixtureItemState armorState;
	armorState.createInfo = 43;
	armorState.itemType = 1;
	armorState.value = 25;
	armorState.identifiedValue = 25;
	armorState.identified = true;
	armorState.itemIndex = 2;
	armorState.armorClass = 5;
	armorState.durability = 20;
	armorState.maxDurability = 20;
	armorState.baseName = "Buckler";
	armorState.identifiedName = "Buckler";
	state.activeStoreItems.push_back({ 1, 43, 25, armorState });

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.gold, 43U);
	for (size_t index = 0; index < result.transitions.size(); ++index)
		EXPECT_EQ(result.transitions[index].stateSha256, fixture.checkpoints[index].stateSha256);
}

TEST(ReplayFixture, ExecutesGameplayTransitionsAndMatchesCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-movement-combat.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.lifeMaximum = fixture.initialState.life;
	state.characterLevel = fixture.initialState.characterLevel;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.combatTargetEntityId = 9;
	state.combatTargetPositionX = 2;
	state.combatTargetPositionY = 0;
	state.combatTargetHitPoints = 11;
	state.combatTargetMaxHitPoints = 11;
	state.combatTargetArmorClass = 2;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.positionX, 1);
	EXPECT_EQ(state.experience, 100U);
	EXPECT_EQ(result.transitions.back().stateSha256, fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesNativeTimedPlayerActionsAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-action-lifecycle.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.playerActionTiming.defined);
	EXPECT_EQ(fixture.playerActionTiming.walkingFrames, 8U);
	EXPECT_EQ(fixture.playerActionTiming.attackFrames, 16U);
	EXPECT_EQ(fixture.playerActionTiming.attackActionFrame, 9U);
	EXPECT_EQ(fixture.playerActionTiming.castingFrames, 20U);
	EXPECT_EQ(fixture.playerActionTiming.castingActionFrame, 14U);
	EXPECT_EQ(fixture.playerActionTiming.deathFrames, 20U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = 40;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.characterLevel = fixture.initialState.characterLevel;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.memorizedSpells = fixture.initialState.memorizedSpells;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.positionX, 1);
	EXPECT_EQ(state.experience, 100U);
	EXPECT_EQ(state.life, 40);
	EXPECT_EQ(state.mana, 5);
	EXPECT_EQ(state.combatMode, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, AppliesQuickAttackFirstFrameSkipToChainedMeleeAction)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-action-lifecycle.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	fixture.initialState.combatTargetHitPoints = 100;
	fixture.initialState.combatTargetMaxHitPoints = 100;
	// This probe changes the fixture state in memory; timing assertions do not
	// claim the original shared checkpoint hashes.
	fixture.contentManifest = {};
	fixture.initialState.equipment.push_back({
	    .slot = 4,
	    .itemSeed = 100,
	    .itemType = 1,
	    .itemClass = 1,
	    .flags = 1U << 17,
	    .statFlag = true,
	});
	ReplayFixtureCommand chainedAttack = fixture.commands[1];
	fixture.commands.resize(2);
	chainedAttack.clientSequence = 4;
	chainedAttack.order.targetTick = 28;
	chainedAttack.order.serverReceiptSequence = 4;
	fixture.commands.push_back(chainedAttack);
	fixture.checkpoints.erase(
	    std::remove_if(fixture.checkpoints.begin(), fixture.checkpoints.end(), [](const auto &checkpoint) {
		    return checkpoint.tick != 10 && checkpoint.tick != 19 && checkpoint.tick != 28;
	    }),
	    fixture.checkpoints.end());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.characterLevel = fixture.initialState.characterLevel;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	ASSERT_TRUE(state.currentAction.defined);
	EXPECT_EQ(state.currentAction.effectTick, 35U);
	EXPECT_EQ(state.currentAction.readyTick, 36U);
	EXPECT_EQ(state.currentAction.endTick, 42U);
}

void VerifyNativeTimedRangedPlayerAction(
    const char *fixturePath,
    std::string_view expectedVariant,
    uint64_t expectedEffectTick,
    uint64_t expectedReadyTick,
    uint64_t expectedEndTick,
    uint64_t expectedMoveTick)
{
	std::ifstream file(fixturePath);
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.gameVariant, expectedVariant);
	ASSERT_TRUE(fixture.playerActionTiming.defined);
	EXPECT_EQ(fixture.playerActionTiming.walkingFrames, 8U);
	EXPECT_EQ(fixture.playerActionTiming.attackFrames, 12U);
	EXPECT_EQ(fixture.playerActionTiming.attackActionFrame, 7U);
	EXPECT_EQ(fixture.playerActionTiming.deathFrames, 20U);
	ASSERT_EQ(fixture.initialState.equipment.size(), 1U);
	EXPECT_EQ(fixture.initialState.equipment[0].slot, 4U);
	EXPECT_EQ(fixture.initialState.equipment[0].itemType, 3);
	EXPECT_EQ(fixture.initialState.equipment[0].itemClass, 1);
	EXPECT_EQ(fixture.initialState.equipment[0].flags, 1U << 18);
	EXPECT_TRUE(fixture.initialState.equipment[0].statFlag);
	ASSERT_EQ(fixture.commands.size(), 2U);
	EXPECT_EQ(fixture.commands[1].order.targetTick, expectedMoveTick);

	auto CreateState = [&fixture]() {
		ReplayFixtureExecutionState state;
		state.gold = fixture.initialState.gold;
		state.life = fixture.initialState.life;
		state.lifeMaximum = fixture.initialState.life;
		state.mana = fixture.initialState.mana;
		state.manaMaximum = fixture.initialState.manaMaximum;
		state.characterLevel = fixture.initialState.characterLevel;
		state.positionX = fixture.initialState.positionX;
		state.positionY = fixture.initialState.positionY;
		state.levelId = fixture.initialState.levelId;
		state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
		// Match the C# ranged fixture setup, which activates the Rogue class
		// definition and its native starting attributes.
		if (state.characterClassId == 1) {
			state.attributes = { 20, 20, 15, 15, 30, 30, 20, 20 };
		}
		return state;
	};

	ReplayFixture probe = fixture;
	probe.commands.resize(1);
	probe.checkpoints.erase(
	    std::remove_if(probe.checkpoints.begin(), probe.checkpoints.end(), [](const auto &checkpoint) {
		    return checkpoint.tick != 10;
	    }),
	    probe.checkpoints.end());
	auto probeState = CreateState();
	ReplayFixtureExecutionResult probeResult;
	ASSERT_TRUE(ExecuteReplayFixture(probe, probeState, probeResult, error)) << error;
	EXPECT_EQ(probeState.combatMode, 5U);
	ASSERT_TRUE(probeState.currentAction.defined);
	EXPECT_EQ(probeState.currentAction.effectTick, expectedEffectTick);
	EXPECT_EQ(probeState.currentAction.readyTick, expectedReadyTick);
	EXPECT_EQ(probeState.currentAction.endTick, expectedEndTick);

	auto state = CreateState();
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.positionX, 1);
	EXPECT_EQ(state.experience, 100U);
	EXPECT_EQ(state.combatMode, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	ASSERT_EQ(state.equipment.size(), 1U);
	EXPECT_EQ(state.equipment[0].state.itemType, 3);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesDiabloFastBowTimelineAgainstCSharpCheckpoints)
{
	VerifyNativeTimedRangedPlayerAction(
	    "test/fixtures/replay/gameplay-player-action-ranged-diablo.json",
	    "Diablo",
	    15,
	    16,
	    20,
	    16);
}

TEST(ReplayFixture, ExecutesHellfireFastBowTimelineAgainstCSharpCheckpoints)
{
	VerifyNativeTimedRangedPlayerAction(
	    "test/fixtures/replay/gameplay-player-action-ranged-hellfire.json",
	    "Hellfire",
	    16,
	    17,
	    21,
	    17);
}

TEST(ReplayFixture, ExecutesPlayerDeathLifecycleAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-death-lifecycle.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.playerActionTiming.deathFrames, 20U);
	EXPECT_EQ(fixture.initialState.combatTargetAttackDamage, 5);
	EXPECT_EQ(fixture.initialState.combatTargetAggroRange, 2);
	EXPECT_EQ(fixture.initialState.goldStackSeed, 123U);
	EXPECT_EQ(fixture.initialState.goldStackCell, 0U);

	auto CreateState = [&fixture]() {
		ReplayFixtureExecutionState state;
		state.gold = fixture.initialState.gold;
		state.life = fixture.initialState.life;
		state.lifeMaximum = fixture.initialState.life;
		state.mana = fixture.initialState.mana;
		state.manaMaximum = fixture.initialState.manaMaximum;
		state.positionX = fixture.initialState.positionX;
		state.positionY = fixture.initialState.positionY;
		state.characterLevel = fixture.initialState.characterLevel;
		state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
		return state;
	};

	ReplayFixture probe = fixture;
	probe.checkpoints.erase(
	    std::remove_if(probe.checkpoints.begin(), probe.checkpoints.end(), [](const auto &checkpoint) {
		    return checkpoint.tick > 2;
	    }),
	    probe.checkpoints.end());
	auto probeState = CreateState();
	ReplayFixtureExecutionResult probeResult;
	ASSERT_TRUE(ExecuteReplayFixture(probe, probeState, probeResult, error)) << error;
	EXPECT_EQ(probeState.life, 0);
	EXPECT_EQ(probeState.positionX, 0);
	EXPECT_EQ(probeState.positionY, 0);
	EXPECT_EQ(probeState.combatMode, 8U);
	EXPECT_EQ(probeState.combatModeEndTick, 41U);
	ASSERT_TRUE(probeState.currentAction.defined);
	EXPECT_EQ(probeState.currentAction.kind, 4U);
	EXPECT_TRUE(probeState.currentAction.effectApplied);
	EXPECT_EQ(probeState.currentAction.targetEntityId, 9U);
	EXPECT_EQ(probeState.currentAction.endTick, 41U);
	EXPECT_EQ(probeState.gold, 50U);
	ASSERT_EQ(probeState.inventory.size(), 1U);
	EXPECT_EQ(probeState.inventory[0].itemSeed, 123U);
	EXPECT_EQ(probeState.inventory[0].price, 51U);
	EXPECT_EQ(probeState.inventory[0].state.value, 51);
	ASSERT_EQ(probeState.inventoryGrid.size(), 40U);
	EXPECT_EQ(probeState.inventoryGrid[0], 0);
	EXPECT_EQ(probeState.worldItemEntityId, 0x40000000U);
	EXPECT_EQ(probeState.worldItemPositionX, 0);
	EXPECT_EQ(probeState.worldItemPositionY, 1);
	EXPECT_EQ(probeState.worldItemPrice, 50U);
	EXPECT_EQ(probeState.worldItemState.itemType, 11);
	EXPECT_EQ(probeState.worldItemState.itemIndex, 0);
	EXPECT_EQ(probeState.worldItemState.value, 50);
	EXPECT_TRUE(probeState.worldItemState.statFlag);
	EXPECT_EQ(probeState.worldItemSeed, 0x015A4E36U);
	EXPECT_EQ(probeState.randomState, probeState.worldItemSeed);

	auto state = CreateState();
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 0);
	EXPECT_EQ(state.positionX, 0);
	EXPECT_EQ(state.positionY, 0);
	EXPECT_EQ(state.combatMode, 8U);
	EXPECT_EQ(state.combatModeEndTick, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	EXPECT_EQ(state.gold, 50U);
	ASSERT_EQ(state.inventory.size(), 1U);
	EXPECT_EQ(state.inventory[0].price, 51U);
	EXPECT_EQ(state.worldItemPrice, 50U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerDeathSuppressionAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-death-suppression.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.gameMode, "Multiplayer");
	EXPECT_TRUE(fixture.initialState.suppressMultiplayerDeathDrops);

	ReplayFixtureExecutionState state;
	state.gold = fixture.initialState.gold;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_EQ(state.life, 0);
	EXPECT_EQ(state.combatMode, 8U);
	EXPECT_EQ(state.gold, 101U);
	ASSERT_EQ(state.inventory.size(), 1U);
	EXPECT_EQ(state.inventory[0].itemSeed, 123U);
	EXPECT_EQ(state.inventory[0].price, 101U);
	EXPECT_EQ(state.worldItemEntityId, 0U);
	EXPECT_EQ(state.randomState, 1U);

	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesEquippedDeathDropsAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-equipped-death-drops.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.facingDirection, 6U);
	ASSERT_EQ(fixture.initialState.equipment.size(), 3U);

	ReplayFixtureExecutionState state;
	state.gold = fixture.initialState.gold;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.questId = fixture.initialState.questId;
	state.questLevelId = fixture.initialState.levelId;
	state.questRequiredProgress = fixture.initialState.questRequiredProgress;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 0);
	EXPECT_EQ(state.combatMode, 8U);
	EXPECT_EQ(state.facingDirection, 6U);
	EXPECT_TRUE(state.equipment.empty());
	ASSERT_EQ(state.worldItems.size(), 3U);
	std::sort(state.worldItems.begin(), state.worldItems.end(), [](const auto &left, const auto &right) {
		return left.entityId < right.entityId;
	});
	EXPECT_EQ(state.worldItems[0].itemSeed, 101U);
	EXPECT_EQ(state.worldItems[1].itemSeed, 102U);
	EXPECT_EQ(state.worldItems[2].itemSeed, 103U);
	EXPECT_EQ(std::pair(state.worldItems[0].positionX, state.worldItems[0].positionY), std::pair(10, 9));
	EXPECT_EQ(std::pair(state.worldItems[1].positionX, state.worldItems[1].positionY), std::pair(9, 11));
	EXPECT_EQ(std::pair(state.worldItems[2].positionX, state.worldItems[2].positionY), std::pair(10, 11));
	EXPECT_TRUE(std::all_of(state.worldItems.begin(), state.worldItems.end(), [](const auto &item) {
		return item.price == 34;
	}));
}

TEST(ReplayFixture, ExecutesPlayerRestartTownAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-restart-town.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.combatTargetLevelId, 1U);

	auto CreateState = [&fixture]() {
		ReplayFixtureExecutionState state;
		state.life = fixture.initialState.life;
		state.lifeMaximum = fixture.initialState.lifeMaximum;
		state.mana = fixture.initialState.mana;
		state.manaMaximum = fixture.initialState.manaMaximum;
		state.positionX = fixture.initialState.positionX;
		state.positionY = fixture.initialState.positionY;
		state.levelId = fixture.initialState.levelId;
		state.characterLevel = fixture.initialState.characterLevel;
		state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
		return state;
	};

	ReplayFixture probe = fixture;
	probe.checkpoints.erase(
	    std::remove_if(probe.checkpoints.begin(), probe.checkpoints.end(), [](const auto &checkpoint) {
		    return checkpoint.tick > 42;
	    }),
	    probe.checkpoints.end());
	auto probeState = CreateState();
	ReplayFixtureExecutionResult probeResult;
	ASSERT_TRUE(ExecuteReplayFixture(probe, probeState, probeResult, error)) << error;
	EXPECT_EQ(probeState.levelId, 0U);
	EXPECT_EQ(probeState.positionX, 75);
	EXPECT_EQ(probeState.positionY, 68);
	EXPECT_EQ(probeState.life, 64);
	EXPECT_EQ(probeState.mana, 0);
	EXPECT_EQ(probeState.combatMode, 10U);
	EXPECT_EQ(probeState.combatModeEndTick, 43U);
	ASSERT_TRUE(probeState.currentAction.defined);
	EXPECT_EQ(probeState.currentAction.kind, 6U);
	EXPECT_TRUE(probeState.currentAction.effectApplied);
	EXPECT_EQ(probeState.currentAction.targetEntityId, 0U);

	auto state = CreateState();
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.levelId, 0U);
	EXPECT_EQ(state.positionX, 75);
	EXPECT_EQ(state.positionY, 68);
	EXPECT_EQ(state.life, 64);
	EXPECT_EQ(state.mana, 0);
	EXPECT_EQ(state.combatMode, 0U);
	EXPECT_EQ(state.combatModeEndTick, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesPlayerLevelTransitionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-level-transition.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	auto CreateState = [&fixture]() {
		ReplayFixtureExecutionState state;
		state.life = fixture.initialState.life;
		state.lifeMaximum = fixture.initialState.life;
		state.mana = fixture.initialState.mana;
		state.manaMaximum = fixture.initialState.manaMaximum;
		state.positionX = fixture.initialState.positionX;
		state.positionY = fixture.initialState.positionY;
		state.levelId = fixture.initialState.levelId;
		state.characterLevel = fixture.initialState.characterLevel;
		state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
		state.portalId = 5;
		state.portalDestinationLevelId = 2;
		state.portalDestinationX = 3;
		state.portalDestinationY = 4;
		return state;
	};

	ReplayFixture probe = fixture;
	probe.checkpoints.erase(
	    std::remove_if(probe.checkpoints.begin(), probe.checkpoints.end(), [](const auto &checkpoint) {
		    return checkpoint.tick != 1;
	    }),
	    probe.checkpoints.end());
	auto probeState = CreateState();
	ReplayFixtureExecutionResult probeResult;
	ASSERT_TRUE(ExecuteReplayFixture(probe, probeState, probeResult, error)) << error;
	EXPECT_EQ(probeState.levelId, 2U);
	EXPECT_EQ(probeState.positionX, 3);
	EXPECT_EQ(probeState.positionY, 4);
	EXPECT_EQ(probeState.combatMode, 10U);
	EXPECT_EQ(probeState.combatModeEndTick, 2U);
	ASSERT_TRUE(probeState.currentAction.defined);
	EXPECT_EQ(probeState.currentAction.kind, 5U);
	EXPECT_TRUE(probeState.currentAction.effectApplied);
	EXPECT_EQ(probeState.currentAction.targetEntityId, 5U);

	auto state = CreateState();
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.levelId, 2U);
	EXPECT_EQ(state.positionX, 3);
	EXPECT_EQ(state.positionY, 4);
	EXPECT_EQ(state.combatMode, 0U);
	EXPECT_EQ(state.combatModeEndTick, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, RejectsInvalidPlayerActionTiming)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-action-lifecycle.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = "\"walking_frames\": 8";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), "\"walking_frames\": 0");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid player-action timing.");
}

TEST(ReplayFixture, RejectsInvalidPlayerDeathTiming)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-death-lifecycle.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = "\"death_frames\": 20";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), "\"death_frames\": 128");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid player-action timing.");
}

TEST(ReplayFixture, RejectsNegativeCombatTargetAttackDamage)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-death-lifecycle.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = "\"combat_target_attack_damage\": 5";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), "\"combat_target_attack_damage\": -1");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid combat-target occupancy state.");
}

TEST(ReplayFixture, RejectsInvalidInitialEquipment)
{
	std::ifstream file("test/fixtures/replay/gameplay-player-action-ranged-diablo.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = "\"item_seed\": 100";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), "\"item_seed\": 0");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_NE(error.find("invalid or duplicate equipment item"), std::string::npos);
}

TEST(ReplayFixture, ExecutesDataDrivenSpellTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-spell-cast.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = 40;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.memorizedSpells = fixture.initialState.memorizedSpells;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 40);
	EXPECT_EQ(state.mana, 5);
}

TEST(ReplayFixture, ExecutesChargedBoltSpawnVectorAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-charged-bolt.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.commands.size(), 1U);
	EXPECT_EQ(fixture.commands[0].targetX, 4);
	EXPECT_EQ(fixture.commands[0].targetY, 0);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.mana, 14);
	ASSERT_EQ(state.projectiles.size(), 5U);
	EXPECT_EQ(state.projectiles[0].pathIndex, 13);
	EXPECT_EQ(state.projectiles[0].damage, 7);
	EXPECT_EQ(state.projectiles[0].velocityX, 370720);
	EXPECT_EQ(state.projectiles[0].velocityY, 185360);
	EXPECT_EQ(state.projectiles[4].pathIndex, 8);
	EXPECT_EQ(state.projectiles[4].damage, 1);
	EXPECT_EQ(state.randomState, 0x7162D42BU);
	EXPECT_EQ(state.facingDirection, 7U);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
}

TEST(ReplayFixture, ExecutesChargedBoltResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-charged-bolt-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.combatTargetEntityId = 9;
	state.combatTargetPositionX = 1;
	state.combatTargetPositionY = 1;
	state.combatTargetHitPoints = 30;
	state.combatTargetMaxHitPoints = 30;
	state.combatTargetLightningResistance = 25;
	state.combatTargetMonsterLevel = 1;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 5U);
	EXPECT_EQ(state.combatTargetHitPoints, 27);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 1696);
	EXPECT_EQ(state.randomState, 0x80DD0724U);
	EXPECT_EQ(state.projectiles.size(), 4U);
	EXPECT_EQ(std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.phase == 1;
	}),
	    2);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesFireboltResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-firebolt-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.combatTargetFireResistance, 25);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 2;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 3U);
	EXPECT_EQ(state.mana, 680);
	EXPECT_EQ(state.combatTargetHitPoints, 8);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 464);
	EXPECT_EQ(state.randomState, 0x3F10CD47U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesFireballResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-fireball-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_TRUE(fixture.initialState.combatTargetPetrified);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 16;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 17U);
	EXPECT_EQ(state.mana, 1104);
	EXPECT_EQ(state.combatTargetHitPoints, 156);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 9926);
	EXPECT_EQ(state.randomState, 0xE8098E4AU);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, RejectsInvalidFireboltResistance)
{
	std::ifstream file("test/fixtures/replay/gameplay-firebolt-resolution.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = R"("combat_target_fire_resistance": 25)";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), R"("combat_target_fire_resistance": 101)");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid combat-target occupancy state.");
}

TEST(ReplayFixture, ExecutesLightningResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-lightning-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.combatTargetLightningResistance, 25);
	EXPECT_TRUE(fixture.initialState.combatTargetPetrified);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 3;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 9U);
	EXPECT_EQ(state.mana, 488);
	EXPECT_EQ(state.combatTargetHitPoints, 6);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 384);
	EXPECT_EQ(state.randomState, 0xCBBBD6A5U);
	EXPECT_EQ(state.projectiles.size(), 6U);
	EXPECT_EQ(std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 3;
	}),
	    1);
	EXPECT_EQ(std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 4;
	}),
	    5);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, RejectsInvalidLightningResistance)
{
	std::ifstream file("test/fixtures/replay/gameplay-lightning-resolution.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = R"("combat_target_lightning_resistance": 25)";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), R"("combat_target_lightning_resistance": 101)");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid combat-target occupancy state.");
}

TEST(ReplayFixture, ExecutesFlashResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-flash-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.combatTargetMagicResistance, 25);
	EXPECT_TRUE(fixture.initialState.combatTargetPetrified);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 5;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 20U);
	EXPECT_EQ(state.mana, 1336);
	EXPECT_EQ(state.combatTargetHitPoints, 97);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 6165);
	EXPECT_EQ(state.randomState, 0xE4BDD038U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHealingResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-healing-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.lifeMaximum, 5000);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 8;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	for (size_t index = 0; index < result.transitions.size(); ++index)
		EXPECT_EQ(result.transitions[index].stateSha256, fixture.checkpoints[index].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 3U);
	EXPECT_EQ(state.life, 5000);
	EXPECT_EQ(state.mana, 1656);
	EXPECT_EQ(state.randomState, 0x8C3C2F4FU);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHealOtherResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-heal-other-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	EXPECT_EQ(fixture.initialState.secondaryPlayer.life, 1000);
	EXPECT_EQ(fixture.initialState.secondaryPlayer.lifeMaximum, 10000);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 17;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 18U);
	EXPECT_EQ(state.life, 1000);
	EXPECT_EQ(state.mana, 2552);
	EXPECT_EQ(state.secondaryPlayerLife, 8488);
	EXPECT_EQ(state.randomState, 0xCB5679F1U);
	EXPECT_EQ(state.combatMode, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesManaShieldResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-mana-shield-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 12;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 5U);
	EXPECT_EQ(state.life, 1080);
	EXPECT_EQ(state.mana, 0);
	EXPECT_FALSE(state.manaShieldActive);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesStoneCurseResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-stone-curse-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 13;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	for (size_t index = 0; index < result.transitions.size(); ++index)
		EXPECT_EQ(result.transitions[index].stateSha256, fixture.checkpoints[index].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 146U);
	EXPECT_EQ(state.mana, 3088);
	EXPECT_EQ(state.combatTargetPositionX, 10);
	EXPECT_EQ(state.combatTargetPositionY, 9);
	EXPECT_FALSE(state.combatTargetMoving);
	EXPECT_FALSE(state.combatTargetPetrified);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesInfravisionResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-infravision-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.spellProjectileBehavior = 14;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 1585U);
	EXPECT_TRUE(state.belt.empty());
	EXPECT_TRUE(state.statusEffects.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSearchResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-search-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 18;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 510U);
	EXPECT_EQ(state.mana, 960);
	EXPECT_TRUE(state.statusEffects.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesTeleportResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-teleport-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 9;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 16U);
	EXPECT_EQ(state.positionX, 9);
	EXPECT_EQ(state.positionY, 10);
	EXPECT_EQ(state.mana, 1592);
	EXPECT_EQ(state.combatMode, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesPhasingResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-phasing-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 15;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 20U);
	EXPECT_EQ(state.positionX, 24);
	EXPECT_EQ(state.positionY, 15);
	EXPECT_EQ(state.mana, 9488);
	EXPECT_EQ(state.facingDirection, 6U);
	EXPECT_EQ(state.randomState, 0x955E76D9U);
	EXPECT_EQ(state.combatMode, 0U);
	EXPECT_FALSE(state.currentAction.defined);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesResurrectionResolutionTimelineAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-resurrection-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	EXPECT_EQ(fixture.initialState.secondaryPlayer.life, 0);
	EXPECT_EQ(fixture.initialState.secondaryPlayer.lifeMaximum, 1000);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 10;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 28U);
	EXPECT_EQ(state.mana, 1720);
	EXPECT_EQ(state.secondaryPlayerLife, 640);
	EXPECT_EQ(state.secondaryPlayerPositionX, 10);
	EXPECT_EQ(state.secondaryPlayerPositionY, 10);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 0U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, RejectsInitialLifeMaximumBelowCurrentLife)
{
	std::ifstream file("test/fixtures/replay/gameplay-healing-resolution.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = R"("life_maximum": 5000)";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), R"("life_maximum": 999)");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid initial player life.");
}

TEST(ReplayFixture, RejectsInvalidFlashResistance)
{
	std::ifstream file("test/fixtures/replay/gameplay-flash-resolution.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string valid = R"("combat_target_magic_resistance": 25)";
	const size_t position = json.find(valid);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, valid.size(), R"("combat_target_magic_resistance": 101)");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid combat-target occupancy state.");
}

TEST(ReplayFixture, ExecutesChargedBoltPvpAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-charged-bolt-pvp.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_LT(state.secondaryPlayerLife, fixture.initialState.secondaryPlayer.life);
	EXPECT_EQ(state.lastAppliedTick, 5U);
	EXPECT_EQ(state.randomState, 0x80DD0724U);
	EXPECT_EQ(state.projectiles.size(), 4U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesFireboltPvpAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-firebolt-pvp.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 2;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_LT(state.secondaryPlayerLife, fixture.initialState.secondaryPlayer.life);
	EXPECT_EQ(state.lastAppliedTick, 5U);
	EXPECT_EQ(state.randomState, 0x3F10CD47U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesFireballPvpAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-fireball-pvp.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 16;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_EQ(state.secondaryPlayerLife, 1123);
	EXPECT_EQ(state.lastAppliedTick, 17U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesFireWallControllerAndSegmentLifecycle)
{
	std::ifstream file("test/fixtures/replay/gameplay-fire-wall-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 22;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.lastAppliedTick, 8U);
	EXPECT_FALSE(state.projectiles.empty());
	EXPECT_EQ(
		std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
			return projectile.behavior == 22;
		}),
		0);
	EXPECT_TRUE(std::all_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 23;
	}));
}

TEST(ReplayFixture, ExecutesLightningWallControllerAndSegmentLifecycle)
{
	std::ifstream file("test/fixtures/replay/gameplay-lightning-wall-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 47;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.lastAppliedTick, 8U);
	EXPECT_FALSE(state.projectiles.empty());
	EXPECT_EQ(
		std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
			return projectile.behavior == 47;
		}),
		0);
	EXPECT_TRUE(std::all_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 23 && projectile.damageType == 2;
	}));
}

TEST(ReplayFixture, ExecutesRingOfFireCrawlAndWallSegments)
{
	std::ifstream file("test/fixtures/replay/gameplay-ring-of-fire-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 42;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.lastAppliedTick, 8U);
	EXPECT_EQ(state.projectiles.size(), 24U);
	EXPECT_TRUE(std::all_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 23 && projectile.damageType == 1 && projectile.remainingTicks > 0;
	}));
}

TEST(ReplayFixture, ExecutesNovaRadialFanControllerAndBalls)
{
	std::ifstream file("test/fixtures/replay/gameplay-nova-radial-fan-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 30;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.projectiles.size(), 36U);
	EXPECT_TRUE(std::all_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 31
			&& projectile.damageType == 2
			&& projectile.remainingTicks == 254
			&& projectile.speed == 16;
	}));
}

TEST(ReplayFixture, ExecutesFlameWaveBranchesAndSegmentMovement)
{
	std::ifstream file("test/fixtures/replay/gameplay-flame-wave-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 24;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.projectiles.size(), 9U);
	EXPECT_TRUE(std::all_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 25
			&& projectile.damageType == 1
			&& projectile.remainingTicks == 254
			&& projectile.speed == 16;
	}));
}

TEST(ReplayFixture, ExecutesChainLightningFanOutAndImpactResolution)
{
	std::ifstream file("test/fixtures/replay/gameplay-chain-lightning-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 28;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.combatTargetHitPoints, 94);
	EXPECT_EQ(std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 29;
	}), 2);
	EXPECT_EQ(std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 4;
	}), 2);
	EXPECT_TRUE(std::all_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return (projectile.behavior == 29 && projectile.remainingTicks == 254)
			|| (projectile.behavior == 4 && projectile.remainingTicks == 5);
	}));
}

TEST(ReplayFixture, ExecutesImmolationRadialFanControllerAndFireballs)
{
	std::ifstream file("test/fixtures/replay/gameplay-immolation-radial-fan-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 43;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.projectiles.size(), 36U);
	for (const auto &projectile : state.projectiles) {
		EXPECT_EQ(projectile.behavior, 16);
		EXPECT_EQ(projectile.damageType, 1);
		EXPECT_EQ(projectile.remainingTicks, 255U);
		EXPECT_EQ(projectile.speed, 16);
		EXPECT_EQ(projectile.impactLifetime, 1);
	}
}

TEST(ReplayFixture, ExecutesElementalRetargetingProjectileLifecycle)
{
	std::ifstream file("test/fixtures/replay/gameplay-elemental-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 35;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 35);
	EXPECT_EQ(state.projectiles[0].damageType, 1);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(state.projectiles[0].speed, 16);
	EXPECT_EQ(state.projectiles[0].impactLifetime, 14);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGuardianTurretAndFireboltAdmission)
{
	std::ifstream file("test/fixtures/replay/gameplay-guardian-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.facingDirection = fixture.initialState.facingDirection;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 36;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 15U);
	EXPECT_EQ(state.projectiles.size(), 2U);
	EXPECT_EQ(std::count_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 2;
	}), 1);
	EXPECT_TRUE(std::any_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 36 && projectile.remainingTicks == 16;
	}));
	const auto controller = std::find_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 36;
	});
	ASSERT_NE(controller, state.projectiles.end());
	EXPECT_EQ(controller->guardianState, 3);
	EXPECT_TRUE(std::any_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 2
			&& projectile.remainingTicks == 255
			&& projectile.speed == 16;
	}));
	const auto bolt = std::find_if(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 2;
	});
	ASSERT_NE(bolt, state.projectiles.end());
	EXPECT_EQ(bolt->positionX, 5);
	EXPECT_EQ(bolt->positionY, 5);
	EXPECT_EQ(bolt->targetX, 16);
	EXPECT_EQ(bolt->targetY, 10);
	EXPECT_EQ(bolt->damage, 5);
	EXPECT_EQ(bolt->velocityX, 368176);
	EXPECT_EQ(bolt->velocityY, 490896);
	EXPECT_EQ(state.randomState, 1443805422U);
	EXPECT_FALSE(ComputeReplayFixtureStateHash(state).empty());
}

TEST(ReplayFixture, ExecutesRageActiveCooldownAndExpiryDamage)
{
	std::ifstream file("test/fixtures/replay/gameplay-rage-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 34;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.lastAppliedTick, 495U);
	EXPECT_EQ(state.life, 6394);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_FALSE(ComputeReplayFixtureStateHash(state).empty());
}

TEST(ReplayFixture, ExecutesInfernoControllerAndPersistentSegments)
{
	std::ifstream file("test/fixtures/replay/gameplay-inferno-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 26;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 6U);
	ASSERT_EQ(state.projectiles.size(), 3U);
	EXPECT_TRUE(std::all_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
		return projectile.behavior == 27;
	}));
	EXPECT_EQ(state.projectiles[0].remainingTicks, 15U);
	EXPECT_EQ(state.projectiles[1].remainingTicks, 18U);
	EXPECT_EQ(state.projectiles[2].remainingTicks, 20U);
	EXPECT_FALSE(ComputeReplayFixtureStateHash(state).empty());
}

TEST(ReplayFixture, ExecutesApocalypseRowMajorScanAndBoomLifecycle)
{
	std::ifstream file("test/fixtures/replay/gameplay-apocalypse-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 32;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetPetrified = fixture.initialState.combatTargetPetrified;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 3U);
	ASSERT_EQ(state.projectiles.size(), 1U);
	const auto &boom = state.projectiles.front();
	EXPECT_EQ(boom.behavior, 33);
	EXPECT_EQ(boom.positionX, 4);
	EXPECT_EQ(boom.positionY, 2);
	EXPECT_EQ(boom.remainingTicks, 13U);
	EXPECT_TRUE(boom.apocalypseHit);
	EXPECT_LT(state.combatTargetHitPoints, state.combatTargetMaxHitPoints);
	EXPECT_FALSE(ComputeReplayFixtureStateHash(state).empty());
}

TEST(ReplayFixture, ExecutesMagiFullManaRestorationAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-magi-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 38;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.mana, state.manaMaximum);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesManaSeededClassScaledRestorationAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-mana-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = static_cast<uint32_t>(fixture.initialState.characterClass);
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 37;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.mana, 3208);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesReflectChargeCreationAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-reflect-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.characterLevel = fixture.initialState.characterLevel;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 39;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.reflectCharges, 6U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesReflectRetaliationAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-reflect-retaliation.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.reflectCharges, 0U);
	EXPECT_EQ(state.life, 12);
	EXPECT_EQ(state.combatTargetHitPoints, 18);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesEtherealizeStatusAndDamageImmunityAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-etherealize-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.spellProjectileBehavior = 49;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	ASSERT_EQ(state.statusEffects.size(), 1U);
	EXPECT_EQ(state.statusEffects.front().effectId, 53U);
	EXPECT_EQ(state.statusEffects.front().remainingTicks, std::numeric_limits<uint32_t>::max());
	EXPECT_EQ(state.life, fixture.initialState.life);
	EXPECT_EQ(state.combatTargetHitPoints, fixture.initialState.combatTargetHitPoints);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesBerserkMutationAndRecastRejectionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-berserk-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellProjectileBehavior = 44;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetMinDamageSpecial = fixture.initialState.combatTargetMinDamageSpecial;
	state.combatTargetMaxDamageSpecial = fixture.initialState.combatTargetMaxDamageSpecial;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_FALSE(result.transitions[1].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_TRUE(state.combatTargetBerserked);
	EXPECT_EQ(state.combatTargetMinDamage, 14);
	EXPECT_EQ(state.combatTargetMaxDamage, 26);
	EXPECT_EQ(state.combatTargetMinDamageSpecial, 39);
	EXPECT_EQ(state.combatTargetMaxDamageSpecial, 52);
	EXPECT_EQ(state.combatTargetAttackDamage, 26);
	EXPECT_EQ(state.mana, 2440);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesBoneSpiritPvpAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-bone-spirit-pvp.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 21;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_EQ(state.life, 896);
	EXPECT_EQ(state.mana, 528);
	EXPECT_EQ(state.secondaryPlayerLife, 1667);
	EXPECT_EQ(state.lastAppliedTick, 10U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesBoneSpiritHomingAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-bone-spirit-homing.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMagicResistance = fixture.initialState.combatTargetMagicResistance;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetPetrified = fixture.initialState.combatTargetPetrified;
	state.spellProjectileBehavior = 21;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_EQ(state.life, 896);
	EXPECT_EQ(state.mana, 464);
	EXPECT_EQ(state.combatTargetHitPoints, 38);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 2416);
	EXPECT_EQ(state.lastAppliedTick, 10U);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].lastCollisionTargetEntityId, 9U);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 5U);
	EXPECT_EQ(state.projectiles[0].behavior, 21);
	EXPECT_EQ(state.projectiles[0].phase, 1);
	EXPECT_EQ(state.projectiles[0].resistantDamageDivisor, 4);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHellfireFireballBlockAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-fireball-hellfire-block.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 16;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_EQ(state.secondaryPlayerLife, 1987);
	EXPECT_EQ(state.lastAppliedTick, 17U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHellfireFireboltBlockAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-firebolt-hellfire-block.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 2;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_EQ(state.secondaryPlayerLife, fixture.initialState.secondaryPlayer.life);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 6U);
	EXPECT_EQ(state.lastAppliedTick, 5U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, CreatesPvpEarAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-pvp-ear-death.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.characterClassId = fixture.initialState.characterClass;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.spellProjectileBehavior = 1;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_EQ(state.characterName, "Caster");
	EXPECT_EQ(state.secondaryPlayerCharacterName, "Dead-RogueDM");
	EXPECT_EQ(state.secondaryPlayerLife, 0);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 8U);
	ASSERT_EQ(state.worldItems.size(), 1U);
	const ReplayFixtureWorldItemState &ear = state.worldItems.front();
	EXPECT_EQ(ear.itemSeed, 1633955154U);
	EXPECT_EQ(ear.price, 0U);
	EXPECT_EQ(ear.state.createInfo, 17509U);
	EXPECT_EQ(ear.state.itemType, 0);
	EXPECT_EQ(ear.state.equipLocation, 7);
	EXPECT_EQ(ear.state.itemClass, 3);
	EXPECT_EQ(ear.state.value, 3);
	EXPECT_EQ(ear.state.miscId, 44);
	EXPECT_EQ(ear.state.itemIndex, 23);
	EXPECT_EQ(ear.state.baseName, "Ear of Dead-RogueDM");
	EXPECT_EQ(ear.state.identifiedName, "Dead-RogueDM");
	EXPECT_TRUE(ear.state.hasNativeCursorGraphic);
	EXPECT_EQ(ear.state.nativeCursorGraphic, 21);
	EXPECT_EQ(state.lastAppliedTick, 5U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHellfireChargedBoltBlockAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-charged-bolt-hellfire-block.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_LT(state.secondaryPlayerLife, fixture.initialState.secondaryPlayer.life);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 7U);
	EXPECT_TRUE(state.hellfire);
	EXPECT_EQ(state.lastAppliedTick, 5U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesChargedBoltObjectCollisionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-charged-bolt-object-collision.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.objectMissilePassable = fixture.initialState.objectMissilePassable;
	state.objectBreakable = fixture.initialState.objectBreakable;
	state.objectDestroyed = fixture.initialState.objectDestroyed;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.objectDestroyed);
	EXPECT_EQ(state.projectiles.size(), 3U);
	EXPECT_EQ(state.randomState, 0x7162D42BU);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesChargedBoltPetrifiedMovingOccupancyAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-charged-bolt-moving-occupancy.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_TRUE(fixture.initialState.combatTargetMoving);
	EXPECT_TRUE(fixture.initialState.combatTargetPetrified);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.characterLevel = fixture.initialState.characterLevel;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.attributes[2] = fixture.initialState.magic;
	state.attributes[3] = fixture.initialState.magic;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_TRUE(state.combatTargetPetrified);
	EXPECT_EQ(state.combatTargetFuturePositionX, 1);
	EXPECT_EQ(state.combatTargetFuturePositionY, 1);
	EXPECT_LT(state.combatTargetHitPoints, fixture.initialState.combatTargetHitPoints);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, RejectsChargedBoltNonAdjacentMovingOccupancy)
{
	std::ifstream file("test/fixtures/replay/gameplay-charged-bolt-moving-occupancy.json");
	ASSERT_TRUE(file.good());
	std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	const std::string validFuturePosition = R"("combat_target_future_position_x": 1)";
	const size_t position = json.find(validFuturePosition);
	ASSERT_NE(position, std::string::npos);
	json.replace(position, validFuturePosition.size(), R"("combat_target_future_position_x": 4)");

	ReplayFixture fixture;
	std::string error;
	EXPECT_FALSE(ParseReplayFixture(json, fixture, error));
	EXPECT_EQ(error, "Replay fixture contains invalid combat-target occupancy state.");
}

TEST(ReplayFixture, ExecutesPortalLevelTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-portal-transition.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.portalId = 5;
	state.portalDestinationLevelId = 2;
	state.portalDestinationX = 3;
	state.portalDestinationY = 4;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.levelId, 2U);
	EXPECT_EQ(state.positionX, 3);
	EXPECT_EQ(state.positionY, 4);
}

TEST(ReplayFixture, RetainsEntitiesOnTheirOriginalLevelAcrossPortalTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-multi-level-occupancy.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = 2;
	state.worldItemPositionY = 0;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.portalId = 5;
	state.portalDestinationLevelId = 2;
	state.portalDestinationX = 3;
	state.portalDestinationY = 4;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.levelId, 2U);
	EXPECT_EQ(state.worldItemEntityId, 20U);
	EXPECT_EQ(state.worldItemLevelId, 1U);
	EXPECT_EQ(state.objectEntityId, 30U);
	EXPECT_EQ(state.objectLevelId, 1U);
}

TEST(ReplayFixture, ExecutesWorldItemPickupTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-world-item-pickup.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.inventoryGrid.assign(40, -1);
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.positionX + 1;
	state.worldItemPositionY = fixture.initialState.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.worldItemEntityId == 0);
	ASSERT_EQ(state.inventory.size(), 1U);
	EXPECT_EQ(state.inventory[0].itemSeed, fixture.initialState.worldItemSeed);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
}

TEST(ReplayFixture, ExecutesStableInventoryItemDropTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-stable-item-drop.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.gold = fixture.initialState.gold;
	state.inventoryGrid.assign(40, -1);

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.inventory.empty());
	EXPECT_FALSE(state.heldItemDefined);
	EXPECT_EQ(state.worldItemEntityId, 0x40000000U);
	EXPECT_EQ(state.worldItemSeed, fixture.initialState.goldStackSeed);
	EXPECT_EQ(state.worldItemState.stableItemId, fixture.initialState.goldStackStableItemId);
	EXPECT_EQ(state.worldItemPositionX, 1);
	EXPECT_EQ(state.worldItemPositionY, 0);
}

TEST(ReplayFixture, RejectsStaleStableInventoryItemDropTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-stable-item-drop-retry.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.gold = fixture.initialState.gold;
	state.inventoryGrid.assign(40, -1);

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_FALSE(result.transitions[1].accepted);
	EXPECT_TRUE(state.inventory.empty());
	EXPECT_EQ(state.worldItemSeed, fixture.initialState.goldStackSeed);
	EXPECT_EQ(state.worldItemState.stableItemId, fixture.initialState.goldStackStableItemId);
}

TEST(ReplayFixture, ExecutesStableBeltItemDropTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-stable-belt-drop.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.belt.empty());
	EXPECT_EQ(state.worldItemSeed, 321U);
	EXPECT_EQ(state.worldItemState.stableItemId, 7002U);
}

TEST(ReplayFixture, RejectsStableBeltItemDropAtNonAdjacentDestination)
{
	std::ifstream file("test/fixtures/replay/gameplay-stable-belt-drop-invalid-target.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_FALSE(result.transitions[0].accepted);
	EXPECT_TRUE(state.worldItemEntityId == 0);
	ASSERT_EQ(state.belt.size(), 1U);
	EXPECT_EQ(state.belt[0].state.stableItemId, 7005U);
}

TEST(ReplayFixture, ExecutesStableEquipmentItemDropTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-stable-equipment-drop.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.equipment.empty());
	EXPECT_EQ(state.worldItemSeed, 654U);
	EXPECT_EQ(state.worldItemState.stableItemId, 7003U);
}

TEST(ReplayFixture, ExecutesGoldPickupOverflowAndHeldItemDrop)
{
	std::ifstream file("test/fixtures/replay/gameplay-gold-pickup-overflow.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.gold = fixture.initialState.gold;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.positionX + 1;
	state.worldItemPositionY = fixture.initialState.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 11;
	state.worldItemState.itemIndex = 0;
	state.worldItemState.value = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.identifiedValue = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
	EXPECT_EQ(state.gold, 5000U);
	ASSERT_EQ(state.inventory.size(), 40U);
	EXPECT_EQ(state.inventory[0].price, 5000U);
	EXPECT_FALSE(state.heldItemDefined);
	EXPECT_EQ(state.worldItemEntityId, 0x40000000U);
	EXPECT_EQ(state.worldItemSeed, 42U);
	EXPECT_EQ(state.worldItemPrice, 65U);
	EXPECT_EQ(state.randomState, 0U);
}

TEST(ReplayFixture, GoldPickupCreditsBalanceAndRejectsMalformedStacks)
{
	std::ifstream file("test/fixtures/replay/gameplay-world-item-pickup.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	fixture.contentManifest = { };

	auto CreateState = [&fixture]() {
		ReplayFixtureExecutionState state;
		state.life = fixture.initialState.life;
		state.lifeMaximum = fixture.initialState.life;
		state.levelId = fixture.initialState.levelId;
		state.positionX = fixture.initialState.positionX;
		state.positionY = fixture.initialState.positionY;
		state.gold = 100;
		state.worldItemEntityId = fixture.initialState.worldItemEntityId;
		state.worldItemLevelId = fixture.initialState.levelId;
		state.worldItemPositionX = fixture.initialState.positionX + 1;
		state.worldItemPositionY = fixture.initialState.positionY;
		state.worldItemSeed = fixture.initialState.worldItemSeed;
		state.worldItemPrice = fixture.initialState.worldItemPrice;
		state.worldItemState.itemType = 11;
		state.worldItemState.itemIndex = 0;
		state.worldItemState.value = 75;
		state.worldItemState.identifiedValue = 75;
		state.worldItemState.statFlag = true;
		return state;
	};

	auto state = CreateState();
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.gold, 175U);
	EXPECT_EQ(state.worldItemEntityId, 0U);
	EXPECT_TRUE(state.inventory.empty());

	auto malformed = CreateState();
	malformed.worldItemState.identifiedValue = 74;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, malformed, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_FALSE(result.transitions[0].accepted);
	EXPECT_EQ(malformed.gold, 100U);
	EXPECT_EQ(malformed.worldItemEntityId, fixture.initialState.worldItemEntityId);
}

TEST(ReplayFixture, RejectsPrimaryHeldItemPickupOnOccupiedTile)
{
	std::ifstream file("test/fixtures/replay/gameplay-primary-held-item-pickup-rejection.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.positionX;
	state.worldItemPositionY = fixture.initialState.positionY + 1;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 1;
	state.worldItemState.itemIndex = 1;
	state.worldItemState.stableItemId = 9920;
	state.worldItemState.statFlag = true;
	state.heldItemDefined = true;
	state.heldItem.itemSeed = 9010;
	state.heldItem.price = 25;
	state.heldItem.state.itemType = 1;
	state.heldItem.state.itemIndex = 1;
	state.heldItem.state.stableItemId = 9921;
	state.heldItem.state.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_FALSE(result.transitions[1].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
	EXPECT_TRUE(state.heldItemDefined);
	EXPECT_EQ(state.heldItem.itemSeed, 9010U);
	EXPECT_EQ(state.worldItemEntityId, fixture.initialState.worldItemEntityId);
}

TEST(ReplayFixture, ExecutesPrimaryHeldItemPickupReplacement)
{
	std::ifstream file("test/fixtures/replay/gameplay-primary-held-item-pickup-replacement.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.inventoryGrid.assign(40, -1);
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.positionX;
	state.worldItemPositionY = fixture.initialState.positionY + 1;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 1;
	state.worldItemState.itemIndex = 1;
	state.worldItemState.stableItemId = 9920;
	state.worldItemState.statFlag = true;
	state.heldItemDefined = true;
	state.heldItem.itemSeed = 9010;
	state.heldItem.price = 25;
	state.heldItem.state.itemType = 1;
	state.heldItem.state.itemIndex = 1;
	state.heldItem.state.stableItemId = 9921;
	state.heldItem.state.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_FALSE(state.heldItemDefined);
	EXPECT_TRUE(state.inventory.size() == 1U);
	EXPECT_EQ(state.inventory[0].itemSeed, fixture.initialState.worldItemSeed);
	EXPECT_EQ(state.inventory[0].state.stableItemId, 9920U);
	ASSERT_EQ(state.worldItems.size(), 1U);
	EXPECT_EQ(state.worldItems[0].entityId, 0x40000000U);
	EXPECT_EQ(state.worldItems[0].itemSeed, 9010U);
	EXPECT_EQ(state.worldItems[0].state.stableItemId, 9921U);
	EXPECT_EQ(state.worldItems[0].positionX, fixture.initialState.positionX);
	EXPECT_EQ(state.worldItems[0].positionY, fixture.initialState.positionY);
}

TEST(ReplayFixture, ExecutesObjectQuestTransitions)
{
	std::ifstream file("test/fixtures/replay/gameplay-object-quest.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.questId = fixture.initialState.questId;
	state.questLevelId = fixture.initialState.levelId;
	state.questRequiredProgress = fixture.initialState.questRequiredProgress;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_TRUE(state.objectActivated);
	EXPECT_EQ(state.questProgress, 1U);
	EXPECT_TRUE(state.questCompleted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
}

TEST(ReplayFixture, ExecutesQuestPoolSelectionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-quest-selection.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.questSelection.defined);
	ASSERT_EQ(fixture.questSelection.pools.size(), 2U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.levelId = fixture.initialState.levelId;
	state.questId = fixture.initialState.questId;
	state.questLevelId = fixture.initialState.levelId;
	state.questRequiredProgress = fixture.initialState.questRequiredProgress;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_FALSE(result.transitions[0].accepted);
	EXPECT_FALSE(result.transitions[1].accepted);
	EXPECT_TRUE(state.questUnavailable);
	EXPECT_EQ(state.questProgress, 0U);
	EXPECT_FALSE(state.questCompleted);
	EXPECT_EQ(state.randomState, 1U);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
}

TEST(ReplayFixture, ExecutesAutonomousRegenerationSoakAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-autonomous-regen-soak.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1000U);
	EXPECT_EQ(state.combatTargetHitPoints, 20);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 1280);
	EXPECT_EQ(state.randomState, static_cast<uint32_t>(fixture.rngSeed));
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGargoyleHealingAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-gargoyle-healing.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetGargoyleSpecialActive = fixture.initialState.combatTargetGargoyleSpecialActive;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.combatTargetHitPoints, 21);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 1344);
	EXPECT_TRUE(state.combatTargetGargoyleSpecialActive);
	EXPECT_EQ(state.randomState, 45390955U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), result.transitions[0].stateSha256);
}

TEST(ReplayFixture, ExecutesGargoyleAttackAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-gargoyle-attack.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetGargoyleSpecialActive = fixture.initialState.combatTargetGargoyleSpecialActive;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 6);
	EXPECT_EQ(state.secondaryPlayerCombatModeEndTick, 5U);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(state.randomState, 2667323828U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoatMeleeAttackAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-goat-melee-attack.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 6);
	EXPECT_EQ(state.secondaryPlayerCombatModeEndTick, 5U);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(state.randomState, 2667323828U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGargoyleAvoidanceIdleAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-gargoyle-avoidance-idle.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetGargoyleSpecialActive = fixture.initialState.combatTargetGargoyleSpecialActive;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.combatTargetPositionX, 2);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.combatTargetAvoidanceMovementIdleTicks, 0);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(state.randomState, 1443805422U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGargoyleAvoidanceWalkAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-gargoyle-avoidance-walk.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetGargoyleSpecialActive = fixture.initialState.combatTargetGargoyleSpecialActive;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.objectMissilePassable = fixture.initialState.objectMissilePassable;
	state.objectBreakable = fixture.initialState.objectBreakable;
	state.objectDestroyed = fixture.initialState.objectDestroyed;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetPositionX, 0);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.combatTargetFuturePositionX, 1);
	EXPECT_EQ(state.combatTargetFuturePositionY, 1);
	EXPECT_EQ(state.combatTargetDirection, 0);
	EXPECT_EQ(state.combatTargetAvoidanceMovementIdleTicks, 1);
	EXPECT_EQ(state.combatTargetAvoidanceMovementMode, 1);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 90781909U);
	EXPECT_EQ(state.randomState, 274169216U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGargoyleAvoidanceMoveAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-gargoyle-avoidance-move.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetGargoyleSpecialActive = fixture.initialState.combatTargetGargoyleSpecialActive;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetPositionX, 0);
	EXPECT_EQ(state.combatTargetPositionY, 1);
	EXPECT_EQ(state.combatTargetFuturePositionX, 0);
	EXPECT_EQ(state.combatTargetFuturePositionY, 2);
	EXPECT_EQ(state.combatTargetDirection, 1);
	EXPECT_EQ(state.combatTargetAvoidanceMovementTicks, 2);
	EXPECT_EQ(state.combatTargetAvoidanceMovementTurn, 0);
	EXPECT_EQ(state.combatTargetAvoidanceMovementIdleTicks, 1);
	EXPECT_EQ(state.combatTargetAvoidanceMovementMode, 0);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 203440698U);
	// The per-monster stream is restored before each multiplayer AI pass;
	// the global stream therefore remains at the fourth tick-2 draw.
	EXPECT_EQ(state.randomState, 1084909516U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSkeletonMeleeDelayAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-skeleton-melee-delay.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetSkeletonMeleeDelayTicks = fixture.initialState.combatTargetSkeletonMeleeDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_FALSE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetPositionX, 4);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.combatTargetSkeletonMeleeDelayTicks, 14);
	EXPECT_EQ(state.randomState, 9U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSkeletonMeleeAttackDelayAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-skeleton-melee-attack-delay.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetSkeletonMeleeDelayTicks = fixture.initialState.combatTargetSkeletonMeleeDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.combatTargetSkeletonMeleeDelayTicks, 0);
	EXPECT_EQ(state.combatTargetPositionX, 1);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.randomState, 9U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesRhinoChargeAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-rhino-charge.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetToHitSpecial = fixture.initialState.combatTargetToHitSpecial;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 97);
	EXPECT_EQ(state.positionX, 6);
	EXPECT_EQ(state.positionY, 0);
	EXPECT_EQ(state.combatTargetPositionX, 4);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.randomState, 9U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSnakeDistantDelayAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-snake-distant-delay.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetSnakeDelayTicks = fixture.initialState.combatTargetSnakeDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.combatTargetSnakeDelayTicks, 15);
	EXPECT_EQ(state.combatTargetPositionX, 1);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.randomState, 9U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSnakeAdjacentDelayAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-snake-adjacent-delay.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetSnakeDelayTicks = fixture.initialState.combatTargetSnakeDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.combatTargetSnakeDelayTicks, 10);
	EXPECT_EQ(state.combatTargetPositionX, 1);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.randomState, 9U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSnakeChargeLifecycleAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-snake-charge-lifecycle.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 6144);
	EXPECT_EQ(state.combatMode, 7U);
	EXPECT_EQ(state.combatModeEndTick, 10U);
	EXPECT_EQ(state.combatTargetPositionX, 3);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles.front().behavior, 54);
	EXPECT_EQ(state.projectiles.front().phase, 1);
	EXPECT_EQ(state.projectiles.front().remainingTicks, 2U);
	EXPECT_EQ(state.randomState, 4261773604U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSneakConcealmentAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-sneak-conceal.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.combatTargetSneakHidden);
	EXPECT_EQ(state.combatTargetPositionX, 4);
	EXPECT_EQ(state.combatTargetPositionY, 10);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSneakRevealAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-sneak-reveal.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetSneakHidden = fixture.initialState.combatTargetSneakHidden;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_FALSE(state.combatTargetSneakHidden);
	EXPECT_EQ(state.combatTargetPositionX, 4);
	EXPECT_EQ(state.combatTargetPositionY, 10);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesBatRetreatAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-bat-retreat.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetBatRetreatTicks = fixture.initialState.combatTargetBatRetreatTicks;
	state.combatTargetBatRetreatDirection = fixture.initialState.combatTargetBatRetreatDirection;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.combatTargetPositionX, 9);
	EXPECT_EQ(state.combatTargetPositionY, 9);
	EXPECT_EQ(state.combatTargetBatRetreatTicks, 0);
	EXPECT_EQ(state.combatTargetBatRetreatDirection, 0);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesScavengerCorpseLifecycleAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-scavenger-corpse.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetScavengerHealingTicks = fixture.initialState.combatTargetScavengerHealingTicks;
	state.combatTargetScavengerCorpseEntityId = fixture.initialState.combatTargetScavengerCorpseEntityId;
	state.corpses = fixture.initialState.corpses;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.combatTargetHitPoints, 16);
	EXPECT_EQ(state.combatTargetPositionX, 5);
	EXPECT_EQ(state.combatTargetPositionY, 10);
	EXPECT_FALSE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetScavengerHealingTicks, 8);
	EXPECT_EQ(state.combatTargetScavengerCorpseEntityId, 30U);
	ASSERT_EQ(state.corpses.size(), 1U);
	EXPECT_EQ(state.corpses.front().entityId, 30U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesScavengerHellfireCorpseLifecycleAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-scavenger-hellfire-corpse.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.hellfire = fixture.gameVariant == "Hellfire";
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetScavengerHealingTicks = fixture.initialState.combatTargetScavengerHealingTicks;
	state.combatTargetScavengerCorpseEntityId = fixture.initialState.combatTargetScavengerCorpseEntityId;
	state.corpses = fixture.initialState.corpses;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 5U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_TRUE(result.transitions[2].accepted);
	EXPECT_TRUE(result.transitions[3].accepted);
	EXPECT_TRUE(result.transitions[4].accepted);
	EXPECT_EQ(state.combatTargetHitPoints, 64);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 4096);
	EXPECT_EQ(state.combatTargetScavengerHealingTicks, 0);
	EXPECT_EQ(state.combatTargetScavengerCorpseEntityId, 0U);
	EXPECT_TRUE(state.corpses.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesZombieActionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-zombie-action.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.combatTargetPositionX, 5);
	EXPECT_EQ(state.combatTargetPositionY, 10);
	EXPECT_FALSE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetDirection, 7);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesFatMovementAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-fat-movement.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetFatMovementIdleTicks = fixture.initialState.combatTargetFatMovementIdleTicks;
	state.combatTargetFatMovementMode = fixture.initialState.combatTargetFatMovementMode;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.combatTargetPositionX, 5);
	EXPECT_EQ(state.combatTargetPositionY, 10);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetFuturePositionX, 6);
	EXPECT_EQ(state.combatTargetFuturePositionY, 10);
	EXPECT_EQ(state.combatTargetDirection, 7);
	EXPECT_EQ(state.combatTargetFatMovementIdleTicks, 0);
	EXPECT_EQ(state.combatTargetFatMovementMode, 1);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoatMeleeMovementAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-goat-melee-movement.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.combatTargetPositionX, 8);
	EXPECT_EQ(state.combatTargetPositionY, 10);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetFuturePositionX, 9);
	EXPECT_EQ(state.combatTargetFuturePositionY, 10);
	EXPECT_EQ(state.combatTargetDirection, 7);
	EXPECT_EQ(state.combatTargetAvoidanceMovementIdleTicks, 0);
	EXPECT_EQ(state.combatTargetAvoidanceMovementMode, 1);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerAutonomousTickAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-autonomous-tick.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_EQ(fixture.commands[0].player, "B");
	EXPECT_TRUE(fixture.commands[1].player.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.lastAppliedTick, 10U);
	EXPECT_EQ(state.positionX, 0);
	EXPECT_EQ(state.positionY, 1);
	EXPECT_EQ(state.life, 73);
	EXPECT_EQ(state.secondaryPlayerLife, 80);
	EXPECT_EQ(state.secondaryPlayerPositionX, 6);
	EXPECT_EQ(state.secondaryPlayerPositionY, 6);
	EXPECT_EQ(state.combatTargetPositionX, 1);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryAttackAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-attack.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_EQ(fixture.commands[0].player, "B");

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.combatTargetHitPoints, 10);
	EXPECT_EQ(state.combatTargetHitPointsQ6, 640);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryItemDropAgainstCSharpProjection)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-item-drop.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.inventoryItemDefined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.secondaryInventory.empty());
	EXPECT_EQ(state.worldItemSeed, 9001U);
	EXPECT_EQ(state.worldItemState.stableItemId, 9901U);
	EXPECT_EQ(std::pair(state.worldItemPositionX, state.worldItemPositionY), std::pair(4, 5));
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryItemPickupAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-item-pickup.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_EQ(fixture.commands[0].player, "B");

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX - 1;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 1;
	state.worldItemState.itemIndex = 1;
	state.worldItemState.stableItemId = 9910;
	state.worldItemState.value = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.identifiedValue = state.worldItemState.value;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_FALSE(result.transitions[1].accepted);
	ASSERT_EQ(state.secondaryInventory.size(), 1U);
	EXPECT_EQ(state.secondaryInventory[0].itemSeed, fixture.initialState.worldItemSeed);
	EXPECT_EQ(state.secondaryInventory[0].purchasedAtTick, 1U);
	EXPECT_EQ(state.secondaryInventoryGrid[0], 0);
	EXPECT_EQ(state.worldItemEntityId, 0U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryItemPickupWithExistingItemAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-item-pickup-with-existing-item.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.inventoryItemDefined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX - 1;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 1;
	state.worldItemState.itemIndex = 1;
	state.worldItemState.stableItemId = 9911;
	state.worldItemState.value = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.identifiedValue = state.worldItemState.value;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	ASSERT_EQ(state.secondaryInventory.size(), 2U);
	EXPECT_EQ(state.secondaryInventory[0].itemSeed, 9003U);
	EXPECT_EQ(state.secondaryInventory[1].itemSeed, fixture.initialState.worldItemSeed);
	EXPECT_EQ(state.secondaryInventory[1].purchasedAtTick, 1U);
	EXPECT_EQ(state.secondaryInventoryGrid[0], 0);
	EXPECT_EQ(state.secondaryInventoryGrid[1], 1);
	EXPECT_EQ(state.worldItemEntityId, 0U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryItemPickupOverflowAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-item-pickup-overflow.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	EXPECT_EQ(fixture.initialState.secondaryPlayer.inventoryFillerCount, 40U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX - 1;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 1;
	state.worldItemState.itemIndex = 1;
	state.worldItemState.stableItemId = 9912;
	state.worldItemState.value = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.identifiedValue = state.worldItemState.value;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.secondaryInventory.size(), 40U);
	EXPECT_FALSE(state.secondaryHeldItemDefined);
	EXPECT_EQ(state.secondaryInventoryGrid[39], 39);
	EXPECT_EQ(state.worldItemEntityId, 0x40000000U);
	EXPECT_EQ(state.worldItemPositionX, 5);
	EXPECT_EQ(state.worldItemPositionY, 6);
	EXPECT_EQ(state.worldItemSeed, fixture.initialState.worldItemSeed);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryHeldItemPickupReplacement)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-held-item-pickup-replacement.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX - 1;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 1;
	state.worldItemState.itemIndex = 1;
	state.worldItemState.stableItemId = 9920;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_FALSE(state.secondaryHeldItemDefined);
	ASSERT_EQ(state.secondaryInventory.size(), 1U);
	EXPECT_EQ(state.secondaryInventory[0].itemSeed, fixture.initialState.worldItemSeed);
	EXPECT_EQ(state.secondaryInventory[0].state.stableItemId, 9920U);
	ASSERT_EQ(state.worldItems.size(), 1U);
	EXPECT_EQ(state.worldItems[0].entityId, 0x40000000U);
	EXPECT_EQ(state.worldItems[0].itemSeed, 9010U);
	EXPECT_EQ(state.worldItems[0].state.stableItemId, 9921U);
	EXPECT_EQ(state.worldItems[0].positionX, fixture.initialState.secondaryPlayer.positionX);
	EXPECT_EQ(state.worldItems[0].positionY, fixture.initialState.secondaryPlayer.positionY);
}

TEST(ReplayFixture, RejectsMultiplayerSecondaryHeldItemPickupOnOccupiedTile)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-held-item-pickup-rejection.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY + 1;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 1;
	state.worldItemState.itemIndex = 1;
	state.worldItemState.stableItemId = 9920;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_FALSE(result.transitions[1].accepted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
	EXPECT_TRUE(state.secondaryHeldItemDefined);
	EXPECT_EQ(state.secondaryHeldItem.itemSeed, 9010U);
	EXPECT_EQ(state.secondaryHeldItem.state.stableItemId, 9921U);
	EXPECT_EQ(state.worldItemEntityId, fixture.initialState.worldItemEntityId);
	EXPECT_TRUE(state.worldItems.empty());
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryGoldPickupAgainstCSharpCheckpoint)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-gold-pickup.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX - 1;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 11;
	state.worldItemState.itemIndex = 0;
	state.worldItemState.stableItemId = 9913;
	state.worldItemState.value = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.identifiedValue = state.worldItemState.value;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(
	    state.secondaryPlayerGold,
	    fixture.initialState.secondaryPlayer.gold + fixture.initialState.worldItemPrice);
	EXPECT_TRUE(state.secondaryInventory.empty());
	EXPECT_FALSE(state.secondaryHeldItemDefined);
	EXPECT_EQ(state.worldItemEntityId, 0U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, RejectsMalformedMultiplayerSecondaryGoldPickup)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-gold-pickup-malformed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX - 1;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 11;
	state.worldItemState.itemIndex = 0;
	state.worldItemState.stableItemId = 9914;
	state.worldItemState.value = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.identifiedValue = state.worldItemState.value - 1;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_FALSE(result.transitions[0].accepted);
	EXPECT_EQ(state.secondaryPlayerGold, 0U);
	EXPECT_TRUE(state.secondaryInventory.empty());
	EXPECT_FALSE(state.secondaryHeldItemDefined);
	EXPECT_EQ(state.worldItemEntityId, fixture.initialState.worldItemEntityId);
}

TEST(ReplayFixture, RejectsMultiplayerSecondaryGoldPickupOnWalletOverflow)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-gold-pickup-overflow.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.secondaryPlayer.positionX - 1;
	state.worldItemPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.worldItemState.itemType = 11;
	state.worldItemState.itemIndex = 0;
	state.worldItemState.stableItemId = 9915;
	state.worldItemState.value = static_cast<int32_t>(fixture.initialState.worldItemPrice);
	state.worldItemState.identifiedValue = state.worldItemState.value;
	state.worldItemState.statFlag = true;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_FALSE(result.transitions[0].accepted);
	EXPECT_EQ(state.secondaryPlayerGold, fixture.initialState.secondaryPlayer.gold);
	EXPECT_TRUE(state.secondaryInventory.empty());
	EXPECT_FALSE(state.secondaryHeldItemDefined);
	EXPECT_EQ(state.worldItemEntityId, fixture.initialState.worldItemEntityId);
}

TEST(ReplayFixture, ExecutesMonsterArrowResolutionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-resolution.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 3U);
	EXPECT_EQ(state.life, 96);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerMonsterArrowAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-monster-arrow.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_EQ(fixture.initialState.secondaryPlayer.entityId, 8U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 3U);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.secondaryPlayerLife, 76);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryPortalAgainstCSharpProjection)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-portal.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_EQ(fixture.commands[0].player, "B");

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.secondaryPlayerLevelId = fixture.initialState.secondaryPlayer.levelId;
	state.portalId = 5;
	state.portalDestinationLevelId = 2;
	state.portalDestinationX = 3;
	state.portalDestinationY = 4;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.levelId, 1U);
	EXPECT_EQ(std::pair(state.positionX, state.positionY), std::pair(0, 0));
	EXPECT_EQ(state.secondaryPlayerLevelId, 2U);
	ASSERT_EQ(state.secondaryPlayerVisitedLevelIds.size(), 2U);
	EXPECT_EQ(state.secondaryPlayerVisitedLevelIds[0], 1U);
	EXPECT_EQ(state.secondaryPlayerVisitedLevelIds[1], 2U);
	EXPECT_EQ(std::pair(state.secondaryPlayerPositionX, state.secondaryPlayerPositionY), std::pair(3, 4));
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints.back().stateSha256);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryMonsterDeathDropsAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-monster-death-drops.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_EQ(fixture.initialState.secondaryPlayer.equipment.size(), 1U);
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 3U);
	EXPECT_EQ(state.secondaryPlayerLife, 0);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 8U);
	EXPECT_EQ(state.secondaryPlayerGold, 50U);
	EXPECT_TRUE(state.secondaryEquipment.empty());
	ASSERT_EQ(state.worldItems.size(), 2U);
	EXPECT_EQ(state.worldItems[0].price, 50U);
	EXPECT_EQ(state.worldItems[1].itemSeed, 101U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryMonsterMeleeAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-monster-melee.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 97);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 0U);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryMonsterNativeAiSeedAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-monster-native-ai-seed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 6U);
	EXPECT_EQ(state.secondaryPlayerCombatModeEndTick, 5U);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryMonsterNativeAiSeedImpactAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-monster-native-ai-seed-impact.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 97);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 0U);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryMonsterPathingAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-monster-pathing.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_EQ(state.combatTargetPositionX, 3);
	EXPECT_EQ(state.combatTargetPositionY, 3);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetFuturePositionX, 2);
	EXPECT_EQ(state.combatTargetFuturePositionY, 2);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerSecondaryMonsterObjectPathingAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-secondary-monster-object-pathing.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.objectMissilePassable = fixture.initialState.objectMissilePassable;
	state.objectBreakable = fixture.initialState.objectBreakable;
	state.objectDestroyed = fixture.initialState.objectDestroyed;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_EQ(state.combatTargetPositionX, 3);
	EXPECT_EQ(state.combatTargetPositionY, 3);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetFuturePositionX, 2);
	EXPECT_EQ(state.combatTargetFuturePositionY, 2);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMultiplayerMonsterArrowVolleyAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-multiplayer-monster-arrow-volley.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_EQ(fixture.initialState.secondaryPlayer.entityId, 8U);
	ASSERT_EQ(fixture.initialProjectiles.size(), 2U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 3U);
	EXPECT_EQ(state.life, 96);
	EXPECT_EQ(state.secondaryPlayerLife, 76);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesAutonomousMonsterArrowAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-autonomous.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.secondaryPlayerLife, 76);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSkeletonRangedMonsterArrowAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-skeleton-ranged.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 13U);
	EXPECT_EQ(state.life, 96);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonHorkSpawnAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-autonomous.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetHorkDemonDelayTicks = fixture.initialState.combatTargetHorkDemonDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 57);
	EXPECT_EQ(state.projectiles[0].spellId, 117U);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 8U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonMultiplayerTargetSelectionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-multiplayer-target.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 57);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 8U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonDelayAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-delay.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetHorkDemonDelayTicks = fixture.initialState.combatTargetHorkDemonDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.combatTargetHorkDemonDelayTicks, 1);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonHorkSpawnPlayerImpactAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-impact.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 6336);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonHorkSpawnMissAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-miss.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.life, 6400);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonHorkSpawnSecondaryBlockAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-secondary-block.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 6400);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 6U);
	EXPECT_EQ(state.secondaryPlayerCombatModeEndTick, 5U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonHorkSpawnSecondaryImpactAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-secondary-impact.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 6336);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 7U);
	EXPECT_EQ(state.secondaryPlayerCombatModeEndTick, 7U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonHorkSpawnObjectBreakAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-object-break.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.objectMissilePassable = fixture.initialState.objectMissilePassable;
	state.objectBreakable = fixture.initialState.objectBreakable;
	state.objectDestroyed = fixture.initialState.objectDestroyed;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_TRUE(state.objectDestroyed);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesHorkDemonHorkSpawnPrimaryBlockAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-hork-spawn-primary-block.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_EQ(fixture.initialProjectiles.size(), 1U);

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 6400);
	EXPECT_EQ(state.combatMode, 6U);
	EXPECT_EQ(state.combatModeEndTick, 5U);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSuccubusBloodStarAdmissionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-blood-star-autonomous.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 20);
	EXPECT_EQ(state.projectiles[0].spellId, 36U);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(state.projectiles[0].originalDirection, 3);
	EXPECT_EQ(state.projectiles[0].velocityX, -741440);
	EXPECT_EQ(state.projectiles[0].velocityY, -370720);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesLazarusSuccubusBloodStarAdmissionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-blood-star-lazarus-succubus.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 20);
	EXPECT_EQ(state.projectiles[0].spellId, 36U);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(state.projectiles[0].originalDirection, 3);
	EXPECT_EQ(state.projectiles[0].velocityX, -741440);
	EXPECT_EQ(state.projectiles[0].velocityY, -370720);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSuccubusBloodStarMultiplayerTargetSelectionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-blood-star-multiplayer-target.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 20);
	EXPECT_EQ(state.projectiles[0].spellId, 36U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSuccubusBloodStarImpactAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-blood-star-impact.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 27U);
	EXPECT_EQ(state.life, 6144);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 20);
	EXPECT_EQ(state.projectiles[0].phase, 1);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 1U);
	EXPECT_EQ(state.projectiles[0].positionX, 0);
	EXPECT_EQ(state.projectiles[0].positionY, 0);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSuccubusBloodStarNativeAiSeedAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-blood-star-native-ai-seed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 20);
	EXPECT_EQ(state.projectiles[0].spellId, 36U);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(state.combatTargetRangedDelayTicks, 10);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSuccubusBloodStarCloseNativeAiSeedAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-blood-star-close-native-ai-seed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetPositionX, 4);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.combatTargetFuturePositionX, 5);
	EXPECT_EQ(state.combatTargetFuturePositionY, 0);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSuccubusBloodStarSecondaryBlockAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-blood-star-secondary-block.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 27U);
	EXPECT_EQ(state.life, 0);
	EXPECT_EQ(state.secondaryPlayerLife, 6400);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 6U);
	EXPECT_EQ(state.secondaryPlayerCombatModeEndTick, 31U);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 20);
	EXPECT_EQ(state.projectiles[0].phase, 1);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 1U);
	EXPECT_EQ(state.projectiles[0].lastCollisionTargetEntityId, 0U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMegaInfernoAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-mega-inferno.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetMegaDelayTicks = fixture.initialState.combatTargetMegaDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	ASSERT_EQ(state.projectiles.size(), 2U);
	EXPECT_EQ(state.projectiles[0].behavior, 26);
	EXPECT_EQ(state.projectiles[0].sourceEntityId, 9U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, 1U);
	EXPECT_EQ(state.projectiles[0].spellId, 21U);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(state.projectiles[0].pathIndex, 1);
	EXPECT_EQ(state.projectiles[1].behavior, 27);
	EXPECT_EQ(state.projectiles[1].sourceEntityId, 9U);
	EXPECT_EQ(state.projectiles[1].damage, 4);
	EXPECT_EQ(state.projectiles[1].remainingTicks, 39U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesMegaInfernoNativeAiSeedAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-mega-inferno-native-ai-seed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.entityId = 1;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetMegaDelayTicks = fixture.initialState.combatTargetMegaDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	ASSERT_EQ(state.projectiles.size(), 2U);
	EXPECT_EQ(state.projectiles[0].behavior, 26);
	EXPECT_EQ(state.projectiles[0].targetEntityId, 1U);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(state.projectiles[0].pathIndex, 1);
	EXPECT_EQ(state.projectiles[1].behavior, 27);
	EXPECT_EQ(state.projectiles[1].damage, 4);
	EXPECT_EQ(state.projectiles[1].remainingTicks, 39U);
	EXPECT_NE(state.combatTargetMonsterAiSeed, fixture.initialState.combatTargetMonsterAiSeed);
	EXPECT_EQ(state.combatTargetMegaDelayTicks, 15);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesLichMonsterMagicMultiplayerTargetSelectionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-multiplayer-target.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 6400);
	EXPECT_EQ(state.secondaryPlayerLife, 6400);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSkeletonRangedMonsterArrowMultiplayerTargetSelectionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-multiplayer-target.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 59);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 6U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoatRangedMonsterArrowMultiplayerTargetSelectionAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-goat-ranged-multiplayer-target.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 100);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 59);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 6U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoatRangedCloseRetreatAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-goat-ranged-close-retreat.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetPositionX, 4);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.combatTargetFuturePositionX, 5);
	EXPECT_EQ(state.combatTargetFuturePositionY, 0);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoatRangedDelayAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-goat-ranged-delay.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.combatTargetRangedDelayTicks = fixture.initialState.combatTargetRangedDelayTicks;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 2U);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 59);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 6U);
	EXPECT_EQ(state.combatTargetRangedDelayTicks, 0);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSkeletonRangedCloseRetreatAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-skeleton-ranged-close-retreat.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetPositionX, 3);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.combatTargetFuturePositionX, 4);
	EXPECT_EQ(state.combatTargetFuturePositionY, 0);
	EXPECT_EQ(state.combatTargetSkeletonRangedMovementIdleTicks, 0);
	EXPECT_EQ(state.combatTargetSkeletonRangedMovementMode, 1);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesSkeletonRangedNativeAiSeedAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-skeleton-ranged-native-ai-seed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 59);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 6U);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoatRangedNativeAiSeedAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-goat-ranged-native-ai-seed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.secondaryPlayerLife, 100);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].behavior, 59);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 6U);
	EXPECT_EQ(state.combatTargetRangedDelayTicks, 10);
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesGoatRangedCloseNativeAiSeedAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-arrow-goat-ranged-close-native-ai-seed.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
	state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_TRUE(state.combatTargetMoving);
	EXPECT_EQ(state.combatTargetPositionX, 4);
	EXPECT_EQ(state.combatTargetPositionY, 0);
	EXPECT_EQ(state.combatTargetFuturePositionX, 5);
	EXPECT_EQ(state.combatTargetFuturePositionY, 0);
	EXPECT_TRUE(state.projectiles.empty());
	EXPECT_EQ(state.combatTargetMonsterAiSeed, 1788971303U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesLichMonsterMagicAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-autonomous.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 27U);
	EXPECT_EQ(state.life, 6144);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 1);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 1U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesBoneDemonMonsterMagicAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-bone-demon.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 6400);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesArchLichMonsterMagicAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-arch-lich.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 6400);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesPsychorbMonsterMagicAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-psychorb.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 6400);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesNecromorbMonsterMagicAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-necromorb.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 1U);
	EXPECT_EQ(state.life, 6400);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 255U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesLichMonsterMagicMissAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-miss.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 27U);
	EXPECT_EQ(state.life, 6400);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 0);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 229U);
	EXPECT_EQ(state.projectiles[0].lastCollisionTargetEntityId, 1U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesLichMonsterMagicSecondaryImpactAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-secondary-impact.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.secondaryPlayer = fixture.initialState.secondaryPlayer.player;
	state.secondaryPlayerCharacterName = fixture.initialState.secondaryPlayer.characterName;
	state.secondaryPlayerEntityId = fixture.initialState.secondaryPlayer.entityId;
	state.secondaryPlayerLife = fixture.initialState.secondaryPlayer.life;
	state.secondaryPlayerLifeMaximum = fixture.initialState.secondaryPlayer.lifeMaximum;
	state.secondaryPlayerPositionX = fixture.initialState.secondaryPlayer.positionX;
	state.secondaryPlayerPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.secondaryPlayerLevelId = fixture.initialState.secondaryPlayer.levelId;
	state.secondaryPlayerCharacterLevel = fixture.initialState.secondaryPlayer.characterLevel;
	state.secondaryPlayerCharacterClassId = fixture.initialState.secondaryPlayer.characterClass;
	state.secondaryPlayerMagic = fixture.initialState.secondaryPlayer.magic;
	state.secondaryPlayerDexterity = fixture.initialState.secondaryPlayer.dexterity;
	state.secondaryPlayerFriendlyMode = fixture.initialState.secondaryPlayer.friendlyMode;
	state.secondaryPlayerBlockCapable = fixture.initialState.secondaryPlayer.blockCapable;
	state.secondaryPlayerCombatMode = fixture.initialState.secondaryPlayer.combatMode;
	state.secondaryPlayerCombatModeEndTick = fixture.initialState.secondaryPlayer.combatModeEndTick;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 27U);
	EXPECT_EQ(state.life, 0);
	EXPECT_EQ(state.secondaryPlayerLife, 6144);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, 8U);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 1);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 1U);
	EXPECT_EQ(state.projectiles[0].lastCollisionTargetEntityId, 0U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesLichMonsterMagicSecondaryBlockAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-monster-magic-secondary-block.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	ASSERT_TRUE(fixture.initialState.secondaryPlayer.defined);
	ASSERT_TRUE(fixture.initialProjectiles.empty());

	ReplayFixtureExecutionState state;
	state.multiplayer = true;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.lifeMaximum;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.levelId = fixture.initialState.levelId;
	state.characterLevel = fixture.initialState.characterLevel;
	state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
	state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
	state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
	state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
	state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
	state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
	state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
	state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
	state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
	state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
	state.combatTargetToHit = fixture.initialState.combatTargetToHit;
	state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
	state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
	state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
	state.secondaryPlayer = fixture.initialState.secondaryPlayer.player;
	state.secondaryPlayerEntityId = fixture.initialState.secondaryPlayer.entityId;
	state.secondaryPlayerLife = fixture.initialState.secondaryPlayer.life;
	state.secondaryPlayerLifeMaximum = fixture.initialState.secondaryPlayer.lifeMaximum;
	state.secondaryPlayerPositionX = fixture.initialState.secondaryPlayer.positionX;
	state.secondaryPlayerPositionY = fixture.initialState.secondaryPlayer.positionY;
	state.secondaryPlayerLevelId = fixture.initialState.secondaryPlayer.levelId;
	state.secondaryPlayerCharacterLevel = fixture.initialState.secondaryPlayer.characterLevel;
	state.secondaryPlayerDexterity = fixture.initialState.secondaryPlayer.dexterity;
	state.secondaryPlayerBlockCapable = fixture.initialState.secondaryPlayer.blockCapable;
	state.secondaryPlayerCombatMode = fixture.initialState.secondaryPlayer.combatMode;
	state.secondaryPlayerCombatModeEndTick = fixture.initialState.secondaryPlayer.combatModeEndTick;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.lastAppliedTick, 27U);
	EXPECT_EQ(state.life, 0);
	EXPECT_EQ(state.secondaryPlayerLife, 6400);
	EXPECT_EQ(state.secondaryPlayerCombatMode, 6U);
	EXPECT_EQ(state.secondaryPlayerCombatModeEndTick, 31U);
	ASSERT_EQ(state.projectiles.size(), 1U);
	EXPECT_EQ(state.projectiles[0].behavior, 50);
	EXPECT_EQ(state.projectiles[0].spellId, 1U);
	EXPECT_EQ(state.projectiles[0].targetEntityId, state.secondaryPlayerEntityId);
	EXPECT_EQ(state.projectiles[0].damageType, 3);
	EXPECT_EQ(state.projectiles[0].phase, 1);
	EXPECT_EQ(state.projectiles[0].remainingTicks, 1U);
	EXPECT_EQ(ComputeReplayFixtureStateHash(state), fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesStatusExpiryByAuthoritativeTick)
{
	std::ifstream file("test/fixtures/replay/gameplay-status-expiry.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.abilitySpellId = fixture.initialState.abilitySpellId;
	state.memorizedSpells = fixture.initialState.memorizedSpells;
	state.spellStatusEffectId = fixture.initialState.statusEffectId;
	state.spellStatusDuration = fixture.initialState.statusDuration;
	state.spellStatusMagnitude = fixture.initialState.statusMagnitude;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.positionX, 1);
	EXPECT_TRUE(state.statusEffects.empty());
}

TEST(ReplayFixture, RejectsMalformedJson)
{
	ReplayFixture fixture;
	std::string error;

	EXPECT_FALSE(ParseReplayFixture(R"({ "format_version": 1, "commands": [ })", fixture, error));
	EXPECT_FALSE(error.empty());
}

} // namespace
} // namespace devilution
