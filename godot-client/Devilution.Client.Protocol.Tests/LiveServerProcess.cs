using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32.SafeHandles;

namespace Devilution.Client.Protocol.Tests;

/** Owns the actual host PID and its output, never a dotnet-run parent process. */
internal sealed class LiveServerProcess : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<BuildLocation, Lazy<Task<string>>> targetPaths = new();
    private readonly Process process;
    private readonly int id;
    private readonly TextReader outputReader;
    private readonly CancellationTokenSource drainLifetime = new();
    private readonly BoundedLineTail output = new();
    private readonly BoundedLineTail error = new();
    private readonly TaskCompletionSource<Identity> identity = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task outputPump;
    private readonly Task errorPump;
    private readonly SemaphoreSlim operatorCommand = new(1);
    private TaskCompletionSource<string>? performanceResponse;
    private bool disposed;
    private bool exitConfirmed;

    private LiveServerProcess(Process process, TextReader outputReader)
    {
        this.process = process;
        id = process.Id;
        this.outputReader = outputReader;
        outputPump = PumpOutputAsync();
        errorPump = PumpErrorAsync();
        // A caller-canceled startup can leave the underlying identity task pending.
        _ = identity.Task.ContinueWith(task => { _ = task.Exception; },
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    internal readonly record struct Identity(int Port, string ContentHash, string RulesetHash);
    private readonly record struct BuildLocation(string Repository, string Configuration, string Artifacts);

    public int Id => id;
    public bool HasExited => exitConfirmed || process.HasExited;
    internal SafeProcessHandle ProcessHandle => process.SafeHandle;
    public int ExitCode => process.ExitCode;
    public string Diagnostics => $"stdout:{Environment.NewLine}{output.Text}{Environment.NewLine}stderr:{Environment.NewLine}{error.Text}";

    public static string BuildConfiguration => typeof(LiveServerProcess).Assembly
        .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
        ?? throw new InvalidOperationException("The executing tests have no build configuration.");

    public static async Task<string> ResolveAssemblyAsync(
        string repositoryRoot,
        string configuration,
        string? artifactsPath,
        string? explicitAssembly,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string assembly;
        if (!string.IsNullOrWhiteSpace(explicitAssembly)) {
            assembly = Path.GetFullPath(explicitAssembly);
        } else {
            var location = new BuildLocation(Path.GetFullPath(repositoryRoot), configuration,
                string.IsNullOrWhiteSpace(artifactsPath) ? string.Empty : Path.GetFullPath(artifactsPath));
            assembly = await targetPaths.GetOrAdd(location, key => new Lazy<Task<string>>(
                () => QueryTargetPathAsync(key))).Value.WaitAsync(cancellationToken);
        }
        if (!File.Exists(assembly) || !File.Exists(Path.ChangeExtension(assembly, ".runtimeconfig.json")))
            throw new FileNotFoundException("Build the selected authoritative server before running live tests.", assembly);
        return assembly;
    }

    public static ProcessStartInfo CreateStartInfo(string repositoryRoot, string assembly)
    {
        var info = new ProcessStartInfo {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add(assembly);
        return info;
    }

    public static LiveServerProcess Start(ProcessStartInfo startInfo,
        Func<StreamReader, TextReader>? decorateOutput = null)
    {
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the authoritative server.");
        TextReader? reader = null;
        try {
            reader = decorateOutput?.Invoke(process.StandardOutput) ?? process.StandardOutput;
            return new LiveServerProcess(process, reader);
        } catch (Exception primary) {
            primary.Data["LiveServerProcessId"] = process.Id;
            var failures = new List<Exception>();
            var confirmed = false;
            try {
                if (!process.HasExited) {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                }
            } catch (Exception failure) { failures.Add(failure); }
            try {
                if (!process.WaitForExit(10_000))
                    throw new TimeoutException("The host failed to confirm exit after process-owner construction failed.");
                confirmed = true;
            } catch (Exception failure) { failures.Add(failure); }
            primary.Data["LiveServerConstructionExitConfirmed"] = confirmed;
            if (confirmed) {
                try { reader?.Dispose(); } catch (Exception failure) { failures.Add(failure); }
                try { process.Dispose(); } catch (Exception failure) { failures.Add(failure); }
            }
            if (failures.Count != 0)
                primary.Data["LiveServerConstructionCleanupFailure"] = new AggregateException(failures);
            throw;
        }
    }

    public async Task<Identity> WaitForIdentityAsync(CancellationToken cancellationToken)
        => await identity.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

    public async Task StartPerformanceRecordingAsync(CancellationToken cancellationToken)
    {
        await operatorCommand.WaitAsync(cancellationToken);
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        try {
            Volatile.Write(ref performanceResponse, response);
            await process.StandardInput.WriteLineAsync("profile-start");
            await process.StandardInput.FlushAsync(cancellationToken);
            var line = await response.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            if (line != "Performance recording started.")
                throw new InvalidOperationException(line);
        } finally {
            Volatile.Write(ref performanceResponse, null);
            operatorCommand.Release();
        }
    }

    public async Task QuitAsync(CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteLineAsync("quit");
        await process.StandardInput.FlushAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await Task.WhenAll(outputPump, errorPump).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        var failures = new List<Exception>();
        try {
            if (!process.HasExited) {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
        } catch (Exception failure) { failures.Add(failure); }
        try {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            exitConfirmed = true;
        } catch (Exception failure) { failures.Add(failure); }
        if (!exitConfirmed)
            throw new AggregateException("The owned live host did not confirm exit; retain its handles and save root.", failures);

        // A failed pump must not strand handles after confirmed host termination.
        try {
            await Task.WhenAll(outputPump, errorPump).WaitAsync(TimeSpan.FromSeconds(5));
        } catch (Exception failure) {
            failures.Add(failure);
            drainLifetime.Cancel();
            try { await Task.WhenAll(outputPump, errorPump).WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        }
        disposed = true;
        try { outputReader.Dispose(); } catch (Exception failure) { failures.Add(failure); }
        try { drainLifetime.Dispose(); } catch (Exception failure) { failures.Add(failure); }
        try { operatorCommand.Dispose(); } catch (Exception failure) { failures.Add(failure); }
        try { process.Dispose(); } catch (Exception failure) { failures.Add(failure); }
        if (failures.Count != 0)
            throw new AggregateException("Live host output/resource cleanup failed after confirmed exit.", failures);
    }

    private async Task PumpOutputAsync()
    {
        int? port = null;
        string? content = null;
        string? ruleset = null;
        try {
            while (await outputReader.ReadLineAsync(drainLifetime.Token) is { } line) {
                output.Add(line);
                if (line.Contains("listening on", StringComparison.Ordinal)
                    && int.TryParse(line[(line.LastIndexOf(':') + 1)..], out var parsedPort))
                    port = parsedPort;
                if (line.StartsWith("Content manifest: ", StringComparison.Ordinal))
                    content = line["Content manifest: ".Length..];
                if (line.StartsWith("Ruleset identity: ", StringComparison.Ordinal))
                    ruleset = line["Ruleset identity: ".Length..];
                if (port.HasValue && content is not null && ruleset is not null)
                    identity.TrySetResult(new Identity(port.Value, content, ruleset));
                if (line is "Performance recording started." or "Performance recording unavailable or already started.")
                    Volatile.Read(ref performanceResponse)?.TrySetResult(line);
            }
            identity.TrySetException(new InvalidOperationException("The authoritative server closed output before publishing its identity."));
            Volatile.Read(ref performanceResponse)?.TrySetException(new EndOfStreamException("Server exited before starting performance recording."));
        } catch (Exception failure) {
            identity.TrySetException(failure);
            Volatile.Read(ref performanceResponse)?.TrySetException(failure);
            throw;
        }
    }

    private async Task PumpErrorAsync()
    {
        while (await process.StandardError.ReadLineAsync(drainLifetime.Token) is { } line)
            error.Add(line);
    }

    private static async Task<string> QueryTargetPathAsync(BuildLocation location)
    {
        var info = new ProcessStartInfo {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = location.Repository,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("msbuild");
        info.ArgumentList.Add(Path.Combine(location.Repository, "server", "src", "Devilution.Server", "Devilution.Server.csproj"));
        info.ArgumentList.Add("-nologo");
        info.ArgumentList.Add("-getProperty:TargetPath");
        info.ArgumentList.Add("-p:Configuration=" + location.Configuration);
        if (location.Artifacts.Length != 0)
            info.ArgumentList.Add("-p:ArtifactsPath=" + location.Artifacts);
        using var query = Process.Start(info) ?? throw new InvalidOperationException("Could not evaluate the server target path.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var stdout = query.StandardOutput.ReadToEndAsync(deadline.Token);
        var stderr = query.StandardError.ReadToEndAsync(deadline.Token);
        try {
            await query.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(stdout, stderr);
            if (query.ExitCode != 0)
                throw new InvalidOperationException($"Server TargetPath evaluation failed: {await stderr}{await stdout}");
            return Path.GetFullPath((await stdout).Trim());
        } catch (Exception failure) {
            try {
                if (!query.HasExited)
                    query.Kill(entireProcessTree: true);
                await query.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            } catch (Exception cleanup) {
                failure.Data["TargetPathQueryCleanupFailure"] = cleanup;
            }
            // Observe read cancellation independently of the caller's startup token.
            deadline.Cancel();
            try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            throw;
        }
    }

    internal sealed class BoundedLineTail
    {
        private readonly Queue<string> lines = new();
        public void Add(string line)
        {
            lock (lines) {
                if (lines.Count == 16)
                    lines.Dequeue();
                lines.Enqueue(line.Length <= 1_024 ? line : line[..1_024]);
            }
        }
        public string Text { get { lock (lines) return string.Join(Environment.NewLine, lines); } }
    }
}
