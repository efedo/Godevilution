[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
$clientCandidates = @(
    (Join-Path $repositoryRoot 'build/x64-Debug-authoritative-vcpkg/devilutionx.exe'),
    (Join-Path $repositoryRoot 'build/x64-Debug-authoritative/devilutionx.exe'),
    (Join-Path $repositoryRoot 'build/x64-Debug/devilutionx.exe'),
    (Join-Path $repositoryRoot 'build/x64-Release-authoritative-vcpkg/devilutionx.exe'),
    (Join-Path $repositoryRoot 'build/x64-Release-authoritative/devilutionx.exe')
)
$clientCandidates = @($clientCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })

if ($clientCandidates.Count -eq 0) {
    $clientBuildDirectory = @(
        (Join-Path $repositoryRoot 'build/x64-Debug-authoritative-vcpkg'),
        (Join-Path $repositoryRoot 'build/x64-Debug-authoritative'),
        (Join-Path $repositoryRoot 'build/x64-Debug')
    ) | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'CMakeCache.txt') -PathType Leaf } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($clientBuildDirectory)) {
        throw "No authoritative native client build was found under $repositoryRoot\build. Configure an authoritative CMake build first."
    }
    if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
        throw 'CMake is required to build the authoritative native client, but it was not found on PATH.'
    }
    Write-Host "Building the authoritative native client in $clientBuildDirectory ..."
    & cmake --build $clientBuildDirectory --config Debug --target devilutionx
    if ($LASTEXITCODE -ne 0) {
        throw "The authoritative native client build failed with exit code $LASTEXITCODE."
    }
    $clientCandidates = @(
        (Join-Path $repositoryRoot 'build/x64-Debug-authoritative-vcpkg/devilutionx.exe'),
        (Join-Path $repositoryRoot 'build/x64-Debug-authoritative/devilutionx.exe'),
        (Join-Path $repositoryRoot 'build/x64-Debug/devilutionx.exe')
    )
    $clientCandidates = @($clientCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
}
if ($clientCandidates.Count -eq 0) {
    throw 'The authoritative native client build completed without producing devilutionx.exe.'
}
$clientExecutable = (Resolve-Path -LiteralPath $clientCandidates[0]).Path

if (-not (Test-Path -LiteralPath $serverExecutable -PathType Leaf)) {
    Write-Host 'Building the authoritative server ...'
    & dotnet build $serverProject --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "The authoritative server build failed with exit code $LASTEXITCODE."
    }
}
if (-not (Test-Path -LiteralPath $serverExecutable -PathType Leaf)) {
    throw "The authoritative server build did not produce $serverExecutable."
}

$captureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Authoritative-' + [guid]::NewGuid().ToString('N'))
$serverOutput = Join-Path $captureRoot 'server.stdout.log'
$serverError = Join-Path $captureRoot 'server.stderr.log'
$saveRoot = Join-Path $captureRoot 'save'
$resumeTokenPath = Join-Path $captureRoot 'resume-token.txt'
$diagnosticsDirectory = Join-Path $captureRoot 'diagnostics'
New-Item -ItemType Directory -Path $captureRoot, $saveRoot, $diagnosticsDirectory | Out-Null

$serverProcess = $null
try {
    $serverArguments = @('--port', '0', '--protocol-version', '0.2.0', '--save-root', $saveRoot)
    $serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $serverOutput -RedirectStandardError $serverError -PassThru

    $port = $null
    $contentHash = $null
    $rulesetHash = $null
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        Start-Sleep -Milliseconds 250
        if ($serverProcess.HasExited) {
            throw "The authoritative server exited during startup. See $serverError"
        }
        $output = Get-Content -LiteralPath $serverOutput -Raw -ErrorAction SilentlyContinue
        if ($null -eq $output) { $output = '' }
        $portMatch = [regex]::Match($output, 'listening on [^:]+:(\d+)')
        $contentMatch = [regex]::Match($output, 'Content manifest: ([0-9a-fA-F]+)')
        $rulesetMatch = [regex]::Match($output, 'Ruleset identity: ([0-9a-fA-F]+)')
        if ($portMatch.Success) { $port = [int]$portMatch.Groups[1].Value }
        if ($contentMatch.Success) { $contentHash = $contentMatch.Groups[1].Value }
        if ($rulesetMatch.Success) { $rulesetHash = $rulesetMatch.Groups[1].Value }
        if ($null -ne $port -and -not [string]::IsNullOrWhiteSpace($contentHash) -and -not [string]::IsNullOrWhiteSpace($rulesetHash)) { break }
    }
    if ($null -eq $port -or [string]::IsNullOrWhiteSpace($contentHash) -or [string]::IsNullOrWhiteSpace($rulesetHash)) {
        throw "Timed out waiting for the authoritative server identity. See $serverOutput and $serverError"
    }

    $serverEndpoint = "127.0.0.1:$port"
    $clientArguments = @(
        '--game-mode', 'authoritative',
        '--authoritative-server', $serverEndpoint,
        '--authoritative-protocol-version', '0.2.0',
        '--authoritative-content-hash', $contentHash,
        '--authoritative-ruleset-hash', $rulesetHash,
        '--authoritative-resume-token', $resumeTokenPath,
        '--authoritative-diagnostics', $diagnosticsDirectory
    )
    Write-Host "Authoritative server listening on $serverEndpoint"
    Write-Host "Launching $clientExecutable"
    $clientProcess = Start-Process -FilePath $clientExecutable -ArgumentList $clientArguments -WorkingDirectory $repositoryRoot -WindowStyle Normal -PassThru -Wait
    exit $clientProcess.ExitCode
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        $serverProcess.WaitForExit()
    }
    Write-Host "Authoritative launch logs: $captureRoot"
}
