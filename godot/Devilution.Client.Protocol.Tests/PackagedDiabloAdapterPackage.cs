using System.Security.Cryptography;
using System.Text.Json;
using Godevilution.Server.Diablo.Package;

namespace Devilution.Client.Protocol.Tests;

/** Creates a disposable production adapter package for live protocol tests. */
internal sealed class PackagedDiabloAdapterPackage : IDisposable
{
    public PackagedDiabloAdapterPackage(string version = "0.1.0")
    {
        Version = version;
        Root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-live-diablo-package",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        var files = new[] {
            ("adapter.dll", typeof(PackagedDiabloAuthoritativeGameHostAdapterFactory).Assembly.Location),
            ("server-diablo.dll", typeof(Devilution.Server.Gameplay.DiabloAuthoritativeGameHostAdapterFactory).Assembly.Location),
            ("diablo.dll", typeof(Devilution.Diablo.DiabloModule).Assembly.Location),
            ("domain.dll", typeof(Devilution.Domain.Protocol.AuthoritativeLevelIdentityRules).Assembly.Location),
        };
        foreach (var (name, source) in files)
            File.Copy(source, Path.Combine(Root, name));

        var manifest = new {
            formatVersion = 1,
            id = "godevilution-diablo-package",
            version,
            assembly = "adapter.dll",
            assemblySha256 = Hash(Path.Combine(Root, "adapter.dll")),
            factoryType = "Godevilution.Server.Diablo.Package.PackagedDiabloAuthoritativeGameHostAdapterFactory",
            sharedContracts = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["Devilution.Data"] = Hash(typeof(Devilution.Data.ContentManifest).Assembly.Location),
                ["Devilution.Engine"] = Hash(typeof(Devilution.Engine.IGameplayModule).Assembly.Location),
                ["Devilution.Server.Host"] = Hash(typeof(Devilution.Server.Host.IAuthoritativeGameHostAdapterFactory).Assembly.Location),
                ["Devilution.Protocol"] = Hash(typeof(Devilution.Protocol.V1.Command).Assembly.Location),
                ["Google.Protobuf"] = Hash(typeof(Google.Protobuf.IMessage).Assembly.Location),
            },
            dependencies = new[] {
                new { assembly = "server-diablo.dll", assemblySha256 = Hash(Path.Combine(Root, "server-diablo.dll")) },
                new { assembly = "diablo.dll", assemblySha256 = Hash(Path.Combine(Root, "diablo.dll")) },
                new { assembly = "domain.dll", assemblySha256 = Hash(Path.Combine(Root, "domain.dll")) },
            },
        };
        File.WriteAllText(
            Path.Combine(Root, "game-host-adapter.json"),
            JsonSerializer.Serialize(manifest));
    }

    public string Root { get; }

    public string Version { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    private static string Hash(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
