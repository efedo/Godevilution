#pragma once

/**
 * @file network/authoritative/envelope_codec.hpp
 *
 * Length-prefixed framing for the authoritative Protobuf transport.
 */

#include <cstdint>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <vector>

#include <asio/ip/tcp.hpp>

#include <expected.hpp>

#include "network/authoritative/protocol_limits.hpp"

namespace devilution::authoritative {

class EnvelopeCodec {
public:
	using Payload = std::vector<uint8_t>;
	using ReadResult = tl::expected<std::optional<Payload>, std::string>;
	using WriteResult = tl::expected<void, std::string>;

	static constexpr uint32_t MaxEnvelopeBytes = static_cast<uint32_t>(MaximumEnvelopeBytes);

	/** Writes one bounded, four-byte little-endian length-prefixed payload. */
	static WriteResult Write(asio::ip::tcp::socket &socket, std::span<const uint8_t> payload);

	/** Reads one payload; an orderly close before the next header returns nullopt. */
	static ReadResult Read(asio::ip::tcp::socket &socket);
};

} // namespace devilution::authoritative
