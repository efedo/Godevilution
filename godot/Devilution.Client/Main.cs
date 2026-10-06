using Devilution.Client.Protocol;
using Devilution.Client.Diagnostics;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Godot;
using System.Runtime.InteropServices;
using Environment = System.Environment;

namespace Devilution.Client;

public partial class Main : Node2D
{
    private readonly AuthoritativeClientModel model = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly AuthoritativeRouteTravel routeTravel = new();
    private readonly AuthoritativeInteractionTravel interactionTravel = new();
    private readonly Control worldFrame = new() {
        Name = "WorldArea", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore,
    };
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
    private string? resumeTokenPath;
    private bool persistLocalSession;
    private string? localSessionWarning;
    private double bookmarkRetrySeconds;
    private ulong snapshotRevision;
    private ClientFrameEvidenceRecorder? frameEvidence;

    // Read-only copies for the scene acceptance harness; never a gameplay owner.
    internal Snapshot? ObservedSnapshot => model.Snapshot?.Clone();
    internal CommandResult? ObservedCommandResult => model.LastCommandResult?.Clone();
    // Installed only for a bounded acceptance observation; normal scenes retain nothing.
    internal Action<CommandAck>? AcceptanceAcknowledgementObserver { get; set; }
    internal AuthoritativeServerIdentity? ObservedIdentity => client?.ServerIdentity;
    internal AuthoritativeClientTimingCaptureReport? ObservedCommandTimingReport
        => client?.CommandTimingRecorder?.CaptureReport();
    internal bool ObservedClockGatedScheduling => client?.IsClockGatedCommandScheduling == true;
    internal bool TransportConnected => client?.IsConnected == true;
    internal int ObservedPendingCommandCount => client?.PendingCommandCount ?? 0;
    internal Vector2? ObservedPlayerScreenPosition => world?.ScreenPositionForEntity(model.PlayerEntityId);
    internal ulong ObservedSnapshotRevision => snapshotRevision;
    internal ulong ObservedLastQueuedSequence { get; private set; }
    internal PlayerItemReference? ObservedSelectedItemReference => hud?.SelectedItemReference?.Clone();
    internal Vector2 ObservedTileScreenPosition(int x, int y) => world!.ScreenPositionForTile(x, y);
    internal Vector2? ObservedEntityScreenPosition(uint entityId) => world?.ScreenPositionForEntity(entityId);
    internal CathedralDoorDrawObservation? ObservedCathedralDoorDrawing(uint entityId)
        => world?.ObserveCathedralDoorDrawing(entityId);
    internal PresentationPackObservation? ObservedPresentationPack => world?.ObservePresentationPack();
    internal ulong ObservedPresentationEpochResetCount => world?.PresentationEpochResetCount ?? 0;
    internal PresentationPackObservation? ObservedFeedbackPresentationPack => feedback?.ObservePresentationPack();
    internal PresentationTextureBindingObservation? ObservedPlayerTextureBinding => world?.ObserveTextureBinding("player");
    internal PresentationPlayerTextureBindingObservation? ObservedPlayerActorTextureBinding
        => world?.ObservePlayerTextureBinding(model.PlayerEntityId);
    internal IReadOnlyList<PresentationPlayerTextureBindingObservation> ObservedPlayerActorTextureBindingHistory
        => world?.ObservePlayerTextureBindingHistory(model.PlayerEntityId)
            ?? Array.Empty<PresentationPlayerTextureBindingObservation>();

    internal string? ClientFrameEvidencePath => frameEvidence?.EvidenceFilePath;

    /** Starts an explicitly requested bounded capture; participant count is caller-supplied. */
    internal bool StartPerformanceRecording(int participantCount, double warmupSeconds)
    {
        if (frameEvidence is not null
            || participantCount is < 1 or > 64
            || !double.IsFinite(warmupSeconds)
            || warmupSeconds < 0)
            return false;
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                "GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT")))
            return false;

        var imageDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CLIENT_ASSEMBLY_IMAGE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(imageDirectory)
            || !Path.IsPathFullyQualified(imageDirectory)
            || !Directory.Exists(imageDirectory))
            return false;

        var snapshot = model.Snapshot;
        var serverIdentity = client?.ServerIdentity;
        if (snapshot is null
            || serverIdentity is null
            || snapshot.Players.Count != participantCount)
            return false;

        var options = connectionOptions ?? ClientConnectionOptions.FromEnvironment();
        var viewport = GetViewport().GetVisibleRect().Size;
        var displayServer = DisplayServer.GetName();
        var vsyncMode = DisplayServer.WindowGetVsyncMode(0).ToString();
        var metadata = new ClientFrameEvidenceMetadata(
            options.ClientBuildId,
            options.ProtocolSchemaVersion,
            options.ContentManifestHash,
            options.RulesetIdentityHash,
            RenderingServer.GetCurrentRenderingMethod(),
            RenderingServer.GetVideoAdapterName(),
            FormattableString.Invariant($"{viewport.X:0}x{viewport.Y:0}"),
            !string.Equals(vsyncMode, "Disabled", StringComparison.Ordinal),
            Engine.GetMaxFps(),
            string.Equals(displayServer, "headless", StringComparison.OrdinalIgnoreCase)
                || OS.HasFeature("headless"),
            GetGodotEngineVersion(),
            participantCount,
            RenderingServer.GetCurrentRenderingDriverName(),
            displayServer,
            OS.GetName(),
            OS.GetVersion(),
            GetOperatingSystemFeatures(),
            vsyncMode,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            GetBuildConfiguration(),
            FormatWindowPosition(DisplayServer.WindowGetPosition(0)),
            DisplayServer.WindowGetMode(0).ToString(),
            DisplayServer.WindowCanDraw(0),
            DisplayServer.WindowIsFocused(0),
            ClientRuntimeAssemblyProvenance.CaptureStagedImages([
                new ClientRuntimeAssemblyImageInput("client", typeof(Main).Assembly,
                    Path.Combine(imageDirectory, "Devilution.Client.dll")),
                new ClientRuntimeAssemblyImageInput("client-protocol", typeof(AuthoritativeClient).Assembly,
                    Path.Combine(imageDirectory, "Devilution.Client.Protocol.dll")),
            ]));
        frameEvidence = ClientFrameEvidenceRecorder.TryStartFromEnvironment(
            metadata,
            warmupSeconds);
        if (frameEvidence is { } recorder) {
            recorder.ObserveServerIdentity(serverIdentity);
            recorder.ObserveParticipantCount(snapshot.Players.Count);
        }
        return frameEvidence is not null;
    }

    /** Stops the capture with an explicit workload result; application exit is never successful. */
    internal bool StopPerformanceRecording(bool workloadPassed)
        => frameEvidence?.Stop(
            workloadPassed ? "workload-complete" : "workload-failed",
            workloadPassed) == true;

    private static string? GetGodotEngineVersion()
    {
        var versionInfo = Engine.GetVersionInfo();
        return versionInfo.ContainsKey("string")
            ? versionInfo["string"].AsString()
            : null;
    }

    private static string GetOperatingSystemFeatures()
    {
        var candidates = new[] {
            "headless", "server", "editor", "movie", "web", "mobile", "desktop",
            "pc", "linux", "windows", "macos", "x11", "wayland",
        };
        return string.Join(",", candidates.Where(OS.HasFeature));
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string FormatWindowPosition(Vector2I position)
        => FormattableString.Invariant($"{position.X},{position.Y}");

    public override void _Ready()
    {
        GetWindow().MinSize = new Vector2I(960, 600);
        var playerSlot = Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER");
        if (int.TryParse(playerSlot, out var playerNumber)
            && playerNumber is >= 1 and <= 4)
            GetWindow().Title = $"Godevilution - Player {playerNumber}";
        if (int.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_QUIT_AFTER_SECONDS"), out var quitSeconds)
            && quitSeconds is > 0 and <= 3600)
            GetTree().CreateTimer(quitSeconds).Timeout += () => GetTree().Quit();
        world = new WorldPresentation();
        AddChild(worldFrame);
        worldFrame.AddChild(world);
        hud = new HudPresentation();
        AddChild(hud);
        feedback = new PresentationFeedback();
        AddChild(feedback);
        characterSetup = new CharacterSetupPresentation { Name = "CharacterSetup" };
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
        hud.RestartTownRequested += () => RequestTownRestart();
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
        hud.TravelRequested += warpId => {
            interactionTravel.Cancel();
            routeTravel.Start(model.Snapshot, warpId, client?.IsConnected == true);
        };
        hud.CancelTravelRequested += () => {
            routeTravel.Cancel();
            interactionTravel.Cancel();
        };
        hud.DestinationRequested += destination => {
            var interaction = destination.CreateInteraction();
            if (AuthoritativeWorldDestinationSelector.IsPresent(model.Snapshot, destination.X, destination.Y, interaction))
                QueueInteractionAfterMove(new Vector2I(destination.X, destination.Y), interaction);
        };
        connectionOptions = ClientConnectionOptions.FromEnvironment();
        resumeTokenPath = Environment.GetEnvironmentVariable("DEVILUTION_RESUME_TOKEN_PATH");
        if (connectionOptions.ResumeToken.Length > 0
            || (connectionOptions.RequestedCharacterClassId.HasValue
                && !string.IsNullOrWhiteSpace(connectionOptions.RequestedCharacterName))) {
            StartConnection(connectionOptions);
        } else {
            var defaults = connectionOptions;
            string? bookmarkError = null;
            if (!string.IsNullOrWhiteSpace(resumeTokenPath)) {
                try {
                    defaults = defaults with { ResumeToken = LocalSessionBookmark.Read(resumeTokenPath) ?? string.Empty };
                } catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) {
                    bookmarkError = $"Could not read local resume bookmark: {exception.Message}";
                }
            }
            characterSetup.ShowSetup(defaults);
            if (bookmarkError is not null)
                characterSetup.ShowError(bookmarkError);
            hud.SetStatus("Choose a character to connect to the authoritative server.");
        }
    }

    private void StartConnection(ClientConnectionOptions options)
    {
        if (client is not null)
            return;
        characterSetup?.HideSetup();
        // An edited remote endpoint must not replace the local host's bookmark.
        persistLocalSession = options.Host == connectionOptions?.Host
            && options.Port == connectionOptions?.Port && !string.IsNullOrWhiteSpace(resumeTokenPath);
        client = new AuthoritativeClient(options) {
            CommandTimingRecorder = Environment.GetEnvironmentVariable("GODEVILUTION_COMMAND_TIMING_TRACE") == "1"
                ? new AuthoritativeClientCommandTimingRecorder()
                : null,
        };
        connectionTask = ConnectAsync(lifetime.Token);
        hud?.SetStatus("Connecting to authoritative server...");
    }

    public override void _Process(double delta)
    {
        var evidenceFrameTimestamp = frameEvidence is null
            ? 0
            : ClientFrameEvidenceRecorder.GetTimestamp();
        var evidenceSceneStartTimestamp = evidenceFrameTimestamp;
        if (localSessionWarning is not null && client?.IsConnected == true) {
            bookmarkRetrySeconds += delta;
            if (bookmarkRetrySeconds >= 5) {
                bookmarkRetrySeconds = 0;
                PersistLocalBookmark();
            }
        }
        if (client is not null) {
            if (Interlocked.Exchange(ref pendingConnectionError, null) is { } connectionError)
                model.Apply(new AuthoritativeClientMessage(Error: new ProtocolError {
                    Code = ProtocolErrorCode.NotAuthenticated,
                    Detail = connectionError,
                }));

            foreach (var message in client.DrainMessages()) {
                try {
                    model.Apply(message);
                    if (message.Acknowledgement is { } acknowledgement)
                        AcceptanceAcknowledgementObserver?.Invoke(acknowledgement.Clone());
                    if (message.Snapshot is not null)
                        snapshotRevision++;
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

        hud?.UpdateLayout();
        if (hud is not null) {
            worldFrame.Position = hud.PlayArea.Position;
            worldFrame.Size = hud.PlayArea.Size;
        }
        world?.ApplyAccepted(
            model.Snapshot,
            model.AcceptedSnapshotRevision,
            delta,
            model.PredictedPlayerPosition,
            client?.ServerTickRateHz ?? AuthoritativePresentationClock.DefaultTickRateHz,
            client?.IsConnected == true);
        TrySubmitPendingWorldInteraction();
        if (routeTravel.Next(model.Snapshot, model.LastCommandResult,
                client?.IsConnected == true, client?.PendingCommandCount ?? 0) is { } routeStep) {
            var sequence = Queue(routeStep, automaticInput: true);
            routeTravel.Submitted(sequence, model.Snapshot!.Players.First());
        }
        hud?.SetTravelStatus(routeTravel.Active || interactionTravel.Active,
            interactionTravel.Active
                ? $"Approaching ({interactionTravel.Target.X}, {interactionTravel.Target.Y}) — server-controlled"
                : routeTravel.Status);
        var spellSources = AuthoritativeSpellSourceSelector.GetSources(model.Snapshot);
        var selectedSpell = AuthoritativeSpellSourceSelector.Select(
            spellSources,
            selectedSpellSourceKey);
        selectedSpellSourceKey = selectedSpell?.Key;
        hud?.SetSpellSources(spellSources, selectedSpellSourceKey);
        hud?.Apply(model, client, connectionTask);
        hud?.SetSessionWarning(localSessionWarning);
        feedback?.Apply(model, world);
        var evidenceSceneEndTimestamp = frameEvidence is null
            ? 0
            : ClientFrameEvidenceRecorder.GetTimestamp();
        if (frameEvidence is { } evidence && evidenceFrameTimestamp != 0) {
            if (client?.ServerIdentity is { } identity)
                evidence.ObserveServerIdentity(identity);
            if (model.Snapshot is { } snapshot)
                evidence.ObserveParticipantCount(snapshot.Players.Count);
            evidence.RecordFrame(
                evidenceFrameTimestamp,
                evidenceSceneStartTimestamp,
                evidenceSceneEndTimestamp);
        }
    }

    private bool RequestTownRestart()
    {
        if (client?.IsConnected != true
            || !AuthoritativeDeathRecoveryRules.CanRequestTownRestart(model.Snapshot?.Players.FirstOrDefault()))
            return false;
        routeTravel.Cancel();
        interactionTravel.Cancel();
        Queue(new Command { RestartTownRequested = new RestartTownRequested() });
        return true;
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
            if (key.Keycode == Key.Escape && (routeTravel.Active || interactionTravel.Active)) {
                routeTravel.Cancel();
                interactionTravel.Cancel();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.Escape && RequestTownRestart()) {
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
            if (!worldFrame.GetGlobalRect().HasPoint(mouse.Position))
                return;
            routeTravel.Cancel("Travel cancelled by manual input.");
            interactionTravel.Cancel();
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

            if (mouse.CtrlPressed) {
                QueueMoveTo(target);
                GetViewport().SetInputAsHandled();
                return;
            }
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
        routeTravel.Cancel("Travel cancelled by an interaction.");
        interactionTravel.Start(model.Snapshot, target.X, target.Y, interaction, client?.IsConnected == true);
        TrySubmitPendingWorldInteraction();
    }

    private void TrySubmitPendingWorldInteraction()
    {
        if (interactionTravel.Interaction is not { } interaction)
            return;
        var target = new Vector2I(interactionTravel.Target.X, interactionTravel.Target.Y);
        var player = model.Snapshot?.Players.FirstOrDefault();
        var command = interactionTravel.Next(model.Snapshot, model.LastCommandResult,
            client?.IsConnected == true, client?.PendingCommandCount ?? 0,
            IsPendingTargetPresent(target, interaction),
            player is not null && IsInteractionInRange(player, interaction, target));
        if (command is not null) {
            var sequence = Queue(command, automaticInput: true);
            if (interactionTravel.Active)
                interactionTravel.Submitted(sequence, player!);
        }
    }

    private bool IsPendingTargetPresent(Vector2I target, Command command)
    {
        return command.IntentCase switch {
            Command.IntentOneofCase.PickupWorldItemRequested =>
                world?.WorldItemEntityIdAt(target)
                    == command.PickupWorldItemRequested.ItemEntityId,
            Command.IntentOneofCase.UsePortalRequested =>
                AuthoritativeWorldDestinationSelector.IsPresent(model.Snapshot, target.X, target.Y, command),
            Command.IntentOneofCase.OperateObjectRequested =>
                world?.ObjectEntityIdAt(target)
                    == command.OperateObjectRequested.ObjectEntityId,
            Command.IntentOneofCase.DisarmTrapRequested =>
                world?.ObjectEntityIdAt(target)
                    == command.DisarmTrapRequested.ObjectEntityId,
            Command.IntentOneofCase.TurnInQuestRequested =>
                model.QuestTownerInteractionAt(target.X, target.Y) is { } interaction
                && interaction.QuestId == command.TurnInQuestRequested.QuestId
                && interaction.TownerId == command.TurnInQuestRequested.TownerId,
            Command.IntentOneofCase.TalkToQuestRequested =>
                AuthoritativeMonsterDialogueSelector.Select(
                    model.Snapshot,
                    model.PlayerEntityId,
                    target.X,
                    target.Y) is { } dialogue
                && dialogue.QuestId == command.TalkToQuestRequested.QuestId
                && dialogue.MonsterEntityId == command.TalkToQuestRequested.TargetEntityId,
            Command.IntentOneofCase.TalkToTownerRequested =>
                AuthoritativeWorldDestinationSelector.IsPresent(model.Snapshot, target.X, target.Y, command),
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
        if (source is not null) {
            hud?.ClearItemSelection();
            hud?.SetStatus($"Selected {source.DisplayName} (click a target to cast; Q/E cycles)");
        }
    }

    private void SelectSpellSource(string sourceKey)
    {
        var source = AuthoritativeSpellSourceSelector.Select(
            AuthoritativeSpellSourceSelector.GetSources(model.Snapshot),
            sourceKey);
        selectedSpellSourceKey = source?.Key;
        if (source is not null) {
            hud?.ClearItemSelection();
            hud?.SetStatus($"Selected {source.DisplayName} (click a target to cast)");
        }
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
        try {
            frameEvidence?.Stop("application-exit", succeeded: false);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            GD.PushWarning($"Could not write client frame evidence: {exception.Message}");
        }
        if (client is not null)
            await client.DisposeAsync();
        lifetime.Dispose();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try {
            await client!.ConnectAsync(cancellationToken);
            PersistLocalBookmark();
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        } catch (Exception exception) {
            Interlocked.Exchange(ref pendingConnectionError, exception.Message);
        }
    }

    private void StartReconnect(bool force)
    {
        routeTravel.Cancel("Travel stopped: connection lost.");
        interactionTravel.Cancel();
        model.ResetTransientPresentation();
        world?.MarkPresentationEpochPending();
        if (reconnectTask is null || reconnectTask.IsCompleted)
            reconnectTask = ReconnectLoopAsync(force);
    }

    private void PersistLocalBookmark()
    {
        if (!persistLocalSession || client is null)
            return;
        try {
            LocalSessionBookmark.Write(resumeTokenPath!, client.SessionToken);
            localSessionWarning = null;
        } catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) {
            localSessionWarning = $"Resume bookmark could not be saved. Keep this session open and fix access to {resumeTokenPath}. Retrying every 5 seconds. {exception.Message}";
        }
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

    private ulong Queue(Command command, Vector2I? predictedDirection = null, bool automaticInput = false)
    {
        if (!automaticInput) {
            routeTravel.Cancel("Travel cancelled by another action.");
            interactionTravel.Cancel();
        }
        var sequence = client!.QueueRealtime(command, model.CurrentTick);
        ObservedLastQueuedSequence = sequence;
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
