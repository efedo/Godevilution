using Devilution.Protocol.V1;
using Devilution.Client.Protocol;
using System.Text.Json;

namespace Devilution.Client;

/** Loads presentation-only level geometry from replaceable content assets. */
public sealed class LevelLayoutCatalog
{
    private readonly Dictionary<uint, LevelLayout> layouts = [];
    private readonly Dictionary<uint, LevelLayout> authoritativeLayouts = [];
    private readonly Dictionary<uint, IReadOnlyList<WarpTriggerSnapshot>> warpTriggers = [];
    private readonly AuthoritativeLevelGeometryState authoritativeGeometry = new();

    public LevelLayoutCatalog()
    {
        Load();
    }

    public LevelLayout Resolve(uint levelId)
    {
        if (authoritativeLayouts.TryGetValue(levelId, out var authoritativeLayout))
            return authoritativeLayout;
        if (layouts.TryGetValue(levelId, out var layout))
            return layout;
        return layouts.TryGetValue(0, out var fallback) ? fallback : LevelLayout.Empty;
    }

    public IReadOnlyList<WarpTriggerSnapshot> GetWarpTriggers(uint levelId)
        => warpTriggers.TryGetValue(levelId, out var definitions) ? definitions : [];

    /** Replaces presentation geometry with the active server's validated projection. */
    public void ApplyAuthoritative(IEnumerable<LevelGeometrySnapshot> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        authoritativeGeometry.Replace(definitions);
        authoritativeLayouts.Clear();
        warpTriggers.Clear();
        foreach (var definition in authoritativeGeometry.Definitions.Values) {
            var cells = definition.BlockedCells
                .Where(cell => cell >= 0 && cell < definition.Width * definition.Height)
                .ToHashSet();
            var missileBlocked = AuthoritativeLevelGeometryCellResolver
                .GetMissileOnlyCells(definition);
            authoritativeLayouts[definition.LevelId] = new LevelLayout(
                definition.LevelId,
                $"Level {definition.LevelId}",
                definition.Width,
                definition.Height,
                cells,
                missileBlocked,
                definition.NativeTransparency.ToArray()) {
                NativeObjectLayer = definition.NativeObjectLayer.ToArray(),
                NativePieceLayer = definition.NativePieceLayer.ToArray(),
            };
            warpTriggers[definition.LevelId] = definition.WarpTriggers.ToArray();
        }
    }

    private void Load()
    {
        if (!Godot.FileAccess.FileExists("res://assets/levels/level_layouts.json")) {
            layouts[0] = LevelLayout.Empty;
            return;
        }

        using var file = Godot.FileAccess.Open("res://assets/levels/level_layouts.json", Godot.FileAccess.ModeFlags.Read);
        if (file is null) {
            layouts[0] = LevelLayout.Empty;
            return;
        }
        var document = JsonSerializer.Deserialize<LevelLayoutDocument>(file.GetAsText(), new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
        });
        foreach (var definition in document?.Levels ?? []) {
            if (definition.Width <= 0 || definition.Height <= 0)
                continue;
            var cells = definition.Blocked
                .Where(cell => cell >= 0 && cell < definition.Width * definition.Height)
                .ToHashSet();
            layouts[definition.Id] = new LevelLayout(
                definition.Id,
                definition.Name ?? $"Level {definition.Id}",
                definition.Width,
                definition.Height,
                cells,
                new HashSet<int>(),
                []);
        }

        layouts.TryAdd(0, LevelLayout.Empty);
    }

    private sealed class LevelLayoutDocument
    {
        public List<LevelLayoutDefinition> Levels { get; init; } = [];
    }

    private sealed class LevelLayoutDefinition
    {
        public uint Id { get; init; }
        public string? Name { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public List<int> Blocked { get; init; } = [];
    }
}

public sealed record LevelLayout(
    uint Id,
    string Name,
    int Width,
    int Height,
    IReadOnlySet<int> Blocked,
    IReadOnlySet<int> MissileBlocked,
    IReadOnlyList<uint> NativeTransparency)
{
    /** Raw server-authored d-piece IDs used by the diagnostic geometry overlay. */
    public IReadOnlyList<uint> NativePieceLayer { get; init; } = [];

    /** Raw server-authored native object IDs used by the diagnostic overlay. */
    public IReadOnlyList<uint> NativeObjectLayer { get; init; } = [];

    public static LevelLayout Empty { get; } = new(0, "Fallback", 32, 20, new HashSet<int>(), new HashSet<int>(), []);

    public bool IsBlocked(int x, int y)
    {
        return x < 0 || y < 0 || x >= Width || y >= Height || Blocked.Contains(y * Width + x);
    }

    public bool IsMissileBlocked(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height && MissileBlocked.Contains(y * Width + x);
    }
}
