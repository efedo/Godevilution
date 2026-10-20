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

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public void ResidentArtAcceptanceRequiresWholeRosterDrawsAndHonestParityLimits()
    {
        var wrapper = Path.Combine(FindRepositoryRoot(), "tools/test-godot-native-classes.ps1");
        var script = "$ErrorActionPreference = 'Stop'\r\n$wrapperPath = " + QuotePowerShell(wrapper) + "\r\n" + """
            $tokens = $null; $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseInput([IO.File]::ReadAllText($wrapperPath), [ref]$tokens, [ref]$errors)
            if ($errors.Count -ne 0) { throw ($errors | Out-String) }
            $function = $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-NativeTownerEvidence' }, $true) | Select-Object -First 1
            . ([scriptblock]::Create($function.Extent.Text))
            $env:GODEVILUTION_NATIVE_TOWNER_PACK = 'requested'
            $scene = [pscustomobject]@{
                ContentHash = 'a' * 64; NativeTown = [pscustomobject]@{ CatalogFingerprint = 'b' * 64 }
                NativeTowners = [pscustomobject]@{
                    Passed = $true; CosmeticAnimation = $true; NativeInitializationAndColdPhaseParity = $false
                    RenderedParity = $false; CompleteRosterRoaming = $true
                    ColdReadiness = [pscustomobject]@{ Passed = $true; StartTimestamp = 100; StopwatchFrequency = 1000
                        StartRevision = 1; EndRevision = 3; StartTick = 10; EndTick = 12
                        Probe = [pscustomobject]@{ TimingRequestId = 1; TransportGeneration = 1; SentStopwatchTimestamp = 101
                            WriteStartStopwatchTimestamp = 102; WriteEndStopwatchTimestamp = 103; WriteOutcome = 1 }
                        Reply = [pscustomobject]@{ TimingRequestId = 1; TransportGeneration = 1; Outcome = 0
                            OutcomeStopwatchTimestamp = 104; TimingCalibrationReady = $true }
                        Snapshots = @([pscustomobject]@{ TransportGeneration = 1; SnapshotTick = 11; ReceivedStopwatchTimestamp = 105 },
                            [pscustomobject]@{ TransportGeneration = 1; SnapshotTick = 12; ReceivedStopwatchTimestamp = 106 }) }
                    Pack = [pscustomobject]@{ IsLoaded = $true; IdentityMatched = $true; AtlasesReady = $true
                        ContentManifestHash = 'a' * 64; TownCatalogFingerprint = 'b' * 64
                        MissingAtlasPaths = @(); LoadedAtlasPaths = @('atlas.png') }
                    Samples = @(foreach ($slot in 0..10) { foreach ($frame in 0..1) {
                        [pscustomobject]@{ ResidentSlot = $slot; FrameIndex = $frame; NativeArt = $true
                            GenericFallback = $false; AtlasPath = 'atlas.png'; AnchorInsideViewport = $true }
                    } })
                }
            }
            Assert-NativeTownerEvidence $scene $false
            Assert-NativeTownerEvidence $scene $true
            foreach ($mutation in @('cow', 'frame', 'fallback', 'anchor', 'content', 'fingerprint', 'atlas', 'phase', 'rendered', 'roam')) {
                $bad = $scene | ConvertTo-Json -Depth 32 | ConvertFrom-Json
                switch ($mutation) {
                    cow { $bad.NativeTowners.Samples = @($bad.NativeTowners.Samples | Where-Object ResidentSlot -ne 10) }
                    frame { $bad.NativeTowners.Samples = @($bad.NativeTowners.Samples | Where-Object { $_.ResidentSlot -ne 0 -or $_.FrameIndex -eq 0 }) }
                    fallback { $bad.NativeTowners.Samples[0].GenericFallback = $true }
                    anchor { $bad.NativeTowners.Samples[0].AnchorInsideViewport = $false }
                    content { $bad.NativeTowners.Pack.ContentManifestHash = 'c' * 64 }
                    fingerprint { $bad.NativeTowners.Pack.TownCatalogFingerprint = 'c' * 64 }
                    atlas { $bad.NativeTowners.Pack.AtlasesReady = $false }
                    phase { $bad.NativeTowners.NativeInitializationAndColdPhaseParity = $true }
                    rendered { $bad.NativeTowners.RenderedParity = $true }
                    roam { $bad.NativeTowners.CompleteRosterRoaming = $false }
                }
                $rejected = $false
                try { Assert-NativeTownerEvidence $bad $false } catch { $rejected = $true }
                if (-not $rejected) { throw "Resident art gate accepted invalid $mutation evidence." }
            }
            foreach ($mutation in @('missing', 'backlog', 'probe', 'reply', 'calibration', 'generation', 'snapshots', 'tick', 'revision')) {
                $bad = $scene | ConvertTo-Json -Depth 32 | ConvertFrom-Json
                switch ($mutation) {
                    missing { $bad.NativeTowners.ColdReadiness = $null }
                    backlog { $bad.NativeTowners.ColdReadiness.Snapshots[0].ReceivedStopwatchTimestamp = 99 }
                    probe { $bad.NativeTowners.ColdReadiness.Probe.SentStopwatchTimestamp = 99 }
                    reply { $bad.NativeTowners.ColdReadiness.Reply.Outcome = 1 }
                    calibration { $bad.NativeTowners.ColdReadiness.Reply.TimingCalibrationReady = $false }
                    generation { $bad.NativeTowners.ColdReadiness.Snapshots[0].TransportGeneration = 2 }
                    snapshots { $bad.NativeTowners.ColdReadiness.Snapshots = @($bad.NativeTowners.ColdReadiness.Snapshots[0]) }
                    tick { $bad.NativeTowners.ColdReadiness.Snapshots[1].SnapshotTick = 11 }
                    revision { $bad.NativeTowners.ColdReadiness.EndRevision = 2 }
                }
                $rejected = $false
                try { Assert-NativeTownerEvidence $bad $true } catch { $rejected = $true }
                if (-not $rejected) { throw "Resident cold gate accepted invalid $mutation evidence." }
            }
            Write-Output 'PASS'
            """;
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
