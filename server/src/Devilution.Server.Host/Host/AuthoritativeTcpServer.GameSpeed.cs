using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Simulation;

namespace Devilution.Server.Host;

/**
 * Game speed scales the paced clock of the whole host; ticks and their results are unchanged.
 * Policy: a single-player session may change it and the host returns to its default when that
 * connection ends. Multiplayer speed is host-controlled: fixed by --game-speed for the host's
 * lifetime, and participant requests are rejected. A host above 1x refuses clients that do not
 * negotiate game speed, so an older client is never paced faster than it expects.
 */
public sealed partial class AuthoritativeTcpServer
{
    private readonly object gameSpeedSynchronization = new();
    private object? gameSpeedOwner;

    /** The scalable clock; defaults to the authoritative clock when it supports scaling. */
    public IGameSpeedClock? GameSpeedClock { get; init; }

    /** The multiplier a single-player host returns to when the session that changed it ends. */
    public uint DefaultGameSpeedMultiplier
    {
        get => defaultGameSpeedMultiplier;
        init => defaultGameSpeedMultiplier = GameSpeedLimits.IsValid(value)
            ? value : throw new ArgumentOutOfRangeException(nameof(DefaultGameSpeedMultiplier));
    }

    private uint defaultGameSpeedMultiplier = GameSpeedLimits.Minimum;

    private IGameSpeedClock? SpeedClock => GameSpeedClock ?? clock as IGameSpeedClock;

    /** Game speed is offered on the clock-gated pipeline, which the realtime client uses. */
    public bool SupportsGameSpeed => UseClockGatedCommandScheduling && SpeedClock is not null;

    public uint CurrentGameSpeedMultiplier => SpeedClock?.SpeedMultiplier ?? GameSpeedLimits.Minimum;

    private ProtocolError? ValidateGameSpeedPeer(ClientHello hello)
        => CurrentGameSpeedMultiplier > GameSpeedLimits.Minimum && !hello.SupportsGameSpeed
            ? new ProtocolError {
                Code = ProtocolErrorCode.UnsupportedSchema,
                Detail = "This host runs above 1x game speed and requires game-speed support.",
            }
            : null;

    private void ApplyGameSpeedHello(ServerHello serverHello, ClientHello clientHello)
    {
        if (!clientHello.SupportsGameSpeed || !SupportsGameSpeed)
            return;
        serverHello.SupportsGameSpeed = true;
        serverHello.GameSpeedMultiplier = CurrentGameSpeedMultiplier;
        serverHello.GameSpeedControllable = handshake.Identity.GameMode == AuthoritativeGameMode.SinglePlayer;
    }

    /** Caller holds the authority lock; `connection` identifies the requesting connection. */
    private GameSpeedAck ApplyGameSpeedRequestCore(object connection, GameSpeedRequest request)
    {
        var ack = new GameSpeedAck {
            RequestId = request.RequestId,
            Multiplier = CurrentGameSpeedMultiplier,
            EffectiveTick = Math.Max(clock.CurrentTick, commandServer.CurrentTick),
        };
        if (!SupportsGameSpeed || SpeedClock is not { } speedClock) {
            ack.RejectReason = GameSpeedRejectReason.Unsupported;
            return ack;
        }
        if (handshake.Identity.GameMode != AuthoritativeGameMode.SinglePlayer) {
            ack.RejectReason = GameSpeedRejectReason.HostControlled;
            return ack;
        }
        if (!GameSpeedLimits.IsValid(request.Multiplier)) {
            ack.RejectReason = GameSpeedRejectReason.OutOfRange;
            return ack;
        }
        // Simulate ticks already due at the old rate before pacing changes.
        AdvanceCurrentTickCore();
        var effectiveTick = speedClock.SetSpeedMultiplier(request.Multiplier);
        lock (gameSpeedSynchronization)
            gameSpeedOwner = request.Multiplier == DefaultGameSpeedMultiplier ? null : connection;
        ack.Accepted = true;
        ack.Multiplier = request.Multiplier;
        ack.EffectiveTick = Math.Max(effectiveTick, commandServer.CurrentTick);
        return ack;
    }

    /** A single-player speed lasts only for the connection that set it. */
    private void ReleaseGameSpeedOwner(object connection)
    {
        lock (gameSpeedSynchronization) {
            if (!ReferenceEquals(gameSpeedOwner, connection))
                return;
            gameSpeedOwner = null;
            SpeedClock?.SetSpeedMultiplier(DefaultGameSpeedMultiplier);
        }
    }
}
