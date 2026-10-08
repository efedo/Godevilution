#include <gmock/gmock.h>
#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <string>
#include <string_view>
#include <system_error>
#include <utility>
#include <vector>

#include "engine/assets.hpp"
#include "engine/random.hpp"
#include "game/quests/quests.hpp"

#include "tables/objdat.h" // For quest IDs
#include "utils/paths.h"

namespace devilution {
namespace {

class ScopedQuestPoolState {
public:
	ScopedQuestPoolState()
	    : questPools_(QuestPools)
	    , overridePaths_(OverridePaths)
	    , assetsPath_(paths::AssetsPath())
	    , globalRandomSeed_(GetLCGEngineState())
	{
		std::copy_n(Quests, MAXQUESTS, quests_.begin());
	}

	ScopedQuestPoolState(const ScopedQuestPoolState &) = delete;
	ScopedQuestPoolState &operator=(const ScopedQuestPoolState &) = delete;

	~ScopedQuestPoolState()
	{
		QuestPools.swap(questPools_);
		std::copy(quests_.begin(), quests_.end(), Quests);
		OverridePaths.swap(overridePaths_);
		if (paths::AssetsPath() != assetsPath_)
			paths::SetAssetsPath(assetsPath_);
		SetRndSeed(globalRandomSeed_);
	}

private:
	std::vector<std::vector<quest_id>> questPools_;
	std::array<Quest, MAXQUESTS> quests_;
	std::vector<std::string> overridePaths_;
	std::string assetsPath_;
	uint32_t globalRandomSeed_;
};

struct QuestPoolSeedCase {
	uint32_t seed;
	std::array<quest_id, 5> unavailableQuests;
	size_t unavailableCount;
};

constexpr std::array<QuestPoolSeedCase, 8> LoadedPoolSeedCases {{
	// Literal outputs were derived independently from DiabloGenerator's 32-bit Borland LCG arithmetic.
	{ 1457187811U, { Q_SKELKING, Q_LTBANNER, Q_BLIND, Q_ZHAR, Q_VEIL }, 5 },
	// The loaded-table compatibility path skips pool 1, then selects pools 2-5.
	{ 988045466U, { Q_BUTCHER, Q_ROCK, Q_MUSHROOM, Q_VEIL, Q_ROCK }, 4 },
	{ 4203210069U, { Q_SKELKING, Q_LTBANNER, Q_BLIND, Q_ZHAR, Q_WARLORD }, 5 },
	{ 2557708932U, { Q_SKELKING, Q_LTBANNER, Q_ROCK, Q_MUSHROOM, Q_WARLORD }, 5 },
	{ 1272442071U, { Q_SKELKING, Q_GARBUD, Q_ROCK, Q_ZHAR, Q_VEIL }, 5 },
	{ 509604U, { Q_PWATER, Q_GARBUD, Q_ROCK, Q_ZHAR, Q_VEIL }, 5 },
	{ 0U, { Q_SKELKING, Q_LTBANNER, Q_BLIND, Q_ANVIL, Q_VEIL }, 5 },
	{ 1U, { Q_SKELKING, Q_BUTCHER, Q_BLOOD, Q_ZHAR, Q_WARLORD }, 5 },
}};

bool ConfigureBuildQuestPoolOverride(std::string &error)
{
	const std::filesystem::path assetsPath { paths::AssetsPath() };
	if (assetsPath.empty()) {
		error = "The native test build has no configured assets directory.";
		return false;
	}
	std::error_code pathError;
	if (!assetsPath.is_absolute() || !std::filesystem::is_directory(assetsPath, pathError) || pathError) {
		error = "The configured native test assets path is not an existing absolute directory.";
		return false;
	}

	const std::filesystem::path questPoolPath = assetsPath / "txtdata" / "quests" / "quest_pools.tsv";
	std::ifstream input(questPoolPath, std::ios::binary);
	if (!input) {
		error = "The loose build quest_pools.tsv is unavailable.";
		return false;
	}

	constexpr std::array<std::string_view, 14> expectedLines {{
		"poolId\tquestId",
		"1\tQ_SKELKING",
		"1\tQ_PWATER",
		"2\tQ_BUTCHER",
		"2\tQ_LTBANNER",
		"2\tQ_GARBUD",
		"3\tQ_BLIND",
		"3\tQ_ROCK",
		"3\tQ_BLOOD",
		"4\tQ_MUSHROOM",
		"4\tQ_ZHAR",
		"4\tQ_ANVIL",
		"5\tQ_VEIL",
		"5\tQ_WARLORD",
	}};
	std::vector<std::string> actualLines;
	std::string line;
	while (std::getline(input, line)) {
		if (!line.empty() && line.back() == '\r')
			line.pop_back();
		actualLines.push_back(line);
	}
	if (!input.eof() || actualLines.size() != expectedLines.size()) {
		error = "The loose build quest_pools.tsv could not be read as the expected table.";
		return false;
	}
	for (size_t i = 0; i < expectedLines.size(); ++i) {
		if (actualLines[i] != expectedLines[i]) {
			error = "The loose build quest_pools.tsv differs from the canonical test fixture.";
			return false;
		}
	}

	std::string overridePath = assetsPath.string();
	if (overridePath.back() != '/' && overridePath.back() != '\\')
		overridePath.push_back(std::filesystem::path::preferred_separator);
	OverridePaths.assign(1, std::move(overridePath));
	return true;
}

void ExpectQuestsRestored(const std::array<Quest, MAXQUESTS> &expected)
{
	for (size_t i = 0; i < MAXQUESTS; ++i) {
		SCOPED_TRACE(i);
		EXPECT_EQ(Quests[i]._qidx, expected[i]._qidx);
		EXPECT_EQ(Quests[i]._qactive, expected[i]._qactive);
		EXPECT_EQ(Quests[i]._qlevel, expected[i]._qlevel);
		EXPECT_EQ(Quests[i].position.x, expected[i].position.x);
		EXPECT_EQ(Quests[i].position.y, expected[i].position.y);
		EXPECT_EQ(Quests[i]._qlvltype, expected[i]._qlvltype);
		EXPECT_EQ(Quests[i]._qslvl, expected[i]._qslvl);
		EXPECT_EQ(Quests[i]._qlog, expected[i]._qlog);
		EXPECT_EQ(Quests[i]._qmsg, expected[i]._qmsg);
		EXPECT_EQ(Quests[i]._qvar1, expected[i]._qvar1);
		EXPECT_EQ(Quests[i]._qvar2, expected[i]._qvar2);
	}
}

} // namespace

void ResetQuests()
{
	for (auto &quest : Quests)
		quest._qactive = QUEST_INIT;
}

std::vector<quest_state> GetActiveFlagsForSlice(std::initializer_list<quest_id> ids)
{
	std::vector<quest_state> temp;

	for (auto id : ids)
		temp.push_back(Quests[id]._qactive);

	return temp;
}

TEST(QuestTest, SinglePlayerBadPools)
{
	ResetQuests();

	// (INT_MIN >> 16) % 2 = 0, so the times when the RNG calls GenerateRnd(2) don't end up with a negative value.
	InitialiseQuestPools(1457187811, Quests);
	EXPECT_EQ(Quests[Q_SKELKING]._qactive, QUEST_NOTAVAIL) << "Skeleton King quest is deactivated with 'bad' seed";
	ResetQuests();

	// Having this seed for level 15 requires starting a game at 1977-12-28 07:44:42 PM or 2087-02-18 10:43:02 PM
	// Given Diablo was released in 1996 and it's currently 2024 this will never have been naturally hit. It's not
	//  possible to hit this case on a pre-NT kernel windows system but it may be possible on macos or winnt?
	InitialiseQuestPools(988045466, Quests);
	EXPECT_THAT(GetActiveFlagsForSlice({ Q_BUTCHER, Q_LTBANNER, Q_GARBUD }), ::testing::Each(QUEST_INIT)) << "All quests in pool 2 remain active with 'bad' seed";
	ResetQuests();

	// This seed can only be reached by editing a save file or modifying the game. Given quest state (including
	//  availability) is saved as part of the game state there's no vanilla compatibility concerns here.
	InitialiseQuestPools(4203210069U, Quests);
	// If we wanted to retain the behaviour that vanilla Diablo would've done we should instead deactivate
	//  Quests[QuestGroup2[-2]]. This would hit QuestGroup1[1] (Ogden's Sign), however that's already marked
	//  as unavailable with this seed due to the previous quest group's roll.
	EXPECT_EQ(Quests[Q_LTBANNER]._qactive, QUEST_NOTAVAIL) << "Ogden's Sign should be deactivated with 'bad' seed";
	EXPECT_EQ(Quests[Q_BLIND]._qactive, QUEST_NOTAVAIL) << "Halls of the Blind should also be deactivated with 'bad' seed";
	ResetQuests();

	// This seed can only be reached by editing a save file or modifying the game. Given quest state (including
	//  availability) is saved as part of the game state there's no vanilla compatibility concerns here.
	InitialiseQuestPools(2557708932U, Quests);
	// If we wanted to retain the behaviour that vanilla Diablo would've done we should instead deactivate
	//  Quests[QuestGroup3[-2]]. This would hit QuestGroup2[1] (Magic Rock), however that's already marked
	//  as unavailable with this seed due to the previous quest group's roll.
	EXPECT_EQ(Quests[Q_ROCK]._qactive, QUEST_NOTAVAIL) << "Magic Rock should be deactivated with 'bad' seed";
	EXPECT_EQ(Quests[Q_MUSHROOM]._qactive, QUEST_NOTAVAIL) << "Black Mushroom should also be deactivated with 'bad' seed";
	ResetQuests();

	InitialiseQuestPools(1272442071, Quests);
	EXPECT_EQ(Quests[Q_VEIL]._qactive, QUEST_NOTAVAIL) << "Lachdan quest is deactivated with 'bad' seed";
}

TEST(QuestTest, SinglePlayerGoodPools)
{
	ResetQuests();

	InitialiseQuestPools(509604, Quests);
	EXPECT_EQ(Quests[Q_SKELKING]._qactive, QUEST_INIT) << "Expected Skeleton King quest to be available with the given seed";
	EXPECT_EQ(Quests[Q_PWATER]._qactive, QUEST_NOTAVAIL) << "Expected Poison Water quest to be deactivated with the given seed";

	EXPECT_EQ(Quests[Q_BUTCHER]._qactive, QUEST_INIT) << "Expected Butcher quest to be available with the given seed";
	EXPECT_EQ(Quests[Q_LTBANNER]._qactive, QUEST_INIT) << "Expected Ogden's Sign quest to be available with the given seed";
	EXPECT_EQ(Quests[Q_GARBUD]._qactive, QUEST_NOTAVAIL) << "Expected Gharbad the Weak quest to be deactivated with the given seed";

	EXPECT_EQ(Quests[Q_BLIND]._qactive, QUEST_INIT) << "Expected Halls of the Blind quest to be available with the given seed";
	EXPECT_EQ(Quests[Q_ROCK]._qactive, QUEST_NOTAVAIL) << "Expected Magic Rock quest to be deactivated with the given seed";
	EXPECT_EQ(Quests[Q_BLOOD]._qactive, QUEST_INIT) << "Expected Valor quest to be available with the given seed";

	EXPECT_EQ(Quests[Q_MUSHROOM]._qactive, QUEST_INIT) << "Expected Black Mushroom quest to be available with the given seed";
	EXPECT_EQ(Quests[Q_ZHAR]._qactive, QUEST_NOTAVAIL) << "Expected Zhar the Mad quest to be deactivated with the given seed";
	EXPECT_EQ(Quests[Q_ANVIL]._qactive, QUEST_INIT) << "Expected Anvil of Fury quest to be available with the given seed";

	EXPECT_EQ(Quests[Q_VEIL]._qactive, QUEST_NOTAVAIL) << "Expected Lachdanan quest to be deactivated with the given seed";
	EXPECT_EQ(Quests[Q_WARLORD]._qactive, QUEST_INIT) << "Expected Warlord of Blood quest to be available with the given seed";
}

TEST(QuestTest, LoadedPoolsPreserveCanonicalTsvOrder)
{
	const std::vector<std::vector<quest_id>> originalPools = QuestPools;
	const std::vector<std::string> originalOverrides = OverridePaths;
	const std::string originalAssetsPath = paths::AssetsPath();
	const uint32_t originalRandomState = GetLCGEngineState();
	std::array<Quest, MAXQUESTS> originalQuests;
	std::copy_n(Quests, MAXQUESTS, originalQuests.begin());

	{
		ScopedQuestPoolState restoreState;
		std::string setupError;
		ASSERT_TRUE(ConfigureBuildQuestPoolOverride(setupError)) << setupError;
		ASSERT_EQ(OverridePaths.size(), 1U);
		ASSERT_FALSE(OverridePaths.front().empty());
		ASSERT_TRUE(OverridePaths.front().back() == '/' || OverridePaths.front().back() == '\\');

		LoadQuestPools();
		ASSERT_EQ(QuestPools.size(), 6U);
		EXPECT_TRUE(QuestPools[0].empty());
		EXPECT_THAT(QuestPools[1], ::testing::ElementsAre(Q_SKELKING, Q_PWATER));
		EXPECT_THAT(QuestPools[2], ::testing::ElementsAre(Q_BUTCHER, Q_LTBANNER, Q_GARBUD));
		EXPECT_THAT(QuestPools[3], ::testing::ElementsAre(Q_BLIND, Q_ROCK, Q_BLOOD));
		EXPECT_THAT(QuestPools[4], ::testing::ElementsAre(Q_MUSHROOM, Q_ZHAR, Q_ANVIL));
		EXPECT_THAT(QuestPools[5], ::testing::ElementsAre(Q_VEIL, Q_WARLORD));
	}

	EXPECT_EQ(QuestPools, originalPools);
	ExpectQuestsRestored(originalQuests);
	EXPECT_EQ(OverridePaths, originalOverrides);
	EXPECT_EQ(paths::AssetsPath(), originalAssetsPath);
	EXPECT_EQ(GetLCGEngineState(), originalRandomState);
}

TEST(QuestTest, LoadedPoolsMatchLiteralSeededSelection)
{
	const std::vector<std::vector<quest_id>> originalPools = QuestPools;
	const std::vector<std::string> originalOverrides = OverridePaths;
	const std::string originalAssetsPath = paths::AssetsPath();
	const uint32_t originalRandomState = GetLCGEngineState();
	std::array<Quest, MAXQUESTS> originalQuests;
	std::copy_n(Quests, MAXQUESTS, originalQuests.begin());

	{
		ScopedQuestPoolState restoreState;
		std::string setupError;
		ASSERT_TRUE(ConfigureBuildQuestPoolOverride(setupError)) << setupError;
		ASSERT_EQ(OverridePaths.size(), 1U);
		ASSERT_FALSE(OverridePaths.front().empty());
		ASSERT_TRUE(OverridePaths.front().back() == '/' || OverridePaths.front().back() == '\\');
		LoadQuestPools();
		ASSERT_EQ(QuestPools.size(), 6U);

		for (const QuestPoolSeedCase &seedCase : LoadedPoolSeedCases) {
			ResetQuests();
			InitialiseQuestPools(seedCase.seed, Quests);

			std::array<quest_state, MAXQUESTS> expectedStates;
			expectedStates.fill(QUEST_INIT);
			for (size_t i = 0; i < seedCase.unavailableCount; ++i)
				expectedStates[static_cast<size_t>(seedCase.unavailableQuests[i])] = QUEST_NOTAVAIL;

			size_t unavailableCount = 0;
			for (size_t i = 0; i < MAXQUESTS; ++i) {
				SCOPED_TRACE(::testing::Message() << "seed=" << seedCase.seed << ", quest=" << i);
				EXPECT_EQ(Quests[i]._qactive, expectedStates[i]);
				if (Quests[i]._qactive == QUEST_NOTAVAIL)
					++unavailableCount;
			}
			EXPECT_EQ(unavailableCount, seedCase.unavailableCount);
			EXPECT_EQ(GetLCGEngineState(), originalRandomState);
		}
	}

	EXPECT_EQ(QuestPools, originalPools);
	ExpectQuestsRestored(originalQuests);
	EXPECT_EQ(OverridePaths, originalOverrides);
	EXPECT_EQ(paths::AssetsPath(), originalAssetsPath);
	EXPECT_EQ(GetLCGEngineState(), originalRandomState);
}
} // namespace devilution
