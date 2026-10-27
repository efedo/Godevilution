using Devilution.Client.Protocol;
using Godot;

namespace Devilution.Client;

/** Native presentation filters: pixel-exact sampling and the DevilutionX brightness curve. */
public partial class WorldPresentation
{
    private const string BrightnessShaderCode = """
        shader_type canvas_item;
        uniform float tone = 0.0;
        void fragment() {
            vec3 mapped = clamp(tone * COLOR.rgb * COLOR.rgb + (1.0 - tone) * COLOR.rgb, 0.0, 1.0);
            COLOR.rgb = floor(mapped * 255.0 + 0.5) / 255.0;
        }
        """;

    private ShaderMaterial? brightnessMaterial;

    public int Brightness { get; private set; }

    public override void _EnterTree()
    {
        // Native art is palette pixel art; linear filtering blends it at fractional positions.
        TextureFilter = TextureFilterEnum.Nearest;
        brightnessMaterial ??= new ShaderMaterial { Shader = new Shader { Code = BrightnessShaderCode } };
        Material = brightnessMaterial;
        SetBrightness(Brightness);
    }

    /** Applies the native brightness option (0 leaves the palette unchanged). */
    public void SetBrightness(int brightness)
    {
        Brightness = NativeBrightness.Normalize(brightness);
        brightnessMaterial?.SetShaderParameter("tone", NativeBrightness.ToneMappingParameter(Brightness));
    }
}
