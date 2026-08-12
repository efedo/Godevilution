using Devilution.Data;
using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class GameplayModuleRegistryTests
{
    [Fact]
    public void LoadsDependenciesInOrderAndResolvesTypedCapabilities()
    {
        var registrationOrder = new List<string>();
        var capability = new TestCapability("base");
        var dependency = new TestModule(
            new GameplayModuleIdentity("base.rules", "1", "hash-a"),
            (registry, _) => {
                registrationOrder.Add("base.rules");
                registry.RegisterCapability<ITestCapability>(capability);
            });
        var dependent = new TestModule(
            new GameplayModuleIdentity("diablo.rules", "1", "hash-b", ["base.rules"]),
            (registry, _) => {
                registrationOrder.Add("diablo.rules");
                Assert.Same(capability, registry.GetRequiredCapability<ITestCapability>());
                registry.RegisterCapability<IOtherCapability>(new OtherCapability());
            });
        var registry = new GameplayModuleRegistry();

        registry.Load([dependent, dependency], CreateContext());

        Assert.Equal(["base.rules", "diablo.rules"], registrationOrder);
        Assert.Equal(
            ["base.rules", "diablo.rules"],
            registry.Modules.Select(module => module.Identity.Id));
        Assert.Same(capability, registry.GetRequiredCapability<ITestCapability>());
        Assert.Equal("base.rules", registry.CapabilityProviders[typeof(ITestCapability)]);
        Assert.Equal("diablo.rules", registry.CapabilityProviders[typeof(IOtherCapability)]);
    }

    [Fact]
    public void FailedRegistrationDoesNotPublishPartialCapabilities()
    {
        var module = new TestModule(
            new GameplayModuleIdentity("broken", "1", "hash"),
            (registry, _) => {
                registry.RegisterCapability<ITestCapability>(new TestCapability("partial"));
                throw new InvalidOperationException("registration failed");
            });
        var registry = new GameplayModuleRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Load([module], CreateContext()));

        Assert.Empty(registry.Modules);
        Assert.Empty(registry.CapabilityProviders);
        Assert.False(registry.TryGetCapability<ITestCapability>(out _));
    }

    [Fact]
    public void AtomicActivationDoesNotPublishEarlierSuccessfulModules()
    {
        var first = new TestModule(
            new GameplayModuleIdentity("first", "1.0.0", "hash-a"),
            (registry, _) => registry.RegisterCapability<ITestCapability>(
                new TestCapability("staged")));
        var failing = new TestModule(
            new GameplayModuleIdentity("failing", "1.0.0", "hash-b", ["first"]),
            (_, _) => throw new InvalidOperationException("candidate failed"));
        var registry = new GameplayModuleRegistry();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Activate([first, failing], CreateContext()));

        Assert.Empty(registry.Modules);
        Assert.Empty(registry.CapabilityProviders);
        Assert.False(registry.TryGetCapability<ITestCapability>(out _));
        Assert.NotNull(registry.LastLoadPlan);
    }

    [Fact]
    public void DuplicateCapabilityProviderLeavesEarlierModuleIntact()
    {
        var firstCapability = new TestCapability("first");
        var first = new TestModule(
            new GameplayModuleIdentity("first", "1", "hash-a"),
            (registry, _) => registry.RegisterCapability<ITestCapability>(firstCapability));
        var second = new TestModule(
            new GameplayModuleIdentity("second", "1", "hash-b", ["first"]),
            (registry, _) => registry.RegisterCapability<ITestCapability>(
                new TestCapability("second")));
        var registry = new GameplayModuleRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Load([first, second], CreateContext()));

        Assert.Equal("first", Assert.Single(registry.Modules).Identity.Id);
        Assert.Same(firstCapability, registry.GetRequiredCapability<ITestCapability>());
    }

    [Fact]
    public void RejectsMissingCyclicAndDuplicateModuleDependencies()
    {
        var missing = new TestModule(
            new GameplayModuleIdentity("missing-user", "1", "hash", ["absent"]));
        Assert.Throws<GameplayModuleCompatibilityException>(() =>
            new GameplayModuleRegistry().Load([missing], CreateContext()));

        var first = new TestModule(
            new GameplayModuleIdentity("first", "1", "hash-a", ["second"]));
        var second = new TestModule(
            new GameplayModuleIdentity("second", "1", "hash-b", ["first"]));
        Assert.Throws<GameplayModuleCompatibilityException>(() =>
            new GameplayModuleRegistry().Load([first, second], CreateContext()));

        var duplicateA = new TestModule(new GameplayModuleIdentity("duplicate", "1", "hash-a"));
        var duplicateB = new TestModule(new GameplayModuleIdentity("duplicate", "2", "hash-b"));
        Assert.Throws<GameplayModuleCompatibilityException>(() =>
            new GameplayModuleRegistry().Load([duplicateA, duplicateB], CreateContext()));
    }

    [Fact]
    public void CapabilityRegistrationIsOnlyAvailableDuringModuleLoading()
    {
        var registry = new GameplayModuleRegistry();

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterCapability<ITestCapability>(new TestCapability("invalid")));
        Assert.Throws<InvalidOperationException>(() =>
            registry.GetRequiredCapability<ITestCapability>());
    }

    [Fact]
    public void ModuleIdentityRejectsSelfAndDuplicateDependencies()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameplayModuleIdentity("self", "1", "hash", ["self"]));
        Assert.Throws<ArgumentException>(() =>
            new GameplayModuleIdentity("module", "1", "hash", ["base", "base"]));
    }

    [Fact]
    public void RulesetIdentityIsDeterministicAndModuleOrderSensitive()
    {
        var content = CreateContent();
        var first = new GameplayModuleIdentity("first", "1", "hash-a");
        var second = new GameplayModuleIdentity("second", "1", "hash-b", ["first"]);

        var identity = new GameplayRulesetIdentity(content, [first, second]);
        var repeated = new GameplayRulesetIdentity(content, [first, second]);
        var reordered = new GameplayRulesetIdentity(content, [second, first]);

        Assert.Equal(identity.CombinedSha256, repeated.CombinedSha256);
        Assert.NotEqual(identity.CombinedSha256, reordered.CombinedSha256);
        Assert.Equal(64, identity.CombinedSha256.Length);
    }

    [Fact]
    public void RulesetIdentityIncludesModuleDependencyVersionRequirements()
    {
        var content = CreateContent();
        var dependency = new GameplayModuleIdentity("base", "1.2.3", "hash-a");
        var unconstrained = new GameplayModuleIdentity(
            "dependent",
            "2.0.0",
            "hash-b",
            ["base"]);
        var constrained = GameplayModuleIdentity.WithDependencyConstraints(
            "dependent",
            "2.0.0",
            "hash-b",
            [new GameplayModuleDependencyConstraint("base", "1.0.0", "1.9.9")]);
        var narrowed = GameplayModuleIdentity.WithDependencyConstraints(
            "dependent",
            "2.0.0",
            "hash-b",
            [new GameplayModuleDependencyConstraint("base", "1.2.0", "1.9.9")]);

        var legacyIdentity = new GameplayRulesetIdentity(content, [dependency, unconstrained]);
        var constrainedIdentity = new GameplayRulesetIdentity(content, [dependency, constrained]);
        var narrowedIdentity = new GameplayRulesetIdentity(content, [dependency, narrowed]);

        Assert.NotEqual(legacyIdentity.CombinedSha256, constrainedIdentity.CombinedSha256);
        Assert.NotEqual(constrainedIdentity.CombinedSha256, narrowedIdentity.CombinedSha256);
    }

    [Fact]
    public void DelegateClockExposesCallerOwnedTick()
    {
        ulong tick = 3;
        var clock = new DelegateAuthoritativeClock(() => tick);

        Assert.Equal(3UL, clock.CurrentTick);
        tick = 9;
        Assert.Equal(9UL, clock.CurrentTick);
    }

    [Fact]
    public void InspectionReportsEngineApiCompatibilityBeforeRegistration()
    {
        var registered = false;
        var module = new TestModule(
            new GameplayModuleIdentity(
                "future",
                "1",
                "hash",
                [],
                new GameplayModuleCompatibility("0.2.0", "0.3.0")),
            (_, _) => registered = true);
        var registry = new GameplayModuleRegistry();

        var plan = registry.Inspect([module], CreateContext());
        var exception = Assert.Throws<GameplayModuleCompatibilityException>(() =>
            registry.Load([module], CreateContext()));

        Assert.False(plan.IsCompatible);
        Assert.Equal(
            "engine-api-incompatible",
            Assert.Single(plan.Diagnostics).Code);
        Assert.Contains("0.2.0 through 0.3.0", exception.Message, StringComparison.Ordinal);
        Assert.False(registered);
        Assert.Empty(registry.Modules);
        Assert.NotNull(registry.LastLoadPlan);
    }

    [Fact]
    public void InspectionProducesDeterministicOrderAndActionableDependencyDiagnostics()
    {
        var dependency = new TestModule(new GameplayModuleIdentity(
            "base",
            "1",
            "hash-a",
            [],
            new GameplayModuleCompatibility("0.1.0", "0.1.0")));
        var dependent = new TestModule(new GameplayModuleIdentity(
            "dependent",
            "1",
            "hash-b",
            ["base"],
            new GameplayModuleCompatibility("0.1.0", "0.1.0")));
        var registry = new GameplayModuleRegistry();

        var compatible = registry.Inspect([dependent, dependency], CreateContext());
        var missing = registry.Inspect([
            new TestModule(new GameplayModuleIdentity("missing-user", "1", "hash", ["absent"])),
        ], CreateContext());

        Assert.True(compatible.IsCompatible);
        Assert.Equal(
            ["base", "dependent"],
            compatible.OrderedModules.Select(module => module.Identity.Id));
        Assert.All(compatible.Diagnostics, diagnostic =>
            Assert.Equal("module-compatible", diagnostic.Code));
        Assert.Equal(
            "module-dependency-missing",
            Assert.Single(missing.Diagnostics).Code);
        Assert.Contains("absent", missing.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InspectionAcceptsInclusiveModuleDependencyVersionRanges()
    {
        var dependency = new TestModule(new GameplayModuleIdentity(
            "base",
            "1.2.3",
            "hash-a"));
        var dependent = new TestModule(GameplayModuleIdentity.WithDependencyConstraints(
            "dependent",
            "2.0.0",
            "hash-b",
            [new GameplayModuleDependencyConstraint("base", "1.2.0", "1.2.3")]));
        var registry = new GameplayModuleRegistry();

        var plan = registry.Inspect([dependent, dependency], CreateContext());
        registry.Load([dependent, dependency], CreateContext());

        Assert.True(plan.IsCompatible);
        Assert.Equal(
            ["base", "dependent"],
            plan.OrderedModules.Select(module => module.Identity.Id));
        var constraint = Assert.Single(dependent.Identity.DependencyConstraints);
        Assert.Equal("base", constraint.DependencyId);
        Assert.Equal("1.2.0", constraint.Minimum.ToString());
        Assert.Equal("1.2.3", constraint.Maximum.ToString());
    }

    [Fact]
    public void InspectionRejectsInvalidAndOutOfRangeDependencyVersions()
    {
        var constrained = new TestModule(GameplayModuleIdentity.WithDependencyConstraints(
            "dependent",
            "2.0.0",
            "hash-b",
            [new GameplayModuleDependencyConstraint("base", "1.0.0", "1.9.9")]));
        var incompatible = new GameplayModuleRegistry().Inspect([
            constrained,
            new TestModule(new GameplayModuleIdentity("base", "2.0.0", "hash-a")),
        ], CreateContext());
        var invalid = new GameplayModuleRegistry().Inspect([
            constrained,
            new TestModule(new GameplayModuleIdentity("base", "legacy", "hash-a")),
        ], CreateContext());

        Assert.False(incompatible.IsCompatible);
        Assert.Equal(
            "module-dependency-version-incompatible",
            Assert.Single(incompatible.Diagnostics).Code);
        Assert.Contains("1.0.0 through 1.9.9", incompatible.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains("2.0.0 is loaded", incompatible.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.False(invalid.IsCompatible);
        Assert.Equal(
            "module-dependency-version-invalid",
            Assert.Single(invalid.Diagnostics).Code);
        Assert.Contains("'legacy'", invalid.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleDependencyConstraintsRequireSemanticDeclaredDependencies()
    {
        Assert.Equal("1.2.3", GameplayModuleVersion.Parse("1.2.3").ToString());
        Assert.Throws<ArgumentException>(() => GameplayModuleVersion.Parse("1.2"));
        Assert.Throws<ArgumentException>(() => new GameplayModuleDependencyConstraint(
            "base",
            "2.0.0",
            "1.0.0"));
        Assert.Throws<ArgumentException>(() => new GameplayModuleIdentity(
            "dependent",
            "1.0.0",
            "hash",
            [],
            null,
            [new GameplayModuleDependencyConstraint("base", "1.0.0", "2.0.0")]));
        Assert.Throws<ArgumentException>(() => GameplayModuleIdentity.WithDependencyConstraints(
            "dependent",
            "1.0.0",
            "hash",
            [
                new GameplayModuleDependencyConstraint("base", "1.0.0", "2.0.0"),
                new GameplayModuleDependencyConstraint("base", "1.5.0", "2.0.0"),
            ]));
    }

    [Fact]
    public void EngineApiVersionsAreSemanticAndRangesAreInclusive()
    {
        var version = GameplayEngineApiVersion.Parse("1.2.3");
        var compatibility = new GameplayModuleCompatibility("1.2.0", "1.3.0");

        Assert.Equal("1.2.3", version.ToString());
        Assert.True(compatibility.Supports(version));
        Assert.False(compatibility.Supports(GameplayEngineApiVersion.Parse("1.3.1")));
        Assert.Throws<ArgumentException>(() => GameplayEngineApiVersion.Parse("1.2"));
        Assert.Throws<ArgumentException>(() =>
            new GameplayModuleCompatibility("2.0.0", "1.0.0"));
    }

    private static GameplayModuleContext CreateContext()
    {
        return new GameplayModuleContext(CreateContent(), new DelegateAuthoritativeClock(() => 0));
    }

    private static ContentManifest CreateContent()
    {
        return new ContentManifest(
            "base",
            "1",
            [new ContentPack("base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]);
    }

    private interface ITestCapability
    {
        string Name { get; }
    }

    private interface IOtherCapability
    {
    }

    private sealed record TestCapability(string Name) : ITestCapability;

    private sealed class OtherCapability : IOtherCapability;

    private sealed class TestModule : IGameplayModule
    {
        private readonly Action<GameplayModuleRegistry, GameplayModuleContext> register;

        public TestModule(
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
