/**
 * @file game/monsters/monster_corpses.cpp
 *
 * Implementation of functions for placing dead monsters.
 */
#include "game/monsters/monster_corpses.hpp"

#include <cstdint>

#include "application/diablo.h"
#include "application/headless_mode.hpp"
#include "game/levels/dungeon_common.h"
#include "engine/lighting.h"
#include "game/monsters/monsters.hpp"
#include "game/monsters/monster_pool.hpp"

namespace devilution {

Corpse Corpses[MaxCorpses];
int8_t stonendx;

namespace {
void InitDeadAnimationFromMonster(Corpse &corpse, const CMonster &mon)
{
	const AnimStruct &animData = mon.getAnimData(MonsterGraphic::Death);
	if (animData.sprites) {
		corpse.sprites.emplace(*animData.sprites);
	} else {
		corpse.sprites = std::nullopt;
	}
	corpse.frame = animData.frames - 1;
	corpse.width = animData.width;
}

void MoveLightToCorpse(Monster &monster)
{
	for (int dx = 0; dx < MAXDUNX; dx++) {
		for (int dy = 0; dy < MAXDUNY; dy++) {
			if ((tileAt(Point { dx, dy }).corpseIndex()) == monster.corpseId) {
				CurrentLightManager.ChangeLightXY(monster.lightId, { dx, dy });
				return;
			}
		}
	}
	CurrentLightManager.AddUnLight(monster.lightId);
}
} // namespace

void InitCorpses()
{
	int8_t mtypes[MaxMonsters] = {};

	int8_t nd = 0;

	for (size_t i = 0; i < LevelBestiary.size(); i++) {
		CMonster &monsterType = LevelBestiary[i];
		if (mtypes[monsterType.type] != 0)
			continue;

		InitDeadAnimationFromMonster(Corpses[nd], monsterType);
		Corpses[nd].translationPaletteIndex = 0;
		nd++;

		monsterType.corpseId = nd;
		mtypes[monsterType.type] = nd;
	}

	nd++; // Unused blood spatter

	if (!HeadlessMode)
		Corpses[nd].sprites.emplace(*GetMissileSpriteData(MissileGraphicID::StoneCurseShatter).sprites);
	Corpses[nd].frame = 11;
	Corpses[nd].width = 128;
	Corpses[nd].translationPaletteIndex = 0;
	nd++;

	stonendx = nd;

	for (const unsigned m : MonsterPoolAdapter::ActiveMonsterRange()) {
		auto &monster = Monsters[m];
		if (monster.isUnique()) {
			InitDeadAnimationFromMonster(Corpses[nd], monster.type());
			Corpses[nd].translationPaletteIndex = m + 1;
			nd++;

			monster.corpseId = nd;
		}
	}

	assert(static_cast<unsigned>(nd) <= MaxCorpses);
}

void AddCorpse(Point tilePosition, int8_t dv, Direction ddir)
{
	tileAt(tilePosition).setCorpse((dv & 0x1F) + (static_cast<int>(ddir) << 5));
}

void MoveLightsToCorpses()
{
	for (const unsigned m : MonsterPoolAdapter::ActiveMonsterRange()) {
		auto &monster = Monsters[m];
		if (!monster.isUnique())
			continue;
		MoveLightToCorpse(monster);
	}
}

} // namespace devilution
