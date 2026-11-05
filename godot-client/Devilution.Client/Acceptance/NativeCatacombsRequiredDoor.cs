using System.Globalization;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** The launcher supplies one seeded target; normal acceptance has no door destination. */
internal sealed record NativeCatacombsRequiredDoor(uint Level, int X, int Y)
{
    internal static NativeCatacombsRequiredDoor? ForRun(bool required, string? level, string? x, string? y)
        => required ? Parse(level, x, y) : null;

    internal static NativeCatacombsRequiredDoor Parse(string? level, string? x, string? y)
    {
        if (!uint.TryParse(level, NumberStyles.None, CultureInfo.InvariantCulture, out var depth) || depth is < 5 or > 8
            || !int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var px) || px is < 0 or >= 112
            || !int.TryParse(y, NumberStyles.None, CultureInfo.InvariantCulture, out var py) || py is < 0 or >= 112)
            throw new InvalidDataException("Required Catacombs door needs the launcher's native level and tile coordinates.");
        return new(depth, px, py);
    }

    internal bool Matches(ObjectSnapshot value)
        => value.LevelId == Level && value.PositionX == X && value.PositionY == Y && value.NativeObjectId is 42 or 43;
}
