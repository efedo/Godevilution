#pragma once

#include <cstdint>
#include <vector>

#include "utils/attributes.h"

namespace devilution {

struct LevelGenerationRow {
	int minLevel;
	int maxLevel;
	int monsterDensityDivisor;
	int mpMonsterMultiplierPercent;
	int trapPercent;
};

extern DVL_API_FOR_TEST std::vector<LevelGenerationRow> LevelGenerationData;

DVL_API_FOR_TEST void LoadLevelGenerationData();
int GetMonsterDensityDivisor(int level);
int GetMpMonsterMultiplierPercent(int level);
DVL_API_FOR_TEST int GetTrapPercent(int level);

} // namespace devilution
