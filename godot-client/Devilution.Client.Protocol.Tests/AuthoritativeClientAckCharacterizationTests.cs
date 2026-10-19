using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientAckCharacterizationTests
{
    private static readonly CommandStatus UnknownStatus = (CommandStatus)99;
    private static readonly CommandRejectReason UnknownRejectReason = (CommandRejectReason)99;

    [Theory]
    [MemberData(nameof(SuccessfulReceiptCases))]
    public void IsAcceptedOrSuccessfulDuplicateDistinguishesAcceptedAndSuccessfulDuplicateReceipts(
        bool expected,
        CommandStatus? status,
        CommandRejectReason rejectReason)
    {
        CommandResult? result = status is null
            ? null
            : new CommandResult {
                Status = status.Value,
                RejectReason = rejectReason,
            };

        Assert.Equal(expected, AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(result));
    }

    public static IEnumerable<object?[]> SuccessfulReceiptCases()
    {
        yield return new object?[] { false, null, CommandRejectReason.Unspecified };
        yield return new object?[] { false, CommandStatus.Unspecified, CommandRejectReason.Unspecified };
        yield return new object?[] { false, CommandStatus.Rejected, CommandRejectReason.Unspecified };
        yield return new object?[] { false, CommandStatus.Rescheduled, CommandRejectReason.Unspecified };
        yield return new object?[] { false, CommandStatus.Duplicate, CommandRejectReason.NotAllowed };
        yield return new object?[] { false, CommandStatus.Duplicate, CommandRejectReason.TooLate };
        yield return new object?[] { false, CommandStatus.Accepted, CommandRejectReason.InvalidTarget };
        yield return new object?[] { false, CommandStatus.Duplicate, UnknownRejectReason };
        yield return new object?[] { false, UnknownStatus, CommandRejectReason.Unspecified };
        yield return new object?[] { true, CommandStatus.Accepted, CommandRejectReason.Unspecified };
        yield return new object?[] { true, CommandStatus.Duplicate, CommandRejectReason.Unspecified };
    }

    [Fact]
    public void SuccessfulDuplicateUsesTheSameAcceptanceGuardButIsNotAccepted()
    {
        var result = new CommandResult {
            ClientSequence = 8,
            Status = CommandStatus.Duplicate,
            RejectReason = CommandRejectReason.Unspecified,
        };

        Assert.True(AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(result));
        Assert.NotEqual(CommandStatus.Accepted, result.Status);
    }

    [Fact]
    public void NewerCommandResultSurvivesAnOlderAcknowledgement()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = {
                new CommandResult {
                    ClientSequence = 8,
                    AppliedTick = 80,
                    Status = CommandStatus.Accepted,
                },
            },
        }));

        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = {
                new CommandResult {
                    ClientSequence = 7,
                    AppliedTick = 70,
                    Status = CommandStatus.Duplicate,
                },
            },
        }));

        Assert.NotNull(model.LastCommandResult);
        Assert.Equal(8UL, model.LastCommandResult!.ClientSequence);
        Assert.Equal(CommandStatus.Accepted, model.LastCommandResult.Status);
        Assert.Equal(80UL, model.LastCommandResult.AppliedTick);
    }

    [Fact]
    public void SameSequenceDuplicateReplacesAnEarlierAcceptedReceipt()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 100,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));
        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = {
                new CommandResult {
                    ClientSequence = 8,
                    AppliedTick = 80,
                    Status = CommandStatus.Accepted,
                },
            },
        }));

        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = {
                new CommandResult {
                    ClientSequence = 8,
                    AppliedTick = 80,
                    Status = CommandStatus.Duplicate,
                },
            },
        }));

        Assert.NotNull(model.LastCommandResult);
        Assert.Equal(8UL, model.LastCommandResult!.ClientSequence);
        Assert.Equal(CommandStatus.Duplicate, model.LastCommandResult.Status);
        Assert.Equal(CommandRejectReason.Unspecified, model.LastCommandResult.RejectReason);
    }

    [Fact]
    public async Task AcceptedThenDuplicateRemainsValidAfterPendingRemoval()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var cancellationToken = timeout.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            await DispatchHandshake(stream, cancellationToken);
            var command = Assert.Single((await ReadEnvelopeAsync(stream, cancellationToken)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = new CommandAck {
                    Results = {
                        new CommandResult {
                            ClientSequence = command.ClientSequence,
                            AppliedTick = command.RequestedTick,
                            Status = CommandStatus.Accepted,
                        },
                    },
                },
            }, cancellationToken);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = new CommandAck {
                    Results = {
                        new CommandResult {
                            ClientSequence = command.ClientSequence,
                            AppliedTick = command.RequestedTick,
                            Status = CommandStatus.Duplicate,
                        },
                    },
                },
            }, cancellationToken);
        }, cancellationToken);

        await using var client = new AuthoritativeClient(ConnectionOptions(listener));
        await client.ConnectAsync(cancellationToken);
        var sequence = client.Queue(new Command {
            MoveRequested = new MoveRequested { DirectionX = 1 },
        }, 109);
        await client.PollAsync(cancellationToken);

        var acknowledgements = new List<CommandResult>();
        for (var attempt = 0; attempt < 100 && acknowledgements.Count < 2; attempt++) {
            foreach (var message in client.DrainMessages()) {
                if (message.Acknowledgement is { } acknowledgement)
                    acknowledgements.AddRange(acknowledgement.Results);
            }
            if (acknowledgements.Count < 2)
                await Task.Delay(10, cancellationToken);
        }

        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        Assert.Equal(0, client.PendingCommandCount);
        Assert.Collection(
            acknowledgements,
            result => {
                Assert.Equal(sequence, result.ClientSequence);
                Assert.Equal(CommandStatus.Accepted, result.Status);
            },
            result => {
                Assert.Equal(sequence, result.ClientSequence);
                Assert.Equal(CommandStatus.Duplicate, result.Status);
            });
    }

    private static ClientConnectionOptions ConnectionOptions(TcpListener listener)
        => new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            "ack-characterization", "0.1.0", "content-hash") {
            MinimumRetryTimeout = TimeSpan.FromSeconds(30),
            MaximumRetryTimeout = TimeSpan.FromSeconds(30),
            SnapshotPollInterval = TimeSpan.FromMinutes(1),
        };

    private static async Task DispatchHandshake(NetworkStream stream, CancellationToken cancellationToken)
    {
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello,
            (await ReadEnvelopeAsync(stream, cancellationToken)).PayloadCase);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                TickRateHz = 20,
                GameMode = GameMode.SinglePlayer,
            },
        }, cancellationToken);
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot { Tick = 100 },
        }, cancellationToken);
    }

    private static async Task<Envelope> ReadEnvelopeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task WriteEnvelopeAsync(
        NetworkStream stream,
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.ToByteArray();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }
}
