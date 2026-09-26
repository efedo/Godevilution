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

    [Fact]
    public void NativeLauncherBuildsAnAuthoritativeClientAndValidatesServerMode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var launcher = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "run-authoritative-client.ps1"));

        Assert.Contains(
            "DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT:BOOL=ON",
            launcher,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "build/x64-Debug/devilutionx.exe",
            launcher,
            StringComparison.OrdinalIgnoreCase);

        var nativeBuild = launcher.IndexOf(
            "--target devilutionx",
            StringComparison.OrdinalIgnoreCase);
        var serverBuild = launcher.IndexOf(
            "dotnet build",
            StringComparison.OrdinalIgnoreCase);
        var clientLaunch = launcher.IndexOf(
            "$clientProcess = Start-Process",
            StringComparison.OrdinalIgnoreCase);
        Assert.True(nativeBuild >= 0, "The launcher must build the native client before use.");
        Assert.True(serverBuild >= 0, "The launcher must build the authoritative server before use.");
        Assert.True(clientLaunch > nativeBuild, "The native build must precede client launch.");
        Assert.True(clientLaunch > serverBuild, "The server build must precede client launch.");

        Assert.Matches(
            @"\[regex\]::Match\(\$output,\s*'Game mode:",
            launcher);
        Assert.Contains(
            "$gameMode -ne 'SinglePlayer'",
            launcher,
            StringComparison.Ordinal);
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
