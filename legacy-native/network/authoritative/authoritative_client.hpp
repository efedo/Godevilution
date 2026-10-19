#pragma once

/**
 * @file network/authoritative/authoritative_client.hpp
 *
 * Compatibility include for the renamed server-backed client.
 */

#include "network/authoritative/server_backed_client.hpp"

namespace devilution::authoritative {

using AuthoritativeClient [[deprecated("Use ServerBackedClient")]] = ServerBackedClient;

} // namespace devilution::authoritative
