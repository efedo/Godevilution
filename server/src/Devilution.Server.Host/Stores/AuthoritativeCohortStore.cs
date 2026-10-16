using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Microsoft.Win32.SafeHandles;

namespace Devilution.Server.Stores;

/**
 * Owns one complete host checkpoint and an exclusive lifetime lease for its
 * root. The cohort file is independent of all legacy per-session files.
 */
public sealed class AuthoritativeCohortStore : IDisposable
{
    public const int CurrentFormatVersion = 1;
    public const string CheckpointFileName = "authoritative-cohort.json";
    private const string LeaseFileName = "authoritative-cohort.lease";
    private const int Sha256HexLength = 64;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.Strict,
    };

    private readonly object synchronization = new();
    private readonly FileStream lease;
    private AuthoritativeCohortPublicationLayout? publicationLayout;
    private bool hostPublicationRequested;
    private bool disposed;
    // SaveCore reuses this detached prior only after rereading exact canonical bytes.
    private byte[]? cachedCheckpointBytes;
    private AuthoritativeCohortDocument? cachedCheckpointDocument;

    public AuthoritativeCohortStore(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A cohort root directory is required.", nameof(rootDirectory));

        RootDirectory = Path.GetFullPath(rootDirectory);
        if (string.Equals(
                RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetPathRoot(RootDirectory)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The cohort root must be a dedicated directory.", nameof(rootDirectory));

        Directory.CreateDirectory(RootDirectory);
        var rootInfo = new DirectoryInfo(RootDirectory);
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The cohort root must not be a reparse point.");

        try {
            var leasePath = Path.Combine(RootDirectory, LeaseFileName);
            try {
                var leaseAttributes = File.GetAttributes(leasePath);
                if ((leaseAttributes
                    & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw new IOException("The cohort lease must be a regular file.");
            } catch (FileNotFoundException) {
                // The exclusive lease is created below.
            } catch (DirectoryNotFoundException) {
                // The root was created above; this is only a defensive race guard.
            }
            lease = new FileStream(
                leasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                options: FileOptions.WriteThrough);
        } catch {
            // A second live store must fail on the OS lease. Never remove a
            // lease file: stale-PID heuristics could destroy another owner.
            throw;
        }
    }

    public string RootDirectory { get; }

    public string CheckpointPath => Path.Combine(RootDirectory, CheckpointFileName);

    /** Explicit host-only publication observer used by managed interruption fixtures. */
    internal Action<AuthoritativePublicationPhase, string>? PublicationObserver { get; set; }

    /**
     * Creates the cohort-aware seed store after host startup has authorized the
     * marker/staging layout. The ordinary seed-store constructor remains
     * unchanged for legacy persistence and direct callers.
     */
    public AuthoritativeWorldSeedStore CreateWorldSeedStore()
    {
        lock (synchronization) {
            ThrowIfDisposed();
            return publicationLayout is null
                ? throw new InvalidOperationException(
                    "The host publication layout has not been prepared.")
                : new AuthoritativeWorldSeedStore(RootDirectory, publicationLayout, this);
        }
    }

    /** Keeps a factory-created seed operation within its original lifetime lease. */
    internal T ExecuteSeedOperation<T>(AuthoritativeCohortPublicationLayout layout, Func<T> operation)
    {
        lock (synchronization) {
            ThrowIfDisposed();
            if (!ReferenceEquals(publicationLayout, layout))
                throw new InvalidOperationException("The seed store does not own this host publication layout.");
            layout.ValidateCanonicalForSave();
            layout.ValidateSeedAccess();
            return operation();
        }
    }

    /** Validates and prepares the host-only first-publication namespace. */
    internal void PrepareHostPublicationLayout()
    {
        lock (synchronization) {
            ThrowIfDisposed();
            hostPublicationRequested = true;
            // Validate the complete canonical envelope before any marker or
            // staging metadata is created. A malformed checkpoint must remain
            // the sole startup failure and must not gain recovery authority.
            var canonicalExists = Load() is not null;
            publicationLayout ??= AuthoritativeCohortPublicationLayout.Prepare(
                RootDirectory,
                canonicalExists);
        }
    }

    /** Loads and validates the complete checkpoint, if one has been published. */
    public AuthoritativeCohortDocument? Load(
        AuthoritativeCohortIdentity? expectedIdentity = null)
    {
        lock (synchronization) {
            ThrowIfDisposed();
            var checkpointExists = ValidateCheckpointPathTarget();
            if (!checkpointExists)
                return null;
            var document = ReadDocument(File.ReadAllBytes(CheckpointPath));
            if (expectedIdentity is not null && document.Identity != expectedIdentity)
                throw new InvalidDataException("The cohort checkpoint identity does not match the active host.");
            return document;
        }
    }

    /**
     * Publishes the next exact generation. Caller-owned collections are
     * detached and sorted before serialization; the existing file is read and
     * validated before any replacement is attempted.
     */
    public void Save(AuthoritativeCohortDocument document)
        => SaveCore(document, null, allowIdentityTransition: false);

    /**
     * Publishes a verified ruleset transition. The prior checkpoint must
     * exist and match the supplied identity exactly; only content, ruleset,
     * and module-format identity fields may change.
     */
    public void Save(
        AuthoritativeCohortDocument document,
        AuthoritativeCohortIdentity expectedPreviousIdentity)
    {
        ArgumentNullException.ThrowIfNull(expectedPreviousIdentity);
        SaveCore(document, expectedPreviousIdentity, allowIdentityTransition: true);
    }

    private void SaveCore(
        AuthoritativeCohortDocument document,
        AuthoritativeCohortIdentity? expectedPreviousIdentity,
        bool allowIdentityTransition)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (synchronization) {
            ThrowIfDisposed();
            if (publicationLayout is not null)
                publicationLayout.ValidateCanonicalForSave();
            else if (hostPublicationRequested)
                throw new InvalidOperationException("Host publication preparation has not succeeded; generic publication is not allowed.");
            var normalized = Normalize(document);
            var checkpointExists = ValidateCheckpointPathTarget();
            var previousBytes = checkpointExists ? File.ReadAllBytes(CheckpointPath) : null;
            AuthoritativeCohortDocument? previous = null;
            if (previousBytes is not null)
                previous = cachedCheckpointBytes is not null
                    && cachedCheckpointDocument is not null
                    && previousBytes.AsSpan().SequenceEqual(cachedCheckpointBytes)
                    ? cachedCheckpointDocument
                    : ReadDocument(previousBytes);
            var expectedGeneration = previous is null
                ? 1UL
                : previous.Generation == ulong.MaxValue
                    ? throw new InvalidOperationException("The cohort generation space is exhausted.")
                    : previous.Generation + 1;
            if (allowIdentityTransition && previous is null)
                throw new InvalidDataException("A verified identity transition requires an existing checkpoint.");
            if (previous is not null && expectedPreviousIdentity is not null
                && previous.Identity != expectedPreviousIdentity)
                throw new InvalidDataException("The expected previous cohort identity does not match the checkpoint.");
            if (previous is not null) {
                if (allowIdentityTransition)
                    ValidateIdentityTransition(previous.Identity, normalized.Identity);
                else if (normalized.Identity != previous.Identity)
                    throw new InvalidDataException("The cohort identity cannot change within one generation chain.");
                ValidateProgression(previous, normalized);
            }
            if (normalized.Generation != expectedGeneration)
                throw new InvalidDataException(
                    $"The cohort generation must be exactly {expectedGeneration}.");

            var payload = ToPayload(normalized);
            var integrity = ComputeIntegrity(payload);
            var persisted = new PersistedRoot(
                payload.FormatVersion,
                payload.Generation,
                payload.Identity,
                payload.Tick,
                payload.NextEntityId,
                payload.NextServerReceiptSequence,
                payload.Participants,
                payload.SerializedModuleState,
                integrity);
            var bytes = StrictUtf8.GetBytes(JsonSerializer.Serialize(persisted, JsonOptions));
            var nextCachedBytes = bytes.ToArray();
            var nextCachedDocument = normalized;
            PublishAtomically(bytes);
            cachedCheckpointBytes = nextCachedBytes;
            cachedCheckpointDocument = nextCachedDocument;
        }
    }

    private static void ValidateIdentityTransition(
        AuthoritativeCohortIdentity previous,
        AuthoritativeCohortIdentity next)
    {
        if (!string.Equals(previous.ModuleId, next.ModuleId, StringComparison.Ordinal)
            || !string.Equals(previous.ProtocolSchemaVersion, next.ProtocolSchemaVersion, StringComparison.Ordinal)
            || previous.TickRateHz != next.TickRateHz
            || previous.GameMode != next.GameMode)
            throw new InvalidDataException(
                "A cohort identity transition changed a host-stable identity field.");
    }

    private static void ValidateProgression(
        AuthoritativeCohortDocument previous,
        AuthoritativeCohortDocument next)
    {
        if (next.Tick < previous.Tick)
            throw new InvalidDataException("The cohort tick cannot move backwards.");
        ValidateCursorProgression(
            previous.NextEntityId,
            next.NextEntityId,
            "entity");
        ValidateCursorProgression(
            previous.NextServerReceiptSequence,
            next.NextServerReceiptSequence,
            "server receipt");

        var previousParticipants = previous.Participants.ToDictionary(
            participant => participant.SessionId,
            StringComparer.Ordinal);
        var nextParticipants = next.Participants.ToDictionary(
            participant => participant.SessionId,
            StringComparer.Ordinal);
        foreach (var previousParticipant in previous.Participants) {
            if (!nextParticipants.TryGetValue(previousParticipant.SessionId, out var nextParticipant))
                throw new InvalidDataException("A persisted cohort participant cannot be removed.");
            if (nextParticipant.EntityId != previousParticipant.EntityId)
                throw new InvalidDataException("A persisted cohort entity binding cannot change.");

            var previousReceipts = previousParticipant.CommandReceipts.ToDictionary(
                receipt => receipt.ClientSequence);
            var nextReceipts = nextParticipant.CommandReceipts.ToDictionary(
                receipt => receipt.ClientSequence);
            foreach (var previousReceipt in previousParticipant.CommandReceipts) {
                if (!nextReceipts.TryGetValue(previousReceipt.ClientSequence, out var nextReceipt)
                    || !ReceiptsEqual(previousReceipt, nextReceipt))
                    throw new InvalidDataException("A persisted command receipt cannot be removed or changed.");
            }

            foreach (var nextReceipt in nextParticipant.CommandReceipts)
                if (!previousReceipts.ContainsKey(nextReceipt.ClientSequence)
                    && (previous.NextServerReceiptSequence == 0
                        || nextReceipt.ServerReceiptSequence < previous.NextServerReceiptSequence))
                    throw new InvalidDataException(
                        "A new command receipt must not reuse an exhausted or prior global sequence.");
        }

        foreach (var nextParticipant in next.Participants)
            if (!previousParticipants.ContainsKey(nextParticipant.SessionId)) {
                if (previous.NextEntityId == 0
                    || nextParticipant.EntityId < previous.NextEntityId)
                    throw new InvalidDataException(
                        "A new cohort participant must use a fresh entity allocation.");
                foreach (var nextReceipt in nextParticipant.CommandReceipts)
                    if (previous.NextServerReceiptSequence == 0
                        || nextReceipt.ServerReceiptSequence < previous.NextServerReceiptSequence)
                        throw new InvalidDataException(
                            "A new participant receipt must not reuse an exhausted or prior global sequence.");
            }
    }

    private static bool ReceiptsEqual(CommandResult left, CommandResult right)
        => left.ClientSequence == right.ClientSequence
            && left.ServerReceiptSequence == right.ServerReceiptSequence
            && left.AppliedTick == right.AppliedTick
            && left.Status == right.Status
            && left.RejectReason == right.RejectReason;

    private static void ValidateCursorProgression(
        ulong previous,
        ulong next,
        string name)
    {
        if (previous == 0 && next != 0
            || previous != 0 && next != 0 && next < previous)
            throw new InvalidDataException($"The {name} cursor cannot move backwards or reopen exhaustion.");
    }

    private static void ValidateCursorProgression(
        uint previous,
        uint next,
        string name)
    {
        if (previous == 0 && next != 0
            || previous != 0 && next != 0 && next < previous)
            throw new InvalidDataException($"The {name} cursor cannot move backwards or reopen exhaustion.");
    }

    public void Dispose()
    {
        lock (synchronization) {
            if (disposed)
                return;
            disposed = true;
            lease.Dispose();
        }
    }

    private void PublishAtomically(byte[] bytes)
    {
        var checkpointExists = ValidateCheckpointPathTarget();
        var temporaryPath = publicationLayout?.CreateStagePath(CheckpointFileName)
            ?? Path.Combine(
                RootDirectory,
                $".{CheckpointFileName}.{Guid.NewGuid():N}.tmp");
        Exception? primaryFailure = null;
        var temporaryOwned = false;
        var observer = PublicationObserver;
        WindowsCheckpointStage? secureStage = null;
        try {
            if (OperatingSystem.IsWindows()) {
                secureStage = WindowsCheckpointStage.Create(
                    temporaryPath, CheckpointPath, Path.GetDirectoryName(temporaryPath)!);
                AuthoritativePublicationWriter.Write(secureStage.Stream, bytes, temporaryPath, observer);
                secureStage.FinalizeMetadataAndClose();
            } else {
                using var output = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       options: FileOptions.WriteThrough);
                temporaryOwned = true;
                AuthoritativePublicationWriter.Write(output, bytes, temporaryPath, observer);
            }

            observer?.Invoke(
                AuthoritativePublicationPhase.AfterFlushBeforeRename,
                temporaryPath);

            // Keep publication to one same-volume rename. Windows ReplaceFile
            // can first rename the old destination to an internal ~RF temporary
            // name; process termination there leaves no canonical checkpoint.
            // The Windows stage already has the verified target metadata profile.
            if (checkpointExists && OperatingSystem.IsWindows())
                RenameWindowsCheckpoint(temporaryPath, CheckpointPath);
            else
                File.Move(temporaryPath, CheckpointPath, overwrite: checkpointExists);
            // The owned resource is the created file, not its old pathname.
            // Release cleanup ownership before any observer can recreate that
            // pathname after the canonical rename.
            if (OperatingSystem.IsWindows())
                secureStage?.MarkPublished();
            temporaryOwned = false;
            observer?.Invoke(
                AuthoritativePublicationPhase.AfterCanonicalRename,
                CheckpointPath);
            if (publicationLayout?.FirstPublicationPending == true) {
                publicationLayout.Commit();
                observer?.Invoke(
                    AuthoritativePublicationPhase.AfterMarkerCommit,
                    CheckpointPath);
            }
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            try {
                if (OperatingSystem.IsWindows())
                    secureStage?.Dispose();
                if (temporaryOwned && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            } catch (Exception cleanupFailure) when (primaryFailure is not null) {
                // Preserve the publication failure that explains the failed
                // checkpoint; cleanup failure must not mask it.
                primaryFailure.Data["CheckpointStageCleanupError"] = cleanupFailure;
            }
        }
    }

    // POSIX rename semantics keep existing shared/delete readers on the old
    // complete file. MoveFileEx overwrite instead refuses an open destination.
    // Unsupported filesystems/Windows versions fail closed; never fall back to
    // ReplaceFile or a delete-then-move sequence.
    private static void RenameWindowsCheckpoint(string source, string destination)
    {
        const uint deleteAccess = 0x00010000;
        const uint openExisting = 3;
        const uint normalAttributes = 0x80;
        const int fileRenameInfoEx = 22;
        const int replaceExistingWithPosixSemantics = 0x3;
        using var handle = CreateFileW(
            WindowsCheckpointPath.ForNativeApi(source),
            deleteAccess,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            openExisting,
            normalAttributes,
            IntPtr.Zero);
        if (handle.IsInvalid)
            throw RenameFailure(Marshal.GetLastPInvokeError());

        // Copy raw WCHARs, not an encoder's replacement-fallback spelling.
        var name = (WindowsCheckpointPath.ForNativeApi(destination) + '\0').ToCharArray();
        // FILE_RENAME_INFO's DWORD flags precede a pointer-aligned HANDLE,
        // DWORD byte length and the variable WCHAR name (no trailing padding).
        var rootOffset = IntPtr.Size == 8 ? 8 : 4;
        var lengthOffset = rootOffset + IntPtr.Size;
        var nameOffset = lengthOffset + sizeof(uint);
        var size = checked(nameOffset + name.Length * sizeof(char));
        var information = Marshal.AllocHGlobal(size);
        try {
            Marshal.Copy(new byte[size], 0, information, size);
            Marshal.WriteInt32(information, replaceExistingWithPosixSemantics);
            Marshal.WriteIntPtr(information, rootOffset, IntPtr.Zero);
            Marshal.WriteInt32(information, lengthOffset, checked((name.Length - 1) * sizeof(char)));
            Marshal.Copy(name, 0, IntPtr.Add(information, nameOffset), name.Length);
            if (!SetFileInformationByHandle(handle, fileRenameInfoEx, information, (uint)size))
                throw RenameFailure(Marshal.GetLastPInvokeError());
        } finally {
            Marshal.FreeHGlobal(information);
        }
    }

    private static IOException RenameFailure(int error)
        => new("Could not publish the canonical cohort by rename.", new Win32Exception(error));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int informationClass,
        IntPtr information,
        uint bufferSize);

    private static AuthoritativeCohortDocument Normalize(AuthoritativeCohortDocument document)
    {
        var detachedParticipants = document.Participants is null
            ? null
            : document.Participants
                .Select(participant => participant is null
                    ? null!
                    : new AuthoritativeCohortParticipantCheckpoint(
                        participant.SessionId,
                        participant.EntityId,
                        participant.CommandReceipts is null
                            ? null!
                            : participant.CommandReceipts
                                .Select(receipt => receipt is null ? null! : receipt.Clone())
                                .ToArray()))
                .ToArray();
        var detached = document with {
            Identity = document.Identity is null ? null! : document.Identity with { },
            Participants = detachedParticipants!,
        };
        ValidateDocument(detached);
        return detached with {
            Participants = detached.Participants
                .OrderBy(participant => participant.SessionId, StringComparer.Ordinal)
                .Select(participant => participant with {
                    CommandReceipts = participant.CommandReceipts
                        .OrderBy(receipt => receipt.ClientSequence)
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static void ValidateDocument(AuthoritativeCohortDocument document)
    {
        if (document.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException("The cohort format version is unsupported.");
        if (document.Generation == 0)
            throw new InvalidDataException("The cohort generation must start at one.");
        ValidateIdentity(document.Identity);
        if (document.SerializedModuleState is null)
            throw new InvalidDataException("The serialized module state is required.");

        var sessionIds = new HashSet<string>(StringComparer.Ordinal);
        var entityIds = new HashSet<uint>();
        ulong maximumReceipt = 0;
        uint maximumEntity = 0;
        var hasReceipt = false;
        var receiptSequences = new HashSet<ulong>();
        foreach (var participant in document.Participants ?? throw new InvalidDataException("The cohort roster is required.")) {
            if (participant is null)
                throw new InvalidDataException("The cohort roster contains a null participant.");
            ValidateSessionId(participant.SessionId);
            if (participant.EntityId == 0 || !entityIds.Add(participant.EntityId))
                throw new InvalidDataException("The cohort roster contains a duplicate or zero entity ID.");
            maximumEntity = Math.Max(maximumEntity, participant.EntityId);

            var clientSequences = new HashSet<ulong>();
            var participantReceipts = participant.CommandReceipts
                ?? throw new InvalidDataException("The participant receipt ledger is required.");
            foreach (var receipt in participantReceipts) {
                ValidateReceipt(receipt, document.Tick, clientSequences);
                if (!receiptSequences.Add(receipt.ServerReceiptSequence))
                    throw new InvalidDataException("The cohort contains a duplicate global receipt sequence.");
                hasReceipt = true;
                maximumReceipt = Math.Max(maximumReceipt, receipt.ServerReceiptSequence);
            }
            if (!sessionIds.Add(participant.SessionId))
                throw new InvalidDataException("The cohort roster contains a duplicate session ID.");
        }

        ValidateCursor(
            document.NextEntityId,
            maximumEntity,
            entityIds.Count != 0,
            "entity");
        ValidateCursor(
            document.NextServerReceiptSequence,
            maximumReceipt,
            hasReceipt,
            "server receipt");

        // StrictUtf8 rejects invalid surrogate strings before they can become
        // a different byte sequence on disk.
        try {
            _ = StrictUtf8.GetBytes(document.SerializedModuleState);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException("The serialized module state is not strict UTF-8.", exception);
        }
    }

    private static void ValidateIdentity(AuthoritativeCohortIdentity identity)
    {
        if (identity is null
            || string.IsNullOrWhiteSpace(identity.ModuleId)
            || identity.ModuleId != identity.ModuleId.Trim()
            || identity.ModuleFormatVersion <= 0
            || string.IsNullOrWhiteSpace(identity.ProtocolSchemaVersion)
            || identity.ProtocolSchemaVersion != identity.ProtocolSchemaVersion.Trim()
            || !IsSha256(identity.ContentManifestHash)
            || !IsSha256(identity.RulesetIdentityHash)
            || identity.TickRateHz == 0
            || !Enum.IsDefined(identity.GameMode))
            throw new InvalidDataException("The cohort identity is invalid.");
        ValidateStrictText(identity.ModuleId, "module ID");
        ValidateStrictText(identity.ProtocolSchemaVersion, "protocol schema version");
    }

    private bool ValidateCheckpointPathTarget()
    {
        FileAttributes attributes;
        try {
            attributes = File.GetAttributes(CheckpointPath);
        } catch (FileNotFoundException) {
            return false;
        } catch (DirectoryNotFoundException) {
            return false;
        }
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException("The cohort checkpoint path is not a regular file.");
        return true;
    }

    private static void ValidateStrictText(string value, string description)
    {
        try {
            _ = StrictUtf8.GetBytes(value);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException($"The cohort {description} is not strict UTF-16/UTF-8 text.", exception);
        }
    }

    private static void ValidateReceipt(
        CommandResult receipt,
        ulong tick,
        HashSet<ulong> clientSequences)
    {
        if (receipt is null
            || receipt.ClientSequence == 0
            || receipt.ServerReceiptSequence == 0
            || !clientSequences.Add(receipt.ClientSequence)
            || receipt.AppliedTick > tick
            || !Enum.IsDefined(receipt.Status)
            || receipt.Status == CommandStatus.Unspecified
            || !Enum.IsDefined(receipt.RejectReason)
            || receipt.Status == CommandStatus.Rejected
                && receipt.RejectReason == CommandRejectReason.Unspecified
            || receipt.Status != CommandStatus.Rejected
                && receipt.RejectReason != CommandRejectReason.Unspecified)
            throw new InvalidDataException("The cohort command receipt is invalid.");
    }

    private static void ValidateCursor<T>(
        T cursor,
        T maximum,
        bool hasValues,
        string name)
        where T : struct, IComparable<T>, IConvertible
    {
        if (cursor.Equals(default(T))) {
            // Zero is the explicit exhausted state. A deleted participant or
            // failed allocation may leave a lower surviving maximum behind.
            return;
        }
        if (hasValues && cursor.CompareTo(maximum) <= 0)
            throw new InvalidDataException($"The {name} cursor is below its persisted high-water mark.");
    }

    private static void ValidateSessionId(string sessionId)
    {
        if (!IsSha256(sessionId))
            throw new InvalidDataException("Cohort session IDs must be lowercase SHA-256 hex.");
    }

    private static bool IsSha256(string? value)
        => value is not null
            && value.Length == Sha256HexLength
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static PersistedPayload ToPayload(AuthoritativeCohortDocument document)
    {
        var participants = document.Participants
            .Select(participant => new PersistedParticipant(
                participant.SessionId,
                participant.EntityId,
                participant.CommandReceipts.Select(ToPersistedReceipt).ToArray()))
            .ToArray();
        return new PersistedPayload(
            document.FormatVersion,
            document.Generation,
            new PersistedIdentity(
                document.Identity.ModuleId,
                document.Identity.ModuleFormatVersion,
                document.Identity.ProtocolSchemaVersion,
                document.Identity.ContentManifestHash,
                document.Identity.RulesetIdentityHash,
                document.Identity.TickRateHz,
                document.Identity.GameMode),
            document.Tick,
            document.NextEntityId,
            document.NextServerReceiptSequence,
            participants,
            Convert.ToBase64String(StrictUtf8.GetBytes(document.SerializedModuleState)));
    }

    private static AuthoritativeCohortDocument FromPayload(PersistedPayload payload)
    {
        byte[] moduleBytes;
        try {
            moduleBytes = Convert.FromBase64String(payload.SerializedModuleState);
        } catch (FormatException exception) {
            throw new InvalidDataException("The serialized module state is not canonical Base64.", exception);
        } catch (ArgumentNullException exception) {
            throw new InvalidDataException("The serialized module state is not canonical Base64.", exception);
        }
        if (!string.Equals(
                Convert.ToBase64String(moduleBytes),
                payload.SerializedModuleState,
                StringComparison.Ordinal))
            throw new InvalidDataException("The serialized module state is not canonical Base64.");
        string moduleState;
        try {
            moduleState = StrictUtf8.GetString(moduleBytes);
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException("The serialized module state is not strict UTF-8.", exception);
        }
        var participants = payload.Participants
            .Select(participant => new AuthoritativeCohortParticipantCheckpoint(
                participant.SessionId,
                participant.EntityId,
                participant.CommandReceipts.Select(ToCommandResult).ToArray()))
            .ToArray();
        var document = new AuthoritativeCohortDocument(
            payload.FormatVersion,
            payload.Generation,
            new AuthoritativeCohortIdentity(
                payload.Identity.ModuleId,
                payload.Identity.ModuleFormatVersion,
                payload.Identity.ProtocolSchemaVersion,
                payload.Identity.ContentManifestHash,
                payload.Identity.RulesetIdentityHash,
                payload.Identity.TickRateHz,
                payload.Identity.GameMode),
            payload.Tick,
            payload.NextEntityId,
            payload.NextServerReceiptSequence,
            participants,
            moduleState);
        ValidateDocument(document);
        return Normalize(document);
    }

    private static PersistedRoot ReadRoot(byte[] bytes)
    {
        string json;
        try {
            json = StrictUtf8.GetString(bytes);
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException("The cohort checkpoint is not strict UTF-8.", exception);
        }

        JsonDocument parsed;
        try {
            parsed = JsonDocument.Parse(json);
        } catch (JsonException exception) {
            throw new InvalidDataException("The cohort checkpoint JSON is malformed.", exception);
        }
        using (parsed) {
            ValidateShapes(parsed.RootElement);
            ValidateNoDuplicateProperties(parsed.RootElement);
        }
        try {
            return JsonSerializer.Deserialize<PersistedRoot>(json, JsonOptions)
                ?? throw new InvalidDataException("The cohort checkpoint is empty.");
        } catch (JsonException exception) {
            throw new InvalidDataException("The cohort checkpoint JSON is malformed.", exception);
        }
    }

    private static AuthoritativeCohortDocument ReadDocument(byte[] bytes)
    {
        var root = ReadRoot(bytes);
        if (root.IntegritySha256 is null || !IsSha256(root.IntegritySha256))
            throw new InvalidDataException("The cohort integrity hash is invalid.");
        var payload = root.ToPayload();
        if (!string.Equals(root.IntegritySha256, ComputeIntegrity(payload), StringComparison.Ordinal))
            throw new InvalidDataException("The cohort integrity hash does not match its payload.");
        return FromPayload(payload);
    }

    private static string ComputeIntegrity(PersistedPayload payload)
        => Convert.ToHexString(
                SHA256.HashData(StrictUtf8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions))))
            .ToLowerInvariant();

    private static PersistedReceipt ToPersistedReceipt(CommandResult receipt)
        => new(
            receipt.ClientSequence,
            receipt.ServerReceiptSequence,
            receipt.AppliedTick,
            receipt.Status,
            receipt.RejectReason);

    private static CommandResult ToCommandResult(PersistedReceipt receipt)
        => new() {
            ClientSequence = receipt.ClientSequence,
            ServerReceiptSequence = receipt.ServerReceiptSequence,
            AppliedTick = receipt.AppliedTick,
            Status = receipt.Status,
            RejectReason = receipt.RejectReason,
        };

    private static void ValidateNoDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) {
                if (!names.Add(property.Name))
                    throw new InvalidDataException("The cohort checkpoint contains duplicate JSON properties.");
                ValidateNoDuplicateProperties(property.Value);
            }
        } else if (value.ValueKind == JsonValueKind.Array) {
            foreach (var element in value.EnumerateArray())
                ValidateNoDuplicateProperties(element);
        }
    }

    private static void ValidateObjectShape(
        JsonElement value,
        IReadOnlySet<string> expected,
        string description)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"The {description} must be an object.");
        var names = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (names.Length != expected.Count
            || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidDataException($"The {description} has missing or duplicate properties.");
        foreach (var property in value.EnumerateObject())
            if (!expected.Contains(property.Name))
                throw new InvalidDataException($"The {description} contains an unknown property '{property.Name}'.");
    }

    private static void ValidateShapes(JsonElement root)
    {
        ValidateObjectShape(root, RootFields, "cohort root");
        ValidateObjectShape(root.GetProperty("Identity"), IdentityFields, "cohort identity");
        var participants = root.GetProperty("Participants");
        if (participants.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The cohort participants must be an array.");
        foreach (var participant in participants.EnumerateArray()) {
            ValidateObjectShape(participant, ParticipantFields, "cohort participant");
            var receipts = participant.GetProperty("CommandReceipts");
            if (receipts.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The participant receipts must be an array.");
            foreach (var receipt in receipts.EnumerateArray())
                ValidateObjectShape(receipt, ReceiptFields, "cohort receipt");
        }
    }

    private static readonly IReadOnlySet<string> RootFields = new HashSet<string>(StringComparer.Ordinal) {
        "FormatVersion", "Generation", "Identity", "Tick", "NextEntityId",
        "NextServerReceiptSequence", "Participants", "SerializedModuleState", "IntegritySha256",
    };

    private static readonly IReadOnlySet<string> IdentityFields = new HashSet<string>(StringComparer.Ordinal) {
        "ModuleId", "ModuleFormatVersion", "ProtocolSchemaVersion", "ContentManifestHash",
        "RulesetIdentityHash", "TickRateHz", "GameMode",
    };

    private static readonly IReadOnlySet<string> ParticipantFields = new HashSet<string>(StringComparer.Ordinal) {
        "SessionId", "EntityId", "CommandReceipts",
    };

    private static readonly IReadOnlySet<string> ReceiptFields = new HashSet<string>(StringComparer.Ordinal) {
        "ClientSequence", "ServerReceiptSequence", "AppliedTick", "Status", "RejectReason",
    };

    private sealed record PersistedRoot(
        int FormatVersion,
        ulong Generation,
        PersistedIdentity Identity,
        ulong Tick,
        uint NextEntityId,
        ulong NextServerReceiptSequence,
        IReadOnlyList<PersistedParticipant> Participants,
        string SerializedModuleState,
        string IntegritySha256)
    {
        public PersistedPayload ToPayload()
            => new(
                FormatVersion,
                Generation,
                Identity,
                Tick,
                NextEntityId,
                NextServerReceiptSequence,
                Participants,
                SerializedModuleState);
    }

    private sealed record PersistedPayload(
        int FormatVersion,
        ulong Generation,
        PersistedIdentity Identity,
        ulong Tick,
        uint NextEntityId,
        ulong NextServerReceiptSequence,
        IReadOnlyList<PersistedParticipant> Participants,
        string SerializedModuleState);

    private sealed record PersistedIdentity(
        string ModuleId,
        int ModuleFormatVersion,
        string ProtocolSchemaVersion,
        string ContentManifestHash,
        string RulesetIdentityHash,
        uint TickRateHz,
        AuthoritativeGameMode GameMode);

    private sealed record PersistedParticipant(
        string SessionId,
        uint EntityId,
        IReadOnlyList<PersistedReceipt> CommandReceipts);

    private sealed record PersistedReceipt(
        ulong ClientSequence,
        ulong ServerReceiptSequence,
        ulong AppliedTick,
        CommandStatus Status,
        CommandRejectReason RejectReason);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
