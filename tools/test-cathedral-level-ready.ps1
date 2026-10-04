<# Cold post-theme Cathedral loader handoff before network join. No server/client is launched. #>
[CmdletBinding()]
param([Parameter(Mandatory = $true)] [string]$MetadataPackDirectory)

$ErrorActionPreference = 'Stop'
$readyRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
foreach ($readyImport in @(
    @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
    @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
    @{ File = 'test-cathedral-full-population.ps1'; Names = @('Assert-CathedralFullExternalPath') })) {
    $readyTokens = $null; $readyErrors = $null
    $readyAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $readyImport.File), [ref]$readyTokens, [ref]$readyErrors)
    if ($readyErrors.Count) { throw 'Shared ready-population declarations did not parse.' }
    foreach ($readyName in $readyImport.Names) {
        $readyDeclarations = @($readyAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $readyName
        }, $true))
        if ($readyDeclarations.Count -ne 1) { throw "Expected exactly one declaration for $readyName." }
        . ([scriptblock]::Create($readyDeclarations[0].Extent.Text))
    }
}

function Assert-CathedralReadyNativeReport([string]$Path) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne 1 -or $cases[0].classname -cne 'ThemeRoomGenerationTest' -or
        $cases[0].name -cne 'CaptureFreshLevelReady' -or $cases[0].status -cne 'RUN' -or
        $cases[0].result -cne 'COMPLETED' -or @($cases[0].failures).Where({ $null -ne $_ }).Count -ne 0) {
        throw 'The exact ready-population native fixture must pass without exclusions.'
    }
}

function Assert-CathedralReadyManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $assemblies = @($report.SelectNodes('//assembly')); $cases = @($report.SelectNodes('//test'))
    $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralLevelReadyNativeOracleTests'
    $names = @(foreach ($depth in 1..4) { foreach ($seed in @(2588, 743271966)) { foreach ($entry in @('main', 'previous')) {
            $type + '.LevelReadyMatchesNative(depth: ' + $depth + ', seed: ' + $seed + ', entry: \"' + $entry + '\")'
    } } })
    if ($assemblies.Count -ne 1 -or $cases.Count -ne 16 -or $assemblies[0].failed -ne 0 -or
        $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or $assemblies[0].'not-run' -ne 0 -or
        @($report.SelectNodes('//errors/error')).Count -ne 0 -or
        @($cases | Where-Object { $_.result -cne 'Pass' -or $_.type -cne $type -or $_.method -cne 'LevelReadyMatchesNative' }).Count -ne 0 -or
        $assemblies[0].total -ne 16 -or $assemblies[0].passed -ne 16 -or
        @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'The exact sixteen post-theme loader comparisons must pass without exclusions.'
    }
}

function Get-CathedralReadyArtifacts([string]$Directory) {
    $root = Get-Item -LiteralPath $Directory -Force
    $entries = @(Get-ChildItem -LiteralPath $Directory -Force | Sort-Object Name -CaseSensitive)
    $names = @('cathedral_automap.tsv', 'native-level-ready.json')
    if (-not $root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries.Count -ne 2 -or @(Compare-Object $names @($entries.Name) -CaseSensitive).Count) {
        throw 'Loader evidence requires exactly the corpus and automap table in one regular directory.'
    }
    foreach ($entry in $entries) {
        $limit = if ($entry.Name -ceq 'cathedral_automap.tsv') { 1MB } else { 128MB }
        if ($entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $entry.Length -le 0 -or $entry.Length -gt $limit) { throw 'Loader artifacts must be bounded nonempty regular files.' }
        $stream = $null; $sha = $null
        try {
            $stream = [IO.File]::OpenRead($entry.FullName)
            if ($stream.Length -ne $entry.Length -or $stream.Length -le 0 -or $stream.Length -gt $limit) {
                throw 'Loader artifact size changed before hashing.'
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

function Assert-CathedralReadyRepeatability($First, $Second) {
    if (@($First).Count -ne 2 -or @($Second).Count -ne 2) { throw 'Two complete capture pairs are required.' }
    foreach ($name in @('cathedral_automap.tsv', 'native-level-ready.json')) {
        $firstMatches = @($First | Where-Object Name -ceq $name); $secondMatches = @($Second | Where-Object Name -ceq $name)
        $limit = if ($name -ceq 'cathedral_automap.tsv') { 1MB } else { 128MB }
        if ($firstMatches.Count -ne 1 -or $secondMatches.Count -ne 1 -or
            $firstMatches[0].Length -le 0 -or $firstMatches[0].Length -gt $limit -or
            $firstMatches[0].Length -ne $secondMatches[0].Length -or
            $firstMatches[0].Sha256 -cnotmatch '^[0-9A-F]{64}$' -or
            $firstMatches[0].Sha256 -cne $secondMatches[0].Sha256) {
            throw 'Both independent native corpus and automap captures must be byte-identical.'
        }
    }
}

$MetadataPackDirectory = (Resolve-Path -LiteralPath $MetadataPackDirectory).Path
Assert-CathedralFullExternalPath $MetadataPackDirectory $readyRepository
if (-not (Test-Path -LiteralPath $MetadataPackDirectory -PathType Container)) { throw 'An explicit compiled metadata directory is required.' }
$readyBuild = Find-CathedralOracleBuild $readyRepository
$readyRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Level-Ready-Oracle-' + [guid]::NewGuid().ToString('N'))
Assert-CathedralFullExternalPath $readyRoot $readyRepository
New-Item -ItemType Directory -Path $readyRoot | Out-Null
$readyRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'fresh-cold-original-diablo-cathedral-sp-normal-post-theme-through-light-vision-before-join'
    MetadataPackDirectory = $MetadataPackDirectory; NativeBuild = $readyBuild.Directory
    Stages = @(); Artifacts = @(); Error = $null; EvidenceError = $null }
$readyVariables = @('GODEVILUTION_CATHEDRAL_LEVEL_READY_ORACLE_DIRECTORY', 'GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY')
$readyPrevious = @{}
foreach ($variable in $readyVariables) { $readyPrevious[$variable] = [Environment]::GetEnvironmentVariable($variable, 'Process') }
$readyPrimaryError = $null
Push-Location $readyRepository
try {
    Write-Host "Private ready-population evidence: $readyRoot"
    $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
        $readyBuild.DeveloperCommand, $readyBuild.CMake, $readyBuild.Directory
    $outcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $buildCommand + '"') (Join-Path $readyRoot 'native-build.log') 300000
    $readyRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $outcome 'Native ready-population build'
    foreach ($capture in 1..2) {
        $captureDirectory = Join-Path $readyRoot "capture-$capture"
        New-Item -ItemType Directory -Path $captureDirectory | Out-Null
        [Environment]::SetEnvironmentVariable($readyVariables[0], $captureDirectory, 'Process')
        $report = Join-Path $readyRoot "native-tests-$capture.json"
        $arguments = '--gtest_filter=ThemeRoomGenerationTest.CaptureFreshLevelReady --gtest_output=json:' + (Quote-CathedralNativeArgument $report)
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $readyBuild.Directory 'theme_item_generation_test.exe') $arguments (Join-Path $readyRoot "native-tests-$capture.log") 120000
        $readyRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $outcome 'Native ready-population capture'
        Assert-CathedralReadyNativeReport $report
        $captureArtifacts = @(Get-CathedralReadyArtifacts $captureDirectory)
        if ($capture -eq 2) { Assert-CathedralReadyRepeatability $readyRecord.Artifacts $captureArtifacts }
        $readyRecord.Artifacts += $captureArtifacts
    }
    $outcome = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application).Source `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --no-restore --disable-build-servers -v minimal' (Join-Path $readyRoot 'managed-build.log') 180000
    $readyRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $outcome 'Managed ready-population build'
    [Environment]::SetEnvironmentVariable($readyVariables[0], (Join-Path $readyRoot 'capture-1'), 'Process')
    [Environment]::SetEnvironmentVariable($readyVariables[1], $MetadataPackDirectory, 'Process')
    foreach ($verification in @('parity')) {
        $method = '*AuthoritativeCathedralLevelReadyNativeOracleTests.LevelReadyMatchesNative*'
        $name = 'parity'
        $arguments = '--no-ansi --parallel none --filter-method ' + (Quote-CathedralNativeArgument $method) +
            ' --explicit on --fail-skips on --report-xunit --results-directory ' + (Quote-CathedralNativeArgument $readyRoot) + ' --report-xunit-filename ' + $name + '.xml'
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $readyRepository 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') `
            $arguments (Join-Path $readyRoot ($name + '.log')) 180000
        $readyRecord.Stages += $outcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $outcome 'Managed ready-population verification'
        Assert-CathedralReadyManagedReport (Join-Path $readyRoot ($name + '.xml'))
    }
    $readyRecord.Passed = $true
} catch { $readyPrimaryError = $_; $readyRecord.Error = $_.Exception.Message }
finally {
    foreach ($variable in $readyVariables) { Restore-CathedralOracleEnvironmentVariable $variable $readyPrevious[$variable] }
    Pop-Location; $readyRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try { [IO.File]::WriteAllText((Join-Path $readyRoot 'result.json'), ($readyRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
    catch { $readyRecord.EvidenceError = $_.Exception.Message; if ($null -eq $readyPrimaryError) { $readyPrimaryError = $_ } }
}
if ($null -ne $readyPrimaryError) { throw $readyPrimaryError }
Write-Host "PASS: sixteen native loader-handoff comparisons and byte-identical corpus/automap captures. Evidence: $readyRoot"
