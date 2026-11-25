using GameFramework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI_Strategy
{
    public class XenoSoldier : Soldier
    {
        public int TargetColumn;
        public int PartnerColumn;

        public override void Move()
        {
            if (speed <= 0 || posY >= PlayerLane.HEIGHT)
            {
                return;
            }

            int x = posX;
            int y = posY;
            int towardsTarget = Math.Sign(TargetColumn - x);
            int towardsPartner = Math.Sign(PartnerColumn - x);

            if (MoveTo(x + towardsTarget, y + 1))
            {
                return;
            }
            if (MoveTo(x + towardsPartner, y + 1))
            {
                return;
            }
            if (MoveTo(x, y + 1))
            {
                return;
            }
            if (MoveTo(x - 1, y + 1))
            {
                return;
            }
            if (MoveTo(x + 1, y + 1))
            {
                return;
            }
            if (MoveTo(x + 1, y))
            {
                return;
            }
            MoveTo(x - 1, y);
        }

        protected override List<Unit> SortTargetsInRange(List<Unit> targets)
        {
            return targets.OrderBy(unit => unit.Health).ToList();
        }
    }
}
