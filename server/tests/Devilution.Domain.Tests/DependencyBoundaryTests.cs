using Devilution.Domain.Inventory;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void DomainHasNoGodevilutionAssemblyDependencies()
    {
        var dependencies = typeof(InventoryGridLayout).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("Devilution.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(dependencies);
    }
}
