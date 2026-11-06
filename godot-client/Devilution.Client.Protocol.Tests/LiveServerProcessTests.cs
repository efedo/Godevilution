using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveServerProcessTests
{
    [Fact]
    public void DirectLaunchUsesOneSelectedAssemblyWithoutDotnetRun()
    {
        var assembly = Path.Combine(Path.GetTempPath(), "selected", "Devilution.Server.dll");
        var info = LiveServerProcess.CreateStartInfo(Path.GetTempPath(), assembly);
        Assert.Equal([assembly, "--tick-rate", "200", "--skip-floor-entry-warmup", "true"], info.ArgumentList);
        Assert.True(info.CreateNoWindow);
        Assert.True(info.RedirectStandardOutput && info.RedirectStandardError && info.RedirectStandardInput);
        Assert.False(info.UseShellExecute);
        Assert.DoesNotContain("run", info.ArgumentList);
    }

    [Fact]
    public void FastStartupOverridesOnlyTheChildAndProductionBenchmarkCanOptOut()
    {
        var original = Environment.GetEnvironmentVariable("DOTNET_TC_QuickJit");
        var fast = LiveServerProcess.CreateStartInfo(Path.GetTempPath(), "selected.dll");
        Assert.Equal("1", fast.Environment["DOTNET_TC_QuickJit"]);
        Assert.Equal(original, Environment.GetEnvironmentVariable("DOTNET_TC_QuickJit"));
        var production = LiveServerProcess.CreateStartInfo(Path.GetTempPath(), "selected.dll", tickRateHz: 20, fastStartup: false);
        Assert.Equal(["selected.dll", "--tick-rate", "20"], production.ArgumentList);
        production.Environment.TryGetValue("DOTNET_TC_QuickJit", out var productionOverride);
        Assert.Equal(original, productionOverride);
    }

    [Fact]
    public async Task ExplicitMissingAssemblyRefusesRatherThanFallingBack()
    {
        var missing = Path.Combine(Path.GetTempPath(), "Godevilution-Missing-" + Guid.NewGuid().ToString("N"), "Devilution.Server.dll");
        var failure = await Assert.ThrowsAsync<FileNotFoundException>(() => LiveServerProcess.ResolveAssemblyAsync(
            Path.GetTempPath(), "Debug", null, missing, TestContext.Current.CancellationToken));
        Assert.Equal(missing, failure.FileName);
    }

    [Fact]
    public async Task CanceledResolutionRefusesBeforeStartingAnEvaluation()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LiveServerProcess.ResolveAssemblyAsync(
            Path.GetTempPath(), "Debug", null, null, canceled.Token));
    }

    [Fact]
    public void DiagnosticTailIsBoundedByLineCountAndLength()
    {
        var tail = new LiveServerProcess.BoundedLineTail();
        for (var index = 0; index < 100; index++)
            tail.Add(index + ":" + new string('x', 2_000));
        var lines = tail.Text.Split(Environment.NewLine);
        Assert.Equal(16, lines.Length);
        Assert.StartsWith("84:", lines[0]);
        Assert.StartsWith("99:", lines[^1]);
        Assert.All(lines, line => Assert.Equal(1_024, line.Length));
    }
}
