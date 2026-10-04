<#
    Independent detached original-Diablo Cathedral item selection/world oracle.
    Zero arguments, bounded owned processes and private external evidence. The
    earlier setup/unique corpus and runner remain separate and unchanged.
#>
[CmdletBinding()]
param()

$placementRepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$ErrorActionPreference = 'Stop'
# Reuse declarations only, never execute either runner's body.
foreach ($placementImport in @(
        @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
            'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
            'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
        @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') })) {
    $placementTokens = $null; $placementParseErrors = $null
    $placementAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $placementImport.File), [ref]$placementTokens, [ref]$placementParseErrors)
    if ($placementParseErrors.Count -ne 0) { throw 'The shared item-oracle declarations did not parse.' }
    foreach ($placementName in $placementImport.Names) {
        $placementDeclarations = @($placementAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $placementName
        }, $true))
        if ($placementDeclarations.Count -ne 1) { throw "Expected exactly one shared declaration for $placementName." }
        . ([scriptblock]::Create($placementDeclarations[0].Extent.Text))
    }
}

function Assert-ThemePlacementNativeReport([string]$Path) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    $names = @('ItemSpaceUsesSignedOccupancyAndObjectOverlapRules', 'FullPoolTypedGoldPreservesSeedAndEveryItemSlot',
        'CaptureIndependentSelectorsAndWorldCreation')
    if ($report.tests -ne 3 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne 3 -or @($cases | Where-Object {
            $_.classname -cne 'ThemeItemPlacementTest' -or $_.status -cne 'RUN' -or $_.result -cne 'COMPLETED' -or
            @($_.failures).Where({ $null -ne $_ }).Count -ne 0
        }).Count -ne 0 -or @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'All three exact native item-placement fixtures must complete without skips.'
    }
}

function Assert-ThemePlacementArtifact([string]$Directory) {
    $entries = @(Get-ChildItem -LiteralPath $Directory -Force)
    if ($entries.Count -ne 1 -or $entries[0].Name -cne 'native-theme-item-placement.json' -or
        $entries[0].PSIsContainer -or ($entries[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries[0].Length -le 0 -or $entries[0].Length -gt 32MB) {
        throw 'Native placement capture must retain exactly one nonempty bounded regular corpus file.'
    }
    # Do not depend on Get-FileHash script-module discovery in inherited
    # PowerShell environments. Hash the bounded regular artifact directly.
    $stream = $null; $sha = $null
    try {
        $stream = [IO.File]::OpenRead($entries[0].FullName)
        if ($stream.Length -ne $entries[0].Length -or $stream.Length -le 0 -or $stream.Length -gt 32MB) {
            throw 'Native placement artifact size changed before hashing.'
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        return [pscustomobject]@{ Path = $entries[0].FullName; Length = $stream.Length
            Sha256 = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    } finally {
        if ($null -ne $sha) { $sha.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Assert-ThemePlacementManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $cases = @($report.SelectNodes('//test'))
    $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemWorldNativeOracleTests'
    $method = 'WorldAndSelectorsMatchNative'
    $names = @(1..4 | ForEach-Object { "${type}.${method}(depth: $_)" })
    if ($cases.Count -ne 4 -or @($cases | Where-Object {
            $_.result -cne 'Pass' -or $_.type -cne $type -or $_.method -cne $method
        }).Count -ne 0 -or @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'All four exact explicit placement depth comparisons must pass without exclusions.'
    }
}

$placementBuild = Find-CathedralOracleBuild $placementRepositoryRoot
$placementEvidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Theme-Item-Placement-Oracle-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $placementEvidenceRoot) { throw 'The placement oracle requires a fresh evidence root.' }
if ([IO.Path]::GetFullPath($placementEvidenceRoot).StartsWith($placementRepositoryRoot.TrimEnd('\') + '\',
        [StringComparison]::OrdinalIgnoreCase)) { throw 'Derived native evidence must remain outside the repository.' }
New-Item -ItemType Directory -Path $placementEvidenceRoot | Out-Null
$placementRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'detached-original-diablo-cathedral-sp-normal-selection-pool-placement-setup-unique-ground'
    NativeBuild = $placementBuild.Directory; Stages = @(); Artifacts = @(); SelectorCount = $null; WorldCount = $null; Error = $null }
$placementPreviousDirectory = [Environment]::GetEnvironmentVariable('GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY', 'Process')
$placementPrimaryError = $null
Push-Location $placementRepositoryRoot
try {
    Write-Host "Private theme-item placement evidence: $placementEvidenceRoot"
    $placementBuildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
        $placementBuild.DeveloperCommand, $placementBuild.CMake, $placementBuild.Directory
    $placementOutcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $placementBuildCommand + '"') `
        (Join-Path $placementEvidenceRoot 'native-build.log') 300000
    $placementRecord.Stages += $placementOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $placementOutcome 'Native placement build'
    $placementDotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    $placementOutcome = Invoke-CathedralPopulationOwnedProcess $placementDotnet `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --configuration Debug --no-restore --nologo' `
        (Join-Path $placementEvidenceRoot 'managed-build.log') 180000
    $placementRecord.Stages += $placementOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $placementOutcome 'Managed placement build'
    $placementPreviousHash = $null
    foreach ($placementCaptureIndex in 1..2) {
        $placementArtifacts = Join-Path $placementEvidenceRoot "capture-$placementCaptureIndex"
        New-Item -ItemType Directory -Path $placementArtifacts | Out-Null
        [Environment]::SetEnvironmentVariable('GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY', $placementArtifacts, 'Process')
        $placementNativeReport = Join-Path $placementEvidenceRoot "native-tests-$placementCaptureIndex.json"
        $placementArguments = '--gtest_filter=ThemeItemPlacementTest.* --gtest_output=json:' + (Quote-CathedralNativeArgument $placementNativeReport)
        $placementOutcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $placementBuild.Directory 'theme_item_generation_test.exe') `
            $placementArguments (Join-Path $placementEvidenceRoot "native-tests-$placementCaptureIndex.log") 60000
        $placementRecord.Stages += $placementOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $placementOutcome 'Native placement capture'
        Assert-ThemePlacementNativeReport $placementNativeReport
        $placementArtifact = Assert-ThemePlacementArtifact $placementArtifacts
        if ($null -ne $placementPreviousHash -and $placementPreviousHash -cne $placementArtifact.Sha256) {
            throw 'Independent native placement captures differ.'
        }
        $placementPreviousHash = $placementArtifact.Sha256
        $placementRecord.Artifacts += $placementArtifact
        $placementCorpus = [IO.File]::ReadAllText($placementArtifact.Path) | ConvertFrom-Json
        if ($placementCorpus.format -cne 'godevilution-cathedral-theme-item-placement' -or $placementCorpus.version -ne 1 -or
            $placementCorpus.selectorCount -ne 224 -or $placementCorpus.selectors.Count -ne 224 -or
            $placementCorpus.worldCount -ne 40 -or $placementCorpus.worldCases.Count -ne 40) {
            throw 'Native placement corpus identity/matrix count is incomplete.'
        }
        $placementRecord.SelectorCount = $placementCorpus.selectorCount; $placementRecord.WorldCount = $placementCorpus.worldCount
        if ($placementCaptureIndex -eq 1) { $placementManagedArtifacts = $placementArtifacts }
    }
    [Environment]::SetEnvironmentVariable('GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY', $placementManagedArtifacts, 'Process')
    $placementArguments = '--no-ansi --parallel none --filter-method "*AuthoritativeCathedralThemeItemWorldNativeOracleTests.WorldAndSelectorsMatchNative*" --explicit only --fail-skips on --report-xunit --results-directory ' +
        (Quote-CathedralNativeArgument $placementEvidenceRoot) + ' --report-xunit-filename managed-tests.xml'
    $placementOutcome = Invoke-CathedralPopulationOwnedProcess `
        (Join-Path $placementRepositoryRoot 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') `
        $placementArguments (Join-Path $placementEvidenceRoot 'managed-tests.log') 120000
    $placementRecord.Stages += $placementOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $placementOutcome 'Managed placement parity'
    Assert-ThemePlacementManagedReport (Join-Path $placementEvidenceRoot 'managed-tests.xml')
    $placementRecord.Passed = $true
} catch {
    $placementPrimaryError = $_; $placementRecord.Error = $_.Exception.Message
} finally {
    Restore-CathedralOracleEnvironmentVariable 'GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY' $placementPreviousDirectory
    Pop-Location
    $placementRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $placementEvidenceRoot 'result.json'), ($placementRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}
if ($null -ne $placementPrimaryError) { throw $placementPrimaryError }
Write-Host "PASS: $($placementRecord.SelectorCount) independent selectors, $($placementRecord.WorldCount) complete worlds, four depth comparisons and byte-identical captures. Evidence: $placementEvidenceRoot"
