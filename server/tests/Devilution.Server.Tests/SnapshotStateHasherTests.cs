using Devilution.Protocol.V1;
using Devilution.Server.Gameplay;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class SnapshotStateHasherTests
{
    [Fact]
    public void SetLevelObjectOriginsAreCanonicalAndHashRelevant()
    {
        var first = new Snapshot {
            MaterializedProceduralLevelIds = { 17, 1 },
            SetLevelObjectOrigins = {
                new SetLevelObjectOriginSnapshot {
                    SetLevelId = 5,
                    LevelId = 23,
                    PositionX = 12,
                    PositionY = 19,
                },
                new SetLevelObjectOriginSnapshot {
                    SetLevelId = 2,
                    LevelId = 17,
                    PositionX = 31,
                    PositionY = 42,
                },
            },
        };
        var reordered = first.Clone();
        var origins = reordered.SetLevelObjectOrigins.ToArray();
        reordered.SetLevelObjectOrigins.Clear();
        reordered.SetLevelObjectOrigins.Add(origins.Reverse());
        var changed = first.Clone();
        changed.SetLevelObjectOrigins[0].PositionX++;
        var changedProceduralLevel = first.Clone();
        changedProceduralLevel.MaterializedProceduralLevelIds[0]++;
        var reorderedProceduralLevels = first.Clone();
        var proceduralLevels = reorderedProceduralLevels.MaterializedProceduralLevelIds.ToArray();
        reorderedProceduralLevels.MaterializedProceduralLevelIds.Clear();
        reorderedProceduralLevels.MaterializedProceduralLevelIds.Add(proceduralLevels.Reverse());

        Assert.Equal(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(reordered));
        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(changed));
        Assert.Equal(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(reorderedProceduralLevels));
        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(changedProceduralLevel));
    }

    [Fact]
    public void EmptySnapshotHashesItsZeroPlayerCount()
    {
        var snapshot = new Snapshot();

        Assert.Equal("3e7077fd2f66d689e0cee6a7cf5b37bf2dca7c979af356d0a31cbc5c85605c7d", SnapshotStateHasher.Compute(snapshot));
    }

    [Fact]
    public void PlayerAndInventoryOrderingDoesNotChangeHash()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 2,
                    Gold = 100,
                    Experience = 200,
                    Inventory = {
                        new ItemSnapshot { StoreId = 1, StoreSlot = 2, ItemSeed = 30 },
                        new ItemSnapshot { StoreId = 1, StoreSlot = 1, ItemSeed = 20 },
                    },
                },
                new PlayerSnapshot { EntityId = 1, Gold = 50 },
            },
        };
        var second = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 1, Gold = 50 },
                new PlayerSnapshot {
                    EntityId = 2,
                    Gold = 100,
                    Experience = 200,
                    Inventory = {
                        new ItemSnapshot { StoreId = 1, StoreSlot = 1, ItemSeed = 20 },
                        new ItemSnapshot { StoreId = 1, StoreSlot = 2, ItemSeed = 30 },
                    },
                },
            },
        };

        Assert.Equal(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void ItemAffixAndBuffFieldsChangeTheStateHash()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    Inventory = {
                        new ItemSnapshot { State = new ItemStateSnapshot { PrefixPower = 1, Buff = 2 } },
                    },
                },
            },
        };
        var second = first.Clone();
        second.Players[0].Inventory[0].State.Buff = 3;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void ActiveStoreItemStateChangesTheStateHash()
    {
        var first = new Snapshot {
            ActiveStore = new StoreSnapshot {
                StoreId = 1,
                Items = {
                    new StoreItemSnapshot {
                        StoreSlot = 0,
                        ItemSeed = 42,
                        Price = 75,
                        State = new ItemStateSnapshot { PrefixPower = 1, Buff = 2 },
                    },
                },
            },
        };
        var second = first.Clone();
        second.ActiveStore.Items[0].State.Buff = 3;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void BroaderPlayerProjectionFieldsChangeTheStateHash()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    Mana = 10,
                    ManaMaximum = 20,
                    Belt = {
                        new BeltItemSnapshot { Slot = 0, ItemSeed = 42 },
                    },
                },
            },
        };
        var changedMaximum = first.Clone();
        changedMaximum.Players[0].ManaMaximum = 21;
        var changedBelt = first.Clone();
        changedBelt.Players[0].Belt[0].ItemSeed = 43;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(changedMaximum));
        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(changedBelt));
    }

    [Fact]
    public void DiabloKillLevelChangesTheStateHash()
    {
        var baseline = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1 } },
        };
        var ranked = baseline.Clone();
        ranked.Players[0].DiabloKillLevel = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(ranked));
    }

    [Fact]
    public void VictoryStateChangesTheStateHash()
    {
        var baseline = new Snapshot();
        var victory = baseline.Clone();
        victory.VictoryTriggered = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(victory));
    }

    [Fact]
    public void NonNormalDifficultyChangesTheStateHash()
    {
        var baseline = new Snapshot();
        var hell = baseline.Clone();
        hell.Difficulty = GameDifficulty.Hell;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(hell));
    }

    [Fact]
    public void HeldItemChangesHashWithoutChangingAbsentLegacyState()
    {
        var baseline = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var held = baseline.Clone();
        held.Players[0].HeldItem = new ItemSnapshot {
            ItemSeed = 42,
            Price = 65,
            PurchasedAtTick = 9,
            State = new ItemStateSnapshot {
                ItemType = 11,
                ItemIndex = 0,
                Value = 65,
                IdentifiedValue = 65,
                StatFlag = true,
            },
        };
        var changed = held.Clone();
        changed.Players[0].HeldItem.Price = 64;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(held));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(held),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void StashStateChangesHashAndRetainsLegacyHashWhenAbsent()
    {
        var baseline = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var withStash = baseline.Clone();
        withStash.Players[0].StashGold = 50;
        withStash.Players[0].StashOpen = true;
        withStash.Players[0].Stash.Add(new StashItemSnapshot {
            Page = 2,
            Cell = 11,
            Item = new ItemSnapshot {
                ItemSeed = 42,
                Price = 65,
                State = new ItemStateSnapshot {
                    ItemType = 1,
                    ItemIndex = 7,
                    StableItemId = 700,
                    Value = 65,
                },
            },
        });
        var changed = withStash.Clone();
        changed.Players[0].Stash[0].Cell = 12;

        Assert.Equal(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
            }));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(withStash));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(withStash),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void StableItemIdentityPreservesLegacySnapshotHash()
    {
        var baseline = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    HeldItem = new ItemSnapshot {
                        ItemSeed = 42,
                        State = new ItemStateSnapshot {
                            ItemType = 1,
                            ItemIndex = 7,
                            Value = 75,
                        },
                    },
                },
            },
        };
        var withStableIdentity = baseline.Clone();
        withStableIdentity.Players[0].HeldItem!.State.StableItemId = 700;

        Assert.Equal(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(withStableIdentity));
    }

    [Fact]
    public void ResourceConversionChangesHashOnlyWhenPresent()
    {
        var withoutConversion = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CharacterLevel = 1,
                    HeldItem = new ItemSnapshot {
                        ItemSeed = 42,
                        State = new ItemStateSnapshot { ItemType = 13 },
                    },
                },
            },
        };
        var withConversion = withoutConversion.Clone();
        withConversion.Players[0].HeldItem!.State = new ItemStateSnapshot {
            ItemType = 13,
            ManaToLifePercent = 50,
        };

        Assert.Equal(
            SnapshotStateHasher.Compute(withoutConversion),
            SnapshotStateHasher.Compute(withoutConversion.Clone()));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(withoutConversion),
            SnapshotStateHasher.Compute(withConversion));
    }

    [Fact]
    public void ResourceBaseMaximaChangeHashOnlyWhenTheyDifferFromEffectiveMaxima()
    {
        var baseline = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    ManaMaximum = 640,
                    LifeMaximum = 640,
                },
            },
        };
        var equivalent = baseline.Clone();
        equivalent.Players[0].BaseManaMaximum = 640;
        equivalent.Players[0].BaseLifeMaximum = 640;
        var converted = equivalent.Clone();
        converted.Players[0].ManaMaximum = 320;
        converted.Players[0].LifeMaximum = 960;
        converted.Players[0].BaseManaMaximum = 640;
        converted.Players[0].BaseLifeMaximum = 640;

        Assert.Equal(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(equivalent));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(equivalent),
            SnapshotStateHasher.Compute(converted));
    }

    [Fact]
    public void LightRadiusPreservesDefaultHashAndChangesWhenNonDefault()
    {
        var baseline = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var explicitDefault = baseline.Clone();
        explicitDefault.Players[0].LightRadius = 10;
        var expanded = baseline.Clone();
        expanded.Players[0].LightRadius = 15;

        Assert.Equal(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(explicitDefault));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(expanded));
    }

    [Fact]
    public void NonzeroPlayerClassIdentityChangesTheStateHash()
    {
        var legacyWarrior = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var monk = legacyWarrior.Clone();
        monk.Players[0].CharacterClassId = 3;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(legacyWarrior),
            SnapshotStateHasher.Compute(monk));
        Assert.Equal(
            "76769ad0f438a4b3a3869e37c4a7f72d8b66dc1fdce6902e5fedfc42b17518ca",
            SnapshotStateHasher.Compute(monk));
    }

    [Fact]
    public void CharacterNameChangesTheHashWithoutChangingUnnamedLegacyState()
    {
        var unnamed = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var named = unnamed.Clone();
        named.Players[0].CharacterName = "Dead-RogueDM";
        var renamed = named.Clone();
        renamed.Players[0].CharacterName = "Other-Rogue";

        Assert.Equal(
            SnapshotStateHasher.Compute(unnamed),
            SnapshotStateHasher.Compute(unnamed.Clone()));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(unnamed),
            SnapshotStateHasher.Compute(named));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(named),
            SnapshotStateHasher.Compute(renamed));
    }

    [Fact]
    public void HostileModeChangesTheStateHashWithoutChangingFriendlyLegacyHashes()
    {
        var friendly = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var hostile = friendly.Clone();
        hostile.Players[0].HostileMode = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(friendly),
            SnapshotStateHasher.Compute(hostile));
    }

    [Fact]
    public void NonDefaultPlayerFacingChangesTheCanonicalHash()
    {
        var south = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var east = south.Clone();
        east.Players[0].FacingDirection = PlayerDirection.East;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(south),
            SnapshotStateHasher.Compute(east));
        Assert.Equal(
            SnapshotStateHasher.Compute(south),
            SnapshotStateHasher.Compute(south.Clone()));
    }

    [Fact]
    public void CombatModeChangesTheHashWithoutChangingStandingLegacyHashes()
    {
        var standing = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var blocking = standing.Clone();
        blocking.Players[0].CombatMode = PlayerCombatMode.Block;
        blocking.Players[0].CombatModeEndTick = 9;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(standing),
            SnapshotStateHasher.Compute(blocking));
        Assert.Equal(
            "a5dee363d076a45767c93fc10fc90c0b5c3b1233772c80711b1a79fb78c0a0f1",
            SnapshotStateHasher.Compute(blocking));
    }

    [Fact]
    public void CurrentPlayerActionChangesEveryDeferredLifecycleHashBoundary()
    {
        var baseline = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CharacterLevel = 1,
                    CombatMode = PlayerCombatMode.Spell,
                    CombatModeEndTick = 20,
                },
            },
        };
        var timed = baseline.Clone();
        timed.Players[0].CurrentAction = new PlayerActionSnapshot {
            Kind = PlayerActionKind.Cast,
            StartedAtTick = 2,
            EffectTick = 10,
            ReadyTick = 10,
            EndTick = 20,
            EffectApplied = true,
            Cast = new CastRequested {
                SpellId = 30,
                TargetEntityId = 9,
                TargetX = 3,
                TargetY = 4,
                Source = CastSourceKind.ChargedItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                },
            },
            TargetEntityId = 9,
        };
        var changed = timed.Clone();
        changed.Players[0].CurrentAction.ReadyTick = 11;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(timed));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(timed),
            SnapshotStateHasher.Compute(changed));
        Assert.Equal(
            "c89460e4f3faa810171be7ff6cfe5e81400c0fe970a41f47616c218de4252388",
            SnapshotStateHasher.Compute(timed));
    }

    [Fact]
    public void SpellKnowledgeChangesHashAndIgnoresMemorizedSpellOrder()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CharacterLevel = 1,
                    AbilitySpellId = 27,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot { SpellId = 3, Level = 1 },
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 2 },
                    },
                },
            },
        };
        var reordered = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CharacterLevel = 1,
                    AbilitySpellId = 27,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 2 },
                        new PlayerSpellLevelSnapshot { SpellId = 3, Level = 1 },
                    },
                },
            },
        };
        var changed = first.Clone();
        changed.Players[0].MemorizedSpells[0].Level = 2;

        Assert.Equal(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(reordered));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changed));
        Assert.Equal(
            "55df082b2fffc775c1768adf67ce55dabaa2fa0cd811ce270fb1ddda0fad178e",
            SnapshotStateHasher.Compute(first));
    }

    [Fact]
    public void ItemInventoryFootprintChangesTheStateHash()
    {
        var first = new Snapshot {
            ActiveStore = new StoreSnapshot {
                StoreId = 1,
                Items = { new StoreItemSnapshot { State = new ItemStateSnapshot { InventoryWidth = 1, InventoryHeight = 1 } } },
            },
        };
        var second = first.Clone();
        second.ActiveStore.Items[0].State.InventoryWidth = 2;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void ItemSpellSourceKindChangesTheStateHash()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Equipment = {
                        new EquippedItemSnapshot {
                            Slot = 4,
                            State = new ItemStateSnapshot { SpellId = 7 },
                        },
                    },
                },
            },
        };
        var changed = first.Clone();
        changed.Players[0].Equipment[0].State.SpellSourceKind =
            ItemSpellSourceKind.ChargedItem;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ItemActionKindChangesTheStateHash()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Inventory = {
                        new ItemSnapshot { State = new ItemStateSnapshot { ItemIndex = 27 } },
                    },
                },
            },
        };
        var changed = first.Clone();
        changed.Players[0].Inventory[0].State.ItemActionKind = ItemActionKind.Identify;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void MonsterProjectionChangesTheStateHashAndIgnoresInputOrder()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot { EntityId = 2, MonsterId = 7, HitPoints = 20, MaxHitPoints = 20, Alive = true },
                new MonsterSnapshot { EntityId = 1, MonsterId = 3, HitPoints = 5, MaxHitPoints = 10, Alive = true },
            },
        };
        var second = new Snapshot {
            Monsters = {
                new MonsterSnapshot { EntityId = 1, MonsterId = 3, HitPoints = 5, MaxHitPoints = 10, Alive = true },
                new MonsterSnapshot { EntityId = 2, MonsterId = 7, HitPoints = 20, MaxHitPoints = 20, Alive = true },
            },
        };

        Assert.Equal(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
        second.Monsters[0].HitPoints = 4;
        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void MonsterRuntimeSeedsExtendTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var itemSeed = first.Clone();
        itemSeed.Monsters[0].MonsterItemSeed = 0x12345678;
        itemSeed.Monsters[0].MonsterAiSeed = 0x87654321;
        var aiSeed = itemSeed.Clone();
        aiSeed.Monsters[0].MonsterAiSeed = 0x87654320;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(itemSeed));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(itemSeed),
            SnapshotStateHasher.Compute(aiSeed));
    }

    [Fact]
    public void FractionalMonsterHitPointsExtendHashWithoutChangingLegacyWholeHashes()
    {
        var legacy = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 10,
                    Alive = true,
                },
            },
        };
        var aligned = legacy.Clone();
        aligned.Monsters[0].HitPointsQ6 = 320;
        aligned.Monsters[0].MaxHitPointsQ6 = 640;
        var fractional = aligned.Clone();
        fractional.Monsters[0].HitPointsQ6 = 304;

        Assert.Equal(
            SnapshotStateHasher.Compute(legacy),
            SnapshotStateHasher.Compute(aligned));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(aligned),
            SnapshotStateHasher.Compute(fractional));
    }

    [Fact]
    public void MonsterMovementPetrificationAndStoneCurseImmunityExtendTheStateHash()
    {
        var stationary = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var moving = stationary.Clone();
        moving.Monsters[0].Moving = true;
        moving.Monsters[0].FuturePositionX = 2;
        moving.Monsters[0].FuturePositionY = 1;
        var petrified = moving.Clone();
        petrified.Monsters[0].Petrified = true;
        var immune = petrified.Clone();
        immune.Monsters[0].StoneCurseImmune = true;
        var undead = immune.Clone();
        undead.Monsters[0].IsUndead = true;
        var demon = undead.Clone();
        demon.Monsters[0].IsDemon = true;
        var animal = demon.Clone();
        animal.Monsters[0].IsAnimal = true;
        var knockback = animal.Clone();
        knockback.Monsters[0].HasKnockback = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(stationary),
            SnapshotStateHasher.Compute(moving));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(moving),
            SnapshotStateHasher.Compute(petrified));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(petrified),
            SnapshotStateHasher.Compute(immune));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(immune),
            SnapshotStateHasher.Compute(undead));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(undead),
            SnapshotStateHasher.Compute(demon));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(demon),
            SnapshotStateHasher.Compute(animal));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(animal),
            SnapshotStateHasher.Compute(knockback));
    }

    [Fact]
    public void MonsterGroupUnityStateExtendsTheStateHash()
    {
        var leader = new MonsterSnapshot {
            EntityId = 1,
            HitPoints = 5,
            MaxHitPoints = 5,
            Alive = true,
            PackSize = 1,
        };
        var member = new MonsterSnapshot {
            EntityId = 2,
            HitPoints = 5,
            MaxHitPoints = 5,
            Alive = true,
            LeaderEntityId = 1,
            LeaderRelation = 1,
            ActiveForTicks = 4,
        };
        var first = new Snapshot { Monsters = { leader, member } };
        var changed = first.Clone();
        changed.Monsters[1].ActiveForTicks++;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void MonsterAcidResistanceExtendsTheStateHash()
    {
        var unresistant = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var resistant = unresistant.Clone();
        resistant.Monsters[0].AcidResistance = 100;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(unresistant),
            SnapshotStateHasher.Compute(resistant));
    }

    [Fact]
    public void MonsterDeathAnimationExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 0,
                    MaxHitPoints = 10,
                    Alive = false,
                },
            },
        };
        var duration = first.Clone();
        duration.Monsters[0].DeathAnimationTicks = 12;
        duration.Monsters[0].DeathTicksRemaining = 12;
        var progressed = duration.Clone();
        progressed.Monsters[0].DeathTicksRemaining = 11;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(duration));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(duration),
            SnapshotStateHasher.Compute(progressed));
    }

    [Fact]
    public void MonsterAccuracyExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var normal = first.Clone();
        normal.Monsters[0].ToHit = 61;
        var special = normal.Clone();
        special.Monsters[0].ToHitSpecial = 43;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(normal));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(normal),
            SnapshotStateHasher.Compute(special));
    }

    [Fact]
    public void MonsterNativeAiExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].NativeAi = "SkeletonRanged";

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void MonsterIntelligenceExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].Intelligence = 3;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void GargoyleSpecialLockExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].GargoyleSpecialActive = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void FallenFearStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].FallenFearTicks = 5;
        second.Monsters[0].FallenFearDirection = 7;
        second.Monsters[0].FallenStandAnimationFrame = 4;
        second.Monsters[0].FallenStandAnimationTick = 2;
        second.Monsters[0].FallenRallyTicks = 165;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void SneakStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].SneakHidden = true;
        second.Monsters[0].SneakRetreatTicks = 5;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void CorpseStateExtendsTheStateHash()
    {
        var first = new Snapshot();
        var second = first.Clone();
        second.Corpses.Add(new CorpseSnapshot {
            EntityId = 30,
            LevelId = 0,
            PositionX = 3,
            PositionY = 3,
        });

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void ScavengerHealingStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].ScavengerHealingTicks = 7;
        second.Monsters[0].ScavengerCorpseEntityId = 30;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void BatRetreatStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].BatRetreatTicks = 1;
        second.Monsters[0].BatRetreatDirection = 7;
        second.Monsters[0].BatMovementIdleTicks = 21;
        second.Monsters[0].BatMovementMode = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void BatMovementStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].BatMovementIdleTicks = 21;
        second.Monsters[0].BatMovementMode = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void RangedAvoidanceMoveGoalStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].RangedAvoidanceMoveTicks = 7;
        second.Monsters[0].RangedAvoidanceTurn = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void RangedAvoidanceMovementCounterStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].RangedAvoidanceMovementIdleTicks = 10;
        second.Monsters[0].RangedAvoidanceMovementMode = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void RhinoMoveGoalStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].RhinoMovementTicks = 7;
        second.Monsters[0].RhinoMovementTurn = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void AiAvoidanceMoveGoalStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].AvoidanceMovementTicks = 7;
        second.Monsters[0].AvoidanceMovementTurn = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void AiAvoidanceMovementCounterStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].AvoidanceMovementIdleTicks = 10;
        second.Monsters[0].AvoidanceMovementMode = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void SkeletonMeleeDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].SkeletonMeleeDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void SkeletonRangedMovementCounterStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].SkeletonRangedMovementIdleTicks = 10;
        second.Monsters[0].SkeletonRangedMovementMode = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void FatMovementCounterStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].FatMovementIdleTicks = 10;
        second.Monsters[0].FatMovementMode = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void RhinoMovementAndDelayStateExtendTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].RhinoMovementIdleTicks = 10;
        second.Monsters[0].RhinoMovementMode = 1;
        second.Monsters[0].RhinoDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void SkeletonKingDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].SkeletonKingDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void HorkDemonDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].HorkDemonDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void RangedAvoidanceDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].RangedAvoidanceDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void SnakeDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].SnakeDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void CounselorDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].CounselorDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void AvoidanceDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].AvoidanceDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void RangedDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].RangedDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void MegaDelayStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].MegaDelayTicks = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void CounselorRetreatStateExtendsTheStateHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    Alive = true,
                },
            },
        };
        var second = first.Clone();
        second.Monsters[0].CounselorRetreatTicks = 3;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void WorldItemProjectionChangesTheStateHash()
    {
        var first = new Snapshot {
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 20,
                    LevelId = 1,
                    PositionX = 2,
                    PositionY = 3,
                    ItemSeed = 42,
                    Price = 75,
                    State = new ItemStateSnapshot { ItemType = 1 },
                },
            },
        };
        var second = first.Clone();
        second.WorldItems[0].PositionX = 4;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void ManaShieldChangesHashWithoutChangingInactiveLegacyState()
    {
        var inactive = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var active = inactive.Clone();
        active.Players[0].ManaShieldActive = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(inactive),
            SnapshotStateHasher.Compute(active));
        Assert.Equal(
            SnapshotStateHasher.Compute(inactive),
            SnapshotStateHasher.Compute(inactive.Clone()));
    }

    [Fact]
    public void ReflectChargesChangeHashWithoutChangingInactiveLegacyState()
    {
        var inactive = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, CharacterLevel = 1 } },
        };
        var active = inactive.Clone();
        active.Players[0].ReflectCharges = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(inactive),
            SnapshotStateHasher.Compute(active));
        Assert.Equal(
            SnapshotStateHasher.Compute(inactive),
            SnapshotStateHasher.Compute(inactive.Clone()));
    }

    [Fact]
    public void TypedStatusBehaviorChangesHashWithoutChangingGenericStatusShape()
    {
        var generic = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CharacterLevel = 1,
                    StatusEffects = {
                        new StatusEffectSnapshot {
                            EffectId = 9,
                            RemainingTicks = 1584,
                        },
                    },
                },
            },
        };
        var typed = generic.Clone();
        typed.Players[0].StatusEffects[0].Behavior = StatusEffectBehavior.Infravision;

        Assert.Equal(
            SnapshotStateHasher.Compute(generic),
            SnapshotStateHasher.Compute(generic.Clone()));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(generic),
            SnapshotStateHasher.Compute(typed));
    }

    [Fact]
    public void DynamicEarPresentationChangesEveryNewItemHashBoundary()
    {
        var baseline = new Snapshot {
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 20,
                    ItemSeed = 1633955154,
                    State = new ItemStateSnapshot {
                        CreateInfo = 17509,
                        ItemIndex = 23,
                        MiscId = 44,
                    },
                },
            },
        };
        var ear = baseline.Clone();
        ear.WorldItems[0].State.BaseName = "Ear of Dead-RogueDM";
        ear.WorldItems[0].State.IdentifiedName = "Dead-RogueDM";
        ear.WorldItems[0].State.NativeCursorGraphic = 21;
        var renamed = ear.Clone();
        renamed.WorldItems[0].State.IdentifiedName = "Other-Rogue";
        var recursored = ear.Clone();
        recursored.WorldItems[0].State.NativeCursorGraphic = 20;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(ear));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(ear),
            SnapshotStateHasher.Compute(renamed));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(ear),
            SnapshotStateHasher.Compute(recursored));
    }

    [Fact]
    public void ProjectileProjectionChangesTheStateHashAndIgnoresInputOrder()
    {
        var first = new Snapshot {
            Projectiles = {
                new ProjectileSnapshot { EntityId = 2, SourceEntityId = 1, SpellId = 4, Damage = 6, RemainingTicks = 2 },
                new ProjectileSnapshot { EntityId = 1, SourceEntityId = 1, SpellId = 4, Damage = 6, RemainingTicks = 1 },
            },
        };
        var second = new Snapshot {
            Projectiles = {
                new ProjectileSnapshot { EntityId = 1, SourceEntityId = 1, SpellId = 4, Damage = 6, RemainingTicks = 1 },
                new ProjectileSnapshot { EntityId = 2, SourceEntityId = 1, SpellId = 4, Damage = 6, RemainingTicks = 2 },
            },
        };

        Assert.Equal(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
        second.Projectiles[0].PositionX = 3;
        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void GameplayRngAndProjectileRuntimeStateChangeTheHash()
    {
        var first = new Snapshot {
            RandomState = 1,
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 1,
                    SpellId = 30,
                    RemainingTicks = 256,
                    Behavior = (int)AuthoritativeProjectileBehavior.ChargedBolt,
                    PathIndex = 4,
                    Speed = 8,
                    ImpactLifetime = 8,
                    ResistantDamageDivisor = 4,
                },
            },
        };
        var changedRng = first.Clone();
        changedRng.RandomState = 2;
        var changedPath = first.Clone();
        changedPath.Projectiles[0].PathIndex = 5;
        var changedSpellLevel = first.Clone();
        changedSpellLevel.Projectiles[0].EffectiveSpellLevel = 3;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changedRng));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changedPath));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changedSpellLevel));
    }

    [Fact]
    public void MonsterArrowRuntimeStateChangesTheHash()
    {
        var first = new Snapshot {
            Monsters = {
                new MonsterSnapshot { EntityId = 9, Alive = true },
            },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 9,
                    TargetEntityId = 7,
                    RemainingTicks = 5,
                    Behavior = 59,
                    Damage = 2,
                    DamageType = 0,
                    Speed = 1,
                    ImpactLifetime = 1,
                    ResistantDamageDivisor = 1,
                },
            },
        };
        var changedDamage = first.Clone();
        changedDamage.Projectiles[0].Damage = 3;
        var changedTarget = first.Clone();
        changedTarget.Projectiles[0].TargetEntityId = 8;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changedDamage));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(first),
            SnapshotStateHasher.Compute(changedTarget));
    }

    [Fact]
    public void ObjectEffectProjectionChangesTheStateHash()
    {
        var first = new Snapshot {
            Objects = { new ObjectSnapshot { EntityId = 20, ObjectId = 4, Activated = true } },
        };
        var second = first.Clone();
        second.Objects[0].EffectKind = (int)AuthoritativeObjectEffectKind.Heal;
        second.Objects[0].EffectAmount = 10;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void ObjectAndProjectileCollisionStateChangeTheHash()
    {
        var baseline = new Snapshot {
            Objects = { new ObjectSnapshot { EntityId = 20, ObjectId = 4 } },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 30,
                    SourceEntityId = 7,
                    SpellId = 30,
                    RemainingTicks = 8,
                },
            },
        };
        var changedObject = baseline.Clone();
        changedObject.Objects[0].Breakable = true;
        changedObject.Objects[0].Destroyed = true;
        var changedTrap = baseline.Clone();
        changedTrap.Objects[0].TrapArmed = true;
        var changedProjectile = baseline.Clone();
        changedProjectile.Projectiles[0].LastCollisionTargetEntityId = 9;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changedObject));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changedTrap));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changedProjectile));
    }

    [Fact]
    public void ObjectPresentationLifecycleChangesTheHash()
    {
        var baseline = new Snapshot {
            Objects = { new ObjectSnapshot { EntityId = 20, ObjectId = 4, Activated = true } },
        };
        var retained = baseline.Clone();
        retained.Objects[0].KeepVisibleAfterActivation = true;
        retained.Objects[0].AnimationFrame = 3;
        retained.Objects[0].ActivatedAnimationFrame = 7;
        var changedFrame = retained.Clone();
        changedFrame.Objects[0].ActivatedAnimationFrame = 8;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(retained));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(retained),
            SnapshotStateHasher.Compute(changedFrame));
    }

    [Fact]
    public void ObjectDialogueIdentityChangesTheHash()
    {
        var baseline = new Snapshot {
            Objects = { new ObjectSnapshot { EntityId = 20, ObjectId = 4 } },
        };
        var dialogue = baseline.Clone();
        dialogue.Objects[0].DialogueMessageId = 412;
        var changedDialogue = dialogue.Clone();
        changedDialogue.Objects[0].DialogueMessageId = 413;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(dialogue));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(dialogue),
            SnapshotStateHasher.Compute(changedDialogue));
    }

    [Fact]
    public void QuestPoolAvailabilityChangesTheHash()
    {
        var available = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3001, RequiredProgress = 1 },
            },
        };
        var unavailable = available.Clone();
        unavailable.Quests[0].Unavailable = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(available),
            SnapshotStateHasher.Compute(unavailable));
    }

    [Fact]
    public void QuestLifecycleStateChangesTheHash()
    {
        var initial = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3001, RequiredProgress = 1, Initial = true },
            },
        };
        var logged = initial.Clone();
        logged.Quests[0].Logged = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(initial),
            SnapshotStateHasher.Compute(logged));
    }

    [Fact]
    public void NativeQuestVariablesChangeTheHash()
    {
        var baseline = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3001, RequiredProgress = 1 },
            },
        };
        var changed = baseline.Clone();
        changed.Quests[0].Variable1 = 2;
        changed.Quests[0].Variable2 = 7;
        changed.Quests[0].MessageId = 123;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void QuestMonsterCompletionTriggerChangesTheHash()
    {
        var baseline = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3001, RequiredProgress = 1 },
            },
        };
        var changed = baseline.Clone();
        changed.Quests[0].CompletionMonsterId = 12;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ContentOwnedQuestTurnInTownerChangesTheHash()
    {
        var baseline = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3011, RequiredProgress = 1 },
            },
        };
        var changed = baseline.Clone();
        changed.Quests[0].TurnInTownerId = 0;

        Assert.True(changed.Quests[0].HasTurnInTownerId);
        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void CompletionStageQuestTurnInChangesTheHash()
    {
        var baseline = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3018, RequiredProgress = 1, Completed = true },
            },
        };
        var changed = baseline.Clone();
        changed.Quests[0].TurnInOnCompletion = true;
        changed.Quests[0].TurnInCompletionVariable2 = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void AlternateItemQuestTurnInChangesTheHash()
    {
        var baseline = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3024, RequiredProgress = 1 },
            },
        };
        var changed = baseline.Clone();
        changed.Quests[0].TurnInAlternateItemId = 2105;
        changed.Quests[0].TurnInAlternateMessageId = 303;

        Assert.True(changed.Quests[0].HasTurnInAlternateItemId);
        Assert.True(changed.Quests[0].HasTurnInAlternateMessageId);
        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ActivationOnlyQuestTurnInChangesTheHash()
    {
        var baseline = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 3016, RequiredProgress = 1 },
            },
        };
        var changed = baseline.Clone();
        changed.Quests[0].TurnInActivationOnly = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ObjectQuestProgressAmountChangesTheHash()
    {
        var baseline = new Snapshot {
            Objects = {
                new ObjectSnapshot { EntityId = 20, ObjectId = 4 },
            },
        };
        var changed = baseline.Clone();
        changed.Objects[0].QuestProgressAmount = 3;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ObjectProjectileTargetOffsetsChangeTheHash()
    {
        var baseline = new Snapshot {
            Objects = {
                new ObjectSnapshot { EntityId = 20, ObjectId = 4 },
            },
        };
        var changed = baseline.Clone();
        changed.Objects[0].ProjectileTargetOffsetX = 1;
        changed.Objects[0].ProjectileTargetOffsetY = -17;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ObjectProjectileSourceKindChangesTheHash()
    {
        var baseline = new Snapshot {
            Objects = {
                new ObjectSnapshot { EntityId = 20, ObjectId = 4 },
            },
        };
        var changed = baseline.Clone();
        changed.Objects[0].ProjectileSourceKind = 1;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ObjectQuestSequenceFollowUpChangesTheHash()
    {
        var baseline = new Snapshot {
            Objects = {
                new ObjectSnapshot { EntityId = 20, ObjectId = 4 },
            },
        };
        var changed = baseline.Clone();
        changed.Objects[0].QuestSequenceFollowUpKind = 1;
        changed.Objects[0].QuestSequenceFollowUpAmount = 7;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ObjectShrineCapabilityChangesTheHash()
    {
        var baseline = new Snapshot {
            Objects = {
                new ObjectSnapshot { EntityId = 20, ObjectId = 4 },
            },
        };
        var changed = baseline.Clone();
        changed.Objects[0].IsShrine = true;

        Assert.NotEqual(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void StaticCatalogPortalsDoNotChangeLegacyHashButDynamicPortalsDo()
    {
        var baseline = new Snapshot();
        var staticRoute = baseline.Clone();
        staticRoute.Portals.Add(new PortalSnapshot {
            PortalId = 1,
            SourceLevelId = 1,
            SourcePositionX = 2,
            SourcePositionY = 3,
            DestinationLevelId = 2,
            DestinationPositionX = 4,
            DestinationPositionY = 5,
        });
        var dynamicRoute = staticRoute.Clone();
        dynamicRoute.Portals[0].OwnerEntityId = 7;
        dynamicRoute.Portals[0].RemainingTicks = 100;
        dynamicRoute.Portals[0].Bidirectional = true;

        Assert.Equal(
            SnapshotStateHasher.Compute(baseline),
            SnapshotStateHasher.Compute(staticRoute));
        Assert.NotEqual(
            SnapshotStateHasher.Compute(staticRoute),
            SnapshotStateHasher.Compute(dynamicRoute));
    }
}
