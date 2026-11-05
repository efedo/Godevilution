namespace Devilution.Client.Acceptance;

/** A control may stop being actionable while the real input is revealed and hovered. */
internal static class AcceptanceControlClick
{
    internal static async Task<bool> WhenEnabled(Func<bool> enabled, Func<Task> prepare, Func<Task> pressAndRelease)
    {
        if (!enabled()) return false;
        await prepare();
        if (!enabled()) return false;
        // No await separates this check from the callback's actual button press.
        await pressAndRelease();
        return true;
    }
}
