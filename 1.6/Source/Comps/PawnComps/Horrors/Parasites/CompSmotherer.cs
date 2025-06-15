using RimWorld;

using System.Collections.Generic;
using System.Linq;

using Verse;


namespace Xenomorphtype
{
    public class CompSmotherer : CompHostHunter
    {

        public override Pawn GetPreyTarget()
        {
            foreach (Pawn humanlike in parent.Map.mapPawns.AllHumanlikeSpawned)
            {
                if (!humanlike.Awake() || humanlike.Downed)
                {
                    if (XMTUtility.IsXenomorph(humanlike))
                    {
                        continue;
                    }
                    return humanlike;
                }
            }
            return null;
        }
        public override bool TryResist(Pawn target)
        {
            bool resisted = base.TryResist(target);
            if (resisted)
            {
                return resisted;
            }

            if (target.apparel != null)
            {
                XMTUtility.DamageApparelByBodyPart(target, BodyPartGroupDefOf.FullHead, 160f);
                return target.apparel.BodyPartGroupIsCovered(BodyPartGroupDefOf.FullHead);

            }
            return false;
        }

        public override List<BodyPartRecord> GetTargetBodyParts(Pawn target)
        {
            return (from x in target.health.hediffSet.GetNotMissingParts()
                   where
                    XMTUtility.IsPartHead(x)
                   select x).ToList();
        }
        public override void PreattachTarget(Pawn target)
        {
            base.PreattachTarget(target);
            if (target?.story != null)
            {
                target.story.hairDef = HairDefOf.Bald;
            }

            if (target?.style != null)
            {
                target.style.beardDef = BeardDefOf.NoBeard;
            }
        }
    }

}
