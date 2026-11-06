using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Only physical TCP silence windows and the live host-rate check require an isolated wall clock. */
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class WallClockTimingCollection
{
    public const string CollectionName = "Wall-clock client timing";
}
