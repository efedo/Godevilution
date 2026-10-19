/**
 * @file network/protocol/command_delivery.cpp
 *
 * Transport-independent client command delivery state.
 */

#include "network/protocol/command_delivery.hpp"

#include <algorithm>
#include <limits>

namespace devilution {

CommandDeliveryTracker::CommandDeliveryTracker(uint64_t initialRttMs)
    : smoothedRttMs_(std::max(initialRttMs, uint64_t { 1 }))
    , rttVarianceMs_(std::max(smoothedRttMs_ / 2, uint64_t { 1 }))
{
}

uint64_t CommandDeliveryTracker::RegisterCommand()
{
	if (!CanRegisterCommand())
		return 0;

	while (FindPending(nextClientSequence_) != nullptr) {
		if (nextClientSequence_ == std::numeric_limits<uint64_t>::max())
			return 0;
		++nextClientSequence_;
		if (nextClientSequence_ == 0)
			nextClientSequence_ = 1;
	}

	const uint64_t sequence = nextClientSequence_++;
	if (sequence == std::numeric_limits<uint64_t>::max()) {
		nextClientSequence_ = 0;
		sequenceSpaceExhausted_ = true;
	}
	pending_.push_back({ .clientSequence = sequence });
	return sequence;
}

bool CommandDeliveryTracker::ObserveServerSequenceFloor(uint64_t floor) noexcept
{
	if (sequenceSpaceExhausted_)
		return false;
	if (floor < sequenceFloor_)
		return true;
	sequenceFloor_ = floor;
	if (floor == std::numeric_limits<uint64_t>::max()) {
		nextClientSequence_ = 0;
		sequenceSpaceExhausted_ = true;
		return false;
	}
	const uint64_t next = floor + 1;
	if (nextClientSequence_ == 0 || nextClientSequence_ <= floor)
		nextClientSequence_ = next;
	return CanRegisterCommand();
}

bool CommandDeliveryTracker::CanRegisterCommand() const noexcept
{
	if (nextClientSequence_ == 0)
		return false;
	return !sequenceSpaceExhausted_ && sequenceFloor_ < std::numeric_limits<uint64_t>::max();
}

bool CommandDeliveryTracker::MarkSent(uint64_t clientSequence, uint64_t nowMs)
{
	PendingCommand *pending = FindPending(clientSequence);
	if (pending == nullptr)
		return false;
	pending->lastSentAtMs = nowMs;
	++pending->attemptCount;
	pending->sent = true;
	return true;
}

std::vector<uint64_t> CommandDeliveryTracker::PrepareResubmissions(uint64_t nowMs)
{
	std::vector<uint64_t> resubmissions;
	for (PendingCommand &pending : pending_) {
		if (!pending.sent || nowMs < pending.lastSentAtMs || nowMs < pending.retryNotBeforeMs
		    || nowMs - pending.lastSentAtMs < RetryTimeoutMs())
			continue;
		pending.lastSentAtMs = nowMs;
		++pending.attemptCount;
		resubmissions.push_back(pending.clientSequence);
	}
	return resubmissions;
}

bool CommandDeliveryTracker::SetRetryNotBefore(uint64_t clientSequence, uint64_t notBeforeMs)
{
	PendingCommand *pending = FindPending(clientSequence);
	if (pending == nullptr)
		return false;
	pending->retryNotBeforeMs = notBeforeMs;
	return true;
}

bool CommandDeliveryTracker::ApplyAcknowledgement(const CommandAcknowledgement &ack, uint64_t nowMs, bool observeRoundTrip)
{
	const auto pending = std::find_if(pending_.begin(), pending_.end(), [&ack](const PendingCommand &command) {
		return command.clientSequence == ack.clientSequence;
	});
	if (pending == pending_.end())
		return false;

	if (observeRoundTrip && pending->sent && nowMs >= pending->lastSentAtMs)
		ObserveRoundTrip(nowMs - pending->lastSentAtMs);
	pending_.erase(pending);
	return true;
}

uint64_t CommandDeliveryTracker::RetryTimeoutMs() const noexcept
{
	const uint64_t varianceWindow = rttVarianceMs_ > (std::numeric_limits<uint64_t>::max() / 4)
	    ? std::numeric_limits<uint64_t>::max()
	    : rttVarianceMs_ * 4;
	const uint64_t timeout = smoothedRttMs_ > std::numeric_limits<uint64_t>::max() - varianceWindow
	    ? std::numeric_limits<uint64_t>::max()
	    : smoothedRttMs_ + varianceWindow;
	return std::clamp(timeout, MinRetryTimeoutMs, MaxRetryTimeoutMs);
}

CommandDeliveryTracker::PendingCommand *CommandDeliveryTracker::FindPending(uint64_t clientSequence) noexcept
{
	for (PendingCommand &pending : pending_) {
		if (pending.clientSequence == clientSequence)
			return &pending;
	}
	return nullptr;
}

void CommandDeliveryTracker::ObserveRoundTrip(uint64_t sampleMs) noexcept
{
	const uint64_t delta = sampleMs > smoothedRttMs_ ? sampleMs - smoothedRttMs_ : smoothedRttMs_ - sampleMs;
	const uint64_t weightedVariance = rttVarianceMs_ > std::numeric_limits<uint64_t>::max() / 3
	    ? std::numeric_limits<uint64_t>::max()
	    : rttVarianceMs_ * 3;
	rttVarianceMs_ = weightedVariance > std::numeric_limits<uint64_t>::max() - delta
	    ? std::numeric_limits<uint64_t>::max()
	    : (weightedVariance + delta) / 4;
	const uint64_t weightedRtt = smoothedRttMs_ > std::numeric_limits<uint64_t>::max() / 7
	    ? std::numeric_limits<uint64_t>::max()
	    : smoothedRttMs_ * 7;
	smoothedRttMs_ = weightedRtt > std::numeric_limits<uint64_t>::max() - sampleMs
	    ? std::numeric_limits<uint64_t>::max()
	    : (weightedRtt + sampleMs) / 8;
}

} // namespace devilution
