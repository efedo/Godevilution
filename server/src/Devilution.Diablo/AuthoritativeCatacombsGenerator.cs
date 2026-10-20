namespace Devilution.Diablo;

/**
 * Pure managed port of the quest-disabled Original Diablo Catacombs generator.
 * The implementation follows level_l2.cpp and dungeon_common.cpp. It accepts
 * no map, TIL, SOL, process-global state, or campaign inputs; fine pieces and
 * specials are materialized later from the admitted content catalogs.
 */
public static partial class AuthoritativeCatacombsGenerator
{
	private const int MegaWidth = 40;
	private const int MegaHeight = 40;
	private const int FineWidth = 112;
	private const int FineHeight = 112;
	private const int FineOffset = 16;
	private const int NativeMinimumBound = 16;
	private const int NativeMaximumBound = 96;
	private const byte Empty = 0;
	private const int FloorMega = 3;

	private readonly record struct MiniSet(int Width, int Height, byte[] Search, byte[] Replace);
	private readonly record struct NativePoint(int X, int Y);
	private readonly record struct NativeSize(int Width, int Height);
	private readonly record struct NativeRect(int X, int Y, int Width, int Height)
	{
		public bool Contains(int x, int y)
			=> x >= X && x < X + Width && y >= Y && y < Y + Height;
	}
	private readonly record struct RoomNode(NativePoint TopLeft, NativePoint BottomRight);
	private readonly record struct HallNode(NativePoint Beginning, NativePoint End, HallDirection Direction);
	private readonly record struct TransparencySeed(int ScanStart, int ScanEnd, int Y, int Dy);
	private readonly record struct ShadowPattern(byte Strig, byte S1, byte S2, byte S3, byte Nv1, byte Nv2, byte Nv3);

	private enum HallDirection
	{
		None,
		Up,
		Right,
		Down,
		Left,
	}

	private static readonly NativePoint[] DirectionDisplacements =
	[
		new(0, 0),
		new(0, -1),
		new(1, 0),
		new(0, 1),
		new(-1, 0),
	];

	private static readonly ShadowPattern[] ShadowPatterns =
	[
		new(6, 3, 0, 3, 48, 0, 50),
		new(9, 3, 0, 3, 48, 0, 50),
	];

	private static MiniSet[] CreateDecorationMinisets()
		=>
	[
			CTRDOOR1, CTRDOOR2, CTRDOOR3, CTRDOOR4, CTRDOOR5, CTRDOOR6, CTRDOOR7, CTRDOOR8,
			VARCH33, VARCH34, VARCH35, VARCH36, VARCH37, VARCH38, VARCH39, VARCH40,
			VARCH1, VARCH2, VARCH3, VARCH4, VARCH5, VARCH6, VARCH7, VARCH8,
			VARCH9, VARCH10, VARCH11, VARCH12, VARCH13, VARCH14, VARCH15, VARCH16,
			VARCH17, VARCH18, VARCH19, VARCH20, VARCH21, VARCH22, VARCH23, VARCH24,
			VARCH25, VARCH26, VARCH27, VARCH28, VARCH29, VARCH30, VARCH31, VARCH32,
			HARCH1, HARCH2, HARCH3, HARCH4, HARCH5, HARCH6, HARCH7, HARCH8,
			HARCH9, HARCH10, HARCH11, HARCH12, HARCH13, HARCH14, HARCH15, HARCH16,
			HARCH17, HARCH18, HARCH19, HARCH20, HARCH21, HARCH22, HARCH23, HARCH24,
			HARCH25, HARCH26, HARCH27, HARCH28, HARCH29, HARCH30, HARCH31, HARCH32,
			HARCH33, HARCH34, HARCH35, HARCH36, HARCH37, HARCH38, HARCH39, HARCH40,
		];

	/** Generate levels 5-8 using only the approved quest-disabled stair policy. */
	public static AuthoritativeCatacombsGenerationResult Generate(
		int depth,
		uint dungeonSeed,
		AuthoritativeCatacombsEntry entry,
		uint? retainedLevelSeed = null,
		AuthoritativeCatacombsGenerationPolicy policy = AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs)
	{
		if (depth is < 5 or > 8)
			throw new ArgumentOutOfRangeException(nameof(depth), depth, "Catacombs depth must be between 5 and 8.");
		if (policy != AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs)
			throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported Catacombs generation policy.");
		if (entry is not AuthoritativeCatacombsEntry.Main
		    and not AuthoritativeCatacombsEntry.Previous
		    and not AuthoritativeCatacombsEntry.TownWarpDown)
			throw new ArgumentOutOfRangeException(nameof(entry), entry, "Unsupported Catacombs entry.");
		if (entry == AuthoritativeCatacombsEntry.TownWarpDown && depth != 5)
			throw new ArgumentOutOfRangeException(nameof(entry), entry, "Town warp entry is only available on Catacombs level 5.");

		return new GenerationState(depth, dungeonSeed, entry, retainedLevelSeed, policy).Run();
	}

	private sealed class GenerationState
	{
		private const int RoomLimit = 80;
		private const int FineCellCount = FineWidth * FineHeight;
		private const int MegaCellCount = MegaWidth * MegaHeight;
		private readonly int depth;
		private readonly uint inputSeed;
		private readonly uint? initialLevelSeed;
		private readonly AuthoritativeCatacombsEntry entry;
		private readonly AuthoritativeCatacombsGenerationPolicy policy;
		private readonly AuthoritativeLegacyRandom random;
		private readonly byte[] megaTiles = new byte[MegaCellCount];
		private readonly byte[] replacementMegaTiles = new byte[MegaCellCount];
		private readonly bool[] protectedTiles = new bool[MegaCellCount];
		private readonly char[] preDungeon = new char[MegaCellCount];
		private readonly sbyte[] transparency = new sbyte[FineCellCount];
		private readonly byte[] nativeTileFlags = new byte[FineCellCount];
		private readonly RoomNode[] roomList = new RoomNode[81];
		private readonly List<HallNode> hallList = [];
		private readonly List<AuthoritativeCatacombsThemeRoom> themes = [];
		private int roomCount;
		private int nextTransparencyValue = 1;
		private NativeRect setPieceRoom;
		private NativeRect setPiece;
		private NativePoint viewPosition;
		private NativePoint stairsUp;
		private NativePoint stairsDown;
		private NativePoint? townWarpStairs;
		private uint attemptSeed;

		public GenerationState(
			int depth,
			uint inputSeed,
			AuthoritativeCatacombsEntry entry,
			uint? initialLevelSeed,
			AuthoritativeCatacombsGenerationPolicy policy)
		{
			this.depth = depth;
			this.inputSeed = inputSeed;
			this.entry = entry;
			this.initialLevelSeed = initialLevelSeed;
			this.policy = policy;
			random = new AuthoritativeLegacyRandom(initialLevelSeed ?? inputSeed);
			Array.Fill(megaTiles, Empty);
			Array.Fill(replacementMegaTiles, Empty);
			Array.Fill(preDungeon, ' ');
		}

		public AuthoritativeCatacombsGenerationResult Run()
		{
			while (true) {
				attemptSeed = random.State;
				roomCount = 0;
				Array.Clear(protectedTiles);
				Array.Fill(preDungeon, ' ');
				Array.Clear(transparency);
				hallList.Clear();
				nextTransparencyValue = 1;
				setPieceRoom = default;
				setPiece = default;

				if (!CreateDungeon())
					continue;
				FixTilesPatterns();
				// The selected quest-disabled policy never selects an L2 set-piece.
				FloodTransparencyValues(FloorMega);
				FixTransparency();
				if (!PlaceStairs())
					continue;
				break;
			}

			FixLockout();
			FixDoors();
			FixDirtTiles();
			PlaceThemeRooms();
			PlaceDecorationMinisets();
			Substitution();
			ApplyShadowsPatterns();
			Array.Copy(megaTiles, replacementMegaTiles, MegaCellCount);

			var resultMegaTiles = megaTiles.Select(value => (ushort)value).ToArray();
			var resultReplacementMegaTiles = replacementMegaTiles.Select(value => (ushort)value).ToArray();
			var resultProtectedTiles = protectedTiles.ToArray();
			var resultTransparency = transparency.Select(value => unchecked((byte)value)).ToArray();
			var entryPosition = new AuthoritativeCathedralPoint(viewPosition.X, viewPosition.Y);
			var result = new AuthoritativeCatacombsGenerationResult(
				depth,
				policy,
				entry,
				inputSeed,
				initialLevelSeed,
				attemptSeed,
				random.State,
				Array.AsReadOnly(resultMegaTiles),
				Array.AsReadOnly(resultReplacementMegaTiles),
				Array.AsReadOnly(resultProtectedTiles),
				Array.AsReadOnly(resultTransparency),
				Array.AsReadOnly(nativeTileFlags.ToArray()),
				Array.AsReadOnly(themes.ToArray()),
				new(stairsUp.X, stairsUp.Y),
				new(stairsDown.X, stairsDown.Y),
				townWarpStairs is { } warp ? new AuthoritativeCathedralPoint(warp.X, warp.Y) : null,
				entryPosition,
				new(NativeMinimumBound, NativeMinimumBound),
				new(NativeMaximumBound, NativeMaximumBound),
				ToResultRectangle(setPieceRoom),
				ToResultRectangle(setPiece),
				nextTransparencyValue);
			result.Validate();
			return result.CopyOwned();
		}

		private bool CreateDungeon()
		{
			CreateRoom(new(2, 2), new(MegaWidth - 1, MegaHeight - 1), 0, HallDirection.None, null);
			while (hallList.Count != 0) {
				var node = hallList[0];
				hallList.RemoveAt(0);
				ConnectHall(node);
			}

			for (var y = 0; y < MegaHeight; y++) {
				for (var x = 0; x < MegaWidth; x++) {
					var cell = GetPreDungeon(x, y);
					if (cell is 'A' or 'B' or 'C' or 'E')
						preDungeon[At(x, y)] = '#';
					if (cell != ',')
						continue;
					preDungeon[At(x, y)] = '.';
					for (var dy = -1; dy <= 1; dy++) {
						for (var dx = -1; dx <= 1; dx++) {
							if (dx == 0 && dy == 0)
								continue;
							var neighborX = x + dx;
							var neighborY = y + dy;
							if (neighborX < 0 || neighborY < 0 || neighborX >= MegaWidth || neighborY >= MegaHeight)
								continue;
							if (preDungeon[At(neighborX, neighborY)] == ' ')
								preDungeon[At(neighborX, neighborY)] = '#';
						}
					}
				}
			}

			if (!FillVoids())
				return false;
			for (var y = 0; y < MegaHeight; y++)
				for (var x = 0; x < MegaWidth; x++)
					DoPatternCheck(x, y);
			return true;
		}

		private void CreateRoom(NativePoint topLeft, NativePoint bottomRight, int destinationRoom, HallDirection hallDirection, NativeSize? forcedSize)
		{
			const int areaMin = 2;
			if (roomCount >= RoomLimit || topLeft.X + areaMin > bottomRight.X || topLeft.Y + areaMin > bottomRight.Y)
				return;

			var areaWidth = bottomRight.X - topLeft.X;
			var areaHeight = bottomRight.Y - topLeft.Y;
			var roomWidth = areaWidth;
			var roomHeight = areaHeight;
			const int roomMax = 10;
			const int roomMin = 4;
			if (areaWidth > roomMin)
				roomWidth = random.GenerateRnd(Math.Min(areaWidth, roomMax) - roomMin) + roomMin;
			if (areaHeight > roomMin)
				roomHeight = random.GenerateRnd(Math.Min(areaHeight, roomMax) - roomMin) + roomMin;
			if (forcedSize is { } forced) {
				roomWidth = forced.Width;
				roomHeight = forced.Height;
			}

			var randomWidth = random.GenerateRnd(areaWidth);
			var randomHeight = random.GenerateRnd(areaHeight);
			var roomTopLeft = new NativePoint(topLeft.X + randomWidth, topLeft.Y + randomHeight);
			var roomBottomRight = new NativePoint(roomTopLeft.X + roomWidth, roomTopLeft.Y + roomHeight);
			if (roomBottomRight.X > bottomRight.X) {
				roomBottomRight = roomBottomRight with { X = bottomRight.X };
				roomTopLeft = roomTopLeft with { X = bottomRight.X - roomWidth };
			}
			if (roomBottomRight.Y > bottomRight.Y) {
				roomBottomRight = roomBottomRight with { Y = bottomRight.Y };
				roomTopLeft = roomTopLeft with { Y = bottomRight.Y - roomHeight };
			}
			roomTopLeft = roomTopLeft with { X = Math.Clamp(roomTopLeft.X, 1, 38), Y = Math.Clamp(roomTopLeft.Y, 1, 38) };
			roomBottomRight = roomBottomRight with { X = Math.Clamp(roomBottomRight.X, 1, 38), Y = Math.Clamp(roomBottomRight.Y, 1, 38) };
			DefineRoom(roomTopLeft, roomBottomRight, forcedSize.HasValue);

			const int standoff = 2;
			if (forcedSize is not null)
				setPieceRoom = new(roomTopLeft.X + standoff, roomTopLeft.Y + standoff, roomWidth - 1, roomHeight - 1);
			var roomId = roomCount;
			if (destinationRoom != 0) {
				var hx1 = 0;
				var hy1 = 0;
				var hx2 = 0;
				var hy2 = 0;
				if (hallDirection == HallDirection.Up) {
					hx1 = random.GenerateRnd(roomWidth - 2) + roomTopLeft.X + 1;
					hy1 = roomTopLeft.Y;
					var destinationWidth = roomList[destinationRoom].BottomRight.X - roomList[destinationRoom].TopLeft.X - 2;
					hx2 = random.GenerateRnd(destinationWidth) + roomList[destinationRoom].TopLeft.X + 1;
					hy2 = roomList[destinationRoom].BottomRight.Y;
				}
				if (hallDirection == HallDirection.Down) {
					hx1 = random.GenerateRnd(roomWidth - 2) + roomTopLeft.X + 1;
					hy1 = roomBottomRight.Y;
					var destinationWidth = roomList[destinationRoom].BottomRight.X - roomList[destinationRoom].TopLeft.X - 2;
					hx2 = random.GenerateRnd(destinationWidth) + roomList[destinationRoom].TopLeft.X + 1;
					hy2 = roomList[destinationRoom].TopLeft.Y;
				}
				if (hallDirection == HallDirection.Right) {
					hx1 = roomBottomRight.X;
					hy1 = random.GenerateRnd(roomHeight - 2) + roomTopLeft.Y + 1;
					hx2 = roomList[destinationRoom].TopLeft.X;
					var destinationHeight = roomList[destinationRoom].BottomRight.Y - roomList[destinationRoom].TopLeft.Y - 2;
					hy2 = random.GenerateRnd(destinationHeight) + roomList[destinationRoom].TopLeft.Y + 1;
				}
				if (hallDirection == HallDirection.Left) {
					hx1 = roomTopLeft.X;
					hy1 = random.GenerateRnd(roomHeight - 2) + roomTopLeft.Y + 1;
					hx2 = roomList[destinationRoom].BottomRight.X;
					var destinationHeight = roomList[destinationRoom].BottomRight.Y - roomList[destinationRoom].TopLeft.Y - 2;
					hy2 = random.GenerateRnd(destinationHeight) + roomList[destinationRoom].TopLeft.Y + 1;
				}
				hallList.Add(new(new(hx1, hy1), new(hx2, hy2), hallDirection));
			}

			var roomBottomLeft = new NativePoint(roomTopLeft.X, roomBottomRight.Y);
			var roomTopRight = new NativePoint(roomBottomRight.X, roomTopLeft.Y);
			if (roomHeight > roomWidth) {
				CreateRoom(new(topLeft.X + standoff, topLeft.Y + standoff), new(roomBottomLeft.X - standoff, roomBottomLeft.Y - standoff), roomId, HallDirection.Right, null);
				CreateRoom(new(roomTopRight.X + standoff, roomTopRight.Y + standoff), new(bottomRight.X - standoff, bottomRight.Y - standoff), roomId, HallDirection.Left, null);
				CreateRoom(new(topLeft.X + standoff, roomBottomRight.Y + standoff), new(roomBottomRight.X - standoff, bottomRight.Y - standoff), roomId, HallDirection.Up, null);
				CreateRoom(new(roomTopLeft.X + standoff, topLeft.Y + standoff), new(bottomRight.X - standoff, roomTopLeft.Y - standoff), roomId, HallDirection.Down, null);
			} else {
				CreateRoom(new(topLeft.X + standoff, topLeft.Y + standoff), new(roomTopRight.X - standoff, roomTopRight.Y - standoff), roomId, HallDirection.Down, null);
				CreateRoom(new(roomBottomLeft.X + standoff, roomBottomLeft.Y + standoff), new(bottomRight.X - standoff, bottomRight.Y - standoff), roomId, HallDirection.Up, null);
				CreateRoom(new(topLeft.X + standoff, roomTopLeft.Y + standoff), new(roomTopLeft.X - standoff, bottomRight.Y - standoff), roomId, HallDirection.Right, null);
				CreateRoom(new(roomBottomRight.X + standoff, topLeft.Y + standoff), new(bottomRight.X - standoff, roomBottomRight.Y - standoff), roomId, HallDirection.Left, null);
			}
		}

		private void DefineRoom(NativePoint topLeft, NativePoint bottomRight, bool forceHorizontalWall)
		{
			preDungeon[At(topLeft.X, topLeft.Y)] = 'C';
			preDungeon[At(topLeft.X, bottomRight.Y)] = 'E';
			preDungeon[At(bottomRight.X, topLeft.Y)] = 'B';
			preDungeon[At(bottomRight.X, bottomRight.Y)] = 'A';
			roomCount++;
			roomList[roomCount] = new(topLeft, bottomRight);

			if (forceHorizontalWall) {
				for (var x = topLeft.X; x < bottomRight.X; x++) {
					while (x < bottomRight.Y) {
						SetProtected(x, topLeft.Y);
						x++;
					}
				}
			}
			for (var x = topLeft.X + 1; x <= bottomRight.X - 1; x++) {
				preDungeon[At(x, topLeft.Y)] = '#';
				preDungeon[At(x, bottomRight.Y)] = '#';
			}
			bottomRight = bottomRight with { Y = bottomRight.Y - 1 };
			for (var y = topLeft.Y + 1; y <= bottomRight.Y; y++) {
				preDungeon[At(topLeft.X, y)] = '#';
				preDungeon[At(bottomRight.X, y)] = '#';
				for (var x = topLeft.X + 1; x < bottomRight.X; x++)
					preDungeon[At(x, y)] = '.';
			}
		}

		private void CreateDoorType(NativePoint position)
		{
			if (GetPreDungeon(position.X - 1, position.Y) == 'D'
			    || GetPreDungeon(position.X + 1, position.Y) == 'D'
			    || GetPreDungeon(position.X, position.Y - 1) == 'D'
			    || GetPreDungeon(position.X, position.Y + 1) == 'D'
			    || GetPreDungeon(position.X, position.Y) is 'A' or 'B' or 'C' or 'E')
				return;
			preDungeon[At(position.X, position.Y)] = 'D';
		}

		private static NativePoint Add(NativePoint point, NativePoint displacement, int multiplier = 1)
			=> new(point.X + displacement.X * multiplier, point.Y + displacement.Y * multiplier);

		private void PlaceHallExtension(NativePoint position)
		{
			if (GetPreDungeon(position.X, position.Y) == ' ')
				preDungeon[At(position.X, position.Y)] = ',';
		}

		private void ConnectHall(HallNode node)
		{
			var beginning = node.Beginning;
			var end = node.End;
			var placeMinusExtension = random.GenerateRnd(100) < 50;
			var placePlusExtension = random.GenerateRnd(100) < 50;
			CreateDoorType(beginning);
			CreateDoorType(end);
			var direction = node.Direction;
			end = Add(end, DirectionDisplacements[(int)direction], -1);
			preDungeon[At(end.X, end.Y)] = ',';
			var insideRoom = false;
			do {
				if (beginning.X >= MegaWidth - 2 && direction == HallDirection.Right)
					direction = HallDirection.Left;
				if (beginning.Y >= MegaHeight - 2 && direction == HallDirection.Down)
					direction = HallDirection.Up;
				if (beginning.X <= 1 && direction == HallDirection.Left)
					direction = HallDirection.Right;
				if (beginning.Y <= 1 && direction == HallDirection.Up)
					direction = HallDirection.Down;
				if (GetPreDungeon(beginning.X, beginning.Y) == 'C' && direction is HallDirection.Up or HallDirection.Left)
					direction = HallDirection.Right;
				if (GetPreDungeon(beginning.X, beginning.Y) == 'B' && direction is HallDirection.Up or HallDirection.Right)
					direction = HallDirection.Down;
				if (GetPreDungeon(beginning.X, beginning.Y) == 'E' && direction is HallDirection.Left or HallDirection.Down)
					direction = HallDirection.Up;
				if (GetPreDungeon(beginning.X, beginning.Y) == 'A' && direction is HallDirection.Right or HallDirection.Down)
					direction = HallDirection.Left;
				beginning = Add(beginning, DirectionDisplacements[(int)direction]);
				var current = GetPreDungeon(beginning.X, beginning.Y);
				if (current == ' ') {
					if (insideRoom) {
						CreateDoorType(Add(beginning, DirectionDisplacements[(int)direction], -1));
						insideRoom = false;
					} else {
						if (placeMinusExtension) {
							var extension = direction is not HallDirection.Up and not HallDirection.Down
							    ? new NativePoint(beginning.X, beginning.Y - 1)
							    : new NativePoint(beginning.X - 1, beginning.Y);
							PlaceHallExtension(extension);
						}
						if (placePlusExtension) {
							var extension = direction is not HallDirection.Up and not HallDirection.Down
							    ? new NativePoint(beginning.X, beginning.Y + 1)
							    : new NativePoint(beginning.X + 1, beginning.Y);
							PlaceHallExtension(extension);
						}
					}
					preDungeon[At(beginning.X, beginning.Y)] = ',';
				} else {
					if (!insideRoom && current == '#')
						CreateDoorType(beginning);
					if (current != ',')
						insideRoom = true;
				}

				var dx = Math.Abs(end.X - beginning.X);
				var dy = Math.Abs(end.Y - beginning.Y);
				if (dx > dy) {
					var probability = Math.Min(2 * dx, 30);
					if (random.GenerateRnd(100) < probability)
						direction = end.X <= beginning.X || beginning.X >= MegaWidth ? HallDirection.Left : HallDirection.Right;
				} else {
					var probability = Math.Min(5 * dy, 80);
					if (random.GenerateRnd(100) < probability)
						direction = end.Y <= beginning.Y || beginning.Y >= MegaHeight ? HallDirection.Up : HallDirection.Down;
				}
				if (dy < 10 && beginning.X == end.X && direction is HallDirection.Right or HallDirection.Left)
					direction = end.Y <= beginning.Y || beginning.Y >= MegaHeight ? HallDirection.Up : HallDirection.Down;
				if (dx < 10 && beginning.Y == end.Y && direction is HallDirection.Up or HallDirection.Down)
					direction = end.X <= beginning.X || beginning.X >= MegaWidth ? HallDirection.Left : HallDirection.Right;
				if (dy == 1 && dx > 1 && direction is HallDirection.Up or HallDirection.Down)
					direction = end.X <= beginning.X || beginning.X >= MegaWidth ? HallDirection.Left : HallDirection.Right;
				if (dx == 1 && dy > 1 && direction is HallDirection.Right or HallDirection.Left)
					direction = end.Y <= beginning.Y || beginning.X >= MegaWidth ? HallDirection.Up : HallDirection.Down;
				if (dx == 0 && GetPreDungeon(beginning.X, beginning.Y) != ' ' && direction is HallDirection.Right or HallDirection.Left)
					direction = end.X <= node.Beginning.X || beginning.X >= MegaWidth ? HallDirection.Up : HallDirection.Down;
				if (dy == 0 && GetPreDungeon(beginning.X, beginning.Y) != ' ' && direction is HallDirection.Up or HallDirection.Down)
					direction = end.Y <= node.Beginning.Y || beginning.Y >= MegaHeight ? HallDirection.Left : HallDirection.Right;
			} while (beginning != end);
		}

		private void DoPatternCheck(int x, int y)
		{
			for (var patternIndex = 0; patternIndex < Patterns.Length && Patterns[patternIndex][4] != 255; patternIndex++) {
				var px = x - 1;
				var py = y - 1;
				var matches = true;
				for (var cell = 0; cell < 9 && matches; cell++) {
					if (cell is 3 or 6) {
						py++;
						px = x - 1;
					}
					if ((uint)px < MegaWidth && (uint)py < MegaHeight) {
						var required = Patterns[patternIndex][cell];
						var value = GetPreDungeon(px, py);
						matches = required switch
						{
							0 => true,
							1 => value == '#',
							2 => value == '.',
							3 => value == 'D',
							4 => value == ' ',
							5 => value is 'D' or '.',
							6 => value is 'D' or '#',
							7 => value is ' ' or '.',
							8 => value is 'D' or '#' or '.',
							_ => false,
						};
					}
					px++;
				}
				if (matches)
					SetCurrentMega(x, y, checked((byte)Patterns[patternIndex][9]));
			}
		}

		private void FixTilesPatterns()
		{
			for (var y = 0; y < MegaHeight; y++) {
				for (var x = 0; x < MegaWidth; x++) {
					if (GetCurrentMega(x, y) == 1 && GetCurrentMega(x, y + 1) == 3)
						SetCurrentMega(x, y + 1, 1);
					if (GetCurrentMega(x, y) == 3 && GetCurrentMega(x, y + 1) == 1)
					SetCurrentMega(x, y + 1, 3);
					if (GetCurrentMega(x, y) == 3 && GetCurrentMega(x + 1, y) == 7)
					SetCurrentMega(x + 1, y, 3);
					if (GetCurrentMega(x, y) == 2 && GetCurrentMega(x + 1, y) == 3)
					SetCurrentMega(x + 1, y, 2);
					if (GetCurrentMega(x, y) == 11 && GetCurrentMega(x + 1, y) == 14)
					SetCurrentMega(x + 1, y, 16);
					if (GetCurrentMega(x, y) == 15 && GetCurrentMega(x, y + 1) == 1)
					SetCurrentMega(x, y + 1, 8);
				}
			}
		}

		private void Substitution()
		{
			for (var y = 0; y < MegaHeight; y++) {
				for (var x = 0; x < MegaWidth; x++) {
					if (setPieceRoom.Contains(x, y) || !random.FlipCoin(4))
						continue;
					var baseType = BaseTypes[GetCurrentMega(x, y)];
					if (baseType == 0)
						continue;
					var remaining = random.GenerateRnd(16);
					var replacement = -1;
					while (remaining >= 0) {
						replacement++;
						if (replacement == BaseTypes.Length)
							replacement = 0;
						if (baseType == BaseTypes[replacement])
							remaining--;
					}

					var hasNeighbor = false;
					for (var yy = y - 2; yy < y + 2 && !hasNeighbor; yy++) {
						for (var xx = x - 2; xx < x + 2; xx++) {
							if (GetCurrentMega(xx, yy) != replacement)
								continue;
							hasNeighbor = true;
							break;
						}
					}
					if (!hasNeighbor)
						SetCurrentMega(x, y, checked((byte)replacement));
				}
			}
		}

		private void ApplyShadowsPatterns()
		{
			for (var y = 1; y < MegaHeight; y++) {
				for (var x = 1; x < MegaWidth; x++) {
					var current = ShadowTypes[GetCurrentMega(x, y)];
					var left = ShadowTypes[GetCurrentMega(x - 1, y)];
					var above = ShadowTypes[GetCurrentMega(x, y - 1)];
					var aboveLeft = ShadowTypes[GetCurrentMega(x - 1, y - 1)];
					foreach (var shadow in ShadowPatterns) {
						if (shadow.Strig != current
						    || shadow.S1 != 0 && shadow.S1 != aboveLeft
						    || shadow.S2 != 0 && shadow.S2 != above
						    || shadow.S3 != 0 && shadow.S3 != left)
							continue;
						if (shadow.Nv1 != 0)
							SetCurrentMega(x - 1, y - 1, shadow.Nv1);
						if (shadow.Nv2 != 0)
							SetCurrentMega(x, y - 1, shadow.Nv2);
						if (shadow.Nv3 != 0)
							SetCurrentMega(x - 1, y, shadow.Nv3);
					}
				}
			}
		}

		private int CountEmptyTiles()
		{
			var count = 0;
			for (var y = 0; y < MegaHeight; y++)
				for (var x = 0; x < MegaWidth; x++)
					if (GetPreDungeon(x, y) == ' ')
						count++;
			return count;
		}

		private void KnockWalls(int x1, int y1, int x2, int y2)
		{
			for (var x = x1 + 1; x < x2; x++) {
				if (GetPreDungeon(x, y1 - 1) == '.' && GetPreDungeon(x, y1 + 1) == '.')
					SetPreDungeon(x, y1, '.');
				if (GetPreDungeon(x, y2 - 1) == '.' && GetPreDungeon(x, y2 + 1) == '.')
					SetPreDungeon(x, y2, '.');
				if (GetPreDungeon(x, y1 - 1) == 'D')
					SetPreDungeon(x, y1 - 1, '.');
				if (GetPreDungeon(x, y2 + 1) == 'D')
					SetPreDungeon(x, y2 + 1, '.');
			}
			for (var y = y1 + 1; y < y2; y++) {
				if (GetPreDungeon(x1 - 1, y) == '.' && GetPreDungeon(x1 + 1, y) == '.')
					SetPreDungeon(x1, y, '.');
				if (GetPreDungeon(x2 - 1, y) == '.' && GetPreDungeon(x2 + 1, y) == '.')
					SetPreDungeon(x2, y, '.');
				if (GetPreDungeon(x1 - 1, y) == 'D')
					SetPreDungeon(x1 - 1, y, '.');
				if (GetPreDungeon(x2 + 1, y) == 'D')
					SetPreDungeon(x2 + 1, y, '.');
			}
		}

		private void FillVoid(bool extendLeft, bool extendUp, bool extendRight, bool extendDown, int xx, int yy)
		{
			var x1 = xx - (extendLeft ? 1 : 0);
			var x2 = xx + (extendRight ? 1 : 0);
			var y1 = yy - (extendUp ? 1 : 0);
			var y2 = yy + (extendDown ? 1 : 0);
			if (!extendLeft) {
				while (extendUp || extendDown) {
					if (y1 == 0) extendUp = false;
					if (y2 == MegaHeight - 1) extendDown = false;
					if (y2 - y1 >= 14) { extendUp = false; extendDown = false; }
					if (extendUp) y1--;
					if (extendDown) y2++;
					if (GetPreDungeon(x2, y1) != ' ') extendUp = false;
					if (GetPreDungeon(x2, y2) != ' ') extendDown = false;
				}
				y1 += 2;
				y2 -= 2;
				if (y2 - y1 > 5) {
					while (extendRight) {
						if (x2 == MegaWidth - 1) extendRight = false;
						if (x2 - x1 >= 12) extendRight = false;
					for (var y = y1; y <= y2; y++)
						if (GetPreDungeon(x2, y) != ' ') extendRight = false;
					if (extendRight) x2++;
					}
					x2 -= 2;
					if (x2 - x1 > 5) { MapRoom(x1, y1, x2, y2); KnockWalls(x1, y1, x2, y2); }
				}
			} else if (!extendRight) {
				while (extendUp || extendDown) {
					if (y1 == 0) extendUp = false;
					if (y2 == MegaHeight - 1) extendDown = false;
					if (y2 - y1 >= 14) { extendUp = false; extendDown = false; }
					if (extendUp) y1--;
					if (extendDown) y2++;
					if (GetPreDungeon(x1, y1) != ' ') extendUp = false;
					if (GetPreDungeon(x1, y2) != ' ') extendDown = false;
				}
				y1 += 2;
				y2 -= 2;
				if (y2 - y1 > 5) {
					while (extendLeft) {
						if (x1 == 0) extendLeft = false;
						if (x2 - x1 >= 12) extendLeft = false;
					for (var y = y1; y <= y2; y++)
						if (GetPreDungeon(x1, y) != ' ') extendLeft = false;
					if (extendLeft) x1--;
					}
					x1 += 2;
					if (x2 - x1 > 5) { MapRoom(x1, y1, x2, y2); KnockWalls(x1, y1, x2, y2); }
				}
			} else if (!extendUp) {
				while (extendLeft || extendRight) {
					if (x1 == 0) extendLeft = false;
					if (x2 == MegaWidth - 1) extendRight = false;
					if (x2 - x1 >= 14) { extendLeft = false; extendRight = false; }
					if (extendLeft) x1--;
					if (extendRight) x2++;
					if (GetPreDungeon(x1, y2) != ' ') extendLeft = false;
					if (GetPreDungeon(x2, y2) != ' ') extendRight = false;
				}
				x1 += 2;
				x2 -= 2;
				if (x2 - x1 > 5) {
					while (extendDown) {
						if (y2 == MegaHeight - 1) extendDown = false;
						if (y2 - y1 >= 12) extendDown = false;
					for (var x = x1; x <= x2; x++)
						if (GetPreDungeon(x, y2) != ' ') extendDown = false;
					if (extendDown) y2++;
					}
					y2 -= 2;
					if (y2 - y1 > 5) { MapRoom(x1, y1, x2, y2); KnockWalls(x1, y1, x2, y2); }
				}
			} else if (!extendDown) {
				while (extendLeft || extendRight) {
					if (x1 == 0) extendLeft = false;
					if (x2 == MegaWidth - 1) extendRight = false;
					if (x2 - x1 >= 14) { extendLeft = false; extendRight = false; }
					if (extendLeft) x1--;
					if (extendRight) x2++;
					if (GetPreDungeon(x1, y1) != ' ') extendLeft = false;
					if (GetPreDungeon(x2, y1) != ' ') extendRight = false;
				}
				x1 += 2;
				x2 -= 2;
				if (x2 - x1 > 5) {
					while (extendUp) {
						if (y1 == 0) extendUp = false;
						if (y2 - y1 >= 12) extendUp = false;
					for (var x = x1; x <= x2; x++)
						if (GetPreDungeon(x, y1) != ' ') extendUp = false;
					if (extendUp) y1--;
					}
					y1 += 2;
					if (y2 - y1 > 5) { MapRoom(x1, y1, x2, y2); KnockWalls(x1, y1, x2, y2); }
				}
			}
		}

		private bool FillVoids()
		{
			var attempts = 0;
			while (CountEmptyTiles() > 700 && attempts < 100) {
				var x = random.GenerateRnd(38) + 1;
				var y = random.GenerateRnd(38) + 1;
				if (GetPreDungeon(x, y) != '#')
					continue;
				var extendLeft = false;
				var extendRight = false;
				var extendUp = false;
				var extendDown = false;
				if (GetPreDungeon(x - 1, y) == ' ' && GetPreDungeon(x + 1, y) == '.') {
					if (GetPreDungeon(x + 1, y - 1) == '.' && GetPreDungeon(x + 1, y + 1) == '.'
					    && GetPreDungeon(x - 1, y - 1) == ' ' && GetPreDungeon(x - 1, y + 1) == ' ') {
						extendLeft = true; extendUp = true; extendDown = true;
					}
				} else if (GetPreDungeon(x + 1, y) == ' ' && GetPreDungeon(x - 1, y) == '.') {
					if (GetPreDungeon(x - 1, y - 1) == '.' && GetPreDungeon(x - 1, y + 1) == '.'
					    && GetPreDungeon(x + 1, y - 1) == ' ' && GetPreDungeon(x + 1, y + 1) == ' ') {
						extendRight = true; extendUp = true; extendDown = true;
					}
				} else if (GetPreDungeon(x, y - 1) == ' ' && GetPreDungeon(x, y + 1) == '.') {
					if (GetPreDungeon(x - 1, y + 1) == '.' && GetPreDungeon(x + 1, y + 1) == '.'
					    && GetPreDungeon(x - 1, y - 1) == ' ' && GetPreDungeon(x + 1, y - 1) == ' ') {
						extendUp = true; extendLeft = true; extendRight = true;
					}
				} else if (GetPreDungeon(x, y + 1) == ' ' && GetPreDungeon(x, y - 1) == '.') {
					if (GetPreDungeon(x - 1, y - 1) == '.' && GetPreDungeon(x + 1, y - 1) == '.'
					    && GetPreDungeon(x - 1, y + 1) == ' ' && GetPreDungeon(x + 1, y + 1) == ' ') {
						extendDown = true; extendLeft = true; extendRight = true;
					}
				}
				if (extendLeft || extendUp || extendRight || extendDown)
					FillVoid(extendLeft, extendUp, extendRight, extendDown, x, y);
				attempts++;
			}
			return CountEmptyTiles() <= 700;
		}

		private void MapRoom(int x1, int y1, int x2, int y2)
		{
			for (var y = y1; y <= y2; y++)
				for (var x = x1; x <= x2; x++)
					SetPreDungeon(x, y, '.');
			for (var y = y1; y <= y2; y++) {
				SetPreDungeon(x1, y, '#');
				SetPreDungeon(x2, y, '#');
			}
			for (var x = x1; x <= x2; x++) {
				SetPreDungeon(x, y1, '#');
				SetPreDungeon(x, y2, '#');
			}
		}

		private void SetProtected(int x, int y)
		{
			if ((uint)x < MegaWidth && (uint)y < MegaHeight)
				protectedTiles[At(x, y)] = true;
		}

		private bool IsProtected(int x, int y)
			=> (uint)x < MegaWidth && (uint)y < MegaHeight && protectedTiles[At(x, y)];

		private char GetPreDungeon(int x, int y)
			=> (uint)x < MegaWidth && (uint)y < MegaHeight ? preDungeon[At(x, y)] : ' ';

		private void SetPreDungeon(int x, int y, char value)
		{
			if ((uint)x < MegaWidth && (uint)y < MegaHeight)
				preDungeon[At(x, y)] = value;
		}

		private void FloodTransparencyValues(byte floorId)
		{
			var y = FineOffset;
			for (var megaY = 0; megaY < MegaHeight; megaY++) {
				var x = FineOffset;
				for (var megaX = 0; megaX < MegaWidth; megaX++) {
					if (GetCurrentMega(megaX, megaY) == floorId && GetTransparency(x, y) == 0) {
						FindTransparencyValues(x, y, floorId);
						IncrementTransparencyValue();
					}
					x += 2;
				}
				y += 2;
			}
		}

		private void FindTransparencyValues(int floorX, int floorY, byte floorId)
		{
			var seeds = new Stack<TransparencySeed>();
			seeds.Push(new(floorX, floorX + 1, floorY, 1));
			while (seeds.Count != 0) {
				var seed = seeds.Pop();
				var scanLeft = seed.ScanStart;
				if (IsInside(scanLeft, seed.Y, floorId)) {
					while (IsInside(scanLeft - 1, seed.Y, floorId)) {
						FillTransparency(scanLeft - 1, seed.Y, floorId);
						scanLeft--;
					}
					CheckDiagonals(scanLeft, seed.Y, -1, floorId, seeds);
				}
				if (scanLeft < seed.ScanStart)
					seeds.Push(new(scanLeft, seed.ScanStart - 1, seed.Y - seed.Dy, -seed.Dy));

				var scanRight = seed.ScanStart;
				while (scanRight < seed.ScanEnd) {
					while (IsInside(scanRight, seed.Y, floorId)) {
						FillTransparency(scanRight, seed.Y, floorId);
						scanRight++;
					}
					seeds.Push(new(scanLeft, scanRight - 1, seed.Y + seed.Dy, seed.Dy));
					if (scanRight - 1 > seed.ScanEnd)
						seeds.Push(new(seed.ScanEnd + 1, scanRight - 1, seed.Y - seed.Dy, -seed.Dy));
					if (scanLeft < scanRight)
						CheckDiagonals(scanRight - 1, seed.Y, 1, floorId, seeds);
					while (scanRight < seed.ScanEnd && !IsInside(scanRight, seed.Y, floorId))
						scanRight++;
					scanLeft = scanRight;
					if (scanLeft < seed.ScanEnd)
						CheckDiagonals(scanLeft, seed.Y, -1, floorId, seeds);
				}
			}
		}

		private void CheckDiagonals(int x, int y, int direction, byte floorId, Stack<TransparencySeed> seeds)
		{
			var upY = y - 1;
			var upX = x + direction;
			if (!IsInside(x, upY, floorId) && IsInside(upX, upY, floorId))
				seeds.Push(new(upX, upX + 1, upY, -1));
			var downY = y + 1;
			var downX = x + direction;
			if (!IsInside(x, downY, floorId) && IsInside(downX, downY, floorId))
				seeds.Push(new(downX, downX + 1, downY, 1));
		}

		private bool IsInside(int x, int y, byte floorId)
			=> GetTransparency(x, y) == 0 && IsFloorWorld(x, y, floorId);

		private bool IsFloorWorld(int x, int y, byte floorId)
		{
			var megaX = (x - FineOffset) / 2;
			var megaY = (y - FineOffset) / 2;
			return (uint)megaX < MegaWidth && (uint)megaY < MegaHeight && GetCurrentMega(megaX, megaY) == floorId;
		}

		private void FillTransparency(int x, int y, byte floorId)
		{
			ReadOnlySpan<(int X, int Y)> directions =
			[
				(-1, -1), (1, 1), (1, -1), (-1, 1), (0, -1), (-1, 0), (1, 0), (0, 1),
			];
			foreach (var direction in directions)
				if (!IsFloorWorld(x + direction.X, y + direction.Y, floorId))
					SetTransparency(x + direction.X, y + direction.Y, unchecked((sbyte)nextTransparencyValue));
			SetTransparency(x, y, unchecked((sbyte)nextTransparencyValue));
		}

		private void FixTransparency()
		{
			var y = FineOffset;
			for (var megaY = 0; megaY < MegaHeight; megaY++) {
				var x = FineOffset;
				for (var megaX = 0; megaX < MegaWidth; megaX++) {
					var current = GetCurrentMega(megaX, megaY);
					var value = GetTransparency(x, y);
					if (current == 14 && GetCurrentMega(megaX, megaY - 1) == 10) {
						SetTransparency(x + 1, y, value);
						SetTransparency(x + 1, y + 1, value);
					}
					if (current == 15 && GetCurrentMega(megaX + 1, megaY) == 11) {
						SetTransparency(x, y + 1, value);
						SetTransparency(x + 1, y + 1, value);
					}
					if (current == 10) {
						SetTransparency(x + 1, y, value);
						SetTransparency(x + 1, y + 1, value);
					}
					if (current == 11) {
						SetTransparency(x, y + 1, value);
						SetTransparency(x + 1, y + 1, value);
					}
					if (current == 16) {
						SetTransparency(x + 1, y, value);
						SetTransparency(x, y + 1, value);
						SetTransparency(x + 1, y + 1, value);
					}
					x += 2;
				}
				y += 2;
			}
		}

		private void FixDirtTiles()
		{
			for (var y = 0; y < MegaHeight; y++) {
				for (var x = 0; x < MegaWidth; x++) {
					if (GetCurrentMega(x, y) == 13 && GetCurrentMega(x + 1, y) != 11)
						SetCurrentMega(x, y, 146);
					if (GetCurrentMega(x, y) == 11 && GetCurrentMega(x + 1, y) != 11)
						SetCurrentMega(x, y, 144);
					if (GetCurrentMega(x, y) == 15 && GetCurrentMega(x + 1, y) != 11)
						SetCurrentMega(x, y, 148);
					if (GetCurrentMega(x, y) == 10 && GetCurrentMega(x, y + 1) != 10)
						SetCurrentMega(x, y, 143);
					if (GetCurrentMega(x, y) == 13 && GetCurrentMega(x, y + 1) != 10)
						SetCurrentMega(x, y, 146);
					if (GetCurrentMega(x, y) == 14 && GetCurrentMega(x, y + 1) != 15)
						SetCurrentMega(x, y, 147);
				}
			}
		}

		private void FixLockout()
		{
			for (var y = 0; y < MegaHeight; y++) {
				for (var x = 0; x < MegaWidth; x++) {
					if (GetCurrentMega(x, y) == 4 && GetCurrentMega(x - 1, y) != 3)
						SetCurrentMega(x, y, 1);
					if (GetCurrentMega(x, y) == 5 && GetCurrentMega(x, y - 1) != 3)
						SetCurrentMega(x, y, 2);
				}
			}
			for (var y = 1; y < MegaHeight - 1; y++) {
				for (var x = 1; x < MegaWidth - 1; x++) {
					if (IsProtected(x, y))
						continue;
					if (GetCurrentMega(x, y) is not (2 or 5) || GetCurrentMega(x, y - 1) != 3 || GetCurrentMega(x, y + 1) != 3)
						continue;
					var doorOkay = false;
					while (true) {
						if (GetCurrentMega(x, y) is not (2 or 5)
						    || GetCurrentMega(x, y - 1) != 3
						    || GetCurrentMega(x, y + 1) != 3)
							break;
						if (GetCurrentMega(x, y) == 5)
							doorOkay = true;
						x++;
					}
					if (!doorOkay && !IsProtected(x - 1, y))
						SetCurrentMega(x - 1, y, 5);
				}
			}
			for (var y = 1; y < MegaWidth - 1; y++) {
				for (var x = 1; x < MegaHeight - 1; x++) {
					if (IsProtected(y, x))
						continue;
					if (GetCurrentMega(y, x) is not (1 or 4) || GetCurrentMega(y - 1, x) != 3 || GetCurrentMega(y + 1, x) != 3)
						continue;
					var doorOkay = false;
					while (true) {
						if (GetCurrentMega(y, x) is not (1 or 4)
						    || GetCurrentMega(y - 1, x) != 3
						    || GetCurrentMega(y + 1, x) != 3)
							break;
						if (GetCurrentMega(y, x) == 4)
							doorOkay = true;
						x++;
					}
					if (!doorOkay && !IsProtected(y, x - 1))
						SetCurrentMega(y, x - 1, 4);
				}
			}
		}

		private void FixDoors()
		{
			for (var y = 1; y < MegaHeight; y++) {
				for (var x = 1; x < MegaWidth; x++) {
					if (GetCurrentMega(x, y) == 4 && GetCurrentMega(x, y - 1) == 3)
						SetCurrentMega(x, y, 7);
					if (GetCurrentMega(x, y) == 5 && GetCurrentMega(x - 1, y) == 3)
						SetCurrentMega(x, y, 9);
				}
			}
		}

		private bool PlaceStairs()
		{
			var position = PlaceMiniSet(USTAIRS);
			if (position is null)
				return false;
			stairsUp = position.Value;
			if (entry == AuthoritativeCatacombsEntry.Main)
				viewPosition = new(16 + stairsUp.X * 2 + 5, 16 + stairsUp.Y * 2 + 4);

			position = PlaceMiniSet(DSTAIRS);
			if (position is null)
				return false;
			stairsDown = position.Value;
			if (entry == AuthoritativeCatacombsEntry.Previous)
				viewPosition = new(16 + stairsDown.X * 2 + 4, 16 + stairsDown.Y * 2 + 6);

			if (depth == 5) {
				position = PlaceMiniSet(WARPSTAIRS);
				if (position is null)
					return false;
				townWarpStairs = position.Value;
				if (entry == AuthoritativeCatacombsEntry.TownWarpDown)
					viewPosition = new(16 + position.Value.X * 2 + 5, 16 + position.Value.Y * 2 + 4);
			} else {
				townWarpStairs = null;
			}
			return true;
		}

		private NativePoint? PlaceMiniSet(MiniSet miniset, int tries = 199)
		{
			var position = new NativePoint(
				random.GenerateRnd(MegaWidth - miniset.Width),
				random.GenerateRnd(MegaHeight - miniset.Height));
			var lastX = MegaWidth - miniset.Width;
			var lastY = MegaHeight - miniset.Height;
			for (var attempt = 0; attempt < tries; attempt++) {
				if (position.X == lastX) {
					position = new(0, position.Y + 1);
					if (position.Y == lastY)
						position = new(0, 0);
				}
				if (!setPieceRoom.Contains(position.X, position.Y) && Matches(miniset, position)) {
					Place(miniset, position);
					return position;
				}
				position = position with { X = position.X + 1 };
			}
			return null;
		}

		private bool Matches(MiniSet miniset, NativePoint position)
		{
			for (var y = 0; y < miniset.Height; y++) {
				for (var x = 0; x < miniset.Width; x++) {
					var cellX = position.X + x;
					var cellY = position.Y + y;
					if ((uint)cellX >= MegaWidth || (uint)cellY >= MegaHeight || IsProtected(cellX, cellY))
						return false;
					var required = miniset.Search[y * miniset.Width + x];
					if (required != 0 && GetCurrentMega(cellX, cellY) != required)
						return false;
				}
			}
			return true;
		}

		private void Place(MiniSet miniset, NativePoint position)
		{
			for (var y = 0; y < miniset.Height; y++)
				for (var x = 0; x < miniset.Width; x++) {
					var replacement = miniset.Replace[y * miniset.Width + x];
					if (replacement != 0)
						SetCurrentMega(position.X + x, position.Y + y, replacement);
				}
		}

		private void PlaceDecorationMinisets()
		{
			foreach (var miniset in CreateDecorationMinisets())
				PlaceMiniSetRandom(miniset, 100);
			PlaceMiniSetRandom(CRUSHCOL, 99);
			PlaceMiniSetRandom1x1(1, 80, 10);
			PlaceMiniSetRandom1x1(1, 81, 10);
			PlaceMiniSetRandom1x1(1, 82, 10);
			PlaceMiniSetRandom1x1(2, 84, 10);
			PlaceMiniSetRandom1x1(2, 85, 10);
			PlaceMiniSetRandom1x1(2, 86, 10);
			PlaceMiniSetRandom1x1(8, 87, 50);
			PlaceMiniSetRandom(PANCREAS1, 1);
			PlaceMiniSetRandom(PANCREAS2, 1);
			PlaceMiniSetRandom(BIG1, 3);
			PlaceMiniSetRandom(BIG2, 3);
			PlaceMiniSetRandom(BIG3, 3);
			PlaceMiniSetRandom(BIG4, 3);
			PlaceMiniSetRandom(BIG5, 3);
			PlaceMiniSetRandom(BIG6, 20);
			PlaceMiniSetRandom(BIG7, 20);
			PlaceMiniSetRandom(BIG8, 3);
			PlaceMiniSetRandom(BIG9, 20);
			PlaceMiniSetRandom(BIG10, 20);
		}

		private void PlaceMiniSetRandom(MiniSet miniset, int randomPercent)
		{
			for (var y = 0; y < MegaHeight - miniset.Height; y++) {
				for (var x = 0; x < MegaWidth - miniset.Width; x++) {
					var position = new NativePoint(x, y);
					if (setPieceRoom.Contains(x, y) || !Matches(miniset, position))
						continue;
					var found = true;
					for (var yy = Math.Max(y - miniset.Height, 0); yy < Math.Min(y + 2 * miniset.Height, MegaHeight) && found; yy++) {
						for (var xx = Math.Max(x - miniset.Width, 0); xx < Math.Min(x + 2 * miniset.Width, MegaWidth); xx++) {
							if (GetCurrentMega(xx, yy) == miniset.Replace[0]) {
								found = false;
								break;
							}
						}
					}
					if (!found || random.GenerateRnd(100) >= randomPercent)
						continue;
					Place(miniset, position);
				}
			}
		}

		private void PlaceMiniSetRandom1x1(byte search, byte replacement, int randomPercent)
			=> PlaceMiniSetRandom(new MiniSet(1, 1, [search], [replacement]), randomPercent);

		private void PlaceThemeRooms()
		{
			for (var y = 0; y < MegaHeight; y++) {
				for (var x = 0; x < MegaWidth; x++) {
					if (GetCurrentMega(x, y) != FloorMega || !random.FlipCoin(0))
						continue;
					var size = GetSizeForThemeRoom(new(x, y), FloorMega, 6, 10);
					if (size is null)
						continue;
					var room = new NativeRect(x + 1, y + 1, size.Value.Width, size.Value.Height);
					SetMegaRectangleTransparency(new(room.X, room.Y, room.Width - 1, room.Height - 1));
					var themeTransparency = unchecked((sbyte)(unchecked((sbyte)nextTransparencyValue) - 1));
					CreateThemeRoom(room);
					themes.Add(new AuthoritativeCatacombsThemeRoom(room.X, room.Y, room.Width, room.Height, themeTransparency));
				}
			}
		}

		private NativeSize? GetSizeForThemeRoom(NativePoint origin, byte floor, int minimumSize, int maximumSize)
		{
			if (origin.X + maximumSize > MegaWidth && origin.Y + maximumSize > MegaHeight)
				return null;
			foreach (var theme in themes) {
				if (new NativeRect(theme.X - 2, theme.Y - 2, theme.Width + 5, theme.Height + 5).Contains(origin.X, origin.Y))
					return null;
			}

			var maxWidth = Math.Min(maximumSize, MegaWidth - origin.X);
			var maxHeight = Math.Min(maximumSize, MegaHeight - origin.Y);
			var roomWidth = maxWidth;
			var roomHeight = maxHeight;
			for (var index = 0; index < maximumSize; index++) {
				var width = index < roomHeight ? index : 0;
				if (index < maxHeight) {
					while (width < roomWidth && GetCurrentMega(origin.X + width, origin.Y + index) == floor)
						width++;
				}
				var height = index < roomWidth ? index : 0;
				if (index < maxWidth) {
					while (height < roomHeight && GetCurrentMega(origin.X + index, origin.Y + height) == floor)
						height++;
				}
				if (width < minimumSize || height < minimumSize) {
					if (index < minimumSize)
						return null;
					break;
				}
				roomWidth = Math.Min(roomWidth, width);
				roomHeight = Math.Min(roomHeight, height);
			}
			return new NativeSize(roomWidth - 2, roomHeight - 2);
		}

		private void SetMegaRectangleTransparency(NativeRect area)
		{
			var startX = area.X * 2 + FineOffset + 1;
			var startY = area.Y * 2 + FineOffset + 1;
			var extentX = area.Width * 2 - 1;
			var extentY = area.Height * 2 - 1;
			var value = unchecked((sbyte)nextTransparencyValue);
			for (var y = startY; y <= startY + extentY; y++)
				for (var x = startX; x <= startX + extentX; x++)
					SetTransparency(x, y, value);
			IncrementTransparencyValue();
		}

		private void CreateThemeRoom(NativeRect room)
		{
			var right = room.X + room.Width;
			var bottom = room.Y + room.Height;
			for (var y = room.Y; y < bottom; y++) {
				for (var x = room.X; x < right; x++) {
					var value = y == room.Y || y == bottom - 1 ? (byte)2
					    : x == room.X || x == right - 1 ? (byte)1
					    : (byte)FloorMega;
					SetCurrentMega(x, y, value);
				}
			}
			SetCurrentMega(room.X, room.Y, 8);
			SetCurrentMega(right - 1, room.Y, 7);
			SetCurrentMega(room.X, bottom - 1, 9);
			SetCurrentMega(right - 1, bottom - 1, 6);
			if (random.FlipCoin(2))
				SetCurrentMega(right - 1, (room.Y + bottom) / 2, 4);
			else
				SetCurrentMega((room.X + right) / 2, bottom - 1, 5);
		}

		private void IncrementTransparencyValue()
			=> nextTransparencyValue = unchecked((sbyte)(unchecked((sbyte)nextTransparencyValue) + 1));

		private byte GetCurrentMega(int x, int y)
			=> (uint)x < MegaWidth && (uint)y < MegaHeight ? megaTiles[At(x, y)] : (byte)0;

		private void SetCurrentMega(int x, int y, byte value)
		{
			if ((uint)x < MegaWidth && (uint)y < MegaHeight)
				megaTiles[At(x, y)] = value;
		}

		private sbyte GetTransparency(int x, int y)
			=> (uint)x < FineWidth && (uint)y < FineHeight ? transparency[At(x, y, FineWidth)] : (sbyte)0;

		private void SetTransparency(int x, int y, sbyte value)
		{
			if ((uint)x < FineWidth && (uint)y < FineHeight)
				transparency[At(x, y, FineWidth)] = value;
		}

		private static AuthoritativeCatacombsRectangle ToResultRectangle(NativeRect rectangle)
			=> new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

		private static int At(int x, int y, int width = MegaWidth)
			=> checked(y * width + x);
	}
}
