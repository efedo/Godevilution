using System.Globalization;
using Devilution.Data;

namespace Devilution.Diablo;

/** One deterministic cell overlay applied by an authored world interaction. */
public sealed class AuthoritativeWorldMutation
{
    public AuthoritativeWorldMutation(
        uint mutationId,
        uint levelId,
        IEnumerable<int>? blockedCellsAdd = null,
        IEnumerable<int>? blockedCellsRemove = null,
        IEnumerable<int>? missileBlockedCellsAdd = null,
        IEnumerable<int>? missileBlockedCellsRemove = null)
    {
        if (mutationId == 0)
            throw new ArgumentOutOfRangeException(nameof(mutationId));
        MutationId = mutationId;
        LevelId = levelId;
        BlockedCellsAdd = Normalize(blockedCellsAdd);
        BlockedCellsRemove = Normalize(blockedCellsRemove);
        MissileBlockedCellsAdd = Normalize(missileBlockedCellsAdd);
        MissileBlockedCellsRemove = Normalize(missileBlockedCellsRemove);
        if (BlockedCellsAdd.Any(BlockedCellsRemoveOverlap)
            || MissileBlockedCellsAdd.Any(MissileBlockedCellsRemoveOverlap)) {
            throw new InvalidDataException(
                "A world mutation cannot add and remove the same geometry cell.");
        }

        bool BlockedCellsRemoveOverlap(int cell)
            => BlockedCellsRemove.Contains(cell);

        bool MissileBlockedCellsRemoveOverlap(int cell)
            => MissileBlockedCellsRemove.Contains(cell);
    }

    public uint MutationId { get; }

    public uint LevelId { get; }

    public IReadOnlyList<int> BlockedCellsAdd { get; }

    public IReadOnlyList<int> BlockedCellsRemove { get; }

    public IReadOnlyList<int> MissileBlockedCellsAdd { get; }

    public IReadOnlyList<int> MissileBlockedCellsRemove { get; }

    private static IReadOnlyList<int> Normalize(IEnumerable<int>? cells)
        => (cells ?? [])
            .Distinct()
            .OrderBy(cell => cell)
            .ToArray();
}

/** Validated external world-cell mutations used by object interactions. */
public sealed class AuthoritativeWorldMutationCatalog
{
    private readonly IReadOnlyDictionary<uint, AuthoritativeWorldMutation> definitions;

    public AuthoritativeWorldMutationCatalog(IEnumerable<AuthoritativeWorldMutation> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.GroupBy(definition => definition.MutationId).Any(group => group.Count() != 1))
            throw new InvalidDataException("World mutations contain duplicate mutation IDs.");
        this.definitions = values.ToDictionary(definition => definition.MutationId);
    }

    public IReadOnlyList<AuthoritativeWorldMutation> Definitions
        => definitions.Values.OrderBy(definition => definition.MutationId).ToArray();

    public bool TryGet(uint mutationId, out AuthoritativeWorldMutation mutation)
        => definitions.TryGetValue(mutationId, out mutation!);

    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var mutation in definitions.Values) {
            if (!world.ContainsLevel(mutation.LevelId))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} references unknown level {mutation.LevelId}.");
            var level = world.Levels.Single(candidate => candidate.LevelId == mutation.LevelId);
            foreach (var cell in mutation.BlockedCellsAdd
                         .Concat(mutation.BlockedCellsRemove)
                         .Concat(mutation.MissileBlockedCellsAdd)
                         .Concat(mutation.MissileBlockedCellsRemove)) {
                if (cell < 0 || cell >= checked(level.Width * level.Height))
                    throw new InvalidDataException(
                        $"World mutation {mutation.MutationId} contains an out-of-range cell {cell}.");
            }
        }
    }

    public static AuthoritativeWorldMutationCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeWorldMutationCatalog(table.Rows.Select(row =>
            new AuthoritativeWorldMutation(
                row.RequiredUInt32("mutation_id"),
                AuthoritativeLevelIdentity.Resolve(row, stableIds),
                ParseCells(row, "blocked_cells_add"),
                ParseCells(row, "blocked_cells_remove"),
                ParseCells(row, "missile_blocked_cells_add"),
                ParseCells(row, "missile_blocked_cells_remove"))));
    }

    private static IReadOnlyList<int> ParseCells(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return [];
        }
        return value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(cell => int.TryParse(
                    cell,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                ? parsed
                : throw new InvalidDataException(
                    $"World mutation row {row.LineNumber} contains an invalid {column} cell '{cell}'."))
            .ToArray();
    }
}
