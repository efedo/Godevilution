using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf.Collections;
using Godot;

namespace Devilution.Client;

public partial class HudPresentation
{
    private readonly PresentationChangeGate<HudInputs> hudChanges = new();
    private readonly PresentationChangeGate<(RepeatedField<ItemSnapshot>?, RepeatedField<int>?, int)> inventoryChanges = new();
    private readonly PresentationChangeGate<(RepeatedField<BeltItemSnapshot>?, uint?)> beltChanges = new();
    private readonly PresentationChangeGate<(RepeatedField<EquippedItemSnapshot>?, uint?)> equipmentChanges = new();
    private readonly PresentationChangeGate<(RepeatedField<ItemSnapshot>?, RepeatedField<EquippedItemSnapshot>?, PlayerItemReference?)> oilChanges = new();
    private readonly PresentationChangeGate<(uint, RepeatedField<StoreItemSnapshot>?, uint?)> storeChanges = new();
    private readonly PresentationChangeGate<(RepeatedField<StashItemSnapshot>?, bool, uint, uint, int)> stashChanges = new();
    private readonly PresentationChangeGate<(IReadOnlyList<AuthoritativeSpellSource>, string?, bool)> spellChanges = new();
    private readonly PresentationChangeGate<(int, int, int, ItemSnapshot?)> inventoryCaptionChanges = new();
    private readonly PresentationChangeGate<(bool, uint, PlayerAttributesSnapshot?)> attributeChanges = new();
    private readonly PresentationChangeGate<(bool, bool, uint, RepeatedField<StatusEffectSnapshot>?)> statusChanges = new();
    private readonly PresentationChangeGate<(RepeatedField<BeltItemSnapshot>?, bool, bool, bool, bool, PlayerCombatMode)> quickBeltChanges = new();
    private readonly PresentationChangeGate<(RepeatedField<ObjectSnapshot>?, RepeatedField<QuestSnapshot>?, ulong, ulong)> dialogChanges = new();
    private readonly PresentationChangeGate<CommandResult?> commandChanges = new();
    private bool? appliedHighContrast;
    private long clockSecond = -1;
    private string? lastClockText;
    private string? lastStatusText;
    private string? lastSessionWarningText;
    private (bool Active, string Message)? lastTravelStatus;
    private bool layoutDirty = true;
    private Vector2 lastLayoutSize;

    private bool NeedsHudApply(AuthoritativeClientModel model, AuthoritativeClient? client, Task? connection)
        => hudChanges.ShouldUpdate(true, new(model, client, connection, model.AcceptedSnapshotRevision, model.LastCommandResult,
            model.RecentEventsStartSequence, model.RecentEventsEndSequence, model.LastError,
            client?.IsConnected == true, client?.PendingCommandCount ?? 0, client?.RetryTimeout.Ticks ?? 0,
            connection?.Status, model.IsCorrectingPosition, sections.CurrentTab, diagnosticsToggle.ButtonPressed,
            selectedInventoryIndex, selectedBeltSlot, selectedEquipmentSlot, selectedStoreSlot,
            selectedStashItemIndex, stashPage, selectedOilTarget, selectedSpellText, sessionMenuTransition));

    private void UpdateHudClock()
    {
        var now = DateTimeOffset.Now;
        var second = now.ToUnixTimeSeconds();
        if (second == clockSecond) return;
        clockSecond = second;
        var text = ClockPresentation.Format(now);
        if (text == lastClockText) return;
        lastClockText = text;
        clock.Text = text;
    }

    private void ApplyPlayerLabels(PlayerSnapshot? player)
    {
        if (statusChanges.ShouldUpdate(statusEffects.IsVisibleInTree(),
                (player is not null, player?.ManaShieldActive ?? false, player?.ReflectCharges ?? 0, player?.StatusEffects))) {
            var presentation = player is null ? null : StatusPresentationStyle.ResolvePlayer(player);
            statusEffects.Text = presentation is null
                ? "Statuses: waiting for authoritative snapshot"
                : $"Statuses: {StatusPresentationStyle.FormatDetailed(presentation.Badges)}";
        }
        if (attributeChanges.ShouldUpdate(attributes.IsVisibleInTree(), (player is not null, player?.StatPoints ?? 0, player?.Attributes)))
            attributes.Text = FormatAttributes(player);
    }

    private bool NeedsLayout()
    {
        var size = GetViewport().GetVisibleRect().Size;
        if (!layoutDirty && size == lastLayoutSize) return false;
        layoutDirty = false;
        lastLayoutSize = size;
        return true;
    }

    private readonly record struct HudInputs(object Model, object? Client, Task? ConnectionTask, ulong Revision, CommandResult? Command,
        ulong EventStart, ulong EventEnd, string? Error, bool Connected, int Pending, long Retry,
        TaskStatus? Connection, bool Correcting, int Tab, bool Diagnostics, int Inventory, uint? Belt,
        uint? Equipment, uint? Store, int Stash, uint StashPage, PlayerItemReference? Oil, string Spell,
        bool SessionTransition);
}
