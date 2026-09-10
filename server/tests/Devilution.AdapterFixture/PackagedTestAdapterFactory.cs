using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Snapshots;

namespace Devilution.AdapterFixture;

public sealed class PackagedTestAdapterFactory : IAuthoritativeGameHostAdapterFactory
{
    public string Id => "fixture.adapter";

    public IReadOnlyList<IGameplayModule> CreateBuiltInModules() => [];

    public bool CanActivate(GameplayModuleRegistry registry) => true;

    public IAuthoritativeGameHostAdapter Activate(
        GameplayModuleRegistry registry,
        ComposedContent content,
        AuthoritativePlayerStartConfiguration playerStart)
        => new PackagedTestAdapter();

    private sealed class PackagedTestAdapter : IAuthoritativeGameHostAdapter
    {
        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
        }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new() { Tick = tick };

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick) => null;

        public string ExportPlayerSave(string sessionId) => "fixture-save";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }
    }
}
