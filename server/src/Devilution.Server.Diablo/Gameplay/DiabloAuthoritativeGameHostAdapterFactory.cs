using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Host;
using Devilution.Server.Stores;

namespace Devilution.Server.Gameplay;

/** Server-side Diablo bridge kept outside Domain, Data, Engine, and the module. */
internal sealed class DiabloAuthoritativeGameHostAdapterFactory
    : IAuthoritativeGameHostAdapterFactory
{
    public string Id => "devilution.diablo";

    public IReadOnlyList<IGameplayModule> CreateBuiltInModules()
    {
        return [new DiabloModule()];
    }

    public bool CanActivate(GameplayModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.TryGetCapability<IDiabloContentBootstrap>(out _);
    }

    public IAuthoritativeGameHostAdapter Activate(
        GameplayModuleRegistry registry,
        ComposedContent content,
        AuthoritativePlayerStartConfiguration playerStart)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(playerStart);
        var activatedContent = registry
            .GetRequiredCapability<IDiabloContentBootstrap>()
            .Activate(
                content,
                playerStart.RandomSeed,
                playerStart.GameMode switch {
                    AuthoritativeGameMode.SinglePlayer => DiabloGameMode.SinglePlayer,
                    AuthoritativeGameMode.Multiplayer => DiabloGameMode.Multiplayer,
                    _ => throw new ArgumentOutOfRangeException(nameof(playerStart)),
                },
                playerStart.ProceduralMonsterLimit is int maximumMonsters
                    ? new DiabloMonsterSpawnConfiguration(
                        maximumMonsters,
                        playerStart.ProceduralMonsterLevelId)
                    : null,
                playerStart.Difficulty switch {
                    AuthoritativeGameDifficulty.Normal => DiabloDifficulty.Normal,
                    AuthoritativeGameDifficulty.Nightmare => DiabloDifficulty.Nightmare,
                    AuthoritativeGameDifficulty.Hell => DiabloDifficulty.Hell,
                    _ => throw new ArgumentOutOfRangeException(nameof(playerStart)),
                });
        var startingLevelId = playerStart.StartingLevelId
            ?? activatedContent.DefaultStartingLevelId;
        if (activatedContent.Simulation.WorldState.World is not { } world
            || !world.ContainsLevel(startingLevelId))
            throw new InvalidDataException(
                $"Starting level {startingLevelId} is not defined by the active content world.");
        var startingPosition = world.GetStartPosition(startingLevelId);
        return new StoreSimulationExecutor(
            activatedContent,
            (sessionId, requestedClassId) => {
                var playerClass = requestedClassId.HasValue
                    ? activatedContent.PlayerClasses.TryGet(
                        requestedClassId.Value,
                        out var selected)
                        ? selected
                        : throw new InvalidDataException(
                            $"Requested player class ID {requestedClassId.Value} is not defined by active content.")
                    : activatedContent.PlayerClasses.Default;
                return activatedContent.PlayerLoadouts.CreateTemplate(
                    sessionId,
                    playerClass.ClassId,
                    playerStart.Gold,
                    playerStart.Life,
                    playerStart.Mana,
                    startingLevelId,
                    playerStart.StatPoints,
                    startingPosition.X,
                    startingPosition.Y);
            });
    }
}

/** Explicit list of built-in host adapters; no filesystem scanning occurs. */
public static class BuiltInAuthoritativeGameHostAdapters
{
    public static AuthoritativeGameHostAdapterCatalog Create(
        string? selectedFactoryId = null)
    {
        return new AuthoritativeGameHostAdapterCatalog([
            new DiabloAuthoritativeGameHostAdapterFactory(),
        ], selectedFactoryId);
    }
}
