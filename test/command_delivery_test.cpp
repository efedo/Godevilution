#include <array>
#include <cstdint>
#include <limits>
#include <vector>

#include <gtest/gtest.h>

#include "network/protocol/command_delivery.hpp"

namespace devilution {
namespace {

TEST(CommandDelivery, AllocatesSessionSequencesAndTracksPendingCommands)
{
	CommandDeliveryTracker tracker;

	const uint64_t first = tracker.RegisterCommand();
	const uint64_t second = tracker.RegisterCommand();
	EXPECT_EQ(first, 1U);
	EXPECT_EQ(second, 2U);
	EXPECT_EQ(tracker.PendingCount(), 2U);
	EXPECT_FALSE(tracker.MarkSent(999, 0));
	EXPECT_TRUE(tracker.MarkSent(first, 10));
}

TEST(CommandDelivery, DoesNotResubmitBeforeInitialSendOrTimeout)
{
	CommandDeliveryTracker tracker;
	const uint64_t sequence = tracker.RegisterCommand();

	EXPECT_TRUE(tracker.PrepareResubmissions(1000).empty());
	ASSERT_TRUE(tracker.MarkSent(sequence, 1000));
	EXPECT_TRUE(tracker.PrepareResubmissions(1299).empty());
	EXPECT_TRUE(tracker.PrepareResubmissions(1300).size() == 1U);
}

TEST(CommandDelivery, ResubmissionResetsTheTimeout)
{
	CommandDeliveryTracker tracker;
	const uint64_t sequence = tracker.RegisterCommand();
	ASSERT_TRUE(tracker.MarkSent(sequence, 0));

	EXPECT_EQ(tracker.PrepareResubmissions(300), std::vector<uint64_t>({ sequence }));
	EXPECT_TRUE(tracker.PrepareResubmissions(599).empty());
	EXPECT_EQ(tracker.PrepareResubmissions(600), std::vector<uint64_t>({ sequence }));
}

TEST(CommandDelivery, EveryServerOutcomeResolvesThePendingCommand)
{
	constexpr std::array outcomes {
	    CommandAckStatus::Accepted,
	    CommandAckStatus::Rejected,
	    CommandAckStatus::Rescheduled,
	    CommandAckStatus::Duplicate,
	};

	for (const CommandAckStatus status : outcomes) {
		CommandDeliveryTracker tracker;
		const uint64_t sequence = tracker.RegisterCommand();
		ASSERT_TRUE(tracker.MarkSent(sequence, 100));
		EXPECT_TRUE(tracker.ApplyAcknowledgement({ .clientSequence = sequence, .status = status }, 200));
		EXPECT_EQ(tracker.PendingCount(), 0U);
		EXPECT_FALSE(tracker.ApplyAcknowledgement({ .clientSequence = sequence, .status = status }, 300));
	}
}

TEST(CommandDelivery, RetryTimeoutAdaptsToMeasuredRoundTrip)
{
	CommandDeliveryTracker tracker;
	const uint64_t first = tracker.RegisterCommand();
	ASSERT_TRUE(tracker.MarkSent(first, 0));
	ASSERT_TRUE(tracker.ApplyAcknowledgement({ .clientSequence = first }, 1000));

	EXPECT_EQ(tracker.SmoothedRttMs(), 212U);
	EXPECT_EQ(tracker.RetryTimeoutMs(), 1260U);

	const uint64_t second = tracker.RegisterCommand();
	ASSERT_TRUE(tracker.MarkSent(second, 1000));
	EXPECT_TRUE(tracker.PrepareResubmissions(2259).empty());
	EXPECT_EQ(tracker.PrepareResubmissions(2260), std::vector<uint64_t>({ second }));
}

TEST(CommandDelivery, IgnoresBackwardsTimestampsForRetriesAndRtt)
{
	CommandDeliveryTracker tracker;
	const uint64_t sequence = tracker.RegisterCommand();
	ASSERT_TRUE(tracker.MarkSent(sequence, 100));

	EXPECT_TRUE(tracker.PrepareResubmissions(50).empty());
	EXPECT_TRUE(tracker.ApplyAcknowledgement({ .clientSequence = sequence }, 50));
	EXPECT_EQ(tracker.SmoothedRttMs(), 100U);
}

TEST(CommandDelivery, RestoresServerSequenceFloorWithoutRewritingPendingCommands)
{
	CommandDeliveryTracker tracker;
	const uint64_t pending = tracker.RegisterCommand();
	ASSERT_EQ(pending, 1U);
	ASSERT_TRUE(tracker.ObserveServerSequenceFloor(5));
	const uint64_t next = tracker.RegisterCommand();
	EXPECT_EQ(next, 6U);
	EXPECT_TRUE(tracker.MarkSent(pending, 10));
	EXPECT_TRUE(tracker.ApplyAcknowledgement({ .clientSequence = pending }, 10));
}

TEST(CommandDelivery, SequenceSpaceDoesNotWrapAfterMaximumFloor)
{
	CommandDeliveryTracker tracker;
	ASSERT_TRUE(tracker.ObserveServerSequenceFloor(std::numeric_limits<uint64_t>::max() - 1));
	const uint64_t maximum = tracker.RegisterCommand();
	EXPECT_EQ(maximum, std::numeric_limits<uint64_t>::max());
	ASSERT_TRUE(tracker.MarkSent(maximum, 1));
	ASSERT_TRUE(tracker.ApplyAcknowledgement({ .clientSequence = maximum }, 1));
	EXPECT_EQ(tracker.RegisterCommand(), 0U);
	EXPECT_FALSE(tracker.ObserveServerSequenceFloor(std::numeric_limits<uint64_t>::max() - 1));
	EXPECT_FALSE(tracker.ObserveServerSequenceFloor(1));
	EXPECT_EQ(tracker.RegisterCommand(), 0U);
}

TEST(CommandDelivery, DeferredRetryDoesNotTrainRttOrResubmitEarly)
{
	CommandDeliveryTracker tracker;
	const uint64_t sequence = tracker.RegisterCommand();
	ASSERT_TRUE(tracker.MarkSent(sequence, 100));
	ASSERT_TRUE(tracker.SetRetryNotBefore(sequence, 1000));
	EXPECT_TRUE(tracker.PrepareResubmissions(399).empty());
	EXPECT_EQ(tracker.PrepareResubmissions(999).size(), 0U);
	EXPECT_EQ(tracker.PrepareResubmissions(1000), std::vector<uint64_t>({ sequence }));
	EXPECT_EQ(tracker.SmoothedRttMs(), 100U);
	EXPECT_TRUE(tracker.ApplyAcknowledgement({ .clientSequence = sequence }, 1100, false));
	EXPECT_EQ(tracker.SmoothedRttMs(), 100U);
}

} // namespace
} // namespace devilution
