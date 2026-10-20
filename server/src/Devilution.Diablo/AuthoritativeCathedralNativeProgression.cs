using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

/** The actual hero and run history sampled once before an unvisited floor is generated. */
public sealed record DiabloNativeFloorEntry(
    uint LevelId,
    AuthoritativeCathedralFreshLevelHeroContext Hero,
    IReadOnlyList<int> InitialUniqueItemFlags,
    string GenerationFingerprint);

/** Explicit depth-run definition; all mutable owners remain local to a simulation. */
internal sealed class AuthoritativeCathedralNativeProgression
{
    private readonly Func<DiabloNativeFloorEntry, AuthoritativeCathedralNativePopulationBuild> build;
    private readonly AuthoritativePlayerAnimationCatalog animations;
    private readonly IReadOnlyDictionary<uint, AuthoritativeCathedralDoorDefinition>? doors;
    internal AuthoritativeCathedralNativeProgression(string fingerprint,
        AuthoritativePlayerAnimationCatalog animations,
        Func<DiabloNativeFloorEntry, AuthoritativeCathedralNativePopulationBuild> build,
        int maximumDepth = 2,
        int cohortFormatVersion = DiabloCohortState.NativeProgressionFormatVersion,
        IEnumerable<AuthoritativeCathedralDoorDefinition>? doors = null)
    {
        if (!AuthoritativeNativeMonsterInitialization.IsFingerprint(fingerprint))
            throw new InvalidDataException("Native progression requires its complete validated run identity.");
        var isLegacyProgression = maximumDepth == 2
            && cohortFormatVersion == DiabloCohortState.NativeProgressionFormatVersion
            && doors is null;
        var isDepthProgression = maximumDepth == AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth
            && cohortFormatVersion is DiabloCohortState.NativeDepthProgressionFormatVersion or DiabloCohortState.NativeTownProgressionFormatVersion
            && doors is not null;
        if (!isLegacyProgression && !isDepthProgression)
            throw new InvalidDataException("Native progression requires a supported depth and cohort format.");
        Fingerprint = fingerprint;
        this.animations = animations;
        this.build = build;
        MaximumDepth = maximumDepth;
        CohortFormatVersion = cohortFormatVersion;
        if (cohortFormatVersion is DiabloCohortState.NativeDepthProgressionFormatVersion or DiabloCohortState.NativeTownProgressionFormatVersion) {
            if (doors is null)
                throw new InvalidDataException("Native depth progression requires its complete ordered door mappings.");
            var definitions = doors.ToArray();
            if (definitions.Length != maximumDepth
                || !definitions.Select(value => value.LevelId).SequenceEqual(
                    Enumerable.Range(1, maximumDepth).Select(value => (uint)value)))
                throw new InvalidDataException("Native depth progression requires the exact ordered door definition for each supported floor.");
            this.doors = definitions.ToDictionary(value => value.LevelId);
        }
    }

    internal Func<DiabloContentActivation>? FreshActivation { get; init; }
    internal string Fingerprint { get; }
    internal int MaximumDepth { get; }
    internal int CohortFormatVersion { get; }
    internal bool Supports(uint levelId) => levelId > 1 && levelId <= MaximumDepth;

    internal AuthoritativeCathedralDoorDefinition GetDoor(uint levelId,
        AuthoritativeCathedralDoorCatalog initialDoors)
    {
        if (!Supports(levelId))
            throw new InvalidDataException("A native progression door belongs to an unsupported floor.");
        if (doors is not null) {
            if (!doors.TryGetValue(levelId, out var definition))
                throw new InvalidDataException("A native progression floor has no immutable door mapping.");
            return definition;
        }
        if (levelId != 2 || initialDoors.Definitions.Count != 1 || initialDoors.Definitions[0].LevelId != 1)
            throw new InvalidDataException("Legacy native progression can synthesize only its original L2 door mapping.");
        return initialDoors.Definitions[0] with { LevelId = levelId };
    }

    internal (DiabloNativeFloorEntry Entry, AuthoritativeCathedralNativePopulationBuild Build) Prepare(
        uint levelId, AuthoritativePlayerState player, IReadOnlySet<int> uniqueFlags)
    {
        if (!Supports(levelId) || player.LevelId + 1 != levelId || player.Life <= 0)
            throw new InvalidDataException("Native floor entry requires a living participant on the preceding floor's stair.");
        var entry = new DiabloNativeFloorEntry(levelId,
            new(player.BaseLifeMaximum, player.BaseManaMaximum, player.LightRadius,
                animations.GetRequired(player.CharacterClassId).IdleFrames,
                new(player.FuturePositionX, player.FuturePositionY), true, true),
            uniqueFlags.Order().ToArray(), string.Empty);
        var prepared = build(entry);
        return (entry with { GenerationFingerprint = prepared.Policy.Fingerprint }, prepared);
    }

    internal AuthoritativeCathedralNativePopulationBuild Restore(DiabloNativeFloorEntry entry)
    {
        if (!Supports(entry.LevelId) || entry.Hero is null || entry.InitialUniqueItemFlags is null
            || entry.InitialUniqueItemFlags.Any(flag => flag is < 0 or >= 128)
            || !entry.InitialUniqueItemFlags.SequenceEqual(entry.InitialUniqueItemFlags.Distinct().Order()))
            throw new InvalidDataException("A retained native floor has an invalid first-entry context.");
        var generated = build(entry);
        if (entry.GenerationFingerprint != generated.Policy.Fingerprint)
            throw new InvalidDataException("A retained native floor changed its first-entry generation identity.");
        return generated;
    }

    internal static string ComputeFingerprint(object identity)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
}

/** Trusted owners rematerialized from validated content and saved entry contexts before restoring mutable state. */
internal sealed record NativeProgressionRestoreContext(
    IReadOnlyList<AuthoritativeCathedralNativePopulationPolicy> Owners,
    AuthoritativeCathedralItemRuntimeProfile Items,
    AuthoritativeCathedralDoorCatalog Doors,
    DiabloWorldGeometryState BackingGeometry);
