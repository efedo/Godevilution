using System.Security.Cryptography;
using System.Text.Json;
using Devilution.AdapterFixture;
using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeGameHostAdapterPackageLoaderTests
{
    [Fact]
    public void LoadsHashVerifiedFactoryFromAnExplicitPackage()
    {
        var root = CreatePackage();
        try {
            var package = new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory(root);

            Assert.Equal("fixture-package", package.PackageId);
            Assert.Equal("1.2.3", package.PackageVersion);
            Assert.Equal("fixture.adapter.with-dependency", package.Factory.Id);
            Assert.Equal(
                typeof(PackagedTestAdapterFactory).FullName,
                package.Factory.GetType().FullName);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsAnAssemblyHashMismatchBeforeLoadingTheFactory()
    {
        var root = CreatePackage(assemblySha256: new string('0', 64));
        try {
            var exception = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory(root));

            Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsTraversalAndUnknownFactoryTypes()
    {
        var root = CreatePackage(assembly: "../adapter.dll");
        try {
            var traversal = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory(root));
            Assert.Contains("canonical package-relative", traversal.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
        }

        root = CreatePackage(factoryType: "Devilution.AdapterFixture.MissingFactory");
        try {
            var missing = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory(root));
            Assert.Contains("missing factory type", missing.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadsTheProductionDiabloAdapterPackageWithItsPrivateClosure()
    {
        var root = CreateProductionDiabloPackage();
        try {
            var package = new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory(root);

            Assert.Equal("godevilution.diablo.package", package.Factory.Id);
            var catalog = new AuthoritativeGameHostAdapterCatalog(
                [package.Factory],
                package.Factory.Id);
            Assert.Single(catalog.CreateBuiltInModules());
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsAnUnlistedPrivateDependency()
    {
        var root = CreatePackage(includeDependency: false);
        try {
            var exception = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory(root));

            Assert.Contains("list every private dependency", exception.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsAPrivateDependencyHashMismatch()
    {
        var root = CreatePackage(dependencySha256: new string('0', 64));
        try {
            var exception = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeGameHostAdapterPackageLoader().LoadDirectory(root));

            Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreatePackage(
        string assembly = "adapter.dll",
        string? assemblySha256 = null,
        string factoryType = "Devilution.AdapterFixture.PackagedTestAdapterFactory",
        bool includeDependency = true,
        string? dependencySha256 = null)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-adapter-package-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sourceAssembly = typeof(PackagedTestAdapterFactory).Assembly.Location;
        File.Copy(sourceAssembly, Path.Combine(root, "adapter.dll"));
        assemblySha256 ??= Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "adapter.dll"))))
            .ToLowerInvariant();
        var dependencyPath = Path.Combine(root, "dependency.dll");
        var dependencyManifests = new List<object>();
        if (includeDependency) {
            var sourceDependency = typeof(Devilution.AdapterDependency.PackageDependencyIdentity).Assembly.Location;
            File.Copy(sourceDependency, dependencyPath);
            dependencySha256 ??= Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(dependencyPath)))
                .ToLowerInvariant();
            dependencyManifests.Add(new {
                assembly = "dependency.dll",
                assemblySha256 = dependencySha256,
            });
        }
        var manifest = new {
            formatVersion = 1,
            id = "fixture-package",
            version = "1.2.3",
            assembly,
            assemblySha256,
            factoryType,
            dependencies = dependencyManifests,
        };
        File.WriteAllText(
            Path.Combine(root, AuthoritativeGameHostAdapterPackageLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
        return root;
    }

    private static string CreateProductionDiabloPackage()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-adapter-package-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var files = new[] {
            ("adapter.dll", typeof(Godevilution.Server.Diablo.Package.PackagedDiabloAuthoritativeGameHostAdapterFactory).Assembly.Location),
            ("server-diablo.dll", typeof(Devilution.Server.Gameplay.DiabloAuthoritativeGameHostAdapterFactory).Assembly.Location),
            ("diablo.dll", typeof(Devilution.Diablo.DiabloModule).Assembly.Location),
            ("domain.dll", typeof(Devilution.Domain.Protocol.AuthoritativeLevelIdentityRules).Assembly.Location),
            ("protobuf.dll", typeof(Google.Protobuf.IMessage).Assembly.Location),
        };
        foreach (var (name, source) in files)
            File.Copy(source, Path.Combine(root, name));

        var manifest = new {
            formatVersion = 1,
            id = "godevilution-diablo-package",
            version = "0.1.0",
            assembly = "adapter.dll",
            assemblySha256 = GetSha256(Path.Combine(root, "adapter.dll")),
            factoryType = "Godevilution.Server.Diablo.Package.PackagedDiabloAuthoritativeGameHostAdapterFactory",
            dependencies = new[] {
                new { assembly = "server-diablo.dll", assemblySha256 = GetSha256(Path.Combine(root, "server-diablo.dll")) },
                new { assembly = "diablo.dll", assemblySha256 = GetSha256(Path.Combine(root, "diablo.dll")) },
                new { assembly = "domain.dll", assemblySha256 = GetSha256(Path.Combine(root, "domain.dll")) },
                new { assembly = "protobuf.dll", assemblySha256 = GetSha256(Path.Combine(root, "protobuf.dll")) },
            },
        };
        File.WriteAllText(
            Path.Combine(root, AuthoritativeGameHostAdapterPackageLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
        return root;
    }

    private static string GetSha256(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
