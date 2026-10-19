using Devilution.Client.Protocol;

namespace Devilution.Client;

/** Pure policy used by the Godot session menu to preserve authoritative retry guarantees. */
public static class SessionLifecyclePolicy
{
    public const string PendingCommandsReturnMessage
        = "Wait for all gameplay commands to be acknowledged before returning to the session menu.";

    public const string CleanupFailurePrefix
        = "Could not finish closing the previous session";

    public const string InMemoryResumeWarning
        = "The current resume credential is retained only in memory in this running client; the local bookmark was not replaced.";

    public static bool CanReturnToSessionMenu(int pendingCommandCount)
        => pendingCommandCount == 0;

    public static bool CanBeginReturn(bool transitionActive, int pendingCommandCount)
        => !transitionActive && CanReturnToSessionMenu(pendingCommandCount);

    public static string ReturnMessage(int pendingCommandCount)
        => CanReturnToSessionMenu(pendingCommandCount)
            ? "Returning to the session menu..."
            : PendingCommandsReturnMessage;

    public static string CleanupFailureMessage(Exception exception)
        => $"{CleanupFailurePrefix}. {exception.Message} The session menu remains closed until cleanup succeeds.";

    public static string CleanupWorkWarningMessage(Exception exception)
        => $"The previous connection ended while closing, but its transport is closed safely. {exception.Message}";

    public static string? MenuWarningMessage(
        bool resumeInMemoryOnly,
        string? bookmarkError,
        Exception? cleanupWorkFailure)
    {
        var warnings = new List<string>();
        if (resumeInMemoryOnly)
            warnings.Add(InMemoryResumeWarning);
        if (bookmarkError is not null)
            warnings.Add($"Bookmark status: {bookmarkError}");
        if (cleanupWorkFailure is not null)
            warnings.Add(CleanupWorkWarningMessage(cleanupWorkFailure));
        return warnings.Count == 0 ? null : string.Join(" ", warnings);
    }

    public static bool ShouldPreferConfirmedToken(
        string? durableBookmark,
        string? confirmedToken)
        => !string.IsNullOrWhiteSpace(confirmedToken)
            && !string.Equals(durableBookmark, confirmedToken, StringComparison.Ordinal);

    public static bool IsCurrentGeneration(long activeGeneration, long callbackGeneration)
        => activeGeneration == callbackGeneration;

    public static bool CanPublishCallback(
        bool transitionActive,
        long activeGeneration,
        long callbackGeneration)
        => !transitionActive && IsCurrentGeneration(activeGeneration, callbackGeneration);

    public static bool CanShowSessionMenu(bool cleanupSucceeded, bool sceneExiting)
        => cleanupSucceeded && !sceneExiting;

    /** Scene cancellation closes presentation; it must not hide transport disposal failure. */
    public static bool CanCompleteSceneExit(SessionCleanupResult? result, Exception? returnFailure)
        => returnFailure is null && result is { Failure: null }
            && (result.Succeeded || result.SceneExiting);

    /** Only the configured local endpoint may update its local resume bookmark. */
    public static bool ShouldPersistLocalBookmark(
        ClientConnectionOptions configuredDefaults,
        ClientConnectionOptions selectedOptions,
        string? bookmarkPath)
        => !string.IsNullOrWhiteSpace(bookmarkPath)
            && string.Equals(configuredDefaults.Host, selectedOptions.Host, StringComparison.Ordinal)
            && configuredDefaults.Port == selectedOptions.Port;

    /** Builds menu defaults without putting a credential in status or diagnostic text. */
    public static ClientConnectionOptions MenuDefaults(
        ClientConnectionOptions configuredDefaults,
        string? retainedBookmark)
    {
        if (string.IsNullOrWhiteSpace(retainedBookmark))
            return configuredDefaults;

        return configuredDefaults with {
            ResumeToken = retainedBookmark,
            RequestedCharacterClassId = null,
            RequestedCharacterName = null,
        };
    }
}

/** Outcome of joining the old session before its transport is disposed. */
public sealed record SessionCleanupResult(
    bool Succeeded,
    bool SceneExiting,
    Exception? Failure,
    Exception? WorkFailure)
{
    public static SessionCleanupResult Completed(Exception? workFailure = null)
        => new(true, false, null, workFailure);

    public static SessionCleanupResult Canceled(Exception? workFailure = null)
        => new(false, true, null, workFailure);

    public static SessionCleanupResult Failed(Exception failure, Exception? workFailure = null)
        => new(false, false, failure, workFailure);
}

/** Joins per-session work before disposing its transport and coalesces duplicate cleanup requests. */
public sealed class SessionCleanupCoordinator
{
    private readonly object gate = new();
    private Task<SessionCleanupResult>? current;

    public Task<SessionCleanupResult> Begin(
        Func<Task> joinWork,
        Func<Task> disposeTransport,
        CancellationToken sceneLifetime)
    {
        ArgumentNullException.ThrowIfNull(joinWork);
        ArgumentNullException.ThrowIfNull(disposeTransport);

        return Begin(
            async () => {
                await joinWork();
                return null;
            },
            disposeTransport,
            sceneLifetime);
    }

    public Task<SessionCleanupResult> Begin(
        Func<Task<Exception?>> joinWork,
        Func<Task> disposeTransport,
        CancellationToken sceneLifetime)
    {
        ArgumentNullException.ThrowIfNull(joinWork);
        ArgumentNullException.ThrowIfNull(disposeTransport);

        lock (gate) {
            if (current is { IsCompleted: false })
                return current;
            return current = RunAsync(joinWork, disposeTransport, sceneLifetime);
        }
    }

    private static async Task<SessionCleanupResult> RunAsync(
        Func<Task<Exception?>> joinWork,
        Func<Task> disposeTransport,
        CancellationToken sceneLifetime)
    {
        Exception? workFailure = null;
        try {
            workFailure = await joinWork();
        } catch (Exception exception) {
            workFailure = exception;
        }

        Exception? disposeFailure = null;
        try {
            await disposeTransport();
        } catch (Exception exception) {
            disposeFailure = exception;
        }

        if (disposeFailure is not null)
            return SessionCleanupResult.Failed(disposeFailure, workFailure);
        if (sceneLifetime.IsCancellationRequested)
            return SessionCleanupResult.Canceled(workFailure);
        return SessionCleanupResult.Completed(workFailure);
    }
}
