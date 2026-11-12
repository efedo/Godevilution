namespace Devilution.Client.Acceptance;

/** A control may stop being actionable while the real input is revealed and hovered. */
internal static class AcceptanceControlClick
{
    /** A selection can lose its living actor during hover, press/release, or popup navigation. */
    internal static void RequireAvailable(AcceptanceCommandBoundary.Observation state, bool enabled, string context)
    {
        // Snapshot ordering puts the local player first, including multiplayer HUDs.
        if (state.Snapshot?.Players.FirstOrDefault() is { Life: <= 0 } player)
            throw new InvalidOperationException($"{context}: the character died on L{player.LevelId} at "
                + $"({player.PositionX},{player.PositionY}); snapshotTick={state.Snapshot.Tick}; {state.Describe()}.");
        if (!enabled)
            throw new InvalidOperationException($"{context} is disabled; {state.Describe()}.");
    }

    internal static async Task RequireClickAsync(Func<AcceptanceCommandBoundary.Observation> observe,
        Func<bool> enabled, Func<Action, Task> click, string context)
    {
        void Validate() => RequireAvailable(observe(), enabled(), context);
        Validate();
        await click(Validate);
        Validate();
    }

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
