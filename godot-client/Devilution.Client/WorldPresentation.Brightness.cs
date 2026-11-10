using Godot;

namespace Devilution.Client;

/** Native presentation filters; brightness is a full-screen pass (ScreenBrightness). */
public partial class WorldPresentation
{
    public override void _EnterTree()
    {
        // Native art is palette pixel art; linear filtering blends it at fractional positions.
        TextureFilter = TextureFilterEnum.Nearest;
    }
}
