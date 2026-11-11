using Devilution.Protocol.V1;
using Devilution.Client.Protocol;

namespace Devilution.Client.Acceptance;

/** The bot may fall back onto observed ground; an empty-looking unexplored room is not cover. */
internal sealed class JourneyCombatPositioning
{
    private readonly HashSet<(uint Level, int X, int Y)> visited = [];
    private readonly HashSet<(uint Level, int X, int Y)> cleared = [];
    internal void Reset() { visited.Clear(); cleared.Clear(); }
    internal void Observe(JourneyCombatSafety.Observation state, int x, int y)
    {
        if (state.Level == 0) return;
        visited.Add((state.Level, x, y));
        if (state.Nearby == 0) cleared.Add((state.Level, x, y));
    }

    internal bool ShouldHold(JourneyCombatSafety.Observation state)
        => state.HealingItems == 0 && state.Nearby >= 3;

    internal (int X, int Y)? CoverStep(Snapshot snapshot, PlayerSnapshot player, LevelGeometrySnapshot geometry,
        Func<int, int, bool> open)
    {
        var width = checked((int)geometry.Width); var height = checked((int)geometry.Height);
        var threats = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion
            && AuthoritativeLevelVisibility.IsVisible(monster.LevelId, player.LevelId)).ToArray();
        (int Adjacent, int Open, long Clearance) Exposure(int x, int y)
        {
            var adjacent = 0; var clearance = long.MaxValue;
            foreach (var monster in threats) {
                var distance = Math.Max(Math.Abs((long)x - monster.PositionX), Math.Abs((long)y - monster.PositionY));
                if (monster.Moving) distance = Math.Min(distance,
                    Math.Max(Math.Abs((long)x - monster.FuturePositionX), Math.Abs((long)y - monster.FuturePositionY)));
                clearance = Math.Min(clearance, distance);
                if (distance <= 1) adjacent++;
            }
            var exits = 0;
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) }) {
                var nx = x + dx; var ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height
                    && !geometry.BlockedCells.Contains(ny * width + nx)
                    && !geometry.StaticObjectBlockedCells.Contains(ny * width + nx)) exits++;
            }
            return (adjacent, exits, clearance);
        }
        var current = Exposure(player.PositionX, player.PositionY);
        var best = current;
        (int X, int Y)? step = null;
        foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (1, -1), (-1, -1), (-1, 1) }) {
            var x = player.PositionX + dx; var y = player.PositionY + dy;
            if (!visited.Contains((player.LevelId, x, y)) || !open(x, y)
                || geometry.WarpTriggers.Any(warp => warp.TriggerX == x && warp.TriggerY == y)
                || dx != 0 && dy != 0 && (!open(player.PositionX + dx, player.PositionY) || !open(player.PositionX, player.PositionY + dy))) continue;
            var candidate = Exposure(x, y);
            if (candidate.Adjacent > current.Adjacent) continue;
            if (candidate.Adjacent == current.Adjacent && candidate.Clearance < current.Clearance) continue;
            var safe = cleared.Contains((player.LevelId, x, y));
            if (!safe && candidate.Open >= current.Open) continue;
            if (candidate.Adjacent < best.Adjacent
                || candidate.Adjacent == best.Adjacent && candidate.Open < best.Open
                || candidate.Adjacent == best.Adjacent && candidate.Open == best.Open && candidate.Clearance > best.Clearance) {
                best = candidate; step = (x, y);
            }
        }
        return step;
    }
}
