<# Canonical native Warrior/Sorcerer L1 preparation. Earlier Rogue captures stay separate. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$MetadataPackDirectory,
    [Parameter(Mandatory = $true)] [string]$EvidenceDirectory,
    [switch]$SkipBuild,
    [switch]$NativeOnly
)
$ErrorActionPreference = 'Stop'
$classRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
foreach ($classImport in @(
    @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
    @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
    @{ File = 'test-cathedral-full-population.ps1'; Names = @('Assert-CathedralFullExternalPath') })) {
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $classImport.File), [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw 'Shared native class declarations did not parse.' }
    foreach ($name in $classImport.Names) {
        $declarations = @($ast.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name
        }, $true))
        if ($declarations.Count -ne 1) { throw "Expected exactly one declaration for $name." }
        . ([scriptblock]::Create($declarations[0].Extent.Text))
    }
}
$MetadataPackDirectory = (Resolve-Path -LiteralPath $MetadataPackDirectory).Path
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
Assert-CathedralFullExternalPath $EvidenceDirectory $classRepository
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Native class evidence requires a new directory.' }
New-Item -ItemType Directory -Path $EvidenceDirectory | Out-Null
$classBuild = Find-CathedralOracleBuild $classRepository
$classVariables = @('GODEVILUTION_CATHEDRAL_NATIVE_WARRIOR_L1_ORACLE_DIRECTORY',
    'GODEVILUTION_CATHEDRAL_NATIVE_SORCERER_L1_ORACLE_DIRECTORY', 'GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY')
$classPrevious = @{}
foreach ($name in $classVariables) { $classPrevious[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$classRecord = [ordered]@{ Passed = $false; NativeOnly = [bool]$NativeOnly; Stages = @(); Artifacts = @(); Error = $null }
Push-Location $classRepository
try {
    if (-not $SkipBuild) {
        $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && set "_CL_=/Z7" && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
            $classBuild.DeveloperCommand, $classBuild.CMake, $classBuild.Directory
        $stage = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $buildCommand + '"') (Join-Path $EvidenceDirectory 'native-build.log') 300000
        $classRecord.Stages += $stage
        Assert-ThemeOwnedStage $stage 'Native class build'
    }
    foreach ($class in @('Warrior', 'Sorcerer')) {
        $variable = 'GODEVILUTION_CATHEDRAL_NATIVE_' + $class.ToUpperInvariant() + '_L1_ORACLE_DIRECTORY'
        $firstHash = $null
        foreach ($capture in 1..2) {
            $directory = Join-Path $EvidenceDirectory ($class.ToLowerInvariant() + '-' + $capture)
            New-Item -ItemType Directory -Path $directory | Out-Null
            [Environment]::SetEnvironmentVariable($variable, $directory, 'Process')
            $reportPath = Join-Path $EvidenceDirectory ($class + '-' + $capture + '.json')
            $arguments = '--gtest_filter=ThemeRoomGenerationTest.CaptureNative' + $class + 'L1Population --gtest_output=json:' + (Quote-CathedralNativeArgument $reportPath)
            $stage = Invoke-CathedralPopulationOwnedProcess (Join-Path $classBuild.Directory 'theme_item_generation_test.exe') $arguments (Join-Path $EvidenceDirectory ($class + '-' + $capture + '.log')) 120000
            $classRecord.Stages += $stage
            Assert-ThemeOwnedStage $stage 'Native class capture'
            $report = [IO.File]::ReadAllText($reportPath) | ConvertFrom-Json
            $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
            if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
                $cases.Count -ne 1 -or $cases[0].classname -cne 'ThemeRoomGenerationTest' -or
                $cases[0].name -cne ('CaptureNative' + $class + 'L1Population') -or $cases[0].status -cne 'RUN' -or $cases[0].result -cne 'COMPLETED') {
                throw 'The exact native class capture must pass without skips.'
            }
            $entries = @(Get-ChildItem -LiteralPath $directory -Force)
            if ($entries.Count -ne 1 -or $entries[0].Name -cne 'native-class-l1-population.json' -or
                $entries[0].PSIsContainer -or ($entries[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                $entries[0].Length -le 0 -or $entries[0].Length -gt 128MB) { throw 'Unexpected native class artifact.' }
            $hash = (Get-FileHash -LiteralPath $entries[0].FullName -Algorithm SHA256).Hash
            if ($capture -eq 1) { $firstHash = $hash } elseif ($hash -cne $firstHash) { throw 'Native class captures differ.' }
            $classRecord.Artifacts += [ordered]@{ Class = $class; Capture = $capture; Length = $entries[0].Length; Sha256 = $hash }
        }
        [Environment]::SetEnvironmentVariable($variable, (Join-Path $EvidenceDirectory ($class.ToLowerInvariant() + '-1')), 'Process')
    }
    if (-not $NativeOnly) {
        if (-not $SkipBuild) {
            $stage = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application).Source `
                'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --no-restore -v quiet -m:1 -nodeReuse:false -p:UseSharedCompilation=false' (Join-Path $EvidenceDirectory 'managed-build.log') 180000
            $classRecord.Stages += $stage
            Assert-ThemeOwnedStage $stage 'Managed class comparison build'
        }
        [Environment]::SetEnvironmentVariable($classVariables[2], $MetadataPackDirectory, 'Process')
        $arguments = '--no-ansi --parallel none --explicit only --fail-skips on --filter-method *AuthoritativeCathedralFreshLevelNativeOracleTests.PlayableClassL1TupleMatchesNativeLoader --report-xunit --results-directory ' +
            (Quote-CathedralNativeArgument $EvidenceDirectory) + ' --report-xunit-filename managed.xml'
        $stage = Invoke-CathedralPopulationOwnedProcess (Join-Path $classRepository 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') $arguments (Join-Path $EvidenceDirectory 'managed.log') 180000
        $classRecord.Stages += $stage
        Assert-ThemeOwnedStage $stage 'Managed native class comparisons'
        [xml]$report = [IO.File]::ReadAllText((Join-Path $EvidenceDirectory 'managed.xml'))
        $assemblies = @($report.SelectNodes('//assembly')); $cases = @($report.SelectNodes('//test'))
        if ($assemblies.Count -ne 1 -or $cases.Count -ne 2 -or $assemblies[0].passed -ne 2 -or $assemblies[0].failed -ne 0 -or
            $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or $assemblies[0].'not-run' -ne 0 -or
            @($report.SelectNodes('//errors/error')).Count -ne 0 -or @($cases | Where-Object { $_.result -cne 'Pass' -or
            $_.method -cne 'PlayableClassL1TupleMatchesNativeLoader' }).Count -ne 0) { throw 'Both exact native class comparisons must pass.' }
    }
    $classRecord.Passed = $true
} catch { $classRecord.Error = $_.Exception.Message; throw }
finally {
    foreach ($name in $classVariables) { Restore-CathedralOracleEnvironmentVariable $name $classPrevious[$name] }
    Pop-Location
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'result.json'), ($classRecord | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}
Write-Host "PASS: canonical native class preparation. Evidence: $EvidenceDirectory"
