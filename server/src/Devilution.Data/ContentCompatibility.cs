using System.Globalization;

namespace Devilution.Data;

/** Three-component version used by content schema and dependency contracts. */
public readonly record struct ContentSemanticVersion(
    uint Major,
    uint Minor,
    uint Patch) : IComparable<ContentSemanticVersion>
{
    public static ContentSemanticVersion Parse(string value)
    {
        if (!TryParse(value, out var result)) {
            throw new ArgumentException(
                $"Content version '{value}' must contain three unsigned integers.",
                nameof(value));
        }
        return result;
    }

    public static bool TryParse(string? value, out ContentSemanticVersion result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var components = value.Split('.', StringSplitOptions.None);
        if (components.Length != 3
            || !uint.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !uint.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !uint.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)) {
            return false;
        }
        result = new ContentSemanticVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(ContentSemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/** Versioned table-schema family targeted by a content pack. */
public sealed record ContentSchemaIdentity
{
    public ContentSchemaIdentity(string id, ContentSemanticVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Version = version;
    }

    public ContentSchemaIdentity(string id, string version)
        : this(id, ContentSemanticVersion.Parse(version))
    {
    }

    public string Id { get; }

    public ContentSemanticVersion Version { get; }

    public override string ToString() => $"{Id}@{Version}";
}

/** Inclusive semantic-version range required from an earlier content pack. */
public sealed record ContentPackDependencyConstraint
{
    public ContentPackDependencyConstraint(
        string dependencyId,
        ContentSemanticVersion minimum,
        ContentSemanticVersion maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyId);
        if (minimum.CompareTo(maximum) > 0) {
            throw new ArgumentException(
                "The minimum dependency version cannot exceed the maximum.");
        }
        DependencyId = dependencyId;
        Minimum = minimum;
        Maximum = maximum;
    }

    public ContentPackDependencyConstraint(
        string dependencyId,
        string minimum,
        string maximum)
        : this(
            dependencyId,
            ContentSemanticVersion.Parse(minimum),
            ContentSemanticVersion.Parse(maximum))
    {
    }

    public string DependencyId { get; }

    public ContentSemanticVersion Minimum { get; }

    public ContentSemanticVersion Maximum { get; }

    public bool Supports(ContentSemanticVersion version)
    {
        return version.CompareTo(Minimum) >= 0 && version.CompareTo(Maximum) <= 0;
    }
}
