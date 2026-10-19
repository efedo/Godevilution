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
