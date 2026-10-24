using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Godevilution.Tools.Archive;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** C# port of the mpqfs read side; native parity lives in NativeArchiveOracleTests. */
public sealed class NativeArchiveTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("Godevilution-Mpq-Test-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Pattern(int length, int seed)
    {
        var data = new byte[length];
        var state = (uint)seed;
        for (var i = 0; i < data.Length; i++) {
            state = state * 1103515245u + 12345u;
            data[i] = (byte)(state >> 16);
        }
        return data;
    }

    [Fact]
    public void TableKeysMatchTheMpqfsConstants()
    {
        Assert.Equal(3283040112u, MpqCrypto.HashString("(hash table)", MpqCrypto.HashFileKey));
        Assert.Equal(3968054179u, MpqCrypto.HashString("(block table)", MpqCrypto.HashFileKey));
    }

    [Theory]
    [InlineData(MpqCrypto.HashTableIndex)]
    [InlineData(MpqCrypto.HashNameA)]
    [InlineData(MpqCrypto.HashNameB)]
    [InlineData(MpqCrypto.HashFileKey)]
    public void NameHashingIgnoresAsciiCaseAndSlashDirection(uint hashType)
    {
        var canonical = MpqCrypto.HashString("LEVELS\\L1DATA\\L1.TIL", hashType);
        Assert.Equal(canonical, MpqCrypto.HashString("levels/l1data/l1.til", hashType));
        Assert.Equal(canonical, MpqCrypto.HashString("Levels\\L1Data/l1.Til", hashType));
        Assert.NotEqual(canonical, MpqCrypto.HashString("levels\\l1data\\l1.sol", hashType));
        // Bytes above 0x7F are not case-folded, exactly as mpqfs's fixed table.
        Assert.NotEqual(MpqCrypto.HashString([0xE9], hashType), MpqCrypto.HashString([0xC9], hashType));
    }

    [Fact]
    public void FileKeyUsesOnlyTheFileNameAndOptionalAdjustment()
    {
        var plain = MpqCrypto.FileKey(Bytes("levels\\l1data\\l1.til"), 0x1234, 777, adjust: false);
        Assert.Equal(MpqCrypto.HashString("l1.til", MpqCrypto.HashFileKey), plain);
        Assert.Equal(plain, MpqCrypto.FileKey(Bytes("other/dir\\l1.til"), 99, 1, adjust: false));
        Assert.Equal(unchecked(plain + 0x1234u) ^ 777u, MpqCrypto.FileKey(Bytes("levels\\l1data\\l1.til"), 0x1234, 777, adjust: true));
    }

    [Fact]
    public void BlockEncryptionRoundTripsAndChangesData()
    {
        uint[] original = [0, 1, 0xDEADBEEF, 0xFFFFFFFF, 42];
        var data = (uint[])original.Clone();
        MpqCrypto.EncryptBlock(data, 0xC0FFEE);
        Assert.NotEqual(original, data);
        MpqCrypto.DecryptBlock(data, 0xC0FFEE);
        Assert.Equal(original, data);
    }

    [Fact]
    public void EncodeNameRefusesCharactersThatAreNotSingleBytes()
    {
        Assert.Equal(new byte[] { 0x61, 0xE9 }, MpqArchive.EncodeName("aé"));
        Assert.Throws<ArgumentException>(() => MpqArchive.EncodeName("aĀ"));
        Assert.Throws<ArgumentException>(() => MpqArchive.EncodeName("a\0b"));
    }

    [Theory]
    [InlineData(new byte[] { 0 }, PkwareStatus.Input)]
    [InlineData(new byte[] { 2, 6 }, PkwareStatus.Literal)]
    [InlineData(new byte[] { 0, 3 }, PkwareStatus.DictionarySize)]
    [InlineData(new byte[] { 1, 7 }, PkwareStatus.DictionarySize)]
    public void ExplodeRejectsInvalidHeaders(byte[] source, PkwareStatus expected)
    {
        Assert.Equal(expected, PkwareExplode.Explode(source, new byte[16], out var written));
        Assert.Equal(0, written);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(false, 5)]
    [InlineData(false, 6)]
    [InlineData(true, 4)]
    [InlineData(true, 6)]
    public void ExplodeDecodesLiteralsInBothModes(bool ascii, int dictionaryBits)
    {
        var expected = new byte[256];
        for (var i = 0; i < expected.Length; i++)
            expected[i] = (byte)i;
        var source = new PkwareTestEncoder(ascii, dictionaryBits).Literals(expected).End().ToArray();
        var output = new byte[300];
        Assert.Equal(PkwareStatus.Ok, PkwareExplode.Explode(source, output, out var written));
        Assert.Equal(expected, output[..written]);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ExplodeCopiesBackReferencesIncludingOverlapAndShortMatches(int dictionaryBits)
    {
        var far = (1 << dictionaryBits) * 3 + 5;
        var prefix = Pattern(far + 10, dictionaryBits);
        var encoder = new PkwareTestEncoder(false, dictionaryBits)
            .Literals(prefix)
            .Match(2, 3)            // two-byte matches use two low distance bits
            .Match(9, far)          // long distance with the full dictionary bits
            .Literal(0x7E)
            .Match(40, 0)           // overlapping run of the previous byte
            .Match(518, 10)         // longest encodable match
            .End();
        var expected = new List<byte>(prefix);
        void Copy(int length, int distance)
        {
            var from = expected.Count - distance - 1;
            for (var i = 0; i < length; i++)
                expected.Add(expected[from + i]);
        }
        Copy(2, 3);
        Copy(9, far);
        expected.Add(0x7E);
        Copy(40, 0);
        Copy(518, 10);
        var output = new byte[expected.Count + 64];
        Assert.Equal(PkwareStatus.Ok, PkwareExplode.Explode(encoder.ToArray(), output, out var written));
        Assert.Equal(expected.ToArray(), output[..written]);
    }

    [Fact]
    public void ExplodeStopsAtAFullOutputWithoutASentinel()
    {
        var source = new PkwareTestEncoder(false, 6).Literals(Bytes("abcdef")).ToArray();
        var output = new byte[6];
        Assert.Equal(PkwareStatus.Ok, PkwareExplode.Explode(source, output, out var written));
        Assert.Equal(6, written);
        Assert.Equal(Bytes("abcdef"), output);
    }

    [Fact]
    public void ExplodeReportsOutputOverflowAndInvalidInput()
    {
        var overflow = new PkwareTestEncoder(false, 6).Literals(Bytes("ab")).Match(10, 0).End().ToArray();
        Assert.Equal(PkwareStatus.Output, PkwareExplode.Explode(overflow, new byte[8], out _));

        var beforeStart = new PkwareTestEncoder(false, 6).Literal(1).Match(3, 5).End().ToArray();
        Assert.Equal(PkwareStatus.Input, PkwareExplode.Explode(beforeStart, new byte[32], out _));

        var truncated = new PkwareTestEncoder(false, 6).Literal(1).Match(20, 0).ToArray();
        Assert.Equal(PkwareStatus.Input, PkwareExplode.Explode(truncated[..^1], new byte[64], out _));
    }

    [Fact]
    public void ReadsStoredEncryptedAndImplodedFilesAcrossSectorBoundaries()
    {
        var builder = new TestArchiveBuilder { SectorShift = 0 };
        var sectorSize = builder.SectorSize;
        var stored = Pattern(sectorSize * 2 + 7, 1);
        var encrypted = Pattern(sectorSize + 3, 2);
        var adjusted = Pattern(sectorSize * 3, 3);
        var imploded = Pattern(sectorSize * 2 + 100, 4);
        var mixed = Pattern(sectorSize * 3 + 1, 5);
        var path = builder
            .AddStored("data\\stored.bin", stored)
            .AddStored("data\\encrypted.bin", encrypted, encrypted: true)
            .AddStored("data\\adjusted.bin", adjusted, encrypted: true, fixKey: true)
            .AddImploded("levels\\imploded.bin", imploded, encrypted: true, fixKey: true)
            // Sector 1 is stored inside an imploded file; sector 2 is empty (zeros).
            .AddImploded("levels\\mixed.bin", mixed, sectorPayload: (sector, plain) => sector switch {
                1 => plain,
                2 => [],
                _ => null,
            })
            .AddStored("empty.bin", [])
            .WriteTo(directory);
        using var archive = MpqArchive.Open(path);
        Assert.Equal(512u, archive.SectorSize);
        Assert.Equal(stored, archive.ReadFile("data\\stored.bin"));
        Assert.Equal(encrypted, archive.ReadFile("DATA/ENCRYPTED.BIN"));
        Assert.Equal(adjusted, archive.ReadFile("data\\adjusted.bin"));
        Assert.Equal(imploded, archive.ReadFile("levels\\imploded.bin"));
        var expectedMixed = (byte[])mixed.Clone();
        Array.Clear(expectedMixed, sectorSize * 2, sectorSize);
        Assert.Equal(expectedMixed, archive.ReadFile("levels\\mixed.bin"));
        Assert.Empty(archive.ReadFile("empty.bin"));
        Assert.True(archive.Contains("empty.bin"));
        Assert.Equal(stored.Length, archive.GetFileSize("data/stored.bin"));
    }

    [Fact]
    public void ReadsMultiMethodZlibAndPkwareSectors()
    {
        var builder = new TestArchiveBuilder { SectorShift = 0 };
        var sectorSize = builder.SectorSize;
        var data = new byte[sectorSize * 2 + 50];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i / 7);
        static byte[] Zlib(ReadOnlySpan<byte> plain)
        {
            using var output = new MemoryStream();
            output.WriteByte(0x02);
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(plain);
            return output.ToArray();
        }
        byte[] Payload(int sector)
        {
            var plain = data.AsSpan(sector * sectorSize, Math.Min(sectorSize, data.Length - sector * sectorSize));
            return sector == 1
                ? [0x08, .. new PkwareTestEncoder(false, 4).Literals(plain).End().ToArray()]
                : Zlib(plain);
        }
        var sectors = Enumerable.Range(0, 3).Select(Payload).ToArray();
        var table = new List<byte>();
        var offset = (uint)(sectors.Length + 1) * 4;
        foreach (var sector in sectors.Prepend([]).Select((value, index) => (value, index))) {
            if (sector.index > 0)
                offset += (uint)sectors[sector.index - 1].Length;
            table.AddRange(BitConverter.GetBytes(sector.index == 0 ? (uint)(sectors.Length + 1) * 4 : offset));
        }
        var payload = table.Concat(sectors.SelectMany(sector => sector)).ToArray();
        var tooLong = new List<byte>(BitConverter.GetBytes(8u));
        var oversized = Zlib(new byte[sectorSize + 1]);
        tooLong.AddRange(BitConverter.GetBytes(8u + (uint)oversized.Length));
        tooLong.AddRange(oversized);
        var path = builder
            .AddRaw("multi.bin", payload, (uint)data.Length, MpqArchive.FileCompress)
            .AddRaw("too-long.bin", tooLong.ToArray(), (uint)sectorSize, MpqArchive.FileCompress)
            .WriteTo(directory);
        using var archive = MpqArchive.Open(path);
        Assert.Equal(data, archive.ReadFile("multi.bin"));
        Assert.Contains("zlib uncompress failed", Assert.Throws<MpqException>(() => archive.ReadFile("too-long.bin")).Message);
    }

    [Fact]
    public void ReadsSingleUnitFilesLargerThanASector()
    {
        var data = Pattern(9000, 6);
        var path = new TestArchiveBuilder()
            .AddStored("single.bin", data, encrypted: true, singleUnit: true)
            .WriteTo(directory);
        using var archive = MpqArchive.Open(path);
        Assert.Equal(data, archive.ReadFile("single.bin"));
    }

    [Fact]
    public void FindsTheHeaderOnA512ByteBoundaryAndProbesPastDeletedEntries()
    {
        var data = Pattern(100, 7);
        var path = new TestArchiveBuilder { Prefix = 1024, HashTableSize = 4 }
            .AddRaw("gone.bin", [], 0, 0, deleted: true)
            .AddStored("a.bin", data)
            .AddStored("b.bin", [1, 2, 3])
            .WriteTo(directory);
        using var archive = MpqArchive.Open(path);
        Assert.Equal(1024, archive.ArchiveOffset);
        Assert.Equal(data, archive.ReadFile("a.bin"));
        Assert.Equal(new byte[] { 1, 2, 3 }, archive.ReadFile("b.bin"));
        Assert.False(archive.Contains("gone.bin"));
        Assert.False(archive.Contains("missing.bin"));
        Assert.Equal(0, archive.GetFileSize("missing.bin"));
        Assert.Throws<MpqException>(() => archive.ReadFile("missing.bin"));
    }

    [Fact]
    public void RefusesUnsupportedOrCorruptArchives()
    {
        var noSignature = Path.Combine(directory, "plain.bin");
        File.WriteAllBytes(noSignature, new byte[4096]);
        Assert.Contains("signature not found", Assert.Throws<MpqException>(() => MpqArchive.Open(noSignature)).Message);

        var version1 = new TestArchiveBuilder { FormatVersion = 1 }.AddStored("a.bin", [1]).WriteTo(directory, "v1.mpq");
        Assert.Contains("unsupported format version 1", Assert.Throws<MpqException>(() => MpqArchive.Open(version1)).Message);

        var truncated = Path.Combine(directory, "truncated.mpq");
        File.WriteAllBytes(truncated, new TestArchiveBuilder().AddStored("a.bin", [1]).Build()[..40]);
        Assert.Contains("hash table", Assert.Throws<MpqException>(() => MpqArchive.Open(truncated)).Message);
    }

    [Fact]
    public void RefusesCorruptSectorTablesAndUnsupportedCompression()
    {
        // A wrong first offset is how mpqfs detects a mis-decrypted table.
        var badTable = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(badTable, 99);
        var unsupported = new byte[12 + 5];
        BinaryPrimitives.WriteUInt32LittleEndian(unsupported, 8);
        BinaryPrimitives.WriteUInt32LittleEndian(unsupported.AsSpan(4), 13);
        unsupported[8] = 0x10; // bzip2
        var reversed = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(reversed, 8);
        BinaryPrimitives.WriteUInt32LittleEndian(reversed.AsSpan(4), 4);
        var path = new TestArchiveBuilder()
            .AddRaw("bad-table.bin", badTable, 100, MpqArchive.FileImplode)
            .AddRaw("bzip2.bin", unsupported, 100, MpqArchive.FileCompress)
            .AddRaw("reversed.bin", reversed, 100, MpqArchive.FileImplode)
            .AddRaw("single-imploded.bin", [0, 6, 0], 100, MpqArchive.FileImplode | MpqArchive.FileSingleUnit)
            .WriteTo(directory);
        using var archive = MpqArchive.Open(path);
        Assert.Contains("sector offset table validation failed", Assert.Throws<MpqException>(() => archive.ReadFile("bad-table.bin")).Message);
        Assert.Contains("unsupported compression method 0x10", Assert.Throws<MpqException>(() => archive.ReadFile("bzip2.bin")).Message);
        Assert.Contains("corrupt sector offsets", Assert.Throws<MpqException>(() => archive.ReadFile("reversed.bin")).Message);
        Assert.Contains("missing sector offset table", Assert.Throws<MpqException>(() => archive.ReadFile("single-imploded.bin")).Message);
    }
}
