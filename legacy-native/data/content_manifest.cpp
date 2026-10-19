#include "data/content_manifest.hpp"

#include <array>
#include <cstdint>
#include <stdexcept>
#include <string_view>
#include <vector>

#include <picosha2.h>

namespace devilution {
namespace {

class CanonicalContentHasher {
public:
	void AppendString(std::string_view value)
	{
		AppendUint64(value.size());
		bytes_.insert(bytes_.end(), value.begin(), value.end());
	}

	void AppendUint64(uint64_t value)
	{
		for (size_t byte = 0; byte < sizeof(value); ++byte)
			bytes_.push_back(static_cast<uint8_t>(value >> (byte * 8)));
	}

	[[nodiscard]] std::string HexDigest() const
	{
		std::array<uint8_t, picosha2::k_digest_size> digest;
		picosha2::hash256(bytes_.begin(), bytes_.end(), digest.begin(), digest.end());
		return picosha2::bytes_to_hex_string(digest.begin(), digest.end());
	}

private:
	std::vector<uint8_t> bytes_;
};

void AppendTable(CanonicalContentHasher &hasher, const ContentManifestTable &table)
{
	hasher.AppendString(table.sourcePath);
	hasher.AppendUint64(table.columns.size());
	for (const std::string &column : table.columns)
		hasher.AppendString(column);
	hasher.AppendUint64(table.rows.size());
	for (const ContentManifestRow &row : table.rows) {
		if (row.cells.size() != table.columns.size())
			throw std::invalid_argument("Content manifest row cell count does not match the table column count.");
		hasher.AppendUint64(row.lineNumber);
		for (size_t column = 0; column < table.columns.size(); ++column)
			hasher.AppendString(row.cells[column]);
	}
}

} // namespace

std::string ComputeContentManifestSha256(const ContentManifest &manifest)
{
	CanonicalContentHasher hasher;
	hasher.AppendString(manifest.id);
	hasher.AppendString(manifest.version);
	hasher.AppendUint64(manifest.packs.size());
	for (const ContentManifestPack &pack : manifest.packs) {
		hasher.AppendString(pack.id);
		hasher.AppendString(pack.version);
		hasher.AppendUint64(pack.tables.size());
		for (const ContentManifestTable &table : pack.tables)
			AppendTable(hasher, table);
	}
	return hasher.HexDigest();
}

} // namespace devilution
