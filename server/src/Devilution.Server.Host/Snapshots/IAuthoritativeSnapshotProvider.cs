using Devilution.Protocol.V1;

namespace Devilution.Server.Snapshots;

/** Produces the authoritative state visible to one connected session. */
public interface IAuthoritativeSnapshotProvider
{
    Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick);
}
