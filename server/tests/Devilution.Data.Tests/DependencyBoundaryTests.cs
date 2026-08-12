using Xunit;

namespace Devilution.Data.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void DataDependsOnlyOnDomain()
    {
        var dependencies = typeof(ContentManifest).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("Devilution.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Devilution.Domain"], dependencies);
    }
}
