using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

/** Normal windowed controls and renderer, with an isolated new character and no gameplay shortcuts. */
public partial class SessionAcceptance
{
    private Key captureKeyOne, captureKeyTwo;

    private CancellationToken townCaptureCancellation;

    private async Task RunTownCaptureAsync(string directory)
    {
        var run = new TownCaptureRun(message => GD.Print(message));
        Main main = null!;
        var sequence = new TownCaptureSequence(Environment.GetEnvironmentVariable("GODEVILUTION_TOWN_CAPTURE_SMOKE") == "1");
        Snapshot initial = null!;
        TownCaptureRoute route = null!;
        var clock = Stopwatch.StartNew();
        var visited = new HashSet<(int X, int Y)>();
        var selectedResidents = new List<int>();
        var heldCommands = 0; var walkClicks = 0; var blockedCommands = 0; var rejectedClicks = 0;
        var blockedClicks = 0;
        string Describe() => main?.FrameCaptureState ?? "no admitted client";
        void Status()
        {
            var output = Environment.GetEnvironmentVariable("GODEVILUTION_CLIENT_FRAME_CAPTURE") ?? directory;
            System.IO.Directory.CreateDirectory(output);
            WriteEvidence(output, "town-capture-status.json", new {
                Passed = run.Completed, Partial = !run.Completed, run.Phase, run.Operation, run.Failure,
                run.CompletedPhases, ResidentsSelected = selectedResidents.ToArray(), BlockedClicks = blockedClicks,
                State = Describe(), FrameEvidence = main?.ClientFrameEvidencePath,
            });
        }
        async Task Phase(string name, double seconds, Func<Task> work)
        {
            await run.PhaseAsync(name, seconds, async token => {
                townCaptureCancellation = token;
                await work();
            }, Describe);
            // The recorder has created its fresh directory by the first admission.
            if (main?.ClientFrameEvidencePath is not null) Status();
        }
        try {
            await Phase("admission", 45, async () => {
                Require(DisplayServer.GetName() != "headless" && !OS.HasFeature("headless"),
                    "Town capture requires the normal windowed renderer.");
                Require(Engine.GetMaxFps() == 60 && DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled,
                    "Town capture requires the normal 60 FPS cap and VSync.");
                run.Operation = "loading the normal client";
                main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
                AddChild(main);
                await Frame(); await Frame();
                var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
                ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = "Town-Capture";
                var classes = (OptionButton)setup.FindChild("CharacterClass", true, false);
                classes.Select(classes.GetItemIndex(0));
                run.Operation = "joining through Connect";
                await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
                await Until(() => main.TransportConnected && main.ObservedIdentity is not null && !setup.Visible
                        && main.ObservedSnapshot?.Players.Count == 1,
                    "Town capture could not enter the ordinary new-character session.", 45);
                initial = main.ObservedSnapshot ?? throw new InvalidOperationException("Town admission has no snapshot.");
                route = new TownCaptureRoute(initial);
            });
            clock.Restart();
            await Phase("warmup", sequence.WarmupSeconds + 10, async () => {
                run.Operation = "waiting for world warmup";
                await CaptureUntilTime(clock, sequence.WarmupSeconds);
                await Until(() => main.FrameCaptureActive, "The requested frame capture did not finish world warmup.", 5);
                Require(sequence.Advance(clock.Elapsed.TotalSeconds), "Town capture warmup ended early.");
            });
            await Phase("idle", sequence.IdleSeconds + 5, async () => {
                main.BeginFrameCapturePhase("idle");
                run.Operation = $"standing still for {sequence.IdleSeconds}s";
                var idleStart = main.TownCapturePlayer;
                await CaptureUntilTime(clock, sequence.PhaseStartedAt + sequence.IdleSeconds);
                var idleEnd = main.TownCapturePlayer;
                Require(idleEnd.Level == 0 && idleEnd.X == idleStart.X && idleEnd.Y == idleStart.Y && idleEnd.Tick > idleStart.Tick,
                    "Town capture idle phase did not remain still on an advancing Town host.");
                Require(sequence.Advance(clock.Elapsed.TotalSeconds), "Town capture idle ended early.");
            });
            await Phase("walking", sequence.WalkingSeconds + 15, async () => {
                main.BeginFrameCapturePhase("walking");
                run.Operation = $"walking for {sequence.WalkingSeconds}s, held keys then clicks";
                GetViewport().GuiReleaseFocus();
                var lastQueued = main.ObservedLastQueuedSequence;
                var wasHeld = true;
                while (!sequence.Advance(clock.Elapsed.TotalSeconds)) {
                    var player = main.TownCapturePlayer;
                    Require(player.Level == 0, "The capture route left Town.");
                    visited.Add((player.X, player.Y));
                    if (sequence.UsesHeldKeys(clock.Elapsed.TotalSeconds)) {
                        var next = route.Next(player.X, player.Y, 1);
                        SetCaptureDirection(next.X - player.X, next.Y - player.Y);
                        var queued = main.ObservedLastQueuedSequence;
                        heldCommands += checked((int)(queued - lastQueued)); lastQueued = queued;
                    } else {
                        if (wasHeld) { ReleaseCaptureKeys(); wasHeld = false; }
                        if (player.Admitted && player.Settled) {
                            var next = CaptureVisibleWaypoint(main, route, player.X, player.Y);
                            await ClickWorld(main, next.X, next.Y);
                            Require(main.ObservedLastQueuedSequence > lastQueued && main.ObservedLastQueuedIntent == Command.IntentOneofCase.MoveRequested,
                                "The Town route click did not queue an ordinary walk.");
                            lastQueued = main.ObservedLastQueuedSequence; walkClicks++;
                        }
                    }
                    await CaptureDelay(.05);
                }
                ReleaseCaptureKeys();
                run.Operation = "settling the last walking command";
                await CaptureSettled(main, "walking phase");
                Require(heldCommands > 0 && walkClicks > 0 && visited.Count >= 4, "The walking phase did not exercise both held keys and clicks.");
            });
            await Phase("unreachable-clicks", 45, async () => {
                main.BeginFrameCapturePhase("unreachable-clicks");
                var geometry = initial.LevelGeometry.Single(level => level.LevelId == 0);
                var blocked = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).Distinct().ToArray();
                for (var click = 0; click < TownCaptureSequence.UnreachableClickCount; click++) {
                    run.Operation = $"blocked-cell click {click + 1}/{TownCaptureSequence.UnreachableClickCount}";
                    var player = main.TownCapturePlayer;
                    var candidates = blocked.Where(cell => CaptureTileVisible(main, cell % geometry.Width, cell / geometry.Width)
                            && !geometry.WarpTriggers.Any(warp => warp.TriggerX == cell % geometry.Width && warp.TriggerY == cell / geometry.Width)
                            && !initial.NativeTownResidents.Any(row => Math.Max(Math.Abs(row.PositionX - cell % geometry.Width), Math.Abs(row.PositionY - cell / geometry.Width)) < 3)
                            && !initial.Objects.Any(row => row.LevelId == 0 && row.PositionX == cell % geometry.Width && row.PositionY == cell / geometry.Width))
                        .OrderBy(cell => Math.Abs(cell % geometry.Width - player.X) + Math.Abs(cell / geometry.Width - player.Y)).ToArray();
                    Require(candidates.Length > 0, "No blocked Town cell is visible for the capture.");
                    var target = candidates[click % Math.Min(candidates.Length, 10)];
                    var queued = main.ObservedLastQueuedSequence;
                    await ClickWorld(main, target % geometry.Width, target / geometry.Width);
                    if (main.ObservedLastQueuedSequence > queued) {
                        Require(main.ObservedLastQueuedIntent == Command.IntentOneofCase.MoveRequested, "A blocked-cell click selected an unrelated interaction.");
                        blockedCommands++;
                    }
                    await CaptureSettled(main, "blocked-cell click");
                    if (main.ObservedCommandResult?.Status == CommandStatus.Rejected) rejectedClicks++;
                    sequence.Clicked(); blockedClicks++;
                    await CaptureDelay(.15);
                }
                sequence.Advance(clock.Elapsed.TotalSeconds);
            });
            await Phase("resident-clicks", 120, async () => {
                main.BeginFrameCapturePhase("resident-clicks");
                var remaining = initial.NativeTownResidents.Where(row => row.TownerId != 9).ToList();
                for (var attempt = 1; attempt <= TownCaptureSequence.ResidentClickCount; attempt++) {
                    await CaptureCloseStore(main, run);
                    await CaptureSettled(main, "before resident approach");
                    var position = main.TownCapturePlayer;
                    var resident = remaining.MinBy(row => route.ResidentDistance(position.X, position.Y, row.PositionX, row.PositionY));
                    Require(resident is not null, "The capture requires three reachable Town residents.");
                    var distance = route.ResidentDistance(position.X, position.Y, resident!.PositionX, resident.PositionY);
                    Require(distance != int.MaxValue, $"No safe Town route reaches resident {resident.TownerId} from ({position.X},{position.Y}).");
                    remaining.Remove(resident);
                    run.Operation = $"resident {attempt}/3 id={resident.TownerId} target=({resident.PositionX},{resident.PositionY}), approach";
                    GD.Print($"Town capture: {run.Operation}; path={distance} tiles; {Describe()}");
                    var approach = Stopwatch.StartNew();
                    var approachLimit = Math.Clamp(distance * .5 + 10, 15, 75);
                    while (true) {
                        var player = main.TownCapturePlayer;
                        if (Math.Max(Math.Abs(player.X - resident.PositionX), Math.Abs(player.Y - resident.PositionY)) <= 1) break;
                        Require(approach.Elapsed.TotalSeconds < approachLimit,
                            $"Resident {resident.TownerId} approach exceeded {approachLimit:0.#}s; {Describe()}.");
                        var next = CaptureResidentWaypoint(main, route, player.X, player.Y, resident.PositionX, resident.PositionY);
                        GD.Print($"Town capture: resident {resident.TownerId} walking to ({next.X},{next.Y}); {Describe()}");
                        var queued = main.ObservedLastQueuedSequence;
                        // Ctrl-walk cannot accidentally select a sprite covering the approach tile.
                        await Click(main.ObservedTileScreenPosition(next.X, next.Y), walk: true);
                        Require(main.ObservedLastQueuedSequence > queued && main.ObservedLastQueuedIntent == Command.IntentOneofCase.MoveRequested,
                            $"Resident {resident.TownerId} approach did not queue a real walk.");
                        await CaptureSettled(main, $"resident {resident.TownerId} viewing walk");
                        var arrived = main.TownCapturePlayer;
                        Require((arrived.X, arrived.Y) == next,
                            $"Resident {resident.TownerId} approach did not arrive at ({next.X},{next.Y}); {Describe()}.");
                    }
                    run.Operation = $"resident {attempt}/3 id={resident.TownerId}, hover and select";
                    GD.Print($"Town capture: {run.Operation}; {Describe()}");
                    var point = CaptureResidentPoint(main, resident.PositionX, resident.PositionY);
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
                    await CaptureDelay(.25);
                    var previous = main.ObservedLastQueuedSequence;
                    await Click(point);
                    Require(main.ObservedLastQueuedSequence > previous && main.ObservedLastQueuedIntent is
                        Command.IntentOneofCase.TalkToTownerRequested or Command.IntentOneofCase.TurnInQuestRequested,
                        $"The resident {resident.TownerId} click did not select a resident interaction.");
                    await CaptureSettled(main, $"resident {resident.TownerId} selection");
                    Require(main.ObservedCommandResult is { } receipt && AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt),
                        $"Resident {resident.TownerId} interaction was not accepted; {Describe()}.");
                    await CaptureCloseStore(main, run);
                    await PressAcceptanceKey(Key.Escape);
                    sequence.Clicked(); selectedResidents.Add(resident.TownerId);
                    GD.Print($"Town capture: resident {attempt}/3 id={resident.TownerId} completed; {Describe()}");
                    Status();
                }
                Require(sequence.Advance(clock.Elapsed.TotalSeconds) && sequence.Phase == TownCapturePhase.Complete,
                    "Town capture did not finish all four phases.");
            });
            await Phase("finalizing", 5, () => {
                run.Operation = "publishing completed evidence (no renderer readback)";
                run.Complete(() => {
                    var final = main.ObservedSnapshot!; var character = final.Players.Single(); var identity = main.ObservedIdentity!;
                    WriteEvidence(directory, "towncapture.json", new Evidence(true, identity.GameMode.ToString(),
                        identity.ContentManifestHash, identity.RulesetIdentityHash, character.EntityId, character.CharacterClassId,
                        character.LevelId, character.PositionX, character.PositionY, main.ObservedLastQueuedSequence, true));
                    WriteEvidence(directory, "town-capture-route.json", new {
                        Smoke = Environment.GetEnvironmentVariable("GODEVILUTION_TOWN_CAPTURE_SMOKE") == "1", HeldCommands = heldCommands,
                        WalkingClicks = walkClicks, VisitedTiles = visited.Count, MinX = visited.Min(cell => cell.X), MaxX = visited.Max(cell => cell.X),
                        MinY = visited.Min(cell => cell.Y), MaxY = visited.Max(cell => cell.Y), BlockedClicks = blockedClicks,
                        BlockedCommands = blockedCommands, RejectedClicks = rejectedClicks, ResidentIds = selectedResidents.ToArray(),
                        DriverPollSeconds = .05, FrameEvidence = main.ClientFrameEvidencePath,
                    });
                    Require(main.StopPerformanceRecording(true), "The frame recorder did not finish cleanly.");
                });
                return Task.CompletedTask;
            });
            GD.Print("Town capture: all phases and completion evidence finished; exiting.");
        } catch (Exception exception) {
            run.Fail(exception);
            try { main?.StopPerformanceRecording(false); }
            finally { Status(); }
            throw;
        } finally {
            ReleaseCaptureKeys();
            // Cancellation can interrupt a real click between its press and release frames.
            var cursor = GetViewport().GetMousePosition();
            foreach (var button in new[] { MouseButton.Left, MouseButton.Right })
                Input.ParseInputEvent(new InputEventMouseButton {
                    ButtonIndex = button, Position = cursor, GlobalPosition = cursor, Pressed = false,
                });
            Input.FlushBufferedEvents();
            townCaptureCancellation = default;
        }
    }

    private async Task CaptureCloseStore(Main main, TownCaptureRun run)
    {
        if (main.ObservedSnapshot?.ActiveStore is null) return;
        run.Operation = "closing resident store/dialogue";
        GD.Print($"Town capture: {run.Operation}; {main.FrameCaptureState}");
        await ClickControl(main, (TabContainer)main.FindChild("HudSections", true, false), 2, "CloseStore");
        await CaptureSettled(main, "close resident dialogue");
        Require(main.ObservedSnapshot?.ActiveStore is null, "The resident store did not close.");
    }

    private async Task CaptureFrameSignal() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task CaptureTimerSignal(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private Task CaptureDelay(double seconds) => CaptureTimerSignal(seconds).WaitAsync(townCaptureCancellation);

    private async Task CaptureUntilTime(Stopwatch clock, double deadline)
    {
        while (clock.Elapsed.TotalSeconds < deadline)
            await CaptureDelay(Math.Max(.01, deadline - clock.Elapsed.TotalSeconds));
    }

    private async Task CaptureSettled(Main main, string context)
    {
        var deadline = Stopwatch.StartNew();
        while (true) {
            var player = main.TownCapturePlayer;
            Require(player.Level == 0, $"Town capture left Town during {context}.");
            if (player.Admitted && player.Settled) return;
            Require(deadline.Elapsed.TotalSeconds < 10, $"Town capture {context} did not settle: queued={main.ObservedLastQueuedSequence}, "
                + $"receipt={main.ObservedCommandResult}, tick={player.Tick}, pending={main.ObservedPendingCommandCount}.");
            await CaptureDelay(.05);
        }
    }

    private bool CaptureTileVisible(Main main, int x, int y)
        => ((Control)main.FindChild("WorldArea", true, false)).GetGlobalRect().Grow(-16).HasPoint(main.ObservedTileScreenPosition(x, y));

    private (int X, int Y) CaptureVisibleWaypoint(Main main, TownCaptureRoute route, int x, int y)
    {
        for (var steps = 6; steps > 0; steps--) {
            var next = route.Next(x, y, steps);
            if (CaptureTileVisible(main, next.X, next.Y)) return next;
        }
        throw new InvalidOperationException("No Town route waypoint is visible.");
    }

    private (int X, int Y) CaptureResidentWaypoint(Main main, TownCaptureRoute route, int x, int y, int targetX, int targetY)
    {
        for (var steps = 5; steps > 0; steps--) {
            var next = route.TowardResident(x, y, targetX, targetY, steps);
            if (CaptureTileVisible(main, next.X, next.Y)) return next;
        }
        throw new InvalidOperationException($"No visible Town approach reaches resident ({targetX},{targetY}) from ({x},{y}).");
    }

    private Vector2 CaptureResidentPoint(Main main, int x, int y)
    {
        var anchor = main.ObservedTileScreenPosition(x, y);
        for (var dy = -16; dy >= -80; dy -= 4)
            for (var dx = -16; dx <= 16; dx += 4) {
                var point = anchor + new Vector2(dx, dy);
                if (main.FrameCaptureResidentAt(point) == new Vector2I(x, y)) return point;
            }
        throw new InvalidOperationException($"The native resident at ({x},{y}) has no visible picking pixel.");
    }

    private void SetCaptureDirection(int x, int y)
    {
        var (one, two) = (Math.Sign(x), Math.Sign(y)) switch {
            (1, 0) => (Key.S, Key.D), (-1, 0) => (Key.W, Key.A),
            (0, 1) => (Key.S, Key.A), (0, -1) => (Key.W, Key.D),
            (1, 1) => (Key.S, Key.None), (-1, -1) => (Key.W, Key.None),
            (-1, 1) => (Key.A, Key.None), (1, -1) => (Key.D, Key.None), _ => (Key.None, Key.None),
        };
        if (one == captureKeyOne && two == captureKeyTwo) return;
        ReleaseCaptureKeys();
        captureKeyOne = one; captureKeyTwo = two;
        CaptureKey(one, true); CaptureKey(two, true);
        Input.FlushBufferedEvents();
    }

    private static void CaptureKey(Key key, bool pressed)
    {
        if (key != Key.None) Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
    }

    private void ReleaseCaptureKeys()
    {
        CaptureKey(captureKeyOne, false); CaptureKey(captureKeyTwo, false);
        captureKeyOne = captureKeyTwo = Key.None;
        Input.FlushBufferedEvents();
    }
}
