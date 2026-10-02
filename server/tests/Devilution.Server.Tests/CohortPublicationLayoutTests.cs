using System.Security.Cryptography;
using System.Text;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** First-publication metadata is host-owned and never a source of recovery state. */
public sealed class CohortPublicationLayoutTests
{
    [Fact]
    public void FreshHostPreparationCreatesInitializingLayoutBeforeSeedPublication()
    {
        var root = CreateRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            AuthoritativeTcpServer.ValidateCohortRootForStartup(store);

            Assert.True(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName)));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName)));
            var staging = Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName);
            Assert.True(Directory.Exists(staging));

            var seed = store.CreateWorldSeedStore();
            Assert.Equal(0x12345678U, seed.LoadOrCreate(0x12345678U));
            Assert.Equal(
                "305419896",
                File.ReadAllText(Path.Combine(root, AuthoritativeWorldSeedStore.FileName)).Trim());

            store.Save(CreateDocument(1));
            Assert.True(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName)));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName)));
            Assert.NotNull(store.Load());
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void OldRootOrphanIsRefusedAndPreservedWithoutBootstrapMarker()
    {
        var root = CreateRoot();
        var orphan = Path.Combine(root, ".authoritative-cohort.json.0123456789abcdef0123456789abcdef.tmp");
        var bytes = Encoding.UTF8.GetBytes("old-orphan");
        try {
            File.WriteAllBytes(orphan, bytes);
            using var store = new AuthoritativeCohortStore(root);

            Assert.Throws<InvalidDataException>(
                () => AuthoritativeTcpServer.ValidateCohortRootForStartup(store));
            Assert.Equal(bytes, File.ReadAllBytes(orphan));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void InitializingResidueIsIgnoredAndNeverPromoted()
    {
        var root = CreateRoot();
        var residue = Path.Combine(
            root,
            AuthoritativeCohortPublicationLayout.StagingDirectoryName,
            "authoritative-cohort.json.0123456789abcdef0123456789abcdef.tmp");
        var bytes = Encoding.UTF8.GetBytes("foreign-complete-looking-checkpoint");
        try {
            using (var first = new AuthoritativeCohortStore(root))
                AuthoritativeTcpServer.ValidateCohortRootForStartup(first);
            File.WriteAllBytes(residue, bytes);

            using (var second = new AuthoritativeCohortStore(root)) {
                AuthoritativeTcpServer.ValidateCohortRootForStartup(second);
                Assert.Null(second.Load());
            }

            Assert.Equal(bytes, File.ReadAllBytes(residue));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortStore.CheckpointFileName)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void ReservedStagingCollisionIsRefusedWithoutMutation()
    {
        var root = CreateRoot();
        var collision = Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName);
        var bytes = Encoding.UTF8.GetBytes("user-file");
        try {
            File.WriteAllBytes(collision, bytes);
            using var store = new AuthoritativeCohortStore(root);

            Assert.Throws<InvalidDataException>(
                () => AuthoritativeTcpServer.ValidateCohortRootForStartup(store));
            Assert.Equal(bytes, File.ReadAllBytes(collision));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void UnmarkedCanonicalAdoptsCommittedHistoryBeforeFuturePublication()
    {
        var root = CreateRoot();
        var unknown = Path.Combine(root, "unrelated-user-file.bin");
        var unknownBytes = RandomNumberGenerator.GetBytes(19);
        try {
            using (var legacyStore = new AuthoritativeCohortStore(root))
                legacyStore.Save(CreateDocument(1));
            File.WriteAllBytes(unknown, unknownBytes);

            using (var adopted = new AuthoritativeCohortStore(root))
                AuthoritativeTcpServer.ValidateCohortRootForStartup(adopted);

            Assert.True(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName)));
            Assert.Equal(unknownBytes, File.ReadAllBytes(unknown));

            File.Delete(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName));
            using var refused = new AuthoritativeCohortStore(root);
            Assert.Throws<InvalidDataException>(
                () => AuthoritativeTcpServer.ValidateCohortRootForStartup(refused));
            Assert.Throws<InvalidOperationException>(() => refused.Save(CreateDocument(1)));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortStore.CheckpointFileName)));
            Assert.Equal(unknownBytes, File.ReadAllBytes(unknown));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void CommittedCanonicalRecreatesMissingManagedStage()
    {
        var root = CreateRoot();
        try {
            using (var first = new AuthoritativeCohortStore(root)) {
                AuthoritativeTcpServer.ValidateCohortRootForStartup(first);
                first.Save(CreateDocument(1));
            }
            Directory.Delete(
                Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName),
                recursive: true);

            using var recovered = new AuthoritativeCohortStore(root);
            AuthoritativeTcpServer.ValidateCohortRootForStartup(recovered);
            Assert.True(Directory.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.StagingDirectoryName)));
            Assert.Equal(1UL, recovered.Load()!.Generation);
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void UnpreparedGenericStoreDoesNotInferHostMarkerState()
    {
        var root = CreateRoot();
        try {
            File.WriteAllBytes(
                Path.Combine(root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName),
                []);
            using var store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            Assert.NotNull(store.Load());
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void CanonicalAfterInterruptedFirstRenameRetainsInitializingStateUntilNextSave()
    {
        var root = CreateRoot();
        try {
            using (var first = new AuthoritativeCohortStore(root)) {
                AuthoritativeTcpServer.ValidateCohortRootForStartup(first);
                first.PublicationObserver = (phase, _) => {
                    if (phase == AuthoritativePublicationPhase.AfterCanonicalRename)
                        throw new IOException("interrupted after canonical publication");
                };
                Assert.Throws<IOException>(() => first.Save(CreateDocument(1)));
            }

            Assert.True(File.Exists(Path.Combine(
                root, AuthoritativeCohortStore.CheckpointFileName)));
            Assert.True(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName)));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName)));

            using var recovered = new AuthoritativeCohortStore(root);
            AuthoritativeTcpServer.ValidateCohortRootForStartup(recovered);
            Assert.Equal(1UL, recovered.Load()!.Generation);
            recovered.Save(CreateDocument(2));
            Assert.True(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName)));
            Assert.False(File.Exists(Path.Combine(
                root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName)));
            Assert.Equal(2UL, recovered.Load()!.Generation);
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void FactorySeedStoreCannotOutliveTheOriginalLease()
    {
        var root = CreateRoot();
        try {
            AuthoritativeWorldSeedStore seed;
            using (var first = new AuthoritativeCohortStore(root)) {
                AuthoritativeTcpServer.ValidateCohortRootForStartup(first);
                seed = first.CreateWorldSeedStore();
            }
            using var second = new AuthoritativeCohortStore(root);
            AuthoritativeTcpServer.ValidateCohortRootForStartup(second);

            Assert.Throws<ObjectDisposedException>(() => seed.LoadOrCreate(7));
            Assert.False(File.Exists(Path.Combine(root, AuthoritativeWorldSeedStore.FileName)));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CohortSeedStoreNeverRegeneratesAnOriginalSeedAfterCanonicalPublication(bool interrupted)
    {
        var root = CreateRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            AuthoritativeTcpServer.ValidateCohortRootForStartup(store);
            var seed = store.CreateWorldSeedStore();
            if (interrupted) {
                store.PublicationObserver = (phase, _) => {
                    if (phase == AuthoritativePublicationPhase.AfterCanonicalRename)
                        throw new IOException("interrupted before marker commit");
                };
                Assert.Throws<IOException>(() => store.Save(CreateDocument(1)));
            } else {
                store.Save(CreateDocument(1));
            }
            var canonical = File.ReadAllBytes(store.CheckpointPath);

            Assert.Throws<InvalidDataException>(() => seed.LoadOrCreate(7));

            Assert.False(File.Exists(Path.Combine(root, AuthoritativeWorldSeedStore.FileName)));
            Assert.Equal(canonical, File.ReadAllBytes(store.CheckpointPath));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void PreparedCommittedStoreRefusesAResetAfterCanonicalRemoval()
    {
        var root = CreateRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            AuthoritativeTcpServer.ValidateCohortRootForStartup(store);
            store.Save(CreateDocument(1));
            File.Delete(store.CheckpointPath);

            Assert.Throws<InvalidDataException>(() => store.Save(CreateDocument(1)));

            Assert.False(File.Exists(store.CheckpointPath));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void InitializingMarkerWithoutStageDirectoryCanRecover()
    {
        var root = CreateRoot();
        try {
            File.WriteAllBytes(Path.Combine(root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName), []);
            using var store = new AuthoritativeCohortStore(root);
            AuthoritativeTcpServer.ValidateCohortRootForStartup(store);
            Assert.True(Directory.Exists(Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName)));
            store.Save(CreateDocument(1));
            Assert.True(File.Exists(Path.Combine(root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData("malformed-seed")]
    [InlineData("malformed-canonical")]
    [InlineData("both-markers")]
    [InlineData("nonempty-marker")]
    public void InvalidRootsRefuseBeforeCreatingPublicationMetadata(string scenario)
    {
        var root = CreateRoot();
        try {
            if (scenario == "malformed-seed")
                File.WriteAllText(Path.Combine(root, AuthoritativeWorldSeedStore.FileName), "not a UInt32");
            if (scenario == "malformed-canonical")
                File.WriteAllText(Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName), "not JSON");
            if (scenario is "both-markers" or "nonempty-marker")
                File.WriteAllBytes(Path.Combine(root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName),
                    scenario == "nonempty-marker" ? [1] : []);
            if (scenario == "both-markers")
                File.WriteAllBytes(Path.Combine(root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName), []);
            var before = Directory.EnumerateFiles(root).ToDictionary(path => Path.GetFileName(path)!,
                File.ReadAllBytes, StringComparer.Ordinal);
            using var store = new AuthoritativeCohortStore(root);

            Assert.Throws<InvalidDataException>(() => AuthoritativeTcpServer.ValidateCohortRootForStartup(store));

            Assert.False(Directory.Exists(Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName)));
            foreach (var original in before)
                Assert.Equal(original.Value, File.ReadAllBytes(Path.Combine(root, original.Key!)));
            Assert.Equal(before.Count + 1, Directory.EnumerateFiles(root).Count()); // Only the existing lease contract writes.
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void FailedExplicitPreparationCannotFallBackToGenericPublication()
    {
        var root = CreateRoot();
        try {
            var orphan = Path.Combine(root, "old-orphan.tmp");
            File.WriteAllText(orphan, "preserved unknown data");
            using var store = new AuthoritativeCohortStore(root);
            Assert.Throws<InvalidDataException>(() => AuthoritativeTcpServer.ValidateCohortRootForStartup(store));

            Assert.Throws<InvalidOperationException>(() => store.Save(CreateDocument(1)));

            Assert.Equal("preserved unknown data", File.ReadAllText(orphan));
            Assert.False(File.Exists(store.CheckpointPath));
            Assert.False(Directory.Exists(Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName)));
            Assert.Equal(2, Directory.EnumerateFileSystemEntries(root).Count());
        } finally {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData("uppercase-guid")]
    [InlineData("unknown")]
    [InlineData("nested")]
    public void InvalidManagedStageEntriesRefuseWithoutMutation(string kind)
    {
        var root = CreateRoot();
        try {
            using (var first = new AuthoritativeCohortStore(root))
                AuthoritativeTcpServer.ValidateCohortRootForStartup(first);
            var staging = Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName);
            var name = kind == "uppercase-guid"
                ? "authoritative-cohort.json.ABCDEF0123456789ABCDEF0123456789.tmp"
                : kind == "nested" ? "authoritative-cohort.json.abcdef0123456789abcdef0123456789.tmp" : "unknown-file.bin";
            var entry = Path.Combine(staging, name);
            if (kind == "nested")
                Directory.CreateDirectory(entry);
            else
                File.WriteAllText(entry, "untouched");
            using var refused = new AuthoritativeCohortStore(root);

            if (kind == "nested")
                Assert.Throws<IOException>(() => AuthoritativeTcpServer.ValidateCohortRootForStartup(refused));
            else
                Assert.Throws<InvalidDataException>(() => AuthoritativeTcpServer.ValidateCohortRootForStartup(refused));

            Assert.False(File.Exists(refused.CheckpointPath));
            Assert.True(File.Exists(Path.Combine(root, AuthoritativeCohortPublicationLayout.InitializingMarkerFileName)));
            Assert.False(File.Exists(Path.Combine(root, AuthoritativeCohortPublicationLayout.CommittedMarkerFileName)));
            if (kind == "nested")
                Assert.True(Directory.Exists(entry));
            else
                Assert.Equal("untouched", File.ReadAllText(entry));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void ReparseStageEntryIsRefusedWithoutTraversingOrRemovingIt()
    {
        var root = CreateRoot();
        string? link = null;
        try {
            using (var first = new AuthoritativeCohortStore(root))
                AuthoritativeTcpServer.ValidateCohortRootForStartup(first);
            link = Path.Combine(root, AuthoritativeCohortPublicationLayout.StagingDirectoryName,
                "authoritative-cohort.json.abcdef0123456789abcdef0123456789.tmp");
            try {
                Directory.CreateSymbolicLink(link, root);
            } catch (UnauthorizedAccessException error) {
                Assert.Skip("The owned fixture cannot create a directory link: " + error.Message);
            } catch (IOException error) when ((error.HResult & 0xffff) is 1314 or 50 or 1) {
                Assert.Skip("The fixture filesystem does not permit directory links: " + error.Message);
            }
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            using var refused = new AuthoritativeCohortStore(root);

            Assert.Throws<IOException>(() => AuthoritativeTcpServer.ValidateCohortRootForStartup(refused));

            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            Assert.False(File.Exists(refused.CheckpointPath));
        } finally {
            if (link is not null && Directory.Exists(link)) {
                Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
                Directory.Delete(link); // Only the owned link, never its target.
            }
            DeleteRoot(root);
        }
    }

    private static AuthoritativeCohortDocument CreateDocument(ulong generation)
        => new(
            AuthoritativeCohortStore.CurrentFormatVersion,
            generation,
            new AuthoritativeCohortIdentity(
                "diablo",
                1,
                "0.2.0",
                new string('c', 64),
                new string('d', 64),
                20,
                AuthoritativeGameMode.SinglePlayer),
            20,
            10,
            20,
            [new(
                new string('a', 64),
                1,
                [new CommandResult {
                    ClientSequence = 1,
                    ServerReceiptSequence = 3,
                    AppliedTick = 20,
                    Status = CommandStatus.Accepted,
                    RejectReason = CommandRejectReason.Unspecified,
                }])],
            "module-state");

    private static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Cohort-Publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
