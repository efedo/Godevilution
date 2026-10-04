using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Domain;
using Xunit;

namespace Devilution.Domain.Tests;

/** Replays pre-call inputs; native scalar/render outputs are comparison-only. */
public sealed class NativeAnimationTimingOracleTests
{
    private const string InputTranscriptSha256 = "4CD29556122BFBB34D81964EB75CCBC6D4176E3FCC2F25CE4417E8022F7F7898";

    [Fact(Explicit = true)]
    public void MatchesCompleteNativeTimingCorpus()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_ANIMATION_TIMING_ORACLE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory));
        var path = Path.Combine(directory!, "animation-timing.json");
        Assert.InRange(new FileInfo(path).Length, 1L, 10_000_000L);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        RequireFields(root, "FormatVersion", "FractionBase", "Scenarios");
        Assert.Equal(1, root.GetProperty("FormatVersion").GetInt32());
        Assert.Equal(128, root.GetProperty("FractionBase").GetInt32());
        var expectedNames = new[] {
            "stand", "walk--2", "walk--1", "walk-2", "walk-3",
            "attack-0", "attack-1", "attack-2", "attack-3", "attack-4",
            "ranged-0", "ranged-1", "ranged-2", "cast", "block", "fast-block",
            "hit-0", "hit-1", "hit-2", "hit-3", "preview-1", "preview-64",
            "preview-127", "preview-128", "repeated-8", "repeated-9", "repeated-12",
            "repeated-15", "repeated-16", "reverse", "change-data", "petrified", "independent",
            "normalized-zero", "normalized-negative", "changed-rate-0", "changed-rate--1",
            "changed-empty", "carry-data-change", "different-count-repeat", "caller-death-hold",
            "signed-phase-wrap", "counter-boundary", "petrified-reset",
            "distributed-rate-0", "distributed-rate--1", "changed-negative--1",
            "changed-negative--2", "changed-negative--128", "negative-reverse-wrap",
        };
        var scenarios = root.GetProperty("Scenarios").EnumerateArray().ToArray();
        Assert.Equal(expectedNames, scenarios.Select(scenario => scenario.GetProperty("Name").GetString()));
        // Bind the complete pre-call transcript before evaluating native outputs.
        Assert.Equal(InputTranscriptSha256, InputFingerprint(scenarios));
        var stepCount = 0;
        foreach (var scenario in scenarios) {
            RequireFields(scenario, "Name", "Steps");
            var actors = new[] { NativeAnimationState.Empty, NativeAnimationState.Empty };
            foreach (var step in scenario.GetProperty("Steps").EnumerateArray()) {
                TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                stepCount++;
                RequireFields(step, "Kind", "Actor", "Frames", "Ticks", "Flags", "Skipped",
                    "Distribute", "Preview", "Value", "State", "RenderFrames", "ProgressResults");
                var actor = step.GetProperty("Actor").GetInt32();
                Assert.InRange(actor, 0, 1);
                var before = actors[actor];
                var frames = step.GetProperty("Frames").GetInt32();
                var ticks = step.GetProperty("Ticks").GetInt32();
                var kind = step.GetProperty("Kind").GetString();
                var current = kind switch {
                    "start" => before.SetNewAnimation(frames, ticks,
                        (NativeAnimationDistributionFlags)step.GetProperty("Flags").GetInt32(),
                        step.GetProperty("Skipped").GetInt32(),
                        step.GetProperty("Distribute").GetInt32(),
                        step.GetProperty("Preview").GetInt32()),
                    "advance" => before.ProcessAnimation(step.GetProperty("Value").GetBoolean()),
                    "change" => before.ChangeAnimationData(frames, ticks),
                    "petrify" => before.WithPetrified(step.GetProperty("Value").GetBoolean()),
                    "rate" => WithRate(before, ticks),
                    "hold" => before,
                    _ => throw new InvalidDataException($"Unknown native timing operation '{kind}'."),
                };
                actors[actor] = current;
                Assert.Equal(ReadState(step.GetProperty("State")), current);
                var rendered = step.GetProperty("RenderFrames").EnumerateArray().ToArray();
                Assert.Equal(129, rendered.Length);
                for (var fraction = 0; fraction < rendered.Length; fraction++)
                    Assert.Equal(rendered[fraction].GetInt32(), current.ResolveRenderFrame(fraction));
                var progress = step.GetProperty("ProgressResults").EnumerateArray().ToArray();
                Assert.Equal(129, progress.Length);
                for (var fraction = 0; fraction < progress.Length; fraction++) {
                    RequireFields(progress[fraction], "Refusal", "Value");
                    var refusal = ProgressRefusal(current, fraction);
                    Assert.Equal(refusal, progress[fraction].GetProperty("Refusal").GetInt32());
                    if (refusal == 0)
                        Assert.Equal(progress[fraction].GetProperty("Value").GetInt32(), current.GetAnimationProgress(fraction));
                    else {
                        Assert.Equal(JsonValueKind.Null, progress[fraction].GetProperty("Value").ValueKind);
                        Assert.NotNull(Record.Exception(() => current.GetAnimationProgress(fraction)));
                    }
                }
                Assert.Equal(current, current.CloneState());
            }
        }
        Assert.Equal(1_369, stepCount);
    }

    private static string InputFingerprint(IEnumerable<JsonElement> scenarios)
    {
        var transcript = new StringBuilder();
        foreach (var scenario in scenarios) {
            RequireFields(scenario, "Name", "Steps");
            transcript.Append(scenario.GetProperty("Name").GetString()).Append('\n');
            foreach (var step in scenario.GetProperty("Steps").EnumerateArray()) {
                RequireFields(step, "Kind", "Actor", "Frames", "Ticks", "Flags", "Skipped",
                    "Distribute", "Preview", "Value", "State", "RenderFrames", "ProgressResults");
                transcript.Append(step.GetProperty("Kind").GetString());
                foreach (var field in new[] { "Actor", "Frames", "Ticks", "Flags", "Skipped", "Distribute", "Preview" })
                    transcript.Append('|').Append(step.GetProperty(field).GetInt32().ToString(CultureInfo.InvariantCulture));
                transcript.Append('|').Append(step.GetProperty("Value").GetBoolean() ? '1' : '0').Append('\n');
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(transcript.ToString())));
    }

    [Fact]
    public void InputFingerprintBindsInputsButNotExpectedOutputs()
    {
        const string fixture = """
            {"Name":"fixture","Steps":[{"Kind":"start","Actor":0,"Frames":16,"Ticks":1,
              "Flags":1,"Skipped":4,"Distribute":9,"Preview":0,"Value":false,
              "State":{},"RenderFrames":[],"ProgressResults":[]}]}
            """;
        using var original = JsonDocument.Parse(fixture);
        using var alteredInput = JsonDocument.Parse(fixture.Replace("\"Skipped\":4", "\"Skipped\":3", StringComparison.Ordinal));
        using var alteredOutput = JsonDocument.Parse(fixture.Replace("\"State\":{}", "\"State\":{\"CurrentFrame\":99}", StringComparison.Ordinal));
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fixture\nstart|0|16|1|1|4|9|0|0\n")));
        Assert.Equal(expected, InputFingerprint([original.RootElement]));
        Assert.NotEqual(expected, InputFingerprint([alteredInput.RootElement]));
        Assert.Equal(expected, InputFingerprint([alteredOutput.RootElement]));
    }

    [Theory]
    [InlineData("{\"Value\":1}")]
    [InlineData("{\"Refusal\":0,\"Value\":1,\"Extra\":false}")]
    [InlineData("{\"Refusal\":0,\"Value\":1,\"Value\":2}")]
    public void EvidenceShapeRefusesMissingExtraAndDuplicateFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.NotNull(Record.Exception(() => RequireFields(document.RootElement, "Refusal", "Value")));
    }

    private static NativeAnimationState WithRate(NativeAnimationState state, int rate)
        => new(state.NumberOfFrames, rate, state.TickCounterOfCurrentFrame, state.CurrentFrame,
            state.IsPetrified, state.RelevantFramesForDistributing, state.SkippedFramesFromPreviousAnimation,
            state.TickModifier, state.TicksSinceSequenceStarted);

    private static int ProgressRefusal(NativeAnimationState state, int fraction)
    {
        var sequenceTicks = Math.Max(0, state.TicksSinceSequenceStarted);
        var modifier = state.TickModifier;
        if (state.RelevantFramesForDistributing <= 0) {
            if (state.TicksPerFrame <= 0)
                return 0;
            sequenceTicks = unchecked((short)((state.CurrentFrame * state.TicksPerFrame + state.TickCounterOfCurrentFrame) * 128));
            modifier = 128 / state.TicksPerFrame;
        }
        if (state.NumberOfFrames == 0)
            return 1;
        var product = (sequenceTicks + (state.IsPetrified ? 0 : fraction)) * (long)modifier;
        if (product is < int.MinValue or > int.MaxValue)
            return 2;
        if (product == int.MinValue && state.NumberOfFrames == -1)
            return 2;
        return product / state.NumberOfFrames / 128 > 128 ? 3 : 0;
    }

    private static NativeAnimationState ReadState(JsonElement state)
    {
        RequireFields(state, "NumberOfFrames", "TicksPerFrame", "TickCounterOfCurrentFrame", "CurrentFrame",
            "IsPetrified", "RelevantFramesForDistributing", "SkippedFramesFromPreviousAnimation",
            "TickModifier", "TicksSinceSequenceStarted");
        return new NativeAnimationState(
            state.GetProperty("NumberOfFrames").GetInt32(),
            state.GetProperty("TicksPerFrame").GetInt32(),
            state.GetProperty("TickCounterOfCurrentFrame").GetInt32(),
            state.GetProperty("CurrentFrame").GetInt32(),
            state.GetProperty("IsPetrified").GetBoolean(),
            state.GetProperty("RelevantFramesForDistributing").GetInt32(),
            state.GetProperty("SkippedFramesFromPreviousAnimation").GetInt32(),
            state.GetProperty("TickModifier").GetInt32(),
            state.GetProperty("TicksSinceSequenceStarted").GetInt32());
    }

    private static void RequireFields(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        var names = value.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(expected.Length, names.Length);
        Assert.Equal(expected.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
    }
}
