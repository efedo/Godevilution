/**
 * @file engine/bestiary.cpp
 *
 * Implementation of the Bestiary class — the catalogue of monster types
 * loaded for the current level.
 */
#include "engine/bestiary.hpp"

#include <cstring>
#include <memory>
#include <string>
#include <vector>

#include <expected.hpp>
#include <fmt/core.h>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/load/load_cl2.hpp"
#include "engine/load/load_file.hpp"
#include "engine/render/clx_render.hpp"
#include "engine/audio/sound.h"
#include "application/game_mode.hpp"
#include "application/headless_mode.hpp"
#include "game/monsters/monsters.hpp"
#include "game/monsters/monster_pool.hpp"
#include "tables/misdat.h"
#include "tables/monstdat.h"
#include "utils/image/cl2_to_clx.hpp"
#include "utils/is_of.hpp"
#include "utils/log.hpp"
#include "utils/pointer_value_union.hpp"
#include "utils/container/static_vector.hpp"
#include "utils/status_macros.hpp"
#include "utils/string/str_cat.hpp"

namespace devilution {

Bestiary LevelBestiary;

// ---------------------------------------------------------------------------
// File-local helpers (mirror of the ones in monster.cpp)
// ---------------------------------------------------------------------------

namespace {

size_t GetNumAnims(const MonsterData &monsterData)
{
    return monsterData.hasSpecial ? 6 : 5;
}

size_t GetNumAnimsWithGraphics(const MonsterData &monsterData)
{
    const size_t numAnims = GetNumAnims(monsterData);
    size_t result = 0;
    for (size_t i = 0; i < numAnims; ++i) {
        if (monsterData.hasAnim(i))
            ++result;
    }
    return result;
}

void InitMonsterTRN(CMonster &monst)
{
    char path[64];
    *BufCopy(path, "monsters\\", monst.data().trnFile, ".trn") = '\0';
    std::array<uint8_t, 256> colorTranslations;
    LoadFileInMem(path, colorTranslations);
    std::replace(colorTranslations.begin(), colorTranslations.end(), uint8_t { 255 }, uint8_t { 0 });

    const size_t numAnims = GetNumAnims(monst.data());
    for (size_t i = 0; i < numAnims; i++) {
        if (i == 1 && IsAnyOf(monst.type, MT_COUNSLR, MT_MAGISTR, MT_CABALIST, MT_ADVOCATE))
            continue;
        AnimStruct &anim = monst.anims[i];
        if (anim.sprites->isSheet()) {
            ClxApplyTrans(ClxSpriteSheet { anim.sprites->sheet() }, colorTranslations.data());
        } else {
            ClxApplyTrans(ClxSpriteList { anim.sprites->list() }, colorTranslations.data());
        }
    }
}

} // namespace

// ---------------------------------------------------------------------------
// Bestiary method implementations
// ---------------------------------------------------------------------------

size_t Bestiary::findTypeIndex(_monster_id type) const
{
    for (size_t i = 0; i < typeCount; i++) {
        if (types[i].type == type)
            return i;
    }
    return typeCount; // sentinel: not found
}

void Bestiary::init()
{
    typeCount = 0;
    for (CMonster &entry : types)
        entry.placeFlags = 0;
}

tl::expected<size_t, std::string> Bestiary::addType(_monster_id type, placeflag flag)
{
    size_t typeIndex = findTypeIndex(type);
    CMonster &monsterType = types[typeIndex];

    if (typeIndex == typeCount) {
        // New entry — populate it.
        typeCount++;
        monsterType.type = type;
        const MonsterData &monsterData = MonstersData[type];

        const size_t numAnims = GetNumAnims(monsterData);
        for (size_t i = 0; i < numAnims; ++i) {
            AnimStruct &anim = monsterType.anims[i];
            anim.frames = monsterData.frames[i];
            if (monsterData.hasAnim(i)) {
                anim.rate = monsterData.rate[i];
                anim.width = monsterData.width;
            }
        }

        RETURN_IF_ERROR(initSounds(monsterType));
    }

    monsterType.placeFlags |= flag;
    return typeIndex;
}

/*static*/
tl::expected<void, std::string> Bestiary::initSounds(CMonster &monsterType)
{
    if (!gbSndInited)
        return {};

    const char *prefixes[] { "a", "h", "d", "s" };
    const MonsterData &data = MonstersData[monsterType.type];
    const std::string_view soundSuffix = data.soundPath();

    for (int i = 0; i < 4; i++) {
        const std::string_view prefix = prefixes[i];
        if (prefix == "s" && !data.hasSpecialSound)
            continue;
        for (int j = 0; j < 2; j++) {
            char path[64];
            *BufCopy(path, "monsters\\", soundSuffix, prefix, j + 1, ".wav") = '\0';
            ASSIGN_OR_RETURN(monsterType.sounds[i][j], SoundFileLoadWithStatus(path));
        }
    }
    return {};
}

/*static*/
tl::expected<void, std::string> Bestiary::initGraphics(CMonster &monsterType, MonsterSpritesData spritesData)
{
    if (HeadlessMode)
        return {};

    const _monster_id mtype = monsterType.type;
    const MonsterData &monsterData = MonstersData[mtype];
    if (spritesData.data == nullptr)
        spritesData = LoadMonsterSpritesData(monsterData);
    monsterType.animData = std::move(spritesData.data);

    const size_t numAnims = GetNumAnims(monsterData);
    for (size_t i = 0, j = 0; i < numAnims; ++i) {
        if (!monsterData.hasAnim(i)) {
            monsterType.anims[i].sprites = std::nullopt;
            continue;
        }
        const uint32_t begin = spritesData.offsets[j];
        const uint32_t end = spritesData.offsets[j + 1];
        auto *animSpritesData = reinterpret_cast<uint8_t *>(&monsterType.animData[begin]);
        const uint16_t numLists = GetNumListsFromClxListOrSheetBuffer(animSpritesData, end - begin);
        monsterType.anims[i].sprites = ClxSpriteListOrSheet { animSpritesData, numLists };
        ++j;
    }

    if (!monsterData.trnFile.empty())
        InitMonsterTRN(monsterType);

    // Load any associated missile graphics.
    if (IsAnyOf(mtype, MT_NMAGMA, MT_YMAGMA, MT_BMAGMA, MT_WMAGMA))
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::MagmaBall).LoadGFX());
    if (IsAnyOf(mtype, MT_STORM, MT_RSTORM, MT_STORML, MT_MAEL))
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::ThinLightning).LoadGFX());
    if (mtype == MT_SNOWWICH) {
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BloodStarBlue).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BloodStarBlueExplosion).LoadGFX());
    }
    if (mtype == MT_HLSPWN) {
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BloodStarRed).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BloodStarRedExplosion).LoadGFX());
    }
    if (mtype == MT_SOLBRNR) {
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BloodStarYellow).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BloodStarYellowExplosion).LoadGFX());
    }
    if (IsAnyOf(mtype, MT_NACID, MT_RACID, MT_BACID, MT_XACID, MT_SPIDLORD)) {
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::Acid).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::AcidSplat).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::AcidPuddle).LoadGFX());
    }
    if (mtype == MT_LICH) {
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::OrangeFlare).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::OrangeFlareExplosion).LoadGFX());
    }
    if (mtype == MT_ARCHLICH) {
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::YellowFlare).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::YellowFlareExplosion).LoadGFX());
    }
    if (IsAnyOf(mtype, MT_PSYCHORB, MT_BONEDEMN))
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BlueFlare2).LoadGFX());
    if (mtype == MT_NECRMORB) {
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::RedFlare).LoadGFX());
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::RedFlareExplosion).LoadGFX());
    }
    if (mtype == MT_PSYCHORB)
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BlueFlareExplosion).LoadGFX());
    if (mtype == MT_BONEDEMN)
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::BlueFlareExplosion2).LoadGFX());
    if (mtype == MT_DIABLO)
        RETURN_IF_ERROR(GetMissileSpriteData(MissileGraphicID::DiabloApocalypseBoom).LoadGFX());

    return {};
}

tl::expected<void, std::string> Bestiary::initAllGraphics()
{
    if (HeadlessMode)
        return {};

    using LevelMonsterTypeIndices = StaticVector<size_t, 8>;
    std::vector<LevelMonsterTypeIndices> monstersBySprite(GetNumMonsterSprites());
    for (size_t i = 0; i < typeCount; ++i)
        monstersBySprite[static_cast<size_t>(types[i].data().spriteId)].emplace_back(i);

    size_t totalUniqueBytes = 0;
    size_t totalBytes = 0;

    for (const LevelMonsterTypeIndices &monsterTypes : monstersBySprite) {
        if (monsterTypes.empty())
            continue;
        CMonster &firstMonster = types[monsterTypes[0]];
        if (firstMonster.animData != nullptr)
            continue;
        MonsterSpritesData spritesData = LoadMonsterSpritesData(firstMonster.data());
        const size_t spritesDataSize = spritesData.offsets[GetNumAnimsWithGraphics(firstMonster.data())];
        for (size_t i = 1; i < monsterTypes.size(); ++i) {
            MonsterSpritesData spritesDataCopy { std::unique_ptr<std::byte[]> { new std::byte[spritesDataSize] }, spritesData.offsets };
            memcpy(spritesDataCopy.data.get(), spritesData.data.get(), spritesDataSize);
            RETURN_IF_ERROR(initGraphics(types[monsterTypes[i]], std::move(spritesDataCopy)));
        }
        LogVerbose("Loaded monster graphics: {:15s} {:>4d} KiB   x{:d}", firstMonster.data().spritePath(), spritesDataSize / 1024, monsterTypes.size());
        totalUniqueBytes += spritesDataSize;
        totalBytes += spritesDataSize * monsterTypes.size();
        RETURN_IF_ERROR(initGraphics(firstMonster, std::move(spritesData)));
    }
    LogVerbose(" Total monster graphics:                 {:>4d} KiB {:>4d} KiB", totalUniqueBytes / 1024, totalBytes / 1024);

    if (totalUniqueBytes > 0) {
        // Re-sync any already-spawned monster that had no sprite yet.
        for (const int mi : MonsterPoolAdapter::ActiveMonsterRange()) {
            Monster &monster = Monsters[mi];
            if (!monster.animInfo.sprites)
                RETURN_IF_ERROR(monster.syncAnim());
        }
    }

    return {};
}

void Bestiary::free()
{
    for (CMonster &monsterType : *this) {
        monsterType.animData = nullptr;
        monsterType.corpseId = 0;
        for (AnimStruct &animData : monsterType.anims)
            animData.sprites = std::nullopt;
        for (auto &variants : monsterType.sounds)
            for (auto &sound : variants)
                sound = nullptr;
    }
}

} // namespace devilution
