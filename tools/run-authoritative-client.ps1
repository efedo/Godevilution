[CmdletBinding()]
param(
    [string]$ClientExecutable,
    [string]$ServerExecutable,
    [string]$ServerEndpoint,
    [string]$ProtocolVersion = '0.2.0',
    [string]$ContentHash,
    [string]$RulesetHash,
    [string]$DiagnosticsDirectory,
    [switch]$Legacy,
    [string[]]$ClientArgument = @()
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$captureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Authoritative-' + [guid]::NewGuid().ToString('N'))
$serverOutput = Join-Path $captureRoot 'server.stdout.log'
$serverError = Join-Path $captureRoot 'server.stderr.log'
$resumeTokenPath = Join-Path $captureRoot 'resume-token.txt'
New-Item -ItemType Directory -Path $captureRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($ClientExecutable)) {
    $clientCandidates = @(
        (Join-Path $repositoryRoot 'build/x64-Debug-authoritative-vcpkg/devilutionx.exe'),
        (Join-Path $repositoryRoot 'build/x64-Debug-authoritative/devilutionx.exe'),
        (Join-Path $repositoryRoot 'build/x64-Debug/devilutionx.exe'),
        (Join-Path $repositoryRoot 'build/x64-Release-authoritative-vcpkg/devilutionx.exe'),
        (Join-Path $repositoryRoot 'build/x64-Release-authoritative/devilutionx.exe')
    )
    $clientCandidates = @($clientCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($clientCandidates.Count -eq 0) {
        throw "No DevilutionX client executable was found under $repositoryRoot\build. Build the authoritative client or pass -ClientExecutable <path>."
    }
    $ClientExecutable = $clientCandidates[0]
} elseif (-not [System.IO.Path]::IsPathRooted($ClientExecutable)) {
    $workingDirectoryPath = Join-Path (Get-Location) $ClientExecutable
    $repositoryPath = Join-Path $repositoryRoot $ClientExecutable
    if (Test-Path -LiteralPath $workingDirectoryPath -PathType Leaf) {
        $ClientExecutable = $workingDirectoryPath
    } elseif (Test-Path -LiteralPath $repositoryPath -PathType Leaf) {
        $ClientExecutable = $repositoryPath
    }
}

if (-not (Test-Path -LiteralPath $ClientExecutable -PathType Leaf))
    { throw "The client executable was not found at $ClientExecutable." }
$ClientExecutable = (Resolve-Path -LiteralPath $ClientExecutable).Path

$serverProcess = $null
$startedServer = $false
try {
    if ($Legacy) {
        $legacyArguments = @('--game-mode', 'legacy') + $ClientArgument
        $clientProcess = Start-Process -FilePath $ClientExecutable -ArgumentList $legacyArguments -WorkingDirectory $repositoryRoot -PassThru -Wait
        exit $clientProcess.ExitCode
    }

    if ([string]::IsNullOrWhiteSpace($ServerEndpoint)) {
        if ([string]::IsNullOrWhiteSpace($ServerExecutable))
            { $ServerExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe' }
        if (-not (Test-Path -LiteralPath $ServerExecutable))
            { throw "The authoritative server executable was not found at $ServerExecutable. Build the server project first, or pass -ServerEndpoint for an already-running server." }

        $serverArguments = @(
            '--port', '0',
            '--protocol-version', $ProtocolVersion,
            '--save-root', (Join-Path $captureRoot 'save'))
        $serverProcess = Start-Process -FilePath $ServerExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $serverOutput -RedirectStandardError $serverError -PassThru
        $startedServer = $true

        $port = $null
        $detectedContentHash = $null
        $detectedRulesetHash = $null
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            Start-Sleep -Milliseconds 250
            if ($serverProcess.HasExited)
                { throw "The authoritative server exited during startup. See $serverError" }
            $output = Get-Content -LiteralPath $serverOutput -Raw -ErrorAction SilentlyContinue
            if ($null -eq $output)
                { $output = '' }
            $portMatch = [regex]::Match($output, 'listening on [^:]+:(\d+)')
            $contentMatch = [regex]::Match($output, 'Content manifest: ([0-9a-fA-F]+)')
            $rulesetMatch = [regex]::Match($output, 'Ruleset identity: ([0-9a-fA-F]+)')
            if ($portMatch.Success) { $port = [int]$portMatch.Groups[1].Value }
            if ($contentMatch.Success) { $detectedContentHash = $contentMatch.Groups[1].Value }
            if ($rulesetMatch.Success) { $detectedRulesetHash = $rulesetMatch.Groups[1].Value }
            if ($null -ne $port -and -not [string]::IsNullOrWhiteSpace($detectedContentHash) -and -not [string]::IsNullOrWhiteSpace($detectedRulesetHash))
                { break }
        }
        if ($null -eq $port -or [string]::IsNullOrWhiteSpace($detectedContentHash) -or [string]::IsNullOrWhiteSpace($detectedRulesetHash))
            { throw "Timed out waiting for authoritative server identity. See $serverOutput and $serverError" }
        $ServerEndpoint = "127.0.0.1:$port"

        if ([string]::IsNullOrWhiteSpace($DiagnosticsDirectory))
            { $DiagnosticsDirectory = Join-Path $captureRoot 'diagnostics' }
        $clientContentHash = if ($ProtocolVersion -eq '0.1.0') { $detectedRulesetHash } else { $detectedContentHash }
        $authoritativeArguments = @(
            '--game-mode', 'authoritative',
            '--authoritative-server', $ServerEndpoint,
            '--authoritative-protocol-version', $ProtocolVersion,
            '--authoritative-content-hash', $clientContentHash,
            '--authoritative-ruleset-hash', $detectedRulesetHash,
            '--authoritative-resume-token', $resumeTokenPath,
            '--authoritative-diagnostics', $DiagnosticsDirectory
        ) + $ClientArgument
        $clientProcess = Start-Process -FilePath $ClientExecutable -ArgumentList $authoritativeArguments -WorkingDirectory $repositoryRoot -PassThru -Wait
        exit $clientProcess.ExitCode
    }

    if ([string]::IsNullOrWhiteSpace($ContentHash) -or [string]::IsNullOrWhiteSpace($RulesetHash))
        { throw "An authoritative launch requires both -ContentHash and -RulesetHash when using -ServerEndpoint. ContentHash='$ContentHash'; RulesetHash='$RulesetHash'." }
    if ([string]::IsNullOrWhiteSpace($DiagnosticsDirectory))
        { $DiagnosticsDirectory = Join-Path $captureRoot 'diagnostics' }

    $authoritativeArguments = @(
        '--game-mode', 'authoritative',
        '--authoritative-server', $ServerEndpoint,
        '--authoritative-protocol-version', $ProtocolVersion,
        '--authoritative-content-hash', $ContentHash,
        '--authoritative-ruleset-hash', $RulesetHash,
        '--authoritative-resume-token', $resumeTokenPath,
        '--authoritative-diagnostics', $DiagnosticsDirectory
    ) + $ClientArgument
    $clientProcess = Start-Process -FilePath $ClientExecutable -ArgumentList $authoritativeArguments -WorkingDirectory $repositoryRoot -PassThru -Wait
    exit $clientProcess.ExitCode
}
finally {
    if ($startedServer -and $null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        $serverProcess.WaitForExit()
    }
    Write-Host "Authoritative launch logs: $captureRoot"
}
