using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeCombatFeedbackTests
{
    [Fact]
    public void UsesExactQ6DamageWhenAvailable()
    {
        var feedback = AuthoritativeCombatFeedbackMapper.Resolve(new GameEvent {
            Damage = new DamageEvent {
                TargetEntityId = 9,
                Amount = 3,
                AmountQ6 = 160,
            },
        });

        Assert.NotNull(feedback);
        Assert.Equal(9U, feedback.TargetEntityId);
        Assert.Equal("-2.5", feedback.Text);
        Assert.Equal(AuthoritativeCombatFeedbackKind.Damage, feedback.Kind);
    }

    [Fact]
    public void FallsBackToLegacyWholePointHealing()
    {
        var feedback = AuthoritativeCombatFeedbackMapper.Resolve(new GameEvent {
            Healing = new HealingEvent {
                TargetEntityId = 4,
                Amount = 7,
            },
        });

        Assert.NotNull(feedback);
        Assert.Equal("+7", feedback.Text);
        Assert.Equal(AuthoritativeCombatFeedbackKind.Healing, feedback.Kind);
    }

    [Fact]
    public void ProjectsExperienceAsPlayerFeedback()
    {
        var feedback = AuthoritativeCombatFeedbackMapper.Resolve(new GameEvent {
            Experience = new ExperienceEvent {
                PlayerEntityId = 3,
                Amount = 25,
            },
        });

        Assert.NotNull(feedback);
        Assert.Equal(3U, feedback.TargetEntityId);
        Assert.Equal("+25 XP", feedback.Text);
        Assert.Equal(AuthoritativeCombatFeedbackKind.Experience, feedback.Kind);
    }

    [Fact]
    public void IgnoresNonCombatEvents()
    {
        Assert.Null(AuthoritativeCombatFeedbackMapper.Resolve(new GameEvent {
            Dialogue = new DialogueEvent { MessageId = 12 },
        }));
    }
}
