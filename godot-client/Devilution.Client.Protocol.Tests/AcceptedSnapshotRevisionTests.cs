using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptedSnapshotRevisionTests
{
    [Fact]
    public void AdvancesOnlyForAdmittedSnapshotsAndAcceptsRewindAfterReset()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(10, 3)));
        Assert.Equal(1UL, model.AcceptedSnapshotRevision);

        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(9, 4)));
        Assert.Equal(1UL, model.AcceptedSnapshotRevision);
        Assert.Equal(10UL, model.CurrentTick);

        model.ResetTransientPresentation();
        Assert.Equal(1UL, model.AcceptedSnapshotRevision);
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(9, 5)));
        Assert.Equal(2UL, model.AcceptedSnapshotRevision);
        Assert.Equal(9UL, model.CurrentTick);
        Assert.Equal(5U, model.Snapshot!.LevelGeometry.Single().LevelId);
    }

    [Fact]
    public void SameTickReplacementAndEmptyGeometryAdvanceTheGeneration()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(20, 7)));
        var firstRevision = model.AcceptedSnapshotRevision;

        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(20, 8)));
        Assert.Equal(firstRevision + 1, model.AcceptedSnapshotRevision);
        Assert.Equal(8U, model.Snapshot!.LevelGeometry.Single().LevelId);

        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 21,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));
        Assert.Equal(firstRevision + 2, model.AcceptedSnapshotRevision);
        Assert.Empty(model.Snapshot!.LevelGeometry);
    }

    [Fact]
    public void PublishesRevisionBeforeMalformedTrailingAcknowledgementThrows()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(30, 9)));
        var next = Snapshot(31, 10);

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: next,
            Acknowledgement: new CommandAck {
                Results = { new CommandResult { Status = CommandStatus.Unspecified } },
            })));

        Assert.Equal(2UL, model.AcceptedSnapshotRevision);
        Assert.Equal(31UL, model.CurrentTick);
        Assert.Equal(10U, model.Snapshot!.LevelGeometry.Single().LevelId);
    }

    [Fact]
    public void PublishesRevisionBeforeMalformedTrailingEventsThrow()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(32, 13)));
        var next = Snapshot(33, 14);

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: next,
            Events: new EventBatch { Events = { new GameEvent() } })));

        Assert.Equal(2UL, model.AcceptedSnapshotRevision);
        Assert.Equal(33UL, model.CurrentTick);
        Assert.Equal(14U, model.Snapshot!.LevelGeometry.Single().LevelId);
    }

    [Fact]
    public void NonSnapshotMessagesAndPredictionDoNotAdvanceTheGeneration()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(34, 15)));
        var revision = model.AcceptedSnapshotRevision;

        model.TrackPredictedMove(1, 1, 0);
        model.Apply(new AuthoritativeClientMessage(Events: new EventBatch {
            Tick = 34,
            Events = { new GameEvent { Experience = new ExperienceEvent { PlayerEntityId = 7 } } },
        }));
        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = { new CommandResult { ClientSequence = 1, Status = CommandStatus.Accepted } },
        }));

        Assert.Equal(revision, model.AcceptedSnapshotRevision);
    }

    [Fact]
    public void ValidStaleSnapshotWithAcknowledgementKeepsRevisionAndAppliesReceipt()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(35, 16)));
        var revision = model.AcceptedSnapshotRevision;

        model.Apply(new AuthoritativeClientMessage(
            Snapshot: Snapshot(34, 17),
            Acknowledgement: new CommandAck {
                Results = { new CommandResult { ClientSequence = 2, Status = CommandStatus.Accepted } },
            }));

        Assert.Equal(revision, model.AcceptedSnapshotRevision);
        Assert.Equal(35UL, model.CurrentTick);
        Assert.Equal(2UL, model.LastCommandResult!.ClientSequence);
    }

    [Fact]
    public void InvalidSnapshotDoesNotAdvanceTheGeneration()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot(40, 11)));

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Tick = 41,
                StateSha256 = "not-a-canonical-hash",
                Players = { new PlayerSnapshot { EntityId = 7 } },
            })));

        Assert.Equal(1UL, model.AcceptedSnapshotRevision);
        Assert.Equal(40UL, model.CurrentTick);
    }

    [Fact]
    public void AdmittedSnapshotOwnsGeometryAfterInputMutation()
    {
        var model = new AuthoritativeClientModel();
        var incoming = Snapshot(50, 12);

        model.Apply(new AuthoritativeClientMessage(Snapshot: incoming));
        incoming.LevelGeometry[0].Width = 1;
        incoming.LevelGeometry[0].BlockedCells.Add(0);

        Assert.Equal(8, model.Snapshot!.LevelGeometry.Single().Width);
        Assert.Empty(model.Snapshot.LevelGeometry.Single().BlockedCells);
    }

    [Fact]
    public void AdmittedSnapshotOwnsNativeLayersAndWarpTriggersAfterInputMutation()
    {
        var model = new AuthoritativeClientModel();
        var incoming = new Snapshot {
            Tick = 51,
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 12 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 12,
                    Width = 2,
                    Height = 2,
                    NativeTransparency = { 1, 2, 3, 4 },
                    NativePieceLayer = { 5, 6, 7, 8 },
                    NativeObjectLayer = { 9, 10, 11, 12 },
                    WarpTriggers = {
                        new WarpTriggerSnapshot {
                            WarpId = 1,
                            TargetLevelId = 13,
                            TriggerX = 0,
                            TriggerY = 0,
                            TargetX = 1,
                            TargetY = 1,
                            Available = true,
                            StableSymbol = "A",
                        },
                    },
                },
                new LevelGeometrySnapshot { LevelId = 13, Width = 2, Height = 2 },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: incoming));
        incoming.LevelGeometry[0].NativeTransparency[0] = 99;
        incoming.LevelGeometry[0].NativePieceLayer[0] = 98;
        incoming.LevelGeometry[0].NativeObjectLayer[0] = 97;
        incoming.LevelGeometry[0].WarpTriggers[0].Available = false;
        incoming.LevelGeometry[0].WarpTriggers[0].StableSymbol = "B";

        var admitted = model.Snapshot!.LevelGeometry.Single(level => level.LevelId == 12);
        Assert.Equal(1U, admitted.NativeTransparency[0]);
        Assert.Equal(5U, admitted.NativePieceLayer[0]);
        Assert.Equal(9U, admitted.NativeObjectLayer[0]);
        Assert.True(admitted.WarpTriggers[0].Available);
        Assert.Equal("A", admitted.WarpTriggers[0].StableSymbol);
    }

    private static Snapshot Snapshot(ulong tick, uint levelId)
    {
        return new Snapshot {
            Tick = tick,
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = levelId } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = levelId,
                    Width = 8,
                    Height = 8,
                },
            },
        };
    }
}
