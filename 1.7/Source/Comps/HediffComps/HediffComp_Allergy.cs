
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_Allergy : HediffComp
    {
        HediffCompProperties_Allergy Props => props as HediffCompProperties_Allergy;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);

            if (XMTUtility.IsXenomorph(Pawn))
            {
                return;
            }

            Hediff immunity = Pawn.health.hediffSet.GetFirstHediffOfDef(Props.allergyImmuneHediff);
            if(immunity != null)
            {
                return;
            }

            Hediff allergy = Pawn.health.hediffSet.GetFirstHediffOfDef(Props.allergyHediff);

            if (allergy == null)
            {
                if(Rand.Chance(Props.allergyChance))
                {
                    Pawn.health.GetOrAddHediff(Props.allergyHediff);
                }
                else
                {
                    Pawn.health.GetOrAddHediff(Props.allergyImmuneHediff);
                    return;
                }
            }

            parent.Severity *= 3;
        }
    }

    public class HediffCompProperties_Allergy : HediffCompProperties
    {
        public float allergyChance = 0.1f;
        public HediffDef allergyHediff = null;
        public HediffDef allergyImmuneHediff = null;
        public HediffCompProperties_Allergy()
        {
            compClass = typeof(HediffComp_Allergy);
        }
    }
}
