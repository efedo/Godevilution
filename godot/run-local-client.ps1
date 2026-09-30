[CmdletBinding()]
param(
    [string]$GodotExecutable,
    [ValidateRange(0, 65535)] [int]$Port = 0,
    [ValidateRange(-1, 4294967295)] [long]$CharacterClassId = -1,
    [string]$CharacterName,
    [switch]$Headless,
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 120,
    [string]$SessionDirectory,
    [switch]$PreviewContent,
    [switch]$Multiplayer,
    [ValidateSet('Create', 'Resume', 'Recovery', 'Journey', 'JourneyResume', 'TownLoop', 'TownLoopResume', 'MultiplayerCreate', 'MultiplayerResume')] [string]$AcceptancePhase
)

$ErrorActionPreference = 'Stop'
if ($AcceptancePhase -in @('TownLoop', 'TownLoopResume') -and -not $PreviewContent) {
    throw 'Town-loop acceptance requires -PreviewContent and its isolated content identity.'
}
if ($Multiplayer -and $PreviewContent) { throw 'The local multiplayer profile uses base content, not the single-player preview.' }
if ($AcceptancePhase -and ($Multiplayer -ne ($AcceptancePhase -in @('MultiplayerCreate', 'MultiplayerResume')))) {
    throw 'Multiplayer acceptance phases require -Multiplayer; single-player phases cannot use it.'
}
$expectedGameMode = if ($Multiplayer) { 'Multiplayer' } else { 'SinglePlayer' }
$serverGameMode = if ($Multiplayer) { 'multiplayer' } else { 'single-player' }
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
$clientProject = Join-Path $repositoryRoot 'godot/Devilution.Client'
if ([string]::IsNullOrWhiteSpace($SessionDirectory)) {
    $SessionDirectory = Join-Path $repositoryRoot $(if ($Multiplayer) { '.local/godot-multiplayer' } elseif ($PreviewContent) { '.local/godot-preview' } else { '.local/godot' })
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
        (Get-Command 'Godot_v*-stable_mono_win64.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        (Get-Command 'Godot_v*-stable_mono_win64_console.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        if (Test-Path -LiteralPath $wingetRoot) {
            (Get-ChildItem -LiteralPath $wingetRoot -Filter 'Godot_v*-stable_mono_win64*.exe' -File -Recurse -ErrorAction SilentlyContinue).FullName
        }
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($GodotExecutable) -or -not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
    throw 'A Godot Mono executable was not found on PATH or in WinGet. Pass -GodotExecutable to this inner harness.'
}
function Resolve-DirectGodotExecutable([string]$Path) {
    # Normalize again after selecting the GUI sibling so an explicit relative
    # console path cannot leak a relative executable into ProcessStartInfo.
    $resolvedPath = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
    $enginePath = $resolvedPath -replace '_console\.exe$', '.exe'
    if (Test-Path -LiteralPath $enginePath -PathType Leaf) {
        $resolvedPath = (Resolve-Path -LiteralPath $enginePath -ErrorAction Stop).ProviderPath
    }
    if ([IO.Path]::GetFileName($resolvedPath) -match '_console\.exe$') {
        throw 'A direct Godot Mono executable is required; the console executable has no concrete GUI sibling.'
    }
    return $resolvedPath
}
$GodotExecutable = Resolve-DirectGodotExecutable $GodotExecutable

$serverProcess = $null
$godotProcess = $null
$startedClients = @()
$importProcess = $null
$importTracked = $null
$sessionLock = $null
$processEvidenceRoot = Join-Path $captureRoot 'processes'
New-Item -ItemType Directory -Path $processEvidenceRoot -Force | Out-Null
$savedEnvironment = @{}
$environmentNames = @('DEVILUTION_SERVER_HOST', 'DEVILUTION_SERVER_PORT', 'DEVILUTION_PROTOCOL_VERSION',
    'DEVILUTION_CONTENT_HASH', 'DEVILUTION_RULESET_HASH', 'DEVILUTION_CHARACTER_CLASS_ID',
    'DEVILUTION_CHARACTER_NAME', 'DEVILUTION_RESUME_TOKEN', 'DEVILUTION_RESUME_TOKEN_PATH',
    'GODEVILUTION_ACCEPTANCE_PHASE', 'GODEVILUTION_ACCEPTANCE_DIRECTORY', 'GODEVILUTION_QUIT_AFTER_SECONDS',
    'GODEVILUTION_LOCAL_PLAYER')
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

function Assert-WritableDirectory([string]$Path, [string]$Purpose) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $probePath = Join-Path $Path ('.godevilution-write-probe-' + [guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::WriteAllText($probePath, $Purpose)
    } catch {
        throw "Godot $Purpose storage is not writable: $Path. $($_.Exception.Message)"
    } finally {
        if (Test-Path -LiteralPath $probePath) {
            Remove-Item -LiteralPath $probePath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Write-ProcessEvidence($Tracked) {
    [IO.File]::WriteAllText(
        $Tracked.EvidencePath,
        ($Tracked.Record | ConvertTo-Json -Depth 4))
}

if ($env:OS -eq 'Windows_NT' -and -not ('GodevilutionNativeErrorMode' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class GodevilutionNativeErrorMode
{
    [DllImport("kernel32.dll")]
    public static extern uint GetErrorMode();

    [DllImport("kernel32.dll")]
    public static extern uint SetErrorMode(uint mode);
}
'@
}

function Start-GodotChildProcess($StartInfo) {
    return [Diagnostics.Process]::Start($StartInfo)
}

function New-GodotProcess([string]$StageName, [string]$Arguments, [hashtable]$EnvironmentOverrides, [bool]$Headless, [bool]$SuppressCrashDialog) {
    $stageRoot = Join-Path $captureRoot ('engine-state/' + $StageName)
    $appDataPath = Join-Path $stageRoot 'AppData'
    $localAppDataPath = Join-Path $stageRoot 'LocalAppData'
    Assert-WritableDirectory $appDataPath 'AppData'
    Assert-WritableDirectory $localAppDataPath 'LocalAppData'

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $GodotExecutable
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $repositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $Headless
    $startInfo.WindowStyle = if ($Headless) { 'Hidden' } else { 'Normal' }
    $startInfo.EnvironmentVariables['APPDATA'] = $appDataPath
    $startInfo.EnvironmentVariables['LOCALAPPDATA'] = $localAppDataPath
    foreach ($entry in $EnvironmentOverrides.GetEnumerator()) {
        $startInfo.EnvironmentVariables[$entry.Key] = [string]$entry.Value
    }

    $process = $null
    try {
        $previousErrorMode = $null
        try {
            if ($SuppressCrashDialog -and $env:OS -eq 'Windows_NT') {
                $previousErrorMode = [GodevilutionNativeErrorMode]::GetErrorMode()
                [void][GodevilutionNativeErrorMode]::SetErrorMode($previousErrorMode -bor 3)
            }
            $process = Start-GodotChildProcess $startInfo
        } finally {
            if ($null -ne $previousErrorMode) {
                [void][GodevilutionNativeErrorMode]::SetErrorMode($previousErrorMode)
            }
        }
        if ($null -eq $process) { throw "Could not start Godot for stage $StageName." }
        $startedUtc = [DateTime]::UtcNow.ToString('O')
        try { $startedUtc = $process.StartTime.ToUniversalTime().ToString('O') }
        catch { }
        $tracked = [pscustomobject]@{
            Process = $process
            EvidencePath = Join-Path $processEvidenceRoot ($StageName + '.json')
            Record = [pscustomobject]@{
                Stage = $StageName
                Executable = $GodotExecutable
                ProcessId = $process.Id
                StartedUtc = $startedUtc
                EndedUtc = $null
                ExitCode = $null
                Error = $null
            }
        }
        Write-ProcessEvidence $tracked
        return $tracked
    } catch {
        $primaryError = $_
        if ($null -ne $process) {
            try {
                if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            } catch { Write-Warning "Could not stop failed Godot process for stage ${StageName}: $($_.Exception.Message)" }
            try { $process.Dispose() }
            catch { Write-Warning "Could not dispose failed Godot process for stage ${StageName}: $($_.Exception.Message)" }
        }
        throw $primaryError
    }
}

function Complete-GodotProcess($Tracked, [int]$TimeoutMilliseconds, [string]$FailurePrefix) {
    if ($null -eq $Tracked.Record.EndedUtc) {
        # HasExited is authoritative when the caller already observed process
        # completion; a zero-time WaitForExit can transiently report false.
        if (-not $Tracked.Process.HasExited -and -not $Tracked.Process.WaitForExit($TimeoutMilliseconds)) {
            $Tracked.Record.Error = "$FailurePrefix timed out after $TimeoutMilliseconds ms."
            Write-ProcessEvidence $Tracked
            throw $Tracked.Record.Error
        }
        $Tracked.Record.EndedUtc = [DateTime]::UtcNow.ToString('O')
    }
    if ($null -eq $Tracked.Record.ExitCode) { $Tracked.Record.ExitCode = $Tracked.Process.ExitCode }
    if ($Tracked.Record.ExitCode -ne 0) {
        if ($null -eq $Tracked.Record.Error) { $Tracked.Record.Error = "$FailurePrefix exited with code $($Tracked.Record.ExitCode)." }
        Write-ProcessEvidence $Tracked
        throw "$($Tracked.Record.Error) See the stage log."
    }
    Write-ProcessEvidence $Tracked
}

function Assert-GodotImportLog($Tracked, [string]$ImportLogPath) {
    if (-not (Test-Path -LiteralPath $ImportLogPath -PathType Leaf)) {
        $Tracked.Record.Error = "Godot asset import did not produce its log: $ImportLogPath."
        Write-ProcessEvidence $Tracked
        throw $Tracked.Record.Error
    }
    $importLogText = Get-Content -LiteralPath $ImportLogPath -Raw -ErrorAction Stop
    foreach ($fatalImportPattern in @(
        'Cannot create file',
        'Cannot save file',
        'Cannot save editor help cache',
        'Error saving editor settings')) {
        if ($importLogText -match [regex]::Escape($fatalImportPattern)) {
            $Tracked.Record.Error = "Godot asset import reported fatal storage error: $fatalImportPattern."
            Write-ProcessEvidence $Tracked
            throw "$($Tracked.Record.Error) See $ImportLogPath"
        }
    }
}

function Stop-GodotProcess($Tracked) {
    if ($null -eq $Tracked -or $null -eq $Tracked.Process) { return }
    if (-not $Tracked.Process.HasExited) {
        $Tracked.Process.Kill()
        $Tracked.Process.WaitForExit()
        if ($null -eq $Tracked.Record.Error) { $Tracked.Record.Error = 'Terminated by launcher cleanup.' }
    }
    if ($null -eq $Tracked.Record.EndedUtc) {
        $Tracked.Record.EndedUtc = [DateTime]::UtcNow.ToString('O')
        $Tracked.Record.ExitCode = $Tracked.Process.ExitCode
        if ($null -eq $Tracked.Record.Error -and $Tracked.Record.ExitCode -ne 0) {
            $Tracked.Record.Error = "Godot $($Tracked.Record.Stage) exited with code $($Tracked.Record.ExitCode)."
        }
        Write-ProcessEvidence $Tracked
    }
}

try {
    # One local writer per save directory; parallel tests use distinct directories.
    $sessionLock = [System.IO.File]::Open((Join-Path $SessionDirectory 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    & dotnet build $serverProject --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the authoritative server.' }
    & dotnet build (Join-Path $clientProject 'Devilution.Client.csproj') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the Godot client.' }
    # Import the checked-in SVG assets even on a clean checkout without .godot cache.
    $importLogPath = Join-Path $captureRoot 'import.log'
    $importArguments = '--headless --editor --import --path "{0}" --log-file "{1}"' -f $clientProject, $importLogPath
    $importTracked = New-GodotProcess 'import' $importArguments @{} $true $true
    $importProcess = $importTracked.Process
    Complete-GodotProcess $importTracked 60000 'Godot asset import'
    Assert-GodotImportLog $importTracked $importLogPath

    # Stable town starts; gameplay acceptance travels into combat afterward.
    $acceptanceArguments = if ($AcceptancePhase -or $PreviewContent -or $Multiplayer) { '--starting-level 0 --random-seed 0' } else { '' }
    $overlayArguments = if ($PreviewContent) { '--content-overlay "{0}"' -f (Join-Path $repositoryRoot 'server/content/town-loop-preview') } else { '' }
    function Start-SessionServer([int]$ListenPort, [string]$LogPrefix) {
        $serverArguments = '--port {0} --protocol-version 0.2.0 --game-mode {4} --save-root "{1}/save" {2} {3}' -f $ListenPort, $SessionDirectory, $acceptanceArguments, $overlayArguments, $serverGameMode
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
            if ($gameMode -ne $expectedGameMode) { throw "Unexpected server mode: $gameMode" }
            return [pscustomobject]@{ Process = $serverProcess; Port = $actualPort; ContentHash = $contentHash; RulesetHash = $rulesetHash; Mode = $gameMode }
        } catch {
            if ($null -ne $serverProcess) {
                try {
                    if (-not $serverProcess.HasExited) { $serverProcess.Kill(); $serverProcess.WaitForExit() }
                } catch { Write-Warning "Could not stop the failed server process: $($_.Exception.Message)" }
                try { $serverProcess.Dispose() }
                catch { Write-Warning "Could not dispose the failed server process: $($_.Exception.Message)" }
            }
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
    function Start-SceneClient([int]$Slot) {
        $clientDirectory = if ($Multiplayer) { Join-Path $SessionDirectory "player-$Slot" } else { $SessionDirectory }
        New-Item -ItemType Directory -Path $clientDirectory -Force | Out-Null
        $clientLog = Join-Path $captureRoot $(if ($Multiplayer) { "godot-player-$Slot.log" } else { 'godot.log' })
        # Each process gets isolated engine storage and its own role/bookmark environment.
        $clientEnvironment = @{
            DEVILUTION_RESUME_TOKEN_PATH = Join-Path $clientDirectory 'resume-token.txt'
        }
        if ($CharacterClassId -ge 0) { $clientEnvironment['DEVILUTION_CHARACTER_CLASS_ID'] = [string]$CharacterClassId }
        if (-not [string]::IsNullOrEmpty($CharacterName)) { $clientEnvironment['DEVILUTION_CHARACTER_NAME'] = $CharacterName }
        if ($Multiplayer) {
            $clientEnvironment['GODEVILUTION_LOCAL_PLAYER'] = [string]$Slot
            $clientEnvironment['DEVILUTION_CHARACTER_NAME'] = if ($CharacterName) { "$CharacterName $Slot" } else { "Hero $Slot" }
        }
        $godotArguments = '--path "{0}" --log-file "{1}"' -f $clientProject, $clientLog
        if ($Headless) { $godotArguments += ' --headless' }
        $clientEvidence = $null
        if ($AcceptancePhase) {
            $clientEnvironment['GODEVILUTION_ACCEPTANCE_PHASE'] = $AcceptancePhase
            $clientEnvironment['GODEVILUTION_ACCEPTANCE_DIRECTORY'] = $clientDirectory
            $godotArguments += ' --scene res://Acceptance/SessionAcceptance.tscn'
            $clientEvidence = Join-Path $clientDirectory ($AcceptancePhase.ToLowerInvariant() + '.json')
            if (Test-Path -LiteralPath $clientEvidence) { throw "Use a fresh acceptance directory; evidence already exists at $clientEvidence" }
            if ($Multiplayer -and (Get-ChildItem -LiteralPath $clientDirectory -Filter ($AcceptancePhase.ToLowerInvariant() + '-*.json') -File)) {
                throw 'Multiplayer acceptance requires fresh phase markers, not a partially completed run.'
            }
            if ($AcceptancePhase -eq 'Recovery') {
                foreach ($marker in @('recovery-ready.json', 'recovery-server-stopped.json', 'recovery-disconnected.json')) {
                    if (Test-Path -LiteralPath (Join-Path $clientDirectory $marker)) { throw "Recovery evidence already exists: $marker" }
                }
            }
        } elseif ($Headless) {
            # Godot --quit-after counts frames, not seconds. The scene owns the timer.
            $clientEnvironment['GODEVILUTION_QUIT_AFTER_SECONDS'] = [string]$QuitAfterSeconds
        }
        $clientTracked = New-GodotProcess ("client-$Slot") $godotArguments $clientEnvironment ([bool]$Headless) ([bool]($Headless -or $AcceptancePhase))
        return [pscustomobject]@{
            Process = $clientTracked.Process
            Tracked = $clientTracked
            Log = $clientLog
            Evidence = $clientEvidence
            Directory = $clientDirectory
        }
    }
    $startedClients += Start-SceneClient 1
    $godotProcess = $startedClients[0].Process
    if ($Multiplayer) { $startedClients += Start-SceneClient 2 }
    if ($Multiplayer -and $AcceptancePhase) {
        $processEvidence = @{ HostProcessId = $serverProcess.Id; HostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O'); Port = $actualPort;
            Clients = @($startedClients | ForEach-Object { @{ ProcessId = $_.Process.Id; StartedUtc = $_.Process.StartTime.ToUniversalTime().ToString('O') } }) }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory ($AcceptancePhase.ToLowerInvariant() + '-processes.json')), ($processEvidence | ConvertTo-Json -Depth 4))
    }
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
    $clientWait = [Diagnostics.Stopwatch]::StartNew()
    do {
        $clientsRunning = $false
        foreach ($startedClient in $startedClients) {
            if (-not $startedClient.Process.HasExited) { $clientsRunning = $true }
            else {
                Complete-GodotProcess $startedClient.Tracked 0 'Godot client'
            }
        }
        if ($clientsRunning -and ($Headless -or $AcceptancePhase) -and $clientWait.Elapsed.TotalSeconds -gt ($QuitAfterSeconds + 15)) {
            foreach ($startedClient in $startedClients) {
                if (-not $startedClient.Process.HasExited) {
                    $startedClient.Tracked.Record.Error = 'Godot did not finish before the launch timeout.'
                    Write-ProcessEvidence $startedClient.Tracked
                }
            }
            throw 'Godot did not finish before the launch timeout.'
        }
        if ($clientsRunning) { Start-Sleep -Milliseconds 100 }
    } while ($clientsRunning)
    if ($AcceptancePhase) {
        foreach ($startedClient in $startedClients) {
            if (-not (Test-Path -LiteralPath $startedClient.Evidence)) { throw "Godot did not emit acceptance evidence. See $($startedClient.Log)" }
            $evidence = Get-Content -LiteralPath $startedClient.Evidence -Raw | ConvertFrom-Json
            if (-not $evidence.Passed -or $evidence.Mode -ne $expectedGameMode -or $evidence.ContentHash -ne $contentHash -or $evidence.RulesetHash -ne $rulesetHash) {
                throw "Godot acceptance evidence failed identity/mode validation. See $($startedClient.Evidence)"
            }
        }
        if (-not (Get-ChildItem -LiteralPath (Join-Path $SessionDirectory 'save') -Filter '*.session.json' -File)) {
            throw 'Godot acceptance did not produce an authoritative checkpoint.'
        }
        if ($Multiplayer) {
            if (@(Get-ChildItem -LiteralPath (Join-Path $SessionDirectory 'save') -Filter '*.session.json' -File).Count -ne 2) {
                throw 'Multiplayer acceptance must preserve exactly two authoritative character checkpoints.'
            }
            $firstBookmark = [IO.File]::ReadAllText((Join-Path $SessionDirectory 'player-1/resume-token.txt')).Trim()
            $secondBookmark = [IO.File]::ReadAllText((Join-Path $SessionDirectory 'player-2/resume-token.txt')).Trim()
            if ([string]::IsNullOrWhiteSpace($firstBookmark) -or [string]::IsNullOrWhiteSpace($secondBookmark) -or $firstBookmark -ceq $secondBookmark) {
                throw 'Multiplayer players must retain distinct private resume bookmarks.'
            }
        }
    }
} finally {
    foreach ($trackedProcess in @($startedClients | ForEach-Object { $_.Tracked }) + @($importTracked)) {
        if ($null -ne $trackedProcess) {
            try { Stop-GodotProcess $trackedProcess }
            catch { Write-Warning "Could not finalize Godot process evidence: $($_.Exception.Message)" }
        }
    }
    foreach ($ownedProcess in @($startedClients | ForEach-Object { $_.Process }) + @($serverProcess, $importProcess)) {
        if ($null -ne $ownedProcess) {
            try {
                if (-not $ownedProcess.HasExited) { $ownedProcess.Kill(); $ownedProcess.WaitForExit() }
            } catch { Write-Warning "Could not stop owned process $($ownedProcess.Id): $($_.Exception.Message)" }
            try { $ownedProcess.Dispose() }
            catch { Write-Warning "Could not dispose owned process: $($_.Exception.Message)" }
        }
    }
    if ($null -ne $sessionLock) {
        try { $sessionLock.Dispose() }
        catch { Write-Warning "Could not dispose the session lock: $($_.Exception.Message)" }
    }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    Write-Host "Session files: $SessionDirectory"
    Write-Host "Launch logs: $captureRoot"
}
