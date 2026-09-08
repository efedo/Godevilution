#include <array>
#include <cstdint>
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
static_assert(MaximumNativeMonsterExperienceReward == 1'000'000);
static_assert(MaximumNativeMonsterToHit == 255);
static_assert(MaximumNativeMonsterIntelligence == 255);
static_assert(MaximumNativeMonsterDirection == 7);
static_assert(MaximumNativeMonsterPackSize == 255);
static_assert(MaximumNativeMonsterActiveTicks == 255);
static_assert(MaximumStableSpellId == 255);
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
	tcp::acceptor acceptor { io, { tcp::v4(), 0 } };
	tcp::socket client { io };
	tcp::socket server { io };

	LoopbackSocketPair()
	{
		client.connect({ asio::ip::address_v4::loopback(), acceptor.local_endpoint().port() });
		acceptor.accept(server);
	}
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

	std::thread writer([&]() {
		for (uint8_t byte : frame)
			asio::write(sockets.server, asio::buffer(&byte, 1));
		sockets.server.close();
	});

	auto actual = EnvelopeCodec::Read(sockets.client);
	writer.join();

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

} // namespace
} // namespace devilution::authoritative
