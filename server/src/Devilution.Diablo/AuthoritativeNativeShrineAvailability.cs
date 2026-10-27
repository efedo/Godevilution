namespace Devilution.Diablo;

/** objects.cpp shrineavail[] for the 26 original-Diablo shrines and AddShrine's game-mode filter. */
public static class AuthoritativeNativeShrineAvailability
{
    public const int OriginalShrineCount = 26;

    private enum GameType
    {
        Any,
        Single,
        Multi,
    }

    private static readonly GameType[] Types = [
        GameType.Any, // Mysterious
        GameType.Any, // Hidden
        GameType.Single, // Gloomy
        GameType.Single, // Weird
        GameType.Any, // Magical
        GameType.Any, // Stone
        GameType.Any, // Religious
        GameType.Any, // Enchanted
        GameType.Single, // Thaumaturgic
        GameType.Any, // Fascinating
        GameType.Any, // Cryptic
        GameType.Any, // Magical
        GameType.Any, // Eldritch
        GameType.Any, // Eerie
        GameType.Any, // Divine
        GameType.Any, // Holy
        GameType.Any, // Sacred
        GameType.Any, // Spiritual
        GameType.Multi, // Spooky
        GameType.Any, // Abandoned
        GameType.Any, // Creepy
        GameType.Any, // Quiet
        GameType.Any, // Secluded
        GameType.Any, // Ornate
        GameType.Any, // Glimmering
        GameType.Multi, // Tainted
    ];

    /** Single-player excludes multiplayer-only shrines; Enchanted is allowed in Cathedral. */
    public static IReadOnlyList<bool> SinglePlayer { get; } =
        Array.AsReadOnly(Types.Select(type => type != GameType.Multi).ToArray());

    /** Multiplayer excludes single-player-only shrines. */
    public static IReadOnlyList<bool> Multiplayer { get; } =
        Array.AsReadOnly(Types.Select(type => type != GameType.Single).ToArray());
}
