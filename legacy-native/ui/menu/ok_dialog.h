#pragma once

/**
 * @file ui/menu/ok_dialog.h
 *
 * Interface for OK selection dialog.
 */


namespace devilution {

void UiSelOkDialog(const char *title, const char *body, bool background);
void selok_Free();
void selok_Select(int value);
void selok_Esc();

} // namespace devilution
