/**
 * @file utils/string/str_case.cpp
 *
 * Implementation of case-insensitive string comparison.
 */


#include "utils/string/str_case.hpp"

namespace devilution {

void AsciiStrToLower(std::string &str)
{
	for (char &c : str) { // NOLINT(readability-identifier-length)
		if (c >= 'A' && c <= 'Z')
			c += ('a' - 'A');
	}
}

} // namespace devilution
