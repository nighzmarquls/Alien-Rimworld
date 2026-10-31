using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    public class AbilityExtension_SirenBroadcast : AbilityExtension_AbilityMod
    {
        public int goodwillImpactForHostile = -25;

        public int goodwillImpactForFriendly = 25;

        public List<PreceptDef> friendlyPrecepts = null;

        public List<PreceptDef> hostilePrecepts = null;


        public JobDef mesmerizedJobDef;

        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            if (!XMTUtility.IsXenomorph(ability.pawn))
            {
                CompPawnInfo info = ability.pawn.Info();
                if (info != null)
                {
                    info.WitnessPsychicHorror(0.1f);
                    info.GainObsession(0.05f);
                    if (info.IsObsessed())
                    {
                        ability.pawn.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                    }
                }
            }

            foreach (Pawn pawn in ability.pawn.Map.mapPawns.AllPawnsSpawned)
            {
                ApplyToPawn(pawn, ability);
            }

            Current.Game.GetComponent<GameComponent_XMTWorldState>().RecordSiren(ability.pawn, friendlyPrecepts, hostilePrecepts,
                goodwillImpactForFriendly, goodwillImpactForHostile);
        }

        private void ApplyToPawn(Pawn pawn, Ability ability)
        {

            if (CanApplyEffects(pawn) && !pawn.Fogged())
            {
                CompPawnInfo info = pawn.Info();
                if (info != null)
                {
                    if (!info.IsObsessed())
                    {
                        float casterSensitivity = ability.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
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

                Job job = JobMaker.MakeJob(mesmerizedJobDef, new LocalTargetInfo(ability.pawn));


                job.expiryInterval = ability.GetDurationForPawn();
                job.mote = MoteMaker.MakeThoughtBubble(pawn, ability.def.iconPath, maintain: true);
                RestUtility.WakeUp(pawn);
                pawn.jobs.StopAll();
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
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
}