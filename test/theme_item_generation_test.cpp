#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <limits>
#include <list>
#include <memory>
#include <optional>
#include <sstream>
#include <string>
#include <string_view>
#include <type_traits>
#include <utility>
#include <vector>

#include "application/diablo.h"
#include "application/game_mode.hpp"
#include "application/headless_mode.hpp"
#include "engine/assets.hpp"
#include "engine/audio/sound.h"
#include "engine/cursor.h"
#include "engine/lighting.h"
#include "engine/load/load_cel.hpp"
#include "engine/load/load_clx.hpp"
#include "engine/load/load_file.hpp"
#include "engine/random.hpp"
#include "engine/render/text_render.hpp"
#include "game/items/item_pool.hpp"
#include "game/items/items.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/themes.h"
#include "game/levels/tile_properties.hpp"
#include "game/levels/triggers.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monster_corpses.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/monsters/monsters.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "game/players/inventory.hpp"
#include "game/players/players.hpp"
#include "game/portals/portal.hpp"
#include "game/quests/quests.hpp"
#include "network/protocol/msg.h"
#include "network/storm/storm_net.hpp"
#include "persistence/loadsave.h"
#include "persistence/options.h"
#include "tables/leveldat.h"
#include "tables/misdat.h"
#include "tables/playerdat.hpp"
#include "tables/textdat.h"
#include "utils/language.h"
#include "utils/parse_int.hpp"
#include "utils/paths.h"

namespace devilution {
namespace {

constexpr char OracleDirectoryVariable[] = "GODEVILUTION_THEME_ITEM_ORACLE_DIRECTORY";

void JsonString(std::ostream &out, std::string_view value)
{
	constexpr char Hex[] = "0123456789abcdef";
	out << '"';
	for (unsigned char ch : value) {
		if (ch == '"' || ch == '\\')
			out << '\\' << ch;
		else if (ch < 0x20)
			out << "\\u00" << Hex[ch >> 4] << Hex[ch & 15];
		else
			out << ch;
	}
	out << '"';
}

class JsonObject {
public:
	explicit JsonObject(std::ostream &out)
	    : out(out)
	{
		out << '{';
	}
	~JsonObject() { out << '}'; }
	void Key(std::string_view name)
	{
		if (!first) out << ',';
		first = false;
		JsonString(out, name);
		out << ':';
	}
	template <typename T>
	void Number(std::string_view name, T value)
	{
		Key(name);
		if constexpr (std::is_enum_v<T>)
			out << static_cast<int64_t>(value);
		else if constexpr (std::is_unsigned_v<T>)
			out << static_cast<uint64_t>(value);
		else
			out << static_cast<int64_t>(value);
	}
	void Boolean(std::string_view name, bool value)
	{
		Key(name);
		out << (value ? "true" : "false");
	}
	void String(std::string_view name, std::string_view value)
	{
		Key(name);
		JsonString(out, value);
	}

private:
	std::ostream &out;
	bool first = true;
};

void WriteItem(std::ostream &out, const Item &item)
{
	JsonObject json(out);
#define ITEM_NUMBER(key, field) json.Number(key, item.field)
#define ITEM_BOOL(key, field) json.Boolean(key, item.field)
	ITEM_NUMBER("seed", _iSeed);
	ITEM_NUMBER("createInfo", _iCreateInfo);
	ITEM_NUMBER("itemType", _itype);
	ITEM_BOOL("animFlag", _iAnimFlag);
	ITEM_NUMBER("positionX", position.x);
	ITEM_NUMBER("positionY", position.y);
	ITEM_BOOL("deleted", _iDelFlag);
	ITEM_NUMBER("selectionRegion", selectionRegion);
	ITEM_BOOL("postDraw", _iPostDraw);
	ITEM_BOOL("identified", _iIdentified);
	ITEM_NUMBER("magical", _iMagical);
	json.String("baseName", item._iName);
	json.String("identifiedName", item._iIName);
	ITEM_NUMBER("equipLocation", _iLoc);
	ITEM_NUMBER("itemClass", _iClass);
	ITEM_NUMBER("cursorGraphic", _iCurs);
	ITEM_NUMBER("value", _ivalue);
	ITEM_NUMBER("identifiedValue", _iIvalue);
	ITEM_NUMBER("minDamage", _iMinDam);
	ITEM_NUMBER("maxDamage", _iMaxDam);
	ITEM_NUMBER("armorClass", _iAC);
	ITEM_NUMBER("flags", _iFlags);
	ITEM_NUMBER("miscId", _iMiscId);
	ITEM_NUMBER("spellId", _iSpell);
	ITEM_NUMBER("itemIndex", IDidx);
	ITEM_NUMBER("charges", _iCharges);
	ITEM_NUMBER("maxCharges", _iMaxCharges);
	ITEM_NUMBER("durability", _iDurability);
	ITEM_NUMBER("maxDurability", _iMaxDur);
	ITEM_NUMBER("plusDamage", _iPLDam);
	ITEM_NUMBER("plusToHit", _iPLToHit);
	ITEM_NUMBER("plusArmorClass", _iPLAC);
	ITEM_NUMBER("plusStrength", _iPLStr);
	ITEM_NUMBER("plusMagic", _iPLMag);
	ITEM_NUMBER("plusDexterity", _iPLDex);
	ITEM_NUMBER("plusVitality", _iPLVit);
	ITEM_NUMBER("plusFireResistance", _iPLFR);
	ITEM_NUMBER("plusLightningResistance", _iPLLR);
	ITEM_NUMBER("plusMagicResistance", _iPLMR);
	ITEM_NUMBER("plusMana", _iPLMana);
	ITEM_NUMBER("plusHitPoints", _iPLHP);
	ITEM_NUMBER("plusDamageModifier", _iPLDamMod);
	ITEM_NUMBER("plusGetHit", _iPLGetHit);
	ITEM_NUMBER("plusLight", _iPLLight);
	ITEM_NUMBER("spellLevelAdd", _iSplLvlAdd);
	ITEM_NUMBER("uniqueId", _iUid);
	ITEM_NUMBER("fireMinDamage", _iFMinDam);
	ITEM_NUMBER("fireMaxDamage", _iFMaxDam);
	ITEM_NUMBER("lightningMinDamage", _iLMinDam);
	ITEM_NUMBER("lightningMaxDamage", _iLMaxDam);
	ITEM_NUMBER("plusEnemyArmorClass", _iPLEnAc);
	ITEM_NUMBER("prefixPower", _iPrePower);
	ITEM_NUMBER("suffixPower", _iSufPower);
	ITEM_NUMBER("valueAdd1", _iVAdd1);
	ITEM_NUMBER("valueMultiply1", _iVMult1);
	ITEM_NUMBER("valueAdd2", _iVAdd2);
	ITEM_NUMBER("valueMultiply2", _iVMult2);
	ITEM_NUMBER("minimumStrength", _iMinStr);
	ITEM_NUMBER("minimumMagic", _iMinMag);
	ITEM_NUMBER("minimumDexterity", _iMinDex);
	ITEM_BOOL("statFlag", _iStatFlag);
	ITEM_BOOL("request", _iRequest);
	ITEM_NUMBER("hellfireDamageArmorFlags", _iDamAcFlags);
	ITEM_NUMBER("buff", dwBuff);
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
	json.Number("type", power.type);
	json.Number("param1", power.param1);
	json.Number("param2", power.param2);
}

template <typename T, typename F>
void WriteArray(std::ostream &out, const T &values, F write)
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

void WriteFlags(std::ostream &out, const std::array<bool, 128> &flags)
{
	WriteArray(out, flags, [&](bool flag) { out << (flag ? "true" : "false"); });
}

std::array<bool, 128> CaptureFlags()
{
	std::array<bool, 128> flags;
	std::copy(std::begin(UniqueItemFlags), std::end(UniqueItemFlags), flags.begin());
	return flags;
}

constexpr size_t PreparedCatacombsUniqueHistoryId = 7;

// This dedicated fixture owns every mutated gameplay/table scalar. It never
// loads Diablo archives, allocates ground pools, or changes a normal profile.
struct NativeStateGuard {
	bool oldHellfire = gbIsHellfire;
	bool oldSpawn = gbIsSpawn;
	bool oldMultiplayer = gbIsMultiplayer;
	LevelId oldLevel = currentLevel().id();
	decltype(sgGameInitInfo.nDifficulty) oldDifficulty = sgGameInitInfo.nDifficulty;
	uint32_t oldRandom = GetLCGEngineState();
	std::string oldLanguage = std::string(*GetOptions().Language.code);
	bool oldBard = *GetOptions().Gameplay.testBard;
	Player *oldPlayer = MyPlayer;
	std::vector<Player> oldPlayers = std::move(Players);
	std::vector<ItemData> oldItems = AllItemsList;
	std::vector<UniqueItem> oldUniques = UniqueItems;
	std::vector<PLStruct> oldPrefixes = ItemPrefixes;
	std::vector<PLStruct> oldSuffixes = ItemSuffixes;
	std::vector<SpellData> oldSpells = SpellsData;
	ItemGenerationConfig oldConfig = CurrentItemGenerationConfig;
	decltype(ItemMappingIdsToIndices) oldItemMappings = ItemMappingIdsToIndices;
	decltype(UniqueItemMappingIdsToIndices) oldUniqueMappings = UniqueItemMappingIdsToIndices;
	std::array<bool, 128> oldFlags = CaptureFlags();

	~NativeStateGuard()
	{
		gbIsHellfire = oldHellfire;
		gbIsSpawn = oldSpawn;
		gbIsMultiplayer = oldMultiplayer;
		currentLevel().setId(oldLevel);
		sgGameInitInfo.nDifficulty = oldDifficulty;
		SetRndSeed(oldRandom);
		GetOptions().Language.code = oldLanguage;
		GetOptions().Gameplay.testBard.SetValue(oldBard);
		Players = std::move(oldPlayers);
		MyPlayer = oldPlayer;
		AllItemsList = std::move(oldItems);
		UniqueItems = std::move(oldUniques);
		ItemPrefixes = std::move(oldPrefixes);
		ItemSuffixes = std::move(oldSuffixes);
		SpellsData = std::move(oldSpells);
		CurrentItemGenerationConfig = std::move(oldConfig);
		ItemMappingIdsToIndices = std::move(oldItemMappings);
		UniqueItemMappingIdsToIndices = std::move(oldUniqueMappings);
		std::copy(oldFlags.begin(), oldFlags.end(), std::begin(UniqueItemFlags));
		UnloadFonts();
	}
};

class ThemeItemGenerationTest : public testing::Test {
protected:
	void SetUp() override
	{
		guard = std::make_unique<NativeStateGuard>();
		gbIsHellfire = false;
		gbIsSpawn = false;
		gbIsMultiplayer = false;
		GetOptions().Language.code = "en";
		GetOptions().Gameplay.testBard.SetValue(false);
		ASSERT_EQ(GetLanguageCode(), "en");
		sgGameInitInfo.nDifficulty = DIFF_NORMAL;
		currentLevel().setId(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL });
		Players.resize(1);
		MyPlayer = &Players[0];
		MyPlayer->life.maximumBase = 6400;
		MyPlayer->mana.maximumBase = 3200;
		MyPlayer->pLvlLoad = 1;
		ClearUniqueItemFlags();
		// Check the optional loader before any StringInPanel call. Missing font
		// must fail as a test assertion, never enter the fatal error UI path.
		font = LoadOptionalClx("fonts\\12-00.clx");
		ASSERT_TRUE(font.has_value()) << "The bundled native test font is required.";
		ASSERT_EQ(font->numSprites(), 256U);
		for (const char *path : { "txtdata\\items\\itemdat.tsv", "txtdata\\items\\unique_itemdat.tsv",
		         "txtdata\\items\\item_prefixes.tsv", "txtdata\\items\\item_suffixes.tsv",
		         "txtdata\\items\\item_generation.tsv", "txtdata\\spells\\spelldat.tsv" }) {
			ASSERT_TRUE(FindAsset(path).ok()) << path;
		}
		UnloadFonts();
		LoadItemData();
		LoadSpellData();
		ASSERT_FALSE(AllItemsList.empty());
		ASSERT_FALSE(SpellsData.empty());
	}

	void WriteCatalog(std::ostream &out)
	{
		JsonObject catalog(out);
		catalog.Key("items");
		int index = 0;
		WriteArray(out, AllItemsList, [&](const ItemData &row) {
			JsonObject item(out);
			item.Number("nativeItemId", index);
			item.Boolean("available", IsItemAvailable(index++));
#define BASE_NUMBER(key, field) item.Number(key, row.field)
			BASE_NUMBER("dropRate", dropRate);
			BASE_NUMBER("uniqueBaseItemId", iItemId);
			BASE_NUMBER("itemType", itype);
			BASE_NUMBER("cursorGraphic", iCurs);
			item.String("name", row.iName);
			item.String("shortName", row.iSName);
			BASE_NUMBER("equipLocation", iLoc);
			BASE_NUMBER("itemClass", iClass);
			BASE_NUMBER("minimumMonsterLevel", iMinMLvl);
			BASE_NUMBER("minDamage", iMinDam);
			BASE_NUMBER("maxDamage", iMaxDam);
			BASE_NUMBER("minArmorClass", iMinAC);
			BASE_NUMBER("maxArmorClass", iMaxAC);
			BASE_NUMBER("flags", iFlags);
			BASE_NUMBER("miscId", iMiscId);
			BASE_NUMBER("spellId", iSpell);
			BASE_NUMBER("value", iValue);
			BASE_NUMBER("durability", iDurability);
			BASE_NUMBER("minimumStrength", iMinStr);
			BASE_NUMBER("minimumMagic", iMinMag);
			BASE_NUMBER("minimumDexterity", iMinDex);
			item.Boolean("usable", row.iUsable);
#undef BASE_NUMBER
		});
		auto writeAffixes = [&](std::string_view name, const std::vector<PLStruct> &rows) {
			catalog.Key(name);
			WriteArray(out, rows, [&](const PLStruct &row) {
				JsonObject affix(out);
				affix.String("name", row.PLName);
				affix.Key("power");
				WritePower(out, row.power);
				affix.Number("minimumLevel", row.PLMinLvl);
				affix.Number("itemTypes", row.PLIType);
				affix.Number("alignment", row.PLGOE);
				affix.Number("chance", row.PLChance);
				affix.Boolean("useful", row.PLOk);
				affix.Number("minValue", row.minVal);
				affix.Number("maxValue", row.maxVal);
				affix.Number("valueMultiplier", row.multVal);
			});
		};
		writeAffixes("prefixes", ItemPrefixes);
		writeAffixes("suffixes", ItemSuffixes);
		catalog.Key("uniques");
		index = 0;
		WriteArray(out, UniqueItems, [&](const UniqueItem &row) {
			JsonObject unique(out);
			unique.Number("nativeUniqueId", index++);
			unique.String("name", row.UIName);
			unique.Number("cursorGraphic", row.UICurs);
			unique.Number("uniqueBaseItemId", row.UIItemId);
			unique.Number("minimumLevel", row.UIMinLvl);
			unique.Number("numberOfPowers", row.UINumPL);
			unique.Number("value", row.UIValue);
			unique.Key("powers");
			WriteArray(out, row.powers, [&](const ItemPower &power) { WritePower(out, power); });
		});
		catalog.Key("spells");
		index = 0;
		WriteArray(out, SpellsData, [&](const SpellData &row) {
			JsonObject spell(out);
			spell.Number("nativeSpellId", index++);
			spell.String("name", row.sNameText);
			spell.Number("bookLevel", row.sBookLvl);
			spell.Number("staffLevel", row.sStaffLvl);
			spell.Number("minimumMagic", row.minInt);
			spell.Number("bookCost", row.bookCost());
			spell.Number("staffCost", row.staffCost());
			spell.Number("magicType", row.type());
			spell.Number("staffMinCharges", row.sStaffMin);
			spell.Number("staffMaxCharges", row.sStaffMax);
		});
		catalog.Key("glyphWidths");
		out << '[';
		for (unsigned i = 0; i < 256; ++i) {
			if (i != 0) out << ',';
			out << (*font)[i].width();
		}
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
			JsonObject glyphRow(out);
			glyphRow.Number("row", row);
			glyphRow.Key("widths");
			out << '[';
			for (unsigned i = 0; i < 256; ++i) {
				if (i != 0) out << ',';
				out << (*rowFont)[i].width();
			}
			out << ']';
		}
		out << ']';
		catalog.Key("animations");
		out << '[';
		bool firstAnimation = true;
		for (int cursor = 0; cursor <= UINT8_MAX; ++cursor) {
			const auto frames = GetItemAnimationLengthForTesting(cursor);
			if (!frames.has_value()) continue;
			if (!firstAnimation) out << ',';
			firstAnimation = false;
			JsonObject animation(out);
			animation.Number("cursorGraphic", cursor);
			animation.Number("frameCount", *frames);
			animation.Number("ticksPerFrame", 1);
		}
		out << ']';
		catalog.Key("cursors");
		{
			JsonObject cursors(out);
			cursors.Number("bookRed", ICURS_BOOK_RED);
			cursors.Number("bookBlue", ICURS_BOOK_BLUE);
			cursors.Number("bookGrey", ICURS_BOOK_GREY);
			cursors.Number("goldSmall", ICURS_GOLD_SMALL);
			cursors.Number("goldMedium", ICURS_GOLD_MEDIUM);
			cursors.Number("goldLarge", ICURS_GOLD_LARGE);
		}
		catalog.Key("generationRules");
		JsonObject rules(out);
#define RULE(field) rules.Number(#field, CurrentItemGenerationConfig.field)
		RULE(magicChanceBase);
		RULE(magicChancePerLevel);
		RULE(uniqueChanceNormal);
		RULE(uniqueChanceUnique);
		RULE(bonusLevelsUnique);
		RULE(prefixPercent);
		RULE(suffixPercent);
		RULE(onlygoodChance);
		RULE(noDropPercent);
		RULE(goldPercent);
#undef RULE
		rules.Key("alwaysMagicMisc");
		WriteArray(out, CurrentItemGenerationConfig.alwaysMagicMisc, [&](auto value) { out << static_cast<int>(value); });
	}

	std::unique_ptr<NativeStateGuard> guard;
	OptionalOwnedClxSpriteList font;
};

TEST_F(ThemeItemGenerationTest, GoldUsesCurrentDepthAndRetainsBaseIdentifiedValue)
{
	currentLevelNumber() = 4;
	Item item;
	SetupAllItems(*MyPlayer, item, IDI_GOLD, 0, 2, CurrentItemGenerationConfig.uniqueChanceNormal, false, true);
	EXPECT_EQ(item._iIvalue, AllItemsList[IDI_GOLD].iValue);
	EXPECT_GE(item._ivalue, 20);
	EXPECT_LT(item._ivalue, 60);
	EXPECT_EQ(item._iCreateInfo, 2 | CF_PREGEN | CF_UPER1);
	EXPECT_EQ(item._iSeed, 0U);
}

TEST_F(ThemeItemGenerationTest, SetupRetainsFieldsOutsideBaseAttributes)
{
	Item item;
	item.position = { 9, 8 };
	item._iCharges = 7;
	item._iPLStr = 17;
	item._iIdentified = true;
	SetupAllItems(*MyPlayer, item, IDI_HEAL, 2588, 2, CurrentItemGenerationConfig.uniqueChanceNormal, false, true);
	EXPECT_EQ(item.position, (Point { 9, 8 }));
	EXPECT_EQ(item._iCharges, 7);
	EXPECT_EQ(item._iPLStr, 17);
	EXPECT_TRUE(item._iIdentified);
}

TEST_F(ThemeItemGenerationTest, ScalarFontMetricsMatchNativePanelWidth)
{
	EXPECT_FALSE(GetItemAnimationLengthForTesting(-1).has_value());
	EXPECT_FALSE(GetItemAnimationLengthForTesting(256).has_value());
	ASSERT_TRUE(GetItemAnimationLengthForTesting(ICURS_GOLD_SMALL).has_value());
	EXPECT_GT(*GetItemAnimationLengthForTesting(ICURS_GOLD_SMALL), 0);
	constexpr std::string_view name = "An unusually long test name of accuracy";
	int width = -2;
	for (unsigned char ch : name)
		width += (*font)[ch].width() + 2;
	EXPECT_EQ(GetLineWidth(name, GameFont12, 2), width);
	EXPECT_EQ(GetLineWidth("A\xe2\x80\x8b"
	                       "B\nignored",
	              GameFont12, 2),
	    (*font)['A'].width() + (*font)['B'].width() + 2);
}

TEST_F(ThemeItemGenerationTest, CaptureIndependentRawSetupAndUniqueContinuation)
{
	const char *directory = std::getenv(OracleDirectoryVariable);
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Select the private theme-item oracle directory explicitly.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained evidence.";
	const auto path = root / "native-theme-items.json";
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int cases = 0;
	int initialUniques = 0;
	int changedUniques = 0;
	int exhaustedUniques = 0;
	int reverseCompatibleUniques = 0;
	int offsetUniques = 0;
	int books = 0;
	int gold = 0;
	int staffSpells = 0;
	int staffPowers = 0;
	int magicalAffixes = 0;
	int ordinaryDurability = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-theme-item-setup");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-raw-setup-and-unique-continuation");
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("cases");
		stream << '[';
		auto capture = [&](std::string_view name, int depth, int index, uint32_t seed, bool onlygood,
		                   const std::array<bool, 128> &flags, const Item &pre = Item {}) {
			if (cases++ != 0) stream << ',';
			currentLevelNumber() = depth;
			std::copy(flags.begin(), flags.end(), std::begin(UniqueItemFlags));
			Item item = pre;
			SetupAllItems(*MyPlayer, item, static_cast<_item_indexes>(index), seed, 2 * depth,
			    CurrentItemGenerationConfig.uniqueChanceNormal, onlygood, true);
			const auto setupRandom = GetLCGEngineState();
			const Item setupItem = item;
			if (item._iMagical == ITEM_QUALITY_UNIQUE) ++initialUniques;
			TryRandomUniqueItem(item, static_cast<_item_indexes>(index), 2 * depth,
			    CurrentItemGenerationConfig.uniqueChanceNormal, onlygood, true);
			if (setupItem._iMagical == ITEM_QUALITY_UNIQUE && item._iMagical == ITEM_QUALITY_UNIQUE && item._iUid != setupItem._iUid) {
				++changedUniques;
				if ((item.dwBuff & CF_UIDOFFSET) != 0)
					++offsetUniques;
				else if (item._iSeed != seed)
					++reverseCompatibleUniques;
			}
			if (setupItem._iMagical == ITEM_QUALITY_UNIQUE && item._iMagical != ITEM_QUALITY_UNIQUE) ++exhaustedUniques;
			if (item._iMiscId == IMISC_BOOK) ++books;
			if (item._itype == ItemType::Gold) ++gold;
			if (item._itype == ItemType::Staff && item._iMagical != ITEM_QUALITY_UNIQUE) {
				if (item._iSpell != SpellID::Null)
					++staffSpells;
				else if (onlygood)
					++staffPowers;
			}
			if (item._iMagical == ITEM_QUALITY_MAGIC && (item._iPrePower != IPL_INVALID || item._iSufPower != IPL_INVALID)) ++magicalAffixes;
			if (item._iMagical == ITEM_QUALITY_NORMAL && item._iDurability > 0 && item._iDurability != DUR_INDESTRUCTIBLE && item._iDurability != item._iMaxDur) ++ordinaryDurability;
			JsonObject result(stream);
			result.String("name", name);
			result.Number("depth", depth);
			result.Number("nativeItemId", index);
			result.Number("seed", seed);
			result.Boolean("onlygood", onlygood);
			result.Key("inputItem");
			WriteItem(stream, pre);
			result.Key("inputUniqueFlags");
			WriteFlags(stream, flags);
			result.Key("setupItem");
			WriteItem(stream, setupItem);
			result.Number("setupRandomState", setupRandom);
			result.Key("expectedItem");
			WriteItem(stream, item);
			result.Number("expectedRandomState", GetLCGEngineState());
			result.Key("expectedUniqueFlags");
			WriteFlags(stream, CaptureFlags());
			// SetupItem uses only the external cursor animation scalars here;
			// sprites and private renderer distribution state remain excluded.
			ASSERT_TRUE(GetItemAnimationLengthForTesting(item._iCurs).has_value());
			SetupItem(item);
			result.Key("groundItem");
			WriteItem(stream, item);
		};
		const std::array<bool, 128> fresh {};
		std::array<bool, 128> exhausted;
		exhausted.fill(true);
		// The final two seeds put INT_MIN at the first/second raw draw. Those
		// draws are safe scalar/coin/book-counter consumers, not vector indices.
		constexpr uint32_t seeds[] = { 0, 1, 2588, 743271966, UINT32_C(0x80000000), UINT32_MAX, 1457187811, 988045466 };
		for (int depth = 1; depth <= 4; ++depth) {
			for (size_t index = 0; index < AllItemsList.size(); ++index) {
				const auto &row = AllItemsList[index];
				const bool isBook = row.itype == ItemType::Misc && row.iMiscId == IMISC_BOOK;
				if (!IsItemAvailable(static_cast<int>(index)) || row.dropRate == 0 || (!isBook && row.iMinMLvl > 2 * depth)
				    || row.iSpell == SpellID::Resurrect || row.iSpell == SpellID::HealOther) continue;
				for (uint32_t seed : seeds) {
					for (bool onlygood : { false, true }) {
						// RndUItem admits books before the ordinary min-monster-level
						// predicate, including later book templates at depth one.
						if (!onlygood && row.iMinMLvl > 2 * depth) continue;
						capture("ordinary", depth, static_cast<int>(index), seed, onlygood, fresh);
					}
				}
				if (row.itype == ItemType::Staff) {
					for (uint32_t seed = 2; seed <= 128; ++seed) {
						Item item;
						SetupAllItems(*MyPlayer, item, static_cast<_item_indexes>(index), seed, 2 * depth,
						    CurrentItemGenerationConfig.uniqueChanceNormal, true, true);
						if (item._iMagical == ITEM_QUALITY_UNIQUE || item._iSpell != SpellID::Null) continue;
						capture("staff-power", depth, static_cast<int>(index), seed, true, fresh);
						break;
					}
				}
				// Bounded seed discovery only selects inputs; all expected outputs
				// come from the actual native calls, never a reimplemented oracle.
				for (uint32_t seed = 1; seed <= 4096; ++seed) {
					Item item;
					SetupAllItems(*MyPlayer, item, static_cast<_item_indexes>(index), seed, 2 * depth,
					    CurrentItemGenerationConfig.uniqueChanceNormal, true, true);
					if (item._iMagical != ITEM_QUALITY_UNIQUE) continue;
					capture("unique-fresh", depth, static_cast<int>(index), seed, true, fresh);
					capture("unique-exhausted", depth, static_cast<int>(index), seed, true, exhausted);
					for (size_t uid = 0; uid < UniqueItems.size(); ++uid) {
						if (UniqueItems[uid].UIItemId != row.iItemId || UniqueItems[uid].UIMinLvl > 2 * depth) continue;
						auto oneRemaining = exhausted;
						oneRemaining[uid] = false;
						capture("unique-one-remaining", depth, static_cast<int>(index), seed, true, oneRemaining);
					}
					break;
				}
			}
		}
		Item retained;
		retained.position = { 9, 8 };
		retained._iCharges = 7;
		retained._iMaxCharges = 9;
		retained._iPLStr = 17;
		retained._iVAdd1 = 73;
		retained._iIdentified = true;
		retained._iStatFlag = true;
		retained.dwBuff = 128;
		capture("retained-fields", 1, IDI_HEAL, 2588, false, fresh, retained);
		// Staff's cyclic counter can safely be negative: unlike books its
		// default spell is Null, and the native helpers continue with row zero.
		capture("staff-signed-counter", 1, 151, 1272442071, true, fresh);
		stream << ']';
		document.Number("caseCount", cases);
		document.Number("initialUniqueCount", initialUniques);
		document.Number("changedUniqueCount", changedUniques);
		document.Number("exhaustedUniqueCount", exhaustedUniques);
		document.Key("coverage");
		JsonObject coverage(stream);
		coverage.Number("reverseCompatibleUniques", reverseCompatibleUniques);
		coverage.Number("offsetUniques", offsetUniques);
		coverage.Number("books", books);
		coverage.Number("gold", gold);
		coverage.Number("staffSpells", staffSpells);
		coverage.Number("staffPowers", staffPowers);
		coverage.Number("magicalAffixes", magicalAffixes);
		coverage.Number("ordinaryDurability", ordinaryDurability);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_GT(cases, 1000);
	EXPECT_GT(initialUniques, 0);
	EXPECT_GT(changedUniques, 0);
	EXPECT_GT(exhaustedUniques, 0);
	EXPECT_GT(reverseCompatibleUniques, 0);
	EXPECT_GT(offsetUniques, 0);
	EXPECT_GT(books, 0);
	EXPECT_GT(gold, 0);
	EXPECT_GT(staffSpells, 0);
	EXPECT_GT(staffPowers, 0);
	EXPECT_GT(magicalAffixes, 0);
	EXPECT_GT(ordinaryDurability, 0);
}

// The world fixture owns tile/SOL/object/item storage in addition to the
// existing raw-table guard. No client, network message or archive is opened.
struct NativeItemWorldGuard {
	std::unique_ptr<TileGrid> oldTiles = std::make_unique<TileGrid>(tiles());
	std::array<TileProperties, MAXTILES> oldSol;
	std::array<Item, MAXITEMS + 1> oldItems;
	std::array<uint8_t, MAXITEMS> oldActive;
	std::array<Object, MAXOBJECTS> oldObjects;
	uint8_t oldCount = ActiveItemCount;

	NativeItemWorldGuard()
	{
		std::copy(std::begin(tileProperties()), std::end(tileProperties()), oldSol.begin());
		std::copy_n(Items, oldItems.size(), oldItems.begin());
		std::copy(std::begin(ActiveItems), std::end(ActiveItems), oldActive.begin());
		std::copy_n(Objects, oldObjects.size(), oldObjects.begin());
	}
	~NativeItemWorldGuard()
	{
		tiles() = *oldTiles;
		std::copy(oldSol.begin(), oldSol.end(), std::begin(tileProperties()));
		std::copy(oldItems.begin(), oldItems.end(), Items);
		std::copy(oldActive.begin(), oldActive.end(), std::begin(ActiveItems));
		std::copy(oldObjects.begin(), oldObjects.end(), Objects);
		ActiveItemCount = oldCount;
	}
};

struct NativeItemWorldState {
	std::array<Item, MAXITEMS + 1> items;
	std::array<uint8_t, MAXITEMS> active;
	std::array<bool, 128> uniqueFlags;
	std::vector<Tile> worldTiles;
	std::array<bool, MAXOBJECTS> objectSolid;
	std::array<int, MAXOBJECTS> objectSelection;
	uint32_t randomState;
	int activeCount;
};

NativeItemWorldState CaptureItemWorld()
{
	NativeItemWorldState state;
	std::copy_n(Items, state.items.size(), state.items.begin());
	std::copy(std::begin(ActiveItems), std::end(ActiveItems), state.active.begin());
	state.uniqueFlags = CaptureFlags();
	state.randomState = GetLCGEngineState();
	state.activeCount = ActiveItemCount;
	for (const Tile &tile : tiles())
		state.worldTiles.push_back(tile);
	for (int i = 0; i < MAXOBJECTS; ++i) {
		state.objectSolid[i] = Objects[i]._oSolidFlag;
		state.objectSelection[i] = static_cast<int>(Objects[i].selectionRegion);
	}
	return state;
}

void WriteItemWorld(std::ostream &out, const NativeItemWorldState &state, bool inputs)
{
	JsonObject json(out);
	json.Number("randomState", state.randomState);
	json.Number("activeCount", state.activeCount);
	json.Key("activeIds");
	WriteArray(out, state.active, [&](uint8_t value) { out << static_cast<int>(value); });
	json.Key("items");
	WriteArray(out, state.items, [&](const Item &value) { WriteItem(out, value); });
	json.Key("uniqueFlags");
	WriteFlags(out, state.uniqueFlags);
	json.Key("itemRefs");
	WriteArray(out, state.worldTiles, [&](const Tile &tile) { out << static_cast<int>(tile.item()); });
	if (!inputs) return;
	json.Key("tiles");
	int index = 0;
	WriteArray(out, state.worldTiles, [&](const Tile &tile) {
		const Point position { index % MAXDUNX, index / MAXDUNX };
		++index;
		out << '[' << static_cast<int>(tile.player()) << ',' << tile.monster() << ','
		    << static_cast<int>(tile.object()) << ',' << static_cast<int>(tile.item()) << ','
		    << (TileContainsSetPiece(position) ? "true" : "false") << ','
		    << (IsTileSolid(position) ? "true" : "false") << ']';
	});
	json.Key("objects");
	out << '[';
	for (int i = 0; i < MAXOBJECTS; ++i) {
		if (i != 0) out << ',';
		out << '[' << (state.objectSolid[i] ? "true" : "false") << ',' << state.objectSelection[i] << ']';
	}
	out << ']';
}

class ThemeItemPlacementTest : public ThemeItemGenerationTest {
protected:
	void SetUp() override
	{
		ThemeItemGenerationTest::SetUp();
		if (!HasFatalFailure()) worldGuard = std::make_unique<NativeItemWorldGuard>();
	}

	void PrepareWorld(int count = 2, bool reverse = true)
	{
		for (Tile &tile : tiles())
			tile = {};
		std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
		tileProperties()[1] = TileProperties::Solid;
		for (int i = 0; i < MAXOBJECTS; ++i)
			Objects[i] = {};
		for (int i = 0; i <= MAXITEMS; ++i) {
			Items[i] = {};
			Items[i]._iSeed = 1000 + i;
			Items[i]._iPLStr = 17;
			Items[i]._iCharges = 7;
			Items[i]._iMaxCharges = 9;
			Items[i]._iIdentified = true;
			Items[i].dwBuff = 128;
			Items[i].animInfo.currentFrame = 3;
			Items[i].animInfo.numberOfFrames = 4;
		}
		for (int i = 0; i < MAXITEMS; ++i)
			ActiveItems[i] = reverse ? MAXITEMS - 1 - i : i;
		ActiveItemCount = count;
		for (int i = 0; i < count; ++i) {
			const int slot = ActiveItems[i];
			const Point p { 4 + i % 100, 4 + i / 100 };
			Items[slot]._itype = ItemType::Misc;
			Items[slot].position = p;
			tileAt(p).setItem(slot + 1);
		}
		ClearUniqueItemFlags();
		MyPlayer->pLvlLoad = 1;
	}

	void CheckUntouchedWorld(const NativeItemWorldState &before)
	{
		int index = 0;
		for (const Tile &tile : tiles()) {
			const Tile &pre = before.worldTiles[index++];
			EXPECT_EQ(tile.piece(), pre.piece());
			EXPECT_EQ(tile.flags(), pre.flags());
			EXPECT_EQ(tile.player(), pre.player());
			EXPECT_EQ(tile.monster(), pre.monster());
			EXPECT_EQ(tile.object(), pre.object());
			EXPECT_EQ(tile.corpse(), pre.corpse());
			EXPECT_EQ(tile.light(), pre.light());
			EXPECT_EQ(tile.preLight(), pre.preLight());
			EXPECT_EQ(tile.transVal(), pre.transVal());
			EXPECT_EQ(tile.special(), pre.special());
		}
		for (int i = 0; i < MAXOBJECTS; ++i) {
			EXPECT_EQ(Objects[i]._oSolidFlag, before.objectSolid[i]);
			EXPECT_EQ(static_cast<int>(Objects[i].selectionRegion), before.objectSelection[i]);
		}
	}

private:
	std::unique_ptr<NativeItemWorldGuard> worldGuard;
};

TEST_F(ThemeItemPlacementTest, ItemSpaceUsesSignedOccupancyAndObjectOverlapRules)
{
	PrepareWorld(0);
	constexpr Point p { 50, 50 };
	tileAt(p).addFlags(DungeonFlag::Populated);
	EXPECT_TRUE(ItemSpaceOk(p)); // Set-piece protection belongs to ItemPlace, not this path.
	tileAt(p).setPlayer(-1);
	EXPECT_FALSE(ItemSpaceOk(p));
	tileAt(p).setPlayer(0);
	tileAt(p).setMonster(-1);
	EXPECT_FALSE(ItemSpaceOk(p));
	tileAt(p).setMonster(0);
	tileAt(p).setItem(-1);
	EXPECT_FALSE(ItemSpaceOk(p));
	tileAt(p).setItem(0);
	tileAt(p).setObject(-1);
	EXPECT_TRUE(ItemSpaceOk(p));
	Objects[0]._oSolidFlag = true;
	EXPECT_FALSE(ItemSpaceOk(p));
	Objects[0]._oSolidFlag = false;
	tileAt(p).setObject(0);
	tileAt(p + Direction::South).setObject(-1);
	Objects[0].selectionRegion = SelectionRegion::Bottom;
	EXPECT_FALSE(ItemSpaceOk(p));
	Objects[0].selectionRegion = SelectionRegion::None;
	EXPECT_TRUE(ItemSpaceOk(p));
	tileAt(p + Direction::South).setObject(0);
	Objects[0].selectionRegion = SelectionRegion::Bottom;
	Objects[1].selectionRegion = SelectionRegion::Bottom;
	tileAt(p + Direction::SouthEast).setObject(1);
	tileAt(p + Direction::SouthWest).setObject(2);
	EXPECT_FALSE(ItemSpaceOk(p));
	tileAt(p + Direction::SouthWest).setObject(-2);
	EXPECT_TRUE(ItemSpaceOk(p));
	tileAt(p + Direction::SouthWest).setObject(2);
	tileAt(p + Direction::SouthEast).setObject(-1);
	EXPECT_TRUE(ItemSpaceOk(p));
	EXPECT_FALSE(ItemSpaceOk({ -1, 0 }));
	EXPECT_FALSE(ItemSpaceOk({ MAXDUNX, 0 }));
}

TEST_F(ThemeItemPlacementTest, FullPoolTypedGoldPreservesSeedAndEveryItemSlot)
{
	PrepareWorld(MAXITEMS);
	SetRndSeed(2588);
	const auto before = CaptureItemWorld();
	CreateTypeItem({ 50, 50 }, false, ItemType::Gold, IMISC_NONE, false, true);
	const auto after = CaptureItemWorld();
	EXPECT_EQ(after.randomState, before.randomState);
	EXPECT_EQ(after.activeCount, before.activeCount);
	EXPECT_EQ(after.active, before.active);
	EXPECT_EQ(after.uniqueFlags, before.uniqueFlags);
	std::ostringstream first;
	std::ostringstream second;
	WriteItemWorld(first, before, false);
	WriteItemWorld(second, after, false);
	EXPECT_EQ(first.str(), second.str());
	CheckUntouchedWorld(before);
}

TEST_F(ThemeItemPlacementTest, CaptureIndependentSelectorsAndWorldCreation)
{
	constexpr char variable[] = "GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY";
	const char *directory = std::getenv(variable);
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Select the separate private placement corpus explicitly.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	std::ofstream stream(root / "native-theme-item-placement.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int selectorCount = 0;
	int worldCount = 0;
	int exhaustedPlacements = 0;
	int fullPools = 0;
	int signedOverwrites = 0;
	int generatedUniques = 0;
	int exhaustedHistories = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-theme-item-placement");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-world-item-creation");
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("selectors");
		stream << '[';
		constexpr uint32_t seeds[] = { 0, 1, 2588, 743271966, UINT32_C(0x80000000), UINT32_MAX, 1457187811, 988045466 };
		for (int depth = 1; depth <= 4; ++depth) {
			currentLevelNumber() = depth;
			for (uint32_t seed : seeds) {
				for (int lane = 0; lane < 7; ++lane) {
					const bool good = lane == 1;
					const int type = lane <= 1 ? -1 : lane == 2 ? static_cast<int>(ItemType::Gold)
					    : lane == 3 || lane == 5                ? static_cast<int>(ItemType::Misc)
					    : lane == 4                             ? static_cast<int>(ItemType::Staff)
					                                            : static_cast<int>(ItemType::Sword);
					const int misc = lane == 3 ? IMISC_BOOK : lane == 5 ? IMISC_HEAL
					                                                    : -1;
					SetRndSeed(seed);
					const int selected = SelectThemeItemForTesting(good, type, misc);
					if (selectorCount++ != 0) stream << ',';
					JsonObject value(stream);
					value.Number("depth", depth);
					value.Number("seed", seed);
					value.Boolean("onlygood", good);
					value.Number("itemType", type);
					value.Number("miscId", misc);
					value.Number("selectedIndex", selected);
					value.Number("randomState", GetLCGEngineState());
				}
			}
		}
		stream << ']';
		document.Key("worldCases");
		stream << '[';
		for (int depth = 1; depth <= 4; ++depth) {
			currentLevelNumber() = depth;
			uint32_t uniqueSeed = 0;
			for (uint32_t seed = 1; seed <= 4096; ++seed) {
				PrepareWorld();
				SetRndSeed(seed);
				CreateTypeItem({ 50, 50 }, true, ItemType::Sword, -1, false, true);
				if (Items[ActiveItems[2]]._iMagical != ITEM_QUALITY_UNIQUE) continue;
				uniqueSeed = seed;
				break;
			}
			ASSERT_NE(uniqueSeed, 0U) << "Bounded unique-world seed discovery must cover this depth.";
			for (int lane = 0; lane < 10; ++lane) {
				constexpr std::string_view names[] = { "random-open", "random-good-objects", "typed-gold-fallback",
					"typed-book-signed", "typed-staff-last-slot", "random-full-pool", "typed-gold-no-space",
					"typed-heal-edge", "typed-sword-unique", "typed-sword-exhausted" };
				PrepareWorld(lane == 4 ? MAXITEMS - 1 : lane == 5 ? MAXITEMS
				                                                  : 2,
				    lane != 4);
				const bool random = lane == 0 || lane == 1 || lane == 5;
				const bool good = lane == 1 || lane >= 8;
				const bool delta = lane != 0 && lane != 4;
				const int type = random ? -1 : lane == 2 || lane == 6 ? static_cast<int>(ItemType::Gold)
				    : lane == 3 || lane == 7                          ? static_cast<int>(ItemType::Misc)
				    : lane == 4                                       ? static_cast<int>(ItemType::Staff)
				                                                      : static_cast<int>(ItemType::Sword);
				const int misc = lane == 3 ? IMISC_BOOK : lane == 7 ? IMISC_HEAL
				                                                    : -1;
				const uint32_t seed = lane >= 8 ? uniqueSeed : lane == 3 ? 988045466
				    : lane == 1                                          ? 743271966
				    : lane == 2 || lane == 7                             ? 1
				                                                         : 2588;
				const Point p = lane == 7 ? Point { 0, 0 } : Point { 50, 50 };
				if (lane == 0) MyPlayer->pLvlLoad = 0;
				if (lane == 1) {
					for (int y = 49; y <= 51; ++y)
						for (int x = 49; x <= 51; ++x)
							tileAt({ x, y }).setPiece(1);
					tileAt({ 49, 49 }).setPiece(0);
					tileAt({ 49, 49 }).setObject(-1);
					tileAt({ 49, 49 }).addFlags(DungeonFlag::Populated);
					Objects[1]._oSolidFlag = true;
					tileAt({ 50, 49 }).setObject(-2);
					Objects[2].selectionRegion = SelectionRegion::Bottom;
					tileAt({ 51, 51 }).setObject(-3);
					tileAt({ 49, 50 }).setPlayer(-1);
					tileAt({ 49, 51 }).setMonster(-2);
				}
				if (lane == 2)
					for (int y = 49; y <= 51; ++y)
						for (int x = 49; x <= 51; ++x)
							tileAt({ x, y }).setPiece(1);
				if (lane == 3) {
					tileAt({ 49, 49 }).setPiece(1);
					tileAt({ 49, 49 }).setItem(-3);
				}
				if (lane == 6)
					for (Tile &tile : tiles())
						tile.setPiece(1);
				if (lane == 9) std::fill(std::begin(UniqueItemFlags), std::end(UniqueItemFlags), true);
				SetRndSeed(seed);
				const auto before = CaptureItemWorld();
				const int selected = SelectThemeItemForTesting(good, type, misc);
				const uint32_t selectionRandom = GetLCGEngineState();
				SetRndSeed(seed);
				if (worldCount++ != 0) stream << ',';
				JsonObject value(stream);
				value.String("name", names[lane]);
				value.Number("depth", depth);
				value.Number("seed", seed);
				value.Boolean("onlygood", good);
				value.Boolean("delta", delta);
				value.Boolean("levelLoading", MyPlayer->pLvlLoad != 0);
				value.Number("itemType", type);
				value.Number("miscId", misc);
				value.Number("x", p.x);
				value.Number("y", p.y);
				value.Number("selectedIndex", selected);
				value.Number("selectionRandomState", selectionRandom);
				value.Key("input");
				WriteItemWorld(stream, before, true);
				if (random)
					CreateRndItem(p, good, false, delta);
				else
					CreateTypeItem(p, good, static_cast<ItemType>(type), misc, false, delta);
				const auto after = CaptureItemWorld();
				value.Key("expected");
				WriteItemWorld(stream, after, false);
				CheckUntouchedWorld(before);
				if (lane == 5) {
					++fullPools;
					EXPECT_EQ(after.randomState, selectionRandom);
					EXPECT_EQ(after.activeCount, MAXITEMS);
				} else {
					EXPECT_EQ(after.activeCount, before.activeCount + 1);
					const int slot = before.active[before.activeCount];
					const Item &item = after.items[slot];
					if (lane == 1) {
						EXPECT_EQ(item.position.x, 49);
						EXPECT_EQ(item.position.y, 49);
					}
					if (lane == 2) {
						EXPECT_EQ(item.position.x, 48);
						EXPECT_EQ(item.position.y, 48);
					}
					if (lane == 3) {
						++signedOverwrites;
						EXPECT_EQ(item.position.x, 49);
						EXPECT_EQ(item.position.y, 49);
						EXPECT_EQ(tileAt({ 49, 49 }).item(), slot + 1);
					}
					if (lane == 4) EXPECT_EQ(tileAt(item.position).item(), 127);
					if (lane == 6) {
						++exhaustedPlacements;
						EXPECT_EQ(item.position.x, 0);
						EXPECT_EQ(item.position.y, 0);
						EXPECT_EQ(tileAt(item.position).item(), 0);
					}
					if (lane == 8) {
						++generatedUniques;
						EXPECT_EQ(item._iMagical, ITEM_QUALITY_UNIQUE);
					}
					if (lane == 9) {
						++exhaustedHistories;
						EXPECT_NE(item._iMagical, ITEM_QUALITY_UNIQUE);
					}
				}
			}
		}
		stream << ']';
		document.Number("selectorCount", selectorCount);
		document.Number("worldCount", worldCount);
		document.Number("fullPools", fullPools);
		document.Number("exhaustedPlacements", exhaustedPlacements);
		document.Number("signedOverwrites", signedOverwrites);
		document.Number("generatedUniques", generatedUniques);
		document.Number("exhaustedHistories", exhaustedHistories);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(selectorCount, 224);
	EXPECT_EQ(worldCount, 40);
	EXPECT_EQ(fullPools, 4);
	EXPECT_EQ(exhaustedPlacements, 4);
	EXPECT_EQ(signedOverwrites, 4);
	EXPECT_EQ(generatedUniques, 4);
	EXPECT_EQ(exhaustedHistories, 4);
	EXPECT_LE(std::filesystem::file_size(root / "native-theme-item-placement.json"), 32U * 1024U * 1024U);
}

// Static theme-object lighting is an independent zero-offset native dependency.
// Preserve the manager's private generated tables and indexed slots as well as
// every tile channel. The algorithm needs no graphics/MPQs; the inherited raw
// item fixture still checks the bundled test font and TSV resources.
struct NativeStaticLightGuard {
	LightManager oldManager = CurrentLightManager;
	bool oldLoading = LoadingMapObjects;
	~NativeStaticLightGuard()
	{
		CurrentLightManager = oldManager;
		LoadingMapObjects = oldLoading;
	}
};

class ThemeStaticLightingTest : public ThemeItemGenerationTest {
protected:
	void SetUp() override
	{
		ThemeItemGenerationTest::SetUp();
		if (HasFatalFailure()) return;
		worldGuard = std::make_unique<NativeItemWorldGuard>();
		lightGuard = std::make_unique<NativeStaticLightGuard>();
		CurrentLightManager.MakeLightTable();
	}

	void PrepareLighting(bool loading)
	{
		LoadingMapObjects = loading;
		for (Tile &tile : tiles()) {
			tile.setLight(15);
			tile.setPreLight(15);
		}
		// Independent sentinels prove min-only lighting versus DoUnLight's
		// unconditional copy, and retain brighter/darker pre-existing state.
		tileAt(52, 52).setLight(1);
		tileAt(53, 53).setPreLight(2);
		tileAt(57, 57).setLight(1);
	}

	static void WriteLightChannels(std::ostream &out)
	{
		JsonObject json(out);
		json.Key("light");
		WriteArray(out, tiles(), [&](const Tile &tile) { out << static_cast<int>(tile.light()); });
		json.Key("preLight");
		WriteArray(out, tiles(), [&](const Tile &tile) { out << static_cast<int>(tile.preLight()); });
	}

private:
	std::unique_ptr<NativeItemWorldGuard> worldGuard;
	std::unique_ptr<NativeStaticLightGuard> lightGuard;
};

TEST_F(ThemeStaticLightingTest, RadiusFivePreservesNativeMinimumAndLoadingCopy)
{
	PrepareLighting(false);
	const uint32_t seed = GetLCGEngineState();
	CurrentLightManager.DoLighting({ 50, 50 }, 5, {});
	EXPECT_EQ(tileAt(50, 50).light(), 0);
	EXPECT_EQ(tileAt(51, 51).light(), 3);
	EXPECT_EQ(tileAt(52, 52).light(), 1);
	EXPECT_EQ(tileAt(50, 50).preLight(), 15);
	EXPECT_EQ(GetLCGEngineState(), seed);
	PrepareLighting(true);
	CurrentLightManager.DoLighting({ 50, 50 }, 5, {});
	CurrentLightManager.DoUnLight({ 50, 50 }, 5);
	EXPECT_EQ(tileAt(51, 51).preLight(), 3);
	EXPECT_EQ(tileAt(57, 57).light(), 15);
	EXPECT_EQ(tileAt(53, 53).light(), 2);
	EXPECT_EQ(GetLCGEngineState(), seed);
}

TEST_F(ThemeStaticLightingTest, CaptureZeroOffsetNativeStaticLighting)
{
	const char *directory = std::getenv("GODEVILUTION_THEME_STATIC_LIGHT_ORACLE_DIRECTORY");
	if (directory == nullptr || directory[0] == '\0')
		GTEST_SKIP() << "Set the separate static-light oracle directory for private capture.";
	const std::filesystem::path root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	const std::filesystem::path repository = std::filesystem::weakly_canonical(
	    std::filesystem::absolute(__FILE__).parent_path().parent_path());
	ASSERT_FALSE(std::mismatch(repository.begin(), repository.end(), root.begin(), root.end()).first == repository.end())
	    << "Derived native evidence must remain outside the repository.";
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	std::ofstream stream(root / "native-theme-static-light.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-theme-static-light");
		document.Number("version", 1);
		document.String("policy", "original-diablo-cathedral-zero-offset-static-light");
		document.Key("cases");
		stream << '[';
		auto capture = [&](Point position, int radius, bool loading) {
			PrepareLighting(loading);
			if (caseCount++ != 0) stream << ',';
			JsonObject row(stream);
			row.Number("x", position.x);
			row.Number("y", position.y);
			row.Number("radius", radius);
			row.Boolean("loadingMapObjects", loading);
			row.Key("input");
			WriteLightChannels(stream);
			const uint32_t seed = GetLCGEngineState();
			const int activeCount = CurrentLightManager.activeLightCount_;
			const auto activeIds = CurrentLightManager.activeLights_;
			CurrentLightManager.DoLighting(position, static_cast<uint8_t>(radius), {});
			if (loading) CurrentLightManager.DoUnLight(position, static_cast<uint8_t>(radius));
			EXPECT_EQ(GetLCGEngineState(), seed);
			EXPECT_EQ(CurrentLightManager.activeLightCount_, activeCount);
			EXPECT_EQ(CurrentLightManager.activeLights_, activeIds);
			row.Key("expected");
			WriteLightChannels(stream);
		};
		for (int radius = 0; radius < 16; ++radius)
			for (bool loading : { false, true })
				capture({ 50, 50 }, radius, loading);
		for (Point position : { Point { 0, 0 }, Point { 111, 0 }, Point { 0, 111 }, Point { 111, 111 } })
			for (bool loading : { false, true })
				capture(position, 5, loading);
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 40);
	EXPECT_LE(std::filesystem::file_size(root / "native-theme-static-light.json"), 8U * 1024U * 1024U);
}

void WriteRoomPoint(std::ostream &out, Point value)
{
	JsonObject point(out);
	point.Number("x", value.x);
	point.Number("y", value.y);
}

void WriteRoomObject(std::ostream &out, int slot, const Object &value)
{
	JsonObject json(out);
	json.Number("slot", slot);
#define ROOM_OBJECT_N(key, field) json.Number(key, value.field)
#define ROOM_OBJECT_B(key, field) json.Boolean(key, value.field)
	ROOM_OBJECT_N("type", _otype);
	json.Key("position");
	WriteRoomPoint(out, value.position);
	ROOM_OBJECT_B("applyLighting", applyLighting);
	ROOM_OBJECT_N("animFlag", _oAnimFlag);
	ROOM_OBJECT_N("animDelay", _oAnimDelay);
	ROOM_OBJECT_N("animCount", _oAnimCnt);
	ROOM_OBJECT_N("animLength", _oAnimLen);
	ROOM_OBJECT_N("animFrame", _oAnimFrame);
	ROOM_OBJECT_N("animWidth", _oAnimWidth);
	ROOM_OBJECT_B("deleteFlag", _oDelFlag);
	ROOM_OBJECT_N("breakFlag", _oBreak);
	ROOM_OBJECT_B("solidFlag", _oSolidFlag);
	ROOM_OBJECT_B("missilePassable", _oMissFlag);
	ROOM_OBJECT_N("selectionRegion", selectionRegion);
	ROOM_OBJECT_B("preFlag", _oPreFlag);
	ROOM_OBJECT_B("trapFlag", _oTrapFlag);
	ROOM_OBJECT_B("doorFlag", _oDoorFlag);
	ROOM_OBJECT_N("lightId", _olid);
	ROOM_OBJECT_N("rndSeed", _oRndSeed);
	ROOM_OBJECT_N("var1", _oVar1);
	ROOM_OBJECT_N("var2", _oVar2);
	ROOM_OBJECT_N("var3", _oVar3);
	ROOM_OBJECT_N("var4", _oVar4);
	ROOM_OBJECT_N("var5", _oVar5);
	ROOM_OBJECT_N("var6", _oVar6);
	ROOM_OBJECT_N("bookMessage", bookMessage);
	ROOM_OBJECT_N("var8", _oVar8);
#undef ROOM_OBJECT_N
#undef ROOM_OBJECT_B
}

// SyncOpObject dispatches straight to native OperateChest without table/registry setup.
// Characterize actual native chest calls against complete item pools, including
// retained inactive slots and the ambient RNG. Expected rows are comparison-only.
TEST_F(ThemeItemPlacementTest, CaptureNativeChestOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_CHEST_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the chest operation capture directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode;
		~ExecutionGuard()
		{
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} executionGuard;
	gbSndInited = false;
	HeadlessMode = true;
	Player remotePlayer; // Avoid the local-player network notification.
	std::ofstream stream(root / "native-chest-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-chest-operations");
		document.Number("version", 1);
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("cases");
		stream << '[';
		constexpr std::string_view names[] = { "zero-content", "ordinary-open", "full-pool", "partial-pool", "local-fallback", "signed-occupancy" };
		constexpr uint32_t seeds[] = { 0, 1, 2588, UINT32_C(0x80000000), UINT32_MAX, 743271966 };
		for (int type = OBJ_CHEST1; type <= OBJ_CHEST3; ++type) {
			for (int branch = 0; branch < 2; ++branch) {
				for (int lane = 0; lane < 6; ++lane) {
					PrepareWorld(lane == 2 ? MAXITEMS : lane == 3 ? MAXITEMS - 1
					                                              : 2,
					    lane != 3);
					MyPlayer->pLvlLoad = lane == 1 ? 0 : 1; // Live drop animation and loader placement are distinct inputs.
					Object &chest = Objects[0];
					chest._otype = static_cast<_object_id>(type);
					chest.position = { 50, 50 };
					chest._oSolidFlag = true;
					chest._oMissFlag = true;
					chest.selectionRegion = SelectionRegion::Bottom;
					chest._oAnimFrame = 1;
					chest._oAnimLen = 3;
					chest._oAnimDelay = 1;
					chest._oAnimWidth = 96;
					chest._oRndSeed = seeds[lane];
					chest._oVar1 = lane == 0 ? 0 : type - OBJ_CHEST1 + 1;
					chest._oVar2 = branch;
					tileAt(chest.position).setObject(1);
					if (lane == 4)
						for (int y = 49; y <= 51; ++y)
							for (int x = 49; x <= 51; ++x)
								tileAt(x, y).setPiece(1);
					if (lane == 5) {
						tileAt(49, 49).setItem(-3);
						tileAt(49, 50).setPlayer(-1);
						tileAt(50, 49).setMonster(-1);
						tileAt(51, 51).setObject(-2);
						Objects[1].selectionRegion = SelectionRegion::Bottom;
					}
					SetRndSeed(UINT32_C(0x12345678));
					const auto before = CaptureItemWorld();
					if (caseCount++ != 0) stream << ',';
					JsonObject row(stream);
					row.String("name", names[lane]);
					row.Number("depth", 1);
					row.Number("slot", 0);
					row.Boolean("levelLoading", MyPlayer->pLvlLoad != 0);
					row.Key("inputChest");
					WriteRoomObject(stream, 0, chest);
					row.Key("input");
					WriteItemWorld(stream, before, true);
					SyncOpObject(remotePlayer, CMD_OPERATEOBJ, chest);
					const auto after = CaptureItemWorld();
					row.Key("expectedChest");
					WriteRoomObject(stream, 0, chest);
					row.Key("expected");
					WriteItemWorld(stream, after, false);
					EXPECT_EQ(chest.selectionRegion, SelectionRegion::None);
					EXPECT_EQ(chest._oAnimFrame, 3U);
					EXPECT_TRUE(chest._oSolidFlag);
					EXPECT_TRUE(chest._oMissFlag);
					EXPECT_FALSE(chest._oTrapFlag);
					EXPECT_EQ(chest._oRndSeed, seeds[lane]);
					EXPECT_EQ(chest._oVar2, branch);
					EXPECT_EQ(after.activeCount, before.activeCount + std::min(chest._oVar1, MAXITEMS - before.activeCount));
					EXPECT_EQ(after.active, before.active);
					if (lane == 0 || (lane == 2 && branch == 0)) EXPECT_EQ(after.randomState, seeds[lane]);
					SyncOpObject(remotePlayer, CMD_OPERATEOBJ, chest);
					const auto repeated = CaptureItemWorld();
					row.Key("repeatedChest");
					WriteRoomObject(stream, 0, chest);
					row.Key("repeated");
					WriteItemWorld(stream, repeated, false);
					std::ostringstream first, second;
					WriteItemWorld(first, after, false);
					WriteItemWorld(second, repeated, false);
					EXPECT_EQ(first.str(), second.str());
					EXPECT_EQ(chest._oAnimFrame, 3U);
					for (int slot = 1; slot < MAXOBJECTS; ++slot) {
						EXPECT_EQ(Objects[slot]._oSolidFlag, before.objectSolid[slot]);
						EXPECT_EQ(static_cast<int>(Objects[slot].selectionRegion), before.objectSelection[slot]);
					}
					int index = 0;
					for (const Tile &tile : tiles()) {
						const Tile &pre = before.worldTiles[index++];
						EXPECT_EQ(tile.piece(), pre.piece());
						EXPECT_EQ(tile.flags(), pre.flags());
						EXPECT_EQ(tile.player(), pre.player());
						EXPECT_EQ(tile.monster(), pre.monster());
						EXPECT_EQ(tile.object(), pre.object());
						EXPECT_EQ(tile.corpse(), pre.corpse());
						EXPECT_EQ(tile.light(), pre.light());
						EXPECT_EQ(tile.preLight(), pre.preLight());
						EXPECT_EQ(tile.transVal(), pre.transVal());
						EXPECT_EQ(tile.special(), pre.special());
					}
				}
			}
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 36);
	EXPECT_LE(std::filesystem::file_size(root / "native-chest-operations.json"), 32U * 1024U * 1024U);
}

void WriteRoomMonster(std::ostream &out, int slot, const Monster &value)
{
	JsonObject json(out);
	json.Number("slot", slot);
	json.Number("type", value.type().type);
#define ROOM_MONSTER_N(key, field) json.Number(key, value.field)
#define ROOM_MONSTER_P(key, field) \
	json.Key(key);                 \
	WriteRoomPoint(out, { value.field.x, value.field.y })
	ROOM_MONSTER_N("levelType", levelType);
	ROOM_MONSTER_N("mode", mode);
	ROOM_MONSTER_N("goal", goal);
	ROOM_MONSTER_N("goalVar1", goalVar1);
	ROOM_MONSTER_N("goalVar2", goalVar2);
	ROOM_MONSTER_N("goalVar3", goalVar3);
	ROOM_MONSTER_N("var1", var1);
	ROOM_MONSTER_N("var2", var2);
	ROOM_MONSTER_N("var3", var3);
	ROOM_MONSTER_N("pathCount", pathCount);
	ROOM_MONSTER_P("tile", position.tile);
	ROOM_MONSTER_P("future", position.future);
	ROOM_MONSTER_P("old", position.old);
	ROOM_MONSTER_P("last", position.last);
	ROOM_MONSTER_P("temp", position.temp);
	ROOM_MONSTER_N("direction", direction);
	ROOM_MONSTER_N("enemy", enemy);
	ROOM_MONSTER_P("enemyPosition", enemyPosition);
	json.Boolean("invalid", value.isInvalid);
	ROOM_MONSTER_N("ai", ai);
	ROOM_MONSTER_N("intelligence", intelligence);
	ROOM_MONSTER_N("activeForTicks", activeForTicks);
	ROOM_MONSTER_N("uniqueType", uniqueType);
	ROOM_MONSTER_N("uniqueTranslation", uniqTrans);
	ROOM_MONSTER_N("corpseId", corpseId);
	ROOM_MONSTER_N("whoHit", whoHit);
	ROOM_MONSTER_N("minDamage", minDamage);
	ROOM_MONSTER_N("maxDamage", maxDamage);
	ROOM_MONSTER_N("minDamageSpecial", minDamageSpecial);
	ROOM_MONSTER_N("maxDamageSpecial", maxDamageSpecial);
	ROOM_MONSTER_N("armorClass", armorClass);
	ROOM_MONSTER_N("resistance", resistance);
	ROOM_MONSTER_N("talkMessage", talkMsg);
	ROOM_MONSTER_N("leader", leader);
	ROOM_MONSTER_N("leaderRelation", leaderRelation);
	ROOM_MONSTER_N("packSize", packSize);
	ROOM_MONSTER_N("flags", flags);
	ROOM_MONSTER_N("rndItemSeed", rndItemSeed);
	ROOM_MONSTER_N("aiSeed", aiSeed);
	ROOM_MONSTER_N("lightId", lightId);
	ROOM_MONSTER_N("hitPoints", hitPoints);
	ROOM_MONSTER_N("maxHitPoints", maxHitPoints);
	ROOM_MONSTER_N("animTicksPerFrame", animInfo.ticksPerFrame);
	ROOM_MONSTER_N("animTickCounter", animInfo.tickCounterOfCurrentFrame);
	ROOM_MONSTER_N("animNumberOfFrames", animInfo.numberOfFrames);
	ROOM_MONSTER_N("animCurrentFrame", animInfo.currentFrame);
	ROOM_MONSTER_N("golemToHit", golemToHit);
	ROOM_MONSTER_N("reducePlayerStrength", reducePlayerStrength);
	ROOM_MONSTER_N("reducePlayerMagic", reducePlayerMagic);
	ROOM_MONSTER_N("reducePlayerDexterity", reducePlayerDexterity);
	ROOM_MONSTER_N("reducePlayerVitality", reducePlayerVitality);
	ROOM_MONSTER_N("reducePlayerMaxHP", reducePlayerMaxHP);
	ROOM_MONSTER_N("reducePlayerMaxMana", reducePlayerMaxMana);
#undef ROOM_MONSTER_N
#undef ROOM_MONSTER_P
}

void WriteRoomWorld(std::ostream &out)
{
	JsonObject json(out);
	json.Number("randomState", GetLCGEngineState());
	json.Number("activeObjectCount", ActiveObjectCount);
	json.Key("activeObjectIds");
	WriteArray(out, ObjectPoolAdapter::ActiveObjectIds(), [&](int id) { out << id; });
	json.Key("availableObjectIds");
	WriteArray(out, ObjectPoolAdapter::AvailableObjectIds(), [&](int id) { out << id; });
	json.Key("objects");
	out << '[';
	for (int slot = 0; slot < MAXOBJECTS; ++slot) {
		if (slot) out << ',';
		WriteRoomObject(out, slot, Objects[slot]);
	}
	out << ']';
	json.Number("activeMonsterCount", ActiveMonsterCount);
	json.Number("monsterTargetCount", MonsterPopulationTargetForTesting());
	json.Key("activeMonsterIds");
	WriteArray(out, MonsterPoolAdapter::ActiveMonsterIds(), [&](unsigned id) { out << id; });
	json.Key("monsters");
	out << '[';
	for (int slot = 0; slot < MaxMonsters; ++slot) {
		if (slot) out << ',';
		WriteRoomMonster(out, slot, Monsters[slot]);
	}
	out << ']';
	json.Key("monsterPetrified");
	WriteArray(out, MonsterPoolAdapter::AllMonsters(), [&](const Monster &m) { out << (m.animInfo.isPetrified ? "true" : "false"); });
	json.Number("activeItemCount", ActiveItemCount);
	json.Key("activeItemIds");
	WriteArray(out, ItemPoolAdapter::ActiveItemIds(), [&](uint8_t id) { out << static_cast<int>(id); });
	json.Key("items");
	out << '[';
	for (int slot = 0; slot <= MAXITEMS; ++slot) {
		if (slot) out << ',';
		WriteItem(out, Items[slot]);
	}
	out << ']';
	json.Key("uniqueFlags");
	WriteFlags(out, CaptureFlags());
	json.Key("tiles");
	WriteArray(out, tiles(), [&](const Tile &tile) {
		out << '[' << tile.piece() << ',' << static_cast<int>(tile.special()) << ',' << static_cast<int>(tile.transVal()) << ','
		    << static_cast<int>(tile.flags()) << ',' << static_cast<int>(tile.corpse()) << ',' << static_cast<int>(tile.player()) << ','
		    << tile.monster() << ',' << static_cast<int>(tile.object()) << ',' << static_cast<int>(tile.item()) << ','
		    << static_cast<int>(tile.light()) << ',' << static_cast<int>(tile.preLight()) << ']';
	});
	json.Key("sol");
	WriteArray(out, tileProperties(), [&](TileProperties property) { out << static_cast<int>(property); });
	const auto scratch = CaptureThemeRoomGlobalsForTesting();
	json.Number("themeX", scratch.x);
	json.Number("themeY", scratch.y);
	json.Number("themeVar1", scratch.variant);
	json.Key("flags");
	{
		JsonObject flags(out);
		flags.Boolean("armorAvailable", armorFlag());
		flags.Boolean("weaponAvailable", weaponFlag());
		flags.Boolean("treasureAvailable", scratch.treasure);
		flags.Boolean("bloodFountainAvailable", scratch.bloodFountain);
		flags.Boolean("purifyingFountainAvailable", scratch.purifyingFountain);
		flags.Boolean("murkyFountainAvailable", scratch.murkyFountain);
		flags.Boolean("tearFountainAvailable", scratch.tearFountain);
		flags.Boolean("cauldronAvailable", scratch.cauldron);
	}
	json.Key("lightAllocator");
	{
		JsonObject lights(out);
		lights.Number("activeCount", CurrentLightManager.activeLightCount_);
		lights.Boolean("updateLighting", CurrentLightManager.updateLighting_);
#ifdef _DEBUG
		lights.Boolean("disableLighting", CurrentLightManager.disableLighting_);
#else
		lights.Boolean("disableLighting", false);
#endif
		lights.Key("activeIds");
		WriteArray(out, CurrentLightManager.activeLights_, [&](uint8_t id) { out << static_cast<int>(id); });
		lights.Key("records");
		int id = 0;
		WriteArray(out, CurrentLightManager.lights_, [&](const Light &light) {
			JsonObject row(out);
			row.Number("lightId", id++);
			row.Key("tile");
			WriteRoomPoint(out, { light.position.tile.x, light.position.tile.y });
			row.Number("offsetX", light.position.offset.deltaX);
			row.Number("offsetY", light.position.offset.deltaY);
			row.Key("oldTile");
			WriteRoomPoint(out, { light.position.old.x, light.position.old.y });
			row.Number("radius", light.radius);
			row.Number("oldRadius", light.oldRadius);
			row.Boolean("isInvalid", light.isInvalid);
			row.Boolean("hasChanged", light.hasChanged);
		});
	}
}

// The dedicated runner starts with empty table caches and bestiary. Own public
// tables and every touched gameplay slot; generated loader caches are private
// to this test process and never become managed production inputs.
struct NativeThemeRoomGuard {
	std::array<Monster, MaxMonsters> monsters;
	std::array<unsigned, MaxMonsters> activeMonsters;
	size_t monsterCount = ActiveMonsterCount;
	std::array<int, MAXOBJECTS> activeObjects;
	std::array<int, MAXOBJECTS> availableObjects;
	int objectCount = ActiveObjectCount;
	std::array<ThemeStruct, MAXTHEMES> oldThemes;
	int oldThemeCount = numThemes();
	bool oldArmor = armorFlag(), oldWeapon = weaponFlag();
	ThemeRoomGlobalsForTesting oldScratch = CaptureThemeRoomGlobalsForTesting();
	std::array<CMonster, MaxLvlMTypes> oldBestiary;
	size_t oldBestiarySize = LevelBestiary.size();
	std::vector<ObjectData> oldObjectData = AllObjects;
	std::vector<std::string> oldObjectFiles = ObjMasterLoadList;
	std::vector<MonsterData> oldMonsterData = MonstersData;
	std::vector<UniqueMonsterData> oldUniques = UniqueMonstersData;
	decltype(AiRegistry) oldAi = AiRegistry;
	std::array<Quest, MAXQUESTS> oldQuests;
	bool oldSound = gbSndInited, oldHeadless = HeadlessMode;
	decltype(MyPlayerId) oldPlayerId = MyPlayerId;
	NativeStaticLightGuard lights;
	NativeThemeRoomGuard()
	{
		for (int i = 0; i < MaxMonsters; ++i) {
			monsters[i] = std::move(Monsters[i]);
			Monsters[i] = {};
		}
		std::copy_n(ActiveMonsters, MaxMonsters, activeMonsters.begin());
		std::copy_n(ActiveObjects, MAXOBJECTS, activeObjects.begin());
		std::copy_n(AvailableObjects, MAXOBJECTS, availableObjects.begin());
		std::copy_n(themes, MAXTHEMES, oldThemes.begin());
		std::copy_n(Quests, MAXQUESTS, oldQuests.begin());
		for (int i = 0; i < MaxLvlMTypes; ++i) {
			oldBestiary[i] = std::move(LevelBestiary.types[i]);
			LevelBestiary.types[i] = {};
		}
	}
	~NativeThemeRoomGuard()
	{
		LevelBestiary.init();
		for (int i = 0; i < MaxLvlMTypes; ++i)
			LevelBestiary.types[i] = std::move(oldBestiary[i]);
		LevelBestiary.restoreSizeForTesting(oldBestiarySize);
		for (int i = 0; i < MaxMonsters; ++i)
			Monsters[i] = std::move(monsters[i]);
		std::copy(activeMonsters.begin(), activeMonsters.end(), ActiveMonsters);
		ActiveMonsterCount = monsterCount;
		std::copy(activeObjects.begin(), activeObjects.end(), ActiveObjects);
		std::copy(availableObjects.begin(), availableObjects.end(), AvailableObjects);
		ActiveObjectCount = objectCount;
		std::copy(oldThemes.begin(), oldThemes.end(), themes);
		numThemes() = oldThemeCount;
		armorFlag() = oldArmor;
		weaponFlag() = oldWeapon;
		RestoreThemeRoomGlobalsForTesting(oldScratch);
		AllObjects = std::move(oldObjectData);
		ObjMasterLoadList = std::move(oldObjectFiles);
		MonstersData = std::move(oldMonsterData);
		UniqueMonstersData = std::move(oldUniques);
		AiRegistry = std::move(oldAi);
		std::copy(oldQuests.begin(), oldQuests.end(), Quests);
		gbSndInited = oldSound;
		HeadlessMode = oldHeadless;
		MyPlayerId = oldPlayerId;
	}
};

class ThemeRoomGenerationTest : public ThemeItemPlacementTest {
protected:
	void CapturePopulationOracle(const char *directory, bool freshLoadOrder, bool levelReady = false, bool nativeRogueRoute = false, std::optional<HeroClass> nativeClassRoute = std::nullopt);
	void CaptureNativeCatacombsPopulationOracle(const char *archivePath, const char *directory, bool captureStages = false);

	void SetUp() override
	{
		ThemeItemPlacementTest::SetUp();
		if (HasFatalFailure()) return;
		ASSERT_TRUE(LevelBestiary.empty()) << "A dedicated empty bestiary is required.";
		ASSERT_TRUE(AllObjects.empty());
		ASSERT_TRUE(MonstersData.empty());
		roomGuard = std::make_unique<NativeThemeRoomGuard>();
		HeadlessMode = true;
		gbSndInited = false;
		MyPlayerId = 0;
		for (const char *asset : { "txtdata\\objects\\objdat.tsv", "txtdata\\monsters\\monstdat.tsv",
		         "txtdata\\monsters\\unique_monstdat.tsv", "txtdata\\monsters\\monster_missiles.tsv" })
			ASSERT_TRUE(FindAsset(asset).ok()) << "A bundled theme metadata resource is missing.";
		LoadObjectData();
		LoadMonsterData();
		for (Quest &quest : Quests)
			quest._qactive = QUEST_NOTAVAIL;
		ASSERT_EQ(AllObjects.size(), 109U);
		for (const int type : { 109, 0, 8, 20, 34, 42 }) {
			const auto index = LevelBestiary.addType(static_cast<_monster_id>(type), type == 109 ? PLACE_SPECIAL : PLACE_SCATTER);
			ASSERT_TRUE(index.has_value());
		}
		CurrentLightManager.MakeLightTable();
	}

	void PrepareRoom(int depth, int itemCount = 2, int objectCount = 0, int monsterCount = 4, bool trapWall = true)
	{
		PrepareWorld(itemCount);
		currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATHEDRAL });
		CurrentLightManager.MakeLightTable();
		for (Tile &tile : tiles()) {
			tile.setPiece(1);
			tile.setLight(15);
			tile.setPreLight(15);
			tile.setCorpse(7);
			tile.setSpecial(11);
		}
		for (int y = 48; y <= 54; ++y)
			for (int x = 48; x <= 54; ++x) {
				tileAt(x, y).setPiece(0);
				tileAt(x, y).setTransVal(1);
				tileAt(x, y).addFlags(DungeonFlag::Populated);
			}
		tileProperties()[2] = TileProperties::Solid | TileProperties::Trap;
		if (trapWall) tileAt(50, 47).setPiece(2);
		ActiveObjectCount = objectCount;
		for (int i = 0; i < MAXOBJECTS; ++i) {
			ActiveObjects[i] = objectCount == MAXOBJECTS ? MAXOBJECTS - 1 - i : 0;
			AvailableObjects[i] = MAXOBJECTS - 1 - i;
			Objects[i] = {};
			Objects[i]._otype = OBJ_CANDLE1;
			Objects[i]._oVar1 = 91;
			Objects[i]._oVar2 = -17;
			Objects[i]._oVar3 = 23;
			Objects[i]._oVar4 = 31;
			Objects[i]._oVar5 = 41;
			Objects[i]._oVar6 = 53;
			Objects[i]._oVar8 = 67;
			Objects[i]._oRndSeed = 1000 + i;
			Objects[i]._olid = 13;
		}
		ActiveMonsterCount = monsterCount;
		for (int i = 0; i < MaxMonsters; ++i) {
			ActiveMonsters[i] = MaxMonsters - 1 - i;
			Monsters[i] = {};
			Monsters[i].position.tile = { 1, 0 };
			Monsters[i].position.future = { 1, 0 };
			Monsters[i].position.old = { 1, 0 };
			Monsters[i].position.last = { 13, 17 };
			Monsters[i].position.temp = { 19, 23 };
			Monsters[i].var1 = 29;
			Monsters[i].var2 = 31;
			Monsters[i].var3 = 37;
			Monsters[i].uniqTrans = 7;
			Monsters[i].packSize = 5;
			Monsters[i].corpseId = 3;
			Monsters[i].animInfo.isPetrified = true;
		}
		for (int i = 0; i < itemCount; ++i) {
			Items[ActiveItems[i]]._iAnimFlag = true;
			Items[ActiveItems[i]].selectionRegion = SelectionRegion::None;
			Items[ActiveItems[i]].animInfo.currentFrame = 0;
		}
		tileAt(50, 50).setPlayer(-1);
		tileAt(51, 50).setMonster(-1);
		MyPlayer->plractive = true;
		MyPlayer->plrlevel = static_cast<uint8_t>(depth);
		MyPlayer->plrIsOnSetLevel = false;
		MyPlayer->_pLvlChanging = false;
		MyPlayer->position.tile = { 60, 60 };
		MyPlayer->position.future = { 61, 60 };
		LoadingMapObjects = false;
		CurrentLightManager.Init();
		for (int id = 0; id < MAXLIGHTS; ++id) {
			CurrentLightManager.activeLights_[id] = MAXLIGHTS - 1 - id;
			CurrentLightManager.lights_[id] = {};
			CurrentLightManager.lights_[id].position.old = { 9, 7 };
			CurrentLightManager.lights_[id].oldRadius = 3;
		}
		CurrentLightManager.activeLightCount_ = 2;
		armorFlag() = true;
		weaponFlag() = true;
		RestoreThemeRoomGlobalsForTesting({ 29, 31, 2, true, false, true, false, true, false });
	}

	void WriteRoomCatalog(std::ostream &out)
	{
		JsonObject json(out);
		json.Key("objects");
		int id = 0;
		WriteArray(out, AllObjects, [&](const ObjectData &object) {
			JsonObject row(out);
			row.Number("nativeType", id++);
			row.Number("flags", object.flags);
			row.Number("animDelay", object.animDelay);
			row.Number("animLength", object.animLen);
			row.Number("animWidth", object.animWidth);
			row.Number("selectionRegion", object.selectionRegion);
		});
		json.Key("bestiary");
		WriteArray(out, LevelBestiary, [&](const CMonster &type) {
			const auto &data = type.data();
			JsonObject row(out);
			row.Number("nativeType", type.type);
			row.Boolean("isSkeleton", IsSkel(type.type));
			row.Boolean("isGoat", IsGoat(type.type));
			row.Number("placeFlags", type.placeFlags);
			row.Number("minimumHitPoints", data.hitPointsMinimum);
			row.Number("maximumHitPoints", data.hitPointsMaximum);
			row.Number("ai", data.ai);
			row.Number("intelligence", data.intelligence);
			row.Number("abilityFlags", data.abilityFlags);
#define ROOM_DATA(key, field) row.Number(key, data.field)
			ROOM_DATA("minDamage", minDamage);
			ROOM_DATA("maxDamage", maxDamage);
			ROOM_DATA("minDamageSpecial", minDamageSpecial);
			ROOM_DATA("maxDamageSpecial", maxDamageSpecial);
			ROOM_DATA("armorClass", armorClass);
			ROOM_DATA("resistance", resistance);
			ROOM_DATA("reducePlayerStrength", reducePlayerStrength);
			ROOM_DATA("reducePlayerMagic", reducePlayerMagic);
			ROOM_DATA("reducePlayerDexterity", reducePlayerDexterity);
			ROOM_DATA("reducePlayerVitality", reducePlayerVitality);
			ROOM_DATA("reducePlayerMaxHP", reducePlayerMaxHP);
			ROOM_DATA("reducePlayerMaxMana", reducePlayerMaxMana);
#undef ROOM_DATA
			row.Key("frames");
			WriteArray(out, type.anims, [&](const AnimStruct &anim) { out << static_cast<int>(anim.frames); });
			row.Key("rates");
			WriteArray(out, type.anims, [&](const AnimStruct &anim) { out << static_cast<int>(anim.rate); });
		});
		json.Key("shrineAvailable");
		WriteArray(out, SinglePlayerShrineAvailabilityForTesting(), [&](bool available) { out << (available ? "true" : "false"); });
	}

private:
	std::unique_ptr<NativeThemeRoomGuard> roomGuard;
};

void WriteChestMissiles(std::ostream &out, WorldTilePosition operationTarget);

TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsChestOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_CHEST_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0')
		GTEST_SKIP() << "Set the explicit Catacombs chest oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native evidence.";
	struct ExecutionGuard {
		bool sound = gbSndInited;
		bool headless = HeadlessMode;
		bool missilePreFlag = MissilePreFlag;
		std::list<Missile> savedMissiles = std::move(Missiles);
		ScopedMissileDataForTest missileData;
		~ExecutionGuard()
		{
			Missiles = std::move(savedMissiles);
			MissilePreFlag = missilePreFlag;
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} guard;
	HeadlessMode = true;
	gbSndInited = false;
	LoadMissileData();
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Arrow));
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::FireArrow));
	Player remotePlayer;
	remotePlayer.position.tile = { 46, 50 };
	std::ofstream stream(root / "native-catacombs-chest-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-chest-operations");
		document.Number("version", 1);
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		struct Lane {
			std::string name;
			int depth;
			int rawType;
			int itemCount;
			int branch;
			bool trapFlag;
			int trapKind;
			uint32_t seed;
			int activeItems;
			bool blockPlacement;
			bool linkedWallTrap;
		};
		std::vector<Lane> lanes;
		constexpr std::array<int, 6> rawTypes { 5, 6, 7, 68, 69, 70 };
		constexpr std::array<uint32_t, 6> seeds { 0, 1, 2588, UINT32_C(0x80000000), UINT32_MAX, 743271966 };
		for (int depth = 5; depth <= 8; ++depth) {
			for (size_t index = 0; index < rawTypes.size(); ++index) {
				const int rawType = rawTypes[index];
				const bool intrinsic = rawType >= 68;
				const bool linkedWallTrap = depth == 8 && rawType == 6;
				std::ostringstream name;
				name << "depth-" << depth << "-type-" << rawType;
				lanes.push_back({ name.str(), depth, rawType, 1, static_cast<int>(index % 2),
				    intrinsic || linkedWallTrap,
				    static_cast<int>((depth + static_cast<int>(index)) % 2),
				    seeds[(static_cast<size_t>(depth) + index) % seeds.size()],
				    2, false, linkedWallTrap });
			}
		}
		for (int rawType = 68; rawType <= 70; ++rawType) {
			std::ostringstream name;
			name << "disarmed-type-" << rawType;
			lanes.push_back({ name.str(), 5, rawType, 1, rawType % 2, false, 1, 2588, 2, false, false });
		}
		lanes.push_back({ "zero-count-zero-seed", 5, 5, 0, 0, false, 0, 0, 2, false, false });
		lanes.push_back({ "full-item-pool", 8, 7, 1, 1, false, 0, UINT32_C(0x80000000), MAXITEMS, false, false });
		lanes.push_back({ "blocked-item-placement", 6, 5, 1, 0, false, 0, 2588, 2, true, false });
		for (const Lane &lane : lanes) {
			PrepareRoom(lane.depth, lane.activeItems, lane.linkedWallTrap ? 2 : 1, 0, false);
			currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(lane.depth), .type = DTYPE_CATACOMBS });
			MyPlayer->plrlevel = static_cast<uint8_t>(lane.depth);
			MyPlayer->plrIsOnSetLevel = false;
			MyPlayer->_pLvlChanging = false;
			MyPlayer->pLvlLoad = 1;
			MyPlayer->position.tile = { 60, 60 };
			tileAt(50, 50).setPlayer(0);
			tileAt(50, 50).setMonster(0);
			if (lane.blockPlacement)
				std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::Solid);
			Object &chest = Objects[0];
			chest._otype = static_cast<_object_id>(lane.rawType);
			chest.position = { 50, 50 };
			chest._oSolidFlag = true;
			chest._oMissFlag = true;
			chest.selectionRegion = SelectionRegion::Bottom;
			chest._oAnimFrame = 1;
			chest._oAnimLen = 3;
			chest._oAnimDelay = 1;
			chest._oAnimWidth = 96;
			chest._oRndSeed = lane.seed;
			chest._oVar1 = lane.itemCount;
			chest._oVar2 = lane.branch;
			chest._oVar4 = lane.trapKind;
			chest._oTrapFlag = lane.trapFlag;
			tileAt(chest.position).setObject(1);
			if (lane.linkedWallTrap) {
				Object &wallTrap = Objects[1];
				wallTrap._otype = static_cast<_object_id>(53);
				wallTrap.position = { 50, 49 };
				wallTrap._oVar1 = 50;
				wallTrap._oVar2 = 50;
				wallTrap._oVar3 = static_cast<int>(MissileID::Arrow);
				wallTrap._oVar4 = 0;
				tileAt(wallTrap.position).setObject(2);
			}
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(UINT32_C(0x12345678));
			const NativeItemWorldState before = CaptureItemWorld();
			if (caseCount++ != 0) stream << ',';
			JsonObject row(stream);
			row.String("name", lane.name);
			row.Number("depth", lane.depth);
			row.Number("levelType", levelType());
			row.Number("rawType", lane.rawType);
			row.Number("itemCount", lane.itemCount);
			row.Number("branch", lane.branch);
			row.Boolean("trapFlag", lane.trapFlag);
			row.Number("trapKind", lane.trapKind);
			row.Number("seed", lane.seed);
			row.Number("remotePlayerX", remotePlayer.position.tile.x);
			row.Number("remotePlayerY", remotePlayer.position.tile.y);
			row.Number("trapDirectionArgument", GetDirection(chest.position, remotePlayer.position.tile));
			row.Boolean("blockPlacement", lane.blockPlacement);
			row.Boolean("linkedWallTrap", lane.linkedWallTrap);
			auto writeLinkedWallTrap = [&]() {
				if (lane.linkedWallTrap)
					WriteRoomObject(stream, 1, Objects[1]);
				else
					stream << "null";
			};
			row.Key("inputChest");
			WriteRoomObject(stream, 0, chest);
			row.Key("inputLinkedWallTrap");
			writeLinkedWallTrap();
			row.Key("inputActiveObjectIds");
			WriteArray(stream, ObjectPoolAdapter::ActiveObjectIds(), [&](int id) { stream << id; });
			row.Key("input");
			WriteItemWorld(stream, before, true);
			row.Key("inputMissiles");
			WriteChestMissiles(stream, remotePlayer.position.tile);
			row.Boolean("inputMissilePreFlag", MissilePreFlag);
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, chest);
			const NativeItemWorldState after = CaptureItemWorld();
			row.Key("expectedChest");
			WriteRoomObject(stream, 0, chest);
			row.Key("expectedLinkedWallTrap");
			writeLinkedWallTrap();
			row.Key("expected");
			WriteItemWorld(stream, after, false);
			row.Key("expectedMissiles");
			WriteChestMissiles(stream, remotePlayer.position.tile);
			row.Boolean("expectedMissilePreFlag", MissilePreFlag);
			EXPECT_EQ(chest.selectionRegion, SelectionRegion::None);
			EXPECT_EQ(chest._oAnimFrame, 3U);
			EXPECT_EQ(chest._oRndSeed, lane.seed);
			if (lane.rawType >= 68 && lane.trapFlag) {
				EXPECT_FALSE(chest._oTrapFlag);
				EXPECT_EQ(Missiles.size(), 1U);
				ASSERT_FALSE(Missiles.empty());
				const Missile &missile = Missiles.front();
				EXPECT_EQ(missile._mitype, lane.trapKind == 0 ? MissileID::Arrow : MissileID::FireArrow);

				EXPECT_EQ(missile.position.start.x, chest.position.x);
				EXPECT_EQ(missile.position.start.y, chest.position.y);
				EXPECT_EQ(missile.position.tile.x, chest.position.x);
				EXPECT_EQ(missile.position.tile.y, chest.position.y);
				EXPECT_TRUE(missile.position.velocity.deltaX != 0 || missile.position.velocity.deltaY != 0);
				EXPECT_EQ(missile._misource, -1);
				EXPECT_EQ(missile._micaster, TARGET_PLAYERS);
				EXPECT_EQ(missile._midam, 0);
			} else {
				EXPECT_EQ(chest._oTrapFlag, lane.trapFlag);
				EXPECT_TRUE(Missiles.empty());
			}
			if (lane.itemCount == 0)
				EXPECT_EQ(after.activeCount, before.activeCount);
			if (lane.activeItems == MAXITEMS)
				EXPECT_EQ(after.activeCount, before.activeCount);
			if (lane.blockPlacement)
				EXPECT_EQ(after.activeCount, before.activeCount + 1);
			std::ostringstream firstState, firstChest, firstMissiles;
			WriteItemWorld(firstState, after, false);
			WriteRoomObject(firstChest, 0, chest);
			WriteChestMissiles(firstMissiles, remotePlayer.position.tile);
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, chest);
			const NativeItemWorldState repeated = CaptureItemWorld();
			row.Key("repeatedChest");
			WriteRoomObject(stream, 0, chest);
			row.Key("repeatedLinkedWallTrap");
			writeLinkedWallTrap();
			row.Key("repeatedActiveObjectIds");
			WriteArray(stream, ObjectPoolAdapter::ActiveObjectIds(), [&](int id) { stream << id; });
			row.Key("repeated");
			WriteItemWorld(stream, repeated, false);
			row.Key("repeatedMissiles");
			WriteChestMissiles(stream, remotePlayer.position.tile);
			row.Boolean("repeatedMissilePreFlag", MissilePreFlag);
			std::ostringstream repeatedState, repeatedChest, repeatedMissiles;
			WriteItemWorld(repeatedState, repeated, false);
			WriteRoomObject(repeatedChest, 0, chest);
			WriteChestMissiles(repeatedMissiles, remotePlayer.position.tile);
			EXPECT_EQ(firstState.str(), repeatedState.str());
			EXPECT_EQ(firstChest.str(), repeatedChest.str());
			EXPECT_EQ(firstMissiles.str(), repeatedMissiles.str());
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 30);
	EXPECT_LE(std::filesystem::file_size(root / "native-catacombs-chest-operations.json"), 64U * 1024U * 1024U);
}

void WriteChestMissiles(std::ostream &out, WorldTilePosition operationTarget)
{
	// AddArrow stores velocity derived from AddMissile's destination, but does not persist that destination in the projectile union.
	out << '[';
	bool first = true;
	for (const Missile &missile : Missiles) {
		if (!first) out << ',';
		first = false;
		JsonObject row(out);
		row.Number("type", missile._mitype);
		row.Number("direction", missile.getDirection());
		row.Number("frameGroup", missile.getFrameGroupRaw());
		row.Number("startX", missile.position.start.x);
		row.Number("startY", missile.position.start.y);
		row.Number("tileX", missile.position.tile.x);
		row.Number("tileY", missile.position.tile.y);
		row.Number("velocityX", missile.position.velocity.deltaX);
		row.Number("velocityY", missile.position.velocity.deltaY);
		row.Number("destinationArgumentX", operationTarget.x);
		row.Number("destinationArgumentY", operationTarget.y);
		row.Number("source", missile._misource);
		row.Number("ownerKind", missile._micaster);
		row.Number("damage", missile._midam);
		row.Number("spellLevel", missile._mispllvl);
	}
	out << ']';
}

TEST_F(ThemeRoomGenerationTest, NativeThemeRoomGuardRestoresBestiarySizeAndEntries)
{
	const size_t originalSize = LevelBestiary.size();
	ASSERT_GT(originalSize, 0U);
	const _monster_id originalFirstType = LevelBestiary[0].type;
	{
		NativeThemeRoomGuard nestedGuard;
		LevelBestiary.init();
		EXPECT_TRUE(LevelBestiary.empty());
	}
	EXPECT_EQ(LevelBestiary.size(), originalSize);
	EXPECT_EQ(LevelBestiary[0].type, originalFirstType);
}

// Capture complete ordinary L1 sarcophagus operation state without asking the
// managed implementation to supply any expected native values.
TEST_F(ThemeRoomGenerationTest, CaptureNativeSarcophagusOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_SARCOPHAGUS_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the native sarcophagus capture directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode;
		~ExecutionGuard()
		{
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} executionGuard;
	gbSndInited = false;
	HeadlessMode = true;
	Player remotePlayer;
	std::ofstream stream(root / "native-sarcophagus-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-sarcophagus-operations");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-untrapped-cathedral-level1");
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		struct Lane {
			const char *name;
			int var1;
			uint32_t sarcophagusSeed;
			int itemCount;
			bool levelLoading;
			int spawnPattern; // 0 native default availability, 1 clear neighbors/occupied center, 2 three clear neighbors, 3 blocked.
		};
		constexpr Lane lanes[] = {
			{ "loot-var1-0", 0, 1, 2, false, 0 },
			{ "loot-var1-2-partial-pool", 2, 2588, MAXITEMS - 1, false, 0 },
			{ "loot-var1-2-full-pool", 2, UINT32_C(0x80000000), MAXITEMS, false, 0 },
			{ "empty-var1-3", 3, UINT32_MAX, 2, false, 0 },
			{ "empty-var1-7-loading", 7, 743271966, 2, true, 0 },
			{ "spawn-var1-8-default-neighbors", 8, 1, 2, false, 0 },
			{ "spawn-var1-9-default-neighbors", 9, 2588, 2, false, 0 },
			{ "spawn-var1-8-clear-neighbors-center-occupied", 8, 1, 2, false, 1 },
			{ "spawn-var1-9-negative-rng", 9, UINT32_C(1457187811), 2, false, 2 },
			{ "spawn-var1-8-no-space", 8, UINT32_C(0x80000000), 2, false, 3 },
		};
		constexpr std::array<Direction, 8> spawnDirections {
			Direction::North,
			Direction::NorthEast,
			Direction::East,
			Direction::NorthWest,
			Direction::SouthEast,
			Direction::West,
			Direction::SouthWest,
			Direction::South,
		};
		auto isSpawnTileAvailable = [](Point position) {
			return InDungeonBounds(position) && tileAt(position).player() == 0
			    && tileAt(position).monster() == 0 && IsTileWalkable(position);
		};
		auto writeAvailability = [&](Point center) {
			stream << '[' << (isSpawnTileAvailable(center) ? "true" : "false");
			for (Direction direction : spawnDirections)
				stream << ',' << (isSpawnTileAvailable(center + direction) ? "true" : "false");
			stream << ']';
		};
		auto writeNearbyTiles = [&](Point center) {
			stream << '[';
			std::array<Point, 9> positions { center };
			for (size_t i = 0; i < spawnDirections.size(); ++i)
				positions[i + 1] = center + spawnDirections[i];
			for (size_t i = 0; i < positions.size(); ++i) {
				if (i != 0) stream << ',';
				const Point position = positions[i];
				const Tile &tile = tileAt(position);
				JsonObject row(stream);
				row.Key("position");
				WriteRoomPoint(stream, position);
				row.Number("piece", tile.piece());
				row.Number("special", static_cast<int>(tile.special()));
				row.Number("transparency", static_cast<int>(tile.transVal()));
				row.Number("flags", static_cast<int>(tile.flags()));
				row.Number("corpse", static_cast<int>(tile.corpse()));
				row.Number("player", static_cast<int>(tile.player()));
				row.Number("monster", tile.monster());
				row.Number("object", static_cast<int>(tile.object()));
				row.Number("item", static_cast<int>(tile.item()));
				row.Number("light", static_cast<int>(tile.light()));
				row.Number("preLight", static_cast<int>(tile.preLight()));
				row.Number("tileProperties", static_cast<int>(tileProperties()[position.y * MAXDUNX + position.x]));
			}
			stream << ']';
		};
		auto writeActiveMonsters = [&]() {
			WriteArray(stream, MonsterPoolAdapter::ActiveMonsterIds(), [&](unsigned id) { stream << id; });
		};
		auto writeActiveObjects = [&]() {
			WriteArray(stream, ObjectPoolAdapter::ActiveObjectIds(), [&](int id) { stream << id; });
		};
		for (const Lane &lane : lanes) {
			PrepareRoom(1, lane.itemCount, 0, 0, false);
			MyPlayer->pLvlLoad = lane.levelLoading ? 1 : 0;
			MyPlayer->position.tile = { 40, 40 };
			MyPlayer->position.future = { 40, 40 };
			tileAt(50, 50).setPlayer(0);
			tileAt(51, 50).setMonster(0);
			tileAt(40, 40).setPlayer(-1);
			Object *sarcophagus = AddObjectPresentation(OBJ_SARC, { 50, 50 });
			ASSERT_NE(sarcophagus, nullptr);
			const int objectSlot = static_cast<int>(sarcophagus->GetId());
			ASSERT_GE(objectSlot, 0);
			ASSERT_LT(objectSlot, MAXOBJECTS);
			ASSERT_NE(sarcophagus->selectionRegion, SelectionRegion::None);
			tileAt({ 50, 49 }).setObject(-(objectSlot + 1));
			sarcophagus->_oRndSeed = lane.sarcophagusSeed;
			sarcophagus->_oVar1 = lane.var1;
			sarcophagus->_oVar2 = -1;
			Monster *linked = nullptr;
			if (lane.var1 >= 8) {
				linked = PreSpawnSkeleton();
				ASSERT_NE(linked, nullptr);
				sarcophagus->_oVar2 = static_cast<int>(linked->getId());
			}
			if (lane.spawnPattern != 0) {
				for (int y = 49; y <= 51; ++y)
					for (int x = 49; x <= 51; ++x)
						tileAt(x, y).setPlayer(1);
				if (lane.spawnPattern == 1) {
					for (int y = 49; y <= 51; ++y)
						for (int x = 49; x <= 51; ++x)
							if (x != 50 || y != 50) tileAt(x, y).setPlayer(0);
				} else if (lane.spawnPattern == 2) {
					tileAt(49, 49).setPlayer(0); // North
					tileAt(51, 49).setPlayer(0); // East
					tileAt(49, 50).setPlayer(0); // NorthWest
				} else if (lane.spawnPattern != 3) {
					FAIL() << "Unknown sarcophagus spawn pattern.";
				}
			}
			EXPECT_FALSE(isSpawnTileAvailable(sarcophagus->position));
			if (lane.spawnPattern == 2) {
				std::array<bool, 9> available {};
				available[0] = isSpawnTileAvailable(sarcophagus->position);
				for (size_t i = 0; i < spawnDirections.size(); ++i)
					available[i + 1] = isSpawnTileAvailable(sarcophagus->position + spawnDirections[i]);
				EXPECT_EQ(std::count(available.begin(), available.end(), true), 3);
				EXPECT_TRUE(available[1]); // North
				EXPECT_TRUE(available[3]); // East
				EXPECT_TRUE(available[4]); // NorthWest
			}
			SetRndSeed(UINT32_C(0x12345678));
			const NativeItemWorldState beforeItems = CaptureItemWorld();
			const size_t activeMonsterCountBefore = ActiveMonsterCount;
			const int linkedSlot = linked == nullptr ? -1 : static_cast<int>(linked->getId());
			const int specialGraphic = static_cast<int>(MonsterGraphic::Special);
			const int specialFrameCount = linked == nullptr ? 0 : linked->type().anims[specialGraphic].frames;
			const int specialTicksPerFrame = linked == nullptr ? 0 : linked->type().anims[specialGraphic].rate;
			if (caseCount++ != 0) stream << ',';
			JsonObject row(stream);
			row.String("name", lane.name);
			row.Number("depth", 1);
			row.Number("slot", objectSlot);
			row.Number("var1", lane.var1);
			row.Number("var2", sarcophagus->_oVar2);
			row.Number("sarcophagusSeed", sarcophagus->_oRndSeed);
			row.Boolean("levelLoading", lane.levelLoading);
			row.Number("linkedMonsterSlot", linkedSlot);
			row.Number("specialFrameCount", specialFrameCount);
			row.Number("specialTicksPerFrame", specialTicksPerFrame);
			row.Key("spawnTilesAvailable");
			writeAvailability(sarcophagus->position);
			row.Key("inputSarcophagus");
			WriteRoomObject(stream, objectSlot, *sarcophagus);
			row.Key("input");
			WriteItemWorld(stream, beforeItems, true);
			row.Number("inputActiveObjectCount", ActiveObjectCount);
			row.Key("inputActiveObjectIds");
			writeActiveObjects();
			row.Key("inputActiveMonsterIds");
			writeActiveMonsters();
			row.Key("inputLinkedMonster");
			if (linked != nullptr)
				WriteRoomMonster(stream, linkedSlot, *linked);
			else
				stream << "null";
			row.Boolean("inputLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, *sarcophagus);
			const NativeItemWorldState afterItems = CaptureItemWorld();
			EXPECT_EQ(sarcophagus->selectionRegion, SelectionRegion::None);
			EXPECT_TRUE(sarcophagus->_oAnimFlag);
			EXPECT_EQ(sarcophagus->_oAnimDelay, 3);
			EXPECT_EQ(sarcophagus->_oRndSeed, lane.sarcophagusSeed);
			EXPECT_EQ(ActiveMonsterCount, activeMonsterCountBefore);
			row.Number("expectedRandomState", GetLCGEngineState());
			row.Key("expectedSarcophagus");
			WriteRoomObject(stream, objectSlot, *sarcophagus);
			row.Key("expected");
			WriteItemWorld(stream, afterItems, false);
			row.Number("expectedActiveObjectCount", ActiveObjectCount);
			row.Key("expectedActiveObjectIds");
			writeActiveObjects();
			row.Key("expectedActiveMonsterIds");
			writeActiveMonsters();
			row.Key("expectedSpawnTiles");
			writeNearbyTiles(sarcophagus->position);
			row.Key("expectedLinkedMonster");
			if (linked != nullptr)
				WriteRoomMonster(stream, linkedSlot, *linked);
			else
				stream << "null";
			row.Boolean("expectedLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			std::ostringstream expectedState;
			WriteItemWorld(expectedState, afterItems, false);
			std::ostringstream expectedObject;
			WriteRoomObject(expectedObject, objectSlot, *sarcophagus);
			std::ostringstream expectedActor;
			if (linked != nullptr) WriteRoomMonster(expectedActor, linkedSlot, *linked);
			const auto expectedActiveMonsterIds = std::vector<unsigned>(MonsterPoolAdapter::ActiveMonsterIds().begin(),
			    MonsterPoolAdapter::ActiveMonsterIds().end());
			SyncOpObject(remotePlayer, CMD_OPERATEOBJ, *sarcophagus);
			const NativeItemWorldState repeatedItems = CaptureItemWorld();
			std::ostringstream repeatedState;
			WriteItemWorld(repeatedState, repeatedItems, false);
			std::ostringstream repeatedObject;
			WriteRoomObject(repeatedObject, objectSlot, *sarcophagus);
			std::ostringstream repeatedActor;
			if (linked != nullptr) WriteRoomMonster(repeatedActor, linkedSlot, *linked);
			const auto repeatedActiveMonsterIds = std::vector<unsigned>(MonsterPoolAdapter::ActiveMonsterIds().begin(),
			    MonsterPoolAdapter::ActiveMonsterIds().end());
			row.Key("repeatedSarcophagus");
			WriteRoomObject(stream, objectSlot, *sarcophagus);
			row.Key("repeated");
			WriteItemWorld(stream, repeatedItems, false);
			row.Number("repeatedActiveObjectCount", ActiveObjectCount);
			row.Key("repeatedActiveObjectIds");
			writeActiveObjects();
			row.Key("repeatedActiveMonsterIds");
			writeActiveMonsters();
			row.Key("repeatedLinkedMonster");
			if (linked != nullptr)
				WriteRoomMonster(stream, linkedSlot, *linked);
			else
				stream << "null";
			row.Boolean("repeatedLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			EXPECT_EQ(expectedState.str(), repeatedState.str());
			EXPECT_EQ(expectedObject.str(), repeatedObject.str());
			EXPECT_EQ(expectedActor.str(), repeatedActor.str());
			EXPECT_EQ(expectedActiveMonsterIds, repeatedActiveMonsterIds);
			EXPECT_EQ(ActiveMonsterCount, activeMonsterCountBefore);
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 10);
	EXPECT_LE(std::filesystem::file_size(root / "native-sarcophagus-operations.json"), 64U * 1024U * 1024U);
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeTrapArrowBoundaries)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_TRAP_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the native trap capture directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode;
		bool networkInitialized = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		std::list<Missile> savedMissiles;
		bool preFlag = MissilePreFlag;
		ExecutionGuard() { Missiles.swap(savedMissiles); }
		~ExecutionGuard()
		{
			Missiles.swap(savedMissiles);
			MissilePreFlag = preFlag;
			if (networkInitialized) SNetDestroy();
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} guard;
	ASSERT_TRUE(guard.networkInitialized);
	HeadlessMode = true;
	gbSndInited = false;
	LoadPlayerDataFiles();
	LoadMissileData();
	std::ofstream stream(root / "native-trap-arrow.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-trap-arrow");
		document.Number("version", 1);
		document.Key("players");
		stream << '[';
		for (int lane = 0; lane < 5; ++lane) {
			PrepareRoom(1, 2, 0, 0, false);
			MyPlayer->_pmode = PM_STAND;
			MyPlayer->_pInvincible = false;
			MyPlayer->_pSpellFlags = SpellFlag::None;
			MyPlayer->_pIFlags = lane == 4 ? ItemSpecialEffect::HalfTrapDamage : ItemSpecialEffect::None;
			MyPlayer->_pIGetHit = 0;
			MyPlayer->_pBlockFlag = lane == 2;
			MyPlayer->attributes.dexterity.base = lane == 2 ? 100 : 0;
			MyPlayer->attributes.dexterity.current = lane == 2 ? 100 : 0;
			MyPlayer->life.current = MyPlayer->life.maximumBase;
			MyPlayer->life.base = MyPlayer->life.current;
			MyPlayer->life.maximum = MyPlayer->life.maximumBase;
			MyPlayer->mana.current = MyPlayer->mana.maximumBase;
			if (lane == 3) MyPlayer->_pSpellFlags |= SpellFlag::Etherealize;
			const int distance = lane == 1 ? 100 : 1;
			const uint32_t seed = lane == 2 ? 0 : UINT32_C(0x12345678);
			SetRndSeed(seed);
			if (lane != 0) stream << ',';
			JsonObject row(stream);
			row.Number("lane", lane);
			row.Number("seed", seed);
			row.Number("distance", distance);
			row.Number("inputLife", MyPlayer->life.current);
			row.Number("inputMana", MyPlayer->mana.current);
			row.Number("maximumLife", MyPlayer->life.maximumBase);
			row.Number("maximumMana", MyPlayer->mana.maximumBase);
			row.Number("armor", MyPlayer->GetArmor());
			row.Number("dexterity", MyPlayer->attributes.dexterity.current);
			bool blocked = false;
			const bool hit = PlayerMHit(*MyPlayer, nullptr, distance, 1, 2, MissileID::Arrow,
			    DamageType::Physical, false, DeathReason::MonsterOrTrap, &blocked);
			row.Boolean("hit", hit);
			row.Boolean("blocked", blocked);
			row.Number("life", MyPlayer->life.current);
			row.Number("mana", MyPlayer->mana.current);
			row.Number("mode", static_cast<int>(MyPlayer->_pmode));
			row.Number("rng", GetLCGEngineState());
		}
		stream << ']';
		PrepareRoom(1, 2, 0, 0, false);
		MyPlayer->_pmode = PM_STAND;
		MyPlayer->_pInvincible = false;
		MyPlayer->_pSpellFlags = SpellFlag::None;
		tileAt(50, 50).setPlayer(0);
		tileAt(51, 50).setMonster(0);
		Missiles.clear();
		Object *trigger = AddObjectPresentation(OBJ_CHEST1, { 52, 50 });
		Object *trap = AddObjectPresentation(OBJ_TRAPL, { 48, 50 });
		ASSERT_NE(trigger, nullptr);
		ASSERT_NE(trap, nullptr);
		trigger->_oTrapFlag = true;
		trigger->selectionRegion = SelectionRegion::None;
		trap->_oVar1 = 52;
		trap->_oVar2 = 50;
		trap->_oVar3 = static_cast<int>(MissileID::Arrow);
		trap->_oVar4 = 0;
		SetRndSeed(UINT32_C(0x12345678));
		ProcessObjects();
		ASSERT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(trap->_oVar4, 1);
		EXPECT_FALSE(trigger->_oTrapFlag);
		document.Key("flight");
		stream << '[';
		for (int step = 0; step < 3; ++step) {
			ProcessObjects();
			ProcessMissiles();
			ASSERT_EQ(Missiles.size(), 1U);
			const Missile &arrow = Missiles.front();
			if (step != 0) stream << ',';
			JsonObject row(stream);
			row.Number("distance", arrow._midist);
			row.Number("remaining", arrow.duration);
			row.Number("x", arrow.position.tile.x);
			row.Number("y", arrow.position.tile.y);
			row.Number("vx", arrow.position.velocity.deltaX);
			row.Number("vy", arrow.position.velocity.deltaY);
			row.Number("tx", arrow.position.traveled.deltaX);
			row.Number("ty", arrow.position.traveled.deltaY);
			row.Number("hash", arrow.lastCollisionTargetHash);
			row.Number("rng", GetLCGEngineState());
		}
		stream << ']';
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeBarrelOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the native barrel capture directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode;
		bool networkInitialized = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		~ExecutionGuard()
		{
			if (networkInitialized) SNetDestroy();
			gbSndInited = sound;
			HeadlessMode = headless;
		}
	} executionGuard;
	ASSERT_TRUE(executionGuard.networkInitialized);
	gbSndInited = false;
	HeadlessMode = true;
	// Class/experience tables are canonical process-lifetime data in native table tests.
	LoadPlayerDataFiles();
	LoadMissileData();
	ASSERT_GT(GetMaximumCharacterLevel(), 0);
	std::ofstream stream(root / "native-barrel-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-barrel-operations");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-untrapped-cathedral-level1");
		document.Number("playerMaximumLife", MyPlayer->life.maximumBase);
		document.Number("playerMaximumMana", MyPlayer->mana.maximumBase);
		document.Key("catalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		auto writePlayer = [&](const Player &player) {
			JsonObject json(stream);
			json.Number("id", player.getId());
			json.Number("level", player.plrlevel);
			json.Boolean("active", player.plractive);
			json.Number("mode", static_cast<int>(player._pmode));
			json.Number("characterLevel", player.getCharacterLevel());
			json.Number("experience", player._pExperience);
			json.Number("strength", player.attributes.strength.current);
			json.Number("dexterity", player.attributes.dexterity.current);
			json.Number("magic", player.attributes.magic.current);
			json.Key("tile");
			WriteRoomPoint(stream, player.position.tile);
			json.Key("future");
			WriteRoomPoint(stream, player.position.future);
			json.Number("life", player.life.current);
			json.Number("lifeMaximumBase", player.life.maximumBase);
			json.Number("mana", player.mana.current);
			json.Number("manaMaximumBase", player.mana.maximumBase);
			json.Number("armor", player.GetArmor());
			json.Number("blockChance", player.GetBlockChance(false));
			json.Boolean("blockFlag", player._pBlockFlag);
			json.Boolean("invincible", player._pInvincible);
			json.Number("fireResistance", player._pFireResist);
			json.Number("lightningResistance", player._pLghtResist);
			json.Number("magicResistance", player._pMagResist);
			json.Number("getHit", player._pIGetHit);
			json.Number("itemFlags", static_cast<uint32_t>(player._pIFlags));
		};
		auto addBarrel = [&](int type, Point position, uint32_t seed, int var2, int var3, int var4) -> Object * {
			Object *barrel = AddObjectPresentation(static_cast<_object_id>(type), position);
			if (barrel == nullptr) return nullptr;
			barrel->_oVar1 = 0;
			barrel->_oVar2 = var2;
			barrel->_oVar3 = var3;
			barrel->_oVar4 = var4;
			barrel->_oRndSeed = seed;
			return barrel;
		};
		auto prepare = [&](int itemCount = 2, int monsterCount = 0) {
			PrepareRoom(1, itemCount, 0, monsterCount, false);
			MyPlayer->pLvlLoad = 0;
			MyPlayer->setCharacterLevel(1);
			MyPlayer->life.base = MyPlayer->life.maximumBase;
			MyPlayer->life.maximum = MyPlayer->life.maximumBase;
			MyPlayer->life.current = MyPlayer->life.maximumBase;
			MyPlayer->mana.base = MyPlayer->mana.maximumBase;
			MyPlayer->mana.maximum = MyPlayer->mana.maximumBase;
			MyPlayer->mana.current = MyPlayer->mana.maximumBase;
			MyPlayer->_pExperience = 0;
			MyPlayer->_pmode = PM_STAND;
			MyPlayer->_pInvincible = false;
			MyPlayer->_pBlockFlag = false;
			MyPlayer->_pFireResist = 0;
			MyPlayer->_pLghtResist = 0;
			MyPlayer->_pMagResist = 0;
			MyPlayer->_pIGetHit = 0;
			MyPlayer->attributes.dexterity.base = 0;
			MyPlayer->attributes.dexterity.current = 0;
			tileAt(50, 50).setPlayer(0);
			tileAt(51, 50).setPlayer(0);
			tileAt(51, 50).setMonster(0);
		};
		auto setPlayerPosition = [&](Point position) {
			tileAt(MyPlayer->position.tile).setPlayer(0);
			MyPlayer->position.tile = position;
			MyPlayer->position.future = position;
			MyPlayer->position.old = position;
			tileAt(position).setPlayer(1); // Positive current occupancy is required by PlayerAtPosition(..., true).
		};
		auto capture = [&](const char *name, Object &barrel, uint32_t initialRandomState, const Monster *lootActor = nullptr) {
			SetRndSeed(initialRandomState);
			const NativeItemWorldState beforeItems = CaptureItemWorld();
			const bool originalSolid = barrel._oSolidFlag;
			barrel._oSolidFlag = false; // BreakBarrel removes solidity before ActivateSkeleton.
			constexpr std::array<Direction, 8> spawnDirections {
				Direction::North,
				Direction::NorthEast,
				Direction::East,
				Direction::NorthWest,
				Direction::SouthEast,
				Direction::West,
				Direction::SouthWest,
				Direction::South,
			};
			std::array<bool, 9> spawnTilesAvailable {};
			auto isSpawnTileAvailable = [](Point position) {
				return InDungeonBounds(position) && tileAt(position).player() == 0
				    && tileAt(position).monster() == 0 && IsTileWalkable(position);
			};
			spawnTilesAvailable[0] = isSpawnTileAvailable(barrel.position);
			for (size_t index = 0; index < spawnDirections.size(); ++index)
				spawnTilesAvailable[index + 1] = isSpawnTileAvailable(barrel.position + spawnDirections[index]);
			barrel._oSolidFlag = originalSolid;
			if (caseCount++ != 0) stream << ',';
			JsonObject row(stream);
			row.String("name", name);
			row.Number("depth", 1);
			row.Number("slot", barrel.GetId());
			row.Number("initialRandomState", initialRandomState);
			row.Number("barrelSeed", barrel._oRndSeed);
			row.Number("nativeType", barrel._otype);
			row.Number("var1", barrel._oVar1);
			row.Number("var2", barrel._oVar2);
			row.Number("var3", barrel._oVar3);
			row.Number("var4", barrel._oVar4);
			row.Boolean("levelLoading", MyPlayer->pLvlLoad != 0);
			row.Key("spawnTilesAvailable");
			stream << '[';
			for (size_t index = 0; index < spawnTilesAvailable.size(); ++index) {
				if (index != 0) stream << ',';
				stream << (spawnTilesAvailable[index] ? "true" : "false");
			}
			stream << ']';
			const Monster *linked = barrel._oVar4 >= 0 && barrel._oVar4 < MaxMonsters
			    ? &Monsters[barrel._oVar4]
			    : nullptr;
			row.Number("linkedMonsterSlot", linked == nullptr ? -1 : barrel._oVar4);
			row.Key("inputLinkedMonster");
			if (linked == nullptr)
				stream << "null";
			else
				WriteRoomMonster(stream, barrel._oVar4, *linked);
			row.Key("lootActorContext");
			if (lootActor == nullptr) {
				stream << "null";
			} else {
				JsonObject context(stream);
				context.Number("slot", lootActor->getId());
				context.Number("type", lootActor->type().type);
				context.Number("level", lootActor->level(DIFF_NORMAL));
				context.Number("experience", lootActor->exp(DIFF_NORMAL));
				context.Number("treasure", lootActor->data().treasure);
				context.Number("whoHit", lootActor->whoHit);
				context.Number("rndItemSeed", lootActor->rndItemSeed);
			}
			row.Boolean("inputLinkedMonsterPetrified", linked != nullptr && linked->animInfo.isPetrified);
			const int specialGraphic = static_cast<int>(MonsterGraphic::Special);
			row.Number("specialFrameCount", linked == nullptr ? 0 : linked->type().anims[specialGraphic].frames);
			row.Number("specialTicksPerFrame", linked == nullptr ? 0 : linked->type().anims[specialGraphic].rate);
			row.Key("inputBarrel");
			WriteRoomObject(stream, barrel.GetId(), barrel);
			row.Key("inputItems");
			WriteItemWorld(stream, beforeItems, true);
			row.Key("inputPlayer");
			writePlayer(*MyPlayer);
			row.Key("inputWorld");
			WriteRoomWorld(stream);
			BreakObject(*MyPlayer, barrel);
			const NativeItemWorldState afterItems = CaptureItemWorld();
			row.Number("expectedRandomState", GetLCGEngineState());
			row.Key("expectedBarrel");
			WriteRoomObject(stream, barrel.GetId(), barrel);
			row.Key("expectedItems");
			WriteItemWorld(stream, afterItems, false);
			row.Key("expectedPlayer");
			writePlayer(*MyPlayer);
			row.Key("expectedWorld");
			WriteRoomWorld(stream);
			EXPECT_FALSE(barrel.canInteractWith());
			EXPECT_FALSE(barrel._oSolidFlag);
			EXPECT_TRUE(barrel._oMissFlag);
			EXPECT_EQ(barrel._oAnimFrame, 1U);
			const int lifeAfterFirstBreak = MyPlayer->life.current;
			const int manaAfterFirstBreak = MyPlayer->mana.current;
			const Point positionAfterFirstBreak = MyPlayer->position.tile;
			const auto modeAfterFirstBreak = MyPlayer->_pmode;
			std::ostringstream firstWorld, firstItems;
			WriteRoomWorld(firstWorld);
			WriteItemWorld(firstItems, afterItems, false);
			BreakObject(*MyPlayer, barrel); // Broken barrels are a native no-op; no second drop or blast.
			const NativeItemWorldState repeatedItems = CaptureItemWorld();
			row.Key("repeatedBarrel");
			WriteRoomObject(stream, barrel.GetId(), barrel);
			row.Key("repeatedItems");
			WriteItemWorld(stream, repeatedItems, false);
			row.Key("repeatedPlayer");
			writePlayer(*MyPlayer);
			row.Key("repeatedWorld");
			WriteRoomWorld(stream);
			std::ostringstream repeatedWorld, repeatedItemsJson;
			WriteRoomWorld(repeatedWorld);
			WriteItemWorld(repeatedItemsJson, repeatedItems, false);
			EXPECT_EQ(firstWorld.str(), repeatedWorld.str());
			EXPECT_EQ(firstItems.str(), repeatedItemsJson.str());
			EXPECT_EQ(lifeAfterFirstBreak, MyPlayer->life.current);
			EXPECT_EQ(manaAfterFirstBreak, MyPlayer->mana.current);
			EXPECT_EQ(positionAfterFirstBreak, MyPlayer->position.tile);
			EXPECT_EQ(modeAfterFirstBreak, MyPlayer->_pmode);
		};

		prepare();
		Object *useful = addBarrel(OBJ_BARREL, { 50, 50 }, 2588, 0, 0, -1);
		ASSERT_NE(useful, nullptr);
		capture("ordinary-useful", *useful, UINT32_C(0x12345678));

		prepare();
		Object *random = addBarrel(OBJ_BARREL, { 50, 50 }, 1, 1, 1, -1);
		ASSERT_NE(random, nullptr);
		capture("ordinary-random", *random, UINT32_C(0x80000000));

		prepare();
		Object *empty = addBarrel(OBJ_BARREL, { 50, 50 }, UINT32_MAX, 2, 2, -1);
		ASSERT_NE(empty, nullptr);
		capture("ordinary-empty", *empty, UINT32_C(0x12345678));

		prepare();
		Object *linkedBarrel = addBarrel(OBJ_BARREL, { 50, 50 }, 743271966, 8, 1, -1);
		ASSERT_NE(linkedBarrel, nullptr);
		Monster *linked = PreSpawnSkeleton();
		ASSERT_NE(linked, nullptr);
		linkedBarrel->_oVar4 = static_cast<int>(linked->getId());
		capture("ordinary-linked-skeleton", *linkedBarrel, UINT32_C(0x87654321));

		prepare();
		for (int y = 49; y <= 51; ++y) {
			for (int x = 49; x <= 51; ++x) {
				const bool available = (x == 50 && y == 49) || (x == 51 && y == 49) || (x == 51 && y == 50);
				if (!available)
					tileAt(x, y).setPiece(2);
			}
		}
		SetRndSeed(UINT32_C(0xA11CE));
		Object *neighborBarrel = addBarrel(OBJ_BARREL, { 50, 50 }, 743271966, 8, 1, -1);
		ASSERT_NE(neighborBarrel, nullptr);
		Monster *neighborLinked = PreSpawnSkeleton();
		ASSERT_NE(neighborLinked, nullptr);
		neighborBarrel->_oVar4 = static_cast<int>(neighborLinked->getId());
		capture("ordinary-linked-skeleton-signed-neighbor", *neighborBarrel, UINT32_C(1457187811));

		auto findPlayerSeed = [](bool shouldHit) {
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				const int roll = GenerateRnd(100);
				if ((roll < 40) == shouldHit) return candidate;
			}
			return UINT32_MAX;
		};
		auto findPlayerBlockSeed = [] {
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				const int hit = GenerateRnd(100);
				const int block = GenerateRnd(100);
				if (hit < 40 && block < std::clamp(MyPlayer->GetBlockChance(false), 0, 100))
					return candidate;
			}
			return UINT32_MAX;
		};
		auto addBlastActor = [&](int resistance, int hitPoints, int whoHit) {
			SetRndSeed(UINT32_C(0xA11CE));
			Monster *actor = PreSpawnSkeleton();
			if (actor == nullptr)
				return actor;
			ActivateSkeleton(*actor, { 51, 50 });
			actor->resistance = static_cast<uint16_t>(resistance);
			actor->hitPoints = hitPoints;
			actor->maxHitPoints = std::max(hitPoints, 6400);
			actor->whoHit = static_cast<int8_t>(whoHit);
			return actor;
		};
		auto findMonsterHitSeed = [](const Monster &actor) {
			const int hitChance = std::clamp(90 - static_cast<int>(actor.armorClass), 5, 95);
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				if (GenerateRnd(100) < hitChance)
					return candidate;
			}
			return UINT32_MAX;
		};
		auto findMonsterLootSeed = [](const Monster &actor) {
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				if (RndItemForMonsterLevel(static_cast<int8_t>(actor.level(DIFF_NORMAL))) != IDI_NONE)
					return candidate;
			}
			return UINT32_MAX;
		};
		prepare();
		Object *hit = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(hit, nullptr);
		setPlayerPosition({ 50, 50 });
		const uint32_t hitSeed = findPlayerSeed(true);
		ASSERT_NE(hitSeed, UINT32_MAX);
		capture("explosive-player-hit", *hit, hitSeed);

		prepare();
		Object *miss = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(miss, nullptr);
		setPlayerPosition({ 50, 50 });
		const uint32_t missSeed = findPlayerSeed(false);
		ASSERT_NE(missSeed, UINT32_MAX);
		capture("explosive-player-miss", *miss, missSeed);

		prepare();
		MyPlayer->attributes.dexterity.base = 100;
		MyPlayer->attributes.dexterity.current = 100;
		MyPlayer->_pBlockFlag = true;
		Object *blocked = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(blocked, nullptr);
		setPlayerPosition({ 50, 50 });
		const uint32_t blockSeed = findPlayerBlockSeed();
		ASSERT_NE(blockSeed, UINT32_MAX);
		capture("explosive-player-blocked", *blocked, blockSeed);

		prepare();
		MyPlayer->_pFireResist = 50;
		Object *resistedPlayer = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(resistedPlayer, nullptr);
		setPlayerPosition({ 50, 50 });
		capture("explosive-player-fire-resisted", *resistedPlayer, findPlayerSeed(true));

		prepare(2, 0);
		Monster *resistedActor = addBlastActor(RESIST_FIRE, 6400, 0);
		ASSERT_NE(resistedActor, nullptr);
		Object *resistedMonster = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(resistedMonster, nullptr);
		capture("explosive-monster-fire-resisted", *resistedMonster, findMonsterHitSeed(*resistedActor), resistedActor);

		prepare(2, 0);
		Monster *immuneActor = addBlastActor(IMMUNE_FIRE, 6400, 0);
		ASSERT_NE(immuneActor, nullptr);
		Object *immuneMonster = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(immuneMonster, nullptr);
		capture("explosive-monster-fire-immune", *immuneMonster, UINT32_C(0xC001D00D), immuneActor);

		for (const auto &[name, whoHit] : std::array<std::pair<const char *, int>, 2> {
		         std::pair { "explosive-monster-lethal-uncredited", 0 },
		         std::pair { "explosive-monster-lethal-credited", 1 } }) {
			prepare(2, 0);
			Monster *lethalActor = addBlastActor(0, 64, whoHit);
			ASSERT_NE(lethalActor, nullptr);
			const uint32_t lootSeed = findMonsterLootSeed(*lethalActor);
			ASSERT_NE(lootSeed, UINT32_MAX);
			lethalActor->rndItemSeed = lootSeed;
			Object *lethalMonster = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
			ASSERT_NE(lethalMonster, nullptr);
			const uint32_t hitSeed = findMonsterHitSeed(*lethalActor);
			ASSERT_NE(hitSeed, UINT32_MAX);
			capture(name, *lethalMonster, hitSeed, lethalActor);
			EXPECT_EQ(lethalActor->hitPoints, 0);
			EXPECT_EQ(MyPlayer->_pExperience > 0, whoHit != 0);
		}

		prepare(2, 0);
		MyPlayer->setCharacterLevel(3);
		MyPlayer->_pExperience = GetNextExperienceThresholdForLevel(2);
		Monster *scaledActor = addBlastActor(0, 64, 1);
		ASSERT_NE(scaledActor, nullptr);
		scaledActor->rndItemSeed = findMonsterLootSeed(*scaledActor);
		Object *scaledBarrel = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		ASSERT_NE(scaledBarrel, nullptr);
		const uint32_t beforeScaledExperience = MyPlayer->_pExperience;
		capture("explosive-monster-lethal-credited-level-delta", *scaledBarrel,
		    findMonsterHitSeed(*scaledActor), scaledActor);
		EXPECT_EQ(MyPlayer->_pExperience - beforeScaledExperience, 51U);

		prepare(2, 0);
		Monster *occupant = PreSpawnSkeleton();
		ASSERT_NE(occupant, nullptr);
		ActivateSkeleton(*occupant, { 51, 50 });
		ASSERT_EQ(occupant->position.tile, (WorldTilePosition { 51, 50 }));
		Object *chainRoot = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		Object *chainChild = addBarrel(OBJ_BARRELEX, { 51, 50 }, 2, 0, 0, -1);
		ASSERT_NE(chainRoot, nullptr);
		ASSERT_NE(chainChild, nullptr);
		setPlayerPosition({ 51, 50 });
		capture("explosive-chain-monster-player-order", *chainRoot, UINT32_C(0x5EED1234));
		EXPECT_FALSE(chainChild->canInteractWith());
		EXPECT_FALSE(chainChild->_oSolidFlag);
		prepare(2, 0);
		Monster *northwestActor = addBlastActor(0, 256, 0);
		ASSERT_NE(northwestActor, nullptr);
		tileAt(northwestActor->position.tile).setMonster(0);
		northwestActor->position.tile = { 48, 48 };
		northwestActor->position.future = { 48, 48 };
		northwestActor->position.old = { 48, 48 };
		tileAt(48, 48).setMonster(northwestActor->getId() + 1);
		Object *threeRoot = addBarrel(OBJ_BARRELEX, { 50, 50 }, 1, 0, 0, -1);
		Object *threeNorthwest = addBarrel(OBJ_BARRELEX, { 49, 49 }, 2, 0, 0, -1);
		Object *threeSouth = addBarrel(OBJ_BARRELEX, { 50, 51 }, 3, 0, 0, -1);
		ASSERT_NE(threeRoot, nullptr);
		ASSERT_NE(threeNorthwest, nullptr);
		ASSERT_NE(threeSouth, nullptr);
		setPlayerPosition({ 51, 50 });
		capture("explosive-three-barrel-nw-monster-player-order", *threeRoot, 2588, northwestActor);
		EXPECT_FALSE(threeNorthwest->canInteractWith());
		EXPECT_FALSE(threeSouth->canInteractWith());
		EXPECT_EQ(northwestActor->hitPoints, 128);
		EXPECT_EQ(MyPlayer->life.current, 5422);
		EXPECT_EQ(GetLCGEngineState(), UINT32_C(1630774887));
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 16);
	EXPECT_LE(std::filesystem::file_size(root / "native-barrel-operations.json"), 64U * 1024U * 1024U);
}

TEST_F(ThemeRoomGenerationTest, FullObjectPoolRetainsSlotsAndFailedShrineCarry)
{
	PrepareRoom(1, 2, MAXOBJECTS, MaxMonsters, false);
	numThemes() = 1;
	themes[0] = { THEME_LIBRARY, 1 };
	SetRndSeed(2588);
	const auto before = CaptureThemeRoomGlobalsForTesting();
	CreateThemeRooms();
	EXPECT_EQ(ActiveObjectCount, MAXOBJECTS);
	EXPECT_EQ(ActiveMonsterCount, MaxMonsters);
	EXPECT_EQ(CaptureThemeRoomGlobalsForTesting().variant, before.variant);
	EXPECT_EQ(CaptureThemeRoomGlobalsForTesting().x, 0);
	EXPECT_EQ(CaptureThemeRoomGlobalsForTesting().y, 0);
	for (int slot = 0; slot < MAXOBJECTS; ++slot) {
		EXPECT_EQ(Objects[slot]._oVar1, 91);
		EXPECT_EQ(Objects[slot]._oVar2, -17);
		EXPECT_EQ(Objects[slot]._oRndSeed, 1000U + slot);
	}
}

TEST_F(ThemeRoomGenerationTest, NoSkeletonBarrelRetainsMinusOneWithoutSelectionDraw)
{
	LevelBestiary.init();
	ASSERT_TRUE(LevelBestiary.addType(MT_GOLEM, PLACE_SPECIAL).has_value());
	ASSERT_TRUE(LevelBestiary.addType(MT_NZOMBIE, PLACE_SCATTER).has_value());
	uint32_t selectedSeed = 0, expectedState = 0;
	int expectedVar2 = 0, expectedVar3 = 0;
	bool found = false;
	// The actual bundled barrel is not animated. Only seed/var2/var3 draw;
	// AddSkeleton's empty candidate list consumes no random value.
	ASSERT_EQ(static_cast<uint8_t>(AllObjects[OBJ_BARREL].flags) & 1U, 0U);
	for (uint32_t seed = 0; seed < 4096 && !found; ++seed) {
		SetRndSeed(seed);
		AdvanceRndSeed();
		expectedVar2 = GenerateRnd(10);
		expectedVar3 = GenerateRnd(3);
		if (expectedVar2 >= 8) {
			selectedSeed = seed;
			expectedState = GetLCGEngineState();
			found = true;
		}
	}
	ASSERT_TRUE(found);
	PrepareRoom(1);
	SetRndSeed(selectedSeed);
	Object *barrel = AddObject(OBJ_BARREL, { 50, 50 });
	ASSERT_NE(barrel, nullptr);
	EXPECT_EQ(barrel->_oVar2, expectedVar2);
	EXPECT_EQ(barrel->_oVar3, expectedVar3);
	EXPECT_EQ(barrel->_oVar4, -1);
	EXPECT_EQ(ActiveMonsterCount, 4U);
	EXPECT_EQ(GetLCGEngineState(), expectedState);
	RecordProperty("barrelSeed", std::to_string(selectedSeed));
}

TEST_F(ThemeRoomGenerationTest, BlockedSkeletonWithNoNeighborRetainsReservationWithoutDraw)
{
	PrepareRoom(1);
	SetRndSeed(2588);
	Monster *skeleton = PreSpawnSkeleton();
	ASSERT_NE(skeleton, nullptr);
	const auto slot = skeleton->getId();
	const auto count = ActiveMonsterCount;
	const uint32_t randomState = GetLCGEngineState();
	for (int y = 49; y <= 51; ++y)
		for (int x = 49; x <= 51; ++x)
			tileAt(x, y).setPlayer(1);
	ActivateSkeleton(*skeleton, { 50, 50 });
	EXPECT_EQ(skeleton->position.tile, (WorldTilePosition { 0, 0 }));
	EXPECT_EQ(skeleton->position.future, (WorldTilePosition { 0, 0 }));
	EXPECT_EQ(skeleton->mode, MonsterMode::Stand);
	EXPECT_EQ(ActiveMonsterCount, count);
	EXPECT_EQ(ActiveMonsters[count - 1], slot);
	EXPECT_EQ(GetLCGEngineState(), randomState);
	EXPECT_FALSE(skeleton->animInfo.isPetrified);
}

TEST_F(ThemeRoomGenerationTest, SignedSkeletonFallbackUsesNativeUnsignedSizeRemainder)
{
	static_assert(sizeof(size_t) == 8, "This signed corner is the frozen x64 oracle contract.");
	PrepareRoom(1);
	SetRndSeed(2588);
	Monster *skeleton = PreSpawnSkeleton();
	ASSERT_NE(skeleton, nullptr);
	for (int y = 49; y <= 51; ++y)
		for (int x = 49; x <= 51; ++x)
			tileAt(x, y).setPlayer(1);
	// Native direction iteration N,NE,E: three choices, not a power of two.
	for (int x = 49; x <= 51; ++x)
		tileAt(x, 49).setPlayer(0);
	SetRndSeed(UINT32_C(1457187811));
	ASSERT_EQ(GenerateRnd(15), -8);
	const uint32_t expectedState = GetLCGEngineState();
	ASSERT_EQ(static_cast<size_t>(-8) % 3U, 2U);
	SetRndSeed(UINT32_C(1457187811));
	ActivateSkeleton(*skeleton, { 50, 50 });
	EXPECT_EQ(GetLCGEngineState(), expectedState);
	EXPECT_EQ(skeleton->position.tile, (WorldTilePosition { 51, 49 }));
	EXPECT_EQ(skeleton->direction, Direction::West);
	EXPECT_EQ(skeleton->mode, MonsterMode::SpecialStand);
	EXPECT_EQ(tileAt(51, 49).monster(), static_cast<int>(skeleton->getId()) + 1);
	EXPECT_FALSE(skeleton->animInfo.isPetrified);
}

TEST_F(ThemeRoomGenerationTest, BothMultiplayerOnlyShrineSelectionsRetryBeforeAnimationCoin)
{
	const auto availability = SinglePlayerShrineAvailabilityForTesting();
	ASSERT_EQ(static_cast<uint8_t>(AllObjects[OBJ_SHRINER].flags) & 1U, 0U);
	for (const int rejected : { 18, 25 }) {
		ASSERT_FALSE(availability[rejected]);
		uint32_t selectedSeed = 0, expectedState = 0, expectedObjectSeed = 0;
		int expectedIndex = 0;
		bool expectedAlternateAnimation = false, found = false;
		for (uint32_t seed = 0; seed < 65536 && !found; ++seed) {
			SetRndSeed(seed);
			expectedObjectSeed = AdvanceRndSeed();
			if (GenerateRnd(26) != rejected) continue;
			do {
				expectedIndex = GenerateRnd(26);
				ASSERT_GE(expectedIndex, 0);
			} while (!availability[expectedIndex]);
			expectedAlternateAnimation = !FlipCoin();
			expectedState = GetLCGEngineState();
			selectedSeed = seed;
			found = true;
		}
		ASSERT_TRUE(found);
		PrepareRoom(1);
		SetRndSeed(selectedSeed);
		Object *shrine = AddObject(OBJ_SHRINER, { 50, 50 });
		ASSERT_NE(shrine, nullptr);
		EXPECT_EQ(shrine->_oRndSeed, expectedObjectSeed);
		EXPECT_EQ(shrine->_oVar1, expectedIndex);
		EXPECT_EQ(GetLCGEngineState(), expectedState);
		EXPECT_TRUE(shrine->_oPreFlag);
		EXPECT_EQ(shrine->_oAnimFrame, expectedAlternateAnimation ? 12U : AllObjects[OBJ_SHRINER].animDelay);
		EXPECT_EQ(shrine->_oAnimLen, expectedAlternateAnimation ? 22U : AllObjects[OBJ_SHRINER].animLen);
		RecordProperty("rejected" + std::to_string(rejected) + "Seed", std::to_string(selectedSeed));
	}
}

TEST_F(ThemeRoomGenerationTest, CaptureCompleteSelectedThemePass)
{
	const char *directory = std::getenv("GODEVILUTION_THEME_ROOM_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory) GTEST_SKIP() << "Select the separate private theme-room corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	const auto repository = std::filesystem::weakly_canonical(std::filesystem::absolute(__FILE__).parent_path().parent_path());
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	ASSERT_FALSE(std::mismatch(repository.begin(), repository.end(), root.begin(), root.end()).first == repository.end());
	std::ofstream stream(root / "native-theme-rooms.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	constexpr std::array<int, 14> types { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 13, 14, 15 };
	int cases = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-theme-rooms");
		document.Number("version", 1);
		document.String("policy", "original-diablo-single-player-normal-complete-selected-cathedral-themes");
		document.Number("playerMaximumLife", 6400);
		document.Number("playerMaximumMana", 3200);
		document.Key("itemCatalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		for (int depth = 1; depth <= 4; ++depth) {
			for (int lane = 0; lane < 22; ++lane) {
				const int itemCount = lane == 17 ? MAXITEMS : 2;
				const int objectCount = lane == 15 || lane == 16 ? MAXOBJECTS : 0;
				const int monsterCount = lane == 18 || lane == 19 ? MaxMonsters : 4;
				PrepareRoom(depth, itemCount, objectCount, monsterCount, lane != 16);
				const uint32_t seed = lane == 17 ? UINT32_C(1457187811) : UINT32_C(2588) + depth * 17;
				const std::string name = lane < 14 ? "type-" + std::to_string(types[lane]) : std::array<std::string, 8> { "ordered", "object-full-library", "object-full-failed-fit", "item-full-signed-pit", "monster-full-skeleton", "monster-full-goat", "loading-shrine", "empty" }[lane - 14];
				numThemes() = lane == 21 ? 0 : lane == 14 ? 14
				                                          : 1;
				if (lane == 14)
					for (int i = 0; i < 14; ++i)
						themes[i] = { static_cast<theme_id>(types[i]), 1 };
				else if (lane != 21)
					themes[0] = { static_cast<theme_id>(lane < 14 ? types[lane] : lane == 15 || lane == 16 ? 5
						                  : lane == 17                                                     ? 2
						                  : lane == 18                                                     ? 3
						                  : lane == 19                                                     ? 11
						                                                                                   : 1),
						1 };
				LoadingMapObjects = lane == 20;
				SetRndSeed(seed);
				if (cases++ != 0) stream << ',';
				JsonObject row(stream);
				row.String("name", name);
				row.Number("depth", depth);
				row.Number("seed", seed);
				row.Boolean("loadingMapObjects", LoadingMapObjects);
				row.Key("themes");
				stream << '[';
				for (int i = 0; i < numThemes(); ++i) {
					if (i) stream << ',';
					stream << '[' << static_cast<int>(themes[i].ttype) << ',' << static_cast<int>(themes[i].ttval) << ']';
				}
				stream << ']';
				row.Key("player");
				{
					JsonObject player(stream);
					player.Number("id", 0);
					player.Number("level", depth);
					player.Boolean("active", true);
					player.Boolean("levelChanging", false);
					player.Key("tile");
					WriteRoomPoint(stream, MyPlayer->position.tile);
					player.Key("future");
					WriteRoomPoint(stream, MyPlayer->position.future);
				}
				row.Key("input");
				WriteRoomWorld(stream);
				CreateThemeRooms();
				row.Key("expected");
				WriteRoomWorld(stream);
				if (lane == 17) {
					EXPECT_EQ(ActiveItemCount, MAXITEMS);
					const Item &last = Items[ActiveItems[MAXITEMS - 1]];
					EXPECT_FALSE(last._iAnimFlag);
					EXPECT_EQ(last.selectionRegion, SelectionRegion::Bottom);
					EXPECT_EQ(last.animInfo.currentFrame, last.animInfo.numberOfFrames - 1);
				}
			}
		}
		stream << ']';
		document.Number("caseCount", cases);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(cases, 88);
	EXPECT_LE(std::filesystem::file_size(root / "native-theme-rooms.json"), 128U * 1024U * 1024U);
}

// Controlled L2 theme seams complement the separately captured generated maps.
TEST_F(ThemeRoomGenerationTest, CaptureCompleteCatacombsThemePass)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_THEME_ROOM_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select the separate private Catacombs theme-room corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	std::ofstream stream(root / "native-catacombs-theme-rooms.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	constexpr std::array<int, 16> types { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16 };
	constexpr std::array<const char *, 6> edges {
		"ordered-racks", "object-full-armor", "object-full-weapon", "item-full-gold", "item-full-minimum-gold", "item-full-nongold"
	};
	int cases = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-theme-rooms");
		document.Number("version", 1);
		document.String("policy", "controlled-original-diablo-single-player-normal-catacombs-theme-branches");
		document.Number("playerMaximumLife", 6400);
		document.Number("playerMaximumMana", 3200);
		document.Key("itemCatalog");
		WriteCatalog(stream);
		document.Key("roomCatalog");
		WriteRoomCatalog(stream);
		document.Key("cases");
		stream << '[';
		for (const int depth : { 5, 8 }) {
			for (int lane = 0; lane < 22; ++lane) {
				const bool fullItems = lane >= 19;
				PrepareRoom(depth, fullItems ? MAXITEMS : 2, lane == 17 || lane == 18 ? MAXOBJECTS : 0);
				currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATACOMBS });
				CurrentLightManager.MakeLightTable();
				const uint32_t seed = UINT32_C(2588) + depth * 17;
				const std::string name = lane < 16 ? "type-" + std::to_string(types[lane]) : edges[lane - 16];
				numThemes() = lane == 16 ? 4 : 1;
				if (lane == 16) {
					for (int i = 0; i < 4; ++i)
						themes[i] = { static_cast<theme_id>(i % 2 == 0 ? 10 : 16), 1 };
				} else {
					themes[0] = { static_cast<theme_id>(lane < 16 ? types[lane] : lane == 17 ? 10
						                  : lane == 18                                       ? 16
						                                                                     : 4),
						1 };
				}
				if (fullItems) {
					Item &last = Items[ActiveItems[MAXITEMS - 1]];
					last._itype = lane == 21 ? ItemType::Misc : ItemType::Gold;
					last.IDidx = lane == 21 ? IDI_HEAL : IDI_GOLD;
					last._ivalue = lane == 20 ? 1 : 99;
				}
				SetRndSeed(seed);
				if (cases++ != 0) stream << ',';
				JsonObject row(stream);
				row.String("name", name);
				row.Number("depth", depth);
				row.Number("seed", seed);
				row.Key("themes");
				stream << '[';
				for (int i = 0; i < numThemes(); ++i) {
					if (i != 0) stream << ',';
					stream << '[' << static_cast<int>(themes[i].ttype) << ',' << static_cast<int>(themes[i].ttval) << ']';
				}
				stream << ']';
				row.Key("player");
				{
					JsonObject player(stream);
					player.Number("id", 0);
					player.Number("level", depth);
					player.Boolean("active", true);
					player.Boolean("levelChanging", false);
					player.Key("tile");
					WriteRoomPoint(stream, MyPlayer->position.tile);
					player.Key("future");
					WriteRoomPoint(stream, MyPlayer->position.future);
				}
				row.Key("input");
				WriteRoomWorld(stream);
				CreateThemeRooms();
				row.Key("expected");
				WriteRoomWorld(stream);
				if (fullItems) {
					const Item &last = Items[ActiveItems[MAXITEMS - 1]];
					EXPECT_EQ(last._ivalue, lane == 21 ? 99 : 1);
					EXPECT_EQ(last._iSeed, 1000U);
					EXPECT_EQ(last._iPLStr, 17);
					EXPECT_EQ(last._iCharges, 7);
					EXPECT_EQ(last._iMaxCharges, 9);
					EXPECT_EQ(last.dwBuff, 128U);
				}
			}
		}
		stream << ']';
		document.Number("caseCount", cases);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(cases, 44);
	EXPECT_LE(std::filesystem::file_size(root / "native-catacombs-theme-rooms.json"), 128U * 1024U * 1024U);
}

// Extra public state borrowed only by the real-geometry opt-in. The underlying
// guards already retain tables, pools, tiles, RNG, player and lighting state.
struct NativeComposedPopulationGuard {
	MonsterPopulationGlobalsForTesting oldMonsterGlobals = CaptureMonsterPopulationGlobalsForTesting();
	ItemInitializationGlobalsForTesting oldItemGlobals = CaptureItemInitializationGlobalsForTesting();
	std::unique_ptr<Level> oldLevel = std::make_unique<Level>(currentLevel().id());
	decltype(sgGameInitInfo) oldGameInfo = sgGameInitInfo;
	bool oldHellfireSave = gbIsHellfireSaveGame;
	bool oldPlayerIsDead = MyPlayerIsDead;
	std::vector<LevelGenerationRow> oldLevelData = LevelGenerationData;
	std::array<std::optional<uint32_t>, NUMLEVELS> oldSeeds;
	std::array<uint32_t, NUMLEVELS> oldDungeonSeeds;
	std::array<TriggerStruct, MAXTRIGGERS> oldTriggers;
	NativeComposedPopulationGuard()
	{
		CopyCoarseState(*oldLevel, currentLevel());
		oldLevel->pMegaTiles_ = std::move(currentLevel().pMegaTiles_);
		oldLevel->pDungeonCels_ = std::move(currentLevel().pDungeonCels_);
		oldLevel->pSpecialCels_ = std::move(currentLevel().pSpecialCels_);
		std::copy_n(LevelSeeds, NUMLEVELS, oldSeeds.begin());
		std::copy_n(DungeonSeeds, NUMLEVELS, oldDungeonSeeds.begin());
		std::copy_n(trigs, MAXTRIGGERS, oldTriggers.begin());
	}
	~NativeComposedPopulationGuard()
	{
		CopyCoarseState(currentLevel(), *oldLevel);
		currentLevel().pMegaTiles_ = std::move(oldLevel->pMegaTiles_);
		currentLevel().pDungeonCels_ = std::move(oldLevel->pDungeonCels_);
		currentLevel().pSpecialCels_ = std::move(oldLevel->pSpecialCels_);
		sgGameInitInfo = oldGameInfo;
		gbIsHellfireSaveGame = oldHellfireSave;
		MyPlayerIsDead = oldPlayerIsDead;
		LevelGenerationData = std::move(oldLevelData);
		std::copy(oldSeeds.begin(), oldSeeds.end(), LevelSeeds);
		std::copy(oldDungeonSeeds.begin(), oldDungeonSeeds.end(), DungeonSeeds);
		std::copy(oldTriggers.begin(), oldTriggers.end(), trigs);
		RestoreMonsterPopulationGlobalsForTesting(oldMonsterGlobals);
		RestoreItemInitializationGlobalsForTesting(oldItemGlobals);
	}
	static void CopyCoarseState(Level &target, const Level &source)
	{
		target.setId(source.id());
		target.DungeonMask_ = source.DungeonMask_;
		target.Protected_ = source.Protected_;
		target.SetPieceRoom_ = source.SetPieceRoom_;
		target.SetPiece_ = source.SetPiece_;
		for (int x = 0; x < DMAXX; ++x)
			std::copy_n(source.megaTiles_[x], DMAXY, target.megaTiles_[x]);
		std::copy_n(source.DPieceMicros_, MAXTILES, target.DPieceMicros_);
		target.dminPosition_ = source.dminPosition_;
		target.dmaxPosition_ = source.dmaxPosition_;
		target.ViewPosition_ = source.ViewPosition_;
		target.MicroTileLen_ = source.MicroTileLen_;
		target.TransVal_ = source.TransVal_;
		target.TransList_ = source.TransList_;
		target.themeCount_ = source.themeCount_;
		std::copy_n(source.themeLoc_, MAXTHEMES, target.themeLoc_);
		target.numThemes_ = source.numThemes_;
		target.armorFlag_ = source.armorFlag_;
		target.weaponFlag_ = source.weaponFlag_;
		target.zharlib_ = source.zharlib_;
		target.trigFlag_ = source.trigFlag_;
		target.numTriggers_ = source.numTriggers_;
		target.tWarpFrom_ = source.tWarpFrom_;
		target.uberRow_ = source.uberRow_;
		target.uberCol_ = source.uberCol_;
		target.isUberRoomOpened_ = source.isUberRoomOpened_;
		target.isUberLeverActivated_ = source.isUberLeverActivated_;
		target.uberDiabloMonsterIndex_ = source.uberDiabloMonsterIndex_;
	}
};

// Bind semantic L8 speech inputs to actual native object initialization; no MPQ is opened.
TEST_F(ThemeRoomGenerationTest, CatacombsStoryBooksUseNativeSecondMessageColumn)
{
	NativeComposedPopulationGuard guard;
	constexpr std::array<_speech_id, 3> messages { TEXT_BOOK12, TEXT_BOOK22, TEXT_BOOK32 };
	constexpr std::array<int, 3> nativeIds { 250, 253, 256 };
	ASSERT_FALSE(AllObjects[OBJ_STORYBOOK].isAnimated());
	for (size_t row = 0; row < messages.size(); ++row) {
		SCOPED_TRACE(row);
		PrepareRoom(8, 0, 0, 0, false);
		currentLevel().setId(LevelId { .levelNum = 8, .type = DTYPE_CATACOMBS });
		currentLevelNumber() = 8;
		levelType() = DTYPE_CATACOMBS;
		DungeonSeeds[16] = static_cast<uint32_t>(row) << 16;
		SetRndSeed(UINT32_C(0x13579BDF));
		const uint32_t before = GetLCGEngineState();
		Object *book = AddObject(OBJ_STORYBOOK, { 50, 50 });
		ASSERT_NE(book, nullptr);
		EXPECT_EQ(static_cast<int>(messages[row]), nativeIds[row]);
		EXPECT_EQ(book->_oVar1, static_cast<int>(row));
		EXPECT_EQ(book->_oVar2, messages[row]);
		EXPECT_EQ(book->_oVar3, 1 + 3 * static_cast<int>(row));
		EXPECT_EQ(book->_oAnimFrame, 5U - 2U * static_cast<unsigned>(row));
		EXPECT_EQ(book->_oVar4, 6 - 2 * static_cast<int>(row));
		EXPECT_EQ(GetLCGEngineState(), before);
	}
}

// Extends the existing controlled upstream oracle, not the complete campaign
// LoadGameLevel lifecycle. Geometry comes from original local assets; this
// fixture alone opens MPQs and only when explicitly selected.
// Own the extra global channels used only by the cold loader continuation.
struct NativeLevelReadyGuard {
	AutomapManager oldAutomap = CurrentAutomapManager;
	std::array<uint16_t, 256> oldDefinitions = CaptureAutomapDefinitionsForTesting();
	std::list<Missile> oldMissiles = std::move(Missiles);
	bool oldMissilePreFlag = MissilePreFlag;
	std::array<Corpse, MaxCorpses> oldCorpses;
	int8_t oldStone = stonendx;
	std::array<Portal, MAXPORTAL> oldPortals;
	NativeLevelReadyGuard()
	{
		for (size_t index = 0; index < MaxCorpses; ++index)
			oldCorpses[index] = std::move(Corpses[index]);
		std::copy_n(Portals, MAXPORTAL, oldPortals.begin());
	}
	~NativeLevelReadyGuard()
	{
		CurrentAutomapManager = oldAutomap;
		RestoreAutomapDefinitionsForTesting(oldDefinitions);
		Missiles = std::move(oldMissiles);
		MissilePreFlag = oldMissilePreFlag;
		for (size_t index = 0; index < MaxCorpses; ++index)
			Corpses[index] = std::move(oldCorpses[index]);
		stonendx = oldStone;
		std::copy(oldPortals.begin(), oldPortals.end(), Portals);
	}
};

void WriteLevelReadyState(std::ostream &out)
{
	JsonObject ready(out);
	ready.Key("world");
	WriteRoomWorld(out);
	ready.Key("corpses");
	WriteArray(out, Corpses, [&](const Corpse &corpse) {
		out << '[' << corpse.frame << ',' << corpse.width << ',' << static_cast<int>(corpse.translationPaletteIndex) << ']';
	});
	ready.Number("stoneCorpseId", stonendx);
	ready.Key("bestiaryCorpseIds");
	WriteArray(out, LevelBestiary, [&](const CMonster &type) { out << static_cast<int>(type.corpseId); });
	ready.Key("deathAnimations");
	WriteArray(out, LevelBestiary, [&](const CMonster &type) {
		const AnimStruct &death = type.getAnimData(MonsterGraphic::Death);
		out << '[' << static_cast<int>(death.frames) << ',' << death.width << ']';
	});
	ready.Key("player");
	{
		const Player &player = *MyPlayer;
		JsonObject value(out);
		value.Key("tile");
		WriteRoomPoint(out, { player.position.tile.x, player.position.tile.y });
		value.Key("future");
		WriteRoomPoint(out, { player.position.future.x, player.position.future.y });
		value.Key("old");
		WriteRoomPoint(out, { player.position.old.x, player.position.old.y });
		value.Key("last");
		WriteRoomPoint(out, { player.position.last.x, player.position.last.y });
		value.Key("temp");
		WriteRoomPoint(out, { player.position.temp.x, player.position.temp.y });
		value.Key("view");
		WriteRoomPoint(out, { viewPosition().x, viewPosition().y });
		value.Number("id", MyPlayerId);
		value.Number("level", player.plrlevel);
		value.Number("currentLife", player.life.current);
		value.Number("lightId", player.lightId);
		value.Number("lightRadius", player._pLightRad);
		value.Boolean("active", player.plractive);
		value.Boolean("levelChanging", player._pLvlChanging);
	}
	ready.Number("spellFlags", MyPlayer->_pSpellFlags);
	ready.Boolean("infravision", MyPlayer->_pInfraFlag);
	ready.Boolean("autoMapShowItems", CurrentAutomapManager.GetAutoMapShowItems());
	ready.Number("missileCount", Missiles.size());
	ready.Boolean("missilePreFlag", MissilePreFlag);
	ready.Key("automapExploration");
	out << '[';
	for (int y = 0; y < DMAXY; ++y)
		for (int x = 0; x < DMAXX; ++x) {
			if (x != 0 || y != 0) out << ',';
			out << static_cast<int>(CurrentAutomapManager.GetAutomapView()[x][y]);
		}
	out << ']';
	ready.Key("visibleTransparencyRegions");
	WriteArray(out, visibleTransparencyRegions(), [&](bool value) { out << (value ? "true" : "false"); });
	ready.Boolean("updateVision", CurrentLightManager.VisionUpdatePendingForTesting());
	ready.Key("visionActive");
	WriteArray(out, CurrentLightManager.visionActive_, [&](bool value) { out << (value ? "true" : "false"); });
	ready.Key("visionRecords");
	int id = 0;
	WriteArray(out, CurrentLightManager.visionList_, [&](const Light &vision) {
		JsonObject value(out);
		value.Number("lightId", id++);
		value.Key("tile");
		WriteRoomPoint(out, { vision.position.tile.x, vision.position.tile.y });
		value.Key("oldTile");
		WriteRoomPoint(out, { vision.position.old.x, vision.position.old.y });
		value.Number("offsetX", vision.position.offset.deltaX);
		value.Number("offsetY", vision.position.offset.deltaY);
		value.Number("radius", vision.radius);
		value.Number("oldRadius", vision.oldRadius);
		value.Boolean("isInvalid", vision.isInvalid);
		value.Boolean("hasChanged", vision.hasChanged);
	});
}

#ifndef UNPACKED_MPQS
std::optional<std::vector<uint16_t>> ReadCursorWidthsForNativeOracle(std::string_view path, std::string &error)
{
	auto asset = LoadAsset(path);
	if (!asset.has_value()) {
		error = asset.error();
		return std::nullopt;
	}
	std::string_view text = static_cast<std::string_view>(asset.value());
	std::vector<uint16_t> widths;
	while (!text.empty()) {
		const char *end;
		const ParseIntResult<uint16_t> parsed = ParseInt<uint16_t>(text, std::numeric_limits<uint16_t>::min(),
		    std::numeric_limits<uint16_t>::max(), &end);
		if (!parsed.has_value() || end == text.data()) {
			error = "Cursor width asset contains an invalid width.";
			return std::nullopt;
		}
		widths.push_back(parsed.value());
		text.remove_prefix(static_cast<size_t>(end - text.data()));
		while (!text.empty() && (text.front() == '\r' || text.front() == '\n'))
			text.remove_prefix(1);
	}
	if (widths.empty()) {
		error = "Cursor width asset is empty.";
		return std::nullopt;
	}
	return widths;
}
#endif

std::optional<std::string> WriteNativeRogueInventoryFootprints(const std::filesystem::path &root)
{
	std::optional<OwnedClxSpriteList> primarySpritesStorage;
	std::optional<OwnedClxSpriteList> secondarySprites;
#ifdef UNPACKED_MPQS
	auto primaryResult = LoadClxWithStatus("data\\inv\\objcurs.clx");
	if (!primaryResult.has_value())
		return primaryResult.error();
	primarySpritesStorage.emplace(std::move(primaryResult.value()));
	if (FindAsset("data\\inv\\objcurs2.clx").ok()) {
		auto secondaryResult = LoadClxWithStatus("data\\inv\\objcurs2.clx");
		if (!secondaryResult.has_value())
			return secondaryResult.error();
		secondarySprites.emplace(std::move(secondaryResult.value()));
	}
#else
	std::string loadError;
	auto primaryWidths = ReadCursorWidthsForNativeOracle("data\\inv\\objcurs-widths.txt", loadError);
	if (!primaryWidths.has_value())
		return loadError;
	auto primaryResult = LoadCelWithStatus("data\\inv\\objcurs", primaryWidths->data());
	if (!primaryResult.has_value())
		return primaryResult.error();
	primarySpritesStorage.emplace(std::move(primaryResult.value()));
	if (FindAsset("data\\inv\\objcurs2-widths.txt").ok()) {
		auto secondaryWidths = ReadCursorWidthsForNativeOracle("data\\inv\\objcurs2-widths.txt", loadError);
		if (!secondaryWidths.has_value())
			return loadError;
		auto secondaryResult = LoadCelWithStatus("data\\inv\\objcurs2", secondaryWidths->data());
		if (!secondaryResult.has_value())
			return secondaryResult.error();
		secondarySprites.emplace(std::move(secondaryResult.value()));
	}
#endif
	if (!primarySpritesStorage.has_value())
		return "The primary native item cursor sprite list is unavailable.";
	const OwnedClxSpriteList &primarySprites = *primarySpritesStorage;
	const uint32_t primarySpriteCount = primarySprites.numSprites();
	std::vector<bool> emitted(AllItemsList.size(), false);
	const std::filesystem::path path = root / "native-rogue-l1-inventory.tsv";
	std::ofstream table(path, std::ios::binary | std::ios::trunc);
	if (!table.good())
		return "Unable to create the native Rogue inventory footprint sidecar.";
	table << "native_item_index\tnative_cursor_id\tpixel_width\tpixel_height\tinventory_width\tinventory_height\r\n";
	const int firstItemCursor = static_cast<int>(CURSOR_FIRSTITEM);
	for (size_t index = 0; index < AllItemsList.size(); ++index) {
		const ItemData &item = AllItemsList[index];
		if (!IsItemAvailable(static_cast<int>(index)) || item.itype == ItemType::None || item.iCurs == ICURS_DEFAULT)
			continue; // Explicit unavailable rows do not have a selectable cursor footprint.
		const int cursorId = firstItemCursor + static_cast<int>(item.iCurs);
		uint16_t pixelWidth;
		uint16_t pixelHeight;
		if (cursorId <= 0)
			return "A selectable native item has a non-positive cursor id.";
		if (static_cast<uint32_t>(cursorId) <= primarySpriteCount) {
			const ClxSprite sprite = primarySprites[static_cast<size_t>(cursorId - 1)];
			pixelWidth = sprite.width();
			pixelHeight = sprite.height();
		} else if (secondarySprites.has_value()
		    && static_cast<uint32_t>(cursorId) - primarySpriteCount <= secondarySprites->numSprites()) {
			const ClxSprite sprite = (*secondarySprites)[static_cast<size_t>(static_cast<uint32_t>(cursorId) - primarySpriteCount - 1)];
			pixelWidth = sprite.width();
			pixelHeight = sprite.height();
		} else {
			return "A selectable native item cursor has no loaded sprite.";
		}
		if (pixelWidth == 0 || pixelHeight == 0)
			return "A selectable native item cursor has an empty sprite.";
		const int inventoryWidth = pixelWidth / InventorySlotSizeInPixels.width;
		const int inventoryHeight = pixelHeight / InventorySlotSizeInPixels.height;
		if (inventoryWidth <= 0 || inventoryHeight <= 0)
			return "A selectable native item cursor is smaller than one inventory cell.";
		table << index << '\t' << cursorId << '\t' << pixelWidth << '\t' << pixelHeight << '\t'
		      << inventoryWidth << '\t' << inventoryHeight << "\r\n";
		emitted[index] = true;
	}
	if (std::none_of(emitted.begin(), emitted.end(), [](bool value) { return value; }))
		return "The native Rogue inventory footprint sidecar contains no selectable items.";
	const auto requireItem = [&](const Item &item, std::string_view source) -> std::optional<std::string> {
		if (item.isEmpty())
			return std::nullopt;
		const int index = static_cast<int>(item.IDidx);
		if (index < 0 || static_cast<size_t>(index) >= emitted.size() || !emitted[static_cast<size_t>(index)])
			return std::string("A native Rogue ") + std::string(source) + " item has no selectable cursor footprint.";
		return std::nullopt;
	};
	for (uint8_t itemId : ItemPoolAdapter::ActiveItemIds()) {
		if (const auto error = requireItem(Items[itemId], "L1 ground"))
			return error;
	}
	if (MyPlayer != nullptr) {
		for (const Item &item : MyPlayer->InvBody)
			if (const auto error = requireItem(item, "owned equipment")) return error;
		if (MyPlayer->_pNumInv < 0 || MyPlayer->_pNumInv > InventoryGridCells)
			return "The native Rogue inventory count is outside its fixed capacity.";
		for (int index = 0; index < MyPlayer->_pNumInv; ++index)
			if (const auto error = requireItem(MyPlayer->InvList[index], "owned inventory")) return error;
		for (const Item &item : MyPlayer->SpdList)
			if (const auto error = requireItem(item, "owned belt")) return error;
		if (const auto error = requireItem(MyPlayer->HoldItem, "owned cursor"))
			return error;
	}
	table.flush();
	if (!table.good())
		return "Writing the native Rogue inventory footprint sidecar failed.";
	table.close();
	return std::nullopt;
}

void ThemeRoomGenerationTest::CapturePopulationOracle(const char *directory, bool freshLoadOrder, bool levelReady, bool nativeRogueRoute, std::optional<HeroClass> nativeClassRoute)
{
	if (directory == nullptr || !*directory) GTEST_SKIP() << "Select private upstream Cathedral population explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	const auto repository = std::filesystem::weakly_canonical(std::filesystem::absolute(__FILE__).parent_path().parent_path());
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	ASSERT_FALSE(std::mismatch(repository.begin(), repository.end(), root.begin(), root.end()).first == repository.end());
	// Use the same original-asset discovery and controlled hero context as the
	// earlier population fixture. No server or client runtime loads archives.
	NativeComposedPopulationGuard composedGuard;
	std::unique_ptr<NativeLevelReadyGuard> readyGuard;
	if (levelReady) readyGuard = std::make_unique<NativeLevelReadyGuard>();
	// The inherited fixture already owns and initializes the player and tables.
	// Avoid TestInitGame/InitQuests: quests are explicitly disabled below, and
	// their extra quest-dialog/UI state is not part of this population seam.
	MyPlayerId = 0;
	sgGameInitInfo.fullQuests = 1;
	gbIsHellfire = false;
	gbIsHellfireSaveGame = false;
	gbSndInited = false;
	UnloadModArchives();
	MpqArchives.erase(MainMpqPriority);
	gbIsSpawn = false;
	LoadGameArchives();
	if (!HaveMainData() || gbIsSpawn) GTEST_SKIP() << "Original DIABDAT.MPQ was not discovered.";
	currentLevel().setId(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL });
#ifndef UNPACKED_MPQS
	const auto &mainArchive = MpqArchives.at(MainMpqPriority);
	for (const std::string_view path : { "levels\\l1data\\l1.til", "levels\\l1data\\l1.sol" }) {
		const AssetRef asset = FindAsset(path);
		ASSERT_TRUE(asset.ok());
		ASSERT_EQ(asset.archive, &mainArchive);
	}
	if (levelReady) {
		const AssetRef automapAsset = FindAsset("levels\\l1data\\l1.amp");
		ASSERT_TRUE(automapAsset.ok());
		ASSERT_EQ(automapAsset.archive, &mainArchive);
	}
#endif
	size_t tileCount = 0;
	auto definitions = LoadFileInMemWithStatus<MegaTile>("levels\\l1data\\l1.til", &tileCount);
	ASSERT_TRUE(definitions.has_value());
	ASSERT_NE(definitions->get(), nullptr);
	ASSERT_EQ(tileCount, 206U);
	megaTiles() = std::move(*definitions);
	for (int piece = 0; piece < MAXTILES; ++piece)
		tileProperties()[piece] = TileProperties::None;
	ASSERT_TRUE(LoadLevelSOLData().has_value());
	ASSERT_TRUE(CurrentLightManager.LoadTrns().has_value());
	CurrentLightManager.MakeLightTable();
	LoadLevelGenerationData();
	if (freshLoadOrder) LoadPlayerDataFiles();
	// New class tuples use actual native character creation, independently of the
	// managed profile. The historical prepared Rogue tuple remains unchanged.
	Player canonicalHero;
	if (nativeClassRoute.has_value()) {
		ASSERT_TRUE(*nativeClassRoute == HeroClass::Warrior || *nativeClassRoute == HeroClass::Sorcerer);
		canonicalHero.create(*nativeClassRoute);
	}
	const bool playableRoute = nativeRogueRoute || nativeClassRoute.has_value();
	const int maximumLife = nativeClassRoute.has_value() ? canonicalHero.life.maximumBase : nativeRogueRoute ? 2880
	    : freshLoadOrder                                                                                     ? 6400
	                                                                                                         : 64;
	const int maximumMana = nativeClassRoute.has_value() ? canonicalHero.mana.maximumBase : nativeRogueRoute ? 1440
	    : freshLoadOrder                                                                                     ? 3200
	                                                                                                         : 0;
	const int lightRadius = nativeClassRoute.has_value() ? canonicalHero._pLightRad : 8;
	const char *fileName = nativeClassRoute.has_value() ? "native-class-l1-population.json" : nativeRogueRoute ? "native-rogue-l1-population.json"
	    : levelReady                                                                                           ? "native-level-ready.json"
	                                                                                                           : (freshLoadOrder ? "native-fresh-population.json" : "native-full-population.json");
	std::ofstream stream(root / fileName, std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int cases = 0, multiRoomCases = 0;
	{
		JsonObject document(stream);
		document.String("format", nativeClassRoute.has_value() ? "godevilution-cathedral-native-class-l1" : nativeRogueRoute ? "godevilution-cathedral-native-rogue-l1"
		        : levelReady                                                                                                 ? "godevilution-cathedral-level-ready"
		                                                                                                                     : (freshLoadOrder ? "godevilution-cathedral-fresh-population" : "godevilution-cathedral-full-population"));
		document.Number("version", 1);
		document.String("policy", nativeClassRoute.has_value() ? "fresh-cold-original-diablo-class-l1-main-seed2588" : nativeRogueRoute ? "fresh-cold-original-diablo-rogue-l1-main-seed2588"
		        : levelReady                                                                                                            ? "fresh-cold-original-diablo-single-player-normal-post-theme-through-light-vision-before-join"
		                                                                                                                                : (freshLoadOrder ? "fresh-cold-original-diablo-single-player-normal-clear-before-createlevel-through-createthemerooms" : "controlled-original-diablo-single-player-normal-post-createlevel-through-createthemerooms"));
		document.Number("playerMaximumLife", maximumLife);
		document.Number("playerMaximumMana", maximumMana);
		if (nativeClassRoute.has_value()) document.Number("playerClassId", static_cast<int>(*nativeClassRoute));
		if (freshLoadOrder) {
			document.Number("playerLightRadius", lightRadius);
			document.Number("storySeed", UINT32_C(0x13579BDF));
			document.Key("playerFuture");
			WriteRoomPoint(stream, playableRoute ? Point { 57, 40 } : Point { 75, 68 });
			document.Boolean("playerLevelChanging", true);
		}
		if (levelReady) {
			document.Key("coldPlayer");
			JsonObject context(stream);
			context.Number("spellFlags", 255);
			context.Boolean("infravision", true);
			context.Boolean("autoMapShowItems", true);
			context.Boolean("missilePreFlag", true);
			context.Number("currentLife", 6400);
			context.Boolean("priorMissilesEmpty", true);
			context.Boolean("portalsAbsent", true);
			context.Key("last");
			WriteRoomPoint(stream, { 13, 17 });
			// InitPlayer's ClearStateVariables clears the pre-load Temp (19,23).
			context.Key("temp");
			WriteRoomPoint(stream, { 0, 0 });
		}
		document.Key("itemCatalog");
		WriteCatalog(stream);
		document.Key("cases");
		stream << '[';
		for (int depth = 1; depth <= 4; ++depth)
			for (const uint32_t seed : { UINT32_C(2588), UINT32_C(743271966) })
				for (const lvl_entry entry : { ENTRY_MAIN, ENTRY_PREV }) {
					if (playableRoute && (depth != 1 || seed != UINT32_C(2588) || entry != ENTRY_MAIN)) continue;
					FreeMonsters();
					InitializeObjectPool();
					ObjectPoolAdapter::ResetLegacyObjectPools();
					for (int slot = 0; slot < MAXOBJECTS; ++slot)
						Objects[slot] = {};
					for (auto &type : LevelBestiary.types)
						type = {};
					InitializeMonsterPool();
					for (Monster &monster : MonsterPoolAdapter::AllMonsters())
						monster = {};
					// InitializeMonsterPool leaves inactive IDs untouched. This controlled
					// baseline explicitly prepares identity IDs with count zero and no
					// RNG draws; InitLevelMonsters independently resets them again below.
					for (unsigned slot = 0; slot < MaxMonsters; ++slot)
						ActiveMonsters[slot] = slot;
					InitializeItemPool();
					ItemPoolAdapter::ResetLegacyItemPools();
					for (int slot = 0; slot <= MAXITEMS; ++slot)
						Items[slot] = {};
					ClearUniqueItemFlags();
					CurrentLightManager.Init();
					// This new oracle explicitly models cold storage, not retained level
					// pools. Init() alone intentionally does not clear inactive light records.
					if (freshLoadOrder) {
						std::fill(std::begin(CurrentLightManager.lights_), std::end(CurrentLightManager.lights_), Light {});
						std::fill(std::begin(CurrentLightManager.visionList_), std::end(CurrentLightManager.visionList_), Light {});
					}
					for (Tile &tile : tiles()) {
						tile.setObject(0);
						tile.setMonster(0);
						tile.setPlayer(0);
						tile.setItem(0);
					}
					currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATHEDRAL });
					LevelSeeds[depth] = std::nullopt;
					DungeonSeeds[depth] = seed;
					DungeonSeeds[16] = UINT32_C(0x13579BDF);
					if (nativeClassRoute.has_value()) *MyPlayer = std::move(canonicalHero);
					MyPlayer->pOriginalCathedral = true;
					MyPlayer->plractive = true;
					MyPlayer->_pClass = nativeClassRoute.value_or(nativeRogueRoute ? HeroClass::Rogue : HeroClass::Warrior);
					MyPlayer->plrlevel = static_cast<uint8_t>(depth);
					MyPlayer->_pLvlChanging = freshLoadOrder;
					MyPlayer->plrIsOnSetLevel = false;
					MyPlayer->life = { 64, 64, 64, 64, 100 };
					if (freshLoadOrder) MyPlayer->life = { maximumLife, maximumLife, maximumLife, maximumLife, 100 };
					MyPlayer->mana = {};
					if (freshLoadOrder) MyPlayer->mana.maximumBase = maximumMana;
					MyPlayer->pLvlLoad = 1;
					MyPlayer->_pLightRad = lightRadius;
					if (freshLoadOrder) MyPlayer->position.future = { static_cast<uint8_t>(playableRoute ? 57 : 75), static_cast<uint8_t>(playableRoute ? 40 : 68) };
					gbIsMultiplayer = false;
					gbIsSpawn = false;
					gbIsHellfire = false;
					gbIsHellfireSaveGame = false;
					LoadingMapObjects = false;
					sgGameInitInfo.nDifficulty = DIFF_NORMAL;
					for (Quest &quest : Quests)
						quest._qactive = QUEST_NOTAVAIL;
					RestoreThemeRoomGlobalsForTesting({});
					armorFlag() = true;
					weaponFlag() = true;
					uint32_t preClearState = 0, postClearState = 0;
					if (freshLoadOrder) {
						SetRndSeed(seed);
						preClearState = GetLCGEngineState();
						InitLevelMonsters();
						postClearState = GetLCGEngineState();
					}
					if (levelReady) {
						Missiles.clear();
						MissilePreFlag = true;
						std::fill(std::begin(Corpses), std::end(Corpses), Corpse {});
						stonendx = 0;
						std::fill(std::begin(Portals), std::end(Portals), Portal {});
						RestoreAutomapDefinitionsForTesting({});
						CurrentAutomapManager.InitAutomap();
						CurrentAutomapManager.SetAutoMapShowItems(true);
						MyPlayer->_pSpellFlags = static_cast<SpellFlag>(255);
						MyPlayer->_pInfraFlag = true;
						if (cases == 0) {
							std::ofstream metadata(root / "cathedral_automap.tsv", std::ios::binary | std::ios::trunc);
							ASSERT_TRUE(metadata.good());
							metadata << "id\ttype\tflags\r\n";
							const auto mapping = CaptureAutomapDefinitionsForTesting();
							for (size_t index = 0; index < mapping.size(); ++index)
								metadata << index << '\t' << (mapping[index] & 0xFF) << '\t' << (mapping[index] >> 8) << "\r\n";
							metadata.flush();
							ASSERT_TRUE(metadata.good());
						}
					}
					CreateLevelForTesting(entry);
					MyPlayer->position.tile = viewPosition();
					if (!freshLoadOrder) MyPlayer->position.future = viewPosition();
					MyPlayer->position.old = viewPosition();
					MyPlayer->position.last = viewPosition();
					if (levelReady) {
						MyPlayer->position.last = { 13, 17 };
						MyPlayer->position.temp = { 19, 23 };
					}
					if (cases++ != 0) stream << ',';
					JsonObject row(stream);
					row.Number("depth", depth);
					row.Number("seed", seed);
					row.String("entry", entry == ENTRY_MAIN ? "main" : "previous");
					row.String("snapshot", "cathedral-population-depth-" + std::to_string(depth) + "-" + std::to_string(seed) + (entry == ENTRY_MAIN ? "-main.json" : "-previous.json"));
					if (!freshLoadOrder) {
						preClearState = GetLCGEngineState();
						row.Number("preClearRandomState", preClearState);
						row.Key("baseline");
						WriteRoomWorld(stream);
						InitLevelMonsters();
						postClearState = GetLCGEngineState();
					} else {
						row.Number("preClearRandomState", preClearState);
						row.Number("nextTransparencyValue", nextTransparencyValue());
						row.Key("triggerPositions");
						stream << '[';
						for (int trigger = 0; trigger < numTriggers(); ++trigger) {
							if (trigger) stream << ',';
							WriteRoomPoint(stream, trigs[trigger].position);
						}
						stream << ']';
					}
					row.Number("postClearRandomState", postClearState);
					SetRndSeed(seed);
					ASSERT_TRUE(GetLevelMTypes().has_value());
					InitThemes();
					if (freshLoadOrder) {
						row.Number("prePlayerRandomState", GetLCGEngineState());
						// Actual player initialization allocates slot 0 before unique lights.
						// false avoids first-load hotkey/UI storage, without changing dungeon
						// placement, animation draws, light allocation or vision activation.
						InitPlayer(*MyPlayer, false);
						ASSERT_EQ(MyPlayer->lightId, 0);
						row.Number("playerIdleFrames", MyPlayer->_pNFrames);
						row.Number("postPlayerRandomState", GetLCGEngineState());
						row.Key("playerAnimation");
						stream << '[' << static_cast<int>(MyPlayer->animInfo.currentFrame)
						       << ',' << static_cast<int>(MyPlayer->animInfo.tickCounterOfCurrentFrame)
						       << ',' << static_cast<int>(MyPlayer->animInfo.numberOfFrames)
						       << ',' << static_cast<int>(MyPlayer->animInfo.ticksPerFrame)
						       << ',' << (MyPlayer->animInfo.isPetrified ? "true" : "false") << ']';
						row.Key("visionActive");
						WriteArray(stream, CurrentLightManager.visionActive_, [&](bool active) { stream << (active ? "true" : "false"); });
						row.Boolean("updateVision", CurrentLightManager.VisionUpdatePendingForTesting());
						row.Key("visionRecords");
						int visionId = 0;
						WriteArray(stream, CurrentLightManager.visionList_, [&](const Light &vision) {
							JsonObject record(stream);
							record.Number("lightId", visionId++);
							record.Key("tile");
							WriteRoomPoint(stream, { vision.position.tile.x, vision.position.tile.y });
							record.Key("oldTile");
							WriteRoomPoint(stream, { vision.position.old.x, vision.position.old.y });
							record.Number("offsetX", vision.position.offset.deltaX);
							record.Number("offsetY", vision.position.offset.deltaY);
							record.Number("radius", vision.radius);
							record.Number("oldRadius", vision.oldRadius);
							record.Boolean("isInvalid", vision.isInvalid);
							record.Boolean("hasChanged", vision.hasChanged);
						});
						row.Key("baseline");
						WriteRoomWorld(stream);
					}
					row.Key("selectedRoomCatalog");
					WriteRoomCatalog(stream);
					row.Key("selectedThemes");
					stream << '[';
					for (int i = 0; i < numThemes(); ++i) {
						if (i) stream << ',';
						stream << '[' << static_cast<int>(themes[i].ttype) << ',' << static_cast<int>(themes[i].ttval) << ']';
					}
					stream << ']';
					if (numThemes() > 1) ++multiRoomCases;
					SetRndSeed(seed);
					HoldThemeRooms();
					InitGolems();
					InitObjects();
					ASSERT_TRUE(InitMonsters().has_value());
					InitItems();
					row.Key("afterItems");
					WriteRoomWorld(stream);
					row.Key("itemInitialization");
					{
						const auto state = CaptureItemInitializationGlobalsForTesting();
						JsonObject init(stream);
						init.Boolean("showUniqueInfo", state.showUniqueInfo);
						init.Number("recordCount", state.count);
						init.Key("records");
						WriteArray(stream, state.records, [&](const ItemGetRecordStruct &record) {
							stream << '[' << record.nSeed << ',' << record.wCI << ',' << record.nIndex << ',' << record.dwTimestamp << ']';
						});
					}
					row.Key("visibleTransparencyRegions");
					WriteArray(stream, visibleTransparencyRegions(), [&](bool visible) { stream << (visible ? "true" : "false"); });
					CreateThemeRooms();
					row.Key("expected");
					WriteRoomWorld(stream);
					if (levelReady) {
						const uint32_t before = GetLCGEngineState();
						CompleteFreshLevelForTesting(entry);
						ASSERT_EQ(GetLCGEngineState(), before);
						row.Key("ready");
						WriteLevelReadyState(stream);
					}
				}
		stream << ']';
		document.Number("caseCount", cases);
		document.Number("multiRoomCases", multiRoomCases);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(cases, playableRoute ? 1 : 16);
	if (!playableRoute) EXPECT_GT(multiRoomCases, 0);
	EXPECT_LE(std::filesystem::file_size(root / fileName), 128U * 1024U * 1024U);
	if (nativeRogueRoute) {
		const auto footprintError = WriteNativeRogueInventoryFootprints(root);
		ASSERT_FALSE(footprintError.has_value()) << footprintError.value_or("unknown cursor footprint failure");
		EXPECT_LE(std::filesystem::file_size(root / "native-rogue-l1-inventory.tsv"), 4U * 1024U * 1024U);
	}
}

#ifndef UNPACKED_MPQS
struct NativeCatacombsPopulationAssetGuard {
	std::string assetsPath = paths::AssetsPath();
	std::vector<std::string> overrides = std::move(OverridePaths);
	decltype(MpqArchives) archives = std::move(MpqArchives);
	~NativeCatacombsPopulationAssetGuard()
	{
		MpqArchives.clear();
		MpqArchives = std::move(archives);
		OverridePaths = std::move(overrides);
		paths::SetAssetsPath(assetsPath);
	}
};

struct NativeCatacombsPopulationOutputGuard {
	std::filesystem::path root;
	bool ownsRoot = false;
	bool complete = false;
	bool WriteStatus(std::string_view value) const
	{
		std::ofstream status(root / "capture-status.json", std::ios::binary | std::ios::trunc);
		if (!status.good())
			return false;
		{
			JsonObject document(status);
			document.String("status", value);
		}
		status << "\r\n";
		status.flush();
		if (!status.good())
			return false;
		status.close();
		return status.good();
	}
	~NativeCatacombsPopulationOutputGuard()
	{
		if (ownsRoot && !complete)
			WriteStatus("incomplete");
	}
};

struct NativeCatacombsPopulationCase {
	HeroClass heroClass;
	int depth;
	lvl_entry entry;
	uint32_t seed;
	bool retainedUniqueHistory;
	bool aiEligibilityCoverage = false;
};

constexpr char NativeCatacombsPreparedInventorySeedPolicy[] = "catacombs-class-storage-slot-v1";

// Pin only starter-item identity seeds; native population uses independent world RNG state.
void ApplyNativeCatacombsPreparedInventorySeedPolicy(Player &player)
{
	const uint32_t classComponent = (static_cast<uint32_t>(player._pClass) & 0xFFU) << 16;
	auto assignContainer = [&](auto &items, uint32_t container) {
		uint32_t slot = 0;
		for (Item &item : items) {
			if (!item.isEmpty())
				item._iSeed = 0xC2000000U | classComponent | (container << 8) | slot;
			++slot;
		}
	};
	assignContainer(player.InvBody, 1);
	assignContainer(player.InvList, 2);
	assignContainer(player.SpdList, 3);
	if (!player.HoldItem.isEmpty())
		player.HoldItem._iSeed = 0xC2000000U | classComponent | (4U << 8);
}

std::vector<uint32_t> CaptureNativeCatacombsPlayerItemSeeds(const Player &player)
{
	std::vector<uint32_t> seeds;
	for (const Item &item : player.InvBody)
		seeds.push_back(item._iSeed);
	for (const Item &item : player.InvList)
		seeds.push_back(item._iSeed);
	for (const Item &item : player.SpdList)
		seeds.push_back(item._iSeed);
	seeds.push_back(player.HoldItem._iSeed);
	return seeds;
}

const char *CatacombsClassSymbol(HeroClass heroClass)
{
	return heroClass == HeroClass::Warrior ? "warrior" : "sorcerer";
}

const char *CatacombsEntrySymbol(lvl_entry entry)
{
	return entry == ENTRY_MAIN ? "main" : entry == ENTRY_PREV ? "previous"
	                                                          : "town-warp-down";
}

void WriteCatacombsPlayer(std::ostream &out, const Player &player)
{
	JsonObject json(out);
	json.Number("classId", player._pClass);
	json.Number("characterLevel", player.getCharacterLevel());
	json.Number("dungeonLevel", player.plrlevel);
	json.Number("experience", player._pExperience);
	json.Number("lifeBase", player.life.base);
	json.Number("lifeCurrent", player.life.current);
	json.Number("lifeMaximum", player.life.maximum);
	json.Number("lifeMaximumBase", player.life.maximumBase);
	json.Number("manaBase", player.mana.base);
	json.Number("manaCurrent", player.mana.current);
	json.Number("manaMaximum", player.mana.maximum);
	json.Number("manaMaximumBase", player.mana.maximumBase);
	json.Number("strength", player.attributes.strength.base);
	json.Number("magic", player.attributes.magic.base);
	json.Number("dexterity", player.attributes.dexterity.base);
	json.Number("vitality", player.attributes.vitality.base);
	json.Number("statPoints", player._pStatPts);
	json.Number("gold", player._pGold);
	json.Number("lightRadius", player._pLightRad);
	json.Number("lightId", player.lightId);
	json.Number("mode", player._pmode);
	json.Number("direction", player.direction);
	json.Number("pLvlLoad", player.pLvlLoad);
	json.Number("spellFlags", player._pSpellFlags);
	json.Number("townWarps", player.pTownWarps);
	json.Boolean("active", player.plractive);
	json.Boolean("levelChanging", player._pLvlChanging);
	json.Boolean("originalCathedral", player.pOriginalCathedral);
	json.Key("positionTile");
	WriteRoomPoint(out, { player.position.tile.x, player.position.tile.y });
	json.Key("positionFuture");
	WriteRoomPoint(out, { player.position.future.x, player.position.future.y });
	json.Key("positionOld");
	WriteRoomPoint(out, { player.position.old.x, player.position.old.y });
	json.Key("positionLast");
	WriteRoomPoint(out, { player.position.last.x, player.position.last.y });
	json.Key("positionTemp");
	WriteRoomPoint(out, { player.position.temp.x, player.position.temp.y });
	json.Number("inventoryCount", player._pNumInv);
	json.Key("bodyItems");
	WriteArray(out, player.InvBody, [&](const Item &item) { WriteItem(out, item); });
	json.Key("inventoryItems");
	WriteArray(out, player.InvList, [&](const Item &item) { WriteItem(out, item); });
	json.Key("beltItems");
	WriteArray(out, player.SpdList, [&](const Item &item) { WriteItem(out, item); });
	json.Key("cursorItem");
	WriteItem(out, player.HoldItem);
	json.Key("inventoryGrid");
	WriteArray(out, player.InvGrid, [&](int8_t value) { out << static_cast<int>(value); });
	json.Key("spellLevels");
	WriteArray(out, player._pSplLvl, [&](uint8_t value) { out << static_cast<int>(value); });
	json.Key("animation");
	{
		JsonObject animation(out);
		animation.Number("currentFrame", player.animInfo.currentFrame);
		animation.Number("tickCounterOfCurrentFrame", player.animInfo.tickCounterOfCurrentFrame);
		animation.Number("frameCount", player.animInfo.numberOfFrames);
		animation.Number("ticksPerFrame", player.animInfo.ticksPerFrame);
		animation.Boolean("isPetrified", player.animInfo.isPetrified);
	}
}

void WriteCatacombsBestiary(std::ostream &out)
{
	out << '[';
	for (size_t index = 0; index < LevelBestiary.size(); ++index) {
		if (index != 0)
			out << ',';
		const CMonster &entry = LevelBestiary[index];
		const MonsterData &source = entry.data();
		JsonObject json(out);
		json.Number("index", index);
		json.Number("monsterType", entry.type);
		json.Number("placeFlags", entry.placeFlags);
		json.Number("corpseId", entry.corpseId);
		json.String("name", source.name);
		json.Number("spriteId", source.spriteId);
		json.Number("nativeAi", source.ai);
		json.Number("availability", source.availability);
		json.Number("imageCost", source.image);
		json.Number("minimumDungeonLevel", source.minDunLvl);
		json.Number("maximumDungeonLevel", source.maxDunLvl);
		json.Number("treasure", source.treasure);
		const std::optional<MissileID> missile = AiMissileType(source.ai);
		if (missile.has_value())
			json.Number("aiMissileId", missile.value());
		else {
			json.Key("aiMissileId");
			out << "null";
		}
		json.Key("sourceFrames");
		WriteArray(out, source.frames, [&](int8_t value) { out << static_cast<int>(value); });
		json.Key("sourceRates");
		WriteArray(out, source.rate, [&](int8_t value) { out << static_cast<int>(value); });
		json.Key("animations");
		out << '[';
		for (size_t animation = 0; animation < std::size(entry.anims); ++animation) {
			if (animation != 0)
				out << ',';
			const AnimStruct &data = entry.anims[animation];
			out << '[' << data.width << ',' << static_cast<int>(data.frames) << ',' << static_cast<int>(data.rate) << ']';
		}
		out << ']';
	}
	out << ']';
}

void WriteCatacombsThemeState(std::ostream &out)
{
	JsonObject json(out);
	json.Key("selected");
	out << '[';
	for (int index = 0; index < numThemes(); ++index) {
		if (index != 0)
			out << ',';
		out << '[' << static_cast<int>(themes[index].ttype) << ',' << static_cast<int>(themes[index].ttval) << ']';
	}
	out << ']';
	json.Number("roomCount", themeCount());
	json.Key("rooms");
	out << '[';
	for (int index = 0; index < themeCount(); ++index) {
		if (index != 0)
			out << ',';
		const THEME_LOC &location = themeLocations()[index];
		JsonObject room(out);
		room.Key("position");
		WriteRoomPoint(out, { location.room.position.x, location.room.position.y });
		room.Key("size");
		out << '[' << static_cast<int>(location.room.size.width) << ',' << static_cast<int>(location.room.size.height) << ']';
		room.Number("transparency", location.ttval);
	}
	out << ']';
}

void WriteCatacombsResetRecords(std::ostream &out)
{
	const MonsterPopulationGlobalsForTesting monsters = CaptureMonsterPopulationGlobalsForTesting();
	const ItemInitializationGlobalsForTesting items = CaptureItemInitializationGlobalsForTesting();
	JsonObject json(out);
	json.Key("monsterPopulation");
	{
		JsonObject state(out);
		state.Number("totalMonsters", monsters.totalMonsters);
		state.Number("imageTotal", monsters.imageTotal);
		state.Number("uniqueTransparency", monsters.uniqueTransparency);
	}
	json.Key("itemInitialization");
	{
		JsonObject state(out);
		state.Boolean("showUniqueInfo", items.showUniqueInfo);
		state.Number("recordCount", items.count);
		state.Key("records");
		WriteArray(out, items.records, [&](const ItemGetRecordStruct &record) {
			out << '[' << record.nSeed << ',' << record.wCI << ',' << record.nIndex << ',' << record.dwTimestamp << ']';
		});
	}
}

void WriteCatacombsRngBoundary(std::ostream &out, bool &first, std::string_view name, uint32_t value)
{
	if (!first)
		out << ',';
	first = false;
	JsonString(out, name);
	out << ':' << value;
}
#endif

void ThemeRoomGenerationTest::CaptureNativeCatacombsPopulationOracle(const char *archivePath, const char *directory, bool captureStages)
{
	if (archivePath == nullptr || archivePath[0] == '\0' || directory == nullptr || directory[0] == '\0')
		GTEST_SKIP() << "Set both explicit Catacombs population archive and new reference directory variables.";
#ifdef UNPACKED_MPQS
	GTEST_SKIP() << "The Catacombs population oracle requires the explicit MPQ archive backend.";
#else
	const std::filesystem::path explicitArchive = std::filesystem::absolute(archivePath).lexically_normal();
	ASSERT_TRUE(std::filesystem::is_regular_file(explicitArchive)) << explicitArchive;
	NativeCatacombsPopulationOutputGuard outputGuard;
	outputGuard.root = std::filesystem::absolute(directory).lexically_normal();
	ASSERT_TRUE(outputGuard.root.is_absolute());
	ASSERT_FALSE(std::filesystem::exists(outputGuard.root)) << "Refusing to overwrite an existing Catacombs population reference directory.";
	ASSERT_TRUE(std::filesystem::is_directory(outputGuard.root.parent_path()))
	    << "Create the reference directory parent before opting into native capture.";

	NativeComposedPopulationGuard composedGuard;
	NativeLevelReadyGuard readyGuard;
	NativeItemWorldGuard itemWorldGuard;
	NativeCatacombsPopulationAssetGuard assetGuard;
	std::error_code createError;
	outputGuard.ownsRoot = std::filesystem::create_directory(outputGuard.root, createError);
	ASSERT_TRUE(outputGuard.ownsRoot) << "Could not create a fresh native population reference directory: " << createError.message();
	ASSERT_TRUE(outputGuard.WriteStatus("capturing")) << "Could not write the capture status marker.";
	const std::filesystem::path repository = std::filesystem::weakly_canonical(std::filesystem::absolute(__FILE__).parent_path().parent_path());
	const std::string repositoryAssets = (repository / "assets").string() + "/";
	paths::SetAssetsPath(repositoryAssets);
	OverridePaths = { repositoryAssets };
	MpqArchives.clear();
	std::int32_t archiveOpenError = 0;
	auto archive = MpqArchive::Open(explicitArchive.string().c_str(), archiveOpenError);
	ASSERT_TRUE(archive.has_value()) << "Could not open the explicitly selected original archive: " << MpqArchive::ErrorMessage()
	                                 << " (" << archiveOpenError << ')';
	const auto [archiveIt, archiveInserted] = MpqArchives.emplace(MainMpqPriority, std::move(archive.value()));
	ASSERT_TRUE(archiveInserted);
	MpqArchive &mainArchive = archiveIt->second;

	currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
	ASSERT_TRUE(FindAsset("levels\\l2data\\l2.til").ok());
	ASSERT_EQ(FindAsset("levels\\l2data\\l2.til").archive, &mainArchive);
	ASSERT_TRUE(FindAsset("levels\\l2data\\l2.sol").ok());
	ASSERT_EQ(FindAsset("levels\\l2data\\l2.sol").archive, &mainArchive);
	for (const char *translationTable : { "plrgfx\\infra.trn", "plrgfx\\stone.trn" }) {
		const AssetRef translationAsset = FindAsset(translationTable);
		ASSERT_TRUE(translationAsset.ok()) << translationTable;
		ASSERT_EQ(translationAsset.archive, &mainArchive) << translationTable;
	}
	// Pause is a bundled engine translation table, not an original MPQ asset.
	// Pin the production lookup to the canonical loose resource bytes.
	std::array<uint8_t, 256> expectedPause {};
	std::array<uint8_t, 256> loadedPause {};
	const auto pausePath = repository / "assets/gendata/pause.trn";
	ASSERT_EQ(std::filesystem::file_size(pausePath), expectedPause.size());
	std::ifstream pauseSource(pausePath, std::ios::binary);
	pauseSource.read(reinterpret_cast<char *>(expectedPause.data()), expectedPause.size());
	ASSERT_TRUE(pauseSource.good());
	pauseSource.close();
	ASSERT_TRUE(pauseSource.good());
	const auto pauseAsset = FindAsset("gendata\\pause.trn");
	ASSERT_TRUE(pauseAsset.ok());
	ASSERT_EQ(pauseAsset.archive, nullptr);
	ASSERT_EQ(pauseAsset.size(), loadedPause.size());
	auto pauseHandle = OpenAsset(FindAsset("gendata\\pause.trn"));
	ASSERT_TRUE(pauseHandle.ok());
	ASSERT_TRUE(pauseHandle.read(loadedPause.data(), loadedPause.size()));
	ASSERT_EQ(loadedPause, expectedPause);
	ASSERT_TRUE(FindAsset("levels\\l2data\\l2.amp").ok());
	ASSERT_EQ(FindAsset("levels\\l2data\\l2.amp").archive, &mainArchive);
	const AssetRef automapAsset = FindAsset("levels\\l2data\\l2.amp");
	ASSERT_EQ(automapAsset.archive, &mainArchive);
	const size_t sourceAutomapByteCount = automapAsset.size();
	ASSERT_GT(sourceAutomapByteCount, 0U);
	ASSERT_LE(sourceAutomapByteCount, 4U * 1024U * 1024U);
	ASSERT_EQ(sourceAutomapByteCount % sizeof(uint16_t), 0U);
	AssetHandle automapHandle = OpenAsset(FindAsset("levels\\l2data\\l2.amp"));
	ASSERT_TRUE(automapHandle.ok());
	std::vector<uint8_t> sourceAutomap(sourceAutomapByteCount);
	ASSERT_TRUE(automapHandle.read(sourceAutomap.data(), sourceAutomap.size()));
	std::vector<uint16_t> sourceAutomapWords(sourceAutomapByteCount / sizeof(uint16_t));
	for (size_t word = 0; word < sourceAutomapWords.size(); ++word) {
		sourceAutomapWords[word] = static_cast<uint16_t>(sourceAutomap[word * 2])
		    | static_cast<uint16_t>(static_cast<uint16_t>(sourceAutomap[word * 2 + 1]) << 8);
	}
	const AssetRef solAsset = FindAsset("levels\\l2data\\l2.sol");
	ASSERT_EQ(solAsset.archive, &mainArchive);
	const size_t sourceSolByteCount = solAsset.size();
	ASSERT_EQ(sourceSolByteCount, 559U);
	AssetHandle solHandle = OpenAsset(FindAsset("levels\\l2data\\l2.sol"));
	ASSERT_TRUE(solHandle.ok());
	std::vector<uint8_t> sourceSol(sourceSolByteCount);
	ASSERT_TRUE(solHandle.read(sourceSol.data(), sourceSol.size()));
	const size_t reservedSolFlagCount = static_cast<size_t>(std::count_if(sourceSol.begin(), sourceSol.end(), [](uint8_t value) { return (value & 0x40) != 0; }));
	ASSERT_GT(reservedSolFlagCount, 0U) << "The raw Catacombs SOL should preserve native reserved 0x40 bits.";

	const AssetRef tileAsset = FindAsset("levels\\l2data\\l2.til");
	ASSERT_EQ(tileAsset.archive, &mainArchive);
	ASSERT_EQ(tileAsset.size(), 160U * sizeof(MegaTile));
	size_t megaTileCount = 0;
	auto tileDefinitions = LoadFileInMemWithStatus<MegaTile>("levels\\l2data\\l2.til", &megaTileCount);
	ASSERT_TRUE(tileDefinitions.has_value());
	ASSERT_NE(tileDefinitions->get(), nullptr);
	ASSERT_EQ(megaTileCount, 160U);
	std::vector<MegaTile> sourceMegaTiles(megaTileCount);
	std::copy_n(tileDefinitions->get(), megaTileCount, sourceMegaTiles.begin());
	megaTiles() = std::move(tileDefinitions.value());
	std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
	ASSERT_TRUE(LoadLevelSOLData().has_value());
	for (size_t piece = 0; piece < MAXTILES; ++piece) {
		const uint8_t expected = piece < sourceSol.size() ? sourceSol[piece] : 0;
		ASSERT_EQ(static_cast<uint8_t>(tileProperties()[piece]), expected) << "SOL piece " << piece;
	}
	ASSERT_TRUE(CurrentLightManager.LoadTrns().has_value());
	CurrentLightManager.MakeLightTable();
	LoadLevelGenerationData();
	LoadObjectData();
	LoadMonsterData();
	LoadItemData();
	LoadSpellData();
	LoadPlayerDataFiles();
	ASSERT_EQ(AllObjects.size(), 109U);
	ASSERT_FALSE(MonstersData.empty());
	ASSERT_FALSE(AllItemsList.empty());

	std::filesystem::create_directories(outputGuard.root / "cases");
	{
		std::ofstream source(outputGuard.root / "native-catacombs-sources.json", std::ios::binary | std::ios::trunc);
		ASSERT_TRUE(source.good());
		{
			JsonObject document(source);
			document.String("format", "godevilution-native-catacombs-population-sources");
			document.Number("version", 1);
			document.Number("tileDefinitionCount", megaTileCount);
			document.Key("tileDefinitions");
			WriteArray(source, sourceMegaTiles, [&](const MegaTile &tile) {
				source << '[' << tile.micro1 << ',' << tile.micro2 << ',' << tile.micro3 << ',' << tile.micro4 << ']';
			});
			document.Number("automapSourceByteCount", sourceAutomap.size());
			document.Number("automapWordCount", sourceAutomapWords.size());
			document.Key("automapRawWordsLittleEndian");
			WriteArray(source, sourceAutomapWords, [&](uint16_t value) { source << value; });
			document.Number("solSourceByteCount", sourceSol.size());
			document.Number("solEffectiveSlotCount", MAXTILES);
			document.Number("solReserved0x40Count", reservedSolFlagCount);
			document.Key("solRawBytes");
			WriteArray(source, sourceSol, [&](uint8_t value) { source << static_cast<unsigned int>(value); });
			document.Key("solEffectiveProperties");
			WriteArray(source, tileProperties(), [&](TileProperties property) { source << static_cast<unsigned int>(property); });
		}
		source << "\r\n";
		source.flush();
		ASSERT_TRUE(source.good());
		source.close();
		ASSERT_TRUE(source.good());
	}

	std::vector<NativeCatacombsPopulationCase> cases;
	for (const uint32_t seed : { UINT32_C(2588), UINT32_C(743271966) })
		for (int depth = 5; depth <= 8; ++depth)
			for (const lvl_entry entry : { ENTRY_MAIN, ENTRY_PREV })
				cases.push_back({ HeroClass::Warrior, depth, entry, seed, false });
	for (int depth = 5; depth <= 8; ++depth)
		cases.push_back({ HeroClass::Sorcerer, depth, ENTRY_MAIN, UINT32_C(2588), false });
	for (const HeroClass heroClass : { HeroClass::Warrior, HeroClass::Sorcerer })
		cases.push_back({ heroClass, 5, ENTRY_TWARPDN, UINT32_C(2588), false });
	for (const HeroClass heroClass : { HeroClass::Warrior, HeroClass::Sorcerer })
		for (int depth = 5; depth <= 8; ++depth)
			cases.push_back({ heroClass, depth, ENTRY_MAIN, UINT32_C(2588), true });
	cases.push_back({ HeroClass::Warrior, 8, ENTRY_MAIN, UINT32_C(0), false, true });
	cases.push_back({ HeroClass::Warrior, 8, ENTRY_MAIN, UINT32_C(1), false, true });
	ASSERT_EQ(cases.size(), 32U);
	std::array<bool, 128> carriedHistory {};
	std::vector<NativeCatacombsPopulationCase> indexRows;
	indexRows.reserve(cases.size());
	for (const NativeCatacombsPopulationCase &spec : cases) {
		if (spec.retainedUniqueHistory && spec.depth != 5)
			std::copy(carriedHistory.begin(), carriedHistory.end(), std::begin(UniqueItemFlags));
		else {
			ClearUniqueItemFlags();
			if (spec.retainedUniqueHistory)
				UniqueItemFlags[PreparedCatacombsUniqueHistoryId] = true;
		}
		const std::array<bool, 128> historyAtCaseStart = CaptureFlags();
		if (spec.retainedUniqueHistory)
			ASSERT_TRUE(historyAtCaseStart[PreparedCatacombsUniqueHistoryId]);
		else
			ASSERT_EQ(std::count(historyAtCaseStart.begin(), historyAtCaseStart.end(), true), 0);
		FreeMonsters();
		InitializeObjectPool();
		ObjectPoolAdapter::ResetLegacyObjectPools();
		for (int slot = 0; slot < MAXOBJECTS; ++slot)
			Objects[slot] = {};
		for (CMonster &type : LevelBestiary.types)
			type = {};
		InitializeMonsterPool();
		for (Monster &monster : MonsterPoolAdapter::AllMonsters())
			monster = {};
		for (unsigned slot = 0; slot < MaxMonsters; ++slot)
			ActiveMonsters[slot] = slot;
		InitializeItemPool();
		ItemPoolAdapter::ResetLegacyItemPools();
		for (int slot = 0; slot <= MAXITEMS; ++slot)
			Items[slot] = {};
		CurrentLightManager.Init();
		std::fill(std::begin(CurrentLightManager.lights_), std::end(CurrentLightManager.lights_), Light {});
		std::fill(std::begin(CurrentLightManager.visionList_), std::end(CurrentLightManager.visionList_), Light {});
		for (Tile &tile : tiles())
			tile = {};
		for (Quest &quest : Quests)
			quest._qactive = QUEST_NOTAVAIL;
		RestoreThemeRoomGlobalsForTesting({});
		for (int index = 0; index < MAXTHEMES; ++index)
			themes[index] = {};
		numThemes() = 0;
		RestoreAutomapDefinitionsForTesting({});
		CurrentAutomapManager.InitAutomap();
		CurrentAutomapManager.SetAutoMapShowItems(true);
		Missiles.clear();
		MissilePreFlag = true;
		std::fill(std::begin(Corpses), std::end(Corpses), Corpse {});
		stonendx = 0;
		std::fill(std::begin(Portals), std::end(Portals), Portal {});
		MyPlayerId = 0;
		Players.resize(1);
		MyPlayer = &Players[0];
		Player canonicalHero;
		canonicalHero.create(spec.heroClass);
		const uint32_t beforePreparedInventorySeedPolicy = GetLCGEngineState();
		ApplyNativeCatacombsPreparedInventorySeedPolicy(canonicalHero);
		ASSERT_EQ(GetLCGEngineState(), beforePreparedInventorySeedPolicy);
		*MyPlayer = std::move(canonicalHero);
		MyPlayer->pOriginalCathedral = true;
		MyPlayer->plractive = true;
		MyPlayer->_pLvlChanging = true;
		MyPlayer->plrIsOnSetLevel = false;
		MyPlayer->plrlevel = static_cast<uint8_t>(spec.depth);
		MyPlayer->pLvlLoad = 1;
		MyPlayer->position.future = { 75, 68 };
		MyPlayer->position.last = { 13, 17 };
		MyPlayer->position.temp = { 19, 23 };
		const int maximumLife = MyPlayer->life.maximumBase;
		const int maximumMana = MyPlayer->mana.maximumBase;
		MyPlayer->life = { maximumLife, maximumLife, maximumLife, maximumLife, 100 };
		MyPlayer->mana = { maximumMana, maximumMana, maximumMana, maximumMana, 100 };
		currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(spec.depth), .type = DTYPE_CATACOMBS });
		currentLevelNumber() = spec.depth;
		levelType() = DTYPE_CATACOMBS;
		LevelSeeds[spec.depth] = std::nullopt;
		DungeonSeeds[spec.depth] = spec.seed;
		DungeonSeeds[16] = UINT32_C(0x13579BDF);
		gbIsMultiplayer = false;
		gbIsSpawn = false;
		gbIsHellfire = false;
		gbIsHellfireSaveGame = false;
		gbSndInited = false;
		LoadingMapObjects = false;
		HeadlessMode = true;
		sgGameInitInfo.fullQuests = 1;
		sgGameInitInfo.nDifficulty = DIFF_NORMAL;
		armorFlag() = true;
		weaponFlag() = true;
		const std::vector<uint32_t> playerGenerationItemSeeds = CaptureNativeCatacombsPlayerItemSeeds(*MyPlayer);
		std::ostringstream playerGenerationInput;
		WriteCatacombsPlayer(playerGenerationInput, *MyPlayer);
		ASSERT_TRUE(playerGenerationInput.good());
		SetRndSeed(spec.seed);
		const uint32_t beforePopulationReset = GetLCGEngineState();
		InitLevelMonsters();
		const uint32_t afterPopulationReset = GetLCGEngineState();
		const uint32_t beforeCreateLevel = GetLCGEngineState();
		CreateLevelForTesting(spec.entry);
		ASSERT_TRUE(LevelSeeds[spec.depth].has_value());
		const uint32_t attemptSeed = *LevelSeeds[spec.depth];
		const uint32_t afterCreateLevel = GetLCGEngineState();
		SetRndSeed(spec.seed);
		ASSERT_TRUE(GetLevelMTypes().has_value());
		const uint32_t afterGetLevelMTypes = GetLCGEngineState();
		InitThemes();
		const uint32_t afterInitThemes = GetLCGEngineState();
		const uint32_t beforeInitPlayer = GetLCGEngineState();
		const Point preEntryTile = MyPlayer->position.tile;
		const Point preEntryFuture = MyPlayer->position.future;
		const Point preEntryOld = MyPlayer->position.old;
		const Point preEntryLast = MyPlayer->position.last;
		const Point preEntryTemp = MyPlayer->position.temp;
		InitPlayer(*MyPlayer, false);
		ASSERT_EQ(MyPlayer->lightId, 0);
		ASSERT_EQ(CaptureNativeCatacombsPlayerItemSeeds(*MyPlayer), playerGenerationItemSeeds)
		    << "Native InitPlayer should receive and preserve the prepared starter-item identities.";
		const uint32_t afterInitPlayer = GetLCGEngineState();
		const std::array<bool, 128> inputUniqueFlags = CaptureFlags();
		ASSERT_EQ(inputUniqueFlags, historyAtCaseStart) << "Level setup unexpectedly changed retained unique-item history.";
		if (spec.retainedUniqueHistory)
			ASSERT_TRUE(inputUniqueFlags[PreparedCatacombsUniqueHistoryId]);
		SetRndSeed(spec.seed);
		const uint32_t afterPopulationSeedReset = GetLCGEngineState();
		const std::string file = std::string(CatacombsClassSymbol(spec.heroClass))
		    + (spec.aiEligibilityCoverage ? "-ai-eligibility-coverage" : spec.retainedUniqueHistory ? "-retained-history"
		            : spec.entry == ENTRY_TWARPDN                                                   ? "-town-warp-down"
		                                                                                            : "-cold")
		    + "-l" + std::to_string(spec.depth) + "-" + CatacombsEntrySymbol(spec.entry) + "-" + std::to_string(spec.seed) + ".json";
		const std::filesystem::path casePath = outputGuard.root / "cases" / file;
		std::ofstream caseOutput(casePath, std::ios::binary | std::ios::trunc);
		ASSERT_TRUE(caseOutput.good());
		uint32_t afterHoldThemeRooms = 0;
		uint32_t afterInitGolems = 0;
		uint32_t afterInitObjects = 0;
		uint32_t afterInitMonsters = 0;
		uint32_t afterInitItems = 0;
		uint32_t afterCreateThemeRooms = 0;
		uint32_t beforeReady = 0;
		uint32_t afterReady = 0;
		std::array<bool, 128> outputUniqueFlags {};
		{
			JsonObject document(caseOutput);
			document.String("format", "godevilution-catacombs-population-case");
			document.Number("version", captureStages ? 2 : 1);
			document.String("policy", "quest-disabled-original-diablo-single-player-normal");
			document.String("characterClass", CatacombsClassSymbol(spec.heroClass));
			document.Number("characterClassId", spec.heroClass);
			document.Number("characterLevel", MyPlayer->getCharacterLevel());
			document.Number("levelId", spec.depth);
			document.String("entry", CatacombsEntrySymbol(spec.entry));
			document.Number("inputSeed", spec.seed);
			if (captureStages) document.Number("storySeed", DungeonSeeds[16]);
			if (spec.aiEligibilityCoverage)
				document.String("casePurpose", "native-ai-eligibility-coverage");
			document.Number("attemptSeed", attemptSeed);
			document.Number("solSourceByteCount", sourceSolByteCount);
			document.Number("solEffectiveSlotCount", MAXTILES);
			document.Boolean("retainedUniqueHistory", spec.retainedUniqueHistory);
			document.Key("rngBoundaries");
			caseOutput << '{';
			bool firstBoundary = true;
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "beforePopulationReset", beforePopulationReset);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterPopulationReset", afterPopulationReset);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "beforeCreateLevel", beforeCreateLevel);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterCreateLevel", afterCreateLevel);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterGetLevelMTypes", afterGetLevelMTypes);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterInitThemes", afterInitThemes);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "beforeInitPlayer", beforeInitPlayer);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterInitPlayer", afterInitPlayer);
			WriteCatacombsRngBoundary(caseOutput, firstBoundary, "afterPopulationSeedReset", afterPopulationSeedReset);
			caseOutput << '}';
			document.Key("uniqueItemHistory");
			{
				JsonObject history(caseOutput);
				history.String("kind", spec.retainedUniqueHistory ? "retained-chain" : "cold");
				if (spec.retainedUniqueHistory && spec.depth > 5)
					history.Number("previousDepth", spec.depth - 1);
				if (spec.retainedUniqueHistory) {
					history.Number("preparedUniqueItemFlagId", PreparedCatacombsUniqueHistoryId);
					history.Boolean("preparedFlagSeededThisCase", spec.depth == 5);
					history.Boolean("preparedFlagPresentAtInput", historyAtCaseStart[PreparedCatacombsUniqueHistoryId]);
				}
				history.Key("inputFlags");
				WriteFlags(caseOutput, inputUniqueFlags);
			}
			document.Key("playerPreEntryContext");
			{
				JsonObject context(caseOutput);
				context.String("kind", "prepared-catacombs-entry-context");
				context.Boolean("precedingFloorGameplayInput", false);
				context.String("preparedInventorySeedPolicy", NativeCatacombsPreparedInventorySeedPolicy);
				context.Number("level", MyPlayer->plrlevel);
				context.Key("tile");
				WriteRoomPoint(caseOutput, preEntryTile);
				context.Key("future");
				WriteRoomPoint(caseOutput, preEntryFuture);
				context.Key("old");
				WriteRoomPoint(caseOutput, preEntryOld);
				context.Key("last");
				WriteRoomPoint(caseOutput, preEntryLast);
				context.Key("temp");
				WriteRoomPoint(caseOutput, preEntryTemp);
			}
			document.Key("playerGenerationInput");
			caseOutput << playerGenerationInput.str();
			document.Key("playerBeforePopulation");
			WriteCatacombsPlayer(caseOutput, *MyPlayer);
			document.Key("bestiary");
			WriteCatacombsBestiary(caseOutput);
			document.Key("worldBeforePopulation");
			WriteRoomWorld(caseOutput);
			document.Key("themesBeforePopulation");
			WriteCatacombsThemeState(caseOutput);
			SetRndSeed(spec.seed);
			HoldThemeRooms();
			afterHoldThemeRooms = GetLCGEngineState();
			InitGolems();
			afterInitGolems = GetLCGEngineState();
			InitObjects();
			afterInitObjects = GetLCGEngineState();
			if (captureStages) {
				document.Key("worldAfterInitialObjects");
				WriteRoomWorld(caseOutput);
			}
			ASSERT_TRUE(InitMonsters().has_value());
			afterInitMonsters = GetLCGEngineState();
			if (captureStages) {
				document.Key("worldAfterInitMonsters");
				WriteRoomWorld(caseOutput);
			}
			InitItems();
			afterInitItems = GetLCGEngineState();
			if (captureStages) {
				document.Key("worldAfterInitItems");
				WriteRoomWorld(caseOutput);
			}
			const std::array<bool, 128> outputUniqueFlagsAfterItems = CaptureFlags();
			if (spec.retainedUniqueHistory)
				ASSERT_TRUE(outputUniqueFlagsAfterItems[PreparedCatacombsUniqueHistoryId]);
			CreateThemeRooms();
			afterCreateThemeRooms = GetLCGEngineState();
			outputUniqueFlags = CaptureFlags();
			if (spec.retainedUniqueHistory)
				ASSERT_TRUE(outputUniqueFlags[PreparedCatacombsUniqueHistoryId]);
			document.Key("rngAfterPopulationStages");
			{
				JsonObject boundaries(caseOutput);
				boundaries.Number("afterHoldThemeRooms", afterHoldThemeRooms);
				boundaries.Number("afterInitGolems", afterInitGolems);
				boundaries.Number("afterInitObjects", afterInitObjects);
				boundaries.Number("afterInitMonsters", afterInitMonsters);
				boundaries.Number("afterInitItems", afterInitItems);
				boundaries.Number("afterCreateThemeRooms", afterCreateThemeRooms);
			}
			document.Key("uniqueItemHistoryAfterItems");
			WriteFlags(caseOutput, outputUniqueFlagsAfterItems);
			document.Key("uniqueItemHistoryOutput");
			WriteFlags(caseOutput, outputUniqueFlags);
			document.Key("populationResetRecords");
			WriteCatacombsResetRecords(caseOutput);
			document.Key("worldAfterThemeCreation");
			WriteRoomWorld(caseOutput);
			document.Key("themesAfterCreation");
			WriteCatacombsThemeState(caseOutput);
			beforeReady = GetLCGEngineState();
			CompleteFreshLevelForTesting(spec.entry);
			afterReady = GetLCGEngineState();
			document.Key("rngAfterLevelReady");
			{
				JsonObject boundaries(caseOutput);
				boundaries.Number("beforeReadySequence", beforeReady);
				boundaries.Number("afterReadySequence", afterReady);
				boundaries.String("sequence", "InitMissiles,InitCorpses,SavePreLighting,SyncPortals,SyncPlayerEntry,UpdateMonsterLights,UnstuckChargers,LoadGameLevelLightVision");
			}
			document.Key("ready");
			WriteLevelReadyState(caseOutput);
			document.Key("playerReady");
			WriteCatacombsPlayer(caseOutput, *MyPlayer);
		}
		caseOutput << "\r\n";
		caseOutput.flush();
		caseOutput.close();
		ASSERT_TRUE(caseOutput.good());
		ASSERT_LE(std::filesystem::file_size(casePath), 128U * 1024U * 1024U) << casePath;
		EXPECT_EQ(beforeReady, afterReady) << "The native fresh level-ready tail should not consume gameplay RNG.";
		if (spec.retainedUniqueHistory)
			carriedHistory = outputUniqueFlags;
		indexRows.push_back(spec);
	}
	{
		std::ofstream manifest(outputGuard.root / "manifest.json", std::ios::binary | std::ios::trunc);
		ASSERT_TRUE(manifest.good());
		{
			JsonObject document(manifest);
			document.String("format", "godevilution-native-catacombs-population");
			document.Number("version", captureStages ? 2 : 1);
			document.String("policy", "quest-disabled-original-diablo-single-player-normal");
			document.Number("caseCount", indexRows.size());
			if (captureStages)
				document.String("capturedStages", "post-InitObjects,post-InitMonsters,post-InitItems");
			document.Number("catacombsFirstLevel", 5);
			document.Number("catacombsLastLevel", 8);
			document.Number("nativeSolSourceBytes", sourceSolByteCount);
			document.Number("nativeSolEffectiveSlots", MAXTILES);
			document.Number("nativeSolReserved0x40Bytes", reservedSolFlagCount);
			document.Number("nativeMegaTileCount", megaTileCount);
			document.Number("populationMatrixCaseCount", 30);
			document.Number("aiEligibilityCoverageCaseCount", 2);
			document.String("aiEligibilityCoverageCases", "appended after the 30-case matrix: Warrior L8 Main seed 0 for Rhino eligibility and seed 1 for Magma/Fat eligibility");
			document.String("preparedInventorySeedPolicy", NativeCatacombsPreparedInventorySeedPolicy);
			document.String("coldWarriorCases", "seeds 2588 and 743271966; levels 5 through 8; Main and Previous");
			document.String("sorcererCases", "seed 2588; levels 5 through 8; Main");
			document.String("townWarpDownCases", "seed 2588; Warrior and Sorcerer; level 5");
			document.String("retainedUniqueHistoryCases", "seed 2588; Warrior and Sorcerer; level 5 seeds unique-item flag 7, then final flags carry through levels 6 to 8");
			document.Number("preparedUniqueItemFlagId", PreparedCatacombsUniqueHistoryId);
			document.String("playerPreEntryContext", "prepared context without preceding-floor gameplay; future 75,68; last 13,17; temp 19,23; InitPlayer supplies tile/old and ready sync supplies future");
			document.Key("readyTail");
			JsonString(manifest, "InitMissiles,InitCorpses,SavePreLighting,SyncPortals,SyncPlayerEntry,UpdateMonsterLights,UnstuckChargers,LoadGameLevelLightVision");
			document.Key("cases");
			manifest << '[';
			for (size_t index = 0; index < indexRows.size(); ++index) {
				if (index != 0)
					manifest << ',';
				const NativeCatacombsPopulationCase &spec = indexRows[index];
				JsonObject row(manifest);
				row.String("file", "cases/" + std::string(CatacombsClassSymbol(spec.heroClass)) + (spec.aiEligibilityCoverage ? "-ai-eligibility-coverage" : spec.retainedUniqueHistory ? "-retained-history"
				                           : spec.entry == ENTRY_TWARPDN                                                                                                                ? "-town-warp-down"
				                                                                                                                                                                        : "-cold")
				        + "-l" + std::to_string(spec.depth) + "-" + CatacombsEntrySymbol(spec.entry) + "-" + std::to_string(spec.seed) + ".json");
				row.String("characterClass", CatacombsClassSymbol(spec.heroClass));
				row.Number("characterClassId", spec.heroClass);
				row.Number("levelId", spec.depth);
				row.String("entry", CatacombsEntrySymbol(spec.entry));
				row.Number("inputSeed", spec.seed);
				if (spec.aiEligibilityCoverage)
					row.String("casePurpose", "native-ai-eligibility-coverage");
				row.Boolean("retainedUniqueHistory", spec.retainedUniqueHistory);
				if (spec.retainedUniqueHistory) {
					row.Number("preparedUniqueItemFlagId", PreparedCatacombsUniqueHistoryId);
					row.Boolean("preparedUniqueItemFlagSeeded", spec.depth == 5);
				}
			}
			manifest << ']';
		}
		manifest << "\r\n";
		manifest.flush();
		ASSERT_TRUE(manifest.good());
		manifest.close();
		ASSERT_TRUE(manifest.good());
	}
	ASSERT_LE(std::filesystem::file_size(outputGuard.root / "manifest.json"), 4U * 1024U * 1024U);
	ASSERT_LE(std::filesystem::file_size(outputGuard.root / "native-catacombs-sources.json"), 4U * 1024U * 1024U);
	ASSERT_TRUE(outputGuard.WriteStatus("complete")) << "Could not finalize the capture status marker.";
	outputGuard.complete = true;
#endif
}

// Additive staged reference; the earlier fixture retains its exact version-1 bytes.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsFreshPopulationStages)
{
	CaptureNativeCatacombsPopulationOracle(
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_ARCHIVE"),
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_REFERENCE_DIRECTORY"), true);
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsFreshPopulation)
{
	CaptureNativeCatacombsPopulationOracle(
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_ARCHIVE"),
	    std::getenv("GODEVILUTION_NATIVE_CATACOMBS_POPULATION_REFERENCE_DIRECTORY"));
}

TEST_F(ThemeRoomGenerationTest, CaptureComposedOrdinaryPopulation)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_FULL_POPULATION_ORACLE_DIRECTORY"), false);
}

TEST_F(ThemeRoomGenerationTest, CaptureFreshOrdinaryPopulation)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_FRESH_POPULATION_ORACLE_DIRECTORY"), true);
}

// Separately selected exact playable-profile tuple; established corpus bytes stay frozen.
TEST_F(ThemeRoomGenerationTest, CaptureNativeRogueL1Population)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_NATIVE_ROGUE_L1_ORACLE_DIRECTORY"), true, false, true);
}

// Separate opt-in birth tuples; no widening or recapture of earlier corpora.
TEST_F(ThemeRoomGenerationTest, CaptureNativeWarriorL1Population)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_NATIVE_WARRIOR_L1_ORACLE_DIRECTORY"), true, false, false, HeroClass::Warrior);
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeSorcererL1Population)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_NATIVE_SORCERER_L1_ORACLE_DIRECTORY"), true, false, false, HeroClass::Sorcerer);
}

TEST_F(ThemeRoomGenerationTest, CaptureFreshLevelReady)
{
	CapturePopulationOracle(std::getenv("GODEVILUTION_CATHEDRAL_LEVEL_READY_ORACLE_DIRECTORY"), true, true);
}

// Actual L2 object operations on controlled SOL/occupancy inputs. No MPQ is required.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsDoorOperations)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_DOOR_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select a private Catacombs door corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	std::ofstream stream(root / "native-catacombs-door-operations.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int count = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-door-operations");
		document.Number("version", 1);
		document.String("policy", "controlled-original-diablo-catacombs-l2-door-sol-and-occupancy");
		document.Key("cases");
		stream << '[';
		for (int depth = 5; depth <= 8; ++depth) {
			for (const auto type : { OBJ_L2LDOOR, OBJ_L2RDOOR }) {
				for (int occupancy = 0; occupancy < 4; ++occupancy) {
					PrepareRoom(depth, 0, 0, 0, false);
					currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATACOMBS });
					for (Tile &tile : tiles()) {
						tile.setPlayer(0);
						tile.setMonster(0);
						tile.setItem(0);
						tile.setCorpse(0);
					}
					for (int y = 48; y <= 52; ++y) {
						for (int x = 48; x <= 52; ++x) {
							tileAt(x, y).setPiece(100 + (y - 48) * 5 + x - 48);
							tileAt(x, y).setSpecial(11);
							tileAt(x, y).setTransVal(23);
						}
					}
					tileProperties()[537] = tileProperties()[539] = TileProperties::Solid | TileProperties::BlockLight | TileProperties::BlockMissile;
					tileProperties()[12] = tileProperties()[16] = TileProperties::None;
					SetRndSeed(2588);
					Object *door = AddObject(type, { 50, 50 });
					ASSERT_NE(door, nullptr);
					Player remotePlayer;
					if (count++ != 0) stream << ',';
					JsonObject row(stream);
					row.Number("depth", depth);
					row.Number("nativeType", type);
					row.Number("occupancy", occupancy);
					row.Key("initialObject");
					WriteRoomObject(stream, door->GetId(), *door);
					row.Key("states");
					stream << '[';
					bool first = true;
					auto capture = [&](std::string_view operation) {
						if (!first) stream << ',';
						first = false;
						JsonObject state(stream);
						state.String("operation", operation);
						state.Number("random", GetLCGEngineState());
						state.Key("object");
						WriteRoomObject(stream, door->GetId(), *door);
						state.Key("cells");
						stream << '[';
						bool firstCell = true;
						for (int y = 48; y <= 52; ++y) {
							for (int x = 48; x <= 52; ++x) {
								if (!firstCell) stream << ',';
								firstCell = false;
								const Tile &tile = tileAt(x, y);
								stream << '[' << tile.piece() << ',' << static_cast<int>(tile.special()) << ',' << static_cast<int>(tile.transVal())
								       << ',' << static_cast<int>(tileProperties()[tile.piece()]) << ']';
							}
						}
						stream << ']';
					};
					capture("initial");
					SyncOpObject(remotePlayer, 0, *door);
					capture("open");
					auto &center = tileAt(50, 50);
					if (occupancy == 0) center.setCorpse(1);
					if (occupancy == 1) center.setPlayer(-1);
					if (occupancy == 2) center.setMonster(-1);
					if (occupancy == 3) center.setItem(1);
					SyncOpObject(remotePlayer, 0, *door);
					capture("blocked-close");
					ProcessObjects();
					capture("occupied-update");
					SyncObjectAnim(*door);
					capture("occupied-sync");
					ProcessObjects();
					capture("occupied-update-after-sync");
					center.setCorpse(0);
					center.setPlayer(0);
					center.setMonster(0);
					center.setItem(0);
					ProcessObjects();
					capture("clear-update");
					SyncOpObject(remotePlayer, 0, *door);
					capture("close");
					DeltaSyncOpObject(*door);
					capture("delta-open");
					DeltaSyncOpObject(*door);
					capture("repeat-delta-open");
					SyncObjectAnim(*door);
					capture("open-sync");
					SyncOpObject(remotePlayer, 0, *door);
					capture("close-after-delta");
					SyncObjectAnim(*door);
					capture("closed-sync");
					stream << ']';
				}
			}
		}
		stream << ']';
		document.Number("caseCount", count);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(count, 32);
}

// The follower observes its leader at its own active-list turn, before AI.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsGargoylePackOrder)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_PACK_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select a private Gargoyle pack reference directory.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	std::ofstream stream(root / "native-catacombs-gargoyle-pack-order.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-gargoyle-pack-order");
		document.Number("version", 1);
		document.String("policy", "prepared-inactive-gargoyle-pack-special-completion-order");
		document.Key("cases");
		stream << '[';
		for (int followerFirst = 0; followerFirst < 2; ++followerFirst) {
			PrepareRoom(5, 0, 0, 0, false);
			currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
			for (Tile &tile : tiles()) {
				tile.setPiece(0);
				tile.setPlayer(0);
				tile.setMonster(0);
				tile.setFlags(DungeonFlag::None);
			}
			MyPlayer->position.tile = { 90, 90 };
			MyPlayer->position.future = { 90, 90 };
			MyPlayer->position.old = { 90, 90 };
			MyPlayer->life.current = 6400;
			tileAt(90, 90).setPlayer(1);
			LevelBestiary.init();
			const auto type = LevelBestiary.addType(MT_GARGOYLE, PLACE_SCATTER);
			ASSERT_TRUE(type.has_value());
			SetRndSeed(2588);
			Monster *leader = AddMonster({ 10, 10 }, Direction::SouthEast, *type, true);
			ASSERT_NE(leader, nullptr);
			Monster *follower = AddMonster({ 11, 10 }, Direction::SouthEast, *type, true);
			ASSERT_NE(follower, nullptr);
			leader->packSize = 1;
			follower->setLeader(leader);
			follower->leaderRelation = LeaderRelation::Leashed;
			for (Monster *actor : { leader, follower }) {
				actor->activeForTicks = 0;
				actor->enemy = 0;
				actor->enemyPosition = { 90, 90 };
				actor->position.last = { 90, 90 };
			}
			leader->flags &= ~MFLAG_ALLOW_SPECIAL;
			leader->animInfo.currentFrame = leader->animInfo.numberOfFrames - 1;
			leader->animInfo.tickCounterOfCurrentFrame = 0;
			if (followerFirst != 0) std::swap(ActiveMonsters[0], ActiveMonsters[1]);
			SetRndSeed(2588);
			if (followerFirst != 0) stream << ',';
			JsonObject row(stream);
			row.Boolean("followerFirst", followerFirst != 0);
			row.Key("initialLeader");
			WriteRoomMonster(stream, leader->getId(), *leader);
			row.Key("initialFollower");
			WriteRoomMonster(stream, follower->getId(), *follower);
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < 3; ++tick) {
				ProcessMonsters();
				if (tick != 0) stream << ',';
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Number("random", GetLCGEngineState());
				state.Key("leader");
				WriteRoomMonster(stream, leader->getId(), *leader);
				state.Key("follower");
				WriteRoomMonster(stream, follower->getId(), *follower);
			}
			stream << ']';
		}
		stream << ']';
		document.Number("caseCount", 2);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
}

// Separate source metadata and actual Monster methods; older corpus bytes stay frozen.
// Actual controller/mode dispatch over a prepared, MPQ-free actor arena.
TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsActorPhases)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_ACTOR_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory)
		GTEST_SKIP() << "Select a private Catacombs actor corpus explicitly.";
	const auto root = std::filesystem::weakly_canonical(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));
	LoadPlayerDataFiles();
	std::ofstream stream(root / "native-catacombs-actor-phases.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-actor-phases");
		document.Number("version", 2);
		document.String("policy", "prepared-original-diablo-single-player-normal-catacombs-actor-arena");
		document.Key("cases");
		stream << '[';
		for (const HeroClass heroClass : { HeroClass::Warrior, HeroClass::Sorcerer }) {
			for (const int lane : { 0, 5, 6, 7, 2, 3, 4 }) {
				PrepareRoom(5, 0, 0, 0, false);
				currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
				for (Tile &tile : tiles()) {
					tile.setPlayer(0);
					tile.setMonster(0);
					tile.setFlags(DungeonFlag::None);
				}
				LevelBestiary.init();
				const bool fat = lane == 0 || lane >= 5;
				const int nativeType = fat ? (lane == 0 ? 52 : lane + 48) : 81;
				const auto typeIndex = LevelBestiary.addType(static_cast<_monster_id>(nativeType), PLACE_SCATTER);
				ASSERT_TRUE(typeIndex.has_value());
				// Character creation requires inventory cursor art. This controller seam
				// prepares only class combat scalars from the canonical native tables.
				Player hero;
				hero._pClass = heroClass;
				hero.setCharacterLevel(1);
				const ClassAttributes &attributes = hero.getClassAttributes();
				hero.attributes.strength.base = hero.attributes.strength.current = attributes.baseStr;
				hero.attributes.magic.base = hero.attributes.magic.current = attributes.baseMag;
				hero.attributes.dexterity.base = hero.attributes.dexterity.current = attributes.baseDex;
				hero.attributes.vitality.base = hero.attributes.vitality.current = attributes.baseVit;
				const int life = hero.calculateBaseLife();
				const int mana = hero.calculateBaseMana();
				hero.life = { life, life, life, life, 100 };
				hero.mana = { mana, mana, mana, mana, 100 };
				hero.setAnimations();
				*MyPlayer = std::move(hero);
				MyPlayer->plractive = true;
				MyPlayer->plrlevel = 5;
				MyPlayer->_pLvlChanging = false;
				MyPlayer->plrIsOnSetLevel = false;
				MyPlayer->_pInvincible = false;
				MyPlayer->_pBlockFlag = false;
				MyPlayer->_pmode = PM_STAND;
				const Point playerPosition { static_cast<WorldTileCoord>(fat ? 51 : 60), 50 };
				MyPlayer->position.tile = playerPosition;
				MyPlayer->position.future = playerPosition;
				MyPlayer->position.old = playerPosition;
				tileAt(playerPosition).setPlayer(1);
				SetRndSeed(2588);
				Monster *actor = AddMonster({ 50, 50 }, Direction::SouthEast, *typeIndex, true);
				ASSERT_NE(actor, nullptr);
				actor->activeForTicks = 255;
				actor->enemy = 0;
				actor->enemyPosition = playerPosition;
				actor->position.last = playerPosition;
				uint32_t inputSeed = 2588;
				if (fat) {
					for (inputSeed = 0; inputSeed < 10000; ++inputSeed) {
						SetRndSeed(inputSeed);
						const int roll = GenerateRnd(100);
						if (roll >= 4 * actor->intelligence + 15 && roll < 4 * actor->intelligence + 20) break;
					}
					ASSERT_LT(inputSeed, 10000U);
				}
				SetRndSeed(inputSeed);
				if (caseCount++ != 0) stream << ',';
				JsonObject row(stream);
				row.Number("classId", heroClass);
				row.Number("nativeType", nativeType);
				row.Number("lane", lane);
				row.Number("inputSeed", inputSeed);
				row.Number("characterLevel", MyPlayer->getCharacterLevel());
				row.Number("initialPlayerLifeQ6", MyPlayer->life.current);
				row.Boolean("canBlock", false);
				row.Number("normalToHit", actor->toHit(DIFF_NORMAL));
				row.Number("specialToHit", actor->toHitSpecial(DIFF_NORMAL));
				row.Number("minimumDamage", actor->minDamage);
				row.Number("maximumDamage", actor->maxDamage);
				row.Number("minimumSpecialDamage", actor->minDamageSpecial);
				row.Number("maximumSpecialDamage", actor->maxDamageSpecial);
				row.Number("normalEffectFrame", actor->data().animFrameNum);
				row.Number("specialEffectFrame", actor->data().animFrameNumSpecial);
				row.Number("intelligence", actor->intelligence);
				row.Number("monsterLevel", actor->level(DIFF_NORMAL));
				row.Key("animations");
				WriteArray(stream, actor->type().anims, [&](const AnimStruct &anim) {
					stream << '[' << static_cast<int>(anim.frames) << ',' << static_cast<int>(anim.rate) << ']';
				});
				row.Key("ticks");
				stream << '[';
				for (int tick = 0; tick < 45; ++tick) {
					if (tick != 0) stream << ',';
					JsonObject state(stream);
					state.Number("tick", tick);
					state.Number("beforeMode", actor->mode);
					state.Number("beforeFlags", actor->flags);
					state.Number("beforeFrame", actor->animInfo.currentFrame);
					state.Number("beforeCounter", actor->animInfo.tickCounterOfCurrentFrame);
					state.Number("beforeRandom", GetLCGEngineState());
					state.Number("beforePlayerLifeQ6", MyPlayer->life.current);
					bool consumedWake = false;
					if (tick == 3 && lane == 3) {
						MyPlayer->position.future = { 51, 50 };
					} else if (tick == 3 && lane == 4) {
						consumedWake = actor->tryLiftGargoyle();
					}
					ProcessMonsters();
					// Freeze further AI after the admitted sequence; keep native mode/animation dispatch.
					if (fat || (actor->flags & MFLAG_ALLOW_SPECIAL) == 0)
						actor->activeForTicks = 0;
					state.Boolean("consumedWake", consumedWake);
					state.Number("mode", actor->mode);
					state.Number("flags", actor->flags);
					state.Number("frame", actor->animInfo.currentFrame);
					state.Number("counter", actor->animInfo.tickCounterOfCurrentFrame);
					state.Number("frameCount", actor->animInfo.numberOfFrames);
					state.Number("ticksPerFrame", actor->animInfo.ticksPerFrame);
					state.Number("random", GetLCGEngineState());
					state.Number("playerLifeQ6", MyPlayer->life.current);
				}
				stream << ']';
			}
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 14);
}

void WriteRangedPlayer(std::ostream &out, const Player &player)
{
	JsonObject json(out);
	json.Number("level", player.plrlevel);
	json.Number("characterLevel", player.getCharacterLevel());
	json.Number("mode", player._pmode);
	json.Boolean("active", player.plractive);
	json.Boolean("levelLoading", player.pLvlLoad != 0);
	json.Key("tile");
	WriteRoomPoint(out, { player.position.tile.x, player.position.tile.y });
	json.Key("future");
	WriteRoomPoint(out, { player.position.future.x, player.position.future.y });
	json.Key("old");
	WriteRoomPoint(out, { player.position.old.x, player.position.old.y });
}

void WriteRangedMissile(std::ostream &out, const Missile &missile)
{
	JsonObject json(out);
	json.Number("type", missile._mitype);
	json.Number("source", missile._misource);
	json.Number("caster", missile._micaster);
	json.Number("damage", missile._midam);
	json.Number("spellLevel", missile._mispllvl);
	json.Number("graphic", missile._miAnimType);
	json.Number("graphicDirection", missile.getFrameGroupRaw());
	json.Number("animationFrame", missile._miAnimFrame);
	json.Number("animationLength", missile._miAnimLen);
	json.Number("animationDelay", missile._miAnimDelay);
	json.Number("duration", missile.duration);
	json.Boolean("delete", missile._miDelFlag);
	json.Key("start");
	WriteRoomPoint(out, { missile.position.start.x, missile.position.start.y });
	json.Key("tile");
	WriteRoomPoint(out, { missile.position.tile.x, missile.position.tile.y });
	json.Key("velocity");
	WriteRoomPoint(out, { missile.position.velocity.deltaX, missile.position.velocity.deltaY });
	json.Key("traveled");
	WriteRoomPoint(out, { missile.position.traveled.deltaX, missile.position.traveled.deltaY });
	json.Key("rawProjectileUnionDestination");
	WriteRoomPoint(out, { missile.projectile.destX, missile.projectile.destY });
	json.Key("raw");
	WriteArray(out, missile.raw, [&](int value) { out << value; });
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsRangedReleases)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_RANGED_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the explicit Catacombs ranged oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native ranged evidence.";
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode, missilePreFlag = MissilePreFlag;
		uint32_t random = GetLCGEngineState();
		std::list<Missile> savedMissiles = std::move(Missiles);
		ScopedMissileDataForTest missileData;
		~ExecutionGuard()
		{
			Missiles = std::move(savedMissiles);
			MissilePreFlag = missilePreFlag;
			gbSndInited = sound;
			HeadlessMode = headless;
			SetRndSeed(random);
		}
	} guard;
	HeadlessMode = true;
	gbSndInited = false;
	LoadPlayerDataFiles();
	LoadMissileData();
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Arrow));
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::MagmaBall));
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Acid));

	std::ofstream stream(root / "native-catacombs-ranged-release.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-ranged-release");
		document.Number("version", 1);
		document.String("policy", "prepared-original-diablo-catacombs-ranged-controller-release-clock");
		document.Key("cases");
		stream << '[';
		auto nextCase = [&]() { if (caseCount++ != 0) stream << ','; };
		auto writeIdentity = [&](JsonObject &row, std::string_view name, int depth, _monster_id nativeType, MonsterAIID ai, MissileID missileId, bool special, std::string_view uniqueName, Monster &actor) {
			row.String("name", name);
			row.Number("depth", depth);
			row.Number("nativeType", nativeType);
			row.Number("ai", ai);
			row.Number("missileId", missileId);
			row.Boolean("special", special);
			row.String("uniqueName", uniqueName);
			row.Number("normalEffectFrame", actor.data().animFrameNum);
			row.Number("specialEffectFrame", actor.data().animFrameNumSpecial);
			row.Number("sourceAnimationFrames", actor.animInfo.numberOfFrames);
			row.Key("initialActor");
			WriteRoomMonster(stream, static_cast<int>(actor.getId()), actor);
			row.Key("initialPlayer");
			WriteRangedPlayer(stream, *MyPlayer);
			row.Number("initialRandom", GetLCGEngineState());
		};
		auto writeMissiles = [&]() {
			stream << '[';
			bool first = true;
			for (const Missile &missile : Missiles) {
				if (!first) stream << ',';
				first = false;
				WriteRangedMissile(stream, missile);
			}
			stream << ']';
		};
		auto prepareActor = [&](int depth, _monster_id nativeType, const UniqueMonsterData *unique) -> Monster * {
			PrepareRoom(depth, 0, 0, 0, false);
			currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATACOMBS });
			std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
			for (Tile &tile : tiles()) {
				tile.setPiece(0);
				tile.setTransVal(0);
				tile.setFlags(DungeonFlag::None);
				tile.setPlayer(0);
				tile.setMonster(0);
				tile.setObject(0);
				tile.setItem(0);
				tile.setCorpse(0);
				tile.setLight(0);
				tile.setPreLight(0);
			}
			LevelBestiary.init();
			const auto typeIndex = LevelBestiary.addType(nativeType, PLACE_SCATTER);
			if (!typeIndex) return nullptr;
			Player hero;
			hero._pClass = HeroClass::Warrior;
			hero.setCharacterLevel(1);
			const ClassAttributes &attributes = hero.getClassAttributes();
			hero.attributes.strength.base = hero.attributes.strength.current = attributes.baseStr;
			hero.attributes.magic.base = hero.attributes.magic.current = attributes.baseMag;
			hero.attributes.dexterity.base = hero.attributes.dexterity.current = attributes.baseDex;
			hero.attributes.vitality.base = hero.attributes.vitality.current = attributes.baseVit;
			const int life = hero.calculateBaseLife();
			const int mana = hero.calculateBaseMana();
			hero.life = { life, life, life, life, 100 };
			hero.mana = { mana, mana, mana, mana, 100 };
			hero.setAnimations();
			*MyPlayer = std::move(hero);
			MyPlayer->plractive = true;
			MyPlayer->plrlevel = static_cast<uint8_t>(depth);
			MyPlayer->plrIsOnSetLevel = false;
			MyPlayer->_pLvlChanging = false;
			MyPlayer->pLvlLoad = 1;
			MyPlayer->_pInvincible = false;
			MyPlayer->_pBlockFlag = false;
			MyPlayer->_pmode = PM_STAND;
			const Point playerPosition { 60, 50 };
			MyPlayer->position.tile = playerPosition;
			MyPlayer->position.future = playerPosition;
			MyPlayer->position.old = playerPosition;
			tileAt(playerPosition).setPlayer(1);
			CurrentLightManager.Init();
			SetRndSeed(2588);
			Monster *actor = AddMonster({ 50, 50 }, Direction::SouthEast, *typeIndex, true);
			if (actor == nullptr) return nullptr;
			actor->activeForTicks = UINT8_MAX;
			actor->enemy = 0;
			actor->enemyPosition = playerPosition;
			actor->position.last = playerPosition;
			actor->position.future = actor->position.tile;
			actor->goal = MonsterGoal::Normal;
			actor->goalVar1 = 0;
			actor->goalVar2 = 0;
			actor->goalVar3 = 0;
			actor->flags &= ~(MFLAG_NO_ENEMY | MFLAG_TARGETS_MONSTER | MFLAG_SEARCH | MFLAG_HIDDEN);
			tileAt(actor->position.tile).addFlags(DungeonFlag::Visible);
			if (unique != nullptr) {
				const size_t uniqueIndex = static_cast<size_t>(unique - UniqueMonstersData.data());
				actor->uniqueType = static_cast<UniqueMonsterType>(uniqueIndex);
				actor->ai = unique->mAi;
				actor->intelligence = unique->mint;
				actor->minDamage = actor->minDamageSpecial = unique->mMinDamage;
				actor->maxDamage = actor->maxDamageSpecial = unique->mMaxDamage;
				actor->maxHitPoints = std::max<int>(unique->mmaxhp / 2, 1) << 6;
				actor->hitPoints = actor->maxHitPoints;
				actor->uniqTrans = 0;
			}
			return actor;
		};
		auto specialSeed = [&](const Monster &actor) {
			const int shift = actor.ai == MonsterAIID::Acid ? 1 : 0;
			const int threshold = (500 * (actor.intelligence + 2)) >> shift;
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				if (candidate.generateRnd(10000) < threshold) return seed;
			}
			return UINT32_MAX;
		};

		struct Lane {
			const char *name;
			int depth;
			_monster_id type;
			MissileID missile;
			bool special;
			bool unique;
		};
		const std::array<Lane, 5> lanes { {
			{ "skeleton-bow", 5, MT_WSKELBW, MissileID::Arrow, false, false },
			{ "goat-ranged", 5, MT_NGOATBW, MissileID::Arrow, false, false },
			{ "magma", 8, MT_NMAGMA, MissileID::MagmaBall, true, false },
			{ "acid", 6, MT_NACID, MissileID::Acid, true, false },
			{ "deathspit", 6, MT_NACID, MissileID::Acid, true, true },
		} };
		for (const Lane &lane : lanes) {
			const UniqueMonsterData *unique = nullptr;
			if (lane.unique) {
				const auto found = std::find_if(UniqueMonstersData.begin(), UniqueMonstersData.end(), [](const UniqueMonsterData &entry) { return entry.mName == "Deathspit"; });
				ASSERT_NE(found, UniqueMonstersData.end());
				unique = &*found;
			}
			Monster *actor = prepareActor(lane.depth, lane.type, unique);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			uint32_t seed = 2588;
			if (actor->ai == MonsterAIID::Magma || actor->ai == MonsterAIID::Acid) seed = specialSeed(*actor);
			ASSERT_NE(seed, UINT32_MAX);
			SetRndSeed(seed);
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, lane.name, lane.depth, lane.type, actor->ai, lane.missile, lane.special, lane.unique ? "Deathspit" : "", *actor);
			row.Key("ticks");
			stream << '[';
			bool firstTick = true, sawRelease = false, completed = false;
			for (int tick = 0; tick < 128; ++tick) {
				const WorldTilePosition future { 60, static_cast<WorldTileCoord>(50 + ((tick + 1) % 3)) };
				MyPlayer->position.future = future;
				const MonsterMode expectedMode = lane.special ? MonsterMode::SpecialRangedAttack : MonsterMode::RangedAttack;
				const int effectFrame = lane.special ? actor->data().animFrameNumSpecial - 1 : actor->data().animFrameNum - 1;
				const bool eligible = actor->mode == expectedMode && actor->animInfo.currentFrame == effectFrame
				    && (!lane.special || actor->animInfo.tickCounterOfCurrentFrame == 0);
				const int beforeFrame = actor->animInfo.currentFrame;
				const MonsterMode beforeMode = actor->mode;
				const bool wasLast = actor->animInfo.isLastFrame();
				const size_t missileCount = Missiles.size();
				if (!firstTick) stream << ',';
				firstTick = false;
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Boolean("releaseFrameEligible", eligible);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", missileCount);
				ProcessMonsters();
				const bool created = Missiles.size() > missileCount;
				if (eligible && created) sawRelease = true;
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", created);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { MyPlayer->position.future.x, MyPlayer->position.future.y });
				state.Key("missiles");
				writeMissiles();
				if (sawRelease && beforeMode == expectedMode && wasLast
				    && (actor->mode != expectedMode || actor->animInfo.currentFrame < beforeFrame)) {
					completed = true;
					break;
				}
			}
			stream << ']';
			row.Boolean("naturalReleaseObserved", sawRelease);
			row.Boolean("completedAndRedispatched", completed);
			EXPECT_TRUE(sawRelease) << lane.name;
			EXPECT_TRUE(completed) << lane.name;
		}

		// Ordinary attack checks the effect frame every tick, so a deliberately
		// held frame emits repeatedly. This is a prepared controller boundary.
		{
			Monster *actor = prepareActor(5, MT_NGOATBW, nullptr);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(2588);
			ProcessMonsters();
			ASSERT_EQ(actor->mode, MonsterMode::RangedAttack);
			actor->animInfo.currentFrame = actor->data().animFrameNum - 1;
			actor->animInfo.ticksPerFrame = 2;
			actor->animInfo.tickCounterOfCurrentFrame = 0;
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, "ordinary-held-effect-frame-repeat", 5, MT_NGOATBW, actor->ai, MissileID::Arrow, false, "", *actor);
			row.String("boundary", "prepared-held-normal-effect-frame");
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < 2; ++tick) {
				if (tick != 0) stream << ',';
				const size_t before = Missiles.size();
				const WorldTilePosition future { 60, 51 };
				MyPlayer->position.future = future;
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Boolean("releaseFrameEligible", true);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", before);
				ProcessMonsters();
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", Missiles.size() > before);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Key("missiles");
				writeMissiles();
			}
			stream << ']';
			row.Boolean("repeatObserved", Missiles.size() == 2U);
			EXPECT_EQ(Missiles.size(), 2U);
		}

		// Special release additionally requires counter zero at the effect frame.
		{
			Monster *actor = prepareActor(8, MT_NMAGMA, nullptr);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(specialSeed(*actor));
			ProcessMonsters();
			ASSERT_EQ(actor->mode, MonsterMode::SpecialRangedAttack);
			actor->animInfo.currentFrame = actor->data().animFrameNumSpecial - 1;
			actor->animInfo.ticksPerFrame = 3;
			actor->animInfo.tickCounterOfCurrentFrame = 1;
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, "special-counter-gated-release", 8, MT_NMAGMA, actor->ai, MissileID::MagmaBall, true, "", *actor);
			row.String("boundary", "prepared-special-effect-frame-counter-gate");
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < 2; ++tick) {
				if (tick != 0) stream << ',';
				if (tick == 1) {
					actor->animInfo.currentFrame = actor->data().animFrameNumSpecial - 1;
					actor->animInfo.tickCounterOfCurrentFrame = 0;
				}
				const size_t before = Missiles.size();
				const WorldTilePosition future { 60, static_cast<WorldTileCoord>(51 + tick) };
				MyPlayer->position.future = future;
				const bool eligible = actor->mode == MonsterMode::SpecialRangedAttack && actor->animInfo.currentFrame == actor->data().animFrameNumSpecial - 1 && actor->animInfo.tickCounterOfCurrentFrame == 0;
				JsonObject state(stream);
				state.Number("tick", tick);
				state.Boolean("releaseFrameEligible", eligible);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", before);
				ProcessMonsters();
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", Missiles.size() > before);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Key("missiles");
				writeMissiles();
			}
			stream << ']';
			row.Boolean("counterGateObserved", Missiles.size() == 1U);
			EXPECT_EQ(Missiles.size(), 1U);
		}

		auto writeInterruptedSpecial = [&](std::string_view name, bool petrified) {
			Monster *actor = prepareActor(8, MT_NMAGMA, nullptr);
			ASSERT_NE(actor, nullptr);
			Missiles.clear();
			MissilePreFlag = false;
			SetRndSeed(specialSeed(*actor));
			ProcessMonsters();
			ASSERT_EQ(actor->mode, MonsterMode::SpecialRangedAttack);
			actor->animInfo.currentFrame = actor->data().animFrameNumSpecial - 1;
			actor->animInfo.tickCounterOfCurrentFrame = 0;
			if (petrified)
				actor->petrify();
			else
				actor->startHit(std::numeric_limits<int>::max());
			nextCase();
			JsonObject row(stream);
			writeIdentity(row, name, 8, MT_NMAGMA, actor->ai, MissileID::MagmaBall, true, "", *actor);
			row.String("boundary", "prepared-special-effect-frame-interruption");
			row.String("interrupt", petrified ? "petrified" : "hard-hit");
			row.Key("ticks");
			stream << '[';
			const size_t before = Missiles.size();
			const WorldTilePosition future { 60, 52 };
			MyPlayer->position.future = future;
			{
				JsonObject state(stream);
				state.Number("tick", 0);
				state.Boolean("releaseFrameEligible", false);
				state.Number("beforeRandom", GetLCGEngineState());
				state.Key("beforeActor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("beforeEnemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("beforePlayerFuture");
				WriteRoomPoint(stream, { future.x, future.y });
				state.Number("missileCountBefore", before);
				ProcessMonsters();
				state.Number("random", GetLCGEngineState());
				state.Number("missileCountAfter", Missiles.size());
				state.Boolean("missileCreated", Missiles.size() > before);
				state.Key("actor");
				WriteRoomMonster(stream, static_cast<int>(actor->getId()), *actor);
				state.Key("enemyPosition");
				WriteRoomPoint(stream, { actor->enemyPosition.x, actor->enemyPosition.y });
				state.Key("playerFuture");
				WriteRoomPoint(stream, { MyPlayer->position.future.x, MyPlayer->position.future.y });
				state.Key("missiles");
				writeMissiles();
			}
			stream << ']';
			row.Boolean("releaseSuppressed", Missiles.size() == before);
			EXPECT_EQ(Missiles.size(), before);
		};
		writeInterruptedSpecial("special-petrified-before-release", true);
		writeInterruptedSpecial("special-hard-hit-before-release", false);
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	ASSERT_TRUE(!stream.fail());
	EXPECT_EQ(caseCount, 9);
	EXPECT_LE(std::filesystem::file_size(root / "native-catacombs-ranged-release.json"), 32U * 1024U * 1024U);
}

struct NativeArrowMissileOrdinals {
	std::vector<std::pair<const Missile *, int>> values;
	int next = 1;

	int Get(const Missile &missile)
	{
		const auto found = std::find_if(values.begin(), values.end(), [&](const auto &entry) { return entry.first == &missile; });
		if (found != values.end())
			return found->second;
		values.emplace_back(&missile, next);
		return next++;
	}

	void Prune()
	{
		values.erase(std::remove_if(values.begin(), values.end(), [&](const auto &entry) {
			return std::none_of(Missiles.begin(), Missiles.end(), [&](const Missile &missile) { return &missile == entry.first; });
		}),
		    values.end());
	}
};

void WriteArrowPlayer(std::ostream &out, size_t index, const Player &player)
{
	JsonObject json(out);
	json.Number("playerIndex", index);
	json.Number("heroClassId", player._pClass);
	json.Number("characterLevel", player.getCharacterLevel());
	json.Number("levelId", player.plrlevel);
	json.Number("experience", player._pExperience);
	json.Boolean("active", player.plractive);
	json.Number("mode", player._pmode);
	json.Number("direction", player.direction);
	json.Key("tile");
	WriteRoomPoint(out, player.position.tile);
	json.Key("future");
	WriteRoomPoint(out, player.position.future);
	json.Key("old");
	WriteRoomPoint(out, player.position.old);
	json.Number("lifeQ6", player.life.current);
	json.Number("maximumLifeQ6", player.life.maximum);
	json.Number("armorClass", player._pArmorClass);
	json.Number("armorValue", player.GetArmor());
	json.Number("blockChanceWithoutLevel", player.GetBlockChance(false));
	json.Boolean("blockFlag", player._pBlockFlag);
	json.Boolean("invincible", player._pInvincible);
	json.Boolean("etherealized", HasAnyOf(player._pSpellFlags, SpellFlag::Etherealize));
	json.Number("spellFlags", player._pSpellFlags);
	json.Number("arghClangBaseSoundId", GetHeroSound(player._pClass, HeroSpeech::ArghClang));
}

bool IsArrowMonsterActive(size_t slot)
{
	return std::find(ActiveMonsters, ActiveMonsters + ActiveMonsterCount, static_cast<unsigned>(slot)) != ActiveMonsters + ActiveMonsterCount;
}

void WriteArrowMonster(std::ostream &out, int slot, const Monster &monster)
{
	JsonObject json(out);
	json.Number("slot", slot);
	json.Number("nativeType", monster.type().type);
	json.Boolean("active", IsArrowMonsterActive(slot));
	json.Key("tile");
	WriteRoomPoint(out, monster.position.tile);
	json.Key("future");
	WriteRoomPoint(out, monster.position.future);
	json.Key("old");
	WriteRoomPoint(out, monster.position.old);
	json.Number("mode", monster.mode);
	json.Number("hitPointsQ6", monster.hitPoints);
	json.Number("maximumHitPointsQ6", monster.maxHitPoints);
	json.Boolean("hasNoLife", monster.hasNoLife());
	json.Boolean("invalid", monster.isInvalid);
	json.Number("goal", monster.goal);
	json.Number("whoHit", monster.whoHit);
	json.Number("corpseId", monster.corpseId);
	json.Number("uniqueType", monster.uniqueType);
	json.Number("killCountForType", MonsterKillCounts[monster.type().type]);
	json.Number("itemSeed", monster.rndItemSeed);
	json.Number("aiSeed", monster.aiSeed);
	json.Number("treasure", monster.data().treasure);
	json.Number("experienceValue", monster.exp(DIFF_NORMAL));
	json.Number("level", monster.level(DIFF_NORMAL));
	json.Number("toHit", monster.toHit(DIFF_NORMAL));
	json.Number("minimumDamage", monster.minDamage);
	json.Number("maximumDamage", monster.maxDamage);
	json.Number("armorClass", monster.armorClass);
	json.Number("faction", monster.isPlayerMinion() ? 1 : 0);
	json.Boolean("playerMinion", monster.isPlayerMinion());
	json.Boolean("berserk", (monster.flags & MFLAG_BERSERK) != 0);
	json.Boolean("petrified", monster.mode == MonsterMode::Petrified || monster.animInfo.isPetrified);
	json.Number("flags", monster.flags);
}

void WriteArrowMissile(std::ostream &out, const Missile &missile, int ordinal)
{
	JsonObject json(out);
	json.Number("ordinal", ordinal);
	json.Number("type", missile._mitype);
	json.Number("sourceSlot", missile._misource);
	json.Number("caster", missile._micaster);
	json.Number("damage", missile._midam);
	json.Number("frameGroupDirection16", missile.getFrameGroupRaw());
	json.Number("animationFrameRaw", missile._miAnimFrame);
	json.Number("duration", missile.duration);
	json.Number("distance", missile._midist);
	json.Boolean("hit", missile._miHitFlag);
	json.Boolean("delete", missile._miDelFlag);
	json.Number("lastCollisionTargetHash", missile.lastCollisionTargetHash);
	json.Key("start");
	WriteRoomPoint(out, missile.position.start);
	json.Key("tile");
	WriteRoomPoint(out, missile.position.tile);
	json.Key("velocity16_16");
	WriteRoomPoint(out, { missile.position.velocity.deltaX, missile.position.velocity.deltaY });
	json.Key("traveled16_16");
	WriteRoomPoint(out, { missile.position.traveled.deltaX, missile.position.traveled.deltaY });
	json.Key("rawProjectileUnionDestination");
	WriteRoomPoint(out, { missile.projectile.destX, missile.projectile.destY });
}

void WriteArrowActiveMonsterPool(std::ostream &out)
{
	JsonObject json(out);
	json.Number("activeCount", ActiveMonsterCount);
	json.Key("activeIds");
	out << '[';
	for (size_t index = 0; index < ActiveMonsterCount; ++index) {
		if (index != 0) out << ',';
		out << ActiveMonsters[index];
	}
	out << ']';
}

void WriteArrowItemPool(std::ostream &out)
{
	JsonObject json(out);
	json.Number("activeCount", ActiveItemCount);
	json.Key("poolOrder");
	out << '[';
	for (int index = 0; index < MAXITEMS; ++index) {
		if (index != 0) out << ',';
		out << static_cast<int>(ActiveItems[index]);
	}
	out << ']';
	json.Key("activeItems");
	out << '[';
	for (int index = 0; index < ActiveItemCount; ++index) {
		if (index != 0) out << ',';
		JsonObject row(out);
		const int slot = ActiveItems[index];
		row.Number("slot", slot);
		row.Key("item");
		WriteItem(out, Items[slot]);
	}
	out << ']';
	json.Key("itemRefs");
	out << '[';
	int tileIndex = 0;
	for (const Tile &tile : tiles()) {
		if (tileIndex++ != 0) out << ',';
		out << static_cast<int>(tile.item());
	}
	out << ']';
}

void AddArrowSampleCell(std::vector<Point> &cells, Point position)
{
	if (!InDungeonBounds(position))
		return;
	if (std::find(cells.begin(), cells.end(), position) == cells.end())
		cells.push_back(position);
}

void WriteArrowCollisionCells(std::ostream &out, const std::vector<Point> &focusCells, const std::vector<int> &actorSlots, const NativeArrowMissileOrdinals & /*ordinals*/)
{
	std::vector<Point> cells = focusCells;
	for (const Player &player : Players) {
		AddArrowSampleCell(cells, player.position.tile);
		AddArrowSampleCell(cells, player.position.future);
		AddArrowSampleCell(cells, player.position.old);
	}
	for (int slot : actorSlots) {
		if (slot < 0 || slot >= MaxMonsters)
			continue;
		const Monster &actor = Monsters[slot];
		AddArrowSampleCell(cells, actor.position.tile);
		AddArrowSampleCell(cells, actor.position.future);
		AddArrowSampleCell(cells, actor.position.old);
	}
	for (const Missile &missile : Missiles) {
		AddArrowSampleCell(cells, missile.position.tile);
		AddArrowSampleCell(cells, missile.position.start);
		for (int y = -1; y <= 1; ++y)
			for (int x = -1; x <= 1; ++x)
				AddArrowSampleCell(cells, missile.position.tile + Displacement { x, y });
	}
	std::sort(cells.begin(), cells.end(), [](Point left, Point right) { return left.y != right.y ? left.y < right.y : left.x < right.x; });
	out << '[';
	bool first = true;
	for (Point position : cells) {
		if (!first)
			out << ',';
		first = false;
		const Tile &tile = tileAt(position);
		const int piece = tile.piece();
		const int signedMonster = tile.monster();
		const int signedPlayer = tile.player();
		const int objectValue = tile.object();
		JsonObject json(out);
		json.Key("position");
		WriteRoomPoint(out, position);
		json.Number("piece", piece);
		json.Number("solFlags", piece >= 0 && piece < MAXTILES ? tileProperties()[piece] : TileProperties::None);
		json.Number("signedMonster", signedMonster);
		json.Number("signedPlayer", signedPlayer);
		json.Number("signedOccupancyXor", signedMonster ^ signedPlayer);
		json.Number("signedObject", objectValue);
		json.Number("item", tile.item());
		json.Number("corpse", tile.corpse());
		json.Boolean("blockedByTileOrObject", IsMissileBlockedByTile(position));
		if (objectValue == 0) {
			json.Key("object");
			out << "null";
		} else {
			const Object &object = Objects[std::abs(objectValue) - 1];
			json.Key("object");
			JsonObject objectJson(out);
			objectJson.Number("slot", std::abs(objectValue) - 1);
			objectJson.Number("type", object._otype);
			objectJson.Number("breakState", object._oBreak);
			objectJson.Boolean("missilePassable", object._oMissFlag);
		}
	}
	out << ']';
}

TEST_F(ThemeRoomGenerationTest, CaptureNativeCatacombsArrowFlightAndImpact)
{
	const char *directory = std::getenv("GODEVILUTION_CATACOMBS_ARROW_FLIGHT_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Set the explicit Catacombs Arrow flight oracle directory.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native Arrow evidence.";
	struct ExecutionGuard {
		bool sound = gbSndInited, headless = HeadlessMode, missilePreFlag = MissilePreFlag;
		uint8_t playerId = MyPlayerId;
		uint32_t random = GetLCGEngineState();
		std::array<int, NUM_MAX_MTYPES> oldMonsterKillCounts {};
		std::list<Missile> savedMissiles;
		ScopedMissileDataForTest missileData;
		bool networkInitialized = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		ExecutionGuard()
		{
			Missiles.swap(savedMissiles);
			std::copy_n(MonsterKillCounts, oldMonsterKillCounts.size(), oldMonsterKillCounts.begin());
		}
		~ExecutionGuard()
		{
			Missiles.swap(savedMissiles);
			MissilePreFlag = missilePreFlag;
			std::copy(oldMonsterKillCounts.begin(), oldMonsterKillCounts.end(), MonsterKillCounts);
			if (networkInitialized) SNetDestroy();
			MyPlayerId = playerId;
			gbSndInited = sound;
			HeadlessMode = headless;
			SetRndSeed(random);
		}
	} guard;
	ASSERT_TRUE(guard.networkInitialized);
	HeadlessMode = true;
	gbSndInited = false;
	MyPlayerId = 0;
	LoadPlayerDataFiles();
	LoadMissileData();
	ASSERT_TRUE(IsMissileDataAvailable(MissileID::Arrow));
	const MissileData &arrowData = GetMissileData(MissileID::Arrow);

	std::ofstream stream(root / "native-catacombs-arrow-flight.json", std::ios::binary | std::ios::trunc);
	ASSERT_TRUE(stream.good());
	int caseCount = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-catacombs-arrow-flight");
		document.Number("version", 1);
		document.String("policy", "prepared-original-diablo-catacombs-arrow-flight-impact");
		document.Key("cases");
		stream << '[';
		auto className = [](HeroClass heroClass) -> std::string_view {
			switch (heroClass) {
			case HeroClass::Warrior: return "warrior";
			case HeroClass::Sorcerer: return "sorcerer";
			default: return "unsupported";
			}
		};
		auto configureHero = [&](HeroClass heroClass, uint8_t characterLevel, Point position) {
			Player hero {};
			hero._pClass = heroClass;
			hero.setCharacterLevel(characterLevel);
			const ClassAttributes &attributes = hero.getClassAttributes();
			hero.attributes.strength.base = hero.attributes.strength.current = attributes.baseStr;
			hero.attributes.magic.base = hero.attributes.magic.current = attributes.baseMag;
			hero.attributes.dexterity.base = hero.attributes.dexterity.current = attributes.baseDex;
			hero.attributes.vitality.base = hero.attributes.vitality.current = attributes.baseVit;
			const int life = hero.calculateBaseLife();
			const int mana = hero.calculateBaseMana();
			hero.life = { life, life, life, life, 100 };
			hero.mana = { mana, mana, mana, mana, 100 };
			hero.setAnimations();
			hero.plractive = true;
			hero.plrlevel = 5;
			hero.plrIsOnSetLevel = false;
			hero._pLvlChanging = false;
			hero.pLvlLoad = 1;
			hero._pInvincible = false;
			hero._pBlockFlag = false;
			hero._pArmorClass = 0;
			hero._pIAC = 0;
			hero._pIBonusAC = 0;
			hero._pmode = PM_STAND;
			hero.position.tile = position;
			hero.position.future = position;
			hero.position.old = position;
			return hero;
		};
		auto seedForPlayerHit = [&](const Player &hero, const Monster &source, bool requireBlock, bool requireHit) {
			int hitChance = static_cast<int>(source.toHit(DIFF_NORMAL))
			    + (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2 + 30 - 2 - hero.GetArmor();
			hitChance = std::max(hitChance, 10);
			const int blockChance = std::clamp(hero.GetBlockChance(false)
			        - (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2,
			    0, 100);
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				const int hitRoll = candidate.generateRnd(100);
				const int blockRoll = requireBlock ? candidate.generateRnd(100) : 100;
				if ((hitRoll < hitChance) == requireHit && (!requireBlock || blockRoll < blockChance))
					return seed;
			}
			return UINT32_MAX;
		};
		auto seedForBlockThenHit = [&](const Player &hero, const Monster &source) {
			int hitChance = static_cast<int>(source.toHit(DIFF_NORMAL))
			    + (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2 + 30 - 2 - hero.GetArmor();
			hitChance = std::max(hitChance, 10);
			const int blockChance = std::clamp(hero.GetBlockChance(false)
			        - (static_cast<int>(source.level(DIFF_NORMAL)) - hero.getCharacterLevel()) * 2,
			    0, 100);
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				const int firstHitRoll = candidate.generateRnd(100);
				const int firstBlockRoll = candidate.generateRnd(100);
				candidate.generateRnd(1); // PlayerMHit rolls arrow damage before testing the block result.
				const int secondHitRoll = candidate.generateRnd(100);
				if (firstHitRoll < hitChance && firstBlockRoll < blockChance && secondHitRoll < hitChance)
					return seed;
			}
			return UINT32_MAX;
		};
		auto seedForMonsterHit = [](const Player &, const Monster &) {
			constexpr int MonsterArrowHitChance = 89; // MonsterTrapHit uses 90 - armor(0) - first-step distance(1).
			for (uint32_t seed = 0; seed < 100000; ++seed) {
				DiabloGenerator candidate(seed);
				if (candidate.generateRnd(100) < MonsterArrowHitChance)
					return seed;
			}
			return UINT32_MAX;
		};
		auto findMonsterLootSeed = [](const Monster &actor) {
			for (uint32_t candidate = 0; candidate < 65536; ++candidate) {
				SetRndSeed(candidate);
				if (RndItemForMonsterLevel(static_cast<int8_t>(actor.level(DIFF_NORMAL))) != IDI_NONE)
					return candidate;
			}
			return UINT32_MAX;
		};

		auto runCase = [&](std::string_view name, std::string_view boundary, HeroClass heroClass, uint8_t heroLevel,
		                   Point sourcePosition, Point heroPosition, Point destination, int tickLimit,
		                   auto prepare, auto chooseSeed, auto launch, auto mutate) {
			PrepareRoom(5, 0, 0, 0, false);
			for (Tile &tile : tiles()) {
				tile.setPlayer(0);
				tile.setMonster(0);
				tile.setObject(0);
			}
			Missiles.clear();
			MissilePreFlag = false;
			Players.resize(1);
			MyPlayer = &Players[0];
			*MyPlayer = configureHero(heroClass, heroLevel, heroPosition);
			MyPlayer->occupyTile(heroPosition, false);
			LevelBestiary.init();
			std::vector<std::pair<_monster_id, size_t>> typeIndices;
			std::vector<Monster *> actors;
			std::vector<int> actorSlots;
			std::vector<Point> focusCells { sourcePosition, heroPosition, destination };
			auto spawnActor = [&](_monster_id type, Point position) -> Monster * {
				const auto existing = std::find_if(typeIndices.begin(), typeIndices.end(), [&](const auto &entry) { return entry.first == type; });
				size_t typeIndex;
				if (existing == typeIndices.end()) {
					const auto added = LevelBestiary.addType(type, PLACE_SCATTER);
					if (!added.has_value()) return nullptr;
					typeIndex = *added;
					typeIndices.emplace_back(type, typeIndex);
				} else {
					typeIndex = existing->second;
				}
				Monster *actor = AddMonster(position, Direction::SouthEast, typeIndex, true);
				if (actor != nullptr) {
					actor->enemy = 0;
					actor->enemyPosition = heroPosition;
					actor->activeForTicks = 0;
					actor->position.last = position;
					actor->position.future = position;
					actor->position.old = position;
					actor->hitPoints = actor->maxHitPoints = 10000 << 6;
					actors.push_back(actor);
					actorSlots.push_back(static_cast<int>(actor->getId()));
					AddArrowSampleCell(focusCells, position);
				}
				return actor;
			};
			Monster *source = spawnActor(MT_WSKELBW, sourcePosition);
			ASSERT_NE(source, nullptr);
			source->minDamage = 1;
			source->maxDamage = 1;
			prepare(*MyPlayer, *source, spawnActor, actors, actorSlots, focusCells);
			const uint32_t seed = chooseSeed(*MyPlayer, *source);
			ASSERT_NE(seed, UINT32_MAX) << "No deterministic seed matches the requested native hit/block boundary: " << name;
			SetRndSeed(seed);
			const bool captureItemPool = name == "monster-impact-remaining-life-64" || name == "monster-impact-lethal-fractional-63";
			bool observedMapEdgeDeletion = false;
			NativeArrowMissileOrdinals ordinals;
			if (caseCount != 0)
				stream << ',';
			JsonObject row(stream);
			row.String("name", name);
			row.String("boundary", boundary);
			row.Number("heroClassId", heroClass);
			row.String("heroClass", className(heroClass));
			row.Number("heroLevel", MyPlayer->getCharacterLevel());
			row.Number("sourceSlot", source->getId());
			row.Number("sourceType", source->type().type);
			row.Key("sourceStart");
			WriteRoomPoint(stream, sourcePosition);
			row.Key("destination");
			WriteRoomPoint(stream, destination);
			row.Number("initialSeed", seed);
			row.Key("missileData");
			{
				JsonObject data(stream);
				data.Number("missileId", MissileID::Arrow);
				data.Number("damageType", arrowData.damageType());
				data.Number("castSoundId", arrowData.castSound);
				data.Number("hitSoundId", arrowData.hitSound);
				data.Number("movementDistribution", arrowData.movementDistribution);
			}
			int launchCount = 0;
			auto emitArrow = [&](std::ostream &out, Point start, Point target, Direction fallbackDirection, bool stationary) {
				const uint32_t rngBefore = GetLCGEngineState();
				Missile *missile = AddMissile(start, target, fallbackDirection, MissileID::Arrow, TARGET_PLAYERS, *source, 0, 0, nullptr, SfxID::None);
				if (missile == nullptr) {
					ADD_FAILURE() << "Native Arrow creation failed.";
					return;
				}
				if (stationary) missile->position.velocity = { 0, 0 };
				if (launchCount++ != 0) out << ',';
				JsonObject launchRow(out);
				const int ordinal = ordinals.Get(*missile);
				launchRow.Number("ordinal", ordinal);
				launchRow.Number("rngBefore", rngBefore);
				launchRow.Number("rngAfter", GetLCGEngineState());
				launchRow.Key("start");
				WriteRoomPoint(out, start);
				launchRow.Number("requestedTargetX", target.x);
				launchRow.Number("requestedTargetY", target.y);
				launchRow.Number("launchFacingDirection", fallbackDirection);
				launchRow.Boolean("preparedStationaryVelocity", stationary);
				launchRow.Number("direction16", missile->_miAnimFrame - 1);
				launchRow.Number("frameGroupDirection16", missile->getFrameGroupRaw());
				launchRow.Number("animationFrameRaw", missile->_miAnimFrame);
				launchRow.Key("velocity16_16");
				WriteRoomPoint(out, { missile->position.velocity.deltaX, missile->position.velocity.deltaY });
			};
			row.Key("launches");
			stream << '[';
			launch(stream, *MyPlayer, *source, actors, actorSlots, focusCells, emitArrow);
			stream << ']';
			auto writePlayers = [&]() {
				stream << '[';
				for (size_t index = 0; index < Players.size(); ++index) {
					if (index != 0) stream << ',';
					WriteArrowPlayer(stream, index, Players[index]);
				}
				stream << ']';
			};
			auto writeActors = [&]() {
				stream << '[';
				for (size_t index = 0; index < actorSlots.size(); ++index) {
					if (index != 0) stream << ',';
					WriteArrowMonster(stream, actorSlots[index], Monsters[actorSlots[index]]);
				}
				stream << ']';
			};
			auto writeMissiles = [&]() {
				std::vector<int> ordinalsNow;
				stream << '[';
				bool first = true;
				for (const Missile &missile : Missiles) {
					if (!first) stream << ',';
					first = false;
					const int ordinal = ordinals.Get(missile);
					ordinalsNow.push_back(ordinal);
					WriteArrowMissile(stream, missile, ordinal);
				}
				stream << ']';
				return ordinalsNow;
			};
			row.Key("initial");
			{
				JsonObject initial(stream);
				initial.Number("rng", GetLCGEngineState());
				initial.Key("players");
				writePlayers();
				initial.Key("actors");
				writeActors();
				initial.Key("activeMonsterPool");
				WriteArrowActiveMonsterPool(stream);
				if (captureItemPool) {
					initial.Key("itemPool");
					WriteArrowItemPool(stream);
				}
				initial.Key("missiles");
				writeMissiles();
				initial.Key("collisionCells");
				WriteArrowCollisionCells(stream, focusCells, actorSlots, ordinals);
			}
			row.Key("ticks");
			stream << '[';
			for (int tick = 0; tick < tickLimit; ++tick) {
				if (tick != 0) stream << ',';
				JsonObject state(stream);
				state.Number("tickIndex", tick);
				state.Key("mutationInputs");
				{
					JsonObject inputs(stream);
					mutate(tick, *MyPlayer, *source, spawnActor, actors, actorSlots, focusCells, inputs);
				}
				state.Number("rngBefore", GetLCGEngineState());
				state.Boolean("missilePreFlagBefore", MissilePreFlag);
				state.Key("playersBefore");
				writePlayers();
				state.Key("actorsBefore");
				writeActors();
				state.Key("missilesBefore");
				const std::vector<int> liveBefore = writeMissiles();
				state.Key("collisionCellsBefore");
				WriteArrowCollisionCells(stream, focusCells, actorSlots, ordinals);
				const int playerLifeBeforeStep = MyPlayer->life.current;
				const int targetHitPointsBeforeStep = actorSlots.size() > 1 ? Monsters[actorSlots.back()].hitPoints : 0;
				const int targetKillCountBeforeStep = actorSlots.size() > 1 ? MonsterKillCounts[Monsters[actorSlots.back()].type().type] : 0;
				const size_t activeMonsterCountBeforeStep = ActiveMonsterCount;
				const uint8_t activeItemCountBeforeStep = ActiveItemCount;
				state.Key("activeMonsterPoolBefore");
				WriteArrowActiveMonsterPool(stream);
				if (captureItemPool) {
					state.Key("itemPoolBefore");
					WriteArrowItemPool(stream);
				}
				ProcessMissiles();
				if (tick == 0) {
					if (name == "warrior-low-damage-hit-no-recovery" || name == "sorcerer-low-damage-hit-no-recovery") {
						EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << name;
						EXPECT_EQ(MyPlayer->_pmode, PM_STAND) << name;
					} else if (name == "player-arrow-miss" || name == "player-arrow-invincible" || name == "player-arrow-etherealized" || name == "player-arrow-already-dead" || name == "tile-missile-blocker") {
						EXPECT_EQ(MyPlayer->life.current, playerLifeBeforeStep) << name;
					} else if (name == "player-arrow-block") {
						EXPECT_EQ(MyPlayer->life.current, playerLifeBeforeStep) << name;
						EXPECT_EQ(MyPlayer->_pmode, PM_BLOCK) << name;
					} else if (name == "player-hit-before-same-cell-tile-blocker" || name == "player-hit-before-same-cell-breakable-crux" || name == "final-lifetime-tick-collision") {
						EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << name;
					}
					if (name == "same-faction-monster-pass" || name == "moving-negative-nonpetrified-monster-ignored")
						ASSERT_EQ(Monsters[actorSlots.back()].hitPoints, targetHitPointsBeforeStep) << name;
					if (name == "opposing-player-minion-target" || name == "source-berserk-allows-same-faction-hit" || name == "target-berserk-allows-same-faction-hit" || name == "petrified-negative-monster-occupancy-hit")
						ASSERT_LT(Monsters[actorSlots.back()].hitPoints, targetHitPointsBeforeStep) << name;
				}
				if (tick == 0 && (name == "source-live-damage-changed-before-impact" || name == "source-removed-before-impact" || name == "source-dead-before-impact"))
					EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << name;
				if (tick == 0 && name == "moving-player-negative-then-settled")
					EXPECT_EQ(MyPlayer->life.current, playerLifeBeforeStep) << "A moving negative player reference is ignored.";
				if (tick == 1 && name == "moving-player-negative-then-settled")
					EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep) << "The settled positive player reference is hit.";
				if (tick == 1 && name == "new-monster-interceptor-at-current-cell")
					ASSERT_LT(Monsters[actorSlots.back()].hitPoints, targetHitPointsBeforeStep);
				if (tick == 1 && name == "new-player-interceptor-at-current-cell")
					ASSERT_LT(MyPlayer->life.current, playerLifeBeforeStep);
				if (tick == 0 && name == "ordered-two-arrow-block-then-hit") {
					ASSERT_EQ(liveBefore.size(), 2U);
					EXPECT_EQ(liveBefore[0], 1);
					EXPECT_EQ(liveBefore[1], 2);
					EXPECT_LT(MyPlayer->life.current, playerLifeBeforeStep);
					EXPECT_EQ(MyPlayer->_pmode, PM_BLOCK);
				}
				if (tick == 0 && name == "monster-impact-remaining-life-64") {
					ASSERT_EQ(targetHitPointsBeforeStep, 128);
					EXPECT_EQ(Monsters[actorSlots.back()].hitPoints, 64);
					EXPECT_FALSE(Monsters[actorSlots.back()].hasNoLife());
					EXPECT_NE(Monsters[actorSlots.back()].mode, MonsterMode::Death);
					EXPECT_EQ(MonsterKillCounts[Monsters[actorSlots.back()].type().type], targetKillCountBeforeStep);
				}
				if (tick == 0 && name == "monster-impact-lethal-fractional-63") {
					Monster &target = Monsters[actorSlots.back()];
					ASSERT_EQ(targetHitPointsBeforeStep, 127);
					ASSERT_TRUE(target.hasNoLife());
					EXPECT_EQ(target.hitPoints, 0);
					EXPECT_EQ(target.mode, MonsterMode::Death);
					EXPECT_EQ(MonsterKillCounts[target.type().type], targetKillCountBeforeStep + 1);
					EXPECT_EQ(ActiveMonsterCount, activeMonsterCountBeforeStep);
					EXPECT_EQ(ActiveItemCount, activeItemCountBeforeStep + 1);
					EXPECT_GT(MyPlayer->_pExperience, 0U);
				}
				if (tick == 0 && name == "player-hit-before-same-cell-breakable-crux") {
					EXPECT_EQ(Objects[0]._oBreak, -1);
					EXPECT_TRUE(Objects[0]._oMissFlag);
				}
				state.Number("rngAfter", GetLCGEngineState());
				state.Boolean("missilePreFlagAfter", MissilePreFlag);
				state.Key("playersAfter");
				writePlayers();
				state.Key("actorsAfter");
				writeActors();
				state.Key("missilesAfter");
				const std::vector<int> liveAfter = writeMissiles();
				state.Key("deletedMissileOrdinals");
				stream << '[';
				bool firstDeleted = true;
				for (int ordinal : liveBefore) {
					if (std::find(liveAfter.begin(), liveAfter.end(), ordinal) != liveAfter.end()) continue;
					if (!firstDeleted) stream << ',';
					firstDeleted = false;
					stream << ordinal;
				}
				stream << ']';
				if (name == "natural-map-edge-out-of-bounds-delete" && !liveBefore.empty()
				    && std::find(liveAfter.begin(), liveAfter.end(), liveBefore.front()) == liveAfter.end()) {
					observedMapEdgeDeletion = true;
					EXPECT_LT(tick, 32) << "An edge flight must expire by leaving the map, well before its 256-tick lifetime.";
				}
				state.Key("activeMonsterPoolAfter");
				WriteArrowActiveMonsterPool(stream);
				if (captureItemPool) {
					state.Key("itemPoolAfter");
					WriteArrowItemPool(stream);
				}
				state.Key("collisionCellsAfter");
				WriteArrowCollisionCells(stream, focusCells, actorSlots, ordinals);
				ordinals.Prune();
				if (name == "natural-map-edge-out-of-bounds-delete" && Missiles.empty()) break;
				if (Missiles.empty() && tick + 1 >= tickLimit) break;
			}
			if (name == "natural-map-edge-out-of-bounds-delete")
				EXPECT_TRUE(observedMapEdgeDeletion) << "The open edge corridor must terminate by native out-of-bounds deletion.";
			stream << ']';
			++caseCount;
		};
		auto noPrepare = [](Player &, Monster &, auto &, auto &, auto &, auto &) {};
		auto noMutation = [](int, Player &, Monster &, auto &, auto &, auto &, auto &, JsonObject &inputs) { inputs.String("kind", "none"); };
		auto fixedSeed = [](const Player &, const Monster &) { return UINT32_C(2588); };
		auto emitOneStationary = [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
			emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true);
		};

		for (int order = 0; order < 2; ++order) {
			runCase(order == 0 ? "direction-16-ordered-burst" : "direction-16-reversed-burst", "prepared-same-tick-native-add-arrow-order", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, noPrepare, fixedSeed, [&, order](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				std::vector<std::pair<int, Point>> destinations;
				for (int direction = 0; direction < 16; ++direction) {
					Point best { 50, 50 }; int bestScore = INT_MAX;
					for (int y = -12; y <= 12; ++y) for (int x = -12; x <= 12; ++x) {
						if (x == 0 && y == 0) continue;
						const Point candidate { 50 + x, 50 + y };
						if (GetDirection16({ 50, 50 }, candidate) != static_cast<Direction16>(direction)) continue;
						const int score = std::max(std::abs(x), std::abs(y)) * 64 + std::abs(x) + std::abs(y);
						if (score < bestScore) { best = candidate; bestScore = score; }
					}
					ASSERT_NE(best, (Point { 50, 50 }));
					destinations.emplace_back(direction, best);
				}
				if (order != 0) std::reverse(destinations.begin(), destinations.end());
				for (const auto &[direction, destination] : destinations)
					emitArrow(out, { 50, 50 }, destination, Direction::South, false); }, noMutation);
		}

		for (int direction = 0; direction < 16; ++direction) {
			std::ostringstream name;
			name << "natural-direction-" << std::setfill('0') << std::setw(2) << direction;
			runCase(name.str(), "native-add-arrow-direction16-fixed-point-flight", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 54, 54 }, 12, noPrepare, fixedSeed, [&, direction](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				Point destination { 50, 50 }; int bestScore = INT_MAX;
				for (int y = -12; y <= 12; ++y) for (int x = -12; x <= 12; ++x) {
					if (x == 0 && y == 0) continue;
					const Point candidate { 50 + x, 50 + y };
					if (GetDirection16({ 50, 50 }, candidate) != static_cast<Direction16>(direction)) continue;
					const int score = std::max(std::abs(x), std::abs(y)) * 64 + std::abs(x) + std::abs(y);
					if (score < bestScore) { destination = candidate; bestScore = score; }
				}
				ASSERT_NE(destination, (Point { 50, 50 }));
				emitArrow(out, { 50, 50 }, destination, Direction::South, false); }, noMutation);
		}

		runCase("natural-flight-beyond-requested-endpoint", "native-arrow-continues-until-collision-or-lifetime", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 52, 50 }, 16, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 52, 50 }, Direction::East, false); }, noMutation);
		runCase("natural-skipped-tile-diagonal", "native-arrow-fixed-point-skipped-tile-sampling", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 58, 57 }, 16, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 58, 57 }, Direction::SouthEast, false); }, noMutation);
		runCase("natural-map-edge-out-of-bounds-delete", "native-putmissile-deletes-after-leaving-dungeon-bounds", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 111, 50 }, 32, [](Player &, Monster &, auto &, auto &, auto &, auto &cells) {
			for (int x = 104; x <= 111; ++x) {
				tileAt({ x, 50 }).setPiece(0);
				tileAt({ x, 50 }).setTransVal(1);
				ASSERT_FALSE(IsMissileBlockedByTile({ x, 50 }));
				AddArrowSampleCell(cells, { x, 50 });
			} }, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				emitArrow(out, { 110, 50 }, { 111, 50 }, Direction::East, false);
				ASSERT_GT(Missiles.back().position.velocity.deltaX, 0);
				ASSERT_GT(Missiles.back().position.velocity.deltaY, 0); }, noMutation);
		runCase("stationary-full-256-tick-lifetime", "prepared-zero-velocity-arrow-lifetime-boundary", HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 50, 50 }, 256, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 51, 50 }, Direction::East, true); }, noMutation);

		auto playerCell = [](Player &hero, Monster &, auto &, auto &, auto &, auto &cells) {
			hero.position.tile = { 50, 50 };
			hero.position.future = hero.position.tile;
			hero.position.old = hero.position.tile;
			for (Tile &tile : tiles())
				tile.setPlayer(0);
			hero.occupyTile(hero.position.tile, false);
			ASSERT_GT(tileAt(hero.position.tile).player(), 0);
			AddArrowSampleCell(cells, hero.position.tile);
		};
		auto lowDamage = [](int, Player &, Monster &source, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			source.minDamage = source.maxDamage = 1;
			inputs.String("kind", "source-damage-fixed-before-step");
			inputs.Number("minimumDamageInput", 1);
			inputs.Number("maximumDamageInput", 1);
		};
		auto hitSeed = [&](const Player &hero, const Monster &source) { return seedForPlayerHit(hero, source, false, true); };
		auto missSeed = [&](const Player &hero, const Monster &source) { return seedForPlayerHit(hero, source, false, false); };
		runCase("warrior-low-damage-hit-no-recovery", "native-player-mhit-low-damage-arghclang-before-recovery-check",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, lowDamage);
		runCase("sorcerer-low-damage-hit-no-recovery", "native-player-mhit-low-damage-arghclang-before-recovery-check",
		    HeroClass::Sorcerer, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, lowDamage);
		runCase("player-arrow-miss", "native-arrow-player-hit-roll-miss", HeroClass::Warrior, 2,
		    { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, missSeed, emitOneStationary, lowDamage);
		auto blockPrepare = [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
			playerCell(hero, source, spawn, actors, slots, cells);
			hero.attributes.dexterity.current = 90;
			hero._pBlockFlag = true;
			ASSERT_GT(hero.GetBlockChance(false), 0);
		};
		auto blockSeed = [&](const Player &hero, const Monster &source) { return seedForPlayerHit(hero, source, true, true); };
		runCase("player-arrow-block", "native-arrow-hit-then-block-roll-and-start-block", HeroClass::Warrior, 2,
		    { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, blockPrepare, blockSeed, emitOneStationary, lowDamage);
		runCase("ordered-two-arrow-block-then-hit", "native-same-tick-arrow-order-changes-player-block-eligibility", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
			playerCell(hero, source, spawn, actors, slots, cells);
			hero.attributes.dexterity.current = 90;
			hero._pBlockFlag = true;
			ASSERT_GT(hero.GetBlockChance(false), 0); }, seedForBlockThenHit, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true);
				emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true); }, [](int tick, Player &, Monster &, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				inputs.String("kind", "native-ordered-two-arrow-block-then-hit");
				inputs.Number("firstExpectedOrdinal", 1); inputs.Number("secondExpectedOrdinal", 2);
			} else inputs.String("kind", "none"); });
		runCase("player-arrow-invincible", "native-player-mhit-invincible-early-return", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) { playerCell(hero, source, spawn, actors, slots, cells); hero._pInvincible = true; ASSERT_TRUE(hero._pInvincible); }, fixedSeed, emitOneStationary, lowDamage);
		runCase("player-arrow-etherealized", "native-arrow-etherealize-early-return", HeroClass::Sorcerer, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) { playerCell(hero, source, spawn, actors, slots, cells); hero._pSpellFlags |= SpellFlag::Etherealize; ASSERT_TRUE(HasAnyOf(hero._pSpellFlags, SpellFlag::Etherealize)); }, fixedSeed, emitOneStationary, lowDamage);
		runCase("player-arrow-already-dead", "native-player-mhit-dead-early-return", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) { playerCell(hero, source, spawn, actors, slots, cells); hero.life.current = 0; ASSERT_EQ(hero.life.current, 0); }, fixedSeed, emitOneStationary, lowDamage);

		auto movingPlayerPrepare = [&](Player &hero, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
			playerCell(hero, source, spawn, actors, slots, cells);
			hero.position.future = { 51, 50 };
			hero.occupyTile({ 50, 50 }, true);
		};
		auto movingPlayerMutation = [](int tick, Player &hero, Monster &, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				ASSERT_LT(tileAt({ 50, 50 }).player(), 0);
				inputs.String("kind", "moving-player-negative-occupancy");
				inputs.Number("signedPlayerBefore", tileAt({ 50, 50 }).player());
			} else {
				hero.position.future = hero.position.tile;
				hero.occupyTile(hero.position.tile, false);
				ASSERT_GT(tileAt({ 50, 50 }).player(), 0);
				inputs.String("kind", "moving-player-settled-to-positive-occupancy");
				inputs.Number("signedPlayerBefore", tileAt({ 50, 50 }).player());
			}
		};
		runCase("moving-player-negative-then-settled", "native-stationary-target-hash-change-and-moving-player-ignore",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 2, movingPlayerPrepare, hitSeed, emitOneStationary, movingPlayerMutation);
		runCase("new-monster-interceptor-at-current-cell", "prepared-new-interceptor-before-native-arrow-step", HeroClass::Warrior, 2, { 44, 50 }, { 80, 80 }, { 50, 50 }, 3, noPrepare, fixedSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 56, 50 }, Direction::East, false); }, [](int tick, Player &, Monster &, auto &spawn, auto &actors, auto &, auto &cells, JsonObject &inputs) {
				if (tick != 1 || Missiles.empty()) { inputs.String("kind", "none"); return; }
				const Point position = Missiles.front().position.tile;
				Missiles.front().position.velocity = { 0, 0 };
				Monster *interceptor = spawn(MT_WSKELBW, position);
				inputs.String("kind", "spawn-native-monster-interceptor"); inputs.Number("positionX", position.x); inputs.Number("positionY", position.y);
				if (interceptor != nullptr) {
					interceptor->armorClass = 0; interceptor->flags |= MFLAG_GOLEM; interceptor->golemToHit = 80;
					ASSERT_TRUE(interceptor->isPlayerMinion());
					ASSERT_GT(tileAt(position).monster(), 0);
					cells.push_back(position); inputs.Number("slot", interceptor->getId()); inputs.Number("signedOccupancy", tileAt(position).monster());
				}
				else inputs.Number("slot", -1);
				(void)actors; });
		runCase("new-player-interceptor-at-current-cell", "prepared-player-moves-onto-stationary-arrow-cell", HeroClass::Warrior, 2, { 44, 50 }, { 80, 80 }, { 50, 50 }, 3, noPrepare, hitSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 56, 50 }, Direction::East, false); }, [](int tick, Player &hero, Monster &, auto &, auto &, auto &, auto &cells, JsonObject &inputs) {
				if (tick != 1 || Missiles.empty()) { inputs.String("kind", "none"); return; }
				const Point oldPosition = hero.position.tile;
				const Point position = Missiles.front().position.tile;
				Missiles.front().position.velocity = { 0, 0 };
				for (Tile &tile : tiles()) tile.setPlayer(0);
				hero.position.old = oldPosition; hero.position.tile = position; hero.position.future = position;
				hero.occupyTile(position, false);
				ASSERT_GT(tileAt(position).player(), 0);
				cells.push_back(position);
				inputs.String("kind", "move-player-to-current-arrow-cell-before-step");
				inputs.Number("oldPositionX", oldPosition.x); inputs.Number("oldPositionY", oldPosition.y);
				inputs.Number("newPositionX", position.x); inputs.Number("newPositionY", position.y);
				inputs.Number("signedPlayerInput", tileAt(position).player()); });

		auto stationaryMonsterTarget = [&](std::string_view caseName, bool sourceGolem, bool sourceBerserk, bool targetGolem, bool targetBerserk, bool targetPetrified, bool targetMoving) {
			auto prepare = [=](Player &, Monster &source, auto &spawn, auto &actors, auto &slots, auto &cells) {
				source.minDamage = source.maxDamage = 1;
				if (sourceGolem) {
					source.flags |= MFLAG_GOLEM;
					source.golemToHit = 80;
					ASSERT_TRUE(source.isPlayerMinion());
				}
				if (sourceBerserk) source.flags |= MFLAG_BERSERK;
				Monster *target = spawn(MT_WSKELBW, { 50, 50 });
				ASSERT_NE(target, nullptr);
				if (targetGolem) {
					target->flags |= MFLAG_GOLEM;
					target->golemToHit = 80;
				}
				if (targetBerserk) target->flags |= MFLAG_BERSERK;
				if (targetPetrified) {
					target->petrify();
					target->occupyTile({ 50, 50 }, true);
					ASSERT_LT(tileAt({ 50, 50 }).monster(), 0);
					ASSERT_TRUE(target->mode == MonsterMode::Petrified || target->animInfo.isPetrified);
				} else if (targetMoving) {
					target->occupyTile({ 50, 50 }, true);
					ASSERT_LT(tileAt({ 50, 50 }).monster(), 0);
					ASSERT_FALSE(target->mode == MonsterMode::Petrified || target->animInfo.isPetrified);
				}
				AddArrowSampleCell(cells, { 50, 50 });
				(void)actors;
				(void)slots;
			};
			auto launch = [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true); };
			runCase(caseName, "native-checkmissilecol-monster-faction-and-signed-occupancy",
			    HeroClass::Warrior, 1, { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, prepare, fixedSeed, launch, noMutation);
		};
		stationaryMonsterTarget("same-faction-monster-pass", false, false, false, false, false, false);
		stationaryMonsterTarget("opposing-player-minion-target", true, false, false, false, false, false);
		stationaryMonsterTarget("source-berserk-allows-same-faction-hit", false, true, false, false, false, false);
		stationaryMonsterTarget("target-berserk-allows-same-faction-hit", false, false, false, true, false, false);
		stationaryMonsterTarget("petrified-negative-monster-occupancy-hit", true, false, false, false, true, false);
		stationaryMonsterTarget("moving-negative-nonpetrified-monster-ignored", false, false, false, false, false, true);

		auto monsterImpactBoundary = [&](std::string_view caseName, int initialHitPointsQ6) {
			auto prepare = [=](Player &, Monster &source, auto &spawn, auto &, auto &, auto &cells) {
				source.flags |= MFLAG_GOLEM;
				source.golemToHit = 80;
				source.minDamage = source.maxDamage = 1;
				ASSERT_TRUE(source.isPlayerMinion());
				Monster *target = spawn(MT_TSKELBW, { 50, 50 });
				ASSERT_NE(target, nullptr);
				ASSERT_FALSE(target->isPlayerMinion());
				target->armorClass = 0;
				target->hitPoints = target->maxHitPoints = initialHitPointsQ6;
				target->whoHit = 1; // Prepared prior player participation exercises native death credit.
				target->rndItemSeed = findMonsterLootSeed(*target);
				ASSERT_NE(target->rndItemSeed, UINT32_MAX) << "The lethal capture needs a deterministic nonempty native loot selection.";
				ASSERT_EQ(tileAt({ 50, 50 }).monster(), static_cast<int>(target->getId()) + 1);
				AddArrowSampleCell(cells, { 50, 50 });
			};
			auto recordInput = [](int tick, Player &, Monster &, auto &, auto &, auto &slots, auto &, JsonObject &inputs) {
				if (tick != 0) {
					inputs.String("kind", "none");
					return;
				}
				const Monster &target = Monsters[slots.back()];
				inputs.String("kind", "prepared-q6-monster-life-boundary");
				inputs.Number("targetSlot", target.getId());
				inputs.Number("targetHitPointsQ6", target.hitPoints);
				inputs.Number("sourceMinimumDamage", Monsters[slots.front()].minDamage);
				inputs.Number("sourceMaximumDamage", Monsters[slots.front()].maxDamage);
				inputs.Boolean("nativeArrowDamageIsAlreadyQ6", false);
				inputs.Number("expectedDamageQ6", 64);
				inputs.Number("expectedResidualHitPointsQ6", target.hitPoints - 64);
			};
			runCase(caseName, "native-monstertrap-hit-q6-death-boundary-and-loot-pool", HeroClass::Warrior, 2, { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, prepare, seedForMonsterHit, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) { emitArrow(out, { 50, 50 }, { 50, 50 }, Direction::East, true); }, recordInput);
		};
		monsterImpactBoundary("monster-impact-remaining-life-64", 128);
		monsterImpactBoundary("monster-impact-lethal-fractional-63", 127);

		auto wallOnlyPrepare = [](Player &, Monster &, auto &, auto &, auto &, auto &cells) {
			tileAt({ 50, 50 }).setPiece(3);
			tileProperties()[3] = TileProperties::BlockMissile;
			ASSERT_TRUE(IsMissileBlockedByTile({ 50, 50 }));
			AddArrowSampleCell(cells, { 50, 50 });
		};
		auto blockerPrepare = [](Player &hero, Monster &, auto &, auto &, auto &, auto &cells) {
			hero.position.tile = { 50, 50 };
			hero.position.future = hero.position.tile;
			hero.position.old = hero.position.tile;
			hero.occupyTile(hero.position.tile, false);
			tileAt({ 50, 50 }).setPiece(3);
			tileProperties()[3] = TileProperties::BlockMissile;
			ASSERT_TRUE(IsMissileBlockedByTile({ 50, 50 }));
			ASSERT_GT(tileAt({ 50, 50 }).player(), 0);
			AddArrowSampleCell(cells, { 50, 50 });
		};
		runCase("tile-missile-blocker", "native-arrow-tile-blocker", HeroClass::Warrior, 2,
		    { 44, 50 }, { 80, 80 }, { 50, 50 }, 1, wallOnlyPrepare, fixedSeed, emitOneStationary, lowDamage);
		runCase("player-hit-before-same-cell-tile-blocker", "native-monster-player-object-check-order", HeroClass::Warrior, 2,
		    { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, blockerPrepare, hitSeed, emitOneStationary, lowDamage);
		runCase("player-hit-before-same-cell-breakable-crux", "native-monster-player-crux-check-order", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, [&](Player &hero, Monster &, auto &, auto &, auto &, auto &cells) {
				hero.position.tile = { 50, 50 }; hero.position.future = hero.position.tile; hero.position.old = hero.position.tile; hero.occupyTile(hero.position.tile, false);
				ActiveObjectCount = 2; ActiveObjects[0] = 0; ActiveObjects[1] = 1;
				for (int slot = 0; slot < 2; ++slot) {
					Objects[slot] = {}; Objects[slot]._otype = OBJ_CRUX1; Objects[slot]._oBreak = 1; Objects[slot]._oMissFlag = false;
					Objects[slot]._oSolidFlag = true; Objects[slot].selectionRegion = SelectionRegion::Bottom; Objects[slot]._oVar8 = 77;
					Objects[slot].position = slot == 0 ? Point { 50, 50 } : Point { 70, 70 };
					tileAt(Objects[slot].position).setObject(static_cast<int8_t>(slot + 1));
				}
				AddArrowSampleCell(cells, { 50, 50 }); AddArrowSampleCell(cells, { 70, 70 }); }, hitSeed, emitOneStationary, lowDamage);

		auto sourceStatMutation = [](int tick, Player &, Monster &source, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				source.minDamage = 9;
				source.maxDamage = 13;
				inputs.String("kind", "source-stat-change-before-impact");
				inputs.Number("minimumDamageInput", 9);
				inputs.Number("maximumDamageInput", 13);
			} else
				inputs.String("kind", "none");
		};
		runCase("source-live-damage-changed-before-impact", "native-processarrow-reads-current-source-slot-damage",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, sourceStatMutation);
		auto removeSourceMutation = [](int tick, Player &, Monster &source, auto &, auto &, auto &, auto &cells, JsonObject &inputs) {
			if (tick != 0) {
				inputs.String("kind", "none");
				return;
			}
			const size_t slot = source.getId();
			const auto active = std::find(ActiveMonsters, ActiveMonsters + ActiveMonsterCount, static_cast<unsigned>(slot));
			ASSERT_NE(active, ActiveMonsters + ActiveMonsterCount);
			const size_t index = static_cast<size_t>(std::distance(ActiveMonsters, active));
			tileAt(source.position.tile).setMonster(0);
			DeleteMonsterAtActiveIndex(index);
			inputs.String("kind", "remove-source-from-active-pool-retain-slot-state");
			inputs.Number("sourceSlotInput", slot);
			AddArrowSampleCell(cells, source.position.tile);
		};
		runCase("source-removed-before-impact", "native-arrow-reads-retained-source-slot-after-active-removal",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, removeSourceMutation);
		auto sourceDeadMutation = [](int tick, Player &, Monster &source, auto &, auto &, auto &, auto &, JsonObject &inputs) {
			if (tick == 0) {
				source.hitPoints = 0;
				source.mode = MonsterMode::Death;
				inputs.String("kind", "source-dead-but-slot-retained");
				inputs.Number("hitPointsInputQ6", 0);
				inputs.Number("modeInput", source.mode);
			} else
				inputs.String("kind", "none");
		};
		runCase("source-dead-before-impact", "native-arrow-reads-dead-source-slot-state",
		    HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, emitOneStationary, sourceDeadMutation);
		runCase("final-lifetime-tick-collision", "native-processarrow-collides-before-duration-expiry-deletion", HeroClass::Warrior, 2, { 44, 50 }, { 50, 50 }, { 50, 50 }, 1, playerCell, hitSeed, [](std::ostream &out, Player &, Monster &, auto &, auto &, auto &, auto &emitArrow) {
				emitArrow(out, { 50, 50 }, { 51, 50 }, Direction::East, true);
				Missiles.back().duration = 1; }, lowDamage);

		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	ASSERT_TRUE(!stream.fail());
	EXPECT_EQ(caseCount, 48);
	EXPECT_LE(std::filesystem::file_size(root / "native-catacombs-arrow-flight.json"), 32U * 1024U * 1024U);
}

TEST_F(ThemeRoomGenerationTest, CaptureMonsterRuntimeMetadata)
{
	const char *directory = std::getenv("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_ORACLE_DIRECTORY");
	if (directory == nullptr || !*directory) GTEST_SKIP() << "Select a separate private runtime metadata directory.";
	const auto root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root)) << "Never overwrite retained native evidence.";
	ASSERT_EQ(MonstersData.size(), 138U);
	ASSERT_EQ(UniqueMonstersData.size(), 100U);
	std::ofstream table(root / "cathedral_population_monster_runtime.tsv", std::ios::binary);
	ASSERT_TRUE(table.good());
	table << "native_monster_id\tstable_symbol\tname\tlevel\tto_hit\tanim_frame_num\tto_hit_special\tanim_frame_num_special\tmonster_class\tselection_region\ttreasure\texperience\tresistance_hell\r\n";
	for (size_t i = 0; i < MonstersData.size(); ++i) {
		const auto &data = MonstersData[i];
		ASSERT_EQ(data.name.find_first_of("\t\r\n"), std::string::npos);
		table << i << "\tNativeMonster_" << std::setfill('0') << std::setw(3) << i << '\t' << data.name
		      << '\t' << static_cast<int>(data.level) << '\t' << static_cast<int>(data.toHit)
		      << '\t' << static_cast<int>(data.animFrameNum) << '\t' << static_cast<int>(data.toHitSpecial)
		      << '\t' << static_cast<int>(data.animFrameNumSpecial) << '\t' << static_cast<int>(data.monsterClass)
		      << '\t' << static_cast<int>(data.selectionRegion) << '\t' << data.treasure
		      << '\t' << data.exp << '\t' << static_cast<int>(data.resistanceHell) << "\r\n";
	}
	table.flush();
	ASSERT_TRUE(table.good());
	table.close();
	std::ofstream stream(root / "native-monster-runtime.json", std::ios::binary);
	ASSERT_TRUE(stream.good());
	int count = 0;
	{
		JsonObject document(stream);
		document.String("format", "godevilution-cathedral-monster-runtime");
		document.Number("version", 1);
		document.Key("cases");
		stream << '[';
		auto capture = [&](int nativeId, int uniqueId, _difficulty difficulty, bool golem, bool berserk) {
			LevelBestiary.types[0].type = static_cast<_monster_id>(nativeId);
			Monster monster {};
			monster.levelType = 0;
			monster.uniqueType = uniqueId < 0 ? UniqueMonsterType::None : static_cast<UniqueMonsterType>(uniqueId);
			monster.flags = (golem ? MFLAG_GOLEM : 0) | (berserk ? MFLAG_BERSERK : 0);
			monster.golemToHit = UINT16_MAX;
			if (count++ != 0) stream << ',';
			JsonObject row(stream);
			row.Number("nativeId", nativeId);
			row.Number("uniqueId", uniqueId);
			row.Number("difficulty", difficulty);
			row.Boolean("playerMinion", monster.isPlayerMinion());
			row.Number("golemToHit", monster.golemToHit);
			row.Number("uniqueDungeonLevel", uniqueId < 0 ? 0 : UniqueMonstersData[uniqueId].mlevel);
			row.Number("uniqueCustomToHit", uniqueId < 0 ? 0 : UniqueMonstersData[uniqueId].customToHit);
			row.Number("level", monster.level(difficulty));
			row.Number("experience", monster.exp(difficulty));
			row.Number("toHit", monster.toHit(difficulty));
			row.Number("toHitSpecial", monster.toHitSpecial(difficulty));
		};
		for (int i = 0; i < 138; ++i)
			for (auto difficulty : { DIFF_NORMAL, DIFF_NIGHTMARE, DIFF_HELL })
				capture(i, -1, difficulty, false, false);
		for (int i = 0; i < 100; ++i)
			for (auto difficulty : { DIFF_NORMAL, DIFF_NIGHTMARE, DIFF_HELL })
				capture(UniqueMonstersData[i].mtype, i, difficulty, false, false);
		for (auto difficulty : { DIFF_NORMAL, DIFF_NIGHTMARE, DIFF_HELL }) {
			capture(MT_GOLEM, -1, difficulty, true, false);
			capture(MT_GOLEM, -1, difficulty, true, true);
		}
		stream << ']';
		document.Number("caseCount", count);
		document.Key("uniqueBaseTypes");
		WriteArray(stream, UniqueMonstersData, [&](const auto &unique) { stream << static_cast<int>(unique.mtype); });
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(count, 720);
}

} // namespace
} // namespace devilution
