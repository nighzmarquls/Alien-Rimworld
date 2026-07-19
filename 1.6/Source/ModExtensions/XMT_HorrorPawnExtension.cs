using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class XMT_HorrorPawnExtension : DefModExtension
    {
        public int tier = -1;
        public List<HorrorAdvancementTarget> promotionTargets = new List<HorrorAdvancementTarget>();
        public List<HorrorAdvancementTarget> demotionTargets = new List<HorrorAdvancementTarget>();
    }

    public class HorrorAdvancementTarget
    {
        public PawnKindDef pawnKind;
        public ThingDef thingDef;
        public float weight = 1f;
    }
}
