#include <algorithm>
#include <array>
#include <atomic>
#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <thread>
#include <tuple>
#include <utility>
#include <vector>

#include <asio/ip/tcp.hpp>
#include <asio/post.hpp>
#include <asio/read.hpp>
#include <asio/write.hpp>

#include <gtest/gtest.h>

#include "application/diablo.h"
#include "application/headless_mode.hpp"
#include "devilution.pb.h"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/world.hpp"
#include "game/items/item_pool.hpp"
#include "game/items/items.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/dungeon_common_defs.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/monsters/monsters.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "game/quests/quests.hpp"
#include "network/authoritative/authoritative_level_id.hpp"
#include "network/authoritative/envelope_codec.hpp"
#include "network/authoritative/server_backed_runtime.hpp"
#include "network/authoritative/server_backed_world_presentation.hpp"
#include "tables/objdat.h"
#include "utils/endian/endian_write.hpp"
#include "utils/paths.h"

namespace devilution::authoritative {
namespace {

namespace protocol = ::devilution::protocol::v1;
using asio::ip::tcp;

class HeadlessModeScope {
public:
	HeadlessModeScope()
	    : previous_(HeadlessMode)
	{
		HeadlessMode = true;
	}

	~HeadlessModeScope()
	{
		HeadlessMode = previous_;
	}

	HeadlessModeScope(const HeadlessModeScope &) = delete;
	HeadlessModeScope &operator=(const HeadlessModeScope &) = delete;

private:
	bool previous_;
};

class NativePresentationGlobalScope {
public:
	NativePresentationGlobalScope()
	    : headlessMode_(HeadlessMode)
	    , levelId_(currentLevel().id())
	    , quest_(Quests[1])
	{
		paths::SetAssetsPath(paths::BasePath() + "/assets/");
		LoadObjectData();
		HeadlessMode = true;
		InitializeItemPool();
		InitializeMonsterPool();
		ObjectPoolAdapter::ResetLegacyObjectPools();
	}

	~NativePresentationGlobalScope()
	{
		for (int index = 0; index < ObjectPoolAdapter::ActiveObjectCountValue(); ++index) {
			const int slot = ObjectPoolAdapter::ActiveObjectIndexAt(index);
			if (InDungeonBounds(Objects[slot].position) && tileAt(Objects[slot].position).object() == slot + 1)
				tileAt(Objects[slot].position).setObject(0);
		}
		ObjectPoolAdapter::ResetLegacyObjectPools();
		InitializeMonsterPool();
		InitializeItemPool();
		Quests[1] = quest_;
		SwitchCurrentLevel(levelId_);
		HeadlessMode = headlessMode_;
	}

	NativePresentationGlobalScope(const NativePresentationGlobalScope &) = delete;
	NativePresentationGlobalScope &operator=(const NativePresentationGlobalScope &) = delete;

private:
	bool headlessMode_;
	LevelId levelId_;
	Quest quest_;
};

class RuntimeStopScope {
public:
	explicit RuntimeStopScope(ServerBackedRuntime &runtime)
	    : runtime_(runtime)
	{
	}

	~RuntimeStopScope()
	{
		runtime_.Stop();
	}

	RuntimeStopScope(const RuntimeStopScope &) = delete;
	RuntimeStopScope &operator=(const RuntimeStopScope &) = delete;

private:
	ServerBackedRuntime &runtime_;
};

OwnedClxSpriteList MakeSpriteList(uint32_t spriteCount)
{
	constexpr uint32_t SpriteHeaderSize = 6;
	constexpr uint32_t SpriteSize = SpriteHeaderSize + 1;
	const uint32_t tableSize = 4 + 4 * (spriteCount + 1);
	const uint32_t dataSize = tableSize + SpriteSize * spriteCount;
	auto data = std::make_unique<uint8_t[]>(dataSize);
	std::fill_n(data.get(), dataSize, 0);
	WriteLE32(data.get(), spriteCount);
	for (uint32_t index = 0; index <= spriteCount; ++index)
		WriteLE32(&data[4 + index * 4], tableSize + index * SpriteSize);
	for (uint32_t index = 0; index < spriteCount; ++index) {
		const uint32_t offset = tableSize + index * SpriteSize;
		WriteLE16(&data[offset], SpriteHeaderSize);
		WriteLE16(&data[offset + 2], 1);
		WriteLE16(&data[offset + 4], 1);
		data[offset + SpriteHeaderSize] = 1;
	}
	return OwnedClxSpriteList { std::move(data) };
}

protocol::Snapshot MakeSnapshot(uint32_t levelId, uint64_t tick, bool completedQuest)
{
	protocol::Snapshot snapshot;
	snapshot.set_tick(tick);
	snapshot.set_state_sha256(std::string(64, 'a'));
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_position_x(20);
	player->set_position_y(20);
	player->set_life(100);
	player->set_life_maximum(100);
	player->set_mana(50);
	player->set_mana_maximum(50);
	player->set_character_level(1);
	player->set_level_id(levelId);
	player->set_character_class_id(0);
	player->set_facing_direction(protocol::PLAYER_DIRECTION_SOUTH);
	player->add_visited_level_ids(1);

	auto *object = snapshot.add_objects();
	object->set_entity_id(2001);
	object->set_object_id(static_cast<uint32_t>(OBJ_SARC));
	object->set_native_object_id(static_cast<uint32_t>(OBJ_SARC));
	object->set_level_id(levelId);
	object->set_position_x(22);
	object->set_position_y(22);

	auto *quest = snapshot.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(1);
	quest->set_completed(completedQuest);
	quest->set_native_quest_id(1);
	quest->set_message_id(0);
	return snapshot;
}

class SnapshotLoopbackServer {
public:
	SnapshotLoopbackServer(std::vector<protocol::Snapshot> snapshots)
	    : acceptor_(io_, { asio::ip::address_v4::loopback(), 0 })
	    , snapshots_(std::move(snapshots))
	{
		StartAccept();
		worker_ = std::thread([this]() { Serve(); });
	}

	~SnapshotLoopbackServer()
	{
		stopRequested_.store(true, std::memory_order_release);
		asio::post(io_, [this]() {
			asio::error_code error;
			acceptor_.close(error);
			CloseConnection();
		});
		if (worker_.joinable())
			worker_.join();
	}

	SnapshotLoopbackServer(const SnapshotLoopbackServer &) = delete;
	SnapshotLoopbackServer &operator=(const SnapshotLoopbackServer &) = delete;

	[[nodiscard]] uint16_t port() const noexcept
	{
		return acceptor_.local_endpoint().port();
	}

private:
	using EnvelopeHandler = std::function<void(protocol::Envelope)>;

	void StartAccept()
	{
		if (stopRequested_.load(std::memory_order_acquire))
			return;
		socket_ = std::make_shared<tcp::socket>(io_);
		acceptor_.async_accept(*socket_, [this](const asio::error_code &error) {
			if (error || stopRequested_.load(std::memory_order_acquire))
				return;
			ReadEnvelope([this](protocol::Envelope hello) {
				if (hello.payload_case() != protocol::Envelope::kClientHello) {
					CloseConnection();
					return;
				}
				protocol::Envelope serverHello;
				serverHello.mutable_server_hello()->set_server_build_id("native-presentation-test-server");
				serverHello.mutable_server_hello()->set_protocol_schema_version("1");
				serverHello.mutable_server_hello()->set_content_manifest_hash("content");
				serverHello.mutable_server_hello()->set_session_token("native-presentation-test-session");
				serverHello.mutable_server_hello()->set_game_mode(protocol::GAME_MODE_UNSPECIFIED);
				WriteEnvelope(std::move(serverHello), [this]() { SendInitialSnapshot(); });
			});
		});
	}

	void ReadEnvelope(EnvelopeHandler handler)
	{
		auto header = std::make_shared<std::array<uint8_t, sizeof(uint32_t)>>();
		asio::async_read(*socket_, asio::buffer(*header), [this, header, handler = std::move(handler)](const asio::error_code &error, size_t) mutable {
			if (error || stopRequested_.load(std::memory_order_acquire))
				return;
			const uint32_t length = static_cast<uint32_t>((*header)[0])
			    | (static_cast<uint32_t>((*header)[1]) << 8)
			    | (static_cast<uint32_t>((*header)[2]) << 16)
			    | (static_cast<uint32_t>((*header)[3]) << 24);
			if (length == 0 || length > EnvelopeCodec::MaxEnvelopeBytes) {
				CloseConnection();
				return;
			}
			auto payload = std::make_shared<std::vector<uint8_t>>(length);
			asio::async_read(*socket_, asio::buffer(*payload), [this, payload, handler = std::move(handler)](const asio::error_code &error, size_t) mutable {
				if (error || stopRequested_.load(std::memory_order_acquire))
					return;
				protocol::Envelope envelope;
				if (!envelope.ParseFromArray(payload->data(), static_cast<int>(payload->size()))) {
					CloseConnection();
					return;
				}
				handler(std::move(envelope));
			});
		});
	}

	void WriteEnvelope(protocol::Envelope envelope, std::function<void()> continuation)
	{
		const std::string payload = envelope.SerializeAsString();
		if (payload.empty() || payload.size() > EnvelopeCodec::MaxEnvelopeBytes) {
			CloseConnection();
			return;
		}
		auto frame = std::make_shared<std::vector<uint8_t>>(sizeof(uint32_t) + payload.size());
		WriteLE32(frame->data(), static_cast<uint32_t>(payload.size()));
		std::copy(payload.begin(), payload.end(), frame->begin() + sizeof(uint32_t));
		asio::async_write(*socket_, asio::buffer(*frame), [this, frame, continuation = std::move(continuation)](const asio::error_code &error, size_t) mutable {
			if (error || stopRequested_.load(std::memory_order_acquire))
				return;
			continuation();
		});
	}

	void SendInitialSnapshot()
	{
		if (snapshots_.empty()) {
			CloseConnection();
			return;
		}
		protocol::Envelope initial;
		*initial.mutable_snapshot() = snapshots_.front();
		WriteEnvelope(std::move(initial), [this]() {
			if (snapshots_.size() == 1) {
				CloseConnection();
				return;
			}
			ReadEnvelope([this](protocol::Envelope request) {
				if (request.payload_case() != protocol::Envelope::kSnapshotRequest) {
					CloseConnection();
					return;
				}
				SendSnapshot(1);
			});
		});
	}

	void SendSnapshot(size_t index)
	{
		if (index >= snapshots_.size()) {
			CloseConnection();
			return;
		}
		protocol::Envelope response;
		*response.mutable_snapshot() = snapshots_[index];
		WriteEnvelope(std::move(response), [this, index]() {
			if (index + 1 == snapshots_.size()) {
				CloseConnection();
				return;
			}
			SendSnapshot(index + 1);
		});
	}

	void CloseConnection()
	{
		if (socket_ == nullptr)
			return;
		asio::error_code error;
		socket_->shutdown(tcp::socket::shutdown_both, error);
		socket_->close(error);
	}

	void Serve()
	{
		io_.run();
	}

	asio::io_context io_;
	tcp::acceptor acceptor_;
	std::shared_ptr<tcp::socket> socket_;
	std::vector<protocol::Snapshot> snapshots_;
	std::atomic<bool> stopRequested_ = false;
	std::thread worker_;
};

ServerBackedRuntimeConfiguration LoopbackConfiguration(uint16_t port)
{
	ServerBackedRuntimeConfiguration configuration;
	configuration.enabled = true;
	configuration.mode = ServerBackedRuntimeConfiguration::GameMode::Authoritative;
	configuration.host = "127.0.0.1";
	configuration.port = port;
	configuration.clientBuildId = "native-presentation-test-client";
	configuration.protocolSchemaVersion = "1";
	configuration.contentManifestHash = "content";
	return configuration;
}

TEST(ServerBackedNativePresentation, ResolvesLoadedArtFrameSentinelAndBounds)
{
	HeadlessModeScope headlessMode;
	HeadlessMode = false;

	Object object;
	object._oAnimFrame = 3;
	OwnedClxSpriteList sprites = MakeSpriteList(4);
	object._oAnimData.emplace(sprites);

	ProjectedObjectSnapshot snapshot;
	snapshot.animationFrame = 0;
	auto frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	ASSERT_TRUE(frame.has_value()) << frame.error();
	EXPECT_EQ(*frame, 3U);
	object._oAnimFrame = 0;
	frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	ASSERT_TRUE(frame.has_value()) << frame.error();
	EXPECT_EQ(*frame, 1U);
	object._oAnimFrame = 9;
	frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	ASSERT_TRUE(frame.has_value()) << frame.error();
	EXPECT_EQ(*frame, 4U);
	object._oAnimFrame = 3;

	snapshot.animationFrame = 1;
	frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	ASSERT_TRUE(frame.has_value()) << frame.error();
	EXPECT_EQ(*frame, 1U);

	snapshot.animationFrame = 4;
	frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	ASSERT_TRUE(frame.has_value()) << frame.error();
	EXPECT_EQ(*frame, 4U);

	snapshot.animationFrame = 5;
	frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	EXPECT_FALSE(frame.has_value());

	snapshot.activated = true;
	snapshot.activatedAnimationFrame = 1;
	frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	ASSERT_TRUE(frame.has_value()) << frame.error();
	EXPECT_EQ(*frame, 1U);
	snapshot.activatedAnimationFrame = 5;
	frame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	EXPECT_FALSE(frame.has_value());
}

TEST(ServerBackedNativePresentation, AppliesTypedCathedralDoorNativeStateExactly)
{
	NativePresentationGlobalScope globals;
	ServerBackedWorldProjection projection;
	ServerBackedWorldPresentation presentation;
	ProjectedObjectSnapshot object;
	object.entityId = 3101;
	object.objectId = OBJ_L1LDOOR;
	object.nativeObjectId = OBJ_L1LDOOR;
	object.levelId = 1;
	object.positionX = 10;
	object.positionY = 10;
	object.keepVisibleAfterActivation = true;
	const int initialSpecial = tileAt(object.positionX, object.positionY).special();

	for (const auto &[state, frame, selectionRegion, preDraw, missilePassable] : std::array {
	         std::tuple<int32_t, uint32_t, uint32_t, bool, bool> { 0, 1, 3, false, false },
	         std::tuple<int32_t, uint32_t, uint32_t, bool, bool> { 1, 3, 2, true, true },
	         std::tuple<int32_t, uint32_t, uint32_t, bool, bool> { 2, 5, 2, true, true },
	     }) {
		object.animationFrame = frame;
		object.missilePassable = missilePassable;
		object.cathedralDoor = ProjectedCathedralDoorSnapshot {
			.entityId = object.entityId,
			.levelId = object.levelId,
			.positionX = object.positionX,
			.positionY = object.positionY,
			.nativeObjectId = object.nativeObjectId,
			.doorState = state,
			.cachedPiece = 43,
			.cachedNeighborPiece = 49,
			.animationFrame = frame,
			.selectionRegion = selectionRegion,
			.preDraw = preDraw,
			.missilePassable = missilePassable,
			.solid = false,
		};
		ASSERT_TRUE(projection.Apply({}, {}, { object }, 1).has_value());
		EXPECT_EQ(
		    projection.AuthoritativeObjectAt(object.positionX, object.positionY),
		    std::optional<uint32_t> { object.entityId });
		ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
		ASSERT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 1);
		const int slot = ObjectPoolAdapter::ActiveObjectIndexAt(0);
		EXPECT_TRUE(Objects[slot]._oDoorFlag);
		EXPECT_EQ(Objects[slot]._oVar1, 43);
		EXPECT_EQ(Objects[slot]._oVar2, 49);
		EXPECT_EQ(Objects[slot]._oVar4, state);
		EXPECT_EQ(Objects[slot]._oAnimFrame, frame);
		EXPECT_EQ(Objects[slot].selectionRegion, static_cast<SelectionRegion>(selectionRegion));
		EXPECT_EQ(Objects[slot]._oPreFlag, preDraw);
		EXPECT_EQ(Objects[slot]._oMissFlag, missilePassable);
		EXPECT_FALSE(Objects[slot]._oSolidFlag);
		EXPECT_EQ(tileAt(object.positionX, object.positionY).special(), state == 0 ? 0 : 7);
	}

	presentation.Clear();
	tileAt(object.positionX, object.positionY).setSpecial(initialSpecial);
}

TEST(ServerBackedNativePresentation, RejectsMissingAndEmptyLoadedArt)
{
	HeadlessModeScope headlessMode;
	HeadlessMode = false;

	Object missingArt;
	ProjectedObjectSnapshot snapshot;
	snapshot.animationFrame = 1;
	auto missingResult = ResolveAuthoritativeObjectAnimationFrame(missingArt, snapshot);
	ASSERT_FALSE(missingResult.has_value());
	EXPECT_NE(missingResult.error().find("Authoritative object presentation requires native art that is not loaded"), std::string::npos);
	EXPECT_NE(missingResult.error().find("object type -1"), std::string::npos);

	Object emptyArt;
	OwnedClxSpriteList emptySprites = MakeSpriteList(0);
	emptyArt._oAnimData.emplace(emptySprites);
	EXPECT_FALSE(ResolveAuthoritativeObjectAnimationFrame(emptyArt, snapshot).has_value());
}

TEST(ServerBackedNativePresentation, GatesInitialAndChangedLevelProjectionOnNativeReadiness)
{
	NativePresentationGlobalScope globals;
	SnapshotLoopbackServer server({
	    MakeSnapshot(1, 1, false),
	    MakeSnapshot(AuthoritativeRuntimeSetLevelIdBase + 10, 2, true),
	});
	ServerBackedRuntime runtime;
	RuntimeStopScope runtimeStop(runtime);
	StoreManager store;
	Player player {};

	auto start = runtime.Start(LoopbackConfiguration(server.port()), store, player);
	ASSERT_TRUE(start.has_value()) << start.error();
	ASSERT_NE(runtime.Session(), nullptr);
	EXPECT_FALSE(runtime.NeedsNativeLevelLoad());
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 0);
	auto prematureReady = runtime.MarkNativeLevelReady();
	EXPECT_FALSE(prematureReady.has_value());
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 0);

	Quests[1]._qactive = QUEST_NOTAVAIL;
	auto prepare = runtime.PrepareNativeLevel();
	ASSERT_TRUE(prepare.has_value()) << prepare.error();
	EXPECT_FALSE(runtime.NeedsNativeLevelLoad());
	EXPECT_FALSE(currentLevel().isSetLevel());
	EXPECT_EQ(Quests[1]._qactive, QUEST_ACTIVE);

	auto ready = runtime.MarkNativeLevelReady();
	ASSERT_TRUE(ready.has_value()) << ready.error();
	EXPECT_FALSE(runtime.NeedsNativeLevelLoad());
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 1);

	auto poll = runtime.Poll(100);
	ASSERT_TRUE(poll.has_value()) << poll.error();
	EXPECT_TRUE(runtime.NeedsNativeLevelLoad());
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 0);

	prepare = runtime.PrepareNativeLevel();
	ASSERT_TRUE(prepare.has_value()) << prepare.error();
	EXPECT_FALSE(runtime.NeedsNativeLevelLoad());
	EXPECT_TRUE(currentLevel().isSetLevel());
	EXPECT_EQ(currentLevel().setLevelId(), SL_ARENA_CHURCH);
	EXPECT_EQ(Quests[1]._qactive, QUEST_DONE);

	ready = runtime.MarkNativeLevelReady();
	ASSERT_TRUE(ready.has_value()) << ready.error();
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 1);

	runtime.InvalidateNativeLevelPresentation();
	EXPECT_FALSE(runtime.NeedsNativeLevelLoad());
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 0);
}

} // namespace
} // namespace devilution::authoritative
