using GameFramework;
using System;
using System.Collections.Generic;
using static GameFramework.Player;

namespace AI_Strategy
{

    public class Vector2
    {
        public int x;
        public int y;

        public Vector2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

        public class Vector3
    {
        public float x, y, z;

        public Vector3(float x,float y,float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 normalized()
        {
            float total = x + y + z;
            return new Vector3(x / total, y / total, z / total);
        }
        public void Normalize()
        {
            float total = x + y + z;
            this.x = x / total;
            this.y = y / total;
            this.z = z / total;
        }
    }
   

    public class Genes
    {
        //Economic Decision Chromosome; 
        public Vector3 EconomicProbalisticTriangle { get; private set; }
        public int MinGoldForBurst { get; private set; }
        public int MinGoldForSurplus { get; private set; } //to spawn towers if all soldiers are spawned



        //Soldier Place and Move Chromosome; adds weight to A* for now-> will change it to D* Lite;
        public float TowerAvoidingValue { get; private set; }
        public float SoldierSafetyValue { get; private set; } //proximity to other soldiers, perhaps te formation
        public float QuickestTurnValue { get; private set; }



        //Tower Placing Chromosome; Places tower on Node with highest value;
        public float TowerSafetyValue { get; private set; } //proximity to other towers, perhaps te formation
        public float TowerCoverageValue { get; private set; } //simple coverage for now

        public Genes(Vector3 economicTriangle,int minGoldForBurst, int minGoldForSurplus, float towerAvoidingValue, float soldierSafetyValue, float quickestTurnValue, float towerSafetyValue, float towerCoverageValue)
        {
            EconomicProbalisticTriangle = economicTriangle;
            EconomicProbalisticTriangle = EconomicProbalisticTriangle.normalized();

            MinGoldForBurst = minGoldForBurst;
            MinGoldForSurplus = minGoldForSurplus;

            TowerAvoidingValue = towerAvoidingValue;
            SoldierSafetyValue = soldierSafetyValue;
            QuickestTurnValue = quickestTurnValue;
            TowerSafetyValue = towerSafetyValue;
            TowerCoverageValue = towerCoverageValue;
        }
    }

    public class DragoStrategyTest : AbstractStrategy
    {
        private static Random random = new Random();

        private static int playerGeneDistribution = 0;

        private Genes genes;
        public DragoStrategyTest(Player player) : base(player)
        {
            if (playerGeneDistribution == 0)
                genes = new Genes(
                    new Vector3(1.0f, 0f, 0f), 49, 100,
                    1.0f, 1.0f, 1.0f, 1.0f, 1.0f);

            if (playerGeneDistribution == 1)
                genes = new Genes(
                    new Vector3(0f, 0f, 1f), 49, 100,
                    1.0f, 1.0f, 1.0f, 1.0f, 1.0f);


            playerGeneDistribution++;

           // DebugLogger.Log("probs: (" + genes.EconomicProbalisticTriangle.x + ", " + genes.EconomicProbalisticTriangle.y + ", " + genes.EconomicProbalisticTriangle.z + ")");
        }
        bool isBursting = false;

        private void DecideEconomicAction(int soldiersPlacedThisTurn)
        {
            float buyTowerCost = player.HomeLane.TowerCount() + 2; //reason against placing tower
            float buySoldierCost = 2;
            float towerSoldierCostRatio = buyTowerCost / buySoldierCost;

            //as towers become more expensive, soldiers become more viable
            float towerPercentage = genes.EconomicProbalisticTriangle.x / towerSoldierCostRatio;
            float soldierPercentage = genes.EconomicProbalisticTriangle.y * towerSoldierCostRatio;
            float preserveMoneyPercentage = genes.EconomicProbalisticTriangle.z;

            List<Vector2> validTowerPlacements = ReturnValidTowerPlacements();

            if (player.Gold < genes.MinGoldForBurst && !isBursting)//regular
            {
                float chancePercent = (float)(random.Next(101)) / 100.0f;
                if (chancePercent < towerPercentage && validTowerPlacements.Count > 0)
                {
                    if (player.Gold < buyTowerCost) return;

                    DebugLogger.Log("Summon Tower! " + chancePercent + " " + towerPercentage + " " + soldierPercentage + " " + preserveMoneyPercentage);

                    bool towerPlaced = TryPlaceTower();
                    if (!towerPlaced)
                        return;

                    DecideEconomicAction(soldiersPlacedThisTurn);
                }
                else if (chancePercent < towerPercentage + soldierPercentage)
                {
                    if (player.Gold < buySoldierCost) return;

                    DebugLogger.Log("Summon Soldier!");
                    PlaceSoldier(ref soldiersPlacedThisTurn);
                    DecideEconomicAction(soldiersPlacedThisTurn);
                }
                else
                {
                    DebugLogger.Log(" Preserve Money!");
                }
            }
            else//burst
            {
                isBursting = true;
                while (true)
                {
                    float chancePercent = (float)(random.Next(101)) / 100.0f;
                    if (chancePercent < towerPercentage && validTowerPlacements.Count > 0)
                    {
                        if (player.Gold < buyTowerCost)
                        {
                            isBursting = false;
                            break;
                        }

                        DebugLogger.Log("Summon Tower! BURST!");
                        bool towerPlaced = TryPlaceTower();
                        if (!towerPlaced)
                            break;

                    }
                    else//the preserve chance goes into place soldier chance
                    {
                        if (player.Gold < buySoldierCost)
                        {
                            isBursting = false;
                            break;
                        }
                        

                        if (soldiersPlacedThisTurn >= PlayerLane.WIDTH)
                            break;

                        DebugLogger.Log("Summon Soldier! BURST!");
                        PlaceSoldier(ref soldiersPlacedThisTurn);
                    }
                }
            }

            


            if(soldiersPlacedThisTurn >= PlayerLane.WIDTH && player.Gold > genes.MinGoldForSurplus + buyTowerCost)
            {
                int enoughToPlaceSoldiers = PlayerLane.WIDTH * 2;
                while (player.Gold > enoughToPlaceSoldiers + buyTowerCost)
                {
                    DebugLogger.Log("Summon Tower! Surplus!");
                    bool towerPlaced = TryPlaceTower();
                    if(!towerPlaced)
                        break;
                }
            }
            
        }

        //fatal bug need to resolve -> if the player has enough gold to place a tower, but no valid placements, it will enter an infinite loop. Need to add a check for valid placements before attempting to place a tower.
        private List<Vector2> ReturnValidTowerPlacements()
        {
            List<Vector2> validCords = new List<Vector2>();

            for (int y = PlayerLane.HEIGHT-1; y > PlayerLane.HEIGHT_OF_SAFETY_ZONE+1; y--) 
            { 
                for (int x = 0; x < PlayerLane.WIDTH; x++)
                {
                    if (player.HomeLane.GetCellAt(x, y).Unit == null)
                    {
                        if (!player.CheckAdjacentCellsAny(player.HomeLane, x, y, cell => cell.Unit is Tower))
                        {
                            validCords.Add(new Vector2(x, y));
                            return validCords;
                        }
                    }
                }
            }

            return validCords;
        }
        public bool TryPlaceTower()
        {
            List<Vector2> validPlacements = ReturnValidTowerPlacements();


            if (validPlacements.Count > 0)
            {
                Vector2 placement = validPlacements[0];
                player.TryBuyTower<Tower>(placement.x, placement.y);
                return true;
            }

            return false;
        }
        public void PlaceSoldier(ref int soldiersPlacedThisTurn)
        {
            if (soldiersPlacedThisTurn > PlayerLane.WIDTH)
            {
                DebugLogger.Log("CAN'T PLACE MORE SOLDIERS!");
                return;
            }


            bool positioned = false;
            int count = 0;
            while (!positioned && count < 10)
            {
                count++;
                int x = random.Next(PlayerLane.WIDTH);
                int y = 0;
                if (player.EnemyLane.GetCellAt(x, y).Unit == null)
                {
                    positioned = true;
                    SoldierPlacementResult result = player.TryBuySoldier<DragoSoldier>(x, out var soldier);
                    if(result == SoldierPlacementResult.Success)
                        soldiersPlacedThisTurn++;               
                }
            }
        }



        public override void DeployTowers()
        {
            DecideEconomicAction(0);
        }

        public override void DeploySoldiers()
        {
           // DecideEconomicAction(player.Gold, 0, 0);

        }










        /*
         * called by the game play environment. The order in which the array is returned here is
         * the order in which soldiers will plan and perform their movement.
         *
         * The default implementation does not change the order. Do better!
         */
        public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
        {
            return unsortedList;
        }
    }
}


/*
 
 
  public class Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
        public float Magnitude()
        {
            return (float)Math.Sqrt(x * x + y * y);
        }
        public Vector3 GetProbabilities()
        {
            float mag = Magnitude();

            float R = 1.0f;//max dist of a vector inside the triangle from center
            float third = 1.0f / 3.0f;

            float p1 = third + (2 * y) / (3 * R);
            float p2 = third - ((y) / (3 * R)) - (x / ((float)Math.Sqrt(3.0f) * R));
            float p3 = third - ((y) / (3 * R)) + (x / ((float)Math.Sqrt(3.0f) * R));

            DebugLogger.Log(p1 + " " + p2 + " " + p3);

            return new Vector3(p1, p2, p3);
        }
    }


 */