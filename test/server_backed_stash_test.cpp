#include <atomic>
#include <span>
#include <string>
#include <thread>

#include <asio/ip/tcp.hpp>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/envelope_codec.hpp"
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/server_backed_runtime.hpp"
#include "qol/stash.h"
#include "ui_test.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
using asio::ip::tcp;
namespace {

bool WriteEnvelope(tcp::socket &socket, const protocol::Envelope &envelope)
{
	const std::string payload = envelope.SerializeAsString();
	return EnvelopeCodec::Write(socket, std::span<const uint8_t>(
	                                    reinterpret_cast<const uint8_t *>(payload.data()), payload.size()))
	    .has_value();
}

class StashItemServer {
public:
	explicit StashItemServer(bool expectCast = false)
	    : acceptor_(serverIo_, { tcp::v4(), 0 })
	    , expectCast_(expectCast)
	    , server_([this]() { Run(); })
	{
	}

	~StashItemServer()
	{
		std::error_code error;
		acceptor_.close(error);
		if (server_.joinable())
			server_.join();
	}

	[[nodiscard]] uint16_t port() const
	{
		return acceptor_.local_endpoint().port();
	}

	[[nodiscard]] bool observed() const
	{
		return observed_.load();
	}

private:
	void Run()
	{
		try {
			tcp::socket socket(serverIo_);
			acceptor_.accept(socket);

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
			if (!WriteEnvelope(socket, serverHello))
				return;

			protocol::Envelope initial;
			initial.mutable_snapshot()->set_tick(1);
			auto *player = initial.mutable_snapshot()->add_players();
			player->set_entity_id(7);
			player->set_level_id(0);
			player->set_stash_open(true);
			auto *stash = player->add_stash();
			stash->set_page(0);
			stash->set_cell(0);
			auto *item = stash->mutable_item();
			item->set_item_seed(42);
			item->mutable_state()->set_stable_item_id(700);
			item->mutable_state()->set_item_type(static_cast<int32_t>(ItemType::Misc));
			item->mutable_state()->set_item_index(IDI_BOOK1);
			item->mutable_state()->set_misc_id(expectCast_ ? IMISC_SCROLL : IMISC_BOOK);
			item->mutable_state()->set_spell_id(static_cast<uint32_t>(SpellID::Firebolt));
			item->mutable_state()->set_spell_targeting_kind(protocol::SPELL_TARGETING_KIND_COMBAT_ENTITY);
			item->mutable_state()->set_inventory_width(1);
			item->mutable_state()->set_inventory_height(1);
			if (!WriteEnvelope(socket, initial))
				return;

			auto commandPayload = EnvelopeCodec::Read(socket);
			if (!commandPayload.has_value() || !commandPayload->has_value())
				return;
			protocol::Envelope command;
			if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
			    || command.payload_case() != protocol::Envelope::kCommandBatch
			    || command.command_batch().commands_size() != 1)
				return;
			const auto &request = command.command_batch().commands(0);
			const protocol::PlayerItemReference *reference = nullptr;
			if (expectCast_) {
				if (!request.has_cast_requested()
				    || request.cast_requested().spell_id() != static_cast<uint32_t>(SpellID::Firebolt)
				    || request.cast_requested().source() != protocol::CAST_SOURCE_KIND_CONSUMABLE_ITEM
				    || !request.cast_requested().has_source_item())
					return;
				reference = &request.cast_requested().source_item();
			} else {
				if (!request.has_read_book_requested())
					return;
				reference = &request.read_book_requested().item();
			}
			if (reference->location() != protocol::PLAYER_ITEM_LOCATION_STASH
			    || reference->slot() != 0
			    || reference->stable_item_id() != 700
			    || reference->page() != 0)
				return;
			observed_ = true;

			protocol::Envelope acknowledgement;
			auto *result = acknowledgement.mutable_command_ack()->add_results();
			result->set_client_sequence(request.client_sequence());
			result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
			if (!WriteEnvelope(socket, acknowledgement))
				return;

			protocol::Envelope snapshot;
			snapshot.mutable_snapshot()->set_tick(2);
			snapshot.mutable_snapshot()->add_players()->set_entity_id(7);
			WriteEnvelope(socket, snapshot);
		} catch (const std::exception &) {
			// The test thread owns shutdown; a closed acceptor/socket is expected
			// when an assertion fails before the protocol exchange completes.
		}
	}

	asio::io_context serverIo_;
	tcp::acceptor acceptor_;
	std::atomic_bool observed_ = false;
	bool expectCast_ = false;
	std::thread server_;
};

class ServerBackedStashTest : public UITest {
protected:
	void TearDown() override
	{
		GetServerBackedRuntime().Stop();
		UITest::TearDown();
	}
};

TEST_F(ServerBackedStashTest, UsesAuthoritativeReferenceForSpellBook)
{
	StashItemServer server;
	Item book { };
	InitializeItem(book, IDI_BOOK1);
	book._iSeed = 42;
	book._iSpell = SpellID::Firebolt;
	book._iMiscId = IMISC_BOOK;
	book._iMinMag = 0;
	book._iStatFlag = true;
	Stash.stashList.push_back(book);
	Stash.stashGrids[0][0][0] = 1;
	Stash.SetPage(0);
	Stash.dirty = false;
	IsStashOpen = true;
	pcurs = CURSOR_HAND;
	MyPlayer->attributes.magic.current = 255;
	const uint8_t fireboltLevelBefore = MyPlayer->_pSplLvl[static_cast<int8_t>(SpellID::Firebolt)];

	ServerBackedRuntimeConfiguration configuration;
	configuration.mode = ServerBackedRuntimeConfiguration::GameMode::Authoritative;
	configuration.enabled = true;
	configuration.host = "127.0.0.1";
	configuration.port = server.port();
	configuration.clientBuildId = "client";
	configuration.protocolSchemaVersion = "1";
	configuration.contentManifestHash = "content";
		auto &runtime = GetServerBackedRuntime();
	const auto started = runtime.Start(configuration, CurrentStoreManager, *MyPlayer);
	ASSERT_TRUE(started.has_value()) << started.error();
	ASSERT_TRUE(runtime.IsConnected());

	EXPECT_TRUE(UseStashItem(0));
	EXPECT_EQ(MyPlayer->_pSplLvl[static_cast<int8_t>(SpellID::Firebolt)], fireboltLevelBefore);

	runtime.Stop();
	EXPECT_TRUE(server.observed());
}

TEST_F(ServerBackedStashTest, UsesAuthoritativeReferenceForOpenStashScrollCast)
{
	StashItemServer server(true);
	ServerBackedRuntimeConfiguration configuration;
	configuration.mode = ServerBackedRuntimeConfiguration::GameMode::Authoritative;
	configuration.enabled = true;
	configuration.host = "127.0.0.1";
	configuration.port = server.port();
	configuration.clientBuildId = "client";
	configuration.protocolSchemaVersion = "1";
	configuration.contentManifestHash = "content";
	auto &runtime = GetServerBackedRuntime();
	ASSERT_TRUE(runtime.Start(configuration, CurrentStoreManager, *MyPlayer).has_value());
	ASSERT_TRUE(runtime.IsConnected());

	pcurs = CURSOR_HAND;
	MyPlayer->_pRSpell = SpellID::Firebolt;
	MyPlayer->_pRSplType = SpellType::Scroll;
	CheckPlrSpell(false, SpellID::Firebolt, SpellType::Scroll);

	runtime.Stop();
	EXPECT_TRUE(server.observed());
}

} // namespace
} // namespace devilution::authoritative
