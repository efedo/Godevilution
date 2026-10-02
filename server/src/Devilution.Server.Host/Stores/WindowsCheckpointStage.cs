using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using Microsoft.Win32.SafeHandles;

namespace Devilution.Server.Stores;

/**
 * Creates a Windows checkpoint stage with the target security descriptor
 * before any checkpoint bytes can be written. Publication remains owned by
 * AuthoritativeCohortStore; this type only owns stage preparation and cleanup.
 */
[SupportedOSPlatform("windows")]
internal sealed class WindowsCheckpointStage : IDisposable
{
    private const AccessControlSections SupportedSecuritySections =
        AccessControlSections.Access
        | AccessControlSections.Owner
        | AccessControlSections.Group;
    private const uint FsctlSetCompression = 0x0009C040;
    private const uint FsctlGetCompression = 0x0009003C;
    private const uint FsctlGetObjectId = 0x0009009C;
    private const short CompressionFormatNone = 0;
    private const short CompressionFormatLznt1 = 2;
    private const int ErrorFileNotFound = 2;
    private const int ErrorHandleEof = 38;
    private const int ErrorAlreadyExists = 183;
    private const int ErrorNotFound = 1168;
    private const int ErrorMoreData = 234;
    private const int ErrorAccessDenied = 5;
    private const int ErrorPrivilegeNotHeld = 1314;
    private const int StreamNameCapacity = 296;
    private const uint SeFileObject = 1;
    private const uint SaclSecurityInformation = 0x00000008;
    private const uint LabelSecurityInformation = 0x00000010;
    private const uint AttributeSecurityInformation = 0x00000020;
    private const uint ScopeSecurityInformation = 0x00000040;
    private const uint SpecialSecurityInformation =
        LabelSecurityInformation | AttributeSecurityInformation | ScopeSecurityInformation;
    private static readonly FileAttributes SupportedBasicAttributes =
        FileAttributes.Archive
        | FileAttributes.Hidden
        | FileAttributes.NotContentIndexed
        | FileAttributes.Offline
        | FileAttributes.ReadOnly
        | FileAttributes.System
        | FileAttributes.Temporary;
    private static readonly FileAttributes UnsupportedAttributes =
        FileAttributes.Device
        | FileAttributes.Directory
        | FileAttributes.ReparsePoint
        | FileAttributes.SparseFile
        | FileAttributes.Encrypted
        | FileAttributes.IntegrityStream
        | FileAttributes.NoScrubData;
    private static readonly FileAttributes KnownAttributes =
        FileAttributes.Normal
        | SupportedBasicAttributes
        | FileAttributes.Compressed
        | UnsupportedAttributes;

    private readonly string stagePath;
    private readonly string? canonicalPath;
    private readonly FileSecurity expectedSecurity;
    private readonly FileAttributes expectedAttributes;
    private readonly DateTime? expectedCreationTimeUtc;
    private readonly IReadOnlyList<AlternateDataStream> alternateDataStreams;
    private readonly short expectedCompressionFormat;
    private readonly bool preserveCanonicalMetadata;
    private bool ownsStage;
    private bool finalized;
    private bool disposed;
    private FileStream? stream;

    private WindowsCheckpointStage(
        string stagePath,
        string? canonicalPath,
        FileSecurity expectedSecurity,
        FileAttributes expectedAttributes,
        DateTime? expectedCreationTimeUtc,
        IReadOnlyList<AlternateDataStream> alternateDataStreams,
        short expectedCompressionFormat,
        bool preserveCanonicalMetadata)
    {
        this.stagePath = stagePath;
        this.canonicalPath = canonicalPath;
        this.expectedSecurity = expectedSecurity;
        this.expectedAttributes = expectedAttributes;
        this.expectedCreationTimeUtc = expectedCreationTimeUtc;
        this.alternateDataStreams = alternateDataStreams;
        this.expectedCompressionFormat = expectedCompressionFormat;
        this.preserveCanonicalMetadata = preserveCanonicalMetadata;
    }

    public FileStream Stream
        => stream ?? throw new InvalidOperationException(
            "The Windows checkpoint stage is not writable.");

    /**
     * Creates a stage directly beneath an existing owned directory. The
     * returned Stream is exposed only after the stage security descriptor has
     * been installed and verified.
     */
    public static WindowsCheckpointStage Create(
        string stagePath,
        string canonicalPath,
        string stageDirectory)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(stagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageDirectory);

        stagePath = Path.GetFullPath(stagePath);
        canonicalPath = Path.GetFullPath(canonicalPath);
        stageDirectory = Path.GetFullPath(stageDirectory);
        ValidateStageLocation(stagePath, stageDirectory);
        ValidateDirectory(stageDirectory);
        ValidateSecurityFeatures(stageDirectory);
        var stageDirectorySecurity = new DirectoryInfo(stageDirectory)
            .GetAccessControl(SupportedSecuritySections);
        RejectConditionalDacl(stageDirectorySecurity);

        var canonicalExists = TryGetRegularFileAttributes(canonicalPath, out var attributes);
        if (canonicalExists)
            ValidateSecurityFeatures(canonicalPath);
        var security = canonicalExists
            ? new FileInfo(canonicalPath).GetAccessControl(SupportedSecuritySections)
            : null;
        if (security is not null)
            RejectConditionalDacl(security);
        var protectedCreationSecurity = security is null ? null : ProtectedCopy(security);
        if (canonicalExists && (attributes & FileAttributes.Encrypted) != 0)
            throw Unsupported("EFS encryption");
        var expectedAttributes = canonicalExists
            ? ValidateAndGetSupportedAttributes(attributes)
            : FileAttributes.Normal;
        var expectedCompressionFormat = canonicalExists
            ? GetCompressionFormat(canonicalPath)
            : (File.GetAttributes(stageDirectory) & FileAttributes.Compressed) != 0
                ? CompressionFormatLznt1
                : CompressionFormatNone;
        if (!canonicalExists && (File.GetAttributes(stageDirectory) & FileAttributes.Encrypted) != 0)
            throw Unsupported("an encrypted stage directory");
        if (canonicalExists && HasObjectId(canonicalPath))
            throw Unsupported("an object identifier");

        DateTime? creationTimeUtc = canonicalExists
            ? File.GetCreationTimeUtc(canonicalPath)
            : null;
        IReadOnlyList<AlternateDataStream> streams = canonicalExists
            ? OpenNamedStreams(canonicalPath)
            : [];
        WindowsCheckpointStage? result = null;
        var stageCreated = false;
        try {
            if (protectedCreationSecurity is null) {
                using var initial = new FileStream(
                    stagePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.WriteThrough);
                stageCreated = true;
                security = new FileInfo(stagePath)
                    .GetAccessControl(SupportedSecuritySections);
            } else {
                using var initial = new FileInfo(stagePath).Create(
                    FileMode.CreateNew,
                    StageRights,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.WriteThrough,
                    protectedCreationSecurity);
                stageCreated = true;
                VerifySecurity(
                    stagePath,
                    protectedCreationSecurity,
                    "creation",
                    allowDaclAutoInheritedDifference: true);
            }

            VerifySupportedAttributes(File.GetAttributes(stagePath), "created stage");
            result = new WindowsCheckpointStage(
                stagePath,
                canonicalExists ? canonicalPath : null,
                security!,
                expectedAttributes,
                creationTimeUtc,
                streams,
                expectedCompressionFormat,
                canonicalExists) {
                ownsStage = true,
            };
            if (protectedCreationSecurity is null)
                VerifySecurity(stagePath, result.expectedSecurity, "first stage");

            SetCompressionState(stagePath, expectedCompressionFormat);
            VerifyCompressionState(stagePath, expectedCompressionFormat);
            ApplyAndVerifyFinalSecurity(result);
            result.CopyAlternateDataStreams();
            result.stream = new FileStream(
                stagePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough);
            VerifySecurity(stagePath, result.expectedSecurity, "final stage");
            return result;
        } catch (Exception primary) {
            try {
                if (result is not null)
                    result.Dispose();
                else {
                    try {
                        DisposeNamedStreams(streams);
                    } finally {
                        if (stageCreated)
                            TryDeleteStagePath(stagePath);
                    }
                }
            } catch (Exception secondary) {
                primary.Data["WindowsCheckpointStage.CleanupException"] = secondary;
            }
            throw;
        }
    }

    /**
     * Creates a dedicated stage directory without changing the root or its
     * parent. Existing directories are never rewritten by this method.
     */
    public static void EnsureStageDirectory(string rootDirectory, string stageDirectory)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageDirectory);
        rootDirectory = Path.GetFullPath(rootDirectory);
        stageDirectory = Path.GetFullPath(stageDirectory);
        if (PathEquals(rootDirectory, stageDirectory)
            || !PathEquals(Path.GetDirectoryName(stageDirectory), rootDirectory))
            throw new ArgumentException(
                "The Windows checkpoint stage directory must be a direct child of the root.",
                nameof(stageDirectory));
        ValidateDirectory(rootDirectory);
        ValidateSecurityFeatures(rootDirectory);

        var rootSecurity = new DirectoryInfo(rootDirectory)
            .GetAccessControl(SupportedSecuritySections);
        RejectConditionalDacl(rootSecurity);
        var stageInfo = new DirectoryInfo(stageDirectory);
        if (stageInfo.Exists) {
            ValidateDirectory(stageDirectory);
            VerifySecurity(
                stageDirectory,
                rootSecurity,
                "existing stage directory",
                isDirectory: true);
            return;
        }

        var protectedCreationSecurity = ProtectedCopy(rootSecurity);
        var result = CreateDirectoryWithSecurity(stageDirectory, protectedCreationSecurity);
        if (!result) {
            VerifySecurity(stageDirectory, rootSecurity, "existing stage directory", isDirectory: true);
            return;
        }
        try {
            VerifySecurity(
                stageDirectory,
                protectedCreationSecurity,
                "stage directory creation",
                isDirectory: true,
                allowDaclAutoInheritedDifference: true);
            stageInfo.SetAccessControl(CloneAccessOnly(rootSecurity));
            VerifySecurity(stageDirectory, rootSecurity, "stage directory", isDirectory: true);
        } catch {
            if (result)
                TryDeleteDirectory(stageDirectory);
            throw;
        }
    }

    /** Flushes the checkpoint payload. No writes are possible before Create returns. */
    public void WriteAndFlush(ReadOnlySpan<byte> bytes)
    {
        ThrowIfDisposedOrFinalized();
        stream!.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    /**
     * Applies supported replacement metadata after payload flush and closes
     * the writable handle. Last-write and last-access times intentionally stay
     * fresh for the new payload; only the documented creation time is copied.
     */
    public void FinalizeMetadataAndClose()
    {
        ThrowIfDisposed();
        if (finalized)
            return;

        try {
            stream?.Flush(flushToDisk: true);
            stream?.Dispose();
            stream = null;
            if (preserveCanonicalMetadata && expectedCreationTimeUtc is not null)
                File.SetCreationTimeUtc(stagePath, expectedCreationTimeUtc.Value);

            SetCompressionState(stagePath, expectedCompressionFormat);
            if (preserveCanonicalMetadata) {
                var desiredAttributes = expectedAttributes & SupportedBasicAttributes;
                File.SetAttributes(
                    stagePath,
                    desiredAttributes == 0 ? FileAttributes.Normal : desiredAttributes);
                VerifyBasicAttributes(stagePath, expectedAttributes);
            } else {
                VerifySupportedAttributes(File.GetAttributes(stagePath), "stage");
            }
            VerifyCompressionState(stagePath, expectedCompressionFormat);
            VerifySecurity(stagePath, expectedSecurity, "finalized stage");
            finalized = true;
        } catch (Exception primary) {
            DisposeStreamPreservingPrimary(primary);
            throw;
        }
    }

    /**
     * Transfers stage-path ownership to the successful publication operation.
     * Call this immediately after the atomic rename succeeds and before any
     * post-publication callback can recreate the old stage pathname.
     */
    public void MarkPublished()
    {
        ThrowIfDisposed();
        if (!finalized)
            throw new InvalidOperationException(
                "A Windows checkpoint stage cannot be published before finalization.");
        ownsStage = false;
    }

    /** Descriptive alias for callers that model publication as ownership transfer. */
    public void ReleaseStageOwnership()
        => MarkPublished();

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Exception? cleanupFailure = null;
        try {
            stream?.Dispose();
        } catch (Exception exception) {
            cleanupFailure = exception;
        } finally {
            stream = null;
        }
        try {
            DisposeNamedStreams(alternateDataStreams);
        } catch (Exception exception) {
            if (cleanupFailure is null)
                cleanupFailure = exception;
            else
                cleanupFailure.Data["WindowsCheckpointStage.StreamCleanupException"] = exception;
        }
        if (ownsStage)
            TryDeleteStage();
        if (cleanupFailure is not null)
            throw cleanupFailure;
    }

    private static readonly FileSystemRights StageRights =
        FileSystemRights.ReadData
        | FileSystemRights.WriteData
        | FileSystemRights.AppendData
        | FileSystemRights.ReadAttributes
        | FileSystemRights.WriteAttributes
        | FileSystemRights.ReadExtendedAttributes
        | FileSystemRights.WriteExtendedAttributes
        | FileSystemRights.ReadPermissions
        | FileSystemRights.Delete
        | FileSystemRights.Synchronize;

    private void CopyAlternateDataStreams()
    {
        foreach (var alternateDataStream in alternateDataStreams) {
            var destinationPath = stagePath + alternateDataStream.Name;
            var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough);
            Exception? primaryFailure = null;
            try {
                SetCompressionState(destination.SafeFileHandle, alternateDataStream.CompressionFormat);
                VerifyCompressionState(destination.SafeFileHandle, alternateDataStream.CompressionFormat);
                alternateDataStream.Source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
            } catch (Exception primary) {
                primaryFailure = primary;
                throw;
            } finally {
                try {
                    destination.Dispose();
                } catch (Exception secondary) when (primaryFailure is not null) {
                    primaryFailure.Data["WindowsCheckpointStage.StreamCleanupException"] = secondary;
                }
            }
        }
    }

    private void DisposeStreamPreservingPrimary(Exception primary)
    {
        try {
            stream?.Dispose();
        } catch (Exception secondary) {
            primary.Data["WindowsCheckpointStage.StreamCleanupException"] = secondary;
        } finally {
            stream = null;
        }
    }

    private static void DisposeNamedStreams(IReadOnlyList<AlternateDataStream> streams)
    {
        Exception? first = null;
        foreach (var stream in streams) {
            try {
                stream.Dispose();
            } catch (Exception exception) {
                first ??= exception;
            }
        }
        if (first is not null)
            throw first;
    }

    private static void ApplyAndVerifyFinalSecurity(WindowsCheckpointStage stage)
    {
        new FileInfo(stage.stagePath).SetAccessControl(CloneAccessOnly(stage.expectedSecurity));
        VerifySecurity(stage.stagePath, stage.expectedSecurity, "final security");
    }

    private void TryDeleteStage()
        => TryDeleteStagePath(stagePath);

    private static void TryDeleteStagePath(string path)
    {
        try {
            if (!File.Exists(path))
                return;
            File.Delete(path);
        } catch {
            try {
                if (File.Exists(path))
                    File.SetAttributes(path, FileAttributes.Normal);
                if (File.Exists(path))
                    File.Delete(path);
            } catch {
                // Cleanup must never replace the publication or metadata error.
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try {
            if (Directory.Exists(path))
                Directory.Delete(path);
        } catch {
            // Directory cleanup is best effort after a failed owned create.
        }
    }

    private static bool CreateDirectoryWithSecurity(
        string path,
        DirectorySecurity security)
    {
        var descriptor = security.GetSecurityDescriptorBinaryForm();
        var descriptorPointer = Marshal.AllocHGlobal(descriptor.Length);
        try {
            Marshal.Copy(descriptor, 0, descriptorPointer, descriptor.Length);
            var securityAttributes = new SecurityAttributes {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = descriptorPointer,
                InheritHandle = 0,
            };
            if (CreateDirectoryW(path, ref securityAttributes))
                return true;
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorAlreadyExists) {
                ValidateDirectory(path);
                return false;
            }
            throw Win32Failure("Could not create the checkpoint stage directory.", error);
        } finally {
            Marshal.FreeHGlobal(descriptorPointer);
        }
    }

    private static FileSecurity ProtectedCopy(FileSecurity source)
    {
        var clone = Clone(source);
        if (!clone.AreAccessRulesProtected)
            clone.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
        return clone;
    }

    private static DirectorySecurity ProtectedCopy(DirectorySecurity source)
    {
        var clone = Clone(source);
        if (!clone.AreAccessRulesProtected)
            clone.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
        return clone;
    }

    private static FileSecurity Clone(FileSecurity source)
    {
        var clone = new FileSecurity();
        clone.SetSecurityDescriptorBinaryForm(
            source.GetSecurityDescriptorBinaryForm(),
            SupportedSecuritySections);
        return clone;
    }

    private static DirectorySecurity Clone(DirectorySecurity source)
    {
        var clone = new DirectorySecurity();
        clone.SetSecurityDescriptorBinaryForm(
            source.GetSecurityDescriptorBinaryForm(),
            SupportedSecuritySections);
        return clone;
    }

    private static FileSecurity CloneAccessOnly(FileSecurity source)
    {
        var clone = new FileSecurity();
        clone.SetSecurityDescriptorBinaryForm(
            source.GetSecurityDescriptorBinaryForm(),
            AccessControlSections.Access);
        return clone;
    }

    private static DirectorySecurity CloneAccessOnly(DirectorySecurity source)
    {
        var clone = new DirectorySecurity();
        clone.SetSecurityDescriptorBinaryForm(
            source.GetSecurityDescriptorBinaryForm(),
            AccessControlSections.Access);
        return clone;
    }

    private static void VerifySecurity(
        string path,
        ObjectSecurity expected,
        string phase,
        bool isDirectory = false,
        bool allowDaclAutoInheritedDifference = false)
    {
        ValidateSecurityFeatures(path);
        ObjectSecurity actual = isDirectory
            ? new DirectoryInfo(path).GetAccessControl(SupportedSecuritySections)
            : new FileInfo(path).GetAccessControl(SupportedSecuritySections);
        RejectConditionalDacl(actual);
        RejectConditionalDacl(expected);
        if (!SecurityEquivalent(expected, actual, allowDaclAutoInheritedDifference))
            throw Unsupported($"the {phase} security descriptor differs from the requested owner/group/DACL");
    }

    private static void ValidateSecurityFeatures(string path)
    {
        QuerySecurityInformation(
            path,
            SpecialSecurityInformation,
            "access-affecting security labels, resource attributes, or scopes");

        var result = GetNamedSecurityInfoW(
            path,
            SeFileObject,
            SaclSecurityInformation,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            out var descriptor);
        if (result == 0) {
            try {
                if (HasSystemAcl(descriptor))
                    throw Unsupported("an audit SACL");
            } finally {
                FreeSecurityDescriptor(descriptor);
            }
        } else if (result is not (ErrorAccessDenied or ErrorPrivilegeNotHeld)) {
            throw Win32Failure("Could not determine checkpoint audit-SACL state.", result);
        }
        // Audit SACLs are deliberately unsupported and are never copied. A
        // normal user cannot inspect them without ACCESS_SYSTEM_SECURITY.
    }

    private static void QuerySecurityInformation(
        string path,
        uint securityInformation,
        string unsupportedDescription)
    {
        var result = GetNamedSecurityInfoW(
            path,
            SeFileObject,
            securityInformation,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            out var descriptor);
        if (result != 0)
            throw Win32Failure(
                $"Could not determine checkpoint {unsupportedDescription} state.",
                result);
        try {
            if (HasSystemAcl(descriptor))
                throw Unsupported(unsupportedDescription);
        } finally {
            FreeSecurityDescriptor(descriptor);
        }
    }

    private static bool HasSystemAcl(IntPtr descriptor)
    {
        if (descriptor == IntPtr.Zero)
            return false;
        var length = GetSecurityDescriptorLength(descriptor);
        if (length <= 0)
            throw Unsupported("an invalid security descriptor");
        var bytes = new byte[length];
        Marshal.Copy(descriptor, bytes, 0, length);
        var raw = new RawSecurityDescriptor(bytes, 0);
        return raw.SystemAcl is { Count: > 0 };
    }

    private static void FreeSecurityDescriptor(IntPtr descriptor)
    {
        if (descriptor != IntPtr.Zero)
            LocalFree(descriptor);
    }

    private static void RejectConditionalDacl(ObjectSecurity security)
    {
        var descriptor = new RawSecurityDescriptor(
            security.GetSecurityDescriptorBinaryForm(),
            0);
        if (descriptor.DiscretionaryAcl is null)
            return;
        foreach (GenericAce ace in descriptor.DiscretionaryAcl) {
            var type = (byte)ace.AceType;
            if (type is 0x09 or 0x0A or 0x0B or 0x0C)
                throw Unsupported("a conditional or callback DACL");
        }
    }

    private static bool SecurityEquivalent(
        ObjectSecurity expected,
        ObjectSecurity actual,
        bool allowDaclAutoInheritedDifference)
    {
        var expectedDescriptor = new RawSecurityDescriptor(
            expected.GetSecurityDescriptorBinaryForm(),
            0);
        var actualDescriptor = new RawSecurityDescriptor(
            actual.GetSecurityDescriptorBinaryForm(),
            0);
        var expectedFlags = expectedDescriptor.ControlFlags;
        var actualFlags = actualDescriptor.ControlFlags;
        if (allowDaclAutoInheritedDifference) {
            const ControlFlags autoInherited = ControlFlags.DiscretionaryAclAutoInherited;
            expectedFlags &= ~autoInherited;
            actualFlags &= ~autoInherited;
        }
        return expectedFlags == actualFlags
            && Equals(expectedDescriptor.Owner, actualDescriptor.Owner)
            && Equals(expectedDescriptor.Group, actualDescriptor.Group)
            && AclEquivalent(
                expectedDescriptor.DiscretionaryAcl,
                actualDescriptor.DiscretionaryAcl);
    }

    private static bool AclEquivalent(RawAcl? expected, RawAcl? actual)
    {
        if (expected is null || actual is null)
            return expected is null && actual is null;
        var expectedBytes = new byte[expected.BinaryLength];
        var actualBytes = new byte[actual.BinaryLength];
        expected.GetBinaryForm(expectedBytes, 0);
        actual.GetBinaryForm(actualBytes, 0);
        return expectedBytes.AsSpan().SequenceEqual(actualBytes);
    }

    private static string SecurityForm(ObjectSecurity security)
        => security.GetSecurityDescriptorSddlForm(SupportedSecuritySections);

    private static FileAttributes ValidateAndGetSupportedAttributes(FileAttributes attributes)
    {
        var unsupported = (attributes & ~KnownAttributes) | (attributes & UnsupportedAttributes);
        if (unsupported != 0)
            throw Unsupported($"file attributes '{unsupported}'");
        return attributes;
    }

    private void VerifyBasicAttributes(string path, FileAttributes expected)
    {
        var actual = File.GetAttributes(path);
        VerifySupportedAttributes(actual, "stage");
        if ((actual & SupportedBasicAttributes) != (expected & SupportedBasicAttributes))
            throw Unsupported("basic attributes could not be preserved");
    }

    private static void VerifySupportedAttributes(FileAttributes attributes, string subject)
    {
        var unsupported = (attributes & ~KnownAttributes) | (attributes & UnsupportedAttributes);
        if (unsupported != 0)
            throw Unsupported($"{subject} attributes '{unsupported}'");
    }

    private static void SetCompressionState(string path, short compressionFormat)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            options: FileOptions.None);
        SetCompressionState(stream.SafeFileHandle, compressionFormat);
    }

    private static void SetCompressionState(SafeFileHandle handle, short compressionFormat)
    {
        short format = compressionFormat;
        if (!DeviceIoControl(
                handle,
                FsctlSetCompression,
                ref format,
                sizeof(short),
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero))
            throw Win32Failure("Could not set checkpoint compression state.");
    }

    private static void VerifyCompressionState(string path, short expected)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            options: FileOptions.None);
        VerifyCompressionState(stream.SafeFileHandle, expected);
    }

    private static void VerifyCompressionState(SafeFileHandle handle, short expected)
    {
        var actual = GetCompressionFormat(handle);
        if (actual != expected)
            throw Unsupported("compression state could not be preserved");
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
            throw Win32Failure("Could not determine checkpoint compression state.");
        if (format is not (CompressionFormatNone or CompressionFormatLznt1))
            throw Unsupported($"compression format {format}");
        return format;
    }

    private static bool HasObjectId(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            options: FileOptions.None);
        var buffer = new byte[64];
        if (DeviceIoControl(
                stream.SafeFileHandle,
                FsctlGetObjectId,
                IntPtr.Zero,
                0,
                buffer,
                buffer.Length,
                out _,
                IntPtr.Zero))
            return true;
        var error = Marshal.GetLastWin32Error();
        if (error is ErrorFileNotFound or ErrorNotFound)
            return false;
        throw Win32Failure("Could not determine checkpoint object-identifier state.", error);
    }

    private static IReadOnlyList<AlternateDataStream> OpenNamedStreams(string path)
    {
        var streams = new List<AlternateDataStream>();
        var handle = FindFirstStreamW(path, 0, out var data, 0);
        if (handle == InvalidHandleValue) {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorHandleEof)
                return streams;
            throw Win32Failure("Could not enumerate checkpoint streams.", error);
        }
        try {
            while (true) {
                if (!string.Equals(data.StreamName, "::$DATA", StringComparison.OrdinalIgnoreCase)) {
                    if (!data.StreamName.EndsWith(":$DATA", StringComparison.OrdinalIgnoreCase))
                        throw Unsupported($"named stream '{data.StreamName}'");
                    streams.Add(OpenNamedStream(path, data.StreamName));
                }
                if (FindNextStreamW(handle, out data))
                    continue;
                var error = Marshal.GetLastWin32Error();
                if (error == ErrorHandleEof)
                    break;
                throw Win32Failure("Could not enumerate checkpoint streams.", error);
            }
        } catch (Exception primary) {
            try {
                DisposeNamedStreams(streams);
            } catch (Exception secondary) {
                primary.Data["WindowsCheckpointStage.StreamCleanupException"] = secondary;
            }
            throw;
        } finally {
            FindClose(handle);
        }
        return streams;
    }

    private static AlternateDataStream OpenNamedStream(string path, string streamName)
    {
        var stream = new FileStream(
            path + streamName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);
        try {
            var attributes = GetStreamAttributes(stream.SafeFileHandle);
            var unsupported = (attributes & ~KnownAttributes) | (attributes & UnsupportedAttributes);
            if (unsupported != 0)
                throw Unsupported($"named stream '{streamName}' attributes '{unsupported}'");
            var compressionFormat = GetCompressionFormat(stream.SafeFileHandle);
            return new AlternateDataStream(streamName, stream, compressionFormat);
        } catch (Exception primary) {
            try {
                stream.Dispose();
            } catch (Exception secondary) {
                primary.Data["WindowsCheckpointStage.StreamCleanupException"] = secondary;
            }
            throw;
        }
    }

    private static FileAttributes GetStreamAttributes(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandleEx(
                handle,
                FileBasicInfoClass,
                out var information,
                Marshal.SizeOf<FileBasicInfo>()))
            throw Win32Failure("Could not determine named stream attributes.");
        return (FileAttributes)information.FileAttributes;
    }

    private static bool TryGetRegularFileAttributes(string path, out FileAttributes attributes)
    {
        try {
            attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new IOException("The checkpoint canonical path is not a regular file.");
            return true;
        } catch (FileNotFoundException) {
            attributes = FileAttributes.Normal;
            return false;
        } catch (DirectoryNotFoundException) {
            attributes = FileAttributes.Normal;
            return false;
        }
    }

    private static void ValidateStageLocation(string stagePath, string stageDirectory)
    {
        if (!PathEquals(Path.GetDirectoryName(stagePath), stageDirectory))
            throw new ArgumentException(
                "The checkpoint stage must be a direct child of its owned stage directory.",
                nameof(stagePath));
        if (PathEquals(stagePath, stageDirectory))
            throw new ArgumentException("The checkpoint stage must be a file path.", nameof(stagePath));
    }

    private static void ValidateDirectory(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Directory) == 0)
            throw new IOException("The checkpoint stage directory is not a directory.");
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The checkpoint stage directory must not be a reparse point.");
    }

    private static bool PathEquals(string? left, string? right)
        => left is not null
            && right is not null
            && string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Windows checkpoint staging is available only on Windows.");
    }

    private void ThrowIfDisposedOrFinalized()
    {
        ThrowIfDisposed();
        if (finalized)
            throw new InvalidOperationException("The Windows checkpoint stage is finalized.");
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(WindowsCheckpointStage));
    }

    private static IOException Unsupported(string detail)
        => new($"The Windows checkpoint stage cannot preserve {detail}.");

    private static IOException Win32Failure(string message, int? error = null)
    {
        var code = error ?? Marshal.GetLastWin32Error();
        return new IOException(message, new Win32Exception(code));
    }

    private static readonly IntPtr InvalidHandleValue = new(-1);

    private const int FileBasicInfoClass = 0;

    private sealed class AlternateDataStream : IDisposable
    {
        public AlternateDataStream(string name, FileStream source, short compressionFormat)
        {
            Name = name;
            Source = source;
            CompressionFormat = compressionFormat;
        }

        public string Name { get; }

        public FileStream Source { get; }

        public short CompressionFormat { get; }

        public void Dispose()
            => Source.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindStreamData
    {
        public long StreamSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = StreamNameCapacity)]
        public string StreamName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileBasicInfo
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint FileAttributes;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(
        string path,
        ref SecurityAttributes securityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstStreamW(
        string fileName,
        int infoLevel,
        out FindStreamData data,
        uint reserved);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(
        IntPtr findStreamHandle,
        out FindStreamData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr findStreamHandle);

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
        [Out] byte[] outputBuffer,
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
}
