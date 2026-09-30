[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$evidenceDirectory = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Client-Performance-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $evidenceDirectory | Out-Null
try {
    # Each profile builds current sources and owns one host plus its actual clients.
    # This controlled 60-FPS workload measures callbacks, not GPU frame completion.
    & $launcher -SessionDirectory (Join-Path $evidenceDirectory 'single-player') -AcceptancePhase Performance -PerformanceParticipants 1 -QuitAfterSeconds 150
    & $launcher -SessionDirectory (Join-Path $evidenceDirectory 'four-player') -AcceptancePhase Performance -PerformanceParticipants 4 -Multiplayer -QuitAfterSeconds 150
    Write-Host 'PASS: rendered one/four-client Godot workloads with settled authoritative movement. These are measured baselines, not release performance gates.'
} finally {
    # Keep private bookmarks/checkpoints and diagnostic logs; never reuse normal saves.
    Write-Host "Client performance evidence (private saves included): $evidenceDirectory"
}
