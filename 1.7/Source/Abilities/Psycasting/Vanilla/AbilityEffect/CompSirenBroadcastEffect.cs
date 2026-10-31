using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace Xenomorphtype
{
    internal class CompSirenBroadcastEffect : CompAbilityEffect
    {
        Pawn caster => parent.pawn;
        public new CompProperties_SirenBroadcast Props => (CompProperties_SirenBroadcast)props;

        private void ApplyToPawn(Pawn pawn)
        {
            if (CanApplyEffects(pawn) && !pawn.Fogged())
            {
                CompPawnInfo info = pawn.Info();
                if (info != null)
                {
                    if (!info.IsObsessed())
                    {
                        float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 20 * casterSensitivity));

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

                Job job = JobMaker.MakeJob(Props.mesmerizedJobDef, new LocalTargetInfo(caster));
                float num = pawn.GetStatValue(StatDefOf.PsychicSensitivity);

                job.expiryInterval = (parent.def.GetStatValueAbstract(StatDefOf.Ability_Duration, parent.pawn) * num).SecondsToTicks();
                job.mote = MoteMaker.MakeThoughtBubble(pawn, parent.def.iconPath, maintain: true);
                RestUtility.WakeUp(pawn);
                pawn.jobs.StopAll();
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
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

            foreach (Pawn pawn in parent.pawn.Map.mapPawns.AllPawnsSpawned)
            {
                ApplyToPawn(pawn);
            }

            Current.Game.GetComponent<GameComponent_XMTWorldState>().RecordSiren(caster, Props.friendlyPrecepts, Props.hostilePrecepts,
                Props.goodwillImpactForFriendly, Props.goodwillImpactForHostile);
            base.Apply(target, dest);
        }

        private bool CanApplyEffects(Pawn p)
        {
            if (p.kindDef.isBoss)
            {
                return false;
            }

            if (!p.Dead && !p.Suspended)
            {
                if (XMTUtility.IsXenomorph(p))
                {
                    return false;
                }

                return p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon;
            }

            return false;
        }

    }
    public class CompProperties_SirenBroadcast : CompProperties_AbilityEffect
    {
        public int goodwillImpactForHostile = -25;

        public int goodwillImpactForFriendly = 25;

        public List<PreceptDef> friendlyPrecepts = null;

        public List<PreceptDef> hostilePrecepts = null;


        public JobDef mesmerizedJobDef;

        public CompProperties_SirenBroadcast()
        {
            compClass = typeof(CompSirenBroadcastEffect);
        }
    }
}
