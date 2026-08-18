using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** One server-owned, localized-independent dialogue message definition. */
public sealed record AuthoritativeDialogueMessage(
    int MessageId,
    string Symbol,
    string Text);

/** Validated message text supplied by the active external content pack. */
public sealed class AuthoritativeDialogueMessageCatalog
{
    private const int MaximumTextBytes = 16 * 1024;
    private readonly IReadOnlyDictionary<int, AuthoritativeDialogueMessage> definitionsById;

    public AuthoritativeDialogueMessageCatalog(
        IEnumerable<AuthoritativeDialogueMessage> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition =>
                definition.MessageId is <= 0 or > short.MaxValue
                || string.IsNullOrWhiteSpace(definition.Symbol)
                || string.IsNullOrWhiteSpace(definition.Text)
                || Encoding.UTF8.GetByteCount(definition.Text) > MaximumTextBytes
                || definition.Text.Any(character =>
                    character is '\0' or '\r'))) {
            throw new InvalidDataException(
                "Dialogue messages contain invalid IDs, symbols, text, or lengths.");
        }
        if (values.GroupBy(definition => definition.MessageId)
                .Any(group => group.Count() != 1)
            || values.GroupBy(definition => definition.Symbol, StringComparer.Ordinal)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Dialogue messages contain duplicate IDs or symbols.");
        }

        var ordered = values
            .OrderBy(definition => definition.MessageId)
            .ToArray();
        definitionsById = ordered.ToDictionary(
            definition => definition.MessageId,
            definition => definition);
        Definitions = ordered;
    }

    public IReadOnlyList<AuthoritativeDialogueMessage> Definitions { get; }

    public bool TryGet(
        int messageId,
        out AuthoritativeDialogueMessage definition)
        => definitionsById.TryGetValue(messageId, out definition!);

    /** Ensures every message identity emitted by an authored route is shipped. */
    public void ValidateMessageIds(IEnumerable<int> messageIds)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        foreach (var messageId in messageIds.Where(messageId => messageId != 0).Distinct()) {
            if (!TryGet(messageId, out _))
                throw new InvalidDataException(
                    $"Dialogue route references unknown message {messageId}.");
        }
    }

    public static AuthoritativeDialogueMessageCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeDialogueMessageCatalog(table.Rows.Select(row =>
            new AuthoritativeDialogueMessage(
                row.RequiredInt32("message_id"),
                row.Required("message_symbol"),
                DecodeText(row.Required("text")))));
    }

    private static string DecodeText(string value)
        => value.Replace("\\n", "\n", StringComparison.Ordinal);
}
