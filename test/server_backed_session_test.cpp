#include <atomic>
#include <span>
#include <string>
#include <thread>

#include <asio/ip/tcp.hpp>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/envelope_codec.hpp"
#include "network/authoritative/server_backed_session.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
using asio::ip::tcp;
namespace {

void WriteEnvelope(tcp::socket &socket, const protocol::Envelope &envelope)
{
	const std::string payload = envelope.SerializeAsString();
	ASSERT_TRUE(EnvelopeCodec::Write(socket, std::span<const uint8_t>(
	                                      reinterpret_cast<const uint8_t *>(payload.data()), payload.size()))
	                .has_value());
}

TEST(ServerBackedSession, ConnectsOpensVendorAndAppliesAuthoritativeState)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool observed = false;
	std::thread server([&]() {
		tcp::socket socket(serverIo);
		acceptor.accept(socket);
		auto helloPayload = EnvelopeCodec::Read(socket);
		if (!helloPayload.has_value() || !helloPayload->has_value())
			return;
		protocol::Envelope hello;
		if (!hello.ParseFromArray(helloPayload->value().data(), static_cast<int>(helloPayload->value().size())))
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		WriteEnvelope(socket, serverHello);
		protocol::Envelope initial;
		initial.mutable_snapshot()->add_players()->set_entity_id(7);
		auto *activeQuest = initial.mutable_snapshot()->add_quests();
		activeQuest->set_quest_id(3011);
		activeQuest->set_required_progress(1);
		activeQuest->set_turn_in_towner_id(0);
		auto *completedQuest = initial.mutable_snapshot()->add_quests();
		completedQuest->set_quest_id(3002);
		completedQuest->set_required_progress(1);
		completedQuest->set_completed(true);
		completedQuest->set_turn_in_towner_id(0);
		WriteEnvelope(socket, initial);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).open_store_requested().store_id() != 1)
			return;
		protocol::Envelope ack;
		ack.mutable_command_ack()->add_results()->set_client_sequence(1);
		ack.mutable_command_ack()->mutable_results(0)->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		WriteEnvelope(socket, ack);
		protocol::Envelope snapshot;
		auto *player = snapshot.mutable_snapshot()->add_players();
		player->set_entity_id(7);
		player->set_active_store_id(1);
		snapshot.mutable_snapshot()->mutable_active_store()->set_store_id(1);
		auto *item = snapshot.mutable_snapshot()->mutable_active_store()->add_items();
		item->set_store_slot(0);
		item->set_item_seed(42);
		item->set_price(75);
		item->mutable_state()->set_item_type(1);
		WriteEnvelope(socket, snapshot);
		observed = true;
	});

	ServerBackedSession::Configuration configuration {
		.client = {
			.host = "127.0.0.1",
			.port = acceptor.local_endpoint().port(),
			.clientBuildId = "client",
			.protocolSchemaVersion = "1",
			.contentManifestHash = "content",
		},
	};
	auto session = ServerBackedSession::Connect(configuration);
	ASSERT_TRUE(session.has_value()) << session.error();
	EXPECT_EQ((*session)->ActiveQuestForTowner(0), std::optional<uint32_t> { 3011 });
	EXPECT_FALSE((*session)->ActiveQuestForTowner(1).has_value());
	auto open = (*session)->OpenVendor(1, 10, 0);
	ASSERT_TRUE(open.has_value()) << open.error();
	EXPECT_EQ((*session)->EntityId(), 7U);
	EXPECT_EQ((*session)->VendorState().Phase(), ServerBackedVendorPhase::Ready);
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	const auto *item = (*session)->VendorState().FindItem(1, 0);
	ASSERT_NE(item, nullptr);
	EXPECT_EQ(item->itemSeed, 42U);
	(*session)->Close();
	server.join();
	EXPECT_TRUE(observed);
}

TEST(ServerBackedSession, DoesNotTreatRejectedCommandAsLocalSuccess)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool observed = false;
	std::thread server([&]() {
		tcp::socket socket(serverIo);
		acceptor.accept(socket);
		auto helloPayload = EnvelopeCodec::Read(socket);
		if (!helloPayload.has_value() || !helloPayload->has_value())
			return;
		protocol::Envelope hello;
		if (!hello.ParseFromArray(helloPayload->value().data(), static_cast<int>(helloPayload->value().size())))
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		WriteEnvelope(socket, serverHello);
		protocol::Envelope initial;
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		auto *initialItem = initialPlayer->add_inventory();
		initialItem->set_item_seed(42);
		initialItem->mutable_state()->set_stable_item_id(700);
		initialItem->mutable_state()->set_item_type(1);
		WriteEnvelope(socket, initial);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.command_batch().commands_size() != 1
		    || !command.command_batch().commands(0).has_sell_item_requested()
		    || command.command_batch().commands(0).sell_item_requested().item().location() != protocol::PLAYER_ITEM_LOCATION_INVENTORY
		    || command.command_batch().commands(0).sell_item_requested().item().slot() != 0
		    || command.command_batch().commands(0).sell_item_requested().item().stable_item_id() != 700)
			return;
		protocol::Envelope ack;
		auto *result = ack.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_REJECTED);
		WriteEnvelope(socket, ack);
		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->add_players()->set_entity_id(7);
		WriteEnvelope(socket, snapshot);
		observed = true;
	});

	ServerBackedSession::Configuration configuration {
		.client = {
			.host = "127.0.0.1",
			.port = acceptor.local_endpoint().port(),
			.clientBuildId = "client",
			.protocolSchemaVersion = "1",
			.contentManifestHash = "content",
		},
	};
	auto session = ServerBackedSession::Connect(configuration);
	ASSERT_TRUE(session.has_value()) << session.error();
	auto sell = (*session)->SellItem(0, 10, 0);
	ASSERT_TRUE(sell.has_value()) << sell.error();
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Rejected);
	(*session)->Close();
	server.join();
	EXPECT_TRUE(observed);
}

} // namespace
} // namespace devilution::authoritative
