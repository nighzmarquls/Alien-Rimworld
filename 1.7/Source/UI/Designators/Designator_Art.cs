using RimWorld;

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class Designator_Art : Designator
    {
        public override bool DragDrawMeasurements => true;
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        private List<Corpse> justDesignated = new List<Corpse>();
        protected override DesignationDef Designation => XenoWorkDefOf.XMT_CorpseArt;

        public override bool Disabled
        {
            get
            {
                if (XenoSocialDefOf.XMT_Starbeast_Sculpture.IsFinished)
                {

                    if (XMTUtility.PlayerXenosOnMap(Find.CurrentMap))
                    {
                        return false;
                    }
                }
                return true;

            }
        }
        public override bool Visible
        {
            get
            {
                if (XenoSocialDefOf.XMT_Starbeast_Sculpture.IsFinished)
                {

                    if (XMTUtility.PlayerXenosOnMap(Find.CurrentMap))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public Designator_Art()
        {
            defaultLabel = "XMT_CommandArt".Translate();
            defaultDesc = "XMT_CommandArtDescription".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Art");
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

            if (!CorpseInCell(c).Any())
            {
                return "XMT_MessageMustDesignateArtable".Translate();
            }

            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            foreach (Corpse item in CorpseInCell(loc))
            {
                DesignateThing(item);
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (t is Corpse corpse && base.Map.designationManager.DesignationOn(corpse, Designation) == null)
            {
                if (corpse.IsDessicated())
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
            justDesignated.Add((Corpse)t);
        }

        protected override void FinalizeDesignationSucceeded()
        {
            base.FinalizeDesignationSucceeded();
       
            justDesignated.Clear();
        }

        private IEnumerable<Corpse> CorpseInCell(IntVec3 c)
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
                    yield return (Corpse)thingList[i];
                }
            }
        }
    }
}
