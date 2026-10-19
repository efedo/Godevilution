#pragma once

/**
 * @file network/authoritative/server_backed_configuration.hpp
 *
 * Runtime configuration for the server-backed client connection.
 */

#include <cstdint>
#include <string>
#include <string_view>

#include <expected.hpp>

namespace devilution::authoritative {

struct ServerBackedRuntimeConfiguration {
	/** The legacy mode remains the default for backward-compatible launches. */
	enum class GameMode {
		Legacy,
		Authoritative,
	};

	GameMode mode = GameMode::Legacy;
	bool enabled = false;
	std::string host = "127.0.0.1";
	uint16_t port = 6113;
	std::string clientBuildId = "devilutionx-client";
	/** Protocol v2 is the production identity contract; v1 remains explicit compatibility. */
	std::string protocolSchemaVersion = "0.2.0";
	std::string contentManifestHash;
	std::string rulesetIdentityHash;
	std::string resumeToken;
	/** Reads/writes the server-issued resume token when non-empty. */
	std::string resumeTokenPath;
	/** Directory for handshake mismatch and startup diagnostic dumps. */
	std::string diagnosticsDirectory = "authoritative-diagnostics";
};

/** Returns the process-wide server-backed connection settings. */
ServerBackedRuntimeConfiguration &GetServerBackedRuntimeConfiguration();

/** Parses a command-line server endpoint and enables the server-backed client. */
tl::expected<ServerBackedRuntimeConfiguration, std::string> ParseServerEndpoint(std::string_view endpoint);

} // namespace devilution::authoritative
