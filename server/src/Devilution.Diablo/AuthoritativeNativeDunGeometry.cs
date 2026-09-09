using Devilution.Data;

namespace Devilution.Diablo;

/**
 * Validated native geometry bundle loader. The server keeps the native binary
 * assets separate from presentation, then composes them only at the
 * authoritative movement-geometry boundary.
 */
public static class AuthoritativeNativeDunGeometryLoader
{
    /** Loads one set-level geometry variant directly from the active content assets. */
    public static AuthoritativeLevel Load(
        ComposedContent content,
        AuthoritativeSetLevelMapDefinition definition,
        uint levelId,
        bool activeMap,
        bool suppressMultiplayerDeathDrops = false,
        AuthoritativeNativeDunGeometryCatalog? externalGeometry = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (string.IsNullOrWhiteSpace(definition.TileAssetPath)
            || string.IsNullOrWhiteSpace(definition.SolAssetPath)) {
            if (externalGeometry is null)
                throw new InvalidDataException(
                    $"Set-level map {definition.SetLevelId} does not declare native geometry inputs.");
            var externalDunAssetPath = activeMap
                ? definition.ActiveMapAssetPath
                : definition.PreMapAssetPath;
            var externalMap = AuthoritativeDunMapAsset.Load(
                externalDunAssetPath,
                content.GetRequiredAsset(externalDunAssetPath).Contents);
            return AuthoritativeDunGeometryMaterializer.Materialize(
                levelId,
                externalMap,
                externalGeometry.Pieces,
                externalGeometry.TileProperties,
                definition.SetLevelSymbol,
                suppressMultiplayerDeathDrops);
        }
        var dunAssetPath = activeMap
            ? definition.ActiveMapAssetPath
            : definition.PreMapAssetPath;
        return Load(
            levelId,
            dunAssetPath,
            content.GetRequiredAsset(dunAssetPath).Contents,
            definition.TileAssetPath,
            content.GetRequiredAsset(definition.TileAssetPath).Contents,
            definition.SolAssetPath,
            content.GetRequiredAsset(definition.SolAssetPath).Contents,
            definition.SetLevelSymbol,
            suppressMultiplayerDeathDrops);
    }

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
