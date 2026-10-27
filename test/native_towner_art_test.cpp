#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <memory>
#include <optional>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

#include <mpqfs/mpqfs.h>

#include "engine/assets.hpp"
#include "engine/gfx/surface.hpp"
#include "engine/load/load_cel.hpp"
#include "engine/render/clx_render.hpp"
#include "tables/townerdat.hpp"
#include "utils/image/cel_to_clx.hpp"
#include "utils/paths.h"
#include "utils/sdl/sdl_compat.h"

namespace devilution {
namespace {

void WriteNumber(std::ostream &out, uint32_t value)
{
	for (int shift = 0; shift < 32; shift += 8)
		out.put(static_cast<char>(value >> shift));
}

std::vector<uint8_t> ReadArchiveFile(mpqfs_archive_t *archive, const std::string &name)
{
	if (!mpqfs_has_file(archive, name.c_str()))
		return {};
	const auto size = mpqfs_file_size(archive, name.c_str());
	if (size == 0 || size > 64 * 1024 * 1024) {
		ADD_FAILURE() << "Invalid original Town asset size: " << name;
		return {};
	}
	std::vector<uint8_t> result(size);
	if (mpqfs_read_file_into(archive, name.c_str(), result.data(), size) != size) {
		ADD_FAILURE() << "Original Town archive read failed: " << name;
		return {};
	}
	return result;
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

std::string ArchivePath(std::string path)
{
	std::replace(path.begin(), path.end(), '\\', '/');
	return path;
}

// Distinct backgrounds identify opacity through the actual native CLX blitter,
// including opaque palette index zero.
void WriteNativePixels(std::ostream &out, ClxSprite sprite)
{
	constexpr int MaximumRightSpill = 64;
	const int canvasWidth = static_cast<int>(sprite.width()) + MaximumRightSpill;
	const int height = sprite.height();
	OwnedSurface first(canvasWidth, height);
	OwnedSurface second(canvasWidth, height);
	SDL_FillSurfaceRect(first.surface, nullptr, 0xAB);
	SDL_FillSurfaceRect(second.surface, nullptr, 0xCD);
	ClxDraw(first, { 0, height - 1 }, sprite);
	ClxDraw(second, { 0, height - 1 }, sprite);
	int footprintWidth = sprite.width();
	for (int y = 0; y < height; ++y) {
		for (int x = sprite.width(); x < canvasWidth; ++x) {
			if (*first.at(x, y) == *second.at(x, y))
				footprintWidth = std::max(footprintWidth, x + 1);
		}
	}
	WriteNumber(out, footprintWidth);
	WriteNumber(out, height);
	for (int y = 0; y < height; ++y) {
		for (int x = 0; x < footprintWidth; ++x) {
			const bool opaque = *first.at(x, y) == *second.at(x, y);
			out.put(static_cast<char>(opaque ? *first.at(x, y) : 0));
			out.put(static_cast<char>(opaque ? 255 : 0));
		}
	}
}

void WriteNativeGroup(std::ostream &out, ClxSpriteList sprites)
{
	WriteNumber(out, sprites.numSprites());
	for (ClxSprite sprite : sprites)
		WriteNativePixels(out, sprite);
}

struct NativeTownerAssetContext {
	NativeTownerAssetContext()
	    : assetsPath(paths::AssetsPath())
	    , overridePaths(OverridePaths)
	{
		const auto repository = std::filesystem::absolute(__FILE__).parent_path().parent_path();
		paths::SetAssetsPath((repository / "assets").string() + "/");
		OverridePaths = { (repository / "assets").string() + "/" };
	}

	~NativeTownerAssetContext()
	{
		OverridePaths = std::move(overridePaths);
		paths::SetAssetsPath(assetsPath);
	}

	std::string assetsPath;
	std::vector<std::string> overridePaths;
};

// Prepare native frame/counter/order cursors without adopting gossip RNG or
// cow initialization phase. ProcessTowners itself supplies every delay edge.
void CapturePreparedTimeline(const std::filesystem::path &outputRoot,
    const std::vector<std::pair<int, TownerDataEntry>> &selected)
{
	struct RestoreTowners {
		std::vector<Towner> previous;
		RestoreTowners() { previous.swap(Towners); }
		~RestoreTowners() { previous.swap(Towners); }
	} restore;
	Towners.reserve(selected.size());
	size_t maximumTick = 0;
	for (const auto &[spriteId, entry] : selected) {
		auto &towner = Towners.emplace_back();
		towner._ttype = entry.type;
		towner._tAnimWidth = entry.animWidth;
		towner._tAnimDelay = spriteId == 8 ? 3 : entry.animDelay;
		towner._tAnimLen = spriteId == 8 ? 12 : entry.animFrames;
		towner._tAnimCnt = 0;
		towner._tAnimFrame = 0;
		towner._tAnimFrameCnt = 0;
		towner.animOrder = entry.animOrder;
		ASSERT_GT(towner._tAnimDelay, 0);
		ASSERT_GT(towner._tAnimLen, 0);
		const size_t cycle = towner.animOrder.empty() ? towner._tAnimLen : towner.animOrder.size();
		maximumTick = std::max(maximumTick, (cycle + 1) * static_cast<size_t>(towner._tAnimDelay));
	}
	ASSERT_LE(maximumTick, 4096U);
	std::ofstream timeline(outputRoot / "native-towner-timeline.tsv", std::ios::binary);
	timeline << "tick\tresident_slot\tsprite_id\tframe\tcounter\torder_cursor\r\n";
	for (size_t tick = 0; tick <= maximumTick; ++tick) {
		for (size_t slot = 0; slot < Towners.size(); ++slot) {
			const auto &towner = Towners[slot];
			timeline << tick << '\t' << slot << '\t' << selected[slot].first << '\t'
			         << static_cast<int>(towner._tAnimFrame) << '\t' << towner._tAnimCnt << '\t'
			         << static_cast<int>(towner._tAnimFrameCnt) << "\r\n";
		}
		if (tick < maximumTick) {
			ProcessTowners();
			if (tick == 0)
				ASSERT_EQ(Towners.front()._tAnimCnt, 1) << "Prepared oracle requires disconnected legacy processing.";
		}
	}
	timeline.flush();
	timeline.close();
	ASSERT_TRUE(timeline.good());
}

TEST(NativeTownerArtTest, NativeCelBlitterPreservesOpaqueZeroAndTransparentRuns)
{
	// One two-pixel-high CEL frame with opaque zero and a transparent run.
	const std::vector<uint8_t> cel { 1, 0, 0, 0, 12, 0, 0, 0, 18, 0, 0, 0,
		2, 0, 1, 0xFF, 1, 2 };
	auto converted = CelToClx(cel.data(), cel.size(), PointerOrValue<uint16_t>(2));
	OwnedClxSpriteList ownedSprites = std::move(converted).list();
	ClxSpriteList sprites(ownedSprites);
	ASSERT_EQ(sprites.numSprites(), 1);
	std::ostringstream output(std::ios::out | std::ios::binary);
	WriteNativePixels(output, sprites[0]);
	const std::string expected { '\2', '\0', '\0', '\0', '\2', '\0', '\0', '\0',
		'\0', '\0', '\2', static_cast<char>(255), '\0', static_cast<char>(255), '\1', static_cast<char>(255) };
	EXPECT_EQ(output.str(), expected);
}

/** Opt-in export for source and native-rendered pixels; original art stays outside the repository. */
TEST(NativeTownerArtTest, CaptureOriginalTownResidentFrames)
{
	const char *archivePath = std::getenv("GODEVILUTION_NATIVE_TOWNER_ART_ARCHIVE");
	const char *outputValue = std::getenv("GODEVILUTION_NATIVE_TOWNER_ART_REFERENCE_DIRECTORY");
	if (archivePath == nullptr || outputValue == nullptr)
		GTEST_SKIP() << "Explicit original Diablo archive and new private reference directory are required.";
	const auto outputRoot = std::filesystem::absolute(outputValue).lexically_normal();
	ASSERT_FALSE(std::filesystem::exists(outputRoot)) << "Native Town reference export must not overwrite an existing directory.";
	std::unique_ptr<mpqfs_archive_t, decltype(&mpqfs_close)> archive(mpqfs_open(archivePath), mpqfs_close);
	ASSERT_NE(archive, nullptr) << mpqfs_last_error();

	NativeTownerAssetContext context;
	LoadTownerData();
	std::vector<std::pair<int, TownerDataEntry>> selected;
	for (const auto &entry : TownersDataEntries) {
		int spriteId = -1;
		switch (entry.type) {
		case TOWN_SMITH: spriteId = 0; break;
		case TOWN_HEALER: spriteId = 1; break;
		case TOWN_TAVERN: spriteId = 2; break;
		case TOWN_STORY: spriteId = 3; break;
		case TOWN_DRUNK: spriteId = 4; break;
		case TOWN_WITCH: spriteId = 5; break;
		case TOWN_BMAID: spriteId = 6; break;
		case TOWN_PEGBOY: spriteId = 7; break;
		case TOWN_COW: spriteId = 8; break;
		default: continue;
		}
		selected.emplace_back(spriteId, entry);
	}
	ASSERT_EQ(selected.size(), 11);
	std::stable_sort(selected.begin(), selected.end(), [](const auto &left, const auto &right) {
		return left.first < right.first;
	});

	const auto sourceRoot = outputRoot / "source";
	const auto sourcePalette = ReadArchiveFile(archive.get(), "levels/towndata/town.pal");
	ASSERT_EQ(sourcePalette.size(), 768);
	WriteBytes(sourceRoot / "levels/towndata/town.pal", sourcePalette);

	std::vector<std::pair<int, std::string>> spritePaths;
	for (const auto &[spriteId, entry] : selected) {
		if (spriteId == 8)
			continue;
		const auto archivePathForCel = ArchivePath(entry.animPath) + ".cel";
		auto sourceBytes = ReadArchiveFile(archive.get(), archivePathForCel);
		ASSERT_FALSE(sourceBytes.empty()) << archivePathForCel;
		WriteBytes(sourceRoot / archivePathForCel, sourceBytes);
		spritePaths.emplace_back(spriteId, archivePathForCel);
	}
	const std::string cowPath = "towners/animals/cow.cel";
	auto cowBytes = ReadArchiveFile(archive.get(), cowPath);
	ASSERT_FALSE(cowBytes.empty()) << cowPath;
	WriteBytes(sourceRoot / cowPath, cowBytes);
	spritePaths.emplace_back(8, cowPath);
	std::sort(spritePaths.begin(), spritePaths.end());
	ASSERT_EQ(spritePaths.size(), 9);

	OverridePaths = { sourceRoot.string() + "/" };
	const auto referencePath = outputRoot / "native-towner-reference.bin";
	std::ofstream reference(referencePath, std::ios::binary);
	ASSERT_TRUE(reference.good());
	WriteNumber(reference, 1);
	WriteNumber(reference, static_cast<uint32_t>(spritePaths.size()));
	std::ofstream inventory(outputRoot / "native-towner-reference.tsv", std::ios::binary);
	inventory << "sprite_id\tpath\twidth\tgroup_count\tframes_per_group\r\n";

	for (const auto &[spriteId, relativePath] : spritePaths) {
		const auto sourceRecord = std::find_if(selected.begin(), selected.end(), [spriteId](const auto &item) {
			return item.first == spriteId;
		});
		ASSERT_NE(sourceRecord, selected.end());
		const uint16_t width = sourceRecord->second.animWidth;
		std::vector<ClxSpriteList> groups;
		std::vector<OwnedClxSpriteListOrSheet> ownedSprites;
		if (spriteId == 8) {
			ownedSprites.emplace_back(std::move(LoadCelSheet("towners\\animals\\cow", 128)));
			ClxSpriteSheet sheet = ownedSprites.back().sheet();
			groups.clear();
			ASSERT_EQ(sheet.numLists(), 8);
			for (size_t direction = 0; direction < sheet.numLists(); ++direction)
				groups.push_back(sheet[direction]);
		} else {
			ownedSprites.emplace_back(std::move(LoadCel(sourceRecord->second.animPath.c_str(), width)));
			groups.emplace_back(ownedSprites.back().list());
		}
		ASSERT_EQ(groups.size(), spriteId == 8 ? 8U : 1U) << relativePath;
		const auto frameCount = groups.front().numSprites();
		ASSERT_GT(frameCount, 0U) << relativePath;
		for (const auto &group : groups)
			ASSERT_EQ(group.numSprites(), frameCount) << relativePath;
		WriteNumber(reference, static_cast<uint32_t>(spriteId));
		WriteNumber(reference, static_cast<uint32_t>(groups.size()));
		WriteNumber(reference, frameCount);
		for (const auto &group : groups)
			WriteNativeGroup(reference, group);
		inventory << spriteId << '\t' << relativePath << '\t' << width << '\t' << groups.size() << '\t' << frameCount << "\r\n";
	}
	reference.flush();
	ASSERT_TRUE(reference.good());
	const auto referenceLength = std::filesystem::file_size(referencePath);
	reference.close();
	ASSERT_TRUE(reference.good());
	inventory.flush();
	inventory.close();
	ASSERT_TRUE(inventory.good());
	EXPECT_GT(referenceLength, 8U);
	CapturePreparedTimeline(outputRoot, selected);
}

} // namespace
} // namespace devilution
