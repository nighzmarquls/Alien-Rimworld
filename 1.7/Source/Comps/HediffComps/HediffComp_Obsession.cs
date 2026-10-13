
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_Obsession : HediffComp
    {
        HediffCompProperties_Obsession Props => props as HediffCompProperties_Obsession;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            CompPawnInfo info = Pawn.Info();
            if (info != null)
            {
               
                info.GainObsession(Props.obsessionGain);
                
            }
        }

        public override void CompPostPostRemoved()
        {
            CompPawnInfo info = Pawn.Info();
            if (info != null)
            {

                info.GainObsession(-Props.obsessionGain);

            }
        }
    }

    public class HediffCompProperties_Obsession : HediffCompProperties
    {
        public float obsessionGain = 0.1f;
        public HediffCompProperties_Obsession()
        {
            compClass = typeof(HediffComp_Obsession);
        }
    }
}
