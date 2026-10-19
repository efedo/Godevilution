using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloNativeClassProgressionAdapterTests
{
    private const string AdapterId = "devilution.diablo.cathedral-native-class-progression.v1";

    [Theory]
    [InlineData(0U, 0U, DiabloPlayerAbilityKind.Repair)]
    [InlineData(0U, 1U, DiabloPlayerAbilityKind.Repair)]
    [InlineData(2U, 0U, DiabloPlayerAbilityKind.Recharge)]
    [InlineData(2U, 1U, DiabloPlayerAbilityKind.Recharge)]
    public void ClassProgressionAdapterDefaultsToItsBoundClassAtTownOrL1(
        uint classId,
        uint startingLevelId,
        DiabloPlayerAbilityKind expectedAbility)
    {
        var fixture = CreateFixture(classId, includeTable: true);
        var factory = Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories(AdapterId));
        var registry = CreateRegistry(fixture);
        Assert.True(factory.CanActivate(registry));

        var adapter = Assert.IsType<StoreSimulationExecutor>(factory.Activate(
            registry,
            fixture.Content,
            new AuthoritativePlayerStartConfiguration(null, null, null) {
                StartingLevelId = startingLevelId,
                RandomSeed = 42,
            }));
        var defaultPlayer = adapter.GetPlayerState("default");
        Assert.Equal(classId, defaultPlayer.CharacterClassId);
        Assert.Equal(startingLevelId, defaultPlayer.LevelId);
        Assert.Equal(expectedAbility, defaultPlayer.AbilityKind);
        Assert.Equal(fixture.Activation.PlayerLoadouts.CreateTemplate(
            "expected", classId, null, null, null, startingLevelId).BaseLifeMaximum,
            defaultPlayer.BaseLifeMaximum);
        Assert.Equal(AuthoritativeProtocolFeatures.TargetCellPathing
            | AuthoritativeProtocolFeatures.QueuedRangedAttacks
            | AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots,
            adapter.SupportedProtocolFeatures);

        adapter.InitializeParticipant("bound-default", new AuthoritativeParticipantSelection(null));
        Assert.Equal(classId, adapter.GetPlayerState("bound-default").CharacterClassId);
        Assert.Throws<NotSupportedException>(() => adapter.InitializeParticipant(
            "wrong-base-class", new AuthoritativeParticipantSelection(classId == 0 ? 2U : 0U)));
        Assert.Throws<NotSupportedException>(() => adapter.InitializeParticipant(
            "expansion-class", new AuthoritativeParticipantSelection(4U)));
    }

    [Fact]
    public void ClassProgressionRequiresExplicitSupportedTableAndCanonicalOverrides()
    {
        var classFixture = CreateFixture(0, includeTable: true);
        var classFactory = Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories(AdapterId));
        var classRegistry = CreateRegistry(classFixture);
        Assert.Throws<NotSupportedException>(() => classFactory.Activate(
            classRegistry, classFixture.Content,
            new AuthoritativePlayerStartConfiguration(101, null, null) { StartingLevelId = 0 }));

        var missingTableFixture = CreateFixture(0, includeTable: false);
        var missingTableRegistry = CreateRegistry(missingTableFixture);
        Assert.Throws<NotSupportedException>(() => classFactory.Activate(
            missingTableRegistry, missingTableFixture.Content,
            new AuthoritativePlayerStartConfiguration(null, null, null) { StartingLevelId = 0 }));

        var oldFactory = Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories(
            DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeDepthProgressionId));
        var oldRegistry = CreateRegistry(classFixture);
        Assert.Throws<NotSupportedException>(() => oldFactory.Activate(
            oldRegistry, classFixture.Content,
            new AuthoritativePlayerStartConfiguration(null, null, null) { StartingLevelId = 0 }));

        var rogueFixture = CreateFixture(1, includeTable: false);
        var rogueFactory = Assert.Single(BuiltInAuthoritativeGameHostAdapters.CreateFactories(
            DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeDepthProgressionId));
        var rogueRegistry = CreateRegistry(rogueFixture);
        var rogueAdapter = Assert.IsType<StoreSimulationExecutor>(rogueFactory.Activate(
            rogueRegistry, rogueFixture.Content,
            new AuthoritativePlayerStartConfiguration(null, null, null) { StartingLevelId = 0 }));
        Assert.Equal(1U, rogueAdapter.GetPlayerState("default").CharacterClassId);
    }

    private static Fixture CreateFixture(uint classId, bool includeTable)
    {
        var root = FindRepositoryRoot();
        var pack = ContentPackLoader.LoadManifestDirectory(Path.Combine(root, "server", "content", "base"));
        var content = ComposedContent.Compose(new ContentManifest(pack.Id, pack.Version, [pack]));
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(content, randomSeed: 42);
        var table = CreateCharacterStartTable(activation, classId);
        var resolved = AuthoritativeCathedralNativeCharacterStart.Resolve(
            includeTable ? table : null,
            activation.PlayerClasses,
            activation.PlayerAnimations,
            activation.PlayerLoadouts);
        return new(content, activation with { NativeCharacterStart = resolved });
    }

    private static TsvTable CreateCharacterStartTable(DiabloContentActivation activation, uint classId)
    {
        var playerClass = activation.PlayerClasses.GetRequired(classId);
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-cathedral-population-profile", classId, null, null, null, 1,
            startingPositionX: 57, startingPositionY: 40);
        var player = template.CreatePlayer();
        return TsvTable.Parse(
            AuthoritativeCathedralNativeCharacterStart.TableName,
            "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\n"
                + $"{classId}\t{playerClass.Symbol}\t{template.BaseLifeMaximum}\t{template.BaseManaMaximum}\t{player.LightRadius}\t{activation.PlayerAnimations.GetRequired(classId).IdleFrames}\n");
    }

    private static GameplayModuleRegistry CreateRegistry(Fixture fixture)
    {
        var registry = new GameplayModuleRegistry();
        registry.Load([new NativeDepthBootstrapModule(fixture.Activation)],
            new GameplayModuleContext(fixture.Content.Manifest, new DelegateAuthoritativeClock(() => 0)));
        return registry;
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var roots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in roots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed record Fixture(ComposedContent Content, DiabloContentActivation Activation);

    private sealed class NativeDepthBootstrapModule(DiabloContentActivation activation) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } = new(
            "test.native-class-progression",
            "1.0.0",
            GameplayModuleIdentity.BuiltInAssemblyHash("test.native-class-progression", "1.0.0"));

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
            => registry.RegisterCapability<IDiabloCathedralNativeDepthProgressionBootstrap>(
                new NativeDepthBootstrap(activation));
    }

    private sealed class NativeDepthBootstrap(DiabloContentActivation activation)
        : IDiabloCathedralNativeDepthProgressionBootstrap
    {
        public DiabloContentActivation ActivateNativeDepthProgression(ComposedContent content, uint randomSeed = 0)
            => activation;
    }
}
