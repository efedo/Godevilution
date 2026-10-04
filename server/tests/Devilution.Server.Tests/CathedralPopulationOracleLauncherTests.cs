using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class CathedralPopulationOracleLauncherTests
{
    public static bool HasPowerShellCore => OperatingSystem.IsWindows() && FindPowerShellCore() is not null;

    [Fact]
    public void RestoresAbsentEmptyAndPopulatedEnvironmentInWindowsPowerShell()
    {
        if (OperatingSystem.IsWindows())
            VerifyEnvironmentRestoration("powershell.exe");
    }

    [Fact(SkipUnless = nameof(HasPowerShellCore), Skip = "Requires an available Windows PowerShell 7 runtime.")]
    public void RestoresAbsentEmptyAndPopulatedEnvironmentInPowerShellCore()
        => VerifyEnvironmentRestoration(FindPowerShellCore()!);

    private static string? FindPowerShellCore()
        => (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim('"'), "pwsh.exe"))
            .FirstOrDefault(File.Exists);

    private static void VerifyEnvironmentRestoration(string executable)
    {
        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "test-cathedral-geometry.ps1"));
        Assert.Contains("Restore-CathedralOracleEnvironmentVariable $oracleDirectoryVariable $previousOracleDirectory", launcher, StringComparison.Ordinal);
        Assert.Contains("Restore-CathedralOracleEnvironmentVariable $inputDirectoryVariable $previousInputDirectory", launcher, StringComparison.Ordinal);
        Assert.Contains("Restore-CathedralOracleEnvironmentVariable $encounterDirectoryVariable $previousEncounterDirectory", launcher, StringComparison.Ordinal);
        Assert.Contains("Restore-CathedralOracleEnvironmentVariable $initialItemDirectoryVariable $previousInitialItemDirectory", launcher, StringComparison.Ordinal);
        var script = BuildPowerShellHarness(["Restore-CathedralOracleEnvironmentVariable"],
            """
            $probeName = 'GODEVILUTION_ORACLE_RESTORE_TEST_' + [guid]::NewGuid().ToString('N')
            try {
                foreach ($original in @($null, '', 'retained-value')) {
                    if ($null -eq $original) {
                        [Environment]::SetEnvironmentVariable($probeName, [NullString]::Value, 'Process')
                    } else {
                        [Environment]::SetEnvironmentVariable($probeName, $original, 'Process')
                    }
                    $before = [Environment]::GetEnvironmentVariable($probeName, 'Process')
                    if ($null -eq $original -and $null -ne $before) { throw 'An absent variable was made empty.' }
                    [Environment]::SetEnvironmentVariable($probeName, 'mutated-value', 'Process')
                    Restore-CathedralOracleEnvironmentVariable $probeName $before
                    if ([Environment]::GetEnvironmentVariable($probeName, 'Process') -cne $before) { throw 'Original environment state was not restored exactly.' }
                }
                Write-Output 'PASS'
            } finally { Restore-CathedralOracleEnvironmentVariable $probeName $null }
            """);
        Assert.Contains("PASS", RunPowerShell(script, executable), StringComparison.Ordinal);
    }

    [Fact]
    public void PopulationModeIsExclusiveAndKeepsExistingModes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "test-cathedral-geometry.ps1"));
        Assert.Contains("[switch]$CathedralDepths", launcher, StringComparison.Ordinal);
        Assert.Contains("[switch]$CathedralDoors", launcher, StringComparison.Ordinal);
        Assert.Contains("[switch]$CathedralPopulation", launcher, StringComparison.Ordinal);
        Assert.Contains("[switch]$CathedralPopulationParity", launcher, StringComparison.Ordinal);
        Assert.Contains("[switch]$CathedralEncounters", launcher, StringComparison.Ordinal);
        Assert.Contains("[switch]$CathedralInitialItems", launcher, StringComparison.Ordinal);
        Assert.Contains("--filter-method *AuthoritativeCathedralInitialItemNativeOracleTests.ItemsMatchNativeOracle --explicit only --fail-skips on", launcher, StringComparison.Ordinal);
        Assert.Contains("Assert-CathedralManagedReport $itemLog 16", launcher, StringComparison.Ordinal);
        Assert.Contains("--filter-method *AuthoritativeCathedralEncounterNativeOracleTests.EncounterMatchesNativeOracle --explicit only --fail-skips on", launcher, StringComparison.Ordinal);
        Assert.Contains("$previousEncounterDirectory", launcher, StringComparison.Ordinal);
        Assert.Contains("Assert-CathedralManagedReport $encounterLog 16", launcher, StringComparison.Ordinal);
        Assert.Contains("$CathedralPopulation -or $CathedralDoors -or $CathedralDepths", launcher, StringComparison.Ordinal);
        Assert.Contains("--filter-class *AuthoritativeCathedralInitialObjectNativeOracleTests --explicit only --fail-skips on", launcher, StringComparison.Ordinal);
        Assert.Contains("$CathedralPopulation -and ($CathedralDoors -or $CathedralDepths)", launcher, StringComparison.Ordinal);
        Assert.Contains("if ($CathedralDoors -and $CathedralDepths)", launcher, StringComparison.Ordinal);
        Assert.Contains("if ($CathedralPopulation)", launcher, StringComparison.Ordinal);
        Assert.Contains("--target cathedral_population_test", launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("Drlg_l1.NativeObjectPopulationLocalAssetOracle", launcher, StringComparison.Ordinal);
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(launcher, @"(?im)(?<![A-Za-z0-9_])\$pid\s*="), "The launcher must not assign the read-only PowerShell PID variable.");
        Assert.DoesNotContain("Kill($true)", launcher, StringComparison.Ordinal);

        var script = BuildPowerShellHarness(
            ["Get-CathedralPopulationOracleArtifactNames"],
            """
            $names = @(Get-CathedralPopulationOracleArtifactNames)
            if ($names.Count -ne 17 -or @($names | Select-Object -Unique).Count -ne 17) { throw 'Population artifact set is not exactly 17 files.' }
            if ($names[0] -cne 'metadata.json' -or 'cathedral-population-depth-4-743271966-previous.json' -notin $names) { throw 'Population artifact names are incomplete.' }
            $savedErrorActionPreference = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try { $incompatibleOutput = (& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $launcherPath -CathedralPopulation -CathedralDoors 2>&1 | Out-String) } finally { $ErrorActionPreference = $savedErrorActionPreference }
            if ($LASTEXITCODE -eq 0 -or $incompatibleOutput -notmatch 'mutually exclusive') { throw 'The incompatible Cathedral population mode did not fail before build selection.' }
            foreach ($incompatibleFlag in @('-CathedralPopulation','-CathedralDoors','-CathedralDepths')) {
                $ErrorActionPreference = 'Continue'
                try { $incompatibleOutput = (& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $launcherPath -CathedralPopulationParity $incompatibleFlag 2>&1 | Out-String) } finally { $ErrorActionPreference = $savedErrorActionPreference }
                if ($LASTEXITCODE -eq 0 -or $incompatibleOutput -notmatch 'mutually exclusive') { throw 'Population parity accepted an incompatible mode.' }
            }
            foreach ($incompatibleFlag in @('-CathedralPopulationParity','-CathedralPopulation','-CathedralDoors','-CathedralDepths')) {
                $ErrorActionPreference = 'Continue'
                try { $incompatibleOutput = (& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $launcherPath -CathedralEncounters $incompatibleFlag 2>&1 | Out-String) } finally { $ErrorActionPreference = $savedErrorActionPreference }
                if ($LASTEXITCODE -eq 0 -or $incompatibleOutput -notmatch 'mutually exclusive') { throw 'Encounter parity accepted an incompatible mode.' }
            }
            foreach ($incompatibleFlag in @('-CathedralEncounters','-CathedralPopulationParity','-CathedralPopulation','-CathedralDoors','-CathedralDepths')) {
                $ErrorActionPreference = 'Continue'
                try { $incompatibleOutput = (& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $launcherPath -CathedralInitialItems $incompatibleFlag 2>&1 | Out-String) } finally { $ErrorActionPreference = $savedErrorActionPreference }
                if ($LASTEXITCODE -eq 0 -or $incompatibleOutput -notmatch 'mutually exclusive') { throw 'Initial item parity accepted an incompatible mode.' }
            }
            Write-Output 'PASS'
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void InitialItemArtifactHeaderRejectsCoercionsExtraFilesAndIncompleteScopes()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(
            ["Assert-CathedralPopulationRequiredProperty", "Assert-CathedralPopulationExactProperties",
             "Assert-CathedralPopulationInteger", "Assert-CathedralPopulationString",
             "Assert-CathedralPopulationArray", "Get-CathedralInitialItemArtifact"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-PopulationValidator-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $primaryFailure = $null
            $path = Join-Path $work 'native-initial-items.json'
            function Write-Header($value) { [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 5)) }
            function Expect-Rejected([scriptblock]$action) {
                $rejected = $false; try { & $action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Malformed initial item header was accepted.' }
            }
            try {
                $valid = [ordered]@{ format = 'godevilution-cathedral-initial-items'; version = 1;
                    policy = 'ordinary-original-diablo-post-initmonsters-inititems'; caseCount = 16;
                    potionDefinitions = @(@{}, @{}); cases = @(1..16 | ForEach-Object { @{} }) }
                Write-Header $valid
                $artifact = Get-CathedralInitialItemArtifact $work
                if ($artifact.Name -cne 'native-initial-items.json' -or $artifact.Bytes -le 0 -or $artifact.Sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid initial item artifact identity.' }
                foreach ($mutation in @('stringVersion','floatCount','shortPotions','shortCases','extraProperty','wrongPolicy')) {
                    $bad = [ordered]@{}; foreach ($entry in $valid.GetEnumerator()) { $bad[$entry.Key] = $entry.Value }
                    switch ($mutation) {
                        'stringVersion' { $bad.version = '1' }
                        'floatCount' { $bad.caseCount = 16.0 }
                        'shortPotions' { $bad.potionDefinitions = @(@{}) }
                        'shortCases' { $bad.cases = @($valid.cases | Select-Object -First 15) }
                        'extraProperty' { $bad['extra'] = 1 }
                        'wrongPolicy' { $bad.policy = 'final-population' }
                    }
                    if ($mutation -eq 'floatCount') {
                        [IO.File]::WriteAllText($path, (($bad | ConvertTo-Json -Depth 5) -replace '"caseCount":\s*16(?:\.0)?,', '"caseCount":16.0,'))
                    } else { Write-Header $bad }
                    Expect-Rejected { Get-CathedralInitialItemArtifact $work }
                }
                Write-Header $valid
                $extra = Join-Path $work 'extra.json'
                [IO.File]::WriteAllText($extra, '{}')
                Expect-Rejected { Get-CathedralInitialItemArtifact $work }
                Remove-Item -LiteralPath $extra
                Write-Output 'PASS'
            } catch { $primaryFailure = $_; throw }
            finally { Remove-PopulationTestDirectory $work $primaryFailure }
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void EncounterArtifactHeaderRejectsCoercionsExtraFilesAndIncompleteScopes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-CathedralPopulationRequiredProperty", "Assert-CathedralPopulationExactProperties",
             "Assert-CathedralPopulationInteger", "Assert-CathedralPopulationString",
             "Assert-CathedralPopulationArray", "Get-CathedralEncounterInputArtifact"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-PopulationValidator-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $primaryFailure = $null
            $path = Join-Path $work 'native-encounter-inputs.json'
            function Write-Header($value) { [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 5)) }
            function Expect-Rejected([scriptblock]$action) {
                $rejected = $false; try { & $action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Malformed encounter artifact header was accepted.' }
            }
            try {
                $valid = [ordered]@{ format = 'godevilution-cathedral-l1-encounters'; version = 1;
                    policy = 'ordinary-original-diablo-post-initial-objects-initmonsters'; caseCount = 16;
                    uniqueDefinitions = @(1..100 | ForEach-Object { @{} }); cases = @(1..16 | ForEach-Object { @{} }) }
                Write-Header $valid
                $artifact = Get-CathedralEncounterInputArtifact $work
                if ($artifact.Name -cne 'native-encounter-inputs.json' -or $artifact.Bytes -le 0 -or $artifact.Sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid encounter artifact identity.' }
                foreach ($mutation in @('stringVersion','floatCount','shortUnique','shortCases','extraProperty','wrongPolicy')) {
                    $bad = [ordered]@{}; foreach ($entry in $valid.GetEnumerator()) { $bad[$entry.Key] = $entry.Value }
                    switch ($mutation) {
                        'stringVersion' { $bad.version = '1' }
                        'floatCount' { $bad.caseCount = 16.0 }
                        'shortUnique' { $bad.uniqueDefinitions = @($valid.uniqueDefinitions | Select-Object -First 99) }
                        'shortCases' { $bad.cases = @($valid.cases | Select-Object -First 15) }
                        'extraProperty' { $bad['extra'] = 1 }
                        'wrongPolicy' { $bad.policy = 'final-population' }
                    }
                    if ($mutation -eq 'floatCount') {
                        [IO.File]::WriteAllText($path, (($bad | ConvertTo-Json -Depth 5) -replace '"caseCount":\s*16(?:\.0)?,', '"caseCount":16.0,'))
                    } else { Write-Header $bad }
                    Expect-Rejected { Get-CathedralEncounterInputArtifact $work }
                }
                Write-Header $valid
                $extraPath = Join-Path $work 'unexpected.json'
                [IO.File]::WriteAllText($extraPath, '{}')
                Expect-Rejected { Get-CathedralEncounterInputArtifact $work }
                Remove-Item -LiteralPath $extraPath
                [IO.File]::WriteAllText($path, '')
                Expect-Rejected { Get-CathedralEncounterInputArtifact $work }
                Write-Output 'PASS'
            } catch { $primaryFailure = $_; throw }
            finally { Remove-PopulationTestDirectory $work $primaryFailure }
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataReportAndArtifactValidatorsRejectMalformedEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            [
                "Assert-CathedralPopulationRequiredProperty", "Assert-CathedralPopulationExactProperties",
                "Assert-CathedralPopulationInteger", "Assert-CathedralPopulationString",
                "Assert-CathedralPopulationArray", "Assert-CathedralPopulationExactStringArray",
                "Assert-CathedralPopulationMetadata", "Get-CathedralPopulationOracleArtifactNames",
                "Get-CathedralPopulationOracleArtifacts", "Get-CathedralPopulationInputArtifact", "Assert-CathedralPopulationNativeReport", "Assert-CathedralManagedReport",
                "Add-CathedralPopulationCleanupError", "Stop-CathedralPopulationOwnedProcessTree", "Invoke-CathedralPopulationOwnedProcess"
            ],
            """
            $ErrorActionPreference = 'Stop'
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-PopulationValidator-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            function Write-Json([string]$Path, $Value) { [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false)) }
            function Expect-Rejected([scriptblock]$Action) { $rejected = $false; try { & $Action | Out-Null } catch { $rejected = $true }; if (-not $rejected) { throw 'Malformed evidence was accepted.' } }
            $primaryFailure = $null; $reparsePath = $null
            try {
                $metadata = [ordered]@{
                    format = 'godevilution-cathedral-l1-population-oracle'; version = 1; policy = 'no-quest-original-diablo-initgolems-initobjects';
                    nativeWidth = 112; nativeHeight = 112; rowMajor = $true; headless = $true; soundInitialized = $false; game = 'diablo'; gameMode = 'singleplayer'; difficulty = 'normal'; playerLifeQ6 = 64;
                    caseCount = 16; depths = @(1,2,3,4); seeds = @(2588,743271966); entries = @('main','previous'); storySeed = 324508639;
                    objectFields = @('type','position','applyLighting','animFlag','animDelay','animCount','animLength','animFrame','animWidth','deleteFlag','breakFlag','solidFlag','missilePassable','selectionRegion','preFlag','trapFlag','doorFlag','lightId','rndSeed','var1','var2','var3','var4','var5','var6','bookMessage','var8');
                    monsterFields = @('type','levelType','mode','goal','goalVar1','goalVar2','goalVar3','var1','var2','var3','pathCount','tile','future','old','last','direction','enemy','enemyPosition','invalid','ai','intelligence','activeForTicks','uniqueType','uniqueTranslation','corpseId','whoHit','minDamage','maxDamage','minDamageSpecial','maxDamageSpecial','armorClass','resistance','talkMessage','leader','leaderRelation','packSize','flags','rndItemSeed','aiSeed','lightId','hitPoints','maxHitPoints','animTicksPerFrame','animTickCounter','animNumberOfFrames','animCurrentFrame');
                    rngBoundaries = @('rngAfterCreateLevel','rngAfterPopulationReset','rngAfterGetLevelMTypes','rngAfterInitThemes','rngAfterInitGolems','rngAfterInitObjects');
                    nativeObjectTypes = [ordered]@{ sarcophagus = 48; barrel = 100 }; nativeObjectCatalog = @()
                }
                foreach ($id in 0..108) { $metadata.nativeObjectCatalog += [ordered]@{ nativeObjectId = $id; graphicIndex = 0; minLevel = 1; maxLevel = 16; levelType = 0; theme = 0; quest = -1; flags = 0; animDelay = 0; animLength = 0; animWidth = 0; selectionRegion = 0; behavior = '' } }
                Write-Json (Join-Path $work 'metadata.json') $metadata
                Assert-CathedralPopulationMetadata $work
                foreach ($bad in @('version','nativeWidth','depths','storySeed')) {
                    $copy = [ordered]@{}; foreach ($entry in $metadata.GetEnumerator()) { $copy[$entry.Key] = $entry.Value }
                    if ($bad -eq 'depths') { $copy[$bad] = @('1',2,3,4) } elseif ($bad -eq 'storySeed') { $copy[$bad] = 0.5 } else { $copy[$bad] = [string]$copy[$bad] }
                    Write-Json (Join-Path $work 'metadata.json') $copy
                    Expect-Rejected { Assert-CathedralPopulationMetadata $work }
                    Write-Json (Join-Path $work 'metadata.json') $metadata
                }
                $metadata['extraInteger'] = 1
                Write-Json (Join-Path $work 'metadata.json') $metadata
                Expect-Rejected { Assert-CathedralPopulationMetadata $work }
                [void]$metadata.Remove('extraInteger')
                Write-Json (Join-Path $work 'metadata.json') $metadata

                $reportPath = Join-Path $work 'report.json'
                function Save-Report([string]$Class, [string]$Name, [string]$Result = 'COMPLETED') {
                    Write-Json $reportPath ([ordered]@{ tests = 1; failures = 0; errors = 0; disabled = 0; testsuites = @(@{ testsuite = @(@{ classname = $Class; name = $Name; status = 'RUN'; result = $Result }) }) })
                }
                Save-Report 'CathedralPopulation' 'NativeObjectPopulationLocalAssetOracle'
                Assert-CathedralPopulationNativeReport $reportPath
                foreach ($badReport in @(
                    @{ Class = 'Drlg_l1'; Result = 'COMPLETED'; Disabled = 0 },
                    @{ Class = 'CathedralPopulation'; Result = 'SKIPPED'; Disabled = 0 },
                    @{ Class = 'CathedralPopulation'; Result = 'COMPLETED'; Disabled = 1 })) {
                    Save-Report $badReport.Class 'NativeObjectPopulationLocalAssetOracle' $badReport.Result
                    if ($badReport.Disabled -ne 0) { $report = [IO.File]::ReadAllText($reportPath) | ConvertFrom-Json; $report.disabled = 1; Write-Json $reportPath $report }
                    Expect-Rejected { Assert-CathedralPopulationNativeReport $reportPath }
                }

                $artifactRoot = Join-Path $work 'artifacts'; New-Item -ItemType Directory -Path $artifactRoot | Out-Null
                foreach ($name in @(Get-CathedralPopulationOracleArtifactNames)) { [IO.File]::WriteAllText((Join-Path $artifactRoot $name), 'evidence') }
                $artifacts = @(Get-CathedralPopulationOracleArtifacts $artifactRoot)
                if ($artifacts.Count -ne 17 -or @($artifacts | Where-Object { $_.Bytes -le 0 -or $_.Sha256 -notmatch '^[0-9a-f]{64}$' }).Count -ne 0) { throw 'Complete population artifact hashes were not retained.' }
                [IO.File]::WriteAllText((Join-Path $artifactRoot 'extra.json'), 'out-of-scope')
                Expect-Rejected { Get-CathedralPopulationOracleArtifacts $artifactRoot }
                [IO.File]::Delete((Join-Path $artifactRoot 'extra.json'))
                $hidden = Join-Path $artifactRoot '.hidden-extra'; [IO.File]::WriteAllText($hidden, 'out-of-scope'); [IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Hidden)
                Expect-Rejected { Get-CathedralPopulationOracleArtifacts $artifactRoot }
                Remove-Item -LiteralPath $hidden -Force
                $inputRoot = Join-Path $work 'inputs'; New-Item -ItemType Directory -Path $inputRoot | Out-Null
                Expect-Rejected { Get-CathedralPopulationInputArtifact $inputRoot }
                $inputPath = Join-Path $inputRoot 'native-population-inputs.json'
                [IO.File]::WriteAllText($inputPath, '')
                Expect-Rejected { Get-CathedralPopulationInputArtifact $inputRoot }
                [IO.File]::WriteAllText($inputPath, '{}')
                $inputArtifact = Get-CathedralPopulationInputArtifact $inputRoot
                if ($inputArtifact.Name -cne 'native-population-inputs.json' -or $inputArtifact.Bytes -ne 2 -or $inputArtifact.Sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Input artifact hash or exact identity was lost.' }
                Expect-Rejected { Get-CathedralPopulationInputArtifact $inputRoot -RequirePrerequisites }
                $inputMetadata = [ordered]@{ format = 'godevilution-cathedral-initial-inputs'; version = 2; policy = 'ordinary-original-diablo-fresh-initial-objects'; caseCount = 16; levelGenerationTableLoaded = $true; freshBestiarySlots = $true; storyMessages = @(); constants = @{}; monsterData = @(); cases = @() }
                Write-Json $inputPath $inputMetadata
                Get-CathedralPopulationInputArtifact $inputRoot -RequirePrerequisites | Out-Null
                foreach ($badVersion in @(1,3,'2',2.5)) {
                    $inputMetadata.version = $badVersion; Write-Json $inputPath $inputMetadata
                    Expect-Rejected { Get-CathedralPopulationInputArtifact $inputRoot -RequirePrerequisites }
                }
                $inputMetadata.version = 2
                foreach ($badFresh in @($false,'true',1)) {
                    $inputMetadata.freshBestiarySlots = $badFresh; Write-Json $inputPath $inputMetadata
                    Expect-Rejected { Get-CathedralPopulationInputArtifact $inputRoot -RequirePrerequisites }
                }
                $inputMetadata.freshBestiarySlots = $true; Write-Json $inputPath $inputMetadata
                $extraInput = Join-Path $inputRoot '.extra-input'; [IO.File]::WriteAllText($extraInput, '{}'); [IO.File]::SetAttributes($extraInput, [IO.FileAttributes]::Hidden)
                Expect-Rejected { Get-CathedralPopulationInputArtifact $inputRoot }
                Remove-Item -LiteralPath $extraInput -Force
                $reparsePath = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-PopulationAlias-' + [guid]::NewGuid().ToString('N'))
                $junction = Invoke-CathedralPopulationOwnedProcess -Executable (Join-Path $env:SystemRoot 'System32\cmd.exe') -Arguments ('/d /c mklink /J "{0}" "{1}"' -f $reparsePath, $inputRoot) -LogPath (Join-Path $work 'junction.log') -TimeoutMilliseconds 10000
                if ($junction.ExitCode -ne 0 -or -not $junction.ConfirmedExited -or $null -ne $junction.Error) { throw ('Could not create the bounded reparse-point probe: ' + $junction.Output + $junction.Error) }
                $reparseItem = Get-Item -LiteralPath $reparsePath -Force
                if (($reparseItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) { throw 'The reparse-point probe is not a junction.' }
                $reparseRejected = $false
                try { Get-CathedralPopulationOracleArtifacts $reparsePath | Out-Null } catch { if ($_.Exception.Message -notmatch 'reparse point') { throw }; $reparseRejected = $true }
                if (-not $reparseRejected) { throw 'A reparse-point artifact root was accepted.' }
                $inputReparseRejected = $false
                try { Get-CathedralPopulationInputArtifact $reparsePath | Out-Null } catch { if ($_.Exception.Message -notmatch 'regular directory') { throw }; $inputReparseRejected = $true }
                if (-not $inputReparseRejected) { throw 'A reparse-point input root was accepted.' }
                $managedLog = Join-Path $work 'managed-summary.log'
                $validSummary = 'Test run summary: Passed! total: 16 failed: 0 succeeded: 16 skipped: 0'
                [IO.File]::WriteAllText($managedLog, $validSummary)
                Assert-CathedralManagedReport $managedLog 16
                foreach ($invalidSummary in @(
                    'Test run summary: Passed! total: 15 failed: 0 succeeded: 15 skipped: 0',
                    'Test run summary: Passed! total: 17 failed: 0 succeeded: 17 skipped: 0',
                    'Test run summary: Passed! total: 16 failed: 1 succeeded: 15 skipped: 0',
                    'Test run summary: Passed! total: 16 failed: 0 succeeded: 15 skipped: 1',
                    ($validSummary + [Environment]::NewLine + $validSummary))) {
                    [IO.File]::WriteAllText($managedLog, $invalidSummary)
                    Expect-Rejected { Assert-CathedralManagedReport $managedLog 16 }
                }
                Write-Output 'PASS'
            } catch { $primaryFailure = $_; throw }
            finally {
                if ($null -ne $reparsePath -and (Test-Path -LiteralPath $reparsePath)) {
                    try { [IO.Directory]::Delete($reparsePath) } catch { Add-PopulationTestCleanupFailure $primaryFailure $_ }
                }
                Remove-PopulationTestDirectory $work $primaryFailure
            }
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void CaseValidatorUsesExactNativePoolsAndRejectsCoercions()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            [
                "Assert-CathedralPopulationRequiredProperty", "Assert-CathedralPopulationExactProperties",
                "Assert-CathedralPopulationInteger", "Assert-CathedralPopulationString",
                "Assert-CathedralPopulationArray", "Assert-CathedralPopulationArrayCount", "Assert-CathedralPopulationExactStringArray",
                "Assert-CathedralPopulationPoint", "Assert-CathedralPopulationUnsignedArray",
                "Assert-CathedralPopulationCase"
            ],
            """
            $ErrorActionPreference = 'Stop'
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-PopulationCase-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            function Point([int]$x, [int]$y) { return [ordered]@{ x = $x; y = $y } }
            function Write-Case($Case) { $path = Join-Path $work 'cathedral-population-depth-1-2588-main.json'; [IO.File]::WriteAllText($path, ($Case | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false)); return $path }
            function Expect-Rejected([scriptblock]$Action) { $rejected = $false; try { & $Action | Out-Null } catch { $rejected = $true }; if (-not $rejected) { throw 'Malformed population case was accepted.' } }
            $primaryFailure = $null
            try {
                $object = [ordered]@{ slot = 0; type = 1; position = (Point 0 0); applyLighting = $true; animFlag = 0; animDelay = 0; animCount = 0; animLength = 0; animFrame = 0; animWidth = 0; deleteFlag = $false; breakFlag = 0; solidFlag = $true; missilePassable = $false; selectionRegion = 0; preFlag = $false; trapFlag = $false; doorFlag = $false; lightId = 0; rndSeed = 1; var1 = 0; var2 = 0; var3 = 0; var4 = 0; var5 = 0; var6 = 0; bookMessage = 0; var8 = 0 }
                $secondObject = [ordered]@{}; foreach ($entry in $object.GetEnumerator()) { $secondObject[$entry.Key] = $entry.Value }; $secondObject.slot = 1; $secondObject.position = (Point 0 1)
                $monster = [ordered]@{ slot = 0; type = 1; levelType = 0; mode = 0; goal = 0; goalVar1 = 0; goalVar2 = 0; goalVar3 = 0; var1 = 0; var2 = 0; var3 = 0; pathCount = 0; tile = (Point 1 0); future = (Point 1 0); old = (Point 1 0); last = (Point 1 0); direction = 0; enemy = -1; enemyPosition = (Point 1 0); invalid = $false; ai = 0; intelligence = 0; activeForTicks = 0; uniqueType = 0; uniqueTranslation = 0; corpseId = 0; whoHit = 0; minDamage = 0; maxDamage = 0; minDamageSpecial = 0; maxDamageSpecial = 0; armorClass = 0; resistance = 0; talkMessage = 0; leader = -1; leaderRelation = 0; packSize = 0; flags = 0; rndItemSeed = 1; aiSeed = 1; lightId = 0; hitPoints = 10; maxHitPoints = 10; animTicksPerFrame = 0; animTickCounter = 0; animNumberOfFrames = 0; animCurrentFrame = 0 }
                $monsters = @(); for ($monsterIndex = 0; $monsterIndex -lt 4; $monsterIndex++) { $copy = [ordered]@{}; foreach ($entry in $monster.GetEnumerator()) { $copy[$entry.Key] = $entry.Value }; $copy.slot = $monsterIndex; $monsters += @($copy) }
                $bestiary = [ordered]@{ index = 0; type = 1; placeFlags = 0; corpseId = 0; frames = @(0,0,0,0,0,0); rates = @(0,0,0,0,0,0); widths = @(0,0,0,0,0,0); skeleton = $false }
                $playerContext = [ordered]@{ id = 0; class = 0; level = 1; active = $true; levelChanging = $false; tile = (Point 1 1); future = (Point 1 1); old = (Point 1 1) }
                $metadata = [ordered]@{ nativeObjectTypes = [ordered]@{ sarcophagus = 48; barrel = 100 }; nativeObjectCatalog = @() }
                foreach ($id in 0..108) { $metadata.nativeObjectCatalog += [ordered]@{ nativeObjectId = $id } }
                $tileRefs = @(1) + (@(0) * 111) + @(2) + (@(0) * 12431)
                $case = [ordered]@{ format = 'godevilution-cathedral-l1-population-oracle'; version = 1; policy = 'no-quest-original-diablo-initgolems-initobjects'; depth = 1; inputSeed = 2588; geometrySeed = 2588; populationSeed = 2588; storySeed = 324508639; entry = 'main'; attemptSeed = 1; rngAfterCreateLevel = 1; rngAfterPopulationReset = 2; rngAfterGetLevelMTypes = 3; rngAfterInitThemes = 4; rngAfterInitGolems = 5; rngAfterInitObjects = 6; entryPosition = (Point 1 1); stairsUp = (Point 2 2); stairsDown = (Point 3 3); triggers = @([ordered]@{ position = (Point 4 4); message = 1; level = 1 }); populatedCellsBeforeHoldThemeRooms = @(0); populatedCellsAfterHoldThemeRooms = @(0,1); pieces = @(0) * 12544; transparency = @(0) * 12544; solProperties = @(0) * 1379; activeObjectCount = 2; activeObjectIds = @(0..126); availableObjectIds = @((2..126) + @(0,1)); tileObjectRefs = $tileRefs; objects = @($object,$secondObject); activeMonsterCount = 4; activeMonsterIds = @(0,1,2,3); monsters = $monsters; bestiary = @($bestiary); playerContext = $playerContext }
                $path = Write-Case $case
                Assert-CathedralPopulationCase $path 'cathedral-population-depth-1-2588-main.json' $metadata
                foreach ($mutation in @(
                    { param($c) $c.version = '1' },
                    { param($c) $c.entryPosition.x = 0.5 },
                    { param($c) $c.tileObjectRefs[0] = 127 },
                    { param($c) $c.tileObjectRefs[0] = 2 },
                    { param($c) $c.activeObjectIds[1] = $c.activeObjectIds[0] },
                    { param($c) $c.availableObjectIds[0] = 0 },
                    { param($c) $c.objects[0].applyLighting = 1 },
                    { param($c) $c.objects[0].rndSeed = -1 },
                    { param($c) $c.objects[0].animFlag = -1 },
                    { param($c) $c.objects[0].animWidth = -1 },
                    { param($c) $c.objects[0].var6 = -1 },
                    { param($c) $c.geometrySeed = 743271966 },
                    { param($c) $c.populationSeed = 743271966 },
                    { param($c) $c.monsters[1].slot = 0 },
                    { param($c) $c.monsters[0].rndItemSeed = -1 },
                    { param($c) $c.monsters[0].levelType = 1 },
                    { param($c) $c.bestiary[0].skeleton = 'false' },
                    { param($c) Add-Member -InputObject $c -MemberType NoteProperty -Name objectCatalog -Value @() })) {
                    $copy = ($case | ConvertTo-Json -Depth 16) | ConvertFrom-Json
                    & $mutation $copy
                    $badPath = Write-Case $copy
                    Expect-Rejected { Assert-CathedralPopulationCase $badPath 'cathedral-population-depth-1-2588-main.json' $metadata }
                }
                $reservationCase = ($case | ConvertTo-Json -Depth 16) | ConvertFrom-Json
                $skeleton = ($monster | ConvertTo-Json -Depth 16) | ConvertFrom-Json
                $skeleton.slot = 4; $skeleton.type = 2; $skeleton.levelType = 1
                foreach ($field in @('tile','future','old','last','enemyPosition')) { $skeleton.$field = Point 0 0 }
                $skeletonEntry = ($bestiary | ConvertTo-Json -Depth 16) | ConvertFrom-Json
                $skeletonEntry.index = 1; $skeletonEntry.type = 2; $skeletonEntry.skeleton = $true
                $reservationCase.activeMonsterCount = 5; $reservationCase.activeMonsterIds = @(0..4)
                $reservationCase.monsters += @($skeleton); $reservationCase.bestiary += @($skeletonEntry)
                $reservationCase.objects[1].type = 100; $reservationCase.objects[1].var2 = 8; $reservationCase.objects[1].var4 = 4
                $reservationCase.objects[0].type = 48; $reservationCase.objects[0].position = Point 5 5
                $reservationCase.objects[0].var1 = 8; $reservationCase.objects[0].var2 = 4
                $reservationCase.tileObjectRefs[0] = 0; $reservationCase.tileObjectRefs[565] = 1; $reservationCase.tileObjectRefs[453] = -1
                Assert-CathedralPopulationCase (Write-Case $reservationCase) 'cathedral-population-depth-1-2588-main.json' $metadata
                foreach ($mutation in @(
                    { param($c) $c.tileObjectRefs[453] = 1 },
                    { param($c) $c.objects[0].var2 = 0 },
                    { param($c) $c.objects[1].var4 = 9 },
                    { param($c) $c.objects[1].var4 = -2 },
                    { param($c) $c.monsters[4].tile = Point 1 1 },
                    { param($c) $c.bestiary[1].skeleton = $false })) {
                    $copy = ($reservationCase | ConvertTo-Json -Depth 16) | ConvertFrom-Json
                    & $mutation $copy
                    Expect-Rejected { Assert-CathedralPopulationCase (Write-Case $copy) 'cathedral-population-depth-1-2588-main.json' $metadata }
                }
                $reservationCase.objects[0].var2 = -1; $reservationCase.objects[1].var4 = -1
                Assert-CathedralPopulationCase (Write-Case $reservationCase) 'cathedral-population-depth-1-2588-main.json' $metadata
                Write-Output 'PASS'
            } catch { $primaryFailure = $_; throw }
            finally { Remove-PopulationTestDirectory $work $primaryFailure }
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void NativeOracleProcessRetainsOutputPidAndBoundedTimeoutEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Quote-CathedralNativeArgument", "Add-CathedralPopulationCleanupError", "Stop-CathedralPopulationOwnedProcessTree", "Invoke-CathedralPopulationOwnedProcess", "Invoke-CathedralPopulationNativeOracle"],
            """
            $ErrorActionPreference = 'Stop'
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-PopulationProcess-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $primaryFailure = $null
            try {
                $powershell = (Get-Command powershell.exe -ErrorAction Stop).Source
                foreach ($deadline in @(-1, 0)) {
                    $invalid = Invoke-CathedralPopulationOwnedProcess -Executable $powershell -Arguments '-NoProfile -NonInteractive -Command "exit 0"' -LogPath (Join-Path $work ('invalid-' + $deadline + '.log')) -TimeoutMilliseconds $deadline
                    if ($null -ne $invalid.Pid -or -not $invalid.LogRetained -or $invalid.Error -notmatch 'positive') { throw 'An invalid deadline started an owned process or lost failure evidence.' }
                }
                $missing = Invoke-CathedralPopulationOwnedProcess -Executable (Join-Path $work 'missing-child.exe') -Arguments '' -LogPath (Join-Path $work 'missing.log') -TimeoutMilliseconds 10000
                if ($null -ne $missing.Pid -or $missing.ConfirmedExited -or -not $missing.LogRetained -or [string]::IsNullOrWhiteSpace($missing.Error)) { throw 'Startup failure did not retain bounded process evidence.' }
                $nonzero = Invoke-CathedralPopulationOwnedProcess -Executable $powershell -Arguments '-NoProfile -NonInteractive -Command "[Console]::Error.WriteLine(''nonzero''); exit 7"' -LogPath (Join-Path $work 'nonzero.log') -TimeoutMilliseconds 10000
                if ($null -eq $nonzero.Pid -or $nonzero.ExitCode -ne 7 -or -not $nonzero.ConfirmedExited -or -not $nonzero.LogRetained -or $nonzero.Output -notmatch 'nonzero') { throw 'Nonzero process outcome did not retain exit/output evidence.' }
                $success = Invoke-CathedralPopulationNativeOracle -Executable $powershell -ReportPath (Join-Path $work 'success.json') -LogPath (Join-Path $work 'success.log') -TimeoutMilliseconds 10000 -Arguments '-NoProfile -NonInteractive -Command "[Console]::WriteLine(''stdout''); [Console]::Error.WriteLine(''stderr'')"'
                if ($null -eq $success.Pid -or $success.ExitCode -ne 0 -or -not $success.ConfirmedExited -or -not $success.LogRetained -or $null -ne $success.Error -or $success.Output -notmatch 'stdout' -or $success.Output -notmatch 'stderr') { throw 'Successful native process outcome lost PID, output, or exit state.' }
                if (-not (Test-Path -LiteralPath (Join-Path $work 'success.log'))) { throw 'Successful process log was not retained.' }
                $timeout = Invoke-CathedralPopulationNativeOracle -Executable $powershell -ReportPath (Join-Path $work 'timeout.json') -LogPath (Join-Path $work 'timeout.log') -TimeoutMilliseconds 250 -Arguments '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 30"'
                if ($null -eq $timeout.Pid -or -not $timeout.TimedOut -or -not $timeout.ConfirmedExited -or -not $timeout.LogRetained -or [string]::IsNullOrWhiteSpace($timeout.Error) -or $timeout.Error -notmatch 'stage deadline' -or $null -eq $timeout.ExitCode) { throw 'Timeout outcome did not retain bounded termination evidence.' }
                if (-not (Test-Path -LiteralPath (Join-Path $work 'timeout.log'))) { throw 'Timeout process log was not retained.' }
                Write-Output 'PASS'
            } catch { $primaryFailure = $_; throw }
            finally { Remove-PopulationTestDirectory $work $primaryFailure }
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    private static string BuildPowerShellHarness(string[] functionNames, string body, string relativeLauncherPath = "tools/test-cathedral-geometry.ps1")
    {
        var launcherPath = Path.Combine(FindRepositoryRoot(), relativeLauncherPath);
        var lines = new List<string>
        {
            "$ErrorActionPreference = 'Stop'",
            $"$launcherPath = {QuotePowerShell(launcherPath)}",
            "$launcherText = [IO.File]::ReadAllText($launcherPath)",
            "$tokens = $null",
            "$parseErrors = $null",
            "$launcherAst = [System.Management.Automation.Language.Parser]::ParseInput($launcherText, [ref]$tokens, [ref]$parseErrors)",
            "if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }",
        };
        foreach (var functionName in functionNames)
        {
            lines.Add($"$functionName = {QuotePowerShell(functionName)}");
            lines.Add("$functionAst = $launcherAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName }, $true) | Select-Object -First 1");
            lines.Add("if ($null -eq $functionAst) { throw \"Could not extract function $functionName from launcher.\" }");
            lines.Add(". ([scriptblock]::Create($functionAst.Extent.Text))");
        }
        lines.Add("""
            function Add-PopulationTestCleanupFailure($PrimaryFailure, $CleanupFailure) {
                if ($null -eq $PrimaryFailure) { throw $CleanupFailure }
                $PrimaryFailure.Exception.Data['PopulationTestCleanup'] = [string]$PrimaryFailure.Exception.Data['PopulationTestCleanup'] + ' | ' + $CleanupFailure.Exception.Message
                Write-Warning -WarningAction Continue ('Secondary population test cleanup failure: ' + $CleanupFailure.Exception.Message)
            }
            function Remove-PopulationTestDirectory([string]$Work, $PrimaryFailure) {
                try {
                    $target = [IO.Path]::GetFullPath($Work)
                    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
                    if (-not $target.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -notmatch '^Godevilution-Population(?:Validator|Case|Process)-[0-9a-f]{32}$') { throw 'Refusing cleanup outside the exact owned temporary test directory.' }
                    if (Test-Path -LiteralPath $target) {
                        if (((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing cleanup of a reparse-point test root.' }
                        Remove-Item -LiteralPath $target -Recurse -Force
                    }
                } catch { Add-PopulationTestCleanupFailure $PrimaryFailure $_ }
            }
            """);
        lines.Add(body.Trim());
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string RunPowerShell(string script, string executable = "powershell.exe")
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"godevilution-population-test-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Exception? primaryFailure = null;
        var cleanupErrors = new List<string>();
        Process? process = null;
        Task<string>? stdout = null;
        Task<string>? stderr = null;
        string? output = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start PowerShell.");
            try
            {
                stdout = process.StandardOutput.ReadToEndAsync();
                stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(45_000))
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (Exception exception) { cleanupErrors.Add($"Could not terminate PowerShell harness: {exception.Message}"); }
                    var exited = process.WaitForExit(10_000);
                    var stdoutJoined = stdout.Wait(10_000);
                    var stderrJoined = stderr.Wait(10_000);
                    var outputAfterTimeout = (stdoutJoined ? stdout.GetAwaiter().GetResult() : "") +
                        (stderrJoined ? stderr.GetAwaiter().GetResult() : "");
                    Assert.True(exited, $"PowerShell population harness did not confirm owned process exit. Output:{Environment.NewLine}{outputAfterTimeout}");
                    Assert.True(stdoutJoined && stderrJoined, $"PowerShell population harness did not drain both output streams. Output:{Environment.NewLine}{outputAfterTimeout}");
                    Assert.Fail($"PowerShell population harness timed out. Output:{Environment.NewLine}{outputAfterTimeout}");
                }
                Assert.True(stdout.Wait(10_000), "PowerShell population harness stdout did not join by its deadline.");
                Assert.True(stderr.Wait(10_000), "PowerShell population harness stderr did not join by its deadline.");
                output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                Assert.True(process.ExitCode == 0, $"PowerShell population harness failed ({process.ExitCode}):{Environment.NewLine}{output}");
                return output!;
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        if (!process.WaitForExit(10_000))
                            cleanupErrors.Add("PowerShell population harness did not confirm owned process exit during cleanup.");
                    }
                }
                catch (Exception exception) { cleanupErrors.Add($"PowerShell harness cleanup failed: {exception.Message}"); }
                foreach (var stream in new[] { stdout, stderr })
                {
                    if (stream is not null)
                    {
                        try
                        {
                            if (!stream.Wait(10_000))
                                cleanupErrors.Add("PowerShell population harness output did not drain during cleanup.");
                        }
                        catch (Exception exception) { cleanupErrors.Add($"PowerShell output cleanup failed: {exception.Message}"); }
                    }
                }
                try { process.Dispose(); }
                catch (Exception exception) { cleanupErrors.Add($"PowerShell process disposal failed: {exception.Message}"); }
            }
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            if (File.Exists(scriptPath))
            {
                try { File.Delete(scriptPath); }
                catch (Exception exception) { cleanupErrors.Add($"Could not remove temporary PowerShell script: {exception.Message}"); }
            }
            if (primaryFailure is null && cleanupErrors.Count > 0)
                throw new InvalidOperationException(string.Join(" | ", cleanupErrors));
            if (primaryFailure is not null && cleanupErrors.Count > 0)
                primaryFailure.Data["PopulationTestCleanup"] = cleanupErrors.ToArray();
        }
    }

    private static string QuotePowerShell(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots)
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}
