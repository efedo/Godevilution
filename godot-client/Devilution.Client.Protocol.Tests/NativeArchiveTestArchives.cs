using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Godevilution.Tools.Archive;

namespace Devilution.Client.Protocol.Tests;

/** LSB-first PKWARE DCL encoder that emits exactly the codes the decoder reads. */
internal sealed class PkwareTestEncoder
{
    private readonly List<byte> bytes = [];
    private uint pending;
    private int pendingBits;

    public PkwareTestEncoder(bool ascii, int dictionaryBits)
    {
        Ascii = ascii;
        DictionaryBits = dictionaryBits;
        bytes.Add(ascii ? (byte)1 : (byte)0);
        bytes.Add((byte)dictionaryBits);
    }

    public bool Ascii { get; }

    public int DictionaryBits { get; }

    public void WriteBits(uint value, int count)
    {
        for (var i = 0; i < count; i++) {
            pending |= ((value >> i) & 1u) << pendingBits;
            if (++pendingBits == 8) {
                bytes.Add((byte)pending);
                pending = 0;
                pendingBits = 0;
            }
        }
    }

    public PkwareTestEncoder Literal(byte value)
    {
        WriteBits(0, 1);
        if (Ascii)
            WriteBits(PkwareExplode.AsciiLiteralCodes[value], PkwareExplode.AsciiLiteralBits[value]);
        else
            WriteBits(value, 8);
        return this;
    }

    public PkwareTestEncoder Literals(ReadOnlySpan<byte> values)
    {
        foreach (var value in values)
            Literal(value);
        return this;
    }

    /** Repeats `length` bytes from `distance + 1` bytes back. */
    public PkwareTestEncoder Match(int length, int distance)
    {
        WriteBits(1, 1);
        var index = -1;
        for (var i = PkwareExplode.LengthBase.Length - 1; i >= 0; i--) {
            if (length >= PkwareExplode.LengthBase[i] + 2) {
                index = i;
                break;
            }
        }
        var extra = (uint)(length - PkwareExplode.LengthBase[index] - 2);
        if (extra >= 1u << PkwareExplode.ExtraLengthBits[index])
            throw new ArgumentOutOfRangeException(nameof(length));
        WriteBits(PkwareExplode.LengthCodes[index], PkwareExplode.LengthBits[index]);
        WriteBits(extra, PkwareExplode.ExtraLengthBits[index]);
        var lowBits = length == 2 ? 2 : DictionaryBits;
        var distanceIndex = distance >> lowBits;
        WriteBits(PkwareExplode.DistanceCodes[distanceIndex], PkwareExplode.DistanceBits[distanceIndex]);
        WriteBits((uint)distance & ((1u << lowBits) - 1u), lowBits);
        return this;
    }

    public PkwareTestEncoder End()
    {
        WriteBits(1, 1);
        WriteBits(PkwareExplode.LengthCodes[15], PkwareExplode.LengthBits[15]);
        WriteBits(0xFF, 8);
        return this;
    }

    public byte[] ToArray()
    {
        var copy = new List<byte>(bytes);
        if (pendingBits > 0)
            copy.Add((byte)pending);
        return copy.ToArray();
    }
}

/** Builds small MPQ v1 archives, including encryption the mpqfs writer lacks. */
internal sealed class TestArchiveBuilder
{
    private readonly List<(string Name, byte[] Payload, uint FileSize, uint Flags, bool Deleted)> files = [];

    public int HashTableSize { get; init; } = 16;

    public int SectorShift { get; init; } = 3;

    public int Prefix { get; init; }

    public ushort FormatVersion { get; init; }

    public int SectorSize => 512 << SectorShift;

    /** Raw block payload; `payload` already contains any offset table. */
    public TestArchiveBuilder AddRaw(string name, byte[] payload, uint fileSize, uint flags, bool deleted = false)
    {
        files.Add((name, payload, fileSize, flags | MpqArchive.FileExists, deleted));
        return this;
    }

    public TestArchiveBuilder AddStored(string name, byte[] data, bool encrypted = false, bool fixKey = false, bool singleUnit = false)
    {
        var flags = (encrypted ? MpqArchive.FileEncrypted : 0)
            | (fixKey ? MpqArchive.FileFixKey : 0)
            | (singleUnit ? MpqArchive.FileSingleUnit : 0);
        files.Add((name, (byte[])data.Clone(), (uint)data.Length, flags | MpqArchive.FileExists, false));
        return this;
    }

    /** Imploded file; each sector is a literal-only binary stream unless stored. */
    public TestArchiveBuilder AddImploded(string name, byte[] data, bool encrypted = false, bool fixKey = false,
        Func<int, byte[], byte[]?>? sectorPayload = null)
    {
        var flags = MpqArchive.FileImplode | (encrypted ? MpqArchive.FileEncrypted : 0) | (fixKey ? MpqArchive.FileFixKey : 0);
        files.Add((name, data, (uint)data.Length, flags | MpqArchive.FileExists, false));
        sectorBuilders[name] = sectorPayload;
        return this;
    }

    private readonly Dictionary<string, Func<int, byte[], byte[]?>?> sectorBuilders = [];

    public byte[] Build()
    {
        var output = new List<byte>(new byte[Prefix]);
        var headerOffset = output.Count;
        output.AddRange(new byte[32]);
        var blocks = new List<(uint Offset, uint Compressed, uint Size, uint Flags)>();
        foreach (var file in files) {
            var offset = (uint)(output.Count - headerOffset);
            var encoded = MpqArchive.EncodeName(file.Name);
            var key = (file.Flags & MpqArchive.FileEncrypted) != 0
                ? MpqCrypto.FileKey(encoded, offset, file.FileSize, (file.Flags & MpqArchive.FileFixKey) != 0)
                : 0u;
            byte[] payload;
            if (sectorBuilders.TryGetValue(file.Name, out var builder)) {
                payload = BuildImploded(file.Payload, key, builder);
            } else if ((file.Flags & (MpqArchive.FileImplode | MpqArchive.FileCompress)) == 0) {
                payload = (byte[])file.Payload.Clone();
                var sectorSize = (file.Flags & MpqArchive.FileSingleUnit) != 0 ? Math.Max(payload.Length, 1) : SectorSize;
                for (var sector = 0; sector * sectorSize < payload.Length; sector++) {
                    var length = Math.Min(sectorSize, payload.Length - sector * sectorSize);
                    if (key != 0)
                        Encrypt(payload.AsSpan(sector * sectorSize, length), unchecked(key + (uint)sector));
                }
            } else {
                payload = file.Payload;
            }
            output.AddRange(payload);
            blocks.Add((offset, (uint)payload.Length, file.FileSize, file.Flags));
        }

        var hashWords = new uint[HashTableSize * 4];
        for (var i = 0; i < HashTableSize; i++) {
            hashWords[i * 4] = 0xFFFFFFFF;
            hashWords[i * 4 + 1] = 0xFFFFFFFF;
            hashWords[i * 4 + 2] = 0xFFFFFFFF;
            hashWords[i * 4 + 3] = MpqArchive.HashEntryEmpty;
        }
        for (var blockIndex = 0; blockIndex < files.Count; blockIndex++) {
            var encoded = MpqArchive.EncodeName(files[blockIndex].Name);
            var slot = MpqCrypto.HashString(encoded, MpqCrypto.HashTableIndex) % (uint)HashTableSize;
            while (hashWords[slot * 4 + 3] != MpqArchive.HashEntryEmpty)
                slot = (slot + 1) % (uint)HashTableSize;
            hashWords[slot * 4] = MpqCrypto.HashString(encoded, MpqCrypto.HashNameA);
            hashWords[slot * 4 + 1] = MpqCrypto.HashString(encoded, MpqCrypto.HashNameB);
            hashWords[slot * 4 + 2] = 0;
            hashWords[slot * 4 + 3] = files[blockIndex].Deleted ? MpqArchive.HashEntryDeleted : (uint)blockIndex;
        }
        var hashOffset = (uint)(output.Count - headerOffset);
        MpqCrypto.EncryptBlock(hashWords, MpqCrypto.HashString("(hash table)", MpqCrypto.HashFileKey));
        foreach (var word in hashWords)
            output.AddRange(LittleEndian(word));

        var blockWords = new uint[blocks.Count * 4];
        for (var i = 0; i < blocks.Count; i++) {
            blockWords[i * 4] = blocks[i].Offset;
            blockWords[i * 4 + 1] = blocks[i].Compressed;
            blockWords[i * 4 + 2] = blocks[i].Size;
            blockWords[i * 4 + 3] = blocks[i].Flags;
        }
        var blockOffset = (uint)(output.Count - headerOffset);
        MpqCrypto.EncryptBlock(blockWords, MpqCrypto.HashString("(block table)", MpqCrypto.HashFileKey));
        foreach (var word in blockWords)
            output.AddRange(LittleEndian(word));

        var bytes = output.ToArray();
        var header = bytes.AsSpan(headerOffset, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(header, MpqArchive.Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)(bytes.Length - headerOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], (ushort)SectorShift);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], hashOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], blockOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)HashTableSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)blocks.Count);
        return bytes;
    }

    private byte[] BuildImploded(byte[] data, uint key, Func<int, byte[], byte[]?>? builder)
    {
        var sectorCount = (data.Length + SectorSize - 1) / SectorSize;
        var sectors = new List<byte[]>();
        for (var sector = 0; sector < sectorCount; sector++) {
            var plain = data.AsSpan(sector * SectorSize, Math.Min(SectorSize, data.Length - sector * SectorSize)).ToArray();
            var payload = builder?.Invoke(sector, plain)
                ?? new PkwareTestEncoder(false, 6).Literals(plain).End().ToArray();
            sectors.Add(payload);
        }
        var offsets = new uint[sectorCount + 1];
        offsets[0] = (uint)(offsets.Length * 4);
        for (var i = 0; i < sectorCount; i++)
            offsets[i + 1] = offsets[i] + (uint)sectors[i].Length;
        var output = new List<byte>();
        var table = (uint[])offsets.Clone();
        if (key != 0)
            MpqCrypto.EncryptBlock(table, unchecked(key - 1));
        foreach (var word in table)
            output.AddRange(LittleEndian(word));
        for (var i = 0; i < sectorCount; i++) {
            var sector = (byte[])sectors[i].Clone();
            if (key != 0)
                Encrypt(sector, unchecked(key + (uint)i));
            output.AddRange(sector);
        }
        return output.ToArray();
    }

    public static void Encrypt(Span<byte> data, uint key)
        => MpqCrypto.EncryptBlock(MemoryMarshal.Cast<byte, uint>(data[..(data.Length / 4 * 4)]), key);

    private static byte[] LittleEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    public string WriteTo(string directory, string name = "test.mpq")
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, Build());
        return path;
    }
}
