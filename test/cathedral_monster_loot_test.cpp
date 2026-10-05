#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <memory>
#include <sstream>
#include <string>
#include <string_view>
#include <type_traits>
#include <utility>
#include <vector>

#include "application/game_mode.hpp"
#include "application/headless_mode.hpp"
#include "application/diablo.h"
#include "engine/audio/sound.h"
#include "engine/assets.hpp"
#include "engine/load/load_clx.hpp"
#include "engine/random.hpp"
#include "engine/render/text_render.hpp"
#include "game/items/items.hpp"
#include "game/items/item_pool.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/tile_properties.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/monsters/monsters.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "game/players/players.hpp"
#include "game/quests/quests.hpp"
#include "persistence/options.h"
#include "tables/itemdat.h"
#include "tables/monstdat.h"
#include "tables/playerdat.hpp"
#include "tables/spelldat.h"
#include "utils/paths.h"

namespace devilution {
// This BUILD_TESTING-only forwarder reaches SpawnLoot without running
// MonsterDeath's animation, lighting, sound, networking, or XP side effects.
DVL_API_FOR_TEST void TestMonsterSpawnLoot(Monster &monster, bool sendmsg);

namespace {

constexpr char OracleDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_MONSTER_LOOT_ORACLE_DIRECTORY";
// Source row 18 is Rotfeast the Hungry, a non-quest unique used to exercise
// Monster::level's unique metadata path without a quest reward branch.
constexpr size_t RotfeastUniqueId = 18;

uint32_t NextLegacyRandomState(uint32_t state)
{
	return static_cast<uint32_t>(static_cast<uint64_t>(state) * UINT64_C(0x015A4E35) + 1);
}

int LegacyGenerateRnd100(uint32_t state)
{
	const uint32_t next = NextLegacyRandomState(state);
	const int64_t signedValue = next >= UINT32_C(0x80000000) ? static_cast<int64_t>(next) - INT64_C(0x100000000) : static_cast<int64_t>(next);
	if (signedValue == -INT64_C(0x80000000))
		return (-32768) % 100;
	const uint64_t absoluteValue = static_cast<uint64_t>(signedValue < 0 ? -signedValue : signedValue);
	return static_cast<int>((absoluteValue >> 16) % 100);
}

void JsonString(std::ostream &out, std::string_view value)
{
	constexpr char Hex[] = "0123456789abcdef";
	out << '"';
	for (unsigned char ch : value) {
		if (ch == '"' || ch == '\\') out << '\\' << ch;
		else if (ch < 0x20) out << "\\u00" << Hex[ch >> 4] << Hex[ch & 15];
		else out << ch;
	}
	out << '"';
}

class JsonObject {
public:
	explicit JsonObject(std::ostream &out) : out(out) { out << '{'; }
	~JsonObject() { out << '}'; }
	void Key(std::string_view name)
	{
		if (!first) out << ',';
		first = false;
		JsonString(out, name);
		out << ':';
	}
	template <typename T> void Number(std::string_view name, T value)
	{
		Key(name);
		if constexpr (std::is_enum_v<T>) out << static_cast<int64_t>(value);
		else if constexpr (std::is_unsigned_v<T>) out << static_cast<uint64_t>(value);
		else out << static_cast<int64_t>(value);
	}
	void Boolean(std::string_view name, bool value) { Key(name); out << (value ? "true" : "false"); }
	void String(std::string_view name, std::string_view value) { Key(name); JsonString(out, value); }

private:
	std::ostream &out;
	bool first = true;
};

template <typename T, typename F> void WriteArray(std::ostream &out, const T &values, F write)
{
	out << '[';
	bool first = true;
	for (const auto &value : values) {
		if (!first) out << ',';
		first = false;
		write(value);
	}
	out << ']';
}

void WriteFlags(std::ostream &out)
{
	out << '[';
	for (int i = 0; i < 128; ++i) {
		if (i != 0) out << ',';
		out << (UniqueItemFlags[i] ? "true" : "false");
	}
	out << ']';
}

void WriteItem(std::ostream &out, const Item &item)
{
	JsonObject json(out);
#define ITEM_NUMBER(key, field) json.Number(key, item.field)
#define ITEM_BOOL(key, field) json.Boolean(key, item.field)
	ITEM_NUMBER("seed", _iSeed); ITEM_NUMBER("createInfo", _iCreateInfo); ITEM_NUMBER("itemType", _itype);
	ITEM_BOOL("animFlag", _iAnimFlag); ITEM_NUMBER("positionX", position.x); ITEM_NUMBER("positionY", position.y);
	ITEM_BOOL("deleted", _iDelFlag); ITEM_NUMBER("selectionRegion", selectionRegion); ITEM_BOOL("postDraw", _iPostDraw);
	ITEM_BOOL("identified", _iIdentified); ITEM_NUMBER("magical", _iMagical);
	json.String("baseName", item._iName); json.String("identifiedName", item._iIName);
	ITEM_NUMBER("equipLocation", _iLoc); ITEM_NUMBER("itemClass", _iClass); ITEM_NUMBER("cursorGraphic", _iCurs);
	ITEM_NUMBER("value", _ivalue); ITEM_NUMBER("identifiedValue", _iIvalue); ITEM_NUMBER("minDamage", _iMinDam);
	ITEM_NUMBER("maxDamage", _iMaxDam); ITEM_NUMBER("armorClass", _iAC); ITEM_NUMBER("flags", _iFlags);
	ITEM_NUMBER("miscId", _iMiscId); ITEM_NUMBER("spellId", _iSpell); ITEM_NUMBER("itemIndex", IDidx);
	ITEM_NUMBER("charges", _iCharges); ITEM_NUMBER("maxCharges", _iMaxCharges); ITEM_NUMBER("durability", _iDurability);
	ITEM_NUMBER("maxDurability", _iMaxDur); ITEM_NUMBER("plusDamage", _iPLDam); ITEM_NUMBER("plusToHit", _iPLToHit);
	ITEM_NUMBER("plusArmorClass", _iPLAC); ITEM_NUMBER("plusStrength", _iPLStr); ITEM_NUMBER("plusMagic", _iPLMag);
	ITEM_NUMBER("plusDexterity", _iPLDex); ITEM_NUMBER("plusVitality", _iPLVit); ITEM_NUMBER("plusFireResistance", _iPLFR);
	ITEM_NUMBER("plusLightningResistance", _iPLLR); ITEM_NUMBER("plusMagicResistance", _iPLMR); ITEM_NUMBER("plusMana", _iPLMana);
	ITEM_NUMBER("plusHitPoints", _iPLHP); ITEM_NUMBER("plusDamageModifier", _iPLDamMod); ITEM_NUMBER("plusGetHit", _iPLGetHit);
	ITEM_NUMBER("plusLight", _iPLLight); ITEM_NUMBER("spellLevelAdd", _iSplLvlAdd); ITEM_NUMBER("uniqueId", _iUid);
	ITEM_NUMBER("fireMinDamage", _iFMinDam); ITEM_NUMBER("fireMaxDamage", _iFMaxDam); ITEM_NUMBER("lightningMinDamage", _iLMinDam);
	ITEM_NUMBER("lightningMaxDamage", _iLMaxDam); ITEM_NUMBER("plusEnemyArmorClass", _iPLEnAc); ITEM_NUMBER("prefixPower", _iPrePower);
	ITEM_NUMBER("suffixPower", _iSufPower); ITEM_NUMBER("valueAdd1", _iVAdd1); ITEM_NUMBER("valueMultiply1", _iVMult1);
	ITEM_NUMBER("valueAdd2", _iVAdd2); ITEM_NUMBER("valueMultiply2", _iVMult2); ITEM_NUMBER("minimumStrength", _iMinStr);
	ITEM_NUMBER("minimumMagic", _iMinMag); ITEM_NUMBER("minimumDexterity", _iMinDex); ITEM_BOOL("statFlag", _iStatFlag);
	ITEM_BOOL("request", _iRequest); ITEM_NUMBER("hellfireDamageArmorFlags", _iDamAcFlags); ITEM_NUMBER("buff", dwBuff);
	json.Key("animation");
	JsonObject animation(out);
	animation.Number("ticksPerFrame", item.animInfo.ticksPerFrame);
	animation.Number("tickCounterOfCurrentFrame", item.animInfo.tickCounterOfCurrentFrame);
	animation.Number("numberOfFrames", item.animInfo.numberOfFrames);
	animation.Number("currentFrame", item.animInfo.currentFrame);
	animation.Boolean("isPetrified", item.animInfo.isPetrified);
#undef ITEM_NUMBER
#undef ITEM_BOOL
}

void WritePower(std::ostream &out, const ItemPower &power)
{
	JsonObject json(out);
	json.Number("type", power.type); json.Number("param1", power.param1); json.Number("param2", power.param2);
}

void WriteCatalog(std::ostream &out, const OptionalOwnedClxSpriteList &font)
{
	JsonObject catalog(out);
	catalog.Key("items");
	int index = 0;
	WriteArray(out, AllItemsList, [&](const ItemData &row) {
		JsonObject item(out);
		item.Number("nativeItemId", index); item.Boolean("available", IsItemAvailable(index++));
#define BASE_NUMBER(key, field) item.Number(key, row.field)
		BASE_NUMBER("dropRate", dropRate); BASE_NUMBER("uniqueBaseItemId", iItemId); BASE_NUMBER("itemType", itype);
		BASE_NUMBER("cursorGraphic", iCurs); item.String("name", row.iName); item.String("shortName", row.iSName);
		BASE_NUMBER("equipLocation", iLoc); BASE_NUMBER("itemClass", iClass); BASE_NUMBER("minimumMonsterLevel", iMinMLvl);
		BASE_NUMBER("minDamage", iMinDam); BASE_NUMBER("maxDamage", iMaxDam); BASE_NUMBER("minArmorClass", iMinAC);
		BASE_NUMBER("maxArmorClass", iMaxAC); BASE_NUMBER("flags", iFlags); BASE_NUMBER("miscId", iMiscId);
		BASE_NUMBER("spellId", iSpell); BASE_NUMBER("value", iValue); BASE_NUMBER("durability", iDurability);
		BASE_NUMBER("minimumStrength", iMinStr); BASE_NUMBER("minimumMagic", iMinMag); BASE_NUMBER("minimumDexterity", iMinDex);
		item.Boolean("usable", row.iUsable);
#undef BASE_NUMBER
	});
	auto writeAffixes = [&](std::string_view name, const std::vector<PLStruct> &rows) {
		catalog.Key(name);
		WriteArray(out, rows, [&](const PLStruct &row) {
			JsonObject affix(out); affix.String("name", row.PLName); affix.Key("power"); WritePower(out, row.power);
			affix.Number("minimumLevel", row.PLMinLvl); affix.Number("itemTypes", row.PLIType); affix.Number("alignment", row.PLGOE);
			affix.Number("chance", row.PLChance); affix.Boolean("useful", row.PLOk); affix.Number("minValue", row.minVal);
			affix.Number("maxValue", row.maxVal); affix.Number("valueMultiplier", row.multVal);
		});
	};
	writeAffixes("prefixes", ItemPrefixes); writeAffixes("suffixes", ItemSuffixes);
	catalog.Key("uniques");
	index = 0;
	WriteArray(out, UniqueItems, [&](const UniqueItem &row) {
		JsonObject unique(out); unique.Number("nativeUniqueId", index++); unique.String("name", row.UIName);
		unique.Number("cursorGraphic", row.UICurs); unique.Number("uniqueBaseItemId", row.UIItemId);
		unique.Number("minimumLevel", row.UIMinLvl); unique.Number("numberOfPowers", row.UINumPL); unique.Number("value", row.UIValue);
		unique.Key("powers"); WriteArray(out, row.powers, [&](const ItemPower &power) { WritePower(out, power); });
	});
	catalog.Key("spells");
	index = 0;
	WriteArray(out, SpellsData, [&](const SpellData &row) {
		JsonObject spell(out); spell.Number("nativeSpellId", index++); spell.String("name", row.sNameText);
		spell.Number("bookLevel", row.sBookLvl); spell.Number("staffLevel", row.sStaffLvl); spell.Number("minimumMagic", row.minInt);
		spell.Number("bookCost", row.bookCost()); spell.Number("staffCost", row.staffCost()); spell.Number("magicType", row.type());
		spell.Number("staffMinCharges", row.sStaffMin); spell.Number("staffMaxCharges", row.sStaffMax);
	});
	catalog.Key("glyphWidths");
	out << '[';
	for (unsigned i = 0; i < 256; ++i) { if (i != 0) out << ','; out << (*font)[i].width(); }
	out << ']';
	catalog.Key("glyphRows");
	out << '[';
	bool firstRow = true;
	for (unsigned row = 0; row <= 0x10FF; ++row) {
		std::ostringstream path;
		path << "fonts\\12-" << std::hex << std::setfill('0') << std::setw(2) << row << ".clx";
		auto rowFont = LoadOptionalClx(path.str().c_str());
		if (!rowFont.has_value()) continue;
		ASSERT_EQ(rowFont->numSprites(), 256U);
		if (!firstRow) out << ',';
		firstRow = false;
		JsonObject glyphRow(out); glyphRow.Number("row", row); glyphRow.Key("widths");
		out << '[';
		for (unsigned i = 0; i < 256; ++i) { if (i != 0) out << ','; out << (*rowFont)[i].width(); }
		out << ']';
	}
	out << ']';
	catalog.Key("animations");
	out << '[';
	bool first = true;
	for (int cursor = 0; cursor <= UINT8_MAX; ++cursor) {
		const auto frames = GetItemAnimationLengthForTesting(cursor);
		if (!frames.has_value()) continue;
		if (!first) out << ','; first = false;
		JsonObject animation(out); animation.Number("cursorGraphic", cursor); animation.Number("frameCount", *frames); animation.Number("ticksPerFrame", 1);
	}
	out << ']';
	catalog.Key("cursors");
	{
		JsonObject cursors(out); cursors.Number("bookRed", ICURS_BOOK_RED); cursors.Number("bookBlue", ICURS_BOOK_BLUE);
		cursors.Number("bookGrey", ICURS_BOOK_GREY); cursors.Number("goldSmall", ICURS_GOLD_SMALL); cursors.Number("goldMedium", ICURS_GOLD_MEDIUM);
		cursors.Number("goldLarge", ICURS_GOLD_LARGE);
	}
	catalog.Key("generationRules");
	{
	JsonObject rules(out);
#define RULE(field) rules.Number(#field, CurrentItemGenerationConfig.field)
	RULE(magicChanceBase); RULE(magicChancePerLevel); RULE(uniqueChanceNormal); RULE(uniqueChanceUnique); RULE(bonusLevelsUnique);
	RULE(prefixPercent); RULE(suffixPercent); RULE(onlygoodChance); RULE(noDropPercent); RULE(goldPercent);
#undef RULE
	rules.Key("alwaysMagicMisc"); WriteArray(out, CurrentItemGenerationConfig.alwaysMagicMisc, [&](auto value) { out << static_cast<int>(value); });
	}
}

void WriteMonsterRuntimeCatalog(std::ostream &out)
{
	out << '[';
	for (size_t index = 0; index < MonstersData.size(); ++index) {
		if (index != 0) out << ',';
		const auto &data = MonstersData[index];
		JsonObject row(out);
		row.Number("nativeMonsterId", index);
		std::ostringstream symbol;
		symbol << "NativeMonster_" << std::setfill('0') << std::setw(3) << index;
		row.String("stableSymbol", symbol.str());
		row.String("name", data.name);
		row.Number("level", data.level);
		row.Number("toHit", data.toHit);
		row.Number("animFrameNum", data.animFrameNum);
		row.Number("toHitSpecial", data.toHitSpecial);
		row.Number("animFrameNumSpecial", data.animFrameNumSpecial);
		row.Number("monsterClass", data.monsterClass);
		row.Number("selectionRegion", data.selectionRegion);
		row.Number("treasure", data.treasure);
		row.Number("experience", data.exp);
		row.Number("resistanceHell", data.resistanceHell);
	}
	out << ']';
}

bool IsQuestOnlyUnique(size_t index)
{
	return index == 0 || index == 2 || index == 3 || index == 7 || index == 8;
}

void WriteUniqueMonsterCatalog(std::ostream &out)
{
	out << '[';
	for (size_t index = 0; index < UniqueMonstersData.size(); ++index) {
		if (index != 0) out << ',';
		const auto &data = UniqueMonstersData[index];
		JsonObject row(out);
		row.Number("nativeUniqueId", index);
		row.Number("monsterType", data.mtype);
		row.Number("dungeonLevel", data.mlevel);
		row.Number("maximumHitPoints", data.mmaxhp);
		row.Number("ai", data.mAi);
		row.Number("intelligence", data.mint);
		row.Number("minDamage", data.mMinDamage);
		row.Number("maxDamage", data.mMaxDamage);
		row.Number("resistance", data.mMagicRes);
		row.Number("reducePlayerStrength", data.reducePlayerStrength);
		row.Number("reducePlayerMagic", data.reducePlayerMagic);
		row.Number("reducePlayerDexterity", data.reducePlayerDexterity);
		row.Number("reducePlayerVitality", data.reducePlayerVitality);
		row.Number("reducePlayerMaxHP", data.reducePlayerMaxHP);
		row.Number("reducePlayerMaxMana", data.reducePlayerMaxMana);
		row.Number("monsterPack", data.monsterPack);
		row.Number("customToHit", data.customToHit);
		row.Number("customArmorClass", data.customArmorClass);
		row.Number("talkMessage", data.mtalkmsg);
		row.Boolean("questOnly", IsQuestOnlyUnique(index));
	}
	out << ']';
}

struct WorldSnapshot {
	uint32_t randomState;
	int activeCount;
};

WorldSnapshot CaptureWorld(std::ostream &out, bool includeInputs)
{
	WorldSnapshot result { GetLCGEngineState(), ActiveItemCount };
	JsonObject json(out);
	json.Number("randomState", result.randomState); json.Number("activeCount", result.activeCount);
	json.Key("activeIds");
	out << '[';
	for (int i = 0; i < MAXITEMS; ++i) { if (i != 0) out << ','; out << static_cast<int>(ActiveItems[i]); }
	out << ']';
	json.Key("items");
	out << '[';
	for (int i = 0; i <= MAXITEMS; ++i) { if (i != 0) out << ','; WriteItem(out, Items[i]); }
	out << ']';
	json.Key("uniqueFlags"); WriteFlags(out);
	json.Key("itemRefs");
	out << '[';
	int itemRefIndex = 0;
	for (const Tile &tile : tiles()) { if (itemRefIndex++ != 0) out << ','; out << static_cast<int>(tile.item()); }
	out << ']';
	if (!includeInputs) return result;
	json.Key("tiles");
	out << '[';
	int index = 0;
	for (const Tile &tile : tiles()) {
		if (index++ != 0) out << ',';
		const Point position { (index - 1) % MAXDUNX, (index - 1) / MAXDUNX };
		out << '[' << static_cast<int>(tile.player()) << ',' << tile.monster() << ',' << static_cast<int>(tile.object()) << ','
		    << static_cast<int>(tile.item()) << ',' << (TileContainsSetPiece(position) ? "true" : "false") << ','
		    << (IsTileSolid(position) ? "true" : "false") << ']';
	}
	out << ']';
	json.Key("objects");
	out << '[';
	for (int i = 0; i < MAXOBJECTS; ++i) {
		if (i != 0) out << ',';
		out << '[' << (Objects[i]._oSolidFlag ? "true" : "false") << ',' << static_cast<int>(Objects[i].selectionRegion) << ']';
	}
	out << ']';
	return result;
}

void WritePlayer(std::ostream &out, const Player &player)
{
	JsonObject json(out);
	json.Number("class", player._pClass); json.Number("maximumLifeBase", player.life.maximumBase); json.Number("maximumManaBase", player.mana.maximumBase);
	json.Number("level", player.getCharacterLevel()); json.Number("pLvlLoad", player.pLvlLoad); json.Number("strength", player.attributes.strength.base);
	json.Number("magic", player.attributes.magic.base); json.Number("dexterity", player.attributes.dexterity.base); json.Number("vitality", player.attributes.vitality.base);
	json.Number("positionX", player.position.tile.x); json.Number("positionY", player.position.tile.y); json.Number("lightRadius", player._pLightRad);
}

void WriteMonster(std::ostream &out, const Monster &monster)
{
	JsonObject json(out);
	json.Number("type", monster.type().type); json.Number("levelType", monster.levelType); json.Number("dataLevel", monster.data().level);
	json.Number("treasure", monster.data().treasure); json.Number("flags", monster.flags); json.Number("uniqueType", monster.uniqueType);
	json.Number("rndItemSeed", monster.rndItemSeed); json.Number("positionX", monster.position.tile.x); json.Number("positionY", monster.position.tile.y);
	json.Number("futureX", monster.position.future.x); json.Number("futureY", monster.position.future.y); json.Number("oldX", monster.position.old.x); json.Number("oldY", monster.position.old.y);
	json.Number("mode", monster.mode); json.Number("hitPoints", monster.hitPoints); json.Number("maxHitPoints", monster.maxHitPoints); json.Number("whoHit", monster.whoHit);
}

enum class PlacementMode {
	Open,
	Fallback,
	Exhausted,
};

void PrepareWorld(int activeCount, bool shuffled, PlacementMode placement, bool signedRefs = false)
{
	for (Tile &tile : tiles()) tile = {};
	std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
	for (int i = 0; i < MAXOBJECTS; ++i) Objects[i] = {};
	for (int i = 0; i <= MAXITEMS; ++i) Items[i] = {};
	for (int i = 0; i < MAXITEMS; ++i)
		ActiveItems[i] = static_cast<uint8_t>(shuffled && i != MAXITEMS - 1 ? MAXITEMS - 2 - i : i);
	ActiveItemCount = static_cast<uint8_t>(activeCount);
	for (int i = 0; i < activeCount; ++i) {
		const int slot = ActiveItems[i];
		Items[slot]._iSeed = 1000 + i;
		Items[slot]._itype = ItemType::Misc;
		Items[slot].position = { 4 + i % 100, 4 + i / 100 };
		tileAt(Items[slot].position).setItem(static_cast<int8_t>(slot + 1));
	}
	if (signedRefs)
		tileAt({ 54, 54 }).setItem(-1);
	if (placement == PlacementMode::Exhausted) {
		for (Tile &tile : tiles()) tile.setPiece(1);
		tileProperties()[1] = TileProperties::Solid;
	} else if (placement == PlacementMode::Fallback) {
		for (int y = 55; y <= 57; ++y) {
			for (int x = 55; x <= 57; ++x)
				tileAt({ x, y }).setPiece(1);
		}
		tileProperties()[1] = TileProperties::Solid;
		tileAt({ 56, 56 }).setPlayer(1);
		tileAt({ 55, 55 }).setMonster(1);
		Objects[0]._oSolidFlag = true;
		Objects[0].position = { 55, 57 };
		tileAt(Objects[0].position).setObject(1);
	}
	ClearUniqueItemFlags();
}

void PrepareMonster(Monster &monster, size_t typeIndex, bool unique, uint32_t seed, uint16_t flags = 0)
{
	monster = {};
	monster.levelType = static_cast<uint8_t>(typeIndex);
	monster.uniqueType = unique ? static_cast<UniqueMonsterType>(RotfeastUniqueId) : UniqueMonsterType::None;
	monster.position.tile = { 56, 56 };
	monster.position.future = monster.position.tile;
	monster.position.old = monster.position.tile;
	monster.rndItemSeed = seed;
	monster.flags = flags;
	monster.mode = MonsterMode::Stand;
	monster.hitPoints = monster.maxHitPoints = 64;
}

class CathedralMonsterLootOracleTest : public testing::Test {
protected:
	void SetUp() override
	{
		oldAssets_ = paths::AssetsPath();
		oldHeadless_ = HeadlessMode; oldHellfire_ = gbIsHellfire; oldSpawn_ = gbIsSpawn; oldMultiplayer_ = gbIsMultiplayer;
		oldSound_ = gbSndInited;
		oldLevel_ = currentLevel().id(); oldDifficulty_ = sgGameInitInfo.nDifficulty; oldRandom_ = GetLCGEngineState(); oldPlayer_ = MyPlayer;
		oldPlayerId_ = MyPlayerId;
		oldPlayers_ = std::move(Players); oldItems_ = AllItemsList; oldUniques_ = UniqueItems; oldPrefixes_ = ItemPrefixes; oldSuffixes_ = ItemSuffixes;
		oldSpells_ = SpellsData; oldConfig_ = CurrentItemGenerationConfig; oldMonsterData_ = MonstersData; oldUniqueMonsterData_ = UniqueMonstersData;
		std::copy_n(Quests, MAXQUESTS, oldQuests_.begin()); oldBestiary_ = std::make_unique<std::array<CMonster, MaxLvlMTypes>>();
		for (size_t i = 0; i < MaxLvlMTypes; ++i) (*oldBestiary_)[i] = std::move(LevelBestiary.types[i]);
		oldTiles_ = std::make_unique<TileGrid>(tiles()); std::copy(std::begin(tileProperties()), std::end(tileProperties()), oldSol_.begin());
		std::copy_n(Items, MAXITEMS + 1, oldItemSlots_.begin()); std::copy_n(ActiveItems, MAXITEMS, oldActiveItems_.begin()); oldActiveCount_ = ActiveItemCount;
		std::copy_n(Objects, MAXOBJECTS, oldObjects_.begin());
		for (size_t i = 0; i < MaxMonsters; ++i) oldMonsters_[i] = std::move(Monsters[i]);
		std::copy_n(ActiveMonsters, MaxMonsters, oldActiveMonsters_.begin()); oldActiveMonsterCount_ = ActiveMonsterCount;
		std::copy_n(UniqueItemFlags, 128, oldUniqueFlags_.begin());
		const std::filesystem::path source = std::filesystem::path(GODEVILUTION_MONSTER_LOOT_SOURCE_ROOT);
		paths::SetAssetsPath((source / "assets").string() + "/");
		HeadlessMode = true; gbIsHellfire = false; gbIsSpawn = false; gbIsMultiplayer = false; gbSndInited = false; MyPlayerId = 0;
		currentLevel().setId(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL }); sgGameInitInfo.nDifficulty = DIFF_NORMAL;
		Players.resize(1); MyPlayer = &Players[0]; MyPlayer->life.maximumBase = 6400; MyPlayer->mana.maximumBase = 3200; MyPlayer->pLvlLoad = 1; MyPlayer->position.tile = { 75, 68 };
		ASSERT_TRUE(FindAsset("txtdata\\items\\itemdat.tsv").ok()); ASSERT_TRUE(FindAsset("txtdata\\monsters\\monstdat.tsv").ok());
		LoadItemData(); LoadSpellData(); LoadMonsterData();
		ASSERT_EQ(MonstersData.size(), 138U); ASSERT_EQ(UniqueMonstersData.size(), 100U);
		for (Quest &quest : Quests) quest._qactive = QUEST_NOTAVAIL;
		LevelBestiary.init();
		const auto type = LevelBestiary.addType(MT_NZOMBIE, PLACE_SCATTER); ASSERT_TRUE(type.has_value()); ordinaryType_ = *type;
		const auto highLevelType = LevelBestiary.addType(MT_GZOMBIE, PLACE_SCATTER); ASSERT_TRUE(highLevelType.has_value()); highLevelType_ = *highLevelType;
		const auto noDropType = LevelBestiary.addType(MT_FIEND, PLACE_SCATTER); ASSERT_TRUE(noDropType.has_value()); noDropType_ = *noDropType;
		font_ = LoadOptionalClx("fonts\\12-00.clx"); ASSERT_TRUE(font_.has_value()); ASSERT_EQ(font_->numSprites(), 256U);
		PrepareWorld(0, false, PlacementMode::Open); SetRndSeed(0);
		if (const char *directory = std::getenv(OracleDirectoryVariable); directory != nullptr && *directory != '\0') {
			historySeed_ = FindHistorySeed();
			ASSERT_NE(historySeed_, UINT32_MAX);
		}
		PrepareWorld(0, false, PlacementMode::Open); SetRndSeed(0);
	}

	void TearDown() override
	{
		LevelBestiary.init(); for (size_t i = 0; i < MaxLvlMTypes; ++i) LevelBestiary.types[i] = std::move((*oldBestiary_)[i]);
		Players = std::move(oldPlayers_); MyPlayer = oldPlayer_; AllItemsList = std::move(oldItems_); UniqueItems = std::move(oldUniques_);
		ItemPrefixes = std::move(oldPrefixes_); ItemSuffixes = std::move(oldSuffixes_); SpellsData = std::move(oldSpells_); CurrentItemGenerationConfig = std::move(oldConfig_);
		MonstersData = std::move(oldMonsterData_); UniqueMonstersData = std::move(oldUniqueMonsterData_);
		std::copy(oldQuests_.begin(), oldQuests_.end(), Quests); tiles() = *oldTiles_; std::copy(oldSol_.begin(), oldSol_.end(), std::begin(tileProperties()));
		std::copy(oldItemSlots_.begin(), oldItemSlots_.end(), Items); std::copy(oldActiveItems_.begin(), oldActiveItems_.end(), ActiveItems); ActiveItemCount = oldActiveCount_;
		std::copy(oldObjects_.begin(), oldObjects_.end(), Objects); for (size_t i = 0; i < MaxMonsters; ++i) Monsters[i] = std::move(oldMonsters_[i]); std::copy(oldActiveMonsters_.begin(), oldActiveMonsters_.end(), ActiveMonsters); ActiveMonsterCount = oldActiveMonsterCount_;
		std::copy(oldUniqueFlags_.begin(), oldUniqueFlags_.end(), UniqueItemFlags); SetRndSeed(oldRandom_); currentLevel().setId(oldLevel_);
		HeadlessMode = oldHeadless_; gbIsHellfire = oldHellfire_; gbIsSpawn = oldSpawn_; gbIsMultiplayer = oldMultiplayer_; gbSndInited = oldSound_; MyPlayerId = oldPlayerId_; sgGameInitInfo.nDifficulty = oldDifficulty_; paths::SetAssetsPath(oldAssets_);
		UnloadFonts();
	}

	void CaptureCase(std::ostream &stream, std::string_view name, int depth, uint32_t seed, size_t monsterType, bool unique, int activeCount, bool shuffled, PlacementMode placement, int historyCount, bool signedRefs = false, uint16_t flags = 0)
	{
		PrepareWorld(activeCount, shuffled, placement, signedRefs); currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATHEDRAL });
		for (int i = 0; i < historyCount; ++i) UniqueItemFlags[i] = true;
		const auto uniqueFlagsBefore = [&] {
			std::array<bool, 128> flags {};
			std::copy_n(UniqueItemFlags, flags.size(), flags.begin());
			return flags;
		}();
		Monster &monster = Monsters[0]; PrepareMonster(monster, monsterType, unique, seed, flags);
		tileAt(monster.position.tile).setMonster(1);
		SetRndSeed(seed ^ UINT32_C(0x9e3779b9));
		const uint32_t preDeathState = GetLCGEngineState();
		SetRndSeed(monster.rndItemSeed);
		if (cases_++ != 0) stream << ',';
		JsonObject json(stream); json.String("name", name); json.Number("depth", depth); json.Number("monsterSeed", seed); json.Number("preDeathRandomState", preDeathState);
		json.Number("monsterTypeIndex", monsterType); json.Boolean("unique", unique); json.Number("activeCount", activeCount); json.Boolean("shuffledActiveItems", shuffled);
		json.Number("placementMode", static_cast<int>(placement)); json.Number("uniqueHistoryCount", historyCount); json.Boolean("signedItemRefs", signedRefs);
		json.Key("player"); WritePlayer(stream, *MyPlayer); json.Key("monster"); WriteMonster(stream, monster);
		json.Key("input"); CaptureWorld(stream, true);
		const int allocationSlot = activeCount < MAXITEMS ? ActiveItems[activeCount] : -1;
		TestMonsterSpawnLoot(monster, false);
		const bool noDropFlag = monsterType == noDropType_;
		const bool noDropRoll = !unique && (seed == 1 || seed == UINT32_MAX);
		const bool playerMinion = (flags & MFLAG_GOLEM) != 0 && (flags & MFLAG_BERSERK) == 0;
		if (playerMinion) {
			ASSERT_EQ(ActiveItemCount, activeCount);
			ASSERT_EQ(GetLCGEngineState(), seed);
			++minionSkippedCases_;
		} else if (noDropFlag) {
			ASSERT_EQ(ActiveItemCount, activeCount);
			ASSERT_EQ(GetLCGEngineState(), seed);
			++noDropFlagCases_;
		} else if (noDropRoll) {
			ASSERT_EQ(ActiveItemCount, activeCount);
			ASSERT_EQ(GetLCGEngineState(), NextLegacyRandomState(seed));
			++noDropRollCases_;
		} else if (!unique && activeCount == MAXITEMS) {
			ASSERT_EQ(ActiveItemCount, activeCount);
			ASSERT_EQ(GetLCGEngineState(), NextLegacyRandomState(NextLegacyRandomState(NextLegacyRandomState(seed))));
			if (seed == 22) {
				ASSERT_LT(LegacyGenerateRnd100(seed), 41);
				ASSERT_GE(LegacyGenerateRnd100(NextLegacyRandomState(seed)), 74);
				ASSERT_EQ(LegacyGenerateRnd100(NextLegacyRandomState(NextLegacyRandomState(seed))), 95);
				++weightedCases_;
			}
			++poolFullCases_;
		} else {
			ASSERT_EQ(ActiveItemCount, activeCount + 1);
			ASSERT_EQ(ActiveItems[activeCount], allocationSlot);
			if (activeCount == MAXITEMS - 1 && shuffled) {
				ASSERT_EQ(allocationSlot, MAXITEMS - 1);
				++lastSlotCases_;
			}
			if (!unique && seed == 22) {
				ASSERT_LT(LegacyGenerateRnd100(seed), 41);
				ASSERT_GE(LegacyGenerateRnd100(NextLegacyRandomState(seed)), 74);
				ASSERT_NE(Items[allocationSlot].IDidx, IDI_GOLD);
				++weightedCases_;
			} else if (!unique && seed == 0) {
				ASSERT_EQ(Items[allocationSlot].IDidx, IDI_GOLD);
				++goldCases_;
			}
			if (unique && historyCount != 0) {
				bool historyChanged = false;
				for (size_t i = 0; i < uniqueFlagsBefore.size(); ++i)
					historyChanged |= uniqueFlagsBefore[i] != UniqueItemFlags[i];
				ASSERT_TRUE(historyChanged || Items[allocationSlot]._iMagical != ITEM_QUALITY_UNIQUE);
			}
			if (name == "ordinary-unique-history-fresh") {
				ASSERT_EQ(Items[allocationSlot]._iMagical, ITEM_QUALITY_UNIQUE);
				++historyFreshCases_;
			}
			if (unique && seed == historySeed_ && historyCount == 128) {
				ASSERT_NE(Items[allocationSlot]._iMagical, ITEM_QUALITY_UNIQUE);
				++historyRerollCases_;
			}
			if (placement == PlacementMode::Fallback) {
				ASSERT_FALSE(Items[allocationSlot].position.x == 0 && Items[allocationSlot].position.y == 0);
				ASSERT_TRUE(Items[allocationSlot].position.x < 55 || Items[allocationSlot].position.x > 57 || Items[allocationSlot].position.y < 55 || Items[allocationSlot].position.y > 57);
				ASSERT_EQ(tileAt(Items[allocationSlot].position).item(), static_cast<int8_t>(allocationSlot + 1));
				if (signedRefs)
					ASSERT_EQ(tileAt({ 54, 54 }).item(), static_cast<int8_t>(-1));
				++fallbackCases_;
			} else if (placement == PlacementMode::Exhausted) {
				ASSERT_EQ(Items[allocationSlot].position.x, 0);
				ASSERT_EQ(Items[allocationSlot].position.y, 0);
				bool referenced = false;
				for (const Tile &tile : tiles()) referenced |= tile.item() == static_cast<int8_t>(allocationSlot + 1);
				ASSERT_FALSE(referenced);
				++exhaustedCases_;
			}
			++droppedCases_;
		}
		json.Key("expected"); const WorldSnapshot expected = CaptureWorld(stream, false); json.Number("expectedRandomState", expected.randomState);
	}

	uint32_t FindHistorySeed()
	{
		for (uint32_t seed = 0; seed <= 4096; ++seed) {
			PrepareWorld(0, false, PlacementMode::Open);
			Monster &monster = Monsters[0]; PrepareMonster(monster, ordinaryType_, true, seed);
			tileAt(monster.position.tile).setMonster(1);
			SetRndSeed(seed);
			TestMonsterSpawnLoot(monster, false);
			if (ActiveItemCount == 1 && Items[ActiveItems[0]]._iMagical == ITEM_QUALITY_UNIQUE)
				return seed;
		}
		return UINT32_MAX;
	}

	std::string oldAssets_; bool oldHeadless_ = false, oldHellfire_ = false, oldSpawn_ = false, oldMultiplayer_ = false, oldSound_ = false; uint8_t oldPlayerId_ = 0;
	LevelId oldLevel_ {}; _difficulty oldDifficulty_ = DIFF_NORMAL; uint32_t oldRandom_ = 0; Player *oldPlayer_ = nullptr;
	std::vector<Player> oldPlayers_; std::vector<ItemData> oldItems_; std::vector<UniqueItem> oldUniques_; std::vector<PLStruct> oldPrefixes_, oldSuffixes_; std::vector<SpellData> oldSpells_; ItemGenerationConfig oldConfig_ {};
	std::vector<MonsterData> oldMonsterData_; std::vector<UniqueMonsterData> oldUniqueMonsterData_; std::array<Quest, MAXQUESTS> oldQuests_ {};
	std::unique_ptr<std::array<CMonster, MaxLvlMTypes>> oldBestiary_; std::unique_ptr<TileGrid> oldTiles_; std::array<TileProperties, MAXTILES> oldSol_ {};
	std::array<Item, MAXITEMS + 1> oldItemSlots_ {}; std::array<uint8_t, MAXITEMS> oldActiveItems_ {}; uint8_t oldActiveCount_ = 0; std::array<Object, MAXOBJECTS> oldObjects_ {};
	std::array<Monster, MaxMonsters> oldMonsters_ {}; std::array<unsigned, MaxMonsters> oldActiveMonsters_ {}; size_t oldActiveMonsterCount_ = 0; std::array<bool, 128> oldUniqueFlags_ {};
	OptionalOwnedClxSpriteList font_; size_t ordinaryType_ = 0, highLevelType_ = 0, noDropType_ = 0; int cases_ = 0;
	int droppedCases_ = 0, noDropFlagCases_ = 0, noDropRollCases_ = 0, poolFullCases_ = 0, minionSkippedCases_ = 0;
	int weightedCases_ = 0, goldCases_ = 0, lastSlotCases_ = 0, fallbackCases_ = 0, exhaustedCases_ = 0, historyFreshCases_ = 0, historyRerollCases_ = 0;
	uint32_t historySeed_ = UINT32_MAX;
};

TEST_F(CathedralMonsterLootOracleTest, CaptureOrdinaryMonsterDeathLoot)
{
	const char *directory = std::getenv(OracleDirectoryVariable);
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Select the private Cathedral monster-loot oracle directory explicitly.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root)); ASSERT_TRUE(std::filesystem::is_empty(root));
	const auto finalPath = root / "native-cathedral-monster-loot.json";
	const auto temporaryPath = root / "native-cathedral-monster-loot.json.tmp";
	std::ofstream stream(temporaryPath, std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	{
		JsonObject document(stream); document.String("format", "godevilution-cathedral-monster-loot"); document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-cathedral-ordinary-monster-death-loot");
		document.String("boundary", "MonsterDeath-rndItemSeed-reset-then-SpawnLoot"); document.String("nativeSource", "BUILD_TESTING TestMonsterSpawnLoot -> Source/game/monsters/monsters.cpp:SpawnLoot -> Source/game/items/items.cpp:SpawnItem");
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase); document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.String("gameMode", "single-player"); document.String("difficulty", "normal"); document.String("levelType", "cathedral"); document.Boolean("questsDisabled", true); document.Boolean("hellfire", false); document.Boolean("spawn", false); document.Boolean("multiplayer", false);
			document.Number("monsterRuntimeCount", MonstersData.size()); document.Number("uniqueMonsterCount", UniqueMonstersData.size());
		document.Number("historySearchSeed", historySeed_);
		document.Key("catalog"); WriteCatalog(stream, font_); document.Key("monsterRuntime"); WriteMonsterRuntimeCatalog(stream); document.Key("uniqueMonsters"); WriteUniqueMonsterCatalog(stream); document.Key("cases"); stream << '[';
		for (int depth = 1; depth <= 4; ++depth) {
			CaptureCase(stream, "ordinary-gold-selection", depth, 0, ordinaryType_, false, 0, false, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-high-level-selection", depth, 22, highLevelType_, false, 0, false, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-zero-seed", depth, 0, ordinaryType_, false, 0, false, PlacementMode::Open, 0, true);
			CaptureCase(stream, "ordinary-no-drop-flag", depth, 0x80000000U, noDropType_, false, 0, false, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-no-item", depth, 1, ordinaryType_, false, 0, false, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-full-capacity", depth, 22, ordinaryType_, false, MAXITEMS, true, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-last-slot", depth, 22, ordinaryType_, false, MAXITEMS - 1, true, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-placement-fallback", depth, 22, ordinaryType_, false, 0, false, PlacementMode::Fallback, 0);
			CaptureCase(stream, "ordinary-placement-exhausted", depth, 22, ordinaryType_, false, 0, false, PlacementMode::Exhausted, 0);
			CaptureCase(stream, "ordinary-signed-item-ref", depth, 22, ordinaryType_, false, 0, false, PlacementMode::Fallback, 0, true);
			CaptureCase(stream, "ordinary-unique-history", depth, historySeed_, ordinaryType_, true, 0, false, PlacementMode::Open, 127);
			CaptureCase(stream, "ordinary-unique-history-all", depth, historySeed_, ordinaryType_, true, 0, false, PlacementMode::Open, 128);
			CaptureCase(stream, "ordinary-unique-zero-seed", depth, 0, ordinaryType_, true, 0, false, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-unique-history-fresh", depth, historySeed_, ordinaryType_, true, 0, false, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-extreme-seed", depth, UINT32_MAX, highLevelType_, false, 0, false, PlacementMode::Open, 0);
			CaptureCase(stream, "ordinary-player-minion-skipped", depth, 2588, ordinaryType_, false, 0, false, PlacementMode::Open, 0, false, MFLAG_GOLEM);
			CaptureCase(stream, "ordinary-berserk-golem-drops", depth, 22, ordinaryType_, false, 0, false, PlacementMode::Open, 0, false, MFLAG_GOLEM | MFLAG_BERSERK);
		}
		stream << ']'; document.Number("caseCount", cases_); document.Number("depthCount", 4); document.Number("lanesPerDepth", 17);
		document.Key("nativeCoverage");
		JsonObject coverage(stream);
		coverage.Number("droppedCases", droppedCases_); coverage.Number("noDropFlagCases", noDropFlagCases_); coverage.Number("noDropRollCases", noDropRollCases_);
		coverage.Number("poolFullCases", poolFullCases_); coverage.Number("minionSkippedCases", minionSkippedCases_); coverage.Number("weightedCases", weightedCases_);
		coverage.Number("goldCases", goldCases_); coverage.Number("lastSlotCases", lastSlotCases_); coverage.Number("fallbackCases", fallbackCases_); coverage.Number("exhaustedCases", exhaustedCases_);
		coverage.Number("historyFreshCases", historyFreshCases_); coverage.Number("historyRerollCases", historyRerollCases_);
	}
	stream.flush(); ASSERT_TRUE(stream.good()); stream.close(); ASSERT_EQ(cases_, 68);
	ASSERT_EQ(droppedCases_, 48); ASSERT_EQ(noDropFlagCases_, 4); ASSERT_EQ(noDropRollCases_, 8); ASSERT_EQ(poolFullCases_, 4); ASSERT_EQ(minionSkippedCases_, 4);
	ASSERT_EQ(weightedCases_, 28); ASSERT_EQ(goldCases_, 8); ASSERT_EQ(lastSlotCases_, 4); ASSERT_EQ(fallbackCases_, 8); ASSERT_EQ(exhaustedCases_, 4); ASSERT_EQ(historyFreshCases_, 4); ASSERT_EQ(historyRerollCases_, 4);
	std::error_code renameError;
	std::filesystem::rename(temporaryPath, finalPath, renameError);
	ASSERT_FALSE(renameError) << renameError.message();
}

} // namespace
} // namespace devilution
