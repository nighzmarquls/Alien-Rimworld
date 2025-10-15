using AlienRace;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{ 
    internal class HivecastUtility
    {
        public static bool PsychicConnectionTest(Pawn subject, Pawn caster)
        {
            float subjectSensitivity = subject.GetStatValue(StatDefOf.PsychicSensitivity);
            float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);

            int subjectLevel = subject.GetPsylinkLevel();
            int casterLevel = caster.GetPsylinkLevel();

            float cumulativeSensitvity = (casterSensitivity * subjectSensitivity) / 2;

            if (Rand.Chance(cumulativeSensitvity))
            {
                return true;
            }

            return false;
        }

        public static bool PsychicChallengeTest(Pawn subject, Pawn caster, float chanceOffset = 0)
        {
            float subjectSensitivity = subject.GetStatValue(StatDefOf.PsychicSensitivity);
            float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);

            int subjectLevel = subject.GetPsylinkLevel()+1;
            int casterLevel = caster.GetPsylinkLevel()+1;

            float difference = (subjectLevel*subjectSensitivity) - (casterLevel*casterSensitivity);

            if (difference > casterSensitivity)
            {
                float cumulativeSensitvity = (casterSensitivity * subjectSensitivity) / 4;
                if (Rand.Chance(cumulativeSensitvity + chanceOffset))
                {
                    return true;
                }
            }
            else
            {
                float cumulativeSensitvity = (casterSensitivity * subjectSensitivity) / 2;
                if (Rand.Chance(cumulativeSensitvity + chanceOffset))
                {
                    return true;
                }
            }

            return false;
        }

        private static void HumanDominateHuman(Pawn subject, Pawn caster)
        {

            bool psychicTestPass = HivecastUtility.PsychicConnectionTest(subject, caster);

            if (psychicTestPass)
            {
                if (subject.guest != null)
                {
                    if (subject.guest.will <= 0 && !subject.guest.IsPrisoner)
                    {
                        subject.guest.will = (subject.kindDef.initialWillRange.Value.RandomInRange) / 2;
                    }

                    caster.interactions.TryInteractWith(subject, InteractionDefOf.EnslaveAttempt);

                    if (subject.guest.will <= 0)
                    {
                        subject.guest.SetGuestStatus(caster.Faction, GuestStatus.Slave);
                    }
                }
                else
                {
                    if (TameUtility.CanTame(subject))
                    {
                        subject.SetFaction(caster.Faction);
                    }
                }
            }
            else
            {
                if (subject.TargetCurrentlyAimingAt.Pawn == caster)
                {
                    float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                    subject.TakeDamage(new DamageInfo(DamageDefOf.Stun, 2 * casterSensitivity));
                }
            }
        }

        internal static void EnactDominion(Pawn subject, Pawn caster, bool xenomorphTarget, bool xenomorphCaster)
        {
            CompPawnInfo info = subject.Info();
            if (xenomorphCaster)
            {
                if (xenomorphTarget)
                {
                    if (ModsConfig.IdeologyActive && subject.ideo != null)
                    {
                        subject.ideo.SetIdeo(caster.ideo.Ideo);
                    }
                    if (subject.Faction != caster.Faction)
                    {
                        subject.SetFaction(caster.Faction);
                    }
                    if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, caster))
                    {
                        subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, caster);
                    }
                    subject.GetMorphComp().tamingSocializing = 1f;
                    XMTUtility.GiveMemory(subject, HorrorMoodDefOf.XMT_CommuneWithQueen);
                }
                else
                {
                    if (info != null)
                    {

                        bool psychicTestPass = PsychicChallengeTest(subject, caster);


                        if (info.IsObsessed())
                        {
                            if (subject.guest != null)
                            {
                                subject.guest.SetGuestStatus(caster.Faction, GuestStatus.Slave);
                            }
                            else
                            {
                                if (TameUtility.CanTame(subject))
                                {
                                    subject.SetFaction(caster.Faction);
                                }
                            }
                            MoteMaker.ThrowText(subject.DrawPos, subject.Map, "Submitted");

                        }
                        else if (psychicTestPass)
                        {
                            MoteMaker.ThrowText(subject.DrawPos, subject.Map, "Struggled");
                            float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                            subject.TakeDamage(new DamageInfo(DamageDefOf.Stun, 6 * casterSensitivity));
   
                            if (subject.guest.will <= 0 && !subject.guest.IsPrisoner)
                            {
                                subject.guest.will = (subject.kindDef.initialWillRange.Value.RandomInRange)/2;
                            }

                            caster.interactions.TryInteractWith(subject, InteractionDefOf.EnslaveAttempt);

                            if(subject.guest.will <= 0)
                            {
                                subject.guest.SetGuestStatus(caster.Faction, GuestStatus.Slave);
                            }
                        }
                        else
                        {
                            MoteMaker.ThrowText(subject.DrawPos, subject.Map, "Resisted");
                            float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                            subject.TakeDamage(new DamageInfo(DamageDefOf.Stun, 2 * casterSensitivity));
                        }
                    }
                }
            }
            else
            {
                CompPawnInfo casterInfo = caster.Info();

                if (casterInfo != null)
                {
                    Pawn queen = XMTUtility.GetQueen();
                    if (queen == null)
                    {
                        if (xenomorphTarget)
                        {
                            if (ModsConfig.IdeologyActive && subject.ideo != null)
                            {
                                subject.ideo.SetIdeo(caster.ideo.Ideo);
                            }

                            subject.GetMorphComp().tamingSocializing += 0.25f;
                        }
                        else
                        {
                            HumanDominateHuman(subject, caster);
                        }
                    }
                    else
                    {

                        bool failedPsycastCheck = PsychicChallengeTest(caster, queen);

                        if (xenomorphTarget)
                        {
                            if (failedPsycastCheck)
                            {
                                if (ModsConfig.IdeologyActive && subject.ideo != null)
                                {
                                    caster.ideo.SetIdeo(queen.ideo.Ideo);
                                }
                                if (subject.relations != null && !caster.relations.DirectRelationExists(PawnRelationDefOf.Parent, queen))
                                {
                                    caster.relations.AddDirectRelation(PawnRelationDefOf.Parent, queen);
                                }

                                if (casterInfo.IsObsessed())
                                {
                                    if (caster.Faction != queen.Faction)
                                    {
                                        caster.SetFaction(queen.Faction);
                                    }
                                }
                            }
                            else
                            {
                                casterInfo.ApplyThreatPheromone(queen, 1, 2);
                                caster.stances.stunner.StunFor(2f.SecondsToTicks(), queen, addBattleLog: false);
                            }
                        }
                        else
                        {
                            if (failedPsycastCheck)
                            {
                                if (ModsConfig.IdeologyActive && subject.ideo != null)
                                {
                                    subject.ideo.SetIdeo(queen.ideo.Ideo);
                                }
                                if (subject.Faction != queen.Faction)
                                {
                                    subject.SetFaction(queen.Faction);
                                }
                                if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, queen))
                                {
                                    subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, queen);
                                }
                            }
                            else
                            {
                                HumanDominateHuman(subject, caster);
                            }
                        }
                    }
                }
            }
        }
    }
}
