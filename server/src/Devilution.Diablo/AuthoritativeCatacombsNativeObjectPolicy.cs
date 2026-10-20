using Devilution.Engine;

namespace Devilution.Diablo;

public enum AuthoritativeCatacombsNativeObjectRole
{
    Decorative,
    Interactive,
    AutonomousTrap,
    AutonomousEffect,
    InertSelectable,
}

public enum AuthoritativeCatacombsNativeObjectOperation
{
    None,
    ChestLoot,
    Door,
    WallTrap,
    Barrel,
    Lever,
    Sarcophagus,
    Shrine,
    Book,
    Bookstand,
    Bookcase,
    Fountain,
    Decapitated,
    BookLever,
    Pedestal,
    ArmorStand,
    GoatShrine,
    Cauldron,
    WeaponRack,
    StoryBook,
    MushroomPatch,
    LazarusStand,
    SlainHero,
    SignChest,
    Crux,
    FlameTrap,
    MagicCircle,
    BurningCross,
    ChamberOfBoneBook,
    TrapLever,
    Unused,
}

public enum AuthoritativeCatacombsChestTrapKind
{
    Arrow = 0,
    FireArrow = 1,
}

public enum AuthoritativeCatacombsChestTrapState
{
    Disarmed,
    Armed,
    Fired,
}

/** Native object classification; recognized operations without handlers remain explicit. */
public sealed record AuthoritativeCatacombsNativeObjectClassification(
    int NativeObjectId,
    AuthoritativeCatacombsNativeObjectRole Role,
    AuthoritativeCatacombsNativeObjectOperation Operation,
    // Runtime operation support, independent of whether generation may retain this raw type.
    bool SupportedInCatacombs);

/** Detached intrinsic trap event planned by native OperateChest. */
public sealed record AuthoritativeCatacombsChestTrapPlan(
    AuthoritativeCatacombsChestTrapState StateBefore,
    AuthoritativeCatacombsChestTrapState StateDuring,
    AuthoritativeCatacombsChestTrapState StateAfter,
    AuthoritativeCatacombsChestTrapKind Kind,
    string NativeMissileId,
    int SourceX,
    int SourceY,
    int TargetX,
    int TargetY,
    int Direction,
    bool TargetsPlayers,
    int OwnerId,
    int Damage);

/** Native Catacombs object admission without inferring support from captured themes. */
public static class AuthoritativeCatacombsNativeObjectPolicy
{
    public const int NativeObjectCount = AuthoritativeCathedralPopulationCatalog.ObjectCatalogCount;

    private static readonly IReadOnlyDictionary<int, AuthoritativeCatacombsNativeObjectOperation> Operations =
        new Dictionary<int, AuthoritativeCatacombsNativeObjectOperation> {
            [1] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [2] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [4] = AuthoritativeCatacombsNativeObjectOperation.Lever,
            [5] = AuthoritativeCatacombsNativeObjectOperation.ChestLoot,
            [6] = AuthoritativeCatacombsNativeObjectOperation.ChestLoot,
            [7] = AuthoritativeCatacombsNativeObjectOperation.ChestLoot,
            [20] = AuthoritativeCatacombsNativeObjectOperation.Crux,
            [21] = AuthoritativeCatacombsNativeObjectOperation.Crux,
            [22] = AuthoritativeCatacombsNativeObjectOperation.Crux,
            [25] = AuthoritativeCatacombsNativeObjectOperation.Book,
            [26] = AuthoritativeCatacombsNativeObjectOperation.BurningCross,
            [28] = AuthoritativeCatacombsNativeObjectOperation.Lever,
            [41] = AuthoritativeCatacombsNativeObjectOperation.ChamberOfBoneBook,
            [42] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [43] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [48] = AuthoritativeCatacombsNativeObjectOperation.Sarcophagus,
            [49] = AuthoritativeCatacombsNativeObjectOperation.FlameTrap,
            [50] = AuthoritativeCatacombsNativeObjectOperation.TrapLever,
            [52] = AuthoritativeCatacombsNativeObjectOperation.Unused,
            [53] = AuthoritativeCatacombsNativeObjectOperation.WallTrap,
            [54] = AuthoritativeCatacombsNativeObjectOperation.WallTrap,
            [57] = AuthoritativeCatacombsNativeObjectOperation.Barrel,
            [58] = AuthoritativeCatacombsNativeObjectOperation.Barrel,
            [59] = AuthoritativeCatacombsNativeObjectOperation.Shrine,
            [60] = AuthoritativeCatacombsNativeObjectOperation.Shrine,
            [61] = AuthoritativeCatacombsNativeObjectOperation.Bookstand,
            [62] = AuthoritativeCatacombsNativeObjectOperation.Bookcase,
            [63] = AuthoritativeCatacombsNativeObjectOperation.Bookcase,
            [64] = AuthoritativeCatacombsNativeObjectOperation.Bookstand,
            [66] = AuthoritativeCatacombsNativeObjectOperation.Fountain,
            [67] = AuthoritativeCatacombsNativeObjectOperation.Decapitated,
            [68] = AuthoritativeCatacombsNativeObjectOperation.ChestLoot,
            [69] = AuthoritativeCatacombsNativeObjectOperation.ChestLoot,
            [70] = AuthoritativeCatacombsNativeObjectOperation.ChestLoot,
            [71] = AuthoritativeCatacombsNativeObjectOperation.BookLever,
            [72] = AuthoritativeCatacombsNativeObjectOperation.BookLever,
            [73] = AuthoritativeCatacombsNativeObjectOperation.Pedestal,
            [74] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [75] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [76] = AuthoritativeCatacombsNativeObjectOperation.Fountain,
            [77] = AuthoritativeCatacombsNativeObjectOperation.ArmorStand,
            [79] = AuthoritativeCatacombsNativeObjectOperation.GoatShrine,
            [80] = AuthoritativeCatacombsNativeObjectOperation.Cauldron,
            [81] = AuthoritativeCatacombsNativeObjectOperation.Fountain,
            [82] = AuthoritativeCatacombsNativeObjectOperation.Fountain,
            [84] = AuthoritativeCatacombsNativeObjectOperation.MagicCircle,
            [85] = AuthoritativeCatacombsNativeObjectOperation.MagicCircle,
            [86] = AuthoritativeCatacombsNativeObjectOperation.StoryBook,
            [88] = AuthoritativeCatacombsNativeObjectOperation.BookLever,
            [89] = AuthoritativeCatacombsNativeObjectOperation.ArmorStand,
            [90] = AuthoritativeCatacombsNativeObjectOperation.WeaponRack,
            [91] = AuthoritativeCatacombsNativeObjectOperation.BurningCross,
            [92] = AuthoritativeCatacombsNativeObjectOperation.WeaponRack,
            [94] = AuthoritativeCatacombsNativeObjectOperation.MushroomPatch,
            [95] = AuthoritativeCatacombsNativeObjectOperation.LazarusStand,
            [96] = AuthoritativeCatacombsNativeObjectOperation.SlainHero,
            [97] = AuthoritativeCatacombsNativeObjectOperation.SignChest,
            [99] = AuthoritativeCatacombsNativeObjectOperation.Barrel,
            [100] = AuthoritativeCatacombsNativeObjectOperation.Barrel,
            [101] = AuthoritativeCatacombsNativeObjectOperation.Barrel,
            [102] = AuthoritativeCatacombsNativeObjectOperation.Barrel,
            [103] = AuthoritativeCatacombsNativeObjectOperation.StoryBook,
            [105] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [106] = AuthoritativeCatacombsNativeObjectOperation.Door,
            [107] = AuthoritativeCatacombsNativeObjectOperation.Lever,
            [108] = AuthoritativeCatacombsNativeObjectOperation.Sarcophagus,
        };

    public static AuthoritativeCatacombsNativeObjectClassification Classify(int nativeObjectId)
    {
        if ((uint)nativeObjectId >= NativeObjectCount)
            throw new InvalidDataException("Native object ID is outside the 109-row object catalog.");

        var operation = Operations.GetValueOrDefault(nativeObjectId);
        var role = operation switch {
            AuthoritativeCatacombsNativeObjectOperation.WallTrap or AuthoritativeCatacombsNativeObjectOperation.FlameTrap
                => AuthoritativeCatacombsNativeObjectRole.AutonomousTrap,
            AuthoritativeCatacombsNativeObjectOperation.MagicCircle or AuthoritativeCatacombsNativeObjectOperation.BurningCross
                => AuthoritativeCatacombsNativeObjectRole.AutonomousEffect,
            AuthoritativeCatacombsNativeObjectOperation.Unused => AuthoritativeCatacombsNativeObjectRole.InertSelectable,
            AuthoritativeCatacombsNativeObjectOperation.None => AuthoritativeCatacombsNativeObjectRole.Decorative,
            _ => AuthoritativeCatacombsNativeObjectRole.Interactive,
        };
        var supported = nativeObjectId is 5 or 6 or 7 or 42 or 43 or 68 or 69 or 70;
        return new(nativeObjectId, role, operation, supported);
    }

    /** TChest native IDs 68–70 share AddChest loot tiers 5–7 without changing their raw ID. */
    public static int LootTierForChestType(int nativeObjectId)
        => nativeObjectId switch {
            >= 5 and <= 7 => nativeObjectId,
            >= 68 and <= 70 => nativeObjectId - 63,
            _ => throw new InvalidDataException("Native object is not one of the admitted ordinary or trapped chest types."),
        };

    internal static AuthoritativeCatacombsChestTrapPlan? PlanIntrinsicTrap(
        AuthoritativeCatacombsNativeChestFacts chest,
        int playerX,
        int playerY)
    {
        if (!chest.TrapFlag || chest.NativeObjectId is not (>= 68 and <= 70))
            return null;
        var kind = chest.Var4 switch {
            0 => AuthoritativeCatacombsChestTrapKind.Arrow,
            1 => AuthoritativeCatacombsChestTrapKind.FireArrow,
            _ => throw new InvalidDataException("Intrinsic Catacombs chest trap Var4 must select Arrow (0) or FireArrow (1)."),
        };
        return new(
            AuthoritativeCatacombsChestTrapState.Armed,
            AuthoritativeCatacombsChestTrapState.Fired,
            AuthoritativeCatacombsChestTrapState.Disarmed,
            kind,
            kind == AuthoritativeCatacombsChestTrapKind.Arrow ? "Arrow" : "FireArrow",
            chest.PositionX,
            chest.PositionY,
            playerX,
            playerY,
            (int)DeterministicGridDirections.Toward(chest.PositionX, chest.PositionY, playerX, playerY),
            TargetsPlayers: true,
            OwnerId: -1,
            Damage: 0);
    }
}

/** Captured fields used by AddChest/OperateChest before world publication. */
public sealed record AuthoritativeCatacombsNativeChestFacts(
    int ObjectSlot,
    int NativeObjectId,
    int PositionX,
    int PositionY,
    uint RandomSeed,
    int Var1,
    int Var2,
    int Var4,
    bool TrapFlag,
    uint AnimationFrame)
{
    /** Exact retained wall-trap row; supplied by the population owner for ordinary linked chests. */
    public AuthoritativeCathedralPopulationObjectState? LinkedWallTrap { get; init; }
}
