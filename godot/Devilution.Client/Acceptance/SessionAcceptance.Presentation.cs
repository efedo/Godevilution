using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private async Task VerifyResponsiveLayout(Main main, string directory, bool capture)
    {
        var sizes = new[] { new Vector2I(960, 600), new Vector2I(1028, 720), new Vector2I(1280, 720) };
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var panel = (Control)main.FindChild("HudPanel", true, false);
        var header = (Control)main.FindChild("StatusPanel", true, false);
        var world = (Control)main.FindChild("WorldArea", true, false);
        var observations = new List<object>();
        foreach (var size in sizes) {
            GetWindow().Size = size;
            for (var frame = 0; frame < 5; frame++)
                await Frame();
            var viewport = GetViewport().GetVisibleRect();
            foreach (var control in new[] { panel, header, world })
                Require(viewport.Encloses(control.GetGlobalRect().Grow(-0.5f)),
                    $"{control.Name} {control.GetGlobalRect()} is clipped by {viewport}.");
            Require(!world.GetGlobalRect().Intersects(panel.GetGlobalRect())
                && !world.GetGlobalRect().Intersects(header.GetGlobalRect()), "HUD obscures the world input area.");
            Require(world.Size.X >= 400 && world.Size.Y >= 200, "The usable world area became too small.");
            Require(main.ObservedPlayerScreenPosition is { } position && world.GetGlobalRect().HasPoint(position),
                "The camera did not retain the player in the exposed world area after resize.");
            for (var index = 0; index < sections.GetTabCount(); index++) {
                await SelectTab(sections, index);
                var page = (ScrollContainer)sections.GetChild(index);
                Require(!page.GetHScrollBar().Visible, $"{page.Name} requires horizontal scrolling.");
                var content = (Control)page.GetChild(0);
                Require(content.GetGlobalRect().End.X <= page.GetGlobalRect().End.X + 1,
                    $"{page.Name} content extends beyond its visible width.");
                if (capture && size.X == 960)
                    await Capture(System.IO.Path.Combine(directory, $"tab-{page.Name.ToString().ToLowerInvariant()}.png"));
            }
            // World tab is last, so route buttons must be visible without scrolling.
            foreach (var name in new[] { "Routes", "Travel", "CancelTravel" }) {
                var control = (Control)main.FindChild(name, true, false);
                Require(viewport.Encloses(control.GetGlobalRect()), $"{name} is outside the viewport.");
            }
            observations.Add(new { Width = viewport.Size.X, Height = viewport.Size.Y,
                WorldWidth = world.Size.X, WorldHeight = world.Size.Y });
            if (capture)
                await Capture(System.IO.Path.Combine(directory, $"layout-{size.X}x{size.Y}.png"));
        }
        GetWindow().Size = new Vector2I(1028, 720);
        await Frame();
        await Frame();
        WriteEvidence(directory, $"{System.Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE")!.ToLowerInvariant()}-layout.json",
            new { Passed = true, Sizes = observations });
    }

    private async Task VerifyRouteJourney(Main main, string directory, bool visible)
    {
        // Exercise global mouse -> reserved world-area coordinates after resizing.
        // At this isolated town spawn, the adjacent right-hand cell is empty.
        var beforeClick = main.ObservedSnapshot!.Players.Single().Clone();
        var clickSequence = main.ObservedCommandResult!.ClientSequence;
        await Click(main.ObservedPlayerScreenPosition!.Value + new Vector2(32, 0), walk: true);
        await Until(() => main.ObservedCommandResult is { Status: CommandStatus.Accepted } clickResult
            && clickResult.ClientSequence > clickSequence
            && main.ObservedSnapshot!.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null } clicked
            && clicked.PositionX == beforeClick.PositionX + 1 && clicked.PositionY == beforeClick.PositionY,
            "World click did not reach the adjacent authoritative tile after resizing.");
        var initial = main.ObservedSnapshot!;
        var sourceLevel = initial.Players.Single().LevelId;
        var route = initial.LevelGeometry.Single(geometry => geometry.LevelId == sourceLevel)
            .WarpTriggers.First(trigger => trigger.Available).Clone();
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await SelectTab(sections, sections.GetTabCount() - 1);
        var travel = (Button)main.FindChild("Travel", true, false);
        var cancel = (Button)main.FindChild("CancelTravel", true, false);
        var sequence = main.ObservedCommandResult!.ClientSequence;
        await Click(travel.GetGlobalRect().GetCenter());
        await Until(() => main.ObservedCommandResult is { Status: CommandStatus.Accepted } result
            && result.ClientSequence > sequence, "Travel did not submit an accepted movement through the real button.");
        await Click(cancel.GetGlobalRect().GetCenter());
        await Until(() => main.ObservedPendingCommandCount == 0
            && main.ObservedSnapshot!.Players.Single() is { CurrentAction: null, CombatMode: PlayerCombatMode.Stand },
            "The in-flight movement did not settle after cancel.");
        var cancelled = main.ObservedSnapshot!.Players.Single().Clone();
        var cancelledSequence = main.ObservedCommandResult!.ClientSequence;
        var tick = main.ObservedSnapshot!.Tick;
        await Until(() => main.ObservedSnapshot!.Tick >= tick + 8, "No snapshots arrived after cancellation.");
        Require(main.ObservedCommandResult!.ClientSequence == cancelledSequence
            && main.ObservedPendingCommandCount == 0
            && main.ObservedSnapshot!.Players.Single().PositionX == cancelled.PositionX
            && main.ObservedSnapshot.Players.Single().PositionY == cancelled.PositionY,
            "Cancelled travel continued submitting input or moving the player.");
        await Click(travel.GetGlobalRect().GetCenter());
        await Until(() => !cancel.Disabled, "Travel did not restart through the real button after cancellation.");
        await Until(() => {
            if (main.ObservedSnapshot?.Players.Single().LevelId == route.TargetLevelId)
                return true;
            Require(!cancel.Disabled, $"Travel stopped before the transition: {((Label)main.FindChild("TravelStatus", true, false)).Text}; result={main.ObservedCommandResult}");
            return false;
        },
            "Route travel did not reach the server-projected target level.");
        Require(main.ObservedCommandResult is { Status: CommandStatus.Accepted }, "Route ended without an accepted step.");
        Require(main.ObservedSnapshot!.LevelGeometry.Any(geometry => geometry.LevelId == route.TargetLevelId),
            "The target level geometry did not reach the presentation client.");
        await Frame();
        await Frame();
        Require(cancel.Disabled, "Level transition did not cancel the source route repeater.");
        if (visible)
            await Capture(System.IO.Path.Combine(directory, "journey-dungeon.png"));
        WriteEvidence(directory, "journey-route.json", new { Passed = true, route.WarpId,
            SourceLevel = sourceLevel, TargetLevel = route.TargetLevelId, CancelledSequence = cancelledSequence,
            FinalSequence = main.ObservedCommandResult!.ClientSequence });
    }

    private async Task SelectTab(TabContainer sections, int index)
    {
        var bar = sections.GetTabBar();
        await Click(bar.GetGlobalRect().Position + bar.GetTabRect(index).GetCenter());
        await Frame();
        Require(sections.CurrentTab == index, $"The real tab control did not select {index}.");
    }

    private async Task Click(Vector2 position, bool walk = false)
    {
        Require(GetViewport().GetVisibleRect().HasPoint(position), "Acceptance tried to click outside the viewport.");
        Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
        await Frame();
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = MouseButton.Left, Position = position, GlobalPosition = position, Pressed = true, CtrlPressed = walk,
        });
        await Frame();
        Input.ParseInputEvent(new InputEventMouseButton {
            ButtonIndex = MouseButton.Left, Position = position, GlobalPosition = position, Pressed = false, CtrlPressed = walk,
        });
        await Frame();
    }
}
