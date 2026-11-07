using Devilution.Client.Protocol;

namespace Devilution.Client;

public partial class Main
{
    // A connected recovery transport may already have queued the full baseline for main-thread admission.
    private static bool NeedsTransportReconnect(AuthoritativeClient activeClient, Task? connection, Task? reconnect)
        => !activeClient.IsTransportConnected && (connection?.IsCompleted ?? true) && (reconnect?.IsCompleted ?? true);

    private void ApplyAuthoritativeMessage(AuthoritativeClient activeClient, AuthoritativeClientMessage message)
    {
        var recovering = activeClient.RequiresCompleteSnapshotAdmission;
        try {
            activeClient.ApplyMessageToModel(model, message);
            if (recovering && !activeClient.RequiresCompleteSnapshotAdmission)
                sessionStatusOverride = null;
        } catch (Exception exception) {
            sessionStatusOverride = $"Invalid authoritative state: {exception.Message} Reconnecting for a complete snapshot; gameplay is disabled.";
            StartReconnect(force: true);
            throw;
        }
    }
}
