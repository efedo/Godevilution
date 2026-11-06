using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{

    [Fact]
    public void GodotProjectStagingPreservesSourcesAndRejectsUnexpectedImports()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            [
                "Get-CanonicalPath", "Assert-PathWithinRepository", "Get-ProjectRelativePath",
                "Assert-NoReparsePoint", "Assert-NoReparseAncestry",
                "Get-IsolatedProjectFiles", "Get-ProjectTreeHash", "Get-FileSha256",
                "Read-ProjectXml", "Resolve-ValidatedProjectReference", "Get-ProjectSignature",
                "Get-ProjectMetadata", "Test-ExactStringSequence", "Set-StagedProjectReferences",
                "Copy-IsolatedProjectTree", "Rewrite-StagedSolutionReferences",
                "New-IsolatedGodotProject", "Complete-IsolatedGodotProject",
                "Write-ProjectStageEvidence", "Parse-SupportedGodotEngineVersion",
            ],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-ProjectStageTest-' + [guid]::NewGuid().ToString('N'))
            $repositoryRoot = Join-Path $work 'repository with spaces'
            $source = Join-Path $repositoryRoot 'godot-client/Devilution.Client'
            $protocol = Join-Path $repositoryRoot 'protocol/Devilution.Protocol/Devilution.Protocol.csproj'
            $clientProtocol = Join-Path $repositoryRoot 'godot-client/Devilution.Client.Protocol/Devilution.Client.Protocol.csproj'
            $config = Join-Path $repositoryRoot 'godot-client/NuGet.Config'
            New-Item -ItemType Directory -Path $source, (Split-Path $protocol), (Split-Path $clientProtocol) | Out-Null
            function Expect-Rejected([scriptblock]$Action) {
                $rejected = $false
                try { & $Action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw "An invalid staged project was accepted: $Action" }
            }
            function Assert-OriginalBytes {
                if ([IO.File]::ReadAllText($project) -cne $projectText -or
                    [IO.File]::ReadAllText($backup) -cne 'pre-existing user backup' -or
                    [IO.File]::ReadAllText($localEdit) -cne 'uncommitted local source edit' -or
                    [IO.File]::ReadAllText($config) -cne $configText) {
                    throw 'Staging or rejection modified original user files.'
                }
            }
            try {
                if ((Parse-SupportedGodotEngineVersion '4.7.2.stable.mono.official.synthetic') -cne '4.7.2') {
                    throw 'The actual stable Mono version format was not recognized.'
                }
                foreach ($badVersion in @('', '4.7.2.stable.official.synthetic',
                        '4.7.2.rc1.mono.official.synthetic', '4.8.1.stable.mono.official.synthetic',
                        '3.7.2.stable.mono.official.synthetic',
                        ('4.7.2.rc1.mono.synthetic' + [Environment]::NewLine + 'warning: stable mono'),
                        ('4.7.2.stable.mono.synthetic' + [Environment]::NewLine + '4.7.1.stable.mono.synthetic'))) {
                    Expect-Rejected { Parse-SupportedGodotEngineVersion $badVersion }
                }
                $project = Join-Path $source 'Devilution.Client.csproj'
                $projectText = '<Project Sdk="Godot.NET.Sdk/4.7.1"><!-- local project comment --><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include="../../protocol/Devilution.Protocol/Devilution.Protocol.csproj" Aliases="global" /><ProjectReference Include="../Devilution.Client.Protocol/Devilution.Client.Protocol.csproj" /></ItemGroup></Project>'
                [IO.File]::WriteAllText($project, $projectText)
                [IO.File]::WriteAllText($protocol, '<Project Sdk="Microsoft.NET.Sdk" />')
                [IO.File]::WriteAllText($clientProtocol, '<Project Sdk="Microsoft.NET.Sdk" />')
                $configText = '<configuration><packageSources><clear /><add key="private-local-test" value="https://example.invalid/v3/index.json" /></packageSources></configuration>'
                [IO.File]::WriteAllText($config, $configText)
                [IO.File]::WriteAllText((Join-Path $source 'project.godot'), 'config_version=5')
                $localEdit = Join-Path $source 'Main.cs'
                [IO.File]::WriteAllText($localEdit, 'uncommitted local source edit')
                $backup = Join-Path $source 'Devilution.Client.csproj.old'
                [IO.File]::WriteAllText($backup, 'pre-existing user backup')
                foreach ($cache in @('.godot', 'bin', 'obj', 'nested/obj')) {
                    $cachePath = Join-Path $source $cache
                    New-Item -ItemType Directory -Path $cachePath -Force | Out-Null
                    [IO.File]::WriteAllText((Join-Path $cachePath 'stale.txt'), 'generated stale output')
                }
                New-Item -ItemType Directory -Path (Join-Path $source 'assets') | Out-Null
                [IO.File]::WriteAllText((Join-Path $source 'assets/player.svg'), '<svg />')
                $solution = Join-Path $source 'Devilution.Client.sln'
                $solutionText = @(
                    'Project("{TYPE}") = "Devilution.Client", "Devilution.Client.csproj", "{CLIENT}"'
                    'EndProject'
                    'Project("{TYPE}") = "Devilution.Protocol", "..\..\protocol\Devilution.Protocol\Devilution.Protocol.csproj", "{PROTOCOL}"'
                    'EndProject'
                ) -join [Environment]::NewLine
                [IO.File]::WriteAllText($solution, $solutionText + [Environment]::NewLine)
                $stagePath = Join-Path $work 'capture with spaces/client'
                $stage = New-IsolatedGodotProject $source $stagePath
                if ($stage -is [array]) { throw 'Staging emitted extra pipeline objects.' }
                Assert-OriginalBytes
                if ([IO.File]::ReadAllText((Join-Path $stagePath 'Main.cs')) -cne 'uncommitted local source edit' -or
                    [IO.File]::ReadAllText((Join-Path $stagePath 'assets/player.svg')) -cne '<svg />') {
                    throw 'Staging omitted authored sources or resources.'
                }
                foreach ($excluded in @('.godot', 'bin', 'obj', 'nested/obj', 'Devilution.Client.csproj.old')) {
                    if (Test-Path -LiteralPath (Join-Path $stagePath $excluded)) {
                        throw "A stale generated artifact was staged: $excluded"
                    }
                }
                if ([IO.File]::ReadAllText($stage.StagedNuGetConfig) -cne $configText) {
                    throw 'Staging lost the original NuGet source policy.'
                }
                $stagedText = [IO.File]::ReadAllText($stage.StagedProject)
                if ($stagedText -notmatch 'Aliases="global"' -or $stagedText -notmatch 'local project comment') {
                    throw 'Staging removed project metadata.'
                }
                $stagedSolution = [IO.File]::ReadAllText((Join-Path $stagePath 'Devilution.Client.sln'))
                if (-not $stagedSolution.Contains('"' + $stage.StagedProject + '"') -or
                    -not $stagedSolution.Contains('"' + $protocol + '"') -or
                    $stagedSolution.Contains('"' + $project + '"')) {
                    throw 'The staged solution can build the original client or has unresolved references.'
                }
                [IO.File]::WriteAllText($stage.StagedProject, $stagedText.Replace('Godot.NET.Sdk/4.7.1', 'Godot.NET.Sdk/4.7.2'))
                $completed = Complete-IsolatedGodotProject $stage '4.7.2'
                if ($completed.ImportedSdk -cne 'Godot.NET.Sdk/4.7.2') { throw 'Imported SDK identity was lost.' }
                $evidence = Join-Path $work 'project-stage.json'
                Write-ProjectStageEvidence $completed '4.7.2' $evidence
                $record = [IO.File]::ReadAllText($evidence) | ConvertFrom-Json
                if ($record.SourceSdk -cne 'Godot.NET.Sdk/4.7.1' -or
                    $record.ImportedSdk -cne 'Godot.NET.Sdk/4.7.2' -or
                    $record.EngineVersion -cne '4.7.2' -or
                    $record.SourceHash -notmatch '^[0-9a-f]{64}$' -or
                    $record.StageHash -notmatch '^[0-9a-f]{64}$') {
                    throw 'Staged project evidence is incomplete.'
                }
                $importedText = [IO.File]::ReadAllText($stage.StagedProject)
                [IO.File]::WriteAllText($stage.StagedProject, $importedText.Replace('net8.0', 'net9.0'))
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedProject, $importedText.Replace('4.7.2', '4.7.3'))
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedProject, $importedText.Replace($protocol, $clientProtocol))
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedProject, $importedText)
                [IO.File]::WriteAllText($stage.StagedNuGetConfig, '<configuration />')
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::Delete($stage.StagedNuGetConfig)
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                [IO.File]::WriteAllText($stage.StagedNuGetConfig, $configText)
                Expect-Rejected { New-IsolatedGodotProject $source $stagePath }
                Assert-OriginalBytes
                [IO.File]::WriteAllText($project, $projectText.Replace('../../protocol/Devilution.Protocol/Devilution.Protocol.csproj', 'missing.csproj'))
                Expect-Rejected { New-IsolatedGodotProject $source (Join-Path $work 'missing-ref/client') }
                [IO.File]::WriteAllText($project, $projectText)
                [IO.File]::WriteAllText($localEdit, 'concurrent user edit that must not be overwritten')
                Expect-Rejected { Complete-IsolatedGodotProject $stage '4.7.2' }
                if ([IO.File]::ReadAllText($localEdit) -cne 'concurrent user edit that must not be overwritten') {
                    throw 'Rejection overwrote a concurrent user edit.'
                }
                [IO.File]::WriteAllText($localEdit, 'uncommitted local source edit')
                Assert-OriginalBytes
                $outside = Join-Path $work 'outside fake repository'
                New-Item -ItemType Directory -Path $outside | Out-Null
                [IO.File]::WriteAllText((Join-Path $outside 'Devilution.Protocol.csproj'), '<Project />')
                $assetLink = Join-Path $source 'assets/linked-external'
                $referenceLink = Join-Path $repositoryRoot 'protocol/linked-external'
                try {
                    New-Item -ItemType Junction -Path $assetLink -Target $outside | Out-Null
                    Expect-Rejected { New-IsolatedGodotProject $source (Join-Path $work 'reparse-source/client') }
                    [IO.Directory]::Delete($assetLink, $false)
                    New-Item -ItemType Junction -Path $referenceLink -Target $outside | Out-Null
                    [IO.File]::WriteAllText($project, $projectText.Replace(
                        '../../protocol/Devilution.Protocol/Devilution.Protocol.csproj',
                        '../../protocol/linked-external/Devilution.Protocol.csproj'))
                    Expect-Rejected { New-IsolatedGodotProject $source (Join-Path $work 'reparse-reference/client') }
                    [IO.File]::WriteAllText($project, $projectText)
                } finally {
                    foreach ($link in @($assetLink, $referenceLink)) {
                        if (Test-Path -LiteralPath $link) { [IO.Directory]::Delete($link, $false) }
                    }
                }
                if (-not (Test-Path -LiteralPath (Join-Path $outside 'Devilution.Protocol.csproj'))) {
                    throw 'Removing the test junction removed its target.'
                }
                Assert-OriginalBytes
                Write-Output 'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-ProjectStageTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryBranchRetainsTerminalMarkerAndNeverRestartsAfterUnconfirmedExit()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Get-OwnedTerminalExitCode"],
            """
            $branch = @($launcherAst.FindAll({ param($node)
                $node -is [System.Management.Automation.Language.IfStatementAst] -and
                $node.Clauses[0].Item1.Extent.Text -ceq '$AcceptancePhase -eq ''Recovery''' -and
                $node.Clauses[0].Item2.Extent.Text -match 'Get-OwnedTerminalExitCode'
            }, $true))
            if ($branch.Count -ne 1) { throw 'Could not isolate the production Recovery branch.' }
            $branchText = $branch[0].Clauses[0].Item2.Extent.Text
            $runRecovery = [scriptblock]::Create($branchText.Substring(1, $branchText.Length - 2))
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-RecoveryBranch-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $actualPort = 6112; $PersistenceMode = 'cohort'; $contentHash = 'content'; $rulesetHash = 'rules'
            $startedServer = [pscustomobject]@{ Persistence = 'complete cohort' }
            $godotProcess = [pscustomobject]@{ Id = 400; StartTime = [DateTime]::UtcNow.AddSeconds(-5) }
            $startedClients = @([pscustomobject]@{ Process = $godotProcess })
            function Stop-OwnedLocalProcess($Process) {
                $script:events.Add('stop')
                if ($script:scenario -ne 'still-live') { $Process.HasExited = $true }
            }
            function Wait-SceneEvidence($name) {
                if ($name -eq 'recovery-ready.json') { return [pscustomobject]@{ Ready = $true; Sequence = 1 } }
                $script:events.Add('offline')
                $marker = [IO.File]::ReadAllText((Join-Path $SessionDirectory 'recovery-server-stopped.json')) | ConvertFrom-Json
                if (-not $marker.Stopped -or $marker.ProcessId -ne 300 -or $marker.ExitCode -ne -1 -or
                    $marker.StartedUtc -cne $oldHost.StartTime.ToUniversalTime().ToString('O') -or
                    [string]::IsNullOrWhiteSpace($marker.EndedUtc) -or $marker.Port -ne 6112 -or
                    -not $script:events.Contains('dispose')) { throw 'Terminal marker not retained before disposal/offline wait.' }
                if ($script:scenario -eq 'offline-failure') { throw 'Harmless offline wait failure.' }
                return [pscustomobject]@{ Passed = $true; Sequence = 1; OfflineSeconds = 1.5 }
            }
            function Start-SessionServer($port, $name) {
                $script:events.Add('restart')
                if ($script:scenario -eq 'restart-failure') { throw 'Harmless replacement startup failure.' }
                return [pscustomobject]@{ Port = $port; ContentHash = $contentHash; RulesetHash = $rulesetHash;
                    Persistence = 'complete cohort'; Process = [pscustomobject]@{ Id = 301; StartTime = [DateTime]::UtcNow } }
            }
            try {
                foreach ($script:scenario in @('success', 'still-live', 'missing-code', 'terminal-wait-failure',
                    'marker-write-failure', 'offline-failure', 'restart-failure')) {
                    $script:events = [Collections.Generic.List[string]]::new()
                    $SessionDirectory = Join-Path $work $script:scenario
                    New-Item -ItemType Directory -Path $SessionDirectory | Out-Null
                    $oldHost = [pscustomobject]@{ Id = 300; HasExited = $false; StartTime = [DateTime]::UtcNow.AddSeconds(-10);
                        ExitCode = -1; WaitResult = $true }
                    $oldHost | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value {
                        param($timeout) $script:events.Add('terminal-wait'); return $this.WaitResult
                    }
                    $oldHost | Add-Member -MemberType ScriptMethod -Name Dispose -Value { $script:events.Add('dispose') }
                    if ($script:scenario -eq 'missing-code') { $oldHost.ExitCode = $null }
                    if ($script:scenario -eq 'terminal-wait-failure') { $oldHost.WaitResult = $false }
                    $markerPath = Join-Path $SessionDirectory 'recovery-server-stopped.json'
                    if ($script:scenario -eq 'marker-write-failure') {
                        New-Item -ItemType Directory -Path $markerPath | Out-Null
                    }
                    $serverProcess = $oldHost; $rejected = $false
                    try { . $runRecovery } catch { $rejected = $true }
                    if ($rejected -ne ($script:scenario -ne 'success')) { throw "Unexpected Recovery result: $script:scenario" }
                    $afterStop = $script:scenario -in @('success', 'offline-failure', 'restart-failure')
                    if ((Test-Path -LiteralPath $markerPath -PathType Leaf) -ne $afterStop) { throw 'Terminal marker lifetime changed.' }
                    if ($script:events.Contains('restart') -ne ($script:scenario -in @('success', 'restart-failure'))) {
                        throw 'Replacement started before reliable termination/offline evidence.'
                    }
                    if ($afterStop) {
                        if (($script:events.GetRange(0, 4) -join ',') -cne 'stop,terminal-wait,dispose,offline') {
                            throw 'Recovery reordered native terminal observation, disposal or offline wait.'
                        }
                    } elseif ($script:events.Contains('dispose') -or $script:events.Contains('offline') -or
                        $null -eq $serverProcess -or -not [object]::ReferenceEquals($serverProcess, $oldHost)) {
                        throw 'Unconfirmed termination/evidence lost the host handle needed by outer cleanup.'
                    }
                    $aggregatePath = Join-Path $SessionDirectory 'recovery-processes.json'
                    if ((Test-Path -LiteralPath $aggregatePath -PathType Leaf) -ne ($script:scenario -eq 'success')) {
                        throw 'Failed Recovery emitted successful replacement evidence.'
                    }
                    if ($script:scenario -eq 'success') {
                        $record = [IO.File]::ReadAllText($aggregatePath) | ConvertFrom-Json
                        $marker = [IO.File]::ReadAllText($markerPath) | ConvertFrom-Json
                        if ($record.StoppedHostEndedUtc -cne $marker.EndedUtc -or $record.StoppedHostExitCode -ne $marker.ExitCode) {
                            throw 'Aggregate Recovery evidence lost the original native exit.'
                        }
                    }
                }
                'PASS'
            } finally {
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-RecoveryBranch-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void OwnedTerminalExitRequiresConfirmedExitAndPreservesNativeCode()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Stop-OwnedLocalProcess", "Get-OwnedTerminalExitCode"],
            """
            function Require-Rejection([scriptblock]$action) {
                $rejected = $false
                try { & $action | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Unconfirmed native exit was accepted.' }
            }
            Require-Rejection { Get-OwnedTerminalExitCode $null }
            foreach ($command in @('exit 23', 'Start-Sleep -Seconds 30')) {
                $start = [Diagnostics.ProcessStartInfo]::new()
                $start.FileName = (Get-Command powershell.exe -CommandType Application).Source
                $start.Arguments = '-NoProfile -NonInteractive -Command "' + $command + '"'
                $start.UseShellExecute = $false; $start.CreateNoWindow = $true
                $child = [Diagnostics.Process]::Start($start)
                try {
                    if ($command -eq 'exit 23') {
                        if (-not $child.WaitForExit(10000)) { throw 'Harmless child did not exit naturally.' }
                    } else {
                        Require-Rejection { Get-OwnedTerminalExitCode $child }
                        Stop-OwnedLocalProcess $child
                    }
                    $code = Get-OwnedTerminalExitCode $child
                    if (-not $child.HasExited -or $code -ne $child.ExitCode -or
                        ($command -eq 'exit 23' -and $code -ne 23)) {
                        throw 'Terminal native exit code was lost or changed.'
                    }
                    if ((Get-OwnedTerminalExitCode $child) -ne $code) { throw 'Repeated terminal observation changed exit code.' }
                } finally {
                    Stop-OwnedLocalProcess $child
                    $child.Dispose()
                }
            }
            foreach ($fixture in @(
                [pscustomobject]@{ HasExited = $true; ExitCode = $null; WaitResult = $true },
                [pscustomobject]@{ HasExited = $true; ExitCode = 23; WaitResult = $false })) {
                $fixture | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param($timeout) return $this.WaitResult }
                Require-Rejection { Get-OwnedTerminalExitCode $fixture }
            }
            'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    [Fact]
    public void GodotRedirectedOutputIsRetainedAfterSuccessFailureAndTimeout()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var script = BuildPowerShellHarness(
            ["Assert-WritableDirectory", "Write-ProcessEvidence", "Start-GodotChildProcess",
                "Stop-OwnedLocalProcess",
                "New-GodotProcess", "Write-GodotRedirectedOutput",
                "Complete-GodotProcess", "Stop-GodotProcess"],
            """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-RedirectTest-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            $captureRoot = $work
            $processEvidenceRoot = Join-Path $work 'processes'
            New-Item -ItemType Directory -Path $processEvidenceRoot | Out-Null
            $repositoryRoot = $work
            $GodotExecutable = (Get-Command powershell.exe -CommandType Application).Source
            $owned = @()
            try {
                $child = Join-Path $work 'harmless redirected child.ps1'
                [IO.File]::WriteAllText($child, @'
            param([int]$ExitCode, [int]$Delay)
            [Console]::Out.WriteLine('retained redirected stdout')
            [Console]::Error.WriteLine('retained redirected stderr')
            [IO.File]::WriteAllText($env:REDIRECT_READY_PATH, 'ready')
            Start-Sleep -Milliseconds $Delay
            exit $ExitCode
            '@)
                foreach ($scenario in @(
                    @{ Name = 'success'; Exit = 0; Delay = 1; Timeout = $false },
                    @{ Name = 'failure'; Exit = 23; Delay = 1; Timeout = $false },
                    @{ Name = 'timeout'; Exit = 0; Delay = 30000; Timeout = $true })) {
                    $name = $scenario.Name
                    $ready = Join-Path $work ($name + '.ready')
                    $stdout = Join-Path $work ($name + '.stdout.log')
                    $stderr = Join-Path $work ($name + '.stderr.log')
                    $arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -ExitCode {1} -Delay {2}' -f $child, $scenario.Exit, $scenario.Delay
                    $tracked = New-GodotProcess $name $arguments @{ REDIRECT_READY_PATH = $ready } $true $false $stdout $stderr
                    $owned += $tracked
                    $clock = [Diagnostics.Stopwatch]::StartNew()
                    while (-not (Test-Path -LiteralPath $ready)) {
                        if ($tracked.Process.HasExited -or $clock.Elapsed.TotalSeconds -gt 10) { throw 'Harmless child did not become ready.' }
                        Start-Sleep -Milliseconds 25
                    }
                    $rejected = $false
                    try {
                        $wait = if ($scenario.Timeout) { 10 } else { 10000 }
                        Complete-GodotProcess $tracked $wait $name
                    } catch { $rejected = $true }
                    if ($rejected -ne ($scenario.Timeout -or $scenario.Exit -ne 0)) {
                        throw "Incorrect process result for $name."
                    }
                    $primaryError = $tracked.Record.Error
                    Stop-GodotProcess $tracked
                    if ($tracked.Record.Error -cne $primaryError -or
                        [IO.File]::ReadAllText($stdout) -notmatch 'retained redirected stdout' -or
                        [IO.File]::ReadAllText($stderr) -notmatch 'retained redirected stderr') {
                        throw "Cleanup lost $name diagnostics or its primary error."
                    }
                    $record = [IO.File]::ReadAllText($tracked.EvidencePath) | ConvertFrom-Json
                    if ($null -eq $record.EndedUtc -or $null -eq $record.ExitCode) {
                        throw 'Terminated child evidence is incomplete.'
                    }
                    if (-not $scenario.Timeout -and $record.ExitCode -ne $scenario.Exit) {
                        throw 'Redirected capture changed the native exit code.'
                    }
                }
                Write-Output 'PASS'
            } finally {
                foreach ($tracked in $owned) {
                    try { Stop-GodotProcess $tracked } catch { }
                    $tracked.Process.Dispose()
                }
                $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                $resolvedWork = [IO.Path]::GetFullPath($work)
                if ($resolvedWork.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
                    [IO.Path]::GetFileName($resolvedWork) -match '^Godevilution-RedirectTest-[0-9a-f]{32}$') {
                    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            """);

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }
}
