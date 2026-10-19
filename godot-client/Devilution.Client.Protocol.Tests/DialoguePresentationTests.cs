using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class DialoguePresentationTests
{
    [Fact]
    public void FormatsQuestDialogueWithoutEmbeddingNativeTextTables()
    {
        var dialogue = new DialogueEvent {
            QuestId = 3012,
            MessageId = 110,
        };

        Assert.Equal(
            "Dialogue quest 3012, message 110",
            AuthoritativeDialoguePresentation.Format(dialogue));
    }

    [Fact]
    public void FormatsNonQuestDialogueByMessageIdentity()
    {
        Assert.Equal(
            "Dialogue message 21",
            AuthoritativeDialoguePresentation.Format(new DialogueEvent { MessageId = 21 }));
    }

    [Fact]
    public void PrefersServerOwnedTextWhenPresent()
    {
        Assert.Equal(
            "My blade sings.",
            AuthoritativeDialoguePresentation.Format(new DialogueEvent {
                MessageId = 110,
                Text = "My blade sings.",
            }));
    }
}
