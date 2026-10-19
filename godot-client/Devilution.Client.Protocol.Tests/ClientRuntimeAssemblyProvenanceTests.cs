using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Client.Diagnostics;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientRuntimeAssemblyProvenanceTests
{
    [Fact]
    public void CapturesOnlyExplicitLoadedAssembliesAndBindsEachToItsFile()
    {
        var testAssembly = typeof(ClientRuntimeAssemblyProvenanceTests).Assembly;
        var protocolAssembly = typeof(AuthoritativeClient).Assembly;
        var observations = ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput("tests", testAssembly),
            new ClientRuntimeAssemblyInput("client-protocol", protocolAssembly),
        ]);

        Assert.Collection(observations,
            test => AssertAssemblyMatches(test, testAssembly, "tests"),
            protocol => AssertAssemblyMatches(protocol, protocolAssembly, "client-protocol"));
        Assert.Equal(2, observations.Count);
        Assert.All(observations, item => Assert.Equal(
            ClientRuntimeAssemblyImageBindingKind.LoadedFile,
            item.ImageBindingKind));
    }

    [Fact]
    public void ValidatesAnExactAssemblyManifestAndRejectsTamperedEntries()
    {
        var assembly = typeof(ClientRuntimeAssemblyProvenanceTests).Assembly;
        var observation = Assert.Single(ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput("client", assembly),
        ]));
        var manifest = ManifestForAssembly("client", assembly);

        ClientRuntimeAssemblyProvenance.Validate([observation], [manifest]);

        var tamperedEntries = new[] {
            manifest with { AssemblyName = "unexpected-name" },
            manifest with { AssemblyVersion = "1.invalid" },
            manifest with { FullName = "not an assembly identity" },
            manifest with { ModuleVersionId = Guid.NewGuid().ToString("D") },
            manifest with { Configuration = observation.Configuration == "Debug" ? "Release" : "Debug" },
            manifest with { Configuration = " " },
            manifest with { Sha256 = new string('0', 64) },
            manifest with { FileName = "unexpected.dll" },
            manifest with { CanonicalLocationSha256 = new string('F', 64) },
        };
        foreach (var tampered in tamperedEntries)
            Assert.Throws<InvalidDataException>(
                () => ClientRuntimeAssemblyProvenance.Validate([observation], [tampered]));
    }

    [Fact]
    public void RequiresAnExactUniqueSetOfAssemblyRoles()
    {
        var observation = Assert.Single(ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput(
                "client", typeof(ClientRuntimeAssemblyProvenanceTests).Assembly),
        ]));
        var manifest = ManifestForAssembly(
            "client", typeof(ClientRuntimeAssemblyProvenanceTests).Assembly);

        Assert.Throws<InvalidDataException>(
            () => ClientRuntimeAssemblyProvenance.Validate([], [manifest]));
        Assert.Throws<InvalidDataException>(
            () => ClientRuntimeAssemblyProvenance.Validate([observation, observation], [manifest]));
        Assert.Throws<InvalidDataException>(
            () => ClientRuntimeAssemblyProvenance.Validate([observation], [manifest, manifest]));
        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput("client", typeof(ClientRuntimeAssemblyProvenanceTests).Assembly),
            new ClientRuntimeAssemblyInput("client", typeof(AuthoritativeClient).Assembly),
        ]));
    }

    [Fact]
    public void AcceptsOptimizedReleaseConfigurationsWhenMetadataConfirmsOptimization()
    {
        var observation = Assert.Single(ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput(
                "client", typeof(ClientRuntimeAssemblyProvenanceTests).Assembly),
        ]));
        var assembly = typeof(ClientRuntimeAssemblyProvenanceTests).Assembly;
        foreach (var configuration in new[] { "Release", "ExportRelease" }) {
            // Metadata-only cases exercise both optimized Godot configurations while
            // retaining the real loaded assembly's identity and file provenance.
            var optimizedMetadataOnly = observation with {
                Configuration = configuration,
                HasDebuggableAttribute = true,
                DebuggableModes = 0,
                OptimizationsDisabled = false,
            };
            var expected = ManifestForAssembly("client", assembly) with {
                Configuration = configuration,
                RequireOptimized = true,
            };
            ClientRuntimeAssemblyProvenance.Validate([optimizedMetadataOnly], [expected]);
        }

        if ((observation.Configuration is "Release" or "ExportRelease")
            && observation.HasDebuggableAttribute
            && observation.OptimizationsDisabled == false) {
            ClientRuntimeAssemblyProvenance.Validate(
                [observation], [ManifestForAssembly("client", assembly, requireOptimized: true)]);
        }

        var missingConfiguration = observation with {
            HasConfigurationAttribute = false,
            Configuration = null,
        };
        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
            [missingConfiguration], [ManifestForAssembly("client", assembly)]));
    }

    [Theory]
    [InlineData("Debug")]
    [InlineData("ExportDebug")]
    [InlineData("Unknown")]
    public void RejectsDebugOrUnknownConfigurationsWhenOptimizationIsRequired(string configuration)
    {
        var assembly = typeof(ClientRuntimeAssemblyProvenanceTests).Assembly;
        var observation = Assert.Single(ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput("client", assembly),
        ]));
        var metadataOnly = observation with {
            Configuration = configuration,
            HasDebuggableAttribute = true,
            DebuggableModes = 0,
            OptimizationsDisabled = false,
        };
        var expected = ManifestForAssembly("client", assembly) with {
            Configuration = configuration,
            RequireOptimized = true,
        };

        Assert.Throws<InvalidDataException>(
            () => ClientRuntimeAssemblyProvenance.Validate([metadataOnly], [expected]));
    }

    [Theory]
    [InlineData("Release")]
    [InlineData("ExportRelease")]
    public void RejectsMissingOrDisabledOptimizationMetadata(string configuration)
    {
        var assembly = typeof(ClientRuntimeAssemblyProvenanceTests).Assembly;
        var observation = Assert.Single(ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput("client", assembly),
        ]));
        var expected = ManifestForAssembly("client", assembly) with {
            Configuration = configuration,
            RequireOptimized = true,
        };
        var missingDebuggableMetadata = observation with {
            Configuration = configuration,
            HasDebuggableAttribute = false,
            DebuggableModes = null,
            OptimizationsDisabled = null,
        };
        var disabledOptimizations = observation with {
            Configuration = configuration,
            HasDebuggableAttribute = true,
            DebuggableModes = (int)DebuggableAttribute.DebuggingModes.DisableOptimizations,
            OptimizationsDisabled = true,
        };

        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
            [missingDebuggableMetadata], [expected]));
        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
            [disabledOptimizations], [expected]));
    }

    [Fact]
    public void RejectsDynamicAssembliesWithoutAFileLocation()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("RuntimeProvenanceDynamicFixture"),
            AssemblyBuilderAccess.Run);

        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Capture([
            new ClientRuntimeAssemblyInput("dynamic", assembly),
        ]));
        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
            new ClientRuntimeAssemblyImageInput(
                "dynamic", assembly, typeof(ClientRuntimeAssemblyProvenanceTests).Assembly.Location),
        ]));
    }

    [Fact]
    public void CapturesMatchingStagedImageForLoadFromStreamAssembly()
    {
        var sourceAssembly = typeof(AuthoritativeClient).Assembly;
        var sourcePath = sourceAssembly.Location;
        var (loadContext, inMemoryAssembly) = LoadFromStream(sourcePath);
        try {
            Assert.Equal(string.Empty, inMemoryAssembly.Location);
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Capture([
                new ClientRuntimeAssemblyInput("client-protocol", inMemoryAssembly),
            ]));

            var observation = Assert.Single(ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                new ClientRuntimeAssemblyImageInput("client-protocol", inMemoryAssembly, sourcePath),
            ]));
            AssertStagedImageMatches(observation, inMemoryAssembly, sourcePath);
            Assert.Equal(
                ClientRuntimeAssemblyImageBindingKind.StagedImageWithMatchingMvid,
                observation.ImageBindingKind);

            var manifest = ManifestForStagedImage(
                "client-protocol",
                inMemoryAssembly,
                sourcePath,
                ClientRuntimeAssemblyImageBindingKind.StagedImageWithMatchingMvid);
            ClientRuntimeAssemblyProvenance.Validate([observation], [manifest]);

            var serialized = JsonSerializer.Serialize(observation);
            Assert.Contains("\"ImageBindingKind\":\"StagedImageWithMatchingMvid\"", serialized);
            Assert.DoesNotContain(JsonSerializer.Serialize(sourcePath), serialized);
            Assert.Equal(observation, JsonSerializer.Deserialize<ClientRuntimeAssemblyObservation>(serialized)!);
            var serializedManifest = JsonSerializer.Serialize(manifest);
            Assert.Contains("\"ImageBindingKind\":\"StagedImageWithMatchingMvid\"", serializedManifest);
            Assert.Equal(manifest, JsonSerializer.Deserialize<ClientRuntimeAssemblyManifestEntry>(serializedManifest)!);
            var malformedBindingKind = serialized.Replace(
                "\"ImageBindingKind\":\"StagedImageWithMatchingMvid\"",
                "\"ImageBindingKind\":\"UnexpectedBinding\"",
                StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClientRuntimeAssemblyObservation>(malformedBindingKind));

            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
                [observation], [manifest with { ImageBindingKind = ClientRuntimeAssemblyImageBindingKind.LoadedFile }]));
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
                [observation with { ImageBindingKind = (ClientRuntimeAssemblyImageBindingKind)99 }], [manifest]));
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
                [observation], [manifest with { ImageBindingKind = (ClientRuntimeAssemblyImageBindingKind)99 }]));
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
                [observation], [manifest with { Sha256 = new string('0', 64) }]));

            var optimizedWithoutMetadata = observation with {
                Configuration = "ExportRelease",
                HasDebuggableAttribute = false,
                DebuggableModes = null,
                OptimizationsDisabled = null,
            };
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.Validate(
                [optimizedWithoutMetadata], [manifest with {
                    Configuration = "ExportRelease",
                    RequireOptimized = true,
                }]));

            var pathLoadedObservation = Assert.Single(ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                new ClientRuntimeAssemblyImageInput("client-protocol-file", sourceAssembly, sourcePath),
            ]));
            Assert.Equal(ClientRuntimeAssemblyImageBindingKind.LoadedFile, pathLoadedObservation.ImageBindingKind);
            ClientRuntimeAssemblyProvenance.Validate([pathLoadedObservation], [ManifestForStagedImage(
                "client-protocol-file",
                sourceAssembly,
                sourcePath,
                ClientRuntimeAssemblyImageBindingKind.LoadedFile)]);

        } finally {
            loadContext.Unload();
        }
    }

    [Fact]
    public void CapturesSignedRuntimeAssemblyFullIdentityFromItsStagedFile()
    {
        var runtimeAssembly = typeof(object).Assembly;
        var runtimePath = runtimeAssembly.Location;
        Assert.True(Path.IsPathFullyQualified(runtimePath));
        Assert.True(File.Exists(runtimePath));
        var publicKeyToken = runtimeAssembly.GetName().GetPublicKeyToken();
        Assert.NotNull(publicKeyToken);
        Assert.NotEmpty(publicKeyToken);

        var observation = Assert.Single(ClientRuntimeAssemblyProvenance.CaptureStagedImages([
            new ClientRuntimeAssemblyImageInput("runtime-core", runtimeAssembly, runtimePath),
        ]));
        Assert.Equal(runtimeAssembly.FullName, observation.FullName);
        Assert.Equal(
            Convert.ToHexString(publicKeyToken),
            Convert.ToHexString(new AssemblyName(observation.FullName).GetPublicKeyToken()!));
        Assert.Equal(ClientRuntimeAssemblyImageBindingKind.LoadedFile, observation.ImageBindingKind);
    }

    [Fact]
    public void RejectsStagedImagesWithWrongIdentityMvidOrLoadedPath()
    {
        var sourceAssembly = typeof(AuthoritativeClient).Assembly;
        var sourcePath = sourceAssembly.Location;
        var (loadContext, inMemoryAssembly) = LoadFromStream(sourcePath);
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"runtime-provenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try {
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                new ClientRuntimeAssemblyImageInput(
                    "client-protocol", inMemoryAssembly, typeof(ClientRuntimeAssemblyProvenanceTests).Assembly.Location),
            ]));

            var differentMvidPath = Path.Combine(temporaryDirectory, "different-mvid.dll");
            var imageBytes = File.ReadAllBytes(sourcePath);
            var sourceMvidBytes = inMemoryAssembly.ManifestModule.ModuleVersionId.ToByteArray();
            var replacementMvidBytes = Guid.NewGuid().ToByteArray();
            var mvidOffset = FindSingleByteSequence(imageBytes, sourceMvidBytes);
            replacementMvidBytes.CopyTo(imageBytes, mvidOffset);
            File.WriteAllBytes(differentMvidPath, imageBytes);
            // This proves MVID binding detects a changed staged image; matching MVID does not
            // prove byte-for-byte identity of arbitrary in-memory bytes.
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                new ClientRuntimeAssemblyImageInput("client-protocol", inMemoryAssembly, differentMvidPath),
            ]));

            var copiedImagePath = Path.Combine(temporaryDirectory, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, copiedImagePath);
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                new ClientRuntimeAssemblyImageInput("client-protocol", sourceAssembly, copiedImagePath),
            ]));
        } finally {
            loadContext.Unload();
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void RejectsMissingMalformedNativeAndDuplicateStagedImages()
    {
        var assembly = typeof(AuthoritativeClient).Assembly;
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dll");
        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
            new ClientRuntimeAssemblyImageInput("client-protocol", assembly, missingPath),
        ]));

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid():N}.dll");
        try {
            File.WriteAllBytes(temporaryPath, [0x4D, 0x5A, 0x01, 0x02, 0x03]);
            Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                new ClientRuntimeAssemblyImageInput("client-protocol", assembly, temporaryPath),
            ]));

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath)) {
                Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                    new ClientRuntimeAssemblyImageInput("client-protocol", assembly, processPath),
                ]));
            }
        } finally {
            File.Delete(temporaryPath);
        }

        Assert.Throws<InvalidDataException>(() => ClientRuntimeAssemblyProvenance.CaptureStagedImages([
            new ClientRuntimeAssemblyImageInput("client-protocol", assembly, assembly.Location),
            new ClientRuntimeAssemblyImageInput("client-protocol", assembly, assembly.Location),
        ]));
    }

    private static void AssertAssemblyMatches(
        ClientRuntimeAssemblyObservation observation,
        Assembly assembly,
        string role)
    {
        var name = assembly.GetName();
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
        var debuggable = assembly.GetCustomAttribute<DebuggableAttribute>();
        int? expectedModes = debuggable is null ? null : (int)debuggable.DebuggingFlags;
        bool? expectedOptimizationsDisabled = debuggable is null
            ? null
            : (debuggable.DebuggingFlags & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
        Assert.Equal(role, observation.Role);
        Assert.Equal(name.Name, observation.AssemblyName);
        Assert.Equal(name.Version?.ToString(), observation.AssemblyVersion);
        Assert.Equal(assembly.FullName, observation.FullName);
        Assert.Equal(assembly.ManifestModule.ModuleVersionId.ToString("D"), observation.ModuleVersionId);
        Assert.True(observation.HasConfigurationAttribute);
        Assert.NotNull(configuration);
        Assert.Equal(configuration.Configuration, observation.Configuration);
        Assert.Equal(Path.GetFileName(assembly.Location), observation.FileName);
        using var stream = File.OpenRead(assembly.Location);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(stream)), observation.Sha256);
        Assert.Equal(
            ClientRuntimeAssemblyProvenance.CanonicalLocationSha256(assembly.Location),
            observation.CanonicalLocationSha256);
        Assert.Equal(debuggable is not null, observation.HasDebuggableAttribute);
        Assert.Equal(expectedModes, observation.DebuggableModes);
        Assert.Equal(expectedOptimizationsDisabled, observation.OptimizationsDisabled);
    }

    private static ClientRuntimeAssemblyManifestEntry ManifestForAssembly(
        string role,
        Assembly assembly,
        bool requireOptimized = false)
    {
        var name = assembly.GetName();
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?? throw new InvalidOperationException("The test assembly configuration attribute is missing.");
        var location = Path.GetFullPath(assembly.Location);
        var canonicalLocation = OperatingSystem.IsWindows()
            ? location.ToUpperInvariant()
            : location;
        using var stream = File.OpenRead(location);
        return new ClientRuntimeAssemblyManifestEntry(
            role,
            name.Name!,
            name.Version!.ToString(),
            assembly.FullName!,
            assembly.ManifestModule.ModuleVersionId.ToString("D"),
            configuration.Configuration,
            Convert.ToHexString(SHA256.HashData(stream)),
            Path.GetFileName(location),
            Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalLocation))),
            requireOptimized);
    }

    private static (AssemblyLoadContext Context, Assembly Assembly) LoadFromStream(string assemblyPath)
    {
        var context = new AssemblyLoadContext($"runtime-provenance-{Guid.NewGuid():N}", isCollectible: true);
        using var stream = File.OpenRead(assemblyPath);
        return (context, context.LoadFromStream(stream));
    }

    private static void AssertStagedImageMatches(
        ClientRuntimeAssemblyObservation observation,
        Assembly assembly,
        string imagePath)
    {
        var name = assembly.GetName();
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
        var debuggable = assembly.GetCustomAttribute<DebuggableAttribute>();
        int? expectedModes = debuggable is null ? null : (int)debuggable.DebuggingFlags;
        bool? expectedOptimizationsDisabled = debuggable is null
            ? null
            : (debuggable.DebuggingFlags & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
        Assert.Equal(name.Name, observation.AssemblyName);
        Assert.Equal(name.Version?.ToString(), observation.AssemblyVersion);
        Assert.Equal(assembly.FullName, observation.FullName);
        Assert.Equal(assembly.ManifestModule.ModuleVersionId.ToString("D"), observation.ModuleVersionId);
        Assert.Equal(configuration is not null, observation.HasConfigurationAttribute);
        Assert.Equal(configuration?.Configuration, observation.Configuration);
        Assert.Equal(Path.GetFileName(imagePath), observation.FileName);
        using var stream = File.OpenRead(imagePath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(stream)), observation.Sha256);
        Assert.Equal(
            ClientRuntimeAssemblyProvenance.CanonicalLocationSha256(imagePath),
            observation.CanonicalLocationSha256);
        Assert.Equal(debuggable is not null, observation.HasDebuggableAttribute);
        Assert.Equal(expectedModes, observation.DebuggableModes);
        Assert.Equal(expectedOptimizationsDisabled, observation.OptimizationsDisabled);
    }

    private static ClientRuntimeAssemblyManifestEntry ManifestForStagedImage(
        string role,
        Assembly assembly,
        string imagePath,
        ClientRuntimeAssemblyImageBindingKind imageBindingKind)
    {
        var name = assembly.GetName();
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?? throw new InvalidOperationException("The protocol assembly configuration attribute is missing.");
        var normalizedPath = Path.GetFullPath(imagePath);
        using var stream = File.OpenRead(normalizedPath);
        return new ClientRuntimeAssemblyManifestEntry(
            role,
            name.Name!,
            name.Version!.ToString(),
            assembly.FullName!,
            assembly.ManifestModule.ModuleVersionId.ToString("D"),
            configuration.Configuration,
            Convert.ToHexString(SHA256.HashData(stream)),
            Path.GetFileName(normalizedPath),
            ClientRuntimeAssemblyProvenance.CanonicalLocationSha256(normalizedPath),
            RequireOptimized: false,
            ImageBindingKind: imageBindingKind);
    }

    private static int FindSingleByteSequence(byte[] source, byte[] sequence)
    {
        var matches = new List<int>();
        for (var offset = 0; offset <= source.Length - sequence.Length; offset++) {
            if (source.AsSpan(offset, sequence.Length).SequenceEqual(sequence))
                matches.Add(offset);
        }
        return Assert.Single(matches);
    }
}
