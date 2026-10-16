using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

/** The actual hero and run history sampled once before an unvisited floor is generated. */
public sealed record DiabloNativeFloorEntry(
    uint LevelId,
    AuthoritativeCathedralFreshLevelHeroContext Hero,
    IReadOnlyList<int> InitialUniqueItemFlags,
    string GenerationFingerprint);

/** Explicit two-floor run definition; all mutable owners remain local to a simulation. */
internal sealed class AuthoritativeCathedralNativeProgression
{
    private readonly Func<DiabloNativeFloorEntry, AuthoritativeCathedralNativePopulationBuild> build;
    private readonly AuthoritativePlayerAnimationCatalog animations;
    internal AuthoritativeCathedralNativeProgression(string fingerprint,
        AuthoritativePlayerAnimationCatalog animations,
        Func<DiabloNativeFloorEntry, AuthoritativeCathedralNativePopulationBuild> build)
    {
        if (!AuthoritativeNativeMonsterInitialization.IsFingerprint(fingerprint))
            throw new InvalidDataException("Native progression requires its complete validated run identity.");
        Fingerprint = fingerprint;
        this.animations = animations;
        this.build = build;
    }

    internal Func<DiabloContentActivation>? FreshActivation { get; init; }
    internal string Fingerprint { get; }
    internal bool Supports(uint levelId) => levelId == 2;

    internal (DiabloNativeFloorEntry Entry, AuthoritativeCathedralNativePopulationBuild Build) Prepare(
        uint levelId, AuthoritativePlayerState player, IReadOnlySet<int> uniqueFlags)
    {
        if (!Supports(levelId) || player.LevelId != 1 || player.Life <= 0)
            throw new InvalidDataException("First native L2 entry requires a living participant on its L1 stair.");
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
