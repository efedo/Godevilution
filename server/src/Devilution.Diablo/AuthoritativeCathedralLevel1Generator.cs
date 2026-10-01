using Devilution.Engine;

namespace Devilution.Diablo;

/** Entry direction used by the ordinary Cathedral level-1 generator. */
public enum AuthoritativeCathedralEntry
{
    Main,
    Previous,
}

/** Native integer coordinates; stair anchors are coarse and entry is world-tile space. */
public readonly record struct AuthoritativeCathedralPoint(int X, int Y);

/**
 * Immutable result of the ordinary original-Diablo Cathedral generator.
 *
 * This is deliberately the coarse-only stage: mega-tile IDs and native
 * 80x80 interior transparency values are authoritative, while .til/.sol
 * expansion remains an external content concern.
 */
public sealed record AuthoritativeCathedralLevel1Result(
    uint InputSeed,
    uint AttemptSeed,
    uint FinalRandomState,
    IReadOnlyList<ushort> MegaTiles,
    IReadOnlyList<ushort> NativeTransparency,
    AuthoritativeCathedralPoint StairsUp,
    AuthoritativeCathedralPoint StairsDown,
    AuthoritativeCathedralPoint EntryPosition)
{
    public const int MegaWidth = 40;
    public const int MegaHeight = 40;
    public const int TransparencyWidth = 80;
    public const int TransparencyHeight = 80;

    public ushort GetMegaTile(int x, int y)
    {
        if ((uint)x >= MegaWidth || (uint)y >= MegaHeight)
            throw new ArgumentOutOfRangeException();
        return MegaTiles[y * MegaWidth + x];
    }

    public ushort GetTransparency(int x, int y)
    {
        if ((uint)x >= TransparencyWidth || (uint)y >= TransparencyHeight)
            throw new ArgumentOutOfRangeException();
        return NativeTransparency[y * TransparencyWidth + x];
    }
}

/**
 * Native-compatible ordinary Cathedral (level 1) coarse generator.
 *
 * The implementation follows level_l1.cpp's ordinary level-1 pipeline and
 * uses the shared legacy Borland LCG. It does not read MPQs, native process
 * state, presentation assets, quest data, or campaign world seeds.
 * Generation retries retain the native behavior; there is no retry limit.
 */
public static class AuthoritativeCathedralLevel1Generator
{
    private const int Width = AuthoritativeCathedralLevel1Result.MegaWidth;
    private const int Height = AuthoritativeCathedralLevel1Result.MegaHeight;
    private const int WorldOffset = 16;
    private const int WorldWidth = 112;
    private const byte VWall = 1;
    private const byte HWall = 2;
    private const byte Corner = 3;
    private const byte DWall = 4;
    private const byte DArch = 5;
    private const byte VWallEnd = 6;
    private const byte HWallEnd = 7;
    private const byte HArchEnd = 8;
    private const byte VArchEnd = 9;
    private const byte HArchVWall = 10;
    private const byte VArch = 11;
    private const byte HArch = 12;
    private const byte Floor = 13;
    private const byte HWallVArch = 14;
    private const byte Pillar = 15;
    private const byte VCorner = 16;
    private const byte HCorner = 17;
    private const byte DirtHwall = 18;
    private const byte DirtVwall = 19;
    private const byte VDirtCorner = 20;
    private const byte HDirtCorner = 21;
    private const byte Dirt = 22;
    private const byte DirtHwallEnd = 23;
    private const byte DirtVwallEnd = 24;
    private const byte VDoor = 25;
    private const byte HDoor = 26;
    private const byte HFenceVWall = 27;
    private const byte HDoorVDoor = 28;
    private const byte DFence = 29;
    private const byte VDoorEnd = 30;
    private const byte HDoorEnd = 31;
    private const byte VFenceEnd = 32;
    private const byte VArchEnd2 = 33;
    private const byte HArchVWall2 = 34;
    private const byte VFence = 35;
    private const byte HFence = 36;
    private const byte HWallVFence = 37;
    private const byte HArchVFence = 38;
    private const byte HArchVDoor = 39;
    private const byte HArchVWall3 = 40;
    private const byte DWall2 = 41;
    private const byte HWallVArch2 = 42;
    private const byte DWall3 = 43;
    private const byte EntranceStairs = 64;
    private const byte VWall2 = 79;
    private const byte HWall2 = 80;
    private const byte DWall4 = 82;
    private const byte VWallEnd2 = 84;
    private const byte VWall4 = 89;
    private const byte VWall5 = 90;
    private const byte HWall4 = 91;
    private const byte HWall5 = 92;
    private const byte VWall8 = 100;
    private const byte Floor12 = 139;
    private const byte Floor13 = 140;
    private const byte Floor14 = 141;
    private const byte Floor15 = 142;
    private const byte Floor16 = 143;
    private const byte Floor17 = 144;
    private const byte Floor18 = 145;
    private const byte VWall17 = 146;
    private const byte VArch5 = 147;
    private const byte HWallShadow = 148;
    private const byte HArchShadow = 149;
    private const byte Floor19 = 150;
    private const byte Floor20 = 151;
    private const byte Floor21 = 152;
    private const byte HArchShadow2 = 153;
    private const byte HWallShadow2 = 154;
    private const byte Floor22 = 162;
    private const byte Floor23 = 163;
    private const byte DirtHWall2 = 199;
    private const byte DirtVWall2 = 200;
    private const byte DirtCorner2 = 202;
    private const byte DirtHWallEnd2 = 204;
    private const byte DirtVWallEnd2 = 205;

    private readonly record struct Shadow(byte Strig, byte S1, byte S2, byte S3, byte Nv1, byte Nv2, byte Nv3);

    private static readonly Shadow[] ShadowPatterns =
    [
        new(HWallEnd, Floor, 0, Floor, Floor17, 0, Floor15),
        new(VCorner, Floor, 0, Floor, Floor17, 0, Floor15),
        new(Pillar, Floor, 0, Floor, Floor18, 0, Floor15),
        new(DArch, Floor, Floor, Floor, Floor21, Floor13, Floor12),
        new(DArch, Floor, VWall, Floor, Floor16, VWall17, Floor12),
        new(DArch, Floor, Floor, HWall, Floor16, Floor13, HWallShadow),
        new(DArch, 0, VWall, HWall, 0, VWall17, HWallShadow),
        new(DArch, Floor, VArch, Floor, Floor16, VArch5, Floor12),
        new(DArch, Floor, Floor, HArch, Floor16, Floor13, HArchShadow),
        new(DArch, Floor, VArch, HArch, Floor19, VArch5, HArchShadow),
        new(DArch, Floor, VWall, HArch, Floor16, VWall17, HArchShadow),
        new(DArch, Floor, VArch, HWall, Floor16, VArch5, HWallShadow),
        new(VArchEnd, Floor, Floor, Floor, Floor17, Floor13, Floor15),
        new(VArchEnd, Floor, VWall, Floor, Floor17, VWall17, Floor15),
        new(VArchEnd, Floor, VArch, Floor, Floor20, VArch5, Floor15),
        new(HArchEnd, Floor, 0, Floor, Floor17, 0, Floor12),
        new(HArchEnd, Floor, 0, HArch, Floor16, 0, HArchShadow),
        new(HArchEnd, 0, 0, HWall, 0, 0, HWallShadow),
        new(VArch, 0, 0, Floor, 0, 0, Floor12),
        new(VArch, Floor, 0, Floor, Floor12, 0, Floor12),
        new(VArch, HWall, 0, Floor, HWallShadow, 0, Floor12),
        new(VArch, HArch, 0, Floor, HArchShadow, 0, Floor12),
        new(VArch, Floor, VArch, HArch, Floor12, 0, HArchShadow),
        new(HWallVArch, 0, 0, Floor, 0, 0, Floor12),
        new(HWallVArch, Floor, 0, Floor, Floor12, 0, Floor12),
        new(HWallVArch, HWall, 0, Floor, HWallShadow, 0, Floor12),
        new(HWallVArch, HArch, 0, Floor, HArchShadow, 0, Floor12),
        new(HWallVArch, Floor, VArch, HArch, Floor12, 0, HArchShadow),
        new(HArchVWall, 0, Floor, 0, 0, Floor13, 0),
        new(HArchVWall, Floor, Floor, 0, Floor13, Floor13, 0),
        new(HArchVWall, 0, VWall, 0, 0, VWall17, 0),
        new(HArchVWall, Floor, VArch, 0, Floor13, VArch5, 0),
        new(HArch, 0, Floor, 0, 0, Floor13, 0),
        new(HArch, Floor, Floor, 0, Floor13, Floor13, 0),
        new(HArch, 0, VWall, 0, 0, VWall17, 0),
        new(HArch, Floor, VArch, 0, Floor13, VArch5, 0),
        new(Corner, Floor, VArch, HArch, Floor19, 0, 0),
    ];

    // These are the complete native level_l1.cpp lookup tables, kept as
    // explicit managed data so coarse generation never depends on archives.
    private static readonly byte[] BaseTypes =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        16, 17, 0, 0, 0, 0, 0, 0, 0, 1, 2, 10, 4, 5, 6, 7,
        8, 9, 10, 11, 12, 14, 5, 14, 10, 4, 14, 4, 5, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        2, 3, 4, 1, 6, 7, 16, 17, 2, 1, 1, 2, 2, 1, 1, 2,
        2, 2, 2, 2, 1, 1, 11, 1, 13, 13, 13, 1, 2, 1, 2, 1,
        2, 1, 2, 2, 2, 2, 12, 0, 0, 11, 1, 11, 1, 13, 0, 0,
        0, 0, 0, 0, 0, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 1, 11, 2, 12, 13, 13, 13, 12, 2, 1, 2, 2, 4, 14,
        4, 10, 13, 13, 4, 4, 1, 1, 4, 2, 2, 13, 13, 13, 13, 25,
        26, 28, 30, 31, 41, 43, 40, 41, 42, 43, 25, 41, 43, 28, 28, 1,
        2, 25, 26, 22, 22, 25, 26, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly byte[] TileDecorations =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        16, 17, 0, 0, 0, 0, 0, 0, 0, 25, 26, 0, 28, 0, 30, 31,
        0, 0, 0, 0, 0, 0, 0, 0, 40, 41, 42, 43, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 79,
        80, 0, 82, 0, 0, 0, 0, 0, 0, 79, 0, 80, 0, 0, 79, 80,
        0, 2, 2, 2, 1, 1, 11, 25, 13, 13, 13, 1, 2, 1, 2, 1,
        2, 1, 2, 2, 2, 2, 12, 0, 0, 11, 1, 11, 1, 13, 0, 0,
        0, 0, 0, 0, 0, 13, 13, 13, 13, 13, 13, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private readonly record struct Rect(int X, int Y, int Width, int Height);

    public static AuthoritativeCathedralLevel1Result Generate(uint dungeonSeed, AuthoritativeCathedralEntry entry)
    {
        var generated = GenerateDepth(
            depth: 1,
            dungeonSeed: dungeonSeed,
            entry: entry,
            policy: AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs);
        return new AuthoritativeCathedralLevel1Result(
            generated.InputSeed,
            generated.AttemptSeed,
            generated.FinalRandomState,
            generated.MegaTiles,
            generated.NativeTransparency,
            generated.StairsUp,
            generated.StairsDown,
            generated.EntryPosition);
    }

    internal static AuthoritativeCathedralDepthResult GenerateDepth(
        int depth,
        uint dungeonSeed,
        AuthoritativeCathedralEntry entry,
        AuthoritativeCathedralDepthGenerationPolicy policy)
    {
        _ = AuthoritativeCathedralDepthGenerator.MinimumAreaForDepth(depth);
        if (entry is not AuthoritativeCathedralEntry.Main and not AuthoritativeCathedralEntry.Previous)
            throw new ArgumentOutOfRangeException(nameof(entry));
        if (policy != AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs)
            throw new ArgumentOutOfRangeException(nameof(policy));

        var random = new AuthoritativeLegacyRandom(dungeonSeed);
        var map = new byte[Width * Height];
        var dungeonMask = new bool[Width * Height];
        var protectedTiles = new bool[Width * Height];
        var chamber = new bool[Width * Height];
        var transparency = new byte[WorldWidth * WorldWidth];
        var verticalLayout = false;
        var hasChamber1 = false;
        var hasChamber2 = false;
        var hasChamber3 = false;
        var attemptSeed = random.State;
        AuthoritativeCathedralPoint stairsUp = default;
        AuthoritativeCathedralPoint stairsDown = default;

        while (true) {
            Array.Clear(transparency);
            while (true) {
                attemptSeed = random.State;
                FirstRoom();
                if (CountMask() >= AuthoritativeCathedralDepthGenerator.MinimumAreaForDepth(depth))
                    break;
            }

            Array.Fill(map, Dirt);
            Array.Clear(protectedTiles);
            Array.Clear(chamber);
            MakeDmt();
            FillChambers();
            FixTilesPatterns();
            AddWall();
            FloodTransparencyValues();

            if (PlaceStairs(out stairsUp, out stairsDown))
                break;
        }

        CopyEntranceTransparency();
        FixTransparency();
        FixDirtTiles();
        FixCornerTiles();
        Substitution();
        ApplyShadowsPatterns();
        var lampCount = random.GenerateRnd(5) + 5;
        for (var index = 0; index < lampCount; index++)
            PlaceMiniSet(new MiniSet(2, 2, new byte[,] { { Floor, 0 }, { Floor, Floor } }, new byte[,] { { 129, 0 }, { 130, 128 } }), 1600, true, out _);
        FillFloor();

        var entryPosition = entry == AuthoritativeCathedralEntry.Main
            ? new AuthoritativeCathedralPoint(stairsUp.X * 2 + 19, stairsUp.Y * 2 + 20)
            : new AuthoritativeCathedralPoint(stairsDown.X * 2 + 19, stairsDown.Y * 2 + 19);
        return new AuthoritativeCathedralDepthResult(
            depth,
            policy,
            dungeonSeed,
            attemptSeed,
            random.State,
            Array.AsReadOnly(map.Select(value => (ushort)value).ToArray()),
            Array.AsReadOnly(ExtractInteriorTransparency()),
            stairsUp,
            stairsDown,
            entryPosition);

        void FirstRoom()
        {
            Array.Clear(dungeonMask);
            Array.Clear(chamber);
            verticalLayout = random.FlipCoin(2);
            hasChamber1 = !random.FlipCoin(2);
            hasChamber2 = !random.FlipCoin(2);
            hasChamber3 = !random.FlipCoin(2);
            if (!hasChamber1 || !hasChamber3)
                hasChamber2 = true;

            var chamber1 = new Rect(1, 15, 10, 10);
            var chamber2 = new Rect(15, 15, 10, 10);
            var chamber3 = new Rect(29, 15, 10, 10);
            var hallway = new Rect(1, 17, 38, 6);
            if (!hasChamber1)
                hallway = hallway with { X = hallway.X + 17, Width = hallway.Width - 17 };
            if (!hasChamber3)
                hallway = hallway with { Width = hallway.Width - 16 };
            if (verticalLayout) {
                chamber1 = SwapRect(chamber1);
                chamber3 = SwapRect(chamber3);
                hallway = SwapRect(hallway);
            }
            if (hasChamber1) MapRoom(chamber1);
            if (hasChamber2) MapRoom(chamber2);
            if (hasChamber3) MapRoom(chamber3);
            MapRoom(hallway);
            if (hasChamber1) GenerateRoom(chamber1, verticalLayout);
            if (hasChamber2) GenerateRoom(chamber2, verticalLayout);
            if (hasChamber3) GenerateRoom(chamber3, verticalLayout);
        }

        void GenerateRoom(Rect area, bool vertical)
        {
            var rotate = random.FlipCoin(4);
            vertical = (!vertical && rotate) || (vertical && !rotate);
            var room1 = new Rect(0, 0, 0, 0);
            var placeRoom1 = false;
            for (var num = 0; num < 20; num++) {
                var width = (random.GenerateRnd(5) + 2) & ~1;
                var height = (random.GenerateRnd(5) + 2) & ~1;
                room1 = new Rect(area.X, area.Y, width, height);
                if (vertical) {
                    room1 = room1 with { X = room1.X - room1.Width, Y = room1.Y + area.Height / 2 - room1.Height / 2 };
                    placeRoom1 = CheckRoom(new Rect(room1.X - 1, room1.Y - 1, room1.Height + 2, room1.Width + 1));
                } else {
                    room1 = room1 with { X = room1.X + area.Width / 2 - room1.Width / 2, Y = room1.Y - room1.Height };
                    placeRoom1 = CheckRoom(new Rect(room1.X - 1, room1.Y - 1, room1.Width + 2, room1.Height + 1));
                }
                if (placeRoom1) break;
            }
            if (placeRoom1)
                MapRoom(new Rect(room1.X, room1.Y, Math.Min(Width - room1.X, room1.Width), Math.Min(Width - room1.Y, room1.Height)));

            var room2 = room1;
            var placeRoom2 = false;
            if (vertical) {
                room2 = room2 with { X = area.X + area.Width };
                placeRoom2 = CheckRoom(new Rect(room2.X, room2.Y - 1, room2.Width + 1, room2.Height + 2));
            } else {
                room2 = room2 with { Y = area.Y + area.Height };
                placeRoom2 = CheckRoom(new Rect(room2.X - 1, room2.Y, room2.Width + 2, room2.Height + 1));
            }
            if (placeRoom2) MapRoom(room2);
            if (placeRoom1) GenerateRoom(room1, !vertical);
            if (placeRoom2) GenerateRoom(room2, !vertical);
        }

        void MapRoom(Rect room)
        {
            for (var y = 0; y < room.Height; y++)
                for (var x = 0; x < room.Width; x++)
                    SetMask(room.X + x, room.Y + y, true);
        }

        bool CheckRoom(Rect room)
        {
            for (var y = 0; y < room.Height; y++)
                for (var x = 0; x < room.Width; x++) {
                    var px = room.X + x;
                    var py = room.Y + y;
                    if ((uint)px >= Width || (uint)py >= Height || GetMask(px, py))
                        return false;
                }
            return true;
        }

        void MakeDmt()
        {
            for (var y = 0; y < Height - 1; y++)
                for (var x = 0; x < Width - 1; x++) {
                    if (GetMask(x, y)) map[At(x, y)] = Floor;
                    else if (!GetMask(x + 1, y + 1) && GetMask(x, y + 1) && GetMask(x + 1, y)) map[At(x, y)] = Floor;
                    else if (GetMask(x + 1, y + 1) && GetMask(x, y + 1) && GetMask(x + 1, y)) map[At(x, y)] = VCorner;
                    else if (GetMask(x, y + 1)) map[At(x, y)] = HWall;
                    else if (GetMask(x + 1, y)) map[At(x, y)] = VWall;
                    else if (GetMask(x + 1, y + 1)) map[At(x, y)] = DWall;
                    else map[At(x, y)] = Dirt;
                }
        }

        void FillChambers()
        {
            var chamber1 = new AuthoritativeCathedralPoint(0, 14);
            var chamber3 = new AuthoritativeCathedralPoint(28, 14);
            var hall1 = new AuthoritativeCathedralPoint(12, 18);
            var hall2 = new AuthoritativeCathedralPoint(26, 18);
            if (verticalLayout) {
                chamber1 = Swap(chamber1);
                chamber3 = Swap(chamber3);
                hall1 = Swap(hall1);
                hall2 = Swap(hall2);
            }
            if (hasChamber1) GenerateChamber(chamber1, false, true);
            if (hasChamber2) GenerateChamber(new AuthoritativeCathedralPoint(14, 14), hasChamber1, hasChamber3);
            if (hasChamber3) GenerateChamber(chamber3, true, false);
            if (hasChamber2) {
                if (hasChamber1) GenerateHall(hall1, 2);
                if (hasChamber3) GenerateHall(hall2, 2);
            } else GenerateHall(hall1, 16);
        }

        void GenerateChamber(AuthoritativeCathedralPoint position, bool connectPrevious, bool connectNext)
        {
            if (connectPrevious) {
                if (verticalLayout) {
                    Set(position.X + 2, position.Y, HArch); Set(position.X + 3, position.Y, HArch); Set(position.X + 4, position.Y, Corner);
                    Set(position.X + 7, position.Y, VArchEnd); Set(position.X + 8, position.Y, HArch); Set(position.X + 9, position.Y, HWall);
                } else {
                    Set(position.X, position.Y + 2, VArch); Set(position.X, position.Y + 3, VArch); Set(position.X, position.Y + 4, Corner);
                    Set(position.X, position.Y + 7, HArchEnd); Set(position.X, position.Y + 8, VArch); Set(position.X, position.Y + 9, VWall);
                }
            }
            if (connectNext) {
                if (verticalLayout) {
                    position = position with { Y = position.Y + 11 };
                    Set(position.X + 2, position.Y, HArchVWall); Set(position.X + 3, position.Y, HArch); Set(position.X + 4, position.Y, HArchEnd);
                    Set(position.X + 7, position.Y, DArch); Set(position.X + 8, position.Y, HArch);
                    if (Get(position.X + 9, position.Y) != DWall) Set(position.X + 9, position.Y, HDirtCorner);
                    position = position with { Y = position.Y - 11 };
                } else {
                    position = position with { X = position.X + 11 };
                    Set(position.X, position.Y + 2, HWallVArch); Set(position.X, position.Y + 3, VArch); Set(position.X, position.Y + 4, VArchEnd);
                    Set(position.X, position.Y + 7, DArch); Set(position.X, position.Y + 8, VArch);
                    if (Get(position.X, position.Y + 9) != DWall) Set(position.X, position.Y + 9, HDirtCorner);
                    position = position with { X = position.X - 11 };
                }
            }
            for (var y = 1; y < 11; y++)
                for (var x = 1; x < 11; x++) { Set(position.X + x, position.Y + y, Floor); chamber[At(position.X + x, position.Y + y)] = true; }
            Set(position.X + 4, position.Y + 4, Pillar); Set(position.X + 7, position.Y + 4, Pillar);
            Set(position.X + 4, position.Y + 7, Pillar); Set(position.X + 7, position.Y + 7, Pillar);
        }

        void GenerateHall(AuthoritativeCathedralPoint start, int length)
        {
            if (verticalLayout) for (var i = start.Y; i < start.Y + length; i++) { Set(start.X, i, VArch); Set(start.X + 3, i, VArch); }
            else for (var i = start.X; i < start.X + length; i++) { Set(i, start.Y, HArch); Set(i, start.Y + 3, HArch); }
        }

        void FixTilesPatterns()
        {
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) {
                if (x + 1 < Width) {
                    if (Get(x, y) == HWall && Get(x + 1, y) == Dirt) Set(x + 1, y, DirtHwallEnd);
                    if (Get(x, y) == Floor && Get(x + 1, y) == Dirt) Set(x + 1, y, DirtHwall);
                    if (Get(x, y) == Floor && Get(x + 1, y) == HWall) Set(x + 1, y, HWallEnd);
                    if (Get(x, y) == VWallEnd && Get(x + 1, y) == Dirt) Set(x + 1, y, DirtVwallEnd);
                }
                if (y + 1 < Height) {
                    if (Get(x, y) == VWall && Get(x, y + 1) == Dirt) Set(x, y + 1, DirtVwallEnd);
                    if (Get(x, y) == Floor && Get(x, y + 1) == VWall) Set(x, y + 1, VWallEnd);
                    if (Get(x, y) == Floor && Get(x, y + 1) == Dirt) Set(x, y + 1, DirtVwall);
                }
            }
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) {
                if (x + 1 < Width) {
                    if (Get(x, y) == Floor && Get(x + 1, y) == DirtVwall) Set(x + 1, y, HDirtCorner);
                    if (Get(x, y) == Floor && Get(x + 1, y) == Dirt) Set(x + 1, y, VDirtCorner);
                    if (Get(x, y) == HWallEnd && Get(x + 1, y) == Dirt) Set(x + 1, y, DirtHwallEnd);
                    if (Get(x, y) == Floor && Get(x + 1, y) == DirtVwallEnd) Set(x + 1, y, HDirtCorner);
                    if (Get(x, y) == DirtVwall && Get(x + 1, y) == Dirt) Set(x + 1, y, VDirtCorner);
                    if (Get(x, y) == HWall && Get(x + 1, y) == DirtVwall) Set(x + 1, y, HDirtCorner);
                    if (Get(x, y) == DirtVwall && Get(x + 1, y) == VWall) Set(x + 1, y, VWallEnd);
                    if (Get(x, y) == HWallEnd && Get(x + 1, y) == DirtVwall) Set(x + 1, y, HDirtCorner);
                    if (Get(x, y) == HWall && Get(x + 1, y) == VWall) Set(x + 1, y, VWallEnd);
                    if (Get(x, y) == Corner && Get(x + 1, y) == Dirt) Set(x + 1, y, DirtVwallEnd);
                    if (Get(x, y) == HDirtCorner && Get(x + 1, y) == VWall) Set(x + 1, y, VWallEnd);
                    if (Get(x, y) == HWallEnd && Get(x + 1, y) == VWall) Set(x + 1, y, VWallEnd);
                    if (Get(x, y) == HWallEnd && Get(x + 1, y) == DirtVwallEnd) Set(x + 1, y, HDirtCorner);
                    if (Get(x, y) == DWall && Get(x + 1, y) == VCorner) Set(x + 1, y, HCorner);
                    if (Get(x, y) == HWallEnd && Get(x + 1, y) == Floor) Set(x + 1, y, HCorner);
                    if (Get(x, y) == HWall && Get(x + 1, y) == DirtVwallEnd) Set(x + 1, y, HDirtCorner);
                    if (Get(x, y) == HWall && Get(x + 1, y) == Floor) Set(x + 1, y, HCorner);
                }
                if (x > 0) {
                    if (Get(x, y) == DirtHwallEnd && Get(x - 1, y) == Dirt) Set(x - 1, y, DirtVwall);
                    if (Get(x, y) == DirtVwall && Get(x - 1, y) == DirtHwallEnd) Set(x - 1, y, HDirtCorner);
                    if (Get(x, y) == VWallEnd && Get(x - 1, y) == Dirt) Set(x - 1, y, DirtVwallEnd);
                    if (Get(x, y) == VWallEnd && Get(x - 1, y) == DirtHwallEnd) Set(x - 1, y, HDirtCorner);
                }
                if (y + 1 < Height) {
                    if (Get(x, y) == VWall && Get(x, y + 1) == HWall) Set(x, y + 1, HWallEnd);
                    if (Get(x, y) == VWallEnd && Get(x, y + 1) == DirtHwall) Set(x, y + 1, HDirtCorner);
                    if (Get(x, y) == DirtHwall && Get(x, y + 1) == HWall) Set(x, y + 1, HWallEnd);
                    if (Get(x, y) == VWallEnd && Get(x, y + 1) == HWall) Set(x, y + 1, HWallEnd);
                    if (Get(x, y) == HDirtCorner && Get(x, y + 1) == HWall) Set(x, y + 1, HWallEnd);
                    if (Get(x, y) == VWallEnd && Get(x, y + 1) == Dirt) Set(x, y + 1, DirtVwallEnd);
                    if (Get(x, y) == VWallEnd && Get(x, y + 1) == Floor) Set(x, y + 1, VCorner);
                    if (Get(x, y) == VWall && Get(x, y + 1) == Floor) Set(x, y + 1, VCorner);
                    if (Get(x, y) == Floor && Get(x, y + 1) == VCorner) Set(x, y + 1, HCorner);
                }
                if (y > 0) {
                    if (Get(x, y) == VWallEnd && Get(x, y - 1) == Dirt) Set(x, y - 1, HWallEnd);
                    if (Get(x, y) == VWallEnd && Get(x, y - 1) == Dirt) Set(x, y - 1, DirtVwallEnd);
                    if (Get(x, y) == HWallEnd && Get(x, y - 1) == DirtVwallEnd) Set(x, y - 1, HDirtCorner);
                    if (Get(x, y) == DirtHwall && Get(x, y - 1) == DirtVwallEnd) Set(x, y - 1, HDirtCorner);
                }
            }
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) {
                if (y + 1 < Height && Get(x, y) == DWall && Get(x, y + 1) == HWall) Set(x, y + 1, HWallEnd);
                if (x + 1 < Width && Get(x, y) == HWall && Get(x + 1, y) == DirtVwall) Set(x + 1, y, HDirtCorner);
                if (y + 1 < Height && Get(x, y) == DirtHwall && Get(x, y + 1) == Dirt) Set(x, y + 1, VDirtCorner);
            }
        }

        void AddWall()
        {
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) {
                if (protectedTiles[At(x, y)] || chamber[At(x, y)]) continue;
                if (Get(x, y) == Corner) { random.Discard(1); var max = HorizontalWallOk(x, y); if (max != -1) HorizontalWall(x, y, HWall, max); }
                if (Get(x, y) == Corner) { random.Discard(1); var max = VerticalWallOk(x, y); if (max != -1) VerticalWall(x, y, VWall, max); }
                if (Get(x, y) == VWallEnd) { random.Discard(1); var max = HorizontalWallOk(x, y); if (max != -1) HorizontalWall(x, y, DWall, max); }
                if (Get(x, y) == HWallEnd) { random.Discard(1); var max = VerticalWallOk(x, y); if (max != -1) VerticalWall(x, y, DWall, max); }
                if (Get(x, y) == HWall) { random.Discard(1); var max = HorizontalWallOk(x, y); if (max != -1) HorizontalWall(x, y, HWall, max); }
                if (Get(x, y) == VWall) { random.Discard(1); var max = VerticalWallOk(x, y); if (max != -1) VerticalWall(x, y, VWall, max); }
            }
        }

        int HorizontalWallOk(int x, int y)
        {
            var length = 1;
            while (Get(x + length, y) == Floor) {
                if (Get(x + length, y - 1) != Floor || Get(x + length, y + 1) != Floor || protectedTiles[At(x + length, y)] || chamber[At(x + length, y)]) break;
                length++;
            }
            if (length == 1) return -1;
            return IsWallTerminator(Get(x + length, y)) ? length : -1;
        }

        int VerticalWallOk(int x, int y)
        {
            var length = 1;
            while (Get(x, y + length) == Floor) {
                if (Get(x - 1, y + length) != Floor || Get(x + 1, y + length) != Floor || protectedTiles[At(x, y + length)] || chamber[At(x, y + length)]) break;
                length++;
            }
            if (length == 1) return -1;
            return IsWallTerminator(Get(x, y + length)) ? length : -1;
        }

        void HorizontalWall(int x, int y, byte start, int maxX)
        {
            var wall = HWall; var door = HDoor;
            switch (random.GenerateRnd(4)) {
            case 2: wall = HArch; door = HArch; if (start == HWall) start = HArch; else if (start == DWall) start = HArchVWall; break;
            case 3: wall = HFence; if (start == HWall) start = HFence; else if (start == DWall) start = HFenceVWall; break;
            }
            if (random.GenerateRnd(6) == 5) door = HArch;
            Set(x, y, start);
            for (var i = 1; i < maxX; i++) Set(x + i, y, wall);
            var doorX = random.GenerateRnd(maxX - 1) + 1;
            Set(x + doorX, y, door);
            if (door == HDoor) protectedTiles[At(x + doorX, y)] = true;
        }

        void VerticalWall(int x, int y, byte start, int maxY)
        {
            var wall = VWall; var door = VDoor;
            switch (random.GenerateRnd(4)) {
            case 2: wall = VArch; door = VArch; if (start == VWall) start = VArch; else if (start == DWall) start = HWallVArch; break;
            case 3: wall = VFence; if (start == VWall) start = VFence; else if (start == DWall) start = HWallVFence; break;
            }
            if (random.GenerateRnd(6) == 5) door = VArch;
            Set(x, y, start);
            for (var i = 1; i < maxY; i++) Set(x, y + i, wall);
            var doorY = random.GenerateRnd(maxY - 1) + 1;
            Set(x, y + doorY, door);
            if (door == VDoor) protectedTiles[At(x, y + doorY)] = true;
        }

        void Substitution()
        {
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) {
                if (!random.FlipCoin(4)) continue;
                var c = TileDecorations[Get(x, y)];
                if (c == 0 || protectedTiles[At(x, y)]) continue;
                var rv = random.GenerateRnd(16);
                var i = -1;
                while (rv >= 0) { i++; if (i == TileDecorations.Length) i = 0; if (c == TileDecorations[i]) rv--; }
                if (i == VWall4 && y > 0) {
                    if (TileDecorations[Get(x, y - 1)] != VWall2 || protectedTiles[At(x, y - 1)]) i = VWall2;
                    else Set(x, y - 1, VWall5);
                }
                if (i == HWall4 && x + 1 < Width) {
                    if (TileDecorations[Get(x + 1, y)] != HWall2 || protectedTiles[At(x + 1, y)]) i = HWall2;
                    else Set(x + 1, y, HWall5);
                }
                Set(x, y, checked((byte)i));
            }
        }

        void ApplyShadowsPatterns()
        {
            for (var y = 1; y < Height; y++) for (var x = 1; x < Width; x++) {
                var s0 = BaseTypes[Get(x, y)]; var s1 = BaseTypes[Get(x - 1, y)]; var s2 = BaseTypes[Get(x, y - 1)]; var s3 = BaseTypes[Get(x - 1, y - 1)];
                foreach (var shadow in ShadowPatterns) {
                    if (shadow.Strig != s0 || shadow.S1 != 0 && shadow.S1 != s3 || shadow.S2 != 0 && shadow.S2 != s2 || shadow.S3 != 0 && shadow.S3 != s1) continue;
                    if (shadow.Nv1 != 0 && !protectedTiles[At(x - 1, y - 1)]) Set(x - 1, y - 1, shadow.Nv1);
                    if (shadow.Nv2 != 0 && !protectedTiles[At(x, y - 1)]) Set(x, y - 1, shadow.Nv2);
                    if (shadow.Nv3 != 0 && !protectedTiles[At(x - 1, y)]) Set(x - 1, y, shadow.Nv3);
                }
            }
            for (var y = 1; y < Height; y++) for (var x = 1; x < Width; x++) {
                if (protectedTiles[At(x - 1, y)]) continue;
                var right = Get(x, y);
                if (Get(x - 1, y) == Floor12) Set(x - 1, y, IsAny(right, DFence, VFenceEnd, VFence, HWallVFence, HArchVFence, HArchVDoor) ? Floor14 : Floor12);
                if (Get(x - 1, y) == HArchShadow) Set(x - 1, y, IsAny(right, DFence, VFenceEnd, VFence, HWallVFence, HArchVFence, HArchVDoor) ? HArchShadow2 : HArchShadow);
                if (Get(x - 1, y) == HWallShadow) Set(x - 1, y, IsAny(right, DFence, VFenceEnd, VFence, HWallVFence, HArchVFence, HArchVDoor) ? HWallShadow2 : HWallShadow);
            }
        }

        void FillFloor()
        {
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) if (Get(x, y) == Floor && !protectedTiles[At(x, y)]) {
                var value = random.RandomIntLessThan(3);
                if (value == 1) Set(x, y, Floor22); else if (value == 2) Set(x, y, Floor23);
            }
        }

        void FixTransparency()
        {
            for (var j = 0; j < Height; j++) for (var i = 0; i < Width; i++) {
                var xx = WorldOffset + i * 2; var yy = WorldOffset + j * 2; var trans = GetWorld(xx, yy);
                if (Get(i, j) == DirtHwallEnd && j > 0 && Get(i, j - 1) == DirtHwall) { SetWorld(xx + 1, yy, trans); SetWorld(xx + 1, yy + 1, trans); }
                if (Get(i, j) == DirtVwallEnd && i + 1 < Height && Get(i + 1, j) == DirtVwall) { SetWorld(xx, yy + 1, trans); SetWorld(xx + 1, yy + 1, trans); }
                if (Get(i, j) == DirtHwall) { SetWorld(xx + 1, yy, trans); SetWorld(xx + 1, yy + 1, trans); }
                if (Get(i, j) == DirtVwall) { SetWorld(xx, yy + 1, trans); SetWorld(xx + 1, yy + 1, trans); }
                if (Get(i, j) == VDirtCorner) { SetWorld(xx + 1, yy, trans); SetWorld(xx, yy + 1, trans); SetWorld(xx + 1, yy + 1, trans); }
            }
        }

        void FixDirtTiles()
        {
            for (var j = 0; j < Height - 1; j++) for (var i = 0; i < Width - 1; i++) {
                if (Get(i, j) == HDirtCorner && Get(i + 1, j) != DirtVwall) Set(i, j, DirtCorner2);
                if (Get(i, j) == DirtVwall && Get(i + 1, j) != DirtVwall) Set(i, j, DirtVWall2);
                if (Get(i, j) == DirtVwallEnd && Get(i + 1, j) != DirtVwall) Set(i, j, DirtVWallEnd2);
                if (Get(i, j) == DirtHwall && Get(i, j + 1) != DirtHwall) Set(i, j, DirtHWall2);
                if (Get(i, j) == HDirtCorner && Get(i, j + 1) != DirtHwall) Set(i, j, DirtCorner2);
                if (Get(i, j) == DirtHwallEnd && Get(i, j + 1) != DirtHwall) Set(i, j, DirtHWallEnd2);
            }
        }

        void FixCornerTiles()
        {
            for (var j = 1; j < Height - 1; j++) for (var i = 1; i < Width - 1; i++) {
                if (!protectedTiles[At(i, j)] && Get(i, j) == HCorner && Get(i - 1, j) == Floor && Get(i, j - 1) == VWall) Set(i, j, VCorner);
                if (Get(i, j) == DirtCorner2 && Get(i + 1, j) == Floor && Get(i, j + 1) == VWall) Set(i, j, HArchEnd);
                if (Get(i, j) == DirtCorner2 && Get(i, j + 1) == Floor && Get(i + 1, j) == HWall) Set(i, j, VArchEnd);
            }
        }

        bool PlaceStairs(out AuthoritativeCathedralPoint up, out AuthoritativeCathedralPoint down)
        {
            // Original Cathedral uses the native L5 stair miniset. The
            // ordinary STAIRSUP variant is reserved for non-original worlds.
            var upSet = new MiniSet(4, 4,
                new byte[,] { { 22, 22, 22, 22 }, { 2, 2, 2, 2 }, { 13, 13, 13, 13 }, { 13, 13, 13, 13 } },
                new byte[,] { { 0, 66, 23, 0 }, { 63, 64, 65, 0 }, { 0, 67, 68, 0 }, { 0, 0, 0, 0 } });
            var downSet = new MiniSet(4, 3,
                new byte[,] { { 13, 13, 13, 13 }, { 13, 13, 13, 13 }, { 13, 13, 13, 13 } },
                new byte[,] { { 62, 57, 58, 0 }, { 61, 59, 60, 0 }, { 0, 0, 0, 0 } });
            if (!PlaceMiniSet(upSet, 1600, true, out up)) { down = default; return false; }
            if (!PlaceMiniSet(downSet, 1600, true, out down)) return false;
            return true;
        }

        bool PlaceMiniSet(MiniSet miniset, int tries, bool drlg1Quirk, out AuthoritativeCathedralPoint position)
        {
            var x = random.GenerateRnd(Width - miniset.Width);
            var y = random.GenerateRnd(Height - miniset.Height);
            for (var attempt = 0; attempt < tries; attempt++) {
                if (x == Width - miniset.Width) { x = 0; y++; if (y == Height - miniset.Height) y = 0; }
                if (drlg1Quirk) {
                    var valid = true;
                    if (x <= 12) { x++; valid = false; }
                    if (y <= 12) { y++; valid = false; }
                    if (!valid) { x++; continue; }
                }
                if (Matches(miniset, x, y)) { Place(miniset, x, y); position = new(x, y); return true; }
                x++;
            }
            position = default;
            return false;
        }

        bool Matches(MiniSet set, int x, int y)
        {
            for (var yy = 0; yy < set.Height; yy++) for (var xx = 0; xx < set.Width; xx++) {
                if (set.Search[yy, xx] != 0 && Get(x + xx, y + yy) != set.Search[yy, xx]) return false;
                if (protectedTiles[At(x + xx, y + yy)]) return false;
            }
            return true;
        }

        void Place(MiniSet set, int x, int y)
        {
            for (var yy = 0; yy < set.Height; yy++) for (var xx = 0; xx < set.Width; xx++) if (set.Replace[yy, xx] != 0) Set(x + xx, y + yy, set.Replace[yy, xx]);
        }

        void FloodTransparencyValues()
        {
            byte next = 1;
            for (var j = 0; j < Height; j++) for (var i = 0; i < Width; i++) {
                var xx = WorldOffset + i * 2; var yy = WorldOffset + j * 2;
                if (Get(i, j) == Floor && GetWorld(xx, yy) == 0) { FindTransparencyValues(xx, yy, next); next++; }
            }
        }

        void FindTransparencyValues(int floorX, int floorY, byte value)
        {
            var stack = new Stack<Seed>();
            stack.Push(new Seed(floorX, floorX + 1, floorY, 1));
            while (stack.Count != 0) {
                var seed = stack.Pop();
                var scanLeft = seed.ScanStart;
                if (IsInside(scanLeft, seed.Y)) {
                    while (IsInside(scanLeft - 1, seed.Y)) { FillTransparency(scanLeft - 1, seed.Y, value); scanLeft--; }
                    CheckDiagonals(scanLeft, seed.Y, -1, value, stack);
                }
                if (scanLeft < seed.ScanStart) stack.Push(new Seed(scanLeft, seed.ScanStart - 1, seed.Y - seed.Dy, -seed.Dy));
                var scanRight = seed.ScanStart;
                while (scanRight < seed.ScanEnd) {
                    while (IsInside(scanRight, seed.Y)) { FillTransparency(scanRight, seed.Y, value); scanRight++; }
                    stack.Push(new Seed(scanLeft, scanRight - 1, seed.Y + seed.Dy, seed.Dy));
                    if (scanRight - 1 > seed.ScanEnd) stack.Push(new Seed(seed.ScanEnd + 1, scanRight - 1, seed.Y - seed.Dy, -seed.Dy));
                    if (scanLeft < scanRight) CheckDiagonals(scanRight - 1, seed.Y, 1, value, stack);
                    while (scanRight < seed.ScanEnd && !IsInside(scanRight, seed.Y)) scanRight++;
                    scanLeft = scanRight;
                    if (scanLeft < seed.ScanEnd) CheckDiagonals(scanLeft, seed.Y, -1, value, stack);
                }
            }
        }

        void CheckDiagonals(int x, int y, int direction, byte value, Stack<Seed> stack)
        {
            var upY = y - 1; var upX = x + direction;
            if (!IsInside(x, upY) && IsInside(upX, upY)) stack.Push(new Seed(upX, upX + 1, upY, -1));
            var downY = y + 1; var downX = x + direction;
            if (!IsInside(x, downY) && IsInside(downX, downY)) stack.Push(new Seed(downX, downX + 1, downY, 1));
        }

        bool IsInside(int x, int y)
            => GetWorld(x, y) == 0 && IsFloorWorld(x, y);

        bool IsFloorWorld(int x, int y)
        {
            var i = (x - WorldOffset) / 2;
            var j = (y - WorldOffset) / 2;
            return (uint)i < Width && (uint)j < Height && Get(i, j) == Floor;
        }

        void FillTransparency(int x, int y, byte value)
        {
            foreach (var (dx, dy) in Directions) if (!IsFloorWorld(x + dx, y + dy)) SetWorld(x + dx, y + dy, value);
            SetWorld(x, y, value);
        }

        void CopyEntranceTransparency()
        {
            for (var j = 0; j < Height; j++) for (var i = 0; i < Width; i++) if (Get(i, j) == EntranceStairs) {
                var xx = WorldOffset + i * 2; var yy = WorldOffset + j * 2;
                SetWorld(xx, yy, GetWorld(xx, yy + 1)); SetWorld(xx + 1, yy, GetWorld(xx + 1, yy + 1));
            }
        }

        ushort[] ExtractInteriorTransparency()
        {
            var result = new ushort[80 * 80];
            for (var y = 0; y < 80; y++) for (var x = 0; x < 80; x++) result[y * 80 + x] = GetWorld(WorldOffset + x, WorldOffset + y);
            return result;
        }

        void SetMask(int x, int y, bool value) { if ((uint)x < Width && (uint)y < Height) dungeonMask[At(x, y)] = value; }
        bool GetMask(int x, int y) => (uint)x < Width && (uint)y < Height && dungeonMask[At(x, y)];
        int CountMask() => dungeonMask.Count(value => value);
        int At(int x, int y) => y * Width + x;
        byte Get(int x, int y) => (uint)x < Width && (uint)y < Height ? map[At(x, y)] : Dirt;
        void Set(int x, int y, byte value) { if ((uint)x < Width && (uint)y < Height) map[At(x, y)] = value; }
        int WorldAt(int x, int y) => y * WorldWidth + x;
        byte GetWorld(int x, int y) => (uint)x < WorldWidth && (uint)y < WorldWidth ? transparency[WorldAt(x, y)] : (byte)0;
        void SetWorld(int x, int y, byte value) { if ((uint)x < WorldWidth && (uint)y < WorldWidth) transparency[WorldAt(x, y)] = value; }
        bool IsWallTerminator(byte value) => IsAny(value, Corner, DWall, DArch, VWallEnd, HWallEnd, VCorner, HCorner, DirtHwall, DirtVwall, VDirtCorner, HDirtCorner, DirtHwallEnd, DirtVwallEnd);
        bool IsAny(byte value, params byte[] values) => values.Contains(value);
        static AuthoritativeCathedralPoint Swap(AuthoritativeCathedralPoint value) => new(value.Y, value.X);
        static Rect SwapRect(Rect value) => new(value.Y, value.X, value.Height, value.Width);
    }

    private readonly record struct Seed(int ScanStart, int ScanEnd, int Y, int Dy);
    private readonly record struct MiniSet(int Width, int Height, byte[,] Search, byte[,] Replace);
    private static readonly (int X, int Y)[] Directions = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];
}
