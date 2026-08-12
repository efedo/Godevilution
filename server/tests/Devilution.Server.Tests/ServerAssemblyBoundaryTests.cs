using System.Reflection;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ServerAssemblyBoundaryTests
{
    [Fact]
    public void ProtocolHostAdapterAndExecutableHaveAcyclicAssemblyBoundaries()
    {
        var protocol = typeof(Command).Assembly;
        var host = typeof(AuthoritativeTcpServer).Assembly;
        var adapter = typeof(StoreSimulationExecutor).Assembly;
        var executable = Assembly.Load("Devilution.Server");

        Assert.Equal("Devilution.Protocol", protocol.GetName().Name);
        Assert.Equal("Devilution.Server.Host", host.GetName().Name);
        Assert.Equal("Devilution.Server.Diablo", adapter.GetName().Name);
        Assert.Equal("Devilution.Server", executable.GetName().Name);
        Assert.Equal([], GodevilutionDependencies(protocol));
        Assert.Equal(
            [
                "Devilution.Data",
                "Devilution.Domain",
                "Devilution.Engine",
                "Devilution.Protocol",
            ],
            GodevilutionDependencies(host));
        Assert.Equal(
            [
                "Devilution.Data",
                "Devilution.Diablo",
                "Devilution.Domain",
                "Devilution.Engine",
                "Devilution.Protocol",
                "Devilution.Server.Host",
            ],
            GodevilutionDependencies(adapter));
        Assert.Equal(
            [
                "Devilution.Data",
                "Devilution.Engine",
                "Devilution.Server.Diablo",
                "Devilution.Server.Host",
            ],
            GodevilutionDependencies(executable));
    }

    [Fact]
    public void CanonicalProtobufSchemaHasOneManagedGeneratorOwner()
    {
        var repositoryRoot = FindRepositoryRoot();
        var owners = new[] {
            Path.Combine(repositoryRoot, "protocol"),
            Path.Combine(repositoryRoot, "server", "src"),
            Path.Combine(repositoryRoot, "godot"),
        }
            .SelectMany(root => Directory.EnumerateFiles(
                root,
                "*.csproj",
                SearchOption.AllDirectories))
            .Where(path => !IsBuildArtifact(path))
            .Where(path => File.ReadAllText(path).Contains(
                "<Protobuf Include=",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["protocol/Devilution.Protocol/Devilution.Protocol.csproj"],
            owners);
    }

    [Fact]
    public void ExecutableProjectContainsOnlyTheCompositionRoot()
    {
        var projectRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "src",
            "Devilution.Server");
        var sources = Directory.EnumerateFiles(
                projectRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .Select(path => Path.GetRelativePath(projectRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Program.cs"], sources);
    }

    private static string[] GodevilutionDependencies(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("Devilution.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsBuildArtifact(string path)
    {
        var segments = Path.GetRelativePath(FindRepositoryRoot(), path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj" or ".godot");
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(
                    directory.FullName,
                    "protocol",
                    "devilution.proto"))) {
                    return directory.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }
}
