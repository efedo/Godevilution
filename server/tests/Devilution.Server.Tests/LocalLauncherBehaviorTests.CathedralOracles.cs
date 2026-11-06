using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{

    [Fact]
    public void CathedralOracleHelpersRejectIncompleteOrSkippedEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-CathedralNativeReport", "Assert-CathedralManagedReport", "Get-CathedralOracleArtifactNames", "Get-CathedralOracleArtifacts"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-OracleHelperTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw "Incomplete oracle evidence was accepted by $Action. Native field=$field; managed summary=$badSummary" }
            }
            try {
                $reportPath = Join-Path $work 'native.json'
                Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                $report = [ordered]@{
                    tests = 1; failures = 0; errors = 0; disabled = 0
                    testsuites = @(@{ testsuite = @(@{
                        classname = 'Drlg_l1'; name = 'CathedralFineGeometryLocalAssetOracle'
                        status = 'RUN'; result = 'COMPLETED'
                    }) })
                }
                function Save-NativeReport { [IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 6)) }
                Save-NativeReport
                Assert-CathedralNativeReport $reportPath
                Expect-Rejected { Assert-CathedralNativeReport $reportPath 'CathedralDepthFineGeometryLocalAssetOracle' }
                $report.testsuites[0].testsuite[0].name = 'CathedralDepthFineGeometryLocalAssetOracle'
                Save-NativeReport
                Assert-CathedralNativeReport $reportPath 'CathedralDepthFineGeometryLocalAssetOracle'
                Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                $report.testsuites[0].testsuite[0].result = 'SKIPPED'
                Save-NativeReport
                Expect-Rejected { Assert-CathedralNativeReport $reportPath 'CathedralDepthFineGeometryLocalAssetOracle' }
                $report.testsuites[0].testsuite[0].name = 'CathedralFineGeometryLocalAssetOracle'
                $report.testsuites[0].testsuite[0].result = 'COMPLETED'
                foreach ($field in @('tests', 'failures', 'errors', 'disabled')) {
                    $original = $report[$field]
                    $report[$field] = if ($field -eq 'tests') { 0 } else { 1 }
                    Save-NativeReport
                    Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                    $report[$field] = $original
                }
                foreach ($field in @('classname', 'name', 'status', 'result')) {
                    $nativeCase = $report.testsuites[0].testsuite[0]
                    $original = $nativeCase[$field]
                    $nativeCase[$field] = if ($field -eq 'result') { 'SKIPPED' } else { 'wrong' }
                    Save-NativeReport
                    Expect-Rejected { Assert-CathedralNativeReport $reportPath }
                    $nativeCase[$field] = $original
                }
                [IO.File]::WriteAllText($reportPath, '{}')
                Expect-Rejected { Assert-CathedralNativeReport $reportPath }

                $managedPath = Join-Path $work 'managed.log'
                $goodSummary = "Test run summary: Passed! - oracle.dll`r`n  total: 12`r`n  failed: 0`r`n  succeeded: 12`r`n  skipped: 0`r`n"
                [IO.File]::WriteAllText($managedPath, $goodSummary)
                Assert-CathedralManagedReport $managedPath
                Expect-Rejected { Assert-CathedralManagedReport $managedPath 48 }
                $depthSummary = $goodSummary.Replace('total: 12', 'total: 48').Replace('succeeded: 12', 'succeeded: 48')
                [IO.File]::WriteAllText($managedPath, $depthSummary)
                Assert-CathedralManagedReport $managedPath 48
                Expect-Rejected { Assert-CathedralManagedReport $managedPath }
                foreach ($badSummary in @($depthSummary.Replace('total: 48', 'total: 47'),
                        $depthSummary.Replace('succeeded: 48', 'succeeded: 47'),
                        $depthSummary.Replace('failed: 0', 'failed: 1'),
                        $depthSummary.Replace('skipped: 0', 'skipped: 1'), ($depthSummary + $depthSummary))) {
                    [IO.File]::WriteAllText($managedPath, $badSummary)
                    Expect-Rejected { Assert-CathedralManagedReport $managedPath 48 }
                }
                foreach ($badSummary in @('', 'Test run summary: Failed!',
                        $goodSummary.Replace('total: 12', 'total: 0'),
                        $goodSummary.Replace('total: 12', 'total: 4').Replace('succeeded: 12', 'succeeded: 4'),
                        $goodSummary.Replace('failed: 0', 'failed: 1'),
                        $goodSummary.Replace('succeeded: 12', 'succeeded: 11'),
                        $goodSummary.Replace('skipped: 0', 'skipped: 1'),
                        ($goodSummary + $goodSummary),
                        ($goodSummary + 'Test run summary: Failed!'))) {
                    [IO.File]::WriteAllText($managedPath, $badSummary)
                    Expect-Rejected { Assert-CathedralManagedReport $managedPath }
                }

                Expect-Rejected { Get-CathedralOracleArtifacts $work }
                $artifactNames = @('l1.til', 'l1.sol', 'cathedral-2588-main.json', 'cathedral-2588-previous.json',
                    'cathedral-743271966-main.json', 'cathedral-743271966-previous.json',
                    'cathedral-0-main.json', 'cathedral-0-previous.json',
                    'cathedral-1-main.json', 'cathedral-1-previous.json',
                    'cathedral-2147483648-main.json', 'cathedral-2147483648-previous.json',
                    'cathedral-4294967295-main.json', 'cathedral-4294967295-previous.json')
                foreach ($name in $artifactNames) { [IO.File]::WriteAllText((Join-Path $work $name), 'nonlicensed synthetic input') }
                if (@(Compare-Object $artifactNames @(Get-CathedralOracleArtifactNames)).Count -ne 0) {
                    throw 'The legacy level-1 artifact contract changed.'
                }
                $artifacts = @(Get-CathedralOracleArtifacts $work)
                if ($artifacts.Count -ne 14 -or @($artifacts | Where-Object { $_.Sha256 -notmatch '^[0-9a-f]{64}$' -or $_.Bytes -le 0 }).Count -ne 0) {
                    throw 'Complete oracle artifacts were not hashed.'
                }
                [IO.File]::WriteAllText((Join-Path $work 'l1.sol'), '')
                Expect-Rejected { Get-CathedralOracleArtifacts $work }
                Expect-Rejected { Get-CathedralOracleArtifacts $work -CathedralDepths }
                $depthNames = @(Get-CathedralOracleArtifactNames -CathedralDepths)
                if ($depthNames.Count -ne 50 -or @($depthNames | Select-Object -Unique).Count -ne 50) {
                    throw 'Depth oracle must require two raw inputs and 48 unique comparisons.'
                }
                foreach ($depth in 1..4) {
                    foreach ($seed in @('2588', '743271966', '0', '1', '2147483648', '4294967295')) {
                        foreach ($entry in @('main', 'previous')) {
                            if ("cathedral-depth-$depth-$seed-$entry.json" -notin $depthNames) {
                                throw 'Depth oracle omitted a mandatory depth/seed/entry.'
                            }
                        }
                    }
                }
                foreach ($name in $depthNames) { [IO.File]::WriteAllText((Join-Path $work $name), 'nonlicensed synthetic input') }
                $depthArtifacts = @(Get-CathedralOracleArtifacts $work -CathedralDepths)
                if ($depthArtifacts.Count -ne 50 -or @($depthArtifacts | Where-Object { $_.Sha256 -notmatch '^[0-9a-f]{64}$' -or $_.Bytes -le 0 }).Count -ne 0) {
                    throw 'Complete depth oracle artifacts were not hashed.'
                }
                [IO.File]::WriteAllText((Join-Path $work 'cathedral-depth-4-4294967295-previous.json'), '')
                Expect-Rejected { Get-CathedralOracleArtifacts $work -CathedralDepths }
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-OracleHelperTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralDoorOracleHelpersRequireTheFrozenReportsArtifactsAndScope()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            [
                "Assert-CathedralDoorNativeReports", "Get-CathedralDoorOracleArtifactNames",
                "Get-CathedralDoorOracleArtifacts", "Assert-CathedralDoorRequiredProperty", "Assert-CathedralDoorInteger",
                "Assert-CathedralDoorLayer", "Assert-CathedralDoorLayerHashes", "Assert-CathedralDoorTile",
                "Assert-CathedralDoorObjectState", "Assert-CathedralDoorOracleCase", "Assert-CathedralDoorOracleSchema",
            ],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-DoorOracleHelperTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            function Copy-Record($Record) {
                $copy = [ordered]@{}
                foreach ($key in $Record.Keys) { $copy[$key] = $Record[$key] }
                return $copy
            }
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Invalid Cathedral door oracle evidence was accepted.' }
            }
            try {
                $objectReport = Join-Path $work 'object.json'
                $deltaReport = Join-Path $work 'delta.json'
                function Save-Report([string]$Path, [string]$Name, [string]$Result = 'COMPLETED') {
                    $report = [ordered]@{
                        tests = 1; failures = 0; errors = 0; disabled = 0
                        testsuites = @(@{ testsuite = @(@{
                            classname = 'Drlg_l1'; name = $Name; status = 'RUN'; result = $Result
                        }) })
                    }
                    [IO.File]::WriteAllText($Path, ($report | ConvertTo-Json -Depth 6))
                }
                Save-Report $objectReport 'CathedralDoorNativeObjectOracle'
                Save-Report $deltaReport 'CathedralDoorNativeDeltaOpenOracle'
                Assert-CathedralDoorNativeReports @($objectReport, $deltaReport)
                Assert-CathedralDoorInteger 4294967295 0 4294967295 'synthetic.uint32'
                foreach ($badInteger in @($true, '1', 0.5, 1.0000000000000002, 1e-29, -1, 4294967296)) {
                    Expect-Rejected { Assert-CathedralDoorInteger $badInteger 0 4294967295 'synthetic.uint32' }
                }
                foreach ($badCount in @('1', $true, 1.5)) {
                    $badReport = [IO.File]::ReadAllText($objectReport) | ConvertFrom-Json
                    $badReport.tests = $badCount
                    [IO.File]::WriteAllText($objectReport, ($badReport | ConvertTo-Json -Depth 6))
                    Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }
                    Save-Report $objectReport 'CathedralDoorNativeObjectOracle'
                }
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport) }
                Expect-Rejected { Assert-CathedralDoorNativeReports @($deltaReport, $objectReport) }
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $objectReport) }
                Save-Report $deltaReport 'CathedralDoorNativeDeltaOpenOracle' 'SKIPPED'
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }
                Save-Report $deltaReport 'wrong-case'
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }
                [IO.File]::WriteAllText($objectReport, '{}')
                Expect-Rejected { Assert-CathedralDoorNativeReports @($objectReport, $deltaReport) }

                $names = @(Get-CathedralDoorOracleArtifactNames)
                if ($names.Count -ne 20 -or @($names | Select-Object -Unique).Count -ne 20 -or
                    'l1.til' -notin $names -or 'l1.sol' -notin $names -or
                    'door-oracle-format.json' -notin $names -or
                    'cathedral-door-delta-depth-2-743271966-main.json' -notin $names) {
                    throw 'The frozen Cathedral door artifact contract is incomplete.'
                }
                $artifactDirectory = Join-Path $work 'artifacts'
                New-Item -ItemType Directory -Path $artifactDirectory | Out-Null
                foreach ($name in $names) { [IO.File]::WriteAllText((Join-Path $artifactDirectory $name), 'synthetic native evidence') }
                $artifacts = @(Get-CathedralDoorOracleArtifacts $artifactDirectory)
                if ($artifacts.Count -ne 20 -or @($artifacts | Where-Object { $_.Bytes -le 0 -or $_.Sha256 -cnotmatch '^[0-9a-f]{64}$' }).Count -ne 0) {
                    throw 'The complete Cathedral door artifact set was not hashed.'
                }
                [IO.File]::WriteAllText((Join-Path $artifactDirectory 'unexpected.json'), 'out of scope')
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.File]::Delete((Join-Path $artifactDirectory 'unexpected.json'))
                $hidden = Join-Path $artifactDirectory '.hidden-extra'
                [IO.File]::WriteAllText($hidden, 'hidden out of scope')
                [IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Hidden)
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.File]::Delete($hidden)
                $hiddenDirectory = Join-Path $artifactDirectory 'hidden-directory'
                New-Item -ItemType Directory -Path $hiddenDirectory | Out-Null
                [IO.File]::SetAttributes($hiddenDirectory, [IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::Directory)
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.Directory]::Delete($hiddenDirectory)
                $lower = Join-Path $artifactDirectory 'l1.til'
                $upper = Join-Path $artifactDirectory 'L1.TIL'
                $renameStage = Join-Path $artifactDirectory 'rename-stage.tmp'
                [IO.File]::Move($lower, $renameStage)
                [IO.File]::Move($renameStage, $upper)
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }
                [IO.File]::Move($upper, $renameStage)
                [IO.File]::Move($renameStage, $lower)
                [IO.File]::Delete((Join-Path $artifactDirectory 'cathedral-door-depth-4-743271966-previous.json'))
                Expect-Rejected { Get-CathedralDoorOracleArtifacts $artifactDirectory }

                $badCase = Join-Path $work 'cathedral-door-depth-1-2588-main.json'
                [IO.File]::WriteAllText($badCase, '{}')
                Expect-Rejected { Assert-CathedralDoorOracleCase $badCase 'cathedral-door-depth-1-2588-main.json' 'sync' }
                $metadata = Join-Path $work 'door-oracle-format.json'
                [IO.File]::WriteAllText($metadata, '{}')
                Expect-Rejected { Assert-CathedralDoorOracleSchema $work }
                $validMetadata = [ordered]@{ format = 'godevilution-cathedral-l1-door-oracle'; version = 1;
                    policy = 'native-add-l1-objects-door-only'; caseCount = 16; depths = @(1,2,3,4);
                    seeds = @(2588,743271966); entries = @('main','previous'); rawAssets = @('l1.til','l1.sol');
                    width = 112; height = 112; rowMajor = 'y*width+x'; deltaCase = 'cathedral-door-delta-depth-2-743271966-main.json' }
                foreach ($field in @('version', 'width', 'seeds')) {
                    $testMetadata = Copy-Record $validMetadata
                    if ($field -eq 'seeds') { $testMetadata.seeds = @('2588',743271966) }
                    else { $testMetadata.$field = [string]$testMetadata.$field }
                    [IO.File]::WriteAllText($metadata, ($testMetadata | ConvertTo-Json -Depth 4))
                    $message = ''
                    try { Assert-CathedralDoorOracleSchema $work } catch { $message = $_.Exception.Message }
                    if ($message -notlike "*metadata.$field*") { throw "The $field metadata type guard was not exercised." }
                }

                $casePath = Join-Path $work 'synthetic-valid-case.json'
                $cellCount = 112 * 112
                function New-Layer {
                    $values = [int[]]::new($cellCount)
                    return [ordered]@{ pieces = $values; special = $values.Clone(); transparency = $values.Clone();
                        properties = $values.Clone(); solid = $values.Clone(); missileBlocked = $values.Clone();
                        blockLight = $values.Clone(); walkable = $values.Clone(); itemBlocking = $values.Clone() }
                }
                function New-Hashes {
                    return [ordered]@{ fullLayer = '0x0000000000000000'; pieces = '0x0000000000000000';
                        special = '0x0000000000000000'; transparency = '0x0000000000000000';
                        properties = '0x0000000000000000'; solid = '0x0000000000000000';
                        missileBlocked = '0x0000000000000000'; blockLight = '0x0000000000000000';
                        walkable = '0x0000000000000000'; itemBlocking = '0x0000000000000000' }
                }
                function New-ObjectState([int]$State, [int]$Frame, [int]$Selection, [bool]$Pre, [bool]$Miss) {
                    return [ordered]@{ state = $State; var1OneBased = 1; var2OneBased = 1; animationFrame = $Frame;
                        selectionRegion = $Selection; doorFlag = $true; preFlag = $Pre; missFlag = $Miss; solidFlag = $false }
                }
                function New-Tile([int]$Piece) {
                    return [ordered]@{ piece = $Piece; special = 0; transparency = 0; properties = 0; solid = 0;
                        missileBlocked = 0; blockLight = 0; walkable = 0; itemBlocking = 0 }
                }
                $layer = New-Layer
                $hashes = New-Hashes
                $closedObject = New-ObjectState 0 0 3 $false $false
                $openObject = New-ObjectState 1 2 2 $true $true
                $reclosedObject = New-ObjectState 0 0 3 $false $false
                $closedTile = New-Tile 0
                $openTile = New-Tile 1
                $changed = [ordered]@{ index = 0; x = 0; y = 0; closed = $closedTile; open = $openTile; reclosed = $closedTile }
                $door = [ordered]@{ ordinal = 0; freshBaselineOrdinal = 1; openPath = 'sync'; nativeTypeId = 1;
                    nativeType = 'OBJ_L1LDOOR'; position = @(1, 1); var1OneBased = 1; var2OneBased = 1;
                    sourcePieceObserved = 0; neighborPieceObserved = 0; closedObject = $closedObject;
                    openObject = $openObject; reclosedObject = $reclosedObject; closedLayerHash = '0x0000000000000000';
                    openLayerHash = '0x0000000000000001'; reclosedLayerHash = '0x0000000000000000';
                    reclosedLayerEqualsClosed = $true; reclosedObjectEqualsClosed = $true; changedCells = @($changed) }
                $syntheticCase = [ordered]@{ format = 'godevilution-cathedral-l1-door-oracle'; version = 1;
                    policy = 'native-add-l1-objects-door-only'; level = 1; entry = 'main'; inputSeed = 2588;
                    attemptSeed = 0; finalRandomState = 0; doorScanRandomStateBefore = 0; doorScanRandomStateAfter = 0;
                    nativeWidth = 112; nativeHeight = 112; rowMajor = 'y*nativeWidth+x'; baselineRebuildCount = 1;
                    closedBaselineHashes = $hashes; closedBaseline = $layer;
                    closedBaselines = @([ordered]@{ ordinal = 1; doorScanRandomStateBefore = 0; doorScanRandomStateAfter = 0;
                        hashes = $hashes; layer = $layer }); doors = @($door) }
                $validJson = $syntheticCase | ConvertTo-Json -Compress -Depth 8
                [IO.File]::WriteAllText($casePath, $validJson)
                Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync'
                function Check-CaseMutation([scriptblock]$Mutation) {
                    $testCase = $validJson | ConvertFrom-Json
                    & $Mutation $testCase
                    [IO.File]::WriteAllText($casePath, ($testCase | ConvertTo-Json -Compress -Depth 8))
                    Expect-Rejected { Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync' }
                }
                foreach ($field in @('version', 'nativeWidth', 'nativeHeight', 'inputSeed', 'attemptSeed')) {
                    Check-CaseMutation { param($testCase) $testCase.$field = [string]$testCase.$field }
                }
                foreach ($badValue in @($true, '0', 0.5, 1.0000000000000002, 1e-29, -1, 65536)) {
                    $badLayer = ($validJson | ConvertFrom-Json).closedBaseline
                    $badLayer.pieces[0] = $badValue
                    $parsedBadLayer = ($badLayer | ConvertTo-Json -Compress -Depth 3) | ConvertFrom-Json
                    Expect-Rejected { Assert-CathedralDoorLayer $parsedBadLayer 'synthetic.closedBaseline' }
                    Check-CaseMutation { param($testCase) $testCase.closedBaseline.pieces[0] = $badValue }
                }
                Check-CaseMutation { param($testCase) $testCase.doors[0].changedCells[0].index = '0' }
                Check-CaseMutation { param($testCase) $testCase.doors[0].changedCells += $testCase.doors[0].changedCells[0] }
                Check-CaseMutation { param($testCase) $testCase.doors[0].changedCells[0].closed.piece = 2 }
                Check-CaseMutation { param($testCase) $testCase.closedBaseline.pieces[0] = 1 }
                Check-CaseMutation { param($testCase) $testCase.doors[0].closedLayerHash = '0x1111111111111111' }
                Check-CaseMutation { param($testCase) $testCase.doors[0].reclosedLayerEqualsClosed = $false }
                Check-CaseMutation { param($testCase) $testCase.doors[0].reclosedObjectEqualsClosed = $false }
                Check-CaseMutation { param($testCase) $testCase.doors[0].reclosedLayerHash = '0x1111111111111111' }
                Check-CaseMutation { param($testCase) $testCase.doors[0].openObject.animationFrame = 1 }
                Check-CaseMutation { param($testCase) $testCase.doors[0].openObject.missFlag = $false }
                $nonRestoringCase = $validJson | ConvertFrom-Json
                $nonRestoringCase.doors[0].changedCells[0].reclosed.piece = 2
                $nonRestoringCase.doors[0].reclosedLayerEqualsClosed = $false
                $nonRestoringCase.doors[0].reclosedLayerHash = '0x0000000000000002'
                [IO.File]::WriteAllText($casePath, ($nonRestoringCase | ConvertTo-Json -Compress -Depth 8))
                Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync'
                $noOpDoor = Copy-Record $door
                $noOpDoor.ordinal = 1
                $noOpDoor.openObject = Copy-Record $closedObject
                $noOpDoor.reclosedObject = Copy-Record $closedObject
                $noOpDoor.changedCells = @([ordered]@{ index = 0; x = 0; y = 0; closed = $closedTile; open = $closedTile; reclosed = $closedTile })
                $syntheticCase.doors = @($door, $noOpDoor)
                [IO.File]::WriteAllText($casePath, ($syntheticCase | ConvertTo-Json -Depth 8))
                Expect-Rejected { Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync' }
                $badLayer = Copy-Record $layer
                $badPieces = [object[]]$layer.pieces.Clone()
                $badPieces[0] = 'not-an-integer'
                $badLayer.pieces = $badPieces
                $syntheticCase.doors = @($door)
                $syntheticCase.closedBaseline = $badLayer
                $syntheticCase.closedBaselines = @([ordered]@{ ordinal = 1; doorScanRandomStateBefore = 0; doorScanRandomStateAfter = 0;
                    hashes = $hashes; layer = $badLayer })
                [IO.File]::WriteAllText($casePath, ($syntheticCase | ConvertTo-Json -Depth 8))
                Expect-Rejected { Assert-CathedralDoorOracleCase $casePath 'cathedral-door-depth-1-2588-main.json' 'sync' }
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-DoorOracleHelperTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralDoorOracleRepeatabilityRejectsChangedTwentyArtifactCaptures()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-CathedralRepeatArtifacts"],
            """
            $first = @(1..20 | ForEach-Object {
                [pscustomobject]@{ Name = ('artifact-{0:D2}.json' -f $_); Bytes = $_ + 1; Sha256 = (('{0:x2}' -f $_) * 32) }
            })
            $repeated = @($first | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Bytes = $_.Bytes; Sha256 = $_.Sha256 } })
            Assert-CathedralRepeatArtifacts $first $repeated 20
            $repeated[19].Sha256 = ('f' * 64)
            $rejected = $false
            try { Assert-CathedralRepeatArtifacts $first $repeated 20 } catch { $rejected = $true }
            if (-not $rejected) { throw 'A changed twentieth artifact hash was accepted.' }
            Write-Output 'PASS'
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralDoorOracleModeIsMutuallyExclusiveWithDepthMode()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var scriptPath = Path.Combine(FindRepositoryRoot(), "tools", "test-cathedral-geometry.ps1");
        var script = File.ReadAllText(scriptPath);
        Assert.Contains("[switch]$CathedralDoors", script, StringComparison.Ordinal);
        Assert.Contains("if ($CathedralDoors -and $CathedralDepths)", script, StringComparison.Ordinal);
        Assert.Contains("-CathedralDoors and -CathedralDepths are mutually exclusive.", script, StringComparison.Ordinal);
        Assert.Contains("PASS", RunPowerShell($$"""
            $rejected = $false
            try { & {{QuotePowerShell(scriptPath)}} -CathedralDoors -CathedralDepths }
            catch {
                if ($_.Exception.Message -notlike '*mutually exclusive*') { throw }
                $rejected = $true
            }
            if (-not $rejected) { throw 'Conflicting oracle modes were accepted.' }
            Write-Output 'PASS'
            """), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralOracleRepeatabilityRejectsChangedOrIncompleteCaptures()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-CathedralRepeatArtifacts"],
            """
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Invalid repeated native evidence was accepted.' }
            }
            $first = @(
                [pscustomobject]@{ Name = 'first.json'; Bytes = 11; Sha256 = ('a' * 64) },
                [pscustomobject]@{ Name = 'second.json'; Bytes = 22; Sha256 = ('b' * 64) })
            function New-RepeatedCapture {
                @($first | ForEach-Object {
                    [pscustomobject]@{ Name = $_.Name; Bytes = $_.Bytes; Sha256 = $_.Sha256 }
                })
            }
            $repeated = @(New-RepeatedCapture)
            Assert-CathedralRepeatArtifacts $first $repeated 2
            Assert-CathedralRepeatArtifacts $first @($repeated[1], $repeated[0]) 2
            Expect-Rejected { Assert-CathedralRepeatArtifacts $first @($repeated[0]) 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts @($first[0]) $repeated 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts $first @($repeated[0], $repeated[0]) 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts @($first[0], $first[0]) $repeated 2 }
            Expect-Rejected { Assert-CathedralRepeatArtifacts $first $repeated 0 }
            foreach ($field in @('Name', 'Bytes', 'Sha256')) {
                foreach ($invalid in $(switch ($field) {
                    'Name' { @('', 'FIRST.json', 'missing.json') }
                    'Bytes' { @(0, 12) }
                    'Sha256' { @('', 'malformed', ('A' * 64), ('c' * 64)) }
                })) {
                    $repeated = @(New-RepeatedCapture)
                    $repeated[0].$field = $invalid
                    Expect-Rejected { Assert-CathedralRepeatArtifacts $first $repeated 2 }
                    Expect-Rejected { Assert-CathedralRepeatArtifacts $repeated $first 2 }
                }
            }
            Write-Output 'PASS'
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralOracleBuildSelectionRequiresTheMatchingDebugSourceTree()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Find-CathedralOracleBuild"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-OracleBuildTest-' + [guid]::NewGuid().ToString('N'))
            $build = Join-Path $work 'build/x64-Debug-authoritative-vcpkg'
            $vs = Join-Path $work 'vs'
            $developer = Join-Path $vs 'Common7/Tools/VsDevCmd.bat'
            $cmake = Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
            New-Item -ItemType Directory -Path $build, ([IO.Path]::GetDirectoryName($developer)), ([IO.Path]::GetDirectoryName($cmake)) | Out-Null
            [IO.File]::WriteAllText($developer, 'synthetic developer environment')
            [IO.File]::WriteAllText($cmake, 'synthetic CMake path')
            $compiler = Join-Path $vs 'VC/Tools/MSVC/test/bin/Hostx64/x64/cl.exe'
            $cachePath = Join-Path $build 'CMakeCache.txt'
            function Save-Cache([string]$Source, [string]$Mode, [string]$Testing, [string]$Compiler) {
                [IO.File]::WriteAllText($cachePath, "UNPACKED_MPQS:BOOL=OFF`r`nBUILD_TESTING:BOOL=$Testing`r`nCMAKE_BUILD_TYPE:STRING=$Mode`r`nCMAKE_HOME_DIRECTORY:INTERNAL=$Source`r`nCMAKE_CXX_COMPILER:FILEPATH=$Compiler`r`n")
            }
            function Expect-Rejected {
                $rejected = $false
                try { Find-CathedralOracleBuild $work | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'An incompatible native build tree was selected.' }
            }
            try {
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' $compiler
                $selected = Find-CathedralOracleBuild $work
                if ($selected.Directory -ne $build -or $selected.DeveloperCommand -ne $developer -or $selected.CMake -ne $cmake) {
                    throw 'The matching developer environment was not selected.'
                }
                Save-Cache (Join-Path $work 'other-project') 'Debug' 'ON' $compiler
                Expect-Rejected
                Save-Cache $work 'Release' 'ON' $compiler
                Expect-Rejected
                Save-Cache $work 'Debug' 'OFF' $compiler
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' (Join-Path $vs 'bin/clang.exe')
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' 'C:/missing-vs/VC/cl.exe'
                Expect-Rejected
                Save-Cache $work 'Debug' 'ON' $compiler
                [IO.File]::WriteAllText($cachePath, ([IO.File]::ReadAllText($cachePath).Replace('UNPACKED_MPQS:BOOL=OFF', 'UNPACKED_MPQS:BOOL=ON')))
                Expect-Rejected
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-OracleBuildTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CathedralOracleNativeInvocationRetainsStderrAndNonzeroExitCodes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Invoke-CathedralNativeOracle"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-OracleInvokeTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                $child = Join-Path $work 'harmless native child.cmd'
                foreach ($testName in @('CathedralFineGeometryLocalAssetOracle', 'CathedralDepthFineGeometryLocalAssetOracle',
                        'CathedralDoorNativeObjectOracle', 'CathedralDoorNativeDeltaOpenOracle')) {
                  foreach ($expectedCode in @(0, 23, -1073741819)) {
                    [IO.File]::WriteAllText($child, "@echo off`r`necho synthetic oracle stdout %*`r`necho INFO: synthetic oracle stderr 1>&2`r`nexit /b $expectedCode`r`n")
                    $log = Join-Path $work 'retained native log.txt'
                    $actualCode = Invoke-CathedralNativeOracle $child (Join-Path $work 'unused report.json') $log $testName
                    if ($actualCode -ne $expectedCode) { throw "Native exit $expectedCode became $actualCode." }
                    $output = [IO.File]::ReadAllText($log)
                    if ($output -notmatch 'synthetic oracle stdout' -or $output -notmatch 'synthetic oracle stderr' -or
                        $output -notmatch [regex]::Escape("--gtest_filter=Drlg_l1.$testName")) {
                        throw 'Native output was not retained.'
                    }
                  }
                }
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-OracleInvokeTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """,
            "tools/test-cathedral-geometry.ps1");

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }
}
