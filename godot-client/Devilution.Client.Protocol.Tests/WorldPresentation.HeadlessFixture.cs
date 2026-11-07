using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client;

// Link the actual retained actor scheduling/observation partial; drawing and texture loading need Godot.
public partial class WorldPresentation
{
    private Snapshot? snapshot;
    private ClientPresentationPosition? localVisualPosition;
    private double? nativePlayerPresentationTick;
    private NativeSceneViewport? nativeSceneViewport;
    private readonly Dictionary<uint, Godot.Vector2> displayedPositions = [];
    private readonly Dictionary<(int X, int Y), List<NativeSceneActorCommand>> retainedActors = [];

    internal void BeginRetainedPlayerTest(PlayerSnapshot player, ClientPresentationPosition visual, NativeSceneViewport view)
    {
        snapshot = new() { Tick = 10, Players = { player } };
        localVisualPosition = visual;
        nativePlayerPresentationTick = 10;
        nativeSceneViewport = view;
        AddNativeSceneActorCommand(retainedActors, player.PositionX, player.PositionY, 0, player.EntityId, 0, 0,
            () => { }, position: () => ResolveNativePlayerPosition(player));
        BindRetainedActorPositions();
    }

    internal void AddRetainedStaticTestActor(uint id, int x, int y)
        => AddNativeSceneActorCommand(retainedActors, x, y, 2, id, 0, 0, () => { });

    internal void AddRetainedRemoteTestActor(uint id, bool monster, Godot.Vector2 renderedCenter)
    {
        displayedPositions[id] = renderedCenter;
        if (monster) {
            var actor = new MonsterSnapshot { EntityId = id, PositionX = 52, PositionY = 50 };
            AddNativeSceneActorCommand(retainedActors, 52, 50, 1, id, 0, 0, () => { },
                position: () => ResolveNativeMonsterPosition(actor));
        } else {
            var actor = new PlayerSnapshot { EntityId = id, PositionX = 52, PositionY = 50 };
            AddNativeSceneActorCommand(retainedActors, 52, 50, 0, id, 0, 0, () => { },
                position: () => ResolveNativePlayerPosition(actor));
        }
        BindRetainedActorPositions();
    }

    internal bool AdvanceRetainedTest(ClientPresentationPosition visual, NativeSceneViewport view)
    {
        localVisualPosition = visual;
        nativeSceneViewport = view;
        return RefreshRetainedActorPositions(view);
    }

    internal (double X, double Y) RetainedTestAnchor(uint id)
        => RetainedActorAnchor(retainedActors.Values.SelectMany(value => value).Single(value => value.EntityId == id), nativeSceneViewport!.Value);
    internal (int X, int Y) RetainedTestCell(uint id)
        => retainedActors.Values.SelectMany(value => value).Single(value => value.EntityId == id).Cell;
    internal int RetainedTestActorCount => retainedActors.Values.Sum(value => value.Count);
}
