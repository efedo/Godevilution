<#
.SYNOPSIS
    Compares detached C# Cathedral item generation against native source.
.DESCRIPTION
    Builds only the dedicated native fixture and existing C# module tests.
    Runs bounded, dialog-suppressed owned children, repeats the native corpus,
    and retains private evidence outside the repository. No game client, MPQ
    archive, save profile, placement pool, or theme-room runtime is launched.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$themeRepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
# Import only reviewed declarations, never execute the geometry runner's body.
# Shared child ownership/error-mode semantics must not drift between oracles.
$themeTokens = $null
$themeParseErrors = $null
$themeHarnessAst = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'test-cathedral-geometry.ps1'), [ref]$themeTokens, [ref]$themeParseErrors)
if ($themeParseErrors.Count -ne 0) { throw 'The shared native verification harness did not parse.' }
foreach ($themeName in @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess')) {
    $themeDeclarations = @($themeHarnessAst.FindAll({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $themeName
    }, $true))
    if ($themeDeclarations.Count -ne 1) { throw "Expected exactly one shared declaration for $themeName." }
    . ([scriptblock]::Create($themeDeclarations[0].Extent.Text))
}

function Assert-ThemeOwnedStage($Outcome, [string]$Stage) {
    if ($null -eq $Outcome.Pid -or $Outcome.Pid -le 0 -or $Outcome.ExitCode -ne 0 -or $Outcome.Error -or $Outcome.CleanupError -or
        $Outcome.TimedOut -or -not $Outcome.ConfirmedExited -or -not $Outcome.LogRetained) {
        throw "$Stage failed its exit, deadline, retained-log, or owned-process cleanup gate."
    }
}

function Assert-ThemeNativeReport([string]$Path) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    $expectedNames = @('GoldUsesCurrentDepthAndRetainsBaseIdentifiedValue', 'SetupRetainsFieldsOutsideBaseAttributes',
        'ScalarFontMetricsMatchNativePanelWidth', 'CaptureIndependentRawSetupAndUniqueContinuation')
    if ($report.tests -ne 4 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne 4 -or @($cases | Where-Object {
            $_.classname -cne 'ThemeItemGenerationTest' -or $_.status -cne 'RUN' -or $_.result -cne 'COMPLETED' -or
            @($_.failures).Where({ $null -ne $_ }).Count -ne 0
        }).Count -ne 0 -or @(Compare-Object -ReferenceObject $expectedNames -DifferenceObject @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'All four exact native theme-item fixtures must complete without skips.'
    }
}

$themeBuild = Find-CathedralOracleBuild $themeRepositoryRoot
$themeEvidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Theme-Item-Oracle-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $themeEvidenceRoot) { throw 'The theme-item oracle needs a fresh evidence directory.' }
if ([IO.Path]::GetFullPath($themeEvidenceRoot).StartsWith($themeRepositoryRoot.TrimEnd('\') + '\',
        [StringComparison]::OrdinalIgnoreCase)) { throw 'Derived native evidence must remain outside the repository.' }
New-Item -ItemType Directory -Path $themeEvidenceRoot | Out-Null
$themeRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'detached-original-diablo-cathedral-sp-normal-item-setup-unique-and-ground-scalars'
    NativeBuild = $themeBuild.Directory; Stages = @(); Artifacts = @(); CaseCount = $null; Error = $null }
$themePreviousDirectory = [Environment]::GetEnvironmentVariable('GODEVILUTION_THEME_ITEM_ORACLE_DIRECTORY', 'Process')
$themePrimaryError = $null
Push-Location $themeRepositoryRoot
try {
    Write-Host "Private theme-item oracle evidence: $themeEvidenceRoot"
    $themeBuildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
        $themeBuild.DeveloperCommand, $themeBuild.CMake, $themeBuild.Directory
    $themeNativeBuild = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $themeBuildCommand + '"') `
        (Join-Path $themeEvidenceRoot 'native-build.log') 300000
    $themeRecord.Stages += $themeNativeBuild | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $themeNativeBuild 'Native build'
    $themeDotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    $themeManagedBuild = Invoke-CathedralPopulationOwnedProcess $themeDotnet `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --configuration Debug --no-restore --nologo' `
        (Join-Path $themeEvidenceRoot 'managed-build.log') 180000
    $themeRecord.Stages += $themeManagedBuild | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $themeManagedBuild 'Managed build'
    $themePreviousHash = $null
    foreach ($themeCaptureIndex in 1..2) {
        $themeArtifacts = Join-Path $themeEvidenceRoot "capture-$themeCaptureIndex"
        New-Item -ItemType Directory -Path $themeArtifacts | Out-Null
        [Environment]::SetEnvironmentVariable('GODEVILUTION_THEME_ITEM_ORACLE_DIRECTORY', $themeArtifacts, 'Process')
        $themeReportPath = Join-Path $themeEvidenceRoot "native-tests-$themeCaptureIndex.json"
        $themeNativeArguments = '--gtest_filter=ThemeItemGenerationTest.* --gtest_output=json:' + (Quote-CathedralNativeArgument $themeReportPath)
        $themeNative = Invoke-CathedralPopulationOwnedProcess (Join-Path $themeBuild.Directory 'theme_item_generation_test.exe') `
            $themeNativeArguments (Join-Path $themeEvidenceRoot "native-tests-$themeCaptureIndex.log") 60000
        $themeRecord.Stages += $themeNative | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $themeNative 'Native capture'
        Assert-ThemeNativeReport $themeReportPath
        $themeEntries = @(Get-ChildItem -LiteralPath $themeArtifacts -Force)
        if ($themeEntries.Count -ne 1 -or $themeEntries[0].Name -cne 'native-theme-items.json' -or
            $themeEntries[0].PSIsContainer -or ($themeEntries[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $themeEntries[0].Length -le 0 -or $themeEntries[0].Length -gt 32MB) {
            throw 'Native capture must retain exactly one nonempty bounded regular corpus file.'
        }
        $themeHash = (Get-FileHash -LiteralPath $themeEntries[0].FullName -Algorithm SHA256).Hash
        if ($null -ne $themePreviousHash -and $themePreviousHash -cne $themeHash) { throw 'Independent native captures differ.' }
        $themePreviousHash = $themeHash
        $themeRecord.Artifacts += [pscustomobject]@{ Path = $themeEntries[0].FullName; Length = $themeEntries[0].Length; Sha256 = $themeHash }
        $themeCorpus = [IO.File]::ReadAllText($themeEntries[0].FullName) | ConvertFrom-Json
        if ($themeCorpus.format -cne 'godevilution-cathedral-theme-item-setup' -or $themeCorpus.version -ne 1 -or
            $themeCorpus.caseCount -le 1000 -or $themeCorpus.cases.Count -ne $themeCorpus.caseCount) {
            throw 'Native corpus identity or nonempty case count is invalid.'
        }
        $themeRecord.CaseCount = $themeCorpus.caseCount
        foreach ($themeCoverage in @('reverseCompatibleUniques', 'offsetUniques', 'books', 'gold',
                'staffSpells', 'staffPowers', 'magicalAffixes', 'ordinaryDurability')) {
            if ($themeCorpus.coverage.$themeCoverage -le 0) { throw "Missing native branch coverage: $themeCoverage." }
        }
        if ($themeCaptureIndex -eq 1) { $themeManagedArtifacts = $themeArtifacts }
    }
    [Environment]::SetEnvironmentVariable('GODEVILUTION_THEME_ITEM_ORACLE_DIRECTORY', $themeManagedArtifacts, 'Process')
    $themeManagedArguments = '--no-ansi --filter-method "*AuthoritativeCathedralThemeItemNativeOracleTests.SetupUniqueAndGroundMatchNative*" --explicit only --fail-skips on --report-xunit --results-directory ' +
        (Quote-CathedralNativeArgument $themeEvidenceRoot) + ' --report-xunit-filename managed-tests.xml'
    $themeManaged = Invoke-CathedralPopulationOwnedProcess `
        (Join-Path $themeRepositoryRoot 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') `
        $themeManagedArguments (Join-Path $themeEvidenceRoot 'managed-tests.log') 120000
    $themeRecord.Stages += $themeManaged | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $themeManaged 'Managed parity'
    [xml]$themeManagedReport = [IO.File]::ReadAllText((Join-Path $themeEvidenceRoot 'managed-tests.xml'))
    $themeTestResults = @($themeManagedReport.SelectNodes('//test'))
    if ($themeTestResults.Count -ne 4 -or @($themeTestResults | Where-Object result -cne 'Pass').Count -ne 0) {
        throw 'All four explicit depth comparisons must pass without exclusions.'
    }
    $themeRecord.Passed = $true
} catch {
    $themePrimaryError = $_
    $themeRecord.Error = $_.Exception.Message
} finally {
    Restore-CathedralOracleEnvironmentVariable 'GODEVILUTION_THEME_ITEM_ORACLE_DIRECTORY' $themePreviousDirectory
    Pop-Location
    $themeRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $themeEvidenceRoot 'result.json'), ($themeRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}
if ($null -ne $themePrimaryError) { throw $themePrimaryError }
Write-Host "PASS: $($themeRecord.CaseCount) native item cases, four explicit managed depth comparisons and byte-identical repeated captures. Evidence: $themeEvidenceRoot"
