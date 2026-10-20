#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <string>
#include <sstream>
#include <vector>

#include <mpqfs/mpqfs.h>

#include "engine/gfx/surface.hpp"
#include "engine/render/clx_render.hpp"
#include "tables/playerdat.hpp"
#include "utils/image/cl2_to_clx.hpp"
#include "utils/paths.h"

namespace devilution {
namespace {

void WriteNumber(std::ostream &out, uint32_t value)
{
	for (int shift = 0; shift < 32; shift += 8)
		out.put(static_cast<char>(value >> shift));
}

// Two different backgrounds distinguish untouched transparent pixels from
// every opaque palette index, including zero, using the real native blitter.
void WriteNativePixels(std::ostream &out, ClxSprite sprite)
{
	// Production permits opaque CLX commands to spill right of the nominal
	// width. Keep the full unoccluded footprint without tight-pitch row wrapping.
	constexpr int MaximumOpaqueSpill = 64;
	const int canvasWidth = sprite.width() + MaximumOpaqueSpill;
	OwnedSurface first(canvasWidth, sprite.height());
	OwnedSurface second(canvasWidth, sprite.height());
	SDL_FillRect(first.surface, nullptr, 0xAB);
	SDL_FillRect(second.surface, nullptr, 0xCD);
	ClxDraw(first, { 0, static_cast<int>(sprite.height()) - 1 }, sprite);
	ClxDraw(second, { 0, static_cast<int>(sprite.height()) - 1 }, sprite);
	int footprintWidth = sprite.width();
	for (int y = 0; y < sprite.height(); ++y) {
		for (int x = sprite.width(); x < canvasWidth; ++x) {
			if (*first.at(x, y) == *second.at(x, y))
				footprintWidth = std::max(footprintWidth, x + 1);
		}
	}
	WriteNumber(out, footprintWidth);
	WriteNumber(out, sprite.height());
	for (int y = 0; y < sprite.height(); ++y) {
		for (int x = 0; x < footprintWidth; ++x) {
			const bool opaque = *first.at(x, y) == *second.at(x, y);
			out.put(static_cast<char>(opaque ? *first.at(x, y) : 0));
			out.put(static_cast<char>(opaque ? 255 : 0));
		}
	}
}

std::vector<uint8_t> ReadArchiveFile(mpqfs_archive_t *archive, const std::string &name)
{
	const auto size = mpqfs_file_size(archive, name.c_str());
	if (!mpqfs_has_file(archive, name.c_str()))
		return {};
	if (size == 0 || size > 4 * 1024 * 1024) {
		ADD_FAILURE() << "Invalid original-art input size: " << name;
		return {};
	}
	std::vector<uint8_t> result(size);
	if (mpqfs_read_file_into(archive, name.c_str(), result.data(), size) != size) {
		ADD_FAILURE() << "Original-art archive read failed: " << name;
		return {};
	}
	return result;
}

void WriteBytes(const std::filesystem::path &path, const std::vector<uint8_t> &bytes)
{
	std::filesystem::create_directories(path.parent_path());
	std::ofstream out(path, std::ios::binary);
	out.write(reinterpret_cast<const char *>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
	out.flush();
	out.close();
	ASSERT_TRUE(out.good()) << path;
}

TEST(NativePlayerArtTest, NativeBlitterPreservesOpaqueZeroAndTransparentRuns)
{
	// One 2x2 bottom-up CL2 frame: opaque zero, skip one, then 2 opaque pixels.
	const std::vector<uint8_t> source { 1, 0, 0, 0, 12, 0, 0, 0, 24, 0, 0, 0,
		6, 0, 0, 0, 0, 0, 255, 0, 1, 254, 7, 8 };
	std::vector<uint8_t> converted;
	EXPECT_EQ(Cl2ToClx(source.data(), source.size(), PointerOrValue<uint16_t>(2), converted), 0);
	ClxSpriteList list(converted.data());
	ASSERT_EQ(list.numSprites(), 1);
	std::ostringstream pixels(std::ios::out | std::ios::binary);
	WriteNativePixels(pixels, list[0]);
	const std::string expected { '\2', '\0', '\0', '\0', '\2', '\0', '\0', '\0',
		'\7', static_cast<char>(255), '\10', static_cast<char>(255), '\0', static_cast<char>(255), '\0', '\0' };
	EXPECT_EQ(pixels.str(), expected);
}

TEST(NativePlayerArtTest, RetainsNativeMergedOpaqueSpillAndNextRowHole)
{
	// Adjacent one-pixel literals straddle a nominal 3-pixel row boundary.
	// Cl2ToClx merges them; production writes both on the lower row.
	const std::vector<uint8_t> source { 1, 0, 0, 0, 12, 0, 0, 0, 24, 0, 0, 0,
		6, 0, 0, 0, 0, 0, 2, 255, 185, 255, 185, 2 };
	std::vector<uint8_t> converted;
	EXPECT_EQ(Cl2ToClx(source.data(), source.size(), PointerOrValue<uint16_t>(3), converted), 0);
	ClxSpriteList list(converted.data());
	std::ostringstream pixels(std::ios::out | std::ios::binary);
	WriteNativePixels(pixels, list[0]);
	const std::string expected { '\4', '\0', '\0', '\0', '\2', '\0', '\0', '\0',
		'\0', '\0', '\0', '\0', '\0', '\0', '\0', '\0',
		'\0', '\0', '\0', '\0', static_cast<char>(185), static_cast<char>(255), static_cast<char>(185), static_cast<char>(255) };
	EXPECT_EQ(pixels.str(), expected);
}

/** Opt-in private reference export; no original art is checked into the repository. */
TEST(NativePlayerArtTest, CaptureOriginalBaseClassClips)
{
	const char *archivePath = std::getenv("GODEVILUTION_NATIVE_PLAYER_ART_ARCHIVE");
	const char *directory = std::getenv("GODEVILUTION_NATIVE_PLAYER_ART_REFERENCE_DIRECTORY");
	if (archivePath == nullptr || directory == nullptr)
		GTEST_SKIP() << "Explicit original archive and new private output directory are required.";
	const auto root = std::filesystem::absolute(directory).lexically_normal();
	ASSERT_FALSE(std::filesystem::exists(root)) << "Reference export must not overwrite existing files.";
	std::unique_ptr<mpqfs_archive_t, decltype(&mpqfs_close)> archive(mpqfs_open(archivePath), mpqfs_close);
	ASSERT_NE(archive, nullptr) << mpqfs_last_error();
	const auto repository = std::filesystem::absolute(__FILE__).parent_path().parent_path();
	const auto oldAssets = paths::AssetsPath();
	paths::SetAssetsPath((repository / "assets").string() + "/");
	LoadPlayerDataFiles();
	paths::SetAssetsPath(oldAssets);
	std::filesystem::create_directories(root);
	for (const char *palette : { "levels/towndata/town.pal", "levels/l1data/l1_1.pal" }) {
		auto bytes = ReadArchiveFile(archive.get(), palette);
		ASSERT_EQ(bytes.size(), 768) << palette;
		WriteBytes(root / palette, bytes);
	}
	const std::array<char, 3> armors { 'l', 'm', 'h' };
	const std::array<char, 9> weapons { 'n', 'u', 's', 'd', 'b', 'a', 'm', 'h', 't' };
	const std::array<const char *, 11> suffixes { "st", "wl", "as", "aw", "at", "ht", "bl", "lm", "fm", "qm", "dt" };
	std::ofstream inventory(root / "native-reference.tsv", std::ios::binary);
	inventory << "class_id\tpath\twidth\tdirections\tframes\r\n";
	size_t captured = 0;
	for (uint32_t classId = 0; classId < 3; ++classId) {
		const auto &data = GetPlayerSpriteDataForClass(static_cast<HeroClass>(classId));
		const std::string classTrnPath = "plrgfx/" + data.trn + ".trn";
		auto classTrn = ReadArchiveFile(archive.get(), classTrnPath);
		if (!classTrn.empty()) {
			ASSERT_EQ(classTrn.size(), 256);
			WriteBytes(root / classTrnPath, classTrn);
		}
		for (char armor : armors) {
			for (size_t weapon = 0; weapon < weapons.size(); ++weapon) {
				for (size_t graphic = 0; graphic < suffixes.size(); ++graphic) {
					const std::string prefix { data.classChar, armor, weapons[weapon] };
					const auto base = "plrgfx/" + data.classPath + "/" + prefix + "/" + prefix + suffixes[graphic];
					auto bytes = ReadArchiveFile(archive.get(), base + ".cl2");
					if (bytes.empty())
						continue;
					const std::array<uint16_t, 11> widths { data.stand, data.walk, data.stand, data.walk,
						weapon == 4 ? data.bow : data.attack, data.swHit, data.block,
						data.lightning, data.fire, data.magic, data.death };
					std::vector<uint8_t> clx;
					const auto groups = Cl2ToClx(bytes.data(), bytes.size(), PointerOrValue<uint16_t>(widths[graphic]), clx);
					ASSERT_EQ(groups, 8) << base;
					ClxSpriteSheet sheet(clx.data(), groups);
					auto graphicTrn = ReadArchiveFile(archive.get(), base + ".trn");
					const auto overridePath = repository / "assets" / (base + ".trn");
					if (std::filesystem::exists(overridePath)) {
						std::ifstream input(overridePath, std::ios::binary);
						graphicTrn.assign(std::istreambuf_iterator<char>(input), {});
					}
					if (!graphicTrn.empty()) {
						ASSERT_EQ(graphicTrn.size(), 256);
						ClxApplyTrans(sheet, graphicTrn.data());
						WriteBytes(root / (base + ".trn"), graphicTrn);
					}
					if (!classTrn.empty())
						ClxApplyTrans(sheet, classTrn.data());
					WriteBytes(root / (base + ".cl2"), bytes);
					std::ofstream reference(root / (base + ".cl2.native.bin"), std::ios::binary);
					WriteNumber(reference, groups);
					for (auto list : sheet) {
						WriteNumber(reference, list.numSprites());
						for (auto sprite : list)
							WriteNativePixels(reference, sprite);
					}
					reference.flush();
					reference.close();
					ASSERT_TRUE(reference.good());
					inventory << classId << '\t' << base << ".cl2\t" << widths[graphic] << '\t' << groups << '\t' << sheet[0].numSprites() << "\r\n";
					++captured;
				}
			}
		}
	}
	EXPECT_EQ(captured, 765);
	// Require the playable starter families in both contexts, plus unarmed
	// death. Other available equipment variants are all exported above.
	for (const auto &[classPath, prefix] : std::array<std::pair<const char *, const char *>, 3> {
		     { { "warrior", "wld" }, { "rogue", "rlb" }, { "sorceror", "slt" } } }) {
		for (const char *suffix : { "st", "wl", "as", "aw", "at", "ht", "lm", "fm", "qm" }) {
			const auto base = std::string("plrgfx/") + classPath + "/" + prefix + "/" + prefix + suffix;
			EXPECT_TRUE(std::filesystem::exists(root / (base + ".cl2.native.bin"))) << base;
		}
		const std::string deadPrefix { prefix[0], prefix[1], 'n' };
		EXPECT_TRUE(std::filesystem::exists(root / (std::string("plrgfx/") + classPath + "/" + deadPrefix + "/" + deadPrefix + "dt.cl2.native.bin")));
	}
	inventory.flush();
	inventory.close();
	EXPECT_TRUE(inventory.good());
}

} // namespace
} // namespace devilution
