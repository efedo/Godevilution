/**
 * @file engine/bestiary.hpp
 *
 * The Bestiary holds all data for monster *types* that are present on the
 * current level: graphics, sounds, placement flags, corpse ids, etc.
 *
 * It is distinct from the Monster array (monster.h), which holds per-instance
 * state for every live monster.  One Bestiary entry (CMonster) may be shared
 * by many Monster instances of the same species.
 *
 * The global instance is `LevelBestiary`.
 */
#pragma once

#include <cstddef>
#include <cstdint>

#include <array>
#include <memory>
#include <string>

#include <expected.hpp>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/audio/sound.h"
#include "tables/monstdat.h"

namespace devilution {

/** Maximum number of distinct monster types allowed on a single level. */
constexpr size_t MaxLvlMTypes = 24;

// ---------------------------------------------------------------------------
// Types that describe a single monster *species* (shared across instances)
// ---------------------------------------------------------------------------

enum class MonsterGraphic : uint8_t {
	Stand,
	Walk,
	Attack,
	GotHit,
	Death,
	Special,
};

enum class MonsterSound : uint8_t {
	Attack,
	Hit,
	Death,
	Special
};

enum placeflag : uint8_t {
	// clang-format off
	PLACE_SCATTER = 1 << 0,
	PLACE_SPECIAL = 1 << 1,
	PLACE_UNIQUE  = 1 << 2,
	// clang-format on
};

struct AnimStruct {
	/** Sprite lists for each of the 8 directions. */
	OptionalClxSpriteListOrSheet sprites;

	[[nodiscard]] OptionalClxSpriteList spritesForDirection(Direction direction) const
	{
		if (!sprites)
			return std::nullopt;
		return sprites->isSheet() ? (*sprites).sheet()[static_cast<size_t>(direction)] : (*sprites).list();
	}

	uint16_t width;
	int8_t frames;
	int8_t rate;
};

struct MonsterSpritesData {
	static constexpr size_t MaxAnims = 6;
	std::unique_ptr<std::byte[]> data;
	std::array<uint32_t, MaxAnims + 1> offsets;
};

/**
 * @brief Data for a single monster *type* (species) loaded for the level.
 *
 * Owns the graphics and sound assets for that species.  Multiple live Monster
 * instances may share one CMonster entry.
 */
struct CMonster {
	std::unique_ptr<std::byte[]> animData;
	AnimStruct anims[6];
	std::unique_ptr<TSnd> sounds[4][2];

	_monster_id type;
	/** placeflag bits describing where this type may be placed */
	uint8_t placeFlags;
	int8_t corpseId = 0;

	[[nodiscard]] const MonsterData &data() const
	{
		return MonstersData[type];
	}

	/** @brief Returns AnimStruct for the given graphic slot. */
	[[nodiscard]] const AnimStruct &getAnimData(MonsterGraphic graphic) const
	{
		return anims[static_cast<int>(graphic)];
	}
};

// ---------------------------------------------------------------------------
// Bestiary class
// ---------------------------------------------------------------------------

/**
 * @brief Catalogue of all monster *types* loaded for the current level.
 *
 * Holds the fixed-size array of CMonster entries together with a count of
 * how many are currently in use, and provides the operations that manage
 * their lifetime.
 */
class Bestiary {
public:
	Bestiary() = default;

	// Non-copyable: entries own heap-allocated graphics/sound data.
	Bestiary(const Bestiary &) = delete;
	Bestiary &operator=(const Bestiary &) = delete;

	// -------------------------------------------------------------------------
	// Accessors
	// -------------------------------------------------------------------------

	/** @brief Number of monster types currently loaded for this level. */
	[[nodiscard]] size_t size() const { return typeCount; }

	/** @brief Returns true when no monster types are loaded. */
	[[nodiscard]] bool empty() const { return typeCount == 0; }

	/**
	 * @brief Decrements the type count by one, allowing the last slot to be
	 *        reused.  Used by debug spawn logic when the bestiary is full.
	 */
	void decrementSize()
	{
		if (typeCount > 0) --typeCount;
	}

	/** @brief Indexed access (unchecked). */
	CMonster &operator[](size_t index) { return types[index]; }
	const CMonster &operator[](size_t index) const { return types[index]; }

	/** @brief Range-for support over the loaded entries. */
	CMonster *begin() { return types; }
	CMonster *end() { return types + typeCount; }
	const CMonster *begin() const { return types; }
	const CMonster *end() const { return types + typeCount; }

	/**
	 * @brief Returns the index of `type` in the loaded array, or `size()` if
	 *        not found (sentinel meaning "not present yet").
	 */
	[[nodiscard]] size_t findTypeIndex(_monster_id type) const;

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	/**
	 * @brief Resets ready for a new level load.
	 *
	 * Clears placement flags on all entries and resets the count to zero.
	 * Does NOT free graphics/sounds — call free() first when tearing down a
	 * level.
	 */
	void init();

	/**
	 * @brief Registers a monster type for the current level.
	 *
	 * If the type is not yet present a new entry is created and its sounds are
	 * loaded.  The given placement flag is OR-ed in regardless.
	 *
	 * @return The index of the type within the bestiary, or an error string.
	 */
	tl::expected<size_t, std::string> addType(_monster_id type, placeflag flag);

	/**
	 * @brief Loads graphics for a single monster type entry.
	 *
	 * @param monsterType  The entry to populate.
	 * @param spritesData  Optional pre-loaded sprite data; loaded from disk if
	 *                     not provided.
	 */
	static tl::expected<void, std::string> initGraphics(CMonster &monsterType, MonsterSpritesData spritesData = {});

	/**
	 * @brief Loads graphics for all registered types, sharing sprite data
	 *        between types that use the same sprite sheet.
	 */
	tl::expected<void, std::string> initAllGraphics();

	/** @brief Loads sound effects for a single monster type. */
	static tl::expected<void, std::string> initSounds(CMonster &monsterType);

	/** @brief Releases all graphics and sound data. Call when tearing down a level. */
	void free();

	// -------------------------------------------------------------------------
	// Storage — public so code not yet refactored can still reach it directly.
	// -------------------------------------------------------------------------
	CMonster types[MaxLvlMTypes];

private:
	size_t typeCount = 0;
};

/** @brief The single global Bestiary instance for the current level. */
extern DVL_API_FOR_TEST Bestiary LevelBestiary;

} // namespace devilution
