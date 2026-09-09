using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeInteractionVisibilityTests
{
    [Fact]
    public void HidesQuestTownerInteractionOutsideProjectedNativeVision()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 2 } },
            Quests = {
                new QuestSnapshot { QuestId = 3001, LevelId = 2, Initial = true },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 1,
                    QuestId = 3001,
                    TownerId = 4,
                    LevelId = 2,
                    PositionX = 4,
                    MessageId = 1,
                    Kind = 0,
                },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 },
            },
        };
        snapshot.LevelGeometry[0].NativeVisionBlockedCells.Add(1);

        Assert.Null(AuthoritativeQuestTownerInteractionSelector.Select(snapshot));
        Assert.Null(AuthoritativeQuestTownerInteractionSelector.SelectAt(snapshot, 4, 0));
    }

    [Fact]
    public void HidesTownerGreetingOutsideProjectedNativeVision()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 2 } },
            TownerGreetings = {
                new TownerGreetingSnapshot {
                    TownerId = 4,
                    LevelId = 2,
                    PositionX = 4,
                    MessageId = 1,
                },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 },
            },
        };
        snapshot.LevelGeometry[0].NativeVisionBlockedCells.Add(1);

        Assert.Null(AuthoritativeTownerGreetingSelector.SelectAt(snapshot, 4, 0));
    }
}
