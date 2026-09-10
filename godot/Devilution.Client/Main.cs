using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

public partial class Main : Node2D
{
    private readonly AuthoritativeClientModel model = new();
    private readonly CancellationTokenSource lifetime = new();
    private AuthoritativeClient? client;
    private WorldPresentation? world;
    private HudPresentation? hud;
    private PresentationFeedback? feedback;
    private CharacterSetupPresentation? characterSetup;
    private ClientConnectionOptions? connectionOptions;
    private Task? connectionTask;
    private Task? pollTask;
    private Task? reconnectTask;
    private string? pendingConnectionError;
    private string? selectedSpellSourceKey;
    private PendingWorldInteraction? pendingWorldInteraction;

    public override void _Ready()
    {
        world = new WorldPresentation();
        AddChild(world);
        hud = new HudPresentation();
        AddChild(hud);
        feedback = new PresentationFeedback();
        AddChild(feedback);
        characterSetup = new CharacterSetupPresentation();
        AddChild(characterSetup);
        characterSetup.Submitted += (classId, name, host, port, resumeToken) => {
            if (connectionOptions is not null)
                StartConnection(connectionOptions with {
                    Host = host,
                    Port = port,
                    ResumeToken = resumeToken,
                    RequestedCharacterClassId = string.IsNullOrEmpty(resumeToken) ? classId : null,
                    RequestedCharacterName = string.IsNullOrEmpty(resumeToken) ? name : null,
                });
        };
        hud.OpenStoreRequested += storeId => QueueIfConnected(new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = storeId } });
        hud.CloseStoreRequested += () => QueueIfConnected(new Command { CloseStoreRequested = new CloseStoreRequested() });
        hud.OpenStashRequested += () => QueueIfConnected(new Command { OpenStashRequested = new OpenStashRequested() });
        hud.CloseStashRequested += () => QueueIfConnected(new Command { CloseStashRequested = new CloseStashRequested() });
        hud.DepositStashGoldRequested += amount => QueueIfConnected(new Command {
            DepositStashGoldRequested = new DepositStashGoldRequested { Amount = amount },
        });
        hud.WithdrawStashGoldRequested += amount => QueueIfConnected(new Command {
            WithdrawStashGoldRequested = new WithdrawStashGoldRequested { Amount = amount },
        });
        hud.PurchaseRequested += (storeId, storeSlot) => QueueIfConnected(new Command { PurchaseRequested = new PurchaseRequested { StoreId = storeId, StoreSlot = storeSlot } });
        hud.SellItemRequested += item => QueueIfConnected(new Command {
            SellItemRequested = new SellItemRequested {
                InventoryIndex = item.Location == PlayerItemLocation.Inventory ? item.Slot : 0,
                Item = item,
            },
        });
        hud.RepairItemRequested += item => QueueIfConnected(new Command {
            RepairItemRequested = new RepairItemRequested {
                InventoryIndex = item.Location == PlayerItemLocation.Inventory ? item.Slot : 0,
                Item = item,
                Source = ResolveItemServiceSource(PlayerAbilityKind.Repair),
            },
        });
        hud.RechargeItemRequested += item => QueueIfConnected(new Command {
            RechargeItemRequested = new RechargeItemRequested {
                InventoryIndex = item.Location == PlayerItemLocation.Inventory ? item.Slot : 0,
                Item = item,
                Source = ResolveItemServiceSource(PlayerAbilityKind.Recharge),
            },
        });
        hud.IdentifyItemRequested += item => {
            var source = ResolveItemServiceSource(PlayerAbilityKind.Identify);
            var request = new IdentifyItemRequested {
                InventoryIndex = item.Location == PlayerItemLocation.Inventory ? item.Slot : 0,
                Item = item,
                Source = source,
            };
            if (source == ItemServiceSourceKind.ConsumableItem) {
                var sourceItem = ResolveIdentifyScrollReference();
                if (sourceItem is not null)
                    request.SourceItem = sourceItem;
            }
            QueueIfConnected(new Command { IdentifyItemRequested = request });
        };
        hud.ReadBookRequested += item => QueueIfConnected(new Command {
            ReadBookRequested = new ReadBookRequested {
                Item = item,
            },
        });
        hud.UseConsumableRequested += item => QueueIfConnected(new Command {
            UseConsumableRequested = new UseConsumableRequested {
                Item = item,
            },
        });
        hud.ApplyOilRequested += (item, target) => QueueIfConnected(new Command {
            UseConsumableRequested = new UseConsumableRequested {
                Item = item,
                TargetItem = target,
            },
        });
        hud.DisarmTrapRequested += entityId => QueueIfConnected(new Command {
            DisarmTrapRequested = new DisarmTrapRequested { ObjectEntityId = entityId },
        });
        hud.MoveInventoryItemRequested += (inventoryIndex, targetCell) => QueueIfConnected(new Command {
            MoveInventoryItemRequested = new MoveInventoryItemRequested {
                InventoryIndex = inventoryIndex,
                TargetCell = targetCell,
                Item = ResolveInventoryItemReference(inventoryIndex),
            },
        });
        hud.MoveItemRequested += (item, destination) => QueueIfConnected(new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = item,
                Destination = destination,
            },
        });
        hud.AllocateAttributeRequested += attribute => QueueIfConnected(new Command {
            AllocateAttributeRequested = new AllocateAttributeRequested {
                Attribute = attribute,
                Amount = 1,
            },
        });
        hud.OperateObjectRequested += entityId => QueueIfConnected(new Command { OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = entityId } });
        hud.AdvanceQuestRequested += questId => QueueIfConnected(new Command { AdvanceQuestRequested = new AdvanceQuestRequested { QuestId = questId } });
        hud.QuestTownerInteractionRequested += (questId, townerId) => QueueIfConnected(new Command {
            TurnInQuestRequested = new TurnInQuestRequested {
                QuestId = questId,
                TownerId = townerId,
            },
        });
        hud.RefillManaRequested += () => QueueIfConnected(new Command { RefillManaRequested = new RefillManaRequested() });
        hud.DropHeldItemRequested += DropHeldItemAtPlayer;
        hud.ClearEventsRequested += model.ClearRecentEvents;
        hud.SpellSourceSelected += SelectSpellSource;
        connectionOptions = ClientConnectionOptions.FromEnvironment();
        if (connectionOptions.ResumeToken.Length > 0
            || (connectionOptions.RequestedCharacterClassId.HasValue
                && !string.IsNullOrWhiteSpace(connectionOptions.RequestedCharacterName))) {
            StartConnection(connectionOptions);
        } else {
            characterSetup.ShowSetup(connectionOptions);
            hud.SetStatus("Choose a character to connect to the authoritative server.");
        }
    }

    private void StartConnection(ClientConnectionOptions options)
    {
        if (client is not null)
            return;
        characterSetup?.HideSetup();
        client = new AuthoritativeClient(options);
        connectionTask = ConnectAsync(lifetime.Token);
        hud?.SetStatus("Connecting to authoritative server...");
    }

    public override void _Process(double delta)
    {
        if (client is not null) {
            if (Interlocked.Exchange(ref pendingConnectionError, null) is { } connectionError)
                model.Apply(new AuthoritativeClientMessage(Error: new ProtocolError {
                    Code = ProtocolErrorCode.NotAuthenticated,
                    Detail = connectionError,
                }));

            foreach (var message in client.DrainMessages()) {
                try {
                    model.Apply(message);
                } catch (Exception exception) {
                    hud?.SetStatus($"Invalid authoritative state: {exception.Message}");
                }
            }

            if (pollTask is { IsCompleted: true }) {
                var pollFailed = pollTask.IsFaulted;
                _ = pollTask.Exception;
                pollTask = null;
                if (pollFailed)
                    StartReconnect(force: true);
            }

            if (client.IsConnected
                && pollTask is null
                && (reconnectTask is null || reconnectTask.IsCompleted))
                pollTask = client.PollAsync(lifetime.Token);

            if (!client.IsConnected
                && (connectionTask?.IsCompleted ?? true)
                && (reconnectTask is null || reconnectTask.IsCompleted))
                StartReconnect(force: false);
        }

        world?.Apply(
            model.Snapshot,
            delta,
            model.PredictedPlayerPosition,
            client?.ServerTickRateHz ?? AuthoritativePresentationClock.DefaultTickRateHz);
        TrySubmitPendingWorldInteraction();
        var spellSources = AuthoritativeSpellSourceSelector.GetSources(model.Snapshot);
        var selectedSpell = AuthoritativeSpellSourceSelector.Select(
            spellSources,
            selectedSpellSourceKey);
        selectedSpellSourceKey = selectedSpell?.Key;
        hud?.SetSpellSources(spellSources, selectedSpellSourceKey);
        hud?.Apply(model, client, connectionTask);
        feedback?.Apply(model, world);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey toggle && toggle.Pressed && !toggle.Echo) {
            if (toggle.Keycode == Key.F3) {
                hud?.ToggleDiagnostics();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (toggle.Keycode == Key.F4) {
                hud?.ToggleHighContrast();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && client?.IsConnected == true) {
            var localPlayer = model.Snapshot?.Players.FirstOrDefault();
            if (key.Keycode == Key.Escape
                && localPlayer?.Life == 0
                && localPlayer.CombatMode == PlayerCombatMode.Death
                && localPlayer.CurrentAction is null) {
                Queue(new Command { RestartTownRequested = new RestartTownRequested() });
                GetViewport().SetInputAsHandled();
                return;
            }
            var direction = key.Keycode switch {
                Key.Up or Key.W => Vector2I.Up,
                Key.Down or Key.S => Vector2I.Down,
                Key.Left or Key.A => Vector2I.Left,
                Key.Right or Key.D => Vector2I.Right,
                _ => Vector2I.Zero,
            };
            if (direction != Vector2I.Zero)
                Queue(new Command { MoveRequested = new MoveRequested { DirectionX = direction.X, DirectionY = direction.Y } }, direction);

            if (key.Keycode == Key.O)
                Queue(new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = 1 } });
            if (key.Keycode == Key.P)
                Queue(new Command { PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 } });
            if (key.Keycode == Key.M)
                Queue(new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = 10 } });
            if (key.Keycode == Key.R)
                QueueIfConnected(new Command { RefillManaRequested = new RefillManaRequested() });
            if (key.Keycode == Key.X)
                DropHeldItemAtPlayer();
            if (key.Keycode == Key.F) {
                var player = model.Snapshot?.Players.FirstOrDefault();
                if (player is not null) {
                    Queue(new Command {
                        SetFriendlyModeRequested = new SetFriendlyModeRequested {
                            FriendlyMode = player.HostileMode,
                        },
                    });
                }
            }
            if (key.Keycode is Key.Q or Key.E)
                CycleSpellSource(key.Keycode == Key.E ? 1 : -1);
        }

        if (@event is InputEventMouseButton mouse && mouse.Pressed && client?.IsConnected == true) {
            var target = world?.ScreenToTile(mouse.Position) ?? Vector2I.Zero;
            world?.SetTargetPreview(target);

            if (mouse.ButtonIndex == MouseButton.Right) {
                pendingWorldInteraction = null;
                if (world?.CombatTargetEntityIdAt(target) is { } targetEntityId) {
                    Queue(new Command {
                        AttackRequested = new AttackRequested {
                            TargetEntityId = targetEntityId,
                        },
                    });
                } else {
                    hud?.SetStatus("No authoritative combat target at that tile.");
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            if (mouse.ButtonIndex != MouseButton.Left)
                return;

            pendingWorldInteraction = null;
            var player = model.Snapshot?.Players.FirstOrDefault();
            if (player?.HeldItem is not null) {
                Queue(new Command {
                    DropHeldItemRequested = new DropHeldItemRequested {
                        TargetX = target.X,
                        TargetY = target.Y,
                    },
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            if (player is not null
                && hud?.SelectedItemReference is { } selectedItem
                && AuthoritativeItemDropRules.IsCarriedItem(player, selectedItem)) {
                Queue(new Command {
                    DropItemRequested = new DropItemRequested {
                        Item = selectedItem.Clone(),
                        TargetX = target.X,
                        TargetY = target.Y,
                    },
                });
                hud.ClearItemSelection();
                GetViewport().SetInputAsHandled();
                return;
            }
            var selectedSpellSource = AuthoritativeSpellSourceSelector.Select(
                AuthoritativeSpellSourceSelector.GetSources(model.Snapshot),
                selectedSpellSourceKey);
            if (selectedSpellSource?.TargetingKind == SpellTargetingKind.WorldEntity
                && AuthoritativeTargetSelector.SelectWorldEntityTarget(
                    model.Snapshot,
                    player?.EntityId ?? 0,
                    target.X,
                    target.Y) is { } worldTargetEntityId) {
                Queue(new Command {
                    CastRequested = CreateCastRequest(
                        target,
                        selectedSpellSource,
                        worldTargetEntityId),
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            if (world?.WorldItemEntityIdAt(target) is { } itemEntityId) {
                QueueInteractionAfterMove(target, new Command {
                    PickupWorldItemRequested = new PickupWorldItemRequested {
                        ItemEntityId = itemEntityId,
                    },
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            if (world?.PortalEntityIdAt(target) is { } portalEntityId) {
                var portal = model.Snapshot?.Portals.FirstOrDefault(
                    candidate => candidate.PortalId == portalEntityId);
                if (portal is null) {
                    GetViewport().SetInputAsHandled();
                    return;
                }
                QueueInteractionAfterMove(target, new Command {
                    UsePortalRequested = new UsePortalRequested {
                        PortalId = portalEntityId,
                    },
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            if (world?.ObjectEntityIdAt(target) is { } objectEntityId) {
                var worldObject = model.Snapshot?.Objects.FirstOrDefault(
                    @object => @object.EntityId == objectEntityId);
                var playerAbility = model.Snapshot?.Players.FirstOrDefault()?.AbilityKind;
                if (worldObject?.TrapArmed == true && playerAbility != PlayerAbilityKind.TrapDisarm) {
                    hud?.SetStatus("This trap requires the Trap Disarm ability.");
                } else if (worldObject?.TrapArmed == true) {
                    QueueInteractionAfterMove(target, new Command {
                        DisarmTrapRequested = new DisarmTrapRequested {
                            ObjectEntityId = objectEntityId,
                        },
                    });
                } else {
                    QueueInteractionAfterMove(target, new Command {
                        OperateObjectRequested = new OperateObjectRequested {
                            ObjectEntityId = objectEntityId,
                        },
                    });
                }
                GetViewport().SetInputAsHandled();
                return;
            }
            if (AuthoritativeMonsterDialogueSelector.Select(
                    model.Snapshot,
                    player?.EntityId ?? 0,
                    target.X,
                    target.Y) is { } monsterDialogue) {
                QueueInteractionAfterMove(target, new Command {
                    TalkToQuestRequested = new TalkToQuestRequested {
                        QuestId = monsterDialogue.QuestId,
                        TargetEntityId = monsterDialogue.MonsterEntityId,
                    },
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            if (model.QuestTownerInteractionAt(target.X, target.Y) is { } interaction) {
                QueueInteractionAfterMove(target, new Command {
                    TurnInQuestRequested = new TurnInQuestRequested {
                        QuestId = interaction.QuestId,
                        TownerId = checked((uint)interaction.TownerId),
                    },
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            if (model.TownerGreetingAt(target.X, target.Y) is { } greeting) {
                QueueInteractionAfterMove(target, new Command {
                    TalkToTownerRequested = new TalkToTownerRequested {
                        TownerId = checked((uint)greeting.TownerId),
                    },
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            if (model.WarpTriggerAt(target.X, target.Y) is { } warp) {
                if (!warp.Available) {
                    hud?.SetStatus("That route is not currently available.");
                } else {
                    Queue(new Command {
                        MoveRequested = new MoveRequested {
                            TargetCell = true,
                            TargetX = target.X,
                            TargetY = target.Y,
                        },
                    });
                }
                GetViewport().SetInputAsHandled();
                return;
            }
            var cast = CreateCastRequest(target);
            if (cast is not null)
                Queue(new Command { CastRequested = cast });
            else
                Queue(new Command {
                    MoveRequested = new MoveRequested {
                        TargetCell = true,
                        TargetX = target.X,
                        TargetY = target.Y,
                    },
                });
        }
    }

    private void DropHeldItemAtPlayer()
    {
        var player = model.Snapshot?.Players.FirstOrDefault();
        if (player?.HeldItem is null)
            return;
        var origin = model.PredictedPlayerPosition
            ?? new ClientPosition(player.PositionX, player.PositionY);
        QueueIfConnected(new Command {
            DropHeldItemRequested = new DropHeldItemRequested {
                TargetX = origin.X,
                TargetY = origin.Y,
            },
        });
    }

    private ulong QueueMoveTo(Vector2I target)
    {
        return Queue(new Command {
            MoveRequested = new MoveRequested {
                TargetCell = true,
                TargetX = target.X,
                TargetY = target.Y,
            },
        });
    }

    private void QueueInteractionAfterMove(Vector2I target, Command interaction)
    {
        var player = model.Snapshot?.Players.FirstOrDefault();
        if (player is null)
            return;
        if (IsInteractionInRange(player, interaction, target)) {
            Queue(interaction);
            return;
        }

        var movementSequence = QueueMoveTo(target);
        pendingWorldInteraction = new PendingWorldInteraction(
            target,
            interaction,
            movementSequence);
    }

    private void TrySubmitPendingWorldInteraction()
    {
        if (pendingWorldInteraction is not { } pending)
            return;
        if (model.LastCommandResult is {
                ClientSequence: var sequence,
                Status: CommandStatus.Rejected,
            } && sequence == pending.MovementSequence) {
            pendingWorldInteraction = null;
            return;
        }
        if (!IsPendingTargetPresent(pending)) {
            pendingWorldInteraction = null;
            return;
        }
        if (client?.IsConnected != true
            || model.Snapshot?.Players.FirstOrDefault() is not { } player
            || !IsInteractionInRange(player, pending.Command, pending.Target)) {
            return;
        }

        pendingWorldInteraction = null;
        Queue(pending.Command);
    }

    private bool IsPendingTargetPresent(PendingWorldInteraction pending)
    {
        return pending.Command.IntentCase switch {
            Command.IntentOneofCase.PickupWorldItemRequested =>
                world?.WorldItemEntityIdAt(pending.Target)
                    == pending.Command.PickupWorldItemRequested.ItemEntityId,
            Command.IntentOneofCase.UsePortalRequested =>
                world?.PortalEntityIdAt(pending.Target)
                    == pending.Command.UsePortalRequested.PortalId,
            Command.IntentOneofCase.OperateObjectRequested =>
                world?.ObjectEntityIdAt(pending.Target)
                    == pending.Command.OperateObjectRequested.ObjectEntityId,
            Command.IntentOneofCase.DisarmTrapRequested =>
                world?.ObjectEntityIdAt(pending.Target)
                    == pending.Command.DisarmTrapRequested.ObjectEntityId,
            Command.IntentOneofCase.TurnInQuestRequested =>
                model.QuestTownerInteractionAt(pending.Target.X, pending.Target.Y) is { } interaction
                && interaction.QuestId == pending.Command.TurnInQuestRequested.QuestId
                && interaction.TownerId == pending.Command.TurnInQuestRequested.TownerId,
            Command.IntentOneofCase.TalkToQuestRequested =>
                AuthoritativeMonsterDialogueSelector.Select(
                    model.Snapshot,
                    model.PlayerEntityId,
                    pending.Target.X,
                    pending.Target.Y) is { } dialogue
                && dialogue.QuestId == pending.Command.TalkToQuestRequested.QuestId
                && dialogue.MonsterEntityId == pending.Command.TalkToQuestRequested.TargetEntityId,
            Command.IntentOneofCase.TalkToTownerRequested =>
                model.TownerGreetingAt(pending.Target.X, pending.Target.Y) is { } greeting
                && greeting.TownerId == pending.Command.TalkToTownerRequested.TownerId,
            _ => false,
        };
    }

    private bool IsInteractionInRange(
        PlayerSnapshot player,
        Command interaction,
        Vector2I target)
    {
        if (interaction.IntentCase == Command.IntentOneofCase.UsePortalRequested) {
            var portal = model.Snapshot?.Portals.FirstOrDefault(
                candidate => candidate.PortalId == interaction.UsePortalRequested.PortalId);
            return portal is not null
                && AuthoritativeInteractionRange.IsAtPortalEndpoint(player, portal);
        }
        return AuthoritativeInteractionRange.IsAdjacent(player, target.X, target.Y);
    }

    private readonly record struct PendingWorldInteraction(
        Vector2I Target,
        Command Command,
        ulong MovementSequence);

    private ItemServiceSourceKind ResolveItemServiceSource(PlayerAbilityKind abilityKind)
    {
        var player = model.Snapshot?.Players.FirstOrDefault();
        return player?.ActiveStoreId == 0 && player.AbilityKind == abilityKind
            ? ItemServiceSourceKind.ClassAbility
            : abilityKind == PlayerAbilityKind.Identify && ResolveIdentifyScrollReference() is not null
                ? ItemServiceSourceKind.ConsumableItem
            : ItemServiceSourceKind.Unspecified;
    }

    private PlayerItemReference? ResolveIdentifyScrollReference()
    {
        var player = model.Snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;
        for (var index = 0; index < player.Inventory.Count; index++) {
            if (player.Inventory[index].State?.ItemActionKind == ItemActionKind.Identify)
                return new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = checked((uint)index),
                    StableItemId = player.Inventory[index].State?.StableItemId ?? 0,
                };
        }
        for (var index = 0; index < player.Belt.Count; index++) {
            if (player.Belt[index].State?.ItemActionKind == ItemActionKind.Identify)
                return new PlayerItemReference {
                    Location = PlayerItemLocation.Belt,
                    Slot = player.Belt[index].Slot,
                    StableItemId = player.Belt[index].State?.StableItemId ?? 0,
                };
        }
        return null;
    }

    private PlayerItemReference ResolveInventoryItemReference(uint inventoryIndex)
    {
        var player = model.Snapshot?.Players.FirstOrDefault();
        if (player is null || inventoryIndex >= player.Inventory.Count)
            return new PlayerItemReference();
        return new PlayerItemReference {
            Location = PlayerItemLocation.Inventory,
            Slot = inventoryIndex,
            StableItemId = player.Inventory[(int)inventoryIndex].State?.StableItemId ?? 0,
        };
    }

    private CastRequested? CreateCastRequest(Vector2I target)
    {
        var source = AuthoritativeSpellSourceSelector.Select(
            AuthoritativeSpellSourceSelector.GetSources(model.Snapshot),
            selectedSpellSourceKey);
        if (source is null)
            return null;
        var player = model.Snapshot!.Players.First();
        return CreateCastRequest(
            target,
            source,
            ResolveVisibleCastTargetEntityId(target, player, source.TargetingKind));
    }

    private static CastRequested CreateCastRequest(
        Vector2I target,
        AuthoritativeSpellSource source,
        uint targetEntityId)
    {
        return new CastRequested {
            SpellId = source.SpellId,
            TargetEntityId = targetEntityId,
            TargetX = target.X,
            TargetY = target.Y,
            Source = source.Kind,
            SourceItem = source.SourceItem,
        };
    }

    private void CycleSpellSource(int direction)
    {
        var source = AuthoritativeSpellSourceSelector.Cycle(
            AuthoritativeSpellSourceSelector.GetSources(model.Snapshot),
            selectedSpellSourceKey,
            direction);
        selectedSpellSourceKey = source?.Key;
        if (source is not null)
            hud?.SetStatus($"Selected {source.DisplayName} (click a target to cast; Q/E cycles)");
    }

    private void SelectSpellSource(string sourceKey)
    {
        var source = AuthoritativeSpellSourceSelector.Select(
            AuthoritativeSpellSourceSelector.GetSources(model.Snapshot),
            sourceKey);
        selectedSpellSourceKey = source?.Key;
        if (source is not null)
            hud?.SetStatus($"Selected {source.DisplayName} (click a target to cast)");
    }

    private uint ResolveVisibleCastTargetEntityId(
        Vector2I target,
        PlayerSnapshot localPlayer,
        SpellTargetingKind targetingKind)
    {
        if (targetingKind == SpellTargetingKind.CombatEntity) {
            return AuthoritativeTargetSelector.SelectCombatTarget(
                model.Snapshot,
                localPlayer.EntityId,
                target.X,
                target.Y)
                ?? 0;
        }
        if (targetingKind is SpellTargetingKind.Player
            or SpellTargetingKind.Unspecified) {
            return AuthoritativeTargetSelector.SelectRemotePlayerTarget(
                model.Snapshot,
                localPlayer.EntityId,
                target.X,
                target.Y,
                includeDead: true)
                ?? 0;
        }
        return 0;
    }

    public override async void _ExitTree()
    {
        lifetime.Cancel();
        if (client is not null)
            await client.DisposeAsync();
        lifetime.Dispose();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try {
            await client!.ConnectAsync(cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        } catch (Exception exception) {
            Interlocked.Exchange(ref pendingConnectionError, exception.Message);
        }
    }

    private void StartReconnect(bool force)
    {
        model.ResetTransientPresentation();
        if (reconnectTask is null || reconnectTask.IsCompleted)
            reconnectTask = ReconnectLoopAsync(force);
    }

    private async Task ReconnectLoopAsync(bool force)
    {
        var firstAttempt = true;
        var delay = TimeSpan.Zero;
        while (!lifetime.IsCancellationRequested && client is not null) {
            if (!force && !firstAttempt && client.IsConnected)
                return;
            if (delay > TimeSpan.Zero) {
                try {
                    await Task.Delay(delay, lifetime.Token);
                } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
                    return;
                }
            }

            try {
                await client.ReconnectAsync(lifetime.Token);
                return;
            } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
                return;
            } catch (Exception exception) {
                Interlocked.Exchange(ref pendingConnectionError, exception.Message);
                delay = delay == TimeSpan.Zero
                    ? TimeSpan.FromMilliseconds(250)
                    : TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 5000));
                force = false;
            }
            firstAttempt = false;
        }
    }

    private ulong Queue(Command command, Vector2I? predictedDirection = null)
    {
        var sequence = client!.Queue(command, client.SuggestedCommandTick(model.CurrentTick));
        if (predictedDirection is { } direction)
            model.TrackPredictedMove(sequence, direction.X, direction.Y);
        return sequence;
    }

    private void QueueIfConnected(Command command)
    {
        if (client?.IsConnected == true)
            Queue(command);
    }
}
