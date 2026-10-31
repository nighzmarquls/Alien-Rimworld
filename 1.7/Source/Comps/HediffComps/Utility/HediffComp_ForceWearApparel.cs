using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_ForceWearApparel : HediffComp
    {
        HediffCompProperties_ForceWearApparel Props => props as HediffCompProperties_ForceWearApparel;
        bool finished = false;
        public override bool CompShouldRemove => finished;
        public override void CompExposeData() { Scribe_Values.Look(ref finished, "XMT_apparelAssigned"); }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (PawnGenerator.IsBeingGenerated(Pawn)) return;
            FinishSetup();
        }

        internal void FinishSetup()
        {
            if (finished) return;
            if (Props.slots != null && Props.slots.Count > 0)
            {
                foreach (XMT_ApparelSlot slot in Props.slots) WearFrom(slot.alternatives);
                finished = true;
                return;
            }

            if( parent.pawn.apparel == null)
            {
                finished = true;
                return;
            }

            List<ThingDef> apparelSelection = Props.headwear?.ToList() ?? new List<ThingDef>();
            apparelSelection.Shuffle();
            foreach (ThingDef def in apparelSelection)
            {

                if (parent.pawn.apparel.CanWearWithoutDroppingAnything(def))
                {

                    Apparel apparel =  ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def)) as Apparel;
                    parent.pawn.apparel.Wear(apparel);
                    break;
                }

            }

            apparelSelection = Props.accessory?.ToList() ?? new List<ThingDef>();
            apparelSelection.Shuffle();
            foreach (ThingDef def in apparelSelection)
            {

                if (parent.pawn.apparel.CanWearWithoutDroppingAnything(def))
                {
                    Apparel apparel = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def)) as Apparel;
                    parent.pawn.apparel.Wear(apparel);
                    break;
                }

            }

            apparelSelection = Props.bodywear?.ToList() ?? new List<ThingDef>();
            apparelSelection.Shuffle();
            foreach (ThingDef def in apparelSelection)
            {

                if (parent.pawn.apparel.CanWearWithoutDroppingAnything(def))
                {
                    Apparel apparel = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def)) as Apparel;
                    parent.pawn.apparel.Wear(apparel);
                    break;
                }

            }

            finished = true;
        }

        private void WearFrom(List<ThingDef> alternatives)
        {
            if (Pawn.apparel == null) return;
            foreach (ThingDef definition in alternatives ?? new List<ThingDef>())
            {
                if (definition == null) continue;
                if (!Props.replaceConflicts && !Pawn.apparel.CanWearWithoutDroppingAnything(definition)) continue;
                if (Props.replaceConflicts)
                    foreach (Apparel old in Pawn.apparel.WornApparel.Where(a => !ApparelUtility.CanWearTogether(a.def, definition, Pawn.RaceProps.body)).ToList())
                    {
                        Pawn.apparel.Remove(old);
                        if (Pawn.inventory?.innerContainer.TryAdd(old, canMergeWithExistingStacks: false) != true) old.Destroy();
                    }
                Apparel apparel = (Apparel)ThingMaker.MakeThing(definition, GenStuff.DefaultStuffFor(definition));
                apparel.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                Pawn.apparel.Wear(apparel);
                break;
            }
        }
    }

    public class XMT_ApparelSlot { public List<ThingDef> alternatives = new List<ThingDef>(); }

    public class HediffCompProperties_ForceWearApparel : HediffCompProperties
    {
        public List<ThingDef> headwear;
        public List<ThingDef> bodywear;
        public List<ThingDef> accessory;
        public List<XMT_ApparelSlot> slots;
        public bool replaceConflicts;
        public HediffCompProperties_ForceWearApparel()
        {
            compClass = typeof(HediffComp_ForceWearApparel);
        }
    }
}
