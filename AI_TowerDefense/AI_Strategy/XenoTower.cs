using GameFramework;
using System.Collections.Generic;
using System.Linq;

namespace AI_Strategy
{
    public class XenoTower : Tower
    {
        protected override List<Unit> SortTargetsInRange(List<Unit> targets)
        {
            return targets.OrderByDescending(unit => unit.PosY).ThenBy(unit => unit.Health).ToList();
        }
    }
}
