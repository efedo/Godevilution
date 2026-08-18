namespace Devilution.Diablo;

/**
 * Validated native geometry bundle loader. The server keeps the native binary
 * assets separate from presentation, then composes them only at the
 * authoritative movement-geometry boundary.
 */
public static class AuthoritativeNativeDunGeometryLoader
{
    public static AuthoritativeLevel Load(
        uint levelId,
        string dunSourcePath,
        ReadOnlyMemory<byte> dunContents,
        string tilSourcePath,
        ReadOnlyMemory<byte> tilContents,
        string solSourcePath,
        ReadOnlyMemory<byte> solContents,
        string stableSymbol = "",
        bool suppressMultiplayerDeathDrops = false)
    {
        var map = AuthoritativeDunMapAsset.Load(dunSourcePath, dunContents);
        var pieces = AuthoritativeDunPieceCatalog.LoadTil(tilSourcePath, tilContents);
        var tileProperties = AuthoritativeDunTilePropertyCatalog.LoadSol(
            solSourcePath,
            solContents);
        return AuthoritativeDunGeometryMaterializer.Materialize(
            levelId,
            map,
            pieces,
            tileProperties,
            stableSymbol,
            suppressMultiplayerDeathDrops);
    }
}
