#include "network/authoritative/server_backed_configuration.hpp"

#include <algorithm>
#include <charconv>
#include <cctype>
#include <limits>
#include <string>

#include <asio/error_code.hpp>
#include <asio/ip/address_v6.hpp>

namespace devilution::authoritative {
namespace {

bool IsAsciiAlphaNumeric(char value)
{
	const unsigned char character = static_cast<unsigned char>(value);
	return std::isalnum(character) != 0;
}

bool IsValidHostname(std::string_view host)
{
	if (host.empty() || host.size() > 253 || host.front() == '.' || host.back() == '.')
		return false;

	std::size_t labelStart = 0;
	while (labelStart < host.size()) {
		const std::size_t labelEnd = host.find('.', labelStart);
		const std::size_t labelLength = (labelEnd == std::string_view::npos ? host.size() : labelEnd) - labelStart;
		if (labelLength == 0 || labelLength > 63)
			return false;
		if (!IsAsciiAlphaNumeric(host[labelStart]) || !IsAsciiAlphaNumeric(host[labelStart + labelLength - 1]))
			return false;
		for (std::size_t i = labelStart + 1; i + 1 < labelStart + labelLength; ++i) {
			if (!IsAsciiAlphaNumeric(host[i]) && host[i] != '-')
				return false;
		}

		if (labelEnd == std::string_view::npos)
			return true;
		labelStart = labelEnd + 1;
	}

	return true;
}

bool IsValidIPv4Address(std::string_view host)
{
	std::size_t octetStart = 0;
	for (int octetIndex = 0; octetIndex < 4; ++octetIndex) {
		const std::size_t octetEnd = host.find('.', octetStart);
		if ((octetIndex < 3 && octetEnd == std::string_view::npos)
		    || (octetIndex == 3 && octetEnd != std::string_view::npos)) {
			return false;
		}

		const std::size_t end = octetEnd == std::string_view::npos ? host.size() : octetEnd;
		const std::string_view octetText = host.substr(octetStart, end - octetStart);
		if (octetText.empty() || octetText.size() > 3)
			return false;

		uint16_t octet = 0;
		const auto [parsedEnd, error] = std::from_chars(octetText.data(), octetText.data() + octetText.size(), octet);
		if (error != std::errc() || parsedEnd != octetText.data() + octetText.size() || octet > 255)
			return false;

		octetStart = end + 1;
	}

	return true;
}

bool IsPotentialIPv4Address(std::string_view host)
{
	return std::ranges::all_of(host, [](char value) {
		const unsigned char character = static_cast<unsigned char>(value);
		return std::isdigit(character) != 0 || value == '.';
	});
}

bool IsValidIPv6Address(std::string_view host)
{
	asio::error_code error;
	asio::ip::make_address_v6(host, error);
	return !error;
}

tl::expected<uint16_t, std::string> ParsePort(std::string_view portText)
{
	if (portText.empty())
		return tl::make_unexpected(std::string("Authoritative server endpoint is missing a port."));

	uint32_t port = 0;
	const auto [end, error] = std::from_chars(portText.data(), portText.data() + portText.size(), port);
	if (error != std::errc() || end != portText.data() + portText.size())
		return tl::make_unexpected(std::string("Authoritative server endpoint has an invalid port."));
	if (port == 0 || port > std::numeric_limits<uint16_t>::max())
		return tl::make_unexpected(std::string("Authoritative server endpoint port is outside the valid range."));

	return static_cast<uint16_t>(port);
}

} // namespace

ServerBackedRuntimeConfiguration &GetServerBackedRuntimeConfiguration()
{
	static ServerBackedRuntimeConfiguration configuration;
	return configuration;
}

tl::expected<ServerBackedRuntimeConfiguration, std::string> ParseServerEndpoint(std::string_view endpoint)
{
	if (endpoint.empty())
		return tl::make_unexpected(std::string("Authoritative server endpoint is empty."));

	std::string_view host;
	std::string_view portText;
	if (endpoint.front() == '[') {
		const std::size_t closingBracket = endpoint.find(']');
		if (closingBracket == std::string_view::npos || closingBracket == 1
		    || closingBracket + 1 >= endpoint.size() || endpoint[closingBracket + 1] != ':') {
			return tl::make_unexpected(std::string("Authoritative server IPv6 endpoint must use [address]:port."));
		}

		host = endpoint.substr(1, closingBracket - 1);
		portText = endpoint.substr(closingBracket + 2);
		if (!IsValidIPv6Address(host))
			return tl::make_unexpected(std::string("Authoritative server endpoint has an invalid IPv6 address."));
	} else {
		const std::size_t separator = endpoint.find(':');
		if (separator == std::string_view::npos)
			return tl::make_unexpected(std::string("Authoritative server endpoint is missing a port."));
		if (separator == 0 || endpoint.find(':', separator + 1) != std::string_view::npos)
			return tl::make_unexpected(std::string("Authoritative server endpoint is malformed."));

		host = endpoint.substr(0, separator);
		portText = endpoint.substr(separator + 1);
		if (IsPotentialIPv4Address(host)) {
			if (!IsValidIPv4Address(host))
				return tl::make_unexpected(std::string("Authoritative server endpoint has an invalid IPv4 address."));
		} else if (!IsValidHostname(host)) {
			return tl::make_unexpected(std::string("Authoritative server endpoint has an invalid hostname."));
		}
	}

	auto port = ParsePort(portText);
	if (!port.has_value())
		return tl::make_unexpected(port.error());

	return ServerBackedRuntimeConfiguration {
		.mode = ServerBackedRuntimeConfiguration::GameMode::Authoritative,
		.enabled = true,
		.host = std::string(host),
		.port = *port,
	};
}

} // namespace devilution::authoritative
