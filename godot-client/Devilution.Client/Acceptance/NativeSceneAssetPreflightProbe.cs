using Godot;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

/** Headless probe for the scene-pack fallback boundary. */
public partial class NativeSceneAssetPreflightProbe : Node
{
    public override void _Ready()
    {
        try {
            VerifyPreflightRefusesBrokenAtlas();
            GD.Print("Native scene atlas preflight probe passed.");
            GetTree().Quit(0);
        } catch (Exception exception) {
            GD.PushError($"Native scene atlas preflight probe failed: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void VerifyPreflightRefusesBrokenAtlas()
    {
        var expectedFailure = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SCENE_PREFLIGHT_EXPECT");
        if (expectedFailure is not ("missing" or "hash-mismatch"))
            throw new InvalidOperationException("Set GODEVILUTION_NATIVE_SCENE_PREFLIGHT_EXPECT to 'missing' or 'hash-mismatch'.");
        var contentHash = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SCENE_PREFLIGHT_CONTENT_HASH");
        if (contentHash is null || contentHash.Length != 64)
            throw new InvalidOperationException("Set GODEVILUTION_NATIVE_SCENE_PREFLIGHT_CONTENT_HASH to a bound SHA-256 identity.");
        if (!uint.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SCENE_PREFLIGHT_LEVEL_ID"), out var levelId))
            throw new InvalidOperationException("Set GODEVILUTION_NATIVE_SCENE_PREFLIGHT_LEVEL_ID to a bound Cathedral level ID.");

        var assets = new NativeSceneAssets();
        assets.SetContentManifestHash(contentHash);
        var loadedPack = assets.ObservePack();
        Require(loadedPack.IsLoaded && loadedPack.IdentityMatched
            && loadedPack.ContentManifestHash == contentHash,
            "The probe catalog did not load with its bound content identity.");

        var level = new LevelLayout(levelId, "Native scene preflight probe", 1, 1,
            new HashSet<int>(), new HashSet<int>(), []) {
            NativePieceLayer = [0],
            NativeSpecialLayer = [],
        };
        var resolved = assets.TryResolveFamily(contentHash, levelId, level, out var family, out var diagnostic);
        Require(!resolved && family is null && !string.IsNullOrWhiteSpace(diagnostic),
            "A broken native scene atlas resolved a family and would activate the native viewport.");
        var expectedDiagnostic = expectedFailure == "missing" ? "unavailable" : "SHA-256";
        Require(diagnostic!.Contains(expectedDiagnostic, StringComparison.OrdinalIgnoreCase),
            $"Expected a {expectedFailure} atlas diagnostic, received: {diagnostic}");

        var repeated = assets.TryResolveFamily(contentHash, levelId, level, out _, out var repeatedDiagnostic);
        Require(!repeated && repeatedDiagnostic == diagnostic,
            "A failed family preflight did not retain its bounded cached diagnostic.");
        var observed = assets.ObservePack();
        Require(observed.IsLoaded && observed.IdentityMatched
            && observed.MissingAtlasPaths.Count > 0,
            "The failed asset was not reported while preserving the matched catalog identity.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
