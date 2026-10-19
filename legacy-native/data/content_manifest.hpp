#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace devilution {

/** One line of an external TSV content table. */
struct ContentManifestRow {
	uint64_t lineNumber;
	std::vector<std::string> cells;
};

/** One ordered external TSV content table. */
struct ContentManifestTable {
	std::string sourcePath;
	std::vector<std::string> columns;
	std::vector<ContentManifestRow> rows;
};

/** One ordered external content pack. */
struct ContentManifestPack {
	std::string id;
	std::string version;
	std::vector<ContentManifestTable> tables;
};

/** Ordered external content data whose hash identifies a gameplay ruleset. */
struct ContentManifest {
	std::string id;
	std::string version;
	std::vector<ContentManifestPack> packs;
};

/** Returns the C# ContentManifest canonical SHA-256 hash. */
[[nodiscard]] std::string ComputeContentManifestSha256(const ContentManifest &manifest);

} // namespace devilution
