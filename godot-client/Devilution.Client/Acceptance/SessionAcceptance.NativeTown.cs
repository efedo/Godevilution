using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;

namespace Devilution.Client.Acceptance;

/** Distinct opt-in Town identity, service controls, and complete wire geometry evidence. */
public partial class SessionAcceptance
{
    private bool nativeTownClassProfile;
    private string? nativeTownCatalogFingerprint;

    private void ValidateNativeTownClassProfile(JsonElement profile, string contentHash, uint classId)
    {
        var townPath = System.Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_TOWN_PROFILE");
        nativeTownClassProfile = !string.IsNullOrWhiteSpace(townPath);
        // An all-class profile's inner progression describes its default Warrior under the all-class policy.
        var allClass = profile.GetProperty("populationPolicy").GetString() == "original-diablo-single-player-normal-all-classes-town-l1-l4";
        Require(profile.GetProperty("adapterId").GetString() == "devilution.diablo.cathedral-native-class-progression.v1"
            && profile.GetProperty("formatVersion").GetInt32() == 4
            && profile.GetProperty("heroClassSymbol").GetString() == (allClass ? "warrior" : classId == 0 ? "warrior" : "sorcerer")
            && (allClass ? nativeTownClassProfile : classId != 1),
            "The native class journey does not match its class-bound profile.");
        if (!nativeTownClassProfile) {
            Require(profile.GetProperty("contentManifestHash").GetString() == contentHash,
                "The native class journey content identity differs from its format-4 profile.");
            return;
        }
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(townPath!));
        var town = document.RootElement;
        var fields = new[] { "formatVersion", "adapterId", "classProfile", "townDirectory", "townPackId",
            "townPackVersion", "townPackTreeHash", "townCatalogFingerprint", "townInitialSeed", "contentManifestHash" };
        if (allClass)
            fields = [.. fields, "classes"];
        Require(town.EnumerateObject().Select(property => property.Name).SequenceEqual(fields)
            && town.GetProperty("formatVersion").GetInt32() == (allClass ? 6 : 5)
            && town.GetProperty("adapterId").GetString() == (allClass
                ? "devilution.diablo.cathedral-native-all-class-town.v1" : "devilution.diablo.cathedral-native-town-progression.v1")
            && (!allClass || town.GetProperty("classes").EnumerateArray().Any(start => start.GetProperty("classId").GetUInt32() == classId))
            && town.GetProperty("contentManifestHash").GetString() == contentHash
            && town.GetProperty("townDirectory").GetString() == "native_town_geometry"
            && JsonNode.DeepEquals(JsonNode.Parse(profile.GetRawText()),
                JsonNode.Parse(town.GetProperty("classProfile").GetRawText())),
            "The native Town journey does not match its separate four-pack format-5 identity.");
        nativeTownCatalogFingerprint = town.GetProperty("townCatalogFingerprint").GetString();
    }

    private object? CaptureNativeTownPresentation(Main main)
    {
        if (!nativeTownClassProfile)
            return null;
        var geometry = main.ObservedSnapshot!.LevelGeometry.Single(level => level.LevelId == 0);
        const int smith = 63 * 112 + 62;
        Require(geometry.Width == 112 && geometry.Height == 112
            && geometry.NativePieceLayer.Count == 112 * 112
            && geometry.NativeSpecialLayer.Count == 112 * 112
            && geometry.NativeSpecialLayer.Any(value => value != 0)
            && geometry.NativeTransparency.Count == 112 * 112
            && geometry.NativeMegaTileLayer.Count == 0
            && geometry.NativePieceLayer[smith] == 923 && geometry.BlockedCells.Contains(smith)
            && geometry.NativePieceLayer[64 * 112 + 62] == 909 && !geometry.BlockedCells.Contains(64 * 112 + 62)
            && geometry.StaticObjectBlockedCells.Count == 20
            && geometry.StaticObjectBlockedCells.Contains(79 * 112 + 55)
            && geometry.StaticObjectBlockedCells.Contains(15 * 112 + 57),
            "The native Town projection omitted its fine/special channels or changed Smith scenery/resident occupancy.");
        return new {
            Passed = true,
            CatalogFingerprint = nativeTownCatalogFingerprint,
            GeometryHash = Convert.ToHexString(SHA256.HashData(geometry.ToByteArray())).ToLowerInvariant(),
            FineCells = geometry.NativePieceLayer.Count,
            SpecialCells = geometry.NativeSpecialLayer.Count,
            OccupiedCells = geometry.StaticObjectBlockedCells.Count,
        };
    }

    private async Task VerifyNativeTownServicesAsync(Main main, string directory)
    {
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await ApproachDeclaredDestination(main, "towner:0", verifyCancellation: false,
            distanceBoundedApproachDeadline: true);
        var player = main.ObservedSnapshot!.Players.Single();
        Require(Math.Max(Math.Abs(player.PositionX - 62), Math.Abs(player.PositionY - 63)) <= 1
            && (player.PositionX != 62 || player.PositionY != 63),
            "Native Smith approach did not stop beside its solid resident/scenery cell.");
        var sequence = main.ObservedLastQueuedSequence;
        await ClickControl(main, sections, 2, "OpenSmith");
        await AcceptedAndSettled(main, sequence, "Open Smith beside native scenery");
        Require(main.ObservedSnapshot!.ActiveStore is { StoreId: 1 }, "Native Smith stock did not open.");
        var smithSequence = main.ObservedCommandResult!.ClientSequence;
        sequence = main.ObservedLastQueuedSequence;
        await ClickControl(main, sections, 2, "CloseStore");
        await AcceptedAndSettled(main, sequence, "Close native Smith stock");
        var healer = await BuyNativeProgressionHealingAsync(main, targetSupply: 0, maximumPurchases: 0);
        Require(main.ObservedSnapshot!.ActiveStore is null && main.ObservedSnapshot.Players.Single().ActiveStoreId == 0,
            "Native Town services did not leave a settled closed-store checkpoint.");
        WriteEvidence(directory, "native-town-services.json", new {
            Passed = true, SmithSequence = smithSequence, HealerStock = healer.Stock,
            FinalSequence = main.ObservedLastQueuedSequence,
        });
    }
}
