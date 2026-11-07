using System.Runtime.CompilerServices;
using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Server.Host;

public sealed partial class AuthoritativeTcpServer
{
    // The existing connection writer owns each cache. Weak keys release large baselines after disconnect.
    private readonly ConditionalWeakTable<Stream, AuthoritativeSnapshotGeometryCache> snapshotGeometryCaches = new();

    private Envelope ProjectSnapshotGeometry(Stream stream, Envelope envelope)
    {
        if (!snapshotGeometryCaches.TryGetValue(stream, out var cache)) return envelope;
        if (envelope.ServerHello is { } hello) hello.SupportsSnapshotReferences = true;
        if (envelope.Snapshot is { } snapshot) {
            // The writer owns the envelope; preserve its unknown fields and the actual wire-size counters.
            envelope.Snapshot = cache.Project(snapshot);
            if (envelope.CalculateSize() > AuthoritativeProtocolLimits.MaximumEnvelopeBytes) {
                // Revision metadata must not reject a formerly valid maximum-size envelope.
                envelope.Snapshot = snapshot;
                snapshotGeometryCaches.Remove(stream);
            }
        }
        return envelope;
    }
}
