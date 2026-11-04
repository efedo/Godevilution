namespace Devilution.Diablo;

/** Explicit Town channels remain separate from legacy half-resolution geometry. */
public sealed partial class DiabloWorldState
{
    private AuthoritativeNativeTownCatalog? nativeTownCatalog;
    private AuthoritativeTownGeometry? nativeTownGeometry;
    private readonly List<AuthoritativeNativeTownMutation> nativeTownMutations = [];

    public bool HasNativeTown => nativeTownCatalog is not null;

    /** Read-only semantic owner used to derive presentation projections from the active profile. */
    public AuthoritativeNativeTownCatalog? NativeTownCatalog => nativeTownCatalog;

    private void InitializeNativeTown(AuthoritativeNativeTownCatalog? catalog,
        DiabloNativeTownCheckpoint? checkpoint, bool deferred)
    {
        if (catalog is null) {
            if (checkpoint is not null)
                throw new InvalidDataException("Native Town backing has no configured content owner.");
            return;
        }
        if (World is null || nativeTownCatalog is not null)
            throw new InvalidDataException("Native Town requires one configured authoritative world.");
        if (checkpoint is not null)
            catalog = catalog.ForCheckpoint(checkpoint);
        nativeTownCatalog = catalog;
        nativeTownGeometry = checkpoint is null ? catalog.CreateInitialGeometry() : catalog.Restore(checkpoint);
        if (checkpoint is not null)
            nativeTownMutations.AddRange(checkpoint.Mutations);
        var actual = World.Levels.SingleOrDefault(level => level.LevelId == catalog.LevelId)
            ?? throw new InvalidDataException("Native Town backing level is missing.");
        RequireNativeTownLevel(actual, allowOmittedSpecial: deferred && checkpoint is not null);
        World.ReplaceLevel(catalog.CreateBackingLevel(nativeTownGeometry, actual));
    }

    private bool IsNativeTownOccupied(uint levelId, int x, int y)
        => nativeTownCatalog is { } catalog && levelId == catalog.LevelId && catalog.Roster.IsOccupied(x, y);

    private bool IsOwnedNativeTownLevel(AuthoritativeLevel level)
    {
        if (nativeTownCatalog is not { } catalog || level.LevelId != catalog.LevelId)
            return false;
        RequireNativeTownLevel(level, allowOmittedSpecial: false);
        return true;
    }

    private void RequireNativeTownLevel(AuthoritativeLevel actual, bool allowOmittedSpecial)
        => RequireNativeTownLevel(nativeTownCatalog!, nativeTownGeometry!, actual, allowOmittedSpecial);

    private static void RequireNativeTownLevel(AuthoritativeNativeTownCatalog catalog,
        AuthoritativeTownGeometry geometry, AuthoritativeLevel actual, bool allowOmittedSpecial)
    {
        var expected = catalog.CreateBackingLevel(geometry, actual);
        DiabloCathedralDoorCheckpoint.RequireSameLevels(
            [CaptureCohortLevel(expected)], [CaptureCohortLevel(actual)]);
        if (!actual.NativeSpecialLayer.SequenceEqual(expected.NativeSpecialLayer)
            && !(allowOmittedSpecial && actual.NativeSpecialLayer.Count == 0))
            throw new InvalidDataException("Native Town special bytes differ from the trusted checkpoint owner.");
    }

    /** Called after legacy geometry restoration, before accepting any player or door topology. */
    private void PreserveNativeTownProjection()
    {
        if (nativeTownCatalog is not { } catalog || World is null)
            return;
        var backing = World.Levels.Single(level => level.LevelId == catalog.LevelId);
        RequireNativeTownLevel(backing, allowOmittedSpecial: true);
        if (projectedLevels.TryGetValue(catalog.LevelId, out var projected))
            RequireNativeTownLevel(projected, allowOmittedSpecial: true);
        var replacement = catalog.CreateBackingLevel(nativeTownGeometry!, backing);
        World.ReplaceLevel(replacement);
        projectedLevels[catalog.LevelId] = replacement;
    }

    public DiabloNativeTownCheckpoint? CaptureNativeTown()
        => CaptureNativeTown(null);

    internal DiabloNativeTownCheckpoint? CaptureNativeTown(List<Action>? deferredValidations)
    {
        if (nativeTownCatalog is null)
            return null;
        if (World is null)
            throw new InvalidDataException("Native Town checkpoint has no authoritative world.");
        if (deferredValidations is not null) {
            var catalog = nativeTownCatalog;
            var geometry = nativeTownGeometry!;
            var backing = World.Levels.Single(level => level.LevelId == catalog.LevelId);
            var effective = projectedLevels.GetValueOrDefault(catalog.LevelId);
            deferredValidations.Add(() => {
                RequireNativeTownLevel(catalog, geometry, backing, false);
                if (effective is not null)
                    RequireNativeTownLevel(catalog, geometry, effective, false);
            });
            return catalog.CaptureForDeferredValidation(geometry, nativeTownMutations, deferredValidations);
        }
        RequireNativeTownLevel(World.Levels.Single(level => level.LevelId == nativeTownCatalog.LevelId), false);
        if (projectedLevels.TryGetValue(nativeTownCatalog.LevelId, out var projected))
            RequireNativeTownLevel(projected, false);
        return nativeTownCatalog.Capture(nativeTownGeometry!, nativeTownMutations);
    }

    /** Hydrates a detached fresh run from validated native provenance before saved geometry is compared. */
    internal void RegenerateNativeTownForCohort(DiabloNativeTownCheckpoint? checkpoint)
    {
        if (nativeTownCatalog is null && checkpoint is null)
            return;
        if (nativeTownCatalog is null || checkpoint is null || World is null)
            throw new InvalidDataException("Saved native Town capability differs from the active profile.");
        var owner = nativeTownCatalog.ForCheckpoint(checkpoint);
        var restored = owner.Restore(checkpoint);
        var prototype = World.Levels.Single(level => level.LevelId == owner.LevelId);
        var replacement = owner.CreateBackingLevel(restored, prototype);
        nativeTownCatalog = owner;
        nativeTownGeometry = restored;
        nativeTownMutations.Clear();
        nativeTownMutations.AddRange(checkpoint.Mutations);
        World.ReplaceLevel(replacement);
        projectedLevels[nativeTownCatalog.LevelId] = replacement;
        PreserveNativeTownDoorBacking(replacement);
    }

    private void PreserveNativeTownDoorBacking(AuthoritativeLevel town)
    {
        if (trustedCathedralDoorBackingGeometry is { Levels: { } levels } backing)
            trustedCathedralDoorBackingGeometry = backing with {
                Levels = levels.Select(level => level.LevelId == town.LevelId
                    ? CaptureCohortLevel(town) : level).ToArray(),
            };
    }

    internal void RestoreNativeTown(DiabloNativeTownCheckpoint? checkpoint)
    {
        if (nativeTownCatalog is null && checkpoint is null)
            return;
        if (nativeTownCatalog is null || checkpoint is null)
            throw new InvalidDataException("Saved native Town capability differs from the active profile.");
        var owner = nativeTownCatalog.ForCheckpoint(checkpoint);
        var restored = owner.Restore(checkpoint);
        // Require the duplicated legacy fine/SOL projection before installing owned special/coarse state.
        var (previousCatalog, previous) = (nativeTownCatalog, nativeTownGeometry);
        (nativeTownCatalog, nativeTownGeometry) = (owner, restored);
        try {
            PreserveNativeTownProjection();
        } catch {
            (nativeTownCatalog, nativeTownGeometry) = (previousCatalog, previous);
            throw;
        }
        nativeTownMutations.Clear();
        nativeTownMutations.AddRange(checkpoint.Mutations);
    }

    /** Native IsWarpOpen: whether the active Town has this warp; non-Town warps are ungated. */
    public bool IsNativeTownWarpOpen(AuthoritativeTownWarpUnlocks warp)
        => warp == AuthoritativeTownWarpUnlocks.None
            || nativeTownCatalog is { } catalog && catalog.IsTownWarpOpen(warp);

    /** Warp triggers that exist in the current world, as native InitTownTriggers publishes them. */
    public IReadOnlyList<AuthoritativeWarpTarget>? PublishedWarpTargets
        => World?.WarpTargets.Where(warp => IsNativeTownWarpOpen(warp.RequiredTownWarp)).ToArray();

    /**
     * Native WM_DIABTWARPUP sets the pTownWarps bit; the next CreateTown draws the opening.
     * Rebuilds the Town under the unlocked settings with the same typed history.
     */
    internal void UnlockNativeTownWarp(AuthoritativeTownWarpUnlocks warp)
    {
        if (warp == AuthoritativeTownWarpUnlocks.None || nativeTownCatalog is not { } catalog
            || catalog.IsTownWarpOpen(warp) || World is null)
            return;
        var unlocked = catalog.WithWarpUnlocks(catalog.Settings.WarpUnlocks | warp);
        var geometry = unlocked.Rebuild(nativeTownMutations);
        var prototype = World.Levels.Single(level => level.LevelId == catalog.LevelId);
        var replacement = unlocked.CreateBackingLevel(geometry, prototype);
        (nativeTownCatalog, nativeTownGeometry) = (unlocked, geometry);
        World.ReplaceLevel(replacement);
        projectedLevels[catalog.LevelId] = replacement;
        PreserveNativeTownDoorBacking(replacement);
    }

    /** Typed operations retain native generation RNG and record exact replayable provenance. */
    public void ApplyNativeTownMutation(AuthoritativeNativeTownMutation mutation)
    {
        if (nativeTownCatalog is null || nativeTownGeometry is null || World is null)
            throw new InvalidOperationException("Native Town mutation requires its configured checkpoint owner.");
        var proposed = mutation switch {
            AuthoritativeNativeTownMutation.OpenHive => AuthoritativeTownGeometryBuilder.OpenHive(nativeTownGeometry),
            AuthoritativeNativeTownMutation.CloseHive => AuthoritativeTownGeometryBuilder.CloseHive(nativeTownGeometry),
            AuthoritativeNativeTownMutation.OpenGrave => AuthoritativeTownGeometryBuilder.OpenGrave(nativeTownGeometry),
            AuthoritativeNativeTownMutation.CloseGrave => AuthoritativeTownGeometryBuilder.CloseGrave(nativeTownGeometry),
            AuthoritativeNativeTownMutation.CleanFountain => AuthoritativeTownGeometryBuilder.CleanFountain(nativeTownGeometry),
            _ => throw new InvalidDataException("Native Town mutation is unsupported."),
        };
        var history = nativeTownMutations.Append(mutation).ToArray();
        _ = nativeTownCatalog.Capture(proposed, history);
        var prototype = World.Levels.Single(level => level.LevelId == nativeTownCatalog.LevelId);
        var replacement = nativeTownCatalog.CreateBackingLevel(proposed, prototype);
        nativeTownGeometry = proposed;
        nativeTownMutations.Add(mutation);
        World.ReplaceLevel(replacement);
        projectedLevels[nativeTownCatalog.LevelId] = replacement;
        PreserveNativeTownDoorBacking(replacement);
    }
}
