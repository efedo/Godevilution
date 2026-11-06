using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

internal sealed partial class AuthoritativeNativePopulationBindings
{
    /** C2 chest capability never normalizes TCHEST raw IDs or unsupported wall effects. */
    internal static bool IsSupportedNativeCatacombsChest(AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Type is not (5 or 6 or 7 or 68 or 69 or 70) || raw.DoorFlag
            || raw.SelectionRegion is < 1 or > 7 || raw.AnimFrame > byte.MaxValue - 2
            || raw.Var1 < 0 || raw.Var2 is < 0 or > 7
            || (uint)raw.Position.X >= AuthoritativeCathedralThemeRoomState.Width
            || (uint)raw.Position.Y >= AuthoritativeCathedralThemeRoomState.Width
            || state.ObjectRefs[raw.Position.Y * AuthoritativeCathedralThemeRoomState.Width + raw.Position.X] != raw.Slot + 1)
            return false;
        var tier = AuthoritativeCatacombsNativeObjectPolicy.LootTierForChestType(raw.Type);
        if (raw.Var1 > tier - 4)
            return false;
        var linked = HasLinkedNativeTrap(state, raw);
        if (raw.Type is >= 68 and <= 70)
            return raw.Var4 is 0 or 1;
        return raw.TrapFlag == linked && (!linked || HasSupportedNativeTrapLink(state, raw));
    }

    internal bool TryGetNativeChestFacts(AuthoritativeWorldObject value,
        out AuthoritativeCatacombsNativeChestFacts? facts)
    {
        ValidateObject(value);
        facts = null;
        if (!objectRows.TryGetValue(value.EntityId, out var raw) || !IsSupportedNativeChest(raw))
            return false;
        facts = new(raw.Slot, raw.Type, raw.Position.X, raw.Position.Y, value.NativeRandomSeed ?? raw.RndSeed,
            raw.Var1, raw.Var2, raw.Var4, value.TrapArmed, raw.AnimFrame) {
            LinkedWallTrap = InitialState.ActiveObjectIds.Take(InitialState.ActiveObjectCount)
                .Select(slot => InitialState.ObjectSlots[slot])
                .FirstOrDefault(trap => trap.Type is 53 or 54
                    && trap.Var1 == raw.Position.X && trap.Var2 == raw.Position.Y),
        };
        return true;
    }

    /** Registry identity belongs only to the additive mixed-family admission path. */
    internal static string ComputeFamilyItemGenerationFingerprint(IReadOnlyList<AuthoritativeNativePopulationBindings> owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        if (owners.Count == 0 || owners.Select(owner => owner.LevelId).Distinct().Count() != owners.Count)
            throw new InvalidDataException("Native family item identity requires unique floor owners.");
        if (owners.Count == 1)
            return owners[0].Fingerprint;
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            owners.OrderBy(owner => owner.LevelId).Select(owner => new {
                owner.Family, owner.Depth, owner.LevelId, owner.Fingerprint,
            }).ToArray())));
    }

    internal AuthoritativeNativeDungeonFamilyContext ItemFamilyContext
        => Family == AuthoritativeNativePopulationFamily.Catacombs
            ? AuthoritativeNativeDungeonFamilyContext.Catacombs(Depth)
            : AuthoritativeNativeDungeonFamilyContext.Cathedral(Depth);
}
