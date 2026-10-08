using Devilution.Client.Protocol;

namespace Devilution.Client;

public partial class Main
{
    // Acceptance reads only immutable, credential-free connection evidence.
    internal AuthoritativeInitialConnectionObservation? ObservedInitialConnection
        => client?.InitialConnectionObservation;
}