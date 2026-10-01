#pragma once

/**
 * @file network/authoritative/server_backed_client.hpp
 *
 * Opt-in C++ client for the C# authoritative server.
 */

#include <cstdint>
#include <chrono>
#include <map>
#include <memory>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#include <asio/ip/tcp.hpp>

#include <expected.hpp>

#include "devilution.pb.h"
#include "network/protocol/command_delivery.hpp"

namespace devilution::authoritative {

namespace protocol = ::devilution::protocol::v1;

class ServerBackedClient {
public:
	struct Configuration {
		std::string host;
		uint16_t port = 0;
		std::string clientBuildId;
		std::string protocolSchemaVersion;
		std::string contentManifestHash;
		/** Optional additive ruleset identity for newer servers. */
		std::string rulesetIdentityHash;
		std::string resumeToken;
		/** Stable active-content class ID requested for a new session. */
		std::optional<uint32_t> requestedCharacterClassId;
		/** Native-compatible character identity requested for a new session. */
		std::optional<std::string> requestedCharacterName;
		/** Directory for detailed handshake failure dumps. */
		std::string diagnosticsDirectory;
		bool expectInitialSnapshot = false;
		/** Explicit opt-in; legacy clients never advertise deferred scheduling. */
		bool supportsClockGatedCommands = false;
		/** Absolute transaction deadline for gated ACK/snapshot exchanges. */
		uint64_t gatedReceiveTimeoutMs = 2000;
	};

	static tl::expected<std::unique_ptr<ServerBackedClient>, std::string> Connect(Configuration configuration);

	~ServerBackedClient();

	ServerBackedClient(const ServerBackedClient &) = delete;
	ServerBackedClient &operator=(const ServerBackedClient &) = delete;

	const protocol::ServerHello &ServerHello() const noexcept { return serverHello_; }
	[[nodiscard]] bool IsClockGated() const noexcept { return clockGated_; }

	/** Resumes the current server session and resubmits unresolved commands. */
	tl::expected<void, std::string> Reconnect(uint64_t nowMs);

	tl::expected<protocol::CommandAck, std::string> Submit(const protocol::CommandBatch &batch);
	tl::expected<protocol::Snapshot, std::string> ReadSnapshot();
	/** Requests a fresh snapshot for autonomous server-side simulation ticks. */
	tl::expected<protocol::Snapshot, std::string> RequestSnapshot();

	/** Returns transient gameplay events received before the latest snapshot. */
	std::vector<protocol::EventBatch> TakePendingEventBatches();

	/** Adds a command and assigns its session-scoped retry sequence. */
	uint64_t QueueCommand(protocol::Command command);

	/** Sends every queued command that has not been sent yet. */
	tl::expected<void, std::string> SendQueuedCommands(uint64_t nowMs);

	/** Resends commands whose adaptive acknowledgement timeout has elapsed. */
	tl::expected<std::vector<uint64_t>, std::string> PrepareTrackedResubmissions(uint64_t nowMs);

	/** Reads and applies one server acknowledgement batch. */
	tl::expected<protocol::CommandAck, std::string> ReceiveCommandAcknowledgement(uint64_t nowMs);

	[[nodiscard]] std::size_t PendingTrackedCommandCount() const noexcept { return pendingCommands_.size(); }

	void Close() noexcept;

private:
	ServerBackedClient();

	tl::expected<void, std::string> ConnectTransport(bool expectInitialSnapshot);
	tl::expected<protocol::Envelope, std::string> ReadEnvelope();
	tl::expected<protocol::Envelope, std::string> ReadEnvelope(std::chrono::steady_clock::time_point deadline);
	tl::expected<void, std::string> WriteEnvelope(const protocol::Envelope &envelope);
	tl::expected<void, std::string> WriteEnvelope(const protocol::Envelope &envelope, std::chrono::steady_clock::time_point deadline);
	tl::expected<void, std::string> ObserveSnapshotIdentity(const protocol::Snapshot &snapshot);
	tl::expected<void, std::string> ObserveTimingReply(const protocol::TimingReply &reply);
	[[nodiscard]] uint64_t GatedCurrentTick() const noexcept;
	[[nodiscard]] uint64_t GatedDispatchTick() const noexcept;
	[[nodiscard]] uint64_t GatedRetryNotBefore(uint64_t requestedTick, uint64_t nowMs) const noexcept;
	void CloseAfterGatedTimeout() noexcept;
	[[nodiscard]] std::string WriteHandshakeDiagnosticDump(std::string_view reason, const protocol::Envelope *response) const noexcept;

	asio::io_context ioContext_;
	asio::ip::tcp::resolver resolver_ { ioContext_ };
	asio::ip::tcp::socket socket_ { ioContext_ };
	Configuration configuration_;
	protocol::ServerHello serverHello_;
	std::optional<protocol::Snapshot> pendingSnapshot_;
	std::vector<protocol::EventBatch> pendingEventBatches_;
	CommandDeliveryTracker deliveryTracker_;
	bool clockGated_ = false;
	uint32_t tickRateHz_ = 20;
	uint64_t gatedRoundTripMs_ = 100;
	uint64_t lastSnapshotTick_ = 0;
	bool hasSnapshotClock_ = false;
	std::chrono::steady_clock::time_point lastSnapshotObservedAt_ {};
	std::optional<std::chrono::steady_clock::time_point> gatedTransactionDeadline_;
	uint64_t nextTimingRequestId_ = 1;
	struct PendingTimingProbe {
		uint64_t requestId = 0;
		std::chrono::steady_clock::time_point sentAt;
		std::chrono::steady_clock::time_point expiresAt;
	};
	std::optional<PendingTimingProbe> pendingTimingProbe_;
	struct PendingCommand {
		protocol::Command command;
		bool sent = false;
		/** True once the command has been assembled into a first-write batch. */
		bool prepared = false;
	};
	std::map<uint64_t, PendingCommand> pendingCommands_;
};

} // namespace devilution::authoritative
