using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Audio and transient combat feedback driven by authoritative events. */
public partial class PresentationFeedback : Node
{
    private sealed class FloatingNumber(Label label, float remainingSeconds)
    {
        public Label Label { get; } = label;
        public float RemainingSeconds { get; set; } = remainingSeconds;
    }

    private readonly AudioStreamPlayer damage = new();
    private readonly AudioStreamPlayer healing = new();
    private readonly AudioStreamPlayer commandRejected = new();
    private readonly List<FloatingNumber> floatingNumbers = [];
    private ulong appliedEventSequence;
    private string? appliedCommandResult;

    public override void _Ready()
    {
        AddChild(damage);
        AddChild(healing);
        AddChild(commandRejected);
        damage.Stream = LoadOptional("res://assets/audio/damage.ogg");
        healing.Stream = LoadOptional("res://assets/audio/healing.ogg");
        commandRejected.Stream = LoadOptional("res://assets/audio/command_rejected.ogg");
    }

    public override void _Process(double delta)
    {
        for (var index = floatingNumbers.Count - 1; index >= 0; index--) {
            var floating = floatingNumbers[index];
            floating.RemainingSeconds -= (float)delta;
            floating.Label.Position += Vector2.Up * (float)(delta * 24);
            if (floating.RemainingSeconds > 0)
                continue;
            floating.Label.QueueFree();
            floatingNumbers.RemoveAt(index);
        }
    }

    public void Apply(AuthoritativeClientModel model, WorldPresentation? world = null)
    {
        if (appliedEventSequence < model.RecentEventsStartSequence)
            appliedEventSequence = model.RecentEventsStartSequence;
        if (appliedEventSequence > model.RecentEventsEndSequence)
            appliedEventSequence = model.RecentEventsEndSequence;
        var firstIndex = checked((int)(appliedEventSequence - model.RecentEventsStartSequence));
        for (var index = firstIndex; index < model.RecentEvents.Count; index++) {
            var gameEvent = model.RecentEvents[index];
            switch (gameEvent.EventCase) {
                case GameEvent.EventOneofCase.Damage:
                    damage.Play();
                    break;
                case GameEvent.EventOneofCase.Healing:
                    healing.Play();
                    break;
            }
            if (world is not null
                && AuthoritativeCombatFeedbackMapper.Resolve(gameEvent) is { } feedback
                && world.ScreenPositionForEntity(feedback.TargetEntityId) is { } position)
                AddFloatingNumber(feedback, position);
        }
        appliedEventSequence = model.RecentEventsEndSequence;

        var result = model.LastCommandResult;
        if (result is null)
            return;
        var resultKey = $"{result.ClientSequence}:{result.Status}:{result.AppliedTick}";
        if (resultKey == appliedCommandResult)
            return;
        appliedCommandResult = resultKey;
        if (!AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(result))
            commandRejected.Play();
    }

    private void AddFloatingNumber(
        AuthoritativeCombatFeedback feedback,
        Vector2 position)
    {
        var label = new Label {
            Text = feedback.Text,
            Position = position + new Vector2(-18, -22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 20,
        };
        label.Modulate = feedback.Kind switch {
            AuthoritativeCombatFeedbackKind.Damage => new Color("ff8a8a"),
            AuthoritativeCombatFeedbackKind.Healing => new Color("8aff9a"),
            _ => new Color("ffe38a"),
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        AddChild(label);
        floatingNumbers.Add(new FloatingNumber(label, 0.9f));
    }

    private static AudioStream? LoadOptional(string path)
    {
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
    }
}
