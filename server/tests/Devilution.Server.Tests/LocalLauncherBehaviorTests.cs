using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{
    [Fact]
    public void PlayerAnimationContextProofRejectsIncompleteOrReplacedReports()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var script = BuildPowerShellHarness(
            ["Assert-PlayerAnimationContextNativeReport", "Get-PlayerAnimationContextManagedCases", "Assert-PlayerAnimationContextManagedReport"],
            """
            function Require-Rejection([scriptblock]$action) {
                $rejected = $false; try { & $action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Incomplete or replaced context evidence was accepted.' }
            }
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-ContextReport-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                $path = Join-Path $work 'native.json'
                foreach ($captureOnly in @($true, $false)) {
                    $names = @('CaptureContextOracle')
                    if (-not $captureOnly) { $names += @('TownPreservesIncomingCombatFrames',
                        'DungeonReplacesAllFramesWithoutAdvancingCursorOrRng', 'SpellGraphicsUseRawMagicTypeIncludingNull') }
                    $valid = @{ tests = $names.Count; failures = 0; disabled = 0; errors = 0
                        testsuites = @(@{ testsuite = @(foreach ($name in $names) {
                            @{ name = $name; classname = 'NativePlayerAnimationContext'; status = 'RUN'; result = 'COMPLETED' }
                        }) }) } | ConvertTo-Json -Depth 6
                    [IO.File]::WriteAllText($path, $valid); Assert-PlayerAnimationContextNativeReport $path $captureOnly
                    foreach ($field in @('tests', 'failures', 'disabled', 'errors')) {
                        $changed = $valid | ConvertFrom-Json; $changed.$field = '0'
                        [IO.File]::WriteAllText($path, ($changed | ConvertTo-Json -Depth 6))
                        Require-Rejection { Assert-PlayerAnimationContextNativeReport $path $captureOnly }
                    }
                    foreach ($field in @('name', 'classname', 'status', 'result')) {
                        $changed = $valid | ConvertFrom-Json; $changed.testsuites[0].testsuite[0].$field = 'replaced'
                        [IO.File]::WriteAllText($path, ($changed | ConvertTo-Json -Depth 6))
                        Require-Rejection { Assert-PlayerAnimationContextNativeReport $path $captureOnly }
                    }
                }
                $path = Join-Path $work 'managed.xml'
                $required = @(Get-PlayerAnimationContextManagedCases); $count = $required.Count
                $tests = @(foreach ($case in $required) {
                    '<test type="' + $case.Type + '" method="' + $case.Method + '" name="' + $case.Name + '" result="Pass" />'
                }) -join ''
                $valid = '<assemblies><assembly total="' + $count + '" passed="' + $count +
                    '" failed="0" skipped="0" errors="0" not-run="0"><collection>' + $tests + '</collection></assembly></assemblies>'
                [IO.File]::WriteAllText($path, $valid)
                if ((Assert-PlayerAnimationContextManagedReport $path) -ne $count) { throw 'Wrong context count.' }
                foreach ($attribute in @('type', 'method', 'name', 'result')) {
                    [xml]$changed = $valid; $changed.SelectSingleNode('//test').SetAttribute($attribute, 'replaced')
                    $changed.Save($path); Require-Rejection { Assert-PlayerAnimationContextManagedReport $path }
                }
                foreach ($field in @('total', 'passed', 'failed', 'skipped', 'errors', 'not-run')) {
                    [xml]$changed = $valid; $changed.SelectSingleNode('//assembly').SetAttribute($field, '01')
                    $changed.Save($path); Require-Rejection { Assert-PlayerAnimationContextManagedReport $path }
                }
                foreach ($case in $required) {
                    [xml]$changed = $valid
                    $target = @($changed.SelectNodes('//test') | Where-Object method -ceq $case.Method)[0]
                    $target.ParentNode.RemoveChild($target) | Out-Null
                    $assembly = $changed.SelectSingleNode('//assembly')
                    $assembly.SetAttribute('total', [string]($count - 1)); $assembly.SetAttribute('passed', [string]($count - 1))
                    $changed.Save($path); Require-Rejection { Assert-PlayerAnimationContextManagedReport $path }
                }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work); $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                if ($resolved.StartsWith($temp + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-ContextReport-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-player-animation-context.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerAnimationContextProofRequiresBoundedRepeatableArtifacts()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var script = BuildPowerShellHarness(
            ["Get-PlayerAnimationContextArtifact", "Assert-PlayerAnimationContextRepeatability"],
            """
            $tokens = $null; $errors = $null
            $shared = [Management.Automation.Language.Parser]::ParseFile(
                (Join-Path (Split-Path $launcherPath -Parent) 'test-animation-timing.ps1'), [ref]$tokens, [ref]$errors)
            if ($errors.Count) { throw 'Shared path declaration did not parse.' }
            $definitions = @($shared.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -ceq 'Assert-AnimationTimingRegularPath'}, $true))
            if ($definitions.Count -ne 1) { throw 'Shared path ambiguity.' }
            . ([scriptblock]::Create($definitions[0].Extent.Text))
            function Require-Rejection([scriptblock]$action) {
                $rejected = $false; try { & $action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Incomplete context artifact was accepted.' }
            }
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-ContextArtifact-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                $path = Join-Path $work 'player-animation-context.json'
                [IO.File]::WriteAllText($path, 'literal artifact')
                $artifact = Get-PlayerAnimationContextArtifact $work
                Assert-PlayerAnimationContextRepeatability $artifact $artifact
                Require-Rejection { Assert-PlayerAnimationContextRepeatability $artifact $null }
                Require-Rejection { Assert-PlayerAnimationContextRepeatability @($artifact, $artifact) $artifact }
                foreach ($field in @('Name', 'Length', 'Sha256')) {
                    $changed = $artifact | Select-Object Name,Length,Sha256
                    $changed.$field = if ($field -eq 'Length') { 0 } else { 'bad' }
                    Require-Rejection { Assert-PlayerAnimationContextRepeatability $artifact $changed }
                }
                $changed = $artifact | Select-Object Name,Length,Sha256; $changed.Length++
                Require-Rejection { Assert-PlayerAnimationContextRepeatability $artifact $changed }
                [IO.File]::WriteAllText($path, '')
                Require-Rejection { Get-PlayerAnimationContextArtifact $work }
                [IO.File]::WriteAllText($path, [string]::new('x',1000001))
                Require-Rejection { Get-PlayerAnimationContextArtifact $work }
                [IO.File]::WriteAllText($path, 'literal artifact'); [IO.File]::WriteAllText((Join-Path $work 'extra'), 'extra')
                Require-Rejection { Get-PlayerAnimationContextArtifact $work }
                Require-Rejection { Get-PlayerAnimationContextArtifact 'relative' }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work); $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                if ($resolved.StartsWith($temp + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-ContextArtifact-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-player-animation-context.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void AnimationTimingOracleRequiresExactCompletedNativeFixtures()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-AnimationTimingNativeReport"], """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-AnimationNativeGuard-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                function Require-Rejection([scriptblock]$Action) {
                    $rejected = $false; try { & $Action } catch { $rejected = $true }
                    if (-not $rejected) { throw 'Incomplete animation evidence was accepted.' }
                }
                $path = Join-Path $work 'native.json'
                $names = @('CaptureTimingOracle', 'AttackSwordWarrior', 'AttackSwordWarriorWithFastestAttack',
                    'AttackSwordWarriorRepeated', 'BlockingWarriorNormal', 'BlockingSorcererWithFastBlock',
                    'HitRecoverySorcererHarmony', 'Stand')
                foreach ($captureOnly in @($false, $true)) {
                    $selected = if ($captureOnly) { @('CaptureTimingOracle') } else { $names }
                    $native = @{ tests = @($selected).Count; failures = 0; disabled = 0; errors = 0;
                        testsuites = @(@{ testsuite = @(foreach ($name in $selected) {
                            @{ classname = 'AnimationInfo'; name = $name; status = 'RUN'; result = 'COMPLETED' }
                        }) }) }
                    $valid = $native | ConvertTo-Json -Depth 8 -Compress
                    [IO.File]::WriteAllText($path, $valid); Assert-AnimationTimingNativeReport $path $captureOnly
                    foreach ($bad in @($valid.Replace('"COMPLETED"', '"SKIPPED"'),
                        $valid.Replace('"RUN"', '"NOTRUN"'), $valid.Replace('"errors":0', '"errors":1'),
                        $valid.Replace('"errors":0', '"errors":"0"'), $valid.Replace('"errors":0', '"errors":false'),
                        $valid.Replace('"errors":0', '"errors":0.0'),
                        $valid.Replace('"disabled":0', '"disabled":1'), $valid.Replace('"failures":0', '"failures":1'),
                        $valid.Replace('"disabled":0', '"disabled":null'), $valid.Replace('"AnimationInfo"', '"replacement"'),
                        $valid.Replace('"CaptureTimingOracle"', '"replacement"'))) {
                        [IO.File]::WriteAllText($path, $bad)
                        Require-Rejection { Assert-AnimationTimingNativeReport $path $captureOnly }
                    }
                    $native.testsuites[0].testsuite[0].failures = @(@{ failure = 'hidden native assertion' })
                    [IO.File]::WriteAllText($path, ($native | ConvertTo-Json -Depth 8 -Compress))
                    Require-Rejection { Assert-AnimationTimingNativeReport $path $captureOnly }
                }
                [IO.File]::WriteAllText($path, $valid.Replace('"tests":1', '"tests":0'))
                Require-Rejection { Assert-AnimationTimingNativeReport $path $true }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work)
                if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-AnimationNativeGuard-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-animation-timing.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void AnimationTimingOracleRequiresPortableFamilyAndExplicitComparison()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-AnimationTimingManagedReport"], """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-AnimationManagedGuard-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                function Require-Rejection([scriptblock]$Action) {
                    $rejected = $false; try { & $Action } catch { $rejected = $true }
                    if (-not $rejected) { throw 'Incomplete animation managed report was accepted.' }
                }
                $root = Split-Path (Split-Path $launcherPath -Parent) -Parent
                $source = [IO.File]::ReadAllText((Join-Path $root 'server/tests/Devilution.Domain.Tests/NativeAnimationStateTests.cs'))
                $methods = @([regex]::Matches($source, 'public void ([A-Za-z0-9_]+)\(') | ForEach-Object { $_.Groups[1].Value })
                if ($methods.Count -ne 20) { throw 'The frozen portable animation family has changed.' }
                $coreType = 'Devilution.Domain.Tests.NativeAnimationStateTests'
                $oracleType = 'Devilution.Domain.Tests.NativeAnimationTimingOracleTests'
                $nodes = @(foreach ($method in $methods) {
                    '<test type="' + $coreType + '" method="' + $method + '" name="' + $coreType + '.' + $method + '" result="Pass" />'
                })
                foreach ($method in @('MatchesCompleteNativeTimingCorpus', 'InputFingerprintBindsInputsButNotExpectedOutputs')) {
                    $nodes += '<test type="' + $oracleType + '" method="' + $method + '" name="' + $oracleType + '.' + $method + '" result="Pass" />'
                }
                foreach ($inputJson in @('{"Value":1}', '{"Refusal":0,"Value":1,"Extra":false}', '{"Refusal":0,"Value":1,"Value":2}')) {
                    $method = 'EvidenceShapeRefusesMissingExtraAndDuplicateFields'
                    $displayName = $oracleType + '.' + $method + '(json: \"' + $inputJson.Replace('"', '\\\"') + '\")'
                    $nodes += '<test type="' + $oracleType + '" method="' + $method + '" name="' +
                        [Security.SecurityElement]::Escape($displayName) + '" result="Pass" />'
                }
                $valid = '<assemblies><assembly total="25" passed="25" failed="0" skipped="0" errors="0" not-run="0"><collection>' +
                    ($nodes -join '') + '</collection></assembly></assemblies>'
                $path = Join-Path $work 'managed.xml'
                [IO.File]::WriteAllText($path, $valid)
                if ((Assert-AnimationTimingManagedReport $path) -ne 25) { throw 'Wrong accepted report count.' }
                foreach ($bad in @($valid.Replace('result="Pass"', 'result="Skip"'), $valid.Replace('not-run="0"', 'not-run="1"'),
                    $valid.Replace(' errors="0"', ''), $valid.Replace('total="25"', 'total="24"'),
                    $valid.Replace('passed="25"', 'passed="24"'), $valid.Replace($nodes[0], ''),
                    $valid.Replace('MatchesCompleteNativeTimingCorpus', 'replacement'),
                    $valid.Replace('DistributedProgressRetainsNativeStateWhenDelayIsNonpositive', 'replacement'),
                    $valid.Replace('Value', 'Replacement'),
                    $valid.Replace('</collection>', '</collection><errors><error /></errors>'),
                    $valid.Replace($nodes[0], $nodes[1]), $valid.Replace($nodes[24], ''),
                    $valid.Replace($oracleType, 'replacement'))) {
                    [IO.File]::WriteAllText($path, $bad)
                    Require-Rejection { Assert-AnimationTimingManagedReport $path }
                }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work)
                if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-AnimationManagedGuard-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-animation-timing.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void AnimationTimingOracleRequiresBoundedRegularRepeatableArtifacts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-AnimationTimingRegularPath", "Get-AnimationTimingArtifact", "Assert-AnimationTimingRepeatability"], """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-AnimationArtifactGuard-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $junction = Join-Path $work 'redirect'
            try {
                function Require-Rejection([scriptblock]$Action) {
                    $rejected = $false; try { & $Action } catch { $rejected = $true }
                    if (-not $rejected) { throw 'Invalid animation artifacts were accepted.' }
                }
                $capture = Join-Path $work 'capture'
                New-Item -ItemType Directory -Path $capture | Out-Null
                $path = Join-Path $capture 'animation-timing.json'
                [IO.File]::WriteAllText($path, 'corpus')
                $artifact = Get-AnimationTimingArtifact $capture
                Assert-AnimationTimingRepeatability $artifact $artifact
                Require-Rejection { Assert-AnimationTimingRepeatability $artifact $null }
                Require-Rejection { Assert-AnimationTimingRepeatability @($artifact, $artifact) $artifact }
                foreach ($property in @('Name', 'Length', 'Sha256')) {
                    $changed = $artifact.PSObject.Copy()
                    $changed.$property = switch ($property) { 'Name' { 'replacement' }; 'Length' { 10000001 }; 'Sha256' { 'b' * 64 } }
                    Require-Rejection { Assert-AnimationTimingRepeatability $artifact $changed }
                }
                $changed = $artifact.PSObject.Copy(); $changed.Sha256 = if ($artifact.Sha256 -ceq ('B' * 64)) { 'A' * 64 } else { 'B' * 64 }
                Require-Rejection { Assert-AnimationTimingRepeatability $artifact $changed }
                [IO.File]::WriteAllText((Join-Path $capture 'extra'), 'extra')
                Require-Rejection { Get-AnimationTimingArtifact $capture }
                Remove-Item -LiteralPath (Join-Path $capture 'extra')
                [IO.File]::WriteAllText($path, '')
                Require-Rejection { Get-AnimationTimingArtifact $capture }
                $stream = [IO.File]::OpenWrite($path); try { $stream.SetLength(10000001) } finally { $stream.Dispose() }
                Require-Rejection { Get-AnimationTimingArtifact $capture }
                [IO.File]::WriteAllText($path, 'corpus')
                New-Item -ItemType Junction -Path $junction -Target $work | Out-Null
                Require-Rejection { Get-AnimationTimingArtifact (Join-Path $junction 'capture') }
                Require-Rejection { Assert-AnimationTimingRegularPath 'relative' }
                Write-Output 'PASS'
            } finally {
                # Detach the owned junction without recursing through it before deleting the private root.
                if (Test-Path -LiteralPath $junction) { [IO.Directory]::Delete($junction) }
                $resolved = [IO.Path]::GetFullPath($work)
                if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-AnimationArtifactGuard-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-animation-timing.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteMetadataProofRejectsExcludedAndReplacedReports()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-CathedralMetadataManagedReport", "Get-CathedralMetadataCompilerReceipt"], """
            $work=Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-MetadataReportTest-'+[guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $path=Join-Path $work 'report.xml'
            function Expect-Rejected([scriptblock]$action) {
                $rejected=$false; try { & $action } catch { $rejected=$true }
                if (-not $rejected) { throw 'Invalid metadata proof report was accepted.' }
            }
            try {
                $receipt='Metadata: '+('A'*64)+'; manifest: '+('b'*64)
                $parsed=Get-CathedralMetadataCompilerReceipt ($receipt+"`r`n")
                if ($parsed.MetadataFingerprint -cne ('A'*64) -or $parsed.ManifestHash -cne ('b'*64)) { throw 'Receipt identity changed.' }
                foreach ($badReceipt in @('',($receipt+"`n"+$receipt),$receipt.Replace('b','g'),$receipt.Replace('manifest:','replacement:'))) {
                    Expect-Rejected { Get-CathedralMetadataCompilerReceipt $badReceipt }
                }
                $type='Devilution.Diablo.Tests.AuthoritativeCathedralFullPopulationNativeOracleTests'
                foreach ($method in @('ExportCompleteMetadataInputs','CompiledMetadataPopulationMatchesNative')) {
                    $names=@($type+'.'+$method)
                    if ($method -ceq 'CompiledMetadataPopulationMatchesNative') {
                        $names=@(foreach ($depth in 1..4) { foreach ($seed in @(2588,743271966)) { foreach ($entry in @('main','previous')) {
                            $type+'.'+$method+'(depth: '+$depth+', seed: '+$seed+', entry: \"'+$entry+'\")'
                        } } })
                    }
                    $nodes=@($names | ForEach-Object { '<test type="'+$type+'" method="'+$method+'" name="'+
                        [Security.SecurityElement]::Escape($_)+'" result="Pass" />' })
                    $valid='<assemblies><assembly total="'+$names.Count+'" passed="'+$names.Count+
                        '" failed="0" skipped="0" errors="0" not-run="0"><collection>'+($nodes -join '')+'</collection></assembly></assemblies>'
                    [IO.File]::WriteAllText($path,$valid); Assert-CathedralMetadataManagedReport $path $method
                    foreach ($bad in @(
                        $valid.Replace('result="Pass"','result="Skip"'), $valid.Replace('method="'+$method+'"','method="replacement"'),
                        $valid.Replace($nodes[0],''), $valid.Replace('errors="0"','errors="1"'),
                        $valid.Replace(' errors="0"',''), $valid.Replace('not-run="0"','not-run="1"'),
                        $valid.Replace('</collection>','</collection><errors><error /></errors>'))) {
                        [IO.File]::WriteAllText($path,$bad)
                        Expect-Rejected { Assert-CathedralMetadataManagedReport $path $method }
                    }
                    if ($names.Count -eq 16) {
                        [IO.File]::WriteAllText($path,$valid.Replace('depth: 4','depth: 3'))
                        Expect-Rejected { Assert-CathedralMetadataManagedReport $path $method }
                    }
                }
                Expect-Rejected { Assert-CathedralMetadataManagedReport $path 'replacement' }
                Write-Output 'PASS'
            } finally {
                $resolved=[IO.Path]::GetFullPath($work)
                if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-MetadataReportTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-cathedral-population-metadata.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteMetadataProofRejectsChangedAndIncompleteFileEvidence()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-CathedralMetadataRegularPath", "Get-CathedralMetadataFileHash", "Assert-CathedralMetadataFileRecord", "Get-CathedralMetadataPackFiles"], """
            $work=Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-MetadataFileTest-'+[guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            function Expect-Rejected([scriptblock]$action) {
                $rejected=$false; try { & $action } catch { $rejected=$true }
                if (-not $rejected) { throw 'Changed or incomplete metadata file evidence was accepted.' }
            }
            try {
                $file=Join-Path $work 'content-pack.json'; [IO.File]::WriteAllText($file,'abc',[Text.UTF8Encoding]::new($false))
                $hash='BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD'
                Assert-CathedralMetadataFileRecord $file 3 $hash
                Expect-Rejected { Assert-CathedralMetadataFileRecord $file 2 $hash }
                Expect-Rejected { Assert-CathedralMetadataFileRecord $file 3 ('A'*64) }
                Expect-Rejected { Get-CathedralMetadataPackFiles $work }
                foreach ($id in 1..19) { [IO.File]::WriteAllText((Join-Path $work ('table-'+$id+'.tsv')),'data') }
                if (@(Get-CathedralMetadataPackFiles $work).Count -ne 20) { throw 'Complete file receipt missing.' }
                [IO.File]::WriteAllText($file,'xyz',[Text.UTF8Encoding]::new($false))
                Expect-Rejected { Assert-CathedralMetadataFileRecord $file 3 $hash }
                [IO.File]::WriteAllText($file,'abc',[Text.UTF8Encoding]::new($false))
                if ((Get-CathedralMetadataFileHash $file) -cne 'BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD') {
                    throw 'Metadata hash did not match literal UTF-8 bytes.'
                }
                New-Item -ItemType Directory -Path (Join-Path $work 'unexpected') | Out-Null
                Expect-Rejected { Get-CathedralMetadataPackFiles $work }
                Write-Output 'PASS'
            } finally {
                $resolved=[IO.Path]::GetFullPath($work)
                if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-MetadataFileTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-cathedral-population-metadata.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void FullPopulationOracleRefusesExcludedReplacedAndIncompleteReports()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-CathedralFullNativeReport", "Assert-CathedralFullManagedReport"], """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-FullReportTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $nativePath = Join-Path $work 'native.json'; $managedPath = Join-Path $work 'managed.xml'
            function Expect-Rejected([scriptblock]$action) {
                $rejected=$false; try { & $action } catch { $rejected=$true }
                if (-not $rejected) { throw 'Incomplete or replaced full-population report was accepted.' }
            }
            try {
                $native = @{ tests=1; failures=0; disabled=0; errors=0; testsuites=@(@{ testsuite=@(@{
                    classname='ThemeRoomGenerationTest'; name='CaptureComposedOrdinaryPopulation'; status='RUN'; result='COMPLETED'
                }) }) } | ConvertTo-Json -Depth 6
                [IO.File]::WriteAllText($nativePath,$native); Assert-CathedralFullNativeReport $nativePath
                foreach ($mutate in @(
                    { param($r) $r.tests=0 }, { param($r) $r.errors=1 }, { param($r) $r.disabled=1 },
                    { param($r) $r.testsuites[0].testsuite[0].result='SKIPPED' },
                    { param($r) $r.testsuites[0].testsuite[0].status='NOTRUN' },
                    { param($r) $r.testsuites[0].testsuite[0].name='replacement' },
                    { param($r) $r.testsuites[0].testsuite[0].classname='replacement' },
                    { param($r) $r.testsuites[0].testsuite[0] | Add-Member failures @(@{ failure='error' }) })) {
                    $bad=$native | ConvertFrom-Json; & $mutate $bad
                    [IO.File]::WriteAllText($nativePath,($bad | ConvertTo-Json -Depth 6))
                    Expect-Rejected { Assert-CathedralFullNativeReport $nativePath }
                }
                $type='Devilution.Diablo.Tests.AuthoritativeCathedralFullPopulationNativeOracleTests'
                $nodes=@(foreach ($depth in 1..4) { foreach ($seed in @(2588,743271966)) { foreach ($entry in @('main','previous')) {
                    '<test type="' + $type + '" method="FullPopulationMatchesNative" name="' + $type +
                    '.FullPopulationMatchesNative(depth: ' + $depth + ', seed: ' + $seed + ', entry: \&quot;' + $entry + '\&quot;)" result="Pass" />'
                } } })
                $valid='<assemblies><assembly total="16" passed="16" failed="0" skipped="0" errors="0" not-run="0"><collection>' +
                    ($nodes -join '') + '</collection></assembly></assemblies>'
                [IO.File]::WriteAllText($managedPath,$valid); Assert-CathedralFullManagedReport $managedPath
                foreach ($bad in @(
                    $valid.Replace('result="Pass"','result="Skip"'),
                    $valid.Replace('method="FullPopulationMatchesNative"','method="replacement"'),
                    $valid.Replace('depth: 4','depth: 3'), $valid.Replace($nodes[0],''),
                    $valid.Replace('\&quot;','&quot;'),
                    $valid.Replace(' errors="0"',''), $valid.Replace('errors="0"','errors="1"'),
                    $valid.Replace('not-run="0"','not-run="1"'),
                    $valid.Replace('</collection>','</collection><errors><error type="fatal" /></errors>'))) {
                    [IO.File]::WriteAllText($managedPath,$bad)
                    Expect-Rejected { Assert-CathedralFullManagedReport $managedPath }
                }
                Write-Output 'PASS'
            } finally {
                $resolved=[IO.Path]::GetFullPath($work)
                if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-FullReportTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-cathedral-full-population.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void FullPopulationOracleRequiresByteIdenticalBoundedEvidence()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-CathedralFullRepeatability", "Assert-CathedralFullExternalPath"], """
            $first=@{ Name='native-full-population.json'; Length=10; Sha256=('A'*64) }
            Assert-CathedralFullRepeatability $first $first
            foreach ($second in @(
                $null, @($first,$first), @{ Name='replacement'; Length=10; Sha256=('A'*64) },
                @{ Name=$first.Name; Length=11; Sha256=('A'*64) },
                @{ Name=$first.Name; Length=10; Sha256=('B'*64) },
                @{ Name=$first.Name; Length=10; Sha256='A' })) {
                $rejected=$false; try { Assert-CathedralFullRepeatability $first $second } catch { $rejected=$true }
                if (-not $rejected) { throw 'Incomplete or changed full-population bytes were accepted.' }
            }
            $repository=Split-Path -Parent (Split-Path -Parent $launcherPath)
            foreach ($path in @($repository,(Join-Path $repository 'forbidden-evidence'))) {
                $rejected=$false; try { Assert-CathedralFullExternalPath $path $repository } catch { $rejected=$true }
                if (-not $rejected) { throw 'Repository-owned licensed evidence was accepted.' }
            }
            Write-Output 'PASS'
            """, "tools/test-cathedral-full-population.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeRoomOracleRejectsIncompleteSkippedAndReplacedReports()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-ThemeRoomNativeReport", "Assert-ThemeRoomManagedReport"], """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-ThemeReportTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $nativePath = Join-Path $work 'native.json'; $managedPath = Join-Path $work 'managed.xml'
            function Expect-Rejected([scriptblock]$action) {
                $rejected = $false; try { & $action } catch { $rejected = $true }
                if (-not $rejected) { throw 'Incomplete/replaced theme evidence was accepted.' }
            }
            try {
                $nativeNames = @(
                    'ThemeStaticLightingTest.RadiusFivePreservesNativeMinimumAndLoadingCopy',
                    'ThemeStaticLightingTest.CaptureZeroOffsetNativeStaticLighting',
                    'ThemeRoomGenerationTest.FullObjectPoolRetainsSlotsAndFailedShrineCarry',
                    'ThemeRoomGenerationTest.NoSkeletonBarrelRetainsMinusOneWithoutSelectionDraw',
                    'ThemeRoomGenerationTest.BlockedSkeletonWithNoNeighborRetainsReservationWithoutDraw',
                    'ThemeRoomGenerationTest.SignedSkeletonFallbackUsesNativeUnsignedSizeRemainder',
                    'ThemeRoomGenerationTest.BothMultiplayerOnlyShrineSelectionsRetryBeforeAnimationCoin',
                    'ThemeRoomGenerationTest.CaptureCompleteSelectedThemePass')
                $nativeCases = @($nativeNames | ForEach-Object {
                    $parts = $_.Split('.'); @{ classname=$parts[0]; name=$parts[1]; status='RUN'; result='COMPLETED' }
                })
                $native = @{ tests=8; failures=0; disabled=0; errors=0; testsuites=@(@{ testsuite=$nativeCases }) }
                $nativeJson = $native | ConvertTo-Json -Depth 6
                [IO.File]::WriteAllText($nativePath,$nativeJson); Assert-ThemeRoomNativeReport $nativePath
                foreach ($mutate in @(
                    { param($r) $r.tests = 7 }, { param($r) $r.failures = 1 },
                    { param($r) $r.testsuites[0].testsuite[0].status='NOTRUN' },
                    { param($r) $r.testsuites[0].testsuite[0].result='SKIPPED' },
                    { param($r) $r.testsuites[0].testsuite[0].name='replacement' },
                    { param($r) $r.testsuites[0].testsuite[0].classname='replacement' })) {
                    $bad = $nativeJson | ConvertFrom-Json; & $mutate $bad
                    [IO.File]::WriteAllText($nativePath,($bad | ConvertTo-Json -Depth 6))
                    Expect-Rejected { Assert-ThemeRoomNativeReport $nativePath }
                }
                $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralThemeRoomNativeOracleTests'
                $light = 'Devilution.Diablo.Tests.AuthoritativeCathedralThemeStaticLightNativeOracleTests'
                $nodes = @(1..4 | ForEach-Object {
                    '<test type="' + $type + '" method="CompleteOrderedThemeWorldMatchesNative" name="' +
                    $type + '.CompleteOrderedThemeWorldMatchesNative(depth: ' + $_ + ')" result="Pass" />'
                }) + @('<test type="' + $light + '" method="CompleteLightChannelsMatchNative" name="' +
                    $light + '.CompleteLightChannelsMatchNative" result="Pass" />')
                $validXml = '<assemblies><assembly total="5" passed="5" failed="0" skipped="0" errors="0" not-run="0"><collection>' +
                    ($nodes -join '') + '</collection></assembly></assemblies>'
                [IO.File]::WriteAllText($managedPath,$validXml); Assert-ThemeRoomManagedReport $managedPath
                foreach ($badXml in @(
                    $validXml.Replace('result="Pass"','result="Skip"'),
                    $validXml.Replace('depth: 4','depth: 3'),
                    $validXml.Replace('method="CompleteOrderedThemeWorldMatchesNative"','method="replacement"'),
                    $validXml.Replace('failed="0"','failed="1"'),
                    $validXml.Replace('errors="0"','errors="1"'),
                    $validXml.Replace(' errors="0"',''),
                    $validXml.Replace('not-run="0"','not-run="1"'),
                    $validXml.Replace('</collection>','</collection><errors><error type="fatal" /></errors>'),
                    $validXml.Replace($nodes[4],''))) {
                    [IO.File]::WriteAllText($managedPath,$badXml)
                    Expect-Rejected { Assert-ThemeRoomManagedReport $managedPath }
                }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work)
                if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
                    [IO.Path]::GetFileName($resolved) -match '^Godevilution-ThemeReportTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-theme-room-generation.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeRoomOracleRepeatabilityRequiresBothUnchangedArtifacts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Assert-ThemeRoomRepeatability"], """
            $first = @(@{ Name='native-theme-rooms.json'; Length=10; Sha256='A' },
                @{ Name='native-theme-static-light.json'; Length=20; Sha256='B' })
            Assert-ThemeRoomRepeatability $first $first
            foreach ($second in @(
                @($first[0]),
                @(@{ Name='replacement'; Length=10; Sha256='A' },$first[1]),
                @(@{ Name='native-theme-rooms.json'; Length=11; Sha256='A' },$first[1]),
                @($first[0],@{ Name='native-theme-static-light.json'; Length=20; Sha256='changed' }))) {
                $rejected=$false; try { Assert-ThemeRoomRepeatability $first $second } catch { $rejected=$true }
                if (-not $rejected) { throw 'Incomplete or changed native theme capture was accepted.' }
            }
            Write-Output 'PASS'
            """, "tools/test-theme-room-generation.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void FreshPopulationOracleGuardsRequireExactNativeMatrixAndRepeatedCapture()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(
            ["Assert-CathedralFreshNativeReport", "Assert-CathedralFreshManagedReport", "Assert-CathedralFreshRepeatability"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-FreshGuard-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                function Require-Rejection([scriptblock]$Action) {
                    $rejected = $false
                    try { & $Action } catch { $rejected = $true }
                    if (-not $rejected) { throw 'Unsafe fresh proof was accepted.' }
                }
                $nativePath = Join-Path $work 'native.json'
                $native = @{ tests = 1; failures = 0; disabled = 0; errors = 0;
                    testsuites = @(@{ testsuite = @(@{ classname = 'ThemeRoomGenerationTest';
                        name = 'CaptureFreshOrdinaryPopulation'; status = 'RUN'; result = 'COMPLETED' }) }) }
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Assert-CathedralFreshNativeReport $nativePath
                $native.testsuites[0].testsuite[0].name = 'CaptureComposedOrdinaryPopulation'
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Require-Rejection { Assert-CathedralFreshNativeReport $nativePath }
                $native.testsuites[0].testsuite[0].name = 'CaptureFreshOrdinaryPopulation'
                $native.testsuites[0].testsuite[0].result = 'SKIPPED'
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Require-Rejection { Assert-CathedralFreshNativeReport $nativePath }
                $managedPath = Join-Path $work 'managed.xml'
                $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralFreshLevelNativeOracleTests'
                $tests = @(foreach ($depth in 1..4) { foreach ($seed in @(2588, 743271966)) { foreach ($entry in @('main', 'previous')) {
                    $name = $type + '.FreshLevelMatchesNative(depth: ' + $depth + ', seed: ' + $seed + ', entry: \"' + $entry + '\")'
                    '<test name="' + [Security.SecurityElement]::Escape($name) + '" type="' + $type + '" method="FreshLevelMatchesNative" result="Pass" />'
                } } })
                $xml = '<assemblies><assembly total="16" passed="16" failed="0" skipped="0" errors="0" not-run="0"><collection>' + ($tests -join '') + '</collection></assembly></assemblies>'
                [IO.File]::WriteAllText($managedPath, $xml)
                Assert-CathedralFreshManagedReport $managedPath
                [IO.File]::WriteAllText($managedPath, $xml.Replace('seed: 2588', 'seed: 7'))
                Require-Rejection { Assert-CathedralFreshManagedReport $managedPath }
                [IO.File]::WriteAllText($managedPath, $xml.Replace('result="Pass"', 'result="Skip"'))
                Require-Rejection { Assert-CathedralFreshManagedReport $managedPath }
                [IO.File]::WriteAllText($managedPath, $xml.Replace('not-run="0"', 'not-run="1"'))
                Require-Rejection { Assert-CathedralFreshManagedReport $managedPath }
                $factoryType = 'Devilution.Diablo.Tests.AuthoritativeCathedralFreshLevelGeneratorTests'
                $factoryTests = @(foreach ($method in @('GeneratedDepthCarriesNativeNextTransparencyValue',
                    'CreateRequestRetainsIndependentDungeonAndStorySeeds', 'InvalidHeroBoundsAreRejectedBeforeFreshGeneration',
                    'HeroEntryContextIsExplicitAndBoundedWithoutOriginalAssets', 'MaterializersPreserveCounterWithoutAliasingCallerLayers')) {
                    '<test name="' + $factoryType + '.' + $method + '" type="' + $factoryType + '" method="' + $method + '" result="Pass" />'
                })
                $generation = 'FreshGenerationDerivesGeometryTriggersPoolsAndPlayerLight'
                foreach ($binding in @(@(1, 2588, 'main'), @(2, 743271966, 'previous'), @(3, 2147483648, 'main'), @(4, 0, 'previous'))) {
                    $name = $factoryType + '.' + $generation + '(depth: ' + $binding[0] + ', seed: ' + $binding[1] + ', entry: \"' + $binding[2] + '\")'
                    $factoryTests += '<test name="' + [Security.SecurityElement]::Escape($name) + '" type="' + $factoryType + '" method="' + $generation + '" result="Pass" />'
                }
                $factoryPrefix = '<assemblies><assembly total="9" passed="9" failed="0" skipped="0" errors="0" not-run="0"><collection>'
                $factorySuffix = '</collection></assembly></assemblies>'
                $factoryXml = $factoryPrefix + ($factoryTests -join '') + $factorySuffix
                [IO.File]::WriteAllText($managedPath, $factoryXml)
                Assert-CathedralFreshManagedReport $managedPath -Factory
                [IO.File]::WriteAllText($managedPath, $factoryXml.Replace('seed: 2147483648', 'seed: 80000000'))
                Require-Rejection { Assert-CathedralFreshManagedReport $managedPath -Factory }
                [IO.File]::WriteAllText($managedPath, $factoryPrefix.Replace('total="9" passed="9"', 'total="8" passed="8"') + ($factoryTests[0..7] -join '') + $factorySuffix)
                Require-Rejection { Assert-CathedralFreshManagedReport $managedPath -Factory }
                $artifact = [pscustomobject]@{ Name = 'native-fresh-population.json'; Length = 123; Sha256 = ('A' * 64) }
                Assert-CathedralFreshRepeatability $artifact $artifact
                Require-Rejection { Assert-CathedralFreshRepeatability $artifact ([pscustomobject]@{ Name = $artifact.Name; Length = 123; Sha256 = ('B' * 64) }) }
                Require-Rejection { Assert-CathedralFreshRepeatability $artifact ([pscustomobject]@{ Name = $artifact.Name; Length = 0; Sha256 = $artifact.Sha256 }) }
                Require-Rejection { Assert-CathedralFreshRepeatability $artifact ([pscustomobject]@{ Name = 'native-full-population.json'; Length = 123; Sha256 = $artifact.Sha256 }) }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work)
                if ((Split-Path $resolved -Parent) -eq [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -and
                    (Split-Path $resolved -Leaf) -match '^Godevilution-FreshGuard-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-cathedral-fresh-population.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }
    [Fact]
    public void MonsterRuntimeOracleGuardsRequireExactMethodAndTwoArtifacts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(
            ["Assert-CathedralRuntimeNativeReport", "Assert-CathedralRuntimeManagedReport", "Get-CathedralRuntimeArtifacts", "Assert-CathedralRuntimeRepeatability"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-MonsterRuntimeGuard-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                function Require-Rejection([scriptblock]$Action) {
                    $rejected = $false
                    try { & $Action } catch { $rejected = $true }
                    if (-not $rejected) { throw 'Unsafe fresh proof was accepted.' }
                }
                $nativePath = Join-Path $work 'native.json'
                $native = @{ tests = 1; failures = 0; disabled = 0; errors = 0;
                    testsuites = @(@{ testsuite = @(@{ classname = 'ThemeRoomGenerationTest';
                        name = 'CaptureMonsterRuntimeMetadata'; status = 'RUN'; result = 'COMPLETED' }) }) }
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Assert-CathedralRuntimeNativeReport $nativePath
                $native.testsuites[0].testsuite[0].name = 'CaptureComposedOrdinaryPopulation'
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Require-Rejection { Assert-CathedralRuntimeNativeReport $nativePath }
                $native.testsuites[0].testsuite[0].name = 'CaptureMonsterRuntimeMetadata'
                $native.testsuites[0].testsuite[0].result = 'SKIPPED'
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Require-Rejection { Assert-CathedralRuntimeNativeReport $nativePath }
                $managedPath = Join-Path $work 'managed.xml'
                $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralMonsterRuntimeNativeOracleTests'
                $method = 'CompleteRuntimeMetadataMatchesNativeMonsterMethods'
                $name = $type + '.' + $method
                $xml = '<assemblies><assembly total="1" passed="1" failed="0" skipped="0" errors="0" not-run="0"><collection><test name="' +
                    $name + '" type="' + $type + '" method="' + $method + '" result="Pass" /></collection></assembly></assemblies>'
                [IO.File]::WriteAllText($managedPath, $xml)
                Assert-CathedralRuntimeManagedReport $managedPath
                [IO.File]::WriteAllText($managedPath, $xml.Replace($method, 'replacement'))
                Require-Rejection { Assert-CathedralRuntimeManagedReport $managedPath }
                [IO.File]::WriteAllText($managedPath, $xml.Replace('result="Pass"', 'result="Skip"'))
                Require-Rejection { Assert-CathedralRuntimeManagedReport $managedPath }
                [IO.File]::WriteAllText($managedPath, $xml.Replace('not-run="0"', 'not-run="1"'))
                Require-Rejection { Assert-CathedralRuntimeManagedReport $managedPath }
                $artifactDirectory = Join-Path $work 'capture'
                New-Item -ItemType Directory -Path $artifactDirectory | Out-Null
                $corpusPath = Join-Path $artifactDirectory 'native-monster-runtime.json'
                $tablePath = Join-Path $artifactDirectory 'cathedral_population_monster_runtime.tsv'
                [IO.File]::WriteAllText($corpusPath, 'corpus')
                [IO.File]::WriteAllText($tablePath, 'table')
                $artifacts = @(Get-CathedralRuntimeArtifacts $artifactDirectory)
                Assert-CathedralRuntimeRepeatability $artifacts $artifacts
                Require-Rejection { Assert-CathedralRuntimeRepeatability $artifacts @($artifacts[0]) }
                $mutated = @($artifacts[0].PSObject.Copy(), $artifacts[1].PSObject.Copy())
                $mutated[0].Sha256 = ('B' * 64)
                Require-Rejection { Assert-CathedralRuntimeRepeatability $artifacts $mutated }
                $extra = Join-Path $artifactDirectory 'extra'
                [IO.File]::WriteAllText($extra, 'extra')
                Require-Rejection { Get-CathedralRuntimeArtifacts $artifactDirectory }
                Remove-Item -LiteralPath $extra -Force
                [IO.File]::WriteAllText($tablePath, '')
                Require-Rejection { Get-CathedralRuntimeArtifacts $artifactDirectory }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work)
                if ((Split-Path $resolved -Parent) -eq [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -and
                    (Split-Path $resolved -Leaf) -match '^Godevilution-MonsterRuntimeGuard-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-cathedral-monster-runtime.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }


    [Fact]
    public void LevelReadyOracleGuardsRequireExactMatrixAndTwoArtifacts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(
            ["Assert-CathedralReadyNativeReport", "Assert-CathedralReadyManagedReport", "Get-CathedralReadyArtifacts", "Assert-CathedralReadyRepeatability"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-LevelReadyGuard-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            try {
                function Require-Rejection([scriptblock]$Action) {
                    $rejected = $false
                    try { & $Action } catch { $rejected = $true }
                    if (-not $rejected) { throw 'Unsafe fresh proof was accepted.' }
                }
                $nativePath = Join-Path $work 'native.json'
                $native = @{ tests = 1; failures = 0; disabled = 0; errors = 0;
                    testsuites = @(@{ testsuite = @(@{ classname = 'ThemeRoomGenerationTest';
                        name = 'CaptureFreshLevelReady'; status = 'RUN'; result = 'COMPLETED' }) }) }
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Assert-CathedralReadyNativeReport $nativePath
                $native.testsuites[0].testsuite[0].name = 'CaptureComposedOrdinaryPopulation'
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Require-Rejection { Assert-CathedralReadyNativeReport $nativePath }
                $native.testsuites[0].testsuite[0].name = 'CaptureFreshLevelReady'
                $native.testsuites[0].testsuite[0].result = 'SKIPPED'
                [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
                Require-Rejection { Assert-CathedralReadyNativeReport $nativePath }
                $managedPath = Join-Path $work 'managed.xml'
                $type = 'Devilution.Diablo.Tests.AuthoritativeCathedralLevelReadyNativeOracleTests'
                $tests = @(foreach ($depth in 1..4) { foreach ($seed in @(2588, 743271966)) { foreach ($entry in @('main', 'previous')) {
                    $name = $type + '.LevelReadyMatchesNative(depth: ' + $depth + ', seed: ' + $seed + ', entry: \"' + $entry + '\")'
                    '<test name="' + [Security.SecurityElement]::Escape($name) + '" type="' + $type + '" method="LevelReadyMatchesNative" result="Pass" />'
                } } })
                $xml = '<assemblies><assembly total="16" passed="16" failed="0" skipped="0" errors="0" not-run="0"><collection>' + ($tests -join '') + '</collection></assembly></assemblies>'
                [IO.File]::WriteAllText($managedPath, $xml)
                Assert-CathedralReadyManagedReport $managedPath
                [IO.File]::WriteAllText($managedPath, $xml.Replace('seed: 2588', 'seed: 7'))
                Require-Rejection { Assert-CathedralReadyManagedReport $managedPath }
                [IO.File]::WriteAllText($managedPath, $xml.Replace('result="Pass"', 'result="Skip"'))
                Require-Rejection { Assert-CathedralReadyManagedReport $managedPath }
                [IO.File]::WriteAllText($managedPath, $xml.Replace('not-run="0"', 'not-run="1"'))
                Require-Rejection { Assert-CathedralReadyManagedReport $managedPath }
                $artifactDirectory = Join-Path $work 'capture'
                New-Item -ItemType Directory -Path $artifactDirectory | Out-Null
                $corpusPath = Join-Path $artifactDirectory 'native-level-ready.json'
                $tablePath = Join-Path $artifactDirectory 'cathedral_automap.tsv'
                [IO.File]::WriteAllText($corpusPath, 'corpus')
                [IO.File]::WriteAllText($tablePath, 'table')
                $artifacts = @(Get-CathedralReadyArtifacts $artifactDirectory)
                Assert-CathedralReadyRepeatability $artifacts $artifacts
                Require-Rejection { Assert-CathedralReadyRepeatability $artifacts @($artifacts[0]) }
                $mutated = @($artifacts[0].PSObject.Copy(), $artifacts[1].PSObject.Copy())
                $mutated[0].Sha256 = ('B' * 64)
                Require-Rejection { Assert-CathedralReadyRepeatability $artifacts $mutated }
                $extra = Join-Path $artifactDirectory 'extra'
                [IO.File]::WriteAllText($extra, 'extra')
                Require-Rejection { Get-CathedralReadyArtifacts $artifactDirectory }
                Remove-Item -LiteralPath $extra -Force
                [IO.File]::WriteAllText($tablePath, '')
                Require-Rejection { Get-CathedralReadyArtifacts $artifactDirectory }
                Write-Output 'PASS'
            } finally {
                $resolved = [IO.Path]::GetFullPath($work)
                if ((Split-Path $resolved -Parent) -eq [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -and
                    (Split-Path $resolved -Leaf) -match '^Godevilution-LevelReadyGuard-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolved -Recurse -Force
                }
            }
            """, "tools/test-cathedral-level-ready.ps1");
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }
}
