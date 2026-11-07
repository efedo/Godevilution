namespace Devilution.Client.Protocol;

[Flags]
public enum NativeSceneRefresh { None = 0, Geometry = 1, Viewport = 2, Actors = 4, Visibility = 8 }

/** Camera changes scroll/cull retained commands; geometry and admitted state have separate invalidation. */
public sealed class NativeSceneRetentionState
{
    public const int FloorChunkCells = 32;
    private object? geometry;
    private uint level;
    private NativeSceneViewport viewport;
    private ulong revision;
    private int light;
    private bool initialized;

    public NativeSceneRefresh Update(object nextGeometry, uint nextLevel, NativeSceneViewport nextViewport,
        ulong nextRevision, int lightRadius)
    {
        var result = NativeSceneRefresh.None;
        if (!initialized || !ReferenceEquals(geometry, nextGeometry) || level != nextLevel)
            result |= NativeSceneRefresh.Geometry | NativeSceneRefresh.Viewport | NativeSceneRefresh.Actors | NativeSceneRefresh.Visibility;
        if (!initialized || viewport.OriginX != nextViewport.OriginX || viewport.OriginY != nextViewport.OriginY
            || viewport.ViewportWidth != nextViewport.ViewportWidth || viewport.ViewportHeight != nextViewport.ViewportHeight)
            result |= NativeSceneRefresh.Viewport;
        if (!initialized || revision != nextRevision) result |= NativeSceneRefresh.Actors;
        if (!initialized || light != lightRadius) result |= NativeSceneRefresh.Visibility | NativeSceneRefresh.Actors;
        geometry = nextGeometry; level = nextLevel; viewport = nextViewport; revision = nextRevision; light = lightRadius; initialized = true;
        return result;
    }

    public void Invalidate() => initialized = false;

    /** Exact inverse of the original half-open 64-by-(microTileLength/2*32) piece bounds. */
    public static IEnumerable<(int X, int Y)> VisiblePieceCells(NativeSceneViewport view, byte microTileLength)
    {
        var minDifference = (-view.OriginX - 32) / 32;
        var maxDifference = (view.ViewportWidth - view.OriginX + 32) / 32;
        var minSum = Math.Max(0, (int)Math.Floor((-view.OriginY - 16) / 16) + 1);
        var maxSum = Math.Min(view.LevelWidth + view.LevelHeight - 2,
            (int)Math.Ceiling((view.ViewportHeight - view.OriginY + 16 + (microTileLength / 2 - 1) * 32) / 16) - 1);
        for (var sum = minSum; sum <= maxSum; sum++) {
            var first = Math.Max(Math.Max(0, sum - view.LevelHeight + 1), (int)Math.Floor((sum + minDifference) / 2) + 1);
            var last = Math.Min(Math.Min(view.LevelWidth - 1, sum), (int)Math.Ceiling((sum + maxDifference) / 2) - 1);
            for (var x = first; x <= last; x++) yield return (x, sum - x);
        }
    }
}
