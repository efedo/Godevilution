#pragma once

/**
 * @file network/protocol/command_delivery.hpp
 *
 * Transport-independent client command delivery state.
 */

#include <cstddef>
#include <cstdint>
#include <vector>

namespace devilution {

enum class CommandAckStatus : uint8_t {
	Accepted,
	Rejected,
	Rescheduled,
	Duplicate,
};

struct CommandAcknowledgement {
	uint64_t clientSequence = 0;
	uint64_t serverReceiptSequence = 0;
	uint64_t appliedTick = 0;
	CommandAckStatus status = CommandAckStatus::Accepted;
};

/**
 * Tracks client commands until the server acknowledges their outcome.
 *
 * The caller supplies monotonic millisecond timestamps. A command remains
 * pending across retries, while every acknowledgement status is terminal for
 * that client sequence. This class does not own transport or gameplay policy.
 */
class CommandDeliveryTracker {
public:
	explicit CommandDeliveryTracker(uint64_t initialRttMs = 100);

	/** Allocates a session-scoped sequence number for a new command. */
	[[nodiscard]] uint64_t RegisterCommand();

	/**
	 * Imports the server's monotonic sequence floor without rewriting pending
	 * commands. Returns false when the floor would make future allocation
	 * impossible.
	 */
	[[nodiscard]] bool ObserveServerSequenceFloor(uint64_t floor) noexcept;

	/** Prevents allocation after the uint64 sequence space is exhausted. */
	[[nodiscard]] bool CanRegisterCommand() const noexcept;

	/** Records that a command was sent or resent at the supplied time. */
	[[nodiscard]] bool MarkSent(uint64_t clientSequence, uint64_t nowMs);

	/**
	 * Returns commands whose acknowledgement timeout elapsed and marks them as
	 * resent. The caller should transmit each returned sequence immediately.
	 */
	[[nodiscard]] std::vector<uint64_t> PrepareResubmissions(uint64_t nowMs);

	/** Defers retrying a command until the supplied absolute timestamp. */
	[[nodiscard]] bool SetRetryNotBefore(uint64_t clientSequence, uint64_t notBeforeMs);

	/** Resolves a pending command and updates the RTT estimate. */
	[[nodiscard]] bool ApplyAcknowledgement(const CommandAcknowledgement &ack, uint64_t nowMs, bool observeRoundTrip = true);

	[[nodiscard]] uint64_t RetryTimeoutMs() const noexcept;
	[[nodiscard]] uint64_t SmoothedRttMs() const noexcept { return smoothedRttMs_; }
	[[nodiscard]] std::size_t PendingCount() const noexcept { return pending_.size(); }

private:
	struct PendingCommand {
		uint64_t clientSequence;
		uint64_t lastSentAtMs = 0;
		uint64_t retryNotBeforeMs = 0;
		uint32_t attemptCount = 0;
		bool sent = false;
	};

	[[nodiscard]] PendingCommand *FindPending(uint64_t clientSequence) noexcept;
	void ObserveRoundTrip(uint64_t sampleMs) noexcept;

	static constexpr uint64_t MinRetryTimeoutMs = 100;
	static constexpr uint64_t MaxRetryTimeoutMs = 5000;

	uint64_t nextClientSequence_ = 1;
	uint64_t sequenceFloor_ = 0;
	bool sequenceSpaceExhausted_ = false;
	uint64_t smoothedRttMs_;
	uint64_t rttVarianceMs_;
	std::vector<PendingCommand> pending_;
};

} // namespace devilution
