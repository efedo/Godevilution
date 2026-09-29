[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$previousDirectory = $env:GODEVILUTION_PERFORMANCE_DIRECTORY
$evidenceDirectory = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Host-Performance-' + [guid]::NewGuid().ToString('N'))
try {
    $env:GODEVILUTION_PERFORMANCE_DIRECTORY = $evidenceDirectory
    & dotnet run --project (Join-Path $repositoryRoot 'godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj') --configuration Release -- --filter-method Devilution.Client.Protocol.Tests.LiveAuthoritativeServerTests.MeasureStandaloneHostBaseline --explicit only --no-ansi
    if ($LASTEXITCODE -ne 0) { throw 'Host performance workload failed; partial evidence is not a baseline.' }
    Write-Host 'PASS: measured one/four-participant standalone host workloads. This is not a release performance gate.'
} finally {
    [Environment]::SetEnvironmentVariable('GODEVILUTION_PERFORMANCE_DIRECTORY', $previousDirectory, 'Process')
    Write-Host "Performance evidence (private saves included): $evidenceDirectory"
}
