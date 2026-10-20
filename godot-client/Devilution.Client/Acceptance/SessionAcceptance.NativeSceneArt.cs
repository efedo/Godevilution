namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private sealed record NativeSceneArtSample(NativeSceneDrawingObservation Drawing, bool PointerRoundTrip);

    private Main? nativeSceneArtMain;
    private readonly List<NativeSceneArtSample> nativeSceneArtSamples = [];
    private readonly HashSet<string> nativeSceneArtSampleKeys = new(StringComparer.Ordinal);

    private void StartNativeSceneArtCapture(Main main)
    {
        if (string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SCENE_PACK")))
            return;
        nativeSceneArtSamples.Clear();
        nativeSceneArtSampleKeys.Clear();
        nativeSceneArtMain = main;
        CaptureNativeSceneArtDraws();
    }

    /** Samples observations emitted by _Draw; snapshot counts cannot satisfy this gate. */
    private void CaptureNativeSceneArtDraws()
    {
        if (nativeSceneArtMain is not { } main || main.ObservedNativeSceneDrawing is not { } draw)
            return;
        var player = main.ObservedSnapshot?.Players.FirstOrDefault();
        if (player is null || player.LevelId != draw.LevelId)
            return;
        var pointerRoundTrip = main.ObservedNativeScenePointerRoundTrip;
        var key = $"{draw.LevelId}/{draw.NativeSceneActive}/{draw.FloorRasterDrawCount}/{draw.ContentRasterDrawCount}"
            + $"/{draw.ActorDrawCount}/{draw.SpecialRasterDrawCount}/{draw.MissingAtlasPaths.Count}/{pointerRoundTrip}";
        if (nativeSceneArtSamples.Count < 128 && nativeSceneArtSampleKeys.Add(key))
            nativeSceneArtSamples.Add(new NativeSceneArtSample(draw, pointerRoundTrip));
    }

    private object? VerifyNativeSceneArtCapture(Main main, bool resumed)
    {
        if (nativeSceneArtMain is null)
            return null;
        CaptureNativeSceneArtDraws();
        var pack = main.ObservedNativeScenePack;
        Require(pack is { IsLoaded: true, IdentityMatched: true }
            && pack.ContentManifestHash == main.ObservedIdentity!.ContentManifestHash,
            "The requested native scene pack did not bind the admitted content identity.");
        var active = nativeSceneArtSamples.Where(sample => sample.Drawing.NativeSceneActive).ToArray();
        if (!resumed) {
            Require(active.Any(sample => sample.Drawing.Location == "cathedral"
                    && sample.Drawing.FloorRasterDrawCount > 0
                    && sample.Drawing.ContentRasterDrawCount > 0
                    && sample.Drawing.ActorDrawCount > 0
                    && sample.Drawing.TextureDrawCount > 0
                    && sample.Drawing.LoadedAtlasPaths.Count > 0
                    && sample.PointerRoundTrip),
                "The ordinary class journey did not issue identity-bound native Cathedral floor/content/actor draws with shared pointer input.");
        }
        if (nativeTownClassProfile) {
            Require(active.Any(sample => sample.Drawing.Location == "town" && sample.Drawing.LevelId == 0
                    && sample.Drawing.FloorRasterDrawCount > 0 && sample.Drawing.ContentRasterDrawCount > 0
                    && sample.Drawing.ActorDrawCount > 0 && sample.Drawing.TextureDrawCount > 0
                    && sample.Drawing.LoadedAtlasPaths.Count > 0 && sample.PointerRoundTrip),
                "The native Town journey did not issue native floor/content/actor draws with shared pointer input.");
        } else if (resumed) {
            // Existing profiles restore in Town, whose native layers are still absent.
            Require(nativeSceneArtSamples.Any(sample => sample.Drawing.GenericFallback
                    && sample.Drawing.LevelId == main.ObservedSnapshot!.Players.Single().LevelId
                    && sample.PointerRoundTrip
                    && !string.IsNullOrWhiteSpace(sample.Drawing.Diagnostic)),
                "The existing Town profile did not report its explicit scene fallback after exact cold restore.");
        }
        Require(active.All(sample => sample.Drawing.IdentityMatched
                && sample.Drawing.ContentManifestHash == main.ObservedIdentity!.ContentManifestHash
                && sample.Drawing.MissingAtlasPaths.Count == 0
                && sample.PointerRoundTrip
                && !string.IsNullOrWhiteSpace(sample.Drawing.ScopeLimitations)),
            "Native scene drawing lost identity, loaded resources, pointer mapping or its explicit parity limits.");
        return new {
            Passed = true,
            Pack = pack,
            Samples = nativeSceneArtSamples.ToArray(),
            TownProfileIntegration = nativeTownClassProfile,
            RenderedParity = false,
        };
    }
}
