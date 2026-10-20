using Devilution.Client.PresentationAssets;
using Godot;

namespace Devilution.Client.Acceptance;

/** Optional original-art evidence collected from actual ordinary class-journey draws. */
public partial class SessionAcceptance
{
    private Main? nativePlayerArtMain;
    private readonly List<NativePlayerDrawObservation> nativePlayerArtDraws = [];
    private readonly HashSet<string> nativePlayerArtDrawKeys = new(StringComparer.Ordinal);

    public override void _Process(double delta)
    {
        if (nativePlayerArtMain is not { } main)
            return;
        CaptureNativePlayerArtDraws(main);
    }

    private void StartNativePlayerArtCapture(Main main)
    {
        if (string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_PLAYER_PACK")))
            return;
        nativePlayerArtDraws.Clear();
        nativePlayerArtDrawKeys.Clear();
        nativePlayerArtMain = main;
        CaptureNativePlayerArtDraws(main);
    }

    private void CaptureNativePlayerArtDraws(Main main)
    {
        foreach (var draw in main.ObservedNativePlayerDrawingHistory) {
            var key = $"{draw.ClipKey}/{draw.DirectionIndex}/{draw.FrameIndex}/{draw.IsLoaded}/{draw.UsedFallback}";
            if (nativePlayerArtDraws.Count < 512 && nativePlayerArtDrawKeys.Add(key))
                nativePlayerArtDraws.Add(draw);
        }
    }

    private object? VerifyNativePlayerArtCapture(Main main, bool resumed)
    {
        if (nativePlayerArtMain is null)
            return null;
        CaptureNativePlayerArtDraws(main);
        var pack = main.ObservedNativePlayerPack;
        Require(pack is { IsLoaded: true, ContentIdentityMatched: true }
            && pack.ContentManifestHash == main.ObservedIdentity!.ContentManifestHash,
            "The requested native player pack did not bind the admitted content identity.");
        var player = main.ObservedSnapshot!.Players.Single();
        var weapon = player.CharacterClassId == 0 ? NativePlayerWeapon.SwordShield : NativePlayerWeapon.Staff;
        var expected = new List<NativePlayerClipKey> {
            new(player.CharacterClassId, NativePlayerLocation.Town, NativePlayerArmor.Light, weapon, NativePlayerGraphic.TownStand),
        };
        if (!resumed) {
            expected.Add(new(player.CharacterClassId, NativePlayerLocation.Town, NativePlayerArmor.Light, weapon, NativePlayerGraphic.TownWalk));
            expected.Add(new(player.CharacterClassId, NativePlayerLocation.Cathedral, NativePlayerArmor.Light, weapon, NativePlayerGraphic.Stand));
            expected.Add(new(player.CharacterClassId, NativePlayerLocation.Cathedral, NativePlayerArmor.Light, weapon, NativePlayerGraphic.Walk));
            if (player.CharacterClassId == 0)
                expected.Add(new(player.CharacterClassId, NativePlayerLocation.Cathedral, NativePlayerArmor.Light, weapon, NativePlayerGraphic.Attack));
            else {
                expected.Add(new(player.CharacterClassId, NativePlayerLocation.Cathedral, NativePlayerArmor.Light, weapon, NativePlayerGraphic.Lightning));
                expected.Add(new(player.CharacterClassId, NativePlayerLocation.Cathedral, NativePlayerArmor.Light, weapon, NativePlayerGraphic.Fire));
            }
        }
        foreach (var key in expected) {
            var formatted = NativePlayerCatalog.FormatKey(key);
            Require(nativePlayerArtDraws.Any(draw => draw.ClipKey == formatted && draw.IsLoaded && !draw.UsedFallback),
                $"The ordinary class journey did not draw native clip '{formatted}'.");
        }
        var loaded = nativePlayerArtDraws.Where(draw => draw.IsLoaded && !draw.UsedFallback).ToArray();
        Require(loaded.Length > 0 && loaded.All(draw => draw.RegionWidth > 0 && draw.RegionHeight > 0
            && draw.DestinationWidth == draw.RegionWidth && draw.DestinationHeight == draw.RegionHeight
            && draw.AnchorWidth is > 0 && draw.AnchorWidth <= draw.RegionWidth
            && draw.DestinationX == draw.AnchorX - draw.AnchorWidth / 2
            && draw.DestinationY == draw.AnchorY - draw.DestinationHeight + 1
            && draw.DirectionIndex is >= 0 and < 8 && draw.FrameIndex >= 0),
            "Native draw evidence changed its unscaled atlas region, eight-way direction or bottom anchor.");
        Require(loaded.Any(draw => draw.FrameIndex > 0), "Native class art never advanced beyond its first frame.");
        return new { Passed = true, Pack = pack, Draws = nativePlayerArtDraws.ToArray(), RenderedParity = false };
    }
}
