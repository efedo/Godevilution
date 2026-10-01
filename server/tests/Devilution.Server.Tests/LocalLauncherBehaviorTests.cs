using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class LocalLauncherBehaviorTests
{
    [Fact]
    public void LifecycleGuardsAllowInteractiveAndMatchingPhasesWithoutLaunchingAnEngine()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness([], """
            $guardEnd = $launcherText.IndexOf('$repositoryRoot = ', [StringComparison]::Ordinal)
            if ($guardEnd -le 0) { throw 'Could not isolate the launcher argument guards.' }
            $guards = [scriptblock]::Create($launcherText.Substring(0, $guardEnd) + "`r`n'GUARDS-PASSED'")
            $scenarios = @(
                @{ Label = 'normal'; Parameters = @{}; Allowed = $true },
                @{ Label = 'base create'; Parameters = @{ AcceptancePhase = 'Create' }; Allowed = $true },
                @{ Label = 'base resume'; Parameters = @{ AcceptancePhase = 'Resume' }; Allowed = $true },
                @{ Label = 'interactive lifecycle'; Parameters = @{ LifecyclePreviewContent = $true }; Allowed = $true },
                @{ Label = 'lifecycle creation'; Parameters = @{ LifecyclePreviewContent = $true; AcceptancePhase = 'Lifecycle' }; Allowed = $true },
                @{ Label = 'lifecycle resume'; Parameters = @{ LifecyclePreviewContent = $true; AcceptancePhase = 'LifecycleResume' }; Allowed = $true },
                @{ Label = 'original preview'; Parameters = @{ PreviewContent = $true }; Allowed = $true },
                @{ Label = 'portal preview'; Parameters = @{ PortalPreviewContent = $true }; Allowed = $true },
                @{ Label = 'multiplayer'; Parameters = @{ Multiplayer = $true }; Allowed = $true },
                @{ Label = 'creation without profile'; Parameters = @{ AcceptancePhase = 'Lifecycle' }; Allowed = $false },
                @{ Label = 'resume without profile'; Parameters = @{ AcceptancePhase = 'LifecycleResume' }; Allowed = $false },
                @{ Label = 'wrong acceptance'; Parameters = @{ LifecyclePreviewContent = $true; AcceptancePhase = 'Resume' }; Allowed = $false },
                @{ Label = 'original preview mixture'; Parameters = @{ LifecyclePreviewContent = $true; PreviewContent = $true }; Allowed = $false },
                @{ Label = 'portal preview mixture'; Parameters = @{ LifecyclePreviewContent = $true; PortalPreviewContent = $true }; Allowed = $false },
                @{ Label = 'multiplayer mixture'; Parameters = @{ LifecyclePreviewContent = $true; Multiplayer = $true }; Allowed = $false },
                @{ Label = 'performance mixture'; Parameters = @{ LifecyclePreviewContent = $true; AcceptancePhase = 'Performance' }; Allowed = $false },
                @{ Label = 'L1 mixture'; Parameters = @{ LifecyclePreviewContent = $true; CathedralProfileDirectory = 'not-opened' }; Allowed = $false },
                @{ Label = 'depth mixture'; Parameters = @{ LifecyclePreviewContent = $true; CathedralDepthProfileDirectory = 'not-opened' }; Allowed = $false }
            )
            foreach ($scenario in $scenarios) {
                $arguments = $scenario.Parameters
                $allowed = $false
                try {
                    $output = @(& $guards @arguments)
                    $allowed = $output -contains 'GUARDS-PASSED'
                } catch {
                    if ($scenario.Allowed) { throw }
                }
                if ($allowed -ne $scenario.Allowed) {
                    throw ('Unexpected guard outcome for ' + $scenario.Label)
                }
            }
            Write-Output 'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void GodotCleanupIsBoundedAndTerminalEvidenceSurvivesOutputFailure()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Stop-OwnedLocalProcess", "Write-ProcessEvidence", "Complete-GodotProcess", "Stop-GodotProcess"],
            """
            $ErrorActionPreference = 'Stop'
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-CleanupTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                Add-Type -TypeDefinition @'
using System;
public sealed class CleanupProcessProbe
{
    public int Id { get { return 123; } }
    public bool HasExited { get; set; }
    public int ExitCode { get; set; }
    public bool KillCompletes { get; set; }
    public bool WaitCompletes { get; set; }
    public bool KillThrows { get; set; }
    public bool ExitDuringKill { get; set; }
    public bool ExitDuringWait { get; set; }
    public int KillCalls { get; private set; }
    public int WaitCalls { get; private set; }
    public int WaitMilliseconds { get; private set; }
    public void Kill()
    {
        KillCalls++;
        if (ExitDuringKill) HasExited = true;
        if (KillThrows) throw new InvalidOperationException("injected termination denied");
        if (KillCompletes) HasExited = true;
    }
    public bool WaitForExit(int milliseconds)
    {
        WaitCalls++;
        WaitMilliseconds = milliseconds;
        if (WaitCompletes || ExitDuringWait) HasExited = true;
        return WaitCompletes;
    }
    public void WaitForExit() { throw new InvalidOperationException("unbounded wait is forbidden"); }
}
'@
                function Write-GodotRedirectedOutput($Tracked) {
                    if ($script:FailOutput) { throw 'injected output storage failure' }
                }
                function New-ProbeRecord([string]$Stage, $Process, [string]$ErrorMessage) {
                    return [pscustomobject]@{
                        Process = $Process
                        EvidencePath = Join-Path $work ($Stage + '.json')
                        Record = [pscustomobject]@{ Stage = $Stage; EndedUtc = $null; ExitCode = $null;
                            Error = $(if ([string]::IsNullOrEmpty($ErrorMessage)) { $null } else { $ErrorMessage }) }
                    }
                }

                $exited = [CleanupProcessProbe]::new()
                $exited.HasExited = $true
                Stop-OwnedLocalProcess $exited
                if ($exited.KillCalls -ne 0 -or $exited.WaitCalls -ne 0) { throw 'An exited process was killed or waited on.' }

                $delayed = [CleanupProcessProbe]::new()
                $delayed.WaitCompletes = $true
                Stop-OwnedLocalProcess $delayed 17
                if ($delayed.KillCalls -ne 1 -or $delayed.WaitCalls -ne 1 -or $delayed.WaitMilliseconds -ne 17) {
                    throw 'Cleanup did not pass the explicit deadline to the process.'
                }

                $race = [CleanupProcessProbe]::new()
                $race.ExitDuringKill = $true
                $race.KillThrows = $true
                Stop-OwnedLocalProcess $race
                if (-not $race.HasExited -or $race.WaitCalls -ne 0) { throw 'A natural exit during Kill was not handled.' }

                $waitRace = [CleanupProcessProbe]::new()
                $waitRace.ExitDuringWait = $true
                Stop-OwnedLocalProcess $waitRace 17
                if (-not $waitRace.HasExited -or $waitRace.WaitMilliseconds -ne 17) {
                    throw 'A natural exit at the wait deadline was reported as failed termination.'
                }

                $stuck = [CleanupProcessProbe]::new()
                $stuckTracked = New-ProbeRecord 'stuck' $stuck 'original stage timeout'
                $rejected = $false
                try { Stop-GodotProcess $stuckTracked } catch { $rejected = $true }
                $stuckEvidence = Get-Content -LiteralPath $stuckTracked.EvidencePath -Raw | ConvertFrom-Json
                if (-not $rejected -or $stuck.WaitMilliseconds -ne 5000 -or $stuck.WaitCalls -ne 1 -or
                    $stuckEvidence.Error -cne 'original stage timeout' -or $stuckEvidence.CleanupError -notmatch 'did not terminate' -or
                    $null -ne $stuckEvidence.EndedUtc -or $null -ne $stuckEvidence.ExitCode) {
                    throw 'Failed termination was unbounded, concealed, or recorded as a terminal exit.'
                }

                $denied = [CleanupProcessProbe]::new()
                $denied.KillThrows = $true
                $deniedTracked = New-ProbeRecord 'denied' $denied 'original import failure'
                $rejected = $false
                try { Stop-GodotProcess $deniedTracked } catch { $rejected = $true }
                $deniedEvidence = Get-Content -LiteralPath $deniedTracked.EvidencePath -Raw | ConvertFrom-Json
                if (-not $rejected -or $denied.WaitCalls -ne 0 -or $deniedEvidence.Error -cne 'original import failure' -or
                    $deniedEvidence.CleanupError -notmatch 'injected termination denied' -or $null -ne $deniedEvidence.EndedUtc) {
                    throw 'A cleanup permission failure replaced the stage error or fabricated completion.'
                }

                $script:FailOutput = $true
                foreach ($code in @(0, -1073741819)) {
                    $terminal = [CleanupProcessProbe]::new()
                    $terminal.HasExited = $true
                    $terminal.ExitCode = $code
                    $tracked = New-ProbeRecord ('output-' + $code) $terminal $null
                    $rejected = $false
                    $caughtMessage = $null
                    try { Complete-GodotProcess $tracked 0 'probe engine' } catch { $rejected = $true; $caughtMessage = $_.Exception.Message }
                    $evidence = Get-Content -LiteralPath $tracked.EvidencePath -Raw | ConvertFrom-Json
                    if (-not $rejected -or $null -eq $evidence.EndedUtc -or $evidence.ExitCode -ne $code) {
                        throw 'Output failure hid terminal exit evidence or was accepted.'
                    }
                    if ($code -ne 0 -and ($evidence.Error -notmatch 'exited with code -1073741819' -or
                        $caughtMessage -notmatch 'probe engine exited with code -1073741819' -or
                        $evidence.DiagnosticError -cne 'injected output storage failure')) {
                        throw 'Output failure erased the original engine failure.'
                    }
                    if ($code -eq 0 -and $caughtMessage -cne 'injected output storage failure') {
                        throw 'A successful engine exit concealed diagnostic-storage failure.'
                    }
                    $endedUtc = $evidence.EndedUtc
                    try { Stop-GodotProcess $tracked } catch { }
                    $finalEvidence = Get-Content -LiteralPath $tracked.EvidencePath -Raw | ConvertFrom-Json
                    if ($finalEvidence.EndedUtc -cne $endedUtc -or $finalEvidence.ExitCode -ne $code) {
                        throw 'Cleanup changed terminal status after output failure.'
                    }
                }
                $script:FailOutput = $false
                $killed = [CleanupProcessProbe]::new()
                $killed.KillCompletes = $true
                $killed.ExitCode = -1
                $killedTracked = New-ProbeRecord 'killed' $killed $null
                Stop-GodotProcess $killedTracked
                if ($killedTracked.Record.ExitCode -ne -1 -or $killedTracked.Record.Error -cne 'Terminated by launcher cleanup.') {
                    throw 'Successful owned-child cleanup lost its outcome.'
                }
            } finally {
                $resolvedWork = [IO.Path]::GetFullPath($work)
                $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
                if (-not $resolvedWork.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
                    [IO.Path]::GetFileName($resolvedWork) -notmatch '^Godevilution-CleanupTest-[a-f0-9]{32}$') {
                    throw 'Refusing to remove a test directory outside its validated temporary target.'
                }
                Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
            }
""");
        RunPowerShell(script);
    }

    [Fact]
    public void GodotLauncherProcessHelpersUseIsolatedChildrenAndRejectBadStages()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
                new[]
                {
                    "Resolve-DirectGodotExecutable",
                    "Assert-WritableDirectory",
                    "Write-ProcessEvidence",
                    "Start-GodotChildProcess",
                    "Stop-OwnedLocalProcess",
                    "New-GodotProcess",
                    "Write-GodotRedirectedOutput",
                    "Complete-GodotProcess",
                    "Assert-GodotImportLog",
                    "Stop-GodotProcess",
            },
            """
            $ErrorActionPreference = 'Stop'
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-LauncherTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work -Force | Out-Null
            $originalErrorMode = $null
            try {
                $captureRoot = $work
                $processEvidenceRoot = Join-Path $work 'processes'
                New-Item -ItemType Directory -Path $processEvidenceRoot -Force | Out-Null
                $repositoryRoot = (Get-Location).Path
                $GodotExecutable = (Get-Command powershell.exe -CommandType Application).Source
                $childScript = Join-Path $work 'child.ps1'
                [IO.File]::WriteAllText($childScript, @'
param()
if (-not ('ChildNativeErrorMode' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class ChildNativeErrorMode
{
    [DllImport("kernel32.dll")]
    public static extern uint GetErrorMode();
}
"@
}
$result = @{
    AppData = $env:APPDATA
    LocalAppData = $env:LOCALAPPDATA
    ParentHost = $env:DEVILUTION_SERVER_HOST
    Role = $env:TEST_ROLE
    ErrorMode = [ChildNativeErrorMode]::GetErrorMode()
    ProcessId = $PID
}
if ($env:CHILD_RESULT) {
    [IO.File]::WriteAllText($env:CHILD_RESULT, ($result | ConvertTo-Json -Compress))
}
if ($env:CHILD_LOG) {
    [IO.File]::WriteAllText($env:CHILD_LOG, $env:CHILD_LOG_TEXT)
}
if ($env:CHILD_SLEEP_MS) {
    Start-Sleep -Milliseconds ([int]$env:CHILD_SLEEP_MS)
}
if ($env:CHILD_EXIT) {
    [Environment]::Exit([int]$env:CHILD_EXIT)
}
'@)
                $childArguments = '-NoProfile -NonInteractive -File "{0}"' -f $childScript

                $fakeConsole = Join-Path $work 'Godot_v4-stable_mono_win64_console.exe'
                $fakeEngine = Join-Path $work 'Godot_v4-stable_mono_win64.exe'
                New-Item -ItemType File -Path $fakeConsole, $fakeEngine -Force | Out-Null
                Push-Location $work
                try {
                    $resolved = Resolve-DirectGodotExecutable '.\Godot_v4-stable_mono_win64_console.exe'
                } finally {
                    Pop-Location
                }
                if ($resolved -ne [IO.Path]::GetFullPath($fakeEngine) -or -not [IO.Path]::IsPathRooted($resolved)) {
                    throw "Relative Godot executable was not normalized to its GUI sibling: $resolved"
                }

                if (-not ('GodevilutionNativeErrorMode' -as [type])) {
                    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GodevilutionNativeErrorMode
{
    [DllImport("kernel32.dll")]
    public static extern uint GetErrorMode();
    [DllImport("kernel32.dll")]
    public static extern uint SetErrorMode(uint mode);
}
'@
                }
                $env:APPDATA = Join-Path $work 'parent-appdata'
                $env:LOCALAPPDATA = Join-Path $work 'parent-localappdata'
                $env:DEVILUTION_SERVER_HOST = 'parent-host'
                $parentAppData = $env:APPDATA
                $parentLocalAppData = $env:LOCALAPPDATA
                $parentHost = $env:DEVILUTION_SERVER_HOST
                $originalErrorMode = [GodevilutionNativeErrorMode]::GetErrorMode()
                [void][GodevilutionNativeErrorMode]::SetErrorMode($originalErrorMode -band [uint32]4294967292)
                $parentErrorMode = [GodevilutionNativeErrorMode]::GetErrorMode()
                $script:AllTracked = [System.Collections.ArrayList]::new()
                $script:ObservedSpawnModes = [System.Collections.ArrayList]::new()
                function Start-GodotChildProcess($StartInfo) {
                    [void]$script:ObservedSpawnModes.Add([GodevilutionNativeErrorMode]::GetErrorMode())
                    return [Diagnostics.Process]::Start($StartInfo)
                }
                function Write-ProcessEvidence($Tracked) {
                    if ($Tracked.Record.Stage -eq 'primary-failure') {
                        [void]$script:AllTracked.Add($Tracked)
                        $readyPath = Join-Path $work 'primary-failure.json'
                        $deadline = [Diagnostics.Stopwatch]::StartNew()
                        while (-not (Test-Path -LiteralPath $readyPath) -and $deadline.Elapsed.TotalSeconds -lt 5) {
                            Start-Sleep -Milliseconds 25
                        }
                        throw 'primary evidence write failed'
                    }
                    [IO.File]::WriteAllText($Tracked.EvidencePath, ($Tracked.Record | ConvertTo-Json -Depth 4))
                }
                function New-TestTracked([string]$Stage, [string]$Arguments, [hashtable]$EnvironmentOverrides, [bool]$Headless, [bool]$SuppressCrashDialog) {
                    $tracked = New-GodotProcess $Stage $Arguments $EnvironmentOverrides $Headless $SuppressCrashDialog
                    [void]$script:AllTracked.Add($tracked)
                    return $tracked
                }
                Add-Type -TypeDefinition @'
using System;
public sealed class FakeCompletedProcess
{
    public bool HasExited { get { return true; } }
    public int ExitCode { get { return 0; } }
    public bool WaitForExit(int milliseconds) { throw new InvalidOperationException("WaitForExit must not be called after HasExited."); }
}
'@
                $fakeTracked = [pscustomobject]@{
                    Process = [FakeCompletedProcess]::new()
                    EvidencePath = Join-Path $processEvidenceRoot 'already-exited.json'
                    Record = [pscustomobject]@{ Stage = 'already-exited'; EndedUtc = $null; ExitCode = $null; Error = $null }
                }
                Complete-GodotProcess $fakeTracked 0 'already exited fake'
                if ($fakeTracked.Record.EndedUtc -eq $null -or $fakeTracked.Record.ExitCode -ne 0) {
                    throw 'A process already observed as exited was not completed deterministically.'
                }

                $firstResultPath = Join-Path $work 'first.json'
                $first = New-TestTracked 'stage-one' $childArguments @{
                    CHILD_RESULT = $firstResultPath
                    TEST_ROLE = 'one'
                } $true $true
                Complete-GodotProcess $first 10000 'safe child one'
                $firstEndedUtc = $first.Record.EndedUtc
                $firstExitCode = $first.Record.ExitCode
                Complete-GodotProcess $first 0 'safe child one repeated'
                if ($first.Record.EndedUtc -ne $firstEndedUtc -or $first.Record.ExitCode -ne $firstExitCode) {
                    throw 'Repeated completion changed the first process outcome.'
                }
                $firstResult = Get-Content -LiteralPath $firstResultPath -Raw | ConvertFrom-Json
                if ($script:ObservedSpawnModes.Count -ne 1 -or (([uint32]$script:ObservedSpawnModes[0] -band [uint32]3) -ne [uint32]3)) {
                    throw 'The suppressed launch did not set crash-dialog bits at the process-spawn boundary.'
                }

                $secondResultPath = Join-Path $work 'second.json'
                $second = New-TestTracked 'stage-two' $childArguments @{
                    CHILD_RESULT = $secondResultPath
                    TEST_ROLE = 'two'
                } $true $true
                Complete-GodotProcess $second 10000 'safe child two'
                $secondResult = Get-Content -LiteralPath $secondResultPath -Raw | ConvertFrom-Json
                if ($script:ObservedSpawnModes.Count -ne 2 -or (([uint32]$script:ObservedSpawnModes[1] -band [uint32]3) -ne [uint32]3)) {
                    throw 'The second suppressed launch did not set crash-dialog bits at the process-spawn boundary.'
                }
                $expectedFirstAppData = [IO.Path]::GetFullPath((Join-Path $work 'engine-state/stage-one/AppData'))
                $expectedFirstLocalAppData = [IO.Path]::GetFullPath((Join-Path $work 'engine-state/stage-one/LocalAppData'))
                $expectedSecondAppData = [IO.Path]::GetFullPath((Join-Path $work 'engine-state/stage-two/AppData'))
                $expectedSecondLocalAppData = [IO.Path]::GetFullPath((Join-Path $work 'engine-state/stage-two/LocalAppData'))
                if ($firstResult.AppData -ne $expectedFirstAppData -or $firstResult.LocalAppData -ne $expectedFirstLocalAppData) {
                    throw 'The first child did not receive its isolated AppData directories.'
                }
                if ($secondResult.AppData -ne $expectedSecondAppData -or $secondResult.LocalAppData -ne $expectedSecondLocalAppData) {
                    throw 'The second child did not receive its isolated AppData directories.'
                }
                if ($firstResult.AppData -eq $secondResult.AppData -or $firstResult.LocalAppData -eq $secondResult.LocalAppData) {
                    throw 'Engine stages shared an AppData directory.'
                }
                if ($firstResult.ParentHost -ne $parentHost -or $secondResult.ParentHost -ne $parentHost -or $firstResult.Role -ne 'one' -or $secondResult.Role -ne 'two') {
                    throw 'Unrelated inherited environment was not preserved for children.'
                }
                if ($null -eq $firstResult.ErrorMode -or $firstResult.ErrorMode -lt 0) {
                    throw 'Suppressed child did not report its Windows error mode.'
                }
                if ($env:APPDATA -ne $parentAppData -or $env:LOCALAPPDATA -ne $parentLocalAppData -or $env:DEVILUTION_SERVER_HOST -ne $parentHost) {
                    throw 'The launcher changed the parent process environment.'
                }
                if ([GodevilutionNativeErrorMode]::GetErrorMode() -ne $parentErrorMode) {
                    throw 'The launcher changed the parent Windows error mode.'
                }

                $unsuppressedResultPath = Join-Path $work 'unsuppressed.json'
                $unsuppressed = New-TestTracked 'stage-unsuppressed' $childArguments @{
                    CHILD_RESULT = $unsuppressedResultPath
                    TEST_ROLE = 'unsuppressed'
                } $true $false
                Complete-GodotProcess $unsuppressed 10000 'safe child unsuppressed'
                $unsuppressedResult = Get-Content -LiteralPath $unsuppressedResultPath -Raw | ConvertFrom-Json
                if ($script:ObservedSpawnModes.Count -ne 3 -or (([uint32]$script:ObservedSpawnModes[2] -band [uint32]3) -ne [uint32]0)) {
                    throw 'The unsuppressed launch changed crash-dialog bits at the process-spawn boundary.'
                }
                if ($null -eq $unsuppressedResult.ErrorMode -or $unsuppressedResult.ErrorMode -lt 0) {
                    throw 'Unsuppressed child did not report its Windows error mode.'
                }
                if ([GodevilutionNativeErrorMode]::GetErrorMode() -ne $parentErrorMode) {
                    throw 'The unsuppressed launch changed the parent Windows error mode.'
                }

                function Assert-NonZeroChild([string]$Stage, [string]$Code) {
                    $resultPath = Join-Path $work ($Stage + '.json')
                    $tracked = New-TestTracked $Stage $childArguments @{
                        CHILD_RESULT = $resultPath
                        TEST_ROLE = $Stage
                        CHILD_EXIT = $Code
                    } $true $true
                    $rejected = $false
                    try {
                        Complete-GodotProcess $tracked 10000 "safe child $Stage"
                    } catch {
                        $rejected = $true
                    }
                    if (-not $rejected -or $tracked.Record.ExitCode -eq 0) {
                        throw "Nonzero child $Code was accepted."
                    }
                    $firstEndedUtc = $tracked.Record.EndedUtc
                    $firstExitCode = $tracked.Record.ExitCode
                    $repeatedRejected = $false
                    try { Complete-GodotProcess $tracked 0 "safe child $Stage repeated" } catch { $repeatedRejected = $true }
                    if (-not $repeatedRejected -or $tracked.Record.EndedUtc -ne $firstEndedUtc -or $tracked.Record.ExitCode -ne $firstExitCode) {
                        throw "Repeated completion changed or accepted nonzero child $Code."
                    }
                }
                Assert-NonZeroChild 'exit-23' '23'
                Assert-NonZeroChild 'exit-av' '-1073741819'

                $timeoutResultPath = Join-Path $work 'timeout.json'
                $timeout = New-TestTracked 'timeout' $childArguments @{
                    CHILD_RESULT = $timeoutResultPath
                    TEST_ROLE = 'timeout'
                    CHILD_SLEEP_MS = '5000'
                } $true $true
                $timedOut = $false
                try { Complete-GodotProcess $timeout 1 'safe timeout' } catch { $timedOut = $true }
                if (-not $timedOut -or $timeout.Record.EndedUtc -ne $null -or $timeout.Record.Error -notmatch 'timed out') {
                    throw 'A running child did not produce timeout evidence.'
                }
                Stop-GodotProcess $timeout
                if ($timeout.Record.EndedUtc -eq $null -or $timeout.Record.ExitCode -eq $null -or $timeout.Record.Error -notmatch 'timed out') {
                    throw 'Stopping a timed-out child did not preserve its first error.'
                }
                $primaryFailurePath = Join-Path $work 'primary-failure.json'
                $primaryError = $null
                try {
                    New-TestTracked 'primary-failure' $childArguments @{
                        CHILD_RESULT = $primaryFailurePath
                        TEST_ROLE = 'primary-failure'
                        CHILD_SLEEP_MS = '30000'
                    } $true $true | Out-Null
                } catch {
                    $primaryError = $_.Exception.Message
                }
                if ($primaryError -notmatch 'primary evidence write failed') {
                    throw "Evidence-write failure was masked: $primaryError"
                }
                $primaryResult = Get-Content -LiteralPath $primaryFailurePath -Raw | ConvertFrom-Json
                $remainingPrimary = Get-Process -Id ([int]$primaryResult.ProcessId) -ErrorAction SilentlyContinue
                if ($null -ne $remainingPrimary) {
                    try {
                        # Windows can still enumerate a terminal process object.
                        # Presence alone does not establish a running child.
                        if (-not $remainingPrimary.HasExited) {
                            throw "Evidence-write failure left child process $($primaryResult.ProcessId) running."
                        }
                    } finally { $remainingPrimary.Dispose() }
                }

                function Assert-ImportRejected([string]$Stage, [string]$LogText) {
                    $resultPath = Join-Path $work ($Stage + '.json')
                    $logPath = Join-Path $work ($Stage + '.log')
                    $tracked = New-TestTracked $Stage $childArguments @{
                        CHILD_RESULT = $resultPath
                        TEST_ROLE = $Stage
                        CHILD_LOG = $logPath
                        CHILD_LOG_TEXT = $LogText
                    } $true $true
                    Complete-GodotProcess $tracked 10000 "safe import $Stage"
                    $rejected = $false
                    try { Assert-GodotImportLog $tracked $logPath } catch { $rejected = $true }
                    if (-not $rejected) { throw "Import log $Stage was accepted unexpectedly." }
                }
                $missingResultPath = Join-Path $work 'import-missing.json'
                $missing = New-TestTracked 'import-missing' $childArguments @{
                    CHILD_RESULT = $missingResultPath
                    TEST_ROLE = 'import-missing'
                } $true $true
                Complete-GodotProcess $missing 10000 'safe import missing'
                $missingRejected = $false
                try { Assert-GodotImportLog $missing (Join-Path $work 'missing.log') } catch { $missingRejected = $true }
                if (-not $missingRejected) { throw 'A missing import log was accepted.' }
                Assert-ImportRejected 'import-fatal' 'Error saving editor settings'

                $goodResultPath = Join-Path $work 'import-good.json'
                $goodLogPath = Join-Path $work 'import-good.log'
                $good = New-TestTracked 'import-good' $childArguments @{
                    CHILD_RESULT = $goodResultPath
                    TEST_ROLE = 'import-good'
                    CHILD_LOG = $goodLogPath
                    CHILD_LOG_TEXT = 'Import finished successfully.'
                } $true $true
                Complete-GodotProcess $good 10000 'safe import good'
                Assert-GodotImportLog $good $goodLogPath
                Write-Output 'PASS'
            } finally {
                foreach ($trackedValue in @($script:AllTracked)) {
                    if ($null -ne $trackedValue) {
                        try { Stop-GodotProcess $trackedValue } catch { }
                        try { $trackedValue.Process.Dispose() } catch { }
                    }
                }
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-LauncherTest-[0-9a-f]{32}$' -and (Test-Path -LiteralPath $resolvedWork)) {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
                if ($null -ne $originalErrorMode) {
                    [void][GodevilutionNativeErrorMode]::SetErrorMode($originalErrorMode)
                }
            }
"""
        );

        var result = RunPowerShell(script);
        Assert.Contains("PASS", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralOracleHelpersRejectIncompleteOrSkippedEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-CathedralNativeReport", "Assert-CathedralManagedReport", "Get-CathedralOracleArtifactNames", "Get-CathedralOracleArtifacts"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-OracleHelperTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw "Incomplete oracle evidence was accepted by $Action. Native field=$field; managed summary=$badSummary" }
            }
            try {
                $reportPath = Join-Path $work 'native.json'
                Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                $report = [ordered]@{
                    tests = 1; failures = 0; errors = 0; disabled = 0
                    testsuites = @(@{ testsuite = @(@{
                        classname = 'Drlg_l1'; name = 'CathedralFineGeometryLocalAssetOracle'
                        status = 'RUN'; result = 'COMPLETED'
                    }) })
                }
                function Save-NativeReport { [IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 6)) }
                Save-NativeReport
                Assert-CathedralNativeReport $reportPath
                Expect-Rejected { Assert-CathedralNativeReport $reportPath 'CathedralDepthFineGeometryLocalAssetOracle' }
                $report.testsuites[0].testsuite[0].name = 'CathedralDepthFineGeometryLocalAssetOracle'
                Save-NativeReport
                Assert-CathedralNativeReport $reportPath 'CathedralDepthFineGeometryLocalAssetOracle'
                Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                $report.testsuites[0].testsuite[0].result = 'SKIPPED'
                Save-NativeReport
                Expect-Rejected { Assert-CathedralNativeReport $reportPath 'CathedralDepthFineGeometryLocalAssetOracle' }
                $report.testsuites[0].testsuite[0].name = 'CathedralFineGeometryLocalAssetOracle'
                $report.testsuites[0].testsuite[0].result = 'COMPLETED'
                foreach ($field in @('tests', 'failures', 'errors', 'disabled')) {
                    $original = $report[$field]
                    $report[$field] = if ($field -eq 'tests') { 0 } else { 1 }
                    Save-NativeReport
                    Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                    $report[$field] = $original
                }
                foreach ($field in @('classname', 'name', 'status', 'result')) {
                    $nativeCase = $report.testsuites[0].testsuite[0]
                    $original = $nativeCase[$field]
                    $nativeCase[$field] = if ($field -eq 'result') { 'SKIPPED' } else { 'wrong' }
                    Save-NativeReport
                    Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                    $nativeCase[$field] = $original
                }
                [IO.File]::WriteAllText($reportPath, '{}')
                Expect-Rejected { Assert-CathedralNativeReport $reportPath }

                $managedPath = Join-Path $work 'managed.log'
                $goodSummary = "Test run summary: Passed! - oracle.dll`r`n  total: 12`r`n  failed: 0`r`n  succeeded: 12`r`n  skipped: 0`r`n"
                [IO.File]::WriteAllText($managedPath, $goodSummary)
                Assert-CathedralManagedReport $managedPath
                Expect-Rejected { Assert-CathedralManagedReport $managedPath 48 }
                $depthSummary = $goodSummary.Replace('total: 12', 'total: 48').Replace('succeeded: 12', 'succeeded: 48')
                [IO.File]::WriteAllText($managedPath, $depthSummary)
                Assert-CathedralManagedReport $managedPath 48
                Expect-Rejected { Assert-CathedralManagedReport $managedPath }
                foreach ($badSummary in @($depthSummary.Replace('total: 48', 'total: 47'),
                        $depthSummary.Replace('succeeded: 48', 'succeeded: 47'),
                        $depthSummary.Replace('failed: 0', 'failed: 1'),
                        $depthSummary.Replace('skipped: 0', 'skipped: 1'), ($depthSummary + $depthSummary))) {
                    [IO.File]::WriteAllText($managedPath, $badSummary)
                    Expect-Rejected { Assert-CathedralManagedReport $managedPath 48 }
                }
                foreach ($badSummary in @('', 'Test run summary: Failed!',
                        $goodSummary.Replace('total: 12', 'total: 0'),
                        $goodSummary.Replace('total: 12', 'total: 4').Replace('succeeded: 12', 'succeeded: 4'),
                        $goodSummary.Replace('failed: 0', 'failed: 1'),
                        $goodSummary.Replace('succeeded: 12', 'succeeded: 11'),
                        $goodSummary.Replace('skipped: 0', 'skipped: 1'),
                        ($goodSummary + $goodSummary),
                        ($goodSummary + 'Test run summary: Failed!'))) {
                    [IO.File]::WriteAllText($managedPath, $badSummary)
                    Expect-Rejected { Assert-CathedralManagedReport $managedPath }
                }

                Expect-Rejected { Get-CathedralOracleArtifacts $work }
                $artifactNames = @('l1.til', 'l1.sol', 'cathedral-2588-main.json', 'cathedral-2588-previous.json',
                    'cathedral-743271966-main.json', 'cathedral-743271966-previous.json',
                    'cathedral-0-main.json', 'cathedral-0-previous.json',
                    'cathedral-1-main.json', 'cathedral-1-previous.json',
                    'cathedral-2147483648-main.json', 'cathedral-2147483648-previous.json',
                    'cathedral-4294967295-main.json', 'cathedral-4294967295-previous.json')
                foreach ($name in $artifactNames) { [IO.File]::WriteAllText((Join-Path $work $name), 'nonlicensed synthetic input') }
                if (@(Compare-Object $artifactNames @(Get-CathedralOracleArtifactNames)).Count -ne 0) {
                    throw 'The legacy level-1 artifact contract changed.'
                }
                $artifacts = @(Get-CathedralOracleArtifacts $work)
                if ($artifacts.Count -ne 14 -or @($artifacts | Where-Object { $_.Sha256 -notmatch '^[0-9a-f]{64}$' -or $_.Bytes -le 0 }).Count -ne 0) {
                    throw 'Complete oracle artifacts were not hashed.'
                }
                [IO.File]::WriteAllText((Join-Path $work 'l1.sol'), '')
                Expect-Rejected { Get-CathedralOracleArtifacts $work }
                Expect-Rejected { Get-CathedralOracleArtifacts $work -CathedralDepths }
                $depthNames = @(Get-CathedralOracleArtifactNames -CathedralDepths)
                if ($depthNames.Count -ne 50 -or @($depthNames | Select-Object -Unique).Count -ne 50) {
                    throw 'Depth oracle must require two raw inputs and 48 unique comparisons.'
                }
                foreach ($depth in 1..4) {
                    foreach ($seed in @('2588', '743271966', '0', '1', '2147483648', '4294967295')) {
                        foreach ($entry in @('main', 'previous')) {
                            if ("cathedral-depth-$depth-$seed-$entry.json" -notin $depthNames) {
                                throw 'Depth oracle omitted a mandatory depth/seed/entry.'
                            }
                        }
                    }
                }
                foreach ($name in $depthNames) { [IO.File]::WriteAllText((Join-Path $work $name), 'nonlicensed synthetic input') }
                $depthArtifacts = @(Get-CathedralOracleArtifacts $work -CathedralDepths)
                if ($depthArtifacts.Count -ne 50 -or @($depthArtifacts | Where-Object { $_.Sha256 -notmatch '^[0-9a-f]{64}$' -or $_.Bytes -le 0 }).Count -ne 0) {
                    throw 'Complete depth oracle artifacts were not hashed.'
                }
                [IO.File]::WriteAllText((Join-Path $work 'cathedral-depth-4-4294967295-previous.json'), '')
                Expect-Rejected { Get-CathedralOracleArtifacts $work -CathedralDepths }
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-OracleHelperTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralDoorOracleHelpersRequireTheFrozenReportsArtifactsAndScope()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            [
                "Assert-CathedralDoorNativeReports", "Get-CathedralDoorOracleArtifactNames",
                "Get-CathedralDoorOracleArtifacts", "Assert-CathedralDoorRequiredProperty", "Assert-CathedralDoorInteger",
                "Assert-CathedralDoorLayer", "Assert-CathedralDoorLayerHashes", "Assert-CathedralDoorTile",
                "Assert-CathedralDoorObjectState", "Assert-CathedralDoorOracleCase", "Assert-CathedralDoorOracleSchema",
            ],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-DoorOracleHelperTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            function Copy-Record($Record) {
                $copy = [ordered]@{}
                foreach ($key in $Record.Keys) { $copy[$key] = $Record[$key] }
                return $copy
            }
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Invalid Cathedral door oracle evidence was accepted.' }
            }
            try {
                $objectReport = Join-Path $work 'object.json'
                $deltaReport = Join-Path $work 'delta.json'
                function Save-Report([string]$Path, [string]$Name, [string]$Result = 'COMPLETED') {
                    $report = [ordered]@{
                        tests = 1; failures = 0; errors = 0; disabled = 0
                        testsuites = @(@{ testsuite = @(@{
                            classname = 'Drlg_l1'; name = $Name; status = 'RUN'; result = $Result
                        }) })
                    }
                    [IO.File]::WriteAllText($Path, ($report | ConvertTo-Json -Depth 6))
                }
                Save-Report $objectReport 'CathedralDoorNativeObjectOracle'
                Save-Report $deltaReport 'CathedralDoorNativeDeltaOpenOracle'
                Assert-CathedralDoorNativeReports @($objectReport, $deltaReport)
                Assert-CathedralDoorInteger 4294967295 0 4294967295 'synthetic.uint32'
                foreach ($badInteger in @($true, '1', 0.5, 1.0000000000000002, 1e-29, -1, 4294967296)) {
                    Expect-Rejected { Assert-CathedralDoorInteger $badInteger 0 4294967295 'synthetic.uint32' }
                }
                foreach ($badCount in @('1', $true, 1.5)) {
                    $badReport = [IO.File]::ReadAllText($objectReport) | ConvertFrom-Json
                    $badReport.tests = $badCount
                    [IO.File]::WriteAllText($objectReport, ($badReport | ConvertTo-Json -Depth 6))
                    Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }
                    Save-Report $objectReport 'CathedralDoorNativeObjectOracle'
                }
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport) }
                Expect-Rejected { Assert-CathedralDoorNativeReports @($deltaReport, $objectReport) }
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $objectReport) }
                Save-Report $deltaReport 'CathedralDoorNativeDeltaOpenOracle' 'SKIPPED'
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }
                Save-Report $deltaReport 'wrong-case'
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }
                [IO.File]::WriteAllText($objectReport, '{}')
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }

                $names = @(Get-CathedralDoorOracleArtifactNames)
                if ($names.Count -ne 20 -or @($names | Select-Object -Unique).Count -ne 20 -or
                    'l1.til' -notin $names -or 'l1.sol' -notin $names -or
                    'door-oracle-format.json' -notin $names -or
                    'cathedral-door-delta-depth-2-743271966-main.json' -notin $names) {
                    throw 'The frozen Cathedral door artifact contract is incomplete.'
                }
                $artifactDirectory = Join-Path $work 'artifacts'
                New-Item -ItemType Directory -Path $artifactDirectory | Out-Null
                foreach ($name in $names) { [IO.File]::WriteAllText((Join-Path $artifactDirectory $name), 'synthetic native evidence') }
                $artifacts = @(Get-CathedralDoorOracleArtifacts $artifactDirectory)
                if ($artifacts.Count -ne 20 -or @($artifacts | Where-Object { $_.Bytes -le 0 -or $_.Sha256 -cnotmatch '^[0-9a-f]{64}$' }).Count -ne 0) {
                    throw 'The complete Cathedral door artifact set was not hashed.'
                }
                [IO.File]::WriteAllText((Join-Path $artifactDirectory 'unexpected.json'), 'out of scope')
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.File]::Delete((Join-Path $artifactDirectory 'unexpected.json'))
                $hidden = Join-Path $artifactDirectory '.hidden-extra'
                [IO.File]::WriteAllText($hidden, 'hidden out of scope')
                [IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Hidden)
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.File]::Delete($hidden)
                $hiddenDirectory = Join-Path $artifactDirectory 'hidden-directory'
                New-Item -ItemType Directory -Path $hiddenDirectory | Out-Null
                [IO.File]::SetAttributes($hiddenDirectory, [IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::Directory)
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.Directory]::Delete($hiddenDirectory)
                $lower = Join-Path $artifactDirectory 'l1.til'
                $upper = Join-Path $artifactDirectory 'L1.TIL'
                $renameStage = Join-Path $artifactDirectory 'rename-stage.tmp'
                [IO.File]::Move($lower, $renameStage)
                [IO.File]::Move($renameStage, $upper)
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.File]::Move($upper, $renameStage)
                [IO.File]::Move($renameStage, $lower)
                [IO.File]::Delete((Join-Path $artifactDirectory 'cathedral-door-depth-4-743271966-previous.json'))
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }

                $badCase = Join-Path $work 'cathedral-door-depth-1-2588-main.json'
                [IO.File]::WriteAllText($badCase, '{}')
                Expect-Rejected { Assert-CathedralDoorOracleCase $badCase 'cathedral-door-depth-1-2588-main.json' 'sync' }
                $metadata = Join-Path $work 'door-oracle-format.json'
                [IO.File]::WriteAllText($metadata, '{}')
                Expect-Rejected { Assert-CathedralDoorOracleSchema $work }
                $validMetadata = [ordered]@{ format = 'godevilution-cathedral-l1-door-oracle'; version = 1;
                    policy = 'native-add-l1-objects-door-only'; caseCount = 16; depths = @(1,2,3,4);
                    seeds = @(2588,743271966); entries = @('main','previous'); rawAssets = @('l1.til','l1.sol');
                    width = 112; height = 112; rowMajor = 'y*width+x'; deltaCase = 'cathedral-door-delta-depth-2-743271966-main.json' }
                foreach ($field in @('version', 'width', 'seeds')) {
                    $testMetadata = Copy-Record $validMetadata
                    if ($field -eq 'seeds') { $testMetadata.seeds = @('2588',743271966) }
                    else { $testMetadata.$field = [string]$testMetadata.$field }
                    [IO.File]::WriteAllText($metadata, ($testMetadata | ConvertTo-Json -Depth 4))
                    $message = ''
                    try { Assert-CathedralDoorOracleSchema $work } catch { $message = $_.Exception.Message }
                    if ($message -notlike "*metadata.$field*") { throw "The $field metadata type guard was not exercised." }
                }

                $casePath = Join-Path $work 'synthetic-valid-case.json'
                $cellCount = 112 * 112
                function New-Layer {
                    $values = [int[]]::new($cellCount)
                    return [ordered]@{ pieces = $values; special = $values.Clone(); transparency = $values.Clone();
                        properties = $values.Clone(); solid = $values.Clone(); missileBlocked = $values.Clone();
                        blockLight = $values.Clone(); walkable = $values.Clone(); itemBlocking = $values.Clone() }
                }
                function New-Hashes {
                    return [ordered]@{ fullLayer = '0x0000000000000000'; pieces = '0x0000000000000000';
                        special = '0x0000000000000000'; transparency = '0x0000000000000000';
                        properties = '0x0000000000000000'; solid = '0x0000000000000000';
                        missileBlocked = '0x0000000000000000'; blockLight = '0x0000000000000000';
                        walkable = '0x0000000000000000'; itemBlocking = '0x0000000000000000' }
                }
                function New-ObjectState([int]$State, [int]$Frame, [int]$Selection, [bool]$Pre, [bool]$Miss) {
                    return [ordered]@{ state = $State; var1OneBased = 1; var2OneBased = 1; animationFrame = $Frame;
                        selectionRegion = $Selection; doorFlag = $true; preFlag = $Pre; missFlag = $Miss; solidFlag = $false }
                }
                function New-Tile([int]$Piece) {
                    return [ordered]@{ piece = $Piece; special = 0; transparency = 0; properties = 0; solid = 0;
                        missileBlocked = 0; blockLight = 0; walkable = 0; itemBlocking = 0 }
                }
                $layer = New-Layer
                $hashes = New-Hashes
                $closedObject = New-ObjectState 0 0 3 $false $false
                $openObject = New-ObjectState 1 2 2 $true $true
                $reclosedObject = New-ObjectState 0 0 3 $false $false
                $closedTile = New-Tile 0
                $openTile = New-Tile 1
                $changed = [ordered]@{ index = 0; x = 0; y = 0; closed = $closedTile; open = $openTile; reclosed = $closedTile }
                $door = [ordered]@{ ordinal = 0; freshBaselineOrdinal = 1; openPath = 'sync'; nativeTypeId = 1;
                    nativeType = 'OBJ_L1LDOOR'; position = @(1, 1); var1OneBased = 1; var2OneBased = 1;
                    sourcePieceObserved = 0; neighborPieceObserved = 0; closedObject = $closedObject;
                    openObject = $openObject; reclosedObject = $reclosedObject; closedLayerHash = '0x0000000000000000';
                    openLayerHash = '0x0000000000000001'; reclosedLayerHash = '0x0000000000000000';
                    reclosedLayerEqualsClosed = $true; reclosedObjectEqualsClosed = $true; changedCells = @($changed) }
                $syntheticCase = [ordered]@{ format = 'godevilution-cathedral-l1-door-oracle'; version = 1;
                    policy = 'native-add-l1-objects-door-only'; level = 1; entry = 'main'; inputSeed = 2588;
                    attemptSeed = 0; finalRandomState = 0; doorScanRandomStateBefore = 0; doorScanRandomStateAfter = 0;
                    nativeWidth = 112; nativeHeight = 112; rowMajor = 'y*nativeWidth+x'; baselineRebuildCount = 1;
                    closedBaselineHashes = $hashes; closedBaseline = $layer;
                    closedBaselines = @([ordered]@{ ordinal = 1; doorScanRandomStateBefore = 0; doorScanRandomStateAfter = 0;
                        hashes = $hashes; layer = $layer }); doors = @($door) }
                $validJson = $syntheticCase | ConvertTo-Json -Compress -Depth 8
                [IO.File]::WriteAllText($casePath, $validJson)
                Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync'
                function Check-CaseMutation([scriptblock]$Mutation) {
                    $testCase = $validJson | ConvertFrom-Json
                    & $Mutation $testCase
                    [IO.File]::WriteAllText($casePath, ($testCase | ConvertTo-Json -Compress -Depth 8))
                    Expect-Rejected { Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync' }
                }
                foreach ($field in @('version', 'nativeWidth', 'nativeHeight', 'inputSeed', 'attemptSeed')) {
                    Check-CaseMutation { param($testCase) $testCase.$field = [string]$testCase.$field }
                }
                foreach ($badValue in @($true, '0', 0.5, 1.0000000000000002, 1e-29, -1, 65536)) {
                    $badLayer = ($validJson | ConvertFrom-Json).closedBaseline
                    $badLayer.pieces[0] = $badValue
                    $parsedBadLayer = ($badLayer | ConvertTo-Json -Compress -Depth 3) | ConvertFrom-Json
                    Expect-Rejected { Assert-CathedralDoorLayer $parsedBadLayer 'synthetic.closedBaseline' }
                    Check-CaseMutation { param($testCase) $testCase.closedBaseline.pieces[0] = $badValue }
                }
                Check-CaseMutation { param($testCase) $testCase.doors[0].changedCells[0].index = '0' }
                Check-CaseMutation { param($testCase) $testCase.doors[0].changedCells += $testCase.doors[0].changedCells[0] }
                Check-CaseMutation { param($testCase) $testCase.doors[0].changedCells[0].closed.piece = 2 }
                Check-CaseMutation { param($testCase) $testCase.closedBaseline.pieces[0] = 1 }
                Check-CaseMutation { param($testCase) $testCase.doors[0].closedLayerHash = '0x1111111111111111' }
                Check-CaseMutation { param($testCase) $testCase.doors[0].reclosedLayerEqualsClosed = $false }
                Check-CaseMutation { param($testCase) $testCase.doors[0].reclosedObjectEqualsClosed = $false }
                Check-CaseMutation { param($testCase) $testCase.doors[0].reclosedLayerHash = '0x1111111111111111' }
                Check-CaseMutation { param($testCase) $testCase.doors[0].openObject.animationFrame = 1 }
                Check-CaseMutation { param($testCase) $testCase.doors[0].openObject.missFlag = $false }
                $nonRestoringCase = $validJson | ConvertFrom-Json
                $nonRestoringCase.doors[0].changedCells[0].reclosed.piece = 2
                $nonRestoringCase.doors[0].reclosedLayerEqualsClosed = $false
                $nonRestoringCase.doors[0].reclosedLayerHash = '0x0000000000000002'
                [IO.File]::WriteAllText($casePath, ($nonRestoringCase | ConvertTo-Json -Compress -Depth 8))
                Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync'
                $noOpDoor = Copy-Record $door
                $noOpDoor.ordinal = 1
                $noOpDoor.openObject = Copy-Record $closedObject
                $noOpDoor.reclosedObject = Copy-Record $closedObject
                $noOpDoor.changedCells = @([ordered]@{ index = 0; x = 0; y = 0; closed = $closedTile; open = $closedTile; reclosed = $closedTile })
                $syntheticCase.doors = @($door, $noOpDoor)
                [IO.File]::WriteAllText($casePath, ($syntheticCase | ConvertTo-Json -Depth 8))
                Expect-Rejected { Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync' }
                $badLayer = Copy-Record $layer
                $badPieces = [object[]]$layer.pieces.Clone()
                $badPieces[0] = 'not-an-integer'
                $badLayer.pieces = $badPieces
                $syntheticCase.doors = @($door)
                $syntheticCase.closedBaseline = $badLayer
                $syntheticCase.closedBaselines = @([ordered]@{ ordinal = 1; doorScanRandomStateBefore = 0; doorScanRandomStateAfter = 0;
                    hashes = $hashes; layer = $badLayer })
                [IO.File]::WriteAllText($casePath, ($syntheticCase | ConvertTo-Json -Depth 8))
                Expect-Rejected { Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync' }
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-DoorOracleHelperTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralDoorOracleRepeatabilityRejectsChangedTwentyArtifactCaptures()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-CathedralRepeatArtifacts"],
            """
            $first = @(1..20 | ForEach-Object {
                [pscustomobject]@{ Name = ('artifact-{0:D2}.json' -f $_); Bytes = $_ + 1; Sha256 = (('{0:x2}' -f $_) * 32) }
            })
            $repeated = @($first | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Bytes = $_.Bytes; Sha256 = $_.Sha256 } })
            Assert-CathedralRepeatArtifacts $first $repeated 20
            $repeated[19].Sha256 = ('f' * 64)
            $rejected = $false
            try { Assert-CathedralRepeatArtifacts $first $repeated 20 } catch { $rejected = $true }
            if (-not $rejected) { throw 'A changed twentieth artifact hash was accepted.' }
            Write-Output 'PASS'
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralDoorOracleModeIsMutuallyExclusiveWithDepthMode()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var scriptPath = Path.Combine(FindRepositoryRoot(), "tools", "test-cathedral-geometry.ps1");
        var script = File.ReadAllText(scriptPath);
        Assert.Contains("[switch]$CathedralDoors", script, StringComparison.Ordinal);
        Assert.Contains("if ($CathedralDoors -and $CathedralDepths)", script, StringComparison.Ordinal);
        Assert.Contains("-CathedralDoors and -CathedralDepths are mutually exclusive.", script, StringComparison.Ordinal);
        Assert.Contains("PASS", RunPowerShell($$"""
            $rejected = $false
            try { & {{QuotePowerShell(scriptPath)}} -CathedralDoors -CathedralDepths }
            catch {
                if ($_.Exception.Message -notlike '*mutually exclusive*') { throw }
                $rejected = $true
            }
            if (-not $rejected) { throw 'Conflicting oracle modes were accepted.' }
            Write-Output 'PASS'
            """), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralOracleRepeatabilityRejectsChangedOrIncompleteCaptures()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-CathedralRepeatArtifacts"],
            """
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Invalid repeated native evidence was accepted.' }
            }
            $first = @(
                [pscustomobject]@{ Name = 'first.json'; Bytes = 11; Sha256 = ('a' * 64) },
                [pscustomobject]@{ Name = 'second.json'; Bytes = 22; Sha256 = ('b' * 64) })
            function New-RepeatedCapture {
                @($first | ForEach-Object {
                    [pscustomobject]@{ Name = $_.Name; Bytes = $_.Bytes; Sha256 = $_.Sha256 }
                })
            }
            $repeated = @(New-RepeatedCapture)
            Assert-CathedralRepeatArtifacts $first $repeated 2
            Assert-CathedralRepeatArtifacts $first @($repeated[1], $repeated[0]) 2
            Expect-Rejected { Assert-CathedralRepeatArtifacts $first @($repeated[0]) 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts @($first[0]) $repeated 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts $first @($repeated[0], $repeated[0]) 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts @($first[0], $first[0]) $repeated 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts $first $repeated 0 }
            foreach ($field in @('Name', 'Bytes', 'Sha256')) {
                foreach ($invalid in $(switch ($field) {
                    'Name' { @('', 'FIRST.json', 'missing.json') }
                    'Bytes' { @(0, 12) }
                    'Sha256' { @('', 'malformed', ('A' * 64), ('c' * 64)) }
                })) {
                    $repeated = @(New-RepeatedCapture)
                    $repeated[0].$field = $invalid
                    Expect-Rejected { Assert-CathedralRepeatArtifacts $first $repeated 2 }
                    Expect-Rejected { Assert-CathedralRepeatArtifacts $repeated $first 2 }
                }
            }
            Write-Output 'PASS'
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralOracleBuildSelectionRequiresTheMatchingDebugSourceTree()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Find-CathedralOracleBuild"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-OracleBuildTest-' + [guid]::NewGuid().ToString('N'))
            $build = Join-Path $work 'build/x64-Debug-authoritative-vcpkg'
            $vs = Join-Path $work 'vs'
            $developer = Join-Path $vs 'Common7/Tools/VsDevCmd.bat'
            $cmake = Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
            New-Item -ItemType Directory -Path $build, ([IO.Path]::GetDirectoryName($developer)), ([IO.Path]::GetDirectoryName($cmake)) | Out-Null
            [IO.File]::WriteAllText($developer, 'synthetic developer environment')
            [IO.File]::WriteAllText($cmake, 'synthetic CMake path')
            $compiler = Join-Path $vs 'VC/Tools/MSVC/test/bin/Hostx64/x64/cl.exe'
            $cachePath = Join-Path $build 'CMakeCache.txt'
            function Save-Cache([string]$Source, [string]$Mode, [string]$Testing, [string]$Compiler) {
                [IO.File]::WriteAllText($cachePath, "UNPACKED_MPQS:BOOL=OFF`r`nBUILD_TESTING:BOOL=$Testing`r`nCMAKE_BUILD_TYPE:STRING=$Mode`r`nCMAKE_HOME_DIRECTORY:INTERNAL=$Source`r`nCMAKE_CXX_COMPILER:FILEPATH=$Compiler`r`n")
            }
            function Expect-Rejected {
                $rejected = $false
                try { Find-CathedralOracleBuild $work | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'An incompatible native build tree was selected.' }
            }
            try {
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' $compiler
                $selected = Find-CathedralOracleBuild $work
                if ($selected.Directory -ne $build -or $selected.DeveloperCommand -ne $developer -or $selected.CMake -ne $cmake) {
                    throw 'The matching developer environment was not selected.'
                }
                Save-Cache (Join-Path $work 'other-project') 'Debug' 'ON' $compiler
                Expect-Rejected
                Save-Cache $work 'Release' 'ON' $compiler
                Expect-Rejected
                Save-Cache $work 'Debug' 'OFF' $compiler
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' (Join-Path $vs 'bin/clang.exe')
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' 'C:/missing-vs/VC/cl.exe'
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' $compiler
                [IO.File]::WriteAllText($cachePath, ([IO.File]::ReadAllText($cachePath).Replace('UNPACKED_MPQS:BOOL=OFF', 'UNPACKED_MPQS:BOOL=ON')))
                Expect-Rejected
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-OracleBuildTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralOracleNativeInvocationRetainsStderrAndNonzeroExitCodes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Invoke-CathedralNativeOracle"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-OracleInvokeTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                $child = Join-Path $work 'harmless native child.cmd'
                foreach ($testName in @('CathedralFineGeometryLocalAssetOracle', 'CathedralDepthFineGeometryLocalAssetOracle',
                        'CathedralDoorNativeObjectOracle', 'CathedralDoorNativeDeltaOpenOracle')) {
                  foreach ($expectedCode in @(0, 23, -1073741819)) {
                    [IO.File]::WriteAllText($child, "@echo off`r`necho synthetic oracle stdout %*`r`necho INFO: synthetic oracle stderr 1>&2`r`nexit /b $expectedCode`r`n")
                    $log = Join-Path $work 'retained native log.txt'
                    $actualCode = Invoke-CathedralNativeOracle $child (Join-Path $work 'unused report.json') $log $testName
                    if ($actualCode -ne $expectedCode) { throw "Native exit $expectedCode became $actualCode." }
                    $output = [IO.File]::ReadAllText($log)
                    if ($output -notmatch 'synthetic oracle stdout' -or $output -notmatch 'synthetic oracle stderr' -or
                        $output -notmatch [regex]::Escape("--gtest_filter=Drlg_l1.$testName")) {
                        throw 'Native output was not retained.'
                    }
                  }
                }
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-OracleInvokeTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void GodotProjectStagingPreservesSourcesAndRejectsUnexpectedImports()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            [
                "Get-CanonicalPath", "Assert-PathWithinRepository", "Get-ProjectRelativePath",
                "Assert-NoReparsePoint", "Assert-NoReparseAncestry",
                "Get-IsolatedProjectFiles", "Get-ProjectTreeHash", "Get-FileSha256",
                "Read-ProjectXml", "Resolve-ValidatedProjectReference", "Get-ProjectSignature",
                "Get-ProjectMetadata", "Test-ExactStringSequence", "Set-StagedProjectReferences",
                "Copy-IsolatedProjectTree", "Rewrite-StagedSolutionReferences",
                "New-IsolatedGodotProject", "Complete-IsolatedGodotProject",
                "Write-ProjectStageEvidence", "Parse-SupportedGodotEngineVersion",
            ],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-ProjectStageTest-' + [guid]::NewGuid().ToString('N'))
            $repositoryRoot = Join-Path $work 'repository with spaces'
            $source = Join-Path $repositoryRoot 'godot/Devilution.Client'
            $protocol = Join-Path $repositoryRoot 'protocol/Devilution.Protocol/Devilution.Protocol.csproj'
            $clientProtocol = Join-Path $repositoryRoot 'godot/Devilution.Client.Protocol/Devilution.Client.Protocol.csproj'
            $config = Join-Path $repositoryRoot 'godot/NuGet.Config'
            New-Item -ItemType Directory -Path $source, (Split-Path $protocol), (Split-Path $clientProtocol) | Out-Null
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw "An invalid staged project was accepted: $Action" }
            }
            function Assert-OriginalBytes {
                if ([IO.File]::ReadAllText($project) -cne $projectText -or
                    [IO.File]::ReadAllText($backup) -cne 'pre-existing user backup' -or
                    [IO.File]::ReadAllText($localEdit) -cne 'uncommitted local source edit' -or
                    [IO.File]::ReadAllText($config) -cne $configText) {
                    throw 'Staging or rejection modified original user files.'
                }
            }
            try {
                if ((Parse-SupportedGodotEngineVersion '4.7.2.stable.mono.official.synthetic') -cne '4.7.2') {
                    throw 'The actual stable Mono version format was not recognized.'
                }
                foreach ($badVersion in @('', '4.7.2.stable.official.synthetic',
                        '4.7.2.rc1.mono.official.synthetic', '4.8.1.stable.mono.official.synthetic',
                        '3.7.2.stable.mono.official.synthetic',
                        ('4.7.2.rc1.mono.synthetic' + [Environment]::NewLine + 'warning: stable mono'),
                        ('4.7.2.stable.mono.synthetic' + [Environment]::NewLine + '4.7.1.stable.mono.synthetic'))) {
                    Expect-Rejected { Parse-SupportedGodotEngineVersion $badVersion }
                }
                $project = Join-Path $source 'Devilution.Client.csproj'
                $projectText = '<Project Sdk="Godot.NET.Sdk/4.7.1"><!-- local project comment --><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include="../../protocol/Devilution.Protocol/Devilution.Protocol.csproj" Aliases="global" /><ProjectReference Include="../Devilution.Client.Protocol/Devilution.Client.Protocol.csproj" /></ItemGroup></Project>'
                [IO.File]::WriteAllText($project, $projectText)
                [IO.File]::WriteAllText($protocol, '<Project Sdk="Microsoft.NET.Sdk" />')
                [IO.File]::WriteAllText($clientProtocol, '<Project Sdk="Microsoft.NET.Sdk" />')
                $configText = '<configuration><packageSources><clear /><add key="private-local-test" value="https://example.invalid/v3/index.json" /></packageSources></configuration>'
                [IO.File]::WriteAllText($config, $configText)
                [IO.File]::WriteAllText((Join-Path $source 'project.godot'), 'config_version=5')
                $localEdit = Join-Path $source 'Main.cs'
                [IO.File]::WriteAllText($localEdit, 'uncommitted local source edit')
                $backup = Join-Path $source 'Devilution.Client.csproj.old'
                [IO.File]::WriteAllText($backup, 'pre-existing user backup')
                foreach ($cache in @('.godot', 'bin', 'obj', 'nested/obj')) {
                    $cachePath = Join-Path $source $cache
                    New-Item -ItemType Directory -Path $cachePath -Force | Out-Null
                    [IO.File]::WriteAllText((Join-Path $cachePath 'stale.txt'), 'generated stale output')
                }
                New-Item -ItemType Directory -Path (Join-Path $source 'assets') | Out-Null
                [IO.File]::WriteAllText((Join-Path $source 'assets/player.svg'), '<svg />')
                $solution = Join-Path $source 'Devilution.Client.sln'
                $solutionText = @(
                    'Project("{TYPE}") = "Devilution.Client", "Devilution.Client.csproj", "{CLIENT}"'
                    'EndProject'
                    'Project("{TYPE}") = "Devilution.Protocol", "..\..\protocol\Devilution.Protocol\Devilution.Protocol.csproj", "{PROTOCOL}"'
                    'EndProject'
                ) -join [Environment]::NewLine
                [IO.File]::WriteAllText($solution, $solutionText + [Environment]::NewLine)
                $stagePath = Join-Path $work 'capture with spaces/client'
                $stage = New-IsolatedGodotProject $source $stagePath
                if ($stage -is [array]) { throw 'Staging emitted extra pipeline objects.' }
                Assert-OriginalBytes
                if ([IO.File]::ReadAllText((Join-Path $stagePath 'Main.cs')) -cne 'uncommitted local source edit' -or
                    [IO.File]::ReadAllText((Join-Path $stagePath 'assets/player.svg')) -cne '<svg />') {
                    throw 'Staging omitted authored sources or resources.'
                }
                foreach ($excluded in @('.godot', 'bin', 'obj', 'nested/obj', 'Devilution.Client.csproj.old')) {
                    if (Test-Path -LiteralPath (Join-Path $stagePath $excluded)) {
                        throw "A stale generated artifact was staged: $excluded"
                    }
                }
                if ([IO.File]::ReadAllText($stage.StagedNuGetConfig) -cne $configText) {
                    throw 'Staging lost the original NuGet source policy.'
                }
                $stagedText = [IO.File]::ReadAllText($stage.StagedProject)
                if ($stagedText -notmatch 'Aliases="global"' -or $stagedText -notmatch 'local project comment') {
                    throw 'Staging removed project metadata.'
                }
                $stagedSolution = [IO.File]::ReadAllText((Join-Path $stagePath 'Devilution.Client.sln'))
                if (-not $stagedSolution.Contains('"' + $stage.StagedProject + '"') -or
                    -not $stagedSolution.Contains('"' + $protocol + '"') -or
                    $stagedSolution.Contains('"' + $project + '"')) {
                    throw 'The staged solution can build the original client or has unresolved references.'
                }
                [IO.File]::WriteAllText($stage.StagedProject, $stagedText.Replace('Godot.NET.Sdk/4.7.1', 'Godot.NET.Sdk/4.7.2'))
                $completed = Complete-IsolatedGodotProject $stage '4.7.2'
                if ($completed.ImportedSdk -cne 'Godot.NET.Sdk/4.7.2') { throw 'Imported SDK identity was lost.' }
                $evidence = Join-Path $work 'project-stage.json'
                Write-ProjectStageEvidence $completed '4.7.2' $evidence
                $record = [IO.File]::ReadAllText($evidence) | ConvertFrom-Json
                if ($record.SourceSdk -cne 'Godot.NET.Sdk/4.7.1' -or
                    $record.ImportedSdk -cne 'Godot.NET.Sdk/4.7.2' -or
                    $record.EngineVersion -cne '4.7.2' -or
                    $record.SourceHash -notmatch '^[0-9a-f]{64}$' -or
                    $record.StageHash -notmatch '^[0-9a-f]{64}$') {
                    throw 'Staged project evidence is incomplete.'
                }
                $importedText = [IO.File]::ReadAllText($stage.StagedProject)
                [IO.File]::WriteAllText($stage.StagedProject, $importedText.Replace('net8.0', 'net9.0'))
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedProject, $importedText.Replace('4.7.2', '4.7.3'))
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedProject, $importedText.Replace($protocol, $clientProtocol))
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedProject, $importedText)
                [IO.File]::WriteAllText($stage.StagedNuGetConfig, '<configuration />')
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::Delete($stage.StagedNuGetConfig)
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedNuGetConfig, $configText)
                Expect-Rejected { New-IsolatedGodotProject $source $stagePath }
                Assert-OriginalBytes
                [IO.File]::WriteAllText($project, $projectText.Replace('../../protocol/Devilution.Protocol/Devilution.Protocol.csproj', 'missing.csproj'))
                Expect-Rejected { New-IsolatedGodotProject $source (Join-Path $work 'missing-ref/client') }
                [IO.File]::WriteAllText($project, $projectText)
                [IO.File]::WriteAllText($localEdit, 'concurrent user edit that must not be overwritten')
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                if ([IO.File]::ReadAllText($localEdit) -cne 'concurrent user edit that must not be overwritten') {
                    throw 'Rejection overwrote a concurrent user edit.'
                }
                [IO.File]::WriteAllText($localEdit, 'uncommitted local source edit')
                Assert-OriginalBytes
                $outside = Join-Path $work 'outside fake repository'
                New-Item -ItemType Directory -Path $outside | Out-Null
                [IO.File]::WriteAllText((Join-Path $outside 'Devilution.Protocol.csproj'), '<Project />')
                $assetLink = Join-Path $source 'assets/linked-external'
                $referenceLink = Join-Path $repositoryRoot 'protocol/linked-external'
                try {
                    New-Item -ItemType Junction -Path $assetLink -Target $outside | Out-Null
                    Expect-Rejected { New-IsolatedGodotProject $source (Join-Path $work 'reparse-source/client') }
                    [IO.Directory]::Delete($assetLink, $false)
                    New-Item -ItemType Junction -Path $referenceLink -Target $outside | Out-Null
                    [IO.File]::WriteAllText($project, $projectText.Replace(
                        '../../protocol/Devilution.Protocol/Devilution.Protocol.csproj',
                        '../../protocol/linked-external/Devilution.Protocol.csproj'))
                    Expect-Rejected { New-IsolatedGodotProject $source (Join-Path $work 'reparse-reference/client') }
                    [IO.File]::WriteAllText($project, $projectText)
                } finally {
                    foreach ($link in @($assetLink, $referenceLink)) {
                        if (Test-Path -LiteralPath $link) { [IO.Directory]::Delete($link, $false) }
                    }
                }
                if (-not (Test-Path -LiteralPath (Join-Path $outside 'Devilution.Protocol.csproj'))) {
                    throw 'Removing the test junction removed its target.'
                }
                Assert-OriginalBytes
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-ProjectStageTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void GodotRedirectedOutputIsRetainedAfterSuccessFailureAndTimeout()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-WritableDirectory", "Write-ProcessEvidence", "Start-GodotChildProcess",
                "Stop-OwnedLocalProcess",
                "New-GodotProcess", "Write-GodotRedirectedOutput",
                "Complete-GodotProcess", "Stop-GodotProcess"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-RedirectTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $captureRoot = $work
            $processEvidenceRoot = Join-Path $work 'processes'
            New-Item -ItemType Directory -Path $processEvidenceRoot | Out-Null
            $repositoryRoot = $work
            $GodotExecutable = (Get-Command powershell.exe -CommandType Application).Source
            $owned = @()
            try {
                $child = Join-Path $work 'harmless redirected child.ps1'
                [IO.File]::WriteAllText($child, @'
            param([int]$ExitCode, [int]$Delay)
            [Console]::Out.WriteLine('retained redirected stdout')
            [Console]::Error.WriteLine('retained redirected stderr')
            [IO.File]::WriteAllText($env:REDIRECT_READY_PATH, 'ready')
            Start-Sleep -Milliseconds $Delay
            exit $ExitCode
            '@)
                foreach ($scenario in @(
                    @{ Name = 'success'; Exit = 0; Delay = 1; Timeout = $false },
                    @{ Name = 'failure'; Exit = 23; Delay = 1; Timeout = $false },
                    @{ Name = 'timeout'; Exit = 0; Delay = 30000; Timeout = $true })) {
                    $name = $scenario.Name
                    $ready = Join-Path $work ($name + '.ready')
                    $stdout = Join-Path $work ($name + '.stdout.log')
                    $stderr = Join-Path $work ($name + '.stderr.log')
                    $arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -ExitCode {1} -Delay {2}' -f $child, $scenario.Exit, $scenario.Delay
                    $tracked = New-GodotProcess $name $arguments @{ REDIRECT_READY_PATH = $ready } $true $false $stdout $stderr
                    $owned += $tracked
                    $clock = [Diagnostics.Stopwatch]::StartNew()
                    while (-not (Test-Path -LiteralPath $ready)) {
                        if ($tracked.Process.HasExited -or $clock.Elapsed.TotalSeconds -gt 10) { throw 'Harmless child did not become ready.' }
                        Start-Sleep -Milliseconds 25
                    }
                    $rejected = $false
                    try {
                        $wait = if ($scenario.Timeout) { 10 } else { 10000 }
                        Complete-GodotProcess $tracked $wait $name
                    } catch { $rejected = $true }
                    if ($rejected -ne ($scenario.Timeout -or $scenario.Exit -ne 0)) {
                        throw "Incorrect process result for $name."
                    }
                    $primaryError = $tracked.Record.Error
                    Stop-GodotProcess $tracked
                    if ($tracked.Record.Error -cne $primaryError -or
                        [IO.File]::ReadAllText($stdout) -notmatch 'retained redirected stdout' -or
                        [IO.File]::ReadAllText($stderr) -notmatch 'retained redirected stderr') {
                        throw "Cleanup lost $name diagnostics or its primary error."
                    }
                    $record = [IO.File]::ReadAllText($tracked.EvidencePath) | ConvertFrom-Json
                    if ($null -eq $record.EndedUtc -or $null -eq $record.ExitCode) {
                        throw 'Terminated child evidence is incomplete.'
                    }
                    if (-not $scenario.Timeout -and $record.ExitCode -ne $scenario.Exit) {
                        throw 'Redirected capture changed the native exit code.'
                    }
                }
                Write-Output 'PASS'
            } finally {
                foreach ($tracked in $owned) {
                    try { Stop-GodotProcess $tracked } catch { }
                    $tracked.Process.Dispose()
                }
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-RedirectTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void GodotPerformanceEvidenceRejectsInvalidWindowsAndComputesExactStatistics()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Get-PerformanceMetricSummary", "Select-PerformancePublicFields", "Assert-GodotPerformanceEvidence"],
            """
            $statistics = Get-PerformanceMetricSummary @(4, 1, 3, 2)
            if ($statistics.Count -ne 4 -or $statistics.MinimumMilliseconds -ne 1 -or
                $statistics.MeanMilliseconds -ne 2.5 -or $statistics.P50Milliseconds -ne 2 -or
                $statistics.P95Milliseconds -ne 4 -or $statistics.P99Milliseconds -ne 4 -or
                $statistics.MaximumMilliseconds -ne 4) { throw 'Incorrect exact nearest-rank statistics.' }
            foreach ($invalid in @(@(), @(-1), @([double]::NaN), @([double]::PositiveInfinity))) {
                $rejected = $false
                try { Get-PerformanceMetricSummary $invalid | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Invalid performance metric was accepted.' }
            }
            $document = [pscustomobject]@{
                SchemaVersion = '1'; CaptureState = 'stopped'
                Outcome = @{ Succeeded = $true; Kind = 'workload-complete' }
                Quality = @{ IsValid = $true; OriginalIdentityUnchanged = $true; ParticipantCountMatches = $true }
                SampleCount = 3; DroppedSampleCount = 0; NonMonotonicTimestampCount = 0; InvalidTimestampCount = 0
                MeasuredDurationMilliseconds = 15000; WarmupMilliseconds = 0; MeasurementWindowMilliseconds = 60000
                ServerIdentity = @{ GameMode = 'SinglePlayer'; TickRateHz = 20; ProtocolSchemaVersion = '0.2.0'; ContentManifestHash = 'content'; RulesetIdentityHash = 'rules' }
                Configuration = @{ ParticipantCount = 1; ProtocolSchemaVersion = '0.2.0'; ContentManifestHash = 'content'; RulesetIdentityHash = 'rules'; BuildConfiguration = 'Debug'; FrameCap = 60; Headless = $true; EngineVersion = '4.7.2'; Viewport = '1280x720'; Runtime = '.NET 8'; OperatingSystem = 'Windows'; Architecture = 'X64'; DisplayServer = 'headless' }
                StartupProcess = @{ WorkingSetBytes = 100; PeakWorkingSetBytes = 120; LastGcHeapSizeBytes = 20; TotalProcessorTimeMilliseconds = 10 }
                FinalProcess = @{ WorkingSetBytes = 110; PeakWorkingSetBytes = 130; LastGcHeapSizeBytes = 30; TotalProcessorTimeMilliseconds = 20 }
                Samples = @(
                    @{ ProcessCallbackMilliseconds = 1; CallbackIntervalMilliseconds = $null },
                    @{ ProcessCallbackMilliseconds = 2; CallbackIntervalMilliseconds = 7500 },
                    @{ ProcessCallbackMilliseconds = 3; CallbackIntervalMilliseconds = 7500 })
            }
            $serialized = $document | ConvertTo-Json -Depth 10
            $summary = Assert-GodotPerformanceEvidence $document 1 'SinglePlayer' 'content' 'rules' $true
            if ($summary.ProcessCallback.Count -ne 3 -or $summary.CallbackInterval.Count -ne 2 -or
                $summary.ProcessCallback.P50Milliseconds -ne 2 -or $summary.CallbackInterval.P50Milliseconds -ne 7500) {
                throw 'Callback cost and callback interval were conflated.'
            }
            $mutations = @(
                { param($e) $e.Outcome.Succeeded = $false },
                { param($e) $e.Quality.OriginalIdentityUnchanged = $false },
                { param($e) $e.Quality.ParticipantCountMatches = $false },
                { param($e) $e.DroppedSampleCount = 1 },
                { param($e) $e.InvalidTimestampCount = 1 },
                { param($e) $e.NonMonotonicTimestampCount = 1 },
                { param($e) $e.Samples = @() },
                { param($e) $e.MeasuredDurationMilliseconds = 14999 },
                { param($e) $e.MeasuredDurationMilliseconds = [double]::NaN },
                { param($e) $e.Configuration.ParticipantCount = 4 },
                { param($e) $e.Configuration.Headless = $false },
                { param($e) $e.Configuration.BuildConfiguration = 'Release' },
                { param($e) $e.ServerIdentity.ContentManifestHash = 'other' },
                { param($e) $e.StartupProcess = $null },
                { param($e) $e.FinalProcess.WorkingSetBytes = -1 },
                { param($e) $e.FinalProcess.LastGcHeapSizeBytes = -1 },
                { param($e) $e.FinalProcess | Add-Member -NotePropertyName ManagedHeapBytes -NotePropertyValue -198408 },
                { param($e) $e.FinalProcess.TotalProcessorTimeMilliseconds = 0 },
                { param($e) $e.FinalProcess.PeakWorkingSetBytes = 1 },
                { param($e) $e.Configuration | Add-Member -NotePropertyName ResumeToken -NotePropertyValue 'private' },
                { param($e) $e.ServerIdentity | Add-Member -NotePropertyName SessionId -NotePropertyValue 'private' },
                { param($e) $e.Samples[0].CallbackIntervalMilliseconds = 999 },
                { param($e) $e.Samples[1].CallbackIntervalMilliseconds = -1 },
                { param($e) $e.Samples[1].ProcessCallbackMilliseconds = [double]::PositiveInfinity })
            foreach ($mutation in $mutations) {
                $changed = $serialized | ConvertFrom-Json
                & $mutation $changed
                $rejected = $false
                try { Assert-GodotPerformanceEvidence $changed 1 'SinglePlayer' 'content' 'rules' $true | Out-Null }
                catch { $rejected = $true }
                if (-not $rejected) { throw 'Invalid frame evidence was accepted as a baseline.' }
            }
            Write-Output 'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void NativeResumeAcceptanceRequiresAnExplicitMarkedProfileAndPreservesItsFiles()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-NativeProfileAncestry", "Open-NativeProfileLock", "Get-NativeAcceptanceLayout"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-NativeProfileTest-' + [guid]::NewGuid().ToString('N'))
            $ancestorLink = Join-Path $work 'linked-parent'
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'An invalid native acceptance profile was accepted.' }
            }
            try {
                $fresh = Get-NativeAcceptanceLayout $work $false
                if ($fresh.ProfileRoot -ne $work -or $fresh.CaptureRoot -ne $work -or $fresh.Resume) {
                    throw 'Fresh native profile layout changed.'
                }
                if (Test-Path -LiteralPath $work) { throw 'Layout validation created the profile.' }
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                New-Item -ItemType Directory -Path $work | Out-Null
                Expect-Rejected { Get-NativeAcceptanceLayout $work $false }
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                $save = Join-Path $work 'save'
                $nativeSave = Join-Path $work 'native-save'
                $config = Join-Path $work 'native-config'
                New-Item -ItemType Directory -Path $save, $nativeSave, $config | Out-Null
                $markerPath = Join-Path $work 'native-acceptance-profile.json'
                $bookmarkPath = Join-Path $work 'resume-token.txt'
                $checkpointPath = Join-Path $save 'test.session.json'
                $configPath = Join-Path $config 'diablo.ini'
                $firstLogPath = Join-Path $work 'native.log'
                $marker = @{ Kind = 'GodevilutionNativeAcceptance'; Version = 2; ProfileRoot = $work;
                    ContentProfile = 'godevilution.native-start@0.1.0' }
                [IO.File]::WriteAllText($markerPath, ($marker | ConvertTo-Json))
                [IO.File]::WriteAllText($configPath, '[Graphics]')
                [IO.File]::WriteAllText($bookmarkPath, 'synthetic-private-bookmark')
                [IO.File]::WriteAllText($firstLogPath, 'retained first run')
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                [IO.File]::WriteAllText($checkpointPath, '{}')
                $before = @{}
                foreach ($path in @($markerPath, $configPath, $bookmarkPath, $checkpointPath, $firstLogPath)) {
                    $before[$path] = [IO.File]::ReadAllText($path)
                }
                $resumed = Get-NativeAcceptanceLayout $work $true
                $again = Get-NativeAcceptanceLayout $work $true
                if (-not $resumed.Resume -or $resumed.ProfileRoot -ne $work -or
                    (Split-Path $resumed.CaptureRoot -Parent) -ne $work -or
                    $resumed.CaptureRoot -eq $again.CaptureRoot -or
                    (Test-Path -LiteralPath $resumed.CaptureRoot)) {
                    throw 'Resume did not retain the profile and select a unique, uncreated log directory.'
                }
                foreach ($path in $before.Keys) {
                    if ([IO.File]::ReadAllText($path) -cne $before[$path]) { throw 'Profile validation rewrote existing files.' }
                }
                $profileLock = Open-NativeProfileLock $work
                try { Expect-Rejected { Open-NativeProfileLock $work } }
                finally { $profileLock.Dispose() }
                $profileLock = Open-NativeProfileLock $work
                $profileLock.Dispose()
                New-Item -ItemType Junction -Path $ancestorLink -Target (Split-Path $work -Parent) | Out-Null
                $aliasedRoot = Join-Path $ancestorLink (Split-Path $work -Leaf)
                $aliasedMarker = $marker.Clone()
                $aliasedMarker.ProfileRoot = $aliasedRoot
                [IO.File]::WriteAllText($markerPath, ($aliasedMarker | ConvertTo-Json))
                Expect-Rejected { Get-NativeAcceptanceLayout $aliasedRoot $true }
                Expect-Rejected { Get-NativeAcceptanceLayout (Join-Path $ancestorLink 'uncreated-profile') $false }
                [IO.Directory]::Delete($ancestorLink, $false)
                [IO.File]::WriteAllText($markerPath, ($marker | ConvertTo-Json))
                foreach ($field in @('Kind', 'Version', 'ProfileRoot', 'ContentProfile')) {
                    $changed = $marker.Clone()
                    $changed[$field] = if ($field -eq 'Version') { 1 } else { 'wrong' }
                    [IO.File]::WriteAllText($markerPath, ($changed | ConvertTo-Json))
                    Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                }
                [IO.File]::WriteAllText($markerPath, 'not JSON')
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                [IO.File]::WriteAllText($markerPath, ($marker | ConvertTo-Json))
                [IO.File]::WriteAllText($bookmarkPath, '')
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                [IO.File]::WriteAllText($bookmarkPath, " `r`n `t")
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                foreach ($badBookmark in @("`nnonempty-later-line", "token`r`n", "token`nsecond-token", ' token')) {
                    [IO.File]::WriteAllText($bookmarkPath, $badBookmark)
                    Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                }
                [IO.File]::WriteAllText($bookmarkPath, "synthetic-private-bookmark`n")
                Get-NativeAcceptanceLayout $work $true | Out-Null
                [IO.File]::WriteAllText($bookmarkPath, 'synthetic-private-bookmark')
                [IO.File]::WriteAllText($checkpointPath, '')
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                [IO.File]::WriteAllText($checkpointPath, '{}')
                [IO.File]::Delete($bookmarkPath)
                New-Item -ItemType Directory -Path $bookmarkPath | Out-Null
                Expect-Rejected { Get-NativeAcceptanceLayout $work $true }
                Write-Output 'PASS'
            } finally {
                if (Test-Path -LiteralPath $ancestorLink) { [IO.Directory]::Delete($ancestorLink, $false) }
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ((Split-Path $resolvedWork -Leaf) -like 'Godevilution-NativeProfileTest-*' -and
                    (Split-Path $resolvedWork -Parent) -eq ([IO.Path]::GetTempPath().TrimEnd('\'))) {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "run-authoritative-client.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void NativeProcessOutcomeRetainsTerminalEvidenceAfterOwnedChildCleanup()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Write-NativeProcessOutcome"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-NativeOutcomeTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $child = $null
            try {
                foreach ($command in @('exit 0', 'exit 7', 'Start-Sleep -Seconds 30')) {
                    $start = [Diagnostics.ProcessStartInfo]::new()
                    $start.FileName = (Get-Command powershell.exe -CommandType Application).Source
                    $start.Arguments = '-NoProfile -NonInteractive -Command "' + $command + '"'
                    $start.UseShellExecute = $false
                    $start.CreateNoWindow = $true
                    $child = [Diagnostics.Process]::Start($start)
                    $path = Join-Path $work 'processes.json'
                    $evidence = [ordered]@{ clientPid = $child.Id; clientEndedUtc = $null; clientExitCode = $null }
                    if ($command -like 'Start-Sleep*') {
                        Write-NativeProcessOutcome $child $evidence $path
                        if ($null -ne $evidence.clientEndedUtc) { throw 'A live child was recorded as terminal.' }
                        $child.Kill()
                    }
                    if (-not $child.WaitForExit(10000)) { throw 'Harmless owned child did not exit.' }
                    Write-NativeProcessOutcome $child $evidence $path
                    $record = [IO.File]::ReadAllText($path) | ConvertFrom-Json
                    if ($null -eq $record.clientEndedUtc -or $record.clientPid -ne $child.Id -or
                        $record.clientExitCode -ne $child.ExitCode -or
                        ($command -eq 'exit 7' -and $record.clientExitCode -ne 7)) {
                        throw 'Terminal native lifecycle evidence was lost.'
                    }
                    $ended = $evidence.clientEndedUtc
                    Write-NativeProcessOutcome $child $evidence $path
                    if ($evidence.clientEndedUtc -ne $ended) { throw 'Finalization changed the recorded terminal time.' }
                    $child.Dispose()
                    $child = $null
                }
                Write-Output 'PASS'
            } finally {
                if ($null -ne $child) {
                    if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit() }
                    $child.Dispose()
                }
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ((Split-Path $resolvedWork -Leaf) -like 'Godevilution-NativeOutcomeTest-*' -and
                    (Split-Path $resolvedWork -Parent) -eq ([IO.Path]::GetTempPath().TrimEnd('\'))) {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "run-authoritative-client.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    private static string BuildPowerShellHarness(
        string[] functionNames,
        string body,
        string relativeLauncherPath = "godot/run-local-client.ps1")
    {
        var launcherPath = Path.Combine(FindRepositoryRoot(), relativeLauncherPath);
        var lines = new List<string>
        {
            "$ErrorActionPreference = 'Stop'",
            $"$launcherPath = {QuotePowerShell(launcherPath)}",
            "$launcherText = [IO.File]::ReadAllText($launcherPath)",
            "$tokens = $null",
            "$parseErrors = $null",
            "$launcherAst = [System.Management.Automation.Language.Parser]::ParseInput($launcherText, [ref]$tokens, [ref]$parseErrors)",
            "if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }",
        };
        foreach (var functionName in functionNames)
        {
            lines.Add($"$functionName = {QuotePowerShell(functionName)}");
            lines.Add("$functionAst = $launcherAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName }, $true) | Select-Object -First 1");
            lines.Add("if ($null -eq $functionAst) { throw \"Could not extract function $functionName from launcher.\" }");
            lines.Add(". ([scriptblock]::Create($functionAst.Extent.Text))");
        }
        lines.Add(body.Trim());
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string RunPowerShell(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"godevilution-launcher-test-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start PowerShell.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                Assert.Fail("PowerShell behavioral harness timed out.");
            }
            var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            Assert.True(process.ExitCode == 0, $"PowerShell behavioral harness failed ({process.ExitCode}):{Environment.NewLine}{output}");
            return output;
        }
        finally
        {
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
        foreach (var start in searchRoots)
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}
