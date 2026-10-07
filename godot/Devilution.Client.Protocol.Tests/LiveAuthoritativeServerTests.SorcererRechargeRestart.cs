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
    public async Task LiveServerRestoresSorcererRechargeAfterProcessRestart()
    {
        const string characterName = "M1-Sorcerer";
        var cancellationToken = TestContext.Current.CancellationToken;
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Sorcerer-Recharge-Restart-" + Guid.NewGuid().ToString("N"));
        var saveRootLease = LiveTestSaveRoot.Register(saveRoot);
        StartedServer? firstServer = null;
        StartedServer? restartedServer = null;
        AuthoritativeClient? firstClient = null;
        AuthoritativeClient? resumedClient = null;
        Exception? primaryFailure = null;

        try {
            string resumeToken;
            string contentHash;
            string rulesetHash;
            uint entityId;
            uint levelId;
            uint stableItemId;
            uint itemSeed;
            int positionX;
            int positionY;
            var expectedResources = (Life: 0, Mana: 0, Gold: 0U, Experience: 0U);
            EquippedItemSnapshot expectedRechargedStaff;
            CommandResult castAcknowledgement;
            CommandResult rechargeAcknowledgement;
            AuthoritativeSessionDocument checkpoint;
            Snapshot checkpointSnapshot;
            PlayerSnapshot checkpointPlayer;

            firstServer = await StartedServer.StartAsync(
                cancellationToken,
                randomSeed: 1,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false);
            var firstProcessId = firstServer.ProcessId;
            var firstPort = firstServer.Port;
            contentHash = firstServer.ContentHash;
            rulesetHash = firstServer.RulesetHash;
            firstClient = new AuthoritativeClient(
                CreateOptions(firstServer, "godot-sorcerer-recharge-restart-first", requestedClassId: 2) with {
                    RequestedCharacterName = characterName,
                });

            await firstClient.ConnectAsync(cancellationToken);
            var initial = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot is not null);
            var initialSnapshot = initial.Snapshot!;
            var initialPlayer = Assert.Single(initialSnapshot.Players);
            Assert.Equal(2U, initialPlayer.CharacterClassId);
            Assert.Equal(characterName, initialPlayer.CharacterName);
            Assert.Equal(27U, initialPlayer.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.Recharge, initialPlayer.AbilityKind);
            Assert.Equal(contentHash, initialSnapshot.ContentManifestHash);
            Assert.Equal(rulesetHash, initialSnapshot.RulesetIdentityHash);
            Assert.Equal(contentHash, firstClient.ServerIdentity!.ContentManifestHash);
            Assert.Equal(rulesetHash, firstClient.ServerIdentity.RulesetIdentityHash);

            var initialStaff = Assert.Single(initialPlayer.Equipment, item => item.Slot == 4);
            Assert.Equal(166, initialStaff.State.ItemIndex);
            Assert.Equal(30, initialStaff.State.SpellId);
            Assert.Equal(40, initialStaff.State.Charges);
            Assert.Equal(40, initialStaff.State.MaxCharges);
            Assert.NotEqual(0U, initialStaff.State.StableItemId);
            entityId = initialPlayer.EntityId;
            Assert.NotEqual(0U, entityId);
            levelId = initialPlayer.LevelId;
            stableItemId = initialStaff.State.StableItemId;
            itemSeed = initialStaff.ItemSeed;
            Assert.NotEqual(0U, itemSeed);
            positionX = initialPlayer.PositionX;
            positionY = initialPlayer.PositionY;

            var castTarget = Assert.Single(
                initialSnapshot.Monsters,
                monster => monster.EntityId == 9 && monster.Alive);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var castSequence = firstClient.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = (uint)initialStaff.State.SpellId,
                    TargetEntityId = castTarget.EntityId,
                    Source = CastSourceKind.ChargedItem,
                    SourceItem = new PlayerItemReference {
                        Location = PlayerItemLocation.Equipment,
                        Slot = 4,
                        StableItemId = stableItemId,
                    },
                },
            }, firstClient.SuggestedCommandTick(model.CurrentTick));
            Assert.Equal(1UL, castSequence);
            await firstClient.PollAsync(cancellationToken);
            castAcknowledgement = await WaitForAcknowledgementAsync(firstClient, castSequence);
            Assert.Equal(CommandStatus.Accepted, castAcknowledgement.Status);
            Assert.Equal(CommandRejectReason.Unspecified, castAcknowledgement.RejectReason);
            Assert.True(castAcknowledgement.ServerReceiptSequence > 0);
            Assert.True(castAcknowledgement.AppliedTick > 0);

            var consumed = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= castAcknowledgement.AppliedTick
                    && snapshot.Players.Single().Equipment.Single(item => item.Slot == 4).State
                        is { Charges: 39, MaxCharges: 40 });
            model.Apply(consumed);
            var consumedStaff = Assert.Single(consumed.Snapshot!.Players).Equipment
                .Single(item => item.Slot == 4);
            var expectedConsumedStaff = initialStaff.Clone();
            expectedConsumedStaff.State.Charges = 39;
            Assert.Equal(expectedConsumedStaff, consumedStaff);

            // This is the same typed class-ability request emitted by the HUD Recharge action.
            var rechargeSequence = firstClient.Queue(new Command {
                RechargeItemRequested = new RechargeItemRequested {
                    Item = new PlayerItemReference {
                        Location = PlayerItemLocation.Equipment,
                        Slot = 4,
                        StableItemId = stableItemId,
                    },
                    Source = ItemServiceSourceKind.ClassAbility,
                },
            }, firstClient.SuggestedCommandTick(model.CurrentTick));
            Assert.Equal(2UL, rechargeSequence);
            await firstClient.PollAsync(cancellationToken);
            rechargeAcknowledgement = await WaitForAcknowledgementAsync(firstClient, rechargeSequence);
            Assert.Equal(CommandStatus.Accepted, rechargeAcknowledgement.Status);
            Assert.Equal(CommandRejectReason.Unspecified, rechargeAcknowledgement.RejectReason);
            Assert.True(rechargeAcknowledgement.ServerReceiptSequence > 0);
            Assert.True(rechargeAcknowledgement.AppliedTick > 0);
            Assert.True(castAcknowledgement.ServerReceiptSequence < rechargeAcknowledgement.ServerReceiptSequence);
            Assert.True(castAcknowledgement.AppliedTick <= rechargeAcknowledgement.AppliedTick);

            var settledAfterRecharge = await WaitForMessageAsync(
                firstClient,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= rechargeAcknowledgement.AppliedTick
                    && snapshot.Players.Single() is { } player
                    && player.Equipment.Single(item => item.Slot == 4).State
                        is { Charges: 39, MaxCharges: 39 }
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null);
            model.Apply(settledAfterRecharge);
            var rechargedPlayer = Assert.Single(settledAfterRecharge.Snapshot!.Players);
            var rechargedStaff = Assert.Single(rechargedPlayer.Equipment, item => item.Slot == 4);
            expectedRechargedStaff = initialStaff.Clone();
            expectedRechargedStaff.State.Charges = 39;
            expectedRechargedStaff.State.MaxCharges = 39;
            Assert.Equal(expectedRechargedStaff, rechargedStaff);
            Assert.Equal("Charged Bolt", rechargedStaff.State.SpellDisplayName);
            Assert.Equal(SpellTargetingKind.CombatEntity, rechargedStaff.State.SpellTargetingKind);
            expectedResources = (
                rechargedPlayer.Life,
                rechargedPlayer.Mana,
                rechargedPlayer.Gold,
                rechargedPlayer.Experience);
            Assert.Equal(entityId, rechargedPlayer.EntityId);
            Assert.Equal(characterName, rechargedPlayer.CharacterName);
            Assert.Equal(levelId, rechargedPlayer.LevelId);
            Assert.Equal((positionX, positionY), (rechargedPlayer.PositionX, rechargedPlayer.PositionY));
            resumeToken = firstClient.SessionToken;
            Assert.False(string.IsNullOrWhiteSpace(resumeToken));

            await firstClient.DisposeAsync();
            firstClient = null;
            firstServer.Dispose();
            Assert.True(firstServer.HasExited, "The first owned host must be stopped before checkpoint inspection.");
            Assert.Empty(cleanupFailures);

            checkpoint = ReadStoppedSessionCheckpoint(firstServer, cancellationToken);
            Assert.Equal(rulesetHash, checkpoint.RulesetIdentityHash);
            Assert.Equal(2, checkpoint.CommandReceipts.Count);
            Assert.Equal(
                castAcknowledgement,
                Assert.Single(checkpoint.CommandReceipts, receipt => receipt.ClientSequence == castSequence));
            Assert.Equal(
                rechargeAcknowledgement,
                Assert.Single(checkpoint.CommandReceipts, receipt => receipt.ClientSequence == rechargeSequence));
            checkpointSnapshot = ParseCheckpointSnapshot(checkpoint);
            checkpointPlayer = Assert.Single(checkpointSnapshot.Players);
            Assert.Equal(entityId, checkpointPlayer.EntityId);
            Assert.Equal(2U, checkpointPlayer.CharacterClassId);
            Assert.Equal(characterName, checkpointPlayer.CharacterName);
            Assert.Equal(levelId, checkpointPlayer.LevelId);
            Assert.Equal((positionX, positionY), (checkpointPlayer.PositionX, checkpointPlayer.PositionY));
            Assert.Equal(expectedResources, (
                checkpointPlayer.Life,
                checkpointPlayer.Mana,
                checkpointPlayer.Gold,
                checkpointPlayer.Experience));
            // The portable payload omits wire identity hashes; the enclosing session document
            // binds the durable ruleset, while the live handshake binds content on resume.
            // SaveCodec maps item state without spell catalogs, so only spell presentation is absent.
            var expectedPortableStaff = expectedRechargedStaff.Clone();
            expectedPortableStaff.State.SpellDisplayName = string.Empty;
            expectedPortableStaff.State.SpellTargetingKind = SpellTargetingKind.Unspecified;
            Assert.Equal(expectedPortableStaff, Assert.Single(checkpointPlayer.Equipment, item => item.Slot == 4));

            restartedServer = await StartedServer.StartAsync(
                cancellationToken,
                portOverride: firstPort,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false);
            Assert.NotEqual(firstProcessId, restartedServer.ProcessId);
            Assert.True(firstServer.HasExited);
            Assert.Equal(contentHash, restartedServer.ContentHash);
            Assert.Equal(rulesetHash, restartedServer.RulesetHash);
            resumedClient = new AuthoritativeClient(
                CreateOptions(restartedServer, "godot-sorcerer-recharge-restart-resume", requestedClassId: 2) with {
                    ResumeToken = resumeToken,
                });
            await resumedClient.ConnectAsync(cancellationToken);
            var restored = await WaitForMessageAsync(
                resumedClient,
                message => message.Snapshot is not null);
            var restoredSnapshot = restored.Snapshot!;
            var restoredPlayer = Assert.Single(restoredSnapshot.Players);
            Assert.True(restoredSnapshot.Tick >= checkpointSnapshot.Tick);
            Assert.Equal(entityId, restoredPlayer.EntityId);
            Assert.Equal(2U, restoredPlayer.CharacterClassId);
            Assert.Equal(characterName, restoredPlayer.CharacterName);
            Assert.Equal(levelId, restoredPlayer.LevelId);
            Assert.Equal((positionX, positionY), (restoredPlayer.PositionX, restoredPlayer.PositionY));
            Assert.Equal(expectedResources, (
                restoredPlayer.Life,
                restoredPlayer.Mana,
                restoredPlayer.Gold,
                restoredPlayer.Experience));
            Assert.Equal(contentHash, restoredSnapshot.ContentManifestHash);
            Assert.Equal(rulesetHash, restoredSnapshot.RulesetIdentityHash);
            Assert.Equal(contentHash, resumedClient.ServerIdentity!.ContentManifestHash);
            Assert.Equal(rulesetHash, resumedClient.ServerIdentity.RulesetIdentityHash);
            var restoredStaff = Assert.Single(restoredPlayer.Equipment, item => item.Slot == 4);
            Assert.Equal(itemSeed, restoredStaff.ItemSeed);
            Assert.Equal(stableItemId, restoredStaff.State.StableItemId);
            Assert.Equal(expectedRechargedStaff, restoredStaff);

            var moveSequence = resumedClient.Queue(new Command {
                MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
            }, resumedClient.SuggestedCommandTick(restoredSnapshot.Tick));
            Assert.Equal(3UL, moveSequence);
            await resumedClient.PollAsync(cancellationToken);
            var moveAcknowledgement = await WaitForAcknowledgementAsync(resumedClient, moveSequence);
            Assert.Equal(3UL, moveAcknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
            Assert.Equal(CommandRejectReason.Unspecified, moveAcknowledgement.RejectReason);
            Assert.True(moveAcknowledgement.ServerReceiptSequence > 0);
            Assert.True(moveAcknowledgement.AppliedTick > 0);
            Assert.True(rechargeAcknowledgement.ServerReceiptSequence < moveAcknowledgement.ServerReceiptSequence);
            Assert.True(rechargeAcknowledgement.AppliedTick <= moveAcknowledgement.AppliedTick);
            var moved = await WaitForMessageAsync(
                resumedClient,
                message => message.Snapshot is { } snapshot
                    && snapshot.Tick >= moveAcknowledgement.AppliedTick
                    && snapshot.Tick > restoredSnapshot.Tick
                    && snapshot.Players.Single() is { } player
                    && player.EntityId == entityId
                    && (player.PositionX, player.PositionY) == (positionX, positionY + 1)
                    && player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null);
            var movedSnapshot = moved.Snapshot!;
            var movedPlayer = Assert.Single(movedSnapshot.Players);
            Assert.Equal(entityId, movedPlayer.EntityId);
            Assert.Equal(2U, movedPlayer.CharacterClassId);
            Assert.Equal(characterName, movedPlayer.CharacterName);
            Assert.Equal(levelId, movedPlayer.LevelId);
            Assert.Equal(expectedResources, (
                movedPlayer.Life,
                movedPlayer.Mana,
                movedPlayer.Gold,
                movedPlayer.Experience));
            Assert.Equal(contentHash, movedSnapshot.ContentManifestHash);
            Assert.Equal(rulesetHash, movedSnapshot.RulesetIdentityHash);
            Assert.Equal(expectedRechargedStaff, Assert.Single(movedPlayer.Equipment, item => item.Slot == 4));
            Assert.Empty(cleanupFailures);
        } catch (Exception failure) {
            primaryFailure = failure;
            throw;
        } finally {
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
            restartedServer?.Dispose();
            firstServer?.Dispose();
            if (primaryFailure is null) {
                Assert.Empty(cleanupFailures);
                CleanupLocalRoot(saveRootLease);
            }
        }
    }
}
