
using RimWorld;
using RimWorld.Planet;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Verse;
using Verse.AI;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;


namespace Xenomorphtype
{
    public class AbilityExtension_ObsessionForceJob : AbilityExtension_AbilityMod
    {
        public JobDef jobDef;

        public StatDef durationMultiplier;

        public FleckDef fleckOnTarget;
        protected void ForceJob(Pawn pawn, Ability ability)
        { 
           
            Job job = JobMaker.MakeJob(jobDef, ability.pawn);
            float num = 1f;
            if (durationMultiplier != null)
            {
                num = pawn.GetStatValue(durationMultiplier);
            }
            job.expiryInterval = (int)(ability.GetDurationForPawn() * num);
            job.mote = MoteMaker.MakeThoughtBubble(pawn, ability.def.iconPath, maintain: true);
            pawn.jobs.StopAll();
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            if (fleckOnTarget != null)
            {
                MakeStaticFleckWithSanitizedVector(pawn.DrawPos, pawn.Map, fleckOnTarget);
            }
            
        }

        // Temporary VEF 1.6 -> RimWorld 1.7 bridge: pass scalar components through the
        // overload's runtime Vector3 type without referencing UnityEngine.CoreModule.
        // Revert this helper to Ability.MakeStaticFleck(loc, map, fleckDef, 1f, 0f)
        // when VEF is rebuilt against 1.7's UnityStubs.
        private static void MakeStaticFleckWithSanitizedVector(Vector3 loc, Map map, FleckDef fleckDef)
        {
            MethodInfo makeStaticFleck = typeof(Ability).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                {
                    if (method.Name != nameof(Ability.MakeStaticFleck))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 5
                        && parameters[0].ParameterType.FullName == "UnityEngine.Vector3"
                        && parameters[1].ParameterType == typeof(Map)
                        && parameters[2].ParameterType == typeof(FleckDef)
                        && parameters[3].ParameterType == typeof(float)
                        && parameters[4].ParameterType == typeof(float);
                });
            Type vectorType = makeStaticFleck.GetParameters()[0].ParameterType;
            object sanitizedLoc = vectorType == typeof(Vector3)
                ? (object)loc
                : Activator.CreateInstance(vectorType, new object[] { loc.x, loc.y, loc.z });
            makeStaticFleck.Invoke(null, new object[] { sanitizedLoc, map, fleckDef, 1f, 0f });
        }

        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);
            bool xenomorphCaster = XMTUtility.IsXenomorph(ability.pawn);
            Pawn caster = ability.pawn;
            if (!xenomorphCaster)
            {
                CompPawnInfo info = caster.Info();
                if (info != null)
                {
                    if (info.IsObsessed())
                    {
                        caster.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                    }
                }
            }
            foreach (GlobalTargetInfo target in targets)
            {
                Pawn subject = target.Pawn;
                if (subject == null)
                {
                    continue;
                }

                if (XMTUtility.IsXenomorph(subject))
                {
                    continue;
                }

                float offsetChance = caster.GetPsylinkLevel() * 0.05f;
                CompPawnInfo info = subject.Info();

                if (info.IsObsessed())
                {
                    ForceJob(subject, ability);
                    if (subject?.needs.joy != null)
                    {
                        subject.needs.joy.GainJoy(0.5f, InternalDefOf.Communion);
                    }
                    XMTUtility.GiveMemory(subject, HorrorMoodDefOf.XMT_CommuneWithQueen);
                }
                else if (HivecastUtility.PsychicChallengeTest(subject, caster, offsetChance))
                {
                    ForceJob(subject, ability);
                }
                else
                {
                    float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                    subject.TakeDamage(new DamageInfo(DamageDefOf.Stun, caster.GetPsylinkLevel() * casterSensitivity));
                }
            }
        }
    }


}
