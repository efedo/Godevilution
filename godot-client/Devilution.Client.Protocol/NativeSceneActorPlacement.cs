namespace Devilution.Client.Protocol;

/** Extends the existing (x+y,x,y) cell order to fractional actor feet without changing terrain commands. */
public static class NativeSceneActorPlacement
{
    public static (int X, int Y) OcclusionCell(ClientPresentationPosition position, int width, int height)
    {
        if (width <= 0 || height <= 0 || !double.IsFinite(position.X) || !double.IsFinite(position.Y))
            throw new ArgumentOutOfRangeException(nameof(position));
        var x = Math.Clamp(position.X, 0, width - 1d);
        var y = Math.Clamp(position.Y, 0, height - 1d);
        var depth = x + y;
        var row = (int)Math.Floor(depth);
        var first = Math.Max(0, row - height + 1);
        var last = Math.Min(width - 1, row);
        // Between diagonals, every wall on the preceding diagonal is behind the actor.
        // On a diagonal, retain its x tie-break, including actors between two cells.
        var column = depth == row ? Math.Clamp((int)Math.Floor(x), first, last) : last;
        return (column, row - column);
    }

    public static int CompareDepth(ClientPresentationPosition left, ClientPresentationPosition right)
    {
        var order = (left.X + left.Y).CompareTo(right.X + right.Y);
        if (order != 0) return order;
        order = left.X.CompareTo(right.X);
        return order != 0 ? order : left.Y.CompareTo(right.Y);
    }

    /** Invert the rendered center, including the smoothing already applied to remote actors. */
    public static ClientPresentationPosition FromScreen(NativeSceneViewport view, double screenX, double screenY)
    {
        var horizontal = (screenX - view.OriginX) / NativeSceneViewport.HalfTileWidth;
        var vertical = (screenY - view.OriginY) / NativeSceneViewport.HalfTileHeight;
        return new(Snap((horizontal + vertical) / 2), Snap((vertical - horizontal) / 2));
    }

    private static double Snap(double value)
        => Math.Abs(value - Math.Round(value)) < 0.00001 ? Math.Round(value) : value;
}
