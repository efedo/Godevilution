using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class MonsterPresentationStyleTests
{
    [Fact]
    public void MarksAnAliveUniqueMonsterWithDialogueAsInteractive()
    {
        var monster = new MonsterSnapshot {
            Alive = true,
            IsUnique = true,
            DialogueMessageId = 145,
        };

        Assert.Equal(MonsterPresentationKind.Dialogue, MonsterPresentationStyle.Resolve(monster));
    }

    [Fact]
    public void MarksAnAlivePlayerMinionAsOwnedPresentation()
    {
        var monster = new MonsterSnapshot {
            Alive = true,
            IsPlayerMinion = true,
            OwnerPlayerId = 7,
        };

        Assert.Equal(MonsterPresentationKind.PlayerMinion, MonsterPresentationStyle.Resolve(monster));
    }

    [Fact]
    public void PlayerMinionPresentationTakesPrecedenceOverDialogueMetadata()
    {
        var monster = new MonsterSnapshot {
            Alive = true,
            IsPlayerMinion = true,
            OwnerPlayerId = 7,
            IsUnique = true,
            DialogueMessageId = 145,
        };

        Assert.Equal(MonsterPresentationKind.PlayerMinion, MonsterPresentationStyle.Resolve(monster));
    }

    [Theory]
    [InlineData(false, true, 145)]
    [InlineData(true, false, 145)]
    [InlineData(true, true, 0)]
    public void UsesGenericPresentationWhenDialogueIsUnavailable(
        bool alive,
        bool unique,
        int dialogueMessageId)
    {
        var monster = new MonsterSnapshot {
            Alive = alive,
            IsUnique = unique,
            DialogueMessageId = dialogueMessageId,
        };

        Assert.Equal(MonsterPresentationKind.Generic, MonsterPresentationStyle.Resolve(monster));
    }
}
