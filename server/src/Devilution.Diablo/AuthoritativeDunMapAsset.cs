using System.Buffers.Binary;

namespace Devilution.Diablo;

/**
 * Parsed native .dun set-piece data. The collision meaning of tile IDs stays
 * in the future geometry materializer; this type preserves every native layer
 * without projecting presentation data into gameplay rules.
 */
public sealed class AuthoritativeDunMapAsset
{
    private const int HeaderWordCount = 2;
    private const int DPieceLayerCount = 4;

    private AuthoritativeDunMapAsset(
        string sourcePath,
        int megaWidth,
        int megaHeight,
        IReadOnlyList<ushort> tileLayer,
        IReadOnlyList<ushort> reservedLayer,
        IReadOnlyList<ushort> monsterLayer,
        IReadOnlyList<ushort> objectLayer,
        IReadOnlyList<ushort> transparencyLayer)
    {
        SourcePath = sourcePath;
        MegaWidth = megaWidth;
        MegaHeight = megaHeight;
        TileLayer = tileLayer;
        ReservedLayer = reservedLayer;
        MonsterLayer = monsterLayer;
        ObjectLayer = objectLayer;
        TransparencyLayer = transparencyLayer;
    }

    public string SourcePath { get; }

    /** Native dimensions before the d-piece layer is doubled. */
    public int MegaWidth { get; }

    public int MegaHeight { get; }

    /** Dimensions of the monster/object/transparency layers. */
    public int TileWidth => checked(MegaWidth * 2);

    public int TileHeight => checked(MegaHeight * 2);

    /** Native mega-tile IDs, stored row-major. */
    public IReadOnlyList<ushort> TileLayer { get; }

    /** Native d-piece layer zero, retained because the legacy loader skips it. */
    public IReadOnlyList<ushort> ReservedLayer { get; }

    /** Native monster IDs, stored at d-piece resolution. */
    public IReadOnlyList<ushort> MonsterLayer { get; }

    /** Native object IDs, stored at d-piece resolution. */
    public IReadOnlyList<ushort> ObjectLayer { get; }

    /** Native transparency values, stored at d-piece resolution as the low-byte
     * encoding of DevilutionX's signed int8_t transparency field. */
    public IReadOnlyList<ushort> TransparencyLayer { get; }

    public ushort GetObjectId(int positionX, int positionY)
        => GetLayerValue(ObjectLayer, positionX, positionY);

    public ushort GetMonsterId(int positionX, int positionY)
        => GetLayerValue(MonsterLayer, positionX, positionY);

    public ushort GetTransparencyValue(int positionX, int positionY)
        => GetLayerValue(TransparencyLayer, positionX, positionY);

    public static AuthoritativeDunMapAsset Load(
        string sourcePath,
        ReadOnlyMemory<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var bytes = contents.Span;
        if (bytes.Length < HeaderWordCount * sizeof(ushort) || bytes.Length % sizeof(ushort) != 0)
            throw new InvalidDataException($"Native .dun asset '{sourcePath}' has an invalid length.");

        var megaWidth = ReadWord(bytes, 0);
        var megaHeight = ReadWord(bytes, 1);
        if (megaWidth is 0 or > 40 || megaHeight is 0 or > 40)
            throw new InvalidDataException(
                $"Native .dun asset '{sourcePath}' has invalid dimensions {megaWidth}x{megaHeight}.");

        var megaCount = checked(megaWidth * megaHeight);
        var dPieceCount = checked(megaCount * 4);
        var expectedWordCount = checked(HeaderWordCount + megaCount + DPieceLayerCount * dPieceCount);
        if (bytes.Length != checked(expectedWordCount * sizeof(ushort)))
            throw new InvalidDataException(
                $"Native .dun asset '{sourcePath}' has {bytes.Length} bytes; expected "
                + $"{expectedWordCount * sizeof(ushort)}.");

        var tileLayer = ReadLayer(bytes, HeaderWordCount, megaCount);
        var dPieceOffset = HeaderWordCount + megaCount;
        var reservedLayer = ReadLayer(bytes, dPieceOffset, dPieceCount);
        var monsterLayer = ReadLayer(bytes, dPieceOffset + dPieceCount, dPieceCount);
        var objectLayer = ReadLayer(bytes, dPieceOffset + dPieceCount * 2, dPieceCount);
        var transparencyLayer = ReadTransparencyLayer(
            bytes,
            dPieceOffset + dPieceCount * 3,
            dPieceCount);
        return new AuthoritativeDunMapAsset(
            sourcePath,
            megaWidth,
            megaHeight,
            tileLayer,
            reservedLayer,
            monsterLayer,
            objectLayer,
            transparencyLayer);
    }

    private static ushort ReadWord(ReadOnlySpan<byte> bytes, int wordIndex)
        => BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.Slice(checked(wordIndex * sizeof(ushort)), sizeof(ushort)));

    private static IReadOnlyList<ushort> ReadLayer(
        ReadOnlySpan<byte> bytes,
        int startWord,
        int count)
    {
        var values = new ushort[count];
        for (var index = 0; index < count; index++)
            values[index] = ReadWord(bytes, checked(startWord + index));
        return Array.AsReadOnly(values);
    }

    private static IReadOnlyList<ushort> ReadTransparencyLayer(
        ReadOnlySpan<byte> bytes,
        int startWord,
        int count)
    {
        var values = new ushort[count];
        for (var index = 0; index < count; index++) {
            // The native loader casts each 16-bit DUN word to int8_t. Keep
            // the equivalent unsigned wire byte so protocol and managed
            // callers compare the same region identity regardless of whether
            // the source word was sign-extended (for example 0xFFFF).
            values[index] = (ushort)(ReadWord(
                bytes,
                checked(startWord + index)) & byte.MaxValue);
        }
        return Array.AsReadOnly(values);
    }

    private ushort GetLayerValue(
        IReadOnlyList<ushort> layer,
        int positionX,
        int positionY)
    {
        if (positionX < 0 || positionX >= TileWidth || positionY < 0 || positionY >= TileHeight)
            throw new ArgumentOutOfRangeException();
        return layer[positionY * TileWidth + positionX];
    }
}
