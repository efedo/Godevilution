using System.Xml.Linq;
using System.Text.RegularExpressions;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientBoundaryTests
{
    [Fact]
    public void GodotSessionCallbacksMustNotCaptureRetiredPresentationModel()
    {
        var root = FindRepositoryRoot();
        var main = File.ReadAllText(Path.Combine(root, "godot-client", "Devilution.Client", "Main.cs"));
        var session = File.ReadAllText(Path.Combine(root, "godot-client", "Devilution.Client", "Main.Session.cs"));

        Assert.Contains("model = new AuthoritativeClientModel()", session, StringComparison.Ordinal);
        Assert.False(Regex.IsMatch(main, @"hud\.\w+\s*\+=\s*model\."),
            "A HUD callback captures a model that session return replaces; resolve the current model when invoked.");
    }

    [Fact]
    public void ProtocolClientReferencesOnlyTheSharedProtocolAssembly()
    {
        var references = typeof(AuthoritativeClientModel).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("Devilution.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Devilution.Protocol"], references);
    }

    [Fact]
    public void GodotClientProjectReferencesOnlyProtocolProjects()
    {
        var projectPath = Path.Combine(
            FindRepositoryRoot(),
            "godot-client",
            "Devilution.Client",
            "Devilution.Client.csproj");
        var project = XDocument.Load(projectPath);
        var references = project
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "../../protocol/Devilution.Protocol/Devilution.Protocol.csproj",
                "../Devilution.Client.Protocol/Devilution.Client.Protocol.csproj",
            ],
            references);
        Assert.DoesNotContain(
            references,
            reference => reference.Contains("server", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            reference => reference.Contains("Diablo", StringComparison.Ordinal));
    }

    [Fact]
    public void GodotProjectUsesVulkanWithoutOpenGlFallback()
    {
        var root = FindRepositoryRoot();
        var rendering = ReadGodotSection(
            Path.Combine(root, "godot-client", "Devilution.Client", "project.godot"),
            "rendering");

        // The editor omits engine defaults when it saves, so a missing key means
        // Godot 4.7's Forward+/Mobile and Vulkan defaults.
        string Setting(string key, string engineDefault)
            => rendering.TryGetValue(key, out var value) ? value : engineDefault;
        Assert.Equal("\"forward_plus\"", Setting("renderer/rendering_method", "\"forward_plus\""));
        Assert.Equal("\"mobile\"", Setting("renderer/rendering_method.mobile", "\"mobile\""));
        foreach (var key in new[] {
            "rendering_device/driver",
            "rendering_device/driver.windows",
            "rendering_device/driver.linuxbsd",
        })
            Assert.Equal("\"vulkan\"", Setting(key, "\"vulkan\""));
        Assert.Equal("false", Assert.Contains("rendering_device/fallback_to_opengl3", rendering));
        Assert.DoesNotContain(rendering.Values, value => value.Contains("gl_compatibility", StringComparison.Ordinal));
    }

    private static Dictionary<string, string> ReadGodotSection(string path, string section)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var inSection = false;
        foreach (var rawLine in File.ReadLines(path)) {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
                continue;
            if (line.StartsWith('[')) {
                inSection = line == $"[{section}]";
                continue;
            }
            var separator = line.IndexOf('=');
            if (inSection && separator > 0)
                values[line[..separator]] = line[(separator + 1)..];
        }
        return values;
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
}
