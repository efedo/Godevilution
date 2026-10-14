using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Verifies that a real native monster walk survives an exact fresh cohort restore. */
public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
	[Fact(Explicit = true)]
	public void NativeWalkCursorContinuesAcrossFreshCohort()
	{
		var metadataDirectory = Environment.GetEnvironmentVariable(
			"GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY");
		var runtimeTablePath = Environment.GetEnvironmentVariable(
			"GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
		var footprintTablePath = Environment.GetEnvironmentVariable(
			"GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH");
		if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(runtimeTablePath)
			|| string.IsNullOrWhiteSpace(footprintTablePath)) {
			Assert.Skip("The exact external Cathedral metadata, runtime catalog, and cursor footprint paths are required.");
			return;
		}

		using var fixture = new NativeFixture(metadataDirectory, runtimeTablePath, footprintTablePath);
		var activation = DiabloModule.Instance.CathedralNativePopulationContentBootstrap
			.ActivateNativePopulation(fixture.Content);
		var simulation = activation.Simulation;
		var world = simulation.WorldState;
		var rogue = activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
		const string sessionId = "native-walk-cohort-continuation";
		var template = activation.PlayerLoadouts.CreateTemplate(sessionId, rogue.ClassId,
			goldOverride: null, lifeOverride: null, manaOverride: null,
			startingLevelId: 1, startingPositionX: 57, startingPositionY: 40);
		var runtime = simulation.CreateRuntime(template);
		runtime.Write(sessionId, player => player.EntityId = 0x70000042);
		var player = runtime.Read(sessionId, value => value);
		var codec = new DiabloCohortCodec();
		var candidateFactory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
			activation.CohortCandidateFactory);
		var acceptedMovementCommands = 0;

		void AdvanceOneTick()
		{
			runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
			_ = runtime.DrainEvents(sessionId);
		}

		AuthoritativeCombatTarget? FindWalkingTarget()
			=> world.CombatTargets.Values.Where(target => target.LevelId == 1 && target.HitPointsQ6 > 0
				&& !target.IsPetrified && !target.IsPlayerMinion && target.NativeWalkCursor is not null)
				.OrderBy(target => target.EntityId).FirstOrDefault();

		var initialPosition = (player.PositionX, player.PositionY);
		AuthoritativeCombatTarget? walkingTarget = null;
		for (var movementAttempt = 0; movementAttempt < 128 && walkingTarget is null; movementAttempt++) {
			if (player.CurrentAction is null) {
				var approachTarget = world.CombatTargets.Values.Where(target => target.LevelId == 1
						&& target.HitPointsQ6 > 0 && !target.IsPetrified && !target.IsPlayerMinion
						&& target.NativeInitialization is { NativeWalkFrameCount: > 1 }
						&& DeterministicGridMetrics.ChebyshevDistance(player.PositionX, player.PositionY,
							target.PositionX, target.PositionY) > 1)
					.OrderBy(target => DeterministicGridMetrics.ChebyshevDistance(
						player.PositionX, player.PositionY, target.PositionX, target.PositionY))
					.ThenBy(target => target.EntityId).FirstOrDefault();
				if (approachTarget is null)
					break;

				bool Goal(int x, int y)
					=> DeterministicGridMetrics.ChebyshevDistance(x, y,
						approachTarget.PositionX, approachTarget.PositionY) == 1
						&& world.IsWalkable(player.LevelId, x, y)
						&& !world.IsOccupied(player.LevelId, x, y, [player], player.EntityId);
				var step = DeterministicGridNavigation.FindStepToGoal(
					player.PositionX,
					player.PositionY,
					maximumDepth: 256,
					Goal,
					(x, y) => world.IsWalkable(player.LevelId, x, y),
					(x, y) => world.IsOccupied(player.LevelId, x, y, [player], player.EntityId));
				if (step is null) {
					AdvanceOneTick();
					walkingTarget = FindWalkingTarget();
					continue;
				}

				var moveTick = checked(runtime.LastAdvancedTick + 1);
				var result = simulation.Execute(player,
					new DiabloMoveToIntent(step.Value.X, step.Value.Y), moveTick, [player]);
				if (!result.Succeeded) {
					AdvanceOneTick();
					walkingTarget = FindWalkingTarget();
					continue;
				}
				acceptedMovementCommands++;
			}

			for (var actionTick = 0; actionTick < 256 && player.CurrentAction is not null
				&& walkingTarget is null; actionTick++) {
				AdvanceOneTick();
				walkingTarget = FindWalkingTarget();
			}
			walkingTarget ??= FindWalkingTarget();
		}

		Assert.True(walkingTarget is not null,
			$"Ordinary Rogue movement from ({initialPosition.PositionX},{initialPosition.PositionY}) "
				+ $"did not admit a native monster walk; moves={runtime.LastAdvancedTick}, "
				+ $"livingNative={world.CombatTargets.Values.Count(target => target.LevelId == 1
					&& target.HitPointsQ6 > 0 && target.NativeInitialization?.NativeWalkFrameCount > 1)}.");
		Assert.True(acceptedMovementCommands > 0,
			"The native walk was not admitted after an ordinary player movement command.");
		Assert.True((player.PositionX, player.PositionY) != initialPosition || player.IsMoving,
			"The native walk capture did not follow actual player movement from the known L1 spawn.");
		var target = walkingTarget!;
		var initialization = target.NativeInitialization
			?? throw new InvalidDataException("The walking native actor lost its initialization binding.");
		var initialCursor = target.NativeWalkCursor
			?? throw new InvalidDataException("The selected native walk has no active cursor.");
		Assert.True(initialization.NativeWalkFrameCount > 1);
		Assert.True(initialization.NativeWalkTicksPerFrame >= 0);
		Assert.InRange(initialCursor.FrameIndex, -1, initialization.NativeWalkFrameCount - 1);
		Assert.InRange(initialCursor.TickCounter, 0, Math.Max(1, initialization.NativeWalkTicksPerFrame) - 1);
		Assert.True(target.IsMoving);
		var walkOrigin = (target.PositionX, target.PositionY);
		var walkDestination = (target.FuturePositionX, target.FuturePositionY);
		Assert.NotEqual(walkOrigin, walkDestination);
		Assert.True(world.IsOccupied(target.LevelId, walkOrigin.PositionX, walkOrigin.PositionY,
			[player], player.EntityId), "The active native walk must keep its origin occupied.");
		Assert.True(world.IsOccupied(target.LevelId, walkDestination.FuturePositionX, walkDestination.FuturePositionY,
			[player], player.EntityId), "The active native walk must reserve its future destination.");

		var captured = simulation.CaptureCohortState(runtime);
		var encoded = codec.Serialize(captured);
		var restored = candidateFactory(codec.Deserialize(encoded));
		var restoredRuntime = restored.CreateRuntime();
		var restoredWorld = restored.Simulation.WorldState;
		var restoredTarget = Assert.Single(restoredWorld.CombatTargets.Values,
			value => value.EntityId == target.EntityId);
		var restoredPlayer = restoredRuntime.Read(sessionId, value => value);
		Assert.Equal(initialCursor, restoredTarget.NativeWalkCursor);
		Assert.Equal(encoded, codec.Serialize(restored.Simulation.CaptureCohortState(restoredRuntime)));
		Assert.Equal(captured.RandomState,
			restored.Simulation.CaptureCohortState(restoredRuntime).RandomState);

		var maximumContinuationTicks = Math.Clamp(
			initialization.NativeWalkFrameCount * Math.Max(1, initialization.NativeWalkTicksPerFrame)
				+ initialization.NativeWalkFrameCount + 16,
			16, 1024);
		var completed = false;
		for (var continuationTick = 0; continuationTick < maximumContinuationTicks; continuationTick++) {
			var tick = checked(runtime.LastAdvancedTick + 1);
			runtime.AdvanceTo(tick);
			restoredRuntime.AdvanceTo(tick);
			var sourceEvents = runtime.DrainEvents(sessionId).ToArray();
			var resumedEvents = restoredRuntime.DrainEvents(sessionId).ToArray();
			Assert.Equal(sourceEvents, resumedEvents);

			var sourceState = simulation.CaptureCohortState(runtime);
			var resumedState = restored.Simulation.CaptureCohortState(restoredRuntime);
			Assert.Equal(sourceState.RandomState, resumedState.RandomState);
			Assert.Equal(codec.Serialize(sourceState), codec.Serialize(resumedState));
			Assert.Equal(target.NativeWalkCursor, restoredTarget.NativeWalkCursor);
			Assert.Equal((target.PositionX, target.PositionY),
				(restoredTarget.PositionX, restoredTarget.PositionY));
			Assert.Equal((target.FuturePositionX, target.FuturePositionY),
				(restoredTarget.FuturePositionX, restoredTarget.FuturePositionY));
			Assert.Equal(target.IsMoving, restoredTarget.IsMoving);
			if (target.NativeWalkCursor is not null) {
				Assert.True(target.IsMoving);
				Assert.True(world.IsOccupied(target.LevelId, target.PositionX, target.PositionY,
					[player], player.EntityId));
				Assert.True(world.IsOccupied(target.LevelId, target.FuturePositionX, target.FuturePositionY,
					[player], player.EntityId));
			}
			if ((target.PositionX, target.PositionY) == walkDestination
				&& (target.PositionX, target.PositionY) != walkOrigin) {
				completed = true;
				break;
			}
			Assert.True(target.HitPointsQ6 > 0 && !target.IsPetrified,
				$"The native walk was interrupted before its terminal frame; actor={target.EntityId}, "
					+ $"cursor={target.NativeWalkCursor}, position=({target.PositionX},{target.PositionY}), "
					+ $"destination=({walkDestination.FuturePositionX},{walkDestination.FuturePositionY}).");
		}
		Assert.True(completed,
			$"The restored native walk did not commit its reserved destination in {maximumContinuationTicks} ticks; "
				+ $"cursor={target.NativeWalkCursor}, position=({target.PositionX},{target.PositionY}), "
				+ $"destination=({walkDestination.FuturePositionX},{walkDestination.FuturePositionY}).");
		Assert.Equal(walkDestination, (target.PositionX, target.PositionY));
		Assert.Equal((target.PositionX, target.PositionY),
			(restoredTarget.PositionX, restoredTarget.PositionY));
		Assert.Equal(simulation.CaptureCohortState(runtime).RandomState,
			restored.Simulation.CaptureCohortState(restoredRuntime).RandomState);
	}
}
