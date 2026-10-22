using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Devilution.ModuleDependencyFixture;
using Devilution.ModuleFixture;
using Devilution.ModuleWithDependencyFixture;
using Devilution.Server.Gameplay;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class GameplayModulePackageLoaderTests
{
    [Fact]
    public void LoadsOnlyDeclaredTypesAndUsesTheVerifiedAssemblyHash()
    {
        using var package = TestPackage.Create();
        var loader = new GameplayModulePackageLoader();

        var loaded = loader.LoadDirectory(package.Root);

        Assert.Equal("fixture.package", loaded.PackageId);
        Assert.Equal("1.0.0", loaded.PackageVersion);
        Assert.Equal(package.AssemblySha256, loaded.AssemblySha256);
        var module = Assert.Single(loaded.Modules);
        Assert.Equal("fixture.external", module.Identity.Id);
        Assert.Equal(package.AssemblySha256, module.Identity.AssemblySha256);
        var dependency = Assert.Single(module.Identity.DependencyConstraints);
        Assert.Equal("devilution.diablo", dependency.DependencyId);
        Assert.Equal("0.1.0", dependency.Minimum.ToString());
        Assert.Equal("0.1.0", dependency.Maximum.ToString());

        var registry = new GameplayModuleRegistry();
        registry.Load([module, DiabloModule.Instance], CreateContext());
        Assert.Equal(
            "fixture.external",
            registry.CapabilityProviders.Single(provider =>
                provider.Key.FullName == "Devilution.ModuleFixture.IFixtureGameplayCapability").Value);
    }

    [Fact]
    public void RejectsHashMismatchBeforeLoadingAssembly()
    {
        using var package = TestPackage.Create(assemblySha256: new string('0', 64));

        var exception = Assert.Throws<InvalidDataException>(() =>
            new GameplayModulePackageLoader().LoadDirectory(package.Root));

        Assert.Contains("expected assembly SHA-256", exception.Message, StringComparison.Ordinal);
        Assert.Contains(package.ActualAssemblySha256, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidUtf8Manifest()
    {
        using var package = TestPackage.Create();
        File.WriteAllBytes(
            Path.Combine(package.Root, GameplayModulePackageLoader.ManifestFileName),
            [0x7B, 0x22, 0x69, 0x64, 0x22, 0x3A, 0xC3, 0x28, 0x7D]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new GameplayModulePackageLoader().LoadDirectory(package.Root));

        Assert.Contains("not valid UTF-8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsReparsePointEntries()
    {
        using var package = TestPackage.Create();
        var outside = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Module-Package-Outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try {
            try {
                Directory.CreateSymbolicLink(Path.Combine(package.Root, "linked"), outside);
            } catch (Exception exception) when (exception is UnauthorizedAccessException
                or PlatformNotSupportedException
                or IOException) {
                return;
            }

            var error = Assert.Throws<InvalidDataException>(() =>
                new GameplayModulePackageLoader().LoadDirectory(package.Root));
            Assert.Contains("reparse-point", error.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void RejectsEscapingAssemblyPathsAndUndeclaredTypes()
    {
        using var escaping = TestPackage.Create(assemblyPath: "../outside.dll");
        Assert.Contains(
            "escapes the package directory",
            Assert.Throws<InvalidDataException>(() =>
                new GameplayModulePackageLoader().LoadDirectory(escaping.Root)).Message,
            StringComparison.Ordinal);

        using var missingType = TestPackage.Create(moduleTypes: ["Missing.Module"]);
        Assert.Contains(
            "missing module type 'Missing.Module'",
            Assert.Throws<InvalidDataException>(() =>
                new GameplayModulePackageLoader().LoadDirectory(missingType.Root)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNonCanonicalAssemblyPaths()
    {
        using var dotAlias = TestPackage.Create(assemblyPath: "./module.dll");
        Assert.Contains(
            "canonical package-relative path",
            Assert.Throws<InvalidDataException>(() =>
                new GameplayModulePackageLoader().LoadDirectory(dotAlias.Root)).Message,
            StringComparison.Ordinal);

        using var traversalAlias = TestPackage.Create(assemblyPath: "sub/../module.dll");
        Assert.Contains(
            "canonical package-relative path",
            Assert.Throws<InvalidDataException>(() =>
                new GameplayModulePackageLoader().LoadDirectory(traversalAlias.Root)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsUnsupportedManifestFormat()
    {
        using var package = TestPackage.Create(formatVersion: 2);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new GameplayModulePackageLoader().LoadDirectory(package.Root));

        Assert.Contains("requires formatVersion 1", exception.Message, StringComparison.Ordinal);

        using var invalidVersion = TestPackage.Create(version: "1");
        Assert.Contains(
            "version must contain three unsigned integers",
            Assert.Throws<InvalidDataException>(() =>
                new GameplayModulePackageLoader().LoadDirectory(invalidVersion.Root)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPrivateManagedDependenciesOutsideTheVerifiedAssembly()
    {
        using var package = TestPackage.Create(
            moduleType: typeof(ModuleWithPrivateDependency));

        var exception = Assert.Throws<InvalidDataException>(() =>
            new GameplayModulePackageLoader().LoadDirectory(package.Root));

        Assert.Contains("unsupported private assemblies", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Devilution.ModuleDependencyFixture", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsDependenciesExplicitlySharedByTheCompositionRoot()
    {
        using var package = TestPackage.Create(
            moduleType: typeof(ModuleWithPrivateDependency));
        var loader = new GameplayModulePackageLoader([
            typeof(PrivateRuleData).Assembly,
        ]);

        var loaded = loader.LoadDirectory(package.Root);

        Assert.Equal(
            "fixture.private-dependency",
            Assert.Single(loaded.Modules).Identity.Id);
    }

    [Fact]
    public async Task StandaloneHostLoadsAnExplicitVerifiedPackage()
    {
        using var package = TestPackage.Create();
        var repositoryRoot = FindRepositoryRoot();
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Module-Host-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var startInfo = new ProcessStartInfo {
            FileName = "dotnet",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(
            repositoryRoot,
            "server/src/Devilution.Server/Devilution.Server.csproj"));
        startInfo.ArgumentList.Add("--no-build");
        // Run the server built alongside this test assembly, not dotnet's Debug default.
        startInfo.ArgumentList.Add("--configuration");
#if DEBUG
        startInfo.ArgumentList.Add("Debug");
#else
        startInfo.ArgumentList.Add("Release");
#endif
        var artifactsPath = Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH");
        if (!string.IsNullOrWhiteSpace(artifactsPath)) {
            startInfo.ArgumentList.Add("--artifacts-path");
            startInfo.ArgumentList.Add(Path.GetFullPath(artifactsPath));
        }
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add("0");
        startInfo.ArgumentList.Add("--save-root");
        startInfo.ArgumentList.Add(saveRoot);
        startInfo.ArgumentList.Add("--module-package");
        startInfo.ArgumentList.Add(package.Root);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the authoritative server.");
        var standardError = process.StandardError.ReadToEndAsync(
            TestContext.Current.CancellationToken);
        var sawListening = false;
        var sawPackage = false;
        var sawModule = false;
        try {
            while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line) {
                sawListening |= line.Contains("listening on", StringComparison.Ordinal);
                sawPackage |= line.Contains(
                    "Module packages: fixture.package@1.0.0",
                    StringComparison.Ordinal);
                sawModule |= line.Contains(
                    "Module fixture.external:",
                    StringComparison.Ordinal);
                if (sawListening && sawPackage && sawModule)
                    break;
            }
            if (!(sawListening && sawPackage && sawModule)) {
                var error = await standardError;
                throw new InvalidOperationException(
                    $"Server exited before reporting the loaded fixture package. {error}");
            }
        } finally {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    private static GameplayModuleContext CreateContext()
    {
        var content = new ContentManifest(
            "base",
            "1",
            [new ContentPack("base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]);
        return new GameplayModuleContext(
            content,
            new DelegateAuthoritativeClock(() => 0));
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "protocol/devilution.proto")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private sealed class TestPackage : IDisposable
    {
        private TestPackage(string root, string assemblySha256, string actualAssemblySha256)
        {
            Root = root;
            AssemblySha256 = assemblySha256;
            ActualAssemblySha256 = actualAssemblySha256;
        }

        public string Root { get; }

        public string AssemblySha256 { get; }

        public string ActualAssemblySha256 { get; }

        public static TestPackage Create(
            int formatVersion = 1,
            string version = "1.0.0",
            string? assemblyPath = null,
            string? assemblySha256 = null,
            IReadOnlyList<string>? moduleTypes = null,
            Type? moduleType = null)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "Godevilution-Module-Package-Test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            moduleType ??= typeof(FixtureGameplayModule);
            var sourceAssembly = moduleType.Assembly.Location;
            var packagedAssemblyName = Path.GetFileName(sourceAssembly);
            var packagedAssembly = Path.Combine(root, packagedAssemblyName);
            File.Copy(sourceAssembly, packagedAssembly);
            string actualHash;
            using (var stream = File.OpenRead(packagedAssembly))
                actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            var declaredHash = assemblySha256 ?? actualHash;
            File.WriteAllText(
                Path.Combine(root, GameplayModulePackageLoader.ManifestFileName),
                JsonSerializer.Serialize(new {
                    formatVersion,
                    id = "fixture.package",
                    version,
                    assembly = assemblyPath ?? packagedAssemblyName,
                    assemblySha256 = declaredHash,
                    moduleTypes = moduleTypes ?? [moduleType.FullName!],
                }));
            return new TestPackage(root, declaredHash, actualHash);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
