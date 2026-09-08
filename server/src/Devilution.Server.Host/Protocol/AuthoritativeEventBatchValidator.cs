using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Server.Protocol;

/** Validates adapter-produced event batches before they cross the wire. */
public static class AuthoritativeEventBatchValidator
{
    public const int MaximumEventBatchCount = AuthoritativeProtocolLimits.MaximumEventBatchCount;
    public const int MaximumDialogueTextBytes = AuthoritativeProtocolLimits.MaximumDialogueTextBytes;

    public static void Validate(EventBatch events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Events.Count > MaximumEventBatchCount)
            throw new InvalidDataException("The authoritative event batch is too large.");

        foreach (var gameEvent in events.Events) {
            if (gameEvent is null)
                throw new InvalidDataException("The authoritative event batch contains a null event.");

            switch (gameEvent.EventCase) {
            case GameEvent.EventOneofCase.Damage:
                if (gameEvent.Damage.Amount < 0
                    || gameEvent.Damage.AmountQ6 < 0
                    || gameEvent.Damage.AmountQ6 == 0
                        && gameEvent.Damage.Amount > int.MaxValue / 64)
                    throw new InvalidDataException("The authoritative event batch contains invalid damage.");
                break;
            case GameEvent.EventOneofCase.Experience:
                break;
            case GameEvent.EventOneofCase.Healing:
                if (gameEvent.Healing.Amount < 0 || gameEvent.Healing.AmountQ6 < 0)
                    throw new InvalidDataException("The authoritative event batch contains invalid healing.");
                break;
            case GameEvent.EventOneofCase.Dialogue:
                if (gameEvent.Dialogue.MessageId < 0
                    || !AuthoritativeProtocolLimits.IsDialogueTextValid(gameEvent.Dialogue.Text))
                    throw new InvalidDataException("The authoritative event batch contains invalid dialogue.");
                break;
            case GameEvent.EventOneofCase.Victory:
                if (gameEvent.Victory.DiabloKillLevel > byte.MaxValue)
                    throw new InvalidDataException(
                        "The authoritative event batch contains an out-of-range Diablo kill level.");
                break;
            case GameEvent.EventOneofCase.MapOpened:
                break;
            default:
                throw new InvalidDataException("The authoritative event batch contains an unknown event kind.");
            }
        }
    }
}
