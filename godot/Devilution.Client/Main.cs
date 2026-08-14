using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

public partial class Main : Node2D
{
    private const uint ResurrectionSpellId = 32;
    private const uint HealOtherSpellId = 34;
    private const uint FireboltSpellId = 1;
    private const uint ChargedBoltSpellId = 30;
    private const uint BoneSpiritSpellId = 38;
    private const uint IdentifySpellId = 5;
    private const uint RepairSpellId = 26;
    private const uint RechargeSpellId = 27;

    private readonly AuthoritativeClientModel model = new();
    private readonly CancellationTokenSource lifetime = new();
    private AuthoritativeClient? client;
    private WorldPresentation? world;
    private HudPresentation? hud;
    private PresentationFeedback? feedback;
    private Task? connectionTask;
    private Task? pollTask;
    private Task? reconnectTask;
    private string? pendingConnectionError;
    private string? selectedSpellSourceKey;

    public override void _Ready()
    {
        world = new WorldPresentation();
        AddChild(world);
        hud = new HudPresentation();
        AddChild(hud);
        feedback = new PresentationFeedback();
        AddChild(feedback);
        hud.OpenStoreRequested += storeId => QueueIfConnected(new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = storeId } });
        hud.PurchaseRequested += (storeId, storeSlot) => QueueIfConnected(new Command { PurchaseRequested = new PurchaseRequested { StoreId = storeId, StoreSlot = storeSlot } });
        hud.SellItemRequested += inventoryIndex => QueueIfConnected(new Command { SellItemRequested = new SellItemRequested { InventoryIndex = inventoryIndex } });
        hud.RepairItemRequested += inventoryIndex => QueueIfConnected(new Command {
            RepairItemRequested = new RepairItemRequested {
                InventoryIndex = inventoryIndex,
                Source = ResolveItemServiceSource(RepairSpellId),
            },
        });
        hud.RechargeItemRequested += inventoryIndex => QueueIfConnected(new Command {
            RechargeItemRequested = new RechargeItemRequested {
                InventoryIndex = inventoryIndex,
                Source = ResolveItemServiceSource(RechargeSpellId),
            },
        });
        hud.IdentifyItemRequested += inventoryIndex => QueueIfConnected(new Command {
            IdentifyItemRequested = new IdentifyItemRequested {
                InventoryIndex = inventoryIndex,
                Source = ResolveItemServiceSource(IdentifySpellId),
            },
        });
        hud.DisarmTrapRequested += entityId => QueueIfConnected(new Command {
            DisarmTrapRequested = new DisarmTrapRequested { ObjectEntityId = entityId },
        });
        hud.MoveInventoryItemRequested += (inventoryIndex, targetCell) => QueueIfConnected(new Command { MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = inventoryIndex, TargetCell = targetCell } });
        hud.OperateObjectRequested += entityId => QueueIfConnected(new Command { OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = entityId } });
        hud.AdvanceQuestRequested += questId => QueueIfConnected(new Command { AdvanceQuestRequested = new AdvanceQuestRequested { QuestId = questId } });
        hud.RefillManaRequested += () => QueueIfConnected(new Command { RefillManaRequested = new RefillManaRequested() });
        hud.DropHeldItemRequested += DropHeldItemAtPlayer;
        hud.ClearEventsRequested += model.ClearRecentEvents;
        client = new AuthoritativeClient(ClientConnectionOptions.FromEnvironment());
        connectionTask = ConnectAsync(lifetime.Token);
        hud.SetStatus("Connecting to authoritative server...");
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

        world?.Apply(model.Snapshot, delta, model.PredictedPlayerPosition);
        var spellSources = AuthoritativeSpellSourceSelector.GetSources(model.Snapshot);
        var selectedSpell = AuthoritativeSpellSourceSelector.Select(
            spellSources,
            selectedSpellSourceKey);
        selectedSpellSourceKey = selectedSpell?.Key;
        hud?.SetSelectedSpellSource(selectedSpell);
        hud?.Apply(model, client, connectionTask);
        feedback?.Apply(model);
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
            if (world?.WorldItemEntityIdAt(target) is { } itemEntityId) {
                Queue(new Command {
                    PickupWorldItemRequested = new PickupWorldItemRequested {
                        ItemEntityId = itemEntityId,
                    },
                });
                GetViewport().SetInputAsHandled();
                return;
            }
            var cast = CreateCastRequest(target);
            if (cast is not null)
                Queue(new Command { CastRequested = cast });
            else
                hud?.SetStatus("No authoritative spell source is currently available.");
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

    private ItemServiceSourceKind ResolveItemServiceSource(uint abilitySpellId)
    {
        var player = model.Snapshot?.Players.FirstOrDefault();
        return player?.ActiveStoreId == 0 && player.AbilitySpellId == abilitySpellId
            ? ItemServiceSourceKind.ClassAbility
            : ItemServiceSourceKind.Unspecified;
    }

    private CastRequested? CreateCastRequest(Vector2I target)
    {
        var source = AuthoritativeSpellSourceSelector.Select(
            AuthoritativeSpellSourceSelector.GetSources(model.Snapshot),
            selectedSpellSourceKey);
        if (source is null)
            return null;
        var player = model.Snapshot!.Players.First();
        return new CastRequested {
            SpellId = source.SpellId,
            TargetEntityId = ResolvePlayerTargetEntityId(source.SpellId, target, player),
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
        hud?.SetSelectedSpellSource(source);
        if (source is not null)
            hud?.SetStatus($"Selected {source.DisplayName} (click a target to cast; Q/E cycles)");
    }

    private uint ResolvePlayerTargetEntityId(
        uint spellId,
        Vector2I target,
        PlayerSnapshot localPlayer)
    {
        if (spellId is not (ResurrectionSpellId
            or HealOtherSpellId
            or FireboltSpellId
            or ChargedBoltSpellId
            or BoneSpiritSpellId))
            return 0;
        var includeDead = spellId == ResurrectionSpellId;
        return AuthoritativeTargetSelector.SelectRemotePlayerTarget(
            model.Snapshot,
            localPlayer.EntityId,
            target.X,
            target.Y,
            includeDead) ?? 0;
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

    private void Queue(Command command, Vector2I? predictedDirection = null)
    {
        var sequence = client!.Queue(command, client.SuggestedCommandTick(model.CurrentTick));
        if (predictedDirection is { } direction)
            model.TrackPredictedMove(sequence, direction.X, direction.Y);
    }

    private void QueueIfConnected(Command command)
    {
        if (client?.IsConnected == true)
            Queue(command);
    }
}
