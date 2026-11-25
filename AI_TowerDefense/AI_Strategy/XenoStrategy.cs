using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework;

namespace AI_Strategy
{
    public class XenoStrategy : AbstractStrategy
    {
        private const int START_GOLD_LIMIT = 30;
        private const int WAVE_ALARM = 5;
        private const int WAVE_LANE_SIZE = 5;
        private const int TRIGGER_BUFFER = 2;
        private const int SOLDIER_COST = 2;
        private static readonly int TOWER_ROW_MIN = 3;
        private static readonly int TOWER_ROW_MAX = PlayerLane.HEIGHT - 3;
        private static readonly int[] SINGLE_ATTACK_COLUMNS = { 0, PlayerLane.WIDTH - 1, PlayerLane.WIDTH / 2 };

        private bool started = false;
        private int waveLeftColumn;
        private int waveLeftRemaining;
        private int waveRightRemaining;

        private List<Soldier> enemySoldiers;
        private List<Tower> myTowers;
        private List<Tower> enemyTowers;
        private List<Soldier> mySoldiers;

        public XenoStrategy(Player player) : base(player)
        {

        }

        public override void DeployTowers()
        {
            ScanField();
            if (!started)
            {
                return;
            }

            if (enemySoldiers.Count >= WAVE_ALARM)
            {
                PlaceWaveTower();
            }
            else if (!EveryColumnHasTower())
            {
                PlaceTowersForUncoveredEnemies();
            }
        }

        public override void DeploySoldiers()
        {
            ScanField();
            if (!started)
            {
                return;
            }

            if (waveLeftRemaining > 0 || waveRightRemaining > 0)
            {
                ContinueWave();
            }
            else if (player.Gold >= 2 * WAVE_LANE_SIZE * SOLDIER_COST)
            {
                StartWave();
            }

            SendSingleSoldiers();
        }

        public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
        {
            return unsortedList.OrderByDescending(soldier => soldier.PosY).ToList();
        }

        public override List<Tower> SortedTowerArray(List<Tower> unsortedList)
        {
            return unsortedList.OrderBy(EnemiesInRange).ToList();
        }

        private int EnemiesInRange(Tower tower)
        {
            int count = 0;
            for (int x = tower.PosX - 2; x <= tower.PosX + 2; x++)
            {
                for (int y = tower.PosY - 2; y <= tower.PosY + 2; y++)
                {
                    Unit unit = player.HomeLane.GetCellAt(x, y)?.Unit;
                    if (unit is Soldier && unit.Player != player)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private void ScanField()
        {
            enemySoldiers = new List<Soldier>();
            myTowers = new List<Tower>();
            enemyTowers = new List<Tower>();
            mySoldiers = new List<Soldier>();

            for (int x = 0; x < PlayerLane.WIDTH; x++)
            {
                for (int y = 0; y < PlayerLane.HEIGHT; y++)
                {
                    Unit home = player.HomeLane.GetCellAt(x, y).Unit;
                    if (home is Soldier enemySoldier && home.Player != player)
                    {
                        enemySoldiers.Add(enemySoldier);
                    }
                    if (home is Tower myTower && home.Player == player)
                    {
                        myTowers.Add(myTower);
                    }

                    Unit away = player.EnemyLane.GetCellAt(x, y).Unit;
                    if (away is Tower enemyTower && away.Player != player)
                    {
                        enemyTowers.Add(enemyTower);
                    }
                    if (away is Soldier mySoldier && away.Player == player)
                    {
                        mySoldiers.Add(mySoldier);
                    }
                }
            }

            if (enemySoldiers.Count > 0 || enemyTowers.Count > 0 || player.Gold >= START_GOLD_LIMIT)
            {
                started = true;
            }
        }

        private void PlaceWaveTower()
        {
            int bestX = -1, bestY = -1, bestCount = 0;

            for (int x = 0; x < PlayerLane.WIDTH; x++)
            {
                for (int y = TOWER_ROW_MIN; y <= TOWER_ROW_MAX; y++)
                {
                    if (!IsAllowedTowerCell(x, y))
                    {
                        continue;
                    }

                    int count = enemySoldiers.Count(soldier => CanStillKill(x, y, soldier));
                    if (count > bestCount)
                    {
                        bestX = x;
                        bestY = y;
                        bestCount = count;
                    }
                }
            }

            if (bestCount > 0)
            {
                TryPlaceTower(bestX, bestY);
            }
        }

        private void PlaceTowersForUncoveredEnemies()
        {
            foreach (Soldier soldier in FindUncoveredEnemies())
            {
                if (soldier.PosY < TOWER_ROW_MAX - 1 - TRIGGER_BUFFER)
                {
                    continue;
                }
                PlaceTowerFor(soldier);
            }
        }

        private List<Soldier> FindUncoveredEnemies()
        {
            var uncovered = new List<Soldier>();
            var waiting = new List<Soldier>();
            var freeTowers = new List<Tower>();

            for (int column = 0; column < PlayerLane.WIDTH; column++)
            {
                waiting.AddRange(enemySoldiers.Where(soldier => soldier.PosX == column));
                freeTowers.AddRange(myTowers.Where(tower => tower.PosX == column));

                freeTowers.RemoveAll(tower => tower.PosX < column - 2);
                uncovered.AddRange(waiting.Where(soldier => soldier.PosX < column - 2));
                waiting.RemoveAll(soldier => soldier.PosX < column - 2);

                foreach (Soldier soldier in waiting.ToList())
                {
                    Tower match = freeTowers.FirstOrDefault(tower => CanStillKill(tower.PosX, tower.PosY, soldier));
                    if (match != null)
                    {
                        waiting.Remove(soldier);
                        freeTowers.Remove(match);
                    }
                }
            }

            uncovered.AddRange(waiting);
            return uncovered;
        }

        private void PlaceTowerFor(Soldier soldier)
        {
            int sx = soldier.PosX;
            var columns = new List<int> { sx - 2, sx + 2 }
                .OrderBy(column => ColumnHasTower(column) ? 1 : 0)
                .ToList();
            columns.Add(PlayerLane.WIDTH / 2);
            columns.Add(sx - 1);
            columns.Add(sx + 1);

            foreach (int x in columns)
            {
                if (Math.Abs(x - sx) > 2)
                {
                    continue;
                }

                for (int y = TOWER_ROW_MAX; y > soldier.PosY; y--)
                {
                    if (TryPlaceTower(x, y))
                    {
                        return;
                    }
                }
            }
        }

        private void SendSingleSoldiers()
        {
            foreach (int column in SINGLE_ATTACK_COLUMNS)
            {
                bool alreadyThere = mySoldiers.Any(soldier => soldier.PosX == column);
                if (Coverage(column) == 0 && !alreadyThere && player.Gold >= TowerReserve() + SOLDIER_COST)
                {
                    SpawnSoldier(column, column, column);
                }
            }
        }

        private void StartWave()
        {
            int best = 0;
            for (int column = 1; column < PlayerLane.WIDTH - 1; column++)
            {
                if (Coverage(column) + Coverage(column + 1) < Coverage(best) + Coverage(best + 1))
                {
                    best = column;
                }
            }

            waveLeftColumn = best;
            waveLeftRemaining = WAVE_LANE_SIZE;
            waveRightRemaining = WAVE_LANE_SIZE;
            ContinueWave();
        }

        private void ContinueWave()
        {
            int left = waveLeftColumn;
            int right = waveLeftColumn + 1;

            if (waveLeftRemaining > 0 && SpawnSoldier(SpawnColumn(left, -1), left, right))
            {
                waveLeftRemaining--;
            }
            if (waveRightRemaining > 0 && SpawnSoldier(SpawnColumn(right, +1), right, left))
            {
                waveRightRemaining--;
            }
        }

        private int SpawnColumn(int column, int outwards)
        {
            int x = column + 2 * outwards;
            if (x >= 0 && x < PlayerLane.WIDTH)
            {
                return x;
            }
            else
            {
                return column - 2 * outwards;
            }
        }

        private bool SpawnSoldier(int x, int targetColumn, int partnerColumn)
        {
            if (player.TryBuySoldier<XenoSoldier>(x, out var soldier) != Player.SoldierPlacementResult.Success)
            {
                return false;
            }

            soldier.TargetColumn = targetColumn;
            soldier.PartnerColumn = partnerColumn;
            return true;
        }

        private bool CanStillKill(int towerX, int towerY, Soldier soldier)
        {
            return Math.Abs(towerX - soldier.PosX) <= 2 && soldier.PosY < towerY;
        }

        private bool IsAllowedTowerCell(int x, int y)
        {
            if (y < TOWER_ROW_MIN || y > TOWER_ROW_MAX)
            {
                return false;
            }

            Cell cell = player.HomeLane.GetCellAt(x, y);
            if (cell == null || cell.Unit != null)
            {
                return false;
            }

            return myTowers.Count == 0 || (x + y) % 2 == (myTowers[0].PosX + myTowers[0].PosY) % 2;
        }

        private bool TryPlaceTower(int x, int y)
        {
            if (!IsAllowedTowerCell(x, y))
            {
                return false;
            }
            if (player.TryBuyTower<XenoTower>(x, y, out var tower) != Player.TowerPlacementResult.Success)
            {
                return false;
            }

            myTowers.Add(tower);
            return true;
        }

        private bool ColumnHasTower(int column) => myTowers.Any(tower => tower.PosX == column);

        private bool EveryColumnHasTower() => Enumerable.Range(0, PlayerLane.WIDTH).All(ColumnHasTower);

        private int Coverage(int column) => enemyTowers.Count(tower => Math.Abs(tower.PosX - column) <= 2);

        private int TowerReserve()
        {
            int next = Tower.GetNextTowerCosts(player.HomeLane);
            return next + (next + 1);
        }
    }
}
