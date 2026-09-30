#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <optional>
#include <stdexcept>
#include <string>
#include <system_error>
#include <vector>

#include "application/game_mode.hpp"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "level_test.hpp"

using namespace devilution;

namespace {

constexpr char CathedralOracleDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY";
constexpr char CathedralOracleFormat[] = "godevilution-cathedral-fine-oracle";
constexpr uint32_t CathedralOracleVersion = 1;
constexpr int NativeWorldWidth = MAXDUNX;
constexpr int NativeWorldHeight = MAXDUNY;

struct CathedralOracleCase {
	uint32_t inputSeed;
	lvl_entry entry;
	uint32_t attemptSeed;
	uint32_t finalRandomState;
	Point entryPosition;
	Point stairsUp;
	Point stairsDown;
	std::vector<uint16_t> megaTiles;
	std::vector<uint16_t> pieces;
	std::vector<uint8_t> transparency;
	std::vector<uint8_t> solProperties;
	std::vector<uint32_t> solidCells;
	std::vector<uint32_t> missileBlockedCells;
	std::vector<uint32_t> blockLightCells;
};

template <typename T>
uint64_t CathedralOracleHashValues(const std::vector<T> &values)
{
	uint64_t hash = UINT64_C(14695981039346656037);
	for (const T value : values) {
		for (size_t byte = 0; byte < sizeof(T); byte++) {
			hash ^= static_cast<uint8_t>(static_cast<uint64_t>(value) >> (byte * 8));
			hash *= UINT64_C(1099511628211);
		}
	}
	return hash;
}

std::string CathedralOracleHex(uint64_t value)
{
	std::ostringstream stream;
	stream << "0x" << std::hex << std::setw(16) << std::setfill('0') << value;
	return stream.str();
}

std::string CathedralOracleLower(std::string value)
{
	std::transform(value.begin(), value.end(), value.begin(), [](unsigned char character) {
		return static_cast<char>(std::tolower(character));
	});
	return value;
}

std::filesystem::path CathedralOracleRepositoryPath(std::error_code &pathError)
{
	std::filesystem::path current = std::filesystem::weakly_canonical(paths::BasePath(), pathError);
	if (pathError)
		return {};
	for (;;) {
		if (std::filesystem::exists(current / "AGENTS.md")
			&& std::filesystem::exists(current / ".git")) {
			return current;
		}
		const auto parent = current.parent_path();
		if (parent == current)
			break;
		current = parent;
	}
	pathError = std::make_error_code(std::errc::no_such_file_or_directory);
	return {};
}

void WriteCathedralOracleNumber(std::ostream &stream, uint64_t value)
{
	stream << value;
}

template <typename T>
void WriteCathedralOracleArray(std::ostream &stream, const std::vector<T> &values)
{
	stream << '[';
	for (size_t index = 0; index < values.size(); index++) {
		if (index != 0)
			stream << ',';
		WriteCathedralOracleNumber(stream, static_cast<uint64_t>(values[index]));
	}
	stream << ']';
}

void WriteCathedralOraclePoint(std::ostream &stream, Point point)
{
	stream << '[' << point.x << ',' << point.y << ']';
}

bool WriteCathedralOracleCase(const std::filesystem::path &path, const CathedralOracleCase &oracleCase)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		return false;
	const char *entry = oracleCase.entry == ENTRY_MAIN ? "main" : "previous";
	stream << "{\n"
	       << "\"format\":\"" << CathedralOracleFormat << "\",\n"
	       << "\"version\":" << CathedralOracleVersion << ",\n"
	       << "\"level\":1,\n"
	       << "\"entry\":\"" << entry << "\",\n"
	       << "\"inputSeed\":" << oracleCase.inputSeed << ",\n"
	       << "\"attemptSeed\":" << oracleCase.attemptSeed << ",\n"
	       << "\"finalRandomState\":" << oracleCase.finalRandomState << ",\n"
	       << "\"coarseWidth\":40,\n"
	       << "\"coarseHeight\":40,\n"
	       << "\"nativeWidth\":" << NativeWorldWidth << ",\n"
	       << "\"nativeHeight\":" << NativeWorldHeight << ",\n"
	       << "\"solCount\":" << oracleCase.solProperties.size() << ",\n"
	       << "\"entryPosition\":";
	WriteCathedralOraclePoint(stream, oracleCase.entryPosition);
	stream << ",\n\"stairsUp\":";
	WriteCathedralOraclePoint(stream, oracleCase.stairsUp);
	stream << ",\n\"stairsDown\":";
	WriteCathedralOraclePoint(stream, oracleCase.stairsDown);
	stream << ",\n\"megaTiles\":";
	WriteCathedralOracleArray(stream, oracleCase.megaTiles);
	stream << ",\n\"pieces\":";
	WriteCathedralOracleArray(stream, oracleCase.pieces);
	stream << ",\n\"transparency\":";
	WriteCathedralOracleArray(stream, oracleCase.transparency);
	stream << ",\n\"solProperties\":";
	WriteCathedralOracleArray(stream, oracleCase.solProperties);
	stream << ",\n\"solidCells\":";
	WriteCathedralOracleArray(stream, oracleCase.solidCells);
	stream << ",\n\"missileBlockedCells\":";
	WriteCathedralOracleArray(stream, oracleCase.missileBlockedCells);
	stream << ",\n\"blockLightCells\":";
	WriteCathedralOracleArray(stream, oracleCase.blockLightCells);
	stream << ",\n\"hashes\":{\n"
	       << "\"megaTiles\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.megaTiles)) << "\",\n"
	       << "\"pieces\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.pieces)) << "\",\n"
	       << "\"transparency\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.transparency)) << "\",\n"
	       << "\"solProperties\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.solProperties)) << "\",\n"
	       << "\"solidCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.solidCells)) << "\",\n"
	       << "\"missileBlockedCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.missileBlockedCells)) << "\",\n"
	       << "\"blockLightCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.blockLightCells)) << "\"\n"
	       << "}\n}\n";
	stream.flush();
	if (!stream.good())
		return false;
	stream.close();
	return stream.good();
}

bool WriteCathedralOracleRawAsset(const std::filesystem::path &path, const uint8_t *data, size_t size)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		return false;
	stream.write(reinterpret_cast<const char *>(data), static_cast<std::streamsize>(size));
	stream.flush();
	if (!stream.good())
		return false;
	stream.close();
	return stream.good();
}

CathedralOracleCase CaptureCathedralOracleCase(uint32_t seed, lvl_entry entry)
{
	LevelSeeds[1] = std::nullopt;
	currentLevelNumber() = 1;
	levelType() = DTYPE_CATHEDRAL;
	CreateDungeon(seed, entry);

	CathedralOracleCase oracleCase {
		seed,
		entry,
		LevelSeeds[1].value(),
		GetLCGEngineState(),
		viewPosition(),
		Point { -1, -1 },
		Point { -1, -1 },
		{},
		{},
		{},
		{},
		{},
		{},
		{},
	};
	int stairsUpCount = 0;
	int stairsDownCount = 0;
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++) {
			const auto value = megaTileAt(x, y).current();
			oracleCase.megaTiles.push_back(value);
			if (value == 66) {
				stairsUpCount++;
				oracleCase.stairsUp = { x - 1, y };
			}
			if (value == 57) {
				stairsDownCount++;
				oracleCase.stairsDown = { x - 1, y };
			}
		}
	}
	for (int y = 0; y < NativeWorldHeight; y++) {
		for (int x = 0; x < NativeWorldWidth; x++) {
			const int cell = y * NativeWorldWidth + x;
			const Tile &tile = tileAt(x, y);
			const uint16_t piece = tile.piece();
			if (piece >= MAXTILES)
				throw std::runtime_error("Cathedral oracle produced an out-of-range d-piece");
			const TileProperties properties = tileProperties()[piece];
			oracleCase.pieces.push_back(piece);
			oracleCase.transparency.push_back(static_cast<uint8_t>(tile.transVal()));
			if (HasAnyOf(properties, TileProperties::Solid))
				oracleCase.solidCells.push_back(static_cast<uint32_t>(cell));
			if (HasAnyOf(properties, TileProperties::BlockMissile))
				oracleCase.missileBlockedCells.push_back(static_cast<uint32_t>(cell));
			if (HasAnyOf(properties, TileProperties::BlockLight))
				oracleCase.blockLightCells.push_back(static_cast<uint32_t>(cell));
		}
	}
	for (int piece = 0; piece < MAXTILES; piece++)
		oracleCase.solProperties.push_back(static_cast<uint8_t>(tileProperties()[piece]));
	if (stairsUpCount != 1 || stairsDownCount != 1
		|| oracleCase.stairsUp.x == -1 || oracleCase.stairsDown.x == -1)
		throw std::runtime_error("Cathedral oracle could not locate generated stairs");
	return oracleCase;
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_1_2588)
{
	LoadExpectedLevelData("diablo/1-2588.dun");

	TestInitGame();

	TestCreateDungeon(1, 2588, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(77, 46));
	TestCreateDungeon(1, 2588, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 49));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_1_743271966)
{
	LoadExpectedLevelData("diablo/1-743271966.dun");

	TestInitGame();

	TestCreateDungeon(1, 743271966, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(51, 82));
	TestCreateDungeon(1, 743271966, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(81, 47));
}

TEST(Drlg_l1, CreateL5Dungeon_rng_state_matches_native_oracle)
{
	struct OracleCase {
		uint32_t seed;
		lvl_entry entry;
		uint32_t attemptSeed;
		uint32_t finalRandomState;
	};

	const OracleCase cases[] = {
		{ UINT32_C(2588), ENTRY_MAIN, UINT32_C(1779836089), UINT32_C(1552486206) },
		{ UINT32_C(2588), ENTRY_PREV, UINT32_C(1779836089), UINT32_C(1552486206) },
		{ UINT32_C(743271966), ENTRY_MAIN, UINT32_C(283661120), UINT32_C(980632310) },
		{ UINT32_C(743271966), ENTRY_PREV, UINT32_C(283661120), UINT32_C(980632310) },
	};

	TestInitGame();
	for (const auto &testCase : cases) {
		LevelSeeds[1] = std::nullopt;
		currentLevelNumber() = 1;
		levelType() = GetLevelType(1);
		megaTiles() = std::make_unique<MegaTile[]>(GetTileCount(levelType()));

		CreateDungeon(testCase.seed, testCase.entry);
		ASSERT_TRUE(LevelSeeds[1].has_value());
		EXPECT_EQ(*LevelSeeds[1], testCase.attemptSeed);
		EXPECT_EQ(GetLCGEngineState(), testCase.finalRandomState);
	}
}

TEST(Drlg_l1, CathedralFineGeometryLocalAssetOracle)
{
	const char *outputDirectory = std::getenv(CathedralOracleDirectoryVariable);
	if (outputDirectory == nullptr || outputDirectory[0] == '\0') {
		GTEST_SKIP() << "Set " << CathedralOracleDirectoryVariable
		             << " to run the opt-in local-asset Cathedral fine-geometry oracle";
	}

	const std::filesystem::path outputPath(outputDirectory);
	ASSERT_TRUE(std::filesystem::exists(outputPath))
		<< "Cathedral oracle output directory does not exist: " << outputPath.string();
	ASSERT_TRUE(std::filesystem::is_directory(outputPath))
		<< "Cathedral oracle output path is not a directory: " << outputPath.string();
	std::error_code pathError;
	const auto canonicalOutputPath = std::filesystem::weakly_canonical(outputPath, pathError);
	ASSERT_FALSE(pathError) << "Unable to canonicalize Cathedral oracle output path: " << pathError.message();
	const auto canonicalRepositoryPath = CathedralOracleRepositoryPath(pathError);
	ASSERT_FALSE(pathError) << "Unable to locate repository root for oracle isolation: " << pathError.message();
	std::string repositoryPrefix = CathedralOracleLower(canonicalRepositoryPath.generic_string());
	if (!repositoryPrefix.ends_with('/'))
		repositoryPrefix += '/';
	const std::string canonicalOutput = CathedralOracleLower(canonicalOutputPath.generic_string());
	ASSERT_FALSE(canonicalOutput == repositoryPrefix.substr(0, repositoryPrefix.size() - 1)
		|| canonicalOutput.starts_with(repositoryPrefix))
		<< "Cathedral oracle output must be outside the repository: " << canonicalOutputPath.string();
	ASSERT_TRUE(std::filesystem::is_empty(outputPath))
		<< "Cathedral oracle output directory must be fresh: " << outputPath.string();

	TestInitGame();
	// TestInitGame installs its preference directory as an asset override.
	// Remove that override and any prior main archive before resolving the
	// original archive through the production GOG-aware search order.
	UnloadModArchives();
	MpqArchives.erase(MainMpqPriority);
	gbIsSpawn = false;
	LoadGameArchives();
	ASSERT_TRUE(HaveMainData()) << "DIABDAT.MPQ was not discovered by LoadGameArchives";
	ASSERT_FALSE(gbIsSpawn) << "Cathedral oracle resolved spawn.mpq instead of original DIABDAT.MPQ";

	levelType() = DTYPE_CATHEDRAL;
	currentLevelNumber() = 1;

#ifndef UNPACKED_MPQS
	const auto &mainArchive = MpqArchives.at(MainMpqPriority);
	for (const std::string_view assetPath : std::array<std::string_view, 2> {
		"levels\\l1data\\l1.til",
		"levels\\l1data\\l1.sol",
	}) {
		AssetRef asset = FindAsset(assetPath);
		ASSERT_TRUE(asset.ok()) << "Required Cathedral asset is unavailable: " << assetPath;
		ASSERT_EQ(asset.archive, &mainArchive)
			<< "Cathedral asset did not resolve from the original main archive: " << assetPath;
	}
#endif

	size_t tilCount = 0;
	auto tilDefinitions = LoadFileInMemWithStatus<MegaTile>(
		"levels\\l1data\\l1.til", &tilCount);
	ASSERT_TRUE(tilDefinitions.has_value()) << tilDefinitions.error();
	ASSERT_NE(tilDefinitions->get(), nullptr);
	ASSERT_EQ(tilCount, static_cast<size_t>(GetTileCount(DTYPE_CATHEDRAL)));

	size_t tilByteCount = 0;
	auto tilBytesResult = LoadFileInMemWithStatus<std::byte>(
		"levels\\l1data\\l1.til", &tilByteCount);
	ASSERT_TRUE(tilBytesResult.has_value()) << tilBytesResult.error();
	ASSERT_NE(tilBytesResult->get(), nullptr);
	ASSERT_EQ(tilByteCount, tilCount * sizeof(MegaTile));
	std::vector<uint8_t> tilBytes(tilByteCount);
	std::memcpy(tilBytes.data(), tilBytesResult->get(), tilByteCount);

	size_t solByteCount = 0;
	auto solBytesResult = LoadFileInMemWithStatus<std::byte>(
		"levels\\l1data\\l1.sol", &solByteCount);
	ASSERT_TRUE(solBytesResult.has_value()) << solBytesResult.error();
	ASSERT_NE(solBytesResult->get(), nullptr);
	ASSERT_GT(solByteCount, static_cast<size_t>(0));
	ASSERT_LE(solByteCount, static_cast<size_t>(MAXTILES));
	std::vector<uint8_t> solBytes(solByteCount);
	std::memcpy(solBytes.data(), solBytesResult->get(), solByteCount);

	for (int piece = 0; piece < MAXTILES; piece++)
		tileProperties()[piece] = TileProperties::None;
	auto solLoad = LoadLevelSOLData();
	ASSERT_TRUE(solLoad.has_value()) << solLoad.error();

	std::vector<MegaTile> immutableTil(tilCount);
	std::copy_n(tilDefinitions->get(), tilCount, immutableTil.begin());
	const struct OracleInput {
		uint32_t seed;
		lvl_entry entry;
		const char *fileName;
		uint32_t attemptSeed;
		uint32_t finalRandomState;
		Point expectedEntry;
	} inputs[] = {
		{ UINT32_C(2588), ENTRY_MAIN, "cathedral-2588-main.json", UINT32_C(1779836089), UINT32_C(1552486206), { 77, 46 } },
		{ UINT32_C(2588), ENTRY_PREV, "cathedral-2588-previous.json", UINT32_C(1779836089), UINT32_C(1552486206), { 49, 49 } },
		{ UINT32_C(743271966), ENTRY_MAIN, "cathedral-743271966-main.json", UINT32_C(283661120), UINT32_C(980632310), { 51, 82 } },
		{ UINT32_C(743271966), ENTRY_PREV, "cathedral-743271966-previous.json", UINT32_C(283661120), UINT32_C(980632310), { 81, 47 } },
	};
	std::vector<CathedralOracleCase> cases;
	cases.reserve(std::size(inputs));
	for (const auto &input : inputs) {
		megaTiles() = std::make_unique<MegaTile[]>(tilCount);
		std::copy(immutableTil.begin(), immutableTil.end(), megaTiles().get());
		cases.push_back(CaptureCathedralOracleCase(input.seed, input.entry));
		const CathedralOracleCase &captured = cases.back();
		ASSERT_EQ(captured.attemptSeed, input.attemptSeed);
		ASSERT_EQ(captured.finalRandomState, input.finalRandomState);
		ASSERT_EQ(captured.entryPosition, input.expectedEntry);
	}

	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.til", tilBytes.data(), tilBytes.size()));
	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.sol", solBytes.data(), solBytes.size()));
	for (size_t index = 0; index < std::size(inputs); index++) {
		ASSERT_TRUE(WriteCathedralOracleCase(outputPath / inputs[index].fileName, cases[index]));
	}
}

TEST(Drlg_l1, CreateL5Dungeon_reuses_successful_attempt_seed)
{
	TestInitGame();
	constexpr uint32_t dungeonSeed = UINT32_C(2588);
	constexpr lvl_entry entry = ENTRY_MAIN;

	LevelSeeds[1] = std::nullopt;
	currentLevelNumber() = 1;
	levelType() = GetLevelType(1);
	megaTiles() = std::make_unique<MegaTile[]>(GetTileCount(levelType()));
	CreateDungeon(dungeonSeed, entry);
	ASSERT_TRUE(LevelSeeds[1].has_value());
	const uint32_t successfulAttemptSeed = *LevelSeeds[1];
	const uint32_t initialFinalRandomState = GetLCGEngineState();

	std::array<uint16_t, DMAXX * DMAXY> initialMegaTiles;
	std::array<uint8_t, (DMAXX * 2) * (DMAXY * 2)> initialTransparency;
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++)
			initialMegaTiles[y * DMAXX + x] = megaTileAt(x, y).current();
	}
	for (int y = 0; y < DMAXY * 2; y++) {
		for (int x = 0; x < DMAXX * 2; x++)
			initialTransparency[y * DMAXX * 2 + x] = tileAt(x + 16, y + 16).transVal();
	}

	LevelSeeds[1] = successfulAttemptSeed;
	megaTiles() = std::make_unique<MegaTile[]>(GetTileCount(levelType()));
	CreateDungeon(dungeonSeed, entry);
	ASSERT_TRUE(LevelSeeds[1].has_value());
	EXPECT_EQ(*LevelSeeds[1], successfulAttemptSeed);
	EXPECT_EQ(GetLCGEngineState(), initialFinalRandomState);
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++)
			EXPECT_EQ(megaTileAt(x, y).current(), initialMegaTiles[y * DMAXX + x]);
	}
	for (int y = 0; y < DMAXY * 2; y++) {
		for (int x = 0; x < DMAXX * 2; x++)
			EXPECT_EQ(tileAt(x + 16, y + 16).transVal(), initialTransparency[y * DMAXX * 2 + x]);
	}
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_2_1383137027)
{
	LoadExpectedLevelData("diablo/2-1383137027.dun");

	TestInitGame();
	Quests[Q_PWATER]._qactive = QUEST_INIT;
	Quests[Q_BUTCHER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(2, 1383137027, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(57, 74));
	TestCreateDungeon(2, 1383137027, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(57, 79));
	TestCreateDungeon(2, 1383137027, ENTRY_RTNLVL);
	EXPECT_EQ(viewPosition(), Point(49, 88));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_3_844660068)
{
	LoadExpectedLevelData("diablo/3-844660068.dun");

	TestInitGame();

	TestCreateDungeon(3, 844660068, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(67, 52));
	TestCreateDungeon(3, 844660068, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(85, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_4_609325643)
{
	LoadExpectedLevelData("diablo/4-609325643.dun");

	TestInitGame();
	Quests[Q_LTBANNER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(4, 609325643, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(85, 78));
	TestCreateDungeon(4, 609325643, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(55, 47));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_4_902156014)
{
	LoadExpectedLevelData("diablo/4-902156014.dun");

	TestInitGame();
	Quests[Q_LTBANNER]._qactive = QUEST_INIT;

	TestCreateDungeon(4, 902156014, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(75, 56));
	TestCreateDungeon(4, 902156014, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(51, 59));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_1_401921334)
{
	LoadExpectedLevelData("hellfire/1-401921334.dun");

	TestInitGame(true, false);

	TestCreateDungeon(1, 401921334, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(79, 80));
	TestCreateDungeon(1, 401921334, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 63));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_1_536340718)
{
	LoadExpectedLevelData("hellfire/1-536340718.dun");

	TestInitGame(true, false);

	TestCreateDungeon(1, 536340718, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(55, 72));
	TestCreateDungeon(1, 536340718, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 63));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_2_128964898)
{
	LoadExpectedLevelData("hellfire/2-128964898.dun");

	TestInitGame(true, false);
	Quests[Q_PWATER]._qactive = QUEST_NOTAVAIL;
	Quests[Q_BUTCHER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(2, 128964898, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(55, 68));
	TestCreateDungeon(2, 128964898, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 63));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_2_1180526547)
{
	LoadExpectedLevelData("hellfire/2-1180526547.dun");

	TestInitGame(true, false);
	Quests[Q_PWATER]._qactive = QUEST_NOTAVAIL;
	Quests[Q_BUTCHER]._qactive = QUEST_INIT;

	TestCreateDungeon(2, 1180526547, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(83, 68));
	TestCreateDungeon(2, 1180526547, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(81, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_3_1369955278)
{
	LoadExpectedLevelData("hellfire/3-1369955278.dun");

	TestInitGame(true, false);
	Quests[Q_SKELKING]._qactive = QUEST_INIT;

	TestCreateDungeon(3, 1369955278, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(63, 86));
	TestCreateDungeon(3, 1369955278, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(75, 69));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_3_1799396623)
{
	LoadExpectedLevelData("hellfire/3-1799396623.dun");

	TestInitGame(true, false);
	Quests[Q_SKELKING]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(3, 1799396623, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(59, 68));
	TestCreateDungeon(3, 1799396623, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(47, 55));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_3_1512491184)
{
	LoadExpectedLevelData("hellfire/3-1512491184.dun");

	TestInitGame(true, false);
	Quests[Q_SKELKING]._qactive = QUEST_INIT;

	TestCreateDungeon(3, 1512491184, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(47, 72));
	TestCreateDungeon(3, 1512491184, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(79, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_4_1190318991)
{
	LoadExpectedLevelData("hellfire/4-1190318991.dun");

	TestInitGame(true, false);
	Quests[Q_LTBANNER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(4, 1190318991, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(67, 80));
	TestCreateDungeon(4, 1190318991, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(77, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_4_1924296259)
{
	LoadExpectedLevelData("hellfire/4-1924296259.dun");

	TestInitGame(true, false);
	Quests[Q_LTBANNER]._qactive = QUEST_INIT;

	TestCreateDungeon(4, 1924296259, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(83, 54));
	TestCreateDungeon(4, 1924296259, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(51, 87));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_1_2122696790)
{
	LoadExpectedLevelData("hellfire/21-2122696790.dun");

	TestInitGame(true, true, true);

	TestCreateDungeon(21, 2122696790, ENTRY_TWARPDN);
	EXPECT_EQ(viewPosition(), Point(61, 81));
	TestCreateDungeon(21, 2122696790, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(53, 67));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_2_1191662129)
{
	LoadExpectedLevelData("hellfire/22-1191662129.dun");

	TestInitGame(true, true, true);
	Quests[Q_PWATER]._qactive = QUEST_NOTAVAIL;
	Quests[Q_BUTCHER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(22, 1191662129, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(71, 47));
	TestCreateDungeon(22, 1191662129, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(85, 71));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_3_97055268)
{
	LoadExpectedLevelData("hellfire/23-97055268.dun");

	TestInitGame(true, true, true);

	TestCreateDungeon(23, 97055268, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(71, 57));
	TestCreateDungeon(23, 97055268, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(81, 59));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_4_1324803725)
{
	LoadExpectedLevelData("hellfire/24-1324803725.dun");

	TestInitGame(true, true, true);

	TestCreateDungeon(24, 1324803725, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(79, 47));
}

} // namespace
