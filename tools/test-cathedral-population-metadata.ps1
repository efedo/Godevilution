<#
    Compiles complete private metadata twice and compares reloaded packs to
    completed native evidence. No native recapture, client or runtime activation.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$FullPopulationEvidenceDirectory,
    [Parameter(Mandatory = $true)][string]$GeometryDirectory
)

$ErrorActionPreference = 'Stop'
$metadataRepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
# Import declarations only; never execute any shared runner's body.
foreach ($metadataImport in @(
        @{ File = 'test-cathedral-geometry.ps1'; Names = @('Quote-CathedralNativeArgument',
            'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
            'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
        @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
        @{ File = 'test-theme-room-generation.ps1'; Names = @('Get-ThemeRoomArtifact') },
        @{ File = 'test-cathedral-full-population.ps1'; Names = @('Assert-CathedralFullExternalPath',
            'Assert-CathedralFullNativeReport', 'Assert-CathedralFullManagedReport', 'Assert-CathedralFullRepeatability') })) {
    $metadataTokens = $null; $metadataErrors = $null
    $metadataAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $metadataImport.File), [ref]$metadataTokens, [ref]$metadataErrors)
    if ($metadataErrors.Count) { throw 'Shared metadata proof declarations did not parse.' }
    foreach ($metadataName in $metadataImport.Names) {
        $metadataDeclarations = @($metadataAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $metadataName
        }, $true))
        if ($metadataDeclarations.Count -ne 1) { throw "Expected one declaration for $metadataName." }
        . ([scriptblock]::Create($metadataDeclarations[0].Extent.Text))
    }
}

function Assert-CathedralMetadataManagedReport([string]$Path, [string]$Method) {
    if ($Method -cnotin @('ExportCompleteMetadataInputs', 'CompiledMetadataPopulationMatchesNative')) {
        throw 'Unsupported metadata proof method.'
    }
    [xml]$report = [IO.File]::ReadAllText($Path)
    $cases = @($report.SelectNodes('//test')); $assemblies = @($report.SelectNodes('//assembly'))
    $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralFullPopulationNativeOracleTests'
    $names = @($type + '.' + $Method)
    if ($Method -ceq 'CompiledMetadataPopulationMatchesNative') {
        $names = @(foreach ($depth in 1..4) { foreach ($seed in @(2588, 743271966)) {
            foreach ($entry in @('main', 'previous')) {
                $type + '.' + $Method + '(depth: ' + $depth + ', seed: ' + $seed + ', entry: \"' + $entry + '\")'
            }
        } })
    }
    if ($assemblies.Count -ne 1 -or $assemblies[0].total -ne $names.Count -or
        $assemblies[0].passed -ne $names.Count -or $assemblies[0].failed -ne 0 -or
        $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or $assemblies[0].'not-run' -ne 0 -or
        @($report.SelectNodes('//errors/error')).Count -ne 0 -or $cases.Count -ne $names.Count -or
        @($cases | Where-Object { $_.result -cne 'Pass' -or $_.type -cne $type -or $_.method -cne $Method }).Count -ne 0 -or
        @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'Every exact metadata proof case must pass without replacements or exclusions.'
    }
}

function Assert-CathedralMetadataFileRecord([string]$Path, [long]$Length, [string]$Sha256) {
    Assert-CathedralMetadataRegularPath $Path
    $file = Get-Item -LiteralPath $Path -Force
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $Length -le 0 -or $Length -gt 128MB -or $file.Length -ne $Length -or
        $Sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or (Get-CathedralMetadataFileHash $Path) -ine $Sha256) {
        throw 'Retained metadata proof reference bytes do not match completed evidence.'
    }
}

function Get-CathedralMetadataFileHash([string]$Path) {
    Assert-CathedralMetadataRegularPath $Path
    $stream = $null; $sha = $null
    try {
        $stream = [IO.File]::OpenRead($Path)
        if ($stream.Length -le 0 -or $stream.Length -gt 128MB) { throw 'Metadata proof file must be nonempty and bounded.' }
        $sha = [Security.Cryptography.SHA256]::Create()
        return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
    } finally {
        if ($null -ne $sha) { $sha.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Assert-CathedralMetadataRegularPath([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and
            ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Metadata proof paths may not traverse reparse points.'
        }
        $parent = [IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
}

function Get-CathedralMetadataPackFiles([string]$Directory) {
    Assert-CathedralMetadataRegularPath $Directory
    $files = @(Get-ChildItem -LiteralPath $Directory -Force | Sort-Object Name -CaseSensitive)
    if ($files.Count -ne 20 -or @($files | Where-Object { $_.PSIsContainer -or
            ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $_.Length -le 0 }).Count) {
        throw 'Published metadata must contain nineteen tables and one regular manifest.'
    }
    return @($files | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Length = $_.Length
        Sha256 = (Get-CathedralMetadataFileHash $_.FullName) } })
}

function Get-CathedralMetadataCompilerReceipt([string]$Output) {
    $matches = [regex]::Matches($Output, '(?m)^Metadata: ([0-9A-Fa-f]{64}); manifest: ([0-9A-Fa-f]{64})\r?$')
    if ($matches.Count -ne 1) { throw 'A single metadata compiler receipt is required.' }
    return [pscustomobject]@{ MetadataFingerprint = $matches[0].Groups[1].Value; ManifestHash = $matches[0].Groups[2].Value }
}

# Require completed clean provenance before creating any new evidence or child.
Assert-CathedralFullExternalPath $FullPopulationEvidenceDirectory $metadataRepositoryRoot
$metadataReference = [IO.File]::ReadAllText((Join-Path $FullPopulationEvidenceDirectory 'result.json')) | ConvertFrom-Json
if ($metadataReference.Passed -ne $true -or $metadataReference.Error -or $metadataReference.EvidenceError -or
    $metadataReference.Scope -cne 'controlled-original-diablo-cathedral-sp-normal-post-createlevel-through-createthemerooms' -or
    @($metadataReference.Stages).Count -ne 5 -or @($metadataReference.Artifacts).Count -ne 2) {
    throw 'Completed controlled full-population reference evidence is required.'
}
foreach ($metadataStage in $metadataReference.Stages) { Assert-ThemeOwnedStage $metadataStage 'Retained native reference' }
foreach ($metadataIndex in 1..2) {
    $metadataCapture = Join-Path $FullPopulationEvidenceDirectory "capture-$metadataIndex"
    Assert-CathedralFullExternalPath $metadataCapture $metadataRepositoryRoot
    $metadataArtifact = Get-ThemeRoomArtifact $metadataCapture 'native-full-population.json' 128MB
    $metadataRecorded = $metadataReference.Artifacts[$metadataIndex - 1]
    Assert-CathedralMetadataFileRecord $metadataArtifact.Path $metadataRecorded.Length $metadataRecorded.Sha256
    Assert-CathedralFullNativeReport (Join-Path $FullPopulationEvidenceDirectory "native-tests-$metadataIndex.json")
}
Assert-CathedralFullRepeatability $metadataReference.Artifacts[0] $metadataReference.Artifacts[1]
Assert-CathedralFullManagedReport (Join-Path $FullPopulationEvidenceDirectory 'managed-tests.xml')
Assert-CathedralFullExternalPath $metadataReference.UpstreamEvidence $metadataRepositoryRoot
$metadataUpstream = [IO.File]::ReadAllText((Join-Path $metadataReference.UpstreamEvidence 'result.json')) | ConvertFrom-Json
if ($metadataUpstream.Passed -ne $true -or $metadataUpstream.Error -or $metadataUpstream.CleanupError -or
    $metadataUpstream.Deterministic -ne $true -or $metadataUpstream.Scope -cne 'native-cathedral-l1-composed-inititems' -or
    @($metadataUpstream.Captures).Count -ne 2) { throw 'Completed upstream raw population inputs are required.' }
foreach ($metadataCapture in $metadataUpstream.Captures) {
    Assert-ThemeOwnedStage ([pscustomobject]@{ Pid = $metadataCapture.NativePid; ExitCode = $metadataCapture.NativeExitCode
        TimedOut = $metadataCapture.NativeTimedOut; ConfirmedExited = $metadataCapture.NativeConfirmedExited
        LogRetained = $metadataCapture.NativeLogRetained; Error = $metadataCapture.NativeError
        CleanupError = $metadataCapture.NativeCleanupError }) 'Retained upstream capture'
    if (@($metadataCapture.Artifacts).Count -ne 17) { throw 'Complete raw upstream artifact inventory is required.' }
    foreach ($metadataField in @('ArtifactDirectory', 'InputDirectory', 'EncounterDirectory')) {
        Assert-CathedralFullExternalPath $metadataCapture.$metadataField $metadataRepositoryRoot
    }
    foreach ($metadataArtifact in $metadataCapture.Artifacts) {
        Assert-CathedralMetadataFileRecord (Join-Path $metadataCapture.ArtifactDirectory $metadataArtifact.Name) `
            $metadataArtifact.Bytes $metadataArtifact.Sha256
    }
    foreach ($metadataPair in @(@('InputDirectory', 'InputArtifact'), @('EncounterDirectory', 'EncounterArtifact'))) {
        $metadataArtifact = $metadataCapture.($metadataPair[1])
        Assert-CathedralMetadataFileRecord (Join-Path $metadataCapture.($metadataPair[0]) $metadataArtifact.Name) `
            $metadataArtifact.Bytes $metadataArtifact.Sha256
    }
}
# Select only the two explicit geometry files from an existing diagnostic pack.
$metadataGeometrySource = [IO.Path]::GetFullPath($GeometryDirectory)
Assert-CathedralMetadataRegularPath $metadataGeometrySource
$metadataGeometryNames = @('native_dun_piece_definitions.tsv', 'native_dun_tile_properties.tsv')
foreach ($metadataName in $metadataGeometryNames) {
    $metadataFile = Get-Item -LiteralPath (Join-Path $metadataGeometrySource $metadataName) -Force
    if ($metadataFile.PSIsContainer -or ($metadataFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Explicit geometry source files must be regular files.'
    }
}

$metadataEvidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Population-Metadata-' + [guid]::NewGuid().ToString('N'))
Assert-CathedralFullExternalPath $metadataEvidenceRoot $metadataRepositoryRoot
if (Test-Path -LiteralPath $metadataEvidenceRoot) { throw 'Metadata proof requires fresh evidence.' }
New-Item -ItemType Directory -Path $metadataEvidenceRoot | Out-Null
$metadataRecord = [ordered]@{ Passed = $false; Scope = 'compiled-controlled-cathedral-population-metadata'
    StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    NativeReference = [IO.Path]::GetFullPath($FullPopulationEvidenceDirectory); Stages = @(); PackFiles = @()
    MetadataFingerprint = $null; ManifestHash = $null; Error = $null; EvidenceError = $null }
$metadataVariables = @('GODEVILUTION_CATHEDRAL_FULL_POPULATION_ORACLE_DIRECTORY',
    'GODEVILUTION_CATHEDRAL_POPULATION_ORACLE_DIRECTORY', 'GODEVILUTION_CATHEDRAL_POPULATION_INPUT_DIRECTORY',
    'GODEVILUTION_CATHEDRAL_ENCOUNTER_ORACLE_DIRECTORY', 'GODEVILUTION_CATHEDRAL_METADATA_GEOMETRY_DIRECTORY',
    'GODEVILUTION_CATHEDRAL_METADATA_SOURCE_DIRECTORY', 'GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY')
$metadataPrevious = @{}
foreach ($metadataVariable in $metadataVariables) { $metadataPrevious[$metadataVariable] = [Environment]::GetEnvironmentVariable($metadataVariable, 'Process') }
$metadataPrimaryError = $null
Push-Location $metadataRepositoryRoot
try {
    Write-Host "Private metadata evidence: $metadataEvidenceRoot"
    $metadataGeometry = Join-Path $metadataEvidenceRoot 'geometry'
    $metadataSource = Join-Path $metadataEvidenceRoot 'source'
    New-Item -ItemType Directory -Path $metadataGeometry, $metadataSource | Out-Null
    foreach ($metadataName in $metadataGeometryNames) { Copy-Item -LiteralPath (Join-Path $metadataGeometrySource $metadataName) -Destination $metadataGeometry }
    $metadataValues = @((Join-Path $FullPopulationEvidenceDirectory 'capture-1'),
        $metadataUpstream.Captures[0].ArtifactDirectory, $metadataUpstream.Captures[0].InputDirectory,
        $metadataUpstream.Captures[0].EncounterDirectory, $metadataGeometry, $metadataSource,
        (Join-Path $metadataEvidenceRoot 'pack-1'))
    for ($metadataIndex = 0; $metadataIndex -lt $metadataVariables.Count; $metadataIndex++) {
        [Environment]::SetEnvironmentVariable($metadataVariables[$metadataIndex], $metadataValues[$metadataIndex], 'Process')
    }
    $metadataDotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
    $metadataOutcome = Invoke-CathedralPopulationOwnedProcess $metadataDotnet `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --no-restore --disable-build-servers -v minimal' `
        (Join-Path $metadataEvidenceRoot 'managed-build.log') 180000
    $metadataRecord.Stages += $metadataOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $metadataOutcome 'Metadata module build'
    $metadataTestExecutable = Join-Path $metadataRepositoryRoot 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe'
    $metadataOutcome = Invoke-CathedralPopulationOwnedProcess $metadataTestExecutable `
        ('--no-ansi --parallel none --filter-method "*AuthoritativeCathedralFullPopulationNativeOracleTests.ExportCompleteMetadataInputs" --explicit only --fail-skips on --report-xunit --results-directory ' +
            (Quote-CathedralNativeArgument $metadataEvidenceRoot) + ' --report-xunit-filename export-tests.xml') `
        (Join-Path $metadataEvidenceRoot 'export-tests.log') 120000
    $metadataRecord.Stages += $metadataOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $metadataOutcome 'Raw metadata export'
    Assert-CathedralMetadataManagedReport (Join-Path $metadataEvidenceRoot 'export-tests.xml') 'ExportCompleteMetadataInputs'
    foreach ($metadataIndex in 1..2) {
        $metadataPack = Join-Path $metadataEvidenceRoot "pack-$metadataIndex"
        $metadataOutcome = Invoke-CathedralPopulationOwnedProcess `
            (Join-Path $metadataRepositoryRoot 'tools/Cathedral.Playtest/bin/Debug/net8.0/Cathedral.Playtest.exe') `
            ('--compile-population-metadata --metadata-directory ' + (Quote-CathedralNativeArgument $metadataSource) +
                ' --output-directory ' + (Quote-CathedralNativeArgument $metadataPack)) `
            (Join-Path $metadataEvidenceRoot "compile-$metadataIndex.log") 30000
        $metadataRecord.Stages += $metadataOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $metadataOutcome 'Offline complete metadata compiler'
        $metadataReceipt = Get-CathedralMetadataCompilerReceipt $metadataOutcome.Output
        $metadataFiles = @(Get-CathedralMetadataPackFiles $metadataPack)
        if ($metadataIndex -eq 1) {
            $metadataRecord.PackFiles = $metadataFiles
            $metadataRecord.MetadataFingerprint = $metadataReceipt.MetadataFingerprint
            $metadataRecord.ManifestHash = $metadataReceipt.ManifestHash
        } elseif (($metadataFiles | ConvertTo-Json -Depth 3 -Compress) -cne ($metadataRecord.PackFiles | ConvertTo-Json -Depth 3 -Compress) -or
            $metadataRecord.MetadataFingerprint -cne $metadataReceipt.MetadataFingerprint -or
            $metadataRecord.ManifestHash -cne $metadataReceipt.ManifestHash) { throw 'Repeated metadata publication changed bytes or identity.' }
    }
    foreach ($metadataMethod in @('CompiledMetadataPopulationMatchesNative', 'FullPopulationMatchesNative')) {
        $metadataOutcome = Invoke-CathedralPopulationOwnedProcess $metadataTestExecutable `
            ('--no-ansi --parallel none --filter-method "*AuthoritativeCathedralFullPopulationNativeOracleTests.' + $metadataMethod + '*" --explicit only --fail-skips on --report-xunit --results-directory ' +
                (Quote-CathedralNativeArgument $metadataEvidenceRoot) + ' --report-xunit-filename ' + $metadataMethod + '.xml') `
            (Join-Path $metadataEvidenceRoot ($metadataMethod + '.log')) 180000
        $metadataRecord.Stages += $metadataOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $metadataOutcome $metadataMethod
        if ($metadataMethod -ceq 'FullPopulationMatchesNative') {
            Assert-CathedralFullManagedReport (Join-Path $metadataEvidenceRoot ($metadataMethod + '.xml'))
        } else { Assert-CathedralMetadataManagedReport (Join-Path $metadataEvidenceRoot ($metadataMethod + '.xml')) $metadataMethod }
    }
    $metadataRecord.Passed = $true
} catch { $metadataPrimaryError = $_; $metadataRecord.Error = $_.Exception.Message }
finally {
    foreach ($metadataVariable in $metadataVariables) { Restore-CathedralOracleEnvironmentVariable $metadataVariable $metadataPrevious[$metadataVariable] }
    Pop-Location; $metadataRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try { [IO.File]::WriteAllText((Join-Path $metadataEvidenceRoot 'result.json'), ($metadataRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
    catch { $metadataRecord.EvidenceError = $_.Exception.Message; if ($null -eq $metadataPrimaryError) { $metadataPrimaryError = $_ } }
}
if ($null -ne $metadataPrimaryError) { throw $metadataPrimaryError }
Write-Host "PASS: byte-identical complete metadata packs, sixteen reloaded comparisons and sixteen legacy comparisons. Evidence: $metadataEvidenceRoot"
