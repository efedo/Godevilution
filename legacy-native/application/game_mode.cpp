/**
 * @file application/game_mode.cpp
 *
 * Implementation of game mode management.
 */


#include "application/game_mode.hpp"

#include <function_ref.hpp>

#include "persistence/options.h"

namespace devilution {
namespace {
void OptionSharewareChanged()
{
	gbIsSpawn = *GetOptions().GameMode.shareware;
}
const auto OptionChangeHandlerShareware = (GetOptions().GameMode.shareware.SetValueChangedCallback(OptionSharewareChanged), true);
} // namespace

bool gbRunGame;
bool gbIsSpawn;
bool gbIsHellfire;
bool gbVanilla;
bool forceHellfire;

} // namespace devilution
