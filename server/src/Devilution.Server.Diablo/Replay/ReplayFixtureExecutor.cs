using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Stores;

namespace Devilution.Server.Replay;

public sealed record ReplayCheckpointResult(ulong Tick, string ExpectedStateSha256, string ActualStateSha256);

public sealed record ReplayExecutionResult(
    Snapshot InitialSnapshot,
    Snapshot FinalSnapshot,
    IReadOnlyList<CommandResult> Results,
    IReadOnlyList<ReplayCheckpointResult> Checkpoints);

/** Executes the fixture's supported authoritative commands and validates receipts. */
public static class ReplayFixtureExecutor
{
    public static ReplayExecutionResult Execute(
        ReplayFixture fixture,
        StoreSimulationExecutor executor,
        AuthoritativeCommandServer commandServer,
        uint entityId = 1)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(commandServer);

        var sessionId = fixture.InitialState.Player;
        if (fixture.InitialState.SecondaryPlayer is { } secondary) {
            if (string.Equals(sessionId, secondary.Player, StringComparison.Ordinal)
                || secondary.EntityId == entityId) {
                throw new InvalidDataException(
                    "Replay secondary-player identity must differ from the primary player.");
            }
            var secondaryInventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
            var secondaryInventory = new List<OwnedStoreItem>();
            if (secondary.InventoryItem is { } secondaryItem) {
                secondaryInventory.Add(secondaryItem.ToOwnedStoreItem());
                secondaryInventoryGrid[checked((int)secondaryItem.Slot)] = 0;
            }
            var fillerState = AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemIndex = 1,
                Value = 1,
                IdentifiedValue = 1,
                StatFlag = true,
            };
            for (var filler = 0U; filler < secondary.InventoryFillerCount; filler++) {
                var cell = Array.IndexOf(secondaryInventoryGrid, -1);
                if (cell < 0)
                    throw new InvalidDataException("Replay secondary inventory filler exceeds available cells.");
                secondaryInventory.Add(new OwnedStoreItem(
                    0,
                    0,
                    checked(200U + filler),
                    1,
                    0,
                    fillerState));
                secondaryInventoryGrid[cell] = secondaryInventory.Count - 1;
            }
            executor.InitializeParticipant(
                secondary.Player,
                new DiabloPlayerTemplate(
                    secondary.Gold,
                    0,
                    secondary.Life,
                    secondary.Mana,
                    secondary.ManaMaximum,
                    PlayerAttributesState.Zero with {
                        Magic = new PlayerAttributeState(
                            secondary.Magic,
                            secondary.Magic),
                        Dexterity = new PlayerAttributeState(
                            secondary.Dexterity,
                            secondary.Dexterity),
                    },
                    secondary.Equipment.Count > 0
                        ? secondary.Equipment
                            .Select(item => item.ToEquippedStoreItem())
                            .ToArray()
                        : secondary.BlockCapable
                        ? [new EquippedStoreItem(
                            5,
                            1,
                            AuthoritativeItemState.Empty with {
                                ItemType = 5,
                                StatFlag = true,
                            })]
                        : [],
                    secondaryInventoryGrid,
                    [],
                    secondary.PositionX,
                    secondary.PositionY,
                    secondary.LifeMaximum,
                    secondary.CharacterLevel,
                    secondary.LevelId,
                    0,
                    secondary.CharacterClass,
                    inventory: secondaryInventory,
                    memorizedSpells: secondary.MemorizedSpells
                        .Select(spell => new MemorizedSpellState(spell.SpellId, spell.Level))
                        .ToArray(),
                    friendlyMode: secondary.FriendlyMode,
                    combatMode: secondary.CombatMode,
                    combatModeEndTick: secondary.CombatModeEndTick,
                    facingDirection: secondary.FacingDirection,
                    characterName: secondary.CharacterName,
                    manaShieldActive: secondary.ManaShieldActive,
                    goldTopologyEnabled: false,
                    heldItem: secondary.HeldItem?.ToOwnedStoreItem()));
            _ = executor.CreateSnapshot(
                secondary.Player,
                secondary.EntityId,
                0);
        }
        // Capture only after every fixture participant has been registered so
        // the replay's initial snapshot has the same complete participant set
        // as later authoritative snapshots and reconnect captures.
        var initialSnapshot = executor.CreateSnapshot(sessionId, entityId, 0);
        if (initialSnapshot.RandomState != checked((uint)fixture.RngSeed)) {
            throw new InvalidDataException(
                $"Replay RNG seed mismatch: fixture {fixture.RngSeed}, simulation {initialSnapshot.RandomState}.");
        }
        var results = new List<CommandResult>();
        var checkpoints = new List<ReplayCheckpointResult>();
        var orderedCommands = fixture.OrderedCommands;
        var timelineTicks = orderedCommands.Select(command => command.TargetTick)
            .Concat(fixture.ContentManifest is null
                ? []
                : fixture.Checkpoints.Select(checkpoint => checkpoint.Tick))
            .Distinct()
            .OrderBy(tick => tick)
            .ToArray();
        foreach (var tick in timelineTicks) {
            foreach (var fixtureCommand in orderedCommands.Where(command => command.TargetTick == tick)) {
                var command = ToProtocolCommand(fixtureCommand);
                var commandSessionId = string.IsNullOrEmpty(fixtureCommand.Player)
                    ? sessionId
                    : fixtureCommand.Player;
                var result = commandServer.Process(commandSessionId, command, tick);
                if (result.ServerReceiptSequence != fixtureCommand.ServerReceiptSequence) {
                    throw new InvalidDataException(
                        $"Replay receipt mismatch for client sequence {fixtureCommand.ClientSequence}.");
                }
                if (fixtureCommand.ExpectedStatus is { } expectedStatus
                    && result.Status != expectedStatus) {
                    throw new InvalidDataException(
                        $"Replay command status mismatch for client sequence {fixtureCommand.ClientSequence}: "
                        + $"expected {expectedStatus}, actual {result.Status}.");
                }
                if (fixtureCommand.ExpectedRejectReason is { } expectedRejectReason
                    && result.RejectReason != expectedRejectReason) {
                    throw new InvalidDataException(
                        $"Replay command reject reason mismatch for client sequence {fixtureCommand.ClientSequence}: "
                        + $"expected {expectedRejectReason}, actual {result.RejectReason}.");
                }
                results.Add(result);
            }

            commandServer.AdvanceTo(tick);
            if (fixture.ContentManifest is null)
                continue;
            var checkpoint = fixture.Checkpoints.SingleOrDefault(candidate => candidate.Tick == tick);
            if (checkpoint is null)
                continue;
            var snapshot = executor.CreateSnapshot(sessionId, entityId, tick);
            if (IsSha256(checkpoint.StateSha256)
                && !string.Equals(
                    checkpoint.StateSha256,
                    snapshot.StateSha256,
                    StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Replay checkpoint hash mismatch at tick {checkpoint.Tick}: expected {checkpoint.StateSha256}, actual {snapshot.StateSha256}.");
            }
            checkpoints.Add(new ReplayCheckpointResult(
                checkpoint.Tick,
                checkpoint.StateSha256,
                snapshot.StateSha256));
        }

        var finalTick = timelineTicks.LastOrDefault();
        var finalSnapshot = executor.CreateSnapshot(sessionId, entityId, finalTick);
        if (fixture.FinalStateSha256 is not null
            && !string.Equals(fixture.FinalStateSha256, finalSnapshot.StateSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Replay final snapshot hash mismatch.");

        return new ReplayExecutionResult(initialSnapshot, finalSnapshot, results, checkpoints);
    }

    private static Command ToProtocolCommand(ReplayFixtureCommand fixtureCommand)
    {
        return fixtureCommand.Kind switch {
                "OpenStore" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    OpenStoreRequested = new OpenStoreRequested { StoreId = fixtureCommand.StoreId },
                },
                "BuyItem" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    PurchaseRequested = new PurchaseRequested {
                        StoreId = fixtureCommand.StoreId,
                        StoreSlot = fixtureCommand.StoreSlot,
                    },
                },
                "SellItem" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    SellItemRequested = new SellItemRequested { InventoryIndex = fixtureCommand.StoreSlot },
                },
                "RefillMana" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    RefillManaRequested = new RefillManaRequested(),
                },
                "Move" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    MoveRequested = new MoveRequested {
                        DirectionX = fixtureCommand.DirectionX,
                        DirectionY = fixtureCommand.DirectionY,
                    },
                },
                "Attack" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    AttackRequested = new AttackRequested { TargetEntityId = fixtureCommand.TargetEntityId },
                },
                "Cast" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    CastRequested = new CastRequested {
                        SpellId = fixtureCommand.SpellId,
                        TargetEntityId = fixtureCommand.TargetEntityId,
                        TargetX = fixtureCommand.TargetX,
                        TargetY = fixtureCommand.TargetY,
                    },
                },
                "UsePortal" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    UsePortalRequested = new UsePortalRequested { PortalId = fixtureCommand.PortalId },
                },
                "RestartTown" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    RestartTownRequested = new RestartTownRequested(),
                },
                "PickupWorldItem" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = fixtureCommand.WorldItemEntityId },
                },
                "DropHeldItem" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    DropHeldItemRequested = new DropHeldItemRequested {
                        TargetX = fixtureCommand.TargetX,
                        TargetY = fixtureCommand.TargetY,
                    },
                },
                "OperateObject" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = fixtureCommand.ObjectEntityId },
                },
                "AdvanceQuest" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    AdvanceQuestRequested = new AdvanceQuestRequested { QuestId = fixtureCommand.QuestId },
                },
                "TalkToQuest" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    TalkToQuestRequested = new TalkToQuestRequested {
                        QuestId = fixtureCommand.QuestId,
                        TargetEntityId = fixtureCommand.TargetEntityId,
                    },
                },
                "DropItem" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    DropItemRequested = new DropItemRequested {
                        Item = new PlayerItemReference {
                            Location = ParseItemLocation(fixtureCommand.ItemLocation),
                            Slot = fixtureCommand.StoreSlot,
                            StableItemId = fixtureCommand.StableItemId,
                        },
                        TargetX = fixtureCommand.TargetX,
                        TargetY = fixtureCommand.TargetY,
                    },
                },
                _ => throw new InvalidDataException($"Unsupported replay command '{fixtureCommand.Kind}'."),
            };
    }

    private static PlayerItemLocation ParseItemLocation(string value)
    {
        return value switch {
            "Inventory" => PlayerItemLocation.Inventory,
            "Belt" => PlayerItemLocation.Belt,
            "Equipment" => PlayerItemLocation.Equipment,
            "Stash" => PlayerItemLocation.Stash,
            _ => throw new InvalidDataException($"Unsupported replay item location '{value}'."),
        };
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(character => Uri.IsHexDigit(character));
    }
}
