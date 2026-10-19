/**
 * @file autopickup.cpp
 *
 * QoL feature for automatically picking up gold
 */
#include "qol/autopickup.h"

#include <algorithm>

#include "game/players/inv_iterators.hpp"
#include "game/levels/dungeon_common.h"
#include "persistence/options.h"
#include "game/players/players.hpp"
#include "network/protocol/legacy_item_policy.hpp"
#include "utils/algorithm/container.hpp"

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif

namespace devilution {
namespace {

bool HasRoomForGold()
{
	for (const int idx : MyPlayer->InvGrid) {
		// Secondary item cell. No need to check those as we'll go through the main item cells anyway.
		if (idx < 0)
			continue;

		// Empty cell. 1x1 space available.
		if (idx == 0)
			return true;

		// Main item cell. Potentially a gold pile so check it.
		auto item = MyPlayer->InvList[idx - 1];
		if (item._itype == ItemType::Gold && item._ivalue < MaxGold)
			return true;
	}

	return false;
}

int NumMiscItemsInInv(int iMiscId)
{
	return c_count_if(InventoryAndBeltPlayerItemsRange { *MyPlayer },
	    [iMiscId](const Item &item) { return item._iMiscId == iMiscId; });
}

bool DoPickup(Item item)
{
	if (item._itype == ItemType::Gold && *GetOptions().Gameplay.autoGoldPickup && HasRoomForGold())
		return true;

	if (item._itype == ItemType::Misc
	    && (CanFitItemInInventory(*MyPlayer, item) || AutoPlaceItemInBelt(*MyPlayer, item))) {
		switch (item._iMiscId) {
		case IMISC_HEAL:
			return *GetOptions().Gameplay.numHealPotionPickup > NumMiscItemsInInv(item._iMiscId);
		case IMISC_FULLHEAL:
			return *GetOptions().Gameplay.numFullHealPotionPickup > NumMiscItemsInInv(item._iMiscId);
		case IMISC_MANA:
			return *GetOptions().Gameplay.numManaPotionPickup > NumMiscItemsInInv(item._iMiscId);
		case IMISC_FULLMANA:
			return *GetOptions().Gameplay.numFullManaPotionPickup > NumMiscItemsInInv(item._iMiscId);
		case IMISC_REJUV:
			return *GetOptions().Gameplay.numRejuPotionPickup > NumMiscItemsInInv(item._iMiscId);
		case IMISC_FULLREJUV:
			return *GetOptions().Gameplay.numFullRejuPotionPickup > NumMiscItemsInInv(item._iMiscId);
		case IMISC_ELIXSTR:
		case IMISC_ELIXMAG:
		case IMISC_ELIXDEX:
		case IMISC_ELIXVIT:
			return *GetOptions().Gameplay.autoElixirPickup;
		case IMISC_OILFIRST:
		case IMISC_OILOF:
		case IMISC_OILACC:
		case IMISC_OILMAST:
		case IMISC_OILSHARP:
		case IMISC_OILDEATH:
		case IMISC_OILSKILL:
		case IMISC_OILBSMTH:
		case IMISC_OILFORT:
		case IMISC_OILPERM:
		case IMISC_OILHARD:
		case IMISC_OILIMP:
		case IMISC_OILLAST:
			return *GetOptions().Gameplay.autoOilPickup;
		default:
			return false;
		}
	}

	return false;
}

} // namespace

void AutoPickup(const Player &player)
{
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyAutoPickup(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
	#endif

	if (&player != MyPlayer)
		return;
	if (levelType() == DTYPE_TOWN && !*GetOptions().Gameplay.autoPickupInTown)
		return;

	for (auto pathDir : PathDirs) {
		const Point tile = player.position.tile + pathDir;
		const int8_t itemId = tileAt(tile).item();
		if (itemId != 0) {
			const int itemIndex = itemId - 1;
			auto &item = Items[itemIndex];
			if (DoPickup(item)) {
				NetSendCmdGItem(true, CMD_REQUESTAGITEM, player, itemIndex);
				item._iRequest = true;
			}
		}
	}
}
} // namespace devilution
