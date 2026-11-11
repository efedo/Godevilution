using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client;

public partial class Main
{
    private void CancelTravel(string reason = "Travel cancelled.")
    {
        client?.CancelRetainedIntentRetries();
        routeTravel.Cancel(reason);
        interactionTravel.Cancel();
    }

    private void SelectTravelRoute(uint warpId)
    {
        CancelTravel();
        routeTravel.Start(model.Snapshot, warpId, client?.IsConnected == true);
    }

    private void SelectInteractionTravel(int x, int y, Command command)
    {
        CancelTravel("Travel cancelled by an interaction.");
        interactionTravel.Start(model.Snapshot, x, y, command, client?.IsConnected == true);
    }

    internal bool ObservedQueuedCommandMatchesIntent(ulong sequence)
        => AuthoritativeCommandOutcome.IsQueuedForIntent(ObservedLastQueuedSequence,
            ObservedLastQueuedIntentSequence, sequence);
}
