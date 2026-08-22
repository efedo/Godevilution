using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class DialogueProtocolProjectionTests
{
    [Fact]
    public void ProjectsSpellTargetingMetadataFromAuthoritativeSpellBehavior()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
            AbilitySpellId = 33,
            MemorizedSpells = [new MemorizedSpellState(33, 1)],
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(33, 15, 0, 0, 0, 0) {
                DisplayName = "Telekinesis",
                ProjectileBehavior = AuthoritativeProjectileBehavior.Telekinesis,
            },
        ]);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source, spells: spells);

        Assert.Equal(
            SpellTargetingKind.WorldEntity,
            snapshot.Players[0].AbilityTargetingKind);
        Assert.Equal(
            SpellTargetingKind.WorldEntity,
            Assert.Single(snapshot.Players[0].MemorizedSpells).TargetingKind);
    }

    [Fact]
    public void RejectsPlayerResourcesOutsideTheSharedProtocolRange()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        void AssertRejected(Func<StorePlayerSnapshot, StorePlayerSnapshot> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Player = mutate(player),
                }));
        }

        AssertRejected(value => value with { Gold = (uint)int.MaxValue + 1U });
        AssertRejected(value => value with { StashGold = (uint)int.MaxValue + 1U });
        AssertRejected(value => value with { StatPoints = (uint)int.MaxValue + 1U });
        AssertRejected(value => value with { Life = -1 });
        AssertRejected(value => value with { LifeMaximum = -1 });
        AssertRejected(value => value with { Life = 65, LifeMaximum = 64 });
        AssertRejected(value => value with { Mana = -1 });
        AssertRejected(value => value with { ManaMaximum = -1 });
        AssertRejected(value => value with { Mana = 65, ManaMaximum = 64 });
        AssertRejected(value => value with { BaseLifeMaximum = -1 });
        AssertRejected(value => value with { BaseManaMaximum = -1 });
        AssertRejected(value => value with { ReflectCharges = ushort.MaxValue + 1U });
        AssertRejected(value => value with { CharacterLevel = 51 });
    }

    [Fact]
    public void RejectsNegativePlayerAttributesBeforeProjection()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Attributes = PlayerAttributesState.Zero with {
                        Dexterity = new PlayerAttributeState(-1, 0),
                    },
                },
            }));
    }

    [Fact]
    public void ProjectsAuthoredWarpTriggersWithQuestAvailability()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [new DiabloQuestState(3017, 0, 0, 1, true) { NativeQuestId = 16 }],
            [],
            null);
        var warp = new AuthoritativeWarpTarget(
            2,
            0,
            3,
            4,
            1,
            1,
            1,
            requiredQuestId: 3017,
            requiredQuestCompleted: true) {
            StableSymbol = "grave_matters_dungeon",
        };

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(
            source,
            levelGeometry: [
                new AuthoritativeLevel(0, 10, 10),
                new AuthoritativeLevel(1, 10, 10),
            ],
            warpTargets: [warp]);

        var trigger = Assert.Single(
            snapshot.LevelGeometry.Single(level => level.LevelId == 0).WarpTriggers);
        Assert.Equal(2U, trigger.WarpId);
        Assert.Equal((3, 4), (trigger.TriggerX, trigger.TriggerY));
        Assert.Equal((1U, 1, 1), (trigger.TargetLevelId, trigger.TargetX, trigger.TargetY));
        Assert.Equal(3017U, trigger.RequiredQuestId);
        Assert.True(trigger.RequiredQuestCompleted);
        Assert.True(trigger.Available);
        Assert.Equal("grave_matters_dungeon", trigger.StableSymbol);
        Assert.Equal(16U, Assert.Single(snapshot.Quests).NativeQuestId);
    }

    [Fact]
    public void OmitsNativeQuestOrdinalWhenNoContentMappingExists()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) { EntityId = 1 };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [new DiabloQuestState(3017, 0, 0, 1, false)],
            [],
            null);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.False(Assert.Single(snapshot.Quests).HasNativeQuestId);
    }

    [Fact]
    public void ProjectsServerOwnedTextAndPreservesNumericIdentity()
    {
        var catalog = new AuthoritativeDialogueMessageCatalog([
            new(110, "TEXT_WARLRD9", "My blade sings."),
        ]);
        var batch = DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                12,
                34,
                0) {
                QuestId = 3012,
                MessageId = 110,
            },
        ], 12, 7, catalog);

        Assert.NotNull(batch);
        var dialogue = Assert.Single(batch!.Events).Dialogue;
        Assert.Equal(3012U, dialogue.QuestId);
        Assert.Equal(110, dialogue.MessageId);
        Assert.Equal("My blade sings.", dialogue.Text);
    }

    [Fact]
    public void OmittingCatalogKeepsIdOnlyCompatibility()
    {
        var batch = DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                0,
                34,
                0) {
                MessageId = 21,
            },
        ], 1, 1);

        Assert.NotNull(batch);
        Assert.Equal(string.Empty, Assert.Single(batch!.Events).Dialogue.Text);
    }

    [Theory]
    [InlineData(AuthoritativeProjectileBehavior.ChargedBolt, NativeProjectileVisual.ChargedBolt)]
    [InlineData(AuthoritativeProjectileBehavior.MonsterChargedBolt, NativeProjectileVisual.ChargedBolt)]
    [InlineData(AuthoritativeProjectileBehavior.Firebolt, NativeProjectileVisual.Firebolt)]
    [InlineData(AuthoritativeProjectileBehavior.Lightning, NativeProjectileVisual.Lightning)]
    [InlineData(AuthoritativeProjectileBehavior.LightningSegment, NativeProjectileVisual.ThinLightning)]
    [InlineData(AuthoritativeProjectileBehavior.FlashBottom, NativeProjectileVisual.FlashBottom)]
    [InlineData(AuthoritativeProjectileBehavior.FlashTop, NativeProjectileVisual.FlashTop)]
    [InlineData(AuthoritativeProjectileBehavior.ResurrectionBeam, NativeProjectileVisual.ResurrectBeam)]
    [InlineData(AuthoritativeProjectileBehavior.Phasing, NativeProjectileVisual.Phasing)]
    [InlineData(AuthoritativeProjectileBehavior.Fireball, NativeProjectileVisual.Fireball)]
    [InlineData(AuthoritativeProjectileBehavior.HolyBolt, NativeProjectileVisual.HolyBolt)]
    [InlineData(AuthoritativeProjectileBehavior.BloodStar, NativeProjectileVisual.BloodStar)]
    [InlineData(AuthoritativeProjectileBehavior.BoneSpirit, NativeProjectileVisual.BoneSpirit)]
    [InlineData(AuthoritativeProjectileBehavior.FireWallSegment, NativeProjectileVisual.FireWall)]
    [InlineData(AuthoritativeProjectileBehavior.FlameWaveController, NativeProjectileVisual.FlameWaveControl)]
    [InlineData(AuthoritativeProjectileBehavior.FlameWaveSegment, NativeProjectileVisual.FlameWave)]
    [InlineData(AuthoritativeProjectileBehavior.InfernoSegment, NativeProjectileVisual.Inferno)]
    [InlineData(AuthoritativeProjectileBehavior.ChainLightningBolt, NativeProjectileVisual.ChainLightning)]
    [InlineData(AuthoritativeProjectileBehavior.ApocalypseBoom, NativeProjectileVisual.ApocalypseBoom)]
    [InlineData(AuthoritativeProjectileBehavior.Elemental, NativeProjectileVisual.Elemental)]
    [InlineData(AuthoritativeProjectileBehavior.GuardianController, NativeProjectileVisual.Guardian)]
    [InlineData(AuthoritativeProjectileBehavior.Etherealize, NativeProjectileVisual.Etherealize)]
    [InlineData(AuthoritativeProjectileBehavior.Acid, NativeProjectileVisual.Acid)]
    [InlineData(AuthoritativeProjectileBehavior.AcidSplat, NativeProjectileVisual.AcidSplat)]
    [InlineData(AuthoritativeProjectileBehavior.AcidPuddle, NativeProjectileVisual.AcidPuddle)]
    [InlineData(AuthoritativeProjectileBehavior.HorkSpawn, NativeProjectileVisual.HorkSpawn)]
    [InlineData(AuthoritativeProjectileBehavior.NovaBall, NativeProjectileVisual.NovaBall)]
    [InlineData(AuthoritativeProjectileBehavior.WeaponArrow, NativeProjectileVisual.Arrow)]
    [InlineData(AuthoritativeProjectileBehavior.MonsterArrow, NativeProjectileVisual.Arrow)]
    public void ProjectsServerOwnedNativeProjectileVisual(
        AuthoritativeProjectileBehavior behavior,
        NativeProjectileVisual expectedVisual)
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [new DiabloProjectileState(
                2,
                1,
                0,
                2,
                1,
                4,
                4,
                4,
                4,
                0,
                AuthoritativeDamageType.Fire,
                0,
                3) {
                Behavior = behavior,
            }],
            null);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.Equal(expectedVisual, Assert.Single(snapshot.Projectiles).NativeVisual);
        if (behavior == AuthoritativeProjectileBehavior.NovaBall)
            Assert.Equal(ProjectilePresentationKind.NovaBall, Assert.Single(snapshot.Projectiles).PresentationKind);
    }

    [Theory]
    [InlineData(AuthoritativeProjectileBehavior.Targeted)]
    [InlineData(AuthoritativeProjectileBehavior.Flash)]
    [InlineData(AuthoritativeProjectileBehavior.Healing)]
    [InlineData(AuthoritativeProjectileBehavior.Teleport)]
    [InlineData(AuthoritativeProjectileBehavior.MonsterMagic)]
    public void LeavesNonSpriteProjectileBehaviorsUnspecified(
        AuthoritativeProjectileBehavior behavior)
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [new DiabloProjectileState(
                2,
                1,
                0,
                2,
                1,
                4,
                4,
                4,
                4,
                0,
                AuthoritativeDamageType.Fire,
                0,
                3) {
                Behavior = behavior,
            }],
            null);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.Equal(NativeProjectileVisual.Unspecified, Assert.Single(snapshot.Projectiles).NativeVisual);
    }
}
