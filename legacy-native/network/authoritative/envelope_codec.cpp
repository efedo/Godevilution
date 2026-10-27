/**
 * @file network/authoritative/envelope_codec.cpp
 *
 * Implementation of authoritative transport framing.
 */

#include "network/authoritative/envelope_codec.hpp"

#include <array>
#include <chrono>
#include <thread>

#include <asio/read.hpp>
#include <asio/write.hpp>

namespace devilution::authoritative {
namespace {

constexpr size_t HeaderSize = sizeof(uint32_t);

std::string SocketError(std::string_view operation, const asio::error_code &error)
{
	return std::string(operation) + ": " + error.message();
}

bool IsValidLength(uint32_t length)
{
	return length > 0 && length <= EnvelopeCodec::MaxEnvelopeBytes;
}

template <typename Buffer>
std::size_t ReadUntil(asio::ip::tcp::socket &socket, Buffer &buffer, std::size_t size, std::chrono::steady_clock::time_point deadline, asio::error_code &error)
{
	std::size_t offset = 0;
	while (offset < size) {
		if (std::chrono::steady_clock::now() >= deadline) {
			error = asio::error::timed_out;
			return offset;
		}
		const std::size_t read = socket.read_some(asio::buffer(static_cast<uint8_t *>(buffer.data()) + offset, size - offset), error);
		if (!error) {
			offset += read;
			continue;
		}
		if (error == asio::error::would_block || error == asio::error::try_again) {
			error.clear();
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
			continue;
		}
		return offset;
	}
	return offset;
}

template <typename Buffer>
std::size_t WriteUntil(asio::ip::tcp::socket &socket, Buffer buffer, std::size_t size, std::chrono::steady_clock::time_point deadline, asio::error_code &error)
{
	std::size_t offset = 0;
	while (offset < size) {
		if (std::chrono::steady_clock::now() >= deadline) {
			error = asio::error::timed_out;
			return offset;
		}
		const std::size_t written = socket.write_some(asio::buffer(static_cast<const uint8_t *>(buffer.data()) + offset, size - offset), error);
		if (!error) {
			offset += written;
			continue;
		}
		if (error == asio::error::would_block || error == asio::error::try_again) {
			error.clear();
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
			continue;
		}
		return offset;
	}
	return offset;
}

} // namespace

EnvelopeCodec::WriteResult EnvelopeCodec::Write(asio::ip::tcp::socket &socket, std::span<const uint8_t> payload)
{
	if (payload.size() == 0 || payload.size() > MaxEnvelopeBytes)
		return tl::make_unexpected(std::string("Envelope length is outside the allowed range."));
	const auto length = static_cast<uint32_t>(payload.size());

	std::array<uint8_t, HeaderSize> header {
		static_cast<uint8_t>(length),
		static_cast<uint8_t>(length >> 8),
		static_cast<uint8_t>(length >> 16),
		static_cast<uint8_t>(length >> 24),
	};
	asio::error_code error;
	asio::write(socket, asio::buffer(header), error);
	if (error)
		return tl::make_unexpected(SocketError("Writing envelope header", error));
	asio::write(socket, asio::buffer(payload.data(), payload.size()), error);
	if (error)
		return tl::make_unexpected(SocketError("Writing envelope payload", error));
	return {};
}

EnvelopeCodec::WriteResult EnvelopeCodec::Write(asio::ip::tcp::socket &socket, std::span<const uint8_t> payload, std::chrono::steady_clock::time_point deadline)
{
	if (payload.size() == 0 || payload.size() > MaxEnvelopeBytes)
		return tl::make_unexpected(std::string("Envelope length is outside the allowed range."));
	const auto length = static_cast<uint32_t>(payload.size());
	std::array<uint8_t, HeaderSize> header {
		static_cast<uint8_t>(length),
		static_cast<uint8_t>(length >> 8),
		static_cast<uint8_t>(length >> 16),
		static_cast<uint8_t>(length >> 24),
	};
	asio::error_code modeError;
	const bool wasNonBlocking = socket.non_blocking();
	socket.non_blocking(true, modeError);
	if (modeError)
		return tl::make_unexpected(SocketError("Configuring envelope socket", modeError));
	asio::error_code error;
	const std::size_t headerWritten = WriteUntil(socket, header, header.size(), deadline, error);
	if (headerWritten != header.size()) {
		socket.non_blocking(wasNonBlocking, modeError);
		if (modeError)
			return tl::make_unexpected(SocketError("Restoring envelope socket", modeError));
		return tl::make_unexpected(error == asio::error::timed_out
		        ? std::string("Timed out writing envelope.")
		        : SocketError("Writing envelope header", error));
	}
	const std::size_t payloadWritten = WriteUntil(socket, payload, payload.size(), deadline, error);
	socket.non_blocking(wasNonBlocking, modeError);
	if (modeError)
		return tl::make_unexpected(SocketError("Restoring envelope socket", modeError));
	if (payloadWritten != payload.size())
		return tl::make_unexpected(error == asio::error::timed_out
		        ? std::string("Timed out writing envelope.")
		        : SocketError("Writing envelope payload", error));
	if (modeError)
		return tl::make_unexpected(SocketError("Restoring envelope socket", modeError));
	return {};
}

EnvelopeCodec::ReadResult EnvelopeCodec::Read(asio::ip::tcp::socket &socket)
{
	std::array<uint8_t, HeaderSize> header {};
	asio::error_code error;
	const size_t bytesRead = asio::read(socket, asio::buffer(header), error);
	if (error == asio::error::eof && bytesRead == 0)
		return std::optional<Payload> {};
	if (error)
		return tl::make_unexpected(bytesRead == 0
		        ? SocketError("Reading envelope header", error)
		        : "The envelope header was truncated.");

	const uint32_t length = static_cast<uint32_t>(header[0])
	    | (static_cast<uint32_t>(header[1]) << 8)
	    | (static_cast<uint32_t>(header[2]) << 16)
	    | (static_cast<uint32_t>(header[3]) << 24);
	if (!IsValidLength(length))
		return tl::make_unexpected(std::string("Envelope length is outside the allowed range."));

	Payload payload(length);
	asio::read(socket, asio::buffer(payload), error);
	if (error)
		return tl::make_unexpected(error == asio::error::eof
		        ? "The envelope payload was truncated."
		        : SocketError("Reading envelope payload", error));
	return std::optional<Payload> { std::move(payload) };
}

EnvelopeCodec::ReadResult EnvelopeCodec::Read(asio::ip::tcp::socket &socket, std::chrono::steady_clock::time_point deadline)
{
	const bool wasNonBlocking = socket.non_blocking();
	asio::error_code modeError;
	socket.non_blocking(true, modeError);
	if (modeError)
		return tl::make_unexpected(SocketError("Configuring envelope socket", modeError));
	std::array<uint8_t, HeaderSize> header {};
	asio::error_code error;
	const std::size_t headerRead = ReadUntil(socket, header, header.size(), deadline, error);
	if (headerRead != header.size()) {
		socket.non_blocking(wasNonBlocking, modeError);
		if (modeError)
			return tl::make_unexpected(SocketError("Restoring envelope socket", modeError));
		if (error == asio::error::eof && headerRead == 0)
			return std::optional<Payload> {};
		return tl::make_unexpected(error == asio::error::timed_out
		        ? std::string("Timed out reading envelope.")
		        : error == asio::error::eof ? std::string("The envelope header was truncated.")
		                                    : SocketError("Reading envelope", error));
	}

	const uint32_t length = static_cast<uint32_t>(header[0])
	    | (static_cast<uint32_t>(header[1]) << 8)
	    | (static_cast<uint32_t>(header[2]) << 16)
	    | (static_cast<uint32_t>(header[3]) << 24);
	if (!IsValidLength(length)) {
		socket.non_blocking(wasNonBlocking, modeError);
		if (modeError)
			return tl::make_unexpected(SocketError("Restoring envelope socket", modeError));
		return tl::make_unexpected(std::string("Envelope length is outside the allowed range."));
	}

	Payload payload(length);
	error.clear();
	const std::size_t payloadRead = ReadUntil(socket, payload, payload.size(), deadline, error);
	socket.non_blocking(wasNonBlocking, modeError);
	if (modeError)
		return tl::make_unexpected(SocketError("Restoring envelope socket", modeError));
	if (payloadRead != payload.size())
		return tl::make_unexpected(error == asio::error::timed_out
		        ? std::string("Timed out reading envelope.")
		        : error == asio::error::eof ? std::string("The envelope payload was truncated.")
		                                    : SocketError("Reading envelope payload", error));
	return std::optional<Payload> { std::move(payload) };
}

} // namespace devilution::authoritative
