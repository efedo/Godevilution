/**
 * @file game/items/items.hpp
 *
 * Interface for items.
 */


// Item data, attributes, and floor/inventory management.
#pragma once

#include <cstdint>
#include <optional>

#include "ui/menu/ui_flags.hpp"
#include "engine/cursor.h"
#include "engine/animationinfo.h"
#include "engine/math/point.hpp"
#include "engine/gfx/surface.hpp"
#include "game/levels/dun_tile.hpp"
#include "game/monsters/monsters.hpp"
#include "tables/itemdat.h"
#include "utils/is_of.hpp"
#include "utils/string/string_or_view.hpp"

namespace devilution {

constexpr int MAXITEMS = 127;
constexpr int ITEMTYPES = 43;

constexpr int GOLD_SMALL_LIMIT = 1000;
constexpr int GOLD_MEDIUM_LIMIT = 2500;
constexpr int GOLD_MAX_LIMIT = 5000;

constexpr uint8_t DUR_INDESTRUCTIBLE = 255; // Sentinel: item cannot be damaged.

constexpr int ItemNameLength = 64;
constexpr int MaxVendorValue = 140000;
constexpr int MaxVendorValueHf = 200000;
constexpr int MaxBoyValue = 90000;
constexpr int MaxBoyValueHf = 200000;

enum item_quality : uint8_t {
	ITEM_QUALITY_NORMAL,
	ITEM_QUALITY_MAGIC,
	ITEM_QUALITY_UNIQUE,
};

enum _unique_items : int32_t {
	UITEM_CLEAVER,
	UITEM_SKCROWN,
	UITEM_INFRARING,
	UITEM_OPTAMULET,
	UITEM_TRING,
	UITEM_HARCREST,
	UITEM_STEELVEIL,
	UITEM_ARMOFVAL,
	UITEM_GRISWOLD,
	UITEM_BOVINE,
	UITEM_RIFTBOW,
	UITEM_NEEDLER,
	UITEM_CELESTBOW,
	UITEM_DEADLYHUNT,
	UITEM_BOWOFDEAD,
	UITEM_BLKOAKBOW,
	UITEM_FLAMEDART,
	UITEM_FLESHSTING,
	UITEM_WINDFORCE,
	UITEM_EAGLEHORN,
	UITEM_GONNAGALDIRK,
	UITEM_DEFENDER,
	UITEM_GRYPHONCLAW,
	UITEM_BLACKRAZOR,
	UITEM_GIBBOUSMOON,
	UITEM_ICESHANK,
	UITEM_EXECUTIONER,
	UITEM_BONESAW,
	UITEM_SHADHAWK,
	UITEM_WIZSPIKE,
	UITEM_LGTSABRE,
	UITEM_FALCONTALON,
	UITEM_INFERNO,
	UITEM_DOOMBRINGER,
	UITEM_GRIZZLY,
	UITEM_GRANDFATHER,
	UITEM_MANGLER,
	UITEM_SHARPBEAK,
	UITEM_BLOODLSLAYER,
	UITEM_CELESTAXE,
	UITEM_WICKEDAXE,
	UITEM_STONECLEAV,
	UITEM_AGUHATCHET,
	UITEM_HELLSLAYER,
	UITEM_MESSERREAVER,
	UITEM_CRACKRUST,
	UITEM_JHOLMHAMM,
	UITEM_CIVERBS,
	UITEM_CELESTSTAR,
	UITEM_BARANSTAR,
	UITEM_GNARLROOT,
	UITEM_CRANBASH,
	UITEM_SCHAEFHAMM,
	UITEM_DREAMFLANGE,
	UITEM_STAFFOFSHAD,
	UITEM_IMMOLATOR,
	UITEM_STORMSPIRE,
	UITEM_GLEAMSONG,
	UITEM_THUNDERCALL,
	UITEM_PROTECTOR,
	UITEM_NAJPUZZLE,
	UITEM_MINDCRY,
	UITEM_RODOFONAN,
	UITEM_SPIRITSHELM,
	UITEM_THINKINGCAP,
	UITEM_OVERLORDHELM,
	UITEM_FOOLSCREST,
	UITEM_GOTTERDAM,
	UITEM_ROYCIRCLET,
	UITEM_TORNFLESH,
	UITEM_GLADBANE,
	UITEM_RAINCLOAK,
	UITEM_LEATHAUT,
	UITEM_WISDWRAP,
	UITEM_SPARKMAIL,
	UITEM_SCAVCARAP,
	UITEM_NIGHTSCAPE,
	UITEM_NAJPLATE,
	UITEM_DEMONSPIKE,
	UITEM_DEFLECTOR,
	UITEM_SKULLSHLD,
	UITEM_DRAGONBRCH,
	UITEM_BLKOAKSHLD,
	UITEM_HOLYDEF,
	UITEM_STORMSHLD,
	UITEM_BRAMBLE,
	UITEM_REGHA,
	UITEM_BLEEDER,
	UITEM_CONSTRICT,
	UITEM_ENGAGE,
	UITEM_INVALID = -1,
};

/*
CF_LEVEL: Item Level (6 bits; value ranges from 0-63)
CF_ONLYGOOD: Item is not able to have affixes with PLOK set to false
CF_UPER15: Item is from a Unique Monster and has 15% chance of being a Unique Item
CF_UPER1: Item is from the dungeon and has a 1% chance of being a Unique Item
CF_UNIQUE: Item is a Unique Item
CF_SMITH: Item is from Griswold (Basic)
CF_SMITHPREMIUM: Item is from Griswold (Premium)
CF_BOY: Item is from Wirt
CF_WITCH: Item is from Adria
CF_HEALER: Item is from Pepin
CF_PREGEN: Item is pre-generated, mostly associated with Quest items found in the dungeon or potions on the dungeon floor

Items that have both CF_UPER15 and CF_UPER1 are CF_USEFUL, which is used to generate Potions and Town Portal scrolls on the dungeon floor
Items that have any of CF_SMITH, CF_SMITHPREMIUM, CF_BOY, CF_WICTH, and CF_HEALER are CF_TOWN, indicating the item is sourced from an NPC
*/
enum icreateinfo_flag {
	// clang-format off
	CF_LEVEL        = (1 << 6) - 1,
	CF_ONLYGOOD     = 1 << 6,
	CF_UPER15       = 1 << 7,
	CF_UPER1        = 1 << 8,
	CF_UNIQUE       = 1 << 9,
	CF_SMITH        = 1 << 10,
	CF_SMITHPREMIUM = 1 << 11,
	CF_BOY          = 1 << 12,
	CF_WITCH        = 1 << 13,
	CF_HEALER       = 1 << 14,
	CF_PREGEN       = 1 << 15,

	CF_USEFUL = CF_UPER15 | CF_UPER1,
	CF_TOWN   = CF_SMITH | CF_SMITHPREMIUM | CF_BOY | CF_WITCH | CF_HEALER,
	// clang-format on
};

enum icreateinfo_flag2 {
	// clang-format off
	CF_HELLFIRE = 1 << 0,
	CF_UIDOFFSET = ((1 << 4) - 1) << 1,
	// clang-format on
};

// All item animation frames have this width.
constexpr int ItemAnimWidth = 96;

// Defined in player.h, forward declared here to allow for functions which operate in the context of a player.
struct Player;

struct Item {
	
	uint32_t _iSeed = 0; // Randomly generated identifier
	uint16_t _iCreateInfo = 0;
	ItemType _itype = ItemType::None;
	bool _iAnimFlag = false;
	Point position = { 0, 0 };
	AnimationInfo animInfo;
	bool _iDelFlag = false; // set when item is flagged for deletion, deprecated in 1.02
	SelectionRegion selectionRegion = SelectionRegion::None;
	bool _iPostDraw = false;
	bool _iIdentified = false;
	item_quality _iMagical = ITEM_QUALITY_NORMAL;
	char _iName[ItemNameLength] = {};
	char _iIName[ItemNameLength] = {};
	item_equip_type _iLoc = ILOC_NONE;
	item_class _iClass = ICLASS_NONE;
	uint8_t _iCurs = 0;
	int _ivalue = 0;
	int _iIvalue = 0;
	uint8_t _iMinDam = 0;
	uint8_t _iMaxDam = 0;
	int16_t _iAC = 0;
	ItemSpecialEffect _iFlags = ItemSpecialEffect::None;
	item_misc_id _iMiscId = IMISC_NONE;
	SpellID _iSpell = SpellID::Null;
	_item_indexes IDidx = IDI_NONE;
	int _iCharges = 0;
	int _iMaxCharges = 0;
	int _iDurability = 0;
	int _iMaxDur = 0;
	int16_t _iPLDam = 0;
	int16_t _iPLToHit = 0;
	int16_t _iPLAC = 0;
	int16_t _iPLStr = 0;
	int16_t _iPLMag = 0;
	int16_t _iPLDex = 0;
	int16_t _iPLVit = 0;
	int16_t _iPLFR = 0;
	int16_t _iPLLR = 0;
	int16_t _iPLMR = 0;
	int16_t _iPLMana = 0;
	int16_t _iPLHP = 0;
	int16_t _iPLDamMod = 0;
	int16_t _iPLGetHit = 0;
	int16_t _iPLLight = 0;
	int8_t _iSplLvlAdd = 0;
	bool _iRequest = false;
	int _iUid = 0; // Unique item ID, used as an index into UniqueItemList
	int16_t _iFMinDam = 0;
	int16_t _iFMaxDam = 0;
	int16_t _iLMinDam = 0;
	int16_t _iLMaxDam = 0;
	int16_t _iPLEnAc = 0;
	enum item_effect_type _iPrePower = IPL_INVALID;
	enum item_effect_type _iSufPower = IPL_INVALID;
	int _iVAdd1 = 0;
	int _iVMult1 = 0;
	int _iVAdd2 = 0;
	int _iVMult2 = 0;
	int8_t _iMinStr = 0;
	uint8_t _iMinMag = 0;
	int8_t _iMinDex = 0;
	bool _iStatFlag = false;
	ItemSpecialEffectHf _iDamAcFlags = ItemSpecialEffectHf::None;
	uint32_t dwBuff = 0;

	Item pop() &; // Clears this item and returns the old value

	// Resets the item so isEmpty() returns true.
	DVL_REINITIALIZES void clear();
	bool isEmpty() const;
	bool isEquipment() const;
	bool isWeapon() const;
	bool isArmor() const;
	bool isGold() const;
	bool isHelm() const;
	bool isShield() const;
	bool isJewelry() const;
	[[nodiscard]] bool isScroll() const;
	[[nodiscard]] bool isScrollOf(SpellID spellId) const;
	[[nodiscard]] bool isRune() const;
	[[nodiscard]] bool isRuneOf(SpellID spellId) const;
	[[nodiscard]] bool isUsable() const;
	[[nodiscard]] bool keyAttributesMatch(uint32_t seed, _item_indexes itemIndex, uint16_t createInfo) const;

	UiFlags getTextColor() const;

	UiFlags getTextColorWithStatCheck() const;

	// showAnimation: true plays the flip animation; false shows only the final ground frame.
	void setNewAnimation(bool showAnimation);

	// For spell books, recalculates the magic requirement for the next level; sets _iStatFlag for all items.
	void updateRequiredStatsCacheForPlayer(const Player &player);

	// Returns the translated item name to display (respects identified flag).
	StringOrView getName() const;

	[[nodiscard]] Displacement getRenderingOffset(const ClxSprite sprite) const;
};

struct ItemGetRecordStruct {
	uint32_t nSeed;
	uint16_t wCI;
	int nIndex;
	uint32_t dwTimestamp;
};

struct CornerStoneStruct {
	Point position;
	bool activated;
	Item item;
	bool isAvailable();
};

/** Contains the items on ground in the current game. Storage is owned by gItemPool (item_pool.cpp). */
extern DVL_API_FOR_TEST Item *Items;
extern bool ShowUniqueItemInfoBox;
extern CornerStoneStruct CornerStone;
extern DVL_API_FOR_TEST bool UniqueItemFlags[128];

uint8_t GetOutlineColor(const Item &item, bool checkReq);
bool IsItemAvailable(int i);
void ClearUniqueItemFlags();
void InitItemGFX();
void InitItems();
void CalcPlrItemVals(Player &player, bool Loadgfx);
void CalcPlrInv(Player &player, bool Loadgfx);
void InitializeItem(Item &item, _item_indexes itemData);
void GenerateNewSeed(Item &item);
int GetGoldCursor(int value);

// Updates the gold cursor sprite to match the item's value
void SetPlrHandGoldCurs(Item &gold);
void CreatePlrItems(Player &player);
bool ItemSpaceOk(Point position);
int AllocateItem();

// Places item on the dungeon floor at position; item should not be used after this call.
// Returns the index assigned to the item.
uint8_t PlaceItemInWorld(Item &&item, WorldTilePosition position);
Point GetSuperItemLoc(Point position);
void GetItemAttrs(Item &item, _item_indexes itemData, int lvl);
void SetupItem(Item &item);
#ifdef BUILD_TESTING
// Detached scalar metadata for independent item-generation fixtures.
std::optional<int> GetItemAnimationLengthForTesting(int cursor);
// Calls the actual native selector; no allocation, placement or seed conversion.
int SelectThemeItemForTesting(bool onlyGood, int itemType = -1, int miscId = -1);
#endif
Item *SpawnUnique(_unique_items uid, Point position, std::optional<int> level = std::nullopt, bool sendmsg = true, bool exactPosition = false);
void GetSuperItemSpace(Point position, int8_t inum);
_item_indexes RndItemForMonsterLevel(int8_t monsterLevel);
void SetupAllItems(const Player &player, Item &item, _item_indexes idx, uint32_t iseed, int lvl, int uper, bool onlygood, bool pregen, int uidOffset = 0, bool forceNotUnique = false);
void TryRandomUniqueItem(Item &item, _item_indexes idx, int8_t mLevel, int uper, bool onlygood, bool pregen);
void SpawnItem(Monster &monster, Point position, bool sendmsg, bool spawn = false);
void CreateRndItem(Point position, bool onlygood, bool sendmsg, bool delta);
void CreateRndUseful(Point position, bool sendmsg);
void CreateTypeItem(Point position, bool onlygood, ItemType itemType, int imisc, bool sendmsg, bool delta, bool spawn = false);
void RecreateItem(const Player &player, Item &item, _item_indexes idx, uint16_t icreateinfo, uint32_t iseed, int ivalue, uint32_t dwBuff);
void RecreateEar(Item &item, uint16_t ic, uint32_t iseed, uint8_t bCursval, std::string_view heroName);
void CornerstoneSave();
void CornerstoneLoad(Point position);
void SpawnQuestItem(_item_indexes itemid, Point position, int randarea, SelectionRegion selectionRegion, bool sendmsg);
void SpawnRewardItem(_item_indexes itemid, Point position, bool sendmsg);
void SpawnMapOfDoom(Point position, bool sendmsg);
void SpawnRuneBomb(Point position, bool sendmsg);
void SpawnTheodore(Point position, bool sendmsg);
void RespawnItem(Item &item, bool FlipFlag);
void DeleteItem(int i);
void ProcessItems();
void FreeItemGFX();
void GetItemFrm(Item &item);
void GetItemStr(Item &item);
void CheckIdentify(Player &player, int cii);
void DoRepair(Player &player, int cii);
void DoRecharge(Player &player, int cii);
bool DoOil(Player &player, int cii);
[[nodiscard]] StringOrView PrintItemPower(char plidx, const Item &item);
void DrawUniqueInfo(const Surface &out);
void PrintItemDetails(const Item &item);
void PrintItemDur(const Item &item);
void UseItem(Player &player, item_misc_id Mid, SpellID spellID, int spellFrom);
bool UseItemOpensHive(const Item &item, Point position);
bool UseItemOpensGrave(const Item &item, Point position);
void SpawnSmith(int lvl);
void ReplacePremium(const Player &player, int idx);
void SpawnPremium(const Player &player);
void SpawnWitch(int lvl);
void SpawnBoy(int lvl);
void SpawnHealer(int lvl);
void MakeGoldStack(Item &goldItem, int value);
int ItemNoFlippy();
void CreateSpellBook(Point position, SpellID ispell, bool sendmsg, bool delta);
void CreateMagicArmor(Point position, ItemType itemType, int icurs, bool sendmsg, bool delta);
void CreateAmulet(Point position, int lvl, bool sendmsg, bool delta, bool spawn = false);
void CreateMagicWeapon(Point position, ItemType itemType, int icurs, bool sendmsg, bool delta);
bool GetItemRecord(uint32_t nSeed, uint16_t wCI, int nIndex);
void SetItemRecord(uint32_t nSeed, uint16_t wCI, int nIndex);
void PutItemRecord(uint32_t nSeed, uint16_t wCI, int nIndex);

void initItemGetRecords(); // Resets item get records.

void RepairItem(Item &item, int lvl);
void RechargeItem(Item &item, Player &player);
bool ApplyOilToItem(Item &item, Player &player);
void UpdateHellfireFlag(Item &item, const char *identifiedItemName); // Sets CF_HELLFIRE in dwBuff if the item was generated in vanilla Hellfire.

/* data */

extern int MaxGold;

extern int8_t ItemCAnimTbl[];
extern SfxID ItemInvSnds[];

} // namespace devilution
