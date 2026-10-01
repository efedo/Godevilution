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
#>
[CmdletBinding()]
param(
    [switch]$CathedralDepths,
    [switch]$CathedralDoors)

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

if ($CathedralDoors -and $CathedralDepths) {
    throw '-CathedralDoors and -CathedralDepths are mutually exclusive.'
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
