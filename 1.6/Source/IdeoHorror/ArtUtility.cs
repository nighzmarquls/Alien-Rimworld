
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class ArtUtility
    {
        public static ThingDef GetSculptureDefForCorpse(Corpse corpse)
        {
            if(corpse == null || corpse.InnerPawn == null)
            {
                return XenoWorkDefOf.XMT_CorpseSculptureSmall;
            }

            float bodysize = corpse.InnerPawn.BodySize;
            if (bodysize >= 4)
            {
                return XenoWorkDefOf.XMT_CorpseSculptureGrand;
            }
            else if(bodysize > 0.5)
            {
                return XenoWorkDefOf.XMT_CorpseSculptureLarge;
            }
          
            return XenoWorkDefOf.XMT_CorpseSculptureSmall;
            
        }
    }
}
