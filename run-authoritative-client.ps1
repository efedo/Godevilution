[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
function Assert-NativeProfileAncestry([string]$Path) {
    $current = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    while ($null -ne $current) {
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Native acceptance cannot use a reparse-point path or ancestor: $($current.FullName)"
        }
        $current = if ($current -is [IO.DirectoryInfo]) { $current.Parent } else { $current.Directory }
    }
}

function Open-NativeProfileLock([string]$Root) {
    Assert-NativeProfileAncestry $Root
    try {
        return [IO.File]::Open((Join-Path $Root 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    } catch {
        throw 'Cannot exclusively lock the native acceptance profile; another run may be using it.'
    }
}

function Write-NativeProcessOutcome($Process, $Evidence, [string]$Path) {
    if ($null -eq $Process -or $null -eq $Evidence -or -not $Process.HasExited) { return }
    if ($null -eq $Evidence.clientEndedUtc) { $Evidence.clientEndedUtc = [DateTime]::UtcNow.ToString('o') }
    $Evidence.clientExitCode = $Process.ExitCode
    [IO.File]::WriteAllText($Path, ($Evidence | ConvertTo-Json))
}

function Get-NativeAcceptanceLayout([string]$Root, [bool]$Resume) {
    $candidate = [IO.Path]::GetFullPath($Root)
    if ($candidate.Contains('"')) { throw 'The native acceptance directory must not contain a quote.' }
    if (-not $Resume) {
        if (Test-Path -LiteralPath $candidate) {
            throw "Native acceptance requires a new directory, not an existing path: $candidate"
        }
        $parent = [IO.DirectoryInfo]::new($candidate).Parent
        while ($null -ne $parent -and -not $parent.Exists) { $parent = $parent.Parent }
        if ($null -eq $parent) { throw 'Native acceptance requires an existing parent volume.' }
        Assert-NativeProfileAncestry $parent.FullName
        return [pscustomobject]@{ ProfileRoot = $candidate; CaptureRoot = $candidate; Resume = $false }
    }

    # Only profiles created by this harness can be resumed. Never infer a save
    # location from the user's normal native configuration or latest temp folder.
    $markerPath = Join-Path $candidate 'native-acceptance-profile.json'
    $requiredDirectories = @($candidate, (Join-Path $candidate 'save'),
        (Join-Path $candidate 'native-save'), (Join-Path $candidate 'native-config'))
    $requiredFiles = @($markerPath, (Join-Path $candidate 'native-config/diablo.ini'),
        (Join-Path $candidate 'resume-token.txt'))
    foreach ($path in ($requiredDirectories + $requiredFiles)) {
        $item = Get-Item -LiteralPath $path -ErrorAction Stop
        if ($item.PSIsContainer -ne ($path -in $requiredDirectories)) {
            throw "Native acceptance profile has the wrong path type: $path"
        }
        Assert-NativeProfileAncestry $path
    }
    $marker = Get-Content -LiteralPath $markerPath -Raw -ErrorAction Stop | ConvertFrom-Json
    if ($marker.Kind -cne 'GodevilutionNativeAcceptance' -or $marker.Version -ne 2 -or
        $marker.ContentProfile -cne 'godevilution.native-start@0.1.0' -or
        -not [string]::Equals([string]$marker.ProfileRoot, $candidate, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The native acceptance profile marker does not match the requested directory.'
    }
    # The native reader takes one binary line without trimming; its writer uses
    # one token plus LF. Never let an empty first line become a new session.
    $bookmark = [IO.File]::ReadAllText((Join-Path $candidate 'resume-token.txt'))
    if ($bookmark -cnotmatch '\A[^\s]+\n?\z' -or
        -not (Get-ChildItem -LiteralPath (Join-Path $candidate 'save') -File -Filter '*.session.json' |
            Where-Object { $_.Length -gt 0 -and ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 })) {
        throw 'Native resume acceptance requires a private bookmark and an authoritative checkpoint.'
    }
    return [pscustomobject]@{
        ProfileRoot = $candidate
        CaptureRoot = Join-Path $candidate ('resume-' + [guid]::NewGuid().ToString('N'))
        Resume = $true
    }
}

# The diagnostic wrapper supplies an isolated profile. Normal zero-argument
# launches retain the user's existing native save/config locations.
$acceptanceRoot = $env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY
$isAcceptanceRun = -not [string]::IsNullOrWhiteSpace($acceptanceRoot)
$isResumeAcceptance = $env:GODEVILUTION_NATIVE_ACCEPTANCE_RESUME -eq '1'
if ($isResumeAcceptance -and -not $isAcceptanceRun) { throw 'Native resume acceptance requires an explicit test profile.' }
$acceptanceLayout = if ($isAcceptanceRun) { Get-NativeAcceptanceLayout $acceptanceRoot $isResumeAcceptance } else { $null }
$captureRoot = if ($isAcceptanceRun) {
    $acceptanceLayout.CaptureRoot
} else {
    Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Authoritative-' + [guid]::NewGuid().ToString('N'))
}
$profileRoot = if ($isAcceptanceRun) { $acceptanceLayout.ProfileRoot } else { $captureRoot }
$nativeProfileLock = $null
try {
    if ($isResumeAcceptance) { $nativeProfileLock = Open-NativeProfileLock $profileRoot }
    $serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
    $serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
    $nativeStartContent = Join-Path $repositoryRoot 'server/content/native-start'
    if (-not (Test-Path -LiteralPath (Join-Path $nativeStartContent 'content-pack.json') -PathType Leaf)) {
        throw 'The native new-game entry content pack is missing.'
    }
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

    $serverOutput = Join-Path $captureRoot 'server.stdout.log'
    $serverError = Join-Path $captureRoot 'server.stderr.log'
    $clientOutput = Join-Path $captureRoot 'native.log'
    $saveRoot = Join-Path $profileRoot 'save'
    $resumeTokenPath = Join-Path $profileRoot 'resume-token.txt'
    $diagnosticsDirectory = Join-Path $captureRoot 'diagnostics'
    New-Item -ItemType Directory -Path $captureRoot, $diagnosticsDirectory | Out-Null
    if (-not $isResumeAcceptance) { New-Item -ItemType Directory -Path $saveRoot | Out-Null }
    if ($isAcceptanceRun) {
        $nativeSaveRoot = Join-Path $profileRoot 'native-save'
        $nativeConfigRoot = Join-Path $profileRoot 'native-config'
        if (-not $isResumeAcceptance) {
            New-Item -ItemType Directory -Path $nativeSaveRoot, $nativeConfigRoot | Out-Null
            [System.IO.File]::WriteAllText(
                (Join-Path $nativeConfigRoot 'diablo.ini'),
                "[Graphics]`r`nFullscreen=0`r`nWidth=1024`r`nHeight=768`r`n",
                [System.Text.UTF8Encoding]::new($false))
            [IO.File]::WriteAllText((Join-Path $profileRoot 'native-acceptance-profile.json'),
                (@{ Kind = 'GodevilutionNativeAcceptance'; Version = 2; ProfileRoot = $profileRoot;
                    ContentProfile = 'godevilution.native-start@0.1.0' } | ConvertTo-Json))
            $nativeProfileLock = Open-NativeProfileLock $profileRoot
        }
    }
} catch {
    if ($null -ne $nativeProfileLock) { $nativeProfileLock.Dispose() }
    throw
}

$serverProcess = $null
$clientProcess = $null
$processEvidence = $null
try {
    # The native launcher enters the local new-game flow, so the paired server
    # must advertise the matching single-player ruleset. A multiplayer server
    # handshake would change gbIsMultiplayer after NetInit and leave the native
    # level-loading path in an incompatible state.
    # Only this native profile selects town's validated content-owned entry.
    # Standalone/base/Godot defaults and pack bytes remain unchanged.
    $nativeSeedArguments = if ($isAcceptanceRun) { @('--random-seed', '0') } else { @() }
    $serverArguments = @('--port', '0', '--protocol-version', '0.2.0', '--game-mode', 'single-player',
        '--starting-level', '0', '--content-overlay', ('"' + $nativeStartContent + '"'),
        '--save-root', ('"' + $saveRoot + '"')) + $nativeSeedArguments
    $serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -WindowStyle Hidden -RedirectStandardOutput $serverOutput -RedirectStandardError $serverError -PassThru

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
        # The paired server uses the base pack, even when GOG Hellfire exists.
        '--diablo',
        '--log-to-file', ('"' + $clientOutput + '"'),
        '--game-mode', 'authoritative',
        '--authoritative-server', $serverEndpoint,
        '--authoritative-protocol-version', '0.2.0',
        '--authoritative-content-hash', $contentHash,
        '--authoritative-ruleset-hash', $rulesetHash,
        '--authoritative-resume-token', ('"' + $resumeTokenPath + '"'),
        '--authoritative-diagnostics', ('"' + $diagnosticsDirectory + '"')
    )
    if ($isAcceptanceRun) {
        $clientArguments += @(
            '-n',
            '--save-dir', ('"' + $nativeSaveRoot + '"'),
            '--config-dir', ('"' + $nativeConfigRoot + '"')
        )
        if ($isResumeAcceptance) {
            Write-Host 'Manual resume acceptance: select the existing test hero, enter the restored authoritative world, verify movement, then exit.'
        } else {
            Write-Host 'Manual acceptance: select Single Player, create a new hero, enter the world, verify movement, then exit.'
        }
        Write-Host 'A successful process exit alone does not prove GUI or gameplay acceptance.'
    }
    Write-Host "Authoritative server listening on $serverEndpoint"
    Write-Host "Launching $clientExecutable"
    $clientProcess = Start-Process -FilePath $clientExecutable -ArgumentList $clientArguments -WorkingDirectory $repositoryRoot -WindowStyle Normal -PassThru
    $processEvidence = [ordered]@{
        clientPid = $clientProcess.Id
        clientStartedUtc = $clientProcess.StartTime.ToUniversalTime().ToString('o')
        serverPid = $serverProcess.Id
        serverStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('o')
        acceptanceResume = $isResumeAcceptance
        clientEndedUtc = $null
        clientExitCode = $null
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $captureRoot 'processes.json'),
        ($processEvidence | ConvertTo-Json),
        [System.Text.UTF8Encoding]::new($false))
    # Keep process ownership before waiting, including on an interrupted run.
    while (-not $clientProcess.WaitForExit(250)) { }
    Write-NativeProcessOutcome $clientProcess $processEvidence (Join-Path $captureRoot 'processes.json')
    exit $clientProcess.ExitCode
}
finally {
    try {
        if ($null -ne $clientProcess -and -not $clientProcess.HasExited) {
            Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
            $clientProcess.WaitForExit()
        }
        if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
            Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
            $serverProcess.WaitForExit()
        }
    } finally {
        try { Write-NativeProcessOutcome $clientProcess $processEvidence (Join-Path $captureRoot 'processes.json') }
        catch { Write-Warning "Could not finalize native process evidence: $($_.Exception.Message)" }
        if ($null -ne $nativeProfileLock) { $nativeProfileLock.Dispose() }
        Write-Host "Authoritative launch logs: $captureRoot"
    }
}
