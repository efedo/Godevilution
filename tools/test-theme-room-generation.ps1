<#
    Detached ordinary Cathedral CreateThemeRooms and zero-offset static lighting.
    Zero arguments; bounded owned children and private external native evidence.
    Earlier item corpora, clients, launch defaults and user saves are untouched.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$roomRepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
# Import declarations only; never execute either shared runner's body.
foreach ($roomImport in @(
        @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
            'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
            'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
        @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') })) {
    $roomTokens = $null; $roomParseErrors = $null
    $roomAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $roomImport.File), [ref]$roomTokens, [ref]$roomParseErrors)
    if ($roomParseErrors.Count) { throw 'Shared theme-room oracle declarations did not parse.' }
    foreach ($roomName in $roomImport.Names) {
        $roomDeclarations = @($roomAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $roomName
        }, $true))
        if ($roomDeclarations.Count -ne 1) { throw "Expected exactly one shared declaration for $roomName." }
        . ([scriptblock]::Create($roomDeclarations[0].Extent.Text))
    }
}

function Assert-ThemeRoomNativeReport([string]$Path) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    $names = @(
        'ThemeStaticLightingTest.RadiusFivePreservesNativeMinimumAndLoadingCopy',
        'ThemeStaticLightingTest.CaptureZeroOffsetNativeStaticLighting',
        'ThemeRoomGenerationTest.FullObjectPoolRetainsSlotsAndFailedShrineCarry',
        'ThemeRoomGenerationTest.NoSkeletonBarrelRetainsMinusOneWithoutSelectionDraw',
        'ThemeRoomGenerationTest.BlockedSkeletonWithNoNeighborRetainsReservationWithoutDraw',
        'ThemeRoomGenerationTest.SignedSkeletonFallbackUsesNativeUnsignedSizeRemainder',
        'ThemeRoomGenerationTest.BothMultiplayerOnlyShrineSelectionsRetryBeforeAnimationCoin',
        'ThemeRoomGenerationTest.CaptureCompleteSelectedThemePass')
    if ($report.tests -ne 8 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne 8 -or @($cases | Where-Object {
            $_.status -cne 'RUN' -or $_.result -cne 'COMPLETED' -or
            @($_.failures).Where({ $null -ne $_ }).Count -ne 0
        }).Count -ne 0 -or @(Compare-Object $names @($cases | ForEach-Object { $_.classname + '.' + $_.name }) -CaseSensitive).Count) {
        throw 'All eight exact native room/static-light fixtures must complete without skips.'
    }
}

function Get-ThemeRoomArtifact([string]$Directory, [string]$Name, [long]$MaximumBytes) {
    $root = Get-Item -LiteralPath $Directory -Force
    $entries = @(Get-ChildItem -LiteralPath $Directory -Force)
    if (-not $root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries.Count -ne 1 -or $entries[0].Name -cne $Name -or $entries[0].PSIsContainer -or
        ($entries[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries[0].Length -le 0 -or $entries[0].Length -gt $MaximumBytes) {
        throw 'Theme evidence requires exactly one nonempty bounded regular corpus per directory.'
    }
    $stream = $null; $sha = $null
    try {
        $stream = [IO.File]::OpenRead($entries[0].FullName)
        if ($stream.Length -ne $entries[0].Length -or $stream.Length -le 0 -or $stream.Length -gt $MaximumBytes) {
            throw 'Theme artifact size changed before hashing.'
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        return [pscustomobject]@{ Name = $Name; Path = $entries[0].FullName; Length = $stream.Length
            Sha256 = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    } finally {
        if ($null -ne $sha) { $sha.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Assert-ThemeRoomRepeatability($First, $Second) {
    if (@($First).Count -ne 2 -or @($Second).Count -ne 2) { throw 'Both room and static-light artifacts are required.' }
    foreach ($index in 0..1) {
        if ($First[$index].Name -cne $Second[$index].Name -or
            $First[$index].Length -ne $Second[$index].Length -or $First[$index].Sha256 -cne $Second[$index].Sha256) {
            throw 'Independent native theme-room/static-light captures differ.'
        }
    }
}

function Assert-ThemeRoomManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $cases = @($report.SelectNodes('//test'))
    $assemblies = @($report.SelectNodes('//assembly'))
    $roomType = 'Devilution.Diablo.Tests.AuthoritativeCathedralThemeRoomNativeOracleTests'
    $lightType = 'Devilution.Diablo.Tests.AuthoritativeCathedralThemeStaticLightNativeOracleTests'
    $names = @(1..4 | ForEach-Object { "$roomType.CompleteOrderedThemeWorldMatchesNative(depth: $_)" }) +
        @("$lightType.CompleteLightChannelsMatchNative")
    if ($assemblies.Count -ne 1 -or $assemblies[0].total -ne 5 -or $assemblies[0].passed -ne 5 -or
        $assemblies[0].failed -ne 0 -or $assemblies[0].skipped -ne 0 -or
        $assemblies[0].errors -ne 0 -or $assemblies[0].'not-run' -ne 0 -or
        @($report.SelectNodes('//errors/error')).Count -ne 0 -or
        $cases.Count -ne 5 -or @($cases | Where-Object { $_.result -cne 'Pass' -or -not (
            ($_.type -ceq $roomType -and $_.method -ceq 'CompleteOrderedThemeWorldMatchesNative') -or
            ($_.type -ceq $lightType -and $_.method -ceq 'CompleteLightChannelsMatchNative')) }).Count -or
        @(Compare-Object $names @($cases.name) -CaseSensitive).Count) {
        throw 'All five exact explicit room/static-light comparisons must pass without exclusions.'
    }
}

$roomBuild = Find-CathedralOracleBuild $roomRepositoryRoot
$roomEvidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Theme-Room-Oracle-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $roomEvidenceRoot) { throw 'The theme oracle requires a fresh evidence root.' }
if ([IO.Path]::GetFullPath($roomEvidenceRoot).StartsWith($roomRepositoryRoot.TrimEnd('\') + '\',
        [StringComparison]::OrdinalIgnoreCase)) { throw 'Native theme evidence must remain outside the repository.' }
New-Item -ItemType Directory -Path $roomEvidenceRoot | Out-Null
$roomRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'detached-original-diablo-cathedral-sp-normal-complete-selected-themes-and-zero-offset-static-light'
    NativeBuild = $roomBuild.Directory; Stages = @(); Artifacts = @(); Error = $null; EvidenceError = $null }
$roomVariables = @('GODEVILUTION_THEME_ROOM_ORACLE_DIRECTORY', 'GODEVILUTION_THEME_STATIC_LIGHT_ORACLE_DIRECTORY')
$roomPrevious = @{}
foreach ($roomVariable in $roomVariables) { $roomPrevious[$roomVariable] = [Environment]::GetEnvironmentVariable($roomVariable, 'Process') }
$roomPrimaryError = $null
Push-Location $roomRepositoryRoot
try {
    Write-Host "Private theme-room evidence: $roomEvidenceRoot"
    $roomBuildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
        $roomBuild.DeveloperCommand, $roomBuild.CMake, $roomBuild.Directory
    $roomOutcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $roomBuildCommand + '"') `
        (Join-Path $roomEvidenceRoot 'native-build.log') 300000
    $roomRecord.Stages += $roomOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $roomOutcome 'Native theme-room build'
    $roomOutcome = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --configuration Debug --no-restore --nologo' `
        (Join-Path $roomEvidenceRoot 'managed-build.log') 180000
    $roomRecord.Stages += $roomOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $roomOutcome 'Managed theme-room build'
    $roomFirstArtifacts = $null
    foreach ($roomCaptureIndex in 1..2) {
        $roomCapture = Join-Path $roomEvidenceRoot "capture-$roomCaptureIndex"
        $roomDirectory = Join-Path $roomCapture 'rooms'; $lightDirectory = Join-Path $roomCapture 'static-light'
        New-Item -ItemType Directory -Path $roomDirectory, $lightDirectory | Out-Null
        [Environment]::SetEnvironmentVariable($roomVariables[0], $roomDirectory, 'Process')
        [Environment]::SetEnvironmentVariable($roomVariables[1], $lightDirectory, 'Process')
        $roomReport = Join-Path $roomEvidenceRoot "native-tests-$roomCaptureIndex.json"
        $roomArguments = '--gtest_filter=ThemeStaticLightingTest.*:ThemeRoomGenerationTest.*-ThemeRoomGenerationTest.CaptureComposedOrdinaryPopulation --gtest_output=json:' +
            (Quote-CathedralNativeArgument $roomReport)
        $roomOutcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $roomBuild.Directory 'theme_item_generation_test.exe') `
            $roomArguments (Join-Path $roomEvidenceRoot "native-tests-$roomCaptureIndex.log") 120000
        $roomRecord.Stages += $roomOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $roomOutcome 'Native theme-room capture'
        Assert-ThemeRoomNativeReport $roomReport
        $roomArtifacts = @((Get-ThemeRoomArtifact $roomDirectory 'native-theme-rooms.json' 128MB),
            (Get-ThemeRoomArtifact $lightDirectory 'native-theme-static-light.json' 8MB))
        if ($null -eq $roomFirstArtifacts) {
            $roomFirstArtifacts = $roomArtifacts; $roomManagedDirectory = $roomDirectory; $lightManagedDirectory = $lightDirectory
        } else { Assert-ThemeRoomRepeatability $roomFirstArtifacts $roomArtifacts }
        $roomRecord.Artifacts += $roomArtifacts
    }
    [Environment]::SetEnvironmentVariable($roomVariables[0], $roomManagedDirectory, 'Process')
    [Environment]::SetEnvironmentVariable($roomVariables[1], $lightManagedDirectory, 'Process')
    $roomArguments = '--no-ansi --parallel none --filter-method "*AuthoritativeCathedralThemeRoomNativeOracleTests.CompleteOrderedThemeWorldMatchesNative*" --filter-method "*AuthoritativeCathedralThemeStaticLightNativeOracleTests.CompleteLightChannelsMatchNative*" --explicit only --fail-skips on --report-xunit --results-directory ' +
        (Quote-CathedralNativeArgument $roomEvidenceRoot) + ' --report-xunit-filename managed-tests.xml'
    $roomOutcome = Invoke-CathedralPopulationOwnedProcess `
        (Join-Path $roomRepositoryRoot 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') `
        $roomArguments (Join-Path $roomEvidenceRoot 'managed-tests.log') 180000
    $roomRecord.Stages += $roomOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $roomOutcome 'Managed theme-room parity'
    Assert-ThemeRoomManagedReport (Join-Path $roomEvidenceRoot 'managed-tests.xml')
    $roomRecord.Passed = $true
} catch { $roomPrimaryError = $_; $roomRecord.Error = $_.Exception.Message }
finally {
    foreach ($roomVariable in $roomVariables) { Restore-CathedralOracleEnvironmentVariable $roomVariable $roomPrevious[$roomVariable] }
    Pop-Location; $roomRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try { [IO.File]::WriteAllText((Join-Path $roomEvidenceRoot 'result.json'), ($roomRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
    catch { $roomRecord.EvidenceError = $_.Exception.Message; if ($null -eq $roomPrimaryError) { $roomPrimaryError = $_ } }
}
if ($null -ne $roomPrimaryError) { throw $roomPrimaryError }
Write-Host "PASS: 88 complete theme worlds, 40 static-light cases, eight native fixtures, five explicit comparisons and byte-identical captures. Evidence: $roomEvidenceRoot"
