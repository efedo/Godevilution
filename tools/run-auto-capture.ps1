[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [ValidateSet('Debug', 'Release')] [string[]]$ClientBuildConfiguration = @('Debug', 'Release'),
    # Reuse an existing setup; fresh sessions use its sessions directory, frame evidence uses OutputDirectory.
    [string]$SetupStatePath,
    [string]$GodotExecutable,
    # Short visible validation, not a substitute for the full 45 s idle / 60 s walking comparison.
    [switch]$Smoke
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Get-AutoCaptureLaunchArguments([string]$Profile, [string]$Configuration, [bool]$ShortRun) {
    $session = Join-Path ([IO.Path]::GetFullPath($Profile)) (
        'sessions/auto-town-' + $Configuration.ToLowerInvariant() + '-' + [guid]::NewGuid().ToString('N'))
    @{
        Automated = $true; AcceptancePhase = 'TownCapture'; PersistenceMode = 'cohort'
        CathedralNativeTownProfileDirectory = $Profile; SessionDirectory = $session
        ClientBuildConfiguration = $Configuration; TickRateHz = 20; QuitAfterSeconds = 360
        TownCaptureSmoke = $ShortRun
    }
}

function Get-AutoCaptureComparisonRow([string]$Configuration, $Evidence) {
    foreach ($name in @('idle', 'walking', 'unreachable-clicks', 'resident-clicks')) {
        $phase = @($Evidence.PhaseSummaries | Where-Object Phase -eq $name)
        if ($phase.Count -ne 1 -or $phase[0].FrameIntervalCount -lt 1) { throw "Missing measured phase: $Configuration / $name" }
        [pscustomobject][ordered]@{
            Build = $Configuration; Phase = $name
            MedianMs = [Math]::Round($phase[0].MedianFrameMilliseconds, 2)
            P99Ms = [Math]::Round($phase[0].P99FrameMilliseconds, 2)
            CpuMsPerSecond = [Math]::Round($phase[0].MedianProcessCpuMillisecondsPerSecond, 1)
            AllocationMBPerSecond = [Math]::Round($phase[0].MedianManagedAllocationBytesPerSecond / 1e6, 3)
            WorstSecond = if ($null -ne $phase[0].WorstSecond) { [Math]::Round($phase[0].WorstSecond.StartSeconds, 2) } else { $null }
            WorstFPS = if ($null -ne $phase[0].WorstSecond) { [Math]::Round($phase[0].WorstSecond.FramesPerSecond, 1) } else { $null }
        }
    }
}

if (-not $SetupStatePath) {
    $SetupStatePath = Join-Path $repositoryRoot '.local/godot/native/current.json'
    if (-not (Test-Path -LiteralPath $SetupStatePath -PathType Leaf)) {
        & (Join-Path $repositoryRoot 'run-godot-client.ps1') -SetupOnly
        if ($LASTEXITCODE -ne 0) { throw 'Native setup failed. Run run-godot-client.ps1 -SetupOnly first.' }
    }
}
$state = Get-Content -LiteralPath $SetupStatePath -Raw | ConvertFrom-Json
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (@($ClientBuildConfiguration | Select-Object -Unique).Count -ne $ClientBuildConfiguration.Count) { throw 'Build configurations must be distinct.' }
foreach ($configuration in $ClientBuildConfiguration) {
    if (Test-Path -LiteralPath (Join-Path $outputRoot $configuration.ToLowerInvariant())) { throw "Capture output must be fresh: $outputRoot / $configuration" }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$settings = @{
    GODEVILUTION_NATIVE_PLAYER_PACK = [string]$state.playerPack
    GODEVILUTION_NATIVE_SCENE_PACK = [string]$state.scenePack
    GODEVILUTION_NATIVE_TOWNER_PACK = [string]$state.townerPack
    GODEVILUTION_NATIVE_OBJECT_PACK = [string]$state.objectPack
    GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT = $null
    GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS = $(if ($Smoke) { '2' } else { '10' })
    # The scenario stops explicitly; this larger bound also covers resident approach walks.
    GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS = '330'
    GODEVILUTION_CLIENT_PERFORMANCE_MAX_SAMPLES = '4096'
    GODEVILUTION_CLIENT_FRAME_CAPTURE = $null
    MSBUILDDISABLENODEREUSE = '1'; DOTNET_CLI_USE_MSBUILD_SERVER = '0'; UseSharedCompilation = 'false'
}
$previous = @{}
foreach ($name in $settings.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$comparison = @()
try {
    foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $settings[$name]) }
    foreach ($configuration in $ClientBuildConfiguration) {
        $destination = Join-Path $outputRoot $configuration.ToLowerInvariant()
        $env:GODEVILUTION_CLIENT_FRAME_CAPTURE = $destination
        $launch = Get-AutoCaptureLaunchArguments $state.profileDirectory $configuration $Smoke.IsPresent
        if ($GodotExecutable) { $launch.GodotExecutable = $GodotExecutable }
        Write-Host "Capturing $configuration in a normal window (60 FPS + VSync); please leave its controls alone."
        & (Join-Path $repositoryRoot 'godot-client/run-local-client.ps1') @launch
        if ($LASTEXITCODE -ne 0) { throw "$configuration Town capture failed with exit $LASTEXITCODE." }
        $evidence = Get-Content -LiteralPath (Join-Path $destination 'client-frame-evidence.json') -Raw | ConvertFrom-Json
        if (-not $evidence.Outcome.Succeeded -or -not $evidence.Quality.IsValid -or $evidence.Configuration.Headless -or
            $evidence.Configuration.FrameCap -ne 60 -or -not $evidence.Configuration.VsyncEnabled) {
            throw "$configuration did not produce valid normal-window frame evidence."
        }
        $comparison += @(Get-AutoCaptureComparisonRow $configuration $evidence)
    }
} finally {
    foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
$comparison | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'comparison.json') -Encoding UTF8
$comparison | Format-Table -AutoSize | Out-Host
Write-Host "Evidence: $outputRoot"
