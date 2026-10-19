/**
 * @file game/stores/stores.hpp
 *
 * Interface of functionality for stores and towner dialogs.
 */
#pragma once

#include <cstdint>
#include <functional>
#include <optional>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#include "ui/menu/ui_flags.hpp"
#include "ui/panel/control.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/gfx/surface.hpp"
#include "application/game_mode.hpp"
#include "game/items/items.hpp"
#include "utils/attributes.h"
#include "utils/container/static_vector.hpp"

namespace devilution {

constexpr int NumSmithBasicItems = 19;
constexpr int NumSmithBasicItemsHf = 24;

constexpr int NumSmithItems = 6;
constexpr int NumSmithItemsHf = 15;

constexpr int NumHealerItems = 17;
constexpr int NumHealerItemsHf = 19;
constexpr int NumHealerPinnedItems = 2;
constexpr int NumHealerPinnedItemsMp = 3;

constexpr int NumWitchItems = 17;
constexpr int NumWitchItemsHf = 24;
constexpr int NumWitchPinnedItems = 3;

constexpr int NumStoreLines = 104;

enum class TalkID : uint8_t {
	None,
	Smith,
	SmithBuy,
	SmithSell,
	SmithRepair,
	Witch,
	WitchBuy,
	WitchSell,
	WitchRecharge,
	WitchRefillMana,
	NoMoney,
	NoRoom,
	Confirm,
	Boy,
	BoyBuy,
	Healer,
	Storyteller,
	HealerBuy,
	StorytellerIdentify,
	SmithPremiumBuy,
	Gossip,
	StorytellerIdentifyShow,
	Tavern,
	Drunk,
	Barmaid,
};

/**
 * Returns whether a classic store purchase/service action has a server-backed
 * command route. Connected sessions must not fall through to legacy mutation
 * for actions that are not listed here.
 */
constexpr bool HasServerBackedStoreAction(TalkID store) noexcept
{
	switch (store) {
	case TalkID::SmithBuy:
	case TalkID::SmithSell:
	case TalkID::SmithRepair:
	case TalkID::WitchBuy:
	case TalkID::WitchSell:
	case TalkID::WitchRecharge:
	case TalkID::BoyBuy:
	case TalkID::HealerBuy:
	case TalkID::StorytellerIdentify:
		return true;
	default:
		return false;
	}
}

struct TownerDialogOption {
	std::function<std::string()> getLabel;
	std::function<void()> onSelect;
};

class StoreManager {
public:
	// Currently selected text line from TextLine
	DVL_API_FOR_TEST int currentTextLine() const { return currentTextLine_; }
	DVL_API_FOR_TEST int &currentTextLine() { return currentTextLine_; }

	// Remember currently selected text line from TextLine while displaying a dialog
	DVL_API_FOR_TEST int oldTextLine() const { return oldTextLine_; }
	DVL_API_FOR_TEST int &oldTextLine() { return oldTextLine_; }

	// Scroll position
	DVL_API_FOR_TEST int scrollPos() const { return scrollPos_; }
	DVL_API_FOR_TEST int &scrollPos() { return scrollPos_; }

	// Remember last scroll position
	DVL_API_FOR_TEST int oldScrollPos() const { return oldScrollPos_; }
	DVL_API_FOR_TEST int &oldScrollPos() { return oldScrollPos_; }

	// Currently active store
	DVL_API_FOR_TEST TalkID activeStore() const { return activeStore_; }
	DVL_API_FOR_TEST TalkID &activeStore() { return activeStore_; }

	// Remember current store while displaying a dialog
	DVL_API_FOR_TEST TalkID oldActiveStore() const { return oldActiveStore_; }
	DVL_API_FOR_TEST TalkID &oldActiveStore() { return oldActiveStore_; }

	// Current index into PlayerItemIndexes/PlayerItems
	DVL_API_FOR_TEST int currentItemIndex() const { return currentItemIndex_; }
	DVL_API_FOR_TEST int &currentItemIndex() { return currentItemIndex_; }

	// Temporary item used to hold the item being traded
	DVL_API_FOR_TEST Item &tempItem() { return tempItem_; }

	// Items sold by Griswold
	DVL_API_FOR_TEST StaticVector<Item, NumSmithBasicItemsHf> &smithItems() { return smithItems_; }
	DVL_API_FOR_TEST const StaticVector<Item, NumSmithBasicItemsHf> &smithItems() const { return smithItems_; }

	DVL_API_FOR_TEST int premiumItemCount() const { return premiumItemCount_; }
	DVL_API_FOR_TEST int &premiumItemCount() { return premiumItemCount_; }

	DVL_API_FOR_TEST int premiumItemLevel() const { return premiumItemLevel_; }
	DVL_API_FOR_TEST int &premiumItemLevel() { return premiumItemLevel_; }

	DVL_API_FOR_TEST StaticVector<Item, NumSmithItemsHf> &premiumItems() { return premiumItems_; }
	DVL_API_FOR_TEST const StaticVector<Item, NumSmithItemsHf> &premiumItems() const { return premiumItems_; }

	// Items sold by Pepin
	DVL_API_FOR_TEST StaticVector<Item, NumHealerItemsHf> &healerItems() { return healerItems_; }
	DVL_API_FOR_TEST const StaticVector<Item, NumHealerItemsHf> &healerItems() const { return healerItems_; }

	// Items sold by Adria
	DVL_API_FOR_TEST StaticVector<Item, NumWitchItemsHf> &witchItems() { return witchItems_; }
	DVL_API_FOR_TEST const StaticVector<Item, NumWitchItemsHf> &witchItems() const { return witchItems_; }

	// Wirt item
	DVL_API_FOR_TEST int boyItemLevel() const { return boyItemLevel_; }
	DVL_API_FOR_TEST int &boyItemLevel() { return boyItemLevel_; }

	DVL_API_FOR_TEST Item &boyItem() { return boyItem_; }
	DVL_API_FOR_TEST const Item &boyItem() const { return boyItem_; }

	// Extra dialog options injected by mods, keyed by towner short name.
	DVL_API_FOR_TEST std::vector<std::pair<std::string, std::vector<TownerDialogOption>>> &extraTownerOptions()
	{
		return extraTownerOptions_;
	}

	// Map of inventory items being presented in the store
	int8_t (&playerItemIndexes())[48] { return playerItemIndexes_; }

	// Copies of the players items as presented in the store
	DVL_API_FOR_TEST Item (&playerItems())[48] { return playerItems_; }

	/**
	 * @brief Returns the towner short name for a top-level TalkID, or nullptr if not a towner store.
	 *
	 * Only maps top-level store entries (Smith, Witch, Boy, Healer, Storyteller, Tavern, Drunk, Barmaid).
	 * Sub-pages (SmithBuy, WitchSell, etc.) return nullptr.
	 */
	DVL_API_FOR_TEST std::optional<std::string_view> TownerNameForTalkID(TalkID s);

	/**
	 * @brief Registers a dynamic dialog option for a towner's talk menu.
	 *
	 * Options are inserted into empty even-numbered lines before the towner's "leave" option.
	 * If no empty lines are available the option is silently skipped for that dialog session
	 * and a warning is logged.
	 *
	 * When the player selects a mod option, onSelect() is called. By default the dialog is
	 * closed afterwards. If onSelect() sets ActiveStore to a value other than TalkID::None,
	 * that value is preserved (e.g. to open a sub-dialog).
	 *
	 * @param townerName   Short name of the towner (e.g. "farnham"). Must match one of the
	 *                     names in TownerShortNames; a warning is logged for unknown names.
	 * @param getLabel     Called when the dialog is built; return a non-empty string to show the
	 *                     option, or an empty string to hide it.
	 * @param onSelect     Called when the player chooses this option.
	 */
	void RegisterTownerDialogOption(std::string_view townerName,
	    std::function<std::string()> getLabel,
	    std::function<void()> onSelect);

	// Clears all mod-registered towner dialog options.
	// Must be called before the Lua state is destroyed, since registered callbacks
	// capture sol::function handles that reference the Lua state.
	void ClearTownerDialogOptions();

	void AddStoreHoldRepair(Item *itm, int8_t i);

	// Clears premium items sold by Griswold and Wirt.
	void InitStores();

	// Spawns items sold by vendors, including premium items sold by Griswold and Wirt.
	void SetupTownStores();

	void FreeStoreMem();

	void PrintSString(const Surface &out, int margin, int line, std::string_view text, UiFlags flags, int price = 0, int cursId = -1, bool cursIndent = false);
	void DrawSLine(const Surface &out, int sy);
	void DrawSTextHelp();
	void ClearSText(int s, int e);
	void StartStore(TalkID s);
	void DrawSText(const Surface &out);
	void StoreESC();
	void StoreUp();
	void StoreDown();
	void StorePrior();
	void StoreNext();
	void TakePlrsMoney(int cost);
	void StoreEnter();
	void CheckStoreBtn();
	void ReleaseStoreBtn();
	bool IsPlayerInStore();

	// @brief Places an item in the player's inventory, belt, or equipment.
	// persistItem: If true, actually place the item. If false, just check if it can be placed.
	// return: true if the item can be/was placed.
	bool StoreAutoPlace(Item &item, bool persistItem);
	bool PlayerCanAfford(int price);

	// Check if Griswold will buy this item.
	bool SmithWillBuy(const Item &item);

	// Check if Adria will buy this item.
	bool WitchWillBuy(const Item &item);

private:
	TalkID activeStore_ = TalkID::None;
	int currentItemIndex_ = 0;
	int8_t playerItemIndexes_[48] = {};
	Item playerItems_[48] = {};

	StaticVector<Item, NumSmithBasicItemsHf> smithItems_;
	int premiumItemCount_ = 0;
	int premiumItemLevel_ = 0;
	StaticVector<Item, NumSmithItemsHf> premiumItems_;

	StaticVector<Item, NumHealerItemsHf> healerItems_;

	StaticVector<Item, NumWitchItemsHf> witchItems_;

	int boyItemLevel_ = 0;
	Item boyItem_ = {};

	int currentTextLine_ = 0;
	int oldTextLine_ = 0;
	int scrollPos_ = 0;
	int oldScrollPos_ = 0;
	TalkID oldActiveStore_ = TalkID::None;
	Item tempItem_ = {};

	std::vector<std::pair<std::string, std::vector<TownerDialogOption>>> extraTownerOptions_;
};

extern DVL_API_FOR_TEST StoreManager CurrentStoreManager;

} // namespace devilution
