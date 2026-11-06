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
