using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class GodotRuntimeAssemblyLauncherTests
{
    [Fact]
    public void PerformanceRuntimeAssemblyGuardBindsFreshDebugImagesAndProjectsSafeScalars()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var repositoryRoot = FindRepositoryRoot();
        var fixtureId = Guid.NewGuid().ToString("N");
        var fixtureRoot = Path.Combine(Path.GetTempPath(), $"godevilution-runtime-assembly-{fixtureId}");
        Directory.CreateDirectory(fixtureRoot);
        var script = BuildPowerShellHarness(
            repositoryRoot,
            [
                "Get-GodotCanonicalLocationSha256",
                "Get-GodotRuntimeAssemblyModuleVersionId",
                "Get-GodotRuntimeAssemblyManifest",
                "Assert-GodotRuntimeAssemblyEvidence",
                "Select-PerformancePublicFields",
            ],
            $$"""
            $fixtureRoot = {{QuotePowerShell(fixtureRoot)}}
            $clientAssembly = {{QuotePowerShell(typeof(GodotRuntimeAssemblyLauncherTests).Assembly.Location)}}
            $protocolAssembly = {{QuotePowerShell(typeof(Devilution.Protocol.V1.Command).Assembly.Location)}}
            $runtimeDirectory = Join-Path $fixtureRoot '.godot/mono/temp/bin/Debug'
            New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
            Copy-Item -LiteralPath $clientAssembly -Destination (Join-Path $runtimeDirectory 'Devilution.Client.dll')
            Copy-Item -LiteralPath $protocolAssembly -Destination (Join-Path $runtimeDirectory 'Devilution.Client.Protocol.dll')
            $manifest = @(Get-GodotRuntimeAssemblyManifest $fixtureRoot)
            if ($manifest.Count -ne 2 -or @($manifest.Role | Sort-Object -Unique).Count -ne 2 -or
                @($manifest | Where-Object { $_.Configuration -cne 'Debug' -or $_.ImageBindingKind -cne 'StagedImageWithMatchingMvid' }).Count -ne 0) {
                throw 'The file-derived manifest did not describe exactly two staged Debug images.'
            }
            $disableOptimizations = [int][Enum]::Parse([type]'System.Diagnostics.DebuggableAttribute+DebuggingModes', 'DisableOptimizations')
            $observations = @($manifest | ForEach-Object {
                [pscustomobject]@{
                    Role = $_.Role
                    AssemblyName = $_.AssemblyName
                    AssemblyVersion = $_.AssemblyVersion
                    FullName = $_.FullName
                    ModuleVersionId = $_.ModuleVersionId
                    HasConfigurationAttribute = $true
                    Configuration = 'Debug'
                    Sha256 = $_.Sha256
                    FileName = $_.FileName
                    CanonicalLocationSha256 = $_.CanonicalLocationSha256
                    HasDebuggableAttribute = $true
                    DebuggableModes = $disableOptimizations
                    OptimizationsDisabled = $true
                    ImageBindingKind = 'StagedImageWithMatchingMvid'
                }
            })
            $validated = @(Assert-GodotRuntimeAssemblyEvidence $observations $manifest)
            if ($validated.Count -ne 2 -or $null -ne $validated[0].PSObject.Properties['Path'] -or
                $null -ne $validated[0].PSObject.Properties['Location']) {
                throw 'The validated public projection leaked a filesystem path.'
            }
            $projection = Select-PerformancePublicFields ([pscustomobject]@{ RuntimeAssemblies = $validated }) @('RuntimeAssemblies')
            if ($projection.RuntimeAssemblies.Count -ne 2 -or
                $null -ne $projection.RuntimeAssemblies[0].PSObject.Properties['Path']) {
                throw 'The performance configuration projection was not path-safe.'
            }

            function Assert-Rejected([string]$Name, [scriptblock]$Action) {
                try { & $Action } catch { return }
                throw "A mutated runtime assembly case was accepted: $Name"
            }
            function Copy-Observations {
                return ,@($observations | ForEach-Object { [pscustomobject]($_ | Select-Object *) })
            }
            Assert-Rejected 'missing observation' { Assert-GodotRuntimeAssemblyEvidence @($observations[0]) $manifest }
            Assert-Rejected 'extra observation' { Assert-GodotRuntimeAssemblyEvidence @($observations + $observations[0]) $manifest }
            $case = Copy-Observations; $case[0].Role = 'unknown'; Assert-Rejected 'unknown role' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[1].Role = $case[0].Role; Assert-Rejected 'duplicate role' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0] | Add-Member -NotePropertyName Location -NotePropertyValue 'private path'; Assert-Rejected 'extra field' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].PSObject.Properties.Remove('FullName'); Assert-Rejected 'missing field' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].Configuration = 'Release'; Assert-Rejected 'wrong configuration' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].HasConfigurationAttribute = $false; Assert-Rejected 'missing configuration attribute' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].HasDebuggableAttribute = $false; Assert-Rejected 'missing debuggable attribute' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].DebuggableModes = 0; Assert-Rejected 'optimization flag mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].DebuggableModes = '256'; Assert-Rejected 'coerced optimization flags' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].OptimizationsDisabled = $false; Assert-Rejected 'optimized debug image' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].ImageBindingKind = 'LoadedFile'; Assert-Rejected 'wrong image binding' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].AssemblyName = 'Other'; Assert-Rejected 'assembly name mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].AssemblyVersion = '0.0.0.0'; Assert-Rejected 'assembly version mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].FullName = 'Other, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'; Assert-Rejected 'full identity mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].ModuleVersionId = '00000000-0000-0000-0000-000000000000'; Assert-Rejected 'MVID mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].Sha256 = ('0' * 64); Assert-Rejected 'file hash mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].CanonicalLocationSha256 = ('0' * 64); Assert-Rejected 'location hash mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].FileName = 'other.dll'; Assert-Rejected 'file name mismatch' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $case = Copy-Observations; $case[0].ModuleVersionId = 'not-a-guid'; Assert-Rejected 'malformed MVID' { Assert-GodotRuntimeAssemblyEvidence $case $manifest }
            $badManifest = @($manifest | ForEach-Object { [pscustomobject]($_ | Select-Object *) }); $badManifest[1].Role = 'client'; Assert-Rejected 'duplicate expected role' { Assert-GodotRuntimeAssemblyEvidence $observations $badManifest }
            Remove-Item -LiteralPath (Join-Path $runtimeDirectory 'Devilution.Client.Protocol.dll') -Force
            Assert-Rejected 'missing staged image' { Get-GodotRuntimeAssemblyManifest $fixtureRoot }
            'PASS'
            """);

        var scriptPath = Path.Combine(fixtureRoot, "runtime-assembly-guard.ps1");
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var succeeded = false;
        var cleanupAttempted = false;
        Exception? primaryFailure = null;
        Process? process = null;
        Task<string>? stdout = null;
        Task<string>? stderr = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the PowerShell metadata harness.");
            stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            var runCompleted = process.WaitForExit(30_000);
            var completion = FinishPowerShellProcess(process, stdout, stderr, terminate: !runCompleted);
            cleanupAttempted = true;
            if (!completion.ProcessExited)
                throw new TimeoutException($"The PowerShell metadata harness did not exit within the bounded cleanup window. {completion.Output}");
            if (!completion.StreamsDrained)
                throw new TimeoutException($"The PowerShell metadata harness output did not drain within the shared cleanup window. Exit code {process.ExitCode}. {completion.Output}");
            if (!runCompleted)
                throw new TimeoutException($"The PowerShell metadata harness exceeded its 30-second run limit. Exit code {process.ExitCode}. {completion.Output}");
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"PowerShell metadata harness failed ({process.ExitCode}):{Environment.NewLine}{completion.Output}");
            var output = completion.Output;
            Assert.Contains("PASS", output, StringComparison.Ordinal);
            succeeded = true;
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            Exception? cleanupFailure = null;
            if (process is not null && !cleanupAttempted)
            {
                try
                {
                    var completion = FinishPowerShellProcess(process, stdout, stderr, terminate: true);
                    cleanupAttempted = true;
                    if (!completion.ProcessExited || !completion.StreamsDrained)
                        cleanupFailure = new InvalidOperationException($"The PowerShell metadata harness cleanup did not finish within five seconds. {completion.Output}");
                }
                catch (Exception exception)
                {
                    cleanupFailure = exception;
                }
            }
            try { process?.Dispose(); }
            catch (Exception exception) { cleanupFailure ??= exception; }

            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var resolvedTarget = Path.GetFullPath(fixtureRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var expectedTarget = Path.Combine(expectedParent, $"godevilution-runtime-assembly-{fixtureId}");
            if (!string.Equals(resolvedTarget, expectedTarget, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetDirectoryName(resolvedTarget), expectedParent, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(Path.GetFileName(resolvedTarget).Replace("godevilution-runtime-assembly-", string.Empty, StringComparison.Ordinal), "N", out _))
                cleanupFailure ??= new InvalidOperationException("The runtime assembly fixture cleanup target was not its owned GUID directory.");
            else if (succeeded && Directory.Exists(resolvedTarget))
            {
                try { Directory.Delete(resolvedTarget, recursive: true); }
                catch (Exception exception) { cleanupFailure ??= exception; }
            }
            if (cleanupFailure is not null && primaryFailure is null)
                throw cleanupFailure;
        }
    }

    private static (bool ProcessExited, bool StreamsDrained, string Output) FinishPowerShellProcess(
        Process process,
        Task<string>? stdout,
        Task<string>? stderr,
        bool terminate)
    {
        var budget = Stopwatch.StartNew();
        var terminationFailure = (string?)null;
        if (terminate)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) { terminationFailure = exception.GetType().Name; }
        }

        var processExited = false;
        try { processExited = process.HasExited || process.WaitForExit(RemainingMilliseconds(budget)); }
        catch (Exception) { }

        var streamsDrained = stdout is null && stderr is null;
        if (stdout is not null && stderr is not null)
        {
            try { streamsDrained = Task.WhenAll(stdout, stderr).Wait(RemainingMilliseconds(budget)); }
            catch (AggregateException) { streamsDrained = stdout.IsCompletedSuccessfully && stderr.IsCompletedSuccessfully; }
            catch (Exception) { streamsDrained = false; }
        }

        var output = new StringBuilder();
        AppendCompletedStream(output, "stdout", stdout);
        AppendCompletedStream(output, "stderr", stderr);
        if (!processExited)
            output.AppendLine("The owned PowerShell process did not exit before the cleanup deadline.");
        if (!streamsDrained)
            output.AppendLine("Redirected output streams did not drain before the shared cleanup deadline.");
        if (terminationFailure is not null)
            output.AppendLine($"The owned PowerShell process could not be terminated cleanly ({terminationFailure}).");
        return (processExited, streamsDrained, output.ToString());
    }

    private static int RemainingMilliseconds(Stopwatch budget)
        => Math.Max(0, 5_000 - (int)Math.Min(5_000, budget.ElapsedMilliseconds));

    private static void AppendCompletedStream(StringBuilder output, string name, Task<string>? stream)
    {
        if (stream is null || !stream.IsCompleted)
            return;
        try { output.AppendLine(stream.GetAwaiter().GetResult()); }
        catch (Exception exception) { output.AppendLine($"{name} read failed: {exception.Message}"); }
    }

    private static string BuildPowerShellHarness(string repositoryRoot, string[] functionNames, string body)
    {
        var launcherPath = Path.Combine(repositoryRoot, "godot-client", "run-local-client.ps1");
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
        lines.Add(body.Trim());
        return string.Join("\r\n", lines) + "\r\n";
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
