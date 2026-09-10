using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Protocol.V1;

namespace Devilution.Server.Stores;

/** Durable atomic checkpoint containing authoritative state and command receipts. */
public sealed class AuthoritativeSessionStore
{
    /** Version 2 adds a state hash; version 3 hashes the command ledger; version 4 binds the ruleset identity. */
    public const int CurrentFormatVersion = 4;
    private static readonly Dictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object PathLockSynchronization = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General) {
        WriteIndented = false,
    };
    private readonly string rootDirectory;

    public AuthoritativeSessionStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    /** Publishes state and its idempotency ledger as one atomic checkpoint. */
    public void Save(
        string sessionId,
        string serializedState,
        IReadOnlyDictionary<ulong, CommandResult> commandResults,
        string? rulesetIdentityHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedState);
        ArgumentNullException.ThrowIfNull(commandResults);
        StrictUtf8Text.Encode(serializedState, "authoritative session state");
        var receipts = commandResults
            .OrderBy(entry => entry.Key)
            .Select(entry => Receipt.From(entry.Key, entry.Value))
            .ToArray();
        var path = GetPath(sessionId);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var pathLock = GetPathLock(path);
        lock (pathLock) {
            try {
                var formatVersion = string.IsNullOrWhiteSpace(rulesetIdentityHash)
                    ? 3
                    : CurrentFormatVersion;
                var document = new SessionDocument(
                    formatVersion,
                    serializedState,
                    receipts,
                    ComputeStateSha256(serializedState),
                    ComputeReceiptsSha256(receipts),
                    rulesetIdentityHash);
                var bytes = StrictUtf8Text.Encode(
                    JsonSerializer.Serialize(document, JsonOptions),
                    "authoritative session checkpoint");
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.WriteThrough)) {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, path, overwrite: true);
            } finally {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }

    /**
     * Loads one complete checkpoint, failing closed on corruption or an
     * incompatible current-format ruleset identity.
     */
    public AuthoritativeSessionDocument? Load(
        string sessionId,
        string? expectedRulesetIdentityHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var path = GetPath(sessionId);
        var pathLock = GetPathLock(path);
        lock (pathLock) {
            if (!File.Exists(path))
                return null;
            SessionDocument? document;
            try {
                document = JsonSerializer.Deserialize<SessionDocument>(
                    StrictUtf8Text.ReadAllText(path, "authoritative session checkpoint"),
                    JsonOptions);
            } catch (JsonException exception) {
                throw new InvalidDataException("The authoritative session checkpoint is malformed.", exception);
            }
            if (document is null || string.IsNullOrWhiteSpace(document.SerializedState))
                throw new InvalidDataException("The authoritative session checkpoint is incomplete.");
            if (document.FormatVersion is not (1 or 2 or 3 or CurrentFormatVersion))
                throw new InvalidDataException(
                    $"Authoritative session checkpoint format {document.FormatVersion} is unsupported.");
            if (document.FormatVersion == CurrentFormatVersion
                && string.IsNullOrWhiteSpace(document.RulesetIdentityHash))
                throw new InvalidDataException(
                    "The current authoritative session checkpoint has no ruleset identity.");
            if (!string.IsNullOrWhiteSpace(expectedRulesetIdentityHash)
                && document.FormatVersion == CurrentFormatVersion
                && !string.Equals(
                    document.RulesetIdentityHash,
                    expectedRulesetIdentityHash,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "The authoritative session checkpoint ruleset identity does not match the active ruleset.");
            if (document.FormatVersion >= 2
                && !string.Equals(
                    document.StateSha256,
                    ComputeStateSha256(document.SerializedState),
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "The authoritative session checkpoint state integrity hash is invalid.");
            if (document.CommandReceipts is null)
                throw new InvalidDataException("The authoritative session checkpoint has no command ledger.");

            var clientSequences = new HashSet<ulong>();
            var receiptSequences = new HashSet<ulong>();
            foreach (var receipt in document.CommandReceipts) {
                if (receipt is null
                    || receipt.ClientSequence == 0
                    || receipt.ServerReceiptSequence == 0
                    || !Enum.IsDefined(receipt.Status)
                    || !Enum.IsDefined(receipt.RejectReason)
                    || !clientSequences.Add(receipt.ClientSequence)
                    || !receiptSequences.Add(receipt.ServerReceiptSequence)) {
                    throw new InvalidDataException(
                        "The authoritative session checkpoint contains an invalid or duplicate command receipt.");
                }
            }
            if (document.FormatVersion >= 3
                && !string.Equals(
                    document.CommandReceiptsSha256,
                    ComputeReceiptsSha256(document.CommandReceipts),
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "The authoritative session checkpoint command-ledger integrity hash is invalid.");

            return new AuthoritativeSessionDocument(
                document.FormatVersion,
                document.SerializedState,
                document.CommandReceipts
                    .Select(receipt => receipt.ToCommandResult())
                    .ToArray(),
                document.RulesetIdentityHash);
        }
    }

    private string GetPath(string sessionId)
    {
        if (sessionId.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
            throw new ArgumentException("Session IDs may contain only letters, digits, '-' and '_'.", nameof(sessionId));
        return Path.Combine(rootDirectory, sessionId + ".session.json");
    }

    private static object GetPathLock(string path)
    {
        lock (PathLockSynchronization) {
            if (!PathLocks.TryGetValue(path, out var pathLock)) {
                pathLock = new object();
                PathLocks.Add(path, pathLock);
            }
            return pathLock;
        }
    }

    private sealed record SessionDocument(
        int FormatVersion,
        string SerializedState,
        Receipt[] CommandReceipts,
        string? StateSha256,
        string? CommandReceiptsSha256,
        string? RulesetIdentityHash);

    private static string ComputeStateSha256(string serializedState)
        => Convert.ToHexString(SHA256.HashData(
            StrictUtf8Text.Encode(serializedState, "authoritative session state")));

    private static string ComputeReceiptsSha256(IReadOnlyList<Receipt> receipts)
    {
        var canonicalReceipts = receipts
            .OrderBy(receipt => receipt.ClientSequence)
            .ThenBy(receipt => receipt.ServerReceiptSequence)
            .ThenBy(receipt => receipt.AppliedTick)
            .ThenBy(receipt => receipt.Status)
            .ThenBy(receipt => receipt.RejectReason)
            .ToArray();
        var serializedReceipts = JsonSerializer.Serialize(canonicalReceipts, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(
            StrictUtf8Text.Encode(serializedReceipts, "authoritative command ledger")));
    }

    private sealed record Receipt(
        ulong ClientSequence,
        ulong ServerReceiptSequence,
        ulong AppliedTick,
        CommandStatus Status,
        CommandRejectReason RejectReason)
    {
        public static Receipt From(ulong clientSequence, CommandResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (clientSequence == 0
                || result.ClientSequence != clientSequence
                || result.ServerReceiptSequence == 0
                || !Enum.IsDefined(result.Status)
                || !Enum.IsDefined(result.RejectReason))
                throw new InvalidDataException("The authoritative session checkpoint contains an invalid command receipt.");
            return new Receipt(
                clientSequence,
                result.ServerReceiptSequence,
                result.AppliedTick,
                result.Status,
                result.RejectReason);
        }

        public CommandResult ToCommandResult()
        {
            return new CommandResult {
                ClientSequence = ClientSequence,
                ServerReceiptSequence = ServerReceiptSequence,
                AppliedTick = AppliedTick,
                Status = Status,
                RejectReason = RejectReason,
            };
        }
    }
}

/** Immutable state and command-receipt view loaded from an atomic checkpoint. */
public sealed record AuthoritativeSessionDocument(
    int FormatVersion,
    string SerializedState,
    IReadOnlyList<CommandResult> CommandReceipts,
    string? RulesetIdentityHash = null);
