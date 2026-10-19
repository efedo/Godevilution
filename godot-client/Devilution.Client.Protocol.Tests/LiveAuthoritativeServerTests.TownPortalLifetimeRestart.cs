using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{
    [Fact]
    public async Task LiveServerRestoresTownPortalLifetimeAfterProcessRestart()
    {
        const string characterName = "M1-Town-Portal-Lifetime";
        const uint scrollSeed = 9100;
        const uint scrollSpellId = 39;
        const uint scrollStoreId = 1;
        const uint scrollStoreSlot = 2;
        const uint scrollPrice = 50;
        var cancellationToken = TestContext.Current.CancellationToken;
        var repositoryRoot = FindRepositoryRoot();
        var townLoopPreviewRoot = Path.Combine(repositoryRoot, "server", "content", "town-loop-preview");
        var townPortalPreviewRoot = Path.Combine(repositoryRoot, "server", "content", "town-portal-preview");
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Town-Portal-Lifetime-" + Guid.NewGuid().ToString("N"));
        var saveRootLease = LiveTestSaveRoot.Register(saveRoot);
        StartedServer? firstServer = null;
        StartedServer? restartedServer = null;
        StartedServer? returnRestartedServer = null;
        AuthoritativeClient? firstClient = null;
        AuthoritativeClient? resumedClient = null;
        AuthoritativeClient? returnResumedClient = null;
        Exception? primaryFailure = null;

        try {
            string resumeToken;
            string contentHash;
            string rulesetHash;
            uint playerEntityId;
            uint sourceLevelId;
            uint portalId;
            ulong baselineTick;
            uint baselineRemaining;
            PortalSnapshot baselinePortal;
            CommandResult purchaseAcknowledgement;
            CommandResult castAcknowledgement;
            CommandResult finalPortalApproachAcknowledgement;
            AuthoritativeSessionDocument checkpoint;
            Snapshot checkpointSnapshot;
            PortalSnapshot checkpointPortal;

            firstServer = await StartedServer.StartAsync(
                cancellationToken,
                contentOverlayRoot: townLoopPreviewRoot,
                additionalContentOverlayRoot: townPortalPreviewRoot,
                randomSeed: 1,
                startingLevelOverride: 0,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false);
            var firstProcessId = firstServer.ProcessId;
            var firstPort = firstServer.Port;
            contentHash = firstServer.ContentHash;
            rulesetHash = firstServer.RulesetHash;
            firstClient = new AuthoritativeClient(
                CreateOptions(firstServer, "godot-town-portal-lifetime-first", requestedClassId: 0) with {
                    RequestedCharacterName = characterName,
                });

            await firstClient.ConnectAsync(cancellationToken);
            var initialMessage = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initialMessage);
            var initialSnapshot = initialMessage.Snapshot!;
            var initialPlayer = Assert.Single(initialSnapshot.Players);
            Assert.Equal(0U, initialPlayer.CharacterClassId);
            Assert.Equal(characterName, initialPlayer.CharacterName);
            Assert.Equal(0U, initialPlayer.LevelId);
            Assert.True(initialPlayer.Gold >= scrollPrice, "The ordinary Warrior start must afford the preview scroll.");
            playerEntityId = initialPlayer.EntityId;
            Assert.NotEqual(0U, playerEntityId);
            Assert.Equal(contentHash, initialSnapshot.ContentManifestHash);
            Assert.Equal(rulesetHash, initialSnapshot.RulesetIdentityHash);
            Assert.Equal(contentHash, firstClient.ServerIdentity!.ContentManifestHash);
            Assert.Equal(rulesetHash, firstClient.ServerIdentity.RulesetIdentityHash);

            await MoveToProjectedTownerAsync(firstClient, model, 0);
            var openStoreSequence = firstClient.Queue(new Command {
                OpenStoreRequested = new OpenStoreRequested { StoreId = scrollStoreId },
            }, firstClient.SuggestedCommandTick(model.CurrentTick));
            await firstClient.PollAsync(cancellationToken);
            var openStoreAcknowledgement = await WaitForAcknowledgementAsync(firstClient, openStoreSequence);
            AssertTownPortalAcceptedReceipt(openStoreAcknowledgement, openStoreSequence);
            var openedStoreMessage = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot?.ActiveStore?.StoreId == scrollStoreId);
            model.Apply(openedStoreMessage);
            var listedScroll = Assert.Single(
                openedStoreMessage.Snapshot!.ActiveStore!.Items,
                item => item.StoreSlot == scrollStoreSlot);
            Assert.Equal(scrollSeed, listedScroll.ItemSeed);
            Assert.Equal(scrollPrice, listedScroll.Price);
            Assert.Equal((int)scrollSpellId, listedScroll.State.SpellId);
            Assert.Equal(ItemSpellSourceKind.ConsumableItem, listedScroll.State.SpellSourceKind);

            var purchaseSequence = firstClient.Queue(new Command {
                PurchaseRequested = new PurchaseRequested {
                    StoreId = scrollStoreId,
                    StoreSlot = scrollStoreSlot,
                },
            }, firstClient.SuggestedCommandTick(model.CurrentTick));
            await firstClient.PollAsync(cancellationToken);
            purchaseAcknowledgement = await WaitForAcknowledgementAsync(firstClient, purchaseSequence);
            AssertTownPortalAcceptedReceipt(purchaseAcknowledgement, purchaseSequence);
            var purchasedMessage = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot?.Players.SingleOrDefault(player => player.EntityId == playerEntityId)
                    ?.Inventory.Any(item => item.ItemSeed == scrollSeed
                        && item.State.SpellId == (int)scrollSpellId) == true);
            model.Apply(purchasedMessage);
            var purchasedPlayer = model.Snapshot!.Players.Single(player => player.EntityId == playerEntityId);
            Assert.Equal(initialPlayer.Gold - scrollPrice, purchasedPlayer.Gold);
            var purchasedScroll = purchasedPlayer.Inventory
                .Select((item, index) => (Item: item, Index: index))
                .Single(entry => entry.Item.ItemSeed == scrollSeed
                    && entry.Item.State.SpellId == (int)scrollSpellId);
            Assert.NotEqual(0U, purchasedScroll.Item.State.StableItemId);
            Assert.Equal(ItemSpellSourceKind.ConsumableItem, purchasedScroll.Item.State.SpellSourceKind);

            var closeStoreSequence = firstClient.Queue(new Command {
                CloseStoreRequested = new CloseStoreRequested(),
            }, firstClient.SuggestedCommandTick(model.CurrentTick));
            await firstClient.PollAsync(cancellationToken);
            var closeStoreAcknowledgement = await WaitForAcknowledgementAsync(firstClient, closeStoreSequence);
            AssertTownPortalAcceptedReceipt(closeStoreAcknowledgement, closeStoreSequence);
            var closedStoreMessage = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot is { ActiveStore: null });
            model.Apply(closedStoreMessage);

            await MoveToProjectedWarpAsync(firstClient, model, "town_to_cathedral");
            var dungeonPlayer = model.Snapshot!.Players.Single(player => player.EntityId == playerEntityId);
            Assert.Equal(1U, dungeonPlayer.LevelId);
            var scrollInventoryIndex = dungeonPlayer.Inventory
                .Select((item, index) => (Item: item, Index: index))
                .Single(entry => entry.Item.State.StableItemId == purchasedScroll.Item.State.StableItemId)
                .Index;
            var castSequence = firstClient.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = scrollSpellId,
                    TargetX = checked(dungeonPlayer.PositionX + 1),
                    TargetY = dungeonPlayer.PositionY,
                    Source = CastSourceKind.ConsumableItem,
                    SourceItem = new PlayerItemReference {
                        Location = PlayerItemLocation.Inventory,
                        Slot = checked((uint)scrollInventoryIndex),
                        StableItemId = purchasedScroll.Item.State.StableItemId,
                    },
                },
            }, firstClient.SuggestedCommandTick(model.CurrentTick));
            await firstClient.PollAsync(cancellationToken);
            castAcknowledgement = await WaitForAcknowledgementAsync(firstClient, castSequence);
            AssertTownPortalAcceptedReceipt(castAcknowledgement, castSequence);
            AssertTownPortalReceiptFollows(purchaseAcknowledgement, castAcknowledgement);
            var castEffectMessage = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= castAcknowledgement.AppliedTick
                    && snapshot.Players.SingleOrDefault(player => player.EntityId == playerEntityId) is { } player
                    && player.Inventory.All(item => item.State.StableItemId != purchasedScroll.Item.State.StableItemId)
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.Any(portal => portal.OwnerEntityId == playerEntityId));
            model.Apply(castEffectMessage);
            var castSnapshot = model.Snapshot!;
            var castPlayer = castSnapshot.Players.Single(player => player.EntityId == playerEntityId);
            Assert.Equal(1U, castPlayer.LevelId);
            Assert.DoesNotContain(
                castPlayer.Inventory,
                item => item.State.StableItemId == purchasedScroll.Item.State.StableItemId);
            baselinePortal = Assert.Single(castSnapshot.Portals, portal => portal.OwnerEntityId == playerEntityId).Clone();
            portalId = baselinePortal.PortalId;
            Assert.NotEqual(0U, portalId);
            Assert.True(baselinePortal.Bidirectional);
            Assert.Equal(1U, baselinePortal.SourceLevelId);
            Assert.Equal(0U, baselinePortal.DestinationLevelId);
            Assert.True(baselinePortal.RemainingTicks > 1);
            baselineTick = castSnapshot.Tick;
            baselineRemaining = baselinePortal.RemainingTicks;
            sourceLevelId = baselinePortal.SourceLevelId;
            var visitedSourceCells = new System.Collections.Generic.List<(int X, int Y)> {
                (castPlayer.PositionX, castPlayer.PositionY),
            };

            var endpointMoveReceipts = new System.Collections.Generic.List<CommandResult>();
            var previousEndpointMoveAcknowledgement = castAcknowledgement;
            for (var attempt = 0; attempt < 16; attempt++) {
                var currentPlayer = model.Snapshot!.Players.Single(player => player.EntityId == playerEntityId);
                if (currentPlayer.LevelId == sourceLevelId
                    && currentPlayer.PositionX == baselinePortal.SourcePositionX
                    && currentPlayer.PositionY == baselinePortal.SourcePositionY)
                    break;

                var priorTick = model.CurrentTick;
                var priorPosition = (currentPlayer.PositionX, currentPlayer.PositionY);
                visitedSourceCells.Add(priorPosition);
                var sequence = firstClient.Queue(new Command {
                    MoveRequested = new MoveRequested {
                        TargetCell = true,
                        TargetX = baselinePortal.SourcePositionX,
                        TargetY = baselinePortal.SourcePositionY,
                    },
                }, firstClient.SuggestedCommandTick(model.CurrentTick));
                await firstClient.PollAsync(cancellationToken);
                var acknowledgement = await WaitForAcknowledgementAsync(firstClient, sequence);
                AssertTownPortalAcceptedReceipt(acknowledgement, sequence);
                AssertTownPortalReceiptFollows(previousEndpointMoveAcknowledgement, acknowledgement);
                previousEndpointMoveAcknowledgement = acknowledgement;
                endpointMoveReceipts.Add(acknowledgement);
                var movedMessage = await WaitForMessageAsync(
                    firstClient,
                    message => message.Snapshot is { } snapshot
                        && snapshot.Tick >= acknowledgement.AppliedTick
                        && snapshot.Tick > priorTick
                        && snapshot.Players.SingleOrDefault(player => player.EntityId == playerEntityId) is { } player
                        && player.LevelId == sourceLevelId
                        && (player.PositionX, player.PositionY) != priorPosition
                        && player.CombatMode == PlayerCombatMode.Stand
                        && player.CurrentAction is null);
                model.Apply(movedMessage);
                var settledPlayer = model.Snapshot!.Players.Single(player => player.EntityId == playerEntityId);
                visitedSourceCells.Add((settledPlayer.PositionX, settledPlayer.PositionY));
                _ = AssertTownPortalAtTick(model.Snapshot!, baselinePortal, baselineTick, baselineRemaining);
            }

            var preStopSnapshot = model.Snapshot!;
            var preStopPlayer = preStopSnapshot.Players.Single(player => player.EntityId == playerEntityId);
            Assert.Equal(sourceLevelId, preStopPlayer.LevelId);
            Assert.Equal((baselinePortal.SourcePositionX, baselinePortal.SourcePositionY),
                (preStopPlayer.PositionX, preStopPlayer.PositionY));
            Assert.Equal(PlayerCombatMode.Stand, preStopPlayer.CombatMode);
            Assert.Null(preStopPlayer.CurrentAction);
            Assert.NotEmpty(endpointMoveReceipts);
            finalPortalApproachAcknowledgement = endpointMoveReceipts[^1];
            var preStopPortal = AssertTownPortalAtTick(
                preStopSnapshot,
                baselinePortal,
                baselineTick,
                baselineRemaining);
            Assert.True(preStopSnapshot.Tick > baselineTick,
                "The real endpoint movement must advance the observed host tick.");
            Assert.True(preStopPortal.RemainingTicks > 1,
                "The pre-stop tick witness must remain above the open-portal clamp.");
            Assert.True(preStopPortal.RemainingTicks < baselineRemaining,
                "The open portal counter must decrease before force-stop.");
            resumeToken = firstClient.SessionToken;
            Assert.False(string.IsNullOrWhiteSpace(resumeToken), "The original host did not issue a resume token.");

            await firstClient.DisposeAsync();
            firstClient = null;
            firstServer.Dispose();
            Assert.True(firstServer.HasExited, "The original owned host must stop before checkpoint inspection.");

            checkpoint = ReadStoppedSessionCheckpoint(firstServer, cancellationToken);
            Assert.Equal(rulesetHash, checkpoint.RulesetIdentityHash);
            foreach (var expectedReceipt in endpointMoveReceipts.Prepend(castAcknowledgement).Prepend(purchaseAcknowledgement)) {
                Assert.Equal(
                    expectedReceipt,
                    Assert.Single(checkpoint.CommandReceipts,
                        receipt => receipt.ClientSequence == expectedReceipt.ClientSequence));
            }
            checkpointSnapshot = ParseCheckpointSnapshot(checkpoint);
            Assert.True(checkpointSnapshot.Tick >= finalPortalApproachAcknowledgement.AppliedTick);
            var checkpointPlayer = Assert.Single(
                checkpointSnapshot.Players,
                player => player.EntityId == playerEntityId);
            Assert.Equal(characterName, checkpointPlayer.CharacterName);
            Assert.Equal(0U, checkpointPlayer.CharacterClassId);
            Assert.Equal(sourceLevelId, checkpointPlayer.LevelId);
            Assert.Equal((baselinePortal.SourcePositionX, baselinePortal.SourcePositionY),
                (checkpointPlayer.PositionX, checkpointPlayer.PositionY));
            Assert.Equal(PlayerCombatMode.Stand, checkpointPlayer.CombatMode);
            Assert.Null(checkpointPlayer.CurrentAction);
            checkpointPortal = AssertTownPortalAtTick(
                checkpointSnapshot,
                baselinePortal,
                baselineTick,
                baselineRemaining);

            restartedServer = await StartedServer.StartAsync(
                cancellationToken,
                contentOverlayRoot: townLoopPreviewRoot,
                additionalContentOverlayRoot: townPortalPreviewRoot,
                randomSeed: 1,
                startingLevelOverride: 0,
                portOverride: firstPort,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false);
            Assert.NotEqual(firstProcessId, restartedServer.ProcessId);
            Assert.True(firstServer.HasExited);
            Assert.Equal(contentHash, restartedServer.ContentHash);
            Assert.Equal(rulesetHash, restartedServer.RulesetHash);
            resumedClient = new AuthoritativeClient(
                CreateOptions(restartedServer, "godot-town-portal-lifetime-resume", requestedClassId: 0) with {
                    RequestedCharacterName = characterName,
                    ResumeToken = resumeToken,
                });
            await resumedClient.ConnectAsync(cancellationToken);
            var restoredMessage = await WaitForMessageAsync(
                resumedClient,
                message => message.Snapshot is not null);
            var restoredSnapshot = restoredMessage.Snapshot!;
            Assert.True(restoredSnapshot.Tick >= checkpointSnapshot.Tick);
            Assert.Equal(contentHash, restoredSnapshot.ContentManifestHash);
            Assert.Equal(rulesetHash, restoredSnapshot.RulesetIdentityHash);
            Assert.Equal(contentHash, resumedClient.ServerIdentity!.ContentManifestHash);
            Assert.Equal(rulesetHash, resumedClient.ServerIdentity.RulesetIdentityHash);
            var restoredPlayer = Assert.Single(
                restoredSnapshot.Players,
                player => player.EntityId == playerEntityId);
            Assert.Equal(characterName, restoredPlayer.CharacterName);
            Assert.Equal(0U, restoredPlayer.CharacterClassId);
            Assert.Equal(sourceLevelId, restoredPlayer.LevelId);
            Assert.Equal((baselinePortal.SourcePositionX, baselinePortal.SourcePositionY),
                (restoredPlayer.PositionX, restoredPlayer.PositionY));
            _ = AssertTownPortalAtTick(
                restoredSnapshot,
                checkpointPortal,
                checkpointSnapshot.Tick,
                checkpointPortal.RemainingTicks);

            var resumedModel = new AuthoritativeClientModel();
            resumedModel.Apply(restoredMessage);
            var lastPersistedSequence = checkpoint.CommandReceipts.Max(receipt => receipt.ClientSequence);
            var sourceUseSequence = resumedClient.Queue(new Command {
                UsePortalRequested = new UsePortalRequested { PortalId = portalId },
            }, resumedClient.SuggestedCommandTick(resumedModel.CurrentTick));
            Assert.Equal(checked(lastPersistedSequence + 1), sourceUseSequence);
            await resumedClient.PollAsync(cancellationToken);
            var sourceUseAcknowledgement = await WaitForAcknowledgementAsync(resumedClient, sourceUseSequence);
            AssertTownPortalAcceptedReceipt(sourceUseAcknowledgement, sourceUseSequence);
            AssertTownPortalReceiptFollows(finalPortalApproachAcknowledgement, sourceUseAcknowledgement);
            var townArrivalMessage = await WaitForMessageAsync(
                resumedClient,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= sourceUseAcknowledgement.AppliedTick
                    && snapshot.Players.SingleOrDefault(player => player.EntityId == playerEntityId) is { } player
                    && player.LevelId == checkpointPortal.DestinationLevelId
                    && player.PositionX == checkpointPortal.DestinationPositionX
                    && player.PositionY == checkpointPortal.DestinationPositionY
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.Any(portal => portal.PortalId == portalId));
            resumedModel.Apply(townArrivalMessage);
            _ = AssertTownPortalAtTick(
                resumedModel.Snapshot!,
                checkpointPortal,
                checkpointSnapshot.Tick,
                checkpointPortal.RemainingTicks);

            var ownerReturnSequence = resumedClient.Queue(new Command {
                UsePortalRequested = new UsePortalRequested { PortalId = portalId },
            }, resumedClient.SuggestedCommandTick(resumedModel.CurrentTick));
            Assert.Equal(checked(sourceUseSequence + 1), ownerReturnSequence);
            await resumedClient.PollAsync(cancellationToken);
            var ownerReturnAcknowledgement = await WaitForAcknowledgementAsync(resumedClient, ownerReturnSequence);
            AssertTownPortalAcceptedReceipt(ownerReturnAcknowledgement, ownerReturnSequence);
            AssertTownPortalReceiptFollows(sourceUseAcknowledgement, ownerReturnAcknowledgement);
            var ownerReturnedMessage = await WaitForMessageAsync(
                resumedClient,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= ownerReturnAcknowledgement.AppliedTick
                    && snapshot.Players.SingleOrDefault(player => player.EntityId == playerEntityId) is { } player
                    && player.LevelId == checkpointPortal.SourceLevelId
                    && player.PositionX == checkpointPortal.SourcePositionX
                    && player.PositionY == checkpointPortal.SourcePositionY
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.All(portal => portal.PortalId != portalId));
            resumedModel.Apply(ownerReturnedMessage);
            Assert.DoesNotContain(resumedModel.Snapshot!.Portals, portal => portal.PortalId == portalId);

            var returnResumeToken = resumedClient.SessionToken;
            Assert.False(string.IsNullOrWhiteSpace(returnResumeToken),
                "The owner-return host did not retain a resume token for the next process.");
            await resumedClient.DisposeAsync();
            resumedClient = null;
            restartedServer.Dispose();
            Assert.True(restartedServer.HasExited,
                "The owner-return host must stop before its durable checkpoint is inspected.");

            var ownerReturnCheckpoint = ReadStoppedSessionCheckpoint(restartedServer, cancellationToken);
            Assert.Equal(rulesetHash, ownerReturnCheckpoint.RulesetIdentityHash);
            Assert.Equal(sourceUseAcknowledgement,
                Assert.Single(ownerReturnCheckpoint.CommandReceipts,
                    receipt => receipt.ClientSequence == sourceUseSequence));
            Assert.Equal(ownerReturnAcknowledgement,
                Assert.Single(ownerReturnCheckpoint.CommandReceipts,
                    receipt => receipt.ClientSequence == ownerReturnSequence));
            var ownerReturnCheckpointSnapshot = ParseCheckpointSnapshot(ownerReturnCheckpoint);
            Assert.True(ownerReturnCheckpointSnapshot.Tick >= ownerReturnAcknowledgement.AppliedTick);
            var ownerReturnCheckpointPlayer = Assert.Single(
                ownerReturnCheckpointSnapshot.Players,
                player => player.EntityId == playerEntityId);
            Assert.Equal(characterName, ownerReturnCheckpointPlayer.CharacterName);
            Assert.Equal(0U, ownerReturnCheckpointPlayer.CharacterClassId);
            Assert.Equal(sourceLevelId, ownerReturnCheckpointPlayer.LevelId);
            Assert.Equal((checkpointPortal.SourcePositionX, checkpointPortal.SourcePositionY),
                (ownerReturnCheckpointPlayer.PositionX, ownerReturnCheckpointPlayer.PositionY));
            Assert.Equal(PlayerCombatMode.Stand, ownerReturnCheckpointPlayer.CombatMode);
            Assert.Null(ownerReturnCheckpointPlayer.CurrentAction);
            Assert.DoesNotContain(ownerReturnCheckpointSnapshot.Portals, portal => portal.PortalId == portalId);
            Assert.DoesNotContain(ownerReturnCheckpointSnapshot.Portals,
                portal => portal.OwnerEntityId == playerEntityId);
            var lastOwnerReturnSequence = ownerReturnCheckpoint.CommandReceipts
                .Max(receipt => receipt.ClientSequence);
            Assert.Equal(ownerReturnSequence, lastOwnerReturnSequence);

            returnRestartedServer = await StartedServer.StartAsync(
                cancellationToken,
                contentOverlayRoot: townLoopPreviewRoot,
                additionalContentOverlayRoot: townPortalPreviewRoot,
                randomSeed: 1,
                startingLevelOverride: 0,
                portOverride: firstPort,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false);
            Assert.NotEqual(restartedServer.ProcessId, returnRestartedServer.ProcessId);
            Assert.True(restartedServer.HasExited);
            Assert.Equal(contentHash, returnRestartedServer.ContentHash);
            Assert.Equal(rulesetHash, returnRestartedServer.RulesetHash);
            returnResumedClient = new AuthoritativeClient(
                CreateOptions(returnRestartedServer, "godot-town-portal-lifetime-return-resume", requestedClassId: 0) with {
                    RequestedCharacterName = characterName,
                    ResumeToken = returnResumeToken,
                });
            await returnResumedClient.ConnectAsync(cancellationToken);
            var returnRestoredMessage = await WaitForMessageAsync(
                returnResumedClient,
                message => message.Snapshot is not null);
            var returnRestoredSnapshot = returnRestoredMessage.Snapshot!;
            Assert.True(returnRestoredSnapshot.Tick >= ownerReturnCheckpointSnapshot.Tick);
            Assert.Equal(contentHash, returnRestoredSnapshot.ContentManifestHash);
            Assert.Equal(rulesetHash, returnRestoredSnapshot.RulesetIdentityHash);
            Assert.Equal(contentHash, returnResumedClient.ServerIdentity!.ContentManifestHash);
            Assert.Equal(rulesetHash, returnResumedClient.ServerIdentity.RulesetIdentityHash);
            var returnRestoredPlayer = Assert.Single(
                returnRestoredSnapshot.Players,
                player => player.EntityId == playerEntityId);
            Assert.Equal(characterName, returnRestoredPlayer.CharacterName);
            Assert.Equal(0U, returnRestoredPlayer.CharacterClassId);
            Assert.Equal(sourceLevelId, returnRestoredPlayer.LevelId);
            Assert.Equal((ownerReturnCheckpointPlayer.PositionX, ownerReturnCheckpointPlayer.PositionY),
                (returnRestoredPlayer.PositionX, returnRestoredPlayer.PositionY));
            Assert.Equal(PlayerCombatMode.Stand, returnRestoredPlayer.CombatMode);
            Assert.Null(returnRestoredPlayer.CurrentAction);
            Assert.DoesNotContain(returnRestoredSnapshot.Portals, portal => portal.PortalId == portalId);
            Assert.DoesNotContain(returnRestoredSnapshot.Portals,
                portal => portal.OwnerEntityId == playerEntityId);

            var returnResumedModel = new AuthoritativeClientModel();
            returnResumedModel.Apply(returnRestoredMessage);
            var nextSequence = checked(lastOwnerReturnSequence + 1);
            var moveTarget = SelectVisitedSafeMoveTarget(
                returnRestoredSnapshot,
                returnRestoredPlayer,
                visitedSourceCells);
            Assert.NotEqual(
                (returnRestoredPlayer.PositionX, returnRestoredPlayer.PositionY),
                moveTarget);
            var returnMoveSequence = returnResumedClient.Queue(new Command {
                MoveRequested = new MoveRequested {
                    TargetCell = true,
                    TargetX = moveTarget.X,
                    TargetY = moveTarget.Y,
                },
            }, returnResumedClient.SuggestedCommandTick(returnResumedModel.CurrentTick));
            Assert.Equal(nextSequence, returnMoveSequence);
            await returnResumedClient.PollAsync(cancellationToken);
            var returnMoveAcknowledgement = await WaitForAcknowledgementAsync(returnResumedClient, returnMoveSequence);
            AssertTownPortalAcceptedReceipt(returnMoveAcknowledgement, returnMoveSequence);
            AssertTownPortalReceiptFollows(ownerReturnAcknowledgement, returnMoveAcknowledgement);
            var returnMoveMessage = await WaitForMessageAsync(
                returnResumedClient,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= returnMoveAcknowledgement.AppliedTick
                    && snapshot.Tick > returnRestoredSnapshot.Tick
                    && snapshot.Players.SingleOrDefault(player => player.EntityId == playerEntityId) is { } player
                    && player.LevelId == sourceLevelId
                    && (player.PositionX, player.PositionY) == moveTarget
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null
                    && snapshot.Portals.All(portal => portal.PortalId != portalId
                        && portal.OwnerEntityId != playerEntityId));
            returnResumedModel.Apply(returnMoveMessage);
            var returnMovedSnapshot = returnResumedModel.Snapshot!;
            var returnMovedPlayer = Assert.Single(
                returnMovedSnapshot.Players,
                player => player.EntityId == playerEntityId);
            Assert.True(returnMoveAcknowledgement.ServerReceiptSequence > ownerReturnAcknowledgement.ServerReceiptSequence);
            Assert.True(returnMoveAcknowledgement.AppliedTick >= ownerReturnAcknowledgement.AppliedTick);
            Assert.Equal((moveTarget.X, moveTarget.Y), (returnMovedPlayer.PositionX, returnMovedPlayer.PositionY));
            Assert.Equal(PlayerCombatMode.Stand, returnMovedPlayer.CombatMode);
            Assert.Null(returnMovedPlayer.CurrentAction);
            Assert.DoesNotContain(returnMovedSnapshot.Portals, portal => portal.PortalId == portalId);
            Assert.DoesNotContain(returnMovedSnapshot.Portals, portal => portal.OwnerEntityId == playerEntityId);
            Assert.Empty(cleanupFailures);
        } catch (Exception failure) {
            primaryFailure = failure;
            throw;
        } finally {
            if (returnResumedClient is not null) {
                try {
                    await returnResumedClient.DisposeAsync();
                } catch (Exception cleanupFailure) {
                    cleanupFailures.Add(cleanupFailure);
                }
            }
            if (resumedClient is not null) {
                try {
                    await resumedClient.DisposeAsync();
                } catch (Exception cleanupFailure) {
                    cleanupFailures.Add(cleanupFailure);
                }
            }
            if (firstClient is not null) {
                try {
                    await firstClient.DisposeAsync();
                } catch (Exception cleanupFailure) {
                    cleanupFailures.Add(cleanupFailure);
                }
            }
            returnRestartedServer?.Dispose();
            restartedServer?.Dispose();
            firstServer?.Dispose();
            if (primaryFailure is null) {
                Assert.Empty(cleanupFailures);
                CleanupLocalRoot(saveRootLease);
            }
        }
    }

    private static (int X, int Y) SelectVisitedSafeMoveTarget(
        Snapshot snapshot,
        PlayerSnapshot player,
        System.Collections.Generic.IEnumerable<(int X, int Y)> visitedCells)
    {
        var geometry = snapshot.LevelGeometry.Single(level => level.LevelId == player.LevelId);
        bool IsSafeVisitedCell((int X, int Y) cell)
        {
            var deltaX = Math.Abs((long)cell.X - player.PositionX);
            var deltaY = Math.Abs((long)cell.Y - player.PositionY);
            if (Math.Max(deltaX, deltaY) != 1
                || AuthoritativeLevelGeometryCellResolver.Resolve(geometry, cell.X, cell.Y)
                    == AuthoritativeLevelGeometryCell.Blocked)
                return false;

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
                return false;

            return true;
        }

        var candidates = visitedCells
            .Distinct()
            .Where(IsSafeVisitedCell)
            .OrderBy(cell => Math.Abs((long)cell.X - player.PositionX)
                + Math.Abs((long)cell.Y - player.PositionY))
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "No previously occupied source-level cell is currently safe for the post-resume movement.");
        return candidates[0];
    }

    private static void AssertTownPortalAcceptedReceipt(CommandResult acknowledgement, ulong sequence)
    {
        Assert.Equal(sequence, acknowledgement.ClientSequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
        Assert.Equal(CommandRejectReason.Unspecified, acknowledgement.RejectReason);
        Assert.True(acknowledgement.ServerReceiptSequence > 0);
        Assert.True(acknowledgement.AppliedTick > 0);
    }

    private static void AssertTownPortalReceiptFollows(CommandResult previous, CommandResult next)
    {
        Assert.True(next.ClientSequence > previous.ClientSequence);
        Assert.True(next.ServerReceiptSequence > previous.ServerReceiptSequence);
        Assert.True(next.AppliedTick >= previous.AppliedTick);
    }

    private static PortalSnapshot AssertTownPortalAtTick(
        Snapshot snapshot,
        PortalSnapshot portalIdentity,
        ulong baselineTick,
        uint baselineRemaining)
    {
        Assert.True(baselineRemaining > 0);
        Assert.True(snapshot.Tick >= baselineTick);
        var elapsedTicks = snapshot.Tick - baselineTick;
        var expectedRemaining = elapsedTicks >= (ulong)(baselineRemaining - 1)
            ? 1U
            : baselineRemaining - checked((uint)elapsedTicks);
        var expected = portalIdentity.Clone();
        expected.RemainingTicks = expectedRemaining;
        var actual = Assert.Single(snapshot.Portals, portal => portal.PortalId == portalIdentity.PortalId);
        Assert.Equal(expected, actual);
        Assert.True(actual.RemainingTicks > 0);
        return actual;
    }
}
