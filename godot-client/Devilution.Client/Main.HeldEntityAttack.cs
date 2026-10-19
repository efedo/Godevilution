using System.Linq;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Godot input owner for a negotiated native-shaped held entity attack. */
public partial class Main
{
    private readonly HeldEntityAttackController heldEntityAttack = new();
    private AuthoritativeClient? heldEntityAttackOwnerClient;
    private long heldEntityAttackOwnerGeneration;
    private long heldEntityAttackOwnerAttemptEpoch;
    private Window? heldEntityAttackFocusWindow;
    private bool heldEntityAttackExitHooked;

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton {
            ButtonIndex: MouseButton.Right,
            Pressed: false,
        })
            heldEntityAttack.Release();
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            heldEntityAttack.Release();
    }

    private void EnsureHeldEntityAttackFocusMonitor()
    {
        var window = GetWindow();
        if (!heldEntityAttackExitHooked) {
            TreeExiting += StopHeldEntityAttackFocusMonitor;
            heldEntityAttackExitHooked = true;
        }
        if (ReferenceEquals(heldEntityAttackFocusWindow, window))
            return;

        if (heldEntityAttackFocusWindow is not null)
            heldEntityAttackFocusWindow.FocusExited -= OnHeldEntityAttackFocusExited;
        heldEntityAttackFocusWindow = window;
        heldEntityAttackFocusWindow.FocusExited += OnHeldEntityAttackFocusExited;
    }

    private void StopHeldEntityAttackFocusMonitor()
    {
        if (heldEntityAttackFocusWindow is not null)
            heldEntityAttackFocusWindow.FocusExited -= OnHeldEntityAttackFocusExited;
        heldEntityAttackFocusWindow = null;
        if (heldEntityAttackExitHooked) {
            TreeExiting -= StopHeldEntityAttackFocusMonitor;
            heldEntityAttackExitHooked = false;
        }
        heldEntityAttack.Reset();
        heldEntityAttackOwnerClient = null;
        heldEntityAttackOwnerAttemptEpoch = 0;
    }

    private void OnHeldEntityAttackFocusExited() => heldEntityAttack.Release();

    private void DriveHeldEntityAttack()
    {
        var activeClient = client;
        var snapshot = model.Snapshot;
        if (activeClient?.IsConnected != true || snapshot is null) {
            heldEntityAttack.Reset();
            return;
        }

        var player = snapshot.Players.FirstOrDefault();
        var viewport = GetViewport();
        if (player is null || player.Life <= 0 || player.ActiveStoreId != 0 || player.StashOpen
            || IsHeldEntityAttackGuiCaptured(viewport)) {
            heldEntityAttack.Release();
            return;
        }

        var mousePosition = viewport.GetMousePosition();
        var targetEntityId = worldFrame.GetGlobalRect().HasPoint(mousePosition)
            ? world?.CombatTargetEntityIdAt(world.ScreenToTile(mousePosition))
            : null;
        var observation = ObserveHeldEntityAttackState(activeClient, snapshot);
        if (!observation.RightButtonHeld)
            heldEntityAttack.Release();

        if (!heldEntityAttack.TryPrepareFollowUp(observation, targetEntityId, out var followUp))
            return;

        var sequence = Queue(new Command {
            AttackRequested = new AttackRequested { TargetEntityId = followUp.TargetEntityId },
        }, automaticInput: true);
        heldEntityAttack.RegisterQueued(followUp, sequence);
    }

    private HeldEntityAttackObservation ObserveHeldEntityAttackState(
        AuthoritativeClient activeClient,
        Snapshot snapshot)
    {
        var player = snapshot.Players.FirstOrDefault();
        var action = player?.CurrentAction;
        var attemptEpoch = activeClient.InitialConnectionObservation?.AttemptEpoch ?? 0;
        return new HeldEntityAttackObservation(
            GetHeldEntityAttackOwnerGeneration(activeClient, attemptEpoch),
            attemptEpoch,
            activeClient.IsConnected,
            activeClient.SupportsQueuedRangedAttacks,
            Input.IsMouseButtonPressed(MouseButton.Right),
            activeClient.PendingCommandCount,
            player?.EntityId ?? 0,
            player?.Life ?? 0,
            player?.LevelId ?? 0,
            snapshot.Tick,
            player?.CombatMode ?? PlayerCombatMode.Stand,
            action is not null,
            action?.Kind ?? PlayerActionKind.Unspecified,
            action?.EffectApplied ?? false,
            action?.StartedAtTick ?? 0,
            action?.ReadyTick ?? 0,
            action?.TargetEntityId ?? 0);
    }

    private long GetHeldEntityAttackOwnerGeneration(AuthoritativeClient activeClient, long attemptEpoch)
    {
        if (!ReferenceEquals(heldEntityAttackOwnerClient, activeClient)
            || heldEntityAttackOwnerAttemptEpoch != attemptEpoch) {
            heldEntityAttackOwnerClient = activeClient;
            heldEntityAttackOwnerAttemptEpoch = attemptEpoch;
            heldEntityAttackOwnerGeneration = checked(heldEntityAttackOwnerGeneration + 1);
            heldEntityAttack.Reset();
        }
        return heldEntityAttackOwnerGeneration;
    }

    private bool IsHeldEntityAttackGuiCaptured(Viewport viewport)
    {
        var hovered = viewport.GuiGetHoveredControl();
        var pointerCaptured = hovered is not null
            && hovered.MouseFilter == Control.MouseFilterEnum.Stop
            && !ReferenceEquals(hovered, worldFrame)
            && !worldFrame.IsAncestorOf(hovered);
        var textEntryFocused = viewport.GuiGetFocusOwner() is LineEdit or TextEdit;
        return pointerCaptured || textEntryFocused;
    }
}
