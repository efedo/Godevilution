#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <exception>
#include <functional>
#include <optional>
#include <stdexcept>
#include <thread>
#include <vector>

#include <asio/ip/tcp.hpp>
#include <asio/read.hpp>
#include <asio/write.hpp>

#include <gtest/gtest.h>

#include "network/authoritative/envelope_codec.hpp"

namespace devilution::authoritative {
namespace {

static_assert(EnvelopeCodec::MaxEnvelopeBytes == MaximumEnvelopeBytes);
static_assert(NativeMaximumPlayerCount == 4);
static_assert(NativeMaximumMonsterCount == 200);
static_assert(NativeMaximumObjectCount == 127);
static_assert(NativeMaximumItemCount == 127);
static_assert(NativeMaximumWarpTriggerCount == 7);
static_assert(NativeMaximumPortalCount == 4);
static_assert(NativeMaximumWidth == 112);
static_assert(NativeMaximumHeight == 112);
static_assert(NativeMaximumMegaWidth == 40);
static_assert(NativeMaximumMegaHeight == 40);
static_assert(NativeMaximumDunObjectLayerId == 146);
static_assert(NativeMaximumDunPieceId == 1378);
static_assert(NativeMaximumObjectId == 108);
static_assert(NativeMaximumQuestId == 23);
static_assert(MaximumNativeVisitedLevelCount == 256);
static_assert(MaximumNativeMonsterDeathAnimationTicks == 1024);
static_assert(MaximumNativeMonsterAiTimerTicks == 1024);
static_assert(MaximumNativeMonsterShortTimerTicks == 64);
static_assert(MaximumNativeMonsterRetreatTicks == 64);
static_assert(MaximumNativeMonsterExperienceReward == 1'000'000);
static_assert(MaximumNativeMonsterToHit == 255);
static_assert(MaximumNativeMonsterIntelligence == 255);
static_assert(MaximumNativeMonsterDirection == 7);
static_assert(MaximumNativeMonsterPackSize == 255);
static_assert(MaximumNativeMonsterActiveTicks == 255);
static_assert(MaximumNativeMonsterMovementTicks == 255);
static_assert(MinimumNativeMonsterMovementMode == -1);
static_assert(MaximumNativeMonsterMovementMode == 3);
static_assert(MinimumNativeMonsterMovementTurn == 0);
static_assert(MaximumNativeMonsterMovementTurn == 1);
static_assert(MaximumNativeMonsterFallenStandAnimationFrame == 127);
static_assert(MaximumNativeMonsterFallenStandAnimationTick == 127);
static_assert(MaximumNativeMonsterFallenRallyTicks == 4096);
static_assert(MinimumNativeMonsterResistance == -100);
static_assert(MaximumNativeMonsterResistance == 100);
static_assert(MaximumStableSpellId == 255);
static_assert(MaximumNativeSpellId == 51);
static_assert(MaximumNativeSpellLevel == 15);
static_assert(MaximumMemorizedSpellCount == 51);
static_assert(MaximumNativeCharacterLevel == 50);
static_assert(MaximumNativeDiabloKillLevel == 255);
static_assert(MaximumNativeReflectCharges == 65535);
static_assert(MinimumNativeLightRadius == 2);
static_assert(MaximumNativeLightRadius == 15);
static_assert(MaximumQuestTownerInteractionCount == 1024);
static_assert(MaximumTownerGreetingCount == 256);

using asio::ip::tcp;

struct LoopbackSocketPair {
	asio::io_context io;
	tcp::acceptor acceptor { io, { asio::ip::address_v4::loopback(), 0 } };
	tcp::socket client { io };
	tcp::socket server { io };

	explicit LoopbackSocketPair(std::optional<int> constrainedBuffer = std::nullopt)
	{
		if (constrainedBuffer.has_value()) {
			asio::error_code error;
			client.open(tcp::v4(), error);
			if (error)
				throw std::runtime_error(error.message());
			client.set_option(asio::socket_base::send_buffer_size(*constrainedBuffer), error);
			if (error)
				throw std::runtime_error(error.message());
			acceptor.set_option(asio::socket_base::receive_buffer_size(*constrainedBuffer), error);
			if (error)
				throw std::runtime_error(error.message());
		}
		client.connect({ asio::ip::address_v4::loopback(), acceptor.local_endpoint().port() });
		acceptor.accept(server);
		if (constrainedBuffer.has_value()) {
			asio::error_code error;
			server.set_option(asio::socket_base::receive_buffer_size(*constrainedBuffer), error);
			if (error)
				throw std::runtime_error(error.message());
		}
	}
};

bool WriteBytesWithin(
    tcp::socket &socket,
    const uint8_t *bytes,
    std::size_t size,
    std::chrono::steady_clock::time_point deadline)
{
	asio::error_code modeError;
	const bool wasNonBlocking = socket.non_blocking();
	socket.non_blocking(true, modeError);
	if (modeError)
		return false;
	std::size_t offset = 0;
	while (offset < size) {
		asio::error_code error;
		const std::size_t written = socket.write_some(asio::buffer(bytes + offset, size - offset), error);
		if (!error) {
			offset += written;
			continue;
		}
		if (error != asio::error::would_block && error != asio::error::try_again)
			break;
		if (std::chrono::steady_clock::now() >= deadline)
			break;
		std::this_thread::sleep_for(std::chrono::milliseconds(1));
	}
	socket.non_blocking(wasNonBlocking, modeError);
	return offset == size && !modeError;
}

std::size_t ReadSomeWithin(
    tcp::socket &socket,
    uint8_t *bytes,
    std::size_t size,
    std::chrono::steady_clock::time_point deadline)
{
	asio::error_code modeError;
	const bool wasNonBlocking = socket.non_blocking();
	socket.non_blocking(true, modeError);
	if (modeError)
		return 0;
	std::size_t read = 0;
	while (read == 0 && std::chrono::steady_clock::now() < deadline) {
		asio::error_code error;
		read = socket.read_some(asio::buffer(bytes, size), error);
		if (!error || error == asio::error::eof)
			break;
		if (error != asio::error::would_block && error != asio::error::try_again)
			break;
		std::this_thread::sleep_for(std::chrono::milliseconds(1));
	}
	socket.non_blocking(wasNonBlocking, modeError);
	return read;
}

class ScopedPeerThread {
public:
	explicit ScopedPeerThread(std::function<void()> function)
	    : thread_([this, function = std::move(function)]() mutable {
		    try {
			    function();
		    } catch (...) {
			    error_ = std::current_exception();
		    }
	    })
	{
	}

	~ScopedPeerThread()
	{
		Join();
	}

	ScopedPeerThread(const ScopedPeerThread &) = delete;
	ScopedPeerThread &operator=(const ScopedPeerThread &) = delete;

	void Join()
	{
		if (thread_.joinable())
			thread_.join();
	}

	[[nodiscard]] bool Failed() const noexcept
	{
		return error_ != nullptr;
	}

private:
	std::exception_ptr error_;
	std::thread thread_;
};

TEST(AuthoritativeEnvelopeCodec, WritesAndReadsLengthPrefixedPayload)
{
	LoopbackSocketPair sockets;
	const std::vector<uint8_t> expected { 0x08, 0x96, 0x01 };

	ASSERT_TRUE(EnvelopeCodec::Write(sockets.client, expected).has_value());
	auto actual = EnvelopeCodec::Read(sockets.server);

	ASSERT_TRUE(actual.has_value());
	ASSERT_TRUE(actual->has_value());
	EXPECT_EQ(**actual, expected);
}

TEST(AuthoritativeEnvelopeCodec, ReadsPayloadWhenServerFragmentsTheFrame)
{
	LoopbackSocketPair sockets;
	const std::array<uint8_t, 7> frame { 3, 0, 0, 0, 0x08, 0x96, 0x01 };
	std::atomic_bool writerFailed = false;

	ScopedPeerThread writer([&]() {
		const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(1);
		for (uint8_t byte : frame) {
			if (!WriteBytesWithin(sockets.server, &byte, 1, deadline)) {
				writerFailed = true;
				return;
			}
		}
		sockets.server.close();
	});

	auto actual = EnvelopeCodec::Read(sockets.client, std::chrono::steady_clock::now() + std::chrono::seconds(1));
	writer.Join();

	EXPECT_FALSE(writer.Failed());
	EXPECT_FALSE(writerFailed);
	ASSERT_TRUE(actual.has_value());
	ASSERT_TRUE(actual->has_value());
	EXPECT_EQ(**actual, std::vector<uint8_t>({ 0x08, 0x96, 0x01 }));
}

TEST(AuthoritativeEnvelopeCodec, CleanEndOfStreamReturnsNoPayload)
{
	LoopbackSocketPair sockets;
	sockets.server.close();

	auto actual = EnvelopeCodec::Read(sockets.client);

	ASSERT_TRUE(actual.has_value());
	EXPECT_FALSE(actual->has_value());
}

TEST(AuthoritativeEnvelopeCodec, RejectsTruncatedPayload)
{
	LoopbackSocketPair sockets;
	const std::array<uint8_t, 6> frame { 3, 0, 0, 0, 0x08, 0x96 };
	asio::write(sockets.server, asio::buffer(frame));
	sockets.server.close();

	auto actual = EnvelopeCodec::Read(sockets.client);

	EXPECT_FALSE(actual.has_value());
	EXPECT_EQ(actual.error(), "The envelope payload was truncated.");
}

TEST(AuthoritativeEnvelopeCodec, RejectsZeroLengthPayload)
{
	LoopbackSocketPair zeroLength;
	const std::array<uint8_t, 4> zeroHeader { 0, 0, 0, 0 };
	asio::write(zeroLength.server, asio::buffer(zeroHeader));
	EXPECT_EQ(EnvelopeCodec::Read(zeroLength.client).error(), "Envelope length is outside the allowed range.");
}

TEST(AuthoritativeEnvelopeCodec, RejectsOversizedPayload)
{
	LoopbackSocketPair oversized;
	const std::array<uint8_t, 4> oversizedHeader { 1, 0, 16, 0 };
	asio::write(oversized.server, asio::buffer(oversizedHeader));
	EXPECT_EQ(EnvelopeCodec::Read(oversized.client).error(), "Envelope length is outside the allowed range.");
}

TEST(AuthoritativeEnvelopeCodec, RejectsPayloadLargerThanTheProtocolLimit)
{
	LoopbackSocketPair sockets;
	std::vector<uint8_t> payload(EnvelopeCodec::MaxEnvelopeBytes + 1);

	EXPECT_EQ(EnvelopeCodec::Write(sockets.client, payload).error(), "Envelope length is outside the allowed range.");
}

TEST(AuthoritativeEnvelopeCodec, BoundedReadUsesOneDeadlineAcrossSlowFragments)
{
	LoopbackSocketPair sockets;
	const std::array<uint8_t, 7> frame { 3, 0, 0, 0, 0x08, 0x96, 0x01 };
	std::atomic_bool writerFailed = false;
	ScopedPeerThread writer([&]() {
		const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(1);
		if (!WriteBytesWithin(sockets.server, frame.data(), 1, deadline)) {
			writerFailed = true;
			return;
		}
		std::this_thread::sleep_for(std::chrono::milliseconds(100));
		if (!WriteBytesWithin(sockets.server, frame.data() + 1, frame.size() - 1, deadline))
			writerFailed = true;
	});

	auto actual = EnvelopeCodec::Read(sockets.client, std::chrono::steady_clock::now() + std::chrono::milliseconds(20));
	writer.Join();
	EXPECT_FALSE(writer.Failed());
	EXPECT_FALSE(writerFailed);
	EXPECT_FALSE(actual.has_value());
	EXPECT_EQ(actual.error(), "Timed out reading envelope.");
	sockets.client.close();
	sockets.server.close();
}

TEST(AuthoritativeEnvelopeCodec, BoundedReadPreservesFramePayload)
{
	LoopbackSocketPair sockets;
	const std::vector<uint8_t> expected { 0x08, 0x96, 0x01 };
	ASSERT_TRUE(EnvelopeCodec::Write(sockets.server, expected).has_value());
	auto actual = EnvelopeCodec::Read(sockets.client, std::chrono::steady_clock::now() + std::chrono::seconds(1));
	ASSERT_TRUE(actual.has_value());
	ASSERT_TRUE(actual->has_value());
	EXPECT_EQ(**actual, expected);
}

TEST(AuthoritativeEnvelopeCodec, ExpiredDeadlineReturnsBeforeReadingAndRestoresBlockingMode)
{
	LoopbackSocketPair sockets;
	const bool originalNonBlocking = sockets.client.non_blocking();
	auto actual = EnvelopeCodec::Read(sockets.client, std::chrono::steady_clock::now() - std::chrono::milliseconds(1));
	if (actual.has_value())
		ADD_FAILURE() << "An expired read deadline unexpectedly returned a payload.";
	else
		EXPECT_EQ(actual.error(), "Timed out reading envelope.");
	EXPECT_EQ(sockets.client.non_blocking(), originalNonBlocking);
	sockets.client.close();
	sockets.server.close();
}

TEST(AuthoritativeEnvelopeCodec, BoundedReadPreservesAnInitiallyNonBlockingMode)
{
	LoopbackSocketPair sockets;
	asio::error_code modeError;
	sockets.client.non_blocking(true, modeError);
	ASSERT_FALSE(modeError);
	const std::vector<uint8_t> expected { 0x08, 0x96, 0x01 };
	ASSERT_TRUE(EnvelopeCodec::Write(sockets.server, expected).has_value());
	auto actual = EnvelopeCodec::Read(sockets.client, std::chrono::steady_clock::now() + std::chrono::seconds(1));
	ASSERT_TRUE(actual.has_value());
	ASSERT_TRUE(actual->has_value());
	EXPECT_EQ(**actual, expected);
	EXPECT_TRUE(sockets.client.non_blocking());
	sockets.client.close();
	sockets.server.close();
}

TEST(AuthoritativeEnvelopeCodec, BoundedWriteUsesOneDeadlineWhenPeerDoesNotRead)
{
	LoopbackSocketPair sockets { 1024 };
	const std::vector<uint8_t> payload(EnvelopeCodec::MaxEnvelopeBytes, 0x5A);
	// Windows loopback may accept a complete frame despite SO_SNDBUF/RCVBUF.
	// Saturate the path with valid frames, not filler, under a finite total bound.
	bool observedTimeout = false;
	const auto saturationDeadline = std::chrono::steady_clock::now() + std::chrono::seconds(2);
	for (unsigned attempt = 0; attempt < 64 && std::chrono::steady_clock::now() < saturationDeadline; ++attempt) {
		auto result = EnvelopeCodec::Write(sockets.client, payload,
		    std::min(saturationDeadline, std::chrono::steady_clock::now() + std::chrono::milliseconds(20)));
		if (!result.has_value()) {
			EXPECT_EQ(result.error(), "Timed out writing envelope.");
			observedTimeout = result.error() == "Timed out writing envelope.";
			break;
		}
	}
	EXPECT_TRUE(observedTimeout) << "The non-reading peer never produced backpressure within the bounded frame budget.";
	std::array<uint8_t, 32> prefix {};
	const std::size_t received = ReadSomeWithin(
	    sockets.server,
	    prefix.data(),
	    prefix.size(),
	    std::chrono::steady_clock::now() + std::chrono::milliseconds(100));
	ASSERT_GE(received, 4U);
	if (received >= 4) {
		const uint32_t announcedLength = static_cast<uint32_t>(prefix[0])
		    | (static_cast<uint32_t>(prefix[1]) << 8)
		    | (static_cast<uint32_t>(prefix[2]) << 16)
		    | (static_cast<uint32_t>(prefix[3]) << 24);
		EXPECT_EQ(announcedLength, payload.size());
		for (std::size_t index = 4; index < received; ++index)
			EXPECT_EQ(prefix[index], 0x5A);
	}
	EXPECT_EQ(sockets.client.non_blocking(), false);
	sockets.client.close();
	sockets.server.close();
}

TEST(AuthoritativeEnvelopeCodec, ExpiredWriteReturnsBeforeSendingBytes)
{
	LoopbackSocketPair sockets;
	const bool originalNonBlocking = sockets.client.non_blocking();
	const std::vector<uint8_t> payload { 0x08, 0x96, 0x01 };
	auto result = EnvelopeCodec::Write(
	    sockets.client,
	    payload,
	    std::chrono::steady_clock::now() - std::chrono::milliseconds(1));
	if (result.has_value())
		ADD_FAILURE() << "An expired write deadline unexpectedly sent a payload.";
	else
		EXPECT_EQ(result.error(), "Timed out writing envelope.");
	std::array<uint8_t, 8> received {};
	EXPECT_EQ(
	    ReadSomeWithin(sockets.server, received.data(), received.size(), std::chrono::steady_clock::now() + std::chrono::milliseconds(20)),
	    0U);
	EXPECT_EQ(sockets.client.non_blocking(), originalNonBlocking);
	sockets.client.close();
	sockets.server.close();
}

TEST(AuthoritativeEnvelopeCodec, BoundedWritePreservesPayloadAndInitialNonBlockingMode)
{
	LoopbackSocketPair sockets;
	asio::error_code modeError;
	sockets.client.non_blocking(true, modeError);
	ASSERT_FALSE(modeError);
	const std::vector<uint8_t> expected { 0x08, 0x96, 0x01 };
	auto result = EnvelopeCodec::Write(
	    sockets.client,
	    expected,
	    std::chrono::steady_clock::now() + std::chrono::seconds(1));
	ASSERT_TRUE(result.has_value());
	EXPECT_TRUE(sockets.client.non_blocking());
	auto actual = EnvelopeCodec::Read(sockets.server, std::chrono::steady_clock::now() + std::chrono::seconds(1));
	ASSERT_TRUE(actual.has_value());
	ASSERT_TRUE(actual->has_value());
	EXPECT_EQ(**actual, expected);
	sockets.client.close();
	sockets.server.close();
}

} // namespace
} // namespace devilution::authoritative
