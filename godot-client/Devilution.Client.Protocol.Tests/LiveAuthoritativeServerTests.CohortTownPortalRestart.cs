using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{
    [Fact]
    public async Task LiveCohortRestoresOwnedTownPortalAndNonOwnerUseAfterProcessRestart()
    {
        const string ownerName = "M1-Cohort-Portal-Owner";
        const string guestName = "M1-Cohort-Portal-Guest";
        const uint smithStoreId = 1;
        const uint portalScrollSlot = 2;
        const uint portalScrollSeed = 9100;
        const uint portalScrollSpellId = 39;
        const uint portalScrollPrice = 50;
        var cancellationToken = TestContext.Current.CancellationToken;
        var repositoryRoot = FindRepositoryRoot();
        var townLoopPreviewRoot = Path.Combine(repositoryRoot, "server", "content", "town-loop-preview");
        var townPortalPreviewRoot = Path.Combine(repositoryRoot, "server", "content", "town-portal-preview");
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Cohort-Town-Portal-" + Guid.NewGuid().ToString("N"));
        var saveRootLease = LiveTestSaveRoot.Register(saveRoot);
        StartedServer? firstServer = null;
        StartedServer? resumedServer = null;
        AuthoritativeClient? ownerClient = null;
        AuthoritativeClient? guestClient = null;
        AuthoritativeClient? resumedGuestClient = null;
        AuthoritativeClient? resumedOwnerClient = null;
        Exception? primaryFailure = null;

        try {
            string ownerResumeToken;
            string guestResumeToken;
            string contentHash;
            string rulesetHash;
            string serverBuildId;
            uint ownerEntityId;
            uint guestEntityId;
            uint portalId;
            ulong ownerLastSequenceAtCheckpoint;
            ulong guestLastSequenceAtCheckpoint;
            ulong firstHostProcessId;
            int port;
            ulong baselinePortalTick;
            uint baselinePortalRemaining;
            PortalSnapshot baselinePortal;
            DiabloCohortPortalState checkpointPortal;
            AuthoritativeCohortDocument firstCheckpoint;
            DiabloCohortState firstCheckpointState;
            var ownerReceipts = new List<CommandResult>();
            var guestReceipts = new List<CommandResult>();
            var globalReceipts = new List<CommandResult>();

            firstServer = await StartCohortPortalServerAsync(
                cancellationToken,
                townLoopPreviewRoot,
                townPortalPreviewRoot,
                saveRoot);
            firstHostProcessId = checked((ulong)firstServer.ProcessId);
            port = firstServer.Port;
            contentHash = firstServer.ContentHash;
            rulesetHash = firstServer.RulesetHash;

            ownerClient = CreateCohortPortalClient(firstServer, "cohort-portal-owner-first", 0, ownerName);
            await ownerClient.ConnectAsync(cancellationToken);
            var ownerModel = new AuthoritativeClientModel();
            var ownerInitial = await WaitForMessageAsync(ownerClient, message =>
                message.Snapshot is { Players.Count: 1 });
            ownerModel.Apply(ownerInitial);
            var ownerInitialPlayer = Assert.Single(ownerModel.Snapshot!.Players);
            Assert.Equal(ownerName, ownerInitialPlayer.CharacterName);
            Assert.Equal(0U, ownerInitialPlayer.CharacterClassId);
            Assert.Equal(0U, ownerInitialPlayer.LevelId);
            Assert.NotEqual(0U, ownerInitialPlayer.EntityId);
            Assert.True(ownerInitialPlayer.Gold >= portalScrollPrice);
            ownerEntityId = ownerInitialPlayer.EntityId;
            AssertClientCohortIdentity(ownerClient, firstServer, null);
            serverBuildId = ownerClient.ServerIdentity!.BuildId;

            guestClient = CreateCohortPortalClient(firstServer, "cohort-portal-guest-first", 1, guestName);
            await guestClient.ConnectAsync(cancellationToken);
            var guestModel = new AuthoritativeClientModel();
            var guestInitial = await WaitForMessageAsync(guestClient, message =>
                message.Snapshot is { Players.Count: 2 }
                && message.Snapshot.Players.Any(player => player.CharacterName == ownerName)
                && message.Snapshot.Players.Any(player => player.CharacterName == guestName));
            guestModel.Apply(guestInitial);
            var guestInitialPlayer = guestModel.Snapshot!.Players.Single(player => player.CharacterName == guestName);
            Assert.Equal(1U, guestInitialPlayer.CharacterClassId);
            Assert.Equal(0U, guestInitialPlayer.LevelId);
            Assert.NotEqual(0U, guestInitialPlayer.EntityId);
            Assert.NotEqual(ownerEntityId, guestInitialPlayer.EntityId);
            guestEntityId = guestInitialPlayer.EntityId;
            AssertClientCohortIdentity(guestClient, firstServer, serverBuildId);

            var ownerTwoActorMessage = await WaitForMessageAsync(ownerClient, message =>
                message.Snapshot is { Players.Count: 2 }
                && message.Snapshot.Players.Any(player => player.EntityId == guestEntityId));
            ownerModel.Apply(ownerTwoActorMessage);
            AssertCohortWireActors(ownerModel.Snapshot!, ownerEntityId, ownerName, 0, guestEntityId, guestName, 1);

            await MoveActorNearTownerAsync(
                ownerClient, ownerModel, ownerEntityId, 0, ownerReceipts, globalReceipts, cancellationToken);
            var ownerNearSmith = FindCohortActor(ownerModel.Snapshot!, ownerEntityId);
            var openStoreAcknowledgement = await SubmitCohortCommandAsync(
                ownerClient,
                ownerModel,
                new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = smithStoreId } },
                ownerReceipts,
                globalReceipts,
                cancellationToken);
            Assert.True(openStoreAcknowledgement.AppliedTick > 0);
            var openedStore = await WaitForCohortSnapshotAsync(ownerClient, ownerModel, snapshot =>
                snapshot.ActiveStore?.StoreId == smithStoreId,
                openStoreAcknowledgement.AppliedTick,
                cancellationToken);
            var listedScroll = Assert.Single(openedStore.ActiveStore!.Items, item => item.StoreSlot == portalScrollSlot);
            Assert.Equal(portalScrollSeed, listedScroll.ItemSeed);
            Assert.Equal(portalScrollPrice, listedScroll.Price);
            Assert.Equal((int)portalScrollSpellId, listedScroll.State.SpellId);
            Assert.Equal(ItemSpellSourceKind.ConsumableItem, listedScroll.State.SpellSourceKind);

            var purchaseAcknowledgement = await SubmitCohortCommandAsync(
                ownerClient,
                ownerModel,
                new Command { PurchaseRequested = new PurchaseRequested { StoreId = smithStoreId, StoreSlot = portalScrollSlot } },
                ownerReceipts,
                globalReceipts,
                cancellationToken);
            var purchasedSnapshot = await WaitForCohortSnapshotAsync(ownerClient, ownerModel, snapshot =>
                FindCohortActor(snapshot, ownerEntityId).Inventory.Any(item =>
                    item.ItemSeed == portalScrollSeed && item.State.SpellId == (int)portalScrollSpellId),
                purchaseAcknowledgement.AppliedTick,
                cancellationToken);
            var purchasedOwner = FindCohortActor(purchasedSnapshot, ownerEntityId);
            Assert.Equal(ownerNearSmith.Gold - portalScrollPrice, purchasedOwner.Gold);
            var purchasedScroll = Assert.Single(purchasedOwner.Inventory, item =>
                item.ItemSeed == portalScrollSeed && item.State.SpellId == (int)portalScrollSpellId);
            Assert.NotEqual(0U, purchasedScroll.State.StableItemId);
            Assert.Equal(ItemSpellSourceKind.ConsumableItem, purchasedScroll.State.SpellSourceKind);

            var closeStoreAcknowledgement = await SubmitCohortCommandAsync(
                ownerClient,
                ownerModel,
                new Command { CloseStoreRequested = new CloseStoreRequested() },
                ownerReceipts,
                globalReceipts,
                cancellationToken);
            _ = await WaitForCohortSnapshotAsync(
                ownerClient,
                ownerModel,
                snapshot => snapshot.ActiveStore is null,
                closeStoreAcknowledgement.AppliedTick,
                cancellationToken);

            await MoveActorToWarpAsync(
                ownerClient, ownerModel, ownerEntityId, "town_to_cathedral", ownerReceipts, globalReceipts, cancellationToken);
            var ownerInCathedral = FindCohortActor(ownerModel.Snapshot!, ownerEntityId);
            Assert.Equal(1U, ownerInCathedral.LevelId);
            var scrollInventoryIndex = ownerInCathedral.Inventory
                .Select((item, index) => (item, index))
                .Single(entry => entry.item.State.StableItemId == purchasedScroll.State.StableItemId)
                .index;
            var castAcknowledgement = await SubmitCohortCommandAsync(
                ownerClient,
                ownerModel,
                new Command {
                    CastRequested = new CastRequested {
                        SpellId = portalScrollSpellId,
                        TargetX = checked(ownerInCathedral.PositionX + 1),
                        TargetY = ownerInCathedral.PositionY,
                        Source = CastSourceKind.ConsumableItem,
                        SourceItem = new PlayerItemReference {
                            Location = PlayerItemLocation.Inventory,
                            Slot = checked((uint)scrollInventoryIndex),
                            StableItemId = purchasedScroll.State.StableItemId,
                        },
                    },
                },
                ownerReceipts,
                globalReceipts,
                cancellationToken);
            var castSnapshot = await WaitForCohortSnapshotAsync(ownerClient, ownerModel, snapshot =>
                FindCohortActor(snapshot, ownerEntityId) is { } player
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && !player.Inventory.Any(item => item.State.StableItemId == purchasedScroll.State.StableItemId)
                    && snapshot.Portals.Any(portal => portal.OwnerEntityId == ownerEntityId),
                castAcknowledgement.AppliedTick,
                cancellationToken);
            baselinePortal = Assert.Single(castSnapshot.Portals, portal => portal.OwnerEntityId == ownerEntityId).Clone();
            portalId = baselinePortal.PortalId;
            Assert.NotEqual(0U, portalId);
            Assert.Equal(1U, baselinePortal.SourceLevelId);
            Assert.Equal(0U, baselinePortal.DestinationLevelId);
            Assert.True(baselinePortal.Bidirectional);
            Assert.True(baselinePortal.RemainingTicks > 1);
            baselinePortalTick = castSnapshot.Tick;
            baselinePortalRemaining = baselinePortal.RemainingTicks;

            await MoveActorToCellAsync(
                ownerClient,
                ownerModel,
                ownerEntityId,
                baselinePortal.SourceLevelId,
                baselinePortal.SourcePositionX,
                baselinePortal.SourcePositionY,
                ownerReceipts,
                globalReceipts,
                cancellationToken);
            await RefreshCohortModelAsync(ownerClient, ownerModel, guestModel.CurrentTick, cancellationToken);
            AssertPortalEndpointUnoccupiedByOtherActor(
                ownerModel.Snapshot!,
                baselinePortal.DestinationLevelId,
                baselinePortal.DestinationPositionX,
                baselinePortal.DestinationPositionY,
                ownerEntityId);
            _ = AssertCohortPortalUseActor(
                ownerModel.Snapshot!,
                ownerEntityId,
                baselinePortal.SourceLevelId,
                baselinePortal.SourcePositionX,
                baselinePortal.SourcePositionY);
            var ownerSourceUse = await SubmitCohortCommandAsync(
                ownerClient,
                ownerModel,
                new Command { UsePortalRequested = new UsePortalRequested { PortalId = portalId } },
                ownerReceipts,
                globalReceipts,
                cancellationToken,
                ownerEntityId);
            var ownerTownSnapshot = await WaitForCohortSnapshotAsync(ownerClient, ownerModel, snapshot =>
                FindCohortActor(snapshot, ownerEntityId) is { } player
                    && player.LevelId == baselinePortal.DestinationLevelId
                    && (player.PositionX, player.PositionY)
                        == (baselinePortal.DestinationPositionX, baselinePortal.DestinationPositionY)
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.Any(portal => portal.PortalId == portalId),
                ownerSourceUse.AppliedTick,
                cancellationToken);
            _ = AssertTownPortalAtTick(ownerTownSnapshot, baselinePortal, baselinePortalTick, baselinePortalRemaining);
            await MoveActorToAdjacentOfPortalEndpointAsync(
                ownerClient,
                ownerModel,
                ownerEntityId,
                baselinePortal.DestinationLevelId,
                baselinePortal.DestinationPositionX,
                baselinePortal.DestinationPositionY,
                ownerReceipts,
                globalReceipts,
                cancellationToken);
            var ownerAwayFromTownEndpoint = FindCohortActor(ownerModel.Snapshot!, ownerEntityId);
            Assert.Equal(1, ChebyshevDistance(
                ownerAwayFromTownEndpoint.PositionX,
                ownerAwayFromTownEndpoint.PositionY,
                baselinePortal.DestinationPositionX,
                baselinePortal.DestinationPositionY));
            Assert.NotEqual(
                (baselinePortal.DestinationPositionX, baselinePortal.DestinationPositionY),
                (ownerAwayFromTownEndpoint.PositionX, ownerAwayFromTownEndpoint.PositionY));

            await RefreshCohortModelAsync(guestClient!, guestModel, ownerModel.CurrentTick, cancellationToken);
            await MoveActorToWarpAsync(
                guestClient!, guestModel, guestEntityId, "town_to_cathedral", guestReceipts, globalReceipts, cancellationToken);
            await MoveActorToCellAsync(
                guestClient!,
                guestModel,
                guestEntityId,
                baselinePortal.SourceLevelId,
                baselinePortal.SourcePositionX,
                baselinePortal.SourcePositionY,
                guestReceipts,
                globalReceipts,
                cancellationToken);
            await RefreshCohortModelAsync(ownerClient, ownerModel, guestModel.CurrentTick, cancellationToken);
            AssertPortalEndpointUnoccupiedByOtherActor(
                guestModel.Snapshot!,
                baselinePortal.DestinationLevelId,
                baselinePortal.DestinationPositionX,
                baselinePortal.DestinationPositionY,
                guestEntityId);
            _ = AssertCohortPortalUseActor(
                guestModel.Snapshot!,
                guestEntityId,
                baselinePortal.SourceLevelId,
                baselinePortal.SourcePositionX,
                baselinePortal.SourcePositionY);
            var guestSourceUse = await SubmitCohortCommandAsync(
                guestClient!,
                guestModel,
                new Command { UsePortalRequested = new UsePortalRequested { PortalId = portalId } },
                guestReceipts,
                globalReceipts,
                cancellationToken,
                guestEntityId);
            var guestTownSnapshot = await WaitForCohortSnapshotAsync(guestClient!, guestModel, snapshot =>
                FindCohortActor(snapshot, guestEntityId) is { } player
                    && player.LevelId == baselinePortal.DestinationLevelId
                    && (player.PositionX, player.PositionY)
                        == (baselinePortal.DestinationPositionX, baselinePortal.DestinationPositionY)
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.Any(portal => portal.PortalId == portalId),
                guestSourceUse.AppliedTick,
                cancellationToken);
            _ = AssertTownPortalAtTick(guestTownSnapshot, baselinePortal, baselinePortalTick, baselinePortalRemaining);
            await RefreshCohortModelAsync(ownerClient, ownerModel, guestModel.CurrentTick, cancellationToken);
            AssertPortalEndpointUnoccupiedByOtherActor(
                guestModel.Snapshot!,
                baselinePortal.SourceLevelId,
                baselinePortal.SourcePositionX,
                baselinePortal.SourcePositionY,
                guestEntityId);
            _ = AssertCohortPortalUseActor(
                guestModel.Snapshot!,
                guestEntityId,
                baselinePortal.DestinationLevelId,
                baselinePortal.DestinationPositionX,
                baselinePortal.DestinationPositionY);
            var guestReturnUse = await SubmitCohortCommandAsync(
                guestClient!,
                guestModel,
                new Command { UsePortalRequested = new UsePortalRequested { PortalId = portalId } },
                guestReceipts,
                globalReceipts,
                cancellationToken,
                guestEntityId);
            var guestReturnedSnapshot = await WaitForCohortSnapshotAsync(guestClient!, guestModel, snapshot =>
                FindCohortActor(snapshot, guestEntityId) is { } player
                    && player.LevelId == baselinePortal.SourceLevelId
                    && (player.PositionX, player.PositionY)
                        == (baselinePortal.SourcePositionX, baselinePortal.SourcePositionY)
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.Any(portal => portal.PortalId == portalId),
                guestReturnUse.AppliedTick,
                cancellationToken);
            _ = AssertTownPortalAtTick(guestReturnedSnapshot, baselinePortal, baselinePortalTick, baselinePortalRemaining);
            AssertPortalEndpointUnoccupiedByOtherActor(
                guestModel.Snapshot!,
                baselinePortal.DestinationLevelId,
                baselinePortal.DestinationPositionX,
                baselinePortal.DestinationPositionY,
                guestEntityId);
            _ = AssertCohortPortalUseActor(
                guestModel.Snapshot!,
                guestEntityId,
                baselinePortal.SourceLevelId,
                baselinePortal.SourcePositionX,
                baselinePortal.SourcePositionY);
            var guestSecondTownUse = await SubmitCohortCommandAsync(
                guestClient!,
                guestModel,
                new Command { UsePortalRequested = new UsePortalRequested { PortalId = portalId } },
                guestReceipts,
                globalReceipts,
                cancellationToken,
                guestEntityId);
            var guestSecondTownSnapshot = await WaitForCohortSnapshotAsync(guestClient!, guestModel, snapshot =>
                FindCohortActor(snapshot, guestEntityId) is { } player
                    && player.LevelId == baselinePortal.DestinationLevelId
                    && (player.PositionX, player.PositionY)
                        == (baselinePortal.DestinationPositionX, baselinePortal.DestinationPositionY)
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.Any(portal => portal.PortalId == portalId),
                guestSecondTownUse.AppliedTick,
                cancellationToken);
            _ = AssertTownPortalAtTick(guestSecondTownSnapshot, baselinePortal, baselinePortalTick, baselinePortalRemaining);
            await MoveActorToAdjacentOfPortalEndpointAsync(
                guestClient!,
                guestModel,
                guestEntityId,
                baselinePortal.DestinationLevelId,
                baselinePortal.DestinationPositionX,
                baselinePortal.DestinationPositionY,
                guestReceipts,
                globalReceipts,
                cancellationToken);
            var ownerHostOneFinal = await RefreshCohortModelAsync(
                ownerClient,
                ownerModel,
                guestModel.CurrentTick,
                cancellationToken);
            AssertPortalEndpointsUnoccupiedByOtherActor(
                ownerHostOneFinal, baselinePortal, ownerEntityId, guestEntityId);
            var ownerTownFinal = FindCohortActor(ownerHostOneFinal, ownerEntityId);
            var guestTownFinal = FindCohortActor(ownerHostOneFinal, guestEntityId);
            Assert.Equal(0U, ownerTownFinal.LevelId);
            Assert.Equal(0U, guestTownFinal.LevelId);
            Assert.Equal(PlayerCombatMode.Stand, ownerTownFinal.CombatMode);
            Assert.Null(ownerTownFinal.CurrentAction);
            Assert.True(ownerTownFinal.Life > 0, "The owner must remain alive in Town before the first checkpoint.");
            Assert.Equal(PlayerCombatMode.Stand, guestTownFinal.CombatMode);
            Assert.Null(guestTownFinal.CurrentAction);
            Assert.True(guestTownFinal.Life > 0, "The guest must remain alive in Town before the first checkpoint.");
            var preStopPortal = AssertTownPortalAtTick(
                ownerHostOneFinal,
                baselinePortal,
                baselinePortalTick,
                baselinePortalRemaining);
            Assert.True(preStopPortal.RemainingTicks > 0);
            ownerResumeToken = ownerClient.SessionToken;
            guestResumeToken = guestClient!.SessionToken;
            Assert.False(string.IsNullOrWhiteSpace(ownerResumeToken), "The owner session did not issue a resume token.");
            Assert.False(string.IsNullOrWhiteSpace(guestResumeToken), "The guest session did not issue a resume token.");

            await DisposeCohortClientAsync(ownerClient);
            ownerClient = null;
            await DisposeCohortClientAsync(guestClient);
            guestClient = null;
            firstServer.Dispose();
            Assert.True(firstServer.HasExited, "The first owned cohort host must stop before checkpoint inspection.");

            (firstCheckpoint, firstCheckpointState) = ReadCohortPortalCheckpoint(firstServer, contentHash, rulesetHash);
            AssertCohortCheckpointIdentity(firstCheckpoint, firstCheckpointState);
            AssertCohortCheckpointReceipts(
                firstCheckpoint,
                ownerEntityId,
                ownerReceipts,
                guestEntityId,
                guestReceipts,
                globalReceipts);
            AssertCohortCheckpointActor(
                firstCheckpointState,
                ownerEntityId,
                ownerName,
                0,
                0,
                ownerTownFinal.PositionX,
                ownerTownFinal.PositionY);
            AssertCohortCheckpointActor(
                firstCheckpointState,
                guestEntityId,
                guestName,
                1,
                0,
                guestTownFinal.PositionX,
                guestTownFinal.PositionY);
            Assert.True(
                firstCheckpointState.Participants.Single(participant => participant.EntityId == ownerEntityId).Player.Life > 0,
                "The owner must be alive in the stopped first cohort checkpoint.");
            Assert.True(
                firstCheckpointState.Participants.Single(participant => participant.EntityId == guestEntityId).Player.Life > 0,
                "The guest must be alive in the stopped first cohort checkpoint.");
            checkpointPortal = AssertCohortPortalAtTick(
                firstCheckpointState,
                baselinePortal,
                baselinePortalTick,
                baselinePortalRemaining,
                firstCheckpoint.Tick);
            Assert.Empty(Directory.EnumerateFiles(saveRoot, "*.session.json"));
            ownerLastSequenceAtCheckpoint = ownerReceipts[^1].ClientSequence;
            guestLastSequenceAtCheckpoint = guestReceipts[^1].ClientSequence;
            Assert.True(firstCheckpoint.NextServerReceiptSequence > globalReceipts[^1].ServerReceiptSequence);

            resumedServer = await StartCohortPortalServerAsync(
                cancellationToken,
                townLoopPreviewRoot,
                townPortalPreviewRoot,
                saveRoot,
                port);
            Assert.NotEqual(firstHostProcessId, checked((ulong)resumedServer.ProcessId));
            Assert.True(firstServer.HasExited);
            Assert.Equal(contentHash, resumedServer.ContentHash);
            Assert.Equal(rulesetHash, resumedServer.RulesetHash);

            resumedGuestClient = CreateCohortPortalClient(
                resumedServer,
                "cohort-portal-guest-resume-first",
                1,
                guestName,
                guestResumeToken);
            await resumedGuestClient.ConnectAsync(cancellationToken);
            var resumedGuestModel = new AuthoritativeClientModel();
            var guestFirstResumeMessage = await WaitForMessageAsync(
                resumedGuestClient,
                message => message.Snapshot is not null);
            resumedGuestModel.Apply(guestFirstResumeMessage);
            AssertClientCohortIdentity(resumedGuestClient, resumedServer, serverBuildId);
            AssertCohortResumeSnapshotIdentity(
                guestFirstResumeMessage.Snapshot!,
                contentHash,
                rulesetHash,
                ownerEntityId,
                guestEntityId);
            AssertCohortWireActors(
                guestFirstResumeMessage.Snapshot!,
                ownerEntityId,
                ownerName,
                0,
                guestEntityId,
                guestName,
                1,
                expectedOwnerLevel: 0,
                expectedOwnerX: ownerTownFinal.PositionX,
                expectedOwnerY: ownerTownFinal.PositionY,
                expectedGuestLevel: 0,
                expectedGuestX: guestTownFinal.PositionX,
                expectedGuestY: guestTownFinal.PositionY);
            _ = AssertTownPortalAtTick(
                guestFirstResumeMessage.Snapshot!,
                CohortPortalToWire(checkpointPortal),
                firstCheckpoint.Tick,
                checkpointPortal.RemainingTicks);

            resumedOwnerClient = CreateCohortPortalClient(
                resumedServer,
                "cohort-portal-owner-resume-second",
                0,
                ownerName,
                ownerResumeToken);
            await resumedOwnerClient.ConnectAsync(cancellationToken);
            var resumedOwnerModel = new AuthoritativeClientModel();
            var ownerFirstResumeMessage = await WaitForMessageAsync(
                resumedOwnerClient,
                message => message.Snapshot is not null);
            resumedOwnerModel.Apply(ownerFirstResumeMessage);
            AssertClientCohortIdentity(resumedOwnerClient, resumedServer, serverBuildId);
            AssertCohortResumeSnapshotIdentity(
                ownerFirstResumeMessage.Snapshot!,
                contentHash,
                rulesetHash,
                ownerEntityId,
                guestEntityId);
            AssertCohortWireActors(
                ownerFirstResumeMessage.Snapshot!,
                ownerEntityId,
                ownerName,
                0,
                guestEntityId,
                guestName,
                1,
                expectedOwnerLevel: 0,
                expectedOwnerX: ownerTownFinal.PositionX,
                expectedOwnerY: ownerTownFinal.PositionY,
                expectedGuestLevel: 0,
                expectedGuestX: guestTownFinal.PositionX,
                expectedGuestY: guestTownFinal.PositionY);
            _ = AssertTownPortalAtTick(
                ownerFirstResumeMessage.Snapshot!,
                CohortPortalToWire(checkpointPortal),
                firstCheckpoint.Tick,
                checkpointPortal.RemainingTicks);

            var restoredOwner = FindCohortActor(ownerFirstResumeMessage.Snapshot!, ownerEntityId);
            Assert.Equal(1, ChebyshevDistance(
                restoredOwner.PositionX,
                restoredOwner.PositionY,
                checkpointPortal.DestinationPositionX,
                checkpointPortal.DestinationPositionY));
            var firstResumeSequence = checked(ownerLastSequenceAtCheckpoint + 1);
            var ownerEndpointMove = await MoveActorToCellAsync(
                resumedOwnerClient,
                resumedOwnerModel,
                ownerEntityId,
                checkpointPortal.DestinationLevelId,
                checkpointPortal.DestinationPositionX,
                checkpointPortal.DestinationPositionY,
                ownerReceipts,
                globalReceipts,
                cancellationToken)
                ?? throw new InvalidOperationException("The restored owner was already on the portal endpoint.");
            Assert.Equal(firstResumeSequence, ownerEndpointMove.ClientSequence);
            Assert.Equal(firstCheckpoint.NextServerReceiptSequence, ownerEndpointMove.ServerReceiptSequence);
            var ownerAtTownEndpoint = FindCohortActor(resumedOwnerModel.Snapshot!, ownerEntityId);
            Assert.Equal((checkpointPortal.DestinationPositionX, checkpointPortal.DestinationPositionY),
                (ownerAtTownEndpoint.PositionX, ownerAtTownEndpoint.PositionY));

            await RefreshCohortModelAsync(
                resumedGuestClient,
                resumedGuestModel,
                ownerEndpointMove.AppliedTick,
                cancellationToken);
            var guestAfterOwnerMove = FindCohortActor(resumedGuestModel.Snapshot!, guestEntityId);
            var guestMoveTarget = SelectSafeAdjacentCell(
                resumedGuestModel.Snapshot!,
                guestAfterOwnerMove,
                guestAfterOwnerMove.PositionX,
                guestAfterOwnerMove.PositionY);
            var guestFirstResumeSequence = checked(guestLastSequenceAtCheckpoint + 1);
            var guestMoveAcknowledgement = await MoveActorToCellAsync(
                resumedGuestClient,
                resumedGuestModel,
                guestEntityId,
                guestAfterOwnerMove.LevelId,
                guestMoveTarget.X,
                guestMoveTarget.Y,
                guestReceipts,
                globalReceipts,
                cancellationToken)
                ?? throw new InvalidOperationException("The restored guest movement did not submit a command.");
            Assert.Equal(guestFirstResumeSequence, guestMoveAcknowledgement.ClientSequence);
            Assert.Equal(checked(ownerEndpointMove.ServerReceiptSequence + 1),
                guestMoveAcknowledgement.ServerReceiptSequence);
            var guestMovedSnapshot = resumedGuestModel.Snapshot!;
            var guestMovedPlayer = FindCohortActor(guestMovedSnapshot, guestEntityId);
            Assert.Equal(guestMoveTarget, (guestMovedPlayer.PositionX, guestMovedPlayer.PositionY));
            Assert.Equal(PlayerCombatMode.Stand, guestMovedPlayer.CombatMode);
            Assert.Null(guestMovedPlayer.CurrentAction);

            await RefreshCohortModelAsync(
                resumedOwnerClient,
                resumedOwnerModel,
                guestMoveAcknowledgement.AppliedTick,
                cancellationToken);
            AssertPortalEndpointUnoccupiedByOtherActor(
                resumedOwnerModel.Snapshot!,
                checkpointPortal.SourceLevelId,
                checkpointPortal.SourcePositionX,
                checkpointPortal.SourcePositionY,
                ownerEntityId);
            _ = AssertCohortPortalUseActor(
                resumedOwnerModel.Snapshot!,
                ownerEntityId,
                checkpointPortal.DestinationLevelId,
                checkpointPortal.DestinationPositionX,
                checkpointPortal.DestinationPositionY);
            var ownerPortalReturn = await SubmitCohortCommandAsync(
                resumedOwnerClient,
                resumedOwnerModel,
                new Command { UsePortalRequested = new UsePortalRequested { PortalId = portalId } },
                ownerReceipts,
                globalReceipts,
                cancellationToken,
                ownerEntityId);
            Assert.Equal(checked(ownerLastSequenceAtCheckpoint + 2), ownerPortalReturn.ClientSequence);
            Assert.Equal(checked(guestMoveAcknowledgement.ServerReceiptSequence + 1),
                ownerPortalReturn.ServerReceiptSequence);
            var ownerReturnedSnapshot = await WaitForCohortSnapshotAsync(resumedOwnerClient, resumedOwnerModel, snapshot =>
                FindCohortActor(snapshot, ownerEntityId) is { } player
                    && player.LevelId == checkpointPortal.SourceLevelId
                    && (player.PositionX, player.PositionY)
                        == (checkpointPortal.SourcePositionX, checkpointPortal.SourcePositionY)
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.All(portal => portal.PortalId != portalId
                        && portal.OwnerEntityId != ownerEntityId),
                ownerPortalReturn.AppliedTick,
                cancellationToken);
            Assert.DoesNotContain(ownerReturnedSnapshot.Portals, portal => portal.PortalId == portalId);
            Assert.DoesNotContain(ownerReturnedSnapshot.Portals, portal => portal.OwnerEntityId == ownerEntityId);
            AssertCohortWireActorsSettled(ownerReturnedSnapshot, ownerEntityId, guestEntityId);

            ownerResumeToken = resumedOwnerClient.SessionToken;
            guestResumeToken = resumedGuestClient.SessionToken;
            Assert.False(string.IsNullOrWhiteSpace(ownerResumeToken), "The resumed owner session lost its resume token.");
            Assert.False(string.IsNullOrWhiteSpace(guestResumeToken), "The resumed guest session lost its resume token.");
            await DisposeCohortClientAsync(resumedGuestClient);
            resumedGuestClient = null;
            await DisposeCohortClientAsync(resumedOwnerClient);
            resumedOwnerClient = null;
            resumedServer.Dispose();
            Assert.True(resumedServer.HasExited, "The resumed owned cohort host must stop before checkpoint inspection.");

            var finalCheckpointAndState = ReadCohortPortalCheckpoint(resumedServer, contentHash, rulesetHash);
            var finalCheckpoint = finalCheckpointAndState.Document;
            var finalCheckpointState = finalCheckpointAndState.State;
            AssertCohortCheckpointIdentity(finalCheckpoint, finalCheckpointState);
            AssertCohortCheckpointReceipts(
                finalCheckpoint,
                ownerEntityId,
                ownerReceipts,
                guestEntityId,
                guestReceipts,
                globalReceipts);
            AssertCohortCheckpointActor(
                finalCheckpointState,
                ownerEntityId,
                ownerName,
                0,
                checkpointPortal.SourceLevelId,
                checkpointPortal.SourcePositionX,
                checkpointPortal.SourcePositionY);
            AssertCohortCheckpointActor(
                finalCheckpointState,
                guestEntityId,
                guestName,
                1,
                guestMovedPlayer.LevelId,
                guestMovedPlayer.PositionX,
                guestMovedPlayer.PositionY);
            Assert.DoesNotContain(finalCheckpointState.Portals, portal => portal.PortalId == portalId);
            Assert.DoesNotContain(finalCheckpointState.Portals, portal => portal.OwnerEntityId == ownerEntityId);
            var finalOwnerLedger = finalCheckpoint.Participants
                .Single(participant => participant.EntityId == ownerEntityId)
                .CommandReceipts;
            Assert.NotEmpty(finalOwnerLedger);
            Assert.Equal(ownerPortalReturn.ClientSequence,
                finalOwnerLedger.Max(receipt => receipt.ClientSequence));
            Assert.True(finalCheckpoint.Tick >= ownerPortalReturn.AppliedTick);
            Assert.Empty(Directory.EnumerateFiles(saveRoot, "*.session.json"));
        } catch (Exception failure) {
            primaryFailure = failure;
            throw;
        } finally {
            if (resumedGuestClient is not null) {
                try { await resumedGuestClient.DisposeAsync(); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
            }
            if (resumedOwnerClient is not null) {
                try { await resumedOwnerClient.DisposeAsync(); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
            }
            if (guestClient is not null) {
                try { await guestClient.DisposeAsync(); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
            }
            if (ownerClient is not null) {
                try { await ownerClient.DisposeAsync(); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
            }
            try { resumedServer?.Dispose(); }
            catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
            try { firstServer?.Dispose(); }
            catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
            if (primaryFailure is null) {
                Assert.Empty(cleanupFailures);
                CleanupLocalRoot(saveRootLease);
            }
        }
    }

    private static async Task<StartedServer> StartCohortPortalServerAsync(
        CancellationToken cancellationToken,
        string townLoopPreviewRoot,
        string townPortalPreviewRoot,
        string saveRoot,
        int? port = null)
        => await StartedServer.StartAsync(
            cancellationToken,
            contentOverlayRoot: townLoopPreviewRoot,
            additionalContentOverlayRoot: townPortalPreviewRoot,
            startingLevelOverride: 0,
            randomSeed: 1,
            gameMode: "multiplayer",
            portOverride: port,
            saveRootOverride: saveRoot,
            deleteSaveRootOnDispose: false,
            commandSchedulingMode: CommandSchedulingMode.ClockGated,
            persistenceMode: "cohort");

    private static AuthoritativeClient CreateCohortPortalClient(
        StartedServer server,
        string clientBuildId,
        uint classId,
        string characterName,
        string? resumeToken = null)
        => new(CreateOptions(server, clientBuildId, classId) with {
            RequestedCharacterName = characterName,
            ResumeToken = resumeToken ?? string.Empty,
        });

    private static void AssertClientCohortIdentity(
        AuthoritativeClient client,
        StartedServer server,
        string? expectedBuildId)
    {
        var identity = client.ServerIdentity
            ?? throw new InvalidOperationException("The cohort client did not accept a server identity.");
        Assert.Equal(server.ContentHash, identity.ContentManifestHash);
        Assert.Equal(server.RulesetHash, identity.RulesetIdentityHash);
        Assert.Equal(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity, identity.ProtocolSchemaVersion);
        Assert.Equal(20U, identity.TickRateHz);
        Assert.Equal(GameMode.Multiplayer, identity.GameMode);
        Assert.Equal(GameMode.Multiplayer, client.ServerGameMode);
        Assert.True(client.IsClockGatedCommandScheduling);
        if (expectedBuildId is not null)
            Assert.Equal(expectedBuildId, identity.BuildId);
    }

    private static async Task<CommandResult> SubmitCohortCommandAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        Command command,
        List<CommandResult> participantReceipts,
        List<CommandResult> globalReceipts,
        CancellationToken cancellationToken,
        uint? rejectionDiagnosticEntityId = null)
    {
        var sequence = client.Queue(command, client.SuggestedCommandTick(model.CurrentTick));
        var expectedSequence = participantReceipts.Count == 0
            ? 1UL
            : checked(participantReceipts[^1].ClientSequence + 1);
        Assert.Equal(expectedSequence, sequence);
        await client.PollAsync(cancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        if (rejectionDiagnosticEntityId is uint diagnosticEntityId
            && (acknowledgement.Status != CommandStatus.Accepted
                || acknowledgement.RejectReason != CommandRejectReason.Unspecified)) {
            var actorDiagnostic = "preCommandActor=unavailable";
            if (model.Snapshot is { } snapshot
                && snapshot.Players.SingleOrDefault(player => player.EntityId == diagnosticEntityId) is { } actor) {
                actorDiagnostic = $"preCommandActorLevel={actor.LevelId},preCommandActorX={actor.PositionX},preCommandActorY={actor.PositionY},preCommandActorLife={actor.Life},preCommandActorMode={(int)actor.CombatMode},preCommandActorHasAction={actor.CurrentAction is not null}";
            }
            throw new InvalidOperationException(
                $"Cohort command rejected: status={(int)acknowledgement.Status},reason={(int)acknowledgement.RejectReason},clientSequence={acknowledgement.ClientSequence},serverReceiptSequence={acknowledgement.ServerReceiptSequence},appliedTick={acknowledgement.AppliedTick},requestedTick={command.RequestedTick},{actorDiagnostic}.");
        }
        AssertTownPortalAcceptedReceipt(acknowledgement, sequence);
        if (participantReceipts.Count > 0)
            Assert.True(acknowledgement.ServerReceiptSequence > participantReceipts[^1].ServerReceiptSequence);
        if (globalReceipts.Count > 0) {
            Assert.Equal(checked(globalReceipts[^1].ServerReceiptSequence + 1), acknowledgement.ServerReceiptSequence);
            Assert.True(acknowledgement.AppliedTick >= globalReceipts[^1].AppliedTick);
        }
        participantReceipts.Add(acknowledgement);
        globalReceipts.Add(acknowledgement);
        return acknowledgement;
    }

    private static async Task<Snapshot> WaitForCohortSnapshotAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        Func<Snapshot, bool> predicate,
        ulong minimumTick,
        CancellationToken cancellationToken)
    {
        var message = await WaitForMessageAsync(client, candidate =>
            candidate.Snapshot is { } snapshot
            && snapshot.Tick >= minimumTick
            && predicate(snapshot));
        cancellationToken.ThrowIfCancellationRequested();
        model.Apply(message);
        return model.Snapshot!;
    }

    private static async Task<Snapshot> RefreshCohortModelAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        ulong minimumTick,
        CancellationToken cancellationToken)
    {
        var currentTick = model.Snapshot?.Tick ?? 0;
        var requiredTick = Math.Max(currentTick, minimumTick);
        return await WaitForCohortSnapshotAsync(
            client,
            model,
            snapshot => snapshot.Tick > requiredTick,
            checked(requiredTick + 1),
            cancellationToken);
    }

    private static async Task MoveActorNearTownerAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        uint entityId,
        int townerId,
        List<CommandResult> participantReceipts,
        List<CommandResult> globalReceipts,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 160; attempt++) {
            var snapshot = model.Snapshot
                ?? throw new InvalidOperationException("The cohort has no live snapshot before town navigation.");
            var player = FindCohortActor(snapshot, entityId);
            var greeting = snapshot.TownerGreetings.Single(item =>
                item.TownerId == townerId && item.LevelId == player.LevelId);
            if (ChebyshevDistance(player.PositionX, player.PositionY, greeting.PositionX, greeting.PositionY) <= 1
                && player.CombatMode == PlayerCombatMode.Stand
                && player.CurrentAction is null)
                return;

            var priorTick = snapshot.Tick;
            var priorPosition = (player.PositionX, player.PositionY);
            var acknowledgement = await SubmitCohortCommandAsync(
                client,
                model,
                new Command {
                    MoveRequested = new MoveRequested {
                        TargetCell = true,
                        TargetX = greeting.PositionX,
                        TargetY = greeting.PositionY,
                    },
                },
                participantReceipts,
                globalReceipts,
                cancellationToken);
            _ = await WaitForCohortSnapshotAsync(client, model, current =>
                FindCohortActor(current, entityId) is { } moved
                    && moved.LevelId == greeting.LevelId
                    && (moved.PositionX, moved.PositionY) != priorPosition
                    && moved.CombatMode == PlayerCombatMode.Stand
                    && moved.CurrentAction is null,
                Math.Max(priorTick + 1, acknowledgement.AppliedTick),
                cancellationToken);
        }
        throw new TimeoutException("The cohort actor could not reach the Smith's ordinary interaction range.");
    }

    private static async Task MoveActorToWarpAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        uint entityId,
        string warpSymbol,
        List<CommandResult> participantReceipts,
        List<CommandResult> globalReceipts,
        CancellationToken cancellationToken)
    {
        var initialPlayer = FindCohortActor(model.Snapshot!, entityId);
        var sourceLevel = initialPlayer.LevelId;
        var warp = FindCohortWarp(model.Snapshot!, sourceLevel, warpSymbol);
        Assert.True(warp.Available);
        for (var attempt = 0; attempt < 240; attempt++) {
            var snapshot = model.Snapshot!;
            var player = FindCohortActor(snapshot, entityId);
            if (player.LevelId == warp.TargetLevelId
                && player.CombatMode == PlayerCombatMode.Stand
                && player.CurrentAction is null)
                return;
            Assert.Equal(sourceLevel, player.LevelId);
            var priorTick = snapshot.Tick;
            var priorPosition = (player.PositionX, player.PositionY);
            var acknowledgement = await SubmitCohortCommandAsync(
                client,
                model,
                new Command {
                    MoveRequested = new MoveRequested {
                        TargetCell = true,
                        TargetX = warp.TriggerX,
                        TargetY = warp.TriggerY,
                    },
                },
                participantReceipts,
                globalReceipts,
                cancellationToken);
            _ = await WaitForCohortSnapshotAsync(client, model, current =>
                FindCohortActor(current, entityId) is { } moved
                    && moved.CombatMode == PlayerCombatMode.Stand
                    && moved.CurrentAction is null
                    && (moved.LevelId == warp.TargetLevelId
                        || (moved.LevelId == sourceLevel
                            && (moved.PositionX, moved.PositionY) != priorPosition)),
                Math.Max(priorTick + 1, acknowledgement.AppliedTick),
                cancellationToken);
        }
        throw new TimeoutException($"The cohort actor could not reach warp '{warpSymbol}'.");
    }

    private static async Task<CommandResult?> MoveActorToCellAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        uint entityId,
        uint levelId,
        int targetX,
        int targetY,
        List<CommandResult> participantReceipts,
        List<CommandResult> globalReceipts,
        CancellationToken cancellationToken)
    {
        CommandResult? finalAcknowledgement = null;
        for (var attempt = 0; attempt < 240; attempt++) {
            var snapshot = model.Snapshot
                ?? throw new InvalidOperationException("The cohort has no live snapshot before movement.");
            var player = FindCohortActor(snapshot, entityId);
            Assert.Equal(levelId, player.LevelId);
            if ((player.PositionX, player.PositionY) == (targetX, targetY)) {
                Assert.Equal(PlayerCombatMode.Stand, player.CombatMode);
                Assert.Null(player.CurrentAction);
                return finalAcknowledgement;
            }

            var priorTick = snapshot.Tick;
            var priorPosition = (player.PositionX, player.PositionY);
            finalAcknowledgement = await SubmitCohortCommandAsync(
                client,
                model,
                new Command {
                    MoveRequested = new MoveRequested {
                        TargetCell = true,
                        TargetX = targetX,
                        TargetY = targetY,
                    },
                },
                participantReceipts,
                globalReceipts,
                cancellationToken);
            var acknowledgement = finalAcknowledgement;
            _ = await WaitForCohortSnapshotAsync(client, model, current =>
                FindCohortActor(current, entityId) is { } moved
                    && moved.LevelId == levelId
                    && (moved.PositionX, moved.PositionY) != priorPosition
                    && moved.CombatMode == PlayerCombatMode.Stand
                    && moved.CurrentAction is null,
                Math.Max(priorTick + 1, acknowledgement.AppliedTick),
                cancellationToken);
            var currentPlayer = FindCohortActor(model.Snapshot!, entityId);
            if ((currentPlayer.PositionX, currentPlayer.PositionY) == (targetX, targetY))
                return finalAcknowledgement;
        }
        throw new TimeoutException("The cohort actor could not reach the requested ordinary movement target.");
    }

    private static async Task MoveActorToAdjacentOfPortalEndpointAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        uint entityId,
        uint levelId,
        int endpointX,
        int endpointY,
        List<CommandResult> participantReceipts,
        List<CommandResult> globalReceipts,
        CancellationToken cancellationToken)
    {
        var snapshot = model.Snapshot!;
        var player = FindCohortActor(snapshot, entityId);
        Assert.Equal(levelId, player.LevelId);
        var target = SelectSafeAdjacentCell(snapshot, player, endpointX, endpointY);
        Assert.NotEqual((player.PositionX, player.PositionY), target);
        var acknowledgement = await MoveActorToCellAsync(
            client,
            model,
            entityId,
            levelId,
            target.X,
            target.Y,
            participantReceipts,
            globalReceipts,
            cancellationToken)
            ?? throw new InvalidOperationException("The actor was already at the selected adjacent portal cell.");
        Assert.Equal(acknowledgement, participantReceipts[^1]);
        var moved = FindCohortActor(model.Snapshot!, entityId);
        Assert.Equal((target.X, target.Y), (moved.PositionX, moved.PositionY));
        Assert.Equal(1, ChebyshevDistance(moved.PositionX, moved.PositionY, endpointX, endpointY));
        Assert.NotEqual((endpointX, endpointY), (moved.PositionX, moved.PositionY));
    }

    private static (int X, int Y) SelectSafeAdjacentCell(
        Snapshot snapshot,
        PlayerSnapshot player,
        int anchorX,
        int anchorY)
    {
        Assert.All(snapshot.Players, other => {
            Assert.Equal(PlayerCombatMode.Stand, other.CombatMode);
            Assert.Null(other.CurrentAction);
        });
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == player.LevelId);
        var offsets = new (int X, int Y)[] {
            (0, -1), (1, 0), (0, 1), (-1, 0),
            (1, -1), (1, 1), (-1, 1), (-1, -1),
        };
        foreach (var offset in offsets) {
            var cell = (X: anchorX + offset.X, Y: anchorY + offset.Y);
            if ((cell.X, cell.Y) == (player.PositionX, player.PositionY)
                || cell.X < 0 || cell.Y < 0 || cell.X >= geometry.Width || cell.Y >= geometry.Height
                || AuthoritativeLevelGeometryCellResolver.Resolve(geometry, cell.X, cell.Y)
                    == AuthoritativeLevelGeometryCell.Blocked)
                continue;
            var index = checked(cell.Y * geometry.Width + cell.X);
            if (geometry.StaticObjectBlockedCells.Contains(index)
                || geometry.WarpTriggers.Any(warp => warp.TriggerX == cell.X && warp.TriggerY == cell.Y)
                || snapshot.Players.Any(other => other.EntityId != player.EntityId
                    && other.Life > 0 && other.LevelId == player.LevelId
                    && (other.PositionX, other.PositionY) == cell)
                || snapshot.Monsters.Any(monster => monster.Alive && monster.LevelId == player.LevelId
                    && ((monster.PositionX, monster.PositionY) == cell
                        || (monster.Moving && (monster.FuturePositionX, monster.FuturePositionY) == cell)))
                || snapshot.Objects.Any(@object => @object.LevelId == player.LevelId
                    && !@object.Destroyed && (@object.PositionX, @object.PositionY) == cell)
                || snapshot.WorldItems.Any(item => item.LevelId == player.LevelId
                    && (item.PositionX, item.PositionY) == cell)
                || snapshot.TownerGreetings.Any(greeting => greeting.LevelId == player.LevelId
                    && (greeting.PositionX, greeting.PositionY) == cell)
                || snapshot.Portals.Any(portal => (portal.SourceLevelId == player.LevelId
                        && (portal.SourcePositionX, portal.SourcePositionY) == cell)
                    || (portal.DestinationLevelId == player.LevelId
                        && (portal.DestinationPositionX, portal.DestinationPositionY) == cell)))
                continue;
            return cell;
        }
        throw new InvalidOperationException("No safe ordinary neighboring cell is available to vacate the portal endpoint.");
    }

    private static WarpTriggerSnapshot FindCohortWarp(Snapshot snapshot, uint levelId, string stableSymbol)
        => snapshot.LevelGeometry
            .Single(level => level.LevelId == levelId)
            .WarpTriggers
            .Single(warp => warp.StableSymbol == stableSymbol && warp.Available);

    private static PlayerSnapshot FindCohortActor(Snapshot snapshot, uint entityId)
        => snapshot.Players.Single(player => player.EntityId == entityId);

    private static void AssertCohortResumeSnapshotIdentity(
        Snapshot snapshot,
        string contentHash,
        string rulesetHash,
        uint ownerEntityId,
        uint guestEntityId)
    {
        Assert.Equal(contentHash, snapshot.ContentManifestHash);
        Assert.Equal(rulesetHash, snapshot.RulesetIdentityHash);
        AssertCohortWireActorsSettled(snapshot, ownerEntityId, guestEntityId);
    }

    private static void AssertCohortWireActorsSettled(
        Snapshot snapshot,
        uint ownerEntityId,
        uint guestEntityId)
    {
        _ = AssertCohortActorAliveAndSettled(snapshot, ownerEntityId);
        _ = AssertCohortActorAliveAndSettled(snapshot, guestEntityId);
    }

    private static PlayerSnapshot AssertCohortPortalUseActor(
        Snapshot snapshot,
        uint entityId,
        uint expectedLevelId,
        int expectedX,
        int expectedY)
    {
        var actor = AssertCohortActorAliveAndSettled(snapshot, entityId);
        Assert.Equal(expectedLevelId, actor.LevelId);
        Assert.Equal((expectedX, expectedY), (actor.PositionX, actor.PositionY));
        return actor;
    }

    private static PlayerSnapshot AssertCohortActorAliveAndSettled(Snapshot snapshot, uint entityId)
    {
        var actor = FindCohortActor(snapshot, entityId);
        Assert.True(
            actor.Life > 0,
            $"Actor {entityId} must be alive before portal use or resumed input (Life={actor.Life}, Mode={(int)actor.CombatMode}, Level={actor.LevelId}, X={actor.PositionX}, Y={actor.PositionY}).");
        Assert.Equal(PlayerCombatMode.Stand, actor.CombatMode);
        Assert.Null(actor.CurrentAction);
        return actor;
    }

    private static void AssertCohortWireActors(
        Snapshot snapshot,
        uint ownerEntityId,
        string ownerName,
        uint ownerClass,
        uint guestEntityId,
        string guestName,
        uint guestClass,
        uint? expectedOwnerLevel = null,
        int? expectedOwnerX = null,
        int? expectedOwnerY = null,
        uint? expectedGuestLevel = null,
        int? expectedGuestX = null,
        int? expectedGuestY = null)
    {
        Assert.Equal(2, snapshot.Players.Count);
        Assert.Equal(2, snapshot.Players.Select(player => player.EntityId).Distinct().Count());
        Assert.NotEqual(0U, ownerEntityId);
        Assert.NotEqual(0U, guestEntityId);
        Assert.NotEqual(ownerEntityId, guestEntityId);
        var owner = FindCohortActor(snapshot, ownerEntityId);
        var guest = FindCohortActor(snapshot, guestEntityId);
        Assert.Equal(ownerName, owner.CharacterName);
        Assert.Equal(ownerClass, owner.CharacterClassId);
        Assert.Equal(guestName, guest.CharacterName);
        Assert.Equal(guestClass, guest.CharacterClassId);
        if (expectedOwnerLevel is uint ownerLevel)
            Assert.Equal(ownerLevel, owner.LevelId);
        if (expectedOwnerX is int ownerX)
            Assert.Equal(ownerX, owner.PositionX);
        if (expectedOwnerY is int ownerY)
            Assert.Equal(ownerY, owner.PositionY);
        if (expectedGuestLevel is uint guestLevel)
            Assert.Equal(guestLevel, guest.LevelId);
        if (expectedGuestX is int guestX)
            Assert.Equal(guestX, guest.PositionX);
        if (expectedGuestY is int guestY)
            Assert.Equal(guestY, guest.PositionY);
    }

    private static void AssertPortalEndpointUnoccupiedByOtherActor(
        Snapshot snapshot,
        uint levelId,
        int x,
        int y,
        uint travelingEntityId)
    {
        Assert.DoesNotContain(snapshot.Players, player => player.EntityId != travelingEntityId
            && player.Life > 0
            && (player.LevelId, player.PositionX, player.PositionY) == (levelId, x, y));
    }

    private static void AssertPortalEndpointsUnoccupiedByOtherActor(
        Snapshot snapshot,
        PortalSnapshot portal,
        uint ownerEntityId,
        uint guestEntityId)
    {
        Assert.DoesNotContain(snapshot.Players, player => player.Life > 0
            && (player.LevelId, player.PositionX, player.PositionY)
                == (portal.SourceLevelId, portal.SourcePositionX, portal.SourcePositionY));
        Assert.DoesNotContain(snapshot.Players, player => player.Life > 0
            && (player.LevelId, player.PositionX, player.PositionY)
                == (portal.DestinationLevelId, portal.DestinationPositionX, portal.DestinationPositionY));
        Assert.Contains(snapshot.Players, player => player.EntityId == ownerEntityId);
        Assert.Contains(snapshot.Players, player => player.EntityId == guestEntityId);
    }

    private static (AuthoritativeCohortDocument Document, DiabloCohortState State) ReadCohortPortalCheckpoint(
        StartedServer server,
        string contentHash,
        string rulesetHash)
    {
        if (!server.HasExited)
            throw new InvalidOperationException("Cohort checkpoint inspection requires confirmed owned-host exit.");
        using var store = new AuthoritativeCohortStore(server.SaveRoot);
        var document = store.Load()
            ?? throw new InvalidDataException("The stopped cohort host has no complete checkpoint.");
        Assert.Equal(contentHash, document.Identity.ContentManifestHash);
        Assert.Equal(rulesetHash, document.Identity.RulesetIdentityHash);
        var state = new DiabloCohortCodec().Deserialize(document.SerializedModuleState);
        return (document, state);
    }

    private static void AssertCohortCheckpointIdentity(
        AuthoritativeCohortDocument document,
        DiabloCohortState state)
    {
        Assert.True(document.Generation > 0);
        Assert.True(document.NextEntityId > 0);
        Assert.True(document.NextServerReceiptSequence > 0);
        Assert.Equal(AuthoritativeGameMode.Multiplayer, document.Identity.GameMode);
        Assert.Equal(20U, document.Identity.TickRateHz);
        Assert.Equal(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            document.Identity.ProtocolSchemaVersion);
        Assert.True(document.Identity.ModuleFormatVersion > 0);
        Assert.False(string.IsNullOrWhiteSpace(document.Identity.ModuleId));
        Assert.Equal(document.Tick, state.Tick);
        Assert.True(state.LastPortalTick.HasValue,
            "The persisted cohort module must retain its last portal-aging tick.");
        Assert.Equal(document.Tick, state.LastPortalTick.Value);
        Assert.Equal(2, document.Participants.Count);
        Assert.Equal(2, state.Participants.Count);
        Assert.Equal(2, document.Participants.Select(participant => participant.EntityId).Distinct().Count());
        Assert.Equal(2, state.Participants.Select(participant => participant.EntityId).Distinct().Count());
        Assert.Equal(
            document.Participants.Select(participant => participant.EntityId).Order().ToArray(),
            state.Participants.Select(participant => participant.EntityId).Order().ToArray());
        foreach (var moduleParticipant in state.Participants) {
            var envelopeParticipant = document.Participants.Single(
                participant => participant.EntityId == moduleParticipant.EntityId);
            Assert.True(
                string.Equals(envelopeParticipant.SessionId, moduleParticipant.SessionId, StringComparison.Ordinal),
                "The cohort envelope and module participant identity must match.");
        }
    }

    private static void AssertCohortCheckpointReceipts(
        AuthoritativeCohortDocument document,
        uint ownerEntityId,
        IReadOnlyList<CommandResult> ownerReceipts,
        uint guestEntityId,
        IReadOnlyList<CommandResult> guestReceipts,
        IReadOnlyList<CommandResult> globalReceipts)
    {
        var ownerParticipant = document.Participants.Single(participant => participant.EntityId == ownerEntityId);
        var guestParticipant = document.Participants.Single(participant => participant.EntityId == guestEntityId);
        AssertReceiptLedger(ownerParticipant.CommandReceipts, ownerReceipts);
        AssertReceiptLedger(guestParticipant.CommandReceipts, guestReceipts);
        var persistedGlobal = document.Participants
            .SelectMany(participant => participant.CommandReceipts)
            .OrderBy(receipt => receipt.ServerReceiptSequence)
            .ToArray();
        Assert.Equal(globalReceipts.Count, persistedGlobal.Length);
        Assert.Equal(globalReceipts.Count, persistedGlobal.Select(receipt => receipt.ServerReceiptSequence).Distinct().Count());
        for (var index = 0; index < globalReceipts.Count; index++) {
            Assert.Equal(globalReceipts[index], persistedGlobal[index]);
            if (index > 0) {
                Assert.Equal(checked(persistedGlobal[index - 1].ServerReceiptSequence + 1),
                    persistedGlobal[index].ServerReceiptSequence);
                Assert.True(persistedGlobal[index].AppliedTick >= persistedGlobal[index - 1].AppliedTick);
            }
        }
        Assert.Equal(checked(persistedGlobal[^1].ServerReceiptSequence + 1), document.NextServerReceiptSequence);
        Assert.Equal(globalReceipts.Max(receipt => receipt.ServerReceiptSequence) + 1,
            document.NextServerReceiptSequence);
    }

    private static void AssertReceiptLedger(
        IReadOnlyList<CommandResult> persisted,
        IReadOnlyList<CommandResult> expected)
    {
        Assert.Equal(expected.Count, persisted.Count);
        var orderedExpected = expected.OrderBy(receipt => receipt.ClientSequence).ToArray();
        var orderedPersisted = persisted.OrderBy(receipt => receipt.ClientSequence).ToArray();
        for (var index = 0; index < orderedExpected.Length; index++) {
            Assert.Equal(orderedExpected[index], orderedPersisted[index]);
            Assert.Equal((ulong)index + 1, orderedPersisted[index].ClientSequence);
        }
    }

    private static void AssertCohortCheckpointActor(
        DiabloCohortState state,
        uint entityId,
        string characterName,
        uint classId,
        uint levelId,
        int positionX,
        int positionY)
    {
        var participant = state.Participants.Single(value => value.EntityId == entityId);
        var player = participant.Player;
        Assert.Equal(entityId, player.EntityId);
        Assert.Equal(characterName, player.CharacterName);
        Assert.Equal(classId, player.CharacterClassId);
        Assert.Equal(levelId, player.LevelId);
        Assert.Equal((positionX, positionY), (player.PositionX, player.PositionY));
        Assert.True(player.Life > 0, "A stopped cohort actor must be alive.");
        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);
        Assert.Null(player.ActiveStoreId);
    }

    private static DiabloCohortPortalState AssertCohortPortalAtTick(
        DiabloCohortState state,
        PortalSnapshot expectedWirePortal,
        ulong baselineTick,
        uint baselineRemaining,
        ulong currentTick)
    {
        Assert.True(baselineRemaining > 0);
        Assert.True(currentTick >= baselineTick);
        var elapsed = currentTick - baselineTick;
        var expectedRemaining = elapsed >= (ulong)(baselineRemaining - 1)
            ? 1U
            : baselineRemaining - checked((uint)elapsed);
        var portal = Assert.Single(state.Portals, candidate => candidate.PortalId == expectedWirePortal.PortalId);
        Assert.Equal(expectedWirePortal.PortalId, portal.PortalId);
        Assert.Equal(expectedWirePortal.SourceLevelId, portal.SourceLevelId);
        Assert.Equal(expectedWirePortal.SourcePositionX, portal.SourcePositionX);
        Assert.Equal(expectedWirePortal.SourcePositionY, portal.SourcePositionY);
        Assert.Equal(expectedWirePortal.DestinationLevelId, portal.DestinationLevelId);
        Assert.Equal(expectedWirePortal.DestinationPositionX, portal.DestinationPositionX);
        Assert.Equal(expectedWirePortal.DestinationPositionY, portal.DestinationPositionY);
        Assert.Equal(expectedWirePortal.OwnerEntityId, portal.OwnerEntityId);
        Assert.Equal(expectedWirePortal.Bidirectional, portal.Bidirectional);
        Assert.Equal(expectedRemaining, portal.RemainingTicks);
        Assert.True(portal.RemainingTicks > 0);
        return portal;
    }

    private static PortalSnapshot CohortPortalToWire(DiabloCohortPortalState portal)
        => new() {
            PortalId = portal.PortalId,
            SourceLevelId = portal.SourceLevelId,
            SourcePositionX = portal.SourcePositionX,
            SourcePositionY = portal.SourcePositionY,
            DestinationLevelId = portal.DestinationLevelId,
            DestinationPositionX = portal.DestinationPositionX,
            DestinationPositionY = portal.DestinationPositionY,
            RemainingTicks = portal.RemainingTicks,
            OwnerEntityId = portal.OwnerEntityId,
            Bidirectional = portal.Bidirectional,
        };

    private static int ChebyshevDistance(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    private static async Task DisposeCohortClientAsync(AuthoritativeClient client)
        => await client.DisposeAsync();
}
