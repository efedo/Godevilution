using Devilution.Domain;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class NativeAnimationStateTests
{
    [Fact]
    public void ProcessPendingAttackUsesNativeDistributionFields()
    {
        var state = NativeAnimationState.Empty.SetNewAnimation(
            numberOfFrames: 16,
            ticksPerFrame: 1,
            flags: NativeAnimationDistributionFlags.ProcessAnimationPending,
            distributeFramesBeforeFrame: 9);

        Assert.Equal(16, state.NumberOfFrames);
        Assert.Equal(1, state.TicksPerFrame);
        Assert.Equal(0, state.CurrentFrame);
        Assert.Equal(8, state.RelevantFramesForDistributing);
        Assert.Equal(0, state.SkippedFramesFromPreviousAnimation);
        Assert.Equal(146, state.TickModifier);
        Assert.Equal(-128, state.TicksSinceSequenceStarted);

        state = state.ProcessAnimation();
        Assert.Equal(1, state.CurrentFrame);
        Assert.Equal(0, state.TickCounterOfCurrentFrame);
        Assert.Equal(0, state.ResolveRenderFrame(0));
        Assert.Equal(0, state.ResolveRenderFrame(38));
        Assert.Equal(0, state.ResolveRenderFrame(76));
        Assert.Equal(0, state.ResolveRenderFrame(102));
    }

    [Fact]
    public void FastestAttackRetainsSignedSkipAndLiteralModifier()
    {
        var state = NativeAnimationState.Empty.SetNewAnimation(
            16,
            1,
            NativeAnimationDistributionFlags.ProcessAnimationPending,
            numSkippedFrames: 2,
            distributeFramesBeforeFrame: 9);

        Assert.Equal(204, state.TickModifier);
        state = state.ProcessAnimation();
        Assert.Equal(3, state.CurrentFrame);
        Assert.Equal(0, state.ResolveRenderFrame(0));
        Assert.Equal(0, state.ResolveRenderFrame(38));
        Assert.Equal(0, state.ResolveRenderFrame(76));
        Assert.Equal(1, state.ResolveRenderFrame(102));
    }

    [Fact]
    public void SignedWalkSkipsAreRenderedWithoutUnsignedNormalization()
    {
        var minusTwo = NativeAnimationState.Empty.SetNewAnimation(8, 1, numSkippedFrames: -2);
        var minusOne = NativeAnimationState.Empty.SetNewAnimation(8, 1, numSkippedFrames: -1);

        Assert.Equal(-2, minusTwo.CurrentFrame);
        Assert.Equal(-1, minusOne.CurrentFrame);
        Assert.Equal(102, minusTwo.TickModifier);
        Assert.Equal(113, minusOne.TickModifier);
        Assert.Equal(0, minusTwo.ResolveRenderFrame(0));
        Assert.Equal(0, minusOne.ResolveRenderFrame(0));
        Assert.Equal(-1, minusTwo.ProcessAnimation().CurrentFrame);
        Assert.Equal(0, minusOne.ProcessAnimation().CurrentFrame);
    }

    [Fact]
    public void LastFrameDelayAndFastBlockUseNativeLiteralProgression()
    {
        var state = NativeAnimationState.Empty.SetNewAnimation(
            2,
            3,
            NativeAnimationDistributionFlags.SkipsDelayOfLastFrame);

        Assert.Equal(2, state.RelevantFramesForDistributing);
        Assert.Equal(64, state.TickModifier);
        Assert.Equal(0, state.ProcessAnimation().CurrentFrame);
        state = state.ProcessAnimation().ProcessAnimation().ProcessAnimation();
        Assert.Equal(1, state.CurrentFrame);
        Assert.Equal(1, state.ProcessAnimation().CurrentFrame);
    }

    [Fact]
    public void RepeatedAnimationCarriesTheTruncatedPreviousFrames()
    {
        var first = NativeAnimationState.Empty.SetNewAnimation(
            16,
            1,
            NativeAnimationDistributionFlags.ProcessAnimationPending,
            distributeFramesBeforeFrame: 9);
        for (var i = 0; i < 9; i++)
            first = first.ProcessAnimation();

        var repeated = first.SetNewAnimation(
            16,
            1,
            NativeAnimationDistributionFlags.ProcessAnimationPending | NativeAnimationDistributionFlags.RepeatedAction,
            distributeFramesBeforeFrame: 9);

        Assert.Equal(6, repeated.SkippedFramesFromPreviousAnimation);
        Assert.Equal(8, repeated.RelevantFramesForDistributing);
        Assert.Equal(256, repeated.TickModifier);
        Assert.Equal(-128, repeated.TicksSinceSequenceStarted);
        Assert.Equal(10, repeated.ProcessAnimation().ResolveRenderFrame(0));
    }

    [Fact]
    public void CompletedDistributedAnimationReturnsItsNativeCurrentFrame()
    {
        var state = NativeAnimationState.Empty.SetNewAnimation(
            16,
            1,
            NativeAnimationDistributionFlags.ProcessAnimationPending,
            distributeFramesBeforeFrame: 9);
        for (var i = 0; i < 8; i++)
            state = state.ProcessAnimation();

        Assert.Equal(8, state.CurrentFrame);
        Assert.Equal(8, state.RelevantFramesForDistributing);
        Assert.Equal(8, state.ResolveRenderFrame(0));
        Assert.Equal(8, state.ResolveRenderFrame(128));
    }

    [Fact]
    public void ChangeAnimationDataClampsFrameAndClearsDistributionButPreservesPetrification()
    {
        var state = NativeAnimationState.Empty
            .SetNewAnimation(8, 1, NativeAnimationDistributionFlags.ProcessAnimationPending, 2, 7)
            .WithPetrified(true);

        var changed = state.ChangeAnimationData(4, 2);

        Assert.Equal(2, changed.CurrentFrame);
        Assert.Equal(4, changed.NumberOfFrames);
        Assert.Equal(2, changed.TicksPerFrame);
        Assert.True(changed.IsPetrified);
        Assert.Equal(0, changed.RelevantFramesForDistributing);
        Assert.Equal(0, changed.TickModifier);
        Assert.Equal(0, changed.TicksSinceSequenceStarted);
    }

    [Fact]
    public void ChangeAnimationDataPreservesNativeNonpositiveDelayInsteadOfClamping()
    {
        var changed = NativeAnimationState.Empty.SetNewAnimation(3, 2).ChangeAnimationData(3, 0);
        var negative = NativeAnimationState.Empty.SetNewAnimation(3, 2).ChangeAnimationData(3, -1);

        Assert.Equal(0, changed.TicksPerFrame);
        Assert.Equal(0, changed.GetAnimationProgress(128));
        Assert.Equal(-1, negative.TicksPerFrame);
        Assert.Equal(0, negative.GetAnimationProgress(128));
        Assert.Equal(0, changed.ResolveRenderFrame(0));
        Assert.Equal(0, negative.ResolveRenderFrame(128));
    }

    [Fact]
    public void DistributedProgressRetainsNativeStateWhenDelayIsNonpositive()
    {
        var state = NativeAnimationState.Restore(
            numberOfFrames: 8,
            ticksPerFrame: 0,
            tickCounterOfCurrentFrame: 0,
            currentFrame: 0,
            isPetrified: false,
            relevantFramesForDistributing: 8,
            skippedFramesFromPreviousAnimation: 0,
            tickModifier: 128,
            ticksSinceSequenceStarted: 128);

        Assert.Equal(16, state.GetAnimationProgress(0));

        var negativeRate = NativeAnimationState.Restore(
            numberOfFrames: 8,
            ticksPerFrame: -1,
            tickCounterOfCurrentFrame: 0,
            currentFrame: 0,
            isPetrified: false,
            relevantFramesForDistributing: 8,
            skippedFramesFromPreviousAnimation: 0,
            tickModifier: 128,
            ticksSinceSequenceStarted: 128);

        Assert.Equal(16, negativeRate.GetAnimationProgress(0));
    }

    [Fact]
    public void NegativeFrameCountUsesNativeNonzeroDivisorAndUnsignedProgressWrap()
    {
        var state = NativeAnimationState.Empty
            .SetNewAnimation(3, 2)
            .ChangeAnimationData(-1, 1);

        Assert.Equal(-1, state.NumberOfFrames);
        Assert.Equal(-1, state.CurrentFrame);
        Assert.Equal(128, state.GetAnimationProgress(0));
        Assert.Equal(0, state.GetAnimationProgress(128));

        var reverse = NativeAnimationState.Empty.SetNewAnimation(3, 1)
            .ChangeAnimationData(-128, 1).ProcessAnimation().ProcessAnimation(reverseAnimation: true);
        Assert.Equal(127, reverse.CurrentFrame);
        Assert.Equal(129, reverse.GetAnimationProgress(0));
        Assert.Equal(128, reverse.GetAnimationProgress(128));
    }

    [Fact]
    public void EmptyNativeCursorUsesTheDistributionAbsentRenderFallback()
    {
        Assert.Equal(0, NativeAnimationState.Empty.ResolveRenderFrame(0));
        Assert.Equal(0, NativeAnimationState.Empty.ResolveRenderFrame(128));
        Assert.Equal(0, NativeAnimationState.Empty.GetAnimationProgress(128));
    }

    [Fact]
    public void ChangedAnimationDataPreservesThePrivatePreviousAnimationCarry()
    {
        var state = NativeAnimationState.Restore(16, 1, 0, 4, false, 8, 6, 137, 128);
        var changed = state.ChangeAnimationData(5, 3);

        Assert.Equal(4, changed.CurrentFrame);
        Assert.Equal(6, changed.SkippedFramesFromPreviousAnimation);
        Assert.Equal(0, changed.RelevantFramesForDistributing);
        Assert.Equal(0, changed.TickModifier);
        Assert.Equal(0, changed.TicksSinceSequenceStarted);
    }

    [Fact]
    public void SetNewAnimationClampsNonpositiveDelayAndPreservesPreviewFraction()
    {
        var state = NativeAnimationState.Empty.SetNewAnimation(
            16,
            0,
            NativeAnimationDistributionFlags.ProcessAnimationPending,
            numSkippedFrames: 2,
            distributeFramesBeforeFrame: 9,
            previewShownGameTickFragments: 128);

        Assert.Equal(1, state.TicksPerFrame);
        Assert.Equal(0, state.TicksSinceSequenceStarted);
        Assert.Equal(170, state.TickModifier);
    }

    [Fact]
    public void ForwardAndReverseProcessingWrapAtNativeFrameBoundaries()
    {
        var forward = NativeAnimationState.Empty.SetNewAnimation(3, 2).ProcessAnimation().ProcessAnimation();
        Assert.Equal(1, forward.CurrentFrame);
        Assert.Equal(0, forward.TickCounterOfCurrentFrame);

        var reverse = forward.ProcessAnimation(true).ProcessAnimation(true);
        Assert.Equal(0, reverse.CurrentFrame);
        Assert.Equal(0, reverse.TickCounterOfCurrentFrame);
    }

    [Fact]
    public void ReverseFrameWrapNarrowsNativeNegativeFrameCountResult()
    {
        var state = NativeAnimationState.Restore(
            numberOfFrames: -128,
            ticksPerFrame: 1,
            tickCounterOfCurrentFrame: 0,
            currentFrame: 0,
            isPetrified: false,
            relevantFramesForDistributing: 0,
            skippedFramesFromPreviousAnimation: 0,
            tickModifier: 0,
            ticksSinceSequenceStarted: 0);

        Assert.Equal(127, state.ProcessAnimation(reverseAnimation: true).CurrentFrame);
    }

    [Fact]
    public void PetrificationSuppressesOnlyRenderFractionAndDoesNotFreezeGameTickState()
    {
        var moving = NativeAnimationState.Empty.SetNewAnimation(
            8,
            1,
            NativeAnimationDistributionFlags.ProcessAnimationPending,
            distributeFramesBeforeFrame: 7).ProcessAnimation();
        var petrified = moving.WithPetrified(true);

        Assert.NotEqual(moving.ResolveRenderFrame(128), petrified.ResolveRenderFrame(128));
        Assert.Equal(moving.GetAnimationProgress(0), petrified.GetAnimationProgress(96));
        var movingNext = moving.ProcessAnimation();
        var petrifiedNext = petrified.ProcessAnimation();
        Assert.Equal(movingNext.CurrentFrame, petrifiedNext.CurrentFrame);
        Assert.Equal(movingNext.TickCounterOfCurrentFrame, petrifiedNext.TickCounterOfCurrentFrame);
        Assert.Equal(movingNext.TicksSinceSequenceStarted, petrifiedNext.TicksSinceSequenceStarted);
    }

    [Fact]
    public void ClonePreservesEveryDistributionScalarAndContinuation()
    {
        var source = NativeAnimationState.Restore(16, 1, 0, 0, false, 8, 6, 137, -128);
        var clone = source.CloneState();

        Assert.Equal(source, clone);
        Assert.Equal(source.ProcessAnimation(), clone.ProcessAnimation());
        Assert.Equal(source.ResolveRenderFrame(64), clone.ResolveRenderFrame(64));
        Assert.Equal(source.GetAnimationProgress(64), clone.GetAnimationProgress(64));
    }

    [Fact]
    public void ActorsKeepIndependentAnimationCursorsAtTheFractionBoundary()
    {
        var stand = NativeAnimationState.Empty.SetNewAnimation(10, 4);
        var attack = NativeAnimationState.Empty.SetNewAnimation(
            16,
            1,
            NativeAnimationDistributionFlags.ProcessAnimationPending,
            numSkippedFrames: 2,
            distributeFramesBeforeFrame: 9);

        stand = stand.ProcessAnimation();
        attack = attack.ProcessAnimation();

        Assert.Equal(0, stand.CurrentFrame);
        Assert.Equal(1, stand.TickCounterOfCurrentFrame);
        Assert.Equal(3, attack.CurrentFrame);
        Assert.Equal(0, attack.TickCounterOfCurrentFrame);
        Assert.Equal(0, stand.ResolveRenderFrame(128));
        Assert.Equal(1, attack.ResolveRenderFrame(128));
        Assert.Equal(12, attack.GetAnimationProgress(128));
    }

    [Fact]
    public void UnchangedAnimationDataRetainsTheEntireNativeCursor()
    {
        var state = NativeAnimationState.Empty
            .SetNewAnimation(16, 1, NativeAnimationDistributionFlags.ProcessAnimationPending, 2, 9)
            .ProcessAnimation();

        var unchanged = state.ChangeAnimationData(state.NumberOfFrames, state.TicksPerFrame);

        Assert.Same(state, unchanged);
        Assert.Equal(state, unchanged);
    }

    [Fact]
    public void NativeWidthsFlagsAndProgressAreFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeAnimationState(128, 1, 0, 0, false, 0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeAnimationState.Empty.SetNewAnimation(8, 1, (NativeAnimationDistributionFlags)8));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeAnimationState.Empty.SetNewAnimation(8, 1, previewShownGameTickFragments: 129));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeAnimationState.Empty.SetNewAnimation(8, 1, distributeFramesBeforeFrame: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeAnimationState.Empty.ResolveRenderFrame(129));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeAnimationState.Restore(0, 1, 0, 0, false, 0, 0, 0, 0).GetAnimationProgress(0));
        var overflowingInterpolation = NativeAnimationState.Restore(8, 1, 0, 0, false, 8, 0, 65535, 32767);
        Assert.Throws<InvalidOperationException>(() => overflowingInterpolation.ResolveRenderFrame(128));
        Assert.Throws<InvalidOperationException>(() => overflowingInterpolation.GetAnimationProgress(128));
        Assert.Throws<InvalidOperationException>(() => overflowingInterpolation.GetAnimationProgress(0));
    }
}
