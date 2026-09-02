using Devilution.Protocol.V1;
using Devilution.Protocol;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSnapshotValidatorTests
{
    [Fact]
    public void AcceptsLegacyOmittedAndCanonicalStateHashes()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot());
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            StateSha256 = new string('a', AuthoritativeSnapshotValidator.Sha256HexLength),
        });
    }

    [Fact]
    public void AcceptsDistinctEntityIdsAcrossProjectedCollections()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1 } },
            Monsters = { new MonsterSnapshot { EntityId = 2 } },
            WorldItems = { new WorldItemSnapshot { EntityId = 3 } },
            Objects = { new ObjectSnapshot { EntityId = 4 } },
            Projectiles = { new ProjectileSnapshot { EntityId = 5 } },
            Corpses = { new CorpseSnapshot { EntityId = 6 } },
        };

        AuthoritativeSnapshotValidator.Validate(snapshot);
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativePlayerCapacity()
    {
        var snapshot = new Snapshot();
        for (var index = 1; index <= 5; index++)
            snapshot.Players.Add(new PlayerSnapshot { EntityId = (uint)index });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsDefinedDifficultyAndValidPortalState()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Difficulty = GameDifficulty.Normal,
            Portals = {
                new PortalSnapshot {
                    PortalId = 1,
                    Bidirectional = true,
                    OwnerEntityId = 2,
                    RemainingTicks = 3,
                },
                new PortalSnapshot { PortalId = 4 },
            },
            ActiveWorldMutationIds = { 5, 6 },
        });
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativePortalCapacity()
    {
        var snapshot = new Snapshot();
        for (var index = 1; index <= 5; index++)
            snapshot.Portals.Add(new PortalSnapshot { PortalId = (uint)index });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativeMonsterCapacityPerLevel()
    {
        var snapshot = new Snapshot();
        for (var index = 1; index <= 201; index++)
            snapshot.Monsters.Add(new MonsterSnapshot {
                EntityId = (uint)index,
                LevelId = 1,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativeObjectAndItemCapacityPerLevel()
    {
        var objectSnapshot = new Snapshot();
        for (var index = 1; index <= 128; index++)
            objectSnapshot.Objects.Add(new ObjectSnapshot {
                EntityId = (uint)index,
                LevelId = 1,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(objectSnapshot));

        var itemSnapshot = new Snapshot();
        for (var index = 1; index <= 128; index++)
            itemSnapshot.WorldItems.Add(new WorldItemSnapshot {
                EntityId = (uint)index,
                LevelId = 1,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(itemSnapshot));
    }

    [Fact]
    public void AcceptsOmittedAndPrintablePlayerNames()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 1 },
                new PlayerSnapshot { EntityId = 2, CharacterName = "Godevilution" },
            },
        });
    }

    [Fact]
    public void RejectsMalformedNonEmptyStateHashes()
    {
        foreach (var hash in new[] {
            "not-a-hash",
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength - 1),
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength + 1),
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength - 1) + "g",
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot { StateSha256 = hash }));
    }

    [Fact]
    public void RejectsZeroOrDuplicateEntityIds()
    {
        var zero = new Snapshot {
            Players = { new PlayerSnapshot() },
        };
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(zero));

        var duplicate = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot { EntityId = 7 } },
        };
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(duplicate));
    }

    [Fact]
    public void RejectsInvalidSnapshotMetadata()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Difficulty = (GameDifficulty)99,
            }));

        var tooManyMutations = new Snapshot();
        tooManyMutations.ActiveWorldMutationIds.AddRange(
            Enumerable.Range(1, 4097).Select(value => (uint)value));

        foreach (var snapshot in new[] {
            new Snapshot { Portals = { new PortalSnapshot() } },
            new Snapshot {
                Portals = {
                    new PortalSnapshot { PortalId = 1 },
                    new PortalSnapshot { PortalId = 1 },
                },
            },
            new Snapshot {
                Portals = {
                    new PortalSnapshot { PortalId = 1, Bidirectional = true },
                },
            },
            new Snapshot {
                Portals = {
                    new PortalSnapshot { PortalId = 1, OwnerEntityId = 2 },
                },
            },
            new Snapshot { ActiveWorldMutationIds = { 0 } },
            new Snapshot { ActiveWorldMutationIds = { 7, 7 } },
            tooManyMutations,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsInvalidPlayerNameShapes()
    {
        foreach (var name in new[] {
            new string('A', 32),
            "bad\nname",
            "bad\0name",
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot {
                    Players = { new PlayerSnapshot { EntityId = 1, CharacterName = name } },
                }));
    }

    [Fact]
    public void RejectsDiabloKillLevelsOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        DiabloKillLevel = byte.MaxValue + 1U,
                    },
                },
            }));
    }

    [Fact]
    public void RejectsPlayerLevelsOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        LevelId = 25,
                    },
                },
            }));
    }

    [Fact]
    public void RejectsDuplicateVisitedLevels()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        LevelId = 4,
                        VisitedLevelIds = { 0, 4, 4 },
                    },
                },
            }));
    }

    [Fact]
    public void RejectsUndefinedPlayerEnums()
    {
        foreach (var player in new[] {
            new PlayerSnapshot { EntityId = 1, FacingDirection = (PlayerDirection)99 },
            new PlayerSnapshot { EntityId = 1, CombatMode = (PlayerCombatMode)99 },
            new PlayerSnapshot { EntityId = 1, AbilityKind = (PlayerAbilityKind)99 },
            new PlayerSnapshot { EntityId = 1, AbilityTargetingKind = (SpellTargetingKind)99 },
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot { Players = { player } }));
    }

    [Fact]
    public void AcceptsMovementTimingOnlyForAnActiveMovingMonster()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    Moving = true,
                    MovementStartedTick = 12,
                    MovementEffectTick = 13,
                },
            },
        });
    }

    [Fact]
    public void AcceptsMaterializedSetLevelPlayerIdentityWithoutRegularVisitedFlag()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    LevelId = AuthoritativeLevelIdRules.RuntimeSetLevelIdBase + 1,
                },
            },
        });
    }

    [Fact]
    public void RejectsMaterializedSetLevelIdentityInRegularVisitedFlags()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        LevelId = 4,
                        VisitedLevelIds = {
                            AuthoritativeLevelIdRules.RuntimeSetLevelIdBase,
                        },
                    },
                },
            }));
    }

    [Fact]
    public void RejectsInvalidMonsterMovementTiming()
    {
        foreach (var monster in new[] {
            new MonsterSnapshot {
                EntityId = 1,
                Moving = true,
                MovementStartedTick = 12,
                MovementEffectTick = 11,
            },
            new MonsterSnapshot {
                EntityId = 1,
                MovementEffectTick = 13,
            },
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot {
                    Monsters = { monster },
                }));
    }
}
