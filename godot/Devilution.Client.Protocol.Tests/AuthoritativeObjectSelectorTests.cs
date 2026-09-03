using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeObjectSelectorTests
{
    [Fact]
    public void IgnoresPresentationOnlyObjects()
    {
        var snapshot = SnapshotWithObject(new ObjectSnapshot {
            EntityId = 21,
            ObjectId = 4,
        });

        Assert.Null(AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4));
    }

    [Fact]
    public void SelectFirstIgnoresPresentationOnlyAndOffLevelObjects()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 9 } },
            Objects = {
                new ObjectSnapshot { EntityId = 10, LevelId = 9, EffectKind = 1 },
                new ObjectSnapshot { EntityId = 11, LevelId = 9, EffectKind = 0 },
                new ObjectSnapshot { EntityId = 12, LevelId = 8, EffectKind = 1 },
                new ObjectSnapshot { EntityId = 13, LevelId = 9, EffectKind = 1, Activated = true },
                new ObjectSnapshot { EntityId = 14, LevelId = 9, EffectKind = 1 },
            },
        };

        Assert.Equal(10U, AuthoritativeObjectSelector.SelectFirst(snapshot)?.EntityId);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0, false)]
    [InlineData(0, 7u, 0, 0, 0, false)]
    [InlineData(0, 0, 2, 0, 0, false)]
    [InlineData(0, 0, 0, 1, 0, false)]
    [InlineData(0, 0, 0, 0, 1, false)]
    [InlineData(0, 0, 0, 0, 0, true)]
    public void SelectsEveryAuthoredInteractionKind(
        int effectKind,
        uint questId,
        int transitionKind,
        int rewardKind,
        int followUpKind,
        bool trapArmed)
    {
        var snapshot = SnapshotWithObject(new ObjectSnapshot {
            EntityId = 21,
            EffectKind = effectKind,
            QuestId = questId,
            QuestTransitionKind = transitionKind,
            RewardKind = rewardKind,
            QuestSequenceFollowUpKind = followUpKind,
            TrapArmed = trapArmed,
        });

        Assert.Equal(21U, AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4)?.EntityId);
    }

    [Fact]
    public void SelectsDialogueOnlyObjects()
    {
        var snapshot = SnapshotWithObject(new ObjectSnapshot {
            EntityId = 21,
            DialogueMessageId = 412,
        });

        Assert.Equal(21U, AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4)?.EntityId);
    }

    [Fact]
    public void RejectsInactiveOrMisplacedObjects()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 9 } },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 11,
                    LevelId = 9,
                    PositionX = 3,
                    PositionY = 4,
                    EffectKind = 1,
                    Activated = true,
                },
                new ObjectSnapshot {
                    EntityId = 12,
                    LevelId = 8,
                    PositionX = 3,
                    PositionY = 4,
                    EffectKind = 1,
                },
                new ObjectSnapshot {
                    EntityId = 13,
                    LevelId = 9,
                    PositionX = 5,
                    PositionY = 4,
                    EffectKind = 1,
                },
            },
        };

        Assert.Null(AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4));
    }

    [Fact]
    public void TreatsZeroLevelAsVisibleCompatibilityWildcard()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 9 } },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 21,
                    LevelId = 0,
                    PositionX = 3,
                    PositionY = 4,
                    EffectKind = 1,
                },
            },
        };

        Assert.Equal(21U, AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4)?.EntityId);
    }

    private static Snapshot SnapshotWithObject(ObjectSnapshot @object)
    {
        @object.LevelId = 9;
        @object.PositionX = 3;
        @object.PositionY = 4;
        return new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 9 } },
            Objects = { @object },
        };
    }
}
