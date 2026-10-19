/**
 * @file game/portals/portal.cpp
 *
 * Implementation of functionality for handling town portals.
 */
#include "game/portals/portal.hpp"

#include "engine/world.hpp"
#include "engine/lighting.h"
#include "game/missiles/missiles.hpp"
#include "network/protocol/multi.h"
#include "network/protocol/legacy_sync_policy.hpp"
#include "network/protocol/legacy_transition_policy.hpp"
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif
#include "game/players/players.hpp"
#include "tables/misdat.h"

namespace devilution {

/** In-game state of portals. */
Portal Portals[MAXPORTAL];

namespace {

/** Current portal number (a portal array index). */
size_t portalindex;

/** Coordinate of each player's portal in town. */
Point PortalTownPosition[MAXPORTAL] = {
	{ 57, 40 },
	{ 59, 40 },
	{ 61, 40 },
	{ 63, 40 },
};

} // namespace

void InitPortals()
{
	for (auto &portal : Portals) {
		portal.open = false;
	}
}

void SetPortalStats(int i, bool o, Point position, int lvl, dungeon_type lvltype, bool isSetLevel)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyDeltaState(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	Portals[i].open = o;
	Portals[i].position = position;
	Portals[i].level = lvl;
	Portals[i].ltype = lvltype;
	Portals[i].setlvl = isSetLevel;
}

void AddPortalMissile(const Player &player, Point position, bool sync)
{
	auto *missile = AddMissile({ 0, 0 }, position, Direction::South, MissileID::TownPortal, TARGET_MONSTERS, player, 0, 0, /*parent=*/nullptr, SfxID::None);
	if (missile != nullptr) {
		// Don't show portal opening animation if we sync existing portals
		if (sync)
			missile->setFrameGroup<PortalFrame>(PortalFrame::Idle);

		if (levelType() != DTYPE_TOWN)
			missile->_mlid = CurrentLightManager.AddLight(missile->position.tile, 15);
	}
}

void SyncPortals()
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyDeltaState(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	for (int i = 0; i < MAXPORTAL; i++) {
		if (!Portals[i].open)
			continue;
		const Player &player = Players[i];
		if (levelType() == DTYPE_TOWN)
			AddPortalMissile(player, PortalTownPosition[i], true);
		else {
			int lvl = currentLevelNumber();
			if (isSetLevel())
				lvl = setLevelNumber();
			if (Portals[i].level == lvl && Portals[i].setlvl == isSetLevel())
				AddPortalMissile(player, Portals[i].position, true);
		}
	}
}

void AddPortalInTown(const Player &player)
{
	AddPortalMissile(player, PortalTownPosition[player.getId()], false);
}

void ActivatePortal(const Player &player, Point position, int lvl, dungeon_type dungeonType, bool isSetLevel)
{
	Portal &portal = Portals[player.getId()];
	portal.open = true;

	if (lvl != 0) {
		portal.position = position;
		portal.level = lvl;
		portal.ltype = dungeonType;
		portal.setlvl = isSetLevel;
	}
}

void DeactivatePortal(const Player &player)
{
	Portals[player.getId()].open = false;
}

bool PortalOnLevel(const Player &player)
{
	const Portal &portal = Portals[player.getId()];
	if (portal.setlvl == isSetLevel() && portal.level == (isSetLevel() ? static_cast<int>(setLevelNumber()) : currentLevelNumber()))
		return true;

	return levelType() == DTYPE_TOWN;
}

void RemovePortalMissile(const Player &player)
{
	const size_t id = player.getId();
	Missiles.remove_if([id](Missile &missile) {
		if (missile._mitype == MissileID::TownPortal && missile._misource == static_cast<int>(id)) {
			tileAt(missile.position.tile).removeFlags(DungeonFlag::Missile);

			if (Portals[id].level != 0)
				CurrentLightManager.AddUnLight(missile._mlid);

			return true;
		}
		return false;
	});
}

void SetCurrentPortal(size_t p)
{
	portalindex = p;
}

void GetPortalLevel()
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyTransitionMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	if (levelType() != DTYPE_TOWN) {
		SwitchCurrentLevel(LevelId { 0, DTYPE_TOWN, false, SL_NONE });
		MyPlayer->setLevel(0);
		return;
	}

	if (Portals[portalindex].setlvl) {
		uint8_t lvl = Portals[portalindex].level;
		SwitchCurrentLevel(LevelId { lvl, Portals[portalindex].ltype, true, static_cast<_setlevels>(lvl) });
		MyPlayer->setLevel(static_cast<_setlevels>(lvl));
	} else {
		uint8_t lvl = Portals[portalindex].level;
		SwitchCurrentLevel(LevelId { lvl, Portals[portalindex].ltype, false, SL_NONE });
		MyPlayer->setLevel(lvl);
	}

	if (portalindex == MyPlayerId) {
		NetSendCmd(true, CMD_DEACTIVATEPORTAL);
		DeactivatePortal(*MyPlayer);
	}
}

void GetPortalLvlPos()
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyTransitionMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	if (levelType() == DTYPE_TOWN) {
		viewPosition() = PortalTownPosition[portalindex] + Displacement { 1, 1 };
	} else {
		viewPosition() = Portals[portalindex].position;

		if (portalindex != MyPlayerId) {
			viewPosition().x++;
			viewPosition().y++;
		}
	}
}

bool PosOkPortal(int lvl, Point position)
{
	for (auto &portal : Portals) {
		if (portal.open
		    && portal.level == lvl
		    && ((portal.position == position)
		        || (portal.position == position - Displacement { 1, 1 })))
			return true;
	}
	return false;
}

} // namespace devilution
