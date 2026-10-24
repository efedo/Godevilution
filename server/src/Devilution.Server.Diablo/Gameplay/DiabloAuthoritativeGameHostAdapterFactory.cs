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
    public const string CathedralNativeDepthProgressionId = "devilution.diablo.cathedral-native-depth-progression.v1";
    public const string CathedralNativeClassProgressionId = "devilution.diablo.cathedral-native-class-progression.v1";
    public const string CathedralNativeTownProgressionId = "devilution.diablo.cathedral-native-town-progression.v1";
    /** One native Town profile for Warrior, Rogue and Sorcerer; the first participant's class selects the run. */
    public const string CathedralNativeAllClassTownId = "devilution.diablo.cathedral-native-all-class-town.v1";
    private static readonly uint[] AllClassTownClassIds = [0, 1, 2];
    private readonly string adapterId;

    public DiabloAuthoritativeGameHostAdapterFactory() : this(false) { }

    public DiabloAuthoritativeGameHostAdapterFactory(bool nativeGenerated)
        : this(nativeGenerated ? NativeGeneratedId : "devilution.diablo") { }

    /** Explicit profile selection; the bool constructor retains its original L1 meaning. */
    public DiabloAuthoritativeGameHostAdapterFactory(string adapterId)
    {
        if (adapterId is not ("devilution.diablo" or NativeGeneratedId or CathedralDepthId or CathedralDoorId
            or CathedralNativePopulationId or CathedralNativeProgressionId or CathedralNativeDepthProgressionId
            or CathedralNativeClassProgressionId or CathedralNativeTownProgressionId or CathedralNativeAllClassTownId))
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
            CathedralNativeTownProgressionId or CathedralNativeAllClassTownId
                => registry.TryGetCapability<IDiabloCathedralNativeTownProgressionBootstrap>(out _),
            NativeGeneratedId => registry.TryGetCapability<IDiabloNativeGeneratedContentBootstrap>(out _),
            CathedralDepthId => registry.TryGetCapability<IDiabloCathedralDepthContentBootstrap>(out _),
            CathedralDoorId => registry.TryGetCapability<IDiabloCathedralDoorContentBootstrap>(out _),
            CathedralNativePopulationId => registry.TryGetCapability<IDiabloCathedralNativePopulationBootstrap>(out _),
            CathedralNativeProgressionId => registry.TryGetCapability<IDiabloCathedralNativeProgressionBootstrap>(out _),
            CathedralNativeDepthProgressionId or CathedralNativeClassProgressionId
                => registry.TryGetCapability<IDiabloCathedralNativeDepthProgressionBootstrap>(out _),
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
        if ((adapterId is CathedralNativePopulationId or CathedralNativeProgressionId
                or CathedralNativeDepthProgressionId or CathedralNativeClassProgressionId or CathedralNativeTownProgressionId
                or CathedralNativeAllClassTownId)
            && (playerStart.GameMode != AuthoritativeGameMode.SinglePlayer
                || playerStart.Difficulty != AuthoritativeGameDifficulty.Normal
                || playerStart.ProceduralMonsterLimit is not null
                || playerStart.ProceduralMonsterLevelId is not null
                || playerStart.StartingLevelId is not (null or 0 or 1)
                || playerStart.StatPoints is not (null or 0)))
            throw new NotSupportedException(
                "The native-populated Cathedral profile requires a Normal single-player start at Town or L1 without procedural overrides.");
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
        if (adapterId == CathedralNativeAllClassTownId) {
            var town = registry.GetRequiredCapability<IDiabloCathedralNativeTownProgressionBootstrap>();
            if (!town.GetNativeTownClassIds(content).Order().SequenceEqual(AllClassTownClassIds))
                throw new InvalidDataException("The all-class native Town profile must start Warrior, Rogue and Sorcerer.");
            // Each class is its own native run; activate all of them and keep the one the player picks.
            return new NativeClassSelectingGameHostAdapter(AllClassTownClassIds.ToDictionary(classId => classId,
                classId => CreateExecutor(town.ActivateNativeTownClass(content, classId, playerStart.RandomSeed),
                    playerStart, nativePopulation: true, classBound: true, nativeProtocol: true, allClass: true)));
        }
        var activatedContent = adapterId switch {
            CathedralNativeTownProgressionId => registry.GetRequiredCapability<IDiabloCathedralNativeTownProgressionBootstrap>()
                .ActivateNativeTownProgression(content, playerStart.RandomSeed),
            CathedralDoorId => registry.GetRequiredCapability<IDiabloCathedralDoorContentBootstrap>()
                .ActivateDoors(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            CathedralNativePopulationId => registry.GetRequiredCapability<IDiabloCathedralNativePopulationBootstrap>()
                .ActivateNativePopulation(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            CathedralNativeProgressionId => registry.GetRequiredCapability<IDiabloCathedralNativeProgressionBootstrap>()
                .ActivateNativeProgression(content, playerStart.RandomSeed),
            CathedralNativeDepthProgressionId or CathedralNativeClassProgressionId
                => registry.GetRequiredCapability<IDiabloCathedralNativeDepthProgressionBootstrap>()
                .ActivateNativeDepthProgression(content, playerStart.RandomSeed),
            NativeGeneratedId => registry.GetRequiredCapability<IDiabloNativeGeneratedContentBootstrap>()
                .ActivateGenerated(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            CathedralDepthId => registry.GetRequiredCapability<IDiabloCathedralDepthContentBootstrap>()
                .ActivateDepths(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
            _ => registry.GetRequiredCapability<IDiabloContentBootstrap>()
                .Activate(content, playerStart.RandomSeed, gameMode, monsterSpawns, difficulty),
        };
        return CreateExecutor(activatedContent, playerStart,
            nativePopulation: adapterId is CathedralNativePopulationId or CathedralNativeProgressionId
                or CathedralNativeDepthProgressionId or CathedralNativeClassProgressionId or CathedralNativeTownProgressionId,
            classBound: adapterId is CathedralNativeClassProgressionId or CathedralNativeTownProgressionId,
            nativeProtocol: adapterId is CathedralNativeDepthProgressionId or CathedralNativeClassProgressionId or CathedralNativeTownProgressionId);
    }

    private static StoreSimulationExecutor CreateExecutor(DiabloContentActivation activatedContent,
        AuthoritativePlayerStartConfiguration playerStart, bool nativePopulation, bool classBound, bool nativeProtocol,
        bool allClass = false)
    {
        var startingLevelId = playerStart.StartingLevelId
            ?? activatedContent.DefaultStartingLevelId;
        if (activatedContent.Simulation.WorldState.World is not { } world
            || !world.ContainsLevel(startingLevelId))
            throw new InvalidDataException(
                $"Starting level {startingLevelId} is not defined by the active content world.");
        var isNativePopulationProfile = nativePopulation;
        var isClassBoundNativeProfile = classBound;
        var characterStart = activatedContent.NativeCharacterStart;
        if (isClassBoundNativeProfile) {
            if (characterStart is not { HasCompiledTable: true, ClassId: 0 or 2 } && !(allClass && characterStart is { HasCompiledTable: true, ClassId: 1 }))
                throw new NotSupportedException(
                    "The class-bound native Cathedral progression requires an explicit Warrior or Sorcerer character-start table.");
        } else if (characterStart?.HasCompiledTable == true) {
            throw new NotSupportedException(
                "An explicit class-bound native character-start table requires the class-progression adapter profile.");
        }
        if (isNativePopulationProfile
            && startingLevelId is not (0 or 1))
            throw new NotSupportedException(
                "The native-populated Cathedral profile supports only Town and Cathedral L1 participant starts.");
        var startingPosition = world.GetStartPosition(startingLevelId);
        return new StoreSimulationExecutor(
            activatedContent,
            (sessionId, requestedClassId) => {
                var playerClass = isClassBoundNativeProfile
                    ? requestedClassId.HasValue && requestedClassId.Value != characterStart!.ClassId
                        ? throw new NotSupportedException(
                            $"The class-bound native profile requires its compiled {characterStart!.ClassSymbol} class.")
                        : activatedContent.PlayerClasses.GetRequired(characterStart!.ClassId)
                    : requestedClassId.HasValue
                    ? activatedContent.PlayerClasses.TryGet(
                        requestedClassId.Value,
                        out var selected)
                        ? selected
                        : throw new InvalidDataException(
                            $"Requested player class ID {requestedClassId.Value} is not defined by active content.")
                    : isNativePopulationProfile
                        ? activatedContent.PlayerClasses.Definitions.Single(definition =>
                            string.Equals(definition.Symbol, "rogue", StringComparison.Ordinal))
                        : activatedContent.PlayerClasses.Default;
                if (isNativePopulationProfile && !isClassBoundNativeProfile
                    && !string.Equals(playerClass.Symbol, "rogue", StringComparison.Ordinal))
                    throw new NotSupportedException(
                        "The native-populated Cathedral profile is generated for the canonical Rogue class.");
                if (isNativePopulationProfile) {
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
                            $"The native-populated Cathedral profile requires canonical fresh {playerClass.Symbol} resources.");
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
            },
            saveCodec: null,
            supportedProtocolFeatures: nativeProtocol
                ? AuthoritativeProtocolFeatures.TargetCellPathing
                    | AuthoritativeProtocolFeatures.QueuedRangedAttacks
                    | AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots
                : AuthoritativeProtocolFeatures.None);
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
                or DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeDepthProgressionId
                or DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeClassProgressionId
                or DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeTownProgressionId
                or DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeAllClassTownId
            ? new DiabloAuthoritativeGameHostAdapterFactory(selectedFactoryId)
            : new DiabloAuthoritativeGameHostAdapterFactory(selectedFactoryId == DiabloAuthoritativeGameHostAdapterFactory.NativeGeneratedId)];
    }

    public static AuthoritativeGameHostAdapterCatalog Create(
        string? selectedFactoryId = null)
    {
        return new AuthoritativeGameHostAdapterCatalog(CreateFactories(selectedFactoryId), selectedFactoryId);
    }
}
