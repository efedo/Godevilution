using Devilution.Engine;

namespace Devilution.Diablo;

/** Backward-compatible Diablo facade over the engine's native-compatible LCG. */
public sealed class DiabloRandom : DeterministicLegacyRandom
{
    public DiabloRandom(uint seed)
        : base(seed)
    {
    }

    public new uint State => base.State;

    public new void Restore(uint restoredState) => base.Restore(restoredState);

    public new uint NextUInt32() => base.NextUInt32();

    public new int AdvanceRndSeed() => base.AdvanceRndSeed();

    public new int GenerateRnd(int limit) => base.GenerateRnd(limit);

    public new int RandomIntLessThan(int limit) => base.RandomIntLessThan(limit);

    public new int RandomIntBetween(int minimum, int maximum, bool halfOpen = false)
        => base.RandomIntBetween(minimum, maximum, halfOpen);

    public new bool FlipCoin(uint frequency) => base.FlipCoin(frequency);

    public new void Discard(int count) => base.Discard(count);
}
