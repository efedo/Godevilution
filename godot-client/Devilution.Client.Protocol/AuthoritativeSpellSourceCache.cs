using Devilution.Protocol.V1;
using Google.Protobuf.Collections;

namespace Devilution.Client.Protocol;

/** Spell sources depend on carried items and spell ownership, not ticks or movement. */
public sealed class AuthoritativeSpellSourceCache
{
    private readonly PresentationChangeGate<Inputs> changes = new();
    private IReadOnlyList<AuthoritativeSpellSource> sources = [];

    public IReadOnlyList<AuthoritativeSpellSource> GetSources(Snapshot? snapshot)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (changes.ShouldUpdate(true, new(player?.Inventory, player?.Belt, player?.Equipment,
                player?.MemorizedSpells, player?.ZeroLevelMemorizedSpells, player?.Stash, player?.StashOpen ?? false,
                player?.AbilitySpellId ?? 0, player?.AbilityKind ?? PlayerAbilityKind.Unspecified,
                player?.AbilityTargetingKind ?? SpellTargetingKind.Unspecified)))
            sources = AuthoritativeSpellSourceSelector.GetSources(snapshot);
        return sources;
    }

    private readonly record struct Inputs(RepeatedField<ItemSnapshot>? Inventory, RepeatedField<BeltItemSnapshot>? Belt,
        RepeatedField<EquippedItemSnapshot>? Equipment, RepeatedField<PlayerSpellLevelSnapshot>? Spells,
        RepeatedField<PlayerSpellLevelSnapshot>? ZeroSpells, RepeatedField<StashItemSnapshot>? Stash, bool StashOpen,
        uint Ability, PlayerAbilityKind AbilityKind, SpellTargetingKind Targeting);
}
