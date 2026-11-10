using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol.Tests;

// Pre-index collision rules, kept as an independent equivalence oracle.
internal static class LegacyPredictionCollision
{
    internal static bool IsProjectedMoveAvailable(
        Snapshot snapshot,
        PlayerSnapshot player,
        int targetX,
        int targetY)
    {
        var level = snapshot.LevelGeometry
            .FirstOrDefault(candidate => candidate.LevelId == player.LevelId);
        if (level is { Width: > 0, Height: > 0 }) {
            if (targetX < 0
                || targetY < 0
                || targetX >= level.Width
                || targetY >= level.Height)
                return false;
            var targetCell = (long)targetY * level.Width + targetX;
            if (targetCell > int.MaxValue
                || level.BlockedCells.Contains((int)targetCell)
                || level.StaticObjectBlockedCells.Contains((int)targetCell))
                return false;
        }

        if (level is not { Width: > 0, Height: > 0 }
            && snapshot.Objects.Any(value => value.LevelId == player.LevelId
                && value.NativeObjectId != 0 && value.CathedralDoor is null && !value.Destroyed))
            return false;

        // AuthoritativeNativeTownRoster / native InitCows reserve the anchor and the three NW cells.
        if (snapshot.NativeTownResidents.Any(resident => resident.LevelId == player.LevelId
            && (targetX == resident.PositionX || resident.TownerId == 9 && (long)targetX == resident.PositionX - 1L)
            && (targetY == resident.PositionY || resident.TownerId == 9 && (long)targetY == resident.PositionY - 1L)))
            return false;

        // DiabloWorldState.IsOccupied keeps both ends of a walk occupied, including dead peers
        // except for native door cells whose death handling clears player occupancy.
        if (snapshot.Players.Any(other =>
                other.EntityId != 0 && other.EntityId != player.EntityId
                && other.LevelId == player.LevelId
                && (other.Life != 0 || !snapshot.Objects.Any(value => value.LevelId == player.LevelId
                    && value.CathedralDoor is not null && value.PositionX == targetX && value.PositionY == targetY))
                && (other.PositionX == targetX && other.PositionY == targetY
                    || AuthoritativePlayerPositionProjection.Resolve(other) == new ClientPosition(targetX, targetY))))
            return false;

        if (snapshot.Monsters.Any(monster =>
            monster.Alive
            && (monster.LevelId == 0 || monster.LevelId == player.LevelId)
            && ((monster.PositionX == targetX && monster.PositionY == targetY)
                || (monster.Moving
                    && monster.FuturePositionX == targetX
                    && monster.FuturePositionY == targetY))))
            return false;

        return !snapshot.Objects.Any(@object =>
            @object.LevelId == player.LevelId
            && @object.PositionX == targetX
            && @object.PositionY == targetY
            && (@object.CathedralDoor is not null
                ? !IsValidPassableCathedralDoor(@object)
                : !@object.ActivateOnOccupancy
                    && !@object.Activated
                    && !@object.Destroyed));
    }

    /** Only a fully validated typed OPEN/BLOCKED door is passable in prediction. */
    private static bool IsValidPassableCathedralDoor(ObjectSnapshot @object)
        => @object.CathedralDoor is { DoorState: 1 or 2 }
            && AuthoritativeObjectSelector.HasAuthoritativeInteraction(@object);

}
