using Google.Protobuf.Collections;

namespace Devilution.Protocol.V1;

public sealed partial class Snapshot
{
    /** A transport view shares read-only body fields; only its geometry and transport metadata may change. */
    public Snapshot WithGeometryForTransport(RepeatedField<LevelGeometrySnapshot> geometry)
        => new(this, geometry, cloneBody: false);

    /** Clones the mutable dynamic body without cloning a separately owned geometry cache. */
    public Snapshot CloneWithoutGeometry() => new(this, new(), cloneBody: true);

    private Snapshot(Snapshot other, RepeatedField<LevelGeometrySnapshot> geometry, bool cloneBody) : this()
    {
        tick_ = other.tick_;
        stateSha256_ = other.stateSha256_;
        players_ = cloneBody ? (other.players_.Clone()) : other.players_;
        activeStore_ = cloneBody ? (other.activeStore_ != null ? other.activeStore_.Clone() : null) : other.activeStore_;
        monsters_ = cloneBody ? (other.monsters_.Clone()) : other.monsters_;
        worldItems_ = cloneBody ? (other.worldItems_.Clone()) : other.worldItems_;
        objects_ = cloneBody ? (other.objects_.Clone()) : other.objects_;
        quests_ = cloneBody ? (other.quests_.Clone()) : other.quests_;
        projectiles_ = cloneBody ? (other.projectiles_.Clone()) : other.projectiles_;
        randomState_ = other.randomState_;
        portals_ = cloneBody ? (other.portals_.Clone()) : other.portals_;
        corpses_ = cloneBody ? (other.corpses_.Clone()) : other.corpses_;
        levelGeometry_ = geometry;
        victoryTriggered_ = other.victoryTriggered_;
        difficulty_ = other.difficulty_;
        questTownerInteractions_ = cloneBody ? (other.questTownerInteractions_.Clone()) : other.questTownerInteractions_;
        activeWorldMutationIds_ = cloneBody ? (other.activeWorldMutationIds_.Clone()) : other.activeWorldMutationIds_;
        setLevelObjectOrigins_ = cloneBody ? (other.setLevelObjectOrigins_.Clone()) : other.setLevelObjectOrigins_;
        materializedProceduralLevelIds_ = cloneBody ? (other.materializedProceduralLevelIds_.Clone()) : other.materializedProceduralLevelIds_;
        contentManifestHash_ = other.contentManifestHash_;
        rulesetIdentityHash_ = other.rulesetIdentityHash_;
        townerGreetings_ = cloneBody ? (other.townerGreetings_.Clone()) : other.townerGreetings_;
        proceduralQuestPieceOrigins_ = cloneBody ? (other.proceduralQuestPieceOrigins_.Clone()) : other.proceduralQuestPieceOrigins_;
        nativeTownResidents_ = cloneBody ? (other.nativeTownResidents_.Clone()) : other.nativeTownResidents_;
        nativeTownCatalogFingerprint_ = other.nativeTownCatalogFingerprint_;
        _unknownFields = Google.Protobuf.UnknownFieldSet.Clone(other._unknownFields);
        geometryRevision_ = other.geometryRevision_;
        geometryUnchanged_ = other.geometryUnchanged_;
        stateRevision_ = other.stateRevision_;
        stateUnchanged_ = other.stateUnchanged_;
        playerRoster_ = cloneBody ? other.playerRoster_.Clone() : other.playerRoster_;
    }
}
