using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Replaceable server-owned level geometry retained by the presentation client. */
public sealed class AuthoritativeLevelGeometryState
{
    private readonly Dictionary<uint, LevelGeometrySnapshot> definitions = [];

    public IReadOnlyDictionary<uint, LevelGeometrySnapshot> Definitions => definitions;

    /** Replaces the prior server projection; an empty projection clears it. */
    public void Replace(IEnumerable<LevelGeometrySnapshot> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        definitions.Clear();
        foreach (var definition in next) {
            if (definition.Width <= 0 || definition.Height <= 0)
                continue;
            definitions[definition.LevelId] = definition;
        }
    }
}
