namespace Devilution.Diablo;

/** Result of one native melee animation update, reported against its pre-advance frame. */
internal readonly record struct AuthoritativeNativeMeleeStep(
    int FrameIndex,
    bool EffectFrame,
    bool Completed,
    bool UsesSpecialAttack = false);

public sealed partial class DiabloCombatCommandProcessor
{
	/** Starts the selected native attack animation after the AI admits a melee attack. */
	internal static void BeginNativeMelee(
		AuthoritativeCombatTarget target,
		bool usesSpecialAttack = false)
	{
		ArgumentNullException.ThrowIfNull(target);
		var initialization = target.NativeInitialization
			?? throw new InvalidOperationException("A native melee animation requires native monster initialization.");
		var frameCount = usesSpecialAttack
			? initialization.NativeSpecialAttackFrameCount
			: initialization.NativeAttackFrameCount;
		if (frameCount <= 0)
			throw new InvalidOperationException(usesSpecialAttack
				? "Native special attack animation metadata is unavailable."
				: "Native normal attack animation metadata is unavailable.");
		if (target.NativeMeleeCursor is not null)
			throw new InvalidOperationException("A native melee animation is already active.");

		target.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(0, 0) {
			UsesSpecialAttack = usesSpecialAttack,
		});
	}

	/**
	 * Native StartRangedAttack (Attack graphic) or StartRangedSpecialAttack (Special graphic):
	 * the cursor releases missile with damage on its effect frame.
	 */
	internal static void BeginNativeRangedAttack(
		AuthoritativeCombatTarget target,
		string missile,
		int damage,
		bool special)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentException.ThrowIfNullOrEmpty(missile);
		if (target.NativeMeleeCursor is not null)
			throw new InvalidOperationException("A native animation is already active.");
		var initialization = target.NativeInitialization
			?? throw new InvalidOperationException("A native ranged attack requires native monster initialization.");
		if ((special ? initialization.NativeSpecialAttackFrameCount : initialization.NativeAttackFrameCount) <= 0)
			throw new InvalidOperationException("Native ranged attack animation metadata is unavailable.");
		target.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(0, 0) {
			UsesSpecialAttack = special,
			RangedMissile = missile,
			RangedDamage = damage,
		});
	}

	/** Starts a native Sneak fade on the Special graphic and applies this update's processAnimation. */
	internal static void BeginNativeFade(AuthoritativeCombatTarget target, AuthoritativeNativeFade fade)
	{
		ArgumentNullException.ThrowIfNull(target);
		if (fade == AuthoritativeNativeFade.None)
			throw new ArgumentOutOfRangeException(nameof(fade));
		if ((target.NativeInitialization?.NativeSpecialAttackFrameCount ?? 0) <= 0)
			throw new InvalidOperationException("Native fade animation metadata is unavailable.");
		if (target.NativeMeleeCursor is not null)
			throw new InvalidOperationException("A native animation is already active.");
		target.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(0, 0) { Fade = fade });
		_ = AdvanceNativeMelee(target);
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
		if (cursor.HitRecovery && cursor.UsesSpecialAttack)
			throw new InvalidDataException("Native hit recovery cannot retain a special attack cursor.");
		// Fades use the Special graphic. FadeOut runs it backwards from the last
		// frame to frame 0; counting forwards gives the same completion tick.
		var special = cursor.UsesSpecialAttack || cursor.Fade != AuthoritativeNativeFade.None;
		var frameCount = cursor.HitRecovery
			? initialization.NativeHitRecoveryFrameCount
			: special
				? initialization.NativeSpecialAttackFrameCount
				: initialization.NativeAttackFrameCount;
		var sourceTicksPerFrame = cursor.HitRecovery
			? initialization.NativeHitRecoveryTicksPerFrame
			: special
				? initialization.NativeSpecialAttackTicksPerFrame
				: initialization.NativeAttackTicksPerFrame;
		if (frameCount <= 0)
			throw new InvalidDataException("Native animation frame metadata is unavailable.");

		if (target.HitPointsQ6 <= 0) {
			target.RestoreNativeMeleeCursor(null);
			return new AuthoritativeNativeMeleeStep(
				cursor.FrameIndex,
				EffectFrame: false,
				Completed: true,
				UsesSpecialAttack: cursor.UsesSpecialAttack);
		}
		target.RestoreNativeMeleeCursor(cursor);

		// Native MonsterPetrified skips AnimationInfo::processAnimation.
		if (target.IsPetrified)
			return new AuthoritativeNativeMeleeStep(
				cursor.FrameIndex,
				EffectFrame: false,
				Completed: false,
				UsesSpecialAttack: cursor.UsesSpecialAttack);

		var effectFrameIndex = cursor.UsesSpecialAttack
			? initialization.SpecialAttackEffectFrame
			: initialization.AttackEffectFrame;
		// MonsterRangedSpecialAttack also requires the frame's first tick.
		var effectFrame = !cursor.HitRecovery && cursor.Fade == AuthoritativeNativeFade.None
			&& cursor.FrameIndex == effectFrameIndex - 1
			&& !(cursor.RangedMissile is not null && cursor.UsesSpecialAttack && cursor.TickCounter != 0);
		if (cursor.FrameIndex == frameCount - 1) {
			// Native enters Stand before processAnimation when already on the last frame.
			target.RestoreNativeMeleeCursor(null);
			return new AuthoritativeNativeMeleeStep(
				cursor.FrameIndex,
				effectFrame,
				Completed: true,
				UsesSpecialAttack: cursor.UsesSpecialAttack);
		}

		var nextTickCounter = cursor.TickCounter + 1;
		var nextFrameIndex = cursor.FrameIndex;
		var ticksPerFrame = Math.Max(1, sourceTicksPerFrame);
		if (nextTickCounter >= ticksPerFrame) {
			nextTickCounter = 0;
			nextFrameIndex++;
		}
		target.RestoreNativeMeleeCursor(cursor with { FrameIndex = nextFrameIndex, TickCounter = nextTickCounter });
		return new AuthoritativeNativeMeleeStep(
			cursor.FrameIndex,
			effectFrame,
			Completed: false,
			UsesSpecialAttack: cursor.UsesSpecialAttack);
	}

	/** Called only when the native hit-recovery transition interrupts a swing. */
	internal static void InterruptNativeMelee(AuthoritativeCombatTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);
		target.RestoreNativeMeleeCursor(null);
	}
}
