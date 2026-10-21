using System.Reflection;
using Devilution.Engine;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralNativePopulationIntegrationTests;

namespace Devilution.Diablo.Tests;

/** Public-tick coverage for privately owned native Catacombs monster Arrows. */
public sealed class AuthoritativeNativeCatacombsArrowTests
{
    private static readonly FieldInfo ProjectedLevelsField = typeof(DiabloWorldState)
        .GetField("projectedLevels", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The projected-level test seam is missing.");
    private static readonly MethodInfo CreateArrowMethod = typeof(DiabloCombatCommandProcessor)
        .GetMethod("CreateNativeCatacombsArrowProjectile", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The Catacombs Arrow release seam is missing.");
    private static readonly MethodInfo ApplyResolvedDamageMethod = typeof(DiabloCombatCommandProcessor)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(method => method.Name == "ApplyResolvedDamageQ6"
            && method.GetParameters().Length == 6);

    [Fact(Explicit = true)]
    public void CatacombsArrowPausesOffFloorAndAdvancesOnItsOwnedFloor()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        var target = scenario.FindClearAdjacentTarget();
        var projectile = scenario.LaunchArrow(target.X, target.Y);
        scenario.FreezeActors();
        var initial = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        var initialProjectile = CaptureFlight(projectile);
        var initialRemainingTicks = projectile.RemainingTicks;
        var initialRandom = scenario.Random.State;

        scenario.Player.LevelId = scenario.Build.Policy.LevelId + 100;
        scenario.Processor.AdvanceTick(1, [scenario.Player]);

        Assert.Equal(initial, Assert.Single(scenario.World.CaptureNativeCatacombsArrows()));
        Assert.Equal(initialProjectile, CaptureFlight(projectile));
        Assert.Equal(initialRandom, scenario.Random.State);

        scenario.Player.LevelId = scenario.Build.Policy.LevelId;
        scenario.PlacePlayerAtClearCell(projectile);
        scenario.Processor.AdvanceTick(2, [scenario.Player]);

        var advanced = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        Assert.Equal(1U, advanced.MissileDistance);
        Assert.Equal(initialRemainingTicks - 1, projectile.RemainingTicks);
        scenario.World.ValidateNativePopulationState([scenario.Player]);
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void CatacombsArrowHitsOneOrTwoPlayersThroughThePublicProcessor(bool twoArrows)
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        var target = scenario.FindClearAdjacentTarget();
        scenario.Player.PositionX = target.X;
        scenario.Player.PositionY = target.Y;
        scenario.Player.CombatMode = DiabloPlayerCombatMode.WalkSouthwards;
        scenario.Source.MinDamage = 4;
        scenario.Source.MaxDamage = 4;
        var first = scenario.LaunchArrow(target.X, target.Y);
        var second = twoArrows ? scenario.LaunchArrow(target.X, target.Y) : null;
        scenario.FreezeActors();
        scenario.Player.CombatMode = DiabloPlayerCombatMode.WalkSouthwards;
        scenario.Random.Restore(FindTwoCertainHitSeed());
        var startLife = scenario.Player.Life;

        var events = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 1; tick <= 8 && scenario.World.Projectiles.Count != 0; tick++)
            events.AddRange(scenario.Processor.AdvanceTick(tick, [scenario.Player]));

        var damageEvents = events.Select(value => value.Event)
            .Where(value => value.Kind == DiabloGameplayEventKind.Damage
                && value.TargetEntityId == scenario.Player.EntityId)
            .ToArray();
        Assert.Equal(twoArrows ? 2 : 1, damageEvents.Length);
        Assert.All(damageEvents, value => Assert.Equal(scenario.Source.EntityId, value.SourceEntityId));
        Assert.True(scenario.Player.Life < startLife);
        Assert.False(scenario.World.HasNativeCatacombsArrow(first.EntityId));
        if (second is not null)
            Assert.False(scenario.World.HasNativeCatacombsArrow(second.EntityId));
        Assert.Empty(scenario.World.Projectiles);
        scenario.World.ValidateNativePopulationState([scenario.Player]);
    }

    [Fact(Explicit = true)]
    public void CatacombsArrowRetainsRemovedSourceFactsAcrossADeathTick()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        var target = scenario.FindLongestClearArrowTarget();
        var projectile = scenario.LaunchArrow(target.X, target.Y);
        foreach (var actor in scenario.World.CombatTargets.Values.Where(value => value.EntityId != scenario.Source.EntityId))
            actor.IsPetrified = true;
        scenario.PlacePlayerAtClearCell(projectile);
        ApplyResolvedDamageMethod.Invoke(scenario.Processor, [scenario.Player, scenario.Source,
            scenario.Source.HitPointsQ6, new List<DiabloGameplayEvent>(), false, false]);
        if (scenario.Source.HitPointsQ6 != 0)
            throw new InvalidOperationException("The shared combat damage path did not defeat the Arrow source.");
        if (scenario.Source.DeathAnimationTicks > 0)
            scenario.Source.RestoreDeathAnimation(1);
        var sourcePosition = (scenario.Source.PositionX, scenario.Source.PositionY);

        scenario.Processor.AdvanceTick(1, [scenario.Player]);

        Assert.False(scenario.World.CombatTargets.ContainsKey(scenario.Source.EntityId));
        Assert.True(scenario.World.Corpses.ContainsKey(scenario.Source.EntityId));
        Assert.Equal(sourcePosition,
            (scenario.World.Corpses[scenario.Source.EntityId].PositionX,
                scenario.World.Corpses[scenario.Source.EntityId].PositionY));
        var arrow = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        Assert.Equal(sourcePosition, (arrow.SourcePositionX, arrow.SourcePositionY));
        Assert.True(arrow.MissileDistance > 0);
        Assert.True(scenario.World.Projectiles.ContainsKey(projectile.EntityId));
        scenario.World.ValidateNativePopulationState([scenario.Player]);
    }

    [Fact(Explicit = true)]
    public void PreparedOpenLaneArrowIsRemovedWhenItsNativeFlightLeavesTheFloor()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        var projectedLevels = ProjectedLevelsField.GetValue(scenario.World)
            as Dictionary<uint, AuthoritativeLevel>
            ?? throw new InvalidOperationException("The qualified Arrow fixture has no projected levels.");
        var levelId = scenario.Build.Policy.LevelId;
        if (!projectedLevels.TryGetValue(levelId, out var originalLevel))
            throw new InvalidOperationException("The qualified Arrow fixture has no projected floor.");
        var sourcePosition = (scenario.Source.PositionX, scenario.Source.PositionY);
        var sourceDirection = scenario.Source.Direction;
        // Prepare a clear outbound lane only in the private collision projection.
        // The trusted backing level and Catacombs owner remain untouched.
        projectedLevels[levelId] = CreatePreparedOpenEdgeLevel(originalLevel);
        try {
            var row = scenario.FindClearEastEdgeRow();
            scenario.Source.PositionX = 110;
            scenario.Source.PositionY = row;
            scenario.Source.RestoreDirection((int)DeterministicGridDirection.East);
            var projectile = scenario.LaunchArrow(111, row);
            scenario.FreezeActors();
            scenario.PlacePlayerAtClearCell(projectile);

            for (ulong tick = 1; tick <= 12 && scenario.World.HasNativeCatacombsArrow(projectile.EntityId); tick++)
                scenario.Processor.AdvanceTick(tick, [scenario.Player]);

            Assert.False(scenario.World.HasNativeCatacombsArrow(projectile.EntityId));
            Assert.False(scenario.World.Projectiles.ContainsKey(projectile.EntityId));
        } finally {
            scenario.Source.PositionX = sourcePosition.PositionX;
            scenario.Source.PositionY = sourcePosition.PositionY;
            scenario.Source.RestoreDirection(sourceDirection);
            projectedLevels[levelId] = originalLevel;
        }
        scenario.World.ValidateNativePopulationState([scenario.Player]);
    }

    internal static NativeCatacombsArrowTestScenario CreateScenario(
        uint classId,
        int depth,
        uint dungeonSeed,
        string nativeAi,
        int targetX = 75,
        int targetY = 68,
        int? nativeSlot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeAi);
        var fixture = CreateFixture(classId);
        try {
            var build = fixture.CreateBuild(depth, checked((uint)depth), dungeonSeed: dungeonSeed);
            var world = DiabloWorldState.CreateNativeCatacombsFloor(
                build, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
            if (world.CombatTargets.Values
                    .Where(value => value.LevelId == build.Policy.LevelId
                        && value.NativeAi == nativeAi
                        && value.NativeInitialization is { InitialState.Mode: 0 }
                        && (nativeSlot is null
                            || value.NativeInitialization.Binding.NativeSlot == nativeSlot.Value))
                    .OrderBy(value => value.NativeInitialization!.Binding.NativeSlot)
                    .FirstOrDefault() is not { } source)
                throw new InvalidOperationException(
                    $"The qualified C2 fixture has no eligible {nativeAi} source on depth {depth}.");
            if (!fixture.Activation.MonsterMissiles!.TryGet(source.NativeAi!, source.MonsterId, out var missile))
                throw new InvalidOperationException("The qualified C2 actor has no compiled missile row.");
            var hero = build.Ready.Request.FreshLevel.Request.Hero;
            var player = fixture.Activation.PlayerLoadouts.CreateTemplate(
                "catacombs-arrow-public-tick", classId, null, null, null,
                startingLevelId: build.Policy.LevelId,
                startingPositionX: hero.PreEntryFuture.X,
                startingPositionY: hero.PreEntryFuture.Y).CreatePlayer();
            player.EntityId = 0x70000001;
            player.PositionX = targetX;
            player.PositionY = targetY;
            var random = new DeterministicLegacyRandom(0x12345678);
            var processor = new DiabloCombatCommandProcessor(world, fixture.Spells,
                new AuthoritativeCombatRules(10, 1, 100), random,
                monsterMissiles: fixture.Activation.MonsterMissiles,
                monsterTypes: fixture.Activation.TrustedOptions!.MonsterTypes);
            return new NativeCatacombsArrowTestScenario(
                fixture, build, world, player, processor, random, source, missile);
        } catch {
            fixture.Dispose();
            throw;
        }
    }

    private static string CaptureFlight(AuthoritativeProjectile projectile)
        => System.Text.Json.JsonSerializer.Serialize(new {
            projectile.EntityId,
            projectile.PositionX,
            projectile.PositionY,
            projectile.TraveledX,
            projectile.TraveledY,
            projectile.RemainingTicks,
            projectile.LastCollisionTargetEntityId,
        });

    private static uint FindTwoCertainHitSeed()
    {
        for (uint seed = 0; seed < 1_000_000; seed++) {
            var candidate = new DeterministicLegacyRandom(seed);
            if (candidate.GenerateRnd(100) == 0) {
                _ = candidate.GenerateRnd(1);
                _ = candidate.AdvanceRndSeed();
                if (candidate.GenerateRnd(100) == 0)
                    return seed;
            }
        }
        throw new InvalidOperationException("Could not find a native seed with two minimum hit rolls.");
    }

    private static AuthoritativeLevel CreatePreparedOpenEdgeLevel(AuthoritativeLevel level)
        => new(level.LevelId, level.Width, level.Height,
            blockedCells: [],
            missileBlockedCells: [],
            isDefaultStart: level.IsDefaultStart,
            suppressMultiplayerDeathDrops: level.SuppressMultiplayerDeathDrops,
            nativeTransparency: level.NativeTransparency,
            nativeObjectLayer: level.NativeObjectLayer,
            nativeMegaTileLayer: level.NativeMegaTileLayer,
            nativePieceLayer: level.NativePieceLayer,
            nativePieceProperties: level.NativePieceProperties,
            nativePieceCollisionCells: [],
            nativePieceMissileBlockedCells: [],
            nativeMonsterLayer: level.NativeMonsterLayer,
            startPositionX: level.StartPositionX,
            startPositionY: level.StartPositionY,
            nativeVisionBlockedCells: [],
            nativeSpecialLayer: level.NativeSpecialLayer,
            staticObjectBlockedCells: []) {
            StableSymbol = level.StableSymbol,
        };

    internal static void EmitArrow(
        DiabloCombatCommandProcessor processor,
        AuthoritativeCombatTarget source,
        AuthoritativePlayerState player,
        AuthoritativeNativeCatacombsRangedAction action,
        AuthoritativeMonsterMissileDefinition missile)
        => CreateArrowMethod.Invoke(processor, [source, player, action, missile]);
}

/** Qualified owner fixture and production release seam shared with native readers. */
internal sealed class NativeCatacombsArrowTestScenario : IDisposable
{
    private readonly List<AuthoritativeNativeCatacombsRangedAction> actionSamples = [];

    internal NativeCatacombsArrowTestScenario(
        QualifiedCatacombsBuilderFixture fixture,
        AuthoritativeCatacombsNativePopulationBuild build,
        DiabloWorldState world,
        AuthoritativePlayerState player,
        DiabloCombatCommandProcessor processor,
        DeterministicLegacyRandom random,
        AuthoritativeCombatTarget source,
        AuthoritativeMonsterMissileDefinition missile)
    {
        Fixture = fixture;
        Build = build;
        World = world;
        Player = player;
        Processor = processor;
        Random = random;
        Source = source;
        Missile = missile;
    }

    internal QualifiedCatacombsBuilderFixture Fixture { get; }
    internal AuthoritativeCatacombsNativePopulationBuild Build { get; }
    internal DiabloWorldState World { get; }
    internal AuthoritativePlayerState Player { get; }
    internal DiabloCombatCommandProcessor Processor { get; }
    internal DeterministicLegacyRandom Random { get; }
    internal AuthoritativeCombatTarget Source { get; }
    internal AuthoritativeMonsterMissileDefinition Missile { get; }
    internal IReadOnlyList<AuthoritativeNativeCatacombsRangedAction> ActionSamples
        => Array.AsReadOnly(actionSamples.ToArray());
    internal AuthoritativeNativeCatacombsRangedAction? ReleasedAction { get; private set; }

    internal void FreezeActors()
    {
        foreach (var actor in World.CombatTargets.Values)
            actor.IsPetrified = true;
    }

    internal (int X, int Y) FindClearAdjacentTarget()
    {
        foreach (var direction in Enum.GetValues<DeterministicGridDirection>()) {
            var displacement = DeterministicGridDirections.GetDisplacement(direction);
            var candidate = (X: Source.PositionX + displacement.X, Y: Source.PositionY + displacement.Y);
            if ((uint)candidate.X >= 112 || (uint)candidate.Y >= 112
                || !World.IsWalkable(Build.Policy.LevelId, candidate.X, candidate.Y)
                || World.IsMissileBlocked(Build.Policy.LevelId, candidate.X, candidate.Y))
                continue;

            var oldPosition = (Player.PositionX, Player.PositionY);
            Player.PositionX = candidate.X;
            Player.PositionY = candidate.Y;
            var occupants = World.NativeTrapArrowOccupants(Build.Policy.LevelId,
                candidate.X, candidate.Y, [Player]);
            Player.PositionX = oldPosition.PositionX;
            Player.PositionY = oldPosition.PositionY;
            if (occupants.Monster is null)
                return candidate;
        }
        throw new InvalidOperationException("The generated actor has no clear adjacent Arrow target.");
    }

    internal (int X, int Y) FindLongestClearArrowTarget()
    {
        var longest = (X: -1, Y: -1);
        var longestDistance = 0;
        foreach (var direction in Enum.GetValues<DeterministicGridDirection>()) {
            var displacement = DeterministicGridDirections.GetDisplacement(direction);
            for (var distance = 1; distance < 112; distance++) {
                var candidate = (X: Source.PositionX + displacement.X * distance,
                    Y: Source.PositionY + displacement.Y * distance);
                if ((uint)candidate.X >= 112 || (uint)candidate.Y >= 112
                    || !World.IsWalkable(Build.Policy.LevelId, candidate.X, candidate.Y)
                    || World.IsMissileBlocked(Build.Policy.LevelId, candidate.X, candidate.Y)
                    || World.IsOccupied(Build.Policy.LevelId, candidate.X, candidate.Y, [Player], Player.EntityId))
                    break;
                if (distance > longestDistance) {
                    longest = candidate;
                    longestDistance = distance;
                }
            }
        }
        if (longestDistance < 2)
            throw new InvalidOperationException("The generated actor has no clear ranged lane for source-removal coverage.");
        return longest;
    }

    internal int FindClearEastEdgeRow()
    {
        for (var y = 1; y < 111; y++) {
            if (World.IsMissileBlocked(Build.Policy.LevelId, 110, y)
                || World.IsMissileBlocked(Build.Policy.LevelId, 111, y)
                || World.IsOccupied(Build.Policy.LevelId, 110, y, [Player], Source.EntityId)
                || World.IsOccupied(Build.Policy.LevelId, 111, y, [Player], Source.EntityId))
                continue;
            return y;
        }
        throw new InvalidOperationException("The generated floor has no unblocked final eastbound Arrow lane.");
    }

    internal void PlacePlayerAtClearCell(AuthoritativeProjectile projectile)
    {
        for (var y = 1; y < 111; y++) {
            for (var x = 1; x < 111; x++) {
                if (DeterministicGridMetrics.ChebyshevDistance(
                        x, y, projectile.PositionX, projectile.PositionY) < 4
                    || !World.IsWalkable(Build.Policy.LevelId, x, y)
                    || World.IsMissileBlocked(Build.Policy.LevelId, x, y)
                    || World.IsOccupied(Build.Policy.LevelId, x, y, [Player], Player.EntityId))
                    continue;
                Player.LevelId = Build.Policy.LevelId;
                Player.PositionX = x;
                Player.PositionY = y;
                return;
            }
        }
        throw new InvalidOperationException("The generated floor has no safe same-floor Arrow observer cell.");
    }

    internal AuthoritativeProjectile LaunchArrow(int targetX, int targetY, uint attackSeed = 123)
    {
        Player.PositionX = targetX;
        Player.PositionY = targetY;
        Player.CombatMode = DiabloPlayerCombatMode.Stand;
        Player.CombatModeEndTick = 0;
        Player.CurrentAction = null;
        World.StartNativeCatacombsRangedAction(Source, Missile, Player, attackSeed);
        var priorProjectileIds = World.Projectiles.Keys.ToHashSet();
        for (var tick = 0; tick < 512; tick++) {
            var action = World.CaptureNativeCatacombsRangedActions()
                .Single(value => value.EntityId == Source.EntityId);
            actionSamples.Add(action);
            var step = World.AdvanceNativeCatacombsRangedAction(Source, Missile,
                Player.FuturePositionX, Player.FuturePositionY);
            if (step.Release) {
                ReleasedAction = step.Action;
                AuthoritativeNativeCatacombsArrowTests.EmitArrow(
                    Processor, Source, Player, step.Action, Missile);
                var projectile = World.Projectiles.Values
                    .Single(value => !priorProjectileIds.Contains(value.EntityId));
                World.CancelNativeCatacombsRangedAction(Source.EntityId);
                return projectile;
            }
            if (step.Completed)
                break;
        }
        World.CancelNativeCatacombsRangedAction(Source.EntityId);
        throw new InvalidOperationException("The qualified ranged action ended without its native effect-frame release.");
    }

    public void Dispose() => Fixture.Dispose();
}
