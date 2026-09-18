[CmdletBinding()]
param(
    [string]$GodotExecutable,
    [int]$Port = 0,
    [ValidateRange(-1, 4294967295)]
    [long]$CharacterClassId = -1,
    [string]$CharacterName,
    [switch]$Headless,
    [ValidateRange(1, 3600)]
    [int]$QuitAfterSeconds = 120
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
$clientProject = Join-Path $repositoryRoot 'godot/Devilution.Client'
$captureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Godot-' + [guid]::NewGuid().ToString('N'))
$stdoutPath = Join-Path $captureRoot 'server.stdout.log'
$stderrPath = Join-Path $captureRoot 'server.stderr.log'
$godotLogPath = Join-Path $captureRoot 'godot.log'
New-Item -ItemType Directory -Path $captureRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe'
    $godotCandidates = @(
        (Get-Command 'Godot_v*-stable_mono_win64_console.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        (Get-Command 'Godot_v*-stable_mono_win64.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        if (Test-Path -LiteralPath $wingetRoot) {
            (Get-ChildItem -LiteralPath $wingetRoot -Filter 'Godot_v*-stable_mono_win64_console.exe' -File -Recurse -ErrorAction SilentlyContinue).FullName
            (Get-ChildItem -LiteralPath $wingetRoot -Filter 'Godot_v*-stable_mono_win64.exe' -File -Recurse -ErrorAction SilentlyContinue).FullName
        }
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
    if ($null -eq $godotCandidates)
        { throw 'A Godot Mono executable was not found on PATH or the standard WinGet package directory. Pass -GodotExecutable explicitly.' }
    $GodotExecutable = $godotCandidates
}

$serverStartTime = Get-Date
if (-not (Test-Path -LiteralPath $serverExecutable)) {
    & dotnet build $serverProject
    if ($LASTEXITCODE -ne 0)
        { throw "Could not build the authoritative server project at $serverProject." }
}
$serverArguments = "--port $Port --protocol-version 0.2.0 --save-root `"$captureRoot/save`""
$serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru

try {
    $actualPort = $null
    $contentHash = $null
    $rulesetHash = $null
    for ($attempt = 0; $attempt -lt 120 -and $null -eq $actualPort; $attempt++) {
        Start-Sleep -Milliseconds 250
        if ($serverProcess.HasExited) {
            throw "Authoritative server exited during startup. See $stderrPath"
        }
        $output = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
        if ($output -match 'listening on [^:]+:(\d+)') { $actualPort = [int]$Matches[1] }
        if ($output -match 'Content manifest: ([0-9a-fA-F]+)') { $contentHash = $Matches[1] }
        if ($output -match 'Ruleset identity: ([0-9a-fA-F]+)') { $rulesetHash = $Matches[1] }
    }
    if (($null -eq $actualPort) -or [string]::IsNullOrWhiteSpace($contentHash) -or [string]::IsNullOrWhiteSpace($rulesetHash)) {
        throw "Timed out waiting for the authoritative server identity. See $stdoutPath and $stderrPath"
    }

    $env:DEVILUTION_SERVER_HOST = '127.0.0.1'
    $env:DEVILUTION_SERVER_PORT = $actualPort
    $env:DEVILUTION_PROTOCOL_VERSION = '0.2.0'
    $env:DEVILUTION_CONTENT_HASH = $contentHash
    $env:DEVILUTION_RULESET_HASH = $rulesetHash
    if ($CharacterClassId -ge 0) {
        $env:DEVILUTION_CHARACTER_CLASS_ID = [string]$CharacterClassId
    } else {
        Remove-Item Env:DEVILUTION_CHARACTER_CLASS_ID -ErrorAction SilentlyContinue
    }
    if (-not [string]::IsNullOrEmpty($CharacterName)) {
        $env:DEVILUTION_CHARACTER_NAME = $CharacterName
    } else {
        Remove-Item Env:DEVILUTION_CHARACTER_NAME -ErrorAction SilentlyContinue
    }
    $godotArguments = if ($Headless) { "--headless --path `"$clientProject`" --log-file `"$godotLogPath`" --quit-after $QuitAfterSeconds" } else { "--path `"$clientProject`" --log-file `"$godotLogPath`"" }
    $godotProcess = Start-Process -FilePath $GodotExecutable -ArgumentList $godotArguments -WorkingDirectory $repositoryRoot -PassThru -Wait
    if ($godotProcess.ExitCode -ne 0)
        { throw "Godot exited with code $($godotProcess.ExitCode)." }
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        $serverProcess.WaitForExit()
    }
    Get-Process -Name 'Devilution.Server' -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $serverStartTime } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "Server logs: $captureRoot"
}
