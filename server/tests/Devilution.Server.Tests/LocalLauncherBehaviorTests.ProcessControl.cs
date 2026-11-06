using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{

    [Fact]
    public void GodotAutomationPolicyBoundsVisibleClientsWithoutChangingInteractiveDefaults()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(["Get-GodotClientRunPolicy"], """
            $guardEnd = $launcherText.IndexOf('$repositoryRoot = ', [StringComparison]::Ordinal)
            if ($guardEnd -le 0) { throw 'Could not isolate the launcher argument guards.' }
            $guards = [scriptblock]::Create($launcherText.Substring(0, $guardEnd) + "`r`n'GUARDS-PASSED'")
            foreach ($headless in @($false, $true)) {
                foreach ($automated in @($false, $true)) {
                    foreach ($phase in @('', 'Create')) {
                        foreach ($seconds in @(1, 27, 3600)) {
                            $policy = Get-GodotClientRunPolicy $headless $automated $phase $seconds
                            $bounded = $headless -or $automated -or $phase -ne ''
                            if ($policy.SuppressCrashDialog -ne $bounded) { throw 'Incorrect child suppression policy.' }
                            if ($bounded) {
                                if ($policy.TimeoutSeconds -ne ($seconds + 15)) { throw 'Missing parent deadline.' }
                            } elseif ($null -ne $policy.TimeoutSeconds) { throw 'Manual launch acquired a deadline.' }
                            if ($bounded -and $phase -eq '') {
                                if ($policy.SceneQuitAfterSeconds -ne $seconds) { throw 'Missing real-seconds scene timer.' }
                            } elseif ($null -ne $policy.SceneQuitAfterSeconds) { throw 'Manual/acceptance scene acquired a quit timer.' }
                        }
                    }
                }
            }
            $rejected = $false
            try { & $guards -Automated | Out-Null } catch { $rejected = $_.Exception.Message -match 'isolated -SessionDirectory' }
            if (-not $rejected) { throw 'Automation silently adopted the normal save profile.' }
            $output = @(& $guards -Automated -SessionDirectory 'explicit-not-opened' -QuitAfterSeconds 27)
            if ($output -notcontains 'GUARDS-PASSED') { throw 'Valid visible automation was refused.' }
            $output = @(& $guards)
            if ($output -notcontains 'GUARDS-PASSED') { throw 'Normal interactive defaults changed.' }
            Write-Output 'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void GodotCleanupFailureRejectsSuccessWithoutMaskingTheLaunchError()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(["Assert-LocalLauncherCleanup"], """
            Assert-LocalLauncherCleanup $false @()
            Assert-LocalLauncherCleanup $true @('owned process could not stop')
            $rejected = $false
            try { Assert-LocalLauncherCleanup $false @('owned process could not stop', 'evidence could not save') }
            catch {
                $rejected = $_.Exception.Message -match 'do not retry' -and
                    $_.Exception.Message -match 'owned process could not stop' -and
                    $_.Exception.Message -match 'evidence could not save'
            }
            if (-not $rejected) { throw 'Failed cleanup was accepted or lost its diagnostics.' }
            $observed = $null
            try {
                $primary = $null
                try { throw 'original native engine failure' }
                catch { $primary = $_; throw }
                finally { Assert-LocalLauncherCleanup ($null -ne $primary) @('owned process could not stop') }
            } catch { $observed = $_.Exception.Message }
            if ($observed -cne 'original native engine failure') { throw 'Cleanup replaced the original launch error.' }
            Write-Output 'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void GodotChildInheritsCrashDialogSuppressionWithoutChangingParentMode()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-WritableDirectory", "Write-ProcessEvidence", "Start-GodotChildProcess", "Stop-OwnedLocalProcess",
             "New-GodotProcess", "Write-GodotRedirectedOutput", "Complete-GodotProcess", "Stop-GodotProcess"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-ModeInheritanceTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $trackedChildren = [Collections.Generic.List[object]]::new()
            $originalErrorMode = $null
            try {
                Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class GodevilutionNativeErrorMode
{
    [DllImport("kernel32.dll")] public static extern uint GetErrorMode();
    [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode);
}
'@
                $originalErrorMode = [GodevilutionNativeErrorMode]::GetErrorMode()
                [void][GodevilutionNativeErrorMode]::SetErrorMode($originalErrorMode -band [uint32]4294967292)
                $parentErrorMode = [GodevilutionNativeErrorMode]::GetErrorMode()
                $GodotExecutable = Join-Path $work 'inherited-mode.exe'
                # PowerShell changes its own error mode at startup. A minimal
                # non-crashing executable observes inheritance without that reset.
                Add-Type -OutputAssembly $GodotExecutable -OutputType ConsoleApplication -TypeDefinition @'
using System;
using System.Globalization;
using System.Runtime.InteropServices;
public static class InheritedModeProbe
{
    [DllImport("kernel32.dll")] private static extern uint GetErrorMode();
    public static int Main()
    {
        Console.WriteLine(GetErrorMode().ToString(CultureInfo.InvariantCulture));
        return 0;
    }
}
'@
                $captureRoot = $work
                $processEvidenceRoot = Join-Path $work 'processes'
                New-Item -ItemType Directory -Path $processEvidenceRoot | Out-Null
                $repositoryRoot = (Get-Location).Path
                $PersistenceMode = 'legacy'
                foreach ($suppress in @($false, $true)) {
                    $stage = if ($suppress) { 'automated' } else { 'manual' }
                    $stdout = Join-Path $work ($stage + '.stdout')
                    $stderr = Join-Path $work ($stage + '.stderr')
                    $tracked = New-GodotProcess $stage '' @{} $true $suppress $stdout $stderr
                    $trackedChildren.Add($tracked)
                    Complete-GodotProcess $tracked 10000 'inherited-mode probe'
                    $childMode = [uint32]([IO.File]::ReadAllText($stdout).Trim())
                    if ($suppress -and ($childMode -band [uint32]3) -ne [uint32]3) {
                        throw "Automated child did not inherit suppression bits: $childMode"
                    }
                    if (-not $suppress -and ($childMode -band [uint32]2) -ne [uint32]0) {
                        throw "Manual child unexpectedly inherited crash-dialog suppression: $childMode"
                    }
                    if ([GodevilutionNativeErrorMode]::GetErrorMode() -ne $parentErrorMode) {
                        throw 'Child launch changed the parent error mode.'
                    }
                }
                Write-Output 'PASS'
            } finally {
                $allExited = $true
                foreach ($tracked in $trackedChildren) {
                    try { Stop-GodotProcess $tracked } catch { $allExited = $false }
                    if (-not $tracked.Process.HasExited) { $allExited = $false }
                    $tracked.Process.Dispose()
                }
                if ($null -ne $originalErrorMode) { [void][GodevilutionNativeErrorMode]::SetErrorMode($originalErrorMode) }
                $resolvedWork = [IO.Path]::GetFullPath($work)
                $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                if ($allExited -and (Split-Path $resolvedWork -Parent) -eq $tempParent -and
                    (Split-Path $resolvedWork -Leaf) -match '^Godevilution-ModeInheritanceTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force
                }
            }
""");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

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
}
