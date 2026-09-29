using System.Text.Json;
using Devilution.Protocol.V1;

namespace Devilution.Server.Stores;

/**
 * Durable command-result ledger used to preserve client-sequence idempotency
 * when a session reconnects through a fresh host process.
 */
public sealed class AuthoritativeCommandReceiptStore
{
    private static readonly Dictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object PathLockSynchronization = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General) {
        WriteIndented = false,
    };
    private readonly string rootDirectory;

    public AuthoritativeCommandReceiptStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    /** Durably publishes cloned results; Windows reuses and flushes identical bytes. */
    public void Save(string sessionId, IReadOnlyDictionary<ulong, CommandResult> results)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(results);
        var entries = results
            .OrderBy(entry => entry.Key)
            .Select(entry => Receipt.From(entry.Key, entry.Value))
            .ToArray();
        var path = GetPath(sessionId);
        var pathLock = GetPathLock(path);
        lock (pathLock) {
            var bytes = StrictUtf8Text.Encode(
                JsonSerializer.Serialize(entries, JsonOptions),
                "command receipt ledger");
            if (FlushIfUnchanged(path, bytes))
                return;
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
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

    /** Loads a previously persisted session ledger, failing closed on corruption. */
    public IReadOnlyList<CommandResult> Load(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var path = GetPath(sessionId);
        var pathLock = GetPathLock(path);
        lock (pathLock) {
            if (!File.Exists(path))
                return [];
            Receipt[] receipts;
            try {
                receipts = JsonSerializer.Deserialize<Receipt[]>(
                    StrictUtf8Text.ReadAllText(path, "command receipt ledger"),
                    JsonOptions)
                    ?? throw new InvalidDataException("The command receipt ledger is empty.");
            } catch (JsonException exception) {
                throw new InvalidDataException("The command receipt ledger is malformed.", exception);
            }
            var results = new List<CommandResult>(receipts.Length);
            var clientSequences = new HashSet<ulong>();
            foreach (var receipt in receipts) {
                if (receipt is null)
                    throw new InvalidDataException("The command receipt ledger contains a null result.");
                if (!clientSequences.Add(receipt.ClientSequence))
                    throw new InvalidDataException("The command receipt ledger contains duplicate client sequences.");
                results.Add(receipt.ToCommandResult());
            }
            return results;
        }
    }

    private static bool FlushIfUnchanged(string path, ReadOnlySpan<byte> bytes)
    {
        // Windows sharing protects this pathname through comparison and flush.
        // Other platforms retain replacement until equivalent protection is proven.
        if (!OperatingSystem.IsWindows())
            return false;
        FileStream? existing = null;
        try {
            try {
                // Check the file itself, never cached hashes or receipt counts.
                // Deny other writers/replacement until comparison and flush finish.
                existing = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (existing.Length != bytes.Length)
                    return false;
                Span<byte> buffer = stackalloc byte[4096];
                for (var offset = 0; offset < bytes.Length;) {
                    var count = Math.Min(buffer.Length, bytes.Length - offset);
                    var chunk = buffer[..count];
                    existing.ReadExactly(chunk);
                    if (!chunk.SequenceEqual(bytes.Slice(offset, count)))
                        return false;
                    offset += count;
                }
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                // Missing/unreadable mirrors retain the original atomic-write path.
                return false;
            }
            // Do not swallow flush failures or assume existing bytes are durable.
            existing.Flush(flushToDisk: true);
            return true;
        } finally {
            existing?.Dispose();
        }
    }

    /** Returns whether a compatibility ledger exists, including an empty one. */
    public bool Exists(string sessionId)
    {
        var path = GetPath(sessionId);
        var pathLock = GetPathLock(path);
        lock (pathLock)
            return File.Exists(path);
    }

    /** Removes one compatibility ledger when a multi-session write rolls back. */
    public bool Delete(string sessionId)
    {
        var path = GetPath(sessionId);
        var pathLock = GetPathLock(path);
        lock (pathLock) {
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
    }

    private string GetPath(string sessionId)
        => AuthoritativePersistencePaths.Combine(rootDirectory, sessionId, ".commands.json");

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
                throw new InvalidDataException("The command receipt ledger contains an invalid client sequence.");
            return new Receipt(
                clientSequence,
                result.ServerReceiptSequence,
                result.AppliedTick,
                result.Status,
                result.RejectReason);
        }

        public CommandResult ToCommandResult()
        {
            if (ClientSequence == 0
                || !Enum.IsDefined(Status)
                || !Enum.IsDefined(RejectReason))
                throw new InvalidDataException("The command receipt ledger contains an invalid result.");
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
