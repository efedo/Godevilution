using Devilution.Diablo;

using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeFatSpecialMeleeCursorTests
{
	[Fact]
	public void SpecialMeleeUsesSpecialGraphicClockAndEffectFrame()
	{
		var target = CreateTarget();
		DiabloCombatCommandProcessor.BeginNativeMelee(target, usesSpecialAttack: true);

		var steps = new List<AuthoritativeNativeMeleeStep>();
		for (var tick = 0; tick < 7; tick++)
			steps.Add(DiabloCombatCommandProcessor.AdvanceNativeMelee(target));

		Assert.Equal(new[] {
			new AuthoritativeNativeMeleeStep(0, false, false, true),
			new AuthoritativeNativeMeleeStep(0, false, false, true),
			new AuthoritativeNativeMeleeStep(1, false, false, true),
			new AuthoritativeNativeMeleeStep(1, false, false, true),
			new AuthoritativeNativeMeleeStep(2, true, false, true),
			new AuthoritativeNativeMeleeStep(2, true, false, true),
			new AuthoritativeNativeMeleeStep(3, false, true, true),
		}, steps);
		Assert.Null(target.NativeMeleeCursor);
	}

	[Fact]
	public void SpecialMeleeCursorSnapshotCopyKeepsItsAttackKindAndEffectTiming()
	{
		var source = CreateTarget();
		DiabloCombatCommandProcessor.BeginNativeMelee(source, usesSpecialAttack: true);
		for (var tick = 0; tick < 4; tick++)
			_ = DiabloCombatCommandProcessor.AdvanceNativeMelee(source);

		var savedCursor = source.CaptureCohortState().NativeMeleeCursor
			?? throw new InvalidOperationException("The special attack cursor should remain active.");
		Assert.True(savedCursor.UsesSpecialAttack);

		var restored = CreateTarget();
		restored.RestoreNativeMeleeCursor(savedCursor);
		var sourceTail = AdvanceToCompletion(source);
		var restoredTail = AdvanceToCompletion(restored);

		Assert.Equal(sourceTail, restoredTail);
		Assert.Equal(2, sourceTail.Count(step => step.EffectFrame && step.UsesSpecialAttack));
		Assert.DoesNotContain(sourceTail, step => step.EffectFrame && !step.UsesSpecialAttack);
	}

	[Fact]
	public void DefaultNativeMeleeCursorRemainsTheNormalAttack()
	{
		var target = CreateTarget();
		DiabloCombatCommandProcessor.BeginNativeMelee(target);

		var first = DiabloCombatCommandProcessor.AdvanceNativeMelee(target);

		Assert.Equal(new AuthoritativeNativeMeleeStep(0, false, false), first);
		Assert.False(target.NativeMeleeCursor!.UsesSpecialAttack);
	}

	[Fact]
	public void NativeFatSpecialSequenceWithNoEffectFrameCompletesWithoutAnEffect()
	{
		var target = CreateTarget(specialAttackEffectFrame: 0);
		DiabloCombatCommandProcessor.BeginNativeMelee(target, usesSpecialAttack: true);

		var steps = AdvanceToCompletion(target);

		Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 3 }, steps.Select(step => step.FrameIndex));
		Assert.All(steps, step => {
			Assert.True(step.UsesSpecialAttack);
			Assert.False(step.EffectFrame);
		});
		Assert.Null(target.NativeMeleeCursor);
	}

	private static IReadOnlyList<AuthoritativeNativeMeleeStep> AdvanceToCompletion(
		AuthoritativeCombatTarget target)
	{
		var steps = new List<AuthoritativeNativeMeleeStep>();
		while (target.NativeMeleeCursor is not null)
			steps.Add(DiabloCombatCommandProcessor.AdvanceNativeMelee(target));
		return steps;
	}

	private static AuthoritativeCombatTarget CreateTarget(int specialAttackEffectFrame = 3)
	{
		const uint entityId = 900;
		const uint monsterId = 52;
		const uint levelId = 5;
		var state = new AuthoritativeCathedralPopulationMonsterState {
			Slot = 0,
			Type = 52,
			Mode = 0,
			Goal = 1,
			UniqueType = byte.MaxValue,
			HitPoints = 6400,
			MaxHitPoints = 6400,
			AnimTicksPerFrame = 1,
			AnimNumberOfFrames = 1,
			Tile = new(10, 10),
			Future = new(10, 10),
			Old = new(10, 10),
			Leader = byte.MaxValue,
		};
		var initialization = new AuthoritativeNativeMonsterInitialization(
			new string('A', 64),
			state,
			0,
			0,
			3,
		specialAttackEffectFrame,
			new AuthoritativeNativeMonsterBinding(
				0,
				52,
				byte.MaxValue,
				entityId,
				monsterId,
				levelId,
				string.Empty),
			false) {
			CatacombsDepth = 5,
			NativeAttackFrameCount = 5,
			NativeAttackTicksPerFrame = 1,
			NativeSpecialAttackFrameCount = 4,
			NativeSpecialAttackTicksPerFrame = 2,
			NativeHitRecoveryFrameCount = 2,
			NativeHitRecoveryTicksPerFrame = 1,
		};
		var target = new AuthoritativeCombatTarget(
			entityId,
			10,
			10,
			hitPoints: 100,
			maxHitPoints: 100,
			monsterId: monsterId,
			levelId: levelId,
			nativeAi: "Fat",
			toHit: 10,
			toHitSpecial: 20,
			minDamage: 1,
			maxDamage: 3,
			minDamageSpecial: 4,
			maxDamageSpecial: 8);
		target.RestoreNativeInitialization(initialization, itemSeed: 7, aiSeed: 11);
		return target;
	}
}
