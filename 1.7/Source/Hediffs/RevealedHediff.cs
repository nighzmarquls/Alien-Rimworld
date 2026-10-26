

using Verse;

namespace Xenomorphtype
{
    public class RevealedHediff : Hediff_Tissue
    {
        public override bool Visible {
            get
            {
                if(pawn.MapHeld != null)
                {
                    if(XMTHiveUtility.PlayerXenosOnMap(pawn.MapHeld))
                    {
                        return true;
                    }

                }
                return base.Visible;
            }
        }


    }
}
