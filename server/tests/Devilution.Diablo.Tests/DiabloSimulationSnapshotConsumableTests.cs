using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Snapshot lifecycle coverage for consumed sources retained by timed casts. */
public sealed class DiabloSimulationSnapshotConsumableTests
{
    [Fact]
    public void TimedConsumableCastRestoresAfterEffectWithoutReconsumingOrReplaying()
    {
        var fixture = CreateFixture();
        var cast = new DiabloCastIntent(
            1,
            0,
            fixture.Player.PositionX,
            fixture.Player.PositionY,
            DiabloCastSourceKind.ConsumableItem,
            new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                0,
                9001));

        var accepted = fixture.Simulation.Execute(
            fixture.Player,
            cast,
            1,
            [fixture.Player]);

        Assert.True(accepted.Succeeded, accepted.Failure?.ToString());
        var action = Assert.IsType<AuthoritativePlayerActionState>(
            fixture.Player.CurrentAction);
        fixture.Simulation.AdvanceTick(action.EffectTick, [fixture.Player]);

        Assert.Empty(fixture.Player.Inventory);
        Assert.True(action.EffectApplied);
        var snapshot = fixture.Simulation.CaptureSnapshot(
            fixture.Player,
            action.EffectTick + 1);
        Assert.NotEmpty(snapshot.Projectiles);

        var restoredSimulation = CreateSimulation();
        var restoredPlayer = CreatePlayer();
        restoredSimulation.RestoreSnapshot(restoredPlayer, snapshot);

        Assert.Empty(restoredPlayer.Inventory);
        Assert.True(restoredPlayer.CurrentAction!.EffectApplied);
        Assert.Equal(snapshot.RandomState, restoredSimulation.RandomState);
        Assert.Equal(
            snapshot.Projectiles.Select(projectile => projectile.EntityId),
            restoredSimulation.WorldState.Projectiles.Keys.Order());

        restoredSimulation.AdvanceTick(snapshot.Tick + 1, [restoredPlayer]);

        Assert.Empty(restoredPlayer.Inventory);
        Assert.Equal(snapshot.RandomState, restoredSimulation.RandomState);
        var restoredAction = Assert.IsType<AuthoritativePlayerActionState>(
            restoredPlayer.CurrentAction);
        var originalProjectileIds = snapshot.Projectiles
            .Select(projectile => projectile.EntityId)
            .ToHashSet();

        restoredSimulation.AdvanceTick(restoredAction.EndTick, [restoredPlayer]);

        Assert.Null(restoredPlayer.CurrentAction);
        Assert.Equal(DiabloPlayerCombatMode.Stand, restoredPlayer.CombatMode);
        Assert.Empty(restoredPlayer.Inventory);
        Assert.Equal(snapshot.RandomState, restoredSimulation.RandomState);
        Assert.All(
            restoredSimulation.WorldState.Projectiles.Keys,
            projectileId => Assert.Contains(projectileId, originalProjectileIds));
    }

    [Fact]
    public void SnapshotRestoreRejectsMissingConsumableSourceBeforeEffect()
    {
        var fixture = CreateFixture();
        var accepted = fixture.Simulation.Execute(
            fixture.Player,
            new DiabloCastIntent(
                1,
                0,
                fixture.Player.PositionX,
                fixture.Player.PositionY,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    9001)),
            1,
            [fixture.Player]);
        Assert.True(accepted.Succeeded, accepted.Failure?.ToString());

        var snapshot = fixture.Simulation.CaptureSnapshot(fixture.Player, 1);
        var missingSource = snapshot with {
            Player = snapshot.Player with {
                Inventory = [],
                InventoryGrid = Enumerable.Repeat(-1, 40).ToArray(),
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            CreateSimulation().RestoreSnapshot(CreatePlayer(), missingSource));
    }

    [Fact]
    public void SnapshotRestoreRejectsMalformedHistoricalConsumableReferences()
    {
        var snapshot = CreatePostEffectSnapshot();
        var currentAction = snapshot.Player.CurrentAction!;

        var invalidLocation = snapshot with {
            Player = snapshot.Player with {
                CurrentAction = currentAction with {
                    CastIntent = currentAction.CastIntent! with {
                        SourceItem = new DiabloPlayerItemReference(
                            (DiabloPlayerItemLocation)99,
                            0,
                            9001),
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            CreateSimulation().RestoreSnapshot(CreatePlayer(), invalidLocation));

        var invalidInventorySlot = snapshot with {
            Player = snapshot.Player with {
                CurrentAction = currentAction with {
                    CastIntent = currentAction.CastIntent! with {
                        SourceItem = new DiabloPlayerItemReference(
                            DiabloPlayerItemLocation.Inventory,
                            40,
                            9001),
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            CreateSimulation().RestoreSnapshot(CreatePlayer(), invalidInventorySlot));

        var invalidStashPage = snapshot with {
            Player = snapshot.Player with {
                CurrentAction = currentAction with {
                    CastIntent = currentAction.CastIntent! with {
                        SourceItem = new DiabloPlayerItemReference(
                            DiabloPlayerItemLocation.Stash,
                            0,
                            9001,
                            100),
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            CreateSimulation().RestoreSnapshot(CreatePlayer(), invalidStashPage));
    }

    private static DiabloSimulationSnapshot CreatePostEffectSnapshot()
    {
        var fixture = CreateFixture();
        var result = fixture.Simulation.Execute(
            fixture.Player,
            new DiabloCastIntent(
                1,
                0,
                fixture.Player.PositionX,
                fixture.Player.PositionY,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    9001)),
            1,
            [fixture.Player]);
        Assert.True(result.Succeeded, result.Failure?.ToString());
        var action = Assert.IsType<AuthoritativePlayerActionState>(
            fixture.Player.CurrentAction);
        fixture.Simulation.AdvanceTick(action.EffectTick, [fixture.Player]);
        return fixture.Simulation.CaptureSnapshot(fixture.Player, action.EffectTick + 1);
    }

    private static TestFixture CreateFixture()
    {
        var simulation = CreateSimulation();
        return new TestFixture(simulation, CreatePlayer(withConsumable: true));
    }

    private static DiabloSimulation CreateSimulation()
    {
        var contentRoot = FindRepositoryRoot();
        var baseContent = Path.Combine(contentRoot, "server", "content", "base");
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(baseContent, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(baseContent, "class_animations.tsv")),
            classes);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0) {
                Range = 112,
                DamageType = AuthoritativeDamageType.Fire,
                ProjectileSpeed = 26,
                ProjectileLifetime = 256,
                ProjectileBehavior = AuthoritativeProjectileBehavior.Firebolt,
                RandomDamageRange = 10,
                MagicDamageDivisor = 8,
                DamageBonus = 1,
            },
        ]);
        return DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                Spells = spells,
                PlayerClasses = classes,
                PlayerAnimations = animations,
                UseNativePlayerActionTiming = true,
                RandomSeed = 1234,
            });
    }

    private static AuthoritativePlayerState CreatePlayer(
        bool withConsumable = false)
    {
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        IReadOnlyList<OwnedStoreItem> inventory = withConsumable
            ? [new OwnedStoreItem(
                0,
                0,
                7001,
                0,
                0,
                AuthoritativeItemState.Empty with {
                    MiscId = 21,
                    SpellId = 1,
                    StableItemId = 9001,
                })]
            : [];
        if (withConsumable)
            inventoryGrid[0] = 0;
        return new AuthoritativePlayerState(
            100,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            inventoryGrid,
            [],
            4,
            4,
            50,
            1,
            0,
            0,
            characterClassId: 0,
            inventory: inventory) {
            EntityId = 7,
        };
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }

    private sealed record TestFixture(
        DiabloSimulation Simulation,
        AuthoritativePlayerState Player);
}
