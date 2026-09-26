using System.Text.RegularExpressions;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class LocalLauncherContractTests
{
    [Theory]
    [InlineData("run-authoritative-client.ps1")]
    [InlineData("godot/run-local-client.ps1")]
    public void NewGameLaunchersPinTheServerToSinglePlayer(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var launcher = File.ReadAllText(Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var assignment = Regex.Match(
            launcher,
            @"(?m)^[ \t]*\$serverArguments[ \t]*=[ \t]*(?<expression>@\((?s:.*?)\)|[^\r\n]+)");
        Assert.True(
            assignment.Success,
            $"{relativePath} must assign the authoritative server arguments explicitly.");

        var expression = Regex.Replace(
            assignment.Groups["expression"].Value,
            @"(?m)#.*$",
            string.Empty);
        Assert.Single(Regex.Matches(
            expression,
            "--game-mode",
            RegexOptions.IgnoreCase));
        var normalized = Regex.Replace(expression, "['\",=]+", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        Assert.Contains("--game-mode single-player", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--game-mode multiplayer", normalized, StringComparison.OrdinalIgnoreCase);

        var scriptAfterAssignment = launcher[(assignment.Index + assignment.Length)..];
        Assert.Single(Regex.Matches(
            scriptAfterAssignment,
            @"\$serverArguments\b",
            RegexOptions.IgnoreCase));
        Assert.Matches(
            @"(?im)^.*Start-Process\b[^\r\n]*-ArgumentList[ \t]+\$serverArguments\b",
            scriptAfterAssignment);
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
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }
}
