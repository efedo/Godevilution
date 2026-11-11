using Devilution.Client.Protocol;
using Godot;

namespace Devilution.Client;

/**
 * The DevilutionX brightness option as a full-screen pass above the world, HUD and menus,
 * like the native palette change. Hidden at 0, so the default frame is untouched.
 */
public partial class ScreenBrightness : CanvasLayer
{
    private const string ShaderCode = """
        shader_type canvas_item;
        uniform sampler2D screen : hint_screen_texture, filter_nearest;
        uniform float tone = 0.0;
        void fragment() {
            vec3 source = texture(screen, SCREEN_UV).rgb;
            vec3 mapped = clamp(tone * source * source + (1.0 - tone) * source, 0.0, 1.0);
            COLOR = vec4(floor(mapped * 255.0 + 0.5) / 255.0, 1.0);
        }
        """;

    private readonly ColorRect overlay = new() { Name = "BrightnessOverlay", MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly ShaderMaterial material = new() { Shader = new Shader { Code = ShaderCode } };

    public int Brightness { get; private set; }

    public ScreenBrightness()
    {
        Name = "ScreenBrightness";
        Layer = 128;
        overlay.Material = material;
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);
        SetBrightness(NativeBrightnessSetting.Default);
    }

    /** Applies the native tone curve (0 leaves every pixel unchanged). */
    public void SetBrightness(int brightness)
    {
        Brightness = NativeBrightness.Normalize(brightness);
        material.SetShaderParameter("tone", NativeBrightness.ToneMappingParameter(Brightness));
        overlay.Visible = Brightness != NativeBrightness.Minimum;
    }
}

/** Godot ConfigFile adapter for the client settings file. */
internal sealed class ConfigFileSettingsStore(ConfigFile file) : IClientSettingsStore
{
    public bool TryGetInt(string section, string key, out int value)
    {
        value = 0;
        if (!file.HasSectionKey(section, key)) return false;
        var stored = file.GetValue(section, key);
        switch (stored.VariantType) {
        case Variant.Type.Int: value = (int)Math.Clamp(stored.AsInt64(), int.MinValue, int.MaxValue); return true;
        case Variant.Type.Float: value = (int)Math.Clamp(stored.AsDouble(), int.MinValue, int.MaxValue); return true;
        default: return false;
        }
    }

    public void SetInt(string section, string key, int value) => file.SetValue(section, key, value);
}
