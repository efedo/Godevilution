using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/**
 * Negotiated game speed. `ServerTickRateHz` is the paced rate (base times multiplier), so the
 * command lead, clock projection, presentation clock and poll cadence all follow the speed.
 * The desired speed survives reconnects and is re-requested by `PollAsync`.
 */
public sealed partial class AuthoritativeClient
{
    private readonly object gameSpeedLock = new();
    private uint gameSpeedMultiplier = GameSpeedLimits.Minimum;
    private uint? desiredGameSpeedMultiplier;
    private ulong? pendingGameSpeedRequestId;
    private ulong nextGameSpeedRequestId;
    private bool supportsGameSpeed;
    private bool gameSpeedControllable;

    /** The negotiated ServerHello rate, unchanged by game speed. */
    public uint BaseTickRateHz { get; private set; } = AuthoritativePresentationClock.DefaultTickRateHz;

    /** Authoritative ticks per wall-clock second at the current game speed. */
    public uint ServerTickRateHz => checked(BaseTickRateHz * GameSpeedMultiplier);

    public uint GameSpeedMultiplier
    {
        get {
            lock (gameSpeedLock)
                return gameSpeedMultiplier;
        }
    }

    /** True when the server negotiated game speed and this session may change it. */
    public bool CanControlGameSpeed
    {
        get {
            lock (gameSpeedLock)
                return supportsGameSpeed && gameSpeedControllable;
        }
    }

    /** The reason of the last rejected request; Unspecified after an accepted one. */
    public GameSpeedRejectReason LastGameSpeedRejectReason { get; private set; }

    /** Increments on every acknowledgement so the presentation can report the outcome. */
    public int GameSpeedRevision { get; private set; }

    /** Records the speed to request; it is sent on the next poll when this session controls speed. */
    public void RequestGameSpeed(uint multiplier)
    {
        if (!GameSpeedLimits.IsValid(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        lock (gameSpeedLock)
            desiredGameSpeedMultiplier = multiplier;
    }

    private void ResetGameSpeedNegotiation()
    {
        lock (gameSpeedLock) {
            supportsGameSpeed = false;
            gameSpeedControllable = false;
            gameSpeedMultiplier = GameSpeedLimits.Minimum;
            pendingGameSpeedRequestId = null;
        }
    }

    private void CaptureGameSpeedHello(ServerHello hello)
    {
        lock (gameSpeedLock) {
            supportsGameSpeed = hello.SupportsGameSpeed;
            gameSpeedControllable = hello.SupportsGameSpeed && hello.GameSpeedControllable;
            var multiplier = hello.SupportsGameSpeed ? GameSpeedLimits.FromWire(hello.GameSpeedMultiplier) : 1;
            if (!GameSpeedLimits.IsValid(multiplier))
                throw new InvalidDataException("The server returned an invalid game speed.");
            gameSpeedMultiplier = multiplier;
            pendingGameSpeedRequestId = null;
        }
    }

    private Envelope? CreateGameSpeedRequestEnvelope()
    {
        lock (gameSpeedLock) {
            if (!supportsGameSpeed || !gameSpeedControllable || pendingGameSpeedRequestId.HasValue
                || desiredGameSpeedMultiplier is not { } desired || desired == gameSpeedMultiplier)
                return null;
            var requestId = ++nextGameSpeedRequestId;
            pendingGameSpeedRequestId = requestId;
            return new Envelope {
                GameSpeedRequest = new GameSpeedRequest { RequestId = requestId, Multiplier = desired },
            };
        }
    }

    private Task SendDesiredGameSpeedAsync(CancellationToken cancellationToken)
    {
        lock (gameSpeedLock) {
            if (!supportsGameSpeed || !gameSpeedControllable || pendingGameSpeedRequestId.HasValue
                || desiredGameSpeedMultiplier is not { } desired || desired == gameSpeedMultiplier)
                return Task.CompletedTask;
        }
        return WriteEnvelopeAsync(CreateGameSpeedRequestEnvelope, cancellationToken);
    }

    /** Applies the speed the server now runs at and re-anchors the projection at its boundary. */
    private void ObserveGameSpeedAck(GameSpeedAck ack)
    {
        bool changed;
        lock (gameSpeedLock) {
            if (pendingGameSpeedRequestId != ack.RequestId)
                return;
            pendingGameSpeedRequestId = null;
            var multiplier = GameSpeedLimits.FromWire(ack.Multiplier);
            if (!GameSpeedLimits.IsValid(multiplier))
                throw new InvalidDataException("The server acknowledged an invalid game speed.");
            changed = multiplier != gameSpeedMultiplier;
            gameSpeedMultiplier = multiplier;
            // A rejected value is not retried until the player picks another one.
            if (!ack.Accepted)
                desiredGameSpeedMultiplier = multiplier;
            LastGameSpeedRejectReason = ack.Accepted ? GameSpeedRejectReason.Unspecified : ack.RejectReason;
            GameSpeedRevision++;
        }
        if (changed && IsClockGatedCommandScheduling)
            commandClock.ObserveSchedulingClock(ack.EffectiveTick);
    }
}
