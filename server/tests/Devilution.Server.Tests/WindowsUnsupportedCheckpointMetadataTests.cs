using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Devilution.Server.Tests;

/** Exercises unsupported NTFS metadata through the real canonical publisher. */
[SupportedOSPlatform("windows")]
public sealed class WindowsUnsupportedCheckpointMetadataTests
{
    private const uint FsctlGetObjectId = 0x0009009C;
    private const uint FsctlCreateOrGetObjectId = 0x000900C0;
    private const uint FsctlSetSparse = 0x000900C4;
    private const string RootPrefix = "Godevilution-Unsupported-Checkpoint-";

    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "NTFS object identifiers are Windows-specific.")]
    public void ObjectIdentifierRefusesBeforePayloadAndPreservesCanonical()
    {
        var root = NewRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(Document(1));
            byte[] objectId;
            try {
                objectId = QueryObjectId(store.CheckpointPath, create: true);
            } catch (IOException failure) when (IsCapabilityFailure(failure)) {
                Assert.Skip($"The owned fixture volume does not support object IDs: {failure.Message}");
                throw;
            }
            Assert.NotEqual(Guid.Empty, new Guid(objectId.AsSpan(0, 16)));
            Assert.Equal(objectId, QueryObjectId(store.CheckpointPath, create: false));
            AssertRefusalPreservesCanonical(store, root, "object identifier");
            Assert.Equal(objectId, QueryObjectId(store.CheckpointPath, create: false));
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "NTFS sparse files are Windows-specific.")]
    public void SparseMainStreamRefusesBeforePayloadAndPreservesCanonical()
    {
        var root = NewRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(Document(1));
            try {
                MarkSparse(store.CheckpointPath);
            } catch (IOException failure) when (IsCapabilityFailure(failure)) {
                Assert.Skip($"The owned fixture volume does not support sparse files: {failure.Message}");
            }
            Assert.True((File.GetAttributes(store.CheckpointPath) & FileAttributes.SparseFile) != 0,
                "Successful sparse setup must be positively confirmed.");
            AssertRefusalPreservesCanonical(store, root, "SparseFile");
            Assert.True((File.GetAttributes(store.CheckpointPath) & FileAttributes.SparseFile) != 0);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    private static void AssertRefusalPreservesCanonical(
        AuthoritativeCohortStore store, string root, string detail)
    {
        var canonical = File.ReadAllBytes(store.CheckpointPath);
        var entries = Directory.GetFileSystemEntries(root).Order(StringComparer.Ordinal).ToArray();
        var boundaries = new List<AuthoritativePublicationPhase>();
        store.PublicationObserver = (phase, _) => boundaries.Add(phase);

        var failure = Assert.Throws<IOException>(() => store.Save(Document(2)));

        Assert.Contains(detail, failure.Message, StringComparison.Ordinal);
        Assert.Empty(boundaries); // Not even BeforeStageWrite may be exposed.
        Assert.Equal(canonical, File.ReadAllBytes(store.CheckpointPath));
        Assert.Equal(1UL, store.Load()!.Generation);
        Assert.Equal(entries, Directory.GetFileSystemEntries(root).Order(StringComparer.Ordinal).ToArray());
    }

    private static byte[] QueryObjectId(string path, bool create)
    {
        using var stream = Open(path);
        var buffer = new byte[64]; // FILE_OBJECTID_BUFFER from winioctl.h.
        if (!DeviceIoControl(stream.SafeFileHandle,
                create ? FsctlCreateOrGetObjectId : FsctlGetObjectId,
                IntPtr.Zero, 0, buffer, buffer.Length, out var returned, IntPtr.Zero))
            throw Failure("Could not create/query the owned object ID.");
        Assert.Equal(buffer.Length, returned);
        return buffer;
    }

    private static void MarkSparse(string path)
    {
        using var stream = Open(path);
        if (!DeviceIoControl(stream.SafeFileHandle, FsctlSetSparse,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw Failure("Could not mark the owned canonical sparse.");
    }

    private static FileStream Open(string path)
        => new(path, FileMode.Open, FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.None);

    private static bool IsCapabilityFailure(IOException failure)
        => failure.InnerException is Win32Exception win32 && win32.NativeErrorCode is 1 or 50 or 87;

    private static IOException Failure(string message)
        => new(message, new Win32Exception(Marshal.GetLastWin32Error()));

    private static AuthoritativeCohortDocument Document(ulong generation)
        => new(AuthoritativeCohortStore.CurrentFormatVersion, generation,
            new AuthoritativeCohortIdentity("metadata-fixture", 1, "0.1.0", new string('a', 64),
                new string('b', 64), 20, AuthoritativeGameMode.SinglePlayer),
            0, 1, 1, [], "module-state");

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), RootPrefix + Guid.NewGuid().ToString("N"));

    private static void DeleteOwnedRoot(string root)
    {
        var full = Path.GetFullPath(root);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
            Path.GetDirectoryName(full), ignoreCase: true);
        Assert.StartsWith(RootPrefix, Path.GetFileName(full));
        if (!Directory.Exists(full))
            return;
        Assert.Equal(0, (int)(File.GetAttributes(full) & FileAttributes.ReparsePoint));
        foreach (var entry in Directory.EnumerateFileSystemEntries(full))
            Assert.Equal(0, (int)(File.GetAttributes(entry) & (FileAttributes.Directory | FileAttributes.ReparsePoint)));
        Directory.Delete(full, recursive: true);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode,
        IntPtr inputBuffer, int inputSize, [Out] byte[] outputBuffer, int outputSize,
        out int bytesReturned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode,
        IntPtr inputBuffer, int inputSize, IntPtr outputBuffer, int outputSize,
        out int bytesReturned, IntPtr overlapped);
}
