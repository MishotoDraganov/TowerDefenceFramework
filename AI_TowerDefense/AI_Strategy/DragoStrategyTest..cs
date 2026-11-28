using GameFramework;
using System;
using System.Collections.Generic;
using static GameFramework.Player;

namespace AI_Strategy
{

    public class Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
        public static float Distance(Vector2 a, Vector2 b)
        {
            return (float)Math.Sqrt(Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2));
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
        public Vector3 EconomicProbalisticTriangle { get; set; }
        public int MinGoldForBurst { get; private set; }
        public int MinGoldForSurplus { get; private set; } //to spawn towers if all soldiers are spawned

        public bool Adaptable { get; private set; }


        //Soldier Place and Move Chromosome; adds weight to A* for now-> will change it to D* Lite;
        public float SoldierAvoidingTowerValue { get; private set; }
        public float SoldierSafetyValue { get; private set; } //proximity to other soldiers, perhaps te formation
        public float SoldierQuickestPathValue { get; private set; }



        //Tower Placing Chromosome; Places tower on Node with highest value;
        public float TowerProximityValue { get; private set; } //proximity to other towers, perhaps te formation
        public float TowerOptimalRangeSoldierRange { get; private set; } //places towers in response to a concentrated swarm
        public float TowerAvoidSoldierRange { get; private set; } //avoids placing towers in range of many soldiers to avoid instakill of towers

        public float TowerYValue { get; private set; }

        public Genes(
            Vector3 economicTriangle,int minGoldForBurst, int minGoldForSurplus, bool adaptable,
            float towerAvoidingValue, float soldierSafetyValue, float quickestTurnValue, 
            float towerproximityValue, float respondToSoldierPlacementValue, float avoidSoldierRangeValue, float towerYValue)
        {
            EconomicProbalisticTriangle = economicTriangle;
            EconomicProbalisticTriangle = EconomicProbalisticTriangle.normalized();
            Adaptable = adaptable;

            MinGoldForBurst = minGoldForBurst;
            MinGoldForSurplus = minGoldForSurplus;

            SoldierAvoidingTowerValue = towerAvoidingValue;
            SoldierSafetyValue = soldierSafetyValue;
            SoldierQuickestPathValue = quickestTurnValue;

            TowerProximityValue = towerproximityValue;
            TowerOptimalRangeSoldierRange = respondToSoldierPlacementValue;
            TowerAvoidSoldierRange = avoidSoldierRangeValue;
            TowerYValue = towerYValue;
        }
    }

    public class PlaneData
    {
        public List<Soldier> Soldiers { get; private set; } = new List<Soldier>();
        public List<Tower> Towers { get; private set; } = new List<Tower>();
        private PlayerLane scannedPlane;

        public float[,] TowerPlacingScore = new float[PlayerLane.WIDTH, PlayerLane.HEIGHT];


        public PlaneData(PlayerLane scannedPlane)
        {
            this.scannedPlane = scannedPlane;
        }

        public void ScanPlane()
        {         
            Soldiers.Clear();
            Towers.Clear();

            for (int x = 0; x < PlayerLane.WIDTH; x++)
                for (int y = 0; y < PlayerLane.HEIGHT; y++)
                {
                    Unit cell = scannedPlane.GetCellAt(x, y).Unit;
                    if (cell is Soldier)
                        Soldiers.Add((Soldier)cell);
                    else if (cell is Tower)
                        Towers.Add((Tower)cell);
                }
            
        }

        public void ScanPlaneAndCalculateTowerPlacingScore(float towerProximityValue, float towerAvoidSoldierRange, float towerOptimalRangeSoldierRange, float towerYValue)
        {
             
            TowerPlacingScore = new float[PlayerLane.WIDTH, PlayerLane.HEIGHT];

            ScanPlane();

            
           
            for (int i = 0; i < Towers.Count; i++)
                AddScoreToTowerPlacementForRange(new Vector2(Towers[i].PosX, Towers[i].PosY), 2, towerProximityValue);

            
            for (int i = 0; i < Soldiers.Count; i++)
                for (int y = 0; y < PlayerLane.HEIGHT; y++) //big assumption that enemies are moving straight forward lol
                {
                    AddScoreToTowerPlacementForRange(new Vector2(Soldiers[i].PosX, y), 1, towerAvoidSoldierRange);
                    AddScoreToTowerPlacementForRange(new Vector2(Soldiers[i].PosX, y), 2, towerOptimalRangeSoldierRange);
                }
            
            for (int y = 0; y < PlayerLane.HEIGHT; y++)
                for (int x = 0; x < PlayerLane.WIDTH; x++)
                    TowerPlacingScore[x, y] += towerYValue * (y / (float)PlayerLane.HEIGHT);
            
            
        }

        private void AddScoreToTowerPlacementForRange(Vector2 cellPos, int range, float proximityValue)
        {    
            for (int x = (int)cellPos.x - range; x <= (int)cellPos.x + range; x++)
            {
                for (int y = (int)cellPos.y - range; y <= (int)cellPos.y + range; y++)
                {
                    if ((0 <= x && x < PlayerLane.WIDTH && 0 <= y && y < PlayerLane.HEIGHT) &&
                        ((int)cellPos.x != x && (int)cellPos.y != y))
                    {
                        float distance = Vector2.Distance(cellPos, new Vector2(x, y));

                        if (distance == 0)
                            continue;

                        TowerPlacingScore[x, y] += proximityValue / distance;
                    }
                }
            }
        }
    }

    public class DragoStrategyTest : AbstractStrategy
    {
        private static Random random = new Random();

        private static int playerGeneDistribution = 0;

        private Genes genes;

        private PlaneData homePlaneData, enemyPlaneData;
        public DragoStrategyTest(Player player) : base(player)
        {
            homePlaneData = new PlaneData(player.HomeLane);
            enemyPlaneData = new PlaneData(player.EnemyLane);



            if (playerGeneDistribution == 0)
                genes = new Genes(
                    new Vector3(0.9f, 0.0f, 0.07f), 49, 100, false,
                    1.0f, 1.0f, 1.0f,
                    1.0f, 1.0f, 1.0f, 0.0f);

            if (playerGeneDistribution == 1)
                genes = new Genes(
                         new Vector3(0.9f, 0.0f, 0.07f), 49, 100, false,
                         1.0f, 1.0f, 1.0f,
                         0.0f, 10.0f, 10.0f, 0.0f);


            playerGeneDistribution++;
        }
        bool isBursting = false;

        private void DecideEconomicAction(int soldiersPlacedThisTurn)
        {

            float buyTowerCost = Tower.GetNextTowerCosts(player.HomeLane); //reason against placing tower
            float buySoldierCost = 2;
            float towerSoldierCostRatio = (buyTowerCost / buySoldierCost);

            //as towers become more expensive, soldiers become more viable
            float towerPercentage = genes.EconomicProbalisticTriangle.x / towerSoldierCostRatio;
            float soldierPercentage = genes.EconomicProbalisticTriangle.y * towerSoldierCostRatio;
            float preserveMoneyPercentage = genes.EconomicProbalisticTriangle.z * player.HomeLane.TowerCount()*3;

            Vector3 finalVec = new Vector3(towerPercentage, soldierPercentage, preserveMoneyPercentage).normalized();
            towerPercentage = finalVec.x;
            soldierPercentage = finalVec.y;
            preserveMoneyPercentage = finalVec.z;

            List<Vector2> validTowerPlacements = ReturnValidTowerPlacements();

            if (player.Gold < genes.MinGoldForBurst && !isBursting)//regular
            {
                float chancePercent = (float)(random.Next(101)) / 100.0f;
                if (chancePercent < towerPercentage && validTowerPlacements.Count > 0 && player.Gold > buyTowerCost)
                {
                    TryPlaceTower();
                }
                else if (chancePercent < towerPercentage + soldierPercentage && player.Gold > buySoldierCost)
                {
                    PlaceSoldier(ref soldiersPlacedThisTurn);
                }
                else
                {
                    //DebugLogger.Log(" Preserve Money!");
                }
            }
            else//burst
            {
                
                isBursting = true;
                while (isBursting)
                {
                    if (player.Gold < buySoldierCost || soldiersPlacedThisTurn >= PlayerLane.WIDTH)
                    {
                        isBursting = false;
                    }
                    else
                    {
                        bool placedSucessfullyAndContinueBursting = PlaceSoldier(ref soldiersPlacedThisTurn);
                        isBursting = placedSucessfullyAndContinueBursting;
                    }
                }
                
            }


            
            
            if (soldiersPlacedThisTurn >= PlayerLane.WIDTH && player.Gold > genes.MinGoldForSurplus + buyTowerCost)
            {
                int enoughToPlaceSoldiers = PlayerLane.WIDTH * 2;
                bool towerPlaced = true;
                while (player.Gold > enoughToPlaceSoldiers + buyTowerCost && towerPlaced)
                {
                    towerPlaced = TryPlaceTower();
                    if (!towerPlaced)
                        break;
                }
            }
            
        }

        private List<Vector2> ReturnValidTowerPlacements()
        {
            List<Vector2> validCords = new List<Vector2>();

            for (int y = PlayerLane.HEIGHT-1; y > PlayerLane.HEIGHT_OF_SAFETY_ZONE+1; y--) 
            { 
                for (int x = 0; x < PlayerLane.WIDTH; x++)
                {
                    if (player.HomeLane.GetCellAt(x, y)!=null && player.HomeLane.GetCellAt(x, y).Unit == null)
                    {
                        if (!player.CheckAdjacentCellsAny(player.HomeLane, x, y, cell => cell.Unit is Tower))
                        {
                                validCords.Add(new Vector2(x, y));
                        }
                    }
                }
            }

            return validCords;
        }

        private bool TryPlaceTower()//it gets stuck here somewhere omg im gonna kms 
        {
            
           
            List<Vector2> validPlacements = ReturnValidTowerPlacements();


            if (validPlacements!=null && validPlacements.Count > 0 && validPlacements[0] != null)
            {
                homePlaneData.ScanPlaneAndCalculateTowerPlacingScore(genes.TowerProximityValue, genes.TowerAvoidSoldierRange, genes.TowerOptimalRangeSoldierRange, genes.TowerYValue);

                int bestPlacementIndex = 0;
                float bestPlacementScore = 0;


                for (int i =0; i<validPlacements.Count; i++)
                {
                   if(homePlaneData.TowerPlacingScore[(int)validPlacements[i].x, (int)validPlacements[i].y] > bestPlacementScore)
                   {
                       bestPlacementIndex = i;
                       bestPlacementScore = homePlaneData.TowerPlacingScore[(int)validPlacements[i].x, (int)validPlacements[i].y];
                   }
                }



  

                
                Vector2 placement = validPlacements[bestPlacementIndex];
                DebugLogger.Log("try place at best place" + placement.x + ", " + placement.y);
                TowerPlacementResult result = player.TryBuyTower<Tower>((int)placement.x, (int)placement.y);

                if (result == TowerPlacementResult.Success)
                {
                    DebugLogger.Log("place success " + placement.x + ", " + placement.y);
                    return true;
                }
                else
                {
                    DebugLogger.Log("place failed " + placement.x + ", " + placement.y + " result: " + result);
                    return false;
                }
               
        }

             
            return false;
        }
        public bool PlaceSoldier(ref int soldiersPlacedThisTurn)
        {
            if (soldiersPlacedThisTurn >= PlayerLane.WIDTH)
            {
                DebugLogger.Log("CAN'T PLACE MORE SOLDIERS!");
                return false;
            }



            for (int x=0; x<PlayerLane.WIDTH; x++)
            {
                if (player.EnemyLane.GetCellAt(x, 0).Unit == null)
                {
                    SoldierPlacementResult result = player.TryBuySoldier<DragoSoldier>(x, out var soldier);
                    if (result == SoldierPlacementResult.Success)
                    {
                        soldiersPlacedThisTurn++;
                        return true;
                    }
                }
            }
            return false;
        }

        public override void DeployTowers()
        {

            DecideEconomicAction(0);


        }

        public override void DeploySoldiers()
        {
           // DecideEconomicAction(player.Gold, 0, 0);
        }





        public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
        {
            return unsortedList;
        }
        public override List<Tower> SortedTowerArray(List<Tower> unsortedList)
        {
            return unsortedList;
        }
    }
}