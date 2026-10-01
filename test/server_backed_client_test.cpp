#include <atomic>
#include <chrono>
#include <cstdint>
#include <exception>
#include <filesystem>
#include <fstream>
#include <functional>
#include <limits>
#include <string>
#include <thread>

#include <asio/ip/tcp.hpp>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/server_backed_clock.hpp"
#include "network/authoritative/server_backed_client.hpp"
#include "network/authoritative/envelope_codec.hpp"

namespace devilution::authoritative {
namespace {

using asio::ip::tcp;
namespace protocol = ::devilution::protocol::v1;

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
	return EnvelopeCodec::Write(socket, std::span<const uint8_t>(
	                                reinterpret_cast<const uint8_t *>(payload.data()), payload.size()), deadline)
		.has_value();
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
	const std::string payload = envelope.SerializeAsString();
	(void)EnvelopeCodec::Write(socket, std::span<const uint8_t>(
	                                reinterpret_cast<const uint8_t *>(payload.data()), payload.size()));
}

TEST(ServerBackedClient, CompletesHandshakeCommandAndSnapshotExchange)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
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
		resumedServerHello.mutable_server_hello()->set_last_client_sequence(2);
		WriteEnvelope(resumedSocket, resumedServerHello);

		protocol::Envelope resynchronizationSnapshot;
		resynchronizationSnapshot.mutable_snapshot()->set_tick(20);
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
		.supportsClockGatedCommands = true,
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
	EXPECT_EQ(snapshot->tick(), 20U);
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

TEST(ServerBackedClient, OptsIntoClockGatedModeAndStampsRealtimeCommandsAboveServerFloor)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		if (!AcceptWithin(acceptor, socket, deadline))
			return;
		protocol::Envelope hello;
		if (!ReadEnvelopeWithin(socket, hello, deadline)
		    || !hello.client_hello().supports_clock_gated_commands())
			return;

		protocol::Envelope serverHello;
		auto *helloMessage = serverHello.mutable_server_hello();
		helloMessage->set_protocol_schema_version("1");
		helloMessage->set_content_manifest_hash("content");
		helloMessage->set_tick_rate_hz(20);
		helloMessage->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		helloMessage->set_last_client_sequence(4);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;

		protocol::Envelope initialSnapshot;
		initialSnapshot.mutable_snapshot()->set_tick(100);
		if (!WriteEnvelopeWithin(socket, initialSnapshot, deadline))
			return;
		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 5
		    || command.command_batch().commands(0).requested_tick() <= 103)
			return;
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
		.supportsClockGatedCommands = true,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	if (!(*client)->IsClockGated()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "The negotiated scheduling mode was not clock-gated.";
		return;
	}
	std::this_thread::sleep_for(std::chrono::milliseconds(500));
	auto snapshot = (*client)->ReadSnapshot();
	if (!snapshot.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << snapshot.error();
		return;
	}
	protocol::Command command;
	command.mutable_move_requested()->set_direction_x(1);
	EXPECT_EQ((*client)->QueueCommand(command), 5U);
	auto send = (*client)->SendQueuedCommands(1000);
	if (!send.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << send.error();
		return;
	}
	(*client)->Close();
	server.Join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClock, DelayedSnapshotsRetainTheProjectedTick)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	for (const uint64_t snapshotTick : { 90U, 100U, 101U }) {
		Clock clock;
		clock.ObserveMonotonic(100, 20, start);
		clock.ObserveMonotonic(snapshotTick, 20, start + std::chrono::milliseconds(350));

		EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(350)), 107U)
			<< "delayed snapshot tick " << snapshotTick << " rewound the projection";
	}
}

TEST(ServerBackedClock, EqualProjectedTickRetainsFractionalAge)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;
	clock.ObserveMonotonic(100, 20, start);
	clock.ObserveMonotonic(107, 20, start + std::chrono::milliseconds(375));

	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(400)), 108U);
}

TEST(ServerBackedClock, EqualFloorBoundarySampleRetainsTheElapsedFraction)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;
	clock.ObserveMonotonic(100, 20, start);
	clock.ObserveMonotonic(100, 20, start + std::chrono::milliseconds(25));

	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(50)), 101U);
}

TEST(ServerBackedClock, StrictlyAheadSnapshotReanchors)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;
	clock.ObserveMonotonic(100, 20, start);
	clock.ObserveMonotonic(108, 20, start + std::chrono::milliseconds(350));

	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(350)), 108U);
}

TEST(ServerBackedClock, DistinguishesNoAnchorFromAnchoredZeroAndResetsEpoch)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;

	EXPECT_FALSE(clock.HasSnapshotClock());
	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(50)), 0U);
	clock.ObserveMonotonic(0, 20, start);
	EXPECT_TRUE(clock.HasSnapshotClock());
	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(50)), 1U);

	clock.ObserveMonotonic(500, 20, start + std::chrono::seconds(1));
	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(1'100)), 502U);

	clock.Reset();
	EXPECT_FALSE(clock.HasSnapshotClock());
	clock.ObserveMonotonic(10, 40, start + std::chrono::seconds(2));
	EXPECT_EQ(clock.CurrentTick(40, start + std::chrono::milliseconds(2'100)), 14U);
}

TEST(ServerBackedClock, BackwardAndEqualObservationTimesDoNotRewind)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;
	clock.ObserveMonotonic(100, 20, start);
	clock.ObserveMonotonic(100, 20, start);
	clock.ObserveMonotonic(100, 20, start - std::chrono::milliseconds(25));

	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(25)), 100U);
}

TEST(ServerBackedClock, SaturatesAdditionAndElapsedTickMultiplication)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;
	clock.ObserveMonotonic(std::numeric_limits<uint64_t>::max() - 1, 20, start);
	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(100)), std::numeric_limits<uint64_t>::max());

	clock.Reset();
	clock.ObserveMonotonic(0, std::numeric_limits<uint32_t>::max(), start);
	const auto multiplicationOverflowTime = start + std::chrono::milliseconds(5'000'000'000LL);
	EXPECT_EQ(
		clock.CurrentTick(std::numeric_limits<uint32_t>::max(), multiplicationOverflowTime),
		std::numeric_limits<uint64_t>::max());
}

TEST(ServerBackedClock, TruncatesSubMillisecondAgeAndUsesMinimumRate)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;
	clock.ObserveMonotonic(100, 2'000, start);

	EXPECT_EQ(clock.CurrentTick(2'000, start + std::chrono::microseconds(999)), 100U);
	EXPECT_EQ(clock.CurrentTick(2'000, start + std::chrono::milliseconds(1)), 102U);

	clock.Reset();
	clock.ObserveMonotonic(0, 0, start);
	EXPECT_EQ(clock.CurrentTick(0, start + std::chrono::milliseconds(1'500)), 1U);
}

TEST(ServerBackedClock, LegacyObservationRefreshesEqualAndIgnoresLowerTicks)
{
	using Clock = detail::GatedSnapshotClock;
	const auto start = Clock::TimePoint {};
	Clock clock;
	clock.Observe(100, start);
	clock.Observe(100, start + std::chrono::milliseconds(350));
	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(350)), 100U);

	clock.Observe(101, start + std::chrono::milliseconds(700));
	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(700)), 101U);

	clock.Observe(90, start + std::chrono::milliseconds(1'050));
	EXPECT_EQ(clock.CurrentTick(20, start + std::chrono::milliseconds(1'050)), 108U);
}

TEST(ServerBackedClient, RejectsClockGatedServerWithoutExplicitOptIn)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		if (!AcceptWithin(acceptor, socket, deadline))
			return;
		protocol::Envelope hello;
		if (!ReadEnvelopeWithin(socket, hello, deadline))
			return;
		protocol::Envelope response;
		response.mutable_server_hello()->set_protocol_schema_version("1");
		response.mutable_server_hello()->set_content_manifest_hash("content");
		response.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		(void)WriteEnvelopeWithin(socket, response, deadline);
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (client.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "A non-opted-in client accepted clock-gated scheduling.";
		return;
	}
	EXPECT_EQ(client.error(), "Authoritative server selected clock-gated scheduling without client opt-in.");
	server.Join();
}

TEST(ServerBackedClient, ClockGatedSnapshotProbeConsumesMixedEnvelopesAndIgnoresForeignReply)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
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
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(100);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;
		protocol::Envelope request;
		if (!ReadEnvelopeWithin(socket, request, deadline)
		    || request.payload_case() != protocol::Envelope::kSnapshotRequest
		    || request.snapshot_request().timing_request_id() == 0)
			return;
		const uint64_t requestId = request.snapshot_request().timing_request_id();
		protocol::Envelope events;
		events.mutable_event_batch()->set_tick(101);
		if (!WriteEnvelopeWithin(socket, events, deadline))
			return;
		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->set_tick(101);
		if (!WriteEnvelopeWithin(socket, snapshot, deadline))
			return;
		protocol::Envelope foreignReply;
		foreignReply.mutable_timing_reply()->set_request_id(requestId + 1);
		if (!WriteEnvelopeWithin(socket, foreignReply, deadline))
			return;
		protocol::Envelope reply;
		reply.mutable_timing_reply()->set_request_id(requestId);
		if (!WriteEnvelopeWithin(socket, reply, deadline))
			return;
		serverObservedExpectedMessages = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
		.supportsClockGatedCommands = true,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	auto initial = (*client)->ReadSnapshot();
	if (!initial.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << initial.error();
		return;
	}
	auto snapshot = (*client)->RequestSnapshot();
	if (!snapshot.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << snapshot.error();
		return;
	}
	EXPECT_EQ(snapshot->tick(), 101U);
	EXPECT_EQ((*client)->TakePendingEventBatches().size(), 1U);
	(*client)->Close();
	server.Join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

TEST(ServerBackedClient, ClockGatedSilentAcknowledgementTimeoutRetainsPendingCommand)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	std::string firstCommandBytes;
	std::string resentCommandBytes;
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
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(100);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;
		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1)
			return;
		firstCommandBytes = command.SerializeAsString();
		tcp::socket resumed(serverIo);
		if (!AcceptWithin(acceptor, resumed, deadline))
			return;
		protocol::Envelope resumedHello;
		if (!ReadEnvelopeWithin(resumed, resumedHello, deadline))
			return;
		protocol::Envelope resumedServerHello;
		resumedServerHello.mutable_server_hello()->set_protocol_schema_version("1");
		resumedServerHello.mutable_server_hello()->set_content_manifest_hash("content");
		resumedServerHello.mutable_server_hello()->set_session_token("resume");
		resumedServerHello.mutable_server_hello()->set_tick_rate_hz(20);
		resumedServerHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(resumed, resumedServerHello, deadline))
			return;
		protocol::Envelope resumedSnapshot;
		resumedSnapshot.mutable_snapshot()->set_tick(100);
		if (!WriteEnvelopeWithin(resumed, resumedSnapshot, deadline))
			return;
		protocol::Envelope resentCommand;
		if (!ReadEnvelopeWithin(resumed, resentCommand, deadline)
		    || resentCommand.payload_case() != protocol::Envelope::kCommandBatch
		    || resentCommand.command_batch().commands_size() != 1)
			return;
		resentCommandBytes = resentCommand.SerializeAsString();
		observed = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
		.supportsClockGatedCommands = true,
		.gatedReceiveTimeoutMs = 100,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	if (!(*client)->ReadSnapshot().has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Initial snapshot was not available.";
		return;
	}
	protocol::Command command;
	command.mutable_move_requested()->set_direction_x(1);
	if ((*client)->QueueCommand(command) != 1 || !(*client)->SendQueuedCommands(0).has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Command was not sent.";
		return;
	}
	auto acknowledgement = (*client)->ReceiveCommandAcknowledgement(0);
	EXPECT_FALSE(acknowledgement.has_value());
	EXPECT_EQ((*client)->PendingTrackedCommandCount(), 1U);
	auto afterTimeout = (*client)->RequestSnapshot();
	if (afterTimeout.has_value())
		ADD_FAILURE() << "A request was accepted after the gated transport timeout.";
	else
		EXPECT_NE(afterTimeout.error(), "Timed out reading envelope.");
	auto reconnect = (*client)->Reconnect(1'000);
	if (!reconnect.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << reconnect.error();
		return;
	}
	(*client)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_EQ(resentCommandBytes, firstCommandBytes);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedClient, ClockGatedTerminalAcknowledgementRequiresFreshProjection)
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
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(100);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;
		protocol::Envelope command;
		if (!ReadEnvelopeWithin(socket, command, deadline)
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1)
			return;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(1);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		if (!WriteEnvelopeWithin(socket, acknowledgement, deadline))
			return;
		observed = true;
		std::this_thread::sleep_for(std::chrono::milliseconds(300));
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
		.supportsClockGatedCommands = true,
		.gatedReceiveTimeoutMs = 100,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	protocol::CommandBatch batch;
	batch.add_commands()->mutable_move_requested()->set_direction_x(1);
	auto acknowledgement = (*client)->Submit(batch);
	if (!acknowledgement.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << acknowledgement.error();
		return;
	}
	EXPECT_EQ(acknowledgement->results_size(), 1);
	auto projection = (*client)->ReadSnapshot();
	if (projection.has_value())
		ADD_FAILURE() << "ReadSnapshot returned the cached handshake snapshot after the terminal ACK.";
	else
		EXPECT_EQ(projection.error(), "Timed out reading envelope.");
	(*client)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedClient, ClockGatedRetryFenceUsesMeasuredRoundTripAboveNormalRto)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	std::string firstCommandBytes;
	std::string retryCommandBytes;
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
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(socket, serverHello, deadline))
			return;
		protocol::Envelope initial;
		initial.mutable_snapshot()->set_tick(100);
		if (!WriteEnvelopeWithin(socket, initial, deadline))
			return;
		protocol::Envelope request;
		if (!ReadEnvelopeWithin(socket, request, deadline)
		    || request.payload_case() != protocol::Envelope::kSnapshotRequest
		    || request.snapshot_request().timing_request_id() == 0)
			return;
		std::this_thread::sleep_for(std::chrono::milliseconds(350));
		const uint64_t requestId = request.snapshot_request().timing_request_id();
		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->set_tick(101);
		if (!WriteEnvelopeWithin(socket, snapshot, deadline))
			return;
		protocol::Envelope timing;
		timing.mutable_timing_reply()->set_request_id(requestId);
		if (!WriteEnvelopeWithin(socket, timing, deadline))
			return;
		protocol::Envelope firstCommand;
		if (!ReadEnvelopeWithin(socket, firstCommand, deadline)
		    || firstCommand.payload_case() != protocol::Envelope::kCommandBatch
		    || firstCommand.command_batch().commands_size() != 1)
			return;
		firstCommandBytes = firstCommand.SerializeAsString();
		protocol::Envelope retry;
		if (!ReadEnvelopeWithin(socket, retry, deadline)
		    || retry.payload_case() != protocol::Envelope::kCommandBatch
		    || retry.command_batch().commands_size() != 1
		    || retry.command_batch().commands(0).client_sequence()
		        != firstCommand.command_batch().commands(0).client_sequence()
		    || retry.command_batch().commands(0).requested_tick()
		        != firstCommand.command_batch().commands(0).requested_tick())
			return;
		retryCommandBytes = retry.SerializeAsString();
		observed = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
		.supportsClockGatedCommands = true,
		.gatedReceiveTimeoutMs = 1'500,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	if (!(*client)->ReadSnapshot().has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Initial snapshot was not available.";
		return;
	}
	auto freshSnapshot = (*client)->RequestSnapshot();
	if (!freshSnapshot.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << freshSnapshot.error();
		return;
	}
	protocol::Command command;
	command.mutable_move_requested()->set_direction_x(1);
	if ((*client)->QueueCommand(command) != 1 || !(*client)->SendQueuedCommands(0).has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Command was not sent.";
		return;
	}
	auto prematureResubmissions = (*client)->PrepareTrackedResubmissions(300);
	if (!prematureResubmissions.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << prematureResubmissions.error();
		return;
	}
	EXPECT_TRUE(prematureResubmissions->empty());
	auto resubmissions = (*client)->PrepareTrackedResubmissions(1'000);
	if (!resubmissions.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << resubmissions.error();
		return;
	}
	EXPECT_EQ(resubmissions->size(), 1U);
	(*client)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_EQ(retryCommandBytes, firstCommandBytes);
	EXPECT_FALSE(server.Failed());
}

TEST(ServerBackedClient, RejectsUnknownSchedulingMode)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		if (!AcceptWithin(acceptor, socket, deadline))
			return;
		protocol::Envelope hello;
		if (!ReadEnvelopeWithin(socket, hello, deadline))
			return;
		protocol::Envelope response;
		response.mutable_server_hello()->set_protocol_schema_version("1");
		response.mutable_server_hello()->set_content_manifest_hash("content");
		response.mutable_server_hello()->set_command_scheduling_mode(static_cast<protocol::CommandSchedulingMode>(99));
		(void)WriteEnvelopeWithin(socket, response, deadline);
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.supportsClockGatedCommands = true,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (client.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Unknown scheduling mode was accepted.";
		return;
	}
	EXPECT_EQ(client.error(), "Authoritative server returned an unknown command scheduling mode.");
	server.Join();
}

TEST(ServerBackedClient, PreservesSnapshotRecoveryWhenSequenceHighWaterIsExhausted)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket socket(serverIo);
		if (!AcceptWithin(acceptor, socket, deadline))
			return;
		protocol::Envelope hello;
		if (!ReadEnvelopeWithin(socket, hello, deadline))
			return;
		protocol::Envelope response;
		auto *serverHello = response.mutable_server_hello();
		serverHello->set_protocol_schema_version("1");
		serverHello->set_content_manifest_hash("content");
		serverHello->set_last_client_sequence(std::numeric_limits<uint64_t>::max());
		if (!WriteEnvelopeWithin(socket, response, deadline))
			return;
		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->set_tick(1);
		(void)WriteEnvelopeWithin(socket, snapshot, deadline);
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
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	auto snapshot = (*client)->ReadSnapshot();
	if (!snapshot.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << snapshot.error();
		return;
	}
	protocol::Command command;
	EXPECT_EQ((*client)->QueueCommand(command), 0U);
	(*client)->Close();
	server.Join();
}

TEST(ServerBackedClient, ResubmitsMaximumSequenceAcrossExhaustedReconnectFloor)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket first(serverIo);
		if (!AcceptWithin(acceptor, first, deadline))
			return;
		protocol::Envelope firstHello;
		if (!ReadEnvelopeWithin(first, firstHello, deadline))
			return;
		protocol::Envelope firstServerHello;
		firstServerHello.mutable_server_hello()->set_protocol_schema_version("1");
		firstServerHello.mutable_server_hello()->set_content_manifest_hash("content");
		firstServerHello.mutable_server_hello()->set_session_token("resume");
		firstServerHello.mutable_server_hello()->set_last_client_sequence(std::numeric_limits<uint64_t>::max() - 1);
		if (!WriteEnvelopeWithin(first, firstServerHello, deadline))
			return;
		protocol::Envelope firstSnapshot;
		firstSnapshot.mutable_snapshot()->set_tick(30);
		if (!WriteEnvelopeWithin(first, firstSnapshot, deadline))
			return;
		protocol::Envelope firstCommand;
		if (!ReadEnvelopeWithin(first, firstCommand, deadline)
		    || firstCommand.command_batch().commands_size() != 1
		    || firstCommand.command_batch().commands(0).client_sequence() != std::numeric_limits<uint64_t>::max())
			return;
		first.close();

		tcp::socket resumed(serverIo);
		if (!AcceptWithin(acceptor, resumed, deadline))
			return;
		protocol::Envelope resumedHello;
		if (!ReadEnvelopeWithin(resumed, resumedHello, deadline))
			return;
		protocol::Envelope resumedServerHello;
		resumedServerHello.mutable_server_hello()->set_protocol_schema_version("1");
		resumedServerHello.mutable_server_hello()->set_content_manifest_hash("content");
		resumedServerHello.mutable_server_hello()->set_session_token("resume");
		resumedServerHello.mutable_server_hello()->set_last_client_sequence(std::numeric_limits<uint64_t>::max());
		if (!WriteEnvelopeWithin(resumed, resumedServerHello, deadline))
			return;
		protocol::Envelope resumedSnapshot;
		resumedSnapshot.mutable_snapshot()->set_tick(20);
		if (!WriteEnvelopeWithin(resumed, resumedSnapshot, deadline))
			return;
		protocol::Envelope resent;
		if (!ReadEnvelopeWithin(resumed, resent, deadline)
		    || resent.command_batch().commands_size() != 1
		    || resent.command_batch().commands(0).client_sequence() != std::numeric_limits<uint64_t>::max())
			return;
		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(std::numeric_limits<uint64_t>::max());
		result->set_status(protocol::COMMAND_STATUS_DUPLICATE);
		if (!WriteEnvelopeWithin(resumed, acknowledgement, deadline))
			return;
		observed = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
		.supportsClockGatedCommands = true,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	if (!(*client)->ReadSnapshot().has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Initial snapshot was not available.";
		return;
	}
	protocol::Command command;
	command.set_requested_tick(31);
	command.mutable_move_requested()->set_direction_x(1);
	EXPECT_EQ((*client)->QueueCommand(command), std::numeric_limits<uint64_t>::max());
	auto send = (*client)->SendQueuedCommands(0);
	if (!send.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << send.error();
		return;
	}
	auto reconnect = (*client)->Reconnect(100);
	if (!reconnect.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << reconnect.error();
		return;
	}
	if (!(*client)->ReadSnapshot().has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Reconnect snapshot was not available.";
		return;
	}
	auto acknowledgement = (*client)->ReceiveCommandAcknowledgement(125);
	if (!acknowledgement.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << acknowledgement.error();
		return;
	}
	EXPECT_EQ((*client)->PendingTrackedCommandCount(), 0U);
	(*client)->Close();
	server.Join();
	EXPECT_TRUE(observed);
}

TEST(ServerBackedClient, PreservesZeroRequestedTickAcrossImmediateToClockGatedReconnect)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { asio::ip::address_v4::loopback(), 0 } };
	std::atomic_bool observed = false;
	std::string firstCommandBytes;
	std::string resentCommandBytes;
	PeerThread server([&]() {
		const auto deadline = SteadyClock::now() + PeerIoTimeout;
		tcp::socket first(serverIo);
		if (!AcceptWithin(acceptor, first, deadline))
			return;
		protocol::Envelope firstHello;
		if (!ReadEnvelopeWithin(first, firstHello, deadline))
			return;
		protocol::Envelope immediateHello;
		immediateHello.mutable_server_hello()->set_protocol_schema_version("1");
		immediateHello.mutable_server_hello()->set_content_manifest_hash("content");
		immediateHello.mutable_server_hello()->set_session_token("resume");
		immediateHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_IMMEDIATE);
		if (!WriteEnvelopeWithin(first, immediateHello, deadline))
			return;
		protocol::Envelope firstSnapshot;
		firstSnapshot.mutable_snapshot()->set_tick(10);
		if (!WriteEnvelopeWithin(first, firstSnapshot, deadline))
			return;
		protocol::Envelope firstCommand;
		if (!ReadEnvelopeWithin(first, firstCommand, deadline)
		    || firstCommand.command_batch().commands_size() != 1
		    || firstCommand.command_batch().commands(0).requested_tick() != 0)
			return;
		firstCommandBytes = firstCommand.SerializeAsString();
		first.close();

		tcp::socket resumed(serverIo);
		if (!AcceptWithin(acceptor, resumed, deadline))
			return;
		protocol::Envelope resumedHello;
		if (!ReadEnvelopeWithin(resumed, resumedHello, deadline))
			return;
		protocol::Envelope gatedHello;
		gatedHello.mutable_server_hello()->set_protocol_schema_version("1");
		gatedHello.mutable_server_hello()->set_content_manifest_hash("content");
		gatedHello.mutable_server_hello()->set_session_token("resume");
		gatedHello.mutable_server_hello()->set_tick_rate_hz(20);
		gatedHello.mutable_server_hello()->set_command_scheduling_mode(protocol::COMMAND_SCHEDULING_MODE_CLOCK_GATED);
		if (!WriteEnvelopeWithin(resumed, gatedHello, deadline))
			return;
		protocol::Envelope resumedSnapshot;
		resumedSnapshot.mutable_snapshot()->set_tick(10);
		if (!WriteEnvelopeWithin(resumed, resumedSnapshot, deadline))
			return;
		protocol::Envelope resentCommand;
		if (!ReadEnvelopeWithin(resumed, resentCommand, deadline)
		    || resentCommand.command_batch().commands_size() != 1
		    || resentCommand.command_batch().commands(0).requested_tick() != 0)
			return;
		resentCommandBytes = resentCommand.SerializeAsString();
		observed = true;
	});

	ServerBackedClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
		.supportsClockGatedCommands = true,
		.gatedReceiveTimeoutMs = 500,
	};
	auto client = ServerBackedClient::Connect(configuration);
	if (!client.has_value()) {
		server.Join();
		ADD_FAILURE() << client.error();
		return;
	}
	if (!(*client)->ReadSnapshot().has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Initial snapshot was not available.";
		return;
	}
	protocol::Command command;
	command.mutable_move_requested()->set_direction_x(1);
	if ((*client)->QueueCommand(command) != 1 || !(*client)->SendQueuedCommands(100).has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << "Initial immediate command was not sent.";
		return;
	}
	auto reconnect = (*client)->Reconnect(200);
	if (!reconnect.has_value()) {
		(*client)->Close();
		server.Join();
		ADD_FAILURE() << reconnect.error();
		return;
	}
	(*client)->Close();
	server.Join();
	EXPECT_TRUE(observed);
	EXPECT_EQ(resentCommandBytes, firstCommandBytes);
}

} // namespace
} // namespace devilution::authoritative
