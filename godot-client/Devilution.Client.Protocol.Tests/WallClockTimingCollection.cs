using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Real RTT, retry and expiry tests run without competing collections until the fake-clock migration. */
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class WallClockTimingCollection
{
    public const string CollectionName = "Wall-clock client timing";
}
