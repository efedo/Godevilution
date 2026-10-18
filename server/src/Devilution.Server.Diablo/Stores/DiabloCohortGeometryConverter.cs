using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Diablo;

namespace Devilution.Server.Stores;

/** Bounded serializer cache for immutable-by-value native world geometry levels. */
internal sealed class DiabloCohortGeometryConverter : JsonConverter<DiabloWorldGeometryLevelState>
{
    private const uint MaximumCachedLevelId = 4;
    private const int VariantsPerLevel = 2;

    private readonly JsonSerializerOptions uncachedOptions;
    private readonly object cacheLock = new();
    private readonly Dictionary<uint, List<CacheEntry>> cache = new();
    private long useSequence;

    internal DiabloCohortGeometryConverter(JsonSerializerOptions uncachedOptions)
    {
        this.uncachedOptions = uncachedOptions ?? throw new ArgumentNullException(nameof(uncachedOptions));
    }

    public override DiabloWorldGeometryLevelState Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) => JsonSerializer.Deserialize<DiabloWorldGeometryLevelState>(ref reader,
            uncachedOptions) ?? throw new JsonException("A world-geometry level must be an object.");

    public override void Write(Utf8JsonWriter writer, DiabloWorldGeometryLevelState value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.LevelId > MaximumCachedLevelId) {
            JsonSerializer.Serialize(writer, value, uncachedOptions);
            return;
        }

        // Always detach first: the input DTO and its nested collections may still be mutated by its owner.
        var snapshot = Detach(value);
        byte[]? payload = null;
        lock (cacheLock) {
            if (cache.TryGetValue(snapshot.LevelId, out var variants)) {
                var hit = variants.FirstOrDefault(entry => HasSameValue(entry.Snapshot, snapshot));
                if (hit is not null) {
                    hit.LastUse = NextUseSequence();
                    payload = hit.Payload;
                }
            }
        }

        if (payload is null) {
            payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, uncachedOptions);
            lock (cacheLock) {
                if (!cache.TryGetValue(snapshot.LevelId, out var variants)) {
                    variants = new List<CacheEntry>(VariantsPerLevel);
                    cache.Add(snapshot.LevelId, variants);
                }

                // Another writer may have populated the same immutable value while this miss serialized.
                var concurrentHit = variants.FirstOrDefault(entry => HasSameValue(entry.Snapshot, snapshot));
                if (concurrentHit is not null) {
                    concurrentHit.LastUse = NextUseSequence();
                    payload = concurrentHit.Payload;
                } else {
                    if (variants.Count == VariantsPerLevel) {
                        var leastRecentlyUsed = 0;
                        for (var index = 1; index < variants.Count; index++) {
                            if (variants[index].LastUse < variants[leastRecentlyUsed].LastUse)
                                leastRecentlyUsed = index;
                        }
                        variants.RemoveAt(leastRecentlyUsed);
                    }
                    variants.Add(new CacheEntry(snapshot, payload, NextUseSequence()));
                }
            }
        }

        writer.WriteRawValue(payload, skipInputValidation: true);
    }

    private static DiabloWorldGeometryLevelState Detach(DiabloWorldGeometryLevelState source) => new(
        source.LevelId,
        source.StableSymbol,
        source.Width,
        source.Height,
        Copy(source.BlockedCells),
        Copy(source.MissileBlockedCells),
        Copy(source.NativeTransparency),
        Copy(source.NativeObjectLayer),
        Copy(source.NativeMegaTileLayer),
        Copy(source.NativePieceLayer),
        CopyPieceProperties(source.NativePieceProperties),
        Copy(source.NativePieceCollisionCells),
        Copy(source.NativeVisionBlockedCells),
        source.HasExplicitNativeVisionBlockedCells,
        Copy(source.NativePieceMissileBlockedCells),
        Copy(source.NativeMonsterLayer),
        source.IsDefaultStart,
        source.SuppressMultiplayerDeathDrops,
        source.StartPositionX,
        source.StartPositionY);

    private static IReadOnlyList<T>? Copy<T>(IReadOnlyList<T>? source) => source?.ToArray();

    private static IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? CopyPieceProperties(
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? source) => source is null
        ? null
        : new DetachedPieceProperties(source);

    private static bool HasSameValue(DiabloWorldGeometryLevelState left, DiabloWorldGeometryLevelState right) =>
        left.LevelId == right.LevelId
        && string.Equals(left.StableSymbol, right.StableSymbol, StringComparison.Ordinal)
        && left.Width == right.Width
        && left.Height == right.Height
        && SameSequence(left.BlockedCells, right.BlockedCells)
        && SameSequence(left.MissileBlockedCells, right.MissileBlockedCells)
        && SameSequence(left.NativeTransparency, right.NativeTransparency)
        && SameSequence(left.NativeObjectLayer, right.NativeObjectLayer)
        && SameSequence(left.NativeMegaTileLayer, right.NativeMegaTileLayer)
        && SameSequence(left.NativePieceLayer, right.NativePieceLayer)
        && SamePieceProperties(left.NativePieceProperties, right.NativePieceProperties)
        && SameSequence(left.NativePieceCollisionCells, right.NativePieceCollisionCells)
        && SameSequence(left.NativeVisionBlockedCells, right.NativeVisionBlockedCells)
        && left.HasExplicitNativeVisionBlockedCells == right.HasExplicitNativeVisionBlockedCells
        && SameSequence(left.NativePieceMissileBlockedCells, right.NativePieceMissileBlockedCells)
        && SameSequence(left.NativeMonsterLayer, right.NativeMonsterLayer)
        && left.IsDefaultStart == right.IsDefaultStart
        && left.SuppressMultiplayerDeathDrops == right.SuppressMultiplayerDeathDrops
        && left.StartPositionX == right.StartPositionX
        && left.StartPositionY == right.StartPositionY;

    private static bool SameSequence<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++) {
            if (!EqualityComparer<T>.Default.Equals(left[index], right[index]))
                return false;
        }
        return true;
    }

    private static bool SamePieceProperties(
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? left,
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        using var leftEnumerator = left.GetEnumerator();
        using var rightEnumerator = right.GetEnumerator();
        while (leftEnumerator.MoveNext()) {
            if (!rightEnumerator.MoveNext() || leftEnumerator.Current.Key != rightEnumerator.Current.Key
                || leftEnumerator.Current.Value != rightEnumerator.Current.Value)
                return false;
        }
        return !rightEnumerator.MoveNext();
    }

    private long NextUseSequence() => unchecked(++useSequence);

    private sealed class CacheEntry(DiabloWorldGeometryLevelState snapshot, byte[] payload, long lastUse)
    {
        public DiabloWorldGeometryLevelState Snapshot { get; } = snapshot;
        public byte[] Payload { get; } = payload;
        public long LastUse { get; set; } = lastUse;
    }

    /** Keeps dictionary enumeration order detached and stable along with key lookup. */
    private sealed class DetachedPieceProperties : IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>
    {
        private readonly KeyValuePair<ushort, AuthoritativeDunTileFlags>[] entries;
        private readonly Dictionary<ushort, AuthoritativeDunTileFlags> lookup;

        public DetachedPieceProperties(IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags> source)
        {
            entries = source.ToArray();
            lookup = entries.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public int Count => entries.Length;
        public IEnumerable<ushort> Keys => EnumerateKeys();
        public IEnumerable<AuthoritativeDunTileFlags> Values => EnumerateValues();
        public AuthoritativeDunTileFlags this[ushort key] => lookup[key];

        public bool ContainsKey(ushort key) => lookup.ContainsKey(key);
        public bool TryGetValue(ushort key, out AuthoritativeDunTileFlags value) => lookup.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<ushort, AuthoritativeDunTileFlags>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<ushort, AuthoritativeDunTileFlags>>)entries).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<ushort> EnumerateKeys()
        {
            foreach (var entry in entries)
                yield return entry.Key;
        }

        private IEnumerable<AuthoritativeDunTileFlags> EnumerateValues()
        {
            foreach (var entry in entries)
                yield return entry.Value;
        }
    }
}