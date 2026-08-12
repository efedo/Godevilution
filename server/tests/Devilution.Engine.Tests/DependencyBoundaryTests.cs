using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void EngineDependsOnlyOnDataAndDomain()
    {
        var dependencies = typeof(GameplayModuleRegistry).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("Devilution.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Devilution.Data", "Devilution.Domain"], dependencies);
        Assert.Same(
            typeof(GameplayModuleRegistry).Assembly,
            typeof(AuthoritativeSimulationRuntime<,>).Assembly);
        Assert.Same(
            typeof(GameplayModuleRegistry).Assembly,
            typeof(GameplayRulesetRuntime<>).Assembly);
    }
}
