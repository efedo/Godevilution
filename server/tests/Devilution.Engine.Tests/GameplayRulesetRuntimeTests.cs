using Devilution.Data;
using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class GameplayRulesetRuntimeTests
{
    [Fact]
    public void SuccessfulReloadPublishesContentModulesIdentityAndTargetTogether()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;

        var result = runtime.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0", "replacement")]);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Same(result.Active, runtime.Active);
        Assert.NotSame(original, result.Active);
        Assert.Equal("replacement", result.Active.Content
            .GetRequiredTable("rules.tsv").Rows[0].Required("value"));
        Assert.Equal("replacement", result.Active.Target.Value);
        Assert.Equal("2.0.0", Assert.Single(result.Active.Modules.Modules).Identity.Version);
        Assert.NotEqual(original.Identity.CombinedSha256, result.Active.Identity.CombinedSha256);
    }

    [Fact]
    public void ReplacedActivationWaitsForLeasesBeforeRetirement()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        using var lease = runtime.Acquire();
        var original = lease.Activation;

        var result = runtime.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0", "replacement")]);

        Assert.True(result.Succeeded);
        Assert.Equal(1, runtime.RetiredActivationCount);
        Assert.Empty(runtime.DrainRetired());

        lease.Dispose();
        var drained = runtime.DrainRetired();
        Assert.Single(drained);
        Assert.Same(original, drained[0]);
        Assert.Equal(0, runtime.RetiredActivationCount);
        lease.Dispose();
    }

    [Fact]
    public void UnleasedReplacedActivationCanBeDrainedImmediately()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;

        Assert.True(runtime.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0", "replacement")]).Succeeded);

        var drained = runtime.DrainRetired();
        Assert.Single(drained);
        Assert.Same(original, drained[0]);
        Assert.Empty(runtime.DrainRetired());
    }

    [Fact]
    public void SustainedReplacementPressureRetiresOnlyAfterAllGenerationLeasesRelease()
    {
        var runtime = CreateRuntime(
            CreateManifest("0", "generation-0"),
            new TestModule("rules", "0.0.0", "generation-0"));
        var leases = new List<GameplayRulesetLease<TestActivation>>();
        const int generationCount = 64;

        try {
            for (var generation = 1; generation <= generationCount; generation++) {
                leases.Add(runtime.Acquire());
                var result = runtime.TryReload(
                    CreateManifest(
                        generation.ToString(),
                        $"generation-{generation}"),
                    [new TestModule(
                        "rules",
                        $"{generation}.0.0",
                        $"generation-{generation}")]);

                Assert.True(result.Succeeded);
                Assert.Equal(generation, runtime.RetiredActivationCount);
            }

            Assert.Empty(runtime.DrainRetired());
            Assert.Equal(
                $"generation-{generationCount}",
                runtime.Active.Target.Value);
        } finally {
            foreach (var lease in leases)
                lease.Dispose();
        }

        var drained = runtime.DrainRetired();
        Assert.Equal(generationCount, drained.Count);
        Assert.Equal(0, runtime.RetiredActivationCount);
    }

    [Fact]
    public void CandidatePreparationRunsBeforePublication()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;
        var prepared = false;

        var result = runtime.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0", "replacement")],
            candidate => {
                prepared = true;
                Assert.Equal("replacement", candidate.Value);
                Assert.Same(original, runtime.Active);
            });

        Assert.True(result.Succeeded);
        Assert.True(prepared);
        Assert.Same(result.Active, runtime.Active);
    }

    [Fact]
    public void ActivationPreparationReceivesCandidateBeforePublication()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;
        GameplayRulesetActivation<TestActivation>? prepared = null;

        var result = runtime.TryReloadWithActivation(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0", "replacement")],
            candidate => {
                prepared = candidate;
                Assert.Same(original, runtime.Active);
                Assert.Equal("replacement", candidate.Target.Value);
            });

        Assert.True(result.Succeeded);
        Assert.Same(result.Active, prepared);
        Assert.Same(result.Active, runtime.Active);
    }

    [Fact]
    public void FailedCandidatePreparationKeepsThePreviousActivation()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;

        var result = runtime.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0", "replacement")],
            _ => throw new InvalidDataException("session migration rejected"));

        Assert.False(result.Succeeded);
        Assert.Equal(
            "ruleset-target-preparation-failed",
            Assert.Single(result.Diagnostics).Code);
        Assert.Same(original, result.Active);
        Assert.Same(original, runtime.Active);
        Assert.Equal(0, runtime.RetiredActivationCount);
    }

    [Fact]
    public void FailedContentCompositionKeepsTheCompletePreviousActivation()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;
        var baseTable = TsvTable.Parse("rules.tsv", "id\tvalue\na\tinitial\n");
        var invalid = new ContentManifest("rules", "2", [
            new ContentPack("base", "1", [baseTable]),
            new ContentPack(
                "conflict",
                "1",
                [TsvTable.Parse("rules.tsv", "id\tvalue\na\treplacement\n")],
                ["base"],
                []),
        ]);

        var result = runtime.TryReload(
            invalid,
            [new TestModule("rules", "2.0.0", "replacement")]);

        Assert.False(result.Succeeded);
        Assert.Same(original, result.Active);
        Assert.Same(original, runtime.Active);
        Assert.Equal("ruleset-content-invalid", Assert.Single(result.Diagnostics).Code);
        Assert.Equal("initial", runtime.Active.Target.Value);
    }

    [Fact]
    public void FailedModuleRegistrationKeepsTheCompletePreviousActivation()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;
        var staged = false;
        var dependency = new DelegateModule(
            new GameplayModuleIdentity("base", "2.0.0", "hash-base"),
            (registry, _) => {
                staged = true;
                registry.RegisterCapability<ITestRules>(new TestRules("staged"));
            });
        var failing = new DelegateModule(
            new GameplayModuleIdentity("rules", "2.0.0", "hash-rules", ["base"]),
            (_, _) => throw new InvalidOperationException("registration failed"));

        var result = runtime.TryReload(
            CreateManifest("2", "replacement"),
            [failing, dependency]);

        Assert.True(staged);
        Assert.False(result.Succeeded);
        Assert.Same(original, runtime.Active);
        Assert.Equal(
            "ruleset-module-registration-failed",
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal("initial", runtime.Active.Target.Value);
    }

    [Fact]
    public void FailedTargetValidationKeepsTheCompletePreviousActivation()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;

        var result = runtime.TryReload(
            CreateManifest("2", "invalid"),
            [new TestModule("rules", "2.0.0", "invalid")]);

        Assert.False(result.Succeeded);
        Assert.Same(original, runtime.Active);
        Assert.Equal(
            "ruleset-target-activation-failed",
            Assert.Single(result.Diagnostics).Code);
        Assert.Contains("rejected", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IncompatibleModuleCandidateKeepsTheCompletePreviousActivation()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;
        var incompatible = new DelegateModule(
            new GameplayModuleIdentity(
                "future",
                "2.0.0",
                "hash",
                [],
                new GameplayModuleCompatibility("2.0.0", "2.0.0")));

        var result = runtime.TryReload(
            CreateManifest("2", "replacement"),
            [incompatible]);

        Assert.False(result.Succeeded);
        Assert.Same(original, runtime.Active);
        Assert.Equal(
            "ruleset-modules-incompatible",
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void EmptyModuleCandidateKeepsTheCompletePreviousActivation()
    {
        var runtime = CreateRuntime(
            CreateManifest("1", "initial"),
            new TestModule("rules", "1.0.0", "initial"));
        var original = runtime.Active;

        var result = runtime.TryReload(CreateManifest("2", "replacement"), []);

        Assert.False(result.Succeeded);
        Assert.Same(original, runtime.Active);
        Assert.Equal(
            "ruleset-identity-invalid",
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task CandidateConstructionDoesNotBlockReadsBeforeAtomicPublication()
    {
        using var candidateStarted = new ManualResetEventSlim();
        using var releaseCandidate = new ManualResetEventSlim();
        var runtime = new GameplayRulesetRuntime<TestActivation>(
            CreateManifest("1", "initial"),
            [new TestModule("rules", "1.0.0", "initial")],
            new DelegateAuthoritativeClock(() => 0),
            (content, registry) => {
                var value = content.GetRequiredTable("rules.tsv")
                    .Rows[0].Required("value");
                if (value == "replacement") {
                    candidateStarted.Set();
                    releaseCandidate.Wait(TestContext.Current.CancellationToken);
                }
                return new TestActivation(
                    value,
                    registry.GetRequiredCapability<ITestRules>().Value);
            });
        var original = runtime.Active;

        var reload = Task.Run(() => runtime.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0", "replacement")]),
            TestContext.Current.CancellationToken);
        try {
            Assert.True(candidateStarted.Wait(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));
            Assert.Same(original, runtime.Active);
        } finally {
            releaseCandidate.Set();
        }
        var result = await reload;

        Assert.True(result.Succeeded);
        Assert.Same(result.Active, runtime.Active);
    }

    private static GameplayRulesetRuntime<TestActivation> CreateRuntime(
        ContentManifest manifest,
        IGameplayModule module)
    {
        return new GameplayRulesetRuntime<TestActivation>(
            manifest,
            [module],
            new DelegateAuthoritativeClock(() => 0),
            (content, registry) => {
                var value = content.GetRequiredTable("rules.tsv")
                    .Rows[0].Required("value");
                if (value == "invalid")
                    throw new InvalidDataException("Target validation rejected candidate content.");
                return new TestActivation(
                    value,
                    registry.GetRequiredCapability<ITestRules>().Value);
            });
    }

    private static ContentManifest CreateManifest(string version, string value)
    {
        return new ContentManifest(
            "rules",
            version,
            [new ContentPack(
                "base",
                version,
                [TsvTable.Parse("rules.tsv", $"id\tvalue\na\t{value}\n")])]);
    }

    private interface ITestRules
    {
        string Value { get; }
    }

    private sealed record TestRules(string Value) : ITestRules;

    private sealed record TestActivation(string Value, string CapabilityValue);

    private sealed class TestModule : IGameplayModule
    {
        private readonly string capabilityValue;

        public TestModule(string id, string version, string capabilityValue)
        {
            Identity = new GameplayModuleIdentity(id, version, $"hash-{version}");
            this.capabilityValue = capabilityValue;
        }

        public GameplayModuleIdentity Identity { get; }

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            registry.RegisterCapability<ITestRules>(new TestRules(capabilityValue));
        }
    }

    private sealed class DelegateModule : IGameplayModule
    {
        private readonly Action<GameplayModuleRegistry, GameplayModuleContext> register;

        public DelegateModule(
            GameplayModuleIdentity identity,
            Action<GameplayModuleRegistry, GameplayModuleContext>? register = null)
        {
            Identity = identity;
            this.register = register ?? ((_, _) => { });
        }

        public GameplayModuleIdentity Identity { get; }

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
            register(registry, context);
        }
    }
}
