namespace Devilution.Diablo;

public partial class AuthoritativeCathedralItemRuntime
{
    /** Identify scrolls use the item action binding, matching the initial native population builder. */
    private static bool IsNativeIdentifyScroll(int miscId, AuthoritativeCathedralItemContentBinding binding)
        => miscId == 21 && binding.ItemActionKind == DiabloItemActionKind.Identify
            && binding.SpellSourceKind is null or DiabloItemSpellSourceKind.None;

    private int ResolveProjectedSpellId(AuthoritativeCathedralInitialItemState raw,
        AuthoritativeCathedralItemContentBinding binding)
    {
        if (raw.SpellId == 5 && IsNativeIdentifyScroll(raw.MiscId, binding)) return 0;
        if (raw.SpellId < 0) return raw.SpellId;
        if (profile.NativeSpellBindings is not null
            && profile.NativeSpellBindings.TryGetValue(raw.SpellId, out var stableSpellId))
            return stableSpellId;
        throw new InvalidDataException(
            $"Native spell ordinal {raw.SpellId} has no explicit stable content binding.");
    }

    private int ResolveNativeItemSpellId(AuthoritativeItemState state)
        => state.SpellId == 0 && IsNativeIdentifyScroll(state.MiscId, GetBinding(state.ItemIndex))
            ? 5 : ToNativeSpellId(state.SpellId);
}
