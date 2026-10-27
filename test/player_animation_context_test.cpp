#include <algorithm>
#include <array>
#include <cctype>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

#include <gtest/gtest.h>
#include <picosha2.h>

#include "application/headless_mode.hpp"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/players/players.hpp"
#include "tables/playerdat.hpp"
#include "tables/spelldat.h"
#include "utils/paths.h"

using namespace devilution;

namespace {

using FrameSet = std::array<int, 9>;

FrameSet CaptureFrames(const Player &player)
{
	return { player._pNFrames, player._pWFrames, player._pAFrames, player._pAFNum,
		player._pHFrames, player._pBFrames, player._pDFrames, player._pSFrames, player._pSFNum };
}

std::array<int, 9> CaptureCursor(const Player &player)
{
	const auto state = player.animInfo.captureTimingState();
	return { state.numberOfFrames, state.ticksPerFrame, state.tickCounterOfCurrentFrame,
		state.currentFrame, state.isPetrified, state.relevantFramesForDistributing,
		state.skippedFramesFromPreviousAnimation, state.tickModifier, state.ticksSinceSequenceStarted };
}

void InstallFrames(Player &player, const FrameSet &frames)
{
	player._pNFrames = static_cast<int8_t>(frames[0]);
	player._pWFrames = static_cast<int8_t>(frames[1]);
	player._pAFrames = static_cast<int8_t>(frames[2]);
	player._pAFNum = static_cast<int8_t>(frames[3]);
	player._pHFrames = static_cast<int8_t>(frames[4]);
	player._pBFrames = static_cast<int8_t>(frames[5]);
	player._pDFrames = static_cast<int8_t>(frames[6]);
	player._pSFrames = static_cast<int8_t>(frames[7]);
	player._pSFNum = static_cast<int8_t>(frames[8]);
}

std::array<int, 27> CaptureBase(const PlayerAnimData &data)
{
	return { data.unarmedFrames, data.unarmedActionFrame, data.unarmedShieldFrames, data.unarmedShieldActionFrame,
		data.swordFrames, data.swordActionFrame, data.swordShieldFrames, data.swordShieldActionFrame,
		data.bowFrames, data.bowActionFrame, data.axeFrames, data.axeActionFrame,
		data.maceFrames, data.maceActionFrame, data.maceShieldFrames, data.maceShieldActionFrame,
		data.staffFrames, data.staffActionFrame, data.idleFrames, data.walkingFrames, data.blockingFrames,
		data.deathFrames, data.castingFrames, data.recoveryFrames, data.townIdleFrames,
		data.townWalkingFrames, data.castingActionFrame };
}

template <size_t N>
void WriteArray(std::ostream &output, const std::array<int, N> &values)
{
	output << '[';
	for (size_t index = 0; index < N; ++index) {
		if (index != 0)
			output << ',';
		output << values[index];
	}
	output << ']';
}

struct ContextScenario {
	int playerClass;
	int weapon;
	int armor;
	FrameSet before;
	std::array<bool, 3> townSequence;
};

std::vector<ContextScenario> Scenarios()
{
	std::vector<ContextScenario> result;
	for (int playerClass = 0; playerClass < 6; ++playerClass)
		for (int weapon = 0; weapon < 9; ++weapon)
			for (const int armor : { 0, 16, 32 })
				for (const FrameSet &before : { FrameSet {}, FrameSet { -3, 0, -7, -1, -9, 0, 111, 112, -2 } })
					for (const bool townFirst : { false, true })
						result.push_back({ playerClass, weapon, armor, before, { townFirst, !townFirst, townFirst } });
	return result;
}

class NativePlayerAnimationContext : public testing::Test {
protected:
	void SetUp() override
	{
		previousAssets_ = paths::AssetsPath();
		previousType_ = levelType();
		previousHeadless_ = HeadlessMode;
		previousRandom_ = GetLCGEngineState();
		previousSpells_.swap(SpellsData);
		// Preserve test/main.cpp's configured bundle path (notably on Apple).
		// The source fallback supplies text resources only, never MPQ discovery.
		const auto source = std::filesystem::path(GODEVILUTION_CONTEXT_SOURCE_ROOT);
		const auto assets = std::filesystem::is_regular_file(std::filesystem::path(previousAssets_) / "txtdata/classes/classdat.tsv")
		    ? std::filesystem::path(previousAssets_)
		    : source / "assets";
		ASSERT_TRUE(std::filesystem::is_regular_file(assets / "txtdata/classes/classdat.tsv"));
		ASSERT_TRUE(std::filesystem::is_regular_file(assets / "txtdata/spells/spelldat.tsv"));
		paths::SetAssetsPath(assets.string() + "/");
		HeadlessMode = true;
		// Hidden class-table caches are owned by this dedicated test process.
		LoadPlayerDataFiles();
		LoadSpellData();
		ASSERT_EQ(GetNumPlayerClasses(), 6);
		ASSERT_EQ(SpellsData.size(), static_cast<size_t>(SpellID::LastDiablo) + 1);
		spellVariants_[0] = SpellsData;
		const auto hellfire = source / "mods/Hellfire";
		ASSERT_TRUE(std::filesystem::is_regular_file(hellfire / "txtdata/spells/spelldat.tsv"));
		paths::SetAssetsPath(hellfire.string() + "/");
		LoadSpellData();
		ASSERT_EQ(SpellsData.size(), static_cast<size_t>(SpellID::LAST) + 1);
		spellVariants_[1] = SpellsData;
		paths::SetAssetsPath(assets.string() + "/");
	}

	void TearDown() override
	{
		paths::SetAssetsPath(previousAssets_);
		levelType() = previousType_;
		HeadlessMode = previousHeadless_;
		SpellsData.swap(previousSpells_);
		SetRndSeed(previousRandom_);
	}

	std::array<std::vector<SpellData>, 2> spellVariants_;

private:
	std::string previousAssets_;
	dungeon_type previousType_ = DTYPE_TOWN;
	bool previousHeadless_ = true;
	uint32_t previousRandom_ = 0;
	std::vector<SpellData> previousSpells_;
};

TEST_F(NativePlayerAnimationContext, TownPreservesIncomingCombatFrames)
{
	Player player {};
	player._pClass = HeroClass::Warrior;
	player._pgfxnum = 16 | static_cast<uint8_t>(PlayerWeaponGraphic::Bow);
	const FrameSet before { -3, 0, -7, -1, -9, 0, 111, 112, -2 };
	InstallFrames(player, before);
	levelType() = DTYPE_TOWN;
	player.setAnimations();
	const auto after = CaptureFrames(player);
	for (const int field : { 2, 3, 4, 5 })
		EXPECT_EQ(after[field], before[field]);
	EXPECT_EQ(after[0], GetPlayerAnimDataForClass(HeroClass::Warrior).townIdleFrames);
	EXPECT_EQ(after[1], GetPlayerAnimDataForClass(HeroClass::Warrior).townWalkingFrames);
	EXPECT_EQ(after[6], 15);
}

TEST_F(NativePlayerAnimationContext, DungeonReplacesAllFramesWithoutAdvancingCursorOrRng)
{
	Player player {};
	player._pClass = HeroClass::Barbarian;
	player._pgfxnum = 32 | static_cast<uint8_t>(PlayerWeaponGraphic::Bow);
	InstallFrames(player, FrameSet { -3, 0, -7, -1, -9, 0, 111, 112, -2 });
	player.animInfo.setNewAnimation(std::nullopt, 16, 1, ProcessAnimationPending, 2, 9);
	player.animInfo.isPetrified = true;
	const auto before = CaptureCursor(player);
	const auto randomBefore = GetLCGEngineState();
	levelType() = DTYPE_CATHEDRAL;
	player.setAnimations();
	EXPECT_EQ(player._pNFrames, 8);
	EXPECT_EQ(player._pDFrames, 15);
	EXPECT_EQ(player._pAFrames, GetPlayerAnimDataForClass(HeroClass::Barbarian).bowFrames);
	EXPECT_EQ(player._pAFNum, GetPlayerAnimDataForClass(HeroClass::Barbarian).bowActionFrame);
	EXPECT_EQ(CaptureCursor(player), before);
	EXPECT_EQ(GetLCGEngineState(), randomBefore);
}

TEST_F(NativePlayerAnimationContext, SpellGraphicsUseRawMagicTypeIncludingNull)
{
	Player player {};
	player._pmode = PM_SPELL;
	player.executedSpell.spellId = SpellID::Null;
	EXPECT_EQ(GetSpellData(SpellID::Null).type(), MagicType::Fire);
	EXPECT_EQ(player.getGraphic(), player_graphic::Fire);
	for (const auto &variant : spellVariants_) {
		SpellsData = variant;
		for (size_t spell = 0; spell < SpellsData.size(); ++spell) {
			player.executedSpell.spellId = static_cast<SpellID>(spell);
			const auto type = SpellsData[spell].type();
			EXPECT_EQ(player.getGraphic(), type == MagicType::Fire ? player_graphic::Fire : type == MagicType::Lightning ? player_graphic::Lightning
			                                                                                                             : player_graphic::Magic);
		}
	}
}

TEST_F(NativePlayerAnimationContext, CaptureContextOracle)
{
	const char *directory = std::getenv("GODEVILUTION_PLAYER_ANIMATION_CONTEXT_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0')
		GTEST_SKIP() << "Opt-in effective player animation context oracle.";
	const std::filesystem::path root(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	const auto destination = root / "player-animation-context.json";
	ASSERT_FALSE(std::filesystem::exists(destination));
	const auto scenarios = Scenarios();
	ASSERT_EQ(scenarios.size(), 648);
	std::ostringstream inputs;
	inputs << "{\"Classes\":[";
	for (int playerClass = 0; playerClass < 6; ++playerClass) {
		if (playerClass != 0)
			inputs << ',';
		inputs << "{\"Class\":" << playerClass << ",\"Base\":";
		WriteArray(inputs, CaptureBase(GetPlayerAnimDataForClass(static_cast<HeroClass>(playerClass))));
		// Explicit native caller-policy inputs, frozen before any expected result.
		const bool overrides = playerClass == static_cast<int>(HeroClass::Warrior) || playerClass == static_cast<int>(HeroClass::Barbarian);
		inputs << ",\"DungeonBowIdleFrames\":" << (overrides ? 8 : 0)
		       << ",\"ArmoredDeathFrames\":" << (overrides ? 15 : 0) << '}';
	}
	inputs << "],\"Scenarios\":[";
	for (size_t index = 0; index < scenarios.size(); ++index) {
		if (index != 0)
			inputs << ',';
		const auto &scenario = scenarios[index];
		inputs << "{\"Class\":" << scenario.playerClass << ",\"WeaponGraphic\":" << scenario.weapon
		       << ",\"ArmorGraphic\":" << scenario.armor << ",\"Before\":";
		WriteArray(inputs, scenario.before);
		inputs << ",\"TownSequence\":[";
		for (size_t step = 0; step < 3; ++step) {
			if (step != 0)
				inputs << ',';
			inputs << (scenario.townSequence[step] ? "true" : "false");
		}
		inputs << "]}";
	}
	inputs << "],\"SpellInputs\":[";
	for (size_t variant = 0; variant < spellVariants_.size(); ++variant) {
		for (size_t spell = 0; spell < spellVariants_[variant].size(); ++spell) {
			if (variant != 0 || spell != 0)
				inputs << ',';
			inputs << "{\"Variant\":" << variant << ",\"Spell\":" << spell << ",\"MagicType\":" << static_cast<int>(spellVariants_[variant][spell].type()) << '}';
		}
	}
	inputs << "]}";
	std::ofstream output(destination, std::ios::binary);
	ASSERT_TRUE(output.is_open());
	auto inputFingerprint = picosha2::hash256_hex_string(inputs.str());
	std::transform(inputFingerprint.begin(), inputFingerprint.end(), inputFingerprint.begin(), [](unsigned char value) { return static_cast<char>(std::toupper(value)); });
	output << "{\"FormatVersion\":1,\"InputFingerprint\":\"" << inputFingerprint
	       << "\",\"Inputs\":" << inputs.str() << ",\"Expected\":{\"Scenarios\":[";
	const auto randomBefore = GetLCGEngineState();
	for (size_t index = 0; index < scenarios.size(); ++index) {
		if (index != 0)
			output << ',';
		const auto &scenario = scenarios[index];
		Player player {};
		player._pClass = static_cast<HeroClass>(scenario.playerClass);
		player._pgfxnum = static_cast<uint8_t>(scenario.weapon | scenario.armor);
		InstallFrames(player, scenario.before);
		player.animInfo.setNewAnimation(std::nullopt, 16, 1, ProcessAnimationPending, 2, 9);
		player.animInfo.isPetrified = true;
		const auto cursorBefore = CaptureCursor(player);
		output << '[';
		for (size_t step = 0; step < 3; ++step) {
			if (step != 0)
				output << ',';
			levelType() = scenario.townSequence[step] ? DTYPE_TOWN : DTYPE_CATHEDRAL;
			player.setAnimations();
			output << "{\"After\":";
			WriteArray(output, CaptureFrames(player));
			output << ",\"Timings\":[";
			for (int graphic = 0; graphic < 9; ++graphic) {
				if (graphic != 0)
					output << ',';
				int8_t frames;
				int8_t rate;
				player.getAnimationFramesAndTicksPerFrame(static_cast<player_graphic>(graphic), frames, rate);
				WriteArray(output, std::array<int, 2> { frames, rate });
			}
			output << "]}";
			EXPECT_EQ(CaptureCursor(player), cursorBefore);
		}
		output << ']';
	}
	output << "],\"SpellGraphics\":[";
	Player spellPlayer {};
	spellPlayer._pmode = PM_SPELL;
	for (size_t variant = 0; variant < spellVariants_.size(); ++variant) {
		SpellsData = spellVariants_[variant];
		for (size_t spell = 0; spell < SpellsData.size(); ++spell) {
			if (variant != 0 || spell != 0)
				output << ',';
			spellPlayer.executedSpell.spellId = static_cast<SpellID>(spell);
			output << static_cast<int>(spellPlayer.getGraphic());
		}
	}
	output << "]}}\r\n";
	output.close();
	ASSERT_TRUE(output.good());
	EXPECT_EQ(GetLCGEngineState(), randomBefore);
}

} // namespace
