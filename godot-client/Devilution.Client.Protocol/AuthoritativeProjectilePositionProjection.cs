using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Defines when a projected projectile must snap instead of interpolating. */
public static class AuthoritativeProjectilePositionProjection
{
    /** Returns true when a cached projectile position must snap to the next state. */
    public static bool RequiresPresentationReset(
        ProjectileSnapshot previous,
        ProjectileSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);

        return previous.EntityId != next.EntityId
            || AuthoritativeEntityPositionProjection.RequiresLevelReset(
                previous.LevelId,
                next.LevelId)
            || previous.Phase != next.Phase
            || previous.Behavior != next.Behavior
            || previous.PresentationKind != next.PresentationKind
            || previous.NativeVisual != next.NativeVisual
            || previous.NativeMissileId != next.NativeMissileId;
    }
}
