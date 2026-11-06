using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{

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
}
