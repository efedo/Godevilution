using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Verifies target-specific metadata through the real canonical publisher. */
[SupportedOSPlatform("windows")]
public sealed class WindowsCheckpointPublicationTests
{
    private const AccessControlSections SecuritySections =
        AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group;

    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows checkpoint metadata is Windows-specific.")]
    public void ReplacementPreservesTargetMetadataBeforeAndAfterPublication()
    {
        var root = Path.Combine(Path.GetTempPath(), "Godev-Publication-Metadata-" + Guid.NewGuid().ToString("N"));
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(Document(1));
            var canonical = store.CheckpointPath;
            var security = new FileInfo(canonical).GetAccessControl(SecuritySections);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(canonical).SetAccessControl(security);
            var expectedSecurity = SecurityForm(canonical);
            var creationTime = new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            File.SetCreationTimeUtc(canonical, creationTime);
            File.SetAttributes(canonical, FileAttributes.Hidden | FileAttributes.Archive | FileAttributes.NotContentIndexed);
            const string streamName = ":checkpoint-meta:$DATA";
            File.WriteAllText(canonical + streamName, "retained metadata");
            var boundaries = new List<AuthoritativePublicationPhase>();
            store.PublicationObserver = (phase, path) => {
                boundaries.Add(phase);
                if (phase == AuthoritativePublicationPhase.BeforeStageWrite) {
                    Assert.Equal(0, new FileInfo(path).Length);
                    Assert.Equal(expectedSecurity, SecurityForm(path));
                    Assert.Equal("retained metadata", File.ReadAllText(path + streamName));
                } else if (phase == AuthoritativePublicationPhase.AfterFlushBeforeRename) {
                    Assert.Equal(creationTime, File.GetCreationTimeUtc(path));
                    Assert.Equal(expectedSecurity, SecurityForm(path));
                    Assert.Equal(File.GetAttributes(canonical), File.GetAttributes(path));
                }
            };

            store.Save(Document(2));

            Assert.Contains(AuthoritativePublicationPhase.BeforeStageWrite, boundaries);
            Assert.Contains(AuthoritativePublicationPhase.AfterFlushBeforeRename, boundaries);
            Assert.Contains(AuthoritativePublicationPhase.AfterCanonicalRename, boundaries);
            Assert.Equal(2UL, store.Load()!.Generation);
            Assert.Equal(expectedSecurity, SecurityForm(canonical));
            Assert.Equal(creationTime, File.GetCreationTimeUtc(canonical));
            Assert.Equal("retained metadata", File.ReadAllText(canonical + streamName));
            Assert.Empty(Directory.EnumerateFiles(root, ".authoritative-cohort.json.*.tmp"));
        } finally {
            Cleanup(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows readonly publication is Windows-specific.")]
    public void ReadonlyCanonicalRefusesReplacementWithoutBypassOrStageLeak()
    {
        var root = Path.Combine(Path.GetTempPath(), "Godev-Publication-Metadata-" + Guid.NewGuid().ToString("N"));
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(Document(1));
            var bytes = File.ReadAllBytes(store.CheckpointPath);
            File.SetAttributes(store.CheckpointPath, File.GetAttributes(store.CheckpointPath) | FileAttributes.ReadOnly);

            Assert.Throws<IOException>(() => store.Save(Document(2)));

            Assert.Equal(bytes, File.ReadAllBytes(store.CheckpointPath));
            Assert.True((File.GetAttributes(store.CheckpointPath) & FileAttributes.ReadOnly) != 0);
            Assert.Equal(1UL, store.Load()!.Generation);
            Assert.Empty(Directory.EnumerateFiles(root, ".authoritative-cohort.json.*.tmp"));
        } finally {
            Cleanup(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows checkpoint publication is Windows-specific.")]
    public void FailureAfterRenameNeverDeletesARecreatedStagePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "Godev-Publication-Metadata-" + Guid.NewGuid().ToString("N"));
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(Document(1));
            string? stage = null;
            store.PublicationObserver = (phase, path) => {
                if (phase == AuthoritativePublicationPhase.BeforeStageWrite)
                    stage = path;
                if (phase == AuthoritativePublicationPhase.AfterCanonicalRename) {
                    File.WriteAllText(stage!, "foreign stage");
                    throw new IOException("post-publication observer failure");
                }
            };

            Assert.Throws<IOException>(() => store.Save(Document(2)));

            Assert.Equal(2UL, store.Load()!.Generation);
            Assert.Equal("foreign stage", File.ReadAllText(stage!));
        } finally {
            Cleanup(root);
        }
    }

    private static AuthoritativeCohortDocument Document(ulong generation)
        => new(AuthoritativeCohortStore.CurrentFormatVersion, generation,
            new AuthoritativeCohortIdentity("metadata-fixture", 1, "0.1.0", new string('a', 64),
                new string('b', 64), 20, AuthoritativeGameMode.SinglePlayer),
            0, 1, 1, [], "module-state");

    private static string SecurityForm(string path)
        => new FileInfo(path).GetAccessControl(SecuritySections).GetSecurityDescriptorSddlForm(SecuritySections);

    private static void Cleanup(string root)
    {
        if (!Directory.Exists(root))
            return;
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
            Path.GetDirectoryName(Path.GetFullPath(root)), ignoreCase: true);
        Assert.StartsWith("Godev-Publication-Metadata-", Path.GetFileName(root));
        foreach (var path in Directory.EnumerateFileSystemEntries(root)) {
            Assert.Equal(0, (int)(File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)));
            File.SetAttributes(path, FileAttributes.Normal);
        }
        Directory.Delete(root, recursive: true);
    }
}
