using System.Globalization;
using System.Text.Json;
using Devilution.Client;
using Godot;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

/** Headless probe for exact native-object identity binding and complete texture preflight. */
public partial class NativeObjectAssetPreflightProbe : Node
{
    public override void _Ready()
    {
        try {
            VerifyObjectPackPreflight();
            GD.Print("Native object atlas preflight probe passed.");
            GetTree().Quit(0);
        } catch (Exception exception) {
            GD.PushError($"Native object atlas preflight probe failed: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void VerifyObjectPackPreflight()
    {
        var contentHash = Environment.GetEnvironmentVariable("GODEVILUTION_OBJECT_PROBE_CONTENT");
        var levelText = Environment.GetEnvironmentVariable("GODEVILUTION_OBJECT_PROBE_LEVEL");
        var levelId = 1U;
        if (!string.IsNullOrWhiteSpace(levelText)
            && !uint.TryParse(levelText, NumberStyles.None, CultureInfo.InvariantCulture, out levelId))
            throw new InvalidOperationException("GODEVILUTION_OBJECT_PROBE_LEVEL must be an unsigned level ID.");
        var expectedText = Environment.GetEnvironmentVariable("GODEVILUTION_OBJECT_PROBE_EXPECT_ACTIVE") ?? "1";
        if (expectedText is not ("0" or "1"))
            throw new InvalidOperationException("GODEVILUTION_OBJECT_PROBE_EXPECT_ACTIVE must be '0' or '1'.");
        var expectActive = expectedText == "1";

        var assets = new NativeObjectAssets();
        var active = assets.Bind(contentHash, levelId);
        var observation = assets.ObservePack();
        var accessibleTextures = Enumerable.Range(0, 8).Count(id => assets.TryGetTexture(id, out _));
        var bindingTransitionPassed = true;
        if (active) {
            bindingTransitionPassed = !assets.Bind(contentHash, 0)
                && Enumerable.Range(0, 8).All(id => !assets.TryGetTexture(id, out _))
                && assets.Bind(contentHash, levelId)
                && Enumerable.Range(0, 8).All(id => assets.TryGetTexture(id, out _));
        }
        var passed = bindingTransitionPassed && accessibleTextures == (active ? 8 : 0)
            && active == expectActive && (expectActive
            ? observation.IsLoaded
                && observation.IdentityMatched
                && observation.AtlasesReady
                && observation.LoadedAtlasPaths.Count == 8
                && observation.MissingAtlasPaths.Count == 0
            : !string.IsNullOrWhiteSpace(observation.Diagnostic));

        var output = Environment.GetEnvironmentVariable("GODEVILUTION_OBJECT_PROBE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
            System.IO.File.WriteAllText(output, JsonSerializer.Serialize(new { Passed = passed, Active = active, AccessibleTextures = accessibleTextures, BindingTransitionPassed = bindingTransitionPassed, Pack = observation }));
        if (!passed)
            throw new InvalidOperationException($"Object pack preflight result did not match expectation. Active={active}; expected={expectActive}; diagnostic={observation.Diagnostic}");
    }
}
