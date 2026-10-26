#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <functional>
#include <map>
#include <memory>
#include <optional>
#include <sstream>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#include <mpqfs/mpqfs.h>

#include "engine/assets.hpp"
#include "engine/gfx/surface.hpp"
#include "engine/lighting.h"
#include "engine/load/load_cel.hpp"
#include "engine/random.hpp"
#include "engine/render/clx_render.hpp"
#include "engine/render/world_renderer.h"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/objects/objects.hpp"
#include "game/players/players.hpp"
#include "network/protocol/msg.h"
#include "tables/objdat.h"
#include "utils/paths.h"

namespace devilution {
namespace {

struct NativeObjectSpec {
	uint32_t nativeObjectId;
	const char *stem;
};

constexpr std::array<NativeObjectSpec, 10> NativeObjects { {
	{ 1, "l1doors" },
	{ 2, "l1doors" },
	{ 5, "chest1" },
	{ 6, "chest2" },
	{ 7, "chest3" },
	{ 48, "sarc" },
	{ 53, "traphole" },
	{ 54, "traphole" },
	{ 57, "barrel" },
	{ 58, "barrelex" },
} };

constexpr std::array<const char *, 8> NativeObjectStems {
	"l1doors", "chest1", "chest2", "chest3", "sarc", "traphole", "barrel", "barrelex"
};

constexpr int MaximumArchiveFileSize = 64 * 1024 * 1024;
constexpr int MaximumOpaqueRightSpill = 256;
constexpr int CanvasWidth = 1024;
constexpr uint8_t FirstBackground = 0xAB;
constexpr uint8_t SecondBackground = 0xCD;

struct NativeObjectArt {
	const char *stem;
	uint16_t animWidth = 0;
	std::optional<OwnedClxSpriteList> sprites;
};

struct OpaqueBounds {
	int left;
	int top;
	int right;
	int bottom;
	bool hasPixels;
};

std::vector<uint8_t> ReadArchiveFile(mpqfs_archive_t *archive, const std::string &name)
{
	if (!mpqfs_has_file(archive, name.c_str())) {
		ADD_FAILURE() << "Original object-art archive is missing " << name;
		return {};
	}
	const size_t size = mpqfs_file_size(archive, name.c_str());
	if (size == 0 || size > MaximumArchiveFileSize) {
		ADD_FAILURE() << "Invalid original object-art input size for " << name << ": " << size;
		return {};
	}
	std::vector<uint8_t> bytes(size);
	if (mpqfs_read_file_into(archive, name.c_str(), bytes.data(), size) != size) {
		ADD_FAILURE() << "Original object-art archive read failed for " << name;
		return {};
	}
	return bytes;
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

void WriteNumber(std::ostream &output, uint32_t value)
{
	for (int shift = 0; shift < 32; shift += 8)
		output.put(static_cast<char>(value >> shift));
}

void WriteString(std::ostream &output, std::string_view value)
{
	ASSERT_LE(value.size(), static_cast<size_t>(UINT32_MAX));
	WriteNumber(output, static_cast<uint32_t>(value.size()));
	output.write(value.data(), static_cast<std::streamsize>(value.size()));
}

OpaqueBounds FindOpaqueBounds(const Surface &first, const Surface &second)
{
	OpaqueBounds result { first.w(), first.h(), 0, 0, false };
	for (int y = 0; y < first.h(); ++y) {
		for (int x = 0; x < first.w(); ++x) {
			if (*first.at(x, y) != *second.at(x, y))
				continue;
			result.left = std::min(result.left, x);
			result.top = std::min(result.top, y);
			result.right = std::max(result.right, x + 1);
			result.bottom = std::max(result.bottom, y + 1);
			result.hasPixels = true;
		}
	}
	return result;
}

bool SamePixels(const Surface &left, const Surface &right)
{
	if (left.w() != right.w() || left.h() != right.h())
		return false;
	for (int y = 0; y < left.h(); ++y) {
		for (int x = 0; x < left.w(); ++x) {
			if (*left.at(x, y) != *right.at(x, y))
				return false;
		}
	}
	return true;
}

// DVLNOAR1 binary schema: eight-byte magic, LE-u32 asset count, then for each
// unique stem a LE-u32 UTF-8 name length/name and LE-u32 actual loaded frame
// count. Each 1-based frame stores LE-u32 frame number, native CLX width,
// spill-aware footprint width, height, then row-major (palette index, opacity)
// byte pairs. Two distinct backgrounds plus ClxDraw preserve opaque index 0.
void WriteNativeFrame(std::ostream &output, ClxSprite sprite, uint32_t frameNumber)
{
	const int nativeWidth = sprite.width();
	const int height = sprite.height();
	ASSERT_GT(nativeWidth, 0);
	ASSERT_GT(height, 0);
	const int canvasWidth = nativeWidth + MaximumOpaqueRightSpill;
	OwnedSurface first(canvasWidth, height);
	OwnedSurface second(canvasWidth, height);
	ASSERT_EQ(SDL_FillRect(first.surface, nullptr, FirstBackground), 0);
	ASSERT_EQ(SDL_FillRect(second.surface, nullptr, SecondBackground), 0);
	ClxDraw(first, { 0, height - 1 }, sprite);
	ClxDraw(second, { 0, height - 1 }, sprite);

	int footprintWidth = nativeWidth;
	for (int y = 0; y < height; ++y) {
		for (int x = nativeWidth; x < canvasWidth; ++x) {
			if (*first.at(x, y) == *second.at(x, y))
				footprintWidth = std::max(footprintWidth, x + 1);
		}
	}
	for (int y = 0; y < height; ++y)
		EXPECT_NE(*first.at(canvasWidth - 1, y), *second.at(canvasWidth - 1, y)) << "Native object frame exceeds the bounded right-spill capture canvas.";

	WriteNumber(output, frameNumber);
	WriteNumber(output, static_cast<uint32_t>(nativeWidth));
	WriteNumber(output, static_cast<uint32_t>(footprintWidth));
	WriteNumber(output, static_cast<uint32_t>(height));
	for (int y = 0; y < height; ++y) {
		for (int x = 0; x < footprintWidth; ++x) {
			const bool opaque = *first.at(x, y) == *second.at(x, y);
			output.put(static_cast<char>(opaque ? *first.at(x, y) : 0));
			output.put(static_cast<char>(opaque ? 255 : 0));
		}
	}
}

class NativeObjectAssetContext {
public:
	NativeObjectAssetContext(const std::filesystem::path &repositoryAssets, const std::filesystem::path &extractedSource)
	    : previousAssetsPath_(paths::AssetsPath())
	    , previousOverrides_(std::move(OverridePaths))
	    , previousArchives_(std::move(MpqArchives))
	    , previousObjects_(std::move(AllObjects))
	    , previousObjectFiles_(std::move(ObjMasterLoadList))
	{
		MpqArchives.clear();
		OverridePaths = { repositoryAssets.string() + "/" };
		paths::SetAssetsPath(repositoryAssets.string() + "/");
		LoadObjectData();
		OverridePaths = { extractedSource.string() + "/" };
		paths::SetAssetsPath("");
	}

	~NativeObjectAssetContext()
	{
		AllObjects = std::move(previousObjects_);
		ObjMasterLoadList = std::move(previousObjectFiles_);
		MpqArchives = std::move(previousArchives_);
		OverridePaths = std::move(previousOverrides_);
		paths::SetAssetsPath(previousAssetsPath_);
	}

	NativeObjectAssetContext(const NativeObjectAssetContext &) = delete;
	NativeObjectAssetContext &operator=(const NativeObjectAssetContext &) = delete;

private:
	std::string previousAssetsPath_;
	std::vector<std::string> previousOverrides_;
	std::map<int, MpqArchiveT, std::greater<>> previousArchives_;
	std::vector<ObjectData> previousObjects_;
	std::vector<std::string> previousObjectFiles_;
};

NativeObjectArt &FindArt(std::array<NativeObjectArt, NativeObjectStems.size()> &assets, std::string_view stem)
{
	const auto found = std::find_if(assets.begin(), assets.end(), [stem](const NativeObjectArt &asset) { return stem == asset.stem; });
	EXPECT_NE(found, assets.end()) << "Missing native object art stem " << stem;
	return *found;
}

const NativeObjectArt &FindArt(const std::array<NativeObjectArt, NativeObjectStems.size()> &assets, std::string_view stem)
{
	const auto found = std::find_if(assets.begin(), assets.end(), [stem](const NativeObjectArt &asset) { return stem == asset.stem; });
	EXPECT_NE(found, assets.end()) << "Missing native object art stem " << stem;
	return *found;
}

Object MakeNativeObject(const NativeObjectSpec &spec, const NativeObjectArt &asset, uint32_t frame, Point owner)
{
	const ObjectData &data = AllObjects.at(spec.nativeObjectId);
	Object object {};
	object._otype = static_cast<_object_id>(spec.nativeObjectId);
	object.position = owner;
	object._oAnimData.emplace(ClxSpriteList(*asset.sprites));
	object._oAnimFlag = data.isAnimated();
	object._oAnimDelay = data.animDelay;
	object._oAnimLen = data.animLen;
	object._oAnimFrame = frame;
	object._oAnimWidth = data.animWidth;
	object._oBreak = data.isBreakable() ? 1 : 0;
	object.selectionRegion = data.selectionRegion;
	object.applyLighting = false;
	return object;
}

void FillBackgrounds(OwnedSurface &first, OwnedSurface &second)
{
	ASSERT_EQ(SDL_FillRect(first.surface, nullptr, FirstBackground), 0);
	ASSERT_EQ(SDL_FillRect(second.surface, nullptr, SecondBackground), 0);
}

void RenderProbe(
    std::ostream &output,
    const NativeObjectSpec &spec,
    std::string_view state,
    std::string_view probe,
    const Object &object,
    Point tilePosition)
{
	ASSERT_TRUE(object._oAnimData);
	const ClxSprite sprite = object.currentSprite();
	const Point targetBufferPosition { 256, static_cast<int>(sprite.height()) + 32 };
	const int canvasHeight = static_cast<int>(sprite.height()) + 64;
	OwnedSurface actualFirst(CanvasWidth, canvasHeight);
	OwnedSurface actualSecond(CanvasWidth, canvasHeight);
	OwnedSurface expectedFirst(CanvasWidth, canvasHeight);
	OwnedSurface expectedSecond(CanvasWidth, canvasHeight);
	FillBackgrounds(actualFirst, actualSecond);
	FillBackgrounds(expectedFirst, expectedSecond);

	const NativeObjectDrawObservationForTesting firstObservation = TestDrawObject(actualFirst, object, tilePosition, targetBufferPosition);
	const NativeObjectDrawObservationForTesting secondObservation = TestDrawObject(actualSecond, object, tilePosition, targetBufferPosition);
	EXPECT_EQ(firstObservation.bottomAnchor, secondObservation.bottomAnchor);
	EXPECT_EQ(firstObservation.spriteWidth, sprite.width());
	EXPECT_EQ(firstObservation.spriteHeight, sprite.height());
	EXPECT_EQ(firstObservation.frameIndex, object._oAnimFrame);
	ClxDraw(expectedFirst, firstObservation.bottomAnchor, sprite);
	ClxDraw(expectedSecond, secondObservation.bottomAnchor, sprite);
	EXPECT_TRUE(SamePixels(actualFirst, expectedFirst)) << "DrawObject disagrees with native ClxDraw for object " << spec.nativeObjectId << " state " << state;
	EXPECT_TRUE(SamePixels(actualSecond, expectedSecond)) << "DrawObject disagrees with native ClxDraw on the second background for object " << spec.nativeObjectId << " state " << state;

	const OpaqueBounds bounds = FindOpaqueBounds(actualFirst, actualSecond);
	EXPECT_LT(bounds.right, CanvasWidth) << "Object probe canvas clipped the native right spill.";
	EXPECT_LT(bounds.bottom, canvasHeight) << "Object probe canvas clipped the native bottom anchor.";
	const Point topLeft { firstObservation.bottomAnchor.x, firstObservation.bottomAnchor.y - static_cast<int>(sprite.height()) + 1 };
	output << spec.nativeObjectId << '\t' << state << '\t' << object._oAnimFrame << '\t' << probe << '\t'
	       << tilePosition.x - object.position.x << '\t' << tilePosition.y - object.position.y << '\t'
	       << targetBufferPosition.x << '\t' << targetBufferPosition.y << '\t'
	       << firstObservation.bottomAnchor.x << '\t' << firstObservation.bottomAnchor.y << '\t'
	       << topLeft.x << '\t' << topLeft.y << '\t' << sprite.width() << '\t' << sprite.height() << '\t';
	if (bounds.hasPixels) {
		output << bounds.left << '\t' << bounds.top << '\t' << bounds.right << '\t' << bounds.bottom;
	} else {
		output << "-1\t-1\t-1\t-1";
	}
	output << "\r\n";
}

void RenderAtBothAnchors(std::ostream &output, const NativeObjectSpec &spec, std::string_view state, const Object &object)
{
	RenderProbe(output, spec, state, "owner", object, object.position);
	RenderProbe(output, spec, state, "occupied-ne", object, object.position + Direction::NorthEast);
}

class RestoreRandomState {
public:
	RestoreRandomState()
	    : state_(GetLCGEngineState())
	{
	}
	~RestoreRandomState() { SetRndSeed(state_); }

private:
	uint32_t state_;
};

class RestoreDoorWorldState {
public:
	explicit RestoreDoorWorldState(Point owner)
	    : positions_ { owner, owner + Direction::NorthEast, owner + Direction::NorthWest }
	    , light_(CurrentLightManager)
	{
		for (size_t index = 0; index < positions_.size(); ++index) {
			oldTiles_[index] = tileAt(positions_[index]);
			tileAt(positions_[index]) = Tile {};
		}
	}
	~RestoreDoorWorldState()
	{
		for (size_t index = 0; index < positions_.size(); ++index)
			tileAt(positions_[index]) = oldTiles_[index];
		CurrentLightManager = light_;
	}

private:
	std::array<Point, 3> positions_;
	std::array<Tile, 3> oldTiles_ {};
	LightManager light_;
};

void WriteActionDrawRows(std::ostream &output, std::array<NativeObjectArt, NativeObjectStems.size()> &assets)
{
	constexpr Point Owner { 64, 64 };
	Player noLocalPlayer {};

	for (const NativeObjectSpec &spec : NativeObjects) {
		NativeObjectArt &asset = FindArt(assets, spec.stem);
		const ObjectData &data = AllObjects.at(spec.nativeObjectId);
		const uint32_t initialFrame = data.isAnimated() ? 1 : data.animDelay;
		ASSERT_GT(initialFrame, 0U);

		if (spec.nativeObjectId == 1 || spec.nativeObjectId == 2) {
			RestoreDoorWorldState restoreWorld(Owner);
			ASSERT_LE(initialFrame + 2, asset.sprites->numSprites()) << "Native door action frames exceed loaded CEL frames.";
			Object door = MakeNativeObject(spec, asset, initialFrame, Owner);
			door._oDoorFlag = true;
			door._oVar1 = 1;
			door._oVar2 = 1;
			door._oVar4 = 0; // Native DOOR_CLOSED.
			RenderAtBothAnchors(output, spec, "door-closed", door);

			SyncOpObject(noLocalPlayer, CMD_OPENDOOR, door);
			ASSERT_EQ(door._oVar4, 1) << "Native door open operation did not reach DOOR_OPEN.";
			ASSERT_EQ(door._oAnimFrame, initialFrame + 2);
			RenderAtBothAnchors(output, spec, "door-open", door);

			tileAt(Owner).setPlayer(1);
			SyncOpObject(noLocalPlayer, CMD_CLOSEDOOR, door);
			ASSERT_EQ(door._oVar4, 2) << "Occupied native door close must remain DOOR_BLOCKED.";
			ASSERT_EQ(door._oAnimFrame, initialFrame + 2);
			RenderAtBothAnchors(output, spec, "door-blocked-close-refused", door);

			tileAt(Owner).setPlayer(0);
			SyncOpObject(noLocalPlayer, CMD_CLOSEDOOR, door);
			ASSERT_EQ(door._oVar4, 0) << "Native door did not return to DOOR_CLOSED after the blocker left.";
			ASSERT_EQ(door._oAnimFrame, initialFrame);
			RenderAtBothAnchors(output, spec, "door-reclosed", door);
			continue;
		}

		if (spec.nativeObjectId >= 5 && spec.nativeObjectId <= 7) {
			for (const uint32_t frame : { initialFrame, initialFrame + 3 }) {
				ASSERT_LE(frame + 2, asset.sprites->numSprites()) << "Native chest action frames exceed loaded CEL frames.";
				Object chest = MakeNativeObject(spec, asset, frame, Owner);
				chest._oVar1 = 0; // Avoid loot generation while exercising native frame transition.
				chest._oRndSeed = GetLCGEngineState();
				const bool alternate = frame == initialFrame + 3;
				RenderAtBothAnchors(output, spec, alternate ? "chest-closed-alternate" : "chest-closed", chest);
				{
					RestoreRandomState restoreRandom;
					SyncOpObject(noLocalPlayer, CMD_OPERATEOBJ, chest);
				}
				ASSERT_EQ(chest._oAnimFrame, frame + 2) << "Native chest open operation changed its two-frame mapping.";
				RenderAtBothAnchors(output, spec, alternate ? "chest-open-alternate" : "chest-open", chest);
			}
			continue;
		}

		if (spec.nativeObjectId == 48) {
			Object sarcophagus = MakeNativeObject(spec, asset, initialFrame, Owner);
			sarcophagus._oVar1 = 3; // Avoid item and skeleton work in this presentation-only operation probe.
			sarcophagus._oVar2 = -1;
			sarcophagus._oRndSeed = GetLCGEngineState();
			{
				RestoreRandomState restoreRandom;
				SyncOpObject(noLocalPlayer, CMD_OPERATEOBJ, sarcophagus);
			}
			ASSERT_TRUE(sarcophagus._oAnimFlag);
			ASSERT_EQ(sarcophagus._oAnimDelay, 3);
			for (uint32_t frame = 1; frame <= data.animLen; ++frame) {
				ASSERT_LE(frame, asset.sprites->numSprites()) << "Native sarcophagus animation metadata exceeds loaded CEL frames.";
				sarcophagus._oAnimFrame = frame;
				RenderAtBothAnchors(output, spec, "sarcophagus-active-frame", sarcophagus);
			}
			continue;
		}

		if (spec.nativeObjectId == 53 || spec.nativeObjectId == 54) {
			Object trap = MakeNativeObject(spec, asset, initialFrame, Owner);
			trap._oVar4 = 0;
			RenderAtBothAnchors(output, spec, "static-trap-armed", trap);
			trap._oVar4 = 1; // UpdateTrapState sets only this trigger field; the static sprite frame is unchanged.
			EXPECT_EQ(trap._oAnimFrame, initialFrame);
			RenderAtBothAnchors(output, spec, "static-trap-triggered", trap);
			continue;
		}

		if (spec.nativeObjectId == 57 || spec.nativeObjectId == 58) {
			Object barrel = MakeNativeObject(spec, asset, initialFrame, Owner);
			if (spec.nativeObjectId == 57) {
				barrel._oVar2 = 2; // Avoid loot generation while exercising native break-start state.
				barrel._oVar4 = -1;
				barrel._oRndSeed = GetLCGEngineState();
				{
					RestoreRandomState restoreRandom;
					SyncBreakObj(noLocalPlayer, barrel);
				}
				ASSERT_TRUE(barrel._oAnimFlag);
				ASSERT_EQ(barrel._oBreak, -1);
				ASSERT_EQ(barrel._oAnimFrame, 1U);
			} else {
				// Explosive break dispatch affects nearby world entities; capture its source-frame sequence without running that gameplay effect.
				barrel._oAnimFlag = true;
				barrel._oAnimDelay = 1;
				barrel._oBreak = -1;
				barrel.selectionRegion = SelectionRegion::None;
			}
			ASSERT_LE(data.animLen, asset.sprites->numSprites());
			for (uint32_t frame = 1; frame <= data.animLen; ++frame) {
				barrel._oAnimFrame = frame;
				RenderAtBothAnchors(output, spec, "barrel-break-animation-frame", barrel);
			}
			Object synchronizedBreak = MakeNativeObject(spec, asset, initialFrame, Owner);
			synchronizedBreak._oBreak = 1;
			DeltaSyncBreakObj(synchronizedBreak);
			ASSERT_EQ(synchronizedBreak._oAnimFrame, data.animLen);
			ASSERT_EQ(synchronizedBreak._oBreak, -1);
			RenderAtBothAnchors(output, spec, "barrel-broken-final", synchronizedBreak);
		}
	}
}

TEST(NativeObjectArtTest, CapturesActualCathedralObjectFramesAndDrawAnchors)
{
	const char *archivePath = std::getenv("GODEVILUTION_NATIVE_OBJECT_ART_ARCHIVE");
	const char *directory = std::getenv("GODEVILUTION_NATIVE_OBJECT_ART_REFERENCE_DIRECTORY");
	if (archivePath == nullptr || directory == nullptr)
		GTEST_SKIP() << "Set the explicit original archive and a new private reference directory to capture native object art.";

	const std::filesystem::path outputRoot = std::filesystem::absolute(directory).lexically_normal();
	ASSERT_FALSE(std::filesystem::exists(outputRoot)) << "Native object art reference export refuses to overwrite an existing path.";
	std::unique_ptr<mpqfs_archive_t, decltype(&mpqfs_close)> archive(mpqfs_open(archivePath), mpqfs_close);
	ASSERT_NE(archive, nullptr) << mpqfs_last_error();

	std::array<std::vector<uint8_t>, NativeObjectStems.size()> celBytes;
	for (size_t index = 0; index < NativeObjectStems.size(); ++index) {
		celBytes[index] = ReadArchiveFile(archive.get(), std::string("objects/") + NativeObjectStems[index] + ".cel");
		ASSERT_FALSE(celBytes[index].empty()) << NativeObjectStems[index];
	}
	const std::vector<uint8_t> palette = ReadArchiveFile(archive.get(), "levels/l1data/l1_1.pal");
	ASSERT_EQ(palette.size(), 768U) << "The extracted L1 palette must contain 256 RGB triples.";

	const std::filesystem::path repositoryAssets = std::filesystem::absolute(__FILE__).parent_path().parent_path() / "assets";
	const std::filesystem::path extractedSource = outputRoot / "source";
	NativeObjectAssetContext assetContext(repositoryAssets, extractedSource);
	ASSERT_GT(AllObjects.size(), 58U);
	std::filesystem::create_directories(outputRoot);
	for (size_t index = 0; index < NativeObjectStems.size(); ++index) {
		WriteBytes(extractedSource / "objects" / (std::string(NativeObjectStems[index]) + ".cel"), celBytes[index]);
	}
	WriteBytes(extractedSource / "levels" / "l1data" / "l1_1.pal", palette);

	std::array<NativeObjectArt, NativeObjectStems.size()> assets;
	for (size_t index = 0; index < assets.size(); ++index)
		assets[index].stem = NativeObjectStems[index];
	for (const NativeObjectSpec &spec : NativeObjects) {
		const ObjectData &data = AllObjects.at(spec.nativeObjectId);
		ASSERT_LT(static_cast<size_t>(data.ofindex), ObjMasterLoadList.size());
		ASSERT_EQ(ObjMasterLoadList.at(static_cast<size_t>(data.ofindex)), spec.stem)
		    << "Native object ID does not map to the expected source CEL stem: " << spec.nativeObjectId;
		NativeObjectArt &asset = FindArt(assets, spec.stem);
		ASSERT_GT(data.animWidth, 0) << spec.nativeObjectId;
		if (asset.animWidth == 0)
			asset.animWidth = data.animWidth;
		else
			EXPECT_EQ(asset.animWidth, data.animWidth) << "Shared source stem has inconsistent native conversion widths: " << spec.stem;
	}
	for (NativeObjectArt &asset : assets) {
		const std::string loadPath = std::string("objects\\") + asset.stem;
		auto loaded = LoadCelWithStatus(loadPath.c_str(), asset.animWidth);
		ASSERT_TRUE(loaded.has_value()) << loaded.error() << " (" << loadPath << ")";
		asset.sprites.emplace(std::move(loaded).value());
		ASSERT_GT(asset.sprites->numSprites(), 0U) << asset.stem;
	}

	std::ostringstream binary(std::ios::out | std::ios::binary);
	binary.write("DVLNOAR1", 8);
	WriteNumber(binary, static_cast<uint32_t>(assets.size()));
	for (const NativeObjectArt &asset : assets) {
		WriteString(binary, asset.stem);
		WriteNumber(binary, asset.sprites->numSprites());
		for (uint32_t frameIndex = 0; frameIndex < asset.sprites->numSprites(); ++frameIndex)
			WriteNativeFrame(binary, (*asset.sprites)[frameIndex], frameIndex + 1);
	}
	ASSERT_TRUE(binary.good());

	std::ofstream indexOutput(outputRoot / "native-object-art-index.tsv", std::ios::binary);
	ASSERT_TRUE(indexOutput.good());
	indexOutput << "native_object_id\tstem\tanim_width\tanim_delay\tanim_length\tanimated\tinitial_frame\talternate_frame\topened_frame\talternate_opened_frame\tfinal_frame\tsource_frame_count\r\n";
	for (const NativeObjectSpec &spec : NativeObjects) {
		const ObjectData &data = AllObjects.at(spec.nativeObjectId);
		const NativeObjectArt &asset = FindArt(assets, spec.stem);
		const uint32_t initialFrame = data.isAnimated() ? 1 : data.animDelay;
		const bool isDoor = spec.nativeObjectId == 1 || spec.nativeObjectId == 2;
		const bool isChest = spec.nativeObjectId >= 5 && spec.nativeObjectId <= 7;
		const std::string alternate = isChest ? std::to_string(initialFrame + 3) : "";
		const std::string opened = (isDoor || isChest) ? std::to_string(initialFrame + 2) : "";
		const std::string alternateOpened = isChest ? std::to_string(initialFrame + 5) : "";
		const std::string finalFrame = data.animLen > 0 ? std::to_string(data.animLen) : "";
		indexOutput << spec.nativeObjectId << '\t' << spec.stem << '\t' << static_cast<int>(data.animWidth) << '\t'
		            << static_cast<int>(data.animDelay) << '\t' << static_cast<int>(data.animLen) << '\t'
		            << static_cast<int>(data.isAnimated()) << '\t' << initialFrame << '\t' << alternate << '\t' << opened << '\t'
		            << alternateOpened << '\t' << finalFrame << '\t' << asset.sprites->numSprites() << "\r\n";
	}
	indexOutput.flush();
	indexOutput.close();
	ASSERT_TRUE(indexOutput.good());

	std::ofstream draws(outputRoot / "native-object-art-draws.tsv", std::ios::binary);
	ASSERT_TRUE(draws.good());
	draws << "native_object_id\tstate\tframe_1based\tprobe\ttile_dx\ttile_dy\ttarget_x\ttarget_y\tdraw_anchor_x\tdraw_anchor_y\ttop_left_x\ttop_left_y\tnominal_width\theight\topaque_min_x\topaque_min_y\topaque_max_x_exclusive\topaque_max_y_exclusive\r\n";
	for (const NativeObjectSpec &spec : NativeObjects) {
		const NativeObjectArt &asset = FindArt(assets, spec.stem);
		for (uint32_t frame = 1; frame <= asset.sprites->numSprites(); ++frame) {
			Object object = MakeNativeObject(spec, asset, frame, { 64, 64 });
			RenderAtBothAnchors(draws, spec, "source-frame", object);
		}
	}
	WriteActionDrawRows(draws, assets);
	draws.flush();
	draws.close();
	ASSERT_TRUE(draws.good());

	std::ofstream binaryOutput(outputRoot / "native-object-art-reference.bin", std::ios::binary);
	ASSERT_TRUE(binaryOutput.good());
	const std::string bytes = binary.str();
	binaryOutput.write(bytes.data(), static_cast<std::streamsize>(bytes.size()));
	binaryOutput.flush();
	binaryOutput.close();
	ASSERT_TRUE(binaryOutput.good());
}

} // namespace
} // namespace devilution
