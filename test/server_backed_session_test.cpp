#include <array>
#include <atomic>
#include <chrono>
#include <exception>
#include <functional>
#include <limits>
#include <span>
#include <string>
#include <thread>
#include <utility>

#include <asio/ip/tcp.hpp>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/envelope_codec.hpp"
#include "network/authoritative/server_backed_session.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
using asio::ip::tcp;
namespace {

using SteadyClock = std::chrono::steady_clock;

constexpr std::chrono::milliseconds PeerIoTimeout { 2'000 };

bool AcceptWithin(tcp::acceptor &acceptor, tcp::socket &socket, SteadyClock::time_point deadline)
{
	asio::error_code error;
	acceptor.non_blocking(true, error);
	if (error)
		return false;
	for (;;) {
		acceptor.accept(socket, error);
		if (!error)
			break;
		if (error != asio::error::would_block && error != asio::error::try_again)
			return false;
		if (SteadyClock::now() >= deadline)
			return false;
		std::this_thread::sleep_for(std::chrono::milliseconds(1));
	}
	asio::error_code restoreError;
	acceptor.non_blocking(false, restoreError);
	socket.non_blocking(false, restoreError);
	return !restoreError;
}

bool ReadEnvelopeWithin(tcp::socket &socket, protocol::Envelope &envelope, SteadyClock::time_point deadline)
{
	auto payload = EnvelopeCodec::Read(socket, deadline);
	if (!payload.has_value() || !payload->has_value())
		return false;
	return envelope.ParseFromArray(payload->value().data(), static_cast<int>(payload->value().size()));
}

bool WriteEnvelopeWithin(tcp::socket &socket, const protocol::Envelope &envelope, SteadyClock::time_point deadline)
{
	const std::string payload = envelope.SerializeAsString();
	return EnvelopeCodec::Write(socket, std::span<const uint8_t>(reinterpret_cast<const uint8_t *>(payload.data()), payload.size()), deadline)
	    .has_value();
}

bool WaitForPeerClose(tcp::socket &socket, SteadyClock::time_point deadline)
{
	asio::error_code error;
	socket.non_blocking(true, error);
	if (error)
		return false;
	std::array<uint8_t, 1> buffer {};
	for (;;) {
		if (SteadyClock::now() >= deadline)
			return false;
		socket.read_some(asio::buffer(buffer), error);
		if (!error)
			continue;
		if (error == asio::error::eof || error == asio::error::connection_reset || error == asio::error::shut_down)
			return true;
		if (error != asio::error::would_block && error != asio::error::try_again)
			return false;
		std::this_thread::sleep_for(std::chrono::milliseconds(1));
	}
}

class PeerThread {
public:
	explicit PeerThread(std::function<void()> function)
	    : thread_([this, function = std::move(function)]() mutable {
		    try {
			    function();
		    } catch (...) {
			    error_ = std::current_exception();
		    }
	    })
	{
	}

	~PeerThread()
	{
		Join();
	}

	PeerThread(const PeerThread &) = delete;
	PeerThread &operator=(const PeerThread &) = delete;

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

void WriteEnvelope(tcp::socket &socket, const protocol::Envelope &envelope)
{
	(void)WriteEnvelopeWithin(socket, envelope, SteadyClock::now() + PeerIoTimeout);
}

TEST(ServerBackedSession, ConnectsOpensVendorAndAppliesAuthoritativeState)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		if (!AcceptWithin(acceptor, socket, deadline))
			return;
		protocol::Envelope hello;
		if (!ReadEnvelopeWithin(socket, hello, deadline))
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->add_visited_level_ids(0);
		initialPlayer->add_visited_level_ids(4);
		initialPlayer->add_visited_level_ids(21);
		auto *activeQuest = initial.mutable_snapshot()->add_quests();
		activeQuest->set_quest_id(3011);
		activeQuest->set_required_progress(1);
		activeQuest->set_turn_in_towner_id(0);
		activeQuest->set_completion_unique_identity("Defiler");
		auto *activationQuest = initial.mutable_snapshot()->add_quests();
		activationQuest->set_quest_id(3004);
		activationQuest->set_required_progress(1);
		activationQuest->set_initial(true);
		activationQuest->set_completion_unique_identity("Zhar");
		auto *completedQuest = initial.mutable_snapshot()->add_quests();
		completedQuest->set_quest_id(3002);
		completedQuest->set_required_progress(1);
		completedQuest->set_completed(true);
		completedQuest->set_variable_2(0);
		completedQuest->set_turn_in_towner_id(1);
		completedQuest->set_turn_in_on_completion(true);
		completedQuest->set_turn_in_completion_variable2(1);
		auto *dialogueQuest = initial.mutable_snapshot()->add_quests();
		dialogueQuest->set_quest_id(3017);
		dialogueQuest->set_required_progress(1);
		dialogueQuest->set_turn_in_towner_id(7);
		dialogueQuest->set_turn_in_message_id(274);
		dialogueQuest->set_turn_in_dialogue_only(true);
		auto *blockedDialogueQuest = initial.mutable_snapshot()->add_quests();
		blockedDialogueQuest->set_quest_id(3018);
		blockedDialogueQuest->set_required_progress(1);
		blockedDialogueQuest->set_turn_in_towner_id(12);
		blockedDialogueQuest->set_turn_in_message_id(274);
		blockedDialogueQuest->set_turn_in_dialogue_only(true);
		blockedDialogueQuest->set_turn_in_blocked_visited_level(21);
		auto *variableOneQuest = initial.mutable_snapshot()->add_quests();
		variableOneQuest->set_quest_id(3014);
		variableOneQuest->set_required_progress(1);
		variableOneQuest->set_completed(true);
		variableOneQuest->set_variable_1(0);
		variableOneQuest->set_turn_in_towner_id(8);
		variableOneQuest->set_turn_in_on_completion(true);
		variableOneQuest->set_turn_in_completion_required_variable1(0);
		variableOneQuest->set_turn_in_completion_variable1(2);
		auto *stagedTurnInQuest = initial.mutable_snapshot()->add_quests();
		stagedTurnInQuest->set_quest_id(3027);
		stagedTurnInQuest->set_required_progress(1);
		stagedTurnInQuest->set_variable_1(5);
		stagedTurnInQuest->set_turn_in_towner_id(9);
		stagedTurnInQuest->set_turn_in_progress_only(true);
		stagedTurnInQuest->set_turn_in_required_variable1(5);
		stagedTurnInQuest->set_turn_in_variable1(7);
		auto *variableGatedActivationQuest = initial.mutable_snapshot()->add_quests();
		variableGatedActivationQuest->set_quest_id(3020);
		variableGatedActivationQuest->set_required_progress(1);
		variableGatedActivationQuest->set_variable_2(0);
		auto *variableGatedActivation = initial.mutable_snapshot()->add_quest_towner_interactions();
		variableGatedActivation->set_interaction_id(300207);
		variableGatedActivation->set_quest_id(3020);
		variableGatedActivation->set_towner_id(11);
		variableGatedActivation->set_level_id(0);
		variableGatedActivation->set_position_x(10);
		variableGatedActivation->set_position_y(10);
		variableGatedActivation->set_kind(0);
		variableGatedActivation->set_required_variable2(0);
		variableGatedActivation->set_variable2(1);
		variableGatedActivation->set_message_id(115);
		variableGatedActivation->set_required_visited_level1(4);
		auto *unloggedDialogueQuest = initial.mutable_snapshot()->add_quests();
		unloggedDialogueQuest->set_quest_id(3025);
		unloggedDialogueQuest->set_required_progress(1);
		unloggedDialogueQuest->set_initial(false);
		unloggedDialogueQuest->set_logged(false);
		auto *unloggedDialogue = initial.mutable_snapshot()->add_quest_towner_interactions();
		unloggedDialogue->set_interaction_id(300210);
		unloggedDialogue->set_quest_id(3025);
		unloggedDialogue->set_towner_id(13);
		unloggedDialogue->set_level_id(0);
		unloggedDialogue->set_position_x(10);
		unloggedDialogue->set_position_y(10);
		unloggedDialogue->set_kind(3);
		unloggedDialogue->set_message_id(39);
		unloggedDialogue->set_consume_item(false);
		unloggedDialogue->set_require_unlogged(true);
		auto *loggedDialogueQuest = initial.mutable_snapshot()->add_quests();
		loggedDialogueQuest->set_quest_id(3026);
		loggedDialogueQuest->set_required_progress(1);
		loggedDialogueQuest->set_initial(false);
		loggedDialogueQuest->set_logged(true);
		auto *loggedDialogue = initial.mutable_snapshot()->add_quest_towner_interactions();
		loggedDialogue->set_interaction_id(300211);
		loggedDialogue->set_quest_id(3026);
		loggedDialogue->set_towner_id(14);
		loggedDialogue->set_level_id(0);
		loggedDialogue->set_position_x(10);
		loggedDialogue->set_position_y(10);
		loggedDialogue->set_kind(3);
		loggedDialogue->set_message_id(39);
		loggedDialogue->set_consume_item(false);
		loggedDialogue->set_require_unlogged(true);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;

		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).open_store_requested().store_id() != 1)
			return;
		protocol::Envelope ack;
		ack.mutable_command_ack()->add_results()->set_client_sequence(1);
		ack.mutable_command_ack()->mutable_results(0)->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		if (!WriteEnvelopeWithin(socket, ack, deadline))
			return;
		protocol::Envelope snapshot;
		auto *player = snapshot.mutable_snapshot()->add_players();
		player->set_entity_id(7);
		player->set_character_level(1);
		player->set_active_store_id(1);
		snapshot.mutable_snapshot()->mutable_active_store()->set_store_id(1);
		auto *item = snapshot.mutable_snapshot()->mutable_active_store()->add_items();
		item->set_store_slot(0);
		item->set_item_seed(42);
		item->set_price(75);
		item->mutable_state()->set_item_type(1);
		if (!WriteEnvelopeWithin(socket, snapshot, deadline))
			return;

		protocol::Envelope purchase;
		if (!ReadEnvelopeWithin(socket, purchase, deadline)
		    || purchase.command_batch().commands_size() != 1
		    || !purchase.command_batch().commands(0).has_purchase_requested()
		    || purchase.command_batch().commands(0).purchase_requested().store_id() != 1
		    || purchase.command_batch().commands(0).purchase_requested().store_slot() != 0)
			return;
		protocol::Envelope purchaseAck;
		auto *purchaseResult = purchaseAck.mutable_command_ack()->add_results();
		purchaseResult->set_client_sequence(2);
		purchaseResult->set_status(protocol::COMMAND_STATUS_DUPLICATE);
		purchaseResult->set_reject_reason(protocol::COMMAND_REJECT_REASON_TOO_LATE);
		if (!WriteEnvelopeWithin(socket, purchaseAck, deadline))
			return;
		protocol::Envelope purchaseSnapshot;
		auto *purchasePlayer = purchaseSnapshot.mutable_snapshot()->add_players();
		purchasePlayer->set_entity_id(7);
		purchasePlayer->set_character_level(1);
		purchasePlayer->set_active_store_id(1);
		purchaseSnapshot.mutable_snapshot()->mutable_active_store()->set_store_id(1);
		auto *purchaseItem = purchaseSnapshot.mutable_snapshot()->mutable_active_store()->add_items();
		purchaseItem->set_store_slot(0);
		purchaseItem->set_item_seed(42);
		purchaseItem->set_price(75);
		purchaseItem->mutable_state()->set_item_type(1);
		if (!WriteEnvelopeWithin(socket, purchaseSnapshot, deadline))
			return;

		protocol::Envelope close;
		if (!ReadEnvelopeWithin(socket, close, deadline)
		    || close.command_batch().commands_size() != 1
		    || !close.command_batch().commands(0).has_close_store_requested())
			return;
		protocol::Envelope closeAck;
		closeAck.mutable_command_ack()->add_results()->set_client_sequence(3);
		closeAck.mutable_command_ack()->mutable_results(0)->set_status(protocol::COMMAND_STATUS_DUPLICATE);
		if (!WriteEnvelopeWithin(socket, closeAck, deadline))
			return;
		protocol::Envelope closedSnapshot;
		auto *closedPlayer = closedSnapshot.mutable_snapshot()->add_players();
		closedPlayer->set_entity_id(7);
		closedPlayer->set_character_level(1);
		if (!WriteEnvelopeWithin(socket, closedSnapshot, deadline))
			return;
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
	EXPECT_EQ((*session)->ActiveQuestForTowner(1), std::optional<uint32_t> { 3002 });
	EXPECT_EQ((*session)->ActiveQuestForTowner(7), std::optional<uint32_t> { 3017 });
	EXPECT_FALSE((*session)->ActiveQuestForTowner(12).has_value());
	EXPECT_EQ((*session)->ActiveQuestForTowner(8), std::optional<uint32_t> { 3014 });
	EXPECT_EQ((*session)->ActiveQuestForTowner(9), std::optional<uint32_t> { 3027 });
	EXPECT_EQ((*session)->ActiveQuestForTowner(11), std::optional<uint32_t> { 3020 });
	EXPECT_EQ((*session)->ActiveQuestForTowner(13), std::optional<uint32_t> { 3025 });
	EXPECT_FALSE((*session)->ActiveQuestForTowner(14).has_value());
	EXPECT_EQ((*session)->ActiveQuestForUniqueIdentity("Defiler"), std::optional<uint32_t> { 3011 });
	EXPECT_EQ((*session)->ActiveQuestForUniqueIdentity("Zhar"), std::optional<uint32_t> { 3004 });
	EXPECT_FALSE((*session)->ActiveQuestForUniqueIdentity("Na-Krul").has_value());
	auto open = (*session)->OpenVendor(1, 10, 0);
	ASSERT_TRUE(open.has_value()) << open.error();
	EXPECT_EQ((*session)->EntityId(), 7U);
	EXPECT_EQ((*session)->VendorState().Phase(), ServerBackedVendorPhase::Ready);
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	const auto *item = (*session)->VendorState().FindItem(1, 0);
	ASSERT_NE(item, nullptr);
	EXPECT_EQ(item->itemSeed, 42U);
	auto purchase = (*session)->Purchase(1, 0, 11, 0);
	ASSERT_TRUE(purchase.has_value()) << purchase.error();
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Rejected);
	EXPECT_FALSE((*session)->VendorState().IsPurchasePending(1, 0));
	EXPECT_EQ((*session)->VendorState().PendingPurchaseCount(), 0U);
	EXPECT_EQ((*session)->VendorState().Phase(), ServerBackedVendorPhase::Ready);
	item = (*session)->VendorState().FindItem(1, 0);
	ASSERT_NE(item, nullptr);
	EXPECT_EQ(item->itemSeed, 42U);
	auto close = (*session)->CloseVendor(11, 0);
	ASSERT_TRUE(close.has_value()) << close.error();
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Duplicate);
	EXPECT_EQ((*session)->VendorState().Phase(), ServerBackedVendorPhase::AwaitingSnapshot);
	EXPECT_EQ((*session)->VendorState().Snapshot(), nullptr);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, TreatsDuplicateTooLateGameplayAsRejected)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		protocol::Envelope hello;
		if (!AcceptWithin(acceptor, socket, deadline)
		    || !ReadEnvelopeWithin(socket, hello, deadline)
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;

		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(10);
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->set_position_x(4);
		initialPlayer->set_position_y(5);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;

		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 1
		    || !command.command_batch().commands(0).has_move_requested()
		    || command.command_batch().commands(0).move_requested().direction_x() != 1)
			return;

		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_DUPLICATE);
		result->set_reject_reason(protocol::COMMAND_REJECT_REASON_TOO_LATE);
		if (!WriteEnvelopeWithin(socket, acknowledgement, deadline))
			return;
		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->set_tick(10);
		auto *snapshotPlayer = snapshot.mutable_snapshot()->add_players();
		snapshotPlayer->set_entity_id(7);
		snapshotPlayer->set_character_level(1);
		snapshotPlayer->set_position_x(4);
		snapshotPlayer->set_position_y(5);
		if (!WriteEnvelopeWithin(socket, snapshot, deadline))
			return;
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
	auto move = (*session)->Move(1, 0, 10, 0);
	ASSERT_TRUE(move.has_value()) << move.error();
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Rejected);
	ASSERT_NE((*session)->PlayerState().Snapshot(), nullptr);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionX, 4);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionY, 5);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, TerminalAckWithoutSnapshotRetiresTrackedCommand)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	std::atomic_int commandCount = 0;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		protocol::Envelope hello;
		if (!AcceptWithin(acceptor, socket, deadline)
		    || !ReadEnvelopeWithin(socket, hello, deadline)
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_IMMEDIATE);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(10);
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->set_position_x(4);
		initialPlayer->set_position_y(5);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;

		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 1
		    || !command.command_batch().commands(0).has_move_requested())
			return;
		++commandCount;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		if (!WriteEnvelopeWithin(socket, acknowledgement, deadline))
			return;
		// Deliberately close after the terminal ACK. The client must not retain
		// the command merely because its follow-up snapshot was unavailable.
		observed = true;
	});

	ServerBackedSession::Configuration configuration {
		.client = {
		    .host = "127.0.0.1",
		    .port = acceptor.local_endpoint().port(),
		    .clientBuildId = "client",
		    .protocolSchemaVersion = "1",
		    .contentManifestHash = "content",
		    .supportsClockGatedCommands = true,
		},
	};
	auto session = ServerBackedSession::Connect(configuration);
	ASSERT_TRUE(session.has_value()) << session.error();
	const auto move = (*session)->Move(1, 0, 11, 0);
	ASSERT_FALSE(move.has_value());
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	EXPECT_EQ((*session)->Client().PendingTrackedCommandCount(), 0U);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_EQ(commandCount, 1);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, ClockGatedTerminalAckWithoutSnapshotUsesOriginalDeadline)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	std::atomic_bool retired = false;
	std::atomic_int commandCount = 0;
	std::atomic<long long> closeElapsedMs = -1;
	PeerThread server([&]() {
		const auto peerDeadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		protocol::Envelope hello;
		if (!AcceptWithin(acceptor, socket, peerDeadline)
		    || !ReadEnvelopeWithin(socket, hello, peerDeadline)
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(socket, serverHello, peerDeadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(10);
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->set_position_x(4);
		initialPlayer->set_position_y(5);
		if (!WriteEnvelopeWithin(socket, initial, peerDeadline))
			return;

		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, peerDeadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 1
		    || !command.command_batch().commands(0).has_move_requested())
			return;
		++commandCount;
		// Consume most of the transaction budget before the terminal ACK. A
		// second snapshot deadline would incorrectly leave another full budget.
		std::this_thread::sleep_for(std::chrono::milliseconds(250));
		const auto ackWrittenAt = SteadyClock::now();
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		if (!WriteEnvelopeWithin(socket, acknowledgement, peerDeadline))
			return;
		const bool connectionRetired = WaitForPeerClose(socket, peerDeadline);
		if (connectionRetired)
			closeElapsedMs = std::chrono::duration_cast<std::chrono::milliseconds>(SteadyClock::now() - ackWrittenAt).count();
		retired = connectionRetired;
		observed = true;
	});

	ServerBackedSession::Configuration configuration {
		.client = {
		    .host = "127.0.0.1",
		    .port = acceptor.local_endpoint().port(),
		    .clientBuildId = "client",
		    .protocolSchemaVersion = "1",
		    .contentManifestHash = "content",
		    .supportsClockGatedCommands = true,
		    .gatedReceiveTimeoutMs = 400,
		},
	};
	auto session = ServerBackedSession::Connect(configuration);
	ASSERT_TRUE(session.has_value()) << session.error();
	ASSERT_TRUE((*session)->Client().IsClockGated());
	const auto move = (*session)->Move(1, 0, 11, 0);
	ASSERT_FALSE(move.has_value());
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	EXPECT_EQ((*session)->Client().PendingTrackedCommandCount(), 0U);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_TRUE(retired);
	EXPECT_EQ(commandCount, 1);
	EXPECT_GE(closeElapsedMs.load(), 0);
	EXPECT_LT(closeElapsedMs.load(), 300);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, TerminalAckMalformedSnapshotRetainsPriorProjectionWithoutReplay)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	std::atomic_int commandCount = 0;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		protocol::Envelope hello;
		if (!AcceptWithin(acceptor, socket, deadline)
		    || !ReadEnvelopeWithin(socket, hello, deadline)
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_IMMEDIATE);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(10);
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->set_position_x(4);
		initialPlayer->set_position_y(5);
		initialPlayer->set_gold(100);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;

		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 1
		    || !command.command_batch().commands(0).has_move_requested())
			return;
		++commandCount;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		if (!WriteEnvelopeWithin(socket, acknowledgement, deadline))
			return;
		protocol::Envelope malformed;
		malformed.mutable_snapshot()->set_tick(11);
		auto *malformedPlayer = malformed.mutable_snapshot()->add_players();
		malformedPlayer->set_entity_id(7);
		malformedPlayer->set_character_level(1);
		malformedPlayer->set_position_x(9);
		malformedPlayer->set_position_y(10);
		malformedPlayer->set_life(-1);
		if (!WriteEnvelopeWithin(socket, malformed, deadline))
			return;
		observed = true;
	});

	ServerBackedSession::Configuration configuration {
		.client = {
		    .host = "127.0.0.1",
		    .port = acceptor.local_endpoint().port(),
		    .clientBuildId = "client",
		    .protocolSchemaVersion = "1",
		    .contentManifestHash = "content",
		    .supportsClockGatedCommands = true,
		},
	};
	auto session = ServerBackedSession::Connect(configuration);
	ASSERT_TRUE(session.has_value()) << session.error();
	const auto move = (*session)->Move(1, 0, 11, 0);
	ASSERT_FALSE(move.has_value());
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	EXPECT_EQ((*session)->Client().PendingTrackedCommandCount(), 0U);
	ASSERT_NE((*session)->PlayerState().Snapshot(), nullptr);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionX, 4);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionY, 5);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->gold, 100U);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_EQ(commandCount, 1);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, ClockGatedTerminalAckMalformedSnapshotRetainsPriorProjectionWithoutReplay)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	std::atomic_int commandCount = 0;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		protocol::Envelope hello;
		if (!AcceptWithin(acceptor, socket, deadline)
		    || !ReadEnvelopeWithin(socket, hello, deadline)
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(10);
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->set_position_x(4);
		initialPlayer->set_position_y(5);
		initialPlayer->set_gold(100);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;

		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 1
		    || !command.command_batch().commands(0).has_move_requested())
			return;
		++commandCount;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		if (!WriteEnvelopeWithin(socket, acknowledgement, deadline))
			return;
		protocol::Envelope malformed;
		malformed.mutable_snapshot()->set_tick(11);
		auto *malformedPlayer = malformed.mutable_snapshot()->add_players();
		malformedPlayer->set_entity_id(7);
		malformedPlayer->set_character_level(1);
		malformedPlayer->set_position_x(9);
		malformedPlayer->set_position_y(10);
		malformedPlayer->set_life(-1);
		if (!WriteEnvelopeWithin(socket, malformed, deadline))
			return;
		observed = true;
	});

	ServerBackedSession::Configuration configuration {
		.client = {
		    .host = "127.0.0.1",
		    .port = acceptor.local_endpoint().port(),
		    .clientBuildId = "client",
		    .protocolSchemaVersion = "1",
		    .contentManifestHash = "content",
		    .supportsClockGatedCommands = true,
		    .gatedReceiveTimeoutMs = 1'000,
		},
	};
	auto session = ServerBackedSession::Connect(configuration);
	ASSERT_TRUE(session.has_value()) << session.error();
	ASSERT_TRUE((*session)->Client().IsClockGated());
	const auto move = (*session)->Move(1, 0, 11, 0);
	ASSERT_FALSE(move.has_value());
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	EXPECT_EQ((*session)->Client().PendingTrackedCommandCount(), 0U);
	ASSERT_NE((*session)->PlayerState().Snapshot(), nullptr);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionX, 4);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionY, 5);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->gold, 100U);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_EQ(commandCount, 1);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, PreservesAcceptedCloseSeparateFromDuplicateClose)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		protocol::Envelope hello;
		if (!AcceptWithin(acceptor, socket, deadline)
		    || !ReadEnvelopeWithin(socket, hello, deadline))
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(10);
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->set_active_store_id(1);
		initial.mutable_snapshot()->mutable_active_store()->set_store_id(1);
		auto *item = initial.mutable_snapshot()->mutable_active_store()->add_items();
		item->set_store_slot(0);
		item->set_item_seed(42);
		item->set_price(75);
		item->mutable_state()->set_item_type(1);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;

		protocol::Envelope close;
		if (!ReadEnvelopeWithin(socket, close, deadline)
		    || close.payload_case() != protocol::Envelope::kCommandBatch
		    || close.command_batch().commands_size() != 1
		    || close.command_batch().commands(0).client_sequence() != 1
		    || !close.command_batch().commands(0).has_close_store_requested())
			return;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		if (!WriteEnvelopeWithin(socket, acknowledgement, deadline))
			return;
		protocol::Envelope closed;
		closed.mutable_snapshot()->set_tick(11);
		auto *closedPlayer = closed.mutable_snapshot()->add_players();
		closedPlayer->set_entity_id(7);
		closedPlayer->set_character_level(1);
		if (!WriteEnvelopeWithin(socket, closed, deadline))
			return;
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
	auto close = (*session)->CloseVendor(11, 0);
	ASSERT_TRUE(close.has_value()) << close.error();
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	EXPECT_EQ((*session)->VendorState().Phase(), ServerBackedVendorPhase::AwaitingSnapshot);
	EXPECT_EQ((*session)->VendorState().Snapshot(), nullptr);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, SequenceExhaustedPurchaseDoesNotLeavePendingVendorIntent)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		protocol::Envelope hello;
		if (!AcceptWithin(acceptor, socket, deadline)
		    || !ReadEnvelopeWithin(socket, hello, deadline))
			return;
		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		serverHello.mutable_server_hello()->set_last_client_sequence(std::numeric_limits<uint64_t>::max());
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(10);
		auto *initialPlayer = initial.mutable_snapshot()->add_players();
		initialPlayer->set_entity_id(7);
		initialPlayer->set_character_level(1);
		initialPlayer->set_active_store_id(1);
		initial.mutable_snapshot()->mutable_active_store()->set_store_id(1);
		auto *item = initial.mutable_snapshot()->mutable_active_store()->add_items();
		item->set_store_slot(0);
		item->set_item_seed(42);
		item->set_price(75);
		item->mutable_state()->set_item_type(1);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;
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
	const auto purchase = (*session)->Purchase(1, 0, 11, 0);
	ASSERT_FALSE(purchase.has_value());
	EXPECT_NE(purchase.error().find("sequence space is exhausted"), std::string::npos);
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::None);
	EXPECT_FALSE((*session)->VendorState().IsPurchasePending(1, 0));
	EXPECT_EQ((*session)->VendorState().PendingPurchaseCount(), 0U);
	EXPECT_EQ((*session)->Client().PendingTrackedCommandCount(), 0U);
	EXPECT_EQ((*session)->VendorState().Phase(), ServerBackedVendorPhase::Ready);
	ASSERT_NE((*session)->VendorState().FindItem(1, 0), nullptr);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedSession, IgnoresStaleSnapshotsAndEventsDuringPolling)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
		initialPlayer->set_character_level(1);
		initialPlayer->set_position_x(4);
		initialPlayer->set_position_y(5);
		initial.mutable_snapshot()->set_tick(10);
		WriteEnvelope(socket, initial);

		auto pollPayload = EnvelopeCodec::Read(socket);
		if (!pollPayload.has_value() || !pollPayload->has_value())
			return;
		protocol::Envelope poll;
		if (!poll.ParseFromArray(pollPayload->value().data(), static_cast<int>(pollPayload->value().size()))
		    || !poll.has_snapshot_request())
			return;

		protocol::Envelope staleEvents;
		staleEvents.mutable_event_batch()->set_tick(9);
		staleEvents.mutable_event_batch()->add_events()->mutable_experience()->set_player_entity_id(7);
		staleEvents.mutable_event_batch()->mutable_events(0)->mutable_experience()->set_amount(99);
		WriteEnvelope(socket, staleEvents);

		protocol::Envelope stale;
		auto *stalePlayer = stale.mutable_snapshot()->add_players();
		stalePlayer->set_entity_id(7);
		stalePlayer->set_character_level(1);
		stalePlayer->set_position_x(1);
		stalePlayer->set_position_y(2);
		stale.mutable_snapshot()->set_tick(9);
		WriteEnvelope(socket, stale);
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
	ASSERT_TRUE((*session)->Poll(1000).has_value());
	ASSERT_NE((*session)->PlayerState().Snapshot(), nullptr);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->serverTick, 10U);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionX, 4);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionY, 5);
	(*session)->Close();
	server.join();
	EXPECT_TRUE(observed);
}

TEST(ServerBackedSession, RejectsMalformedSnapshotWithoutPartialStateMutation)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
		initialPlayer->set_character_level(1);
		initialPlayer->set_position_x(4);
		initialPlayer->set_position_y(5);
		initial.mutable_snapshot()->set_tick(10);
		WriteEnvelope(socket, initial);

		auto pollPayload = EnvelopeCodec::Read(socket);
		if (!pollPayload.has_value() || !pollPayload->has_value())
			return;
		protocol::Envelope poll;
		if (!poll.ParseFromArray(pollPayload->value().data(), static_cast<int>(pollPayload->value().size()))
		    || !poll.has_snapshot_request())
			return;

		protocol::Envelope malformed;
		auto *malformedPlayer = malformed.mutable_snapshot()->add_players();
		malformedPlayer->set_entity_id(7);
		malformedPlayer->set_character_level(1);
		malformedPlayer->set_position_x(9);
		malformedPlayer->set_position_y(10);
		malformed.mutable_snapshot()->set_tick(11);
		auto *invalidWorldItem = malformed.mutable_snapshot()->add_world_items();
		invalidWorldItem->set_entity_id(8);
		invalidWorldItem->set_position_x(9);
		invalidWorldItem->set_position_y(10);
		invalidWorldItem->set_item_seed(0);
		WriteEnvelope(socket, malformed);
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
	auto poll = (*session)->Poll(1000);
	ASSERT_FALSE(poll.has_value());
	ASSERT_NE((*session)->PlayerState().Snapshot(), nullptr);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->serverTick, 10U);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionX, 4);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionY, 5);
	(*session)->Close();
	server.join();
	EXPECT_TRUE(observed);
}

TEST(ServerBackedSession, DoesNotTreatRejectedCommandAsLocalSuccess)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
		initialPlayer->set_character_level(1);
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
		auto *snapshotPlayer = snapshot.mutable_snapshot()->add_players();
		snapshotPlayer->set_entity_id(7);
		snapshotPlayer->set_character_level(1);
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

TEST(ServerBackedSession, SubmitsAuthoredQuestDialogueIntent)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
		initialPlayer->set_character_level(1);
		auto *quest = initial.mutable_snapshot()->add_quests();
		quest->set_quest_id(3021);
		quest->set_required_progress(1);
		quest->set_completion_unique_identity("Defiler");
		WriteEnvelope(socket, initial);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.command_batch().commands_size() != 1
		    || !command.command_batch().commands(0).has_talk_to_quest_requested()
		    || command.command_batch().commands(0).talk_to_quest_requested().quest_id() != 3021
		    || command.command_batch().commands(0).talk_to_quest_requested().target_entity_id() != 44)
			return;
		protocol::Envelope ack;
		auto *result = ack.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		WriteEnvelope(socket, ack);
		protocol::Envelope snapshot;
		auto *snapshotPlayer = snapshot.mutable_snapshot()->add_players();
		snapshotPlayer->set_entity_id(7);
		snapshotPlayer->set_character_level(1);
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
	auto talk = (*session)->TalkToQuest(3021, 44, 10, 0);
	ASSERT_TRUE(talk.has_value()) << talk.error();
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	(*session)->Close();
	server.join();
	EXPECT_TRUE(observed);
}

TEST(ServerBackedSession, SubmitsStableWorldDropIntent)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
		initialPlayer->set_character_level(1);
		auto *item = initial.mutable_snapshot()->mutable_players(0)->add_inventory();
		item->set_item_seed(42);
		item->mutable_state()->set_stable_item_id(700);
		item->mutable_state()->set_item_type(1);
		WriteEnvelope(socket, initial);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.command_batch().commands_size() != 1)
			return;
		const auto &request = command.command_batch().commands(0);
		if (!request.has_drop_item_requested()
		    || request.drop_item_requested().target_x() != 1
		    || request.drop_item_requested().target_y() != 2
		    || request.drop_item_requested().item().location() != protocol::PLAYER_ITEM_LOCATION_INVENTORY
		    || request.drop_item_requested().item().slot() != 0
		    || request.drop_item_requested().item().stable_item_id() != 700)
			return;

		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(request.client_sequence());
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		WriteEnvelope(socket, acknowledgement);
		protocol::Envelope snapshot;
		auto *snapshotPlayer = snapshot.mutable_snapshot()->add_players();
		snapshotPlayer->set_entity_id(7);
		snapshotPlayer->set_character_level(1);
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
	const auto drop = (*session)->DropItem(
	    ServerBackedItemReference {
	        .location = ServerBackedItemLocation::Inventory,
	        .slot = 0,
	        .stableItemId = 700,
	    },
	    1,
	    2,
	    10,
	    0);
	ASSERT_TRUE(drop.has_value()) << drop.error();
	EXPECT_EQ((*session)->LastCommandResolution(), ServerBackedSession::CommandResolution::Accepted);
	(*session)->Close();
	server.join();
	EXPECT_TRUE(observed);
}

TEST(ServerBackedSession, AcceptsLowerTickFromFreshReconnectEpoch)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket first(serverIo);
		protocol::Envelope firstHello;
		if (!AcceptWithin(acceptor, first, deadline)
		    || !ReadEnvelopeWithin(first, firstHello, deadline))
			return;
		protocol::Envelope firstServerHello;
		firstServerHello.mutable_server_hello()->set_protocol_schema_version("1");
		firstServerHello.mutable_server_hello()->set_content_manifest_hash("content");
		firstServerHello.mutable_server_hello()->set_session_token("resume");
		firstServerHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_IMMEDIATE);
		if (!WriteEnvelopeWithin(first, firstServerHello, deadline))
			return;
		protocol::Envelope firstSnapshot;
		firstSnapshot.mutable_snapshot()->set_tick(30);
		auto *firstPlayer = firstSnapshot.mutable_snapshot()->add_players();
		firstPlayer->set_entity_id(7);
		firstPlayer->set_character_level(1);
		firstPlayer->set_position_x(4);
		firstPlayer->set_position_y(5);
		firstPlayer->set_gold(100);
		firstPlayer->set_mana(10);
		if (!WriteEnvelopeWithin(first, firstSnapshot, deadline))
			return;

		tcp::socket resumed(serverIo);
		protocol::Envelope resumedHello;
		if (!AcceptWithin(acceptor, resumed, deadline)
		    || !ReadEnvelopeWithin(resumed, resumedHello, deadline)
		    || resumedHello.payload_case() != protocol::Envelope::kClientHello
		    || resumedHello.client_hello().resume_token() != "resume")
			return;
		protocol::Envelope resumedServerHello;
		resumedServerHello.mutable_server_hello()->set_protocol_schema_version("1");
		resumedServerHello.mutable_server_hello()->set_content_manifest_hash("content");
		resumedServerHello.mutable_server_hello()->set_session_token("resume");
		resumedServerHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_IMMEDIATE);
		if (!WriteEnvelopeWithin(resumed, resumedServerHello, deadline))
			return;
		protocol::Envelope resumedSnapshot;
		resumedSnapshot.mutable_snapshot()->set_tick(20);
		auto *resumedPlayer = resumedSnapshot.mutable_snapshot()->add_players();
		resumedPlayer->set_entity_id(7);
		resumedPlayer->set_character_level(1);
		resumedPlayer->set_position_x(9);
		resumedPlayer->set_position_y(11);
		resumedPlayer->set_gold(250);
		resumedPlayer->set_mana(17);
		if (!WriteEnvelopeWithin(resumed, resumedSnapshot, deadline))
			return;
		observed = true;
	});

	ServerBackedSession::Configuration configuration {
		.client = {
		    .host = "127.0.0.1",
		    .port = acceptor.local_endpoint().port(),
		    .clientBuildId = "client",
		    .protocolSchemaVersion = "1",
		    .contentManifestHash = "content",
		    .supportsClockGatedCommands = true,
		},
	};
	auto session = ServerBackedSession::Connect(configuration);
	ASSERT_TRUE(session.has_value()) << session.error();
	auto reconnect = (*session)->Reconnect(100);
	ASSERT_TRUE(reconnect.has_value()) << reconnect.error();
	ASSERT_NE((*session)->PlayerState().Snapshot(), nullptr);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->serverTick, 20U);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionX, 9);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->positionY, 11);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->gold, 250U);
	EXPECT_EQ((*session)->PlayerState().Snapshot()->mana, 17);
	(*session)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_FALSE(server.Failed());
}

} // namespace
} // namespace devilution::authoritative
