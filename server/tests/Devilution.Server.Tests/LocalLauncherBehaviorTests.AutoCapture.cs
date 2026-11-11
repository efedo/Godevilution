using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{
    [Fact]
    public void TownCaptureRejectsHeadlessAndAcceleratedOrNonCaptureLaunchesBeforeStartingProcesses()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness([], """
            $guard = $launcherAst.FindAll({ param($node)
                $node -is [System.Management.Automation.Language.IfStatementAst] -and
                $node.Extent.Text.Contains("throw 'TownCapture requires") }, $true) | Select-Object -First 1
            if ($null -eq $guard) { throw 'Town capture launch guard is missing.' }
            $AcceptancePhase = 'TownCapture'; $Automated = $true; $Headless = $false; $TickRateHz = 20
            $CathedralNativeTownProfileDirectory = 'profile'; $env:GODEVILUTION_CLIENT_FRAME_CAPTURE = 'fresh-output'
            $env:GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT = $null
            . ([scriptblock]::Create($guard.Extent.Text))
            foreach ($invalid in @('headless', 'accelerated', 'manual', 'missing-output', 'performance')) {
                $Headless = $invalid -eq 'headless'; $TickRateHz = if ($invalid -eq 'accelerated') { 200 } else { 20 }
                $Automated = $invalid -ne 'manual'
                $env:GODEVILUTION_CLIENT_FRAME_CAPTURE = if ($invalid -eq 'missing-output') { $null } else { 'fresh-output' }
                $env:GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT = if ($invalid -eq 'performance') { 'other-output' } else { $null }
                try { . ([scriptblock]::Create($guard.Extent.Text)); throw "Accepted $invalid." }
                catch { if ($_.Exception.Message -eq "Accepted $invalid.") { throw } }
            }
            'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void NativeTownProfileAdmitsTheWindowCapturePhaseAndStillRejectsOtherUnrelatedPhases()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness([], """
            $guard = $launcherAst.FindAll({ param($node)
                $node -is [System.Management.Automation.Language.IfStatementAst] -and
                $node.Extent.Text.Contains("throw 'Class-bound native profiles require") }, $true) | Select-Object -First 1
            if ($null -eq $guard) { throw 'Native profile guard is missing.' }
            $cathedralNativeTown = $true; $cathedralClassNative = $false
            $nativeClassPhase = $false; $nativeMultiplayerPhase = $false; $SessionMenuAcceptance = $false
            $PersistenceMode = 'cohort'; $CharacterClassId = -1; $AcceptancePhase = 'TownCapture'
            . ([scriptblock]::Create($guard.Extent.Text))
            $AcceptancePhase = 'TownLoop'
            try { . ([scriptblock]::Create($guard.Extent.Text)); throw 'Accepted an unrelated phase.' }
            catch { if ($_.Exception.Message -eq 'Accepted an unrelated phase.') { throw } }
            'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void AutoCaptureUsesIsolatedNormalWindowedTwentyHzLaunchesInEitherBuildConfiguration()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Get-AutoCaptureLaunchArguments"], """
            foreach ($configuration in @('Debug', 'Release')) {
                foreach ($short in @($false, $true)) {
                    $launch = Get-AutoCaptureLaunchArguments 'profile' $configuration $short
                    if ($launch.Headless -or $launch.TickRateHz -ne 20 -or -not $launch.Automated -or
                        $launch.AcceptancePhase -ne 'TownCapture' -or $launch.PersistenceMode -ne 'cohort' -or
                        [IO.Path]::GetDirectoryName($launch.SessionDirectory) -ne [IO.Path]::GetFullPath('profile/sessions') -or
                        $launch.ClientBuildConfiguration -ne $configuration -or
                        $launch.TownCaptureSmoke -ne $short) { throw 'Auto capture changed the normal windowed launch policy.' }
                }
            }
            'PASS'
            """, "tools/run-auto-capture.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void AutoCaptureCreatesFreshProfileSessionChildrenAndKeepsTheRequestedEvidenceOutput()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Get-AutoCaptureLaunchArguments"], """
            $profile = Join-Path ([IO.Path]::GetTempPath()) 'Godevilution-AutoCapture-Profile'
            $output = Join-Path ([IO.Path]::GetTempPath()) 'Godevilution-AutoCapture-Evidence'
            $env:GODEVILUTION_CLIENT_FRAME_CAPTURE = Join-Path $output 'release'
            $expectedParent = [IO.Path]::GetFullPath((Join-Path $profile 'sessions'))
            $first = Get-AutoCaptureLaunchArguments $profile 'Release' $true
            $second = Get-AutoCaptureLaunchArguments $profile 'Release' $true
            foreach ($launch in @($first, $second)) {
                $session = [IO.Path]::GetFullPath($launch.SessionDirectory)
                if ([IO.Path]::GetDirectoryName($session) -ne $expectedParent -or
                    [IO.Path]::GetFileName($session) -notmatch '^auto-town-release-[a-f0-9]{32}$') {
                    throw "Capture session is not a fresh child of the compiled profile sessions directory: $session"
                }
                if ($launch.CathedralNativeTownProfileDirectory -ne $profile) { throw 'Capture changed its compiled profile.' }
            }
            if ($first.SessionDirectory -eq $second.SessionDirectory) { throw 'Capture reused a previous session.' }
            if ($env:GODEVILUTION_CLIENT_FRAME_CAPTURE -ne (Join-Path $output 'release')) { throw 'Capture moved the requested frame evidence.' }
            'PASS'
            """, "tools/run-auto-capture.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void AutoCaptureComparisonPrintsAllFourPhasesAndTheirActualFramePercentiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Get-AutoCaptureComparisonRow"], """
            $phases = @('idle', 'walking', 'unreachable-clicks', 'resident-clicks') | ForEach-Object {
                [pscustomobject]@{ Phase = $_; FrameIntervalCount = 100; MedianFrameMilliseconds = 16.66; P99FrameMilliseconds = 30.12
                    MedianProcessCpuMillisecondsPerSecond = 100; MedianManagedAllocationBytesPerSecond = 250000
                    WorstSecond = [pscustomobject]@{ StartSeconds = 4; FramesPerSecond = 30 } }
            }
            $rows = @(Get-AutoCaptureComparisonRow 'Release' ([pscustomobject]@{ PhaseSummaries = $phases }))
            if ($rows.Count -ne 4 -or $rows[0].MedianMs -ne 16.66 -or $rows[1].P99Ms -ne 30.12 -or
                $rows[1].WorstFPS -ne 30 -or $rows[1].AllocationMBPerSecond -ne .25) { throw 'Invalid phase comparison.' }
            try { Get-AutoCaptureComparisonRow 'Debug' ([pscustomobject]@{ PhaseSummaries = @() }); throw 'Accepted missing phases.' }
            catch { if ($_.Exception.Message -eq 'Accepted missing phases.') { throw } }
            'PASS'
            """, "tools/run-auto-capture.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }
}
