// Native MPQ reader oracle for the C# port in tools/Native.Archive.
//
// Writes a synthetic archive with the pinned mpqfs writer, then records what
// mpqfs reads back from it and from one real archive (spawn.mpq in CI, or an
// explicit GODEVILUTION_MPQ_ORACLE_ARCHIVE such as a private DIABDAT.MPQ).
// Only names, sizes and SHA-256 digests are written; archive bytes are never
// copied into the oracle directory.

#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

#include <gtest/gtest.h>
#include <mpqfs/mpqfs.h>
#include <picosha2.h>

namespace {

struct SyntheticFile {
	std::string name;
	std::vector<uint8_t> data;
};

std::vector<uint8_t> Pattern(size_t size, uint32_t seed, bool compressible)
{
	std::vector<uint8_t> data(size);
	uint32_t state = seed;
	for (size_t i = 0; i < size; ++i) {
		state = state * 1103515245U + 12345U;
		data[i] = compressible ? static_cast<uint8_t>("Godevilution MPQ "[(i / 3) % 17]) : static_cast<uint8_t>(state >> 16);
	}
	return data;
}

std::vector<SyntheticFile> SyntheticFiles()
{
	std::vector<SyntheticFile> files;
	files.push_back({ "empty.bin", {} });
	files.push_back({ "one.bin", { 0x5A } });
	files.push_back({ "levels\\text\\sector-minus-one.txt", Pattern(4095, 1, true) });
	files.push_back({ "levels\\text\\sector-exact.txt", Pattern(4096, 2, true) });
	files.push_back({ "levels\\text\\sector-plus-one.txt", Pattern(4097, 3, true) });
	files.push_back({ "levels\\text\\multi-sector.txt", Pattern(70000, 4, true) });
	files.push_back({ "data\\random-incompressible.bin", Pattern(10000, 5, false) });
	std::vector<uint8_t> mixed = Pattern(4096, 6, false);
	const auto tail = Pattern(9000, 7, true);
	mixed.insert(mixed.end(), tail.begin(), tail.end());
	files.push_back({ "data\\mixed-stored-and-imploded.bin", mixed });
	files.push_back({ "Data/Forward/Slash.BIN", Pattern(5000, 8, true) });
	files.push_back({ "zeros.bin", std::vector<uint8_t>(20000, 0) });
	return files;
}

// Well-known Diablo paths the profile/art pipeline reads; absent entries are
// recorded so lookup misses are compared too.
const char *const KnownNames[] = {
	"levels\\towndata\\town.cel", "levels\\towndata\\town.min", "levels\\towndata\\town.til",
	"levels\\towndata\\town.sol", "levels\\towndata\\town.pal", "levels\\towndata\\towns.cel",
	"levels\\towndata\\automap.amp", "levels\\towndata\\sector1s.dun", "levels\\towndata\\sector2s.dun",
	"levels\\towndata\\sector3s.dun", "levels\\towndata\\sector4s.dun", "levels\\towndata\\automap.dun",
	"levels\\l1data\\l1.cel", "levels\\l1data\\l1.min", "levels\\l1data\\l1.til", "levels\\l1data\\l1.sol",
	"levels\\l1data\\l1s.cel", "levels\\l1data\\l1.amp", "levels\\l1data\\l1_1.pal", "levels\\l1data\\l1_2.pal",
	"levels\\l1data\\l1_3.pal", "levels\\l1data\\l1_4.pal", "levels\\l1data\\l1_5.pal",
	"levels\\l1data\\rnd6.dun", "levels\\l1data\\skngdo.dun", "levels\\l1data\\banner1.dun",
	"levels\\l1data\\banner2.dun", "levels\\l1data\\sklkng1.dun", "levels\\l1data\\sklkng2.dun",
	"levels\\l1data\\vile1.dun", "levels\\l1data\\vile2.dun",
	"levels\\l2data\\l2.cel", "levels\\l2data\\l2.min", "levels\\l2data\\l2.til", "levels\\l2data\\l2.sol",
	"levels\\l2data\\l2.amp", "levels\\l2data\\l2_1.pal", "levels\\l2data\\blind1.dun", "levels\\l2data\\blood1.dun",
	"levels\\l2data\\bonestr2.dun",
	"levels\\l3data\\l3.cel", "levels\\l3data\\l3.min", "levels\\l3data\\l3.til", "levels\\l3data\\l3.sol",
	"levels\\l3data\\l3.amp", "levels\\l3data\\l3_1.pal", "levels\\l3data\\anvil.dun",
	"levels\\l4data\\l4.cel", "levels\\l4data\\l4.min", "levels\\l4data\\l4.til", "levels\\l4data\\l4.sol",
	"levels\\l4data\\l4.amp", "levels\\l4data\\l4_1.pal", "levels\\l4data\\diab1.dun", "levels\\l4data\\warlord.dun",
	"objects\\l1doors.cel", "objects\\chest1.cel", "objects\\chest2.cel", "objects\\chest3.cel",
	"objects\\sarc.cel", "objects\\traphole.cel", "objects\\barrel.cel", "objects\\barrelex.cel",
	"towners\\smith\\smithn.cel", "towners\\healer\\healer.cel", "towners\\animals\\cow.cel",
	"plrgfx\\warrior\\wln\\wlnas.cl2", "plrgfx\\warrior\\wln\\wlnst.cl2", "plrgfx\\warrior\\wln\\wlnwl.cl2",
	"plrgfx\\rogue\\rln\\rlnas.cl2", "plrgfx\\sorceror\\sln\\slnas.cl2", "plrgfx\\infra.trn", "plrgfx\\stone.trn",
	"gendata\\pause.trn", "gendata\\cutstart.pal", "gendata\\cuttt.pal", "gendata\\cutl1d.pal",
	"data\\inv\\objcurs.cel", "music\\dintro.wav", "no\\such\\file.bin",
};

std::string Sha256(const std::vector<uint8_t> &data)
{
	return picosha2::hash256_hex_string(data.begin(), data.end());
}

std::string Escape(const std::string &value)
{
	std::string escaped;
	for (const char ch : value) {
		if (ch == '\\' || ch == '"')
			escaped.push_back('\\');
		escaped.push_back(ch);
	}
	return escaped;
}

void WriteDigest(std::ostream &out, mpqfs_archive_t *archive, const std::string &name)
{
	out << "{\"name\":\"" << Escape(name) << "\",";
	if (!mpqfs_has_file(archive, name.c_str())) {
		out << "\"exists\":false,\"size\":0,\"sha256\":null}";
		return;
	}
	size_t size = 0;
	void *raw = mpqfs_read_file(archive, name.c_str(), &size);
	ASSERT_NE(raw, nullptr) << name << ": " << (mpqfs_last_error() ? mpqfs_last_error() : "unknown error");
	const auto *bytes = static_cast<const uint8_t *>(raw);
	const std::vector<uint8_t> data(bytes, bytes + size);
	std::free(raw);
	out << "\"exists\":true,\"size\":" << size << ",\"sha256\":\"" << Sha256(data) << "\"}";
}

std::string FileSha256(const std::filesystem::path &path)
{
	std::ifstream input(path, std::ios::binary);
	std::vector<uint8_t> data((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
	return Sha256(data);
}

} // namespace

TEST(NativeMpqOracle, CaptureReaderDigests)
{
	const char *directory = std::getenv("GODEVILUTION_MPQ_ORACLE_DIRECTORY");
	if (directory == nullptr || *directory == '\0')
		GTEST_SKIP() << "Opt-in native MPQ reader oracle.";
	const std::filesystem::path root(directory);
	ASSERT_TRUE(root.is_absolute());
	ASSERT_TRUE(std::filesystem::is_directory(root));
	ASSERT_TRUE(std::filesystem::is_empty(root));

	// The real archive is required: synthetic files cannot exercise encryption.
	const char *configured = std::getenv("GODEVILUTION_MPQ_ORACLE_ARCHIVE");
	const std::filesystem::path realArchive = configured != nullptr && *configured != '\0'
	    ? std::filesystem::path(configured)
	    : std::filesystem::absolute("spawn.mpq");
	ASSERT_TRUE(std::filesystem::is_regular_file(realArchive)) << "Real archive not found: " << realArchive.string();

	const auto synthetic = root / "synthetic.mpq";
	const auto files = SyntheticFiles();
	mpqfs_writer_t *writer = mpqfs_writer_create(synthetic.string().c_str(), 64);
	ASSERT_NE(writer, nullptr) << mpqfs_last_error();
	for (const auto &file : files)
		ASSERT_TRUE(mpqfs_writer_add_file(writer, file.name.c_str(), file.data.data(), file.data.size())) << file.name;
	ASSERT_TRUE(mpqfs_writer_close(writer)) << mpqfs_last_error();

	std::ostringstream out;
	out << "{\"format\":\"godevilution-native-mpq-oracle\",\"version\":1,\"hashes\":[";
	bool first = true;
	for (const char *name : KnownNames) {
		if (!first)
			out << ',';
		first = false;
		uint32_t index = 0;
		uint32_t nameA = 0;
		uint32_t nameB = 0;
		mpqfs_file_hash(name, &index, &nameA, &nameB);
		out << "{\"name\":\"" << Escape(name) << "\",\"index\":" << index << ",\"nameA\":" << nameA
		    << ",\"nameB\":" << nameB << ",\"fileKey\":" << mpqfs_hash_string(name, MPQFS_HASH_FILE_KEY) << '}';
	}
	out << "],\"synthetic\":{\"file\":\"synthetic.mpq\",\"files\":[";
	mpqfs_archive_t *archive = mpqfs_open(synthetic.string().c_str());
	ASSERT_NE(archive, nullptr) << mpqfs_last_error();
	for (size_t i = 0; i < files.size(); ++i) {
		if (i != 0)
			out << ',';
		EXPECT_EQ(Sha256(files[i].data), [&] {
			size_t size = 0;
			void *raw = mpqfs_read_file(archive, files[i].name.c_str(), &size);
			const std::vector<uint8_t> data(static_cast<uint8_t *>(raw), static_cast<uint8_t *>(raw) + size);
			std::free(raw);
			return Sha256(data);
		}()) << files[i].name;
		WriteDigest(out, archive, files[i].name);
	}
	out << ",";
	WriteDigest(out, archive, "missing.bin");
	mpqfs_close(archive);

	out << "]},\"real\":{\"path\":\"" << Escape(std::filesystem::absolute(realArchive).string()) << "\",\"sha256\":\""
	    << FileSha256(realArchive) << "\",\"files\":[";
	archive = mpqfs_open(realArchive.string().c_str());
	ASSERT_NE(archive, nullptr) << mpqfs_last_error();
	first = true;
	int present = 0;
	for (const char *name : KnownNames) {
		if (!first)
			out << ',';
		first = false;
		present += mpqfs_has_file(archive, name) ? 1 : 0;
		WriteDigest(out, archive, name);
	}
	mpqfs_close(archive);
	out << "]}}";
	ASSERT_GT(present, 10) << "The real archive should contain the common Town/L1 files.";

	std::ofstream output(root / "native-mpq-oracle.json", std::ios::binary);
	ASSERT_TRUE(output.is_open());
	output << out.str();
	ASSERT_TRUE(output.good());
}
