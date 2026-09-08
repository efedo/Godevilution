/** Native mirrors of the byte-level authoritative protocol limits. */
#pragma once

#include <cstddef>

namespace devilution::authoritative {

inline constexpr std::size_t MaximumEventBatchCount = 4096;
inline constexpr std::size_t MaximumDialogueTextBytes = 16 * 1024;
inline constexpr std::size_t MaximumIdentityBytes = 128;

} // namespace devilution::authoritative
