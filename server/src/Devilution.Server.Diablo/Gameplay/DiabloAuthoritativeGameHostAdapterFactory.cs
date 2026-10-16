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
    public const string CathedralNativePopulationId = "devilution.diablo.cathedral-native-population.v1";
    public const string CathedralNativeProgressionId = "devilution.diablo.cathedral-native-progression.v1";
    private readonly string adapterId;

    public DiabloAuthoritativeGameHostAdapterFactory() : this(false) { }

    public DiabloAuthoritativeGameHostAdapterFactory(bool nativeGenerated)
        : this(nativeGenerated ? NativeGeneratedId : "devilution.diablo") { }

    /** Explicit profile selection; the bool constructor retains its original L1 meaning. */
    public DiabloAuthoritativeGameHostAdapterFactory(string adapterId)
    {
        if (adapterId is not ("devilution.diablo" or NativeGeneratedId or CathedralDepthId or CathedralDoorId or CathedralNativePopulationId or CathedralNativeProgressionId))
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
            CathedralNativePopulationId => registry.TryGetCapability<IDiabloCathedralNativePopulationBootstrap>(out _),
            CathedralNativeProgressionId => registry.TryGetCapability<IDiabloCathedralNativeProgressionBootstrap>(out _),
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
        if ((adapterId is CathedralNativePopulationId or CathedralNativeProgressionId)
            && (playerStart.GameMode != AuthoritativeGameMode.SinglePlayer
                || playerStart.Difficulty != AuthoritativeGameDifficulty.Normal
                || playerStart.ProceduralMonsterLimit is not null
                || playerStart.ProceduralMonsterLevelId is not null
                || playerStart.StartingLevelId is not (null or 0 or 1)
                || playerStart.StatPoints is not (null or 0)))
            throw new NotSupportedException(
                "The native-populated Cathedral profile requires a Normal single-player Rogue start at Town or L1 without procedural overrides.");
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
            CathedralNativePopulationId => registry.GetRequiredCapability<IDiabloCathedralNativePopulationBootstrap>()
                .ActivateNativePopulation(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            CathedralNativeProgressionId => registry.GetRequiredCapability<IDiabloCathedralNativeProgressionBootstrap>()
                .ActivateNativeProgression(content, playerStart.RandomSeed),
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
        if ((adapterId is CathedralNativePopulationId or CathedralNativeProgressionId) && startingLevelId is not (0 or 1))
            throw new NotSupportedException(
                "The native-populated Cathedral profile supports only Town and Cathedral L1 participant starts.");
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
                    : (adapterId is CathedralNativePopulationId or CathedralNativeProgressionId)
                        ? activatedContent.PlayerClasses.Definitions.Single(definition =>
                            string.Equals(definition.Symbol, "rogue", StringComparison.Ordinal))
                        : activatedContent.PlayerClasses.Default;
                if ((adapterId is CathedralNativePopulationId or CathedralNativeProgressionId)
                    && !string.Equals(playerClass.Symbol, "rogue", StringComparison.Ordinal))
                    throw new NotSupportedException(
                        "The native-populated Cathedral profile is generated for the canonical Rogue class.");
                if ((adapterId is CathedralNativePopulationId or CathedralNativeProgressionId)) {
                    var canonical = activatedContent.PlayerLoadouts.CreateTemplate(
                        "native-cathedral-population-profile",
                        playerClass.ClassId,
                        goldOverride: null,
                        lifeOverride: null,
                        manaOverride: null,
                        startingLevelId,
                        startingPositionX: startingPosition.X,
                        startingPositionY: startingPosition.Y);
                    if (playerStart.Gold is uint gold && gold != canonical.Gold
                        || playerStart.Life is int life && life != canonical.Life
                        || playerStart.Mana is int mana && mana != canonical.Mana)
                        throw new NotSupportedException(
                            "The native-populated Cathedral profile requires canonical fresh Rogue resources.");
                }
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
                or DiabloAuthoritativeGameHostAdapterFactory.CathedralNativePopulationId
                or DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeProgressionId
            ? new DiabloAuthoritativeGameHostAdapterFactory(selectedFactoryId)
            : new DiabloAuthoritativeGameHostAdapterFactory(selectedFactoryId == DiabloAuthoritativeGameHostAdapterFactory.NativeGeneratedId)];
    }

    public static AuthoritativeGameHostAdapterCatalog Create(
        string? selectedFactoryId = null)
    {
        return new AuthoritativeGameHostAdapterCatalog(CreateFactories(selectedFactoryId), selectedFactoryId);
    }
}
