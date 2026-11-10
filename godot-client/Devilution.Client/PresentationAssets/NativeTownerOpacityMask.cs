namespace Devilution.Client.PresentationAssets;

/** CPU alpha retained once per validated atlas; picking never reads back a GPU texture. */
public sealed class NativeTownerOpacityMask
{
    private readonly byte[] alpha;
    private readonly int width, height;

    public NativeTownerOpacityMask(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0 || (rgba.Length % 4 != 0 || (long)width * height != rgba.Length / 4))
            throw new ArgumentException("A resident opacity mask needs a complete RGBA8 atlas.", nameof(rgba));
        this.width = width; this.height = height;
        alpha = new byte[checked(width * height)];
        for (var pixel = 0; pixel < alpha.Length; pixel++) alpha[pixel] = rgba[pixel * 4 + 3];
    }

    /** Uses the same rounded destination and frame region as DrawTextureRectRegion. */
    public bool HitTest(NativeTownerFrame frame, int alignmentWidth, double anchorX, double anchorY, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;
        var bounds = NativeScenePlacement.ResolveResidentBounds(anchorX, anchorY, alignmentWidth, frame.Width, frame.Height);
        var localX = Math.Floor(x - (float)bounds.X);
        var localY = Math.Floor(y - (float)bounds.Y);
        if (localX < 0 || localY < 0 || localX >= frame.Width || localY >= frame.Height) return false;
        var atlasX = (long)frame.X + (int)localX;
        var atlasY = (long)frame.Y + (int)localY;
        return atlasX >= 0 && atlasX < width && atlasY >= 0 && atlasY < height
            && alpha[(int)(atlasY * width + atlasX)] != 0;
    }
}
