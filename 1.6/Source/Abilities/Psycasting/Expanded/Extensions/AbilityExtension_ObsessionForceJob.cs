
using RimWorld;
using RimWorld.Planet;

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
                Ability.MakeStaticFleck(pawn.DrawPos, pawn.Map, fleckOnTarget, 1f, 0f);
            }
            
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
