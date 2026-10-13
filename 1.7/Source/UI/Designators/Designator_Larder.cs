using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class Designator_Larder : Designator
    {
        public override bool DragDrawMeasurements => true;
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        private List<Pawn> justDesignated = new List<Pawn>();
        protected override DesignationDef Designation => XenoWorkDefOf.XMT_Larder;

        public override bool Disabled
        {
            get
            {
               

                if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_LarderSerum))
                {
                    return false;
                }
                
                return true;

            }
        }
        public override bool Visible
        {
            get
            {

                if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_LarderSerum))
                {
                    return true;
                }
                
                return false;
            }
        }
        public Designator_Larder()
        {
            defaultLabel = "XMT_CommandLarder".Translate();
            defaultDesc = "XMT_CommandLarderDescription".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Larder");
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Hunt;
            hotKey = KeyBindingDefOf.Misc11;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(base.Map))
            {
                return false;
            }
            IEnumerable<Thing> pawns = PawnInCell(c);
            if (pawns == null )
            {
                return "XMT_MessageMustDesignateLarder".Translate();
            }

            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            foreach (Pawn pawn in PawnInCell(loc))
            {
                DesignateThing(pawn);
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if(base.Map.designationManager.DesignationOn(t, Designation) != null)
            {
                return false;
            }
            if (t is Pawn pawn)
            {
                if (pawn.Downed && XMTHiveUtility.IsMorphingCandidate(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        public override void DesignateThing(Thing t)
        {
            base.Map.designationManager.RemoveAllDesignationsOn(t);
            base.Map.designationManager.AddDesignation(new Designation(t, Designation));
            justDesignated.Add((Pawn)t);
        }

        protected override void FinalizeDesignationSucceeded()
        {
            base.FinalizeDesignationSucceeded();

            justDesignated.Clear();
        }

        private IEnumerable<Thing> PawnInCell(IntVec3 c)
        {
            if (c.Fogged(base.Map))
            {
                yield break;
            }

            List<Thing> thingList = c.GetThingList(base.Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (CanDesignateThing(thingList[i]).Accepted)
                {
                    yield return thingList[i];
                }
            }
        }
    }
}
