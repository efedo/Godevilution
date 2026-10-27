#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <functional>
#include <memory>
#include <string>
#include <vector>

#include <mpqfs/mpqfs.h>

#include "application/game_mode.hpp"
#include "engine/gfx/surface.hpp"
#include "engine/lighting.h"
#include "engine/load/load_cel.hpp"
#include "engine/load/load_file.hpp"
#include "engine/random.hpp"
#include "engine/render/clx_render.hpp"
#include "engine/render/dun_render.hpp"
#include "engine/render/world_renderer.h"
#include "game/levels/dungeon_common.h"
#include "game/levels/town.h"
#include "game/levels/triggers.h"
#include "game/players/players.hpp"
#include "game/quests/quests.hpp"
#include "network/protocol/multi.h"
#include "persistence/options.h"
#include "tables/playerdat.hpp"
#include "utils/endian/endian_swap.hpp"
#include "utils/paths.h"
#include "utils/sdl/sdl_compat.h"

namespace devilution {
namespace {

void Number(std::ostream &out, uint32_t value)
{
	for (int shift = 0; shift < 32; shift += 8)
		out.put(static_cast<char>(value >> shift));
}

// Generator/blitter state is restored. Canonical player tables stay loaded under
// the existing native table-test convention. The level channel buffer is on the heap.
class NativeSceneGuard {
public:
	NativeSceneGuard()
	    : level(std::make_unique<Level>(currentLevel().id()))
	    , light(std::make_unique<LightManager>(CurrentLightManager))
	    , player(MyPlayer)
	    , spawn(gbIsSpawn)
	    , hellfire(gbIsHellfire)
	    , multiplayer(gbIsMultiplayer)
	    , seed(GetLCGEngineState())
	    , assets(paths::AssetsPath())
	    , overrides(OverridePaths)
	    , archives(std::move(MpqArchives))
	    , perPixel(*GetOptions().Graphics.perPixelLighting)
	{
		std::copy(std::begin(Quests), std::end(Quests), quests.begin());
		MpqArchives.clear();
		const auto repository = std::filesystem::absolute(__FILE__).parent_path().parent_path();
		OverridePaths = { (repository / "assets").string() + "/" };
		LoadPlayerDataFiles();
		OverridePaths.clear();
		SwapLevelChannels();
		currentLevel().setId(LevelId { .levelNum = 0, .type = DTYPE_TOWN });
		MyPlayer = &localPlayer;
		gbIsSpawn = false;
		gbIsMultiplayer = false;
		gbIsHellfire = false;
		GetOptions().Graphics.perPixelLighting.SetValue(false);
		for (auto &table : CurrentLightManager.lightTables_) {
			for (size_t i = 0; i < table.size(); ++i)
				table[i] = static_cast<uint8_t>(i);
		}
		CurrentLightManager.fullyLitLightTable_ = CurrentLightManager.lightTables_[0].data();
	}

	~NativeSceneGuard()
	{
		SwapLevelChannels();
		currentLevel().setId(level->id());
		CurrentLightManager = *light;
		MyPlayer = player;
		gbIsSpawn = spawn;
		gbIsHellfire = hellfire;
		gbIsMultiplayer = multiplayer;
		std::copy(quests.begin(), quests.end(), std::begin(Quests));
		SetRndSeed(seed);
		paths::SetAssetsPath(assets);
		OverridePaths = std::move(overrides);
		MpqArchives = std::move(archives);
		GetOptions().Graphics.perPixelLighting.SetValue(perPixel);
	}

private:
	void SwapLevelChannels()
	{
		// Bestiary owns unrelated non-movable storage; swap only channels this
		// fixture can touch, including every CreateDungeon initialization field.
		auto &active = currentLevel();
		using std::swap;
		swap(active.tiles_, level->tiles_);
		swap(active.megaTiles_, level->megaTiles_);
		swap(active.Protected_, level->Protected_);
		swap(active.SetPieceRoom_, level->SetPieceRoom_);
		swap(active.SetPiece_, level->SetPiece_);
		swap(active.pSpecialCels_, level->pSpecialCels_);
		swap(active.pMegaTiles_, level->pMegaTiles_);
		swap(active.pDungeonCels_, level->pDungeonCels_);
		swap(active.SOLData_, level->SOLData_);
		swap(active.dminPosition_, level->dminPosition_);
		swap(active.dmaxPosition_, level->dmaxPosition_);
		swap(active.ViewPosition_, level->ViewPosition_);
		swap(active.MicroTileLen_, level->MicroTileLen_);
		swap(active.TransVal_, level->TransVal_);
		swap(active.TransList_, level->TransList_);
		swap(active.DPieceMicros_, level->DPieceMicros_);
		swap(active.tWarpFrom_, level->tWarpFrom_);
	}

	std::unique_ptr<Level> level;
	std::unique_ptr<LightManager> light;
	Player *player;
	Player localPlayer {};
	std::array<Quest, MAXQUESTS> quests;
	bool spawn;
	bool hellfire;
	bool multiplayer;
	uint32_t seed;
	std::string assets;
	std::vector<std::string> overrides;
	decltype(MpqArchives) archives;
	bool perPixel;
};

void WriteBytes(const std::filesystem::path &path, const std::vector<uint8_t> &data)
{
	std::filesystem::create_directories(path.parent_path());
	std::ofstream out(path, std::ios::binary);
	out.write(reinterpret_cast<const char *>(data.data()), static_cast<std::streamsize>(data.size()));
	out.close();
	ASSERT_TRUE(out.good()) << path;
}

void Extract(mpqfs_archive_t *archive, const std::filesystem::path &root, const std::string &name)
{
	ASSERT_TRUE(mpqfs_has_file(archive, name.c_str())) << name;
	const auto size = mpqfs_file_size(archive, name.c_str());
	ASSERT_GT(size, 0) << name;
	ASSERT_LE(size, 16 * 1024 * 1024) << name;
	std::vector<uint8_t> data(size);
	ASSERT_EQ(mpqfs_read_file_into(archive, name.c_str(), data.data(), size), size) << name;
	WriteBytes(root / name, data);
}

// Paired backgrounds distinguish every opaque index (including zero) from skips.
void Pixels(std::ostream &out, int width, int height, const std::function<void(const Surface &, const Lightmap &)> &draw)
{
	OwnedSurface first(width, height);
	OwnedSurface second(width, height);
	SDL_FillSurfaceRect(first.surface, nullptr, 0xAB);
	SDL_FillSurfaceRect(second.surface, nullptr, 0xCD);
	std::vector<uint8_t> light(static_cast<size_t>(first.pitch()) * height);
	for (const auto &surface : { first.surface, second.surface }) {
		Surface output(surface);
		Lightmap map(output.begin(), light, output.pitch(), CurrentLightManager.lightTables_,
		    CurrentLightManager.lightTables_[0].data(), CurrentLightManager.lightTables_.back().data());
		draw(output, map);
	}
	for (int y = 0; y < height; ++y) {
		for (int x = 0; x < width; ++x) {
			const bool opaque = *first.at(x, y) == *second.at(x, y);
			out.put(static_cast<char>(opaque ? *first.at(x, y) : 0));
			out.put(static_cast<char>(opaque ? 255 : 0));
		}
	}
}

void CaptureArt(const std::filesystem::path &root, const std::string &family, const std::string &base, int blocks)
{
	levelType() = family == "Town" ? DTYPE_TOWN : DTYPE_CATHEDRAL;
	std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
	ASSERT_TRUE(LoadLevelSOLData().has_value());
	size_t words = 0;
	auto min = LoadFileInMem<uint16_t>(base + ".min", &words);
	ASSERT_EQ(words % blocks, 0);
	const size_t pieceCount = words / blocks;
	ASSERT_LE(pieceCount, MAXTILES);
	dungeonCels() = LoadFileInMem<std::byte>(base + ".cel");
	SetDungeonMicros(dungeonCels(), microTileLength());
	ASSERT_EQ(microTileLength(), blocks);
	const auto directory = root / family;
	std::filesystem::create_directories(directory);
	std::ofstream out(directory / "native-reference.bin", std::ios::binary);
	std::ofstream composed(directory / "native-pieces.bin", std::ios::binary);
	for (auto *stream : { &out, &composed }) {
		Number(*stream, 1);
		Number(*stream, static_cast<uint32_t>(pieceCount));
		Number(*stream, blocks);
	}
	for (uint32_t piece = 0; piece < pieceCount; ++piece) {
		const auto flags = tileProperties()[piece];
		const bool floor = !HasAnyOf(flags, TileProperties::Solid | TileProperties::BlockMissile);
		Number(out, piece);
		Number(out, static_cast<uint8_t>(flags));
		for (int slot = 0; slot < blocks; ++slot) {
			Number(out, Swap16LE(min[piece * blocks + blocks - 2 + (slot & 1) - (slot & 0xE)]));
			const auto micro = levelMicros()[piece].mt[slot];
			Number(out, micro.data);
			Pixels(out, 32, 32, [&](const Surface &surface, const Lightmap &lightmap) {
				if (micro.hasValue() && floor && slot < 2)
					RenderTileFrame(surface, lightmap, { 0, 31 }, slot == 0 ? TileType::LeftTriangle : TileType::RightTriangle,
					    GetDunFrame(dungeonCels().get(), micro.frame()), 31, MaskType::Solid, CurrentLightManager.lightTables_[0].data());
			});
			Pixels(out, 32, 32, [&](const Surface &surface, const Lightmap &lightmap) {
				if (!micro.hasValue())
					return;
				if (floor && slot < 2) {
					if (micro.type() == TileType::TransparentSquare)
						RenderTileFoliage(surface, lightmap, { 0, 31 }, dungeonCels().get(), micro, CurrentLightManager.lightTables_[0].data());
				} else {
					RenderTile(surface, lightmap, { 0, 31 }, dungeonCels().get(), micro, MaskType::Solid, CurrentLightManager.lightTables_[0].data());
				}
			});
		}
		tileAt(20, 20).clear();
		tileAt(20, 20).setPiece(static_cast<uint16_t>(piece));
		Number(composed, piece);
		for (bool floorPass : { true, false }) {
			Pixels(composed, 64, 256, [&](const Surface &surface, const Lightmap &lightmap) {
				TestDrawNativeScenePiece(surface, lightmap, { 20, 20 }, { 0, 255 }, floorPass);
			});
		}
	}
	out.close();
	composed.close();
	ASSERT_TRUE(out.good());
	ASSERT_TRUE(composed.good());
	auto special = LoadCelWithStatus((base + "s").c_str(), 64);
	ASSERT_TRUE(special.has_value());
	std::ofstream sprites(directory / "native-specials.bin", std::ios::binary);
	Number(sprites, 1);
	Number(sprites, special->numSprites());
	for (auto sprite : ClxSpriteList { *special }) {
		Number(sprites, sprite.width());
		Number(sprites, sprite.height());
		Pixels(sprites, sprite.width(), sprite.height(), [&](const Surface &surface, const Lightmap &) {
			ClxDraw(surface, { 0, static_cast<int>(sprite.height()) - 1 }, sprite);
		});
	}
	sprites.close();
	ASSERT_TRUE(sprites.good());
}

void TownState(std::ostream &out)
{
	Number(out, GetLCGEngineState());
	Number(out, viewPosition().x);
	Number(out, viewPosition().y);
	Number(out, minimumDungeonPosition().x);
	Number(out, minimumDungeonPosition().y);
	Number(out, maximumDungeonPosition().x);
	Number(out, maximumDungeonPosition().y);
	for (int y = 0; y < MAXDUNY; ++y) {
		for (int x = 0; x < MAXDUNX; ++x) {
			const auto &tile = tileAt(x, y);
			Number(out, tile.piece());
			Number(out, tile.special());
			Number(out, static_cast<uint8_t>(tileProperties()[tile.piece()]));
			Number(out, static_cast<uint8_t>(tile.transVal()));
		}
	}
	for (int y = 0; y < DMAXY; ++y) {
		for (int x = 0; x < DMAXX; ++x) {
			Number(out, megaTileAt(x, y).current());
			Number(out, protectedTiles().test(x, y));
		}
	}
}

void PrepareTown(uint32_t seed, bool hf, int warps, bool water, bool hive, bool grave, bool dirty)
{
	gbIsHellfire = hf;
	gbIsSpawn = false;
	gbIsMultiplayer = false;
	MyPlayer->pTownWarps = static_cast<uint8_t>(warps);
	MyPlayer->setCharacterLevel(1);
	for (Quest &quest : Quests)
		quest._qactive = QUEST_NOTAVAIL;
	Quests[Q_PWATER]._qactive = water ? QUEST_ACTIVE : QUEST_DONE;
	Quests[Q_FARMER]._qactive = hive ? QUEST_DONE : QUEST_INIT;
	Quests[Q_GRAVE]._qactive = grave ? QUEST_DONE : QUEST_INIT;
	for (int y = 0; y < MAXDUNY; ++y) {
		for (int x = 0; x < MAXDUNX; ++x) {
			tileAt(x, y).clear();
			if (dirty) {
				tileAt(x, y).setSpecial(31);
				tileAt(x, y).setTransVal(77);
			}
		}
	}
	protectedTiles().reset();
	for (int y = 0; y < DMAXY; ++y) {
		for (int x = 0; x < DMAXX; ++x) {
			megaTileAt(x, y).setCurrent(dirty ? 19 : 0);
			protectedTiles().set(x, y, dirty && ((x + y) % 3 == 0));
		}
	}
	tWarpFrom() = 0;
	viewPosition() = { 41, 42 };
	SetRndSeed(seed);
}

void CaptureTown(const std::filesystem::path &root)
{
	levelType() = DTYPE_TOWN;
	megaTiles() = LoadFileInMem<MegaTile>("levels/towndata/town.til");
	std::fill(std::begin(tileProperties()), std::end(tileProperties()), TileProperties::None);
	ASSERT_TRUE(LoadLevelSOLData().has_value());
	const auto directory = root / "Town";
	std::filesystem::create_directories(directory);
	std::ofstream manifest(directory / "geometry.tsv", std::ios::binary);
	manifest << "file\tseed\thellfire\twarps\twater\thive\tgrave\tentry\twarp_from\tdirty\tmutation\twater_state\r\n";
	int serial = 0;
	auto capture = [&](uint32_t seed, bool hf, int warps, bool water, bool hive, bool grave, lvl_entry entry, int from, bool dirty, const char *mutation, int waterState = -1) {
		PrepareTown(seed, hf, warps, water, hive, grave, dirty);
		if (waterState >= 0) Quests[Q_PWATER]._qactive = static_cast<quest_state>(waterState);
		tWarpFrom() = from;
		CreateDungeon(seed ^ 0x5A5A5A5A, entry); // Town ignores this argument: caller owns RNG seeding.
		if (std::string(mutation) == "hive") TownOpenHive();
		if (std::string(mutation) == "grave") TownOpenGrave();
		if (std::string(mutation) == "fountain") CleanTownFountain();
		if (std::string(mutation) == "all") {
			TownOpenHive();
			TownOpenGrave();
			CleanTownFountain();
		}
		const auto name = "geometry-" + std::to_string(serial++) + ".bin";
		std::ofstream out(directory / name, std::ios::binary);
		TownState(out);
		out.close();
		EXPECT_TRUE(out.good());
		manifest << name << '\t' << seed << '\t' << hf << '\t' << warps << '\t' << water << '\t' << hive << '\t' << grave
		         << '\t' << static_cast<int>(entry) << '\t' << from << '\t' << dirty << '\t' << mutation
		         << '\t' << static_cast<int>(Quests[Q_PWATER]._qactive) << "\r\n";
	};
	for (uint32_t seed : { 2588U, 0x80000000U }) {
		for (bool hf : { false, true }) {
			for (int warps = 0; warps < 8; ++warps) {
				for (bool water : { false, true }) {
					for (int quest = 0; quest < (hf ? 4 : 1); ++quest)
						capture(seed, hf, warps, water, (quest & 1) != 0, (quest & 2) != 0, ENTRY_MAIN, 0, false, "none");
				}
			}
		}
	}
	EXPECT_EQ(serial, 160);
	for (int entry = ENTRY_MAIN; entry <= ENTRY_TWARPUP; ++entry) {
		for (bool dirty : { false, true })
			capture(2588, true, 0, true, false, false, static_cast<lvl_entry>(entry), 5, dirty, "none");
	}
	for (int from : { 9, 13, 17, 21, 99 })
		capture(2588, true, 7, false, true, true, ENTRY_TWARPUP, from, false, "none");
	for (const char *mutation : { "hive", "grave", "fountain", "all" }) {
		for (bool hf : { false, true })
			capture(0x80000000U, hf, 0, true, false, false, ENTRY_LOAD, 0, true, mutation);
	}
	EXPECT_EQ(serial, 189);
	capture(2588, false, 0, true, false, false, ENTRY_MAIN, 0, false, "none", QUEST_INIT);
	capture(2588, false, 0, false, false, false, ENTRY_MAIN, 0, false, "none", QUEST_NOTAVAIL);
	manifest.close();
	ASSERT_TRUE(manifest.good());
	EXPECT_EQ(serial, 191);
}

TEST(NativeSceneArtTest, WarpPolicyCoversModesBitsThresholdsAndQuestStates)
{
	NativeSceneGuard guard;
	for (bool spawn : { false, true })
		for (bool mp : { false, true })
			for (bool hf : { false, true })
				for (int bits = 0; bits < 8; ++bits)
					for (int level : { 1, 9, 10, 14, 15, 19, 20 })
						for (quest_state farmer : { QUEST_NOTAVAIL, QUEST_INIT, QUEST_ACTIVE, QUEST_DONE, QUEST_HIVE_DONE })
							for (quest_state grave : { QUEST_NOTAVAIL, QUEST_INIT, QUEST_ACTIVE, QUEST_DONE }) {
								gbIsSpawn = spawn;
								gbIsMultiplayer = mp;
								gbIsHellfire = hf;
								MyPlayer->pTownWarps = bits;
								MyPlayer->setCharacterLevel(static_cast<uint8_t>(level));
								Quests[Q_FARMER]._qactive = farmer;
								Quests[Q_GRAVE]._qactive = grave;
								EXPECT_EQ(IsWarpOpen(DTYPE_CATACOMBS), !spawn && (mp || (bits & 1) != 0 || (hf && level >= 10)));
								EXPECT_EQ(IsWarpOpen(DTYPE_CAVES), !spawn && (mp || (bits & 2) != 0 || (hf && level >= 15)));
								EXPECT_EQ(IsWarpOpen(DTYPE_HELL), !spawn && (mp || (bits & 4) != 0 || (hf && level >= 20)));
								EXPECT_EQ(IsWarpOpen(DTYPE_NEST), !spawn && hf && (farmer == QUEST_DONE || farmer == QUEST_HIVE_DONE));
								EXPECT_EQ(IsWarpOpen(DTYPE_CRYPT), !spawn && (mp || (hf && grave == QUEST_DONE)));
							}
}

/** Explicit private original-data export; never opens a game window or writes into an archive. */
TEST(NativeSceneArtTest, CaptureOriginalTownAndCathedral)
{
	const char *archivePath = std::getenv("GODEVILUTION_NATIVE_SCENE_ARCHIVE");
	const char *directory = std::getenv("GODEVILUTION_NATIVE_SCENE_REFERENCE_DIRECTORY");
	if (archivePath == nullptr || directory == nullptr)
		GTEST_SKIP() << "Explicit archive and new private reference directory are required.";
	const auto root = std::filesystem::absolute(directory).lexically_normal();
	ASSERT_FALSE(std::filesystem::exists(root));
	std::unique_ptr<mpqfs_archive_t, decltype(&mpqfs_close)> archive(mpqfs_open(archivePath), mpqfs_close);
	ASSERT_NE(archive, nullptr) << mpqfs_last_error();
	for (const std::string &base : { std::string("levels/towndata/town"), std::string("levels/l1data/l1") }) {
		for (const char *ext : { ".cel", ".min", ".til", ".sol", "s.cel" })
			Extract(archive.get(), root, base + ext);
	}
	for (const char *name : { "levels/towndata/sector1s.dun", "levels/towndata/sector2s.dun", "levels/towndata/sector3s.dun",
	         "levels/towndata/sector4s.dun", "levels/towndata/town.pal", "levels/l1data/l1_1.pal" })
		Extract(archive.get(), root, name);
	ASSERT_FALSE(::testing::Test::HasFatalFailure());
	// Automap is a bundled production asset, absent from the original archive.
	const auto repository = std::filesystem::absolute(__FILE__).parent_path().parent_path();
	std::filesystem::copy_file(repository / "assets/levels/towndata/automap.dun", root / "levels/towndata/automap.dun");
	// Headless typed optional-load fallback reports an empty successful value.
	// Supply the preferred Town paths explicitly with the same original bytes;
	// the exported source tree still identifies the archive paths above.
	std::filesystem::create_directories(root / "nlevels/towndata");
	for (const char *ext : { ".min", ".sol" })
		std::filesystem::copy_file(root / (std::string("levels/towndata/town") + ext),
		    root / (std::string("nlevels/towndata/town") + ext));
	NativeSceneGuard guard;
	paths::SetAssetsPath(root.string() + "/");
	OverridePaths = { root.string() + "/" };
	// Verify the real asset loader sees exactly the private exported bytes.
	for (const auto &file : std::filesystem::recursive_directory_iterator(root)) {
		if (!file.is_regular_file()) continue;
		std::ifstream input(file.path(), std::ios::binary);
		const std::vector<uint8_t> expected(std::istreambuf_iterator<char>(input), {});
		size_t size = 0;
		const auto relative = std::filesystem::relative(file.path(), root).generic_string();
		auto loaded = LoadFileInMem<uint8_t>(relative, &size);
		ASSERT_EQ(size, expected.size()) << relative;
		ASSERT_TRUE(std::equal(expected.begin(), expected.end(), loaded.get())) << relative;
	}
	CaptureTown(root);
	CaptureArt(root, "Town", "levels/towndata/town", 16);
	CaptureArt(root, "Cathedral", "levels/l1data/l1", 10);
}

} // namespace
} // namespace devilution
