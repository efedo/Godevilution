using System.Text.Json;
using Devilution.Client.Diagnostics;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientFrameAssemblyEvidenceTests
{
    [Fact]
    public void PreservesExplicitAssemblyEvidenceWithoutSerializingFullLocations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"Godevilution-FrameAssembly-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try {
            var legacyMetadata = new ClientFrameEvidenceMetadata(
                "test", "schema", "content", "rules", null, null, null,
                null, null, true, null, 1);
            Assert.Null(legacyMetadata.RuntimeAssemblies);
            var assemblies = ClientRuntimeAssemblyProvenance.Capture([
                new ClientRuntimeAssemblyInput("client", typeof(ClientFrameAssemblyEvidenceTests).Assembly),
                new ClientRuntimeAssemblyInput("client-protocol", typeof(AuthoritativeClient).Assembly),
            ]);
            var recorder = ClientFrameEvidenceRecorder.Create(
                new ClientFrameEvidenceConfiguration(
                    Path.Combine(root, "capture"), TimeSpan.Zero, TimeSpan.FromSeconds(1), 4),
                legacyMetadata with { RuntimeAssemblies = assemblies });
            Assert.True(recorder.Stop("metadata-inspection", succeeded: false));
            var json = File.ReadAllText(recorder.EvidenceFilePath);
            var document = JsonSerializer.Deserialize<ClientFrameEvidenceDocument>(json);
            Assert.NotNull(document);
            Assert.Equal(assemblies, document.Configuration.RuntimeAssemblies);
            Assert.False(document.Outcome.Succeeded);

            using var parsed = JsonDocument.Parse(json);
            var observations = parsed.RootElement.GetProperty("Configuration").GetProperty("RuntimeAssemblies");
            var publicFields = new HashSet<string>(StringComparer.Ordinal) {
                "Role", "AssemblyName", "AssemblyVersion", "FullName", "ModuleVersionId",
                "HasConfigurationAttribute", "Configuration", "Sha256", "FileName",
                "CanonicalLocationSha256", "HasDebuggableAttribute", "DebuggableModes",
                "OptimizationsDisabled", "ImageBindingKind",
            };
            Assert.Equal(2, observations.GetArrayLength());
            foreach (var observation in observations.EnumerateArray()) {
                Assert.Equal(publicFields.Count, observation.EnumerateObject().Count());
                Assert.All(observation.EnumerateObject(), property => Assert.Contains(property.Name, publicFields));
                Assert.False(Path.IsPathFullyQualified(observation.GetProperty("FileName").GetString()!));
                Assert.Equal("LoadedFile", observation.GetProperty("ImageBindingKind").GetString());
            }
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
