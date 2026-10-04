#include <algorithm>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <limits>
#include <string>
#include <vector>

#include <gtest/gtest.h>

#include "engine/animationinfo.h"
#include "network/protocol/network_ticks.h"

using namespace devilution;

enum class TestDataType {
	SetNewAnimation,
	GameTick,
	Rendering
};

/**
 * @brief Represents a Action in the game_logic or rendering.
 */
struct TestData {
	virtual ~TestData() = default;
	virtual TestDataType type() const = 0;
};

/**
 * @brief Represents a call to setNewAnimation
 */
struct SetNewAnimationData : public TestData {
	SetNewAnimationData(int numberOfFrames, int delayLen, AnimationDistributionFlags params = AnimationDistributionFlags::None, int numSkippedFrames = 0, int distributeFramesBeforeFrame = 0)
	{
		_NumberOfFrames = numberOfFrames;
		_DelayLen = delayLen;
		_Params = params;
		_NumSkippedFrames = numSkippedFrames;
		_DistributeFramesBeforeFrame = distributeFramesBeforeFrame;
	}

	TestDataType type() const override
	{
		return TestDataType::SetNewAnimation;
	}

	int _NumberOfFrames;
	int _DelayLen;
	AnimationDistributionFlags _Params;
	int _NumSkippedFrames;
	int _DistributeFramesBeforeFrame;
};

/**
 * @brief Represents a GameTick, this includes skipping of Frames (for example because of Fastest Attack Modifier) and processAnimation (which also updates Animation Frame/Count).
 */
struct GameTickData : TestData {
	int _ExpectedAnimationFrame;
	int _ExpectedAnimationCnt;
	GameTickData(int expectedAnimationFrame, int expectedAnimationCnt)
	{
		_ExpectedAnimationFrame = expectedAnimationFrame;
		_ExpectedAnimationCnt = expectedAnimationCnt;
	}

	TestDataType type() const override
	{
		return TestDataType::GameTick;
	}
};

/**
 * @brief Represents a rendering/drawing Action.
 * This happens directly after the game_logic (_fProgressToNextGameTick = 0) or if no GameTick is due between two GameTicks (_fProgressToNextGameTick != 0).
 */
struct RenderingData : TestData {
	/**
	 * @brief the progress as a fraction (0.0f to 1.0f) in time to the next game tick
	 */
	float _fProgressToNextGameTick;
	int _ExpectedRenderingFrame;
	RenderingData(float fProgressToNextGameTick, int expectedRenderingFrame)
	{
		this->_fProgressToNextGameTick = fProgressToNextGameTick;
		this->_ExpectedRenderingFrame = expectedRenderingFrame;
	}

	TestDataType type() const override
	{
		return TestDataType::Rendering;
	}
};

/**
 * @brief
 * This UnitTest tries to simulate the GameLoop (GameTickData) and the Rendering that can happen (RenderingData).
 * Rendering can happen more often than GameTicks and at any time between two GameTicks.
 * The Animation Distribution Logic must adjust to the Rendering that happens at any give point in time.
 */
void RunAnimationTest(const std::vector<TestData *> &vecTestData)
{
	AnimationInfo animInfo = {};

	int currentGameTick = 0;
	for (TestData *x : vecTestData) {
		switch (x->type()) {
		case TestDataType::SetNewAnimation: {
			auto *setNewAnimationData = static_cast<SetNewAnimationData *>(x);
			animInfo.setNewAnimation(std::nullopt, setNewAnimationData->_NumberOfFrames, setNewAnimationData->_DelayLen, setNewAnimationData->_Params, setNewAnimationData->_NumSkippedFrames, setNewAnimationData->_DistributeFramesBeforeFrame);
		} break;
		case TestDataType::GameTick: {
			auto *gameTickData = static_cast<GameTickData *>(x);
			currentGameTick += 1;
			animInfo.processAnimation();
			EXPECT_EQ(animInfo.currentFrame, gameTickData->_ExpectedAnimationFrame);
			EXPECT_EQ(animInfo.tickCounterOfCurrentFrame, gameTickData->_ExpectedAnimationCnt);
		} break;
		case TestDataType::Rendering: {
			auto *renderingData = static_cast<RenderingData *>(x);
			ProgressToNextGameTick = static_cast<uint8_t>(renderingData->_fProgressToNextGameTick * AnimationInfo::baseValueFraction);
			EXPECT_EQ(animInfo.getFrameToUseForRendering(), renderingData->_ExpectedRenderingFrame)
			    << std::fixed << std::setprecision(2)
			    << "ProgressToNextGameTick: " << renderingData->_fProgressToNextGameTick
			    << " currentFrame: " << animInfo.currentFrame
			    << " DelayCounter: " << animInfo.tickCounterOfCurrentFrame
			    << " GameTick: " << currentGameTick;
		} break;
		}
	}
	for (TestData *x : vecTestData) {
		delete x;
	}
}

namespace {

struct TimingOperation {
	const char *kind;
	int actor = 0;
	int frames = 0;
	int ticks = 0;
	int flags = 0;
	int skipped = 0;
	int distribute = 0;
	int preview = 0;
	bool value = false;
};

struct TimingScenario {
	std::string name;
	std::vector<TimingOperation> operations;
};

void AppendTicks(TimingScenario &scenario, int count, bool reverse = false, int actor = 0)
{
	for (int i = 0; i < count; ++i)
		scenario.operations.push_back({ "advance", actor, 0, 0, 0, 0, 0, 0, reverse });
}

std::vector<TimingScenario> TimingScenarios()
{
	std::vector<TimingScenario> scenarios;
	const auto add = [&](std::string name, int frames, int ticks, int flags, int skipped, int distribute, int preview = 0) {
		TimingScenario scenario { std::move(name), { { "start", 0, frames, ticks, flags, skipped, distribute, preview } } };
		AppendTicks(scenario, frames * std::max(1, ticks) + 4);
		scenarios.push_back(std::move(scenario));
	};
	add("stand", 10, 4, 0, 0, 0);
	for (const int skip : { -2, -1, 2, 3 })
		add("walk-" + std::to_string(skip), 8, 1, ProcessAnimationPending, skip, 0);
	for (int skip = 0; skip <= 4; ++skip)
		add("attack-" + std::to_string(skip), 16, 1, ProcessAnimationPending, skip, 9);
	for (int skip = 0; skip <= 2; ++skip)
		add("ranged-" + std::to_string(skip), 12, 1, ProcessAnimationPending, skip, 7);
	add("cast", 12, 1, ProcessAnimationPending, 0, 7);
	add("block", 6, 3, SkipsDelayOfLastFrame, 0, 0);
	add("fast-block", 6, 3, SkipsDelayOfLastFrame, 4, 0);
	for (int skip = 0; skip <= 3; ++skip)
		add("hit-" + std::to_string(skip), 8, 1, 0, skip, 0);
	for (const int preview : { 1, 64, 127, 128 })
		add("preview-" + std::to_string(preview), 16, 1, ProcessAnimationPending, 2, 9, preview);
	for (const int priorTicks : { 8, 9, 12, 15, 16 }) {
		TimingScenario repeated { "repeated-" + std::to_string(priorTicks), { { "start", 0, 16, 1, ProcessAnimationPending, 0, 9 } } };
		AppendTicks(repeated, priorTicks);
		repeated.operations.push_back({ "start", 0, 16, 1, ProcessAnimationPending | RepeatedAction, 4, 9 });
		AppendTicks(repeated, 20);
		scenarios.push_back(std::move(repeated));
	}
	TimingScenario reverse { "reverse", { { "start", 0, 10, 3 } } };
	AppendTicks(reverse, 34, true);
	scenarios.push_back(std::move(reverse));
	TimingScenario changed { "change-data", { { "start", 0, 16, 1, ProcessAnimationPending, 2, 9 } } };
	AppendTicks(changed, 10);
	changed.operations.push_back({ "change", 0, 5, 3 });
	AppendTicks(changed, 7);
	changed.operations.push_back({ "change", 0, 5, 3 });
	AppendTicks(changed, 12);
	scenarios.push_back(std::move(changed));
	TimingScenario petrified { "petrified", { { "start", 0, 16, 1, ProcessAnimationPending, 4, 9 } } };
	AppendTicks(petrified, 2);
	petrified.operations.push_back({ "petrify", 0, 0, 0, 0, 0, 0, 0, true });
	AppendTicks(petrified, 4);
	petrified.operations.push_back({ "petrify" });
	AppendTicks(petrified, 5);
	scenarios.push_back(std::move(petrified));
	TimingScenario independent { "independent", { { "start", 0, 10, 4 }, { "start", 1, 16, 1, ProcessAnimationPending, 2, 9 } } };
	for (int i = 0; i < 20; ++i) {
		AppendTicks(independent, 1, false, 0);
		AppendTicks(independent, 1, false, 1);
	}
	scenarios.push_back(std::move(independent));
	add("normalized-zero", 10, 0, 0, 0, 0);
	add("normalized-negative", 10, -1, 0, 0, 0);
	for (const int rate : { 0, -1 }) {
		TimingScenario changedRate { "changed-rate-" + std::to_string(rate), { { "start", 0, 3, 2 } } };
		AppendTicks(changedRate, 2);
		changedRate.operations.push_back({ "change", 0, 3, rate });
		AppendTicks(changedRate, 3);
		scenarios.push_back(std::move(changedRate));
	}
	TimingScenario empty { "changed-empty", { { "start", 0, 3, 2 } } };
	AppendTicks(empty, 2);
	empty.operations.push_back({ "change", 0, 0, 1 });
	AppendTicks(empty, 3);
	scenarios.push_back(std::move(empty));
	TimingScenario carry { "carry-data-change", { { "start", 0, 16, 1, ProcessAnimationPending, 0, 9 } } };
	AppendTicks(carry, 8);
	carry.operations.push_back({ "start", 0, 16, 1, ProcessAnimationPending | RepeatedAction, 4, 9 });
	AppendTicks(carry, 1);
	carry.operations.push_back({ "petrify", 0, 0, 0, 0, 0, 0, 0, true });
	carry.operations.push_back({ "change", 0, 16, 1 });
	carry.operations.push_back({ "change", 0, 5, 3 });
	AppendTicks(carry, 2);
	carry.operations.push_back({ "start", 0, 5, 3 });
	AppendTicks(carry, 10);
	scenarios.push_back(std::move(carry));
	TimingScenario different { "different-count-repeat", { { "start", 0, 16, 1, ProcessAnimationPending, 0, 9 } } };
	AppendTicks(different, 8);
	different.operations.push_back({ "start", 0, 12, 1, ProcessAnimationPending | RepeatedAction, 2, 7 });
	AppendTicks(different, 10);
	scenarios.push_back(std::move(different));
	TimingScenario death { "caller-death-hold", { { "start", 0, 20, 2 } } };
	AppendTicks(death, 38);
	// Mirrors DoDeath's direct delay assignment, not ChangeAnimationData.
	death.operations.push_back({ "rate", 0, 0, 100 });
	AppendTicks(death, 40);
	death.operations.push_back({ "hold" });
	scenarios.push_back(std::move(death));
	TimingScenario wrap { "signed-phase-wrap", { { "start", 0, 127, 4 } } };
	AppendTicks(wrap, 260);
	scenarios.push_back(std::move(wrap));
	TimingScenario counter { "counter-boundary", { { "start", 0, 127, 127 } } };
	AppendTicks(counter, 128);
	scenarios.push_back(std::move(counter));
	TimingScenario reset { "petrified-reset", { { "start", 0, 8, 1, ProcessAnimationPending, 2, 7 } } };
	AppendTicks(reset, 2);
	reset.operations.push_back({ "petrify", 0, 0, 0, 0, 0, 0, 0, true });
	reset.operations.push_back({ "start", 0, 8, 1 });
	AppendTicks(reset, 8);
	scenarios.push_back(std::move(reset));
	for (const int rate : { 0, -1 }) {
		TimingScenario distributed { "distributed-rate-" + std::to_string(rate), { { "start", 0, 8, 1, RepeatedAction } } };
		AppendTicks(distributed, 1);
		distributed.operations.push_back({ "rate", 0, 0, rate });
		AppendTicks(distributed, 3);
		distributed.operations.push_back({ "petrify", 0, 0, 0, 0, 0, 0, 0, true });
		AppendTicks(distributed, 1);
		scenarios.push_back(std::move(distributed));
	}
	for (const int frames : { -1, -2, -128 }) {
		TimingScenario negative { "changed-negative-" + std::to_string(frames), { { "start", 0, 3, 2 } } };
		AppendTicks(negative, 2);
		negative.operations.push_back({ "change", 0, frames, 1 });
		AppendTicks(negative, 1);
		AppendTicks(negative, 2, true);
		scenarios.push_back(std::move(negative));
	}
	TimingScenario negativeReverse { "negative-reverse-wrap", { { "start", 0, 3, 1 }, { "change", 0, -128, 1 } } };
	AppendTicks(negativeReverse, 1);
	AppendTicks(negativeReverse, 2, true);
	scenarios.push_back(std::move(negativeReverse));
	return scenarios;
}

// Check native assertion/division/arithmetic preconditions before querying.
// This never supplies an expected result: eligible values call the real method.
int ProgressRefusal(const AnimationTimingState &state, int fraction)
{
	int16_t sequenceTicks = std::max<int16_t>(0, state.ticksSinceSequenceStarted);
	int32_t modifier = state.tickModifier;
	if (state.relevantFramesForDistributing <= 0) {
		if (state.ticksPerFrame <= 0)
			return 0; // Native explicitly returns zero before division.
		sequenceTicks = static_cast<int16_t>((state.currentFrame * state.ticksPerFrame + state.tickCounterOfCurrentFrame) * AnimationInfo::baseValueFraction);
		modifier = AnimationInfo::baseValueFraction / state.ticksPerFrame;
	}
	if (state.numberOfFrames == 0)
		return 1;
	const int64_t product = (sequenceTicks + (state.isPetrified ? 0 : fraction)) * static_cast<int64_t>(modifier);
	if (product < std::numeric_limits<int32_t>::min() || product > std::numeric_limits<int32_t>::max())
		return 2;
	if (product == std::numeric_limits<int32_t>::min() && state.numberOfFrames == -1)
		return 2; // Signed division cannot represent INT_MIN / -1.
	if (product / state.numberOfFrames / AnimationInfo::baseValueFraction > AnimationInfo::baseValueFraction)
		return 3;
	return 0;
}

void WriteTimingState(std::ostream &output, const AnimationTimingState &state)
{
	output << "{\"NumberOfFrames\":" << static_cast<int>(state.numberOfFrames)
	       << ",\"TicksPerFrame\":" << static_cast<int>(state.ticksPerFrame)
	       << ",\"TickCounterOfCurrentFrame\":" << static_cast<int>(state.tickCounterOfCurrentFrame)
	       << ",\"CurrentFrame\":" << static_cast<int>(state.currentFrame)
	       << ",\"IsPetrified\":" << (state.isPetrified ? "true" : "false")
	       << ",\"RelevantFramesForDistributing\":" << static_cast<int>(state.relevantFramesForDistributing)
	       << ",\"SkippedFramesFromPreviousAnimation\":" << static_cast<int>(state.skippedFramesFromPreviousAnimation)
	       << ",\"TickModifier\":" << state.tickModifier
	       << ",\"TicksSinceSequenceStarted\":" << state.ticksSinceSequenceStarted << '}';
}

} // namespace

TEST(AnimationInfo, CaptureTimingOracle)
{
	const char *directory = std::getenv("GODEVILUTION_ANIMATION_TIMING_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0')
		GTEST_SKIP() << "Opt-in scalar animation timing oracle.";
	const std::filesystem::path root(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	const auto destination = root / "animation-timing.json";
	ASSERT_FALSE(std::filesystem::exists(destination));
	const auto previousProgress = ProgressToNextGameTick;
	struct RestoreProgress {
		uint8_t previous;
		~RestoreProgress() { ProgressToNextGameTick = previous; }
	} restore { previousProgress };
	std::ofstream output(destination, std::ios::binary);
	ASSERT_TRUE(output.good());
	output << "{\"FormatVersion\":1,\"FractionBase\":128,\"Scenarios\":[";
	bool firstScenario = true;
	for (const auto &scenario : TimingScenarios()) {
		std::array<AnimationInfo, 2> actors {};
		if (!firstScenario)
			output << ',';
		firstScenario = false;
		output << "{\"Name\":\"" << scenario.name << "\",\"Steps\":[";
		bool firstStep = true;
		for (const auto &operation : scenario.operations) {
			if (!firstStep)
				output << ',';
			firstStep = false;
			auto &animation = actors[operation.actor];
			const std::string kind(operation.kind);
			if (kind == "start")
				animation.setNewAnimation(std::nullopt, operation.frames, operation.ticks,
				    static_cast<AnimationDistributionFlags>(operation.flags), operation.skipped, operation.distribute, operation.preview);
			else if (kind == "advance")
				animation.processAnimation(operation.value);
			else if (kind == "change")
				animation.changeAnimationData(std::nullopt, operation.frames, operation.ticks);
			else if (kind == "petrify")
				animation.isPetrified = operation.value;
			else if (kind == "rate")
				animation.ticksPerFrame = operation.ticks;
			else if (kind == "hold") {
				ASSERT_EQ(animation.currentFrame, 19);
				ASSERT_EQ(animation.tickCounterOfCurrentFrame, 40);
			}
			else
				FAIL() << "Unknown timing operation.";
			output << "{\"Kind\":\"" << kind << "\",\"Actor\":" << operation.actor
			       << ",\"Frames\":" << operation.frames << ",\"Ticks\":" << operation.ticks
			       << ",\"Flags\":" << operation.flags << ",\"Skipped\":" << operation.skipped
			       << ",\"Distribute\":" << operation.distribute << ",\"Preview\":" << operation.preview
			       << ",\"Value\":" << (operation.value ? "true" : "false") << ",\"State\":";
			const auto captured = animation.captureTimingState();
			WriteTimingState(output, captured);
			output << ",\"RenderFrames\":[";
			for (int fraction = 0; fraction <= AnimationInfo::baseValueFraction; ++fraction) {
				if (fraction != 0)
					output << ',';
				ProgressToNextGameTick = fraction;
				output << static_cast<int>(animation.getFrameToUseForRendering());
			}
			output << "],\"ProgressResults\":[";
			for (int fraction = 0; fraction <= AnimationInfo::baseValueFraction; ++fraction) {
				if (fraction != 0)
					output << ',';
				ProgressToNextGameTick = static_cast<uint8_t>(fraction);
				const auto refusal = ProgressRefusal(captured, fraction);
				output << "{\"Refusal\":" << refusal << ",\"Value\":";
				if (refusal == 0)
					output << static_cast<int>(animation.getAnimationProgress());
				else
					output << "null";
				output << '}';
			}
			output << "]}";
			const auto afterRendering = animation.captureTimingState();
			EXPECT_EQ(captured.numberOfFrames, afterRendering.numberOfFrames);
			EXPECT_EQ(captured.ticksPerFrame, afterRendering.ticksPerFrame);
			EXPECT_EQ(captured.tickCounterOfCurrentFrame, afterRendering.tickCounterOfCurrentFrame);
			EXPECT_EQ(captured.ticksSinceSequenceStarted, afterRendering.ticksSinceSequenceStarted);
			EXPECT_EQ(captured.currentFrame, afterRendering.currentFrame);
			EXPECT_EQ(captured.isPetrified, afterRendering.isPetrified);
			EXPECT_EQ(captured.relevantFramesForDistributing, afterRendering.relevantFramesForDistributing);
			EXPECT_EQ(captured.skippedFramesFromPreviousAnimation, afterRendering.skippedFramesFromPreviousAnimation);
			EXPECT_EQ(captured.tickModifier, afterRendering.tickModifier);
		}
		output << "]}";
	}
	output << "]}\r\n";
	output.flush();
	ASSERT_TRUE(output.good());
}

TEST(AnimationInfo, AttackSwordWarrior) // ProcessAnimationPending should be considered by distribution logic
{
	RunAnimationTest(
	    {
	        new SetNewAnimationData(16, 1, AnimationDistributionFlags::ProcessAnimationPending, 0, 9),
	        // processAnimation directly after StartAttack (in same GameTick). So we don't see any rendering before.
	        new GameTickData(1, 0),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 0),
	        new RenderingData(0.8f, 0),
	        new GameTickData(2, 0),
	        new RenderingData(0.0f, 1),
	        new RenderingData(0.3f, 1),
	        new RenderingData(0.6f, 1),
	        new RenderingData(0.8f, 2),
	        new GameTickData(3, 0),
	        new RenderingData(0.0f, 2),
	        new RenderingData(0.3f, 2),
	        new RenderingData(0.6f, 2),
	        new RenderingData(0.8f, 3),
	        new GameTickData(4, 0),
	        new RenderingData(0.0f, 3),
	        new RenderingData(0.3f, 3),
	        new RenderingData(0.6f, 4),
	        new RenderingData(0.8f, 4),
	        new GameTickData(5, 0),
	        new RenderingData(0.0f, 4),
	        new RenderingData(0.3f, 4),
	        new RenderingData(0.6f, 5),
	        new RenderingData(0.8f, 5),
	        new GameTickData(6, 0),
	        new RenderingData(0.0f, 5),
	        new RenderingData(0.3f, 6),
	        new RenderingData(0.6f, 6),
	        new RenderingData(0.8f, 6),
	        new GameTickData(7, 0),
	        new RenderingData(0.0f, 6),
	        new RenderingData(0.3f, 7),
	        new RenderingData(0.6f, 7),
	        new RenderingData(0.8f, 7),

	        // After this GameTick, the Animation Distribution Logic is disabled
	        new GameTickData(8, 0),
	        new RenderingData(0.1f, 8),
	        new GameTickData(9, 0),
	        new RenderingData(0.4f, 9),
	        new GameTickData(10, 0),
	        new RenderingData(0.4f, 10),
	        new GameTickData(11, 0),
	        new RenderingData(0.3f, 11),
	        new GameTickData(12, 0),
	        new RenderingData(0.0f, 12),
	        new GameTickData(13, 0),
	        new RenderingData(0.6f, 13),
	        new GameTickData(14, 0),
	        new RenderingData(0.6f, 14),
	        new GameTickData(15, 0),
	        new RenderingData(0.6f, 15),
	        // Animation stopped because PM_DoAttack would stop the Animation "if (plr[pnum].animInfo.currentFrame == plr[pnum]._pAFrames - 1) {"
	    });
}

TEST(AnimationInfo, AttackSwordWarriorWithFastestAttack) // Skipped frames and ProcessAnimationPending should be considered by distribution logic
{
	RunAnimationTest(
	    {
	        new SetNewAnimationData(16, 1, AnimationDistributionFlags::ProcessAnimationPending, 2, 9),
	        // processAnimation directly after StartAttack (in same GameTick). So we don't see any rendering before.
	        new GameTickData(3, 0),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 0),
	        new RenderingData(0.8f, 1),
	        new GameTickData(4, 0),
	        new RenderingData(0.0f, 1),
	        new RenderingData(0.3f, 2),
	        new RenderingData(0.6f, 2),
	        new RenderingData(0.8f, 2),
	        new GameTickData(5, 0),
	        new RenderingData(0.0f, 3),
	        new RenderingData(0.3f, 3),
	        new RenderingData(0.6f, 4),
	        new RenderingData(0.8f, 4),
	        new GameTickData(6, 0),
	        new RenderingData(0.0f, 4),
	        new RenderingData(0.3f, 5),
	        new RenderingData(0.6f, 5),
	        new RenderingData(0.8f, 6),
	        new GameTickData(7, 0),
	        new RenderingData(0.0f, 6),
	        new RenderingData(0.3f, 6),
	        new RenderingData(0.6f, 7),
	        new RenderingData(0.8f, 7),

	        // After this GameTick, the Animation Distribution Logic is disabled
	        new GameTickData(8, 0),
	        new RenderingData(0.1f, 8),
	        new GameTickData(9, 0),
	        new RenderingData(0.4f, 9),
	        new GameTickData(10, 0),
	        new RenderingData(0.4f, 10),
	        new GameTickData(11, 0),
	        new RenderingData(0.3f, 11),
	        new GameTickData(12, 0),
	        new RenderingData(0.0f, 12),
	        new GameTickData(13, 0),
	        new RenderingData(0.6f, 13),
	        new GameTickData(14, 0),
	        new RenderingData(0.6f, 14),
	        new GameTickData(15, 0),
	        new RenderingData(0.6f, 15),
	        // Animation stopped because PM_DoAttack would stop the Animation "if (plr[pnum].animInfo.currentFrame == plr[pnum]._pAFrames - 1) {"
	    });
}

/**
 * @brief The Warrior make two attacks. The second queued attack cancels the first after the Hit Frame.
 */
TEST(AnimationInfo, AttackSwordWarriorRepeated)
{
	RunAnimationTest(
	    {
	        new SetNewAnimationData(16, 1, AnimationDistributionFlags::ProcessAnimationPending, 0, 9),
	        // processAnimation directly after StartAttack (in same GameTick). So we don't see any rendering before.
	        new GameTickData(1, 0),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 0),
	        new RenderingData(0.8f, 0),
	        new GameTickData(2, 0),
	        new RenderingData(0.0f, 1),
	        new RenderingData(0.3f, 1),
	        new RenderingData(0.6f, 1),
	        new RenderingData(0.8f, 2),
	        new GameTickData(3, 0),
	        new RenderingData(0.0f, 2),
	        new RenderingData(0.3f, 2),
	        new RenderingData(0.6f, 2),
	        new RenderingData(0.8f, 3),
	        new GameTickData(4, 0),
	        new RenderingData(0.0f, 3),
	        new RenderingData(0.3f, 3),
	        new RenderingData(0.6f, 4),
	        new RenderingData(0.8f, 4),
	        new GameTickData(5, 0),
	        new RenderingData(0.0f, 4),
	        new RenderingData(0.3f, 4),
	        new RenderingData(0.6f, 5),
	        new RenderingData(0.8f, 5),
	        new GameTickData(6, 0),
	        new RenderingData(0.0f, 5),
	        new RenderingData(0.3f, 6),
	        new RenderingData(0.6f, 6),
	        new RenderingData(0.8f, 6),
	        new GameTickData(7, 0),
	        new RenderingData(0.0f, 6),
	        new RenderingData(0.3f, 7),
	        new RenderingData(0.6f, 7),
	        new RenderingData(0.8f, 7),

	        // After this GameTick, the Animation Distribution Logic is disabled
	        new GameTickData(8, 0),
	        new RenderingData(0.1f, 8),
	        new GameTickData(9, 0),
	        new RenderingData(0.3f, 9),

	        // Start of repeated attack, because plr[pnum].animInfo.currentFrame > plr[myplr]._pAFNum
	        new SetNewAnimationData(16, 1, static_cast<AnimationDistributionFlags>(AnimationDistributionFlags::ProcessAnimationPending | AnimationDistributionFlags::RepeatedAction), 0, 9),
	        // processAnimation directly after StartAttack (in same GameTick). So we don't see any rendering before.
	        new GameTickData(1, 0),
	        new RenderingData(0.0f, 10),
	        new RenderingData(0.3f, 10),
	        new RenderingData(0.6f, 11),
	        new RenderingData(0.8f, 11),
	        new GameTickData(2, 0),
	        new RenderingData(0.0f, 12),
	        new RenderingData(0.3f, 12),
	        new RenderingData(0.6f, 13),
	        new RenderingData(0.8f, 13),
	        new GameTickData(3, 0),
	        new RenderingData(0.0f, 14),
	        new RenderingData(0.3f, 14),
	        new RenderingData(0.6f, 15),
	        new RenderingData(0.8f, 15),
	        new GameTickData(4, 0),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 1),
	        new RenderingData(0.8f, 1),
	        new GameTickData(5, 0),
	        new RenderingData(0.0f, 2),
	        new RenderingData(0.3f, 2),
	        new RenderingData(0.6f, 3),
	        new RenderingData(0.8f, 3),
	        new GameTickData(6, 0),
	        new RenderingData(0.0f, 4),
	        new RenderingData(0.3f, 4),
	        new RenderingData(0.6f, 5),
	        new RenderingData(0.8f, 5),
	        new GameTickData(7, 0),
	        new RenderingData(0.0f, 6),
	        new RenderingData(0.3f, 6),
	        new RenderingData(0.6f, 7),
	        new RenderingData(0.8f, 7),

	        // After this GameTick, the Animation Distribution Logic is disabled
	        new GameTickData(8, 0),
	        new RenderingData(0.1f, 8),
	        new GameTickData(9, 0),
	        new RenderingData(0.4f, 9),
	        new GameTickData(10, 0),
	        new RenderingData(0.4f, 10),
	        new GameTickData(11, 0),
	        new RenderingData(0.3f, 11),
	        new GameTickData(12, 0),
	        new RenderingData(0.0f, 12),
	        new GameTickData(13, 0),
	        new RenderingData(0.6f, 13),
	        new GameTickData(14, 0),
	        new RenderingData(0.6f, 14),
	        new GameTickData(15, 0),
	        new RenderingData(0.6f, 15),
	        // Animation stopped because PM_DoAttack would stop the Animation "if (plr[pnum].animInfo.currentFrame == plr[pnum]._pAFrames - 1) {"
	    });
}

TEST(AnimationInfo, BlockingWarriorNormal) // Ignored delay for last Frame should be considered by distribution logic
{
	RunAnimationTest(
	    {
	        new SetNewAnimationData(2, 3, AnimationDistributionFlags::SkipsDelayOfLastFrame),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 0),
	        new RenderingData(0.8f, 0),
	        new GameTickData(0, 1),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 0),
	        new RenderingData(0.8f, 0),
	        new GameTickData(0, 2),
	        new RenderingData(0.0f, 1),
	        new RenderingData(0.3f, 1),
	        new RenderingData(0.6f, 1),
	        new RenderingData(0.8f, 1),
	        new GameTickData(1, 0),
	        new RenderingData(0.0f, 1),
	        new RenderingData(0.3f, 1),
	        new RenderingData(0.6f, 1),
	        new RenderingData(0.8f, 1),
	        // Animation stopped because PM_DoBlock would stop the Animation "if (plr[pnum].animInfo.currentFrame >= plr[pnum]._pBFrames) {"
	    });
}

TEST(AnimationInfo, BlockingSorcererWithFastBlock) // Skipped frames and ignored delay for last Frame should be considered by distribution logic
{
	RunAnimationTest(
	    {
	        new SetNewAnimationData(6, 3, AnimationDistributionFlags::SkipsDelayOfLastFrame, 4),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 0),
	        new RenderingData(0.8f, 1),
	        new GameTickData(4, 1),
	        new RenderingData(0.0f, 1),
	        new RenderingData(0.3f, 1),
	        new RenderingData(0.6f, 2),
	        new RenderingData(0.8f, 2),
	        new GameTickData(4, 2),
	        new RenderingData(0.0f, 3),
	        new RenderingData(0.3f, 3),
	        new RenderingData(0.6f, 3),
	        new RenderingData(0.8f, 4),
	        new GameTickData(5, 0),
	        new RenderingData(0.0f, 4),
	        new RenderingData(0.3f, 4),
	        new RenderingData(0.6f, 5),
	        new RenderingData(0.8f, 5),
	        // Animation stopped because PM_DoBlock would stop the Animation "if (plr[pnum].animInfo.currentFrame >= plr[pnum]._pBFrames) {"
	    });
}

TEST(AnimationInfo, HitRecoverySorcererHarmony) // Skipped frames and ignored delay for last Frame should be considered by distribution logic
{
	RunAnimationTest(
	    {
	        new SetNewAnimationData(8, 1, AnimationDistributionFlags::None, 3),
	        new RenderingData(0.0f, 0),
	        new RenderingData(0.3f, 0),
	        new RenderingData(0.6f, 0),
	        new RenderingData(0.8f, 1),
	        new GameTickData(4, 0),
	        new RenderingData(0.0f, 1),
	        new RenderingData(0.3f, 2),
	        new RenderingData(0.6f, 2),
	        new RenderingData(0.8f, 2),
	        new GameTickData(5, 0),
	        new RenderingData(0.0f, 3),
	        new RenderingData(0.3f, 3),
	        new RenderingData(0.6f, 4),
	        new RenderingData(0.8f, 4),
	        new GameTickData(6, 0),
	        new RenderingData(0.0f, 4),
	        new RenderingData(0.3f, 5),
	        new RenderingData(0.6f, 5),
	        new RenderingData(0.8f, 6),
	        new GameTickData(7, 0),
	        new RenderingData(0.0f, 6),
	        new RenderingData(0.3f, 6),
	        new RenderingData(0.6f, 7),
	        new RenderingData(0.8f, 7),
	        // Animation stopped because PM_DoGotHit would stop the Animation "if (plr[pnum].animInfo.currentFrame >= plr[pnum]._pHFrames) {"
	    });
}
TEST(AnimationInfo, Stand) // Distribution Logic shouldn't change anything here
{
	RunAnimationTest(
	    {
	        new SetNewAnimationData(10, 4),
	        new RenderingData(0.1f, 0),
	        new GameTickData(0, 1),
	        new RenderingData(0.6f, 0),
	        new GameTickData(0, 2),
	        new RenderingData(0.6f, 0),
	        new GameTickData(0, 3),
	        new RenderingData(0.6f, 0),

	        new GameTickData(1, 0),
	        new RenderingData(0.6f, 1),
	        new GameTickData(1, 1),
	        new RenderingData(0.6f, 1),
	        new GameTickData(1, 2),
	        new RenderingData(0.6f, 1),
	        new GameTickData(1, 3),
	        new RenderingData(0.6f, 1),

	        new GameTickData(2, 0),
	        new RenderingData(0.6f, 2),
	        new GameTickData(2, 1),
	        new RenderingData(0.6f, 2),
	        new GameTickData(2, 2),
	        new RenderingData(0.6f, 2),
	        new GameTickData(2, 3),
	        new RenderingData(0.6f, 2),

	        new GameTickData(3, 0),
	        new RenderingData(0.6f, 3),
	        new GameTickData(3, 1),
	        new RenderingData(0.6f, 3),
	        new GameTickData(3, 2),
	        new RenderingData(0.6f, 3),
	        new GameTickData(3, 3),
	        new RenderingData(0.6f, 3),

	        new GameTickData(4, 0),
	        new RenderingData(0.6f, 4),
	        new GameTickData(4, 1),
	        new RenderingData(0.6f, 4),
	        new GameTickData(4, 2),
	        new RenderingData(0.6f, 4),
	        new GameTickData(4, 3),
	        new RenderingData(0.6f, 4),

	        new GameTickData(5, 0),
	        new RenderingData(0.6f, 5),
	        new GameTickData(5, 1),
	        new RenderingData(0.6f, 5),
	        new GameTickData(5, 2),
	        new RenderingData(0.6f, 5),
	        new GameTickData(5, 3),
	        new RenderingData(0.6f, 5),

	        new GameTickData(6, 0),
	        new RenderingData(0.6f, 6),
	        new GameTickData(6, 1),
	        new RenderingData(0.6f, 6),
	        new GameTickData(6, 2),
	        new RenderingData(0.6f, 6),
	        new GameTickData(6, 3),
	        new RenderingData(0.6f, 6),

	        new GameTickData(7, 0),
	        new RenderingData(0.6f, 7),
	        new GameTickData(7, 1),
	        new RenderingData(0.6f, 7),
	        new GameTickData(7, 2),
	        new RenderingData(0.6f, 7),
	        new GameTickData(7, 3),
	        new RenderingData(0.6f, 7),

	        new GameTickData(8, 0),
	        new RenderingData(0.6f, 8),
	        new GameTickData(8, 1),
	        new RenderingData(0.6f, 8),
	        new GameTickData(8, 2),
	        new RenderingData(0.6f, 8),
	        new GameTickData(8, 3),
	        new RenderingData(0.6f, 8),

	        new GameTickData(9, 0),
	        new RenderingData(0.6f, 9),
	        new GameTickData(9, 1),
	        new RenderingData(0.6f, 9),
	        new GameTickData(9, 2),
	        new RenderingData(0.6f, 9),
	        new GameTickData(9, 3),
	        new RenderingData(0.6f, 9),

	        // Animation starts again
	        new GameTickData(0, 0),
	        new RenderingData(0.1f, 0),
	        new GameTickData(0, 1),
	        new RenderingData(0.6f, 0),
	        new GameTickData(0, 2),
	        new RenderingData(0.6f, 0),
	        new GameTickData(0, 3),
	        new RenderingData(0.6f, 0),
	    });
}
