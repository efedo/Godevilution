<# Fresh-cold Cathedral generation in native load order. No server/client is launched. #>
[CmdletBinding()]
param([Parameter(Mandatory = $true)] [string]$MetadataPackDirectory)

$ErrorActionPreference = 'Stop'
$freshRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
foreach ($freshImport in @(
    @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
    @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
    @{ File = 'test-theme-room-generation.ps1'; Names = @('Get-ThemeRoomArtifact') },
    @{ File = 'test-cathedral-full-population.ps1'; Names = @('Assert-CathedralFullExternalPath') })) {
    $freshTokens = $null; $freshErrors = $null
    $freshAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $freshImport.File), [ref]$freshTokens, [ref]$freshErrors)
    if ($freshErrors.Count) { throw 'Shared fresh-population declarations did not parse.' }
    foreach ($freshName in $freshImport.Names) {
        $freshDeclarations = @($freshAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $freshName
        }, $true))
        if ($freshDeclarations.Count -ne 1) { throw "Expected exactly one declaration for $freshName." }
        . ([scriptblock]::Create($freshDeclarations[0].Extent.Text))
    }
}

function Assert-CathedralFreshNativeReport([string]$Path) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne 1 -or $cases[0].classname -cne 'ThemeRoomGenerationTest' -or
        $cases[0].name -cne 'CaptureFreshOrdinaryPopulation' -or $cases[0].status -cne 'RUN' -or
        $cases[0].result -cne 'COMPLETED' -or @($cases[0].failures).Where({ $null -ne $_ }).Count -ne 0) {
        throw 'The exact fresh-population native fixture must pass without exclusions.'
    }
}

function Assert-CathedralFreshManagedReport([string]$Path, [switch]$Factory) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $assemblies = @($report.SelectNodes('//assembly')); $cases = @($report.SelectNodes('//test'))
    $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralFreshLevel' + $(if ($Factory) { 'GeneratorTests' } else { 'NativeOracleTests' })
    if ($assemblies.Count -ne 1 -or $cases.Count -eq 0 -or $assemblies[0].failed -ne 0 -or
        $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or $assemblies[0].'not-run' -ne 0 -or
        @($report.SelectNodes('//errors/error')).Count -ne 0 -or
        @($cases | Where-Object { $_.result -cne 'Pass' -or $_.type -cne $type }).Count -ne 0 -or
        $assemblies[0].total -ne $cases.Count -or $assemblies[0].passed -ne $cases.Count) {
        throw 'All selected fresh-population cases must pass without exclusions.'
    }
    if (-not $Factory) {
        $names = @(foreach ($depth in 1..4) { foreach ($seed in @(2588, 743271966)) { foreach ($entry in @('main', 'previous')) {
            $type + '.FreshLevelMatchesNative(depth: ' + $depth + ', seed: ' + $seed + ', entry: \"' + $entry + '\")'
        } } })
        if ($cases.Count -ne 16 -or @($cases | Where-Object method -cne 'FreshLevelMatchesNative').Count -ne 0 -or
            @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
            throw 'The exact sixteen fresh-population comparisons are required.'
        }
    } else {
        $methods = @('GeneratedDepthCarriesNativeNextTransparencyValue',
            'CreateRequestRetainsIndependentDungeonAndStorySeeds',
            'InvalidHeroBoundsAreRejectedBeforeFreshGeneration',
            'HeroEntryContextIsExplicitAndBoundedWithoutOriginalAssets',
            'MaterializersPreserveCounterWithoutAliasingCallerLayers')
        $names = @($methods | ForEach-Object { $type + '.' + $_ })
        $generation = 'FreshGenerationDerivesGeometryTriggersPoolsAndPlayerLight'
        foreach ($binding in @(@(1, 2588, 'main'), @(2, 743271966, 'previous'),
            @(3, 2147483648, 'main'), @(4, 0, 'previous'))) {
            $names += $type + '.' + $generation + '(depth: ' + $binding[0] + ', seed: ' +
                $binding[1] + ', entry: \"' + $binding[2] + '\")'
        }
        if ($cases.Count -ne 9 -or @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0 -or
            @($cases | Where-Object { $_.method -cnotin ($methods + $generation) -or
                -not $_.name.StartsWith($type + '.' + $_.method, [StringComparison]::Ordinal) }).Count -ne 0) {
            throw 'All five portable factory checks and four exact generation cases are required.'
        }
    }
}

function Assert-CathedralFreshRepeatability($First, $Second) {
    if ($null -eq $First -or $null -eq $Second -or $First.Name -cne 'native-fresh-population.json' -or
        $Second.Name -cne $First.Name -or $First.Length -le 0 -or $First.Length -gt 128MB -or
        $First.Length -ne $Second.Length -or $First.Sha256 -cnotmatch '^[0-9A-F]{64}$' -or $First.Sha256 -cne $Second.Sha256) {
        throw 'Two independent fresh-population captures must be byte-identical.'
    }
}

$MetadataPackDirectory = (Resolve-Path -LiteralPath $MetadataPackDirectory).Path
Assert-CathedralFullExternalPath $MetadataPackDirectory $freshRepository
if (-not (Test-Path -LiteralPath $MetadataPackDirectory -PathType Container)) { throw 'An explicit compiled metadata directory is required.' }
$freshBuild = Find-CathedralOracleBuild $freshRepository
$freshRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Fresh-Population-Oracle-' + [guid]::NewGuid().ToString('N'))
Assert-CathedralFullExternalPath $freshRoot $freshRepository
New-Item -ItemType Directory -Path $freshRoot | Out-Null
$freshRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'fresh-cold-original-diablo-cathedral-sp-normal-clear-before-createlevel-through-createthemerooms'
    MetadataPackDirectory = $MetadataPackDirectory; NativeBuild = $freshBuild.Directory
    Stages = @(); Artifacts = @(); Error = $null; EvidenceError = $null }
$freshVariables = @('GODEVILUTION_CATHEDRAL_FRESH_POPULATION_ORACLE_DIRECTORY', 'GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY')
$freshPrevious = @{}
foreach ($variable in $freshVariables) { $freshPrevious[$variable] = [Environment]::GetEnvironmentVariable($variable, 'Process') }
$freshPrimaryError = $null
Push-Location $freshRepository
try {
    Write-Host "Private fresh-population evidence: $freshRoot"
    $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
        $freshBuild.DeveloperCommand, $freshBuild.CMake, $freshBuild.Directory
    $outcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $buildCommand + '"') (Join-Path $freshRoot 'native-build.log') 300000
    $freshRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $outcome 'Native fresh-population build'
    foreach ($capture in 1..2) {
        $captureDirectory = Join-Path $freshRoot "capture-$capture"
        New-Item -ItemType Directory -Path $captureDirectory | Out-Null
        [Environment]::SetEnvironmentVariable($freshVariables[0], $captureDirectory, 'Process')
        $report = Join-Path $freshRoot "native-tests-$capture.json"
        $arguments = '--gtest_filter=ThemeRoomGenerationTest.CaptureFreshOrdinaryPopulation --gtest_output=json:' + (Quote-CathedralNativeArgument $report)
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $freshBuild.Directory 'theme_item_generation_test.exe') $arguments (Join-Path $freshRoot "native-tests-$capture.log") 120000
        $freshRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $outcome 'Native fresh-population capture'
        Assert-CathedralFreshNativeReport $report
        $artifact = Get-ThemeRoomArtifact $captureDirectory 'native-fresh-population.json' 128MB
        if ($capture -eq 2) { Assert-CathedralFreshRepeatability $freshRecord.Artifacts[0] $artifact }
        $freshRecord.Artifacts += $artifact
    }
    $outcome = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application).Source `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --no-restore --disable-build-servers -v minimal' (Join-Path $freshRoot 'managed-build.log') 180000
    $freshRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $outcome 'Managed fresh-population build'
    [Environment]::SetEnvironmentVariable($freshVariables[0], (Join-Path $freshRoot 'capture-1'), 'Process')
    [Environment]::SetEnvironmentVariable($freshVariables[1], $MetadataPackDirectory, 'Process')
    foreach ($factory in @($false, $true)) {
        $method = if ($factory) { '*AuthoritativeCathedralFreshLevelGeneratorTests*' } else { '*AuthoritativeCathedralFreshLevelNativeOracleTests.FreshLevelMatchesNative*' }
        $name = if ($factory) { 'factory' } else { 'parity' }
        $arguments = '--no-ansi --parallel none --filter-method ' + (Quote-CathedralNativeArgument $method) +
            ' --explicit on --fail-skips on --report-xunit --results-directory ' + (Quote-CathedralNativeArgument $freshRoot) + ' --report-xunit-filename ' + $name + '.xml'
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $freshRepository 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') `
            $arguments (Join-Path $freshRoot ($name + '.log')) 180000
        $freshRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $outcome 'Managed fresh-population verification'
        Assert-CathedralFreshManagedReport (Join-Path $freshRoot ($name + '.xml')) -Factory:$factory
    }
    $freshRecord.Passed = $true
} catch { $freshPrimaryError = $_; $freshRecord.Error = $_.Exception.Message }
finally {
    foreach ($variable in $freshVariables) { Restore-CathedralOracleEnvironmentVariable $variable $freshPrevious[$variable] }
    Pop-Location; $freshRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try { [IO.File]::WriteAllText((Join-Path $freshRoot 'result.json'), ($freshRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
    catch { $freshRecord.EvidenceError = $_.Exception.Message; if ($null -eq $freshPrimaryError) { $freshPrimaryError = $_ } }
}
if ($null -ne $freshPrimaryError) { throw $freshPrimaryError }
Write-Host "PASS: sixteen fresh normal-order population comparisons, factory checks and byte-identical native captures. Evidence: $freshRoot"
