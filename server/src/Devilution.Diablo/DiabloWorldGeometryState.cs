namespace Devilution.Diablo;

/** Detached backing geometry for one authoritative world checkpoint. */
public sealed record DiabloWorldGeometryState(
	IReadOnlyList<DiabloWorldGeometryLevelState>? Levels,
	IReadOnlyList<DiabloWorldGeometryWarpState>? WarpTargets);

/** Complete constructor state for one AuthoritativeLevel. */
public sealed record DiabloWorldGeometryLevelState(
	uint LevelId,
	string StableSymbol,
	int Width,
	int Height,
	IReadOnlyList<int>? BlockedCells,
	IReadOnlyList<int>? MissileBlockedCells,
	IReadOnlyList<ushort>? NativeTransparency,
	IReadOnlyList<ushort>? NativeObjectLayer,
	IReadOnlyList<ushort>? NativeMegaTileLayer,
	IReadOnlyList<ushort>? NativePieceLayer,
	IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? NativePieceProperties,
	IReadOnlyList<int>? NativePieceCollisionCells,
	IReadOnlyList<int>? NativeVisionBlockedCells,
	bool HasExplicitNativeVisionBlockedCells,
	IReadOnlyList<int>? NativePieceMissileBlockedCells,
	IReadOnlyList<ushort>? NativeMonsterLayer,
	bool IsDefaultStart,
	bool SuppressMultiplayerDeathDrops,
	int StartPositionX,
	int StartPositionY);

/** Complete constructor and stable-symbol state for one warp target. */
public sealed record DiabloWorldGeometryWarpState(
	uint WarpId,
	uint SourceLevelId,
	int TriggerX,
	int TriggerY,
	uint TargetLevelId,
	int TargetX,
	int TargetY,
	uint RequiredQuestId,
	bool RequiredQuestCompleted,
	int TargetSetLevelId,
	bool TargetSetLevelActiveMap,
	string TargetSetLevelSymbol,
	string StableSymbol);
