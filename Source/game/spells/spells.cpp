/**
 * @file game/spells/spells.cpp
 *
 * Implementation of functionality for casting player spells.
 */
#include "game/spells/spells.hpp"

#include "ui/panel/control.hpp"
#include "engine/cursor.h"
#ifdef _DEBUG
#include "application/debug.h"
#endif
#include "engine/gfx/backbuffer_state.hpp"
#include "engine/math/point.hpp"
#include "engine/random.hpp"
#include "engine/math/world_tile.hpp"
#include "application/game_mode.hpp"
#include "ui/gamemenu.h"
#include "game/players/inventory.hpp"
#include "game/missiles/missiles.hpp"
#include "network/protocol/legacy_player_state_policy.hpp"
#include "network/protocol/legacy_combat_policy.hpp"
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif

namespace devilution {

namespace {

/**
 * @brief Gets a value indicating whether the player's current readied spell is a valid spell. Readied spells can be
 * invalidaded in a few scenarios where the spell comes from items, for example (like dropping the only scroll that
 * provided the spell).
 * @param player The player whose readied spell is to be checked.
 * @return 'true' when the readied spell is currently valid, and 'false' otherwise.
 */
bool IsReadiedSpellValid(const Player &player)
{
	switch (player._pRSplType) {
	case SpellType::Skill:
	case SpellType::Spell:
	case SpellType::Invalid:
		return true;

	case SpellType::Charges:
		return (player._pISpells & GetSpellBitmask(player._pRSpell)) != 0;

	case SpellType::Scroll:
		return (player._pScrlSpells & GetSpellBitmask(player._pRSpell)) != 0;

	default:
		return false;
	}
}

/**
 * @brief Clears the current player's readied spell selection.
 * @note Will force a UI redraw in case the values actually change, so that the new spell reflects on the bottom panel.
 * @param player The player whose readied spell is to be cleared.
 */
void ClearReadiedSpell(Player &player)
{
	if (player._pRSpell != SpellID::Invalid) {
		player._pRSpell = SpellID::Invalid;
		RedrawEverything();
	}

	if (player._pRSplType != SpellType::Invalid) {
		player._pRSplType = SpellType::Invalid;
		RedrawEverything();
	}
}

} // namespace

bool IsValidSpell(SpellID spl)
{
	return spl > SpellID::Null && static_cast<size_t>(spl) < SpellsData.size();
}

bool IsValidSpellFrom(int spellFrom)
{
	if (spellFrom == 0)
		return true;
	if (spellFrom >= INVITEM_INV_FIRST && spellFrom <= INVITEM_INV_LAST)
		return true;
	if (spellFrom >= INVITEM_BELT_FIRST && spellFrom <= INVITEM_BELT_LAST)
		return true;
	return false;
}

bool IsWallSpell(SpellID spl)
{
	return spl == SpellID::FireWall || spl == SpellID::LightningWall;
}

bool TargetsMonster(SpellID id)
{
	return id == SpellID::Fireball
	    || id == SpellID::FireWall
	    || id == SpellID::Inferno
	    || id == SpellID::Lightning
	    || id == SpellID::StoneCurse
	    || id == SpellID::FlameWave;
}

int GetManaAmount(const Player &player, SpellID sn)
{
	int ma; // mana amount

	// mana adjust
	int adj = 0;

	// spell level
	const int sl = std::max(player.GetSpellLevel(sn) - 1, 0);

	if (sl > 0) {
		adj = sl * GetSpellData(sn).sManaAdj;
	}
	if (sn == SpellID::Firebolt) {
		adj /= 2;
	}
	if (sn == SpellID::Resurrect && sl > 0) {
		adj = sl * (GetSpellData(SpellID::Resurrect).sManaCost / 8);
	}

	if (sn == SpellID::Healing || sn == SpellID::HealOther) {
		ma = (GetSpellData(SpellID::Healing).sManaCost + 2 * player.getCharacterLevel() - adj);
	} else if (GetSpellData(sn).sManaCost == 255) {
		ma = (player.mana.maximumBase >> 6) - adj;
	} else {
		ma = (GetSpellData(sn).sManaCost - adj);
	}

	ma = std::max(ma, 0);
	ma <<= 6;

	if (gbIsHellfire && player._pClass == HeroClass::Sorcerer) {
		ma /= 2;
	} else if (player._pClass == HeroClass::Rogue || player._pClass == HeroClass::Monk || player._pClass == HeroClass::Bard) {
		ma -= ma / 4;
	}

	if (GetSpellData(sn).sMinMana > ma >> 6) {
		ma = GetSpellData(sn).sMinMana << 6;
	}

	return ma;
}

void ConsumeSpell(Player &player, SpellID sn)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyPlayerStateMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	switch (player.executedSpell.spellType) {
	case SpellType::Skill:
	case SpellType::Invalid:
		break;
	case SpellType::Scroll:
		ConsumeScroll(player);
		break;
	case SpellType::Charges:
		ConsumeStaffCharge(player);
		break;
	case SpellType::Spell:
#ifdef _DEBUG
		if (DebugGodMode)
			break;
#endif
		int ma = GetManaAmount(player, sn);
		player.mana.current -= ma;
		player.mana.base -= ma;
		RedrawComponent(PanelDrawComponent::Mana);
		break;
	}
	if (sn == SpellID::BloodStar) {
		player.applyDamage(DamageType::Physical, 5);
	}
	if (sn == SpellID::BoneSpirit) {
		player.applyDamage(DamageType::Physical, 6);
	}
}

void EnsureValidReadiedSpell(Player &player)
{
	if (!IsReadiedSpellValid(player)) {
		ClearReadiedSpell(player);
	}
}

SpellCheckResult CheckSpell(const Player &player, SpellID sn, SpellType st, bool manaonly)
{
#ifdef _DEBUG
	if (DebugGodMode)
		return SpellCheckResult::Success;
#endif

	if (!manaonly && pcurs != CURSOR_HAND) {
		return SpellCheckResult::Fail_Busy;
	}

	if (st == SpellType::Skill) {
		return SpellCheckResult::Success;
	}

	if (player.GetSpellLevel(sn) <= 0) {
		return SpellCheckResult::Fail_Level0;
	}

	if (player.mana.current < GetManaAmount(player, sn) || HasAnyOf(player._pIFlags, ItemSpecialEffect::NoMana)) {
		return SpellCheckResult::Fail_NoMana;
	}

	return SpellCheckResult::Success;
}

void CastSpell(Player &player, SpellID spl, WorldTilePosition src, WorldTilePosition dst, int spllvl)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacySpellSimulation(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	Direction dir = player.direction;
	if (IsWallSpell(spl)) {
		dir = player.tempDirection;
	}

	bool fizzled = false;
	const SpellData &spellData = GetSpellData(spl);
	for (size_t i = 0; i < sizeof(spellData.sMissiles) / sizeof(spellData.sMissiles[0]) && spellData.sMissiles[i] != MissileID::Null; i++) {
		Missile *missile = AddMissile(src, dst, dir, spellData.sMissiles[i], TARGET_MONSTERS, player, 0, spllvl);
		fizzled |= (missile == nullptr);
	}
	if (spl == SpellID::ChargedBolt) {
		for (int i = (spllvl / 2) + 3; i > 0; i--) {
			Missile *missile = AddMissile(src, dst, dir, MissileID::ChargedBolt, TARGET_MONSTERS, player, 0, spllvl);
			fizzled |= (missile == nullptr);
		}
	}
	if (!fizzled) {
		ConsumeSpell(player, spl);
	}
}

void SpawnResurrectBeam(Player &caster, Player &target)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacySpellSimulation(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	AddMissile(
	    target.position.tile,
	    target.position.tile,
	    Direction::South,
	    MissileID::ResurrectBeam,
	    TARGET_MONSTERS,
	    caster.getId(),
	    0,
	    0);
}

void ApplyResurrect(Player &target)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyCombatMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	if (&target == MyPlayer) {
		MyPlayerIsDead = false;
		gamemenu_off();
		RedrawComponent(PanelDrawComponent::Health);
		RedrawComponent(PanelDrawComponent::Mana);
	}

	target.clearPath();
	target.destAction = ACTION_NONE;
	target._pInvincible = false;
	target.syncInitialPosition();

	int hp = 10 << 6;
	if (target.life.maximumBase < (10 << 6)) {
		hp = target.life.maximumBase;
	}
	target.setHitPoints(hp);

	target.life.base = target.life.current + (target.life.maximumBase - target.life.maximum); // CODEFIX: does the same stuff as SetPlayerHitPoints above, can be removed
	target.mana.current = 0;
	target.mana.base = target.mana.current + (target.mana.maximumBase - target.mana.maximum);

	target._pmode = PM_STAND;

	CalcPlrInv(target, true);

	if (target.isOnActiveLevel()) {
		target.startStand(target.direction);
	}
}

void DoHealOther(const Player &caster, Player &target)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyCombatMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	if (target.hasNoLife()) {
		return;
	}

	int hp = (GenerateRnd(10) + 1) << 6;
	for (unsigned i = 0; i < caster.getCharacterLevel(); i++) {
		hp += (GenerateRnd(4) + 1) << 6;
	}
	for (int i = 0; i < caster.GetSpellLevel(SpellID::HealOther); i++) {
		hp += (GenerateRnd(6) + 1) << 6;
	}

	if (caster._pClass == HeroClass::Warrior || caster._pClass == HeroClass::Barbarian) {
		hp *= 2;
	} else if (caster._pClass == HeroClass::Rogue || caster._pClass == HeroClass::Bard) {
		hp += hp / 2;
	} else if (caster._pClass == HeroClass::Monk) {
		hp *= 3;
	}

	target.life.current = std::min(target.life.current + hp, target.life.maximum);
	target.life.base = std::min(target.life.base + hp, target.life.maximumBase);

	if (&target == MyPlayer) {
		RedrawComponent(PanelDrawComponent::Health);
	}
}

int GetSpellBookLevel(SpellID s)
{
	if (gbIsSpawn) {
		switch (s) {
		case SpellID::StoneCurse:
		case SpellID::Guardian:
		case SpellID::Golem:
		case SpellID::Elemental:
		case SpellID::BloodStar:
		case SpellID::BoneSpirit:
			return -1;
		default:
			break;
		}
	}

	if (static_cast<uint8_t>(s) >= SpellsData.size()) {
		return -1;
	}

	return GetSpellData(s).sBookLvl;
}

int GetSpellStaffLevel(SpellID s)
{
	if (gbIsSpawn) {
		switch (s) {
		case SpellID::StoneCurse:
		case SpellID::Guardian:
		case SpellID::Golem:
		case SpellID::Apocalypse:
		case SpellID::Elemental:
		case SpellID::BloodStar:
		case SpellID::BoneSpirit:
			return -1;
		default:
			break;
		}
	}

	if (static_cast<uint8_t>(s) >= SpellsData.size()) {
		return -1;
	}

	return GetSpellData(s).sStaffLvl;
}

} // namespace devilution
