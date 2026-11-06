using System.Collections.Concurrent;
using System.Reflection;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Behavior = Devilution.Diablo.AuthoritativeProjectileBehavior;

namespace Devilution.Server.Tests;

public sealed partial class NativeTrapHostPublicationTests
{
    /** All ticks still cross the live host; checkpoint work uses observable boundary classes. */
    [Fact]
    [Trait("Category", "Slow")]
    public async Task EveryNativeMissileBoundaryCrossesHostClientAndCheckpointForItsEntireLife()
    {
        foreach (var type in new[] { 5, 6, 7, 68, 69, 70 })
            AssertIntrinsicFireArrowOwnerAdmission(type, type >= 68);
        await RunNativeMissileMatrix(exhaustive: false);
    }

    /** Native AddTrap/OperateChest, PutMissile and all four process functions bound this finite matrix. */
    [Fact(Explicit = true)]
    public Task EveryNativeMissileCartesianCombinationCrossesHostClientAndCheckpoint()
        => RunNativeMissileMatrix(exhaustive: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Slow")]
    public Task DoorTriggeredMissilesUseBothMatrixRestoreLegs(bool exhaustive)
        => RunNativeMissileMatrix(exhaustive, doorsOnly: true);

    private async Task RunNativeMissileMatrix(bool exhaustive, bool doorsOnly = false)
    {
        var missileMappings = new AuthoritativeMonsterMissileCatalog([
            new("Bat", "Lightning", Behavior.MonsterMagic, MonsterId: 43, SpellId: 3, DamageType: AuthoritativeDamageType.Lightning,
                ProjectileSpeed: 16, ProjectileLifetime: 2, ProjectileImpactLifetime: 8,
                RangedDelayRandomRange: 20, ProjectileDamageRandomRange: 10, IsPostMeleeMissile: true),
        ]);
        var cases = 0;
        var snapshots = 0;
        var restores = 0;
        var stages = new ConcurrentDictionary<NativeObjectMissileStage, byte>();
        var restoredClasses = new ConcurrentDictionary<NativeBoundaryClass, byte>();
        var checkpoints = 0;
        var nativeKinds = new ConcurrentDictionary<uint, byte>();
        var doorRestores = new ConcurrentDictionary<(int Trigger, int Missile), byte>();
        var templates = new ConcurrentDictionary<int, DiabloCohortState>();
        var sharedFamiliarRestore = doorsOnly ? null : new NativeTrapRestoreFixture(7, [], [], -1, 151392341);
        if (!doorsOnly) {
            // The exact failed value is first, so this mandatory test fails on the old mapper guard.
            await Run([BoundaryMissile.Familiar()], 7);
            foreach (var edge in new[] { (111, 0), (0, 111), (111, 111) })
                await Run([BoundaryMissile.Familiar(edge.Item1, edge.Item2)], 7);
            if (!exhaustive)
                foreach (var damage in new[] { 1, 10 })
                    foreach (var cell in new[] { (51, 53), (111, 0), (0, 111), (111, 111) })
                        await Run([BoundaryMissile.Familiar(cell.Item1, cell.Item2, damage)], 7);
        }
        await Parallel.ForEachAsync(DoorBoundarySpecs(), new ParallelOptions {
            MaxDegreeOfParallelism = 4, CancellationToken = TestContext.Current.CancellationToken,
        }, async (value, _) => {
            await Run([new BoundaryMissile(value.Spec, value.Depth, 0)], value.Depth);
        });
        foreach (var trigger in new[] { 1, 2, 42, 43 })
            foreach (var type in new[] { 0, 1, 7 })
                Assert.Contains((trigger, type), doorRestores.Keys);

        var selected = doorsOnly ? Array.Empty<LeveledBoundary>() : exhaustive
            ? Enumerable.Range(1, 8).SelectMany(depth => BoundarySpecs(depth).Select(spec => new LeveledBoundary(depth, spec))).ToArray()
            : DistinctBoundarySpecs().ToArray();
        var work = selected.GroupBy(value => value.Depth).OrderBy(value => value.Key)
            .SelectMany(level => (exhaustive ? BoundaryBatches(level.Select(value => value.Spec))
                    : PackedBoundaryBatches(level.Select(value => value.Spec)))
                .Select(specs => (Depth: level.Key, Specs: specs)));
        await Parallel.ForEachAsync(work, new ParallelOptions {
            MaxDegreeOfParallelism = 4, CancellationToken = TestContext.Current.CancellationToken,
        }, async (chunk, _) => {
            var slot = 0;
            var missiles = chunk.Specs.Select((spec, index) => {
                var nativeSlot = exhaustive ? index * 2 : slot;
                slot += spec.Intrinsic ? 1 : 2;
                return new BoundaryMissile(spec, chunk.Depth, index, nativeSlot);
            }).ToArray();
            await Run(missiles, chunk.Depth);
        });
        if (!doorsOnly) {
            Assert.Contains(NativeObjectMissileStage.Flight, stages.Keys);
            Assert.Contains(NativeObjectMissileStage.ElementalImpact, stages.Keys);
            Assert.Contains(NativeObjectMissileStage.Explosion, stages.Keys);
            Assert.Contains(NativeObjectMissileStage.LightningSegment, stages.Keys);
            Assert.True(restores > 0);
            if (exhaustive)
                Assert.True(restores >= cases * 2, "Every lifecycle must actually restore its initial and final state.");
            else {
                AssertBoundaryClassCoverage(restoredClasses.Keys);
                foreach (var kind in new uint[] { 53, 54, 68, 69, 70 })
                    Assert.Contains(kind, nativeKinds.Keys);
            }
        }
        output.WriteLine($"Checkpoint round trips: {checkpoints}; distinct restore classes: {restoredClasses.Count}.");
        output.WriteLine($"Actual snapshot restores with native-owner rehydration: {restores}.");
        output.WriteLine($"Complete matrix: {cases} lifecycles; {snapshots} ticks; native durations bound every case.");

        async Task Run(BoundaryMissile[] missiles, int depth)
        {
            var template = templates.GetOrAdd(depth, NativeMissileCohortFixture.Create);
            var codec = new DiabloCohortCodec();
            var decodedTicks = new Dictionary<ulong, DiabloCohortState>();
            var geometry = new[] { new AuthoritativeLevel((uint)depth, 112, 112) };
            var objects = missiles.SelectMany(value => value.Objects).OrderBy(value => value.EntityId).ToArray();
            var owners = missiles.SelectMany(value => value.Owners).OrderBy(value => value.Facts.TrapObjectSlot).ToArray();
            var projection = DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
                FallbackWidth = 112, FallbackHeight = 112, MonsterMissiles = missileMappings,
                Objects = objects.Select(value => new AuthoritativeWorldObject(value.EntityId, value.ObjectId,
                    value.LevelId, value.PositionX, value.PositionY, activated: value.Activated,
                    missilePassable: value.MissilePassable, nativeObjectId: value.NativeObjectId,
                    hideWhenActivated: value.HideWhenActivated)).ToArray(),
            });
            foreach (var missile in missiles) {
                if (sharedFamiliarRestore is not null) missile.ShareFamiliarRestore(sharedFamiliarRestore);
                if (exhaustive) missile.PrepareCheckpointRestore(depth);
            }
            var observer = new AuthoritativePlayerState(0, 0, 6400, 0, 0, PlayerAttributesState.Zero,
                [], [], [], 56, 56, 6400, 1, (uint)depth, 0) { EntityId = 1 };
            Snapshot Build(ulong tick)
            {
                projection.WorldState.Projectiles.Clear();
                foreach (var live in missiles.SelectMany(value => value.World.Projectiles.Values))
                    projection.WorldState.Projectiles.Add(live.EntityId, live);
                var source = projection.CapturePresentationSnapshot(observer, tick);
                var projectiles = source.Projectiles;
                foreach (var value in objects) nativeKinds.TryAdd(value.NativeObjectId, 0);
                foreach (var projectile in projectiles) {
                    if (projectile.NativeTrapArrow is { } trap) {
                        trap.ValidateShape(projectile);
                        stages.TryAdd(trap.Stage, 0);
                    }
                    else {
                        Assert.Equal(0U, projectile.SpellId);
                        Assert.Equal("Lightning", projectile.NativeMissileId);
                        projectile.NativeMonsterMissile!.ValidateFamiliarLightningShape(projectile);
                    }
                }
                var captured = template with { Tick = tick, Objects = objects, NativeTraps = owners, Projectiles = projectiles };
                foreach (var missile in missiles)
                    captured = missile.PrepareDoorCheckpoint(captured, depth);
                if (exhaustive) {
                    var decoded = RoundTrip(captured);
                    foreach (var missile in missiles) {
                        var count = missile.RestoreCheckpointBoundaries(decoded);
                        Interlocked.Add(ref restores, count);
                        if (count != 0) missile.RecordDoorRestore(doorRestores);
                    }
                } else {
                    var pending = new Dictionary<DiabloCohortState, HashSet<BoundaryMissile>>(ReferenceEqualityComparer.Instance);
                    foreach (var missile in missiles) {
                        foreach (var boundary in missile.CheckpointBoundaries(captured)) {
                            var key = missile.BoundaryClass(boundary.Projectile, boundary.Role, depth);
                            if (!restoredClasses.TryAdd(key, 0)) continue;
                            if (!pending.TryGetValue(boundary.Checkpoint, out var consumers))
                                pending.Add(boundary.Checkpoint, consumers = []);
                            consumers.Add(missile);
                        }
                    }
                    foreach (var work in pending.OrderBy(value => value.Key.Tick)) {
                        var decoded = RoundTrip(work.Key);
                        foreach (var missile in work.Value) {
                            missile.RestoreCheckpoint(decoded, depth);
                            missile.RecordDoorRestore(doorRestores);
                            Interlocked.Increment(ref restores);
                        }
                    }
                }
                var projected = DiabloProtocolMapper.ToProtocolSnapshot(source, levelGeometry: geometry, monsterMissiles: missileMappings);
                foreach (var projectile in projected.Projectiles) {
                    Assert.True(Enum.IsDefined(ProjectilePresentationStyle.Resolve(projectile)));
                    if (projectile.NativeMissileId == "Lightning" && projectile.Behavior == (int)Behavior.MonsterMagic)
                        Assert.Equal(3U, projectile.SpellId);
                }
                Interlocked.Increment(ref snapshots);
                return projected;
            }
            DiabloCohortState RoundTrip(DiabloCohortState captured)
            {
                // Only Advance mutates this fixture. Repeated requests for one tick have identical bytes.
                if (!exhaustive && decodedTicks.TryGetValue(captured.Tick, out var cached)) {
                    Assert.Equal(captured.Objects, cached.Objects);
                    Assert.Equal(captured.NativeTraps, cached.NativeTraps);
                    Assert.Equal(captured.Projectiles, cached.Projectiles);
                    return cached;
                }
                var bytes = codec.Serialize(captured);
                var decoded = codec.Deserialize(bytes);
                Assert.Equal(bytes, codec.Serialize(decoded));
                Assert.Equal(captured.Projectiles, decoded.Projectiles);
                Interlocked.Increment(ref checkpoints);
                if (!exhaustive) {
                    foreach (var tick in decodedTicks.Keys.Where(tick => tick + 1 < captured.Tick).ToArray())
                        decodedTicks.Remove(tick);
                    decodedTicks[captured.Tick] = decoded;
                }
                return decoded;
            }
            void Advance(ulong tick) { foreach (var missile in missiles) missile.AdvanceTo(tick, depth); }
            await using var host = new SnapshotHost(0, Build, Advance);
            await host.ConnectAsync();
            // ProcessGenericProjectile may expire on tick 256 and append a ten-frame explosion.
            for (ulong tick = 1; tick <= 256 + 10; tick++) {
                var snapshot = await host.PollAsync(tick);
                Assert.InRange(snapshot.Projectiles.Count, 0, 125);
                Assert.Equal(tick, host.Model.Snapshot!.Tick);
                if (snapshot.Projectiles.Count == 0) break;
            }
            Assert.All(missiles, value => Assert.Empty(value.World.Projectiles));
            Assert.Null(host.Server.LastClientFailure);
            Assert.False(host.Server.Completion.IsCompleted);
            Interlocked.Add(ref cases, missiles.Length);
        }
    }

    /** Batch independent lifecycles within native object (127) and missile (125) pool bounds. */
    private static IEnumerable<BoundarySpec[]> BoundaryBatches(IEnumerable<BoundarySpec> specs)
    {
        var lightning = new Queue<BoundarySpec>(specs.Where(value => value.Type == 7));
        var other = new Queue<BoundarySpec>(specs.Where(value => value.Type != 7));
        while (lightning.Count != 0 || other.Count != 0) {
            var batch = new List<BoundarySpec>();
            while (batch.Count < 8 && lightning.TryDequeue(out var next)) batch.Add(next);
            while (batch.Count < 56 && other.TryDequeue(out var next)) batch.Add(next);
            yield return batch.ToArray();
        }
    }

    private sealed record BoundarySpec(int Type, bool Intrinsic, int X, int Y, int TargetX, int TargetY, bool Blocked, uint Seed, int TriggerType = 0);

    private static IEnumerable<BoundarySpec> BoundarySpecs(int depth)
    {
        // Eight grid octants plus their 2:1 slopes exercise all native 16-way visual sectors.
        (int X, int Y)[] aims = [(1, 0), (2, 1), (1, 1), (1, 2), (0, 1), (-1, 2), (-1, 1), (-2, 1),
            (-1, 0), (-2, -1), (-1, -1), (-1, -2), (0, -1), (1, -2), (1, -1), (2, -1)];
        var types = new List<(int Type, bool Intrinsic)> { (0, false) };
        if (depth >= 3) types.Add((1, false));
        if (depth >= 6) types.Add((7, false));
        if (depth >= 5) { types.Add((0, true)); types.Add((27, true)); }
        foreach (var (type, intrinsic) in types) {
            // Wall sources use the complete dungeon bounds; native random chests occupy 16..95.
            var endpoints = intrinsic ? new[] { 16, 56, 95 } : new[] { 0, 56, 111 };
            foreach (var x in endpoints) foreach (var y in endpoints) foreach (var aim in aims) {
                var tx = Math.Clamp(x + 16 * aim.X, 0, 111);
                var ty = Math.Clamp(y + 16 * aim.Y, 0, 111);
                if (tx == x && ty == y) continue;
                // Both collision/expiry stages and complete unobstructed flights to the map edge.
                yield return new(type, intrinsic, x, y, tx, ty, false, 2588);
                yield return new(type, intrinsic, x, y, tx, ty, true, 2588);
            }
            // Exercise the equal-source/target nudge while keeping native chest placement bounds.
            if (intrinsic) yield return new(type, true, 95, 95, 95, 95, true, 2588);
            // Firebolt's stored damage reaches both native L1-L8 payload extremes.
            if (type == 1) foreach (var roll in new[] { 0, 2 * depth - 1 }) {
                var seed = Enumerable.Range(0, 65536).Select(value => (uint)value)
                    .First(value => new DeterministicLegacyRandom(value).GenerateRnd(2 * depth) == roll);
                yield return new(type, false, 0, 0, 111, 111, false, seed);
            }
        }
    }

    private sealed class BoundaryMissile
    {
        private static readonly MethodInfo ProcessMonster = typeof(DiabloCombatCommandProcessor)
            .GetMethod("AdvanceNativeMonsterMissile", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly DiabloCombatCommandProcessor processor;
        private readonly AuthoritativePlayerState player;
        private ulong currentTick;
        private readonly int missileType;
        private readonly uint sourceEntityId;
        private readonly int nativeObjectSlot;
        private NativeTrapRestoreFixture? restoreFixture;
        private DiabloCohortState? previousCheckpoint;
        private DiabloProjectileState[] previousProjectiles = [];
        private ulong? restoredTick;
        public DiabloWorldState World { get; }
        public IReadOnlyList<DiabloObjectState> Objects { get; }
        public IReadOnlyList<DiabloNativeTrapState> Owners { get; }

        public BoundaryMissile(BoundarySpec spec, int depth, int index, int? objectSlot = null)
        {
            missileType = spec.Type;
            nativeObjectSlot = objectSlot ?? index * 2;
            var direction = (int)DeterministicGridDirections.Toward(spec.X, spec.Y, spec.TargetX, spec.TargetY);
            var velocity = DiabloCombatCommandProcessor.NativeObjectMissileVelocity(spec.X, spec.Y, spec.TargetX, spec.TargetY,
                spec.Type == 1 ? 26 : 32);
            var obstacle = DiabloCombatCommandProcessor.NativeTrapArrowTile(spec.X, spec.Y, velocity.X * 4, velocity.Y * 4);
            World = new(fallbackWidth: 112, fallbackHeight: 112,
                fallbackMissileBlockedCells: spec.Blocked && (uint)obstacle.X < 112 && (uint)obstacle.Y < 112
                    ? [obstacle.Y * 112 + obstacle.X] : []);
            var random = new DeterministicLegacyRandom(spec.Seed);
            player = Player(depth);
            processor = new(World, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]), new AuthoritativeCombatRules(10, 1, 100), random);
            var id = 0x80000000U + (uint)index * 2048;
            processor.RestoreCohortAllocators(new(0x40000000, 0x10000000, id + 1));
            var source = sourceEntityId = 0x09003000U + (uint)index * 2;
            var trigger = spec.Intrinsic ? source : spec.TriggerType is 1 or 2
                ? DiabloWorldState.GetCathedralDoorEntityId((uint)depth, 112, spec.TargetX, spec.TargetY) : source + 1;
            var sourceType = spec.Intrinsic ? 68 + index % 3 : 53 + index % 2;
            // Door cases use interior, native-valid frames; the range matrix also retains edge-linked chests.
            var triggerType = spec.TriggerType != 0 ? spec.TriggerType : 5 + index % 3;
            var missile = DiabloCombatCommandProcessor.AddObjectTrapMissile(id, source, trigger, (uint)depth,
                nativeObjectSlot, spec.X, spec.Y, spec.TargetX, spec.TargetY, direction, spec.Type, spec.Intrinsic, random, depth);
            World.Projectiles.Add(id, missile);
            var objects = new List<DiabloObjectState> { Object(source, (uint)sourceType, depth, spec.X, spec.Y, spec.Intrinsic) };
            if (!spec.Intrinsic) {
                var isDoor = triggerType is 1 or 2 or 42 or 43;
                objects.Add(Object(trigger, (uint)triggerType, depth, missile.TargetX, missile.TargetY, !isDoor) with {
                    AnimationFrame = isDoor ? triggerType is 1 or 42 ? 3 : 4 : 2,
                });
            }
            Objects = objects;
            Owners = spec.Intrinsic ? [] : [new(new(nativeObjectSlot, source, sourceType, (uint)depth, 0,
                spec.X, spec.Y, 0, missile.TargetX, missile.TargetY, spec.Type, 0, nativeObjectSlot + 1, trigger, triggerType,
                missile.TargetX, missile.TargetY, true), 1, false, DiabloNativeTrapDisposition.Fired)];
        }

        private BoundaryMissile(int x, int y, int damage)
        {
            missileType = -1;
            sourceEntityId = 151392341;
            World = new(fallbackWidth: 112, fallbackHeight: 112);
            player = Player(7);
            processor = new(World, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
                new AuthoritativeCombatRules(10, 1, 100), new DeterministicLegacyRandom(2588));
            var state = new AuthoritativeNativeMonsterMissileState(AuthoritativeNativeMonsterMissileKind.Lightning,
                0, 0, false, 50, 53, 13, 50, 4, 16, 3, false, false);
            // The live failure: entity 2147483667, SpellId 0, damage 9, duration 4 (halfway through eight ticks), L7 at (51,53).
            var missile = new AuthoritativeProjectile(2147483667, 151392341, 1, 0, 7, x, y, x + 1, y + 1,
                damage, AuthoritativeDamageType.Lightning, 0, 8, allowZeroSpellId: true) {
                Behavior = Behavior.MonsterMagic, NativeMissileId = "Lightning", StartX = x + 1, StartY = y + 1,
                ImpactLifetime = 0, NativeMonsterMissile = state,
            };
            World.Projectiles.Add(missile.EntityId, missile);
            Objects = []; Owners = [];
        }

        public void ShareFamiliarRestore(NativeTrapRestoreFixture shared)
        {
            if (missileType == -1) restoreFixture = shared;
        }

        public void PrepareCheckpointRestore(int depth)
            => restoreFixture ??= new(depth, Objects, Owners, missileType,
                missileType == -1 ? sourceEntityId : null, nativeObjectSlot);

        public DiabloCohortState PrepareDoorCheckpoint(DiabloCohortState checkpoint, int depth)
        {
            if (!Objects.Any(value => value.NativeObjectId is 1 or 2 or 42 or 43)) return checkpoint;
            if (restoreFixture is null) PrepareCheckpointRestore(depth);
            return restoreFixture!.PrepareDoorCheckpoint(checkpoint);
        }

        public void RecordDoorRestore(ConcurrentDictionary<(int Trigger, int Missile), byte> coverage)
        {
            if (Objects.FirstOrDefault(value => value.NativeObjectId is 1 or 2 or 42 or 43) is { } door)
                coverage.TryAdd(((int)door.NativeObjectId, missileType), 0);
        }

        public void RestoreCheckpoint(DiabloCohortState checkpoint, int depth)
        {
            if (restoreFixture is null) PrepareCheckpointRestore(depth);
            restoreFixture!.Restore(checkpoint);
        }

        public IEnumerable<NativeBoundary> CheckpointBoundaries(DiabloCohortState checkpoint)
        {
            var current = checkpoint.Projectiles.Where(value => value.SourceEntityId == sourceEntityId).ToArray();
            var previousById = previousProjectiles.ToDictionary(value => value.EntityId);
            var currentById = current.ToDictionary(value => value.EntityId);
            foreach (var projectile in current) {
                if (!previousById.TryGetValue(projectile.EntityId, out var old))
                    yield return new(checkpoint, projectile, NativeBoundaryRole.Birth);
                else if (old.Phase != projectile.Phase || old.NativeTrapArrow?.Stage != projectile.NativeTrapArrow?.Stage
                    || old.NativeMissileId != projectile.NativeMissileId) {
                    yield return new(previousCheckpoint!, old, NativeBoundaryRole.PhaseEnd);
                    yield return new(checkpoint, projectile, NativeBoundaryRole.PhaseStart);
                }
            }
            foreach (var old in previousProjectiles.Where(value => !currentById.ContainsKey(value.EntityId)))
                yield return new(previousCheckpoint!, old, NativeBoundaryRole.Final);
            previousCheckpoint = checkpoint;
            previousProjectiles = current;
        }

        public NativeBoundaryClass BoundaryClass(DiabloProjectileState projectile, NativeBoundaryRole role, int depth)
        {
            var source = Objects.FirstOrDefault(value => value.EntityId == sourceEntityId);
            var trigger = Objects.FirstOrDefault(value => value.EntityId == projectile.TargetEntityId);
            var startMin = source?.NativeObjectId is >= 68 and <= 70 ? 16 : 0;
            var startMax = source?.NativeObjectId is >= 68 and <= 70 ? 95 : 111;
            return new(missileType, projectile.NativeTrapArrow?.Stage ?? NativeObjectMissileStage.Flight, role,
                depth <= 4 ? 1 : 2, OwnerClass(source?.NativeObjectId ?? 0), OwnerClass(trigger?.NativeObjectId ?? 0),
                PositionClass(projectile.StartX, projectile.StartY, startMin, startMax),
                PositionClass(projectile.PositionX, projectile.PositionY, 0, 111),
                PositionClass(projectile.TargetX, projectile.TargetY, 0, 111),
                DirectionClass(projectile), DamageClass(missileType, projectile.Damage));
        }

        /** Restore each projectile's birth, both sides of a phase change, and its final retained tick. */
        public int RestoreCheckpointBoundaries(DiabloCohortState checkpoint)
        {
            var current = checkpoint.Projectiles.Where(value => value.SourceEntityId == sourceEntityId).ToArray();
            var previousById = previousProjectiles.ToDictionary(value => value.EntityId);
            var currentById = current.ToDictionary(value => value.EntityId);
            var boundary = previousCheckpoint is null
                || current.Any(value => !previousById.TryGetValue(value.EntityId, out var old)
                    || old.Phase != value.Phase || old.NativeTrapArrow?.Stage != value.NativeTrapArrow?.Stage
                    || old.NativeMissileId != value.NativeMissileId)
                || previousProjectiles.Any(value => !currentById.ContainsKey(value.EntityId));
            var restored = 0;
            if (boundary) {
                if (previousCheckpoint is not null && restoredTick != previousCheckpoint.Tick) {
                    restoreFixture!.Restore(previousCheckpoint);
                    restored++;
                }
                restoreFixture!.Restore(checkpoint);
                restoredTick = checkpoint.Tick;
                restored++;
            }
            previousCheckpoint = checkpoint;
            previousProjectiles = current;
            return restored;
        }

        public static BoundaryMissile Familiar(int x = 51, int y = 53, int damage = 9) => new(x, y, damage);
        private static AuthoritativePlayerState Player(int depth) => new(0, 0, 6400, 0, 0,
            PlayerAttributesState.Zero, [], [], [], 56, 56, 6400, 1, (uint)depth, 0) { EntityId = 1 };
        private static DiabloObjectState Object(uint id, uint type, int depth, int x, int y, bool activated)
            => new(id, type, (uint)depth, x, y, activated, 0, AuthoritativeObjectEffectKind.None, 0) {
                NativeObjectId = type, MissilePassable = true, HideWhenActivated = false, AnimationFrame = activated ? 2 : 0,
            };

        public void AdvanceTo(ulong tick, int depth)
        {
            while (currentTick < tick) {
                currentTick++;
                var ids = World.Projectiles.Keys.Order().ToList(); var seen = ids.ToHashSet();
                for (var index = 0; index < ids.Count; index++) {
                    if (World.Projectiles.TryGetValue(ids[index], out var missile)) {
                        if (missile.NativeMonsterMissile is not null)
                            ProcessMonster.Invoke(processor, [currentTick, missile, new[] { player }, new List<DiabloDirectedGameplayEvent>()]);
                        else processor.ProcessObjectTrapMissile(currentTick, missile, depth, [player], []);
                    }
                    foreach (var id in World.Projectiles.Keys.Order()) if (seen.Add(id)) ids.Add(id);
                }
            }
        }
    }
}
