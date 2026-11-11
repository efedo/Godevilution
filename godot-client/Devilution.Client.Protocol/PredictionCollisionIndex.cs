using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Collision facts for one admitted view. Tick-only changes retain the search revision. */
internal sealed class PredictionCollisionIndex
{
    private Snapshot? prepared;
    private uint entity, levelId;
    private int width, height;
    private bool hasGeometry, unknownFootprints;
    private HashSet<int> solids = [], nextSolids = [];
    private HashSet<(int X, int Y)> occupied = [], nextOccupied = [];
    private readonly HashSet<(int X, int Y)> doors = [];
    internal ulong Revision { get; private set; }

    internal void Prepare(Snapshot snapshot, PlayerSnapshot player)
    {
        if (ReferenceEquals(prepared, snapshot) && entity == player.EntityId && levelId == player.LevelId) return;
        LevelGeometrySnapshot? geometry = null;
        foreach (var candidate in snapshot.LevelGeometry)
            if (candidate.LevelId == player.LevelId) { geometry = candidate; break; }
        var nextWidth = geometry?.Width ?? 0;
        var nextHeight = geometry?.Height ?? 0;
        nextSolids.Clear();
        if (geometry is not null) {
            foreach (var cell in geometry.BlockedCells) nextSolids.Add(cell);
            foreach (var cell in geometry.StaticObjectBlockedCells) nextSolids.Add(cell);
        }
        nextOccupied.Clear(); doors.Clear();
        var unknown = false;
        foreach (var value in snapshot.Objects) {
            if (value.LevelId != player.LevelId) continue;
            if (value.CathedralDoor is not null) {
                doors.Add((value.PositionX, value.PositionY));
                if (value.CathedralDoor.DoorState is not (1 or 2)
                    || !AuthoritativeObjectSelector.HasAuthoritativeInteraction(value))
                    nextOccupied.Add((value.PositionX, value.PositionY));
            } else {
                if (value.NativeObjectId != 0 && !value.Destroyed) unknown = true;
                if (!value.ActivateOnOccupancy && !value.Activated && !value.Destroyed)
                    nextOccupied.Add((value.PositionX, value.PositionY));
            }
        }
        foreach (var resident in snapshot.NativeTownResidents) {
            if (resident.LevelId != player.LevelId) continue;
            nextOccupied.Add((resident.PositionX, resident.PositionY));
            // Native InitCows reserves the anchor and the three NW cells, without integer wrapping.
            if (resident.TownerId == 9) {
                if (resident.PositionX > int.MinValue) nextOccupied.Add((resident.PositionX - 1, resident.PositionY));
                if (resident.PositionY > int.MinValue) nextOccupied.Add((resident.PositionX, resident.PositionY - 1));
                if (resident.PositionX > int.MinValue && resident.PositionY > int.MinValue)
                    nextOccupied.Add((resident.PositionX - 1, resident.PositionY - 1));
            }
        }
        foreach (var other in snapshot.Players) {
            if (other.EntityId == 0 || other.EntityId == player.EntityId || other.LevelId != player.LevelId) continue;
            AddPlayerCell(other.PositionX, other.PositionY, other.Life != 0);
            var future = AuthoritativePlayerPositionProjection.Resolve(other);
            AddPlayerCell(future.X, future.Y, other.Life != 0);
        }
        foreach (var monster in snapshot.Monsters) {
            if (!monster.Alive || !AuthoritativeLevelVisibility.IsVisible(monster.LevelId, player.LevelId)) continue;
            nextOccupied.Add((monster.PositionX, monster.PositionY));
            if (monster.Moving) nextOccupied.Add((monster.FuturePositionX, monster.FuturePositionY));
        }
        unknown &= nextWidth <= 0 || nextHeight <= 0;
        if (prepared is null || entity != player.EntityId || levelId != player.LevelId
            || hasGeometry != (geometry is not null) || width != nextWidth || height != nextHeight
            || unknownFootprints != unknown || !solids.SetEquals(nextSolids) || !occupied.SetEquals(nextOccupied))
            Revision++;
        (solids, nextSolids) = (nextSolids, solids);
        (occupied, nextOccupied) = (nextOccupied, occupied);
        width = nextWidth; height = nextHeight; hasGeometry = geometry is not null; unknownFootprints = unknown;
        entity = player.EntityId; levelId = player.LevelId; prepared = snapshot;
    }

    private void AddPlayerCell(int x, int y, bool alive)
    {
        // Native door death handling clears player occupancy on the door cell only.
        if (alive || !doors.Contains((x, y))) nextOccupied.Add((x, y));
    }

    internal bool IsAvailable(int x, int y)
        => !unknownFootprints && (width <= 0 || height <= 0 || IsOpenGeometryCell(x, y))
            && !occupied.Contains((x, y));

    internal bool IsCornerOpen(int x, int y) => !hasGeometry || IsOpenGeometryCell(x, y);

    private bool IsOpenGeometryCell(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return false;
        var cell = (long)y * width + x;
        return cell <= int.MaxValue && !solids.Contains((int)cell);
    }
}
