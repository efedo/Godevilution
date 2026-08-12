using Devilution.Engine;
using Devilution.ModuleDependencyFixture;

namespace Devilution.ModuleWithDependencyFixture;

/** Invalid format-1 fixture because its behavior uses a private assembly. */
public sealed class ModuleWithPrivateDependency : IGameplayModule
{
    public GameplayModuleIdentity Identity { get; } = new(
        "fixture.private-dependency",
        PrivateRuleData.ModuleVersion,
        "unverified-package-placeholder");

    public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);
    }
}
