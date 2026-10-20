namespace Devilution.Diablo;

/** Explicit entry policies for ordinary quest-disabled Catacombs generation. */
public enum AuthoritativeCatacombsEntry
{
    Main,
    Previous,
    TownWarpDown,
}

public enum AuthoritativeCatacombsGenerationPolicy
{
    QuestDisabledOriginalDiabloStairs,
}

/** Native coarse rectangle; empty set pieces retain their zero coordinates. */
public readonly record struct AuthoritativeCatacombsRectangle(int X, int Y, int Width, int Height);

/** Generated native theme location, before theme entity initialization. */
public sealed record AuthoritativeCatacombsThemeRoom(int X, int Y, int Width, int Height, int Transparency);

/**
 * Pure L2 generation output. Coarse tile IDs are one-based; transparency is the
 * complete native fine grid. External TIL/SOL metadata supplies pieces later.
 * InitialLevelSeed distinguishes an absent retained seed from retained zero.
 */
public sealed record AuthoritativeCatacombsGenerationResult(
    int Depth,
    AuthoritativeCatacombsGenerationPolicy Policy,
    AuthoritativeCatacombsEntry Entry,
    uint InputSeed,
    uint? InitialLevelSeed,
    uint AttemptSeed,
    uint FinalRandomState,
    IReadOnlyList<ushort> MegaTiles,
    IReadOnlyList<ushort> ReplacementMegaTiles,
    IReadOnlyList<bool> ProtectedMegaTiles,
    IReadOnlyList<byte> NativeTransparency,
    IReadOnlyList<byte> NativeTileFlags,
    IReadOnlyList<AuthoritativeCatacombsThemeRoom> Themes,
    AuthoritativeCathedralPoint StairsUp,
    AuthoritativeCathedralPoint StairsDown,
    AuthoritativeCathedralPoint? TownWarpStairs,
    AuthoritativeCathedralPoint EntryPosition,
    AuthoritativeCathedralPoint MinimumBounds,
    AuthoritativeCathedralPoint MaximumBounds,
    AuthoritativeCatacombsRectangle SetPieceRoom,
    AuthoritativeCatacombsRectangle SetPiece,
    int NextTransparencyValue)
{
    public const int MegaWidth = 40;
    public const int MegaHeight = 40;
    public const int FineWidth = 112;
    public const int FineHeight = 112;

    public ushort GetMegaTile(int x, int y)
        => MegaTiles[Cell(x, y, MegaWidth, MegaHeight)];

    public byte GetTransparency(int x, int y)
        => NativeTransparency[Cell(x, y, FineWidth, FineHeight)];

    internal AuthoritativeCatacombsGenerationResult CopyOwned()
        => this with {
            MegaTiles = Array.AsReadOnly(MegaTiles.ToArray()),
            ReplacementMegaTiles = Array.AsReadOnly(ReplacementMegaTiles.ToArray()),
            ProtectedMegaTiles = Array.AsReadOnly(ProtectedMegaTiles.ToArray()),
            NativeTransparency = Array.AsReadOnly(NativeTransparency.ToArray()),
            NativeTileFlags = Array.AsReadOnly(NativeTileFlags.ToArray()),
            Themes = Array.AsReadOnly(Themes.Select(theme => theme with { }).ToArray()),
        };

    internal void Validate()
    {
        if (Depth is < 5 or > 8 || Policy != AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs
            || !Enum.IsDefined(Entry) || (Entry == AuthoritativeCatacombsEntry.TownWarpDown && Depth != 5))
            throw new InvalidDataException("Catacombs generation requires its explicit L5-L8 policy and supported entry.");
        if (MegaTiles is null || MegaTiles.Count != MegaWidth * MegaHeight
            || ReplacementMegaTiles is null || ReplacementMegaTiles.Count != MegaWidth * MegaHeight
            || ProtectedMegaTiles is null || ProtectedMegaTiles.Count != MegaWidth * MegaHeight
            || MegaTiles.Any(value => value is 0 or > 160)
            || ReplacementMegaTiles.Any(value => value is 0 or > 160))
            throw new InvalidDataException("Catacombs generation requires complete native current/replacement/protected coarse layers.");
        if (NativeTransparency is null || NativeTransparency.Count != FineWidth * FineHeight
            || NativeTileFlags is null || NativeTileFlags.Count != FineWidth * FineHeight
            || NativeTileFlags.Any(value => value != 0))
            throw new InvalidDataException("Quest-disabled Catacombs generation requires full fine transparency and empty pre-population tile flags.");
        if (MinimumBounds != new AuthoritativeCathedralPoint(16, 16)
            || MaximumBounds != new AuthoritativeCathedralPoint(96, 96)
            || SetPieceRoom != default || SetPiece != default
            || NextTransparencyValue is < sbyte.MinValue or > sbyte.MaxValue)
            throw new InvalidDataException("Catacombs generation has invalid native bounds, set pieces or signed transparency counter.");
        ValidateAnchor(StairsUp);
        ValidateAnchor(StairsDown);
        if ((Depth == 5) != TownWarpStairs.HasValue)
            throw new InvalidDataException("Only ordinary L5 has a generated Town warp stair.");
        if (TownWarpStairs is { } townWarp)
            ValidateAnchor(townWarp);
        var anchor = Entry switch {
            AuthoritativeCatacombsEntry.Main => StairsUp,
            AuthoritativeCatacombsEntry.Previous => StairsDown,
            _ => TownWarpStairs!.Value,
        };
        var expected = Entry == AuthoritativeCatacombsEntry.Previous
            ? new AuthoritativeCathedralPoint(16 + 2 * anchor.X + 4, 16 + 2 * anchor.Y + 6)
            : new AuthoritativeCathedralPoint(16 + 2 * anchor.X + 5, 16 + 2 * anchor.Y + 4);
        if (EntryPosition != expected)
            throw new InvalidDataException("Catacombs entry does not match the selected native stair anchor.");
        if (Themes is null || Themes.Count > 50 || Themes.Any(theme => theme is null
                || (uint)theme.X >= MegaWidth || (uint)theme.Y >= MegaHeight
                || theme.Width <= 0 || theme.Height <= 0
                || theme.Width > MegaWidth - theme.X || theme.Height > MegaHeight - theme.Y
                || theme.Transparency is < sbyte.MinValue or > sbyte.MaxValue))
            throw new InvalidDataException("Catacombs generation has an invalid ordered native theme room.");
    }

    private static void ValidateAnchor(AuthoritativeCathedralPoint point)
    {
        if ((uint)point.X >= MegaWidth || (uint)point.Y >= MegaHeight)
            throw new InvalidDataException("Catacombs stair anchor is outside its coarse grid.");
    }

    private static int Cell(int x, int y, int width, int height)
    {
        if ((uint)x >= width || (uint)y >= height)
            throw new ArgumentOutOfRangeException();
        return y * width + x;
    }
}
