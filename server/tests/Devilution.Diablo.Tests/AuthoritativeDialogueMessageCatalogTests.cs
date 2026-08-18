using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeDialogueMessageCatalogTests
{
    [Fact]
    public void TsvLoaderDecodesNativeLineBreakEscapesAndOrdersByIdentity()
    {
        var catalog = AuthoritativeDialogueMessageCatalog.LoadTsv(
            "dialogue_messages.tsv",
            "message_id\tmessage_symbol\ttext\n"
            + "23\tTEXT_BANNER12\tSecond line\n"
            + "21\tTEXT_BANNER10\tFirst\\nSecond\n");

        Assert.Equal([21, 23], catalog.Definitions.Select(message => message.MessageId));
        Assert.True(catalog.TryGet(21, out var message));
        Assert.Equal("First\nSecond", message.Text);
    }

    [Fact]
    public void ValidationRejectsDuplicateIdentitiesAndUnknownRoutes()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeDialogueMessageCatalog([
            new(21, "TEXT_ONE", "One"),
            new(21, "TEXT_TWO", "Two"),
        ]));

        var catalog = new AuthoritativeDialogueMessageCatalog([
            new(21, "TEXT_ONE", "One"),
        ]);
        Assert.Throws<InvalidDataException>(() => catalog.ValidateMessageIds([21, 99]));
    }
}
