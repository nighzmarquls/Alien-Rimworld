using Verse;

namespace Xenomorphtype
{
    internal class MentalState_XMT_InorganicSubvertedBerserk : MentalState_XMT_MurderousRage
    {
        protected override Thing FindTarget()
        {
            return XMTMentalStateUtility.FindInorganicSubversionTargetToKill(pawn);
        }
    }
}
