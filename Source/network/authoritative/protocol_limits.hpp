/** Native mirrors of the byte-level authoritative protocol limits. */
#pragma once

#include <cstddef>

namespace devilution::authoritative {

/** Stable behavior identities carried by ProjectileSnapshot.behavior. */
enum class ProjectileBehavior : int {
	Targeted = 0,
	ChargedBolt = 1,
	Firebolt = 2,
	Lightning = 3,
	LightningSegment = 4,
	Flash = 5,
	FlashBottom = 6,
	FlashTop = 7,
	Healing = 8,
	Teleport = 9,
	Resurrection = 10,
	ResurrectionBeam = 11,
	ManaShield = 12,
	StoneCurse = 13,
	Infravision = 14,
	Phasing = 15,
	Fireball = 16,
	HealOther = 17,
	Search = 18,
	HolyBolt = 19,
	BloodStar = 20,
	BoneSpirit = 21,
	FireWallController = 22,
	FireWallSegment = 23,
	FlameWaveController = 24,
	FlameWaveSegment = 25,
	InfernoController = 26,
	InfernoSegment = 27,
	ChainLightningController = 28,
	ChainLightningBolt = 29,
	NovaController = 30,
	NovaBall = 31,
	ApocalypseController = 32,
	ApocalypseBoom = 33,
	RageController = 34,
	Elemental = 35,
	GuardianController = 36,
	Telekinesis = 37,
	TownPortal = 38,
	Reflect = 39,
	Magi = 40,
	Mana = 41,
	RingOfFireController = 42,
	ImmolationController = 43,
	Jester = 44,
	Berserk = 45,
	Warp = 46,
	LightningWallController = 47,
	Golem = 48,
	Etherealize = 49,
	MonsterMagic = 50,
	Acid = 51,
	AcidSplat = 52,
	AcidPuddle = 53,
	SnakeCharge = 54,
	BatCharge = 55,
	MonsterChargedBolt = 56,
	HorkSpawn = 57,
	WeaponArrow = 58,
	MonsterArrow = 59,
};

/** Server-owned projectile presentation family carried by snapshots. */
enum class ProjectilePresentationKind : int {
	Unspecified = 0,
	Generic = 1,
	Fire = 2,
	FireImpact = 3,
	Lightning = 4,
	Holy = 5,
	BloodStar = 6,
	BoneSpirit = 7,
	Acid = 8,
	Arrow = 9,
	Beam = 10,
	StoneCurse = 11,
	Teleport = 12,
	Phasing = 13,
	MonsterMagic = 14,
	HorkSpawn = 15,
	Guardian = 16,
	Flash = 17,
	Hidden = 18,
	NovaBall = 19,
};

/** Exact native compatibility sprite identity carried by snapshots. */
enum class NativeProjectileVisual : int {
	Unspecified = 0,
	Arrow = 1,
	Firebolt = 2,
	Lightning = 3,
	ThinLightning = 4,
	FlashBottom = 5,
	FlashTop = 6,
	ResurrectBeam = 7,
	Phasing = 8,
	Fireball = 9,
	HolyBolt = 10,
	BloodStar = 11,
	BoneSpirit = 12,
	FireWall = 13,
	FlameWaveControl = 14,
	FlameWave = 15,
	Inferno = 16,
	ChainLightning = 17,
	ApocalypseBoom = 18,
	Elemental = 19,
	Guardian = 20,
	Etherealize = 21,
	Acid = 22,
	AcidSplat = 23,
	AcidPuddle = 24,
	ChargedBolt = 25,
	HorkSpawn = 26,
	NovaBall = 27,
	LightningWall = 28,
	MonsterLightning = 29,
	MonsterOrangeFlare = 30,
	MonsterBlueFlare = 31,
	MonsterRedFlare = 32,
	MonsterYellowFlare = 33,
	MonsterBlueFlare2 = 34,
	MonsterMagmaBall = 35,
};

/** Server-owned status effect identity carried by player snapshots. */
enum class StatusEffectBehavior : int {
	Unspecified = 0,
	Infravision = 1,
	Search = 2,
	Etherealize = 3,
};

/** Server-owned item classification identities carried by item snapshots. */
enum class ItemPresentationKind : int {
	Unspecified = 0,
	Generic = 1,
	Gold = 2,
	Ear = 3,
};

enum class ItemSpellSourceKind : int {
	Unspecified = 0,
	ChargedItem = 1,
	ConsumableItem = 2,
};

enum class ItemActionKind : int {
	Unspecified = 0,
	Identify = 1,
	ReadBook = 2,
	RestorePartialLife = 3,
	RestoreFullLife = 4,
	RestorePartialMana = 5,
	RestoreFullMana = 6,
	RestorePartialLifeAndMana = 7,
	RestoreFullLifeAndMana = 8,
	IncreaseStrength = 9,
	IncreaseMagic = 10,
	IncreaseDexterity = 11,
	IncreaseVitality = 12,
	IncreaseAllAttributes = 13,
	ApplyBlacksmithOil = 14,
	ApplyAccuracyOil = 15,
	ApplySharpnessOil = 16,
	ApplyFortitudeOil = 17,
	OpenCathedralMap = 18,
	ReadQuestNote = 19,
};

enum class SpellTargetingKind : int {
	Unspecified = 0,
	Tile = 1,
	Player = 2,
	CombatEntity = 3,
	WorldEntity = 4,
};

/** Server-owned object presentation identity carried by object snapshots. */
enum class ObjectPresentationKind : int {
	Unspecified = 0,
	Generic = 1,
	Shrine = 2,
	Quest = 3,
	Breakable = 4,
	Trap = 5,
};

inline constexpr std::size_t MaximumEnvelopeBytes = 1024 * 1024;
inline constexpr std::size_t MaximumEventBatchCount = 4096;
inline constexpr std::size_t MaximumWorldMutationCount = 4096;
inline constexpr std::size_t MaximumSnapshotQuestCount = 4096;
inline constexpr std::size_t MaximumSetLevelObjectOriginCount = 4096;
inline constexpr std::size_t MaximumMaterializedProceduralLevelCount = 4096;
inline constexpr int MaximumNativeSetLevelId = 8;
inline constexpr int MaximumMonsterLeaderRelation = 2;
inline constexpr int MaximumMonsterDialogueState = 2;
inline constexpr int MaximumProjectileDamageType = 4;
inline constexpr int MaximumProjectilePhase = 1;
inline constexpr std::size_t MaximumDialogueTextBytes = 16 * 1024;
inline constexpr std::size_t MaximumIdentityBytes = 128;
inline constexpr int MaximumProjectileBehavior = static_cast<int>(ProjectileBehavior::MonsterArrow);
inline constexpr int MaximumProjectilePresentationKind = static_cast<int>(ProjectilePresentationKind::NovaBall);
inline constexpr int MaximumNativeProjectileVisual = static_cast<int>(NativeProjectileVisual::MonsterMagmaBall);
inline constexpr int MaximumStatusEffectBehavior = static_cast<int>(StatusEffectBehavior::Etherealize);
inline constexpr int MaximumItemPresentationKind = static_cast<int>(ItemPresentationKind::Ear);
inline constexpr int MaximumItemSpellSourceKind = static_cast<int>(ItemSpellSourceKind::ConsumableItem);
inline constexpr int MaximumItemActionKind = static_cast<int>(ItemActionKind::ReadQuestNote);
inline constexpr int MaximumSpellTargetingKind = static_cast<int>(SpellTargetingKind::WorldEntity);
inline constexpr int MaximumObjectPresentationKind = static_cast<int>(ObjectPresentationKind::Trap);
inline constexpr int MaximumObjectEffectKind = 9;
inline constexpr int MaximumObjectQuestTransitionKind = 8;
inline constexpr int MaximumObjectRewardKind = 2;
inline constexpr int MaximumObjectRewardOriginKind = 1;
inline constexpr int MaximumObjectQuestSequenceFollowUpKind = 3;
inline constexpr int MaximumObjectProjectileSourceKind = 1;
inline constexpr int MaximumQuestTownerInteractionKind = 4;

} // namespace devilution::authoritative
