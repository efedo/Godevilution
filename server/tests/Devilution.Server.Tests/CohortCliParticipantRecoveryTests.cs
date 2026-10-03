using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/**
 * Bounded black-box coverage for the shipped standalone cohort CLI.  These
 * tests deliberately use TCP participants and the production Server entry
 * point so a passing in-process host cannot mask launcher or recovery drift.
 */
public sealed class CohortCliParticipantRecoveryTests
{
    private const uint FixedWorldSeed = 2588;

    [Fact]
    public async Task ProductionCliPersistsAndRecoversCompleteCohortAcrossAbruptStop()
    {
        var root = NewFixtureRoot();
        var completed = false;
        try {
            var initial = await CliHost.StartAsync(
                root,
                FixedWorldSeed,
                TestContext.Current.CancellationToken);
            Exception? initialFailure = null;
            try {
                Assert.Contains("Persistence: complete cohort", initial.StartupLines);
                Assert.Contains("Command scheduling: clock-gated", initial.StartupLines);

                await using var participantA = await CliParticipant.ConnectAsync(
                    initial,
                    "cli-A",
                    string.Empty,
                    TestContext.Current.CancellationToken);
                await using var participantB = await CliParticipant.ConnectAsync(
                    initial,
                    "cli-B",
                    string.Empty,
                    TestContext.Current.CancellationToken);
                Assert.NotEqual(
                    participantA.EntityId,
                    participantB.EntityId);
                var tokenA = participantA.SessionToken;
                var tokenB = participantB.SessionToken;
                var entityA = participantA.EntityId;
                var entityB = participantB.EntityId;

                var seedBefore = ReadSeed(root);
                await participantB.DisposeAsync();

                // B's startup/disconnect work may outlive A's initial scheduling horizon.
                await participantA.RequestSnapshotAsync(TestContext.Current.CancellationToken);
                var starterItem = Assert.Single(
                    participantA.Snapshot.Players
                        .Single(player => player.CharacterName == "cli-A")
                        .Inventory
                        .Select((item, slot) => (item, slot)),
                        candidate => candidate.item.State is not null
                            && candidate.item.State.PresentationKind != ItemPresentationKind.Gold
                            && candidate.item.State.StableItemId != 0);
                var target = FindDropTarget(participantA.Snapshot, entityA);
                var dropLeadTicks = ComputeFutureLeadTicks(
                    participantA.LastProbeRoundTrip,
                    participantA.TickRateHz);
                var dropCommand = new Command {
                    ClientSequence = 1,
                    RequestedTick = checked(participantA.Snapshot.Tick + dropLeadTicks),
                    DropItemRequested = new DropItemRequested {
                        Item = new PlayerItemReference {
                            Location = PlayerItemLocation.Inventory,
                            Slot = checked((uint)starterItem.slot),
                            StableItemId = starterItem.item.State.StableItemId,
                        },
                        TargetX = target.X,
                        TargetY = target.Y,
                    },
                };
                var drop = await participantA.SendAsync(
                    dropCommand,
                    TestContext.Current.CancellationToken,
                    AdmissionDiagnostics(participantA, dropLeadTicks));
                RequireAccepted(drop.Result);
                Assert.Equal(1UL, drop.Result.ServerReceiptSequence);
                var dropped = Assert.Single(
                    participantA.Snapshot.WorldItems,
                    item => item.State is not null
                        && item.State.StableItemId == starterItem.item.State.StableItemId);
                Assert.Equal(target.X, dropped.PositionX);
                Assert.Equal(target.Y, dropped.PositionY);
                Assert.DoesNotContain(
                    participantA.Snapshot.Players.Single(player => player.CharacterName == "cli-A").Inventory,
                    item => item.State is not null
                        && item.State.StableItemId == starterItem.item.State.StableItemId);
                Assert.Equal(seedBefore, ReadSeed(root));

                var afterDrop = await WaitForCheckpointAsync(
                    root,
                    document => document.Participants.Count == 2
                        && document.Participants.Any(participant =>
                            participant.CommandReceipts.Any(receipt =>
                                receipt.ClientSequence == 1
                                && receipt.ServerReceiptSequence == 1)),
                    TestContext.Current.CancellationToken);
                Assert.Equal(2UL, afterDrop.NextServerReceiptSequence);
                Assert.Equal(
                    new[] { participantA.SessionId, participantB.SessionId }
                        .OrderBy(value => value, StringComparer.Ordinal),
                    afterDrop.Participants
                        .Select(participant => participant.SessionId)
                        .OrderBy(value => value, StringComparer.Ordinal));
                Assert.Equal(
                    entityA,
                    afterDrop.Participants.Single(participant =>
                        participant.SessionId == participantA.SessionId).EntityId);
                Assert.Equal(
                    entityB,
                    afterDrop.Participants.Single(participant =>
                        participant.SessionId == participantB.SessionId).EntityId);
                await initial.AbruptStopAsync(TestContext.Current.CancellationToken);
                Assert.Equal(seedBefore, ReadSeed(root));
                AssertNoLegacySessionFiles(root);
                using (var interruptedStore = new AuthoritativeCohortStore(root)) {
                    var interrupted = interruptedStore.Load()
                        ?? throw new XunitException("Abrupt CLI stop lost its canonical checkpoint.");
                    Assert.Equal(2UL, interrupted.NextServerReceiptSequence);
                    AssertTerminalModule(interrupted);
                }

                var resumed = await CliHost.StartAsync(
                    root,
                    null,
                    TestContext.Current.CancellationToken);
                Exception? resumedFailure = null;
                try {
                    await using var resumedB = await CliParticipant.ConnectAsync(
                        resumed,
                        "cli-B",
                        tokenB,
                        TestContext.Current.CancellationToken);
                    Assert.Equal(entityA, resumedB.EntityIdFor("cli-A"));
                    Assert.Equal(entityB, resumedB.EntityIdFor("cli-B"));
                    await resumedB.RequestSnapshotAsync(TestContext.Current.CancellationToken);
                    // Realtime simulation advances between these observations; RNG
                    // equality across different snapshot ticks is not a restore oracle.
                    var restoredWorldItem = Assert.Single(
                        resumedB.Snapshot.WorldItems,
                        item => item.State is not null
                            && item.State.StableItemId == starterItem.item.State.StableItemId);
                    Assert.Equal(dropped.EntityId, restoredWorldItem.EntityId);
                    Assert.Equal(dropped.ItemSeed, restoredWorldItem.ItemSeed);

                    var pickupLeadTicks = ComputeFutureLeadTicks(
                        resumedB.LastProbeRoundTrip,
                        resumedB.TickRateHz);
                    var pickupCommand = new Command {
                        ClientSequence = 1,
                        RequestedTick = checked(resumedB.Snapshot.Tick + pickupLeadTicks),
                        PickupWorldItemRequested = new PickupWorldItemRequested {
                            ItemEntityId = restoredWorldItem.EntityId,
                        },
                    };
                    var pickup = await resumedB.SendAsync(
                        pickupCommand,
                        TestContext.Current.CancellationToken,
                        AdmissionDiagnostics(resumedB, pickupLeadTicks));
                    RequireAccepted(pickup.Result);
                    Assert.Equal(2UL, pickup.Result.ServerReceiptSequence);
                    Assert.DoesNotContain(
                        resumedB.Snapshot.WorldItems,
                        item => item.EntityId == restoredWorldItem.EntityId);
                    Assert.Contains(
                        resumedB.Snapshot.Players.Single(player => player.CharacterName == "cli-B").Inventory,
                        item => item.State is not null
                            && item.State.StableItemId == starterItem.item.State.StableItemId);

                    await using var resumedA = await CliParticipant.ConnectAsync(
                        resumed,
                        "cli-A",
                        tokenA,
                        TestContext.Current.CancellationToken);
                    Assert.Equal(entityA, resumedA.EntityId);
                    Assert.Equal(entityB, resumedA.EntityIdFor("cli-B"));
                    Assert.DoesNotContain(
                        resumedA.Snapshot.WorldItems,
                        item => item.EntityId == restoredWorldItem.EntityId);
                    var duplicate = await resumedA.SendAsync(
                        dropCommand,
                        TestContext.Current.CancellationToken);
                    Assert.Equal(CommandStatus.Duplicate, duplicate.Result.Status);
                    Assert.Equal(1UL, duplicate.Result.ServerReceiptSequence);
                    AssertDuplicateReceipt(duplicate.Result, drop.Result);
                    Assert.DoesNotContain(
                        resumedA.Snapshot.WorldItems,
                        item => item.EntityId == restoredWorldItem.EntityId);

                    var afterPickup = await WaitForCheckpointAsync(
                        root,
                        document => document.NextServerReceiptSequence == 3
                            && document.Participants.All(participant =>
                                participant.CommandReceipts.Any(receipt =>
                                    receipt.ClientSequence == 1)),
                        TestContext.Current.CancellationToken);
                    Assert.Equal(3UL, afterPickup.NextServerReceiptSequence);
                    Assert.Equal(
                        new[] { 1UL, 2UL },
                        afterPickup.Participants
                            .SelectMany(participant => participant.CommandReceipts)
                            .Select(receipt => receipt.ServerReceiptSequence)
                            .OrderBy(sequence => sequence));
                    Assert.Equal(seedBefore, ReadSeed(root));
                    AssertNoLegacySessionFiles(root);

                    await resumed.QuitAsync(TestContext.Current.CancellationToken);
                    using (var validatedStore = new AuthoritativeCohortStore(root)) {
                        var validated = validatedStore.Load()
                            ?? throw new XunitException("Graceful CLI quit did not leave a cohort checkpoint.");
                        Assert.Equal(3UL, validated.NextServerReceiptSequence);
                        Assert.Equal(2, validated.Participants.Count);
                        AssertTerminalModule(validated);
                    }

                    var final = await CliHost.StartAsync(
                        root,
                        null,
                        TestContext.Current.CancellationToken);
                    Exception? finalFailure = null;
                    CommandResult? finalAResult = null;
                    CommandResult? finalBResult = null;
                    try {
                        await using var finalA = await CliParticipant.ConnectAsync(
                            final,
                            "cli-A",
                            tokenA,
                            TestContext.Current.CancellationToken);
                        await using var finalB = await CliParticipant.ConnectAsync(
                            final,
                            "cli-B",
                            tokenB,
                            TestContext.Current.CancellationToken);
                        Assert.Equal(entityA, finalA.EntityId);
                        Assert.Equal(entityB, finalA.EntityIdFor("cli-B"));
                        Assert.Equal(entityA, finalB.EntityIdFor("cli-A"));
                        Assert.Equal(entityB, finalB.EntityId);
                        Assert.Equal(1UL, finalA.LastClientSequence);
                        Assert.Equal(1UL, finalB.LastClientSequence);
                        Assert.DoesNotContain(
                            finalA.Snapshot.WorldItems,
                            item => item.EntityId == restoredWorldItem.EntityId);
                        Assert.Contains(
                            finalA.Snapshot.Players.Single(player => player.CharacterName == "cli-B").Inventory,
                            item => item.State is not null
                                && item.State.StableItemId == starterItem.item.State.StableItemId);
                        await finalA.RequestSnapshotAsync(TestContext.Current.CancellationToken);
                        var nextTarget = FindDropTarget(finalA.Snapshot, entityA);
                        var ownerPosition = finalA.Snapshot.Players.Single(player => player.EntityId == entityA);
                        var nextOwnerLeadTicks = ComputeFutureLeadTicks(
                            finalA.LastProbeRoundTrip,
                            finalA.TickRateHz);
                        var nextOwnerCommand = new Command {
                            ClientSequence = 2,
                            RequestedTick = checked(finalA.Snapshot.Tick + nextOwnerLeadTicks),
                            MoveRequested = new MoveRequested {
                                DirectionX = nextTarget.X - ownerPosition.PositionX,
                                DirectionY = nextTarget.Y - ownerPosition.PositionY,
                            },
                        };
                        var nextOwnerResult = await finalA.SendAsync(
                            nextOwnerCommand,
                            TestContext.Current.CancellationToken,
                            AdmissionDiagnostics(finalA, nextOwnerLeadTicks));
                        RequireAccepted(nextOwnerResult.Result);
                        Assert.Equal(3UL, nextOwnerResult.Result.ServerReceiptSequence);
                        finalAResult = nextOwnerResult.Result;
                        var duplicateOwnerCommand = await finalA.SendAsync(
                            nextOwnerCommand,
                            TestContext.Current.CancellationToken);
                        Assert.Equal(CommandStatus.Duplicate, duplicateOwnerCommand.Result.Status);
                        Assert.Equal(3UL, duplicateOwnerCommand.Result.ServerReceiptSequence);
                        AssertDuplicateReceipt(duplicateOwnerCommand.Result, nextOwnerResult.Result);
                        await finalA.WaitForSnapshotAsync(snapshot => IsSettledAt(snapshot, entityA, nextTarget),
                            TestContext.Current.CancellationToken);
                        await finalB.RequestSnapshotAsync(TestContext.Current.CancellationToken);
                        var peerTarget = FindDropTarget(finalB.Snapshot, entityB);
                        var peerPosition = finalB.Snapshot.Players.Single(player => player.EntityId == entityB);
                        var nextPeerLeadTicks = ComputeFutureLeadTicks(
                            finalB.LastProbeRoundTrip,
                            finalB.TickRateHz);
                        var nextPeerCommand = new Command {
                            ClientSequence = 2,
                            RequestedTick = checked(finalB.Snapshot.Tick + nextPeerLeadTicks),
                            MoveRequested = new MoveRequested {
                                DirectionX = peerTarget.X - peerPosition.PositionX,
                                DirectionY = peerTarget.Y - peerPosition.PositionY,
                            },
                        };
                        var nextPeerResult = await finalB.SendAsync(
                            nextPeerCommand,
                            TestContext.Current.CancellationToken,
                            AdmissionDiagnostics(finalB, nextPeerLeadTicks));
                        RequireAccepted(nextPeerResult.Result);
                        Assert.Equal(4UL, nextPeerResult.Result.ServerReceiptSequence);
                        finalBResult = nextPeerResult.Result;
                        await finalB.WaitForSnapshotAsync(snapshot => IsSettledAt(snapshot, entityB, peerTarget),
                            TestContext.Current.CancellationToken);
                    } catch (Exception exception) {
                        finalFailure = exception;
                        throw;
                    } finally {
                        await final.DisposePreservingAsync(finalFailure, gracefully: true);
                    }
                    using (var terminalStore = new AuthoritativeCohortStore(root)) {
                        var terminal = terminalStore.Load()
                            ?? throw new XunitException("Final CLI quit did not leave a cohort checkpoint.");
                        Assert.Equal(5UL, terminal.NextServerReceiptSequence);
                        Assert.Equal(afterDrop.NextEntityId, terminal.NextEntityId);
                        AssertTerminalModule(terminal);
                        Assert.Equal(
                            3UL,
                            terminal.Participants
                                .Single(participant => participant.EntityId == entityA)
                                .CommandReceipts
                                .Single(receipt => receipt.ClientSequence == 2)
                                .ServerReceiptSequence);
                        Assert.Equal(
                            2UL,
                            terminal.Participants
                                .Single(participant => participant.EntityId == entityB)
                                .CommandReceipts
                                .Single(receipt => receipt.ClientSequence == 1)
                                .ServerReceiptSequence);
                        Assert.Equal(4UL, terminal.Participants.Single(participant => participant.EntityId == entityB)
                            .CommandReceipts.Single(receipt => receipt.ClientSequence == 2).ServerReceiptSequence);
                        Assert.NotNull(finalAResult);
                        Assert.NotNull(finalBResult);
                        AssertCanonicalReceipt(terminal.Participants.Single(participant => participant.EntityId == entityA)
                            .CommandReceipts.Single(receipt => receipt.ClientSequence == 2), finalAResult);
                        AssertCanonicalReceipt(terminal.Participants.Single(participant => participant.EntityId == entityB)
                            .CommandReceipts.Single(receipt => receipt.ClientSequence == 2), finalBResult);
                    }
                } catch (Exception exception) {
                    resumedFailure = exception;
                    throw;
                } finally {
                    await resumed.DisposePreservingAsync(resumedFailure);
                }
            } catch (Exception exception) {
                initialFailure = exception;
                throw;
            } finally {
                await initial.DisposePreservingAsync(initialFailure);
            }

            completed = true;
        } finally {
            if (completed)
                DeleteFixtureRoot(root);
        }
    }

    [Fact]
    public async Task ProductionCliRefusesCompetingCohortLeaseWithoutChangingSeed()
    {
        var root = NewFixtureRoot();
        var completed = false;
        try {
            var owner = await CliHost.StartAsync(
                root,
                FixedWorldSeed,
                TestContext.Current.CancellationToken);
            Exception? ownerFailure = null;
            try {
                await using var ownerParticipant = await CliParticipant.ConnectAsync(
                    owner,
                    "lease-owner",
                    string.Empty,
                    TestContext.Current.CancellationToken);
                var ownerEntityId = ownerParticipant.EntityId;
                var ownerSessionId = ownerParticipant.SessionId;
                _ = await WaitForCheckpointAsync(
                    root,
                    document => document.Participants.Count == 1,
                    TestContext.Current.CancellationToken);
                var seedBefore = ReadSeed(root);
                var competing = await CliHost.StartRefusedAsync(
                    root,
                    null,
                    TestContext.Current.CancellationToken);
                Assert.Equal(1, competing.ExitCode);
                Assert.DoesNotContain(
                    "listening on",
                    competing.StandardOutput,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    "authoritative-cohort.lease",
                    competing.StandardError,
                    StringComparison.Ordinal);
                Assert.False(owner.Process.HasExited);
                Assert.Equal(seedBefore, ReadSeed(root));
                var liveCheckpoint = TryReadCheckpoint(root);
                Assert.NotNull(liveCheckpoint);
                Assert.Equal(
                    ownerEntityId,
                    liveCheckpoint!.Participants.Single(participant =>
                        participant.SessionId == ownerSessionId).EntityId);
                var liveSnapshot = await ownerParticipant.RequestSnapshotAsync(
                    TestContext.Current.CancellationToken);
                Assert.Equal(ownerEntityId, liveSnapshot.Players.Single(
                    player => player.CharacterName == "lease-owner").EntityId);
                var usableLeadTicks = ComputeFutureLeadTicks(
                    ownerParticipant.LastProbeRoundTrip,
                    ownerParticipant.TickRateHz);
                var usable = await ownerParticipant.SendAsync(
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = checked(ownerParticipant.Snapshot.Tick + usableLeadTicks),
                        MoveRequested = new MoveRequested {
                            DirectionX = 1,
                        },
                    },
                    TestContext.Current.CancellationToken,
                    AdmissionDiagnostics(ownerParticipant, usableLeadTicks));
                RequireAccepted(usable.Result);
                Assert.Equal(1UL, usable.Result.ServerReceiptSequence);
            } catch (Exception exception) {
                ownerFailure = exception;
                throw;
            } finally {
                await owner.DisposePreservingAsync(ownerFailure, gracefully: true);
            }

            completed = true;
        } finally {
            if (completed)
                DeleteFixtureRoot(root);
        }
    }

    [Fact]
    public async Task ProductionCliRefusesCorruptTerminalCheckpointWithoutChangingBytes()
    {
        var root = NewFixtureRoot();
        var completed = false;
        try {
            var owner = await CliHost.StartAsync(
                root,
                FixedWorldSeed,
                TestContext.Current.CancellationToken);
            Exception? ownerFailure = null;
            try {
                await using var participant = await CliParticipant.ConnectAsync(
                    owner,
                    "corruption-owner",
                    string.Empty,
                    TestContext.Current.CancellationToken);
                await participant.RequestSnapshotAsync(TestContext.Current.CancellationToken);
                var acceptedLeadTicks = ComputeFutureLeadTicks(
                    participant.LastProbeRoundTrip,
                    participant.TickRateHz);
                var accepted = await participant.SendAsync(
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = checked(participant.Snapshot.Tick + acceptedLeadTicks),
                        MoveRequested = new MoveRequested {
                            DirectionX = 1,
                        },
                    },
                    TestContext.Current.CancellationToken,
                    AdmissionDiagnostics(participant, acceptedLeadTicks));
                RequireAccepted(accepted.Result);
                Assert.Equal(1UL, accepted.Result.ServerReceiptSequence);
            } catch (Exception exception) {
                ownerFailure = exception;
                throw;
            } finally {
                await owner.DisposePreservingAsync(ownerFailure, gracefully: true);
            }

            var checkpointPath = Path.Combine(
                root,
                AuthoritativeCohortStore.CheckpointFileName);
            using (var terminalStore = new AuthoritativeCohortStore(root)) {
                var terminal = terminalStore.Load()
                    ?? throw new XunitException("Terminal checkpoint was not readable before corruption.");
                Assert.Equal(2UL, terminal.NextServerReceiptSequence);
                AssertTerminalModule(terminal);
            }
            var corrupted = File.ReadAllBytes(checkpointPath);
            var closingBrace = Array.LastIndexOf(corrupted, (byte)'}');
            Assert.True(closingBrace >= 0, "The terminal checkpoint was not a JSON object.");
            corrupted[closingBrace] = (byte)'!';
            File.WriteAllBytes(checkpointPath, corrupted);
            var seedBefore = ReadSeed(root);

            var refusal = await CliHost.StartRefusedAsync(
                root,
                null,
                TestContext.Current.CancellationToken);
            Assert.Equal(1, refusal.ExitCode);
            Assert.DoesNotContain(
                "listening on",
                refusal.StandardOutput,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Server startup failed", refusal.StandardError, StringComparison.Ordinal);
            Assert.Equal(corrupted, File.ReadAllBytes(checkpointPath));
            Assert.Equal(seedBefore, ReadSeed(root));
            AssertNoLegacySessionFiles(root);
            completed = true;
        } finally {
            if (completed)
                DeleteFixtureRoot(root);
        }
    }

    /** A stopped boundary validates complete encoding/owner bindings, not cross-tick RNG equality. */
    private static void AssertTerminalModule(AuthoritativeCohortDocument checkpoint)
    {
        var module = new DiabloCohortCodec().Deserialize(checkpoint.SerializedModuleState);
        Assert.Equal(checkpoint.Tick, module.Tick);
        Assert.Equal(FixedWorldSeed, module.Configuration.InitialRandomSeed);
        Assert.Equal(DiabloGameMode.Multiplayer, module.Configuration.GameMode);
        Assert.Equal(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            checkpoint.Identity.ProtocolSchemaVersion);
        Assert.Equal(AuthoritativeGameMode.Multiplayer, checkpoint.Identity.GameMode);
        Assert.Equal(checkpoint.Participants.Select(participant => (participant.SessionId, participant.EntityId))
                .OrderBy(owner => owner.SessionId, StringComparer.Ordinal),
            module.Participants.Select(participant => (participant.SessionId, participant.EntityId))
                .OrderBy(owner => owner.SessionId, StringComparer.Ordinal));
    }

    private static bool IsSettledAt(Snapshot snapshot, uint entityId, (int X, int Y) target)
        => snapshot.Players.Single(player => player.EntityId == entityId) is {
            CurrentAction: null, CombatMode: PlayerCombatMode.Stand } player
            && player.PositionX == target.X && player.PositionY == target.Y;

    private static void AssertDuplicateReceipt(CommandResult duplicate, CommandResult accepted)
    {
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(CommandRejectReason.Unspecified, duplicate.RejectReason);
        Assert.Equal(accepted.ClientSequence, duplicate.ClientSequence);
        Assert.Equal(accepted.ServerReceiptSequence, duplicate.ServerReceiptSequence);
        Assert.Equal(accepted.AppliedTick, duplicate.AppliedTick);
    }

    private static void AssertCanonicalReceipt(CommandResult canonical, CommandResult accepted)
    {
        RequireAccepted(canonical);
        Assert.Equal(accepted.ClientSequence, canonical.ClientSequence);
        Assert.Equal(accepted.ServerReceiptSequence, canonical.ServerReceiptSequence);
        Assert.Equal(accepted.AppliedTick, canonical.AppliedTick);
    }

    private static void RequireAccepted(CommandResult result)
        => Assert.True(result.Status == CommandStatus.Accepted
            && result.RejectReason == CommandRejectReason.Unspecified,
            $"Production CLI command {result.ClientSequence}: {result.Status}/{result.RejectReason}; "
            + $"applied tick {result.AppliedTick}, receipt {result.ServerReceiptSequence}.");

    private static ulong ComputeFutureLeadTicks(
        TimeSpan probeRoundTrip,
        uint tickRateHz)
    {
        Assert.True(tickRateHz > 0, "The production handshake must negotiate a tick rate.");
        Assert.True(
            probeRoundTrip >= TimeSpan.Zero
                && probeRoundTrip.TotalSeconds < ulong.MaxValue / (double)tickRateHz,
            "The measured timing probe duration must be finite and bounded.");
        var measuredTicks = probeRoundTrip.TotalSeconds * tickRateHz;
        Assert.True(
            double.IsFinite(measuredTicks),
            "The measured timing probe duration must produce a finite tick count.");
        return checked((ulong)Math.Ceiling(measuredTicks) + 1);
    }

    private static string AdmissionDiagnostics(
        CliParticipant participant,
        ulong futureLeadTicks)
        => $"timing request {participant.LastTimingRequestId}, "
            + $"timing reply snapshot tick {participant.LastTimingReplySnapshotTick}, "
            + $"command snapshot tick {participant.Snapshot.Tick}, "
            + $"probe RTT {participant.LastProbeRoundTrip.TotalMilliseconds:F3} ms, "
            + $"tick rate {participant.TickRateHz}, future lead {futureLeadTicks}";

    private static async Task<CohortCheckpointSummary> WaitForCheckpointAsync(
        string root,
        Func<CohortCheckpointSummary, bool> predicate,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true) {
            var document = TryReadCheckpoint(root);
            if (document is not null && predicate(document))
                return document;
            await Task.Delay(25, timeout.Token);
        }
    }

    private static CohortCheckpointSummary? TryReadCheckpoint(string root)
    {
        var path = Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName);
        try {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            var rootElement = document.RootElement;
            var participants = rootElement
                .GetProperty("Participants")
                .EnumerateArray()
                .Select(participant => new CohortParticipantSummary(
                    participant.GetProperty("SessionId").GetString()
                        ?? throw new JsonException("Cohort participant has no session ID."),
                    participant.GetProperty("EntityId").GetUInt32(),
                    participant
                        .GetProperty("CommandReceipts")
                        .EnumerateArray()
                        .Select(receipt => new CohortReceiptSummary(
                            receipt.GetProperty("ClientSequence").GetUInt64(),
                            receipt.GetProperty("ServerReceiptSequence").GetUInt64()))
                        .ToArray()))
                .ToArray();
            return new CohortCheckpointSummary(
                rootElement.GetProperty("Generation").GetUInt64(),
                rootElement.GetProperty("Tick").GetUInt64(),
                rootElement.GetProperty("NextEntityId").GetUInt32(),
                rootElement.GetProperty("NextServerReceiptSequence").GetUInt64(),
                participants);
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        } catch (IOException) {
            return null;
        } catch (JsonException) {
            return null;
        } catch (InvalidOperationException) {
            return null;
        } catch (KeyNotFoundException) {
            return null;
        }
    }

    private sealed record CohortCheckpointSummary(
        ulong Generation,
        ulong Tick,
        uint NextEntityId,
        ulong NextServerReceiptSequence,
        IReadOnlyList<CohortParticipantSummary> Participants);

    private sealed record CohortParticipantSummary(
        string SessionId,
        uint EntityId,
        IReadOnlyList<CohortReceiptSummary> CommandReceipts);

    private sealed record CohortReceiptSummary(
        ulong ClientSequence,
        ulong ServerReceiptSequence);

    private static byte[] ReadSeed(string root)
        => File.ReadAllBytes(Path.Combine(root, AuthoritativeWorldSeedStore.FileName));

    private static void AssertNoLegacySessionFiles(string root)
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly),
            path => {
                var name = Path.GetFileName(path);
                return name.EndsWith(".session.json", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".commands.json", StringComparison.OrdinalIgnoreCase)
                    || (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(
                            name,
                            AuthoritativeCohortStore.CheckpointFileName,
                            StringComparison.OrdinalIgnoreCase));
            });
    }

    private static (int X, int Y) FindDropTarget(Snapshot snapshot, uint ownerEntityId)
    {
        var owner = snapshot.Players.Single(player => player.EntityId == ownerEntityId);
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == owner.LevelId);
        var blocked = geometry.BlockedCells.ToHashSet();
        var occupied = snapshot.Players
            .Select(player => Cell(player.PositionX, player.PositionY, geometry.Width))
            .Concat(snapshot.WorldItems
                .Where(item => item.LevelId == owner.LevelId)
                .Select(item => Cell(item.PositionX, item.PositionY, geometry.Width)))
            .Concat(snapshot.Objects
                .Where(item => item.LevelId == owner.LevelId)
                .Select(item => Cell(item.PositionX, item.PositionY, geometry.Width)))
            .Concat(snapshot.Monsters
                .Where(item => item.LevelId == owner.LevelId && item.Alive)
                .Select(item => Cell(item.PositionX, item.PositionY, geometry.Width)))
            .ToHashSet();
        foreach (var (x, y) in new[] {
                     (owner.PositionX + 1, owner.PositionY),
                     (owner.PositionX, owner.PositionY + 1),
                     (owner.PositionX - 1, owner.PositionY),
                     (owner.PositionX, owner.PositionY - 1),
                 }) {
            if (x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height)
                continue;
            var cell = Cell(x, y, geometry.Width);
            if (!blocked.Contains(cell) && !occupied.Contains(cell))
                return (x, y);
        }
        throw new XunitException("The shipped base level has no adjacent valid drop target.");
    }

    private static int Cell(int x, int y, int width) => checked(y * width + x);

    private static string NewFixtureRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Cli-Cohort-" + Guid.NewGuid().ToString("N"));

    private static void DeleteFixtureRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var leaf = new DirectoryInfo(fullRoot).Name;
        const string prefix = "Godevilution-Cli-Cohort-";
        if (!string.Equals(
                Path.GetDirectoryName(fullRoot)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                tempRoot,
                StringComparison.OrdinalIgnoreCase)
            || !leaf.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(leaf[prefix.Length..], "N", out _)) {
            throw new InvalidOperationException($"Refusing to delete an unowned CLI fixture root: {fullRoot}");
        }
        if (Directory.Exists(fullRoot)) {
            AssertNoFixtureReparsePoints(fullRoot);
            Directory.Delete(fullRoot, recursive: true);
        }
    }

    private static void AssertNoFixtureReparsePoints(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0) {
            var current = pending.Pop();
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    $"Refusing to recursively delete a reparse point in the CLI fixture: {current}");
            if ((attributes & FileAttributes.Directory) == 0)
                continue;
            foreach (var child in Directory.EnumerateFileSystemEntries(current))
                pending.Push(child);
        }
    }

    private static string FindRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var starts = string.IsNullOrWhiteSpace(configured)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configured!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private static string FindServerAssembly()
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY");
        var configuration = typeof(CohortCliParticipantRecoveryTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
            ?? throw new XunitException("The executing test assembly has no build configuration.");
        var assembly = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(FindRepositoryRoot(), "server", "src", "Devilution.Server", "bin",
                configuration, "net10.0", "Devilution.Server.dll")
            : Path.GetFullPath(configured);
        if (!File.Exists(assembly))
            throw new XunitException(
                $"GODEVILUTION_SERVER_ASSEMBLY does not exist: {assembly}");
        return assembly;
    }

    private sealed class CliHost : IAsyncDisposable
    {
        private readonly Task<string> outputTail;
        private readonly Task<string> errorTail;
        private bool stopped;
        private bool disposed;

        private CliHost(
            Process process,
            string saveRoot,
            string[] startupLines,
            int port,
            string contentManifestHash,
            string rulesetIdentityHash,
            string protocolSchemaVersion,
            Task<string> outputTail,
            Task<string> errorTail)
        {
            Process = process;
            SaveRoot = saveRoot;
            StartupLines = startupLines;
            Port = port;
            ContentManifestHash = contentManifestHash;
            RulesetIdentityHash = rulesetIdentityHash;
            ProtocolSchemaVersion = protocolSchemaVersion;
            this.outputTail = outputTail;
            this.errorTail = errorTail;
        }

        public Process Process { get; }
        public string SaveRoot { get; }
        public IReadOnlyList<string> StartupLines { get; }
        public int Port { get; }
        public string ContentManifestHash { get; }
        public string RulesetIdentityHash { get; }
        public string ProtocolSchemaVersion { get; }

        public static async Task<CliHost> StartAsync(
            string saveRoot,
            uint? randomSeed,
            CancellationToken cancellationToken)
        {
            var process = StartProcess(saveRoot, randomSeed);
            Task<string>? errorTail = null;
            Task<string>? outputTail = null;
            try {
                errorTail = process.StandardError.ReadToEndAsync();
                var lines = new List<string>();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                while (true) {
                    var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                    if (line is null)
                        throw new XunitException(
                            $"Production CLI exited before startup (exit {process.ExitCode}): {await errorTail.WaitAsync(TimeSpan.FromSeconds(2), timeout.Token)}");
                    lines.Add(line);
                    if (line.StartsWith("Operator commands:", StringComparison.Ordinal))
                        break;
                }
                var port = ParsePort(lines);
                var content = ParseRequiredLine(lines, "Content manifest:");
                var ruleset = ParseRequiredLine(lines, "Ruleset identity:");
                var protocol = ParseRequiredLine(lines, "Protocol schema:");
                Assert.Equal(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity, protocol);
                Assert.Contains("Game mode: Multiplayer", lines);
                outputTail = process.StandardOutput.ReadToEndAsync();
                return new CliHost(
                    process,
                    saveRoot,
                    lines.ToArray(),
                    port,
                    content,
                    ruleset,
                    protocol,
                    outputTail,
                    errorTail);
            } catch (Exception primary) {
                await TerminateAndDisposeAsync(process, outputTail, errorTail, primary);
                throw;
            }
        }

        public static async Task<RefusedHost> StartRefusedAsync(
            string saveRoot,
            uint? randomSeed,
            CancellationToken cancellationToken)
        {
            var process = StartProcess(saveRoot, randomSeed);
            Task<string>? output = null;
            Task<string>? error = null;
            Exception? primary = null;
            try {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                error = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token)
                    .WaitAsync(TimeSpan.FromSeconds(10), timeout.Token);
                return new RefusedHost(process.ExitCode, await output, await error);
            } catch (Exception exception) {
                primary = exception;
                throw;
            } finally {
                await TerminateAndDisposeAsync(process, output, error, primary);
            }
        }

        public async Task QuitAsync(CancellationToken cancellationToken)
        {
            if (stopped)
                return;
            if (!Process.HasExited) {
                await Process.StandardInput.WriteLineAsync("quit");
                await Process.StandardInput.FlushAsync(cancellationToken);
                await Process.WaitForExitAsync(cancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            await DrainAsync(cancellationToken);
            Assert.Equal(0, Process.ExitCode);
            stopped = true;
        }

        public async Task AbruptStopAsync(CancellationToken cancellationToken)
        {
            if (stopped)
                return;
            if (!Process.HasExited)
                Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await DrainAsync(cancellationToken);
            stopped = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;
            await TerminateAndDisposeAsync(Process, outputTail, errorTail);
        }

        public async Task DisposePreservingAsync(Exception? primary, bool gracefully = false)
        {
            var failure = primary;
            if (gracefully) {
                try { await QuitAsync(TestContext.Current.CancellationToken); }
                catch (Exception exception) { RecordCleanupFailure(ref failure, exception, "Quit"); }
            }
            try { await DisposeAsync(); }
            catch (Exception exception) { RecordCleanupFailure(ref failure, exception, "Dispose"); }
            if (primary is null && failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        /** Always attempt terminal wait, each pipe drain and disposal, preserving the first error. */
        private static async Task TerminateAndDisposeAsync(Process process, Task<string>? output,
            Task<string>? error, Exception? primary = null)
        {
            var failure = primary;
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception exception) { RecordCleanupFailure(ref failure, exception, "Kill"); }
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception exception) { RecordCleanupFailure(ref failure, exception, "TerminalWait"); }
            foreach (var (name, task) in new[] { ("Stdout", output), ("Stderr", error) }) {
                if (task is null) continue;
                try { await task.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (Exception exception) { RecordCleanupFailure(ref failure, exception, name); }
            }
            try { process.Dispose(); }
            catch (Exception exception) { RecordCleanupFailure(ref failure, exception, "Handle"); }
            if (primary is null && failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static void RecordCleanupFailure(ref Exception? failure, Exception secondary, string stage)
        {
            if (failure is null) failure = secondary;
            else failure.Data["CliCleanup-" + stage] = secondary.Message;
        }

        private async Task DrainAsync(CancellationToken cancellationToken)
        {
            await outputTail.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await errorTail.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }

        private static Process StartProcess(string saveRoot, uint? randomSeed)
        {
            var repository = FindRepositoryRoot();
            var contentRoot = Path.Combine(repository, "server", "content", "base");
            var startInfo = new ProcessStartInfo {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = repository,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(FindServerAssembly());
            startInfo.ArgumentList.Add("--content-root");
            startInfo.ArgumentList.Add(contentRoot);
            startInfo.ArgumentList.Add("--save-root");
            startInfo.ArgumentList.Add(saveRoot);
            startInfo.ArgumentList.Add("--bind");
            startInfo.ArgumentList.Add("127.0.0.1");
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("--persistence-mode");
            startInfo.ArgumentList.Add("cohort");
            startInfo.ArgumentList.Add("--game-mode");
            startInfo.ArgumentList.Add("multiplayer");
            startInfo.ArgumentList.Add("--protocol-version");
            startInfo.ArgumentList.Add(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity);
            startInfo.ArgumentList.Add("--command-scheduling");
            startInfo.ArgumentList.Add("clock-gated");
            startInfo.ArgumentList.Add("--starting-level");
            startInfo.ArgumentList.Add("0");
            if (randomSeed.HasValue) {
                startInfo.ArgumentList.Add("--random-seed");
                startInfo.ArgumentList.Add(randomSeed.Value.ToString());
            }
            var process = new Process { StartInfo = startInfo };
            try {
                if (!process.Start())
                    throw new XunitException("The production Devilution.Server process did not start.");
                return process;
            } catch (Exception primary) {
                try { process.Dispose(); }
                catch (Exception secondary) { primary.Data["CliStartupDispose"] = secondary.Message; }
                throw;
            }
        }

        private static int ParsePort(IReadOnlyList<string> lines)
        {
            var line = lines.Single(value => value.StartsWith(
                "Godevilution authoritative server listening on ",
                StringComparison.Ordinal));
            var separator = line.LastIndexOf(':');
            return int.Parse(line[(separator + 1)..]);
        }

        private static string ParseRequiredLine(
            IReadOnlyList<string> lines,
            string prefix)
        {
            var line = lines.Single(value => value.StartsWith(prefix, StringComparison.Ordinal));
            return line[prefix.Length..].Trim();
        }
    }

    private sealed record RefusedHost(int ExitCode, string StandardOutput, string StandardError);

    private sealed class CliParticipant : IAsyncDisposable
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private ulong nextTimingRequestId;

        private CliParticipant(
            TcpClient client,
            NetworkStream stream,
            string sessionToken,
            Snapshot snapshot,
            string characterName,
            ulong lastClientSequence,
            uint tickRateHz)
        {
            this.client = client;
            this.stream = stream;
            SessionToken = sessionToken;
            Snapshot = snapshot;
            CharacterName = characterName;
            LastClientSequence = lastClientSequence;
            TickRateHz = tickRateHz;
        }

        public string SessionToken { get; }
        public string SessionId => SessionIdForToken(SessionToken);
        public Snapshot Snapshot { get; private set; }
        public uint EntityId => EntityIdForCharacter(Snapshot, CharacterName);
        public ulong LastClientSequence { get; }
        public uint TickRateHz { get; }
        public ulong LastTimingReplySnapshotTick { get; private set; }
        public ulong LastTimingRequestId { get; private set; }
        public TimeSpan LastProbeRoundTrip { get; private set; }
        private string CharacterName { get; }

        public static async Task<CliParticipant> ConnectAsync(
            CliHost host,
            string characterName,
            string resumeToken,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var client = new TcpClient();
            try {
                await client.ConnectAsync(IPAddress.Loopback, host.Port, timeout.Token);
                var stream = client.GetStream();
                await EnvelopeCodec.WriteAsync(
                    stream,
                    new Envelope {
                        ClientHello = new ClientHello {
                            ClientBuildId = "cohort-cli-recovery-test",
                            ProtocolSchemaVersion = host.ProtocolSchemaVersion,
                            ContentManifestHash = host.ContentManifestHash,
                            RulesetIdentityHash = host.RulesetIdentityHash,
                            ResumeToken = resumeToken,
                            RequestedCharacterName = characterName,
                            SupportsClockGatedCommands = true,
                        },
                    },
                    timeout.Token);
                var hello = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
                Assert.Equal(host.ContentManifestHash, hello.ServerHello.ContentManifestHash);
                Assert.Equal(host.RulesetIdentityHash, hello.ServerHello.RulesetIdentityHash);
                Assert.Equal(host.ProtocolSchemaVersion, hello.ServerHello.ProtocolSchemaVersion);
                Assert.Equal(GameMode.Multiplayer, hello.ServerHello.GameMode);
                Assert.Equal(CommandSchedulingMode.ClockGated, hello.ServerHello.CommandSchedulingMode);
                Assert.NotEqual(0U, hello.ServerHello.TickRateHz);
                var snapshot = await ReadSnapshotAsync(stream, timeout.Token);
                return new CliParticipant(
                    client,
                    stream,
                    hello.ServerHello.SessionToken,
                    snapshot,
                    characterName,
                    hello.ServerHello.LastClientSequence,
                    hello.ServerHello.TickRateHz);
            } catch {
                client.Dispose();
                throw;
            }
        }

        public async Task<(CommandResult Result, Snapshot Snapshot)> SendAsync(
            Command command,
            CancellationToken cancellationToken,
            string? admissionDiagnostics = null)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var admissionSnapshotTick = Snapshot.Tick;
            await EnvelopeCodec.WriteAsync(
                stream,
                new Envelope { CommandBatch = new CommandBatch { Commands = { command } } },
                timeout.Token);
            CommandResult? result = null;
            while (result is null) {
                var envelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                switch (envelope.PayloadCase) {
                case Envelope.PayloadOneofCase.CommandAck:
                    result = envelope.CommandAck.Results.SingleOrDefault(
                        candidate => candidate.ClientSequence == command.ClientSequence);
                    if (result is null)
                        continue;
                    break;
                case Envelope.PayloadOneofCase.Snapshot:
                    Snapshot = envelope.Snapshot;
                    break;
                case Envelope.PayloadOneofCase.EventBatch:
                case Envelope.PayloadOneofCase.TimingReply:
                    break;
                default:
                    throw new XunitException(
                        $"Unexpected payload while waiting for command {command.ClientSequence}: {envelope.PayloadCase}.");
                }
            }
            if (result!.Status is CommandStatus.Rejected or CommandStatus.Rescheduled) {
                throw new XunitException(
                    "Production CLI command admission was not accepted: "
                    + $"timing request {LastTimingRequestId}, "
                    + $"timing reply snapshot tick {LastTimingReplySnapshotTick}, "
                    + $"requested tick {command.RequestedTick}, snapshot tick {admissionSnapshotTick}, "
                    + $"status {result.Status}, reason {result.RejectReason}, "
                    + $"applied tick {result.AppliedTick}, receipt {result.ServerReceiptSequence}."
                    + (admissionDiagnostics is null
                        ? string.Empty
                        : $" Admission probe: {admissionDiagnostics}."));
            }
            Snapshot = await ReadSnapshotAsync(stream, timeout.Token);
            Assert.True(
                Snapshot.Tick >= result.AppliedTick,
                $"Snapshot tick {Snapshot.Tick} regressed below applied tick {result.AppliedTick}.");
            return (result!, Snapshot);
        }

        public async Task<Snapshot> RequestSnapshotAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var timingRequestId = checked(++nextTimingRequestId);
            var probeStarted = Stopwatch.GetTimestamp();
            await EnvelopeCodec.WriteAsync(
                stream,
                new Envelope {
                    SnapshotRequest = new Envelope.Types.SnapshotRequest {
                        TimingRequestId = timingRequestId,
                    },
                },
                timeout.Token);
            Snapshot? timingSnapshot = null;
            while (true) {
                var envelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                switch (envelope.PayloadCase) {
                case Envelope.PayloadOneofCase.Snapshot:
                    // The host publishes the correlated snapshot before its
                    // matching timing reply. Keep consuming queued broadcasts
                    // until that reply is observed.
                    timingSnapshot = envelope.Snapshot;
                    break;
                case Envelope.PayloadOneofCase.TimingReply:
                    if (envelope.TimingReply.RequestId != timingRequestId)
                        continue;
                    timingSnapshot ??= await ReadSnapshotAsync(stream, timeout.Token);
                    await EnvelopeCodec.WriteAsync(
                        stream,
                        new Envelope {
                            // Request a snapshot after the matching timing
                            // reply so pre-request broadcasts are excluded.
                            SnapshotRequest = new Envelope.Types.SnapshotRequest(),
                        },
                        timeout.Token);
                    Snapshot = await ReadSnapshotAsync(stream, timeout.Token);
                    LastTimingRequestId = timingRequestId;
                    LastTimingReplySnapshotTick = timingSnapshot.Tick;
                    LastProbeRoundTrip = Stopwatch.GetElapsedTime(probeStarted);
                    return Snapshot;
                case Envelope.PayloadOneofCase.EventBatch:
                    break;
                default:
                    throw new XunitException(
                        $"Unexpected payload while waiting for timing request {timingRequestId}: "
                        + $"{envelope.PayloadCase}.");
                }
            }
        }

        public async Task WaitForSnapshotAsync(Func<Snapshot, bool> predicate, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (!predicate(await RequestSnapshotAsync(timeout.Token)))
                await Task.Delay(25, timeout.Token);
        }

        public uint EntityIdFor(string characterName)
            => EntityIdForCharacter(Snapshot, characterName);

        public async ValueTask DisposeAsync()
        {
            try { await stream.DisposeAsync(); }
            finally { client.Dispose(); }
        }

        private static uint EntityIdForCharacter(Snapshot snapshot, string characterName)
            => snapshot.Players.Single(player => player.CharacterName == characterName).EntityId;
    }

    private static string SessionIdForToken(string token)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    private static async Task<Snapshot> ReadSnapshotAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        while (true) {
            var envelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot)
                return envelope.Snapshot;
            if (envelope.PayloadCase is Envelope.PayloadOneofCase.EventBatch
                or Envelope.PayloadOneofCase.TimingReply)
                continue;
            throw new XunitException(
                $"Unexpected payload while waiting for a snapshot: {envelope.PayloadCase}.");
        }
    }

    private static async Task<Envelope> ReadRequiredEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var envelope = await EnvelopeCodec.ReadAsync(stream, timeout.Token)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), timeout.Token);
        return envelope ?? throw new XunitException("The production CLI closed a participant connection unexpectedly.");
    }
}
