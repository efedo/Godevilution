using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class DialogueProtocolProjectionTests
{
    [Fact]
    public void ProjectsServerOwnedTextAndPreservesNumericIdentity()
    {
        var catalog = new AuthoritativeDialogueMessageCatalog([
            new(110, "TEXT_WARLRD9", "My blade sings."),
        ]);
        var batch = DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                12,
                34,
                0) {
                QuestId = 3012,
                MessageId = 110,
            },
        ], 12, 7, catalog);

        Assert.NotNull(batch);
        var dialogue = Assert.Single(batch!.Events).Dialogue;
        Assert.Equal(3012U, dialogue.QuestId);
        Assert.Equal(110, dialogue.MessageId);
        Assert.Equal("My blade sings.", dialogue.Text);
    }

    [Fact]
    public void OmittingCatalogKeepsIdOnlyCompatibility()
    {
        var batch = DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                0,
                34,
                0) {
                MessageId = 21,
            },
        ], 1, 1);

        Assert.NotNull(batch);
        Assert.Equal(string.Empty, Assert.Single(batch!.Events).Dialogue.Text);
    }
}
