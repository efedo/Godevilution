namespace Devilution.Client.Protocol;

/**
 * Deterministic presentation outline for one native DUN object-layer value.
 *
 * Native object IDs are only provenance here. Gameplay and interaction remain
 * driven by server-owned object entities and never by this style resolver.
 */
public readonly record struct AuthoritativeNativeObjectLayerStyle(
    byte Red,
    byte Green,
    byte Blue,
    byte Alpha)
{
    public bool IsVisible => Alpha != 0;

    public static AuthoritativeNativeObjectLayerStyle Resolve(uint objectId)
    {
        if (objectId == 0)
            return default;

        var hash = unchecked(objectId * 2_246_822_519u + 3_265_443_576u);
        var red = (byte)(112 + ((hash >> 0) & 0x3F));
        var green = (byte)(84 + ((hash >> 8) & 0x3F));
        var blue = (byte)(42 + ((hash >> 16) & 0x3F));
        return new(red, green, blue, 108);
    }
}
