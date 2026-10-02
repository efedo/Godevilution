using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

/** Exercises actual launcher guards/evidence validators, without starting Godot. */
public sealed class MultiplayerRecoveryLauncherTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public async Task ProcessEvidenceRequiresTerminalHostAndExactTwoClientIdentities()
    {
        var output = await RunHarness("""
            $driver = Join-Path $repositoryRoot 'tools/test-godot-session.ps1'
            $tokens = $null; $errors = $null
            $ast = [Management.Automation.Language.Parser]::ParseFile($driver, [ref]$tokens, [ref]$errors)
            if ($errors.Count) { throw 'The actual driver did not parse.' }
            $functions = @($ast.FindAll({ param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -ceq 'Assert-MultiplayerRecoveryProcessEvidence'
            }, $true))
            if ($functions.Count -ne 1) { throw 'Expected exactly one actual evidence validator.' }
            . ([scriptblock]::Create($functions[0].Extent.Text))

            function New-Evidence {
                return ('{"PersistenceMode":"cohort","HostPersistence":"complete cohort",' +
                    '"StoppedProcessId":101,"InitialHostProcessId":101,"RestartedProcessId":102,' +
                    '"StoppedHostStartedUtc":"2026-10-02T12:00:00Z","InitialHostStartedUtc":"2026-10-02T12:00:00Z",' +
                    '"StoppedHostEndedUtc":"2026-10-02T12:00:02Z","RestartedHostStartedUtc":"2026-10-02T12:00:05Z",' +
                    '"StoppedHostExitCode":-1,' +
                    '"Clients":[{"ProcessId":201,"StartedUtc":"2026-10-02T12:00:01Z"},{"ProcessId":202,"StartedUtc":"2026-10-02T12:00:01Z"}],' +
                    '"ExistingClients":[{"ProcessId":201,"StartedUtc":"2026-10-02T12:00:01Z"},{"ProcessId":202,"StartedUtc":"2026-10-02T12:00:01Z"}],' +
                    '"Ready":[{"Slot":1},{"Slot":2}],"Offline":[{"Slot":1},{"Slot":2}]}') | ConvertFrom-Json
            }
            Assert-MultiplayerRecoveryProcessEvidence (New-Evidence)
            $cases = @(
                @{ Name = 'legacy'; Change = { param($e) $e.PersistenceMode = 'legacy' } },
                @{ Name = 'wrong host mode'; Change = { param($e) $e.HostPersistence = 'legacy per-session' } },
                @{ Name = 'missing terminal exit'; Change = { param($e) $e.StoppedHostExitCode = $null } },
                @{ Name = 'invalid old host'; Change = { param($e) $e.StoppedProcessId = 0; $e.InitialHostProcessId = 0 } },
                @{ Name = 'invalid new host'; Change = { param($e) $e.RestartedProcessId = 0 } },
                @{ Name = 'different initial host'; Change = { param($e) $e.InitialHostProcessId = 103 } },
                @{ Name = 'different initial start'; Change = { param($e) $e.InitialHostStartedUtc = '2026-10-02T11:00:00Z' } },
                @{ Name = 'same host'; Change = { param($e) $e.RestartedProcessId = 101; $e.RestartedHostStartedUtc = $e.StoppedHostStartedUtc } },
                @{ Name = 'overlapping host'; Change = { param($e) $e.RestartedHostStartedUtc = $e.StoppedHostEndedUtc } },
                @{ Name = 'bad terminal time'; Change = { param($e) $e.StoppedHostEndedUtc = 'invalid' } },
                @{ Name = 'missing client'; Change = { param($e) $e.Clients = @($e.Clients[0]) } },
                @{ Name = 'extra client'; Change = { param($e) $e.ExistingClients += $e.ExistingClients[0] } },
                @{ Name = 'duplicate initial client'; Change = { param($e) $e.Clients[1] = $e.Clients[0] } },
                @{ Name = 'duplicate retained client'; Change = { param($e) $e.ExistingClients[1] = $e.ExistingClients[0] } },
                @{ Name = 'replacement client'; Change = { param($e) $e.ExistingClients[1].ProcessId = 203 } },
                @{ Name = 'reused client pid'; Change = { param($e) $e.ExistingClients[1].StartedUtc = '2026-10-02T12:00:06Z' } },
                @{ Name = 'invalid client'; Change = { param($e) $e.Clients[0].ProcessId = 0 } },
                @{ Name = 'missing client time'; Change = { param($e) $e.Clients[0].StartedUtc = '' } },
                @{ Name = 'bad client time'; Change = { param($e) $e.ExistingClients[0].StartedUtc = 'invalid' } },
                @{ Name = 'missing ready barrier'; Change = { param($e) $e.Ready = @($e.Ready[0]) } },
                @{ Name = 'missing offline barrier'; Change = { param($e) $e.Offline = @($e.Offline[0]) } }
            )
            foreach ($case in $cases) {
                $evidence = New-Evidence
                & $case.Change $evidence
                $rejected = $false
                try { Assert-MultiplayerRecoveryProcessEvidence $evidence } catch { $rejected = $true }
                if (-not $rejected) { throw ('False acceptance: ' + $case.Name) }
            }
            Write-Output 'PASS'
            """);
        Assert.Contains("PASS", output, StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public async Task RecoveryGuardPreservesLegacyDefaultsAndRejectsMixedProfiles()
    {
        var output = await RunHarness("""
            $text = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'tools/test-godot-session.ps1'))
            $end = $text.IndexOf('$repositoryRoot = ', [StringComparison]::Ordinal)
            if ($end -le 0) { throw 'Cannot isolate the actual argument guards.' }
            $guards = [scriptblock]::Create($text.Substring(0, $end) + "`r`n'GUARDS-PASSED'")
            $cases = @(
                @{ Name = 'legacy default'; Arguments = @{}; Allowed = $true },
                @{ Name = 'legacy multiplayer'; Arguments = @{ Multiplayer = $true }; Allowed = $true },
                @{ Name = 'cohort multiplayer'; Arguments = @{ Multiplayer = $true; PersistenceMode = 'cohort' }; Allowed = $true },
                @{ Name = 'cohort recovery'; Arguments = @{ MultiplayerRecovery = $true; PersistenceMode = 'cohort' }; Allowed = $true },
                @{ Name = 'legacy recovery'; Arguments = @{ MultiplayerRecovery = $true }; Allowed = $false },
                @{ Name = 'journey mixture'; Arguments = @{ MultiplayerRecovery = $true; PersistenceMode = 'cohort'; Journey = $true }; Allowed = $false },
                @{ Name = 'town mixture'; Arguments = @{ MultiplayerRecovery = $true; PersistenceMode = 'cohort'; TownLoop = $true }; Allowed = $false },
                @{ Name = 'lifecycle mixture'; Arguments = @{ MultiplayerRecovery = $true; PersistenceMode = 'cohort'; Lifecycle = $true }; Allowed = $false },
                @{ Name = 'cathedral mixture'; Arguments = @{ MultiplayerRecovery = $true; PersistenceMode = 'cohort'; CathedralProfileDirectory = 'not-opened' }; Allowed = $false },
                @{ Name = 'depth mixture'; Arguments = @{ MultiplayerRecovery = $true; PersistenceMode = 'cohort'; CathedralDepthProfileDirectory = 'not-opened' }; Allowed = $false }
            )
            foreach ($case in $cases) {
                $arguments = $case.Arguments
                $allowed = $false
                try { $allowed = @(& $guards @arguments) -contains 'GUARDS-PASSED' } catch {
                    if ($case.Allowed) { throw }
                }
                if ($allowed -ne $case.Allowed) { throw ('Unexpected guard result: ' + $case.Name) }
            }
            Write-Output 'PASS'
            """);
        Assert.Contains("PASS", output, StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public async Task FinalCommandRequiresExactAcceptedCanonicalOwnerReceipt()
    {
        var output = await RunHarness("""
            $tokens = $null; $errors = $null
            $ast = [Management.Automation.Language.Parser]::ParseFile(
                (Join-Path $repositoryRoot 'godot/run-local-client.ps1'), [ref]$tokens, [ref]$errors)
            if ($errors.Count) { throw 'The actual launcher did not parse.' }
            foreach ($name in @('Test-CohortJsonInteger', 'Assert-CohortRecoveryReceipt')) {
                $functions = @($ast.FindAll({ param($node)
                    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name
                }, $true))
                if ($functions.Count -ne 1) { throw ('Expected exactly one actual helper: ' + $name) }
                . ([scriptblock]::Create($functions[0].Extent.Text))
            }
            function New-Inputs {
                return '{"Owner":{"EntityId":42,"CommandReceipts":[{"ClientSequence":7,"ServerReceiptSequence":21,"AppliedTick":17,"Status":1,"RejectReason":0}]},"Final":{"Passed":true,"EntityId":42,"Sequence":7,"ReceiptSequence":21,"AppliedTick":17}}' | ConvertFrom-Json
            }
            $valid = New-Inputs
            Assert-CohortRecoveryReceipt $valid.Owner $valid.Final
            $cases = @(
                @{ Name = 'wrong owner'; Change = { param($e) $e.Final.EntityId = 43 } },
                @{ Name = 'old sequence'; Change = { param($e) $e.Final.Sequence = 6 } },
                @{ Name = 'old receipt'; Change = { param($e) $e.Final.ReceiptSequence = 20 } },
                @{ Name = 'wrong applied tick'; Change = { param($e) $e.Final.AppliedTick = 16 } },
                @{ Name = 'empty ledger'; Change = { param($e) $e.Owner.CommandReceipts = @() } },
                @{ Name = 'duplicate ledger'; Change = { param($e) $e.Owner.CommandReceipts += $e.Owner.CommandReceipts[0] } },
                @{ Name = 'rejected ledger'; Change = { param($e) $e.Owner.CommandReceipts[0].Status = 2 } },
                @{ Name = 'rescheduled ledger'; Change = { param($e) $e.Owner.CommandReceipts[0].Status = 3 } },
                @{ Name = 'duplicate instead of original'; Change = { param($e) $e.Owner.CommandReceipts[0].Status = 4 } },
                @{ Name = 'rejection reason'; Change = { param($e) $e.Owner.CommandReceipts[0].RejectReason = 1 } },
                @{ Name = 'failed scene'; Change = { param($e) $e.Final.Passed = $false } },
                @{ Name = 'string success'; Change = { param($e) $e.Final.Passed = 'true' } },
                @{ Name = 'zero sequence'; Change = { param($e) $e.Final.Sequence = 0 } },
                @{ Name = 'string sequence'; Change = { param($e) $e.Final.Sequence = '7' } },
                @{ Name = 'fractional tick'; Change = { param($e) $e.Final.AppliedTick = 17.5 } }
            )
            foreach ($case in $cases) {
                $inputs = New-Inputs
                & $case.Change $inputs
                $rejected = $false
                try { Assert-CohortRecoveryReceipt $inputs.Owner $inputs.Final } catch { $rejected = $true }
                if (-not $rejected) { throw ('False canonical receipt acceptance: ' + $case.Name) }
            }
            Write-Output 'PASS'
            """);
        Assert.Contains("PASS", output, StringComparison.Ordinal);
    }

    private static async Task<string> RunHarness(string body)
    {
        var root = FindRepositoryRoot();
        var script = "$ErrorActionPreference = 'Stop'\r\n$repositoryRoot = '"
            + root.Replace("'", "''", StringComparison.Ordinal) + "'\r\n" + body;
        var start = new ProcessStartInfo("powershell.exe") {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand",
                     Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
            start.ArgumentList.Add(argument);
        using var child = new Process { StartInfo = start };
        Assert.True(child.Start());
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        try {
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var stdout = await output.WaitAsync(TimeSpan.FromSeconds(2));
            var stderr = await error.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(child.ExitCode == 0, $"Launcher helper failed ({child.ExitCode}): {stderr}\n{stdout}");
            return stdout;
        } catch (Exception primary) {
            try {
                if (!child.HasExited)
                    child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            } catch (Exception cleanup) {
                primary.Data["OwnedChildCleanupError"] = cleanup.Message;
            }
            throw;
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "tools", "test-godot-session.ps1"))
                    && File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the actual recovery launcher.");
    }
}
