using System.Reflection;
using System.Text.RegularExpressions;
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
    public void DiabloGameModuleDependsOnlyOnDomainDataAndEngine()
    {
        var diablo = Assembly.Load("Devilution.Diablo");

        Assert.Equal(
            [
                "Devilution.Data",
                "Devilution.Domain",
                "Devilution.Engine",
            ],
            GodevilutionDependencies(diablo));
    }

    [Fact]
    public void CanonicalProtobufSchemaHasOneManagedGeneratorOwner()
    {
        var repositoryRoot = FindRepositoryRoot();
        var owners = new[] {
            Path.Combine(repositoryRoot, "protocol"),
            Path.Combine(repositoryRoot, "server", "src"),
            Path.Combine(repositoryRoot, "godot-client"),
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

    [Fact]
    public void AuthoritativeCSharpPathHasNoLegacyLuaReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        var authoritativeRoots = new[] {
            Path.Combine(repositoryRoot, "server", "src"),
            Path.Combine(repositoryRoot, "godot-client"),
        };
        var forbiddenReferences = authoritativeRoots
            .SelectMany(root => Directory.EnumerateFiles(
                root,
                "*.*",
                SearchOption.AllDirectories))
            .Where(path => !IsBuildArtifact(path))
            .Where(path => Path.GetExtension(path) is
                ".cs" or ".csproj" or ".props" or ".targets" or ".proto")
            .SelectMany(path => {
                var text = File.ReadAllText(path);
                return Regex.IsMatch(
                        text,
                        @"\b(lua|sol2)\b",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    ? new[] { Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/') }
                    : Array.Empty<string>();
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void ActiveContentPacksContainNoLuaAssetsOrManifestReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        var activePackRoots = new[] {
            Path.Combine(repositoryRoot, "server", "content", "base"),
            Path.Combine(repositoryRoot, "server", "content", "hellfire"),
        };
        var luaFiles = activePackRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.lua", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var manifestReferences = activePackRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "content-pack.json", SearchOption.TopDirectoryOnly))
            .Where(path => File.ReadAllText(path).Contains(".lua", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(luaFiles);
        Assert.Empty(manifestReferences);
    }

    [Fact]
    public void AuthoritativeNativeClientKeepsLuaBehindTheLegacyModeGuard()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "legacy-native",
            "application",
            "diablo.cpp"));
        const string modeGuard =
            "authoritative::GetServerBackedRuntimeConfiguration().mode != "
            + "authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative";

        // The lifecycle policy is centralized in ShouldInitializeLegacyLua;
        // only the debug-console registration keeps a direct mode comparison.
        static void AssertMatchCount(string sourceText, string pattern, int expected)
        {
            var actual = Regex.Matches(sourceText, Regex.Escape(pattern)).Count;
            Assert.True(
                actual == expected,
                $"Expected {expected} matches for '{pattern}', found {actual}.");
        }

        AssertMatchCount(source, modeGuard, 1);
        AssertMatchCount(source, "ShouldInitializeLegacyLua(", 2);
        AssertMatchCount(source, "if (ShouldInitializeLegacyLua(", 2);
        Assert.Contains("LuaInitialize();", source, StringComparison.Ordinal);
        Assert.Contains("LuaShutdown();", source, StringComparison.Ordinal);
        Assert.Contains(
            "// Authoritative mode delegates gameplay and content ownership to the server;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "// Keep Lua available for explicit legacy launches, but leave the",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritativeChatHotkeysDoNotEnterLegacyConsole()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "legacy-native",
            "ui",
            "panel",
            "control_chat.cpp"));
        const string nativeOnlyGuard = "if (!IsNativeOnlyConsole())";
        var guardIndex = source.IndexOf(nativeOnlyGuard, StringComparison.Ordinal);
        var legacyConsoleCallIndex = source.IndexOf(
            "RunInConsole(std::string_view(msg).substr(LuaPrefix.size()))",
            StringComparison.Ordinal);

        Assert.True(guardIndex >= 0);
        Assert.True(
            legacyConsoleCallIndex > guardIndex,
            "The legacy Lua hotkey must remain inside the authoritative native-only guard.");
    }

    [Fact]
    public void AuthoritativeNativeModDiscoverySkipsScriptMods()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "legacy-native",
            "persistence",
            "options.cpp"));

        Assert.Contains(
            "if (!IsAuthoritativeServerBackedMode())",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (IsAuthoritativeServerBackedMode())",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "const std::string modScriptPath =",
            source,
            StringComparison.Ordinal);
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
