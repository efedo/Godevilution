using System.Globalization;

namespace Devilution.Tools.NativePlayerArt;

public static class NativePlayerArtNames
{
    public static readonly string[] ArmorNames = ["light", "medium", "heavy"];
    public static readonly string[] WeaponNames = ["unarmed", "unarmed-shield", "sword", "sword-shield", "bow", "axe", "mace", "mace-shield", "staff"];
    public const string TownLocation = "town";
    public const string CathedralLocation = "cathedral";
}

public sealed record NativePlayerClipMetadata(
    string Location,
    string Graphic,
    string SourceSuffix,
    int FramesPerDirection,
    int ActionFrame,
    int TicksPerFrame,
    int FrameWidth);

public sealed record NativePlayerClassMetadata(
    uint ClassId,
    string Name,
    string FolderName,
    string SpritePath,
    char ClassCharacter,
    string ClassTranslation,
    IReadOnlyDictionary<string, int> SpriteWidths,
    IReadOnlyDictionary<string, int> AnimationValues)
{
    public int GetWidth(string graphic, string weapon)
    {
        if (graphic is "attack" && weapon is "bow")
            return SpriteWidths["bow"];
        return graphic switch {
            "town-stand" or "stand" => SpriteWidths["stand"],
            "town-walk" or "walk" => SpriteWidths["walk"],
            "attack" => SpriteWidths["attack"],
            "hit" => SpriteWidths["swHit"],
            "block" => SpriteWidths["block"],
            "lightning" => SpriteWidths["lightning"],
            "fire" => SpriteWidths["fire"],
            "magic" => SpriteWidths["magic"],
            "death" => SpriteWidths["death"],
            _ => throw new InvalidDataException($"Unsupported native player graphic '{graphic}'."),
        };
    }

    public int GetFrames(string graphic, string weapon, string armor)
    {
        var warriorBowIdleOverride = ClassId == 0 && weapon == "bow" && graphic == "stand";
        var warriorArmoredDeathOverride = ClassId == 0 && graphic == "death" && armor != "light";
        if (warriorBowIdleOverride)
            return 8;
        if (warriorArmoredDeathOverride)
            return 15;

        return graphic switch {
            "town-stand" => AnimationValues["townIdleFrames"],
            "town-walk" => AnimationValues["townWalkingFrames"],
            "stand" => AnimationValues["idleFrames"],
            "walk" => AnimationValues["walkingFrames"],
            "attack" => AnimationValues[$"{AttackAnimationPrefix(weapon)}Frames"],
            "hit" => AnimationValues["recoveryFrames"],
            "block" => AnimationValues["blockingFrames"],
            "lightning" or "fire" or "magic" => AnimationValues["castingFrames"],
            "death" => AnimationValues["deathFrames"],
            _ => throw new InvalidDataException($"Unsupported native player graphic '{graphic}'."),
        };
    }

    public int GetActionFrame(string graphic, string weapon)
    {
        return graphic switch {
            "attack" => AnimationValues[$"{AttackAnimationPrefix(weapon)}ActionFrame"],
            "lightning" or "fire" or "magic" => AnimationValues["castingActionFrame"],
            _ => 0,
        };
    }

    public static string NormalizeWeapon(string location, string graphic, string weapon)
    {
        if (graphic == "death")
            return "unarmed";
        if (location == NativePlayerArtNames.TownLocation && graphic is "lightning" or "fire" or "magic") {
            return weapon switch {
                "sword" or "mace" => "unarmed",
                "sword-shield" or "mace-shield" => "unarmed-shield",
                _ => weapon,
            };
        }
        return weapon;
    }

    public static IReadOnlyList<NativePlayerClipMetadata> CreateClipMetadata(
        NativePlayerClassMetadata playerClass,
        string location,
        string graphic,
        string sourceSuffix,
        string weapon,
        string armor)
    {
        var normalizedWeapon = NormalizeWeapon(location, graphic, weapon);
        var frames = playerClass.GetFrames(graphic, normalizedWeapon, armor);
        if (frames is < 1 or > NativeCl2SpriteSheetDecoder.MaximumFrameCountPerDirection)
            throw new InvalidDataException($"Class {playerClass.Name} {graphic} frame count is outside the supported bounds.");
        var actionFrame = playerClass.GetActionFrame(graphic, normalizedWeapon);
        if (actionFrame < 0 || actionFrame > frames)
            throw new InvalidDataException($"Class {playerClass.Name} {graphic} action frame is outside its frame range.");
        var ticksPerFrame = graphic switch {
            "town-stand" or "stand" => 4,
            "death" => 2,
            "block" => 3,
            _ => 1,
        };
        return [new NativePlayerClipMetadata(
            location,
            graphic,
            sourceSuffix,
            frames,
            actionFrame,
            ticksPerFrame,
            playerClass.GetWidth(graphic, normalizedWeapon))];
    }

    private static string AttackAnimationPrefix(string weapon) => weapon switch {
        "unarmed" => "unarmed",
        "unarmed-shield" => "unarmedShield",
        "sword" => "sword",
        "sword-shield" => "swordShield",
        "bow" => "bow",
        "axe" => "axe",
        "mace" => "mace",
        "mace-shield" => "maceShield",
        "staff" => "staff",
        _ => throw new InvalidDataException($"Unsupported native player weapon '{weapon}'."),
    };
}

public sealed record NativePlayerSpellGraphic(uint SpellId, string Graphic);

/** Strict reader for the repository's native class, animation, spell and runtime-spell binding TSVs. */
public sealed class NativePlayerArtMetadata
{
    private static readonly string[] RequiredSpriteWidths = [
        "stand", "walk", "attack", "bow", "swHit", "block", "lightning", "fire", "magic", "death",
    ];
    private static readonly string[] RequiredAnimationValues = [
        "unarmedFrames", "unarmedActionFrame", "unarmedShieldFrames", "unarmedShieldActionFrame",
        "swordFrames", "swordActionFrame", "swordShieldFrames", "swordShieldActionFrame",
        "bowFrames", "bowActionFrame", "axeFrames", "axeActionFrame", "maceFrames", "maceActionFrame",
        "maceShieldFrames", "maceShieldActionFrame", "staffFrames", "staffActionFrame", "idleFrames",
        "walkingFrames", "blockingFrames", "deathFrames", "castingFrames", "recoveryFrames",
        "townIdleFrames", "townWalkingFrames", "castingActionFrame",
    ];

    private NativePlayerArtMetadata(
        IReadOnlyList<NativePlayerClassMetadata> classes,
        IReadOnlyList<NativePlayerSpellGraphic> spells,
        IReadOnlyList<string> diagnostics)
    {
        Classes = classes;
        Spells = spells;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<NativePlayerClassMetadata> Classes { get; }
    public IReadOnlyList<NativePlayerSpellGraphic> Spells { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    public static NativePlayerArtMetadata Load(string textDataRoot, string gameContentDirectory)
    {
        var root = Path.GetFullPath(textDataRoot);
        var gameRoot = Path.GetFullPath(gameContentDirectory);
        var classRows = ReadTable(Path.Combine(root, "classes", "classdat.tsv"), "className", "folderName", "portrait", "inv");
        var expectedFolders = new[] { "warrior", "rogue", "sorcerer" };
        if (classRows.Count < expectedFolders.Length)
            throw new InvalidDataException("classdat.tsv does not contain all three base classes.");

        var classes = new List<NativePlayerClassMetadata>(3);
        for (var id = 0; id < expectedFolders.Length; id++) {
            var row = classRows[id];
            var folder = row["folderName"];
            if (!string.Equals(folder, expectedFolders[id], StringComparison.Ordinal))
                throw new InvalidDataException($"Base class ID {id} must be '{expectedFolders[id]}', found '{folder}'.");

            var spriteRows = ReadPairs(Path.Combine(root, "classes", folder, "sprites.tsv"));
            var animationRows = ReadPairs(Path.Combine(root, "classes", folder, "animations.tsv"));
            var widths = RequiredSpriteWidths.ToDictionary(key => key, key => ParseBoundedInt(spriteRows, key, 1, 255), StringComparer.Ordinal);
            var animationValues = RequiredAnimationValues.ToDictionary(key => key, key => ParseBoundedInt(animationRows, key, 0, 255), StringComparer.Ordinal);
            var spritePath = ReadRequired(spriteRows, "classPath");
            var classCharacterValue = ReadRequired(spriteRows, "classChar");
            var classTranslation = ReadRequired(spriteRows, "trn");
            if (classCharacterValue.Length != 1 || !char.IsAsciiLetter(classCharacterValue[0]))
                throw new InvalidDataException($"{folder}/sprites.tsv classChar must be one ASCII letter.");
            ValidatePathSegment(spritePath, $"{folder}/sprites.tsv classPath");
            ValidatePathSegment(classTranslation, $"{folder}/sprites.tsv trn");

            classes.Add(new NativePlayerClassMetadata(
                checked((uint)id),
                row["className"],
                folder,
                spritePath,
                classCharacterValue[0],
                classTranslation,
                widths,
                animationValues));
        }

        var nativeSpellSymbols = ReadNativeSpellSymbols(Path.Combine(gameRoot, "native_spells.tsv"));
        var activeNativeSpells = ReadActiveNativeSpells(Path.Combine(root, "spells", "spelldat.tsv"), nativeSpellSymbols);
        var runtimeSpells = ReadRuntimeSpellRows(Path.Combine(gameRoot, "spells.tsv"));
        var diagnostics = new List<string>();
        var spellGraphics = new List<NativePlayerSpellGraphic>();
        var spellIds = new HashSet<uint>();
        foreach (var runtimeSpell in runtimeSpells) {
            if (!spellIds.Add(runtimeSpell.SpellId))
                throw new InvalidDataException($"Duplicate runtime spell ID {runtimeSpell.SpellId}.");
            if (!nativeSpellSymbols.TryGetValue(runtimeSpell.NativeSpellId, out var nativeSymbol))
                throw new InvalidDataException($"Runtime spell {runtimeSpell.SpellId} references unknown native spell ID {runtimeSpell.NativeSpellId}.");
            if (!activeNativeSpells.TryGetValue(nativeSymbol, out var graphic)) {
                diagnostics.Add($"spellId={runtimeSpell.SpellId} nativeSpellId={runtimeSpell.NativeSpellId} symbol={nativeSymbol} has no active base spelldat.tsv row; no player graphic mapping emitted.");
                continue;
            }
            spellGraphics.Add(new NativePlayerSpellGraphic(runtimeSpell.SpellId, graphic));
        }

        return new NativePlayerArtMetadata(classes, spellGraphics, diagnostics.AsReadOnly());
    }

    private static Dictionary<uint, string> ReadNativeSpellSymbols(string path)
    {
        var rows = ReadTable(path, "native_spell_id", "native_spell_symbol");
        var result = new Dictionary<uint, string>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows) {
            var id = ParseBoundedUInt(row["native_spell_id"], 0, 255, path);
            var symbol = row["native_spell_symbol"];
            ValidatePathSegment(symbol, "native spell symbol");
            if (!result.TryAdd(id, symbol) || !symbols.Add(symbol))
                throw new InvalidDataException($"{path} contains a duplicate native spell identity.");
        }
        return result;
    }

    private static Dictionary<string, string> ReadActiveNativeSpells(string path, IReadOnlyDictionary<uint, string> nativeSpellSymbols)
    {
        var rows = ReadTable(path, "id", "name", "soundId", "bookCost10", "staffCost10", "manaCost", "flags", "bookLevel", "staffLevel", "minIntelligence", "missiles", "manaMultiplier", "minMana", "staffMin", "staffMax");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Count; index++) {
            var row = rows[index];
            var symbol = row["id"];
            var nativeSpellId = checked((uint)index + 1);
            if (!nativeSpellSymbols.TryGetValue(nativeSpellId, out var expectedSymbol)
                || !string.Equals(symbol, expectedSymbol, StringComparison.Ordinal))
                throw new InvalidDataException($"{path} row {index + 2} does not match native SpellID {nativeSpellId}.");
            var flags = row["flags"].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var types = flags.Where(flag => flag is "Fire" or "Lightning" or "Magic").Distinct(StringComparer.Ordinal).ToArray();
            if (types.Length != 1)
                throw new InvalidDataException($"{path} row {index + 2} must have exactly one native MagicType flag.");
            var graphic = types[0].ToLowerInvariant();
            if (!result.TryAdd(symbol, graphic))
                throw new InvalidDataException($"{path} contains duplicate spell symbol '{symbol}'.");
        }
        return result;
    }

    private static IReadOnlyList<RuntimeSpellRow> ReadRuntimeSpellRows(string path)
    {
        var rows = ReadTableWithRequiredColumns(path, "spell_id", "spell_symbol", "native_spell_id");
        var result = new List<RuntimeSpellRow>(rows.Count);
        foreach (var row in rows) {
            var spellId = ParseBoundedUInt(row["spell_id"], 1, uint.MaxValue, path);
            var nativeId = ParseBoundedUInt(row["native_spell_id"], 0, 255, path);
            if (string.IsNullOrWhiteSpace(row["spell_symbol"]))
                throw new InvalidDataException($"{path} contains a spell without a stable symbol.");
            result.Add(new RuntimeSpellRow(spellId, nativeId));
        }
        return result;
    }

    private static List<Dictionary<string, string>> ReadTableWithRequiredColumns(string path, params string[] requiredColumns)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Required metadata file is missing.", path);
        var lines = File.ReadAllLines(path);
        if (lines.Length < 1)
            throw new InvalidDataException($"Metadata file '{path}' is empty.");
        var header = lines[0].Split('\t');
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Length
            || requiredColumns.Any(required => !header.Contains(required, StringComparer.Ordinal)))
            throw new InvalidDataException($"Metadata file '{path}' is missing required unique columns.");

        var rows = new List<Dictionary<string, string>>();
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) {
            if (lines[lineIndex].Length == 0)
                continue;
            var values = lines[lineIndex].Split('\t');
            if (values.Length != header.Length)
                throw new InvalidDataException($"Metadata file '{path}' row {lineIndex + 1} has {values.Length} columns; expected {header.Length}.");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < header.Length; column++)
                row.Add(header[column], values[column]);
            rows.Add(row);
        }
        return rows;
    }
    private static List<Dictionary<string, string>> ReadTable(string path, params string[] expectedColumns)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Required metadata file is missing.", path);
        var lines = File.ReadAllLines(path);
        if (lines.Length < 1)
            throw new InvalidDataException($"Metadata file '{path}' is empty.");
        var header = lines[0].Split('\t');
        if (!header.SequenceEqual(expectedColumns, StringComparer.Ordinal))
            throw new InvalidDataException($"Metadata file '{path}' has an unexpected ordered header.");

        var rows = new List<Dictionary<string, string>>();
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) {
            if (lines[lineIndex].Length == 0)
                continue;
            var values = lines[lineIndex].Split('\t');
            if (values.Length != expectedColumns.Length)
                throw new InvalidDataException($"Metadata file '{path}' row {lineIndex + 1} has {values.Length} columns; expected {expectedColumns.Length}.");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < expectedColumns.Length; column++)
                row.Add(expectedColumns[column], values[column]);
            rows.Add(row);
        }
        return rows;
    }

    private static Dictionary<string, string> ReadPairs(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Required class metadata file is missing.", path);
        var lines = File.ReadAllLines(path);
        if (lines.Length < 1 || !string.Equals(lines[0], "Variable\tValue", StringComparison.Ordinal))
            throw new InvalidDataException($"Class metadata '{path}' must begin with the Variable/Value header.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) {
            if (lines[lineIndex].Length == 0)
                continue;
            var values = lines[lineIndex].Split('\t');
            if (values.Length != 2 || string.IsNullOrWhiteSpace(values[0]) || string.IsNullOrWhiteSpace(values[1]))
                throw new InvalidDataException($"Class metadata '{path}' row {lineIndex + 1} must have two nonempty columns.");
            if (!result.TryAdd(values[0], values[1]))
                throw new InvalidDataException($"Class metadata '{path}' duplicates key '{values[0]}'.");
        }
        return result;
    }

    private static string ReadRequired(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Required metadata key '{key}' is missing.");

    private static int ParseBoundedInt(IReadOnlyDictionary<string, string> values, string key, int minimum, int maximum)
    {
        var raw = ReadRequired(values, key);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new InvalidDataException($"Metadata value '{key}' must be an integer from {minimum} through {maximum}.");
        return value;
    }

    private static uint ParseBoundedUInt(string raw, uint minimum, uint maximum, string source)
    {
        if (!uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new InvalidDataException($"Metadata value '{raw}' in '{source}' is outside the supported unsigned range.");
        return value;
    }

    private static void ValidatePathSegment(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            throw new InvalidDataException($"{label} is not a safe path segment.");
    }

    private sealed record RuntimeSpellRow(uint SpellId, uint NativeSpellId);
}
