<# Sprite-free native AnimationInfo timing qualification. No MPQ, server, client or Godot process. #>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$animationRepository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
foreach ($animationImport in @(
    @{ File = 'test-cathedral-geometry.ps1'; Names = @('Find-CathedralOracleBuild', 'Quote-CathedralNativeArgument',
        'Restore-CathedralOracleEnvironmentVariable', 'Add-CathedralPopulationCleanupError',
        'Stop-CathedralPopulationOwnedProcessTree', 'Invoke-CathedralPopulationOwnedProcess') },
    @{ File = 'test-theme-item-generation.ps1'; Names = @('Assert-ThemeOwnedStage') })) {
    $animationTokens = $null; $animationErrors = $null
    $animationAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $animationImport.File), [ref]$animationTokens, [ref]$animationErrors)
    if ($animationErrors.Count) { throw 'Shared animation proof declarations did not parse.' }
    foreach ($animationName in $animationImport.Names) {
        $animationDeclarations = @($animationAst.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $animationName
        }, $true))
        if ($animationDeclarations.Count -ne 1) { throw "Expected exactly one declaration for $animationName." }
        . ([scriptblock]::Create($animationDeclarations[0].Extent.Text))
    }
}

function Assert-AnimationTimingRegularPath([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path)) { throw 'Animation evidence requires an absolute path.' }
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $entry = Get-Item -LiteralPath $cursor -Force
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Animation evidence may not traverse reparse points.'
            }
        }
        $parent = [IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
}

function Assert-AnimationTimingNativeReport([string]$Path, [bool]$CaptureOnly) {
    $report = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $names = @('CaptureTimingOracle')
    if (-not $CaptureOnly) {
        $names += @('AttackSwordWarrior', 'AttackSwordWarriorWithFastestAttack', 'AttackSwordWarriorRepeated',
            'BlockingWarriorNormal', 'BlockingSorcererWithFastBlock', 'HitRecoverySorcererHarmony', 'Stand')
    }
    foreach ($field in @('tests', 'failures', 'disabled', 'errors')) {
        if ($report.PSObject.Properties.Name -cnotcontains $field -or
            ($report.$field -isnot [int] -and $report.$field -isnot [long])) {
            throw "Animation native report is missing $field."
        }
    }
    $cases = @($report.testsuites | ForEach-Object { $_.testsuite })
    if ($report.tests -ne $names.Count -or $report.failures -ne 0 -or $report.disabled -ne 0 -or $report.errors -ne 0 -or
        $cases.Count -ne $names.Count -or @($cases | Where-Object {
            $_.classname -cne 'AnimationInfo' -or $_.status -cne 'RUN' -or $_.result -cne 'COMPLETED' -or
            @($_.failures).Where({ $null -ne $_ }).Count -ne 0
        }).Count -ne 0 -or @(Compare-Object $names @($cases.name) -CaseSensitive).Count -ne 0) {
        throw 'The exact native animation fixtures must complete without exclusions.'
    }
}

function Assert-AnimationTimingManagedReport([string]$Path) {
    [xml]$report = [IO.File]::ReadAllText($Path)
    $assemblies = @($report.SelectNodes('//assembly')); $cases = @($report.SelectNodes('//test'))
    if ($assemblies.Count -ne 1 -or $cases.Count -eq 0) { throw 'One nonempty managed animation report is required.' }
    foreach ($field in @('total', 'passed', 'failed', 'skipped', 'errors', 'not-run')) {
        if (-not $assemblies[0].HasAttribute($field) -or $assemblies[0].GetAttribute($field) -cnotmatch '^(0|[1-9][0-9]*)$') {
            throw "Animation managed report is missing a valid $field count."
        }
    }
    if ($assemblies[0].total -ne $cases.Count -or $assemblies[0].passed -ne $cases.Count -or
        $assemblies[0].failed -ne 0 -or $assemblies[0].skipped -ne 0 -or $assemblies[0].errors -ne 0 -or
        $assemblies[0].'not-run' -ne 0 -or @($report.SelectNodes('//errors/error')).Count -ne 0 -or
        @($cases | Where-Object { $_.result -cne 'Pass' -or [string]::IsNullOrWhiteSpace($_.name) }).Count -ne 0 -or
        @($cases.name | Sort-Object -Unique -CaseSensitive).Count -ne $cases.Count) {
        throw 'Every reported managed test must pass without exclusions or duplicate evidence.'
    }
    $coreType = 'Devilution.Domain.Tests.NativeAnimationStateTests'
    $coreMethods = @('ProcessPendingAttackUsesNativeDistributionFields', 'FastestAttackRetainsSignedSkipAndLiteralModifier',
        'SignedWalkSkipsAreRenderedWithoutUnsignedNormalization', 'LastFrameDelayAndFastBlockUseNativeLiteralProgression',
        'RepeatedAnimationCarriesTheTruncatedPreviousFrames', 'CompletedDistributedAnimationReturnsItsNativeCurrentFrame',
        'ChangeAnimationDataClampsFrameAndClearsDistributionButPreservesPetrification',
        'ChangeAnimationDataPreservesNativeNonpositiveDelayInsteadOfClamping',
        'DistributedProgressRetainsNativeStateWhenDelayIsNonpositive',
        'NegativeFrameCountUsesNativeNonzeroDivisorAndUnsignedProgressWrap',
        'EmptyNativeCursorUsesTheDistributionAbsentRenderFallback', 'ChangedAnimationDataPreservesThePrivatePreviousAnimationCarry',
        'SetNewAnimationClampsNonpositiveDelayAndPreservesPreviewFraction', 'ForwardAndReverseProcessingWrapAtNativeFrameBoundaries',
        'ReverseFrameWrapNarrowsNativeNegativeFrameCountResult',
        'PetrificationSuppressesOnlyRenderFractionAndDoesNotFreezeGameTickState',
        'ClonePreservesEveryDistributionScalarAndContinuation', 'ActorsKeepIndependentAnimationCursorsAtTheFractionBoundary',
        'UnchangedAnimationDataRetainsTheEntireNativeCursor', 'NativeWidthsFlagsAndProgressAreFailClosed')
    $coreCases = @($cases | Where-Object type -ceq $coreType)
    if ($coreCases.Count -ne $coreMethods.Count) { throw 'The complete portable native animation family is required.' }
    foreach ($method in $coreMethods) {
        $matches = @($coreCases | Where-Object { $_.method -ceq $method -and $_.name -ceq ($coreType + '.' + $method) })
        if ($matches.Count -ne 1) { throw "Missing or replaced portable animation test $method." }
    }
    $oracleType = 'Devilution.Domain.Tests.NativeAnimationTimingOracleTests'
    $oracleCases = @($cases | Where-Object type -ceq $oracleType)
    if ($oracleCases.Count -ne 5) { throw 'Native comparison and all four evidence guards must pass.' }
    foreach ($method in @('MatchesCompleteNativeTimingCorpus', 'InputFingerprintBindsInputsButNotExpectedOutputs')) {
        $matches = @($oracleCases | Where-Object { $_.method -ceq $method -and $_.name -ceq ($oracleType + '.' + $method) })
        if ($matches.Count -ne 1) { throw "The exact native animation comparison/guard $method must pass." }
    }
    $shapeInputs = @('{"Value":1}', '{"Refusal":0,"Value":1,"Extra":false}', '{"Refusal":0,"Value":1,"Value":2}')
    $shapeNames = @(foreach ($inputJson in $shapeInputs) {
        $oracleType + '.EvidenceShapeRefusesMissingExtraAndDuplicateFields(json: \"' + $inputJson.Replace('"', '\\\"') + '\")'
    })
    $shapeCases = @($oracleCases | Where-Object method -ceq 'EvidenceShapeRefusesMissingExtraAndDuplicateFields')
    if ($shapeCases.Count -ne 3 -or @(Compare-Object $shapeNames @($shapeCases.name) -CaseSensitive).Count -ne 0) {
        throw 'All three animation evidence-shape cases must pass.'
    }
    return $cases.Count
}

function Get-AnimationTimingArtifact([string]$Directory) {
    Assert-AnimationTimingRegularPath $Directory
    $root = Get-Item -LiteralPath $Directory -Force
    $entries = @(Get-ChildItem -LiteralPath $Directory -Force)
    if (-not $root.PSIsContainer -or $entries.Count -ne 1 -or $entries[0].Name -cne 'animation-timing.json' -or
        $entries[0].PSIsContainer -or ($entries[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entries[0].Length -le 0 -or $entries[0].Length -gt 10000000) {
        throw 'Animation capture requires exactly one bounded nonempty regular animation-timing.json.'
    }
    $stream = $null; $sha = $null
    try {
        $stream = [IO.File]::OpenRead($entries[0].FullName)
        if ($stream.Length -ne $entries[0].Length -or $stream.Length -le 0 -or $stream.Length -gt 10000000) {
            throw 'Animation artifact size changed before hashing.'
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        [pscustomobject]@{ Name = $entries[0].Name; Length = $stream.Length
            Sha256 = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    } finally {
        if ($null -ne $stream) { $stream.Dispose() }
        if ($null -ne $sha) { $sha.Dispose() }
    }
}

function Assert-AnimationTimingRepeatability($First, $Second) {
    foreach ($artifact in @($First, $Second)) {
        if (@($artifact).Count -ne 1 -or $artifact.Name -cne 'animation-timing.json' -or
            $artifact.Length -le 0 -or $artifact.Length -gt 10000000 -or $artifact.Sha256 -cnotmatch '^[0-9A-F]{64}$') {
            throw 'Two complete bounded animation captures are required.'
        }
    }
    if ($First.Length -ne $Second.Length -or $First.Sha256 -cne $Second.Sha256) {
        throw 'Both independent native timing captures must be byte-identical.'
    }
}

function Add-AnimationTimingStage($Record, $Outcome, [string]$Name) {
    $Record.Stages += $Outcome | Select-Object @{ N = 'Stage'; E = { $Name } }, Pid, ExitCode, TimedOut,
        ConfirmedExited, LogRetained, Error, CleanupError
    Assert-ThemeOwnedStage $Outcome $Name
}

$animationBuild = Find-CathedralOracleBuild $animationRepository
$animationRoot = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-Animation-Timing-Oracle-' + [guid]::NewGuid().ToString('N'))
Assert-AnimationTimingRegularPath $animationRoot
New-Item -ItemType Directory -Path $animationRoot | Out-Null
$animationRecord = [ordered]@{ Passed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); EndedUtc = $null
    Scope = 'sprite-free-AnimationInfo-core-not-player-controller-or-Godot-action-timing'
    NativeBuild = $animationBuild.Directory; ManagedTests = $null
    Stages = @(); Artifacts = @(); Error = $null; EvidenceError = $null }
$animationVariable = 'GODEVILUTION_ANIMATION_TIMING_ORACLE_DIRECTORY'
$animationPrevious = [Environment]::GetEnvironmentVariable($animationVariable, 'Process')
$animationPrimaryError = $null
Push-Location $animationRepository
try {
    Write-Host "Private animation timing evidence: $animationRoot"
    $buildCommand = 'call "{0}" -arch=x64 && where cl.exe && where rc.exe && where mt.exe && "{1}" --build "{2}" --config Debug --target animationinfo_test --parallel 2' -f
        $animationBuild.DeveloperCommand, $animationBuild.CMake, $animationBuild.Directory
    $outcome = Invoke-CathedralPopulationOwnedProcess $env:ComSpec ('/d /c "' + $buildCommand + '"') (Join-Path $animationRoot 'native-build.log') 300000
    Add-AnimationTimingStage $animationRecord $outcome 'native-build'
    foreach ($capture in 1..2) {
        $captureDirectory = Join-Path $animationRoot "capture-$capture"
        New-Item -ItemType Directory -Path $captureDirectory | Out-Null
        [Environment]::SetEnvironmentVariable($animationVariable, $captureDirectory, 'Process')
        $report = Join-Path $animationRoot "native-tests-$capture.json"
        $filter = if ($capture -eq 1) { 'AnimationInfo.*' } else { 'AnimationInfo.CaptureTimingOracle' }
        $arguments = '--gtest_filter=' + $filter + ' --gtest_output=json:' + (Quote-CathedralNativeArgument $report)
        $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $animationBuild.Directory 'animationinfo_test.exe') $arguments (Join-Path $animationRoot "native-tests-$capture.log") 120000
        Add-AnimationTimingStage $animationRecord $outcome "native-capture-$capture"
        Assert-AnimationTimingNativeReport $report ($capture -eq 2)
        $captureArtifact = Get-AnimationTimingArtifact $captureDirectory
        if ($capture -eq 2) { Assert-AnimationTimingRepeatability $animationRecord.Artifacts[0] $captureArtifact }
        $animationRecord.Artifacts += $captureArtifact
    }
    $outcome = Invoke-CathedralPopulationOwnedProcess (Get-Command dotnet -CommandType Application).Source `
        'build server/tests/Devilution.Domain.Tests/Devilution.Domain.Tests.csproj --configuration Release --no-restore --disable-build-servers -v minimal' (Join-Path $animationRoot 'managed-build.log') 180000
    Add-AnimationTimingStage $animationRecord $outcome 'managed-build'
    [Environment]::SetEnvironmentVariable($animationVariable, (Join-Path $animationRoot 'capture-1'), 'Process')
    $arguments = '--no-ansi --parallel none --explicit on --fail-skips on --report-xunit --results-directory ' +
        (Quote-CathedralNativeArgument $animationRoot) + ' --report-xunit-filename domain.xml'
    $outcome = Invoke-CathedralPopulationOwnedProcess (Join-Path $animationRepository 'server/tests/Devilution.Domain.Tests/bin/Release/net8.0/Devilution.Domain.Tests.exe') `
        $arguments (Join-Path $animationRoot 'domain.log') 180000
    Add-AnimationTimingStage $animationRecord $outcome 'managed-domain'
    $animationRecord.ManagedTests = Assert-AnimationTimingManagedReport (Join-Path $animationRoot 'domain.xml')
    foreach ($capture in 1..2) {
        Assert-AnimationTimingRepeatability $animationRecord.Artifacts[$capture - 1] (Get-AnimationTimingArtifact (Join-Path $animationRoot "capture-$capture"))
    }
    $animationRecord.Passed = $true
} catch { $animationPrimaryError = $_; $animationRecord.Error = $_.Exception.Message }
finally {
    Restore-CathedralOracleEnvironmentVariable $animationVariable $animationPrevious
    Pop-Location; $animationRecord.EndedUtc = [DateTime]::UtcNow.ToString('O')
    try {
        $json = (($animationRecord | ConvertTo-Json -Depth 5) -replace '\r?\n', "`r`n") + "`r`n"
        [IO.File]::WriteAllText((Join-Path $animationRoot 'result.json'), $json, [Text.UTF8Encoding]::new($false))
    } catch { $animationRecord.EvidenceError = $_.Exception.Message; if ($null -eq $animationPrimaryError) { $animationPrimaryError = $_ } }
}
if ($null -ne $animationPrimaryError) { throw $animationPrimaryError }
Write-Host "PASS: 50 native traces, all nine cursor scalars and 129 render/progress fractions per transition; $($animationRecord.ManagedTests) Domain tests. Evidence: $animationRoot"
