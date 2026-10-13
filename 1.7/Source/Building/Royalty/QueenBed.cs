using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class QueenBed : Building_Bed
    {
        public override IntVec3 InteractionCell => FindPreferredInteractionCell();

        public IntVec3 FindPreferredInteractionCell()
        {
            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Position, 1, false);

            foreach(IntVec3 cell in cells)
            {
                if (cell.Standable(Map))
                {
                    return cell;
                }
            }

            return Position;
        }
    }
}
