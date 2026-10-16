using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsCheckpointStageTests
{
    private const AccessControlSections SecuritySections =
        AccessControlSections.Access
        | AccessControlSections.Owner
        | AccessControlSections.Group;

    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows ACL and stream semantics are Windows-specific.")]
    public void StageIsSecuredBeforePayloadAndPreservesCreationTimeAndAcl()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var creationTime = new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, [9, 8, 7]);
            File.SetCreationTimeUtc(canonical, creationTime);
            var expectedSecurity = SecurityForm(GetFileSecurity(canonical));

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, root);
            Assert.Equal(0, new FileInfo(stage).Length);
            Assert.Equal(expectedSecurity, SecurityForm(GetFileSecurity(stage)));

            checkpoint.WriteAndFlush(payload);
            checkpoint.FinalizeMetadataAndClose();

            Assert.Equal(payload, File.ReadAllBytes(stage));
            Assert.Equal(creationTime, File.GetCreationTimeUtc(stage));
            Assert.Equal(expectedSecurity, SecurityForm(GetFileSecurity(stage)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows ACL semantics are Windows-specific.")]
    public void ProtectedCustomDaclIsAppliedAtCreateAndRetained()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, [1, 2, 3]);
            var identity = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The test identity has no SID.");
            var security = GetFileSecurity(canonical);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                identity,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            new FileInfo(canonical).SetAccessControl(security);
            var expectedSecurity = SecurityForm(GetFileSecurity(canonical));

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, root);
            Assert.Equal(expectedSecurity, SecurityForm(GetFileSecurity(stage)));
            checkpoint.WriteAndFlush([4, 5, 6]);
            checkpoint.FinalizeMetadataAndClose();
            Assert.Equal(expectedSecurity, SecurityForm(GetFileSecurity(stage)));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stream semantics are Windows-specific.")]
    public void NamedStreamsAreCopiedOnlyAfterStageSecurityIsInstalled()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        const string streamSuffix = ":checkpoint-meta:$DATA";
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, [1, 2, 3]);
            File.WriteAllText(canonical + streamSuffix, "metadata");

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, root);
            Assert.Equal(0, new FileInfo(stage).Length);
            Assert.Equal("metadata", File.ReadAllText(stage + streamSuffix));
            checkpoint.WriteAndFlush([7, 8, 9]);
            checkpoint.FinalizeMetadataAndClose();
            Assert.Equal([7, 8, 9], File.ReadAllBytes(stage));
            Assert.Equal("metadata", File.ReadAllText(stage + streamSuffix));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stream sharing is Windows-specific.")]
    public void StreamCopyFailureCleansOnlyTheOwnedStageAndRetainsCanonical()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        const string streamSuffix = ":checkpoint-meta:$DATA";
        try {
            Directory.CreateDirectory(root);
            var original = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(canonical, original);
            File.WriteAllText(canonical + streamSuffix, "metadata");
            using (var blocker = new FileStream(
                       canonical + streamSuffix,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.None)) {
                Assert.Throws<IOException>(() =>
                    WindowsCheckpointStage.Create(stage, canonical, root));
            }
            Assert.False(File.Exists(stage));
            Assert.Equal(original, File.ReadAllBytes(canonical));
            Assert.Equal("metadata", File.ReadAllText(canonical + streamSuffix));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows compression is Windows-specific.")]
    public void SupportedCompressionStateIsAppliedBeforePayload()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, new byte[4096]);
            try {
                SetCompression(canonical, compressed: true);
            } catch (IOException exception) when (IsCompressionCapabilityFailure(exception)) {
                Assert.Skip($"The fixture volume does not support NTFS compression: {exception.Message}");
            }
            Assert.True(
                (File.GetAttributes(canonical) & FileAttributes.Compressed) != 0,
                "FSCTL_SET_COMPRESSION succeeded but the canonical fixture is not compressed.");

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, root);
            Assert.True((File.GetAttributes(stage) & FileAttributes.Compressed) != 0);
            checkpoint.WriteAndFlush(new byte[4096]);
            checkpoint.FinalizeMetadataAndClose();
            Assert.True((File.GetAttributes(stage) & FileAttributes.Compressed) != 0);
        } finally {
            if (File.Exists(canonical))
                TrySetCompression(canonical, compressed: false);
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stage ownership is Windows-specific.")]
    public void ExistingStageCollisionIsNeverDeleted()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, [1, 2, 3]);
            using (var initial = WindowsCheckpointStage.Create(stage, canonical, root)) {
                initial.WriteAndFlush([4, 5, 6]);
                initial.FinalizeMetadataAndClose();
            }
            Assert.False(File.Exists(stage));
            File.WriteAllText(stage, "existing");

            Assert.Throws<IOException>(() =>
                WindowsCheckpointStage.Create(stage, canonical, root));
            Assert.Equal("existing", File.ReadAllText(stage));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stage ownership is Windows-specific.")]
    public void PublishedStageDoesNotDeleteAFileRecreatedAtTheOldStagePath()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, [1, 2, 3]);

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, root);
            checkpoint.WriteAndFlush([4, 5, 6]);
            checkpoint.FinalizeMetadataAndClose();

            var published = Path.Combine(root, "published.json");
            File.Move(stage, published);
            checkpoint.MarkPublished();
            File.WriteAllText(stage, "foreign stage");
            checkpoint.Dispose();

            Assert.Equal("foreign stage", File.ReadAllText(stage));
            Assert.Equal([4, 5, 6], File.ReadAllBytes(published));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stream compression is Windows-specific.")]
    public void NamedStreamCompressionIsPreservedIndependentlyOfMainStream()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        const string streamSuffix = ":checkpoint-meta:$DATA";
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, new byte[4096]);
            File.WriteAllText(canonical + streamSuffix, new string('m', 4096));
            try {
                SetCompression(canonical + streamSuffix, compressed: true);
            } catch (IOException exception) when (IsCompressionCapabilityFailure(exception)) {
                Assert.Skip($"The fixture volume does not support NTFS stream compression: {exception.Message}");
            }
            Assert.Equal((short)0, GetCompressionFormat(canonical));
            Assert.Equal((short)2, GetCompressionFormat(canonical + streamSuffix));

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, root);
            Assert.Equal((short)0, GetCompressionFormat(checkpoint.Stream.SafeFileHandle));
            Assert.Equal((short)2, GetCompressionFormat(stage + streamSuffix));
            checkpoint.WriteAndFlush([7, 8, 9]);
            checkpoint.FinalizeMetadataAndClose();
            Assert.Equal((short)2, GetCompressionFormat(stage + streamSuffix));
        } finally {
            if (File.Exists(canonical + streamSuffix))
                TrySetCompression(canonical + streamSuffix, compressed: false);
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows sparse-stream semantics are Windows-specific.")]
    public void SparseNamedStreamIsRefusedBeforeStageCreation()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        const string streamSuffix = ":checkpoint-meta:$DATA";
        try {
            Directory.CreateDirectory(root);
            var original = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(canonical, original);
            File.WriteAllBytes(canonical + streamSuffix, new byte[4096]);
            try {
                SetSparse(canonical + streamSuffix);
            } catch (IOException exception) when (IsSparseCapabilityFailure(exception)) {
                Assert.Skip($"The fixture volume does not support sparse streams: {exception.Message}");
            }
            Assert.True(
                (GetHandleAttributes(canonical + streamSuffix) & FileAttributes.SparseFile) != 0,
                "FSCTL_SET_SPARSE succeeded but the named stream is not sparse.");

            Assert.Throws<IOException>(() =>
                WindowsCheckpointStage.Create(stage, canonical, root));
            Assert.False(File.Exists(stage));
            Assert.Equal(original, File.ReadAllBytes(canonical));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stage ownership is Windows-specific.")]
    public void MarkPublishedRequiresFinalization()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stage = Path.Combine(root, ".canonical.json.stage");
        try {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(canonical, [1, 2, 3]);
            using (var checkpoint = WindowsCheckpointStage.Create(stage, canonical, root))
                Assert.Throws<InvalidOperationException>(() => checkpoint.MarkPublished());
            Assert.False(File.Exists(stage));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows inheritance semantics are Windows-specific.")]
    public void FirstStageUsesOrdinaryInheritedFileSecurity()
    {
        var root = CreateRoot();
        var stageDirectory = Path.Combine(root, ".staging");
        var canonical = Path.Combine(root, "missing-canonical.json");
        var stage = Path.Combine(stageDirectory, "first.json");
        var probe = Path.Combine(stageDirectory, "inheritance-probe.json");
        try {
            Directory.CreateDirectory(root);
            WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory);
            File.WriteAllBytes(probe, [0]);
            var expectedSecurity = SecurityForm(GetFileSecurity(probe));
            File.Delete(probe);

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, stageDirectory);
            Assert.Equal(expectedSecurity, SecurityForm(GetFileSecurity(stage)));
            Assert.Equal(0, new FileInfo(stage).Length);
            checkpoint.WriteAndFlush([1, 2, 3]);
            checkpoint.FinalizeMetadataAndClose();
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows ACL semantics are Windows-specific.")]
    public void ChangedManagedStageDirectoryIsRejectedWithoutRewriteOrRemoval()
    {
        var root = CreateRoot();
        var stageDirectory = Path.Combine(root, ".staging");
        try {
            Directory.CreateDirectory(root);
            WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory);

            var identity = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The test identity has no SID.");
            var changed = new DirectorySecurity();
            var original = new DirectoryInfo(stageDirectory)
                .GetAccessControl(SecuritySections);
            changed.SetSecurityDescriptorBinaryForm(
                original.GetSecurityDescriptorBinaryForm(),
                AccessControlSections.Access);
            changed.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            changed.SetAccessRule(new FileSystemAccessRule(
                identity,
                FileSystemRights.ReadAttributes,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow));
            new DirectoryInfo(stageDirectory).SetAccessControl(changed);
            var changedSecurity = SecurityForm(new DirectoryInfo(stageDirectory)
                .GetAccessControl(SecuritySections));

            try {
                Assert.Throws<IOException>(() =>
                    WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory));
                Assert.True(Directory.Exists(stageDirectory));
                Assert.Equal(
                    changedSecurity,
                    SecurityForm(new DirectoryInfo(stageDirectory)
                        .GetAccessControl(SecuritySections)));
            } finally {
                // A read descriptor has no dirty sections; restore a fresh
                // access-only descriptor rather than performing a no-op set.
                var restored = new DirectorySecurity();
                restored.SetSecurityDescriptorBinaryForm(
                    original.GetSecurityDescriptorBinaryForm(),
                    AccessControlSections.Access);
                new DirectoryInfo(stageDirectory).SetAccessControl(restored);
            }
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows directory ACL semantics are Windows-specific.")]
    public void DedicatedStageDirectoryUsesRootSecurityWithoutChangingRoot()
    {
        var root = CreateRoot();
        var stageDirectory = Path.Combine(root, ".staging");
        try {
            Directory.CreateDirectory(root);
            var rootSecurity = SecurityForm(new DirectoryInfo(root)
                .GetAccessControl(SecuritySections));
            WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory);

            Assert.True(Directory.Exists(stageDirectory));
            Assert.Equal(
                rootSecurity,
                SecurityForm(new DirectoryInfo(root).GetAccessControl(SecuritySections)));
            Assert.Equal(
                rootSecurity,
                SecurityForm(new DirectoryInfo(stageDirectory).GetAccessControl(SecuritySections)));
        } finally {
            DeleteRoot(root);
        }
    }

    private static void SetCompression(string path, bool compressed)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            options: FileOptions.None);
        short format = compressed ? (short)1 : (short)0;
        if (!DeviceIoControl(
                stream.SafeFileHandle,
                FsctlSetCompression,
                ref format,
                sizeof(short),
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero))
            throw Win32Failure("Could not set test compression state.");
    }

    private static void TrySetCompression(string path, bool compressed)
    {
        try {
            SetCompression(path, compressed);
        } catch (IOException) {
            // Cleanup is best effort after a capability-skipped fixture.
        }
    }

    private static short GetCompressionFormat(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            options: FileOptions.None);
        return GetCompressionFormat(stream.SafeFileHandle);
    }

    private static short GetCompressionFormat(SafeFileHandle handle)
    {
        if (!DeviceIoControl(
                handle,
                FsctlGetCompression,
                IntPtr.Zero,
                0,
                out short format,
                sizeof(short),
                out var bytesReturned,
                IntPtr.Zero)
            || bytesReturned != sizeof(short))
            throw Win32Failure("Could not query test compression state.");
        return format;
    }

    private static void SetSparse(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            options: FileOptions.None);
        if (!DeviceIoControl(
                stream.SafeFileHandle,
                FsctlSetSparse,
                IntPtr.Zero,
                0,
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero))
            throw Win32Failure("Could not set test sparse state.");
    }

    private static FileAttributes GetHandleAttributes(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            options: FileOptions.None);
        if (!GetFileInformationByHandleEx(
                stream.SafeFileHandle,
                FileBasicInfoClass,
                out var information,
                Marshal.SizeOf<FileBasicInfo>()))
            throw Win32Failure("Could not query test stream attributes.");
        return (FileAttributes)information.FileAttributes;
    }

    private static bool IsCompressionCapabilityFailure(IOException exception)
        => IsCapabilityFailure(exception);

    private static bool IsSparseCapabilityFailure(IOException exception)
        => IsCapabilityFailure(exception);

    private static bool IsCapabilityFailure(IOException exception)
    {
        if (exception.InnerException is not Win32Exception win32)
            return false;
        return win32.NativeErrorCode is 1 or 50 or 87;
    }

    private static IOException Win32Failure(string message)
        => new(message, new Win32Exception(Marshal.GetLastWin32Error()));

    private static FileSecurity GetFileSecurity(string path)
        => new FileInfo(path).GetAccessControl(SecuritySections);

    private static string SecurityForm(ObjectSecurity security)
        => security.GetSecurityDescriptorSddlForm(SecuritySections);

    [Theory(SkipUnless = nameof(IsWindows), Skip = "Windows long-path metadata APIs are Windows-specific.")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LongCheckpointStagePreservesPayloadSecurityAndNamedStreams(bool replacing, bool longCanonical)
    {
        var root = CreateRoot();
        var checkpointRoot = root;
        var targetLength = longCanonical ? 282 : 214;
        while (checkpointRoot.Length < targetLength - 1)
            checkpointRoot = Path.Combine(checkpointRoot,
                new string('x', Math.Min(40, targetLength - checkpointRoot.Length - 1)));
        var stageDirectory = Path.Combine(checkpointRoot, ".authoritative-staging");
        var canonical = Path.Combine(checkpointRoot, "authoritative-cohort.json");
        var stage = Path.Combine(stageDirectory, "authoritative-cohort.json." + Guid.NewGuid().ToString("N") + ".tmp");
        const string streamSuffix = ":checkpoint-meta:$DATA";
        var original = new byte[] { 9, 8, 7 };
        var payload = new byte[] { 1, 2, 3, 4 };
        Assert.True(stage.Length > 260);
        Assert.Equal(longCanonical, canonical.Length > 260);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(root));
        try {
            Directory.CreateDirectory(checkpointRoot);
            if (replacing) {
                File.WriteAllBytes(canonical, original);
                File.WriteAllText(canonical + streamSuffix, "retained metadata");
            }
            WindowsCheckpointStage.EnsureStageDirectory(checkpointRoot, stageDirectory);
            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, stageDirectory);
            Assert.Equal(0, new FileInfo(stage).Length);
            var security = SecurityForm(GetFileSecurity(stage));
            if (replacing) {
                Assert.Equal(SecurityForm(GetFileSecurity(canonical)), security);
                Assert.Equal("retained metadata", File.ReadAllText(stage + streamSuffix));
            }
            checkpoint.WriteAndFlush(payload);
            checkpoint.FinalizeMetadataAndClose();
            Assert.Equal(payload, File.ReadAllBytes(stage));
            Assert.Equal(security, SecurityForm(GetFileSecurity(stage)));
            if (replacing) {
                Assert.Equal(original, File.ReadAllBytes(canonical));
                Assert.Equal("retained metadata", File.ReadAllText(stage + streamSuffix));
            } else {
                Assert.False(File.Exists(canonical));
            }
        } finally {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Windows-Checkpoint-Stage-\u03a9-" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private const uint FsctlSetCompression = 0x0009C040;
    private const uint FsctlGetCompression = 0x0009003C;
    private const uint FsctlSetSparse = 0x000900C4;
    private const int FileBasicInfoClass = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileBasicInfo
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint FileAttributes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        ref short inputBuffer,
        int inputBufferSize,
        IntPtr outputBuffer,
        int outputBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        IntPtr inputBuffer,
        int inputBufferSize,
        out short outputBuffer,
        int outputBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        IntPtr inputBuffer,
        int inputBufferSize,
        IntPtr outputBuffer,
        int outputBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out FileBasicInfo fileInformation,
        int bufferSize);
}
