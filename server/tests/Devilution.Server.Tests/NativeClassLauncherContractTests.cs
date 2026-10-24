using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

/** Exercises class-bound launcher guards and session markers through Windows PowerShell. */
public sealed class NativeClassLauncherContractTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public void ClassAcceptanceGuardsRejectMissingProfilesWrongPhasesAndClassOverrides()
    {
        var root = FindRepositoryRoot();
        var launcherPath = Path.Combine(root, "godot-client/run-local-client.ps1");
        var script = string.Join("\r\n", [
            "$ErrorActionPreference = 'Stop'",
            $"$launcherPath = {QuotePowerShell(launcherPath)}",
            "$work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-NativeClass-Guard-' + [guid]::NewGuid().ToString('N'))",
            "$session = Join-Path $work 'session'",
            "New-Item -ItemType Directory -Path $session -Force | Out-Null",
            "function Assert-Rejected([string]$Name, [string]$ExpectedMessage, [hashtable]$Arguments) {",
            "    try { & $launcherPath @Arguments; throw ('Unexpectedly accepted ' + $Name) }",
            "    catch { if ($_.Exception.Message -cne $ExpectedMessage) { throw ('Wrong rejection for ' + $Name + ': ' + $_.Exception.Message) } }",
            "}",
            "Assert-Rejected 'missing class profile' 'Native class acceptance requires its explicit class profile and automated headless cohort session.' @{",
            "    AcceptancePhase = 'NativeWarriorJourney'; Headless = $true; Automated = $true; PersistenceMode = 'cohort'; SessionDirectory = $session",
            "}",
            "$classProfile = Join-Path $work 'profile-does-not-need-to-exist'",
            "Assert-Rejected 'class profile in base Create phase' 'Class-bound native profiles require a direct single-player Warrior, Rogue or Sorcerer cohort launch or their explicit acceptance phases.' @{",
            "    AcceptancePhase = 'Create'; CathedralNativeClassProfileDirectory = $classProfile; PersistenceMode = 'cohort'; SessionDirectory = $session",
            "}",
            "Assert-Rejected 'class override on an acceptance phase' 'Native class acceptance requires its explicit class profile and automated headless cohort session.' @{",
            "    AcceptancePhase = 'NativeWarriorJourney'; CathedralNativeClassProfileDirectory = $classProfile; Headless = $true; Automated = $true;",
            "    PersistenceMode = 'cohort'; SessionDirectory = $session; CharacterClassId = 0",
            "}",
            "Write-Output 'PASS'",
            "if (-not ([IO.Path]::GetFullPath($work)).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\\') + '\\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Temporary test cleanup escaped its root.' }",
            "Remove-Item -LiteralPath $work -Recurse -Force",
        ]) + "\r\n";

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public void ClassSessionMarkerAllowsSameProfileAndRejectsChangedClassHashesAndOtherRoots()
    {
        var root = FindRepositoryRoot();
        var loaderPath = Path.Combine(root, "godot-client/native-class-profile.ps1");
        var script = string.Join("\r\n", [
            "$ErrorActionPreference = 'Stop'",
            $"$loaderPath = {QuotePowerShell(loaderPath)}",
            "$loaderText = [IO.File]::ReadAllText($loaderPath)",
            "$tokens = $null; $parseErrors = $null",
            "$loaderAst = [System.Management.Automation.Language.Parser]::ParseInput($loaderText, [ref]$tokens, [ref]$parseErrors)",
            "if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }",
            "$functionAst = $loaderAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Initialize-CathedralNativeClassSession' }, $true) | Select-Object -First 1",
            "if ($null -eq $functionAst) { throw 'Could not extract the session initializer.' }",
            ". ([scriptblock]::Create($functionAst.Extent.Text))",
            "function New-Profile([int]$ClassId, [string]$ClassSymbol, [string]$MetadataHash = ('A' * 64)) {",
            "    [pscustomobject]@{ MetadataHash = $MetadataHash; ContentTreeHash = 'B' * 64; PopulationTreeHash = 'C' * 64; RuntimeTreeHash = 'D' * 64; ClassId = $ClassId; ClassSymbol = $ClassSymbol }",
            "}",
            "function Assert-Rejected([scriptblock]$Action, [string]$ExpectedMessage) {",
            "    try { & $Action; throw 'Expected the session initializer to reject this profile.' }",
            "    catch { if ($_.Exception.Message -cnotlike ('*' + $ExpectedMessage + '*')) { throw ('Unexpected session rejection: ' + $_.Exception.Message) } }",
            "}",
            "$work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-NativeClass-Marker-' + [guid]::NewGuid().ToString('N'))",
            "New-Item -ItemType Directory -Path $work -Force | Out-Null",
            "$classProfile = New-Profile 0 'warrior'",
            "$session = Join-Path $work 'warrior-session'; New-Item -ItemType Directory -Path $session | Out-Null",
            "Initialize-CathedralNativeClassSession $classProfile $session",
            "Initialize-CathedralNativeClassSession $classProfile $session",
            "$markerPath = Join-Path $session 'cathedral-native-class-progression-session.json'",
            "$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json",
            "if ($marker.ClassId -ne 0 -or $marker.ClassSymbol -cne 'warrior' -or $marker.MetadataHash -cne $classProfile.MetadataHash) { throw 'The marker did not preserve the bound Warrior profile.' }",
            "foreach ($hashName in @('MetadataHash', 'ContentTreeHash', 'PopulationTreeHash', 'RuntimeTreeHash')) {",
            "    $changedHash = New-Profile 0 'warrior'; $changedHash.$hashName = 'E' * 64",
            "    Assert-Rejected { Initialize-CathedralNativeClassSession $changedHash $session } 'profile changed'",
            "}",
            "$otherClass = New-Profile 2 'sorcerer'",
            "Assert-Rejected { Initialize-CathedralNativeClassSession $otherClass $session } 'profile changed'",
            "foreach ($name in @('cathedral-session.json', 'cathedral-door-session.json', 'cathedral-native-population-session.json', 'cathedral-native-progression-session.json', 'cathedral-native-depth-progression-session.json')) {",
            "    $otherSession = Join-Path $work ([guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $otherSession | Out-Null",
            "    [IO.File]::WriteAllText((Join-Path $otherSession $name), '{}')",
            "    Assert-Rejected { Initialize-CathedralNativeClassSession $classProfile $otherSession } 'another Cathedral profile'",
            "}",
            "$unmarkedSession = Join-Path $work 'unmarked-session'; New-Item -ItemType Directory -Path $unmarkedSession | Out-Null",
            "[IO.File]::WriteAllText((Join-Path $unmarkedSession 'existing-save.json'), '{}')",
            "Assert-Rejected { Initialize-CathedralNativeClassSession $classProfile $unmarkedSession } 'unmarked existing save profile'",
            "$lockedSession = Join-Path $work 'launcher-lock-session'; New-Item -ItemType Directory -Path $lockedSession | Out-Null",
            "[IO.File]::WriteAllText((Join-Path $lockedSession 'launcher.lock'), '')",
            "Initialize-CathedralNativeClassSession $classProfile $lockedSession",
            "if (-not (Test-Path -LiteralPath (Join-Path $lockedSession 'cathedral-native-class-progression-session.json'))) { throw 'The launcher lock prevented marker creation.' }",
            "Write-Output 'PASS'",
            "if (-not ([IO.Path]::GetFullPath($work)).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\\') + '\\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Temporary test cleanup escaped its root.' }",
            "Remove-Item -LiteralPath $work -Recurse -Force",
        ]) + "\r\n";

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public void NativeClassEvidenceRejectsMissingPayloadAndSceneHashMismatches()
    {
        var root = FindRepositoryRoot();
        var wrapperPath = Path.Combine(root, "tools/test-godot-native-classes.ps1");
        var script = string.Join("\r\n", [
            "$ErrorActionPreference = 'Stop'",
            $"$wrapperPath = {QuotePowerShell(wrapperPath)}",
            "$wrapperText = [IO.File]::ReadAllText($wrapperPath)",
            "$tokens = $null; $parseErrors = $null",
            "$wrapperAst = [System.Management.Automation.Language.Parser]::ParseInput($wrapperText, [ref]$tokens, [ref]$parseErrors)",
            "if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }",
            "foreach ($functionName in @('Test-NativeClassIntegerInRange', 'Assert-NativeClassEvidence')) {",
            "    $functionAst = $wrapperAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName }, $true) | Select-Object -First 1",
            "    if ($null -eq $functionAst) { throw ('Could not extract native class validator: ' + $functionName) }",
            "    . ([scriptblock]::Create($functionAst.Extent.Text))",
            "}",
            "function Assert-Rejected([scriptblock]$Action, [string]$ExpectedMessage) {",
            "    try { & $Action; throw 'Expected native class evidence to be rejected.' }",
            "    catch { if ($_.Exception.Message -cnotlike ('*' + $ExpectedMessage + '*')) { throw ('Unexpected evidence rejection: ' + $_.Exception.Message) } }",
            "}",
            "$contentHash = 'A' * 64; $rulesetHash = 'B' * 64",
            "$checkpoint = [pscustomobject]@{ PlayerPayload = 'AQID'; WorldHash = 'C' * 64; RandomState = 11; Sequence = 5; ClassId = 0 }",
            "$scene = [pscustomobject]@{ Passed = $true; Mode = 'SinglePlayer'; ClassId = 0; Sequence = 5; ExactColdBaseline = $false; ContentHash = $contentHash; RulesetHash = $rulesetHash; NativeClass = $checkpoint }",
            "$processEvidence = [pscustomobject]@{ ClassId = 0; ContentHash = $contentHash; RulesetHash = $rulesetHash }",
            "Assert-NativeClassEvidence $scene $processEvidence 0 'Journey'",
            "$scene.ExactColdBaseline = $true; $scene.Sequence = 6; $scene | Add-Member -NotePropertyName FirstAcceptedSequence -NotePropertyValue 6; $scene | Add-Member -NotePropertyName FirstAcceptedAppliedTick -NotePropertyValue 7",
            "Assert-NativeClassEvidence $scene $processEvidence 0 'Resume'",
            "$scene.ExactColdBaseline = $false; $scene.Sequence = 5; $scene.PSObject.Properties.Remove('FirstAcceptedSequence'); $scene.PSObject.Properties.Remove('FirstAcceptedAppliedTick')",
            "$checkpoint.PSObject.Properties.Remove('PlayerPayload')",
            "Assert-Rejected { Assert-NativeClassEvidence $scene $processEvidence 0 'Journey' } 'checkpoint omitted a valid payload'",
            "$checkpoint | Add-Member -NotePropertyName PlayerPayload -NotePropertyValue 'AQID'",
            "$scene.ContentHash = 'E' * 64",
            "Assert-Rejected { Assert-NativeClassEvidence $scene $processEvidence 0 'Journey' } 'scene ContentHash does not match its process evidence'",
            "$scene.ContentHash = $contentHash; $scene.RulesetHash = 'D' * 64",
            "Assert-Rejected { Assert-NativeClassEvidence $scene $processEvidence 0 'Journey' } 'scene RulesetHash does not match its process evidence'",
            "Write-Output 'PASS'",
        ]) + "\r\n";

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    private static string RunPowerShell(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"godevilution-native-class-test-{Guid.NewGuid():N}.ps1");
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
