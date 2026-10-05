<# Independent death-time Cathedral loot proof; private evidence, no runtime profile or client. #>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$lootRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
foreach ($lootImport in @(
    @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
    @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
    @{ File = 'test-cathedral-full-population.ps1'; Names = @('Assert-CathedralFullExternalPath') })) {
    $lootTokens = $null; $lootErrors = $null
    $lootAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $lootImport.File), [ref]$lootTokens, [ref]$lootErrors)
    if ($lootErrors.Count) { throw 'Shared monster-loot declarations did not parse.' }
    foreach ($lootName in $lootImport.Names) {
        $lootDeclarations = @($lootAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $lootName
        }, $true))
        if ($lootDeclarations.Count -ne 1) { throw "Expected exactly one declaration for $lootName." }
        . ([scriptblock]::Create($lootDeclarations[0].Extent.Text))
    }
}

function Test-MonsterLootInteger($Value, [long]$Expected) {
    return ($Value -is [int] -or $Value -is [long]) -and $Value -eq $Expected
}

function Assert-MonsterLootNativeReport([string]$Path, [string]$ExpectedClass, [string[]]$ExpectedNames) {
    if ($ExpectedNames.Count -eq 0 -or @($ExpectedNames | Select-Object -Unique).Count -ne $ExpectedNames.Count) {
        throw 'Native loot report requires a nonempty unique expected fixture inventory.'
    }
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if (-not (Test-MonsterLootInteger $report.tests $ExpectedNames.Count) -or
        -not (Test-MonsterLootInteger $report.failures 0) -or
        -not (Test-MonsterLootInteger $report.disabled 0) -or
        -not (Test-MonsterLootInteger $report.errors 0) -or $cases.Count -ne $ExpectedNames.Count -or
        @($cases | Where-Object { $_.classname -cne $ExpectedClass -or $_.status -cne 'RUN' -or
            $_.result -cne 'COMPLETED' -or @($_.failures).Where({ $null -ne $_ }).Count -ne 0 }).Count -ne 0 -or
        @(Compare-Object $ExpectedNames @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'Every exact native monster-loot fixture must complete without skips or report errors.'
    }
}

function Assert-MonsterLootManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $assemblies = @($report.SelectNodes('//assembly')); $cases = @($report.SelectNodes('//test'))
    $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralMonsterLootNativeOracleTests'
    $method = 'DeathTimeLootMatchesNative'
    $names = @(1..4 | ForEach-Object { "${type}.${method}(depth: $_)" })
    if ($assemblies.Count -ne 1 -or $assemblies[0].total -cne '4' -or $assemblies[0].passed -cne '4' -or
        $assemblies[0].failed -cne '0' -or $assemblies[0].skipped -cne '0' -or
        $assemblies[0].errors -cne '0' -or $assemblies[0].'not-run' -cne '0' -or
        @($report.SelectNodes('//errors/error')).Count -ne 0 -or $cases.Count -ne 4 -or
        @($cases | Where-Object { $_.result -cne 'Pass' -or $_.type -cne $type -or $_.method -cne $method }).Count -ne 0 -or
        @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'All four exact death-time loot depth comparisons must pass without exclusions.'
    }
}

function Get-MonsterLootArtifact([string]$Directory) {
    $cursor = [IO.Path]::GetFullPath($Directory)
    while ($cursor) {
        $entry = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
        if (-not $entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Monster-loot evidence may not traverse non-directory or reparse-point ancestors.'
        }
        $parent = [IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
    $entries = @(Get-ChildItem -LiteralPath $Directory -Force)
    if ($entries.Count -ne 1 -or $entries[0].Name -cne 'native-cathedral-monster-loot.json' -or
        $entries[0].PSIsContainer -or ($entries[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries[0].Length -le 0 -or $entries[0].Length -gt 128MB) {
        throw 'Monster-loot capture requires exactly one nonempty bounded regular corpus.'
    }
    $stream = $null; $sha = $null
    try {
        $stream = [IO.File]::OpenRead($entries[0].FullName)
        if ($stream.Length -ne $entries[0].Length -or $stream.Length -le 0 -or $stream.Length -gt 128MB) {
            throw 'Monster-loot corpus size changed before hashing.'
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        return [pscustomobject]@{ Name = $entries[0].Name; Length = $stream.Length
            Sha256 = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    } finally {
        if ($null -ne $sha) { $sha.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Assert-MonsterLootRepeatability($First, $Second) {
    $firstEntries = @($First); $secondEntries = @($Second)
    if ($firstEntries.Count -ne 1 -or $secondEntries.Count -ne 1) {
        throw 'Two complete independent monster-loot captures must be byte-identical.'
    }
    # Array.Length is the inventory count, not the retained artifact size.
    $firstArtifact = $firstEntries[0]; $secondArtifact = $secondEntries[0]
    if ($firstArtifact.Name -cne 'native-cathedral-monster-loot.json' -or $secondArtifact.Name -cne $firstArtifact.Name -or
        -not ($firstArtifact.Length -is [long] -or $firstArtifact.Length -is [int]) -or
        -not ($secondArtifact.Length -is [long] -or $secondArtifact.Length -is [int]) -or
        $firstArtifact.Length -le 0 -or $firstArtifact.Length -gt 128MB -or $secondArtifact.Length -ne $firstArtifact.Length -or
        $firstArtifact.Sha256 -cnotmatch '^[0-9A-F]{64}$' -or $secondArtifact.Sha256 -cne $firstArtifact.Sha256) {
        throw 'Two complete independent monster-loot captures must be byte-identical.'
    }
}

$lootBuild = Find-CathedralOracleBuild $lootRepository
$lootRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Monster-Loot-Oracle-' + [guid]::NewGuid().ToString('N'))
Assert-CathedralFullExternalPath $lootRoot $lootRepository
if (Test-Path -LiteralPath $lootRoot) { throw 'Monster-loot proof requires a fresh evidence root.' }
New-Item -ItemType Directory -Path $lootRoot | Out-Null
$lootRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'detached-original-diablo-cathedral-sp-normal-death-reseed-and-actual-SpawnLoot-not-MonsterDeath-or-live-runtime'
    NativeBuild = $lootBuild.Directory; Stages = @(); Artifacts = @(); CaseCount = $null; Error = $null; EvidenceError = $null }
$lootVariable = 'GODEVILUTION_CATHEDRAL_MONSTER_LOOT_ORACLE_DIRECTORY'
$lootPrevious = [Environment]::GetEnvironmentVariable($lootVariable, 'Process')
$lootPrimaryError = $null
Push-Location $lootRepository
try {
    Write-Host "Private monster-loot evidence: $lootRoot"
    $lootBuildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target cathedral_monster_loot_test --parallel 2' -f
        $lootBuild.DeveloperCommand, $lootBuild.CMake, $lootBuild.Directory
    $lootOutcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $lootBuildCommand + '"') (Join-Path $lootRoot 'native-build.log') 300000
    $lootRecord.Stages += $lootOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $lootOutcome 'Native death-time loot build'
    foreach ($lootCapture in 1..2) {
        $lootDirectory = Join-Path $lootRoot "capture-$lootCapture"
        New-Item -ItemType Directory -Path $lootDirectory | Out-Null
        [Environment]::SetEnvironmentVariable($lootVariable, $lootDirectory, 'Process')
        $lootReport = Join-Path $lootRoot "native-tests-$lootCapture.json"
        $lootArguments = '--gtest_filter=CathedralMonsterLootOracleTest.CaptureOrdinaryMonsterDeathLoot --gtest_output=json:' + (Quote-CathedralNativeArgument $lootReport)
        $lootOutcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $lootBuild.Directory 'cathedral_monster_loot_test.exe') $lootArguments (Join-Path $lootRoot "native-tests-$lootCapture.log") 120000
        $lootRecord.Stages += $lootOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
        Assert-ThemeOwnedStage $lootOutcome 'Native death-time loot capture'
        Assert-MonsterLootNativeReport $lootReport 'CathedralMonsterLootOracleTest' @('CaptureOrdinaryMonsterDeathLoot')
        $lootArtifact = Get-MonsterLootArtifact $lootDirectory
        if ($lootCapture -eq 2) { Assert-MonsterLootRepeatability $lootRecord.Artifacts $lootArtifact }
        $lootRecord.Artifacts += $lootArtifact
        $lootCorpus = [IO.File]::ReadAllText((Join-Path $lootDirectory $lootArtifact.Name)) | ConvertFrom-Json
        if ($lootCorpus.format -cne 'godevilution-cathedral-monster-loot' -or -not (Test-MonsterLootInteger $lootCorpus.version 1) -or
            -not (Test-MonsterLootInteger $lootCorpus.caseCount 68) -or @($lootCorpus.cases).Count -ne 68 -or
            -not (Test-MonsterLootInteger $lootCorpus.depthCount 4) -or -not (Test-MonsterLootInteger $lootCorpus.lanesPerDepth 17)) {
            throw 'Native death-time loot identity/matrix counters are incomplete.'
        }
        $lootRecord.CaseCount = $lootCorpus.caseCount
        $lootCorpus = $null
    }
    $lootOutcome = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --configuration Release --no-restore --disable-build-servers -v minimal' (Join-Path $lootRoot 'managed-build.log') 180000
    $lootRecord.Stages += $lootOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $lootOutcome 'Managed death-time loot build'
    [Environment]::SetEnvironmentVariable($lootVariable, (Join-Path $lootRoot 'capture-1'), 'Process')
    $lootArguments = '--no-ansi --parallel none --filter-method "*AuthoritativeCathedralMonsterLootNativeOracleTests.DeathTimeLootMatchesNative*" --explicit only --fail-skips on --report-xunit --results-directory ' +
        (Quote-CathedralNativeArgument $lootRoot) + ' --report-xunit-filename managed-tests.xml'
    $lootOutcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $lootRepository 'server/tests/Devilution.Diablo.Tests/bin/Release/net8.0/Devilution.Diablo.Tests.exe') `
        $lootArguments (Join-Path $lootRoot 'managed-tests.log') 120000
    $lootRecord.Stages += $lootOutcome | Select-Object Pid, ExitCode, TimedOut, ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $lootOutcome 'Managed death-time loot parity'
    Assert-MonsterLootManagedReport (Join-Path $lootRoot 'managed-tests.xml')
    $lootRecord.Passed = $true
} catch { $lootPrimaryError = $_; $lootRecord.Error = $_.Exception.Message }
finally {
    Restore-CathedralOracleEnvironmentVariable $lootVariable $lootPrevious
    Pop-Location
    $lootRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try { [IO.File]::WriteAllText((Join-Path $lootRoot 'result.json'), ($lootRecord | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
    catch { $lootRecord.EvidenceError = $_.Exception.Message; if ($null -eq $lootPrimaryError) { $lootPrimaryError = $_ } }
}
if ($null -ne $lootPrimaryError) { throw $lootPrimaryError }
Write-Host "PASS: 68 actual native death-time loot cases, four depth comparisons and byte-identical captures. Evidence: $lootRoot"
