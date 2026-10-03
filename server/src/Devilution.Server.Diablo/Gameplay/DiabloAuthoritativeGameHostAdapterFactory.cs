using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Host;
using Devilution.Server.Stores;

namespace Devilution.Server.Gameplay;

/** Server-side Diablo bridge kept outside Domain, Data, Engine, and the module. */
public sealed class DiabloAuthoritativeGameHostAdapterFactory
    : IAuthoritativeGameHostAdapterFactory
{
    public const string NativeGeneratedId = "devilution.diablo.cathedral-l1.v1";
    public const string CathedralDepthId = "devilution.diablo.cathedral-depths.v1";
    public const string CathedralDoorId = "devilution.diablo.cathedral-doors.v1";
    private readonly string adapterId;

    public DiabloAuthoritativeGameHostAdapterFactory() : this(false) { }

    public DiabloAuthoritativeGameHostAdapterFactory(bool nativeGenerated)
        : this(nativeGenerated ? NativeGeneratedId : "devilution.diablo") { }

    /** Explicit profile selection; the bool constructor retains its original L1 meaning. */
    public DiabloAuthoritativeGameHostAdapterFactory(string adapterId)
    {
        if (adapterId is not ("devilution.diablo" or NativeGeneratedId or CathedralDepthId or CathedralDoorId))
            throw new ArgumentException("Unsupported built-in Diablo adapter profile.", nameof(adapterId));
        this.adapterId = adapterId;
    }

    public string Id => adapterId;

    public IReadOnlyList<IGameplayModule> CreateBuiltInModules()
    {
        return [new DiabloModule()];
    }

    public bool CanActivate(GameplayModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return adapterId switch {
            NativeGeneratedId => registry.TryGetCapability<IDiabloNativeGeneratedContentBootstrap>(out _),
            CathedralDepthId => registry.TryGetCapability<IDiabloCathedralDepthContentBootstrap>(out _),
            CathedralDoorId => registry.TryGetCapability<IDiabloCathedralDoorContentBootstrap>(out _),
            _ => registry.TryGetCapability<IDiabloContentBootstrap>(out _),
        };
    }

    public IAuthoritativeGameHostAdapter Activate(
        GameplayModuleRegistry registry,
        ComposedContent content,
        AuthoritativePlayerStartConfiguration playerStart)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(playerStart);
        var gameMode = playerStart.GameMode switch {
            AuthoritativeGameMode.SinglePlayer => DiabloGameMode.SinglePlayer,
            AuthoritativeGameMode.Multiplayer => DiabloGameMode.Multiplayer,
            _ => throw new ArgumentOutOfRangeException(nameof(playerStart)),
        };
        var monsterSpawns = playerStart.ProceduralMonsterLimit is int maximumMonsters
            ? new DiabloMonsterSpawnConfiguration(maximumMonsters, playerStart.ProceduralMonsterLevelId)
            : null;
        var difficulty = playerStart.Difficulty switch {
            AuthoritativeGameDifficulty.Normal => DiabloDifficulty.Normal,
            AuthoritativeGameDifficulty.Nightmare => DiabloDifficulty.Nightmare,
            AuthoritativeGameDifficulty.Hell => DiabloDifficulty.Hell,
            _ => throw new ArgumentOutOfRangeException(nameof(playerStart)),
        };
        var activatedContent = adapterId switch {
            CathedralDoorId => registry.GetRequiredCapability<IDiabloCathedralDoorContentBootstrap>()
                .ActivateDoors(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            NativeGeneratedId => registry.GetRequiredCapability<IDiabloNativeGeneratedContentBootstrap>()
                .ActivateGenerated(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            CathedralDepthId => registry.GetRequiredCapability<IDiabloCathedralDepthContentBootstrap>()
                .ActivateDepths(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            _ => registry.GetRequiredCapability<IDiabloContentBootstrap>()
                .Activate(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
        };
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
    public static IReadOnlyList<IAuthoritativeGameHostAdapterFactory> CreateFactories()
        => CreateFactories(null);

    /** Only explicit selection exposes the opt-in profile; default discovery stays unambiguous. */
    public static IReadOnlyList<IAuthoritativeGameHostAdapterFactory> CreateFactories(string? selectedFactoryId)
    {
        return [selectedFactoryId is DiabloAuthoritativeGameHostAdapterFactory.CathedralDepthId
                or DiabloAuthoritativeGameHostAdapterFactory.CathedralDoorId
            ? new DiabloAuthoritativeGameHostAdapterFactory(selectedFactoryId)
            : new DiabloAuthoritativeGameHostAdapterFactory(selectedFactoryId == DiabloAuthoritativeGameHostAdapterFactory.NativeGeneratedId)];
    }

    public static AuthoritativeGameHostAdapterCatalog Create(
        string? selectedFactoryId = null)
    {
        return new AuthoritativeGameHostAdapterCatalog(CreateFactories(selectedFactoryId), selectedFactoryId);
    }
}
