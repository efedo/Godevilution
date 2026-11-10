using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;

namespace Devilution.Client;

internal sealed record NativeTownerDrawObservation(int ResidentSlot, int TownerId, int PositionX, int PositionY,
    int NativeDirection, bool NativeArt, bool GenericFallback, string? PackId, string? AtlasPath,
    int DirectionIndex, int FrameIndex, int AlignmentWidth, float AnchorX, float AnchorY,
    float DrawX, float DrawY, int Width, int Height, double CosmeticTick, string? Diagnostic, bool AnchorInsideViewport)
{
    /** The live acceptance guard; placement must follow the renderer's shared bounds helper. */
    internal bool MatchesNativeDraw(NativeTownResidentSnapshot row, NativeTownerSprite sprite)
    {
        var frame = sprite.ResolveFrame(CosmeticTick, row.NativeDirection);
        var bounds = NativeScenePlacement.ResolveResidentBounds(AnchorX, AnchorY,
            sprite.AlignmentWidth, frame.Width, frame.Height);
        return AnchorInsideViewport && ResidentSlot == row.ResidentSlot
            && TownerId == row.TownerId && PositionX == row.PositionX && PositionY == row.PositionY
            && NativeDirection == row.NativeDirection
            && DirectionIndex == frame.DirectionIndex && FrameIndex == frame.FrameIndex
            && AlignmentWidth == sprite.AlignmentWidth
            && DrawX == (float)bounds.X && DrawY == (float)bounds.Y;
    }
}
