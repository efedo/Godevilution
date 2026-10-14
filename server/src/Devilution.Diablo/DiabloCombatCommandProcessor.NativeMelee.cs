namespace Devilution.Diablo;

/** Result of one native melee animation update, reported against its pre-advance frame. */
internal readonly record struct AuthoritativeNativeMeleeStep(
    int FrameIndex,
    bool EffectFrame,
    bool Completed);

public sealed partial class DiabloCombatCommandProcessor
{
	/** Starts the normal attack animation after the AI has admitted a melee attack. */
	internal static void BeginNativeMelee(AuthoritativeCombatTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);
		var initialization = target.NativeInitialization
			?? throw new InvalidOperationException("A native melee animation requires native monster initialization.");
		if (initialization.NativeAttackFrameCount <= 0)
			throw new InvalidOperationException("Native normal attack animation metadata is unavailable.");
		if (target.NativeMeleeCursor is not null)
			throw new InvalidOperationException("A native melee animation is already active.");

		target.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(0, 0));
	}

	/** Starts the imported hit-recovery animation after native hard-hit admission. */
	internal static void BeginNativeHitRecovery(AuthoritativeCombatTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);
		var initialization = target.NativeInitialization
			?? throw new InvalidOperationException("Native hit recovery requires native monster initialization.");
		if (initialization.NativeHitRecoveryFrameCount <= 0)
			throw new InvalidOperationException("Native hit-recovery animation metadata is unavailable.");

		target.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(0, 0) {
			HitRecovery = true,
		});
	}

	/**
	 * Checks the effect on the current frame, then advances the source animation
	 * cursor. The live current enemy is resolved by the caller when EffectFrame
	 * is true; no victim identity is retained by the animation.
	 */
	internal static AuthoritativeNativeMeleeStep AdvanceNativeMelee(AuthoritativeCombatTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);
		var cursor = target.NativeMeleeCursor
			?? throw new InvalidOperationException("A native melee animation is not active.");
		var initialization = target.NativeInitialization
			?? throw new InvalidDataException("A native melee cursor lost its initialization metadata.");
		var frameCount = cursor.HitRecovery
			? initialization.NativeHitRecoveryFrameCount
			: initialization.NativeAttackFrameCount;
		var sourceTicksPerFrame = cursor.HitRecovery
			? initialization.NativeHitRecoveryTicksPerFrame
			: initialization.NativeAttackTicksPerFrame;
		if (frameCount <= 0)
			throw new InvalidDataException("Native animation frame metadata is unavailable.");

		if (target.HitPointsQ6 <= 0) {
			target.RestoreNativeMeleeCursor(null);
			return new AuthoritativeNativeMeleeStep(cursor.FrameIndex, EffectFrame: false, Completed: true);
		}
		target.RestoreNativeMeleeCursor(cursor);

		// Native MonsterPetrified skips AnimationInfo::processAnimation.
		if (target.IsPetrified)
			return new AuthoritativeNativeMeleeStep(cursor.FrameIndex, EffectFrame: false, Completed: false);

		var effectFrame = !cursor.HitRecovery
			&& cursor.FrameIndex == initialization.AttackEffectFrame - 1;
		if (cursor.FrameIndex == frameCount - 1) {
			// Native enters Stand before processAnimation when already on the last frame.
			target.RestoreNativeMeleeCursor(null);
			return new AuthoritativeNativeMeleeStep(cursor.FrameIndex, effectFrame, Completed: true);
		}

		var nextTickCounter = cursor.TickCounter + 1;
		var nextFrameIndex = cursor.FrameIndex;
		var ticksPerFrame = Math.Max(1, sourceTicksPerFrame);
		if (nextTickCounter >= ticksPerFrame) {
			nextTickCounter = 0;
			nextFrameIndex++;
		}
		target.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(nextFrameIndex, nextTickCounter) {
			HitRecovery = cursor.HitRecovery,
		});
		return new AuthoritativeNativeMeleeStep(cursor.FrameIndex, effectFrame, Completed: false);
	}

	/** Called only when the native hit-recovery transition interrupts a swing. */
	internal static void InterruptNativeMelee(AuthoritativeCombatTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);
		target.RestoreNativeMeleeCursor(null);
	}
}
