using Devilution.Client.PresentationAssets;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Observes actual drawing/resource use; never resolves bindings to make a check pass. */
    private async Task VerifyPresentationPackAsync(Main main, string directory, string phase)
    {
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PRESENTATION_PACK") ?? "default";
        Require(profile is "default" or "atlas" or "semantic" or "missing" or "undersized" or "invalid", "Unknown presentation acceptance profile.");
        var nativePlayerConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_PLAYER_PACK"));
        await Until(() => nativePlayerConfigured
                ? main.ObservedNativePlayerDrawing is { IsLoaded: true } or { UsedFallback: true }
                : profile == "semantic"
                    ? main.ObservedPlayerActorTextureBinding?.Binding is { IsLoaded: true } or { UsedFallback: true }
                    : main.ObservedPlayerTextureBinding is { IsLoaded: true } or { UsedFallback: true },
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
        if (nativePlayerConfigured) {
            var nativePack = main.ObservedNativePlayerPack;
            var nativePlayer = main.ObservedNativePlayerDrawing;
            Require(profile == "default" && nativePack is { IsLoaded: true, ContentIdentityMatched: true }
                && nativePack.ContentManifestHash == main.ObservedIdentity!.ContentManifestHash
                && nativePlayer is { IsLoaded: true, UsedFallback: false },
                "The native player draw did not bind its admitted content and loaded resource.");
            Require(pack.IsActive && pack.PackId == "sample-default"
                && pack.ManifestPath == "res://assets/presentation-pack.json"
                && pack.SchemaVersion == PresentationAssetCatalog.CurrentSchemaVersion
                && pack.LoadedTextureKeys.Contains("floor"),
                "Native player art changed the shared default floor/presentation pack.");
            VerifyHudLocalization(main, directory, phase, profile);
            WriteEvidence(directory, phase.ToLowerInvariant() + "-presentation-pack.json", new {
                Passed = true, Profile = "native-player", Pack = pack, NativePack = nativePack,
                Player = nativePlayer, FeedbackPack = feedbackPack,
            });
            return;
        }
        if (profile == "semantic") {
            var actor = main.ObservedPlayerActorTextureBinding
                ?? throw new InvalidOperationException("The semantic actor draw observation is missing.");
            Require(pack.IsActive && pack.PackId == "godevilution.acceptance-semantic"
                && actor.EntityId == main.ObservedSnapshot!.Players[0].EntityId
                && actor.IsSemanticSelection && actor.RuleId == SemanticIdleRule(main.ObservedSnapshot!.Players[0].FacingDirection)
                && actor.AssetId == "player.semantic"
                && actor.Binding is {
                    Key: "player.semantic", IsDeclared: true, IsLoaded: true, UsedFallback: false,
                    ResourcePath: "res://assets/player.svg",
                    FrameRegion: { Width: 16, Height: 16, X: 0 or 16, Y: 0 or 16 },
                },
                "The actual world draw did not use the selected semantic actor resource.");
            VerifyHudLocalization(main, directory, phase, profile);
            WriteEvidence(directory, phase.ToLowerInvariant() + "-presentation-pack.json", new {
                Passed = true, Profile = profile, Pack = pack, Actor = actor, FeedbackPack = feedbackPack,
            });
            return;
        }
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
        VerifyHudLocalization(main, directory, phase, profile);
        WriteEvidence(directory, phase.ToLowerInvariant() + "-presentation-pack.json", new {
            Passed = true, Profile = profile, Pack = pack, Player = player, FeedbackPack = feedbackPack,
            DrawnFrameRegions = observedRegions.OrderBy(region => region.Y).ThenBy(region => region.X).ToArray(),
        });
    }

    private static string SemanticIdleRule(Devilution.Protocol.V1.PlayerDirection direction)
        => direction switch {
            Devilution.Protocol.V1.PlayerDirection.South => "rogue-idle-south",
            Devilution.Protocol.V1.PlayerDirection.SouthWest => "rogue-idle-south-west",
            Devilution.Protocol.V1.PlayerDirection.West => "rogue-idle-west",
            Devilution.Protocol.V1.PlayerDirection.NorthWest => "rogue-idle-north-west",
            Devilution.Protocol.V1.PlayerDirection.North => "rogue-idle-north",
            Devilution.Protocol.V1.PlayerDirection.NorthEast => "rogue-idle-north-east",
            Devilution.Protocol.V1.PlayerDirection.East => "rogue-idle-east",
            Devilution.Protocol.V1.PlayerDirection.SouthEast => "rogue-idle-south-east",
            _ => string.Empty,
        };

    /** Confirms an accepted movement crossed the semantic action binding and retained fallback on a missing selected resource. */
    private async Task VerifySemanticActorTransitionAsync(
        Main main,
        string directory,
        string phase,
        PresentationPlayerTextureBindingObservation? initialBinding)
    {
        if ((Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PRESENTATION_PACK") ?? "default") != "semantic")
            return;
        var entityId = main.ObservedSnapshot?.Players.Single().EntityId
            ?? throw new InvalidOperationException("The semantic actor snapshot is missing.");
        await Until(() => main.ObservedPlayerActorTextureBindingHistory.Any(observation =>
                observation.EntityId == entityId
                && observation.IsSemanticSelection
                && observation.RuleId == "rogue-move-south-west"),
            "The actual world draw never observed the semantic movement action.", 5);
        var move = main.ObservedPlayerActorTextureBindingHistory.Single(observation =>
            observation.RuleId == "rogue-move-south-west");
        Require(move.AssetId == "player.semantic.move"
            && move.Binding is { IsDeclared: true, IsLoaded: false, UsedFallback: true }
            && move.Binding.ResourcePath == "res://assets/semantic-move-missing.svg",
            "A declared semantic movement resource did not retain procedural fallback.");
        WriteEvidence(directory, phase.ToLowerInvariant() + "-semantic-actor.json", new {
            Passed = true, EntityId = entityId, Initial = initialBinding,
            History = main.ObservedPlayerActorTextureBindingHistory,
        });
    }
}
