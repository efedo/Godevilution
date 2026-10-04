<# Supplemental native monster metadata and methods proof. No MPQ, server, client or Godot process. #>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$runtimeRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
foreach ($runtimeImport in @(
    @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
    @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
    @{ File = 'test-cathedral-full-population.ps1'; Names = @('Assert-CathedralFullExternalPath') })) {
    $runtimeTokens = $null; $runtimeErrors = $null
    $runtimeAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $runtimeImport.File), [ref]$runtimeTokens, [ref]$runtimeErrors)
    if ($runtimeErrors.Count) { throw 'Shared monster metadata declarations did not parse.' }
    foreach ($runtimeName in $runtimeImport.Names) {
        $runtimeDeclarations = @($runtimeAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $runtimeName
        }, $true))
        if ($runtimeDeclarations.Count -ne 1) { throw "Expected exactly one declaration for $runtimeName." }
        . ([scriptblock]::Create($runtimeDeclarations[0].Extent.Text))
    }
}

function Assert-CathedralRuntimeNativeReport([string]$Path) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne 1 -or $cases[0].classname -cne 'ThemeRoomGenerationTest' -or
        $cases[0].name -cne 'CaptureMonsterRuntimeMetadata' -or $cases[0].status -cne 'RUN' -or
        $cases[0].result -cne 'COMPLETED' -or @($cases[0].failures).Where({ $null -ne $_ }).Count -ne 0) {
        throw 'The exact monster metadata native fixture must pass without exclusions.'
    }
}

function Assert-CathedralRuntimeManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $assemblies = @($report.SelectNodes('//assembly')); $cases = @($report.SelectNodes('//test'))
    $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralMonsterRuntimeNativeOracleTests'
    $method = 'CompleteRuntimeMetadataMatchesNativeMonsterMethods'
    if ($assemblies.Count -ne 1 -or $cases.Count -ne 1 -or $assemblies[0].failed -ne 0 -or
        $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or $assemblies[0].'not-run' -ne 0 -or
        @($report.SelectNodes('//errors/error')).Count -ne 0 -or
        $cases[0].result -cne 'Pass' -or $cases[0].type -cne $type -or $cases[0].method -cne $method -or
        $cases[0].name -cne ($type + '.' + $method) -or
        $assemblies[0].total -ne 1 -or $assemblies[0].passed -ne 1) {
        throw 'The exact native monster method comparison must pass without exclusions.'
    }
}

function Get-CathedralRuntimeArtifacts([string]$Directory) {
    $root = Get-Item -LiteralPath $Directory -Force
    $entries = @(Get-ChildItem -LiteralPath $Directory -Force | Sort-Object Name -CaseSensitive)
    $names = @('cathedral_population_monster_runtime.tsv', 'native-monster-runtime.json')
    if (-not $root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries.Count -ne 2 -or @(Compare-Object $names @($entries.Name) -CaseSensitive).Count) {
        throw 'Monster metadata evidence requires exactly the corpus and monster runtime table in one regular directory.'
    }
    foreach ($entry in $entries) {
        $limit = if ($entry.Name -ceq 'cathedral_population_monster_runtime.tsv') { 1MB } else { 128MB }
        if ($entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $entry.Length -le 0 -or $entry.Length -gt $limit) { throw 'Monster metadata artifacts must be bounded nonempty regular files.' }
        $stream = $null; $sha = $null
        try {
            $stream = [IO.File]::OpenRead($entry.FullName)
            if ($stream.Length -ne $entry.Length -or $stream.Length -le 0 -or $stream.Length -gt $limit) {
                throw 'Monster metadata artifact size changed before hashing.'
            }
            $sha = [Security.Cryptography.SHA256]::Create()
            $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
            [pscustomobject]@{ Name = $entry.Name; Length = $stream.Length; Sha256 = $hash }
        } finally {
            if ($null -ne $stream) { $stream.Dispose() }
            if ($null -ne $sha) { $sha.Dispose() }
        }
    }
}

function Assert-CathedralRuntimeRepeatability($First, $Second) {
    if (@($First).Count -ne 2 -or @($Second).Count -ne 2) { throw 'Two complete capture pairs are required.' }
    foreach ($name in @('cathedral_population_monster_runtime.tsv', 'native-monster-runtime.json')) {
        $firstMatches = @($First | Where-Object Name -ceq $name); $secondMatches = @($Second | Where-Object Name -ceq $name)
        $limit = if ($name -ceq 'cathedral_population_monster_runtime.tsv') { 1MB } else { 128MB }
        if ($firstMatches.Count -ne 1 -or $secondMatches.Count -ne 1 -or
            $firstMatches[0].Length -le 0 -or $firstMatches[0].Length -gt $limit -or
            $firstMatches[0].Length -ne $secondMatches[0].Length -or
            $firstMatches[0].Sha256 -cnotmatch '^[0-9A-F]{64}$' -or
            $firstMatches[0].Sha256 -cne $secondMatches[0].Sha256) {
            throw 'Both independent native corpus and monster runtime captures must be byte-identical.'
        }
    }
}

$runtimeBuild = Find-CathedralOracleBuild $runtimeRepository
$runtimeRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Monster-Runtime-Oracle-' + [guid]::NewGuid().ToString('N'))
Assert-CathedralFullExternalPath $runtimeRoot $runtimeRepository
New-Item -ItemType Directory -Path $runtimeRoot | Out-Null
$runtimeRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'supplemental-138-native-monster-rows-and-720-native-method-cases-not-runtime-installation'
    NativeBuild = $runtimeBuild.Directory
    Stages = @(); Artifacts = @(); Error = $null; EvidenceError = $null }
$runtimeVariables = @('GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_ORACLE_DIRECTORY')
$runtimePrevious = @{}
foreach ($variable in $runtimeVariables) { $runtimePrevious[$variable] = [Environment]::GetEnvironmentVariable($variable, 'Process') }
$runtimePrimaryError = $null
Push-Location $runtimeRepository
try {
    Write-Host "Private monster metadata evidence: $runtimeRoot"
    $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
        $runtimeBuild.DeveloperCommand, $runtimeBuild.CMake, $runtimeBuild.Directory
    $outcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $buildCommand + '"') (Join-Path $runtimeRoot 'native-build.log') 300000
    $runtimeRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $outcome 'Native monster metadata build'
    foreach ($capture in 1..2) {
        $captureDirectory = Join-Path $runtimeRoot "capture-$capture"
        New-Item -ItemType Directory -Path $captureDirectory | Out-Null
        [Environment]::SetEnvironmentVariable($runtimeVariables[0], $captureDirectory, 'Process')
        $report = Join-Path $runtimeRoot "native-tests-$capture.json"
        $arguments = '--gtest_filter=ThemeRoomGenerationTest.CaptureMonsterRuntimeMetadata --gtest_output=json:' + (Quote-CathedralNativeArgument $report)
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $runtimeBuild.Directory 'theme_item_generation_test.exe') $arguments (Join-Path $runtimeRoot "native-tests-$capture.log") 120000
        $runtimeRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $outcome 'Native monster metadata capture'
        Assert-CathedralRuntimeNativeReport $report
        $captureArtifacts = @(Get-CathedralRuntimeArtifacts $captureDirectory)
        if ($capture -eq 2) { Assert-CathedralRuntimeRepeatability $runtimeRecord.Artifacts $captureArtifacts }
        $runtimeRecord.Artifacts += $captureArtifacts
    }
    $outcome = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application).Source `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --no-restore --disable-build-servers -v minimal' (Join-Path $runtimeRoot 'managed-build.log') 180000
    $runtimeRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $outcome 'Managed monster metadata build'
    [Environment]::SetEnvironmentVariable($runtimeVariables[0], (Join-Path $runtimeRoot 'capture-1'), 'Process')
    foreach ($verification in @('parity')) {
        $method = '*AuthoritativeCathedralMonsterRuntimeNativeOracleTests.CompleteRuntimeMetadataMatchesNativeMonsterMethods*'
        $name = 'parity'
        $arguments = '--no-ansi --parallel none --filter-method ' + (Quote-CathedralNativeArgument $method) +
            ' --explicit on --fail-skips on --report-xunit --results-directory ' + (Quote-CathedralNativeArgument $runtimeRoot) + ' --report-xunit-filename ' + $name + '.xml'
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $runtimeRepository 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') `
            $arguments (Join-Path $runtimeRoot ($name + '.log')) 180000
        $runtimeRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $outcome 'Managed monster metadata verification'
        Assert-CathedralRuntimeManagedReport (Join-Path $runtimeRoot ($name + '.xml'))
    }
    $runtimeRecord.Passed = $true
} catch { $runtimePrimaryError = $_; $runtimeRecord.Error = $_.Exception.Message }
finally {
    foreach ($variable in $runtimeVariables) { Restore-CathedralOracleEnvironmentVariable $variable $runtimePrevious[$variable] }
    Pop-Location; $runtimeRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try { [IO.File]::WriteAllText((Join-Path $runtimeRoot 'result.json'), ($runtimeRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
    catch { $runtimeRecord.EvidenceError = $_.Exception.Message; if ($null -eq $runtimePrimaryError) { $runtimePrimaryError = $_ } }
}
if ($null -ne $runtimePrimaryError) { throw $runtimePrimaryError }
Write-Host "PASS: 720 actual native monster method cases and byte-identical corpus/metadata captures. Evidence: $runtimeRoot"
