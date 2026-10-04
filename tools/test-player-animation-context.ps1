<# Native effective player-animation metadata proof. No client, server, MPQ or lifecycle activation. #>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$contextRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
foreach ($contextImport in @(
    @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
    @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') },
    @{ File = 'test-animation-timing.ps1'; Names = @('Assert-AnimationTimingRegularPath') })) {
    $contextTokens = $null; $contextErrors = $null
    $contextAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $contextImport.File), [ref]$contextTokens, [ref]$contextErrors)
    if ($contextErrors.Count) { throw 'Shared context proof declarations did not parse.' }
    foreach ($contextName in $contextImport.Names) {
        $contextDeclarations = @($contextAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $contextName
        }, $true))
        if ($contextDeclarations.Count -ne 1) { throw "Expected exactly one declaration for $contextName." }
        . ([scriptblock]::Create($contextDeclarations[0].Extent.Text))
    }
}

function Assert-PlayerAnimationContextNativeReport([string]$Path, [bool]$CaptureOnly) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $names = @('CaptureContextOracle')
    if (-not $CaptureOnly) {
        $names += @('TownPreservesIncomingCombatFrames', 'DungeonReplacesAllFramesWithoutAdvancingCursorOrRng',
            'SpellGraphicsUseRawMagicTypeIncludingNull')
    }
    foreach ($field in @('tests', 'failures', 'disabled', 'errors')) {
        if ($report.PSObject.Properties.Name -cnotcontains $field -or
            ($report.$field -isnot [int] -and $report.$field -isnot [long])) {
            throw "Context native report is missing a valid $field."
        }
    }
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne $names.Count -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne $names.Count -or @($cases | Where-Object {
            $_.classname -cne 'NativePlayerAnimationContext' -or $_.status -cne 'RUN' -or $_.result -cne 'COMPLETED' -or
            @($_.failures).Where({ $null -ne $_ }).Count -ne 0
        }).Count -ne 0 -or @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'Exactly the required native context fixtures must complete without exclusions.'
    }
}

function Get-PlayerAnimationContextManagedCases {
    $portable = 'Devilution.Diablo.Tests.AuthoritativePlayerAnimationContextCatalogTests'
    foreach ($method in @('ResolvesExplicitDungeonExceptionsFromPreparedGraphics',
        'TownApplicationPreservesIncomingAttackHitAndBlockFields', 'UnknownClassesLevelsAndGraphicsFailClosed',
        'SpellGraphicUsesExplicitNativeSchoolAndNotDamageType', 'SpellRowsRequireCompleteStableNativeBindings',
        'FingerprintIsDetachedUppercaseAndInputSensitive', 'NullAndZeroClassExceptionsAreDisabledWithoutClassNameInference',
        'NativeGraphicTimingUsesSourceRatesAndSignedWidthValidation', 'RowOrderDoesNotChangeFingerprint',
        'MalformedTextIdentitiesFailClosed', 'InputRowsAndGetterSnapshotsAreDetached',
        'DuplicateAndUnknownContextReferencesAndInvalidRowsFailClosed',
        'StableAndNativeSpellIdentitiesDoNotFallbackAcrossNamespaces')) {
        [pscustomobject]@{ Type = $portable; Method = $method; Name = $portable + '.' + $method }
    }
    $oracle = 'Devilution.Diablo.Tests.NativePlayerAnimationContextOracleTests'
    foreach ($method in @('MatchesCompleteNativeContextCorpus', 'InputFingerprintBindsInputsButNotExpectedOutputs',
        'EvidenceShapeRejectsMissingExtraAndDuplicateFields')) {
        [pscustomobject]@{ Type = $oracle; Method = $method; Name = $oracle + '.' + $method }
    }
}

function Assert-PlayerAnimationContextManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $assemblies = @($report.SelectNodes('//assembly')); $cases = @($report.SelectNodes('//test'))
    $required = @(Get-PlayerAnimationContextManagedCases)
    if ($assemblies.Count -ne 1 -or $cases.Count -ne $required.Count) { throw 'One exact managed context family is required.' }
    foreach ($field in @('total', 'passed', 'failed', 'skipped', 'errors', 'not-run')) {
        if (-not $assemblies[0].HasAttribute($field) -or $assemblies[0].GetAttribute($field) -cnotmatch '^(0|[1-9][0-9]*)$') {
            throw "Context managed report is missing a valid $field count."
        }
    }
    if ($assemblies[0].total -ne $cases.Count -or $assemblies[0].passed -ne $cases.Count -or
        $assemblies[0].failed -ne 0 -or $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or
        $assemblies[0].'not-run' -ne 0 -or @($report.SelectNodes('//errors/error')).Count -ne 0 -or
        @($cases | Where-Object { $_.result -cne 'Pass' }).Count -ne 0) { throw 'All context cases must pass without exclusions.' }
    foreach ($expected in $required) {
        if (@($cases | Where-Object {
            $_.type -ceq $expected.Type -and $_.method -ceq $expected.Method -and $_.name -ceq $expected.Name
        }).Count -ne 1) { throw "Missing, replaced or duplicate context case $($expected.Name)." }
    }
    return $cases.Count
}

function Get-PlayerAnimationContextArtifact([string]$Directory) {
    Assert-AnimationTimingRegularPath $Directory
    $root = Get-Item -LiteralPath $Directory -Force
    $entries = @(Get-ChildItem -LiteralPath $Directory -Force)
    if (-not $root.PSIsContainer -or $entries.Count -ne 1 -or $entries[0].Name -cne 'player-animation-context.json' -or
        $entries[0].PSIsContainer -or ($entries[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries[0].Length -le 0 -or $entries[0].Length -gt 1000000) {
        throw 'Context capture requires exactly one bounded nonempty regular player-animation-context.json.'
    }
    $stream = $null; $sha = $null
    try {
        $stream = [IO.File]::OpenRead($entries[0].FullName)
        if ($stream.Length -ne $entries[0].Length -or $stream.Length -le 0 -or $stream.Length -gt 1000000) {
            throw 'Context artifact size changed before hashing.'
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        [pscustomobject]@{ Name = $entries[0].Name; Length = $stream.Length
            Sha256 = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    } finally {
        if ($null -ne $stream) { $stream.Dispose() }
        if ($null -ne $sha) { $sha.Dispose() }
    }
}

function Assert-PlayerAnimationContextRepeatability($First, $Second) {
    foreach ($artifact in @($First, $Second)) {
        if (@($artifact).Count -ne 1 -or $artifact.Name -cne 'player-animation-context.json' -or
            $artifact.Length -le 0 -or $artifact.Length -gt 1000000 -or $artifact.Sha256 -cnotmatch '^[0-9A-F]{64}$') {
            throw 'Two complete bounded context artifacts are required.'
        }
    }
    if ($First.Length -ne $Second.Length -or $First.Sha256 -cne $Second.Sha256) { throw 'Context captures must be byte-identical.' }
}

function Add-PlayerAnimationContextStage($Record, $Outcome, [string]$Name) {
    $Record.Stages += $Outcome | Select-Object @{ N = 'Stage'; E = { $Name } }, Pid, ExitCode, TimedOut,
        ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $Outcome $Name
}

$contextBuild = Find-CathedralOracleBuild $contextRepository
$contextRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Player-Animation-Context-' + [guid]::NewGuid().ToString('N'))
Assert-AnimationTimingRegularPath $contextRoot
New-Item -ItemType Directory -Path $contextRoot | Out-Null
$contextRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'prepared-effective-graphics-and-metadata-not-equipment-admission-or-player-lifecycle'
    NativeBuild = $contextBuild.Directory; ManagedTests = $null
    Stages = @(); Artifacts = @(); Error = $null; EvidenceError = $null }
$contextVariable = 'GODEVILUTION_PLAYER_ANIMATION_CONTEXT_ORACLE_DIRECTORY'
$contextPrevious = [Environment]::GetEnvironmentVariable($contextVariable, 'Process')
$contextPrimaryError = $null
Push-Location $contextRepository
try {
    Write-Host "Private player-animation context evidence: $contextRoot"
    $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target player_animation_context_test --parallel 2' -f
        $contextBuild.DeveloperCommand, $contextBuild.CMake, $contextBuild.Directory
    $outcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $buildCommand + '"') (Join-Path $contextRoot 'native-build.log') 300000
    Add-PlayerAnimationContextStage $contextRecord $outcome 'native-build'
    foreach ($capture in 1..2) {
        $captureDirectory = Join-Path $contextRoot "capture-$capture"
        New-Item -ItemType Directory -Path $captureDirectory | Out-Null
        [Environment]::SetEnvironmentVariable($contextVariable, $captureDirectory, 'Process')
        $report = Join-Path $contextRoot "native-tests-$capture.json"
        $filter = if ($capture -eq 1) { 'NativePlayerAnimationContext.*' } else { 'NativePlayerAnimationContext.CaptureContextOracle' }
        $arguments = '--gtest_filter=' + $filter + ' --gtest_output=json:' + (Quote-CathedralNativeArgument $report)
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $contextBuild.Directory 'player_animation_context_test.exe') $arguments (Join-Path $contextRoot "native-tests-$capture.log") 120000
        Add-PlayerAnimationContextStage $contextRecord $outcome "native-capture-$capture"
        Assert-PlayerAnimationContextNativeReport $report ($capture -eq 2)
        $artifact = Get-PlayerAnimationContextArtifact $captureDirectory
        if ($capture -eq 2) { Assert-PlayerAnimationContextRepeatability $contextRecord.Artifacts[0] $artifact }
        $contextRecord.Artifacts += $artifact
    }
    $outcome = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application).Source `
        'build server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj --configuration Release --no-restore --disable-build-servers -v minimal' (Join-Path $contextRoot 'managed-build.log') 180000
    Add-PlayerAnimationContextStage $contextRecord $outcome 'managed-build'
    [Environment]::SetEnvironmentVariable($contextVariable, (Join-Path $contextRoot 'capture-1'), 'Process')
    $arguments = '--no-ansi --parallel none --explicit on --fail-skips on --filter-class "*PlayerAnimationContext*" --report-xunit --results-directory ' +
        (Quote-CathedralNativeArgument $contextRoot) + ' --report-xunit-filename context.xml'
    $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $contextRepository 'server/tests/Devilution.Diablo.Tests/bin/Release/net8.0/Devilution.Diablo.Tests.exe') `
        $arguments (Join-Path $contextRoot 'context.log') 180000
    Add-PlayerAnimationContextStage $contextRecord $outcome 'managed-context'
    $contextRecord.ManagedTests = Assert-PlayerAnimationContextManagedReport (Join-Path $contextRoot 'context.xml')
    foreach ($capture in 1..2) {
        Assert-PlayerAnimationContextRepeatability $contextRecord.Artifacts[$capture - 1] (Get-PlayerAnimationContextArtifact (Join-Path $contextRoot "capture-$capture"))
    }
    $contextRecord.Passed = $true
} catch { $contextPrimaryError = $_; $contextRecord.Error = $_.Exception.Message }
finally {
    Restore-CathedralOracleEnvironmentVariable $contextVariable $contextPrevious
    Pop-Location; $contextRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try {
        $json = (($contextRecord | ConvertTo-Json -Depth 5) -replace '\r?\n', "`r`n") + "`r`n"
        [IO.File]::WriteAllText((Join-Path $contextRoot 'result.json'), $json, [Text.UTF8Encoding]::new($false))
    } catch { $contextRecord.EvidenceError = $_.Exception.Message; if ($null -eq $contextPrimaryError) { $contextPrimaryError = $_ } }
}
if ($null -ne $contextPrimaryError) { throw $contextPrimaryError }
Write-Host "PASS: 648 native context sequences/1944 transitions, nine graphic timings per transition and 89 base/Hellfire spell graphics; $($contextRecord.ManagedTests) context tests. Evidence: $contextRoot"
