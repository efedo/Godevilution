using Devilution.Engine;
using Point = Devilution.Diablo.AuthoritativeCathedralPopulationPoint;
using ObjectState = Devilution.Diablo.AuthoritativeCathedralPopulationObjectState;
using MonsterState = Devilution.Diablo.AuthoritativeCathedralPopulationMonsterState;

namespace Devilution.Diablo;

/** Native InitGolems + ordinary Cathedral InitObjects from pre-phase data, never expected-state replay or archives. */
public static class AuthoritativeCathedralPopulationGenerator
{
    public static AuthoritativeCathedralPopulationResult Generate(AuthoritativeCathedralPopulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new Population(parameters).Generate();
    }

    private sealed class Population
    {
        private const int Width = 112, Capacity = 127;
        private const int Light = 0, LeftDoor = 1, RightDoor = 2, Chest1 = 5, Chest2 = 6, Chest3 = 7;
        private const int Sarcophagus = 48, LeftTrap = 53, RightTrap = 54, Barrel = 57, ExplosiveBarrel = 58;
        private const int StoryBook = 86, StoryCandle = 87;
        private const byte Solid = 1, Trap = 128;
        private const uint LockAnimation = 2, AllowSpecial = 4, TargetsMonster = 16;
        private const uint Golem = 32, NoEnemy = 1024, Berserk = 2048;
        private static readonly int[] BarrelDx = [-1, 0, 1, -1, 1, -1, 0, 1];
        private static readonly int[] BarrelDy = [-1, -1, -1, 0, 0, 1, 1, 1];
        private readonly AuthoritativeCathedralPopulationParameters input;
        private readonly DeterministicLegacyRandom random;
        private readonly ObjectState[] objects = Enumerable.Range(0, Capacity).Select(slot => new ObjectState { Slot = slot }).ToArray();
        private readonly int[] active = new int[Capacity];
        private readonly int[] available = Enumerable.Range(0, Capacity).ToArray();
        private readonly int[] objectRefs = new int[Width * Width];
        private readonly List<MonsterState> monsters = [];
        private readonly ushort[] pieces;
        private readonly byte[] special;
        private readonly byte[] transparency;
        private int objectCount;

        public Population(AuthoritativeCathedralPopulationParameters input)
        {
            this.input = input;
            random = new(input.PopulationSeed);
            pieces = input.Geometry.Pieces.ToArray();
            special = input.Geometry.Special.ToArray();
            transparency = input.Geometry.Transparency.ToArray();
        }

        public AuthoritativeCathedralPopulationResult Generate()
        {
            for (var index = 0; index < 4; index++) AddMonster(0, new(1, 0));
            var afterGolems = random.State;
            random.Discard(1);
            if (input.Depth == 4) AddStoryBooks();
            AddRandomObjects(10, 15, Sarcophagus, -1, 1, -2, 1);
            AddCathedralObjects();
            AddBarrelGroups();
            AddRandomObjects(5, 10, Chest1, -1, 1, -1, 1);
            AddRandomObjects(3, 6, Chest2, -1, 1, -1, 1);
            AddRandomObjects(1, 5, Chest3, -1, 1, -1, 1);
            AddObjectTraps();
            return new(objects, objectCount, active, available, objectRefs, monsters.ToArray(),
                pieces, special, transparency, afterGolems, random.State);
        }

        private static int Index(Point position)
        {
            if (!AuthoritativeCathedralPopulationParameters.Contains(position))
                throw new InvalidDataException("Native Cathedral placement escaped the supplied geometry.");
            return position.X + position.Y * Width;
        }
        private bool IsSolid(Point position) => (input.Geometry.PieceProperties[pieces[Index(position)]] & Solid) != 0;
        private bool RandomLocationAllowed(Point position)
        {
            var index = Index(position);
            if (input.Geometry.MonsterOccupied[index] || input.Geometry.PlayerOccupied[index]
                || objectRefs[index] != 0 || input.Geometry.Populated[index] || IsSolid(position)) return false;
            return pieces[index] <= 125 || pieces[index] >= 143;
        }
        private bool AreaAllowed(Point position, int minX, int maxX, int minY, int maxY)
        {
            for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                    if (!RandomLocationAllowed(new(position.X + x, position.Y + y))) return false;
            return true;
        }
        private Point RandomCandidate() => new(random.GenerateRnd(80) + 16, random.GenerateRnd(80) + 16);
        private Point RequirePlacement(Func<Point, bool> predicate)
        {
            for (var attempt = 0; attempt < input.MaximumPlacementAttempts; attempt++) {
                var position = RandomCandidate();
                if (predicate(position)) return position;
            }
            throw new InvalidDataException("Native Cathedral placement exhausted the configured safety bound.");
        }
        private void AddRandomObjects(int minimum, int maximum, int type, int minX, int maxX, int minY, int maxY)
        {
            var count = random.GenerateRnd(maximum - minimum) + minimum;
            for (var index = 0; index < count; index++)
                AddObject(type, RequirePlacement(p => AreaAllowed(p, minX, maxX, minY, maxY)));
        }
        private void AddStoryBooks()
        {
            // Native's legitimate finite bound includes attempt 20000. Failure
            // skips this optional group, unlike exhausted unbounded placement.
            for (var attempt = 0; attempt <= 20000; attempt++) {
                var position = RandomCandidate();
                if (!AreaAllowed(position, -3, 3, -2, 2)) continue;
                AddObject(StoryBook, position);
                foreach (var offset in new Point[] { new(-2, 1), new(-2, 0), new(-1, -1), new(1, -1), new(2, 0), new(2, 1) })
                    AddObject(StoryCandle, new(position.X + offset.X, position.Y + offset.Y));
                return;
            }
        }
        private void AddCathedralObjects()
        {
            for (var y = 0; y < Width; y++) {
                for (var x = 0; x < Width; x++) {
                    var position = new Point(x, y);
                    var piece = pieces[Index(position)];
                    if (piece == 269) AddObject(Light, position);
                    if (piece is 43 or 50 or 213) AddObject(LeftDoor, position);
                    if (piece is 45 or 55) AddObject(RightDoor, position);
                }
            }
        }
        private void AddBarrelGroups()
        {
            var groups = random.GenerateRnd(5) + 3;
            for (var group = 0; group < groups; group++) {
                var position = RequirePlacement(RandomLocationAllowed);
                AddObject(random.FlipCoin(4) ? ExplosiveBarrel : Barrel, position);
                var found = true;
                var chance = 0;
                var count = 1;
                var attempts = 0;
                // FlipCoin is evaluated even after failed direction search.
                while (random.FlipCoin((uint)chance) && found) {
                    if (++attempts > input.MaximumPlacementAttempts)
                        throw new InvalidDataException("Native barrel growth exhausted the configured safety bound.");
                    found = false;
                    for (var trial = 0; trial < 3; trial++) {
                        var direction = random.GenerateRnd(8);
                        if (direction is < 0 or > 7) throw new InvalidDataException("Native barrel direction is not representable.");
                        position = new(position.X + BarrelDx[direction], position.Y + BarrelDy[direction]);
                        found = RandomLocationAllowed(position);
                        if (found) break;
                    }
                    if (found) {
                        AddObject(random.FlipCoin(5) ? ExplosiveBarrel : Barrel, position);
                        count++;
                    }
                    chance = count / 2;
                }
            }
        }

        private int? AddObject(int type, Point position)
        {
            if (objectCount == Capacity) return null;
            var slot = available[0];
            available[0] = available[Capacity - 1 - objectCount];
            active[objectCount] = slot;
            objectRefs[Index(position)] = slot + 1;
            var data = input.Objects[type];
            var animated = (data.Flags & 1) != 0;
            var item = new ObjectState {
                Slot = slot, Type = type, Position = position, AnimFlag = animated ? 1U : 0U,
                AnimDelay = animated ? data.AnimDelay : 1000, AnimCount = animated ? random.GenerateRnd(data.AnimDelay) : 0,
                AnimLength = data.AnimLength,
                AnimFrame = animated ? unchecked((uint)(random.GenerateRnd(data.AnimLength - 1) + 1)) : data.AnimDelay,
                AnimWidth = data.AnimWidth, SolidFlag = (data.Flags & 2) != 0,
                MissilePassable = (data.Flags & 4) != 0, ApplyLighting = (data.Flags & 8) != 0,
                BreakFlag = (data.Flags & 32) != 0 ? 1 : 0, SelectionRegion = data.SelectionRegion,
            };
            switch (type) {
            case LeftDoor:
            case RightDoor: {
                var neighbor = type == LeftDoor ? new Point(position.X, position.Y - 1) : new Point(position.X - 1, position.Y);
                var positionIndex = Index(position);
                var neighborIndex = Index(neighbor);
                item = item with { DoorFlag = true, Var1 = pieces[positionIndex] + 1, Var2 = pieces[neighborIndex] + 1,
                    MissilePassable = false, SelectionRegion = 3 };
                special[positionIndex] = 0;
                pieces[positionIndex] = (ushort)(item.Var1 - 1);
                pieces[neighborIndex] = item.Var2 == 50 && pieces[neighborIndex] == 395
                    ? (ushort)(type == LeftDoor ? 411 : 410) : (ushort)(item.Var2 - 1);
                break;
            }
            case Chest1:
            case Chest2:
            case Chest3:
                if (random.FlipCoin(2)) item = item with { AnimFrame = unchecked(item.AnimFrame + 3) };
                item = item with { RndSeed = random.AdvanceSeed(), Var1 = random.GenerateRnd(type - Chest1 + 2), Var2 = random.GenerateRnd(8) };
                break;
            case Sarcophagus:
                objectRefs[Index(new(position.X, position.Y - 1))] = -(slot + 1);
                item = item with { Var1 = random.GenerateRnd(10), RndSeed = random.AdvanceSeed() };
                if (item.Var1 >= 8) item = item with { Var2 = PreSpawnSkeleton() };
                break;
            case Barrel:
            case ExplosiveBarrel:
                item = item with { RndSeed = random.AdvanceSeed(), Var2 = type == ExplosiveBarrel ? 0 : random.GenerateRnd(10), Var3 = random.GenerateRnd(3) };
                if (item.Var2 >= 8) item = item with { Var4 = PreSpawnSkeleton() };
                break;
            case StoryBook:
                var row = (int)((input.StorySeed >> 16) % 3);
                item = item with { Var1 = row, Var2 = input.StoryMessages[row], Var3 = input.Depth / 4 + 3 * row - 1,
                    AnimFrame = (uint)(5 - 2 * row), Var4 = 6 - 2 * row };
                break;
            case LeftTrap:
            case RightTrap:
                var missile = random.GenerateRnd(input.Depth / 3 + 1);
                item = item with { Var3 = missile switch { 0 => 0, 1 => 1, 2 => 7, _ => 0 } };
                break;
            }
            if (type is Light or StoryCandle) item = item with { Var1 = -1 };
            objects[slot] = item;
            objectCount++;
            return slot;
        }
        private void AddObjectTraps()
        {
            for (var y = 0; y < Width; y++) {
                for (var x = 0; x < Width; x++) {
                    var reference = objectRefs[x + y * Width];
                    if (reference <= 0) continue;
                    var triggerSlot = reference - 1;
                    var trigger = objects[triggerSlot];
                    if (trigger.Type == -1 || random.GenerateRnd(100) >= input.TrapPercent) continue;
                    if ((input.Objects[trigger.Type].Flags & 16) == 0) continue;
                    var left = random.FlipCoin(2);
                    var wall = left ? new Point(x - 1, y) : new Point(x, y - 1);
                    while (!IsSolid(wall)) wall = left ? new(wall.X - 1, wall.Y) : new(wall.X, wall.Y - 1);
                    var wallIndex = Index(wall);
                    if (objectRefs[wallIndex] != 0 || input.Geometry.Populated[wallIndex]
                        || (input.Geometry.PieceProperties[pieces[wallIndex]] & Trap) == 0
                        || (left ? x - wall.X : y - wall.Y) <= 1) continue;
                    var trapSlot = AddObject(left ? LeftTrap : RightTrap, wall);
                    if (trapSlot is null) continue;
                    objects[trapSlot.Value] = objects[trapSlot.Value] with { Var1 = x, Var2 = y };
                    objects[triggerSlot] = trigger with { TrapFlag = true };
                }
            }
        }

        private int AddMonster(int levelType, Point position)
        {
            if (monsters.Count == AuthoritativeCathedralPopulationParameters.MonsterCapacity) return -1;
            var data = input.OrderedBestiary[levelType];
            var tickCounter = unchecked((sbyte)random.GenerateRnd(data.Rates[0] - 1));
            var currentFrame = unchecked((sbyte)random.GenerateRnd(data.Frames[0] - 1));
            // Native RandomIntBetween clamps the rare negative GenerateRnd.
            // Do not change the shared RNG's other established call sites.
            var rawHp = data.MinimumHitPoints + random.RandomIntLessThan(data.MaximumHitPoints - data.MinimumHitPoints + 1);
            var hitPoints = Math.Max((rawHp << 6) / 2, 64);
            var monster = new MonsterState {
                Slot = monsters.Count, Type = data.NativeType, LevelType = levelType, Tile = position, Future = position, Old = position,
                AnimTicksPerFrame = data.Rates[0], AnimNumberOfFrames = data.Frames[0], AnimTickCounter = tickCounter, AnimCurrentFrame = currentFrame,
                HitPoints = hitPoints, MaxHitPoints = hitPoints, Ai = data.Ai, Intelligence = data.Intelligence, Goal = 1,
                UniqueType = 255, LightId = -1, Leader = 255, TalkMessage = -1,
                RndItemSeed = random.AdvanceSeed(), AiSeed = random.AdvanceSeed(),
                MinDamage = data.MinDamage, MaxDamage = data.MaxDamage, MinDamageSpecial = data.MinDamageSpecial, MaxDamageSpecial = data.MaxDamageSpecial,
                ArmorClass = data.ArmorClass, Resistance = data.Resistance, Flags = data.AbilityFlags,
                ReducePlayerStrength = data.ReducePlayerStrength, ReducePlayerMagic = data.ReducePlayerMagic,
                ReducePlayerDexterity = data.ReducePlayerDexterity, ReducePlayerVitality = data.ReducePlayerVitality,
                ReducePlayerMaxHP = data.ReducePlayerMaxHP, ReducePlayerMaxMana = data.ReducePlayerMaxMana,
            };
            if (monster.Ai == 12) monster = monster with {
                AnimTicksPerFrame = data.Rates[5], AnimNumberOfFrames = data.Frames[5], AnimCurrentFrame = 0,
                Flags = monster.Flags | AllowSpecial, Mode = 7,
            };
            monsters.Add(monster);
            return monster.Slot;
        }
        private int PreSpawnSkeleton()
        {
            var candidates = input.OrderedBestiary.Select((data, index) => (data, index))
                .Where(entry => entry.data.IsSkeleton).Select(entry => entry.index).ToArray();
            if (candidates.Length == 0) return -1;
            var selection = random.GenerateRnd(candidates.Length);
            if (selection < 0 || selection >= candidates.Length) throw new InvalidDataException("Native skeleton selection is not representable.");
            var levelType = candidates[selection];
            var slot = AddMonster(levelType, new(0, 0));
            if (slot < 0) return -1;
            var data = input.OrderedBestiary[levelType];
            var monster = monsters[slot];
            // NewMonsterAnim normalizes rate here, unlike changeAnimationData.
            // Fresh AnimationInfo.isPetrified remains invariant false.
            monster = monster with { Var1 = monster.Mode, Var2 = 0, Var3 = 0, Temp = new(0, 0), Mode = 0,
                AnimTicksPerFrame = Math.Max(data.Rates[0], 1), AnimNumberOfFrames = data.Frames[0], AnimCurrentFrame = 0, AnimTickCounter = 0,
                Flags = monster.Flags & ~(LockAnimation | AllowSpecial), Future = monster.Tile, Old = monster.Tile };
            monsters[slot] = UpdateEnemy(monster);
            return slot;
        }
        private static bool IsPlayerMinion(MonsterState monster) => (monster.Flags & Golem) != 0 && (monster.Flags & Berserk) == 0;
        private MonsterState UpdateEnemy(MonsterState monster)
        {
            var enemy = -1;
            var bestDistance = -1;
            var bestSameRoom = false;
            var target = new Point();
            var flags = monster.Flags;
            var minion = IsPlayerMinion(monster);
            bool Prefer(Point tile) {
                var sameRoom = transparency[Index(monster.Tile)] == transparency[Index(tile)];
                var distance = Math.Max(Math.Abs(tile.X - monster.Tile.X), Math.Abs(tile.Y - monster.Tile.Y));
                if (!((sameRoom && !bestSameRoom) || ((sameRoom || !bestSameRoom) && distance < bestDistance) || enemy == -1)) return false;
                bestSameRoom = sameRoom;
                bestDistance = distance;
                return true;
            }
            if (!minion && input.Player.Active && input.Player.Level == input.Depth && !input.Player.LevelChanging && Prefer(input.Player.Tile)) {
                flags &= ~TargetsMonster;
                enemy = input.Player.Id;
                target = input.Player.Future;
            }
            foreach (var other in monsters) {
                if (other.Slot == monster.Slot || other.HitPoints <= 0 || other.Tile == new Point(1, 0) || (minion && IsPlayerMinion(other))) continue;
                var distance = Math.Max(Math.Abs(other.Tile.X - monster.Tile.X), Math.Abs(other.Tile.Y - monster.Tile.Y));
                if ((flags & (Golem | Berserk)) == 0
                    && ((distance >= 2 && monster.Ai is not (3 or 7 or 14 or 29)) || (other.Flags & Golem) == 0)) continue;
                if (!Prefer(other.Tile)) continue;
                flags |= TargetsMonster;
                enemy = other.Slot;
                target = other.Future;
            }
            return enemy == -1 ? monster with { Flags = flags | NoEnemy }
                : monster with { Flags = flags & ~NoEnemy, Enemy = enemy, EnemyPosition = target };
        }
    }
}
