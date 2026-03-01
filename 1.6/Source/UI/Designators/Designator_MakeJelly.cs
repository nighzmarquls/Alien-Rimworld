
using RimWorld;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using static Verse.HediffCompProperties_RandomizeSeverityPhases;

namespace Xenomorphtype
{
    internal class Designator_MakeJelly : Designator
    {
        public CompJellyMakerProperties JellyMakingProperties
        {
            get
            {
                if(_jellyMakingProperties == null)
                {
                    foreach (CompProperties properties in InternalDefOf.XMT_Starbeast_AlienRace.comps)
                    {
                        if(properties is CompJellyMakerProperties)
                        {
                            _jellyMakingProperties = (CompJellyMakerProperties)properties;
                            break;
                        }
                    }
                }
                return _jellyMakingProperties;
            }
        }
        private CompJellyMakerProperties _jellyMakingProperties = null;
        public override bool DragDrawMeasurements => true;
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        private List<Pawn> justDesignated = new List<Pawn>();
        protected override DesignationDef Designation => XenoWorkDefOf.XMT_MakeJelly;


        public override bool Disabled
        {
            get
            {


                if (XMTUtility.QueenIsPlayer())
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

                if (XMTUtility.QueenIsPlayer())
                {
                    return true;
                }

                return false;
            }
        }
        public Designator_MakeJelly()
        {
            defaultLabel = "XMT_CommandMakeJelly".Translate();
            defaultDesc = "XMT_CommandMakeJellyDescription".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/MakeJelly");
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


           
            if (!XenoformingUtility.CellIsFertile(c, base.Map))
            {
                if (!CanDesignateThingInCell(c, base.Map))
                {
                    return "XMT_MessageMustDesignateJelly".Translate();
                }
            }

            return true;
        }

        public bool CanDesignateThingInCell(IntVec3 c, Map map)
        {
            foreach(Thing thing in c.GetThingList(map))
            {
                if(JellyMakingProperties.jellyIngredientFilter.Allows(thing))
                {
                    return true;
                }
            }
            return false;
        } 

        public override void DesignateSingleCell(IntVec3 loc)
        {
            base.Map.designationManager.TryRemoveDesignation(loc, Designation);
            base.Map.designationManager.AddDesignation(new Designation(loc, Designation));
        }

        protected override void FinalizeDesignationSucceeded()
        {
            base.FinalizeDesignationSucceeded();

            justDesignated.Clear();
        }

    }
}
