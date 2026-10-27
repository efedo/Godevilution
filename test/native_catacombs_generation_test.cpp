#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <memory>
#include <optional>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

#include <mpqfs/mpqfs.h>

#include "application/diablo.h"
#include "application/game_mode.hpp"
#include "engine/assets.hpp"
#include "engine/load/load_file.hpp"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level_l2.h"
#include "game/levels/triggers.h"
#include "game/players/players.hpp"
#include "game/quests/quests.hpp"
#include "network/protocol/multi.h"
#include "utils/paths.h"

namespace devilution {
namespace {

constexpr size_t CoarseCellCount = DMAXX * DMAXY;
constexpr size_t FineCellCount = MAXDUNX * MAXDUNY;
constexpr uint32_t SentinelViewX = 101;
constexpr uint32_t SentinelViewY = 103;

struct ThemeRecord {
	int x;
	int y;
	int width;
	int height;
	int transparency;
};

struct TriggerRecord {
	int x;
	int y;
	int message;
	int level;
};

struct GenerationSnapshot {
	uint32_t attemptSeed = 0;
	uint32_t finalRandomState = 0;
	Point view;
	Point minimumBounds;
	Point maximumBounds;
	std::array<int, 4> setPieceRoom {};
	std::array<int, 4> setPiece {};
	int nextTransparency = 0;
	std::vector<uint8_t> megaTiles;
	std::vector<uint8_t> replacementMegaTiles;
	std::vector<uint8_t> protectedTiles;
	std::vector<uint16_t> finePieces;
	std::vector<uint8_t> fineSpecials;
	std::vector<uint8_t> fineTransparency;
	std::vector<uint8_t> fineTileFlags;
	std::vector<uint8_t> effectiveSol;
	std::vector<ThemeRecord> themes;
	std::vector<TriggerRecord> triggers;
	bool triggerFlag = false;
};

struct CaseSpec {
	int levelId;
	lvl_entry entry;
	uint32_t inputSeed;
	std::optional<uint32_t> initialLevelSeed;
	std::string caseId;
};

std::string EntryName(lvl_entry entry)
{
	switch (entry) {
	case ENTRY_MAIN: return "main";
	case ENTRY_PREV: return "previous";
	case ENTRY_SETLVL: return "set-level";
	case ENTRY_RTNLVL: return "return-level";
	case ENTRY_LOAD: return "load";
	case ENTRY_WARPLVL: return "warp-level";
	case ENTRY_TWARPDN: return "town-warp-down";
	case ENTRY_TWARPUP: return "town-warp-up";
	}
	return "unknown";
}

std::string SeedName(uint32_t seed)
{
	std::ostringstream output;
	output << std::hex << std::nouppercase << std::setfill('0') << std::setw(8) << seed;
	return output.str();
}

void WriteBytes(const std::filesystem::path &path, const std::vector<uint8_t> &bytes)
{
	std::filesystem::create_directories(path.parent_path());
	std::ofstream output(path, std::ios::binary);
	output.write(reinterpret_cast<const char *>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
	output.flush();
	output.close();
	ASSERT_TRUE(output.good()) << path;
}

void Extract(mpqfs_archive_t *archive, const std::filesystem::path &root, const std::string &name)
{
	ASSERT_TRUE(mpqfs_has_file(archive, name.c_str())) << name;
	const size_t size = mpqfs_file_size(archive, name.c_str());
	ASSERT_GT(size, 0U) << name;
	ASSERT_LE(size, 16U * 1024U * 1024U) << name;
	std::vector<uint8_t> bytes(size);
	ASSERT_EQ(mpqfs_read_file_into(archive, name.c_str(), bytes.data(), size), size) << name;
	WriteBytes(root / "source" / name, bytes);
}

/**
 * Keep the opt-in oracle isolated from the test process's active level, player,
 * quest state, assets, trigger state, and random generator.
 */
class NativeCatacombsStateGuard {
public:
	NativeCatacombsStateGuard()
	    : savedLevel_(std::make_unique<Level>(currentLevel().id()))
	    , savedPlayers_(std::move(Players))
	    , savedMyPlayer_(MyPlayer)
	    , savedMyPlayerId_(MyPlayerId)
	    , savedQuests_ {}
	    , savedLevelSeeds_ {}
	    , savedGameInitInfo_(sgGameInitInfo)
	    , savedSpawn_(gbIsSpawn)
	    , savedMultiplayer_(gbIsMultiplayer)
	    , savedHellfire_(gbIsHellfire)
	    , savedRandomState_(GetLCGEngineState())
	    , savedAssetsPath_(paths::AssetsPath())
	    , savedOverridePaths_(OverridePaths)
	    , savedArchives_(std::move(MpqArchives))
	    , savedTriggers_ {}
	{
		std::copy(std::begin(Quests), std::end(Quests), savedQuests_.begin());
		std::copy(std::begin(LevelSeeds), std::end(LevelSeeds), savedLevelSeeds_.begin());
		std::copy(std::begin(trigs), std::end(trigs), savedTriggers_.begin());
		MpqArchives.clear();
		Players.clear();
		Players.emplace_back();
		MyPlayer = &Players.front();
		MyPlayerId = 0;
		MyPlayer->pOriginalCathedral = true;
		for (Quest &quest : Quests)
			quest._qactive = QUEST_NOTAVAIL;
		for (auto &levelSeed : LevelSeeds)
			levelSeed = std::nullopt;
		gbIsSpawn = false;
		gbIsMultiplayer = false;
		gbIsHellfire = false;
		sgGameInitInfo.fullQuests = 1;
		sgGameInitInfo.nDifficulty = DIFF_NORMAL;
		SwapLevelChannels();
	}

	~NativeCatacombsStateGuard()
	{
		SwapLevelChannels();
		currentLevel().setId(savedLevel_->id());
		std::copy(savedQuests_.begin(), savedQuests_.end(), std::begin(Quests));
		std::copy(savedLevelSeeds_.begin(), savedLevelSeeds_.end(), std::begin(LevelSeeds));
		std::copy(savedTriggers_.begin(), savedTriggers_.end(), std::begin(trigs));
		MyPlayer = savedMyPlayer_;
		MyPlayerId = savedMyPlayerId_;
		Players = std::move(savedPlayers_);
		sgGameInitInfo = savedGameInitInfo_;
		gbIsSpawn = savedSpawn_;
		gbIsMultiplayer = savedMultiplayer_;
		gbIsHellfire = savedHellfire_;
		SetRndSeed(savedRandomState_);
		paths::SetAssetsPath(savedAssetsPath_);
		OverridePaths = std::move(savedOverridePaths_);
		MpqArchives = std::move(savedArchives_);
	}

	void UseExtractedSource(const std::filesystem::path &sourceRoot)
	{
		const std::string root = sourceRoot.string() + "/";
		paths::SetAssetsPath(root);
		OverridePaths = { root };
	}

private:
	void SwapLevelChannels()
	{
		auto &active = currentLevel();
		using std::swap;
		swap(active.DungeonMask_, savedLevel_->DungeonMask_);
		swap(active.megaTiles_, savedLevel_->megaTiles_);
		swap(active.Protected_, savedLevel_->Protected_);
		swap(active.SetPieceRoom_, savedLevel_->SetPieceRoom_);
		swap(active.SetPiece_, savedLevel_->SetPiece_);
		swap(active.pSpecialCels_, savedLevel_->pSpecialCels_);
		swap(active.pMegaTiles_, savedLevel_->pMegaTiles_);
		swap(active.pDungeonCels_, savedLevel_->pDungeonCels_);
		swap(active.SOLData_, savedLevel_->SOLData_);
		swap(active.dminPosition_, savedLevel_->dminPosition_);
		swap(active.dmaxPosition_, savedLevel_->dmaxPosition_);
		swap(active.ViewPosition_, savedLevel_->ViewPosition_);
		swap(active.MicroTileLen_, savedLevel_->MicroTileLen_);
		swap(active.TransVal_, savedLevel_->TransVal_);
		swap(active.TransList_, savedLevel_->TransList_);
		swap(active.DPieceMicros_, savedLevel_->DPieceMicros_);
		swap(active.tiles_, savedLevel_->tiles_);
		swap(active.themeCount_, savedLevel_->themeCount_);
		swap(active.themeLoc_, savedLevel_->themeLoc_);
		swap(active.numThemes_, savedLevel_->numThemes_);
		swap(active.armorFlag_, savedLevel_->armorFlag_);
		swap(active.weaponFlag_, savedLevel_->weaponFlag_);
		swap(active.zharlib_, savedLevel_->zharlib_);
		swap(active.trigFlag_, savedLevel_->trigFlag_);
		swap(active.numTriggers_, savedLevel_->numTriggers_);
		swap(active.tWarpFrom_, savedLevel_->tWarpFrom_);
	}

	std::unique_ptr<Level> savedLevel_;
	std::vector<Player> savedPlayers_;
	Player *savedMyPlayer_;
	int savedMyPlayerId_;
	std::array<Quest, MAXQUESTS> savedQuests_;
	std::array<std::optional<uint32_t>, NUMLEVELS> savedLevelSeeds_;
	decltype(sgGameInitInfo) savedGameInitInfo_;
	bool savedSpawn_;
	bool savedMultiplayer_;
	bool savedHellfire_;
	uint32_t savedRandomState_;
	std::string savedAssetsPath_;
	std::vector<std::string> savedOverridePaths_;
	decltype(MpqArchives) savedArchives_;
	std::array<TriggerStruct, MAXTRIGGERS> savedTriggers_;
};

void ResetGenerationState(Point initialView)
{
	for (int y = 0; y < MAXDUNY; ++y) {
		for (int x = 0; x < MAXDUNX; ++x)
			tileAt(x, y).clear();
	}
	for (int y = 0; y < DMAXY; ++y) {
		for (int x = 0; x < DMAXX; ++x) {
			megaTileAt(x, y).setCurrent(0);
			megaTileAt(x, y).setReplacement(0);
		}
	}
	dungeonMask().reset();
	protectedTiles().reset();
	setPieceRoom() = {};
	setPiece() = {};
	viewPosition() = initialView;
	minimumDungeonPosition() = {};
	maximumDungeonPosition() = {};
	nextTransparencyValue() = 0;
	visibleTransparencyRegions() = {};
	themeCount() = 0;
	std::fill(std::begin(themeLocations()), std::end(themeLocations()), THEME_LOC {});
	numThemes() = 0;
	armorFlag() = false;
	weaponFlag() = false;
	zharlib() = 0;
	trigFlag() = false;
	numTriggers() = 0;
	tWarpFrom() = 0;
}

void CaptureGeometry(GenerationSnapshot &snapshot)
{
	snapshot.view = viewPosition();
	snapshot.minimumBounds = { minimumDungeonPosition().x, minimumDungeonPosition().y };
	snapshot.maximumBounds = { maximumDungeonPosition().x, maximumDungeonPosition().y };
	snapshot.setPieceRoom = { setPieceRoom().position.x, setPieceRoom().position.y,
		setPieceRoom().size.width, setPieceRoom().size.height };
	snapshot.setPiece = { setPiece().position.x, setPiece().position.y,
		setPiece().size.width, setPiece().size.height };
	snapshot.nextTransparency = nextTransparencyValue();
	snapshot.megaTiles.reserve(CoarseCellCount);
	snapshot.replacementMegaTiles.reserve(CoarseCellCount);
	snapshot.protectedTiles.reserve(CoarseCellCount);
	for (int y = 0; y < DMAXY; ++y) {
		for (int x = 0; x < DMAXX; ++x) {
			snapshot.megaTiles.push_back(megaTileAt(x, y).current());
			snapshot.replacementMegaTiles.push_back(megaTileAt(x, y).replacement());
			snapshot.protectedTiles.push_back(protectedTiles().test(x, y) ? 1 : 0);
		}
	}
	snapshot.finePieces.reserve(FineCellCount);
	snapshot.fineSpecials.reserve(FineCellCount);
	snapshot.fineTransparency.reserve(FineCellCount);
	snapshot.fineTileFlags.reserve(FineCellCount);
	for (int y = 0; y < MAXDUNY; ++y) {
		for (int x = 0; x < MAXDUNX; ++x) {
			const Tile &tile = tileAt(x, y);
			snapshot.finePieces.push_back(tile.piece());
			snapshot.fineSpecials.push_back(static_cast<uint8_t>(tile.special()));
			snapshot.fineTransparency.push_back(static_cast<uint8_t>(tile.transVal()));
			snapshot.fineTileFlags.push_back(static_cast<uint8_t>(tile.flags()));
		}
	}
	snapshot.effectiveSol.reserve(MAXTILES);
	for (int piece = 0; piece < MAXTILES; ++piece)
		snapshot.effectiveSol.push_back(static_cast<uint8_t>(tileProperties()[piece]));
	snapshot.themes.reserve(themeCount());
	for (int index = 0; index < themeCount(); ++index) {
		const auto &room = themeLocations()[index].room;
		snapshot.themes.push_back({ room.position.x, room.position.y,
		    room.size.width, room.size.height, themeLocations()[index].ttval });
	}
}

void CaptureTriggers(GenerationSnapshot &snapshot)
{
	std::fill(std::begin(trigs), std::end(trigs), TriggerStruct {});
	InitL2Triggers();
	snapshot.triggerFlag = trigFlag();
	ASSERT_GE(numTriggers(), 0);
	ASSERT_LE(numTriggers(), MAXTRIGGERS);
	snapshot.triggers.reserve(numTriggers());
	for (int index = 0; index < numTriggers(); ++index) {
		const TriggerStruct &trigger = trigs[index];
		snapshot.triggers.push_back({ trigger.position.x, trigger.position.y,
		    static_cast<int>(trigger._tmsg), trigger._tlvl });
	}
}

void WriteIntegerArray(std::ostream &output, const std::vector<uint8_t> &values)
{
	output << '[';
	for (size_t index = 0; index < values.size(); ++index) {
		if (index != 0)
			output << ',';
		output << static_cast<unsigned int>(values[index]);
	}
	output << ']';
}

void WriteIntegerArray(std::ostream &output, const std::vector<uint16_t> &values)
{
	output << '[';
	for (size_t index = 0; index < values.size(); ++index) {
		if (index != 0)
			output << ',';
		output << values[index];
	}
	output << ']';
}

void WritePoint(std::ostream &output, Point point)
{
	output << '[' << point.x << ',' << point.y << ']';
}

void WriteRect(std::ostream &output, const std::array<int, 4> &rectangle)
{
	output << '[' << rectangle[0] << ',' << rectangle[1] << ',' << rectangle[2] << ',' << rectangle[3] << ']';
}

void WriteJson(const std::filesystem::path &path, int levelId, const CaseSpec &spec, const GenerationSnapshot &snapshot, size_t sourceSolByteCount)
{
	std::ofstream output(path, std::ios::binary);
	ASSERT_TRUE(output.good()) << path;
	output << "{\"schemaVersion\":1"
	       << ",\"policy\":\"quest-disabled-original-diablo-single-player-normal-catacombs\""
	       << ",\"sourceSolByteCount\":" << sourceSolByteCount
	       << ",\"questPolicy\":\"all-NOTAVAIL\""
	       << ",\"captureBoundary\":\"post-CreateDungeon-pre-CreateThemeRooms\""
	       << ",\"caseId\":\"" << spec.caseId << "\""
	       << ",\"levelId\":" << levelId
	       << ",\"entry\":\"" << EntryName(spec.entry) << "\""
	       << ",\"inputSeed\":" << spec.inputSeed
	       << ",\"initialLevelSeed\":";
	if (spec.initialLevelSeed.has_value())
		output << *spec.initialLevelSeed;
	else
		output << "null";
	output << ",\"attemptSeed\":" << snapshot.attemptSeed
	       << ",\"finalRandomState\":" << snapshot.finalRandomState
	       << ",\"initialView\":[" << SentinelViewX << ',' << SentinelViewY << ']'
	       << ",\"view\":";
	WritePoint(output, snapshot.view);
	output << ",\"minimumBounds\":";
	WritePoint(output, snapshot.minimumBounds);
	output << ",\"maximumBounds\":";
	WritePoint(output, snapshot.maximumBounds);
	output << ",\"setPieceRoom\":";
	WriteRect(output, snapshot.setPieceRoom);
	output << ",\"setPiece\":";
	WriteRect(output, snapshot.setPiece);
	output << ",\"nextTransparency\":" << snapshot.nextTransparency
	       << ",\"megaTiles\":";
	WriteIntegerArray(output, snapshot.megaTiles);
	output << ",\"replacementMegaTiles\":";
	WriteIntegerArray(output, snapshot.replacementMegaTiles);
	output << ",\"protectedTiles\":";
	WriteIntegerArray(output, snapshot.protectedTiles);
	output << ",\"finePieces\":";
	WriteIntegerArray(output, snapshot.finePieces);
	output << ",\"fineSpecials\":";
	WriteIntegerArray(output, snapshot.fineSpecials);
	output << ",\"fineTransparency\":";
	WriteIntegerArray(output, snapshot.fineTransparency);
	output << ",\"fineTileFlags\":";
	WriteIntegerArray(output, snapshot.fineTileFlags);
	output << ",\"effectiveSol\":";
	WriteIntegerArray(output, snapshot.effectiveSol);
	output << ",\"themes\":[";
	for (size_t index = 0; index < snapshot.themes.size(); ++index) {
		if (index != 0)
			output << ',';
		const ThemeRecord &theme = snapshot.themes[index];
		output << "{\"x\":" << theme.x << ",\"y\":" << theme.y
		       << ",\"width\":" << theme.width << ",\"height\":" << theme.height
		       << ",\"transparency\":" << theme.transparency << '}';
	}
	output << "],\"triggers\":[";
	for (size_t index = 0; index < snapshot.triggers.size(); ++index) {
		if (index != 0)
			output << ',';
		const TriggerRecord &trigger = snapshot.triggers[index];
		output << "{\"x\":" << trigger.x << ",\"y\":" << trigger.y
		       << ",\"message\":" << trigger.message << ",\"level\":" << trigger.level << '}';
	}
	output << "],\"triggerFlag\":" << (snapshot.triggerFlag ? "true" : "false") << "}\r\n";
	output.flush();
	output.close();
	ASSERT_TRUE(output.good()) << path;
}

void ExpectSameGenerationExceptView(const GenerationSnapshot &first, const GenerationSnapshot &second)
{
	EXPECT_EQ(first.attemptSeed, second.attemptSeed);
	EXPECT_EQ(first.finalRandomState, second.finalRandomState);
	EXPECT_EQ(first.minimumBounds, second.minimumBounds);
	EXPECT_EQ(first.maximumBounds, second.maximumBounds);
	EXPECT_EQ(first.setPieceRoom, second.setPieceRoom);
	EXPECT_EQ(first.setPiece, second.setPiece);
	EXPECT_EQ(first.nextTransparency, second.nextTransparency);
	EXPECT_EQ(first.megaTiles, second.megaTiles);
	EXPECT_EQ(first.replacementMegaTiles, second.replacementMegaTiles);
	EXPECT_EQ(first.protectedTiles, second.protectedTiles);
	EXPECT_EQ(first.finePieces, second.finePieces);
	EXPECT_EQ(first.fineSpecials, second.fineSpecials);
	EXPECT_EQ(first.fineTransparency, second.fineTransparency);
	EXPECT_EQ(first.fineTileFlags, second.fineTileFlags);
	EXPECT_EQ(first.effectiveSol, second.effectiveSol);
	ASSERT_EQ(first.themes.size(), second.themes.size());
	for (size_t index = 0; index < first.themes.size(); ++index) {
		EXPECT_EQ(first.themes[index].x, second.themes[index].x);
		EXPECT_EQ(first.themes[index].y, second.themes[index].y);
		EXPECT_EQ(first.themes[index].width, second.themes[index].width);
		EXPECT_EQ(first.themes[index].height, second.themes[index].height);
		EXPECT_EQ(first.themes[index].transparency, second.themes[index].transparency);
	}
	ASSERT_EQ(first.triggers.size(), second.triggers.size());
	for (size_t index = 0; index < first.triggers.size(); ++index) {
		EXPECT_EQ(first.triggers[index].x, second.triggers[index].x);
		EXPECT_EQ(first.triggers[index].y, second.triggers[index].y);
		EXPECT_EQ(first.triggers[index].message, second.triggers[index].message);
		EXPECT_EQ(first.triggers[index].level, second.triggers[index].level);
	}
	EXPECT_EQ(first.triggerFlag, second.triggerFlag);
}

void CaptureCase(const CaseSpec &spec, GenerationSnapshot &snapshot)
{
	const LevelId id { .levelNum = static_cast<uint8_t>(spec.levelId), .type = DTYPE_CATACOMBS };
	currentLevel().setId(id);
	levelType() = DTYPE_CATACOMBS;
	currentLevelNumber() = spec.levelId;
	ResetGenerationState({ static_cast<int>(SentinelViewX), static_cast<int>(SentinelViewY) });
	for (Quest &quest : Quests)
		quest._qactive = QUEST_NOTAVAIL;
	LevelSeeds[spec.levelId] = spec.initialLevelSeed;
	SetRndSeed(spec.inputSeed);

	CreateDungeon(spec.inputSeed, spec.entry);

	ASSERT_TRUE(LevelSeeds[spec.levelId].has_value());
	snapshot.attemptSeed = *LevelSeeds[spec.levelId];
	snapshot.finalRandomState = GetLCGEngineState();
	CaptureGeometry(snapshot);
	ASSERT_EQ(snapshot.megaTiles.size(), CoarseCellCount);
	ASSERT_EQ(snapshot.replacementMegaTiles.size(), CoarseCellCount);
	ASSERT_EQ(snapshot.protectedTiles.size(), CoarseCellCount);
	ASSERT_EQ(snapshot.finePieces.size(), FineCellCount);
	ASSERT_EQ(snapshot.fineSpecials.size(), FineCellCount);
	ASSERT_EQ(snapshot.fineTransparency.size(), FineCellCount);
	ASSERT_EQ(snapshot.fineTileFlags.size(), FineCellCount);
	ASSERT_EQ(snapshot.effectiveSol.size(), MAXTILES);
	ASSERT_LE(snapshot.themes.size(), MAXTHEMES);
	ASSERT_EQ(snapshot.minimumBounds, (Point { 16, 16 }));
	ASSERT_EQ(snapshot.maximumBounds, (Point { 96, 96 }));
	for (const uint16_t piece : snapshot.finePieces)
		ASSERT_LT(piece, MAXTILES);
	for (const uint8_t special : snapshot.fineSpecials)
		ASSERT_LE(special, 6);

	// Trigger discovery is a derived scan and must not perturb generation RNG.
	CaptureTriggers(snapshot);
	EXPECT_EQ(GetLCGEngineState(), snapshot.finalRandomState);
}

std::vector<CaseSpec> BuildCases()
{
	const std::array<uint32_t, 5> sharedSeeds { 0U, 1U, 2588U, 0x80000000U, 0xFFFFFFFFU };
	const std::array<uint32_t, 4> fixtureSeeds { 1677631846U, 2034738122U, 680552750U, 1999936419U };
	std::vector<CaseSpec> cases;
	auto add = [&](int level, lvl_entry entry, uint32_t inputSeed, std::optional<uint32_t> retainedSeed, std::string suffix) {
		const std::string id = "l" + std::to_string(level) + "-" + EntryName(entry) + "-" + suffix;
		cases.push_back({ level, entry, inputSeed, retainedSeed, id });
	};
	for (int level = 5; level <= 8; ++level) {
		std::vector<uint32_t> seeds(sharedSeeds.begin(), sharedSeeds.end());
		seeds.push_back(fixtureSeeds[level - 5]);
		for (const uint32_t seed : seeds) {
			for (const lvl_entry entry : { ENTRY_MAIN, ENTRY_PREV })
				add(level, entry, seed, std::nullopt, "seed-" + SeedName(seed));
			if (level == 5)
				add(level, ENTRY_TWARPDN, seed, std::nullopt, "seed-" + SeedName(seed));
		}
	}
	for (int level = 5; level <= 8; ++level) {
		const uint32_t supplied = 0x13579BDFU + static_cast<uint32_t>(level);
		const uint32_t retained = 0x2468ACE0U + static_cast<uint32_t>(level);
		add(level, ENTRY_MAIN, supplied, 0U, "retained-00000000-supplied-" + SeedName(supplied));
		add(level, ENTRY_MAIN, retained, std::nullopt, "seed-" + SeedName(retained));
		add(level, ENTRY_MAIN, supplied, retained, "retained-" + SeedName(retained) + "-supplied-" + SeedName(supplied));
	}
	for (const int level : { 5, 6 }) {
		for (const lvl_entry entry : { ENTRY_SETLVL, ENTRY_RTNLVL, ENTRY_WARPLVL, ENTRY_TWARPUP })
			add(level, entry, 2588U, std::nullopt, "sentinel-seed-00000a1c");
	}
	add(6, ENTRY_TWARPDN, 2588U, std::nullopt, "sentinel-seed-00000a1c-no-town-warp");
	return cases;
}

TEST(NativeCatacombsGenerationTest, ScopedGuardRestoresPriorLevelAndGlobalState)
{
	// The outer guard keeps this MPQ-free restoration probe independent from
	// the remaining native suite. Only the inner guard is under assertion.
	NativeCatacombsStateGuard outer;
	const LevelId priorId { .levelNum = 7, .type = DTYPE_CATACOMBS };
	currentLevel().setId(priorId);
	Players.emplace_back();
	MyPlayer = &Players.back();
	MyPlayerId = 1;
	Player *const priorPlayer = MyPlayer;
	LevelSeeds[5] = 0U;
	LevelSeeds[6] = 0x12345678U;
	Quests[Q_BLIND]._qactive = QUEST_DONE;
	gbIsSpawn = true;
	gbIsMultiplayer = true;
	gbIsHellfire = true;
	sgGameInitInfo.fullQuests = 0;
	sgGameInitInfo.nDifficulty = DIFF_HELL;
	SetRndSeed(0xFEDCBA98U);
	paths::SetAssetsPath("catacombs-guard-assets/");
	const std::string priorAssetsPath = paths::AssetsPath();
	OverridePaths = { "catacombs-guard-override/" };
	viewPosition() = { 29, 31 };
	minimumDungeonPosition() = { 17, 19 };
	maximumDungeonPosition() = { 91, 93 };
	megaTileAt(2, 3).setCurrent(158);
	megaTileAt(2, 3).setReplacement(159);
	protectedTiles().set(2, 3);
	dungeonMask().set(4, 5);
	tileAt(11, 12).setPiece(177);
	tileAt(11, 12).setSpecial(5);
	tileAt(11, 12).setTransVal(-7);
	tileAt(11, 12).addFlags(DungeonFlag::Populated);
	themeCount() = 1;
	themeLocations()[0] = { { { 4, 5 }, { 6, 7 } }, 8 };
	nextTransparencyValue() = -7;
	trigFlag() = true;
	numTriggers() = 1;
	trigs[0] = {};
	trigs[0].position = { 43, 47 };
	trigs[0]._tmsg = WM_DIABTWARPUP;
	trigs[0]._tlvl = 5;
	GenerationSnapshot before;
	CaptureGeometry(before);
	{
		NativeCatacombsStateGuard inner;
		currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
		ResetGenerationState({ 101, 103 });
		LevelSeeds[5] = std::nullopt;
		LevelSeeds[6] = 9U;
		SetRndSeed(1);
		paths::SetAssetsPath("changed-catacombs-assets/");
		OverridePaths = { "changed-catacombs-override/" };
		trigs[0] = {};
	}
	GenerationSnapshot after;
	CaptureGeometry(after);
	ExpectSameGenerationExceptView(before, after);
	EXPECT_EQ(before.view, after.view);
	EXPECT_EQ(currentLevel().id(), priorId);
	EXPECT_EQ(MyPlayer, priorPlayer);
	EXPECT_EQ(MyPlayer, &Players.back());
	EXPECT_EQ(MyPlayerId, 1);
	EXPECT_EQ(Players.size(), 2U);
	ASSERT_TRUE(LevelSeeds[5].has_value());
	EXPECT_EQ(*LevelSeeds[5], 0U);
	EXPECT_EQ(LevelSeeds[6], 0x12345678U);
	EXPECT_EQ(Quests[Q_BLIND]._qactive, QUEST_DONE);
	EXPECT_TRUE(gbIsSpawn);
	EXPECT_TRUE(gbIsMultiplayer);
	EXPECT_TRUE(gbIsHellfire);
	EXPECT_EQ(sgGameInitInfo.fullQuests, 0);
	EXPECT_EQ(sgGameInitInfo.nDifficulty, DIFF_HELL);
	EXPECT_EQ(GetLCGEngineState(), 0xFEDCBA98U);
	EXPECT_EQ(paths::AssetsPath(), priorAssetsPath);
	EXPECT_EQ(OverridePaths, (std::vector<std::string> { "catacombs-guard-override/" }));
	EXPECT_TRUE(MpqArchives.empty());
	EXPECT_TRUE(dungeonMask().test(4, 5));
	EXPECT_TRUE(trigFlag());
	EXPECT_EQ(numTriggers(), 1);
	EXPECT_EQ(trigs[0].position, (Point { 43, 47 }));
	EXPECT_EQ(trigs[0]._tmsg, WM_DIABTWARPUP);
	EXPECT_EQ(trigs[0]._tlvl, 5);
}

TEST(NativeCatacombsGenerationTest, CaptureQuestDisabledOriginalDiabloGeneration)
{
	const char *archivePath = std::getenv("GODEVILUTION_NATIVE_CATACOMBS_ARCHIVE");
	const char *directory = std::getenv("GODEVILUTION_NATIVE_CATACOMBS_REFERENCE_DIRECTORY");
	if (archivePath == nullptr || directory == nullptr)
		GTEST_SKIP() << "Explicit original archive and new Catacombs reference directory are required.";
	ASSERT_NE(archivePath[0], '\0');
	ASSERT_NE(directory[0], '\0');
	const std::filesystem::path root = std::filesystem::absolute(directory).lexically_normal();
	ASSERT_FALSE(std::filesystem::exists(root)) << "Refusing to overwrite existing native reference directory: " << root;
	const std::filesystem::path sourceRoot = root / "source";
	const std::filesystem::path casesRoot = root / "cases";
	NativeCatacombsStateGuard stateGuard;
	std::filesystem::create_directories(root);
	std::unique_ptr<mpqfs_archive_t, decltype(&mpqfs_close)> archive(mpqfs_open(archivePath), mpqfs_close);
	ASSERT_NE(archive, nullptr) << mpqfs_last_error();
	Extract(archive.get(), root, "levels/l2data/l2.til");
	Extract(archive.get(), root, "levels/l2data/l2.sol");
	ASSERT_FALSE(::testing::Test::HasFatalFailure());
	archive.reset();
	stateGuard.UseExtractedSource(sourceRoot);

	currentLevel().setId(LevelId { .levelNum = 5, .type = DTYPE_CATACOMBS });
	levelType() = DTYPE_CATACOMBS;
	auto tiles = LoadFileInMemWithStatus<MegaTile>("levels\\l2data\\l2.til");
	ASSERT_TRUE(tiles.has_value()) << "Could not load extracted production L2 TIL.";
	ASSERT_EQ(std::filesystem::file_size(sourceRoot / "levels/l2data/l2.til"), 160U * sizeof(MegaTile));
	megaTiles() = std::move(*tiles);
	// Native reads the source length into the fixed table; validate before that
	// pointer-based load and prove the remaining effective slots stay zero.
	const size_t sourceSolByteCount = std::filesystem::file_size(sourceRoot / "levels/l2data/l2.sol");
	ASSERT_GT(sourceSolByteCount, 0U);
	ASSERT_LE(sourceSolByteCount, static_cast<size_t>(MAXTILES));
	std::vector<uint8_t> sourceSol(sourceSolByteCount);
	std::ifstream sourceSolFile(sourceRoot / "levels/l2data/l2.sol", std::ios::binary);
	sourceSolFile.read(reinterpret_cast<char *>(sourceSol.data()), static_cast<std::streamsize>(sourceSol.size()));
	ASSERT_TRUE(sourceSolFile.good());
	std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
	ASSERT_TRUE(LoadLevelSOLData().has_value());
	for (size_t piece = 0; piece < MAXTILES; ++piece)
		ASSERT_EQ(static_cast<uint8_t>(tileProperties()[piece]), piece < sourceSol.size() ? sourceSol[piece] : 0U);

	std::filesystem::create_directories(casesRoot);
	std::ofstream index(casesRoot / "cases.tsv", std::ios::binary);
	ASSERT_TRUE(index.good());
	index << "case_id\tlevel_id\tentry\tinput_seed\tinitial_level_seed\tattempt_seed\tfinal_random_state\tview_x\tview_y\tfile\r\n";
	std::vector<std::pair<CaseSpec, GenerationSnapshot>> captured;
	const std::vector<CaseSpec> cases = BuildCases();
	captured.reserve(cases.size());
	for (const CaseSpec &spec : cases) {
		GenerationSnapshot snapshot;
		ASSERT_NO_FATAL_FAILURE(CaptureCase(spec, snapshot));
		const std::string file = spec.caseId + ".json";
		WriteJson(casesRoot / file, spec.levelId, spec, snapshot, sourceSolByteCount);
		index << spec.caseId << '\t' << spec.levelId << '\t' << EntryName(spec.entry) << '\t' << spec.inputSeed << '\t';
		if (spec.initialLevelSeed.has_value())
			index << *spec.initialLevelSeed;
		else
			index << "none";
		index << '\t' << snapshot.attemptSeed << '\t' << snapshot.finalRandomState << '\t'
		      << snapshot.view.x << '\t' << snapshot.view.y << '\t' << file << "\r\n";
		captured.emplace_back(spec, std::move(snapshot));
	}
	index.flush();
	index.close();
	ASSERT_TRUE(index.good());

	// Entry is only a view-selection input for L2; generation, stairs and RNG
	// must remain identical for the same seed under the quest-disabled policy.
	for (size_t firstIndex = 0; firstIndex < captured.size(); ++firstIndex) {
		const CaseSpec &firstSpec = captured[firstIndex].first;
		if (firstSpec.initialLevelSeed.has_value() || firstSpec.entry != ENTRY_MAIN)
			continue;
		for (size_t secondIndex = firstIndex + 1; secondIndex < captured.size(); ++secondIndex) {
			const CaseSpec &secondSpec = captured[secondIndex].first;
			if (secondSpec.levelId != firstSpec.levelId || secondSpec.inputSeed != firstSpec.inputSeed
			    || secondSpec.initialLevelSeed.has_value() || secondSpec.entry == ENTRY_MAIN)
				continue;
			if (secondSpec.entry != ENTRY_PREV && !(firstSpec.levelId == 5 && secondSpec.entry == ENTRY_TWARPDN))
				continue;
			ExpectSameGenerationExceptView(captured[firstIndex].second, captured[secondIndex].second);
		}
	}
	for (size_t firstIndex = 0; firstIndex < captured.size(); ++firstIndex) {
		const CaseSpec &firstSpec = captured[firstIndex].first;
		if (!firstSpec.initialLevelSeed.has_value())
			continue;
		const uint32_t retained = *firstSpec.initialLevelSeed;
		for (const auto &[normalSpec, normalSnapshot] : captured) {
			if (normalSpec.levelId != firstSpec.levelId || normalSpec.entry != ENTRY_MAIN
			    || normalSpec.initialLevelSeed.has_value() || normalSpec.inputSeed != retained)
				continue;
			ExpectSameGenerationExceptView(captured[firstIndex].second, normalSnapshot);
			EXPECT_EQ(captured[firstIndex].second.view, normalSnapshot.view);
		}
	}
	for (const auto &[spec, snapshot] : captured) {
		if (spec.entry == ENTRY_SETLVL || spec.entry == ENTRY_RTNLVL
		    || spec.entry == ENTRY_WARPLVL || spec.entry == ENTRY_TWARPUP
		    || (spec.levelId != 5 && spec.entry == ENTRY_TWARPDN)) {
			EXPECT_EQ(snapshot.view, (Point { static_cast<int>(SentinelViewX), static_cast<int>(SentinelViewY) }));
		}
	}
	EXPECT_EQ(captured.size(), 75U);
}

} // namespace
} // namespace devilution
