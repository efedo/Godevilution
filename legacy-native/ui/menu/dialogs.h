#pragma once

/**
 * @file ui/menu/dialogs.h
 *
 * Interface for menu dialog screens.
 */


#include <cstddef>
#include <string_view>

#include "ui/menu/ui_item.h"

namespace devilution {

void UiErrorOkDialog(std::string_view text, const std::vector<std::unique_ptr<UiItemBase>> &renderBehind);
void UiErrorOkDialog(std::string_view caption, std::string_view text, const std::vector<std::unique_ptr<UiItemBase>> &renderBehind);

} // namespace devilution
