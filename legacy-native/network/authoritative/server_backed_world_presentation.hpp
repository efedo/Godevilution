#pragma once

/**
 * @file network/authoritative/server_backed_world_presentation.hpp
 *
 * Applies authoritative world entities to the legacy presentation pools.
 */

#include <cstddef>
#include <cstdint>
#include <map>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include <expected.hpp>

#include "engine/gfx/surface.hpp"
#include "engine/math/point.hpp"
#include "game/players/players.hpp"
#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/server_backed_world_projection.hpp"

namespace devilution {
struct Object;
}

namespace devilution::authoritative {

/** Maps an authoritative player combat mode to a native presentation graphic. */
[[nodiscard]] player_graphic GetRemotePlayerGraphic(const ProjectedPlayerSnapshot &snapshot);

/** Maps authoritative equipment to the native weapon/armor sprite bitmask. */
[[nodiscard]] uint8_t GetRemotePlayerEquipmentGraphicBits(const ProjectedPlayerSnapshot &snapshot);

/** Refreshes native player graphics from authoritative equipment only. */
void RefreshProjectedPlayerGraphics(Player &player, const ProjectedPlayerSnapshot &snapshot);

/** Applies an authoritative player action to native animation state only. */
void ApplyProjectedPlayerAnimation(
    Player &player,
    const ProjectedPlayerSnapshot &snapshot,
    player_graphic graphic,
    bool reset,
    bool hasPreviousTick,
    uint64_t previousTick,
    uint64_t presentationTicksSincePreviousSnapshot = 0);

/** Advances projected player animation state without running gameplay logic. */
void AdvanceProjectedPlayerAnimation(Player &player, player_graphic graphic, uint64_t elapsedTicks, bool loop);

/** Returns whether a projected remote player belongs to the currently rendered level. */
[[nodiscard]] bool IsRemotePlayerOnActiveLevel(uint32_t projectedLevelId, const Player &player);

/** Returns whether an authoritative monster should occupy native presentation state. */
[[nodiscard]] bool IsAuthoritativeMonsterPresentable(const ProjectedMonsterSnapshot &snapshot);

/** Resolves the native object template used by an authoritative presentation. */
[[nodiscard]] uint32_t NativeObjectPresentationId(const ProjectedObjectSnapshot &snapshot);

/** Resolves the zero sentinel and validates positive frames against loaded art. */
[[nodiscard]] tl::expected<uint32_t, std::string> ResolveAuthoritativeObjectAnimationFrame(const Object &object, const ProjectedObjectSnapshot &snapshot);

/** Presentation interpretation of the additive server-owned dialogue state. */
enum class AuthoritativeMonsterDialoguePresentationState : int32_t {
	Legacy = 0,
	Inquiring = 1,
	Combat = 2,
};

/** Resolves the additive server-owned dialogue state used by native presentation. */
[[nodiscard]] AuthoritativeMonsterDialoguePresentationState ResolveAuthoritativeMonsterDialoguePresentationState(int32_t dialogueState) noexcept;

/** Keeps legacy rendering and tile lookup state synchronized with the server. */
class ServerBackedWorldPresentation {
public:
	[[nodiscard]] tl::expected<void, std::string> Apply(
	    const ServerBackedWorldProjection &projection,
	    uint32_t levelId,
	    const std::vector<ProjectedPlayerSnapshot> &players = {},
	    uint32_t localEntityId = 0);

	/** Advances presentation animation state without running authoritative simulation. */
	[[nodiscard]] std::size_t AdvanceAnimations() noexcept;

	/** Draws projected remote players without adding them to native simulation pools. */
	void DrawRemotePlayers(
	    const Surface &out,
	    Point tilePosition,
	    Point targetBufferPosition,
	    int lightTableIndex) const;

	/** Visits projected remote players for presentation consumers only. */
	template <typename Callback>
	void ForEachRemotePlayer(Callback callback) const
	{
		for (const auto &[entityId, remote] : remotePlayers_)
			callback(entityId, remote.player);
	}
	/** Finds a projected remote player for read-only UI inspection. */
	[[nodiscard]] const Player *FindRemotePlayer(uint32_t entityId) const noexcept;
	/** Finds a projected remote player for the legacy read-only inspection panel. */
	[[nodiscard]] Player *FindRemotePlayer(uint32_t entityId) noexcept;

	[[nodiscard]] std::size_t RemotePlayerCount() const noexcept { return remotePlayers_.size(); }

	void Clear() noexcept;

private:
	std::unordered_map<uint32_t, unsigned> monsterSlots_;
	std::unordered_map<uint32_t, unsigned> itemSlots_;
	std::unordered_map<uint32_t, int> objectSlots_;
	std::unordered_set<uint32_t> projectileEntities_;
	std::optional<uint64_t> lastWorldServerTick_;

	struct RemotePlayerPresentation {
		Player player;
		uint32_t levelId = 0;
		Point tilePosition;
		player_graphic graphic = player_graphic::Stand;
		std::optional<uint32_t> actionKind;
		std::optional<uint64_t> actionStartedAtTick;
		uint64_t serverTick = 0;
		bool hasServerTick = false;
		uint64_t presentationTicksSinceServerTick = 0;
	};
	std::map<uint32_t, RemotePlayerPresentation> remotePlayers_;
};

} // namespace devilution::authoritative
