[CmdletBinding()]
param(
    [string]$GodotExecutable,
    [ValidateRange(0, 65535)] [int]$Port = 0,
    [ValidateRange(-1, 4294967295)] [long]$CharacterClassId = -1,
    [string]$CharacterName,
    [switch]$Headless,
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 120,
    [string]$SessionDirectory,
    [ValidateSet('Create', 'Resume', 'Recovery')] [string]$AcceptancePhase
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
$clientProject = Join-Path $repositoryRoot 'godot/Devilution.Client'
if ([string]::IsNullOrWhiteSpace($SessionDirectory)) {
    $SessionDirectory = Join-Path $repositoryRoot '.local/godot'
}
$SessionDirectory = [System.IO.Path]::GetFullPath($SessionDirectory)
if ($SessionDirectory.Contains('"')) { throw 'The session directory must not contain a quote.' }
$captureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Godot-' + [guid]::NewGuid().ToString('N'))
$godotLogPath = Join-Path $captureRoot 'godot.log'
New-Item -ItemType Directory -Path $captureRoot | Out-Null
New-Item -ItemType Directory -Path $SessionDirectory -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe'
    $GodotExecutable = @(
        (Get-Command 'Godot_v*-stable_mono_win64_console.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        (Get-Command 'Godot_v*-stable_mono_win64.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        if (Test-Path -LiteralPath $wingetRoot) {
            (Get-ChildItem -LiteralPath $wingetRoot -Filter 'Godot_v*-stable_mono_win64*.exe' -File -Recurse -ErrorAction SilentlyContinue).FullName
        }
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($GodotExecutable) -or -not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
    throw 'A Godot Mono executable was not found on PATH or in WinGet. Pass -GodotExecutable to this inner harness.'
}
# Track the engine itself, not the console wrapper which spawns a child process.
$engineExecutable = $GodotExecutable -replace '_console\.exe$', '.exe'
if (Test-Path -LiteralPath $engineExecutable -PathType Leaf) { $GodotExecutable = $engineExecutable }

$serverProcess = $null
$godotProcess = $null
$importProcess = $null
$sessionLock = $null
$savedEnvironment = @{}
$environmentNames = @('DEVILUTION_SERVER_HOST', 'DEVILUTION_SERVER_PORT', 'DEVILUTION_PROTOCOL_VERSION',
    'DEVILUTION_CONTENT_HASH', 'DEVILUTION_RULESET_HASH', 'DEVILUTION_CHARACTER_CLASS_ID',
    'DEVILUTION_CHARACTER_NAME', 'DEVILUTION_RESUME_TOKEN', 'DEVILUTION_RESUME_TOKEN_PATH',
    'GODEVILUTION_ACCEPTANCE_PHASE', 'GODEVILUTION_ACCEPTANCE_DIRECTORY', 'GODEVILUTION_QUIT_AFTER_SECONDS')
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    # One local writer per save directory; parallel tests use distinct directories.
    $sessionLock = [System.IO.File]::Open((Join-Path $SessionDirectory 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    & dotnet build $serverProject --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the authoritative server.' }
    & dotnet build (Join-Path $clientProject 'Devilution.Client.csproj') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the Godot client.' }
    # Import the checked-in SVG assets even on a clean checkout without .godot cache.
    $importArguments = '--headless --editor --import --path "{0}" --log-file "{1}"' -f $clientProject, (Join-Path $captureRoot 'import.log')
    $importProcess = Start-Process -FilePath $GodotExecutable -ArgumentList $importArguments -WorkingDirectory $repositoryRoot -WindowStyle Hidden -PassThru
    if (-not $importProcess.WaitForExit(60000)) { throw 'Godot asset import timed out.' }
    if ($importProcess.ExitCode -ne 0) { throw "Godot asset import failed. See $captureRoot/import.log" }

    # Acceptance uses a fixed seed and town to exclude autonomous combat/knockback.
    $acceptanceArguments = if ($AcceptancePhase) { '--starting-level 0 --random-seed 0' } else { '' }
    function Start-SessionServer([int]$ListenPort, [string]$LogPrefix) {
        $serverArguments = '--port {0} --protocol-version 0.2.0 --game-mode single-player --save-root "{1}/save" {2}' -f $ListenPort, $SessionDirectory, $acceptanceArguments
        $stdoutPath = Join-Path $captureRoot ($LogPrefix + '.stdout.log')
        $stderrPath = Join-Path $captureRoot ($LogPrefix + '.stderr.log')
        $serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
        try {
            $actualPort = $null
            $contentHash = $null
            $rulesetHash = $null
            $gameMode = $null
            for ($attempt = 0; $attempt -lt 120; $attempt++) {
                if ($serverProcess.HasExited) { throw "Authoritative server exited during startup. See $stderrPath" }
                $output = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
                if ($output -match 'listening on [^:]+:(\d+)') { $actualPort = [int]$Matches[1] }
                if ($output -match 'Content manifest: ([0-9a-fA-F]{64})') { $contentHash = $Matches[1] }
                if ($output -match 'Ruleset identity: ([0-9a-fA-F]{64})') { $rulesetHash = $Matches[1] }
                if ($output -match 'Game mode: (\w+)') { $gameMode = $Matches[1] }
                if ($actualPort -and $contentHash -and $rulesetHash -and $gameMode) { break }
                Start-Sleep -Milliseconds 250
            }
            if (-not ($actualPort -and $contentHash -and $rulesetHash -and $gameMode)) {
                throw "Timed out waiting for the authoritative server identity. See $stdoutPath and $stderrPath"
            }
            if ($gameMode -ne 'SinglePlayer') { throw "Unexpected server mode: $gameMode" }
            return [pscustomobject]@{ Process = $serverProcess; Port = $actualPort; ContentHash = $contentHash; RulesetHash = $rulesetHash; Mode = $gameMode }
        } catch {
            if (-not $serverProcess.HasExited) { $serverProcess.Kill(); $serverProcess.WaitForExit() }
            $serverProcess.Dispose()
            throw
        }
    }
    function Wait-SceneEvidence([string]$Name) {
        $path = Join-Path $SessionDirectory $Name
        $deadline = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $path)) {
            if ($godotProcess.HasExited) { throw "Godot exited before emitting $Name. See $godotLogPath" }
            if ($deadline.Elapsed.TotalSeconds -ge 30) { throw "Godot did not emit $Name. See $godotLogPath" }
            Start-Sleep -Milliseconds 100
        }
        return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    $startedServer = Start-SessionServer $Port 'server'
    $serverProcess = $startedServer.Process
    $actualPort = $startedServer.Port
    $contentHash = $startedServer.ContentHash
    $rulesetHash = $startedServer.RulesetHash

    # Clear inherited session/test settings, then supply this launch's identity.
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    $env:DEVILUTION_SERVER_HOST = '127.0.0.1'
    $env:DEVILUTION_SERVER_PORT = [string]$actualPort
    $env:DEVILUTION_PROTOCOL_VERSION = '0.2.0'
    $env:DEVILUTION_CONTENT_HASH = $contentHash
    $env:DEVILUTION_RULESET_HASH = $rulesetHash
    $env:DEVILUTION_RESUME_TOKEN_PATH = Join-Path $SessionDirectory 'resume-token.txt'
    if ($CharacterClassId -ge 0) { $env:DEVILUTION_CHARACTER_CLASS_ID = [string]$CharacterClassId }
    if (-not [string]::IsNullOrEmpty($CharacterName)) { $env:DEVILUTION_CHARACTER_NAME = $CharacterName }
    $godotArguments = '--path "{0}" --log-file "{1}"' -f $clientProject, $godotLogPath
    if ($Headless) { $godotArguments += ' --headless' }
    if ($AcceptancePhase) {
        $env:GODEVILUTION_ACCEPTANCE_PHASE = $AcceptancePhase
        $env:GODEVILUTION_ACCEPTANCE_DIRECTORY = $SessionDirectory
        $godotArguments += ' --scene res://Acceptance/SessionAcceptance.tscn'
        $evidencePath = Join-Path $SessionDirectory ($AcceptancePhase.ToLowerInvariant() + '.json')
        if (Test-Path -LiteralPath $evidencePath) { throw "Use a fresh acceptance directory; evidence already exists at $evidencePath" }
        if ($AcceptancePhase -eq 'Recovery') {
            foreach ($marker in @('recovery-ready.json', 'recovery-server-stopped.json', 'recovery-disconnected.json')) {
                if (Test-Path -LiteralPath (Join-Path $SessionDirectory $marker)) { throw "Recovery evidence already exists: $marker" }
            }
        }
    } elseif ($Headless) {
        # Godot --quit-after counts frames, not seconds. The scene owns the timer.
        $env:GODEVILUTION_QUIT_AFTER_SECONDS = [string]$QuitAfterSeconds
    }
    $windowStyle = if ($Headless) { 'Hidden' } else { 'Normal' }
    $godotProcess = Start-Process -FilePath $GodotExecutable -ArgumentList $godotArguments -WorkingDirectory $repositoryRoot -WindowStyle $windowStyle -PassThru
    if ($AcceptancePhase -eq 'Recovery') {
        $ready = Wait-SceneEvidence 'recovery-ready.json'
        if (-not $ready.Ready -or $ready.Sequence -lt 1) { throw 'Invalid recovery readiness evidence.' }
        if ($serverProcess.HasExited) { throw 'The server exited before the recovery fault injection.' }
        # Kill this owned host only. Keep the original Godot process running.
        $stoppedProcessId = $serverProcess.Id
        $serverProcess.Kill()
        $serverProcess.WaitForExit()
        $serverProcess.Dispose()
        $serverProcess = $null
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'recovery-server-stopped.json'), '{"Stopped":true}')
        $offline = Wait-SceneEvidence 'recovery-disconnected.json'
        if (-not $offline.Passed -or $offline.Sequence -ne $ready.Sequence -or $offline.OfflineSeconds -lt 1.5) {
            throw 'Godot did not verify frozen disconnected state and disabled gameplay input.'
        }
        $restartedServer = Start-SessionServer $actualPort 'server-restarted'
        $serverProcess = $restartedServer.Process
        if ($restartedServer.Port -ne $actualPort -or $restartedServer.ContentHash -ne $contentHash -or $restartedServer.RulesetHash -ne $rulesetHash) {
            throw 'The restarted host did not preserve the original endpoint and content/ruleset identity.'
        }
        $restartEvidence = @{ StoppedProcessId = $stoppedProcessId; RestartedProcessId = $serverProcess.Id; GodotProcessId = $godotProcess.Id; Port = $actualPort }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'recovery-processes.json'), ($restartEvidence | ConvertTo-Json))
    }
    if ($Headless -or $AcceptancePhase) {
        if (-not $godotProcess.WaitForExit(($QuitAfterSeconds + 15) * 1000)) { throw 'Godot did not finish before the launch timeout.' }
    } else {
        $godotProcess.WaitForExit()
    }
    if ($godotProcess.ExitCode -ne 0) { throw "Godot exited with code $($godotProcess.ExitCode). See $godotLogPath" }
    if ($AcceptancePhase) {
        if (-not (Test-Path -LiteralPath $evidencePath)) { throw "Godot did not emit acceptance evidence. See $godotLogPath" }
        $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
        if (-not $evidence.Passed -or $evidence.Mode -ne 'SinglePlayer' -or $evidence.ContentHash -ne $contentHash -or $evidence.RulesetHash -ne $rulesetHash) {
            throw "Godot acceptance evidence failed identity/mode validation. See $evidencePath"
        }
        if (-not (Get-ChildItem -LiteralPath (Join-Path $SessionDirectory 'save') -Filter '*.session.json' -File)) {
            throw 'Godot acceptance did not produce an authoritative checkpoint.'
        }
    }
} finally {
    foreach ($ownedProcess in @($godotProcess, $serverProcess, $importProcess)) {
        if ($null -ne $ownedProcess) {
            if (-not $ownedProcess.HasExited) { $ownedProcess.Kill(); $ownedProcess.WaitForExit() }
            $ownedProcess.Dispose()
        }
    }
    if ($null -ne $sessionLock) { $sessionLock.Dispose() }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    Write-Host "Session files: $SessionDirectory"
    Write-Host "Launch logs: $captureRoot"
}
