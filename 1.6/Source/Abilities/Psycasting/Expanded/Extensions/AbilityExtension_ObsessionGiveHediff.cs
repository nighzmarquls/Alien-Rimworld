using RimWorld;
using RimWorld.Planet;

using System.Linq;

using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    public class AbilityExtension_ObsessionGiveHediff : AbilityExtension_AbilityMod
    {
        public HediffDef morphHediff;
        public HediffDef obsessedHediff;
        public HediffDef defaultHediff;
        public BodyPartDef bodyPartToApply;
        public float severity = 0;
        public StatDef durationMultiplier;
        public bool durationMultiplierFromCaster;

        public Hediff ApplyHediff(Pawn targetPawn, Ability ability)
        {
            BodyPartRecord bodyPart = bodyPartToApply != null
                     ? targetPawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == bodyPartToApply)
                     : null;
            var duration = ability.GetDurationForPawn();
            if (durationMultiplier != null)
            {
                duration = (int)(duration * (durationMultiplierFromCaster ?
                                                 ability.pawn.GetStatValue(durationMultiplier) :
                                                 targetPawn.GetStatValue(durationMultiplier)));
            }

            if (XMTUtility.IsXenomorph(targetPawn))
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(targetPawn + " identified as morph adding hediff " + morphHediff);
                }
                return ability.ApplyHediff(targetPawn, morphHediff, bodyPart, duration, severity);
            }
            else
            {
                CompPawnInfo info = targetPawn.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    if (info.IsObsessed())
                    {
                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message(targetPawn + " identified as obsessed adding hediff " + obsessedHediff);
                        }
                        return ability.ApplyHediff(targetPawn, obsessedHediff, bodyPart, duration, severity);
                    }
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(targetPawn + " identified as default adding hediff " + defaultHediff);
                }
                return ability.ApplyHediff(targetPawn, defaultHediff, bodyPart, duration, severity);
            }
        }
        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);


            foreach(GlobalTargetInfo target in targets)
            {
                if(target.Pawn == null)
                {
                    continue;
                }

                ApplyHediff(target.Pawn, ability);
            }
        }
    }
}
