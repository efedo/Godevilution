using Devilution.Protocol.V1;
using Google.Protobuf.Collections;

namespace Devilution.Protocol;

/** Connection-local geometry and idle-body references, independent of simulation state and save formats. */
public sealed class AuthoritativeSnapshotGeometryCache
{
    private RepeatedField<LevelGeometrySnapshot>? geometry;
    private string? content, ruleset;
    private ulong revision, stateRevision;
    private Snapshot? body;

    public AuthoritativeSnapshotGeometryCache() : this(false) { }

    public AuthoritativeSnapshotGeometryCache(bool levelScopedIdleReferences)
        => LevelScopedIdleReferences = levelScopedIdleReferences;

    public bool LevelScopedIdleReferences { get; }

    public Snapshot Project(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var unchanged = geometry is not null && content == snapshot.ContentManifestHash
            && ruleset == snapshot.RulesetIdentityHash && geometry.Equals(snapshot.LevelGeometry);
        if (!unchanged) {
            // Providers may reuse their graphs: retain an owned baseline before later comparisons.
            geometry = snapshot.LevelGeometry.Clone();
            content = snapshot.ContentManifestHash;
            ruleset = snapshot.RulesetIdentityHash;
            revision = checked(revision + 1);
        }
        var candidate = snapshot.WithGeometryForTransport(new());
        candidate.Tick = 0; candidate.StateSha256 = string.Empty;
        candidate.GeometryRevision = 0; candidate.GeometryUnchanged = false;
        candidate.StateRevision = 0; candidate.StateUnchanged = false;
        var sameBody = unchanged && body is not null && body.Equals(candidate);
        if (!sameBody) {
            body = candidate.CloneWithoutGeometry();
            stateRevision = checked(stateRevision + 1);
        }
        if (sameBody && IsIdleBody(snapshot, LevelScopedIdleReferences))
            return new Snapshot { Tick = snapshot.Tick, StateSha256 = snapshot.StateSha256,
                ContentManifestHash = snapshot.ContentManifestHash, RulesetIdentityHash = snapshot.RulesetIdentityHash,
                GeometryRevision = revision, GeometryUnchanged = true, StateRevision = stateRevision, StateUnchanged = true };
        var result = snapshot.WithGeometryForTransport(unchanged ? new() : snapshot.LevelGeometry);
        result.GeometryRevision = revision;
        result.GeometryUnchanged = unchanged;
        result.StateRevision = stateRevision;
        result.StateUnchanged = false;
        return result;
    }

    /** A clock-only projection cannot carry an action whose validity depends on the new tick. */
    public static bool IsIdleBody(Snapshot snapshot)
        => IsIdleBody(snapshot, levelScopedMonsterActivity: false);

    /** The receiving player is first; older negotiated peers retain the global monster check. */
    public static bool IsIdleBody(Snapshot snapshot, bool levelScopedMonsterActivity)
    {
        if (levelScopedMonsterActivity && snapshot.Players.Count == 0) return false;
        foreach (var player in snapshot.Players)
            if (player.CombatMode != PlayerCombatMode.Stand || player.CurrentAction is not null
                || player.NativeWalkPathPending || player.StatusEffects.Count != 0) return false;
        foreach (var monster in snapshot.Monsters)
            if (monster.Moving && (!levelScopedMonsterActivity || monster.LevelId == snapshot.Players[0].LevelId)) return false;
        return snapshot.Projectiles.Count == 0;
    }
}
