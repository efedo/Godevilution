using Devilution.Server.Host;

namespace Godevilution.Server.Diablo.Package;

/**
 * Production package entry point for the Diablo adapter. The implementation
 * remains in the server-side Diablo assembly; this public package type keeps
 * the deployment boundary explicit and lets the host load the full game
 * dependency closure in an isolated context.
 */
public sealed class PackagedDiabloAuthoritativeGameHostAdapterFactory
    : IAuthoritativeGameHostAdapterFactory
{
    private readonly Devilution.Server.Gameplay.DiabloAuthoritativeGameHostAdapterFactory inner = new();

    public string Id => "godevilution.diablo.package";

    public IReadOnlyList<Devilution.Engine.IGameplayModule> CreateBuiltInModules()
        => inner.CreateBuiltInModules();

    public bool CanActivate(Devilution.Engine.GameplayModuleRegistry registry)
        => inner.CanActivate(registry);

    public IAuthoritativeGameHostAdapter Activate(
        Devilution.Engine.GameplayModuleRegistry registry,
        Devilution.Data.ComposedContent content,
        AuthoritativePlayerStartConfiguration playerStart)
        => inner.Activate(registry, content, playerStart);
}
