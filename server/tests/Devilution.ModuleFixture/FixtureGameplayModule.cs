using Devilution.Engine;

namespace Devilution.ModuleFixture;

public interface IFixtureGameplayCapability
{
    string Value { get; }
}

public sealed record FixtureGameplayCapability(string Value) : IFixtureGameplayCapability;

/** Minimal external assembly used to verify the package-loading boundary. */
public sealed class FixtureGameplayModule : IGameplayModule
{
    public GameplayModuleIdentity Identity { get; } = GameplayModuleIdentity.WithDependencyConstraints(
        "fixture.external",
        "1.0.0",
        "unverified-package-placeholder",
        [new GameplayModuleDependencyConstraint(
            "devilution.diablo",
            "0.1.0",
            "0.1.0")],
        new GameplayModuleCompatibility(
            GameplayEngineApiVersion.Current,
            GameplayEngineApiVersion.Current));

    public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        registry.RegisterCapability<IFixtureGameplayCapability>(
            new FixtureGameplayCapability("loaded"));
    }
}
