using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class LocalLauncherBehaviorTests
{
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
                    "New-GodotProcess",
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
                    throw "Evidence-write failure left child process $($primaryResult.ProcessId) running."
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

    private static string BuildPowerShellHarness(string[] functionNames, string body)
    {
        var launcherPath = Path.Combine(FindRepositoryRoot(), "godot", "run-local-client.ps1");
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
