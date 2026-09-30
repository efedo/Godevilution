using System.Text.RegularExpressions;
using Devilution.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class LocalLauncherContractTests
{
    [Theory]
    [InlineData(nameof(AuthoritativeProtocolLimits.MinimumNativeRewardOffset), AuthoritativeProtocolLimits.MinimumNativeRewardOffset)]
    [InlineData(nameof(AuthoritativeProtocolLimits.MaximumNativeRewardOffset), AuthoritativeProtocolLimits.MaximumNativeRewardOffset)]
    public void NativeInitialSnapshotRewardLimitsMatchThePublishedContract(string name, int expected)
    {
        var nativeLimits = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "Source/network/authoritative/protocol_limits.hpp"));
        var declaration = Regex.Match(nativeLimits,
            $@"inline constexpr int {Regex.Escape(name)} = (-?\d+);");
        Assert.True(declaration.Success, $"Missing native protocol limit {name}.");
        Assert.Equal(expected, int.Parse(declaration.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("run-authoritative-client.ps1")]
    [InlineData("godot/run-local-client.ps1")]
    public void NewGameLaunchersDefaultToSinglePlayer(string relativePath)
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
        if (relativePath.StartsWith("godot/", StringComparison.Ordinal)) {
            Assert.Contains("--game-mode {4}", normalized, StringComparison.Ordinal);
            Assert.Contains("$serverGameMode = if ($Multiplayer) { 'multiplayer' } else { 'single-player' }", launcher);
            Assert.Contains("$overlayArguments, $serverGameMode", expression);
            Assert.DoesNotContain("$Multiplayer = $true", launcher);
            var wrapper = File.ReadAllText(Path.Combine(repositoryRoot, "run-godot-client.ps1"));
            Assert.Contains("& $godotLauncher", wrapper);
            Assert.DoesNotContain("Multiplayer", wrapper);
        } else {
            Assert.Contains("--game-mode single-player", normalized, StringComparison.OrdinalIgnoreCase);
        }
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
        Assert.Contains("--command-scheduling clock-gated", launcher, StringComparison.Ordinal);
        Assert.Contains("Unexpected command scheduling:", launcher, StringComparison.Ordinal);
        var launch = launcher.IndexOf("$serverProcess = Start-Process", StringComparison.Ordinal);
        var serverBuild = launcher.IndexOf("dotnet build $serverProject", StringComparison.Ordinal);
        var clientBuild = launcher.IndexOf("dotnet build (Join-Path $clientProject", StringComparison.Ordinal);
        var imported = launcher.IndexOf("Assert-GodotImportLog $importTracked $importLogPath", StringComparison.Ordinal);
        Assert.True(serverBuild >= 0 && launch > serverBuild);
        Assert.True(imported >= 0 && clientBuild > imported && launch > clientBuild,
            "The imported staged client must build successfully before the host starts.");
        Assert.Contains("--path \"{0}\"", launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Process", launcher);
        Assert.Contains("$gameMode -ne $expectedGameMode", launcher);
        Assert.Contains("$expectedGameMode = if ($Multiplayer) { 'Multiplayer' } else { 'SinglePlayer' }", launcher);
        Assert.Contains("DEVILUTION_RESUME_TOKEN_PATH", launcher);
        Assert.Contains("$savedEnvironment[$name]", launcher);
    }

    [Fact]
    public void AuthoredGodotPreviewIsOptInWithAnIsolatedSaveDirectory()
    {
        var root = FindRepositoryRoot();
        var normal = File.ReadAllText(Path.Combine(root, "run-godot-client.ps1"));
        var preview = File.ReadAllText(Path.Combine(root, "run-godot-preview.ps1"));
        var harness = File.ReadAllText(Path.Combine(root, "godot/run-local-client.ps1"));
        var acceptance = File.ReadAllText(Path.Combine(root, "tools/test-godot-session.ps1"));
        Assert.Contains("param()", preview);
        Assert.DoesNotContain("PreviewContent", normal);
        Assert.Contains("& $godotLauncher -PreviewContent", preview);
        Assert.Contains("Test-Path -LiteralPath $godotLauncher -PathType Leaf", preview);
        Assert.Contains("if ($PreviewContent) { '.local/godot-preview' } else { '.local/godot' }", harness);
        Assert.Contains("$overlayArguments = if ($PreviewContent -or $PortalPreviewContent)", harness);
        Assert.Contains("--content-overlay", harness);
        Assert.Contains("server/content/town-loop-preview", harness);
        Assert.Contains("Town-loop acceptance requires -PreviewContent", harness);
        Assert.Contains("$parameters.PortalPreviewContent = $true", acceptance);
        Assert.Contains("-AcceptancePhase TownLoopResume", acceptance);
        Assert.Contains("if ($Journey -and $TownLoop)", acceptance);
    }

    [Fact]
    public void PortalPreviewLayersOnTheOriginalPreviewWithoutReusingItsSaves()
    {
        var root = FindRepositoryRoot();
        var original = File.ReadAllText(Path.Combine(root, "run-godot-preview.ps1"));
        var portal = File.ReadAllText(Path.Combine(root, "run-godot-portal-preview.ps1"));
        var harness = File.ReadAllText(Path.Combine(root, "godot/run-local-client.ps1"));
        Assert.Contains("param()", portal);
        Assert.Contains("& $godotLauncher -PreviewContent", original);
        Assert.DoesNotContain("PortalPreviewContent", original);
        Assert.Contains("& $godotLauncher -PortalPreviewContent", portal);
        Assert.Contains("'.local/godot-portal-preview'", harness);
        Assert.Contains("'.local/godot-preview'", harness);
        Assert.Contains("server/content/town-portal-preview", harness);
        Assert.True(harness.IndexOf("server/content/town-loop-preview", StringComparison.Ordinal)
            < harness.IndexOf("server/content/town-portal-preview", StringComparison.Ordinal));
        Assert.Contains("if ($PreviewContent -and $PortalPreviewContent)", harness);
        Assert.Contains("GODEVILUTION_PORTAL_PREVIEW", harness);
        Assert.Contains("$clientEnvironment['GODEVILUTION_PORTAL_PREVIEW'] = '1'", harness);
        Assert.Contains("-not ($PreviewContent -or $PortalPreviewContent)", harness);
    }

    [Fact]
    public void GodotMultiplayerOwnsOneHostAndTwoIsolatedClientProcesses()
    {
        var root = FindRepositoryRoot();
        var wrapper = File.ReadAllText(Path.Combine(root, "run-godot-multiplayer.ps1"));
        var launcher = File.ReadAllText(Path.Combine(root, "godot/run-local-client.ps1"));
        var acceptance = File.ReadAllText(Path.Combine(root, "tools/test-godot-session.ps1"));
        Assert.Contains("param()", wrapper);
        Assert.Contains("& $godotLauncher -Multiplayer", wrapper);
        Assert.Contains("Test-Path -LiteralPath $godotLauncher -PathType Leaf", wrapper);
        Assert.Contains("if ($Multiplayer) { '.local/godot-multiplayer' }", launcher);
        Assert.Contains("Join-Path $SessionDirectory \"player-$Slot\"", launcher);
        Assert.Contains("DEVILUTION_RESUME_TOKEN_PATH = Join-Path $clientDirectory 'resume-token.txt'", launcher);
        Assert.Contains("$clientEnvironment['GODEVILUTION_LOCAL_PLAYER']", launcher);
        Assert.Contains("$startInfo.WindowStyle = if ($Headless) { 'Hidden' } else { 'Normal' }", launcher);
        Assert.Contains("$startedClients += Start-SceneClient 1", launcher);
        Assert.Contains("if ($Multiplayer) { $startedClients += Start-SceneClient 2 }", launcher);
        Assert.Single(Regex.Matches(launcher, @"\$startedServer = Start-SessionServer"));
        Assert.Contains("$startedClients | ForEach-Object { $_.Process }", launcher);
        Assert.Contains("Complete-GodotProcess $startedClient.Tracked 0 'Godot client'", launcher);
        Assert.Contains("Multiplayer acceptance requires fresh phase markers", launcher);
        Assert.Contains("exactly two authoritative character checkpoints", launcher);
        Assert.Contains("distinct private resume bookmarks", launcher);
        Assert.Contains("Multiplayer acceptance phases require -Multiplayer", launcher);
        Assert.Contains("$Multiplayer -and ($PreviewContent -or $PortalPreviewContent)", launcher);
        Assert.Contains("-AcceptancePhase MultiplayerCreate", acceptance);
        Assert.Contains("-AcceptancePhase MultiplayerResume", acceptance);
        Assert.Contains("Multiplayer resume did not start a fresh authoritative host", acceptance);
        Assert.Contains("Multiplayer resume did not start two fresh Godot processes", acceptance);
    }

    [Fact]
    public void GodotLauncherRecordsProcessOutcomesAndPreservesPrimaryFailures()
    {
        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "godot/run-local-client.ps1"));
        Assert.Contains("if ($null -eq $Tracked.Record.EndedUtc)", launcher, StringComparison.Ordinal);
        Assert.Contains("if ($null -eq $Tracked.Record.ExitCode)", launcher, StringComparison.Ordinal);
        Assert.Contains("$Tracked.Record.ExitCode = $Tracked.Process.ExitCode", launcher, StringComparison.Ordinal);
        Assert.Contains("$primaryError = $_", launcher, StringComparison.Ordinal);
        Assert.Contains("Could not stop failed Godot process", launcher, StringComparison.Ordinal);
        Assert.Contains("Could not dispose failed Godot process", launcher, StringComparison.Ordinal);
        Assert.Contains("Assert-GodotImportLog $importTracked $importLogPath", launcher, StringComparison.Ordinal);
        Assert.Contains("$resolvedPath = (Resolve-Path -LiteralPath $enginePath -ErrorAction Stop).ProviderPath", launcher, StringComparison.Ordinal);
        Assert.Contains("try { $sessionLock.Dispose() }", launcher, StringComparison.Ordinal);
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
