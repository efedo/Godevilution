using System;

namespace Devilution.Domain;

/// <summary>
/// Native controls for distributing animation frames over game ticks.
/// </summary>
[Flags]
public enum NativeAnimationDistributionFlags : byte
{
    None = 0,
    ProcessAnimationPending = 1,
    SkipsDelayOfLastFrame = 2,
    RepeatedAction = 4,
}

/// <summary>
/// Pure managed equivalent of the scalar and distribution state in
/// legacy-native/engine/animationinfo.{h,cpp}. All nine cursor scalars, including private
/// distribution continuation, are retained. No sprite or global clock is included.
/// </summary>
public sealed record NativeAnimationState
{
    /// <summary>Native fixed-point fraction base used for render interpolation.</summary>
    public const int BaseValueFraction = 128;
    private const int SByteMinimum = sbyte.MinValue;
    private const int SByteMaximum = sbyte.MaxValue;
    private const int UInt16Maximum = ushort.MaxValue;
    private const int Int16Minimum = short.MinValue;
    private const int Int16Maximum = short.MaxValue;

    public NativeAnimationState(
        int numberOfFrames,
        int ticksPerFrame,
        int tickCounterOfCurrentFrame,
        int currentFrame,
        bool isPetrified,
        int relevantFramesForDistributing,
        int skippedFramesFromPreviousAnimation,
        int tickModifier,
        int ticksSinceSequenceStarted)
    {
        NumberOfFrames = RequireSByte(nameof(numberOfFrames), numberOfFrames);
        TicksPerFrame = RequireSByte(nameof(ticksPerFrame), ticksPerFrame);
        TickCounterOfCurrentFrame = RequireSByte(nameof(tickCounterOfCurrentFrame), tickCounterOfCurrentFrame);
        CurrentFrame = RequireSByte(nameof(currentFrame), currentFrame);
        IsPetrified = isPetrified;
        RelevantFramesForDistributing = RequireSByte(nameof(relevantFramesForDistributing), relevantFramesForDistributing);
        SkippedFramesFromPreviousAnimation = RequireSByte(nameof(skippedFramesFromPreviousAnimation), skippedFramesFromPreviousAnimation);
        TickModifier = RequireUInt16(nameof(tickModifier), tickModifier);
        TicksSinceSequenceStarted = RequireInt16(nameof(ticksSinceSequenceStarted), ticksSinceSequenceStarted);
    }

    /// <summary>Zero-initialized native AnimationInfo state.</summary>
    public static NativeAnimationState Empty { get; } = new(0, 0, 0, 0, false, 0, 0, 0, 0);

    /// <summary>Rehydrates a detached native cursor after validating every stored width.</summary>
    public static NativeAnimationState Restore(
        int numberOfFrames,
        int ticksPerFrame,
        int tickCounterOfCurrentFrame,
        int currentFrame,
        bool isPetrified,
        int relevantFramesForDistributing,
        int skippedFramesFromPreviousAnimation,
        int tickModifier,
        int ticksSinceSequenceStarted)
    {
        return new NativeAnimationState(
            numberOfFrames,
            ticksPerFrame,
            tickCounterOfCurrentFrame,
            currentFrame,
            isPetrified,
            relevantFramesForDistributing,
            skippedFramesFromPreviousAnimation,
            tickModifier,
            ticksSinceSequenceStarted);
    }

    public int NumberOfFrames { get; }
    public int TicksPerFrame { get; }
    public int TickCounterOfCurrentFrame { get; }
    public int CurrentFrame { get; }
    public bool IsPetrified { get; }
    public int RelevantFramesForDistributing { get; }
    public int SkippedFramesFromPreviousAnimation { get; }
    public int TickModifier { get; }
    public int TicksSinceSequenceStarted { get; }

    /// <summary>
    /// Starts an animation using the exact native distribution calculation.
    /// Requires positive frame metadata, known flags, a nonnegative distribution
    /// boundary and a preview fraction in [0, 128], matching supported callers.
    /// </summary>
    public NativeAnimationState SetNewAnimation(
        int numberOfFrames,
        int ticksPerFrame,
        NativeAnimationDistributionFlags flags = NativeAnimationDistributionFlags.None,
        int numSkippedFrames = 0,
        int distributeFramesBeforeFrame = 0,
        int previewShownGameTickFragments = 0)
    {
        RequireAnimationFrameCount(numberOfFrames);
        RequireSByte(nameof(ticksPerFrame), ticksPerFrame);
        RequireSByte(nameof(numSkippedFrames), numSkippedFrames);
        RequireSByte(nameof(distributeFramesBeforeFrame), distributeFramesBeforeFrame);
        RequirePreviewFraction(previewShownGameTickFragments);
        RequireFlags(flags);
        if (distributeFramesBeforeFrame < 0)
            throw new ArgumentOutOfRangeException(nameof(distributeFramesBeforeFrame), "Native callers use zero or a nonnegative frame boundary.");

        var skippedFramesFromPreviousAnimation = 0;
        if ((flags & NativeAnimationDistributionFlags.RepeatedAction) != 0
            && distributeFramesBeforeFrame != 0
            && NumberOfFrames == numberOfFrames
            && CurrentFrame + 1 >= distributeFramesBeforeFrame
            && CurrentFrame != NumberOfFrames - 1) {
            skippedFramesFromPreviousAnimation = NumberOfFrames - CurrentFrame - 1;
        }

        var nativeTicksPerFrame = ticksPerFrame <= 0 ? 1 : ticksPerFrame;
        var relevantFramesForDistributing = 0;
        var tickCounterOfCurrentFrame = 0;
        var currentFrame = numSkippedFrames;
        var ticksSinceSequenceStarted = 0;
        var tickModifier = 0;

        if (numSkippedFrames != 0 || flags != NativeAnimationDistributionFlags.None) {
            relevantFramesForDistributing = numberOfFrames;
            if (distributeFramesBeforeFrame != 0)
                relevantFramesForDistributing = distributeFramesBeforeFrame - 1;

            var relevantAnimationTicksForDistribution = relevantFramesForDistributing * nativeTicksPerFrame;
            var relevantAnimationTicksWithSkipping = relevantAnimationTicksForDistribution - (numSkippedFrames * nativeTicksPerFrame);

            if ((flags & NativeAnimationDistributionFlags.ProcessAnimationPending) != 0) {
                relevantAnimationTicksWithSkipping -= 1;
                ticksSinceSequenceStarted = -BaseValueFraction;
            }

            if ((flags & NativeAnimationDistributionFlags.SkipsDelayOfLastFrame) != 0)
                relevantAnimationTicksWithSkipping -= nativeTicksPerFrame - 1;

            relevantAnimationTicksForDistribution += skippedFramesFromPreviousAnimation * nativeTicksPerFrame;
            relevantAnimationTicksForDistribution *= BaseValueFraction;
            relevantAnimationTicksWithSkipping *= BaseValueFraction;

            ticksSinceSequenceStarted += previewShownGameTickFragments;
            relevantAnimationTicksWithSkipping += previewShownGameTickFragments;

            if (relevantAnimationTicksWithSkipping != 0) {
                var calculatedTickModifier = BaseValueFraction * relevantAnimationTicksForDistribution / relevantAnimationTicksWithSkipping;
                calculatedTickModifier /= nativeTicksPerFrame;
                tickModifier = unchecked((ushort)calculatedTickModifier);
            }
        }

        return new NativeAnimationState(
            numberOfFrames,
            nativeTicksPerFrame,
            tickCounterOfCurrentFrame,
            currentFrame,
            false,
            relevantFramesForDistributing,
            skippedFramesFromPreviousAnimation,
            tickModifier,
            ticksSinceSequenceStarted);
    }

    /// <summary>
    /// Changes frame metadata without starting a new animation. Native does
    /// not clamp the supplied delay here; callers must preserve that distinction
    /// from SetNewAnimation's nonpositive-delay normalization.
    /// </summary>
    public NativeAnimationState ChangeAnimationData(int numberOfFrames, int ticksPerFrame)
    {
        RequireSByte(nameof(numberOfFrames), numberOfFrames);
        RequireSByte(nameof(ticksPerFrame), ticksPerFrame);
        if (numberOfFrames == NumberOfFrames && ticksPerFrame == TicksPerFrame)
            return this;

        var currentFrame = numberOfFrames >= 1
            ? Math.Clamp(CurrentFrame, 0, numberOfFrames - 1)
            : -1;

        return new NativeAnimationState(
            numberOfFrames,
            ticksPerFrame,
            TickCounterOfCurrentFrame,
            currentFrame,
            IsPetrified,
            0,
            SkippedFramesFromPreviousAnimation,
            0,
            0);
    }

    /// <summary>Processes one native game tick, optionally in reverse.</summary>
    public NativeAnimationState ProcessAnimation(bool reverseAnimation = false)
    {
        var tickCounter = NarrowSByte(TickCounterOfCurrentFrame + 1);
        var ticksSinceSequenceStarted = NarrowInt16(TicksSinceSequenceStarted + BaseValueFraction);
        var currentFrame = CurrentFrame;

        if (tickCounter >= TicksPerFrame) {
            tickCounter = 0;
            if (reverseAnimation) {
                currentFrame = NarrowSByte(currentFrame - 1);
                if (currentFrame == -1) {
                    currentFrame = NarrowSByte(NumberOfFrames - 1);
                    ticksSinceSequenceStarted = 0;
                }
            } else {
                currentFrame = NarrowSByte(currentFrame + 1);
                if (currentFrame >= NumberOfFrames) {
                    currentFrame = 0;
                    ticksSinceSequenceStarted = 0;
                }
            }
        }

        return new NativeAnimationState(
            NumberOfFrames,
            TicksPerFrame,
            tickCounter,
            currentFrame,
            IsPetrified,
            RelevantFramesForDistributing,
            SkippedFramesFromPreviousAnimation,
            TickModifier,
            ticksSinceSequenceStarted);
    }

    /// <summary>
    /// Returns the native render frame for a Q7 progress value in [0, 128].
    /// </summary>
    public int ResolveRenderFrame(int progressQ7)
    {
        RequireProgress(progressQ7);

        if (RelevantFramesForDistributing <= 0)
            return Math.Max(0, CurrentFrame);

        if (CurrentFrame >= RelevantFramesForDistributing)
            return CurrentFrame;

        var ticksSinceSequenceStarted = Math.Max(0, TicksSinceSequenceStarted);
        var totalTicksForCurrentAnimationSequence = GetProgressToNextGameTick(progressQ7) + ticksSinceSequenceStarted;
        var absoluteAnimationFrame = NarrowSByte(MultiplyChecked(totalTicksForCurrentAnimationSequence, TickModifier, "render interpolation") / BaseValueFraction / BaseValueFraction);

        if (SkippedFramesFromPreviousAnimation > 0) {
            absoluteAnimationFrame = NarrowSByte(absoluteAnimationFrame - SkippedFramesFromPreviousAnimation);
            if (absoluteAnimationFrame < 0)
                absoluteAnimationFrame = NarrowSByte(NumberOfFrames + absoluteAnimationFrame);
        } else if (absoluteAnimationFrame >= RelevantFramesForDistributing) {
            return RelevantFramesForDistributing - 1;
        }

        return Math.Max(0, absoluteAnimationFrame);
    }

    /// <summary>
    /// Returns native AnimationInfo::getAnimationProgress as its raw uint8 result.
    /// The input interpolation fraction is restricted to the native [0, 128]
    /// range, but a negative signed intermediate is deliberately preserved
    /// through the native byte conversion rather than treated as a Q7 error.
    /// </summary>
    public int GetAnimationProgress(int progressQ7)
    {
        RequireProgress(progressQ7);

        var ticksSinceSequenceStarted = Math.Max(0, TicksSinceSequenceStarted);
        var tickModifier = TickModifier;
        if (RelevantFramesForDistributing <= 0) {
            if (TicksPerFrame <= 0)
                return 0;
            RequireNonzeroAnimationFrameCount(NumberOfFrames);
            ticksSinceSequenceStarted = NarrowInt16(((CurrentFrame * TicksPerFrame) + TickCounterOfCurrentFrame) * BaseValueFraction);
            tickModifier = BaseValueFraction / TicksPerFrame;
        } else {
            RequireNonzeroAnimationFrameCount(NumberOfFrames);
        }

        var totalTicksForCurrentAnimationSequence = GetProgressToNextGameTick(progressQ7) + ticksSinceSequenceStarted;
        var progressInAnimationFrames = MultiplyChecked(totalTicksForCurrentAnimationSequence, tickModifier, "animation progress");
        if (progressInAnimationFrames == int.MinValue && NumberOfFrames == -1)
            throw new InvalidOperationException("Native animation progress reached undefined INT_MIN / -1 division.");
        var animationFraction = progressInAnimationFrames / NumberOfFrames / BaseValueFraction;
        if (animationFraction > BaseValueFraction)
            throw new InvalidOperationException("Native animation progress escaped its Q7 range.");
        return unchecked((byte)animationFraction);
    }

    /// <summary>Returns a detached state with only the native petrified bit changed.</summary>
    public NativeAnimationState WithPetrified(bool isPetrified)
    {
        if (isPetrified == IsPetrified)
            return this;
        return new NativeAnimationState(
            NumberOfFrames,
            TicksPerFrame,
            TickCounterOfCurrentFrame,
            CurrentFrame,
            isPetrified,
            RelevantFramesForDistributing,
            SkippedFramesFromPreviousAnimation,
            TickModifier,
            TicksSinceSequenceStarted);
    }

    /// <summary>Copies every scalar, including private distribution state.</summary>
    public NativeAnimationState CloneState()
    {
        return new NativeAnimationState(
            NumberOfFrames,
            TicksPerFrame,
            TickCounterOfCurrentFrame,
            CurrentFrame,
            IsPetrified,
            RelevantFramesForDistributing,
            SkippedFramesFromPreviousAnimation,
            TickModifier,
            TicksSinceSequenceStarted);
    }

    private int GetProgressToNextGameTick(int progressQ7)
    {
        return IsPetrified ? 0 : progressQ7;
    }

    private static void RequireAnimationFrameCount(int value)
    {
        if (value is < 1 or > SByteMaximum)
            throw new ArgumentOutOfRangeException(nameof(value), "Native animation frame counts must be in the supported positive int8 range.");
    }

    private static void RequireNonzeroAnimationFrameCount(int value)
    {
        if (value == 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Native animation progress cannot divide by a zero frame count.");
    }

    private static void RequireProgress(int value)
    {
        if (value is < 0 or > BaseValueFraction)
            throw new ArgumentOutOfRangeException(nameof(value), "Native progress is a Q7 value in [0, 128].");
    }

    private static void RequirePreviewFraction(int value)
    {
        if (value is < 0 or > BaseValueFraction)
            throw new ArgumentOutOfRangeException(nameof(value), "Native preview progress is a Q7 value in [0, 128].");
    }

    private static void RequireFlags(NativeAnimationDistributionFlags flags)
    {
        const NativeAnimationDistributionFlags knownFlags = NativeAnimationDistributionFlags.ProcessAnimationPending
            | NativeAnimationDistributionFlags.SkipsDelayOfLastFrame
            | NativeAnimationDistributionFlags.RepeatedAction;
        if ((flags & ~knownFlags) != 0)
            throw new ArgumentOutOfRangeException(nameof(flags), "Unknown native animation distribution flags are unsupported.");
    }

    private static int RequireSByte(string name, int value)
    {
        if (value is < SByteMinimum or > SByteMaximum)
            throw new ArgumentOutOfRangeException(name, "The native field is signed int8.");
        return value;
    }

    private static int RequireUInt16(string name, int value)
    {
        if (value is < 0 or > UInt16Maximum)
            throw new ArgumentOutOfRangeException(name, "The native field is unsigned int16.");
        return value;
    }

    private static int RequireInt16(string name, int value)
    {
        if (value is < Int16Minimum or > Int16Maximum)
            throw new ArgumentOutOfRangeException(name, "The native field is signed int16.");
        return value;
    }

    private static int NarrowSByte(int value)
    {
        return unchecked((sbyte)value);
    }

    private static int NarrowInt16(int value)
    {
        return unchecked((short)value);
    }

    private static int MultiplyChecked(int left, int right, string operation)
    {
        var product = (long)left * right;
        if (product is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException($"Native {operation} exceeded signed int32 arithmetic.");
        return (int)product;
    }
}
