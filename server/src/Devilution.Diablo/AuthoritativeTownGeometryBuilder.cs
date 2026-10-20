using System.Collections.ObjectModel;

namespace Devilution.Diablo;

/** Materialized Town channels, still separate from the Cathedral level model. */
public sealed class AuthoritativeTownGeometry
{
    internal AuthoritativeTownGeometry(
        ushort[] finePieces,
        ushort[] megaTiles,
        ushort[] specialTiles,
        ushort[] transparency,
        IReadOnlyList<int> protectedMegaCells,
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags> pieceProperties,
        IReadOnlyList<AuthoritativeDunTileFlags> propertyLayer,
        IReadOnlyList<int> solidCells,
        IReadOnlyList<int> missileBlockedCells,
        IReadOnlyList<int> blockLightCells,
        AuthoritativeDunPieceCatalog pieceCatalog,
        AuthoritativeDunTilePropertyCatalog tileProperties,
        AuthoritativeTownWarpPolicy warpPolicy,
        AuthoritativeTownWarpOpenings warpOpenings,
        AuthoritativeTownEntry entry,
        int townWarpFrom,
        AuthoritativeTownWaterQuestState waterQuestState,
        uint initialRandomState,
        int viewPositionX,
        int viewPositionY,
        uint finalRandomState,
        IReadOnlyList<int> closedHellTileChoices)
    {
        NativePieceLayer = Array.AsReadOnly(finePieces);
        NativeMegaTileLayer = Array.AsReadOnly(megaTiles);
        NativeSpecialLayer = Array.AsReadOnly(specialTiles);
        NativeTransparencyLayer = Array.AsReadOnly(transparency);
        NativeProtectedMegaCells = Array.AsReadOnly(protectedMegaCells.ToArray());
        NativePieceProperties = new ReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>(
            pieceProperties.ToDictionary(pair => pair.Key, pair => pair.Value));
        NativeSolidCells = Array.AsReadOnly(solidCells.ToArray());
        NativeMissileBlockedCells = Array.AsReadOnly(missileBlockedCells.ToArray());
        NativeBlockLightCells = Array.AsReadOnly(blockLightCells.ToArray());
        NativeSolPropertyLayer = Array.AsReadOnly(propertyLayer.ToArray());
        PieceCatalog = pieceCatalog;
        TileProperties = tileProperties;
        WarpPolicy = warpPolicy;
        WarpPolicyVersion = AuthoritativeTownWarpPolicy.Version;
        WarpOpenings = warpOpenings;
        Entry = entry;
        TownWarpFrom = townWarpFrom;
        WaterQuestState = waterQuestState;
        InitialRandomState = initialRandomState;
        FinalRandomState = finalRandomState;
        ClosedHellTileChoices = Array.AsReadOnly(closedHellTileChoices.ToArray());
        ViewPositionX = viewPositionX;
        ViewPositionY = viewPositionY;
    }

    public const int FineWidth = 112;
    public const int FineHeight = 112;
    public const int MegaWidth = 40;
    public const int MegaHeight = 40;

    public IReadOnlyList<ushort> NativePieceLayer { get; }
    public IReadOnlyList<ushort> NativeMegaTileLayer { get; }
    public IReadOnlyList<ushort> NativeSpecialLayer { get; }
    public IReadOnlyList<ushort> NativeTransparencyLayer { get; }
    public IReadOnlyList<int> NativeProtectedMegaCells { get; }
    public IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags> NativePieceProperties { get; }
    public IReadOnlyList<int> NativeSolidCells { get; }
    public IReadOnlyList<int> NativeMissileBlockedCells { get; }
    public IReadOnlyList<int> NativeBlockLightCells { get; }
    public IReadOnlyList<AuthoritativeDunTileFlags> NativeSolPropertyLayer { get; }
    public AuthoritativeTownWarpPolicy WarpPolicy { get; }
    public string WarpPolicyVersion { get; }
    public AuthoritativeTownWarpOpenings WarpOpenings { get; }
    public AuthoritativeTownEntry Entry { get; }
    public int TownWarpFrom { get; }
    public AuthoritativeTownWaterQuestState WaterQuestState { get; }
    public uint InitialRandomState { get; }
    public uint FinalRandomState { get; }
    public IReadOnlyList<int> ClosedHellTileChoices { get; }
    public int ViewPositionX { get; }
    public int ViewPositionY { get; }

    /** CreateTown writes these native fine-grid bounds for every entry. */
    public int MinimumDungeonPositionX => 10;
    public int MinimumDungeonPositionY => 10;
    public int MaximumDungeonPositionX => 84;
    public int MaximumDungeonPositionY => 84;

    internal AuthoritativeDunPieceCatalog PieceCatalog { get; }
    internal AuthoritativeDunTilePropertyCatalog TileProperties { get; }

    internal AuthoritativeTownGeometry WithUpdatedLayers(
        ushort[] finePieces,
        ushort[] megaTiles)
    {
        var channels = AuthoritativeTownGeometryBuilder.DeriveSolChannels(
            finePieces,
            TileProperties);
        return new AuthoritativeTownGeometry(
            finePieces,
            megaTiles,
            NativeSpecialLayer.ToArray(),
            NativeTransparencyLayer.ToArray(),
            NativeProtectedMegaCells,
            NativePieceProperties,
            channels.Properties,
            channels.Solid,
            channels.Missile,
            channels.BlockLight,
            PieceCatalog,
            TileProperties,
            WarpPolicy,
            WarpOpenings,
            Entry,
            TownWarpFrom,
            WaterQuestState,
            InitialRandomState,
            ViewPositionX,
            ViewPositionY,
            FinalRandomState,
            ClosedHellTileChoices);
    }
}

/** Reproduces the legacy Town DrlgTPass3 composition from explicit assets. */
public static class AuthoritativeTownGeometryBuilder
{
    public const string PolicyVersion = AuthoritativeTownWarpPolicy.Version;

    private static readonly (ushort PieceId, byte SpecialId)[] TownSpecialMappings = [
        (359, 1), (357, 2), (128, 6), (129, 7), (127, 8), (116, 9),
        (156, 10), (157, 11), (155, 12), (161, 13), (159, 14),
        (213, 15), (211, 16), (216, 17), (215, 18),
    ];

    public static AuthoritativeTownGeometry Build(AuthoritativeTownGeometryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Sector1South);
        ArgumentNullException.ThrowIfNull(request.Sector2South);
        ArgumentNullException.ThrowIfNull(request.Sector3South);
        ArgumentNullException.ThrowIfNull(request.Sector4South);
        ArgumentNullException.ThrowIfNull(request.Automap);
        ArgumentNullException.ThrowIfNull(request.PieceCatalog);
        ArgumentNullException.ThrowIfNull(request.TileProperties);
        ArgumentNullException.ThrowIfNull(request.WarpPolicy);
        ValidateRequest(request);

        var finePieces = new ushort[AuthoritativeTownGeometry.FineWidth
            * AuthoritativeTownGeometry.FineHeight];
        Array.Fill(finePieces, (ushort)426);
        var transparency = new ushort[finePieces.Length];
        var megaTiles = CopyInitialMegaTiles(request.InitialMegaTileLayer);
        var protectedMegaCells = CopyInitialProtectedCells(request.InitialProtectedMegaCells);
        var specialTiles = request.Entry == AuthoritativeTownEntry.Load
            ? request.InitialSpecialLayer!.ToArray()
            : new ushort[AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight];
        var openings = request.WarpPolicy.ResolveOpenings();
        var viewPosition = ResolveViewPosition(request);

        FillSector(request.Sector1South, finePieces, 46, 46, request.PieceCatalog);
        FillSector(request.Sector2South, finePieces, 46, 0, request.PieceCatalog);
        FillSector(request.Sector3South, finePieces, 0, 46, request.PieceCatalog);
        FillSector(request.Sector4South, finePieces, 0, 0, request.PieceCatalog);
        PlaceAutomap(request.Automap, megaTiles, protectedMegaCells);

        var random = new AuthoritativeLegacyRandom(request.InitialRandomState);
        var closedHellTileChoices = new List<int>(10);
        if (!openings.Catacombs) {
            SetMegaTile(megaTiles, 20, 7, 10);
            SetMegaTile(megaTiles, 20, 6, 8);
            FillTile(finePieces, request.PieceCatalog, 48, 20, 320);
        }
        if (!openings.Caves) {
            SetMegaTile(megaTiles, 4, 30, 8);
            FillTile(finePieces, request.PieceCatalog, 16, 68, 332);
            FillTile(finePieces, request.PieceCatalog, 16, 70, 331);
        }
        if (!openings.Hell) {
            SetMegaTile(megaTiles, 15, 35, 7);
            SetMegaTile(megaTiles, 16, 35, 7);
            SetMegaTile(megaTiles, 17, 35, 7);
            for (var x = 36; x < 46; x++) {
                var choice = Math.Max(random.GenerateRnd(4), 0) + 1;
                closedHellTileChoices.Add(choice);
                FillTile(finePieces, request.PieceCatalog, x, 78, choice);
            }
        }

        if (request.WarpPolicy.IsHellfire) {
            ApplyHive(finePieces, megaTiles, openings.Nest);
            ApplyGrave(finePieces, megaTiles, openings.Crypt);
        }

        FillTile(
            finePieces,
            request.PieceCatalog,
            60,
            70,
            request.WaterQuestState is not AuthoritativeTownWaterQuestState.Done
                and not AuthoritativeTownWaterQuestState.NotAvailable ? 342 : 71);
        ApplyTownSpecialMappings(finePieces, specialTiles);

        var channels = DeriveSolChannels(finePieces, request.TileProperties);
        var pieceProperties = request.TileProperties.Definitions.ToDictionary(
            property => checked((ushort)property.PieceId),
            property => property.Flags);
        return new AuthoritativeTownGeometry(
            finePieces,
            megaTiles,
            specialTiles,
            transparency,
            protectedMegaCells.Order().ToArray(),
            pieceProperties,
            channels.Properties,
            channels.Solid,
            channels.Missile,
            channels.BlockLight,
            request.PieceCatalog,
            request.TileProperties,
            request.WarpPolicy,
            openings,
            request.Entry,
            request.TownWarpFrom,
            request.WaterQuestState,
            request.InitialRandomState,
            viewPosition.X,
            viewPosition.Y,
            random.State,
            closedHellTileChoices);
    }

    public static AuthoritativeTownGeometry OpenHive(AuthoritativeTownGeometry geometry)
        => Mutate(geometry, (pieces, mega) => ApplyHive(pieces, mega, true));

    public static AuthoritativeTownGeometry CloseHive(AuthoritativeTownGeometry geometry)
        => Mutate(geometry, (pieces, mega) => ApplyHive(pieces, mega, false));

    public static AuthoritativeTownGeometry OpenGrave(AuthoritativeTownGeometry geometry)
        => Mutate(geometry, (pieces, mega) => ApplyGrave(pieces, mega, true));

    public static AuthoritativeTownGeometry CloseGrave(AuthoritativeTownGeometry geometry)
        => Mutate(geometry, (pieces, mega) => ApplyGrave(pieces, mega, false));

    public static AuthoritativeTownGeometry CleanFountain(AuthoritativeTownGeometry geometry)
        => Mutate(geometry, (pieces, _) => FillTile(pieces, geometry.PieceCatalog, 60, 70, 71));

    internal static (
        IReadOnlyList<AuthoritativeDunTileFlags> Properties,
        IReadOnlyList<int> Solid,
        IReadOnlyList<int> Missile,
        IReadOnlyList<int> BlockLight)
        DeriveSolChannels(
            IReadOnlyList<ushort> finePieces,
            AuthoritativeDunTilePropertyCatalog tileProperties)
    {
        ArgumentNullException.ThrowIfNull(finePieces);
        ArgumentNullException.ThrowIfNull(tileProperties);
        if (finePieces.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight)
            throw new InvalidDataException("Native Town fine-piece layer has an invalid size.");

        var solid = new List<int>();
        var missile = new List<int>();
        var blockLight = new List<int>();
        var propertyLayer = new AuthoritativeDunTileFlags[finePieces.Count];
        for (var cell = 0; cell < finePieces.Count; cell++) {
            var pieceId = finePieces[cell];
            if (!tileProperties.TryGet(pieceId, out var flags))
                throw new InvalidDataException(
                    $"Native Town d-piece {pieceId} has no .sol properties.");
            propertyLayer[cell] = flags;
            if (flags.HasFlag(AuthoritativeDunTileFlags.Solid))
                solid.Add(cell);
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile))
                missile.Add(cell);
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockLight))
                blockLight.Add(cell);
        }
        return (
            Array.AsReadOnly(propertyLayer),
            solid.AsReadOnly(),
            missile.AsReadOnly(),
            blockLight.AsReadOnly());
    }

    private static AuthoritativeTownGeometry Mutate(
        AuthoritativeTownGeometry geometry,
        Action<ushort[], ushort[]> operation)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var pieces = geometry.NativePieceLayer.ToArray();
        var mega = geometry.NativeMegaTileLayer.ToArray();
        operation(pieces, mega);
        return geometry.WithUpdatedLayers(pieces, mega);
    }

    private static void ValidateRequest(AuthoritativeTownGeometryRequest request)
    {
        request.WarpPolicy.Validate();
        if (!Enum.IsDefined(request.Entry)
            || !Enum.IsDefined(request.WaterQuestState)
            || request.TownWarpFrom < 0
            || request.TownWarpFrom > byte.MaxValue
            || request.ExistingViewPositionX < 0
            || request.ExistingViewPositionX >= AuthoritativeTownGeometry.FineWidth
            || request.ExistingViewPositionY < 0
            || request.ExistingViewPositionY >= AuthoritativeTownGeometry.FineHeight)
            throw new ArgumentOutOfRangeException(nameof(request));

        if (request.Automap.MegaWidth != AuthoritativeTownGeometry.MegaWidth
            || request.Automap.MegaHeight != AuthoritativeTownGeometry.MegaHeight)
            throw new InvalidDataException("Native Town automap must be exactly 40x40 mega tiles.");
        ValidateSector(request.Sector1South, 46, 46);
        ValidateSector(request.Sector2South, 46, 0);
        ValidateSector(request.Sector3South, 0, 46);
        ValidateSector(request.Sector4South, 0, 0);
        ValidateRawByteLayer(request.Automap, "Town automap");

        ValidateInitialMegaTiles(request.InitialMegaTileLayer);
        ValidateInitialProtectedCells(request.InitialProtectedMegaCells);
        ValidateInitialSpecialLayer(request.InitialSpecialLayer);
        if (request.Entry == AuthoritativeTownEntry.Load && request.InitialSpecialLayer is null)
            throw new InvalidDataException(
                "Town ENTRY_LOAD requires the saved special layer because native Town initialization does not clear it.");
    }

    private static void ValidateSector(AuthoritativeTownTileMap sector, int offsetX, int offsetY)
    {
        if (sector.TileWidth + offsetX > AuthoritativeTownGeometry.FineWidth
            || sector.TileHeight + offsetY > AuthoritativeTownGeometry.FineHeight)
            throw new InvalidDataException(
                $"Native Town sector '{sector.SourcePath}' exceeds the 112x112 fine grid.");
    }

    private static void ValidateRawByteLayer(AuthoritativeTownTileMap asset, string name)
    {
        if (asset.TileLayer.Any(value => value > byte.MaxValue))
            throw new InvalidDataException($"{name} contains a mega-tile ID outside the native byte range.");
    }

    private static void ValidateInitialMegaTiles(IReadOnlyList<ushort>? layer)
    {
        if (layer is null)
            return;
        if (layer.Count != AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight
            || layer.Any(value => value > byte.MaxValue))
            throw new InvalidDataException("Initial Town mega-tile state is invalid.");
    }

    private static void ValidateInitialProtectedCells(IReadOnlyList<int>? protectedCells)
    {
        if (protectedCells is null)
            return;
        if (protectedCells.Any(cell => cell < 0
                || cell >= AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight)
            || protectedCells.Distinct().Count() != protectedCells.Count)
            throw new InvalidDataException("Initial Town protected-tile state is invalid.");
    }

    private static void ValidateInitialSpecialLayer(IReadOnlyList<ushort>? layer)
    {
        if (layer is null)
            return;
        if (layer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || layer.Any(value => value > byte.MaxValue))
            throw new InvalidDataException("Initial Town special layer is invalid.");
    }

    private static ushort[] CopyInitialMegaTiles(IReadOnlyList<ushort>? source)
        => source?.ToArray()
            ?? new ushort[AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight];

    private static HashSet<int> CopyInitialProtectedCells(IReadOnlyList<int>? source)
        => source?.ToHashSet() ?? [];

    private static void FillSector(
        AuthoritativeTownTileMap sector,
        ushort[] finePieces,
        int offsetX,
        int offsetY,
        AuthoritativeDunPieceCatalog pieceCatalog)
    {
        for (var megaY = 0; megaY < sector.MegaHeight; megaY++) {
            for (var megaX = 0; megaX < sector.MegaWidth; megaX++) {
                var source = sector.TileLayer[megaY * sector.MegaWidth + megaX];
                if (source == 0) {
                    SetMegaPieces(finePieces, offsetX + megaX * 2, offsetY + megaY * 2,
                        218, 218, 218, 218);
                    continue;
                }
                var megaTileId = (uint)(source - 1);
                if (!pieceCatalog.TryGet(megaTileId, out var definition))
                    throw new InvalidDataException(
                        $"Native Town sector '{sector.SourcePath}' references unknown one-based mega-tile {source}.");
                SetMegaPieces(
                    finePieces,
                    offsetX + megaX * 2,
                    offsetY + megaY * 2,
                    definition.NorthWestPieceId,
                    definition.NorthEastPieceId,
                    definition.SouthWestPieceId,
                    definition.SouthEastPieceId);
            }
        }
    }

    private static void PlaceAutomap(
        AuthoritativeTownTileMap automap,
        ushort[] megaTiles,
        HashSet<int> protectedMegaCells)
    {
        for (var y = 0; y < automap.MegaHeight; y++) {
            for (var x = 0; x < automap.MegaWidth; x++) {
                var tileId = automap.TileLayer[y * automap.MegaWidth + x];
                if (tileId == 0)
                    continue;
                var cell = y * AuthoritativeTownGeometry.MegaWidth + x;
                megaTiles[cell] = tileId;
                protectedMegaCells.Add(cell);
            }
        }
    }

    private static (int X, int Y) ResolveViewPosition(AuthoritativeTownGeometryRequest request)
    {
        return request.Entry switch {
            AuthoritativeTownEntry.Main => (75, 68),
            AuthoritativeTownEntry.Previous => (25, 31),
            AuthoritativeTownEntry.TownWarpUp => request.TownWarpFrom switch {
                5 => (49, 22),
                9 => (18, 69),
                13 => (41, 81),
                21 => (36, 25),
                17 => (79, 62),
                _ => (request.ExistingViewPositionX, request.ExistingViewPositionY),
            },
            _ => (request.ExistingViewPositionX, request.ExistingViewPositionY),
        };
    }

    private static void SetMegaTile(ushort[] megaTiles, int x, int y, ushort value)
        => megaTiles[y * AuthoritativeTownGeometry.MegaWidth + x] = value;

    private static void FillTile(
        ushort[] finePieces,
        AuthoritativeDunPieceCatalog pieceCatalog,
        int x,
        int y,
        int oneBasedMegaTileId)
    {
        if (oneBasedMegaTileId <= 0
            || !pieceCatalog.TryGet((uint)(oneBasedMegaTileId - 1), out var definition))
            throw new InvalidDataException(
                $"Native Town references unknown one-based mega-tile {oneBasedMegaTileId}.");
        SetMegaPieces(
            finePieces,
            x,
            y,
            definition.NorthWestPieceId,
            definition.NorthEastPieceId,
            definition.SouthWestPieceId,
            definition.SouthEastPieceId);
    }

    private static void SetMegaPieces(
        ushort[] finePieces,
        int x,
        int y,
        uint northWest,
        uint northEast,
        uint southWest,
        uint southEast)
    {
        EnsureFineCell(x, y);
        EnsureFineCell(x + 1, y);
        EnsureFineCell(x, y + 1);
        EnsureFineCell(x + 1, y + 1);
        finePieces[y * AuthoritativeTownGeometry.FineWidth + x] = ToPieceId(northWest);
        finePieces[y * AuthoritativeTownGeometry.FineWidth + x + 1] = ToPieceId(northEast);
        finePieces[(y + 1) * AuthoritativeTownGeometry.FineWidth + x] = ToPieceId(southWest);
        finePieces[(y + 1) * AuthoritativeTownGeometry.FineWidth + x + 1] = ToPieceId(southEast);
    }

    private static ushort ToPieceId(uint value)
    {
        if (value >= AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            throw new InvalidDataException($"Native Town d-piece {value} is outside MAXTILES.");
        return checked((ushort)value);
    }

    private static void EnsureFineCell(int x, int y)
    {
        if (x < 0 || y < 0 || x >= AuthoritativeTownGeometry.FineWidth
            || y >= AuthoritativeTownGeometry.FineHeight)
            throw new InvalidDataException("Native Town tile write exceeds the fine grid.");
    }

    private static void ApplyHive(
        ushort[] finePieces,
        ushort[] megaTiles,
        bool open)
    {
        if (open) {
            SetMegaTile(megaTiles, 36, 27, 47);
            SetTownPieces(finePieces, [
                (78, 60, 0x489), (79, 60, 0x48A), (78, 61, 0x48B), (79, 61, 0x50D),
                (78, 62, 0x4ED), (78, 63, 0x4EF), (79, 62, 0x50F), (79, 63, 0x510),
                (79, 64, 0x511), (78, 64, 0x119), (78, 65, 0x11B), (79, 65, 0x11C),
                (80, 60, 0x512), (80, 61, 0x514), (81, 61, 0x515), (82, 60, 0x516),
                (83, 60, 0x517), (82, 61, 0x518), (83, 61, 0x519), (80, 62, 0x51A),
                (81, 62, 0x51B), (80, 63, 0x51C), (81, 63, 0x51D), (80, 64, 0x51E),
                (81, 64, 0x51F), (80, 65, 0x520), (81, 65, 0x521), (82, 62, 0x522),
                (83, 62, 0x523), (82, 63, 0x524), (83, 63, 0x525), (82, 64, 0x526),
                (83, 64, 0x527), (82, 65, 0x528), (83, 65, 0x529), (84, 61, 279),
                (84, 62, 280), (84, 63, 279), (84, 64, 10), (85, 60, 11),
                (85, 61, 12), (85, 62, 13), (85, 63, 14), (85, 64, 15),
                (86, 60, 16), (86, 61, 17),
            ]);
            return;
        }

        SetMegaTile(megaTiles, 35, 27, 18);
        SetMegaTile(megaTiles, 36, 27, 63);
        SetTownPieces(finePieces, [
            (78, 60, 0x489), (79, 60, 0x4EA), (78, 61, 0x4EB), (79, 61, 0x4EC),
            (78, 62, 0x4ED), (79, 62, 0x4EE), (78, 63, 0x4EF), (79, 63, 0x4F0),
            (78, 64, 0x4F1), (79, 64, 0x4F2), (78, 65, 0x4F3), (80, 60, 0x4F4),
            (81, 60, 0x4F5), (80, 61, 0x4F6), (81, 61, 0x4F7), (82, 60, 0x4F8),
            (83, 60, 0x4F9), (82, 61, 0x4FA), (83, 61, 0x4FB), (80, 62, 0x4FC),
            (81, 62, 0x4FD), (80, 63, 0x4FE), (81, 63, 0x4FF), (80, 64, 0x500),
            (81, 64, 0x501), (80, 65, 0x502), (81, 65, 0x503), (82, 62, 0x504),
            (83, 62, 0x505), (82, 63, 0x506), (83, 63, 0x507), (82, 64, 0x508),
            (83, 64, 0x509), (82, 65, 0x50A), (83, 65, 0x50B), (84, 61, 279),
            (84, 62, 280), (84, 63, 279), (84, 64, 10), (85, 60, 11),
            (85, 61, 12), (85, 62, 13), (85, 63, 14), (85, 64, 15),
            (86, 60, 16), (86, 61, 17),
        ]);
    }

    private static void ApplyGrave(ushort[] finePieces, ushort[] megaTiles, bool open)
    {
        if (open) {
            SetMegaTile(megaTiles, 14, 8, 47);
            SetMegaTile(megaTiles, 14, 7, 47);
            SetTownPieces(finePieces, [
                (36, 21, 0x532), (37, 21, 0x533), (36, 22, 0x534), (37, 22, 0x535),
                (36, 23, 0x536), (37, 23, 0x537), (36, 24, 0x538), (37, 24, 0x539),
                (35, 21, 0x53A), (34, 21, 0x53B),
            ]);
            return;
        }

        SetTownPieces(finePieces, [
            (36, 21, 0x52A), (37, 21, 0x52B), (36, 22, 0x52C), (37, 22, 0x52D),
            (36, 23, 0x52E), (37, 23, 0x52F), (36, 24, 0x530), (37, 24, 0x531),
            (35, 21, 0x53A), (34, 21, 0x53B),
        ]);
    }

    private static void SetTownPieces(
        ushort[] finePieces,
        ReadOnlySpan<(int X, int Y, int PieceId)> writes)
    {
        foreach (var (x, y, pieceId) in writes) {
            EnsureFineCell(x, y);
            if (pieceId < 0 || pieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount)
                throw new InvalidDataException($"Native Town d-piece {pieceId} is outside MAXTILES.");
            finePieces[y * AuthoritativeTownGeometry.FineWidth + x] = checked((ushort)pieceId);
        }
    }

    private static void ApplyTownSpecialMappings(ushort[] finePieces, ushort[] specialTiles)
    {
        for (var cell = 0; cell < finePieces.Length; cell++) {
            var pieceId = finePieces[cell];
            foreach (var (mappedPiece, specialId) in TownSpecialMappings) {
                if (pieceId == mappedPiece) {
                    specialTiles[cell] = specialId;
                    break;
                }
            }
        }
    }
}
