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
        => nativeSceneViewport is { } view && displayedPositions.TryGetValue(entityId, out var anchor)
            ? NativeSceneActorPlacement.FromScreen(view, anchor.X, anchor.Y) : fallback;

    private void AddNativePlayerActor(Dictionary<(int X, int Y), List<NativeSceneActorCommand>> commands,
        PlayerSnapshot player, Action draw)
        => AddNativeSceneActorCommand(commands, player.PositionX, player.PositionY, 0, player.EntityId, 0, 0, draw,
            position: () => ResolveNativePlayerPosition(player), cell: () => NativeSceneActorPlacement.PlayerCell(player));

    private void AddNativeMonsterActor(Dictionary<(int X, int Y), List<NativeSceneActorCommand>> commands,
        MonsterSnapshot monster, Action draw)
        => AddNativeSceneActorCommand(commands, monster.PositionX, monster.PositionY, 1, monster.EntityId, 0, 0, draw,
            position: () => ResolveNativeMonsterPosition(monster), cell: () => NativeSceneActorPlacement.MonsterCell(monster));

    private void AddNativeSceneActorCommand(
        Dictionary<(int X, int Y), List<NativeSceneActorCommand>> commands,
        int x, int y, int priority, uint entityId, float anchorX, float anchorY, Action draw,
        Func<bool>? visible = null, Func<ClientPresentationPosition>? position = null, Func<(int X, int Y)>? cell = null)
    {
        if (nativeSceneViewport is not { } view || x < 0 || y < 0 || x >= view.LevelWidth || y >= view.LevelHeight)
            return;
        var point = position?.Invoke() ?? new ClientPresentationPosition(x, y);
        var marker = cell?.Invoke() ?? (x, y);
        if (!commands.TryGetValue(marker, out var actors)) commands.Add(marker, actors = []);
        actors.Add(new(priority, entityId, draw, visible) {
            Position = point, Cell = marker, PositionProvider = position, CellProvider = cell,
        });
    }

    private void BindRetainedActorPositions()
    {
        retainedMovingActors.Clear();
        foreach (var actors in retainedActors.Values)
            foreach (var actor in actors)
                if (actor.PositionProvider is not null) retainedMovingActors.Add(actor);
    }

    /** Refresh pixel offsets; only a native occupancy-marker change can move an actor between retained cells. */
    private bool RefreshRetainedActorPositions(NativeSceneViewport view)
    {
        var changedCells = false;
        foreach (var actor in retainedMovingActors) {
            actor.Position = actor.PositionProvider!();
            var cell = actor.CellProvider?.Invoke() ?? actor.Cell;
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
        public Func<(int X, int Y)>? CellProvider { get; init; }

        public static int Compare(NativeSceneActorCommand left, NativeSceneActorCommand right)
        {
            var comparison = left.Priority.CompareTo(right.Priority);
            return comparison != 0 ? comparison : left.EntityId.CompareTo(right.EntityId);
        }
    }
}
