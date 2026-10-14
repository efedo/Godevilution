using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Exercises native melee and hit-recovery cursors through detached cohort restore. */
public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
	[Fact(Explicit = true)]
	public void NativeMeleeWindupAndRecoveryResumeFromFreshCohorts()
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
		const string sessionId = "native-melee-cohort-continuation";
		var template = activation.PlayerLoadouts.CreateTemplate(sessionId, rogue.ClassId,
			goldOverride: null, lifeOverride: null, manaOverride: null,
			startingLevelId: 1, startingPositionX: 57, startingPositionY: 40);
		var runtime = simulation.CreateRuntime(template);
		runtime.Write(sessionId, player => player.EntityId = 0x70000041);
		var player = runtime.Read(sessionId, value => value);
		var codec = new DiabloCohortCodec();
		var candidateFactory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
			activation.CohortCandidateFactory);

		// A trusted v4 save omitted the added animation rates. Hydrate only from this activated profile.
		var initialV5 = simulation.CaptureCohortState(runtime);
		Assert.Equal(DiabloCohortState.NativeCombatFormatVersion, initialV5.FormatVersion);
		Assert.DoesNotContain(initialV5.Projectiles, projectile => projectile.NativeWeaponArrow is not null);
		var legacyV4 = initialV5 with {
			FormatVersion = DiabloCohortState.CathedralItemFormatVersion,
			CombatTargets = initialV5.CombatTargets.Select(target => target with {
				Monster = target.Monster with { NativeMeleeCursor = null },
				NativeInitialization = target.NativeInitialization is { } initialization
					? initialization with {
						NativeAttackFrameCount = 0,
						NativeAttackTicksPerFrame = 0,
						NativeHitRecoveryFrameCount = 0,
						NativeHitRecoveryTicksPerFrame = 0,
						NativeWalkFrameCount = 0,
						NativeWalkTicksPerFrame = 0,
					}
					: null,
			}).ToArray(),
		};
		var hydratedLegacy = candidateFactory(legacyV4);
		var hydratedState = hydratedLegacy.Simulation.CaptureCohortState(hydratedLegacy.CreateRuntime());
		Assert.Equal(codec.Serialize(initialV5), codec.Serialize(hydratedState));

		// Native-combat state cannot be restored without the exact active owner.
		var unownedOptions = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules);
		Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance
			.CreateCohortCandidate(unownedOptions, initialV5));

		var eligibleAis = new HashSet<string>(StringComparer.Ordinal) {
			"Fallen", "Scavenger", "SkeletonMelee", "Zombie",
		};
		var meleeTarget = world.CombatTargets.Values
			.Where(target => target.LevelId == 1 && target.HitPointsQ6 > 0 && !target.IsPetrified
				&& !target.IsPlayerMinion && target.NativeInitialization is { } native
				&& eligibleAis.Contains(target.NativeAi)
				&& native.AttackEffectFrame > 1
				&& native.AttackEffectFrame < native.NativeAttackFrameCount)
			.OrderBy(target => DeterministicGridMetrics.ChebyshevDistance(
				player.PositionX, player.PositionY, target.PositionX, target.PositionY))
			.ThenByDescending(target => target.ToHit)
			.FirstOrDefault();
		Assert.True(meleeTarget is not null,
			"The imported L1 roster has no living melee actor with a reachable pre-effect attack frame.");
		var target = meleeTarget!;
		Assert.Equal(0, target.AggroRange);
		var targetInitialization = target.NativeInitialization!;
		var effectFrame = targetInitialization.AttackEffectFrame - 1;
		var drainedEvents = new List<DiabloGameplayEvent>();

		void AdvanceRuntime(ulong targetTick)
		{
			while (runtime.LastAdvancedTick < targetTick) {
				runtime.AdvanceTo(checked(runtime.LastAdvancedTick + 1));
				drainedEvents.AddRange(runtime.DrainEvents(sessionId));
			}
		}

		bool IsWithinPlayerNativeVision(int viewerX, int viewerY, int monsterX, int monsterY)
		{
			var dx = (long)monsterX - viewerX;
			var dy = (long)monsterY - viewerY;
			return dx * dx + dy * dy <= (long)player.LightRadius * player.LightRadius
				&& world.HasAiLineOfSight(player.LevelId, viewerX, viewerY, monsterX, monsterY);
		}
		bool IsVisibleAdjacency()
			=> DeterministicGridMetrics.ChebyshevDistance(
				player.PositionX, player.PositionY, target.PositionX, target.PositionY) == 1
			&& IsWithinPlayerNativeVision(
				player.PositionX, player.PositionY, target.PositionX, target.PositionY);
		for (var actionCount = 0; actionCount < 256 && !IsVisibleAdjacency();
			actionCount++) {
			Assert.True(target.HitPointsQ6 > 0,
				$"The chosen native melee actor died during ordinary approach; entity={target.EntityId}, "
					+ $"player=({player.PositionX},{player.PositionY}), target=({target.PositionX},{target.PositionY}).");
			bool Goal(int x, int y)
				=> DeterministicGridMetrics.ChebyshevDistance(x, y, target.PositionX, target.PositionY) == 1
					&& world.IsWalkable(player.LevelId, x, y)
					&& IsWithinPlayerNativeVision(x, y, target.PositionX, target.PositionY)
					&& !world.IsOccupied(player.LevelId, x, y, [player], player.EntityId);
			var step = DeterministicGridNavigation.FindStepToGoal(
				player.PositionX,
				player.PositionY,
				maximumDepth: 256,
				Goal,
				(x, y) => world.IsWalkable(player.LevelId, x, y),
				(x, y) => world.IsOccupied(player.LevelId, x, y, [player], player.EntityId));
			Assert.True(step is not null,
				$"No ordinary movement path reaches native actor {target.EntityId}; "
					+ $"player=({player.PositionX},{player.PositionY}), target=({target.PositionX},{target.PositionY}).");
			_ = MoveOneCell(simulation, runtime, world, player, step!.Value, AdvanceRuntime);
		}
		Assert.InRange(DeterministicGridMetrics.ChebyshevDistance(
			player.PositionX, player.PositionY, target.PositionX, target.PositionY), 0L, 1L);
		Assert.True(IsVisibleAdjacency(),
			$"The native melee actor has no visible adjacent player tile; entity={target.EntityId}, "
				+ $"AI={target.NativeAi}, player=({player.PositionX},{player.PositionY}), "
				+ $"target=({target.PositionX},{target.PositionY}).");

		for (var tickCount = 0; tickCount < 512; tickCount++) {
			if (target.NativeMeleeCursor is { HitRecovery: false } cursor
				&& cursor.FrameIndex < effectFrame)
				break;
			AdvanceRuntime(checked(runtime.LastAdvancedTick + 1));
			Assert.True(player.Life > 0,
				$"The Rogue died before a native melee windup could be captured; target={target.EntityId}, "
					+ $"cursor={target.NativeMeleeCursor}, events={drainedEvents.Count}.");
			if (tickCount == 511)
				Assert.Fail($"No pre-effect melee cursor appeared for native actor {target.EntityId}; "
					+ $"AI={target.NativeAi}, frameCount={targetInitialization.NativeAttackFrameCount}, "
					+ $"effectFrame={targetInitialization.AttackEffectFrame}, "
					+ $"aggroRange={target.AggroRange}, activeForTicks={target.ActiveForTicks}, "
					+ $"playerMode={player.CombatMode}, lightRadius={player.LightRadius}, "
					+ $"nativeVisionRay={world.HasAiLineOfSight(player.LevelId, player.PositionX, player.PositionY, target.PositionX, target.PositionY)}, "
					+ $"withinPlayerNativeVision={IsWithinPlayerNativeVision(player.PositionX, player.PositionY, target.PositionX, target.PositionY)}, "
					+ $"scavengerHealingTicks={target.ScavengerHealingTicks}, "
					+ $"scavengerCorpse={target.ScavengerCorpseEntityId}, "
					+ $"position=({target.PositionX},{target.PositionY}), "
					+ $"player=({player.PositionX},{player.PositionY}).");
		}
		Assert.Null(player.CurrentAction);
		var windupCursor = Assert.IsType<AuthoritativeNativeMeleeCursor>(target.NativeMeleeCursor);
		Assert.False(windupCursor.HitRecovery);
		Assert.True(windupCursor.FrameIndex < effectFrame);
		Assert.True(targetInitialization.NativeHitRecoveryFrameCount > 0);
		Assert.True(targetInitialization.NativeHitRecoveryTicksPerFrame >= 0);

		var windupState = simulation.CaptureCohortState(runtime);
		var windupBytes = codec.Serialize(windupState);
		var restoredWindup = candidateFactory(codec.Deserialize(windupBytes));
		var windupRuntime = restoredWindup.CreateRuntime();
		var restoredTarget = Assert.Single(restoredWindup.Simulation.WorldState.CombatTargets.Values,
			value => value.EntityId == target.EntityId);
		var restoredPlayer = windupRuntime.Read(sessionId, value => value);
		Assert.Equal(windupCursor, restoredTarget.NativeMeleeCursor);
		Assert.Equal(windupBytes,
			codec.Serialize(restoredWindup.Simulation.CaptureCohortState(windupRuntime)));

		// Fork the real wind-up into the hit-recovery phase and verify a fresh cohort resumes it.
		var interruptedAttack = candidateFactory(codec.Deserialize(windupBytes));
		var interruptedRuntime = interruptedAttack.CreateRuntime();
		var interruptedTarget = Assert.Single(interruptedAttack.Simulation.WorldState.CombatTargets.Values,
			value => value.EntityId == target.EntityId);
		Assert.False(interruptedTarget.NativeMeleeCursor!.HitRecovery);
		DiabloCombatCommandProcessor.BeginNativeHitRecovery(interruptedTarget);
		var recoveryState = interruptedAttack.Simulation.CaptureCohortState(interruptedRuntime);
		var recoveryBytes = codec.Serialize(recoveryState);
		var restoredRecovery = candidateFactory(codec.Deserialize(recoveryBytes));
		var recoveryRuntime = restoredRecovery.CreateRuntime();
		var restoredRecoveryTarget = Assert.Single(restoredRecovery.Simulation.WorldState.CombatTargets.Values,
			value => value.EntityId == target.EntityId);
		Assert.True(interruptedTarget.NativeMeleeCursor!.HitRecovery);
		Assert.Equal(recoveryBytes,
			codec.Serialize(restoredRecovery.Simulation.CaptureCohortState(recoveryRuntime)));
		var expectedRecoverySteps = (targetInitialization.NativeHitRecoveryFrameCount - 1)
			* Math.Max(1, targetInitialization.NativeHitRecoveryTicksPerFrame) + 1;
		var recoverySteps = 0;
		while (interruptedTarget.NativeMeleeCursor is not null) {
			var sourceStep = DiabloCombatCommandProcessor.AdvanceNativeMelee(interruptedTarget);
			var resumedStep = DiabloCombatCommandProcessor.AdvanceNativeMelee(restoredRecoveryTarget);
			recoverySteps++;
			Assert.Equal(sourceStep, resumedStep);
			Assert.False(sourceStep.EffectFrame);
			Assert.Equal(interruptedTarget.NativeMeleeCursor, restoredRecoveryTarget.NativeMeleeCursor);
			Assert.Equal(codec.Serialize(interruptedAttack.Simulation.CaptureCohortState(interruptedRuntime)),
				codec.Serialize(restoredRecovery.Simulation.CaptureCohortState(recoveryRuntime)));
		}
		Assert.Equal(expectedRecoverySteps, recoverySteps);

		var invalidCursorState = windupState with {
			CombatTargets = windupState.CombatTargets.Select(value => value.Monster.EntityId == target.EntityId
				? value with { Monster = value.Monster with {
					NativeMeleeCursor = windupCursor with {
						FrameIndex = targetInitialization.NativeAttackFrameCount,
					},
				} }
				: value).ToArray(),
		};
		Assert.Throws<InvalidDataException>(() => candidateFactory(invalidCursorState));
		var invalidTimingState = windupState with {
			CombatTargets = windupState.CombatTargets.Select(value => value.Monster.EntityId == target.EntityId
				? value with { NativeInitialization = value.NativeInitialization! with {
					NativeHitRecoveryFrameCount = targetInitialization.NativeHitRecoveryFrameCount + 1,
				} }
				: value).ToArray(),
		};
		Assert.Throws<InvalidDataException>(() => candidateFactory(invalidTimingState));
		Assert.Equal(windupBytes, codec.Serialize(simulation.CaptureCohortState(runtime)));

		var effectWasObserved = false;
		var targetDamageEvents = new List<DiabloGameplayEvent>();
		DiabloCohortState? postEffectState = null;
		for (var tickCount = 0; tickCount < 512; tickCount++) {
			var preAdvance = target.NativeMeleeCursor;
			var tick = checked(runtime.LastAdvancedTick + 1);
			runtime.AdvanceTo(tick);
			windupRuntime.AdvanceTo(tick);
			var sourceEvents = runtime.DrainEvents(sessionId).ToArray();
			var resumedEvents = windupRuntime.DrainEvents(sessionId).ToArray();
			Assert.Equal(sourceEvents, resumedEvents);
			var meleeDamageEvents = sourceEvents.Where(value => value.Kind == DiabloGameplayEventKind.Damage
				&& value.SourceEntityId == target.EntityId && value.TargetEntityId == player.EntityId
				&& value.AmountQ6 > 0).ToArray();
			if (preAdvance is not { HitRecovery: false, FrameIndex: var damageFrame } || damageFrame != effectFrame)
				Assert.Empty(meleeDamageEvents);
			targetDamageEvents.AddRange(meleeDamageEvents);
			if (preAdvance is { HitRecovery: false, FrameIndex: var frame } && frame == effectFrame)
				effectWasObserved = true;
			Assert.Equal(target.HitPointsQ6, restoredTarget.HitPointsQ6);
			Assert.Equal(player.Life, restoredPlayer.Life);
			Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
				codec.Serialize(restoredWindup.Simulation.CaptureCohortState(windupRuntime)));
			if (effectWasObserved && target.NativeMeleeCursor is { HitRecovery: false } current
				&& current.FrameIndex > effectFrame) {
				postEffectState = simulation.CaptureCohortState(runtime);
				break;
			}
			Assert.True(player.Life > 0,
				$"The Rogue died before the saved swing passed its effect frame; target={target.EntityId}, "
					+ $"cursor={target.NativeMeleeCursor}, damageEvents={targetDamageEvents.Count}.");
		}
		Assert.NotNull(postEffectState);
		Assert.True(effectWasObserved); // The source RNG may legitimately miss this swing.
		var postEffectCursor = Assert.IsType<AuthoritativeNativeMeleeCursor>(
			postEffectState!.CombatTargets.Single(value => value.Monster.EntityId == target.EntityId)
				.Monster.NativeMeleeCursor);
		Assert.False(postEffectCursor.HitRecovery);
		Assert.True(postEffectCursor.FrameIndex > effectFrame);

		var postEffectBytes = codec.Serialize(postEffectState);
		var restoredPostEffect = candidateFactory(codec.Deserialize(postEffectBytes));
		var postEffectRuntime = restoredPostEffect.CreateRuntime();
		var restoredPostEffectTarget = Assert.Single(restoredPostEffect.Simulation.WorldState.CombatTargets.Values,
			value => value.EntityId == target.EntityId);
		Assert.Equal(postEffectBytes,
			codec.Serialize(restoredPostEffect.Simulation.CaptureCohortState(postEffectRuntime)));
		var remainingAttackFrames = 0;
		while (target.NativeMeleeCursor is not null) {
			var sourceStep = DiabloCombatCommandProcessor.AdvanceNativeMelee(target);
			var resumedStep = DiabloCombatCommandProcessor.AdvanceNativeMelee(restoredPostEffectTarget);
			remainingAttackFrames++;
			Assert.Equal(sourceStep, resumedStep);
			Assert.False(sourceStep.EffectFrame);
			Assert.Equal(target.NativeMeleeCursor, restoredPostEffectTarget.NativeMeleeCursor);
			Assert.Equal(target.HitPointsQ6, restoredPostEffectTarget.HitPointsQ6);
			Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
				codec.Serialize(restoredPostEffect.Simulation.CaptureCohortState(postEffectRuntime)));
			Assert.InRange(remainingAttackFrames, 1, targetInitialization.NativeAttackTicksPerFrame
				* targetInitialization.NativeAttackFrameCount + targetInitialization.NativeAttackFrameCount);
		}
		Assert.Null(target.NativeMeleeCursor);
	}
}
