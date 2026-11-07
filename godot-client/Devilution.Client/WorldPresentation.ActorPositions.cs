using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client;

public partial class WorldPresentation
{
    private readonly List<NativeSceneActorCommand> retainedMovingActors = [];

    private ClientPresentationPosition ResolveNativePlayerPosition(PlayerSnapshot player)
        => player.EntityId == snapshot!.Players[0].EntityId && localVisualPosition is { } visual ? visual
            : ResolveRenderedActorPosition(player.EntityId,
                AuthoritativePlayerPositionProjection.ResolvePresentation(player, nativePlayerPresentationTick ?? snapshot.Tick));

    private ClientPresentationPosition ResolveNativeMonsterPosition(MonsterSnapshot monster)
        => ResolveRenderedActorPosition(monster.EntityId,
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, nativePlayerPresentationTick ?? snapshot!.Tick));

    private ClientPresentationPosition ResolveRenderedActorPosition(uint entityId, ClientPresentationPosition fallback)
        => nativeSceneViewport is { } view && displayedPositions.TryGetValue(entityId, out var center)
            ? NativeSceneActorPlacement.FromScreen(view, center.X, center.Y) : fallback;

    private void AddNativeSceneActorCommand(
        Dictionary<(int X, int Y), List<NativeSceneActorCommand>> commands,
        int x, int y, int priority, uint entityId, float anchorX, float anchorY, Action draw,
        Func<bool>? visible = null, Func<ClientPresentationPosition>? position = null)
    {
        if (nativeSceneViewport is not { } view || x < 0 || y < 0 || x >= view.LevelWidth || y >= view.LevelHeight)
            return;
        var point = position?.Invoke() ?? new ClientPresentationPosition(x, y);
        var cell = NativeSceneActorPlacement.OcclusionCell(point, view.LevelWidth, view.LevelHeight);
        if (!commands.TryGetValue(cell, out var actors)) commands.Add(cell, actors = []);
        actors.Add(new(priority, entityId, draw, visible) { Position = point, Cell = cell, PositionProvider = position });
    }

    private void BindRetainedActorPositions()
    {
        retainedMovingActors.Clear();
        foreach (var actors in retainedActors.Values)
            foreach (var actor in actors)
                if (actor.PositionProvider is not null) retainedMovingActors.Add(actor);
    }

    /** Only actor membership/order changes between snapshots; terrain geometry and raster plans stay retained. */
    private bool RefreshRetainedActorPositions(NativeSceneViewport view)
    {
        var changedCells = false;
        foreach (var actor in retainedMovingActors) {
            actor.Position = actor.PositionProvider!();
            var cell = NativeSceneActorPlacement.OcclusionCell(actor.Position, view.LevelWidth, view.LevelHeight);
            if (cell != actor.Cell) {
                var previous = retainedActors[actor.Cell];
                previous.Remove(actor);
                if (previous.Count == 0) retainedActors.Remove(actor.Cell);
                if (!retainedActors.TryGetValue(cell, out var next)) retainedActors.Add(cell, next = []);
                next.Add(actor);
                actor.Cell = cell;
                changedCells = true;
            }
        }
        foreach (var actor in retainedMovingActors) {
            var actors = retainedActors[actor.Cell];
            if (actors.Count > 1) actors.Sort(NativeSceneActorCommand.Compare);
        }
        return changedCells;
    }

    private static (double X, double Y) RetainedActorAnchor(NativeSceneActorCommand actor, NativeSceneViewport view)
        => view.CellBottomAnchor(actor.Position.X, actor.Position.Y);

    private sealed class NativeSceneActorCommand(int priority, uint entityId, Action draw, Func<bool>? visible)
    {
        public int Priority { get; } = priority;
        public uint EntityId { get; } = entityId;
        public Action Draw { get; } = draw;
        public Func<bool>? Visible { get; } = visible;
        public ClientPresentationPosition Position { get; set; }
        public (int X, int Y) Cell { get; set; }
        public Func<ClientPresentationPosition>? PositionProvider { get; init; }

        public static int Compare(NativeSceneActorCommand left, NativeSceneActorCommand right)
        {
            var comparison = NativeSceneActorPlacement.CompareDepth(left.Position, right.Position);
            if (comparison != 0) return comparison;
            comparison = left.Priority.CompareTo(right.Priority);
            return comparison != 0 ? comparison : left.EntityId.CompareTo(right.EntityId);
        }
    }
}
