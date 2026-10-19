[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ProfileDirectory,
    [Parameter(Mandatory)] [string]$SourceSessionDirectory,
    [Parameter(Mandatory)] [string]$EntryBaselinePath,
    [string]$GodotExecutable,
    [ValidateSet('Debug', 'Release')] [string]$ServerBuildConfiguration = 'Release',
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 3600
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$profileRoot = (Resolve-Path -LiteralPath $ProfileDirectory).ProviderPath
$sourceRoot = (Resolve-Path -LiteralPath $SourceSessionDirectory).ProviderPath
$baseline = (Resolve-Path -LiteralPath $EntryBaselinePath).ProviderPath
$profileMetadata = Get-Content -LiteralPath (Join-Path $profileRoot 'cathedral-native-depth-progression-playtest.json') -Raw | ConvertFrom-Json
if ($profileMetadata.formatVersion -ne 3 -or
    $profileMetadata.adapterId -cne 'devilution.diablo.cathedral-native-depth-progression.v1' -or
    $profileMetadata.nativeDepth -ne 4 -or $profileMetadata.nativePopulationClaim -ne $false) {
    throw 'The continuation requires the explicit opt-in native L1-L4 depth profile.'
}
$baselineRecord = Get-Content -LiteralPath $baseline -Raw | ConvertFrom-Json
if ($baselineRecord.FormatVersion -ne 1 -or $baselineRecord.LevelId -ne 3 -or
    [string]$baselineRecord.InputCheckpointSha256 -cnotmatch '^[0-9A-Fa-f]{64}$') {
    throw 'The supplied baseline is not the reviewed R18 L3 entry snapshot.'
}
$sourceSave = Join-Path $sourceRoot 'save'
$sourceCohort = Join-Path $sourceSave 'authoritative-cohort.json'
if (-not (Test-Path -LiteralPath $sourceCohort -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $sourceSave 'world-seed.txt') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $sourceSave '.authoritative-cohort.committed') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $sourceRoot 'resume-token.txt') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $sourceRoot 'cathedral-native-depth-progression-session.json') -PathType Leaf)) {
    throw 'The source session is missing its saved cohort or native profile marker.'
}
if ((Get-Item -LiteralPath (Join-Path $sourceSave '.authoritative-cohort.committed')).Length -ne 0) {
    throw 'The source session committed marker is not the expected empty marker.'
}
$sourceHash = (Get-FileHash -LiteralPath $sourceCohort -Algorithm SHA256).Hash.ToUpperInvariant()
if ($sourceHash -cne ([string]$baselineRecord.InputCheckpointSha256).ToUpperInvariant()) {
    throw 'The untouched source session does not match the exact copied R18 entry baseline.'
}

function Copy-CoreContinuationSession([string]$From, [string]$Label) {
    $target = Join-Path $profileRoot ('sessions/acceptance-' + [guid]::NewGuid().ToString('N'))
    $targetSave = Join-Path $target 'save'
    New-Item -ItemType Directory -Path $targetSave -Force | Out-Null
    foreach ($name in @('authoritative-cohort.json', 'world-seed.txt', '.authoritative-cohort.committed')) {
        Copy-Item -LiteralPath (Join-Path $From ('save/' + $name)) -Destination $targetSave -ErrorAction Stop
    }
    foreach ($name in @('resume-token.txt', 'cathedral-native-depth-progression-session.json')) {
        Copy-Item -LiteralPath (Join-Path $From $name) -Destination $target -ErrorAction Stop
    }
    Write-Host "Native depth continuation $Label session: $target"
    return $target
}

function Read-ContinuationProcessEvidence([string]$Session) {
    $path = Join-Path $Session 'cathedralnativedepthprogressionresume-processes.json'
    $processes = Get-Content -LiteralPath $path -Raw -ErrorAction Stop | ConvertFrom-Json
    if ($processes.PersistenceMode -cne 'cohort' -or $processes.HostPersistence -cne 'complete cohort' -or
        $null -eq $processes.HostProcessId -or [string]::IsNullOrWhiteSpace($processes.HostStartedUtc) -or
        @($processes.Clients).Count -ne 1 -or $null -eq $processes.Clients[0].ProcessId -or
        [string]::IsNullOrWhiteSpace($processes.Clients[0].StartedUtc) -or
        [string]$processes.ContentHash -cnotmatch '^[0-9a-f]{64}$' -or
        [string]$processes.RulesetHash -cnotmatch '^[0-9a-f]{64}$') {
        throw 'The continuation did not emit complete cohort host/client process evidence.'
    }
    return $processes
}

function Assert-ContinuationProcessesStopped($Processes) {
    $owned = @([pscustomobject]@{ ProcessId = $Processes.HostProcessId; StartedUtc = $Processes.HostStartedUtc }) + @($Processes.Clients)
    foreach ($identity in $owned) {
        try { $ownedProcess = [Diagnostics.Process]::GetProcessById([int]$identity.ProcessId) }
        catch [ArgumentException] { continue }
        try {
            try { $startedUtc = $ownedProcess.StartTime.ToUniversalTime() }
            catch [InvalidOperationException] { continue }
            if ($startedUtc -eq ([DateTimeOffset]$identity.StartedUtc).UtcDateTime) {
                throw "An owned continuation process is still running: $($identity.ProcessId)."
            }
        } finally { $ownedProcess.Dispose() }
    }
}

function Write-ContinuationInputProof([string]$Session, [string]$ExpectedHash) {
    $canonicalPath = Join-Path $Session 'save/authoritative-cohort.json'
    $actualHash = (Get-FileHash -LiteralPath $canonicalPath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actualHash -cne $ExpectedHash.ToUpperInvariant()) {
        throw 'The isolated continuation copy does not match its prelaunch canonical checkpoint hash.'
    }
    $proof = @{ FormatVersion = 1; InputCheckpointSha256 = $actualHash } | ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText((Join-Path $Session 'native-depth-entry-input-proof.json'), $proof,
        [System.Text.UTF8Encoding]::new($false))
}

$launcher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
$entrySession = Copy-CoreContinuationSession $sourceRoot 'entry'
$arguments = @{
    CathedralNativeDepthProgressionProfileDirectory = $profileRoot
    NativeDepthEntryBaselinePath = $baseline
    PersistenceMode = 'cohort'
    Headless = $true
    Automated = $true
    CommandTimingTrace = $true
    SessionDirectory = $entrySession
    QuitAfterSeconds = $QuitAfterSeconds
    ServerBuildConfiguration = $ServerBuildConfiguration
    AcceptancePhase = 'CathedralNativeDepthProgressionResume'
}
if ($GodotExecutable) { $arguments.GodotExecutable = $GodotExecutable }
Write-ContinuationInputProof $entrySession ([string]$baselineRecord.InputCheckpointSha256)
& $launcher @arguments
if ($LASTEXITCODE -ne 0) { throw "The exact-entry L3/L4 continuation failed with exit $LASTEXITCODE." }
$entryProcesses = Read-ContinuationProcessEvidence $entrySession
Assert-ContinuationProcessesStopped $entryProcesses

$entryEvidencePath = Join-Path $entrySession 'native-depth-entry-continuation.json'
$townBaselinePath = Join-Path $entrySession 'native-depth-entry-continuation-town.json'
$entryEvidence = Get-Content -LiteralPath $entryEvidencePath -Raw -ErrorAction Stop | ConvertFrom-Json
$townScene = Get-Content -LiteralPath $townBaselinePath -Raw -ErrorAction Stop | ConvertFrom-Json
if ($entryEvidence.CheckpointKind -cne 'native-depth-entry-continuation-town-v1' -or
    $entryEvidence.FullRouteClaimed -ne $false -or
    [string]::Join(',', [string[]]@($entryEvidence.Combat | ForEach-Object { $_.LevelId })) -cne '3,4' -or
    [string]::Join(',', [string[]]@($entryEvidence.Floors | ForEach-Object { $_.LevelId })) -cne '1,2,3,4' -or
    @($entryEvidence.AcceptedWorldClickPaths).Count -lt 1 -or
    $townScene.CheckpointKind -cne 'native-depth-entry-continuation-town-v1' -or
    $townScene.InputCheckpointSha256 -cne '' -or
    $townScene.LevelId -ne 0) {
    throw 'The entry continuation did not retain its L3/L4 combat and exact Town checkpoint evidence.'
}

$stoppedTownHash = (Get-FileHash -LiteralPath (Join-Path $entrySession 'save/authoritative-cohort.json') -Algorithm SHA256).Hash.ToUpperInvariant()
if ($stoppedTownHash -cnotmatch '^[0-9A-F]{64}$') { throw 'The stopped Town checkpoint SHA-256 is malformed.' }
$stoppedTownCohort = Get-Content -LiteralPath (Join-Path $entrySession 'save/authoritative-cohort.json') -Raw | ConvertFrom-Json
$townBaselineRecord = Get-Content -LiteralPath $townBaselinePath -Raw | ConvertFrom-Json
$townBaselineRecord.InputCheckpointSha256 = $stoppedTownHash
$townBaselineRecord.Tick = [UInt64]$stoppedTownCohort.Tick
$townResumeBaselinePath = Join-Path $entrySession 'native-depth-entry-continuation-town-baseline.json'
[System.IO.File]::WriteAllText($townResumeBaselinePath, (ConvertTo-Json -InputObject $townBaselineRecord -Depth 16 -Compress),
    [System.Text.UTF8Encoding]::new($false))
$townBaseline = Get-Content -LiteralPath $townResumeBaselinePath -Raw | ConvertFrom-Json
if ($townBaseline.CheckpointKind -cne 'native-depth-entry-continuation-town-v1' -or
    $townBaseline.InputCheckpointSha256 -cne $stoppedTownHash -or
    $townBaseline.Tick -ne $stoppedTownCohort.Tick -or $townBaseline.LevelId -ne 0) {
    throw 'The post-stop Town resume baseline did not bind its exact canonical SHA and retained state.'
}

$townSession = Copy-CoreContinuationSession $entrySession 'fresh-Town-resume'
$arguments.SessionDirectory = $townSession
Write-ContinuationInputProof $townSession $stoppedTownHash
$arguments.NativeDepthEntryBaselinePath = $townResumeBaselinePath
& $launcher @arguments
if ($LASTEXITCODE -ne 0) { throw "The fresh-host Town continuation resume failed with exit $LASTEXITCODE." }
$resumeProcesses = Read-ContinuationProcessEvidence $townSession
Assert-ContinuationProcessesStopped $resumeProcesses

$resumePath = Join-Path $townSession 'native-depth-entry-continuation-resume.json'
$resume = Get-Content -LiteralPath $resumePath -Raw -ErrorAction Stop | ConvertFrom-Json
if ($resume.ContinuationComplete -ne $true -or $resume.FullRouteClaimed -ne $false -or
    [string]::Join(',', [string[]]@($resume.RevisitedLevels)) -cne '1,2,3,4' -or
    $resume.InputCheckpointSha256 -cne $townBaseline.InputCheckpointSha256 -or
    $resume.LevelId -ne 0 -or $resume.EntityId -ne $townBaseline.EntityId) {
    throw 'The fresh-host continuation did not restore Town, revisit all four retained floors and return alive.'
}
if (($entryProcesses.HostProcessId -eq $resumeProcesses.HostProcessId -and
        $entryProcesses.HostStartedUtc -ceq $resumeProcesses.HostStartedUtc) -or
    ($entryProcesses.Clients[0].ProcessId -eq $resumeProcesses.Clients[0].ProcessId -and
        $entryProcesses.Clients[0].StartedUtc -ceq $resumeProcesses.Clients[0].StartedUtc) -or
    $entryProcesses.ContentHash -cne $resumeProcesses.ContentHash -or
    $entryProcesses.RulesetHash -cne $resumeProcesses.RulesetHash) {
    throw 'The Town continuation did not use a fresh matching host/client process pair.'
}

Write-Output "Native L3/L4 entry continuation and fresh Town restore passed. EntrySession=$entrySession; ResumeSession=$townSession"
