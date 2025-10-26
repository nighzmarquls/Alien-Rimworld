using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class Designator_ScentFriend : Designator
    {
        public override bool DragDrawMeasurements => true;
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;
        private List<Pawn> justDesignated = new List<Pawn>();

        protected override DesignationDef Designation => XenoWorkDefOf.XMT_Friend;
        public Designator_ScentFriend()
        {
            defaultLabel = "XMT_CommandMarkFriend".Translate();
            defaultDesc = "XMT_CommandMarkFriendDescription".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Friend");
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Claim;
            hotKey = KeyBindingDefOf.Misc11;
        }
        public override bool Disabled
        {
            get
            {
                if (XMTUtility.NoQueenPresent())
                {
                    return true;
                }

                Pawn Queen = XMTUtility.GetQueen();

                return Queen.Faction == null || !Queen.Faction.IsPlayer;
            }
        }
        public override bool Visible
        {
            get
            {
                if (XMTUtility.NoQueenPresent())
                {
                    return false;
                }

                return XMTUtility.QueenIsPlayer();
            }
        }
        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(base.Map))
            {
                return false;
            }

            if (!MarkableInCell(c).Any())
            {
                return "XMT_MessageMustDesignateAlive".Translate();
            }

            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            foreach (Pawn item in MarkableInCell(loc))
            {
                DesignateThing(item);
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (t is Pawn pawn)
            {
                if(XMTUtility.IsXenomorph(pawn))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public override void DesignateThing(Thing t)
        {
            Map.designationManager.RemoveAllDesignationsOn(t);
            Map.designationManager.AddDesignation(new Designation(t, Designation));
            justDesignated.Add((Pawn)t);
        }

        protected override void FinalizeDesignationSucceeded()
        {
            base.FinalizeDesignationSucceeded();

            justDesignated.Clear();
        }

        private IEnumerable<Pawn> MarkableInCell(IntVec3 c)
        {
            if (c.Fogged(base.Map))
            {
                yield break;
            }

            List<Thing> thingList = c.GetThingList(base.Map);
            if (thingList != null)
            {
                for (int i = 0; i < thingList.Count; i++)
                {
                    if (CanDesignateThing(thingList[i]).Accepted)
                    {
                        yield return (Pawn)thingList[i];
                    }
                }
            }

            yield break;
        }
    }
}
