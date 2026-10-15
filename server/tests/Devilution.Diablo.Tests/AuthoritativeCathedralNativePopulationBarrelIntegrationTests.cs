using System.Text.Json;
using Devilution.Diablo;
using Devilution.Domain.Inventory;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    private const string NativeBarrelSession = "native-barrel-integration";

    [Fact(Explicit = true)]
    public void NativeBarrelMeleeCommandBreaksCapturedLootBarrelAcrossV6AndV7ColdRestore()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;

        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var barrel = world.Objects.Values
            .Where(value => value.LevelId == 1 && value.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType)
            .Where(world.CanBreakNativeBarrel)
            .Where(value => world.TryGetNativeBarrelFacts(value, out var facts)
                && facts.Var2 <= 1 && facts.LinkedMonster is null)
            .Where(value => !world.CombatTargets.Values.Any(target => target.LevelId == value.LevelId
                && target.HitPointsQ6 > 0
                && (target.PositionX == value.PositionX && target.PositionY == value.PositionY
                    || target.IsMoving && target.FuturePositionX == value.PositionX
                        && target.FuturePositionY == value.PositionY)))
            .Where(value => !world.WorldItems.Values.Any(item => item.LevelId == value.LevelId
                && item.PositionX == value.PositionX && item.PositionY == value.PositionY))
            .OrderBy(value => value.EntityId)
            .FirstOrDefault();
        Assert.NotNull(barrel);
        Assert.True(world.TryGetNativeBarrelFacts(barrel!, out var initialFacts));
        Assert.Equal(AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType, initialFacts.NativeTypeId);
        Assert.InRange(initialFacts.Var2, 0, 1);
        Assert.Null(initialFacts.LinkedMonster);

        var start = FindAdjacentWalkableCell(world, barrel!);
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var template = setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeBarrelSession,
            rogue.ClassId,
            goldOverride: null,
            lifeOverride: null,
            manaOverride: null,
            startingLevelId: 1,
            startingPositionX: start.X,
            startingPositionY: start.Y);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write(NativeBarrelSession, player => player.EntityId = 0x7E000201);
        var codec = new DiabloCohortCodec();
        var initialPlayer = runtime.Read(NativeBarrelSession, value => value);
        var beforeRefusal = codec.Serialize(simulation.CaptureCohortState(runtime));
        var operateAttempt = simulation.Execute(initialPlayer,
            new DiabloOperateObjectIntent(barrel!.EntityId), 1, [initialPlayer]);
        Assert.False(operateAttempt.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, operateAttempt.Failure);
        Assert.Equal(beforeRefusal, codec.Serialize(simulation.CaptureCohortState(runtime)));

        // Version 6 carries the complete native sarcophagus state, but predates
        // the barrel lifecycle sidecar. Restore it before invoking real melee.
        var capturedV5 = simulation.CaptureCohortState(runtime);
        var capturedV5Bytes = codec.Serialize(capturedV5);
        var v6 = capturedV5 with {
            FormatVersion = DiabloCohortState.NativeSarcophagusFormatVersion,
            NativeSarcophagi = world.CaptureNativeSarcophagi(),
            NativeBarrels = null,
        };
        v6.ValidateShape();
        Assert.NotNull(v6.NativeSarcophagi);
        Assert.Null(v6.NativeBarrels);
        var v6Bytes = codec.Serialize(v6);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);
        var restored = factory(codec.Deserialize(v6Bytes));
        var restoredSimulation = restored.Simulation;
        var restoredRuntime = restored.CreateRuntime();
        var restoredPlayer = restoredRuntime.Read(NativeBarrelSession, value => value);
        var restoredBarrel = restoredSimulation.WorldState.Objects[barrel!.EntityId];
        var untouchedRestore = restoredSimulation.CaptureCohortState(restoredRuntime);
        Assert.Equal(DiabloCohortState.NativeCombatFormatVersion, untouchedRestore.FormatVersion);
        Assert.Null(untouchedRestore.NativeSarcophagi);
        Assert.Equal(capturedV5Bytes, codec.Serialize(untouchedRestore));

        var beforeVisibleItems = restoredSimulation.WorldState.WorldItems.Values
            .Where(value => value.LevelId == 1).Select(value => value.EntityId).ToHashSet();
        var bow = Assert.Single(restoredPlayer.Equipment, value => value.Slot == 4);
        var inventoryCell = InventoryGridLayout.FindNativePlacement(restoredPlayer.InventoryGrid,
            bow.State.InventoryWidth, bow.State.InventoryHeight);
        Assert.True(inventoryCell >= 0);
        var unequip = restoredSimulation.Execute(restoredPlayer, new DiabloMoveItemIntent(
            new(DiabloPlayerItemLocation.Equipment, 4, bow.State.StableItemId),
            new(DiabloPlayerItemLocation.Inventory, checked((uint)inventoryCell), bow.State.StableItemId)), 1);
        Assert.True(unequip.Succeeded, $"The Rogue's starter bow could not be unequipped for native melee: {unequip.Failure}.");
        restoredRuntime.AdvanceTo(1);
        Assert.DoesNotContain(restoredPlayer.Equipment, value => value.Slot == 4);
        var attack = restoredSimulation.Execute(restoredPlayer,
            new DiabloAttackAtIntent(restoredBarrel.PositionX, restoredBarrel.PositionY),
            2,
            [restoredPlayer]);
        Assert.True(attack.Succeeded, $"The ordinary native melee command was refused: {attack.Failure}.");
        var action = Assert.IsType<AuthoritativePlayerActionState>(restoredPlayer.CurrentAction);
        restoredRuntime.AdvanceTo(action.EffectTick);

        var afterState = restoredSimulation.WorldState.GetNativeBarrelState(initialFacts.ObjectSlot);
        Assert.NotNull(afterState);
        Assert.True(afterState!.Broken);
        Assert.Equal(DiabloNativeBarrelActorState.None, afterState.ActorState);
        Assert.False(afterState.SolidFlag);
        Assert.True(afterState.MissilePassable);
        Assert.Equal(0, afterState.SelectionRegion);
        Assert.True(restoredBarrel.Activated);
        Assert.True(restoredBarrel.MissilePassable);
        Assert.False(restoredSimulation.WorldState.CanBreakNativeBarrel(restoredBarrel));
        var dropped = Assert.Single(restoredSimulation.WorldState.WorldItems.Values
            .Where(value => value.LevelId == 1 && !beforeVisibleItems.Contains(value.EntityId)));
        Assert.InRange(Chebyshev(dropped.PositionX, dropped.PositionY,
            initialFacts.PositionX, initialFacts.PositionY), 0, 1);

        var broken = restoredSimulation.CaptureCohortState(restoredRuntime);
        Assert.Equal(DiabloCohortState.NativeBarrelFormatVersion, broken.FormatVersion);
        Assert.NotNull(broken.NativeBarrels);
        Assert.NotNull(broken.NativeSarcophagi);
        var brokenBarrel = Assert.Single(broken.NativeBarrels!, value => value.ObjectSlot == initialFacts.ObjectSlot);
        Assert.True(brokenBarrel.Broken);
        Assert.Equal(initialFacts.RandomSeed, brokenBarrel.RandomSeed);
        var brokenBytes = codec.Serialize(broken);
        var fresh = factory(codec.Deserialize(brokenBytes));
        var freshRuntime = fresh.CreateRuntime();
        Assert.Equal(brokenBytes, codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime)));
        var retainedDrop = fresh.Simulation.WorldState.WorldItems[dropped.EntityId];
        Assert.Equal(dropped.ItemSeed, retainedDrop.ItemSeed);
        Assert.Equal(dropped.State, retainedDrop.State);
        Assert.Equal((dropped.PositionX, dropped.PositionY),
            (retainedDrop.PositionX, retainedDrop.PositionY));

        var duplicatePlayer = freshRuntime.Read(NativeBarrelSession, value => value);
        var beforeDuplicate = codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime));
        var duplicate = fresh.Simulation.BreakNativeBarrelAtEffectBoundary(
            duplicatePlayer, restoredBarrel.EntityId, checked(freshRuntime.LastAdvancedTick + 1), [duplicatePlayer]);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, duplicate.Failure);
        Assert.Equal(beforeDuplicate, codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime)));
    }

    [Fact(Explicit = true)]
    public void NativeExplosiveBarrelUsesCurrentWorldRngAndColdRestoresWithoutPoolDraws()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;

        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var explosive = world.Objects.Values
            .Where(value => value.LevelId == 1
                && value.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType)
            .Where(world.CanBreakNativeBarrel)
            .Where(value => !world.CombatTargets.Values.Any(target => target.LevelId == value.LevelId
                && target.HitPointsQ6 > 0
                && Math.Max(Math.Abs(target.PositionX - value.PositionX),
                    Math.Abs(target.PositionY - value.PositionY)) <= 1))
            .Where(value => !world.Objects.Values.Any(other => other.EntityId != value.EntityId
                && other.LevelId == value.LevelId
                && other.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType
                && Math.Max(Math.Abs(other.PositionX - value.PositionX),
                    Math.Abs(other.PositionY - value.PositionY)) <= 1))
            .OrderBy(value => value.EntityId)
            .FirstOrDefault();
        Assert.NotNull(explosive);
        Assert.True(world.TryGetNativeBarrelFacts(explosive!, out var facts));
        Assert.True(facts.IsExplosive);

        var start = Enumerable.Range(1, AuthoritativeCathedralThemeRoomState.Width - 2)
            .SelectMany(y => Enumerable.Range(1, AuthoritativeCathedralThemeRoomState.Width - 2)
                .Select(x => (X: x, Y: y)))
            .First(position => world.IsWalkable(1, position.X, position.Y)
                && !world.IsOccupied(1, position.X, position.Y, [])
                && Math.Max(Math.Abs(position.X - explosive!.PositionX),
                    Math.Abs(position.Y - explosive.PositionY)) > 2);
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeBarrelSession, rogue.ClassId, goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: start.X, startingPositionY: start.Y));
        runtime.Write(NativeBarrelSession, player => player.EntityId = 0x7E000202);
        var player = runtime.Read(NativeBarrelSession, value => value);
        var codec = new DiabloCohortCodec();
        var beforePool = JsonSerializer.Serialize(simulation.CaptureCohortState(runtime).CathedralItems);
        var initialRandom = simulation.RandomState;

        var result = simulation.BreakNativeBarrelAtEffectBoundary(
            player, explosive!.EntityId, checked(runtime.LastAdvancedTick + 1), [player]);
        Assert.True(result.Succeeded, $"The isolated native explosive boundary was refused: {result.Failure}.");
        Assert.Equal(initialRandom, simulation.RandomState);
        Assert.Equal(beforePool, JsonSerializer.Serialize(simulation.CaptureCohortState(runtime).CathedralItems));
        var state = world.GetNativeBarrelState(facts.ObjectSlot);
        Assert.NotNull(state);
        Assert.True(state!.Broken);
        Assert.Equal(DiabloNativeBarrelActorState.None, state.ActorState);

        var checkpoint = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeBarrelFormatVersion, checkpoint.FormatVersion);
        var bytes = codec.Serialize(checkpoint);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);
        var restored = factory(codec.Deserialize(bytes));
        var restoredRuntime = restored.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(initialRandom, restored.Simulation.RandomState);
    }

    [Fact(Explicit = true)]
    public void NativePinnedThreeBarrelChainAppliesMonsterAndPlayerHitsAndColdRestores()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;

        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var clusterPositions = new (int X, int Y)[] { (47, 58), (46, 57), (47, 59) };
        var cluster = clusterPositions.Select(position => Assert.Single(world.Objects.Values,
            value => value.LevelId == 1 && value.PositionX == position.X && value.PositionY == position.Y
                && value.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType))
            .ToArray();
        var clusterFacts = new AuthoritativeCathedralNativeBarrelFacts[cluster.Length];
        for (var index = 0; index < cluster.Length; index++) {
            var barrel = cluster[index];
            Assert.True(world.CanBreakNativeBarrel(barrel));
            Assert.True(world.TryGetNativeBarrelFacts(barrel, out var facts));
            Assert.True(facts.IsExplosive);
            Assert.Null(facts.LinkedMonster);
            clusterFacts[index] = facts;
        }

        // Match the native capture's three-barrel geometry: the monster is
        // reached by the NW child, then the player is reached by the root
        // blast before the south child is visited.
        var playerStart = (X: 48, Y: 58);
        var player = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var template = setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeBarrelSession, player.ClassId, goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: playerStart.X, startingPositionY: playerStart.Y);
        var actor = world.CombatTargets.Values
            .Where(value => value.LevelId == 1 && value.HitPointsQ6 > 0
                && !value.IsPlayerMinion && !value.IsUnique
                && value.NativeInitialization?.Binding.NativeTypeId == 8)
            .OrderBy(value => value.EntityId)
            .FirstOrDefault();
        Assert.NotNull(actor);
        var liveActor = actor!;
        var actorTile = (X: 45, Y: 56);
        Assert.True(world.IsWalkable(1, actorTile.X, actorTile.Y));
        Assert.False(world.IsOccupied(1, actorTile.X, actorTile.Y, [], liveActor.EntityId));
        liveActor.ApplyKnockback(actorTile.X, actorTile.Y);

        const int capturedActorHitPointsQ6 = 256;
        liveActor.RestoreHitPointsQ6(capturedActorHitPointsQ6, 6400);
        liveActor.ArmorClass = 0;
        liveActor.FireResistance = 0;
        liveActor.RestoreNativePlayerHitRecorded(false);

        bool InBlastUnion(int x, int y) => clusterPositions.Any(center =>
            Chebyshev(x, y, center.X, center.Y) <= 1);

        foreach (var otherActor in world.CombatTargets.Values
                     .Where(value => value.EntityId != liveActor.EntityId && value.LevelId == 1
                         && value.HitPointsQ6 > 0
                         && (InBlastUnion(value.PositionX, value.PositionY)
                             || value.IsMoving && InBlastUnion(value.FuturePositionX, value.FuturePositionY)))
                     .OrderBy(value => value.EntityId).ToArray()) {
            var safeTile = Enumerable.Range(1, AuthoritativeCathedralThemeRoomState.Width - 2)
                .SelectMany(y => Enumerable.Range(1, AuthoritativeCathedralThemeRoomState.Width - 2)
                    .Select(x => (X: x, Y: y)))
                .FirstOrDefault(position => !InBlastUnion(position.X, position.Y)
                    && !clusterPositions.Contains(position)
                    && world.IsWalkable(1, position.X, position.Y)
                    && !world.IsOccupied(1, position.X, position.Y, [], otherActor.EntityId));
            Assert.NotEqual(default, safeTile);
            otherActor.ApplyKnockback(safeTile.X, safeTile.Y);
        }
        Assert.DoesNotContain(world.CombatTargets.Values, value => value.EntityId != liveActor.EntityId
            && value.LevelId == 1 && value.HitPointsQ6 > 0
            && (InBlastUnion(value.PositionX, value.PositionY)
                || value.IsMoving && InBlastUnion(value.FuturePositionX, value.FuturePositionY)));

        Assert.True(world.IsWalkable(1, playerStart.X, playerStart.Y));
        Assert.False(world.IsOccupied(1, playerStart.X, playerStart.Y, []));
        var runtime = simulation.CreateRuntime(template);
        runtime.Write(NativeBarrelSession, value => value.EntityId = 0x7E000205);
        var livePlayer = runtime.Read(NativeBarrelSession, value => value);
        var initialActorHitPointsQ6 = liveActor.HitPointsQ6;
        var initialPlayerLifeQ6 = livePlayer.Life;
        Assert.Equal(capturedActorHitPointsQ6, initialActorHitPointsQ6);
        Assert.Equal(2880, initialPlayerLifeQ6);

        var codec = new DiabloCohortCodec();
        var scenario = simulation.CaptureCohortState(runtime) with { RandomState = 2588 };
        scenario.ValidateShape();
        var scenarioBytes = codec.Serialize(scenario);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);

        var candidate = factory(codec.Deserialize(scenarioBytes));
        var candidateRuntime = candidate.CreateRuntime();
        var candidatePlayer = candidateRuntime.Read(NativeBarrelSession, value => value);
        var result = candidate.Simulation.BreakNativeBarrelAtEffectBoundary(
            candidatePlayer, cluster[0].EntityId, checked(candidateRuntime.LastAdvancedTick + 1), [candidatePlayer]);
        Assert.True(result.Succeeded, $"The pinned native three-barrel chain was refused: {result.Failure}.");
        var afterBytes = codec.Serialize(candidate.Simulation.CaptureCohortState(candidateRuntime));
        Assert.Equal(1630774887U, candidate.Simulation.RandomState);
        var damageEvents = result.Events.Where(value => value.Kind == DiabloGameplayEventKind.Damage).ToArray();
        var actorDamage = Assert.Single(damageEvents, value => value.TargetEntityId == liveActor.EntityId);
        var playerDamageEvents = damageEvents.Where(value => value.TargetEntityId == 0x7E000205u).ToArray();
        Assert.NotEmpty(playerDamageEvents);
        Assert.True(Array.IndexOf(damageEvents, actorDamage) < Array.FindIndex(damageEvents,
            value => value.TargetEntityId == 0x7E000205u), "The NW child must damage the monster before the root hits the player.");
        Assert.Equal(128, actorDamage.AmountQ6);
        var playerDamage = Assert.Single(playerDamageEvents);
        Assert.Equal(978, playerDamage.AmountQ6);
        Assert.Equal(2, damageEvents.Length);

        var fresh = factory(codec.Deserialize(afterBytes));
        var freshRuntime = fresh.CreateRuntime();
        Assert.Equal(afterBytes, codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime)));
        Assert.Equal(128, fresh.Simulation.WorldState.CombatTargets[liveActor.EntityId].HitPointsQ6);
        Assert.Equal(initialActorHitPointsQ6 - actorDamage.AmountQ6,
            fresh.Simulation.WorldState.CombatTargets[liveActor.EntityId].HitPointsQ6);
        Assert.Equal(initialPlayerLifeQ6 - playerDamageEvents.Sum(value => value.AmountQ6),
            freshRuntime.Read(NativeBarrelSession, value => value.Life));
        Assert.Equal(1902, freshRuntime.Read(NativeBarrelSession, value => value.Life));
        var savedBarrels = fresh.Simulation.WorldState.CaptureNativeBarrels();
        Assert.NotNull(savedBarrels);
        foreach (var expected in clusterFacts) {
            var saved = Assert.Single(savedBarrels!, value => value.ObjectSlot == expected.ObjectSlot);
            Assert.True(saved.Broken);
            Assert.Equal(DiabloNativeBarrelActorState.None, saved.ActorState);
        }

        var freshPlayer = freshRuntime.Read(NativeBarrelSession, value => value);
        var beforeDuplicate = codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime));
        var duplicate = fresh.Simulation.BreakNativeBarrelAtEffectBoundary(
            freshPlayer, cluster[0].EntityId, checked(freshRuntime.LastAdvancedTick + 1), [freshPlayer]);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, duplicate.Failure);
        Assert.Equal(beforeDuplicate, codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime)));
    }

    [Fact(Explicit = true)]
    public void NativeExplosivePlayerDamageMatchesCapturedHitMissBlockAndResistanceCases()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Set GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY to the native capture.");
        var path = Path.Combine(Path.GetFullPath(directory), "native-barrel-operations.json");
        Assert.InRange(new FileInfo(path).Length, 1L, 64L * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        Assert.Equal("godevilution-cathedral-barrel-operations", root.GetProperty("format").GetString());
        var cases = root.GetProperty("cases").EnumerateArray()
            .Where(value => value.GetProperty("name").GetString() is string name
                && name.StartsWith("explosive-player-", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, cases.Length);

        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;
        var baselineSimulation = setup.Activation.Simulation;
        var baselineWorld = baselineSimulation.WorldState;
        var explosive = baselineWorld.Objects.Values
            .Where(value => value.LevelId == 1
                && value.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType)
            .Where(baselineWorld.CanBreakNativeBarrel)
            .Where(value => !baselineWorld.CombatTargets.Values.Any(target => target.LevelId == value.LevelId
                && target.HitPointsQ6 > 0
                && (Chebyshev(target.PositionX, target.PositionY, value.PositionX, value.PositionY) <= 1
                    || target.IsMoving && Chebyshev(target.FuturePositionX, target.FuturePositionY,
                        value.PositionX, value.PositionY) <= 1)))
            .Where(value => !baselineWorld.Objects.Values.Any(other => other.EntityId != value.EntityId
                && other.LevelId == value.LevelId
                && other.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType
                && Chebyshev(other.PositionX, other.PositionY, value.PositionX, value.PositionY) <= 1))
            .OrderBy(value => value.EntityId)
            .FirstOrDefault();
        Assert.NotNull(explosive);
        var explosiveEntityId = explosive!.EntityId;

        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var start = FindAdjacentWalkableCell(baselineWorld, explosive);
        var template = setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeBarrelSession, rogue.ClassId, goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: start.X, startingPositionY: start.Y);
        var baseRuntime = baselineSimulation.CreateRuntime(template);
        baseRuntime.Write(NativeBarrelSession, player => player.EntityId = 0x7E000203);
        var baseline = baselineSimulation.CaptureCohortState(baseRuntime);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);

        for (var caseIndex = 0; caseIndex < cases.Length; caseIndex++) {
            var value = cases[caseIndex];
            var name = value.GetProperty("name").GetString()!;
            var input = value.GetProperty("inputPlayer");
            var expected = value.GetProperty("expectedPlayer");
            var randomSeed = value.GetProperty("initialRandomState").GetUInt32();
            var candidate = factory(baseline with { RandomState = randomSeed });
            var simulation = candidate.Simulation;
            var world = simulation.WorldState;
            var barrel = world.Objects[explosiveEntityId];
            Assert.True(world.CanBreakNativeBarrel(barrel));
            Assert.True(world.TryGetNativeBarrelFacts(barrel, out var barrelFacts));

            var inputLife = input.GetProperty("life").GetInt32();
            var expectedLife = expected.GetProperty("life").GetInt32();
            var dexterity = input.GetProperty("dexterity").GetInt32();
            var blockCase = input.GetProperty("blockFlag").GetBoolean();
            var fireResistance = input.GetProperty("fireResistance").GetInt32();
            IReadOnlyList<EquippedStoreItem> equipment = fireResistance == 0
                ? []
                : [new EquippedStoreItem(0, 1, AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusFireResistance = fireResistance,
                })];
            if (blockCase) {
                var shieldDefinition = simulation.Items.GetRequired(101);
                Assert.True(AuthoritativeItemClassification.IsShield(shieldDefinition));
                var shieldState = AuthoritativeItemState.Empty with {
                    StableItemId = shieldDefinition.ItemId,
                    ItemType = shieldDefinition.ItemType,
                    Identified = shieldDefinition.Identified,
                    EquipLocation = shieldDefinition.EquipLocation,
                    ItemClass = shieldDefinition.ItemClass,
                    Value = shieldDefinition.Value,
                    IdentifiedValue = shieldDefinition.IdentifiedValue,
                    ArmorClass = shieldDefinition.ArmorClass,
                    Durability = shieldDefinition.MaxDurability,
                    MaxDurability = shieldDefinition.MaxDurability,
                    ItemIndex = shieldDefinition.NativeItemIndex,
                    StatFlag = true,
                    BaseName = shieldDefinition.Name,
                    IdentifiedName = shieldDefinition.IdentifiedName,
                };
                equipment = [.. equipment, new EquippedStoreItem(5, 101, shieldState)];
            }
            var strength = input.GetProperty("strength").GetInt32();
            var magic = input.GetProperty("magic").GetInt32();
            var player = new AuthoritativePlayerState(
                0,
                input.GetProperty("experience").GetUInt32(),
                inputLife,
                input.GetProperty("mana").GetInt32(),
                input.GetProperty("manaMaximumBase").GetInt32(),
                new(
                    new(strength, strength),
                    new(magic, magic),
                    new(dexterity, dexterity),
                    new(0, 0)),
                equipment,
                [],
                [],
                barrel.PositionX,
                barrel.PositionY,
                input.GetProperty("lifeMaximumBase").GetInt32(),
                input.GetProperty("characterLevel").GetUInt32(),
                1,
                input.GetProperty("armor").GetInt32(),
                rogue.ClassId,
                items: simulation.Items) {
                EntityId = checked(0x7E000300u + (uint)caseIndex),
            };
            var players = new[] { player };
            var beforeItems = world.WorldItems.Keys.Order().ToArray();
            Assert.Equal(randomSeed, simulation.RandomState);

            var result = simulation.BreakNativeBarrelAtEffectBoundary(
                player, barrel.EntityId, 1, players);

            Assert.True(result.Succeeded, $"Captured explosive case '{name}' was refused: {result.Failure}.");
            Assert.Equal(expected.GetProperty("experience").GetUInt32(), player.Experience);
            Assert.Equal(expectedLife, player.Life);
            Assert.Equal(expected.GetProperty("mana").GetInt32(), player.Mana);
            Assert.Equal(expected.GetProperty("mode").GetInt32(), (int)player.CombatMode);
            Assert.Equal(value.GetProperty("expectedRandomState").GetUInt32(), simulation.RandomState);
            Assert.Equal(beforeItems, world.WorldItems.Keys.Order().ToArray());
            var state = world.GetNativeBarrelState(barrelFacts.ObjectSlot);
            Assert.NotNull(state);
            Assert.True(state!.Broken);

            var damage = result.Events.Where(gameEvent => gameEvent.Kind == DiabloGameplayEventKind.Damage
                && gameEvent.TargetEntityId == player.EntityId).ToArray();
            if (expectedLife < inputLife) {
                var hit = Assert.Single(damage);
                Assert.Equal(inputLife - expectedLife, hit.AmountQ6);
            } else {
                Assert.Empty(damage);
            }
            if (blockCase)
                Assert.Equal(DiabloPlayerCombatMode.Block, player.CombatMode);
        }
    }

    [Fact(Explicit = true)]
    public void NativeExplosiveMonsterDamageAndDeathLootMatchCapturedBoundaries()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Set GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY to the native capture.");
        var path = Path.Combine(Path.GetFullPath(directory), "native-barrel-operations.json");
        Assert.InRange(new FileInfo(path).Length, 1L, 64L * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        Assert.Equal("godevilution-cathedral-barrel-operations", root.GetProperty("format").GetString());
        var cases = root.GetProperty("cases").EnumerateArray()
            .Where(value => value.GetProperty("name").GetString() is string name
                && name.StartsWith("explosive-monster-", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(5, cases.Length);

        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;
        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var explosive = world.Objects.Values
            .Where(value => value.LevelId == 1
                && value.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType)
            .Where(world.CanBreakNativeBarrel)
            .Where(value => !world.CombatTargets.Values.Any(target => target.LevelId == value.LevelId
                && target.HitPointsQ6 > 0
                && (Chebyshev(target.PositionX, target.PositionY, value.PositionX, value.PositionY) <= 1
                    || target.IsMoving && Chebyshev(target.FuturePositionX, target.FuturePositionY,
                        value.PositionX, value.PositionY) <= 1)))
            .Where(value => !world.Objects.Values.Any(other => other.EntityId != value.EntityId
                && other.LevelId == value.LevelId
                && other.NativeObjectId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType
                && Chebyshev(other.PositionX, other.PositionY, value.PositionX, value.PositionY) <= 1))
            .OrderBy(value => value.EntityId)
            .FirstOrDefault();
        Assert.NotNull(explosive);

        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var playerStart = Enumerable.Range(1, AuthoritativeCathedralThemeRoomState.Width - 2)
            .SelectMany(y => Enumerable.Range(1, AuthoritativeCathedralThemeRoomState.Width - 2)
                .Select(x => (X: x, Y: y)))
            .Where(position => world.IsWalkable(1, position.X, position.Y)
                && !world.IsOccupied(1, position.X, position.Y, [])
                && Chebyshev(position.X, position.Y, explosive!.PositionX, explosive.PositionY) > 5)
            .OrderBy(position => position.Y)
            .ThenBy(position => position.X)
            .First();
        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeBarrelSession, rogue.ClassId, goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: playerStart.X, startingPositionY: playerStart.Y));
        runtime.Write(NativeBarrelSession, player => player.EntityId = 0x7E000204);
        var baseline = simulation.CaptureCohortState(runtime);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);
        var barrelEntityId = explosive!.EntityId;

        var offsets = new (int X, int Y)[] {
            (0, 0), (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
        };
        foreach (var value in cases) {
            var name = value.GetProperty("name").GetString()!;
            var initialRandom = value.GetProperty("initialRandomState").GetUInt32();
            var candidate = factory(baseline with { RandomState = initialRandom });
            var candidateSimulation = candidate.Simulation;
            var candidateWorld = candidateSimulation.WorldState;
            var candidateRuntime = candidate.CreateRuntime();
            var inputPlayer = value.GetProperty("inputPlayer");
            candidateRuntime.Write(NativeBarrelSession, current => {
                current.PositionX = playerStart.X;
                current.PositionY = playerStart.Y;
                current.Experience = inputPlayer.GetProperty("experience").GetUInt32();
                current.CharacterLevel = inputPlayer.GetProperty("characterLevel").GetUInt32();
                current.Life = inputPlayer.GetProperty("life").GetInt32();
                current.Mana = inputPlayer.GetProperty("mana").GetInt32();
            });
            var player = candidateRuntime.Read(NativeBarrelSession, current => current);

            var barrel = candidateWorld.Objects[barrelEntityId];
            var target = candidateWorld.CombatTargets.Values
                .Where(monster => monster.LevelId == 1 && monster.HitPointsQ6 > 0
                    && !monster.IsPlayerMinion && !monster.IsUnique && !monster.IsMoving
                    && monster.NativeInitialization?.Binding.NativeTypeId == 8)
                .OrderBy(monster => monster.EntityId)
                .FirstOrDefault();
            Assert.NotNull(target);
            var actor = target!;
            // Keep the real profile-bound skeleton identity and immutable raw
            // initialization, while setting only the captured live hit inputs
            // (tile, HP, resistance, item seed, and local-hit credit).
            var spawnTile = offsets.Skip(1)
                .Select(offset => (X: barrel.PositionX + offset.X, Y: barrel.PositionY + offset.Y))
                .FirstOrDefault(position => candidateWorld.IsWalkable(1, position.X, position.Y)
                    && !candidateWorld.IsOccupied(1, position.X, position.Y, [player], actor.EntityId));
            Assert.True(spawnTile != default,
                $"The controlled monster fixture for '{name}' found no open explosive-neighbor tile.");
            actor.ApplyKnockback(spawnTile.X, spawnTile.Y);

            var inputMonster = value.GetProperty("inputWorld").GetProperty("monsters").EnumerateArray()
                .Single(monster => monster.GetProperty("slot").GetInt32() == 199);
            var expectedMonster = value.GetProperty("expectedWorld").GetProperty("monsters").EnumerateArray()
                .Single(monster => monster.GetProperty("slot").GetInt32() == 199);
            var inputHitPointsQ6 = inputMonster.GetProperty("hitPoints").GetInt32();
            var expectedHitPointsQ6 = expectedMonster.GetProperty("hitPoints").GetInt32();
            actor.RestoreHitPointsQ6(inputHitPointsQ6, inputMonster.GetProperty("maxHitPoints").GetInt32());
            actor.ArmorClass = inputMonster.GetProperty("armorClass").GetInt32();
            var resistanceFlags = inputMonster.GetProperty("resistance").GetInt32();
            actor.FireResistance = (resistanceFlags & 16) != 0 ? 100
                : (resistanceFlags & 2) != 0 ? 75 : 0;
            actor.MonsterItemSeed = inputMonster.GetProperty("rndItemSeed").GetUInt32();
            actor.RestoreNativePlayerHitRecorded(inputMonster.GetProperty("whoHit").GetInt32() != 0);

            var beforeWorldItems = candidateWorld.WorldItems.Keys.ToHashSet();
            var inputItems = value.GetProperty("inputItems").GetProperty("items").EnumerateArray().ToArray();
            var expectedItems = value.GetProperty("expectedItems").GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(inputItems.Length, expectedItems.Length);
            var beforeActiveItemCount = Assert.Single(candidateWorld.CaptureCathedralItems()!.Levels,
                level => level.LevelId == 1).ActiveItemCount;
            Assert.Equal(initialRandom, candidateSimulation.RandomState);
            var result = candidateSimulation.BreakNativeBarrelAtEffectBoundary(
                player, barrel.EntityId, checked(candidateRuntime.LastAdvancedTick + 1), [player]);

            Assert.True(result.Succeeded, $"Captured explosive case '{name}' was refused: {result.Failure}.");
            Assert.Equal(value.GetProperty("expectedRandomState").GetUInt32(), candidateSimulation.RandomState);
            Assert.Equal(expectedHitPointsQ6, actor.HitPointsQ6);
            Assert.Equal(value.GetProperty("expectedPlayer").GetProperty("experience").GetUInt32(), player.Experience);
            var targetDamage = result.Events.Where(gameEvent => gameEvent.Kind == DiabloGameplayEventKind.Damage
                && gameEvent.TargetEntityId == actor.EntityId).ToArray();
            if (expectedHitPointsQ6 < inputHitPointsQ6) {
                var hit = Assert.Single(targetDamage);
                Assert.True(hit.AmountQ6 > 0);
                if (expectedHitPointsQ6 > 0)
                    Assert.Equal(inputHitPointsQ6 - expectedHitPointsQ6, hit.AmountQ6);
            } else {
                Assert.Empty(targetDamage);
            }

            var newItems = candidateWorld.WorldItems.Values
                .Where(item => !beforeWorldItems.Contains(item.EntityId)).ToArray();
            var expectedActiveCountDelta = value.GetProperty("expectedItems").GetProperty("activeCount").GetInt32()
                - value.GetProperty("inputItems").GetProperty("activeCount").GetInt32();
            Assert.Equal(expectedActiveCountDelta, newItems.Length);
            var afterActiveItemCount = Assert.Single(candidateWorld.CaptureCathedralItems()!.Levels,
                level => level.LevelId == 1).ActiveItemCount;
            Assert.Equal(beforeActiveItemCount + expectedActiveCountDelta, afterActiveItemCount);
            if (expectedHitPointsQ6 == 0) {
                var drop = Assert.Single(newItems);
                var newSlots = Enumerable.Range(0, expectedItems.Length)
                    .Where(index => expectedItems[index].GetProperty("itemIndex").GetInt32() >= 0
                        && inputItems[index].GetProperty("itemIndex").GetInt32() < 0)
                    .ToArray();
                var newSlot = Assert.Single(newSlots);
                var expectedDrop = expectedItems[newSlot];
                Assert.Equal(expectedDrop.GetProperty("seed").GetUInt32(), drop.ItemSeed);
                Assert.Equal(expectedDrop.GetProperty("value").GetInt32(), drop.State.Value);
                Assert.Equal(expectedDrop.GetProperty("itemIndex").GetInt32(), drop.State.ItemIndex);
                Assert.Equal("Gold", drop.State.BaseName);
            }
        }
    }

    private static int Chebyshev(int firstX, int firstY, int secondX, int secondY)
        => Math.Max(Math.Abs(firstX - secondX), Math.Abs(firstY - secondY));
}
