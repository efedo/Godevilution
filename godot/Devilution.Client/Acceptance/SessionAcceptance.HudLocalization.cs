using Godot;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Checks already-created UI controls; this observation never loads pack resources. */
    private void VerifyHudLocalization(Main main, string directory, string phase, string profile)
    {
        var sections = main.FindChild("HudSections", true, false) as TabContainer
            ?? throw new InvalidOperationException("HUD sections are missing.");
        var names = new[] { "Hero", "Items", "Trade", "Stash", "World" };
        var pages = sections.GetChildren().OfType<ScrollContainer>().ToArray();
        Require(sections.GetTabCount() == names.Length && pages.Length == names.Length,
            "The localized HUD changed the number of sections.");
        var titles = new string[names.Length];
        for (var index = 0; index < names.Length; index++) {
            Require(pages[index].Name.ToString() == names[index],
                "Localization changed a stable HUD control identity.");
            titles[index] = sections.GetTabTitle(index);
            var expected = profile == "semantic" && (index is 0 or 4)
                ? index == 0 ? "Character" : "Travel"
                : names[index];
            Require(titles[index] == expected,
                "The actual HUD title did not use its selected localization or default fallback.");
        }
        var routeCaption = main.FindChild("RouteCaption", true, false) as Label
            ?? throw new InvalidOperationException("HUD route caption is missing.");
        var destinationCaption = main.FindChild("DestinationCaption", true, false) as Label
            ?? throw new InvalidOperationException("HUD destination caption is missing.");
        var travelButton = main.FindChild("Travel", true, false) as Button
            ?? throw new InvalidOperationException("HUD travel button is missing.");
        var cancelButton = main.FindChild("CancelTravel", true, false) as Button
            ?? throw new InvalidOperationException("HUD cancel button is missing.");
        var worldHelp = main.FindChild("WorldHelpCaption", true, false) as Label
            ?? throw new InvalidOperationException("HUD world guidance is missing.");
        Require(routeCaption.Text == (profile == "semantic" ? "Path choices" : "Routes supplied by the server"),
            "The actual route caption did not use its selected localization or default fallback.");
        Require(travelButton.Text == (profile == "semantic" ? "Travel now" : "Travel to route")
            && travelButton.TooltipText == travelButton.Text,
            "The actual travel caption/tooltip did not use its selected localization or default fallback.");
        Require(destinationCaption.Text == "Declared towners and portal endpoints"
            && cancelButton.Text == "Cancel travel" && cancelButton.TooltipText == cancelButton.Text,
            "Missing localization entries changed the default HUD captions.");
        Require(worldHelp.Text.Contains("Escape or " + titles[0] + " / Restart in Town requests server recovery.",
                StringComparison.Ordinal),
            "Default world guidance does not identify the displayed character section.");
        WriteEvidence(directory, phase.ToLowerInvariant() + "-hud-localization.json", new {
            Passed = true, Profile = profile,
            ControlNames = pages.Select(page => page.Name.ToString()).ToArray(), TabTitles = titles,
            RouteCaption = routeCaption.Text, DestinationCaption = destinationCaption.Text,
            TravelCaption = travelButton.Text, TravelTooltip = travelButton.TooltipText,
            CancelCaption = cancelButton.Text, CancelTooltip = cancelButton.TooltipText,
            WorldGuidance = worldHelp.Text,
        });
    }
}
