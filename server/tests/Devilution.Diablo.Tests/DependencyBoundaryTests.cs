using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void DiabloDependsOnlyOnDomainDataAndEngine()
    {
        var dependencies = typeof(DiabloStoreRules).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("Devilution.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Devilution.Data", "Devilution.Domain", "Devilution.Engine"], dependencies);
    }

    [Fact]
    public void AuthoritativePlayerStateBelongsToDiabloModule()
    {
        var moduleAssembly = typeof(DiabloStoreRules).Assembly;

        Assert.Same(moduleAssembly, typeof(OwnedStoreItem).Assembly);
        Assert.Same(moduleAssembly, typeof(PlayerAttributesState).Assembly);
        Assert.Same(moduleAssembly, typeof(EquippedStoreItem).Assembly);
        Assert.Same(moduleAssembly, typeof(BeltStoreItem).Assembly);
        Assert.Same(moduleAssembly, typeof(StorePlayerSnapshot).Assembly);
        Assert.Same(moduleAssembly, typeof(AuthoritativePlayerState).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloStoreCommandProcessor).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloWorldState).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloWorldIntent).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloWorldCommandProcessor).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloCombatIntent).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloCombatCommandProcessor).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloGameplayEvent).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloDirectedGameplayEvent).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloSimulation).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloSimulationSnapshot).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloMonsterState).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloPlayerTemplate).Assembly);
        Assert.Same(moduleAssembly, typeof(AuthoritativePlayerClassCatalog).Assembly);
        Assert.Same(moduleAssembly, typeof(AuthoritativePlayerClassDefinition).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloModule).Assembly);
        Assert.Same(moduleAssembly, typeof(IDiabloContentBootstrap).Assembly);
        Assert.Same(moduleAssembly, typeof(DiabloContentActivation).Assembly);
    }
}
