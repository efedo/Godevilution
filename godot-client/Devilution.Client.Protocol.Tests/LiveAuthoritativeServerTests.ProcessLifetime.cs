using System.Diagnostics;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests : IAsyncLifetime
{
    private readonly List<StartedServer> ownedServers = [];
    private readonly List<Exception> cleanupFailures = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        foreach (var server in ownedServers)
            server.Dispose();
        if (cleanupFailures.Count != 0)
            throw new AggregateException("Live authoritative test cleanup failed.", cleanupFailures);
        return ValueTask.CompletedTask;
    }

    private void CleanupLocalRoot(LiveTestSaveRoot lease)
    {
        try { lease.Delete(); }
        catch (Exception failure) { cleanupFailures.Add(failure); }
    }

    [Fact]
    public async Task LiveHostFixtureTracksActualHostAndReleasesSaveRootOnAbruptStop()
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken);
        var saveRoot = server.SaveRoot;
        using var observedHost = Process.GetProcessById(server.ProcessId);
        await using (var client = new AuthoritativeClient(CreateOptions(server, "owned-host-stop", requestedClassId: 1))) {
            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var snapshot = (await WaitForMessageAsync(client, message => message.Snapshot is not null)).Snapshot!;
            var sequence = client.Queue(new Devilution.Protocol.V1.Command {
                SetFriendlyModeRequested = new Devilution.Protocol.V1.SetFriendlyModeRequested { FriendlyMode = false },
            }, client.SuggestedCommandTick(snapshot.Tick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(Devilution.Protocol.V1.CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);
        }
        Assert.NotEmpty(Directory.GetFiles(saveRoot, "*.commands.json"));
        server.Dispose();
        Assert.True(observedHost.HasExited);
        Assert.False(Directory.Exists(saveRoot));
    }

    [Fact]
    public async Task LiveHostFixtureDrainsOperatorRepliesAndPreservesBorrowedSaveRoot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Godevilution-Owned-Host-" + Guid.NewGuid().ToString("N"));
        var lease = LiveTestSaveRoot.Register(directory);
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "performance.json");
        var saveRoot = Path.Combine(directory, "save");
        try {
            using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken,
                saveRootOverride: saveRoot, deleteSaveRootOnDispose: false, performanceOutput: output);
            await server.StartPerformanceRecordingAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.StartPerformanceRecordingAsync(TestContext.Current.CancellationToken));
            await server.QuitAsync(TestContext.Current.CancellationToken);
            server.Dispose();
            Assert.True(Directory.Exists(saveRoot));
            Assert.True(File.Exists(output));
        } finally {
            // Only this GUID fixture root is owned; a failed host join retains it.
            if (cleanupFailures.Count == 0)
                CleanupLocalRoot(lease);
        }
    }

    [Fact]
    public async Task LiveHostStartupRefusalPreservesBorrowedRootAndDiagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Godevilution-Startup-Refusal-" + Guid.NewGuid().ToString("N"));
        var lease = LiveTestSaveRoot.Register(directory);
        var saveRoot = Path.Combine(directory, "save");
        var marker = Path.Combine(saveRoot, "borrowed-marker.txt");
        var existingOutput = Path.Combine(directory, "existing-output.json");
        try {
            Directory.CreateDirectory(saveRoot);
            await File.WriteAllTextAsync(marker, "preserve", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(existingOutput, "preserve-output", TestContext.Current.CancellationToken);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => StartedServer.StartAsync(
                TestContext.Current.CancellationToken, saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: true, performanceOutput: existingOutput));
            Assert.Equal("preserve", await File.ReadAllTextAsync(marker, TestContext.Current.CancellationToken));
            Assert.Equal("preserve-output", await File.ReadAllTextAsync(existingOutput, TestContext.Current.CancellationToken));
            Assert.Contains("startup failed", Assert.IsType<string>(failure.Data["LiveServerDiagnostics"]), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(cleanupFailures);
        } finally {
            if (cleanupFailures.Count == 0)
                CleanupLocalRoot(lease);
        }
    }

    [Fact]
    public async Task AlreadyCanceledStartupDoesNotStartHostOrTouchBorrowedRoot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Godevilution-Canceled-Startup-" + Guid.NewGuid().ToString("N"));
        var lease = LiveTestSaveRoot.Register(directory);
        var marker = Path.Combine(directory, "marker.txt");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        try {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(marker, "preserve", TestContext.Current.CancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StartedServer.StartAsync(
                canceled.Token, saveRootOverride: directory));
            Assert.Empty(ownedServers);
            Assert.Empty(cleanupFailures);
            Assert.Equal([marker], Directory.GetFiles(directory));
            Assert.Equal("preserve", await File.ReadAllTextAsync(marker, TestContext.Current.CancellationToken));
        } finally { CleanupLocalRoot(lease); }
    }

    [Fact]
    public async Task FaultedOutputPumpStillReleasesConfirmedHostHandles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Godevilution-Faulted-Pump-" + Guid.NewGuid().ToString("N"));
        var lease = LiveTestSaveRoot.Register(directory);
        LiveServerProcess? process = null;
        Process? observedHost = null;
        try {
            var assembly = await LiveServerProcess.ResolveAssemblyAsync(FindRepositoryRoot(), ResolveBuildConfiguration(),
                Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH"),
                Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY"), TestContext.Current.CancellationToken);
            Directory.CreateDirectory(directory);
            var info = LiveServerProcess.CreateStartInfo(FindRepositoryRoot(), assembly);
            info.ArgumentList.Add("--port");
            info.ArgumentList.Add("0");
            info.ArgumentList.Add("--save-root");
            info.ArgumentList.Add(directory);
            var injected = new IOException("controlled-output-pump-failure");
            process = LiveServerProcess.Start(info, reader => new FailAfterIdentityReader(reader, injected));
            // All post-start probes are inside the owned process cleanup scope.
            var handle = process.ProcessHandle;
            observedHost = Process.GetProcessById(process.Id);
            await process.WaitForIdentityAsync(TestContext.Current.CancellationToken);
            var failure = await Assert.ThrowsAsync<AggregateException>(() => process.DisposeAsync().AsTask());
            Assert.Contains(failure.InnerExceptions, exception => ReferenceEquals(exception, injected));
            Assert.True(observedHost.HasExited);
            Assert.True(handle.IsClosed);
        } finally {
            var released = process is null;
            if (process is not null) {
                try {
                    await process.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
                    released = process.HasExited;
                } catch (Exception failure) { cleanupFailures.Add(failure); }
            }
            try { observedHost?.Dispose(); }
            catch (Exception failure) { cleanupFailures.Add(failure); }
            if (released)
                CleanupLocalRoot(lease);
        }
    }

    [Fact]
    public async Task ThrowingOutputDecoratorDoesNotStrandStartedHost()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Godevilution-Factory-Refusal-" + Guid.NewGuid().ToString("N"));
        var lease = LiveTestSaveRoot.Register(directory);
        var canDelete = true;
        try {
            var assembly = await LiveServerProcess.ResolveAssemblyAsync(FindRepositoryRoot(), ResolveBuildConfiguration(),
                Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH"),
                Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY"), TestContext.Current.CancellationToken);
            var info = LiveServerProcess.CreateStartInfo(FindRepositoryRoot(), assembly);
            info.ArgumentList.Add("--port");
            info.ArgumentList.Add("0");
            info.ArgumentList.Add("--save-root");
            info.ArgumentList.Add(directory);
            var primary = new IOException("controlled-output-factory-refusal");
            var observed = Assert.Throws<IOException>(() => LiveServerProcess.Start(info, _ => throw primary));
            canDelete = observed.Data["LiveServerConstructionExitConfirmed"] is true;
            Assert.Same(primary, observed);
            Assert.True(canDelete);
            Assert.Null(observed.Data["LiveServerConstructionCleanupFailure"]);
            var id = Assert.IsType<int>(observed.Data["LiveServerProcessId"]);
            try {
                using var host = Process.GetProcessById(id);
                Assert.True(host.HasExited);
            } catch (ArgumentException) {
                // Windows has already removed the terminated PID from its process table.
            }
        } finally {
            if (canDelete)
                CleanupLocalRoot(lease);
        }
    }

    [Fact]
    public async Task CleanupFailureDoesNotMaskBodyFailureAndStillFailsLifecycle()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Skip("Requires Windows delete-sharing semantics for a controlled cleanup refusal.");
        var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken);
        var marker = Path.Combine(server.SaveRoot, "locked-cleanup-marker.txt");
        var lease = LiveTestSaveRoot.Register(server.SaveRoot);
        var primary = new InvalidOperationException("controlled-test-body-failure");
        Exception? expectedCleanup = null;
        try {
            using (var held = new FileStream(marker, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) {
                var observed = Assert.Throws<InvalidOperationException>((Action)(() => {
                    using (server) { throw primary; }
                }));
                Assert.Same(primary, observed);
                expectedCleanup = Assert.Single(cleanupFailures);
                Assert.IsType<IOException>(expectedCleanup);
                Assert.True(Directory.Exists(server.SaveRoot));
                var deferred = await Assert.ThrowsAsync<AggregateException>(() => DisposeAsync().AsTask());
                Assert.Same(expectedCleanup, Assert.Single(deferred.InnerExceptions));
            }
            lease.Delete();
            // Only the deliberately induced, fully verified cleanup error is consumed.
            Assert.True(cleanupFailures.Remove(expectedCleanup));
        } finally {
            if (server.HasExited)
                CleanupLocalRoot(lease);
        }
    }

    private sealed class FailAfterIdentityReader(StreamReader reader, Exception failure) : TextReader
    {
        private bool identitySeen;
        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            if (identitySeen)
                throw failure;
            var line = await reader.ReadLineAsync(cancellationToken);
            identitySeen = line?.StartsWith("Ruleset identity: ", StringComparison.Ordinal) == true;
            return line;
        }
        protected override void Dispose(bool disposing) { if (disposing) reader.Dispose(); }
    }
}
