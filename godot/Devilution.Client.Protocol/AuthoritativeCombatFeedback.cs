using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Typed presentation payload derived from an authoritative gameplay event. */
public sealed record AuthoritativeCombatFeedback(
    uint TargetEntityId,
    string Text,
    AuthoritativeCombatFeedbackKind Kind);

public enum AuthoritativeCombatFeedbackKind
{
    Damage,
    Healing,
    Experience,
}

/** Converts server-owned combat events into presentation-only feedback. */
public static class AuthoritativeCombatFeedbackMapper
{
    public static AuthoritativeCombatFeedback? Resolve(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        return gameEvent.EventCase switch {
            GameEvent.EventOneofCase.Damage => new AuthoritativeCombatFeedback(
                gameEvent.Damage.TargetEntityId,
                $"-{FormatAmount(gameEvent.Damage.Amount, gameEvent.Damage.AmountQ6)}",
                AuthoritativeCombatFeedbackKind.Damage),
            GameEvent.EventOneofCase.Healing => new AuthoritativeCombatFeedback(
                gameEvent.Healing.TargetEntityId,
                $"+{FormatAmount(gameEvent.Healing.Amount, gameEvent.Healing.AmountQ6)}",
                AuthoritativeCombatFeedbackKind.Healing),
            GameEvent.EventOneofCase.Experience => new AuthoritativeCombatFeedback(
                gameEvent.Experience.PlayerEntityId,
                $"+{gameEvent.Experience.Amount} XP",
                AuthoritativeCombatFeedbackKind.Experience),
            _ => null,
        };
    }

    private static string FormatAmount(int amount, int amountQ6)
    {
        return amountQ6 == 0
            ? amount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : (amountQ6 / 64.0).ToString(
            "0.##",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
