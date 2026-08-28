using Devilution.Engine;

namespace Devilution.Diablo;

/** Compatibility implementation of the native Diablo LCG used by item generation. */
public sealed class AuthoritativeLegacyRandom : DeterministicLegacyRandom
{
    public AuthoritativeLegacyRandom(uint seed)
        : base(seed)
    {
    }
}
