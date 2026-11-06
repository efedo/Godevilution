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
#include "utils/endian/endian_write.hpp"
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
#include "game/spells/spells.hpp"
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
#include "ui/automap.h"
#include "ui/diablo_msg.hpp"
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
		for (int depth = 1; depth <= 4; ++depth) {
			for (int type = OBJ_CHEST1; type <= OBJ_CHEST3; ++type) {
				for (int branch = 0; branch < 2; ++branch) {
					for (int lane = depth == 1 ? 0 : 1; lane < (depth == 1 ? 6 : 2); ++lane) {
						PrepareWorld(lane == 2 ? MAXITEMS : lane == 3 ? MAXITEMS - 1
						                                              : 2,
						    lane != 3);
						currentLevel().setId(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATHEDRAL });
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
						row.Number("depth", depth);
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
		}
		stream << ']';
		document.Number("caseCount", caseCount);
	}
	stream.flush();
	ASSERT_TRUE(stream.good());
	stream.close();
	EXPECT_EQ(caseCount, 54);
	EXPECT_LE(std::filesystem::file_size(root / "native-chest-operations.json"), 43U * 1024U * 1024U);
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
	void CapturePopulationOracle(const char *directory, bool freshLoadOrder, bool levelReady = false, bool nativeRogueRoute = false, std::optional<HeroClass> nativeClassRoute = std::nullopt, bool multiplayer = false);
	void CaptureNativeCatacombsPopulationOracle(const char *archivePath, const char *directory, bool captureStages = false, bool multiplayer = false);
	void CaptureCatacombsThemePass(const char *directory, bool multiplayer);
	void CaptureCatacombsChestOperations(const char *directory, bool multiplayer);

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

	Monster *PrepareCatacombsActor(int depth, _monster_id nativeType, const UniqueMonsterData *unique, int actorX = 50);

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
		WriteArray(out, gbIsMultiplayer ? MultiplayerShrineAvailabilityForTesting() : SinglePlayerShrineAvailabilityForTesting(), [&](bool available) { out << (available ? "true" : "false"); });
	}

private:
	std::unique_ptr<NativeThemeRoomGuard> roomGuard;
};

void WriteChestMissiles(std::ostream &out, WorldTilePosition operationTarget);

#include "theme_item_generation_objects_test.inc"
#include "theme_item_generation_population_test.inc"
#include "theme_item_generation_missiles_test.inc"
#include "theme_item_generation_actors_test.inc"
#include "theme_item_generation_traps_test.inc"
#include "theme_item_generation_shrines_test.inc"
#include "theme_item_generation_theme_effects_test.inc"

} // namespace
} // namespace devilution
