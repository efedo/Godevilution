using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{

    private static string BuildPowerShellHarness(
        string[] functionNames,
        string body,
        string relativeLauncherPath = "godot-client/run-local-client.ps1")
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
        lines.Add(body.Trim());
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string RunPowerShell(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"godevilution-launcher-test-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start PowerShell.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                Assert.Fail("PowerShell behavioral harness timed out.");
            }
            var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            Assert.True(process.ExitCode == 0, $"PowerShell behavioral harness failed ({process.ExitCode}):{Environment.NewLine}{output}");
            return output;
        }
        finally
        {
            if (File.Exists(scriptPath))
                File.Delete(scriptPath);
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
