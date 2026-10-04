<#
.SYNOPSIS
    Compares Cathedral level-1 fine geometry with a locally installed Diablo.
.DESCRIPTION
    Builds the native and managed oracle tests, retains private licensed inputs
    and comparisons in a fresh temporary directory, and rejects missing or
    skipped evidence. This is isolated verification, not a server MPQ importer.
    -CathedralDepths compares ordinary quest-disabled original-Diablo depths
    1-4; the zero-argument level-1 contract remains unchanged.
    -CathedralDoors captures the native-only Cathedral door oracle and repeats
    its complete frozen artifact corpus in a fresh process.
    -CathedralPopulation captures the native initial object phase, reserved
    monsters, bestiary, player context, and RNG boundaries without managed parity.
    -CathedralPopulationParity additionally captures independent phase inputs
    and compares the managed initial-object algorithm with all sixteen cases.
#>
[CmdletBinding()]
param(
    [switch]$CathedralDepths,
    [switch]$CathedralDoors,
    [switch]$CathedralPopulation,
    [switch]$CathedralPopulationParity)

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

function Assert-CathedralNativeReport(
    [string]$ReportPath,
    [ValidateSet('CathedralFineGeometryLocalAssetOracle', 'CathedralDepthFineGeometryLocalAssetOracle')]
    [string]$TestName = 'CathedralFineGeometryLocalAssetOracle') {
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        throw 'The native oracle did not emit its test result.'
    }
    $report = [IO.File]::ReadAllText($ReportPath) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.errors -ne 0 -or
        $report.disabled -ne 0 -or $cases.Count -ne 1 -or
        $cases[0].classname -ne 'Drlg_l1' -or
        $cases[0].name -ne $TestName -or
        $cases[0].status -ne 'RUN' -or $cases[0].result -ne 'COMPLETED') {
        throw 'The required native oracle did not complete successfully; skipped tests are not evidence.'
    }
}

function Invoke-CathedralNativeOracle(
    [string]$Executable, [string]$ReportPath, [string]$LogPath,
    [ValidateSet('CathedralFineGeometryLocalAssetOracle', 'CathedralDepthFineGeometryLocalAssetOracle',
        'CathedralDoorNativeObjectOracle', 'CathedralDoorNativeDeltaOpenOracle')]
    [string]$TestName = 'CathedralFineGeometryLocalAssetOracle') {
    # Merge native stderr before Windows PowerShell can turn an informational
    # message into NativeCommandError. Exit and structured evidence still gate success.
    $command = '"{0}" --gtest_filter=Drlg_l1.{2} "--gtest_output=json:{1}" 2>&1' -f
        $Executable, $ReportPath, $TestName
    & $env:ComSpec /d /c $command | Tee-Object -FilePath $LogPath | Out-Host
    return $LASTEXITCODE
}

function Assert-CathedralManagedReport([string]$LogPath, [ValidateRange(1, 48)] [int]$ExpectedComparisons = 12) {
    $output = [IO.File]::ReadAllText($LogPath)
    $summaries = [regex]::Matches($output,
        '(?s)Test run summary: Passed!.*?total:\s*(\d+)\s+failed:\s*(\d+)\s+succeeded:\s*(\d+)\s+skipped:\s*(\d+)')
    if ([regex]::Matches($output, 'Test run summary:').Count -ne 1 -or
        $summaries.Count -ne 1 -or [int]$summaries[0].Groups[1].Value -ne $ExpectedComparisons -or
        $summaries[0].Groups[2].Value -ne '0' -or [int]$summaries[0].Groups[3].Value -ne $ExpectedComparisons -or
        $summaries[0].Groups[4].Value -ne '0') {
        throw "All $ExpectedComparisons managed native-geometry comparisons must pass without exclusions."
    }
}

function Get-CathedralOracleArtifactNames([switch]$CathedralDepths) {
    'l1.til'
    'l1.sol'
    $depths = if ($CathedralDepths) { 1..4 } else { @(1) }
    foreach ($depth in $depths) {
        foreach ($seed in @('2588', '743271966', '0', '1', '2147483648', '4294967295')) {
            foreach ($entry in @('main', 'previous')) {
                if ($CathedralDepths) { "cathedral-depth-$depth-$seed-$entry.json" }
                else { "cathedral-$seed-$entry.json" }
            }
        }
    }
}

function Get-CathedralOracleArtifacts([string]$EvidenceRoot, [switch]$CathedralDepths) {
    foreach ($name in (Get-CathedralOracleArtifactNames -CathedralDepths:$CathedralDepths)) {
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

function Assert-CathedralRepeatArtifacts(
    [object[]]$First,
    [object[]]$Repeated,
    [ValidateRange(1, 1000)] [int]$ExpectedCount) {
    if ($First.Count -ne $ExpectedCount -or $Repeated.Count -ne $ExpectedCount) {
        throw 'Both native captures must contain the complete required artifact set.'
    }
    $repeatedByName = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($artifact in $Repeated) {
        $name = [string]$artifact.Name
        if ([string]::IsNullOrWhiteSpace($name) -or $artifact.Bytes -le 0 -or
            $artifact.Sha256 -cnotmatch '^[0-9a-f]{64}$' -or $repeatedByName.ContainsKey($name)) {
            throw 'Repeated capture contains invalid or duplicate artifact evidence.'
        }
        $repeatedByName.Add($name, $artifact)
    }
    $firstNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($artifact in $First) {
        $name = [string]$artifact.Name
        if ([string]::IsNullOrWhiteSpace($name) -or $artifact.Bytes -le 0 -or
            $artifact.Sha256 -cnotmatch '^[0-9a-f]{64}$' -or -not $firstNames.Add($name) -or
            -not $repeatedByName.ContainsKey($name)) {
            throw 'First capture contains invalid, duplicate, or unmatched artifact evidence.'
        }
        $matchingArtifact = $repeatedByName[$name]
        if ($artifact.Bytes -ne $matchingArtifact.Bytes -or
            -not [string]::Equals($artifact.Sha256, $matchingArtifact.Sha256, [StringComparison]::Ordinal)) {
            throw "Native captures differ for $name."
        }
    }
}

function Get-CathedralDoorOracleArtifactNames {
    $names = @('l1.til', 'l1.sol', 'door-oracle-format.json')
    foreach ($depth in 1..4) {
        foreach ($seed in @('2588', '743271966')) {
            foreach ($entry in @('main', 'previous')) {
                $names += "cathedral-door-depth-$depth-$seed-$entry.json"
            }
        }
    }
    $names += 'cathedral-door-delta-depth-2-743271966-main.json'
    return $names
}

function Assert-CathedralDoorRequiredProperty($Object, [string]$Name) {
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) {
        throw "Cathedral door oracle JSON is missing $Name."
    }
}

function Assert-CathedralDoorInteger($Value, [decimal]$Minimum, [decimal]$Maximum, [string]$Scope) {
    if ($null -eq $Value) {
        throw "$Scope is not a native integer."
    }
    $type = $Value.GetType()
    if ($type -ne [byte] -and $type -ne [sbyte] -and $type -ne [int16] -and $type -ne [uint16] -and
        $type -ne [int32] -and $type -ne [uint32] -and $type -ne [int64] -and $type -ne [uint64] -and
        $type -ne [decimal]) {
        throw "$Scope is not a native integer."
    }
    $number = [decimal]$Value
    if ($number -ne [decimal]::Truncate($number) -or $number -lt $Minimum -or $number -gt $Maximum) {
        throw "$Scope is outside its native integer range."
    }
}

function Assert-CathedralDoorLayer($Layer, [string]$Scope) {
    $expectedCellCount = 112 * 112
    foreach ($field in @('pieces', 'special', 'transparency', 'properties', 'solid', 'missileBlocked', 'blockLight', 'walkable', 'itemBlocking')) {
        Assert-CathedralDoorRequiredProperty $Layer $field
        $values = @($Layer.$field)
        if ($values.Count -ne $expectedCellCount) {
            throw "$Scope.$field must contain exactly $expectedCellCount cells."
        }
        $maximum = if ($field -eq 'pieces') { 65535 } elseif ($field -in @('special', 'transparency', 'properties')) { 255 } else { 1 }
        # Inline the hot loop: millions of scalar function calls/temporary type
        # arrays otherwise dominate a complete two-capture validation.
        foreach ($value in $values) {
            if ($null -eq $value) { throw "$Scope.$field contains a null value." }
            $typeCode = [int][Type]::GetTypeCode($value.GetType())
            # Native JSON emits integer literals. Reject floating types before
            # conversion can round a near-integer or tiny fraction to an integer.
            if ($typeCode -lt 5 -or ($typeCode -gt 12 -and $typeCode -ne 15)) {
                throw "$Scope.$field contains a noninteger value."
            }
            $number = [decimal]$value
            if ($number -ne [decimal]::Truncate($number) -or $number -lt 0 -or $number -gt $maximum) {
                throw "$Scope.$field contains a value outside its native integer range."
            }
        }
    }
}

function Assert-CathedralDoorLayerHashes($Hashes, [string]$Scope) {
    foreach ($field in @('fullLayer', 'pieces', 'special', 'transparency', 'properties', 'solid', 'missileBlocked', 'blockLight', 'walkable', 'itemBlocking')) {
        Assert-CathedralDoorRequiredProperty $Hashes $field
        if ([string]$Hashes.$field -cnotmatch '^0x[0-9a-f]{16}$') {
            throw "$Scope.$field is not a native 64-bit hexadecimal hash."
        }
    }
}

function Assert-CathedralDoorTile($Tile, [string]$Scope) {
    foreach ($field in @('piece', 'special', 'transparency', 'properties', 'solid', 'missileBlocked', 'blockLight', 'walkable', 'itemBlocking')) {
        Assert-CathedralDoorRequiredProperty $Tile $field
        $maximum = if ($field -eq 'piece') { 65535 } elseif ($field -in @('special', 'transparency', 'properties')) { 255 } else { 1 }
        Assert-CathedralDoorInteger $Tile.$field 0 $maximum "$Scope.$field"
    }
}

function Assert-CathedralDoorObjectState($Object, [string]$Scope) {
    foreach ($field in @('state', 'var1OneBased', 'var2OneBased', 'animationFrame', 'selectionRegion', 'doorFlag', 'preFlag', 'missFlag', 'solidFlag')) {
        Assert-CathedralDoorRequiredProperty $Object $field
        if ($null -eq $Object.$field) { throw "$Scope.$field is null." }
    }
    Assert-CathedralDoorInteger $Object.state 0 1 "$Scope.state"
    Assert-CathedralDoorInteger $Object.var1OneBased 0 65535 "$Scope.var1OneBased"
    Assert-CathedralDoorInteger $Object.var2OneBased 0 65535 "$Scope.var2OneBased"
    Assert-CathedralDoorInteger $Object.animationFrame 0 255 "$Scope.animationFrame"
    Assert-CathedralDoorInteger $Object.selectionRegion 0 255 "$Scope.selectionRegion"
    foreach ($field in @('doorFlag', 'preFlag', 'missFlag', 'solidFlag')) {
        if ($Object.$field -isnot [bool]) { throw "$Scope.$field is not a native boolean." }
    }
}

function Assert-CathedralDoorOracleCase(
    [string]$Path,
    [string]$ExpectedFileName,
    [ValidateSet('sync', 'delta')] [string]$ExpectedOpenPath) {
    try { $case = [IO.File]::ReadAllText($Path) | ConvertFrom-Json }
    catch { throw "The Cathedral door oracle case $ExpectedFileName is not valid JSON: $($_.Exception.Message)" }
    foreach ($field in @('format', 'version', 'policy', 'level', 'entry', 'inputSeed', 'attemptSeed',
            'finalRandomState', 'doorScanRandomStateBefore', 'doorScanRandomStateAfter', 'nativeWidth',
            'nativeHeight', 'rowMajor', 'baselineRebuildCount', 'closedBaselineHashes', 'closedBaseline',
            'closedBaselines', 'doors')) {
        Assert-CathedralDoorRequiredProperty $case $field
    }
    Assert-CathedralDoorInteger $case.version 1 1 "$ExpectedFileName.version"
    Assert-CathedralDoorInteger $case.nativeWidth 112 112 "$ExpectedFileName.nativeWidth"
    Assert-CathedralDoorInteger $case.nativeHeight 112 112 "$ExpectedFileName.nativeHeight"
    if ([string]$case.format -cne 'godevilution-cathedral-l1-door-oracle' -or $case.version -ne 1 -or
        [string]$case.policy -cne 'native-add-l1-objects-door-only' -or $case.nativeWidth -ne 112 -or
        $case.nativeHeight -ne 112 -or [string]$case.rowMajor -cne 'y*nativeWidth+x') {
        throw "The Cathedral door oracle case $ExpectedFileName has the wrong native format or scope."
    }
    $nameMatch = [regex]::Match($ExpectedFileName, '^cathedral-door-depth-(1|2|3|4)-(2588|743271966)-(main|previous)\.json$')
    $isDelta = $ExpectedFileName -eq 'cathedral-door-delta-depth-2-743271966-main.json'
    if (-not $nameMatch.Success -and -not $isDelta) { throw "Unknown Cathedral door artifact $ExpectedFileName." }
    if ($isDelta) {
        if ($case.level -ne 2 -or $case.inputSeed -ne 743271966 -or [string]$case.entry -cne 'main') {
            throw 'The Cathedral delta artifact has the wrong depth, seed, or entry.'
        }
    } else {
        $expectedDepth = [Convert]::ToInt32($nameMatch.Groups[1].Value)
        $expectedSeed = [Convert]::ToUInt32($nameMatch.Groups[2].Value)
        if ($case.level -ne $expectedDepth -or $case.inputSeed -ne $expectedSeed -or
            [string]$case.entry -cne $nameMatch.Groups[3].Value) {
            throw "The Cathedral door artifact $ExpectedFileName has mismatched case identity."
        }
    }
    Assert-CathedralDoorInteger $case.level 1 4 "$ExpectedFileName.level"
    Assert-CathedralDoorInteger $case.inputSeed 0 4294967295 "$ExpectedFileName.inputSeed"
    foreach ($field in @('attemptSeed', 'finalRandomState', 'doorScanRandomStateBefore', 'doorScanRandomStateAfter')) {
        Assert-CathedralDoorInteger $case.$field 0 4294967295 "$ExpectedFileName.$field"
    }
    Assert-CathedralDoorInteger $case.baselineRebuildCount 1 65535 "$ExpectedFileName.baselineRebuildCount"
    if ($case.baselineRebuildCount -lt 1) { throw "$ExpectedFileName has no fresh baseline." }
    Assert-CathedralDoorLayerHashes $case.closedBaselineHashes "$ExpectedFileName.closedBaselineHashes"
    Assert-CathedralDoorLayer $case.closedBaseline "$ExpectedFileName.closedBaseline"
    $baselines = @($case.closedBaselines)
    if ($baselines.Count -ne $case.baselineRebuildCount) { throw "$ExpectedFileName has an incomplete baseline list." }
    for ($index = 0; $index -lt $baselines.Count; $index++) {
        $baseline = $baselines[$index]
        foreach ($field in @('ordinal', 'doorScanRandomStateBefore', 'doorScanRandomStateAfter', 'hashes', 'layer')) {
            Assert-CathedralDoorRequiredProperty $baseline $field
        }
        if ($baseline.ordinal -ne $index + 1) { throw "$ExpectedFileName has non-sequential baseline ordinals." }
        Assert-CathedralDoorInteger $baseline.ordinal 1 65535 "$ExpectedFileName.closedBaselines[$index].ordinal"
        Assert-CathedralDoorInteger $baseline.doorScanRandomStateBefore 0 4294967295 "$ExpectedFileName.closedBaselines[$index].doorScanRandomStateBefore"
        Assert-CathedralDoorInteger $baseline.doorScanRandomStateAfter 0 4294967295 "$ExpectedFileName.closedBaselines[$index].doorScanRandomStateAfter"
        Assert-CathedralDoorLayerHashes $baseline.hashes "$ExpectedFileName.closedBaselines[$index].hashes"
        Assert-CathedralDoorLayer $baseline.layer "$ExpectedFileName.closedBaselines[$index].layer"
        if ($index -eq 0) {
            foreach ($field in @('pieces', 'special', 'transparency', 'properties', 'solid', 'missileBlocked', 'blockLight', 'walkable', 'itemBlocking')) {
                if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals(
                        @($case.closedBaseline.$field), @($baseline.layer.$field))) {
                    throw "$ExpectedFileName has inconsistent initial baseline arrays."
                }
            }
        }
    }
    foreach ($field in @('fullLayer', 'pieces', 'special', 'transparency', 'properties', 'solid', 'missileBlocked', 'blockLight', 'walkable', 'itemBlocking')) {
        if ($case.closedBaselineHashes.$field -cne $baselines[0].hashes.$field) {
            throw "$ExpectedFileName has inconsistent initial baseline hashes."
        }
    }
    if ($case.doorScanRandomStateBefore -ne $baselines[0].doorScanRandomStateBefore -or
        $case.doorScanRandomStateAfter -ne $baselines[0].doorScanRandomStateAfter) {
        throw "$ExpectedFileName has inconsistent initial scanner RNG."
    }
    $doors = @($case.doors)
    if ($doors.Count -eq 0) { throw "$ExpectedFileName contains no native doors." }
    $meaningfulTransition = $false
    for ($index = 0; $index -lt $doors.Count; $index++) {
        $door = $doors[$index]
        foreach ($field in @('ordinal', 'freshBaselineOrdinal', 'openPath', 'nativeTypeId', 'nativeType', 'position',
                'var1OneBased', 'var2OneBased', 'sourcePieceObserved', 'neighborPieceObserved', 'closedObject',
                'openObject', 'reclosedObject', 'closedLayerHash', 'openLayerHash', 'reclosedLayerHash',
                'reclosedLayerEqualsClosed', 'reclosedObjectEqualsClosed', 'changedCells')) {
            Assert-CathedralDoorRequiredProperty $door $field
        }
        if ($door.ordinal -ne $index -or $door.freshBaselineOrdinal -lt 1 -or
            $door.freshBaselineOrdinal -gt $case.baselineRebuildCount -or [string]$door.openPath -cne $ExpectedOpenPath) {
            throw "$ExpectedFileName contains an invalid native door transition ordinal or path."
        }
        Assert-CathedralDoorInteger $door.ordinal 0 65535 "$ExpectedFileName.doors[$index].ordinal"
        Assert-CathedralDoorInteger $door.freshBaselineOrdinal 1 $case.baselineRebuildCount "$ExpectedFileName.doors[$index].freshBaselineOrdinal"
        Assert-CathedralDoorInteger $door.nativeTypeId 1 2 "$ExpectedFileName.doors[$index].nativeTypeId"
        if (($door.nativeTypeId -eq 1 -and [string]$door.nativeType -cne 'OBJ_L1LDOOR') -or
            ($door.nativeTypeId -eq 2 -and [string]$door.nativeType -cne 'OBJ_L1RDOOR')) {
            throw "$ExpectedFileName contains an unknown native Cathedral door type."
        }
        Assert-CathedralDoorInteger $door.var1OneBased 1 65535 "$ExpectedFileName.doors[$index].var1OneBased"
        Assert-CathedralDoorInteger $door.var2OneBased 1 65535 "$ExpectedFileName.doors[$index].var2OneBased"
        Assert-CathedralDoorInteger $door.sourcePieceObserved 0 65534 "$ExpectedFileName.doors[$index].sourcePieceObserved"
        Assert-CathedralDoorInteger $door.neighborPieceObserved 0 65534 "$ExpectedFileName.doors[$index].neighborPieceObserved"
        if ($door.sourcePieceObserved -ne $door.var1OneBased - 1 -or $door.neighborPieceObserved -ne $door.var2OneBased - 1) {
            throw "$ExpectedFileName contains inconsistent native door piece identities."
        }
        foreach ($field in @('closedLayerHash', 'openLayerHash', 'reclosedLayerHash')) {
            if ([string]$door.$field -cnotmatch '^0x[0-9a-f]{16}$') {
                throw "$ExpectedFileName contains an invalid $field."
            }
        }
        foreach ($field in @('reclosedLayerEqualsClosed', 'reclosedObjectEqualsClosed')) {
            if ($door.$field -isnot [bool]) { throw "$ExpectedFileName.$field is not a native boolean." }
        }
        $referenceBaseline = $baselines[$door.freshBaselineOrdinal - 1]
        if ($door.closedLayerHash -cne $referenceBaseline.hashes.fullLayer) {
            throw "$ExpectedFileName contains a mismatched fresh baseline hash."
        }
        $position = @($door.position)
        if ($position.Count -ne 2) {
            throw "$ExpectedFileName contains an invalid native door position."
        }
        Assert-CathedralDoorInteger $position[0] 0 111 "$ExpectedFileName.doors[$index].position.x"
        Assert-CathedralDoorInteger $position[1] 0 111 "$ExpectedFileName.doors[$index].position.y"
        Assert-CathedralDoorObjectState $door.closedObject "$ExpectedFileName.doors[$index].closedObject"
        Assert-CathedralDoorObjectState $door.openObject "$ExpectedFileName.doors[$index].openObject"
        Assert-CathedralDoorObjectState $door.reclosedObject "$ExpectedFileName.doors[$index].reclosedObject"
        foreach ($object in @($door.closedObject, $door.openObject, $door.reclosedObject)) {
            if ($object.var1OneBased -ne $door.var1OneBased -or $object.var2OneBased -ne $door.var2OneBased) {
                throw "$ExpectedFileName contains inconsistent native door object identities."
            }
        }
        if ($door.closedObject.state -ne 0 -or -not $door.closedObject.doorFlag -or $door.closedObject.preFlag -or
            $door.closedObject.missFlag -or $door.closedObject.selectionRegion -ne 3 -or
            $door.openObject.state -ne 1 -or $door.openObject.animationFrame -ne $door.closedObject.animationFrame + 2 -or
            -not $door.openObject.doorFlag -or -not $door.openObject.preFlag -or -not $door.openObject.missFlag -or
            $door.openObject.selectionRegion -ne 2 -or $door.reclosedObject.state -ne 0 -or
            $door.reclosedObject.animationFrame -ne $door.openObject.animationFrame - 2 -or
            -not $door.reclosedObject.doorFlag -or $door.reclosedObject.preFlag -or $door.reclosedObject.missFlag -or
            $door.reclosedObject.selectionRegion -ne 3) {
            throw "$ExpectedFileName contains an invalid CLOSED/OPEN/CLOSED native door state transition."
        }
        $doorMeaningfulTransition = $false
        $reclosedObjectsEqual = $true
        foreach ($field in @('state', 'var1OneBased', 'var2OneBased', 'animationFrame', 'selectionRegion', 'doorFlag', 'preFlag', 'missFlag', 'solidFlag')) {
            if ($door.closedObject.$field -ne $door.reclosedObject.$field) { $reclosedObjectsEqual = $false }
        }
        if ($door.reclosedObjectEqualsClosed -ne $reclosedObjectsEqual) {
            throw "$ExpectedFileName contains a contradictory reclosed object equality flag."
        }
        $reclosedCellsEqual = $true
        $lastChangedIndex = -1
        $changedCells = @($door.changedCells)
        if ($changedCells.Count -eq 0) { throw "$ExpectedFileName contains a door transition without changed cells." }
        foreach ($changed in $changedCells) {
            foreach ($field in @('index', 'x', 'y', 'closed', 'open', 'reclosed')) {
                Assert-CathedralDoorRequiredProperty $changed $field
            }
            Assert-CathedralDoorInteger $changed.index 0 12543 "$ExpectedFileName.changedCells.index"
            Assert-CathedralDoorInteger $changed.x 0 111 "$ExpectedFileName.changedCells.x"
            Assert-CathedralDoorInteger $changed.y 0 111 "$ExpectedFileName.changedCells.y"
            if ($changed.index -le $lastChangedIndex) {
                throw "$ExpectedFileName contains duplicate or unordered changed-cell indices."
            }
            $lastChangedIndex = $changed.index
            if ($changed.index -lt 0 -or $changed.index -ge 112 * 112 -or $changed.x -ne $changed.index % 112 -or
                $changed.y -ne [math]::Floor($changed.index / 112)) {
                throw "$ExpectedFileName contains an invalid changed-cell coordinate."
            }
            Assert-CathedralDoorTile $changed.closed "$ExpectedFileName.changedCells.closed"
            Assert-CathedralDoorTile $changed.open "$ExpectedFileName.changedCells.open"
            Assert-CathedralDoorTile $changed.reclosed "$ExpectedFileName.changedCells.reclosed"
            $cellChanged = $false
            foreach ($field in @('piece', 'special', 'transparency', 'properties', 'solid', 'missileBlocked', 'blockLight', 'walkable', 'itemBlocking')) {
                $layerField = if ($field -eq 'piece') { 'pieces' } else { $field }
                if ($changed.closed.$field -ne $referenceBaseline.layer.$layerField[$changed.index]) {
                    throw "$ExpectedFileName contains a changed cell inconsistent with its closed baseline."
                }
                if ($changed.closed.$field -ne $changed.open.$field) {
                    $doorMeaningfulTransition = $true
                    $cellChanged = $true
                }
                if ($changed.closed.$field -ne $changed.reclosed.$field) {
                    $reclosedCellsEqual = $false
                    $cellChanged = $true
                }
            }
            if (-not $cellChanged) { throw "$ExpectedFileName contains an unchanged cell in the changed-cell union." }
        }
        if ($door.reclosedLayerEqualsClosed -ne $reclosedCellsEqual -or
            ($door.reclosedLayerEqualsClosed -and $door.closedLayerHash -cne $door.reclosedLayerHash)) {
            throw "$ExpectedFileName contains contradictory reclosed layer evidence."
        }
        if (-not $doorMeaningfulTransition) { throw "$ExpectedFileName contains a door transition without changed open cells." }
        $meaningfulTransition = $true
    }
    if (-not $meaningfulTransition) { throw "$ExpectedFileName contains no meaningful closed-to-open native transition." }
}

function Assert-CathedralDoorOracleSchema([string]$EvidenceRoot) {
    $metadataPath = Join-Path $EvidenceRoot 'door-oracle-format.json'
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { throw 'The Cathedral door format metadata is missing.' }
    try { $metadata = [IO.File]::ReadAllText($metadataPath) | ConvertFrom-Json }
    catch { throw "The Cathedral door format metadata is not valid JSON: $($_.Exception.Message)" }
    foreach ($field in @('format', 'version', 'policy', 'caseCount', 'depths', 'seeds', 'entries', 'rawAssets', 'width', 'height', 'rowMajor', 'deltaCase')) {
        Assert-CathedralDoorRequiredProperty $metadata $field
    }
    Assert-CathedralDoorInteger $metadata.version 1 1 'metadata.version'
    Assert-CathedralDoorInteger $metadata.caseCount 16 16 'metadata.caseCount'
    Assert-CathedralDoorInteger $metadata.width 112 112 'metadata.width'
    Assert-CathedralDoorInteger $metadata.height 112 112 'metadata.height'
    foreach ($depth in @($metadata.depths)) { Assert-CathedralDoorInteger $depth 1 4 'metadata.depths' }
    foreach ($seed in @($metadata.seeds)) { Assert-CathedralDoorInteger $seed 0 4294967295 'metadata.seeds' }
    if ([string]$metadata.format -cne 'godevilution-cathedral-l1-door-oracle' -or $metadata.version -ne 1 -or
        [string]$metadata.policy -cne 'native-add-l1-objects-door-only' -or $metadata.caseCount -ne 16 -or
        $metadata.width -ne 112 -or $metadata.height -ne 112 -or [string]$metadata.rowMajor -cne 'y*width+x' -or
        [string]$metadata.deltaCase -cne 'cathedral-door-delta-depth-2-743271966-main.json') {
        throw 'The Cathedral door format metadata has the wrong native scope.'
    }
    if ((@($metadata.depths) -join ',') -cne '1,2,3,4' -or (@($metadata.seeds) -join ',') -cne '2588,743271966' -or
        (@($metadata.entries) -join ',') -cne 'main,previous' -or (@($metadata.rawAssets) -join ',') -cne 'l1.til,l1.sol') {
        throw 'The Cathedral door format metadata has the wrong case scope.'
    }
    foreach ($name in (Get-CathedralDoorOracleArtifactNames | Where-Object { $_ -like '*.json' -and $_ -ne 'door-oracle-format.json' })) {
        $expectedOpenPath = if ($name -eq 'cathedral-door-delta-depth-2-743271966-main.json') { 'delta' } else { 'sync' }
        Assert-CathedralDoorOracleCase (Join-Path $EvidenceRoot $name) $name $expectedOpenPath
    }
}

function Get-CathedralDoorOracleArtifacts([string]$EvidenceRoot) {
    $expectedNames = @(Get-CathedralDoorOracleArtifactNames)
    $expectedNameSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $expectedNames) { [void]$expectedNameSet.Add($name) }
    $children = @(Get-ChildItem -LiteralPath $EvidenceRoot -Force)
    if (@($children | Where-Object { $_.PSIsContainer }).Count -ne 0) {
        throw 'The Cathedral door oracle emitted an out-of-scope directory.'
    }
    $actualNames = @($children | Where-Object { -not $_.PSIsContainer } | ForEach-Object { $_.Name })
    if ($actualNames.Count -ne $expectedNames.Count -or @($actualNames | Where-Object { -not $expectedNameSet.Contains($_) }).Count -ne 0) {
        throw 'The Cathedral door oracle emitted an incomplete or out-of-scope artifact set.'
    }
    foreach ($name in $expectedNames) {
        $path = Join-Path $EvidenceRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "The Cathedral door oracle did not emit a nonempty $name."
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
        [pscustomobject]@{ Name = $name; Bytes = (Get-Item -LiteralPath $path).Length; Sha256 = $hash }
    }
}

function Assert-CathedralDoorNativeReports([string[]]$ReportPaths) {
    $expectedNames = @('CathedralDoorNativeObjectOracle', 'CathedralDoorNativeDeltaOpenOracle')
    if ($ReportPaths.Count -ne $expectedNames.Count) { throw 'Exactly two Cathedral door native reports are required.' }
    for ($index = 0; $index -lt $ReportPaths.Count; $index++) {
        $path = $ReportPaths[$index]
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing native report $path." }
        try { $report = [IO.File]::ReadAllText($path) | ConvertFrom-Json }
        catch { throw "Native report $path is not valid JSON: $($_.Exception.Message)" }
        foreach ($field in @('tests', 'failures', 'errors', 'disabled')) {
            Assert-CathedralDoorRequiredProperty $report $field
            $expectedCount = if ($field -eq 'tests') { 1 } else { 0 }
            Assert-CathedralDoorInteger $report.$field $expectedCount $expectedCount "$path.$field"
        }
        $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
        if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.errors -ne 0 -or $report.disabled -ne 0 -or
            $cases.Count -ne 1 -or $cases[0].classname -cne 'Drlg_l1' -or $cases[0].name -cne $expectedNames[$index] -or
            $cases[0].status -cne 'RUN' -or $cases[0].result -cne 'COMPLETED') {
            throw "The required Cathedral door native report $($expectedNames[$index]) did not complete successfully; skipped tests are not evidence."
        }
    }
}

function Assert-CathedralPopulationRequiredProperty($Object, [string]$Name) {
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) {
        throw "Cathedral population evidence is missing $Name."
    }
}

function Assert-CathedralPopulationExactProperties($Object, [string[]]$Names, [string]$Scope) {
    foreach ($name in $Names) { Assert-CathedralPopulationRequiredProperty $Object $name }
    $actual = @($Object.PSObject.Properties.Name)
    if (@($actual | Where-Object { $Names -cnotcontains $_ }).Count -ne 0 -or
        @($Names | Where-Object { $actual -cnotcontains $_ }).Count -ne 0) {
        throw "$Scope has an unexpected or missing property."
    }
}

function Assert-CathedralPopulationInteger($Value, [long]$Minimum, [long]$Maximum, [string]$Scope) {
    if ($null -eq $Value) { throw "$Scope is null." }
    $type = $Value.GetType()
    if ($type -ne [byte] -and $type -ne [sbyte] -and $type -ne [int16] -and $type -ne [uint16] -and
        $type -ne [int32] -and $type -ne [uint32] -and $type -ne [int64] -and $type -ne [uint64]) {
        throw "$Scope is not a native integer (floating-point, Boolean, and string values are rejected)."
    }
    try { $number = [decimal]$Value } catch { throw "$Scope is outside its native integer range." }
    if ($number -lt [decimal]$Minimum -or $number -gt [decimal]$Maximum) {
        throw "$Scope is outside its native integer range."
    }
}

function Assert-CathedralPopulationString($Value, [string]$Expected, [string]$Scope) {
    if ($Value -isnot [string] -or $Value -cne $Expected) { throw "$Scope has the wrong value or type." }
}

function Assert-CathedralPopulationArray([object[]]$Values, [int]$ExpectedCount, [string]$Scope) {
    if ($Values.Count -ne $ExpectedCount) { throw "$Scope has an invalid count." }
}

function Assert-CathedralPopulationArrayCount([object[]]$Values, [int]$MinimumCount, [int]$MaximumCount, [string]$Scope) {
    if ($Values.Count -lt $MinimumCount -or $Values.Count -gt $MaximumCount) { throw "$Scope has an invalid count." }
}

function Assert-CathedralPopulationPoint($Point, [string]$Scope, [int]$Minimum = -32768, [int]$Maximum = 32767) {
    Assert-CathedralPopulationExactProperties $Point @('x', 'y') $Scope
    Assert-CathedralPopulationInteger $Point.x $Minimum $Maximum "$Scope.x"
    Assert-CathedralPopulationInteger $Point.y $Minimum $Maximum "$Scope.y"
}

function Assert-CathedralPopulationExactStringArray([object[]]$Values, [string[]]$Expected, [string]$Scope) {
    Assert-CathedralPopulationArray $Values $Expected.Count $Scope
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        Assert-CathedralPopulationString $Values[$index] $Expected[$index] "$Scope[$index]"
    }
}

function Assert-CathedralPopulationMetadata([string]$EvidenceRoot) {
    $path = Join-Path $EvidenceRoot 'metadata.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'The Cathedral population metadata is missing.' }
    try { $metadata = [IO.File]::ReadAllText($path) | ConvertFrom-Json }
    catch { throw "The Cathedral population metadata is not valid JSON: $($_.Exception.Message)" }
    $fields = @('format', 'version', 'policy', 'nativeWidth', 'nativeHeight', 'rowMajor', 'headless', 'soundInitialized',
        'game', 'gameMode', 'difficulty', 'playerLifeQ6', 'caseCount', 'depths', 'seeds', 'entries', 'storySeed',
        'objectFields', 'monsterFields', 'rngBoundaries', 'nativeObjectTypes', 'nativeObjectCatalog')
    Assert-CathedralPopulationExactProperties $metadata $fields 'metadata'
    Assert-CathedralPopulationString $metadata.format 'godevilution-cathedral-l1-population-oracle' 'metadata.format'
    Assert-CathedralPopulationString $metadata.policy 'no-quest-original-diablo-initgolems-initobjects' 'metadata.policy'
    Assert-CathedralPopulationInteger $metadata.version 1 1 'metadata.version'
    Assert-CathedralPopulationInteger $metadata.nativeWidth 112 112 'metadata.nativeWidth'
    Assert-CathedralPopulationInteger $metadata.nativeHeight 112 112 'metadata.nativeHeight'
    Assert-CathedralPopulationInteger $metadata.caseCount 16 16 'metadata.caseCount'
    Assert-CathedralPopulationInteger $metadata.storySeed 324508639 324508639 'metadata.storySeed'
    if ($metadata.rowMajor -isnot [bool] -or -not $metadata.rowMajor -or $metadata.headless -isnot [bool] -or -not $metadata.headless -or
        $metadata.soundInitialized -isnot [bool] -or $metadata.soundInitialized) { throw 'metadata runtime flags are not the required headless native values.' }
    Assert-CathedralPopulationString $metadata.game 'diablo' 'metadata.game'
    Assert-CathedralPopulationString $metadata.gameMode 'singleplayer' 'metadata.gameMode'
    Assert-CathedralPopulationString $metadata.difficulty 'normal' 'metadata.difficulty'
    Assert-CathedralPopulationInteger $metadata.playerLifeQ6 64 64 'metadata.playerLifeQ6'
    Assert-CathedralPopulationArray @($metadata.depths) 4 'metadata.depths'
    foreach ($depth in @($metadata.depths)) { Assert-CathedralPopulationInteger $depth 1 4 'metadata.depths' }
    if ((@($metadata.depths) -join ',') -cne '1,2,3,4') { throw 'metadata.depths has the wrong order.' }
    Assert-CathedralPopulationArray @($metadata.seeds) 2 'metadata.seeds'
    foreach ($seed in @($metadata.seeds)) { Assert-CathedralPopulationInteger $seed 0 2147483647 'metadata.seeds' }
    if (@($metadata.seeds)[0] -ne 2588 -or @($metadata.seeds)[1] -ne 743271966) { throw 'metadata.seeds has the wrong order.' }
    Assert-CathedralPopulationExactStringArray @($metadata.entries) @('main', 'previous') 'metadata.entries'
    Assert-CathedralPopulationExactStringArray @($metadata.objectFields) @(
        'type', 'position', 'applyLighting', 'animFlag', 'animDelay', 'animCount', 'animLength', 'animFrame',
        'animWidth', 'deleteFlag', 'breakFlag', 'solidFlag', 'missilePassable', 'selectionRegion', 'preFlag',
        'trapFlag', 'doorFlag', 'lightId', 'rndSeed', 'var1', 'var2', 'var3', 'var4', 'var5', 'var6', 'bookMessage', 'var8') 'metadata.objectFields'
    Assert-CathedralPopulationExactStringArray @($metadata.monsterFields) @(
        'type', 'levelType', 'mode', 'goal', 'goalVar1', 'goalVar2', 'goalVar3', 'var1', 'var2', 'var3', 'pathCount',
        'tile', 'future', 'old', 'last', 'direction', 'enemy', 'enemyPosition', 'invalid', 'ai', 'intelligence',
        'activeForTicks', 'uniqueType', 'uniqueTranslation', 'corpseId', 'whoHit', 'minDamage', 'maxDamage',
        'minDamageSpecial', 'maxDamageSpecial', 'armorClass', 'resistance', 'talkMessage', 'leader', 'leaderRelation',
        'packSize', 'flags', 'rndItemSeed', 'aiSeed', 'lightId', 'hitPoints', 'maxHitPoints', 'animTicksPerFrame',
        'animTickCounter', 'animNumberOfFrames', 'animCurrentFrame') 'metadata.monsterFields'
    Assert-CathedralPopulationExactStringArray @($metadata.rngBoundaries) @(
        'rngAfterCreateLevel', 'rngAfterPopulationReset', 'rngAfterGetLevelMTypes', 'rngAfterInitThemes', 'rngAfterInitGolems', 'rngAfterInitObjects') 'metadata.rngBoundaries'
    Assert-CathedralPopulationExactProperties $metadata.nativeObjectTypes @('sarcophagus', 'barrel') 'metadata.nativeObjectTypes'
    Assert-CathedralPopulationInteger $metadata.nativeObjectTypes.sarcophagus 0 126 'metadata.nativeObjectTypes.sarcophagus'
    Assert-CathedralPopulationInteger $metadata.nativeObjectTypes.barrel 0 126 'metadata.nativeObjectTypes.barrel'
    if ($metadata.nativeObjectTypes.sarcophagus -eq $metadata.nativeObjectTypes.barrel) { throw 'metadata.nativeObjectTypes must distinguish sarcophagus and barrel.' }
    $catalogFields = @('nativeObjectId', 'graphicIndex', 'minLevel', 'maxLevel', 'levelType', 'theme', 'quest', 'flags', 'animDelay', 'animLength', 'animWidth', 'selectionRegion', 'behavior')
    $catalog = @($metadata.nativeObjectCatalog)
    Assert-CathedralPopulationArray $catalog 109 'metadata.nativeObjectCatalog'
    $catalogIds = [Collections.Generic.HashSet[int]]::new()
    foreach ($entry in $catalog) {
        Assert-CathedralPopulationExactProperties $entry $catalogFields 'metadata.nativeObjectCatalog.entry'
        Assert-CathedralPopulationInteger $entry.nativeObjectId 0 108 'metadata.nativeObjectCatalog.nativeObjectId'
        if (-not $catalogIds.Add([int]$entry.nativeObjectId)) { throw 'metadata.nativeObjectCatalog has duplicate native object IDs.' }
        foreach ($field in @('graphicIndex','minLevel','maxLevel','levelType','theme','quest','flags','animDelay','animLength','animWidth','selectionRegion')) {
            Assert-CathedralPopulationInteger $entry.$field -2147483648 2147483647 "metadata.nativeObjectCatalog.$field"
        }
        if ($entry.behavior -isnot [string]) { throw 'metadata.nativeObjectCatalog.behavior is not a string.' }
    }
    if (-not $catalogIds.Contains([int]$metadata.nativeObjectTypes.sarcophagus) -or -not $catalogIds.Contains([int]$metadata.nativeObjectTypes.barrel)) { throw 'metadata.nativeObjectTypes is not represented in the native object catalog.' }
}

function Get-CathedralPopulationOracleArtifactNames {
    $names = @('metadata.json')
    foreach ($depth in 1..4) {
        foreach ($seed in @('2588', '743271966')) {
            foreach ($entry in @('main', 'previous')) { $names += "cathedral-population-depth-$depth-$seed-$entry.json" }
        }
    }
    return $names
}

function Get-CathedralPopulationOracleArtifacts([string]$EvidenceRoot) {
    $expectedNames = @(Get-CathedralPopulationOracleArtifactNames)
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $expectedNames) { [void]$expected.Add($name) }
    $rootItem = Get-Item -LiteralPath $EvidenceRoot -ErrorAction Stop
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'The Cathedral population artifact root must not be a reparse point.' }
    $children = @(Get-ChildItem -LiteralPath $EvidenceRoot -Force)
    if (@($children | Where-Object { $_.PSIsContainer }).Count -ne 0) { throw 'The Cathedral population oracle emitted an out-of-scope directory.' }
    if (@($children | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -ne 0) { throw 'The Cathedral population oracle emitted a reparse-point artifact.' }
    $actualNames = @($children | ForEach-Object { $_.Name })
    if ($actualNames.Count -ne $expectedNames.Count -or @($actualNames | Where-Object { -not $expected.Contains($_) }).Count -ne 0) {
        throw 'The Cathedral population oracle emitted an incomplete or out-of-scope artifact set.'
    }
    foreach ($name in $expectedNames) {
        $path = Join-Path $EvidenceRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) { throw "The Cathedral population oracle did not emit a nonempty $name." }
        $hasher = [Security.Cryptography.SHA256]::Create(); $stream = $null
        try { $stream = [IO.File]::OpenRead($path); $hash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { if ($null -ne $stream) { $stream.Dispose() }; $hasher.Dispose() }
        [pscustomobject]@{ Name = $name; Bytes = (Get-Item -LiteralPath $path).Length; Sha256 = $hash }
    }
}

function Assert-CathedralPopulationUnsignedArray([object[]]$Values, [int]$ExpectedCount, [long]$Maximum, [string]$Scope) {
    Assert-CathedralPopulationArray $Values $ExpectedCount $Scope
    foreach ($value in $Values) { Assert-CathedralPopulationInteger $value 0 $Maximum $Scope }
}

function Assert-CathedralPopulationCase([string]$Path, [string]$ExpectedFileName, $Metadata) {
    try { $case = [IO.File]::ReadAllText($Path) | ConvertFrom-Json }
    catch { throw "The Cathedral population case $ExpectedFileName is not valid JSON: $($_.Exception.Message)" }
    $fields = @('format', 'version', 'policy', 'depth', 'inputSeed', 'geometrySeed', 'populationSeed', 'storySeed', 'entry', 'attemptSeed',
        'rngAfterCreateLevel', 'rngAfterPopulationReset', 'rngAfterGetLevelMTypes', 'rngAfterInitThemes', 'rngAfterInitGolems', 'rngAfterInitObjects', 'entryPosition',
        'stairsUp', 'stairsDown', 'triggers', 'populatedCellsBeforeHoldThemeRooms', 'populatedCellsAfterHoldThemeRooms', 'pieces', 'transparency',
        'solProperties', 'activeObjectCount', 'activeObjectIds', 'availableObjectIds', 'tileObjectRefs', 'objects', 'activeMonsterCount',
        'activeMonsterIds', 'monsters', 'bestiary', 'playerContext')
    Assert-CathedralPopulationExactProperties $case $fields $ExpectedFileName
    Assert-CathedralPopulationString $case.format 'godevilution-cathedral-l1-population-oracle' "$ExpectedFileName.format"
    Assert-CathedralPopulationString $case.policy 'no-quest-original-diablo-initgolems-initobjects' "$ExpectedFileName.policy"
    Assert-CathedralPopulationInteger $case.version 1 1 "$ExpectedFileName.version"
    Assert-CathedralPopulationInteger $case.depth 1 4 "$ExpectedFileName.depth"
    Assert-CathedralPopulationInteger $case.inputSeed 0 2147483647 "$ExpectedFileName.inputSeed"
    Assert-CathedralPopulationInteger $case.geometrySeed 0 2147483647 "$ExpectedFileName.geometrySeed"
    Assert-CathedralPopulationInteger $case.populationSeed 0 2147483647 "$ExpectedFileName.populationSeed"
    if ($case.geometrySeed -ne $case.inputSeed -or $case.populationSeed -ne $case.inputSeed) { throw "$ExpectedFileName geometry/population seed boundaries must equal inputSeed." }
    Assert-CathedralPopulationInteger $case.storySeed 324508639 324508639 "$ExpectedFileName.storySeed"
    if ($case.entry -isnot [string] -or @('main', 'previous') -notcontains $case.entry) { throw "$ExpectedFileName.entry has the wrong value or type." }
    foreach ($field in @('attemptSeed', 'rngAfterCreateLevel', 'rngAfterPopulationReset', 'rngAfterGetLevelMTypes', 'rngAfterInitThemes', 'rngAfterInitGolems', 'rngAfterInitObjects')) {
        Assert-CathedralPopulationInteger $case.$field 0 4294967295 "$ExpectedFileName.$field"
    }
    $identity = [regex]::Match($ExpectedFileName, '^cathedral-population-depth-(1|2|3|4)-(2588|743271966)-(main|previous)\.json$')
    if (-not $identity.Success -or $case.depth -ne [int]$identity.Groups[1].Value -or $case.inputSeed -ne [int64]$identity.Groups[2].Value -or $case.entry -cne $identity.Groups[3].Value) { throw "$ExpectedFileName has mismatched case identity." }
    Assert-CathedralPopulationPoint $case.entryPosition "$ExpectedFileName.entryPosition" 0 111
    Assert-CathedralPopulationPoint $case.stairsUp "$ExpectedFileName.stairsUp" 0 111
    Assert-CathedralPopulationPoint $case.stairsDown "$ExpectedFileName.stairsDown" 0 111
    $triggers = @($case.triggers); Assert-CathedralPopulationArrayCount $triggers 1 64 "$ExpectedFileName.triggers"
    foreach ($trigger in $triggers) {
        Assert-CathedralPopulationExactProperties $trigger @('position', 'message', 'level') "$ExpectedFileName.trigger"
        Assert-CathedralPopulationPoint $trigger.position "$ExpectedFileName.trigger.position" 0 111
        Assert-CathedralPopulationInteger $trigger.message 0 2147483647 "$ExpectedFileName.trigger.message"
        Assert-CathedralPopulationInteger $trigger.level 0 2147483647 "$ExpectedFileName.trigger.level"
    }
    foreach($populationField in @('populatedCellsBeforeHoldThemeRooms','populatedCellsAfterHoldThemeRooms')){
        Assert-CathedralPopulationArrayCount @($case.$populationField) 1 12544 "$ExpectedFileName.$populationField"
        foreach($cell in @($case.$populationField)){Assert-CathedralPopulationInteger $cell 0 12543 "$ExpectedFileName.$populationField"}
    }
    Assert-CathedralPopulationUnsignedArray @($case.pieces) 12544 65535 "$ExpectedFileName.pieces"
    Assert-CathedralPopulationUnsignedArray @($case.transparency) 12544 255 "$ExpectedFileName.transparency"
    Assert-CathedralPopulationUnsignedArray @($case.solProperties) 1379 255 "$ExpectedFileName.solProperties"
    Assert-CathedralPopulationInteger $case.activeObjectCount 1 127 "$ExpectedFileName.activeObjectCount"
    Assert-CathedralPopulationUnsignedArray @($case.activeObjectIds) 127 126 "$ExpectedFileName.activeObjectIds"
    Assert-CathedralPopulationUnsignedArray @($case.availableObjectIds) 127 126 "$ExpectedFileName.availableObjectIds"
    $activeIds = [Collections.Generic.HashSet[int]]::new(); for($i=0;$i -lt [int]$case.activeObjectCount;$i++){ if(-not $activeIds.Add([int]$case.activeObjectIds[$i])){throw "$ExpectedFileName has duplicate active object ids."} }
    $availableCount = 127 - [int]$case.activeObjectCount; $availableIds = [Collections.Generic.HashSet[int]]::new(); for($i=0;$i -lt $availableCount;$i++){ if(-not $availableIds.Add([int]$case.availableObjectIds[$i])){throw "$ExpectedFileName has duplicate available object ids."}; if($activeIds.Contains([int]$case.availableObjectIds[$i])){throw "$ExpectedFileName active and available object ids overlap."} }
    if ($activeIds.Count + $availableIds.Count -ne 127) { throw "$ExpectedFileName object pools do not partition the native slots." }
    $tileRefs = @($case.tileObjectRefs); Assert-CathedralPopulationArray $tileRefs 12544 "$ExpectedFileName.tileObjectRefs"
    foreach ($reference in $tileRefs) {
        Assert-CathedralPopulationInteger $reference -127 127 "$ExpectedFileName.tileObjectRefs"
        if ([int]$reference -ne 0 -and -not $activeIds.Contains(([math]::Abs([int]$reference) - 1))) { throw "$ExpectedFileName tileObjectRefs points to an inactive object." }
    }
    $objectFields = @('slot', 'type', 'position', 'applyLighting', 'animFlag', 'animDelay', 'animCount', 'animLength', 'animFrame', 'animWidth', 'deleteFlag', 'breakFlag', 'solidFlag', 'missilePassable', 'selectionRegion', 'preFlag', 'trapFlag', 'doorFlag', 'lightId', 'rndSeed', 'var1', 'var2', 'var3', 'var4', 'var5', 'var6', 'bookMessage', 'var8')
    $objects = @($case.objects); Assert-CathedralPopulationArray $objects ([int]$case.activeObjectCount) "$ExpectedFileName.objects"; $objectSlots = [Collections.Generic.HashSet[int]]::new()
    foreach ($object in $objects) {
        Assert-CathedralPopulationExactProperties $object $objectFields "$ExpectedFileName.object"
        Assert-CathedralPopulationInteger $object.slot 0 126 "$ExpectedFileName.object.slot"; if(-not $objectSlots.Add([int]$object.slot)){throw "$ExpectedFileName has duplicate object slots."}; if(-not $activeIds.Contains([int]$object.slot)){throw "$ExpectedFileName object slot is not active."}
        Assert-CathedralPopulationInteger $object.type -2147483648 2147483647 "$ExpectedFileName.object.type"; Assert-CathedralPopulationPoint $object.position "$ExpectedFileName.object.position" 0 111
        foreach($field in @('applyLighting','deleteFlag','solidFlag','missilePassable','preFlag','trapFlag','doorFlag')){ if($object.$field -isnot [bool]){throw "$ExpectedFileName.object.$field is not Boolean."} }
        foreach($field in @('animFlag','animLength','animFrame','rndSeed','var6')){ Assert-CathedralPopulationInteger $object.$field 0 4294967295 "$ExpectedFileName.object.$field" }
        Assert-CathedralPopulationInteger $object.animWidth 0 65535 "$ExpectedFileName.object.animWidth"
        foreach($field in @('animDelay','animCount','breakFlag','selectionRegion','lightId','var1','var2','var3','var4','var5','bookMessage','var8')){ Assert-CathedralPopulationInteger $object.$field -2147483648 2147483647 "$ExpectedFileName.object.$field" }
        $positionCell = ([int]$object.position.y * 112) + [int]$object.position.x
        if ([int]$tileRefs[$positionCell] -ne ([int]$object.slot + 1)) { throw "$ExpectedFileName primary object tile reference does not equal slot+1." }
        if ([int]$object.type -eq [int]$Metadata.nativeObjectTypes.sarcophagus -and [int]$object.position.y -gt 0) {
            $northCell = ([int]$object.position.y - 1) * 112 + [int]$object.position.x
            if ([int]$tileRefs[$northCell] -ne -([int]$object.slot + 1)) { throw "$ExpectedFileName sarcophagus north tile reference is not negative slot+1." }
        }
    }
    Assert-CathedralPopulationInteger $case.activeMonsterCount 0 200 "$ExpectedFileName.activeMonsterCount"; $monsterIds = @($case.activeMonsterIds); Assert-CathedralPopulationArray $monsterIds ([int]$case.activeMonsterCount) "$ExpectedFileName.activeMonsterIds"; $monsterSlots = [Collections.Generic.HashSet[int]]::new(); foreach($id in $monsterIds){Assert-CathedralPopulationInteger $id 0 199 "$ExpectedFileName.activeMonsterIds"; if(-not $monsterSlots.Add([int]$id)){throw "$ExpectedFileName has duplicate monster ids."}}
    if ($null -eq $Metadata) { throw "$ExpectedFileName requires the exact native metadata catalog." }
    $catalog = @($Metadata.nativeObjectCatalog); $catalogById = @{}; foreach($catalogEntry in $catalog){$catalogById[[int]$catalogEntry.nativeObjectId] = $catalogEntry}
    $bestiary = @($case.bestiary); Assert-CathedralPopulationArrayCount $bestiary 1 255 "$ExpectedFileName.bestiary"
    $bestiaryByType = @{}; $bestiaryByIndex = @{}
    foreach($entry in $bestiary){ Assert-CathedralPopulationExactProperties $entry @('index','type','placeFlags','corpseId','frames','rates','widths','skeleton') "$ExpectedFileName.bestiary"; Assert-CathedralPopulationInteger $entry.index 0 255 "$ExpectedFileName.bestiary.index"; Assert-CathedralPopulationInteger $entry.type -2147483648 2147483647 "$ExpectedFileName.bestiary.type"; if($bestiaryByType.ContainsKey([int]$entry.type)){throw "$ExpectedFileName.bestiary has duplicate monster types."}; if($bestiaryByIndex.ContainsKey([int]$entry.index)){throw "$ExpectedFileName.bestiary has duplicate indices."}; $bestiaryByType[[int]$entry.type] = $entry; $bestiaryByIndex[[int]$entry.index] = $entry; Assert-CathedralPopulationInteger $entry.placeFlags -2147483648 2147483647 "$ExpectedFileName.bestiary.placeFlags"; Assert-CathedralPopulationInteger $entry.corpseId -2147483648 2147483647 "$ExpectedFileName.bestiary.corpseId"; if($entry.skeleton -isnot [bool]){throw "$ExpectedFileName.bestiary.skeleton is not Boolean."}; foreach($field in @('frames','rates','widths')){Assert-CathedralPopulationUnsignedArray @($entry.$field) 6 2147483647 "$ExpectedFileName.bestiary.$field"} }
    $monsterFields = @('slot','type','levelType','mode','goal','goalVar1','goalVar2','goalVar3','var1','var2','var3','pathCount','tile','future','old','last','direction','enemy','enemyPosition','invalid','ai','intelligence','activeForTicks','uniqueType','uniqueTranslation','corpseId','whoHit','minDamage','maxDamage','minDamageSpecial','maxDamageSpecial','armorClass','resistance','talkMessage','leader','leaderRelation','packSize','flags','rndItemSeed','aiSeed','lightId','hitPoints','maxHitPoints','animTicksPerFrame','animTickCounter','animNumberOfFrames','animCurrentFrame')
    $monsters = @($case.monsters); Assert-CathedralPopulationArray $monsters ([int]$case.activeMonsterCount) "$ExpectedFileName.monsters"
    $monsterRecordSlots = [Collections.Generic.HashSet[int]]::new()
    foreach($monster in $monsters){ Assert-CathedralPopulationExactProperties $monster $monsterFields "$ExpectedFileName.monster"; Assert-CathedralPopulationInteger $monster.slot 0 199 "$ExpectedFileName.monster.slot"; if(-not $monsterSlots.Contains([int]$monster.slot)){throw "$ExpectedFileName monster slot is not active."}; if(-not $monsterRecordSlots.Add([int]$monster.slot)){throw "$ExpectedFileName has duplicate monster records."}; if(-not $bestiaryByType.ContainsKey([int]$monster.type)){throw "$ExpectedFileName monster type is absent from bestiary."}; Assert-CathedralPopulationInteger $monster.type -2147483648 2147483647 "$ExpectedFileName.monster.type"; foreach($field in @('tile','future','old','last','enemyPosition')){Assert-CathedralPopulationPoint $monster.$field "$ExpectedFileName.monster.$field" -32768 32767}; if($monster.invalid -isnot [bool]){throw "$ExpectedFileName.monster.invalid is not Boolean."}; Assert-CathedralPopulationInteger $monster.levelType 0 ($bestiary.Count - 1) "$ExpectedFileName.monster.levelType"; if(-not $bestiaryByIndex.ContainsKey([int]$monster.levelType) -or [int]$bestiaryByIndex[[int]$monster.levelType].type -ne [int]$monster.type){throw "$ExpectedFileName monster type does not match its levelType bestiary link."}; foreach($field in @('mode','goal','goalVar1','goalVar2','goalVar3','var1','var2','var3','pathCount','direction','enemy','ai','intelligence','activeForTicks','uniqueType','uniqueTranslation','corpseId','whoHit','minDamage','maxDamage','minDamageSpecial','maxDamageSpecial','armorClass','resistance','talkMessage','leader','leaderRelation','packSize','animTicksPerFrame','animTickCounter','animNumberOfFrames','animCurrentFrame')){Assert-CathedralPopulationInteger $monster.$field -2147483648 2147483647 "$ExpectedFileName.monster.$field"}; foreach($field in @('flags','rndItemSeed','aiSeed')){Assert-CathedralPopulationInteger $monster.$field 0 4294967295 "$ExpectedFileName.monster.$field"} }
    $bestiaryIndices = [Collections.Generic.HashSet[int]]::new(); foreach($entry in $bestiary){if(-not $bestiaryIndices.Add([int]$entry.index)){throw "$ExpectedFileName.bestiary has duplicate indices."}}
    for($expectedIndex=0;$expectedIndex -lt $bestiary.Count;$expectedIndex++){if(-not $bestiaryIndices.Contains($expectedIndex)){throw "$ExpectedFileName.bestiary indices are not contiguous."}}
    $golemEntry = @($bestiary | Where-Object { [int]$_.index -eq 0 })
    if ($golemEntry.Count -ne 1) { throw "$ExpectedFileName.bestiary is missing native golem index 0." }
    if ([int]$case.activeMonsterCount -lt 4) { throw "$ExpectedFileName does not retain the four native golem reservations." }
    for($golemIndex=0;$golemIndex -lt 4;$golemIndex++){ if([int]$case.activeMonsterIds[$golemIndex] -ne $golemIndex){throw "$ExpectedFileName native golem IDs are not the first four slots."}; $golem = @($monsters | Where-Object { [int]$_.slot -eq $golemIndex })[0]; if([int]$golem.type -ne [int]$golemEntry[0].type -or [int]$golem.tile.x -ne 1 -or [int]$golem.tile.y -ne 0){throw "$ExpectedFileName native golem reservation is not at GolemHoldingCell."} }
    Assert-CathedralPopulationExactProperties $case.playerContext @('id','class','level','active','levelChanging','tile','future','old') "$ExpectedFileName.playerContext"
    foreach($field in @('id','class','level')){Assert-CathedralPopulationInteger $case.playerContext.$field -2147483648 2147483647 "$ExpectedFileName.playerContext.$field"}; foreach($field in @('active','levelChanging')){if($case.playerContext.$field -isnot [bool]){throw "$ExpectedFileName.playerContext.$field is not Boolean."}}; foreach($field in @('tile','future','old')){Assert-CathedralPopulationPoint $case.playerContext.$field "$ExpectedFileName.playerContext.$field" -32768 32767}
    foreach($object in $objects){
        $objectType = [int]$object.type
        if (-not $catalogById.ContainsKey($objectType)) { throw "$ExpectedFileName object type is absent from metadata.nativeObjectCatalog." }
        $reservationSlot = $null; $threshold = $null
        if ($objectType -eq [int]$Metadata.nativeObjectTypes.sarcophagus) { $reservationSlot = [int]$object.var2; $threshold = [int]$object.var1 }
        elseif ($objectType -eq [int]$Metadata.nativeObjectTypes.barrel) { $reservationSlot = [int]$object.var4; $threshold = [int]$object.var2 }
        if ($null -ne $reservationSlot -and $reservationSlot -lt -1) { throw "$ExpectedFileName skeleton reservation is below the native -1 sentinel." }
        if ($null -ne $reservationSlot -and $threshold -ge 8 -and $reservationSlot -ge 0) {
            if (-not $monsterSlots.Contains($reservationSlot)) { throw "$ExpectedFileName skeleton reservation is not an active monster." }
            $reservedMonster = @($monsters | Where-Object { [int]$_.slot -eq $reservationSlot })[0]
            if (-not $bestiaryByType.ContainsKey([int]$reservedMonster.type) -or -not $bestiaryByType[[int]$reservedMonster.type].skeleton) { throw "$ExpectedFileName skeleton reservation does not reference a skeleton monster." }
            if ([int]$reservedMonster.tile.x -ne 0 -or [int]$reservedMonster.tile.y -ne 0) { throw "$ExpectedFileName skeleton reservation is not at the native {0,0} cell." }
        }
    }
}

function Assert-CathedralPopulationNativeReport([string]$ReportPath) {
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) { throw 'The native population oracle did not emit its test result.' }
    try { $report = [IO.File]::ReadAllText($ReportPath) | ConvertFrom-Json } catch { throw "The native population report is not valid JSON: $($_.Exception.Message)" }
    foreach($field in @('tests','failures','errors','disabled')){ Assert-CathedralPopulationRequiredProperty $report $field; Assert-CathedralPopulationInteger $report.$field 0 2147483647 "native report.$field" }
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.errors -ne 0 -or $report.disabled -ne 0 -or $cases.Count -ne 1 -or $cases[0].classname -cne 'CathedralPopulation' -or $cases[0].name -cne 'NativeObjectPopulationLocalAssetOracle' -or $cases[0].status -cne 'RUN' -or $cases[0].result -cne 'COMPLETED') { throw 'The required native population oracle did not complete successfully; skipped tests are not evidence.' }
}

function Quote-CathedralNativeArgument([string]$Value) {
    $builder = [Text.StringBuilder]::new(); [void]$builder.Append('"'); $slashes = 0
    foreach($character in $Value.ToCharArray()) {
        if($character -eq '\'){ $slashes++; continue }
        if($character -eq '"') {
            for($index = 0; $index -lt (($slashes * 2) + 1); $index++){ [void]$builder.Append('\') }
            [void]$builder.Append('"')
        } else {
            for($index = 0; $index -lt $slashes; $index++){ [void]$builder.Append('\') }
            $slashes = 0
            [void]$builder.Append($character)
        }
        if($character -eq '"'){$slashes=0}
    }
    for($index = 0; $index -lt ($slashes * 2); $index++){ [void]$builder.Append('\') }
    [void]$builder.Append('"'); return $builder.ToString()
}

function Add-CathedralPopulationCleanupError([System.Collections.IDictionary]$Outcome, [string]$Message) {
    if ([string]::IsNullOrWhiteSpace($Message)) { return }
    if ([string]::IsNullOrWhiteSpace([string]$Outcome.CleanupError)) { $Outcome.CleanupError = $Message }
    else { $Outcome.CleanupError = [string]$Outcome.CleanupError + ' | ' + $Message }
}

function Stop-CathedralPopulationOwnedProcessTree([Diagnostics.Process]$Process, [System.Collections.IDictionary]$Outcome) {
    if ($null -eq $Process) { return }
    $pidValue = $Outcome.Pid
    try {
        if (-not $Process.HasExited) {
            $taskKill = [Diagnostics.ProcessStartInfo]::new()
            $taskKill.FileName = Join-Path $env:SystemRoot 'System32\taskkill.exe'
            $taskKill.Arguments = "/PID $pidValue /T /F"
            $taskKill.UseShellExecute = $false; $taskKill.CreateNoWindow = $true
            $taskKill.RedirectStandardOutput = $true; $taskKill.RedirectStandardError = $true
            $killer = $null; $killerStdoutTask = $null; $killerStderrTask = $null
            try {
                $killer = [Diagnostics.Process]::Start($taskKill)
                if ($null -eq $killer) {
                    Add-CathedralPopulationCleanupError $Outcome ('Could not start the task-tree terminator for owned PID ' + $pidValue + '.')
                } else {
                    $killerStdoutTask = $killer.StandardOutput.ReadToEndAsync(); $killerStderrTask = $killer.StandardError.ReadToEndAsync()
                    if (-not $killer.WaitForExit(10000)) {
                        Add-CathedralPopulationCleanupError $Outcome ('Could not confirm task-tree termination for owned PID ' + $pidValue + '.')
                        try { $killer.Kill() } catch { Add-CathedralPopulationCleanupError $Outcome ('Could not terminate task-tree helper: ' + $_.Exception.Message) }
                        if (-not $killer.WaitForExit(10000)) { Add-CathedralPopulationCleanupError $Outcome 'Task-tree helper did not terminate after its bounded cleanup.' }
                    }
                    foreach ($killerTask in @($killerStdoutTask, $killerStderrTask)) {
                        if ($null -ne $killerTask) {
                            try { if (-not $killerTask.Wait(10000)) { Add-CathedralPopulationCleanupError $Outcome 'Task-tree helper output did not drain by its deadline.' } }
                            catch { Add-CathedralPopulationCleanupError $Outcome ('Could not drain task-tree helper output: ' + $_.Exception.Message) }
                        }
                    }
                    $killerOutput = ''
                    foreach ($killerTask in @($killerStdoutTask, $killerStderrTask)) { if ($null -ne $killerTask -and $killerTask.Status -eq [Threading.Tasks.TaskStatus]::RanToCompletion) { $killerOutput += $killerTask.Result } }
                    if ($killer.ExitCode -ne 0 -and -not $Process.HasExited) { Add-CathedralPopulationCleanupError $Outcome ('taskkill failed for owned PID ' + $pidValue + ' (' + $killer.ExitCode + '): ' + $killerOutput) }
                }
            } catch { Add-CathedralPopulationCleanupError $Outcome ('Could not terminate owned PID ' + $pidValue + ': ' + $_.Exception.Message) }
            finally {
                if ($null -ne $killer) {
                    try { if (-not $killer.HasExited) { $killer.Kill(); if (-not $killer.WaitForExit(10000)) { Add-CathedralPopulationCleanupError $Outcome 'Task-tree helper did not confirm exit during cleanup.' } } }
                    catch { Add-CathedralPopulationCleanupError $Outcome "Could not finish task-tree helper cleanup: $($_.Exception.Message)" }
                    try { $killer.Dispose() } catch { Add-CathedralPopulationCleanupError $Outcome "Could not dispose task-tree terminator: $($_.Exception.Message)" }
                }
            }
        }
        if (-not $Process.HasExited) { try { $Process.Kill() } catch { Add-CathedralPopulationCleanupError $Outcome ('Could not terminate owned PID ' + $pidValue + ' directly: ' + $_.Exception.Message) } }
        if (-not $Process.WaitForExit(10000)) { Add-CathedralPopulationCleanupError $Outcome ('Owned PID ' + $pidValue + ' did not confirm exit after bounded termination.') }
    } catch { Add-CathedralPopulationCleanupError $Outcome ('Could not confirm owned PID ' + $pidValue + ' exit: ' + $_.Exception.Message) }
}

function Invoke-CathedralPopulationOwnedProcess([string]$Executable, [string]$Arguments, [string]$LogPath, [int]$TimeoutMilliseconds = 180000) {
    $outcome = [ordered]@{ Pid = $null; ExitCode = $null; Output = ''; TimedOut = $false; ConfirmedExited = $false; LogRetained = $false; Error = $null; CleanupError = $null }
    $process = $null; $stdoutTask = $null; $stderrTask = $null; $previousErrorMode = $null
    try {
        if ($TimeoutMilliseconds -le 0) { throw 'The owned process stage deadline must be positive.' }
        $start = [Diagnostics.ProcessStartInfo]::new(); $start.FileName = $Executable; $start.Arguments = $Arguments
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        if ($env:OS -eq 'Windows_NT' -and -not ('GodevilutionNativeErrorMode' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GodevilutionNativeErrorMode
{
    [DllImport("kernel32.dll")] public static extern uint GetErrorMode();
    [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode);
}
'@
        }
        try {
            if ($env:OS -eq 'Windows_NT') { $previousErrorMode = [GodevilutionNativeErrorMode]::GetErrorMode(); [void][GodevilutionNativeErrorMode]::SetErrorMode($previousErrorMode -bor 3) }
            try { $process = [Diagnostics.Process]::Start($start) } catch { $outcome.Error = "Could not start owned Cathedral population process '$Executable': $($_.Exception.Message)" }
        } finally { if ($null -ne $previousErrorMode) { try { [void][GodevilutionNativeErrorMode]::SetErrorMode($previousErrorMode) } catch { Add-CathedralPopulationCleanupError $outcome "Could not restore native error mode: $($_.Exception.Message)" } } }
        if ($null -eq $process) { if ($null -eq $outcome.Error) { $outcome.Error = "Could not start owned Cathedral population process '$Executable'." } }
        else {
            $outcome.Pid = $process.Id
            $stdoutTask = $process.StandardOutput.ReadToEndAsync(); $stderrTask = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit($TimeoutMilliseconds)) { $outcome.TimedOut = $true; $outcome.Error = "Owned Cathedral population process PID $($outcome.Pid) exceeded the $TimeoutMilliseconds ms stage deadline."; Stop-CathedralPopulationOwnedProcessTree $process $outcome }
        }
    } catch {
        if ($null -eq $outcome.Error) { $outcome.Error = $_.Exception.Message } else { Add-CathedralPopulationCleanupError $outcome $_.Exception.Message }
    } finally {
        if ($null -ne $process) {
            try { if (-not $process.HasExited) { Stop-CathedralPopulationOwnedProcessTree $process $outcome } } catch { Add-CathedralPopulationCleanupError $outcome $_.Exception.Message }
            try { if ($process.HasExited) { $outcome.ConfirmedExited = $true; $outcome.ExitCode = $process.ExitCode } } catch { Add-CathedralPopulationCleanupError $outcome "Could not retain owned PID $($outcome.Pid) exit state: $($_.Exception.Message)" }
        }
        foreach ($task in @($stdoutTask, $stderrTask)) {
            if ($null -ne $task) { try { if ($task.Wait(10000)) { $outcome.Output += $task.Result } else { Add-CathedralPopulationCleanupError $outcome 'Owned population process output did not drain by its deadline.' } } catch { Add-CathedralPopulationCleanupError $outcome "Could not drain owned population process output: $($_.Exception.Message)" } }
        }
        try { [IO.File]::WriteAllText($LogPath, $outcome.Output, [Text.UTF8Encoding]::new($false)); $outcome.LogRetained = $true } catch { Add-CathedralPopulationCleanupError $outcome "Could not retain owned population process log: $($_.Exception.Message)" }
        if ($null -ne $process) { try { $process.Dispose() } catch { Add-CathedralPopulationCleanupError $outcome "Could not dispose owned population process: $($_.Exception.Message)" } }
    }
    if ($null -eq $outcome.Error -and $null -ne $outcome.CleanupError) { $outcome.Error = $outcome.CleanupError }
    return [pscustomobject]$outcome
}

function Invoke-CathedralPopulationNativeOracle([string]$Executable, [string]$ReportPath, [string]$LogPath, [int]$TimeoutMilliseconds = 180000, [string]$Arguments = '') {
    $nativeArguments = if ([string]::IsNullOrWhiteSpace($Arguments)) {
        '--gtest_filter=CathedralPopulation.NativeObjectPopulationLocalAssetOracle --gtest_output=json:' + (Quote-CathedralNativeArgument $ReportPath)
    } else { $Arguments }
    return Invoke-CathedralPopulationOwnedProcess $Executable $nativeArguments $LogPath $TimeoutMilliseconds
}

function Get-CathedralPopulationInputArtifact([string]$EvidenceRoot, [switch]$RequirePrerequisites) {
    $root = Get-Item -LiteralPath $EvidenceRoot -Force -ErrorAction Stop
    if (-not $root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The population input root must be a regular directory.'
    }
    $children = @(Get-ChildItem -LiteralPath $EvidenceRoot -Force)
    if ($children.Count -ne 1 -or $children[0].Name -cne 'native-population-inputs.json' -or
        $children[0].PSIsContainer -or ($children[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $children[0].Length -le 0 -or $children[0].Length -gt 33554432) {
        throw 'Population parity requires exactly one nonempty regular native-population-inputs.json input artifact.'
    }
    if ($RequirePrerequisites) {
        $inputMetadata = [IO.File]::ReadAllText($children[0].FullName) | ConvertFrom-Json -ErrorAction Stop
        Assert-CathedralPopulationExactProperties $inputMetadata @('format','version','policy','caseCount','levelGenerationTableLoaded','freshBestiarySlots','storyMessages','constants','monsterData','cases') 'population prerequisites metadata'
        Assert-CathedralPopulationString $inputMetadata.format 'godevilution-cathedral-initial-inputs' 'population input format'
        Assert-CathedralPopulationInteger $inputMetadata.version 2 2 'population input version'
        Assert-CathedralPopulationInteger $inputMetadata.caseCount 16 16 'population input case count'
        Assert-CathedralPopulationString $inputMetadata.policy 'ordinary-original-diablo-fresh-initial-objects' 'population input policy'
        foreach ($property in @('levelGenerationTableLoaded','freshBestiarySlots')) {
            if ($inputMetadata.$property -isnot [bool] -or -not $inputMetadata.$property) { throw "Population prerequisite $property must be true." }
        }
    }
    $hasher = [Security.Cryptography.SHA256]::Create(); $stream = $null
    try { $stream = [IO.File]::OpenRead($children[0].FullName); $hash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { if ($null -ne $stream) { $stream.Dispose() }; $hasher.Dispose() }
    [pscustomobject]@{
        Name = $children[0].Name
        Bytes = $children[0].Length
        Sha256 = $hash
    }
}

if ($CathedralPopulationParity) {
    if ($CathedralPopulation -or $CathedralDoors -or $CathedralDepths) {
        throw '-CathedralPopulationParity and all other Cathedral modes are mutually exclusive.'
    }
    $CathedralPopulation = $true
}

if ($CathedralPopulation -and ($CathedralDoors -or $CathedralDepths)) {
    throw '-CathedralPopulation, -CathedralDoors, and -CathedralDepths are mutually exclusive.'
}

if ($CathedralDoors -and $CathedralDepths) {
    throw '-CathedralDoors and -CathedralDepths are mutually exclusive.'
}

if ($CathedralPopulation) {
    $oracleDirectoryVariable = 'GODEVILUTION_CATHEDRAL_POPULATION_ORACLE_DIRECTORY'
    $inputDirectoryVariable = 'GODEVILUTION_CATHEDRAL_POPULATION_INPUT_DIRECTORY'
    $nativeBuild = Find-CathedralOracleBuild $repositoryRoot
    $evidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Cathedral-Population-Oracle-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $evidenceRoot) { throw 'Cathedral population verification requires a fresh evidence directory.' }
    $fullEvidenceRoot = [IO.Path]::GetFullPath($evidenceRoot)
    if ($fullEvidenceRoot.StartsWith($repositoryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Licensed Cathedral population inputs must remain outside the repository.'
    }
    New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
    $previousOracleDirectory = [Environment]::GetEnvironmentVariable($oracleDirectoryVariable, 'Process')
    $previousInputDirectory = [Environment]::GetEnvironmentVariable($inputDirectoryVariable, 'Process')
    $record = [ordered]@{
        Passed = $false
        StartedUtc = [DateTime]::UtcNow.ToString('O')
        EndedUtc = $null
        NativeBuild = $nativeBuild.Directory
        Scope = if ($CathedralPopulationParity) { 'native-cathedral-l1-prerequisites-initial-objects' } else { 'native-cathedral-l1-object-population' }
        NativeTestName = 'NativeObjectPopulationLocalAssetOracle'
        Deterministic = $false
        Captures = @()
        Error = $null
        CleanupError = $null
        ManagedParity = [bool]$CathedralPopulationParity
        PrerequisitesParity = [bool]$CathedralPopulationParity
        ManagedOutcome = $null
        Stage = 'native-build'
    }
    $primaryError = $null
    $locationPushed = $false
    try {
        Push-Location $repositoryRoot
        $locationPushed = $true
        Write-Host "Private Cathedral population oracle evidence: $evidenceRoot"
        Write-Host 'This directory contains locally licensed geometry inputs; do not publish it.'
        $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target cathedral_population_test --parallel 2' -f
            $nativeBuild.DeveloperCommand, $nativeBuild.CMake, $nativeBuild.Directory
        $buildOutcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ("/d /c $buildCommand") (Join-Path $evidenceRoot 'native-build.log') 600000
        $record.BuildPid = $buildOutcome.Pid
        $record.BuildExitCode = $buildOutcome.ExitCode
        $record.BuildTimedOut = $buildOutcome.TimedOut
        $record.BuildConfirmedExited = $buildOutcome.ConfirmedExited
        $record.BuildLogRetained = $buildOutcome.LogRetained
        $record.BuildError = $buildOutcome.Error
        $record.BuildCleanupError = $buildOutcome.CleanupError
        if ($null -ne $buildOutcome.Error) { throw $buildOutcome.Error }
        if ($buildOutcome.ExitCode -ne 0) { throw "The native Cathedral population build failed with exit code $($buildOutcome.ExitCode)." }
        if ($CathedralPopulationParity) {
            $record.Stage = 'managed-build'
            $managedBuild = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -ErrorAction Stop).Source 'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --configuration Debug --no-restore --nologo' (Join-Path $evidenceRoot 'managed-build.log') 600000
            $record.ManagedBuildOutcome = $managedBuild
            if ($null -ne $managedBuild.Error) { throw $managedBuild.Error }
            if ($managedBuild.ExitCode -ne 0) { throw "The managed initial-object build failed with exit code $($managedBuild.ExitCode)." }
        }
        $nativeExecutable = Join-Path $nativeBuild.Directory 'cathedral_population_test.exe'
        for ($captureIndex = 0; $captureIndex -lt 2; $captureIndex++) {
            $captureName = if ($captureIndex -eq 0) { 'first' } else { 'repeated' }
            $captureRoot = Join-Path $evidenceRoot ($captureName + '-capture')
            if (Test-Path -LiteralPath $captureRoot) { throw 'A Cathedral population capture directory was not fresh.' }
            New-Item -ItemType Directory -Path $captureRoot | Out-Null
            $artifactRoot = Join-Path $captureRoot 'artifacts'
            New-Item -ItemType Directory -Path $artifactRoot | Out-Null
            $inputRoot = $null
            if ($CathedralPopulationParity) {
                $inputRoot = Join-Path $captureRoot 'inputs'
                New-Item -ItemType Directory -Path $inputRoot | Out-Null
            }
            $reportPath = Join-Path $captureRoot 'native-tests.json'
            $logPath = Join-Path $captureRoot 'native-tests.log'
            $record.Captures += [pscustomobject][ordered]@{
                Name = $captureName
                Root = $captureRoot
                ArtifactDirectory = $artifactRoot
                InputDirectory = $inputRoot
                InputArtifact = $null
                Report = $reportPath
                Log = $logPath
                NativePid = $null
                NativeExitCode = $null
                NativeTimedOut = $false
                NativeConfirmedExited = $false
                NativeLogRetained = $false
                NativeError = $null
                NativeCleanupError = $null
                Artifacts = @()
                Error = $null
            }
            [Environment]::SetEnvironmentVariable($oracleDirectoryVariable, $artifactRoot, 'Process')
            [Environment]::SetEnvironmentVariable($inputDirectoryVariable, $inputRoot, 'Process')
            $record.Stage = 'native-capture'
            $outcome = Invoke-CathedralPopulationNativeOracle $nativeExecutable $reportPath $logPath
            $record.Captures[$captureIndex].NativePid = $outcome.Pid
            $record.Captures[$captureIndex].NativeExitCode = $outcome.ExitCode
            $record.Captures[$captureIndex].NativeTimedOut = $outcome.TimedOut
            $record.Captures[$captureIndex].NativeConfirmedExited = $outcome.ConfirmedExited
            $record.Captures[$captureIndex].NativeLogRetained = $outcome.LogRetained
            $record.Captures[$captureIndex].NativeError = $outcome.Error
            $record.Captures[$captureIndex].NativeCleanupError = $outcome.CleanupError
            if ($null -ne $outcome.Error) { throw $outcome.Error }
            if ($outcome.ExitCode -ne 0) { throw "The native Cathedral population oracle failed with exit code $($outcome.ExitCode)." }
            Assert-CathedralPopulationNativeReport $reportPath
            Assert-CathedralPopulationMetadata $artifactRoot
            $metadata = [IO.File]::ReadAllText((Join-Path $artifactRoot 'metadata.json')) | ConvertFrom-Json
            foreach ($name in (Get-CathedralPopulationOracleArtifactNames | Where-Object { $_ -ne 'metadata.json' })) {
                Assert-CathedralPopulationCase (Join-Path $artifactRoot $name) $name $metadata
            }
            $record.Captures[$captureIndex].Artifacts = @(Get-CathedralPopulationOracleArtifacts $artifactRoot)
            if ($CathedralPopulationParity) {
                $record.Captures[$captureIndex].InputArtifact = Get-CathedralPopulationInputArtifact $inputRoot -RequirePrerequisites
            }
        }
        $record.Stage = 'repeat-validation'
        Assert-CathedralRepeatArtifacts $record.Captures[0].Artifacts $record.Captures[1].Artifacts 17
        if ($CathedralPopulationParity) {
            Assert-CathedralRepeatArtifacts @($record.Captures[0].InputArtifact) @($record.Captures[1].InputArtifact) 1
            [Environment]::SetEnvironmentVariable($oracleDirectoryVariable, $record.Captures[0].ArtifactDirectory, 'Process')
            [Environment]::SetEnvironmentVariable($inputDirectoryVariable, $record.Captures[0].InputDirectory, 'Process')
            $managedLog = Join-Path $evidenceRoot 'managed-tests.log'
            $managedExecutable = Join-Path $repositoryRoot 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe'
            $record.Stage = 'managed-comparison'
            $record.ManagedOutcome = Invoke-CathedralPopulationOwnedProcess $managedExecutable '--no-ansi --filter-class *AuthoritativeCathedralInitialObjectNativeOracleTests --explicit only --fail-skips on' $managedLog 180000
            if ($null -ne $record.ManagedOutcome.Error) { throw $record.ManagedOutcome.Error }
            if ($record.ManagedOutcome.ExitCode -ne 0) { throw "The managed initial-object oracle failed with exit code $($record.ManagedOutcome.ExitCode)." }
            Assert-CathedralManagedReport $managedLog 16
        }
        $record.Deterministic = $true
        $record.Passed = $true
        $record.Stage = 'completed'
    } catch {
        $primaryError = $_
        $record.Error = $_.Exception.Message
        if ($record.Stage -eq 'native-capture' -and $record.Captures.Count -gt 0) { $record.Captures[-1].Error = $record.Error }
    } finally {
        try { [Environment]::SetEnvironmentVariable($oracleDirectoryVariable, $previousOracleDirectory, 'Process') }
        catch { Add-CathedralPopulationCleanupError $record "Could not restore population output environment: $($_.Exception.Message)" }
        try { [Environment]::SetEnvironmentVariable($inputDirectoryVariable, $previousInputDirectory, 'Process') }
        catch { Add-CathedralPopulationCleanupError $record "Could not restore population input environment: $($_.Exception.Message)" }
        try { if ($locationPushed) { Pop-Location } }
        catch { Add-CathedralPopulationCleanupError $record "Could not restore population working directory: $($_.Exception.Message)" }
        $record.EndedUtc = [DateTime]::UtcNow.ToString('O')
    }
    if ($null -eq $primaryError -and $null -ne $record.CleanupError) {
        try { throw $record.CleanupError } catch { $primaryError = $_; $record.Error = $_.Exception.Message }
        $record.Passed = $false
    }
    try {
        [IO.File]::WriteAllText((Join-Path $evidenceRoot 'result.json'), ($record | ConvertTo-Json -Depth 24), [Text.UTF8Encoding]::new($false))
    } catch {
        if ($null -eq $primaryError) { throw }
        Write-Warning "Could not retain the Cathedral population result record: $($_.Exception.Message)"
    }
    if ($null -ne $primaryError) { throw $primaryError }
    if ($CathedralPopulationParity) {
        Write-Host "PASS: sixteen native/managed Cathedral prerequisite and initial-object comparisons and repeated native captures. Evidence: $evidenceRoot"
        return
    }
    Write-Host "PASS: native Cathedral object population captures are repeatable. Evidence: $evidenceRoot"
    return
}

if ($CathedralDoors) {
    $oracleDirectoryVariable = 'GODEVILUTION_CATHEDRAL_DOOR_ORACLE_DIRECTORY'
    $nativeBuild = Find-CathedralOracleBuild $repositoryRoot
    $evidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Cathedral-Door-Oracle-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $evidenceRoot) { throw 'Cathedral door verification requires a fresh evidence directory.' }
    $fullEvidenceRoot = [IO.Path]::GetFullPath($evidenceRoot)
    if ($fullEvidenceRoot.StartsWith($repositoryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Licensed Cathedral door inputs must remain outside the repository.'
    }
    New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
    $previousOracleDirectory = [Environment]::GetEnvironmentVariable($oracleDirectoryVariable, 'Process')
    $record = [ordered]@{
        Passed = $false
        StartedUtc = [DateTime]::UtcNow.ToString('O')
        EndedUtc = $null
        NativeBuild = $nativeBuild.Directory
        Scope = 'native-cathedral-l1-door-oracle'
        NativeTestNames = @('CathedralDoorNativeObjectOracle', 'CathedralDoorNativeDeltaOpenOracle')
        Deterministic = $false
        Captures = @()
        Error = $null
    }
    $primaryError = $null
    $locationPushed = $false
    try {
        Push-Location $repositoryRoot
        $locationPushed = $true
        Write-Host "Private Cathedral door oracle evidence: $evidenceRoot"
        Write-Host 'This directory contains locally licensed geometry inputs; do not publish it.'
        $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target level_l1_test --parallel 2' -f
            $nativeBuild.DeveloperCommand, $nativeBuild.CMake, $nativeBuild.Directory
        & $env:ComSpec /d /c $buildCommand 2>&1 | Tee-Object -FilePath (Join-Path $evidenceRoot 'native-build.log')
        if ($LASTEXITCODE -ne 0) { throw "The native Cathedral door oracle build failed with exit code $LASTEXITCODE." }
        $nativeExecutable = Join-Path $nativeBuild.Directory 'level_l1_test.exe'
        foreach ($captureName in @('first', 'repeated')) {
            $captureRoot = Join-Path $evidenceRoot ($captureName + '-capture')
            if (Test-Path -LiteralPath $captureRoot) { throw 'A Cathedral door capture directory was not fresh.' }
            New-Item -ItemType Directory -Path $captureRoot | Out-Null
            $artifactRoot = Join-Path $captureRoot 'artifacts'
            New-Item -ItemType Directory -Path $artifactRoot | Out-Null
            $capture = [pscustomobject][ordered]@{
                Name = $captureName
                Root = $captureRoot
                ArtifactDirectory = $artifactRoot
                Reports = @()
                Logs = @()
                NativeExitCodes = @()
                SchemaValidationMilliseconds = $null
                Artifacts = @()
                Error = $null
            }
            $record.Captures += $capture
            [Environment]::SetEnvironmentVariable($oracleDirectoryVariable, $artifactRoot, 'Process')
            $testNames = @('CathedralDoorNativeObjectOracle', 'CathedralDoorNativeDeltaOpenOracle')
            foreach ($testName in $testNames) {
                $reportPath = Join-Path $captureRoot ($testName + '.json')
                $logPath = Join-Path $captureRoot ($testName + '.log')
                $exitCode = Invoke-CathedralNativeOracle $nativeExecutable $reportPath $logPath $testName
                $capture.Reports += $reportPath
                $capture.Logs += $logPath
                $capture.NativeExitCodes += $exitCode
                if ($exitCode -ne 0) { throw "The native Cathedral door oracle $testName failed with exit code $exitCode." }
            }
            Assert-CathedralDoorNativeReports @($capture.Reports)
            $schemaTimer = [Diagnostics.Stopwatch]::StartNew()
            Assert-CathedralDoorOracleSchema $artifactRoot
            $schemaTimer.Stop()
            $capture.SchemaValidationMilliseconds = $schemaTimer.Elapsed.TotalMilliseconds
            $capture.Artifacts = @(Get-CathedralDoorOracleArtifacts $artifactRoot)
        }
        $firstCapture = $record.Captures[0]
        $repeatedCapture = $record.Captures[1]
        Assert-CathedralRepeatArtifacts $firstCapture.Artifacts $repeatedCapture.Artifacts 20
        $record.Deterministic = $true
        $record.Passed = $true
    } catch {
        $primaryError = $_
        $record.Error = $_.Exception.Message
        if ($record.Captures.Count -gt 0) {
            $record.Captures[-1].Error = $record.Error
        }
    } finally {
        try {
            [Environment]::SetEnvironmentVariable($oracleDirectoryVariable, $previousOracleDirectory, 'Process')
        } finally {
            if ($locationPushed) { Pop-Location }
        }
        $record.EndedUtc = [DateTime]::UtcNow.ToString('O')
    }
    try {
        [IO.File]::WriteAllText((Join-Path $evidenceRoot 'result.json'), ($record | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false))
    } catch {
        if ($null -eq $primaryError) { throw }
        Write-Warning "Could not retain the Cathedral door result record: $($_.Exception.Message)"
    }
    if ($null -ne $primaryError) { throw $primaryError }
    Write-Host "PASS: native Cathedral door oracle artifacts are repeatable. Evidence: $evidenceRoot"
    return
}

$oracleDirectoryVariable = if ($CathedralDepths) { 'GODEVILUTION_CATHEDRAL_DEPTH_ORACLE_DIRECTORY' }
    else { 'GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY' }
$nativeTestName = if ($CathedralDepths) { 'CathedralDepthFineGeometryLocalAssetOracle' }
    else { 'CathedralFineGeometryLocalAssetOracle' }
$managedTestName = if ($CathedralDepths) { 'NativeLocalAssetDepthFineGeometryMatchesManagedMaterializer' }
    else { 'NativeLocalAssetFineGeometryMatchesManagedMaterializer' }
$expectedComparisons = if ($CathedralDepths) { 48 } else { 12 }
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
$previousOracleDirectory = [Environment]::GetEnvironmentVariable($oracleDirectoryVariable, 'Process')
$record = [ordered]@{
    Passed = $false
    StartedUtc = [DateTime]::UtcNow.ToString('O')
    EndedUtc = $null
    NativeBuild = $nativeBuild.Directory
    ArtifactDirectory = $artifactRoot
    Scope = if ($CathedralDepths) { 'ordinary-cathedral-depths-1-4' } else { 'ordinary-cathedral-level-1' }
    ExpectedComparisons = $expectedComparisons
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

    [Environment]::SetEnvironmentVariable($oracleDirectoryVariable, $artifactRoot, 'Process')
    $nativeExecutable = Join-Path $nativeBuild.Directory 'level_l1_test.exe'
    $nativeReport = Join-Path $evidenceRoot 'native-tests.json'
    $record.NativeExitCode = Invoke-CathedralNativeOracle $nativeExecutable $nativeReport (Join-Path $evidenceRoot 'native-tests.log') $nativeTestName
    if ($record.NativeExitCode -ne 0) { throw "The native oracle failed with exit code $($record.NativeExitCode)." }
    Assert-CathedralNativeReport $nativeReport $nativeTestName
    $record.Artifacts = @(Get-CathedralOracleArtifacts $artifactRoot -CathedralDepths:$CathedralDepths)

    $managedLog = Join-Path $evidenceRoot 'managed-tests.log'
    & server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe --no-ansi --filter-method "Devilution.Diablo.Tests.AuthoritativeCathedralNativeGeometryOracleTests.$managedTestName" --explicit only 2>&1 |
        Tee-Object -FilePath $managedLog
    $record.ManagedExitCode = $LASTEXITCODE
    if ($record.ManagedExitCode -ne 0) { throw "The managed oracle failed with exit code $($record.ManagedExitCode)." }
    Assert-CathedralManagedReport $managedLog $expectedComparisons
    $record.Passed = $true
} catch {
    $primaryError = $_
    $record.Error = $_.Exception.Message
} finally {
    [Environment]::SetEnvironmentVariable($oracleDirectoryVariable, $previousOracleDirectory, 'Process')
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
if ($CathedralDepths) {
    Write-Host "PASS: 48 native/managed Cathedral fine-coordinate comparisons. Evidence: $evidenceRoot"
} else {
    Write-Host "PASS: twelve native/managed Cathedral fine-coordinate comparisons. Evidence: $evidenceRoot"
}
