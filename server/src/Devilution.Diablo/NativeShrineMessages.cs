namespace Devilution.Diablo;

/** Native ui/diablo_msg.cpp MsgStrings, EMSG_SHRINE_MYSTERIOUS through EMSG_SHRINE_TAINTED2. */
internal static class NativeShrineMessages
{
    private static readonly string[] Messages = [
        "Some are weakened as one grows strong",
        "New strength is forged through destruction",
        "Those who defend seldom attack",
        "The sword of justice is swift and sharp",
        "While the spirit is vigilant the body thrives",
        "The powers of mana refocused renews",
        "Time cannot diminish the power of steel",
        "Magic is not always what it seems to be",
        "What once was opened now is closed",
        "Intensity comes at the cost of wisdom",
        "Arcane power brings destruction",
        "That which cannot be held cannot be harmed",
        "Crimson and Azure become as the sun",
        "Knowledge and wisdom at the cost of self",
        "Drink and be refreshed",
        "Wherever you go, there you are",
        "Energy comes at the cost of wisdom",
        "Riches abound when least expected",
        "Where avarice fails, patience gains reward",
        "Blessed by a benevolent companion!",
        "The hands of men may be guided by fate",
        "Strength is bolstered by heavenly faith",
        "The essence of life flows from within",
        "The way is made clear when viewed from above",
        "Salvation comes at the cost of wisdom",
        "Mysteries are revealed in the light of reason",
        "Those who are last may yet be first",
        "Generosity brings its own rewards",
    ];

    internal static string Get(int id) => Messages[id - 12];
}
