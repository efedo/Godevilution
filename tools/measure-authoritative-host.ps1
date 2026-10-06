[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not [string]::IsNullOrEmpty($env:GODEVILUTION_SERVER_ASSEMBLY)) {
    throw 'Host performance measurement requires the evaluated Release server assembly; clear the explicit assembly override and rerun.'
}
$previousDirectory = $env:GODEVILUTION_PERFORMANCE_DIRECTORY
$evidenceDirectory = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Host-Performance-' + [guid]::NewGuid().ToString('N'))
try {
    $env:GODEVILUTION_PERFORMANCE_DIRECTORY = $evidenceDirectory
    & dotnet run --project (Join-Path $repositoryRoot 'godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj') --configuration Release -- --filter-method Devilution.Client.Protocol.Tests.LiveAuthoritativeServerTests.MeasureStandaloneHostBaseline --explicit only --no-ansi
    if ($LASTEXITCODE -ne 0) { throw 'Host performance workload failed; partial evidence is not a baseline.' }
    Write-Host 'PASS: measured one/four-participant Immediate and ClockGated standalone host workloads. This is not a release performance gate.'
} finally {
    [Environment]::SetEnvironmentVariable('GODEVILUTION_PERFORMANCE_DIRECTORY', $previousDirectory, 'Process')
    Write-Host "Performance evidence (private saves included): $evidenceDirectory"
}
