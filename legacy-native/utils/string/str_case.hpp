#pragma once

/**
 * @file utils/string/str_case.hpp
 *
 * Interface for case-insensitive string comparison.
 */


#include <string>
#include <string_view>

namespace devilution {

void AsciiStrToLower(std::string &str);

[[nodiscard]] inline std::string AsciiStrToLower(std::string_view str)
{
	std::string copy { str.data(), str.size() };
	AsciiStrToLower(copy);
	return copy;
}

} // namespace devilution
