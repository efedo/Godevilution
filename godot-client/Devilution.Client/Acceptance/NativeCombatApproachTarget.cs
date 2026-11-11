using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Keep the chosen detour across walk acknowledgments instead of alternating between nearby monsters. */
internal sealed class NativeCombatApproachTarget(uint? requiredTarget = null)
{
    private readonly uint? required = requiredTarget;
    private uint? following = requiredTarget;

    internal IEnumerable<MonsterSnapshot> Candidates(IEnumerable<MonsterSnapshot> eligible,
        Func<MonsterSnapshot, long> distance, Func<MonsterSnapshot, bool>? canAttackNow = null)
        => eligible.Where(value => required is null || value.EntityId == required || canAttackNow?.Invoke(value) == true)
            .OrderBy(value => canAttackNow?.Invoke(value) == true ? 0 : value.EntityId == following ? 1 : 2)
            .ThenBy(distance).ThenBy(value => value.EntityId).Take(12);

    internal void Follow(uint entityId) => following = entityId;
}

/** Counter-fire is optional; a failed diversion must not declare the requested stair blocker unreachable. */
internal sealed class NativeCombatAttackTarget(uint requestedTarget)
{
    private readonly uint requested = requestedTarget;
    private bool counterFireAllowed = true;
    internal uint Current { get; private set; } = requestedTarget;

    internal void PreferRanged(uint shooter, bool currentIsRanged)
    {
        if (counterFireAllowed && shooter != 0 && !currentIsRanged) Current = shooter;
    }

    internal bool FallBack()
    {
        if (Current == requested) return false;
        Current = requested;
        counterFireAllowed = false;
        return true;
    }

    /** At most two searches: the optional shooter, then the original target within the same bounded attempt. */
    internal async Task<MonsterSnapshot?> ApproachAsync(Func<uint, Task<MonsterSnapshot>> approach,
        Func<MonsterSnapshot, uint, bool> reached, Func<bool> canRecover)
    {
        for (var attempt = 0; attempt < 2; attempt++) {
            try {
                var result = await approach(Current);
                if (reached(result, Current)) return result;
            } catch (InvalidOperationException) when (canRecover()) {
                // The approach failed; only failure of the original target may escape as no reachable encounter.
            }
            if (!FallBack()) return null;
        }
        return null;
    }
}
