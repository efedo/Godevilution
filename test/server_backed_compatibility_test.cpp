#include <type_traits>

#include <gtest/gtest.h>

#include "controls/player_controls.h"
#include "devilution.pb.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monsters.hpp"
#include "game/players/inventory.hpp"
#include "game/stores/stores.hpp"
#include "network/authoritative/authoritative_client.hpp"
#include "network/authoritative/protocol_limits.hpp"
#include "network/authoritative/runtime_configuration.hpp"
#include "network/authoritative/server_backed_client.hpp"
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/server_backed_vendor_state.hpp"
#include "network/authoritative/server_backed_world_projection.hpp"
#include "network/authoritative/store_snapshot.hpp"
#include "network/authoritative/store_state.hpp"
#include "network/authoritative/vendor_snapshot.hpp"
#include "network/protocol/legacy_attribute_policy.hpp"
#include "network/protocol/legacy_combat_policy.hpp"
#include "network/protocol/legacy_debug_policy.hpp"
#include "network/protocol/legacy_input_policy.hpp"
#include "network/protocol/legacy_item_policy.hpp"
#include "network/protocol/legacy_lua_policy.hpp"
#include "network/protocol/legacy_player_state_policy.hpp"
#include "network/protocol/legacy_store_policy.hpp"
#include "network/protocol/legacy_sync_policy.hpp"
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

TEST(ServerBackedCompatibility, AuthoritativeModeDoesNotOwnLegacyLuaLifecycle)
{
	EXPECT_TRUE(ShouldInitializeLegacyLua(false));
	EXPECT_FALSE(ShouldInitializeLegacyLua(true));
}

TEST(ServerBackedCompatibility, AuthoritativeModeDoesNotProcessLegacyLuaEvents)
{
	EXPECT_TRUE(ShouldProcessLegacyLuaEvents(false));
	EXPECT_FALSE(ShouldProcessLegacyLuaEvents(true));
}

TEST(ServerBackedCompatibility, AuthoritativeGameModeMustMatchTheNetInitPath)
{
	EXPECT_TRUE(IsAuthoritativeGameModeCompatible(false, std::nullopt));
	EXPECT_TRUE(IsAuthoritativeGameModeCompatible(true, std::nullopt));
	EXPECT_TRUE(IsAuthoritativeGameModeCompatible(false, false));
	EXPECT_TRUE(IsAuthoritativeGameModeCompatible(true, true));
	EXPECT_FALSE(IsAuthoritativeGameModeCompatible(false, true));
	EXPECT_FALSE(IsAuthoritativeGameModeCompatible(true, false));
}

TEST(ServerBackedCompatibility, PresentationOnlyObjectsCannotBecomeCommandTargets)
{
	ServerBackedWorldProjection projection;
	ProjectedObjectSnapshot presentationObject;
	presentationObject.entityId = 17;
	presentationObject.levelId = 1;
	presentationObject.positionX = 4;
	presentationObject.positionY = 5;

	ASSERT_TRUE(projection.Apply({}, {}, { presentationObject }, 1).has_value());
	EXPECT_EQ(projection.ObjectAt(4, 5), std::optional<uint32_t> { 17 });
	EXPECT_FALSE(projection.AuthoritativeObjectAt(4, 5).has_value());

	presentationObject.effectKind = static_cast<int32_t>(ProjectedObjectEffectKind::WorldMutation);
	ASSERT_TRUE(projection.Apply({}, {}, { presentationObject }, 1).has_value());
	EXPECT_EQ(projection.AuthoritativeObjectAt(4, 5), std::optional<uint32_t> { 17 });

	presentationObject.effectKind = static_cast<int32_t>(ProjectedObjectEffectKind::None);
	presentationObject.dialogueMessageId = 412;
	ASSERT_TRUE(projection.Apply({}, {}, { presentationObject }, 1).has_value());
	EXPECT_EQ(projection.AuthoritativeObjectAt(4, 5), std::optional<uint32_t> { 17 });
}

TEST(ServerBackedCompatibility, WorldProjectionUsesNamedNativeScalarBounds)
{
	ServerBackedWorldProjection projection;
	ProjectedMonsterSnapshot monster;
	monster.entityId = 1;
	monster.packSize = MaximumNativeMonsterPackSize;
	monster.activeForTicks = MaximumNativeMonsterActiveTicks;
	ASSERT_TRUE(projection.Apply({ monster }, {}, {}, 1).has_value());

	monster.packSize = MaximumNativeMonsterPackSize + 1;
	EXPECT_FALSE(projection.Apply({ monster }, {}, {}, 1).has_value());
	monster.packSize = MaximumNativeMonsterPackSize;
	monster.activeForTicks = MaximumNativeMonsterActiveTicks + 1;
	EXPECT_FALSE(projection.Apply({ monster }, {}, {}, 1).has_value());

	ProjectedProjectileSnapshot projectile;
	projectile.entityId = 2;
	projectile.sourceEntityId = 1;
	projectile.behavior = static_cast<int>(ProjectileBehavior::WeaponArrow);
	projectile.remainingTicks = 1;
	projectile.damageType = MaximumProjectileDamageType;
	projectile.phase = MaximumProjectilePhase;
	ASSERT_TRUE(projection.Apply({}, {}, {}, { projectile }, 1).has_value());

	projectile.damageType = MaximumProjectileDamageType + 1;
	EXPECT_FALSE(projection.Apply({}, {}, {}, { projectile }, 1).has_value());
	projectile.damageType = MaximumProjectileDamageType;
	projectile.phase = MaximumProjectilePhase + 1;
	EXPECT_FALSE(projection.Apply({}, {}, {}, { projectile }, 1).has_value());
}

TEST(ServerBackedCompatibility, AuthoritativeMissilesDoNotOwnLegacyTileFlags)
{
	EXPECT_TRUE(ShouldClearLegacyMissileTileFlags(false));
	EXPECT_FALSE(ShouldClearLegacyMissileTileFlags(true));
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
	    Route::FailClosed);
}

TEST(ServerBackedCompatibility, ConnectedMonsterConversationSkipsLegacyModeMutation)
{
	EXPECT_TRUE(ShouldApplyLegacyMonsterConversationMutation(false));
	EXPECT_FALSE(ShouldApplyLegacyMonsterConversationMutation(true));
}

TEST(ServerBackedCompatibility, ConnectedManaShieldUsesServerLifecycle)
{
	EXPECT_TRUE(ShouldProcessNativeManaShield(false));
	EXPECT_FALSE(ShouldProcessNativeManaShield(true));
}

TEST(ServerBackedCompatibility, ConnectedReflectUsesServerLifecycle)
{
	EXPECT_TRUE(ShouldProcessNativeReflect(false));
	EXPECT_FALSE(ShouldProcessNativeReflect(true));
}

TEST(ServerBackedCompatibility, ConnectedLevelTransitionsPreserveNativeStatusState)
{
	EXPECT_TRUE(ShouldProcessLegacyStatusLifecycle(false));
	EXPECT_FALSE(ShouldProcessLegacyStatusLifecycle(true));
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

TEST(ServerBackedCompatibility, ConnectedLegacyDebugMutationsFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyDebugMutation(false));
	EXPECT_FALSE(ShouldProcessLegacyDebugMutation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyItemProtocolFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyItemProtocol(false));
	EXPECT_FALSE(ShouldProcessLegacyItemProtocol(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyInventoryMutationsFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyInventoryMutation(false));
	EXPECT_FALSE(ShouldProcessLegacyInventoryMutation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyItemServicesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyItemServiceMutation(false));
	EXPECT_FALSE(ShouldProcessLegacyItemServiceMutation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyStoreMutationsFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyStoreMutation(false));
	EXPECT_FALSE(ShouldProcessLegacyStoreMutation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyItemUseFailsClosedExceptServerRoutedCasts)
{
	EXPECT_TRUE(ShouldProcessLegacyItemUse(false, false));
	EXPECT_FALSE(ShouldProcessLegacyItemUse(true, false));
	EXPECT_TRUE(ShouldProcessLegacyItemUse(true, true));
}

TEST(ServerBackedCompatibility, ConnectedGoldSplittingFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyGoldDrop(false));
	EXPECT_FALSE(ShouldProcessLegacyGoldDrop(true));
}

TEST(ServerBackedCompatibility, ConnectedCtrlClickStashTransferUsesAuthoritativeMove)
{
	EXPECT_TRUE(ShouldProcessLegacyStashInventoryTransfer(false));
	EXPECT_FALSE(ShouldProcessLegacyStashInventoryTransfer(true));
}

TEST(ServerBackedCompatibility, ConnectedAutoPickupUsesAuthoritativeWorldItems)
{
	EXPECT_TRUE(ShouldProcessLegacyAutoPickup(false));
	EXPECT_FALSE(ShouldProcessLegacyAutoPickup(true));
}

TEST(ServerBackedCompatibility, ConnectedTelekinesisUsesAuthoritativeInput)
{
	EXPECT_TRUE(ShouldProcessLegacyTelekinesis(false));
	EXPECT_FALSE(ShouldProcessLegacyTelekinesis(true));
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

TEST(ServerBackedCompatibility, ConnectedLegacySpellSimulationFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacySpellSimulation(false));
	EXPECT_FALSE(ShouldProcessLegacySpellSimulation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyDeathDropsFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyDeathDrops(false));
	EXPECT_FALSE(ShouldProcessLegacyDeathDrops(true));
}

TEST(ServerBackedCompatibility, ConnectedNativeDrainLifeFailsClosed)
{
	EXPECT_TRUE(ShouldProcessNativeDrainLife(false));
	EXPECT_FALSE(ShouldProcessNativeDrainLife(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyWorldStateMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyWorldStateMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyWorldStateMessage(true));
	EXPECT_TRUE(ShouldProcessLegacyObjectSynchronization(false));
	EXPECT_FALSE(ShouldProcessLegacyObjectSynchronization(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyQuestResyncFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyQuestResync(false));
	EXPECT_FALSE(ShouldProcessLegacyQuestResync(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyLevelLoadMutationsFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyLevelLoadMutation(false));
	EXPECT_FALSE(ShouldProcessLegacyLevelLoadMutation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyLevelLoadPlayerPlacementFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyLevelLoadPlayerPlacement(false));
	EXPECT_FALSE(ShouldProcessLegacyLevelLoadPlayerPlacement(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyLevelLoadMonsterCleanupFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyLevelLoadMonsterCleanup(false));
	EXPECT_FALSE(ShouldProcessLegacyLevelLoadMonsterCleanup(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyPlayerStateMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyPlayerStateMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyPlayerStateMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyPlayerSimulationFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyPlayerSimulation(false));
	EXPECT_FALSE(ShouldProcessLegacyPlayerSimulation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacySimulationFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacySimulation(false));
	EXPECT_FALSE(ShouldProcessLegacySimulation(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyQuestTicksFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyQuestTick(false));
	EXPECT_FALSE(ShouldProcessLegacyQuestTick(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyTriggerChecksFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyTriggerCheck(false));
	EXPECT_FALSE(ShouldProcessLegacyTriggerCheck(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyInputMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyInputMessage(false));
	EXPECT_FALSE(ShouldProcessLegacyInputMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacySyncMessagesFailClosed)
{
	EXPECT_TRUE(ShouldProcessLegacySyncMessage(false));
	EXPECT_FALSE(ShouldProcessLegacySyncMessage(true));
}

TEST(ServerBackedCompatibility, ConnectedLegacyDeltaStateFailsClosed)
{
	EXPECT_TRUE(ShouldProcessLegacyDeltaState(false));
	EXPECT_FALSE(ShouldProcessLegacyDeltaState(true));
}

} // namespace
} // namespace devilution::authoritative
