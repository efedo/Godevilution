using Devilution.Protocol.V1;

namespace Devilution.Protocol;

/** Retained wall-trap firing is distinct from an opened or disarmed trigger. */
public static class AuthoritativeNativeTrapFiringState
{
    public static void Validate(IReadOnlyList<ObjectSnapshot> objects, ulong snapshotTick = ulong.MaxValue)
    {
        foreach (var source in objects) {
            var id = source.NativeTrapFiredTriggerEntityId;
            if (id == 0) {
                if (source.HasNativeTrapFiredTick)
                    throw new InvalidDataException("A native firing tick has no fired link.");
                continue;
            }
            var trigger = objects.FirstOrDefault(value => value.EntityId == id);
            if (source.NativeObjectId is not (53 or 54) || source.Destroyed || source.LevelId == 0
                || trigger is null || trigger.LevelId != source.LevelId || trigger.Destroyed || trigger.TrapArmed
                || source.HasNativeTrapFiredTick && source.NativeTrapFiredTick > snapshotTick
                || trigger.NativeObjectId is not (1 or 2 or 5 or 6 or 7 or 42 or 43))
                throw new InvalidDataException("A retained native trap firing has an invalid wall source or trigger link.");
        }
    }
}
