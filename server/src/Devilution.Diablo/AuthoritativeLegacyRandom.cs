namespace Devilution.Diablo;

/** Compatibility implementation of the native Diablo LCG used by item generation. */
public sealed class AuthoritativeLegacyRandom
{
    private readonly DiabloRandom random;

    public AuthoritativeLegacyRandom(uint seed)
    {
        random = new DiabloRandom(seed);
    }

    public uint State => random.State;

    /** Advances the native LCG and returns the seed value used by InitMonster. */
    public uint AdvanceSeed()
        => unchecked((uint)random.AdvanceRndSeed());

    public int Next(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
            return 0;

        return random.RandomIntLessThan(exclusiveMaximum);
    }

    public void Discard(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        random.Discard(count);
    }
}
