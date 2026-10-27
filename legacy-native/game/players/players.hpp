/**
 * @file game/players/players.hpp
 *
 * Interface of player functionality, leveling, actions, creation, loading, etc.
 */
#pragma once

#include <cstdint>
#include <vector>

#include <algorithm>
#include <array>
#include <string_view>

#include "application/diablo.h"
#include "application/game_mode.hpp"
#include "engine/actor.hpp"
#include "engine/actor_position.hpp"
#include "engine/attributes.hpp"
#include "engine/combat_actor.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/displacement.hpp"
#include "engine/math/point.hpp"
#include "engine/path.h"
#include "game/items/items.hpp"
#include "game/items/validation.h"
#include "game/levels/dun_tile.hpp"
#include "game/levels/dungeon_common.h"
#include "network/protocol/multi.h"
#include "tables/playerdat.hpp"
#include "tables/spelldat.h"
#include "ui/load_screens.h"
#include "utils/attributes.h"
#include "utils/enum_traits.h"
#include "utils/is_of.hpp"

namespace devilution {

constexpr int InventoryGridCells = 40;
constexpr int MaxBeltItems = 8;
constexpr int MaxResistance = 75;
constexpr uint8_t MaxSpellLevel = 15;
constexpr int PlayerNameLength = 32;
constexpr size_t NumHotkeys = 12;

// Walking directions
enum {
	// clang-format off
	WALK_NE   =  1,
	WALK_NW   =  2,
	WALK_SE   =  3,
	WALK_SW   =  4,
	WALK_N    =  5,
	WALK_E    =  6,
	WALK_S    =  7,
	WALK_W    =  8,
	WALK_NONE = -1,
	// clang-format on
};

enum class CharacterAttribute : uint8_t {
	Strength,
	Magic,
	Dexterity,
	Vitality,

	FIRST = Strength,
	LAST = Vitality
};

// Logical equipment locations
enum inv_body_loc : uint8_t {
	INVLOC_HEAD,
	INVLOC_RING_LEFT,
	INVLOC_RING_RIGHT,
	INVLOC_AMULET,
	INVLOC_HAND_LEFT,
	INVLOC_HAND_RIGHT,
	INVLOC_CHEST,
	NUM_INVLOC,
};

enum class player_graphic : uint8_t {
	Stand,
	Walk,
	Attack,
	Hit,
	Lightning,
	Fire,
	Magic,
	Death,
	Block,

	LAST = Block
};

enum class PlayerWeaponGraphic : uint8_t {
	Unarmed,
	UnarmedShield,
	Sword,
	SwordShield,
	Bow,
	Axe,
	Mace,
	MaceShield,
	Staff,
};

enum PLR_MODE : uint8_t {
	PM_STAND,
	PM_WALK_NORTHWARDS,
	PM_WALK_SOUTHWARDS,
	PM_WALK_SIDEWAYS,
	PM_ATTACK,
	PM_RATTACK,
	PM_BLOCK,
	PM_GOTHIT,
	PM_DEATH,
	PM_SPELL,
	PM_NEWLVL,
	PM_QUIT,
};

enum action_id : int8_t {
	// clang-format off
	ACTION_WALK        = -2, // Automatic walk when using gamepad
	ACTION_NONE        = -1,
	ACTION_ATTACK      = 9,
	ACTION_RATTACK     = 10,
	ACTION_SPELL       = 12,
	ACTION_OPERATE     = 13,
	ACTION_DISARM      = 14,
	ACTION_PICKUPITEM  = 15, // put item in hand (inventory screen open)
	ACTION_PICKUPAITEM = 16, // put item in inventory
	ACTION_TALK        = 17,
	ACTION_OPERATETK   = 18, // operate via telekinesis
	ACTION_ATTACKMON   = 20,
	ACTION_ATTACKPLR   = 21,
	ACTION_RATTACKMON  = 22,
	ACTION_RATTACKPLR  = 23,
	ACTION_SPELLMON    = 24,
	ACTION_SPELLPLR    = 25,
	ACTION_SPELLWALL   = 26,
	// clang-format on
};

enum class SpellFlag : uint8_t {
	// clang-format off
	None         = 0,
	Etherealize  = 1 << 0,
	RageActive   = 1 << 1,
	RageCooldown = 1 << 2,
	// bits 3-7 are unused
	// clang-format on
};
use_enum_as_flags(SpellFlag);

// When the player dies, what is the reason/source why?
enum class DeathReason {
	MonsterOrTrap, // Monster or Trap (dungeon)
	Player,        // Other player or selfkill (for example firewall)
	Unknown,       // HP is zero but we don't know when or where this happened
};

// Maps from armor animation to letter used in graphic files.
constexpr std::array<char, 3> ArmourChar = {
	'l', // light
	'm', // medium
	'h', // heavy
};

// Maps from weapon animation to letter used in graphic files.
constexpr std::array<char, 9> WepChar = {
	'n', // unarmed
	'u', // no weapon + shield
	's', // sword + no shield
	'd', // sword + shield
	'b', // bow
	'a', // axe
	'm', // blunt + no shield
	'h', // blunt + shield
	't', // staff
};

// Contains Data (CelSprites) for a player graphic (player_graphic)
struct PlayerAnimationData {
	OptionalOwnedClxSpriteSheet sprites; // Sprite lists for each of the 8 directions
	[[nodiscard]] ClxSpriteList spritesForDirection(Direction direction) const { return (*sprites)[static_cast<size_t>(direction)]; }
};

struct SpellCastInfo {
	SpellID spellId;
	SpellType spellType;
	int8_t spellFrom; // Inventory location for scrolls
	int spellLevel;   // Used for spell level
};

struct Player : CombatActor {
	Player() = default;
	Player(Player &&) noexcept = default;
	Player &operator=(Player &&) noexcept = default;

	char _pName[PlayerNameLength];
	Item InvBody[NUM_INVLOC];
	Item InvList[InventoryGridCells];
	Item SpdList[MaxBeltItems];
	Item HoldItem;

	int _pNumInv;
	PrimaryAttributes attributes;
	int _pStatPts;
	int _pDamageMod;
	VitalResource life;
	VitalResource mana;
	DamageBonuses damageBonuses;
	int _pIAC;
	int _pIBonusToHit;
	int _pIBonusAC;
	int _pIGetHit;
	uint32_t _pExperience;
	PLR_MODE _pmode;
	int8_t walkpath[MaxPathLengthPlayer];
	bool plractive;
	action_id destAction;
	int destParam1;
	int destParam2;
	int destParam3;
	int destParam4;
	int _pGold;

	OptionalClxSprite previewCelSprite;             // Contains a optional preview ClxSprite
	int8_t progressToNextGameTickWhenPreviewWasSet; // Contains the progress to next game tick when previewCelSprite was set
	ItemSpecialEffect _pIFlags;                     // Bitmask using item_special_effect

	// Contains Data (Sprites) for the different Animations
	std::array<PlayerAnimationData, enum_size<player_graphic>::value> AnimationData;
	mutable std::array<OptionalOwnedClxSpriteSheet, 2> PartyInfoSprites;
	mutable std::array<std::string, 2> PartyInfoSpriteLocations;
	int8_t _pNFrames;
	int8_t _pWFrames;
	int8_t _pAFrames;
	int8_t _pAFNum;
	int8_t _pSFrames;
	int8_t _pSFNum;
	int8_t _pHFrames;
	int8_t _pDFrames;
	int8_t _pBFrames;
	int8_t InvGrid[InventoryGridCells];

	uint8_t plrlevel;
	bool plrIsOnSetLevel;
	HeroClass _pClass;

private:
	uint8_t _pLevel = 1; // Use get/setCharacterLevel to ensure this attribute stays within the accepted range

public:
	uint8_t _pgfxnum; // Bitmask indicating what variant of the sprite the player is using. The 3 lower bits define weapon (PlayerWeaponGraphic) and the higher bits define armour (starting with PlayerArmorGraphic)
	int8_t _pISplLvlAdd;
	bool friendlyMode = true;    // Specifies whether players are in non-PvP mode
	SpellCastInfo queuedSpell;   // The next queued spell
	SpellCastInfo executedSpell; // The spell that is currently being cast
	SpellID inventorySpell;      // Which spell should be executed with CURSOR_TELEPORT
	int8_t spellFrom;            // Inventory location for scrolls with CURSOR_TELEPORT
	SpellID _pRSpell;
	SpellType _pRSplType;
	SpellID _pSBkSpell;
	uint8_t _pSplLvl[64];
	uint64_t _pISpells;    // Bitmask of staff spell
	uint64_t _pMemSpells;  // Bitmask of learned spells
	uint64_t _pAblSpells;  // Bitmask of abilities
	uint64_t _pScrlSpells; // Bitmask of spells available via scrolls
	SpellFlag _pSpellFlags;
	SpellID _pSplHotKey[NumHotkeys];
	SpellType _pSplTHotKey[NumHotkeys];
	bool _pBlockFlag;
	bool _pInvincible;
	int8_t _pLightRad;
	bool _pLvlChanging; // True when the player is transitioning between levels
	int8_t _pArmorClass;
	int8_t _pMagResist;
	int8_t _pFireResist;
	int8_t _pLghtResist;
	bool _pInfraFlag;
	Direction tempDirection; // Player's direction when ending movement. Also used for casting direction of SpellID::FireWall

	bool _pLvlVisited[NUMLEVELS];
	bool _pSLvlVisited[NUMLEVELS]; // only 10 used

	item_misc_id _pOilType;
	uint8_t pTownWarps;
	uint8_t pDungMsgs;
	uint8_t pLvlLoad;
	bool pManaShield;
	uint8_t pDungMsgs2;
	bool pOriginalCathedral;
	uint8_t pDiabloKillLevel;
	uint16_t wReflections;
	ItemSpecialEffectHf pDamAcFlags;

	[[nodiscard]] std::string_view name() const override { return _pName; }

	// Convenience function to get the base stats/bonuses for this player's class
	[[nodiscard]] const ClassAttributes &getClassAttributes() const { return GetClassAttributes(_pClass); }
	[[nodiscard]] const PlayerCombatData &getPlayerCombatData() const { return GetPlayerCombatDataForClass(_pClass); }
	[[nodiscard]] const PlayerData &getPlayerData() const { return GetPlayerDataForClass(_pClass); }

	// Gets the translated name for the character's class
	[[nodiscard]] std::string_view getClassName() const { return _(getPlayerData().className); }

	[[nodiscard]] int getBaseToBlock() const { return getPlayerCombatData().baseToBlock; }

	void CalcScrolls();
	bool CanUseItem(const Item &item) const;
	bool CanCleave();
	bool isEquipped(ItemType itemType, bool isTwoHanded = false);

	// Remove an item from player inventory
	// iv: invList index of item to be removed
	// calcScrolls: If true, CalcScrolls() gets called after removing item
	void RemoveInvItem(int iv, bool calcScrolls = true);

	// Returns the network identifier for this player
	[[nodiscard]] uint8_t getId() const;

	void RemoveSpdBarItem(int iv);

	// Gets the most valuable item out of all the player's items that match the given predicate.
	// itemPredicate: The predicate used to match the items.
	// return: The most valuable item out of all the player's items that match the given predicate, or 'nullptr' in case no
	// matching items were found.
	template <typename TPredicate>
	const Item *GetMostValuableItem(const TPredicate &itemPredicate) const
	{
		const auto getMostValuableItem = [&itemPredicate](const Item *begin, const Item *end, const Item *mostValuableItem = nullptr) {
			for (const auto *item = begin; item < end; item++) {
				if (item->isEmpty() || !itemPredicate(*item)) {
					continue;
				}

				if (mostValuableItem == nullptr || item->_iIvalue > mostValuableItem->_iIvalue) {
					mostValuableItem = item;
				}
			}

			return mostValuableItem;
		};

		const Item *mostValuableItem = getMostValuableItem(SpdList, SpdList + MaxBeltItems);
		mostValuableItem = getMostValuableItem(InvBody, InvBody + inv_body_loc::NUM_INVLOC, mostValuableItem);
		mostValuableItem = getMostValuableItem(InvList, InvList + _pNumInv, mostValuableItem);

		return mostValuableItem;
	}

	// Gets the base value of the player's specified attribute
	int GetBaseAttributeValue(CharacterAttribute attribute) const;

	// Gets the current value of the player's specified attribute
	int GetCurrentAttributeValue(CharacterAttribute attribute) const;

	// Gets the maximum value of the player's specified attribute
	int GetMaximumAttributeValue(CharacterAttribute attribute) const;

	// Get the tile coordinates a player is moving to (if not moving, then it corresponds to current position)
	Point GetTargetPosition() const;

	// Returns the index of the given position in `walkpath`, or -1 if not found
	int GetPositionPathIndex(Point position);

	// Says a speech line.
	// TODO: Prevent more than one speech to be played at a time (reject new requests).
	void Say(HeroSpeech speechId) const;

	// Says a speech line after a given delay.
	// speechId: The speech ID to say.
	// delay: Multiple of 50ms wait before starting the speech
	void Say(HeroSpeech speechId, int delay) const;

	// Says a speech line, without random variants.
	void SaySpecific(HeroSpeech speechId) const;

	// Attempts to stop the player from performing any queued up action. If the player is currently walking, his walking will
	// stop as soon as he reaches the next tile. If any action was queued with the previous command (like targeting a monster,
	// opening a chest, picking an item up, etc) this action will also be cancelled.
	void Stop();

	[[nodiscard]] bool isWalking() const override;           // Is the player currently walking?
	item_equip_type GetItemLocation(const Item &item) const; // Returns item location taking into consideration barbarian's
	                                                         // ability to hold two-handed maces and clubs in one hand.
	int GetArmor() const;                                    // Return player's armor value
	int GetMeleeToHit() const;                               // Return player's melee to hit value
	int GetMeleePiercingToHit() const;                       // Return player's melee to hit value, including armor piercing
	int GetRangedToHit() const;                              // Return player's ranged to hit value
	int GetRangedPiercingToHit() const;
	int GetMagicToHit() const; // Return magic hit chance

	// Return block chance
	// useLevel: indicate if player's level should be added to block chance (the only case where it isn't is blocking a trap)
	int GetBlockChance(bool useLevel = true) const;

	// Return reciprocal of the factor for calculating damage reduction due to Mana Shield.
	// Valid only for players with Mana Shield spell level greater than zero.
	int GetManaShieldDamageReduction();

	// Gets the effective spell level for the player, considering item bonuses
	// spell: SpellID enum member identifying the spell
	// return: effective spell level
	int GetSpellLevel(SpellID spell) const;

	// Return monster armor value after including player's armor piercing % (hellfire only)
	// monsterArmor: monster armor before applying % armor pierce
	// isMelee: indicates if it's melee or ranged combat
	int CalculateArmorPierce(int monsterArmor, bool isMelee) const;

	// Calculates the players current Hit Points as a percentage of their max HP and stores it for later reference
	// The stored value is unused... see life.percentage
	// return: The players current hit points as a percentage of their maximum (from 0 to 80%)
	int UpdateHitPointPercentage();

	int UpdateManaPercentage();

	// Restores between 1/8 (inclusive) and 1/4 (exclusive) of the players max HP (further adjusted by class).
	// This determines a random amount of non-fractional life points to restore then scales the value based on the
	//  player class. Warriors/barbarians get between 1/4 and 1/2 life restored per potion, rogue/monk/bard get 3/16
	//  to 3/8, and sorcerers get the base amount.
	void RestorePartialLife();
	void RestoreFullLife(); // Resets hp to maxHp

	// Restores between 1/8 (inclusive) and 1/4 (exclusive) of the players max Mana (further adjusted by class).
	// This determines a random amount of non-fractional mana points to restore then scales the value based on the
	//  player class. Sorcerers get between 1/4 and 1/2 mana restored per potion, rogue/monk/bard get 3/16 to 3/8,
	//  and warrior/barbarian get the base amount. However if the player can't use magic due to an equipped item then
	//  they get nothing.
	void RestorePartialMana();
	void RestoreFullMana(); // Resets mana to maxMana (if the player can use magic)

	// Sets the readied spell to the spell in the specified equipment slot. Does nothing if the item does not have a valid spell.
	// bodyLocation: the body location whose item will be checked for the spell.
	// forceSpell: if true, always change active spell, if false, only when current spell slot is empty
	void ReadySpellFromEquipment(inv_body_loc bodyLocation, bool forceSpell);

	bool UsesRangedWeapon() const; // Does the player currently have a ranged weapon equipped?
	bool CanChangeAction();

	[[nodiscard]] player_graphic getGraphic() const;
	[[nodiscard]] uint16_t getSpriteWidth() const;
	void getAnimationFramesAndTicksPerFrame(player_graphic graphics, int8_t &numberOfFrames, int8_t &ticksPerFrame) const;
	[[nodiscard]] ClxSprite currentSprite() const { return previewCelSprite ? *previewCelSprite : animInfo.currentSprite(); }

	// Updates previewCelSprite according to new requested command
	// cmdId: What command is requested
	// point: Point for the command
	// wParam1: First Parameter
	// wParam2: Second Parameter
	void UpdatePreviewCelSprite(_cmd_id cmdId, Point point, uint16_t wParam1, uint16_t wParam2);
	ClxSprite getPortraitSprite() const; // Get the current portrait sprite used for the party panel
	bool isUnarmed() const;

	[[nodiscard]] uint8_t getCharacterLevel() const { return _pLevel; }
	void setCharacterLevel(uint8_t level); // Sets the character level to the target level or nearest valid
	                                       // value (clamped to allowed range)
	[[nodiscard]] uint8_t getMaxCharacterLevel() const;
	[[nodiscard]] bool isMaxCharacterLevel() const { return getCharacterLevel() >= getMaxCharacterLevel(); }

private:
	void _addExperience(uint32_t experience, int levelDelta);

public:
	// Adds experience to the local player based on the current game mode
	// experience: base value to add, this will be adjusted to prevent power leveling in multiplayer games
	void addExperience(uint32_t experience);

	// Adds experience to the local player based on the difference between the monster level
	// and current level, then also applying the power level cap in multiplayer games.
	// experience: base value to add, will be scaled up/down by the difference between player and monster level
	// monsterLevel: level of the monster that has rewarded this experience
	void addExperience(uint32_t experience, int monsterLevel);

	[[nodiscard]] uint32_t getNextExperienceThreshold() const;

	bool isOnActiveLevel() const;           // Checks if the player is on the same level as the local player (MyPlayer)
	bool isOnLevel(uint8_t level) const;    // Checks if the player is on the corresponding level
	bool isOnLevel(_setlevels level) const; // Checks if the player is on the corresponding level
	bool isOnArenaLevel() const;            // Checks if the player is on a arena level
	void setLevel(uint8_t level);
	void setLevel(_setlevels level);

	int32_t calculateBaseLife() const; // Returns a character's life based on starting life, level, and base vitality.
	int32_t calculateBaseMana() const; // Returns a character's mana based on starting mana, level, and base magic.

	// Sets a tile/dPlayer to be occupied by the player
	// position: tile to update
	// isMoving: specifies whether the player is moving or not (true/moving results in a negative index in dPlayer)
	void occupyTile(Point position, bool isMoving) const;

	bool isLevelOwnedByLocalClient() const; // Checks if the player level is owned by local client

	bool isHoldingItem(const ItemType type) const; // Checks if the player is holding an item of the provided type, and is usable

	bool hasNoLife() const;
	bool hasNoMana() const;
	void initDungeonMessages();

	void loadGraphic(player_graphic graphic);
	void initGraphics();
	void resetGraphics();
	void setAnimation(player_graphic graphic, Direction dir, AnimationDistributionFlags flags = AnimationDistributionFlags::None, int8_t numSkippedFrames = 0, int8_t distributeFramesBeforeFrame = 0);
	void setAnimations();
	void create(HeroClass heroClass);
	int calculateStatDifference();
	void advanceLevel();
	void saveOldPosition();
	void fixLocation(Direction direction);
	void startStand(Direction direction);
	void startBlock(Direction direction);
	void startHit(int damage, bool forceHit);
	void startKill(DeathReason deathReason);
	void applyDamage(DamageType damageType, int damage, int minHitPoints = 0, int fraction = 0, DeathReason deathReason = DeathReason::MonsterOrTrap);
	void syncKill(DeathReason deathReason);
	void removeMissiles() const;
	void clearPath();
	bool positionIsAvailable(Point position) const;
	void makePath(Point targetPosition, bool endspace);
	void syncAnimation();
	void syncInitialPosition();
	void syncInitialState();
	void checkStats();
	void modifyStrength(int value);
	void modifyMagic(int value);
	void modifyDexterity(int value);
	void modifyVitality(int value);
	void setHitPoints(int value);
	void setStrength(int value);
	void setMagic(int value);
	void setDexterity(int value);
	void setVitality(int value);
};

extern DVL_API_FOR_TEST uint8_t MyPlayerId;
extern DVL_API_FOR_TEST Player *MyPlayer;
extern DVL_API_FOR_TEST std::vector<Player> Players;

// What Player items and stats should be displayed? Normally this is identical to MyPlayer but can differ when /inspect was used.
extern Player *InspectPlayer;

// Do we currently inspect a remote player (/inspect was used)? In this case the (remote) players items and stats can't be modified.
inline bool IsInspectingPlayer() { return MyPlayer != InspectPlayer; }
extern DVL_API_FOR_TEST bool MyPlayerIsDead;

Player *PlayerAtPosition(Point position, bool ignoreMovingPlayers = false);

void AddPlrMonstExper(int lvl, unsigned int exp, char pmask);
void InitPlayer(Player &player, bool FirstTime);
void InitMultiView();
void PlrClrTrans(Point position);
void PlrDoTrans(Point position);
void FixPlrWalkTags(const Player &player);
void StripTopGold(Player &player); // Strip the top off gold piles that are larger than MaxGold
void StartNewLvl(Player &player, interface_mode fom, int lvl);
void RestartTownLvl(Player &player);
/** Starts the legacy town-loading presentation without deciding authoritative resources or position. */
void PresentRestartTown(Player &player);
void StartWarpLvl(Player &player, size_t pidx);
void ProcessPlayers();
void CheckPlrSpell(bool isShiftHeld, SpellID spellID = MyPlayer->_pRSpell, SpellType spellType = MyPlayer->_pRSplType);
void PlayDungMsgs();

} // namespace devilution
