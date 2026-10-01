<#
.SYNOPSIS
    Compares Cathedral level-1 fine geometry with a locally installed Diablo.
.DESCRIPTION
    Builds the native and managed oracle tests, retains private licensed inputs
    and comparisons in a fresh temporary directory, and rejects missing or
    skipped evidence. This is isolated verification, not a server MPQ importer.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

function Find-CathedralOracleBuild([string]$RepositoryRoot) {
    foreach ($relativePath in @('build/x64-Debug-authoritative-vcpkg', 'build/x64-Debug-authoritative', 'build/x64-Debug')) {
        $buildPath = Join-Path $RepositoryRoot $relativePath
        $cachePath = Join-Path $buildPath 'CMakeCache.txt'
        if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) { continue }
        $cache = [IO.File]::ReadAllText($cachePath)
        if ($cache -notmatch '(?m)^UNPACKED_MPQS:BOOL=OFF\r?$' -or
            $cache -notmatch '(?m)^BUILD_TESTING:BOOL=ON\r?$' -or
            $cache -notmatch '(?m)^CMAKE_BUILD_TYPE:STRING=Debug\r?$') { continue }
        $sourceMatch = [regex]::Match($cache, '(?m)^CMAKE_HOME_DIRECTORY:INTERNAL=([^\r\n]+)')
        $compilerMatch = [regex]::Match($cache, '(?m)^CMAKE_CXX_COMPILER:FILEPATH=([^\r\n]+)')
        if (-not $sourceMatch.Success -or -not $compilerMatch.Success) { continue }
        if (-not [IO.Path]::GetFullPath($sourceMatch.Groups[1].Value).Equals(
                [IO.Path]::GetFullPath($RepositoryRoot), [StringComparison]::OrdinalIgnoreCase)) { continue }
        $compilerPath = [IO.Path]::GetFullPath($compilerMatch.Groups[1].Value)
        if ([IO.Path]::GetFileName($compilerPath) -ne 'cl.exe') { continue }
        $vcOffset = $compilerPath.IndexOf('\VC\', [StringComparison]::OrdinalIgnoreCase)
        if ($vcOffset -lt 0) { continue }
        $vsRoot = $compilerPath.Substring(0, $vcOffset)
        $developerCommand = Join-Path $vsRoot 'Common7/Tools/VsDevCmd.bat'
        $cmakePath = Join-Path $vsRoot 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
        if (-not (Test-Path -LiteralPath $developerCommand -PathType Leaf) -or
            -not (Test-Path -LiteralPath $cmakePath -PathType Leaf)) { continue }
        return [pscustomobject]@{ Directory = $buildPath; DeveloperCommand = $developerCommand; CMake = $cmakePath }
    }
    throw 'Configure a packed-MPQ Debug x64 MSVC CMake tree with BUILD_TESTING=ON first; see docs/ai/msvc-build.md.'
}

function Assert-CathedralNativeReport([string]$ReportPath) {
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        throw 'The native oracle did not emit its test result.'
    }
    $report = [IO.File]::ReadAllText($ReportPath) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.errors -ne 0 -or
        $report.disabled -ne 0 -or $cases.Count -ne 1 -or
        $cases[0].classname -ne 'Drlg_l1' -or
        $cases[0].name -ne 'CathedralFineGeometryLocalAssetOracle' -or
        $cases[0].status -ne 'RUN' -or $cases[0].result -ne 'COMPLETED') {
        throw 'The required native oracle did not complete successfully; skipped tests are not evidence.'
    }
}

function Invoke-CathedralNativeOracle([string]$Executable, [string]$ReportPath, [string]$LogPath) {
    # Merge native stderr before Windows PowerShell can turn an informational
    # message into NativeCommandError. Exit and structured evidence still gate success.
    $command = '"{0}" --gtest_filter=Drlg_l1.CathedralFineGeometryLocalAssetOracle "--gtest_output=json:{1}" 2>&1' -f
        $Executable, $ReportPath
    & $env:ComSpec /d /c $command | Tee-Object -FilePath $LogPath | Out-Host
    return $LASTEXITCODE
}

function Assert-CathedralManagedReport([string]$LogPath) {
    $output = [IO.File]::ReadAllText($LogPath)
    $summaries = [regex]::Matches($output,
        '(?s)Test run summary: Passed!.*?total:\s*(\d+)\s+failed:\s*(\d+)\s+succeeded:\s*(\d+)\s+skipped:\s*(\d+)')
    if ([regex]::Matches($output, 'Test run summary:').Count -ne 1 -or
        $summaries.Count -ne 1 -or $summaries[0].Groups[1].Value -ne '12' -or
        $summaries[0].Groups[2].Value -ne '0' -or $summaries[0].Groups[3].Value -ne '12' -or
        $summaries[0].Groups[4].Value -ne '0') {
        throw 'All twelve managed native-geometry comparisons must pass without exclusions.'
    }
}

function Get-CathedralOracleArtifacts([string]$EvidenceRoot) {
    foreach ($name in @('l1.til', 'l1.sol', 'cathedral-2588-main.json', 'cathedral-2588-previous.json',
            'cathedral-743271966-main.json', 'cathedral-743271966-previous.json', 'cathedral-0-main.json',
            'cathedral-0-previous.json', 'cathedral-1-main.json', 'cathedral-1-previous.json',
            'cathedral-2147483648-main.json', 'cathedral-2147483648-previous.json',
            'cathedral-4294967295-main.json', 'cathedral-4294967295-previous.json')) {
        $path = Join-Path $EvidenceRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "The native oracle did not emit a nonempty $name."
        }
        $hasher = [Security.Cryptography.SHA256]::Create()
        $stream = $null
        try {
            $stream = [IO.File]::OpenRead($path)
            $hash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        } finally {
            if ($null -ne $stream) { $stream.Dispose() }
            $hasher.Dispose()
        }
        [pscustomobject]@{
            Name = $name
            Bytes = (Get-Item -LiteralPath $path).Length
            Sha256 = $hash
        }
    }
}

$nativeBuild = Find-CathedralOracleBuild $repositoryRoot
$evidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Cathedral-Oracle-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $evidenceRoot) { throw 'Oracle verification requires a fresh evidence directory.' }
$fullEvidenceRoot = [IO.Path]::GetFullPath($evidenceRoot)
if ($fullEvidenceRoot.StartsWith($repositoryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Licensed oracle inputs must remain outside the repository.'
}
New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
$artifactRoot = Join-Path $evidenceRoot 'geometry'
New-Item -ItemType Directory -Path $artifactRoot | Out-Null
$previousOracleDirectory = [Environment]::GetEnvironmentVariable('GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY', 'Process')
$record = [ordered]@{
    Passed = $false
    StartedUtc = [DateTime]::UtcNow.ToString('O')
    EndedUtc = $null
    NativeBuild = $nativeBuild.Directory
    ArtifactDirectory = $artifactRoot
    NativeExitCode = $null
    ManagedExitCode = $null
    Artifacts = @()
    Error = $null
}
$primaryError = $null
Push-Location $repositoryRoot
try {
    Write-Host "Private Cathedral oracle evidence: $evidenceRoot"
    Write-Host 'This directory contains locally licensed geometry inputs; do not publish it.'
    $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target level_l1_test --parallel 2' -f
        $nativeBuild.DeveloperCommand, $nativeBuild.CMake, $nativeBuild.Directory
    & $env:ComSpec /d /c $buildCommand 2>&1 | Tee-Object -FilePath (Join-Path $evidenceRoot 'native-build.log')
    if ($LASTEXITCODE -ne 0) { throw "The native oracle build failed with exit code $LASTEXITCODE." }
    & dotnet build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --configuration Debug --no-restore --nologo 2>&1 |
        Tee-Object -FilePath (Join-Path $evidenceRoot 'managed-build.log')
    if ($LASTEXITCODE -ne 0) { throw "The managed oracle build failed with exit code $LASTEXITCODE." }

    $env:GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY = $artifactRoot
    $nativeExecutable = Join-Path $nativeBuild.Directory 'level_l1_test.exe'
    $nativeReport = Join-Path $evidenceRoot 'native-tests.json'
    $record.NativeExitCode = Invoke-CathedralNativeOracle $nativeExecutable $nativeReport (Join-Path $evidenceRoot 'native-tests.log')
    if ($record.NativeExitCode -ne 0) { throw "The native oracle failed with exit code $($record.NativeExitCode)." }
    Assert-CathedralNativeReport $nativeReport
    $record.Artifacts = @(Get-CathedralOracleArtifacts $artifactRoot)

    $managedLog = Join-Path $evidenceRoot 'managed-tests.log'
    & server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe --no-ansi --filter-method 'Devilution.Diablo.Tests.AuthoritativeCathedralNativeGeometryOracleTests.NativeLocalAssetFineGeometryMatchesManagedMaterializer' --explicit only 2>&1 |
        Tee-Object -FilePath $managedLog
    $record.ManagedExitCode = $LASTEXITCODE
    if ($record.ManagedExitCode -ne 0) { throw "The managed oracle failed with exit code $($record.ManagedExitCode)." }
    Assert-CathedralManagedReport $managedLog
    $record.Passed = $true
} catch {
    $primaryError = $_
    $record.Error = $_.Exception.Message
} finally {
    [Environment]::SetEnvironmentVariable('GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY', $previousOracleDirectory, 'Process')
    Pop-Location
    $record.EndedUtc = [DateTime]::UtcNow.ToString('O')
}
try {
    [IO.File]::WriteAllText((Join-Path $evidenceRoot 'result.json'), ($record | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
} catch {
    if ($null -eq $primaryError) { throw }
    Write-Warning "Could not retain the result record: $($_.Exception.Message)"
}
if ($null -ne $primaryError) { throw $primaryError }
Write-Host "PASS: twelve native/managed Cathedral fine-coordinate comparisons. Evidence: $evidenceRoot"
