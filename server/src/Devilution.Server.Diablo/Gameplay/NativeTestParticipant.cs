using System.Text.Json;
using Devilution.Diablo;

namespace Devilution.Server.Gameplay;

/**
 * Acceptance-only played-character start for native profiles: class, level,
 * experience, base attributes, and equipment/belt slots by stable item symbol.
 * Supplied only through --test-participant; normal launches never set it.
 */
public sealed record NativeTestParticipant(
    string ClassSymbol,
    uint CharacterLevel,
    uint Experience,
    int Strength,
    int Magic,
    int Dexterity,
    int Vitality,
    IReadOnlyDictionary<uint, string>? Equipment = null,
    IReadOnlyDictionary<uint, string>? Belt = null,
    uint? Gold = null)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static NativeTestParticipant Parse(string json)
    {
        var participant = JsonSerializer.Deserialize<NativeTestParticipant>(json, Json)
            ?? throw new InvalidDataException("The test participant definition is empty.");
        if (string.IsNullOrWhiteSpace(participant.ClassSymbol)
            || participant.CharacterLevel is < 1 or > AuthoritativePlayerLoadoutCatalog.MaximumNativeCharacterLevel
            || new[] { participant.Strength, participant.Magic, participant.Dexterity, participant.Vitality }.Any(value => value is < 1 or > 255))
            throw new InvalidDataException("The test participant needs a class, a native character level and positive base attributes.");
        return participant;
    }

    public DiabloPlayerTestStart ToTestStart(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        uint ItemId(string symbol) => items.Definitions.SingleOrDefault(item => item.StableSymbol == symbol)?.ItemId
            ?? throw new InvalidDataException($"The test participant names unknown item '{symbol}'.");
        PlayerAttributeState Attribute(int value) => new(value, value);
        return new DiabloPlayerTestStart(CharacterLevel, Experience,
            new PlayerAttributesState(Attribute(Strength), Attribute(Magic), Attribute(Dexterity), Attribute(Vitality)),
            (Equipment ?? new Dictionary<uint, string>()).ToDictionary(pair => pair.Key, pair => ItemId(pair.Value)),
            (Belt ?? new Dictionary<uint, string>()).ToDictionary(pair => pair.Key, pair => ItemId(pair.Value)),
            Gold);
    }
}
