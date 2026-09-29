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

    [Fact]
    public void NativeAcceptanceUsesFreshIsolatedPathsWithoutChangingNormalSaveLocations()
    {
        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "run-authoritative-client.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools/run-native-acceptance.ps1"));
        Assert.Contains("param()", launcher, StringComparison.Ordinal);
        Assert.Contains("param()", wrapper, StringComparison.Ordinal);
        Assert.Contains("$env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY", launcher, StringComparison.Ordinal);
        var freshPathGuard = launcher.IndexOf("if (Test-Path -LiteralPath $candidate)", StringComparison.Ordinal);
        Assert.True(freshPathGuard >= 0);
        Assert.True(freshPathGuard < launcher.IndexOf("--target devilutionx", StringComparison.Ordinal));
        Assert.Matches(@"(?s)if \(\$isAcceptanceRun\) \{\s*\$clientArguments \+= @\(.*?'--save-dir'.*?'--config-dir'", launcher);
        Assert.Single(Regex.Matches(launcher, "'--save-dir'"));
        Assert.Single(Regex.Matches(launcher, "'--config-dir'"));
        Assert.Contains("Fullscreen=0", launcher, StringComparison.Ordinal);
        Assert.Contains("'--diablo'", launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("'--hellfire'", launcher, StringComparison.Ordinal);
        Assert.Contains("'--log-to-file'", launcher, StringComparison.Ordinal);
        Assert.Contains("Join-Path $captureRoot 'native-save'", launcher, StringComparison.Ordinal);
        Assert.Contains("Join-Path $captureRoot 'native-config'", launcher, StringComparison.Ordinal);
        Assert.Contains("$env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY = $previousAcceptanceRoot", wrapper, StringComparison.Ordinal);
        Assert.Contains("Remove-Item Env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY", wrapper, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeLauncherOwnsClientBeforeWaitingAndCleansBothProcesses()
    {
        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "run-authoritative-client.ps1"));
        Assert.Matches(@"(?m)^\s*\$serverProcess = Start-Process[^\r\n]*-WindowStyle Hidden", launcher);
        var clientLaunch = Regex.Match(launcher, @"(?m)^\s*\$clientProcess = Start-Process[^\r\n]*");
        Assert.True(clientLaunch.Success);
        Assert.DoesNotContain("-Wait", clientLaunch.Value, StringComparison.OrdinalIgnoreCase);
        Assert.True(launcher.IndexOf("$clientProcess.WaitForExit(250)", StringComparison.Ordinal) > clientLaunch.Index);
        Assert.Contains("Join-Path $captureRoot 'processes.json'", launcher, StringComparison.Ordinal);
        Assert.Contains("clientStartedUtc = $clientProcess.StartTime.ToUniversalTime()", launcher, StringComparison.Ordinal);
        Assert.Contains("serverStartedUtc = $serverProcess.StartTime.ToUniversalTime()", launcher, StringComparison.Ordinal);
        var cleanup = launcher[launcher.IndexOf("finally {", StringComparison.Ordinal)..];
        Assert.Contains("Stop-Process -Id $clientProcess.Id", cleanup, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Id $serverProcess.Id", cleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Process", launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void GodotLauncherBuildsBothProjectsAndOwnsOnlyItsProcesses()
    {
        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "godot/run-local-client.ps1"));
        var launch = launcher.IndexOf("$serverProcess = Start-Process", StringComparison.Ordinal);
        Assert.True(launch > launcher.IndexOf("dotnet build $serverProject", StringComparison.Ordinal));
        Assert.True(launch > launcher.IndexOf("dotnet build (Join-Path $clientProject", StringComparison.Ordinal));
        Assert.DoesNotContain("Get-Process", launcher);
        Assert.Contains("$gameMode -ne 'SinglePlayer'", launcher);
        Assert.Contains("DEVILUTION_RESUME_TOKEN_PATH", launcher);
        Assert.Contains("$savedEnvironment[$name]", launcher);
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
