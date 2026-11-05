using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Server.Tests;

/** Archive-free immutable native owners for the projectile-only snapshot restore API. */
internal sealed class NativeTrapRestoreFixture
{
    private const int Cells = 112 * 112;
    private const uint CorpseId = 0x0F001000;
    private static readonly string Fingerprint = new('A', 64);
    private static readonly Lazy<AuthoritativeCathedralPopulationMetadata> Cathedral = new(CreateCathedralMetadata);
    private static readonly Lazy<AuthoritativeCatacombsPopulationMetadata> Catacombs = new(CreateCatacombsMetadata);
    private static readonly Lazy<IReadOnlyDictionary<int, DiabloCathedralItemCheckpoint>> ItemCheckpoints = new(() =>
        Enumerable.Range(1, 8).ToDictionary(depth => depth, depth => {
            var checkpoint = NativeMissileCohortFixture.Create(depth).CathedralItems!;
            return checkpoint with { MetadataFingerprint = Fingerprint, GenerationFingerprint = Fingerprint,
                Levels = checkpoint.Levels.Where(value => value.LevelId == depth).ToArray() };
        }));
    private static readonly FieldInfo PopulationField = typeof(DiabloWorldState)
        .GetField("nativePopulationPolicy", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly Lazy<AuthoritativeCathedralMonsterRuntimeCatalog> MonsterCatalog = new(() =>
        AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(OpenTables().Single(value =>
            value.SourcePath == AuthoritativeCathedralMonsterRuntimeCatalog.TableName)));
    private static readonly AuthoritativeMonsterMissileCatalog RangedMissiles = new([
        new("Bat", "Lightning", AuthoritativeProjectileBehavior.MonsterMagic, MonsterId: 43, SpellId: 3,
            DamageType: AuthoritativeDamageType.Lightning, IsPostMeleeMissile: true),
    ]);
    private static readonly MethodInfo CohortProjectileRestore = typeof(DiabloSimulation)
        .GetMethod("CreateProjectile", BindingFlags.NonPublic | BindingFlags.Static)!;
    private readonly AuthoritativeNativePopulationBindings owner;
    private readonly AuthoritativeCathedralItemRuntimeProfile items;
    private readonly AuthoritativeCathedralMonsterRuntimeAdapterResult actors;
    private readonly HashSet<uint> objectIds;
    private readonly HashSet<uint> projectileSources;
    private readonly uint level;
    private readonly bool hasDoor;
    private DiabloSimulation? simulation;
    private AuthoritativePlayerState? player;

    internal NativeTrapRestoreFixture(int depth, IReadOnlyList<DiabloObjectState> objects,
        IReadOnlyList<DiabloNativeTrapState> traps, int missileType, uint? monsterSource = null, int? intrinsicSlot = null)
    {
        level = (uint)depth;
        hasDoor = objects.Any(value => value.NativeObjectId is 1 or 2 or 42 or 43);
        objectIds = objects.Select(value => value.EntityId).ToHashSet();
        projectileSources = objectIds.Concat(monsterSource is uint id ? new[] { id } : []).ToHashSet();
        var initial = objects.Select(value => new AuthoritativeWorldObject(value.EntityId, value.ObjectId,
            level, value.PositionX, value.PositionY, trapArmed: value.NativeObjectId is >= 68 and <= 70
                || traps.Any(trap => trap.Facts.TriggerEntityId == value.EntityId),
            missilePassable: value.NativeObjectId is 1 or 2 or 42 or 43 ? false : value.MissilePassable,
            nativeObjectId: value.NativeObjectId, hideWhenActivated: false,
            animationFrame: value.NativeObjectId is 1 or 42 ? 1 : value.NativeObjectId is 2 or 43 ? 2 : 0)).ToArray();
        foreach (var value in initial.Where(value => value.NativeObjectId is 1 or 2))
            value.CathedralDoor = new((AuthoritativeCathedralDoorKind)value.NativeObjectId, value.PositionX, value.PositionY,
                AuthoritativeCathedralDoorStatus.Closed, value.NativeObjectId == 1 ? 44 : 46, 1,
                value.AnimationFrame, 3, false, false);
        var slots = Enumerable.Range(0, 127).Select(slot => new AuthoritativeCathedralPopulationObjectState {
            Slot = slot, Type = -1, BookMessage = -1,
        }).ToArray();
        var refs = new int[Cells];
        var pieces = new ushort[Cells];
        var active = new List<int>();
        foreach (var value in initial) {
            var wall = traps.FirstOrDefault(trap => trap.Facts.TrapEntityId == value.EntityId);
            var linked = traps.FirstOrDefault(trap => trap.Facts.TriggerEntityId == value.EntityId);
            var slot = wall?.Facts.TrapObjectSlot ?? linked?.Facts.TriggerObjectSlot
                ?? intrinsicSlot ?? (int)(value.EntityId - 0x09003000U);
            var door = value.NativeObjectId is 1 or 2 or 42 or 43;
            if (door) pieces[value.PositionY * 112 + value.PositionX] = value.NativeObjectId switch {
                1 => 43, 2 => 45, 42 => AuthoritativeCatacombsDoors.ClosedLeftPiece,
                _ => AuthoritativeCatacombsDoors.ClosedRightPiece,
            };
            slots[slot] = slots[slot] with { Type = (int)value.NativeObjectId,
                Position = new(value.PositionX, value.PositionY), TrapFlag = value.TrapArmed,
                MissilePassable = value.MissilePassable, SelectionRegion = 3, DoorFlag = door,
                AnimFrame = (uint)value.AnimationFrame, AnimDelay = door ? 1000 : 0, AnimWidth = (ushort)(door ? 64 : 0),
                Var1 = value.CathedralDoor?.Var1 ?? wall?.Facts.LinkX ?? 0,
                Var2 = value.CathedralDoor?.Var2 ?? wall?.Facts.LinkY ?? 0,
                Var3 = wall?.Facts.MissileTypeId ?? 0, Var4 = value.NativeObjectId is >= 68 and <= 70 && missileType == 27 ? 1 : 0 };
            refs[value.PositionY * 112 + value.PositionX] = slot + 1;
            active.Add(slot);
        }
        var monsters = Enumerable.Range(0, 200).Select(slot => new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot, UniqueType = 255,
        }).ToArray();
        var records = new List<AuthoritativeCathedralMonsterRuntimeRecord>();
        for (var slot = 0; slot < 5; slot++) {
            var golem = slot < 4;
            var position = golem ? new AuthoritativeCathedralPopulationPoint(1, 0) : new(40, 40);
            var raw = monsters[slot] = monsters[slot] with {
                Type = golem ? AuthoritativeCathedralPopulationParameters.GolemNativeType : 0,
                Ai = golem ? 21 : 0, Goal = 1, Tile = position, Future = position, Old = position,
                HitPoints = 64, MaxHitPoints = 64, Leader = 255,
            };
            var binding = new AuthoritativeCathedralMonsterRuntimeBinding(slot, CorpseId - 4 + (uint)slot, 1, level);
            var initialization = new AuthoritativeNativeMonsterInitialization(Fingerprint, raw, 0, 3, 1, 1,
                new(slot, raw.Type, 255, binding.EntityId, 1, level, ""), false) {
                    CatacombsDepth = depth >= 5 ? depth : 0,
                };
            AuthoritativeCombatTarget? target = null;
            if (!golem) {
                target = new(binding.EntityId, 40, 40, 1, maxHitPoints: 1, levelId: level, monsterId: 1, nativeAi: "Zombie");
                target.RestoreNativeInitialization(initialization, 0, 0);
            }
            records.Add(new(binding, raw, initialization, target, golem
                ? AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DormantGolem
                : AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None));
        }
        var metadata = Cathedral.Value;
        var transparency = new sbyte[Cells];
        if (depth <= 4) {
            var coarse = AuthoritativeCathedralDepthGenerator.Generate(depth, 2588, AuthoritativeCathedralEntry.Main,
                AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs);
            var materialized = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(level, coarse,
                metadata.Geometry.Pieces, metadata.Geometry.TileProperties, "restore-fixture").Level;
            transparency = materialized.NativeTransparency.Select(value => (sbyte)value).ToArray();
        }
        var lights = Enumerable.Range(0, 32).Select(slot => new AuthoritativeCathedralEncounterLightState(
            slot, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
        var state = new AuthoritativeCathedralThemeRoomState(slots,
            active.Concat(Enumerable.Range(0, 127).Except(active)).ToArray(),
            Enumerable.Range(0, 127).Except(active).Concat(active).ToArray(), active.Count,
            monsters, Enumerable.Range(0, 200).ToArray(), 5, 5, new int[Cells], new int[Cells],
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, 127).ToArray(), 0, new int[Cells], new HashSet<int>(),
            pieces, transparency, new byte[Cells], new byte[1379], new byte[Cells], refs,
            new byte[Cells], new byte[Cells], Enumerable.Range(0, 32).ToArray(), 0, lights, false, false);
        actors = new(Fingerprint, state.MonsterSlots, state.ActiveMonsterIds, 5, state.MonsterPetrified,
            records, (_, _, _, _) => throw new InvalidOperationException("No skeleton reservations in this fixture."), []);
        var bindings = active.Select(slot => {
            var raw = slots[slot];
            var value = initial.Single(value => value.PositionX == raw.Position.X && value.PositionY == raw.Position.Y);
            return new AuthoritativeCathedralItemObjectBinding(slot, value.EntityId, raw.Type,
                raw.Position.X, raw.Position.Y, false, 3, MutationPolicy: raw.DoorFlag
                    ? depth <= 4 ? AuthoritativeCathedralItemObjectMutationPolicy.CathedralDoor
                        : AuthoritativeCathedralItemObjectMutationPolicy.CatacombsDoor
                    : raw.Type is 5 or 6 or 7 or 68 or 69 or 70
                    ? AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                    : AuthoritativeCathedralItemObjectMutationPolicy.ExplicitlyRefused);
        }).ToArray();
        var checkpoint = ItemCheckpoints.Value[depth];
        AuthoritativeCathedralItemRuntime.ValidateCheckpointShape(checkpoint,
            new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> {
                [level] = depth <= 4 ? AuthoritativeNativeDungeonFamilyContext.Cathedral(depth)
                    : AuthoritativeNativeDungeonFamilyContext.Catacombs(depth),
            });
        items = new(Fingerprint, Fingerprint, checkpoint, metadata.Items.Catalog, new(depth),
            MonsterCatalog.Value,
            metadata.UniqueMonsters.Definitions, new Dictionary<int, AuthoritativeCathedralItemContentBinding>(), new(),
            [new(level, depth, new bool[Cells], new bool[Cells], refs, bindings)]);
        if (depth <= 4) {
            var request = AuthoritativeCathedralFreshLevelGenerator.CreateRequest(metadata, depth, 2588, 0,
                AuthoritativeCathedralEntry.Main, new(6400, 0, 5, 8, new(56, 56), true), "restore-fixture");
            owner = AuthoritativeCathedralNativePopulationPolicy.CreateLevelEntry(request, state, actors, initial, items).Bindings;
        } else {
            var backing = new AuthoritativeLevel(level, 112, 112, nativePieceLayer: state.Pieces,
                nativeSpecialLayer: new ushort[Cells], nativePieceProperties: hasDoor
                    ? Enumerable.Range(0, 1379).ToDictionary(value => (ushort)value, _ => AuthoritativeDunTileFlags.None)
                    : null);
            owner = new(AuthoritativeNativePopulationFamily.Catacombs, depth, state, actors, initial, backing,
                Catacombs.Value.Population.Monsters.Select(value => value.PopulationDefinition).ToArray(), items,
                catacombsDoorCatalogFingerprint: AuthoritativeCatacombsDoorCatalog.Load(Catacombs.Value).Fingerprint);
            owner.BindGenerationFingerprint(Fingerprint);
        }
        items = items with { GenerationFingerprint = owner.Fingerprint,
            InitialCheckpoint = checkpoint with { GenerationFingerprint = owner.Fingerprint } };
    }

    internal DiabloCohortState PrepareDoorCheckpoint(DiabloCohortState checkpoint)
    {
        if (!hasDoor) return checkpoint;
        var objects = checkpoint.Objects.Where(value => objectIds.Contains(value.EntityId)).ToArray();
        var traps = checkpoint.NativeTraps!.Where(value => objectIds.Contains(value.Facts.TrapEntityId)).ToArray();
        if (simulation is null) Initialize(objects, traps, checkpoint.FormatVersion);
        var world = simulation!.WorldState;
        var backing = world.World!.CaptureGeometryState().Levels!.Single();
        var effective = world.CaptureCohortInfrastructure().EffectiveLevels!.Single();
        var geometry = checkpoint.Geometry! with {
            Levels = checkpoint.Geometry!.Levels!.Select(value => value.LevelId == level ? backing : value).ToArray(),
        };
        var levels = checkpoint.WorldInfrastructure.EffectiveLevels!
            .Select(value => value.LevelId == level ? effective : value).ToArray();
        var doors = checkpoint.CathedralDoors! with { BackingGeometry = geometry, EffectiveLevels = levels };
        if (level <= 4) {
            var saved = world.CaptureCathedralDoors()!;
            doors = doors with { CatalogFingerprint = saved.CatalogFingerprint,
                DoorLevels = doors.DoorLevels.Select(value => value.LevelId == level
                    ? saved.DoorLevels.Single() : value).ToArray() };
        }
        return checkpoint with { Geometry = geometry,
            WorldInfrastructure = checkpoint.WorldInfrastructure with { EffectiveLevels = levels },
            CathedralDoors = doors,
            NativeCatacombs = level >= 5 ? checkpoint.NativeCatacombs! with {
                Doors = world.CaptureNativeCatacombsDoorStates().SelectMany(value => value.Doors).ToArray(),
            } : checkpoint.NativeCatacombs,
        };
    }

    private IReadOnlyList<AuthoritativeCatacombsDoorState> OpenCatacombsDoors()
    {
        var levelGeometry = owner.CreateBackingLevel();
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(Catacombs.Value);
        return owner.InitialState.ActiveObjectIds.Take(owner.InitialState.ActiveObjectCount)
            .Select(slot => owner.InitialState.ObjectSlots[slot]).Where(value => value.Type is 42 or 43)
            .Select(raw => {
                var entity = owner.ObjectEntityAt(raw.Position.X, raw.Position.Y)!.Value;
                var closed = new AuthoritativeWorldObject(entity, (uint)raw.Type, level,
                    raw.Position.X, raw.Position.Y, trapArmed: raw.TrapFlag, missilePassable: false,
                    nativeObjectId: (uint)raw.Type, hideWhenActivated: false, animationFrame: (int)raw.AnimFrame);
                var state = AuthoritativeCatacombsDoors.BindRetained(levelGeometry, level, catalog,
                    owner.InitialState, raw, closed);
                return AuthoritativeCatacombsDoors.DeltaOpen(levelGeometry, level, catalog, state).Door;
            }).ToArray();
    }

    internal void Restore(DiabloCohortState checkpoint)
    {
        var objects = checkpoint.Objects.Where(value => objectIds.Contains(value.EntityId)).ToArray();
        var projectiles = checkpoint.Projectiles.Where(value => projectileSources.Contains(value.SourceEntityId)).ToArray();
        var traps = checkpoint.NativeTraps!.Where(value => objectIds.Contains(value.Facts.TrapEntityId)).ToArray();
        if (simulation is null) Initialize(objects, traps, checkpoint.FormatVersion,
            checkpoint.NativeCatacombs?.Doors.Where(value => value.LevelId == level).ToArray());
        var restored = simulation!.WorldState;
        var snapshot = simulation.CaptureSnapshot(player!, checkpoint.Tick) with {
            Player = checkpoint.Participants[0].Player with { PositionX = 100, PositionY = 100 },
            Objects = objects, Projectiles = projectiles,
        };
        if (hasDoor && level <= 4) {
            var saved = checkpoint.CathedralDoors!;
            var subset = saved with {
                BackingGeometry = saved.BackingGeometry with {
                    Levels = saved.BackingGeometry.Levels!.Where(value => value.LevelId == level).ToArray(),
                },
                EffectiveLevels = saved.EffectiveLevels.Where(value => value.LevelId == level).ToArray(),
                DoorLevels = saved.DoorLevels.Where(value => value.LevelId == level).ToArray(),
            };
            snapshot = snapshot with { CathedralDoors = subset,
                CathedralDoorObjects = subset.DoorLevels.SelectMany(value => value.Doors).ToArray() };
        }
        // Exercise the complete-cohort projectile constructor too; it must preserve every extension field.
        foreach (var projectile in projectiles)
            Assert.IsType<AuthoritativeProjectile>(CohortProjectileRestore.Invoke(null, [projectile]));
        simulation.RestoreSnapshot(player!, snapshot);
        owner.ValidateWorld(restored);
        Assert.Equal(traps, restored.CaptureNativeTraps());
        var captured = simulation.CapturePresentationSnapshot(player!, checkpoint.Tick);
        Assert.Equal(objects, captured.Objects);
        Assert.Equal(projectiles, captured.Projectiles);
        if (hasDoor && level <= 4)
            Assert.Equal(snapshot.CathedralDoorObjects, captured.CathedralDoorObjects);
        if (hasDoor && level >= 5)
            Assert.Equal(checkpoint.NativeCatacombs!.Doors.Where(value => value.LevelId == level),
                restored.CaptureNativeCatacombsDoorStates().SelectMany(value => value.Doors));
    }

    private void Initialize(IReadOnlyList<DiabloObjectState> objects, IReadOnlyList<DiabloNativeTrapState> traps, int version, IReadOnlyList<AuthoritativeCatacombsDoorState>? savedDoors = null)
    {
        var world = new AuthoritativeWorld(); world.AddLevel(owner.CreateBackingLevel());
        simulation = DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = world, FallbackWidth = 112, FallbackHeight = 112, MonsterMissiles = RangedMissiles,
            CathedralDoors = level <= 4 ? new AuthoritativeCathedralDoorCatalog([new(level, 1, 2)]) : null,
            Objects = objects.Where(value => value.NativeObjectId is not (1 or 2)).Select(value => new AuthoritativeWorldObject(value.EntityId, value.ObjectId,
                level, value.PositionX, value.PositionY, activated: value.Activated,
                missilePassable: value.MissilePassable, nativeObjectId: value.NativeObjectId,
                hideWhenActivated: false, animationFrame: value.AnimationFrame)).ToArray(),
            // Native actors are dead, so the legacy projection API has no native actor to erase.
            Corpses = [new(CorpseId, level, 40, 40)],
        });
        var restored = simulation.WorldState;
        if (level >= 5) {
            // Supply the trusted constructor input before running the real C2 rehydrators;
            // all population/object/lifecycle validation below remains enabled.
            PopulationField.SetValue(restored, owner);
            restored.ConfigureNativeCatacombsDoors(owner, AuthoritativeCatacombsDoorCatalog.Load(Catacombs.Value),
                savedDoors ?? OpenCatacombsDoors());
            restored.ConfigureNativeCatacombsMonsterPhases(new((int)level, level, actors, []), []);
            restored.ConfigureNativeCatacombsRangedActions(owner, RangedMissiles, []);
            restored.RestoreNativeCatacombsArrows(owner, []);
        }
        foreach (var door in objects.Where(value => value.NativeObjectId is 1 or 2))
            Assert.True(restored.TryOperateCathedralDoor(door.EntityId, []));
        restored.ConfigureNativePopulationBindings(owner, items, Fingerprint, version, [], [], traps);
        player = new AuthoritativePlayerState(0, 0, 6400, 0, 0, PlayerAttributesState.Zero,
            [], [], [], 100, 100, 6400, 1, level, 0) { EntityId = 1 };
    }

    private static IReadOnlyList<TsvTable> OpenTables() => Directory.GetFiles(
        Path.Combine(RepositoryRoot(), "test", "fixtures", "native-open-metadata"), "*.tsv")
        .Select(path => TsvTable.Parse(Path.GetFileName(path), File.ReadAllText(path))).ToArray();

    private static AuthoritativeNativeDunGeometryCatalog Geometry(int count) => new(
        new AuthoritativeDunPieceCatalog(Enumerable.Range(0, count).Select(value => new AuthoritativeDunPieceDefinition((uint)value, 0, 1, 2, 3))),
        new AuthoritativeDunTilePropertyCatalog(Enumerable.Range(0, 1379).Select(value => new AuthoritativeDunTileProperty((uint)value, AuthoritativeDunTileFlags.None))));

    private static AuthoritativeCathedralPopulationMetadata CreateCathedralMetadata()
    {
        var tables = OpenTables().Where(value => value.SourcePath != AuthoritativeCathedralMonsterRuntimeCatalog.TableName).ToList();
        tables.Add(TsvTable.Parse(AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
            + string.Concat(Enumerable.Range(0, 206).Select(value => $"{value}\t0\t1\t2\t3\n"))));
        tables.Add(TsvTable.Parse(AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n"
            + string.Concat(Enumerable.Range(0, 1379).Select(value => $"{value}\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\n"))));
        tables.Add(TsvTable.Parse(AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName,
            "piece_id\teffective_flags\n" + string.Concat(Enumerable.Range(0, 1379).Select(value => $"{value}\t0\n"))));
        return AuthoritativeCathedralPopulationMetadata.Load(new("synthetic-projectile-restore", "1.0.0", tables,
            schema: new(AuthoritativeCathedralPopulationMetadata.SchemaId, AuthoritativeCathedralPopulationMetadata.SchemaVersion)));
    }

    private static AuthoritativeCatacombsPopulationMetadata CreateCatacombsMetadata()
    {
        var source = Cathedral.Value;
        var geometry = Geometry(AuthoritativeCatacombsPopulationMetadata.NativeTilRecordCount);
        var til = new byte[AuthoritativeCatacombsPopulationMetadata.NativeTilRecordCount * 8];
        for (var index = 0; index < til.Length / 2; index++) BinaryPrimitives.WriteUInt16LittleEndian(til.AsSpan(index * 2), (ushort)(index % 4));
        var text = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "assets", "txtdata", "text", "textdat.tsv"));
        var rows = AuthoritativeCatacombsPopulationMetadata.CreateTables(geometry, new byte[1379],
            Convert.ToHexString(SHA256.HashData(til)).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(new byte[AuthoritativeCatacombsPopulationMetadata.NativeSolSourceByteCount])).ToLowerInvariant(),
            source.Population.Monsters, source.Population.Objects, source.UniqueMonsters.Definitions, source.Items.Catalog,
            Enumerable.Range(5, 4).Select(depth => new AuthoritativeCatacombsPopulationDepthRule(depth, 30, 150, depth < 7 ? 20 : 25, 190)).ToArray(),
            [250, 253, 256], text.Length, Convert.ToHexString(SHA256.HashData(text)).ToLowerInvariant(), source.ShrineAvailable);
        return AuthoritativeCatacombsPopulationMetadata.Load(new(AuthoritativeCatacombsPopulationMetadata.CatalogId, "1.0.0", rows,
            schema: new(AuthoritativeCatacombsPopulationMetadata.SchemaId, AuthoritativeCatacombsPopulationMetadata.SchemaVersion)));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "assets", "txtdata"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
