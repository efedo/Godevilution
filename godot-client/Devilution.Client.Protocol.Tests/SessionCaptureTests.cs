using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class SessionCaptureTests
{
    [Fact]
    public void CapturedFramesRoundTripAndCanDriveTheModel()
    {
        var capture = new AuthoritativeSessionCapture();
        capture.Record(new Envelope {
            Snapshot = new Snapshot {
                Tick = 4,
                Players = { new PlayerSnapshot { EntityId = 7, PositionX = 2 } },
            },
        });
        capture.Record(new Envelope {
            EventBatch = new EventBatch {
                Tick = 4,
                Events = { new GameEvent { Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = 10 } } },
            },
        });

        using var stream = new MemoryStream();
        capture.Write(stream);
        stream.Position = 0;
        var replay = AuthoritativeSessionCapture.Read(stream);
        var model = new AuthoritativeClientModel();
        foreach (var envelope in replay.Replay())
            model.Apply(AuthoritativeClientMessage.FromEnvelope(envelope));

        Assert.Equal(2, replay.Frames.Count);
        Assert.Equal(4UL, model.CurrentTick);
        Assert.Equal(2, (int)model.Snapshot!.Players[0].PositionX);
        Assert.Equal(10U, model.RecentEvents.Single().Experience.Amount);
    }

    [Fact]
    public void PredictionIsRemovedAfterAcknowledgementAndCorrection()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 5,
            Players = { new PlayerSnapshot { EntityId = 7, PositionX = 0, PositionY = 0 } },
        }));

        model.TrackPredictedMove(12, 0, 1);
        Assert.Equal(new ClientPosition(0, 1), model.PredictedPlayerPosition);
        Assert.True(model.IsCorrectingPosition);

        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = { new CommandResult { ClientSequence = 12, Status = CommandStatus.Accepted } },
        }));
        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
    }
}
