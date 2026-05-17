
using RimWorld;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal class ThoughtWorker_RoomShape : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.story.traits.HasTrait(TraitDefOf.Ascetic))
            {
                return ThoughtState.Inactive;
            }

            Room room = p.GetRoom();
            if (room == null)
            {
                return ThoughtState.Inactive;
            }

            if(room.PsychologicallyOutdoors)
            {
                return ThoughtState.Inactive;
            }
            
            if(room.CellCount <= 1)
            {
                return ThoughtState.Inactive;
            }

            if(!XMTUtility.IsXenomorph(p))
            {
                return ThoughtState.Inactive;
            }

            int borderCells = room.BorderCellsCardinal.Count();

            int sideCells = borderCells / 4;

            int squareCellCount = sideCells* sideCells;

            float difference = squareCellCount - room.CellCount;

            float ratioOfDifference = difference / room.CellCount;

            switch (ratioOfDifference)
            {
                case > 0.25f:
                    return ThoughtState.ActiveAtStage(2);
                case > 0.10f:
                    return ThoughtState.Inactive;
                case < 0.05f:
                    return ThoughtState.ActiveAtStage(0);
            }

            return ThoughtState.ActiveAtStage(1);
        }
    }
}
