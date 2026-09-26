[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
$clientBuildDirectory = @(
    (Join-Path $repositoryRoot 'build/x64-Debug-authoritative-vcpkg'),
    (Join-Path $repositoryRoot 'build/x64-Debug-authoritative'),
    (Join-Path $repositoryRoot 'build/x64-Release-authoritative-vcpkg'),
    (Join-Path $repositoryRoot 'build/x64-Release-authoritative')
) | Where-Object {
    $cache = Join-Path $_ 'CMakeCache.txt'
    (Test-Path -LiteralPath $cache -PathType Leaf) -and
        (Select-String -LiteralPath $cache -Pattern '^DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT:BOOL=ON$' -Quiet)
} | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($clientBuildDirectory)) {
    throw "No authoritative native client build was found under $repositoryRoot\build. Configure an authoritative CMake build first."
}

$vsDevCmd = Get-ChildItem -Path (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\*\*\Common7\Tools\VsDevCmd.bat') -File -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if ($null -eq $vsDevCmd) {
    throw 'A Visual Studio developer environment was not found.'
}
$cmakeCommand = Get-Command cmake.exe -ErrorAction SilentlyContinue
$cmakeExecutable = if ($null -ne $cmakeCommand) {
    $cmakeCommand.Source
} else {
    Join-Path $vsDevCmd.Directory.FullName '..\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
}
if (-not (Test-Path -LiteralPath $cmakeExecutable -PathType Leaf)) {
    throw 'CMake is required to build the authoritative native client, but it was not found.'
}

Write-Host "Building the authoritative native client in $clientBuildDirectory ..."
$nativeBuildCommand = 'call "{0}" -arch=x64 >nul && where cl.exe >nul && where rc.exe >nul && where mt.exe >nul && "{1}" --build "{2}" --config Debug --target devilutionx' -f
    $vsDevCmd.FullName,
    $cmakeExecutable,
    $clientBuildDirectory
& $env:ComSpec /d /c $nativeBuildCommand
if ($LASTEXITCODE -ne 0) {
    throw "The authoritative native client build failed with exit code $LASTEXITCODE."
}
$clientExecutable = Join-Path $clientBuildDirectory 'devilutionx.exe'
if (-not (Test-Path -LiteralPath $clientExecutable -PathType Leaf)) {
    throw "The authoritative native client build did not produce $clientExecutable."
}
$clientExecutable = (Resolve-Path -LiteralPath $clientExecutable).Path

Write-Host 'Building the authoritative server ...'
& dotnet build $serverProject --configuration Debug --nologo
if ($LASTEXITCODE -ne 0) {
    throw "The authoritative server build failed with exit code $LASTEXITCODE."
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
    # The native launcher enters the local new-game flow, so the paired server
    # must advertise the matching single-player ruleset. A multiplayer server
    # handshake would change gbIsMultiplayer after NetInit and leave the native
    # level-loading path in an incompatible state.
    $serverArguments = @('--port', '0', '--protocol-version', '0.2.0', '--game-mode', 'single-player', '--save-root', $saveRoot)
    $serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $serverOutput -RedirectStandardError $serverError -PassThru

    $port = $null
    $contentHash = $null
    $rulesetHash = $null
    $gameMode = $null
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
        $gameModeMatch = [regex]::Match($output, 'Game mode: ([A-Za-z]+)')
        if ($portMatch.Success) { $port = [int]$portMatch.Groups[1].Value }
        if ($contentMatch.Success) { $contentHash = $contentMatch.Groups[1].Value }
        if ($rulesetMatch.Success) { $rulesetHash = $rulesetMatch.Groups[1].Value }
        if ($gameModeMatch.Success) { $gameMode = $gameModeMatch.Groups[1].Value }
        if ($null -ne $port -and -not [string]::IsNullOrWhiteSpace($contentHash) -and -not [string]::IsNullOrWhiteSpace($rulesetHash) -and -not [string]::IsNullOrWhiteSpace($gameMode)) { break }
    }
    if ($null -eq $port -or [string]::IsNullOrWhiteSpace($contentHash) -or [string]::IsNullOrWhiteSpace($rulesetHash) -or [string]::IsNullOrWhiteSpace($gameMode)) {
        throw "Timed out waiting for the authoritative server identity. See $serverOutput and $serverError"
    }
    if ($gameMode -ne 'SinglePlayer') {
        throw "The local authoritative server started in $gameMode mode instead of SinglePlayer. See $serverOutput"
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
