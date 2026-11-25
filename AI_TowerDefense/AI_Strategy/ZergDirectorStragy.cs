using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework;

namespace AI_Strategy
{
    public class ZergDirectorStrategy : AbstractStrategy
    {
        private readonly Dictionary<Tower, List<ZergSoldier>> squadAssignments = new Dictionary<Tower, List<ZergSoldier>>();
        private const int SQUAD_SIZE = 4;

        private readonly (int x, int y)[] farmSpots = new[]
        {
            (1, 6),
            (5, 6),
            (3, 9),
            (3, 12)
        };

        public ZergDirectorStrategy(Player player) : base(player)
        {
        }

        public override void DeployTowers()
        {
            if (player.HomeLane.TowerCount() >= farmSpots.Length) return;

            foreach (var spot in farmSpots)
            {
                int nextCost = Tower.GetNextTowerCosts(player.HomeLane);
                if (player.Gold < nextCost) return;

                if (player.HomeLane.GetCellAt(spot.x, spot.y)?.Unit == null)
                {
                    if (player.TryBuyTower<ZergTower>(spot.x, spot.y) == Player.TowerPlacementResult.Success)
                    {
                        return;
                    }
                }
            }
        }

        public override void DeploySoldiers()
        {
            CleanupAssignments();

            List<Tower> enemyTowers = GetEnemyTowers();

            foreach (Tower tower in enemyTowers)
            {
                if (!squadAssignments.ContainsKey(tower))
                {
                    squadAssignments[tower] = new List<ZergSoldier>();
                }
            }

            AssignUnassignedSoldiers(enemyTowers);

            if (enemyTowers.Count > 0)
            {
                Tower targetTower = enemyTowers
                    .OrderBy(t => squadAssignments[t].Count >= SQUAD_SIZE ? 1 : 0)
                    .ThenBy(t => t.PosY)
                    .First();

                int needed = SQUAD_SIZE - squadAssignments[targetTower].Count;

                if (needed > 0)
                {
                    int goldNeeded = needed * 2;
                    if (player.Gold < goldNeeded && player.Gold < 14)
                    {
                        return;
                    }

                    SpawnSquadForTower(targetTower, needed);
                    return;
                }
            }

            while (player.Gold >= 2)
            {
                int bestX = GetSafestSpawnColumn(enemyTowers);
                if (bestX == -1) break;

                if (player.TryBuySoldier<ZergSoldier>(bestX, out ZergSoldier soldier) == Player.SoldierPlacementResult.Success)
                {
                    if (enemyTowers.Count > 0)
                    {
                        Tower supportTarget = enemyTowers.OrderBy(t => squadAssignments[t].Count).First();
                        soldier.TargetTower = supportTarget;
                        squadAssignments[supportTarget].Add(soldier);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void SpawnSquadForTower(Tower targetTower, int count)
        {
            int[] preferredColumns = Enumerable.Range(0, PlayerLane.WIDTH)
                .OrderBy(x => Math.Abs(x - targetTower.PosX))
                .ToArray();

            int spawned = 0;
            foreach (int x in preferredColumns)
            {
                if (spawned >= count || player.Gold < 2) break;

                if (player.EnemyLane.GetCellAt(x, 0)?.Unit == null)
                {
                    if (player.TryBuySoldier<ZergSoldier>(x, out ZergSoldier soldier) == Player.SoldierPlacementResult.Success)
                    {
                        soldier.TargetTower = targetTower;
                        squadAssignments[targetTower].Add(soldier);
                        spawned++;
                    }
                }
            }
        }

        private void AssignUnassignedSoldiers(List<Tower> enemyTowers)
        {
            if (enemyTowers.Count == 0) return;

            List<ZergSoldier> freeSoldiers = GetAliveSoldiers()
                .Where(s => s.TargetTower == null || s.TargetTower.Health <= 0 || s.TargetTower.PosX < 0)
                .ToList();

            foreach (ZergSoldier soldier in freeSoldiers)
            {
                Tower needyTower = enemyTowers
                    .Where(t => squadAssignments[t].Count < SQUAD_SIZE)
                    .OrderBy(t => t.PosY)
                    .FirstOrDefault();

                if (needyTower != null)
                {
                    soldier.TargetTower = needyTower;
                    squadAssignments[needyTower].Add(soldier);
                }
                else
                {
                    soldier.TargetTower = null;
                }
            }
        }

        private void CleanupAssignments()
        {
            List<Tower> deadTowers = squadAssignments.Keys
                .Where(t => t.Health <= 0 || t.PosX < 0)
                .ToList();

            foreach (Tower dead in deadTowers)
            {
                foreach (ZergSoldier s in squadAssignments[dead])
                {
                    s.TargetTower = null;
                }
                squadAssignments.Remove(dead);
            }

            foreach (var kvp in squadAssignments)
            {
                kvp.Value.RemoveAll(s => s.Health <= 0 || s.PosX < 0);
            }
        }

        private List<Tower> GetEnemyTowers()
        {
            List<Tower> towers = new List<Tower>();
            for (int x = 0; x < PlayerLane.WIDTH; x++)
            {
                for (int y = 0; y < PlayerLane.HEIGHT; y++)
                {
                    if (player.EnemyLane.GetCellAt(x, y)?.Unit is Tower tower && tower.Health > 0)
                    {
                        towers.Add(tower);
                    }
                }
            }
            return towers;
        }

        private List<ZergSoldier> GetAliveSoldiers()
        {
            List<ZergSoldier> soldiers = new List<ZergSoldier>();
            for (int x = 0; x < PlayerLane.WIDTH; x++)
            {
                for (int y = 0; y < PlayerLane.HEIGHT; y++)
                {
                    if (player.EnemyLane.GetCellAt(x, y)?.Unit is ZergSoldier soldier && soldier.Health > 0)
                    {
                        soldiers.Add(soldier);
                    }
                }
            }
            return soldiers;
        }

        private int GetSafestSpawnColumn(List<Tower> enemyTowers)
        {
            int bestX = -1;
            int lowestDanger = int.MaxValue;

            for (int x = 0; x < PlayerLane.WIDTH; x++)
            {
                if (player.EnemyLane.GetCellAt(x, 0)?.Unit != null) continue;

                int danger = enemyTowers.Count(t => Math.Abs(t.PosX - x) <= 2);
                if (danger < lowestDanger)
                {
                    lowestDanger = danger;
                    bestX = x;
                }
            }

            return bestX;
        }

        public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
        {
            return unsortedList
                .OrderByDescending(s => s.PosY)
                .ThenBy(s => s is ZergSoldier zs && zs.TargetTower != null ? Math.Abs(s.PosX - zs.TargetTower.PosX) : 0)
                .ToList();
        }

        public override List<Tower> SortedTowerArray(List<Tower> unsortedList)
        {
            return unsortedList
                .OrderByDescending(t => t.PosY)
                .ToList();
        }
    }
}