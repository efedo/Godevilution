using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Formats server-owned dialogue identities without embedding native text tables in the client. */
public static class AuthoritativeDialoguePresentation
{
    public static string Format(DialogueEvent dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        if (!string.IsNullOrWhiteSpace(dialogue.Text))
            return dialogue.Text;
        return dialogue.QuestId == 0
            ? $"Dialogue message {dialogue.MessageId}"
            : $"Dialogue quest {dialogue.QuestId}, message {dialogue.MessageId}";
    }
}
