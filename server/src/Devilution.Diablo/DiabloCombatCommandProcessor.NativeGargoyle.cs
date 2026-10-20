namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Unique overrides such as Nightwing retain their admitted effective AI. */
    private static bool HasDifferentCatacombsEffectiveAi(AuthoritativeCombatTarget target, string nativeAi)
        => target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }
            && target.NativeAi != nativeAi;

    private bool OwnsNativeMonsterTiming(AuthoritativeCombatTarget target)
        => world.OwnsNativePopulationLevel(target.LevelId)
            || target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 };

    private bool IsNativeGargoyleWakePending(AuthoritativeCombatTarget target)
        => world.NativeCatacombsMonsterPhase(target.EntityId)?.State
                == AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
            || target.GargoyleSpecialActive;

    /** The C2 statue lock is independent of the older generic Gargoyle healing flag. */
    private bool ConsumeNativeGargoyleWake(AuthoritativeCombatTarget target)
    {
        if (world.TryWakeNativeCatacombsGargoyle(target)) return true;
        if (!target.GargoyleSpecialActive) return false;
        target.GargoyleSpecialActive = false;
        return true;
    }

    private bool AdvanceNativeCatacombsGargoyle(
        AuthoritativeCombatTarget target, IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (!world.HasNativeCatacombsMonsterPhase(target.EntityId)) return false;
        var enemy = players.Where(player => player.Life > 0 && player.LevelId == target.LevelId)
            .OrderBy(player => player.EntityId).FirstOrDefault();
        if (enemy is not null) {
            var distance = Math.Max(Math.Abs(target.PositionX - enemy.FuturePositionX),
                Math.Abs(target.PositionY - enemy.FuturePositionY));
            world.TryWakeNativeCatacombsGargoyleByProximity(target, distance, target.Intelligence + 2);
        }
        var suppressAi = world.AdvanceNativeCatacombsGargoyle(target);
        if (world.NativeCatacombsMonsterPhase(target.EntityId)?.State
            == AuthoritativeNativeCatacombsMonsterPhaseState.Completed)
            ApplyNativeCatacombsGroupUnityAfterModeCompletion(target);
        return suppressAi || target.ActiveForTicks == 0;
    }

    /** Native GroupUnity runs only after a mode completes, before AI repeats. */
    private void ApplyNativeCatacombsGroupUnityAfterModeCompletion(AuthoritativeCombatTarget target)
    {
        if (target.NativeInitialization is not { CatacombsDepth: >= 5 and <= 8 }) return;
        AuthoritativeMonsterGroupUnity.Apply(target, world.CombatTargets, world.HasAiLineOfSight);
        world.ApplyNativeCatacombsGargoyleLeaderWakeAfterGroupUnity(target);
    }
}
