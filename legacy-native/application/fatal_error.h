/**
 * @file application/fatal_error.h
 *
 * Interface of error dialogs.
 */
#pragma once

#include <string_view>

#include "utils/attributes.h"

namespace devilution {

#define ErrSdl() ErrDlg("SDL Error", SDL_GetError(), __FILE__, __LINE__)

#undef assert

#ifndef _DEBUG
#define assert(exp)
#else
#define assert(exp) (void)((exp) || (assert_fail(__LINE__, __FILE__, #exp), 0))
#endif

// Terminates the game and displays an error message box.
// title: Message box title.
// body: Error message.
[[noreturn]] void DisplayFatalErrorAndExit(std::string_view title, std::string_view body);

// Terminates the game and displays an error message box.
// str: Error message.
[[noreturn]] void app_fatal(std::string_view str);

#ifdef _DEBUG
// Show an error and exit the application.
// nLineNo: The line number of the assertion
// pszFile: File name where the assertion is located
// pszFail: Fail message
[[noreturn]] void assert_fail(int nLineNo, std::string_view pszFile, std::string_view pszFail);
#endif

// Terminates the game and displays an error dialog box based on the given dialog_id.
[[noreturn]] void ErrDlg(std::string_view title, std::string_view error, std::string_view logFilePath, int logLineNr);

// Terminates the game with an insert CD error dialog.
[[noreturn]] void InsertCDDlg(std::string_view archiveName);

// Terminates the game with a read-only directory error dialog.
[[noreturn]] void DirErrorDlg(std::string_view error);

} // namespace devilution
