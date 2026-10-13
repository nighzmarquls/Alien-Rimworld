
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

            switch (XMTHiveUtility.CryptimorphRoomShapeStage(room))
            {
                case 2:
                    return ThoughtState.ActiveAtStage(2);
                case 0:
                    return ThoughtState.ActiveAtStage(0);
                case 1:
                    return ThoughtState.ActiveAtStage(1);
            }

            return ThoughtState.Inactive;
        }
    }
}
