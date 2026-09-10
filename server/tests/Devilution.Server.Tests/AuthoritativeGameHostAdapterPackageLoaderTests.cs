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
            Assert.Equal("fixture.adapter", package.Factory.Id);
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

    private static string CreatePackage(
        string assembly = "adapter.dll",
        string? assemblySha256 = null,
        string factoryType = "Devilution.AdapterFixture.PackagedTestAdapterFactory")
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
        var manifest = new {
            formatVersion = 1,
            id = "fixture-package",
            version = "1.2.3",
            assembly,
            assemblySha256,
            factoryType,
        };
        File.WriteAllText(
            Path.Combine(root, AuthoritativeGameHostAdapterPackageLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
        return root;
    }
}
