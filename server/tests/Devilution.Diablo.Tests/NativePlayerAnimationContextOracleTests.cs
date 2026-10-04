using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Actual native metadata outputs are comparison-only, never managed continuation inputs. */
public sealed class NativePlayerAnimationContextOracleTests
{
    private const string InputSha256 = "BC39F24FB5C419DA473182A1DECA4334B779FFA935BFEEFCB2D1B18F8DAC01FE";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    [Fact(Explicit = true)]
    public void MatchesCompleteNativeContextCorpus()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_PLAYER_ANIMATION_CONTEXT_ORACLE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory));
        var path = Path.Combine(directory!, "player-animation-context.json");
        Assert.InRange(new FileInfo(path).Length, 1L, 1_000_000L);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        RequireFields(root, "FormatVersion", "InputFingerprint", "Inputs", "Expected");
        Assert.Equal(1, root.GetProperty("FormatVersion").GetInt32());
        var inputs = root.GetProperty("Inputs");
        RequireFields(inputs, "Classes", "Scenarios", "SpellInputs");
        // Independently frozen pre-call transcript; a self-supplied hash is insufficient.
        Assert.Equal(InputSha256, Fingerprint(inputs));
        Assert.Equal(InputSha256, root.GetProperty("InputFingerprint").GetString());
        var classInputs = inputs.GetProperty("Classes").EnumerateArray().ToArray();
        Assert.Equal(6, classInputs.Length);
        var classCatalog = LoadClasses();
        var animations = new AuthoritativePlayerAnimationCatalog(classInputs.Select((row, index) => {
            RequireFields(row, "Class", "Base", "DungeonBowIdleFrames", "ArmoredDeathFrames");
            Assert.Equal(index, row.GetProperty("Class").GetInt32());
            var values = IntArray(row.GetProperty("Base"), 27);
            return Definition((uint)index, values);
        }), classCatalog);
        var levels = new[] {
            new AuthoritativePlayerAnimationLevelDefinition(0, "native.town", true),
            new AuthoritativePlayerAnimationLevelDefinition(1, "native.dungeon", false),
        };
        var overrides = classInputs.SelectMany(row => levels.Select(level =>
            new AuthoritativePlayerAnimationClassContextDefinition(
                row.GetProperty("Class").GetUInt32(), level.LevelId,
                row.GetProperty("DungeonBowIdleFrames").GetInt32(),
                row.GetProperty("ArmoredDeathFrames").GetInt32(), "native.source-policy"))).ToArray();
        var spellInputs = inputs.GetProperty("SpellInputs").EnumerateArray().ToArray();
        Assert.Equal(89, spellInputs.Length);
        foreach (var row in spellInputs)
            RequireFields(row, "Variant", "Spell", "MagicType");
        var catalogs = new AuthoritativePlayerAnimationContextCatalog[2];
        for (var variant = 0; variant < 2; variant++) {
            var rows = spellInputs.Where(row => row.GetProperty("Variant").GetInt32() == variant).ToArray();
            Assert.Equal(variant == 0 ? 37 : 52, rows.Length);
            var spellRows = rows.Select((row, index) => {
                Assert.Equal(index, row.GetProperty("Spell").GetInt32());
                Assert.InRange(row.GetProperty("MagicType").GetInt32(), 0, 2);
                return new AuthoritativePlayerAnimationSpellDefinition(
                    (uint)(1000 + index), (uint)index,
                    (DiabloPlayerAnimationMagicType)row.GetProperty("MagicType").GetInt32());
            }).ToArray();
            var spells = new AuthoritativeSpellCatalog(spellRows.Select(row =>
                new AuthoritativeSpellDefinition(row.SpellId, 1, 0, 0, 0, 0) { NativeSpellId = row.NativeSpellId }));
            catalogs[variant] = new(animations, classCatalog, spells, levels, overrides, spellRows);
        }
        var expected = root.GetProperty("Expected");
        RequireFields(expected, "Scenarios", "SpellGraphics");
        var scenarios = inputs.GetProperty("Scenarios").EnumerateArray().ToArray();
        var outputs = expected.GetProperty("Scenarios").EnumerateArray().ToArray();
        Assert.Equal(648, scenarios.Length);
        Assert.Equal(648, outputs.Length);
        var transitions = 0;
        for (var index = 0; index < scenarios.Length; index++) {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var scenario = scenarios[index];
            RequireFields(scenario, "Class", "WeaponGraphic", "ArmorGraphic", "Before", "TownSequence");
            var current = FrameSet(IntArray(scenario.GetProperty("Before"), 9));
            var towns = scenario.GetProperty("TownSequence").EnumerateArray().ToArray();
            var steps = outputs[index].EnumerateArray().ToArray();
            Assert.Equal(3, towns.Length);
            Assert.Equal(3, steps.Length);
            foreach (var (step, offset) in steps.Select((value, offset) => (value, offset))) {
                RequireFields(step, "After", "Timings");
                var resolution = catalogs[0].ResolveNative(scenario.GetProperty("Class").GetUInt32(),
                    towns[offset].GetBoolean() ? 0U : 1U,
                    (DiabloPlayerAttackAnimationProfile)scenario.GetProperty("WeaponGraphic").GetInt32(),
                    (DiabloPlayerArmorGraphic)scenario.GetProperty("ArmorGraphic").GetInt32());
                current = resolution.ApplyTo(current);
                Assert.Equal(FrameSet(IntArray(step.GetProperty("After"), 9)), current);
                var timings = step.GetProperty("Timings").EnumerateArray().ToArray();
                Assert.Equal(9, timings.Length);
                for (var graphic = 0; graphic < 9; graphic++) {
                    var pair = IntArray(timings[graphic], 2);
                    Assert.Equal(new AuthoritativePlayerAnimationTimingSample(pair[0], pair[1]),
                        AuthoritativePlayerAnimationContextCatalog.GetTiming(current, graphic));
                }
                transitions++;
            }
        }
        Assert.Equal(1944, transitions);
        var graphicOutputs = IntArray(expected.GetProperty("SpellGraphics"), 89);
        for (var index = 0; index < spellInputs.Length; index++) {
            var row = spellInputs[index];
            var catalog = catalogs[row.GetProperty("Variant").GetInt32()];
            var nativeId = row.GetProperty("Spell").GetUInt32();
            var school = catalog.ResolveNativeSpell(nativeId);
            Assert.Equal(school, catalog.ResolveSpell(nativeId + 1000));
            var graphic = school switch {
                DiabloPlayerAnimationMagicType.Fire => 5,
                DiabloPlayerAnimationMagicType.Lightning => 4,
                DiabloPlayerAnimationMagicType.Magic => 6,
                _ => throw new InvalidDataException("Unknown native MagicType."),
            };
            Assert.Equal(graphicOutputs[index], graphic);
        }
    }

    [Fact]
    public void InputFingerprintBindsInputsButNotExpectedOutputs()
    {
        using var original = JsonDocument.Parse("{\"Inputs\":{\"Before\":[0]},\"Expected\":1}");
        using var outputChanged = JsonDocument.Parse("{\"Inputs\":{\"Before\":[0]},\"Expected\":2}");
        using var inputChanged = JsonDocument.Parse("{\"Inputs\":{\"Before\":[1]},\"Expected\":1}");
        Assert.Equal(Fingerprint(original.RootElement.GetProperty("Inputs")), Fingerprint(outputChanged.RootElement.GetProperty("Inputs")));
        Assert.NotEqual(Fingerprint(original.RootElement.GetProperty("Inputs")), Fingerprint(inputChanged.RootElement.GetProperty("Inputs")));
    }

    [Fact]
    public void EvidenceShapeRejectsMissingExtraAndDuplicateFields()
    {
        foreach (var json in new[] { "{}", "{\"After\":[],\"Extra\":0}", "{\"After\":[],\"After\":[]}", "[]" }) {
            using var document = JsonDocument.Parse(json);
            Assert.Throws<InvalidDataException>(() => RequireFields(document.RootElement, "After"));
        }
        using var valid = JsonDocument.Parse("{\"After\":[]}");
        RequireFields(valid.RootElement, "After");
    }

    private static string Fingerprint(JsonElement inputs)
        => Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(inputs.GetRawText())));

    private static void RequireFields(JsonElement value, params string[] required)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Oracle records must be objects.");
        var names = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (names.Length != required.Length || names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || !names.Order(StringComparer.Ordinal).SequenceEqual(required.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Missing, extra or duplicate oracle fields.");
    }

    private static int[] IntArray(JsonElement value, int count)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        var values = value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
        Assert.Equal(count, values.Length);
        return values;
    }

    private static AuthoritativePlayerAnimationFrameSet FrameSet(int[] values)
        => new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);

    private static AuthoritativePlayerAnimationDefinition Definition(uint id, int[] values)
        => new(id, new(values[0], values[1]), new(values[2], values[3]), new(values[4], values[5]),
            new(values[6], values[7]), new(values[8], values[9]), new(values[10], values[11]),
            new(values[12], values[13]), new(values[14], values[15]), new(values[16], values[17]),
            values[18], values[19], values[20], values[21], values[22], values[23], values[24], values[25], values[26]);

    private static AuthoritativePlayerClassCatalog LoadClasses()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                var path = Path.Combine(directory.FullName, "server/content/base/classes.tsv");
                if (File.Exists(path))
                    return AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", File.ReadAllText(path));
            }
        }
        throw new DirectoryNotFoundException("The explicit repository class metadata is missing.");
    }
}
