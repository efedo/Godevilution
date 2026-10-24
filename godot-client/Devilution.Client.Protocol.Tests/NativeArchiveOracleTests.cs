using System.Security.Cryptography;
using System.Text.Json;
using Godevilution.Tools.Archive;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/**
 * Compares the C# MPQ reader with native mpqfs output from
 * native_mpq_oracle_test (CI: synthetic archive plus spawn.mpq).
 */
public sealed class NativeArchiveOracleTests
{
    private static JsonElement LoadOracle()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_MPQ_ORACLE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory), "Set GODEVILUTION_MPQ_ORACLE_DIRECTORY.");
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory!, "native-mpq-oracle.json")));
        var root = document.RootElement.Clone();
        Assert.Equal("godevilution-native-mpq-oracle", root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        return root;
    }

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static void AssertDigests(MpqArchive archive, JsonElement files)
    {
        var compared = 0;
        foreach (var file in files.EnumerateArray()) {
            var name = file.GetProperty("name").GetString()!;
            var exists = file.GetProperty("exists").GetBoolean();
            Assert.True(exists == archive.Contains(name), $"{name}: native exists={exists}");
            if (!exists) {
                Assert.Equal(0, archive.GetFileSize(name));
                continue;
            }
            var data = archive.ReadFile(name);
            Assert.True(file.GetProperty("size").GetInt64() == data.Length, $"{name}: size");
            Assert.True(file.GetProperty("sha256").GetString() == Sha256(data), $"{name}: SHA-256");
            compared++;
        }
        Assert.True(compared > 0);
    }

    [Fact(Explicit = true)]
    public void HashesMatchNativeMpqfs()
    {
        foreach (var row in LoadOracle().GetProperty("hashes").EnumerateArray()) {
            var name = row.GetProperty("name").GetString()!;
            Assert.Equal(row.GetProperty("index").GetUInt32(), MpqCrypto.HashString(name, MpqCrypto.HashTableIndex));
            Assert.Equal(row.GetProperty("nameA").GetUInt32(), MpqCrypto.HashString(name, MpqCrypto.HashNameA));
            Assert.Equal(row.GetProperty("nameB").GetUInt32(), MpqCrypto.HashString(name, MpqCrypto.HashNameB));
            Assert.Equal(row.GetProperty("fileKey").GetUInt32(), MpqCrypto.HashString(name, MpqCrypto.HashFileKey));
        }
    }

    [Fact(Explicit = true)]
    public void SyntheticArchiveWrittenByMpqfsReadsIdentically()
    {
        var oracle = LoadOracle();
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_MPQ_ORACLE_DIRECTORY")!;
        var synthetic = oracle.GetProperty("synthetic");
        using var archive = MpqArchive.Open(Path.Combine(directory, synthetic.GetProperty("file").GetString()!));
        AssertDigests(archive, synthetic.GetProperty("files"));
    }

    [Fact(Explicit = true)]
    public void RealArchiveReadsIdenticallyToNativeMpqfs()
    {
        var real = LoadOracle().GetProperty("real");
        var path = real.GetProperty("path").GetString()!;
        // Bind the comparison to the exact archive the native capture read.
        using (var stream = File.OpenRead(path))
            Assert.Equal(real.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
        using var archive = MpqArchive.Open(path);
        AssertDigests(archive, real.GetProperty("files"));
    }
}
