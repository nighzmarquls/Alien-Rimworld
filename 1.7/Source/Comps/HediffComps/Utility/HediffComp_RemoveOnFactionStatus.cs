using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_RemoveOnFactionStatus : HediffComp
    {
        HediffCompProperties_RemoveOnFactionStatus Props => props as HediffCompProperties_RemoveOnFactionStatus;
        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }
                if(Props.RemoveIfPlayer)
                {
                    return Pawn.Faction == null ? false : Pawn.Faction == Faction.OfPlayer;
                }

                return Pawn.Faction == null ? true : Pawn.HostileTo(Faction.OfPlayer);
            }
        }
    }
    public class HediffCompProperties_RemoveOnFactionStatus : HediffCompProperties
    {
        public bool RemoveIfPlayer = false;
        public HediffCompProperties_RemoveOnFactionStatus()
        {
            this.compClass = typeof(HediffComp_RemoveOnFactionStatus);
        }
    }

}
