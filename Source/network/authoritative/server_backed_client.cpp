/**
 * @file network/authoritative/server_backed_client.cpp
 *
 * Implementation of the server-backed client.
 */

#include "network/authoritative/server_backed_client.hpp"

#include <chrono>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iterator>
#include <sstream>
#include <span>
#include <utility>

#include <asio/connect.hpp>

#include "network/authoritative/envelope_codec.hpp"

namespace devilution::authoritative {
namespace {

tl::expected<protocol::Envelope, std::string> ParseEnvelope(const EnvelopeCodec::Payload &payload)
{
	protocol::Envelope envelope;
	if (!envelope.ParseFromArray(payload.data(), static_cast<int>(payload.size())))
		return tl::make_unexpected(std::string("The envelope payload is not valid Protobuf."));
	return envelope;
}

std::string ProtocolErrorMessage(const protocol::Envelope &envelope)
{
	if (envelope.payload_case() == protocol::Envelope::kError)
		return "Authoritative server rejected the message: " + envelope.error().detail();
	return "Unexpected authoritative server message.";
}

std::string DiagnosticValue(std::string_view value)
{
	std::string result(value);
	for (char &character : result) {
		if (character == '\r' || character == '\n')
			character = ' ';
	}
	return result;
}

tl::expected<CommandAckStatus, std::string> ToCommandAckStatus(protocol::CommandStatus status)
{
	switch (status) {
	case protocol::COMMAND_STATUS_ACCEPTED:
		return CommandAckStatus::Accepted;
	case protocol::COMMAND_STATUS_REJECTED:
		return CommandAckStatus::Rejected;
	case protocol::COMMAND_STATUS_RESCHEDULED:
		return CommandAckStatus::Rescheduled;
	case protocol::COMMAND_STATUS_DUPLICATE:
		return CommandAckStatus::Duplicate;
	case protocol::COMMAND_STATUS_UNSPECIFIED:
		break;
	}
	return tl::make_unexpected(std::string("Authoritative server returned an unspecified command status."));
}

bool IsValidCommandRejectReason(protocol::CommandRejectReason reason)
{
	switch (reason) {
	case protocol::COMMAND_REJECT_REASON_UNSPECIFIED:
	case protocol::COMMAND_REJECT_REASON_MALFORMED:
	case protocol::COMMAND_REJECT_REASON_TOO_LATE:
	case protocol::COMMAND_REJECT_REASON_NOT_ALLOWED:
	case protocol::COMMAND_REJECT_REASON_INVALID_TARGET:
	case protocol::COMMAND_REJECT_REASON_INSUFFICIENT_RESOURCES:
		return true;
	}
	return false;
}

tl::expected<void, std::string> ValidateCommandAcknowledgement(const protocol::CommandAck &acknowledgement)
{
	if (acknowledgement.results_size() > MaximumCommandAcknowledgementResultCount)
		return tl::make_unexpected(std::string("The authoritative command acknowledgement is too large."));

	for (const auto &result : acknowledgement.results()) {
		if (result.status() == protocol::COMMAND_STATUS_UNSPECIFIED
		    || ToCommandAckStatus(result.status()).has_value() == false
		    || !IsValidCommandRejectReason(result.reject_reason())
		    || (result.status() != protocol::COMMAND_STATUS_REJECTED
		        && result.status() != protocol::COMMAND_STATUS_DUPLICATE
		        && result.reject_reason() != protocol::COMMAND_REJECT_REASON_UNSPECIFIED)) {
			return tl::make_unexpected(std::string("The authoritative command acknowledgement contains an invalid result."));
		}
	}
	return {};
}

} // namespace

ServerBackedClient::ServerBackedClient() = default;

tl::expected<std::unique_ptr<ServerBackedClient>, std::string> ServerBackedClient::Connect(Configuration configuration)
{
	if (configuration.host.empty() || configuration.port == 0
	    || configuration.clientBuildId.empty() || configuration.protocolSchemaVersion.empty()
	    || configuration.contentManifestHash.empty())
		return tl::make_unexpected(std::string("Server-backed client configuration is incomplete."));

	auto client = std::unique_ptr<ServerBackedClient>(new ServerBackedClient());
	client->configuration_ = std::move(configuration);
	if (auto result = client->ConnectTransport(client->configuration_.expectInitialSnapshot); !result.has_value())
		return tl::make_unexpected(result.error());
	return client;
}

tl::expected<void, std::string> ServerBackedClient::Reconnect(uint64_t nowMs)
{
	Close();
	pendingSnapshot_.reset();
	pendingEventBatches_.clear();
	if (auto result = ConnectTransport(true); !result.has_value())
		return result;

	for (auto &entry : pendingCommands_)
		entry.second.sent = false;
	return SendQueuedCommands(nowMs);
}

tl::expected<void, std::string> ServerBackedClient::ConnectTransport(bool expectInitialSnapshot)
{
	asio::error_code error;
	const auto endpoints = resolver_.resolve(configuration_.host, std::to_string(configuration_.port), error);
	if (error)
		return tl::make_unexpected("Resolving authoritative server: " + error.message());
	asio::connect(socket_, endpoints, error);
	if (error)
		return tl::make_unexpected("Connecting to authoritative server: " + error.message());

	protocol::Envelope hello;
	auto *clientHello = hello.mutable_client_hello();
	clientHello->set_client_build_id(configuration_.clientBuildId);
	clientHello->set_protocol_schema_version(configuration_.protocolSchemaVersion);
	clientHello->set_content_manifest_hash(configuration_.contentManifestHash);
	if (!configuration_.rulesetIdentityHash.empty())
		clientHello->set_ruleset_identity_hash(configuration_.rulesetIdentityHash);
	clientHello->set_resume_token(configuration_.resumeToken);
	if (configuration_.requestedCharacterClassId.has_value())
		clientHello->set_requested_character_class_id(*configuration_.requestedCharacterClassId);
	if (configuration_.requestedCharacterName.has_value())
		clientHello->set_requested_character_name(*configuration_.requestedCharacterName);
	if (auto result = WriteEnvelope(hello); !result.has_value())
		return tl::make_unexpected(result.error());

	auto response = ReadEnvelope();
	if (!response.has_value())
		return tl::make_unexpected(response.error());
	if (response->payload_case() != protocol::Envelope::kServerHello) {
		const std::string message = ProtocolErrorMessage(*response);
		const std::string diagnosticPath = WriteHandshakeDiagnosticDump("server-rejected-handshake", &*response);
		return tl::make_unexpected(message + (diagnosticPath.empty() ? std::string {} : " Diagnostic dump: " + diagnosticPath));
	}
	const auto &serverHello = response->server_hello();
	const bool protocolMismatch = serverHello.protocol_schema_version() != configuration_.protocolSchemaVersion;
	const bool contentMismatch = serverHello.content_manifest_hash() != configuration_.contentManifestHash;
	const bool rulesetMismatch = !configuration_.rulesetIdentityHash.empty()
	    && serverHello.ruleset_identity_hash() != configuration_.rulesetIdentityHash;
	if (protocolMismatch || contentMismatch || rulesetMismatch) {
		const std::string reason = protocolMismatch ? "protocol-mismatch" : contentMismatch ? "content-hash-mismatch" : "ruleset-hash-mismatch";
		const std::string diagnosticPath = WriteHandshakeDiagnosticDump(reason, &*response);
		const std::string detail = "Authoritative server returned incompatible protocol, content, or ruleset identity."
		    + (diagnosticPath.empty() ? std::string {} : " Diagnostic dump: " + diagnosticPath);
		return tl::make_unexpected(detail);
	}

	serverHello_ = response->server_hello();
	configuration_.contentManifestHash = serverHello_.content_manifest_hash();
	configuration_.rulesetIdentityHash = serverHello_.ruleset_identity_hash();
	if (!serverHello_.session_token().empty())
		configuration_.resumeToken = serverHello_.session_token();
	if (expectInitialSnapshot) {
		auto initialSnapshot = ReadEnvelope();
		if (!initialSnapshot.has_value())
			return tl::make_unexpected(initialSnapshot.error());
		if (initialSnapshot->payload_case() != protocol::Envelope::kSnapshot)
			return tl::make_unexpected(ProtocolErrorMessage(*initialSnapshot));
		if (auto identity = ObserveSnapshotIdentity(initialSnapshot->snapshot()); !identity.has_value())
			return identity;
		pendingSnapshot_ = initialSnapshot->snapshot();
	}
	return {};
}

std::string ServerBackedClient::WriteHandshakeDiagnosticDump(std::string_view reason, const protocol::Envelope *response) const noexcept
{
	try {
		const std::filesystem::path directory = configuration_.diagnosticsDirectory.empty()
		    ? std::filesystem::path("authoritative-diagnostics")
		    : std::filesystem::path(configuration_.diagnosticsDirectory);
		std::filesystem::create_directories(directory);

		const auto now = std::chrono::system_clock::now();
		const auto milliseconds = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;
		const std::time_t time = std::chrono::system_clock::to_time_t(now);
		std::tm calendarTime {};
#if defined(_WIN32)
		localtime_s(&calendarTime, &time);
#else
		localtime_r(&time, &calendarTime);
#endif
		std::ostringstream filename;
		filename << "handshake-mismatch-" << std::put_time(&calendarTime, "%Y%m%d-%H%M%S")
		         << '-' << std::setfill('0') << std::setw(3) << milliseconds.count() << ".txt";
		const std::filesystem::path path = directory / filename.str();
		std::ofstream dump(path, std::ios::binary);
		if (!dump)
			return {};

		dump << "reason=" << DiagnosticValue(reason) << '\n';
		dump << "endpoint=" << DiagnosticValue(configuration_.host) << ':' << configuration_.port << '\n';
		dump << "working_directory=" << std::filesystem::current_path().string() << '\n';
		dump << "expected.client_build_id=" << DiagnosticValue(configuration_.clientBuildId) << '\n';
		dump << "expected.protocol_schema_version=" << DiagnosticValue(configuration_.protocolSchemaVersion) << '\n';
		dump << "expected.content_manifest_hash=" << DiagnosticValue(configuration_.contentManifestHash) << '\n';
		dump << "expected.ruleset_identity_hash=" << DiagnosticValue(configuration_.rulesetIdentityHash) << '\n';
		if (response != nullptr && response->payload_case() == protocol::Envelope::kServerHello) {
			const auto &serverHello = response->server_hello();
			dump << "actual.server_build_id=" << DiagnosticValue(serverHello.server_build_id()) << '\n';
			dump << "actual.protocol_schema_version=" << DiagnosticValue(serverHello.protocol_schema_version()) << '\n';
			dump << "actual.content_manifest_hash=" << DiagnosticValue(serverHello.content_manifest_hash()) << '\n';
			dump << "actual.ruleset_identity_hash=" << DiagnosticValue(serverHello.ruleset_identity_hash()) << '\n';
			dump << "actual.tick_rate_hz=" << serverHello.tick_rate_hz() << '\n';
			dump << "actual.game_mode=" << protocol::GameMode_Name(serverHello.game_mode()) << '\n';
		} else if (response != nullptr && response->payload_case() == protocol::Envelope::kError) {
			dump << "server_error.code=" << response->error().code() << '\n';
			dump << "server_error.detail=" << DiagnosticValue(response->error().detail()) << '\n';
		} else {
			dump << "actual.response_payload=unexpected-or-unavailable\n";
		}
		dump.flush();
		if (!dump)
			return {};
		return path.string();
	} catch (...) {
		return {};
	}
}

tl::expected<protocol::CommandAck, std::string> ServerBackedClient::Submit(const protocol::CommandBatch &batch)
{
	protocol::Envelope request;
	*request.mutable_command_batch() = batch;
	if (auto result = WriteEnvelope(request); !result.has_value())
		return tl::make_unexpected(result.error());

	auto response = ReadEnvelope();
	if (!response.has_value())
		return tl::make_unexpected(response.error());
	if (response->payload_case() != protocol::Envelope::kCommandAck)
		return tl::make_unexpected(ProtocolErrorMessage(*response));
	if (auto validation = ValidateCommandAcknowledgement(response->command_ack()); !validation.has_value())
		return tl::make_unexpected(validation.error());
	return response->command_ack();
}

tl::expected<protocol::Snapshot, std::string> ServerBackedClient::ReadSnapshot()
{
	if (pendingSnapshot_.has_value()) {
		protocol::Snapshot snapshot = std::move(*pendingSnapshot_);
		pendingSnapshot_.reset();
		if (auto identity = ObserveSnapshotIdentity(snapshot); !identity.has_value())
			return tl::make_unexpected(identity.error());
		return snapshot;
	}

	for (;;) {
		auto response = ReadEnvelope();
		if (!response.has_value())
			return tl::make_unexpected(response.error());
		if (response->payload_case() == protocol::Envelope::kEventBatch) {
			pendingEventBatches_.push_back(response->event_batch());
			continue;
		}
		if (response->payload_case() != protocol::Envelope::kSnapshot)
			return tl::make_unexpected(ProtocolErrorMessage(*response));
		if (auto identity = ObserveSnapshotIdentity(response->snapshot()); !identity.has_value())
			return tl::make_unexpected(identity.error());
		return response->snapshot();
	}
}

tl::expected<void, std::string> ServerBackedClient::ObserveSnapshotIdentity(const protocol::Snapshot &snapshot)
{
	const bool hasContentManifestHash = !snapshot.content_manifest_hash().empty();
	const bool hasRulesetIdentityHash = !snapshot.ruleset_identity_hash().empty();
	if (!hasContentManifestHash && !hasRulesetIdentityHash)
		return {};
	if (hasContentManifestHash != hasRulesetIdentityHash)
		return tl::make_unexpected(std::string("The authoritative snapshot contains an incomplete ruleset identity."));
	serverHello_.set_content_manifest_hash(snapshot.content_manifest_hash());
	serverHello_.set_ruleset_identity_hash(snapshot.ruleset_identity_hash());
	configuration_.contentManifestHash = snapshot.content_manifest_hash();
	configuration_.rulesetIdentityHash = snapshot.ruleset_identity_hash();
	return {};
}

tl::expected<protocol::Snapshot, std::string> ServerBackedClient::RequestSnapshot()
{
	protocol::Envelope request;
	request.mutable_snapshot_request();
	if (auto result = WriteEnvelope(request); !result.has_value())
		return tl::make_unexpected(result.error());
	return ReadSnapshot();
}

std::vector<protocol::EventBatch> ServerBackedClient::TakePendingEventBatches()
{
	std::vector<protocol::EventBatch> events = std::move(pendingEventBatches_);
	pendingEventBatches_.clear();
	return events;
}

uint64_t ServerBackedClient::QueueCommand(protocol::Command command)
{
	const uint64_t sequence = deliveryTracker_.RegisterCommand();
	command.set_client_sequence(sequence);
	pendingCommands_.emplace(sequence, PendingCommand { .command = std::move(command) });
	return sequence;
}

tl::expected<void, std::string> ServerBackedClient::SendQueuedCommands(uint64_t nowMs)
{
	protocol::CommandBatch batch;
	std::vector<uint64_t> sequences;
	for (auto &[sequence, pending] : pendingCommands_) {
		if (pending.sent)
			continue;
		*batch.add_commands() = pending.command;
		sequences.push_back(sequence);
	}
	if (sequences.empty())
		return {};

	protocol::Envelope envelope;
	*envelope.mutable_command_batch() = std::move(batch);
	if (auto result = WriteEnvelope(envelope); !result.has_value())
		return result;
	for (const uint64_t sequence : sequences) {
		if (!deliveryTracker_.MarkSent(sequence, nowMs))
			return tl::make_unexpected(std::string("Tracked command disappeared before it was marked sent."));
		pendingCommands_.at(sequence).sent = true;
	}
	return {};
}

tl::expected<std::vector<uint64_t>, std::string> ServerBackedClient::PrepareTrackedResubmissions(uint64_t nowMs)
{
	const std::vector<uint64_t> sequences = deliveryTracker_.PrepareResubmissions(nowMs);
	if (sequences.empty())
		return sequences;

	protocol::CommandBatch batch;
	for (const uint64_t sequence : sequences) {
		const auto pending = pendingCommands_.find(sequence);
		if (pending == pendingCommands_.end())
			return tl::make_unexpected(std::string("Tracked command disappeared before resubmission."));
		*batch.add_commands() = pending->second.command;
	}

	protocol::Envelope envelope;
	*envelope.mutable_command_batch() = std::move(batch);
	if (auto result = WriteEnvelope(envelope); !result.has_value())
		return tl::make_unexpected(result.error());
	return sequences;
}

tl::expected<protocol::CommandAck, std::string> ServerBackedClient::ReceiveCommandAcknowledgement(uint64_t nowMs)
{
	auto response = ReadEnvelope();
	if (!response.has_value())
		return tl::make_unexpected(response.error());
	if (response->payload_case() != protocol::Envelope::kCommandAck)
		return tl::make_unexpected(ProtocolErrorMessage(*response));
	if (auto validation = ValidateCommandAcknowledgement(response->command_ack()); !validation.has_value())
		return tl::make_unexpected(validation.error());

	for (const protocol::CommandResult &result : response->command_ack().results()) {
		auto status = ToCommandAckStatus(result.status());
		if (!status.has_value())
			return tl::make_unexpected(status.error());
		const CommandAcknowledgement acknowledgement {
			.clientSequence = result.client_sequence(),
			.serverReceiptSequence = result.server_receipt_sequence(),
			.appliedTick = result.applied_tick(),
			.status = status.value(),
		};
		if (deliveryTracker_.ApplyAcknowledgement(acknowledgement, nowMs))
			pendingCommands_.erase(result.client_sequence());
	}
	return response->command_ack();
}

void ServerBackedClient::Close() noexcept
{
	if (!socket_.is_open())
		return;
	asio::error_code ignored;
	socket_.shutdown(asio::ip::tcp::socket::shutdown_both, ignored);
	socket_.close(ignored);
}

ServerBackedClient::~ServerBackedClient()
{
	Close();
}

tl::expected<protocol::Envelope, std::string> ServerBackedClient::ReadEnvelope()
{
	auto payload = EnvelopeCodec::Read(socket_);
	if (!payload.has_value())
		return tl::make_unexpected(payload.error());
	if (!payload->has_value())
		return tl::make_unexpected(std::string("Authoritative server closed the connection."));
	return ParseEnvelope(payload->value());
}

tl::expected<void, std::string> ServerBackedClient::WriteEnvelope(const protocol::Envelope &envelope)
{
	const std::string payload = envelope.SerializeAsString();
	return EnvelopeCodec::Write(socket_, std::span<const uint8_t>(
	    reinterpret_cast<const uint8_t *>(payload.data()), payload.size()));
}

} // namespace devilution::authoritative
