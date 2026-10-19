#pragma once

/**
 * @file network/authoritative/runtime_configuration.hpp
 *
 * Compatibility include for the renamed server-backed configuration.
 */

#include "network/authoritative/server_backed_configuration.hpp"

namespace devilution::authoritative {

using RuntimeConfiguration [[deprecated("Use ServerBackedRuntimeConfiguration")]] = ServerBackedRuntimeConfiguration;

inline ServerBackedRuntimeConfiguration &GetRuntimeConfiguration()
{
	return GetServerBackedRuntimeConfiguration();
}

inline tl::expected<ServerBackedRuntimeConfiguration, std::string> ParseAuthoritativeEndpoint(std::string_view endpoint)
{
	return ParseServerEndpoint(endpoint);
}

} // namespace devilution::authoritative
