namespace Devilution.Diablo;

public sealed partial record DiabloCohortState
{
    private void ValidateNativeTownCopies()
    {
        var town = NativeTown!;
        if (Geometry is null || CathedralDoors is null)
            throw new InvalidDataException("Native Town cohorts require complete backing and effective geometry.");
        foreach (var levels in new[] { Geometry.Levels!, WorldInfrastructure.EffectiveLevels!,
                     CathedralDoors.BackingGeometry.Levels!, CathedralDoors.EffectiveLevels! }) {
            var level = levels.SingleOrDefault(value => value.LevelId == town.LevelId)
                ?? throw new InvalidDataException("Native Town is missing from a retained geometry copy.");
            if (level.Width != AuthoritativeTownGeometry.FineWidth
                || level.Height != AuthoritativeTownGeometry.FineHeight
                || level.NativeMegaTileLayer is null || level.NativeMegaTileLayer.Count != 0
                || level.NativePieceLayer is null || !level.NativePieceLayer.SequenceEqual(town.NativePieceLayer)
                || level.NativeTransparency is null || !level.NativeTransparency.SequenceEqual(town.NativeTransparencyLayer)
                || level.BlockedCells is null || !level.BlockedCells.SequenceEqual(town.NativeSolidCells)
                || level.MissileBlockedCells is null || !level.MissileBlockedCells.SequenceEqual(town.NativeMissileBlockedCells)
                || level.NativeVisionBlockedCells is null || !level.NativeVisionBlockedCells.SequenceEqual(town.NativeBlockLightCells.Concat(town.NativeSolidCells).Distinct().Order())
                || !level.HasExplicitNativeVisionBlockedCells
                || level.NativePieceCollisionCells is null || !level.NativePieceCollisionCells.SequenceEqual(town.NativeSolidCells)
                || level.NativePieceMissileBlockedCells is null || !level.NativePieceMissileBlockedCells.SequenceEqual(town.NativeMissileBlockedCells)
                || level.NativePieceProperties is null || !level.NativePieceProperties.OrderBy(value => value.Key)
                    .SequenceEqual(town.NativePieceProperties.OrderBy(value => value.Key)))
                throw new InvalidDataException("Native Town checkpoint differs from a retained fine/SOL geometry copy.");
        }
    }
}
