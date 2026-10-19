using Devilution.Client.Protocol;

namespace Devilution.Client;

public partial class Main
{
    private sealed record PendingConnectionError(
        AuthoritativeClient Client,
        long Generation,
        string Detail);

    private CancellationTokenSource? connectionLifetime;
    private long sessionGeneration;
    private bool sessionTransition;
    private string? sessionStatusOverride;
    private ClientConnectionOptions? activeConnectionOptions;
    private ClientConnectionOptions? menuConnectionOptions;
    private bool menuUsesLocalBookmark;
    private string? menuConfirmedSessionToken;
    private ClientConnectionOptions? menuConfirmedTokenOptions;
    private bool menuResumeInMemoryOnly;
    private AuthoritativeClient? cleanupClient;
    private readonly SessionCleanupCoordinator cleanupCoordinator = new();
    private Task? returnCleanupTask;
    private SessionCleanupResult? returnCleanupOutcome;
    private readonly TaskCompletionSource<MainExitCleanupObservation> exitCleanupCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long modelGeneration;
    private ulong observedEventBatchCount;
    private long sessionMenuPublicationCount;
    private long postExitMenuPublicationCount;
    private int acceptanceReturnCleanupGateEntered;

    // Acceptance-only synchronization for proving scene exit while return owns cleanup.
    internal Task? AcceptanceReturnCleanupGate { get; set; }

    // Read-only lifecycle observations for the bounded acceptance harness.
    internal bool ObservedSessionTransition => sessionTransition;
    internal long ObservedSessionGeneration => sessionGeneration;
    internal string? ObservedSessionStatus => sessionStatusOverride;
    internal bool ObservedReturnCleanupActive
        => sessionTransition && returnCleanupTask is { IsCompleted: false };
    internal bool ObservedReturnCleanupCompleted => returnCleanupTask?.IsCompleted == true;
    internal bool ObservedReturnCleanupOutcomePresent => returnCleanupOutcome is not null;
    internal bool ObservedReturnCleanupSucceeded
        => returnCleanupOutcome is { Failure: null } result && (result.Succeeded || result.SceneExiting);
    internal bool ObservedReturnCleanupFailed => returnCleanupOutcome?.Failure is not null;
    internal bool ObservedReturnCleanupGateEntered
        => Volatile.Read(ref acceptanceReturnCleanupGateEntered) != 0;
    internal Task<MainExitCleanupObservation> ObservedExitCleanupCompletion
        => exitCleanupCompletion.Task;

    private bool IsCurrentSession(AuthoritativeClient candidate, long generation)
        => SessionLifecyclePolicy.CanPublishCallback(
                sessionTransition,
                sessionGeneration,
                generation)
            && ReferenceEquals(client, candidate);

    private void SetSessionError(
        AuthoritativeClient candidate,
        long generation,
        string detail)
    {
        if (IsCurrentSession(candidate, generation))
            Interlocked.Exchange(
                ref pendingConnectionError,
                new PendingConnectionError(candidate, generation, detail));
    }

    private void BeginReturnToSessionMenu()
    {
        if (sessionTransition)
            return;
        returnCleanupOutcome = null;
        returnCleanupTask = ReturnToSessionMenuAsync();
    }

    private async Task ReturnToSessionMenuAsync()
    {
        if (sessionTransition)
            return;

        var activeClient = cleanupClient ?? client;
        var pendingCount = activeClient?.PendingCommandCount ?? 0;
        if (!SessionLifecyclePolicy.CanBeginReturn(sessionTransition, pendingCount)
            && cleanupClient is null) {
            sessionStatusOverride = SessionLifecyclePolicy.ReturnMessage(pendingCount);
            hud?.SetStatus(sessionStatusOverride);
            return;
        }
        if (!SessionLifecyclePolicy.CanReturnToSessionMenu(pendingCount)) {
            sessionStatusOverride = SessionLifecyclePolicy.ReturnMessage(pendingCount);
            hud?.SetStatus(sessionStatusOverride);
            return;
        }
        if (localSessionWarning is not null) {
            sessionStatusOverride = $"{localSessionWarning} Return is disabled until the bookmark is saved.";
            hud?.SetStatus(sessionStatusOverride);
            return;
        }

        var selectedConnectionOptions = activeConnectionOptions;
        var selectedUsesLocalBookmark = persistLocalSession;
        sessionTransition = true;
        sessionStatusOverride = SessionLifecyclePolicy.ReturnMessage(0);
        hud?.SetSessionMenuTransition(true);
        hud?.SetStatus(sessionStatusOverride);
        ResetPresentationForNewSession();
        hud?.SetStatus(sessionStatusOverride);
        routeTravel.Cancel("Returning to the session menu.");
        interactionTravel.Cancel();

        if (activeClient is not null && cleanupClient is null) {
            cleanupClient = activeClient;
            menuConnectionOptions = selectedConnectionOptions;
            menuUsesLocalBookmark = selectedUsesLocalBookmark;
        }
        sessionGeneration++;
        connectionLifetime?.Cancel();
        client = null;
        var oldConnectionTask = connectionTask;
        var oldPollTask = pollTask;
        var oldReconnectTask = reconnectTask;
        connectionTask = null;
        pollTask = null;
        reconnectTask = null;
        Interlocked.Exchange(ref pendingConnectionError, null);

        var cleanupResult = await cleanupCoordinator.Begin(
            () => JoinSessionWorkAsync(
                oldConnectionTask,
                oldPollTask,
                oldReconnectTask,
                activeClient),
            () => DisposeSessionClientAsync(cleanupClient),
            lifetime.Token);
        returnCleanupOutcome = cleanupResult;
        connectionLifetime?.Dispose();
        connectionLifetime = null;

        // Scene exit may free the HUD while the coordinator is finishing. Never
        // publish cleanup failure or menu state after the lifetime is canceled.
        if (lifetime.IsCancellationRequested || cleanupResult.SceneExiting)
            return;

        if (!cleanupResult.Succeeded) {
            sessionTransition = false;
            hud?.SetSessionMenuTransition(false);
            sessionStatusOverride = SessionLifecyclePolicy.CleanupFailureMessage(
                cleanupResult.Failure
                    ?? new InvalidOperationException("The previous session did not finish cleanup."));
            hud?.SetStatus(sessionStatusOverride);
            return;
        }

        if (!SessionLifecyclePolicy.CanShowSessionMenu(
                cleanupSucceeded: true,
                sceneExiting: lifetime.IsCancellationRequested))
            return;

        cleanupClient = null;
        sessionTransition = false;
        hud?.SetSessionMenuTransition(false);
        ShowSessionMenu(cleanupResult.WorkFailure);
    }

    private async Task<Exception?> JoinSessionWorkAsync(
        Task? connectionTaskToJoin,
        Task? pollTaskToJoin,
        Task? reconnectTaskToJoin,
        AuthoritativeClient? joinedClient)
    {
        var failures = new List<Exception>();
        AddCleanupFailure(failures, await AwaitFailure(
            connectionTaskToJoin,
            allowCancellation: true));
        AddCleanupFailure(failures, await AwaitFailure(
            pollTaskToJoin,
            allowCancellation: true));
        AddCleanupFailure(failures, await AwaitFailure(
            reconnectTaskToJoin,
            allowCancellation: true));

        if (AcceptanceReturnCleanupGate is { } acceptanceGate) {
            Interlocked.Exchange(ref acceptanceReturnCleanupGateEntered, 1);
            await acceptanceGate;
        }

        // A handshake can publish a resume token just before its canceled task
        // completes. Capture it before the transport is disposed.
        if (joinedClient is not null
            && !string.IsNullOrWhiteSpace(joinedClient.SessionToken)
            && menuConnectionOptions is not null) {
            menuConfirmedSessionToken = joinedClient.SessionToken;
            menuConfirmedTokenOptions = activeConnectionOptions;
            menuConnectionOptions = menuConnectionOptions with {
                ResumeToken = menuConfirmedSessionToken,
                RequestedCharacterClassId = null,
                RequestedCharacterName = null,
            };
        }

        return failures.Count > 0 ? failures[0] : null;
    }

    private static async Task DisposeSessionClientAsync(AuthoritativeClient? sessionClient)
    {
        if (sessionClient is not null)
            await sessionClient.DisposeAsync();
    }

    private void ResetPresentationForNewSession()
    {
        routeTravel.Cancel();
        interactionTravel.Cancel();
        model = new AuthoritativeClientModel();
        modelGeneration = checked(modelGeneration + 1);
        observedEventBatchCount = 0;
        snapshotRevision = 0;
        selectedSpellSourceKey = null;
        ObservedLastQueuedSequence = 0;
        localSessionWarning = null;
        bookmarkRetrySeconds = 0;
        persistLocalSession = false;
        world?.Apply(null, 0, null, AuthoritativePresentationClock.DefaultTickRateHz);
        feedback?.QueueFree();
        feedback = new PresentationFeedback();
        AddChild(feedback);
        hud?.SetStatus("Choose a character to connect to the authoritative server.");
    }

    private void ShowSessionMenu(Exception? cleanupWorkFailure = null)
    {
        if (lifetime.IsCancellationRequested) {
            Interlocked.Increment(ref postExitMenuPublicationCount);
            return;
        }
        Interlocked.Increment(ref sessionMenuPublicationCount);
        ClientConnectionOptions defaults = menuConnectionOptions is not null && !menuUsesLocalBookmark
            ? menuConnectionOptions!
            : connectionOptions ?? ClientConnectionOptions.FromEnvironment();
        string? bookmarkError = null;
        menuResumeInMemoryOnly = false;
        if (menuUsesLocalBookmark || menuConnectionOptions is null) {
            if (!string.IsNullOrWhiteSpace(resumeTokenPath)) {
                try {
                    var durableBookmark = LocalSessionBookmark.Read(resumeTokenPath);
                    defaults = SessionLifecyclePolicy.MenuDefaults(defaults, durableBookmark);
                    if (SessionLifecyclePolicy.ShouldPreferConfirmedToken(
                            durableBookmark,
                            menuConfirmedSessionToken)) {
                        if (string.IsNullOrWhiteSpace(durableBookmark)) {
                            try {
                                LocalSessionBookmark.Write(
                                    resumeTokenPath,
                                    menuConfirmedSessionToken!);
                                defaults = SessionLifecyclePolicy.MenuDefaults(
                                    defaults,
                                    menuConfirmedSessionToken);
                            } catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) {
                                menuResumeInMemoryOnly = true;
                                defaults = SessionLifecyclePolicy.MenuDefaults(
                                    defaults,
                                    menuConfirmedSessionToken);
                                bookmarkError = exception.Message;
                            }
                        } else {
                            menuResumeInMemoryOnly = true;
                            defaults = SessionLifecyclePolicy.MenuDefaults(
                                defaults,
                                menuConfirmedSessionToken);
                        }
                    }
                } catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) {
                    bookmarkError = $"Could not read local resume bookmark: {exception.Message}";
                    if (!string.IsNullOrWhiteSpace(menuConfirmedSessionToken)) {
                        menuResumeInMemoryOnly = true;
                        defaults = SessionLifecyclePolicy.MenuDefaults(
                            defaults,
                            menuConfirmedSessionToken);
                    }
                }
            }
        }

        hud!.Visible = false;
        characterSetup!.ShowSetup(defaults);
        var warning = SessionLifecyclePolicy.MenuWarningMessage(
            menuResumeInMemoryOnly, bookmarkError, cleanupWorkFailure);
        if (warning is not null)
            characterSetup.ShowError(warning);
        sessionStatusOverride = null;
    }

    private static async Task<Exception?> AwaitFailure(
        Task? task,
        bool allowCancellation)
    {
        if (task is null)
            return null;
        try {
            await task;
            return null;
        } catch (OperationCanceledException) when (allowCancellation) {
            return null;
        } catch (ObjectDisposedException) when (allowCancellation) {
            return null;
        } catch (Exception exception) {
            return exception;
        }
    }

    private static void AddCleanupFailure(
        ICollection<Exception> failures,
        Exception? failure)
    {
        if (failure is not null)
            failures.Add(failure);
    }
}

internal sealed record MainExitCleanupObservation(
    bool Completed,
    bool ReturnCleanupJoined,
    int FailureCount,
    long PostExitMenuPublicationAttemptCount,
    bool NoPostExitMenuPublication);
