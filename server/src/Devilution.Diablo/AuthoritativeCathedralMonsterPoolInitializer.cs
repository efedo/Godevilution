using Devilution.Engine;

namespace Devilution.Diablo;

/** Detached fresh ordinary-Cathedral monster storage before the population reseed. */
public sealed class AuthoritativeCathedralFreshMonsterPool
{
    internal AuthoritativeCathedralFreshMonsterPool(
        AuthoritativeCathedralPopulationMonsterState[] monsters, uint finalRandomState)
    {
        Monsters = Array.AsReadOnly(monsters.ToArray());
        FinalRandomState = finalRandomState;
    }

    public IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> Monsters { get; }
    public uint FinalRandomState { get; }
}

/**
 * Fresh zeroed native storage followed by ClrAllMonsters: direction and one
 * discarded draw per slot. This stream precedes, and is not reconstructed from,
 * the independently reseeded GetLevelMTypes/initial-object population stream.
 * Retained/revisited monster pools require their actual lifecycle state instead.
 */
public static class AuthoritativeCathedralMonsterPoolInitializer
{
    public static AuthoritativeCathedralFreshMonsterPool CreateFresh(uint randomStateBeforeClear)
    {
        var random = new DeterministicLegacyRandom(randomStateBeforeClear);
        var monsters = new AuthoritativeCathedralPopulationMonsterState[
            AuthoritativeCathedralPopulationParameters.MonsterCapacity];
        for (var slot = 0; slot < monsters.Length; slot++) {
            monsters[slot] = new() {
                Slot = slot,
                // Type is a derived model field: levelType 0 resolves to the
                // eventual Golem bestiary entry; clearing does not select types.
                Type = AuthoritativeCathedralPopulationParameters.GolemNativeType,
                Direction = random.GenerateRnd(8),
                Flags = 1U << 10,
                LightId = -1,
            };
            random.Discard(1);
        }
        return new(monsters, random.State);
    }
}
