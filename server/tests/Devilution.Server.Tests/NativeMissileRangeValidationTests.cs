using System.Text.Json;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeMissileRangeValidationTests
{
    private static readonly AuthoritativeMonsterMissileCatalog Mappings = new([
        new("Bat", "Lightning", AuthoritativeProjectileBehavior.MonsterMagic, MonsterId: 43, SpellId: 3,
            ProjectileSpeed: 16, ProjectileLifetime: 2, ProjectileImpactLifetime: 8,
            RangedDelayRandomRange: 20, ProjectileDamageRandomRange: 10,
            DamageType: AuthoritativeDamageType.Lightning, IsPostMeleeMissile: true),
    ]);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)]
    [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(18)] [InlineData(19)]
    public void FamiliarMappingAndCohortRefuseCorruptNativePayloads(int defect)
    {
        var p = Familiar();
        p = defect switch {
            0 => p with { SpellId = 1 }, 1 => p with { NativeMissileId = "Firebolt" },
            2 => p with { Behavior = AuthoritativeProjectileBehavior.Targeted }, 3 => p with { RemainingTicks = 9 },
            4 => p with { RemainingTicks = 0 }, 5 => p with { Damage = 0 }, 6 => p with { Damage = 11 },
            7 => p with { VelocityX = 1 }, 8 => p with { PositionX = 112 }, 9 => p with { TargetX = 114 },
            10 => p with { StartX = 77 }, 11 => p with { SourceEntityId = 0 },
            12 => p with { NativeMonsterMissile = p.NativeMonsterMissile! with { SourcePositionX = 112 } },
            13 => p with { NativeMonsterMissile = p.NativeMonsterMissile! with { Kind = AuthoritativeNativeMonsterMissileKind.Acid } },
            14 => p with { NativeMonsterMissile = p.NativeMonsterMissile! with { Ending = true } },
            15 => p with { NativeWeaponArrow = new(0) },
            16 => p with { NativeMonsterMissile = p.NativeMonsterMissile! with { LastCollisionTargetHash = 256 } },
            17 => p with { PathIndex = 1 }, 18 => p with { Phase = AuthoritativeProjectilePhase.Impact },
            _ => p with { TraveledX = 1 },
        };
        Assert.Throws<InvalidDataException>(() => p.NativeMonsterMissile!.ValidateShape(p));
        var cohort = NativeMissileCohortFixture.Create(7) with { Projectiles = [p] };
        Assert.Throws<InvalidDataException>(() => new DiabloCohortCodec().Serialize(cohort));
        var snapshot = new DiabloSimulationSnapshot(0, cohort.Participants[0].Player, [], [], [], [], [p], null);
        Assert.Throws<InvalidDataException>(() => DiabloProtocolMapper.ToProtocolSnapshot(snapshot, monsterMissiles: Mappings));
    }

    [Theory]
    [InlineData(111, 0)] [InlineData(0, 111)] [InlineData(111, 111)] [InlineData(51, 53)]
    public void NativeFamiliarOffsetRestoresAtTheDungeonEdgeAndStaysNativeInTheCheckpoint(int x, int y)
    {
        var p = Familiar() with { PositionX = x, PositionY = y, StartX = x + 1, StartY = y + 1,
            TargetX = x + 1, TargetY = y + 1 };
        var cohort = NativeMissileCohortFixture.Create(7) with { Projectiles = [p] };
        var source = new DiabloSimulationSnapshot(0, cohort.Participants[0].Player, [], [], [], [], [p], null);
        var simulation = DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            FallbackWidth = 112, FallbackHeight = 112, MonsterMissiles = Mappings,
        });
        var player = new AuthoritativePlayerState(0, 0, 6400, 0, 0, PlayerAttributesState.Zero,
            [], [], [], 56, 56, 6400, 1, 7, 0) { EntityId = 1 };
        simulation.RestoreSnapshot(player, source);
        var restored = Assert.Single(simulation.WorldState.Projectiles.Values);
        Assert.Equal(0U, restored.SpellId);
        Assert.Equal((x + 1, y + 1), (restored.TargetX, restored.TargetY));
        var initialWorld = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112, projectiles: [restored]);
        Assert.Same(restored, initialWorld.Projectiles[restored.EntityId]);
        var codec = new DiabloCohortCodec();
        Assert.Equal(p, Assert.Single(codec.Deserialize(codec.Serialize(cohort)).Projectiles));
        var projected = Assert.Single(DiabloProtocolMapper.ToProtocolSnapshot(source, monsterMissiles: Mappings).Projectiles);
        Assert.Equal(3U, projected.SpellId);
        Assert.Equal((x, y), (projected.TargetX, projected.TargetY));
        Assert.Equal((x + 1, y + 1), (projected.StartX, projected.StartY));
        Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player,
            source with { Projectiles = [p with { TargetX = p.TargetX + 1 }] }));
    }

    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(27)]
    public void FrozenImpactAndLightningStagesRejectChangedGeometryClockAndDamage(int type)
    {
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112,
            fallbackMissileBlockedCells: [50 * 112 + 52]);
        var random = new DeterministicLegacyRandom(2588);
        var processor = new DiabloCombatCommandProcessor(world, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
            new AuthoritativeCombatRules(10, 1, 100), random);
        var player = new AuthoritativePlayerState(0, 0, 6400, 0, 0, PlayerAttributesState.Zero,
            [], [], [], 60, 60, 6400, 1, 7, 0) { EntityId = 1 };
        var initial = DiabloCombatCommandProcessor.AddObjectTrapMissile(800, 10, 11, 7, 1, 48, 50, 56, 50,
            (int)DeterministicGridDirections.Toward(48, 50, 56, 50), type, type == 27, random, 7);
        world.Projectiles.Add(initial.EntityId, initial);
        DiabloProjectileState? stage = null;
        for (ulong tick = 1; tick <= 256 && stage is null; tick++) {
            var ids = world.Projectiles.Keys.Order().ToList(); var seen = ids.ToHashSet();
            for (var index = 0; index < ids.Count; index++) {
                if (world.Projectiles.TryGetValue(ids[index], out var missile))
                    processor.ProcessObjectTrapMissile(tick, missile, 7, [player], []);
                foreach (var id in world.Projectiles.Keys.Order()) if (seen.Add(id)) ids.Add(id);
            }
            var stationary = world.Projectiles.Values.FirstOrDefault(value => value.NativeTrapArrow!.Stage != NativeObjectMissileStage.Flight);
            if (stationary is not null) stage = JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(stationary));
        }
        Assert.NotNull(stage);
        stage!.NativeTrapArrow!.ValidateShape(stage);
        var corruptions = new[] {
            stage with { PositionX = stage.PositionX + 1 }, stage with { TraveledX = stage.TraveledX + 1 },
            stage with { VelocityX = 1 }, stage with { Speed = 99 }, stage with { Damage = 99 },
            stage with { RemainingTicks = 257 }, stage with { Phase = (AuthoritativeProjectilePhase)9 },
            stage with { NativeTrapArrow = stage.NativeTrapArrow with { MissileDistance = 257 } },
        };
        foreach (var corrupt in corruptions)
            Assert.Throws<InvalidDataException>(() => corrupt.NativeTrapArrow!.ValidateShape(corrupt));
    }

    private static DiabloProjectileState Familiar() => new(2147483667, 151392341, 1, 0, 7, 51, 53, 52, 54,
        9, AuthoritativeDamageType.Lightning, 0, 4) {
        Behavior = AuthoritativeProjectileBehavior.MonsterMagic, NativeMissileId = "Lightning", StartX = 52, StartY = 54,
        ImpactLifetime = 0, NativeMonsterMissile = new(AuthoritativeNativeMonsterMissileKind.Lightning, 0, 0, false,
            50, 53, 13, 50, 4, 16, 3, false, false),
    };
}
