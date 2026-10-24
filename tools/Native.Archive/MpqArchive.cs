using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Godevilution.Tools.Archive;

/** Raised for malformed archives, missing files and unsupported encodings. */
public sealed class MpqException(string message) : IOException(message);

/**
 * Read-only MPQ v1 archive. Faithful port of the read side of DevilutionX's
 * pinned mpqfs (`mpq_archive.c`, `mpq_stream.c`; MIT, commit c4e1327feb46).
 * Supports what Diablo and Hellfire archives use: PKWARE implode, per-file
 * encryption with optional key adjustment, single-unit and stored files.
 * Multi-method sectors other than PKWARE are refused, as in an mpqfs build
 * without zlib/bzip2. Client-side only; the server never opens archives.
 */
public sealed class MpqArchive : IDisposable
{
    public const uint Signature = 0x1A51504D;
    public const uint HashEntryEmpty = 0xFFFFFFFF;
    public const uint HashEntryDeleted = 0xFFFFFFFE;
    public const uint FileImplode = 0x00000100;
    public const uint FileCompress = 0x00000200;
    public const uint FileEncrypted = 0x00010000;
    public const uint FileFixKey = 0x00020000;
    public const uint FileSingleUnit = 0x01000000;
    public const uint FileExists = 0x80000000;
    private const byte CompressionPkware = 0x08;
    private const int HeaderSize = 32;
    private const long MaximumHeaderSearch = 128L * 1024 * 1024;

    private readonly FileStream stream;
    private readonly HashEntry[] hashTable;
    private readonly BlockEntry[] blockTable;

    private readonly record struct HashEntry(uint HashA, uint HashB, uint BlockIndex);

    private readonly record struct BlockEntry(uint Offset, uint CompressedSize, uint FileSize, uint Flags);

    private MpqArchive(FileStream stream, long archiveOffset, uint sectorSize,
        HashEntry[] hashTable, BlockEntry[] blockTable)
    {
        this.stream = stream;
        ArchiveOffset = archiveOffset;
        SectorSize = sectorSize;
        this.hashTable = hashTable;
        this.blockTable = blockTable;
    }

    /** Byte offset of the MPQ header within the file (512-byte aligned). */
    public long ArchiveOffset { get; }

    public uint SectorSize { get; }

    public int HashTableCount => hashTable.Length;

    public int BlockTableCount => blockTable.Length;

    public static MpqArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try {
            return Open(stream, path);
        } catch {
            stream.Dispose();
            throw;
        }
    }

    private static MpqArchive Open(FileStream stream, string source)
    {
        // MpqFindHeader: the signature sits on a 512-byte boundary.
        long archiveOffset = -1;
        Span<byte> probe = stackalloc byte[4];
        for (long offset = 0; offset < MaximumHeaderSearch; offset += 512) {
            if (!TryRead(stream, offset, probe))
                break;
            if (BinaryPrimitives.ReadUInt32LittleEndian(probe) == Signature) {
                archiveOffset = offset;
                break;
            }
        }
        if (archiveOffset < 0)
            throw new MpqException($"{source}: MPQ signature not found");

        Span<byte> header = stackalloc byte[HeaderSize];
        if (!TryRead(stream, archiveOffset, header))
            throw new MpqException($"{source}: failed to read MPQ header");
        var formatVersion = BinaryPrimitives.ReadUInt16LittleEndian(header[12..]);
        if (formatVersion != 0) {
            throw new MpqException(
                $"{source}: unsupported format version {formatVersion} (only v1 / version 0 is supported)");
        }
        var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header[14..]);
        if (sectorShift > 22)
            throw new MpqException($"{source}: sector size shift {sectorShift} is out of range");
        var hashOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        var blockOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
        var hashCount = BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);
        var blockCount = BinaryPrimitives.ReadUInt32LittleEndian(header[28..]);

        var hashWords = ReadTable(stream, archiveOffset + hashOffset, hashCount, "(hash table)")
            ?? throw new MpqException($"{source}: failed to load hash table");
        var blockWords = ReadTable(stream, archiveOffset + blockOffset, blockCount, "(block table)")
            ?? throw new MpqException($"{source}: failed to load block table");
        var hashes = new HashEntry[hashCount];
        for (var i = 0; i < hashes.Length; i++)
            hashes[i] = new(hashWords[i * 4], hashWords[i * 4 + 1], hashWords[i * 4 + 3]);
        var blocks = new BlockEntry[blockCount];
        for (var i = 0; i < blocks.Length; i++)
            blocks[i] = new(blockWords[i * 4], blockWords[i * 4 + 1], blockWords[i * 4 + 2], blockWords[i * 4 + 3]);
        return new(stream, archiveOffset, 512u << sectorShift, hashes, blocks);
    }

    private static uint[]? ReadTable(FileStream stream, long offset, uint count, string keyName)
    {
        var bytes = (long)count * 16;
        if (offset < 0 || bytes > stream.Length || offset > stream.Length - bytes)
            return null;
        var raw = new byte[bytes];
        if (!TryRead(stream, offset, raw))
            return null;
        var words = new uint[count * 4];
        for (var i = 0; i < words.Length; i++)
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i * 4));
        MpqCrypto.DecryptBlock(words, MpqCrypto.HashString(Encoding.ASCII.GetBytes(keyName), MpqCrypto.HashFileKey));
        return words;
    }

    /** Archive paths are byte strings; refuse characters a byte cannot hold. */
    public static byte[] EncodeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var bytes = new byte[name.Length];
        for (var i = 0; i < name.Length; i++) {
            if (name[i] is '\0' or > 'ÿ')
                throw new ArgumentException($"Archive path '{name}' contains a character that is not a single byte.", nameof(name));
            bytes[i] = (byte)name[i];
        }
        return bytes;
    }

    // mpq_lookup_hash_entry: open addressing from the index hash.
    private int FindHashEntry(byte[] name)
    {
        if (hashTable.Length == 0)
            return -1;
        var count = (uint)hashTable.Length;
        var index = MpqCrypto.HashString(name, MpqCrypto.HashTableIndex) % count;
        var nameA = MpqCrypto.HashString(name, MpqCrypto.HashNameA);
        var nameB = MpqCrypto.HashString(name, MpqCrypto.HashNameB);
        var start = index;
        while (true) {
            var entry = hashTable[index];
            if (entry.BlockIndex == HashEntryEmpty)
                return -1;
            if (entry.BlockIndex != HashEntryDeleted && entry.HashA == nameA && entry.HashB == nameB)
                return entry.BlockIndex < (uint)blockTable.Length ? (int)index : -1;
            index = (index + 1) % count;
            if (index == start)
                return -1;
        }
    }

    private int FindBlock(byte[] name)
    {
        var hash = FindHashEntry(name);
        return hash < 0 ? -1 : (int)hashTable[hash].BlockIndex;
    }

    /** mpqfs_has_file: found and the block carries MPQ_FILE_EXISTS. */
    public bool Contains(string name)
    {
        var block = FindBlock(EncodeName(name));
        return block >= 0 && (blockTable[block].Flags & FileExists) != 0;
    }

    /** mpqfs_file_size: zero for missing files, as in native. */
    public long GetFileSize(string name)
    {
        var block = FindBlock(EncodeName(name));
        return block >= 0 && (blockTable[block].Flags & FileExists) != 0 ? blockTable[block].FileSize : 0;
    }

    /** mpqfs_read_file. Throws MpqException where native returns NULL. */
    public byte[] ReadFile(string name)
    {
        var encoded = EncodeName(name);
        var blockIndex = FindBlock(encoded);
        if (blockIndex < 0)
            throw new MpqException($"mpqfs_read_file: file '{name}' not found");
        var block = blockTable[blockIndex];
        if ((block.Flags & FileExists) == 0)
            throw new MpqException($"mpq_stream_open: block {blockIndex} does not exist");

        var fileKey = (block.Flags & FileEncrypted) != 0
            ? MpqCrypto.FileKey(encoded, block.Offset, block.FileSize, (block.Flags & FileFixKey) != 0)
            : 0u;
        var output = new byte[block.FileSize];
        if (block.FileSize == 0)
            return output;

        var compressed = (block.Flags & (FileImplode | FileCompress)) != 0;
        var sectorSize = SectorSize;
        var sectorCount = (block.FileSize + sectorSize - 1) / sectorSize;
        uint[]? offsets = null;
        var fileStart = ArchiveOffset + block.Offset;
        if (compressed && (block.Flags & FileSingleUnit) == 0) {
            var entries = sectorCount + 1;
            var table = new byte[entries * 4];
            if (!TryRead(stream, fileStart, table))
                throw new MpqException($"mpq_stream_open: failed to read sector offset table for block {blockIndex}");
            offsets = new uint[entries];
            for (var i = 0; i < offsets.Length; i++)
                offsets[i] = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(i * 4));
            // The offset table uses the base key minus one, without sector index.
            if (fileKey != 0)
                MpqCrypto.DecryptBlock(offsets, unchecked(fileKey - 1));
            if (offsets[0] != table.Length) {
                throw new MpqException(
                    $"mpq_stream_open: sector offset table validation failed for block {blockIndex} "
                    + $"(expected first entry {table.Length}, got {offsets[0]} — table may be encrypted or corrupt)");
            }
        } else if ((block.Flags & FileSingleUnit) != 0) {
            sectorCount = 1;
            if (block.FileSize > sectorSize)
                sectorSize = block.FileSize;
        }

        for (uint sector = 0; sector < sectorCount; sector++) {
            var remaining = block.FileSize - sector * sectorSize;
            var expect = (int)Math.Min(remaining, sectorSize);
            var target = output.AsSpan((int)(sector * sectorSize), expect);
            var sectorKey = unchecked(fileKey + sector);
            if (!compressed) {
                if (!TryRead(stream, fileStart + (long)sector * sectorSize, target))
                    throw new MpqException($"mpq_stream: read error on raw sector {sector}");
                if (fileKey != 0)
                    Decrypt(target, sectorKey);
                continue;
            }
            if (offsets is null)
                throw new MpqException("mpq_stream: missing sector offset table");
            var start = offsets[sector];
            var end = offsets[sector + 1];
            if (end < start) {
                throw new MpqException(
                    $"mpq_stream: corrupt sector offsets (sector {sector}: start={start} end={end})");
            }
            var compressedSize = end - start;
            if (compressedSize == 0) {
                // Zero-length sector — all zeros; `output` is already zeroed.
                continue;
            }
            if (compressedSize == expect) {
                // Stored sector: the compressor gave up.
                if (!TryRead(stream, fileStart + start, target))
                    throw new MpqException($"mpq_stream: read error on uncompressed sector {sector}");
                if (fileKey != 0)
                    Decrypt(target, sectorKey);
                continue;
            }
            var source = new byte[compressedSize];
            if (!TryRead(stream, fileStart + start, source))
                throw new MpqException($"mpq_stream: read error on compressed sector {sector}");
            if (fileKey != 0)
                Decrypt(source, sectorKey);
            ReadOnlySpan<byte> payload = source;
            if ((block.Flags & FileImplode) == 0) {
                var method = source[0];
                if ((method & ~CompressionPkware) != 0) {
                    throw new MpqException(
                        $"mpq_stream: unsupported compression method 0x{method:X2} on sector {sector}");
                }
                if ((method & CompressionPkware) == 0) {
                    // Native leaves such sectors undecoded; refuse rather than guess.
                    throw new MpqException(
                        $"mpq_stream: compression method 0x{method:X2} on sector {sector} names no decoder");
                }
                payload = payload[1..];
            }
            // Native leaves bytes past a short explode undefined; they stay zero here.
            var status = PkwareExplode.Explode(payload, target, out _);
            if (status != PkwareStatus.Ok)
                throw new MpqException($"mpq_stream: PKWARE explode failed on sector {sector} (rc={(int)status})");
        }
        return output;
    }

    // mpq_decrypt_block on whole little-endian dwords; trailing bytes stay plain.
    private static void Decrypt(Span<byte> data, uint key)
    {
        var words = MemoryMarshal.Cast<byte, uint>(data[..(data.Length / 4 * 4)]);
        if (!BitConverter.IsLittleEndian) {
            for (var i = 0; i < words.Length; i++)
                words[i] = BinaryPrimitives.ReverseEndianness(words[i]);
        }
        MpqCrypto.DecryptBlock(words, key);
        if (!BitConverter.IsLittleEndian) {
            for (var i = 0; i < words.Length; i++)
                words[i] = BinaryPrimitives.ReverseEndianness(words[i]);
        }
    }

    private static bool TryRead(FileStream stream, long offset, Span<byte> buffer)
    {
        if (offset < 0 || offset > stream.Length || buffer.Length > stream.Length - offset)
            return false;
        stream.Position = offset;
        stream.ReadExactly(buffer);
        return true;
    }

    public void Dispose() => stream.Dispose();
}
