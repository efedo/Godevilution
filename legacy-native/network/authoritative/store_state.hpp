#pragma once

/**
 * @file network/authoritative/store_state.hpp
 *
 * Compatibility include for the renamed server-backed vendor state.
 */

#include "network/authoritative/server_backed_vendor_state.hpp"

namespace devilution::authoritative {

using StoreIntentRoute [[deprecated("Use VendorIntentRoute")]] = VendorIntentRoute;
using AuthoritativeStorePhase [[deprecated("Use ServerBackedVendorPhase")]] = ServerBackedVendorPhase;
using AuthoritativeStoreState [[deprecated("Use ServerBackedVendorState")]] = ServerBackedVendorState;

} // namespace devilution::authoritative
