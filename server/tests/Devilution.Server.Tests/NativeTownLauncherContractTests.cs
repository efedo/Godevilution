using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

/** Behavior checks for the opt-in format-5 Town launcher boundary. */
public sealed class NativeTownLauncherContractTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public void TownAcceptanceRequiresExplicitTownOrClassProfileBeforeLaunchingProcesses()
    {
        var root = FindRepositoryRoot();
        var launcherPath = Path.Combine(root, "godot-client/run-local-client.ps1");
        var script = string.Join("\r\n", [
            "$ErrorActionPreference = 'Stop'",
            $"$launcherPath = {QuotePowerShell(launcherPath)}",
            "function Assert-Rejected([scriptblock]$Action, [string]$ExpectedMessage) {",
            "    try { & $Action; throw 'Expected the launcher guard to reject this invocation.' }",
            "    catch { if ($_.Exception.Message -cnotlike ('*' + $ExpectedMessage + '*')) { throw ('Unexpected launcher failure: ' + $_.Exception.Message) } }",
            "}",
            "Assert-Rejected { & $launcherPath -AcceptancePhase NativeWarriorJourney -Headless -Automated -PersistenceMode cohort -SessionDirectory (Join-Path $env:TEMP 'town-guard-no-profile') } 'Native class acceptance requires its explicit class profile and automated headless cohort session.'",
            "Assert-Rejected { & $launcherPath -AcceptancePhase Resume -Headless -Automated -PersistenceMode cohort -SessionDirectory (Join-Path $env:TEMP 'town-guard-wrong-phase') -CathedralNativeTownProfileDirectory 'unused' } 'explicit acceptance phases'",
            "Assert-Rejected { & $launcherPath -AcceptancePhase NativeSorcererJourney -Headless -Automated -PersistenceMode cohort -SessionDirectory (Join-Path $env:TEMP 'town-guard-conflict') -CathedralNativeTownProfileDirectory 'unused' -CathedralNativeClassProfileDirectory 'unused' } 'Choose one explicit native Cathedral profile'",
            "Write-Output 'PASS'",
        ]) + "\r\n";

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public void TownSessionMarkerBindsProfileClassAndAllPackIdentities()
    {
        var root = FindRepositoryRoot();
        var helperPath = Path.Combine(root, "tools/NativeTownProfile.ps1");
        var script = string.Join("\r\n", [
            "$ErrorActionPreference = 'Stop'",
            $"$helperPath = {QuotePowerShell(helperPath)}",
            "$helperText = [IO.File]::ReadAllText($helperPath)",
            "$tokens = $null; $parseErrors = $null",
            "$helperAst = [System.Management.Automation.Language.Parser]::ParseInput($helperText, [ref]$tokens, [ref]$parseErrors)",
            "if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }",
            "$functionAst = $helperAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Initialize-CathedralNativeTownSession' }, $true) | Select-Object -First 1",
            "if ($null -eq $functionAst) { throw 'Could not extract native Town session initializer.' }",
            ". ([scriptblock]::Create($functionAst.Extent.Text))",
            "function Assert-Rejected([scriptblock]$Action, [string]$ExpectedMessage) {",
            "    try { & $Action; throw 'Expected the Town session marker to reject this profile.' }",
            "    catch { if ($_.Exception.Message -cnotlike ('*' + $ExpectedMessage + '*')) { throw ('Unexpected marker rejection: ' + $_.Exception.Message) } }",
            "}",
            "$root = Join-Path $env:TEMP ('native-town-marker-' + [guid]::NewGuid().ToString('N'))",
            "$session = Join-Path $root 'sessions/local'; New-Item -ItemType Directory -Path $session -Force | Out-Null",
            "$profile = [pscustomobject]@{ Metadata = [pscustomobject]@{ contentManifestHash = 'A' * 64; townCatalogFingerprint = 'B' * 64; townInitialSeed = 123 }; MetadataHash = 'C' * 64; ContentTreeHash = 'D' * 64; PopulationTreeHash = 'E' * 64; RuntimeTreeHash = 'F' * 64; TownTreeHash = '1' * 64; ClassId = 0; ClassSymbol = 'warrior' }",
            "Initialize-CathedralNativeTownSession $profile $session",
            "$markerPath = Join-Path $session 'cathedral-native-town-progression-session.json'; $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json",
            "if ($marker.ContentManifestHash -cne $profile.Metadata.contentManifestHash -or $marker.TownTreeHash -cne $profile.TownTreeHash -or $marker.ClassId -ne 0) { throw 'The Town marker omitted a bound profile identity.' }",
            "Initialize-CathedralNativeTownSession $profile $session",
            "$changedHash = [pscustomobject]@{ Metadata = [pscustomobject]@{ contentManifestHash = '2' * 64; townCatalogFingerprint = 'B' * 64; townInitialSeed = 123 }; MetadataHash = 'C' * 64; ContentTreeHash = 'D' * 64; PopulationTreeHash = 'E' * 64; RuntimeTreeHash = 'F' * 64; TownTreeHash = '1' * 64; ClassId = 0; ClassSymbol = 'warrior' }",
            "Assert-Rejected { Initialize-CathedralNativeTownSession $changedHash $session } 'profile changed'",
            "$changedClass = [pscustomobject]@{ Metadata = $profile.Metadata; MetadataHash = $profile.MetadataHash; ContentTreeHash = $profile.ContentTreeHash; PopulationTreeHash = $profile.PopulationTreeHash; RuntimeTreeHash = $profile.RuntimeTreeHash; TownTreeHash = $profile.TownTreeHash; ClassId = 2; ClassSymbol = 'sorcerer' }",
            "Assert-Rejected { Initialize-CathedralNativeTownSession $changedClass $session } 'profile changed'",
            "$other = Join-Path $root 'sessions/other'; New-Item -ItemType Directory -Path $other -Force | Out-Null; Set-Content -LiteralPath (Join-Path $other 'cathedral-native-class-progression-session.json') 'legacy'",
            "Assert-Rejected { Initialize-CathedralNativeTownSession $profile $other } 'another Cathedral profile'",
            "$unmarked = Join-Path $root 'sessions/unmarked'; New-Item -ItemType Directory -Path $unmarked -Force | Out-Null; Set-Content -LiteralPath (Join-Path $unmarked 'save.dat') 'save'",
            "Assert-Rejected { Initialize-CathedralNativeTownSession $profile $unmarked } 'unmarked existing save profile'",
            "Remove-Item -LiteralPath $root -Recurse -Force",
            "Write-Output 'PASS'",
        ]) + "\r\n";

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    private static string RunPowerShell(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"godevilution-native-town-test-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try {
            using var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "powershell.exe",
                    Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            Assert.True(process.Start(), "Could not start Windows PowerShell.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000)) {
                process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(10_000), "Timed out PowerShell process did not exit after termination.");
                Assert.Fail("Windows PowerShell behavior harness timed out.");
            }
            Assert.True(stdout.Wait(10_000) && stderr.Wait(10_000), "PowerShell output did not drain after exit.");
            var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            Assert.True(process.ExitCode == 0, $"PowerShell behavior harness failed ({process.ExitCode}):{Environment.NewLine}{output}");
            return output;
        } finally {
            if (File.Exists(scriptPath))
                File.Delete(scriptPath);
        }
    }

    private static string QuotePowerShell(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}
