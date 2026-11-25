using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework;

namespace AI_Strategy
{
    public class ZergSoldier : Soldier
    {
        public Tower TargetTower { get; set; }

        protected override List<Unit> SortTargetsInRange(List<Unit> targets)
        {
            return targets
                .OrderByDescending(u => u == TargetTower ? 2 : (u is Tower ? 1 : 0))
                .ThenBy(u => u.Health)
                .ToList();
        }

        public override void Move()
        {
            if (speed <= 0 || posY >= PlayerLane.HEIGHT) return;

            if (TargetTower != null && TargetTower.Health > 0 && TargetTower.PosX >= 0)
            {
                int distToTarget = Math.Max(Math.Abs(posX - TargetTower.PosX), Math.Abs(posY - TargetTower.PosY));
                if (distToTarget <= 1)
                {
                    return;
                }

                MoveToward(TargetTower.PosX, TargetTower.PosY);
                return;
            }

            int x = posX;
            int y = posY;

            if (MoveTo(x, y + 1)) return;
            if (MoveTo(x - 1, y + 1)) return;
            if (MoveTo(x + 1, y + 1)) return;
            if (MoveTo(x - 1, y)) return;
            if (MoveTo(x + 1, y)) return;
            if (MoveTo(x, y - 1)) return;
        }

        private void MoveToward(int targetX, int targetY)
        {
            List<(int nx, int ny)> candidates = new List<(int, int)>();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    candidates.Add((posX + dx, posY + dy));
                }
            }

            var orderedMoves = candidates
                .OrderBy(c => Math.Max(Math.Abs(c.nx - targetX), Math.Abs(c.ny - targetY)))
                .ThenBy(c => Math.Abs(c.nx - targetX) + Math.Abs(c.ny - targetY));

            foreach (var move in orderedMoves)
            {
                if (MoveTo(move.nx, move.ny))
                {
                    return;
                }
            }
        }
    }

    public class ZergTower : Tower
    {
        protected override List<Unit> SortTargetsInRange(List<Unit> targets)
        {
            return targets
                .OrderByDescending(u => u.PosY)
                .ThenBy(u => u.Health)
                .ToList();
        }
    }
}