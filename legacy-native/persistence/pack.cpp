/**
 * @file persistence/pack.cpp
 *
 * Implementation of functions for minifying player data structure.
 */
#include "persistence/pack.h"

#include <cstdint>

#include "engine/random.hpp"
#include "application/game_mode.hpp"
#include "game/items/validation.h"
#include "persistence/loadsave.h"
#include "ui/player_messages.h"
#include "game/stores/stores.hpp"
#include "tables/playerdat.hpp"
#include "utils/endian/endian_read.hpp"
#include "utils/endian/endian_swap.hpp"
#include "utils/is_of.hpp"
#include "utils/log.hpp"
#include "utils/string/utf8.hpp"

#define ValidateField(logValue, condition)                         \
	do {                                                           \
		if (!(condition)) {                                        \
			LogFailedJoinAttempt(#condition, #logValue, logValue); \
			EventFailedJoinAttempt(player._pName);                 \
			return false;                                          \
		}                                                          \
	} while (0)

#define ValidateFields(logValue1, logValue2, condition)                                     \
	do {                                                                                    \
		if (!(condition)) {                                                                 \
			LogFailedJoinAttempt(#condition, #logValue1, logValue1, #logValue2, logValue2); \
			EventFailedJoinAttempt(player._pName);                                          \
			return false;                                                                   \
		}                                                                                   \
	} while (0)

namespace devilution {

namespace {

void EventFailedJoinAttempt(const char *playerName)
{
	const std::string message = fmt::format("Player '{}' sent invalid player data during attempt to join the game.", playerName);
	EventPlrMsg(message);
}

template <typename T>
void LogFailedJoinAttempt(const char *condition, const char *name, T value)
{
	LogDebug("Remote player validation failed: ValidateField({}: {}, {})", name, value, condition);
}

template <typename T1, typename T2>
void LogFailedJoinAttempt(const char *condition, const char *name1, T1 value1, const char *name2, T2 value2)
{
	LogDebug("Remote player validation failed: ValidateFields({}: {}, {}: {}, {})", name1, value1, name2, value2, condition);
}

void VerifyGoldSeeds(Player &player)
{
	for (int i = 0; i < player._pNumInv; i++) {
		if (player.InvList[i].IDidx != IDI_GOLD)
			continue;
		for (int j = 0; j < player._pNumInv; j++) {
			if (i == j)
				continue;
			if (player.InvList[j].IDidx != IDI_GOLD)
				continue;
			if (player.InvList[i]._iSeed != player.InvList[j]._iSeed)
				continue;
			player.InvList[i]._iSeed = AdvanceRndSeed();
			j = -1;
		}
	}
}

} // namespace

bool RecreateHellfireSpellBook(const Player &player, const TItem &packedItem, Item *item)
{
	Item spellBook {};
	RecreateItem(player, packedItem, spellBook);

	// Hellfire uses the spell book level when generating items via CreateSpellBook()
	int spellBookLevel = GetSpellBookLevel(spellBook._iSpell);

	// CreateSpellBook() adds 1 to the spell level for ilvl
	spellBookLevel++;

	if (spellBookLevel >= 1 && (spellBook._iCreateInfo & CF_LEVEL) == spellBookLevel * 2) {
		// The ilvl matches the result for a spell book drop, so we confirm the item is legitimate
		if (item != nullptr)
			*item = spellBook;
		return true;
	}

	ValidateFields(spellBook._iCreateInfo, spellBook.dwBuff, IsDungeonItemValid(spellBook._iCreateInfo, spellBook.dwBuff));
	if (item != nullptr)
		*item = spellBook;
	return true;
}

void PackItem(ItemPack &packedItem, const Item &item, bool isHellfire)
{
	packedItem = {};
	// Arena potions don't exist in vanilla so don't save them to stay backward compatible
	if (item.isEmpty() || item._iMiscId == IMISC_ARENAPOT) {
		packedItem.idx = 0xFFFF;
	} else {
		auto idx = item.IDidx;
		if (!isHellfire) {
			idx = RemapItemIdxToDiablo(idx);
		}
		if (gbIsSpawn) {
			idx = RemapItemIdxToSpawn(idx);
		}
		packedItem.idx = Swap16LE(idx);
		if (item.IDidx == IDI_EAR) {
			packedItem.iCreateInfo = Swap16LE(item._iIName[1] | (item._iIName[0] << 8));
			packedItem.iSeed = Swap32LE(LoadBE32(&item._iIName[2]));
			packedItem.bId = item._iIName[6];
			packedItem.bDur = item._iIName[7];
			packedItem.bMDur = item._iIName[8];
			packedItem.bCh = item._iIName[9];
			packedItem.bMCh = item._iIName[10];
			packedItem.wValue = Swap16LE(item._ivalue | (item._iIName[11] << 8) | ((item._iCurs - ICURS_EAR_SORCERER) << 6));
			packedItem.dwBuff = Swap32LE(LoadBE32(&item._iIName[12]));
		} else {
			packedItem.iSeed = Swap32LE(item._iSeed);
			packedItem.iCreateInfo = Swap16LE(item._iCreateInfo);
			packedItem.bId = (item._iMagical << 1) | (item._iIdentified ? 1 : 0);
			if (item._iMaxDur > 255)
				packedItem.bMDur = 254;
			else
				packedItem.bMDur = item._iMaxDur;
			packedItem.bDur = std::min<int32_t>(item._iDurability, packedItem.bMDur);

			packedItem.bCh = item._iCharges;
			packedItem.bMCh = item._iMaxCharges;
			if (item.IDidx == IDI_GOLD)
				packedItem.wValue = Swap16LE(item._ivalue);
			packedItem.dwBuff = item.dwBuff;
		}
	}
}

void PackPlayer(PlayerPack &packed, const Player &player)
{
	memset(&packed, 0, sizeof(packed));
	packed.destAction = player.destAction;
	packed.destParam1 = player.destParam1;
	packed.destParam2 = player.destParam2;
	packed.plrlevel = player.plrlevel;
	packed.px = player.position.tile.x;
	packed.py = player.position.tile.y;
	if (gbVanilla) {
		packed.targx = player.position.tile.x;
		packed.targy = player.position.tile.y;
	}
	CopyUtf8(packed.pName, player._pName, sizeof(packed.pName));
	packed.pClass = static_cast<uint8_t>(player._pClass);
	packed.pBaseStr = player.attributes.strength.base;
	packed.pBaseMag = player.attributes.magic.base;
	packed.pBaseDex = player.attributes.dexterity.base;
	packed.pBaseVit = player.attributes.vitality.base;
	packed.pLevel = player.getCharacterLevel();
	packed.pStatPts = player._pStatPts;
	packed.pExperience = Swap32LE(player._pExperience);
	packed.pGold = Swap32LE(player._pGold);
	packed.pHPBase = Swap32LE(player.life.base);
	packed.pMaxHPBase = Swap32LE(player.life.maximumBase);
	packed.pManaBase = Swap32LE(player.mana.base);
	packed.pMaxManaBase = Swap32LE(player.mana.maximumBase);
	packed.pMemSpells = Swap64LE(player._pMemSpells);

	for (int i = 0; i < 37; i++) // Should be MAX_SPELLS but set to 37 to make save games compatible
		packed.pSplLvl[i] = player._pSplLvl[i];
	for (int i = 37; i < 47; i++)
		packed.pSplLvl2[i - 37] = player._pSplLvl[i];

	for (int i = 0; i < NUM_INVLOC; i++)
		PackItem(packed.InvBody[i], player.InvBody[i], gbIsHellfire);

	packed._pNumInv = player._pNumInv;
	for (int i = 0; i < packed._pNumInv; i++)
		PackItem(packed.InvList[i], player.InvList[i], gbIsHellfire);

	for (int i = 0; i < InventoryGridCells; i++)
		packed.InvGrid[i] = player.InvGrid[i];

	for (int i = 0; i < MaxBeltItems; i++)
		PackItem(packed.SpdList[i], player.SpdList[i], gbIsHellfire);

	packed.wReflections = Swap16LE(player.wReflections);
	packed.pDamAcFlags = Swap32LE(static_cast<uint32_t>(player.pDamAcFlags));
	packed.pDiabloKillLevel = Swap32LE(player.pDiabloKillLevel);
	packed.bIsHellfire = gbIsHellfire ? 1 : 0;
}

void PackNetItem(const Item &item, ItemNetPack &packedItem)
{
	if (item.isEmpty()) {
		packedItem.def.wIndx = static_cast<_item_indexes>(0xFFFF);
		return;
	}
	packedItem.def.wIndx = static_cast<_item_indexes>(Swap16LE(item.IDidx));
	packedItem.def.wCI = Swap16LE(item._iCreateInfo);
	packedItem.def.dwSeed = Swap32LE(item._iSeed);
	if (item.IDidx != IDI_EAR)
		PrepareItemForNetwork(item, packedItem.item);
	else
		PrepareEarForNetwork(item, packedItem.ear);
}

void PackNetPlayer(PlayerNetPack &packed, const Player &player)
{
	packed.plrlevel = player.plrlevel;
	packed.px = player.position.tile.x;
	packed.py = player.position.tile.y;
	packed.pdir = static_cast<uint8_t>(player.direction);
	CopyUtf8(packed.pName, player._pName, sizeof(packed.pName));
	packed.pClass = static_cast<uint8_t>(player._pClass);
	packed.pBaseStr = player.attributes.strength.base;
	packed.pBaseMag = player.attributes.magic.base;
	packed.pBaseDex = player.attributes.dexterity.base;
	packed.pBaseVit = player.attributes.vitality.base;
	packed.pLevel = player.getCharacterLevel();
	packed.pStatPts = player._pStatPts;
	packed.pExperience = Swap32LE(player._pExperience);
	packed.pHPBase = Swap32LE(player.life.base);
	packed.pMaxHPBase = Swap32LE(player.life.maximumBase);
	packed.pManaBase = Swap32LE(player.mana.base);
	packed.pMaxManaBase = Swap32LE(player.mana.maximumBase);
	packed.pMemSpells = Swap64LE(player._pMemSpells);

	for (int i = 0; i < MAX_SPELLS; i++)
		packed.pSplLvl[i] = player._pSplLvl[i];

	for (int i = 0; i < NUM_INVLOC; i++)
		PackNetItem(player.InvBody[i], packed.InvBody[i]);

	packed._pNumInv = player._pNumInv;
	for (int i = 0; i < packed._pNumInv; i++)
		PackNetItem(player.InvList[i], packed.InvList[i]);

	for (int i = 0; i < InventoryGridCells; i++)
		packed.InvGrid[i] = player.InvGrid[i];

	for (int i = 0; i < MaxBeltItems; i++)
		PackNetItem(player.SpdList[i], packed.SpdList[i]);

	packed.wReflections = Swap16LE(player.wReflections);
	packed.pDiabloKillLevel = player.pDiabloKillLevel;
	packed.pManaShield = player.pManaShield;
	packed.friendlyMode = player.friendlyMode ? 1 : 0;
	packed.isOnSetLevel = player.plrIsOnSetLevel;

	packed.currentStrength = Swap32LE(player.attributes.strength.current);
	packed.currentMagic = Swap32LE(player.attributes.magic.current);
	packed.currentDexterity = Swap32LE(player.attributes.dexterity.current);
	packed.currentVitality = Swap32LE(player.attributes.vitality.current);
	packed.currentHitPoints = Swap32LE(player.life.current);
	packed.maximumHitPoints = Swap32LE(player.life.maximum);
	packed.currentMana = Swap32LE(player.mana.current);
	packed.maximumMana = Swap32LE(player.mana.maximum);
	packed.pDamageMod = Swap32LE(player._pDamageMod);
	// we pack base to block as a basic check that remote players are using the same playerdat values as we are
	packed.pBaseToBlk = Swap32LE(player.getBaseToBlock());
	packed.physicalDamageMinimum = Swap32LE(player.damageBonuses.physical.minimum);
	packed.physicalDamageMaximum = Swap32LE(player.damageBonuses.physical.maximum);
	packed.pIAC = Swap32LE(player._pIAC);
	packed.damagePercentBonus = Swap32LE(player.damageBonuses.percent);
	packed.pIBonusToHit = Swap32LE(player._pIBonusToHit);
	packed.pIBonusAC = Swap32LE(player._pIBonusAC);
	packed.damageFlatBonus = Swap32LE(player.damageBonuses.flat);
	packed.pIGetHit = Swap32LE(player._pIGetHit);
	packed.armorPiercing = Swap32LE(player.damageBonuses.armorPiercing);
	packed.fireDamageMinimum = Swap32LE(player.damageBonuses.fire.minimum);
	packed.fireDamageMaximum = Swap32LE(player.damageBonuses.fire.maximum);
	packed.lightningDamageMinimum = Swap32LE(player.damageBonuses.lightning.minimum);
	packed.lightningDamageMaximum = Swap32LE(player.damageBonuses.lightning.maximum);
}

void UnPackItem(const ItemPack &packedItem, const Player &player, Item &item, bool isHellfire)
{
	if (packedItem.idx == 0xFFFF) {
		item.clear();
		return;
	}

	auto idx = static_cast<_item_indexes>(Swap16LE(packedItem.idx));

	if (gbIsSpawn) {
		idx = RemapItemIdxFromSpawn(idx);
	}
	if (!isHellfire) {
		idx = RemapItemIdxFromDiablo(idx);
	}

	if (!IsItemAvailable(idx)) {
		item.clear();
		return;
	}

	if (idx == IDI_EAR) {
		const uint16_t ic = Swap16LE(packedItem.iCreateInfo);
		const uint32_t iseed = Swap32LE(packedItem.iSeed);
		const uint16_t ivalue = Swap16LE(packedItem.wValue);
		const int32_t ibuff = Swap32LE(packedItem.dwBuff);

		char heroName[17];
		heroName[0] = static_cast<char>((ic >> 8) & 0x7F);
		heroName[1] = static_cast<char>(ic & 0x7F);
		heroName[2] = static_cast<char>((iseed >> 24) & 0x7F);
		heroName[3] = static_cast<char>((iseed >> 16) & 0x7F);
		heroName[4] = static_cast<char>((iseed >> 8) & 0x7F);
		heroName[5] = static_cast<char>(iseed & 0x7F);
		heroName[6] = static_cast<char>(packedItem.bId & 0x7F);
		heroName[7] = static_cast<char>(packedItem.bDur & 0x7F);
		heroName[8] = static_cast<char>(packedItem.bMDur & 0x7F);
		heroName[9] = static_cast<char>(packedItem.bCh & 0x7F);
		heroName[10] = static_cast<char>(packedItem.bMCh & 0x7F);
		heroName[11] = static_cast<char>((ivalue >> 8) & 0x7F);
		heroName[12] = static_cast<char>((ibuff >> 24) & 0x7F);
		heroName[13] = static_cast<char>((ibuff >> 16) & 0x7F);
		heroName[14] = static_cast<char>((ibuff >> 8) & 0x7F);
		heroName[15] = static_cast<char>(ibuff & 0x7F);
		heroName[16] = '\0';

		RecreateEar(item, ic, iseed, ivalue & 0xFF, heroName);
	} else {
		item = {};
		// Item generation logic will assign CF_HELLFIRE based on isHellfire
		// so if we carry it over from packedItem, it may be incorrect
		const uint32_t dwBuff = Swap32LE(packedItem.dwBuff) | (isHellfire ? CF_HELLFIRE : 0);
		RecreateItem(player, item, idx, Swap16LE(packedItem.iCreateInfo), Swap32LE(packedItem.iSeed), Swap16LE(packedItem.wValue), dwBuff);
		item._iIdentified = (packedItem.bId & 1) != 0;
		item._iMaxDur = packedItem.bMDur;
		item._iDurability = ClampDurability(item, packedItem.bDur);
		item._iMaxCharges = std::clamp<int>(packedItem.bMCh, 0, item._iMaxCharges);
		item._iCharges = std::clamp<int>(packedItem.bCh, 0, item._iMaxCharges);
	}
}

void UnPackPlayer(const PlayerPack &packed, Player &player)
{
	const Point position { packed.px, packed.py };

	player = {};
	player.setCharacterLevel(packed.pLevel);
	player.life.maximumBase = Swap32LE(packed.pMaxHPBase);
	player.life.base = Swap32LE(packed.pHPBase);
	player.life.base = std::clamp<int32_t>(player.life.base, 0, player.life.maximumBase);
	player.life.maximum = player.life.maximumBase;
	player.life.current = player.life.base;
	player.position.tile = position;
	player.position.future = position;
	player.setLevel(std::clamp<int8_t>(packed.plrlevel, 0, NUMLEVELS));

	player._pClass = static_cast<HeroClass>(std::clamp<uint8_t>(packed.pClass, 0, static_cast<uint8_t>(GetNumPlayerClasses() - 1)));

	player.clearPath();
	player.destAction = ACTION_NONE;

	CopyUtf8(player._pName, packed.pName, sizeof(player._pName));

	InitPlayer(player, true);

	player.attributes.strength.base = std::min<uint8_t>(packed.pBaseStr, player.GetMaximumAttributeValue(CharacterAttribute::Strength));
	player.attributes.strength.current = player.attributes.strength.base;
	player.attributes.magic.base = std::min<uint8_t>(packed.pBaseMag, player.GetMaximumAttributeValue(CharacterAttribute::Magic));
	player.attributes.magic.current = player.attributes.magic.base;
	player.attributes.dexterity.base = std::min<uint8_t>(packed.pBaseDex, player.GetMaximumAttributeValue(CharacterAttribute::Dexterity));
	player.attributes.dexterity.current = player.attributes.dexterity.base;
	player.attributes.vitality.base = std::min<uint8_t>(packed.pBaseVit, player.GetMaximumAttributeValue(CharacterAttribute::Vitality));
	player.attributes.vitality.current = player.attributes.vitality.base;
	player._pStatPts = packed.pStatPts;

	player._pExperience = Swap32LE(packed.pExperience);
	player._pGold = Swap32LE(packed.pGold);
	if ((int)(player.life.base & 0xFFFFFFC0) < 64)
		player.life.base = 64;

	player.mana.maximumBase = Swap32LE(packed.pMaxManaBase);
	player.mana.base = Swap32LE(packed.pManaBase);
	player.mana.base = std::min<int32_t>(player.mana.base, player.mana.maximumBase);
	player._pMemSpells = Swap64LE(packed.pMemSpells);

	// Only read spell levels for learnable spells (Diablo)
	for (int i = 0; i < 37; i++) { // Should be MAX_SPELLS but set to 36 to make save games compatible
		auto spl = static_cast<SpellID>(i);
		if (GetSpellBookLevel(spl) != -1)
			player._pSplLvl[i] = packed.pSplLvl[i];
		else
			player._pSplLvl[i] = 0;
	}
	// Only read spell levels for learnable spells (Hellfire)
	for (int i = 37; i < 47; i++) {
		auto spl = static_cast<SpellID>(i);
		if (GetSpellBookLevel(spl) != -1)
			player._pSplLvl[i] = packed.pSplLvl2[i - 37];
		else
			player._pSplLvl[i] = 0;
	}
	// These spells are unavailable in Diablo as learnable spells
	if (!gbIsHellfire) {
		player._pSplLvl[static_cast<uint8_t>(SpellID::Apocalypse)] = 0;
		player._pSplLvl[static_cast<uint8_t>(SpellID::Nova)] = 0;
	}

	const bool isHellfire = packed.bIsHellfire != 0;

	for (int i = 0; i < NUM_INVLOC; i++)
		UnPackItem(packed.InvBody[i], player, player.InvBody[i], isHellfire);

	player._pNumInv = packed._pNumInv;
	for (int i = 0; i < player._pNumInv; i++)
		UnPackItem(packed.InvList[i], player, player.InvList[i], isHellfire);

	for (int i = 0; i < InventoryGridCells; i++)
		player.InvGrid[i] = packed.InvGrid[i];

	VerifyGoldSeeds(player);

	for (int i = 0; i < MaxBeltItems; i++)
		UnPackItem(packed.SpdList[i], player, player.SpdList[i], isHellfire);

	CalcPlrInv(player, false);
	player.wReflections = Swap16LE(packed.wReflections);
	player.pDiabloKillLevel = Swap32LE(packed.pDiabloKillLevel);
}

bool UnPackNetItem(const Player &player, const ItemNetPack &packedItem, Item &item)
{
	item = {};
	const auto idx = static_cast<_item_indexes>(Swap16LE(packedItem.def.wIndx));
	if (idx < 0 || idx >= static_cast<_item_indexes>(AllItemsList.size()))
		return true;
	if (idx == IDI_EAR) {
		RecreateEar(item, Swap16LE(packedItem.ear.wCI), Swap32LE(packedItem.ear.dwSeed), packedItem.ear.bCursval, packedItem.ear.heroname);
		return true;
	}

	const uint16_t creationFlags = Swap16LE(packedItem.item.wCI);
	const uint32_t dwBuff = Swap16LE(packedItem.item.dwBuff);
	if (idx != IDI_GOLD)
		ValidateField(creationFlags, IsCreationFlagComboValid(creationFlags));
	if ((creationFlags & CF_TOWN) != 0)
		ValidateField(creationFlags, IsTownItemValid(creationFlags, player));
	else if ((creationFlags & CF_USEFUL) == CF_UPER15)
		ValidateFields(creationFlags, dwBuff, IsUniqueMonsterItemValid(creationFlags, dwBuff));
	else if ((dwBuff & CF_HELLFIRE) != 0 && AllItemsList[idx].iMiscId == IMISC_BOOK)
		return RecreateHellfireSpellBook(player, packedItem.item, &item);
	else
		ValidateFields(creationFlags, dwBuff, IsDungeonItemValid(creationFlags, dwBuff));

	RecreateItem(player, packedItem.item, item);
	return true;
}

bool UnPackNetPlayer(const PlayerNetPack &packed, Player &player)
{
	CopyUtf8(player._pName, packed.pName, sizeof(player._pName));

	ValidateField(packed.pClass, packed.pClass < GetNumPlayerClasses());
	player._pClass = static_cast<HeroClass>(packed.pClass);

	const Point position { packed.px, packed.py };
	ValidateFields(position.x, position.y, InDungeonBounds(position));
	ValidateField(packed.plrlevel, packed.plrlevel < NUMLEVELS);
	ValidateField(packed.pLevel, packed.pLevel >= 1 && packed.pLevel <= player.getMaxCharacterLevel());

	const int32_t baseHpMax = Swap32LE(packed.pMaxHPBase);
	const int32_t baseHp = Swap32LE(packed.pHPBase);
	const int32_t hpMax = Swap32LE(packed.maximumHitPoints);
	ValidateFields(baseHp, baseHpMax, baseHp >= (baseHpMax - hpMax) && baseHp <= baseHpMax);

	const int32_t baseManaMax = Swap32LE(packed.pMaxManaBase);
	const int32_t baseMana = Swap32LE(packed.pManaBase);
	ValidateFields(baseMana, baseManaMax, baseMana <= baseManaMax);

	ValidateFields(packed.pClass, packed.pBaseStr, packed.pBaseStr <= player.GetMaximumAttributeValue(CharacterAttribute::Strength));
	ValidateFields(packed.pClass, packed.pBaseMag, packed.pBaseMag <= player.GetMaximumAttributeValue(CharacterAttribute::Magic));
	ValidateFields(packed.pClass, packed.pBaseDex, packed.pBaseDex <= player.GetMaximumAttributeValue(CharacterAttribute::Dexterity));
	ValidateFields(packed.pClass, packed.pBaseVit, packed.pBaseVit <= player.GetMaximumAttributeValue(CharacterAttribute::Vitality));

	ValidateField(packed._pNumInv, packed._pNumInv <= InventoryGridCells);

	ValidateField(packed.pdir, packed.pdir <= static_cast<uint8_t>(Direction::SouthEast));
	player.setCharacterLevel(packed.pLevel);
	player.position.tile = position;
	player.position.future = position;
	player.direction = static_cast<Direction>(packed.pdir);
	player.plrlevel = packed.plrlevel;
	player.plrIsOnSetLevel = packed.isOnSetLevel != 0;
	player.life.maximumBase = baseHpMax;
	player.life.base = baseHp;
	player.life.maximum = baseHpMax;
	player.life.current = baseHp;

	player.clearPath();
	player.destAction = ACTION_NONE;

	InitPlayer(player, true);

	player.attributes.strength.base = packed.pBaseStr;
	player.attributes.strength.current = player.attributes.strength.base;
	player.attributes.magic.base = packed.pBaseMag;
	player.attributes.magic.current = player.attributes.magic.base;
	player.attributes.dexterity.base = packed.pBaseDex;
	player.attributes.dexterity.current = player.attributes.dexterity.base;
	player.attributes.vitality.base = packed.pBaseVit;
	player.attributes.vitality.current = player.attributes.vitality.base;
	player._pStatPts = packed.pStatPts;

	player._pExperience = Swap32LE(packed.pExperience);
	player.mana.maximumBase = baseManaMax;
	player.mana.base = baseMana;
	player._pMemSpells = Swap64LE(packed.pMemSpells);
	player.wReflections = Swap16LE(packed.wReflections);
	player.pDiabloKillLevel = packed.pDiabloKillLevel;
	player.pManaShield = packed.pManaShield != 0;
	player.friendlyMode = packed.friendlyMode != 0;

	for (int i = 0; i < MAX_SPELLS; i++)
		player._pSplLvl[i] = packed.pSplLvl[i];

	for (int i = 0; i < NUM_INVLOC; i++) {
		if (!UnPackNetItem(player, packed.InvBody[i], player.InvBody[i]))
			return false;
		if (player.InvBody[i].isEmpty())
			continue;
		auto loc = static_cast<int8_t>(player.GetItemLocation(player.InvBody[i]));
		switch (i) {
		case INVLOC_HEAD:
			ValidateField(loc, loc == ILOC_HELM);
			break;
		case INVLOC_RING_LEFT:
		case INVLOC_RING_RIGHT:
			ValidateField(loc, loc == ILOC_RING);
			break;
		case INVLOC_AMULET:
			ValidateField(loc, loc == ILOC_AMULET);
			break;
		case INVLOC_HAND_LEFT:
		case INVLOC_HAND_RIGHT:
			ValidateField(loc, IsAnyOf(loc, ILOC_ONEHAND, ILOC_TWOHAND));
			break;
		case INVLOC_CHEST:
			ValidateField(loc, loc == ILOC_ARMOR);
			break;
		}
	}

	player._pNumInv = packed._pNumInv;
	for (int i = 0; i < player._pNumInv; i++) {
		if (!UnPackNetItem(player, packed.InvList[i], player.InvList[i]))
			return false;
	}

	for (int i = 0; i < InventoryGridCells; i++)
		player.InvGrid[i] = packed.InvGrid[i];

	for (int i = 0; i < MaxBeltItems; i++) {
		Item &item = player.SpdList[i];
		if (!UnPackNetItem(player, packed.SpdList[i], item))
			return false;
		if (item.isEmpty())
			continue;
		const Size beltItemSize = GetInventorySize(item);
		const auto beltItemType = static_cast<int8_t>(item._itype);
		const bool beltItemUsable = item.isUsable();
		ValidateFields(beltItemSize.width, beltItemSize.height, (beltItemSize == Size { 1, 1 }));
		ValidateField(beltItemType, item._itype != ItemType::Gold);
		ValidateField(beltItemUsable, beltItemUsable);
	}

	CalcPlrInv(player, false);
	player._pGold = CalculateGold(player);

	ValidateFields(player.attributes.strength.current, SwapSigned32LE(packed.currentStrength),
	    player.attributes.strength.current == SwapSigned32LE(packed.currentStrength));
	ValidateFields(player.attributes.magic.current, SwapSigned32LE(packed.currentMagic), player.attributes.magic.current == SwapSigned32LE(packed.currentMagic));
	ValidateFields(player.attributes.dexterity.current, SwapSigned32LE(packed.currentDexterity),
	    player.attributes.dexterity.current == SwapSigned32LE(packed.currentDexterity));
	ValidateFields(player.attributes.vitality.current, SwapSigned32LE(packed.currentVitality),
	    player.attributes.vitality.current == SwapSigned32LE(packed.currentVitality));
	ValidateFields(player.life.current, SwapSigned32LE(packed.currentHitPoints), player.life.current == SwapSigned32LE(packed.currentHitPoints));
	ValidateFields(player.life.maximum, SwapSigned32LE(packed.maximumHitPoints), player.life.maximum == SwapSigned32LE(packed.maximumHitPoints));
	ValidateFields(player.mana.current, SwapSigned32LE(packed.currentMana), player.mana.current == SwapSigned32LE(packed.currentMana));
	ValidateFields(player.mana.maximum, SwapSigned32LE(packed.maximumMana), player.mana.maximum == SwapSigned32LE(packed.maximumMana));
	ValidateFields(player._pDamageMod, SwapSigned32LE(packed.pDamageMod), player._pDamageMod == SwapSigned32LE(packed.pDamageMod));
	ValidateFields(player.getBaseToBlock(), SwapSigned32LE(packed.pBaseToBlk), player.getBaseToBlock() == SwapSigned32LE(packed.pBaseToBlk));
	ValidateFields(player.damageBonuses.physical.minimum, SwapSigned32LE(packed.physicalDamageMinimum),
	    player.damageBonuses.physical.minimum == SwapSigned32LE(packed.physicalDamageMinimum));
	ValidateFields(player.damageBonuses.physical.maximum, SwapSigned32LE(packed.physicalDamageMaximum),
	    player.damageBonuses.physical.maximum == SwapSigned32LE(packed.physicalDamageMaximum));
	ValidateFields(player._pIAC, SwapSigned32LE(packed.pIAC), player._pIAC == SwapSigned32LE(packed.pIAC));
	ValidateFields(player.damageBonuses.percent, SwapSigned32LE(packed.damagePercentBonus),
	    player.damageBonuses.percent == SwapSigned32LE(packed.damagePercentBonus));
	ValidateFields(player._pIBonusToHit, SwapSigned32LE(packed.pIBonusToHit), player._pIBonusToHit == SwapSigned32LE(packed.pIBonusToHit));
	ValidateFields(player._pIBonusAC, SwapSigned32LE(packed.pIBonusAC), player._pIBonusAC == SwapSigned32LE(packed.pIBonusAC));
	ValidateFields(player.damageBonuses.flat, SwapSigned32LE(packed.damageFlatBonus), player.damageBonuses.flat == SwapSigned32LE(packed.damageFlatBonus));
	ValidateFields(player._pIGetHit, SwapSigned32LE(packed.pIGetHit), player._pIGetHit == SwapSigned32LE(packed.pIGetHit));
	ValidateFields(player.damageBonuses.armorPiercing, SwapSigned32LE(packed.armorPiercing),
	    player.damageBonuses.armorPiercing == SwapSigned32LE(packed.armorPiercing));
	ValidateFields(player.damageBonuses.fire.minimum, SwapSigned32LE(packed.fireDamageMinimum),
	    player.damageBonuses.fire.minimum == SwapSigned32LE(packed.fireDamageMinimum));
	ValidateFields(player.damageBonuses.fire.maximum, SwapSigned32LE(packed.fireDamageMaximum),
	    player.damageBonuses.fire.maximum == SwapSigned32LE(packed.fireDamageMaximum));
	ValidateFields(player.damageBonuses.lightning.minimum, SwapSigned32LE(packed.lightningDamageMinimum),
	    player.damageBonuses.lightning.minimum == SwapSigned32LE(packed.lightningDamageMinimum));
	ValidateFields(player.damageBonuses.lightning.maximum, SwapSigned32LE(packed.lightningDamageMaximum),
	    player.damageBonuses.lightning.maximum == SwapSigned32LE(packed.lightningDamageMaximum));
	ValidateFields(player.life.maximumBase, player.calculateBaseLife(), player.life.maximumBase <= player.calculateBaseLife());
	ValidateFields(player.mana.maximumBase, player.calculateBaseMana(), player.mana.maximumBase <= player.calculateBaseMana());

	return true;
}

} // namespace devilution
