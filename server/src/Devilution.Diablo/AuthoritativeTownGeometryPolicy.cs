namespace Devilution.Diablo;

public enum AuthoritativeTownWarp : byte
{
    Catacombs,
    Caves,
    Hell,
    Nest,
    Crypt,
}

[Flags]
public enum AuthoritativeTownWarpUnlocks : byte
{
    None = 0,
    Catacombs = 1,
    Caves = 2,
    Hell = 4,
}

public enum AuthoritativeTownFarmerQuestState : byte
{
    Other,
    Done,
    HiveDone,
}

public enum AuthoritativeTownGraveQuestState : byte
{
    Other,
    Done,
}

public enum AuthoritativeTownWaterQuestState : byte
{
    NotAvailable = 0,
    Init = 1,
    Active = 2,
    Done = 3,
}

public enum AuthoritativeTownEntry : byte
{
    Main = 0,
    Previous = 1,
    SetLevel = 2,
    ReturnLevel = 3,
    Load = 4,
    WarpLevel = 5,
    TownWarpDown = 6,
    TownWarpUp = 7,
}

/** Native IsWarpOpen inputs; the result is bound to the named policy version. */
public sealed record AuthoritativeTownWarpPolicy(
    bool IsSpawn,
    bool IsMultiplayer,
    bool IsHellfire,
    int CharacterLevel,
    AuthoritativeTownWarpUnlocks TownWarpUnlocks,
    AuthoritativeTownFarmerQuestState FarmerQuestState,
    AuthoritativeTownGraveQuestState GraveQuestState)
{
    public const string Version = "native-town-warp-policy.v1";

    public bool IsWarpOpen(AuthoritativeTownWarp warp)
    {
        Validate();
        if (!Enum.IsDefined(warp))
            throw new ArgumentOutOfRangeException(nameof(warp));
        if (IsSpawn)
            return false;
        if (IsMultiplayer && warp != AuthoritativeTownWarp.Nest)
            return true;

        return warp switch {
            AuthoritativeTownWarp.Catacombs =>
                TownWarpUnlocks.HasFlag(AuthoritativeTownWarpUnlocks.Catacombs)
                || (IsHellfire && CharacterLevel >= 10),
            AuthoritativeTownWarp.Caves =>
                TownWarpUnlocks.HasFlag(AuthoritativeTownWarpUnlocks.Caves)
                || (IsHellfire && CharacterLevel >= 15),
            AuthoritativeTownWarp.Hell =>
                TownWarpUnlocks.HasFlag(AuthoritativeTownWarpUnlocks.Hell)
                || (IsHellfire && CharacterLevel >= 20),
            AuthoritativeTownWarp.Nest => IsHellfire
                && (FarmerQuestState == AuthoritativeTownFarmerQuestState.Done
                    || FarmerQuestState == AuthoritativeTownFarmerQuestState.HiveDone),
            AuthoritativeTownWarp.Crypt => IsHellfire
                && GraveQuestState == AuthoritativeTownGraveQuestState.Done,
            _ => throw new ArgumentOutOfRangeException(nameof(warp)),
        };
    }

    internal void Validate()
    {
        const AuthoritativeTownWarpUnlocks supportedUnlocks =
            AuthoritativeTownWarpUnlocks.Catacombs
            | AuthoritativeTownWarpUnlocks.Caves
            | AuthoritativeTownWarpUnlocks.Hell;
        if (CharacterLevel < 0
            || CharacterLevel > 50
            || (TownWarpUnlocks & ~supportedUnlocks) != 0
            || !Enum.IsDefined(FarmerQuestState)
            || !Enum.IsDefined(GraveQuestState))
            throw new ArgumentOutOfRangeException(nameof(AuthoritativeTownWarpPolicy));
    }

    public AuthoritativeTownWarpOpenings ResolveOpenings()
    {
        Validate();
        return new AuthoritativeTownWarpOpenings(
            IsWarpOpen(AuthoritativeTownWarp.Catacombs),
            IsWarpOpen(AuthoritativeTownWarp.Caves),
            IsWarpOpen(AuthoritativeTownWarp.Hell),
            IsWarpOpen(AuthoritativeTownWarp.Nest),
            IsWarpOpen(AuthoritativeTownWarp.Crypt));
    }
}

/** Resolved native gate values captured with the policy that produced them. */
public readonly record struct AuthoritativeTownWarpOpenings(
    bool Catacombs,
    bool Caves,
    bool Hell,
    bool Nest,
    bool Crypt)
{
    public bool IsOpen(AuthoritativeTownWarp warp)
        => warp switch {
            AuthoritativeTownWarp.Catacombs => Catacombs,
            AuthoritativeTownWarp.Caves => Caves,
            AuthoritativeTownWarp.Hell => Hell,
            AuthoritativeTownWarp.Nest => Nest,
            AuthoritativeTownWarp.Crypt => Crypt,
            _ => throw new ArgumentOutOfRangeException(nameof(warp)),
        };
}

/** Inputs for the pure CreateTown/DrlgTPass3 composition boundary. */
public sealed record AuthoritativeTownGeometryRequest(
    AuthoritativeTownTileMap Sector1South,
    AuthoritativeTownTileMap Sector2South,
    AuthoritativeTownTileMap Sector3South,
    AuthoritativeTownTileMap Sector4South,
    AuthoritativeTownTileMap Automap,
    AuthoritativeDunPieceCatalog PieceCatalog,
    AuthoritativeDunTilePropertyCatalog TileProperties,
    AuthoritativeTownWarpPolicy WarpPolicy,
    AuthoritativeTownEntry Entry,
    int TownWarpFrom,
    AuthoritativeTownWaterQuestState WaterQuestState,
    uint InitialRandomState,
    int ExistingViewPositionX = 0,
    int ExistingViewPositionY = 0,
    IReadOnlyList<ushort>? InitialMegaTileLayer = null,
    IReadOnlyList<int>? InitialProtectedMegaCells = null,
    IReadOnlyList<ushort>? InitialSpecialLayer = null);
