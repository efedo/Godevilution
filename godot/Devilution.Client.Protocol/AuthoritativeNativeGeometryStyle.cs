namespace Devilution.Client.Protocol;

/**
 * Deterministic presentation tint for one server-authored native d-piece.
 *
 * This is a diagnostic fallback while licensed native tile art is not yet
 * part of the Godot content pipeline. It never supplies collision or game
 * rules; those remain in the authoritative level projection.
 */
public readonly record struct AuthoritativeNativeGeometryStyle(
    byte Red,
    byte Green,
    byte Blue,
    byte Alpha)
{
    public bool IsVisible => Alpha != 0;

    /** Resolves a stable tint without depending on Godot or native SDL types. */
    public static AuthoritativeNativeGeometryStyle Resolve(
        uint pieceId,
        uint transparency,
        bool blocked,
        bool missileBlocked)
    {
        if (pieceId == 0 && transparency == 0)
            return default;

        if (blocked)
            return new(112, 54, 76, 92);
        if (missileBlocked)
            return new(142, 70, 122, 78);
        if (transparency != 0)
            return new(80, 142, 156, 66);

        // A small integer hash keeps neighboring native tile IDs visually
        // distinguishable while remaining stable across clients and runs.
        var hash = unchecked(pieceId * 2_654_435_761u + 2_246_822_519u);
        var red = (byte)(58 + ((hash >> 0) & 0x3F));
        var green = (byte)(62 + ((hash >> 8) & 0x3F));
        var blue = (byte)(76 + ((hash >> 16) & 0x3F));
        return new(red, green, blue, 42);
    }
}
