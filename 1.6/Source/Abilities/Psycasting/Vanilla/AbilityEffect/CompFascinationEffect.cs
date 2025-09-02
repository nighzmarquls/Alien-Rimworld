using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class CompFascinationEffect : CompAbilityEffect_WithDest
    {
        Pawn caster => parent.pawn;
        public new CompProperties_AbilityForceJob Props => (CompProperties_AbilityForceJob)props;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!XMTUtility.IsXenomorph(caster))
            {
                CompPawnInfo info = caster.Info();
                if (info != null)
                {
                    info.WitnessPsychicHorror(0.1f);
                    info.GainObsession(0.05f);
                    if (info.IsObsessed())
                    {
                        caster.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                    }
                }
            }

            if (target.Thing is Pawn pawn)
            {
                CompPawnInfo info = pawn.Info();
                if (info != null)
                {
                    if(!info.IsObsessed())
                    {
                        float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 2*casterSensitivity));

                        info.WitnessPsychicHorror(0.1f);
                        info.GainObsession(0.05f);

                        return;
                    }

                    info.WitnessPsychicHorror(0.1f);
                    info.GainObsession(0.1f);
                    if (pawn.needs.joy != null)
                    {
                        pawn.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                    }
                }

                Job job = JobMaker.MakeJob(Props.jobDef, new LocalTargetInfo(GetDestination(target).Cell));
                float num = 1f;
                if (Props.durationMultiplier != null)
                {
                    num = pawn.GetStatValue(Props.durationMultiplier);
                }

                job.expiryInterval = (parent.def.GetStatValueAbstract(StatDefOf.Ability_Duration, parent.pawn) * num).SecondsToTicks();
                job.mote = MoteMaker.MakeThoughtBubble(pawn, parent.def.iconPath, maintain: true);
                RestUtility.WakeUp(pawn);
                pawn.jobs.StopAll();
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }
    }
}
