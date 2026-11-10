using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;

namespace Devilution.Client;

/**
 * Game speed (1x-20x): + and - step through GameSpeedLimits.Steps. The server paces its
 * authoritative clock at the chosen multiple, so only wall-clock pacing changes. The choice is
 * saved for later sessions; GODEVILUTION_GAME_SPEED overrides it for headless runs.
 */
public partial class Main
{
    private uint desiredGameSpeed = GameSpeedLimits.Minimum;
    private int reportedGameSpeedRevision;

    internal uint ObservedGameSpeed => client?.GameSpeedMultiplier ?? GameSpeedLimits.Minimum;

    private void LoadGameSpeed()
    {
        if (uint.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_GAME_SPEED"), out var configured)
            && GameSpeedLimits.IsValid(configured)) {
            desiredGameSpeed = configured;
            return;
        }
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE")))
            return;
        var settings = new ConfigFile();
        if (settings.Load(SettingsPath) == Error.Ok)
            desiredGameSpeed = GameSpeedLimits.NearestStep((uint)Math.Clamp(
                settings.GetValue("gameplay", "game_speed", 1).AsInt32(), 1, (int)GameSpeedLimits.Maximum));
    }

    /** A new client re-requests the saved speed once the server offers control. */
    private void ApplyDesiredGameSpeed(AuthoritativeClient target)
    {
        reportedGameSpeedRevision = 0;
        if (desiredGameSpeed != GameSpeedLimits.Minimum)
            target.RequestGameSpeed(desiredGameSpeed);
    }

    private void AdjustGameSpeed(int direction)
    {
        if (client is { } active && active.IsConnected && !active.CanControlGameSpeed) {
            hud?.SetStatus($"Game speed {active.GameSpeedMultiplier}x is set by the host");
            return;
        }
        desiredGameSpeed = GameSpeedLimits.Step(desiredGameSpeed, direction);
        client?.RequestGameSpeed(desiredGameSpeed);
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GODEVILUTION_ACCEPTANCE_PHASE"))) {
            var settings = new ConfigFile();
            settings.Load(SettingsPath);
            settings.SetValue("gameplay", "game_speed", (int)desiredGameSpeed);
            settings.Save(SettingsPath);
        }
        hud?.SetStatus($"Game speed {desiredGameSpeed}x requested (+ faster, - slower)");
    }

    /** Reports each acknowledgement once on the status line. */
    private void ReportGameSpeed()
    {
        if (client is not { } active || active.GameSpeedRevision == reportedGameSpeedRevision)
            return;
        reportedGameSpeedRevision = active.GameSpeedRevision;
        hud?.SetStatus(active.LastGameSpeedRejectReason switch {
            GameSpeedRejectReason.Unspecified => $"Game speed {active.GameSpeedMultiplier}x",
            GameSpeedRejectReason.HostControlled => $"Game speed {active.GameSpeedMultiplier}x is set by the host",
            _ => $"Game speed unavailable; running at {active.GameSpeedMultiplier}x",
        });
    }

    private static bool IsGameSpeedKey(Key key, out int direction)
    {
        direction = key switch {
            Key.Equal or Key.KpAdd => 1,
            Key.Minus or Key.KpSubtract => -1,
            _ => 0,
        };
        return direction != 0;
    }
}
