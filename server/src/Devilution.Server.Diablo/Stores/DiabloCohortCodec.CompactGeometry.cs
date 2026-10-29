using Devilution.Diablo;

namespace Devilution.Server.Stores;

/**
 * Format 13 is format 12 with each geometry level written once. Format 12 holds
 * four copies: Geometry, the door backing geometry (always equal to Geometry),
 * the door effective levels and the infrastructure effective levels (always equal
 * to each other). Format 13 keeps Geometry, writes the effective levels that differ
 * from it and leaves both door copies empty; decoding restores all four.
 */
public sealed partial class DiabloCohortCodec
{
    private const int CompactGeometryFormatVersion = DiabloCohortState.NativeCatacombsCompactGeometryFormatVersion;
    private static readonly DiabloWorldGeometryState EmptyGeometry = new([], []);

    /** False keeps the full format 12 encoding, for a cohort whose copies disagree. */
    internal static bool TryCompactGeometry(DiabloCohortState state, out DiabloCohortState compact)
    {
        compact = state;
        if (state.FormatVersion != NativeCatacombsProgressionFormatVersion
            || state.Geometry is not { Levels: { } levels, WarpTargets: { } warps }
            || state.CathedralDoors is not { } doors
            || state.WorldInfrastructure?.EffectiveLevels is not { } effective
            || doors.BackingGeometry?.Levels is null || doors.BackingGeometry.WarpTargets is null
            || doors.EffectiveLevels is null
            || effective.Count != levels.Count
            || !warps.SequenceEqual(doors.BackingGeometry.WarpTargets)
            || !SameLevels(levels, doors.BackingGeometry.Levels)
            || !SameLevels(effective, doors.EffectiveLevels))
            return false;
        var changed = new List<DiabloWorldGeometryLevelState>();
        for (var index = 0; index < levels.Count; index++) {
            if (effective[index].LevelId != levels[index].LevelId)
                return false;
            if (!SameLevel(effective[index], levels[index]))
                changed.Add(effective[index]);
        }
        compact = state with {
            FormatVersion = CompactGeometryFormatVersion,
            CathedralDoors = doors with { BackingGeometry = EmptyGeometry, EffectiveLevels = [] },
            WorldInfrastructure = state.WorldInfrastructure with { EffectiveLevels = changed.ToArray() },
        };
        return true;
    }

    internal static DiabloCohortState ExpandCompactGeometry(DiabloCohortState state)
    {
        if (state.Geometry is not { Levels: { } levels, WarpTargets: not null } geometry
            || state.CathedralDoors is not { } doors
            || state.WorldInfrastructure?.EffectiveLevels is not { } changed
            || doors.BackingGeometry is not { Levels.Count: 0, WarpTargets.Count: 0 }
            || doors.EffectiveLevels is not { Count: 0 })
            throw new InvalidDataException("A compact-geometry cohort must hold only its single geometry copy.");
        var byLevel = new Dictionary<uint, DiabloWorldGeometryLevelState>();
        var positions = levels.Select((level, index) => (level.LevelId, index))
            .ToDictionary(pair => pair.LevelId, pair => pair.index);
        var previous = -1;
        foreach (var level in changed) {
            if (level is null || !positions.TryGetValue(level.LevelId, out var position)
                || position <= previous || SameLevel(level, levels[position]))
                throw new InvalidDataException("Compact effective levels must be ordered, distinct changes of the geometry.");
            previous = position;
            byLevel.Add(level.LevelId, level);
        }
        var effective = levels.Select(level => byLevel.GetValueOrDefault(level.LevelId, level)).ToArray();
        return state with {
            FormatVersion = NativeCatacombsProgressionFormatVersion,
            CathedralDoors = doors with { BackingGeometry = geometry, EffectiveLevels = effective },
            WorldInfrastructure = state.WorldInfrastructure with { EffectiveLevels = effective },
        };
    }

    private static bool SameLevels(
        IReadOnlyList<DiabloWorldGeometryLevelState> left,
        IReadOnlyList<DiabloWorldGeometryLevelState> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++) {
            if (!SameLevel(left[index], right[index]))
                return false;
        }
        return true;
    }

    /** Field-by-field value equality; the record's own equality compares list references. */
    private static bool SameLevel(DiabloWorldGeometryLevelState? left, DiabloWorldGeometryLevelState? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        return left.LevelId == right.LevelId
            && string.Equals(left.StableSymbol, right.StableSymbol, StringComparison.Ordinal)
            && left.Width == right.Width && left.Height == right.Height
            && left.HasExplicitNativeVisionBlockedCells == right.HasExplicitNativeVisionBlockedCells
            && left.IsDefaultStart == right.IsDefaultStart
            && left.SuppressMultiplayerDeathDrops == right.SuppressMultiplayerDeathDrops
            && left.StartPositionX == right.StartPositionX && left.StartPositionY == right.StartPositionY
            && SameList(left.BlockedCells, right.BlockedCells)
            && SameList(left.MissileBlockedCells, right.MissileBlockedCells)
            && SameList(left.NativeTransparency, right.NativeTransparency)
            && SameList(left.NativeObjectLayer, right.NativeObjectLayer)
            && SameList(left.NativeMegaTileLayer, right.NativeMegaTileLayer)
            && SameList(left.NativePieceLayer, right.NativePieceLayer)
            && SamePieceProperties(left.NativePieceProperties, right.NativePieceProperties)
            && SameList(left.NativePieceCollisionCells, right.NativePieceCollisionCells)
            && SameList(left.NativeVisionBlockedCells, right.NativeVisionBlockedCells)
            && SameList(left.NativePieceMissileBlockedCells, right.NativePieceMissileBlockedCells)
            && SameList(left.NativeMonsterLayer, right.NativeMonsterLayer);
    }

    private static bool SameList<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right) where T : IEquatable<T>
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++) {
            if (!left[index].Equals(right[index]))
                return false;
        }
        return true;
    }

    /** Ordered comparison: dictionary order is part of the written bytes. */
    private static bool SamePieceProperties(
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? left,
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;
        return left.SequenceEqual(right);
    }
}
