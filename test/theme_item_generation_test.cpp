#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <memory>
#include <sstream>
#include <string>
#include <string_view>
#include <type_traits>
#include <vector>

#include "application/diablo.h"
#include "application/game_mode.hpp"
#include "engine/assets.hpp"
#include "engine/load/load_clx.hpp"
#include "engine/random.hpp"
#include "engine/render/text_render.hpp"
#include "game/items/items.hpp"
#include "game/items/item_pool.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/tile_properties.hpp"
#include "game/objects/objects.hpp"
#include "game/players/players.hpp"
#include "persistence/options.h"
#include "utils/language.h"

namespace devilution {
namespace {

constexpr char OracleDirectoryVariable[] = "GODEVILUTION_THEME_ITEM_ORACLE_DIRECTORY";

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
				affix.Key("power"); WritePower(out, row.power);
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
			JsonObject glyphRow(out);
			glyphRow.Number("row", row);
			glyphRow.Key("widths");
			out << '[';
			for (unsigned i = 0; i < 256; ++i) { if (i != 0) out << ','; out << (*rowFont)[i].width(); }
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
		RULE(magicChanceBase); RULE(magicChancePerLevel); RULE(uniqueChanceNormal); RULE(uniqueChanceUnique);
		RULE(bonusLevelsUnique); RULE(prefixPercent); RULE(suffixPercent); RULE(onlygoodChance);
		RULE(noDropPercent); RULE(goldPercent);
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
	for (unsigned char ch : name) width += (*font)[ch].width() + 2;
	EXPECT_EQ(GetLineWidth(name, GameFont12, 2), width);
	EXPECT_EQ(GetLineWidth("A\xe2\x80\x8b" "B\nignored", GameFont12, 2), (*font)['A'].width() + (*font)['B'].width() + 2);
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
		document.Key("catalog"); WriteCatalog(stream);
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
				if ((item.dwBuff & CF_UIDOFFSET) != 0) ++offsetUniques;
				else if (item._iSeed != seed) ++reverseCompatibleUniques;
			}
			if (setupItem._iMagical == ITEM_QUALITY_UNIQUE && item._iMagical != ITEM_QUALITY_UNIQUE) ++exhaustedUniques;
			if (item._iMiscId == IMISC_BOOK) ++books;
			if (item._itype == ItemType::Gold) ++gold;
			if (item._itype == ItemType::Staff && item._iMagical != ITEM_QUALITY_UNIQUE) {
				if (item._iSpell != SpellID::Null) ++staffSpells;
				else if (onlygood) ++staffPowers;
			}
			if (item._iMagical == ITEM_QUALITY_MAGIC && (item._iPrePower != IPL_INVALID || item._iSufPower != IPL_INVALID)) ++magicalAffixes;
			if (item._iMagical == ITEM_QUALITY_NORMAL && item._iDurability > 0 && item._iDurability != DUR_INDESTRUCTIBLE && item._iDurability != item._iMaxDur) ++ordinaryDurability;
			JsonObject result(stream);
			result.String("name", name);
			result.Number("depth", depth);
			result.Number("nativeItemId", index);
			result.Number("seed", seed);
			result.Boolean("onlygood", onlygood);
			result.Key("inputItem"); WriteItem(stream, pre);
			result.Key("inputUniqueFlags"); WriteFlags(stream, flags);
			result.Key("setupItem"); WriteItem(stream, setupItem);
			result.Number("setupRandomState", setupRandom);
			result.Key("expectedItem"); WriteItem(stream, item);
			result.Number("expectedRandomState", GetLCGEngineState());
			result.Key("expectedUniqueFlags"); WriteFlags(stream, CaptureFlags());
			// SetupItem uses only the external cursor animation scalars here;
			// sprites and private renderer distribution state remain excluded.
			ASSERT_TRUE(GetItemAnimationLengthForTesting(item._iCurs).has_value());
			SetupItem(item);
			result.Key("groundItem"); WriteItem(stream, item);
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
		retained._iCharges = 7; retained._iMaxCharges = 9;
		retained._iPLStr = 17; retained._iVAdd1 = 73;
		retained._iIdentified = true; retained._iStatFlag = true;
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
	for (const Tile &tile : tiles()) state.worldTiles.push_back(tile);
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
	json.Key("activeIds"); WriteArray(out, state.active, [&](uint8_t value) { out << static_cast<int>(value); });
	json.Key("items"); WriteArray(out, state.items, [&](const Item &value) { WriteItem(out, value); });
	json.Key("uniqueFlags"); WriteFlags(out, state.uniqueFlags);
	json.Key("itemRefs"); WriteArray(out, state.worldTiles, [&](const Tile &tile) { out << static_cast<int>(tile.item()); });
	if (!inputs) return;
	json.Key("tiles");
	int index = 0;
	WriteArray(out, state.worldTiles, [&](const Tile &tile) {
		const Point position { index % MAXDUNX, index / MAXDUNX }; ++index;
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
		for (Tile &tile : tiles()) tile = {};
		std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
		tileProperties()[1] = TileProperties::Solid;
		for (int i = 0; i < MAXOBJECTS; ++i) Objects[i] = {};
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
		for (int i = 0; i < MAXITEMS; ++i) ActiveItems[i] = reverse ? MAXITEMS - 1 - i : i;
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
			EXPECT_EQ(tile.piece(), pre.piece()); EXPECT_EQ(tile.flags(), pre.flags());
			EXPECT_EQ(tile.player(), pre.player()); EXPECT_EQ(tile.monster(), pre.monster());
			EXPECT_EQ(tile.object(), pre.object()); EXPECT_EQ(tile.corpse(), pre.corpse());
			EXPECT_EQ(tile.light(), pre.light()); EXPECT_EQ(tile.preLight(), pre.preLight());
			EXPECT_EQ(tile.transVal(), pre.transVal()); EXPECT_EQ(tile.special(), pre.special());
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
	tileAt(p).setPlayer(-1); EXPECT_FALSE(ItemSpaceOk(p)); tileAt(p).setPlayer(0);
	tileAt(p).setMonster(-1); EXPECT_FALSE(ItemSpaceOk(p)); tileAt(p).setMonster(0);
	tileAt(p).setItem(-1); EXPECT_FALSE(ItemSpaceOk(p)); tileAt(p).setItem(0);
	tileAt(p).setObject(-1); EXPECT_TRUE(ItemSpaceOk(p));
	Objects[0]._oSolidFlag = true; EXPECT_FALSE(ItemSpaceOk(p));
	Objects[0]._oSolidFlag = false; tileAt(p).setObject(0);
	tileAt(p + Direction::South).setObject(-1);
	Objects[0].selectionRegion = SelectionRegion::Bottom; EXPECT_FALSE(ItemSpaceOk(p));
	Objects[0].selectionRegion = SelectionRegion::None; EXPECT_TRUE(ItemSpaceOk(p));
	tileAt(p + Direction::South).setObject(0);
	Objects[0].selectionRegion = SelectionRegion::Bottom;
	Objects[1].selectionRegion = SelectionRegion::Bottom;
	tileAt(p + Direction::SouthEast).setObject(1);
	tileAt(p + Direction::SouthWest).setObject(2); EXPECT_FALSE(ItemSpaceOk(p));
	tileAt(p + Direction::SouthWest).setObject(-2); EXPECT_TRUE(ItemSpaceOk(p));
	tileAt(p + Direction::SouthWest).setObject(2);
	tileAt(p + Direction::SouthEast).setObject(-1); EXPECT_TRUE(ItemSpaceOk(p));
	EXPECT_FALSE(ItemSpaceOk({ -1, 0 })); EXPECT_FALSE(ItemSpaceOk({ MAXDUNX, 0 }));
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
	std::ostringstream first; std::ostringstream second;
	WriteItemWorld(first, before, false); WriteItemWorld(second, after, false);
	EXPECT_EQ(first.str(), second.str());
	CheckUntouchedWorld(before);
}

TEST_F(ThemeItemPlacementTest, CaptureIndependentSelectorsAndWorldCreation)
{
	constexpr char variable[] = "GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY";
	const char *directory = std::getenv(variable);
	if (directory == nullptr || *directory == '\0') GTEST_SKIP() << "Select the separate private placement corpus explicitly.";
	const std::filesystem::path root = std::filesystem::absolute(directory);
	ASSERT_TRUE(std::filesystem::is_directory(root)); ASSERT_TRUE(std::filesystem::is_empty(root));
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
		document.Key("catalog"); WriteCatalog(stream);
		document.Key("selectors"); stream << '[';
		constexpr uint32_t seeds[] = { 0, 1, 2588, 743271966, UINT32_C(0x80000000), UINT32_MAX, 1457187811, 988045466 };
		for (int depth = 1; depth <= 4; ++depth) {
			currentLevelNumber() = depth;
			for (uint32_t seed : seeds) {
				for (int lane = 0; lane < 7; ++lane) {
					const bool good = lane == 1;
					const int type = lane <= 1 ? -1 : lane == 2 ? static_cast<int>(ItemType::Gold)
					    : lane == 3 || lane == 5 ? static_cast<int>(ItemType::Misc)
					    : lane == 4 ? static_cast<int>(ItemType::Staff) : static_cast<int>(ItemType::Sword);
					const int misc = lane == 3 ? IMISC_BOOK : lane == 5 ? IMISC_HEAL : -1;
					SetRndSeed(seed);
					const int selected = SelectThemeItemForTesting(good, type, misc);
					if (selectorCount++ != 0) stream << ',';
					JsonObject value(stream);
					value.Number("depth", depth); value.Number("seed", seed);
					value.Boolean("onlygood", good); value.Number("itemType", type); value.Number("miscId", misc);
					value.Number("selectedIndex", selected); value.Number("randomState", GetLCGEngineState());
				}
			}
		}
		stream << ']';
		document.Key("worldCases"); stream << '[';
		for (int depth = 1; depth <= 4; ++depth) {
			currentLevelNumber() = depth;
			uint32_t uniqueSeed = 0;
			for (uint32_t seed = 1; seed <= 4096; ++seed) {
				PrepareWorld(); SetRndSeed(seed);
				CreateTypeItem({ 50, 50 }, true, ItemType::Sword, -1, false, true);
				if (Items[ActiveItems[2]]._iMagical != ITEM_QUALITY_UNIQUE) continue;
				uniqueSeed = seed; break;
			}
			ASSERT_NE(uniqueSeed, 0U) << "Bounded unique-world seed discovery must cover this depth.";
			for (int lane = 0; lane < 10; ++lane) {
				constexpr std::string_view names[] = { "random-open", "random-good-objects", "typed-gold-fallback",
				    "typed-book-signed", "typed-staff-last-slot", "random-full-pool", "typed-gold-no-space",
				    "typed-heal-edge", "typed-sword-unique", "typed-sword-exhausted" };
				PrepareWorld(lane == 4 ? MAXITEMS - 1 : lane == 5 ? MAXITEMS : 2, lane != 4);
				const bool random = lane == 0 || lane == 1 || lane == 5;
				const bool good = lane == 1 || lane >= 8;
				const bool delta = lane != 0 && lane != 4;
				const int type = random ? -1 : lane == 2 || lane == 6 ? static_cast<int>(ItemType::Gold)
				    : lane == 3 || lane == 7 ? static_cast<int>(ItemType::Misc)
				    : lane == 4 ? static_cast<int>(ItemType::Staff) : static_cast<int>(ItemType::Sword);
				const int misc = lane == 3 ? IMISC_BOOK : lane == 7 ? IMISC_HEAL : -1;
				const uint32_t seed = lane >= 8 ? uniqueSeed : lane == 3 ? 988045466 : lane == 1 ? 743271966 : lane == 2 || lane == 7 ? 1 : 2588;
				const Point p = lane == 7 ? Point { 0, 0 } : Point { 50, 50 };
				if (lane == 0) MyPlayer->pLvlLoad = 0;
				if (lane == 1) {
					for (int y = 49; y <= 51; ++y) for (int x = 49; x <= 51; ++x) tileAt({ x, y }).setPiece(1);
					tileAt({ 49, 49 }).setPiece(0); tileAt({ 49, 49 }).setObject(-1);
					tileAt({ 49, 49 }).addFlags(DungeonFlag::Populated);
					Objects[1]._oSolidFlag = true; tileAt({ 50, 49 }).setObject(-2);
					Objects[2].selectionRegion = SelectionRegion::Bottom; tileAt({ 51, 51 }).setObject(-3);
					tileAt({ 49, 50 }).setPlayer(-1); tileAt({ 49, 51 }).setMonster(-2);
				}
				if (lane == 2) for (int y = 49; y <= 51; ++y) for (int x = 49; x <= 51; ++x) tileAt({ x, y }).setPiece(1);
				if (lane == 3) { tileAt({ 49, 49 }).setPiece(1); tileAt({ 49, 49 }).setItem(-3); }
				if (lane == 6) for (Tile &tile : tiles()) tile.setPiece(1);
				if (lane == 9) std::fill(std::begin(UniqueItemFlags), std::end(UniqueItemFlags), true);
				SetRndSeed(seed);
				const auto before = CaptureItemWorld();
				const int selected = SelectThemeItemForTesting(good, type, misc);
				const uint32_t selectionRandom = GetLCGEngineState();
				SetRndSeed(seed);
				if (worldCount++ != 0) stream << ',';
				JsonObject value(stream);
				value.String("name", names[lane]); value.Number("depth", depth); value.Number("seed", seed);
				value.Boolean("onlygood", good); value.Boolean("delta", delta); value.Boolean("levelLoading", MyPlayer->pLvlLoad != 0);
				value.Number("itemType", type); value.Number("miscId", misc); value.Number("x", p.x); value.Number("y", p.y);
				value.Number("selectedIndex", selected); value.Number("selectionRandomState", selectionRandom);
				value.Key("input"); WriteItemWorld(stream, before, true);
				if (random) CreateRndItem(p, good, false, delta);
				else CreateTypeItem(p, good, static_cast<ItemType>(type), misc, false, delta);
				const auto after = CaptureItemWorld();
				value.Key("expected"); WriteItemWorld(stream, after, false);
				CheckUntouchedWorld(before);
				if (lane == 5) { ++fullPools; EXPECT_EQ(after.randomState, selectionRandom); EXPECT_EQ(after.activeCount, MAXITEMS); }
				else {
					EXPECT_EQ(after.activeCount, before.activeCount + 1);
					const int slot = before.active[before.activeCount];
					const Item &item = after.items[slot];
					if (lane == 1) { EXPECT_EQ(item.position.x, 49); EXPECT_EQ(item.position.y, 49); }
					if (lane == 2) { EXPECT_EQ(item.position.x, 48); EXPECT_EQ(item.position.y, 48); }
					if (lane == 3) { ++signedOverwrites; EXPECT_EQ(item.position.x, 49); EXPECT_EQ(item.position.y, 49); EXPECT_EQ(tileAt({ 49, 49 }).item(), slot + 1); }
					if (lane == 4) EXPECT_EQ(tileAt(item.position).item(), 127);
					if (lane == 6) { ++exhaustedPlacements; EXPECT_EQ(item.position.x, 0); EXPECT_EQ(item.position.y, 0); EXPECT_EQ(tileAt(item.position).item(), 0); }
					if (lane == 8) { ++generatedUniques; EXPECT_EQ(item._iMagical, ITEM_QUALITY_UNIQUE); }
					if (lane == 9) { ++exhaustedHistories; EXPECT_NE(item._iMagical, ITEM_QUALITY_UNIQUE); }
				}
			}
		}
		stream << ']';
		document.Number("selectorCount", selectorCount); document.Number("worldCount", worldCount);
		document.Number("fullPools", fullPools); document.Number("exhaustedPlacements", exhaustedPlacements);
		document.Number("signedOverwrites", signedOverwrites); document.Number("generatedUniques", generatedUniques);
		document.Number("exhaustedHistories", exhaustedHistories);
	}
	stream.flush(); ASSERT_TRUE(stream.good()); stream.close();
	EXPECT_EQ(selectorCount, 224); EXPECT_EQ(worldCount, 40);
	EXPECT_EQ(fullPools, 4); EXPECT_EQ(exhaustedPlacements, 4); EXPECT_EQ(signedOverwrites, 4);
	EXPECT_EQ(generatedUniques, 4); EXPECT_EQ(exhaustedHistories, 4);
	EXPECT_LE(std::filesystem::file_size(root / "native-theme-item-placement.json"), 32U * 1024U * 1024U);
}

} // namespace
} // namespace devilution
