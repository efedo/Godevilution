#include "network/authoritative/server_backed_world_presentation.hpp"

#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <limits>
#include <string>
#include <unordered_set>

#include "application/headless_mode.hpp"
#include "engine/lighting_defs.hpp"
#include "game/items/item_pool.hpp"
#include "game/items/items.hpp"
#include "game/levels/dungeon_common.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/monsters/monsters.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "engine/render/world_renderer.h"
#include "devilution.pb.h"
#include "game/spells/spells.hpp"
#include "network/authoritative/authoritative_level_id.hpp"
#include "network/authoritative/protocol_limits.hpp"
#include "tables/objdat.h"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

bool IsCurrentLevel(uint32_t entityLevelId, uint32_t currentLevelId)
{
	return entityLevelId == 0 || entityLevelId == currentLevelId;
}

bool IsValidWorldPosition(int32_t x, int32_t y)
{
	return x >= 0 && y >= 0
	    && x <= std::numeric_limits<WorldTileCoord>::max()
	    && y <= std::numeric_limits<WorldTileCoord>::max()
	    && InDungeonBounds({ x, y });
}

bool IsValidPlayerClass(uint32_t characterClassId)
{
	return characterClassId <= static_cast<uint32_t>(HeroClass::LAST);
}

uint8_t GetRemotePlayerEquipmentGraphicBitsInternal(const ProjectedPlayerSnapshot &snapshot)
{
	ItemType weaponType = ItemType::None;
	bool holdsShield = false;
	uint8_t armorGraphic = 0;
	for (const auto &equipped : snapshot.equipment) {
		const Item &item = equipped.item;
		if (item.isEmpty())
			continue;
		switch (equipped.slot) {
		case INVLOC_HAND_LEFT:
		case INVLOC_HAND_RIGHT:
			if (item._itype == ItemType::Shield)
				holdsShield = true;
			else if (item._iClass == ICLASS_WEAPON)
				weaponType = item._itype;
			break;
		case INVLOC_CHEST:
			switch (item._itype) {
			case ItemType::MediumArmor:
				armorGraphic = 1 << 4;
				break;
			case ItemType::HeavyArmor:
				armorGraphic = 1 << 5;
				break;
			default:
				armorGraphic = 0;
				break;
			}
			break;
		default:
			break;
		}
	}

	uint8_t weaponGraphic;
	switch (weaponType) {
	case ItemType::Sword:
		weaponGraphic = static_cast<uint8_t>(holdsShield ? PlayerWeaponGraphic::SwordShield : PlayerWeaponGraphic::Sword);
		break;
	case ItemType::Axe:
		weaponGraphic = static_cast<uint8_t>(PlayerWeaponGraphic::Axe);
		break;
	case ItemType::Bow:
		weaponGraphic = static_cast<uint8_t>(PlayerWeaponGraphic::Bow);
		break;
	case ItemType::Mace:
		weaponGraphic = static_cast<uint8_t>(holdsShield ? PlayerWeaponGraphic::MaceShield : PlayerWeaponGraphic::Mace);
		break;
	case ItemType::Staff:
		weaponGraphic = static_cast<uint8_t>(PlayerWeaponGraphic::Staff);
		break;
	default:
		weaponGraphic = static_cast<uint8_t>(holdsShield ? PlayerWeaponGraphic::UnarmedShield : PlayerWeaponGraphic::Unarmed);
		break;
	}
	return weaponGraphic | armorGraphic;
}

void InitializeRemotePlayer(Player &player, const ProjectedPlayerSnapshot &snapshot)
{
	player = {};
	player._pClass = static_cast<HeroClass>(snapshot.characterClassId);
	player.life.current = snapshot.life;
	player.life.maximum = snapshot.lifeMaximum;
	RefreshProjectedPlayerGraphics(player, snapshot);
}

Direction GetRemotePlayerDirection(const ProjectedPlayerSnapshot &snapshot)
{
	return snapshot.facingDirection <= static_cast<uint32_t>(Direction::SouthEast)
	    ? static_cast<Direction>(snapshot.facingDirection)
	    : Direction::South;
}

player_graphic GetRemotePlayerGraphicInternal(const ProjectedPlayerSnapshot &snapshot)
{
	if (snapshot.life <= 0 || snapshot.combatMode == PM_DEATH)
		return player_graphic::Death;

	switch (snapshot.combatMode) {
	case PM_WALK_NORTHWARDS:
	case PM_WALK_SOUTHWARDS:
	case PM_WALK_SIDEWAYS:
		return player_graphic::Walk;
	case PM_ATTACK:
	case PM_RATTACK:
		return player_graphic::Attack;
	case PM_BLOCK:
		return player_graphic::Block;
	case PM_GOTHIT:
		return player_graphic::Hit;
	case PM_SPELL:
		if (snapshot.currentAction.has_value()) {
			const SpellID spellId = static_cast<SpellID>(snapshot.currentAction->spellId);
			if (IsValidSpell(spellId)) {
				switch (GetSpellData(spellId).type()) {
				case MagicType::Fire:
					return player_graphic::Fire;
				case MagicType::Lightning:
					return player_graphic::Lightning;
				case MagicType::Magic:
					return player_graphic::Magic;
				}
			}
		}
		// An absent or older-server spell identity falls back safely to the
		// generic native magic family.
		return player_graphic::Magic;
	default:
		return player_graphic::Stand;
	}
}

void SetRemotePlayerAnimationFrame(Player &player, player_graphic graphic, uint64_t elapsedTicks, bool loop)
{
	int8_t numberOfFrames;
	int8_t ticksPerFrame;
	player.getAnimationFramesAndTicksPerFrame(graphic, numberOfFrames, ticksPerFrame);
	if (numberOfFrames <= 0 || ticksPerFrame <= 0)
		return;

	const uint64_t frame = elapsedTicks / static_cast<uint64_t>(ticksPerFrame);
	player.animInfo.currentFrame = static_cast<int8_t>(loop
	        ? frame % static_cast<uint64_t>(numberOfFrames)
	        : std::min<uint64_t>(frame, static_cast<uint64_t>(numberOfFrames - 1)));
	player.animInfo.tickCounterOfCurrentFrame = static_cast<int8_t>(elapsedTicks % static_cast<uint64_t>(ticksPerFrame));
}

void AdvanceProjectedPlayerAnimationInternal(Player &player, player_graphic graphic, uint64_t elapsedTicks, bool loop)
{
	int8_t numberOfFrames;
	int8_t ticksPerFrame;
	player.getAnimationFramesAndTicksPerFrame(graphic, numberOfFrames, ticksPerFrame);
	if (numberOfFrames <= 0 || ticksPerFrame <= 0)
		return;

	const uint64_t currentTicks = static_cast<uint64_t>(std::max<int8_t>(player.animInfo.currentFrame, 0)) * static_cast<uint64_t>(ticksPerFrame)
	    + static_cast<uint64_t>(std::max<int8_t>(player.animInfo.tickCounterOfCurrentFrame, 0));
	SetRemotePlayerAnimationFrame(player, graphic, currentTicks + elapsedTicks, loop);
}

void ApplyProjectedPlayerAnimationInternal(
	Player &player,
	const ProjectedPlayerSnapshot &snapshot,
	player_graphic graphic,
	bool reset,
	bool hasPreviousTick,
	uint64_t previousTick,
	uint64_t presentationTicksSincePreviousSnapshot)
{
	if (reset)
		player.setAnimation(graphic, player.direction);

	const bool loop = graphic == player_graphic::Stand || graphic == player_graphic::Walk;
	if (snapshot.currentAction.has_value()) {
		const uint64_t actionStart = snapshot.currentAction->startedAtTick;
		const uint64_t elapsed = snapshot.serverTick >= actionStart ? snapshot.serverTick - actionStart : 0;
		SetRemotePlayerAnimationFrame(player, graphic, elapsed, loop);
	} else if (reset) {
		SetRemotePlayerAnimationFrame(player, graphic, 0, loop);
	} else if (hasPreviousTick && snapshot.serverTick >= previousTick) {
		const uint64_t serverTicks = snapshot.serverTick - previousTick;
		const uint64_t unappliedServerTicks = serverTicks > presentationTicksSincePreviousSnapshot
		    ? serverTicks - presentationTicksSincePreviousSnapshot
		    : 0;
		AdvanceProjectedPlayerAnimationInternal(player, graphic, unappliedServerTicks, loop);
	}
}

void ApplyRemotePlayerSnapshot(Player &player, const ProjectedPlayerSnapshot &snapshot)
{
	player.plractive = true;
	player.friendlyMode = snapshot.friendlyMode;
	player.pManaShield = snapshot.manaShieldActive;
	player.pDiabloKillLevel = static_cast<uint8_t>(std::min<uint32_t>(snapshot.diabloKillLevel, UINT8_MAX));
	player.wReflections = static_cast<uint16_t>(snapshot.reflectCharges);
	player._pGold = snapshot.gold > static_cast<uint32_t>(std::numeric_limits<int>::max())
	    ? std::numeric_limits<int>::max()
	    : static_cast<int>(snapshot.gold);
	player._pExperience = snapshot.experience;
	player._pStatPts = snapshot.statPoints > static_cast<uint32_t>(std::numeric_limits<int>::max())
	    ? std::numeric_limits<int>::max()
	    : static_cast<int>(snapshot.statPoints);
	std::snprintf(player._pName, sizeof(player._pName), "%s", snapshot.characterName.c_str());
	player.life.current = snapshot.life;
	player.life.maximum = snapshot.lifeMaximum;
	player.life.maximumBase = snapshot.lifeMaximumBase;
	player.mana.current = snapshot.mana;
	player.mana.maximum = snapshot.manaMaximum;
	player.mana.maximumBase = snapshot.manaMaximumBase;
	player.UpdateHitPointPercentage();
	player.UpdateManaPercentage();
	player.attributes.strength = { snapshot.strength.base, snapshot.strength.current };
	player.attributes.magic = { snapshot.magic.base, snapshot.magic.current };
	player.attributes.dexterity = { snapshot.dexterity.base, snapshot.dexterity.current };
	player.attributes.vitality = { snapshot.vitality.base, snapshot.vitality.current };
	player._pAblSpells = snapshot.abilitySpellId != 0
	        && snapshot.abilitySpellId <= static_cast<uint32_t>(SpellID::LAST)
	    ? GetSpellBitmask(static_cast<SpellID>(snapshot.abilitySpellId))
	    : 0;
	player._pMemSpells = 0;
	std::fill(std::begin(player._pSplLvl), std::end(player._pSplLvl), 0);
	for (const auto &spell : snapshot.memorizedSpells) {
		if (spell.spellId == 0 || spell.spellId > static_cast<uint32_t>(SpellID::LAST))
			continue;
		player._pMemSpells |= GetSpellBitmask(static_cast<SpellID>(spell.spellId));
		player._pSplLvl[spell.spellId] = static_cast<uint8_t>(std::min<uint32_t>(spell.level, MaxSpellLevel));
	}
	const auto firstValidMemorizedSpell = std::find_if(snapshot.memorizedSpells.begin(), snapshot.memorizedSpells.end(), [](const ProjectedMemorizedSpell &spell) {
		return spell.spellId != 0 && spell.spellId <= static_cast<uint32_t>(SpellID::LAST);
	});
	if (firstValidMemorizedSpell != snapshot.memorizedSpells.end()) {
		player._pRSpell = static_cast<SpellID>(firstValidMemorizedSpell->spellId);
		player._pRSplType = SpellType::Spell;
	} else if (snapshot.abilitySpellId != 0
	    && snapshot.abilitySpellId <= static_cast<uint32_t>(SpellID::LAST)) {
		player._pRSpell = static_cast<SpellID>(snapshot.abilitySpellId);
		player._pRSplType = SpellType::Skill;
	} else {
		player._pRSpell = SpellID::Invalid;
		player._pRSplType = SpellType::Invalid;
	}
	player._pInfraFlag = std::any_of(snapshot.statusEffects.begin(), snapshot.statusEffects.end(), [](const ProjectedStatusEffect &effect) {
		return effect.behavior == 1;
	});
	player._pSpellFlags &= ~SpellFlag::Etherealize;
	if (std::any_of(snapshot.statusEffects.begin(), snapshot.statusEffects.end(), [](const ProjectedStatusEffect &effect) {
			return effect.behavior == 3;
		})) {
		player._pSpellFlags |= SpellFlag::Etherealize;
	}
	std::fill(std::begin(player._pLvlVisited), std::end(player._pLvlVisited), false);
	for (const uint32_t visitedLevelId : snapshot.visitedLevelIds) {
		if (visitedLevelId < NUMLEVELS)
			player._pLvlVisited[visitedLevelId] = true;
	}
	for (auto &item : player.InvBody)
		item.clear();
	for (auto &item : player.InvList)
		item.clear();
	for (auto &item : player.SpdList)
		item.clear();
	for (const auto &equipment : snapshot.equipment) {
		if (equipment.slot < NUM_INVLOC)
			player.InvBody[equipment.slot] = equipment.item;
	}
	for (const auto &beltItem : snapshot.belt) {
		if (beltItem.slot < MaxBeltItems)
			player.SpdList[beltItem.slot] = beltItem.item;
	}
	for (std::size_t index = 0; index < snapshot.inventory.size() && index < InventoryGridCells; ++index)
		player.InvList[index] = snapshot.inventory[index].item;
	player._pNumInv = static_cast<int>(std::min<std::size_t>(snapshot.inventory.size(), InventoryGridCells));
	player._pISpells = 0;
	player._pScrlSpells = 0;
	for (const Item &item : player.InvBody) {
		if (!item.isEmpty() && item._iStatFlag && IsValidSpell(item._iSpell) && item._iCharges != 0)
			player._pISpells |= GetSpellBitmask(item._iSpell);
	}
	for (const Item &item : player.SpdList) {
		if (item.isScroll() && item._iStatFlag && IsValidSpell(item._iSpell))
			player._pScrlSpells |= GetSpellBitmask(item._iSpell);
	}
	for (int index = 0; index < player._pNumInv; ++index) {
		const Item &item = player.InvList[index];
		if (item.isScroll() && item._iStatFlag && IsValidSpell(item._iSpell))
			player._pScrlSpells |= GetSpellBitmask(item._iSpell);
	}
	std::fill(std::begin(player.InvGrid), std::end(player.InvGrid), 0);
	if (snapshot.inventoryGrid.size() == InventoryGridCells) {
		for (std::size_t index = 0; index < InventoryGridCells; ++index) {
			const int32_t cell = snapshot.inventoryGrid[index];
			const int32_t clampedCell = std::clamp<int32_t>(cell, -1, std::numeric_limits<int8_t>::max() - 1);
			player.InvGrid[index] = clampedCell < 0 ? 0 : static_cast<int8_t>(clampedCell + 1);
		}
	}
	if (snapshot.heldItem.has_value())
		player.HoldItem = snapshot.heldItem->item;
	else
		player.HoldItem.clear();
	if (const uint8_t maximumLevel = player.getMaxCharacterLevel(); maximumLevel > 0)
		player.setCharacterLevel(static_cast<uint8_t>(std::min<uint32_t>(snapshot.characterLevel, maximumLevel)));
	player.position.tile = {
		static_cast<WorldTileCoord>(snapshot.positionX),
		static_cast<WorldTileCoord>(snapshot.positionY),
	};
	player.position.future = player.position.tile;
	if (snapshot.currentAction.has_value() && snapshot.currentAction->kind == 1
	    && !snapshot.currentAction->effectApplied
	    && snapshot.currentAction->directionX >= MinimumNativeDirection && snapshot.currentAction->directionX <= MaximumNativeDirection
	    && snapshot.currentAction->directionY >= MinimumNativeDirection && snapshot.currentAction->directionY <= MaximumNativeDirection
	    && (snapshot.currentAction->directionX != 0 || snapshot.currentAction->directionY != 0)) {
		player.position.future += Displacement {
		    static_cast<int8_t>(snapshot.currentAction->directionX),
		    static_cast<int8_t>(snapshot.currentAction->directionY),
		};
	}
	player.position.last = player.position.tile;
	player.position.old = player.position.tile;
	player.position.temp = player.position.tile;
	if (const auto setLevel = AuthoritativeNativeSetLevel(snapshot.levelId); setLevel.has_value())
		player.setLevel(*setLevel);
	else
		player.setLevel(static_cast<uint8_t>(snapshot.levelId));
	player.direction = GetRemotePlayerDirection(snapshot);
	player._pBlockFlag = snapshot.combatMode == static_cast<uint32_t>(PM_BLOCK);
	player._pmode = snapshot.life <= 0 || snapshot.combatMode == PM_DEATH
	    ? PM_DEATH
	    : snapshot.combatMode <= PM_QUIT ? static_cast<PLR_MODE>(snapshot.combatMode) : PM_STAND;
	// Remote players are presentation-only, but native lighting and automap
	// still consult this transition gate while drawing them.
	player._pLvlChanging = player._pmode == PM_NEWLVL;
}

void ApplyRemotePlayerEquipment(Player &player, const ProjectedPlayerSnapshot &snapshot)
{
	player.life.current = snapshot.life;
	player.life.maximum = snapshot.lifeMaximum;
	RefreshProjectedPlayerGraphics(player, snapshot);
}

/** Returns the native sprite family that can be presented without simulation. */
std::optional<MissileID> NativePresentationMissileId(
	int behavior,
	int nativeVisual,
	const std::string &nativeMissileId,
	int damageType)
{
	if (nativeVisual != 0) {
		switch (static_cast<protocol::NativeProjectileVisual>(nativeVisual)) {
		case protocol::NATIVE_PROJECTILE_VISUAL_ARROW:
			return MissileID::Arrow;
		case protocol::NATIVE_PROJECTILE_VISUAL_FIREBOLT:
			return MissileID::Firebolt;
		case protocol::NATIVE_PROJECTILE_VISUAL_LIGHTNING:
			return MissileID::Lightning;
		case protocol::NATIVE_PROJECTILE_VISUAL_THIN_LIGHTNING:
			return MissileID::ThinLightning;
		case protocol::NATIVE_PROJECTILE_VISUAL_FLASH_BOTTOM:
			return MissileID::FlashBottom;
		case protocol::NATIVE_PROJECTILE_VISUAL_FLASH_TOP:
			return MissileID::FlashTop;
		case protocol::NATIVE_PROJECTILE_VISUAL_RESURRECT_BEAM:
			return MissileID::ResurrectBeam;
		case protocol::NATIVE_PROJECTILE_VISUAL_PHASING:
			return MissileID::Phasing;
		case protocol::NATIVE_PROJECTILE_VISUAL_FIREBALL:
			return MissileID::Fireball;
		case protocol::NATIVE_PROJECTILE_VISUAL_HOLY_BOLT:
			return MissileID::HolyBolt;
		case protocol::NATIVE_PROJECTILE_VISUAL_BLOOD_STAR:
			return MissileID::BloodStar;
		case protocol::NATIVE_PROJECTILE_VISUAL_BONE_SPIRIT:
			return MissileID::BoneSpirit;
		case protocol::NATIVE_PROJECTILE_VISUAL_FIRE_WALL:
			return MissileID::FireWall;
		case protocol::NATIVE_PROJECTILE_VISUAL_FLAME_WAVE_CONTROL:
			return MissileID::FlameWaveControl;
		case protocol::NATIVE_PROJECTILE_VISUAL_FLAME_WAVE:
			return MissileID::FlameWave;
		case protocol::NATIVE_PROJECTILE_VISUAL_INFERNO:
			return MissileID::Inferno;
		case protocol::NATIVE_PROJECTILE_VISUAL_CHAIN_LIGHTNING:
			return MissileID::ChainLightning;
		case protocol::NATIVE_PROJECTILE_VISUAL_APOCALYPSE_BOOM:
			return MissileID::ApocalypseBoom;
		case protocol::NATIVE_PROJECTILE_VISUAL_ELEMENTAL:
			return MissileID::Elemental;
		case protocol::NATIVE_PROJECTILE_VISUAL_GUARDIAN:
			return MissileID::Guardian;
		case protocol::NATIVE_PROJECTILE_VISUAL_ETHEREALIZE:
			return MissileID::Etherealize;
		case protocol::NATIVE_PROJECTILE_VISUAL_ACID:
			return MissileID::Acid;
		case protocol::NATIVE_PROJECTILE_VISUAL_ACID_SPLAT:
			return MissileID::AcidSplat;
		case protocol::NATIVE_PROJECTILE_VISUAL_ACID_PUDDLE:
			return MissileID::AcidPuddle;
		case protocol::NATIVE_PROJECTILE_VISUAL_CHARGED_BOLT:
			return MissileID::ChargedBolt;
		case protocol::NATIVE_PROJECTILE_VISUAL_HORK_SPAWN:
			return MissileID::HorkSpawn;
		case protocol::NATIVE_PROJECTILE_VISUAL_NOVA_BALL:
			return MissileID::NovaBall;
		case protocol::NATIVE_PROJECTILE_VISUAL_LIGHTNING_WALL:
			return MissileID::LightningWall;
		case protocol::NATIVE_PROJECTILE_VISUAL_MONSTER_LIGHTNING:
			return MissileID::Lightning;
		case protocol::NATIVE_PROJECTILE_VISUAL_MONSTER_ORANGE_FLARE:
			return MissileID::OrangeFlare;
		case protocol::NATIVE_PROJECTILE_VISUAL_MONSTER_BLUE_FLARE:
			return MissileID::BlueFlare;
		case protocol::NATIVE_PROJECTILE_VISUAL_MONSTER_RED_FLARE:
			return MissileID::RedFlare;
		case protocol::NATIVE_PROJECTILE_VISUAL_MONSTER_YELLOW_FLARE:
			return MissileID::YellowFlare;
		case protocol::NATIVE_PROJECTILE_VISUAL_MONSTER_BLUE_FLARE2:
			return MissileID::BlueFlare2;
		case protocol::NATIVE_PROJECTILE_VISUAL_MONSTER_MAGMA_BALL:
			return MissileID::MagmaBall;
		default:
			// An explicit unknown identity must fail closed rather than infer a
			// different visual from gameplay behavior.
			return std::nullopt;
		}
	}

	if (!nativeMissileId.empty()) {
		if (behavior != static_cast<int>(ProjectileBehavior::MonsterMagic))
			return std::nullopt;
		if (nativeMissileId == "Lightning")
			return MissileID::Lightning;
		if (nativeMissileId == "OrangeFlare")
			return MissileID::OrangeFlare;
		if (nativeMissileId == "BlueFlare")
			return MissileID::BlueFlare;
		if (nativeMissileId == "RedFlare")
			return MissileID::RedFlare;
		if (nativeMissileId == "YellowFlare")
			return MissileID::YellowFlare;
		if (nativeMissileId == "BlueFlare2")
			return MissileID::BlueFlare2;
		if (nativeMissileId == "MagmaBall")
			return MissileID::MagmaBall;
		if (nativeMissileId == "HorkSpawn")
			return MissileID::HorkSpawn;
		// An explicit unknown identity must fail closed rather than infer a
		// different visual from the enum or gameplay behavior.
		return std::nullopt;
	}

	switch (static_cast<ProjectileBehavior>(behavior)) {
	case ProjectileBehavior::ChargedBolt:
		return MissileID::ChargedBolt;
	case ProjectileBehavior::Firebolt:
		return MissileID::Firebolt;
	case ProjectileBehavior::Lightning:
		return MissileID::Lightning;
	case ProjectileBehavior::LightningSegment:
		return MissileID::ThinLightning;
	case ProjectileBehavior::FlashBottom:
		return MissileID::FlashBottom;
	case ProjectileBehavior::FlashTop:
		return MissileID::FlashTop;
	case ProjectileBehavior::ResurrectionBeam:
		return MissileID::ResurrectBeam;
	case ProjectileBehavior::Phasing:
		return MissileID::Phasing;
	case ProjectileBehavior::Fireball:
		return MissileID::Fireball;
	case ProjectileBehavior::HolyBolt:
		return MissileID::HolyBolt;
	case ProjectileBehavior::BloodStar:
		return MissileID::BloodStar;
	case ProjectileBehavior::BoneSpirit:
		return MissileID::BoneSpirit;
	case ProjectileBehavior::NovaBall:
		return MissileID::NovaBall;
	case ProjectileBehavior::FireWallSegment:
		// Lightning Wall reuses the shared wall-segment behavior. Older
		// servers may omit native_visual, so retain the damage-channel
		// fallback for native presentation compatibility.
		return damageType == 2 ? MissileID::LightningWall : MissileID::FireWall;
	case ProjectileBehavior::FlameWaveController:
		return MissileID::FlameWaveControl;
	case ProjectileBehavior::FlameWaveSegment:
		return MissileID::FlameWave;
	case ProjectileBehavior::InfernoSegment:
		return MissileID::Inferno;
	case ProjectileBehavior::ChainLightningBolt:
		return MissileID::ChainLightning;
	case ProjectileBehavior::ApocalypseBoom:
		return MissileID::ApocalypseBoom;
	case ProjectileBehavior::Elemental:
		return MissileID::Elemental;
	case ProjectileBehavior::GuardianController:
		return MissileID::Guardian;
	case ProjectileBehavior::Etherealize:
		return MissileID::Etherealize;
	case ProjectileBehavior::Acid:
		return MissileID::Acid;
	case ProjectileBehavior::AcidSplat:
		return MissileID::AcidSplat;
	case ProjectileBehavior::AcidPuddle:
		return MissileID::AcidPuddle;
	case ProjectileBehavior::MonsterChargedBolt:
		return MissileID::ChargedBolt;
	case ProjectileBehavior::HorkSpawn:
		return MissileID::HorkSpawn;
	case ProjectileBehavior::WeaponArrow:
		return MissileID::Arrow;
	case ProjectileBehavior::MonsterArrow:
		return MissileID::Arrow;
	default:
		return std::nullopt;
	}
}

Missile *FindAuthoritativePresentationMissile(uint32_t entityId)
{
	const auto missile = std::find_if(Missiles.begin(), Missiles.end(), [entityId](const auto &candidate) {
		return candidate._miAuthoritativeEntityId == entityId;
	});
	return missile == Missiles.end() ? nullptr : &*missile;
}

Missile *CreateAuthoritativePresentationMissile(
	uint32_t entityId,
	MissileID missileId,
	const ProjectedProjectileSnapshot &snapshot)
{
	if (!IsMissileDataAvailable(missileId))
		return nullptr;
	if (Missiles.size() >= Missiles.max_size())
		return nullptr;

	Missiles.emplace_back();
	Missile &missile = Missiles.back();
	const auto &missileData = GetMissileData(missileId);
	const WorldTilePosition position { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) };
	missile._mitype = missileId;
	missile._misource = -1;
	missile._micaster = TARGET_BOTH;
	missile._miAnimAdd = 1;
	missile._miAnimType = missileData.graphic;
	missile._miDrawFlag = missileData.isDrawn();
	missile._miLightFlag = false;
	missile._mlid = NO_LIGHT;
	missile._miAuthoritativeEntityId = entityId;
	missile.position.tile = position;
	missile.position.start = position;
	missile.position.tileForRendering = position;
	missile.position.offset = {};
	missile.position.offsetForRendering = {};
	missile.position.velocity = {};
	missile.position.traveled = {};
	missile.setDefaultFrameGroup();
	return &missile;
}

void RemoveAuthoritativePresentationMissile(uint32_t entityId)
{
	Missiles.remove_if([entityId](const auto &missile) {
		return missile._miAuthoritativeEntityId == entityId;
	});
}

void ApplyAuthoritativePresentationMissile(
	Missile &missile,
	const ProjectedProjectileSnapshot &snapshot)
{
	const WorldTilePosition position { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) };
	// The server already resolved movement. Do not reuse native velocity or
	// collision interpolation, which would turn this presentation object into
	// a second local simulation.
	missile.position.tile = position;
	missile.position.start = position;
	missile.position.tileForRendering = position;
	missile.position.offset = {};
	missile.position.offsetForRendering = {};
	missile.position.velocity = {};
	missile.position.traveled = {};
	missile.duration = static_cast<int>(std::min<uint32_t>(snapshot.remainingTicks, std::numeric_limits<int>::max()));
	missile._miDelFlag = false;
	if (missile._mitype == MissileID::Guardian) {
		missile.setFrameGroup<GuardianFrame>(GuardianFrame::Idle);
	} else if (snapshot.originalDirection >= 0 && snapshot.originalDirection <= MaximumNativeMonsterDirection) {
		missile.setFrameGroupRaw(snapshot.originalDirection * 2);
	}
}

int FindActiveItemListIndex(unsigned itemSlot)
{
	for (int activeIndex = 0; activeIndex < ItemPoolAdapter::ActiveItemCountValue(); ++activeIndex) {
		if (ItemPoolAdapter::ActiveItemIds()[activeIndex] == itemSlot)
			return activeIndex;
	}
	return -1;
}

int FindActiveMonsterListIndex(unsigned monsterSlot)
{
	for (size_t activeIndex = 0; activeIndex < MonsterPoolAdapter::ActiveMonsterCountValue(); ++activeIndex) {
		if (MonsterPoolAdapter::ActiveMonsterIds()[activeIndex] == monsterSlot)
			return static_cast<int>(activeIndex);
	}
	return -1;
}

int FindActiveObjectListIndex(int objectSlot)
{
	for (int activeIndex = 0; activeIndex < ObjectPoolAdapter::ActiveObjectCountValue(); ++activeIndex) {
		if (ObjectPoolAdapter::ActiveObjectIndexAt(activeIndex) == objectSlot)
			return activeIndex;
	}
	return -1;
}

template <typename SlotMap, typename Slot>
void RebindPresentationSlot(SlotMap &slots, uint32_t entityId, Slot slot)
{
	for (auto it = slots.begin(); it != slots.end();) {
		if (it->first != entityId && it->second == slot)
			it = slots.erase(it);
		else
			++it;
	}
}

void ClearMonsterTile(unsigned monsterSlot, const Point &position)
{
	if (InDungeonBounds(position)
	    && std::abs(tileAt(position).monster()) == static_cast<int>(monsterSlot + 1)) {
		tileAt(position).setMonster(0);
	}
}

void ApplyMonsterSnapshot(Monster &monster, const ProjectedMonsterSnapshot &snapshot)
{
	const Point position { snapshot.positionX, snapshot.positionY };
	const Point futurePosition { snapshot.futurePositionX, snapshot.futurePositionY };
	const Point oldPosition { monster.position.tile.x, monster.position.tile.y };
	const Point oldFuturePosition { monster.position.future.x, monster.position.future.y };
	const Direction previousDirection = monster.direction;
	const Direction authoritativeDirection = static_cast<Direction>(snapshot.direction);
	ClearMonsterTile(static_cast<unsigned>(monster.getId()), oldPosition);
	if (oldFuturePosition != oldPosition)
		ClearMonsterTile(static_cast<unsigned>(monster.getId()), oldFuturePosition);
	monster.direction = authoritativeDirection;
	if (!snapshot.petrified && monster.mode == MonsterMode::Petrified)
		monster.startStand(monster.direction);
	if (snapshot.moving && !snapshot.petrified) {
		const MonsterMode movementMode = GetMonsterMovementMode(authoritativeDirection);
		if (monster.mode != movementMode || previousDirection != authoritativeDirection) {
			monster.mode = movementMode;
			// Keep the animation phase intact for consecutive snapshots that
			// describe the same movement; changing only the sprite direction is
			// sufficient when the authoritative direction changes.
			monster.changeAnimationData(MonsterGraphic::Walk, authoritativeDirection);
		}
	} else if (!snapshot.moving && !snapshot.petrified && IsMonsterModeMove(monster.mode)) {
		monster.mode = MonsterMode::Stand;
		const MonsterGraphic standingGraphic = monster.type().type == MT_GOLEM
		    ? MonsterGraphic::Walk
		    : MonsterGraphic::Stand;
		monster.changeAnimationData(standingGraphic, authoritativeDirection);
	}
	monster.position.old = position;
	monster.position.last = position;
	monster.position.future = futurePosition;
	monster.position.tile = position;
	// The server owns unique-monster dialogue progression. Keep the native
	// compatibility pool's talk identity synchronized for interaction affordances
	// without allowing an out-of-range server value to index the speech table.
	monster.talkMsg = snapshot.dialogueMessageId > 0
	        && static_cast<std::size_t>(snapshot.dialogueMessageId) < Speeches.size()
	    ? static_cast<_speech_id>(snapshot.dialogueMessageId)
	    : TEXT_NONE;
	// Authored unique-monster routes carry their encounter state explicitly.
	// This keeps the compatibility pool from inferring a terminal combat
	// transition from the native AI or quest-variable tables.
	switch (ResolveAuthoritativeMonsterDialoguePresentationState(snapshot.dialogueState)) {
	case AuthoritativeMonsterDialoguePresentationState::Inquiring:
		monster.goal = MonsterGoal::Inquiring;
		break;
	case AuthoritativeMonsterDialoguePresentationState::Combat:
		monster.goal = MonsterGoal::Normal;
		monster.activeForTicks = UINT8_MAX;
		if (monster.mode == MonsterMode::Talk)
			monster.startStand(monster.direction);
		break;
	case AuthoritativeMonsterDialoguePresentationState::Legacy:
		break;
	}
	if (snapshot.alive) {
		monster.occupyTile(position, false);
		if (snapshot.moving)
			monster.occupyTile(futurePosition, true);
	}
	monster.animInfo.isPetrified = snapshot.petrified;
	if (snapshot.petrified)
		monster.mode = MonsterMode::Petrified;
	const int32_t hitPointsQ6 = snapshot.hitPointsQ6 == 0 && snapshot.hitPoints != 0
	    ? static_cast<int32_t>(static_cast<int64_t>(snapshot.hitPoints) * 64)
	    : snapshot.hitPointsQ6;
	const int32_t maxHitPointsQ6 = snapshot.maxHitPointsQ6 == 0 && snapshot.maxHitPoints != 0
	    ? static_cast<int32_t>(static_cast<int64_t>(snapshot.maxHitPoints) * 64)
	    : snapshot.maxHitPointsQ6;
	monster.hitPoints = std::max(0, hitPointsQ6);
	monster.maxHitPoints = std::max(monster.hitPoints, std::max(0, maxHitPointsQ6));
	monster.armorClass = static_cast<uint8_t>(std::clamp(snapshot.armorClass, 0, 255));
	monster.minDamage = static_cast<uint8_t>(std::clamp(snapshot.attackDamage, 0, 255));
	monster.maxDamage = monster.minDamage;
		monster.activeForTicks = static_cast<uint8_t>(std::clamp(snapshot.activeForTicks, 0, MaximumNativeMonsterActiveTicks));
		monster.packSize = static_cast<uint8_t>(std::clamp(snapshot.packSize, 0, MaximumNativeMonsterPackSize));
	// Resolve the entity-based leader reference after all projected slots have
	// been assigned. A hidden leader is intentionally treated as an ordinary
	// presentation monster so native compatibility code never follows a null
	// pointer from an incomplete visual projection.
	monster.leader = Monster::NoLeader;
	monster.leaderRelation = LeaderRelation::None;
}

void ApplyItemSnapshot(Item &item, const ProjectedWorldItemSnapshot &snapshot)
{
	const auto animationInfo = item.animInfo;
	const auto animationFlag = item._iAnimFlag;
	const auto selectionRegion = item.selectionRegion;
	const auto postDraw = item._iPostDraw;
	item = snapshot.item;
	item.position = { snapshot.positionX, snapshot.positionY };
	item.animInfo = animationInfo;
	item._iAnimFlag = animationFlag;
	item.selectionRegion = selectionRegion;
	item._iPostDraw = postDraw;
}

tl::expected<uint32_t, std::string> ResolveObjectAnimationFrameInternal(const Object &object, const ProjectedObjectSnapshot &snapshot)
{
	const uint32_t requestedFrame = snapshot.activated ? snapshot.activatedAnimationFrame : snapshot.animationFrame;
	if (HeadlessMode)
		return requestedFrame == 0 ? std::max(object._oAnimFrame, 1U) : requestedFrame;
	if (!object._oAnimData) {
		const int objectType = static_cast<int>(object._otype);
		const int graphic = objectType >= 0 && static_cast<size_t>(objectType) < AllObjects.size()
		    ? static_cast<int>(AllObjects[static_cast<_object_id>(objectType)].ofindex)
		    : -1;
		return tl::make_unexpected(
		    "Authoritative object presentation requires native art that is not loaded (object type "
		    + std::to_string(objectType) + ", graphic " + std::to_string(graphic) + ").");
	}

	const uint32_t frameCount = object._oAnimData->numSprites();
	if (frameCount == 0)
		return tl::make_unexpected("Authoritative object presentation loaded an empty native sprite list.");
	if (requestedFrame > frameCount)
		return tl::make_unexpected("Authoritative object presentation selected a native frame outside the loaded sprite list.");
	if (requestedFrame == 0)
		return std::clamp(object._oAnimFrame, 1U, frameCount);
	return requestedFrame;
}

tl::expected<void, std::string> ApplyObjectSnapshot(Object &object, int objectSlot, const ProjectedObjectSnapshot &snapshot)
{
	const auto animationFrame = ResolveAuthoritativeObjectAnimationFrame(object, snapshot);
	if (!animationFrame.has_value())
		return tl::make_unexpected(animationFrame.error());
	const Point position { snapshot.positionX, snapshot.positionY };
	const bool shouldOccupyTile = !snapshot.activateOnOccupancy
	    && (!snapshot.activated || snapshot.keepVisibleAfterActivation)
	    && !snapshot.destroyed;
	const auto clearOwnedTile = [objectSlot](const Point tilePosition) {
		if (InDungeonBounds(tilePosition) && tileAt(tilePosition).object() == objectSlot + 1)
			tileAt(tilePosition).setObject(0);
	};
	if (object.position != position) {
		clearOwnedTile(object.position);
		object.position = position;
	}
	if (shouldOccupyTile)
		tileAt(position).setObject(objectSlot + 1);
	else {
		// Activation and destruction can leave the object at the same position.
		// Clear that tile explicitly so stale native occupancy cannot survive a
		// server snapshot transition.
		clearOwnedTile(position);
		if (ObjectUnderCursor == &object)
			ObjectUnderCursor = nullptr;
	}
	object._oDelFlag = snapshot.destroyed || snapshot.activated && !snapshot.keepVisibleAfterActivation;
	object._oAnimFrame = *animationFrame;
	if (snapshot.cathedralDoor.has_value()) {
		// Typed Cathedral doors carry the complete native presentation state. Do
		// not call SyncDoor: the server already owns the effective d-piece map.
		const auto &door = *snapshot.cathedralDoor;
		object._oDoorFlag = true;
		object._oVar1 = static_cast<int>(door.cachedPiece);
		object._oVar2 = static_cast<int>(door.cachedNeighborPiece);
		object._oVar4 = door.doorState;
		object._oAnimFrame = door.animationFrame;
		object._oPreFlag = door.preDraw;
		object._oMissFlag = door.missilePassable;
		object._oSolidFlag = door.solid;
		object.selectionRegion = static_cast<SelectionRegion>(door.selectionRegion);
		if (InDungeonBounds(position))
			tileAt(position).setSpecial(door.doorState == 0 ? 0 : door.nativeObjectId == 1 ? 7 : 8);
	} else if (object.isDoor()) {
		// Match only the native draw/selection state. Door map mutations and
		// collision decisions still belong to the authoritative server.
		// Native tooltip states use 0 for closed and 1 for open.
		object._oVar4 = static_cast<int>(snapshot.activated);
		object._oPreFlag = snapshot.activated;
		object.selectionRegion = snapshot.activated
		    ? SelectionRegion::Middle
		    : SelectionRegion::Bottom | SelectionRegion::Middle;
	}
	object._oTrapFlag = snapshot.trapArmed;
	object._oMissFlag = snapshot.missilePassable;
	object._oBreak = snapshot.destroyed && snapshot.breakable ? -1 : snapshot.breakable ? 1 : 0;
	return {};
}

void RemoveMappedItem(unsigned itemSlot)
{
	if (const int activeIndex = FindActiveItemListIndex(itemSlot); activeIndex >= 0) {
		if (InDungeonBounds(Items[itemSlot].position) && tileAt(Items[itemSlot].position).item() == static_cast<int>(itemSlot + 1))
			tileAt(Items[itemSlot].position).setItem(0);
		DeleteItem(activeIndex);
	}
}

void RemoveMappedMonster(unsigned monsterSlot)
{
	if (const int activeIndex = FindActiveMonsterListIndex(monsterSlot); activeIndex >= 0) {
		const auto &monster = Monsters[monsterSlot];
		ClearMonsterTile(monsterSlot, monster.position.tile);
		if (monster.position.future != monster.position.tile)
			ClearMonsterTile(monsterSlot, monster.position.future);
		DeleteMonsterAtActiveIndex(activeIndex);
	}
}

void RemoveUnclaimedMonsterSlots(const std::unordered_set<unsigned> &claimedSlots)
{
	for (int activeIndex = static_cast<int>(MonsterPoolAdapter::ActiveMonsterCountValue()) - 1; activeIndex >= 0; --activeIndex) {
		const unsigned monsterSlot = MonsterPoolAdapter::ActiveMonsterIds()[static_cast<size_t>(activeIndex)];
		if (claimedSlots.contains(monsterSlot))
			continue;

		const Monster &monster = Monsters[monsterSlot];
		ClearMonsterTile(monsterSlot, monster.position.tile);
		if (monster.position.future != monster.position.tile)
			ClearMonsterTile(monsterSlot, monster.position.future);
		DeleteMonsterAtActiveIndex(static_cast<size_t>(activeIndex));
	}
}

void RemoveUnclaimedItemSlots(const std::unordered_set<unsigned> &claimedSlots)
{
	for (int activeIndex = ItemPoolAdapter::ActiveItemCountValue() - 1; activeIndex >= 0; --activeIndex) {
		const unsigned itemSlot = ItemPoolAdapter::ActiveItemIds()[static_cast<size_t>(activeIndex)];
		if (claimedSlots.contains(itemSlot))
			continue;

		RemoveMappedItem(itemSlot);
	}
}

void RemoveMappedObject(int objectSlot)
{
	if (const int activeIndex = FindActiveObjectListIndex(objectSlot); activeIndex >= 0) {
		if (InDungeonBounds(Objects[objectSlot].position) && tileAt(Objects[objectSlot].position).object() == objectSlot + 1)
			tileAt(Objects[objectSlot].position).setObject(0);
		if (ObjectUnderCursor == &Objects[objectSlot])
			ObjectUnderCursor = nullptr;
		ObjectPoolAdapter::ReleaseObjectSlot(objectSlot, activeIndex);
	}
}

void RemoveUnclaimedObjectSlots(const std::unordered_set<int> &claimedSlots)
{
	for (int activeIndex = ObjectPoolAdapter::ActiveObjectCountValue() - 1; activeIndex >= 0; --activeIndex) {
		const int objectSlot = ObjectPoolAdapter::ActiveObjectIndexAt(activeIndex);
		if (claimedSlots.contains(objectSlot))
			continue;

		RemoveMappedObject(objectSlot);
	}
}

tl::expected<void, std::string> ApplyAuthoritativeNativeGeometry(
	const ProjectedLevelGeometrySnapshot *geometry,
	uint32_t levelId)
{
	if (geometry == nullptr || geometry->levelId != levelId)
		return {};
	if (geometry->width < 0 || geometry->height < 0)
		return tl::make_unexpected("Authoritative level geometry has negative dimensions.");

	// Native dungeon arrays are fixed-size. Keep the presentation bridge from
	// indexing outside them even when a projection was constructed directly
	// instead of passing through protocol validation.
	if (geometry->width > MAXDUNX || geometry->height > MAXDUNY)
		return tl::make_unexpected("Authoritative level geometry exceeds the native dungeon dimensions.");
	const auto coarseWidth = (static_cast<uint64_t>(geometry->width) + 1) / 2;
	const auto coarseHeight = (static_cast<uint64_t>(geometry->height) + 1) / 2;
	if (!geometry->nativeMegaTileLayer.empty()
	    && (coarseWidth > DMAXX || coarseHeight > DMAXY))
		return tl::make_unexpected("Authoritative level geometry exceeds the native megatile dimensions.");

	const auto cellCount = static_cast<uint64_t>(geometry->width) * static_cast<uint64_t>(geometry->height);
	const auto coarseCellCount = coarseWidth * coarseHeight;
	const auto hasInvalidSize = [cellCount, coarseCellCount](const ProjectedLevelGeometrySnapshot &candidate) {
		return (candidate.width == 0 || candidate.height == 0)
		        ? (!candidate.nativeTransparency.empty()
		            || !candidate.nativeObjectLayer.empty()
		            || !candidate.nativeMegaTileLayer.empty()
		            || !candidate.nativePieceLayer.empty()
		            || !candidate.nativeMonsterLayer.empty()
		            || !candidate.nativeSpecialLayer.empty())
		        : (( !candidate.nativeTransparency.empty() && candidate.nativeTransparency.size() != cellCount)
		            || (!candidate.nativeObjectLayer.empty() && candidate.nativeObjectLayer.size() != cellCount)
		            || (!candidate.nativeMegaTileLayer.empty() && candidate.nativeMegaTileLayer.size() != coarseCellCount)
		            || (!candidate.nativePieceLayer.empty() && candidate.nativePieceLayer.size() != cellCount)
		            || (!candidate.nativeMonsterLayer.empty() && candidate.nativeMonsterLayer.size() != cellCount)
		            || (!candidate.nativeSpecialLayer.empty() && candidate.nativeSpecialLayer.size() != cellCount));
	};
	if (hasInvalidSize(*geometry))
		return tl::make_unexpected("Authoritative level geometry layer dimensions do not match the projected level.");

	if (std::any_of(geometry->nativeTransparency.begin(), geometry->nativeTransparency.end(), [](uint16_t value) {
			return value > std::numeric_limits<uint8_t>::max();
		})
	    || std::any_of(geometry->nativeMegaTileLayer.begin(), geometry->nativeMegaTileLayer.end(), [](uint16_t value) {
			return value > std::numeric_limits<uint8_t>::max();
		})
	    || std::any_of(geometry->nativeObjectLayer.begin(), geometry->nativeObjectLayer.end(), [](uint16_t value) {
			return value > DUN_OBJECT_ID_LAST;
		})
	    || std::any_of(geometry->nativeMonsterLayer.begin(), geometry->nativeMonsterLayer.end(), [](uint16_t value) {
			return value > NativeMaximumDunMonsterLayerId;
		})
	    || std::any_of(geometry->nativeSpecialLayer.begin(), geometry->nativeSpecialLayer.end(), [](uint16_t value) {
			return value > std::numeric_limits<uint8_t>::max();
		}))
		return tl::make_unexpected("Authoritative level geometry contains a value outside the native presentation range.");

	if (!geometry->nativeMegaTileLayer.empty()) {
		for (int y = 0; y < static_cast<int>(coarseHeight); ++y) {
			for (int x = 0; x < static_cast<int>(coarseWidth); ++x)
				megaTileAt(x, y).setCurrent(static_cast<uint8_t>(geometry->nativeMegaTileLayer[static_cast<size_t>(y) * coarseWidth + x]));
		}
	}
	if (!geometry->nativePieceLayer.empty()) {
		for (int y = 0; y < geometry->height; ++y) {
			for (int x = 0; x < geometry->width; ++x)
				tileAt(x, y).setPiece(geometry->nativePieceLayer[static_cast<size_t>(y) * geometry->width + x]);
		}
	}
	if (!geometry->nativeTransparency.empty()) {
		for (int y = 0; y < geometry->height; ++y) {
			for (int x = 0; x < geometry->width; ++x)
				tileAt(x, y).setTransVal(static_cast<int8_t>(geometry->nativeTransparency[static_cast<size_t>(y) * geometry->width + x]));
		}
	}
	if (!geometry->nativeSpecialLayer.empty()) {
		for (int y = 0; y < geometry->height; ++y) {
			for (int x = 0; x < geometry->width; ++x)
				tileAt(x, y).setSpecial(static_cast<uint8_t>(geometry->nativeSpecialLayer[static_cast<size_t>(y) * geometry->width + x]));
		}
	}

	// nativeObjectLayer values are DUN object-layer IDs, not authoritative
	// object identities. Authored object snapshots are applied below so that
	// object behavior cannot be reconstructed locally from presentation data.
	return {};
}

} // namespace

tl::expected<uint32_t, std::string> ResolveAuthoritativeObjectAnimationFrame(const Object &object, const ProjectedObjectSnapshot &snapshot)
{
	return ResolveObjectAnimationFrameInternal(object, snapshot);
}

uint32_t NativeObjectPresentationId(const ProjectedObjectSnapshot &snapshot)
{
	// Authored semantic families own the native object template selected by the
	// compatibility client. Only explicit native families and older unspecified
	// snapshots may use native_object_id as a presentation fallback.
	if (UsesAuthoredObjectPresentationIdentity(
		static_cast<ObjectPresentationKind>(snapshot.presentationKind)))
		return snapshot.objectId;
	return snapshot.nativeObjectId == 0 ? snapshot.objectId : snapshot.nativeObjectId;
}

player_graphic GetRemotePlayerGraphic(const ProjectedPlayerSnapshot &snapshot)
{
	return GetRemotePlayerGraphicInternal(snapshot);
}

uint8_t GetRemotePlayerEquipmentGraphicBits(const ProjectedPlayerSnapshot &snapshot)
{
	return GetRemotePlayerEquipmentGraphicBitsInternal(snapshot);
}

void RefreshProjectedPlayerGraphics(Player &player, const ProjectedPlayerSnapshot &snapshot)
{
	const uint8_t graphicBits = GetRemotePlayerEquipmentGraphicBitsInternal(snapshot);
	player._pgfxnum = graphicBits;
	player.resetGraphics();
	player.setAnimations();
	player.initGraphics();
	// initGraphics intentionally removes the armor bits from a death sprite.
	// Keep the authoritative equipment identity on the player so a later live
	// snapshot can detect the transition without restarting every death tick.
	player._pgfxnum = graphicBits;
}

void ApplyProjectedPlayerAnimation(
	Player &player,
	const ProjectedPlayerSnapshot &snapshot,
	player_graphic graphic,
	bool reset,
	bool hasPreviousTick,
	uint64_t previousTick,
	uint64_t presentationTicksSincePreviousSnapshot)
{
	ApplyProjectedPlayerAnimationInternal(
	    player,
	    snapshot,
	    graphic,
	    reset,
	    hasPreviousTick,
	    previousTick,
	    presentationTicksSincePreviousSnapshot);
}

void AdvanceProjectedPlayerAnimation(Player &player, player_graphic graphic, uint64_t elapsedTicks, bool loop)
{
	AdvanceProjectedPlayerAnimationInternal(player, graphic, elapsedTicks, loop);
}

bool IsRemotePlayerOnActiveLevel(uint32_t projectedLevelId, const Player &player)
{
	return projectedLevelId == 0 || player.isOnActiveLevel();
}

bool IsAuthoritativeMonsterPresentable(const ProjectedMonsterSnapshot &snapshot)
{
	return snapshot.alive && !snapshot.sneakHidden;
}

AuthoritativeMonsterDialoguePresentationState ResolveAuthoritativeMonsterDialoguePresentationState(int32_t dialogueState) noexcept
{
	switch (dialogueState) {
	case 1:
		return AuthoritativeMonsterDialoguePresentationState::Inquiring;
	case 2:
		return AuthoritativeMonsterDialoguePresentationState::Combat;
	default:
		return AuthoritativeMonsterDialoguePresentationState::Legacy;
	}
}

tl::expected<void, std::string> ServerBackedWorldPresentation::Apply(
    const ServerBackedWorldProjection &projection,
    uint32_t levelId,
    const std::vector<ProjectedPlayerSnapshot> &players,
    uint32_t localEntityId)
{
	if (const auto serverTick = projection.ServerTick(); serverTick.has_value()
	    && lastWorldServerTick_.has_value()
	    && *serverTick < *lastWorldServerTick_)
		return {};
	if (auto result = ApplyAuthoritativeNativeGeometry(projection.CurrentLevelGeometry(), levelId); !result.has_value())
		return result;

	std::unordered_set<uint32_t> currentMonsterEntities;
	std::unordered_set<uint32_t> currentItemEntities;
	std::unordered_set<uint32_t> currentObjectEntities;
	std::unordered_set<uint32_t> currentProjectileEntities;
	std::unordered_set<unsigned> claimedMonsterSlots;
	std::unordered_set<unsigned> claimedItemSlots;
	std::unordered_set<int> claimedObjectSlots;
	const ProjectedPlayerSnapshot *localViewer = nullptr;
	if (!players.empty()) {
		const auto local = std::find_if(players.begin(), players.end(), [localEntityId](const auto &player) {
			return localEntityId != 0 && player.entityId == localEntityId;
		});
		localViewer = local == players.end() ? &players.front() : &*local;
	}
	const bool hasLocalViewer = localViewer != nullptr
	    && IsCurrentLevel(localViewer->levelId, levelId);
	const auto isMonsterVisible = [&projection, localViewer, hasLocalViewer, levelId](const ProjectedMonsterSnapshot &snapshot) {
		if (!IsAuthoritativeMonsterPresentable(snapshot))
			return false;
		if (!hasLocalViewer || !IsCurrentLevel(snapshot.levelId, levelId))
			return true;
		const auto visible = projection.IsVisionVisibleFrom(
		    localViewer->positionX,
		    localViewer->positionY,
		    snapshot.positionX,
		    snapshot.positionY,
		    localViewer->lightRadius);
		return !visible.has_value() || *visible;
	};

	for (const auto &snapshot : projection.Monsters()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative monster position cannot be represented by the native world tile type.");
		if (snapshot.moving && !IsValidWorldPosition(snapshot.futurePositionX, snapshot.futurePositionY))
			return tl::make_unexpected("Authoritative moving monster destination cannot be represented by the native world tile type.");
		// Godot omits dead monsters from presentation. Do the same here instead
		// of retaining a previously mapped compatibility slot with stale animation
		// state; RemoveUnclaimedMonsterSlots below removes that slot and the mapping
		// cleanup at the end of this method drops its entity identity.
		if (!isMonsterVisible(snapshot))
			continue;
		currentMonsterEntities.insert(snapshot.entityId);
		unsigned slot = std::numeric_limits<unsigned>::max();
		if (const auto mapped = monsterSlots_.find(snapshot.entityId); mapped != monsterSlots_.end()
		    && FindActiveMonsterListIndex(mapped->second) >= 0)
			slot = mapped->second;
		if (slot == std::numeric_limits<unsigned>::max()) {
			for (const auto activeSlot : MonsterPoolAdapter::ActiveMonsterRange()) {
				if (claimedMonsterSlots.contains(activeSlot))
					continue;
				if (static_cast<uint32_t>(Monsters[activeSlot].type().type) == snapshot.monsterId) {
					slot = activeSlot;
					break;
				}
			}
		}
		if (slot == std::numeric_limits<unsigned>::max() && snapshot.alive && MonsterPoolAdapter::HasFreeMonsterSlot()) {
			if (const auto typeIndex = FindNativeMonsterTypeIndex(snapshot.monsterId); typeIndex.has_value()) {
				if (Monster *monster = AddMonster({ snapshot.positionX, snapshot.positionY }, Direction::South, *typeIndex, true); monster != nullptr)
					slot = static_cast<unsigned>(monster->getId());
			}
		}
		if (slot == std::numeric_limits<unsigned>::max())
			continue;
		claimedMonsterSlots.insert(slot);
		RebindPresentationSlot(monsterSlots_, snapshot.entityId, slot);
		monsterSlots_[snapshot.entityId] = slot;
		ApplyMonsterSnapshot(Monsters[slot], snapshot);
	}
	for (const auto &snapshot : projection.Monsters()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId)
		    || !isMonsterVisible(snapshot)
		    || snapshot.leaderEntityId == 0)
			continue;
		const auto member = monsterSlots_.find(snapshot.entityId);
		const auto leader = monsterSlots_.find(snapshot.leaderEntityId);
		if (member == monsterSlots_.end() || leader == monsterSlots_.end()
		    || leader->second > std::numeric_limits<uint8_t>::max())
			continue;
		Monsters[member->second].leader = static_cast<uint8_t>(leader->second);
		Monsters[member->second].leaderRelation = static_cast<LeaderRelation>(snapshot.leaderRelation);
	}
	// Native level loading still creates compatibility-pool monsters so their
	// presentation data is available. They are not authoritative, however, and
	// any slot not claimed by this snapshot must be removed before interaction or
	// rendering can observe a local ghost.
	RemoveUnclaimedMonsterSlots(claimedMonsterSlots);

	for (const auto &snapshot : projection.WorldItems()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative item position cannot be represented by the native world tile type.");
		currentItemEntities.insert(snapshot.entityId);
		unsigned slot = std::numeric_limits<unsigned>::max();
		if (const auto mapped = itemSlots_.find(snapshot.entityId); mapped != itemSlots_.end()
		    && FindActiveItemListIndex(mapped->second) >= 0)
			slot = mapped->second;
		if (slot == std::numeric_limits<unsigned>::max()) {
			for (const auto activeSlot : ItemPoolAdapter::ActiveItemIds().subspan(0, ItemPoolAdapter::ActiveItemCountValue())) {
				if (claimedItemSlots.contains(activeSlot))
					continue;
				if (Items[activeSlot]._iSeed == snapshot.itemSeed) {
					slot = activeSlot;
					break;
				}
			}
		}
		if (slot == std::numeric_limits<unsigned>::max()) {
			if (!ItemPoolAdapter::HasFreeItemSlot())
				continue;
			slot = PlaceItemInWorld(Item { snapshot.item }, { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) });
		}
		claimedItemSlots.insert(slot);
		RebindPresentationSlot(itemSlots_, snapshot.entityId, slot);
		itemSlots_[snapshot.entityId] = slot;
		ApplyItemSnapshot(Items[slot], snapshot);
	}
	// Native level loading may leave compatibility-pool items behind when the
	// authoritative snapshot omits them. Remove those slots so stale floor
	// items cannot remain visible or interactable on the connected client.
	RemoveUnclaimedItemSlots(claimedItemSlots);

	// Release omitted or changed templates before admitting replacements. A
	// valid full-capacity snapshot must not require an extra temporary slot.
	for (auto mapped = objectSlots_.begin(); mapped != objectSlots_.end();) {
		const auto retained = std::find_if(projection.Objects().begin(), projection.Objects().end(), [&](const auto &snapshot) {
			return IsCurrentLevel(snapshot.levelId, levelId) && snapshot.entityId == mapped->first
			    && static_cast<uint32_t>(Objects[mapped->second]._otype) == NativeObjectPresentationId(snapshot);
		});
		if (retained == projection.Objects().end()) {
			// A native reload may reuse the same slot for a different entity.
			// Release its stale mapping without deleting an incoming entity's
			// already matching template/position.
			const Object &object = Objects[mapped->second];
			const bool rebindsExistingSlot = std::any_of(projection.Objects().begin(), projection.Objects().end(), [&](const auto &snapshot) {
				return IsCurrentLevel(snapshot.levelId, levelId)
				    && static_cast<uint32_t>(object._otype) == NativeObjectPresentationId(snapshot)
				    && object.position == Point { snapshot.positionX, snapshot.positionY };
			});
			if (!rebindsExistingSlot)
				RemoveMappedObject(mapped->second);
			mapped = objectSlots_.erase(mapped);
		} else {
			++mapped;
		}
	}
	for (const auto &snapshot : projection.Objects()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative object position cannot be represented by the native world tile type.");
		currentObjectEntities.insert(snapshot.entityId);
		const uint32_t nativeObjectId = NativeObjectPresentationId(snapshot);
		if (nativeObjectId > static_cast<uint32_t>(OBJ_LAST))
			return tl::make_unexpected("Authoritative object selected an unsupported native presentation template.");
		if (!HeadlessMode && nativeObjectId >= AllObjects.size())
			return tl::make_unexpected("Authoritative object presentation requires native template data that is not loaded.");
		if (!HeadlessMode && !IsObjectGraphicLoaded(AllObjects[static_cast<_object_id>(nativeObjectId)].ofindex)) {
			return tl::make_unexpected(
			    "Authoritative object presentation requires native art that is not loaded (level "
			    + std::to_string(levelId) + ", entity " + std::to_string(snapshot.entityId)
			    + ", native object " + std::to_string(nativeObjectId) + ", graphic "
			    + std::to_string(AllObjects[static_cast<_object_id>(nativeObjectId)].ofindex) + ").");
		}
		int slot = -1;
		bool admittedSlot = false;
		if (const auto mapped = objectSlots_.find(snapshot.entityId); mapped != objectSlots_.end()
		    && FindActiveObjectListIndex(mapped->second) >= 0
		    && static_cast<uint32_t>(Objects[mapped->second]._otype) == nativeObjectId)
			slot = mapped->second;
		if (slot < 0) {
			const Point projectedPosition { snapshot.positionX, snapshot.positionY };
			int positionMatchedSlot = -1;
			for (int activeIndex = 0; activeIndex < ObjectPoolAdapter::ActiveObjectCountValue(); ++activeIndex) {
				const int candidate = ObjectPoolAdapter::ActiveObjectIndexAt(activeIndex);
				if (claimedObjectSlots.contains(candidate))
					continue;
				if (static_cast<uint32_t>(Objects[candidate]._otype) != nativeObjectId)
					continue;
				if (Objects[candidate].position == projectedPosition) {
					slot = candidate;
					break;
				}
				if (positionMatchedSlot < 0)
					positionMatchedSlot = candidate;
			}
			if (slot < 0)
				slot = positionMatchedSlot;
		}
		if (slot < 0 && !ObjectPoolAdapter::HasFreeObjectSlot()) {
			// Native level loading can leave an entirely full compatibility pool.
			// Reclaim only an unclaimed, unmapped slot; never evict another server
			// entity that has yet to be applied in this snapshot.
			for (int index = ObjectPoolAdapter::ActiveObjectCountValue() - 1; index >= 0; --index) {
				const int candidate = ObjectPoolAdapter::ActiveObjectIndexAt(index);
				if (claimedObjectSlots.contains(candidate)
				    || std::any_of(objectSlots_.begin(), objectSlots_.end(), [candidate](const auto &entry) { return entry.second == candidate; }))
					continue;
				RemoveMappedObject(candidate);
				break;
			}
		}
		if (slot < 0 && ObjectPoolAdapter::HasFreeObjectSlot()) {
			if (Object *object = AddObjectPresentation(static_cast<_object_id>(nativeObjectId), { snapshot.positionX, snapshot.positionY }); object != nullptr) {
				slot = static_cast<int>(object - Objects);
				admittedSlot = true;
			}
		}
		if (slot < 0)
			return tl::make_unexpected("Authoritative object presentation could not acquire a native object slot.");
		if (!HeadlessMode && !Objects[slot]._oAnimData) {
			if (admittedSlot)
				RemoveMappedObject(slot);
			return tl::make_unexpected("Authoritative object presentation acquired a slot without native art.");
		}
		if (auto result = ApplyObjectSnapshot(Objects[slot], slot, snapshot); !result.has_value()) {
			// A newly admitted slot is not in objectSlots_ yet, so Clear() cannot
			// discover it after a malformed frame or sprite-list failure.
			if (admittedSlot)
				RemoveMappedObject(slot);
			return result;
		}
		claimedObjectSlots.insert(slot);
		RebindPresentationSlot(objectSlots_, snapshot.entityId, slot);
		objectSlots_[snapshot.entityId] = slot;
	}
	// Native level loading may also leave compatibility-pool objects behind.
	// Only objects claimed by the current authoritative snapshot may remain in
	// the connected presentation pool.
	RemoveUnclaimedObjectSlots(claimedObjectSlots);

	for (const auto &snapshot : projection.Projectiles()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		const auto missileId = NativePresentationMissileId(
			snapshot.behavior,
			snapshot.nativeVisual,
			snapshot.nativeMissileId,
			snapshot.damageType);
		if (!missileId)
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative projectile position cannot be represented by the native world tile type.");

		Missile *missile = FindAuthoritativePresentationMissile(snapshot.entityId);
		if (missile != nullptr && missile->_mitype != *missileId) {
			RemoveAuthoritativePresentationMissile(snapshot.entityId);
			missile = nullptr;
		}
		if (missile == nullptr)
			missile = CreateAuthoritativePresentationMissile(snapshot.entityId, *missileId, snapshot);
		if (missile == nullptr)
			continue;
		ApplyAuthoritativePresentationMissile(*missile, snapshot);
		currentProjectileEntities.insert(snapshot.entityId);
		projectileEntities_.insert(snapshot.entityId);
	}

	std::unordered_set<uint32_t> retainedRemotePlayers;
	for (const auto &snapshot : players) {
		if (snapshot.entityId == 0 || snapshot.entityId == localEntityId
		    || !IsValidPlayerClass(snapshot.characterClassId)) {
			continue;
		}
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative remote player position cannot be represented by the native world tile type.");
		if (snapshot.currentAction.has_value()
		    && snapshot.currentAction->kind == 1
		    && !snapshot.currentAction->effectApplied) {
			const auto &action = *snapshot.currentAction;
			if (action.directionX < MinimumNativeDirection || action.directionX > MaximumNativeDirection
			    || action.directionY < MinimumNativeDirection || action.directionY > MaximumNativeDirection
			    || (action.directionX == 0 && action.directionY == 0)
			    || !IsValidWorldPosition(snapshot.positionX + action.directionX, snapshot.positionY + action.directionY))
				return tl::make_unexpected("Authoritative remote player walk destination cannot be represented by the native world tile type.");
		}
		retainedRemotePlayers.insert(snapshot.entityId);
		auto [it, inserted] = remotePlayers_.try_emplace(snapshot.entityId);
		const bool hasPreviousTick = it->second.hasServerTick;
		const uint64_t previousTick = it->second.serverTick;
		const uint64_t presentationTicksSincePreviousSnapshot = it->second.presentationTicksSinceServerTick;
		if (hasPreviousTick && snapshot.serverTick < previousTick)
			continue;
		const bool classChanged = !inserted && it->second.player._pClass != static_cast<HeroClass>(snapshot.characterClassId);
		const bool equipmentChanged = !inserted && !classChanged
		    && it->second.player._pgfxnum != GetRemotePlayerEquipmentGraphicBitsInternal(snapshot);
		if (inserted || classChanged)
			InitializeRemotePlayer(it->second.player, snapshot);
		else if (equipmentChanged)
			ApplyRemotePlayerEquipment(it->second.player, snapshot);
		const player_graphic graphic = GetRemotePlayerGraphicInternal(snapshot);
		const Direction direction = GetRemotePlayerDirection(snapshot);
		const std::optional<uint32_t> actionKind = snapshot.currentAction.has_value()
		    ? std::optional<uint32_t>(snapshot.currentAction->kind)
		    : std::nullopt;
		const std::optional<uint64_t> actionStartedAtTick = snapshot.currentAction.has_value()
		    ? std::optional<uint64_t>(snapshot.currentAction->startedAtTick)
		    : std::nullopt;
		const bool animationChanged = inserted || classChanged || equipmentChanged
		    || it->second.graphic != graphic
		    || it->second.player.direction != direction
		    || it->second.actionKind != actionKind
		    || it->second.actionStartedAtTick != actionStartedAtTick;
		ApplyRemotePlayerSnapshot(it->second.player, snapshot);
		ApplyProjectedPlayerAnimationInternal(
		    it->second.player,
		    snapshot,
		    graphic,
		    animationChanged,
		    hasPreviousTick,
		    previousTick,
		    presentationTicksSincePreviousSnapshot);
		it->second.levelId = snapshot.levelId;
		it->second.tilePosition = it->second.player.position.tile;
		it->second.graphic = graphic;
		it->second.actionKind = actionKind;
		it->second.actionStartedAtTick = actionStartedAtTick;
		it->second.serverTick = snapshot.serverTick;
		it->second.hasServerTick = true;
		if (animationChanged || snapshot.currentAction.has_value() || snapshot.serverTick > previousTick)
			it->second.presentationTicksSinceServerTick = 0;
	}
	for (auto it = remotePlayers_.begin(); it != remotePlayers_.end();) {
		if (!retainedRemotePlayers.contains(it->first)) {
			if (InspectPlayer == &it->second.player)
				InspectPlayer = MyPlayer;
			it = remotePlayers_.erase(it);
		} else {
			++it;
		}
	}

	for (auto it = itemSlots_.begin(); it != itemSlots_.end();) {
		if (currentItemEntities.contains(it->first)) {
			++it;
			continue;
		}
		if (const int activeIndex = FindActiveItemListIndex(it->second); activeIndex >= 0) {
			RemoveMappedItem(it->second);
		}
		it = itemSlots_.erase(it);
	}
	for (auto it = monsterSlots_.begin(); it != monsterSlots_.end();) {
		if (currentMonsterEntities.contains(it->first)) {
			++it;
			continue;
		}
		if (const int activeIndex = FindActiveMonsterListIndex(it->second); activeIndex >= 0) {
			RemoveMappedMonster(it->second);
		}
		it = monsterSlots_.erase(it);
	}
	for (auto it = objectSlots_.begin(); it != objectSlots_.end();) {
		if (currentObjectEntities.contains(it->first)) {
			++it;
			continue;
		}
		if (const int activeIndex = FindActiveObjectListIndex(it->second); activeIndex >= 0) {
			RemoveMappedObject(it->second);
		}
		it = objectSlots_.erase(it);
	}
	for (auto it = projectileEntities_.begin(); it != projectileEntities_.end();) {
		if (currentProjectileEntities.contains(*it)) {
			++it;
			continue;
		}
		RemoveAuthoritativePresentationMissile(*it);
		it = projectileEntities_.erase(it);
	}
	if (projection.ServerTick().has_value())
		lastWorldServerTick_ = projection.ServerTick();

	return { };
}

void ServerBackedWorldPresentation::DrawRemotePlayers(
	const Surface &out,
	Point tilePosition,
	Point targetBufferPosition,
	int lightTableIndex) const
{
	for (const auto &[entityId, remote] : remotePlayers_) {
		(void)entityId;
		if (!IsRemotePlayerOnActiveLevel(remote.levelId, remote.player)
		    || remote.tilePosition != tilePosition
		    || !remote.player.animInfo.sprites)
			continue;
		DrawPlayer(out, remote.player, tilePosition, targetBufferPosition, lightTableIndex);
	}
}

const Player *ServerBackedWorldPresentation::FindRemotePlayer(uint32_t entityId) const noexcept
{
	const auto it = remotePlayers_.find(entityId);
	return it == remotePlayers_.end() ? nullptr : &it->second.player;
}

Player *ServerBackedWorldPresentation::FindRemotePlayer(uint32_t entityId) noexcept
{
	const auto it = remotePlayers_.find(entityId);
	return it == remotePlayers_.end() ? nullptr : &it->second.player;
}

void ServerBackedWorldPresentation::Clear() noexcept
{
	for (const auto &[entityId, itemSlot] : itemSlots_)
		RemoveMappedItem(itemSlot);
	for (const auto &[entityId, monsterSlot] : monsterSlots_)
		RemoveMappedMonster(monsterSlot);
	for (const auto &[entityId, objectSlot] : objectSlots_)
		RemoveMappedObject(objectSlot);
	for (const auto entityId : projectileEntities_)
		RemoveAuthoritativePresentationMissile(entityId);
	for (const auto &[entityId, remote] : remotePlayers_) {
		(void)entityId;
		if (InspectPlayer == &remote.player)
			InspectPlayer = MyPlayer;
	}
	monsterSlots_.clear();
	itemSlots_.clear();
	objectSlots_.clear();
	projectileEntities_.clear();
	lastWorldServerTick_.reset();
	remotePlayers_.clear();
}

std::size_t ServerBackedWorldPresentation::AdvanceAnimations() noexcept
{
	// Connected sessions skip ProcessMonsters entirely because it owns gameplay
	// mutation. Advance only presentation animation state for projected
	// monsters and remote players.
	std::size_t advanced = 0;
	for (const auto &[entityId, monsterSlot] : monsterSlots_) {
		(void)entityId;
		if (FindActiveMonsterListIndex(monsterSlot) < 0)
			continue;
		Monster &monster = Monsters[monsterSlot];
		if (monster.isWalking() && !monster.animInfo.isPetrified) {
			monster.animInfo.processAnimation();
			++advanced;
		}
	}
	for (auto &[entityId, remote] : remotePlayers_) {
		(void)entityId;
		const bool loop = remote.graphic == player_graphic::Stand
		    || remote.graphic == player_graphic::Walk;
		AdvanceProjectedPlayerAnimationInternal(remote.player, remote.graphic, 1, loop);
		++remote.presentationTicksSinceServerTick;
		++advanced;
	}
	return advanced;
}

} // namespace devilution::authoritative
