using System.ComponentModel;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Additional Windows-only checkpoint capability and cleanup policies. */
[SupportedOSPlatform("windows")]
public sealed class WindowsCheckpointPolicyTests
{
    private const uint SeFileObject = 1;
    private const uint LabelSecurityInformation = 0x00000010;
    private const uint StringSecurityDescriptorRevision = 1;
    private const uint FsctlSetCompression = 0x0009C040;
    private const uint FsctlGetCompression = 0x0009003C;
    private const short CompressionFormatNone = 0;
    private const short CompressionFormatLznt1 = 2;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows security labels are Windows-specific.")]
    public void ExplicitLowIntegrityCanonicalIsRejectedBeforeStageCreation()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stageDirectory = Path.Combine(root, ".staging");
        var stage = Path.Combine(stageDirectory, "canonical.json.stage");
        var original = new byte[] { 1, 2, 3, 4 };
        try {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(stageDirectory);
            File.WriteAllBytes(canonical, original);
            SetAndRequireLowIntegrityLabel(canonical);

            Assert.Throws<IOException>(() =>
                WindowsCheckpointStage.Create(stage, canonical, stageDirectory));
            Assert.False(File.Exists(stage));
            Assert.Equal(original, File.ReadAllBytes(canonical));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows security labels are Windows-specific.")]
    public void NonInheritedLowIntegrityRootLabelRefusesStageDirectoryCreation()
    {
        var root = CreateRoot();
        var stageDirectory = Path.Combine(root, ".staging");
        try {
            Directory.CreateDirectory(root);
            SetAndRequireLowIntegrityLabel(root);

            Assert.Throws<IOException>(() =>
                WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory));
            Assert.False(Directory.Exists(stageDirectory));
            Assert.True(QueryHasLowIntegrityLabel(root));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows compression is Windows-specific.")]
    public void FirstManagedStageFileInheritsQueriedLznt1Compression()
    {
        var root = CreateRoot();
        var stageDirectory = Path.Combine(root, ".staging");
        var canonical = Path.Combine(root, "missing-canonical.json");
        var stage = Path.Combine(stageDirectory, "first.json");
        try {
            Directory.CreateDirectory(root);
            try {
                SetDirectoryCompression(root, compressed: true);
                RequireCompressionFormat(root, CompressionFormatLznt1);
            } catch (IOException exception) when (IsCompressionCapabilityFailure(exception)) {
                Assert.Skip($"The fixture volume does not support directory compression: {exception.Message}");
            }

            WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory);
            RequireCompressionFormat(stageDirectory, CompressionFormatLznt1);

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, stageDirectory);
            Assert.Equal(CompressionFormatLznt1,
                QueryCompressionFormat(checkpoint.Stream.SafeFileHandle));
            checkpoint.WriteAndFlush([1, 2, 3]);
            checkpoint.FinalizeMetadataAndClose();
            RequireCompressionFormat(stage, CompressionFormatLznt1);
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stage cleanup is Windows-specific.")]
    public void ThrowingMainStreamDisposeDeletesOnlyOwnedStage()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stageDirectory = Path.Combine(root, ".staging");
        var stage = Path.Combine(stageDirectory, "canonical.json.stage");
        var foreign = Path.Combine(stageDirectory, "foreign.tmp");
        var original = new byte[] { 9, 8, 7 };
        var foreignBytes = new byte[] { 4, 5, 6 };
        try {
            Directory.CreateDirectory(root);
            WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory);
            File.WriteAllBytes(canonical, original);
            File.WriteAllBytes(foreign, foreignBytes);

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, stageDirectory);
            ReplacePrivateStream(checkpoint, () => new ThrowingDisposeFileStream(stage));

            Assert.Throws<IOException>(() => checkpoint.Dispose());
            Assert.False(File.Exists(stage));
            Assert.Equal(original, File.ReadAllBytes(canonical));
            Assert.Equal(foreignBytes, File.ReadAllBytes(foreign));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows stage cleanup is Windows-specific.")]
    public void FlushFailureRemainsPrimaryAndDisposeFailureIsAttached()
    {
        var root = CreateRoot();
        var canonical = Path.Combine(root, "canonical.json");
        var stageDirectory = Path.Combine(root, ".staging");
        var stage = Path.Combine(stageDirectory, "canonical.json.stage");
        var foreign = Path.Combine(stageDirectory, "foreign.tmp");
        var original = new byte[] { 7, 8, 9 };
        var foreignBytes = new byte[] { 1, 3, 5 };
        try {
            Directory.CreateDirectory(root);
            WindowsCheckpointStage.EnsureStageDirectory(root, stageDirectory);
            File.WriteAllBytes(canonical, original);
            File.WriteAllBytes(foreign, foreignBytes);

            using var checkpoint = WindowsCheckpointStage.Create(stage, canonical, stageDirectory);
            ReplacePrivateStream(checkpoint, () => new ThrowingFlushAndDisposeFileStream(stage));

            var primary = Assert.Throws<IOException>(() => checkpoint.FinalizeMetadataAndClose());
            Assert.Contains("primary flush", primary.Message, StringComparison.Ordinal);
            Assert.True(
                primary.Data["WindowsCheckpointStage.StreamCleanupException"] is IOException secondary
                && secondary.Message.Contains("secondary dispose", StringComparison.Ordinal));

            checkpoint.Dispose();
            Assert.False(File.Exists(stage));
            Assert.Equal(original, File.ReadAllBytes(canonical));
            Assert.Equal(foreignBytes, File.ReadAllBytes(foreign));
        } finally {
            DeleteRoot(root);
        }
    }

    private static void SetAndRequireLowIntegrityLabel(string path)
    {
        try {
            SetLowIntegrityLabel(path);
        } catch (IOException exception) when (IsLabelCapabilityFailure(exception)) {
            Assert.Skip($"The fixture volume does not permit an owned low-integrity label: {exception.Message}");
        }

        Assert.True(
            QueryHasLowIntegrityLabel(path),
            "The low-integrity label setup was not positively confirmed by a security-descriptor query.");
    }

    private static bool QueryHasLowIntegrityLabel(string path)
    {
        try {
            var descriptor = QuerySecurityDescriptor(path);
            if (descriptor.SystemAcl is null)
                return false;
            foreach (GenericAce ace in descriptor.SystemAcl) {
                // SYSTEM_MANDATORY_LABEL_ACE: four-byte header, policy mask,
                // then SID. Verify the actual fields, not SDDL formatting.
                if ((byte)ace.AceType != 0x11 || ace.AceFlags != AceFlags.None)
                    continue;
                var bytes = new byte[ace.BinaryLength];
                ace.GetBinaryForm(bytes, 0);
                if (bytes.Length >= 20
                    && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) == 1
                    && new SecurityIdentifier(bytes, 8).Value == "S-1-16-4096")
                    return true;
            }
            return false;
        } catch (IOException exception) when (IsLabelCapabilityFailure(exception)) {
            Assert.Skip($"The fixture volume does not expose mandatory-label state: {exception.Message}");
            return false;
        }
    }

    private static void SetLowIntegrityLabel(string path)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                "S:(ML;;NW;;;LW)",
                StringSecurityDescriptorRevision,
                out var descriptor,
                out _))
            throw Win32Failure("Could not create the low-integrity descriptor.");
        try {
            if (!GetSecurityDescriptorSacl(
                    descriptor,
                    out var saclPresent,
                    out var sacl,
                    out _)
                || !saclPresent)
                throw new IOException("The low-integrity descriptor has no mandatory-label SACL.");
            var result = SetNamedSecurityInfoW(
                path,
                SeFileObject,
                LabelSecurityInformation,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                sacl);
            if (result != 0)
                throw Win32Failure("Could not apply the low-integrity label.", unchecked((int)result));
        } finally {
            LocalFree(descriptor);
        }
    }

    private static RawSecurityDescriptor QuerySecurityDescriptor(string path)
    {
        var result = GetNamedSecurityInfoW(
            path,
            SeFileObject,
            LabelSecurityInformation,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            out var descriptor);
        if (result != 0)
            throw Win32Failure("Could not query the mandatory-label state.", result);
        try {
            var length = GetSecurityDescriptorLength(descriptor);
            if (length <= 0)
                throw new IOException("The queried security descriptor is invalid.");
            var bytes = new byte[length];
            Marshal.Copy(descriptor, bytes, 0, length);
            return new RawSecurityDescriptor(bytes, 0);
        } finally {
            LocalFree(descriptor);
        }
    }

    private static void SetDirectoryCompression(string path, bool compressed)
    {
        using var handle = CreateFileW(
            path,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
            throw Win32Failure("Could not open the directory for compression.");
        var format = compressed ? CompressionFormatLznt1 : CompressionFormatNone;
        if (!DeviceIoControl(
                handle,
                FsctlSetCompression,
                ref format,
                sizeof(short),
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero))
            throw Win32Failure("Could not set directory compression.");
    }

    private static void RequireCompressionFormat(string path, short expected)
    {
        Assert.Equal(expected, QueryCompressionFormat(path));
    }

    private static short QueryCompressionFormat(string path)
    {
        using var handle = CreateFileW(
            path,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
            throw Win32Failure("Could not open the path for compression query.");
        return QueryCompressionFormat(handle);
    }

    private static short QueryCompressionFormat(SafeFileHandle handle)
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
            throw Win32Failure("Could not query compression format.");
        return format;
    }

    private static void ReplacePrivateStream(
        WindowsCheckpointStage checkpoint,
        Func<FileStream> replacementFactory)
    {
        var field = typeof(WindowsCheckpointStage).GetField(
            "stream",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(WindowsCheckpointStage).FullName, "stream");
        var original = (FileStream?)field.GetValue(checkpoint)
            ?? throw new InvalidOperationException("The checkpoint has no writable stream.");
        field.SetValue(checkpoint, null);
        original.Dispose();
        field.SetValue(checkpoint, replacementFactory());
    }

    private sealed class ThrowingDisposeFileStream : FileStream
    {
        public ThrowingDisposeFileStream(string path)
            : base(path, FileMode.Open, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                throw new IOException("secondary dispose failure");
        }
    }

    private sealed class ThrowingFlushAndDisposeFileStream : FileStream
    {
        public ThrowingFlushAndDisposeFileStream(string path)
            : base(path, FileMode.Open, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)
        {
        }

        public override void Flush(bool flushToDisk)
            => throw new IOException("primary flush failure");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                throw new IOException("secondary dispose failure");
        }
    }

    private static bool IsLabelCapabilityFailure(IOException exception)
        => IsWin32CapabilityFailure(exception, 1, 5, 50, 87, 1314);

    private static bool IsCompressionCapabilityFailure(IOException exception)
        => IsWin32CapabilityFailure(exception, 1, 50, 87);

    private static bool IsWin32CapabilityFailure(IOException exception, params int[] codes)
        => exception.InnerException is Win32Exception win32
            && codes.Contains(win32.NativeErrorCode);

    private static IOException Win32Failure(string message)
        => Win32Failure(message, Marshal.GetLastWin32Error());

    private static IOException Win32Failure(string message, int error)
        => new(message, new Win32Exception(error));

    private static string CreateRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Windows-Checkpoint-Policy-" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        var full = Path.GetFullPath(root);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
            Path.GetDirectoryName(full), ignoreCase: true);
        Assert.StartsWith("Godevilution-Windows-Checkpoint-Policy-", Path.GetFileName(full));
        if (!Directory.Exists(full))
            return;
        Assert.Equal(0, (int)(File.GetAttributes(full) & FileAttributes.ReparsePoint));
        foreach (var entry in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
            Assert.Equal(0, (int)(File.GetAttributes(entry) & FileAttributes.ReparsePoint));
        Directory.Delete(full, recursive: true);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor,
        uint stringSDRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorSacl(
        IntPtr securityDescriptor,
        out bool saclPresent,
        out IntPtr sacl,
        out bool saclDefaulted);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SetNamedSecurityInfoW(
        string objectName,
        uint objectType,
        uint securityInfo,
        IntPtr owner,
        IntPtr group,
        IntPtr dacl,
        IntPtr sacl);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetNamedSecurityInfoW(
        string objectName,
        uint objectType,
        uint securityInfo,
        IntPtr owner,
        IntPtr group,
        IntPtr dacl,
        IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport("advapi32.dll")]
    private static extern int GetSecurityDescriptorLength(IntPtr securityDescriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

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
}
