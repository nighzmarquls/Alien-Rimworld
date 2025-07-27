using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class Designator_Abduct : Designator
    {
        private List<Pawn> justDesignated = new List<Pawn>();
        protected override DesignationDef Designation => XenoWorkDefOf.XMT_Abduct;

        public override bool Disabled
        {
            get
            {
                return !XMTUtility.PlayerXenosOnMap(Find.CurrentMap);
            }
        }
        public override bool Visible
        {
            get
            {
                return XMTUtility.PlayerXenosOnMap(Find.CurrentMap);
            }
        }
        public Designator_Abduct()
        {
            defaultLabel = "XMT_CommandAbduct".Translate();
            defaultDesc = "XMT_CommandAbductDescription".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Abduct");
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

            if (!AbductableInCell(c).Any())
            {
                return "XMT_MessageMustDesignateAbductable".Translate();
            }

            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            foreach (Pawn item in AbductableInCell(loc))
            {
                DesignateThing(item);
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (t is Pawn pawn && base.Map.designationManager.DesignationOn(pawn, Designation) == null)
            {
                return true;
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
            foreach (PawnKindDef kind in justDesignated.Select((Pawn p) => p.kindDef).Distinct())
            {
                ShowDesignationWarnings(justDesignated.First((Pawn x) => x.kindDef == kind));
            }

            justDesignated.Clear();
        }

        private IEnumerable<Pawn> AbductableInCell(IntVec3 c)
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
                    yield return (Pawn)thingList[i];
                }
            }
        }

        public static void ShowDesignationWarnings(Pawn pawn)
        {
            float manhunterOnDamageChance = pawn.RaceProps.manhunterOnDamageChance;
            float manhunterOnDamageChance2 = PawnUtility.GetManhunterOnDamageChance(pawn);
            if (manhunterOnDamageChance >= 0.015f)
            {
                Messages.Message("MessageAnimalsGoPsychoHunted".Translate(pawn.kindDef.GetLabelPlural().CapitalizeFirst(), manhunterOnDamageChance2.ToStringPercent(), pawn.Named("ANIMAL")).CapitalizeFirst(), pawn, MessageTypeDefOf.CautionInput, historical: false);
            }

            SlaughterDesignatorUtility.CheckWarnAboutVeneratedAnimal(pawn);
        }
    }
}
