/**
 * @file game/monsters/monsters.hpp
 *
 * Interface of monster functionality, AI, actions, spawning, loading, etc.
 */
#pragma once

#include <cstddef>
#include <cstdint>

#include <array>
#include <functional>
#include <optional>
#include <string>

#include <expected.hpp>
#include <function_ref.hpp>

#include "application/game_mode.hpp"
#include "engine/actor.hpp"
#include "engine/actor_position.hpp"
#include "engine/bestiary.hpp"
#include "engine/combat_actor.hpp"
#include "engine/math/point.hpp"
#include "engine/math/world_tile.hpp"
#include "game/levels/dun_tile.hpp"
#include "tables/misdat.h"
#include "tables/monstdat.h"
#include "tables/spelldat.h"
#include "tables/textdat.h"
#include "utils/language.h"

namespace devilution {

struct Missile;
struct Player;

constexpr size_t MaxMonsters = 200;

/**
 * Describes how a native unique-monster conversation may be handled while
 * the server-backed client is connected.
 */
enum class ServerBackedMonsterInteractionRoute : uint8_t {
	None,
	Submit,
	FailClosed,
};

/**
 * Keeps connected monster conversations on the authoritative boundary; the
 * caller must fail closed when no authored quest dialogue is available.
 */
constexpr ServerBackedMonsterInteractionRoute ResolveServerBackedMonsterInteractionRoute(
    bool serverConnected,
    bool projectedEntityAvailable,
    bool questAvailable) noexcept
{
	if (!serverConnected)
		return ServerBackedMonsterInteractionRoute::None;
	if (!projectedEntityAvailable)
		return ServerBackedMonsterInteractionRoute::FailClosed;
	return questAvailable
	    ? ServerBackedMonsterInteractionRoute::Submit
	    : ServerBackedMonsterInteractionRoute::FailClosed;
}

/**
 * Legacy monster dialogue changes the local monster mode only when the
 * authoritative runtime is not owning the conversation.
 */
constexpr bool ShouldApplyLegacyMonsterConversationMutation(bool authoritativeMode) noexcept
{
	return !authoritativeMode;
}

enum monster_flag : uint16_t {
	// clang-format off
	MFLAG_HIDDEN          = 1 << 0,
	MFLAG_LOCK_ANIMATION  = 1 << 1,
	MFLAG_ALLOW_SPECIAL   = 1 << 2,
	MFLAG_TARGETS_MONSTER = 1 << 4,
	MFLAG_GOLEM           = 1 << 5,
	MFLAG_QUEST_COMPLETE  = 1 << 6,
	MFLAG_KNOCKBACK       = 1 << 7,
	MFLAG_SEARCH          = 1 << 8,
	MFLAG_CAN_OPEN_DOOR   = 1 << 9,
	MFLAG_NO_ENEMY        = 1 << 10,
	MFLAG_BERSERK         = 1 << 11,
	MFLAG_NOLIFESTEAL     = 1 << 12,
	// clang-format on
};

// Indexes from UniqueMonstersData array for special unique monsters (usually quest related)
enum class UniqueMonsterType : uint8_t {
	Garbud,
	SkeletonKing,
	Zhar,
	SnotSpill,
	Lazarus,
	RedVex,
	BlackJade,
	Lachdan,
	WarlordOfBlood,
	Butcher,
	HorkDemon,
	Defiler,
	NaKrul,
	None = static_cast<uint8_t>(-1),
};

enum class MonsterMode : uint8_t {
	Stand,
	MoveNorthwards, // Movement towards N, NW, or NE
	MoveSouthwards, // Movement towards S, SW, or SE
	MoveSideways,   // Movement towards W or E
	MeleeAttack,
	HitRecovery,
	Death,
	SpecialMeleeAttack,
	FadeIn,
	FadeOut,
	RangedAttack,
	SpecialStand,
	SpecialRangedAttack,
	Delay,
	Charge,
	Petrified,
	Heal,
	Talk,
};

bool IsMonsterModeMove(MonsterMode mode);

// Maps an eight-way movement direction to the native renderer's movement mode.
// NoDirection is only a presentation fallback and maps to Stand.
[[nodiscard]] MonsterMode GetMonsterMovementMode(Direction direction);

enum class MonsterGoal : uint8_t {
	None,
	Normal,
	Retreat,
	Healing,
	Move,
	Attack,
	Inquiring,
	Talking,
};

// Defines the relation of the monster to a monster pack.
// If value is different from Individual Monster, the leader must also be set
enum class LeaderRelation : uint8_t {
	None,
	Leashed,   // Minion that sticks to the leader
	Separated, // Minion that was separated from the leader and acts individually until it reaches the leader again
};

struct Monster : CombatActor { // note: missing field _mAFNum

	std::unique_ptr<uint8_t[]> uniqueMonsterTRN;
	// animInfo, direction, lightId, hitPoints, maxHitPoints are inherited from Actor
	uint32_t flags;
	uint32_t rndItemSeed; // Seed used to determine item drops on death
	uint32_t aiSeed;      // Seed used to determine AI behaviour/sync sounds in multiplayer games?
	uint16_t golemToHit;
	uint16_t resistance;
	_speech_id talkMsg;
	int16_t goalVar1; // Specifies monster's behaviour regarding moving and changing goals.
	int8_t goalVar2;  // Specifies turning direction for @p RoundWalk in most cases.
	                  // Used in custom way by @p FallenAi, @p SnakeAi, @p M_FallenFear and @p FallenAi.
	int8_t goalVar3;  // Controls monster's behaviour regarding special actions.
	                  // Used only by @p ScavengerAi, @p MegaAi and @p GolemAi.
	int16_t var1;
	int16_t var2;
	int8_t var3;
	MonsterGoal goal;                // Specifies current goal of the monster
	WorldTilePosition enemyPosition; // Usually corresponds to the enemy's future position
	uint8_t levelType;
	MonsterMode mode;
	uint8_t pathCount;
	uint8_t enemy; // The current target of the monster.
	               // An index in to either the player or monster array based on the _meflag value.
	bool isInvalid;
	MonsterAIID ai;
	uint8_t intelligence;   // Affects monster thinking; higher value results in more aggressive behaviour (e.g. some monsters use this to calculate the @p AiDelay)
	uint8_t activeForTicks; // Stores information for how many ticks the monster will remain active
	UniqueMonsterType uniqueType;
	uint8_t uniqTrans;
	int8_t corpseId;
	int8_t whoHit;
	uint8_t minDamage;
	uint8_t maxDamage;
	uint8_t minDamageSpecial;
	uint8_t maxDamageSpecial;
	uint8_t armorClass;
	uint8_t reducePlayerStrength;
	uint8_t reducePlayerMagic;
	uint8_t reducePlayerDexterity;
	uint8_t reducePlayerVitality;
	uint8_t reducePlayerMaxHP;
	uint8_t reducePlayerMaxMana;
	uint8_t leader;
	LeaderRelation leaderRelation;
	uint8_t packSize;

	static constexpr uint8_t NoLeader = -1;

	// Sets the current cell sprite to match the desired desiredDirection and animation sequence
	void changeAnimationData(MonsterGraphic graphic, Direction desiredDirection);

	// Sets the current cell sprite to match the desired animation sequence using the direction the monster is currently facing
	void changeAnimationData(MonsterGraphic graphic);

	// Check if correct stand Animation is loaded; needed if direction is changed (e.g. monster stands and looks at the player)
	void checkStandAnimationIsLoaded(Direction dir);

	// Sets mode to MonsterMode::Petrified
	void petrify();

	[[nodiscard]] const CMonster &type() const;

	[[nodiscard]] const MonsterData &data() const;

	// Returns monster's name
	[[nodiscard]] std::string_view name() const override;

	// Calculates monster's XP value, including bonuses from difficulty and monster being unique
	[[nodiscard]] unsigned int exp(_difficulty difficulty) const;

	// Calculates monster's chance to hit with normal attack, including bonuses from difficulty and monster being unique
	unsigned int toHit(_difficulty difficulty) const;

	// Calculates monster's chance to hit with special attack, including bonuses from difficulty and monster being unique
	unsigned int toHitSpecial(_difficulty difficulty) const;

	// Calculates monster's level, including bonuses from difficulty and monster being unique
	[[nodiscard]] unsigned int level(_difficulty difficulty) const;

	// Returns the network identifier for this monster
	// This is currently the index into the Monsters array, but may change in the future.
	[[nodiscard]] size_t getId() const;

	[[nodiscard]] Monster *getLeader() const;
	void setLeader(const Monster *leader);

	[[nodiscard]] bool hasLeashedMinions() const;

	// Calculates the distance in tiles between this monster and its current target
	// The distance is not calculated as the euclidean distance, but rather as
	// the longest number of tiles in the coordinate system.
	[[nodiscard]] unsigned distanceToEnemy() const;

	// Is the monster currently walking?
	[[nodiscard]] bool isWalking() const override;
	[[nodiscard]] bool isImmune(MissileID mitype, DamageType missileElement) const;
	[[nodiscard]] bool isResistant(MissileID mitype, DamageType missileElement) const;

	// Is this a player's golem?
	[[nodiscard]] bool isPlayerMinion() const;

	bool isPossibleToHit() const;

	void tag(const Player &tagger);

	[[nodiscard]] bool isUnique() const;

	bool tryLiftGargoyle();

	// Gets the visual/shown monster mode
	// Petrified monsters have mode MonsterMode::Petrified but are rendered with the old/real mode
	[[nodiscard]] MonsterMode getVisualMonsterMode() const;

	// Sets a tile/dMonster to be occupied by the monster
	// isMoving specifies whether the monster is moving or not (true/moving results in a negative index in dMonster)
	void occupyTile(Point tile, bool isMoving) const;

	// Returns true when this monster is a talker (quest-related dialogue monster).
	[[nodiscard]] bool isTalker() const;

	// Returns true when the monster is currently in an inquiring or talking goal state.
	[[nodiscard]] bool canTalk() const;

	// Clears monster occupancy from the tiles around its last known position
	void clearSquares();

	// Puts the monster into a standing mode, resetting movement variables
	void startStand(Direction md);

	// Applies a knockback displacement to the monster from the given attacker position
	void getKnockback(WorldTilePosition attackerStartPos);

	// Applies a hit reaction to the monster for the given damage amount.
	void startHit(int dam);

	// Applies a hit reaction attributed to a player, updating target tracking.
	void startHit(const Player &player, int dam);

	// Begins the monster death sequence attributed to the player who landed the killing blow
	void startKill(const Player &player);

	// Network-synchronized version of startKill; repositions the monster before killing it.
	// position: The authoritative tile position of the monster at time of death.
	// player: The player who landed the killing blow.
	void syncStartKill(Point position, const Player &player);

	// Releases pack/leader relationships when monster is removed from level
	void updateRelations() const;

	// Returns true if the monster can legally move one step in the given direction
	[[nodiscard]] bool isDirOK(Direction mdir) const;

	// Moves monster one tile in desired direction if possible; returns true if successful
	bool walk(Direction md);

	// Encodes the monster's current enemy reference into a network-safe byte.
	[[nodiscard]] uint8_t encodeEnemy() const;

	// Decodes a network-received enemy byte and updates internal enemy tracking.
	void decodeEnemy(uint8_t enemyId);

	// Re-synchronises the monster's animation to match its current mode.
	tl::expected<void, std::string> syncAnim();

	// Plays the given sound effect at this monster's position.
	void playEffect(MonsterSound mode);

	// Applies damage to this monster, triggering network sync and kill if needed.
	void applyDamage(DamageType damageType, int damage);

	// Permanently reduces a player's attributes based on this monster's drain values.
	void reducePlayerAttribute(Player &player);

	// Spawns a doppelganger copy of this monster on an adjacent tile.
	void addDoppelganger();
};

/** Storage owned by gMonsterPool (monster_pool.cpp). */
extern DVL_API_FOR_TEST Monster *Monsters;
extern DVL_API_FOR_TEST int MonsterKillCounts[NUM_MAX_MTYPES];
extern bool sgbSaveSoundOn;

tl::expected<void, std::string> PrepareUniqueMonst(Monster &monster, UniqueMonsterType monsterType, size_t miniontype, int bosspacksize, const UniqueMonsterData &uniqueMonsterData);
void InitLevelMonsters();
tl::expected<void, std::string> GetLevelMTypes();
tl::expected<size_t, std::string> AddMonsterType(_monster_id type, placeflag placeflag);
tl::expected<size_t, std::string> AddMonsterType(UniqueMonsterType uniqueType, placeflag placeflag);
MonsterSpritesData LoadMonsterSpritesData(const MonsterData &monsterData);

// InitMonsterSND, InitMonsterGFX, and InitAllMonsterGFX have been promoted to
// Bestiary::initSounds(), Bestiary::initGraphics(), and Bestiary::initAllGraphics().
// These free-function wrappers are kept for call sites that have not yet been updated.
tl::expected<void, std::string> InitMonsterSND(CMonster &monsterType);
tl::expected<void, std::string> InitMonsterGFX(CMonster &monsterType, MonsterSpritesData spritesData = {});
tl::expected<void, std::string> InitAllMonsterGFX();
void WeakenNaKrul();
void InitGolems();
DVL_API_FOR_TEST tl::expected<void, std::string> InitMonsters();
#ifdef BUILD_TESTING
/** Test-only observation of the native InitMonsters density target. */
DVL_API_FOR_TEST size_t MonsterPopulationTargetForTesting();
/** Owned fixture snapshot of hidden counters changed by ordinary initialization. */
struct MonsterPopulationGlobalsForTesting {
	size_t totalMonsters;
	int imageTotal;
	int uniqueTransparency;
};
DVL_API_FOR_TEST MonsterPopulationGlobalsForTesting CaptureMonsterPopulationGlobalsForTesting();
DVL_API_FOR_TEST void RestoreMonsterPopulationGlobalsForTesting(MonsterPopulationGlobalsForTesting state);
#endif
tl::expected<void, std::string> SetMapMonsters(const uint16_t *dunData, Point startPosition);
Monster *AddMonster(Point position, Direction dir, size_t typeIndex, bool inMap);
[[nodiscard]] std::optional<size_t> FindNativeMonsterTypeIndex(uint32_t monsterId);
void DeleteMonsterAtActiveIndex(size_t activeIndex);

// Spawns a new monsters (dynamically/not on level load).
// Only executed for the level owner, to prevent desyncs in multiplayer.
// The level owner sends a CMD_SPAWNMONSTER-message to the other players.
void SpawnMonster(Point position, Direction dir, size_t typeIndex);

// Loads data for a dynamically spawned monster when entering a level in multiplayer.
void LoadDeltaSpawnedMonster(size_t typeIndex, size_t monsterId, uint32_t seed, uint8_t golemOwnerPlayerId, int16_t golemSpellLevel);

// Initialize a spanwed monster (from a network message or from SpawnMonster-function).
void InitializeSpawnedMonster(Point position, Direction dir, size_t typeIndex, size_t monsterId, uint32_t seed, uint8_t golemOwnerPlayerId, int16_t golemSpellLevel);
void StartMonsterDeath(Monster &monster, const Player &player, bool sendmsg);
void MonsterDeath(Monster &monster, Direction md, bool sendmsg);
void KillGolem(Monster &golem);
void DoEnding();
void PrepDoEnding();
void GolumAi(Monster &golem);
void DeleteMonsterList();
void RemoveEnemyReferences(const Player &player);
/** One native autonomous regeneration tick; health and the increment are raw Q6. */
void RegenerateMonsterHitPoints(Monster &monster, unsigned monsterLevel);
void ProcessMonsters();
void FreeMonsters();
bool LineClearMissile(Point startPoint, Point endPoint);

// Checks for same missile obstructions as CheckMissileCol() for missiles that move along a path between two points
bool LineClearMovingMissile(Point startPoint, Point endPoint);
void M_FallenFear(Point position);
void PrintMonstHistory(int mt);
void PrintUniqueHistory();
void MissToMonst(Missile &missile, Point position);

Monster *FindMonsterAtPosition(Point position, bool ignoreMovingMonsters = false);
Monster *FindUniqueMonster(UniqueMonsterType monsterType);
Monster *FindGolemForPlayer(const Player &player);

// Check that the given tile is available to the monster
bool IsTileAvailable(const Monster &monster, Point position);
bool IsSkel(_monster_id mt);
bool IsGoat(_monster_id mt);

// Reveals a monster that was hiding in a container
// monster: instance returned from a previous call to PreSpawnSkeleton
// position: tile to try spawn the monster at, neighboring tiles will be used as a fallback
void ActivateSkeleton(Monster &monster, Point position);

Monster *PreSpawnSkeleton();
void TalktoMonster(Player &player, Monster &monster);
void SpawnGolem(const Player &player, Point position, uint8_t spellLevel);

} // namespace devilution
