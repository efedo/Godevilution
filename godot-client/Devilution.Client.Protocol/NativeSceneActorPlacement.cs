using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Native render-cell selection is independent of animation progress and pixel offsets. */
public static class NativeSceneActorPlacement
{
    /** world_renderer.cpp DrawDungeon: select the negative destination marker for southwards walks and East. */
    public static (int X, int Y) PlayerCell(PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.CurrentAction is { Kind: PlayerActionKind.Move, EffectApplied: false } move
            && (player.CombatMode == PlayerCombatMode.WalkSouthwards
                || player.CombatMode == PlayerCombatMode.WalkSideways && move.DirectionX == 1 && move.DirectionY == -1))
            return (player.PositionX + move.DirectionX, player.PositionY + move.DirectionY);
        return (player.PositionX, player.PositionY);
    }

    /** world_renderer.cpp DrawDungeon / monsters.cpp GetMonsterMovementMode: all other walks draw at the positive origin marker. */
    public static (int X, int Y) MonsterCell(MonsterSnapshot monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        // DrawDungeon checks the actual mode, so Petrified uses the origin despite its saved walking visual mode.
        if (monster.Moving && !monster.Petrified
            && (monster.FuturePositionX - monster.PositionX, monster.FuturePositionY - monster.PositionY)
                is (1, 1) or (0, 1) or (1, 0) or (1, -1))
            return (monster.FuturePositionX, monster.FuturePositionY);
        return (monster.PositionX, monster.PositionY);
    }

    /** Invert WorldPresentation.ToScreen's CellBottomAnchor, including remote-actor smoothing. */
    public static ClientPresentationPosition FromScreen(NativeSceneViewport view, double screenX, double screenY)
    {
        var horizontal = (screenX - view.OriginX) / NativeSceneViewport.HalfTileWidth;
        var vertical = (screenY - view.OriginY - NativeSceneViewport.BottomInclusiveOffset) / NativeSceneViewport.HalfTileHeight;
        return new(Snap((horizontal + vertical) / 2), Snap((vertical - horizontal) / 2));
    }

    private static double Snap(double value)
        => Math.Abs(value - Math.Round(value)) < 0.00001 ? Math.Round(value) : value;
}
