using Devilution.Protocol.V1;
using Google.Protobuf.Collections;

namespace Devilution.Client.Protocol;

public sealed partial class AuthoritativeClientModel
{
    private RepeatedField<LevelGeometrySnapshot>? cachedSnapshotGeometry;
    private ulong cachedGeometryRevision, cachedStateRevision;
    private Snapshot? cachedSnapshotBody;
    private string? cachedGeometryContent, cachedGeometryRuleset;
    private bool cachedLevelScopedIdleReferences;

    private Snapshot ExpandSnapshotGeometry(Snapshot wire)
    {
        if (acceptNextSnapshotAsNewEpoch || wire.GeometryRevision == 0
            || wire.GeometryRevision != cachedGeometryRevision || cachedSnapshotGeometry is null
            || wire.LevelGeometry.Count != 0 || wire.ContentManifestHash != cachedGeometryContent
            || wire.RulesetIdentityHash != cachedGeometryRuleset)
            throw new InvalidDataException("The authoritative snapshot has an unknown or conflicting geometry reference.");
        // Validation reads this view; transport-owned dynamic fields are cloned only after all checks pass.
        return wire.WithGeometryForTransport(cachedSnapshotGeometry);
    }

    private Snapshot? OwnSnapshotProjection(Snapshot wire, bool discarded, bool levelScopedIdleReferences, bool levelScopedSnapshots)
    {
        // Stale publications are validated, but cannot change an admitted reference baseline.
        if (discarded) return null;
        cachedLevelScopedIdleReferences = levelScopedIdleReferences;
        cachedLevelScopedSnapshots = levelScopedSnapshots;
        Snapshot? owned = null;
        if (wire.GeometryUnchanged) {
            if (!discarded) {
                owned = wire.CloneWithoutGeometry();
                owned.LevelGeometry.AddRange(cachedSnapshotGeometry!);
            }
        } else {
            if (!discarded) owned = wire.Clone();
            cachedGeometryRevision = wire.GeometryRevision;
            cachedSnapshotGeometry = wire.GeometryRevision == 0 ? null : owned?.LevelGeometry ?? wire.LevelGeometry.Clone();
            cachedGeometryContent = wire.ContentManifestHash;
            cachedGeometryRuleset = wire.RulesetIdentityHash;
        }
        cachedStateRevision = wire.StateRevision;
        cachedSnapshotBody = wire.StateRevision == 0 ? null : owned ?? MakeCompleteBody(wire);
        if (owned is not null) {
            // The model always exposes a complete projection. Wire cache metadata is not gameplay state.
            owned.GeometryRevision = 0;
            owned.GeometryUnchanged = false;
            owned.StateRevision = 0;
            owned.StateUnchanged = false;
        }
        return owned;
    }

    private Snapshot MakeCompleteBody(Snapshot wire)
    {
        var owned = wire.CloneWithoutGeometry();
        owned.LevelGeometry.AddRange(cachedSnapshotGeometry ?? wire.LevelGeometry);
        return owned;
    }

    private Snapshot ExpandIdleSnapshotReference(Snapshot wire)
    {
        if (acceptNextSnapshotAsNewEpoch || !wire.GeometryUnchanged || wire.StateRevision == 0 || wire.StateRevision != cachedStateRevision
            || cachedSnapshotBody is null || !Devilution.Protocol.AuthoritativeSnapshotGeometryCache.IsIdleBody(
                cachedSnapshotBody, cachedLevelScopedIdleReferences))
            throw new InvalidDataException("The authoritative snapshot has an unknown or active body reference.");
        // This also fences the geometry token and content/ruleset identity.
        ExpandSnapshotGeometry(wire);
        if (wire.PlayerRoster.Count != 0 || wire.Players.Count != 0 || wire.Monsters.Count != 0 || wire.WorldItems.Count != 0 || wire.Objects.Count != 0
            || wire.Quests.Count != 0 || wire.Projectiles.Count != 0 || wire.Portals.Count != 0 || wire.Corpses.Count != 0
            || wire.ActiveStore is not null || wire.RandomState != 0 || wire.VictoryTriggered || wire.Difficulty != GameDifficulty.Unspecified
            || wire.QuestTownerInteractions.Count != 0 || wire.ActiveWorldMutationIds.Count != 0 || wire.SetLevelObjectOrigins.Count != 0
            || wire.MaterializedProceduralLevelIds.Count != 0 || wire.TownerGreetings.Count != 0
            || wire.ProceduralQuestPieceOrigins.Count != 0 || wire.NativeTownResidents.Count != 0 || wire.NativeTownCatalogFingerprint.Length != 0)
            throw new InvalidDataException("The authoritative idle snapshot reference contains conflicting body fields.");
        var expanded = cachedSnapshotBody.WithGeometryForTransport(cachedSnapshotGeometry!);
        expanded.Tick = wire.Tick;
        expanded.StateSha256 = wire.StateSha256;
        return expanded;
    }

    private void ApplyIdleSnapshotReference(Snapshot wire)
    {
        var expanded = ExpandIdleSnapshotReference(wire);
        ValidateSessionMetadata(expanded);
        if (Snapshot is { } current && expanded.Tick < current.Tick) return;
        Snapshot = expanded;
        AcceptedSnapshotRevision = checked(AcceptedSnapshotRevision + 1);
        LastError = null;
        RebuildPredictedPosition();
    }

    /** Only acceptance observers request an independent complete copy of a referenced wire snapshot. */
    public Snapshot CompleteSnapshotObservation(Snapshot wire)
    {
        var complete = wire.StateUnchanged ? ExpandIdleSnapshotReference(wire).Clone()
            : wire.GeometryUnchanged ? ExpandSnapshotGeometry(wire).Clone() : wire.Clone();
        complete.GeometryRevision = 0;
        complete.GeometryUnchanged = false;
        complete.StateRevision = 0;
        complete.StateUnchanged = false;
        return complete;
    }
}
