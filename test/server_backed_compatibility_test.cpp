#include <type_traits>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "controls/player_controls.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monsters.hpp"
#include "game/players/inventory.hpp"
#include "game/stores/stores.hpp"
#include "network/authoritative/authoritative_client.hpp"
#include "network/authoritative/runtime_configuration.hpp"
#include "network/authoritative/store_snapshot.hpp"
#include "network/authoritative/store_state.hpp"
#include "network/authoritative/server_backed_client.hpp"
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/vendor_snapshot.hpp"
#include "network/authoritative/server_backed_vendor_state.hpp"
#include "network/protocol/legacy_attribute_policy.hpp"
#include "network/protocol/legacy_combat_policy.hpp"
#include "network/protocol/legacy_item_policy.hpp"
#include "network/protocol/legacy_input_policy.hpp"
#include "network/protocol/legacy_player_state_policy.hpp"
#include "network/protocol/legacy_transition_policy.hpp"
#include "network/protocol/legacy_world_state_policy.hpp"
#include "persistence/save_policy.hpp"
#include "ui/panel/control_chat_commands.hpp"

namespace devilution::authoritative {
namespace {

TEST(ServerBackedCompatibility, PreservesRenamedSourceAliases)
{
	static_assert(std::is_same_v<AuthoritativeClient, ServerBackedClient>);
	static_assert(std::is_same_v<RuntimeConfiguration, ServerBackedRuntimeConfiguration>);
	static_assert(std::is_same_v<ProjectedStoreItem, ProjectedVendorItem>);
	static_assert(std::is_same_v<ProjectedStoreSnapshot, ProjectedVendorSnapshot>);
	static_assert(std::is_same_v<StoreIntentRoute, VendorIntentRoute>);
	static_assert(std::is_same_v<AuthoritativeStorePhase, ServerBackedVendorPhase>);
	static_assert(std::is_same_v<AuthoritativeStoreState, ServerBackedVendorState>);

	const auto configuration = ParseAuthoritativeEndpoint("localhost:6113");
	ASSERT_TRUE(configuration.has_value()) << configuration.error();
	EXPECT_TRUE(configuration->enabled);

	protocol::Snapshot snapshot;
	snapshot.mutable_active_store()->set_store_id(1);
	const auto projected = ProjectStoreSnapshot(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->storeId, 1U);
}

TEST(ServerBackedCompatibility, StaleObjectHighlightsFailClosed)
{
	using Route = ServerBackedObjectInteractionRoute;
	EXPECT_EQ(
	    ResolveServerBackedObjectInteractionRoute(false, true, false),
	    Route::None);
	EXPECT_EQ(
	    ResolveServerBackedObjectInteractionRoute(true, false, true),
	    Route::Submit);
	EXPECT_EQ(
	    ResolveServerBackedObjectInteractionRoute(true, true, true),
	    Route::Submit);
	EXPECT_EQ(
	    ResolveServerBackedObjectInteractionRoute(true, true, false),
	    Route::FailClosed);
	EXPECT_EQ(
	    ResolveServerBackedObjectInteractionRoute(true, false, false),
	    Route::None);
}

TEST(ServerBackedCompatibility, MissingMonsterProjectionFailsClosed)
{
	using Route = ServerBackedMonsterInteractionRoute;
	EXPECT_EQ(
	    ResolveServerBackedMonsterInteractionRoute(false, false, false),
	    Route::None);
	EXPECT_EQ(
	    ResolveServerBackedMonsterInteractionRoute(true, false, false),
	    Route::FailClosed);
	EXPECT_EQ(
	    ResolveServerBackedMonsterInteractionRoute(true, true, true),
	    Route::Submit);
	EXPECT_EQ(
	    ResolveServerBackedMonsterInteractionRoute(true, true, false),
	    Route::None);
}

TEST(ServerBackedCompatibility, ConnectedManaShieldUsesServerLifecycle)
{
	EXPECT_TRUE(ShouldProcessNativeManaShield(false));
	EXPECT_FALSE(ShouldProcessNativeManaShield(true));
}

TEST(ServerBackedCompatibility, ConnectedInventorySortFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyInventorySort(false));
	EXPECT_FALSE(ShouldProcessLegacyInventorySort(true));
}

TEST(ServerBackedCompatibility, ConnectedSessionsDoNotUseLegacySaves)
{
	EXPECT_TRUE(ShouldUseLegacyLocalSave(false));
	EXPECT_FALSE(ShouldUseLegacyLocalSave(true));
}

TEST(ServerBackedCompatibility, ConnectedPremiumStorePurchaseFailsClosed)
{
	EXPECT_TRUE(HasServerBackedStoreAction(TalkID::SmithBuy));
	EXPECT_TRUE(HasServerBackedStoreAction(TalkID::SmithRepair));
	EXPECT_TRUE(HasServerBackedStoreAction(TalkID::StorytellerIdentify));
	EXPECT_FALSE(HasServerBackedStoreAction(TalkID::SmithPremiumBuy));
}

TEST(ServerBackedCompatibility, ConnectedLegacyChatMutationsFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyChatMutation(false));
	EXPECT_FALSE(ShouldProcessLegacyChatMutation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyItemProtocolFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyItemProtocol(false));
	EXPECT_FALSE(ShouldProcessLegacyItemProtocol(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyAttributeMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyAttributeMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyAttributeMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyTransitionMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyTransitionMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyTransitionMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyCombatMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyCombatMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyCombatMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyWorldStateMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyWorldStateMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyWorldStateMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyPlayerStateMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyPlayerStateMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyPlayerStateMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyInputMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyInputMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyInputMessage(true));
}

} // namespace
} // namespace devilution::authoritative
