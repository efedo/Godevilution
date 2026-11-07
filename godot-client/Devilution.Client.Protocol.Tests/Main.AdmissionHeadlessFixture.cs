using Devilution.Client.Protocol;

namespace Devilution.Client;

// Executes the linked production admission handler; only the Godot reconnect callback is substituted.
public partial class Main
{
    private readonly AuthoritativeClientModel model = new();
    private string? sessionStatusOverride;
    internal AuthoritativeClientModel AdmissionModel => model;
    internal string? AdmissionStatus => sessionStatusOverride;
    internal bool AdmissionReconnectRequested { get; private set; }
    internal bool NeedsTransportReconnectForTest(AuthoritativeClient client)
        => NeedsTransportReconnect(client, Task.CompletedTask, Task.CompletedTask);
    internal void AdmitForTest(AuthoritativeClient client, AuthoritativeClientMessage message)
        => ApplyAuthoritativeMessage(client, message);
    private void StartReconnect(bool force) => AdmissionReconnectRequested = force;
}
