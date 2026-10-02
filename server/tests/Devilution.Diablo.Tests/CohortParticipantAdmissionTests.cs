using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Focused module coverage for explicit admission after cohort restoration. */
public sealed class CohortParticipantAdmissionTests
{
    [Fact]
    public void RestoredRuntimeAdmitsMatchingPolicyAndNeverImplicitlyCreates()
    {
        var fixture = CreateFixture();
        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();

        Assert.Throws<InvalidOperationException>(
            () => restored.Read("missing", _ => 0));

        var admitted = fixture.Template.CreatePlayer();
        admitted.EntityId = 2;
        candidate.Simulation.AdmitCohortParticipant(restored, "new", admitted);

        var newPlayer = restored.Read("new", player => player);
        Assert.Equal(2U, newPlayer.EntityId);
        Assert.Same(fixture.Classes.Default, newPlayer.PlayerClass);
        Assert.Same(fixture.Items, newPlayer.Items);
        var after = candidate.Simulation.CaptureCohortState(restored);
        Assert.Equal(
            ["hero", "new"],
            after.Participants.Select(participant => participant.SessionId));
        Assert.Equal(
            [1U, 2U],
            after.Participants.Select(participant => participant.EntityId));
    }

    [Fact]
    public void RestoredAdmissionRejectsMissingMismatchedAndCollidingBindings()
    {
        var fixture = CreateFixture();
        var checkpoint = fixture.Simulation.CaptureCohortState(fixture.Runtime);

        AssertRejectedWithoutMutation(
            fixture,
            checkpoint,
            () => CreatePlayer(fixture, 0));
        AssertRejectedWithoutMutation(
            fixture,
            checkpoint,
            () => CreatePlayer(fixture, 1));
        AssertRejectedWithoutMutation(
            fixture,
            checkpoint,
            () => CreatePlayer(
                fixture,
                2,
                CreateClass("independent_class"),
                fixture.Items));
        AssertRejectedWithoutMutation(
            fixture,
            checkpoint,
            () => CreatePlayer(fixture, 2, null, fixture.Items));
        AssertRejectedWithoutMutation(
            fixture,
            checkpoint,
            () => CreatePlayer(fixture, 2, fixture.Classes.Default, null));
        AssertRejectedWithoutMutation(
            fixture,
            checkpoint,
            () => CreatePlayer(fixture, 2, fixture.Classes.Default, CreateItems()));
    }

    private static void AssertRejectedWithoutMutation(
        Fixture fixture,
        DiabloCohortState checkpoint,
        Func<AuthoritativePlayerState> createPlayer)
    {
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            fixture.Options,
            checkpoint);
        var restored = candidate.CreateRuntime();
        var player = createPlayer();

        Assert.Throws<InvalidDataException>(
            () => candidate.Simulation.AdmitCohortParticipant(restored, "new", player));
        var after = candidate.Simulation.CaptureCohortState(restored);
        Assert.Single(after.Participants);
        Assert.Equal("hero", after.Participants[0].SessionId);
        Assert.Equal(1U, after.Participants[0].EntityId);
    }

    [Fact]
    public async Task ConcurrentAdmissionCannotRegisterTheSameEntityTwice()
    {
        var fixture = CreateFixture();
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(fixture.Options,
            fixture.Simulation.CaptureCohortState(fixture.Runtime));
        var runtime = candidate.CreateRuntime();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => Task.Run(() => {
            var player = fixture.Template.CreatePlayer();
            player.EntityId = 2;
            try {
                candidate.Simulation.AdmitCohortParticipant(runtime, "new-" + index, player);
                return true;
            } catch (InvalidDataException) { return false; }
        }, TestContext.Current.CancellationToken)));
        Assert.Single(results, success => success);
        var state = candidate.Simulation.CaptureCohortState(runtime);
        Assert.Equal(2, state.Participants.Count);
        Assert.Single(state.Participants, participant => participant.EntityId == 2);
    }

    [Fact]
    public void ExistingActorAndForeignRuntimeAreRejectedWithoutNormalization()
    {
        var fixture = CreateFixture();
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(fixture.Options,
            fixture.Simulation.CaptureCohortState(fixture.Runtime));
        var runtime = candidate.CreateRuntime();
        var before = candidate.Simulation.CaptureCohortState(runtime);
        var existing = runtime.Read("hero", player => player);
        Assert.Throws<InvalidOperationException>(() =>
            candidate.Simulation.AdmitCohortParticipant(runtime, "other", existing));
        Assert.Throws<InvalidOperationException>(() =>
            candidate.Simulation.AdmitCohortParticipant(fixture.Runtime, "other", existing));
        Assert.Equivalent(before, candidate.Simulation.CaptureCohortState(runtime), strict: true);
    }

    private static AuthoritativePlayerState CreatePlayer(
        Fixture fixture,
        uint entityId,
        AuthoritativePlayerClassDefinition? playerClass = null,
        AuthoritativeItemCatalog? items = null)
    {
        var template = playerClass == fixture.Classes.Default
            && items == fixture.Items
            ? fixture.Template
            : new DiabloPlayerTemplate(
                gold: 100,
                experience: 0,
                life: 50,
                mana: 0,
                manaMaximum: 0,
                attributes: (playerClass ?? fixture.Classes.Default).CreateStartingAttributes(),
                equipment: null,
                inventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
                belt: null,
                positionX: 1,
                positionY: 1,
                lifeMaximum: 50,
                characterLevel: 1,
                levelId: 0,
                armorClass: 0,
                characterClassId: (playerClass ?? fixture.Classes.Default).ClassId,
                playerClass: playerClass,
                items: items);
        var player = template.CreatePlayer();
        player.EntityId = entityId;
        return player;
    }

    private static Fixture CreateFixture()
    {
        var classes = new AuthoritativePlayerClassCatalog([CreateClass("active_class")]);
        var items = CreateItems();
        var options = new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            FallbackWidth = 8,
            FallbackHeight = 8,
            PlayerClasses = classes,
            Items = items,
            RandomSeed = 19,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var template = new DiabloPlayerTemplate(
            gold: 100,
            experience: 0,
            life: 50,
            mana: 0,
            manaMaximum: 0,
            attributes: classes.Default.CreateStartingAttributes(),
            equipment: null,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            belt: null,
            positionX: 1,
            positionY: 1,
            lifeMaximum: 50,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            characterClassId: classes.Default.ClassId,
            playerClass: classes.Default,
            items: items);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("hero", player => player.EntityId = 1);
        return new Fixture(options, simulation, runtime, classes, items, template);
    }

    private static AuthoritativePlayerClassDefinition CreateClass(string symbol)
    {
        return new AuthoritativePlayerClassDefinition(
            ClassId: 0,
            Symbol: symbol,
            DisplayName: symbol,
            Availability: DiabloPlayerClassAvailability.BaseGame,
            IsDefault: true,
            Flags: DiabloPlayerClassFlags.None,
            BaseStrength: 30,
            BaseMagic: 10,
            BaseDexterity: 20,
            BaseVitality: 25,
            MaximumStrength: 250,
            MaximumMagic: 250,
            MaximumDexterity: 250,
            MaximumVitality: 250,
            BaseBlockChance: 0,
            BaseMagicToHit: 0,
            BaseMeleeToHit: 0,
            BaseRangedToHit: 0,
            LifeAdjustmentQ6: 0,
            ManaAdjustmentQ6: 0,
            LifePerLevelQ6: 64,
            ManaPerLevelQ6: 32,
            LifePerVitalityQ6: 64,
            ManaPerMagicQ6: 32,
            ItemLifePerVitalityQ6: 0,
            ItemManaPerMagicQ6: 0) { MonkArmorLevelDivisor = 1 };
    }

    private static AuthoritativeItemCatalog CreateItems()
    {
        return new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 23,
                MiscId = 44,
                StableSymbol = "pvp_ear",
            },
            new AuthoritativeItemDefinition(
                500,
                1,
                10,
                10,
                1,
                4,
                0,
                10,
                10,
                1,
                1,
                true) {
                EquipLocation = 1,
                ItemClass = 1,
                StableSymbol = "admission_sword",
            },
        ]);
    }

    private sealed record Fixture(
        DiabloSimulationOptions Options,
        DiabloSimulation Simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> Runtime,
        AuthoritativePlayerClassCatalog Classes,
        AuthoritativeItemCatalog Items,
        DiabloPlayerTemplate Template);
}
