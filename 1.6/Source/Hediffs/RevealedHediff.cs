

using Verse;

namespace Xenomorphtype
{
    public class RevealedHediff : HediffWithComps
    {
        public override bool Visible {
            get
            {
                if(pawn.MapHeld != null)
                {
                    if(HiveUtility.PlayerXenosOnMap(pawn.MapHeld))
                    {
                        return true;
                    }

                }
                return base.Visible;
            }
        }


    }
}
