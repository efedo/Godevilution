using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/** Cathedral population rules from open txtdata and native source constants. */
public static class NativeSourceRuleExport
{
    public const int CathedralDepthCount = 4;

    // monsters.cpp InitMonsters: MaxMonsters (200) minus the 10 reserved slots.
    public const int OrdinaryMonsterCapacity = 200 - 10;

    // objects.cpp StoryText[3][3]; the population records each book's first entry.
    private static readonly string[] StoryBookSpeech = ["TEXT_BOOK11", "TEXT_BOOK21", "TEXT_BOOK31"];

    public static IReadOnlyList<AuthoritativeCathedralPopulationDepthRule> DepthRules(string textDataRoot)
    {
        var table = Load(textDataRoot, "levels", "level_generation.tsv");
        return Enumerable.Range(1, CathedralDepthCount).Select(depth => {
            // leveldat.cpp FindRow: the first row whose range contains the level.
            var row = table.Rows.FirstOrDefault(candidate =>
                candidate.RequiredInt32("minLevel") <= depth && depth <= candidate.RequiredInt32("maxLevel"));
            var divisor = row?.RequiredInt32("monsterDensityDivisor") ?? 30;
            var trapPercent = row?.RequiredInt32("trapPercent") ?? 0;
            // The Cathedral metadata does not store the multiplayer multiplier; the encounter generator assumes the native value.
            if ((row?.RequiredInt32("mpMonsterMultiplierPercent") ?? 150)
                != AuthoritativeCathedralEncounterParameters.NativeMultiplayerMonsterMultiplierPercent)
                throw new InvalidDataException($"Cathedral depth {depth} has a non-native multiplayer monster multiplier.");
            return new AuthoritativeCathedralPopulationDepthRule(depth, divisor, trapPercent, OrdinaryMonsterCapacity);
        }).ToArray();
    }

    /** Speech ids follow textdat.tsv row order. */
    public static IReadOnlyList<int> StoryMessages(string textDataRoot) => SpeechIds(textDataRoot, StoryBookSpeech);

    /** Catacombs levels 5-8 (leveldat.cpp FindRow), with the multiplayer multiplier the Catacombs metadata stores. */
    public static IReadOnlyList<AuthoritativeCatacombsPopulationDepthRule> CatacombsDepthRules(string textDataRoot)
    {
        var table = Load(textDataRoot, "levels", "level_generation.tsv");
        return Enumerable.Range(5, 4).Select(depth => {
            var row = table.Rows.FirstOrDefault(candidate =>
                candidate.RequiredInt32("minLevel") <= depth && depth <= candidate.RequiredInt32("maxLevel"))
                ?? throw new InvalidDataException($"level_generation.tsv has no row for depth {depth}.");
            return new AuthoritativeCatacombsPopulationDepthRule(depth, row.RequiredInt32("monsterDensityDivisor"),
                row.RequiredInt32("mpMonsterMultiplierPercent"), row.RequiredInt32("trapPercent"), OrdinaryMonsterCapacity);
        }).ToArray();
    }

    /** objects.cpp StoryText: on L8 (Catacombs) each book uses its second entry. */
    public static IReadOnlyList<int> CatacombsStoryMessages(string textDataRoot)
        => SpeechIds(textDataRoot, ["TEXT_BOOK12", "TEXT_BOOK22", "TEXT_BOOK32"]);

    private static IReadOnlyList<int> SpeechIds(string textDataRoot, IEnumerable<string> names)
    {
        var table = Load(textDataRoot, "text", "textdat.tsv");
        var ids = table.Rows.Select((row, index) => (Id: row.Values[table.Columns[0]], Index: index))
            .ToDictionary(row => row.Id, row => row.Index, StringComparer.Ordinal);
        return names.Select(name => ids.TryGetValue(name, out var id)
            ? id
            : throw new InvalidDataException($"textdat.tsv has no {name} row.")).ToArray();
    }

    /** CreateShrine: single-player excludes multiplayer-only shrines; Enchanted is allowed in Cathedral. */
    public static IReadOnlyList<bool> SinglePlayerShrineAvailability()
        => AuthoritativeNativeShrineAvailability.SinglePlayer.ToArray();

    internal static TsvTable Load(string textDataRoot, params string[] parts)
    {
        var path = Path.Combine([textDataRoot, .. parts]);
        return TsvTable.Parse(string.Join('/', parts), File.ReadAllText(path));
    }
}
