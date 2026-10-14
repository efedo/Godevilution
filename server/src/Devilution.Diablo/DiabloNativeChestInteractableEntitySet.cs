using System.Collections;

namespace Devilution.Diablo;

/** Immutable value set so transient snapshot capabilities do not alter record equality. */
public sealed class DiabloNativeChestInteractableEntitySet : IReadOnlySet<uint>, IEquatable<DiabloNativeChestInteractableEntitySet>
{
    private readonly HashSet<uint> entityIds;

    private DiabloNativeChestInteractableEntitySet(IEnumerable<uint> entityIds)
        => this.entityIds = entityIds.ToHashSet();

    public static DiabloNativeChestInteractableEntitySet Empty { get; } = new(Array.Empty<uint>());

    internal static DiabloNativeChestInteractableEntitySet Create(IEnumerable<uint> entityIds)
    {
        ArgumentNullException.ThrowIfNull(entityIds);
        var result = new DiabloNativeChestInteractableEntitySet(entityIds);
        return result.Count == 0 ? Empty : result;
    }

    public int Count => entityIds.Count;

    public bool Contains(uint item) => entityIds.Contains(item);

    public bool IsProperSubsetOf(IEnumerable<uint> other) => entityIds.IsProperSubsetOf(other);

    public bool IsProperSupersetOf(IEnumerable<uint> other) => entityIds.IsProperSupersetOf(other);

    public bool IsSubsetOf(IEnumerable<uint> other) => entityIds.IsSubsetOf(other);

    public bool IsSupersetOf(IEnumerable<uint> other) => entityIds.IsSupersetOf(other);

    public bool Overlaps(IEnumerable<uint> other) => entityIds.Overlaps(other);

    public bool SetEquals(IEnumerable<uint> other) => entityIds.SetEquals(other);

    public bool Equals(DiabloNativeChestInteractableEntitySet? other)
        => other is not null && entityIds.SetEquals(other.entityIds);

    public override bool Equals(object? obj) => obj is DiabloNativeChestInteractableEntitySet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var entityId in entityIds)
            hash ^= entityId.GetHashCode();
        return HashCode.Combine(entityIds.Count, hash);
    }

    public IEnumerator<uint> GetEnumerator() => entityIds.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
