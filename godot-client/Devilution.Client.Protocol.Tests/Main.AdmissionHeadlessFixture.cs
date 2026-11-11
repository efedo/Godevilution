using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client;

// Links production batch admission, reconnect, and session cleanup. Only Godot rendering is substituted.
#pragma warning disable CS0414, CS0649, CS0169
public partial class Main : IAsyncDisposable
{
    private AuthoritativeClientModel model = new();
    private AuthoritativeClient? client;
    private readonly CancellationTokenSource lifetime = new();
    private readonly AuthoritativeRouteTravel routeTravel = new();
    private readonly AuthoritativeInteractionTravel interactionTravel = new();
    private readonly HeldEntityAttackController heldEntityAttack = new();
    private Task? connectionTask, pollTask, reconnectTask;
    private PendingConnectionError? pendingConnectionError;
    private ClientConnectionOptions? connectionOptions;
    private readonly HeadlessHud hud = new();
    private readonly HeadlessSetup characterSetup = new();
    private HeadlessWorld? world;
    private PresentationFeedback? feedback;
    private string? selectedSpellSourceKey, localSessionWarning, resumeTokenPath;
    private bool spellSourceArmed, heldMouseWalk, persistLocalSession;
    private double bookmarkRetrySeconds, admissionMilliseconds;
    private long admissionAllocated;
    private ulong snapshotRevision;
    internal Action<CommandAck>? AcceptanceAcknowledgementObserver { get; set; }
    internal Action<Snapshot>? AcceptanceSnapshotObserver { get; set; }
    internal ulong ObservedLastQueuedSequence { get; private set; }
    internal ulong ObservedLastQueuedIntentSequence { get; private set; }
    internal Command.IntentOneofCase ObservedLastQueuedIntent { get; private set; }
    internal uint ObservedLastQueuedObjectEntityId { get; private set; }
    internal AuthoritativeClientModel AdmissionModel => model;
    internal string? AdmissionStatus => sessionStatusOverride;
    internal bool AdmissionMenuVisible => characterSetup.Visible;
    internal string? AdmissionMenuError => characterSetup.Error;
    internal Task? AdmissionReconnectTask => reconnectTask;
    internal Task? AdmissionReturnTask => returnCleanupTask;
    internal int AdmissionRecoveryAttemptCount => Volatile.Read(ref admissionRecoveryAttempts);
    internal ulong AdmissionPublishedSnapshotCount => snapshotRevision;
    internal void SetAdmissionBookmarkWarning(string warning) => localSessionWarning = warning;
    internal void SetReconnectTime(TimeProvider time) => reconnectTime = time;
    internal void AttachTravelForTest(AuthoritativeClient candidate, Snapshot snapshot)
    {
        client = candidate;
        model.Apply(new(Snapshot: snapshot));
    }
    internal void DetachTravelForTest() => client = null;
    internal void CancelTravelForTest() => CancelTravel();
    internal void SelectTravelForTest(uint warp) => SelectTravelRoute(warp);
    internal void SelectInteractionForTest() => SelectInteractionTravel(0, 3, new() { OperateObjectRequested = new() { ObjectEntityId = 2 } });
    internal Command? NextTravelForTest() => routeTravel.Active
        ? routeTravel.Next(model.Snapshot, model.LastCommandResult, client?.IsConnected == true, client?.PendingCommandCount ?? 0)
        : interactionTravel.Next(model.Snapshot, model.LastCommandResult, client?.IsConnected == true, client?.PendingCommandCount ?? 0, true, false);


    internal async Task ConnectAdmissionSceneAsync(AuthoritativeClient candidate, ClientConnectionOptions options)
    {
        client = candidate;
        activeConnectionOptions = connectionOptions = options;
        connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        sessionGeneration++;
        connectionTask = candidate.ConnectAsync(connectionLifetime.Token);
        await connectionTask;
    }

    internal bool PumpAdmissionScene(IReadOnlyList<AuthoritativeClientMessage>? messages = null)
    {
        if (client is not { } active || sessionTransition) return false;
        var accepted = ApplyAuthoritativeBatch(active, messages ?? active.DrainMessages());
        if (CompleteFailedAdmissionRecovery()) return false;
        if (NeedsTransportReconnect(active, connectionTask, reconnectTask)
            || active.RequiresCompleteSnapshotAdmission && (reconnectTask?.IsCompleted ?? true))
            StartReconnect(force: active.RequiresCompleteSnapshotAdmission);
        return accepted;
    }

    internal bool NeedsTransportReconnectForTest(AuthoritativeClient candidate)
        => NeedsTransportReconnect(candidate, Task.CompletedTask, Task.CompletedTask);
    public async ValueTask DisposeAsync()
    {
        if (client is not null || cleanupClient is not null) await ReturnToSessionMenuAsync("Fixture cleanup.");
        lifetime.Cancel();
        lifetime.Dispose();
    }
    private static long FrameCostTimestamp() => 0;
    private static long FrameCostAllocation() => 0;
    private static long FrameCostAllocationSince(long _) => 0;
    private static double FrameCostElapsed(long _) => 0;
    private static void AddChild(PresentationFeedback _) { }
    private sealed class HeadlessHud
    {
        public bool Visible { get; set; }
        public void SetStatus(string? _) { }
        public void SetSessionMenuTransition(bool _) { }
    }
    private sealed class HeadlessSetup
    {
        public bool Visible { get; private set; }
        public string? Error { get; private set; }
        public void ShowSetup(ClientConnectionOptions _) => Visible = true;
        public void ShowError(string error) => Error = error;
    }
    private sealed class HeadlessWorld
    {
        public void Apply(Snapshot? snapshot, double delta, ClientPosition? predicted, uint rate) { }
        public void MarkPresentationEpochPending(ulong revision) { }
    }
}
internal sealed class PresentationFeedback { public void QueueFree() { } }
#pragma warning restore CS0414, CS0649, CS0169
