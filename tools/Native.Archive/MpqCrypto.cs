namespace Godevilution.Tools.Archive;

/**
 * MPQ v1 hashing and block cipher. Faithful port of DevilutionX's pinned
 * mpqfs `mpq_crypto.c` (MIT, commit c4e1327feb46): StormLib-compatible
 * tables, locale-independent name normalization and file-key derivation.
 */
public static class MpqCrypto
{
    public const uint HashTableIndex = 0x000;
    public const uint HashNameA = 0x100;
    public const uint HashNameB = 0x200;
    public const uint HashFileKey = 0x300;
    private const uint HashKey2Mix = 0x400;

    private static readonly uint[] CryptTable = BuildCryptTable();

    // mpqfs MpqAsciiToUpper: only a-z are upper-cased and '/' becomes '\'.
    private static byte Normalize(byte value)
        => value switch {
            >= (byte)'a' and <= (byte)'z' => (byte)(value - 0x20),
            (byte)'/' => (byte)'\\',
            _ => value,
        };

    private static uint[] BuildCryptTable()
    {
        var table = new uint[0x500];
        uint seed = 0x00100001;
        for (uint index1 = 0; index1 < 0x100; index1++) {
            var index2 = index1;
            for (var i = 0; i < 5; i++, index2 += 0x100) {
                seed = (seed * 125 + 3) % 0x2AAAAB;
                var temp1 = (seed & 0xFFFF) << 0x10;
                seed = (seed * 125 + 3) % 0x2AAAAB;
                var temp2 = seed & 0xFFFF;
                table[index2] = temp1 | temp2;
            }
        }
        return table;
    }

    /** mpq_hash_string over the raw bytes of an archive path. */
    public static uint HashString(ReadOnlySpan<byte> name, uint hashType)
    {
        uint seed1 = 0x7FED7FED;
        uint seed2 = 0xEEEEEEEE;
        foreach (var raw in name) {
            uint ch = Normalize(raw);
            seed1 = CryptTable[hashType + ch] ^ unchecked(seed1 + seed2);
            seed2 = unchecked(ch + seed1 + seed2 + (seed2 << 5) + 3);
        }
        return seed1;
    }

    public static uint HashString(string name, uint hashType)
        => HashString(MpqArchive.EncodeName(name), hashType);

    public static void DecryptBlock(Span<uint> data, uint key)
    {
        uint seed = 0xEEEEEEEE;
        for (var i = 0; i < data.Length; i++) {
            seed = unchecked(seed + CryptTable[HashKey2Mix + (key & 0xFF)]);
            var ch = data[i] ^ unchecked(key + seed);
            data[i] = ch;
            key = unchecked((~key << 0x15) + 0x11111111) | (key >> 0x0B);
            seed = unchecked(ch + seed + (seed << 5) + 3);
        }
    }

    public static void EncryptBlock(Span<uint> data, uint key)
    {
        uint seed = 0xEEEEEEEE;
        for (var i = 0; i < data.Length; i++) {
            seed = unchecked(seed + CryptTable[HashKey2Mix + (key & 0xFF)]);
            var ch = data[i];
            data[i] = ch ^ unchecked(key + seed);
            key = unchecked((~key << 0x15) + 0x11111111) | (key >> 0x0B);
            seed = unchecked(ch + seed + (seed << 5) + 3);
        }
    }

    /** mpq_file_key: hashes only the name after the last '\' or '/'. */
    public static uint FileKey(ReadOnlySpan<byte> path, uint blockOffset, uint fileSize, bool adjust)
    {
        var name = path;
        var backslash = name.LastIndexOf((byte)'\\');
        if (backslash >= 0)
            name = name[(backslash + 1)..];
        var slash = name.LastIndexOf((byte)'/');
        if (slash >= 0)
            name = name[(slash + 1)..];
        var key = HashString(name, HashFileKey);
        return adjust ? unchecked(key + blockOffset) ^ fileSize : key;
    }
}
