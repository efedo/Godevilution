using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

/**
 * Routes each host operation through one leased ruleset generation. A reload
 * may publish a new adapter between operations, but cannot retire the adapter
 * used by an operation that is already in flight.
 */
public sealed class LeasedAuthoritativeGameHostAdapter : IAuthoritativeGameHostAdapter
{
    private readonly GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> rulesets;

    public LeasedAuthoritativeGameHostAdapter(
        GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> rulesets)
    {
        this.rulesets = rulesets ?? throw new ArgumentNullException(nameof(rulesets));
    }

    public CommandExecutionResult Execute(
        string sessionId,
        Command command,
        ulong appliedTick)
    {
        return WithAdapter(adapter => adapter.Execute(sessionId, command, appliedTick));
    }

    public void AdvanceTo(ulong tick)
    {
        WithAdapter(adapter => adapter.AdvanceTo(tick));
    }

    public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
    {
        return WithAdapter(adapter => adapter.CreateSnapshot(sessionId, entityId, tick));
    }

    public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
    {
        return WithAdapter(adapter => adapter.DrainEvents(sessionId, entityId, tick));
    }

    public string ExportPlayerSave(string sessionId)
    {
        return WithAdapter(adapter => adapter.ExportPlayerSave(sessionId));
    }

    public void ImportPlayerSave(string sessionId, string serializedSave)
    {
        WithAdapter(adapter => adapter.ImportPlayerSave(sessionId, serializedSave));
    }

    public void InitializeParticipant(
        string sessionId,
        AuthoritativeParticipantSelection selection)
    {
        WithAdapter(adapter => adapter.InitializeParticipant(sessionId, selection));
    }

    private TResult WithAdapter<TResult>(Func<IAuthoritativeGameHostAdapter, TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var lease = rulesets.Acquire();
        return operation(lease.Activation.Target);
    }

    private void WithAdapter(Action<IAuthoritativeGameHostAdapter> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var lease = rulesets.Acquire();
        operation(lease.Activation.Target);
    }
}
