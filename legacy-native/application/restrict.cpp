/**
 * @file application/restrict.cpp
 *
 * Implementation of functionality for checking if the game will be able run on the system.
 */

#include "application/restrict.h"
#include <string>

#include "application/fatal_error.h"
#include "utils/file/file_util.h"
#include "utils/paths.h"

#ifdef USE_SDL3
#include <SDL3/SDL_iostream.h>
#else
#include <SDL.h>
#endif

#include "utils/sdl/sdl_compat.h"

namespace devilution {

void ReadOnlyTest()
{
	const std::string path = paths::PrefPath() + "Diablo1ReadOnlyTest.foo";
	SDL_IOStream *file = SDL_IOFromFile(path.c_str(), "w");
	if (file == nullptr) {
		DirErrorDlg(paths::PrefPath());
	}
	SDL_CloseIO(file);
	RemoveFile(path.c_str());
}

} // namespace devilution
