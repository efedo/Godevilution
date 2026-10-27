#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#include <gtest/gtest.h>

#include "data/content_manifest.hpp"

namespace devilution {
namespace {

ContentManifest MakeBaselineManifest()
{
	return {
		.id = "baseline",
		.version = "1",
		.packs = {
		    { .id = "core", .version = "1", .tables = {
		                                        { .sourcePath = "items.tsv", .columns = { "id", "name" }, .rows = {
		                                                                                                      { .lineNumber = 2, .cells = { "short-sword", "Short Sword" } },
		                                                                                                      { .lineNumber = 3, .cells = { "buckler", "Buckler" } },
		                                                                                                  } },
		                                    } },
		    { .id = "expansion", .version = "2", .tables = {
		                                             { .sourcePath = "monsters.tsv", .columns = { "id", "name" }, .rows = {
		                                                                                                              { .lineNumber = 2, .cells = { "goat", "Goatman" } },
		                                                                                                          } },
		                                         } },
		},
	};
}

TEST(ContentManifest, MatchesCSharpCanonicalGoldenVector)
{
	EXPECT_EQ(ComputeContentManifestSha256(MakeBaselineManifest()), "40d13766d2726d36216120e9cd36e064c72fee0395a46d23abcf6d316c9ff34c");
}

TEST(ContentManifest, OrderedPacksHaveDistinctGoldenVectors)
{
	ContentManifest manifest = MakeBaselineManifest();
	std::swap(manifest.packs[0], manifest.packs[1]);

	EXPECT_EQ(ComputeContentManifestSha256(manifest), "5c536ffd249de81cefd8d74bcf35b4738f129d950a89e339f521c0f1994ee35f");
}

TEST(ContentManifest, TableCellChangesHashGoldenVector)
{
	ContentManifest manifest = MakeBaselineManifest();
	manifest.packs[0].tables[0].rows[1].cells[1] = "Tower Shield";

	EXPECT_EQ(ComputeContentManifestSha256(manifest), "5d12a34dddd0fdaa81382946736e6637afc5e319e5228a89571144da2a24cf59");
}

TEST(ContentManifest, RejectsRowsWithTheWrongCellCount)
{
	ContentManifest manifest = MakeBaselineManifest();
	manifest.packs[0].tables[0].rows[1].cells.pop_back();

	EXPECT_THROW(static_cast<void>(ComputeContentManifestSha256(manifest)), std::invalid_argument);
}

} // namespace
} // namespace devilution
