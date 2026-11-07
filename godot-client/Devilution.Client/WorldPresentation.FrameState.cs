using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf.Collections;

namespace Devilution.Client;

public partial class WorldPresentation
{
    private Snapshot? appliedPresentationSnapshot;
    private readonly bool recordDrawDiagnostics = !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE"))
        || System.Environment.GetEnvironmentVariable("GODEVILUTION_PRESENTATION_HISTORY") == "1";
    private ulong? appliedPresentationRevision;
    private bool snapshotDataChanged;
    private readonly HashSet<uint> activePresentationEntities = [];
    private PlayerSnapshot[] visiblePlayers = [];
    private MonsterSnapshot[] visibleMonsters = [];
    private WorldItemSnapshot[] visibleItems = [];
    private ObjectSnapshot[] visibleObjects = [];
    private CorpseSnapshot[] visibleCorpses = [];
    private PortalSnapshot[] visiblePortals = [];
    private ProjectileSnapshot[] visibleProjectiles = [];
    private readonly PresentationChangeGate<(string?, uint)> objectBindingChanges = new();
    private readonly PresentationChangeGate<(string?, string, RepeatedField<NativeTownResidentSnapshot>?)> townerBindingChanges = new();

    private void RefreshPresentationMembers(PlayerSnapshot? player, LevelGeometrySnapshot? geometry, int radius)
    {
        if (!snapshotDataChanged || snapshot is null) return;
        var level = player?.LevelId ?? 0;
        visiblePlayers = snapshot.Players.Where(value => value.EntityId == player?.EntityId || player is not null
            && AuthoritativeNativeVisionVisibility.IsVisible(value.LevelId, value.PositionX, value.PositionY,
                level, geometry, player.PositionX, player.PositionY, radius)).ToArray();
        visibleMonsters = snapshot.Monsters.Where(value => player is not null && AuthoritativeMonsterPresentationVisibility.IsVisible(
            value, level, geometry, player.PositionX, player.PositionY, radius)).ToArray();
        visibleItems = snapshot.WorldItems.Where(value => player is not null && AuthoritativeWorldItemPresentationVisibility.IsVisible(
            value, level, geometry, player.PositionX, player.PositionY, radius)).ToArray();
        visibleObjects = snapshot.Objects.Where(value => player is not null && AuthoritativeObjectPresentationVisibility.IsVisible(
            value, level, geometry, player.PositionX, player.PositionY, radius)).ToArray();
        visibleCorpses = snapshot.Corpses.Where(value => player is not null && AuthoritativeCorpsePresentationVisibility.IsVisible(
            value, level, geometry, player.PositionX, player.PositionY, radius)).ToArray();
        visiblePortals = snapshot.Portals.Where(value => player is not null && AuthoritativePortalPresentation.Resolve(
            value, level, geometry, player.PositionX, player.PositionY, radius) is not null).ToArray();
        visibleProjectiles = snapshot.Projectiles.Where(value => AuthoritativeEntityPositionProjection.IsVisibleOnActiveLevel(value.LevelId, level)).ToArray();
    }
}
