namespace Devilution.Diablo;

public sealed partial class AuthoritativeCathedralNativePopulationPolicy
{
    internal IReadOnlyList<AuthoritativeCathedralNativeTrapFacts> CreateInitialNativeTrapFacts()
        => Bindings.CreateInitialNativeTrapFacts();

    internal IReadOnlyList<DiabloNativeTrapState> CreateInitialNativeTrapStates()
        => Bindings.CreateInitialNativeTrapStates();

    internal bool TryGetNativeTrapFacts(
        AuthoritativeWorldObject trigger,
        out IReadOnlyList<AuthoritativeCathedralNativeTrapFacts> facts)
        => Bindings.TryGetNativeTrapFacts(trigger, out facts);

    internal AuthoritativeCathedralNativeTrapFacts GetNativeTrapFacts(int trapObjectSlot)
        => Bindings.GetNativeTrapFacts(trapObjectSlot);

    internal static bool IsSupportedNativeTrapTriggerType(
        AuthoritativeCathedralPopulationObjectState value)
        => AuthoritativeNativePopulationBindings.IsSupportedNativeTrapTriggerType(value);

    internal static bool HasSupportedNativeTrapLink(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState trigger)
        => AuthoritativeNativePopulationBindings.HasSupportedNativeTrapLink(state, trigger);
}
