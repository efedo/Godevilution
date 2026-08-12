using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

public partial class Main : Node2D
{
    private const uint ResurrectionSpellId = 32;
    private const uint HealOtherSpellId = 34;
    private const uint IdentifySpellId = 5;
    private const uint RepairSpellId = 26;
    private const uint RechargeSpellId = 27;

    private readonly AuthoritativeClientModel model = new();
    private AuthoritativeClient? client;
    private WorldPresentation? world;
    private HudPresentation? hud;
    private PresentationFeedback? feedback;
    private Task? connectionTask;
    private Task? pollTask;

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
        connectionTask = ConnectAsync();
        hud.SetStatus("Connecting to authoritative server...");
    }

    public override void _Process(double delta)
    {
        if (client is not null) {
            foreach (var message in client.DrainMessages()) {
                try {
                    model.Apply(message);
                } catch (Exception exception) {
                    hud?.SetStatus($"Invalid authoritative state: {exception.Message}");
                }
            }

            if (client.IsConnected && (pollTask is null || pollTask.IsCompleted))
                pollTask = client.PollAsync();
        }

        world?.Apply(model.Snapshot, delta, model.PredictedPlayerPosition);
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
        }

        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && client?.IsConnected == true) {
            var target = world?.ScreenToTile(mouse.Position) ?? Vector2I.Zero;
            world?.SetTargetPreview(target);
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
        var player = model.Snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var memorized = player.MemorizedSpells
            .Where(spell => spell.SpellId != 0 && spell.Level != 0)
            .OrderBy(spell => spell.SpellId)
            .FirstOrDefault();
        if (memorized is not null) {
            return new CastRequested {
                SpellId = memorized.SpellId,
                TargetEntityId = ResolvePlayerTargetEntityId(
                    memorized.SpellId,
                    target,
                    player),
                TargetX = target.X,
                TargetY = target.Y,
                Source = CastSourceKind.MemorizedSpell,
            };
        }

        var staff = player.Equipment
            .Where(item => item.Slot == 4
                && item.State is not null
                && item.State.SpellId > 0
                && item.State.Charges > 0
                && item.State.MiscId is 23 or 27)
            .OrderBy(item => item.ItemSeed)
            .FirstOrDefault();
        if (staff is not null) {
            return new CastRequested {
                SpellId = (uint)staff.State.SpellId,
                TargetEntityId = ResolvePlayerTargetEntityId(
                    (uint)staff.State.SpellId,
                    target,
                    player),
                TargetX = target.X,
                TargetY = target.Y,
                Source = CastSourceKind.ChargedItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = staff.Slot,
                },
            };
        }

        for (var index = 0; index < player.Inventory.Count; index++) {
            var item = player.Inventory[index];
            if (!IsSpellConsumable(item.State))
                continue;
            return CreateConsumableCast(
                (uint)item.State.SpellId,
                target,
                ResolvePlayerTargetEntityId(
                    (uint)item.State.SpellId,
                    target,
                    player),
                PlayerItemLocation.Inventory,
                (uint)index);
        }
        var beltItem = player.Belt
            .Where(item => IsSpellConsumable(item.State))
            .OrderBy(item => item.Slot)
            .FirstOrDefault();
        if (beltItem is not null) {
            return CreateConsumableCast(
                (uint)beltItem.State.SpellId,
                target,
                ResolvePlayerTargetEntityId(
                    (uint)beltItem.State.SpellId,
                    target,
                    player),
                PlayerItemLocation.Belt,
                beltItem.Slot);
        }

        return player.AbilitySpellId == 0
            ? null
            : new CastRequested {
                SpellId = player.AbilitySpellId,
                TargetEntityId = ResolvePlayerTargetEntityId(
                    player.AbilitySpellId,
                    target,
                    player),
                TargetX = target.X,
                TargetY = target.Y,
                Source = CastSourceKind.Ability,
            };
    }

    private static CastRequested CreateConsumableCast(
        uint spellId,
        Vector2I target,
        uint targetEntityId,
        PlayerItemLocation location,
        uint slot)
    {
        return new CastRequested {
            SpellId = spellId,
            TargetEntityId = targetEntityId,
            TargetX = target.X,
            TargetY = target.Y,
            Source = CastSourceKind.ConsumableItem,
            SourceItem = new PlayerItemReference { Location = location, Slot = slot },
        };
    }

    private uint ResolvePlayerTargetEntityId(
        uint spellId,
        Vector2I target,
        PlayerSnapshot localPlayer)
    {
        if (spellId is not (ResurrectionSpellId or HealOtherSpellId))
            return 0;
        return model.Snapshot?.Players
            .Where(player => player.EntityId != localPlayer.EntityId
                && player.LevelId == localPlayer.LevelId
                && (spellId != HealOtherSpellId || player.Life > 0)
                && player.PositionX == target.X
                && player.PositionY == target.Y)
            .OrderBy(player => player.EntityId)
            .Select(player => player.EntityId)
            .FirstOrDefault() ?? 0;
    }

    private static bool IsSpellConsumable(ItemStateSnapshot? state)
    {
        if (state is null || state.SpellId <= 0)
            return false;
        return state.MiscId is 21 or 22
            || (state.MiscId is >= 47 and <= 51 && state.MiscId == state.SpellId);
    }

    public override async void _ExitTree()
    {
        if (client is not null)
            await client.DisposeAsync();
    }

    private async Task ConnectAsync()
    {
        try {
            await client!.ConnectAsync();
        } catch (Exception exception) {
            model.Apply(new AuthoritativeClientMessage(Error: new ProtocolError {
                Code = ProtocolErrorCode.NotAuthenticated,
                Detail = exception.Message,
            }));
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
