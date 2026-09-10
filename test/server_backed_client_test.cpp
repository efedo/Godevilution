#include <atomic>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <string>
#include <thread>

#include <asio/ip/tcp.hpp>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/server_backed_client.hpp"
#include "network/authoritative/envelope_codec.hpp"

namespace devilution::authoritative {
namespace {

using asio::ip::tcp;
namespace protocol = ::devilution::protocol::v1;

void WriteEnvelope(tcp::socket &socket, const protocol::Envelope &envelope)
{
	const std::string payload = envelope.SerializeAsString();
	ASSERT_TRUE(EnvelopeCodec::Write(socket, std::span<const uint8_t>(
	                                      reinterpret_cast<const uint8_t *>(payload.data()), payload.size()))
	                .has_value());
}

TEST(ServerBackedClient, CompletesHandshakeCommandAndSnapshotExchange)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
	std::thread server([&]() {
		tcp::socket socket(serverIo);
		acceptor.accept(socket);

		auto helloPayload = EnvelopeCodec::Read(socket);
		if (!helloPayload.has_value() || !helloPayload->has_value())
			return;
		protocol::Envelope hello;
		if (!hello.ParseFromArray(helloPayload->value().data(), static_cast<int>(helloPayload->value().size()))
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;
		if (hello.client_hello().protocol_schema_version() != "1"
		    || hello.client_hello().content_manifest_hash() != "content"
		    || !hello.client_hello().has_requested_character_class_id()
		    || hello.client_hello().requested_character_class_id() != 2
		    || !hello.client_hello().has_requested_character_name()
		    || hello.client_hello().requested_character_name() != "Godot-Rogue")
			return;

		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_server_build_id("server");
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_ruleset_identity_hash("ruleset");
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_session_token("session");
		serverHello.mutable_server_hello()->set_game_mode(protocol::GAME_MODE_MULTIPLAYER);
		WriteEnvelope(socket, serverHello);

		protocol::Envelope initialSnapshot;
		initialSnapshot.mutable_snapshot()->set_tick(11);
		initialSnapshot.mutable_snapshot()->set_state_sha256("initial-state");
		initialSnapshot.mutable_snapshot()->set_content_manifest_hash("content");
		initialSnapshot.mutable_snapshot()->set_ruleset_identity_hash("ruleset");
		WriteEnvelope(socket, initialSnapshot);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 7)
			return;

		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(7);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		result->set_applied_tick(12);
		WriteEnvelope(socket, acknowledgement);

		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->set_tick(12);
		snapshot.mutable_snapshot()->set_state_sha256("state");
		snapshot.mutable_snapshot()->set_content_manifest_hash("reloaded-content");
		snapshot.mutable_snapshot()->set_ruleset_identity_hash("reloaded-ruleset");
		WriteEnvelope(socket, snapshot);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.requestedCharacterClassId = 2,
		.requestedCharacterName = "Godot-Rogue",
		.expectInitialSnapshot = true,
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();
	ASSERT_EQ((*client)->ServerHello().session_token(), "session");
	EXPECT_EQ((*client)->ServerHello().game_mode(), protocol::GAME_MODE_MULTIPLAYER);
	EXPECT_EQ((*client)->ServerHello().content_manifest_hash(), "content");
	EXPECT_EQ((*client)->ServerHello().ruleset_identity_hash(), "ruleset");
	auto initialSnapshot = (*client)->ReadSnapshot();
	ASSERT_TRUE(initialSnapshot.has_value()) << initialSnapshot.error();
	EXPECT_EQ(initialSnapshot->tick(), 11U);
	EXPECT_EQ(initialSnapshot->state_sha256(), "initial-state");

	protocol::CommandBatch batch;
	auto *command = batch.add_commands();
	command->set_client_sequence(7);
	command->set_requested_tick(12);
	command->mutable_move_requested()->set_direction_x(1);
	auto acknowledgement = (*client)->Submit(batch);
	ASSERT_TRUE(acknowledgement.has_value()) << acknowledgement.error();
	ASSERT_EQ(acknowledgement->results_size(), 1);
	EXPECT_EQ(acknowledgement->results(0).status(), protocol::COMMAND_STATUS_ACCEPTED);

	auto snapshot = (*client)->ReadSnapshot();
	ASSERT_TRUE(snapshot.has_value()) << snapshot.error();
	EXPECT_EQ(snapshot->tick(), 12U);
	EXPECT_EQ(snapshot->state_sha256(), "state");
	EXPECT_EQ(snapshot->content_manifest_hash(), "reloaded-content");
	EXPECT_EQ(snapshot->ruleset_identity_hash(), "reloaded-ruleset");
	EXPECT_EQ((*client)->ServerHello().content_manifest_hash(), "reloaded-content");
	EXPECT_EQ((*client)->ServerHello().ruleset_identity_hash(), "reloaded-ruleset");

	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, RejectsMalformedCommandAcknowledgementBeforeTrackingResolution)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
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
		WriteEnvelope(socket, serverHello);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.payload_case() != protocol::Envelope::kCommandBatch)
			return;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		result->set_reject_reason(protocol::COMMAND_REJECT_REASON_NOT_ALLOWED);
		WriteEnvelope(socket, acknowledgement);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();
	protocol::CommandBatch batch;
	batch.add_commands()->mutable_move_requested()->set_direction_x(1);
	auto acknowledgement = (*client)->Submit(batch);
	ASSERT_FALSE(acknowledgement.has_value());
	EXPECT_EQ(acknowledgement.error(), "The authoritative command acknowledgement contains an invalid result.");
	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, RejectsOversizedCommandAcknowledgementBeforeTrackingResolution)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
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
		WriteEnvelope(socket, serverHello);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.payload_case() != protocol::Envelope::kCommandBatch)
			return;
		protocol::Envelope acknowledgement;
		for (int index = 0; index < 257; ++index) {
			auto *result = acknowledgement.mutable_command_ack()->add_results();
			result->set_client_sequence(static_cast<uint64_t>(index + 1));
			result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		}
		WriteEnvelope(socket, acknowledgement);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();
	protocol::CommandBatch batch;
	batch.add_commands()->mutable_move_requested()->set_direction_x(1);
	auto acknowledgement = (*client)->Submit(batch);
	ASSERT_FALSE(acknowledgement.has_value());
	EXPECT_EQ(acknowledgement.error(), "The authoritative command acknowledgement is too large.");
	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, RejectsMalformedTrackedAcknowledgementBeforeApplyingDelivery)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
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
		WriteEnvelope(socket, serverHello);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1)
			return;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(command.command_batch().commands(0).client_sequence());
		result->set_status(protocol::COMMAND_STATUS_UNSPECIFIED);
		WriteEnvelope(socket, acknowledgement);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();
	protocol::Command command;
	command.mutable_move_requested()->set_direction_x(1);
	const uint64_t sequence = (*client)->QueueCommand(std::move(command));
	ASSERT_EQ(sequence, 1U);
	ASSERT_TRUE((*client)->SendQueuedCommands(100).has_value());
	auto acknowledgement = (*client)->ReceiveCommandAcknowledgement(100);
	ASSERT_FALSE(acknowledgement.has_value());
	EXPECT_EQ(acknowledgement.error(), "The authoritative command acknowledgement contains an invalid result.");
	EXPECT_EQ((*client)->PendingTrackedCommandCount(), 1U);
	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, RequestsAutonomousSnapshotsAfterHandshake)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
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
		WriteEnvelope(socket, serverHello);

		auto requestPayload = EnvelopeCodec::Read(socket);
		if (!requestPayload.has_value() || !requestPayload->has_value())
			return;
		protocol::Envelope request;
		if (!request.ParseFromArray(requestPayload->value().data(), static_cast<int>(requestPayload->value().size()))
		    || request.payload_case() != protocol::Envelope::kSnapshotRequest)
			return;
		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->set_tick(25);
		snapshot.mutable_snapshot()->set_state_sha256("autonomous-state");
		WriteEnvelope(socket, snapshot);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();
	EXPECT_EQ((*client)->ServerHello().game_mode(), protocol::GAME_MODE_UNSPECIFIED);
	auto snapshot = (*client)->RequestSnapshot();
	ASSERT_TRUE(snapshot.has_value()) << snapshot.error();
	EXPECT_EQ(snapshot->tick(), 25U);
	EXPECT_EQ(snapshot->state_sha256(), "autonomous-state");
	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, RejectsIncompleteSnapshotIdentity)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
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
		WriteEnvelope(socket, serverHello);
		protocol::Envelope malformedSnapshot;
		malformedSnapshot.mutable_snapshot()->set_tick(11);
		malformedSnapshot.mutable_snapshot()->set_content_manifest_hash("content");
		WriteEnvelope(socket, malformedSnapshot);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_FALSE(client.has_value());
	EXPECT_EQ(client.error(), "The authoritative snapshot contains an incomplete ruleset identity.");
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, WritesDetailedDiagnosticDumpForIdentityMismatch)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
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
		serverHello.mutable_server_hello()->set_server_build_id("debug-server");
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("actual-content");
		serverHello.mutable_server_hello()->set_ruleset_identity_hash("actual-ruleset");
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		WriteEnvelope(socket, serverHello);
	});

	const auto diagnosticsDirectory = std::filesystem::temp_directory_path() / "devilutionx-server-backed-mismatch-test";
	std::filesystem::remove_all(diagnosticsDirectory);
	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "expected-content",
		.rulesetIdentityHash = "expected-ruleset",
		.diagnosticsDirectory = diagnosticsDirectory.string(),
	};
	auto client = ServerBackedClient::Connect(configuration);
	server.join();
	ASSERT_FALSE(client.has_value());
	ASSERT_NE(client.error().find("Diagnostic dump:"), std::string::npos);

	const auto dumpPath = std::filesystem::path(client.error().substr(client.error().find("Diagnostic dump: ") + 17));
	std::ifstream dump(dumpPath);
	ASSERT_TRUE(dump.good());
	const std::string contents((std::istreambuf_iterator<char>(dump)), std::istreambuf_iterator<char>());
	EXPECT_NE(contents.find("reason=content-hash-mismatch"), std::string::npos);
	EXPECT_NE(contents.find("expected.content_manifest_hash=expected-content"), std::string::npos);
	EXPECT_NE(contents.find("actual.content_manifest_hash=actual-content"), std::string::npos);
	EXPECT_NE(contents.find("expected.ruleset_identity_hash=expected-ruleset"), std::string::npos);
	EXPECT_NE(contents.find("actual.ruleset_identity_hash=actual-ruleset"), std::string::npos);
	dump.close();
	std::filesystem::remove_all(diagnosticsDirectory);
}

TEST(ServerBackedClient, RetriesTrackedCommandsWithTheOriginalSequence)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
	std::thread server([&]() {
		tcp::socket socket(serverIo);
		acceptor.accept(socket);

		auto helloPayload = EnvelopeCodec::Read(socket);
		if (!helloPayload.has_value() || !helloPayload->has_value())
			return;
		protocol::Envelope hello;
		if (!hello.ParseFromArray(helloPayload->value().data(), static_cast<int>(helloPayload->value().size()))
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;

		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_session_token("session");
		WriteEnvelope(socket, serverHello);

		auto firstPayload = EnvelopeCodec::Read(socket);
		if (!firstPayload.has_value() || !firstPayload->has_value())
			return;
		protocol::Envelope first;
		if (!first.ParseFromArray(firstPayload->value().data(), static_cast<int>(firstPayload->value().size()))
		    || first.payload_case() != protocol::Envelope::kCommandBatch
		    || first.command_batch().commands_size() != 1
		    || first.command_batch().commands(0).client_sequence() != 1)
			return;

		auto retryPayload = EnvelopeCodec::Read(socket);
		if (!retryPayload.has_value() || !retryPayload->has_value())
			return;
		protocol::Envelope retry;
		if (!retry.ParseFromArray(retryPayload->value().data(), static_cast<int>(retryPayload->value().size()))
		    || retry.payload_case() != protocol::Envelope::kCommandBatch
		    || retry.command_batch().commands_size() != 1
		    || retry.command_batch().commands(0).client_sequence() != 1)
			return;

		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		WriteEnvelope(socket, acknowledgement);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();

	protocol::Command command;
	command.set_requested_tick(12);
	command.mutable_move_requested()->set_direction_x(1);
	EXPECT_EQ((*client)->QueueCommand(command), 1U);
	ASSERT_TRUE((*client)->SendQueuedCommands(0).has_value());
	EXPECT_EQ((*client)->PendingTrackedCommandCount(), 1U);

	EXPECT_TRUE((*client)->PrepareTrackedResubmissions(299).has_value());
	auto resubmissions = (*client)->PrepareTrackedResubmissions(300);
	ASSERT_TRUE(resubmissions.has_value());
	EXPECT_EQ(resubmissions.value(), std::vector<uint64_t>({ 1 }));

	auto acknowledgement = (*client)->ReceiveCommandAcknowledgement(400);
	ASSERT_TRUE(acknowledgement.has_value()) << acknowledgement.error();
	ASSERT_EQ(acknowledgement->results_size(), 1);
	EXPECT_EQ(acknowledgement->results(0).status(), protocol::COMMAND_STATUS_ACCEPTED);
	EXPECT_EQ((*client)->PendingTrackedCommandCount(), 0U);

	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, ReconnectsWithSessionTokenAndResubmitsUnresolvedCommands)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
	std::thread server([&]() {
		tcp::socket initialSocket(serverIo);
		acceptor.accept(initialSocket);

		auto initialHelloPayload = EnvelopeCodec::Read(initialSocket);
		if (!initialHelloPayload.has_value() || !initialHelloPayload->has_value())
			return;
		protocol::Envelope initialHello;
		if (!initialHello.ParseFromArray(initialHelloPayload->value().data(), static_cast<int>(initialHelloPayload->value().size()))
		    || initialHello.payload_case() != protocol::Envelope::kClientHello
		    || !initialHello.client_hello().resume_token().empty())
			return;

		protocol::Envelope initialServerHello;
		initialServerHello.mutable_server_hello()->set_protocol_schema_version("1");
		initialServerHello.mutable_server_hello()->set_content_manifest_hash("content");
		initialServerHello.mutable_server_hello()->set_session_token("resume-session");
		WriteEnvelope(initialSocket, initialServerHello);
		protocol::Envelope initialSnapshot;
		initialSnapshot.mutable_snapshot()->set_tick(30);
		initialSnapshot.mutable_snapshot()->set_content_manifest_hash("reloaded-content");
		initialSnapshot.mutable_snapshot()->set_ruleset_identity_hash("reloaded-ruleset");
		WriteEnvelope(initialSocket, initialSnapshot);

		auto firstCommandPayload = EnvelopeCodec::Read(initialSocket);
		if (!firstCommandPayload.has_value() || !firstCommandPayload->has_value())
			return;
		protocol::Envelope firstCommand;
		if (!firstCommand.ParseFromArray(firstCommandPayload->value().data(), static_cast<int>(firstCommandPayload->value().size()))
		    || firstCommand.payload_case() != protocol::Envelope::kCommandBatch
		    || firstCommand.command_batch().commands_size() != 1
		    || firstCommand.command_batch().commands(0).client_sequence() != 1
		    || firstCommand.command_batch().commands(0).move_requested().direction_x() != 1)
			return;
		initialSocket.close();

		tcp::socket resumedSocket(serverIo);
		acceptor.accept(resumedSocket);
		auto resumedHelloPayload = EnvelopeCodec::Read(resumedSocket);
		if (!resumedHelloPayload.has_value() || !resumedHelloPayload->has_value())
			return;
		protocol::Envelope resumedHello;
		if (!resumedHello.ParseFromArray(resumedHelloPayload->value().data(), static_cast<int>(resumedHelloPayload->value().size()))
		    || resumedHello.payload_case() != protocol::Envelope::kClientHello
		    || resumedHello.client_hello().resume_token() != "resume-session"
		    || resumedHello.client_hello().content_manifest_hash() != "reloaded-content"
		    || resumedHello.client_hello().ruleset_identity_hash() != "reloaded-ruleset")
			return;

		protocol::Envelope resumedServerHello;
		resumedServerHello.mutable_server_hello()->set_protocol_schema_version("1");
		resumedServerHello.mutable_server_hello()->set_content_manifest_hash("reloaded-content");
		resumedServerHello.mutable_server_hello()->set_ruleset_identity_hash("reloaded-ruleset");
		resumedServerHello.mutable_server_hello()->set_session_token("resume-session");
		WriteEnvelope(resumedSocket, resumedServerHello);

		protocol::Envelope resynchronizationSnapshot;
		resynchronizationSnapshot.mutable_snapshot()->set_tick(40);
		resynchronizationSnapshot.mutable_snapshot()->set_state_sha256("resynchronized-state");
		resynchronizationSnapshot.mutable_snapshot()->set_content_manifest_hash("reloaded-content");
		resynchronizationSnapshot.mutable_snapshot()->set_ruleset_identity_hash("reloaded-ruleset");
		WriteEnvelope(resumedSocket, resynchronizationSnapshot);

		auto resentCommandsPayload = EnvelopeCodec::Read(resumedSocket);
		if (!resentCommandsPayload.has_value() || !resentCommandsPayload->has_value())
			return;
		protocol::Envelope resentCommands;
		if (!resentCommands.ParseFromArray(resentCommandsPayload->value().data(), static_cast<int>(resentCommandsPayload->value().size()))
		    || resentCommands.payload_case() != protocol::Envelope::kCommandBatch
		    || resentCommands.command_batch().commands_size() != 2
		    || resentCommands.command_batch().commands(0).client_sequence() != 1
		    || resentCommands.command_batch().commands(0).move_requested().direction_x() != 1
		    || resentCommands.command_batch().commands(1).client_sequence() != 2
		    || resentCommands.command_batch().commands(1).move_requested().direction_y() != -1)
			return;

		protocol::Envelope acknowledgement;
		for (uint64_t sequence : { 1U, 2U }) {
			auto *result = acknowledgement.mutable_command_ack()->add_results();
			result->set_client_sequence(sequence);
			result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		}
		WriteEnvelope(resumedSocket, acknowledgement);
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
	};
	auto client = ServerBackedClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();

	protocol::Command sentCommand;
	sentCommand.set_requested_tick(39);
	sentCommand.mutable_move_requested()->set_direction_x(1);
	EXPECT_EQ((*client)->QueueCommand(sentCommand), 1U);
	ASSERT_TRUE((*client)->SendQueuedCommands(0).has_value());

	protocol::Command queuedCommand;
	queuedCommand.set_requested_tick(40);
	queuedCommand.mutable_move_requested()->set_direction_y(-1);
	EXPECT_EQ((*client)->QueueCommand(queuedCommand), 2U);
	EXPECT_EQ((*client)->PendingTrackedCommandCount(), 2U);

	auto reconnect = (*client)->Reconnect(100);
	ASSERT_TRUE(reconnect.has_value()) << reconnect.error();
	auto snapshot = (*client)->ReadSnapshot();
	ASSERT_TRUE(snapshot.has_value()) << snapshot.error();
	EXPECT_EQ(snapshot->tick(), 40U);
	EXPECT_EQ(snapshot->state_sha256(), "resynchronized-state");

	auto acknowledgement = (*client)->ReceiveCommandAcknowledgement(125);
	ASSERT_TRUE(acknowledgement.has_value()) << acknowledgement.error();
	EXPECT_EQ(acknowledgement->results_size(), 2);
	EXPECT_EQ((*client)->PendingTrackedCommandCount(), 0U);

	protocol::Command nextCommand;
	EXPECT_EQ((*client)->QueueCommand(nextCommand), 3U);

	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

} // namespace
} // namespace devilution::authoritative
