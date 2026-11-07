using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Visual walking only: native players.cpp DoWalk and world_renderer.cpp GetOffsetForWalking. */
public sealed class AuthoritativeLocalPlayerPresentation
{
    public const int DefaultWalkTicks = 8;
    private uint entity;
    private uint level;
    private bool initialized;
    private PlayerActionKind lastKind;
    private ClientPosition? previousAuthoritativePosition;
    private (ulong Started, int X, int Y)? completedVisualStep;
    public ClientPresentationPosition Position { get; private set; }

    /** The caller uses this one world-space position for the local sprite and both cameras. */
    public ClientPresentationPosition Advance(PlayerSnapshot player, double tick,
        ClientPosition? predictedDestination, double deltaSeconds, uint tickRateHz)
    {
        ArgumentNullException.ThrowIfNull(player);
        var hadPosition = initialized;
        var changedIdentity = initialized && (entity != player.EntityId || level != player.LevelId);
        var authoritative = AuthoritativePlayerPositionProjection.ResolvePresentation(player, tick);
        var occupiedOrReserved = AuthoritativePlayerPositionProjection.Resolve(player);
        var discontinuity = player.CurrentAction?.Kind is PlayerActionKind.LevelTransition or PlayerActionKind.RestartTown
            || initialized && previousAuthoritativePosition is { } previous
                && (Math.Abs((long)occupiedOrReserved.X - previous.X) > 1 || Math.Abs((long)occupiedOrReserved.Y - previous.Y) > 1);
        previousAuthoritativePosition = occupiedOrReserved;
        if (!initialized || entity != player.EntityId || level != player.LevelId || player.Life <= 0
            || discontinuity || lastKind is PlayerActionKind.LevelTransition or PlayerActionKind.RestartTown
            || Distance(Position, authoritative) > 2.01) {
            Position = authoritative;
            completedVisualStep = null;
            initialized = true;
            entity = player.EntityId;
            level = player.LevelId;
        }
        lastKind = player.CurrentAction?.Kind ?? PlayerActionKind.Unspecified;
        if (player.Life <= 0 || discontinuity || hadPosition && changedIdentity || !hadPosition && predictedDestination is null)
            return Position;

        var delta = double.IsFinite(deltaSeconds) ? Math.Clamp(deltaSeconds, 0, 1) : 0;
        var rate = tickRateHz == 0 ? 20 : tickRateHz;
        var walk = player.CurrentAction is { Kind: PlayerActionKind.Move, EffectApplied: false } action ? action : null;
        var duration = walk is not null && walk.EffectTick > walk.StartedAtTick
            ? walk.EffectTick - walk.StartedAtTick : DefaultWalkTicks;
        var speed = rate / (double)duration;
        var target = authoritative;
        if (predictedDestination is { } predicted) {
            // Never cut the corner of an unfinished server-owned step when input reverses or turns.
            var reserved = AuthoritativePlayerPositionProjection.Resolve(player);
            var step = walk is null ? ((ulong, int, int)?)null : (walk.StartedAtTick, player.PositionX, player.PositionY);
            if (walk is not null && Distance(Position, new(reserved.X, reserved.Y)) < 0.000001)
                completedVisualStep = step;
            var bounded = new ClientPosition(reserved.X + (int)Math.Clamp((long)predicted.X - reserved.X, -1, 1),
                reserved.Y + (int)Math.Clamp((long)predicted.Y - reserved.Y, -1, 1));
            target = walk is not null && completedVisualStep != step && OnWalk(Position, player, walk) && Distance(Position,
                    new ClientPresentationPosition(reserved.X, reserved.Y)) > 0.000001
                ? new ClientPresentationPosition(reserved.X, reserved.Y)
                : new ClientPresentationPosition(bounded.X, bounded.Y);
            if (walk is not null && completedVisualStep != step && !OnWalk(Position, player, walk)) {
                target = authoritative;
                speed *= 2;
            }
        } else if (walk is not null && OnWalk(Position, player, walk)) {
            // Input may have started before the authoritative action. Do not rewind it on ACK.
            var reserved = AuthoritativePlayerPositionProjection.Resolve(player);
            target = new ClientPresentationPosition(reserved.X, reserved.Y);
        } else {
            // Rejections, interrupted walks and small server corrections return smoothly.
            speed *= 2;
        }
        Position = MoveTowards(Position, target, speed * delta);
        return Position;
    }

    /** Native sprite and camera offsets use exactly the same fractional progress. */
    public NativeSceneViewport NativeViewport(int width, int height, int levelWidth, int levelHeight)
        => NativeSceneViewport.Create(width, height, levelWidth, levelHeight, Position.X, Position.Y);

    public AuthoritativeWorldViewport FallbackViewport(int width, int height, int levelWidth, int levelHeight, double tileSize = 32)
        => AuthoritativeWorldViewport.Create(width, height, levelWidth, levelHeight, Position.X, Position.Y, tileSize);

    public void Reset() => initialized = false;

    private static bool OnWalk(ClientPresentationPosition position, PlayerSnapshot player, PlayerActionSnapshot walk)
    {
        var dx = position.X - player.PositionX;
        var dy = position.Y - player.PositionY;
        return dx * walk.DirectionX >= -0.000001 && dy * walk.DirectionY >= -0.000001
            && Math.Abs(dx) <= Math.Abs(walk.DirectionX) + 0.000001
            && Math.Abs(dy) <= Math.Abs(walk.DirectionY) + 0.000001
            && (walk.DirectionX == 0 || walk.DirectionY == 0 || Math.Abs(dx * walk.DirectionY - dy * walk.DirectionX) < 0.000001);
    }

    private static ClientPresentationPosition MoveTowards(ClientPresentationPosition from, ClientPresentationPosition to, double step)
    {
        var distance = Distance(from, to);
        if (distance <= step || distance == 0) return to;
        return new(from.X + (to.X - from.X) * step / distance, from.Y + (to.Y - from.Y) * step / distance);
    }

    private static double Distance(ClientPresentationPosition from, ClientPresentationPosition to)
        => Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
}
