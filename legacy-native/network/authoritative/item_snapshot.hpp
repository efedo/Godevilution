#pragma once

/**
 * @file network/authoritative/item_snapshot.hpp
 *
 * Native projection of one authoritative item snapshot.
 */

#include <cstdint>
#include <string>

#include <expected.hpp>

#include "game/items/items.hpp"

namespace devilution::protocol::v1 {
class ItemStateSnapshot;
}

namespace devilution::authoritative {

/** Converts one server-owned item state into the legacy native item value type. */
[[nodiscard]] tl::expected<Item, std::string> ProjectNativeItem(
    const ::devilution::protocol::v1::ItemStateSnapshot &state,
    uint32_t itemSeed,
    uint32_t price);

} // namespace devilution::authoritative
