using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeEventBatchValidatorTests
{
    [Fact]
    public void AcceptsKnownEventsWithLegacyZeroIdentityValues()
    {
        AuthoritativeEventBatchValidator.Validate(new EventBatch {
            Events = {
                new GameEvent { Damage = new DamageEvent() },
                new GameEvent { Experience = new ExperienceEvent() },
                new GameEvent { Healing = new HealingEvent() },
                new GameEvent { Dialogue = new DialogueEvent() },
                new GameEvent { Victory = new VictoryEvent() },
                new GameEvent { MapOpened = new MapOpenedEvent() },
            },
        });
    }

    [Fact]
    public void RejectsMalformedOrOversizedBatches()
    {
        foreach (var invalidEvent in new[] {
            new GameEvent { Damage = new DamageEvent { Amount = -1 } },
            new GameEvent { Damage = new DamageEvent { AmountQ6 = -1 } },
            new GameEvent { Damage = new DamageEvent { Amount = int.MaxValue / 64 + 1 } },
            new GameEvent { Healing = new HealingEvent { Amount = -1 } },
            new GameEvent { Healing = new HealingEvent { AmountQ6 = -1 } },
            new GameEvent { Dialogue = new DialogueEvent { MessageId = -1 } },
            new GameEvent { Dialogue = new DialogueEvent { Text = "bad\0text" } },
            new GameEvent { Dialogue = new DialogueEvent { Text = "bad\rtext" } },
            new GameEvent { Dialogue = new DialogueEvent { Text = new string('x', AuthoritativeEventBatchValidator.MaximumDialogueTextBytes + 1) } },
            new GameEvent { Victory = new VictoryEvent { DiabloKillLevel = byte.MaxValue + 1U } },
            new GameEvent(),
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeEventBatchValidator.Validate(new EventBatch {
                    Events = { invalidEvent },
                }));

        var oversized = new EventBatch();
        for (var index = 0; index <= AuthoritativeEventBatchValidator.MaximumEventBatchCount; index++)
            oversized.Events.Add(new GameEvent {
                Experience = new ExperienceEvent { Amount = (uint)index },
            });
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEventBatchValidator.Validate(oversized));
    }
}
