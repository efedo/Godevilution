using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

/** The actual hero and run history sampled once before an unvisited floor is generated. */
public sealed record DiabloNativeFloorEntry(
    uint LevelId,
    AuthoritativeCathedralFreshLevelHeroContext Hero,
    IReadOnlyList<int> InitialUniqueItemFlags,
    string GenerationFingerprint)
{
    /** Catacombs only: the entering participant's life, an input of the native ready phase. */
    public int? CatacombsLifeQ6 { get; init; }

    /** L5 only: TownWarpDown when the floor was first entered by the Town warp; null is Main. */
    public AuthoritativeCatacombsEntry? CatacombsEntry { get; init; }
}

/** Explicit depth-run definition; all mutable owners remain local to a simulation. */
internal sealed class AuthoritativeCathedralNativeProgression
{
    private readonly Func<DiabloNativeFloorEntry, AuthoritativeCathedralNativePopulationBuild> build;
    private readonly Func<DiabloNativeFloorEntry, AuthoritativeCatacombsNativePopulationBuild>? buildCatacombs;
    private readonly AuthoritativePlayerAnimationCatalog animations;
    private readonly IReadOnlyDictionary<uint, AuthoritativeCathedralDoorDefinition>? doors;
    internal AuthoritativeCathedralNativeProgression(string fingerprint,
        AuthoritativePlayerAnimationCatalog animations,
        Func<DiabloNativeFloorEntry, AuthoritativeCathedralNativePopulationBuild> build,
        int maximumDepth = 2,
        int cohortFormatVersion = DiabloCohortState.NativeProgressionFormatVersion,
        IEnumerable<AuthoritativeCathedralDoorDefinition>? doors = null,
        Func<DiabloNativeFloorEntry, AuthoritativeCatacombsNativePopulationBuild>? buildCatacombs = null)
    {
        if (!AuthoritativeNativeMonsterInitialization.IsFingerprint(fingerprint))
            throw new InvalidDataException("Native progression requires its complete validated run identity.");
        var isLegacyProgression = maximumDepth == 2
            && cohortFormatVersion == DiabloCohortState.NativeProgressionFormatVersion
            && doors is null;
        var isDepthProgression = maximumDepth == AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth
            && cohortFormatVersion is DiabloCohortState.NativeDepthProgressionFormatVersion or DiabloCohortState.NativeTownProgressionFormatVersion
            && doors is not null;
        // Batch 2.2a: the Town run continues below L4 into the Catacombs.
        var isCatacombsProgression = maximumDepth == AuthoritativeCatacombsNativeDepths.LastDepth
            && cohortFormatVersion == DiabloCohortState.NativeCatacombsProgressionFormatVersion
            && doors is not null && buildCatacombs is not null;
        if (!isLegacyProgression && !isDepthProgression && !isCatacombsProgression
            || buildCatacombs is not null && !isCatacombsProgression)
            throw new InvalidDataException("Native progression requires a supported depth and cohort format.");
        Fingerprint = fingerprint;
        this.animations = animations;
        this.build = build;
        this.buildCatacombs = buildCatacombs;
        MaximumDepth = maximumDepth;
        CohortFormatVersion = cohortFormatVersion;
        if (!isLegacyProgression) {
            if (doors is null)
                throw new InvalidDataException("Native depth progression requires its complete ordered door mappings.");
            var cathedralDepth = Math.Min(maximumDepth, AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth);
            var definitions = doors.ToArray();
            if (definitions.Length != cathedralDepth
                || !definitions.Select(value => value.LevelId).SequenceEqual(
                    Enumerable.Range(1, cathedralDepth).Select(value => (uint)value)))
                throw new InvalidDataException("Native depth progression requires the exact ordered door definition for each supported floor.");
            this.doors = definitions.ToDictionary(value => value.LevelId);
        }
    }

    internal Func<DiabloContentActivation>? FreshActivation { get; init; }
    /** The trusted monster missile mappings Catacombs floors admit with. */
    internal AuthoritativeMonsterMissileCatalog? CatacombsMissiles { get; init; }
    internal string Fingerprint { get; }
    internal int MaximumDepth { get; }
    internal int CohortFormatVersion { get; }
    internal bool Supports(uint levelId) => levelId > 1 && levelId <= MaximumDepth;
    internal bool IsCatacombs(uint levelId) => buildCatacombs is not null && AuthoritativeCatacombsNativeDepths.Owns(levelId);

    internal AuthoritativeCathedralDoorDefinition GetDoor(uint levelId,
        AuthoritativeCathedralDoorCatalog initialDoors)
    {
        if (!Supports(levelId) || IsCatacombs(levelId))
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
        if (IsCatacombs(levelId))
            throw new InvalidDataException("A Catacombs floor entry uses its own native owner.");
        var entry = SampleEntry(levelId, player, uniqueFlags);
        var prepared = build(entry);
        return (entry with { GenerationFingerprint = prepared.Policy.Fingerprint }, prepared);
    }

    internal (DiabloNativeFloorEntry Entry, AuthoritativeCatacombsNativePopulationBuild Build) PrepareCatacombs(
        uint levelId, AuthoritativePlayerState player, IReadOnlySet<int> uniqueFlags,
        AuthoritativeCatacombsEntry? firstEntry = null)
    {
        if (!IsCatacombs(levelId) || firstEntry is not (null or AuthoritativeCatacombsEntry.TownWarpDown))
            throw new InvalidDataException("A Catacombs floor entry requires a Catacombs depth and stair or Town warp.");
        var townWarp = firstEntry == AuthoritativeCatacombsEntry.TownWarpDown;
        if (townWarp && (levelId != AuthoritativeCatacombsNativeDepths.FirstDepth || player.LevelId != 0))
            throw new InvalidDataException("The native Town warp enters only L5 from Town.");
        var entry = SampleEntry(levelId, player, uniqueFlags, townWarp) with {
            CatacombsLifeQ6 = player.Life, CatacombsEntry = firstEntry,
        };
        var prepared = buildCatacombs!(entry);
        return (entry with { GenerationFingerprint = prepared.Policy.Fingerprint }, prepared);
    }

    private DiabloNativeFloorEntry SampleEntry(uint levelId, AuthoritativePlayerState player, IReadOnlySet<int> uniqueFlags,
        bool townWarp = false)
    {
        if (!Supports(levelId) || !townWarp && player.LevelId + 1 != levelId || player.Life <= 0)
            throw new InvalidDataException("Native floor entry requires a living participant on the preceding floor's stair.");
        return new DiabloNativeFloorEntry(levelId,
            new(player.BaseLifeMaximum, player.BaseManaMaximum, player.LightRadius,
                animations.GetRequired(player.CharacterClassId).IdleFrames,
                new(player.FuturePositionX, player.FuturePositionY), true, true),
            uniqueFlags.Order().ToArray(), string.Empty);
    }

    internal AuthoritativeCathedralNativePopulationBuild Restore(DiabloNativeFloorEntry entry)
    {
        ValidateRetainedEntry(entry, catacombs: false);
        var generated = build(entry);
        if (entry.GenerationFingerprint != generated.Policy.Fingerprint)
            throw new InvalidDataException("A retained native floor changed its first-entry generation identity.");
        return generated;
    }

    internal AuthoritativeCatacombsNativePopulationBuild RestoreCatacombs(DiabloNativeFloorEntry entry)
    {
        ValidateRetainedEntry(entry, catacombs: true);
        var generated = buildCatacombs!(entry);
        if (entry.GenerationFingerprint != generated.Policy.Fingerprint)
            throw new InvalidDataException("A retained native floor changed its first-entry generation identity.");
        return generated;
    }

    private void ValidateRetainedEntry(DiabloNativeFloorEntry entry, bool catacombs)
    {
        if (!Supports(entry.LevelId) || IsCatacombs(entry.LevelId) != catacombs
            || (entry.CatacombsLifeQ6 is > 0) != catacombs || entry.Hero is null || entry.InitialUniqueItemFlags is null
            || entry.CatacombsEntry is not (null or AuthoritativeCatacombsEntry.TownWarpDown)
            || entry.CatacombsEntry is not null && entry.LevelId != AuthoritativeCatacombsNativeDepths.FirstDepth
            || entry.InitialUniqueItemFlags.Any(flag => flag is < 0 or >= 128)
            || !entry.InitialUniqueItemFlags.SequenceEqual(entry.InitialUniqueItemFlags.Distinct().Order()))
            throw new InvalidDataException("A retained native floor has an invalid first-entry context.");
    }

    internal static string ComputeFingerprint(object identity)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
}

/** Trusted owners rematerialized from validated content and saved entry contexts before restoring mutable state. */
internal sealed record NativeProgressionRestoreContext(
    IReadOnlyList<AuthoritativeCathedralNativePopulationPolicy> Owners,
    AuthoritativeCathedralItemRuntimeProfile Items,
    AuthoritativeCathedralDoorCatalog Doors,
    DiabloWorldGeometryState BackingGeometry)
{
    /** Catacombs floors regenerated from their saved first-entry contexts (format 12). */
    internal IReadOnlyList<AuthoritativeCatacombsNativePopulationBuild> CatacombsFloors { get; init; } = [];
}
