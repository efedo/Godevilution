<#
    Controlled ordinary Cathedral population through CreateThemeRooms.
    Zero arguments; two private native captures and sixteen managed comparisons.
    No client, runtime profile, campaign lifecycle or user save is changed.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$fullRepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
# Import declarations only, not the shared runners' executable bodies.
foreach ($fullImport in @(
        @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
            'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
            'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
        @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
        @{ File = 'test-theme-room-generation.ps1'; Names = @('Get-ThemeRoomArtifact') })) {
    $fullTokens = $null; $fullParseErrors = $null
    $fullAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $fullImport.File), [ref]$fullTokens, [ref]$fullParseErrors)
    if ($fullParseErrors.Count) { throw 'Shared full-population oracle declarations did not parse.' }
    foreach ($fullName in $fullImport.Names) {
        $fullDeclarations = @($fullAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $fullName
        }, $true))
        if ($fullDeclarations.Count -ne 1) { throw "Expected exactly one shared declaration for $fullName." }
        . ([scriptblock]::Create($fullDeclarations[0].Extent.Text))
    }
}

function Assert-CathedralFullExternalPath([string]$Path, [string]$RepositoryRoot) {
    $absolute = [IO.Path]::GetFullPath($Path)
    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
    if ($absolute.Equals($repository, [StringComparison]::OrdinalIgnoreCase) -or
        $absolute.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Licensed full-population evidence must remain outside the repository.'
    }
    $cursor = $absolute
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $entry = Get-Item -LiteralPath $cursor -Force
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Full-population evidence may not traverse reparse points.'
            }
        }
        $parent = [IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
}

function Assert-CathedralFullNativeReport([string]$Path) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne 1 -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne 1 -or $cases[0].classname -cne 'ThemeRoomGenerationTest' -or
        $cases[0].name -cne 'CaptureComposedOrdinaryPopulation' -or $cases[0].status -cne 'RUN' -or
        $cases[0].result -cne 'COMPLETED' -or @($cases[0].failures).Where({ $null -ne $_ }).Count -ne 0) {
        throw 'The exact full-population native fixture must complete without exclusions.'
    }
}

function Assert-CathedralFullManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $cases = @($report.SelectNodes('//test')); $assemblies = @($report.SelectNodes('//assembly'))
    $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralFullPopulationNativeOracleTests'
    $names = @(foreach ($depth in 1..4) { foreach ($seed in @(2588, 743271966)) {
        foreach ($entry in @('main', 'previous')) {
            $type + '.FullPopulationMatchesNative(depth: ' + $depth + ', seed: ' + $seed + ', entry: \"' + $entry + '\")'
        }
    } })
    if ($assemblies.Count -ne 1 -or $assemblies[0].total -ne 16 -or $assemblies[0].passed -ne 16 -or
        $assemblies[0].failed -ne 0 -or $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or
        $assemblies[0].'not-run' -ne 0 -or @($report.SelectNodes('//errors/error')).Count -ne 0 -or
        $cases.Count -ne 16 -or @($cases | Where-Object { $_.result -cne 'Pass' -or $_.type -cne $type -or
            $_.method -cne 'FullPopulationMatchesNative' }).Count -ne 0 -or
        @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'All sixteen exact explicit full-population comparisons must pass without exclusions.'
    }
}

function Assert-CathedralFullRepeatability($First, $Second) {
    if (@($First).Count -ne 1 -or @($Second).Count -ne 1 -or $null -eq $First -or $null -eq $Second -or
        $First.Name -cne 'native-full-population.json' -or $Second.Name -cne $First.Name -or
        $First.Length -le 0 -or $First.Length -gt 128MB -or $First.Length -ne $Second.Length -or
        $First.Sha256 -cnotmatch '^[0-9A-F]{64}$' -or $First.Sha256 -cne $Second.Sha256) {
        throw 'Two complete independent full-population captures must be byte-identical.'
    }
}

function Get-CathedralFullUpstreamEvidence([string]$Output, [string]$RepositoryRoot) {
    $matches = [regex]::Matches($Output,
        '(?m)^PASS: sixteen composed InitItems comparisons, thirty-two prerequisite/encounter checks and repeated 17\+1\+1\+1 captures\. Evidence: ([^\r\n]+)\r?$')
    if ($matches.Count -ne 1) { throw 'A single successful prerequisite evidence path is required.' }
    $path = $matches[0].Groups[1].Value
    Assert-CathedralFullExternalPath $path $RepositoryRoot
    $record = [IO.File]::ReadAllText((Join-Path $path 'result.json')) | ConvertFrom-Json
    if ($record.Passed -ne $true -or $record.Scope -cne 'native-cathedral-l1-composed-inititems' -or
        @($record.Captures).Count -ne 2) { throw 'Completed repeated InitItems prerequisite evidence is required.' }
    foreach ($capture in $record.Captures) {
        foreach ($field in @('ArtifactDirectory', 'InputDirectory', 'EncounterDirectory', 'InitialItemDirectory')) {
            Assert-CathedralFullExternalPath $capture.$field $RepositoryRoot
        }
    }
    return [pscustomobject]@{ Path = $path; FirstCapture = $record.Captures[0] }
}

$fullBuild = Find-CathedralOracleBuild $fullRepositoryRoot
$fullEvidenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Full-Population-Oracle-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $fullEvidenceRoot) { throw 'The full-population oracle requires a fresh evidence root.' }
Assert-CathedralFullExternalPath $fullEvidenceRoot $fullRepositoryRoot
New-Item -ItemType Directory -Path $fullEvidenceRoot | Out-Null
$fullRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'controlled-original-diablo-cathedral-sp-normal-post-createlevel-through-createthemerooms'
    NativeBuild = $fullBuild.Directory; UpstreamEvidence = $null; Stages = @(); Artifacts = @()
    Error = $null; EvidenceError = $null }
$fullVariables = @('GODEVILUTION_CATHEDRAL_FULL_POPULATION_ORACLE_DIRECTORY',
    'GODEVILUTION_CATHEDRAL_POPULATION_ORACLE_DIRECTORY', 'GODEVILUTION_CATHEDRAL_POPULATION_INPUT_DIRECTORY',
    'GODEVILUTION_CATHEDRAL_ENCOUNTER_ORACLE_DIRECTORY')
$fullPrevious = @{}
foreach ($fullVariable in $fullVariables) { $fullPrevious[$fullVariable] = [Environment]::GetEnvironmentVariable($fullVariable, 'Process') }
$fullPrimaryError = $null
Push-Location $fullRepositoryRoot
try {
    Write-Host "Private full-population evidence: $fullEvidenceRoot"
    $fullPowerShell = (Get-Process -Id $PID).Path
    $fullArguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ' +
        (Quote-CathedralNativeArgument (Join-Path $PSScriptRoot 'test-cathedral-geometry.ps1')) + ' -CathedralInitialItems'
    $fullOutcome = Invoke-CathedralPopulationOwnedProcess $fullPowerShell $fullArguments `
        (Join-Path $fullEvidenceRoot 'upstream.log') 600000
    $fullRecord.Stages += $fullOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $fullOutcome 'Composed InitItems prerequisites'
    $fullUpstream = Get-CathedralFullUpstreamEvidence $fullOutcome.Output $fullRepositoryRoot
    $fullRecord.UpstreamEvidence = $fullUpstream.Path
    $fullBuildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target theme_item_generation_test --parallel 2' -f
        $fullBuild.DeveloperCommand, $fullBuild.CMake, $fullBuild.Directory
    $fullOutcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $fullBuildCommand + '"') `
        (Join-Path $fullEvidenceRoot 'native-build.log') 300000
    $fullRecord.Stages += $fullOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $fullOutcome 'Native full-population build'
    $fullFirstArtifact = $null
    foreach ($fullCaptureIndex in 1..2) {
        $fullDirectory = Join-Path $fullEvidenceRoot "capture-$fullCaptureIndex"
        New-Item -ItemType Directory -Path $fullDirectory | Out-Null
        Assert-CathedralFullExternalPath $fullDirectory $fullRepositoryRoot
        [Environment]::SetEnvironmentVariable($fullVariables[0], $fullDirectory, 'Process')
        $fullReport = Join-Path $fullEvidenceRoot "native-tests-$fullCaptureIndex.json"
        $fullArguments = '--gtest_filter=ThemeRoomGenerationTest.CaptureComposedOrdinaryPopulation --gtest_output=json:' +
            (Quote-CathedralNativeArgument $fullReport)
        $fullOutcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $fullBuild.Directory 'theme_item_generation_test.exe') `
            $fullArguments (Join-Path $fullEvidenceRoot "native-tests-$fullCaptureIndex.log") 120000
        $fullRecord.Stages += $fullOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $fullOutcome 'Native full-population capture'
        Assert-CathedralFullNativeReport $fullReport
        $fullArtifact = Get-ThemeRoomArtifact $fullDirectory 'native-full-population.json' 128MB
        if ($null -eq $fullFirstArtifact) { $fullFirstArtifact = $fullArtifact; $fullManagedDirectory = $fullDirectory }
        else { Assert-CathedralFullRepeatability $fullFirstArtifact $fullArtifact }
        $fullRecord.Artifacts += $fullArtifact
    }
    [Environment]::SetEnvironmentVariable($fullVariables[0], $fullManagedDirectory, 'Process')
    [Environment]::SetEnvironmentVariable($fullVariables[1], $fullUpstream.FirstCapture.ArtifactDirectory, 'Process')
    [Environment]::SetEnvironmentVariable($fullVariables[2], $fullUpstream.FirstCapture.InputDirectory, 'Process')
    [Environment]::SetEnvironmentVariable($fullVariables[3], $fullUpstream.FirstCapture.EncounterDirectory, 'Process')
    $fullArguments = '--no-ansi --parallel none --filter-method "*AuthoritativeCathedralFullPopulationNativeOracleTests.FullPopulationMatchesNative*" --explicit only --fail-skips on --report-xunit --results-directory ' +
        (Quote-CathedralNativeArgument $fullEvidenceRoot) + ' --report-xunit-filename managed-tests.xml'
    $fullOutcome = Invoke-CathedralPopulationOwnedProcess `
        (Join-Path $fullRepositoryRoot 'server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe') `
        $fullArguments (Join-Path $fullEvidenceRoot 'managed-tests.log') 180000
    $fullRecord.Stages += $fullOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $fullOutcome 'Managed full-population parity'
    Assert-CathedralFullManagedReport (Join-Path $fullEvidenceRoot 'managed-tests.xml')
    $fullRecord.Passed = $true
} catch { $fullPrimaryError = $_; $fullRecord.Error = $_.Exception.Message }
finally {
    foreach ($fullVariable in $fullVariables) { Restore-CathedralOracleEnvironmentVariable $fullVariable $fullPrevious[$fullVariable] }
    Pop-Location; $fullRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try { [IO.File]::WriteAllText((Join-Path $fullEvidenceRoot 'result.json'), ($fullRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
    catch { $fullRecord.EvidenceError = $_.Exception.Message; if ($null -eq $fullPrimaryError) { $fullPrimaryError = $_ } }
}
if ($null -ne $fullPrimaryError) { throw $fullPrimaryError }
Write-Host "PASS: sixteen composed full-population comparisons and byte-identical native captures, with forty-eight upstream comparisons. Evidence: $fullEvidenceRoot"
