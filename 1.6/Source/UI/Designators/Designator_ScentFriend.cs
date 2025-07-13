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
        private List<Pawn> justDesignated = new List<Pawn>();
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

                Pawn Queen = XMTUtility.GetQueen();

                return Queen.Faction != null && Queen.Faction.IsPlayer;
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
                return true;
            }

            return false;
        }

        public override void DesignateThing(Thing t)
        {
            //base.Map.designationManager.RemoveAllDesignationsOn(t);
            //Map.designationManager.AddDesignation(new Designation(t, Designation));
            justDesignated.Add((Pawn)t);
        }

        protected override void FinalizeDesignationSucceeded()
        {
            base.FinalizeDesignationSucceeded();

            foreach (Pawn designated in justDesignated)
            {
                CompPawnInfo info = designated.GetComp<CompPawnInfo>();

                if (info != null)
                {
                    info.ApplyFriendlyPheromone(XMTUtility.GetQueen(), 0.25f, 1.5f);
                }
            }
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
