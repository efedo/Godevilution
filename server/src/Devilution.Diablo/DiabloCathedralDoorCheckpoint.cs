namespace Devilution.Diablo;

/** Exact native fields, keyed by stable level/cell entity rather than operation replay. */
public sealed record DiabloCathedralDoorObjectState(
    uint EntityId, uint LevelId, int PositionX, int PositionY,
    int NativeObjectId, int DoorState, int CachedPiece, int CachedNeighborPiece,
    int AnimationFrame, int SelectionRegion, bool PreDraw, bool MissilePassable,
    bool Solid);

/** Native special bytes supplement the unchanged legacy geometry DTO. */
public sealed record DiabloCathedralDoorLevelState(
    uint LevelId, IReadOnlyList<ushort> BackingSpecial,
    IReadOnlyList<ushort> EffectiveSpecial,
    IReadOnlyList<DiabloCathedralDoorObjectState> Doors);

/** Versioned opt-in mutable geometry; legacy envelopes cannot silently omit it. */
public sealed record DiabloCathedralDoorCheckpoint(
    int FormatVersion, string ProfileId, int Revision, string CatalogFingerprint,
    DiabloWorldGeometryState BackingGeometry,
    IReadOnlyList<DiabloWorldGeometryLevelState> EffectiveLevels,
    IReadOnlyList<DiabloCathedralDoorLevelState> DoorLevels)
{
    public const int CurrentFormatVersion = 1;

    /** Persistence must retain one identical native-state copy on wire and in geometry. */
    public static void RequireSnapshotCopies(DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CathedralDoorObjects is null)
            throw new InvalidDataException("Door presentation state must be an explicit collection.");
        if (snapshot.CathedralDoors is null) {
            if (snapshot.CathedralDoorObjects.Count != 0)
                throw new InvalidDataException("Typed door persistence requires its mutable geometry checkpoint.");
            return;
        }
        snapshot.CathedralDoors.ValidateShape();
        if (!snapshot.CathedralDoors.DoorLevels.SelectMany(level => level.Doors)
                .SequenceEqual(snapshot.CathedralDoorObjects))
            throw new InvalidDataException("Door wire fields disagree with the mutable geometry checkpoint.");
    }

    public void ValidateShape()
    {
        if (FormatVersion != CurrentFormatVersion
            || ProfileId != AuthoritativeCathedralDoorCatalog.ProfileId
            || Revision != AuthoritativeCathedralDoorCatalog.Revision
            || CatalogFingerprint is null || CatalogFingerprint.Length != 64
            || CatalogFingerprint.Any(value => !char.IsAsciiHexDigitUpper(value))
            || BackingGeometry?.Levels is null || BackingGeometry.WarpTargets is null
            || EffectiveLevels is null || DoorLevels is null
            || DoorLevels.Count is < 1 or > 4
            || DoorLevels.Any(level => level is null)
            || DoorLevels.Select(level => level.LevelId).Distinct().Count() != DoorLevels.Count)
            throw new InvalidDataException("Cathedral door checkpoint has an unsupported or incomplete shape.");
        foreach (var level in DoorLevels) {
            if (level.LevelId is < 1 or > 4 || level.BackingSpecial is null
                || level.EffectiveSpecial is null || level.Doors is null
                || level.Doors.Count > AuthoritativeLevel.NativeMaximumObjectCount
                || level.Doors.Any(door => door is null || door.LevelId != level.LevelId
                    || door.NativeObjectId is < 1 or > 2 || door.DoorState is < 0 or > 2
                    || door.CachedPiece <= 0 || door.CachedNeighborPiece <= 0
                    || door.AnimationFrame is < 0 or > byte.MaxValue
                    || door.SelectionRegion is not (2 or 3)
                    || door.Solid))
                throw new InvalidDataException("Cathedral door checkpoint contains invalid native fields.");
        }
    }

    /** Compares complete duplicated graphs by value, never array-reference equality. */
    internal static void RequireSameGeometry(DiabloWorldGeometryState? expected,
        DiabloWorldGeometryState? restored)
    {
        if (expected?.WarpTargets is null || restored?.WarpTargets is null
            || !expected.WarpTargets.SequenceEqual(restored.WarpTargets))
            throw new InvalidDataException("Cathedral backing route topology differs from the trusted graph.");
        RequireSameLevels(expected.Levels, restored.Levels);
    }

    internal static void RequireSameLevels(IReadOnlyList<DiabloWorldGeometryLevelState>? expected,
        IReadOnlyList<DiabloWorldGeometryLevelState>? restored)
    {
        if (expected is null || restored is null || expected.Count != restored.Count)
            throw new InvalidDataException("Cathedral geometry level coverage differs from the trusted graph.");
        for (var index = 0; index < expected.Count; index++) {
            var first = expected[index];
            var second = restored[index];
            bool Equal<T>(IEnumerable<T>? left, IEnumerable<T>? right)
                => left is not null && right is not null && left.SequenceEqual(right);
            if (first is null || second is null
                || (first.LevelId, first.StableSymbol, first.Width, first.Height,
                    first.IsDefaultStart, first.SuppressMultiplayerDeathDrops,
                    first.StartPositionX, first.StartPositionY, first.HasExplicitNativeVisionBlockedCells)
                != (second.LevelId, second.StableSymbol, second.Width, second.Height,
                    second.IsDefaultStart, second.SuppressMultiplayerDeathDrops,
                    second.StartPositionX, second.StartPositionY, second.HasExplicitNativeVisionBlockedCells)
                || !Equal(first.BlockedCells, second.BlockedCells)
                || !Equal(first.MissileBlockedCells, second.MissileBlockedCells)
                || !Equal(first.NativeTransparency, second.NativeTransparency)
                || !Equal(first.NativeObjectLayer, second.NativeObjectLayer)
                || !Equal(first.NativeMegaTileLayer, second.NativeMegaTileLayer)
                || !Equal(first.NativePieceLayer, second.NativePieceLayer)
                || !Equal(first.NativePieceProperties, second.NativePieceProperties)
                || !Equal(first.NativePieceCollisionCells, second.NativePieceCollisionCells)
                || !Equal(first.NativeVisionBlockedCells, second.NativeVisionBlockedCells)
                || !Equal(first.NativePieceMissileBlockedCells, second.NativePieceMissileBlockedCells)
                || !Equal(first.NativeMonsterLayer, second.NativeMonsterLayer))
                throw new InvalidDataException("Cathedral geometry differs from its trusted or duplicated graph.");
        }
    }
}
