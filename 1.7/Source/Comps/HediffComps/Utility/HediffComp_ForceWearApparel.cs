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

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {

            if( parent.pawn.apparel == null)
            {
                finished = true;
                return;
            }

            List<ThingDef> apparelSelection = Props.headwear;
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

            apparelSelection = Props.accessory;
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

            apparelSelection = Props.bodywear;
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
    }

    public class HediffCompProperties_ForceWearApparel : HediffCompProperties
    {
        public List<ThingDef> headwear;
        public List<ThingDef> bodywear;
        public List<ThingDef> accessory;
        public HediffCompProperties_ForceWearApparel()
        {
            compClass = typeof(HediffComp_ForceWearApparel);
        }
    }
}
