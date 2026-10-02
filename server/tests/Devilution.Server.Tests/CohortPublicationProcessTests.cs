using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Devilution.CohortPublicationFixture;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

[CollectionDefinition(CohortPublicationProcessCollection.CollectionName, DisableParallelization = true)]
public sealed class CohortPublicationProcessCollection
{
    public const string CollectionName = "Owned cohort publication processes";
}

/** Abruptly kills only a dedicated managed child at a reported durable boundary. */
[Collection(CohortPublicationProcessCollection.CollectionName)]
public sealed class CohortPublicationProcessTests
{
    private const string StageDirectoryName = ".authoritative-staging";
    private const string InitializingMarker = ".authoritative-cohort.initializing";
    private const string CommittedMarker = ".authoritative-cohort.committed";

    [Theory]
    [InlineData("seed", "BeforeStageWrite")]
    [InlineData("seed", "AfterPartialWriteBeforeFlush")]
    [InlineData("seed", "AfterFlushBeforeRename")]
    [InlineData("seed", "AfterCanonicalRename")]
    [InlineData("cohort", "BeforeStageWrite")]
    [InlineData("cohort", "AfterPartialWriteBeforeFlush")]
    [InlineData("cohort", "AfterFlushBeforeRename")]
    [InlineData("cohort", "AfterCanonicalRename")]
    [InlineData("cohort", "AfterMarkerCommit")]
    public async Task InterruptedInitializationRestartsWithoutPromotingOrDeletingStages(string source, string phase)
    {
        var root = NewRoot();
        var children = new List<OwnedChild>();
        var success = false;
        try {
            var port = ReserveLoopbackPort();
            var interrupted = StartChild("interrupt", root, source, phase, port);
            children.Add(interrupted);
            var boundaryLine = await interrupted.Process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            Assert.NotNull(boundaryLine);
            Assert.StartsWith("BOUNDARY:", boundaryLine);
            string stagedPath;
            using (var boundary = JsonDocument.Parse(boundaryLine!["BOUNDARY:".Length..])) {
                Assert.Equal(source, boundary.RootElement.GetProperty("Source").GetString());
                Assert.Equal(phase, boundary.RootElement.GetProperty("Phase").GetString());
                Assert.Equal(port, boundary.RootElement.GetProperty("Port").GetInt32());
                stagedPath = boundary.RootElement.GetProperty("StagePath").GetString()!;
            }
            Assert.False(interrupted.Process.HasExited);
            // Query listening sockets directly: a closed loopback port may
            // timeout rather than reject under the machine's network policy.
            Assert.DoesNotContain(
                IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(),
                endpoint => endpoint.Port == port);
            await interrupted.StopAsync();
            Assert.NotEqual(0, interrupted.Process.ExitCode);
            Assert.DoesNotContain("READY:", await interrupted.ReadRemainingOutputAsync());
            Assert.Empty(await interrupted.Error.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            if (phase is "BeforeStageWrite" or "AfterPartialWriteBeforeFlush" or "AfterFlushBeforeRename") {
                Assert.Equal(Path.Combine(root, StageDirectoryName), Path.GetDirectoryName(stagedPath));
                var stagedBytes = File.ReadAllBytes(stagedPath);
                if (phase == "BeforeStageWrite") {
                    Assert.Empty(stagedBytes);
                } else if (phase == "AfterPartialWriteBeforeFlush") {
                    Assert.NotEmpty(stagedBytes);
                    if (source == "seed")
                        Assert.True(stagedBytes.Length < "305419896".Length);
                    else
                        Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(stagedBytes));
                } else if (source == "seed") {
                    Assert.Equal("305419896", System.Text.Encoding.UTF8.GetString(stagedBytes));
                } else {
                    using var flushed = JsonDocument.Parse(stagedBytes);
                    Assert.Equal(1UL, flushed.RootElement.GetProperty("Generation").GetUInt64());
                    AssertFlushedIntegrity(flushed.RootElement);
                }
            }

            var seedPath = Path.Combine(root, AuthoritativeWorldSeedStore.FileName);
            var priorSeed = File.Exists(seedPath) ? File.ReadAllBytes(seedPath) : null;
            Assert.Equal(source == "cohort" || phase == "AfterCanonicalRename", priorSeed is not null);
            ulong? priorGeneration;
            using (var prior = new AuthoritativeCohortStore(root))
                priorGeneration = prior.Load()?.Generation;
            Assert.Equal(source == "cohort" && (phase is "AfterCanonicalRename" or "AfterMarkerCommit"), priorGeneration.HasValue);
            Assert.Equal(phase == "AfterMarkerCommit", File.Exists(Path.Combine(root, CommittedMarker)));
            Assert.Equal(phase != "AfterMarkerCommit", File.Exists(Path.Combine(root, InitializingMarker)));

            // Valid staging names do not confer authority, even for complete,
            // plausible seed bytes or a foreign checkpoint payload.
            var stageDirectory = Path.Combine(root, StageDirectoryName);
            File.WriteAllText(Path.Combine(stageDirectory, $"{AuthoritativeWorldSeedStore.FileName}.{Guid.NewGuid():N}.tmp"), "4294967295");
            File.WriteAllText(Path.Combine(stageDirectory, $"authoritative-cohort.json.{Guid.NewGuid():N}.tmp"),
                "foreign checkpoint must never be read or promoted");
            var retainedStages = StageHashes(stageDirectory);
            var recovered = StartChild("recover", root, source, "unused", ReserveLoopbackPort());
            children.Add(recovered);
            var readyLine = await recovered.Process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            Assert.NotNull(readyLine);
            Assert.StartsWith("READY:", readyLine);
            using (var ready = JsonDocument.Parse(readyLine!["READY:".Length..])) {
                Assert.Equal((priorGeneration ?? 0) + 1, ready.RootElement.GetProperty("Generation").GetUInt64());
                Assert.Equal(0, ready.RootElement.GetProperty("ParticipantCount").GetInt32());
                Assert.Equal(0x12345678U, ready.RootElement.GetProperty("Seed").GetUInt32());
            }
            await recovered.WaitForExitAsync();
            Assert.Equal(0, recovered.Process.ExitCode);
            Assert.Empty(await recovered.Error.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Empty(await recovered.ReadRemainingOutputAsync());
            Assert.True(File.Exists(Path.Combine(root, CommittedMarker)));
            Assert.Equal(0, new FileInfo(Path.Combine(root, CommittedMarker)).Length);
            Assert.False(File.Exists(Path.Combine(root, InitializingMarker)));
            AssertStageHashesEqual(retainedStages, StageHashes(stageDirectory));
            if (priorSeed is not null)
                Assert.Equal(priorSeed, File.ReadAllBytes(seedPath));
            using (var verified = new AuthoritativeCohortStore(root)) {
                var canonical = verified.Load();
                Assert.NotNull(canonical);
                Assert.True(canonical!.Generation >= (priorGeneration ?? 0) + 1);
                Assert.Empty(canonical.Participants);
            }
            success = true;
        } finally {
            await FinishOwnedChildrenAsync(children, root, success);
        }
    }

    [Fact]
    public async Task CommittedRootMissingCanonicalRefusesWithoutRegeneratingSeedOrPromotingStages()
    {
        var root = NewRoot();
        var children = new List<OwnedChild>();
        var success = false;
        try {
            var initial = StartChild("recover", root, "cohort", "unused", ReserveLoopbackPort());
            children.Add(initial);
            var line = await initial.Process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            Assert.StartsWith("READY:", line);
            await initial.WaitForExitAsync();
            Assert.Equal(0, initial.Process.ExitCode);
            Assert.Empty(await initial.Error.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            var seedPath = Path.Combine(root, AuthoritativeWorldSeedStore.FileName);
            var seed = File.ReadAllBytes(seedPath);
            var stageDirectory = Path.Combine(root, StageDirectoryName);
            var retainedPath = Path.Combine(stageDirectory, $"authoritative-cohort.json.{Guid.NewGuid():N}.tmp");
            File.Copy(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName), retainedPath);
            var retained = StageHashes(stageDirectory);
            // This is an explicitly owned disposable fixture, never a user save.
            File.Delete(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName));
            var refused = StartChild("recover", root, "cohort", "unused", ReserveLoopbackPort());
            children.Add(refused);
            await refused.WaitForExitAsync();
            Assert.Equal(1, refused.Process.ExitCode);
            Assert.DoesNotContain("READY:", await refused.ReadRemainingOutputAsync());
            Assert.Contains("canonical", await refused.Error.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName)));
            Assert.True(File.Exists(Path.Combine(root, CommittedMarker)));
            Assert.Equal(seed, File.ReadAllBytes(seedPath));
            AssertStageHashesEqual(retained, StageHashes(stageDirectory));
            success = true;
        } finally {
            await FinishOwnedChildrenAsync(children, root, success);
        }
    }

    private static OwnedChild StartChild(string action, string root, string source, string phase, int port)
    {
        var testAssembly = typeof(CohortPublicationProcessTests).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var argument in new[] {
            "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(testAssembly, ".deps.json"),
            typeof(FixtureAssemblyMarker).Assembly.Location, action, root, source, phase,
            port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        })
            start.ArgumentList.Add(argument);
        var child = Process.Start(start) ?? throw new InvalidOperationException("Managed publication fixture did not start.");
        return new OwnedChild(child);
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            // Positive control makes the negative listener observation useful.
            Assert.Contains(endpoint,
                IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());
            return endpoint.Port;
        }
        finally { listener.Stop(); }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "Godev-Interrupted-Pub-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Dictionary<string, string> StageHashes(string stageDirectory)
        => Directory.EnumerateFiles(stageDirectory).ToDictionary(path => Path.GetFileName(path)!,
            path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    private static void AssertFlushedIntegrity(JsonElement checkpoint)
    {
        using var payload = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payload)) {
            writer.WriteStartObject();
            foreach (var property in checkpoint.EnumerateObject())
                if (property.Name != "IntegritySha256")
                    property.WriteTo(writer);
            writer.WriteEndObject();
        }
        var expected = Convert.ToHexString(SHA256.HashData(payload.ToArray())).ToLowerInvariant();
        Assert.Equal(expected, checkpoint.GetProperty("IntegritySha256").GetString());
    }

    private static void AssertStageHashesEqual(Dictionary<string, string> expected, Dictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var pair in expected) {
            Assert.True(actual.TryGetValue(pair.Key, out var hash), pair.Key);
            Assert.Equal(pair.Value, hash);
        }
    }

    private static async Task FinishOwnedChildrenAsync(List<OwnedChild> children, string root, bool success)
    {
        var cleanupSucceeded = true;
        foreach (var child in children) {
            try {
                await child.StopAsync();
                if (!success) {
                    Console.Error.WriteLine($"Owned publication child {child.Process.Id} exited {child.Process.ExitCode}; retained {root}");
                    Console.Error.WriteLine(await child.Error.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
                }
                child.Process.Dispose();
            } catch (Exception error) {
                cleanupSucceeded = false;
                Console.Error.WriteLine($"Owned publication child cleanup failed; retained {root}: {error}");
            }
        }
        if (success && cleanupSucceeded) {
            var canonicalRoot = Path.GetFullPath(root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                Path.GetDirectoryName(canonicalRoot), ignoreCase: OperatingSystem.IsWindows());
            Assert.StartsWith("Godev-Interrupted-Pub-", Path.GetFileName(canonicalRoot));
            var directories = new Stack<string>();
            directories.Push(canonicalRoot);
            while (directories.TryPop(out var directory)) {
                Assert.Equal(0, (int)(File.GetAttributes(directory) & FileAttributes.ReparsePoint));
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory)) {
                    var attributes = File.GetAttributes(entry);
                    Assert.Equal(0, (int)(attributes & FileAttributes.ReparsePoint));
                    if ((attributes & FileAttributes.Directory) != 0)
                        directories.Push(entry);
                }
            }
            Directory.Delete(canonicalRoot, recursive: true);
        } else {
            Console.Error.WriteLine($"Publication counterexample retained at {root}");
            if (success)
                throw new InvalidOperationException("Owned managed child termination could not be confirmed.");
        }
    }

    private sealed class OwnedChild(Process process)
    {
        public Process Process { get; } = process;
        public Task<string> Error { get; } = process.StandardError.ReadToEndAsync(CancellationToken.None);

        public async Task WaitForExitAsync()
            => await Process.WaitForExitAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        public async Task StopAsync()
        {
            if (!Process.HasExited) {
                try { Process.Kill(); }
                catch (InvalidOperationException) when (Process.HasExited) { }
                // Teardown must still stop the exact owned child after test cancellation.
                await Process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            }
        }

        public async Task<string> ReadRemainingOutputAsync()
            => await Process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
