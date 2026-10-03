using Devilution.Client.PresentationAssets;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Observes actual drawing/resource use; never resolves bindings to make a check pass. */
    private async Task VerifyPresentationPackAsync(Main main, string directory, string phase)
    {
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PRESENTATION_PACK") ?? "default";
        Require(profile is "default" or "atlas" or "missing" or "undersized" or "invalid", "Unknown presentation acceptance profile.");
        await Until(() => main.ObservedPlayerTextureBinding is { IsLoaded: true } or { UsedFallback: true },
            "The real world draw never observed its player binding.", 5);
        var pack = main.ObservedPresentationPack
            ?? throw new InvalidOperationException("Presentation pack observation is missing.");
        var player = main.ObservedPlayerTextureBinding!;
        var feedbackPack = main.ObservedFeedbackPresentationPack
            ?? throw new InvalidOperationException("Feedback presentation pack observation is missing.");
        Require(feedbackPack.IsActive == pack.IsActive && feedbackPack.PackId == pack.PackId
            && feedbackPack.ManifestPath == pack.ManifestPath,
            "World and feedback did not use the same selected presentation pack.");
        foreach (var key in new[] { "damage", "healing", "command_rejected" })
            Require(feedbackPack.LoadedAudioKeys.Contains(key) || feedbackPack.MissingAudioKeys.Contains(key),
                "The real feedback node did not resolve its optional audio binding.");
        Require(player.Key == "player", "The draw observation does not describe the player binding.");
        var observedRegions = new HashSet<PresentationFrameRegion>();
        if (profile == "invalid") {
            Require(!pack.IsActive && pack.PackId is null && !string.IsNullOrWhiteSpace(pack.Diagnostic),
                "An unsafe pack was activated or its refusal was hidden.");
            Require(!player.IsDeclared && player.UsedFallback && player.ResourcePath != "res://player.svg",
                "A refused pack binding bypassed the safe fallback.");
        } else {
            Require(pack.IsActive && pack.SchemaVersion == PresentationAssetCatalog.CurrentSchemaVersion,
                "The presentation pack was not activated.");
            Require(pack.LoadedTextureKeys.Contains("floor") && player.IsDeclared,
                "The real draw did not load the declared floor/player bindings.");
            if (profile is "missing" or "undersized") {
                Require(pack.PackId == "godevilution.acceptance-" + profile
                    && pack.MissingTextureKeys.Contains("player")
                    && !player.IsLoaded && player.UsedFallback && player.FrameRegion is null,
                    "A missing or undersized declared texture did not retain procedural fallback.");
            } else {
                Require(player.IsLoaded && !player.UsedFallback && pack.LoadedTextureKeys.Contains("player"),
                    "The declared player texture was not actually loaded.");
                Require(player.ResourcePath == "res://assets/player.svg", "The player used an unexpected texture source.");
                if (profile == "default") {
                    Require(pack.PackId == "sample-default"
                        && pack.ManifestPath == "res://assets/presentation-pack.json" && player.FrameRegion is null,
                        "Default appearance changed or a different manifest was selected.");
                } else {
                    Require(pack.PackId == "godevilution.acceptance-atlas", "The atlas fixture was not activated.");
                    await Until(() => {
                        if (main.ObservedPlayerTextureBinding?.FrameRegion is { } region) {
                            Require(region.Width == 16 && region.Height == 16
                                && region.X is 0 or 16 && region.Y is 0 or 16,
                                "The actual atlas region escaped the declared texture layout.");
                            observedRegions.Add(region);
                        }
                        return observedRegions.Count >= 2;
                    }, "The real draw did not animate at least two atlas frames.", 5);
                }
            }
        }
        WriteEvidence(directory, phase.ToLowerInvariant() + "-presentation-pack.json", new {
            Passed = true, Profile = profile, Pack = pack, Player = player, FeedbackPack = feedbackPack,
            DrawnFrameRegions = observedRegions.OrderBy(region => region.Y).ThenBy(region => region.X).ToArray(),
        });
    }
}
