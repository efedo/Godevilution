#pragma once

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <limits>

namespace devilution::authoritative::detail {

/** Timestamped local projection used by clock-gated server-backed commands. */
class GatedSnapshotClock {
public:
	using TimePoint = std::chrono::steady_clock::time_point;

	/** Preserves the legacy raw-tick observation used by Immediate scheduling. */
	void Observe(uint64_t tick, TimePoint observedAt) noexcept
	{
		if (!hasSnapshotClock_ || tick >= lastSnapshotTick_) {
			lastSnapshotTick_ = tick;
			hasSnapshotClock_ = true;
			lastSnapshotObservedAt_ = observedAt;
		}
	}

	/** Retains the projected tick for stale, equal, or floor-equivalent snapshots. */
	void ObserveMonotonic(uint64_t tick, uint32_t tickRateHz, TimePoint observedAt) noexcept
	{
		if (!hasSnapshotClock_) {
			lastSnapshotTick_ = tick;
			hasSnapshotClock_ = true;
			lastSnapshotObservedAt_ = observedAt;
			return;
		}
		if (tick > CurrentTick(tickRateHz, observedAt)) {
			lastSnapshotTick_ = tick;
			lastSnapshotObservedAt_ = observedAt;
		}
	}

	[[nodiscard]] bool HasSnapshotClock() const noexcept
	{
		return hasSnapshotClock_;
	}

	[[nodiscard]] uint64_t CurrentTick(uint32_t tickRateHz, TimePoint now) const noexcept
	{
		if (!hasSnapshotClock_)
			return 0;

		const uint64_t rate = std::max<uint32_t>(tickRateHz, 1U);
		uint64_t elapsedTicks = 0;
		if (now > lastSnapshotObservedAt_) {
			const auto elapsedMs = static_cast<uint64_t>(
			    std::chrono::duration_cast<std::chrono::milliseconds>(now - lastSnapshotObservedAt_).count());
			elapsedTicks = elapsedMs > std::numeric_limits<uint64_t>::max() / rate
			    ? std::numeric_limits<uint64_t>::max()
			    : (elapsedMs * rate) / 1000U;
		}
		return SaturatingAdd(lastSnapshotTick_, elapsedTicks);
	}

	void Reset() noexcept
	{
		lastSnapshotTick_ = 0;
		hasSnapshotClock_ = false;
		lastSnapshotObservedAt_ = {};
	}

private:
	static uint64_t SaturatingAdd(uint64_t left, uint64_t right) noexcept
	{
		return left > std::numeric_limits<uint64_t>::max() - right
		    ? std::numeric_limits<uint64_t>::max()
		    : left + right;
	}

	uint64_t lastSnapshotTick_ = 0;
	bool hasSnapshotClock_ = false;
	TimePoint lastSnapshotObservedAt_ {};
};

} // namespace devilution::authoritative::detail
