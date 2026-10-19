#include "tables/leveldat.h"

#include <algorithm>

#include "data/mpq_file.hpp"
#include "data/record_reader.hpp"

namespace devilution {

std::vector<LevelGenerationRow> LevelGenerationData;

void LoadLevelGenerationData()
{
	const std::string_view filename = "txtdata\\levels\\level_generation.tsv";
	DataFile dataFile = DataFile::loadOrDie(filename);
	dataFile.skipHeaderOrDie(filename);

	LevelGenerationData.clear();
	LevelGenerationData.reserve(dataFile.numRecords());
	for (DataFileRecord record : dataFile) {
		RecordReader reader { record, filename };
		LevelGenerationRow &row = LevelGenerationData.emplace_back();
		reader.readInt("minLevel", row.minLevel);
		reader.readInt("maxLevel", row.maxLevel);
		reader.readInt("monsterDensityDivisor", row.monsterDensityDivisor);
		reader.readInt("mpMonsterMultiplierPercent", row.mpMonsterMultiplierPercent);
		reader.readInt("trapPercent", row.trapPercent);
	}
	LevelGenerationData.shrink_to_fit();
}

static const LevelGenerationRow *FindRow(int level)
{
	auto it = std::find_if(LevelGenerationData.begin(), LevelGenerationData.end(),
	    [level](const LevelGenerationRow &row) {
		    return level >= row.minLevel && level <= row.maxLevel;
	    });
	if (it != LevelGenerationData.end())
		return &*it;
	return nullptr;
}

int GetMonsterDensityDivisor(int level)
{
	const auto *row = FindRow(level);
	if (row != nullptr)
		return row->monsterDensityDivisor;
	return 30; // default fallback
}

int GetMpMonsterMultiplierPercent(int level)
{
	const auto *row = FindRow(level);
	if (row != nullptr)
		return row->mpMonsterMultiplierPercent;
	return 150; // default fallback
}

int GetTrapPercent(int level)
{
	const auto *row = FindRow(level);
	if (row != nullptr)
		return row->trapPercent;
	return 0;
}

} // namespace devilution
