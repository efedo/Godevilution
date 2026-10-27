using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;

namespace Devilution.Client;

/** Held walking (left mouse button or WASD) and the persisted native brightness option. */
public partial class Main
{
    private const string SettingsPath = "user://settings.cfg";
    private const double HeldWalkInterval = 0.1;
    private bool heldMouseWalk;
    private Vector2I? lastHeldWalkTarget;
    private double heldWalkCooldown;

    /** The screen-relative step for the WASD keys currently held, including the one just pressed. */
    private static Vector2I HeldScreenWalkDirection(Key pressed = Key.None)
    {
        bool Held(Key key) => key == pressed || Input.IsKeyPressed(key);
        var (x, y) = ScreenDirectionMovement.Resolve(Held(Key.W), Held(Key.S), Held(Key.A), Held(Key.D));
        return new Vector2I(x, y);
    }

    private void BeginHeldMouseWalk(Vector2I target)
    {
        heldMouseWalk = true;
        lastHeldWalkTarget = target;
        heldWalkCooldown = HeldWalkInterval;
    }

    /**
     * Keeps walking while the walk button or a WASD key stays held, like DevilutionX's
     * repeated click-walk. A standing player takes one key step, so walls still answer;
     * a walking player's native path is retargeted from its next tile without a pause.
     */
    private void UpdateHeldWalk(double delta)
    {
        heldWalkCooldown = Math.Max(0, heldWalkCooldown - delta);
        if (!Input.IsMouseButtonPressed(MouseButton.Left))
            heldMouseWalk = false;
        if (client?.IsConnected != true || client.PendingCommandCount > 0 || heldWalkCooldown > 0
            || model.Snapshot?.Players.FirstOrDefault() is not { Life: > 0, ActiveStoreId: 0 } player
            || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
            return;
        var settled = AuthoritativePlayerSettlement.IsSettled(player, 0);
        var direction = HeldScreenWalkDirection();
        if (direction != Vector2I.Zero) {
            heldWalkCooldown = HeldWalkInterval;
            if (settled) {
                Queue(new Command { MoveRequested = new MoveRequested { DirectionX = direction.X, DirectionY = direction.Y } }, direction);
                return;
            }
            var ahead = new Vector2I(player.PositionX + 2 * direction.X, player.PositionY + 2 * direction.Y);
            if (ahead != lastHeldWalkTarget) {
                lastHeldWalkTarget = ahead;
                QueueMoveTo(ahead);
            }
            return;
        }
        if (!heldMouseWalk || world is null)
            return;
        var cursor = GetViewport().GetMousePosition();
        if (!worldFrame.GetGlobalRect().HasPoint(cursor))
            return;
        var target = world.ScreenToTile(cursor);
        if (target == lastHeldWalkTarget || (target.X == player.PositionX && target.Y == player.PositionY))
            return;
        lastHeldWalkTarget = target;
        heldWalkCooldown = HeldWalkInterval;
        QueueMoveTo(target);
    }

    /** Restores the saved brightness; acceptance runs keep the native default. */
    private void LoadBrightness()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE")))
            return;
        var settings = new ConfigFile();
        if (settings.Load(SettingsPath) == Error.Ok)
            world?.SetBrightness(settings.GetValue("graphics", "brightness", 0).AsInt32());
    }

    /** G brightens and Shift+G darkens by one native step; the value is saved for later sessions. */
    private void AdjustBrightness(int direction)
    {
        if (world is null)
            return;
        world.SetBrightness(NativeBrightness.Adjust(world.Brightness, direction));
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE"))) {
            var settings = new ConfigFile();
            settings.Load(SettingsPath);
            settings.SetValue("graphics", "brightness", world.Brightness);
            settings.Save(SettingsPath);
        }
        hud?.SetStatus($"Brightness {world.Brightness} (G brighter, Shift+G darker)");
    }
}
