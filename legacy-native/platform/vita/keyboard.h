#pragma once

/**
 * @file platform/vita/keyboard.h
 *
 * Interface for keyboard.
 */


#include <string_view>

void vita_start_text_input(std::string_view guide_text, std::string_view initial_text, unsigned max_length);
