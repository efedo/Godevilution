namespace Devilution.Engine;

/** Native-compatible Borland 32-bit LCG reusable by authoritative modules. */
public class DeterministicLegacyRandom
{
    private uint state;

    public DeterministicLegacyRandom(uint seed)
    {
        state = seed;
    }

    public uint State => state;

    public void Restore(uint restoredState)
    {
        state = restoredState;
    }

    public uint NextUInt32()
    {
        state = unchecked(0x015A4E35U * state + 1U);
        return state;
    }

    public int AdvanceRndSeed()
    {
        var seed = unchecked((int)NextUInt32());
        return seed == int.MinValue ? int.MinValue : Math.Abs(seed);
    }

    public int GenerateRnd(int limit)
    {
        if (limit <= 0)
            return 0;
        var seed = AdvanceRndSeed();
        return (limit <= 0x7FFF ? seed >> 16 : seed) % limit;
    }

    public int RandomIntLessThan(int limit) => Math.Max(GenerateRnd(limit), 0);

    public int RandomIntBetween(int minimum, int maximum, bool halfOpen = false)
    {
        if (maximum < minimum || (halfOpen && maximum == minimum))
            throw new ArgumentOutOfRangeException(nameof(maximum));
        var width = checked(maximum - minimum + (halfOpen ? 0 : 1));
        return checked(minimum + GenerateRnd(width));
    }

    public bool FlipCoin(uint frequency) => GenerateRnd(unchecked((int)frequency)) == 0;

    public void Discard(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        for (var index = 0; index < count; index++)
            NextUInt32();
    }
}
