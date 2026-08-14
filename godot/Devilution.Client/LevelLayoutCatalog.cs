using Devilution.Protocol.V1;
using System.Text.Json;

namespace Devilution.Client;

/** Loads presentation-only level geometry from replaceable content assets. */
public sealed class LevelLayoutCatalog
{
    private readonly Dictionary<uint, LevelLayout> layouts = [];

    public LevelLayoutCatalog()
    {
        Load();
    }

    public LevelLayout Resolve(uint levelId)
    {
        if (layouts.TryGetValue(levelId, out var layout))
            return layout;
        return layouts.TryGetValue(0, out var fallback) ? fallback : LevelLayout.Empty;
    }

    /** Replaces presentation geometry with the active server's validated projection. */
    public void ApplyAuthoritative(IEnumerable<LevelGeometrySnapshot> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions) {
            if (definition.Width <= 0 || definition.Height <= 0)
                continue;
            var cells = definition.BlockedCells
                .Where(cell => cell >= 0 && cell < definition.Width * definition.Height)
                .ToHashSet();
            layouts[definition.LevelId] = new LevelLayout(
                definition.LevelId,
                $"Level {definition.LevelId}",
                definition.Width,
                definition.Height,
                cells);
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
            layouts[definition.Id] = new LevelLayout(definition.Id, definition.Name ?? $"Level {definition.Id}", definition.Width, definition.Height, cells);
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

public sealed record LevelLayout(uint Id, string Name, int Width, int Height, IReadOnlySet<int> Blocked)
{
    public static LevelLayout Empty { get; } = new(0, "Fallback", 32, 20, new HashSet<int>());

    public bool IsBlocked(int x, int y)
    {
        return x < 0 || y < 0 || x >= Width || y >= Height || Blocked.Contains(y * Width + x);
    }
}
